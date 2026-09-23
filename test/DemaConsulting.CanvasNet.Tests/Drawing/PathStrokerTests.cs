using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

// cspell:ignore Outliner unstroked

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="PathStroker"/>.
/// </summary>
public class PathStrokerTests
{
    /// <summary>
    ///     Proves that a butt-capped horizontal line becomes an exact rectangle with no extension.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineButtCap_FillsExactRectangleNoExtension()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var surface = RenderStroke(path, style, 8, 5);

        // Assert
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[5, 2].A);
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a round-capped horizontal line renders semicircular end caps.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineRoundCap_FillsRectanglePlusSemicircularEnds()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var surface = RenderStroke(path, style, 8, 5);

        // Assert
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)166, surface[1, 1].A);
        Assert.Equal((byte)166, surface[6, 1].A);
        Assert.Equal((byte)0, surface[0, 1].A);
    }

    /// <summary>
    ///     Proves that a square-capped horizontal line extends by half the stroke width at each
    ///     end.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineSquareCap_FillsRectanglePlusHalfWidthExtension()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Square);

        // Act
        var surface = RenderStroke(path, style, 9, 5);

        // Assert
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[6, 2].A);
        Assert.Equal((byte)0, surface[0, 1].A);
        Assert.Equal((byte)0, surface[7, 1].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a miter join renders the miter-only corner pixel.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerMiterJoin_FillsSharpMiteredCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Miter, miterLimit: 4f);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)255, surface[9, 2].A);
        Assert.Equal((byte)255, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a round join renders the quarter-circle corner
    ///     coverage.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerRoundJoin_FillsRoundedCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Round);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)202, surface[8, 2].A);
        Assert.Equal((byte)62, surface[9, 2].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a bevel join omits the miter-only corner pixel.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerBevelJoin_FillsFlatBeveledCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Bevel);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)0, surface[9, 2].A);
        Assert.Equal((byte)128, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that stroking a closed rectangle produces a ring with an unfilled center and an
    ///     unfilled exterior.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedRectangle_FillsRingLeavingInteriorAndExteriorUnfilled()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .LineTo(new Vector2(6, 6))
            .LineTo(new Vector2(2, 6))
            .Close()
            .Build();
        var style = new StrokeStyle(2f);

        // Act
        var surface = RenderStroke(path, style, 8, 8);

        // Assert
        Assert.Equal((byte)255, surface[1, 3].A);
        Assert.Equal((byte)0, surface[3, 3].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that two independently-emitted OUTER stroke outlines from ONE
    ///     <see cref="PathStroker.Stroke"/> call - an open-line outline and an unrelated
    ///     point-cap circle - whose areas overlap render the overlap region as FULLY FILLED, not
    ///     as an unfilled hole.
    /// </summary>
    /// <remarks>
    ///     Before the winding-normalization fix, an open-line outline was always wound clockwise
    ///     while a point-cap circle was always wound counterclockwise (see
    ///     the review thread that reported this defect). <see cref="PathStroker.Stroke"/> emits each stroked
    ///     subpath's outline as an independent closed subpath in one output <see cref="Path"/>,
    ///     documented to be filled with <see cref="FillRule.NonZero"/>; when two such outlines
    ///     overlap with opposite signed winding, their winding numbers in the overlap cancel to
    ///     zero and <see cref="FillRule.NonZero"/> incorrectly renders a hole there instead of
    ///     solid fill. This path strokes a horizontal line (producing a rectangle-shaped outline,
    ///     ignoring its round-cap bulges) and, in a second subpath, a single point at the line's
    ///     midpoint using the SAME <see cref="StrokeStyle.Width"/> (so the resulting cap circle's
    ///     radius exactly equals the line's half-width) - the circle therefore sits entirely
    ///     within the line's rectangle. Before the fix, the whole circle region cancels to an
    ///     unfilled hole; after the fix, it reinforces and stays solidly filled.
    /// </remarks>
    [Fact]
    public void PathStroker_Stroke_OverlappingLineAndPointCapOutlines_FillsOverlapRegionSolid()
    {
        // Arrange: a horizontal line from (2,4) to (10,4), plus a zero-length point subpath at
        // its midpoint (6,4), both stroked with the same Width: 4 (half-width 2) Round-cap style.
        // The point subpath's round cap becomes a radius-2 circle centered at (6,4), which is
        // entirely enclosed within the line's rectangle-shaped outline (x:[2,10], y:[2,6]).
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 4))
            .LineTo(new Vector2(10, 4))
            .MoveTo(new Vector2(6, 4))
            .LineTo(new Vector2(6, 4))
            .Build();
        var style = new StrokeStyle(4f, cap: LineCap.Round);

        // Act
        var surface = RenderStroke(path, style, 13, 8);

        // Assert: the point-cap circle's center - deep inside both the circle and the
        // surrounding line rectangle - must render fully opaque, not as an unfilled hole.
        Assert.Equal((byte)255, surface[6, 4].A);
        Assert.Equal((byte)255, surface[5, 3].A);
        Assert.Equal((byte)255, surface[7, 4].A);
    }

    /// <summary>
    ///     Proves that a closed contour's inner (hole) corner is always the geometrically exact
    ///     offset-edge intersection, never the outer <see cref="LineJoin"/> style stylization,
    ///     using a Bevel join where the two constructions diverge sharply.
    /// </summary>
    /// <remarks>
    ///     The rectangle (4,4)-(12,4)-(12,12)-(4,12) stroked with width 4 (half-width 2) must
    ///     produce an exact 4x4 hole ring at (6,6)-(10,6)-(10,10)-(6,10). This asserts directly on
    ///     the polygon ring vertices produced by the internal <see cref="StrokeOutliner.Outline"/>
    ///     API rather than sampling rasterized pixels: for this convex rectangle, a buggy
    ///     implementation that instead bevels the inner corner produces the self-intersecting
    ///     eight-vertex ring (6,12)(4,10)(12,10)(10,12)(10,4)(12,6)(4,6)(6,4), which happens to
    ///     rasterize to the exact same pixel coverage as the correct four-vertex ring under
    ///     FillRule.NonZero (the extra self-overlapping "wings" cancel out) - so pixel sampling at
    ///     any coordinate cannot actually distinguish correct from buggy geometry here, while
    ///     asserting the exact ring vertex set does.
    /// </remarks>
    [Fact]
    public void PathStroker_Stroke_ClosedRectangleBevelJoin_InnerCornerMatchesExactIntersectionNotBevel()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(12, 4), new(12, 12), new(4, 12) };
        var style = new StrokeStyle(4f, join: LineJoin.Bevel);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: the ring touching the exact intersection point (6,6) is precisely the 4-vertex
        // square hole, not an 8-vertex self-intersecting beveled ring
        var innerRing = FindRingContaining(polygons, new Vector2(6, 6));
        Assert.Equal(4, innerRing.Count);
        Assert.Contains(new Vector2(10, 6), innerRing);
        Assert.Contains(new Vector2(10, 10), innerRing);
        Assert.Contains(new Vector2(6, 10), innerRing);
    }

    /// <summary>
    ///     Proves that a closed contour's inner (hole) corner is always the geometrically exact
    ///     offset-edge intersection, never a rounded arc, using a Round join where the two
    ///     constructions diverge sharply.
    /// </summary>
    /// <remarks>
    ///     Same rectangle and ring-vertex reasoning as
    ///     <see cref="PathStroker_Stroke_ClosedRectangleBevelJoin_InnerCornerMatchesExactIntersectionNotBevel"/>:
    ///     a buggy implementation that rounds the inner corner instead of forcing the exact
    ///     intersection would tessellate an arc at every inner corner, growing the inner ring well
    ///     past 4 vertices even though (as with Bevel) the rasterized pixel coverage of that buggy
    ///     ring happens to coincide with the correct ring's coverage for this convex rectangle.
    /// </remarks>
    [Fact]
    public void PathStroker_Stroke_ClosedRectangleRoundJoin_InnerCornerMatchesExactIntersectionNotArc()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(12, 4), new(12, 12), new(4, 12) };
        var style = new StrokeStyle(4f, join: LineJoin.Round);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: the ring touching the exact intersection point (6,6) is precisely the 4-vertex
        // square hole, not a tessellated rounded-corner ring
        var innerRing = FindRingContaining(polygons, new Vector2(6, 6));
        Assert.Equal(4, innerRing.Count);
        Assert.Contains(new Vector2(10, 6), innerRing);
        Assert.Contains(new Vector2(10, 10), innerRing);
        Assert.Contains(new Vector2(6, 10), innerRing);
    }

    /// <summary>
    ///     Proves that a Miter join on a closed contour continues to produce the same exact
    ///     inner-corner ring as Bevel/Round now do, confirming the local-turn-based outer/inner
    ///     distinction does not regress the Miter path (which already computed an edge
    ///     intersection on both sides).
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedRectangleMiterJoin_InnerCornerMatchesExactIntersection()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(12, 4), new(12, 12), new(4, 12) };
        var style = new StrokeStyle(4f, join: LineJoin.Miter);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert: the ring touching the exact intersection point (6,6) is precisely the 4-vertex
        // square hole
        var innerRing = FindRingContaining(polygons, new Vector2(6, 6));
        Assert.Equal(4, innerRing.Count);
        Assert.Contains(new Vector2(10, 6), innerRing);
        Assert.Contains(new Vector2(10, 10), innerRing);
        Assert.Contains(new Vector2(6, 10), innerRing);
    }

    /// <summary>
    ///     Proves that a CONCAVE (reflex-vertex) closed contour resolves the styled join versus
    ///     the exact offset-edge intersection independently at each vertex, from the LOCAL turn
    ///     direction there, rather than from a single ring-wide outer/inner assignment.
    /// </summary>
    /// <remarks>
    ///     The L-shaped hexagon (0,0)-(6,0)-(6,3)-(3,3)-(3,6)-(0,6) has five ordinary convex
    ///     vertices and exactly one reflex (concave) vertex at (3,3), so which of the two offset
    ///     rings is locally convex there flips relative to every other vertex. Stroked with width
    ///     2 (half-width 1) and a Bevel join, pixel [2,2] sits just inside the reflex vertex's
    ///     inner corner: under the corrected local-turn logic, the ring that is locally convex at
    ///     (3,3) is styled with a Bevel chord between (1,5) and (2,5) [a different ring than the
    ///     one styled at every other, convex, vertex], leaving [2,2] half-covered (alpha 128) by
    ///     that diagonal chord. A global (pre-fix) outer/inner assignment gets this backwards -
    ///     it bevels the wrong ring at (3,3) and forces the other ring to the exact intersection
    ///     point (2,2) - leaving pixel [2,2] fully covered (alpha 255) instead. Pixel [5,1] near
    ///     the ordinary convex corner (6,0)-(6,3) is unaffected by the bug either way, confirming
    ///     the fix does not disturb correct convex-vertex joins, and pixel [8,8] confirms the
    ///     stroke does not spuriously extend into the contour's unstroked exterior.
    /// </remarks>
    [Fact]
    public void PathStroker_Stroke_ConcaveClosedContourBevelJoin_AppliesJoinsByLocalVertexConvexity()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(6, 0))
            .LineTo(new Vector2(6, 3))
            .LineTo(new Vector2(3, 3))
            .LineTo(new Vector2(3, 6))
            .LineTo(new Vector2(0, 6))
            .Close()
            .Build();
        var style = new StrokeStyle(2f, join: LineJoin.Bevel);

        // Act
        var surface = RenderStroke(path, style, 10, 10);

        // Assert
        Assert.Equal((byte)128, surface[2, 2].A);
        Assert.Equal((byte)255, surface[5, 1].A);
        Assert.Equal((byte)0, surface[8, 8].A);
    }

    /// <summary>
    ///     Finds the single polygon ring produced by <see cref="StrokeOutliner.Outline"/> that
    ///     contains the given vertex, failing the test if zero or more than one ring qualifies.
    /// </summary>
    private static List<Vector2> FindRingContaining(List<List<Vector2>> polygons, Vector2 vertex)
    {
        List<Vector2>? found = null;
        foreach (var polygon in polygons)
        {
            if (!polygon.Contains(vertex))
            {
                continue;
            }

            Assert.Null(found);
            found = polygon;
        }

        Assert.NotNull(found);
        return found;
    }

    /// <summary>
    ///     Proves that a closed subpath reduced to exactly two distinct points (a zero-area,
    ///     degenerate closed contour that immediately doubles back over the same segment) still
    ///     renders as a stroked line segment, rather than vanishing entirely because its coincident
    ///     "outer" and "inner" offset rings would otherwise be forced into opposite winding and
    ///     cancel under FillRule.NonZero. The expected pixel coverage matches the equivalent
    ///     open, butt-capped, two-point stroke exactly (see
    ///     <see cref="PathStroker_Stroke_HorizontalLineButtCap_FillsExactRectangleNoExtension"/>).
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedTwoPointSubpath_FillsStrokedSegmentAreaNotEmpty()
    {
        // Arrange: a closed subpath (M ... L ... Z) with only two distinct points
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Close()
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act: rasterize and sample actual pixel coverage, not just outline generation
        var surface = RenderStroke(path, style, 8, 5);

        // Assert: coverage matches the equivalent open two-point butt-capped stroke
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[5, 2].A);
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a closed subpath reduced to three or more DISTINCT but COLLINEAR points
    ///     (a zero-area closed contour tracing back and forth along a single line) still renders
    ///     as a stroked line segment, using the same pixel coverage as the equivalent open,
    ///     butt-capped stroke, rather than vanishing entirely. This generalizes
    ///     <see cref="PathStroker_Stroke_ClosedTwoPointSubpath_FillsStrokedSegmentAreaNotEmpty"/>
    ///     to more than two collinear points.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedThreePointCollinearSubpath_FillsStrokedSegmentAreaNotEmpty()
    {
        // Arrange: a closed subpath (M ... L ... L ... Z) with three distinct, collinear points
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(4, 2))
            .LineTo(new Vector2(6, 2))
            .Close()
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act: rasterize and sample actual pixel coverage, not just outline generation
        var surface = RenderStroke(path, style, 8, 5);

        // Assert: coverage matches the equivalent open two-point butt-capped stroke
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[5, 2].A);
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a dash array whose entries individually are finite but whose summed total
    ///     pattern length would overflow a naive float32 accumulation does not hang the public
    ///     <see cref="PathStroker.Stroke(Path, StrokeStyle, float)"/> entry point, when combined
    ///     with a negative dash offset. This test intentionally makes no timing assertion: it
    ///     relies only on xUnit's normal test execution completing to prove there is no infinite
    ///     loop reachable through the public stroking API.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(10, 0))
            .Build();
        var style = new StrokeStyle(2f, dashArray: [float.MaxValue, float.MaxValue], dashOffset: -1f);

        // Act
        var stroked = PathStroker.Stroke(path, style);

        // Assert: the call returned (did not hang) with a well-formed result
        Assert.NotNull(stroked);
    }

    /// <summary>
    ///     Proves that a huge-but-finite total path length combined with a fine dash span does
    ///     not hang the public <see cref="PathStroker.Stroke(Path, StrokeStyle, float)"/> entry
    ///     point, pairing with <see cref="DashSplitterTests.DashSplitter_Split_HugeFiniteTotalLengthWithFineDashSpan_FallsBackToSolidStroke"/>.
    /// </summary>
    [Fact]
    public async Task PathStroker_Stroke_HugeFiniteCoordinatesWithFineDashPattern_CompletesWithoutHanging()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(-1e20f, -1e20f))
            .LineTo(new Vector2(1e20f, 1e20f))
            .Build();
        var style = new StrokeStyle(2f, dashArray: [5f, 5f]);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var task = Task.Run(() => PathStroker.Stroke(path, style), cancellationToken);
        var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));

        // Assert: the call completed (did not hang) with a well-formed result
        Assert.Same(task, completedTask);
        var stroked = await task;
        Assert.NotNull(stroked);
    }

    /// <summary>
    ///     Proves that a sharp corner exceeding the miter limit falls back to bevel geometry.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_SharpAngleExceedingMiterLimit_FallsBackToBevel()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Miter, miterLimit: 1f);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)0, surface[9, 2].A);
        Assert.Equal((byte)128, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that dashing leaves gaps unfilled between visible stroke segments.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_DashedLine_FillsOnlyOnSegmentsLeavingGapsUnfilled()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 2))
            .LineTo(new Vector2(10, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt, dashArray: [2f, 2f]);

        // Act
        var surface = RenderStroke(path, style, 11, 5);

        // Assert
        Assert.Equal((byte)255, surface[0, 1].A);
        Assert.Equal((byte)0, surface[2, 1].A);
        Assert.Equal((byte)255, surface[4, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a round cap becomes a full circle.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathRoundCap_FillsCircleOfRadiusHalfWidth()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)148, surface[1, 1].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a square cap becomes a width-by-width square.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathSquareCap_FillsSquareOfSideWidth()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Square);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[2, 2].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a butt cap contributes no fillable area.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathButtCap_ProducesNoFilledPixels()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[2, 2].A);
    }

    /// <summary>
    ///     Proves that a single-point subpath matches the zero-length-subpath cap behavior.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_SinglePointSubpath_MatchesZeroLengthSubpathBehavior()
    {
        // Arrange
        var singlePointPath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .Build();
        var zeroLengthPath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var singlePoint = RenderStroke(singlePointPath, style, 5, 5);
        var zeroLength = RenderStroke(zeroLengthPath, style, 5, 5);

        // Assert
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(zeroLength[x, y], singlePoint[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that stroking an empty path returns <see cref="Path.Empty"/> and renders no
    ///     pixels when subsequently filled.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_EmptyPath_ReturnsEmptyPathNoOp()
    {
        // Arrange
        var style = new StrokeStyle(2f);

        // Act
        var stroked = PathStroker.Stroke(Path.Empty, style);
        var surface = new Surface(3, 3);
        PathFiller.Fill(surface, stroked, new Rgba32(255, 0, 0, 255));

        // Assert
        Assert.Same(Path.Empty, stroked);
        Assert.Equal((byte)0, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves that a null path is rejected.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_NullPath_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => PathStroker.Stroke(null!, new StrokeStyle(1f)));
    }

    /// <summary>
    ///     Proves that a null stroke style is rejected.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_NullStrokeStyle_ThrowsArgumentNullException()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .Build();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PathStroker.Stroke(path, null!));
    }

    /// <summary>
    ///     Proves that a non-positive or non-finite flatten tolerance is rejected.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void PathStroker_Stroke_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException(float tolerance)
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .Build();
        var style = new StrokeStyle(1f);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PathStroker.Stroke(path, style, tolerance));
    }

    private static Path BuildRightAnglePath() => new PathBuilder()
        .MoveTo(new Vector2(4, 4))
        .LineTo(new Vector2(8, 4))
        .LineTo(new Vector2(8, 8))
        .Build();

    private static Surface RenderStroke(Path path, StrokeStyle style, int width, int height, float flattenTolerance = 0.25f)
    {
        var stroked = PathStroker.Stroke(path, style, flattenTolerance);
        var surface = new Surface(width, height);
        PathFiller.Fill(surface, stroked, new Rgba32(255, 0, 0, 255), FillRule.NonZero, flattenTolerance);
        return surface;
    }
}
