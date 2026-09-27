// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
// cspell:ignore aliceblue antiquewhite blanchedalmond blueviolet burlywood cadetblue cornflowerblue
// cspell:ignore cornsilk darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta
// cspell:ignore darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue
// cspell:ignore darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray
// cspell:ignore dimgrey dodgerblue floralwhite forestgreen gainsboro ghostwhite greenyellow hotpink
// cspell:ignore indianred lavenderblush lawngreen lemonchiffon lightcoral lightcyan
// cspell:ignore lightgoldenrodyellow lightgray lightgreen lightpink lightsalmon lightseagreen
// cspell:ignore lightskyblue lightslategray lightslategrey lightsteelblue lightyellow limegreen
// cspell:ignore mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue
// cspell:ignore mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose
// cspell:ignore navajowhite oldlace olivedrab orangered palegoldenrod palegreen paleturquoise
// cspell:ignore palevioletred papayawhip peachpuff powderblue rebeccapurple rosybrown royalblue
// cspell:ignore saddlebrown sandybrown seagreen skyblue slateblue slategray slategrey springgreen
// cspell:ignore steelblue whitesmoke yellowgreen
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    // ================================================================================================
    // Paint and color resolution
    // ================================================================================================

    /// <summary>
    ///     Resolves a raw <c>fill</c>/<c>stroke</c> presentation-attribute value into a concrete
    ///     paint: a solid <see cref="Rgba32"/> color, a <see cref="Gradient"/>, or
    ///     <see langword="null"/> for "paint nothing".
    /// </summary>
    /// <param name="spec">The raw paint specification (<c>none</c>/color/<c>url(#id)</c>).</param>
    /// <param name="alphaMultiplier">
    ///     The combined opacity multiplier (the relevant <c>fill-opacity</c>/<c>stroke-opacity</c>
    ///     times the cascaded <c>opacity</c> product) folded into a resolved solid color's alpha.
    /// </param>
    /// <param name="localPath">The shape's local-space outline, used as a gradient's object-bounding-box basis.</param>
    /// <param name="transform">The accumulated transform, composed into a resolved gradient's own transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     A boxed <see cref="Rgba32"/>, a <see cref="Gradient"/>, or <see langword="null"/> - see
    ///     this class's error-handling policy remarks for why an unrecognized color keyword or a
    ///     dangling <c>url(#id)</c> reference tolerantly resolve to <see langword="null"/> rather
    ///     than throwing.
    /// </returns>
    private static object? ResolvePaint(string spec, float alphaMultiplier, Path localPath, Matrix3x2 transform, RenderContext context)
    {
        var trimmed = spec.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            var id = ExtractUrlId(trimmed);
            if (id == null || !context.IdIndex.TryGetValue(id, out var element))
            {
                return null;
            }

            return BuildGradient(element, alphaMultiplier, localPath, transform, context);
        }

        var color = ParseColor(trimmed);
        return color == null ? null : ApplyAlpha(color.Value, alphaMultiplier);
    }

    /// <summary>Extracts the fragment id referenced by a <c>url(#id)</c> paint specification.</summary>
    /// <param name="spec">The raw, already <c>url(</c>-prefixed specification.</param>
    /// <returns>The referenced id, or <see langword="null"/> if the syntax is not a <c>url(#id)</c> reference.</returns>
    private static string? ExtractUrlId(string spec)
    {
        var openIndex = spec.IndexOf('(');
        var closeIndex = spec.LastIndexOf(')');
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        var inner = spec[(openIndex + 1)..closeIndex].Trim().Trim('\'', '"');
        return inner.StartsWith('#') ? inner[1..] : null;
    }

    /// <summary>Applies an alpha multiplier to a color, rounding and clamping the result to a valid byte.</summary>
    /// <param name="color">The base color.</param>
    /// <param name="multiplier">The multiplier to apply to <paramref name="color"/>'s existing alpha.</param>
    /// <returns>The color with its alpha channel scaled.</returns>
    private static Rgba32 ApplyAlpha(Rgba32 color, float multiplier)
    {
        var alpha = (byte)Math.Clamp(MathF.Round(color.A * multiplier), 0, 255);
        return new Rgba32(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    ///     Parses a CSS/SVG color value in any of this codec's supported forms: a named keyword,
    ///     <c>#rgb</c>/<c>#rrggbb</c> hex, or <c>rgb(...)</c>/<c>rgba(...)</c>.
    /// </summary>
    /// <param name="spec">The trimmed, non-empty, non-<c>"none"</c> color text.</param>
    /// <returns>
    ///     The parsed color, or <see langword="null"/> if <paramref name="spec"/> does not match
    ///     any supported form - tolerated as "no paint" rather than an error, since an
    ///     unrecognized color keyword is a well-formed-but-unsupported value, not corrupt data.
    /// </returns>
    private static Rgba32? ParseColor(string spec)
    {
        if (spec.StartsWith('#'))
        {
            return ParseHexColor(spec);
        }

        if (spec.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
            spec.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgbFunctionColor(spec);
        }

        return NamedColors.TryGetValue(spec, out var color) ? color : null;
    }

    /// <summary>Parses a <c>#rgb</c> or <c>#rrggbb</c> hex color.</summary>
    /// <param name="spec">The full, <c>#</c>-prefixed hex text.</param>
    /// <returns>The parsed color, or <see langword="null"/> if not a recognized hex form.</returns>
    private static Rgba32? ParseHexColor(string spec)
    {
        var hex = spec[1..];
        return hex.Length switch
        {
            3 => ParseShortHexColor(hex),
            6 => ParseLongHexColor(hex),
            _ => null
        };
    }

    /// <summary>Parses a three-digit <c>#rgb</c> hex color, doubling each digit per the CSS shorthand rule.</summary>
    /// <param name="hex">The three hex digits following the <c>#</c>.</param>
    /// <returns>The parsed color, or <see langword="null"/> if any digit is not a valid hex character.</returns>
    private static Rgba32? ParseShortHexColor(string hex)
    {
        if (!TryHexNibble(hex[0], out var r) || !TryHexNibble(hex[1], out var g) || !TryHexNibble(hex[2], out var b))
        {
            return null;
        }

        return new Rgba32((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), 255);
    }

    /// <summary>Parses a six-digit <c>#rrggbb</c> hex color.</summary>
    /// <param name="hex">The six hex digits following the <c>#</c>.</param>
    /// <returns>The parsed color, or <see langword="null"/> if any pair is not a valid hex byte.</returns>
    private static Rgba32? ParseLongHexColor(string hex)
    {
        if (!byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return null;
        }

        return new Rgba32(r, g, b, 255);
    }

    /// <summary>Parses a single hexadecimal digit's value.</summary>
    /// <param name="digit">The character to parse.</param>
    /// <param name="value">The digit's value (0-15), if this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="digit"/> is a valid hex digit.</returns>
    private static bool TryHexNibble(char digit, out int value)
    {
        value = digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => -1
        };

        return value >= 0;
    }

    /// <summary>Parses an <c>rgb(r, g, b)</c> or <c>rgba(r, g, b, a)</c> functional color.</summary>
    /// <param name="spec">The full, already <c>rgb(</c>/<c>rgba(</c>-prefixed text.</param>
    /// <returns>The parsed color, or <see langword="null"/> if the syntax is not recognized.</returns>
    private static Rgba32? ParseRgbFunctionColor(string spec)
    {
        var openIndex = spec.IndexOf('(');
        var closeIndex = spec.LastIndexOf(')');
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        var parts = spec[(openIndex + 1)..closeIndex]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4))
        {
            return null;
        }

        var r = ParseColorChannel(parts[0]);
        var g = ParseColorChannel(parts[1]);
        var b = ParseColorChannel(parts[2]);
        if (r == null || g == null || b == null)
        {
            return null;
        }

        var alpha = 255;
        if (parts.Length == 4)
        {
            var a = ParsePercentOrNumber(parts[3], 1f);
            if (a == null)
            {
                return null;
            }

            alpha = (int)Math.Clamp(MathF.Round(a.Value * 255f), 0, 255);
        }

        return new Rgba32(r.Value, g.Value, b.Value, (byte)alpha);
    }

    /// <summary>Parses one <c>rgb()</c>/<c>rgba()</c> color channel (a bare 0-255 number or a percentage).</summary>
    /// <param name="token">The raw, trimmed channel text.</param>
    /// <returns>The channel's byte value, or <see langword="null"/> if not a recognizable number/percentage.</returns>
    private static byte? ParseColorChannel(string token)
    {
        var value = ParsePercentOrNumber(token, 255f);
        return value == null ? null : (byte)Math.Clamp(MathF.Round(value.Value), 0, 255);
    }

    /// <summary>
    ///     Parses either a bare number or a CSS percentage (resolved against <paramref name="basis"/>).
    /// </summary>
    /// <param name="token">The raw, trimmed text.</param>
    /// <param name="basis">The value a <c>100%</c> percentage resolves to.</param>
    /// <returns>
    ///     The resolved value, or <see langword="null"/> if not a recognizable number/percentage,
    ///     or if it resolves to a non-finite value (<c>NaN</c>/<c>Infinity</c>).
    /// </returns>
    private static float? ParsePercentOrNumber(string token, float basis)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        float value;
        if (trimmed.EndsWith('%'))
        {
            if (!float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                return null;
            }

            value = pct / 100f * basis;
        }
        else if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return null;
        }

        // Reject a non-finite result (NaN/Infinity) the same way as an unparseable token: it is
        // syntactically a valid float but never a meaningful coordinate/channel/offset value -
        // see ParseCoordinate's identical rule for the throwing (non-nullable) counterpart of
        // this method
        return float.IsFinite(value) ? value : null;
    }

    // ================================================================================================
    // Named CSS/SVG color keyword table
    // ================================================================================================

    /// <summary>
    ///     The full CSS Color Module Level 3 "extended color keywords" table (the X11 color names
    ///     supported by mainstream browsers, identical to the original SVG 1.0 color keyword list),
    ///     plus <c>transparent</c>. Looked up case-insensitively, matching the CSS specification's
    ///     "ASCII case-insensitive" keyword-matching rule.
    /// </summary>
    private static readonly Dictionary<string, Rgba32> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = new Rgba32(0xF0, 0xF8, 0xFF, 0xFF),
        ["antiquewhite"] = new Rgba32(0xFA, 0xEB, 0xD7, 0xFF),
        ["aqua"] = new Rgba32(0x00, 0xFF, 0xFF, 0xFF),
        ["aquamarine"] = new Rgba32(0x7F, 0xFF, 0xD4, 0xFF),
        ["azure"] = new Rgba32(0xF0, 0xFF, 0xFF, 0xFF),
        ["beige"] = new Rgba32(0xF5, 0xF5, 0xDC, 0xFF),
        ["bisque"] = new Rgba32(0xFF, 0xE4, 0xC4, 0xFF),
        ["black"] = new Rgba32(0x00, 0x00, 0x00, 0xFF),
        ["blanchedalmond"] = new Rgba32(0xFF, 0xEB, 0xCD, 0xFF),
        ["blue"] = new Rgba32(0x00, 0x00, 0xFF, 0xFF),
        ["blueviolet"] = new Rgba32(0x8A, 0x2B, 0xE2, 0xFF),
        ["brown"] = new Rgba32(0xA5, 0x2A, 0x2A, 0xFF),
        ["burlywood"] = new Rgba32(0xDE, 0xB8, 0x87, 0xFF),
        ["cadetblue"] = new Rgba32(0x5F, 0x9E, 0xA0, 0xFF),
        ["chartreuse"] = new Rgba32(0x7F, 0xFF, 0x00, 0xFF),
        ["chocolate"] = new Rgba32(0xD2, 0x69, 0x1E, 0xFF),
        ["coral"] = new Rgba32(0xFF, 0x7F, 0x50, 0xFF),
        ["cornflowerblue"] = new Rgba32(0x64, 0x95, 0xED, 0xFF),
        ["cornsilk"] = new Rgba32(0xFF, 0xF8, 0xDC, 0xFF),
        ["crimson"] = new Rgba32(0xDC, 0x14, 0x3C, 0xFF),
        ["cyan"] = new Rgba32(0x00, 0xFF, 0xFF, 0xFF),
        ["darkblue"] = new Rgba32(0x00, 0x00, 0x8B, 0xFF),
        ["darkcyan"] = new Rgba32(0x00, 0x8B, 0x8B, 0xFF),
        ["darkgoldenrod"] = new Rgba32(0xB8, 0x86, 0x0B, 0xFF),
        ["darkgray"] = new Rgba32(0xA9, 0xA9, 0xA9, 0xFF),
        ["darkgreen"] = new Rgba32(0x00, 0x64, 0x00, 0xFF),
        ["darkgrey"] = new Rgba32(0xA9, 0xA9, 0xA9, 0xFF),
        ["darkkhaki"] = new Rgba32(0xBD, 0xB7, 0x6B, 0xFF),
        ["darkmagenta"] = new Rgba32(0x8B, 0x00, 0x8B, 0xFF),
        ["darkolivegreen"] = new Rgba32(0x55, 0x6B, 0x2F, 0xFF),
        ["darkorange"] = new Rgba32(0xFF, 0x8C, 0x00, 0xFF),
        ["darkorchid"] = new Rgba32(0x99, 0x32, 0xCC, 0xFF),
        ["darkred"] = new Rgba32(0x8B, 0x00, 0x00, 0xFF),
        ["darksalmon"] = new Rgba32(0xE9, 0x96, 0x7A, 0xFF),
        ["darkseagreen"] = new Rgba32(0x8F, 0xBC, 0x8F, 0xFF),
        ["darkslateblue"] = new Rgba32(0x48, 0x3D, 0x8B, 0xFF),
        ["darkslategray"] = new Rgba32(0x2F, 0x4F, 0x4F, 0xFF),
        ["darkslategrey"] = new Rgba32(0x2F, 0x4F, 0x4F, 0xFF),
        ["darkturquoise"] = new Rgba32(0x00, 0xCE, 0xD1, 0xFF),
        ["darkviolet"] = new Rgba32(0x94, 0x00, 0xD3, 0xFF),
        ["deeppink"] = new Rgba32(0xFF, 0x14, 0x93, 0xFF),
        ["deepskyblue"] = new Rgba32(0x00, 0xBF, 0xFF, 0xFF),
        ["dimgray"] = new Rgba32(0x69, 0x69, 0x69, 0xFF),
        ["dimgrey"] = new Rgba32(0x69, 0x69, 0x69, 0xFF),
        ["dodgerblue"] = new Rgba32(0x1E, 0x90, 0xFF, 0xFF),
        ["firebrick"] = new Rgba32(0xB2, 0x22, 0x22, 0xFF),
        ["floralwhite"] = new Rgba32(0xFF, 0xFA, 0xF0, 0xFF),
        ["forestgreen"] = new Rgba32(0x22, 0x8B, 0x22, 0xFF),
        ["fuchsia"] = new Rgba32(0xFF, 0x00, 0xFF, 0xFF),
        ["gainsboro"] = new Rgba32(0xDC, 0xDC, 0xDC, 0xFF),
        ["ghostwhite"] = new Rgba32(0xF8, 0xF8, 0xFF, 0xFF),
        ["gold"] = new Rgba32(0xFF, 0xD7, 0x00, 0xFF),
        ["goldenrod"] = new Rgba32(0xDA, 0xA5, 0x20, 0xFF),
        ["gray"] = new Rgba32(0x80, 0x80, 0x80, 0xFF),
        ["green"] = new Rgba32(0x00, 0x80, 0x00, 0xFF),
        ["greenyellow"] = new Rgba32(0xAD, 0xFF, 0x2F, 0xFF),
        ["grey"] = new Rgba32(0x80, 0x80, 0x80, 0xFF),
        ["honeydew"] = new Rgba32(0xF0, 0xFF, 0xF0, 0xFF),
        ["hotpink"] = new Rgba32(0xFF, 0x69, 0xB4, 0xFF),
        ["indianred"] = new Rgba32(0xCD, 0x5C, 0x5C, 0xFF),
        ["indigo"] = new Rgba32(0x4B, 0x00, 0x82, 0xFF),
        ["ivory"] = new Rgba32(0xFF, 0xFF, 0xF0, 0xFF),
        ["khaki"] = new Rgba32(0xF0, 0xE6, 0x8C, 0xFF),
        ["lavender"] = new Rgba32(0xE6, 0xE6, 0xFA, 0xFF),
        ["lavenderblush"] = new Rgba32(0xFF, 0xF0, 0xF5, 0xFF),
        ["lawngreen"] = new Rgba32(0x7C, 0xFC, 0x00, 0xFF),
        ["lemonchiffon"] = new Rgba32(0xFF, 0xFA, 0xCD, 0xFF),
        ["lightblue"] = new Rgba32(0xAD, 0xD8, 0xE6, 0xFF),
        ["lightcoral"] = new Rgba32(0xF0, 0x80, 0x80, 0xFF),
        ["lightcyan"] = new Rgba32(0xE0, 0xFF, 0xFF, 0xFF),
        ["lightgoldenrodyellow"] = new Rgba32(0xFA, 0xFA, 0xD2, 0xFF),
        ["lightgray"] = new Rgba32(0xD3, 0xD3, 0xD3, 0xFF),
        ["lightgreen"] = new Rgba32(0x90, 0xEE, 0x90, 0xFF),
        ["lightgrey"] = new Rgba32(0xD3, 0xD3, 0xD3, 0xFF),
        ["lightpink"] = new Rgba32(0xFF, 0xB6, 0xC1, 0xFF),
        ["lightsalmon"] = new Rgba32(0xFF, 0xA0, 0x7A, 0xFF),
        ["lightseagreen"] = new Rgba32(0x20, 0xB2, 0xAA, 0xFF),
        ["lightskyblue"] = new Rgba32(0x87, 0xCE, 0xFA, 0xFF),
        ["lightslategray"] = new Rgba32(0x77, 0x88, 0x99, 0xFF),
        ["lightslategrey"] = new Rgba32(0x77, 0x88, 0x99, 0xFF),
        ["lightsteelblue"] = new Rgba32(0xB0, 0xC4, 0xDE, 0xFF),
        ["lightyellow"] = new Rgba32(0xFF, 0xFF, 0xE0, 0xFF),
        ["lime"] = new Rgba32(0x00, 0xFF, 0x00, 0xFF),
        ["limegreen"] = new Rgba32(0x32, 0xCD, 0x32, 0xFF),
        ["linen"] = new Rgba32(0xFA, 0xF0, 0xE6, 0xFF),
        ["magenta"] = new Rgba32(0xFF, 0x00, 0xFF, 0xFF),
        ["maroon"] = new Rgba32(0x80, 0x00, 0x00, 0xFF),
        ["mediumaquamarine"] = new Rgba32(0x66, 0xCD, 0xAA, 0xFF),
        ["mediumblue"] = new Rgba32(0x00, 0x00, 0xCD, 0xFF),
        ["mediumorchid"] = new Rgba32(0xBA, 0x55, 0xD3, 0xFF),
        ["mediumpurple"] = new Rgba32(0x93, 0x70, 0xDB, 0xFF),
        ["mediumseagreen"] = new Rgba32(0x3C, 0xB3, 0x71, 0xFF),
        ["mediumslateblue"] = new Rgba32(0x7B, 0x68, 0xEE, 0xFF),
        ["mediumspringgreen"] = new Rgba32(0x00, 0xFA, 0x9A, 0xFF),
        ["mediumturquoise"] = new Rgba32(0x48, 0xD1, 0xCC, 0xFF),
        ["mediumvioletred"] = new Rgba32(0xC7, 0x15, 0x85, 0xFF),
        ["midnightblue"] = new Rgba32(0x19, 0x19, 0x70, 0xFF),
        ["mintcream"] = new Rgba32(0xF5, 0xFF, 0xFA, 0xFF),
        ["mistyrose"] = new Rgba32(0xFF, 0xE4, 0xE1, 0xFF),
        ["moccasin"] = new Rgba32(0xFF, 0xE4, 0xB5, 0xFF),
        ["navajowhite"] = new Rgba32(0xFF, 0xDE, 0xAD, 0xFF),
        ["navy"] = new Rgba32(0x00, 0x00, 0x80, 0xFF),
        ["oldlace"] = new Rgba32(0xFD, 0xF5, 0xE6, 0xFF),
        ["olive"] = new Rgba32(0x80, 0x80, 0x00, 0xFF),
        ["olivedrab"] = new Rgba32(0x6B, 0x8E, 0x23, 0xFF),
        ["orange"] = new Rgba32(0xFF, 0xA5, 0x00, 0xFF),
        ["orangered"] = new Rgba32(0xFF, 0x45, 0x00, 0xFF),
        ["orchid"] = new Rgba32(0xDA, 0x70, 0xD6, 0xFF),
        ["palegoldenrod"] = new Rgba32(0xEE, 0xE8, 0xAA, 0xFF),
        ["palegreen"] = new Rgba32(0x98, 0xFB, 0x98, 0xFF),
        ["paleturquoise"] = new Rgba32(0xAF, 0xEE, 0xEE, 0xFF),
        ["palevioletred"] = new Rgba32(0xDB, 0x70, 0x93, 0xFF),
        ["papayawhip"] = new Rgba32(0xFF, 0xEF, 0xD5, 0xFF),
        ["peachpuff"] = new Rgba32(0xFF, 0xDA, 0xB9, 0xFF),
        ["peru"] = new Rgba32(0xCD, 0x85, 0x3F, 0xFF),
        ["pink"] = new Rgba32(0xFF, 0xC0, 0xCB, 0xFF),
        ["plum"] = new Rgba32(0xDD, 0xA0, 0xDD, 0xFF),
        ["powderblue"] = new Rgba32(0xB0, 0xE0, 0xE6, 0xFF),
        ["purple"] = new Rgba32(0x80, 0x00, 0x80, 0xFF),
        ["rebeccapurple"] = new Rgba32(0x66, 0x33, 0x99, 0xFF),
        ["red"] = new Rgba32(0xFF, 0x00, 0x00, 0xFF),
        ["rosybrown"] = new Rgba32(0xBC, 0x8F, 0x8F, 0xFF),
        ["royalblue"] = new Rgba32(0x41, 0x69, 0xE1, 0xFF),
        ["saddlebrown"] = new Rgba32(0x8B, 0x45, 0x13, 0xFF),
        ["salmon"] = new Rgba32(0xFA, 0x80, 0x72, 0xFF),
        ["sandybrown"] = new Rgba32(0xF4, 0xA4, 0x60, 0xFF),
        ["seagreen"] = new Rgba32(0x2E, 0x8B, 0x57, 0xFF),
        ["seashell"] = new Rgba32(0xFF, 0xF5, 0xEE, 0xFF),
        ["sienna"] = new Rgba32(0xA0, 0x52, 0x2D, 0xFF),
        ["silver"] = new Rgba32(0xC0, 0xC0, 0xC0, 0xFF),
        ["skyblue"] = new Rgba32(0x87, 0xCE, 0xEB, 0xFF),
        ["slateblue"] = new Rgba32(0x6A, 0x5A, 0xCD, 0xFF),
        ["slategray"] = new Rgba32(0x70, 0x80, 0x90, 0xFF),
        ["slategrey"] = new Rgba32(0x70, 0x80, 0x90, 0xFF),
        ["snow"] = new Rgba32(0xFF, 0xFA, 0xFA, 0xFF),
        ["springgreen"] = new Rgba32(0x00, 0xFF, 0x7F, 0xFF),
        ["steelblue"] = new Rgba32(0x46, 0x82, 0xB4, 0xFF),
        ["tan"] = new Rgba32(0xD2, 0xB4, 0x8C, 0xFF),
        ["teal"] = new Rgba32(0x00, 0x80, 0x80, 0xFF),
        ["thistle"] = new Rgba32(0xD8, 0xBF, 0xD8, 0xFF),
        ["tomato"] = new Rgba32(0xFF, 0x63, 0x47, 0xFF),
        ["transparent"] = new Rgba32(0x00, 0x00, 0x00, 0x00),
        ["turquoise"] = new Rgba32(0x40, 0xE0, 0xD0, 0xFF),
        ["violet"] = new Rgba32(0xEE, 0x82, 0xEE, 0xFF),
        ["wheat"] = new Rgba32(0xF5, 0xDE, 0xB3, 0xFF),
        ["white"] = new Rgba32(0xFF, 0xFF, 0xFF, 0xFF),
        ["whitesmoke"] = new Rgba32(0xF5, 0xF5, 0xF5, 0xFF),
        ["yellow"] = new Rgba32(0xFF, 0xFF, 0x00, 0xFF),
        ["yellowgreen"] = new Rgba32(0x9A, 0xCD, 0x32, 0xFF)
    };
}
