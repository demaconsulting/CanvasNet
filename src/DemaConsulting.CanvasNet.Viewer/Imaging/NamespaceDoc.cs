namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Viewer.Imaging"/> namespace is the library equivalent of the
///     <c>canvasnet</c> command-line tool: everything the tool does, as plain calls with plain records. A file is
///     probed with <see cref="SourceLoader"/> (the facts of <c>info</c>), and then
///     <see cref="ImageRenderer"/> loads, transforms (<see cref="ImagePipeline"/> and its
///     <see cref="IImageOperation"/> records), and writes it (<c>render</c> and the verb commands);
///     <see cref="ImageInspector"/> reports its colors or one pixel (<c>inspect</c>); <see cref="ImageComparer"/>
///     measures how two images differ (<c>compare</c>); and <see cref="MontageBuilder"/> composes a contact sheet
///     (<c>montage</c>). The building blocks (<see cref="SourceLoader"/>, <see cref="ImageWriter"/>,
///     <see cref="ContactSheet"/>, <see cref="GridOverlay"/>, <see cref="ImageComparison"/>) are public too. Every
///     expected failure is a <see cref="ViewerException"/> whose <see cref="ViewerException.Code"/> is one of the stable
///     <see cref="ErrorCodes"/>.
/// </summary>
internal static class NamespaceDoc
{
}
