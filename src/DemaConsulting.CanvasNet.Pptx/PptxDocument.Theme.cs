using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore clrscheme fontscheme srgbclr sysclr hlink folhlink pptx

/// <summary>
///     Implements the <see cref="PptxDocument"/> theme parser (Phase 1b): parses
///     <c>ppt/theme/themeN.xml</c> (located via a slide master's <c>/theme</c> relationship) into
///     a <see cref="PptxTheme"/> - its color scheme (<c>&lt;a:clrScheme&gt;</c>) and font scheme
///     (<c>&lt;a:fontScheme&gt;</c>). Lazy and cached by resolved theme part path, so multiple
///     masters sharing one theme file parse it only once.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>The XML namespace used by DrawingML elements (<c>&lt;a:...&gt;</c>), including theme contents.</summary>
    private static readonly XNamespace DrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Caches each theme's parsed form, keyed by its resolved part path, on first access.</summary>
    private readonly Dictionary<string, PptxTheme> _themeCache;

    /// <summary>
    ///     Returns the parsed theme at <paramref name="themePartPath"/>, parsing and caching it on
    ///     first access.
    /// </summary>
    /// <param name="themePartPath">The theme part's resolved path (for example <c>"ppt/theme/theme1.xml"</c>).</param>
    /// <returns>The parsed <see cref="PptxTheme"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the theme part is missing or not well-formed XML, when its
    ///     <c>&lt;a:theme&gt;/&lt;a:themeElements&gt;/&lt;a:clrScheme&gt;</c> or
    ///     <c>.../&lt;a:fontScheme&gt;</c> element is missing, when a color scheme slot's color
    ///     element is missing, of an unrecognized shape, or has an invalid hexadecimal color
    ///     value, or when a font scheme's major/minor font collection is missing its
    ///     <c>&lt;a:latin&gt;</c>/<c>&lt;a:ea&gt;</c>/<c>&lt;a:cs&gt;</c> element.
    /// </exception>
    internal PptxTheme GetTheme(string themePartPath)
    {
        if (_themeCache.TryGetValue(themePartPath, out var cached))
        {
            return cached;
        }

        var root = LoadPartXmlRoot(themePartPath);
        var themeElements = root.Element(DrawingNamespace + "themeElements") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:themeElements> element.");

        var clrSchemeElement = themeElements.Element(DrawingNamespace + "clrScheme") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:clrScheme> element.");
        var colorScheme = ParseColorScheme(clrSchemeElement, themePartPath);

        var fontSchemeElement = themeElements.Element(DrawingNamespace + "fontScheme") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:fontScheme> element.");
        var fontScheme = ParseFontScheme(fontSchemeElement, themePartPath);

        var bgFillStyleList = ParseBgFillStyleList(themeElements);

        var theme = new PptxTheme(colorScheme, fontScheme, bgFillStyleList);
        _themeCache[themePartPath] = theme;
        return theme;
    }

    /// <summary>
    ///     Resolves the effective <see cref="PptxColorMap"/> for a single slide: the slide's own
    ///     <c>&lt;p:clrMapOvr&gt;</c> when it wraps an <c>&lt;a:overrideClrMapping&gt;</c>, else
    ///     its layout's own <c>&lt;p:clrMapOvr&gt;</c> when it wraps an
    ///     <c>&lt;a:overrideClrMapping&gt;</c>, else <paramref name="masterColorMap"/> unchanged.
    /// </summary>
    /// <remarks>
    ///     A <c>&lt;p:clrMapOvr&gt;</c> wrapping <c>&lt;a:masterClrMapping/&gt;</c> (rather than
    ///     <c>&lt;a:overrideClrMapping&gt;</c>) is an explicit, schema-defined sentinel meaning
    ///     "this tier declares no override" - it must fall through to the next tier exactly as if
    ///     <c>&lt;p:clrMapOvr&gt;</c> were absent entirely, not be misread as "override with an
    ///     empty map".
    /// </remarks>
    /// <param name="slideClrMapOvr">The slide's own <see cref="PptxSlide.ClrMapOvr"/>, or <see langword="null"/>.</param>
    /// <param name="layoutClrMapOvr">The slide's layout's own <see cref="PptxLayout.ClrMapOvr"/>, or <see langword="null"/>.</param>
    /// <param name="masterColorMap">That layout's master's own <see cref="PptxMaster.ColorMap"/> - the final fallback.</param>
    /// <returns>The effective <see cref="PptxColorMap"/> to use when resolving this slide's <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c> scheme colors.</returns>
    internal static PptxColorMap ResolveEffectiveColorMap(
        XElement? slideClrMapOvr, XElement? layoutClrMapOvr, PptxColorMap masterColorMap) =>
        TryParseOverrideClrMapping(slideClrMapOvr) ??
        TryParseOverrideClrMapping(layoutClrMapOvr) ??
        masterColorMap;

    /// <summary>
    ///     Parses a single <c>&lt;p:clrMapOvr&gt;</c> element's <c>&lt;a:overrideClrMapping
    ///     bg1="..." tx1="..." bg2="..." tx2="..." .../&gt;</c> child into a
    ///     <see cref="PptxColorMap"/>, reading only its <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c>
    ///     attributes (see <see cref="PptxColorMap"/>'s own remarks for why <c>accentN</c>/
    ///     <c>hlink</c>/<c>folHlink</c> are not carried).
    /// </summary>
    /// <param name="clrMapOvrElement">The <c>&lt;p:clrMapOvr&gt;</c> element, or <see langword="null"/>.</param>
    /// <returns>
    ///     The parsed <see cref="PptxColorMap"/>, or <see langword="null"/> when
    ///     <paramref name="clrMapOvrElement"/> is itself <see langword="null"/>, declares no
    ///     recognized child at all, or wraps <c>&lt;a:masterClrMapping/&gt;</c> (the "no override
    ///     at this tier" sentinel - see <see cref="ResolveEffectiveColorMap"/>'s remarks).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="clrMapOvrElement"/> wraps an
    ///     <c>&lt;a:overrideClrMapping&gt;</c> missing any of its required <c>bg1</c>/<c>tx1</c>/
    ///     <c>bg2</c>/<c>tx2</c> attributes.
    /// </exception>
    private static PptxColorMap? TryParseOverrideClrMapping(XElement? clrMapOvrElement)
    {
        var overrideElement = clrMapOvrElement?.Element(DrawingNamespace + "overrideClrMapping");
        if (overrideElement is null)
        {
            return null;
        }

        string RequiredAttribute(string name) =>
            (string?)overrideElement.Attribute(name) ??
            throw new InvalidDataException($"A <p:clrMapOvr>/<a:overrideClrMapping> element has no '{name}' attribute.");

        return new PptxColorMap(
            RequiredAttribute("bg1"),
            RequiredAttribute("tx1"),
            RequiredAttribute("bg2"),
            RequiredAttribute("tx2"));
    }

    /// <summary>
    ///     Parses a theme's optional <c>&lt;a:fmtScheme&gt;/&lt;a:bgFillStyleLst&gt;</c> element
    ///     into its raw, unparsed fill-definition child elements, in document order.
    /// </summary>
    /// <param name="themeElementsElement">The theme's <c>&lt;a:themeElements&gt;</c> element.</param>
    /// <returns>
    ///     The <c>&lt;a:bgFillStyleLst&gt;</c>'s children, or an empty list when the theme
    ///     declares no <c>&lt;a:fmtScheme&gt;</c> or no <c>&lt;a:bgFillStyleLst&gt;</c> at all -
    ///     this content is rare in practice (only consulted by a <c>&lt;p:bgRef&gt;</c> background
    ///     reference), so its absence is tolerated rather than treated as malformed.
    /// </returns>
    private static IReadOnlyList<XElement> ParseBgFillStyleList(XElement themeElementsElement)
    {
        var bgFillStyleLst = themeElementsElement.Element(DrawingNamespace + "fmtScheme")?
            .Element(DrawingNamespace + "bgFillStyleLst");
        return bgFillStyleLst is null ? [] : bgFillStyleLst.Elements().ToList();
    }

    /// <summary>Parses all 12 named slots of <c>&lt;a:clrScheme&gt;</c> into a <see cref="PptxColorScheme"/>.</summary>
    private static PptxColorScheme ParseColorScheme(XElement clrSchemeElement, string themePartPath) =>
        new(
            ParseSchemeColor(clrSchemeElement, "dk1", themePartPath),
            ParseSchemeColor(clrSchemeElement, "lt1", themePartPath),
            ParseSchemeColor(clrSchemeElement, "dk2", themePartPath),
            ParseSchemeColor(clrSchemeElement, "lt2", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent1", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent2", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent3", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent4", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent5", themePartPath),
            ParseSchemeColor(clrSchemeElement, "accent6", themePartPath),
            ParseSchemeColor(clrSchemeElement, "hlink", themePartPath),
            ParseSchemeColor(clrSchemeElement, "folHlink", themePartPath));

    /// <summary>
    ///     Parses a single named color-scheme slot (for example <c>&lt;a:dk1&gt;</c>), resolving
    ///     its single color-definition child element - either <c>&lt;a:srgbClr val="RRGGBB"/&gt;</c>
    ///     (resolved directly) or <c>&lt;a:sysClr val="..." lastClr="RRGGBB"/&gt;</c> (resolved via
    ///     its cached <c>lastClr</c> RGB equivalent) - into an <see cref="Rgba32"/> value.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the named slot element is missing, has no color-definition child, has an
    ///     unrecognized color-definition element, or that element's hex color value is missing or
    ///     not a valid six-digit <c>RRGGBB</c> string (OOXML's <c>srgbClr/@val</c> and
    ///     <c>sysClr/@lastClr</c> are always exactly six hex digits - an eight-digit
    ///     <c>AARRGGBB</c> value, which <see cref="Rgba32.Parse(string)"/> would otherwise also
    ///     accept, misinterpreting its first byte as alpha, must be rejected rather than silently
    ///     misread).
    /// </exception>
    private static Rgba32 ParseSchemeColor(XElement clrSchemeElement, string slotName, string themePartPath)
    {
        var slotElement = clrSchemeElement.Element(DrawingNamespace + slotName) ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:{slotName}> color scheme slot.");

        var colorElement = slotElement.Elements().FirstOrDefault() ??
            throw new InvalidDataException(
                $"Theme part '{themePartPath}' color scheme slot '{slotName}' has no color definition.");

        string hex;
        if (colorElement.Name == DrawingNamespace + "srgbClr")
        {
            hex = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException(
                    $"Theme part '{themePartPath}' color scheme slot '{slotName}' has an <a:srgbClr> with no 'val' attribute.");
        }
        else if (colorElement.Name == DrawingNamespace + "sysClr")
        {
            hex = (string?)colorElement.Attribute("lastClr") ??
                throw new InvalidDataException(
                    $"Theme part '{themePartPath}' color scheme slot '{slotName}' has an <a:sysClr> with no 'lastClr' attribute.");
        }
        else
        {
            throw new InvalidDataException(
                $"Theme part '{themePartPath}' color scheme slot '{slotName}' has an unrecognized color definition element '{colorElement.Name.LocalName}'.");
        }

        if (hex.Length != 6)
        {
            throw new InvalidDataException(
                $"Theme part '{themePartPath}' color scheme slot '{slotName}' has a color value '{hex}' that is not exactly six hexadecimal digits (OOXML's RRGGBB form).");
        }

        try
        {
            return Rgba32.Parse("#" + hex);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(
                $"Theme part '{themePartPath}' color scheme slot '{slotName}' has an invalid hexadecimal color value '{hex}'.", ex);
        }
    }

    /// <summary>Parses <c>&lt;a:fontScheme&gt;</c>'s major and minor font collections into a <see cref="PptxFontScheme"/>.</summary>
    private static PptxFontScheme ParseFontScheme(XElement fontSchemeElement, string themePartPath)
    {
        var majorFontElement = fontSchemeElement.Element(DrawingNamespace + "majorFont") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:majorFont> element.");
        var minorFontElement = fontSchemeElement.Element(DrawingNamespace + "minorFont") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' has no <a:minorFont> element.");

        return new PptxFontScheme(
            ParseFontCollection(majorFontElement, "majorFont", themePartPath),
            ParseFontCollection(minorFontElement, "minorFont", themePartPath));
    }

    /// <summary>
    ///     Parses a single font collection's (<c>&lt;a:majorFont&gt;</c> or <c>&lt;a:minorFont&gt;</c>)
    ///     <c>&lt;a:latin&gt;</c>/<c>&lt;a:ea&gt;</c>/<c>&lt;a:cs&gt;</c> typeface attributes.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when any of the three required child elements is missing. A missing
    ///     <c>typeface</c> attribute on a present element is tolerated (resolved as
    ///     <see cref="string.Empty"/>), matching real-world themes that declare an empty East
    ///     Asian/complex-script typeface to mean "use the Latin typeface instead".
    /// </exception>
    private static PptxFontCollection ParseFontCollection(XElement fontCollectionElement, string collectionName, string themePartPath)
    {
        var latin = fontCollectionElement.Element(DrawingNamespace + "latin") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' <a:{collectionName}> has no <a:latin> element.");
        var ea = fontCollectionElement.Element(DrawingNamespace + "ea") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' <a:{collectionName}> has no <a:ea> element.");
        var cs = fontCollectionElement.Element(DrawingNamespace + "cs") ??
            throw new InvalidDataException($"Theme part '{themePartPath}' <a:{collectionName}> has no <a:cs> element.");

        return new PptxFontCollection(
            (string?)latin.Attribute("typeface") ?? string.Empty,
            (string?)ea.Attribute("typeface") ?? string.Empty,
            (string?)cs.Attribute("typeface") ?? string.Empty);
    }
}
