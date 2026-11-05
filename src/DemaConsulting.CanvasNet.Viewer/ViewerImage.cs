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
///     <see cref="Echo"/> carry the notes and printed text of every file of the render, but <b>no anchors</b>: the anchors
///     of an OpenSCAD model are reported only by <c>ImageRenderer</c> (<c>RenderedPage.Anchors</c>) and
///     <c>SourceLoader.Load</c> (<c>LoadedSource.Anchors</c>) in the <c>DemaConsulting.CanvasNet.Viewer.Imaging</c>
///     namespace.
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
    internal ViewerImage(
        Surface surface,
        string format,
        IReadOnlyList<ImageSourceDiagnostic> diagnostics,
        IReadOnlyList<string> echo,
        IReadOnlyList<EmbeddedImage> embedded)
    {
        Surface = surface;
        Format = format;
        Diagnostics = new ReadOnlyCollection<ImageSourceDiagnostic>([.. diagnostics]);
        Echo = new ReadOnlyCollection<string>([.. echo]);
        Embedded = new ReadOnlyCollection<EmbeddedImage>([.. embedded]);
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
    ///     Disposes <see cref="Surface"/>.
    /// </summary>
    public void Dispose()
    {
        Surface.Dispose();
    }
}
