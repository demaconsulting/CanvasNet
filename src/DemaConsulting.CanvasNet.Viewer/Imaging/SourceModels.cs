// cspell:ignore jpeg tiff pptx vsdx scad jpeg2000 Visio
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The kinds of input file the imaging operations recognize.
/// </summary>
/// <remarks>
///     The members are in the order in which the <c>formats</c> command of the <c>canvasnet</c> tool lists the input
///     formats, and the lower-case member name (see <see cref="SourceKindExtensions.ToFormatName(SourceKind)"/>) is the
///     format name used in the command output and in JSON.
/// </remarks>
public enum SourceKind
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
///     The image formats the imaging operations write.
/// </summary>
public enum OutputFormat
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
public static class SourceKindExtensions
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
/// <remarks>
///     The viewer reports notes in two shapes: this record (a string <see cref="Severity"/>) for the loaders and the
///     operations of this namespace (<c>RenderedPage</c>, <c>InspectResult</c>, <c>CompareSide</c>, <c>MontageCell</c>,
///     <see cref="LoadedSource"/>), and the core <c>ImageSourceDiagnostic</c> (an enum severity) for the
///     <c>ViewerImage</c> of <c>CanvasNetViewer.Render</c>. The <see cref="Code"/> of a note of an OpenSCAD model is
///     <c>scad-warning</c>; the finer <c>ScadDiagnostic.Code</c> is not carried here, so branch on the
///     <see cref="Code"/> and never on the <see cref="Message"/>.
/// </remarks>
/// <param name="Source">The part of the tool or library that reported the note, for example <c>svg</c>.</param>
/// <param name="Severity">The severity: <c>info</c>, <c>warning</c>, or <c>unsupported</c>.</param>
/// <param name="Code">A stable, machine-readable code.</param>
/// <param name="Message">A human-readable description.</param>
/// <param name="Page">The 1-based page the note is about, or <see langword="null"/> when it is not about one page.</param>
public sealed record ImageDiagnostic(string Source, string Severity, string Code, string Message, int? Page = null)
{
    /// <summary>
    ///     Gets the embedded files, outermost first, that the note is about; empty (the default) for a note about the
    ///     source itself.
    /// </summary>
    /// <remarks>
    ///     It is the <see cref="DemaConsulting.CanvasNet.ImageSources.ImageSourceDiagnostic.Origin"/> of a note that was
    ///     reported for a file an SVG document embeds, copied unchanged: for a note a source reported, the file that
    ///     reported it preceded by the files that embedded it; for a link that was not drawn, the file that contains the
    ///     link. Each entry is the folder-relative file name of a file link and the link as written for a scheme link.
    ///     <see cref="Message"/> keeps its <c>"{href}: "</c> prefix unchanged. The list is compared by its entries.
    /// </remarks>
    public IReadOnlyList<string> Origin { get; init; } = [];

    /// <summary>
    ///     Determines whether another note has the same values, comparing <see cref="Origin"/> entry by entry.
    /// </summary>
    /// <param name="other">The note to compare with.</param>
    /// <returns><see langword="true"/> when every member, including each origin entry in order, is equal.</returns>
    public bool Equals(ImageDiagnostic? other) =>
        other is not null
        && Source == other.Source
        && Severity == other.Severity
        && Code == other.Code
        && Message == other.Message
        && Page == other.Page
        && Origin.SequenceEqual(other.Origin);

    /// <summary>
    ///     Gets a hash code that is consistent with <see cref="Equals(ImageDiagnostic?)"/>.
    /// </summary>
    /// <returns>The hash code, combining the members and every origin entry.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        hash.Add(Severity);
        hash.Add(Code);
        hash.Add(Message);
        hash.Add(Page);
        foreach (var entry in Origin)
        {
            hash.Add(entry);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
///     One page or slide of a multi-page source.
/// </summary>
/// <param name="Number">
///     The 1-based page number (the first page is 1). The format packages count from 0 (<c>pageIndex</c> of
///     <c>PdfDocument.Render</c>, <c>slideIndex</c> of <c>PptxDocument.Render</c>); the viewer converts, so never pass
///     this number to them unchanged.
/// </param>
/// <param name="Width">The natural width in pixels at 96 dots per inch.</param>
/// <param name="Height">The natural height in pixels at 96 dots per inch.</param>
/// <param name="Name">The page name, or <see langword="null"/> when it has none.</param>
/// <param name="WidthPoints">The width in points (1/72 inch).</param>
/// <param name="HeightPoints">The height in points (1/72 inch).</param>
/// <param name="Rotation">The clockwise viewing rotation in degrees of a PDF page, or <see langword="null"/> for other sources.</param>
public sealed record SourcePage(
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
/// <param name="Pages">The pages of a multi-page source in page order (the first entry is page 1); empty for a single-image source.</param>
/// <param name="Modules">The module names of an OpenSCAD model; empty for every other kind.</param>
/// <param name="Functions">The function names of an OpenSCAD model; empty for every other kind.</param>
public sealed record SourceInfo(
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
public sealed record ChartFacts(
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
public sealed record ChartDataReference(string Kind, string? Url, long? Rows, int Line);

/// <summary>
///     An <c>include</c> or <c>use</c> directive of an OpenSCAD model.
/// </summary>
/// <param name="Kind">The kind of directive: <c>include</c> or <c>use</c>.</param>
/// <param name="Path">The file the directive names, exactly as written between the angle brackets.</param>
/// <param name="Line">The 1-based line of the directive.</param>
public sealed record SourceDirective(string Kind, string Path, int Line);

/// <summary>
///     A named viewpoint of a 3D model.
/// </summary>
/// <param name="Name">The lower-case name used with <c>--view</c>.</param>
/// <param name="Azimuth">The azimuth in degrees: the rotation of the eye about the Z axis, 0 being in front of the model (on the -Y side).</param>
/// <param name="Elevation">The elevation in degrees: the angle of the eye above the XY plane.</param>
public sealed record ScadView(string Name, double Azimuth, double Elevation);

/// <summary>
///     How an OpenSCAD model is evaluated and viewed.
/// </summary>
/// <remarks>
///     The default is the isometric view, orthographic projection, no variable overrides, <c>$t</c> of 0, the
///     renderer's default triangle limit, and the renderer's default surface color.
/// </remarks>
public sealed record ScadViewOptions
{
    /// <summary>
    ///     The default of <see cref="Defines"/>: an empty dictionary that cannot be changed.
    /// </summary>
    private static readonly ReadOnlyDictionary<string, object?> NoDefines = new(new Dictionary<string, object?>());

    /// <summary>
    ///     Gets the name of the viewpoint: one of <see cref="SourceLoader.ScadViews"/>; <c>iso</c> by default.
    /// </summary>
    public string View { get; init; } = SourceLoader.DefaultScadView;

    /// <summary>
    ///     Gets a value indicating whether the viewpoint was chosen (with <c>--view</c> or a per-input view of a
    ///     comparison); without it, and without an azimuth or an elevation, the library picks the view: isometric for a
    ///     model with 3D objects, a flat top view for a model that is entirely 2D.
    /// </summary>
    /// <remarks>
    ///     The command line sets it for every <c>--view</c>, including <c>--view iso</c>; code that builds the options
    ///     directly only needs it to ask for the isometric view of a model that is entirely 2D, because any other
    ///     <see cref="View"/> counts as chosen.
    /// </remarks>
    public bool ViewGiven { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the view is explicit: a viewpoint (given, or any other than the default), an
    ///     azimuth, or an elevation was given.
    /// </summary>
    public bool IsViewExplicit =>
        ViewGiven || Azimuth.HasValue || Elevation.HasValue || !string.Equals(View, SourceLoader.DefaultScadView, StringComparison.OrdinalIgnoreCase);

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
    /// <remarks>
    ///     The default is one shared empty dictionary that cannot be changed, not even through a cast, so sharing it
    ///     between instances is safe.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Defines { get; init; } = NoDefines;

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
public sealed record SourceOptions
{
    /// <summary>
    ///     Gets the 1-based page (or slide) to load; the first page is 1, and the default. A single-image source has only
    ///     page 1. The format packages count from 0 (<c>pageIndex</c> of <c>PdfDocument.Render</c>, <c>slideIndex</c> of
    ///     <c>PptxDocument.Render</c>); the viewer converts, so do not mix the two bases. A page that does not exist is
    ///     refused with the code <c>page-out-of-range</c>.
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
public sealed record AppliedOperation(string Op, IReadOnlyList<KeyValuePair<string, object>> Fields);

/// <summary>
///     A source that has been loaded into pixels.
/// </summary>
/// <param name="Surface">The pixels; disposed with the loaded source.</param>
/// <param name="Info">
///     The facts about the source file; for a page of a document, <see cref="SourceInfo.Width"/> and
///     <see cref="SourceInfo.Height"/> are the natural size of the loaded page at the requested resolution.
/// </param>
/// <param name="Page">The 1-based page that was loaded (the first page is 1); <c>1</c> for a single-image source.</param>
/// <param name="IsVector"><see langword="true"/> when the source was rendered at the requested size rather than decoded.</param>
/// <param name="Diagnostics">The notes reported while loading; empty when there are none.</param>
/// <param name="Echo">The text a model printed while it was evaluated; empty for every other kind.</param>
/// <param name="Operations">What was done to the source to produce <paramref name="Surface"/>, in order.</param>
/// <remarks>
///     The loaded source owns its surface: disposing the loaded source disposes the surface. Of every viewer result,
///     only this record (through <see cref="Anchors"/>) and <c>RenderedPage</c> report the anchors of a model; the
///     <c>ViewerImage</c> of <c>CanvasNetViewer.Render</c>, <c>InspectResult</c>, <c>CompareSide</c>, and
///     <c>MontageCell</c> carry <c>Diagnostics</c> and <c>Echo</c> but no anchors. An anchor that cannot be read is
///     reported as a <c>scad-warning</c> note whose <c>ScadDiagnostic.Code</c> is <c>scad-anchor-invalid</c>.
/// </remarks>
public sealed record LoadedSource(
    Surface Surface,
    SourceInfo Info,
    int Page,
    bool IsVector,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo,
    IReadOnlyList<AppliedOperation> Operations) : IDisposable
{
    /// <summary>
    ///     Gets the anchors a model declared with <c>echo("anchor", name, [x, y, z])</c>, with their positions in
    ///     <see cref="Surface"/> (the image the model was drawn into, before any pipeline step); empty for every other
    ///     kind and for a model without anchors.
    /// </summary>
    public IReadOnlyList<SourceAnchor> Anchors { get; init; } = [];

    /// <inheritdoc />
    public void Dispose()
    {
        Surface.Dispose();
    }
}

/// <summary>
///     An anchor declared by an OpenSCAD model, with the position the library projected it to.
/// </summary>
/// <remarks>
///     The pixel position is continuous, with the origin at the top-left corner of the image and pixel <c>i</c>
///     spanning <c>[i, i + 1)</c>. Whether it is inside the image is not stored: it depends on the size of the image
///     after the pipeline, so the writer computes it.
/// </remarks>
/// <param name="Name">The anchor name.</param>
/// <param name="X">The world X of the anchor point.</param>
/// <param name="Y">The world Y of the anchor point.</param>
/// <param name="Z">The world Z of the anchor point.</param>
/// <param name="PixelX">The horizontal pixel position, or <see langword="null"/> when the point cannot be projected.</param>
/// <param name="PixelY">The vertical pixel position, or <see langword="null"/> when the point cannot be projected.</param>
public sealed record SourceAnchor(string Name, double X, double Y, double Z, double? PixelX, double? PixelY);
