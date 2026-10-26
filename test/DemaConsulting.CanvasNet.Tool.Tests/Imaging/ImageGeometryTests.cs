// cspell:ignore bilinear premultiplied
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageGeometry class: the size and pixels of rotation, and the bounds of a trim.
/// </summary>
public class ImageGeometryTests
{
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
    ///     Creates a surface of pseudo-random pixels, some translucent, from a fixed seed.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The surface.</returns>
    private static Surface RandomSurface(int width, int height)
    {
        var random = new Random(4711);
        var surface = new Surface(width, height);
        var bytes = new byte[4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                random.NextBytes(bytes);
                surface[x, y] = new Rgba32(bytes[0], bytes[1], bytes[2], random.Next(3) == 0 ? bytes[3] : (byte)255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Verifies the canvas size for right and oblique angles.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotatedSize_RightAndObliqueAngles_ComputesCeilingOfBoundingBox()
    {
        Assert.Equal((100, 50), ImageGeometry.RotatedSize(100, 50, 0));
        Assert.Equal((50, 100), ImageGeometry.RotatedSize(100, 50, 90));
        Assert.Equal((107, 107), ImageGeometry.RotatedSize(100, 50, 45));
        Assert.Equal((112, 94), ImageGeometry.RotatedSize(100, 50, 30));
        Assert.Equal((2, 2), ImageGeometry.RotatedSize(1, 1, 45));
        Assert.Equal((189, 167), ImageGeometry.RotatedSize(160, 100, 30));
        Assert.Equal(ImageGeometry.RotatedSize(100, 50, 30), ImageGeometry.RotatedSize(100, 50, -330));
        Assert.Equal(ImageGeometry.RotatedSize(100, 50, 30), ImageGeometry.RotatedSize(100, 50, 210));
    }

    /// <summary>
    ///     Verifies that floating-point noise at an exact fit does not add a pixel.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotatedSize_NearExactFit_DoesNotGainAPixel()
    {
        foreach (var degrees in new[] { 90.0, 180.0, 270.0, 360.0, -90.0, 450.0, 720.0 })
        {
            var expected = Math.Abs(degrees % 180) > 45 && Math.Abs(degrees % 180) < 135 ? (50, 100) : (100, 50);
            Assert.Equal(expected, ImageGeometry.RotatedSize(100, 50, degrees));
        }

        Assert.Equal((8192, 8192), ImageGeometry.RotatedSize(8192, 8192, 90));
        Assert.Equal((8192, 1), ImageGeometry.RotatedSize(8192, 1, 180));
    }

    /// <summary>
    ///     Verifies that a point right of the center moves below the center for a clockwise quarter turn.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_Clockwise_MovesRightOfCenterBelowCenter()
    {
        using var source = Solid(11, 11, White);
        var marker = new Rgba32(255, 0, 0, 255);
        source[8, 5] = marker;

        using var turned = ImageGeometry.RotateArbitrary(source, 90, Black, nearest: true);

        Assert.Equal(11, turned.Width);
        Assert.Equal(11, turned.Height);
        Assert.Equal(marker, turned[5, 8]);
        Assert.Equal(White, turned[8, 5]);

        // A point above the center moves to the right of it.
        using var up = Solid(11, 11, White);
        up[5, 2] = marker;
        using var right = ImageGeometry.RotateArbitrary(up, 90, Black, nearest: true);
        Assert.Equal(marker, right[8, 5]);
    }

    /// <summary>
    ///     Verifies that a solid image keeps its color throughout the interior of the rotated canvas.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_SolidImage_FillsInteriorWithSourceColor()
    {
        var color = new Rgba32(50, 100, 150, 255);
        using var source = Solid(30, 20, color);

        using var rotated = ImageGeometry.RotateArbitrary(source, 20, Black, nearest: false);

        var (cx, cy) = (rotated.Width / 2, rotated.Height / 2);
        for (var y = cy - 3; y <= cy + 3; y++)
        {
            for (var x = cx - 3; x <= cx + 3; x++)
            {
                Assert.Equal(color, rotated[x, y]);
            }
        }
    }

    /// <summary>
    ///     Verifies that rotating a smooth image by a small angle and back stays close to the original.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_RoundTripSmallAngle_StaysCloseToOriginal()
    {
        // Bilinear sampling reproduces a linear gradient exactly, so only rounding differs.
        using var source = new Surface(100, 60);
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                var level = (byte)(x + y);
                source[x, y] = new Rgba32(level, level, level, 255);
            }
        }

        using var there = ImageGeometry.RotateArbitrary(source, 3, Black, nearest: false);
        using var back = ImageGeometry.RotateArbitrary(there, -3, Black, nearest: false);

        // 100x60 -> 104x66 -> 108x72: the original sits 4 columns and 6 rows into the result.
        Assert.Equal((104, 66), (there.Width, there.Height));
        Assert.Equal((108, 72), (back.Width, back.Height));
        for (var y = 10; y < 50; y++)
        {
            for (var x = 10; x < 90; x++)
            {
                var difference = Math.Abs(source[x, y].R - back[x + 4, y + 6].R);
                Assert.True(difference <= 2, $"Pixel ({x},{y}) differs by {difference}");
            }
        }
    }

    /// <summary>
    ///     Verifies that the corners of the expanded canvas are exactly the fill color.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_Corners_AreFillColor()
    {
        var fill = new Rgba32(255, 0, 0, 255);
        using var source = Solid(100, 60, new Rgba32(0, 0, 255, 255));

        foreach (var nearest in new[] { true, false })
        {
            using var rotated = ImageGeometry.RotateArbitrary(source, 30, fill, nearest);

            Assert.Equal((117, 102), (rotated.Width, rotated.Height));
            Assert.Equal(fill, rotated[0, 0]);
            Assert.Equal(fill, rotated[rotated.Width - 1, 0]);
            Assert.Equal(fill, rotated[0, rotated.Height - 1]);
            Assert.Equal(fill, rotated[rotated.Width - 1, rotated.Height - 1]);
        }
    }

    /// <summary>
    ///     Verifies that a transparent fill gives transparent corners and a soft alpha edge.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_TransparentFill_EdgePixelsFadeInAlpha()
    {
        using var source = Solid(40, 40, White);

        using var rotated = ImageGeometry.RotateArbitrary(source, 45, new Rgba32(9, 9, 9, 0), nearest: false);

        Assert.Equal(default, rotated[0, 0]);
        Assert.Equal(White, rotated[rotated.Width / 2, rotated.Height / 2]);
        var partial = 0;
        for (var y = 0; y < rotated.Height; y++)
        {
            for (var x = 0; x < rotated.Width; x++)
            {
                var pixel = rotated[x, y];
                if (pixel.A is > 0 and < 255)
                {
                    partial++;

                    // The color of the white source is not darkened by the transparent fill.
                    Assert.Equal(new Rgba32(255, 255, 255, pixel.A), pixel);
                }
                else if (pixel.A == 0)
                {
                    Assert.Equal(default, pixel);
                }
            }
        }

        Assert.True(partial > 40, $"Only {partial} edge pixels have a partial alpha");
    }

    /// <summary>
    ///     Verifies that a semi-transparent fill is blended with the image in premultiplied space.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_SemiTransparentFill_BlendsInPremultipliedSpace()
    {
        var fill = new Rgba32(0, 0, 255, 128);
        using var source = Solid(40, 40, new Rgba32(255, 0, 0, 255));

        using var rotated = ImageGeometry.RotateArbitrary(source, 45, fill, nearest: false);

        Assert.Equal(fill, rotated[0, 0]);
        var checkedPixels = 0;
        for (var y = 0; y < rotated.Height; y++)
        {
            for (var x = 0; x < rotated.Width; x++)
            {
                var pixel = rotated[x, y];
                if (pixel.A is <= 128 or >= 255)
                {
                    continue;
                }

                // Coverage f of the red image and 1 - f of the fill: alpha = 128 + 127 f.
                var coverage = (pixel.A - 128) / 127.0;
                var expectedRed = coverage * 255 * 255 / pixel.A;
                var expectedBlue = (1 - coverage) * 128 * 255 / pixel.A;
                Assert.InRange(pixel.R, expectedRed - 4, expectedRed + 4);
                Assert.InRange(pixel.B, expectedBlue - 4, expectedBlue + 4);
                Assert.Equal(0, pixel.G);
                checkedPixels++;
            }
        }

        Assert.True(checkedPixels > 20, $"Only {checkedPixels} blended edge pixels were found");
    }

    /// <summary>
    ///     Verifies that nearest sampling copies source pixels and never blends.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_Nearest_CopiesPixelsWithoutBlending()
    {
        var first = new Rgba32(200, 30, 30, 255);
        var second = new Rgba32(30, 30, 200, 255);
        var fill = new Rgba32(0, 255, 0, 255);
        using var source = Solid(24, 24, first);
        FillRect(source, 12, 0, 12, 24, second);

        using var rotated = ImageGeometry.RotateArbitrary(source, 17, fill, nearest: true);

        var fillCount = 0;
        for (var y = 0; y < rotated.Height; y++)
        {
            for (var x = 0; x < rotated.Width; x++)
            {
                var pixel = rotated[x, y];
                Assert.True(pixel == first || pixel == second || pixel == fill, $"Pixel ({x},{y}) is {pixel}");
                if (pixel == fill)
                {
                    fillCount++;
                }
            }
        }

        Assert.True(fillCount > 0);
    }

    /// <summary>
    ///     Verifies that bilinear sampling makes intermediate grays along a hard edge.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_Bilinear_BlendsAHardEdge()
    {
        using var source = Solid(40, 20, Black);
        FillRect(source, 20, 0, 20, 20, White);

        using var rotated = ImageGeometry.RotateArbitrary(source, 10, White, nearest: false);

        var grays = 0;
        for (var y = 0; y < rotated.Height; y++)
        {
            for (var x = 0; x < rotated.Width; x++)
            {
                var pixel = rotated[x, y];
                if (pixel.R is > 10 and < 245)
                {
                    Assert.Equal(pixel.R, pixel.G);
                    Assert.Equal(pixel.R, pixel.B);
                    Assert.Equal(255, pixel.A);
                    grays++;
                }
            }
        }

        Assert.True(grays >= 10, $"Only {grays} blended pixels were found");
    }

    /// <summary>
    ///     Verifies that the fast paths give the same pixels as the generic sampler.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_FastPathAndGenericPath_GiveSamePixels()
    {
        using var source = RandomSurface(37, 29);
        var fills = new[] { new Rgba32(1, 2, 3, 0), new Rgba32(10, 20, 30, 200), White };

        foreach (var fill in fills)
        {
            foreach (var degrees in new[] { 7.0, 30.0, 45.0, 90.0, 133.0, 200.0, 275.0, -12.5 })
            {
                using var fast = ImageGeometry.RotateArbitrary(source, degrees, fill, nearest: false, useFastPaths: true);
                using var generic = ImageGeometry.RotateArbitrary(source, degrees, fill, nearest: false, useFastPaths: false);

                SurfaceAssert.Equal(generic, fast);
            }
        }
    }

    /// <summary>
    ///     Verifies that a result larger than the maximum size is refused before anything is allocated.
    /// </summary>
    [Fact]
    public void ImageGeometry_RotateArbitrary_ResultTooLarge_ThrowsSizeLimitBeforeAllocating()
    {
        var thrown = Assert.Throws<ToolException>(() => ImageGeometry.CheckRotatedSize(8192, 8192, 45));
        Assert.Equal(ErrorCodes.SizeLimit, thrown.Code);
        Assert.Contains("11586", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("--fit", thrown.Hint, StringComparison.Ordinal);

        // An exact quarter turn of the largest image fits.
        Assert.Equal((8192, 8192), ImageGeometry.CheckRotatedSize(8192, 8192, 90));

        // 8192x3400 at 45 degrees needs 8273 pixels on a side.
        using var source = new Surface(8192, 3400);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fromRotate = Assert.Throws<ToolException>(() => ImageGeometry.RotateArbitrary(source, 45, White, nearest: false));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(ErrorCodes.SizeLimit, fromRotate.Code);
        Assert.True(allocated < 1_000_000, $"{allocated} bytes were allocated before the refusal");
    }

    /// <summary>
    ///     Verifies that a uniform border is trimmed to the content box.
    /// </summary>
    [Fact]
    public void ImageGeometry_FindTrimBounds_UniformBorder_ReturnsContentBox()
    {
        using var surface = Solid(20, 10, White);
        FillRect(surface, 4, 2, 5, 3, Black);

        Assert.Equal((4, 2, 5, 3), ImageGeometry.FindTrimBounds(surface, 8));

        using var trimmed = ImageGeometry.Trim(surface, 8);
        Assert.Equal((5, 3), (trimmed.Width, trimmed.Height));
        Assert.Equal(Black, trimmed[0, 0]);
        Assert.Equal(Black, trimmed[4, 2]);
    }

    /// <summary>
    ///     Verifies that noise within the tolerance is background and noise beyond it is content.
    /// </summary>
    [Fact]
    public void ImageGeometry_FindTrimBounds_Tolerance_IgnoresNoiseBelowTolerance()
    {
        using var surface = Solid(20, 10, White);
        FillRect(surface, 8, 4, 4, 3, Black);
        surface[1, 1] = new Rgba32(250, 252, 255, 255);
        surface[18, 8] = new Rgba32(255, 255, 255, 248);

        Assert.Equal((8, 4, 4, 3), ImageGeometry.FindTrimBounds(surface, 8));
        Assert.Equal((8, 4, 4, 3), ImageGeometry.FindTrimBounds(surface, 7));

        // At a tolerance of 4 the pixel at 1,1 (distance 5) and at 18,8 (distance 7) are content.
        Assert.Equal((1, 1, 18, 8), ImageGeometry.FindTrimBounds(surface, 4));
        Assert.Equal((1, 1, 18, 8), ImageGeometry.FindTrimBounds(surface, 0));
    }

    /// <summary>
    ///     Verifies that all transparent pixels are equal whatever their color.
    /// </summary>
    [Fact]
    public void ImageGeometry_FindTrimBounds_TransparentBorder_TreatsAllTransparentPixelsAsEqual()
    {
        using var surface = Solid(12, 8, new Rgba32(0, 0, 0, 0));
        surface[0, 0] = new Rgba32(255, 0, 0, 0);
        surface[11, 0] = new Rgba32(0, 255, 0, 0);
        surface[0, 7] = new Rgba32(0, 0, 255, 0);
        surface[11, 7] = new Rgba32(7, 7, 7, 0);
        surface[5, 3] = new Rgba32(10, 20, 30, 255);
        surface[6, 4] = new Rgba32(10, 20, 30, 40);

        Assert.Equal((5, 3, 2, 2), ImageGeometry.FindTrimBounds(surface, 8));
    }

    /// <summary>
    ///     Verifies that the most frequent corner color is the background and a tie goes to the first corner.
    /// </summary>
    [Fact]
    public void ImageGeometry_FindTrimBounds_CornerMajority_ChoosesMostFrequentCorner()
    {
        // Three white corners: the red pixel at the top right is content.
        using var majority = Solid(10, 10, White);
        majority[9, 0] = new Rgba32(255, 0, 0, 255);
        FillRect(majority, 3, 3, 2, 2, Black);

        Assert.Equal((3, 0, 7, 5), ImageGeometry.FindTrimBounds(majority, 8));

        // Two white and two black corners tie, so the top left (white) wins and the black half is content.
        using var tie = Solid(10, 6, White);
        FillRect(tie, 5, 0, 5, 6, Black);

        Assert.Equal((5, 0, 5, 6), ImageGeometry.FindTrimBounds(tie, 8));
    }

    /// <summary>
    ///     Verifies that an image without content has no trim bounds and is copied unchanged.
    /// </summary>
    [Fact]
    public void ImageGeometry_FindTrimBounds_AllBackground_ReturnsNull()
    {
        using var blank = Solid(9, 7, new Rgba32(200, 200, 200, 255));
        using var nearly = Solid(9, 7, new Rgba32(200, 200, 200, 255));
        nearly[4, 3] = new Rgba32(205, 195, 200, 255);
        using var single = Solid(1, 1, White);

        Assert.Null(ImageGeometry.FindTrimBounds(blank, 0));
        Assert.Null(ImageGeometry.FindTrimBounds(nearly, 8));
        Assert.Null(ImageGeometry.FindTrimBounds(single, 0));

        using var copy = ImageGeometry.Trim(blank, 0);
        SurfaceAssert.Equal(blank, copy);
        Assert.NotSame(blank, copy);
    }

    /// <summary>
    ///     Verifies that content touching every edge is not trimmed.
    /// </summary>
    [Fact]
    public void ImageGeometry_Trim_ContentTouchingEdges_ReturnsFullImage()
    {
        using var surface = Solid(8, 6, White);
        surface[0, 2] = Black;
        surface[7, 3] = Black;
        surface[3, 0] = Black;
        surface[4, 5] = Black;

        Assert.Equal((0, 0, 8, 6), ImageGeometry.FindTrimBounds(surface, 8));

        using var trimmed = ImageGeometry.Trim(surface, 8);
        SurfaceAssert.Equal(surface, trimmed);
    }

    /// <summary>
    ///     Verifies that null and invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void ImageGeometry_NullOrInvalidArguments_Throw()
    {
        using var surface = Solid(4, 4, White);

        Assert.Throws<ArgumentNullException>(() => ImageGeometry.RotateArbitrary(null!, 10, White, nearest: false));
        Assert.Throws<ArgumentNullException>(() => ImageGeometry.FindTrimBounds(null!, 8));
        Assert.Throws<ArgumentNullException>(() => ImageGeometry.Trim(null!, 8));

        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.RotateArbitrary(surface, double.NaN, White, nearest: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.RotateArbitrary(surface, double.PositiveInfinity, White, nearest: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.RotatedSize(0, 5, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.RotatedSize(5, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.RotatedSize(5, 5, double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.CheckRotatedSize(5, 5, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.FindTrimBounds(surface, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.FindTrimBounds(surface, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageGeometry.Trim(surface, 256));
    }
}
