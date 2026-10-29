// cspell:ignore jpeg tiff pptx vsdx scad jpeg2000 Visio
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The kinds of input file the tool recognizes.
/// </summary>
/// <remarks>
///     The members are in the order in which the <c>formats</c> command lists the input formats, and the
///     lower-case member name (see <see cref="SourceKindExtensions.ToFormatName(SourceKind)"/>) is the format name
///     used in the command output and in JSON.
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
    Scad,

    /// <summary>A Vega-Lite chart specification.</summary>
    VegaLite
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
    ///     Gets the format name of a source kind: its lower-case member name, for example <c>jpeg2000</c>; a Vega-Lite
    ///     chart is <c>vega-lite</c>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The format name.</returns>
    public static string ToFormatName(this SourceKind kind)
    {
        return kind == SourceKind.VegaLite ? "vega-lite" : kind.ToString().ToLowerInvariant();
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

    /// <summary>
    ///     Determines whether a source kind is a 3D model, which is drawn from a viewpoint (<see cref="ScadViewOptions"/>).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for an OpenSCAD model.</returns>
    public static bool IsModel(this SourceKind kind)
    {
        return kind == SourceKind.Scad;
    }

    /// <summary>
    ///     Determines whether a source kind is a vector source, which is rendered at the size it is asked for instead of
    ///     being resampled from pixels.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for SVG, PDF, PPTX, VSDX, OpenSCAD, and Vega-Lite.</returns>
    public static bool IsVector(this SourceKind kind)
    {
        return kind is SourceKind.Svg or SourceKind.Pdf or SourceKind.Pptx or SourceKind.Vsdx or SourceKind.Scad or SourceKind.VegaLite;
    }

    /// <summary>
    ///     Determines whether a source kind is a chart specification, which is described by a <see cref="ChartFacts"/>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for a Vega-Lite chart.</returns>
    public static bool IsChart(this SourceKind kind)
    {
        return kind == SourceKind.VegaLite;
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
    IReadOnlyList<string> Functions)
{
    /// <summary>
    ///     Gets the <c>include</c> and <c>use</c> directives of an OpenSCAD model in source order; empty for every
    ///     other kind.
    /// </summary>
    public IReadOnlyList<SourceDirective> Directives { get; init; } = [];

    /// <summary>
    ///     Gets what a Vega-Lite chart declares (its marks, data references, parameters, title, and schema version),
    ///     or <see langword="null"/> for every other kind.
    /// </summary>
    public ChartFacts? Chart { get; init; }
}

/// <summary>
///     What a Vega-Lite chart specification declares, read without drawing it.
/// </summary>
/// <param name="Marks">The mark types in document order, as written (for example <c>bar</c>).</param>
/// <param name="DataReferences">The data sources the specification names, in document order.</param>
/// <param name="Parameters">The names of the declared parameters, in document order.</param>
/// <param name="Title">The plain text of the title, or <see langword="null"/> when there is none.</param>
/// <param name="SchemaMajorVersion">The Vega-Lite major version named by the <c>$schema</c>, or <see langword="null"/> when there is none.</param>
internal sealed record ChartFacts(
    IReadOnlyList<string> Marks,
    IReadOnlyList<ChartDataReference> DataReferences,
    IReadOnlyList<string> Parameters,
    string? Title,
    int? SchemaMajorVersion);

/// <summary>
///     A data source named by a Vega-Lite chart.
/// </summary>
/// <param name="Kind">How the data is named: <c>inline</c>, <c>url</c>, <c>name</c>, or <c>sequence</c>.</param>
/// <param name="Url">The <c>url</c> as written for a <c>url</c> source, otherwise <see langword="null"/>.</param>
/// <param name="Rows">The number of rows when the text alone gives it, otherwise <see langword="null"/>.</param>
/// <param name="Line">The 1-based line of the <c>data</c> object, or 0 when unknown.</param>
internal sealed record ChartDataReference(string Kind, string? Url, long? Rows, int Line);

/// <summary>
///     An <c>include</c> or <c>use</c> directive of an OpenSCAD model.
/// </summary>
/// <param name="Kind">The kind of directive: <c>include</c> or <c>use</c>.</param>
/// <param name="Path">The file the directive names, exactly as written between the angle brackets.</param>
/// <param name="Line">The 1-based line of the directive.</param>
internal sealed record SourceDirective(string Kind, string Path, int Line);

/// <summary>
///     A named viewpoint of a 3D model.
/// </summary>
/// <param name="Name">The lower-case name used with <c>--view</c>.</param>
/// <param name="Azimuth">The azimuth in degrees: the rotation of the eye about the Z axis, 0 being in front of the model (on the -Y side).</param>
/// <param name="Elevation">The elevation in degrees: the angle of the eye above the XY plane.</param>
internal sealed record ScadView(string Name, double Azimuth, double Elevation);

/// <summary>
///     How an OpenSCAD model is evaluated and viewed.
/// </summary>
/// <remarks>
///     The default is the isometric view, orthographic projection, no variable overrides, <c>$t</c> of 0, the
///     renderer's default triangle limit, and the renderer's default surface color.
/// </remarks>
internal sealed record ScadViewOptions
{
    /// <summary>
    ///     Gets the name of the viewpoint: one of <see cref="SourceLoader.ScadViews"/>; <c>iso</c> by default.
    /// </summary>
    public string View { get; init; } = SourceLoader.DefaultScadView;

    /// <summary>
    ///     Gets the azimuth in degrees that replaces the azimuth of <see cref="View"/>, or <see langword="null"/> to keep it.
    /// </summary>
    public double? Azimuth { get; init; }

    /// <summary>
    ///     Gets the elevation in degrees that replaces the elevation of <see cref="View"/>, or <see langword="null"/> to keep it.
    /// </summary>
    public double? Elevation { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the view is a perspective projection; the default is orthographic.
    /// </summary>
    public bool Perspective { get; init; }

    /// <summary>
    ///     Gets the variables that replace the assignments of the same name in the model: each value is a
    ///     <see cref="double"/>, a <see cref="bool"/>, or a <see cref="string"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Defines { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    ///     Gets the value of <c>$t</c> (the animation time), or <see langword="null"/> for 0.
    /// </summary>
    public double? Time { get; init; }

    /// <summary>
    ///     Gets the largest number of triangles the model may produce, or <see langword="null"/> for the library default.
    /// </summary>
    public long? MaxTriangles { get; init; }

    /// <summary>
    ///     Gets the color of a surface the model does not color itself, or <see langword="null"/> for the renderer's gray.
    /// </summary>
    public Rgba32? DefaultColor { get; init; }
}

/// <summary>
///     Options that control how a source is loaded.
/// </summary>
/// <remarks>
///     Properties are added here as further source kinds need them; a property that does not apply to a kind is
///     ignored by that kind.
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
    ///     Gets the resampling filter of the resize that a raster source gets from <see cref="Scale"/>,
    ///     <see cref="Width"/>, or <see cref="Height"/>; <see cref="ResamplingMode.Auto"/> by default. A vector
    ///     source is rendered at its target size and is not resampled.
    /// </summary>
    public ResamplingMode Filter { get; init; } = ResamplingMode.Auto;

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

    /// <summary>
    ///     Gets how an OpenSCAD model is evaluated and viewed, or <see langword="null"/> for the defaults of
    ///     <see cref="ScadViewOptions"/>. The other kinds ignore it.
    /// </summary>
    public ScadViewOptions? Scad { get; init; }
}

/// <summary>
///     One step that was applied to produce an image, for the <c>operations</c> list of a result.
/// </summary>
/// <param name="Op">The operation name, for example <c>render</c>, <c>auto-orient</c>, or <c>resize</c>.</param>
/// <param name="Fields">
///     The operation's parameters in the order they are written; each value is a <see cref="string"/>,
///     <see cref="int"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>, or a list of strings.
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
