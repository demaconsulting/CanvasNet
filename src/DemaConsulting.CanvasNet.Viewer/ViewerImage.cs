using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     The result of <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>: the picture and everything the
///     render reported. The caller owns it and disposes it.
/// </summary>
/// <remarks>
///     Disposing the object disposes <see cref="Surface"/>. The object is not thread-safe. <see cref="Diagnostics"/> and
///     <see cref="Echo"/> carry the notes and printed text of every file of the render; <see cref="Anchors"/> carries the
///     anchors of an OpenSCAD model (empty for every other file). Use <c>using var image = CanvasNetViewer.Render(...)</c>.
///     <see cref="Format"/>, <see cref="Page"/>, <see cref="PageCount"/>, <see cref="Diagnostics"/>, <see cref="Echo"/>,
///     <see cref="Anchors"/>, and <see cref="Embedded"/> are plain data; only
///     <see cref="Surface"/> is released by disposing. <c>Render</c> returns pixels and writes no file; to save them
///     with a quality or a format, call <see cref="Imaging.ImageWriter.Write"/> in the
///     <c>DemaConsulting.CanvasNet.Viewer.Imaging</c> namespace.
/// </remarks>
public sealed class ViewerImage : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ViewerImage"/> class.
    /// </summary>
    /// <param name="surface">The picture; the new object owns it.</param>
    /// <param name="format">The format that was drawn.</param>
    /// <param name="diagnostics">The notes of the file and of the files it embeds; copied into a read-only list.</param>
    /// <param name="echo">The text the files printed; copied into a read-only list.</param>
    /// <param name="embedded">The images the file embedded; copied into a read-only list.</param>
    /// <param name="page">The 1-based page that was drawn.</param>
    /// <param name="pageCount">The number of pages the file has.</param>
    /// <param name="anchors">The anchors a model declared; copied into a read-only list; <see langword="null"/> for none.</param>
    internal ViewerImage(
        Surface surface,
        string format,
        IReadOnlyList<ImageSourceDiagnostic> diagnostics,
        IReadOnlyList<string> echo,
        IReadOnlyList<EmbeddedImage> embedded,
        int page = 1,
        int pageCount = 1,
        IReadOnlyList<Imaging.SourceAnchor>? anchors = null)
    {
        Surface = surface;
        Format = format;
        Diagnostics = new ReadOnlyCollection<ImageSourceDiagnostic>([.. diagnostics]);
        Echo = new ReadOnlyCollection<string>([.. echo]);
        Embedded = new ReadOnlyCollection<EmbeddedImage>([.. embedded]);
        Page = page;
        PageCount = pageCount;
        Anchors = new ReadOnlyCollection<Imaging.SourceAnchor>([.. anchors ?? []]);
    }

    /// <summary>
    ///     Gets the picture. It is valid until the object is disposed.
    /// </summary>
    public Surface Surface { get; }

    /// <summary>
    ///     Gets the format that was drawn: <see cref="ImageSourceResult.Format"/> of the source (<c>png</c>, <c>jpeg</c>,
    ///     ...) or its <see cref="IImageSource.Name"/> (<c>svg</c>, <c>scad</c>, <c>vega-lite</c>, <c>pdf</c>, <c>pptx</c>,
    ///     <c>vsdx</c>).
    /// </summary>
    public string Format { get; }

    /// <summary>
    ///     Gets the notes of the render: those of the file itself, then those of the files it embedded (with the embedding
    ///     link in front of each message, and a warning with the code <see cref="FolderImageResolver.UnresolvedCode"/>
    ///     for every link that was not drawn).
    /// </summary>
    public IReadOnlyList<ImageSourceDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets the text the file and the files it embedded printed (the <c>echo</c> output of models).
    /// </summary>
    public IReadOnlyList<string> Echo { get; }

    /// <summary>
    ///     Gets the images the file embedded (at every level of nesting; see <see cref="EmbeddedImage.Depth"/>), in
    ///     completion order. Empty for a file that embeds nothing.
    /// </summary>
    public IReadOnlyList<EmbeddedImage> Embedded { get; }

    /// <summary>
    ///     Gets the 1-based page, slide or sheet that was drawn (<see cref="ViewerRenderOptions.Page"/>); <c>1</c> for a
    ///     file without pages.
    /// </summary>
    public int Page { get; }

    /// <summary>
    ///     Gets the number of pages the file has (1 for a file without pages). It lets a caller walk the pages of a
    ///     document: render page 1, read this, then render the others.
    /// </summary>
    public int PageCount { get; }

    /// <summary>
    ///     Gets the anchors an OpenSCAD model declared with <c>echo("anchor", name, [x, y, z])</c>, with their positions in
    ///     <see cref="Surface"/> when the render had no <see cref="ViewerRenderOptions.Width"/> or
    ///     <see cref="ViewerRenderOptions.Height"/> (a resized picture moves them); empty for every other file and for a
    ///     model without anchors.
    /// </summary>
    public IReadOnlyList<Imaging.SourceAnchor> Anchors { get; }

    /// <summary>
    ///     Disposes <see cref="Surface"/>.
    /// </summary>
    public void Dispose()
    {
        Surface.Dispose();
    }
}
