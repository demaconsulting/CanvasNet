namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Drawing"/> namespace provides two public vector
///     rendering entry points built on the same geometry-to-coverage pipeline:
///     <see cref="PathFiller"/> fills a closed
///     <see cref="DemaConsulting.CanvasNet.Geometry.Path"/> directly onto a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> with a single solid
///     <see cref="DemaConsulting.CanvasNet.Canvas.Rgba32"/> color, resolving overlapping or
///     self-intersecting geometry via a caller-selected <see cref="FillRule"/>; and
///     <see cref="PathStroker"/> converts a path stroke into closed fillable outline geometry
///     configured by <see cref="StrokeStyle"/>, <see cref="LineCap"/>, and
///     <see cref="LineJoin"/>. This namespace consumes both
///     <see cref="DemaConsulting.CanvasNet.Geometry"/> (for <c>Path</c>, curve flattening, and
///     arc conversion) and <see cref="DemaConsulting.CanvasNet.Canvas"/> (for the pixel buffer it
///     paints into) - neither of those namespaces depends on this one, so future changes here
///     never ripple back into either foundational namespace. Gradient or pattern paint,
///     transform-aware fills, clip regions beyond the surface's own bounds, and font/text
///     rendering remain reserved for later phases.
/// </summary>
internal static class NamespaceDoc
{
}
