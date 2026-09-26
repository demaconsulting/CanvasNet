using System.Linq;
using System.Numerics;

// cspell:ignore Outliner outliner underflows underflowed inradius

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
    ///     The offsetting parameters shared by every join built for one side (<see cref="SideSign"/>
    ///     of <c>+1</c>/<c>-1</c>) of a stroked polyline: which <see cref="StrokeStyle"/> to honor,
    ///     how far to offset (<see cref="HalfWidth"/>), and how finely to tessellate any round
    ///     join/cap arcs (<see cref="FlattenTolerance"/>).
    /// </summary>
    /// <remarks>
    ///     Bundles the four arguments that <see cref="AppendOpenJoin"/>, <see cref="AppendStyledJoin"/>,
    ///     and their <see cref="BuildOpenSide"/>/<see cref="BuildClosedSide"/> callers always pass
    ///     together, unchanged, for the entire side of a polyline being offset.
    /// </remarks>
    private readonly record struct StrokeSideGeometry(float SideSign, StrokeStyle Style, float HalfWidth, float FlattenTolerance);

    /// <summary>
    ///     The tessellation parameters shared by every arc-appending helper: the arc's
    ///     <see cref="Radius"/>, the flattening <see cref="FlattenTolerance"/> bounding each
    ///     segment's sagitta, and whether the arc's start/end point should itself be emitted
    ///     (<see cref="IncludeStart"/>/<see cref="IncludeEnd"/>) or left for the caller to add.
    /// </summary>
    /// <remarks>
    ///     Bundles the four arguments <see cref="AppendArcShortest"/>, <see cref="AppendArcThrough"/>,
    ///     and <see cref="AppendArc"/> all share identically - only the arc's angular span itself
    ///     differs between the three.
    /// </remarks>
    private readonly record struct ArcTessellation(float Radius, float FlattenTolerance, bool IncludeStart, bool IncludeEnd);

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

        if (isClosed && IsDegenerateClosedContour(simplified))
        {
            // A closed contour needs at least three NON-COLLINEAR distinct vertices to enclose
            // any area. With fewer than three distinct vertices, or with any number of distinct
            // vertices that all lie on one line (e.g. a 3+ point closed contour that traces back
            // and forth along a single line), the "outer" and "inner" offset rings built by
            // CreateClosedStrokePolygons would have zero signed area and then be forced into
            // opposite winding, which makes FillRule.NonZero cancel the whole band to nothing.
            // Instead, treat this degenerate closed contour exactly as an equivalent open
            // multi-segment stroke would be outlined (with caps at both ends per style.Cap) - the
            // only well-defined non-empty rendering of a closed path that encloses no area.
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
        // Tracks the last point that survived the filter below, so each candidate point is kept
        // only when it differs from the previous KEPT point - not merely the previous SOURCE
        // point - reproducing the original loop's "simplified[^1] != point" comparison exactly.
        Vector2? lastKept = null;
        var simplified = points.Where(point =>
        {
            if (lastKept == point)
            {
                return false;
            }

            lastKept = point;
            return true;
        }).ToList();

        if (isClosed && simplified.Count > 1 && simplified[0] == simplified[^1])
        {
            simplified.RemoveAt(simplified.Count - 1);
        }

        return simplified;
    }

    /// <summary>
    ///     Determines whether a closed contour encloses no area: either it has fewer than three
    ///     distinct vertices, or every distinct vertex lies on a single line.
    /// </summary>
    /// <remarks>
    ///     This generalizes the original "exactly two distinct points" special case: a closed
    ///     contour with three or more distinct but COLLINEAR points (e.g. tracing
    ///     <c>(0,0)-&gt;(1,0)-&gt;(2,0)-&gt;Close</c>) is just as degenerate - it is a zero-area
    ///     band that would otherwise fall through to <see cref="CreateClosedStrokePolygons"/> and
    ///     produce two coincident-band rings of opposite winding that cancel completely under
    ///     <see cref="FillRule.NonZero"/>, even though a stroked degenerate line should still
    ///     render as a visible stroke.
    /// </remarks>
    private static bool IsDegenerateClosedContour(IReadOnlyList<Vector2> points) =>
        points.Count < 3 || AreAllPointsCollinear(points);

    /// <summary>
    ///     Determines whether every point in <paramref name="points"/> lies on a single line.
    /// </summary>
    /// <remarks>
    ///     Picks the first non-degenerate direction from <c>points[0]</c> to any later point (so
    ///     duplicate/near-duplicate leading points do not defeat the check), normalizes it, and
    ///     then confirms every remaining point's perpendicular distance from that line is within
    ///     <see cref="NearZeroDistance"/>. If every point coincides with <c>points[0]</c> (no
    ///     usable direction exists), the contour is fully collapsed and therefore trivially
    ///     collinear.
    ///     <para>
    ///     Every vertex difference, squared-length, normalization, and cross-product used here is
    ///     computed in <see langword="double"/> precision rather than via <see cref="Vector2"/>
    ///     arithmetic and <see cref="Vector2.LengthSquared()"/>/<see cref="Vector2.Length()"/>:
    ///     for a contour spanning near-extreme float32 coordinates (e.g. vertices near
    ///     <c>±3e38</c>), the float32 <c>dx*dx + dy*dy</c> computation in
    ///     <c>candidate.LengthSquared()</c> overflows to <see cref="float.PositiveInfinity"/> well
    ///     before the true squared length would, and normalizing an overflowed vector produces
    ///     <see cref="float.NaN"/> components. Because a NaN comparison is always false, that
    ///     would make every point spuriously pass the perpendicular-distance check below and
    ///     misclassify a legitimately non-collinear large contour as degenerate. This is the same
    ///     class of float32-overflow bug already fixed for edge-length computations in
    ///     <see cref="BuildSegmentFrames"/> and <see cref="DashSplitter.BuildCumulativeLengths"/>.
    ///     </para>
    /// </remarks>
    private static bool AreAllPointsCollinear(IReadOnlyList<Vector2> points)
    {
        if (points.Count <= 2)
        {
            return true;
        }

        var origin = points[0];
        var directionX = 0.0;
        var directionY = 0.0;
        var haveDirection = false;
        const double nearZeroDistanceSquared = (double)NearZeroDistance * NearZeroDistance;
        for (var i = 1; i < points.Count; i++)
        {
            var candidateX = (double)points[i].X - origin.X;
            var candidateY = (double)points[i].Y - origin.Y;
            if (candidateX * candidateX + candidateY * candidateY > nearZeroDistanceSquared)
            {
                directionX = candidateX;
                directionY = candidateY;
                haveDirection = true;
                break;
            }
        }

        if (!haveDirection)
        {
            return true;
        }

        var directionLength = Math.Sqrt(directionX * directionX + directionY * directionY);
        var normalizedDirectionX = directionX / directionLength;
        var normalizedDirectionY = directionY / directionLength;
        for (var i = 1; i < points.Count; i++)
        {
            var offsetX = (double)points[i].X - origin.X;
            var offsetY = (double)points[i].Y - origin.Y;
            var perpendicularDistance = normalizedDirectionX * offsetY - normalizedDirectionY * offsetX;
            if (Math.Abs(perpendicularDistance) > NearZeroDistance)
            {
                return false;
            }
        }

        return true;
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
                var circle = CreateCirclePolygon(center, halfWidth, flattenTolerance);
                NormalizeOuterWinding(circle);
                return [circle];

            case LineCap.Square:
                var square = new List<Vector2>
                {
                    new(center.X - halfWidth, center.Y - halfWidth),
                    new(center.X + halfWidth, center.Y - halfWidth),
                    new(center.X + halfWidth, center.Y + halfWidth),
                    new(center.X - halfWidth, center.Y + halfWidth)
                };
                NormalizeOuterWinding(square);
                return [square];

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
                new ArcTessellation(halfWidth, flattenTolerance, IncludeStart: false, IncludeEnd: true));

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
                new ArcTessellation(halfWidth, flattenTolerance, IncludeStart: false, IncludeEnd: false));
        }

        RemoveTrailingDuplicateOfFirst(polygon);
        if (polygon.Count < 3)
        {
            return [];
        }

        NormalizeOuterWinding(polygon);
        return [polygon];
    }

    /// <summary>
    ///     Produces the outer and inner shell rings for a closed contour.
    /// </summary>
    /// <remarks>
    ///     A closed contour's two offset rings are NOT symmetric, but which ring is "outer" vs
    ///     "inner" at a given vertex is a LOCAL property, not a fixed global one: for a convex
    ///     closed contour every vertex has the same ring as the convex (outer) side, but for a
    ///     concave (reflex) contour the locally convex side can flip between the two rings from
    ///     vertex to vertex. Treating one ring as globally "outer" (as the open-path code already
    ///     avoids doing, see <see cref="AppendOpenJoin"/>) would apply the caller's requested
    ///     <see cref="LineJoin"/> (Miter/Round/Bevel) to a locally concave vertex - chamfering or
    ///     rounding away real stroke area, or adding extraneous area - and would force the exact
    ///     offset-edge intersection onto a locally convex vertex, losing the requested join style
    ///     there. <see cref="BuildClosedSide"/> therefore decides, independently at every vertex
    ///     and from the same local turn-direction sign used by the open-path join logic, whether
    ///     that vertex on that ring is the locally convex side (style the join) or the locally
    ///     concave side (force the exact intersection). Because the two rings use opposite side
    ///     signs, exactly one of them is locally convex at any given vertex, so the styled join and
    ///     the exact intersection are always applied to the correct ring at that vertex, even when
    ///     convexity flips along the contour.
    /// </remarks>
    private static List<List<Vector2>> CreateClosedStrokePolygons(
        IReadOnlyList<Vector2> points,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        // Defense in depth: the dispatch in Outline() already routes degenerate (collinear or
        // fewer-than-three-distinct-vertex) closed contours to CreateOpenStrokePolygons before
        // ever reaching here, but re-checking here means this method can never itself produce the
        // coincident-band, opposite-winding rings that FillRule.NonZero cancels to nothing, even
        // if some future caller invokes it directly without going through that dispatch.
        if (IsDegenerateClosedContour(points))
        {
            return CreateOpenStrokePolygons(points, style, halfWidth, flattenTolerance);
        }

        var area = ComputeSignedArea(points);
        var outerSideSign = area >= 0.0 ? -1f : +1f;

        var outerRing = BuildClosedSide(points, outerSideSign, style, halfWidth, flattenTolerance, out _);
        if (outerRing.Count < 3)
        {
            return [];
        }

        // Normalize the outer ring's own winding to the same fixed direction used for every other
        // independently-emitted outer outline (open-line outlines, point-cap circles/squares - see
        // NormalizeOuterWinding), regardless of the source contour's authored orientation. Without
        // this, a source contour authored in reversed order flips outerSideSign's resulting
        // winding to follow it, so two overlapping stroked elements from the same Stroke call can
        // end up with opposite signed winding under FillRule.NonZero and cancel to a hole in their
        // overlap instead of reinforcing (unioning). The inner ring is intentionally NOT
        // normalized here - it is fixed up below to remain OPPOSITE the (now-normalized) outer
        // ring, which is the unrelated, already-correct hole-vs-shell invariant.
        NormalizeOuterWinding(outerRing);

        var innerRing = BuildClosedSide(points, -outerSideSign, style, halfWidth, flattenTolerance, out var innerRingCollapsed);

        // When the stroke half-width exceeds the contour's local inradius somewhere along its
        // length, the "inner" ring's exact offset-edge intersection points (see BuildClosedSide's
        // forceExactIntersection) are no longer a valid inward offset: the offset edges for two
        // adjacent source edges have been pushed so far toward (and past) each other that the
        // shared offset segment between their exact intersection points runs BACKWARDS relative
        // to its source edge's direction, rather than forwards. BuildClosedSide detects this via
        // innerRingCollapsed. Feeding such a ring to the NonZero fill as a hole would carve out a
        // nonsensical region (potentially larger than the source contour itself, and wound so it
        // no longer nests inside the outer ring) instead of correctly leaving the entire interior
        // filled. Omit the hole entirely in that case, so the whole interior renders as solid
        // stroke - the same outcome a true geometric erosion of the contour by half-width would
        // produce once half-width exceeds the inradius (an empty inner boundary).
        if (innerRing.Count < 3 || innerRingCollapsed)
        {
            return [outerRing];
        }

        // Regardless of how many vertices were locally reflex on each ring, the two rings must
        // still end up with opposite winding for FillRule.NonZero to render the band between them
        // (rather than the solid disc of one ring or the empty complement) - flip the inner ring
        // if the constructed rings happen to share the same winding sign. Compare signs directly
        // (rather than multiplying the two areas together and checking > 0) so that two
        // individually-finite-but-extreme double areas can never spuriously overflow their
        // product to Infinity or (Infinity * 0) to NaN.
        var outerArea = ComputeSignedArea(outerRing);
        var innerArea = ComputeSignedArea(innerRing);

        // Areas are computed (not caller-supplied literal) values, so guard the "is this ring
        // degenerate" check with a small tolerance rather than an exact-zero comparison - a
        // ring that is only nearly collinear could otherwise land at a tiny nonzero double
        // instead of exactly 0.0, and still needs to be treated as having no meaningful sign.
        const double areaNearZeroTolerance = 1e-9;
        if (Math.Abs(outerArea) > areaNearZeroTolerance
            && Math.Abs(innerArea) > areaNearZeroTolerance
            && Math.Sign(outerArea) == Math.Sign(innerArea))
        {
            innerRing.Reverse();
        }

        return [outerRing, innerRing];
    }

    /// <summary>
    ///     Computes one tangent/normal pair per segment of an open or closed polyline.
    /// </summary>
    /// <remarks>
    ///     The edge delta and its length are computed in <see langword="double"/> precision
    ///     rather than via <see cref="Vector2"/> subtraction and <see cref="Vector2.Length()"/>:
    ///     both of those operate in float32, and for an edge spanning near-extreme float32
    ///     coordinates (e.g. one endpoint near <c>-1e20</c> and the other near <c>1e20</c>), the
    ///     float32 <c>dx*dx + dy*dy</c> computation overflows to
    ///     <see cref="float.PositiveInfinity"/> well before the true length would, even though the
    ///     same computation in double precision remains finite. That overflow would otherwise
    ///     collapse the tangent/normal for a legitimately huge but finite edge to something
    ///     degenerate (zero, <c>NaN</c>, or <c>Infinity</c>), silently producing no stroke area for
    ///     that segment - the same class of float32-overflow bug already fixed for edge-length
    ///     accumulation in <see cref="DashSplitter.BuildCumulativeLengths"/>. Normalizing in
    ///     double before narrowing back to <see cref="Vector2"/> keeps the tangent/normal
    ///     computation correct for any finite edge, however extreme its coordinates.
    /// </remarks>
    private static (Vector2 Tangent, Vector2 Normal)[] BuildSegmentFrames(IReadOnlyList<Vector2> points, bool isClosed)
    {
        var segmentCount = isClosed ? points.Count : points.Count - 1;
        var frames = new (Vector2 Tangent, Vector2 Normal)[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
            var dx = (double)end.X - start.X;
            var dy = (double)end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= NearZeroDistance)
            {
                frames[i] = (Vector2.UnitX, new Vector2(0f, 1f));
                continue;
            }

            var tangent = new Vector2((float)(dx / length), (float)(dy / length));
            frames[i] = (tangent, new Vector2(-tangent.Y, tangent.X));
        }

        return frames;
    }

    /// <summary>
    ///     Builds one side of an open stroke polygon from start to end.
    /// </summary>
    private static List<Vector2> BuildOpenSide(
        IReadOnlyList<Vector2> points,
        (Vector2 Tangent, Vector2 Normal)[] frames,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance)
    {
        var side = new List<Vector2>(points.Count * 2);
        AddPointIfDistinct(side, GetOpenEndpoint(points[0], frames[0].Tangent, frames[0].Normal, sideSign, style.Cap, -1f, halfWidth));

        var sideGeometry = new StrokeSideGeometry(sideSign, style, halfWidth, flattenTolerance);
        for (var i = 1; i < points.Count - 1; i++)
        {
            AppendOpenJoin(side, points[i], frames[i - 1], frames[i], sideGeometry);
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
        StrokeSideGeometry geometry)
    {
        var previousPoint = vertex + geometry.SideSign * previousFrame.Normal * geometry.HalfWidth;
        var nextPoint = vertex + geometry.SideSign * nextFrame.Normal * geometry.HalfWidth;
        var turn = Cross(previousFrame.Tangent, nextFrame.Tangent);
        var dot = Vector2.Dot(previousFrame.Tangent, nextFrame.Tangent);
        if (MathF.Abs(turn) <= NearZeroDistance && dot > 0f)
        {
            AddPointIfDistinct(side, nextPoint);
            return;
        }

        var isConvexOnThisSide = turn * geometry.SideSign < 0f;
        if (!isConvexOnThisSide)
        {
            AddPointIfDistinct(side, previousPoint);
            AddPointIfDistinct(side, nextPoint);
            return;
        }

        AppendStyledJoin(side, vertex, previousFrame, nextFrame, geometry);
    }

    /// <summary>
    ///     Builds one closed offset ring for a closed contour.
    /// </summary>
    /// <param name="points">The flattened, simplified closed-contour vertices.</param>
    /// <param name="sideSign">The offset direction (+1/-1) for this ring relative to the normals.</param>
    /// <param name="style">The stroke geometry to apply.</param>
    /// <param name="halfWidth">Half the stroke width.</param>
    /// <param name="flattenTolerance">The tolerance used when tessellating round arcs.</param>
    /// <remarks>
    ///     Unlike the open-path join logic in <see cref="AppendOpenJoin"/> - which can freely emit
    ///     the two un-joined offset points on the locally concave side because that side remains
    ///     part of the same single ring - a closed contour's two sides are built as two separate,
    ///     independent rings that are later combined as a NonZero-fill outer shell and hole. Each
    ///     vertex is therefore resolved with the same local turn-direction test used by
    ///     <see cref="AppendOpenJoin"/> (is this side convex or concave AT THIS VERTEX, not
    ///     globally for the ring) to decide whether to honor <see cref="StrokeStyle.Join"/> (the
    ///     locally convex side) or force the exact offset-edge intersection (the locally concave
    ///     side, where a stylized corner would carve away or add stroke area).
    /// </remarks>
    /// <param name="collapsed">
    ///     Set to <see langword="true"/> when the stroke half-width exceeds this contour's local
    ///     inradius somewhere along its length, so that two adjacent forced exact-intersection
    ///     vertices' shared offset edge runs backwards relative to its source edge - the ring is
    ///     no longer a valid inward offset and must not be used as a fill hole.
    /// </param>
    private static List<Vector2> BuildClosedSide(
        IReadOnlyList<Vector2> points,
        float sideSign,
        StrokeStyle style,
        float halfWidth,
        float flattenTolerance,
        out bool collapsed)
    {
        var frames = BuildSegmentFrames(points, isClosed: true);
        var ring = new List<Vector2>(points.Count * 2);
        var geometry = new StrokeSideGeometry(sideSign, style, halfWidth, flattenTolerance);

        // Tracks, for each source vertex, the ring index of the single plain offset point emitted
        // for it (see the remarks below), or -1 if that vertex instead emitted a styled join
        // (Round/Bevel/Miter, or a Miter/exact-intersection fallback) that can contribute zero,
        // one, or more than one point not directly tied to a single shared offset edge.
        var plainPointIndex = new int[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var previousFrame = frames[(i + frames.Length - 1) % frames.Length];
            var nextFrame = frames[i];
            var turn = Cross(previousFrame.Tangent, nextFrame.Tangent);
            var dot = Vector2.Dot(previousFrame.Tangent, nextFrame.Tangent);
            if (MathF.Abs(turn) <= NearZeroDistance && dot > 0f)
            {
                AddPointIfDistinct(ring, points[i] + sideSign * nextFrame.Normal * halfWidth);
                plainPointIndex[i] = ring.Count - 1;
                continue;
            }

            var isConvexOnThisSide = turn * sideSign < 0f;
            var countBefore = ring.Count;
            AppendStyledJoin(
                ring,
                points[i],
                previousFrame,
                nextFrame,
                geometry,
                forceExactIntersection: !isConvexOnThisSide);

            // Only a locally concave (forceExactIntersection) vertex that emitted exactly one
            // point is a "plain" vertex whose single point is the shared endpoint of two adjacent
            // offset edges - see the collapse check below. A locally convex (styled-join) vertex,
            // or a concave vertex whose lines were parallel and fell back to the two un-joined
            // offset points, is excluded from that check.
            plainPointIndex[i] = !isConvexOnThisSide && ring.Count - countBefore == 1 ? ring.Count - 1 : -1;
        }

        // Detect inner-ring collapse: when the stroke half-width exceeds the contour's local
        // inradius, two adjacent plain (forced exact-intersection) vertices' shared offset edge -
        // which must run in the SAME direction as its source edge's tangent for the ring to be a
        // valid inward offset - instead runs BACKWARDS, because the offset lines were pushed past
        // each other. A single such reversal invalidates the whole ring as a hole (see
        // CreateClosedStrokePolygons).
        collapsed = false;
        for (var i = 0; i < points.Count; i++)
        {
            var next = (i + 1) % points.Count;
            var currentIndex = plainPointIndex[i];
            var nextIndex = plainPointIndex[next];
            if (currentIndex < 0 || nextIndex < 0)
            {
                continue;
            }

            var edgeVector = ring[nextIndex] - ring[currentIndex];
            if (Vector2.Dot(edgeVector, frames[i].Tangent) <= NearZeroDistance)
            {
                collapsed = true;
                break;
            }
        }

        RemoveTrailingDuplicateOfFirst(ring);
        return ring;
    }

    /// <summary>
    ///     Appends the requested styled join between two offset segments.
    /// </summary>
    /// <param name="target">The point list to append the join's vertices to.</param>
    /// <param name="vertex">The source polyline vertex the join is centered on.</param>
    /// <param name="previousFrame">The incoming segment's tangent/normal frame.</param>
    /// <param name="nextFrame">The outgoing segment's tangent/normal frame.</param>
    /// <param name="geometry">The side/style/half-width/tolerance this join is built with.</param>
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
        (Vector2 Tangent, Vector2 Normal) previousFrame,
        (Vector2 Tangent, Vector2 Normal) nextFrame,
        StrokeSideGeometry geometry,
        bool forceExactIntersection = false)
    {
        var previousPoint = vertex + geometry.SideSign * previousFrame.Normal * geometry.HalfWidth;
        var nextPoint = vertex + geometry.SideSign * nextFrame.Normal * geometry.HalfWidth;

        if (forceExactIntersection)
        {
            if (TryIntersectLines(previousPoint, previousFrame.Tangent, nextPoint, nextFrame.Tangent, out var intersection))
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

        switch (geometry.Style.Join)
        {
            case LineJoin.Round:
                AddPointIfDistinct(target, previousPoint);
                AppendArcShortest(
                    target,
                    vertex,
                    previousPoint - vertex,
                    nextPoint - vertex,
                    new ArcTessellation(geometry.HalfWidth, geometry.FlattenTolerance, IncludeStart: false, IncludeEnd: true));
                break;

            case LineJoin.Bevel:
                AddPointIfDistinct(target, previousPoint);
                AddPointIfDistinct(target, nextPoint);
                break;

            default:
                if (TryCreateMiter(vertex, (previousPoint, previousFrame.Tangent), (nextPoint, nextFrame.Tangent), geometry.Style.Width, geometry.Style.MiterLimit, out var miter))
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
    /// <remarks>
    ///     <paramref name="vertex"/>-to-intersection distance is measured on a single offset line
    ///     that already sits half of <paramref name="strokeWidth"/> away from the source vertex,
    ///     so it is exactly half of the SVG "miter length" (the full tip-to-tip span across both
    ///     offset lines). Dividing by <c>strokeWidth / 2</c> therefore reproduces the standard SVG
    ///     <c>miterLength / strokeWidth</c> ratio (for example, <c>~1.414</c> for a right-angle
    ///     join) without needing to materialize the opposite offset line's mirrored intersection
    ///     point. Dividing by the full <paramref name="strokeWidth"/> instead would halve the
    ///     computed ratio and silently make every <see cref="StrokeStyle.MiterLimit"/> value twice
    ///     as permissive as the documented SVG-style contract.
    /// </remarks>
    private static bool TryCreateMiter(
        Vector2 vertex,
        (Vector2 Point, Vector2 Direction) previousLine,
        (Vector2 Point, Vector2 Direction) nextLine,
        float strokeWidth,
        float miterLimit,
        out Vector2 miterPoint)
    {
        miterPoint = default;
        if (!TryIntersectLines(previousLine.Point, previousLine.Direction, nextLine.Point, nextLine.Direction, out var intersection))
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
        ArcTessellation arc)
    {
        var startAngle = MathF.Atan2(startVector.Y, startVector.X);
        var endAngle = MathF.Atan2(endVector.Y, endVector.X);
        var sweep = NormalizeSignedAngle(endAngle - startAngle);
        AppendArc(target, center, startAngle, sweep, arc);
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
        ArcTessellation arc)
    {
        var startAngle = MathF.Atan2(startVector.Y, startVector.X);
        var endAngle = MathF.Atan2(endVector.Y, endVector.X);
        var sweep = NormalizeSignedAngle(endAngle - startAngle);
        var throughAngle = MathF.Atan2(throughVector.Y, throughVector.X);
        if (!AngleLiesOnSweep(startAngle, sweep, throughAngle))
        {
            sweep = sweep > 0f ? sweep - 2f * MathF.PI : sweep + 2f * MathF.PI;
        }

        AppendArc(target, center, startAngle, sweep, arc);
    }

    /// <summary>
    ///     Appends a tessellated circular arc.
    /// </summary>
    private static void AppendArc(
        List<Vector2> target,
        Vector2 center,
        float startAngle,
        float sweep,
        ArcTessellation arc)
    {
        var segmentCount = GetArcSegmentCount(arc.Radius, MathF.Abs(sweep), arc.FlattenTolerance);
        for (var i = 0; i <= segmentCount; i++)
        {
            if (i == 0 && !arc.IncludeStart)
            {
                continue;
            }

            if (i == segmentCount && !arc.IncludeEnd)
            {
                continue;
            }

            var angle = startAngle + sweep * i / segmentCount;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * arc.Radius;
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
    private static void AppendReversed(List<Vector2> source, List<Vector2> target, bool skipFirst)
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
    ///     Reverses <paramref name="ring"/> in place, if necessary, so every independently-emitted
    ///     OUTER outline (an open-line outline, a point-cap circle or square, or a closed
    ///     contour's outer shell ring) ends up with the same fixed winding direction, regardless
    ///     of the source subpath's authored orientation.
    /// </summary>
    /// <remarks>
    ///     <see cref="PathStroker.Stroke(DemaConsulting.CanvasNet.Geometry.Path, StrokeStyle, float)"/>
    ///     emits every stroked subpath's outline(s) as independent closed subpaths in one output
    ///     <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>, documented to be filled with
    ///     <see cref="FillRule.NonZero"/>. Under NonZero, two overlapping subpaths only reinforce
    ///     (union) each other when they carry the SAME signed winding; if their windings happen to
    ///     be opposite, their overlap's winding numbers cancel and NonZero incorrectly renders a
    ///     hole there instead of solid fill. Before this normalization, an open-line stroke
    ///     outline was always wound one way, point-cap circles/squares were always wound the other
    ///     way, and a closed contour's outer ring followed whatever direction its source contour
    ///     happened to be authored in - so two overlapping outer outlines from the very same
    ///     <see cref="PathStroker.Stroke"/> call could easily end up with opposite winding purely
    ///     by chance of which outline kind produced each one. Forcing every outer outline to the
    ///     same fixed sign (positive, matching this file's <see cref="ComputeSignedArea"/>
    ///     convention) eliminates that chance cancellation while leaving each outline's actual
    ///     vertex positions - and therefore its filled shape - completely unchanged; reversing a
    ///     polygon's vertex order only flips its signed winding, not its silhouette.
    ///     <para>
    ///     This must only ever be applied to an OUTER outline. A closed contour's INNER ring (the
    ///     hole) must remain the OPPOSITE winding of its own outer ring - that asymmetry is the
    ///     mechanism by which <see cref="FillRule.NonZero"/> renders the shell between the two
    ///     rings rather than the solid disc of one ring - so callers fix the inner ring up
    ///     relative to the (already-normalized) outer ring instead of normalizing it here.
    ///     </para>
    /// </remarks>
    private static void NormalizeOuterWinding(List<Vector2> ring)
    {
        if (ComputeSignedArea(ring) < 0.0)
        {
            ring.Reverse();
        }
    }

    /// <summary>
    ///     Computes the signed area of a polygon-like vertex sequence.
    /// </summary>
    /// <remarks>
    ///     Both the shoelace cross-product terms and their running sum are computed and
    ///     accumulated in <see langword="double"/> precision, and the result is returned as
    ///     <see langword="double"/> rather than narrowed back to <see langword="float"/>: for
    ///     vertices near extreme float32 magnitudes (e.g. close to <c>float.MaxValue</c>), the
    ///     float32 products <c>current.X * next.Y</c> and <c>current.Y * next.X</c> - and their
    ///     running sum across every vertex, and even the true signed area itself for a large
    ///     enough contour - can overflow float32's finite range well before the true magnitude
    ///     would, even though the same computation in double precision remains finite. Every
    ///     caller of this method only ever inspects the SIGN of the result (never its magnitude),
    ///     so returning the full-precision double lets callers make a correct sign decision even
    ///     when the true area is too large to represent as a finite <see langword="float"/>. An
    ///     overflowed or NaN result here would silently pick the wrong outer-ring side sign in
    ///     <see cref="CreateClosedStrokePolygons"/> and defeat the opposite-winding check in
    ///     <see cref="NormalizeOuterWinding"/> (a NaN comparison is always false) - the same class
    ///     of float32-overflow bug already fixed for edge-length computations in
    ///     <see cref="BuildSegmentFrames"/> and <see cref="DashSplitter.BuildCumulativeLengths"/>.
    /// </remarks>
    private static double ComputeSignedArea(IReadOnlyList<Vector2> points)
    {
        var area = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (double)current.X * next.Y - (double)current.Y * next.X;
        }

        return area / 2.0;
    }
}
