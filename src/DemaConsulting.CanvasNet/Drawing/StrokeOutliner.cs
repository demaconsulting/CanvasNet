using System.Numerics;

// cspell:ignore Outliner outliner underflows underflowed

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Converts a flattened polyline into one or more closed outline polygons representing the
///     area covered by stroking that polyline.
/// </summary>
/// <remarks>
///     The outliner performs purely geometric conversion: it offsets each segment by half the
///     stroke width, resolves joins and caps into ordinary polygon vertices, and leaves
///     rasterization to <see cref="PathFiller"/>. This keeps the stroke feature on the existing
///     fill code path rather than introducing a separate rasterizer.
/// </remarks>
internal static class StrokeOutliner
{
    /// <summary>
    ///     The displacement below which vectors are treated as degenerate for join/cap math.
    /// </summary>
    private const float NearZeroDistance = 1e-6f;

    /// <summary>
    ///     Converts one flattened path segment into its closed outline polygon(s).
    /// </summary>
    /// <param name="points">The flattened polyline vertices.</param>
    /// <param name="isClosed">Whether the polyline is a closed contour.</param>
    /// <param name="style">The stroke geometry to apply.</param>
    /// <param name="flattenTolerance">The tolerance used when tessellating round arcs.</param>
    /// <returns>One or more closed polygons representing the stroke area.</returns>
    internal static List<List<Vector2>> Outline(
        List<Vector2> points,
        bool isClosed,
        StrokeStyle style,
        float flattenTolerance)
    {
        var simplified = SimplifyPoints(points, isClosed);
        if (simplified.Count == 0)
        {
            return [];
        }

        var halfWidth = style.Width / 2f;
        if (simplified.Count == 1)
        {
            return CreatePointStrokePolygons(simplified[0], style.Cap, halfWidth, flattenTolerance);
        }

        if (isClosed && simplified.Count == 2)
        {
            // A closed contour needs at least three distinct vertices to enclose any area; with
            // exactly two, the "outer" and "inner" offset rings built by CreateClosedStrokePolygons
            // would be coincident (zero signed area) and then forced into opposite winding, which
            // makes FillRule.NonZero cancel the whole band to nothing. Instead, treat this
            // degenerate closed contour exactly as an equivalent open single-segment stroke would
            // be outlined (with caps at both ends per style.Cap) - the only well-defined non-empty
            // rendering of a closed path that immediately doubles back over the same segment.
            return CreateOpenStrokePolygons(simplified, style, halfWidth, flattenTolerance);
        }

        return isClosed
            ? CreateClosedStrokePolygons(simplified, style, halfWidth, flattenTolerance)
            : CreateOpenStrokePolygons(simplified, style, halfWidth, flattenTolerance);
    }

    /// <summary>
    ///     Removes redundant duplicate vertices while preserving the semantic distinction between a
    ///     single-point path and a real segment chain.
    /// </summary>
    private static List<Vector2> SimplifyPoints(IReadOnlyList<Vector2> points, bool isClosed)
    {
        var simplified = new List<Vector2>(points.Count);
        foreach (var point in points)
        {
            if (simplified.Count == 0 || simplified[^1] != point)
            {
                simplified.Add(point);
            }
        }

        if (isClosed && simplified.Count > 1 && simplified[0] == simplified[^1])
        {
            simplified.RemoveAt(simplified.Count - 1);
        }

        return simplified;
    }

    /// <summary>
    ///     Produces the special-case stroke geometry for a degenerate subpath reduced to one
    ///     point.
    /// </summary>
    private static List<List<Vector2>> CreatePointStrokePolygons(
        Vector2 center,
        LineCap cap,
        float halfWidth,
        float flattenTolerance)
    {
        switch (cap)
        {
            case LineCap.Round:
                return [CreateCirclePolygon(center, halfWidth, flattenTolerance)];

            case LineCap.Square:
                return
                [
                    new List<Vector2>
                    {
                        new(center.X - halfWidth, center.Y - halfWidth),
                        new(center.X + halfWidth, center.Y - halfWidth),
                        new(center.X + halfWidth, center.Y + halfWidth),
                        new(center.X - halfWidth, center.Y + halfWidth)
                    }
                ];

            case LineCap.Butt:
            default:
                return [];
        }
    }

    /// <summary>
    ///     Produces the stroke polygon for an open polyline, including caps.
    /// </summary>
    private static List<List<Vector2>> CreateOpenStrokePolygons(
        IReadOnlyList<Vector2> points,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        var frames = BuildSegmentFrames(points, isClosed: false);
        var leftSide = BuildOpenSide(points, frames, +1f, style, halfWidth, flattenTolerance);
        var rightSide = BuildOpenSide(points, frames, -1f, style, halfWidth, flattenTolerance);
        if (leftSide.Count == 0 || rightSide.Count == 0)
        {
            return [];
        }

        var polygon = new List<Vector2>(leftSide.Count + rightSide.Count + 32);
        foreach (var point in leftSide)
        {
            AddPointIfDistinct(polygon, point);
        }

        if (style.Cap == LineCap.Round)
        {
            var end = points[^1];
            var tangent = frames[^1].Tangent;
            var leftEnd = points[^1] + frames[^1].Normal * halfWidth;
            var rightEnd = points[^1] - frames[^1].Normal * halfWidth;
            AppendArcThrough(
                polygon,
                end,
                leftEnd - end,
                rightEnd - end,
                tangent * halfWidth,
                halfWidth,
                flattenTolerance,
                includeStart: false,
                includeEnd: true);

            AppendReversed(rightSide, polygon, skipFirst: true);
        }
        else
        {
            AppendReversed(rightSide, polygon, skipFirst: false);
        }

        if (style.Cap == LineCap.Round)
        {
            var start = points[0];
            var tangent = frames[0].Tangent;
            var leftStart = points[0] + frames[0].Normal * halfWidth;
            var rightStart = points[0] - frames[0].Normal * halfWidth;
            AppendArcThrough(
                polygon,
                start,
                rightStart - start,
                leftStart - start,
                -tangent * halfWidth,
                halfWidth,
                flattenTolerance,
                includeStart: false,
                includeEnd: false);
        }

        RemoveTrailingDuplicateOfFirst(polygon);
        return polygon.Count >= 3 ? [polygon] : [];
    }

    /// <summary>
    ///     Produces the outer and inner shell rings for a closed contour.
    /// </summary>
    /// <remarks>
    ///     The outer and inner offset rings are NOT symmetric for a closed contour: the outer
    ///     ring is the outside boundary of the stroke band, where every vertex is a convex corner
    ///     that the caller's requested <see cref="LineJoin"/> (Miter/Round/Bevel) legitimately
    ///     stylizes. The inner ring is the boundary of the hole left by the union of the two
    ///     offset strips - at that same vertex, the inner side is the geometrically exact
    ///     intersection of the two offset edges, not a stylized corner. Applying Bevel or Round to
    ///     the inner side would chamfer/round away real stroke area (or, if the geometry required
    ///     it, add extraneous area) at every inner corner, producing a hole boundary with the
    ///     wrong shape. <see cref="BuildClosedSide"/> is therefore called once per ring with an
    ///     explicit outer/inner flag so only the outer ring honors <see cref="StrokeStyle.Join"/>.
    /// </remarks>
    private static List<List<Vector2>> CreateClosedStrokePolygons(
        IReadOnlyList<Vector2> points,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        var area = ComputeSignedArea(points);
        var outerSideSign = area >= 0f ? -1f : +1f;

        var outerRing = BuildClosedSide(points, outerSideSign, style, halfWidth, flattenTolerance, isOuterSide: true);
        var innerRing = BuildClosedSide(points, -outerSideSign, style, halfWidth, flattenTolerance, isOuterSide: false);
        if (outerRing.Count < 3 || innerRing.Count < 3)
        {
            return [];
        }

        if (ComputeSignedArea(outerRing) * ComputeSignedArea(innerRing) > 0f)
        {
            innerRing.Reverse();
        }

        return [outerRing, innerRing];
    }

    /// <summary>
    ///     Computes one tangent/normal pair per segment of an open or closed polyline.
    /// </summary>
    private static (Vector2 Tangent, Vector2 Normal)[] BuildSegmentFrames(IReadOnlyList<Vector2> points, bool isClosed)
    {
        var segmentCount = isClosed ? points.Count : points.Count - 1;
        var frames = new (Vector2 Tangent, Vector2 Normal)[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            var direction = end - start;
            var length = direction.Length();
            if (length <= NearZeroDistance)
            {
                frames[i] = (Vector2.UnitX, new Vector2(0f, 1f));
                continue;
            }

            var tangent = direction / length;
            frames[i] = (tangent, new Vector2(-tangent.Y, tangent.X));
        }

        return frames;
    }

    /// <summary>
    ///     Builds one side of an open stroke polygon from start to end.
    /// </summary>
    private static List<Vector2> BuildOpenSide(
        IReadOnlyList<Vector2> points,
        IReadOnlyList<(Vector2 Tangent, Vector2 Normal)> frames,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        var side = new List<Vector2>(points.Count * 2);
        AddPointIfDistinct(side, GetOpenEndpoint(points[0], frames[0].Tangent, frames[0].Normal, sideSign, style.Cap, -1f, halfWidth));

        for (var i = 1; i < points.Count - 1; i++)
        {
            AppendOpenJoin(side, points[i], frames[i - 1], frames[i], sideSign, style, halfWidth, flattenTolerance);
        }

        AddPointIfDistinct(side, GetOpenEndpoint(points[^1], frames[^1].Tangent, frames[^1].Normal, sideSign, style.Cap, +1f, halfWidth));
        return side;
    }

    /// <summary>
    ///     Computes the start or end point of an open-side offset, including square-cap
    ///     extensions.
    /// </summary>
    private static Vector2 GetOpenEndpoint(
        Vector2 point,
        Vector2 tangent,
        Vector2 normal,
        float sideSign,
        LineCap cap,
        float capDirection,
        float halfWidth)
    {
        var offset = sideSign * normal * halfWidth;
        if (cap == LineCap.Square)
        {
            offset += capDirection * tangent * halfWidth;
        }

        return point + offset;
    }

    /// <summary>
    ///     Appends the join geometry for one interior vertex of an open stroke side.
    /// </summary>
    private static void AppendOpenJoin(
        List<Vector2> side,
        Vector2 vertex,
        (Vector2 Tangent, Vector2 Normal) previousFrame,
        (Vector2 Tangent, Vector2 Normal) nextFrame,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        var previousPoint = vertex + sideSign * previousFrame.Normal * halfWidth;
        var nextPoint = vertex + sideSign * nextFrame.Normal * halfWidth;
        var turn = Cross(previousFrame.Tangent, nextFrame.Tangent);
        var dot = Vector2.Dot(previousFrame.Tangent, nextFrame.Tangent);
        if (MathF.Abs(turn) <= NearZeroDistance && dot > 0f)
        {
            AddPointIfDistinct(side, nextPoint);
            return;
        }

        var isConvexOnThisSide = turn * sideSign < 0f;
        if (!isConvexOnThisSide)
        {
            AddPointIfDistinct(side, previousPoint);
            AddPointIfDistinct(side, nextPoint);
            return;
        }

        AppendStyledJoin(
            side,
            vertex,
            previousFrame.Tangent,
            nextFrame.Tangent,
            previousFrame.Normal,
            nextFrame.Normal,
            sideSign,
            style,
            halfWidth,
            flattenTolerance);
    }

    /// <summary>
    ///     Builds one closed offset ring for a closed contour.
    /// </summary>
    /// <param name="points">The flattened, simplified closed-contour vertices.</param>
    /// <param name="sideSign">The offset direction (+1/-1) for this ring relative to the normals.</param>
    /// <param name="style">The stroke geometry to apply.</param>
    /// <param name="halfWidth">Half the stroke width.</param>
    /// <param name="flattenTolerance">The tolerance used when tessellating round arcs.</param>
    /// <param name="isOuterSide">
    ///     Whether this ring is the outer (outside boundary) side of the stroke band. When
    ///     <see langword="false"/> (the inner/hole side), every vertex is forced to the exact
    ///     offset-edge intersection regardless of <see cref="StrokeStyle.Join"/> - see the remarks
    ///     on <see cref="CreateClosedStrokePolygons"/> for why the two sides cannot share styling.
    /// </param>
    private static List<Vector2> BuildClosedSide(
        IReadOnlyList<Vector2> points,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance,
        bool isOuterSide)
    {
        var frames = BuildSegmentFrames(points, isClosed: true);
        var ring = new List<Vector2>(points.Count * 2);
        for (var i = 0; i < points.Count; i++)
        {
            var previousFrame = frames[(i + frames.Length - 1) % frames.Length];
            var nextFrame = frames[i];
            var turn = Cross(previousFrame.Tangent, nextFrame.Tangent);
            var dot = Vector2.Dot(previousFrame.Tangent, nextFrame.Tangent);
            if (MathF.Abs(turn) <= NearZeroDistance && dot > 0f)
            {
                AddPointIfDistinct(ring, points[i] + sideSign * nextFrame.Normal * halfWidth);
                continue;
            }

            AppendStyledJoin(
                ring,
                points[i],
                previousFrame.Tangent,
                nextFrame.Tangent,
                previousFrame.Normal,
                nextFrame.Normal,
                sideSign,
                style,
                halfWidth,
                flattenTolerance,
                forceExactIntersection: !isOuterSide);
        }

        RemoveTrailingDuplicateOfFirst(ring);
        return ring;
    }

    /// <summary>
    ///     Appends the requested styled join between two offset segments.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="vertex"></param>
    /// <param name="previousTangent"></param>
    /// <param name="nextTangent"></param>
    /// <param name="previousNormal"></param>
    /// <param name="nextNormal"></param>
    /// <param name="sideSign"></param>
    /// <param name="style"></param>
    /// <param name="halfWidth"></param>
    /// <param name="flattenTolerance"></param>
    /// <param name="forceExactIntersection">
    ///     When <see langword="true"/>, ignores <see cref="StrokeStyle.Join"/> and always emits
    ///     the geometrically exact intersection of the two offset edges (falling back to the
    ///     un-joined offset points only when the edges are parallel and have no intersection).
    ///     This is required for the inner/hole ring of a closed contour, where the correct corner
    ///     is always the exact intersection point, never a stylized miter/round/bevel corner - see
    ///     the remarks on <see cref="CreateClosedStrokePolygons"/>.
    /// </param>
    private static void AppendStyledJoin(
        List<Vector2> target,
        Vector2 vertex,
        Vector2 previousTangent,
        Vector2 nextTangent,
        Vector2 previousNormal,
        Vector2 nextNormal,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance,
        bool forceExactIntersection = false)
    {
        var previousPoint = vertex + sideSign * previousNormal * halfWidth;
        var nextPoint = vertex + sideSign * nextNormal * halfWidth;

        if (forceExactIntersection)
        {
            if (TryIntersectLines(previousPoint, previousTangent, nextPoint, nextTangent, out var intersection))
            {
                AddPointIfDistinct(target, intersection);
            }
            else
            {
                AddPointIfDistinct(target, previousPoint);
                AddPointIfDistinct(target, nextPoint);
            }

            return;
        }

        switch (style.Join)
        {
            case LineJoin.Round:
                AddPointIfDistinct(target, previousPoint);
                AppendArcShortest(
                    target,
                    vertex,
                    previousPoint - vertex,
                    nextPoint - vertex,
                    halfWidth,
                    flattenTolerance,
                    includeStart: false,
                    includeEnd: true);
                break;

            case LineJoin.Bevel:
                AddPointIfDistinct(target, previousPoint);
                AddPointIfDistinct(target, nextPoint);
                break;

            default:
                if (TryCreateMiter(vertex, previousPoint, nextPoint, previousTangent, nextTangent, style.Width, style.MiterLimit, out var miter))
                {
                    AddPointIfDistinct(target, miter);
                }
                else
                {
                    AddPointIfDistinct(target, previousPoint);
                    AddPointIfDistinct(target, nextPoint);
                }

                break;
        }
    }

    /// <summary>
    ///     Computes the miter-join vertex and validates it against the configured limit.
    /// </summary>
    private static bool TryCreateMiter(
        Vector2 vertex,
        Vector2 previousPoint,
        Vector2 nextPoint,
        Vector2 previousTangent,
        Vector2 nextTangent,
        float strokeWidth,
        float miterLimit,
        out Vector2 miterPoint)
    {
        miterPoint = default;
        if (!TryIntersectLines(previousPoint, previousTangent, nextPoint, nextTangent, out var intersection))
        {
            return false;
        }

        var halfWidth = strokeWidth / 2f;
        var miterRatio = Vector2.Distance(vertex, intersection) / halfWidth;
        if (!float.IsFinite(miterRatio) || miterRatio > miterLimit)
        {
            return false;
        }

        miterPoint = intersection;
        return true;
    }

    /// <summary>
    ///     Intersects two infinite lines defined by point-plus-direction pairs.
    /// </summary>
    private static bool TryIntersectLines(
        Vector2 pointA,
        Vector2 directionA,
        Vector2 pointB,
        Vector2 directionB,
        out Vector2 intersection)
    {
        var denominator = Cross(directionA, directionB);
        if (MathF.Abs(denominator) <= NearZeroDistance)
        {
            intersection = default;
            return false;
        }

        var delta = pointB - pointA;
        var t = Cross(delta, directionB) / denominator;
        intersection = pointA + directionA * t;
        return true;
    }

    /// <summary>
    ///     Appends the shorter arc between two vectors around a common center.
    /// </summary>
    private static void AppendArcShortest(
        List<Vector2> target,
        Vector2 center,
        Vector2 startVector,
        Vector2 endVector,
        float radius,
        float flattenTolerance,
        bool includeStart,
        bool includeEnd)
    {
        var startAngle = MathF.Atan2(startVector.Y, startVector.X);
        var endAngle = MathF.Atan2(endVector.Y, endVector.X);
        var sweep = NormalizeSignedAngle(endAngle - startAngle);
        AppendArc(target, center, startAngle, sweep, radius, flattenTolerance, includeStart, includeEnd);
    }

    /// <summary>
    ///     Appends the arc from <paramref name="startVector"/> to <paramref name="endVector"/>
    ///     chosen so that it passes through <paramref name="throughVector"/>.
    /// </summary>
    private static void AppendArcThrough(
        List<Vector2> target,
        Vector2 center,
        Vector2 startVector,
        Vector2 endVector,
        Vector2 throughVector,
        float radius,
        float flattenTolerance,
        bool includeStart,
        bool includeEnd)
    {
        var startAngle = MathF.Atan2(startVector.Y, startVector.X);
        var endAngle = MathF.Atan2(endVector.Y, endVector.X);
        var sweep = NormalizeSignedAngle(endAngle - startAngle);
        var throughAngle = MathF.Atan2(throughVector.Y, throughVector.X);
        if (!AngleLiesOnSweep(startAngle, sweep, throughAngle))
        {
            sweep = sweep > 0f ? sweep - 2f * MathF.PI : sweep + 2f * MathF.PI;
        }

        AppendArc(target, center, startAngle, sweep, radius, flattenTolerance, includeStart, includeEnd);
    }

    /// <summary>
    ///     Appends a tessellated circular arc.
    /// </summary>
    private static void AppendArc(
        List<Vector2> target,
        Vector2 center,
        float startAngle,
        float sweep,
        float radius,
        float flattenTolerance,
        bool includeStart,
        bool includeEnd)
    {
        var segmentCount = GetArcSegmentCount(radius, MathF.Abs(sweep), flattenTolerance);
        for (var i = 0; i <= segmentCount; i++)
        {
            if (i == 0 && !includeStart)
            {
                continue;
            }

            if (i == segmentCount && !includeEnd)
            {
                continue;
            }

            var angle = startAngle + sweep * i / segmentCount;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            AddPointIfDistinct(target, point);
        }
    }

    /// <summary>
    ///     The angle (in radians) swept by a single segment when falling back to the
    ///     angle-based minimum segment count heuristic (see <see cref="GetArcSegmentCount"/>).
    /// </summary>
    private const float FallbackSegmentAngle = MathF.PI / 2f;

    /// <summary>
    ///     Computes the number of straight segments needed to keep an arc's sagitta within the
    ///     requested flattening tolerance.
    /// </summary>
    /// <remarks>
    ///     At sufficiently large <paramref name="radius"/> (or sufficiently small
    ///     <paramref name="flattenTolerance"/>), <c>flattenTolerance / radius</c> underflows
    ///     float32 precision (below ~1.19e-7), which would make <c>1f - flattenTolerance / radius</c>
    ///     round to exactly <c>1.0f</c> and <see cref="MathF.Acos(float)"/> return <c>0</c>. Rather
    ///     than let that precision loss silently collapse the arc to a single straight chord, this
    ///     method falls back to a conservative angle-based minimum segment count (one segment per
    ///     <see cref="FallbackSegmentAngle"/> radians of sweep) so round joins/caps always produce a
    ///     genuinely curved outline for any valid, finite radius and sweep. This mirrors the
    ///     established convention (see
    ///     <see cref="DemaConsulting.CanvasNet.Geometry.BezierFlattening"/>'s
    ///     <c>MaxRecursionDepth</c>) of explicitly bounding pathological-input behavior instead of
    ///     degrading silently.
    /// </remarks>
    private static int GetArcSegmentCount(float radius, float sweepMagnitude, float flattenTolerance)
    {
        if (radius <= 0f || sweepMagnitude <= 0f)
        {
            return 1;
        }

        var fallbackCount = Math.Max(1, (int)MathF.Ceiling(sweepMagnitude / FallbackSegmentAngle));

        if (flattenTolerance >= radius)
        {
            return fallbackCount;
        }

        var cosine = 1f - flattenTolerance / radius;
        cosine = Math.Clamp(cosine, -1f, 1f);
        var maxAngle = 2f * MathF.Acos(cosine);
        if (!float.IsFinite(maxAngle) || maxAngle <= 0f)
        {
            // flattenTolerance / radius underflowed to (effectively) zero: the tolerance-based
            // computation cannot distinguish this arc from a full circle, so fall back to the
            // angle-based heuristic instead of returning 1 (a single straight chord). fallbackCount
            // itself is exactly 1 for any sweep of at most FallbackSegmentAngle (PI/2, i.e. 90
            // degrees), which would silently collapse right back into the same single straight
            // chord this fallback exists to eliminate; clamp to a minimum of 2 segments so every
            // positive sweep - however small - still produces a genuinely curved (multi-segment)
            // outline.
            return Math.Max(2, fallbackCount);
        }

        return Math.Max(fallbackCount, (int)MathF.Ceiling(sweepMagnitude / maxAngle));
    }

    /// <summary>
    ///     Creates a full-circle polygon centered on <paramref name="center"/>.
    /// </summary>
    private static List<Vector2> CreateCirclePolygon(Vector2 center, float radius, float flattenTolerance)
    {
        var circle = new List<Vector2>();
        var segmentCount = Math.Max(4, GetArcSegmentCount(radius, 2f * MathF.PI, flattenTolerance));
        for (var i = 0; i < segmentCount; i++)
        {
            var angle = 2f * MathF.PI * i / segmentCount;
            circle.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        return circle;
    }

    /// <summary>
    ///     Appends <paramref name="source"/> in reverse order to <paramref name="target"/>.
    /// </summary>
    private static void AppendReversed(IReadOnlyList<Vector2> source, List<Vector2> target, bool skipFirst)
    {
        var endIndex = skipFirst ? source.Count - 2 : source.Count - 1;
        for (var i = endIndex; i >= 0; i--)
        {
            AddPointIfDistinct(target, source[i]);
        }
    }

    /// <summary>
    ///     Removes a trailing duplicate of the first vertex when a helper already closed the loop
    ///     explicitly.
    /// </summary>
    private static void RemoveTrailingDuplicateOfFirst(List<Vector2> points)
    {
        if (points.Count > 1 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }
    }

    /// <summary>
    ///     Appends <paramref name="point"/> unless it duplicates the current final point.
    /// </summary>
    private static void AddPointIfDistinct(List<Vector2> points, Vector2 point)
    {
        if (points.Count == 0 || points[^1] != point)
        {
            points.Add(point);
        }
    }

    /// <summary>
    ///     Computes the scalar 2D cross product.
    /// </summary>
    private static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    /// <summary>
    ///     Normalizes an angle into <c>[-π, π]</c>.
    /// </summary>
    private static float NormalizeSignedAngle(float angle)
    {
        while (angle <= -MathF.PI)
        {
            angle += 2f * MathF.PI;
        }

        while (angle > MathF.PI)
        {
            angle -= 2f * MathF.PI;
        }

        return angle;
    }

    /// <summary>
    ///     Determines whether <paramref name="angle"/> lies on the directed sweep starting at
    ///     <paramref name="startAngle"/> and spanning <paramref name="sweep"/>.
    /// </summary>
    private static bool AngleLiesOnSweep(float startAngle, float sweep, float angle)
    {
        if (sweep >= 0f)
        {
            var delta = NormalizePositiveAngle(angle - startAngle);
            var normalizedSweep = NormalizePositiveAngle(sweep);
            return delta <= normalizedSweep + NearZeroDistance;
        }

        var negativeDelta = NormalizePositiveAngle(startAngle - angle);
        var normalizedNegativeSweep = NormalizePositiveAngle(-sweep);
        return negativeDelta <= normalizedNegativeSweep + NearZeroDistance;
    }

    /// <summary>
    ///     Normalizes an angle into <c>[0, 2π)</c>.
    /// </summary>
    private static float NormalizePositiveAngle(float angle)
    {
        var normalized = angle % (2f * MathF.PI);
        if (normalized < 0f)
        {
            normalized += 2f * MathF.PI;
        }

        return normalized;
    }

    /// <summary>
    ///     Computes the signed area of a polygon-like vertex sequence.
    /// </summary>
    private static float ComputeSignedArea(IReadOnlyList<Vector2> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += current.X * next.Y - current.Y * next.X;
        }

        return area / 2f;
    }
}
