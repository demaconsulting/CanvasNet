using System.Linq;
using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;

// cspell:ignore Outliner underflows inradius

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="StrokeOutliner"/> type.
/// </summary>
public class StrokeOutlinerTests
{
    /// <summary>
    ///     Proves that a closed contour produces one outer ring and one inner ring wound in
    ///     opposite directions.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_ClosedSubpath_ProducesTwoCounterWoundRings()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(2, 2),
            new(6, 2),
            new(6, 6),
            new(2, 6)
        };
        var style = new StrokeStyle(2f);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert
        Assert.Equal(2, polygons.Count);
        Assert.True(GetSignedArea(polygons[0]) * GetSignedArea(polygons[1]) < 0f);
    }

    /// <summary>
    ///     Proves that every independently-emitted OUTER outline shares the same fixed winding
    ///     direction, regardless of the kind of geometry that produced it: an open-line stroke
    ///     outline and an unrelated point-cap circle must wind the same way.
    /// </summary>
    /// <remarks>
    ///     Before the winding-normalization fix, an open-line outline was always wound one way
    ///     (clockwise, i.e. negative signed area) while a point-cap circle was always wound the
    ///     other way (counterclockwise, i.e. positive signed area) - see
    ///     the review thread that reported this defect. Two such outlines from the same
    ///     <see cref="PathStroker.Stroke"/> call would then carry opposite signed winding under
    ///     <see cref="FillRule.NonZero"/>, so an overlap between them would cancel to a hole
    ///     instead of reinforcing (unioning). This test proves the two outline kinds now always
    ///     agree in sign.
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_OpenLineAndPointCapCircle_ShareSameOuterWinding()
    {
        // Arrange
        var lineStyle = new StrokeStyle(2f, cap: LineCap.Round);
        var linePoints = new List<Vector2> { new(2, 4), new(6, 4) };
        var pointStyle = new StrokeStyle(6f, cap: LineCap.Round);
        var pointPoints = new List<Vector2> { new(4, 4) };

        // Act
        var linePolygon = Assert.Single(StrokeOutliner.Outline(linePoints, isClosed: false, lineStyle, flattenTolerance: 0.25f));
        var circlePolygon = Assert.Single(StrokeOutliner.Outline(pointPoints, isClosed: false, pointStyle, flattenTolerance: 0.25f));

        // Assert
        Assert.True(
            GetSignedArea(linePolygon) * GetSignedArea(circlePolygon) > 0f,
            "Expected the open-line outline and the point-cap circle to share the same signed " +
            "winding direction, so overlapping strokes reinforce rather than cancel under " +
            "FillRule.NonZero.");
    }

    /// <summary>
    ///     Proves that a closed contour's OUTER ring normalizes to the same fixed winding
    ///     direction whether the source contour is authored clockwise or counterclockwise.
    /// </summary>
    /// <remarks>
    ///     Before the winding-normalization fix, the outer ring's winding followed whichever
    ///     direction the source contour happened to be authored in, so a reversed-winding source
    ///     contour's outer ring would end up wound oppositely from an equivalent
    ///     forward-authored contour, and could cancel against it under
    ///     <see cref="FillRule.NonZero"/> if the two overlapped.
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_ClosedContourReversedSourceWinding_NormalizesOuterRingConsistently()
    {
        // Arrange: the same 4x4 square authored once counterclockwise and once clockwise.
        var counterClockwise = new List<Vector2>
        {
            new(2, 2),
            new(6, 2),
            new(6, 6),
            new(2, 6)
        };
        var clockwise = new List<Vector2>
        {
            new(2, 2),
            new(2, 6),
            new(6, 6),
            new(6, 2)
        };
        var style = new StrokeStyle(2f);

        // Act
        var ccwPolygons = StrokeOutliner.Outline(counterClockwise, isClosed: true, style, flattenTolerance: 0.25f);
        var cwPolygons = StrokeOutliner.Outline(clockwise, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: both outer rings (index 0) share the same signed-winding direction.
        Assert.Equal(2, ccwPolygons.Count);
        Assert.Equal(2, cwPolygons.Count);
        Assert.True(
            GetSignedArea(ccwPolygons[0]) * GetSignedArea(cwPolygons[0]) > 0f,
            "Expected both source-winding variants' outer rings to share the same signed " +
            "winding direction.");
    }

    /// <summary>
    ///     Proves that a miter join within the configured limit emits the sharp intersection
    ///     vertex.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_MiterJoinWithinLimit_ProducesSharpVertex()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(8, 4), new(8, 8) };
        var style = new StrokeStyle(4f, join: LineJoin.Miter, miterLimit: 4f);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.Contains(new Vector2(10, 2), polygon);
    }

    /// <summary>
    ///     Proves that a miter join exceeding the configured limit falls back to bevel vertices.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_MiterJoinExceedingLimit_FallsBackToBevelVertex()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(8, 4), new(8, 8) };
        var style = new StrokeStyle(4f, join: LineJoin.Miter, miterLimit: 1f);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.DoesNotContain(new Vector2(10, 2), polygon);
        Assert.Contains(new Vector2(8, 2), polygon);
        Assert.Contains(new Vector2(10, 4), polygon);
    }

    /// <summary>
    ///     Proves that a round join at an extreme stroke half-width (large enough that
    ///     <c>flattenTolerance / radius</c> underflows float32 precision) still produces a
    ///     genuinely curved (multi-segment) outline rather than silently degrading into the same
    ///     straight chord a bevel join would produce.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundJoinAtExtremeScale_ProducesCurvedNotStraightJoin()
    {
        // Arrange: a sharp (~177 degree) turn so the round join's convex side sweeps a large
        // angle, paired with a stroke half-width many orders of magnitude larger than the
        // ~1.19e-7 relative float32 precision floor (so flattenTolerance / radius underflows to
        // zero for any reasonable flattenTolerance).
        var points = new List<Vector2> { new(0, 0), new(10, 0), new(0, 0.5f) };
        const float extremeWidth = 2e13f;
        var halfWidth = extremeWidth / 2f;
        var roundStyle = new StrokeStyle(extremeWidth, join: LineJoin.Round);
        var bevelStyle = new StrokeStyle(extremeWidth, join: LineJoin.Bevel);

        // Act
        var roundPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, roundStyle, flattenTolerance: 0.25f));
        var bevelPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, bevelStyle, flattenTolerance: 0.25f));

        // Assert: the round join must contribute at least one vertex that sits substantially away
        // from every vertex the (straight-chord) bevel join produces at the same geometry and
        // scale. A genuine multi-segment arc bulges far off the previous/next tangent-point chord
        // (here, by nearly the full half-width), whereas a collapsed single-chord arc endpoint
        // would land within float32 noise (~halfWidth * 1.2e-7) of the bevel join's own vertex.
        // The threshold below sits comfortably between those two scales.
        var maxDistanceFromBevel = roundPolygon
            .Select(roundVertex => bevelPolygon.Min(bevelVertex => Vector2.Distance(roundVertex, bevelVertex)))
            .Max();
        Assert.True(
            maxDistanceFromBevel > halfWidth * 0.1f,
            $"Expected a round-join vertex more than {halfWidth * 0.1f} units from any bevel-join vertex " +
            $"(found max {maxDistanceFromBevel}): a small distance indicates the flattenTolerance/radius " +
            "underflow regression has returned and the round join collapsed to a single straight chord.");
    }

    /// <summary>
    ///     Proves that the extreme-scale safety fallback does not change tessellation at ordinary,
    ///     well-conditioned scales: a round-capped point stroke must still produce exactly the
    ///     segment count implied by the flattening-tolerance formula.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundCapAtTypicalScale_MatchesExpectedSegmentCount()
    {
        // Arrange: with a half-width of 2 and a flattening tolerance of 0.25, the tolerance-based
        // formula yields a maximum chord angle of roughly 1.010465 radians, which requires
        // exactly 7 segments to sweep a full circle within tolerance.
        var style = new StrokeStyle(4f, cap: LineCap.Round);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline([new Vector2(0, 0)], isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.Equal(7, polygon.Count);
    }

    /// <summary>
    ///     Proves that a round join with a small (~45 degree) sweep angle at an extreme stroke
    ///     half-width (large enough that <c>flattenTolerance / radius</c> underflows float32
    ///     precision) still produces a genuinely multi-segment curved outline. Prior to the fix,
    ///     the underflow fallback's angle-based minimum segment count
    ///     (<c>ceil(sweepMagnitude / (PI/2))</c>) was exactly 1 for any sweep of at most 90
    ///     degrees, which collapsed the arc right back into a single straight chord - the same
    ///     regression the fallback was supposed to eliminate, just for smaller sweep angles than
    ///     the previously covered near-180-degree case.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundJoinSmallSweepAtExtremeScale_ProducesMultiSegmentCurve()
    {
        // Arrange: a ~45 degree turn (well under the FallbackSegmentAngle of 90 degrees, so
        // fallbackCount alone would be exactly 1 without the Math.Max(2, ...) floor), paired with
        // a stroke half-width many orders of magnitude larger than the ~1.19e-7 relative float32
        // precision floor so flattenTolerance / radius underflows to zero for any reasonable
        // flattenTolerance.
        var points = new List<Vector2>
        {
            new(0, 0),
            new(10, 0),
            new(10 + 10 * MathF.Cos(-MathF.PI / 4f), 10 * MathF.Sin(-MathF.PI / 4f))
        };
        const float extremeWidth = 2e13f;
        var roundStyle = new StrokeStyle(extremeWidth, join: LineJoin.Round);
        var bevelStyle = new StrokeStyle(extremeWidth, join: LineJoin.Bevel);

        // Act
        var roundPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, roundStyle, flattenTolerance: 0.25f));
        var bevelPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, bevelStyle, flattenTolerance: 0.25f));

        // Assert: a genuinely curved (multi-segment) round join contributes at least one extra
        // vertex beyond the two straight-chord endpoints a bevel join produces at the same
        // geometry and scale. A collapsed single-chord round join would instead produce exactly
        // the same vertex count as the bevel join.
        Assert.True(
            roundPolygon.Count > bevelPolygon.Count,
            $"Expected the round join ({roundPolygon.Count} vertices) to contribute more vertices " +
            $"than the equivalent bevel join ({bevelPolygon.Count} vertices): an equal count " +
            "indicates the small-sweep-angle underflow fallback has collapsed back to a single " +
            "straight chord.");
    }

    /// <summary>
    ///     Proves that a CONCAVE (reflex-vertex) closed contour resolves, independently at each
    ///     vertex, which of the two offset rings gets the styled <see cref="LineJoin"/> and which
    ///     gets the exact offset-edge intersection - from the LOCAL turn direction at that vertex,
    ///     not from a single ring-wide outer/inner assignment.
    /// </summary>
    /// <remarks>
    ///     The L-shaped hexagon (0,0)-(6,0)-(6,3)-(3,3)-(3,6)-(0,6) has five ordinary convex
    ///     vertices and exactly one reflex (concave) vertex at (3,3). At every convex vertex, ring
    ///     A carries the styled Bevel join and ring B carries the exact intersection point; at the
    ///     one reflex vertex, this flips - ring A must instead carry the exact intersection point
    ///     (4,4), and ring B must instead carry the styled Bevel join's two offset points, (1,5)
    ///     and (2,5). A global (pre-fix) outer/inner assignment gets exactly this vertex backwards:
    ///     it would emit the Bevel chord (3,4)/(4,3) on ring A and the exact intersection (2,2) on
    ///     ring B instead.
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_ConcaveClosedContourBevelJoin_ResolvesJoinPerVertexFromLocalTurn()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(0, 0),
            new(6, 0),
            new(6, 3),
            new(3, 3),
            new(3, 6),
            new(0, 6)
        };
        var style = new StrokeStyle(2f, join: LineJoin.Bevel);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert
        Assert.Equal(2, polygons.Count);
        var ringWithExactReflexIntersection = polygons[0];
        var ringWithStyledReflexJoin = polygons[1];

        Assert.Contains(new Vector2(4, 4), ringWithExactReflexIntersection);
        Assert.DoesNotContain(new Vector2(3, 4), ringWithExactReflexIntersection);
        Assert.DoesNotContain(new Vector2(4, 3), ringWithExactReflexIntersection);

        Assert.Contains(new Vector2(1, 5), ringWithStyledReflexJoin);
        Assert.Contains(new Vector2(2, 5), ringWithStyledReflexJoin);
        Assert.DoesNotContain(new Vector2(2, 2), ringWithStyledReflexJoin);
    }

    /// <summary>
    ///     Proves that a closed contour reduced to three or more DISTINCT but COLLINEAR points
    ///     (a zero-area closed contour that traces back and forth along a single line) still
    ///     renders as a visible stroke, rather than vanishing entirely because its "outer" and
    ///     "inner" offset rings would otherwise be coincident (zero signed area) and forced into
    ///     opposite winding, which cancels completely under <see cref="FillRule.NonZero"/>. This
    ///     generalizes the already-fixed exactly-two-point case to any number of collinear points.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_ThreePointCollinearClosedContour_ProducesVisibleStroke()
    {
        // Arrange: a closed contour (M...L...L...Z) whose three distinct points all lie on the
        // line y=2.
        var points = new List<Vector2> { new(2, 2), new(4, 2), new(6, 2) };
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: a non-empty stroke covering the full [2,6] extent of the collinear points,
        // one half-width above and below the line - exactly what an equivalent open stroke over
        // the same points would produce.
        var polygon = Assert.Single(polygons);
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var maxY = polygon.Max(p => p.Y);
        Assert.Equal(2f, minX);
        Assert.Equal(6f, maxX);
        Assert.Equal(1f, minY);
        Assert.Equal(3f, maxY);
    }

    /// <summary>
    ///     Proves that a closed contour mixing duplicate/near-duplicate points with otherwise
    ///     collinear points still renders as a visible stroke rather than vanishing.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_CollinearClosedContourWithDuplicatePoints_ProducesVisibleStroke()
    {
        // Arrange: an exact duplicate of the first point, followed by collinear points, closing
        // back to the start.
        var points = new List<Vector2> { new(2, 2), new(2, 2), new(4, 2), new(6, 2) };
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert
        var polygon = Assert.Single(polygons);
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var maxY = polygon.Max(p => p.Y);
        Assert.Equal(2f, minX);
        Assert.Equal(6f, maxX);
        Assert.Equal(1f, minY);
        Assert.Equal(3f, maxY);
    }

    /// <summary>
    ///     Proves that stroking a closed contour with a half-width that EXCEEDS the contour's
    ///     inradius (the largest half-width for which the stroke band still leaves an unfilled
    ///     hole) does not produce an invalid, collapsed "inner ring" hole - the entire interior
    ///     must instead render as solid stroke, exactly as a true geometric erosion of the contour
    ///     by that half-width would (an empty inner boundary once half-width exceeds the
    ///     inradius).
    /// </summary>
    /// <remarks>
    ///     The 4x4 square (2,2)-(6,2)-(6,6)-(2,6) has an inradius of only 2 (half its 4-unit side
    ///     length). At <c>Width: 10</c> (half-width 5), the naive per-vertex exact offset-edge
    ///     intersection used to build the inner/hole ring produces corners far OUTSIDE the
    ///     original square (around <c>[-3,11]</c> for the outer ring and a wrongly inverted,
    ///     oversized square around <c>[1,7]</c> for what would have been the "inner" ring, per the
    ///     original bug report), rather than correctly collapsing to nothing. Before the fix, this
    ///     produced two counter-wound rings and left a bogus, unfilled square hole spanning
    ///     roughly <c>[1,7]</c> - larger than the original 4x4 square itself. After the fix, the
    ///     inner ring's collapse is detected and the hole is omitted entirely, leaving a single
    ///     outer ring whose interior fully covers the original square (and a comfortable margin
    ///     beyond it).
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_ClosedSquareHalfWidthExceedsInradius_ProducesNoInvalidHole()
    {
        // Arrange: a 4x4 square (inradius 2) stroked with half-width 5, well past the inradius.
        var points = new List<Vector2>
        {
            new(2, 2),
            new(6, 2),
            new(6, 6),
            new(2, 6)
        };
        var style = new StrokeStyle(10f);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: exactly one polygon (no hole) - the whole square interior renders as stroke.
        var polygon = Assert.Single(polygons);

        // The entire original square, and a reasonable margin around it, must lie within the
        // single outer polygon (no unfilled hole anywhere within the square).
        Vector2[] mustBeCovered =
        [
            new(4, 4), // center
            new(2.5f, 2.5f),
            new(5.5f, 2.5f),
            new(5.5f, 5.5f),
            new(2.5f, 5.5f),
            new(2, 2),
            new(6, 6)
        ];
        foreach (var point in mustBeCovered)
        {
            Assert.True(
                IsPointInPolygon(point, polygon),
                $"Expected {point} to be covered by the stroke band (no unfilled hole), but it was not.");
        }
    }

    /// <summary>
    ///     Proves that a half-width comfortably NEAR but still below the contour's inradius still
    ///     produces the correct, valid (non-collapsed) hole - the fix for over-erosion must not
    ///     regress the ordinary case where the inner ring remains geometrically valid.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_ClosedSquareHalfWidthNearButBelowInradius_ProducesValidHole()
    {
        // Arrange: the same 4x4 square (inradius 2), now stroked with half-width 1.9 - close to,
        // but still below, the inradius.
        var points = new List<Vector2>
        {
            new(2, 2),
            new(6, 2),
            new(6, 6),
            new(2, 6)
        };
        var style = new StrokeStyle(3.8f);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: two counter-wound rings (a genuine hole is still produced).
        Assert.Equal(2, polygons.Count);
        Assert.True(GetSignedArea(polygons[0]) * GetSignedArea(polygons[1]) < 0f);

        // The center of the square (the deepest interior point, furthest from every edge) must
        // fall within the small remaining hole, not be covered by the stroke band.
        var outerRing = polygons[0];
        var innerRing = polygons[1];
        Assert.True(IsPointInPolygon(new Vector2(4, 4), outerRing));
        Assert.True(IsPointInPolygon(new Vector2(4, 4), innerRing));

        // The hole must remain nested well within the original square, not spill outside it.
        foreach (var vertex in innerRing)
        {
            Assert.InRange(vertex.X, 2f, 6f);
            Assert.InRange(vertex.Y, 2f, 6f);
        }
    }

    /// <summary>
    ///     Proves that a segment spanning near-extreme float32 coordinates - far enough apart
    ///     that the naive float32 <c>dx*dx + dy*dy</c> computation inside
    ///     <see cref="Vector2.Length()"/> overflows to <see cref="float.PositiveInfinity"/> even
    ///     though the true edge length remains comfortably finite - still produces a valid,
    ///     non-degenerate stroke outline.
    /// </summary>
    /// <remarks>
    ///     A segment from <c>(-1e20, 0)</c> to <c>(1e20, 0)</c> has a true length of <c>2e20</c>,
    ///     itself well within float32's representable range (max ~3.4e38). But squaring that delta
    ///     in float32 - <c>(2e20)^2 = 4e40</c> - overflows float32's range before the square root
    ///     is ever taken, collapsing <c>Vector2.Length()</c> to <see cref="float.PositiveInfinity"/>
    ///     for a perfectly valid, finite edge. Before the fix, dividing the (finite) delta by that
    ///     infinite length produced a zero tangent/normal, which silently collapsed the segment's
    ///     offset to zero and dropped the whole outline (fewer than 3 vertices after
    ///     deduplication). This test proves the segment now offsets by the full half-width as
    ///     normal, with no <c>NaN</c>/<c>Infinity</c> coordinates anywhere in the result.
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_SegmentSpanningExtremeFloat32Coordinates_ProducesValidNonDegenerateOutline()
    {
        // Arrange: a horizontal segment whose endpoints are individually well within float32's
        // representable range, but whose squared length overflows float32 before the fix.
        var points = new List<Vector2> { new(-1e20f, 0f), new(1e20f, 0f) };
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: false, style, flattenTolerance: 0.25f);

        // Assert: exactly one valid quadrilateral outline, offset by the full half-width (1) on
        // each side, with every coordinate finite.
        var polygon = Assert.Single(polygons);
        Assert.Equal(4, polygon.Count);
        foreach (var vertex in polygon)
        {
            Assert.True(float.IsFinite(vertex.X), $"Expected a finite X coordinate but found {vertex.X}.");
            Assert.True(float.IsFinite(vertex.Y), $"Expected a finite Y coordinate but found {vertex.Y}.");
        }

        Assert.Contains(polygon, vertex => MathF.Abs(vertex.Y - 1f) < 1e-6f);
        Assert.Contains(polygon, vertex => MathF.Abs(vertex.Y - -1f) < 1e-6f);
        Assert.Contains(polygon, vertex => MathF.Abs(vertex.X - -1e20f) < 1e13f);
        Assert.Contains(polygon, vertex => MathF.Abs(vertex.X - 1e20f) < 1e13f);
    }

    /// <summary>
    ///     Proves that a closed, genuinely non-collinear contour spanning near-extreme float32
    ///     coordinates - large enough that the naive float32 <c>dx*dx + dy*dy</c> computation
    ///     inside <see cref="Vector2.LengthSquared()"/> overflows to
    ///     <see cref="float.PositiveInfinity"/> - is still correctly routed through
    ///     <c>CreateClosedStrokePolygons</c> (an outer ring plus a counter-wound inner ring), NOT
    ///     misclassified as a degenerate/collinear closed contour and collapsed to the open-stroke
    ///     path.
    /// </summary>
    /// <remarks>
    ///     The contour is a right triangle with leg length <c>2.5e19</c>. Its true (double
    ///     precision) leg length and enclosed area remain comfortably finite, and the three
    ///     vertices are unambiguously non-collinear. But squaring that leg length in float32 -
    ///     <c>(2.5e19)^2 = 6.25e38</c> - overflows float32's representable range
    ///     (max ~3.4e38) before <c>AreAllPointsCollinear</c>'s direction-normalization step can
    ///     even take a square root, collapsing the normalized direction to <c>(0, 0)</c>. Every
    ///     subsequent perpendicular-distance check against that zeroed direction then evaluates to
    ///     zero, so before the fix, this perfectly ordinary large triangle was misclassified as a
    ///     degenerate/collinear closed contour and silently rendered via the open-stroke path
    ///     (a single band with caps at both ends) instead of a proper shell with a hole.
    /// </remarks>
    [Fact]
    public void StrokeOutliner_Outline_ClosedContourWithExtremeFloat32NonCollinearCoordinates_ProducesValidShellOutline()
    {
        // Arrange: a right triangle whose leg lengths are individually well within float32's
        // representable range, but whose squared length (as computed by Vector2.LengthSquared())
        // overflows float32 before the fix.
        var points = new List<Vector2>
        {
            new(0f, 0f),
            new(2.5e19f, 0f),
            new(0f, 2.5e19f)
        };
        var style = new StrokeStyle(4f);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: correctly classified as a genuine (non-degenerate) closed contour, producing an
        // outer ring and a counter-wound inner ring (the stroked band with its hole) - NOT the
        // single open-stroke band that the pre-fix float32 overflow in AreAllPointsCollinear would
        // have misclassified this large-but-legitimate triangle into producing.
        Assert.Equal(2, polygons.Count);

        foreach (var polygon in polygons)
        {
            Assert.True(polygon.Count >= 3, "Expected each ring to be a valid polygon.");
            foreach (var vertex in polygon)
            {
                Assert.True(float.IsFinite(vertex.X), $"Expected a finite X coordinate but found {vertex.X}.");
                Assert.True(float.IsFinite(vertex.Y), $"Expected a finite Y coordinate but found {vertex.Y}.");
            }
        }

        // The outer and inner rings must be non-zero-area and oppositely wound (the shell/hole
        // invariant), using a double-precision area computation so the assertion itself cannot be
        // defeated by the same float32 overflow being proven fixed above.
        var outerArea = GetSignedAreaDouble(polygons[0]);
        var innerArea = GetSignedAreaDouble(polygons[1]);

        // Mirror StrokeOutliner's own near-zero tolerance (rather than an exact-zero comparison)
        // when treating a computed area as "non-zero" - these areas are computed, not literal
        // sentinel values, so a tiny tolerance guards against floating noise near true zero.
        const double areaNearZeroTolerance = 1e-9;
        Assert.True(
            Math.Abs(outerArea) > areaNearZeroTolerance
            && Math.Abs(innerArea) > areaNearZeroTolerance
            && Math.Sign(outerArea) != Math.Sign(innerArea),
            $"Expected oppositely-wound non-zero rings, but got outerArea={outerArea}, innerArea={innerArea}.");
    }

    private static float GetSignedArea(IReadOnlyList<Vector2> points)
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

    /// <summary>
    ///     Double-precision equivalent of <see cref="GetSignedArea"/>, used by tests exercising
    ///     near-extreme float32 coordinates where the float32 shoelace computation itself would
    ///     overflow before the assertion could even inspect the true sign.
    /// </summary>
    private static double GetSignedAreaDouble(IReadOnlyList<Vector2> points)
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

    /// <summary>
    ///     Determines whether a point lies within a simple polygon using the standard ray-casting
    ///     (even-odd crossing) test.
    /// </summary>
    private static bool IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var vi = polygon[i];
            var vj = polygon[j];
            if (((vi.Y > point.Y) != (vj.Y > point.Y)) &&
                (point.X < (vj.X - vi.X) * (point.Y - vi.Y) / (vj.Y - vi.Y) + vi.X))
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
