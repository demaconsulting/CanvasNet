// cspell:ignore jpeg tiff pptx vsdx scad jpeg2000 Visio
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The kinds of input file the tool recognizes.
/// </summary>
/// <remarks>
///     The members are in the order in which the <c>formats</c> command lists the input formats, and the
///     lower-case member name (see <see cref="SourceKindExtensions.ToFormatName(SourceKind)"/>) is the format name
///     used in the command output and in JSON. A kind that is recognized but not yet loadable is reported as
///     <c>unsupported-format</c> by <see cref="SourceLoader"/>.
/// </remarks>
internal enum SourceKind
{
    /// <summary>A PNG raster image.</summary>
    Png,

    /// <summary>A JPEG raster image.</summary>
    Jpeg,

    /// <summary>A BMP raster image.</summary>
    Bmp,

    /// <summary>A TIFF raster image.</summary>
    Tiff,

    /// <summary>A GIF raster image (the first frame is used).</summary>
    Gif,

    /// <summary>A JPEG 2000 raster image (a JP2 file or a raw codestream).</summary>
    Jpeg2000,

    /// <summary>An SVG vector drawing.</summary>
    Svg,

    /// <summary>A PDF document.</summary>
    Pdf,

    /// <summary>A PowerPoint presentation.</summary>
    Pptx,

    /// <summary>A Visio drawing.</summary>
    Vsdx,

    /// <summary>An OpenSCAD model.</summary>
    Scad
}

/// <summary>
///     The image formats the tool writes.
/// </summary>
internal enum OutputFormat
{
    /// <summary>PNG, lossless, keeps transparency.</summary>
    Png,

    /// <summary>JPEG, lossy, opaque.</summary>
    Jpeg,

    /// <summary>BMP, 32 bits per pixel, keeps transparency.</summary>
    Bmp,

    /// <summary>TIFF, uncompressed, keeps transparency.</summary>
    Tiff
}

/// <summary>
///     Names for the format enumerations, as used in command output, JSON, and the <c>--format</c> option.
/// </summary>
internal static class SourceKindExtensions
{
    /// <summary>
    ///     Gets the format name of a source kind: its lower-case member name, for example <c>jpeg2000</c>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The format name.</returns>
    public static string ToFormatName(this SourceKind kind)
    {
        return kind.ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Gets the format name of an output format: its lower-case member name, for example <c>jpeg</c>.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The format name.</returns>
    public static string ToFormatName(this OutputFormat format)
    {
        return format.ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Determines whether a source kind is a document whose pages (or slides) are chosen with <c>--page</c>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for PDF, PPTX, and VSDX.</returns>
    public static bool IsDocument(this SourceKind kind)
    {
        return kind is SourceKind.Pdf or SourceKind.Pptx or SourceKind.Vsdx;
    }
}

/// <summary>
///     A note about a source that did not stop it from being loaded, such as a feature that was skipped.
/// </summary>
/// <param name="Source">The part of the tool or library that reported the note, for example <c>svg</c>.</param>
/// <param name="Severity">The severity: <c>info</c>, <c>warning</c>, or <c>unsupported</c>.</param>
/// <param name="Code">A stable, machine-readable code.</param>
/// <param name="Message">A human-readable description.</param>
/// <param name="Page">The 1-based page the note is about, or <see langword="null"/> when it is not about one page.</param>
internal sealed record ImageDiagnostic(string Source, string Severity, string Code, string Message, int? Page = null);

/// <summary>
///     One page or slide of a multi-page source.
/// </summary>
/// <param name="Number">The 1-based page number.</param>
/// <param name="Width">The natural width in pixels at 96 dots per inch.</param>
/// <param name="Height">The natural height in pixels at 96 dots per inch.</param>
/// <param name="Name">The page name, or <see langword="null"/> when it has none.</param>
/// <param name="WidthPoints">The width in points (1/72 inch).</param>
/// <param name="HeightPoints">The height in points (1/72 inch).</param>
/// <param name="Rotation">The clockwise viewing rotation in degrees of a PDF page, or <see langword="null"/> for other sources.</param>
internal sealed record SourcePage(
    int Number,
    int Width,
    int Height,
    string? Name,
    double WidthPoints,
    double HeightPoints,
    int? Rotation);

/// <summary>
///     The facts about a source file that can be read without rendering it.
/// </summary>
/// <param name="Path">The path of the file as it was given.</param>
/// <param name="Format">The detected kind.</param>
/// <param name="Bytes">The size of the file in bytes.</param>
/// <param name="Width">The natural width in pixels, as stored (before any EXIF orientation); for a document, the width of page 1 at 96 dots per inch.</param>
/// <param name="Height">The natural height in pixels, as stored (before any EXIF orientation); for a document, the height of page 1 at 96 dots per inch.</param>
/// <param name="Channels">The number of channels in the file.</param>
/// <param name="HasAlpha"><see langword="true"/> when the source has transparency.</param>
/// <param name="CanDecode"><see langword="false"/> when the header was read but the image cannot be decoded.</param>
/// <param name="FrameCount">The number of frames (more than one only for an animated GIF).</param>
/// <param name="PageCount">The number of pages or slides; <c>1</c> for a single-image source.</param>
/// <param name="ExifOrientation">The EXIF orientation (1 to 8) of a JPEG, or <see langword="null"/> when it has none.</param>
/// <param name="Pages">The pages of a multi-page source; empty for a single-image source.</param>
/// <param name="Modules">The module names of an OpenSCAD model; empty for every other kind.</param>
/// <param name="Functions">The function names of an OpenSCAD model; empty for every other kind.</param>
internal sealed record SourceInfo(
    string Path,
    SourceKind Format,
    long Bytes,
    int Width,
    int Height,
    int Channels,
    bool HasAlpha,
    bool CanDecode,
    int FrameCount,
    int PageCount,
    int? ExifOrientation,
    IReadOnlyList<SourcePage> Pages,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Functions);

/// <summary>
///     Options that control how a source is loaded.
/// </summary>
/// <remarks>
///     Properties are added here as further source kinds need them (the OpenSCAD view is still to come); a property
///     that does not apply to a kind is ignored by that kind.
/// </remarks>
internal sealed record SourceOptions
{
    /// <summary>
    ///     Gets the 1-based page (or slide) to load. A single-image source has only page 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    ///     Gets the resolution in dots per inch that a page of a document is sized at, or <see langword="null"/> for
    ///     96. A VSDX page takes a whole number.
    /// </summary>
    public double? Dpi { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a feature the library cannot draw is an error instead of a note.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    ///     Gets the factor applied to the natural size, or <see langword="null"/> for none.
    /// </summary>
    public double? Scale { get; init; }

    /// <summary>
    ///     Gets the target width in pixels, or <see langword="null"/> to derive it (keeping the aspect ratio when
    ///     only the height is given, otherwise the natural width).
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    ///     Gets the target height in pixels, or <see langword="null"/> to derive it (keeping the aspect ratio when
    ///     only the width is given, otherwise the natural height).
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    ///     Gets the color that transparent areas are flattened over, or <see langword="null"/> for the default:
    ///     white for a vector source, and no flattening for a raster source. A color with zero alpha keeps the
    ///     transparency of a vector source.
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the EXIF orientation of a JPEG is applied (the default).
    /// </summary>
    public bool AutoOrient { get; init; } = true;
}

/// <summary>
///     One step that was applied to produce an image, for the <c>operations</c> list of a result.
/// </summary>
/// <param name="Op">The operation name, for example <c>render</c>, <c>auto-orient</c>, or <c>resize</c>.</param>
/// <param name="Fields">
///     The operation's parameters in the order they are written; each value is a <see cref="string"/>,
///     <see cref="int"/>, <see cref="double"/>, or <see cref="bool"/>.
/// </param>
internal sealed record AppliedOperation(string Op, IReadOnlyList<KeyValuePair<string, object>> Fields);

/// <summary>
///     A source that has been loaded into pixels.
/// </summary>
/// <param name="Surface">The pixels; disposed with the loaded source.</param>
/// <param name="Info">
///     The facts about the source file; for a page of a document, <see cref="SourceInfo.Width"/> and
///     <see cref="SourceInfo.Height"/> are the natural size of the loaded page at the requested resolution.
/// </param>
/// <param name="Page">The 1-based page that was loaded; <c>1</c> for a single-image source.</param>
/// <param name="IsVector"><see langword="true"/> when the source was rendered at the requested size rather than decoded.</param>
/// <param name="Diagnostics">The notes reported while loading; empty when there are none.</param>
/// <param name="Echo">The text a model printed while it was evaluated; empty for every other kind.</param>
/// <param name="Operations">What was done to the source to produce <paramref name="Surface"/>, in order.</param>
/// <remarks>
///     The loaded source owns its surface: disposing the loaded source disposes the surface.
/// </remarks>
internal sealed record LoadedSource(
    Surface Surface,
    SourceInfo Info,
    int Page,
    bool IsVector,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo,
    IReadOnlyList<AppliedOperation> Operations) : IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
        Surface.Dispose();
    }
}
