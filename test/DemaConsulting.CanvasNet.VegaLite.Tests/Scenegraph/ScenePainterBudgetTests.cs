using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the paint budget of the scene painter (<see cref="VegaLiteLimits.MaxPaintedPixels"/>): what is charged for a fill, a stroke, a clip layer, text and a picture, that the charge is
///     made before the item is painted, that the failure names the limit and is located at the item or at the nearest enclosing group that has a location, that a total exactly at the limit is
///     accepted, and that the resampled copies of pictures are made once for the items that share them. The expectations follow from the arithmetic of the boxes on whole pixels.
/// </summary>
public class ScenePainterBudgetTests
{
    /// <summary>Opaque white.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque blue.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>The location of the mark that the groups of the tests carry.</summary>
    private static readonly VegaLiteLocation MarkLocation = new("t.vl.json", "/mark", 3, 5);

    /// <summary>The location that an item of the tests carries itself.</summary>
    private static readonly VegaLiteLocation ItemLocation = new("t.vl.json", "/layer/1/mark", 9, 7);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Paints a white scene of 100 by 100 pixels whose root group holds the items, at a scale of one, with a limit.
    /// </summary>
    /// <param name="limit">The limit of painted pixels.</param>
    /// <param name="root">The root group.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(long limit, SceneGroup root) =>
        new ScenePainter(new TextMeasurer(), null, limit).Paint(new Scene(100, 100, "white", root), 100, 100, ScenePlacement.Uniform(1), Ct);

    /// <summary>
    ///     Makes a root group with a location that holds items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The group.</returns>
    private static SceneGroup Root(params SceneItem[] items) => new("root", 0, 0) { Children = items, Source = MarkLocation };

    /// <summary>
    ///     Test that two rectangles that cover the whole surface fit a limit of exactly twice its area, and that a limit one pixel below fails before the second one is painted: each rectangle is
    ///     charged its box (10,000 pixels) before its fill.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FullSurfaceRects_AreChargedTheirBoxBeforeThePaint()
    {
        // Arrange
        var items = new SceneItem[]
        {
            new SceneRect("first", 0, 0, 100, 100) { Fill = "#ff0000" },
            new SceneRect("second", 0, 0, 100, 100) { Fill = "#0000ff" },
        };

        // Act
        using var surface = Paint(20_000, Root(items));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(19_999, Root(items)));

        // Assert: exactly the limit is accepted (the second rectangle paints over the first), one pixel less fails at the second
        Assert.Equal(Blue, surface[50, 50]);
        Assert.Contains("VegaLiteLimits.MaxPaintedPixels", error.Message, StringComparison.Ordinal);
        Assert.Contains("'second'", error.Message, StringComparison.Ordinal);
        Assert.Contains("19999", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the failure comes before any pixel of the item that did not fit is painted: painting onto an existing surface, the item that fails leaves the pixels of the earlier item.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ItemThatDoesNotFit_IsNotPainted()
    {
        // Arrange
        using var surface = new Surface(100, 100);
        var scene = new Scene(100, 100, "white", Root(new SceneRect("first", 0, 0, 100, 100) { Fill = "#ff0000" }, new SceneRect("second", 0, 0, 100, 100) { Fill = "#0000ff" }));

        // Act
        Assert.Throws<VegaLiteRenderException>(() => new ScenePainter(new TextMeasurer(), null, 15_000).Paint(scene, surface, ScenePlacement.Uniform(1), Ct));

        // Assert: the first rectangle is there and the second one is not
        Assert.Equal(Red, surface[50, 50]);
    }

    /// <summary>
    ///     Test that the failure is located at the nearest enclosing group that has a location (the marks group of the mark) when the item has none, and at the item's own location when it has one.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FailureIsLocatedAtTheItemOrTheNearestLocatedGroup()
    {
        // Arrange: a nested group without a location inside one with a location; and a text item with its own location
        var plain = new SceneGroup("inner", 0, 0) { Children = [new SceneRect("rect", 0, 0, 100, 100) { Fill = "#ff0000" }] };
        var text = new SceneText("label", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20, Source = ItemLocation };

        // Act
        var viaGroup = Assert.Throws<VegaLiteRenderException>(() => Paint(100, Root(plain)));
        var viaItem = Assert.Throws<VegaLiteRenderException>(() => Paint(100, Root(text)));

        // Assert
        Assert.Equal(MarkLocation, viaGroup.Location);
        Assert.Equal(ItemLocation, viaItem.Location);
    }

    /// <summary>
    ///     Test that a box is cut to the surface and an item outside it costs nothing: a rectangle of a million pixels beyond the surface is not charged, and one that covers it is charged the surface.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_BoxIsCutToTheSurface_AndAnItemOutsideCostsNothing()
    {
        // Arrange: a limit of 10,000 pixels, which is the surface
        var outside = new SceneRect("outside", 5000, 5000, 1000, 1000) { Fill = "#ff0000" };
        var huge = new SceneRect("huge", -5000, -5000, 100_000, 100_000) { Fill = "#ff0000" };

        // Act
        using var first = Paint(1, Root(outside));
        using var second = Paint(10_000, Root(huge));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(9_999, Root(huge)));

        // Assert
        Assert.Equal(White, first[50, 50]);
        Assert.Equal(Red, second[50, 50]);
        Assert.Contains("MaxPaintedPixels", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a stroke is charged its box grown by half the width on each side: a horizontal rule of width 10 across the surface covers 100 by 10 pixels.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_StrokeIsChargedItsBoxGrownByHalfTheWidth()
    {
        // Arrange
        var rule = new SceneRule("rule", 0, 50, 100, 50) { Stroke = "#ff0000", StrokeWidth = 10 };

        // Act
        using var surface = Paint(1_000, Root(rule));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(999, Root(rule)));

        // Assert
        Assert.Equal(Red, surface[50, 50]);
        Assert.Contains("'rule'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a fill and a stroke of one item are two charges: a rectangle of 50 by 50 pixels with both costs 2,500 for the fill and 2,500 for the stroke box cut to the surface (the box grows by the half width
    ///     of 1 on each side: 52 by 52 pixels, 2,704).
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FillAndStrokeAreTwoCharges()
    {
        // Arrange
        var rect = new SceneRect("both", 10, 10, 50, 50) { Fill = "#ff0000", Stroke = "#0000ff", StrokeWidth = 2 };

        // Act: 2,500 for the fill, 2,704 for the stroke (the box grown by 1 on each side)
        using var surface = Paint(2_500 + 2_704, Root(rect));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(2_500 + 2_703, Root(rect)));

        // Assert
        Assert.Equal(Red, surface[30, 30]);
        Assert.Contains("'both'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the layer of a clipped group is charged its pixels, and what is drawn inside it is cut to the layer: a clip of 50 by 50 pixels is 2,500 and a rectangle inside it that covers
    ///     the clip is 2,500 more.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ClipLayerAndItsContentAreCharged()
    {
        // Arrange
        var clipped = new SceneGroup("clipped", 0, 0)
        {
            Clip = new SceneClip(0, 0, 50, 50),
            Children = [new SceneRect("inside", 0, 0, 100, 100) { Fill = "#ff0000" }],
        };

        // Act
        using var surface = Paint(5_000, Root(clipped));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(4_999, Root(clipped)));

        // Assert
        Assert.Equal(Red, surface[25, 25]);
        Assert.Equal(White, surface[75, 75]);
        Assert.Contains("'inside'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text is charged an estimated box where the painter draws it (a character is 0.6 of the font size wide and a line is a font size and a third tall, from the ascent above the baseline
    ///     to the descent below it): eleven characters at a font size of 20 on a baseline of 50 are 132 pixels wide, cut to the surface 100 by 26 pixels, so exactly that is accepted and one pixel less is refused.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextIsChargedAnEstimatedBox()
    {
        // Arrange
        var text = new SceneText("label", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20 };

        // Act
        using var surface = Paint(100 * 26, Root(text));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint((100 * 26) - 1, Root(text)));

        // Assert
        Assert.Equal(White, surface[99, 0]);
        Assert.Contains("'label'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a zero or negative line height (Vega steps the baselines upward or not at all) is charged for every line that is drawn: every line paints its own row of glyphs, so the charge is never negative
    ///     and never below the painting. Three lines of eleven characters at a font size of 20 on a baseline of 50: with a step of -5 or 0 the three boxes of 100 by 26 pixels are on the surface (3 x 2,600), and
    ///     with a step of -200 only the first one is (the others are above the surface and cost nothing).
    /// </summary>
    /// <param name="lineHeight">The line height of the text.</param>
    /// <param name="expected">The expected charge.</param>
    [Theory]
    [InlineData(-5.0, 7_800)]
    [InlineData(-200.0, 2_600)]
    [InlineData(0.0, 7_800)]
    public void ScenePainter_Paint_TextWithANonPositiveLineHeight_IsChargedEveryLine(double lineHeight, int expected)
    {
        // Arrange
        var lines = new[] { "Hello world", "Hello world", "Hello world" };
        var text = new SceneText("label", 0, 50, lines) { Fill = "#000000", FontSize = 20, LineHeight = lineHeight };

        // Act
        using var surface = Paint(expected, Root(text));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint(expected - 1, Root(text)));

        // Assert: the limit of exactly the estimate is accepted and one below it fails
        Assert.Equal(White, surface[99, 0]);
        Assert.Contains("MaxPaintedPixels", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text is not charged for what is not drawn: text beyond the surface (to the right, or to the left of a right-aligned anchor) costs nothing, text that is partly on it is charged
    ///     the part that is (an anchor at 90 leaves 10 by 26 pixels; a centered anchor at 0 leaves half of the 132 pixels), and an empty line is not charged.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextBeyondTheSurfaceIsNotChargedAndTextAtTheEdgeIsChargedThePartThatIsOnIt()
    {
        // Arrange
        var beyond = new SceneText("beyond", 500, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20 };
        var left = new SceneText("left", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20, Align = "right" };
        var above = new SceneText("above", 0, -100, ["Hello world"]) { Fill = "#000000", FontSize = 20 };
        var edge = new SceneText("edge", 90, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20 };
        var centered = new SceneText("centered", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20, Align = "center" };
        var gap = new SceneText("gap", 0, 50, ["", "Hello world"]) { Fill = "#000000", FontSize = 20 };

        // Act: the smallest limit accepts the text that is not drawn on the surface
        using var first = Paint(1, Root(beyond, left, above));
        using var atEdge = Paint(10 * 26, Root(edge));
        var edgeError = Assert.Throws<VegaLiteRenderException>(() => Paint((10 * 26) - 1, Root(edge)));
        using var atCenter = Paint(66 * 26, Root(centered));
        var centeredError = Assert.Throws<VegaLiteRenderException>(() => Paint((66 * 26) - 1, Root(centered)));
        using var withGap = Paint(100 * 26, Root(gap));
        var gapError = Assert.Throws<VegaLiteRenderException>(() => Paint((100 * 26) - 1, Root(gap)));

        // Assert
        Assert.Equal(White, first[50, 50]);
        Assert.Contains("'edge'", edgeError.Message, StringComparison.Ordinal);
        Assert.Contains("'centered'", centeredError.Message, StringComparison.Ordinal);
        Assert.Contains("'gap'", gapError.Message, StringComparison.Ordinal);
        Assert.NotNull(atEdge);
        Assert.NotNull(atCenter);
        Assert.NotNull(withGap);
    }

    /// <summary>
    ///     Test that text that is truncated by its limit is charged the width that it keeps, not the width that it would have: eleven characters at a font size of 20 limited to 30 pixels (the ellipsis
    ///     included) are charged 30 by 26 pixels.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextTruncatedByItsLimitIsChargedTheWidthItKeeps()
    {
        // Arrange
        var text = new SceneText("label", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20, Limit = 30 };

        // Act
        using var surface = Paint(30 * 26, Root(text));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint((30 * 26) - 1, Root(text)));

        // Assert
        Assert.Equal(White, surface[99, 0]);
        Assert.Contains("'label'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text that the painter does not draw is not charged: a fill of none, a fill that is fully transparent, an opacity of zero and a font size of zero (the smallest limit accepts a hundred of
    ///     each, which would cost millions of pixels if they were charged as boxes).
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_TextThatIsNotDrawnIsNotCharged()
    {
        // Arrange: each of these is a text item that is drawn by no pixel
        var items = new List<SceneItem>();
        for (var i = 0; i < 100; i++)
        {
            items.Add(new SceneText("none", 0, 50, ["Hello world"]) { Fill = "none", FontSize = 2000 });
            items.Add(new SceneText("clear", 0, 50, ["Hello world"]) { Fill = "#00000000", FontSize = 2000 });
            items.Add(new SceneText("faded", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 2000, Opacity = 0 });
            items.Add(new SceneText("zero", 0, 50, ["Hello world"]) { Fill = "#000000", FontSize = 0 });
        }

        // Act
        using var surface = Paint(1, Root([.. items]));

        // Assert
        Assert.Equal(White, surface[50, 50]);
    }

    /// <summary>
    ///     Test that a rotated text item is charged the square around its anchor that holds it in any rotation, cut to the surface: a quarter turn of a 132 pixel line at the middle of the surface reaches
    ///     133 pixels, so the whole surface is charged, and the same text rotated far outside the surface is not charged.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_RotatedTextIsChargedTheSquareAroundItsAnchorCutToTheSurface()
    {
        // Arrange
        var near = new SceneText("near", 50, 50, ["Hello world"]) { Fill = "#000000", FontSize = 20, Angle = 90 };
        var far = new SceneText("far", 5000, 5000, ["Hello world"]) { Fill = "#000000", FontSize = 20, Angle = 90 };

        // Act
        using var accepted = Paint(100 * 100, Root(near));
        var error = Assert.Throws<VegaLiteRenderException>(() => Paint((100 * 100) - 1, Root(near)));
        using var outside = Paint(1, Root(far));

        // Assert
        Assert.NotNull(accepted);
        Assert.Contains("'near'", error.Message, StringComparison.Ordinal);
        Assert.Equal(White, outside[50, 50]);
    }

    /// <summary>
    ///     Test that an arc is charged for its whole ellipse and not for its end point grown by the radius: arcs (pies, donuts, rounded corners, a full circle) centered on and beyond the edges
    ///     and corners of the surface are each charged at least the pixels that they paint, so a limit one pixel below what an arc paints refuses it. (The box of the control points of an
    ///     arc is not the box of its end point: a large arc reaches up to two radii from its end point.)
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ArcsAtTheEdge_AreChargedAtLeastTheirVisibleArea()
    {
        // Arrange: sweeps from a sliver to a full circle, with and without a hole and rounded corners, centered at an edge, a corner and just outside
        var centers = new (double X, double Y)[] { (100, 50), (0, 50), (50, 0), (50, 100), (100, 100), (0, 0), (110, 50), (50, 120) };
        var sweeps = new (double Start, double End, double Inner, double Corner)[]
        {
            (0, 2 * Math.PI, 0, 0),
            (0, 2 * Math.PI, 12, 0),
            (0.3, 4.9, 0, 0),
            (0.3, 4.9, 10, 6),
            (1, 1.05, 0, 0),
            (0, 3 * Math.PI / 2, 0, 0),
        };

        foreach (var (cx, cy) in centers)
        {
            foreach (var (start, end, inner, corner) in sweeps)
            {
                SceneItem Arc() => new SceneArc("arc", cx, cy) { OuterRadius = 30, InnerRadius = inner, StartAngle = start, EndAngle = end, CornerRadius = corner, Fill = "#ff0000" };
                using var unlimited = Paint(long.MaxValue, Root(Arc()));
                var painted = 0;
                for (var y = 0; y < 100; y++)
                {
                    for (var x = 0; x < 100; x++)
                    {
                        painted += unlimited[x, y] == White ? 0 : 1;
                    }
                }

                // Act and Assert: a limit one pixel below what the arc paints refuses it, and the pixels that it paints are accepted
                if (painted > 0)
                {
                    var thrown = Record.Exception(() => Paint(painted - 1, Root(Arc())));
                    var error = Assert.IsType<VegaLiteRenderException>(thrown, exactMatch: true);
                    Assert.Contains("MaxPaintedPixels", error.Message, StringComparison.Ordinal);
                    using var accepted = Paint(100 * 100, Root(Arc()));
                    Assert.NotNull(accepted);
                }
            }
        }
    }

    /// <summary>
    ///     Test that a painter without a limit counts nothing: items that would cost billions of pixels are painted as before, and a limit below one is rejected.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_WithoutALimit_CountsNothing()
    {
        // Arrange
        var rect = new SceneRect("huge", -1e9, -1e9, 2e9, 2e9) { Fill = "#ff0000" };

        // Act
        using var surface = new ScenePainter(new TextMeasurer()).Paint(new Scene(100, 100, "white", Root(rect)), 100, 100, ScenePlacement.Uniform(1), Ct);

        // Assert
        Assert.Equal(Red, surface[50, 50]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScenePainter(new TextMeasurer(), null, 0));
    }

    /// <summary>
    ///     Test that a limit is per painting: one painter paints twice with a limit that one painting nearly uses up.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_LimitIsPerPainting()
    {
        // Arrange
        var painter = new ScenePainter(new TextMeasurer(), null, 10_000);
        var scene = new Scene(100, 100, "white", Root(new SceneRect("full", 0, 0, 100, 100) { Fill = "#ff0000" }));

        // Act
        using var first = painter.Paint(scene, 100, 100, ScenePlacement.Uniform(1), Ct);
        using var second = painter.Paint(scene, 100, 100, ScenePlacement.Uniform(1), Ct);

        // Assert
        Assert.Equal(Red, first[1, 1]);
        Assert.Equal(Red, second[1, 1]);
    }

    /// <summary>
    ///     A resolver that answers every request with a picture made by a function.
    /// </summary>
    private sealed class FuncResolver : IImageResolver
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls { get; private set; }

        /// <summary>Gets the function that makes the picture of a request.</summary>
        public required Func<int, int, Surface> Make { get; init; }

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls++;
            return Make(pixelWidth, pixelHeight);
        }
    }

    /// <summary>
    ///     Makes a 2 by 1 picture whose pixels are red and blue.
    /// </summary>
    /// <returns>The surface.</returns>
    private static Surface RedBlue()
    {
        var surface = new Surface(2, 1);
        surface[0, 0] = Red;
        surface[1, 0] = Blue;
        return surface;
    }

    /// <summary>
    ///     Paints pictures with a resolver and a limit.
    /// </summary>
    /// <param name="resolver">The resolver.</param>
    /// <param name="limit">The limit of painted pixels.</param>
    /// <param name="items">The items.</param>
    /// <returns>The surface.</returns>
    private static Surface PaintImages(IImageResolver resolver, long limit, params SceneItem[] items)
    {
        using var images = new SceneImageSource(resolver, 100, (_, _, _) => { });
        return new ScenePainter(new TextMeasurer(), images, limit).Paint(new Scene(100, 100, "white", Root(items)), 100, 100, ScenePlacement.Uniform(1), Ct);
    }

    /// <summary>
    ///     Test that a picture without a size (defect B4: the natural size is the 256 by 256 of a vector picture) is charged its box cut to the surface before the picture is drawn, and the failure is located at
    ///     the mark of the picture.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PictureBox_IsChargedBeforeItIsDrawnAndLocatedAtTheMark()
    {
        // Arrange: a 60 by 60 picture on a 100 by 100 surface costs 3,600 pixels
        var resolver = new FuncResolver { Make = (_, _) => new Surface(60, 60) };
        var image = new SceneImage("mark-image", "a.png", 0, 0) { Width = 60, Height = 60, Source = ItemLocation };

        // Act
        using var surface = PaintImages(resolver, 3_600, image);
        var error = Assert.Throws<VegaLiteRenderException>(() => PaintImages(resolver, 3_599, image));

        // Assert
        Assert.Equal(White, surface[99, 99]);
        Assert.Equal(ItemLocation, error.Location);
        Assert.Contains("VegaLiteLimits.MaxPaintedPixels", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the nearest-neighbor copy of a picture that is not smoothed is made once for the items that draw it at the same size (defect B4: one copy per item): three items of 100 by 50 pixels are
    ///     5,000 pixels each and the copy is 5,000 once, so 20,000 is enough and 19,999 is not (a copy per item would need 30,000).
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_NonSmoothPictureCopy_IsMadeOnceForTheItemsThatShareIt()
    {
        // Arrange
        var resolver = new FuncResolver { Make = (_, _) => RedBlue() };
        SceneItem[] items =
        [
            new SceneImage("mark-image", "a.png", 0, 0) { Width = 100, Height = 50, Smooth = false },
            new SceneImage("mark-image", "a.png", 0, 50) { Width = 100, Height = 50, Smooth = false },
            new SceneImage("mark-image", "a.png", 0, 25) { Width = 100, Height = 50, Smooth = false },
        ];

        // Act
        using var surface = PaintImages(resolver, 20_000, items);
        var error = Assert.Throws<VegaLiteRenderException>(() => PaintImages(resolver, 19_999, items));

        // Assert: the picture is asked for once per painting, drawn hard-edged
        Assert.Equal(Red, surface[10, 10]);
        Assert.Equal(Blue, surface[90, 10]);
        Assert.Contains("MaxPaintedPixels", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test the tile cache of the loader: the same picture, size and opacity give the same copy without a second charge, another opacity or size is another copy, a smoothed opaque picture is the picture itself,
    ///     and a copy beyond the pixels that are kept is handed out as the caller's and is disposable.
    /// </summary>
    [Fact]
    public void SceneImageSource_GetTile_SharesCopiesAndBoundsWhatItKeeps()
    {
        // Arrange
        using var source = new SceneImageSource(null, 1, (_, _, _) => { });
        using var picture = RedBlue();
        var charged = new List<long>();

        // Act
        var first = source.GetTile(picture, false, 8, 4, 1, charged.Add, out var firstTransient);
        var again = source.GetTile(picture, false, 8, 4, 1, charged.Add, out var againTransient);
        var translucent = source.GetTile(picture, true, 2, 1, 0.5, charged.Add, out _);
        var other = source.GetTile(picture, false, 4, 2, 1, charged.Add, out _);
        var same = source.GetTile(picture, true, 2, 1, 1, charged.Add, out var sameTransient);
        var big = source.GetTile(picture, false, 3000, 3000, 1, charged.Add, out var bigTransient);
        var bigAgain = source.GetTile(picture, false, 3000, 3000, 1, charged.Add, out _);

        // Assert
        Assert.False(firstTransient);
        Assert.False(againTransient);
        Assert.Same(first, again);
        Assert.NotSame(first, translucent);
        Assert.NotSame(first, other);
        Assert.Same(picture, same);
        Assert.False(sameTransient);
        Assert.Equal(128, translucent[0, 0].A);

        // Assert: the work of each copy that was made is charged once (8 by 4, 2 by 1 for the picture itself, 4 by 2, and 3,000 by 3,000 twice because the big one is not kept)
        Assert.Equal([32L, 2, 8, 3000L * 3000, 3000L * 3000], charged);
        Assert.True(bigTransient);
        Assert.NotSame(big, bigAgain);
        big.Dispose();
        bigAgain.Dispose();
    }
}
