using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm srgbclr schemeclr lummod lumoff pptx gsLst prstDash lgDash sysDash sysDot dashDot patt hlink scrgb prst positionable

/// <summary>
///     Implements the <see cref="PptxDocument"/> DrawingML paint resolvers (Phase 1c): fill
///     (<c>&lt;a:noFill&gt;</c>/<c>&lt;a:solidFill&gt;</c>/<c>&lt;a:gradFill&gt;</c>), color
///     (<c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c> plus the
///     <c>lumMod</c>/<c>lumOff</c>/<c>shade</c>/<c>tint</c>/<c>alpha</c> color-transform chain),
///     and line/stroke resolution (<c>&lt;a:ln&gt;</c>) - see <c>pptx-document.md</c>'s "Geometry
///     and Paint (Phase 1c)" design section for the full color-transform math and the
///     supported/deferred fill-kind boundary.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves a shape's fill: the single recognized fill-definition child
    ///     (<c>&lt;a:noFill&gt;</c>/<c>&lt;a:solidFill&gt;</c>/<c>&lt;a:gradFill&gt;</c>) of
    ///     <paramref name="fillParentElement"/>, into a concrete <see cref="PptxPaint"/>.
    /// </summary>
    /// <param name="fillParentElement">
    ///     The element that may directly contain a fill-definition child - typically a shape's
    ///     <c>&lt;p:spPr&gt;</c>, or an <c>&lt;a:ln&gt;</c> line-properties element (both declare
    ///     their fill the same way).
    /// </param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU (needed to position a gradient fill).</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU (needed to position a gradient fill).</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c>
    ///     ("placeholder color") token, or <see langword="null"/> (the default) to preserve the
    ///     existing <see cref="PptxColorScheme.Dark1"/> fallback - see
    ///     <see cref="ResolveColor"/>'s remarks. Supplied by
    ///     <see cref="ResolveSlideBackgroundFill"/> when resolving a <c>&lt;p:bgRef&gt;</c>'s own
    ///     theme format-scheme fill-style entry, which is always <c>phClr</c>-templated; every
    ///     other, pre-existing call site omits this parameter, leaving its behavior unchanged.
    /// </param>
    /// <returns>
    ///     The resolved <see cref="PptxPaint"/> - <see cref="PptxNoFill.Instance"/> when
    ///     <paramref name="fillParentElement"/> is <see langword="null"/>, declares an explicit
    ///     <c>&lt;a:noFill/&gt;</c>, or declares no recognized fill-definition child at all (a
    ///     deliberate simplification: this phase does not resolve fill inheritance from a
    ///     placeholder/layout/master/theme format scheme - see the design document).
    /// </returns>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when the recognized fill-definition child is <c>&lt;a:pattFill&gt;</c> (pattern
    ///     fill) or <c>&lt;a:blipFill&gt;</c> (picture fill) - both deferred to a later phase (see
    ///     the design document's rationale).
    /// </exception>
    internal static PptxPaint ResolveFill(
        XElement? fillParentElement, PptxTheme theme, float widthEmu, float heightEmu, Rgba32? phClrOverride = null)
    {
        if (fillParentElement is null)
        {
            return PptxNoFill.Instance;
        }

        if (fillParentElement.Element(DrawingNamespace + "noFill") is not null)
        {
            return PptxNoFill.Instance;
        }

        var solidFill = fillParentElement.Element(DrawingNamespace + "solidFill");
        if (solidFill is not null)
        {
            var colorElement = solidFill.Elements().FirstOrDefault() ??
                throw new InvalidDataException("An <a:solidFill> element has no color-definition child.");
            return new PptxSolidFill(ResolveColor(colorElement, theme, phClrOverride));
        }

        var gradFill = fillParentElement.Element(DrawingNamespace + "gradFill");
        if (gradFill is not null)
        {
            return ResolveGradientFill(gradFill, theme, widthEmu, heightEmu, phClrOverride);
        }

        if (fillParentElement.Element(DrawingNamespace + "pattFill") is not null)
        {
            throw new PptxUnsupportedFeatureException("pptx-pattern-fill", "Pattern fill (<a:pattFill>) is not supported.");
        }

        if (fillParentElement.Element(DrawingNamespace + "blipFill") is not null)
        {
            throw new PptxUnsupportedFeatureException("pptx-picture-fill", "Picture fill (<a:blipFill>) is not supported.");
        }

        return PptxNoFill.Instance;
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:gradFill&gt;</c> element's <c>&lt;a:gsLst&gt;</c> gradient stops
    ///     and <c>&lt;a:lin&gt;</c> linear direction into a <see cref="PptxGradientFill"/>.
    /// </summary>
    /// <param name="gradFillElement">The <c>&lt;a:gradFill&gt;</c> element.</param>
    /// <param name="theme">The resolved theme, used to resolve any stop's <c>&lt;a:schemeClr&gt;</c>.</param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU.</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU.</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for each stop's <c>&lt;a:schemeClr val="phClr"/&gt;</c>
    ///     token, or <see langword="null"/> (the default) - see <see cref="ResolveFill"/>'s
    ///     matching parameter.
    /// </param>
    /// <returns>The resolved <see cref="PptxGradientFill"/>, positioned in the shape's own local geometry coordinate space.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="gradFillElement"/> has no <c>&lt;a:gsLst&gt;</c>, or
    ///     <c>&lt;a:gsLst&gt;</c> has no <c>&lt;a:gs&gt;</c> children, or a <c>&lt;a:gs&gt;</c> has
    ///     a missing or non-numeric <c>pos</c> attribute or no color-definition child.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <paramref name="gradFillElement"/> declares a path gradient
    ///     (<c>&lt;a:path&gt;</c>) rather than a linear one (<c>&lt;a:lin&gt;</c>), or declares
    ///     neither - both radial/path gradients and the (rare) gradient with no direction element
    ///     at all are deferred to a later phase (feature token <c>"pptx-gradient-path"</c>).
    /// </exception>
    internal static PptxGradientFill ResolveGradientFill(
        XElement gradFillElement, PptxTheme theme, float widthEmu, float heightEmu, Rgba32? phClrOverride = null)
    {
        var gsLst = gradFillElement.Element(DrawingNamespace + "gsLst") ??
            throw new InvalidDataException("An <a:gradFill> element has no <a:gsLst> element.");

        var gsElements = gsLst.Elements(DrawingNamespace + "gs").ToList();
        if (gsElements.Count == 0)
        {
            throw new InvalidDataException("An <a:gradFill>'s <a:gsLst> element has no <a:gs> children.");
        }

        var stops = new List<GradientStop>(gsElements.Count);
        foreach (var gs in gsElements)
        {
            var posValue = (string?)gs.Attribute("pos") ??
                throw new InvalidDataException("An <a:gs> element has no 'pos' attribute.");
            if (!float.TryParse(posValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var pos))
            {
                throw new InvalidDataException($"An <a:gs> element has a non-numeric 'pos' attribute value '{posValue}'.");
            }

            var colorElement = gs.Elements().FirstOrDefault() ??
                throw new InvalidDataException("An <a:gs> element has no color-definition child.");
            var offset = Math.Clamp(pos / 100000f, 0f, 1f);
            stops.Add(new GradientStop(offset, ResolveColor(colorElement, theme, phClrOverride)));
        }

        if (gradFillElement.Element(DrawingNamespace + "lin") is not { } lin)
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-gradient-path",
                "Only a linear gradient (<a:lin>) is supported; radial/path gradients (<a:path>) are not.");
        }

        var ang60000ths = (int?)lin.Attribute("ang") ?? 0;
        var angleRadians = ang60000ths / 60000f * (MathF.PI / 180f);
        var direction = new Vector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians));
        var extent = (MathF.Abs(direction.X) * widthEmu + MathF.Abs(direction.Y) * heightEmu) / 2f;
        var center = new Vector2(widthEmu / 2f, heightEmu / 2f);

        var gradient = new LinearGradient(center - direction * extent, center + direction * extent, stops);
        return new PptxGradientFill(gradient);
    }

    /// <summary>
    ///     Resolves a single color-definition element
    ///     (<c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c>) and its
    ///     child color-transform chain into a concrete <see cref="Rgba32"/> value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <c>&lt;a:schemeClr val="..."/&gt;</c>'s 12 ordinary slot names (<c>dk1</c>/<c>lt1</c>/
    ///     <c>dk2</c>/<c>lt2</c>/<c>accent1-6</c>/<c>hlink</c>/<c>folHlink</c>) map directly to
    ///     <see cref="PptxColorScheme"/>'s like-named property; the four special aliases
    ///     <c>bg1</c>/<c>tx1</c>/<c>bg2</c>/<c>tx2</c> map to <c>lt1</c>/<c>dk1</c>/<c>lt2</c>/
    ///     <c>dk2</c> respectively (per ECMA-376's documented background/text-color aliasing),
    ///     and <c>phClr</c> ("placeholder color" - a shape-style-reference token meaning "whatever
    ///     color this shape's style already resolved to") resolves to <paramref name="phClrOverride"/>
    ///     when supplied (the one caller that threads shape-style-reference context through -
    ///     <see cref="ResolveSlideBackgroundFill"/>'s <c>&lt;p:bgRef&gt;</c> resolution); every
    ///     other call site - which does not have that surrounding context available - omits
    ///     <paramref name="phClrOverride"/>, so <c>phClr</c> resolves to
    ///     <see cref="PptxColorScheme.Dark1"/> as a deterministic, documented fallback.
    ///     </para>
    ///     <para>
    ///     The color-transform chain is applied in a fixed pipeline order regardless of the
    ///     transform elements' own order in the XML: <c>lumMod</c>/<c>lumOff</c> together (in HSL
    ///     luminance space: <c>L' = L * lumMod + lumOff</c>, each as a <c>val/100000</c>
    ///     fraction), then <c>shade</c> (RGB-space: <c>c' = c * val/100000</c>), then <c>tint</c>
    ///     (RGB-space: <c>c' = c * val/100000 + 255 * (1 - val/100000)</c>), then <c>alpha</c>
    ///     (<c>a' = val/100000 * 255</c>, replacing the alpha channel outright). This is a
    ///     deliberate, documented simplification of OOXML's true colorimetric transform model -
    ///     see the design document.
    ///     </para>
    /// </remarks>
    /// <param name="colorElement">The color-definition element.</param>
    /// <param name="theme">The resolved theme, used to resolve an <c>&lt;a:schemeClr&gt;</c>'s named slot.</param>
    /// <param name="phClrOverride">
    ///     The concrete color to substitute for an <c>&lt;a:schemeClr val="phClr"/&gt;</c> token,
    ///     or <see langword="null"/> (the default) to preserve the existing
    ///     <see cref="PptxColorScheme.Dark1"/> fallback documented below. Every pre-existing call
    ///     site omits this parameter, leaving its behavior unchanged; only
    ///     <see cref="ResolveSlideBackgroundFill"/>'s <c>&lt;p:bgRef&gt;</c> resolution supplies
    ///     it.
    /// </param>
    /// <returns>The resolved, fully color-transformed <see cref="Rgba32"/> value.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="colorElement"/> is an <c>&lt;a:srgbClr&gt;</c>/
    ///     <c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c> missing its required <c>val</c>
    ///     attribute (or, for <c>&lt;a:sysClr&gt;</c>, missing <c>lastClr</c>), has an invalid hex
    ///     value, or names an unrecognized <c>&lt;a:schemeClr val="..."/&gt;</c> slot.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <paramref name="colorElement"/> is a color-definition kind other than
    ///     <c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c> (for example
    ///     <c>&lt;a:scrgbClr&gt;</c>/<c>&lt;a:hslClr&gt;</c>/<c>&lt;a:prstClr&gt;</c>) - deferred
    ///     to a later phase (feature token <c>"pptx-color-kind"</c>).
    /// </exception>
    internal static Rgba32 ResolveColor(XElement colorElement, PptxTheme theme, Rgba32? phClrOverride = null)
    {
        var baseColor = ResolveBaseColor(colorElement, theme, phClrOverride);
        return ApplyColorTransforms(baseColor, colorElement);
    }

    /// <summary>Resolves a color-definition element's own base color, before any color-transform chain is applied.</summary>
    private static Rgba32 ResolveBaseColor(XElement colorElement, PptxTheme theme, Rgba32? phClrOverride = null)
    {
        if (colorElement.Name == DrawingNamespace + "srgbClr")
        {
            var hex = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException("An <a:srgbClr> element has no 'val' attribute.");
            return ParseHexColor(hex, "srgbClr");
        }

        if (colorElement.Name == DrawingNamespace + "sysClr")
        {
            var hex = (string?)colorElement.Attribute("lastClr") ??
                throw new InvalidDataException("An <a:sysClr> element has no 'lastClr' attribute.");
            return ParseHexColor(hex, "sysClr");
        }

        if (colorElement.Name == DrawingNamespace + "schemeClr")
        {
            var val = (string?)colorElement.Attribute("val") ??
                throw new InvalidDataException("An <a:schemeClr> element has no 'val' attribute.");
            if (val == "phClr" && phClrOverride is { } overrideColor)
            {
                return overrideColor;
            }

            return ResolveSchemeColor(val, theme.ColorScheme);
        }

        throw new PptxUnsupportedFeatureException(
            "pptx-color-kind",
            $"Color-definition element '{colorElement.Name.LocalName}' is not supported.");
    }

    /// <summary>
    ///     Parses a required <c>#RRGGBB</c>/<c>RRGGBB</c> hex color value, rejecting any value
    ///     that is not exactly six hexadecimal digits.
    /// </summary>
    /// <remarks>
    ///     OOXML's <c>srgbClr/@val</c> and <c>sysClr/@lastClr</c> are always exactly six hex
    ///     digits (<c>RRGGBB</c>); an eight-digit <c>AARRGGBB</c> value, which
    ///     <see cref="Rgba32.Parse(string)"/> would otherwise also accept, misinterpreting its
    ///     first byte as alpha, must be rejected rather than silently misread - mirroring
    ///     <c>PptxDocument.Theme.cs</c>'s own <c>ParseSchemeColor</c> six-digit validation for the
    ///     same class of value.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="hex"/> (after stripping any leading <c>'#'</c>) is not
    ///     exactly six hexadecimal digits, or is not valid hexadecimal at all.
    /// </exception>
    private static Rgba32 ParseHexColor(string hex, string elementName)
    {
        var digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (digits.Length != 6)
        {
            throw new InvalidDataException(
                $"An <a:{elementName}> element has a color value '{hex}' that is not exactly six hexadecimal digits (OOXML's RRGGBB form).");
        }

        try
        {
            return Rgba32.Parse("#" + digits);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"An <a:{elementName}> element has an invalid hexadecimal color value '{hex}'.", ex);
        }
    }

    /// <summary>Maps an <c>&lt;a:schemeClr val="..."/&gt;</c> slot name to its resolved <see cref="PptxColorScheme"/> color.</summary>
    private static Rgba32 ResolveSchemeColor(string val, PptxColorScheme scheme) =>
        val switch
        {
            "dk1" => scheme.Dark1,
            "lt1" => scheme.Light1,
            "dk2" => scheme.Dark2,
            "lt2" => scheme.Light2,
            "accent1" => scheme.Accent1,
            "accent2" => scheme.Accent2,
            "accent3" => scheme.Accent3,
            "accent4" => scheme.Accent4,
            "accent5" => scheme.Accent5,
            "accent6" => scheme.Accent6,
            "hlink" => scheme.Hyperlink,
            "folHlink" => scheme.FollowedHyperlink,
            // Background/text aliases - see ResolveColor's remarks.
            "bg1" => scheme.Light1,
            "tx1" => scheme.Dark1,
            "bg2" => scheme.Light2,
            "tx2" => scheme.Dark2,
            // "Placeholder color" - unresolvable without shape-style-reference context; see ResolveColor's remarks.
            "phClr" => scheme.Dark1,
            _ => throw new InvalidDataException($"An <a:schemeClr> element names an unrecognized slot '{val}'."),
        };

    /// <summary>Applies a color-definition element's lumMod/lumOff/shade/tint/alpha transform chain, in that fixed order.</summary>
    private static Rgba32 ApplyColorTransforms(Rgba32 color, XElement colorElement)
    {
        var lumMod = ReadPercentageChild(colorElement, "lumMod");
        var lumOff = ReadPercentageChild(colorElement, "lumOff");
        if (lumMod is not null || lumOff is not null)
        {
            color = ApplyLuminance(color, lumMod ?? 1f, lumOff ?? 0f);
        }

        if (ReadPercentageChild(colorElement, "shade") is { } shade)
        {
            color = new Rgba32(
                (byte)Math.Clamp(color.R * shade, 0f, 255f),
                (byte)Math.Clamp(color.G * shade, 0f, 255f),
                (byte)Math.Clamp(color.B * shade, 0f, 255f),
                color.A);
        }

        if (ReadPercentageChild(colorElement, "tint") is { } tint)
        {
            color = new Rgba32(
                (byte)Math.Clamp(color.R * tint + 255f * (1f - tint), 0f, 255f),
                (byte)Math.Clamp(color.G * tint + 255f * (1f - tint), 0f, 255f),
                (byte)Math.Clamp(color.B * tint + 255f * (1f - tint), 0f, 255f),
                color.A);
        }

        if (ReadPercentageChild(colorElement, "alpha") is { } alpha)
        {
            color = new Rgba32(color.R, color.G, color.B, (byte)Math.Clamp(alpha * 255f, 0f, 255f));
        }

        return color;
    }

    /// <summary>
    ///     Reads a color-transform child element's own <c>val</c> attribute (OOXML's "thousandths
    ///     of a percent", 100000 = 100%) as a <c>[0,1]</c>-scaled (or, for <c>lumOff</c>, signed)
    ///     fraction, or <see langword="null"/> when the child is absent.
    /// </summary>
    private static float? ReadPercentageChild(XElement colorElement, string childName)
    {
        var child = colorElement.Element(DrawingNamespace + childName);
        if (child is null)
        {
            return null;
        }

        var val = (string?)child.Attribute("val") ??
            throw new InvalidDataException($"An <a:{childName}> element has no 'val' attribute.");
        if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val100000ths))
        {
            throw new InvalidDataException($"An <a:{childName}> element has a non-numeric 'val' attribute value '{val}'.");
        }

        return val100000ths / 100000f;
    }

    /// <summary>Applies <c>L' = L * lumMod + lumOff</c> in HSL space, preserving hue/saturation/alpha.</summary>
    private static Rgba32 ApplyLuminance(Rgba32 color, float lumMod, float lumOff)
    {
        var (h, s, l) = RgbToHsl(color);
        l = Math.Clamp(l * lumMod + lumOff, 0f, 1f);
        return HslToRgb(h, s, l, color.A);
    }

    /// <summary>Converts an <see cref="Rgba32"/> color's RGB channels to HSL (hue in turns [0,1), saturation/lightness in [0,1]).</summary>
    private static (float H, float S, float L) RgbToHsl(Rgba32 color)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;

        // Identify the dominant (max) channel by comparing the original byte values rather than
        // their derived float equivalents, so no floating-point equality comparison is needed.
        var maxByte = Math.Max(color.R, Math.Max(color.G, color.B));
        var minByte = Math.Min(color.R, Math.Min(color.G, color.B));
        var max = maxByte / 255f;
        var min = minByte / 255f;
        var l = (max + min) / 2f;

        if (maxByte == minByte)
        {
            return (0f, 0f, l);
        }

        var delta = max - min;
        var s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

        float h;
        if (maxByte == color.R)
        {
            h = (g - b) / delta + (g < b ? 6f : 0f);
        }
        else if (maxByte == color.G)
        {
            h = (b - r) / delta + 2f;
        }
        else
        {
            h = (r - g) / delta + 4f;
        }

        h /= 6f;
        return (h, s, l);
    }

    /// <summary>Converts an HSL color (hue in turns [0,1), saturation/lightness in [0,1]) back to RGB, with the given alpha.</summary>
    private static Rgba32 HslToRgb(float h, float s, float l, byte alpha)
    {
        if (s <= 0f)
        {
            var gray = (byte)Math.Clamp(l * 255f, 0f, 255f);
            return new Rgba32(gray, gray, gray, alpha);
        }

        var q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        var p = 2f * l - q;

        var r = HueToChannel(p, q, h + 1f / 3f);
        var g = HueToChannel(p, q, h);
        var b = HueToChannel(p, q, h - 1f / 3f);

        return new Rgba32(
            (byte)Math.Clamp(r * 255f, 0f, 255f),
            (byte)Math.Clamp(g * 255f, 0f, 255f),
            (byte)Math.Clamp(b * 255f, 0f, 255f),
            alpha);
    }

    /// <summary>Standard HSL-to-RGB single-channel helper (see the CSS Color Module's reference algorithm).</summary>
    private static float HueToChannel(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + (q - p) * 6f * t;
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + (q - p) * (2f / 3f - t) * 6f;
        }

        return p;
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:ln&gt;</c> line-properties element into a <see cref="PptxLineStyle"/>.
    /// </summary>
    /// <param name="lnElement">The <c>&lt;a:ln&gt;</c> element, or <see langword="null"/> for "no line properties declared".</param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c> in the line's fill.</param>
    /// <returns>
    ///     The resolved <see cref="PptxLineStyle"/>, or <see langword="null"/> meaning "no
    ///     stroke": when <paramref name="lnElement"/> is <see langword="null"/>, declares no
    ///     <c>w</c> attribute or a non-positive one (a deliberate simplification - this phase does
    ///     not resolve a missing width from the shape's format-scheme-inherited default line
    ///     style; see the design document), or its resolved fill is <see cref="PptxNoFill"/>
    ///     (including an explicit <c>&lt;a:noFill/&gt;</c>, or no recognized fill-definition child
    ///     at all).
    /// </returns>
    internal static PptxLineStyle? ResolveLineStyle(XElement? lnElement, PptxTheme theme)
    {
        if (lnElement is null)
        {
            return null;
        }

        var widthEmu = (float?)lnElement.Attribute("w") ?? 0f;
        if (widthEmu <= 0f)
        {
            return null;
        }

        // A line's fill carries no shape width/height of its own to position a gradient against -
        // gradient-filled lines are not meaningfully positionable this phase, so 1x1 is used as a
        // neutral placeholder extent (only reachable if a document declares <a:ln><a:gradFill>).
        var paint = ResolveFill(lnElement, theme, 1f, 1f);
        if (paint is PptxNoFill)
        {
            return null;
        }

        var dashArray = ResolveDashArray(lnElement, widthEmu);
        return new PptxLineStyle(widthEmu, paint, dashArray);
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:ln&gt;</c>'s <c>&lt;a:prstDash val="..."/&gt;</c> preset dash
    ///     pattern name into a dash-length array scaled to <paramref name="widthEmu"/> (matching
    ///     OOXML's own width-relative dash/gap proportions), or <see langword="null"/> for a
    ///     solid line.
    /// </summary>
    /// <remarks>
    ///     Supports the six most common preset names (<c>dash</c>, <c>dashDot</c>, <c>dot</c>,
    ///     <c>lgDash</c>, <c>lgDashDot</c>, <c>sysDash</c>, <c>sysDot</c>); <c>solid</c>, an
    ///     absent <c>&lt;a:prstDash&gt;</c>, and any other (rarer) preset name all resolve to
    ///     <see langword="null"/> (solid line) - a documented simplification, since an unsupported
    ///     dash name degrading to a solid line is a purely cosmetic difference that should not
    ///     block resolving an otherwise well-formed line style.
    /// </remarks>
    private static IReadOnlyList<float>? ResolveDashArray(XElement lnElement, float widthEmu)
    {
        var prstDash = lnElement.Element(DrawingNamespace + "prstDash");
        var val = (string?)prstDash?.Attribute("val");
        return val switch
        {
            "dash" => [widthEmu * 4f, widthEmu * 3f],
            "dashDot" => [widthEmu * 4f, widthEmu * 3f, widthEmu, widthEmu * 3f],
            "dot" => [widthEmu, widthEmu * 3f],
            "lgDash" => [widthEmu * 8f, widthEmu * 3f],
            "lgDashDot" => [widthEmu * 8f, widthEmu * 3f, widthEmu, widthEmu * 3f],
            "sysDash" => [widthEmu * 3f, widthEmu * 2f],
            "sysDot" => [widthEmu, widthEmu * 2f],
            _ => null,
        };
    }

    /// <summary>
    ///     Converts a shape's local-space outline <paramref name="shapePath"/> into its stroked,
    ///     fillable outline per <paramref name="lineStyle"/>, mirroring the exact
    ///     <see cref="StrokeStyle"/>-construction/<see cref="PathStroker.Stroke"/> call pattern
    ///     the PDF renderer uses (<c>PdfDocument.PaintStroke</c>).
    /// </summary>
    /// <param name="shapePath">The shape's own local-space geometry (see <see cref="ResolveShapeGeometry"/>).</param>
    /// <param name="lineStyle">The resolved line style to stroke with.</param>
    /// <returns>
    ///     The stroked outline <see cref="Path"/>, in the same local coordinate space as
    ///     <paramref name="shapePath"/> - a later rendering phase fills it (via
    ///     <see cref="Drawing.PathFiller.Fill(Canvas.Surface, Path, Rgba32, FillRule, float)"/>,
    ///     <see cref="FillRule.NonZero"/>) with <paramref name="lineStyle"/>'s own resolved paint;
    ///     this phase has no rendering surface yet, so only the outline geometry is produced here.
    /// </returns>
    internal static Path ResolveStrokeOutline(Path shapePath, PptxLineStyle lineStyle)
    {
        var style = new StrokeStyle(lineStyle.WidthEmu, dashArray: lineStyle.DashArray);
        return PathStroker.Stroke(shapePath, style);
    }
}
