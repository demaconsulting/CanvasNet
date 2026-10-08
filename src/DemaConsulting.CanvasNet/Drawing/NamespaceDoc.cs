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
///     <see cref="LineJoin"/>. <see cref="Gradient"/> paint - either a
///     <see cref="LinearGradient"/> or a <see cref="RadialGradient"/>, each defined by an
///     ordered list of <see cref="GradientStop"/> colors and a <see cref="GradientSpread"/>
///     mode controlling how the gradient repeats beyond its defined extent - and
///     <see cref="TilePaint"/>, which repeats a caller-supplied
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> tile by unconditionally wrapping
///     pattern-space coordinates modulo its <see cref="TilePaint.XStep"/>/
///     <see cref="TilePaint.YStep"/> pitch - there is no caller-selectable spread mode analogous
///     to <see cref="GradientSpread"/>; tiling always repeats - are both integrated directly into
///     <see cref="PathFiller"/> as alternatives to a single solid color. This namespace consumes
///     both
///     <see cref="DemaConsulting.CanvasNet.Geometry"/> (for <c>Path</c>, curve flattening, and
///     arc conversion) and <see cref="DemaConsulting.CanvasNet.Canvas"/> (for the pixel buffer it
///     paints into) - neither of those namespaces depends on this one, so future changes here
///     never ripple back into either foundational namespace. An internal <see cref="ClipMask"/>
///     type represents a per-pixel antialiased clip-region coverage mask - built from a path the
///     same way <see cref="PathFiller"/> itself rasterizes one, then intersected (never replaced)
///     with any previously active clip - and is threaded through dedicated internal clip-aware
///     overloads of <see cref="PathFiller.Fill(DemaConsulting.CanvasNet.Canvas.Surface, DemaConsulting.CanvasNet.Geometry.Path, DemaConsulting.CanvasNet.Canvas.Rgba32, FillRule, float)"/>
///     for <c>DemaConsulting.CanvasNet.Pdf</c> (an <c>InternalsVisibleTo</c> friend assembly) to
///     enforce a PDF content stream's current clipping path (PDF 32000-1 &#xA7;8.5.4) without that
///     concept becoming part of this namespace's public surface. Transform-aware fills and
///     font/text rendering remain reserved for later phases.
/// </summary>
internal static class NamespaceDoc
{
}
