// cspell:ignore premultiplied
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the whole-surface transformations of the Surface class: flip, rotate by 180 degrees, pad, trim,
///     fit into a box, flatten over a color, and the transparency test.
/// </summary>
public class SurfaceTransformsTests
{
    /// <summary>
    ///     The margin color used by the fit tests.
    /// </summary>
    private static readonly Rgba32 Fill = new(10, 20, 30, 255);

    /// <summary>
    ///     The color of the images that are fitted.
    /// </summary>
    private static readonly Rgba32 Ink = new(200, 100, 50, 255);

    /// <summary>
    ///     An opaque white color.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     An opaque black color.
    /// </summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     Creates a surface of one color.
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
    ///     Creates an opaque surface whose pixel at every coordinate is different.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The surface.</returns>
    private static Surface Gradient(int width, int height)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = new Rgba32(
                    (byte)(x * 255 / Math.Max(1, width - 1)),
                    (byte)(y * 255 / Math.Max(1, height - 1)),
                    (byte)(((x * 7) + (y * 13)) % 256),
                    255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Fills a rectangle of a surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    private static void FillRect(Surface surface, int left, int top, int width, int height, Rgba32 color)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                surface[x, y] = color;
            }
        }
    }

    /// <summary>
    ///     Test that the flips and the half turn move every pixel to its mirrored position and leave the source alone.
    /// </summary>
    [Fact]
    public void Surface_Flip_Pixels_MirrorAboutTheAxes()
    {
        // Arrange: a 5x3 surface with a different pixel everywhere
        using var source = Gradient(5, 3);
        using var original = source.Clone();

        // Act
        using var horizontal = source.FlipHorizontal();
        using var vertical = source.FlipVertical();
        using var half = source.Rotate180();

        // Assert: each pixel lands at the mirrored position and the source is unchanged
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(original[4 - x, y], horizontal[x, y]);
                Assert.Equal(original[x, 2 - y], vertical[x, y]);
                Assert.Equal(original[4 - x, 2 - y], half[x, y]);
            }
        }

        Assert.NotSame(source, horizontal);
        SurfaceAssert.Equal(original, source);
    }

    /// <summary>
    ///     Test that applying a flip twice, or both flips, gives the original or the half turn.
    /// </summary>
    [Fact]
    public void Surface_Flip_Twice_RestoresOriginalAndBothAxesEqualHalfTurn()
    {
        // Arrange
        using var source = Gradient(7, 4);

        // Act
        using var horizontalTwice = source.FlipHorizontal().FlipHorizontal();
        using var verticalTwice = source.FlipVertical().FlipVertical();
        using var both = source.FlipHorizontal().FlipVertical();
        using var half = source.Rotate180();

        // Assert
        SurfaceAssert.Equal(source, horizontalTwice);
        SurfaceAssert.Equal(source, verticalTwice);
        SurfaceAssert.Equal(half, both);
    }

    /// <summary>
    ///     Test that pad surrounds the surface with a border of the given color and copies the pixels without blending.
    /// </summary>
    [Fact]
    public void Surface_Pad_Border_AddsColoredBorderAndCopiesPixels()
    {
        // Arrange: a half-transparent 2x2 surface
        using var source = Solid(2, 2, new Rgba32(200, 100, 50, 128));

        // Act
        using var padded = source.Pad(1, 2, 3, 4, Fill);

        // Assert: 6x8 pixels, the source copied as is at (1, 2), the border the fill color
        Assert.Equal((6, 8), (padded.Width, padded.Height));
        Assert.Equal(new Rgba32(200, 100, 50, 128), padded[1, 2]);
        Assert.Equal(new Rgba32(200, 100, 50, 128), padded[2, 3]);
        Assert.Equal(Fill, padded[0, 0]);
        Assert.Equal(Fill, padded[3, 2]);
        Assert.Equal(Fill, padded[1, 4]);
        Assert.Equal(Fill, padded[5, 7]);
    }

    /// <summary>
    ///     Test that a transparent border color leaves the border transparent and a zero border is a copy.
    /// </summary>
    [Fact]
    public void Surface_Pad_TransparentColorOrNoBorder_LeavesBorderTransparentOrCopies()
    {
        // Arrange
        using var source = Solid(2, 2, Ink);

        // Act
        using var padded = source.Pad(1, 1, 1, 1, default);
        using var copy = source.Pad(0, 0, 0, 0, Fill);

        // Assert
        Assert.Equal(default, padded[0, 0]);
        Assert.Equal(Ink, padded[1, 1]);
        Assert.Equal(default, padded[3, 3]);
        Assert.NotSame(source, copy);
        SurfaceAssert.Equal(source, copy);
    }

    /// <summary>
    ///     Test that pad rejects a negative border and a result larger than the maximum before allocating.
    /// </summary>
    [Fact]
    public void Surface_Pad_InvalidBorder_Throws()
    {
        // Arrange
        using var source = Solid(2, 2, Ink);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Pad(-1, 0, 0, 0, Fill));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Pad(0, -1, 0, 0, Fill));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Pad(0, 0, -1, 0, Fill));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Pad(0, 0, 0, -1, Fill));
        Assert.Throws<ArgumentException>(() => source.Pad(Surface.MaxDimension, 0, 0, 0, Fill));
        Assert.Throws<ArgumentException>(() => source.Pad(0, 0, 0, Surface.MaxDimension - 1, Fill));
        Assert.Throws<ArgumentException>(() => source.Pad(int.MaxValue, 0, int.MaxValue, 0, Fill));

        // The largest permitted result is allowed
        using var largest = source.Pad(0, 0, Surface.MaxDimension - 2, 0, Fill);
        Assert.Equal(Surface.MaxDimension, largest.Width);
    }

    /// <summary>
    ///     Test that a uniform border is trimmed to the content box.
    /// </summary>
    [Fact]
    public void Surface_FindTrimBounds_UniformBorder_ReturnsContentBox()
    {
        using var surface = Solid(20, 10, White);
        FillRect(surface, 4, 2, 5, 3, Black);

        Assert.Equal((4, 2, 5, 3), surface.FindTrimBounds(8));

        using var trimmed = surface.Trim(8);
        Assert.Equal((5, 3), (trimmed.Width, trimmed.Height));
        Assert.Equal(Black, trimmed[0, 0]);
        Assert.Equal(Black, trimmed[4, 2]);
    }

    /// <summary>
    ///     Test that noise within the tolerance is background and noise beyond it is content.
    /// </summary>
    [Fact]
    public void Surface_FindTrimBounds_Tolerance_IgnoresNoiseBelowTolerance()
    {
        using var surface = Solid(20, 10, White);
        FillRect(surface, 8, 4, 4, 3, Black);
        surface[1, 1] = new Rgba32(250, 252, 255, 255);
        surface[18, 8] = new Rgba32(255, 255, 255, 248);

        Assert.Equal((8, 4, 4, 3), surface.FindTrimBounds(8));
        Assert.Equal((8, 4, 4, 3), surface.FindTrimBounds(7));

        // At a tolerance of 4 the pixel at 1,1 (distance 5) and at 18,8 (distance 7) are content.
        Assert.Equal((1, 1, 18, 8), surface.FindTrimBounds(4));
        Assert.Equal((1, 1, 18, 8), surface.FindTrimBounds(0));
    }

    /// <summary>
    ///     Test that all transparent pixels are equal whatever their color.
    /// </summary>
    [Fact]
    public void Surface_FindTrimBounds_TransparentBorder_TreatsAllTransparentPixelsAsEqual()
    {
        using var surface = Solid(12, 8, new Rgba32(0, 0, 0, 0));
        surface[0, 0] = new Rgba32(255, 0, 0, 0);
        surface[11, 0] = new Rgba32(0, 255, 0, 0);
        surface[0, 7] = new Rgba32(0, 0, 255, 0);
        surface[11, 7] = new Rgba32(7, 7, 7, 0);
        surface[5, 3] = new Rgba32(10, 20, 30, 255);
        surface[6, 4] = new Rgba32(10, 20, 30, 40);

        Assert.Equal((5, 3, 2, 2), surface.FindTrimBounds(8));
    }

    /// <summary>
    ///     Test that the most frequent corner color is the background and a tie goes to the first corner.
    /// </summary>
    [Fact]
    public void Surface_FindTrimBounds_CornerMajority_ChoosesMostFrequentCorner()
    {
        // Three white corners: the red pixel at the top right is content.
        using var majority = Solid(10, 10, White);
        majority[9, 0] = new Rgba32(255, 0, 0, 255);
        FillRect(majority, 3, 3, 2, 2, Black);

        Assert.Equal((3, 0, 7, 5), majority.FindTrimBounds(8));

        // Two white and two black corners tie, so the top left (white) wins and the black half is content.
        using var tie = Solid(10, 6, White);
        FillRect(tie, 5, 0, 5, 6, Black);

        Assert.Equal((5, 0, 5, 6), tie.FindTrimBounds(8));
    }

    /// <summary>
    ///     Test that an image without content has no trim bounds and is copied unchanged.
    /// </summary>
    [Fact]
    public void Surface_FindTrimBounds_AllBackground_ReturnsNull()
    {
        using var blank = Solid(9, 7, new Rgba32(200, 200, 200, 255));
        using var nearly = Solid(9, 7, new Rgba32(200, 200, 200, 255));
        nearly[4, 3] = new Rgba32(205, 195, 200, 255);
        using var single = Solid(1, 1, White);

        Assert.Null(blank.FindTrimBounds(0));
        Assert.Null(nearly.FindTrimBounds(8));
        Assert.Null(single.FindTrimBounds(0));

        using var copy = blank.Trim(0);
        SurfaceAssert.Equal(blank, copy);
        Assert.NotSame(blank, copy);
    }

    /// <summary>
    ///     Test that content touching every edge is not trimmed.
    /// </summary>
    [Fact]
    public void Surface_Trim_ContentTouchingEdges_ReturnsFullImage()
    {
        using var surface = Solid(8, 6, White);
        surface[0, 2] = Black;
        surface[7, 3] = Black;
        surface[3, 0] = Black;
        surface[4, 5] = Black;

        Assert.Equal((0, 0, 8, 6), surface.FindTrimBounds(8));

        using var trimmed = surface.Trim(8);
        SurfaceAssert.Equal(surface, trimmed);
    }

    /// <summary>
    ///     Test that a tolerance outside 0 to 255 is rejected.
    /// </summary>
    [Fact]
    public void Surface_Trim_InvalidTolerance_Throws()
    {
        using var surface = Solid(4, 4, White);

        Assert.Throws<ArgumentOutOfRangeException>(() => surface.FindTrimBounds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.FindTrimBounds(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Trim(256));
    }

    /// <summary>
    ///     Test that FitInside scales to the limiting side, rounds half away from zero, and clamps into the box.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="boxWidth">The width of the box.</param>
    /// <param name="boxHeight">The height of the box.</param>
    /// <param name="expectedWidth">The expected fitted width.</param>
    /// <param name="expectedHeight">The expected fitted height.</param>
    [Theory]
    [InlineData(100, 50, 20, 20, 20, 10)]
    [InlineData(50, 100, 20, 20, 10, 20)]
    [InlineData(10, 10, 40, 20, 20, 20)]
    [InlineData(3, 2, 10, 10, 10, 7)]
    [InlineData(5, 1, 3, 3, 3, 1)]
    [InlineData(1, 5000, 100, 100, 1, 100)]
    [InlineData(7, 7, 7, 7, 7, 7)]
    public void Surface_FitInside_RoundsAndClamps(
        int width, int height, int boxWidth, int boxHeight, int expectedWidth, int expectedHeight)
    {
        // Arrange / Act: fit the image into the box
        var fitted = Surface.FitInside(width, height, boxWidth, boxHeight);

        // Assert: the expected size (3x2 into 10x10 is 10 x 6.67, which rounds to 7)
        Assert.Equal((expectedWidth, expectedHeight), fitted);
    }

    /// <summary>
    ///     Test that FitInside rejects sizes below one.
    /// </summary>
    [Fact]
    public void Surface_FitInside_NonPositiveSize_ThrowsArgumentOutOfRange()
    {
        // Arrange / Act / Assert: each argument is checked
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.FitInside(0, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.FitInside(1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.FitInside(1, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.FitInside(1, 1, 1, 0));
    }

    /// <summary>
    ///     Test that a contain fit scales to the limiting side, enlarging a small image, and centers it on the fill.
    /// </summary>
    [Fact]
    public void Surface_FitInto_Contain_ScalesToLimitingSideAndCenters()
    {
        // Arrange: a 4x2 image and a 20x20 box
        using var source = Solid(4, 2, Ink);

        // Act
        using var fitted = source.FitInto(20, 20, FitMode.Contain, Fill, ResamplingMode.Nearest);

        // Assert: the image became 20x10 and sits in rows 5 to 14; the bands above and below are the fill
        Assert.Equal((20, 20), (fitted.Width, fitted.Height));
        Assert.Equal(Fill, fitted[0, 0]);
        Assert.Equal(Fill, fitted[19, 4]);
        Assert.Equal(Ink, fitted[0, 5]);
        Assert.Equal(Ink, fitted[19, 14]);
        Assert.Equal(Fill, fitted[10, 15]);
        Assert.Equal(Fill, fitted[19, 19]);
    }

    /// <summary>
    ///     Test that a stretch fit resamples to exactly the box.
    /// </summary>
    [Fact]
    public void Surface_FitInto_Stretch_ResamplesToExactBox()
    {
        // Arrange: a 4x2 image and a 20x20 box
        using var source = Solid(4, 2, Ink);

        // Act
        using var fitted = source.FitInto(20, 20, FitMode.Stretch, Fill, ResamplingMode.Nearest);

        // Assert: every pixel is the image color; there is no margin
        Assert.Equal((20, 20), (fitted.Width, fitted.Height));
        Assert.Equal(Ink, fitted[0, 0]);
        Assert.Equal(Ink, fitted[19, 19]);
        Assert.Equal(Ink, fitted[10, 3]);
    }

    /// <summary>
    ///     Test that an image that already has the size of the box is returned as an equal copy in both modes.
    /// </summary>
    /// <param name="stretch"><see langword="true"/> for the stretch mode, <see langword="false"/> for contain.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Surface_FitInto_SameSize_ReturnsEqualPixels(bool stretch)
    {
        // Arrange: a gradient that already has the size of the box
        var mode = stretch ? FitMode.Stretch : FitMode.Contain;
        using var source = Gradient(12, 8);

        // Act
        using var fitted = source.FitInto(12, 8, mode, Fill);

        // Assert: an independent copy with the same pixels
        Assert.NotSame(source, fitted);
        SurfaceAssert.Equal(source, fitted);
    }

    /// <summary>
    ///     Test that FitInto rejects an undefined mode and a box outside the permitted size.
    /// </summary>
    [Fact]
    public void Surface_FitInto_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert
        using var source = Solid(2, 2, Ink);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.FitInto(4, 4, (FitMode)99, Fill));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.FitInto(0, 4, FitMode.Contain, Fill));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.FitInto(4, Surface.MaxDimension + 1, FitMode.Stretch, Fill));
    }

    /// <summary>
    ///     Test that flatten composites over the background, returns a new opaque surface, and leaves the source alone.
    /// </summary>
    [Fact]
    public void Surface_FlattenOver_TranslucentPixels_CompositesOverBackground()
    {
        // Arrange: a half-transparent red pixel and a transparent pixel
        using var source = new Surface(2, 1);
        source[0, 0] = new Rgba32(255, 0, 0, 128);

        // Act
        using var flattened = source.FlattenOver(White);

        // Assert: red at alpha 128 over white, and the background where the source is transparent
        Assert.Equal(new Rgba32(255, 127, 127, 255), flattened[0, 0]);
        Assert.Equal(White, flattened[1, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 128), source[0, 0]);
        Assert.False(flattened.HasTransparency());
    }

    /// <summary>
    ///     Test that HasTransparency finds a single pixel that is not fully opaque.
    /// </summary>
    [Fact]
    public void Surface_HasTransparency_OpaqueAndTranslucentSurfaces_ReportsCorrectly()
    {
        // Arrange
        using var opaque = Solid(5, 4, Ink);
        using var translucent = Solid(5, 4, Ink);
        translucent[4, 3] = new Rgba32(1, 2, 3, 254);

        // Act / Assert
        Assert.False(opaque.HasTransparency());
        Assert.True(translucent.HasTransparency());
    }

    /// <summary>
    ///     Test that every transformation of a disposed surface throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public void Surface_Transforms_AfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        var surface = new Surface(2, 2);
        surface.Dispose();

        // Act / Assert
        Assert.Throws<ObjectDisposedException>(() => surface.FlipHorizontal());
        Assert.Throws<ObjectDisposedException>(() => surface.FlipVertical());
        Assert.Throws<ObjectDisposedException>(() => surface.Rotate180());
        Assert.Throws<ObjectDisposedException>(() => surface.Pad(1, 1, 1, 1, Fill));
        Assert.Throws<ObjectDisposedException>(() => surface.FindTrimBounds(8));
        Assert.Throws<ObjectDisposedException>(() => surface.Trim(8));
        Assert.Throws<ObjectDisposedException>(() => surface.FitInto(4, 4, FitMode.Contain, Fill));
        Assert.Throws<ObjectDisposedException>(() => surface.FlattenOver(White));
        Assert.Throws<ObjectDisposedException>(() => surface.HasTransparency());
    }
}
