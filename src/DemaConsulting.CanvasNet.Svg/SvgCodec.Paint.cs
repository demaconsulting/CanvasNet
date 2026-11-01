// cspell:ignore linecap linejoin dasharray dashoffset miterlimit anchor xlink href
// cspell:ignore Glyf Loca
// cspell:ignore evenodd nonzero viewbox gradientunits gradienttransform spreadmethod
// cspell:ignore userspaceonuse objectboundingbox skewx skewy tspan
// cspell:ignore rasterizing unparseable rrggbb sizeless bbox moveto multiplicatively pillarbox SMIL uncatchable formedness
// cspell:ignore unblurred premult
using System.Globalization;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Svg;

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

    /// <summary>Applies an alpha multiplier to a color, rounding half away from zero and clamping the result to a valid byte.</summary>
    /// <param name="color">The base color.</param>
    /// <param name="multiplier">The multiplier to apply to <paramref name="color"/>'s existing alpha.</param>
    /// <returns>The color with its alpha channel scaled.</returns>
    private static Rgba32 ApplyAlpha(Rgba32 color, float multiplier)
    {
        var alpha = (byte)Math.Clamp(MathF.Round(color.A * multiplier, MidpointRounding.AwayFromZero), 0, 255);
        return new Rgba32(color.R, color.G, color.B, alpha);
    }

    /// <summary>
    ///     Parses a CSS/SVG color value with the shared CSS color grammar of the core library
    ///     (<see cref="CssColor.TryParse"/>): a named keyword, <c>#rgb</c>/<c>#rgba</c>/<c>#rrggbb</c>/<c>#rrggbbaa</c>
    ///     hex, <c>rgb(...)</c>/<c>rgba(...)</c>, or <c>hsl(...)</c>/<c>hsla(...)</c>.
    /// </summary>
    /// <param name="spec">The trimmed, non-empty, non-<c>"none"</c> color text.</param>
    /// <returns>
    ///     The parsed color, or <see langword="null"/> if <paramref name="spec"/> does not match
    ///     any supported form - tolerated as "no paint" rather than an error, since an
    ///     unrecognized color keyword is a well-formed-but-unsupported value, not corrupt data.
    /// </returns>
    private static Rgba32? ParseColor(string spec) => CssColor.TryParse(spec, out var color) ? color : null;

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
}
