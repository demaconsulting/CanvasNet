using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba

/// <summary>
///     A shape's resolved stroke/fill paint, after walking the Master/MasterShape cell merge and
///     the StyleSheet inheritance chain (see <c>VsdxDocument.Paint.cs</c>).
/// </summary>
/// <param name="HasLine">
///     <see langword="false"/> when the resolved <c>LinePattern</c> is literally <c>"0"</c> (no
///     line) - otherwise <see langword="true"/>. This shape-wide flag does not consult any
///     geometry section's own <c>NoLine</c> flag; a shape may declare several geometry sections,
///     and each section's own <c>NoLine</c> flag is instead honored per-section (gating this same
///     shape-wide flag) by <c>VsdxDocument.Render.cs</c>'s <c>PaintShapeGeometry</c> - see
///     <c>VsdxDocument.Paint.cs</c>'s <c>ResolvePaint</c> remarks.
/// </param>
/// <param name="StrokeColor">
///     The resolved stroke color (see <c>VsdxColorPalette.Resolve</c>). Meaningful only
///     when <see cref="HasLine"/> is <see langword="true"/>.
/// </param>
/// <param name="StrokeWidthInches">The resolved <c>LineWeight</c> cell's value, in inches (Visio's own native unit for this cell - see the format reference's unit-of-measure caveat).</param>
/// <param name="HasFill">
///     <see langword="false"/> when the resolved <c>FillPattern</c> is literally <c>"0"</c> (no
///     fill) - otherwise <see langword="true"/>, including for any non-<c>0</c>/non-<c>1</c>
///     <c>FillPattern</c> value, which degrades to a flat fill using <see cref="FillColor"/>
///     rather than implementing the specific gradient/pattern construction - a non-solid fill
///     always degrades to a flat fill rather than throwing. This shape-wide flag does not
///     consult any geometry section's own
///     <c>NoFill</c> flag - see <see cref="HasLine"/>'s own remarks for why, and where that flag
///     is instead honored.
/// </param>
/// <param name="FillColor">
///     The resolved fill color (see <c>VsdxColorPalette.Resolve</c>). Meaningful only when
///     <see cref="HasFill"/> is <see langword="true"/>.
/// </param>
/// <param name="BeginArrowhead">
///     The shape's resolved <c>BeginArrow</c>/<c>BeginArrowSize</c> arrowhead (see
///     <c>VsdxDocument.Arrowheads.cs</c>), resolved for every shape - meaningful only for a 1-D
///     (connector) shape with <see cref="VsdxArrowheadStyle.None"/> otherwise rendering as a
///     no-op.
/// </param>
/// <param name="EndArrowhead">The shape's resolved <c>EndArrow</c>/<c>EndArrowSize</c> arrowhead - see <see cref="BeginArrowhead"/>'s own remarks.</param>
internal sealed record VsdxResolvedPaint(
    bool HasLine,
    Rgba32 StrokeColor,
    double StrokeWidthInches,
    bool HasFill,
    Rgba32 FillColor,
    VsdxArrowhead BeginArrowhead,
    VsdxArrowhead EndArrowhead);
