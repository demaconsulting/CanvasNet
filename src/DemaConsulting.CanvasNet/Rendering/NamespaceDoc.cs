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
///     <see cref="Canvas"/>). No new NuGet dependencies are introduced.
/// </remarks>
internal static class NamespaceDoc
{
}
