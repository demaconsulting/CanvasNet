namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio THEMEVAL

/// <summary>
///     A single parsed <c>&lt;StyleSheet&gt;</c> element from <c>visio/document.xml</c>'s
///     <c>&lt;StyleSheets&gt;</c> index.
/// </summary>
/// <param name="Cells">The style's own direct <c>&lt;Cell&gt;</c> children.</param>
/// <param name="LineStyleParentId">The style's own <c>LineStyle=</c> attribute (the next StyleSheet ID to consult for a <c>Line*</c> cell left unresolved here), or <see langword="null"/> when absent (a terminal/built-in style, for example ID <c>0</c>, "No Style").</param>
/// <param name="FillStyleParentId">The style's own <c>FillStyle=</c> attribute (the next StyleSheet ID to consult for a <c>Fill*</c> cell), or <see langword="null"/> when absent.</param>
/// <param name="TextStyleParentId">The style's own <c>TextStyle=</c> attribute (the next StyleSheet ID to consult for a text/<c>Char*</c> cell), or <see langword="null"/> when absent.</param>
/// <param name="CharacterRows">The style's own direct <c>&lt;Section N="Character"&gt;</c> child's <c>&lt;Row IX="k"&gt;</c> children, keyed by row index. Empty when the style declares no such section at all - meaning it defers to <see cref="TextStyleParentId"/>'s own rows for the whole row (see <c>VsdxDocument.TextStyle.cs</c>).</param>
/// <param name="ParagraphRows">The style's own direct <c>&lt;Section N="Paragraph"&gt;</c> child's <c>&lt;Row IX="k"&gt;</c> children, keyed by row index. Same "absent section defers to parent" convention as <see cref="CharacterRows"/>.</param>
internal sealed record VsdxStyleSheetInfo(
    VsdxCellBag Cells,
    string? LineStyleParentId,
    string? FillStyleParentId,
    string? TextStyleParentId,
    IReadOnlyDictionary<int, VsdxCellBag> CharacterRows,
    IReadOnlyDictionary<int, VsdxCellBag> ParagraphRows);

/// <summary>
///     Implements the <see cref="VsdxDocument"/> StyleSheet chain resolver: parses
///     <c>visio/document.xml</c>'s <c>&lt;StyleSheets&gt;</c> index (including the built-in
///     StyleSheet IDs <c>0</c>-<c>3</c>, which every in-scope fixture serializes explicitly rather
///     than leaving implicit) and walks a style's own category-specific parent chain
///     (<c>LineStyle</c>/<c>FillStyle</c>/<c>TextStyle</c>) to find the first literal
///     (present, non-<c>"Inh"</c>) cell of a given name.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The parsed StyleSheet index, keyed by StyleSheet <c>ID</c>, populated lazily by <see cref="GetStyleSheets"/>.</summary>
    private IReadOnlyDictionary<string, VsdxStyleSheetInfo>? _styleSheets;

    /// <summary>
    ///     Resolves the first literal (present, non-inherited) cell named <paramref name="cellName"/>
    ///     found by walking the StyleSheet chain starting at <paramref name="startStyleId"/>, each
    ///     hop following <paramref name="nextParentId"/>'s own category-specific parent pointer.
    /// </summary>
    /// <remarks>
    ///     Returns the full <see cref="VsdxCell"/> (not just its raw <c>V</c> string) so a caller
    ///     needing theme-awareness (see <c>VsdxDocument.Paint.cs</c>'s <c>ResolveLineCellValue</c>/
    ///     <c>ResolveFillCellValue</c>) can also consult the cell's own <c>F</c> formula text for a
    ///     <c>THEMEVAL("slotName")</c> reference - see <c>VsdxColorPalette.Resolve(string?, string?, VsdxTheme?, Rgba32)</c>.
    /// </remarks>
    /// <param name="startStyleId">The effective StyleSheet ID to start the walk at, or <see langword="null"/> to resolve nothing.</param>
    /// <param name="cellName">The cell name to search for at each style in the chain.</param>
    /// <param name="nextParentId">Selects the next StyleSheet ID to consult from a given style's own <see cref="VsdxStyleSheetInfo"/> (its <c>LineStyle</c>, <c>FillStyle</c>, or <c>TextStyle</c> attribute, matching the category <paramref name="cellName"/> belongs to).</param>
    /// <returns>The first literal cell found, or <see langword="null"/> when none is found before the chain terminates (a style with no further parent pointer) or a cycle is detected.</returns>
    private VsdxCell? ResolveStyleCellValue(string? startStyleId, string cellName, Func<VsdxStyleSheetInfo, string?> nextParentId)
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

            if (style.Cells.TryGetLiteral(cellName, out var cell))
            {
                return cell;
            }

            styleId = nextParentId(style);
        }

        return null;
    }

    /// <summary>Parses and caches <c>visio/document.xml</c>'s <c>&lt;StyleSheets&gt;</c> index.</summary>
    /// <returns>The parsed index, keyed by StyleSheet <c>ID</c>. Empty when the document declares no <c>&lt;StyleSheets&gt;</c> element at all.</returns>
    private IReadOnlyDictionary<string, VsdxStyleSheetInfo> GetStyleSheets()
    {
        if (_styleSheets is not null)
        {
            return _styleSheets;
        }

        var documentRoot = LoadPartXmlRoot(_documentPartPath);
        var styleSheetsElement = documentRoot.Element(VsdxMainNamespace + "StyleSheets");

        var result = new Dictionary<string, VsdxStyleSheetInfo>(StringComparer.Ordinal);
        if (styleSheetsElement is not null)
        {
            foreach (var styleSheetElement in styleSheetsElement.Elements(VsdxMainNamespace + "StyleSheet"))
            {
                var id = (string?)styleSheetElement.Attribute("ID");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                result[id] = new VsdxStyleSheetInfo(
                    VsdxCellBag.Parse(styleSheetElement),
                    (string?)styleSheetElement.Attribute("LineStyle"),
                    (string?)styleSheetElement.Attribute("FillStyle"),
                    (string?)styleSheetElement.Attribute("TextStyle"),
                    ParseTextSectionRows(styleSheetElement, "Character"),
                    ParseTextSectionRows(styleSheetElement, "Paragraph"));
            }
        }

        _styleSheets = result;
        return result;
    }
}
