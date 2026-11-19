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
    /// <param name="state">
    ///     The referencing element's cascaded state, used to resolve a gradient coordinate written
    ///     with a CSS unit (<c>em</c> resolves against the referencing element's font - a documented
    ///     simplification of the paint server's own cascade).
    /// </param>
    /// <returns>
    ///     A boxed <see cref="Rgba32"/>, a <see cref="Gradient"/>, or <see langword="null"/> - see
    ///     this class's error-handling policy remarks for why an unrecognized color keyword
    ///     tolerantly resolves to <see langword="null"/> rather than throwing. A <c>url()</c>
    ///     reference that is not a local <c>#id</c>, names no element, or names an element that is
    ///     not a <c>linearGradient</c>, <c>radialGradient</c> or <c>pattern</c> resolves to the
    ///     fallback color written after it (<c>url(#missing) orange</c>), or to
    ///     <see langword="null"/> when there is none, as SVG 2 and browsers do; a gradient that
    ///     exists but has no stops paints nothing even with a fallback (zero stops mean
    ///     <c>none</c>). A gradient in <c>objectBoundingBox</c> units on a shape whose bounding box
    ///     has no width or no height is ignored, as the SVG specification requires: the fallback
    ///     color written after the reference (<c>url(#g) red</c>) is returned instead, or
    ///     <see langword="null"/> when there is none (see <see cref="IsObjectBoundingBoxServerOnEmptyBox"/>).
    /// </returns>
    private static object? ResolvePaint(string spec, float alphaMultiplier, Path localPath, Matrix3x2 transform, RenderContext context, RenderState state)
    {
        var trimmed = spec.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            // A reference that is not a local fragment, names no element, or names an element that is not a paint
            // server is invalid: the fallback color written after it is painted, or nothing without one (SVG 2
            // Painting, "Specifying paint"; Edge does the same)
            var id = ExtractUrlId(trimmed);
            if (id == null || !context.IdIndex.TryGetValue(id, out var element) || !IsPaintServerElement(element))
            {
                return ResolvePaintFallback(trimmed, alphaMultiplier);
            }

            // A bounding-box-relative gradient cannot be placed on a box with no width or no
            // height, so the specification ignores it and the paint falls back to the color
            // written after the reference, if any
            if (IsObjectBoundingBoxServerOnEmptyBox(element, localPath))
            {
                return ResolvePaintFallback(trimmed, alphaMultiplier);
            }

            return BuildGradient(element, alphaMultiplier, localPath, transform, context, state);
        }

        var color = ParseColor(trimmed);
        return color == null ? null : ApplyAlpha(color.Value, alphaMultiplier);
    }

    /// <summary>
    ///     Determines whether a paint server must be ignored because it is laid out in
    ///     <c>objectBoundingBox</c> units and the shape's bounding box has no width or no height.
    /// </summary>
    /// <remarks>
    ///     The SVG specification (coordinate systems, "object bounding box units") ignores such an
    ///     effect, because a box with no extent on one axis cannot map the unit square; browsers then
    ///     paint the fallback color of the paint value, or nothing. This check is kept apart from
    ///     <see cref="ComputeObjectBoundingBoxMap(Rect)"/>, whose identity result for an empty box is
    ///     still relied on by <c>clipPathUnits</c> and <c>maskContentUnits</c>. The unit attributes
    ///     are read from <paramref name="server"/> itself, exactly as <see cref="BuildGradient"/> and
    ///     <see cref="RenderPatternFill"/> read them. Stateless and thread-safe.
    /// </remarks>
    /// <param name="server">The referenced element: a gradient, a pattern, or any other element (never ignored).</param>
    /// <param name="localPath">The shape's local-space outline (never the stroke outline), the bounding-box basis.</param>
    /// <returns>
    ///     <see langword="true"/> when the bounds of <paramref name="localPath"/> are empty or have no width or no
    ///     height and <paramref name="server"/> is a <c>linearGradient</c>/<c>radialGradient</c> whose
    ///     <c>gradientUnits</c> is not <c>userSpaceOnUse</c>, or a <c>pattern</c> whose <c>patternUnits</c> is not
    ///     <c>userSpaceOnUse</c> or whose content is in <c>objectBoundingBox</c> units without a <c>viewBox</c>.
    /// </returns>
    private static bool IsObjectBoundingBoxServerOnEmptyBox(XElement server, Path localPath)
    {
        // A box with extent on both axes can always be mapped, whatever the units
        var bounds = localPath.GetBounds();
        if (!bounds.IsEmpty && bounds.Width > 0f && bounds.Height > 0f)
        {
            return false;
        }

        switch (server.Name.LocalName)
        {
            case "linearGradient":
            case "radialGradient":
                // objectBoundingBox is the default gradientUnits
                return !string.Equals(
                    (string?)server.Attribute("gradientUnits"), "userSpaceOnUse", StringComparison.OrdinalIgnoreCase);

            case "pattern":
                // The tile is placed in objectBoundingBox units by default; the content is only in
                // objectBoundingBox units when asked, and a viewBox overrides patternContentUnits
                var tileUsesBox = !string.Equals(
                    (string?)server.Attribute("patternUnits"), "userSpaceOnUse", StringComparison.Ordinal);
                var contentUsesBox = string.Equals(
                        (string?)server.Attribute("patternContentUnits"), "objectBoundingBox", StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace((string?)server.Attribute("viewBox"));
                return tileUsesBox || contentUsesBox;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Determines whether an element is a paint server a <c>fill</c> or <c>stroke</c> can reference.
    /// </summary>
    /// <param name="element">The referenced element.</param>
    /// <returns><see langword="true"/> for a <c>linearGradient</c>, <c>radialGradient</c> or <c>pattern</c> element.</returns>
    private static bool IsPaintServerElement(XElement element) =>
        element.Name.LocalName is "linearGradient" or "radialGradient" or "pattern";

    /// <summary>
    ///     Resolves the fallback color written after a <c>url(...)</c> reference in a paint value
    ///     (<c>url(#g) red</c>), used when the referenced paint server is ignored or invalid.
    /// </summary>
    /// <param name="spec">The trimmed, <c>url(</c>-prefixed paint specification.</param>
    /// <param name="alphaMultiplier">The combined opacity multiplier folded into the fallback color's alpha.</param>
    /// <returns>
    ///     The boxed fallback <see cref="Rgba32"/> with its alpha scaled, or <see langword="null"/> (paint nothing)
    ///     when there is no fallback, it is <c>none</c>, or it is not a recognized color.
    /// </returns>
    private static object? ResolvePaintFallback(string spec, float alphaMultiplier)
    {
        var closeIndex = FindUrlCloseIndex(spec);
        if (closeIndex < 0)
        {
            return null;
        }

        var fallback = spec[(closeIndex + 1)..].Trim();
        if (fallback.Length == 0 || string.Equals(fallback, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var color = ParseColor(fallback);
        return color == null ? null : ApplyAlpha(color.Value, alphaMultiplier);
    }

    /// <summary>Extracts the fragment id referenced by a <c>url(#id)</c> paint specification.</summary>
    /// <param name="spec">The raw, already <c>url(</c>-prefixed specification, optionally followed by a fallback.</param>
    /// <returns>The referenced id, or <see langword="null"/> if the syntax is not a <c>url(#id)</c> reference.</returns>
    private static string? ExtractUrlId(string spec)
    {
        var openIndex = spec.IndexOf('(');
        var closeIndex = FindUrlCloseIndex(spec);
        if (openIndex < 0 || closeIndex < 0)
        {
            return null;
        }

        var inner = spec[(openIndex + 1)..closeIndex].Trim().Trim('\'', '"');
        return inner.StartsWith('#') ? inner[1..] : null;
    }

    /// <summary>
    ///     Finds the <c>)</c> that closes a <c>url(...)</c> reference: the first one after the opening
    ///     <c>(</c>, as for an unquoted CSS <c>url()</c>, so a fallback such as <c>rgb(0,128,0)</c>
    ///     written after the reference is not taken into the id.
    /// </summary>
    /// <param name="spec">The raw, already <c>url(</c>-prefixed specification.</param>
    /// <returns>The index of the closing parenthesis, or <c>-1</c> when there is none.</returns>
    private static int FindUrlCloseIndex(string spec)
    {
        var openIndex = spec.IndexOf('(');
        return openIndex < 0 ? -1 : spec.IndexOf(')', openIndex + 1);
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
    ///     Parses either a bare number or a CSS percentage (resolved against <paramref name="basis"/>)
    ///     - the tolerant (class T) number-or-percentage parser used by opacity-like and
    ///     fraction-only values (stop <c>offset</c>, filter fractions). Gradient, pattern and mask
    ///     coordinates do not use it: they resolve through <see cref="TryResolveLength"/> with a
    ///     coordinate-space axis (see <see cref="GetGradientCoordinateOrDefault"/>), so a plain
    ///     percentage and a percentage inside <c>calc()</c> always agree.
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
