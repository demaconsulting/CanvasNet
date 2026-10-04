using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore defrpr lnspc spcpct spcpts lststyle txstyles sng pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> run/paragraph property-inheritance resolver
///     (Phase 1d).
/// </summary>
/// <remarks>
///     <para>
///         <strong>This deliberately departs from Phase 1b's element-level
///         "first-non-null-element-wins" fallback</strong> (see
///         <c>PptxDocument.Inheritance.cs</c>'s own remarks) - text run/paragraph property
///         resolution instead walks <em>each individual attribute</em>
///         (typeface/size/bold/italic/underline/color for a run; alignment/margin/indent/line
///         spacing for a paragraph) through its own, independent chain, each stopping at the
///         first tier that actually declares that one attribute:
///     </para>
///     <list type="number">
///         <item><description>The run's own <c>&lt;a:rPr&gt;</c> (or, for paragraph attributes, the paragraph's own <c>&lt;a:pPr&gt;</c>).</description></item>
///         <item><description>The paragraph's own <c>&lt;a:pPr&gt;/&lt;a:defRPr&gt;</c> (run attributes only).</description></item>
///         <item>
///             <description>
///                 The placeholder's <see cref="PptxPlaceholderProperties.EffectiveTxBodyListStyle"/>,
///                 indexed by the paragraph's own (0-based, clamped <c>[0,8]</c>) <c>lvl</c> into
///                 its <c>&lt;a:lvl{N+1}pPr&gt;</c> child (run attributes read that level's own
///                 <c>&lt;a:defRPr&gt;</c>; paragraph attributes read the level element directly).
///             </description>
///         </item>
///         <item>
///             <description>
///                 The slide master's own <c>&lt;p:txStyles&gt;</c>, bucketed by the placeholder's
///                 type (see <see cref="SelectMasterTextStyle"/>) and indexed by level the same
///                 way.
///             </description>
///         </item>
///         <item><description>A final, hard-coded, documented default (see each attribute's own resolver).</description></item>
///     </list>
///     <para>
///         This attribute-level granularity is required because real-world presentations
///         routinely declare, for example, only a run's bold flag while leaving its typeface to be
///         inherited from several tiers higher - an element-level "first non-null wins" rule would
///         incorrectly discard every other attribute the instant any single attribute is locally
///         overridden.
///     </para>
/// </remarks>
public sealed partial class PptxDocument
{
    /// <summary>The OOXML schema default run font size, in EMU (18pt: <c>18 * 12700</c>).</summary>
    private const float DefaultFontSizeEmu = 18f * 12700f;

    /// <summary>Converts a <c>sz</c> attribute value (hundredths of a point) to EMU.</summary>
    private const float HundredthsOfPointToEmu = 127f;

    /// <summary>Converts a <c>spcPts val</c> attribute value (hundredths of a point) to EMU.</summary>
    private const float SpcPtsToEmu = 127f;

    /// <summary>
    ///     Resolves a single run's fully-effective character properties, walking the
    ///     attribute-level inheritance chain described in this class's remarks.
    /// </summary>
    /// <param name="run">The run being resolved.</param>
    /// <param name="paragraph">The run's owning paragraph.</param>
    /// <param name="placeholderProperties">The owning shape's resolved placeholder property chain.</param>
    /// <param name="theme">The resolved theme, used to resolve the hard-coded default typeface/color.</param>
    /// <param name="placeholderType">
    ///     The owning shape's placeholder type (for example <c>"title"</c>), or <see cref="string.Empty"/>
    ///     for a non-placeholder shape - used to select the default typeface (title vs. body theme
    ///     font) and the master <c>&lt;p:txStyles&gt;</c> bucket (see <see cref="SelectMasterTextStyle"/>).
    /// </param>
    /// <returns>The resolved <see cref="PptxEffectiveRunProperties"/>.</returns>
    internal static PptxEffectiveRunProperties ResolveEffectiveRunProperties(
        PptxTextRun run,
        PptxParagraph paragraph,
        PptxPlaceholderProperties placeholderProperties,
        PptxTheme theme,
        string placeholderType)
    {
        var level = paragraph.RawProperties.Level;
        var placeholderLevelDefRPr = GetLevelDefRPr(placeholderProperties.EffectiveTxBodyListStyle, level);
        var masterStyle = SelectMasterTextStyle(placeholderProperties, placeholderType);
        var masterLevelDefRPr = GetLevelDefRPr(masterStyle, level);

        var runRPr = run.RawRPr;
        var paragraphDefRPr = paragraph.RawProperties.DefRPrElement;

        var typeface =
            GetTypeface(runRPr) ??
            GetTypeface(paragraphDefRPr) ??
            GetTypeface(placeholderLevelDefRPr) ??
            GetTypeface(masterLevelDefRPr) ??
            DefaultTypeface(theme, placeholderType);

        var sizeEmu =
            GetFontSizeEmu(runRPr) ??
            GetFontSizeEmu(paragraphDefRPr) ??
            GetFontSizeEmu(placeholderLevelDefRPr) ??
            GetFontSizeEmu(masterLevelDefRPr) ??
            DefaultFontSizeEmu;

        var bold =
            GetBoolAttribute(runRPr, "b") ??
            GetBoolAttribute(paragraphDefRPr, "b") ??
            GetBoolAttribute(placeholderLevelDefRPr, "b") ??
            GetBoolAttribute(masterLevelDefRPr, "b") ??
            false;

        var italic =
            GetBoolAttribute(runRPr, "i") ??
            GetBoolAttribute(paragraphDefRPr, "i") ??
            GetBoolAttribute(placeholderLevelDefRPr, "i") ??
            GetBoolAttribute(masterLevelDefRPr, "i") ??
            false;

        var underline =
            GetUnderline(runRPr) ??
            GetUnderline(paragraphDefRPr) ??
            GetUnderline(placeholderLevelDefRPr) ??
            GetUnderline(masterLevelDefRPr) ??
            false;

        var color =
            GetRunColor(runRPr, theme) ??
            GetRunColor(paragraphDefRPr, theme) ??
            GetRunColor(placeholderLevelDefRPr, theme) ??
            GetRunColor(masterLevelDefRPr, theme) ??
            theme.ColorScheme.Dark1;

        return new PptxEffectiveRunProperties(typeface, sizeEmu, bold, italic, underline, color);
    }

    /// <summary>
    ///     Resolves a paragraph's fully-effective properties, walking the same attribute-level
    ///     inheritance chain described in this class's remarks (paragraph attributes only consult
    ///     the paragraph's own <c>&lt;a:pPr&gt;</c> - there is no paragraph-level "defPPr" tier
    ///     analogous to a run's <c>&lt;a:defRPr&gt;</c> tier, since <c>&lt;a:pPr&gt;</c> already is
    ///     the paragraph's own properties element).
    /// </summary>
    /// <param name="paragraph">The paragraph being resolved.</param>
    /// <param name="placeholderProperties">The owning shape's resolved placeholder property chain.</param>
    /// <param name="placeholderType">The owning shape's placeholder type (see <see cref="ResolveEffectiveRunProperties"/>).</param>
    /// <returns>The resolved <see cref="PptxEffectiveParagraphProperties"/>.</returns>
    internal static PptxEffectiveParagraphProperties ResolveEffectiveParagraphProperties(
        PptxParagraph paragraph,
        PptxPlaceholderProperties placeholderProperties,
        string placeholderType)
    {
        var level = paragraph.RawProperties.Level;
        var placeholderLevelElement = GetLevelElement(placeholderProperties.EffectiveTxBodyListStyle, level);
        var masterStyle = SelectMasterTextStyle(placeholderProperties, placeholderType);
        var masterLevelElement = GetLevelElement(masterStyle, level);

        var raw = paragraph.RawProperties;

        var algn = NormalizeAlignment(
            raw.Algn ??
            (string?)placeholderLevelElement?.Attribute("algn") ??
            (string?)masterLevelElement?.Attribute("algn") ??
            "l");

        var marL =
            raw.MarLEmu ??
            (float?)placeholderLevelElement?.Attribute("marL") ??
            (float?)masterLevelElement?.Attribute("marL") ??
            0f;

        var indent =
            raw.IndentEmu ??
            (float?)placeholderLevelElement?.Attribute("indent") ??
            (float?)masterLevelElement?.Attribute("indent") ??
            0f;

        var lineSpacing =
            ParseLineSpacing(raw.LnSpcElement) ??
            ParseLineSpacing(placeholderLevelElement?.Element(DrawingNamespace + "lnSpc")) ??
            ParseLineSpacing(masterLevelElement?.Element(DrawingNamespace + "lnSpc")) ??
            PptxLineSpacing.Default;

        return new PptxEffectiveParagraphProperties(algn, marL, indent, lineSpacing);
    }

    /// <summary>
    ///     Selects which of a slide master's three <c>&lt;p:txStyles&gt;</c> buckets
    ///     (<c>titleStyle</c>/<c>bodyStyle</c>/<c>otherStyle</c>) a given placeholder type
    ///     consults: <c>"title"</c>/<c>"ctrTitle"</c> select <c>titleStyle</c>; an empty
    ///     <paramref name="placeholderType"/> (a non-placeholder shape) selects <c>otherStyle</c>;
    ///     every other type (including <c>"body"</c> and every remapped placeholder type) selects
    ///     <c>bodyStyle</c>.
    /// </summary>
    private static XElement? SelectMasterTextStyle(PptxPlaceholderProperties placeholderProperties, string placeholderType) =>
        placeholderType switch
        {
            "title" or "ctrTitle" => placeholderProperties.MasterTextStyles?.TitleStyle,
            "" => placeholderProperties.MasterTextStyles?.OtherStyle,
            _ => placeholderProperties.MasterTextStyles?.BodyStyle,
        };

    /// <summary>Resolves a level-indexed <c>&lt;a:lvl{N+1}pPr&gt;</c> element from an <c>&lt;a:lstStyle&gt;</c>-shaped parent.</summary>
    private static XElement? GetLevelElement(XElement? listStyleLikeElement, int level) =>
        listStyleLikeElement?.Element(DrawingNamespace + $"lvl{level + 1}pPr");

    /// <summary>Resolves a level-indexed <c>&lt;a:lvl{N+1}pPr&gt;/&lt;a:defRPr&gt;</c> element.</summary>
    private static XElement? GetLevelDefRPr(XElement? listStyleLikeElement, int level) =>
        GetLevelElement(listStyleLikeElement, level)?.Element(DrawingNamespace + "defRPr");

    /// <summary>Extracts an <c>&lt;a:rPr&gt;</c>/<c>&lt;a:defRPr&gt;</c>-shaped element's <c>&lt;a:latin typeface="..."/&gt;</c> value.</summary>
    private static string? GetTypeface(XElement? rPrLikeElement) =>
        (string?)rPrLikeElement?.Element(DrawingNamespace + "latin")?.Attribute("typeface");

    /// <summary>Resolves the hard-coded default typeface: the theme's major font for a title-type placeholder, else its minor font.</summary>
    private static string DefaultTypeface(PptxTheme theme, string placeholderType) =>
        placeholderType is "title" or "ctrTitle" ? theme.FontScheme.MajorFont.Latin : theme.FontScheme.MinorFont.Latin;

    /// <summary>
    ///     Extracts an <c>&lt;a:rPr&gt;</c>/<c>&lt;a:defRPr&gt;</c>-shaped element's <c>sz</c>
    ///     attribute (hundredths of a point), converted to EMU.
    /// </summary>
    private static float? GetFontSizeEmu(XElement? rPrLikeElement)
    {
        var sz = (float?)rPrLikeElement?.Attribute("sz");
        return sz is { } value ? value * HundredthsOfPointToEmu : null;
    }

    /// <summary>
    ///     Parses a boolean-valued attribute (<c>"1"</c>/<c>"true"</c> are truthy, <c>"0"</c>/
    ///     <c>"false"</c> are falsy, per the OOXML schema's <c>xsd:boolean</c> convention), or
    ///     <see langword="null"/> when <paramref name="rPrLikeElement"/> or the attribute is absent.
    /// </summary>
    private static bool? GetBoolAttribute(XElement? rPrLikeElement, string attributeName)
    {
        var value = (string?)rPrLikeElement?.Attribute(attributeName);
        return value switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => null,
        };
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:rPr&gt;</c>/<c>&lt;a:defRPr&gt;</c>-shaped element's <c>u</c>
    ///     (underline) attribute to a boolean: any value other than absent/<c>"none"</c> is
    ///     treated as underlined - the specific underline style (single/double/wavy/etc.) is a
    ///     documented simplification, not distinguished further this phase.
    /// </summary>
    private static bool? GetUnderline(XElement? rPrLikeElement)
    {
        var value = (string?)rPrLikeElement?.Attribute("u");
        return value is null ? null : value != "none";
    }

    /// <summary>Resolves an <c>&lt;a:rPr&gt;</c>/<c>&lt;a:defRPr&gt;</c>-shaped element's <c>&lt;a:solidFill&gt;</c> color child, if any.</summary>
    private static Rgba32? GetRunColor(XElement? rPrLikeElement, PptxTheme theme)
    {
        var colorElement = rPrLikeElement?.Element(DrawingNamespace + "solidFill")?.Elements().FirstOrDefault();
        return colorElement is null ? null : ResolveColor(colorElement, theme);
    }

    /// <summary>
    ///     Normalizes a resolved <c>algn</c> value: <c>"just"</c>/<c>"justLow"</c> (full
    ///     justification) is deliberately resolved to <c>"l"</c>, a documented simplification (see
    ///     the design document's "Text Layout and Rendering (Phase 1d)" section) - redistributing
    ///     inter-word spacing per line is a separable refinement, not implemented this phase.
    /// </summary>
    private static string NormalizeAlignment(string algn) =>
        algn switch
        {
            "ctr" => "ctr",
            "r" => "r",
            _ => "l",
        };

    /// <summary>
    ///     Parses an <c>&lt;a:lnSpc&gt;</c> element into a <see cref="PptxLineSpacing"/>: exactly
    ///     one of its <c>&lt;a:spcPct val="..."/&gt;</c> (a <c>val/100000</c> fraction of a single
    ///     line's natural height) or <c>&lt;a:spcPts val="..."/&gt;</c> (hundredths of a point,
    ///     converted to EMU) child is present per the OOXML schema.
    /// </summary>
    /// <returns><see langword="null"/> when <paramref name="lnSpcElement"/> is absent or declares neither child.</returns>
    private static PptxLineSpacing? ParseLineSpacing(XElement? lnSpcElement)
    {
        if (lnSpcElement is null)
        {
            return null;
        }

        var spcPct = lnSpcElement.Element(DrawingNamespace + "spcPct");
        if (spcPct is not null)
        {
            var val = (float?)spcPct.Attribute("val") ?? 100000f;
            return new PptxLineSpacing(val / 100000f, null);
        }

        var spcPts = lnSpcElement.Element(DrawingNamespace + "spcPts");
        if (spcPts is not null)
        {
            var val = (float?)spcPts.Attribute("val") ?? 0f;
            return new PptxLineSpacing(null, val * SpcPtsToEmu);
        }

        return null;
    }
}
