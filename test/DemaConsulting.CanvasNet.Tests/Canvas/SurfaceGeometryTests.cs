// cspell:ignore bilinear premultiplied
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the rotation of the Surface class (part of the SurfaceGeometry unit): the size and pixels of a
///     rotation by any angle.
/// </summary>
public class SurfaceGeometryTests
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
    public void Surface_RotatedSize_RightAndObliqueAngles_ComputesCeilingOfBoundingBox()
    {
        Assert.Equal((100, 50), Surface.RotatedSize(100, 50, 0));
        Assert.Equal((50, 100), Surface.RotatedSize(100, 50, 90));
        Assert.Equal((107, 107), Surface.RotatedSize(100, 50, 45));
        Assert.Equal((112, 94), Surface.RotatedSize(100, 50, 30));
        Assert.Equal((2, 2), Surface.RotatedSize(1, 1, 45));
        Assert.Equal((189, 167), Surface.RotatedSize(160, 100, 30));
        Assert.Equal(Surface.RotatedSize(100, 50, 30), Surface.RotatedSize(100, 50, -330));
        Assert.Equal(Surface.RotatedSize(100, 50, 30), Surface.RotatedSize(100, 50, 210));
    }

    /// <summary>
    ///     Verifies that floating-point noise at an exact fit does not add a pixel.
    /// </summary>
    [Fact]
    public void Surface_RotatedSize_NearExactFit_DoesNotGainAPixel()
    {
        foreach (var degrees in new[] { 90.0, 180.0, 270.0, 360.0, -90.0, 450.0, 720.0 })
        {
            var expected = Math.Abs(degrees % 180) > 45 && Math.Abs(degrees % 180) < 135 ? (50, 100) : (100, 50);
            Assert.Equal(expected, Surface.RotatedSize(100, 50, degrees));
        }

        Assert.Equal((8192, 8192), Surface.RotatedSize(8192, 8192, 90));
        Assert.Equal((8192, 1), Surface.RotatedSize(8192, 1, 180));
    }

    /// <summary>
    ///     Verifies that a point right of the center moves below the center for a clockwise quarter turn.
    /// </summary>
    [Fact]
    public void Surface_RotateArbitrary_Clockwise_MovesRightOfCenterBelowCenter()
    {
        using var source = Solid(11, 11, White);
        var marker = new Rgba32(255, 0, 0, 255);
        source[8, 5] = marker;

        using var turned = source.RotateArbitrary(90, Black, nearest: true);

        Assert.Equal(11, turned.Width);
        Assert.Equal(11, turned.Height);
        Assert.Equal(marker, turned[5, 8]);
        Assert.Equal(White, turned[8, 5]);

        // A point above the center moves to the right of it.
        using var up = Solid(11, 11, White);
        up[5, 2] = marker;
        using var right = up.RotateArbitrary(90, Black, nearest: true);
        Assert.Equal(marker, right[8, 5]);
    }

    /// <summary>
    ///     Verifies that a solid image keeps its color throughout the interior of the rotated canvas.
    /// </summary>
    [Fact]
    public void Surface_RotateArbitrary_SolidImage_FillsInteriorWithSourceColor()
    {
        var color = new Rgba32(50, 100, 150, 255);
        using var source = Solid(30, 20, color);

        using var rotated = source.RotateArbitrary(20, Black, nearest: false);

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
    public void Surface_RotateArbitrary_RoundTripSmallAngle_StaysCloseToOriginal()
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

        using var there = source.RotateArbitrary(3, Black, nearest: false);
        using var back = there.RotateArbitrary(-3, Black, nearest: false);

        // 100x60 -> 104x66 -> 108x72: the original sits 4 columns and 6 rows into the result.
        Assert.Equal((104, 66), (there.Width, there.Height));
        Assert.Equal((108, 72), (back.Width, back.Height));
        for (var y = 10; y < 50; y++)
        {
            for (var x = 10; x < 90; x++)
            {
                var difference = Math.Abs(source[x, y].R - back[x + 4, y + 6].R);
                if (difference > 2)
                {
                    Assert.Fail($"Pixel ({x},{y}) differs by {difference}");
                }
            }
        }
    }

    /// <summary>
    ///     Verifies that the corners of the expanded canvas are exactly the fill color.
    /// </summary>
    [Fact]
    public void Surface_RotateArbitrary_Corners_AreFillColor()
    {
        var fill = new Rgba32(255, 0, 0, 255);
        using var source = Solid(100, 60, new Rgba32(0, 0, 255, 255));

        foreach (var nearest in new[] { true, false })
        {
            using var rotated = source.RotateArbitrary(30, fill, nearest);

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
    public void Surface_RotateArbitrary_TransparentFill_EdgePixelsFadeInAlpha()
    {
        using var source = Solid(40, 40, White);

        using var rotated = source.RotateArbitrary(45, new Rgba32(9, 9, 9, 0), nearest: false);

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
    public void Surface_RotateArbitrary_SemiTransparentFill_BlendsInPremultipliedSpace()
    {
        var fill = new Rgba32(0, 0, 255, 128);
        using var source = Solid(40, 40, new Rgba32(255, 0, 0, 255));

        using var rotated = source.RotateArbitrary(45, fill, nearest: false);

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
    public void Surface_RotateArbitrary_Nearest_CopiesPixelsWithoutBlending()
    {
        var first = new Rgba32(200, 30, 30, 255);
        var second = new Rgba32(30, 30, 200, 255);
        var fill = new Rgba32(0, 255, 0, 255);
        using var source = Solid(24, 24, first);
        FillRect(source, 12, 0, 12, 24, second);

        using var rotated = source.RotateArbitrary(17, fill, nearest: true);

        var fillCount = 0;
        for (var y = 0; y < rotated.Height; y++)
        {
            for (var x = 0; x < rotated.Width; x++)
            {
                var pixel = rotated[x, y];
                if (pixel != first && pixel != second && pixel != fill)
                {
                    Assert.Fail($"Pixel ({x},{y}) is {pixel}");
                }

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
    public void Surface_RotateArbitrary_Bilinear_BlendsAHardEdge()
    {
        using var source = Solid(40, 20, Black);
        FillRect(source, 20, 0, 20, 20, White);

        using var rotated = source.RotateArbitrary(10, White, nearest: false);

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
    public void Surface_RotateArbitrary_FastPathAndGenericPath_GiveSamePixels()
    {
        using var source = RandomSurface(37, 29);
        var fills = new[] { new Rgba32(1, 2, 3, 0), new Rgba32(10, 20, 30, 200), White };

        foreach (var fill in fills)
        {
            foreach (var degrees in new[] { 7.0, 30.0, 45.0, 90.0, 133.0, 200.0, 275.0, -12.5 })
            {
                using var fast = source.RotateArbitraryCore(degrees, fill, nearest: false, useFastPaths: true);
                using var generic = source.RotateArbitraryCore(degrees, fill, nearest: false, useFastPaths: false);

                SurfaceAssert.Equal(generic, fast);
            }
        }
    }

    /// <summary>
    ///     Verifies that a result larger than the maximum size is refused before anything is allocated.
    /// </summary>
    [Fact]
    public void Surface_RotateArbitrary_ResultTooLarge_ThrowsBeforeAllocating()
    {
        // The size itself is reported, and is larger than the maximum; an exact quarter turn of the largest image fits.
        Assert.Equal((11586, 11586), Surface.RotatedSize(8192, 8192, 45));
        Assert.Equal((8192, 8192), Surface.RotatedSize(8192, 8192, 90));

        // 8192x3400 at 45 degrees needs 8197 pixels on a side.
        using var source = new Surface(8192, 3400);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var thrown = Assert.Throws<ArgumentException>(() => source.RotateArbitrary(45, White, nearest: false));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("8197x8197", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("8192", thrown.Message, StringComparison.Ordinal);
        Assert.True(allocated < 1_000_000, $"{allocated} bytes were allocated before the refusal");
    }

    /// <summary>
    ///     Verifies that a huge finite angle is reduced modulo 360 degrees, so it gives the size and pixels of the
    ///     reduced angle rather than an overflowed one.
    /// </summary>
    [Fact]
    public void Surface_Rotation_HugeFiniteAngle_EqualsReducedAngle()
    {
        foreach (var huge in new[] { double.MaxValue, -double.MaxValue })
        {
            var reduced = huge % 360.0;
            var size = Surface.RotatedSize(100, 50, huge);

            Assert.Equal(Surface.RotatedSize(100, 50, reduced), size);
            Assert.True(size.Width >= 1 && size.Height >= 1);

            using var source = RandomSurface(13, 9);
            using var fromHuge = source.RotateArbitrary(huge, White, nearest: false);
            using var fromReduced = source.RotateArbitrary(reduced, White, nearest: false);
            SurfaceAssert.Equal(fromReduced, fromHuge);
        }

        Assert.Equal(Surface.RotatedSize(100, 50, 90), Surface.RotatedSize(100, 50, 360.0 * 1000 + 90));
    }

    /// <summary>
    ///     Verifies that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void Surface_RotationInvalidArguments_Throw()
    {
        using var surface = Solid(4, 4, White);

        Assert.Throws<ArgumentOutOfRangeException>(() => surface.RotateArbitrary(double.NaN, White, nearest: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.RotateArbitrary(double.PositiveInfinity, White, nearest: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(0, 5, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(5, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(Surface.MaxDimension + 1, 5, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(5, Surface.MaxDimension + 1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(5, 5, double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.RotatedSize(5, 5, double.NaN));
    }

    /// <summary>
    ///     Verifies that the rotation and the skew estimation of a disposed surface are refused.
    /// </summary>
    [Fact]
    public void Surface_RotationAndSkew_AfterDispose_ThrowObjectDisposedException()
    {
        var surface = Solid(4, 4, White);
        surface.Dispose();

        Assert.Throws<ObjectDisposedException>(() => surface.RotateArbitrary(10, White, nearest: false));
        Assert.Throws<ObjectDisposedException>(() => surface.EstimateSkew());
    }
}
