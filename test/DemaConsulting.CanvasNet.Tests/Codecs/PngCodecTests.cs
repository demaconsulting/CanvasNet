using System.IO.Compression;
using CanvasNet.Canvas;
using CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the PngCodec class and the PngColorType enum.
/// </summary>
/// <remarks>
///     Several tests build raw PNG byte streams by hand, using independent helper
///     implementations of CRC-32, Adler-32, zlib wrapping, and scanline filtering (rather than
///     calling into <see cref="PngCodec"/>'s own private algorithms), so that these tests verify
///     <see cref="PngCodec"/>'s behavior against the PNG/zlib specifications themselves, not just
///     against its own internal consistency.
/// </remarks>
public class PngCodecTests
{
    /// <summary>
    ///     The standard 8-byte PNG file signature, duplicated here so hand-built test PNG byte
    ///     streams do not depend on <see cref="PngCodec"/>'s own (private) copy.
    /// </summary>
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    ///     An independent CRC-32 lookup table, built with the same standard polynomial the PNG
    ///     specification requires, for use only by this test class's helper methods.
    /// </summary>
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    ///     The modulus used by the Adler-32 checksum algorithm.
    /// </summary>
    private const uint AdlerModulus = 65521;

    /// <summary>
    ///     Builds a small surface with distinct, non-trivial per-pixel RGBA values so that
    ///     channel-drop and reconstruction bugs cannot hide behind uniform pixel data.
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
    ///     Proves that saving at PngColorType.Rgb and loading back reproduces every pixel's RGB
    ///     values exactly, with alpha forced to 255 (opaque) regardless of the source alpha.
    /// </summary>
    [Fact]
    public void PngCodec_SaveThenLoad_Rgb_ReturnsExpectedPixelsWithOpaqueAlpha()
    {
        // Arrange: build a surface with varied, non-opaque pixel values
        var surface = BuildTestCanvas(5, 4);
        using var stream = new MemoryStream();

        // Act: save as RGB then load back
        PngCodec.Save(surface, stream, PngColorType.Rgb);
        stream.Position = 0;
        var loaded = PngCodec.Load(stream);

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
    ///     Proves that saving at PngColorType.Rgba (the default) and loading back reproduces
    ///     every pixel's RGBA values exactly, including alpha.
    /// </summary>
    [Fact]
    public void PngCodec_SaveThenLoad_Rgba_ReturnsExpectedPixelsIncludingAlpha()
    {
        // Arrange: build a surface with varied pixel values including non-opaque alpha
        var surface = BuildTestCanvas(5, 4);
        using var stream = new MemoryStream();

        // Act: save using the default color type then load back
        PngCodec.Save(surface, stream);
        stream.Position = 0;
        var loaded = PngCodec.Load(stream);

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
    ///     Proves that Save(Surface, string, PngColorType) and Load(string) round-trip a surface
    ///     through a real file on disk.
    /// </summary>
    [Fact]
    public void PngCodec_Load_FromFilePath_ReturnsExpectedPixels()
    {
        // Arrange: build a surface and a temporary file path
        var surface = BuildTestCanvas(3, 3);
        var path = Path.GetTempFileName();
        try
        {
            // Act: save to the file path then load from it
            PngCodec.Save(surface, path);
            var loaded = PngCodec.Load(path);

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
    ///     Proves that IDAT data split across several chunks is concatenated correctly before
    ///     decompression, using a hand-built PNG with the compressed payload deliberately split
    ///     into five separate IDAT chunks of a fixed, small byte size (unrelated to the
    ///     compressed data's natural size), regardless of how PngCodec's own Save happens to
    ///     chunk its output.
    /// </summary>
    [Fact]
    public void PngCodec_Load_MultipleIdatChunks_ReturnsExpectedPixels()
    {
        // Arrange: a small 2x2 RGB image with known raw pixel rows, filtered with type None,
        // hand-split into five deliberately tiny IDAT chunks
        var rawRows = new[]
        {
            new byte[] { 10, 20, 30, 40, 50, 60 },
            new byte[] { 70, 80, 90, 100, 110, 120 }
        };
        var filterTypes = new byte[] { 0, 0 };
        var bytes = BuildPng(2, 2, 3, (byte)PngColorType.Rgb, rawRows, filterTypes, idatChunkCount: 5);
        using var stream = new MemoryStream(bytes);

        // Act: load the hand-built, multi-chunk PNG
        var loaded = PngCodec.Load(stream);

        // Assert: every pixel matches the known raw rows exactly, with alpha forced to 255
        Assert.Equal(new Rgba32(10, 20, 30, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(40, 50, 60, 255), loaded[1, 0]);
        Assert.Equal(new Rgba32(70, 80, 90, 255), loaded[0, 1]);
        Assert.Equal(new Rgba32(100, 110, 120, 255), loaded[1, 1]);
    }

    /// <summary>
    ///     Proves that filter type 1 (Sub) scanlines are reconstructed correctly, using a
    ///     hand-built single-row PNG whose filtered bytes were computed independently of
    ///     PngCodec.
    /// </summary>
    [Fact]
    public void PngCodec_Load_FilterTypeSub_ReconstructsExpectedPixels()
    {
        // Arrange: one row, RGB, with the Sub filter applied
        var rawRows = new[] { new byte[] { 5, 10, 15, 25, 35, 45 } };
        var filterTypes = new byte[] { 1 };
        var bytes = BuildPng(2, 1, 3, (byte)PngColorType.Rgb, rawRows, filterTypes);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: the Sub filter is correctly reversed
        Assert.Equal(new Rgba32(5, 10, 15, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(25, 35, 45, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that filter type 2 (Up) scanlines are reconstructed correctly, using a
    ///     hand-built two-row PNG whose filtered bytes were computed independently of PngCodec.
    /// </summary>
    [Fact]
    public void PngCodec_Load_FilterTypeUp_ReconstructsExpectedPixels()
    {
        // Arrange: two rows, RGB; the second row uses the Up filter, referencing the first row
        var rawRows = new[]
        {
            new byte[] { 8, 16, 24 },
            new byte[] { 20, 10, 200 }
        };
        var filterTypes = new byte[] { 0, 2 };
        var bytes = BuildPng(1, 2, 3, (byte)PngColorType.Rgb, rawRows, filterTypes);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: the Up filter is correctly reversed
        Assert.Equal(new Rgba32(8, 16, 24, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(20, 10, 200, 255), loaded[0, 1]);
    }

    /// <summary>
    ///     Proves that filter type 3 (Average) scanlines are reconstructed correctly, using a
    ///     hand-built two-row, two-pixel-wide PNG whose filtered bytes were computed
    ///     independently of PngCodec.
    /// </summary>
    [Fact]
    public void PngCodec_Load_FilterTypeAverage_ReconstructsExpectedPixels()
    {
        // Arrange: two rows, RGB; the second row uses the Average filter
        var rawRows = new[]
        {
            new byte[] { 30, 60, 90, 40, 70, 100 },
            new byte[] { 50, 90, 130, 210, 5, 255 }
        };
        var filterTypes = new byte[] { 0, 3 };
        var bytes = BuildPng(2, 2, 3, (byte)PngColorType.Rgb, rawRows, filterTypes);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: the Average filter is correctly reversed
        Assert.Equal(new Rgba32(30, 60, 90, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(40, 70, 100, 255), loaded[1, 0]);
        Assert.Equal(new Rgba32(50, 90, 130, 255), loaded[0, 1]);
        Assert.Equal(new Rgba32(210, 5, 255, 255), loaded[1, 1]);
    }

    /// <summary>
    ///     Proves that filter type 4 (Paeth) scanlines are reconstructed correctly, using a
    ///     hand-built two-row, two-pixel-wide PNG whose filtered bytes were computed
    ///     independently of PngCodec.
    /// </summary>
    [Fact]
    public void PngCodec_Load_FilterTypePaeth_ReconstructsExpectedPixels()
    {
        // Arrange: two rows, RGBA; the second row uses the Paeth filter
        var rawRows = new[]
        {
            new byte[] { 12, 34, 56, 78, 90, 100, 110, 120 },
            new byte[] { 200, 150, 90, 30, 10, 250, 5, 60 }
        };
        var filterTypes = new byte[] { 0, 4 };
        var bytes = BuildPng(2, 2, 4, (byte)PngColorType.Rgba, rawRows, filterTypes);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: the Paeth filter is correctly reversed
        Assert.Equal(new Rgba32(12, 34, 56, 78), loaded[0, 0]);
        Assert.Equal(new Rgba32(90, 100, 110, 120), loaded[1, 0]);
        Assert.Equal(new Rgba32(200, 150, 90, 30), loaded[0, 1]);
        Assert.Equal(new Rgba32(10, 250, 5, 60), loaded[1, 1]);
    }

    /// <summary>
    ///     Proves that Load rejects a chunk whose CRC-32 does not match its type and data with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_CorruptChunkCrc_ThrowsInvalidDataException()
    {
        // Arrange: a valid, saved PNG with the last byte (part of IEND's CRC-32) corrupted
        var surface = BuildTestCanvas(2, 2);
        using var validStream = new MemoryStream();
        PngCodec.Save(surface, validStream);
        var bytes = validStream.ToArray();
        bytes[^1] ^= 0xFF;
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the corrupt CRC must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a zlib stream whose Adler-32 trailer does not match the
    ///     decompressed data, with InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_CorruptAdlerTrailer_ThrowsInvalidDataException()
    {
        // Arrange: build a valid PNG, then locate and corrupt the last byte of the zlib Adler-32
        // trailer (the final byte of the final IDAT chunk's data), recomputing that chunk's
        // CRC-32 so only the Adler-32 check is exercised
        var surface = BuildTestCanvas(6, 6);
        using var validStream = new MemoryStream();
        PngCodec.Save(surface, validStream);
        var bytes = CorruptFinalIdatAdlerByte(validStream.ToArray());
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the corrupt Adler-32 trailer must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream not starting with the PNG signature with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_BadSignature_ThrowsInvalidDataException()
    {
        // Arrange: 8 bytes that do not match the PNG signature
        var bytes = new byte[8];
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the bad signature must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream containing a valid signature and a structurally
    ///     valid <c>IEND</c> chunk (correct CRC-32) but no <c>IHDR</c> chunk, with
    ///     <see cref="InvalidDataException"/> naming <c>IHDR</c> as the missing chunk, rather
    ///     than proceeding to decode pixel data using default/uninitialized header fields.
    /// </summary>
    [Fact]
    public void PngCodec_Load_IendBeforeIhdr_ThrowsInvalidDataExceptionMentioningIhdr()
    {
        // Arrange: a valid signature followed directly by a well-formed IEND chunk, with no
        // IHDR chunk present anywhere in the stream
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var iend = BuildChunk("IEND", []);
        stream.Write(iend, 0, iend.Length);
        stream.Position = 0;

        // Act & Assert: the missing IHDR chunk must be rejected, with the exception message
        // identifying IHDR as the cause
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("IHDR", exception.Message);
    }

    /// <summary>
    ///     Proves that Load rejects a grayscale (color type 0) IHDR with InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnsupportedColorTypeGrayscale_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG declaring grayscale color type
        var bytes = BuildMinimalPngHeaderOnly(colorType: 0);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported color type must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a palette-based (color type 3) IHDR with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnsupportedColorTypePalette_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG declaring palette/indexed color type
        var bytes = BuildMinimalPngHeaderOnly(colorType: 3);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported color type must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects unsupported bit depths (1, 2, 4, and 16) with
    ///     InvalidDataException.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(16)]
    public void PngCodec_Load_UnsupportedBitDepth_ThrowsInvalidDataException(int bitDepth)
    {
        // Arrange: a minimal PNG declaring an unsupported bit depth
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, bitDepth: (byte)bitDepth);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported bit depth must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects an interlaced (Adam7, interlace method 1) IHDR with
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnsupportedInterlaceAdam7_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG declaring Adam7 interlacing
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, interlace: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported interlace method must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream that ends before all declared chunk data (and the
    ///     mandatory IEND chunk) has been read, with InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TruncatedStream_ThrowsInvalidDataException()
    {
        // Arrange: a valid signature and IHDR chunk, but nothing else (no IDAT/IEND)
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb);
        using var stream = new MemoryStream(bytes);
        // Force Load past IHDR parsing successfully, then run out of stream while looking for
        // the next chunk (there is none) - the same bytes as the color-type test above already
        // demonstrate this, since no IDAT/IEND chunk follows a bare IHDR
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Save rejects a null surface with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_Save_NullCanvas_ThrowsArgumentNullException()
    {
        // Arrange: a valid destination stream
        using var stream = new MemoryStream();

        // Act & Assert: a null surface must be rejected
        Assert.Throws<ArgumentNullException>(() => PngCodec.Save(null!, stream));
    }

    /// <summary>
    ///     Proves that Save rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_Save_NullStream_ThrowsArgumentNullException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: a null stream must be rejected
        Assert.Throws<ArgumentNullException>(() => PngCodec.Save(surface, (Stream)null!));
    }

    /// <summary>
    ///     Proves that Save rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_Save_NullPath_ThrowsArgumentNullException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: a null path must be rejected
        Assert.Throws<ArgumentNullException>(() => PngCodec.Save(surface, (string)null!));
    }

    /// <summary>
    ///     Proves that Save rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void PngCodec_Save_EmptyPath_ThrowsArgumentException()
    {
        // Arrange: a valid surface
        var surface = new Surface(1, 1);

        // Act & Assert: an empty path must be rejected
        Assert.Throws<ArgumentException>(() => PngCodec.Save(surface, string.Empty));
    }

    /// <summary>
    ///     Proves that Save rejects an undefined PngColorType value with
    ///     ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void PngCodec_Save_UndefinedColorType_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a valid surface, stream, and an undefined color type value
        var surface = new Surface(1, 1);
        using var stream = new MemoryStream();
        const PngColorType undefined = (PngColorType)99;

        // Act & Assert: the undefined color type must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => PngCodec.Save(surface, stream, undefined));
    }

    /// <summary>
    ///     Proves that Load rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_NullStream_ThrowsArgumentNullException()
    {
        // Act & Assert: a null stream must be rejected
        Assert.Throws<ArgumentNullException>(() => PngCodec.Load((Stream)null!));
    }

    /// <summary>
    ///     Proves that Load rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert: a null path must be rejected
        Assert.Throws<ArgumentNullException>(() => PngCodec.Load((string)null!));
    }

    /// <summary>
    ///     Proves that Load rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_EmptyPath_ThrowsArgumentException()
    {
        // Act & Assert: an empty path must be rejected
        Assert.Throws<ArgumentException>(() => PngCodec.Load(string.Empty));
    }

    /// <summary>
    ///     Corrupts the last byte of the final IDAT chunk's payload (which, for a valid PNG
    ///     produced by <see cref="PngCodec.Save(Surface, Stream, PngColorType)"/>, always falls
    ///     inside the zlib stream's trailing 4-byte Adler-32 checksum) and recomputes that
    ///     chunk's CRC-32 so only the Adler-32 check inside the zlib stream is exercised.
    /// </summary>
    private static byte[] CorruptFinalIdatAdlerByte(byte[] pngBytes)
    {
        var result = (byte[])pngBytes.Clone();
        var offset = Signature.Length;
        var lastIdatDataStart = -1;
        var lastIdatDataLength = 0;

        while (offset < result.Length)
        {
            var length = (int)ReadUInt32Be(result, offset);
            var typeStart = offset + 4;
            var dataStart = typeStart + 4;
            var isIdat = result[typeStart] == (byte)'I' && result[typeStart + 1] == (byte)'D' &&
                         result[typeStart + 2] == (byte)'A' && result[typeStart + 3] == (byte)'T';
            if (isIdat)
            {
                lastIdatDataStart = dataStart;
                lastIdatDataLength = length;
            }

            offset = dataStart + length + 4;
        }

        if (lastIdatDataStart < 0)
        {
            throw new InvalidOperationException("Test helper: no IDAT chunk found in the provided PNG bytes.");
        }

        // Flip the last byte of the last IDAT chunk's data (the last byte of the Adler-32
        // trailer) and recompute that chunk's CRC-32 so the CRC check still passes
        result[lastIdatDataStart + lastIdatDataLength - 1] ^= 0xFF;

        var typeOffset = lastIdatDataStart - 4;
        var crcInput = new byte[4 + lastIdatDataLength];
        Array.Copy(result, typeOffset, crcInput, 0, 4);
        Array.Copy(result, lastIdatDataStart, crcInput, 4, lastIdatDataLength);
        WriteUInt32Be(result, lastIdatDataStart + lastIdatDataLength, Crc32(crcInput));

        return result;
    }

    /// <summary>
    ///     Builds a minimal PNG byte sequence containing only the signature and a single IHDR
    ///     chunk, for use in IHDR-level failure tests where the codec is expected to throw
    ///     before any further chunk is needed.
    /// </summary>
    private static byte[] BuildMinimalPngHeaderOnly(
        byte colorType,
        byte bitDepth = 8,
        byte interlace = 0,
        int width = 1,
        int height = 1)
    {
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(width, height, bitDepth, colorType, 0, 0, interlace);
        stream.Write(ihdr, 0, ihdr.Length);
        return stream.ToArray();
    }

    /// <summary>
    ///     Builds a complete, valid PNG byte sequence from explicitly specified raw (unfiltered)
    ///     scanline rows and a chosen filter type per row, splitting the compressed IDAT payload
    ///     across a chosen number of chunks.
    /// </summary>
    private static byte[] BuildPng(
        int width,
        int height,
        int channels,
        byte colorType,
        byte[][] rawRows,
        byte[] filterTypes,
        int idatChunkCount = 1)
    {
        var rowBytes = width * channels;
        var raw = new byte[(rowBytes + 1) * height];
        var offset = 0;
        var previousRow = new byte[rowBytes];
        for (var y = 0; y < height; y++)
        {
            var filterType = filterTypes[y];
            raw[offset] = filterType;
            offset++;

            var rawRow = rawRows[y];
            for (var i = 0; i < rowBytes; i++)
            {
                int left = i >= channels ? rawRow[i - channels] : 0;
                int up = previousRow[i];
                int upperLeft = i >= channels ? previousRow[i - channels] : 0;
                var predicted = filterType switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upperLeft),
                    _ => throw new InvalidOperationException("Test helper: unsupported filter type.")
                };
                raw[offset + i] = (byte)(rawRow[i] - predicted);
            }

            offset += rowBytes;
            previousRow = rawRow;
        }

        var zlib = ZlibCompress(raw);

        using var result = new MemoryStream();
        result.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(width, height, 8, colorType, 0, 0, 0);
        result.Write(ihdr, 0, ihdr.Length);

        var chunkSize = Math.Max(1, (int)Math.Ceiling(zlib.Length / (double)idatChunkCount));
        var position = 0;
        while (position < zlib.Length)
        {
            var length = Math.Min(chunkSize, zlib.Length - position);
            var chunk = BuildChunk("IDAT", zlib.AsSpan(position, length).ToArray());
            result.Write(chunk, 0, chunk.Length);
            position += length;
        }

        var iend = BuildChunk("IEND", []);
        result.Write(iend, 0, iend.Length);
        return result.ToArray();
    }

    /// <summary>
    ///     Computes the PNG Paeth predictor for the given left, up, and upper-left byte values,
    ///     implemented independently of PngCodec's own copy for use only by this test class.
    /// </summary>
    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    /// <summary>
    ///     Builds a 13-byte IHDR chunk (with a correct CRC-32) from explicit field values.
    /// </summary>
    private static byte[] BuildIhdrChunk(
        int width,
        int height,
        byte bitDepth,
        byte colorType,
        byte compression,
        byte filter,
        byte interlace)
    {
        var data = new byte[13];
        WriteUInt32Be(data, 0, (uint)width);
        WriteUInt32Be(data, 4, (uint)height);
        data[8] = bitDepth;
        data[9] = colorType;
        data[10] = compression;
        data[11] = filter;
        data[12] = interlace;
        return BuildChunk("IHDR", data);
    }

    /// <summary>
    ///     Builds a complete PNG chunk (length, type, data, and CRC-32), using this test class's
    ///     own independent CRC-32 implementation.
    /// </summary>
    private static byte[] BuildChunk(string type, byte[] data)
    {
        var typeBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        using var stream = new MemoryStream();
        var lengthBytes = new byte[4];
        WriteUInt32Be(lengthBytes, 0, (uint)data.Length);
        stream.Write(lengthBytes, 0, lengthBytes.Length);
        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var crcBytes = new byte[4];
        WriteUInt32Be(crcBytes, 0, Crc32(crcInput));
        stream.Write(crcBytes, 0, crcBytes.Length);

        return stream.ToArray();
    }

    /// <summary>
    ///     Compresses raw bytes into a complete zlib stream (2-byte header, DEFLATE data, 4-byte
    ///     big-endian Adler-32 trailer), using this test class's own independent Adler-32
    ///     implementation.
    /// </summary>
    private static byte[] ZlibCompress(byte[] rawData)
    {
        byte[] deflateData;
        using (var output = new MemoryStream())
        {
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
            {
                deflate.Write(rawData, 0, rawData.Length);
            }

            deflateData = output.ToArray();
        }

        var result = new byte[2 + deflateData.Length + 4];
        result[0] = 0x78;
        result[1] = 0x9C;
        deflateData.CopyTo(result, 2);
        WriteUInt32Be(result, result.Length - 4, Adler32(rawData));
        return result;
    }

    /// <summary>
    ///     Builds the 256-entry CRC-32 lookup table using the standard IEEE 802.3 polynomial, for
    ///     use only by this test class's own independent <see cref="Crc32"/> implementation.
    /// </summary>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>
    ///     Computes the PNG/zlib CRC-32 checksum of a byte sequence, independently of PngCodec's
    ///     own (private) copy, for use only by this test class.
    /// </summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, independently of PngCodec's own
    ///     (private) copy, for use only by this test class.
    /// </summary>
    private static uint Adler32(byte[] data)
    {
        var a = 1u;
        var b = 0u;
        foreach (var value in data)
        {
            a = (a + value) % AdlerModulus;
            b = (b + a) % AdlerModulus;
        }

        return (b << 16) | a;
    }

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer from a byte buffer at the given offset.
    /// </summary>
    private static uint ReadUInt32Be(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8) |
        buffer[offset + 3];

    /// <summary>
    ///     Writes a big-endian, unsigned 32-bit integer into a byte buffer at the given offset.
    /// </summary>
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
