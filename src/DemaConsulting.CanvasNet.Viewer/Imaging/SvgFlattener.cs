using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What to flatten: where to write the copy, and how the replaced pictures are drawn.
/// </summary>
/// <remarks>
///     The output path is the only required member; every other member is an optional init property whose default is
///     what the <c>canvasnet flatten</c> command uses when the option is not given.
/// </remarks>
/// <example>
///     Flatten a drawing that embeds a Mermaid diagram, with pictures twice the placement size for print:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer.Imaging;
///
///     var info = SourceLoader.Probe("dashboard.svg");
///     var result = SvgFlattener.Execute(info, new FlattenRequest("dashboard-flat.svg") { Scale = 2 });
///     Console.WriteLine(result.Flattened
///         ? $"{result.Replaced.Count} pictures replaced in {result.Output}"
///         : "nothing to flatten; no file was written");
///     </code>
/// </example>
/// <param name="Output">
///     The path of the flattened copy. It may not be the input; a missing folder is created. Nothing is written when
///     nothing is replaced.
/// </param>
public sealed record FlattenRequest(string Output)
{
    /// <summary>
    ///     The default of <see cref="MaxOutputBytes"/>: 64 MiB.
    /// </summary>
    public const long DefaultMaxOutputBytes = 64L * 1024 * 1024;

    /// <summary>
    ///     Gets the factor every replaced picture is drawn at, relative to its placement size in the drawing at its
    ///     natural size; from 0.001 to 8192, <c>1</c> by default. Use 2 for sharper pictures in print. A value out of range
    ///     is refused with the code <c>usage-error</c>.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>
    ///     Gets a value indicating whether a link that cannot be drawn, or a feature a linked file uses that the library
    ///     cannot draw, is an error (<c>unsupported-feature</c>) instead of a warning; <see langword="false"/> by default.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    ///     Gets a value indicating whether an existing output is refused with the code <c>output-exists</c> instead of
    ///     being replaced; <see langword="false"/> by default.
    /// </summary>
    public bool NoClobber { get; init; }

    /// <summary>
    ///     Gets the largest size in bytes the flattened copy may have; more than zero, <see cref="DefaultMaxOutputBytes"/>
    ///     by default. A copy that would be larger is refused with the code <c>size-limit</c> and nothing is written.
    /// </summary>
    public long MaxOutputBytes { get; init; } = DefaultMaxOutputBytes;
}

/// <summary>
///     One link of the drawing that was replaced by a PNG picture.
/// </summary>
/// <param name="Href">The link as written in the drawing (the attribute value, with entities decoded and trimmed).</param>
/// <param name="Format">The format of the linked file, for example <c>mermaid</c>, <c>scad</c>, <c>pdf</c>, or <c>svg</c> for a nested SVG file that links such a file.</param>
/// <param name="Width">The width in pixels of the PNG picture.</param>
/// <param name="Height">The height in pixels of the PNG picture.</param>
/// <param name="Elements">How many <c>image</c> elements link it and were rewritten (a link used twice is drawn once, at the larger placement).</param>
/// <param name="Bytes">The size in bytes of the PNG picture before base64 encoding.</param>
public sealed record FlattenedImage(string Href, string Format, int Width, int Height, int Elements, long Bytes);

/// <summary>
///     One relative link of the drawing that was rewritten so it still finds its file from the folder of the copy.
/// </summary>
/// <param name="Href">The link as written in the drawing.</param>
/// <param name="NewHref">The link as written in the copy: a relative path from the folder of the copy, or a <c>file:</c> URI when no relative path exists (another drive).</param>
public sealed record RebasedLink(string Href, string NewHref);

/// <summary>
///     What a flatten produced.
/// </summary>
/// <remarks>
///     The result holds no pixels and has nothing to dispose. When <see cref="Flattened"/> is <see langword="false"/>
///     nothing was written: the drawing links no picture that only CanvasNet draws (or none that it shows), so any SVG
///     reader can show it as it is.
/// </remarks>
/// <param name="Info">The facts about the input.</param>
/// <param name="Output">The path of the copy as it was given, or <see langword="null"/> when nothing was replaced and nothing was written.</param>
/// <param name="Bytes">The size in bytes of the copy; <c>0</c> when nothing was written.</param>
/// <param name="Replaced">The links that were replaced by pictures, in the order they first appear in the drawing.</param>
/// <param name="Rebased">The relative links that were rewritten for the folder of the copy, in the order they first appear; empty when the copy is in the folder of the input.</param>
/// <param name="Diagnostics">The notes: those of the linked files and of links that were not drawn, as <c>canvasnet render</c> reports them, and <see cref="SvgFlattener.ImageNotDrawnCode"/> for a link the drawing never shows.</param>
/// <param name="Echo">The text the linked models printed while they were evaluated.</param>
public sealed record FlattenResult(
    SourceInfo Info,
    string? Output,
    long Bytes,
    IReadOnlyList<FlattenedImage> Replaced,
    IReadOnlyList<RebasedLink> Rebased,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo)
{
    /// <summary>
    ///     Gets a value indicating whether at least one link was replaced and the copy was written.
    /// </summary>
    public bool Flattened => Replaced.Count > 0;
}

/// <summary>
///     The flatten operation: writes a copy of an SVG drawing in which every picture that only CanvasNet can draw is
///     replaced by a PNG picture, so that any SVG reader shows the drawing as CanvasNet does - the library form of
///     <c>canvasnet flatten</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>What is replaced.</b> An <c>image</c> element whose link (<c>href</c>, or <c>xlink:href</c> when there is no
///         <c>href</c>, as the SVG library reads it) names a Mermaid diagram, an OpenSCAD model, a Vega-Lite chart, a PDF
///         document, a PowerPoint presentation, or a Visio drawing, with or without a fragment (<c>#page=2</c>,
///         <c>#def.width=20</c>), and a nested SVG file that links such a file at any depth. Raster pictures, SVG files
///         that link only rasters and SVG files, <c>data:</c> URIs, same-document <c>#</c> links, and network links are
///         left alone. Only the value of the link attribute changes, to <c>data:image/png;base64,...</c>; every other
///         byte of the file (comments, formatting, line endings, a byte order mark) is kept. When the copy is written to
///         another folder, the relative links that remain are rewritten to find their files from there
///         (<see cref="FlattenResult.Rebased"/>); links of other elements (an external <c>use</c>, a CSS <c>url()</c>) are
///         not.
///     </para>
///     <para>
///         <b>How it is drawn.</b> The drawing is drawn once by the SVG library with the resolver of
///         <c>canvasnet render</c> (the folder of the drawing is the sandbox; the image count, depth, cycle, fragment, and
///         pixel budget limits apply), and each picture to replace is drawn at the size of its placement through every
///         transform and the <c>viewBox</c>, times <see cref="FlattenRequest.Scale"/>; the SVG reader then fits it per
///         <c>preserveAspectRatio</c> as before. A link the drawing never shows (hidden, an unused definition, outside
///         the canvas) is left as a link and reported as <see cref="ImageNotDrawnCode"/>.
///     </para>
///     <para>
///         <b>Limits.</b> At most 64 pictures are drawn (nested ones count); the copy is at most
///         <see cref="FlattenRequest.MaxOutputBytes"/>; the drawing is read as UTF-8 text of at most 5,000,000
///         characters (a file that declares another encoding is accepted only when it is plain ASCII).
///     </para>
///     <para>
///         The input is probed by the caller (<see cref="SourceLoader.Probe(string)"/>). The operation reads files, writes
///         at most one file (staged next to the output and moved into place when complete), holds no state, and is safe to
///         call from several threads for different outputs.
///     </para>
/// </remarks>
public static partial class SvgFlattener
{
    /// <summary>
    ///     The code of the warning for a link to a file only CanvasNet draws that the drawing never shows (hidden, an
    ///     unused definition, outside the canvas, or over a limit of the SVG library), so it was left as a link.
    /// </summary>
    public const string ImageNotDrawnCode = "svg-flatten-image-not-drawn";

    /// <summary>
    ///     The most characters a drawing may have: the limit of the SVG library.
    /// </summary>
    internal const int MaxInputCharacters = 5_000_000;

    /// <summary>
    ///     The largest file that is read: four bytes for each of <see cref="MaxInputCharacters"/>, the most UTF-8 needs.
    /// </summary>
    private const long MaxInputBytes = 4L * MaxInputCharacters;

    /// <summary>
    ///     The most pixels of the canvas the drawing is drawn on to find the placements (2048 by 2048); a larger drawing
    ///     is drawn smaller and the placement sizes are enlarged by the same factor.
    /// </summary>
    internal const long DetectionMaxPixels = 2048L * 2048;

    /// <summary>
    ///     The UTF-8 encoding that refuses invalid bytes instead of replacing them, so that the text written back is the
    ///     text that was read.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     The byte order mark of UTF-8.
    /// </summary>
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    ///     The pattern of the encoding an XML declaration names.
    /// </summary>
    private static readonly Regex DeclaredEncoding = new(
        "^<\\?xml\\s[^>]*?\\bencoding\\s*=\\s*([\"'])(?<name>[^\"']*)\\1",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     The text of a drawing and the bytes it was read from.
    /// </summary>
    /// <param name="Bytes">The bytes of the file.</param>
    /// <param name="Text">The text, without the byte order mark.</param>
    /// <param name="HasBom">Whether the file starts with a UTF-8 byte order mark.</param>
    private sealed record SvgText(byte[] Bytes, string Text, bool HasBom);

    /// <summary>
    ///     Writes a copy of an SVG drawing whose pictures that only CanvasNet can draw are PNG pictures.
    /// </summary>
    /// <param name="info">The facts about the drawing, from <see cref="SourceLoader.Probe(string)"/>.</param>
    /// <param name="request">Where to write the copy, and how.</param>
    /// <returns>
    ///     What was replaced, rebased, and reported. When nothing was replaced, <see cref="FlattenResult.Flattened"/> is
    ///     <see langword="false"/> and no file was written (an existing output is left as it is).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument or <see cref="FlattenRequest.Output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="FlattenRequest.MaxOutputBytes"/> is not more than zero.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when the scale is out of range; <c>unsupported-format</c> when the input is
    ///     not an SVG file; <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c> when the copy
    ///     cannot be written; <c>corrupt-input</c> when the drawing is not well-formed UTF-8 XML, declares a DTD, or is
    ///     too large; <c>unsupported-feature</c> when it declares an encoding other than UTF-8 and is not plain ASCII, or,
    ///     with <see cref="FlattenRequest.Strict"/>, when a link cannot be drawn; <c>size-limit</c> when the copy would
    ///     exceed <see cref="FlattenRequest.MaxOutputBytes"/>; and with the codes of <c>canvasnet render</c>
    ///     (<c>scad-error</c>, <c>vega-lite-error</c>, <c>mermaid-error</c>, <c>corrupt-input</c>, ...) naming both files
    ///     when a linked file is found but broken.
    /// </exception>
    public static FlattenResult Execute(SourceInfo info, FlattenRequest request)
    {
        // Refuse a request that cannot be carried out before anything is read
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxOutputBytes);
        if (SourceLoader.FindOptionProblem(new SourceOptions { Scale = request.Scale }) is { } problem)
        {
            throw new ViewerException(ErrorCodes.UsageError, problem.Message, problem.Hint);
        }

        if (info.Format != SourceKind.Svg)
        {
            throw new ViewerException(
                ErrorCodes.UnsupportedFormat,
                $"flatten reads SVG drawings; '{info.Path}' is {info.Format.ToFormatName()}.",
                "Give an .svg file; draw other files with 'canvasnet render'.");
        }

        ImageWriter.CheckTarget(request.Output, info.Path, request.NoClobber);

        // Read the drawing, draw it once to learn which links are shown and draw each picture to replace, and apply the
        // --strict policy of render to what the resolver reported
        var document = ReadDocument(info.Path);
        var images = new SourceLoader.SvgImages(info.Path, new SourceOptions { Strict = request.Strict });
        var recorder = Detect(info, request, document, images);
        if (request.Strict)
        {
            images.ThrowForUnresolved();
        }

        // Find the link attributes in the text, and report the links only CanvasNet draws that were never drawn
        var links = SourceLoader.Read(info.Path, SourceKind.Svg, () => FindImageLinks(document.Bytes, document.Text));
        var diagnostics = images.Notes();
        diagnostics.AddRange(NotDrawn(info.Path, links, recorder.Requested));
        var echo = images.Resolver.Echo.ToList();

        // Rewrite the link values; nothing is written when nothing was replaced
        var rewrite = Rewrite(document, links, recorder, info.Path, request);
        if (rewrite.Replaced.Count == 0)
        {
            return new FlattenResult(info, null, 0, [], [], diagnostics, echo);
        }

        var content = Encode(document, rewrite.Text);
        CheckSize(content.LongLength, request.MaxOutputBytes);
        Publish(content, request.Output, request.NoClobber);
        return new FlattenResult(info, request.Output, content.LongLength, rewrite.Replaced, rewrite.Rebased, diagnostics, echo);
    }

    /// <summary>
    ///     Reads a drawing as UTF-8 text.
    /// </summary>
    /// <param name="path">The path of the drawing.</param>
    /// <returns>The bytes and the text.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>input-not-found</c> or <c>input-unreadable</c> when the file cannot be read,
    ///     <c>corrupt-input</c> when it is too large, is not UTF-8, or contains a null character, and
    ///     <c>unsupported-feature</c> when it declares another encoding and is not plain ASCII.
    /// </exception>
    /// <remarks>
    ///     The text must be exactly what the XML reader sees, so that the positions it reports are positions in the text:
    ///     that holds for UTF-8 and for plain ASCII under any ASCII-compatible declaration, and the rest is refused.
    /// </remarks>
    private static SvgText ReadDocument(string path)
    {
        var bytes = SourceLoader.Read(path, SourceKind.Svg, () =>
        {
            if (new FileInfo(path).Length > MaxInputBytes)
            {
                throw new InvalidDataException($"the file is larger than {MaxInputBytes} bytes");
            }

            return File.ReadAllBytes(path);
        });

        // A byte order mark is kept for the copy but is not part of the text the reader reports positions in
        var hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        var offset = hasBom ? Utf8Bom.Length : 0;
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ViewerException(
                ErrorCodes.CorruptInput,
                $"'{path}' is not a valid svg file: it is not UTF-8 text.",
                "Save the drawing as UTF-8.",
                ex);
        }

        // A null character is not allowed in XML, and is how UTF-16 text without a byte order mark looks as UTF-8
        if (text.Contains('\0', StringComparison.Ordinal))
        {
            throw new ViewerException(
                ErrorCodes.CorruptInput,
                $"'{path}' is not a valid svg file: it contains a null character (UTF-16 text is not read).",
                "Save the drawing as UTF-8.");
        }

        var declared = DeclaredEncoding.Match(text);
        if (declared.Success && !IsUtf8Name(declared.Groups["name"].Value) && bytes.Any(b => b >= 0x80))
        {
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"'{path}' declares the encoding '{declared.Groups["name"].Value}'; flatten reads UTF-8 drawings only.",
                "Save the drawing as UTF-8.");
        }

        return new SvgText(bytes, text, hasBom);
    }

    /// <summary>
    ///     Tells whether an encoding name of an XML declaration reads the same as UTF-8.
    /// </summary>
    /// <param name="name">The name as declared.</param>
    /// <returns><see langword="true"/> for <c>UTF-8</c> and <c>US-ASCII</c> in any case.</returns>
    private static bool IsUtf8Name(string name)
    {
        return name.Equals("utf-8", StringComparison.OrdinalIgnoreCase)
               || name.Equals("us-ascii", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Draws the drawing once with the recording resolver, which draws and keeps each picture to replace.
    /// </summary>
    /// <param name="info">The facts about the drawing.</param>
    /// <param name="request">The request.</param>
    /// <param name="document">The drawing.</param>
    /// <param name="images">The embedded pictures of the drawing: the resolver of <c>canvasnet render</c> and its error mapping.</param>
    /// <returns>The recorder, with what the drawing asked for and the pictures it kept.</returns>
    /// <exception cref="ViewerException">Thrown with the codes of <c>canvasnet render</c> for a broken drawing or linked file, and <c>size-limit</c> for pictures over the output limit.</exception>
    private static Recorder Detect(SourceInfo info, FlattenRequest request, SvgText document, SourceLoader.SvgImages images)
    {
        var (width, height, enlarge) = DetectionCanvas(info.Width, info.Height, request.Scale);
        var recorder = new Recorder(images.Resolver, enlarge, request.MaxOutputBytes);
        try
        {
            SourceLoader.Read(info.Path, SourceKind.Svg, () =>
            {
                using var stream = new MemoryStream(document.Bytes, false);
                using var surface = SvgCodec.LoadWithFontFaces(stream, width, height, null, new SvgLoadOptions { ImageResolver = recorder });
                return surface.Width;
            });
        }
        catch (ImageSourceException ex)
        {
            throw images.Map(ex);
        }

        return recorder;
    }

    /// <summary>
    ///     Chooses the canvas the drawing is drawn on to find the placements.
    /// </summary>
    /// <param name="naturalWidth">The natural width of the drawing.</param>
    /// <param name="naturalHeight">The natural height of the drawing.</param>
    /// <param name="scale">The scale of the request.</param>
    /// <returns>
    ///     The canvas: the natural size times <paramref name="scale"/>, shrunk evenly to at most
    ///     <see cref="DetectionMaxPixels"/>; and the factor that turns a placement on that canvas into the size to draw.
    /// </returns>
    internal static (int Width, int Height, double Enlarge) DetectionCanvas(int naturalWidth, int naturalHeight, double scale)
    {
        var width = Math.Max(1, naturalWidth) * scale;
        var height = Math.Max(1, naturalHeight) * scale;
        var shrink = Math.Min(1, Math.Sqrt(DetectionMaxPixels / (width * height)));
        var canvasWidth = (int)Math.Clamp(Math.Round(width * shrink), 1, Surface.MaxDimension);
        var canvasHeight = (int)Math.Clamp(Math.Round(height * shrink), 1, Surface.MaxDimension);
        return (canvasWidth, canvasHeight, 1 / shrink);
    }

    /// <summary>
    ///     Reports the links to files only CanvasNet draws that the drawing never asked for.
    /// </summary>
    /// <param name="path">The path of the drawing.</param>
    /// <param name="links">The links of the drawing.</param>
    /// <param name="requested">The links the drawing asked the resolver for.</param>
    /// <returns>One <see cref="ImageNotDrawnCode"/> warning for each such link, with its line and column.</returns>
    private static IEnumerable<ImageDiagnostic> NotDrawn(string path, IReadOnlyList<ImageLink> links, IReadOnlySet<string> requested)
    {
        var file = Path.GetFileName(path);
        return links
            .Where(link => !requested.Contains(link.Href)
                           && !link.Href.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                           && IsCanvasNetOnly(SourceLoader.SvgImages.KindOfName(link.Href)))
            .Select(link => new ImageDiagnostic(
                "svg",
                "warning",
                ImageNotDrawnCode,
                $"image '{link.Href}' is not shown by the drawing (hidden, unused, outside the canvas, or over a limit), so it was left as a link")
            {
                Location = new ImageDiagnosticLocation(file, link.Line, link.Column, null)
            });
    }

    /// <summary>
    ///     Tells whether a kind of file is one that only CanvasNet draws (a plain SVG reader shows nothing for it).
    /// </summary>
    /// <param name="kind">The kind of the linked file, or <see langword="null"/> when unknown.</param>
    /// <returns><see langword="true"/> for documents, models, charts, and diagrams; <see langword="false"/> for rasters, SVG files, and unknown kinds.</returns>
    internal static bool IsCanvasNetOnly(SourceKind? kind)
    {
        return kind is SourceKind.Pdf or SourceKind.Pptx or SourceKind.Vsdx or SourceKind.Scad or SourceKind.VegaLite or SourceKind.Mermaid;
    }

    /// <summary>
    ///     Encodes the rewritten text as the copy, with the byte order mark of the input when it had one.
    /// </summary>
    /// <param name="document">The drawing that was read.</param>
    /// <param name="text">The rewritten text.</param>
    /// <returns>The bytes of the copy.</returns>
    private static byte[] Encode(SvgText document, string text)
    {
        var body = StrictUtf8.GetBytes(text);
        return document.HasBom ? [.. Utf8Bom, .. body] : body;
    }

    /// <summary>
    ///     Refuses a copy that is larger than the limit.
    /// </summary>
    /// <param name="bytes">The size of the copy, or a lower bound of it.</param>
    /// <param name="maxBytes">The limit.</param>
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when <paramref name="bytes"/> exceeds <paramref name="maxBytes"/>.</exception>
    internal static void CheckSize(long bytes, long maxBytes)
    {
        if (bytes > maxBytes)
        {
            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"The flattened copy would be larger than {maxBytes} bytes.",
                "Use a smaller --scale, or link fewer or smaller pictures.");
        }
    }

    /// <summary>
    ///     Writes the copy: staged next to the output and moved into place, all or nothing.
    /// </summary>
    /// <param name="content">The bytes of the copy.</param>
    /// <param name="outputPath">The output path.</param>
    /// <param name="noClobber"><see langword="true"/> to refuse an existing output.</param>
    /// <exception cref="ViewerException">Thrown with code <c>output-exists</c> or <c>output-write-failed</c>.</exception>
    private static void Publish(byte[] content, string outputPath, bool noClobber)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath)!;

        // A short fixed name, as the image outputs use: a name made from the target could be too long
        var temporary = Path.Combine(directory, $".canvasnet-{Guid.NewGuid():N}.tmp");
        using var staged = new ImageWriter.StagedOutput(temporary, fullPath, outputPath, noClobber);
        try
        {
            Directory.CreateDirectory(directory);
            using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new ViewerException(
                ErrorCodes.OutputWriteFailed,
                $"The output '{outputPath}' cannot be written: {ex.Message}",
                null,
                ex);
        }

        ImageWriter.PublishAll([staged]);
    }
}
