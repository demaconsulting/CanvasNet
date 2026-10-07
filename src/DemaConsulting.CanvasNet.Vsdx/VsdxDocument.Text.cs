using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward

/// <summary>
///     Implements the <see cref="VsdxDocument"/> <c>&lt;Text&gt;</c> element parser: reads a
///     shape's direct <c>&lt;Text&gt;</c> child's mixed content (literal text interleaved with
///     <c>&lt;cp IX="k"/&gt;</c>/<c>&lt;pp IX="k"/&gt;</c> run/paragraph markers, per the format
///     reference's §8.1) into an ordered sequence of marker-delimited <see cref="VsdxRawTextRun"/>
///     entries, and a shape's own direct <c>&lt;Section N="Character"&gt;</c>/
///     <c>&lt;Section N="Paragraph"&gt;</c> children (no section-level <c>IX=</c>, only
///     <c>&lt;Row IX="k"&gt;</c> children) into a row-indexed <see cref="VsdxCellBag"/> lookup.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Parses a shape's direct <c>&lt;Text&gt;</c> child element into a
    ///     <see cref="VsdxRawText"/>.
    /// </summary>
    /// <param name="textElement">The shape's <c>&lt;Text&gt;</c> element, or <see langword="null"/> when the shape has no <c>&lt;Text&gt;</c> element at all.</param>
    /// <returns><see cref="VsdxRawText.Empty"/> when <paramref name="textElement"/> is <see langword="null"/> or empty; otherwise the parsed runs, per <see cref="BuildRawRuns"/>.</returns>
    internal static VsdxRawText ParseTextElement(XElement? textElement)
    {
        if (textElement is null)
        {
            return VsdxRawText.Empty;
        }

        return new VsdxRawText(BuildRawRuns(textElement));
    }

    /// <summary>
    ///     Walks <paramref name="textElement"/>'s mixed content (<see cref="XText"/> nodes
    ///     interleaved with <c>&lt;cp&gt;</c>/<c>&lt;pp&gt;</c> child elements) in document order,
    ///     per the format reference's §8.1 rule: a <c>&lt;cp IX="k"/&gt;</c> sets the current
    ///     character-row index for every subsequent text segment until the next <c>&lt;cp&gt;</c>
    ///     or the end of the element; <c>&lt;pp IX="k"/&gt;</c> does the same for the paragraph-
    ///     row index, independently. A text segment with no preceding marker of a given kind
    ///     carries <see langword="null"/> for that axis (resolved through row <c>0</c>'s own
    ///     StyleSheet-chain default by <c>VsdxDocument.TextStyle.cs</c>). Adjacent text nodes
    ///     with no intervening marker are merged into a single run.
    /// </summary>
    /// <param name="textElement">The <c>&lt;Text&gt;</c> element to walk.</param>
    /// <returns>The parsed runs, in document order; empty when the element carries no text content at all.</returns>
    private static IReadOnlyList<VsdxRawTextRun> BuildRawRuns(XElement textElement)
    {
        var ns = textElement.Name.Namespace;
        var runs = new List<VsdxRawTextRun>();

        int? characterRowIndex = null;
        int? paragraphRowIndex = null;
        var pendingText = new System.Text.StringBuilder();

        void FlushPendingText()
        {
            if (pendingText.Length == 0)
            {
                return;
            }

            runs.Add(new VsdxRawTextRun(pendingText.ToString(), characterRowIndex, paragraphRowIndex));
            pendingText.Clear();
        }

        foreach (var node in textElement.Nodes())
        {
            switch (node)
            {
                case XText textNode:
                    pendingText.Append(textNode.Value);
                    break;

                case XElement { Name.LocalName: "cp" } cpElement when cpElement.Name.Namespace == ns:
                    FlushPendingText();
                    characterRowIndex = ParseMarkerIndex(cpElement);
                    break;

                case XElement { Name.LocalName: "pp" } ppElement when ppElement.Name.Namespace == ns:
                    FlushPendingText();
                    paragraphRowIndex = ParseMarkerIndex(ppElement);
                    break;
            }
        }

        FlushPendingText();
        return runs;
    }

    /// <summary>Parses a <c>&lt;cp&gt;</c>/<c>&lt;pp&gt;</c> marker element's <c>IX=</c> attribute.</summary>
    /// <param name="markerElement">The marker element to parse.</param>
    /// <returns>The parsed row index, or <see langword="null"/> when absent/non-numeric.</returns>
    private static int? ParseMarkerIndex(XElement markerElement)
    {
        var indexText = (string?)markerElement.Attribute("IX");
        return int.TryParse(
            indexText,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var index)
            ? index
            : null;
    }

    /// <summary>
    ///     Parses a shape's (or a StyleSheet's) direct <c>&lt;Section N="{sectionName}"&gt;</c>
    ///     child's <c>&lt;Row IX="k"&gt;</c> children into a row-indexed lookup. Unlike
    ///     <c>Section N="Geometry"</c>, a <c>Character</c>/<c>Paragraph</c> section carries no
    ///     section-level <c>IX=</c> attribute of its own (confirmed directly against
    ///     <c>davehoward-test12-colors.vsdx</c>'s page1.xml Shape ID='2') - only its <c>Row</c>
    ///     children are indexed, and there is no <c>Del=</c>/<c>T=</c> concept to carry, so no new
    ///     raw-row type (unlike <see cref="VsdxGeometryRowRaw"/>) is needed: each row parses
    ///     directly into a plain <see cref="VsdxCellBag"/>.
    /// </summary>
    /// <param name="element">The <c>&lt;Shape&gt;</c>/<c>&lt;StyleSheet&gt;</c> element whose direct <c>&lt;Section N="{sectionName}"&gt;</c> child (if any) should be parsed.</param>
    /// <param name="sectionName">The section's <c>N=</c> attribute value (<c>"Character"</c> or <c>"Paragraph"</c>).</param>
    /// <returns>A lookup from each row's <c>IX=</c> attribute to its parsed cell bag. Empty when <paramref name="element"/> declares no matching section, or the section has no (or only non-numeric-indexed) rows.</returns>
    private static IReadOnlyDictionary<int, VsdxCellBag> ParseTextSectionRows(XElement element, string sectionName)
    {
        var ns = element.Name.Namespace;
        var sectionElement = element.Elements(ns + "Section")
            .FirstOrDefault(section => (string?)section.Attribute("N") == sectionName);

        var result = new Dictionary<int, VsdxCellBag>();
        if (sectionElement is null)
        {
            return result;
        }

        foreach (var rowElement in sectionElement.Elements(ns + "Row"))
        {
            var indexText = (string?)rowElement.Attribute("IX");
            if (int.TryParse(
                    indexText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var index))
            {
                result[index] = VsdxCellBag.Parse(rowElement);
            }
        }

        return result;
    }
}
