using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the painting of <see cref="SceneImage"/> items and of <see cref="SceneImageSource"/> with a fake resolver: the rectangle a picture is drawn in (the rules of
///     <c>vega-scenegraph/src/marks/image.js</c>: the natural size, the aspect rule, the anchors), the size it is asked for, its opacity, the clip of its group, the
///     per-painting cache, the limit of pictures and the reports. Every expectation follows from the arithmetic of the geometry and from the colors of generated pictures; the pixels at the
///     edges of the drawn rectangle are only asserted where the rectangle is on whole pixels, so no expectation depends on the filter of the resampler.
/// </summary>
public class ScenePainterImageTests
{
    /// <summary>Opaque white.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque blue.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A resolver that records every call and answers with generated pictures.
    /// </summary>
    private sealed class FakeResolver : IImageResolver
    {
        /// <summary>The calls: the link and the requested size.</summary>
        public List<(string Href, int Width, int Height)> Calls { get; } = [];

        /// <summary>The surfaces that were handed out.</summary>
        public List<Surface> Handed { get; } = [];

        /// <summary>Makes the picture for a link; null for no picture.</summary>
        public Func<string, int, int, Surface?> Make { get; init; } = (_, _, _) => null;

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls.Add((href, pixelWidth, pixelHeight));
            var surface = Make(href, pixelWidth, pixelHeight);
            if (surface is not null)
            {
                Handed.Add(surface);
            }

            return surface;
        }
    }

    /// <summary>
    ///     Makes a picture of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Makes a picture whose left half is red and whose right half is blue.
    /// </summary>
    /// <param name="width">The width (even).</param>
    /// <param name="height">The height.</param>
    /// <returns>The surface.</returns>
    private static Surface Halves(int width, int height)
    {
        var surface = Solid(width, height, Red);
        for (var y = 0; y < height; y++)
        {
            for (var x = width / 2; x < width; x++)
            {
                surface[x, y] = Blue;
            }
        }

        return surface;
    }

    /// <summary>
    ///     Paints a scene of one white root group at a placement.
    /// </summary>
    /// <param name="resolver">The resolver, or null for none.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="scale">The scale of the placement.</param>
    /// <param name="warnings">Receives the warnings.</param>
    /// <param name="maxImages">The limit of pictures.</param>
    /// <param name="items">The items.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(IImageResolver? resolver, int width, int height, double scale, List<string> warnings, int maxImages, params SceneItem[] items)
    {
        var scene = new Scene(width / scale, height / scale, "white", new SceneGroup("root", 0, 0) { Children = items });
        using var images = new SceneImageSource(resolver, maxImages, (message, _, _) => warnings.Add(message));
        return new ScenePainter(new TextMeasurer(), images).Paint(scene, width, height, ScenePlacement.Uniform(scale), Ct);
    }

    /// <summary>
    ///     Paints a scene at a scale of one with the default limit.
    /// </summary>
    /// <param name="resolver">The resolver.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="items">The items.</param>
    /// <returns>The surface.</returns>
    private static Surface PaintAtOne(IImageResolver? resolver, int width, int height, params SceneItem[] items) =>
        Paint(resolver, width, height, 1, [], 256, items);

    /// <summary>
    ///     Test that a raster is drawn at its natural size at the anchor, a pixel to a pixel, and the resolver is asked for the box that is known: with no box, the 256 default.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageWithoutSize_DrawsNaturalSizeAtLeftTop()
    {
        // Arrange: a 10 by 6 raster whose halves are red and blue
        var resolver = new FakeResolver { Make = (_, _, _) => Halves(10, 6) };
        var item = new SceneImage("mark-image", "icon.png", 20, 10);

        // Act
        using var surface = PaintAtOne(resolver, 60, 40, item);

        // Assert: x from 20 to 30 and y from 10 to 16, red on the left half and blue on the right half; the pixels around are white
        Assert.Equal(("icon.png", 256, 256), Assert.Single(resolver.Calls));
        Assert.Equal(Red, surface[20, 10]);
        Assert.Equal(Red, surface[24, 15]);
        Assert.Equal(Blue, surface[25, 10]);
        Assert.Equal(Blue, surface[29, 15]);
        Assert.Equal(White, surface[19, 10]);
        Assert.Equal(White, surface[30, 10]);
        Assert.Equal(White, surface[20, 9]);
        Assert.Equal(White, surface[20, 16]);
    }

    /// <summary>
    ///     Test that the anchor moves the natural box: <c>center</c> and <c>middle</c> by half of it, <c>right</c> and <c>bottom</c> by all of it, and any other name is left and top.
    /// </summary>
    /// <param name="align">The horizontal anchor.</param>
    /// <param name="baseline">The vertical anchor.</param>
    /// <param name="left">The expected left edge.</param>
    /// <param name="top">The expected top edge.</param>
    [Theory]
    [InlineData("left", "top", 30, 20)]
    [InlineData("center", "middle", 25, 17)]
    [InlineData("right", "bottom", 20, 14)]
    [InlineData("alphabetic", "alphabetic", 30, 20)]
    public void ScenePainter_Paint_ImageAnchor_MovesTheNaturalBox(string align, string baseline, int left, int top)
    {
        // Arrange: a 10 by 6 picture anchored at (30, 20)
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(10, 6, Red) };

        // Act
        using var surface = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 30, 20) { Align = align, Baseline = baseline });

        // Assert: the box is [left, left + 10) by [top, top + 6)
        Assert.Equal(Red, surface[left, top]);
        Assert.Equal(Red, surface[left + 9, top + 5]);
        Assert.Equal(White, surface[left - 1, top]);
        Assert.Equal(White, surface[left, top - 1]);
        Assert.Equal(White, surface[left + 10, top]);
        Assert.Equal(White, surface[left, top + 6]);
    }

    /// <summary>
    ///     Test that a given width and height are the box and the resolver is asked for it in device pixels, at every scale.
    /// </summary>
    /// <param name="scale">The scale.</param>
    [Theory]
    [InlineData(1d)]
    [InlineData(2d)]
    public void ScenePainter_Paint_ImageWithSize_AsksForTheBoxInDevicePixels(double scale)
    {
        // Arrange: a vector-like picture: it is drawn at the size it is asked for
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };
        var item = new SceneImage("mark-image", "part.scad", 10, 5) { Width = 20, Height = 10 };

        // Act
        using var surface = Paint(resolver, (int)(60 * scale), (int)(40 * scale), scale, [], 256, item);

        // Assert: the box is (10, 5) to (30, 15) in scene units
        Assert.Equal(("part.scad", (int)(20 * scale), (int)(10 * scale)), Assert.Single(resolver.Calls));
        var s = (int)scale;
        Assert.Equal(Red, surface[10 * s, 5 * s]);
        Assert.Equal(Red, surface[(30 * s) - 1, (15 * s) - 1]);
        Assert.Equal(White, surface[(10 * s) - 1, 5 * s]);
        Assert.Equal(White, surface[30 * s, 5 * s]);
        Assert.Equal(White, surface[10 * s, (15 * s)]);
    }

    /// <summary>
    ///     Test that a raster is scaled with the chart: its pixels are scene units, so at a scale of two it covers twice the pixels.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_RasterWithoutSize_ScalesWithThePlacement()
    {
        // Arrange: a 10 by 6 raster at a scale of two; the resolver is asked for 256 scene units, i.e. 512 device pixels, and answers with the raster
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(10, 6, Red) };

        // Act
        using var surface = Paint(resolver, 120, 80, 2, [], 256, new SceneImage("mark-image", "icon.png", 10, 5));

        // Assert: the box is 20 by 12 device pixels at (20, 10)
        Assert.Equal(("icon.png", 512, 512), Assert.Single(resolver.Calls));
        Assert.Equal(Red, surface[20, 10]);
        Assert.Equal(Red, surface[39, 21]);
        Assert.Equal(White, surface[40, 10]);
        Assert.Equal(White, surface[20, 22]);
    }

    /// <summary>
    ///     Test that a picture without a size that comes back at the size it was asked for (a vector picture) has the requested size divided by the scale as its natural size.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_VectorWithoutSize_HasTheDefaultNaturalSize()
    {
        // Arrange: a scale of one, so a picture of 256 by 256 fills the surface from the anchor
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };

        // Act
        using var surface = PaintAtOne(resolver, 300, 300, new SceneImage("mark-image", "part.scad", 10, 10));

        // Assert: 256 by 256 at (10, 10)
        Assert.Equal(Red, surface[10, 10]);
        Assert.Equal(Red, surface[265, 265]);
        Assert.Equal(White, surface[266, 10]);
        Assert.Equal(White, surface[10, 266]);
    }

    /// <summary>
    ///     Test that with the aspect kept a width alone gives the height of the picture's aspect ratio, a height alone the width, and without the aspect the natural size of the other
    ///     dimension.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageWithOneDimension_FollowsTheAspectRule()
    {
        // Arrange: a 10 by 5 raster (aspect ratio 2): a width of 20 gives a height of 10 and a height of 10 a width of 20; with aspect off the other side is the natural one
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(10, 5, Red) };

        // Act
        using var widthOnly = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 5, 5) { Width = 20 });
        using var heightOnly = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 5, 5) { Height = 10 });
        using var stretched = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 5, 5) { Width = 20, Aspect = false });

        // Assert: the box is 20 by 10 at (5, 5) for the first two, and 20 by 5 (the natural height) for the one without the aspect
        foreach (var surface in new[] { widthOnly, heightOnly })
        {
            Assert.Equal(Red, surface[5, 5]);
            Assert.Equal(Red, surface[24, 14]);
            Assert.Equal(White, surface[25, 5]);
            Assert.Equal(White, surface[5, 15]);
        }

        Assert.Equal(Red, stretched[24, 9]);
        Assert.Equal(White, stretched[5, 10]);
    }

    /// <summary>
    ///     Test that with both dimensions and the aspect kept a picture of another aspect ratio is fitted inside the box and centered (<c>ar1 &lt; ar0</c> shortens the height, else the width
    ///     shrinks), and that without the aspect it fills the box.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageInBoxOfOtherAspect_IsFittedAndCentered()
    {
        // Arrange: a 10 by 10 raster in a box of 20 by 10 at (10, 5): the picture is 10 by 10 centered horizontally, from x = 15 to 25. In a box of 10 by 20 it is 10 by 10 centered vertically
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(10, 10, Red) };

        // Act
        using var wide = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 10, 5) { Width = 20, Height = 10 });
        using var tall = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 10, 5) { Width = 10, Height = 20 });
        using var filled = PaintAtOne(resolver, 60, 40, new SceneImage("mark-image", "icon.png", 10, 5) { Width = 20, Height = 10, Aspect = false });

        // Assert: the margins of the box are background
        Assert.Equal(Red, wide[15, 5]);
        Assert.Equal(Red, wide[24, 14]);
        Assert.Equal(White, wide[14, 10]);
        Assert.Equal(White, wide[25, 10]);
        Assert.Equal(Red, tall[10, 10]);
        Assert.Equal(Red, tall[19, 19]);
        Assert.Equal(White, tall[15, 9]);
        Assert.Equal(White, tall[15, 20]);
        Assert.Equal(Red, filled[10, 5]);
        Assert.Equal(Red, filled[29, 14]);
    }

    /// <summary>
    ///     Test that a picture of the exact size of a box that was asked for fills the box even when its size was rounded up from a fractional device size.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_PictureOfTheRequestedSize_FillsTheBox()
    {
        // Arrange: a box of 10.5 by 7.5 asks for 11 by 8 pixels and the picture comes back at that size
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };

        // Act
        using var surface = PaintAtOne(resolver, 40, 30, new SceneImage("mark-image", "part.scad", 5, 5) { Width = 10.5, Height = 7.5 });

        // Assert: the whole box (the pixels of the box that are fully covered) is red
        Assert.Equal((11, 8), (Assert.Single(resolver.Calls).Width, resolver.Calls[0].Height));
        Assert.Equal(Red, surface[5, 5]);
        Assert.Equal(Red, surface[14, 11]);
        Assert.Equal(White, surface[16, 5]);
        Assert.Equal(White, surface[5, 13]);
    }

    /// <summary>
    ///     Test that the opacity of the item is multiplied into the alpha of the picture, and that the picture of the resolver is not changed.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageOpacity_BlendsOverTheBackground()
    {
        // Arrange
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(4, 4, Red) };

        // Act: half opaque red over white is (255, 128, 128)
        using var surface = PaintAtOne(resolver, 20, 20, new SceneImage("mark-image", "icon.png", 5, 5) { Opacity = 0.5 });

        // Assert
        var pixel = surface[6, 6];
        Assert.Equal(255, pixel.R);
        Assert.InRange(pixel.G, 126, 129);
        Assert.InRange(pixel.B, 126, 129);
        Assert.Equal(255, pixel.A);
        Assert.Equal(White, surface[4, 4]);
    }

    /// <summary>
    ///     Test that the picture of the item is drawn inside the clip of its group.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageInClippedGroup_IsCut()
    {
        // Arrange: a picture of 20 by 20 at (0, 0) in a group at (10, 10) that is clipped to 8 by 8
        var resolver = new FakeResolver { Make = (_, _, _) => Solid(20, 20, Red) };
        var group = new SceneGroup("marks", 10, 10)
        {
            Clip = new SceneClip(0, 0, 8, 8),
            Children = [new SceneImage("mark-image", "icon.png", 0, 0)],
        };
        var scene = new Scene(40, 40, "white", new SceneGroup("root", 0, 0) { Children = [group] });
        using var images = new SceneImageSource(resolver, 256, (_, _, _) => { });

        // Act
        using var surface = new ScenePainter(new TextMeasurer(), images).Paint(scene, 40, 40, ScenePlacement.Uniform(1), Ct);

        // Assert
        Assert.Equal(Red, surface[10, 10]);
        Assert.Equal(Red, surface[17, 17]);
        Assert.Equal(White, surface[18, 10]);
        Assert.Equal(White, surface[10, 18]);
    }

    /// <summary>
    ///     Test that a picture that is not smoothed keeps hard edges when it is enlarged, and that a smoothed one is interpolated between the halves.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageWithoutSmoothing_KeepsHardEdges()
    {
        // Arrange: a 2 by 1 raster (red, blue) enlarged to 20 by 10
        Surface Make(string href, int w, int h)
        {
            var surface = new Surface(2, 1);
            surface[0, 0] = Red;
            surface[1, 0] = Blue;
            return surface;
        }

        // Act
        using var hard = PaintAtOne(new FakeResolver { Make = Make }, 30, 20, new SceneImage("mark-image", "x.png", 5, 5) { Width = 20, Height = 10, Smooth = false });

        // Assert: every pixel of the left half is exactly red and of the right half exactly blue, including the two next to the seam
        Assert.Equal(Red, hard[14, 7]);
        Assert.Equal(Blue, hard[15, 7]);
        Assert.Equal(Red, hard[5, 5]);
        Assert.Equal(Blue, hard[24, 14]);
    }

    /// <summary>
    ///     Test that the same link at the same size is asked for once, that two sizes are two calls, and that every surface is disposed when the painting ends.
    /// </summary>
    [Fact]
    public void SceneImageSource_Get_CachesWithinOnePaintingAndDisposesEverySurface()
    {
        // Arrange: three items with one link at one size and one with the same link at another size
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };
        var items = new SceneItem[]
        {
            new SceneImage("mark-image", "a.png", 0, 0) { Width = 5, Height = 5 },
            new SceneImage("mark-image", "a.png", 10, 0) { Width = 5, Height = 5 },
            new SceneImage("mark-image", "a.png", 20, 0) { Width = 5, Height = 5 },
            new SceneImage("mark-image", "a.png", 30, 0) { Width = 6, Height = 5 },
        };

        // Act
        using var surface = PaintAtOne(resolver, 40, 10, items);

        // Assert
        Assert.Equal([("a.png", 5, 5), ("a.png", 6, 5)], resolver.Calls);
        Assert.Equal(2, resolver.Handed.Count);
        foreach (var handed in resolver.Handed)
        {
            Assert.Throws<ObjectDisposedException>(() => handed[0, 0]);
        }

        Assert.Equal(Red, surface[22, 2]);
    }

    /// <summary>
    ///     Test that the call beyond the limit is not made and fails with the location of the mark, and that a repeated link does not count again.
    /// </summary>
    [Fact]
    public void SceneImageSource_Get_BeyondTheLimit_FailsAtTheMark()
    {
        // Arrange: a limit of two pictures and three distinct links
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };
        var location = new VegaLiteLocation("chart.vl.json", "/mark", 3, 4);
        var items = new SceneItem[]
        {
            new SceneImage("mark-image", "a.png", 0, 0) { Width = 5, Height = 5, Source = location },
            new SceneImage("mark-image", "a.png", 6, 0) { Width = 5, Height = 5, Source = location },
            new SceneImage("mark-image", "b.png", 12, 0) { Width = 5, Height = 5, Source = location },
            new SceneImage("mark-image", "c.png", 18, 0) { Width = 5, Height = 5, Source = location },
        };

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Paint(resolver, 40, 10, 1, [], 2, items));

        // Assert: the third distinct link was not asked for, the error is located at the mark and names the limit, and the surfaces that were handed out are released
        Assert.Equal(["a.png", "b.png"], resolver.Calls.Select(c => c.Href));
        Assert.Equal(location, ex.Location);
        Assert.Contains("MaxImages", ex.Message, StringComparison.Ordinal);
        Assert.All(resolver.Handed, handed => Assert.Throws<ObjectDisposedException>(() => handed[0, 0]));
    }

    /// <summary>
    ///     Test that a resolver that gives nothing is reported once for the link (also when several items use it), is asked once, and draws nothing.
    /// </summary>
    [Fact]
    public void SceneImageSource_Get_ResolverGivesNothing_ReportsOnceAndDrawsNothing()
    {
        // Arrange
        var resolver = new FakeResolver();
        var warnings = new List<string>();

        // Act
        using var surface = Paint(
            resolver,
            30,
            10,
            1,
            warnings,
            256,
            new SceneImage("mark-image", "missing.png", 0, 0),
            new SceneImage("mark-image", "missing.png", 10, 0));

        // Assert
        Assert.Single(resolver.Calls);
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-mark-image-unresolved:", warning, StringComparison.Ordinal);
        Assert.Contains("missing.png", warning, StringComparison.Ordinal);
        Assert.Equal(White, surface[1, 1]);
    }

    /// <summary>
    ///     Test that without a resolver nothing is asked, reported or drawn (the catalog reports the mark).
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_WithoutResolver_DrawsNothingAndReportsNothing()
    {
        // Arrange
        var warnings = new List<string>();

        // Act
        using var surface = Paint(null, 20, 20, 1, warnings, 256, new SceneImage("mark-image", "icon.png", 2, 2));

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(White, surface[3, 3]);
    }

    /// <summary>
    ///     Test that a painter without a loader skips the pictures.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_WithoutLoader_SkipsPictures()
    {
        // Arrange
        var scene = new Scene(20, 20, "white", new SceneGroup("root", 0, 0) { Children = [new SceneImage("mark-image", "icon.png", 2, 2)] });

        // Act
        using var surface = new ScenePainter(new TextMeasurer()).Paint(scene, 20, 20, ScenePlacement.Uniform(1), Ct);

        // Assert
        Assert.Equal(White, surface[3, 3]);
    }

    /// <summary>
    ///     Test that an exception of the resolver propagates unchanged and that the surfaces already handed out are released.
    /// </summary>
    [Fact]
    public void SceneImageSource_Get_ResolverThrows_PropagatesUnchanged()
    {
        // Arrange
        var failure = new InvalidOperationException("boom");
        var resolver = new FakeResolver
        {
            Make = (href, w, h) => href == "bad.png" ? throw failure : Solid(w, h, Red),
        };

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => PaintAtOne(
            resolver,
            30,
            10,
            new SceneImage("mark-image", "good.png", 0, 0) { Width = 5, Height = 5 },
            new SceneImage("mark-image", "bad.png", 10, 0) { Width = 5, Height = 5 }));

        // Assert
        Assert.Same(failure, ex);
        Assert.Throws<ObjectDisposedException>(() => resolver.Handed[0][0, 0]);
    }

    /// <summary>
    ///     Test the items that are never drawn and never asked for: a box without area, a box that is not finite or is negative, an item with no opacity and an empty link.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="opacity">The opacity.</param>
    /// <param name="x">The x position.</param>
    [Theory]
    [InlineData("a.png", 0d, 5d, 1d, 2d)]
    [InlineData("a.png", 5d, 0d, 1d, 2d)]
    [InlineData("a.png", -5d, 5d, 1d, 2d)]
    [InlineData("a.png", double.NaN, 5d, 1d, 2d)]
    [InlineData("a.png", 5d, double.PositiveInfinity, 1d, 2d)]
    [InlineData("a.png", 5d, 5d, 0d, 2d)]
    [InlineData("", 5d, 5d, 1d, 2d)]
    [InlineData("a.png", 5d, 5d, 1d, double.NaN)]
    [InlineData("a.png", 5d, 5d, 1d, 1e9)]
    public void ScenePainter_Paint_EmptyImage_IsNotAskedFor(string link, double width, double height, double opacity, double x)
    {
        // Arrange
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };

        // Act
        using var surface = PaintAtOne(resolver, 20, 20, new SceneImage("mark-image", link, x, 2) { Width = width, Height = height, Opacity = opacity });

        // Assert
        Assert.Empty(resolver.Calls);
        Assert.True(VegaLiteTestHelpers.AllPixelsAre(surface, White));
    }

    /// <summary>
    ///     Test the constructor argument checks of the loader and the item.
    /// </summary>
    [Fact]
    public void SceneImage_Constructors_RejectNullArguments()
    {
        // Act and assert
        Assert.Throws<ArgumentNullException>(() => new SceneImage("mark-image", null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => new SceneImageSource(null, 1, null!));
        Assert.Throws<ArgumentNullException>(() => new ScenePainter(null!, null));
    }

    /// <summary>
    ///     Test that the item holds Vega's defaults: left and top, the aspect kept and smoothing.
    /// </summary>
    [Fact]
    public void SceneImage_Defaults_AreVegasDefaults()
    {
        // Act
        var item = new SceneImage("mark-image", "a.png", 1, 2);

        // Assert
        Assert.Equal(("a.png", 1d, 2d, "left", "top", true, true, 1d), (item.Url, item.X, item.Y, item.Align, item.Baseline, item.Aspect, item.Smooth, item.Opacity));
        Assert.Null(item.Width);
        Assert.Null(item.Height);
    }

    /// <summary>
    ///     Test that a resolver that returns the same link repeatedly at two scales is asked in device pixels of each (the key of the cache includes the size).
    /// </summary>
    [Fact]
    public void SceneImageSource_Get_KeyIncludesTheDeviceSize()
    {
        // Arrange
        var resolver = new FakeResolver { Make = (_, w, h) => Solid(w, h, Red) };
        var warnings = new List<string>();
        using var source = new SceneImageSource(resolver, 10, (message, _, _) => warnings.Add(message));

        // Act
        var first = source.Get("a.png", 4, 4, default);
        var again = source.Get("a.png", 4, 4, default);
        var other = source.Get("a.png", 8, 8, default);

        // Assert
        Assert.Same(first, again);
        Assert.NotSame(first, other);
        Assert.True(source.HasResolver);
        Assert.Equal(2, resolver.Calls.Count);
        Assert.Empty(warnings);
    }
}
