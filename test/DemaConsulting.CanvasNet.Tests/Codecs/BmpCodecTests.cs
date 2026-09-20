using CanvasNet.Canvas;
using CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the BmpCodec class and the BmpBitDepth enum.
/// </summary>
public class BmpCodecTests
{
    /// <summary>
    ///     The number of leading bytes of a BITMAPINFOHEADER used by
    ///     <see cref="MakeMinimalBmpBytes"/> (biSize through biCompression); headers shorter
    ///     than this (for example a 12-byte BITMAPCOREHEADER) have no room for these fields.
    /// </summary>
    private const int InfoHeaderFieldsSize = 20;

    /// <summary>
    ///     Builds a small surface with distinct, non-trivial per-pixel RGBA values so that
    ///     channel-swap and orientation bugs cannot hide behind uniform pixel data.
    /// </summary>
    private static Surface BuildTestCanvas(int width, int height)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = new Rgba32(
                    (byte)(x * 17 + 1),
                    (byte)(y * 23 + 2),
                    (byte)(x + y + 3),
                    (byte)(200 - x - y));
            }
        }

        return surface;
    }

    /// <summary>
    ///     Proves that saving at Bit24 and loading back reproduces every pixel's RGB values
    ///     exactly, with alpha forced to 255 (opaque) regardless of the source alpha.
    /// </summary>
    [Fact]
    public void BmpCodec_SaveThenLoad_24Bit_ReturnsExpectedPixelsWithOpaqueAlpha()
    {
        // Arrange: build a surface with varied, non-opaque pixel values
        var surface = BuildTestCanvas(4, 3);
        using var stream = new MemoryStream();

        // Act: save at 24-bit depth then load back
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit24);
        stream.Position = 0;
        var loaded = BmpCodec.Load(stream);

        // Assert: dimensions and every pixel's RGB match, with alpha forced to 255
        Assert.Equal(surface.Width, loaded.Width);
        Assert.Equal(surface.Height, loaded.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var source = surface[x, y];
                var result = loaded[x, y];
                Assert.Equal(source.R, result.R);
                Assert.Equal(source.G, result.G);
                Assert.Equal(source.B, result.B);
                Assert.Equal(255, result.A);
            }
        }
    }

    /// <summary>
    ///     Proves that saving at Bit32 and loading back reproduces every pixel's RGBA values
    ///     exactly, including alpha.
    /// </summary>
    [Fact]
    public void BmpCodec_SaveThenLoad_32Bit_ReturnsExpectedPixelsIncludingAlpha()
    {
        // Arrange: build a surface with varied pixel values including non-opaque alpha
        var surface = BuildTestCanvas(4, 3);
        using var stream = new MemoryStream();

        // Act: save at 32-bit depth then load back
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit32);
        stream.Position = 0;
        var loaded = BmpCodec.Load(stream);

        // Assert: every pixel, including alpha, matches exactly
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], loaded[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that a row whose byte width is not a multiple of four is padded to the next
    ///     multiple of four with zero bytes, verified directly at the byte level.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_WidthRequiringPadding_ProducesCorrectlyPaddedRows()
    {
        // Arrange: width 3 at 24-bit => 9 bytes/row, which needs 3 padding bytes to reach 12
        var surface = BuildTestCanvas(3, 2);
        using var stream = new MemoryStream();

        // Act: save at 24-bit depth
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit24);
        var bytes = stream.ToArray();

        // Assert: the pixel data section length equals paddedRowBytes * height (12 * 2 = 24),
        // and the three padding bytes at the end of each row are zero
        const int paddedRowBytes = 12;
        const int pixelDataOffset = 54;
        Assert.Equal(pixelDataOffset + paddedRowBytes * 2, bytes.Length);
        for (var row = 0; row < 2; row++)
        {
            var rowStart = pixelDataOffset + row * paddedRowBytes;
            Assert.Equal(0, bytes[rowStart + 9]);
            Assert.Equal(0, bytes[rowStart + 10]);
            Assert.Equal(0, bytes[rowStart + 11]);
        }

        // Assert: round-trip still reproduces the expected pixels despite the padding
        stream.Position = 0;
        var loaded = BmpCodec.Load(stream);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var source = surface[x, y];
                var result = loaded[x, y];
                Assert.Equal(source.R, result.R);
                Assert.Equal(source.G, result.G);
                Assert.Equal(source.B, result.B);
            }
        }
    }

    /// <summary>
    ///     Proves that the first row written to the file is the surface's last (bottom) row,
    ///     independently of the Load path, confirming bottom-up orientation.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_DistinctTopAndBottomRows_WritesBottomRowFirstInFile()
    {
        // Arrange: a 2x2 surface with a distinct, known pixel in row 0 (top) and row 1 (bottom)
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(10, 20, 30, 255);
        surface[1, 0] = new Rgba32(11, 21, 31, 255);
        surface[0, 1] = new Rgba32(40, 50, 60, 255);
        surface[1, 1] = new Rgba32(41, 51, 61, 255);
        using var stream = new MemoryStream();

        // Act: save at 32-bit depth (4 bytes/pixel, no padding needed for width 2)
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit32);
        var bytes = stream.ToArray();

        // Assert: the first row in the file (immediately after the 54-byte header) must be the
        // surface's bottom row (row 1), stored as BGRA
        const int pixelDataOffset = 54;
        Assert.Equal(60, bytes[pixelDataOffset]); // B
        Assert.Equal(50, bytes[pixelDataOffset + 1]); // G
        Assert.Equal(40, bytes[pixelDataOffset + 2]); // R
        Assert.Equal(255, bytes[pixelDataOffset + 3]); // A
        Assert.Equal(61, bytes[pixelDataOffset + 4]); // B
        Assert.Equal(51, bytes[pixelDataOffset + 5]); // G
        Assert.Equal(41, bytes[pixelDataOffset + 6]); // R
    }

    /// <summary>
    ///     Proves that Save(Surface, string, BmpBitDepth) and Load(string) round-trip a surface
    ///     through a real file on disk.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_FromFilePath_ReturnsExpectedPixels()
    {
        // Arrange: build a surface and a temporary file path
        var surface = BuildTestCanvas(3, 3);
        var path = Path.GetTempFileName();
        try
        {
            // Act: save to the file path then load from it
            BmpCodec.Save(surface, path, BmpBitDepth.Bit32);
            var loaded = BmpCodec.Load(path);

            // Assert: every pixel matches
            for (var y = 0; y < surface.Height; y++)
            {
                for (var x = 0; x < surface.Width; x++)
                {
                    Assert.Equal(surface[x, y], loaded[x, y]);
                }
            }
        }
        finally
        {
            // Cleanup: always remove the temporary file, even if an assertion failed
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that Save rejects a null surface with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_NullCanvas_ThrowsArgumentNullException()
    {
        // Arrange: a valid destination stream
        using var stream = new MemoryStream();

        // Act & Assert: a null surface must be rejected
        Assert.Throws<ArgumentNullException>(() => BmpCodec.Save(null!, stream));
    }

    /// <summary>
    ///     Proves that Save rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_NullStream_ThrowsArgumentNullException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: a null stream must be rejected
        Assert.Throws<ArgumentNullException>(() => BmpCodec.Save(surface, (Stream)null!));
    }

    /// <summary>
    ///     Proves that Save rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_NullPath_ThrowsArgumentNullException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: a null path must be rejected
        Assert.Throws<ArgumentNullException>(() => BmpCodec.Save(surface, (string)null!));
    }

    /// <summary>
    ///     Proves that Save rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_EmptyPath_ThrowsArgumentException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: an empty path must be rejected
        Assert.Throws<ArgumentException>(() => BmpCodec.Save(surface, string.Empty));
    }

    /// <summary>
    ///     Proves that Save rejects an undefined BmpBitDepth value with
    ///     ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void BmpCodec_Save_UndefinedBitDepth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a valid surface, stream, and an undefined bit depth value
        var surface = new Surface(1, 1);
        using var stream = new MemoryStream();
        const BmpBitDepth undefined = (BmpBitDepth)99;

        // Act & Assert: the undefined bit depth must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => BmpCodec.Save(surface, stream, undefined));
    }

    /// <summary>
    ///     Proves that Load rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_NullStream_ThrowsArgumentNullException()
    {
        // Act & Assert: a null stream must be rejected
        Assert.Throws<ArgumentNullException>(() => BmpCodec.Load((Stream)null!));
    }

    /// <summary>
    ///     Proves that Load rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert: a null path must be rejected
        Assert.Throws<ArgumentNullException>(() => BmpCodec.Load((string)null!));
    }

    /// <summary>
    ///     Proves that Load rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_EmptyPath_ThrowsArgumentException()
    {
        // Act & Assert: an empty path must be rejected
        Assert.Throws<ArgumentException>(() => BmpCodec.Load(string.Empty));
    }

    /// <summary>
    ///     Proves that Load rejects a stream not starting with the "BM" signature with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_BadSignature_ThrowsInvalidDataException()
    {
        // Arrange: a 14-byte header with an incorrect signature
        var bytes = new byte[14];
        bytes[0] = (byte)'X';
        bytes[1] = (byte)'X';
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the bad signature must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a 12-byte BITMAPCOREHEADER (identified by biSize) with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_Bitmapcoreheader12Bytes_ThrowsInvalidDataException()
    {
        // Arrange: a valid file header followed by a biSize of 12 (BITMAPCOREHEADER)
        var bytes = MakeMinimalBmpBytes(biSize: 12, biBitCount: 24, biCompression: 0, biHeight: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported header size must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a 108-byte BITMAPV4HEADER (identified by biSize) with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_UnsupportedHeaderSizeV4_ThrowsInvalidDataException()
    {
        // Arrange: a valid file header followed by a biSize of 108 (BITMAPV4HEADER)
        var bytes = MakeMinimalBmpBytes(biSize: 108, biBitCount: 24, biCompression: 0, biHeight: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported header size must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects RLE-compressed pixel data (biCompression != BI_RGB) with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_RleCompression_ThrowsInvalidDataException()
    {
        // Arrange: a valid header declaring BI_RLE8 (1) compression
        var bytes = MakeMinimalBmpBytes(biSize: 40, biBitCount: 8, biCompression: 1, biHeight: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported compression must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects an 8-bit palette-based bit depth with InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_8BitPaletteDepth_ThrowsInvalidDataException()
    {
        // Arrange: a valid header declaring an 8-bit (palette-based) bit depth
        var bytes = MakeMinimalBmpBytes(biSize: 40, biBitCount: 8, biCompression: 0, biHeight: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported bit depth must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a negative (top-down) height with InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_TopDownNegativeHeight_ThrowsInvalidDataException()
    {
        // Arrange: a valid header declaring a negative (top-down) height
        var bytes = MakeMinimalBmpBytes(biSize: 40, biBitCount: 24, biCompression: 0, biHeight: -1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported top-down orientation must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream that ends before the declared pixel data has been
    ///     fully read, with InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_TruncatedStream_ThrowsInvalidDataException()
    {
        // Arrange: a valid header for a 1x1 24-bit image, but with no pixel data following it
        var bytes = MakeMinimalBmpBytes(biSize: 40, biBitCount: 24, biCompression: 0, biHeight: 1, includePixelData: false);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the truncated stream must be rejected
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a width exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        // Arrange: a valid header declaring a width one above Surface.MaxDimension
        var bytes = MakeMinimalBmpBytes(
            biSize: 40, biBitCount: 24, biCompression: 0, biHeight: 1, biWidth: Surface.MaxDimension + 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the oversized width must be rejected as malformed data, not as an
        // out-of-range constructor argument
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a height exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor.
    /// </summary>
    [Fact]
    public void BmpCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException()
    {
        // Arrange: a valid header declaring a height one above Surface.MaxDimension
        var bytes = MakeMinimalBmpBytes(
            biSize: 40, biBitCount: 24, biCompression: 0, biHeight: Surface.MaxDimension + 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the oversized height must be rejected as malformed data, not as an
        // out-of-range constructor argument
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, channel count, and no-alpha flag
    ///     for a 24-bit BMP, without needing to Load (decode) the pixel data.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_24Bit_ReturnsExpectedInfoWithoutAlpha()
    {
        // Arrange: save a 3x2 surface at Bit24
        using var stream = new MemoryStream();
        var surface = BuildTestCanvas(3, 2);
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit24);
        stream.Position = 0;

        // Act
        var info = BmpCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(3, 2, 3, false), info);
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, channel count, and alpha flag for
    ///     a 32-bit BMP, without needing to Load (decode) the pixel data.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_32Bit_ReturnsExpectedInfoWithAlpha()
    {
        // Arrange: save a 3x2 surface at Bit32
        using var stream = new MemoryStream();
        var surface = BuildTestCanvas(3, 2);
        BmpCodec.Save(surface, stream, BmpBitDepth.Bit32);
        stream.Position = 0;

        // Act
        var info = BmpCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(3, 2, 4, true), info);
    }

    /// <summary>
    ///     Proves that GetInfo consumes only the 54-byte header, never reading any pixel data,
    ///     by wrapping a valid BMP's bytes in a stream that throws if more than 54 bytes are read.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_NeverReadsPixelData()
    {
        // Arrange: a valid 4x4 32-bit BMP, wrapped so any read past the 54-byte header throws
        using var source = new MemoryStream();
        BmpCodec.Save(BuildTestCanvas(4, 4), source, BmpBitDepth.Bit32);
        var bytes = source.ToArray();
        using var bounded = new BoundedReadStream(new MemoryStream(bytes), maxBytes: 54);

        // Act
        var info = BmpCodec.GetInfo(bounded);

        // Assert
        Assert.Equal(new ImageInfo(4, 4, 4, true), info);
    }

    /// <summary>
    ///     Proves that GetInfo(Stream) rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BmpCodec.GetInfo((Stream)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BmpCodec.GetInfo((string)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => BmpCodec.GetInfo(string.Empty));
    }

    /// <summary>
    ///     Proves that GetInfo rejects a stream not starting with the "BM" signature with
    ///     InvalidDataException, mirroring Load's malformed-header rejection.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_BadSignature_ThrowsInvalidDataException()
    {
        // Arrange: a 14-byte header with an incorrect signature
        var bytes = new byte[14];
        bytes[0] = (byte)'X';
        bytes[1] = (byte)'X';
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => BmpCodec.GetInfo(stream));
    }

    /// <summary>
    ///     Proves that GetInfo does not enforce Surface.MaxDimension - it returns the raw
    ///     oversized header dimensions rather than throwing - while Load on the exact same
    ///     bytes still throws InvalidDataException.
    /// </summary>
    [Fact]
    public void BmpCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows()
    {
        // Arrange: a header declaring a width one above Surface.MaxDimension
        var bytes = MakeMinimalBmpBytes(
            biSize: 40, biBitCount: 24, biCompression: 0, biHeight: 1, biWidth: Surface.MaxDimension + 1,
            includePixelData: false);

        // Act: GetInfo must not throw, and must report the raw oversized width
        using var infoStream = new MemoryStream(bytes);
        var info = BmpCodec.GetInfo(infoStream);
        Assert.Equal(Surface.MaxDimension + 1, info.Width);

        // Assert: Load on the same bytes still rejects the oversized width
        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => BmpCodec.Load(loadStream));
    }

    /// <summary>
    ///     Builds the minimal byte sequence for a {biWidth}x{biHeight}-pixel BMP file with the
    ///     specified header field values, for use in malformed/unsupported-format failure tests.
    /// </summary>
    private static byte[] MakeMinimalBmpBytes(
        int biSize,
        int biBitCount,
        int biCompression,
        int biHeight,
        bool includePixelData = true,
        int biWidth = 1)
    {
        using var stream = new MemoryStream();

        // BITMAPFILEHEADER (14 bytes)
        stream.WriteByte((byte)'B');
        stream.WriteByte((byte)'M');
        WriteLe32(stream, 0); // bfSize (not validated by Load)
        WriteLe16(stream, 0); // bfReserved1
        WriteLe16(stream, 0); // bfReserved2
        WriteLe32(stream, 54); // bfOffBits

        // Info header (biSize bytes). Only BITMAPINFOHEADER-sized (40-byte) headers have room
        // for the width/height/bitcount/compression fields below offset 20; smaller headers
        // (for example the 12-byte BITMAPCOREHEADER) only ever get biSize written, which is
        // all Load needs to identify and reject them before reading any further fields.
        var infoHeader = new byte[biSize];
        WriteLe32(infoHeader, 0, biSize);
        if (biSize >= InfoHeaderFieldsSize)
        {
            WriteLe32(infoHeader, 4, biWidth); // biWidth
            WriteLe32(infoHeader, 8, biHeight);
            WriteLe16(infoHeader, 12, 1); // biPlanes
            WriteLe16(infoHeader, 14, (ushort)biBitCount);
            WriteLe32(infoHeader, 16, biCompression);
        }

        stream.Write(infoHeader, 0, infoHeader.Length);

        if (includePixelData)
        {
            // One padded row of zero pixel data, sized generously (4 bytes) to satisfy any
            // bit depth used by these tests
            stream.Write(new byte[4], 0, 4);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit integer directly to a stream.
    /// </summary>
    private static void WriteLe16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit integer into a byte buffer at the given offset.
    /// </summary>
    private static void WriteLe16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    ///     Writes a little-endian, signed 32-bit integer directly to a stream.
    /// </summary>
    private static void WriteLe32(Stream stream, int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }

    /// <summary>
    ///     Writes a little-endian, signed 32-bit integer into a byte buffer at the given offset.
    /// </summary>
    private static void WriteLe32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
