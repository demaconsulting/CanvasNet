using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the painting of paths with segments, arcs, trails and the miter limit, asserted on pixels whose values
///     follow from the geometry.
/// </summary>
public class ScenePainterPathTests
{
    /// <summary>Opaque white.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>Opaque blue.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a scene with a white background whose root group holds the items.
    /// </summary>
    /// <param name="width">The scene width.</param>
    /// <param name="height">The scene height.</param>
    /// <param name="items">The items of the root group.</param>
    /// <returns>The scene.</returns>
    private static Scene MakeScene(double width, double height, params SceneItem[] items) =>
        new(width, height, "white", new SceneGroup("root", 0, 0) { Children = items });

    /// <summary>
    ///     Paints a scene at a placement.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="placement">The placement.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(Scene scene, int width, int height, ScenePlacement placement) =>
        new ScenePainter(new TextMeasurer()).Paint(scene, width, height, placement, Ct);

    /// <summary>
    ///     Paints a scene at a scale of one.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The surface.</returns>
    private static Surface PaintAtOne(Scene scene, int width, int height) => Paint(scene, width, height, ScenePlacement.Uniform(1));

    /// <summary>
    ///     Asserts that every pixel of a surface is white.
    /// </summary>
    /// <param name="surface">The surface.</param>
    private static void AssertBlank(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(White, surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Validates that a cubic segment is stroked along its curve and not along the chord.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_SegmentPathStrokesThroughItsCubicCurve()
    {
        // Arrange: M(5, 20) C(5, 5) (35, 5) (35, 20): B(0.5) = (20, 0.125 * 20 + 0.375 * 5 + 0.375 * 5 + 0.125 * 20) = (20, 8.75)
        var scene = MakeScene(
            40,
            30,
            new ScenePath("mark-line", [ScenePathSegment.Move(5, 20), ScenePathSegment.Cubic(5, 5, 35, 5, 35, 20)]) { Stroke = "#000000", StrokeWidth = 2 });

        // Act
        using var surface = PaintAtOne(scene, 40, 30);

        // Assert: the stroke is 2 wide around y = 8.75 at the top of the curve, so row 8 is covered across x = 20
        Assert.Equal(Black, surface[20, 8]);
        Assert.Equal(Black, surface[19, 8]);
        Assert.Equal(White, surface[20, 5]);
        Assert.Equal(White, surface[20, 12]);

        // Assert: the chord (y = 20) is not drawn and neither is the interior (the path has no fill)
        Assert.Equal(White, surface[20, 20]);
        Assert.Equal(White, surface[20, 15]);

        // Assert: the curve leaves its start going up: the first stretch is a vertical stroke from x = 4 to 6 (it drifts right
        // by 0.04 within the row, so pixel 4 is covered by 96 percent)
        Assert.True(surface[4, 19].R < 40);
        Assert.Equal(Black, surface[5, 19]);
        Assert.Equal(White, surface[2, 19]);
    }

    /// <summary>
    ///     Validates that a closed segment path is filled and that the fill does not extend beyond it.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ClosedSegmentPathFillsItsInterior()
    {
        // Arrange: a square (2, 2) to (12, 12) with a red fill, and a triangle that is left open (the fill closes it)
        var scene = MakeScene(
            40,
            20,
            new ScenePath(
                "mark-area",
                [
                    ScenePathSegment.Move(2, 2),
                    ScenePathSegment.Line(12, 2),
                    ScenePathSegment.Line(12, 12),
                    ScenePathSegment.Line(2, 12),
                    ScenePathSegment.Close(),
                ])
            { Fill = "#ff0000" },
            new ScenePath("mark-area", [ScenePathSegment.Move(20, 2), ScenePathSegment.Line(30, 2), ScenePathSegment.Line(20, 12)]) { Fill = "#ff0000" });

        // Act
        using var surface = PaintAtOne(scene, 40, 20);

        // Assert: the square covers exactly pixels 2 to 11
        Assert.Equal(Red, surface[2, 2]);
        Assert.Equal(Red, surface[11, 11]);
        Assert.Equal(Red, surface[7, 7]);
        Assert.Equal(White, surface[1, 5]);
        Assert.Equal(White, surface[12, 5]);
        Assert.Equal(White, surface[5, 12]);

        // Assert: the open triangle (20, 2), (30, 2), (20, 12) is filled up-left of its hypotenuse x + y = 32 and not beyond it
        Assert.Equal(Red, surface[21, 4]);
        Assert.Equal(Red, surface[21, 9]);
        Assert.Equal(White, surface[28, 10]);
        Assert.Equal(White, surface[35, 5]);
    }

    /// <summary>
    ///     Validates that a move starts a separate subpath, that commands before the first move are skipped and that a command
    ///     after a close continues at the start of the closed subpath.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_MoveToSegmentsStartSeparateSubpaths()
    {
        // Arrange: two filled squares in one path; two stroked pieces in one path (no stroke joins them); a path that draws
        // before its first move; and a path that continues after a close
        var scene = MakeScene(
            40,
            40,
            new ScenePath(
                "squares",
                [
                    ScenePathSegment.Move(2, 2),
                    ScenePathSegment.Line(8, 2),
                    ScenePathSegment.Line(8, 8),
                    ScenePathSegment.Line(2, 8),
                    ScenePathSegment.Close(),
                    ScenePathSegment.Move(20, 2),
                    ScenePathSegment.Line(28, 2),
                    ScenePathSegment.Line(28, 8),
                    ScenePathSegment.Line(20, 8),
                    ScenePathSegment.Close(),
                ])
            { Fill = "#ff0000" },
            new ScenePath(
                "pieces",
                [ScenePathSegment.Move(2, 10), ScenePathSegment.Line(8, 10), ScenePathSegment.Move(12, 14), ScenePathSegment.Line(18, 14)])
            { Stroke = "#000000", StrokeWidth = 2 },
            new ScenePath("skipped", [ScenePathSegment.Line(0, 20), ScenePathSegment.Cubic(1, 20, 2, 20, 39, 20), ScenePathSegment.Close()])
            { Stroke = "#ff0000", StrokeWidth = 4 },
            new ScenePath(
                "after-close",
                [ScenePathSegment.Move(2, 28), ScenePathSegment.Line(10, 28), ScenePathSegment.Close(), ScenePathSegment.Line(2, 34)])
            { Stroke = "#0000ff", StrokeWidth = 2 });

        // Act
        using var surface = PaintAtOne(scene, 40, 40);

        // Assert: both squares are filled and the gap between them is not
        Assert.Equal(Red, surface[5, 5]);
        Assert.Equal(Red, surface[24, 5]);
        Assert.Equal(White, surface[14, 5]);
        Assert.Equal(White, surface[10, 5]);

        // Assert: the pieces are stroked (rows 9 and 10 of the first, 13 and 14 of the second), and the line from (8, 10) to
        // (12, 14) that a join would draw is missing at its middle (10, 12)
        Assert.Equal(Black, surface[5, 10]);
        Assert.Equal(Black, surface[15, 14]);
        Assert.Equal(White, surface[10, 12]);

        // Assert: the commands before the first move draw nothing
        Assert.Equal(White, surface[20, 20]);
        Assert.Equal(White, surface[20, 19]);

        // Assert: after the close the path continues at its start (2, 28): the vertical piece to (2, 34) covers x = 1 and 2
        Assert.Equal(Blue, surface[1, 31]);
        Assert.Equal(Blue, surface[2, 31]);
        Assert.Equal(White, surface[4, 31]);
        Assert.Equal(Blue, surface[6, 27]);
    }

    /// <summary>
    ///     Validates that items with a coordinate that is not finite draw nothing.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_NonFiniteSegmentsArcsAndTrailsDrawNothing()
    {
        // Arrange: every item would cover a large part of the surface if it were drawn
        var scene = MakeScene(
            30,
            30,
            new ScenePath("nan", [ScenePathSegment.Move(0, 0), ScenePathSegment.Line(double.NaN, 10), ScenePathSegment.Line(20, 20)]) { Fill = "red", Stroke = "red" },
            new ScenePath("cubic", [ScenePathSegment.Move(0, 0), ScenePathSegment.Cubic(1, 1, double.PositiveInfinity, 2, 20, 20)]) { Stroke = "red" },
            new SceneArc("arc-x", double.NaN, 15) { OuterRadius = 10, EndAngle = 3, Fill = "red" },
            new SceneArc("arc-r", 15, 15) { OuterRadius = double.PositiveInfinity, EndAngle = 3, Fill = "red" },
            new SceneArc("arc-angle", 15, 15) { OuterRadius = 10, EndAngle = double.NaN, Fill = "red" },
            new SceneTrail("trail-nan", [[new SceneTrailPoint(double.NaN, 5, 4), new SceneTrailPoint(20, 5, 4)], [new SceneTrailPoint(5, 20, double.NaN), new SceneTrailPoint(20, 20, 4)]]) { Fill = "red" },
            new SceneTrail("trail-size", [[new SceneTrailPoint(2, 10, 0), new SceneTrailPoint(20, 10, -3), new SceneTrailPoint(25, 10, double.PositiveInfinity)]]) { Fill = "red" });

        // Act
        using var surface = PaintAtOne(scene, 30, 30);

        // Assert
        AssertBlank(surface);
    }

    /// <summary>
    ///     Validates that a path whose end point is beyond the magnitude bound (or overflows a single) draws nothing, and that a cubic whose control point is beyond it is drawn as a line to its
    ///     end point instead of being flattened at a huge size.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_SegmentsBeyondTheMagnitudeBoundDrawNothingOrBecomeLines()
    {
        // Arrange: paths that would cross the whole surface if they were drawn; the end point of the last command is 2,000,000 (far above the bound of 32,768), 1e300
        // (finite as a double, infinity as a single) or -1e7 on the y axis
        Assert.Equal(32768, ScenePainter.MaxMagnitude);
        var far = MakeScene(
            30,
            30,
            new ScenePath("far-line", [ScenePathSegment.Move(0, 0), ScenePathSegment.Line(15, 15), ScenePathSegment.Line(2e6, 5)]) { Fill = "red", Stroke = "red" },
            new ScenePath("overflow", [ScenePathSegment.Move(0, 0), ScenePathSegment.Line(1e300, 5)]) { Stroke = "red", StrokeWidth = 4 },
            new ScenePath("far-cubic", [ScenePathSegment.Move(0, 0), ScenePathSegment.Cubic(5, 5, 10, 10, 20, -1e7)]) { Stroke = "red", StrokeWidth = 4 },
            new ScenePath("far-move", [ScenePathSegment.Move(-2e6, 5), ScenePathSegment.Line(20, 20)]) { Stroke = "red", StrokeWidth = 4 });

        // Act
        using var surface = PaintAtOne(far, 30, 30);

        // Assert: nothing at all
        AssertBlank(surface);

        // Arrange: a cubic from (2, 10) to (28, 10) whose control points are 2,000,000 above and below (beyond the bound, but finite), and one that overflows a single, 2 wide
        var degraded = MakeScene(
            30,
            30,
            new ScenePath("cubic", [ScenePathSegment.Move(2, 10), ScenePathSegment.Cubic(5, 2e6, 25, -2e6, 28, 10)]) { Stroke = "#000000", StrokeWidth = 2 },
            new ScenePath("overflow-control", [ScenePathSegment.Move(2, 20), ScenePathSegment.Cubic(5, 1e300, 25, -1e300, 28, 20)]) { Stroke = "#000000", StrokeWidth = 2 });

        // Act
        using var lines = PaintAtOne(degraded, 30, 30);

        // Assert: both are the straight line between their end points, 2 wide: the rows 9 and 10 (the line is at y = 10) and 19 and 20 are covered between the caps at x = 2 and 28
        foreach (var y in new[] { 9, 10, 19, 20 })
        {
            Assert.Equal(Black, lines[15, y]);
            Assert.Equal(Black, lines[3, y]);
            Assert.Equal(Black, lines[27, y]);
        }

        // Assert: and nothing of the huge curve is drawn above or below them
        Assert.Equal(White, lines[15, 6]);
        Assert.Equal(White, lines[15, 13]);
        Assert.Equal(White, lines[15, 16]);
        Assert.Equal(White, lines[15, 23]);
        Assert.Equal(White, lines[1, 10]);
    }

    /// <summary>
    ///     Validates that an arc whose center, radius or corner radius is beyond the magnitude bound draws nothing, and that an arc inside the bound is drawn.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ArcsBeyondTheMagnitudeBoundDrawNothing()
    {
        // Arrange: full circles that would cover the surface; the radii are above the bound (the geometry alone accepts up to 1e9), the center is 1e7 away and the corner radius is above the bound
        var far = MakeScene(
            30,
            30,
            new SceneArc("center", 1e7, 15) { OuterRadius = 10, EndAngle = 2 * Math.PI, Fill = "red" },
            new SceneArc("outer", 15, 15) { OuterRadius = 5e8, EndAngle = 2 * Math.PI, Fill = "red" },
            new SceneArc("inner", 15, 15) { InnerRadius = 2e6, OuterRadius = 10, EndAngle = 2 * Math.PI, Fill = "red" },
            new SceneArc("corner", 15, 15) { OuterRadius = 10, EndAngle = 2 * Math.PI, CornerRadius = 1.5e6, Fill = "red" },
            new SceneArc("overflow", 15, 15) { OuterRadius = 1e300, EndAngle = 2 * Math.PI, Fill = "red" });

        // Act
        using var blank = PaintAtOne(far, 30, 30);

        // Assert
        AssertBlank(blank);

        // Act: a circle of radius 10 inside the bound is drawn
        using var drawn = PaintAtOne(MakeScene(30, 30, new SceneArc("ok", 15, 15) { OuterRadius = 10, EndAngle = 2 * Math.PI, Fill = "#ff0000" }), 30, 30);

        // Assert
        Assert.Equal(Red, drawn[15, 15]);
        Assert.Equal(White, drawn[1, 1]);
    }

    /// <summary>
    ///     Validates that a trail point beyond the magnitude bound (in a coordinate or in the size) ends its run, as a point that is not finite does.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TrailPointsBeyondTheMagnitudeBoundEndTheRun()
    {
        // Arrange: size 6 (radius 3) at y = 10; x = 2, 15, then a point 2,000,000 away, then 25 and 28; and a second run at y = 25 whose size 2,000,000 ends it
        var scene = MakeScene(
            40,
            40,
            new SceneTrail(
                "mark-trail",
                [
                    [
                        new SceneTrailPoint(2, 10, 6),
                        new SceneTrailPoint(15, 10, 6),
                        new SceneTrailPoint(2e6, 10, 6),
                        new SceneTrailPoint(25, 10, 6),
                        new SceneTrailPoint(36, 10, 6),
                    ],
                    [
                        new SceneTrailPoint(2, 25, 6),
                        new SceneTrailPoint(15, 25, 2e6),
                        new SceneTrailPoint(25, 25, 6),
                        new SceneTrailPoint(36, 25, 6),
                    ],
                ])
            { Fill = "#000000" });

        // Act
        using var surface = PaintAtOne(scene, 40, 40);

        // Assert: the first run draws two capsules, 2 to 15 and 25 to 36 (the far point is skipped and the points on both sides do not join), with the gap from 18 to 22 between
        Assert.Equal(Black, surface[8, 10]);
        Assert.Equal(White, surface[20, 10]);
        Assert.Equal(Black, surface[30, 10]);

        // Assert: the huge size ends its run: 2 and 15 are not joined (only the first capsule, if any, would cross x = 8) and 25 to 36 is drawn
        Assert.Equal(White, surface[8, 25]);
        Assert.Equal(White, surface[20, 25]);
        Assert.Equal(Black, surface[30, 25]);
    }

    /// <summary>
    ///     Validates the cost estimate of a trail capsule and that a trail is painted in chunks of a bounded cost: the number of capsules per chunk follows from the weights, and a chunk
    ///     boundary polls the cancellation token before it paints anything.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TrailIsPaintedInChunksWithACancellationPollBetweenThem()
    {
        // Assert: the weight is 1 + sqrt(radius * scale / 0.02): 1 for no radius, 11 for a radius of 2, 906.1 for the largest radius that the bound allows (half of the size 32,768)
        Assert.Equal(1, ScenePainter.TrailCapsuleWeight(0, 1), 9);
        Assert.Equal(11, ScenePainter.TrailCapsuleWeight(2, 1), 3);
        Assert.Equal(906.1, ScenePainter.TrailCapsuleWeight(16384, 1), 1);
        Assert.Equal(21, ScenePainter.TrailCapsuleWeight(2, 4), 3);

        // Arrange: 450 points of size 32,768 (radius 16,384, the largest the bound allows) in a row; a capsule weighs 906.1, so a chunk of 100,000 is complete after the 111th capsule
        // (110 * 906.1 = 99,671 is below it, 111 * 906.1 = 100,577 is not), that is with the 112th point
        var points = Enumerable.Range(0, 450).Select(i => new SceneTrailPoint(i, 10, 32768)).ToArray();
        var scene = MakeScene(40, 40, new SceneTrail("mark-trail", [points]) { Fill = "#000000" });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var chunks = new List<int>();
        var painter = new ScenePainter(new TextMeasurer())
        {
            TrailChunkFlushed = capsules =>
            {
                chunks.Add(capsules);
                cancellation.Cancel();
            },
        };

        // Act / Assert: the first chunk is reported with 111 capsules and the poll that follows stops the painting before any capsule is filled (no second chunk is reached)
        Assert.Throws<OperationCanceledException>(() => painter.Paint(scene, 40, 40, ScenePlacement.Uniform(1), cancellation.Token).Dispose());
        Assert.Equal([111], chunks);

        // Arrange: an ordinary trail (100 points of size 4: a weight of 11 each, 99 capsules) is one chunk, which the observer never sees
        var ordinary = MakeScene(120, 20, new SceneTrail("mark-trail", [Enumerable.Range(0, 100).Select(i => new SceneTrailPoint(10 + i, 10, 4)).ToArray()]) { Fill = "#000000" });
        var seen = new List<int>();
        using var surface = new ScenePainter(new TextMeasurer()) { TrailChunkFlushed = seen.Add }.Paint(ordinary, 120, 20, ScenePlacement.Uniform(1), Ct);

        // Assert: drawn whole, without a flush
        Assert.Empty(seen);
        Assert.Equal(Black, surface[60, 10]);
    }

    /// <summary>
    ///     Validates that the bound is applied to the size in surface pixels, that is to the value times the scale of the placement: a path that is drawn at a scale of one draws nothing at a scale
    ///     of two, and a stroke width beyond the bound is not stroked.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TheBoundAppliesToTheSizeInSurfacePixels()
    {
        // Arrange: a 2 wide line from (0, 15) to (20000, 15): 20,000 pixels at a scale of one (within the bound of 32,768), 40,000 at a scale of two (beyond it)
        var scene = MakeScene(30, 30, new ScenePath("far", [ScenePathSegment.Move(0, 15), ScenePathSegment.Line(20000, 15)]) { Stroke = "#000000", StrokeWidth = 2 });

        // Act
        using var one = PaintAtOne(scene, 30, 30);
        using var two = Paint(scene, 60, 60, ScenePlacement.Uniform(2));

        // Assert: at one the line covers the rows 14 and 15 (it is 2 wide around y = 15); at two it is not drawn
        Assert.Equal(Black, one[10, 14]);
        Assert.Equal(Black, one[10, 15]);
        Assert.Equal(White, one[10, 12]);
        AssertBlank(two);

        // Arrange: a stroke width of 40,000 (beyond the bound at a scale of one) and 1e300 on a short line, which would cover the whole surface
        var wide = MakeScene(
            30,
            30,
            new ScenePath("wide", [ScenePathSegment.Move(2, 15), ScenePathSegment.Line(28, 15)]) { Stroke = "#000000", StrokeWidth = 40000 },
            new ScenePath("huge", [ScenePathSegment.Move(2, 15), ScenePathSegment.Line(28, 15)]) { Stroke = "#000000", StrokeWidth = 1e300 });

        // Act
        using var strokes = PaintAtOne(wide, 30, 30);

        // Assert
        AssertBlank(strokes);

        // Arrange: an arc of radius 20,000 is within the bound at a scale of one and beyond it at a scale of two
        var arc = MakeScene(30, 30, new SceneArc("ring", 15, 15) { InnerRadius = 19990, OuterRadius = 20000, EndAngle = 2 * Math.PI, Fill = "#000000" });

        // Act: at a scale of two nothing is drawn (the ring would also miss the surface at one, so only the cost is what the bound is about)
        using var ring = Paint(arc, 60, 60, ScenePlacement.Uniform(2));

        // Assert
        AssertBlank(ring);
    }

    /// <summary>
    ///     Validates that a group placed beyond the magnitude bound draws nothing.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GroupBeyondTheMagnitudeBoundDrawsNothing()
    {
        // Arrange: a group at x = 1e300 (infinity as a single) and one at y = 2e6 with a path that would be visible without the offset
        var path = new ScenePath("p", [ScenePathSegment.Move(0, 0), ScenePathSegment.Line(20, 20)]) { Stroke = "red", StrokeWidth = 4 };
        var scene = new Scene(
            30,
            30,
            "white",
            new SceneGroup("root", 0, 0)
            {
                Children =
                [
                    new SceneGroup("far-x", 1e300, 0) { Children = [path] },
                    new SceneGroup("far-y", 0, 2e6) { Children = [path] },
                ],
            });

        // Act
        using var surface = PaintAtOne(scene, 30, 30);

        // Assert
        AssertBlank(surface);
    }

    /// <summary>
    ///     Validates that an arc fills only its wedge and leaves the hole of an annular sector empty.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ArcFillsOnlyItsWedgeAndLeavesTheHoleEmpty()
    {
        // Arrange: an annular sector (20 to 40) over the right half (from 12 o'clock to 6 o'clock) and a pie quadrant below-right
        var scene = MakeScene(
            200,
            100,
            new SceneArc("mark-arc", 50, 50) { InnerRadius = 20, OuterRadius = 40, StartAngle = 0, EndAngle = Math.PI, Fill = "#ff0000" },
            new SceneArc("mark-arc", 150, 50) { OuterRadius = 40, StartAngle = Math.PI / 2, EndAngle = Math.PI, Fill = "#ff0000" });

        // Act
        using var surface = PaintAtOne(scene, 200, 100);

        // Assert: the right half of the ring is red (radius 30 at 3 o'clock, 35 at the top and bottom edges)
        Assert.Equal(Red, surface[80, 50]);
        Assert.Equal(Red, surface[51, 15]);
        Assert.Equal(Red, surface[51, 85]);

        // Assert: the left half, the hole, the inside of the hole at the center and the outside of the ring are not
        Assert.Equal(White, surface[20, 50]);
        Assert.Equal(White, surface[48, 15]);
        Assert.Equal(White, surface[60, 50]);
        Assert.Equal(White, surface[50, 50]);
        Assert.Equal(White, surface[95, 50]);

        // Assert: the pie quadrant covers the bottom-right of its center and nothing else
        Assert.Equal(Red, surface[170, 70]);
        Assert.Equal(Red, surface[152, 52]);
        Assert.Equal(White, surface[130, 70]);
        Assert.Equal(White, surface[170, 30]);
        Assert.Equal(White, surface[130, 30]);
        Assert.Equal(White, surface[185, 85]);
    }

    /// <summary>
    ///     Validates the stroke and the opacity of an arc.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ArcStrokeAndOpacityFollowTheItem()
    {
        // Arrange: a half transparent red pie quadrant; a stroked quadrant without a fill; a quadrant with no opacity
        var scene = MakeScene(
            200,
            100,
            new SceneArc("fill", 50, 50) { OuterRadius = 40, StartAngle = Math.PI / 2, EndAngle = Math.PI, Fill = "#ff0000", Opacity = 0.5 },
            new SceneArc("stroke", 150, 50) { OuterRadius = 40, StartAngle = Math.PI / 2, EndAngle = Math.PI, Stroke = "#000000", StrokeWidth = 4 },
            new SceneArc("hidden", 100, 50) { OuterRadius = 40, StartAngle = Math.PI / 2, EndAngle = Math.PI, Fill = "#ff0000", Opacity = 0 });

        // Act
        using var surface = PaintAtOne(scene, 200, 100);

        // Assert: half transparent red over white is (255, 127, 127), and only inside the quadrant
        Assert.Equal(new Rgba32(255, 127, 127, 255), surface[70, 70]);
        Assert.Equal(White, surface[30, 70]);
        Assert.Equal(White, surface[70, 30]);

        // Assert: the stroke is 4 wide and centered on the edges: the edge to the right of the center (y = 50) covers rows 48 to 51
        Assert.Equal(Black, surface[170, 49]);
        Assert.Equal(Black, surface[170, 50]);
        Assert.Equal(White, surface[170, 45]);

        // Assert: the straight edge down from the center covers columns 148 to 151, and the interior stays unfilled
        Assert.Equal(Black, surface[149, 70]);
        Assert.Equal(White, surface[165, 70]);

        // Assert: an item with no opacity draws nothing
        Assert.Equal(White, surface[120, 70]);
    }

    /// <summary>
    ///     Validates that the placement (scale and translation) applies to segment paths and arcs.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PathsAndArcsScaleWithThePlacement()
    {
        // Arrange: a segment square (5, 5) to (10, 10) and a pie quadrant (center 25, 25, radius 20) below-right, at a scale
        // of two with the origin moved to (4, 6): the square covers x = 14 to 23 and y = 16 to 25; the arc has its
        // center at (54, 56) and a radius of 40
        var scene = MakeScene(
            50,
            50,
            new ScenePath(
                "mark-area",
                [ScenePathSegment.Move(5, 5), ScenePathSegment.Line(10, 5), ScenePathSegment.Line(10, 10), ScenePathSegment.Line(5, 10), ScenePathSegment.Close()])
            { Fill = "#ff0000" },
            new SceneArc("mark-arc", 25, 25) { OuterRadius = 20, StartAngle = Math.PI / 2, EndAngle = Math.PI, Fill = "#ff0000" });

        // Act
        using var surface = Paint(scene, 110, 110, new ScenePlacement(2, 4, 6));

        // Assert: the scaled and translated square
        Assert.Equal(Red, surface[14, 16]);
        Assert.Equal(Red, surface[23, 25]);
        Assert.Equal(White, surface[13, 16]);
        Assert.Equal(White, surface[24, 25]);
        Assert.Equal(White, surface[14, 26]);

        // Assert: the scaled and translated quadrant: (27, 27) from the center is at radius 38.9 < 40, the mirror points are not drawn
        Assert.Equal(Red, surface[54 + 27, 56 + 27]);
        Assert.Equal(White, surface[54 - 28, 56 + 28]);
        Assert.Equal(White, surface[54 + 28, 56 - 28]);
        Assert.Equal(White, surface[54 + 30, 56 + 30]);
    }

    /// <summary>
    ///     Validates that the width of a trail follows the size of the points: the half width is 2 at the first point and 6 at the last.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TrailWidthFollowsTheSizeOfEachPoint()
    {
        // Arrange: along y = 20 from (10, size 4) to (50, size 12): the half width is r1 + (r2 - r1) * t = 2 + 4 * t
        var scene = MakeScene(70, 40, new SceneTrail("mark-trail", [[new SceneTrailPoint(10, 20, 4), new SceneTrailPoint(50, 20, 12)]]) { Fill = "#000000" });

        // Act
        using var surface = PaintAtOne(scene, 70, 40);

        // Assert: at column 14 (center 14.5, t = 0.1125) the half width is 2.45: rows 18 to 21 are inside, rows up to 16 and from 23 are not
        for (var y = 18; y <= 21; y++)
        {
            Assert.Equal(Black, surface[14, y]);
        }

        Assert.Equal(White, surface[14, 16]);
        Assert.Equal(White, surface[14, 23]);

        // Assert: at the middle (column 30, half width 4.0 to 4.1): rows 17 to 22 are inside, rows up to 14 and from 25 are not
        for (var y = 17; y <= 22; y++)
        {
            Assert.Equal(Black, surface[30, y]);
        }

        Assert.Equal(White, surface[30, 14]);
        Assert.Equal(White, surface[30, 25]);

        // Assert: near the end (column 46, t = 0.9125, half width 5.65): rows 15 to 24 are inside, rows up to 13 and from 26 are not
        for (var y = 15; y <= 24; y++)
        {
            Assert.Equal(Black, surface[46, y]);
        }

        Assert.Equal(White, surface[46, 13]);
        Assert.Equal(White, surface[46, 26]);

        // Assert: the ends are half circles of radii 2 and 6 (pixels that lie wholly inside or outside of them)
        Assert.Equal(Black, surface[9, 19]);
        Assert.Equal(Black, surface[9, 20]);
        Assert.Equal(White, surface[9, 17]);
        Assert.Equal(White, surface[5, 20]);
        Assert.Equal(Black, surface[54, 17]);
        Assert.Equal(Black, surface[54, 19]);
        Assert.Equal(White, surface[57, 20]);
        Assert.Equal(White, surface[55, 14]);
    }

    /// <summary>
    ///     Validates that runs are separate, that a single point or an unusable point ends a run and that coincident points draw a disc.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TrailBreaksAtRunBoundariesAndDrawsCapsForCoincidentPoints()
    {
        // Arrange: runs of size 6 (radius 3) at y = 10: 5 to 15 and 25 to 35; a lone point at 50; a pair of coincident points of
        // size 8 at 60; and a run at y = 25 with a point of size 0 at x = 25 that splits it into 5 to 15 and 35 to 45
        var scene = MakeScene(
            70,
            40,
            new SceneTrail(
                "mark-trail",
                [
                    [new SceneTrailPoint(5, 10, 6), new SceneTrailPoint(15, 10, 6)],
                    [new SceneTrailPoint(25, 10, 6), new SceneTrailPoint(35, 10, 6)],
                    [new SceneTrailPoint(50, 10, 6)],
                    [new SceneTrailPoint(60, 10, 8), new SceneTrailPoint(60, 10, 8)],
                    [
                        new SceneTrailPoint(5, 25, 6),
                        new SceneTrailPoint(15, 25, 6),
                        new SceneTrailPoint(25, 25, 0),
                        new SceneTrailPoint(35, 25, 6),
                        new SceneTrailPoint(45, 25, 6),
                    ],
                ])
            { Fill = "#000000" });

        // Act
        using var surface = PaintAtOne(scene, 70, 40);

        // Assert: the first two runs are drawn with caps (x from 2 to 18 and from 22 to 38) and the gap between them is not
        Assert.Equal(Black, surface[10, 10]);
        Assert.Equal(Black, surface[16, 10]);
        Assert.Equal(White, surface[20, 10]);
        Assert.Equal(Black, surface[23, 10]);
        Assert.Equal(Black, surface[30, 10]);

        // Assert: a run of one point draws nothing
        Assert.Equal(White, surface[50, 10]);

        // Assert: two coincident points of size 8 draw a disc of radius 4 (x from 56 to 64)
        Assert.Equal(Black, surface[60, 10]);
        Assert.Equal(Black, surface[62, 10]);
        Assert.Equal(Black, surface[57, 10]);
        Assert.Equal(White, surface[65, 10]);
        Assert.Equal(White, surface[54, 10]);

        // Assert: the point of size 0 ends the run: its neighbors have no capsule between them, only their own caps
        Assert.Equal(Black, surface[10, 25]);
        Assert.Equal(Black, surface[16, 25]);
        Assert.Equal(White, surface[20, 25]);
        Assert.Equal(White, surface[25, 25]);
        Assert.Equal(White, surface[30, 25]);
        Assert.Equal(Black, surface[33, 25]);
        Assert.Equal(Black, surface[40, 25]);
    }

    /// <summary>
    ///     Validates that the miter limit decides between a miter and a bevel at a sharp corner, and its clamping.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_StrokeMiterLimitBevelsASharpJoin()
    {
        // Arrange: legs from (10, 30) up to the apex (20, 10) and down to (30, 30), 4 wide: the half angle between the legs is
        // atan(10 / 20) = 26.57 degrees, so the miter ratio is 1 / sin(26.57 degrees) = 2.236 and its tip is 4.47 above the apex
        // (y = 5.53); a bevel stops at y = 9.106, that is 10 - 2 * 0.447 (the offset points of the two legs at the apex lie 2 from it, perpendicular to the legs)
        ScenePath Make(double? limit) =>
            new("mark-line", [new ScenePoint(10, 30), new ScenePoint(20, 10), new ScenePoint(30, 30)])
            {
                Stroke = "#000000",
                StrokeWidth = 4,
                StrokeMiterLimit = limit,
            };

        // Act: the default of 4, a limit above the ratio, a limit below it, a limit below 1, not a number and a huge one
        var results = new Dictionary<string, (Rgba32 Spike, Rgba32 Beside)>();
        foreach (var (name, limit) in new (string, double?)[]
        {
            ("default", null),
            ("above", 3),
            ("below", 2),
            ("tiny", 0.5),
            ("nan", double.NaN),
            ("huge", 1e9),
        })
        {
            using var surface = PaintAtOne(MakeScene(40, 40, Make(limit)), 40, 40);
            results[name] = (surface[19, 7], surface[20, 8]);
        }

        // Assert: with a miter the spike above the apex is dark (it covers most of pixel 19, 7), with a bevel nothing is drawn there
        Assert.True(results["default"].Spike.R < 100, "default limit 4 keeps the miter");
        Assert.True(results["above"].Spike.R < 100, "limit 3 keeps the miter");
        Assert.True(results["huge"].Spike.R < 100, "a huge limit is clamped but still keeps the miter");
        Assert.Equal(White, results["below"].Spike);
        Assert.Equal(White, results["tiny"].Spike);
        Assert.Equal(White, results["nan"].Spike);
        Assert.Equal(White, results["below"].Beside);

        // Assert: the miter limit that the stroker receives: 4 without a value, 1 for a value below 1 or not a number, at most 1000
        Assert.Equal(4f, ScenePainter.ResolveMiterLimit(null));
        Assert.Equal(2.5f, ScenePainter.ResolveMiterLimit(2.5));
        Assert.Equal(1f, ScenePainter.ResolveMiterLimit(0.2));
        Assert.Equal(1f, ScenePainter.ResolveMiterLimit(double.NaN));
        Assert.Equal(1f, ScenePainter.ResolveMiterLimit(double.NegativeInfinity));
        Assert.Equal(1f, ScenePainter.ResolveMiterLimit(double.PositiveInfinity));
        Assert.Equal(1000f, ScenePainter.ResolveMiterLimit(5000));
        Assert.Equal(1000f, ScenePainter.ResolveMiterLimit(1000));
    }

    /// <summary>
    ///     Validates that a path of segments is stroked with the dash pattern, offset, cap and join of its item.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PathStrokeHonorsDashCapJoinAndOffset()
    {
        // Arrange: three horizontal lines 2 wide with dashes of 4 on and 4 off: with butt caps, with square caps and with an
        // offset of 2; and a right-angle corner with a miter, bevel and round join
        ScenePath Line(double y, string? cap, double offset) =>
            new("mark-line", [ScenePathSegment.Move(2, y), ScenePathSegment.Line(30, y)])
            {
                Stroke = "#000000",
                StrokeWidth = 2,
                StrokeDash = [4, 4],
                StrokeDashOffset = offset,
                StrokeCap = cap,
            };

        ScenePath Corner(double x, string join) =>
            new("mark-line", [ScenePathSegment.Move(x, 38), ScenePathSegment.Line(x + 20, 38), ScenePathSegment.Line(x + 20, 48)])
            {
                Stroke = "#000000",
                StrokeWidth = 4,
                StrokeJoin = join,
            };

        var scene = MakeScene(120, 50, Line(10, null, 0), Line(20, "square", 0), Line(30, null, 2), Corner(0, "miter"), Corner(40, "bevel"), Corner(80, "round"));

        // Act
        using var surface = PaintAtOne(scene, 120, 50);

        // Assert: butt caps: on from 2 to 6, off from 6 to 10, on from 10 to 14, off from 14 to 18
        Assert.Equal(Black, surface[3, 9]);
        Assert.Equal(Black, surface[5, 9]);
        Assert.Equal(White, surface[7, 9]);
        Assert.Equal(White, surface[8, 9]);
        Assert.Equal(Black, surface[11, 9]);
        Assert.Equal(White, surface[15, 9]);
        Assert.Equal(White, surface[1, 9]);

        // Assert: square caps extend every dash by one on each side: on from 1 to 7, off from 7 to 9, on from 9 to 15
        Assert.Equal(Black, surface[1, 19]);
        Assert.Equal(Black, surface[6, 19]);
        Assert.Equal(White, surface[7, 19]);
        Assert.Equal(White, surface[8, 19]);
        Assert.Equal(Black, surface[9, 19]);
        Assert.Equal(Black, surface[14, 19]);

        // Assert: an offset of 2 starts two units into the pattern: on from 2 to 4, off from 4 to 8, on from 8 to 12
        Assert.Equal(Black, surface[2, 29]);
        Assert.Equal(Black, surface[3, 29]);
        Assert.Equal(White, surface[4, 29]);
        Assert.Equal(White, surface[7, 29]);
        Assert.Equal(Black, surface[8, 29]);
        Assert.Equal(Black, surface[11, 29]);
        Assert.Equal(White, surface[12, 29]);

        // Assert: the outer corner of a 4 wide right angle at (20, 38) / (60, 38) / (100, 38) reaches (22, 36) with a miter and
        // is cut off by a bevel (nothing in pixel 21, 36) and rounded off by a round join (about a third of the pixel), so the
        // pixel is only fully inside for the miter
        Assert.Equal(Black, surface[21, 36]);
        Assert.Equal(White, surface[61, 36]);
        Assert.True(surface[101, 36].R > 100, "a round join rounds the corner");
    }
}
