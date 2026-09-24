using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

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
    ///     chunk its output. This also proves the consecutive-IDAT-chunks requirement's
    ///     no-regression case: a run of consecutive IDAT chunks with no other chunk type
    ///     interleaved between them must still load successfully.
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
    ///     Proves that Load decodes an 8-bit grayscale (color type 0) image, replicating each
    ///     gray sample into R, G, and B, with alpha forced to 255 (no tRNS chunk present).
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale8Bit_ReturnsExpectedGrayPixels()
    {
        // Arrange: one row, two gray samples, filter type None
        var rawRows = new[] { new byte[] { 10, 200 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 1, 0, rawRows, filterTypes, rowBytes: 2, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(10, 10, 10, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(200, 200, 200, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes an 8-bit grayscale-with-alpha (color type 4) image, retaining
    ///     each pixel's stored alpha value.
    /// </summary>
    [Fact]
    public void PngCodec_Load_GrayscaleAlpha8Bit_ReturnsExpectedPixels()
    {
        // Arrange: one row, two (gray, alpha) sample pairs, filter type None
        var rawRows = new[] { new byte[] { 50, 128, 90, 255 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 2, 4, rawRows, filterTypes, rowBytes: 4, bpp: 2);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(50, 50, 50, 128), loaded[0, 0]);
        Assert.Equal(new Rgba32(90, 90, 90, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes an 8-bit palette (color type 3) image, resolving each
    ///     palette-index sample through the PLTE chunk with alpha forced to 255 (no tRNS chunk
    ///     present).
    /// </summary>
    [Fact]
    public void PngCodec_Load_Palette8Bit_ResolvesIndicesThroughPlte()
    {
        // Arrange: a 3-entry palette and a row of three distinct indices
        var plte = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        var rawRows = new[] { new byte[] { 0, 1, 2 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(3, 1, 1, 3, rawRows, filterTypes, rowBytes: 3, bpp: 1, plteData: plte);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(10, 20, 30, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(40, 50, 60, 255), loaded[1, 0]);
        Assert.Equal(new Rgba32(70, 80, 90, 255), loaded[2, 0]);
    }

    /// <summary>
    ///     Proves that Load honors a palette (color type 3) image's tRNS chunk, applying each
    ///     entry's per-palette-index alpha byte, and defaulting to fully opaque for any palette
    ///     entry the tRNS chunk does not cover.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PaletteWithTrns_AppliesPerIndexAlpha()
    {
        // Arrange: a 3-entry palette; tRNS covers only the first two entries
        var plte = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        var trns = new byte[] { 255, 0 }; // index 0 -> opaque, index 1 -> fully transparent
        var rawRows = new[] { new byte[] { 0, 1, 2 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(3, 1, 1, 3, rawRows, filterTypes, rowBytes: 3, bpp: 1, plteData: plte, trnsData: trns);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: index 0 opaque, index 1 transparent, index 2 (uncovered by tRNS) defaults opaque
        Assert.Equal(new Rgba32(10, 20, 30, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(40, 50, 60, 0), loaded[1, 0]);
        Assert.Equal(new Rgba32(70, 80, 90, 255), loaded[2, 0]);
    }

    /// <summary>
    ///     Proves that Load rejects a palette (color type 3) image with no PLTE chunk, with
    ///     InvalidDataException naming PLTE as the cause.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PaletteWithoutPlte_ThrowsInvalidDataExceptionMentioningPlte()
    {
        // Arrange: a palette-color-type image with no PLTE chunk at all
        var rawRows = new[] { new byte[] { 0 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(1, 1, 1, 3, rawRows, filterTypes, rowBytes: 1, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("PLTE", exception.Message);
    }

    /// <summary>
    ///     Proves that Load rejects a palette-index sample outside the PLTE chunk's entry count
    ///     with InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PaletteIndexOutOfRange_ThrowsInvalidDataException()
    {
        // Arrange: a 2-entry palette but a row containing index 5
        var plte = new byte[] { 1, 2, 3, 4, 5, 6 };
        var rawRows = new[] { new byte[] { 5 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(1, 1, 1, 3, rawRows, filterTypes, rowBytes: 1, bpp: 1, plteData: plte);
        using var stream = new MemoryStream(bytes);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream containing two PLTE chunks with InvalidDataException,
    ///     since the PNG specification permits at most one PLTE chunk per file.
    /// </summary>
    [Fact]
    public void PngCodec_Load_DuplicatePlte_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by two PLTE chunks
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var plte1 = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte1, 0, plte1.Length);
        var plte2 = BuildChunk("PLTE", [4, 5, 6]);
        stream.Write(plte2, 0, plte2.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a stream containing two tRNS chunks with InvalidDataException,
    ///     since the PNG specification permits at most one tRNS chunk per file.
    /// </summary>
    [Fact]
    public void PngCodec_Load_DuplicateTrns_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by two tRNS chunks
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var trns1 = BuildChunk("tRNS", [1, 2, 3]);
        stream.Write(trns1, 0, trns1.Length);
        var trns2 = BuildChunk("tRNS", [4, 5, 6]);
        stream.Write(trns2, 0, trns2.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a PLTE chunk that appears after the first IDAT chunk with
    ///     InvalidDataException, since the PNG specification requires PLTE (when present) to
    ///     precede the first IDAT chunk.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteAfterIdat_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR, an IDAT chunk, and then a PLTE chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var idat = BuildChunk("IDAT", []);
        stream.Write(idat, 0, idat.Length);
        var plte = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a tRNS chunk that appears after the first IDAT chunk with
    ///     InvalidDataException, since the PNG specification requires tRNS (when present) to
    ///     precede the first IDAT chunk.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TrnsAfterIdat_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR, an IDAT chunk, and then a tRNS chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var idat = BuildChunk("IDAT", []);
        stream.Write(idat, 0, idat.Length);
        var trns = BuildChunk("tRNS", [1, 2, 3]);
        stream.Write(trns, 0, trns.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a PLTE chunk declaring more than 256 palette entries with
    ///     InvalidDataException, since the PNG specification permits at most 256 PLTE entries
    ///     regardless of color type.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteExceeds256Entries_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by a PLTE chunk declaring 257 entries (771 bytes)
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var plte = BuildChunk("PLTE", new byte[257 * 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a palette (color type 3) image whose PLTE chunk declares more
    ///     palette entries than the image's bit depth can index (2^bitDepth), with
    ///     InvalidDataException - for example a 2-bit-depth palette image can address at most 4
    ///     distinct entries.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteEntryCountExceedsBitDepthCapacity_ThrowsInvalidDataException()
    {
        // Arrange: a 2-bit-depth palette image whose PLTE chunk declares 5 entries (only 4 are
        // addressable by a 2-bit index)
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 2, 3 /* Palette */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var plte = BuildChunk("PLTE", new byte[5 * 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects an unrecognized <em>critical</em> chunk (uppercase first type
    ///     byte, not one of IHDR/PLTE/tRNS/IDAT/IEND) with InvalidDataException, since it may
    ///     change how pixel data must be interpreted and this codec has no logic for it.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnrecognizedCriticalChunk_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by a hypothetical unrecognized critical chunk "ABCD"
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var unknownCritical = BuildChunk("ABCD", [1, 2, 3]);
        stream.Write(unknownCritical, 0, unknownCritical.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("ABCD", exception.Message);
    }

    /// <summary>
    ///     Regression test for a code-review finding: chunk-type classification (critical vs.
    ///     ancillary, see <see cref="PngCodec_Load_UnrecognizedCriticalChunk_ThrowsInvalidDataException"/>
    ///     and <see cref="PngCodec_Load_UnrecognizedAncillaryChunk_StillLoadsSuccessfully"/>) used
    ///     to trust the chunk type's first byte alone, without validating that all four bytes are
    ///     ASCII letters. Proves that Load rejects a chunk type containing a non-letter byte (for
    ///     example <c>"a!cd"</c>) with InvalidDataException, before that malformed type ever
    ///     reaches the classification logic.
    /// </summary>
    [Fact]
    public void PngCodec_Load_ChunkTypeWithNonLetterByte_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by a chunk whose type's second byte ('!') is not an
        // ASCII letter at all
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var malformed = BuildChunk("a!cd", [1, 2, 3]);
        stream.Write(malformed, 0, malformed.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("ASCII letter", exception.Message);
    }

    /// <summary>
    ///     Regression test for a code-review finding: a chunk type whose reserved (third) byte is
    ///     lowercase - for example <c>"abcd"</c> - is not spec-valid per the PNG specification's
    ///     reserved-bit rule (the third byte must always be uppercase), yet the prior
    ///     classification logic would have accepted it as an ordinary ancillary chunk based on the
    ///     first byte's case alone. Proves that Load rejects such a chunk type with
    ///     InvalidDataException instead of silently treating it as ancillary.
    /// </summary>
    [Fact]
    public void PngCodec_Load_ChunkTypeWithLowercaseReservedByte_ThrowsInvalidDataException()
    {
        // Arrange: a valid IHDR followed by an all-lowercase chunk type ("abcd"); its first byte
        // being lowercase would otherwise mark it ancillary, but its lowercase third byte ('c')
        // violates the reserved-bit rule
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var malformed = BuildChunk("abcd", [1, 2, 3]);
        stream.Write(malformed, 0, malformed.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("reserved-bit rule", exception.Message);
    }

    /// <summary>
    ///     Proves that Load still decodes successfully when a stream contains an unrecognized
    ///     <em>ancillary</em> chunk (lowercase first type byte, for example a hypothetical "abCd"
    ///     chunk - third byte uppercase, per the PNG specification's reserved-bit rule), since an
    ///     unrecognized ancillary chunk carries no information required to decode pixels correctly
    ///     and remains safe to skip.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnrecognizedAncillaryChunk_StillLoadsSuccessfully()
    {
        // Arrange: a valid IHDR followed by a hypothetical unrecognized ancillary chunk "abCd"
        // (third byte uppercase, satisfying the reserved-bit rule), then the usual IDAT/IEND
        // chunks for a single opaque Truecolor pixel
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, (byte)PngColorType.Rgb, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var unknownAncillary = BuildChunk("abCd", [9, 9, 9]);
        stream.Write(unknownAncillary, 0, unknownAncillary.Length);

        var raw = new byte[] { 0, 10, 20, 30 }; // filter type 0 (None) + one RGB pixel
        var zlib = ZlibCompress(raw);
        var idat = BuildChunk("IDAT", zlib);
        stream.Write(idat, 0, idat.Length);
        var iend = BuildChunk("IEND", []);
        stream.Write(iend, 0, iend.Length);
        stream.Position = 0;

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: the unrecognized ancillary chunk did not affect decoding
        Assert.Equal(new Rgba32(10, 20, 30, 255), loaded[0, 0]);
    }

    /// <summary>
    ///     Proves that Load rejects a PLTE chunk on a grayscale (color type 0) image with
    ///     InvalidDataException, since grayscale samples are never resolved through a palette.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteOnGrayscale_ThrowsInvalidDataException()
    {
        // Arrange: a grayscale IHDR followed by a PLTE chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 0 /* Grayscale */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var plte = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a PLTE chunk on a grayscale-with-alpha (color type 4) image
    ///     with InvalidDataException, since grayscale samples are never resolved through a
    ///     palette.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteOnGrayscaleAlpha_ThrowsInvalidDataException()
    {
        // Arrange: a grayscale-with-alpha IHDR followed by a PLTE chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 4 /* Grayscale+alpha */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var plte = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a tRNS chunk appearing before the PLTE chunk on an
    ///     indexed-color (color type 3) image with InvalidDataException, since the PNG
    ///     specification requires tRNS to follow PLTE for indexed-color images (its
    ///     per-palette-entry alpha values are meaningless before the palette they index into has
    ///     been read).
    /// </summary>
    [Fact]
    public void PngCodec_Load_TrnsBeforePlteForIndexedColor_ThrowsInvalidDataException()
    {
        // Arrange: a palette IHDR, then tRNS before PLTE (the specification requires the opposite
        // order)
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 3 /* Palette */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var trns = BuildChunk("tRNS", [255]);
        stream.Write(trns, 0, trns.Length);
        var plte = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("PLTE", exception.Message);
    }

    /// <summary>
    ///     Proves that Load rejects a PLTE chunk that appears after a tRNS chunk has already been
    ///     accepted, with InvalidDataException naming tRNS as the cause. Truecolor (color type 2)
    ///     is used here since it permits both an optional suggested PLTE and a key-color tRNS
    ///     chunk, so this exercises the PLTE-after-tRNS ordering check independently of the
    ///     already-covered indexed-color (Palette) tRNS-before-PLTE check.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteAfterTrns_ThrowsInvalidDataException()
    {
        // Arrange: a Truecolor IHDR, then tRNS followed by PLTE - the specification requires
        // PLTE to precede tRNS whenever both chunks are present
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var trns = BuildChunk("tRNS", [0, 10, 0, 20, 0, 30]);
        stream.Write(trns, 0, trns.Length);
        var plte = BuildChunk("PLTE", [1, 2, 3]);
        stream.Write(plte, 0, plte.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("tRNS", exception.Message);
    }

    /// <summary>
    ///     Proves that Load rejects a stream in which an ancillary chunk (tEXt) appears between
    ///     two IDAT chunks, with InvalidDataException, since the PNG specification requires every
    ///     IDAT chunk to be consecutive - no other chunk type may appear between the first and
    ///     last IDAT chunk.
    /// </summary>
    [Fact]
    public void PngCodec_Load_NonConsecutiveIdatChunks_ThrowsInvalidDataException()
    {
        // Arrange: two IDAT chunks with an unrelated ancillary chunk between them, followed by a
        // well-formed terminating IEND chunk so the only defect in this stream is the
        // non-consecutive IDAT run itself (otherwise a truncated-stream EOF exception could mask
        // the intended check and let this test pass for the wrong reason)
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var idat1 = BuildChunk("IDAT", [1, 2, 3]);
        stream.Write(idat1, 0, idat1.Length);
        var textChunk = BuildChunk("tEXt", [9, 9, 9]);
        stream.Write(textChunk, 0, textChunk.Length);
        var idat2 = BuildChunk("IDAT", [4, 5, 6]);
        stream.Write(idat2, 0, idat2.Length);
        var iend = BuildChunk("IEND", []);
        stream.Write(iend, 0, iend.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("consecutive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that Load rejects a stream containing an ancillary chunk (tEXt) before the
    ///     mandatory IHDR chunk, with InvalidDataException naming IHDR as the cause, since the
    ///     PNG specification always requires IHDR to be the first chunk - even a chunk this codec
    ///     would otherwise silently skip must still be rejected in this position.
    /// </summary>
    [Fact]
    public void PngCodec_Load_AncillaryChunkBeforeIhdr_ThrowsInvalidDataException()
    {
        // Arrange: a valid signature followed directly by an ancillary chunk, with no IHDR chunk
        // present anywhere before it
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var textChunk = BuildChunk("tEXt", [9, 9, 9]);
        stream.Write(textChunk, 0, textChunk.Length);
        stream.Position = 0;

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        Assert.Contains("IHDR", exception.Message);
    }

    /// <summary>
    ///     Proves that Load rejects a tRNS chunk on a grayscale-with-alpha (color type 4) image
    ///     with InvalidDataException, since that color type already carries a full per-pixel
    ///     alpha channel, leaving nothing for a single-key-color transparency chunk to add.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TrnsOnGrayscaleAlpha_ThrowsInvalidDataException()
    {
        // Arrange: a grayscale-with-alpha IHDR followed by a tRNS chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 4 /* Grayscale+alpha */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var trns = BuildChunk("tRNS", [0, 100]);
        stream.Write(trns, 0, trns.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a tRNS chunk on a Truecolor-with-alpha (color type 6) image
    ///     with InvalidDataException, since that color type already carries a full per-pixel
    ///     alpha channel, leaving nothing for a single-key-color transparency chunk to add.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TrnsOnTruecolorAlpha_ThrowsInvalidDataException()
    {
        // Arrange: a Truecolor-with-alpha IHDR followed by a tRNS chunk
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 6 /* Truecolor+alpha */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var trns = BuildChunk("tRNS", [0, 10, 0, 20, 0, 30]);
        stream.Write(trns, 0, trns.Length);
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load honors a grayscale (color type 0) image's tRNS chunk, marking exactly
    ///     the pixels whose gray sample matches the tRNS value as fully transparent.
    /// </summary>
    [Fact]
    public void PngCodec_Load_GrayscaleWithTrns_MarksExactMatchTransparent()
    {
        // Arrange: two gray samples, one matching the tRNS value exactly, one not
        var trns = new byte[] { 0, 100 }; // gray value 100 is the transparent key color
        var rawRows = new[] { new byte[] { 100, 150 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 1, 0, rawRows, filterTypes, rowBytes: 2, bpp: 1, trnsData: trns);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(100, 100, 100, 0), loaded[0, 0]);
        Assert.Equal(new Rgba32(150, 150, 150, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load honors a Truecolor (color type 2) image's tRNS chunk, marking exactly
    ///     the pixel whose RGB triple matches the tRNS value as fully transparent.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TruecolorWithTrns_MarksExactMatchTransparent()
    {
        // Arrange: two RGB triples, one matching the tRNS value exactly, one not
        var trns = new byte[] { 0, 10, 0, 20, 0, 30 }; // (10, 20, 30) is the transparent key color
        var rawRows = new[] { new byte[] { 10, 20, 30, 11, 20, 30 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 3, (byte)PngColorType.Rgb, rawRows, filterTypes, trnsData: trns);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(10, 20, 30, 0), loaded[0, 0]);
        Assert.Equal(new Rgba32(11, 20, 30, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes a 1-bit grayscale image, scaling each 1-bit sample to the full
    ///     0-255 range and unpacking bits MSB-first, including across a non-byte-aligned final
    ///     partial byte whose padding bits must not be misread as an extra sample.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale1BitDepth_ScalesSamplesAndUnpacksMsbFirst()
    {
        // Arrange: width 9 is not a multiple of 8, so the packed row is 2 bytes: the first byte
        // holds 8 one-bit samples [0,1,0,1,1,0,1,0] (0x5A), and the second byte holds only 1 real
        // sample (the 9th, value 1, in its MSB) followed by 7 padding bits deliberately set to 0
        // so a bug that mis-locates the real bit (e.g. reads the LSB instead of the MSB) would be
        // caught by the final assertion below
        var rawRows = new[] { new byte[] { 0x5A, 0x80 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(9, 1, 1, 0, rawRows, filterTypes, bitDepth: 1, rowBytes: 2, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: 0 scales to 0, 1 scales to 255, including the 9th (non-byte-aligned) sample
        var expectedGray = new byte[] { 0, 255, 0, 255, 255, 0, 255, 0, 255 };
        for (var x = 0; x < 9; x++)
        {
            var g = expectedGray[x];
            Assert.Equal(new Rgba32(g, g, g, 255), loaded[x, 0]);
        }
    }

    /// <summary>
    ///     Proves that Load decodes a 2-bit grayscale image, scaling each 2-bit sample (0-3) to
    ///     the full 0-255 range.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale2BitDepth_ScalesSamples()
    {
        // Arrange: four 2-bit samples [0,1,2,3] packed MSB-first into a single byte 0x1B
        var rawRows = new[] { new byte[] { 0x1B } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(4, 1, 1, 0, rawRows, filterTypes, bitDepth: 2, rowBytes: 1, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: sample * 255 / 3
        var expectedGray = new byte[] { 0, 85, 170, 255 };
        for (var x = 0; x < 4; x++)
        {
            var g = expectedGray[x];
            Assert.Equal(new Rgba32(g, g, g, 255), loaded[x, 0]);
        }
    }

    /// <summary>
    ///     Proves that Load decodes a 2-bit grayscale image whose width is not a multiple of 4,
    ///     correctly unpacking the final partial byte's single real sample and ignoring its
    ///     padding bits.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale2BitDepthNonByteAlignedWidth_HandlesFinalPartialByte()
    {
        // Arrange: width 5 is not a multiple of 4, so the packed row is 2 bytes: the first byte
        // holds 4 two-bit samples [0,1,2,3] (0x1B), and the second byte holds only 1 real sample
        // (the 5th, value 1, in its top 2 bits) followed by 6 padding bits deliberately set to 1
        // so a bug that reads beyond the real sample would be caught by the final assertion below
        var rawRows = new[] { new byte[] { 0x1B, 0x7F } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(5, 1, 1, 0, rawRows, filterTypes, bitDepth: 2, rowBytes: 2, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: sample * 255 / 3, including the 5th (non-byte-aligned) sample
        var expectedGray = new byte[] { 0, 85, 170, 255, 85 };
        for (var x = 0; x < 5; x++)
        {
            var g = expectedGray[x];
            Assert.Equal(new Rgba32(g, g, g, 255), loaded[x, 0]);
        }
    }

    /// <summary>
    ///     Proves that Load decodes a 4-bit grayscale image, scaling each 4-bit sample (0-15) to
    ///     the full 0-255 range.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale4BitDepth_ScalesSamples()
    {
        // Arrange: two 4-bit samples [5,10] packed MSB-first into a single byte 0x5A
        var rawRows = new[] { new byte[] { 0x5A } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 1, 0, rawRows, filterTypes, bitDepth: 4, rowBytes: 1, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: sample * 255 / 15
        Assert.Equal(new Rgba32(85, 85, 85, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(170, 170, 170, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes a 4-bit grayscale image whose width is not a multiple of 2,
    ///     correctly unpacking the final partial byte's single real sample and ignoring its
    ///     padding bits.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale4BitDepthNonByteAlignedWidth_HandlesFinalPartialByte()
    {
        // Arrange: width 3 is not a multiple of 2, so the packed row is 2 bytes: the first byte
        // holds 2 four-bit samples [5,10] (0x5A), and the second byte holds only 1 real sample
        // (the 3rd, value 7, in its top nibble) followed by 4 padding bits deliberately set to 1
        // so a bug that reads beyond the real sample would be caught by the final assertion below
        var rawRows = new[] { new byte[] { 0x5A, 0x7F } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(3, 1, 1, 0, rawRows, filterTypes, bitDepth: 4, rowBytes: 2, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: sample * 255 / 15, including the 3rd (non-byte-aligned) sample
        Assert.Equal(new Rgba32(85, 85, 85, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(170, 170, 170, 255), loaded[1, 0]);
        Assert.Equal(new Rgba32(119, 119, 119, 255), loaded[2, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes a 2-bit palette image, unpacking each 2-bit palette-index
    ///     sample (never scaled, unlike grayscale) and resolving it through the PLTE chunk.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Palette2BitDepth_UnpacksIndicesWithoutScaling()
    {
        // Arrange: a 4-entry palette and four 2-bit indices [0,1,2,3] packed into byte 0x1B
        var plte = new byte[] { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4 };
        var rawRows = new[] { new byte[] { 0x1B } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(4, 1, 1, 3, rawRows, filterTypes, bitDepth: 2, rowBytes: 1, bpp: 1, plteData: plte);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(1, 1, 1, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(2, 2, 2, 255), loaded[1, 0]);
        Assert.Equal(new Rgba32(3, 3, 3, 255), loaded[2, 0]);
        Assert.Equal(new Rgba32(4, 4, 4, 255), loaded[3, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes a 16-bit grayscale image, discarding the low byte of each
    ///     big-endian 16-bit sample.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale16BitDepth_DiscardsLowByte()
    {
        // Arrange: two 16-bit big-endian samples, 0x1234 and 0xABCD
        var rawRows = new[] { new byte[] { 0x12, 0x34, 0xAB, 0xCD } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 2, 0, rawRows, filterTypes, bitDepth: 16, rowBytes: 4, bpp: 2);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: only the high byte of each sample survives
        Assert.Equal(new Rgba32(0x12, 0x12, 0x12, 255), loaded[0, 0]);
        Assert.Equal(new Rgba32(0xAB, 0xAB, 0xAB, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that Load decodes a 16-bit Truecolor-with-alpha image, discarding the low byte
    ///     of every channel's big-endian 16-bit sample.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TruecolorAlpha16BitDepth_DiscardsLowByteOfEveryChannel()
    {
        // Arrange: one pixel, R=0x0102, G=0x0304, B=0x0506, A=0x0708
        var rawRows = new[] { new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(1, 1, 4, (byte)PngColorType.Rgba, rawRows, filterTypes, bitDepth: 16, rowBytes: 8, bpp: 8);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert
        Assert.Equal(new Rgba32(0x01, 0x03, 0x05, 0x07), loaded[0, 0]);
    }

    /// <summary>
    ///     Regression test for the tRNS-before-downshift ordering requirement: proves that a
    ///     16-bit grayscale tRNS comparison uses the full 16-bit raw sample, not the downshifted
    ///     8-bit value. Two pixels share the same downshifted (high) byte but differ in their raw
    ///     16-bit value; only the one that matches the tRNS chunk's raw 16-bit value exactly is
    ///     marked transparent. An implementation that incorrectly downshifted before comparing
    ///     would mark both pixels transparent, since their downshifted values are identical.
    /// </summary>
    [Fact]
    public void PngCodec_Load_Grayscale16BitTrns_ComparesRawSampleBeforeDownshift()
    {
        // Arrange: pixel 0 raw = 0x0100 (256, matches tRNS exactly); pixel 1 raw = 0x01FF (511,
        // downshifts to the same high byte as pixel 0 but does not match tRNS exactly)
        var trns = new byte[] { 0x01, 0x00 };
        var rawRows = new[] { new byte[] { 0x01, 0x00, 0x01, 0xFF } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 2, 0, rawRows, filterTypes, bitDepth: 16, rowBytes: 4, bpp: 2, trnsData: trns);
        using var stream = new MemoryStream(bytes);

        // Act
        var loaded = PngCodec.Load(stream);

        // Assert: both downshift to gray byte 0x01, but only the exact raw match is transparent
        Assert.Equal(new Rgba32(0x01, 0x01, 0x01, 0), loaded[0, 0]);
        Assert.Equal(new Rgba32(0x01, 0x01, 0x01, 255), loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, 1 channel, and no alpha for a
    ///     grayscale (color type 0) PNG.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_Grayscale_ReturnsExpectedInfoWithoutAlpha()
    {
        // Arrange
        var rawRows = new[] { new byte[] { 10, 200 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(2, 1, 1, 0, rawRows, filterTypes, rowBytes: 2, bpp: 1);
        using var stream = new MemoryStream(bytes);

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(2, 1, 1, false), info);
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, 2 channels, and alpha for a
    ///     grayscale-with-alpha (color type 4) PNG.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_GrayscaleAlpha_ReturnsExpectedInfoWithAlpha()
    {
        // Arrange
        var rawRows = new[] { new byte[] { 50, 128 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(1, 1, 2, 4, rawRows, filterTypes, rowBytes: 2, bpp: 2);
        using var stream = new MemoryStream(bytes);

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(1, 1, 2, true), info);
    }

    /// <summary>
    ///     Proves that GetInfo reports a palette (color type 3) PNG's raw file encoding - 1
    ///     channel, no alpha - rather than the 4-channel RGBA result Load would produce after
    ///     resolving indices through PLTE/tRNS; this is the documented design decision, not an
    ///     oversight.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_Palette_ReturnsRawFileEncodingNotDecodedRgba()
    {
        // Arrange: a palette PNG that Load would decode to opaque RGBA pixels
        var plte = new byte[] { 10, 20, 30 };
        var rawRows = new[] { new byte[] { 0 } };
        var filterTypes = new byte[] { 0 };
        var bytes = BuildPng(1, 1, 1, 3, rawRows, filterTypes, rowBytes: 1, bpp: 1, plteData: plte);
        using var stream = new MemoryStream(bytes);

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert: 1 channel, no alpha - the raw file encoding, not Load's 4-channel RGBA result
        Assert.Equal(new ImageInfo(1, 1, 1, false), info);
    }

    /// <summary>
    ///     Proves that GetInfo succeeds and reports the correct dimensions for every sub-byte bit
    ///     depth (1, 2, 4) a well-formed grayscale IHDR may declare, without needing a full PLTE/
    ///     IDAT/IEND stream.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void PngCodec_GetInfo_SubByteGrayscaleBitDepth_ReturnsCorrectDimensions(int bitDepth)
    {
        // Arrange
        var bytes = BuildMinimalPngHeaderOnly(colorType: 0, bitDepth: (byte)bitDepth, width: 9, height: 3);
        using var stream = new MemoryStream(bytes);

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(9, info.Width);
        Assert.Equal(3, info.Height);
    }

    /// <summary>
    ///     Proves that GetInfo succeeds and reports the correct dimensions for a 16-bit-depth
    ///     Truecolor-with-alpha IHDR (a combination Load fully supports, but exercised here via
    ///     the header-only path).
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_BitDepth16_ReturnsCorrectDimensions()
    {
        // Arrange
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgba, bitDepth: 16, width: 6, height: 4);
        using var stream = new MemoryStream(bytes);

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(6, info.Width);
        Assert.Equal(4, info.Height);
    }

    /// <summary>
    ///     Proves that both GetInfo and Load reject an out-of-range PNG bit depth (values other
    ///     than 1, 2, 4, 8, or 16) with InvalidDataException - a well-formedness defect, not a
    ///     decode-capability limitation.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(99)]
    public void PngCodec_GetInfoAndLoad_InvalidBitDepth_BothThrowInvalidDataException(int bitDepth)
    {
        // Arrange
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, bitDepth: (byte)bitDepth);

        // Act & Assert
        using var infoStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(infoStream));
        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(loadStream));
    }

    /// <summary>
    ///     Proves that both GetInfo and Load reject an out-of-range PNG color type (values other
    ///     than 0, 2, 3, 4, or 6) with InvalidDataException - a well-formedness defect, not a
    ///     decode-capability limitation.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void PngCodec_GetInfoAndLoad_InvalidColorType_BothThrowInvalidDataException(int colorType)
    {
        // Arrange
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)colorType);

        // Act & Assert
        using var infoStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(infoStream));
        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(loadStream));
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
    ///     Proves that Load rejects a bit-depth/color-type combination that is itself invalid per
    ///     the PNG specification (not merely unimplemented by this codec), with
    ///     InvalidDataException. No PngSuite fixture exercises these combinations, since they are
    ///     not legal PNG files at all.
    /// </summary>
    [Theory]
    [InlineData((byte)PngColorType.Rgb, (byte)1)] // Truecolor requires 8 or 16 bits
    [InlineData((byte)PngColorType.Rgb, (byte)2)]
    [InlineData((byte)PngColorType.Rgb, (byte)4)]
    [InlineData((byte)3, (byte)16)] // Palette never permits 16-bit depth
    [InlineData((byte)4, (byte)1)] // Grayscale-with-alpha requires 8 or 16 bits
    [InlineData((byte)4, (byte)2)]
    [InlineData((byte)4, (byte)4)]
    [InlineData((byte)PngColorType.Rgba, (byte)1)] // Truecolor-with-alpha requires 8 or 16 bits
    [InlineData((byte)PngColorType.Rgba, (byte)2)]
    [InlineData((byte)PngColorType.Rgba, (byte)4)]
    public void PngCodec_Load_InvalidBitDepthColorTypeCombination_ThrowsInvalidDataException(
        byte colorType,
        byte bitDepth)
    {
        // Arrange: a minimal PNG declaring an IHDR combination the PNG specification itself
        // never permits
        var bytes = BuildMinimalPngHeaderOnly(colorType: colorType, bitDepth: bitDepth);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the invalid combination must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that GetInfo also rejects a bit-depth/color-type combination that is invalid
    ///     per the PNG specification - unlike Adam7 interlacing, this is a well-formedness defect,
    ///     not merely a decode-capability limitation, so GetInfo must reject it exactly as Load
    ///     does.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_PaletteBitDepth16_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG declaring palette color type at the never-legal 16-bit depth
        var bytes = BuildMinimalPngHeaderOnly(colorType: 3, bitDepth: 16);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the invalid combination must be rejected by GetInfo too
        Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(stream));
    }

    /// <summary>
    ///     Proves that Load rejects an interlaced (Adam7, interlace method 1) IHDR with
    ///     InvalidDataException, since Adam7 decoding is not implemented, while GetInfo on the
    ///     same bytes still succeeds and reports the correct declared dimensions.
    /// </summary>
    [Fact]
    public void PngCodec_Load_UnsupportedInterlaceAdam7_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG declaring Adam7 interlacing
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, interlace: 1, width: 5, height: 7);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the unsupported interlace method must be rejected by Load
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));

        // Assert: GetInfo on the same bytes still succeeds with the correct dimensions
        using var infoStream = new MemoryStream(bytes);
        var info = PngCodec.GetInfo(infoStream);
        Assert.Equal(5, info.Width);
        Assert.Equal(7, info.Height);
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
    ///     Proves that Load rejects a width exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor, and before the width*channels row-byte-width arithmetic
    ///     performed later in Load is ever reached.
    /// </summary>
    [Fact]
    public void PngCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG IHDR declaring a width one above Surface.MaxDimension
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, width: Surface.MaxDimension + 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the oversized width must be rejected as malformed data, not as an
        // out-of-range constructor argument
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
    }

    /// <summary>
    ///     Proves that Load rejects a height exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor.
    /// </summary>
    [Fact]
    public void PngCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException()
    {
        // Arrange: a minimal PNG IHDR declaring a height one above Surface.MaxDimension
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, height: Surface.MaxDimension + 1);
        using var stream = new MemoryStream(bytes);

        // Act & Assert: the oversized height must be rejected as malformed data, not as an
        // out-of-range constructor argument
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
    ///     Proves that GetInfo reports the correct dimensions, channel count, and no-alpha flag
    ///     for an RGB PNG, without needing to Load (decode) the pixel data.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_Rgb_ReturnsExpectedInfoWithoutAlpha()
    {
        // Arrange
        using var stream = new MemoryStream();
        PngCodec.Save(BuildTestCanvas(3, 2), stream, PngColorType.Rgb);
        stream.Position = 0;

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(3, 2, 3, false), info);
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, channel count, and alpha flag for
    ///     an RGBA PNG, without needing to Load (decode) the pixel data.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_Rgba_ReturnsExpectedInfoWithAlpha()
    {
        // Arrange
        using var stream = new MemoryStream();
        PngCodec.Save(BuildTestCanvas(3, 2), stream, PngColorType.Rgba);
        stream.Position = 0;

        // Act
        var info = PngCodec.GetInfo(stream);

        // Assert
        Assert.Equal(new ImageInfo(3, 2, 4, true), info);
    }

    /// <summary>
    ///     Proves that GetInfo consumes only the signature and the first (IHDR) chunk - 33 bytes
    ///     - never reading any subsequent chunk, by wrapping a valid PNG's bytes in a stream that
    ///     throws if more than 33 bytes are read.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_NeverReadsPastIhdr()
    {
        // Arrange
        using var source = new MemoryStream();
        PngCodec.Save(BuildTestCanvas(4, 4), source, PngColorType.Rgba);
        var bytes = source.ToArray();
        using var bounded = new BoundedReadStream(new MemoryStream(bytes), maxBytes: 33);

        // Act
        var info = PngCodec.GetInfo(bounded);

        // Assert
        Assert.Equal(new ImageInfo(4, 4, 4, true), info);
    }

    /// <summary>
    ///     Regression test for finding #1: a non-<c>IHDR</c> first chunk with a huge declared
    ///     length used to be fully allocated and read (<c>ReadChunkFrame</c>) before the chunk
    ///     type was ever inspected, so a crafted file could force a multi-gigabyte allocation on
    ///     <see cref="PngCodec.GetInfo(Stream)"/>'s cheap-probing path. Proves, by measuring
    ///     actual bytes allocated (never wall-clock time), that the fixed
    ///     <c>ReadIhdrChunkFrame</c> rejects the non-<c>IHDR</c> type before any length-dependent
    ///     allocation/read is attempted.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_NonIhdrFirstChunkWithHugeDeclaredLength_ThrowsWithoutLargeAllocation()
    {
        // Arrange: signature + a "tEXt"-typed chunk header declaring a 100 MB length, with no
        // real trailing data at all - if the (fixed) code ever attempted to allocate/read the
        // declared-length payload, it would throw a plain end-of-stream InvalidDataException
        // instead of the expected "not IHDR" one, and would have allocated ~100 MB first.
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var header = BuildFakeChunkHeader("tEXt", 100_000_000);
        stream.Write(header, 0, header.Length);
        stream.Position = 0;

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(stream));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("not IHDR", ex.Message, StringComparison.OrdinalIgnoreCase);

        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Regression test for finding #1: an <c>IHDR</c>-typed first chunk with a huge declared
    ///     length (anything other than the mandatory 13) used to be fully allocated and read
    ///     before its length was validated, so a crafted file could force a multi-gigabyte
    ///     allocation on <see cref="PngCodec.GetInfo(Stream)"/>'s cheap-probing path. Proves, by
    ///     measuring actual bytes allocated (never wall-clock time), that the fixed
    ///     <c>ReadIhdrChunkFrame</c> rejects the wrong declared length before any length-dependent
    ///     allocation/read is attempted.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_IhdrChunkWithWrongDeclaredLength_ThrowsWithoutLargeAllocation()
    {
        // Arrange: signature + an "IHDR"-typed chunk header declaring a 100 MB length instead of
        // the mandatory 13, with no real trailing data at all.
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var header = BuildFakeChunkHeader("IHDR", 100_000_000);
        stream.Write(header, 0, header.Length);
        stream.Position = 0;

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(stream));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("13", ex.Message);

        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: a <c>PLTE</c> chunk's size checks used to
    ///     run only after <c>ReadChunkFrame</c> had already allocated and read the full declared
    ///     payload, so a crafted PNG could declare a huge (but still sub-<see cref="int.MaxValue"/>)
    ///     <c>PLTE</c> length purely to force a large allocation before any size check ran. Proves,
    ///     by measuring actual bytes allocated (never wall-clock time or a real multi-gigabyte
    ///     buffer), that a declared length far beyond the 768-byte (256-entry) maximum a
    ///     <c>PLTE</c> chunk can legitimately have is rejected before that payload is allocated.
    /// </summary>
    [Fact]
    public void PngCodec_Load_PlteChunkWithHugeDeclaredLength_ThrowsWithoutLargeAllocation()
    {
        // Arrange: a valid IHDR followed by a "PLTE"-typed chunk header declaring a 100 MB
        // length, with no real trailing data at all - if the declared length were allocated
        // before validation, this would force a ~100 MB allocation.
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 2 /* Truecolor */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var header = BuildFakeChunkHeader("PLTE", 100_000_000);
        stream.Write(header, 0, header.Length);
        stream.Position = 0;

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("PLTE", ex.Message);

        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: a <c>tRNS</c> chunk's size checks used to
    ///     run only after <c>ReadChunkFrame</c> had already allocated and read the full declared
    ///     payload, so a crafted PNG could declare a huge (but still sub-<see cref="int.MaxValue"/>)
    ///     <c>tRNS</c> length purely to force a large allocation before any size check ran. Proves,
    ///     by measuring actual bytes allocated (never wall-clock time or a real multi-gigabyte
    ///     buffer), that a declared length far beyond the 2-byte maximum a grayscale <c>tRNS</c>
    ///     chunk can legitimately have is rejected before that payload is allocated.
    /// </summary>
    [Fact]
    public void PngCodec_Load_TrnsChunkWithHugeDeclaredLength_ThrowsWithoutLargeAllocation()
    {
        // Arrange: a valid grayscale IHDR followed by a "tRNS"-typed chunk header declaring a
        // 100 MB length, with no real trailing data at all - if the declared length were
        // allocated before validation, this would force a ~100 MB allocation.
        using var stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(1, 1, 8, 0 /* Grayscale */, 0, 0, 0);
        stream.Write(ihdr, 0, ihdr.Length);
        var header = BuildFakeChunkHeader("tRNS", 100_000_000);
        stream.Write(header, 0, header.Length);
        stream.Position = 0;

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => PngCodec.Load(stream));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("tRNS", ex.Message);

        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Builds only a chunk's 8-byte length+type header (a 4-byte big-endian declared length
    ///     followed by the 4-byte ASCII type), deliberately writing no data or CRC bytes at all -
    ///     used only by the memory-exhaustion regression tests above to prove the declared length
    ///     is rejected before any length-dependent allocation is attempted, without needing to
    ///     actually provide (or allocate) that much real trailing data.
    /// </summary>
    private static byte[] BuildFakeChunkHeader(string type, uint declaredLength)
    {
        var header = new byte[8];
        WriteUInt32Be(header, 0, declaredLength);
        for (var i = 0; i < 4; i++)
        {
            header[4 + i] = (byte)type[i];
        }

        return header;
    }

    /// <summary>
    ///     Proves that GetInfo succeeds on a file whose IDAT/IEND region is deliberately
    ///     corrupt/truncated (never read by GetInfo), while Load on the same bytes still throws.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_SucceedsWithCorruptIdatRegion_ButLoadThrows()
    {
        // Arrange: a valid PNG whose IDAT payload is corrupted after the IHDR chunk
        using var source = new MemoryStream();
        PngCodec.Save(BuildTestCanvas(4, 4), source, PngColorType.Rgba);
        var bytes = CorruptFinalIdatAdlerByte(source.ToArray());

        // Act: GetInfo reports the correct header info regardless
        using var infoStream = new MemoryStream(bytes);
        var info = PngCodec.GetInfo(infoStream);
        Assert.Equal(new ImageInfo(4, 4, 4, true), info);

        // Assert: Load on the same bytes still throws, since it must decompress the corrupt IDAT
        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(loadStream));
    }

    /// <summary>
    ///     Proves that GetInfo(Stream) rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PngCodec.GetInfo((Stream)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PngCodec.GetInfo((string)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PngCodec.GetInfo(string.Empty));
    }

    /// <summary>
    ///     Proves that GetInfo rejects a stream not starting with the PNG signature with
    ///     InvalidDataException, mirroring Load's malformed-signature rejection.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_BadSignature_ThrowsInvalidDataException()
    {
        var bytes = new byte[8];
        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(stream));
    }

    /// <summary>
    ///     Proves that GetInfo does not enforce Surface.MaxDimension - it returns the raw
    ///     oversized header dimensions rather than throwing - while Load on the exact same
    ///     bytes still throws InvalidDataException.
    /// </summary>
    [Fact]
    public void PngCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows()
    {
        // Arrange: a minimal PNG IHDR declaring a width one above Surface.MaxDimension
        var bytes = BuildMinimalPngHeaderOnly(colorType: (byte)PngColorType.Rgb, width: Surface.MaxDimension + 1);

        // Act
        using var infoStream = new MemoryStream(bytes);
        var info = PngCodec.GetInfo(infoStream);
        Assert.Equal(Surface.MaxDimension + 1, info.Width);

        // Assert
        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(loadStream));
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
    ///     across a chosen number of chunks. The <paramref name="rowBytes"/> and
    ///     <paramref name="bpp"/> parameters let a caller build rows for any bit depth/color-type
    ///     combination (not just <c>width * channels</c> at 8-bit depth): <paramref name="bpp"/>
    ///     is the number of whole bytes per pixel used by the Sub/Average/Paeth filter
    ///     predictors (matching PngCodec's own <c>Math.Max(1, (bitsPerPixel + 7) / 8)</c>
    ///     formula), and <paramref name="rowBytes"/> is the packed byte width of one scanline
    ///     (matching PngCodec's own <c>(width * bitsPerPixel + 7) / 8</c> formula).
    /// </summary>
    private static byte[] BuildPng(
        int width,
        int height,
        int channels,
        byte colorType,
        byte[][] rawRows,
        byte[] filterTypes,
        int idatChunkCount = 1,
        byte bitDepth = 8,
        int? rowBytes = null,
        int? bpp = null,
        byte[]? plteData = null,
        byte[]? trnsData = null)
    {
        var effectiveRowBytes = rowBytes ?? width * channels;
        var effectiveBpp = bpp ?? channels;
        var raw = new byte[(effectiveRowBytes + 1) * height];
        var offset = 0;
        var previousRow = new byte[effectiveRowBytes];
        for (var y = 0; y < height; y++)
        {
            var filterType = filterTypes[y];
            raw[offset] = filterType;
            offset++;

            var rawRow = rawRows[y];
            for (var i = 0; i < effectiveRowBytes; i++)
            {
                int left = i >= effectiveBpp ? rawRow[i - effectiveBpp] : 0;
                int up = previousRow[i];
                int upperLeft = i >= effectiveBpp ? previousRow[i - effectiveBpp] : 0;
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

            offset += effectiveRowBytes;
            previousRow = rawRow;
        }

        var zlib = ZlibCompress(raw);

        using var result = new MemoryStream();
        result.Write(Signature, 0, Signature.Length);
        var ihdr = BuildIhdrChunk(width, height, bitDepth, colorType, 0, 0, 0);
        result.Write(ihdr, 0, ihdr.Length);

        if (plteData != null)
        {
            var plte = BuildChunk("PLTE", plteData);
            result.Write(plte, 0, plte.Length);
        }

        if (trnsData != null)
        {
            var trns = BuildChunk("tRNS", trnsData);
            result.Write(trns, 0, trns.Length);
        }

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
