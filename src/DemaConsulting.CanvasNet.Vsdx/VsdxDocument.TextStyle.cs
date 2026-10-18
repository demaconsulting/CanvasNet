using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba Themed IX davehoward Foregnd bitflags

/// <summary>
///     Implements the <see cref="VsdxDocument"/> text StyleSheet-chain resolver: the third axis
///     alongside Milestone 3's <c>LineStyle</c>/<c>FillStyle</c> resolvers (see
///     <c>VsdxDocument.Paint.cs</c>) - resolves a text run's effective
///     <c>Section N="Character"</c>/<c>"Paragraph"</c> row cells by walking the shape's own
///     direct row override, then the <c>TextStyle</c> StyleSheet chain, and a shape's flat
///     <c>VerticalAlign</c>/margin cells the same way <c>LineColor</c>/<c>FillForegnd</c> already
///     resolve (Evidence #7 of the originating plan report).
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The neutral default typeface substituted when no literal <c>Font</c> cell can be resolved anywhere in the chain; a <c>"Themed"</c> <c>Font</c> first resolves to the theme's minor Latin typeface, then to the "No Style" StyleSheet's own <c>Font</c>, and only then to this default.</summary>
    private const string DefaultFontFamily = "Arial";

    /// <summary>The default font em-height, in inches, substituted when no literal <c>Size</c> cell can be resolved anywhere in the chain (12pt, Visio's own common default).</summary>
    private const double DefaultFontSizeInches = 12d / 72d;

    /// <summary>The neutral ink color substituted when no literal <c>Color</c> cell can be resolved anywhere in the chain.</summary>
    private static readonly Rgba32 DefaultTextColor = new(0, 0, 0, 255);

    /// <summary>
    ///     Resolves the first literal (present, non-inherited) cell named <paramref name="cellName"/>
    ///     in the row indexed <paramref name="rowIndex"/>, found by walking the <c>TextStyle</c>
    ///     StyleSheet chain starting at <paramref name="startStyleId"/> - mirroring
    ///     <c>ResolveStyleCellValue</c>'s own chain-walk shape, but consulting a row-indexed
    ///     section (<paramref name="rowsSelector"/>) at each stop instead of a flat cell bag. A
    ///     StyleSheet declaring no matching row at all at this index (section-level absence, not
    ///     per-cell <c>F="Inh"</c>) simply has nothing to contribute at this stop and the walk
    ///     continues to its own <c>TextStyle</c> parent - identical "absent falls through" shape
    ///     as the flat cell merge, one level deeper (Evidence #3 of the originating plan report).
    /// </summary>
    /// <param name="startStyleId">The effective <c>TextStyle</c> StyleSheet ID to start the walk at, or <see langword="null"/> to resolve nothing.</param>
    /// <param name="rowIndex">The row index (<c>IX=</c>) to consult at each StyleSheet.</param>
    /// <param name="cellName">The cell name to search for within that row.</param>
    /// <param name="rowsSelector">Selects the row-indexed <c>Character</c>/<c>Paragraph</c> section to consult from a given StyleSheet's own <see cref="VsdxStyleSheetInfo"/>.</param>
    /// <returns>The first literal cell found (carrying both its raw value and formula), or <see langword="null"/> when none is found before the chain terminates or a cycle is detected.</returns>
    private VsdxCell? ResolveTextRowCellValue(
        string? startStyleId,
        int rowIndex,
        string cellName,
        Func<VsdxStyleSheetInfo, IReadOnlyDictionary<int, VsdxCellBag>> rowsSelector)
    {
        var styleSheets = GetStyleSheets();
        var styleId = startStyleId;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (styleId is not null && visited.Add(styleId))
        {
            if (!styleSheets.TryGetValue(styleId, out var style))
            {
                return null;
            }

            if (rowsSelector(style).TryGetValue(rowIndex, out var row) && row.TryGetLiteral(cellName, out var cell))
            {
                return cell;
            }

            styleId = style.TextStyleParentId;
        }

        return null;
    }

    /// <summary>
    ///     Resolves a <c>Section N="Character"</c> cell from the built-in "No Style" StyleSheet
    ///     (ID <c>0</c>, row <c>0</c>) - the document's unthemed base text appearance, used in
    ///     place of an unresolvable <c>"Themed"</c> host-cell <c>THEMEVAL()</c> value (see
    ///     <see cref="ApplyUnthemedFallback"/>).
    /// </summary>
    /// <param name="cellName">The character cell name.</param>
    /// <returns>The "No Style" row-0 value when it is a non-themed literal; otherwise <see langword="null"/>.</returns>
    private string? ResolveUnthemedCharacterCell(string cellName)
    {
        var value = GetStyleSheets().TryGetValue("0", out var noStyle) &&
                    noStyle.CharacterRows.TryGetValue(0, out var row) &&
                    row.TryGetLiteral(cellName, out var cell)
            ? cell.Value
            : null;

        return string.Equals(value, "Themed", StringComparison.Ordinal) ? null : value;
    }

    /// <summary>
    ///     Resolves a shape's flat <c>VerticalAlign</c>/margin cells: the shape's own literal
    ///     value, or the <c>TextStyle</c> StyleSheet chain walked via <c>ResolveStyleCellValue</c>
    ///     (the existing Milestone-3 helper, reused verbatim with a new <c>TextStyleParentId</c>
    ///     lambda - see the originating plan report's Evidence #7).
    /// </summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="textStyleId">The shape's effective <c>TextStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <returns>The resolved <see cref="VsdxEffectiveTextBoxStyle"/>.</returns>
    private VsdxEffectiveTextBoxStyle ResolveTextBoxStyle(VsdxCellBag effectiveCells, string? textStyleId)
    {
        string? ResolveFlat(string cellName) =>
            effectiveCells.TryGetLiteral(cellName, out var cell)
                ? cell.Value
                : ResolveStyleCellValue(textStyleId, cellName, style => style.TextStyleParentId)?.Value;

        var verticalAlign = ResolveFlat("VerticalAlign") switch
        {
            "0" => VsdxVerticalAlign.Top,
            "2" => VsdxVerticalAlign.Bottom,
            _ => VsdxVerticalAlign.Middle,
        };

        return new VsdxEffectiveTextBoxStyle(
            verticalAlign,
            ParseDouble(ResolveFlat("LeftMargin")),
            ParseDouble(ResolveFlat("RightMargin")),
            ParseDouble(ResolveFlat("TopMargin")),
            ParseDouble(ResolveFlat("BottomMargin")));
    }

    /// <summary>
    ///     Resolves a single marker-delimited raw run into its fully-effective
    ///     <see cref="VsdxEffectiveTextRun"/>: for each of <c>Font</c>/<c>Color</c>/<c>Size</c>/
    ///     <c>Style</c> (bold/italic bitflags - bit 0 = bold, bit 1 = italic, the only two bits
    ///     CanvasNet distinguishes, mirroring <c>PptxEffectiveRunProperties</c>'s own bold/italic-
    ///     only scope), the shape's own merged <c>Section N="Character"</c> row (if the run
    ///     carries a <see cref="VsdxRawTextRun.CharacterRowIndex"/> and the shape declares that
    ///     row) wins for that cell only; otherwise the value falls through to
    ///     <see cref="ResolveTextRowCellValue"/> via the <c>TextStyle</c> chain. Paragraph cells
    ///     (<c>HorzAlign</c>/<c>SpLine</c>/<c>SpBefore</c>/<c>SpAfter</c>/<c>IndFirst</c>/
    ///     <c>IndLeft</c>/<c>IndRight</c>) resolve the same two-tier way against
    ///     <c>Section N="Paragraph"</c>. A run with no preceding <c>&lt;cp&gt;</c>/<c>&lt;pp&gt;</c>
    ///     marker resolves through row <c>0</c>'s own default, per every observed fixture.
    /// </summary>
    /// <param name="raw">The raw, marker-delimited run to resolve.</param>
    /// <param name="shapeCharacterRows">The shape's merged (Master-resolved) <c>Section N="Character"</c> rows.</param>
    /// <param name="shapeParagraphRows">The shape's merged (Master-resolved) <c>Section N="Paragraph"</c> rows.</param>
    /// <param name="textStyleId">The shape's effective <c>TextStyle</c> StyleSheet ID, or <see langword="null"/>.</param>
    /// <returns>The resolved <see cref="VsdxEffectiveTextRun"/>.</returns>
    private VsdxEffectiveTextRun ResolveEffectiveRun(
        VsdxRawTextRun raw,
        IReadOnlyDictionary<int, VsdxCellBag> shapeCharacterRows,
        IReadOnlyDictionary<int, VsdxCellBag> shapeParagraphRows,
        string? textStyleId)
    {
        var characterRowIndex = raw.CharacterRowIndex ?? 0;
        var paragraphRowIndex = raw.ParagraphRowIndex ?? 0;

        VsdxCell? ResolveCharacterCellFull(string cellName)
        {
            var resolved = shapeCharacterRows.TryGetValue(characterRowIndex, out var row) && row.TryGetLiteral(cellName, out var cell)
                ? cell
                : ResolveTextRowCellValue(textStyleId, characterRowIndex, cellName, style => style.CharacterRows);

            // A themed Color takes the "No Style" color only when its formula names no resolvable theme slot.
            if (cellName == "Color" &&
                resolved is { } themed &&
                string.Equals(themed.Value, "Themed", StringComparison.Ordinal) &&
                !VsdxColorPalette.ResolvesThemeSlot(themed.Formula, GetTheme()) &&
                ResolveUnthemedCharacterCell(cellName) is { } unthemed)
            {
                return new VsdxCell(cellName, unthemed, null);
            }

            return resolved;
        }

        string? ResolveCharacterCell(string cellName) => ResolveCharacterCellFull(cellName)?.Value;

        string? ResolveParagraphCell(string cellName) =>
            shapeParagraphRows.TryGetValue(paragraphRowIndex, out var row) && row.TryGetLiteral(cellName, out var cell)
                ? cell.Value
                : ResolveTextRowCellValue(textStyleId, paragraphRowIndex, cellName, style => style.ParagraphRows)?.Value;

        var fontFamily = ResolveRunFontFamily(ResolveCharacterCell("Font"));

        var colorCell = ResolveCharacterCellFull("Color");
        var color = VsdxColorPalette.Resolve(colorCell?.Value, colorCell?.Formula, GetTheme(), DefaultTextColor);
        var colorTrans = ParseDouble(ResolveCharacterCell("ColorTrans"));
        if (colorTrans > 0d)
        {
            color = ApplyTransparency(color, ResolveCharacterCell("ColorTrans"));
        }

        var sizeInches = ParseDouble(ResolveCharacterCell("Size"), DefaultFontSizeInches);
        var styleBits = ParseInt(ResolveCharacterCell("Style"));
        var bold = (styleBits & 0x1) != 0;
        var italic = (styleBits & 0x2) != 0;

        var decoration = ResolveRunDecoration(styleBits, ResolveCharacterCell);

        var text = raw.Text;
        var caseValue = ParseInt(ResolveCharacterCell("Case"));
        if (caseValue == 1)
        {
            text = text.ToUpperInvariant();
        }
        else if (caseValue != 0)
        {
            ReportUnsupported("vsdx-text-case-initialcaps", "Character Case 'initial caps' is not supported; text is drawn as authored.");
        }

        if (ParseInt(ResolveCharacterCell("Pos")) != 0)
        {
            ReportUnsupported("vsdx-text-superscript-subscript", "Character Pos (superscript/subscript) is not supported; text is drawn on the baseline.");
        }

        var smallCaps = (styleBits & 0x8) != 0;

        var horzAlign = ResolveParagraphCell("HorzAlign") == "1" ? VsdxHorizontalAlign.Center : VsdxHorizontalAlign.Left;
        var paragraph = new VsdxEffectiveParagraphProperties(
            horzAlign,
            ParseDouble(ResolveParagraphCell("SpLine")),
            ParseDouble(ResolveParagraphCell("SpBefore")),
            ParseDouble(ResolveParagraphCell("SpAfter")),
            ParseDouble(ResolveParagraphCell("IndFirst")),
            ParseDouble(ResolveParagraphCell("IndLeft")),
            ParseDouble(ResolveParagraphCell("IndRight")));

        return new VsdxEffectiveTextRun(text, fontFamily, sizeInches, bold, italic, color, paragraph, decoration, smallCaps);
    }

    /// <summary>Resolves a run's font family from its raw <c>Font</c> cell value.</summary>
    /// <param name="fontRaw">The raw resolved <c>Font</c> cell value, if any.</param>
    /// <returns>The font family name.</returns>
    private string ResolveRunFontFamily(string? fontRaw)
    {
        if (string.IsNullOrEmpty(fontRaw))
        {
            return DefaultFontFamily;
        }

        return string.Equals(fontRaw, "Themed", StringComparison.Ordinal)
            ? GetTheme()?.MinorLatinFont ?? ResolveUnthemedCharacterCell("Font") ?? DefaultFontFamily
            : fontRaw;
    }

    /// <summary>
    ///     Resolves a run's decoration: style bit 2 = underline; Strikethru/DoubleStrikethrough/Overline
    ///     are separate cells.
    /// </summary>
    /// <param name="styleBits">The run's <c>Style</c> bit flags.</param>
    /// <param name="resolveCharacterCell">Resolves a character cell's value by name.</param>
    /// <returns>The combined decoration flags.</returns>
    private static VsdxTextDecoration ResolveRunDecoration(int styleBits, Func<string, string?> resolveCharacterCell)
    {
        var decoration = (styleBits & 0x4) != 0 ? VsdxTextDecoration.Underline : VsdxTextDecoration.None;

        if (ParseInt(resolveCharacterCell("Strikethru")) != 0)
        {
            decoration |= VsdxTextDecoration.Strikethrough;
        }

        if (ParseInt(resolveCharacterCell("DoubleStrikethrough")) != 0)
        {
            decoration |= VsdxTextDecoration.DoubleStrikethrough;
        }

        if (ParseInt(resolveCharacterCell("Overline")) != 0)
        {
            decoration |= VsdxTextDecoration.Overline;
        }

        return decoration;
    }

    /// <summary>
    ///     Expands a small-caps run into segments: each lowercase letter becomes its capital at
    ///     80% of the run's size, every other character is kept as is. A non-small-caps run is
    ///     returned unchanged.
    /// </summary>
    /// <param name="run">The resolved run.</param>
    /// <returns>One or more runs replacing <paramref name="run"/>.</returns>
    private static IEnumerable<VsdxEffectiveTextRun> ExpandSmallCaps(VsdxEffectiveTextRun run)
    {
        if (!run.SmallCaps || run.Text.Length == 0)
        {
            yield return run;
            yield break;
        }

        var segment = new System.Text.StringBuilder();
        var segmentIsLower = false;
        foreach (var ch in run.Text)
        {
            var isLower = char.IsLower(ch);
            if (segment.Length > 0 && isLower != segmentIsLower)
            {
                yield return Segment();
                segment.Clear();
            }

            segmentIsLower = isLower;
            segment.Append(isLower ? char.ToUpperInvariant(ch) : ch);
        }

        if (segment.Length > 0)
        {
            yield return Segment();
        }

        VsdxEffectiveTextRun Segment() =>
            run with { Text = segment.ToString(), SizeInches = segmentIsLower ? run.SizeInches * 0.8d : run.SizeInches, SmallCaps = false };
    }

    /// <summary>Parses a raw cell string as a culture-invariant, finite <see cref="double"/>, returning <paramref name="defaultValue"/> otherwise.</summary>
    private static double ParseDouble(string? rawValue, double defaultValue = 0d) =>
        !string.IsNullOrEmpty(rawValue) &&
        double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) &&
        double.IsFinite(result)
            ? result
            : defaultValue;

    /// <summary>Parses a raw cell string as a culture-invariant <see cref="int"/>, returning <c>0</c> otherwise.</summary>
    private static int ParseInt(string? rawValue) =>
        !string.IsNullOrEmpty(rawValue) &&
        int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
}
