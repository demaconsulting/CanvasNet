using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba

/// <summary>
///     A shape's resolved stroke/fill paint, after walking the Master/MasterShape cell merge and
///     the StyleSheet inheritance chain (see <c>VsdxDocument.Paint.cs</c>).
/// </summary>
/// <param name="HasLine">
///     <see langword="false"/> when the shape's geometry section(s) set <c>NoLine</c>, or when
///     the resolved <c>LinePattern</c> is literally <c>"0"</c> (no line) - otherwise
///     <see langword="true"/>.
/// </param>
/// <param name="StrokeColor">
///     The resolved stroke color (see <see cref="VsdxColorPalette.Resolve"/>). Meaningful only
///     when <see cref="HasLine"/> is <see langword="true"/>.
/// </param>
/// <param name="StrokeWidthInches">The resolved <c>LineWeight</c> cell's value, in inches (Visio's own native unit for this cell - see the format reference's unit-of-measure caveat).</param>
/// <param name="HasFill">
///     <see langword="false"/> when the shape's geometry section(s) set <c>NoFill</c>, or when the
///     resolved <c>FillPattern</c> is literally <c>"0"</c> (no fill) - otherwise
///     <see langword="true"/>, including for any non-<c>0</c>/non-<c>1</c> <c>FillPattern</c>
///     value, which degrades to a flat fill using <see cref="FillColor"/> rather than
///     implementing the specific gradient/pattern construction (per
///     <c>canvas-net-vsdx.md</c>'s Design Constraints: a non-solid fill "degrades to a flat fill
///     ... never throwing").
/// </param>
/// <param name="FillColor">
///     The resolved fill color (see <see cref="VsdxColorPalette.Resolve"/>). Meaningful only when
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
