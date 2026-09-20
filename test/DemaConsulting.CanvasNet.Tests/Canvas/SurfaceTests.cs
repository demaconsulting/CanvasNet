using CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the Surface class and the Rgba32 struct.
/// </summary>
public class SurfaceTests
{
    /// <summary>
    ///     Proves that the constructor stores the supplied width and height on the Width and
    ///     Height properties.
    /// </summary>
    [Fact]
    public void Surface_Constructor_ValidDimensions_SetsWidthAndHeight()
    {
        // Arrange & Act: construct a surface with known dimensions
        var surface = new Surface(3, 2);

        // Assert: Width and Height must reflect the constructor arguments
        Assert.Equal(3, surface.Width);
        Assert.Equal(2, surface.Height);
    }

    /// <summary>
    ///     Proves that a freshly constructed surface has every pixel initialized to fully
    ///     transparent (all channels zero).
    /// </summary>
    [Fact]
    public void Surface_Constructor_ValidDimensions_BufferIsAllZero()
    {
        // Arrange & Act: construct a surface with no explicit initialization
        var surface = new Surface(2, 2);

        // Assert: every pixel in every row must be fully transparent black
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                Assert.Equal(default, pixel);
            }
        }
    }

    /// <summary>
    ///     Proves that constructing a surface with zero width throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: zero width must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(0, 1));
    }

    /// <summary>
    ///     Proves that constructing a surface with a negative width throws
    ///     ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: negative width must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(-1, 1));
    }

    /// <summary>
    ///     Proves that constructing a surface with zero height throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: zero height must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(1, 0));
    }

    /// <summary>
    ///     Proves that constructing a surface with a negative height throws
    ///     ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: negative height must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(1, -1));
    }

    /// <summary>
    ///     Proves that constructing a surface with width at the maximum permitted dimension
    ///     succeeds.
    /// </summary>
    [Fact]
    public void Surface_Constructor_WidthAtMaximum_Succeeds()
    {
        // Act: construct a surface with width at the maximum permitted dimension
        var surface = new Surface(8192, 1);

        // Assert: the surface reports the requested width
        Assert.Equal(8192, surface.Width);
    }

    /// <summary>
    ///     Proves that constructing a surface with height at the maximum permitted dimension
    ///     succeeds.
    /// </summary>
    [Fact]
    public void Surface_Constructor_HeightAtMaximum_Succeeds()
    {
        // Act: construct a surface with height at the maximum permitted dimension
        var surface = new Surface(1, 8192);

        // Assert: the surface reports the requested height
        Assert.Equal(8192, surface.Height);
    }

    /// <summary>
    ///     Proves that constructing a surface with a width exceeding the maximum permitted
    ///     dimension throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_WidthExceedsMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: a width beyond the maximum permitted dimension must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(8193, 1));
    }

    /// <summary>
    ///     Proves that constructing a surface with a height exceeding the maximum permitted
    ///     dimension throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Constructor_HeightExceedsMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert: a height beyond the maximum permitted dimension must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new Surface(1, 8193));
    }

    /// <summary>
    ///     Proves that setting a pixel through the indexer and reading it back returns the
    ///     stored value.
    /// </summary>
    [Fact]
    public void Surface_Indexer_SetThenGet_ReturnsStoredPixel()
    {
        // Arrange: create a surface and a distinct pixel value
        var surface = new Surface(4, 4);
        var pixel = new Rgba32(10, 20, 30, 40);

        // Act: store the pixel then read it back
        surface[2, 1] = pixel;
        var result = surface[2, 1];

        // Assert: the read value must exactly match the stored value
        Assert.Equal(pixel, result);
    }

    /// <summary>
    ///     Proves that writing to the span returned by GetRowSpanBytes is reflected by the
    ///     indexer, confirming the span aliases the surface's own storage.
    /// </summary>
    [Fact]
    public void Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange()
    {
        // Arrange: create a surface and obtain the raw byte span for row 0
        var surface = new Surface(2, 2);
        var row = surface.GetRowSpanBytes(0);

        // Act: write raw RGBA bytes for the first pixel directly into the span
        row[0] = 1;
        row[1] = 2;
        row[2] = 3;
        row[3] = 4;

        // Assert: the indexer must observe the same bytes as a pixel
        Assert.Equal(new Rgba32(1, 2, 3, 4), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that writing to the span returned by GetRowSpan is reflected by the indexer,
    ///     confirming the span aliases the surface's own storage.
    /// </summary>
    [Fact]
    public void Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange()
    {
        // Arrange: create a surface and obtain the pixel span for row 1
        var surface = new Surface(3, 2);
        var row = surface.GetRowSpan(1);

        // Act: write a pixel directly through the span
        row[2] = new Rgba32(5, 6, 7, 8);

        // Assert: the indexer must observe the same pixel value
        Assert.Equal(new Rgba32(5, 6, 7, 8), surface[2, 1]);
    }

    /// <summary>
    ///     Proves that Crop returns a new surface containing exactly the pixels from the
    ///     requested sub-region.
    /// </summary>
    [Fact]
    public void Surface_Crop_ValidRegion_ReturnsExpectedPixels()
    {
        // Arrange: create a 4x4 surface with a distinct pixel value at every coordinate
        var surface = new Surface(4, 4);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)x, (byte)y, 0, 255);
            }
        }

        // Act: crop a 2x2 region starting at (1, 1)
        var cropped = surface.Crop(1, 1, 2, 2);

        // Assert: the cropped surface must have the requested size and matching pixels
        Assert.Equal(2, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(surface[1, 1], cropped[0, 0]);
        Assert.Equal(surface[2, 1], cropped[1, 0]);
        Assert.Equal(surface[1, 2], cropped[0, 1]);
        Assert.Equal(surface[2, 2], cropped[1, 1]);
    }

    /// <summary>
    ///     Proves that modifying the surface returned by Crop does not affect the source surface,
    ///     confirming the two buffers are independent.
    /// </summary>
    [Fact]
    public void Surface_Crop_ModifyResult_DoesNotAffectSource()
    {
        // Arrange: create a source surface and crop it
        var source = new Surface(2, 2);
        source[0, 0] = new Rgba32(1, 1, 1, 1);
        var cropped = source.Crop(0, 0, 2, 2);

        // Act: modify the cropped result
        cropped[0, 0] = new Rgba32(255, 255, 255, 255);

        // Assert: the source surface must retain its original pixel
        Assert.Equal(new Rgba32(1, 1, 1, 1), source[0, 0]);
    }

    /// <summary>
    ///     Proves that modifying the source surface after cropping does not affect the
    ///     previously cropped result, confirming the two buffers are independent.
    /// </summary>
    [Fact]
    public void Surface_Crop_ModifySource_DoesNotAffectResult()
    {
        // Arrange: create a source surface and crop it
        var source = new Surface(2, 2);
        source[0, 0] = new Rgba32(1, 1, 1, 1);
        var cropped = source.Crop(0, 0, 2, 2);

        // Act: modify the source surface after the crop was taken
        source[0, 0] = new Rgba32(255, 255, 255, 255);

        // Assert: the cropped result must retain the pixel value captured at crop time
        Assert.Equal(new Rgba32(1, 1, 1, 1), cropped[0, 0]);
    }

    /// <summary>
    ///     Proves that Crop rejects a negative x coordinate with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_NegativeX_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: negative x must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(-1, 0, 2, 2));
    }

    /// <summary>
    ///     Proves that Crop rejects a negative y coordinate with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_NegativeY_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: negative y must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(0, -1, 2, 2));
    }

    /// <summary>
    ///     Proves that Crop rejects a zero width with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_ZeroWidth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: zero width must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(0, 0, 0, 2));
    }

    /// <summary>
    ///     Proves that Crop rejects a zero height with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_ZeroHeight_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: zero height must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(0, 0, 2, 0));
    }

    /// <summary>
    ///     Proves that Crop rejects a region whose width exceeds the source surface's bounds
    ///     with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_WidthExceedsSourceBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: x + width exceeding Width must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(3, 0, 2, 2));
    }

    /// <summary>
    ///     Proves that Crop rejects a region whose height exceeds the source surface's bounds
    ///     with ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Surface_Crop_HeightExceedsSourceBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a surface to crop
        var surface = new Surface(4, 4);

        // Act & Assert: y + height exceeding Height must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Crop(0, 3, 2, 2));
    }

    /// <summary>
    ///     Row widths, in pixels, that exercise the internal row-padding boundary (padding is
    ///     applied in multiples of 16 pixels): widths at, just below, and just above each
    ///     boundary, plus a couple of larger "normal" widths.
    /// </summary>
    public static TheoryData<int> BoundaryWidths =>
    [
        1, 15, 16, 17, 31, 32, 33, 100, 257
    ];

    /// <summary>
    ///     Proves that GetRowSpanBytes always returns a span of exactly Width * 4 bytes,
    ///     regardless of internal row-padding boundaries, for every row of the surface.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_GetRowSpanBytes_BoundaryWidths_ReturnsWidthTimesFourLength(int width)
    {
        // Arrange: construct a surface at a boundary width, with a couple of rows
        var surface = new Surface(width, 2);

        // Act & Assert: every row's byte span must be exactly Width * 4 bytes long
        for (var y = 0; y < surface.Height; y++)
        {
            Assert.Equal(width * 4, surface.GetRowSpanBytes(y).Length);
        }
    }

    /// <summary>
    ///     Proves that GetRowSpan always returns a span of exactly Width pixels, regardless of
    ///     internal row-padding boundaries, for every row of the surface.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_GetRowSpan_BoundaryWidths_ReturnsWidthLength(int width)
    {
        // Arrange: construct a surface at a boundary width, with a couple of rows
        var surface = new Surface(width, 2);

        // Act & Assert: every row's pixel span must be exactly Width pixels long
        for (var y = 0; y < surface.Height; y++)
        {
            Assert.Equal(width, surface.GetRowSpan(y).Length);
        }
    }

    /// <summary>
    ///     Proves that Crop remains byte-exact at internal row-padding boundary widths,
    ///     confirming the stride/padding change is not observable through Crop.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_Crop_BoundaryWidths_ReturnsExpectedPixels(int width)
    {
        // Arrange: build a surface at a boundary width with a distinct pixel value per column
        var surface = new Surface(width, 3);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x % 256), (byte)(y * 10), 7, 255);
            }
        }

        // Act: crop the full surface
        var cropped = surface.Crop(0, 0, width, 3);

        // Assert: every pixel must survive the crop unchanged
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], cropped[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that Rgba32 fields store the values supplied to the constructor.
    /// </summary>
    [Fact]
    public void Rgba32_FieldAssignment_StoresChannelValues()
    {
        // Arrange & Act: construct a pixel with distinct channel values
        var pixel = new Rgba32(1, 2, 3, 4);

        // Assert: each field must match the constructor argument
        Assert.Equal(1, pixel.R);
        Assert.Equal(2, pixel.G);
        Assert.Equal(3, pixel.B);
        Assert.Equal(4, pixel.A);
    }

    /// <summary>
    ///     Proves that two Rgba32 values with identical channel values are equal.
    /// </summary>
    [Fact]
    public void Rgba32_Equals_SameChannelValues_ReturnsTrue()
    {
        // Arrange: construct two independent pixels with identical channel values
        var first = new Rgba32(9, 8, 7, 6);
        var second = new Rgba32(9, 8, 7, 6);

        // Act & Assert: equality (and operator ==) must hold for equal channel values
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
    }

    /// <summary>
    ///     Premultiply-alpha test vectors: (color, alpha, expectedPremultipliedColor), computed
    ///     independently of Surface's implementation using round(color * alpha / 255) with
    ///     round-half-away-from-zero.
    /// </summary>
    public static TheoryData<byte, byte, byte> PremultiplyVectors =>
    new()
    {
        { 200, 128, 100 },
        { 100, 128, 50 },
        { 50, 128, 25 },
        { 255, 255, 255 },
        { 0, 255, 0 },
        { 255, 0, 0 },
        { 37, 17, 2 },
        { 250, 10, 10 },
    };

    /// <summary>
    ///     Proves that PremultiplyAlpha converts each color channel to
    ///     round(channel * alpha / 255), leaving alpha unchanged, for a range of representative
    ///     color/alpha combinations including full transparency, full opacity, and partial alpha.
    /// </summary>
    [Theory]
    [MemberData(nameof(PremultiplyVectors))]
    public void Surface_PremultiplyAlpha_VariousValues_ComputesExpectedPixels(byte color, byte alpha, byte expected)
    {
        // Arrange: a single-pixel surface with the same component value in every color channel
        var surface = new Surface(1, 1);
        surface[0, 0] = new Rgba32(color, color, color, alpha);

        // Act
        surface.PremultiplyAlpha();

        // Assert: every color channel matches the independently computed expected value; alpha
        // is unchanged
        var pixel = surface[0, 0];
        Assert.Equal(new Rgba32(expected, expected, expected, alpha), pixel);
    }

    /// <summary>
    ///     Proves that PremultiplyAlpha correctly processes every visible pixel of a multi-row
    ///     surface at each internal row-padding boundary width. Each pixel is given a distinct
    ///     (color, alpha) pair derived from its (x, y) position, so a row-offset or
    ///     padding-boundary bug - which a single-pixel test cannot detect - would show up as a
    ///     mismatch at some specific pixel.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_PremultiplyAlpha_MultiRowBoundaryWidths_ComputesExpectedPixelForEveryPixel(int width)
    {
        // Arrange: build a surface at a boundary width, three rows tall, with a distinct
        // (color, alpha) pair at every pixel position
        const int height = 3;
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = (byte)((x * 7 + y * 29) % 256);
                var alpha = (byte)((x * 3 + y * 41) % 256);
                surface[x, y] = new Rgba32(color, color, color, alpha);
            }
        }

        // Act
        surface.PremultiplyAlpha();

        // Assert: every visible pixel, at every row, matches the independently computed
        // expected value; alpha is unchanged
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = (byte)((x * 7 + y * 29) % 256);
                var alpha = (byte)((x * 3 + y * 41) % 256);
                var expected = ComputeExpectedPremultiplied(color, alpha);
                Assert.Equal(new Rgba32(expected, expected, expected, alpha), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Computes the expected premultiplied channel value independently of Surface's SIMD
    ///     float32 implementation, using plain scalar double arithmetic, so that the multi-row
    ///     boundary-width tests have a genuine oracle rather than re-deriving the formula under
    ///     test.
    /// </summary>
    private static byte ComputeExpectedPremultiplied(byte color, byte alpha)
    {
        var value = Math.Round(color * alpha / 255.0, MidpointRounding.AwayFromZero);
        return (byte)Math.Clamp(value, 0, 255);
    }

    /// <summary>
    ///     Unpremultiply-alpha test vectors: (premultipliedColor, alpha, expectedStraightColor),
    ///     computed independently of Surface's implementation using
    ///     round(premultipliedColor * 255 / alpha), clamped to [0, 255], with alpha == 0 defined
    ///     as producing 0.
    /// </summary>
    public static TheoryData<byte, byte, byte> UnpremultiplyVectors =>
    new()
    {
        { 100, 128, 199 },
        { 0, 0, 0 },
        { 255, 255, 255 },
        { 1, 17, 15 },
        { 250, 10, 255 }, // exercises the load-bearing clamp: the raw division exceeds 255
    };

    /// <summary>
    ///     Proves that UnpremultiplyAlpha converts each color channel to
    ///     round(channel * 255 / alpha), clamped to [0, 255], leaving alpha unchanged, for a
    ///     range of representative premultiplied-color/alpha combinations.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnpremultiplyVectors))]
    public void Surface_UnpremultiplyAlpha_VariousValues_ComputesExpectedPixels(byte color, byte alpha, byte expected)
    {
        // Arrange: a single-pixel surface with the same component value in every color channel
        var surface = new Surface(1, 1);
        surface[0, 0] = new Rgba32(color, color, color, alpha);

        // Act
        surface.UnpremultiplyAlpha();

        // Assert: every color channel matches the independently computed expected value; alpha
        // is unchanged
        var pixel = surface[0, 0];
        Assert.Equal(new Rgba32(expected, expected, expected, alpha), pixel);
    }

    /// <summary>
    ///     Proves that UnpremultiplyAlpha maps a fully transparent pixel (alpha == 0) to
    ///     R = G = B = 0, the documented degenerate case, regardless of the (unrecoverable) input
    ///     color channel values.
    /// </summary>
    [Fact]
    public void Surface_UnpremultiplyAlpha_AlphaZero_ResultIsZeroRgb()
    {
        // Arrange: a fully transparent pixel with arbitrary, non-zero color channel garbage
        var surface = new Surface(1, 1);
        surface[0, 0] = new Rgba32(123, 45, 67, 0);

        // Act
        surface.UnpremultiplyAlpha();

        // Assert: RGB must be zero; alpha remains zero
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that UnpremultiplyAlpha correctly processes every visible pixel of a multi-row
    ///     surface at each internal row-padding boundary width. Each pixel is given a distinct
    ///     (color, alpha) pair derived from its (x, y) position, so a row-offset or
    ///     padding-boundary bug - which a single-pixel test cannot detect - would show up as a
    ///     mismatch at some specific pixel. Alpha is restricted to [1, 255] so the degenerate
    ///     alpha == 0 case (already covered by
    ///     <see cref="Surface_UnpremultiplyAlpha_AlphaZero_ResultIsZeroRgb"/>) stays out of
    ///     scope here.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_UnpremultiplyAlpha_MultiRowBoundaryWidths_ComputesExpectedPixelForEveryPixel(int width)
    {
        // Arrange: build a surface at a boundary width, three rows tall, with a distinct
        // (color, alpha) pair at every pixel position; alpha is never zero
        const int height = 3;
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = (byte)((x * 7 + y * 29) % 256);
                var alpha = (byte)(1 + (x * 3 + y * 41) % 255);
                surface[x, y] = new Rgba32(color, color, color, alpha);
            }
        }

        // Act
        surface.UnpremultiplyAlpha();

        // Assert: every visible pixel, at every row, matches the independently computed
        // expected value; alpha is unchanged
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = (byte)((x * 7 + y * 29) % 256);
                var alpha = (byte)(1 + (x * 3 + y * 41) % 255);
                var expected = ComputeExpectedUnpremultiplied(color, alpha);
                Assert.Equal(new Rgba32(expected, expected, expected, alpha), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Computes the expected unpremultiplied channel value independently of Surface's SIMD
    ///     float32 implementation, using plain scalar double arithmetic, so that the multi-row
    ///     boundary-width test has a genuine oracle rather than re-deriving the formula under
    ///     test. Callers must not pass alpha == 0 (the degenerate case is defined separately, not
    ///     via this division-based formula).
    /// </summary>
    private static byte ComputeExpectedUnpremultiplied(byte color, byte alpha)
    {
        var value = Math.Round(color * 255.0 / alpha, MidpointRounding.AwayFromZero);
        return (byte)Math.Clamp(value, 0, 255);
    }

    /// <summary>
    ///     Proves that premultiplying then unpremultiplying a non-degenerate (non-zero,
    ///     non-255-alpha) pixel round-trips within the documented lossy rounding tolerance,
    ///     rather than asserting false exactness.
    /// </summary>
    [Theory]
    [InlineData(200, 128)]
    [InlineData(37, 90)]
    [InlineData(10, 200)]
    public void Surface_PremultiplyThenUnpremultiplyAlpha_PartialAlpha_RoundTripsWithinTolerance(byte color, byte alpha)
    {
        // Arrange
        var surface = new Surface(1, 1);
        surface[0, 0] = new Rgba32(color, color, color, alpha);

        // Act: round-trip through premultiply then unpremultiply
        surface.PremultiplyAlpha();
        surface.UnpremultiplyAlpha();

        // Assert: each channel must be within 1 of the original value - premultiply is lossy
        // (integer rounding on the way in), so exact equality is not guaranteed, but the error
        // must not exceed a single rounding step
        var pixel = surface[0, 0];
        Assert.InRange(pixel.R, (byte)Math.Max(0, color - 1), (byte)Math.Min(255, color + 1));
        Assert.Equal(alpha, pixel.A);
    }

    /// <summary>
    ///     Proves that premultiplying then unpremultiplying alpha 0 and alpha 255 pixels
    ///     round-trips exactly (no rounding loss at these boundary alpha values).
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 255)]
    [InlineData(255, 255)]
    [InlineData(128, 255)]
    public void Surface_PremultiplyThenUnpremultiplyAlpha_BoundaryAlpha_RoundTripsExactly(byte color, byte alpha)
    {
        // Arrange
        var surface = new Surface(1, 1);
        surface[0, 0] = new Rgba32(color, color, color, alpha);
        var expected = alpha == 0 ? new Rgba32(0, 0, 0, 0) : new Rgba32(color, color, color, alpha);

        // Act
        surface.PremultiplyAlpha();
        surface.UnpremultiplyAlpha();

        // Assert
        Assert.Equal(expected, surface[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) with a fully opaque foreground replaces the
    ///     background pixel entirely, for every color channel.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_OpaqueForeground_ReplacesBackground()
    {
        // Arrange: an opaque foreground pixel and a distinctly different background pixel
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(10, 20, 30, 255);
        var foreground = new Surface(1, 1);
        foreground[0, 0] = new Rgba32(200, 150, 100, 255);

        // Act
        background.CompositeOver(foreground);

        // Assert: the result must exactly equal the opaque foreground pixel
        Assert.Equal(new Rgba32(200, 150, 100, 255), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) with a fully transparent foreground leaves the
    ///     background pixel entirely unchanged.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_TransparentForeground_LeavesBackgroundUnchanged()
    {
        // Arrange: a fully transparent foreground pixel (color channels are garbage - they must
        // not affect the result) and a distinct, partially transparent background pixel
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(80, 160, 240, 120);
        var foreground = new Surface(1, 1);
        foreground[0, 0] = new Rgba32(30, 60, 90, 0);

        // Act
        background.CompositeOver(foreground);

        // Assert: the background must be completely unaffected
        Assert.Equal(new Rgba32(80, 160, 240, 120), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) zeroes the result when both the background and
    ///     foreground are fully transparent, exercising the <c>outA == 0</c> guard path that
    ///     avoids a NaN 0/0 division: with a nonzero-alpha background, <c>outA</c> is never zero,
    ///     so no existing test observes this path.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_BothFullyTransparent_ResultIsZero()
    {
        // Arrange: both background and foreground fully transparent, with distinct nonzero
        // "garbage" RGB values that must not survive into the result
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(200, 50, 30, 0);
        var foreground = new Surface(1, 1);
        foreground[0, 0] = new Rgba32(10, 220, 90, 0);

        // Act
        background.CompositeOver(foreground);

        // Assert: outA == 0 must force every channel to exactly zero, not NaN or garbage
        Assert.Equal(new Rgba32(0, 0, 0, 0), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) with partially transparent foreground and
    ///     background pixels matches an independently hand-computed Porter-Duff "over" result
    ///     (computed separately from Surface's own implementation, not by re-deriving the same
    ///     formula under test).
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_PartialAlpha_MatchesIndependentlyComputedResult()
    {
        // Arrange: both operands partially transparent
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(80, 160, 240, 120);
        var foreground = new Surface(1, 1);
        foreground[0, 0] = new Rgba32(30, 60, 90, 180);

        // Act
        background.CompositeOver(foreground);

        // Assert: expected value independently computed (Porter-Duff "over", normalized [0,1]
        // math, round-half-away-from-zero, clamped) - see the developer's companion computation
        Assert.Equal(new Rgba32(38, 76, 115, 215), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) throws ArgumentNullException when the foreground
    ///     argument is null.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_NullForeground_ThrowsArgumentNullException()
    {
        // Arrange
        var background = new Surface(2, 2);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => background.CompositeOver((Surface)null!));
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) throws ArgumentException when the foreground's
    ///     width does not match this surface's width.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_WidthMismatch_ThrowsArgumentException()
    {
        // Arrange
        var background = new Surface(3, 2);
        var foreground = new Surface(4, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => background.CompositeOver(foreground));
    }

    /// <summary>
    ///     Proves that CompositeOver(Surface) throws ArgumentException when the foreground's
    ///     height does not match this surface's height.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverSurface_HeightMismatch_ThrowsArgumentException()
    {
        // Arrange
        var background = new Surface(3, 2);
        var foreground = new Surface(3, 5);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => background.CompositeOver(foreground));
    }

    /// <summary>
    ///     Proves that CompositeOver(Rgba32) with a fully opaque color replaces the background
    ///     pixel entirely, for every color channel.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverColor_OpaqueColor_ReplacesBackground()
    {
        // Arrange
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(10, 20, 30, 255);

        // Act
        background.CompositeOver(new Rgba32(200, 150, 100, 255));

        // Assert
        Assert.Equal(new Rgba32(200, 150, 100, 255), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Rgba32) with a fully transparent color leaves the
    ///     background pixel entirely unchanged.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverColor_TransparentColor_LeavesBackgroundUnchanged()
    {
        // Arrange
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(80, 160, 240, 120);

        // Act: color channels are garbage on a fully transparent color - they must not affect
        // the result
        background.CompositeOver(new Rgba32(30, 60, 90, 0));

        // Assert
        Assert.Equal(new Rgba32(80, 160, 240, 120), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Rgba32) with a partially transparent color over an opaque
    ///     background matches an independently hand-computed Porter-Duff "over" result.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverColor_PartialAlpha_MatchesIndependentlyComputedResult()
    {
        // Arrange: opaque background, semi-transparent overlay color
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(0, 255, 0, 255);

        // Act
        background.CompositeOver(new Rgba32(255, 0, 0, 128));

        // Assert: expected value independently computed (Porter-Duff "over" against an opaque
        // background simplifies to the standard alpha-blend formula: outC = fgC*a + bgC*(1-a))
        Assert.Equal(new Rgba32(128, 127, 0, 255), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Rgba32) zeroes the result when both the background pixel
    ///     and the overlay color are fully transparent, exercising the <c>outA == 0</c> guard
    ///     path that avoids a NaN 0/0 division: with a nonzero-alpha background, <c>outA</c> is
    ///     never zero, so no existing test observes this path.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverColor_BothFullyTransparent_ResultIsZero()
    {
        // Arrange: a fully transparent background pixel and a fully transparent overlay color,
        // both with distinct nonzero "garbage" RGB values that must not survive into the result
        var background = new Surface(1, 1);
        background[0, 0] = new Rgba32(200, 50, 30, 0);

        // Act
        background.CompositeOver(new Rgba32(10, 220, 90, 0));

        // Assert: outA == 0 must force every channel to exactly zero, not NaN or garbage
        Assert.Equal(new Rgba32(0, 0, 0, 0), background[0, 0]);
    }

    /// <summary>
    ///     Proves that CompositeOver(Rgba32) applies the constant color identically to every
    ///     pixel across multiple rows and columns, not just a single-pixel surface.
    /// </summary>
    [Fact]
    public void Surface_CompositeOverColor_MultiRowSurface_AppliesToEveryPixel()
    {
        // Arrange: a 3x2 opaque background surface, uniformly filled
        var background = new Surface(3, 2);
        for (var y = 0; y < background.Height; y++)
        {
            for (var x = 0; x < background.Width; x++)
            {
                background[x, y] = new Rgba32(10, 20, 30, 255);
            }
        }

        // Act: composite a fully opaque color over every pixel
        background.CompositeOver(new Rgba32(200, 150, 100, 255));

        // Assert: every pixel must equal the opaque overlay color
        for (var y = 0; y < background.Height; y++)
        {
            for (var x = 0; x < background.Width; x++)
            {
                Assert.Equal(new Rgba32(200, 150, 100, 255), background[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that Surface.MaxDimension is declared as a genuinely public member (not merely
    ///     internal) and equals the documented maximum of 8192, so that external callers (for
    ///     example a GetInfo-based caller performing bomb triage before Load) can compare a
    ///     header-declared dimension against it. A plain compile-time reference to
    ///     Surface.MaxDimension alone cannot prove this - the test assembly already has
    ///     InternalsVisibleTo access to the main assembly (see the main project's csproj), so an
    ///     internal member would compile and read identically here; only a reflection-based check
    ///     of the field's declared accessibility, which InternalsVisibleTo does not affect, proves
    ///     the member is truly public.
    /// </summary>
    [Fact]
    public void Surface_MaxDimension_IsPubliclyAccessible_Equals8192()
    {
        // Act: read the constant exactly as an external, non-test-internals caller would
        const int maxDimension = Surface.MaxDimension;

        // Act: reflect on the field's declared accessibility, which is unaffected by this test
        // assembly's InternalsVisibleTo access, unlike a plain compile-time reference
        var field = typeof(Surface).GetField(nameof(Surface.MaxDimension))!;

        // Assert: the documented maximum value is unchanged by the visibility widening, and the
        // field is genuinely declared public (not merely reachable via InternalsVisibleTo)
        Assert.Equal(8192, maxDimension);
        Assert.True(field.IsPublic);
    }
}
