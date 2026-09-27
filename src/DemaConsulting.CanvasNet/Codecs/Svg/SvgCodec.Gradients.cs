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
    // Gradient resolution
    // ================================================================================================

    /// <summary>
    ///     Resolves a <c>linearGradient</c>/<c>radialGradient</c> element (reached via a
    ///     <c>fill="url(#id)"</c>/<c>stroke="url(#id)"</c> reference) into a concrete
    ///     <see cref="Gradient"/>.
    /// </summary>
    /// <param name="element">The gradient element referenced by id.</param>
    /// <param name="alphaMultiplier">The combined opacity multiplier folded into every stop's alpha.</param>
    /// <param name="localPath">The shape's local-space outline, used as the object-bounding-box basis.</param>
    /// <param name="elementTransform">The accumulated transform from local space into pixel space.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The resolved <see cref="Gradient"/>, or <see langword="null"/> if it resolves to zero
    ///     stops (tolerated as "no paint"), <paramref name="element"/> is not itself a
    ///     <c>linearGradient</c>/<c>radialGradient</c>, or its composed
    ///     <c>gradientTransform</c>/bounding-box/<paramref name="elementTransform"/> product
    ///     overflows to a non-finite value (also tolerated as "no paint").
    /// </returns>
    /// <remarks>
    ///     <paramref name="element"/>'s own <c>gradientUnits</c>/<c>gradientTransform</c>/
    ///     <c>spreadMethod</c>/coordinate attributes are always read directly from
    ///     <paramref name="element"/> itself, never inherited through an <c>href</c> chain -
    ///     only color stops are template-inherited (see <see cref="ResolveGradientStops"/>). This
    ///     is a deliberate, bounded simplification of the full SVG href-inheritance model,
    ///     documented as an out-of-scope limitation. This composition is independent of, and not
    ///     subsumed by, <see cref="RenderElement"/>'s own composed-transform finiteness check: an
    ///     extreme-but-individually-finite shape bounding box combined with a
    ///     <c>gradientTransform</c> can overflow this method's own product even when
    ///     <paramref name="elementTransform"/> alone is finite.
    /// </remarks>
    private static object? BuildGradient(
        XElement element,
        float alphaMultiplier,
        Path localPath,
        Matrix3x2 elementTransform,
        RenderContext context)
    {
        var stops = ApplyAlphaToStops(ResolveGradientStops(element, context), alphaMultiplier);
        if (stops.Count == 0)
        {
            return null;
        }

        var isUserSpace = string.Equals(
            (string?)element.Attribute("gradientUnits"), "userSpaceOnUse", StringComparison.OrdinalIgnoreCase);
        var bboxMap = isUserSpace ? Matrix3x2.Identity : ComputeObjectBoundingBoxMap(localPath);
        var transform = ParseGradientTransform(element) * bboxMap * elementTransform;

        // A non-finite composed transform cannot meaningfully position this gradient's stops -
        // tolerate it as "no paint", matching ResolvePaint's existing dangling-reference/
        // unrecognized-color convention, rather than letting it reach Gradient's constructor and
        // throw an uncaught ArgumentOutOfRangeException
        if (!IsFiniteTransform(transform))
        {
            return null;
        }

        var spread = ParseSpreadMethod((string?)element.Attribute("spreadMethod"));

        return element.Name.LocalName switch
        {
            "linearGradient" => BuildLinearGradient(element, stops, spread, transform),
            "radialGradient" => BuildRadialGradient(element, stops, spread, transform),
            _ => null
        };
    }

    /// <summary>
    ///     Computes the transform mapping the <c>[0, 1]</c> object-bounding-box fraction space
    ///     onto <paramref name="localPath"/>'s own local-space bounds.
    /// </summary>
    /// <param name="localPath">The shape's local-space outline.</param>
    /// <returns>
    ///     The bounding-box mapping transform, or <see cref="Matrix3x2.Identity"/> if the path's
    ///     bounds are empty or have zero extent on either axis (a degenerate case tolerated rather
    ///     than producing a non-invertible/zero-scale gradient mapping).
    /// </returns>
    private static Matrix3x2 ComputeObjectBoundingBoxMap(Path localPath)
    {
        return ComputeObjectBoundingBoxMap(localPath.GetBounds());
    }

    /// <summary>
    ///     Computes the transform mapping the <c>[0, 1]</c> object-bounding-box fraction space
    ///     onto <paramref name="bounds"/> directly - the bounds-only counterpart of
    ///     <see cref="ComputeObjectBoundingBoxMap(Path)"/> (which simply forwards to this overload
    ///     after computing its own path's bounds), also used by <c>clipPathUnits</c>/
    ///     <c>maskContentUnits</c> resolution (see <see cref="ApplyClipPath"/>/<see cref="ApplyMask"/>)
    ///     where the reference bounds are already known and no <see cref="Path"/> is available.
    /// </summary>
    /// <param name="bounds">The reference local-space bounds.</param>
    /// <returns>
    ///     The bounding-box mapping transform, or <see cref="Matrix3x2.Identity"/> if
    ///     <paramref name="bounds"/> is empty or has zero extent on either axis (a degenerate case
    ///     tolerated rather than producing a non-invertible/zero-scale mapping).
    /// </returns>
    private static Matrix3x2 ComputeObjectBoundingBoxMap(Rect bounds)
    {
        if (bounds.IsEmpty || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return Matrix3x2.Identity;
        }

        return Matrix3x2.CreateScale(bounds.Width, bounds.Height) * Matrix3x2.CreateTranslation(bounds.X, bounds.Y);
    }

    /// <summary>
    ///     Resolves a gradient element's effective color stops, walking its <c>href</c>/
    ///     <c>xlink:href</c> template-inheritance chain (starting at <paramref name="start"/>
    ///     itself) until an element with at least one <c>stop</c> child is found. The result is
    ///     cached per <paramref name="start"/> element (see
    ///     <see cref="RenderContext.GradientStopCache"/>), so a gradient referenced by many shapes
    ///     has this chain walk performed only once per <c>Load</c> call.
    /// </summary>
    /// <param name="start">The gradient element originally referenced.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>
    ///     The first chain element's own stops (not merged across chain levels), or an empty list
    ///     if the chain ends (a dangling/absent/non-gradient <c>href</c> target) without ever
    ///     finding one with stops.
    /// </returns>
    /// <exception cref="InvalidDataException">Thrown when the chain revisits an element (a cycle).</exception>
    private static List<GradientStop> ResolveGradientStops(XElement start, RenderContext context)
    {
        if (context.GradientStopCache.TryGetValue(start, out var cached))
        {
            return cached;
        }

        var visited = new HashSet<XElement>();
        var current = start;
        while (visited.Add(current))
        {
            var stops = ParseStops(current);
            if (stops.Count > 0)
            {
                context.GradientStopCache[start] = stops;
                return stops;
            }

            var hrefId = GetHrefAttribute(current) is { } href ? ExtractFragmentId(href) : null;
            if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var next))
            {
                // Cache the empty-list terminal case too, so a dangling/cycle-free chain with no
                // stops is not re-walked on every future reference to the same starting element
                context.GradientStopCache[start] = stops;
                return stops;
            }

            current = next;
        }

        throw new InvalidDataException("The gradient's href chain contains a cycle.");
    }

    /// <summary>Extracts the fragment id from a bare <c>#id</c>-form <c>href</c> value.</summary>
    /// <param name="raw">The raw <c>href</c>/<c>xlink:href</c> attribute value.</param>
    /// <returns>The referenced id, or <see langword="null"/> if not a <c>#id</c>-form reference.</returns>
    private static string? ExtractFragmentId(string raw) => raw.StartsWith('#') ? raw[1..] : null;

    /// <summary>Parses a gradient element's direct <c>stop</c> children into color stops.</summary>
    /// <param name="gradientElement">The gradient element to inspect.</param>
    /// <returns>
    ///     The parsed stops, in document order, with each stop's effective offset clamped to
    ///     <c>[0, 1]</c> and forced non-decreasing relative to the previous stop, per the SVG
    ///     specification's stop-offset normalization rule.
    /// </returns>
    private static List<GradientStop> ParseStops(XElement gradientElement)
    {
        var stops = new List<GradientStop>();
        var previousOffset = 0f;

        foreach (var stopElement in gradientElement.Elements().Where(e => e.Name.LocalName == "stop"))
        {
            var rawOffset = (string?)stopElement.Attribute("offset");
            var offset = rawOffset == null ? 0f : ParsePercentOrNumber(rawOffset, 1f) ?? 0f;
            offset = Math.Max(previousOffset, Math.Clamp(offset, 0f, 1f));
            previousOffset = offset;

            stops.Add(new GradientStop(offset, ParseStopColor(stopElement)));
        }

        return stops;
    }

    /// <summary>Parses a single <c>stop</c> element's <c>stop-color</c>/<c>stop-opacity</c> into a color.</summary>
    /// <param name="stopElement">The <c>stop</c> element.</param>
    /// <returns>
    ///     The stop's color; <c>stop-color</c> defaults to black and <c>stop-opacity</c> defaults
    ///     to fully opaque, per the SVG specification's initial values. An unrecognized
    ///     <c>stop-color</c> keyword tolerantly falls back to black rather than throwing.
    /// </returns>
    private static Rgba32 ParseStopColor(XElement stopElement)
    {
        var rawColor = (string?)stopElement.Attribute("stop-color");
        var color = rawColor == null ? null : ParseColor(rawColor.Trim());
        var baseColor = color ?? new Rgba32(0, 0, 0, 255);

        var rawOpacity = (string?)stopElement.Attribute("stop-opacity");
        var opacity = rawOpacity == null ? 1f : ParseOpacityValue(rawOpacity);

        return ApplyAlpha(baseColor, opacity);
    }

    /// <summary>Applies an alpha multiplier to every stop's color, preserving each stop's offset.</summary>
    /// <param name="stops">The source stops.</param>
    /// <param name="multiplier">The multiplier to apply to each stop's existing alpha.</param>
    /// <returns>A new list of stops with their colors' alpha scaled.</returns>
    private static List<GradientStop> ApplyAlphaToStops(List<GradientStop> stops, float multiplier) =>
        stops.Select(stop => new GradientStop(stop.Offset, ApplyAlpha(stop.Color, multiplier))).ToList();

    /// <summary>Parses a <c>spreadMethod</c> keyword.</summary>
    /// <param name="raw">The attribute's raw value, or <see langword="null"/> if absent.</param>
    /// <returns>
    ///     The matching <see cref="GradientSpread"/>, defaulting to <see cref="GradientSpread.Pad"/>
    ///     for an absent or unrecognized value.
    /// </returns>
    private static GradientSpread ParseSpreadMethod(string? raw) => raw switch
    {
        "reflect" => GradientSpread.Reflect,
        "repeat" => GradientSpread.Repeat,
        _ => GradientSpread.Pad
    };

    /// <summary>
    ///     Reads one gradient-geometry coordinate/percentage attribute (for example <c>x1</c>,
    ///     <c>cx</c>, <c>r</c>), tolerantly falling back to <paramref name="fallback"/> for an
    ///     absent or unparseable value.
    /// </summary>
    /// <param name="element">The gradient element to inspect.</param>
    /// <param name="name">The attribute name to read.</param>
    /// <param name="fallback">The value to use if the attribute is absent or unparseable.</param>
    /// <returns>The resolved coordinate value.</returns>
    /// <remarks>
    ///     A <c>%</c> suffix is always resolved as a fraction of <c>1</c> (matching the
    ///     objectBoundingBox unit convention), even in <c>userSpaceOnUse</c> mode, where a
    ///     percentage should properly resolve against the current viewport - a documented,
    ///     bounded simplification.
    /// </remarks>
    private static float GetGradientCoordinateOrDefault(XElement element, string name, float fallback)
    {
        var raw = (string?)element.Attribute(name);
        return raw == null ? fallback : ParsePercentOrNumber(raw, 1f) ?? fallback;
    }

    /// <summary>
    ///     Reads one gradient <b>radius</b> attribute (<c>r</c> or <c>fr</c>), tolerantly falling
    ///     back to <paramref name="fallback"/> for an absent, unparseable, non-finite, <b>or
    ///     negative</b> value.
    /// </summary>
    /// <remarks>
    ///     <see cref="GetGradientCoordinateOrDefault"/> already tolerates an absent/unparseable/
    ///     non-finite value, but a radius is the only gradient-geometry attribute with an
    ///     additional sign constraint - <see cref="Drawing.RadialGradient"/>'s constructor throws
    ///     <see cref="ArgumentOutOfRangeException"/> for a negative <c>startRadius</c>/<c>endRadius</c>.
    ///     A negative <c>r</c>/<c>fr</c> is syntactically valid (finite) but out of that documented
    ///     contract, so - matching this codec's existing tolerant handling of every other gradient
    ///     coordinate (see <see cref="GetGradientCoordinateOrDefault"/>) rather than aborting the
    ///     whole document over one presentation attribute - it falls back to
    ///     <paramref name="fallback"/> here instead of reaching the constructor and throwing an
    ///     undocumented <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    /// <param name="element">The gradient element to inspect.</param>
    /// <param name="name">The attribute name to read (<c>r</c> or <c>fr</c>).</param>
    /// <param name="fallback">The value to use if the attribute is absent, unparseable, or negative.</param>
    /// <returns>The resolved, non-negative radius value.</returns>
    private static float GetGradientRadiusOrDefault(XElement element, string name, float fallback)
    {
        var value = GetGradientCoordinateOrDefault(element, name, fallback);
        return value >= 0f ? value : fallback;
    }

    /// <summary>Builds a <c>linearGradient</c> element's <see cref="LinearGradient"/>.</summary>
    /// <param name="element">The <c>linearGradient</c> element.</param>
    /// <param name="stops">The resolved, alpha-adjusted color stops.</param>
    /// <param name="spread">The resolved spread method.</param>
    /// <param name="transform">The combined gradient-to-pixel-space transform.</param>
    /// <returns>The built gradient.</returns>
    private static LinearGradient BuildLinearGradient(
        XElement element,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread,
        Matrix3x2 transform)
    {
        var start = new Vector2(
            GetGradientCoordinateOrDefault(element, "x1", 0f),
            GetGradientCoordinateOrDefault(element, "y1", 0f));
        var end = new Vector2(
            GetGradientCoordinateOrDefault(element, "x2", 1f),
            GetGradientCoordinateOrDefault(element, "y2", 0f));

        return new LinearGradient(start, end, stops, spread, transform);
    }

    /// <summary>Builds a <c>radialGradient</c> element's <see cref="RadialGradient"/>.</summary>
    /// <param name="element">The <c>radialGradient</c> element.</param>
    /// <param name="stops">The resolved, alpha-adjusted color stops.</param>
    /// <param name="spread">The resolved spread method.</param>
    /// <param name="transform">The combined gradient-to-pixel-space transform.</param>
    /// <returns>The built gradient.</returns>
    /// <remarks>
    ///     The focal point (<c>fx</c>/<c>fy</c>) defaults to the end circle's own center
    ///     (<c>cx</c>/<c>cy</c>) per the SVG specification, and the focal radius (<c>fr</c>,
    ///     SVG 2) defaults to zero.
    /// </remarks>
    private static RadialGradient BuildRadialGradient(
        XElement element,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread,
        Matrix3x2 transform)
    {
        var cx = GetGradientCoordinateOrDefault(element, "cx", 0.5f);
        var cy = GetGradientCoordinateOrDefault(element, "cy", 0.5f);
        var r = GetGradientRadiusOrDefault(element, "r", 0.5f);
        var fx = GetGradientCoordinateOrDefault(element, "fx", cx);
        var fy = GetGradientCoordinateOrDefault(element, "fy", cy);
        var fr = GetGradientRadiusOrDefault(element, "fr", 0f);

        return new RadialGradient(new Vector2(fx, fy), fr, new Vector2(cx, cy), r, stops, spread, transform);
    }
}
