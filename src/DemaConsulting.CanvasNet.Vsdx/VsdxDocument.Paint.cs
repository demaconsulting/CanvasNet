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
///     to the built-in "No Style" StyleSheet's value for a bare <c>THEMEVAL()</c> cell (see
///     <see cref="ApplyUnthemedFallback"/>) and finally to <see cref="VsdxColorPalette.ThemedFallback"/> (see that member's own remarks
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
    /// <summary>The neutral stroke color substituted when no literal <c>LineColor</c> can be resolved anywhere in the chain (should not occur for a well-formed document, since the built-in "No Style" StyleSheet, ID <c>0</c>, always supplies one, but guarded defensively consistent with this package's "never throwing" design convention).</summary>
    private static readonly Rgba32 DefaultStrokeColor = new(0, 0, 0, 255);

    /// <summary>The neutral fill color substituted when no literal <c>FillForegnd</c> can be resolved anywhere in the chain.</summary>
    private static readonly Rgba32 DefaultFillColor = new(255, 255, 255, 255);

    /// <summary>Resolves a shape's stroke/fill paint.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="lineStyleId">The shape's effective <c>LineStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <param name="fillStyleId">The shape's effective <c>FillStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <returns>
    ///     The resolved <see cref="VsdxResolvedPaint"/>. This shape-wide result intentionally does
    ///     not consult any geometry section's own <c>NoFill</c>/<c>NoLine</c> flag - a shape may
    ///     declare several geometry sections, and a flag on one section must not suppress painting
    ///     of a different, paintable section. Each section's own flag is instead honored
    ///     per-section, directly against this resolved (shape-wide) <c>HasFill</c>/<c>HasLine</c>,
    ///     by <c>VsdxDocument.Render.cs</c>'s <c>PaintShapeGeometry</c>.
    /// </returns>
    private VsdxResolvedPaint ResolvePaint(
        VsdxCellBag effectiveCells,
        string? lineStyleId,
        string? fillStyleId)
    {
        var linePattern = ResolveLineCellValue(effectiveCells, "LinePattern", lineStyleId)?.Value;
        var lineColorCell = ResolveLineCellValue(effectiveCells, "LineColor", lineStyleId);
        var lineWeightRaw = ResolveLineCellValue(effectiveCells, "LineWeight", lineStyleId)?.Value;

        var fillPattern = ResolveFillCellValue(effectiveCells, "FillPattern", fillStyleId)?.Value;
        var fillColorCell = ResolveFillCellValue(effectiveCells, "FillForegnd", fillStyleId);
        var fillTransRaw = ResolveFillCellValue(effectiveCells, "FillForegndTrans", fillStyleId)?.Value;
        var lineTransRaw = ResolveLineCellValue(effectiveCells, "LineColorTrans", lineStyleId)?.Value;

        var hasLine = linePattern != "0";
        var hasFill = fillPattern != "0";

        var strokeWidth = double.TryParse(lineWeightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWeight) &&
            double.IsFinite(parsedWeight) && parsedWeight >= 0
            ? parsedWeight
            : 0d;

        var dashPattern = VsdxLinePattern.Resolve(linePattern, out var patternFeature);
        if (hasFill &&
            int.TryParse(fillPattern, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fillPatternIndex) &&
            fillPatternIndex > 1)
        {
            ReportUnsupported(
                $"vsdx-fill-pattern-{fillPatternIndex}",
                $"FillPattern {fillPatternIndex} (gradient/hatch) is not supported; the shape is filled solid with its foreground color.");
        }
        if (hasLine && patternFeature is not null)
        {
            ReportUnsupported(
                patternFeature,
                patternFeature.EndsWith("-approximated", StringComparison.Ordinal)
                    ? $"LinePattern '{linePattern}' is drawn with an approximated dash pattern."
                    : $"LinePattern '{linePattern}' is not supported; the line is drawn solid.");
        }

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
            EndArrowhead: endArrowhead,
            DashPattern: dashPattern);
    }

    /// <summary>
    ///     Modulates <paramref name="color"/>'s own alpha channel by a resolved <c>*Trans</c>
    ///     (transparency) cell's raw value, parsed as a <c>0..1</c> fraction (<c>0</c> fully
    ///     opaque, <c>1</c> fully transparent) and clamped to <c>[0, 1]</c> - defaulting to
    ///     <c>0</c> (fully opaque, this method's previous behavior before this cell was consulted
    ///     at all) when <paramref name="transRaw"/> is <see langword="null"/>, empty, or
    ///     unparseable, consistent with this package's "never throw" design convention. Confirmed
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
        ApplyUnthemedFallback(
            effectiveCells.TryGetLiteral(cellName, out var cell)
                ? cell
                : ResolveStyleCellValue(lineStyleId, cellName, style => style.LineStyleParentId),
            cellName);

    /// <summary>
    ///     Replaces an unresolvable <c>"Themed"</c> cell (a host-cell <c>THEMEVAL()</c> whose
    ///     value Visio would compute from the document theme's QuickStyle state) with the same
    ///     cell's value from the built-in "No Style" StyleSheet (ID <c>0</c>) - the document's
    ///     unthemed base appearance (black line, white solid fill, 0.75 pt weight, black text).
    /// </summary>
    /// <remarks>
    ///     Verified against a real diagram whose equipment shapes all inherit <c>"Themed"</c>
    ///     paint from the built-in "Theme" StyleSheet (ID <c>6</c>): Visio's own render shows
    ///     black outlines, black text and a fill that is invisible on the white page, whereas
    ///     the previous neutral gray substitution painted solid gray discs and gray text.
    ///     A cell whose formula names a canonical clrScheme slot that the theme resolves (see
    ///     <see cref="VsdxColorPalette.Resolve(string?, string?, VsdxTheme?, Rgba32)"/>) is left
    ///     untouched, as is a cell for which the "No Style" StyleSheet supplies no literal value
    ///     of its own (the existing gray fallback and diagnostic then still apply).
    ///     Honoring a non-default <c>QuickStyle*Color</c> index (<c>0</c>-<c>7</c>) is not
    ///     implemented.
    /// </remarks>
    /// <param name="cell">The resolved cell, or <see langword="null"/>.</param>
    /// <param name="cellName">The cell's name.</param>
    /// <returns>The "No Style" cell when <paramref name="cell"/> is an unresolvable <c>"Themed"</c> sentinel and that cell supplies a non-themed value; otherwise <paramref name="cell"/>.</returns>
    private VsdxCell? ApplyUnthemedFallback(VsdxCell? cell, string cellName)
    {
        if (cell is not { } themed || !string.Equals(themed.Value, "Themed", StringComparison.Ordinal))
        {
            return cell;
        }

        if (VsdxColorPalette.ResolvesThemeSlot(themed.Formula, GetTheme()))
        {
            return cell;
        }

        return GetStyleSheets().TryGetValue("0", out var noStyle) &&
               noStyle.Cells.TryGetLiteral(cellName, out var baseCell) &&
               !string.Equals(baseCell.Value, "Themed", StringComparison.Ordinal)
            ? baseCell
            : themed;
    }

    /// <summary>Resolves a <c>Fill*</c>-category cell: the shape's own literal value, or the StyleSheet chain walked via the <c>FillStyle</c> parent pointer.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="cellName">The cell name to resolve.</param>
    /// <param name="fillStyleId">The shape's effective <c>FillStyle</c> StyleSheet ID.</param>
    /// <returns>The resolved cell (carrying both its raw value and formula, for theme-aware callers), or <see langword="null"/> when unresolved anywhere.</returns>
    private VsdxCell? ResolveFillCellValue(VsdxCellBag effectiveCells, string cellName, string? fillStyleId) =>
        ApplyUnthemedFallback(
            effectiveCells.TryGetLiteral(cellName, out var cell)
                ? cell
                : ResolveStyleCellValue(fillStyleId, cellName, style => style.FillStyleParentId),
            cellName);
}
