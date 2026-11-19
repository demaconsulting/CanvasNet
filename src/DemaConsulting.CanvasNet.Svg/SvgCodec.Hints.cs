using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Svg;

// SvgCodec.Hints.cs - authoring hints reported through SvgLoadOptions.HintHandler (see SvgHint).
//
// The one hint today is svg-arrowhead-shape: a filled triangle (or notched four-point arrow shape) drawn by hand at the
// end of a stroked connector that has no marker at that end. Such a head is placed and rotated by the author, so it
// does not follow the connector when the connector changes; a marker with orient="auto" does. The analysis is
// deliberately conservative - a hint is a nudge, never a failure - and runs only when a handler is set:
//
//   - only shapes painted directly by the document count (not through use, not marker content, and nothing inside
//     defs, marker, symbol, pattern, mask or clipPath);
//   - a connector end is the first or last point of an open line/polyline/path with a visible stroke, no fill (a line
//     never fills), and no marker at that end;
//   - a head is a polygon/path with a visible fill made of one subpath of straight segments with 3 distinct points
//     (a triangle, not a sliver) or 4 points with exactly one reflex corner (a notched head);
//   - a head matches when its longest edge L is between 1.5 and 12 times the connector's stroke width and its centroid
//     is more than a fifth of L and at most L from the connector end (both in device pixels), so a symbol centered on
//     the end (a chart's data point) is not taken for a head.
//
// Collection is capped so a hostile document cannot make the analysis expensive; one SvgHint is reported per document
// with the number of matched heads and the first one in document order.
public static partial class SvgCodec
{
    /// <summary>The most connector ends the arrowhead analysis records per document.</summary>
    private const int MaxHintConnectorEnds = 1024;

    /// <summary>The most head candidates the arrowhead analysis records per document.</summary>
    private const int MaxHintHeads = 1024;

    /// <summary>The smallest head size, as a multiple of the connector's stroke width, that counts as an arrowhead.</summary>
    private const float MinHeadToStrokeRatio = 1.5f;

    /// <summary>The largest head size, as a multiple of the connector's stroke width, that counts as an arrowhead.</summary>
    private const float MaxHeadToStrokeRatio = 12f;

    /// <summary>The smallest triangle area, as a fraction of its longest edge squared, that is not a sliver.</summary>
    private const float MinHeadAreaRatio = 0.05f;

    /// <summary>
    ///     The head-centroid distance from a connector end, as a fraction of the head's longest edge, at or below which the
    ///     shape is centered on the end - a data-point symbol such as a chart marker, not an arrowhead.
    /// </summary>
    private const float CenteredHeadRatio = 0.2f;

    /// <summary>
    ///     The containers whose content never reports a hint: templates and paint/clip content that is not part of the
    ///     figure as drawn.
    /// </summary>
    private static readonly HashSet<string> HintExcludedContainers = new(StringComparer.Ordinal)
    {
        "defs", "marker", "symbol", "pattern", "mask", "clipPath"
    };

    /// <summary>
    ///     Records <paramref name="element"/> as a connector and/or a head candidate for the hand-drawn-arrowhead hint.
    /// </summary>
    /// <param name="hints">The collector of the document.</param>
    /// <param name="element">The <c>line</c>/<c>polyline</c>/<c>polygon</c>/<c>path</c> element just painted directly.</param>
    /// <param name="path">The element's local-space outline.</param>
    /// <param name="state">The element's cascaded render state.</param>
    /// <param name="transform">The element's composed transform to device pixels.</param>
    /// <param name="context">The fixed per-document render context, for resolving marker references.</param>
    private static void CollectArrowheadHintShape(
        ArrowheadHintCollector hints,
        XElement element,
        Path path,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context)
    {
        if (!hints.TryVisit(element) || path.Subpaths.Count == 0 || element.Ancestors().Any(a => HintExcludedContainers.Contains(a.Name.LocalName)))
        {
            return;
        }

        var name = element.Name.LocalName;

        // Visibility follows the renderer's own paint path (a pattern first, then ResolvePaint), so a dangling or invalid
        // url(#...) without a fallback color, a color that resolves to full transparency, or a gradient whose stops are all
        // transparent counts as no paint, exactly as it is (not) drawn; with a fallback color the fallback decides
        var hasFill = name != "line" && HasVisiblePaint(state.Fill, state.FillOpacity * state.Opacity, path, transform, context, state);
        var hasStroke = state.StrokeWidth > 0 && HasVisiblePaint(state.Stroke, state.StrokeOpacity * state.Opacity, path, transform, context, state);

        if (hasStroke && !hasFill && name != "polygon")
        {
            CollectConnectorEnds(hints, element, path, state, transform, context);
        }

        if (hasFill && (name is "polygon" or "path") && TryMeasureHead(path, transform, out var centroid, out var longestEdge))
        {
            hints.AddHead(new HintHead(element, centroid, longestEdge));
        }
    }

    /// <summary>
    ///     Records the open, unmarked ends of a stroked connector.
    /// </summary>
    /// <param name="hints">The collector of the document.</param>
    /// <param name="element">The connector element.</param>
    /// <param name="path">The connector's local-space outline.</param>
    /// <param name="state">The connector's cascaded render state.</param>
    /// <param name="transform">The connector's composed transform to device pixels.</param>
    /// <param name="context">The fixed per-document render context.</param>
    private static void CollectConnectorEnds(
        ArrowheadHintCollector hints,
        XElement element,
        Path path,
        RenderState state,
        Matrix3x2 transform,
        RenderContext context)
    {
        var scale = MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
        var deviceStrokeWidth = state.StrokeWidth * scale;
        if (!float.IsFinite(deviceStrokeWidth) || deviceStrokeWidth <= 0)
        {
            return;
        }

        var first = path.Subpaths[0];
        if (!first.IsClosed && first.Commands.Count > 0 && !HasMarker(state.MarkerStart, context))
        {
            hints.AddConnectorEnd(new HintConnectorEnd(element, Vector2.Transform(first.Start, transform), deviceStrokeWidth));
        }

        var last = path.Subpaths[^1];
        if (!last.IsClosed && last.Commands.Count > 0 && !HasMarker(state.MarkerEnd, context))
        {
            hints.AddConnectorEnd(new HintConnectorEnd(element, Vector2.Transform(last.Commands[^1].EndPoint, transform), deviceStrokeWidth));
        }
    }

    /// <summary>Returns whether a marker specification names a <c>marker</c> element that exists.</summary>
    /// <param name="spec">The raw <c>marker-start</c>/<c>marker-end</c> value.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns><see langword="true"/> when a marker is drawn at that end.</returns>
    private static bool HasMarker(string spec, RenderContext context) =>
        !IsNone(spec) && ResolveMarkerElement(spec, context) != null;

    /// <summary>Returns whether a paint or marker value is the keyword <c>none</c>.</summary>
    /// <param name="value">The raw value.</param>
    /// <returns><see langword="true"/> for <c>none</c> in any case, ignoring surrounding white space.</returns>
    private static bool IsNone(string value) => string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Determines whether a <c>fill</c> or <c>stroke</c> draws anything, following the renderer's own paint path: a
    ///     pattern (resolved first, as the renderer does) is visible when its combined opacity is not zero; otherwise
    ///     the paint from <see cref="ResolvePaint"/> is visible when it is a color whose alpha (with the opacities folded
    ///     in) is not zero, or a gradient with at least one stop whose alpha is not zero. None, an unrecognized color,
    ///     and a dangling or invalid <c>url(#...)</c> without a fallback color draw nothing (with one, the fallback color
    ///     decides). A gradient or pattern in <c>objectBoundingBox</c>
    ///     units on a box with no width or no height is ignored, so only its fallback color can be visible.
    /// </summary>
    /// <param name="spec">The cascaded <c>fill</c> or <c>stroke</c> value.</param>
    /// <param name="alpha">The combined opacity multiplier of that paint.</param>
    /// <param name="path">The element's local-space outline.</param>
    /// <param name="transform">The element's composed transform to device pixels.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="state">The element's cascaded render state.</param>
    /// <returns><see langword="true"/> when the paint is visible.</returns>
    private static bool HasVisiblePaint(string spec, float alpha, Path path, Matrix3x2 transform, RenderContext context, RenderState state)
    {
        if (ResolvePatternElement(spec, context) is { } pattern)
        {
            // A bounding-box-relative pattern on a box with no width or no height is ignored and
            // the renderer paints the fallback color instead, so visibility follows that color
            if (IsObjectBoundingBoxServerOnEmptyBox(pattern, path))
            {
                return ResolvePaintFallback(spec.Trim(), alpha) is Rgba32 fallback && fallback.A > 0;
            }

            return alpha > 0;
        }

        return ResolvePaint(spec, alpha, path, transform, context, state) switch
        {
            Rgba32 color => color.A > 0,
            Gradient gradient => gradient.Stops.Any(stop => stop.Color.A > 0),
            _ => false,
        };
    }

    /// <summary>
    ///     Measures a filled outline as an arrowhead candidate: one subpath of straight segments with 3 distinct points
    ///     (not a sliver) or 4 points with exactly one reflex corner.
    /// </summary>
    /// <param name="path">The local-space outline.</param>
    /// <param name="transform">The composed transform to device pixels.</param>
    /// <param name="centroid">The device-space centroid of the area when this method returns <see langword="true"/>.</param>
    /// <param name="longestEdge">The device-space length of the longest edge when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the outline has the shape of an arrowhead.</returns>
    internal static bool TryMeasureHead(Path path, Matrix3x2 transform, out Vector2 centroid, out float longestEdge)
    {
        centroid = default;
        longestEdge = 0;
        if (path.Subpaths.Count != 1)
        {
            return false;
        }

        var subpath = path.Subpaths[0];
        var points = new List<Vector2>(5) { Vector2.Transform(subpath.Start, transform) };
        foreach (var command in subpath.Commands)
        {
            if (command.Type == PathCommandType.Close)
            {
                continue;
            }

            if (command.Type != PathCommandType.LineTo || points.Count >= 5)
            {
                return false;
            }

            var point = Vector2.Transform(command.EndPoint, transform);
            if (Vector2.DistanceSquared(point, points[^1]) > 1e-6f)
            {
                points.Add(point);
            }
        }

        // A last point that returns to the start is the implicit closing edge written out
        if (points.Count > 1 && Vector2.DistanceSquared(points[0], points[^1]) <= 1e-6f)
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count is not (3 or 4))
        {
            return false;
        }

        var twiceArea = 0f;
        var weighted = Vector2.Zero;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var cross = (a.X * b.Y) - (b.X * a.Y);
            twiceArea += cross;
            weighted += (a + b) * cross;
            longestEdge = MathF.Max(longestEdge, Vector2.Distance(a, b));
        }

        var area = MathF.Abs(twiceArea) / 2;
        if (!float.IsFinite(area) || longestEdge <= 0 || area < MinHeadAreaRatio * longestEdge * longestEdge)
        {
            return false;
        }

        if (points.Count == 4 && CountReflexCorners(points, MathF.Sign(twiceArea)) != 1)
        {
            return false;
        }

        centroid = weighted / (3 * twiceArea);
        return float.IsFinite(centroid.X) && float.IsFinite(centroid.Y);
    }

    /// <summary>Counts the corners of a simple polygon that turn against its orientation.</summary>
    /// <param name="points">The corners, in order.</param>
    /// <param name="orientation">The sign of the polygon's signed area.</param>
    /// <returns>The number of reflex corners.</returns>
    private static int CountReflexCorners(List<Vector2> points, int orientation)
    {
        var reflex = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var previous = points[(i + points.Count - 1) % points.Count];
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            var turn = ((current.X - previous.X) * (next.Y - current.Y)) - ((current.Y - previous.Y) * (next.X - current.X));
            if (MathF.Sign(turn) == -orientation)
            {
                reflex++;
            }
        }

        return reflex;
    }

    /// <summary>One open, unmarked end of a stroked connector, in device pixels.</summary>
    /// <param name="Element">The connector element.</param>
    /// <param name="Point">The end point.</param>
    /// <param name="StrokeWidth">The connector's stroke width.</param>
    private readonly record struct HintConnectorEnd(XElement Element, Vector2 Point, float StrokeWidth);

    /// <summary>One filled arrowhead-shaped outline, in device pixels.</summary>
    /// <param name="Element">The head element.</param>
    /// <param name="Centroid">The centroid of its area.</param>
    /// <param name="LongestEdge">The length of its longest edge.</param>
    private readonly record struct HintHead(XElement Element, Vector2 Centroid, float LongestEdge);

    /// <summary>
    ///     Collects connector ends and head candidates while a document is painted, then reports one
    ///     <see cref="SvgHint"/> for the heads that sit on a connector end.
    /// </summary>
    /// <param name="handler">The host callback.</param>
    private sealed class ArrowheadHintCollector(Action<SvgHint> handler)
    {
        /// <summary>The elements already analyzed (a filtered element can be painted twice).</summary>
        private readonly HashSet<XElement> _visited = [];

        /// <summary>The connector ends, in document order.</summary>
        private readonly List<HintConnectorEnd> _ends = [];

        /// <summary>The head candidates, in document order.</summary>
        private readonly List<HintHead> _heads = [];

        /// <summary>Marks an element as analyzed.</summary>
        /// <param name="element">The element.</param>
        /// <returns><see langword="true"/> the first time; <see langword="false"/> when already analyzed or over the caps.</returns>
        public bool TryVisit(XElement element) =>
            (_ends.Count < MaxHintConnectorEnds || _heads.Count < MaxHintHeads) && _visited.Add(element);

        /// <summary>Records a connector end, up to <see cref="MaxHintConnectorEnds"/>.</summary>
        /// <param name="end">The end.</param>
        public void AddConnectorEnd(HintConnectorEnd end)
        {
            if (_ends.Count < MaxHintConnectorEnds)
            {
                _ends.Add(end);
            }
        }

        /// <summary>Records a head candidate, up to <see cref="MaxHintHeads"/>.</summary>
        /// <param name="head">The head.</param>
        public void AddHead(HintHead head)
        {
            if (_heads.Count < MaxHintHeads)
            {
                _heads.Add(head);
            }
        }

        /// <summary>Calls the handler once when at least one head sits on a connector end.</summary>
        public void Report()
        {
            var matched = _heads.Where(head => _ends.Any(end => SitsOnEnd(head, end))).ToList();
            if (matched.Count == 0)
            {
                return;
            }

            var count = matched.Count;
            var found = matched[0];

            var id = (string?)found.Element.Attribute("id");
            var name = id == null ? $"<{found.Element.Name.LocalName}>" : $"<{found.Element.Name.LocalName} id=\"{id}\">";
            var x = MathF.Round(found.Centroid.X).ToString(CultureInfo.InvariantCulture);
            var y = MathF.Round(found.Centroid.Y).ToString(CultureInfo.InvariantCulture);
            var shapes = count == 1 ? "1 filled shape looks" : $"{count.ToString(CultureInfo.InvariantCulture)} filled shapes look";
            handler(new SvgHint
            {
                Code = SvgHint.ArrowheadShapeCode,
                Count = count,
                Message = $"{shapes} like a hand-drawn arrowhead at an end of a connector (first: {name} at pixel {x},{y}); " +
                          "draw arrowheads with a <marker> so they follow the connector: on marker-end with orient=\"auto\" " +
                          "for the end, or on marker-start with orient=\"auto-start-reverse\" for the start."
            });
        }

        /// <summary>Returns whether a head sits on a connector end at a size that fits the connector.</summary>
        /// <param name="head">The head candidate.</param>
        /// <param name="end">The connector end.</param>
        /// <returns><see langword="true"/> for a match.</returns>
        private static bool SitsOnEnd(HintHead head, HintConnectorEnd end)
        {
            if (ReferenceEquals(head.Element, end.Element) ||
                head.LongestEdge < MinHeadToStrokeRatio * end.StrokeWidth ||
                head.LongestEdge > MaxHeadToStrokeRatio * end.StrokeWidth)
            {
                return false;
            }

            // A head's centroid lies back along the connector from its tip; a centroid on the end itself marks a
            // centered data-point symbol
            var distance = Vector2.Distance(head.Centroid, end.Point);
            return distance > CenteredHeadRatio * head.LongestEdge && distance <= head.LongestEdge;
        }
    }
}
