using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

// cspell:ignore THEMEVAL

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba srgb hlink folhlink sysclr lastClr

/// <summary>
///     A document's parsed theme (<c>visio/theme/theme1.xml</c>): its color scheme
///     (<c>&lt;a:clrScheme&gt;</c>), resolved to 12 named, independently-nullable slots. This is
///     structurally identical to the DrawingML <c>&lt;a:clrScheme&gt;</c> format already parsed by
///     <c>DemaConsulting.CanvasNet.Pptx.PptxTheme</c>/<c>PptxDocument.Theme.cs</c> - see
///     <see cref="Parse"/>'s own remarks for the one deliberate divergence (never throwing).
/// </summary>
/// <param name="Dk1">The <c>dk1</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Lt1">The <c>lt1</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Dk2">The <c>dk2</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Lt2">The <c>lt2</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent1">The <c>accent1</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent2">The <c>accent2</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent3">The <c>accent3</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent4">The <c>accent4</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent5">The <c>accent5</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Accent6">The <c>accent6</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="Hlink">The <c>hlink</c> slot, or <see langword="null"/> when absent/unparseable.</param>
/// <param name="FolHlink">The <c>folHlink</c> slot, or <see langword="null"/> when absent/unparseable.</param>
internal sealed record VsdxTheme(
    Rgba32? Dk1,
    Rgba32? Lt1,
    Rgba32? Dk2,
    Rgba32? Lt2,
    Rgba32? Accent1,
    Rgba32? Accent2,
    Rgba32? Accent3,
    Rgba32? Accent4,
    Rgba32? Accent5,
    Rgba32? Accent6,
    Rgba32? Hlink,
    Rgba32? FolHlink)
{
    /// <summary>The XML namespace used by DrawingML elements (<c>&lt;a:...&gt;</c>), including theme contents - identical to the OOXML namespace already used by <c>PptxDocument.Theme.cs</c>.</summary>
    private static readonly XNamespace DrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>
    ///     Parses a <c>&lt;a:theme&gt;</c> root element's <c>&lt;a:themeElements&gt;/
    ///     &lt;a:clrScheme&gt;</c> into a new <see cref="VsdxTheme"/>.
    /// </summary>
    /// <remarks>
    ///     Deliberately non-throwing, unlike <c>PptxTheme</c>'s own <c>GetTheme</c> (which throws
    ///     <see cref="InvalidDataException"/> for a malformed theme): <c>canvas-net-vsdx.md</c>'s
    ///     Risk Control Measures mandate a Vsdx color-resolution construct degrade gracefully
    ///     rather than fail the whole parse. A missing <c>&lt;a:themeElements&gt;</c>/
    ///     <c>&lt;a:clrScheme&gt;</c> element yields a <see cref="VsdxTheme"/> with every slot
    ///     <see langword="null"/> (not a caller-visible exception); an individual slot that is
    ///     itself missing, has no recognized color-definition child, or has an unparseable hex
    ///     value likewise degrades to <see langword="null"/> for that slot only, rather than
    ///     failing the whole theme.
    /// </remarks>
    /// <param name="themeRoot">The theme part's root <c>&lt;a:theme&gt;</c> element.</param>
    /// <returns>The parsed <see cref="VsdxTheme"/>, with every absent/unparseable slot <see langword="null"/>.</returns>
    public static VsdxTheme Parse(XElement themeRoot)
    {
        var clrSchemeElement = themeRoot
            .Element(DrawingNamespace + "themeElements")?
            .Element(DrawingNamespace + "clrScheme");

        if (clrSchemeElement is null)
        {
            return new VsdxTheme(null, null, null, null, null, null, null, null, null, null, null, null);
        }

        return new VsdxTheme(
            ParseSchemeColor(clrSchemeElement, "dk1"),
            ParseSchemeColor(clrSchemeElement, "lt1"),
            ParseSchemeColor(clrSchemeElement, "dk2"),
            ParseSchemeColor(clrSchemeElement, "lt2"),
            ParseSchemeColor(clrSchemeElement, "accent1"),
            ParseSchemeColor(clrSchemeElement, "accent2"),
            ParseSchemeColor(clrSchemeElement, "accent3"),
            ParseSchemeColor(clrSchemeElement, "accent4"),
            ParseSchemeColor(clrSchemeElement, "accent5"),
            ParseSchemeColor(clrSchemeElement, "accent6"),
            ParseSchemeColor(clrSchemeElement, "hlink"),
            ParseSchemeColor(clrSchemeElement, "folHlink"));
    }

    /// <summary>
    ///     Looks up the color-scheme slot named <paramref name="slotName"/> (one of the 12
    ///     canonical clrScheme slot names - see this type's own parameter list), used by the
    ///     narrow <c>THEMEVAL("slotName")</c> text-match in <c>VsdxColorPalette.Resolve</c>.
    /// </summary>
    /// <param name="slotName">The clrScheme slot name to look up, matched case-sensitively.</param>
    /// <param name="color">The slot's resolved color, when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="slotName"/> is a recognized slot name whose color is not <see langword="null"/>.</returns>
    public bool TryGetSlot(string slotName, out Rgba32 color)
    {
        var slot = slotName switch
        {
            "dk1" => Dk1,
            "lt1" => Lt1,
            "dk2" => Dk2,
            "lt2" => Lt2,
            "accent1" => Accent1,
            "accent2" => Accent2,
            "accent3" => Accent3,
            "accent4" => Accent4,
            "accent5" => Accent5,
            "accent6" => Accent6,
            "hlink" => Hlink,
            "folHlink" => FolHlink,
            _ => (Rgba32?)null
        };

        if (slot is { } resolved)
        {
            color = resolved;
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>
    ///     Parses a single named color-scheme slot (for example <c>&lt;a:dk1&gt;</c>), resolving
    ///     its single color-definition child element - either <c>&lt;a:srgbClr val="RRGGBB"/&gt;</c>
    ///     or <c>&lt;a:sysClr val="..." lastClr="RRGGBB"/&gt;</c> - mirroring
    ///     <c>PptxTheme</c>'s own <c>ParseSchemeColor</c>, but degrading to <see langword="null"/>
    ///     for any of that method's throwing conditions rather than throwing - see
    ///     <see cref="Parse"/>'s own remarks.
    /// </summary>
    /// <param name="clrSchemeElement">The theme's <c>&lt;a:clrScheme&gt;</c> element.</param>
    /// <param name="slotName">The named slot's local element name (for example <c>"dk1"</c>).</param>
    /// <returns>The slot's resolved <see cref="Rgba32"/> color, or <see langword="null"/> when the slot is absent/unparseable.</returns>
    private static Rgba32? ParseSchemeColor(XElement clrSchemeElement, string slotName)
    {
        var slotElement = clrSchemeElement.Element(DrawingNamespace + slotName);
        var colorElement = slotElement?.Elements().FirstOrDefault();
        if (colorElement is null)
        {
            return null;
        }

        string? hex;
        if (colorElement.Name == DrawingNamespace + "srgbClr")
        {
            hex = (string?)colorElement.Attribute("val");
        }
        else if (colorElement.Name == DrawingNamespace + "sysClr")
        {
            hex = (string?)colorElement.Attribute("lastClr");
        }
        else
        {
            return null;
        }

        if (string.IsNullOrEmpty(hex) || hex.Length != 6)
        {
            return null;
        }

        return Rgba32.TryParse("#" + hex, out var color) ? color : null;
    }
}
