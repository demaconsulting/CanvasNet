using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba Themed Foregnd

/// <summary>
///     Implements the <see cref="VsdxDocument"/> paint resolver: for each paint-related cell name
///     (<c>LineColor</c>/<c>LineWeight</c>/<c>LinePattern</c>/<c>FillForegnd</c>/
///     <c>FillPattern</c>), the shape's own merged (Master-resolved) literal cell wins; otherwise
///     the value falls through to the StyleSheet chain, walked via the shape's effective
///     <c>LineStyle</c>/<c>FillStyle</c> ID (see <c>VsdxDocument.Styles.cs</c>). The resolved
///     <c>FillPattern 0/1</c> case is implemented directly (no fill / solid fill using
///     <c>FillForegnd</c>); any other numeric <c>FillPattern</c> value degrades to the same solid-
///     fill treatment as <c>1</c> rather than throwing (see <see cref="VsdxResolvedPaint"/>'s own
///     remarks for the design-doc citation), and the literal sentinel <c>"Themed"</c> color value
///     resolves through <see cref="VsdxColorPalette.ThemedFallback"/> (deferred in full to
///     Milestone 6 - see <see cref="VsdxColorPalette.ThemedFallback"/>'s own remarks for the
///     fixture evidence motivating this deliberate deviation from the originating plan report).
///     Also resolves <c>BeginArrow</c>/<c>EndArrow</c> (and their paired
///     <c>BeginArrowSize</c>/<c>EndArrowSize</c>) through the exact same <c>Line*</c>-category
///     precedence - see <c>VsdxDocument.Arrowheads.cs</c>'s <c>ResolveArrowhead</c>.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The neutral stroke color substituted when no literal <c>LineColor</c> can be resolved anywhere in the chain (should not occur for a well-formed document, since the built-in "No Style" StyleSheet, ID <c>0</c>, always supplies one, but guarded defensively per <c>canvas-net-vsdx.md</c>'s "never throwing" design constraint).</summary>
    private static readonly Rgba32 DefaultStrokeColor = new(0, 0, 0, 255);

    /// <summary>The neutral fill color substituted when no literal <c>FillForegnd</c> can be resolved anywhere in the chain.</summary>
    private static readonly Rgba32 DefaultFillColor = new(255, 255, 255, 255);

    /// <summary>Resolves a shape's stroke/fill paint.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="geometries">The shape's resolved geometry sections (supplies the <c>NoFill</c>/<c>NoLine</c> flags - the first section's flags are used, matching every in-scope fixture's single-section shapes).</param>
    /// <param name="lineStyleId">The shape's effective <c>LineStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <param name="fillStyleId">The shape's effective <c>FillStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <returns>The resolved <see cref="VsdxResolvedPaint"/>.</returns>
    private VsdxResolvedPaint ResolvePaint(
        VsdxCellBag effectiveCells,
        IReadOnlyList<VsdxGeometrySection> geometries,
        string? lineStyleId,
        string? fillStyleId)
    {
        var sectionNoFill = geometries.Count > 0 && geometries[0].NoFill;
        var sectionNoLine = geometries.Count > 0 && geometries[0].NoLine;

        var linePattern = ResolveLineCellValue(effectiveCells, "LinePattern", lineStyleId);
        var lineColorRaw = ResolveLineCellValue(effectiveCells, "LineColor", lineStyleId);
        var lineWeightRaw = ResolveLineCellValue(effectiveCells, "LineWeight", lineStyleId);

        var fillPattern = ResolveFillCellValue(effectiveCells, "FillPattern", fillStyleId);
        var fillColorRaw = ResolveFillCellValue(effectiveCells, "FillForegnd", fillStyleId);

        var hasLine = !sectionNoLine && linePattern != "0";
        var hasFill = !sectionNoFill && fillPattern != "0";

        var strokeWidth = double.TryParse(lineWeightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWeight) &&
            double.IsFinite(parsedWeight) && parsedWeight >= 0
            ? parsedWeight
            : 0d;

        var beginArrowhead = ResolveArrowhead(effectiveCells, lineStyleId, isBegin: true);
        var endArrowhead = ResolveArrowhead(effectiveCells, lineStyleId, isBegin: false);

        return new VsdxResolvedPaint(
            HasLine: hasLine,
            StrokeColor: VsdxColorPalette.Resolve(lineColorRaw, DefaultStrokeColor),
            StrokeWidthInches: strokeWidth,
            HasFill: hasFill,
            FillColor: VsdxColorPalette.Resolve(fillColorRaw, DefaultFillColor),
            BeginArrowhead: beginArrowhead,
            EndArrowhead: endArrowhead);
    }

    /// <summary>Resolves a <c>Line*</c>-category cell: the shape's own literal value, or the StyleSheet chain walked via the <c>LineStyle</c> parent pointer.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="cellName">The cell name to resolve.</param>
    /// <param name="lineStyleId">The shape's effective <c>LineStyle</c> StyleSheet ID.</param>
    /// <returns>The resolved raw value, or <see langword="null"/> when unresolved anywhere.</returns>
    private string? ResolveLineCellValue(VsdxCellBag effectiveCells, string cellName, string? lineStyleId) =>
        effectiveCells.TryGetLiteral(cellName, out var cell)
            ? cell.Value
            : ResolveStyleCellValue(lineStyleId, cellName, style => style.LineStyleParentId);

    /// <summary>Resolves a <c>Fill*</c>-category cell: the shape's own literal value, or the StyleSheet chain walked via the <c>FillStyle</c> parent pointer.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="cellName">The cell name to resolve.</param>
    /// <param name="fillStyleId">The shape's effective <c>FillStyle</c> StyleSheet ID.</param>
    /// <returns>The resolved raw value, or <see langword="null"/> when unresolved anywhere.</returns>
    private string? ResolveFillCellValue(VsdxCellBag effectiveCells, string cellName, string? fillStyleId) =>
        effectiveCells.TryGetLiteral(cellName, out var cell)
            ? cell.Value
            : ResolveStyleCellValue(fillStyleId, cellName, style => style.FillStyleParentId);
}
