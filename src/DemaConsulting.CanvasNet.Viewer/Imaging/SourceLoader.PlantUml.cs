using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.PlantUml;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     PlantUML sources of <see cref="SourceLoader"/>: a sequence, activity, class, object, state, use case, component, or
///     deployment diagram drawn from its text.
/// </summary>
/// <remarks>
///     <para>
///         A diagram has no pages. Its natural size is the size the library lays it out at (<c>Measure</c>, PlantUML's SVG
///         size rounded up to whole pixels, times the diagram's own <c>scale</c>). Without <c>--width</c> and
///         <c>--height</c> the diagram is drawn at that size times <c>--scale</c>; with them it is drawn sharply into the
///         box, fitted inside it and centered when both sides are given. The diagram keeps its own background (white, as
///         PlantUML draws it, unless its <c>skinparam backgroundColor</c> says otherwise) unless the background of the
///         options replaces it; a background with zero alpha gives a transparent image. There is no theme and there are no
///         feature positions: the <c>--theme</c> and <c>--positions</c> options of the tool are refused for a PlantUML
///         diagram, and a <c>!theme</c> line of the diagram is reported as unsupported.
///     </para>
///     <para>
///         The <c>!include</c> directives of the diagram are resolved by the PlantUML library: a relative path is looked up
///         from the folder of the file that names it, and the file is read under <see cref="SourceOptions.FileAccess"/>
///         (every regular local file by default); a URL or a standard-library include is never fetched. An include whose
///         file cannot be read is a <c>plantuml-error</c>, and a file the policy denies is the error
///         <c>file-access-denied</c>.
///     </para>
///     <para>
///         A render is static and offline and uses only the fonts bundled with the libraries, so the same diagram gives the
///         same pixels on every machine. A warning of the library is the note <c>plantuml-warning</c> with the specific
///         <c>plantuml-*</c> code in <see cref="ImageDiagnostic.Detail"/>; a construct the library recognizes but does not
///         draw is the unsupported note named by its feature (a diagram type that is not drawn, such as a legacy
///         activity diagram, is <c>plantuml-diagram-unsupported</c> and an empty picture); with
///         <see cref="SourceOptions.Strict"/> it is an
///         <c>unsupported-feature</c> error instead. Text that is not a valid diagram, and a diagram that cannot be laid
///         out, is a <c>plantuml-error</c> with the location and the hint of the library; a limit of the library (nodes,
///         edges, includes, nesting, layout work, painted pixels) is <c>budget-exceeded</c>; a text longer than the text
///         limit is <c>corrupt-input</c>; and a target size larger than the maximum image size is <c>size-limit</c>.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads what a PlantUML diagram declares and the size it is laid out at, by parsing and measuring it; the
    ///     diagram is not drawn.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <param name="fileAccess">The file-access policy of the files the diagram includes.</param>
    /// <param name="limits">The resource limits of the diagram, or <see langword="null"/> for <see cref="PlantUmlLimits.Default"/>.</param>
    /// <returns>The facts about the file: the natural size of the diagram, 4 channels, and its type, title, and counts.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>plantuml-error</c> when the text is not a valid diagram or cannot be laid out,
    ///     <c>budget-exceeded</c> when a limit is exceeded, <c>file-access-denied</c> when the policy refuses an include,
    ///     and <c>corrupt-input</c> when the text is longer than the limit.
    /// </exception>
    private static SourceInfo ProbePlantUml(SourceInput input, long bytes, FileAccessPolicy fileAccess, PlantUmlLimits? limits)
    {
        var path = input.Name;
        var document = Read(path, SourceKind.PlantUml, () => OpenPlantUml(input, fileAccess, limits));
        var size = Read(path, SourceKind.PlantUml, () => MeasurePlantUml(document, limits, path, CancellationToken.None));
        var diagram = new DiagramFacts(
            document.DiagramType,
            document.IsSupported,
            null,
            document.Title,
            null,
            document.NodeCount,
            document.EdgeCount,
            document.SubgraphCount);
        return new SourceInfo(path, SourceKind.PlantUml, bytes, size.Width, size.Height, 4, true, true, 1, 1, null, [], [], [])
        {
            Diagram = diagram
        };
    }

    /// <summary>
    ///     Parses a PlantUML diagram from its source: the name of the source is the file name of the library's locations,
    ///     the folder of the source is where its relative includes are looked up, and the policy decides which files they
    ///     may read.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="fileAccess">The file-access policy of the includes.</param>
    /// <param name="limits">The resource limits of the diagram, or <see langword="null"/> for <see cref="PlantUmlLimits.Default"/>.</param>
    /// <returns>The parsed diagram.</returns>
    private static PlantUmlDocument OpenPlantUml(SourceInput input, FileAccessPolicy fileAccess, PlantUmlLimits? limits)
    {
        using var stream = input.Open();
        return PlantUmlDocument.Open(
            stream,
            new PlantUmlOpenOptions { SourceName = input.Name, BaseDirectory = input.Directory, FileAccess = fileAccess, Limits = limits });
    }

    /// <summary>
    ///     Measures the natural size of a PlantUML diagram without reporting anything.
    /// </summary>
    /// <param name="document">The diagram.</param>
    /// <param name="limits">The resource limits of the diagram, or <see langword="null"/> for <see cref="PlantUmlLimits.Default"/>.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <param name="cancellationToken">The token that cancels the measure.</param>
    /// <returns>
    ///     The natural size, which may be larger than <see cref="Surface.MaxDimension"/> (the size options of the load then
    ///     bring it within the limit, or the load is refused with <c>size-limit</c>).
    /// </returns>
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> or <c>plantuml-error</c> for an argument failure of the library.</exception>
    internal static PlantUmlSize MeasurePlantUml(PlantUmlDocument document, PlantUmlLimits? limits, string path, CancellationToken cancellationToken)
    {
        try
        {
            return document.Measure(new PlantUmlRenderOptions { Limits = limits ?? PlantUmlLimits.Default }, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "scale")
        {
            // The library refuses a natural size beyond the largest surface at scale 1 and names the size, in the words of
            // the Mermaid library; the diagram still has that size, and a smaller scale, width, or height draws it
            var match = MermaidOversizeMessage.Match(ex.Message);
            if (match.Success
                && int.TryParse(match.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                && int.TryParse(match.Groups["height"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height))
            {
                return new PlantUmlSize(width, height);
            }

            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"'{path}' lays out larger than the maximum of {Surface.MaxDimension} pixels per side: {OneLine(ex.Message)}",
                "Split the diagram, or draw it at a smaller scale, width, or height.",
                ex);
        }
        catch (ArgumentException ex)
        {
            throw new ViewerException(ErrorCodes.PlantUmlError, $"PlantUML measure error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Maps a failure of the PlantUML library (a syntax error, an include that cannot be read, or a failed layout or
    ///     render) to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="code">The stable <c>plantuml-*</c> code of the failure.</param>
    /// <param name="stage">The stage that failed, for the message: <c>syntax</c> or <c>render</c>.</param>
    /// <param name="message">The message of the library, which names the file and the position.</param>
    /// <param name="hint">The suggested fix of the library, or <see langword="null"/>.</param>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>file-access-denied</c> when the file-access policy refused an include,
    ///     <c>budget-exceeded</c> for <c>plantuml-limit-exceeded</c> (a limit on nodes, edges, includes, nesting, label
    ///     length, layout work, or painted pixels), otherwise <c>plantuml-error</c>; the hint of the library is kept, and
    ///     the location and the <c>plantuml-*</c> code of the failure are the <see cref="ViewerException.Location"/> and
    ///     <see cref="ViewerException.DetailCode"/>.
    /// </returns>
    internal static ViewerException MapPlantUml(string code, string stage, string message, string? hint, Exception exception)
    {
        if (IsFileAccessDenial(exception))
        {
            return SourceFailure(ErrorCodes.FileAccessDenied, $"PlantUML include denied: {OneLine(message)}", FileAccessDeniedHint, exception);
        }

        if (code == PlantUmlDiagnosticCodes.LimitExceeded)
        {
            return SourceFailure(
                ErrorCodes.BudgetExceeded,
                $"The PlantUML diagram exceeds a limit: {OneLine(message)}",
                hint ?? "Simplify the diagram.",
                exception);
        }

        return SourceFailure(ErrorCodes.PlantUmlError, $"PlantUML {stage} error: {OneLine(message)}", hint, exception);
    }

    /// <summary>
    ///     Creates the options of the render of a PlantUML diagram: the background, the strictness, and the handlers that
    ///     turn the reports of the library into notes.
    /// </summary>
    /// <param name="background">The color that replaces the diagram's own background, or <see langword="null"/> to keep it.</param>
    /// <param name="notes">The list the warnings and unsupported features are added to.</param>
    /// <param name="strict"><see langword="true"/> to make the first unsupported construct an error.</param>
    /// <param name="scale">The factor of the natural size, for a render at the natural size.</param>
    /// <param name="limits">The resource limits of the diagram, or <see langword="null"/> for <see cref="PlantUmlLimits.Default"/>.</param>
    /// <returns>The options.</returns>
    private static PlantUmlRenderOptions CreatePlantUmlOptions(Rgba32? background, List<ImageDiagnostic> notes, bool strict, double scale, PlantUmlLimits? limits)
    {
        return new PlantUmlRenderOptions
        {
            Scale = scale,
            Limits = limits ?? PlantUmlLimits.Default,
            Background = background,
            ThrowOnUnsupportedFeature = strict,
            WarningHandler = warning => notes.Add(
                new ImageDiagnostic("plantuml", "warning", PlantUmlImageSource.WarningCode, OneLine(warning.ToString()))
                {
                    Detail = warning.Code,
                    Location = ToPlantUmlLocation(warning.Location)
                }),
            UnsupportedFeatureHandler = unsupported => notes.Add(
                new ImageDiagnostic("plantuml", "unsupported", unsupported.Feature, DescribePlantUmlUnsupported(unsupported))
                {
                    Location = ToPlantUmlLocation(unsupported.Location)
                })
        };
    }

    /// <summary>
    ///     Describes a construct the PlantUML library does not draw, with the location and the hint of the library.
    /// </summary>
    /// <param name="unsupported">The report.</param>
    /// <returns>One line, for example <c>flow.puml:2:1: activity-legacy diagrams are not drawn by this version (...)</c>.</returns>
    private static string DescribePlantUmlUnsupported(PlantUmlUnsupportedFeature unsupported)
    {
        var text = unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message;
        return OneLine(unsupported.Hint is { Length: > 0 } hint ? $"{text} ({hint})" : text);
    }

    /// <summary>
    ///     Converts the position the PlantUML library reports to the location of a note.
    /// </summary>
    /// <param name="location">The position in the text.</param>
    /// <returns>The location, or <see langword="null"/> when the library does not know it.</returns>
    private static ImageDiagnosticLocation? ToPlantUmlLocation(PlantUmlLocation location)
    {
        if (!location.IsKnown)
        {
            return null;
        }

        int? column = location.Column > 0 ? location.Column : null;
        return new ImageDiagnosticLocation(location.File, location.Line, column, null);
    }

    /// <summary>
    ///     A PlantUML diagram opened for drawing.
    /// </summary>
    private sealed class PlantUmlPageSource : PageSource
    {
        /// <summary>
        ///     The parsed diagram.
        /// </summary>
        private readonly PlantUmlDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PlantUmlPageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed diagram.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private PlantUmlPageSource(PlantUmlDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a PlantUML diagram.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be read or is not a valid diagram.</exception>
        public static PlantUmlPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context)
        {
            var document = Read(info.Path, SourceKind.PlantUml, () => OpenPlantUml(input, options.FileAccess, context.Limits.PlantUml));
            return new PlantUmlPageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var notes = new List<ImageDiagnostic>();
            var (width, height) = ComputeTargetSize(Info.Path, Info.Width, Info.Height, Options);

            // Without a width or a height the diagram is drawn at its natural size times the scale (the library rounds the
            // size the same way as ComputeTargetSize); a box is fitted
            var exact = Options.Width is null && Options.Height is null;
            var renderOptions = CreatePlantUmlOptions(Options.Background, notes, Options.Strict, exact ? Options.Scale ?? 1 : 1, Context.Limits.PlantUml);

            // The surface is charged before the diagram is drawn, so a diagram that cannot be paid for costs nothing
            Context.ChargeSurface(width, height);
            Surface surface;
            try
            {
                surface = Read(
                    Info.Path,
                    SourceKind.PlantUml,
                    () => exact
                        ? _document.Render(renderOptions, Context.CancellationToken)
                        : _document.Render(width, height, renderOptions, Context.CancellationToken));
            }
            catch (ArgumentException ex)
            {
                // The target size was validated before the render, so such a failure is a diagram the library cannot draw
                throw new ViewerException(ErrorCodes.PlantUmlError, $"PlantUML render error: {OneLine(ex.Message)}", null, ex);
            }

            var fields = new List<(string Name, object Value)>
            {
                ("width", surface.Width),
                ("height", surface.Height)
            };
            if (Options.Background is { } background)
            {
                fields.Add(("background", FormatColor(background)));
            }

            var operations = new List<AppliedOperation> { Operation("render", [.. fields]) };
            return new LoadedSource(surface, Info, 1, true, notes, [], operations);
        }
    }
}
