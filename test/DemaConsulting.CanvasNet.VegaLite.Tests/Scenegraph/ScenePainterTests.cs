using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the scene painter: fills, strokes, opacity, clipping, scaling, symbols and text, asserted on pixels
///     whose values follow from the geometry.
/// </summary>
public class ScenePainterTests
{
    /// <summary>Opaque white.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>Opaque green.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

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
    ///     Paints a scene at a scale of one.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The surface.</returns>
    private static Surface PaintAtOne(Scene scene, int width, int height) =>
        new ScenePainter(new TextMeasurer()).Paint(scene, width, height, ScenePlacement.Uniform(1), Ct);

    /// <summary>
    ///     Validates fills, strokes, opacities, dashes and rules against the pixels that their geometry covers.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_RectAndRuleHonorsFillStrokeOpacity()
    {
        // Arrange: a filled rectangle, a filled rectangle with a 2 pixel black stroke centered on its edge, a half
        // transparent fill, a half transparent stroke, a solid rule and a dashed rule
        var scene = MakeScene(
            60,
            40,
            new SceneRect("plain", 2, 2, 8, 8) { Fill = "#ff0000" },
            new SceneRect("bordered", 15, 5, 20, 20) { Fill = "#00ff00", Stroke = "#000000", StrokeWidth = 2 },
            new SceneRect("half-fill", 40, 2, 8, 8) { Fill = "#0000ff", FillOpacity = 0.5 },
            new SceneRect("half-stroke", 40, 15, 10, 10) { Stroke = "#000000", StrokeWidth = 2, StrokeOpacity = 0.5 },
            new SceneRect("item-opacity", 52, 2, 6, 6) { Fill = "#0000ff", Opacity = 0.5, FillOpacity = 1 },
            new SceneRule("solid", 0, 35, 30, 35) { Stroke = "#ff0000", StrokeWidth = 2 },
            new SceneRule("dashed", 0, 30, 16, 30) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [4, 4] });

        // Act
        using var surface = PaintAtOne(scene, 60, 40);

        // Assert: a filled rectangle covers exactly its pixels
        Assert.Equal(Red, surface[2, 2]);
        Assert.Equal(Red, surface[9, 9]);
        Assert.Equal(White, surface[1, 1]);
        Assert.Equal(White, surface[10, 10]);

        // Assert: the stroke is centered on the edge: x from 14 to 16 is black, the interior (from 16) green, outside white
        Assert.Equal(Black, surface[14, 15]);
        Assert.Equal(Black, surface[15, 15]);
        Assert.Equal(Green, surface[16, 15]);
        Assert.Equal(White, surface[13, 15]);
        Assert.Equal(Black, surface[14, 4]);
        Assert.Equal(Black, surface[34, 24]);
        Assert.Equal(Green, surface[33, 23]);
        Assert.Equal(White, surface[36, 26]);

        // Assert: half transparent blue over white: 255 * (1 - 128/255) = 127 in red and green
        var halfBlue = new Rgba32(127, 127, 255, 255);
        Assert.Equal(halfBlue, surface[44, 6]);
        Assert.Equal(halfBlue, surface[55, 5]);

        // Assert: half transparent black stroke over white (the fill is absent, so the inside stays white)
        var halfBlack = new Rgba32(127, 127, 127, 255);
        Assert.Equal(halfBlack, surface[39, 20]);
        Assert.Equal(halfBlack, surface[40, 20]);
        Assert.Equal(White, surface[45, 20]);

        // Assert: a 2 pixel rule on y = 35 covers rows 34 and 35 of the pixels from x = 0 to 29
        Assert.Equal(Red, surface[10, 34]);
        Assert.Equal(Red, surface[10, 35]);
        Assert.Equal(White, surface[10, 33]);
        Assert.Equal(White, surface[10, 36]);
        Assert.Equal(Red, surface[29, 35]);
        Assert.Equal(White, surface[30, 35]);

        // Assert: the dashes are 4 on and 4 off, with butt caps (the edges fall on pixel boundaries)
        Assert.Equal(Black, surface[0, 29]);
        Assert.Equal(Black, surface[3, 30]);
        Assert.Equal(White, surface[4, 29]);
        Assert.Equal(White, surface[7, 30]);
        Assert.Equal(Black, surface[8, 29]);
        Assert.Equal(Black, surface[11, 30]);
        Assert.Equal(White, surface[12, 29]);
        Assert.Equal(White, surface[15, 30]);
    }

    /// <summary>
    ///     Validates paints that must draw nothing and the background rules.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_NoPaintDrawsNothingAndBackgroundFillsTheSurface()
    {
        // Arrange: items that are invisible for different reasons
        var invisible = MakeScene(
            20,
            20,
            new SceneRect("none", 2, 2, 8, 8) { Fill = "none" },
            new SceneRect("garbage", 2, 2, 8, 8) { Fill = "not a color" },
            new SceneRect("transparent", 2, 2, 8, 8) { Fill = "transparent" },
            new SceneRect("zero-opacity", 2, 2, 8, 8) { Fill = "red", Opacity = 0 },
            new SceneRect("zero-width", 2, 2, 0, 8) { Fill = "red" },
            new SceneRect("zero-stroke", 2, 2, 8, 8) { Stroke = "red", StrokeWidth = 0 },
            new SceneRect("nan", double.NaN, 2, 8, 8) { Fill = "red" },
            new SceneRule("point", 5, 5, 5, 5) { Stroke = "red" },
            new SceneText("blank", 2, 12, ["   "]) { Fill = "black", FontSize = 10 },
            new SceneText("plain", 2, 12, ["Hi"]) { FontSize = 10 });

        // Act
        using var surface = PaintAtOne(invisible, 20, 20);
        using var translucent = new ScenePainter(new TextMeasurer()).Paint(
            new Scene(10, 10, "rgba(255, 0, 0, 0.5)", new SceneGroup("root", 0, 0)),
            10,
            10,
            ScenePlacement.Uniform(1),
            Ct);
        using var unpainted = new ScenePainter(new TextMeasurer()).Paint(
            new Scene(10, 10, null, new SceneGroup("root", 0, 0)),
            10,
            10,
            ScenePlacement.Uniform(1),
            Ct);

        // Assert: nothing but the background; a translucent background keeps its alpha; no background is transparent
        Assert.True(VegaLiteTestHelpers.AllPixelsAre(surface, White));
        Assert.True(VegaLiteTestHelpers.AllPixelsAre(translucent, new Rgba32(255, 0, 0, 128)));
        Assert.True(VegaLiteTestHelpers.AllPixelsAre(unpainted, new Rgba32(0, 0, 0, 0)));
    }

    /// <summary>
    ///     Validates clipping, uniform scaling and placement against analytically derived pixels.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ClipAndScaleMatchAnalyticPixels()
    {
        // Arrange: a group at (5, 5) clipped to 10 by 10 that holds a rectangle far larger than the clip
        var clipped = MakeScene(
            20,
            20,
            new SceneGroup("clipped", 5, 5)
            {
                Clip = new SceneClip(0, 0, 10, 10),
                Children = [new SceneRect("big", -5, -5, 40, 40) { Fill = "#ff0000" }],
            });

        // Act
        using var surface = PaintAtOne(clipped, 20, 20);

        // Assert: only the clip rectangle [5, 15) x [5, 15) is painted, edge pixels are exact
        Assert.Equal(Red, surface[5, 5]);
        Assert.Equal(Red, surface[14, 14]);
        Assert.Equal(Red, surface[5, 14]);
        Assert.Equal(White, surface[15, 15]);
        Assert.Equal(White, surface[15, 10]);
        Assert.Equal(White, surface[10, 15]);
        Assert.Equal(White, surface[4, 4]);
        Assert.Equal(White, surface[4, 10]);
        Assert.Equal(White, surface[0, 19]);
        Assert.Equal(100, CountPixels(surface, Red));

        // Arrange: the same group at scale 2 on a larger surface, and a scene that is placed with an offset
        using var doubled = new ScenePainter(new TextMeasurer()).Paint(clipped, 40, 40, ScenePlacement.Uniform(2), Ct);
        var blueRect = MakeScene(10, 10, new SceneRect("r", 1, 1, 4, 4) { Fill = "#0000ff" });
        using var tripled = new ScenePainter(new TextMeasurer()).Paint(blueRect, 30, 30, ScenePlacement.Uniform(3), Ct);
        using var fitted = new ScenePainter(new TextMeasurer()).Paint(blueRect, 40, 20, ScenePlacement.Fit(10, 10, 40, 20), Ct);

        // Assert: the clip scales with the chart: [10, 30) x [10, 30)
        Assert.Equal(Red, doubled[10, 10]);
        Assert.Equal(Red, doubled[29, 29]);
        Assert.Equal(White, doubled[30, 30]);
        Assert.Equal(White, doubled[9, 9]);
        Assert.Equal(400, CountPixels(doubled, Red));

        // Assert: a rectangle at (1, 1) with size 4 at scale 3 covers [3, 15)
        Assert.Equal(Blue, tripled[3, 3]);
        Assert.Equal(Blue, tripled[14, 14]);
        Assert.Equal(White, tripled[2, 2]);
        Assert.Equal(White, tripled[15, 15]);
        Assert.Equal(144, CountPixels(tripled, Blue));

        // Assert: fitting 10 by 10 into 40 by 20 scales by 2 and centers horizontally (offset 10): [12, 20) x [2, 10)
        Assert.Equal(Blue, fitted[12, 2]);
        Assert.Equal(Blue, fitted[19, 9]);
        Assert.Equal(White, fitted[11, 5]);
        Assert.Equal(White, fitted[20, 5]);
        Assert.Equal(64, CountPixels(fitted, Blue));
    }

    /// <summary>
    ///     Validates that a clip edge on a fractional coordinate is anti-aliased by its coverage, and nested clips intersect.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FractionalAndNestedClipsUseCoverage()
    {
        // Arrange: clip [2.5, 7.5) in x and [0, 5) in y; and an inner clip that cuts the outer one further
        var fractional = MakeScene(
            10,
            5,
            new SceneGroup("clipped", 0, 0)
            {
                Clip = new SceneClip(2.5, 0, 5, 5),
                Children = [new SceneRect("big", 0, 0, 10, 5) { Fill = "#ff0000" }],
            });
        var nested = MakeScene(
            20,
            20,
            new SceneGroup("outer", 0, 0)
            {
                Clip = new SceneClip(2, 2, 12, 12),
                Children =
                [
                    new SceneGroup("inner", 0, 0)
                    {
                        Clip = new SceneClip(8, 8, 12, 12),
                        Children = [new SceneRect("big", 0, 0, 20, 20) { Fill = "#ff0000" }],
                    },
                ],
            });

        // Act
        using var half = PaintAtOne(fractional, 10, 5);
        using var both = PaintAtOne(nested, 20, 20);

        // Assert: the edge columns are half covered (red over white: green and blue drop to 127 or 128), inner ones full
        Assert.Equal(White, half[1, 2]);
        Assert.Equal(Red, half[3, 2]);
        Assert.Equal(Red, half[6, 2]);
        Assert.Equal(White, half[8, 2]);
        Assert.Equal(255, half[2, 2].R);
        Assert.InRange(half[2, 2].G, 127, 128);
        Assert.InRange(half[7, 2].B, 127, 128);

        // Assert: the nested clips intersect: [8, 14) x [8, 14)
        Assert.Equal(Red, both[8, 8]);
        Assert.Equal(Red, both[13, 13]);
        Assert.Equal(White, both[14, 14]);
        Assert.Equal(White, both[7, 7]);
        Assert.Equal(36, CountPixels(both, Red));
    }

    /// <summary>
    ///     Validates symbols: circle, square, rotation, the shapes' bounding boxes and the area rule.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_SymbolsCoverTheirDocumentedArea()
    {
        // Arrange: a square of area 100 (side 10), the same rotated by 45 degrees, a circle of area 100 (diameter 10)
        var scene = MakeScene(
            60,
            20,
            new SceneSymbol("square", 10, 10) { Shape = "square", Size = 100, Fill = "#ff0000" },
            new SceneSymbol("diamond-square", 30, 10) { Shape = "square", Size = 100, Angle = 45, Fill = "#ff0000" },
            new SceneSymbol("circle", 50, 10) { Shape = "circle", Size = 100, Fill = "#ff0000" });

        // Act
        using var surface = PaintAtOne(scene, 60, 20);

        // Assert: the square covers [5, 15) x [5, 15)
        Assert.Equal(Red, surface[5, 5]);
        Assert.Equal(Red, surface[14, 14]);
        Assert.Equal(White, surface[4, 4]);
        Assert.Equal(White, surface[15, 15]);

        // Assert: rotated by 45 degrees the corners move to the middle of the sides, at distance 5 * sqrt(2) = 7.07
        Assert.Equal(Red, surface[30, 4]);
        Assert.Equal(Red, surface[30, 15]);
        Assert.Equal(White, surface[25, 5]);
        Assert.Equal(White, surface[35, 15]);

        // Assert: the circle has diameter 10: its center and the pixels well inside are in, the corners of its box are out
        Assert.Equal(Red, surface[50, 10]);
        Assert.Equal(Red, surface[50, 6]);
        Assert.Equal(Red, surface[46, 10]);
        Assert.Equal(White, surface[45, 5]);
        Assert.Equal(White, surface[54, 14]);

        // Assert: the circle's area is that of the circle of radius 5, within the error of anti-aliasing and flattening
        var coverage = Enumerable.Range(40, 20).Sum(x => Enumerable.Range(0, 20).Sum(y => 1.0 - (surface[x, y].G / 255.0)));
        Assert.InRange(coverage, Math.PI * 25 * 0.98, Math.PI * 25 * 1.02);
    }

    /// <summary>
    ///     Validates the bounding boxes of the symbol shapes, which follow Vega's definitions of the size as an area.
    /// </summary>
    [Fact]
    public void ScenePainter_SymbolPath_ShapesHaveVegaBounds()
    {
        // Arrange: size 64 gives r = sqrt(64) / 2 = 4; triangles are sqrt(3)/2 * r = 3.4641 high on each side of the center
        var half = Math.Sqrt(3) / 2 * 4;

        // Act / Assert: bounding boxes (the conservative control-point bounds are exact for polygons)
        AssertBounds(ScenePainter.SymbolPath("square", 64), -4, -4, 4, 4);
        AssertBounds(ScenePainter.SymbolPath("cross", 64), -4, -4, 4, 4);
        AssertBounds(ScenePainter.SymbolPath("diamond", 64), -4, -4, 4, 4);
        AssertBounds(ScenePainter.SymbolPath("triangle-up", 64), -4, -half, 4, half);
        AssertBounds(ScenePainter.SymbolPath("triangle-down", 64), -4, -half, 4, half);
        AssertBounds(ScenePainter.SymbolPath("triangle-right", 64), -half, -4, half, 4);
        AssertBounds(ScenePainter.SymbolPath("triangle-left", 64), -half, -4, half, 4);
        AssertBounds(ScenePainter.SymbolPath("stroke", 64), -4, 0, 4, 0);
        AssertBounds(ScenePainter.SymbolPath("circle", 64), -4, -4, 4, 4);
        AssertBounds(ScenePainter.SymbolPath("unknown-shape", 64), -4, -4, 4, 4);

        // Assert: a size that is not positive has no shape
        Assert.Empty(ScenePainter.SymbolPath("circle", 0).Subpaths);
        Assert.Empty(ScenePainter.SymbolPath("square", -5).Subpaths);
        Assert.NotEmpty(ScenePainter.SymbolPath("arrow", 64).Subpaths);
        Assert.NotEmpty(ScenePainter.SymbolPath("wedge", 64).Subpaths);
        Assert.NotEmpty(ScenePainter.SymbolPath("triangle", 64).Subpaths);
    }

    /// <summary>
    ///     Validates rounded rectangles, polylines, polygons, and the stroked group background with Vega's pixel offset.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PathsRoundedRectanglesAndGroupBackgroundAreCrisp()
    {
        // Arrange
        var scene = MakeScene(
            60,
            40,
            new SceneRect("round", 0, 0, 20, 20) { Fill = "#ff0000", CornerRadius = 5 },
            new ScenePath("line", [new ScenePoint(25, 2), new ScenePoint(45, 2), new ScenePoint(45, 12)]) { Stroke = "#000000", StrokeWidth = 2 },
            new ScenePath("triangle", [new ScenePoint(25, 30), new ScenePoint(35, 30), new ScenePoint(25, 20)]) { Fill = "#00ff00", Closed = true },
            new SceneGroup("view", 40, 20) { Width = 15, Height = 15, Stroke = "#dddddd" });

        // Act
        using var surface = PaintAtOne(scene, 60, 40);

        // Assert: the rounded corner is cut away, the middle of the edges is not
        Assert.Equal(White, surface[0, 0]);
        Assert.Equal(Red, surface[10, 0]);
        Assert.Equal(Red, surface[0, 10]);
        Assert.Equal(Red, surface[10, 10]);

        // Assert: the open polyline is stroked along both legs (the 2 pixel wide stroke covers 1 on each side)
        Assert.Equal(Black, surface[35, 1]);
        Assert.Equal(Black, surface[35, 2]);
        Assert.Equal(White, surface[35, 3]);
        Assert.Equal(Black, surface[44, 8]);
        Assert.Equal(Black, surface[45, 8]);
        Assert.Equal(White, surface[47, 8]);

        // Assert: the closed triangle is filled inside and not outside of its hypotenuse
        Assert.Equal(Green, surface[26, 28]);
        Assert.Equal(White, surface[34, 21]);

        // Assert: the one pixel border of a group is offset by half a pixel, so it covers exactly one pixel column and row
        var border = new Rgba32(221, 221, 221, 255);
        Assert.Equal(border, surface[40, 27]);
        Assert.Equal(White, surface[39, 27]);
        Assert.Equal(White, surface[41, 27]);
        Assert.Equal(border, surface[55, 27]);
        Assert.Equal(border, surface[47, 20]);
        Assert.Equal(border, surface[47, 35]);
        Assert.Equal(White, surface[47, 21]);
    }

    /// <summary>
    ///     Validates text drawing: it leaves ink where the layout says, rotates, scales and is deterministic.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextUsesBaselineRotationAndScale()
    {
        // Arrange: "Hello" with its baseline at y = 20 and the top of the text at y = 20, a rotated copy and a scaled paint
        var horizontal = MakeScene(60, 30, new SceneText("t", 5, 20, ["Hello"]) { Fill = "#000000", FontSize = 12 });
        var onTop = MakeScene(60, 30, new SceneText("t", 5, 5, ["Hello"]) { Fill = "#000000", FontSize = 12, Baseline = "top" });
        var rotated = MakeScene(30, 60, new SceneText("t", 20, 55, ["Hello"]) { Fill = "#000000", FontSize = 12, Angle = -90 });
        var centered = MakeScene(60, 30, new SceneText("t", 30, 20, ["Hello"]) { Fill = "#000000", FontSize = 12, Align = "center" });
        var right = MakeScene(60, 30, new SceneText("t", 55, 20, ["Hello"]) { Fill = "#000000", FontSize = 12, Align = "right" });

        // Act
        using var a = PaintAtOne(horizontal, 60, 30);
        using var b = PaintAtOne(horizontal, 60, 30);
        using var top = PaintAtOne(onTop, 60, 30);
        using var up = PaintAtOne(rotated, 30, 60);
        using var middle = PaintAtOne(centered, 60, 30);
        using var end = PaintAtOne(right, 60, 30);
        using var doubled = new ScenePainter(new TextMeasurer()).Paint(horizontal, 120, 60, ScenePlacement.Uniform(2), Ct);

        // Assert: deterministic
        Assert.Equal(VegaLiteTestHelpers.Snapshot(a), VegaLiteTestHelpers.Snapshot(b));

        // Assert: ink starts at the anchor, sits on the baseline (nothing below it except the descent of the letters: none in "Hello")
        var ink = VegaLiteTestHelpers.InkBounds(a, White);
        Assert.NotNull(ink);
        Assert.InRange(ink.Value.Left, 5, 6);
        Assert.InRange(ink.Value.Bottom, 19, 20);
        Assert.InRange(ink.Value.Top, 10, 12);
        Assert.True(ink.Value.Right - ink.Value.Left > 20);

        // Assert: the top baseline puts the text below the anchor: offset 0.79 * 12 = 9.48 rounds to 9, so the baseline is at 5 + 9 = 14
        var topInk = VegaLiteTestHelpers.InkBounds(top, White);
        Assert.NotNull(topInk);
        Assert.InRange(topInk.Value.Bottom, 13, 14);

        // Assert: rotated by -90 degrees the run reads upwards: taller than wide, starting at the anchor and going up
        var upInk = VegaLiteTestHelpers.InkBounds(up, White);
        Assert.NotNull(upInk);
        Assert.True(upInk.Value.Bottom - upInk.Value.Top > 20);
        Assert.True(upInk.Value.Right - upInk.Value.Left < 12);
        Assert.InRange(upInk.Value.Bottom, 54, 55);
        Assert.InRange(upInk.Value.Right, 19, 20);

        // Assert: alignment puts the anchor at the center or the end of the run
        var centerInk = VegaLiteTestHelpers.InkBounds(middle, White);
        var endInk = VegaLiteTestHelpers.InkBounds(end, White);
        Assert.NotNull(centerInk);
        Assert.NotNull(endInk);
        Assert.InRange((centerInk.Value.Left + centerInk.Value.Right) / 2.0, 29, 31);
        Assert.InRange(endInk.Value.Right, 53, 55);

        // Assert: at scale 2 the same text is twice as large
        var bigInk = VegaLiteTestHelpers.InkBounds(doubled, White);
        Assert.NotNull(bigInk);
        Assert.InRange(bigInk.Value.Left, 10, 12);
        Assert.InRange(bigInk.Value.Right - bigInk.Value.Left, 2 * (ink.Value.Right - ink.Value.Left) - 3, (2 * (ink.Value.Right - ink.Value.Left)) + 3);
        Assert.InRange(bigInk.Value.Bottom, 39, 40);
    }

    /// <summary>
    ///     Validates the limit of the text: a long line is cut and ends with the ellipsis, so its ink stays inside the limit.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextLimitTruncatesTheDrawnLine()
    {
        // Arrange
        var free = MakeScene(200, 20, new SceneText("t", 0, 15, ["A very long label for a short space"]) { Fill = "#000000", FontSize = 12 });
        var limited = MakeScene(200, 20, new SceneText("t", 0, 15, ["A very long label for a short space"]) { Fill = "#000000", FontSize = 12, Limit = 60 });

        // Act
        using var wide = PaintAtOne(free, 200, 20);
        using var narrow = PaintAtOne(limited, 200, 20);

        // Assert: the full line is wider than 100 pixels, the limited one fits inside the limit
        Assert.True(VegaLiteTestHelpers.InkBounds(wide, White)!.Value.Right > 100);
        Assert.InRange(VegaLiteTestHelpers.InkBounds(narrow, White)!.Value.Right, 40, 60);
    }

    /// <summary>
    ///     Validates that dash patterns whose work would be unbounded are drawn solid, without any timing assumption.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PathologicalDashPatternsFallBackToSolid()
    {
        // Arrange: a period far below a quarter pixel, a pattern that would make millions of dashes on a long rule, and
        // a pattern whose entries underflow to zero as floats; each is a black 2 pixel rule on rows 4 and 5
        var scene = MakeScene(
            20,
            40,
            new SceneRule("tiny", 0, 5, 20, 5) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [0.001, 0.001] },
            new SceneRule("long", 0, 15, 10_000_000, 15) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [2, 2] },
            new SceneRule("underflow", 0, 25, 20, 25) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [1e-50, 1e-50] },
            new SceneRule("overflow", 0, 35, 20, 35) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [1e300, 1] });

        // Act
        using var surface = PaintAtOne(scene, 20, 40);

        // Assert: every pixel of each rule is covered, so none of the patterns left gaps
        foreach (var row in new[] { 4, 5, 14, 15, 24, 25, 34, 35 })
        {
            for (var x = 0; x < 20; x++)
            {
                Assert.Equal(Black, surface[x, row]);
            }
        }
    }

    /// <summary>
    ///     Validates that a stroke width, dash entry or dash offset that is finite as a double but not as a float never
    ///     escapes as an exception: an unusable width draws nothing and an unusable dash pattern or offset is solid or zero.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ValuesOutsideTheFloatRangeAreSafe()
    {
        // Arrange: widths that overflow and underflow as floats, a dash offset that overflows, and a normal dash pattern
        // for comparison
        var scene = MakeScene(
            20,
            40,
            new SceneRule("wide", 0, 5, 20, 5) { Stroke = "#000000", StrokeWidth = 1e300 },
            new SceneRule("thin", 0, 15, 20, 15) { Stroke = "#000000", StrokeWidth = 1e-50 },
            new SceneRule("offset", 0, 25, 20, 25) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [4, 4], StrokeDashOffset = 1e300 },
            new SceneRule("dashed", 0, 35, 20, 35) { Stroke = "#000000", StrokeWidth = 2, StrokeDash = [4, 4] });

        // Act
        using var surface = PaintAtOne(scene, 20, 40);

        // Assert: the unusable widths draw nothing
        Assert.Equal(White, surface[10, 5]);
        Assert.Equal(White, surface[10, 15]);

        // Assert: the overflowing offset counts as zero, so the dashes are the ones of an unshifted pattern
        for (var x = 0; x < 20; x++)
        {
            Assert.Equal(surface[x, 34], surface[x, 24]);
        }

        Assert.Equal(Black, surface[0, 25]);
        Assert.Equal(White, surface[5, 25]);
    }

    /// <summary>
    ///     Validates the linear gradient fill: the gradient runs along the item's bounding box (left to right by
    ///     default, bottom to top for a legend gradient), continues with its end colors beyond the stops, supports hard
    ///     steps and several stops, and is evaluated at the pixel positions the geometry gives.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GradientFillRunsAlongTheItemBox()
    {
        // Arrange: a 100 by 20 rectangle from red to blue, a 20 by 100 rectangle that is black at the bottom and white at
        // the top, a rectangle with stops at 0.25 and 0.75 (the end colors continue outside them), and a hard step
        var scene = MakeScene(
            240,
            120,
            new SceneRect("horizontal", 10, 10, 100, 20)
            {
                FillGradient = SceneGradient.Horizontal([new SceneGradientStop(0, "#ff0000"), new SceneGradientStop(1, "#0000ff")]),
            },
            new SceneRect("vertical", 120, 10, 20, 100)
            {
                FillGradient = SceneGradient.Vertical([new SceneGradientStop(0, "#000000"), new SceneGradientStop(1, "#ffffff")]),
            },
            new SceneRect("padded", 150, 10, 80, 20)
            {
                FillGradient = SceneGradient.Horizontal([new SceneGradientStop(0.25, "#ff0000"), new SceneGradientStop(0.75, "#0000ff")]),
            },
            new SceneRect("hard", 10, 50, 100, 20)
            {
                FillGradient = SceneGradient.Horizontal(
                [
                    new SceneGradientStop(0, "#ff0000"),
                    new SceneGradientStop(0.5, "#ff0000"),
                    new SceneGradientStop(0.5, "#0000ff"),
                    new SceneGradientStop(1, "#0000ff"),
                ]),
            });

        // Act
        using var surface = PaintAtOne(scene, 240, 120);

        // Assert: along the red to blue rectangle the red channel falls and the blue one rises by 2.55 a pixel; a column
        // centered at 59.5 is 0.495 of the way: red 128.8, blue 126.2 (the sampling position allows one more step)
        AssertNear(new Rgba32(254, 0, 1, 255), surface[10, 20], 3);
        AssertNear(new Rgba32(129, 0, 126, 255), surface[59, 20], 3);
        AssertNear(new Rgba32(1, 0, 254, 255), surface[109, 20], 3);
        Assert.Equal(surface[59, 12], surface[59, 28]);
        Assert.Equal(White, surface[9, 20]);
        Assert.Equal(White, surface[110, 20]);

        // Assert: the legend gradient is black at the bottom and white at the top, with the same grey in a row
        AssertNear(new Rgba32(3, 3, 3, 255), surface[130, 109], 4);
        AssertNear(new Rgba32(127, 127, 127, 255), surface[130, 59], 4);
        AssertNear(new Rgba32(252, 252, 252, 255), surface[130, 10], 4);
        Assert.Equal(surface[121, 60], surface[138, 60]);
        Assert.Equal(White, surface[130, 9]);
        Assert.Equal(White, surface[130, 110]);

        // Assert: a quarter in, the first color continues (pad); a quarter before the end the last one does; the
        // middle is half way
        Assert.Equal(Red, surface[150, 20]);
        Assert.Equal(Red, surface[168, 20]);
        AssertNear(new Rgba32(128, 0, 127, 255), surface[190, 20], 4);
        Assert.Equal(Blue, surface[212, 20]);
        Assert.Equal(Blue, surface[229, 20]);

        // Assert: a hard step is an exact change of color at the stop
        Assert.Equal(Red, surface[10, 60]);
        Assert.Equal(Red, surface[59, 60]);
        Assert.Equal(Blue, surface[60, 60]);
        Assert.Equal(Blue, surface[109, 60]);
    }

    /// <summary>
    ///     Validates that the gradient follows the placement: a scale of two doubles the box and the gradient with it, and
    ///     a gradient inside a translated group is positioned by the group.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GradientFollowsTheScaleAndTheEnclosingGroup()
    {
        // Arrange: a 50 by 10 rectangle in a group that is moved by (5, 5)
        var gradient = SceneGradient.Horizontal([new SceneGradientStop(0, "#000000"), new SceneGradientStop(1, "#ffffff")]);
        var scene = new Scene(
            80,
            30,
            "white",
            new SceneGroup("root", 0, 0)
            {
                Children = [new SceneGroup("moved", 5, 5) { Children = [new SceneRect("r", 0, 0, 50, 10) { FillGradient = gradient }] }],
            });

        // Act: at a scale of one and of two
        using var single = new ScenePainter(new TextMeasurer()).Paint(scene, 80, 30, ScenePlacement.Uniform(1), Ct);
        using var doubled = new ScenePainter(new TextMeasurer()).Paint(scene, 160, 60, ScenePlacement.Uniform(2), Ct);

        // Assert: at a scale of one the rectangle covers columns 5 to 54; the middle column (29.5 of 5..55) is half grey
        Assert.Equal(White, single[4, 10]);
        AssertNear(new Rgba32(1, 1, 1, 255), single[5, 10], 3);
        AssertNear(new Rgba32(127, 127, 127, 255), single[29, 10], 4);
        AssertNear(new Rgba32(254, 254, 254, 255), single[54, 10], 3);
        Assert.Equal(White, single[55, 10]);

        // Assert: at a scale of two it covers columns 10 to 109 with the same gradient over twice the width
        Assert.Equal(White, doubled[9, 20]);
        AssertNear(new Rgba32(1, 1, 1, 255), doubled[10, 20], 3);
        AssertNear(new Rgba32(127, 127, 127, 255), doubled[59, 20], 4);
        AssertNear(new Rgba32(254, 254, 254, 255), doubled[109, 20], 3);
        Assert.Equal(White, doubled[110, 20]);
    }

    /// <summary>
    ///     Validates that the fill opacity scales the stops, that a gradient replaces the solid fill without falling back
    ///     to it, and that an unusable gradient (no usable stop, geometry that is not finite) falls back to the solid fill.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GradientOpacityReplacementAndFallback()
    {
        // Arrange: a half transparent red gradient over a solid green fill, a fully transparent gradient, a gradient
        // whose stops cannot be used, a gradient with a start that is not a number, and a stroked gradient
        var red = SceneGradient.Horizontal([new SceneGradientStop(0, "#ff0000"), new SceneGradientStop(1, "#ff0000")]);
        var scene = MakeScene(
            100,
            60,
            new SceneRect("half", 0, 0, 20, 20) { Fill = "#00ff00", FillGradient = red, FillOpacity = 0.5 },
            new SceneRect("clear", 30, 0, 20, 20) { Fill = "#00ff00", FillGradient = red, FillOpacity = 0 },
            new SceneRect("unusable", 60, 0, 20, 20)
            {
                Fill = "#00ff00",
                FillGradient = SceneGradient.Horizontal([new SceneGradientStop(0, "not a color"), new SceneGradientStop(double.NaN, "#ff0000")]),
            },
            new SceneRect("empty", 0, 30, 20, 20) { Fill = "#00ff00", FillGradient = SceneGradient.Horizontal([]) },
            new SceneRect("infinite", 30, 30, 20, 20)
            {
                Fill = "#00ff00",
                FillGradient = new SceneGradient(double.NaN, 0, 1, 0, [new SceneGradientStop(0, "#ff0000")]),
            },
            new SceneRect("stroked", 60, 30, 20, 20)
            {
                Stroke = "#000000",
                StrokeWidth = 2,
                FillGradient = red,
            });

        // Act
        using var surface = PaintAtOne(scene, 100, 60);

        // Assert: half transparent red over white is 255 * (1 - 128 / 255) = 127 in green and blue
        AssertNear(new Rgba32(255, 127, 127, 255), surface[10, 10], 1);

        // Assert: a fully transparent gradient draws nothing, and does not fall back to the solid fill that it replaces
        Assert.Equal(White, surface[40, 10]);

        // Assert: a gradient without a usable stop, with an empty stop list, or with a start that is not a number
        // is ignored and the solid fill is drawn
        Assert.Equal(Green, surface[70, 10]);
        Assert.Equal(Green, surface[10, 40]);
        Assert.Equal(Green, surface[40, 40]);

        // Assert: the stroke is drawn over the gradient fill
        Assert.Equal(Black, surface[60, 40]);
        Assert.Equal(Red, surface[70, 40]);
    }

    /// <summary>
    ///     Validates the gradient on other items: the background of a group, a symbol (whose box is its shape) and a
    ///     polygon path, each with the box of its own geometry, and that a gradient on text is not painted.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GradientOnGroupSymbolAndPath()
    {
        // Arrange: a group background, a circle of size 400 (20 by 20) centered at (60, 20), a triangle path, and text
        var ramp = SceneGradient.Horizontal([new SceneGradientStop(0, "#000000"), new SceneGradientStop(1, "#ffffff")]);
        var scene = MakeScene(
            100,
            70,
            new SceneGroup("background", 5, 5) { Width = 20, Height = 30, FillGradient = ramp },
            new SceneSymbol("circle", 60, 20) { Size = 400, FillGradient = ramp },
            new ScenePath("triangle", [new ScenePoint(10, 60), new ScenePoint(50, 60), new ScenePoint(30, 40)])
            {
                Closed = true,
                FillGradient = SceneGradient.Vertical([new SceneGradientStop(0, "#ff0000"), new SceneGradientStop(1, "#0000ff")]),
            },
            new SceneText("label", 70, 60, ["I"]) { FontSize = 20, FillGradient = ramp });

        // Act
        using var surface = PaintAtOne(scene, 100, 70);

        // Assert: the group background is black at its left edge and white at its right edge
        AssertNear(new Rgba32(6, 6, 6, 255), surface[5, 20], 5);
        AssertNear(new Rgba32(249, 249, 249, 255), surface[24, 20], 5);
        Assert.Equal(White, surface[25, 20]);

        // Assert: the circle spans x from 50 to 70, so a column left of its center is dark, one right of it is light, the
        // corner of its box is outside the circle, and the column at its center (center 60.5, 0.525 of the way) is 134
        var left = surface[53, 20];
        var right = surface[67, 20];
        Assert.True(left.R < 70 && right.R > 185, $"The circle gradient runs left to right: {left} and {right}.");
        AssertNear(new Rgba32(134, 134, 134, 255), surface[60, 20], 4);
        Assert.Equal(White, surface[50, 10]);

        // Assert: the vertical gradient of the triangle (y from 40 to 60) starts at the bottom of its box (red) and ends
        // at the top (blue)
        var bottom = surface[30, 58];
        var apex = surface[30, 43];
        Assert.True(bottom.R > 200 && bottom.B < 60, $"The bottom of the triangle is red: {bottom}.");
        Assert.True(apex.B > 150 && apex.R < 100, $"The top of the triangle is blue: {apex}.");

        // Assert: text is not filled with a gradient (it has no solid fill here), so nothing is drawn
        Assert.Equal(0, CountPixelsOutsideWhite(surface, 66, 40, 99, 69));
    }

    /// <summary>
    ///     Validates that painting a gradient is deterministic: the same scene and placement give the same pixels, also
    ///     from several threads.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_GradientIsDeterministic()
    {
        // Arrange
        var scene = MakeScene(
            60,
            40,
            new SceneRect("r", 3.3, 4.7, 50.4, 30.2)
            {
                FillGradient = new SceneGradient(
                    0.1,
                    0.9,
                    0.8,
                    0.2,
                    [new SceneGradientStop(0, "#1b9e77"), new SceneGradientStop(0.4, "rgba(217, 95, 2, 0.8)"), new SceneGradientStop(1, "#7570b3")]),
                CornerRadius = 4,
            });
        using var first = PaintAtOne(scene, 60, 40);

        // Act: paint again and from several threads
        using var second = PaintAtOne(scene, 60, 40);
        var parallel = new Surface[4];
        Parallel.For(0, parallel.Length, i => parallel[i] = PaintAtOne(scene, 60, 40));

        // Assert
        Assert.True(Same(first, second));
        foreach (var other in parallel)
        {
            Assert.True(Same(first, other));
            other.Dispose();
        }

        // Assert: something was drawn, and the colors vary
        Assert.NotEqual(first[10, 30], first[50, 8]);
    }

    /// <summary>
    ///     Validates the argument checks and cancellation of the painter.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_InvalidArgumentsAndCancellation()
    {
        // Arrange
        var painter = new ScenePainter(new TextMeasurer());
        var scene = MakeScene(10, 10, new SceneRect("r", 0, 0, 5, 5) { Fill = "red" });
        using var surface = new Surface(10, 10);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ScenePainter(null!));
        Assert.Throws<ArgumentNullException>(() => painter.Paint(null!, surface, ScenePlacement.Uniform(1), Ct));
        Assert.Throws<ArgumentNullException>(() => painter.Paint(scene, (Surface)null!, ScenePlacement.Uniform(1), Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => painter.Paint(scene, surface, ScenePlacement.Uniform(0), Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => painter.Paint(scene, surface, ScenePlacement.Uniform(double.NaN), Ct));
        Assert.Throws<OperationCanceledException>(() => painter.Paint(scene, 10, 10, ScenePlacement.Uniform(1), canceled.Token));
        Assert.Equal(new ScenePlacement(1, 0, 0), ScenePlacement.Fit(0, 5, 10, 10));
    }

    /// <summary>
    ///     Asserts the bounds of a path within a small tolerance.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="left">The expected left.</param>
    /// <param name="top">The expected top.</param>
    /// <param name="right">The expected right.</param>
    /// <param name="bottom">The expected bottom.</param>
    private static void AssertBounds(DemaConsulting.CanvasNet.Geometry.Path path, double left, double top, double right, double bottom)
    {
        var bounds = path.GetBounds();
        Assert.Equal(left, bounds.X, 3);
        Assert.Equal(top, bounds.Y, 3);
        Assert.Equal(right, bounds.X + bounds.Width, 3);
        Assert.Equal(bottom, bounds.Y + bounds.Height, 3);
    }

    /// <summary>
    ///     Counts the pixels of a surface that have a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountPixels(Surface surface, Rgba32 color)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Asserts that a pixel equals a color within a tolerance per channel.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The pixel.</param>
    /// <param name="tolerance">The allowed difference of each channel.</param>
    private static void AssertNear(Rgba32 expected, Rgba32 actual, int tolerance)
    {
        Assert.InRange(actual.R, expected.R - tolerance, expected.R + tolerance);
        Assert.InRange(actual.G, expected.G - tolerance, expected.G + tolerance);
        Assert.InRange(actual.B, expected.B - tolerance, expected.B + tolerance);
        Assert.InRange(actual.A, expected.A - tolerance, expected.A + tolerance);
    }

    /// <summary>
    ///     Counts the pixels of a region of a surface that are not white.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The first column.</param>
    /// <param name="top">The first row.</param>
    /// <param name="right">The last column.</param>
    /// <param name="bottom">The last row.</param>
    /// <returns>The number of pixels that are not opaque white.</returns>
    private static int CountPixelsOutsideWhite(Surface surface, int left, int top, int right, int bottom)
    {
        var count = 0;
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (surface[x, y] != White)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Compares two surfaces of the same size pixel by pixel.
    /// </summary>
    /// <param name="a">One surface.</param>
    /// <param name="b">The other surface.</param>
    /// <returns><see langword="true"/> when the sizes and all pixels are equal.</returns>
    private static bool Same(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            if (!a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y)))
            {
                return false;
            }
        }

        return true;
    }
}
