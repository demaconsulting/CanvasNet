using System.Globalization;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     Mermaid sources of <see cref="SourceLoader"/>: a diagram drawn from its text.
/// </summary>
/// <remarks>
///     <para>
///         A diagram has no pages. Its natural size is the size the library lays it out at (<c>Measure</c>, Mermaid's SVG
///         view box rounded up to whole pixels). Without <c>--width</c> and <c>--height</c> the diagram is drawn at that size
///         times <c>--scale</c> and placed exactly as Mermaid places it; with them it is drawn sharply into the box, fitted
///         inside it and centered when both sides are given. The probe lays the diagram out without the pictures of its
///         nodes (it reads no other file), so a picture node without its <c>w</c> and <c>h</c> can make the drawn diagram a
///         little larger or smaller than the probed size (fitted into the box when there is one); give such a node its size
///         for a layout that does not depend on the picture. A load at the natural size charges the surface it drew (the
///         pixels beyond the probed size are charged after the draw, and the surface is refused when they do not fit) and
///         reports the natural size of the drawn diagram, so the loaded picture's size and charge are the drawn size. The
///         diagram is drawn over the background of the options, white when there is none (Mermaid's own SVG is transparent; a
///         background with zero alpha keeps that). <see cref="SourceOptions.Mermaid"/> chooses the site-level theme, which
///         a theme the text sets itself still overrides, as with <c>mmdc -t</c>.
///     </para>
///     <para>
///         A render is static and offline and uses only the fonts bundled with the libraries, so the same diagram gives the
///         same pixels on every machine. A warning of the library is the note <c>mermaid-warning</c> with the specific
///         <c>mermaid-*</c> code in <see cref="ImageDiagnostic.Detail"/>; a construct the library recognizes but does not
///         draw is the unsupported note named by its feature (a diagram type that is not drawn yet, such as a class
///         diagram, is <c>mermaid-diagram-unsupported</c> and an empty picture); with <see cref="SourceOptions.Strict"/> it
///         is an <c>unsupported-feature</c> error instead. Text that is not a valid diagram, and a diagram that cannot be
///         laid out, is a <c>mermaid-error</c> with the hint of the library; a limit of the library (nodes, edges, layout
///         work, pictures, painted pixels) is <c>budget-exceeded</c>; a text longer than the text limit is
///         <c>corrupt-input</c>; and a target size larger than the maximum image size is <c>size-limit</c> (a diagram
///         whose natural size is larger can still be drawn with a smaller <c>--scale</c>, <c>--width</c>, or
///         <c>--height</c>).
///     </para>
///     <para>
///         <b>Pictures.</b> The <c>@{ img: ... }</c> nodes of a diagram show files that lie beside it, through the same
///         sandboxed resolver an SVG file and a Vega-Lite chart use (<see cref="SvgImages"/>), rooted at the folder of the
///         diagram: a raster, an SVG file, a model (<c>part.scad</c>), a chart, a page of a document, or another diagram,
///         and nothing outside the folder; a remote URL is never fetched. A link that cannot be drawn is the warning
///         <c>svg-image-unresolved</c> (the library's <c>mermaid-image-unresolved</c> for the same link is left out), the
///         files that were drawn are <c>embed</c> operations (one per link: the library asks for a picture twice, once for its
///         natural size and once to draw it), and with <see cref="SourceOptions.Strict"/> a link that was
///         not drawn is an <c>unsupported-feature</c> error. A diagram that is itself embedded
///         (<see cref="LoadContext.UseScopedResolver"/>) uses the resolver of the embedding document and leaves the notes to it.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     The <see cref="ImageDiagnostic.Detail"/> of the library's warning for a picture that was not drawn.
    /// </summary>
    private const string MermaidImageUnresolved = "mermaid-image-unresolved";

    /// <summary>
    ///     The pattern of the message of the library for a natural size that is larger than a surface can be: the size it
    ///     gives at the scale that was asked for.
    /// </summary>
    private static readonly Regex MermaidOversizeMessage = new(
        "gives a size of (?<width>[0-9]+) by (?<height>[0-9]+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Finds the name of a Mermaid theme.
    /// </summary>
    /// <param name="name">The name as typed; compared without regard to case.</param>
    /// <returns>The name of the theme as Mermaid writes it (for example <c>dark</c>), or <see langword="null"/> when it is not a theme.</returns>
    internal static string? FindMermaidTheme(string name)
    {
        return MermaidRenderOptions.ThemeNames.FirstOrDefault(theme => string.Equals(theme, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Reads what a diagram declares and the size it is laid out at, by parsing and measuring it; the diagram is not
    ///     drawn and no picture of it is loaded.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file: the natural size of the diagram, 4 channels, and its type, direction, titles, and counts.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>mermaid-error</c> when the text is not a valid diagram or cannot be laid out,
    ///     <c>budget-exceeded</c> when a limit is exceeded, and <c>corrupt-input</c> when the text is longer than the limit.
    /// </exception>
    private static SourceInfo ProbeMermaid(SourceInput input, long bytes)
    {
        var path = input.Name;
        var document = Read(path, SourceKind.Mermaid, () => OpenMermaid(input));
        var size = Read(path, SourceKind.Mermaid, () => MeasureDiagram(document, null, path, CancellationToken.None));
        var diagram = new DiagramFacts(
            document.DiagramType,
            document.IsSupported,
            document.Direction,
            document.Title,
            document.AccTitle,
            document.NodeCount,
            document.EdgeCount,
            document.SubgraphCount);
        return new SourceInfo(path, SourceKind.Mermaid, bytes, size.Width, size.Height, 4, true, true, 1, 1, null, [], [], [])
        {
            Diagram = diagram
        };
    }

    /// <summary>
    ///     Parses a diagram from its source; the name of the source is the file name of the library's locations.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <returns>The parsed diagram.</returns>
    private static MermaidDocument OpenMermaid(SourceInput input)
    {
        using var stream = input.Open();
        return MermaidDocument.Open(stream, new MermaidOpenOptions { SourceName = input.Name });
    }

    /// <summary>
    ///     Measures the natural size of a diagram, without its pictures and without reporting anything.
    /// </summary>
    /// <param name="document">The diagram.</param>
    /// <param name="theme">The site-level theme, or <see langword="null"/> for the default.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <param name="cancellationToken">The token that cancels the measure.</param>
    /// <returns>
    ///     The natural size, which may be larger than <see cref="Surface.MaxDimension"/> (the size options of the load then
    ///     bring it within the limit, or the load is refused with <c>size-limit</c>).
    /// </returns>
    /// <exception cref="ViewerException">Thrown with code <c>mermaid-error</c> for any other argument failure of the library.</exception>
    internal static MermaidSize MeasureDiagram(MermaidDocument document, string? theme, string path, CancellationToken cancellationToken)
    {
        try
        {
            return document.Measure(new MermaidRenderOptions { Theme = theme }, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "scale")
        {
            // The library refuses a natural size beyond the largest surface at scale 1 and names the size; the diagram still has
            // that size, and a smaller --scale, --width, or --height draws it (a test pins the text of the message)
            var match = MermaidOversizeMessage.Match(ex.Message);
            if (match.Success
                && int.TryParse(match.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                && int.TryParse(match.Groups["height"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height))
            {
                return new MermaidSize(width, height);
            }

            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"'{path}' lays out larger than the maximum of {Surface.MaxDimension} pixels per side: {OneLine(ex.Message)}",
                "Split the diagram, or draw it with a smaller --scale.",
                ex);
        }
        catch (ArgumentException ex)
        {
            throw new ViewerException(ErrorCodes.MermaidError, $"Mermaid measure error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Draws a diagram, mapping the failures of the library to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="render">The render of the diagram.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown for the allow-listed failures of <see cref="Read{T}"/>, with code <c>size-limit</c> when the scaled diagram
    ///     lays out larger than the largest image with its pictures, and with code <c>mermaid-error</c> for any other argument
    ///     failure of the library: the target size was validated before the render, so such a failure is a diagram the
    ///     library cannot draw and never a usage error.
    /// </exception>
    internal static Surface RenderDiagram(Func<Surface> render, string path)
    {
        try
        {
            return Read(path, SourceKind.Mermaid, render);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "scale")
        {
            // The pictures of the nodes made the scaled diagram larger than the largest image
            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"'{path}' is larger than the maximum of {Surface.MaxDimension} pixels per side with its pictures: {OneLine(ex.Message)}",
                "Use a smaller --scale, or --width or --height.",
                ex);
        }
        catch (ArgumentException ex)
        {
            throw new ViewerException(ErrorCodes.MermaidError, $"Mermaid render error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Names the file in a message of the library that has no location (a location starts with the file name).
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="message">The message of the library.</param>
    /// <param name="located"><see langword="true"/> when the message starts with the location, and so with the file name.</param>
    /// <returns>The message, after <c>'path': </c> when it has no location.</returns>
    private static string Named(string path, string message, bool located)
    {
        return located ? message : $"'{path}': {message}";
    }

    /// <summary>
    ///     Maps a failure of the Mermaid library (a syntax error or a failed layout or render) to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="code">The stable <c>mermaid-*</c> code of the failure.</param>
    /// <param name="stage">The stage that failed, for the message: <c>syntax</c> or <c>render</c>.</param>
    /// <param name="message">The message of the library, which names the file and the position.</param>
    /// <param name="hint">The suggested fix of the library, or <see langword="null"/>.</param>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>budget-exceeded</c> for <c>mermaid-limit-exceeded</c> (a limit on nodes, edges,
    ///     nesting, label length, layout work, pictures, or painted pixels), otherwise <c>mermaid-error</c>; the hint of the
    ///     library is kept.
    /// </returns>
    internal static ViewerException MapMermaid(string code, string stage, string message, string? hint, Exception exception)
    {
        if (code == MermaidDiagnosticCodes.LimitExceeded)
        {
            return new ViewerException(
                ErrorCodes.BudgetExceeded,
                $"The Mermaid diagram exceeds a limit: {OneLine(message)}",
                hint ?? "Simplify the diagram; this limit cannot be changed from the command line.",
                exception);
        }

        return new ViewerException(ErrorCodes.MermaidError, $"Mermaid {stage} error: {OneLine(message)}", hint, exception);
    }

    /// <summary>
    ///     Creates the options of the render of a diagram: the theme, the background, the resolver of its pictures, the
    ///     strictness, and the handlers that turn the reports of the library into notes.
    /// </summary>
    /// <param name="theme">The site-level theme, or <see langword="null"/> for the default.</param>
    /// <param name="background">The color the surface is filled with, or <see langword="null"/> for transparent.</param>
    /// <param name="notes">The list the warnings and unsupported features are added to.</param>
    /// <param name="resolver">The resolver of the pictures of the nodes, or <see langword="null"/> for none.</param>
    /// <param name="strict"><see langword="true"/> to make the first unsupported construct an error.</param>
    /// <param name="scale">The factor of the natural size, for a render at the natural size.</param>
    /// <returns>The options.</returns>
    private static MermaidRenderOptions CreateMermaidOptions(
        string? theme,
        Rgba32? background,
        List<ImageDiagnostic> notes,
        IImageResolver? resolver,
        bool strict,
        double scale)
    {
        return new MermaidRenderOptions
        {
            Scale = scale,
            Theme = theme,
            Background = background,
            ImageResolver = resolver,
            ThrowOnUnsupportedFeature = strict,
            WarningHandler = warning => notes.Add(
                new ImageDiagnostic("mermaid", "warning", MermaidImageSource.WarningCode, OneLine(warning.ToString()))
                {
                    Detail = warning.Code,
                    Location = ToMermaidLocation(warning.Location)
                }),
            UnsupportedFeatureHandler = unsupported => notes.Add(
                new ImageDiagnostic("mermaid", "unsupported", unsupported.Feature, DescribeUnsupported(unsupported))
                {
                    Location = ToMermaidLocation(unsupported.Location)
                })
        };
    }

    /// <summary>
    ///     Describes a construct the library does not draw, with the location and the hint of the library.
    /// </summary>
    /// <param name="unsupported">The report.</param>
    /// <returns>
    ///     One line, for example <c>flow.mmd:1:1: 'journey' diagrams (journey) are not drawn by this version; the
    ///     picture is empty (journey diagrams are not supported; supported in this version: flowchart, graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram, pie, gantt)</c>.
    /// </returns>
    private static string DescribeUnsupported(MermaidUnsupportedFeature unsupported)
    {
        var text = unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message;
        return OneLine(unsupported.Hint is { Length: > 0 } hint ? $"{text} ({hint})" : text);
    }

    /// <summary>
    ///     Keeps the last <c>embed</c> operation of every link, in the order of those last operations.
    /// </summary>
    /// <param name="operations">The <c>embed</c> operations of a render, in the order the pictures were requested.</param>
    /// <returns>One operation per link: the one of the request that drew the picture.</returns>
    private static List<AppliedOperation> LastPerLink(List<AppliedOperation> operations)
    {
        static object Href(AppliedOperation operation) => operation.Fields.First(field => field.Key == "href").Value;
        return [.. operations.Where((operation, index) => !operations.Skip(index + 1).Any(later => Equals(Href(later), Href(operation))))];
    }

    /// <summary>
    ///     Gives the natural size of a side a diagram was drawn at, from the side of the surface and the scale.
    /// </summary>
    /// <param name="drawn">The side of the surface in pixels.</param>
    /// <param name="scale">The scale the diagram was drawn at.</param>
    /// <returns>
    ///     The natural side in pixels, at least one: exact at a scale of 1 or more (the drawn side is the natural side
    ///     times the scale, rounded), and the nearest consistent natural side below 1.
    /// </returns>
    private static int Unscale(int drawn, double scale)
    {
        return Math.Max(1, (int)Math.Round(drawn / scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Converts the position the Mermaid library reports to the location of a note.
    /// </summary>
    /// <param name="location">The position in the text.</param>
    /// <returns>The location, or <see langword="null"/> when the library does not know it.</returns>
    private static ImageDiagnosticLocation? ToMermaidLocation(MermaidLocation location)
    {
        if (!location.IsKnown)
        {
            return null;
        }

        int? column = location.Column > 0 ? location.Column : null;
        return new ImageDiagnosticLocation(location.File, location.Line, column, null);
    }

    /// <summary>
    ///     A diagram opened for drawing.
    /// </summary>
    private sealed class MermaidPageSource : PageSource
    {
        /// <summary>
        ///     The parsed diagram.
        /// </summary>
        private readonly MermaidDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MermaidPageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed diagram.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private MermaidPageSource(MermaidDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a diagram.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be read or is not a valid diagram.</exception>
        public static MermaidPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context)
        {
            var document = Read(info.Path, SourceKind.Mermaid, () => OpenMermaid(input));
            return new MermaidPageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var notes = new List<ImageDiagnostic>();

            // A theme can change the layout (its font or spacing), and the probe measured the diagram with the default one
            var theme = Options.Mermaid?.Theme;
            var info = Info;
            if (theme is not null)
            {
                var measured = Read(Info.Path, SourceKind.Mermaid, () => MeasureDiagram(_document, theme, Info.Path, Context.CancellationToken));
                info = Info with { Width = measured.Width, Height = measured.Height };
            }

            var (width, height) = ComputeTargetSize(info.Path, info.Width, info.Height, Options);

            // The pictures of the nodes: through the resolver of the embedding document when there is one, else through a
            // resolver confined to the folder of the diagram
            var scoped = Context.UseScopedResolver;
            var images = scoped ? null : new SvgImages(Info.Path, Options);
            var background = Options.Background ?? new Rgba32(255, 255, 255, 255);
            // Without a width or a height the diagram is drawn at its natural size times the scale, placed exactly as Mermaid
            // places it (the library rounds the size the same way as ComputeTargetSize); a box is fitted
            var exact = Options.Width is null && Options.Height is null;
            var renderOptions = CreateMermaidOptions(
                theme,
                background.A == 0 ? null : background,
                notes,
                scoped ? Context.ScopedResolver : images!.Resolver,
                Options.Strict,
                exact ? Options.Scale ?? 1 : 1);

            // The surface is charged before the diagram is drawn, so a diagram that cannot be paid for costs nothing; a diagram
            // its pictures enlarge is charged the rest after the draw, when its size is known
            Context.ChargeSurface(width, height);
            Surface surface;
            try
            {
                surface = RenderDiagram(
                    () => exact
                        ? _document.Render(renderOptions, Context.CancellationToken)
                        : _document.Render(width, height, renderOptions, Context.CancellationToken),
                    Info.Path);
            }
            catch (ImageSourceException ex) when (images is not null)
            {
                throw images.Map(ex);
            }

            // The pictures of the nodes can change the layout of a diagram drawn at its natural size (a node without its w
            // and h takes the size of its picture), which the probe measured without them: the pixels drawn beyond the
            // charge are charged now (the surface is refused when they do not fit), and the natural size and the render
            // operation report the size that was drawn
            var drawnPixels = (long)surface.Width * surface.Height;
            var excess = drawnPixels - (long)width * height;
            if (excess > 0)
            {
                try
                {
                    // At most Surface.MaxDimension squared, so it fits an int; charged as one row of pixels
                    Context.ChargeSurface((int)excess, 1);
                }
                catch (ImageSourceBudgetException ex)
                {
                    // The refusal names the whole surface against what remains once the charge before the draw is given
                    // back (the resolver refunds it), as the resolver reports a surface larger than its charge
                    surface.Dispose();
                    throw new ImageSourceBudgetException(drawnPixels, ex.RemainingPixels + ((long)width * height));
                }
                catch
                {
                    surface.Dispose();
                    throw;
                }
            }

            if (exact && (surface.Width != width || surface.Height != height))
            {
                var scale = Options.Scale ?? 1;
                info = info with { Width = Unscale(surface.Width, scale), Height = Unscale(surface.Height, scale) };
            }

            var fields = new List<(string Name, object Value)>
            {
                ("width", surface.Width),
                ("height", surface.Height),
                ("background", FormatColor(background))
            };
            if (theme is not null)
            {
                fields.Add(("theme", theme));
            }

            var operations = new List<AppliedOperation> { Operation("render", [.. fields]) };
            if (images is null)
            {
                return new LoadedSource(surface, info, 1, true, notes, [], operations);
            }

            try
            {
                // A reference that was not drawn is an error in strict mode, as it is for the pictures of an SVG file
                if (Options.Strict)
                {
                    images.ThrowForUnresolved();
                }

                // The library asks for every picture twice, once at a probe size for its natural size and once to draw it: an
                // embed operation is listed once per link, at the size it was drawn (the last request), and a note of the
                // picture once
                operations.AddRange(LastPerLink(images.Operations()));
                notes.AddRange(images.Notes().Distinct());
                return new LoadedSource(
                    surface,
                    info,
                    1,
                    true,
                    DropDuplicateUnresolved(notes, images.Resolver.Diagnostics, MermaidImageUnresolved),
                    [.. images.Resolver.Echo],
                    operations);
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }
    }
}
