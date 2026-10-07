using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore txbody bodypr lstyle lnspc spcbef spcaft spcpct spcpts marl pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> DrawingML text-body/paragraph/run parser (Phase
///     1d): <c>&lt;p:txBody&gt;</c>/<c>&lt;a:bodyPr&gt;</c>/<c>&lt;a:p&gt;</c>/<c>&lt;a:pPr&gt;</c>/
///     <c>&lt;a:r&gt;</c>/<c>&lt;a:rPr&gt;</c>/<c>&lt;a:t&gt;</c> - see
///     <c>pptx-document.md</c>'s "Text Layout and Rendering (Phase 1d)" design section for the
///     full structural parsing boundary, and <c>PptxDocument.TextInheritance.cs</c> for how the
///     raw, unresolved properties this parser extracts are later resolved against the
///     placeholder/layout/master/theme inheritance chain.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The OOXML schema default left/right text inset, in EMU (0.1 inch).</summary>
    private const float DefaultInsetLeftRightEmu = 91440f;

    /// <summary>The OOXML schema default top/bottom text inset, in EMU (0.05 inch).</summary>
    private const float DefaultInsetTopBottomEmu = 45720f;

    /// <summary>The highest paragraph list level this phase resolves (<c>&lt;a:lvl9pPr&gt;</c>, 0-based).</summary>
    internal const int MaxParagraphLevel = 8;

    /// <summary>
    ///     Parses a <c>&lt;p:txBody&gt;</c> element into a <see cref="PptxTextBody"/>: its
    ///     resolved <c>&lt;a:bodyPr&gt;</c> properties plus its ordered <c>&lt;a:p&gt;</c>
    ///     paragraphs.
    /// </summary>
    /// <param name="txBodyElement">The <c>&lt;p:txBody&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="PptxTextBody"/>.</returns>
    internal static PptxTextBody ParseTextBody(XElement txBodyElement)
    {
        var properties = ParseBodyProperties(txBodyElement.Element(DrawingNamespace + "bodyPr"));
        var paragraphs = txBodyElement.Elements(DrawingNamespace + "p").Select(ParseParagraph).ToList();
        return new PptxTextBody(properties, paragraphs);
    }

    /// <summary>
    ///     Parses an <c>&lt;a:bodyPr&gt;</c> element into a <see cref="PptxBodyProperties"/>,
    ///     applying the OOXML schema's documented defaults for every attribute/child the element
    ///     (or an absent element itself) omits.
    /// </summary>
    /// <param name="bodyPrElement">The <c>&lt;a:bodyPr&gt;</c> element to parse, or <see langword="null"/> when absent.</param>
    /// <returns>The parsed <see cref="PptxBodyProperties"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="bodyPrElement"/>'s <c>lIns</c>, <c>tIns</c>, <c>rIns</c>,
    ///     or <c>bIns</c> attribute is present but not a valid numeric value.
    /// </exception>
    internal static PptxBodyProperties ParseBodyProperties(XElement? bodyPrElement)
    {
        if (bodyPrElement is null)
        {
            return new PptxBodyProperties(
                PptxTextAnchor.Top,
                PptxTextWrap.Square,
                DefaultInsetLeftRightEmu,
                DefaultInsetTopBottomEmu,
                DefaultInsetLeftRightEmu,
                DefaultInsetTopBottomEmu,
                null);
        }

        var anchor = (string?)bodyPrElement.Attribute("anchor") switch
        {
            "ctr" => PptxTextAnchor.Middle,
            "b" => PptxTextAnchor.Bottom,
            _ => PptxTextAnchor.Top,
        };

        var wrap = (string?)bodyPrElement.Attribute("wrap") == "none" ? PptxTextWrap.None : PptxTextWrap.Square;

        var insetLeft = ParseInsetAttribute(bodyPrElement, "lIns", DefaultInsetLeftRightEmu);
        var insetTop = ParseInsetAttribute(bodyPrElement, "tIns", DefaultInsetTopBottomEmu);
        var insetRight = ParseInsetAttribute(bodyPrElement, "rIns", DefaultInsetLeftRightEmu);
        var insetBottom = ParseInsetAttribute(bodyPrElement, "bIns", DefaultInsetTopBottomEmu);

        var autofitElement =
            bodyPrElement.Element(DrawingNamespace + "noAutofit") ??
            bodyPrElement.Element(DrawingNamespace + "normAutofit") ??
            bodyPrElement.Element(DrawingNamespace + "spAutoFit");

        return new PptxBodyProperties(anchor, wrap, insetLeft, insetTop, insetRight, insetBottom, autofitElement);
    }

    /// <summary>
    ///     Parses one of an <c>&lt;a:bodyPr&gt;</c> element's <c>lIns</c>/<c>tIns</c>/<c>rIns</c>/
    ///     <c>bIns</c> inset attributes, defaulting to <paramref name="defaultValueEmu"/> when
    ///     absent.
    /// </summary>
    /// <param name="bodyPrElement">The <c>&lt;a:bodyPr&gt;</c> element to inspect.</param>
    /// <param name="attributeName">The inset attribute's name (<c>"lIns"</c>, <c>"tIns"</c>, <c>"rIns"</c>, or <c>"bIns"</c>).</param>
    /// <param name="defaultValueEmu">The value to use, in EMU, when <paramref name="attributeName"/> is absent.</param>
    /// <returns>The parsed inset value, in EMU.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="attributeName"/> is present but not a valid numeric value.
    /// </exception>
    private static float ParseInsetAttribute(XElement bodyPrElement, string attributeName, float defaultValueEmu)
    {
        try
        {
            return (float?)bodyPrElement.Attribute(attributeName) ?? defaultValueEmu;
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(
                $"An <a:bodyPr> element has a non-numeric '{attributeName}' attribute value '{(string?)bodyPrElement.Attribute(attributeName)}'.",
                ex);
        }
    }

    /// <summary>
    ///     Parses an <c>&lt;a:p&gt;</c> element into a <see cref="PptxParagraph"/>: its raw,
    ///     unresolved properties plus its ordered content items. An <c>&lt;a:p&gt;</c> with no
    ///     recognized child produces a valid, empty paragraph (a blank line) - not an error.
    /// </summary>
    /// <remarks>
    ///     <c>&lt;a:br/&gt;</c> is DrawingML's explicit line-break element: it is preserved as a
    ///     <see cref="PptxLineBreakItem"/> in document order alongside <c>&lt;a:r&gt;</c> runs
    ///     (wrapped as <see cref="PptxRunItem"/>), rather than being dropped, so that the layout
    ///     stage (<c>PptxDocument.TextLayout.cs</c>) can honor it as an explicit line boundary.
    ///     <c>&lt;a:fld&gt;</c> (auto-text fields such as <c>type="slidenum"</c> or
    ///     <c>type="datetime1"</c>) shares <c>&lt;a:r&gt;</c>'s <c>&lt;a:rPr&gt;</c>/<c>&lt;a:t&gt;</c>
    ///     structure and is parsed the same way, rendering PowerPoint's cached field text since
    ///     CanvasNet has no live engine to recompute the field's current value.
    /// </remarks>
    /// <param name="pElement">The <c>&lt;a:p&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="PptxParagraph"/>.</returns>
    internal static PptxParagraph ParseParagraph(XElement pElement)
    {
        var properties = ParseParagraphProperties(pElement.Element(DrawingNamespace + "pPr"));
        var items = new List<PptxParagraphItem>();
        foreach (var child in pElement.Elements())
        {
            if (child.Name == DrawingNamespace + "r" || child.Name == DrawingNamespace + "fld")
            {
                // <a:fld> (e.g. type="slidenum"/"datetime") carries the same <a:rPr>/<a:t>
                // shape as <a:r>, but its <a:t> holds PowerPoint's last-computed cached field
                // value rather than literal authored text. CanvasNet has no live PowerPoint
                // engine to recompute the field, so (matching how most static OOXML renderers
                // handle fields) the cached text is rendered as-is.
                items.Add(new PptxRunItem(ParseRun(child)));
            }
            else if (child.Name == DrawingNamespace + "br")
            {
                items.Add(PptxLineBreakItem.Instance);
            }
        }

        return new PptxParagraph(properties, items);
    }

    /// <summary>
    ///     Parses an <c>&lt;a:pPr&gt;</c> element into a <see cref="PptxRawParagraphProperties"/>,
    ///     retaining every value as the paragraph's own, unresolved value - no inheritance
    ///     resolution happens here.
    /// </summary>
    /// <param name="pPrElement">The <c>&lt;a:pPr&gt;</c> element to parse, or <see langword="null"/> when absent.</param>
    /// <returns>The parsed <see cref="PptxRawParagraphProperties"/>.</returns>
    internal static PptxRawParagraphProperties ParseParagraphProperties(XElement? pPrElement)
    {
        if (pPrElement is null)
        {
            return new PptxRawParagraphProperties(0, null, null, null, null, null, null, null);
        }

        var level = Math.Clamp(ParseOptionalInt(pPrElement, "lvl") ?? 0, 0, MaxParagraphLevel);
        var algn = (string?)pPrElement.Attribute("algn");
        var marL = (float?)pPrElement.Attribute("marL");
        var indent = (float?)pPrElement.Attribute("indent");
        var lnSpc = pPrElement.Element(DrawingNamespace + "lnSpc");
        var spcBefore = pPrElement.Element(DrawingNamespace + "spcBef");
        var spcAfter = pPrElement.Element(DrawingNamespace + "spcAft");
        var defRPr = pPrElement.Element(DrawingNamespace + "defRPr");
        var bulletProperties = ParseBulletProperties(pPrElement);

        return new PptxRawParagraphProperties(level, algn, marL, indent, lnSpc, spcBefore, spcAfter, defRPr, bulletProperties);
    }

    /// <summary>
    ///     Parses an <c>&lt;a:pPr&gt;</c>-shaped element's raw bullet/numbering markup into a
    ///     <see cref="PptxRawBulletProperties"/>, retaining each of the four independent
    ///     choice-group elements (type, color, font, size) unresolved - see
    ///     <see cref="PptxDocument.ResolveEffectiveBulletProperties"/> for inheritance resolution.
    ///     Also used, with the same element shape, to extract bullet markup from a placeholder/
    ///     master level-indexed <c>&lt;a:lvl{N}pPr&gt;</c> element.
    /// </summary>
    /// <param name="pPrLikeElement">The <c>&lt;a:pPr&gt;</c>/<c>&lt;a:lvl{N}pPr&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="PptxRawBulletProperties"/>.</returns>
    internal static PptxRawBulletProperties ParseBulletProperties(XElement pPrLikeElement) =>
        new(
            GetBulletTypeElement(pPrLikeElement),
            GetBulletColorElement(pPrLikeElement),
            GetBulletFontElement(pPrLikeElement),
            GetBulletSizeElement(pPrLikeElement));

    /// <summary>
    ///     Parses an <c>&lt;a:r&gt;</c> element into a <see cref="PptxTextRun"/>: its own raw,
    ///     unresolved <c>&lt;a:rPr&gt;</c> element plus its <c>&lt;a:t&gt;</c> text (defaulting to
    ///     <see cref="string.Empty"/> when <c>&lt;a:t&gt;</c> is absent, per the OOXML schema).
    /// </summary>
    /// <param name="rElement">The <c>&lt;a:r&gt;</c> element to parse.</param>
    /// <returns>The parsed <see cref="PptxTextRun"/>.</returns>
    internal static PptxTextRun ParseRun(XElement rElement)
    {
        var rPr = rElement.Element(DrawingNamespace + "rPr");
        var text = rElement.Element(DrawingNamespace + "t")?.Value ?? string.Empty;
        return new PptxTextRun(rPr, text);
    }

    /// <summary>Parses an optional integer attribute, returning <see langword="null"/> when absent or non-numeric.</summary>
    private static int? ParseOptionalInt(XElement element, string attributeName)
    {
        var value = (string?)element.Attribute(attributeName);
        return value is not null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
