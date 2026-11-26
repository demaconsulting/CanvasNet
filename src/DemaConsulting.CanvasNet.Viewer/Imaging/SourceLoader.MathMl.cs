using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.MathMl;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     MathML sources of <see cref="SourceLoader"/>: an equation of MathML Core drawn from its markup.
/// </summary>
/// <remarks>
///     <para>
///         An equation has no pages. Its natural size is the size the library lays it out at with the default font size of
///         16 pixels (<c>Measure</c>, the picture rounded out to whole pixels), in the display mode of
///         <see cref="SourceOptions.MathMl"/> (the <c>display=</c> fragment of an input or a link) or else its own (the
///         <c>display</c> attribute of its <c>math</c> element). Without <c>--width</c> and <c>--height</c> the equation is
///         laid out at that size times <c>--scale</c>, so a larger scale gives a sharper picture rather than a magnified one;
///         with them it is drawn sharply into the box, fitted inside it and centered. The equation is drawn over the
///         background of the options, white when there is none (an embedded equation is transparent); a background with
///         zero alpha gives a transparent picture. There is no theme and there are no feature positions: the
///         <c>--theme</c> and <c>--positions</c> options of the tool are refused for an equation.
///     </para>
///     <para>
///         MathML Core has no external references: the equation reads no other file and never uses the network, so the
///         file-access policy has nothing to judge. A render is static and offline and uses only the math font bundled with
///         the core (Noto Sans Math, with Noto Sans as the fallback), so the same equation gives the same pixels on every
///         machine. A warning of the library is the note <c>mathml-warning</c> with the specific <c>mathml-*</c> code in
///         <see cref="ImageDiagnostic.Detail"/> and the line and column in <see cref="ImageDiagnostic.Location"/>; an
///         element, attribute, or style the library recognizes but does not draw is the unsupported note named by its
///         feature (<c>mathml-element-unsupported</c>, <c>mathml-attribute-unsupported</c>, <c>mathml-css-unsupported</c>);
///         with <see cref="SourceOptions.Strict"/> it is an <c>unsupported-feature</c> error instead. Text that is not a
///         valid equation (including an empty file, located at line 1, column 1), and an equation that cannot be laid out,
///         is a <c>mathml-error</c> with the location and the hint of the library; a limit of the library (elements,
///         nesting, token length, table cells, layout work, painted glyphs, and an equation larger than the maximum image
///         size at its natural size) is <c>budget-exceeded</c>; a text longer than the text limit is
///         <c>corrupt-input</c>; and a target size larger than the maximum image size is <c>size-limit</c>.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads what a MathML equation declares and the size it is laid out at, by parsing and measuring it; the equation
    ///     is not drawn.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <param name="display">The display mode to lay the equation out in, or <see langword="null"/> for its own.</param>
    /// <param name="limits">The resource limits of the equation, or <see langword="null"/> for <see cref="MathMlLimits.Default"/>.</param>
    /// <returns>The facts about the file: the natural size of the equation, 4 channels with alpha, one page, and its equation facts.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>mathml-error</c> when the text is not a valid equation or cannot be laid out,
    ///     <c>budget-exceeded</c> when a limit is exceeded, and <c>corrupt-input</c> when the text is longer than the limit.
    /// </exception>
    private static SourceInfo ProbeMathMl(SourceInput input, long bytes, MathMlDisplay? display, MathMlLimits? limits)
    {
        var path = input.Name;
        var document = Read(path, SourceKind.MathMl, () => OpenMathMl(input, limits));
        var size = MeasureMathMl(document, display, 1, limits, path, CancellationToken.None);
        var laidOut = display ?? document.Display;
        var equation = new EquationFacts(
            DisplayName(laidOut),
            document.ElementCount,
            document.AltText,
            [.. document.Annotations.Select(annotation => annotation.Encoding ?? string.Empty)],
            size.Baseline);
        return new SourceInfo(path, SourceKind.MathMl, bytes, size.Width, size.Height, 4, true, true, 1, 1, null, [], [], [])
        {
            Equation = equation
        };
    }

    /// <summary>
    ///     Probes an equation again in the display mode the fragment of its input names, keeping the selection and the
    ///     policy of the first probe.
    /// </summary>
    /// <param name="info">The facts of the first probe, with the selection of the input.</param>
    /// <param name="display">The display mode the fragment names.</param>
    /// <returns>The facts in that display mode; the same facts when the first probe already used it.</returns>
    /// <exception cref="ViewerException">Thrown for the failures of the first probe (the file may have changed since).</exception>
    private static SourceInfo ReprobeMathMl(SourceInfo info, MathMlDisplay display)
    {
        if (info.Format != SourceKind.MathMl || info.Equation?.Display == DisplayName(display))
        {
            return info;
        }

        var probed = ProbeMathMl(SourceInput.FromFile(info.Path), info.Bytes, display, null);
        return probed with { Selection = info.Selection, FileAccess = info.FileAccess };
    }

    /// <summary>
    ///     Gets the name of a display mode as the facts, the operations, and the fragment write it.
    /// </summary>
    /// <param name="display">The display mode.</param>
    /// <returns><c>block</c> or <c>inline</c>.</returns>
    private static string DisplayName(MathMlDisplay display)
    {
        return display == MathMlDisplay.Block ? "block" : "inline";
    }

    /// <summary>
    ///     Parses an equation from its source; the name of the source is the file name of the library's locations.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="limits">The resource limits of the equation, or <see langword="null"/> for <see cref="MathMlLimits.Default"/>.</param>
    /// <returns>The parsed equation.</returns>
    private static MathMlDocument OpenMathMl(SourceInput input, MathMlLimits? limits)
    {
        using var stream = input.Open();
        return MathMlDocument.Open(stream, new MathMlOpenOptions { SourceName = input.Name, Limits = limits });
    }

    /// <summary>
    ///     Measures the size of an equation at a scale without reporting anything.
    /// </summary>
    /// <param name="document">The equation.</param>
    /// <param name="display">The display mode, or <see langword="null"/> for the equation's own.</param>
    /// <param name="scale">The factor of the natural size.</param>
    /// <param name="limits">The resource limits of the equation, or <see langword="null"/> for <see cref="MathMlLimits.Default"/>.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <param name="cancellationToken">The token that cancels the measure.</param>
    /// <returns>The size in pixels of the picture at that scale and the row of its baseline.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>mathml-error</c> when the equation cannot be laid out, and <c>budget-exceeded</c> when a
    ///     limit is exceeded (a picture larger than the maximum image size included).
    /// </exception>
    internal static MathMlSize MeasureMathMl(
        MathMlDocument document,
        MathMlDisplay? display,
        double scale,
        MathMlLimits? limits,
        string path,
        CancellationToken cancellationToken)
    {
        var options = new MathMlRenderOptions { Display = display, Scale = scale, Limits = limits ?? MathMlLimits.Default };
        return Read(path, SourceKind.MathMl, () => document.Measure(options, cancellationToken));
    }

    /// <summary>
    ///     Maps a failure of the MathML library (a syntax error or a failed layout or render) to a
    ///     <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="code">The stable <c>mathml-*</c> code of the failure.</param>
    /// <param name="stage">The stage that failed, for the message: <c>syntax</c> or <c>render</c>.</param>
    /// <param name="message">The message of the library, which names the file and the position.</param>
    /// <param name="hint">The suggested fix of the library, or <see langword="null"/>.</param>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>budget-exceeded</c> for <c>mathml-limit-exceeded</c> (a limit on elements, nesting,
    ///     token length, table cells, layout work, painted glyphs, or the size of the picture), otherwise
    ///     <c>mathml-error</c> (for <c>mathml-syntax-error</c> and <c>mathml-render-failed</c>); the hint of the library is
    ///     kept, and the location and the <c>mathml-*</c> code of the failure are the
    ///     <see cref="ViewerException.Location"/> and <see cref="ViewerException.DetailCode"/>.
    /// </returns>
    internal static ViewerException MapMathMl(string code, string stage, string message, string? hint, Exception exception)
    {
        if (code == MathMlDiagnosticCodes.LimitExceeded)
        {
            return SourceFailure(
                ErrorCodes.BudgetExceeded,
                $"The MathML equation exceeds a limit: {OneLine(message)}",
                hint ?? "Simplify the equation.",
                exception);
        }

        return SourceFailure(ErrorCodes.MathMlError, $"MathML {stage} error: {OneLine(message)}", hint, exception);
    }

    /// <summary>
    ///     Creates the options of the render of an equation: the display mode, the background, the strictness, and the
    ///     handlers that turn the reports of the library into notes.
    /// </summary>
    /// <param name="display">The display mode, or <see langword="null"/> for the equation's own.</param>
    /// <param name="background">The color the equation is drawn over; one with zero alpha leaves the picture transparent.</param>
    /// <param name="notes">The list the warnings and unsupported features are added to.</param>
    /// <param name="strict"><see langword="true"/> to make the first unsupported construct an error.</param>
    /// <param name="scale">The factor of the natural size, for a render at the natural size.</param>
    /// <param name="limits">The resource limits of the equation, or <see langword="null"/> for <see cref="MathMlLimits.Default"/>.</param>
    /// <returns>The options.</returns>
    private static MathMlRenderOptions CreateMathMlOptions(
        MathMlDisplay? display,
        Rgba32 background,
        List<ImageDiagnostic> notes,
        bool strict,
        double scale,
        MathMlLimits? limits)
    {
        return new MathMlRenderOptions
        {
            Display = display,
            Scale = scale,
            Limits = limits ?? MathMlLimits.Default,
            Background = background.A == 0 ? null : background,
            ThrowOnUnsupportedFeature = strict,
            WarningHandler = warning => notes.Add(
                new ImageDiagnostic("mathml", "warning", MathMlImageSource.WarningCode, OneLine(warning.ToString()))
                {
                    Detail = warning.Code,
                    Location = ToMathMlLocation(warning.Location)
                }),
            UnsupportedFeatureHandler = unsupported => notes.Add(
                new ImageDiagnostic("mathml", "unsupported", unsupported.Feature, DescribeMathMlUnsupported(unsupported))
                {
                    Location = ToMathMlLocation(unsupported.Location)
                })
        };
    }

    /// <summary>
    ///     Describes a construct the MathML library does not draw, with the location and the hint of the library.
    /// </summary>
    /// <param name="unsupported">The report.</param>
    /// <returns>One line, for example <c>eq.mml:3:5: the element 'mfenced' is not part of MathML Core (write the fences as mo)</c>.</returns>
    private static string DescribeMathMlUnsupported(MathMlUnsupportedFeature unsupported)
    {
        var text = unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message;
        return OneLine(unsupported.Hint is { Length: > 0 } hint ? $"{text} ({hint})" : text);
    }

    /// <summary>
    ///     Converts the position the MathML library reports to the location of a note.
    /// </summary>
    /// <param name="location">The position in the text.</param>
    /// <returns>The location, or <see langword="null"/> when the library does not know it.</returns>
    private static ImageDiagnosticLocation? ToMathMlLocation(MathMlLocation location)
    {
        if (!location.IsKnown)
        {
            return null;
        }

        int? column = location.Column > 0 ? location.Column : null;
        return new ImageDiagnosticLocation(location.SourceName, location.Line, column, null);
    }

    /// <summary>
    ///     A MathML equation opened for drawing.
    /// </summary>
    private sealed class MathMlPageSource : PageSource
    {
        /// <summary>
        ///     The parsed equation.
        /// </summary>
        private readonly MathMlDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MathMlPageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed equation.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private MathMlPageSource(MathMlDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a MathML equation.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be read or is not a valid equation.</exception>
        public static MathMlPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context)
        {
            var document = Read(info.Path, SourceKind.MathMl, () => OpenMathMl(input, context.Limits.MathMl));
            return new MathMlPageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var notes = new List<ImageDiagnostic>();
            var limits = Context.Limits.MathMl;
            var display = Options.MathMl?.Display;

            // The display mode changes the layout, and the probe may have measured the equation in another one (an embedded
            // equation is probed in its own display mode before the fragment of its link is read)
            var info = Info;
            if (display is { } chosen && info.Equation?.Display != DisplayName(chosen))
            {
                var measured = MeasureMathMl(_document, chosen, 1, limits, Info.Path, Context.CancellationToken);
                var facts = Info.Equation is { } probed ? probed with { Display = DisplayName(chosen), Baseline = measured.Baseline } : null;
                info = Info with { Width = measured.Width, Height = measured.Height, Equation = facts };
            }

            // Without a width or a height the equation is laid out at its natural size times the scale (sharp at every scale,
            // and the library rounds the picture out at that scale, so it is measured there); a box is fitted
            var exact = Options.Width is null && Options.Height is null;
            var scale = exact ? Options.Scale ?? 1 : 1;
            var (width, height) = ComputeTargetSize(info.Path, info.Width, info.Height, Options);
            if (exact && Options.Scale is not null)
            {
                var scaled = MeasureMathMl(_document, display, scale, limits, info.Path, Context.CancellationToken);
                (width, height) = (scaled.Width, scaled.Height);
            }

            var background = Options.Background ?? DefaultVectorBackground;
            var renderOptions = CreateMathMlOptions(display, background, notes, Options.Strict, scale, limits);

            // The surface is charged before the equation is drawn, so an equation that cannot be paid for costs nothing
            Context.ChargeSurface(width, height);
            var surface = Read(
                info.Path,
                SourceKind.MathMl,
                () => exact
                    ? _document.Render(renderOptions, Context.CancellationToken)
                    : _document.Render(width, height, renderOptions, Context.CancellationToken));

            var fields = new List<(string Name, object Value)>
            {
                ("width", surface.Width),
                ("height", surface.Height)
            };
            if (display is { } mode)
            {
                fields.Add(("display", DisplayName(mode)));
            }

            if (Options.Background is { } chosenBackground)
            {
                fields.Add(("background", FormatColor(chosenBackground)));
            }

            var operations = new List<AppliedOperation> { Operation("render", [.. fields]) };
            return new LoadedSource(surface, info, 1, true, notes, [], operations);
        }
    }
}
