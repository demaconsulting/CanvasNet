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
/// <remarks>
///     <para>
///         <see cref="ViewerException"/> and <see cref="ErrorCodes"/> are in the
///         <see cref="DemaConsulting.CanvasNet.Viewer"/> namespace; the operations, requests, and results are here. An
///         example that catches a failure needs both <c>using DemaConsulting.CanvasNet.Viewer;</c> and
///         <c>using DemaConsulting.CanvasNet.Viewer.Imaging;</c>. The pixels are
///         <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> objects, with <c>FitMode</c> and <c>ResamplingMode</c>, in
///         <see cref="DemaConsulting.CanvasNet.Canvas"/>, and <see cref="DemaConsulting.CanvasNet.Codecs"/> reads and
///         writes the image files.
///     </para>
///     <para>
///         See <see cref="ImageRenderer"/> with <see cref="RenderRequest"/> for the usual call and
///         <see cref="IImageOperation"/> for the operations that transform the image (<see cref="CropOperation"/>,
///         <see cref="ResizeOperation"/>, <see cref="FitOperation"/>, and the others of this namespace),
///         <see cref="ImageComparer"/> with <see cref="CompareRequest"/> and <see cref="CompareResult"/> to measure a
///         difference, <see cref="ImageInspector"/> with <see cref="InspectRequest"/>, and
///         <see cref="MontageBuilder"/> with <see cref="MontageRequest"/>. To render a file in one call without any of
///         this, or to add image sources of your own, see <see cref="DemaConsulting.CanvasNet.Viewer"/> and
///         <see cref="DemaConsulting.CanvasNet.ImageSources"/>. Pages are numbered from 1 here (the format packages count
///         from 0), and anchors of an OpenSCAD model are reported only by <see cref="ImageRenderer"/> and
///         <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/>.
///     </para>
/// </remarks>
/// <example>
///     Render page 3 of a document at 150 dots per inch, fitted into 800 x 600 pixels, then measure how much a rendering
///     differs from a reference picture (an RMSE of at most 10 on the 0 to 255 scale):
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer;
///     using DemaConsulting.CanvasNet.Viewer.Imaging;
///
///     try
///     {
///         var info = SourceLoader.Probe("deck.pptx");
///         var request = new RenderRequest("slide.png")
///         {
///             Source = new SourceOptions { Page = 3, Dpi = 150 },
///             Operations = [new FitOperation(800, 600)],
///         };
///         ImageRenderer.Execute(info, request);
///
///         var compare = new CompareRequest(
///             new CompareInput("a", SourceLoader.Probe("reference.png")),
///             new CompareInput("b", SourceLoader.Probe("slide.png")))
///         {
///             Resize = ResizeTarget.ToInputA,
///             Thresholds = [new ComparisonThreshold(ThresholdMetric.Rmse, 10)],
///         };
///         var result = ImageComparer.Execute(compare);
///         Console.WriteLine($"RMSE {result.Metrics.Rmse:F2}, passed: {result.Checks.All(check => check.Passed)}");
///     }
///     catch (ViewerException ex)
///     {
///         Console.WriteLine($"{ex.Code}: {ex.Message}");
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
