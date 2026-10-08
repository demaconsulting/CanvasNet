using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

// cspell:ignore THEMEVAL

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
///     resolves against the document's parsed theme (<c>VsdxDocument.Theme.cs</c>) when the
///     cell's own formula carries a recognized <c>THEMEVAL("slotName")</c> reference, falling back
///     to <see cref="VsdxColorPalette.ThemedFallback"/> otherwise (see that member's own remarks
///     for the fixture evidence motivating this deliberate deviation from the originating
///     Milestone-4 plan report).
///     Also resolves <c>BeginArrow</c>/<c>EndArrow</c> (and their paired
///     <c>BeginArrowSize</c>/<c>EndArrowSize</c>) through the exact same <c>Line*</c>-category
///     precedence - see <c>VsdxDocument.Arrowheads.cs</c>'s <c>ResolveArrowhead</c>.
///     Milestone 10 also resolves <c>FillForegndTrans</c>/<c>LineColorTrans</c> (transparency,
///     <c>0..1</c>) through this same <c>Fill*</c>/<c>Line*</c>-category chain precedence, and
///     modulates the resolved <c>FillColor</c>/<c>StrokeColor</c> alpha channel accordingly (see
///     <see cref="ApplyTransparency"/>) - previously unconsulted anywhere in this resolver,
///     leaving every fill/stroke fully opaque regardless of a literal transparency cell.
///     This milestone also resolves a shape's own <c>HideText</c> cell (see
///     <see cref="ResolveHideText"/>), consumed by <c>VsdxDocument.Render.cs</c> to suppress only
///     the shape's own text rendering - previously never consulted anywhere in the package.
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

        var linePattern = ResolveLineCellValue(effectiveCells, "LinePattern", lineStyleId)?.Value;
        var lineColorCell = ResolveLineCellValue(effectiveCells, "LineColor", lineStyleId);
        var lineWeightRaw = ResolveLineCellValue(effectiveCells, "LineWeight", lineStyleId)?.Value;

        var fillPattern = ResolveFillCellValue(effectiveCells, "FillPattern", fillStyleId)?.Value;
        var fillColorCell = ResolveFillCellValue(effectiveCells, "FillForegnd", fillStyleId);
        var fillTransRaw = ResolveFillCellValue(effectiveCells, "FillForegndTrans", fillStyleId)?.Value;
        var lineTransRaw = ResolveLineCellValue(effectiveCells, "LineColorTrans", lineStyleId)?.Value;

        var hasLine = !sectionNoLine && linePattern != "0";
        var hasFill = !sectionNoFill && fillPattern != "0";

        var strokeWidth = double.TryParse(lineWeightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWeight) &&
            double.IsFinite(parsedWeight) && parsedWeight >= 0
            ? parsedWeight
            : 0d;

        var beginArrowhead = ResolveArrowhead(effectiveCells, lineStyleId, isBegin: true);
        var endArrowhead = ResolveArrowhead(effectiveCells, lineStyleId, isBegin: false);

        var theme = GetTheme();

        var strokeColor = VsdxColorPalette.Resolve(lineColorCell?.Value, lineColorCell?.Formula, theme, DefaultStrokeColor);
        var fillColor = VsdxColorPalette.Resolve(fillColorCell?.Value, fillColorCell?.Formula, theme, DefaultFillColor);

        return new VsdxResolvedPaint(
            HasLine: hasLine,
            StrokeColor: ApplyTransparency(strokeColor, lineTransRaw),
            StrokeWidthInches: strokeWidth,
            HasFill: hasFill,
            FillColor: ApplyTransparency(fillColor, fillTransRaw),
            BeginArrowhead: beginArrowhead,
            EndArrowhead: endArrowhead);
    }

    /// <summary>
    ///     Modulates <paramref name="color"/>'s own alpha channel by a resolved <c>*Trans</c>
    ///     (transparency) cell's raw value, parsed as a <c>0..1</c> fraction (<c>0</c> fully
    ///     opaque, <c>1</c> fully transparent) and clamped to <c>[0, 1]</c> - defaulting to
    ///     <c>0</c> (fully opaque, this method's previous behavior before this cell was consulted
    ///     at all) when <paramref name="transRaw"/> is <see langword="null"/>, empty, or
    ///     unparseable, per <c>canvas-net-vsdx.md</c>'s "never throw" design constraint. Confirmed
    ///     necessary against <c>60973.vsdx</c>'s "Virtual Devices" container shape, whose Master
    ///     resolves literal <c>FillForegndTrans="0.4"</c>/<c>LineColorTrans="0.4"</c> cells (40%
    ///     transparency) that, unconsulted, left the container's fill fully opaque - visually
    ///     confirmed to obscure its own label and its children's top edges against the
    ///     Visio-reference PNG, which instead shows a light, mostly-see-through frame.
    /// </summary>
    /// <param name="color">The already fully opaque-or-not resolved color (<c>VsdxColorPalette.Resolve</c>'s own result) to modulate.</param>
    /// <param name="transRaw">The resolved <c>*Trans</c> cell's raw string value, or <see langword="null"/> when unresolved anywhere in the chain.</param>
    /// <returns><paramref name="color"/> with its alpha channel reduced by the parsed transparency fraction.</returns>
    private static Rgba32 ApplyTransparency(Rgba32 color, string? transRaw)
    {
        var transparency = double.TryParse(transRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed)
            ? Math.Clamp(parsed, 0d, 1d)
            : 0d;

        if (transparency <= 0d)
        {
            return color;
        }

        var alpha = (byte)Math.Round(color.A * (1d - transparency), MidpointRounding.AwayFromZero);
        return new Rgba32(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    ///     Resolves a shape's own merged <c>HideText</c> cell - a per-shape, non-inherited flag
    ///     (like <c>NonPrinting</c>, resolved directly from the shape's merged cell bag rather
    ///     than walked through any StyleSheet chain) that, when truthy, suppresses only the
    ///     shape's own text rendering (fill/stroke are unaffected - see
    ///     <c>VsdxDocument.Render.cs</c>'s <c>RenderShapeRecursive</c>). Confirmed necessary
    ///     against <c>60489.vsdx</c>'s "Dynamic connector" master's four midpoint-label helper
    ///     sub-shapes (<c>ID="6"</c>/<c>"7"</c>/<c>"8"</c>/<c>"9"</c>), each carrying a literal
    ///     <c>&lt;Cell N='HideText' V='1' F='NOT(Sheet.5!User.ShowMulti)'/&gt;</c> baked at
    ///     authoring time: unconsulted, these shapes rendered their literal <c>"M1"</c>/<c>"M2"</c>/
    ///     <c>"M3"</c>/<c>"M4"</c> text unconditionally, bleeding through into the final render.
    /// </summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <returns><see langword="true"/> when the resolved <c>HideText</c> cell is truthy (<c>"1"</c>); otherwise <see langword="false"/>.</returns>
    private static bool ResolveHideText(VsdxCellBag effectiveCells) => effectiveCells.GetBool("HideText");

    /// <summary>Resolves a <c>Line*</c>-category cell: the shape's own literal value, or the StyleSheet chain walked via the <c>LineStyle</c> parent pointer.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="cellName">The cell name to resolve.</param>
    /// <param name="lineStyleId">The shape's effective <c>LineStyle</c> StyleSheet ID.</param>
    /// <returns>The resolved cell (carrying both its raw value and formula, for theme-aware callers), or <see langword="null"/> when unresolved anywhere.</returns>
    private VsdxCell? ResolveLineCellValue(VsdxCellBag effectiveCells, string cellName, string? lineStyleId) =>
        effectiveCells.TryGetLiteral(cellName, out var cell)
            ? cell
            : ResolveStyleCellValue(lineStyleId, cellName, style => style.LineStyleParentId);

    /// <summary>Resolves a <c>Fill*</c>-category cell: the shape's own literal value, or the StyleSheet chain walked via the <c>FillStyle</c> parent pointer.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="cellName">The cell name to resolve.</param>
    /// <param name="fillStyleId">The shape's effective <c>FillStyle</c> StyleSheet ID.</param>
    /// <returns>The resolved cell (carrying both its raw value and formula, for theme-aware callers), or <see langword="null"/> when unresolved anywhere.</returns>
    private VsdxCell? ResolveFillCellValue(VsdxCellBag effectiveCells, string cellName, string? fillStyleId) =>
        effectiveCells.TryGetLiteral(cellName, out var cell)
            ? cell
            : ResolveStyleCellValue(fillStyleId, cellName, style => style.FillStyleParentId);
}
