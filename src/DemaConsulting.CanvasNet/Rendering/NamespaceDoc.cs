namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Rendering"/> namespace provides higher-level
///     rendering primitives built on top of the <see cref="DemaConsulting.CanvasNet.Canvas"/>,
///     <see cref="DemaConsulting.CanvasNet.Geometry"/>, <see cref="DemaConsulting.CanvasNet.Drawing"/>,
///     and <see cref="DemaConsulting.CanvasNet.Fonts"/> subsystems.
/// </summary>
/// <remarks>
///     The subsystem's three units are the <see cref="Canvas"/> class (which wraps a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> with a transform stack and per-path
///     fill/stroke methods delegating to <see cref="DemaConsulting.CanvasNet.Drawing.PathFiller"/>
///     and <see cref="DemaConsulting.CanvasNet.Drawing.PathStroker"/>), the <see cref="TextRenderer"/>
///     static class (which measures and draws text using a
///     <see cref="DemaConsulting.CanvasNet.Fonts.TrueTypeFont"/>), and the <see cref="Shapes"/>
///     static extension class (which provides convenience shape helpers on
///     <see cref="Canvas"/>). No new NuGet dependencies are introduced. See <see cref="Canvas"/> for the transform stack
///     and path painting, <see cref="TextRenderer"/> for measuring and drawing text with a
///     <see cref="DemaConsulting.CanvasNet.Fonts.TrueTypeFont"/> (see <see cref="DemaConsulting.CanvasNet.Fonts"/> to find
///     one), and <see cref="DemaConsulting.CanvasNet.Codecs"/> to save the surface that was drawn on.
/// </remarks>
/// <example>
///     Draw a rounded rectangle and a centered caption on a new surface, then save it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Fonts;
///     using DemaConsulting.CanvasNet.Rendering;
///
///     using var surface = new Surface(200, 100);
///     var canvas = new Canvas(surface);
///     canvas.Clear(new Rgba32(255, 255, 255, 255));
///     canvas.FillRoundRect(10, 10, 180, 80, 12, new Rgba32(0, 102, 204, 255));
///     var font = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: true, italic: false);
///     canvas.DrawText("CanvasNet", 100, 58, TextAlign.Center, font, 24f, new Rgba32(255, 255, 255, 255));
///     PngCodec.Save(surface, "badge.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
