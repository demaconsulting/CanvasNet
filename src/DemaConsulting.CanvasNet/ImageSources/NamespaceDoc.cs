namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.ImageSources"/> namespace lets a document that links to other pictures
///     (an SVG <c>&lt;image href="..."&gt;</c>, for example) draw 3D models, charts, other vector files, document pages
///     and rasters, without the library that draws the document knowing any of those formats.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="IImageResolver"/> is the one-method contract a document renderer calls to turn a link into a
///         <see cref="Canvas.Surface"/>. <see cref="FolderImageResolver"/> is the ready-made implementation: it confines
///         the link to a folder (through <see cref="Files.SandboxedFileResolver"/>), enforces depth, cycle and count
///         limits, and hands the file to the <see cref="IImageSource"/> that claims its file name extension (or the link
///         itself to the one that claims its URI scheme).
///     </para>
///     <para>
///         <see cref="IImageSource"/> is the contract of one format. This library ships <see cref="RasterImageSource"/>;
///         the format packages ship the others (SVG, OpenSCAD, Vega-Lite, PDF, PPTX, VSDX), the
///         <c>DemaConsulting.CanvasNet.Viewer</c> package lists them all, and any application can implement the
///         interface for its own format or its own live source. <see cref="ImageSourceSet"/> composes sources and
///         states which one wins when two claim the same extension or scheme: the later one.
///     </para>
///     <para>
///         The namespace has no global state and no discovery: a source is used only when the caller passes it in.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
