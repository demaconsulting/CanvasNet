using CanvasNet.Canvas;
using CanvasNet.Codecs;

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
    ///     Proves that a PNG save/load round-trip remains byte-exact at internal row-padding
    ///     boundary widths, confirming the stride/padding change is not observable through the
    ///     PNG codec.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void Surface_PngCodecRoundTrip_BoundaryWidths_ReturnsExpectedPixels(int width)
    {
        // Arrange: build a surface at a boundary width with distinct, non-trivial pixel values
        var surface = new Surface(width, 3);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 17 + 1), (byte)(y * 23 + 2), (byte)(x + y + 3), (byte)(200 - (x % 200)));
            }
        }

        using var stream = new MemoryStream();

        // Act: save and reload the surface through the PNG codec
        PngCodec.Save(surface, stream, PngColorType.Rgba);
        stream.Position = 0;
        var reloaded = PngCodec.Load(stream);

        // Assert: every pixel must round-trip exactly
        Assert.Equal(surface.Width, reloaded.Width);
        Assert.Equal(surface.Height, reloaded.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], reloaded[x, y]);
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
}
