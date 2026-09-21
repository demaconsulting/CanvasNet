namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Drawing"/> namespace provides an antialiased,
///     scanline-coverage fill rasterizer (<see cref="PathFiller"/>) that paints a closed
///     <see cref="DemaConsulting.CanvasNet.Geometry.Path"/> onto a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> with a single solid
///     <see cref="DemaConsulting.CanvasNet.Canvas.Rgba32"/> color, resolving overlapping or
///     self-intersecting geometry via a caller-selected <see cref="FillRule"/>. This namespace
///     consumes both <see cref="DemaConsulting.CanvasNet.Geometry"/> (for <c>Path</c>, curve
///     flattening, and arc conversion) and <see cref="DemaConsulting.CanvasNet.Canvas"/> (for the
///     pixel buffer it paints into) - neither of those namespaces depends on this one, so future
///     changes here never ripple back into either foundational namespace. Stroking, gradient or
///     pattern paint, transform-aware fills, clip regions beyond the surface's own bounds, and
///     font/text rendering are all reserved for later phases; this namespace's sole responsibility
///     this phase is solid-color fill.
/// </summary>
internal static class NamespaceDoc
{
}
