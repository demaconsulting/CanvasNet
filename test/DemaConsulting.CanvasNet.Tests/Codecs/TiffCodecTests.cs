using System.IO.Compression;
using CanvasNet.Canvas;
using CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the TiffCodec class and the TiffCompression enum.
/// </summary>
/// <remarks>
///     Several tests build raw TIFF byte streams by hand, or decode <see cref="TiffCodec"/>'s own
///     saved output, using independent test-only implementations of PackBits, TIFF-flavor LZW, the
///     horizontal-differencing predictor, and zlib wrapping (rather than calling into
///     <see cref="TiffCodec"/>'s own private algorithms), so that these tests verify
///     <see cref="TiffCodec"/>'s behavior against the TIFF 6.0 and zlib specifications themselves,
///     not just against its own internal consistency.
/// </remarks>
public class TiffCodecTests
{
    private const ushort TagImageWidth = 256;
    private const ushort TagImageLength = 257;
    private const ushort TagBitsPerSample = 258;
    private const ushort TagCompression = 259;
    private const ushort TagPhotometricInterpretation = 262;
    private const ushort TagStripOffsets = 273;
    private const ushort TagSamplesPerPixel = 277;
    private const ushort TagRowsPerStrip = 278;
    private const ushort TagStripByteCounts = 279;
    private const ushort TagPlanarConfiguration = 284;
    private const ushort TagTileWidth = 322;
    private const ushort TagTileLength = 323;
    private const ushort TagPredictor = 317;
    private const ushort TagExtraSamples = 338;

    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;

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

    // ------------------------------------------------------------------------------------------
    // Independent test-only byte-order helpers (deliberately separate from TiffCodec's own copy)
    // ------------------------------------------------------------------------------------------

    private static ushort ReadU16(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            : (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    private static uint ReadU32(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3]
            : buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);

    // ------------------------------------------------------------------------------------------
    // Independent test-only hand-built TIFF file builder
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     A minimal, independent TIFF file builder used only by these tests, so that hand-built
    ///     TIFF byte streams do not depend on any of <see cref="TiffCodec"/>'s own private layout
    ///     logic.
    /// </summary>
    private sealed class TestTiffBuilder(bool bigEndian)
    {
        private readonly List<(ushort Tag, ushort Type, uint[] Values)> _tags = [];
        private byte[][]? _strips;

        public TestTiffBuilder Add(ushort tag, ushort type, params uint[] values)
        {
            _tags.Add((tag, type, values));
            return this;
        }

        public TestTiffBuilder WithStrips(params byte[][] strips)
        {
            _strips = strips;
            return this;
        }

        private static void WriteU16(byte[] buffer, int offset, ushort value, bool bigEndian)
        {
            if (bigEndian)
            {
                buffer[offset] = (byte)(value >> 8);
                buffer[offset + 1] = (byte)value;
            }
            else
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
            }
        }

        private static void WriteU32(byte[] buffer, int offset, uint value, bool bigEndian)
        {
            if (bigEndian)
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
            else
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }
        }

        public byte[] Build()
        {
            var tags = new List<(ushort Tag, ushort Type, uint[] Values)>(_tags);
            if (_strips != null)
            {
                tags.Add((TagStripOffsets, TypeLong, new uint[_strips.Length]));
                tags.Add((TagStripByteCounts, TypeLong, [.. _strips.Select(s => (uint)s.Length)]));
            }

            tags.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            var stripsIndex = _strips != null ? tags.FindIndex(t => t.Tag == TagStripOffsets) : -1;

            var entryCount = tags.Count;
            var ifdSize = 2 + (entryCount * 12) + 4;
            var pos = 8 + ifdSize;

            var externalOffsets = new int[tags.Count];
            for (var i = 0; i < tags.Count; i++)
            {
                var (_, type, values) = tags[i];
                var typeSize = type == TypeShort ? 2 : 4;
                var total = typeSize * values.Length;
                if (total > 4)
                {
                    externalOffsets[i] = pos;
                    pos += total;
                }
                else
                {
                    externalOffsets[i] = -1;
                }
            }

            var stripDataStart = pos;
            if (_strips != null)
            {
                var stripOffsetValues = new uint[_strips.Length];
                var offset = stripDataStart;
                for (var i = 0; i < _strips.Length; i++)
                {
                    stripOffsetValues[i] = (uint)offset;
                    offset += _strips[i].Length;
                }

                tags[stripsIndex] = (tags[stripsIndex].Tag, tags[stripsIndex].Type, stripOffsetValues);
            }

            var totalStripBytes = _strips?.Sum(s => s.Length) ?? 0;
            var file = new byte[stripDataStart + totalStripBytes];

            file[0] = bigEndian ? (byte)'M' : (byte)'I';
            file[1] = bigEndian ? (byte)'M' : (byte)'I';
            WriteU16(file, 2, 42, bigEndian);
            WriteU32(file, 4, 8, bigEndian);

            WriteU16(file, 8, (ushort)entryCount, bigEndian);
            var entryPos = 10;
            for (var i = 0; i < tags.Count; i++)
            {
                var (tag, type, values) = tags[i];
                WriteU16(file, entryPos, tag, bigEndian);
                WriteU16(file, entryPos + 2, type, bigEndian);
                WriteU32(file, entryPos + 4, (uint)values.Length, bigEndian);

                if (externalOffsets[i] < 0)
                {
                    var inlinePos = entryPos + 8;
                    foreach (var value in values)
                    {
                        if (type == TypeShort)
                        {
                            WriteU16(file, inlinePos, (ushort)value, bigEndian);
                            inlinePos += 2;
                        }
                        else
                        {
                            WriteU32(file, inlinePos, value, bigEndian);
                            inlinePos += 4;
                        }
                    }
                }
                else
                {
                    WriteU32(file, entryPos + 8, (uint)externalOffsets[i], bigEndian);
                    var externalPos = externalOffsets[i];
                    foreach (var value in values)
                    {
                        if (type == TypeShort)
                        {
                            WriteU16(file, externalPos, (ushort)value, bigEndian);
                            externalPos += 2;
                        }
                        else
                        {
                            WriteU32(file, externalPos, value, bigEndian);
                            externalPos += 4;
                        }
                    }
                }

                entryPos += 12;
            }

            WriteU32(file, entryPos, 0, bigEndian);

            if (_strips != null)
            {
                var offset = stripDataStart;
                foreach (var strip in _strips)
                {
                    strip.CopyTo(file, offset);
                    offset += strip.Length;
                }
            }

            return file;
        }
    }

    /// <summary>
    ///     Adds the standard set of mandatory tags for an 8-bit RGB (3 samples per pixel) image,
    ///     to be combined with <see cref="TestTiffBuilder.WithStrips"/>.
    /// </summary>
    private static TestTiffBuilder StandardRgbBuilder(bool bigEndian, int width, int height, int compression, int rowsPerStrip) =>
        new TestTiffBuilder(bigEndian)
            .Add(TagImageWidth, TypeLong, (uint)width)
            .Add(TagImageLength, TypeLong, (uint)height)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8)
            .Add(TagCompression, TypeShort, (uint)compression)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 3)
            .Add(TagRowsPerStrip, TypeLong, (uint)rowsPerStrip)
            .Add(TagPlanarConfiguration, TypeShort, 1);

    // ------------------------------------------------------------------------------------------
    // Independent test-only PackBits, LZW, predictor, and zlib implementations
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Encodes a byte sequence using a simple, entirely-literal PackBits (TIFF 6.0 Section 9)
    ///     stream (one or more literal runs of at most 128 bytes each), independent of
    ///     <see cref="TiffCodec"/>'s own PackBits implementation.
    /// </summary>
    private static byte[] TestEncodePackBitsLiteral(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var length = Math.Min(128, data.Length - i);
            output.WriteByte((byte)(length - 1));
            output.Write(data, i, length);
            i += length;
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Decodes a PackBits (TIFF 6.0 Section 9) run-length encoded byte stream, built entirely
    ///     independently of <see cref="TiffCodec"/>'s own decoder, directly from the specification
    ///     text: control byte 0-127 copies the next n+1 literal bytes; 129-255 (interpreted as
    ///     signed -127 to -1) repeats the next single byte -n+1 times; 128 is a no-op.
    /// </summary>
    private static byte[] TestDecodePackBits(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var control = data[i];
            i++;
            if (control <= 127)
            {
                var count = control + 1;
                output.Write(data, i, count);
                i += count;
            }
            else if (control != 128)
            {
                var count = 257 - control;
                var value = data[i];
                i++;
                for (var j = 0; j < count; j++)
                {
                    output.WriteByte(value);
                }
            }
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Applies the TIFF horizontal-differencing predictor (Predictor tag value 2) to a single
    ///     row's bytes, built entirely independently of <see cref="TiffCodec"/>'s own
    ///     implementation, directly from TIFF 6.0 Section 14's description.
    /// </summary>
    private static byte[] TestApplyHorizontalPredictor(byte[] row, int samplesPerPixel)
    {
        var result = (byte[])row.Clone();
        for (var i = result.Length - 1; i >= samplesPerPixel; i--)
        {
            result[i] = (byte)(result[i] - row[i - samplesPerPixel]);
        }

        return result;
    }

    /// <summary>
    ///     Reverses the TIFF horizontal-differencing predictor on a single row's bytes, built
    ///     entirely independently of <see cref="TiffCodec"/>'s own implementation.
    /// </summary>
    private static byte[] TestRemoveHorizontalPredictor(byte[] row, int samplesPerPixel)
    {
        var result = (byte[])row.Clone();
        for (var i = samplesPerPixel; i < result.Length; i++)
        {
            result[i] = (byte)(result[i] + result[i - samplesPerPixel]);
        }

        return result;
    }

    /// <summary>
    ///     Encodes a byte sequence using the TIFF-flavor LZW algorithm (TIFF 6.0 Section 13:
    ///     variable-width 9-12 bit codes, MSB-first bit packing, clear code 256, end-of-information
    ///     code 257), written entirely independently of <see cref="TiffCodec"/>'s own encoder,
    ///     directly from the specification text.
    /// </summary>
    private static byte[] TestEncodeLzw(byte[] data)
    {
        var bytes = new List<byte>();
        ulong bitBuffer = 0;
        var bitCount = 0;

        void WriteCode(int code, int bits)
        {
            bitBuffer = (bitBuffer << bits) | (uint)code;
            bitCount += bits;
            while (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }

            bitBuffer &= (1UL << bitCount) - 1;
        }

        var codeSize = 9;
        var nextCode = 258;
        var table = new Dictionary<(int, byte), int>();
        WriteCode(256, codeSize);

        if (data.Length > 0)
        {
            var prefix = (int)data[0];
            for (var i = 1; i < data.Length; i++)
            {
                var b = data[i];
                if (table.TryGetValue((prefix, b), out var existing))
                {
                    prefix = existing;
                    continue;
                }

                WriteCode(prefix, codeSize);
                table[(prefix, b)] = nextCode;
                nextCode++;
                if (nextCode is 511 or 1023 or 2047)
                {
                    codeSize++;
                }

                prefix = b;
            }

            WriteCode(prefix, codeSize);
        }

        WriteCode(257, codeSize);
        if (bitCount > 0)
        {
            bytes.Add((byte)((bitBuffer << (8 - bitCount)) & 0xFF));
        }

        return [.. bytes];
    }

    /// <summary>
    ///     Compresses raw bytes into a complete zlib stream (2-byte header, DEFLATE data, 4-byte
    ///     big-endian Adler-32 trailer), written entirely independently of <see cref="TiffCodec"/>'s
    ///     own zlib wrapper.
    /// </summary>
    private static byte[] TestZlibCompress(byte[] rawData)
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

        var a = 1u;
        var b = 0u;
        foreach (var value in rawData)
        {
            a = (a + value) % AdlerModulus;
            b = (b + a) % AdlerModulus;
        }

        var adler = (b << 16) | a;
        var result = new byte[2 + deflateData.Length + 4];
        result[0] = 0x78;
        result[1] = 0x9C;
        deflateData.CopyTo(result, 2);
        result[^4] = (byte)(adler >> 24);
        result[^3] = (byte)(adler >> 16);
        result[^2] = (byte)(adler >> 8);
        result[^1] = (byte)adler;
        return result;
    }

    /// <summary>
    ///     Decompresses a complete zlib stream, independent of <see cref="TiffCodec"/>'s own
    ///     implementation.
    /// </summary>
    private static byte[] TestZlibDecompress(byte[] zlibData)
    {
        var deflateData = zlibData.AsSpan(2, zlibData.Length - 2 - 4).ToArray();
        using var input = new MemoryStream(deflateData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    ///     Decodes a TIFF-flavor LZW stream, written entirely independently of
    ///     <see cref="TiffCodec"/>'s own decoder, directly from the TIFF 6.0 Section 13
    ///     specification text.
    /// </summary>
    private static byte[] TestDecodeLzwStandalone(byte[] data)
    {
        var bytePos = 0;
        ulong bitBuffer = 0;
        var bitCount = 0;

        int ReadCode(int bits)
        {
            while (bitCount < bits)
            {
                bitBuffer = (bitBuffer << 8) | data[bytePos];
                bytePos++;
                bitCount += 8;
            }

            bitCount -= bits;
            return (int)((bitBuffer >> bitCount) & ((1UL << bits) - 1));
        }

        using var output = new MemoryStream();
        var codeSize = 9;
        var table = new List<byte[]>();
        byte[]? previous = null;

        var first = ReadCode(codeSize);
        Assert.Equal(256, first);

        while (true)
        {
            var code = ReadCode(codeSize);
            if (code == 257)
            {
                break;
            }

            if (code == 256)
            {
                table.Clear();
                codeSize = 9;
                previous = null;
                continue;
            }

            byte[] entry;
            if (code < 256)
            {
                entry = [(byte)code];
            }
            else if (code - 258 < table.Count)
            {
                entry = table[code - 258];
            }
            else
            {
                entry = new byte[previous!.Length + 1];
                previous.CopyTo(entry, 0);
                entry[^1] = previous[0];
            }

            output.Write(entry, 0, entry.Length);

            if (previous != null)
            {
                var newEntry = new byte[previous.Length + 1];
                previous.CopyTo(newEntry, 0);
                newEntry[^1] = entry[0];
                table.Add(newEntry);
                var nextCode = 258 + table.Count;
                if (nextCode is 511 or 1023 or 2047)
                {
                    codeSize++;
                }
            }

            previous = entry;
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Locates and reads the first strip's raw (still-compressed, predictor-applied) bytes and
    ///     the relevant IFD tag values from a little-endian TIFF file, independent of
    ///     <see cref="TiffCodec"/>'s own IFD parser. Used only to inspect
    ///     <see cref="TiffCodec.Save(Surface, Stream, TiffCompression)"/>'s own output.
    /// </summary>
    private static (int Width, int Height, int Compression, int Predictor, int SamplesPerPixel, byte[] StripBytes)
        ReadFirstStripFromSavedFile(byte[] file)
    {
        Assert.Equal((byte)'I', file[0]);
        Assert.Equal((byte)'I', file[1]);

        var ifdOffset = ReadU32(file, 4, false);
        var entryCount = ReadU16(file, (int)ifdOffset, false);
        var pos = (int)ifdOffset + 2;

        var width = 0;
        var height = 0;
        var compression = 0;
        var predictor = 1;
        var samplesPerPixel = 0;
        uint stripOffset = 0;
        uint stripByteCount = 0;

        for (var i = 0; i < entryCount; i++)
        {
            var tag = ReadU16(file, pos, false);
            var type = ReadU16(file, pos + 2, false);
            var value = type == TypeShort ? ReadU16(file, pos + 8, false) : ReadU32(file, pos + 8, false);

            switch (tag)
            {
                case TagImageWidth:
                    width = (int)value;
                    break;
                case TagImageLength:
                    height = (int)value;
                    break;
                case TagCompression:
                    compression = (int)value;
                    break;
                case TagPredictor:
                    predictor = (int)value;
                    break;
                case TagSamplesPerPixel:
                    samplesPerPixel = (int)value;
                    break;
                case TagStripOffsets:
                    stripOffset = value;
                    break;
                case TagStripByteCounts:
                    stripByteCount = value;
                    break;
            }

            pos += 12;
        }

        var stripBytes = file.AsSpan((int)stripOffset, (int)stripByteCount).ToArray();
        return (width, height, compression, predictor, samplesPerPixel, stripBytes);
    }

    /// <summary>
    ///     Decodes a saved TIFF's first strip and reverses any predictor, using only the
    ///     independent test-only decoders above, returning the raw (RGBA) pixel bytes for direct
    ///     comparison against the original surface.
    /// </summary>
    private static byte[] DecodeSavedStripIndependently(byte[] file)
    {
        var (width, height, compression, predictor, samplesPerPixel, stripBytes) = ReadFirstStripFromSavedFile(file);

        var decompressed = compression switch
        {
            1 => stripBytes,
            32773 => TestDecodePackBits(stripBytes),
            5 => TestDecodeLzwStandalone(stripBytes),
            8 => TestZlibDecompress(stripBytes),
            _ => throw new InvalidOperationException("Unexpected compression value.")
        };

        var rowBytes = width * samplesPerPixel;
        if (predictor != 2)
        {
            return decompressed;
        }

        var result = new byte[decompressed.Length];
        for (var y = 0; y < height; y++)
        {
            var row = decompressed.AsSpan(y * rowBytes, rowBytes).ToArray();
            var restored = TestRemoveHorizontalPredictor(row, samplesPerPixel);
            restored.CopyTo(result, y * rowBytes);
        }

        return result;
    }

    // ------------------------------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that Load(Stream) rejects a null stream argument.
    /// </summary>
    [Fact]
    public void TiffCodec_LoadStream_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Load((Stream)null!));
    }

    /// <summary>
    ///     Verifies that Load(string) rejects a null path argument.
    /// </summary>
    [Fact]
    public void TiffCodec_LoadPath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Load((string)null!));
    }

    /// <summary>
    ///     Verifies that Load(string) rejects an empty path argument.
    /// </summary>
    [Fact]
    public void TiffCodec_LoadPath_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TiffCodec.Load(string.Empty));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, TiffCompression) rejects a null surface argument.
    /// </summary>
    [Fact]
    public void TiffCodec_SaveStream_NullCanvas_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Save(null!, stream));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, TiffCompression) rejects a null stream argument.
    /// </summary>
    [Fact]
    public void TiffCodec_SaveStream_NullStream_ThrowsArgumentNullException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Save(surface, (Stream)null!));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, TiffCompression) rejects a null surface argument.
    /// </summary>
    [Fact]
    public void TiffCodec_SavePath_NullCanvas_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Save(null!, "test.tiff"));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, TiffCompression) rejects a null path argument.
    /// </summary>
    [Fact]
    public void TiffCodec_SavePath_NullPath_ThrowsArgumentNullException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentNullException>(() => TiffCodec.Save(surface, (string)null!));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, TiffCompression) rejects an empty path argument.
    /// </summary>
    [Fact]
    public void TiffCodec_SavePath_EmptyPath_ThrowsArgumentException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentException>(() => TiffCodec.Save(surface, string.Empty));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, TiffCompression) rejects an undefined TiffCompression value.
    /// </summary>
    [Fact]
    public void TiffCodec_SaveStream_UndefinedCompression_ThrowsArgumentOutOfRangeException()
    {
        using var stream = new MemoryStream();
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => TiffCodec.Save(surface, stream, (TiffCompression)999));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, TiffCompression) rejects an undefined TiffCompression value.
    /// </summary>
    [Fact]
    public void TiffCodec_SavePath_UndefinedCompression_ThrowsArgumentOutOfRangeException()
    {
        var surface = new Surface(1, 1);
        var path = Path.GetTempFileName();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TiffCodec.Save(surface, path, (TiffCompression)999));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Round-trip save/load (production Save + production Load)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Proves that saving with each supported TiffCompression value and loading back reproduces every pixel exactly, including alpha.
    /// </summary>
    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.PackBits)]
    [InlineData(TiffCompression.Lzw)]
    [InlineData(TiffCompression.Deflate)]
    public void TiffCodec_SaveThenLoad_AllCompressions_RoundTripsExactly(TiffCompression compression)
    {
        var surface = BuildTestCanvas(9, 7);

        using var stream = new MemoryStream();
        TiffCodec.Save(surface, stream, compression);
        stream.Position = 0;
        var loaded = TiffCodec.Load(stream);

        Assert.Equal(surface.Width, loaded.Width);
        Assert.Equal(surface.Height, loaded.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], loaded[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the path-based Save/Load overloads round-trip a pixel correctly.
    /// </summary>
    [Fact]
    public void TiffCodec_SaveThenLoad_PathRoundTrip_ReturnsExpectedPixel()
    {
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(10, 20, 30, 40);

        var path = Path.GetTempFileName();
        try
        {
            TiffCodec.Save(surface, path, TiffCompression.PackBits);
            var loaded = TiffCodec.Load(path);
            Assert.Equal(new Rgba32(10, 20, 30, 40), loaded[1, 0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that Save writes the Predictor tag (value 2) for LZW/Deflate compression, and that an independent test-only decoder reproduces the original raw pixel bytes exactly.
    /// </summary>
    [Theory]
    [InlineData(TiffCompression.Lzw)]
    [InlineData(TiffCompression.Deflate)]
    public void TiffCodec_Save_WithPredictorCompression_WritesPredictorTagAndIndependentlyDecodesToSameBytes(
        TiffCompression compression)
    {
        var surface = BuildTestCanvas(11, 5);

        using var stream = new MemoryStream();
        TiffCodec.Save(surface, stream, compression);
        var fileBytes = stream.ToArray();

        var expectedRaw = new byte[surface.Width * surface.Height * 4];
        for (var y = 0; y < surface.Height; y++)
        {
            surface.GetRowSpanBytes(y).CopyTo(expectedRaw.AsSpan(y * surface.Width * 4, surface.Width * 4));
        }

        var (_, _, _, predictor, _, _) = ReadFirstStripFromSavedFile(fileBytes);
        Assert.Equal(2, predictor);

        var decoded = DecodeSavedStripIndependently(fileBytes);
        Assert.Equal(expectedRaw, decoded);
    }

    /// <summary>
    ///     Proves that Save omits the Predictor tag (defaulting to 1, none) for None/PackBits compression.
    /// </summary>
    [Theory]
    [InlineData(TiffCompression.None)]
    [InlineData(TiffCompression.PackBits)]
    public void TiffCodec_Save_WithoutPredictorCompression_OmitsPredictorTag(TiffCompression compression)
    {
        var surface = BuildTestCanvas(6, 4);

        using var stream = new MemoryStream();
        TiffCodec.Save(surface, stream, compression);
        var fileBytes = stream.ToArray();

        var (_, _, _, predictor, _, _) = ReadFirstStripFromSavedFile(fileBytes);
        Assert.Equal(1, predictor);
    }

    // ------------------------------------------------------------------------------------------
    // Hand-built stream tests: byte order, photometric, RGB/Grayscale, multi-strip
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that Load decodes a hand-built little-endian, single-strip, uncompressed RGB TIFF correctly.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_LittleEndianRgbSingleStrip_ReturnsExpectedPixels()
    {
        const int width = 3;
        const int height = 2;
        var raw = new byte[]
        {
            255, 0, 0, 0, 255, 0, 0, 0, 255,
            10, 20, 30, 40, 50, 60, 70, 80, 90
        };

        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(raw)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(width, surface.Width);
        Assert.Equal(height, surface.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[2, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[0, 1]);
        Assert.Equal(new Rgba32(40, 50, 60, 255), surface[1, 1]);
        Assert.Equal(new Rgba32(70, 80, 90, 255), surface[2, 1]);
    }

    /// <summary>
    ///     Verifies that Load decodes a hand-built big-endian, single-strip, uncompressed RGB TIFF correctly.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_BigEndianRgbSingleStrip_ReturnsExpectedPixels()
    {
        const int width = 2;
        const int height = 1;
        var raw = new byte[] { 1, 2, 3, 4, 5, 6 };

        var file = StandardRgbBuilder(true, width, height, 1, height)
            .WithStrips(raw)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 2, 3, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(4, 5, 6, 255), surface[1, 0]);
    }

    /// <summary>
    ///     Verifies that Load preserves the alpha channel for an RGB image with 4 samples per pixel and an ExtraSamples tag of 2 (unassociated alpha).
    /// </summary>
    [Fact]
    public void TiffCodec_Load_RgbaWithExtraSamples_PreservesAlpha()
    {
        const int width = 2;
        const int height = 1;
        var raw = new byte[] { 1, 2, 3, 128, 4, 5, 6, 64 };

        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, width)
            .Add(TagImageLength, TypeLong, height)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8, 8)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 4)
            .Add(TagRowsPerStrip, TypeLong, (uint)height)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .Add(TagExtraSamples, TypeShort, 2)
            .WithStrips(raw)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 2, 3, 128), surface[0, 0]);
        Assert.Equal(new Rgba32(4, 5, 6, 64), surface[1, 0]);
    }

    /// <summary>
    ///     Verifies that Load decodes a Grayscale (BlackIsZero) image as R=G=B=gray with full alpha.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_Grayscale_ReturnsGrayPixelsWithFullAlpha()
    {
        const int width = 2;
        const int height = 1;
        var raw = new byte[] { 100, 200 };

        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, width)
            .Add(TagImageLength, TypeLong, height)
            .Add(TagBitsPerSample, TypeShort, 8)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 1)
            .Add(TagSamplesPerPixel, TypeShort, 1)
            .Add(TagRowsPerStrip, TypeLong, (uint)height)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .WithStrips(raw)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(100, 100, 100, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(200, 200, 200, 255), surface[1, 0]);
    }

    /// <summary>
    ///     Verifies that Load correctly reassembles an image split across multiple one-row strips.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_MultiStrip_ReturnsExpectedPixels()
    {
        const int width = 2;
        const int height = 4;
        var strip0 = new byte[] { 1, 1, 1, 2, 2, 2 };
        var strip1 = new byte[] { 3, 3, 3, 4, 4, 4 };
        var strip2 = new byte[] { 5, 5, 5, 6, 6, 6 };
        var strip3 = new byte[] { 7, 7, 7, 8, 8, 8 };

        var file = StandardRgbBuilder(false, width, height, 1, 1)
            .WithStrips(strip0, strip1, strip2, strip3)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 1, 1, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(2, 2, 2, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(3, 3, 3, 255), surface[0, 1]);
        Assert.Equal(new Rgba32(4, 4, 4, 255), surface[1, 1]);
        Assert.Equal(new Rgba32(5, 5, 5, 255), surface[0, 2]);
        Assert.Equal(new Rgba32(6, 6, 6, 255), surface[1, 2]);
        Assert.Equal(new Rgba32(7, 7, 7, 255), surface[0, 3]);
        Assert.Equal(new Rgba32(8, 8, 8, 255), surface[1, 3]);
    }

    /// <summary>
    ///     Verifies that Load correctly reassembles an image split across multiple strips where the final strip contains fewer rows than RowsPerStrip.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_MultiStripWithTwoRowsPerStrip_ReturnsExpectedPixels()
    {
        const int width = 1;
        const int height = 3;
        var strip0 = new byte[] { 10, 10, 10, 20, 20, 20 };
        var strip1 = new byte[] { 30, 30, 30 };

        var file = StandardRgbBuilder(false, width, height, 1, 2)
            .WithStrips(strip0, strip1)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(10, 10, 10, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(20, 20, 20, 255), surface[0, 1]);
        Assert.Equal(new Rgba32(30, 30, 30, 255), surface[0, 2]);
    }

    /// <summary>
    ///     Verifies that Load decodes a strip compressed with an independent, test-only PackBits encoder.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_PackBitsCompressedStrip_DecodesUsingIndependentEncoder()
    {
        const int width = 4;
        const int height = 1;
        var raw = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var compressed = TestEncodePackBitsLiteral(raw);

        var file = StandardRgbBuilder(false, width, height, 32773, height)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 2, 3, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(4, 5, 6, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(7, 8, 9, 255), surface[2, 0]);
        Assert.Equal(new Rgba32(10, 11, 12, 255), surface[3, 0]);
    }

    /// <summary>
    ///     Verifies that Load decodes a strip compressed with an independent, test-only TIFF-flavor LZW encoder.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_LzwCompressedStrip_DecodesUsingIndependentEncoder()
    {
        const int width = 4;
        const int height = 1;
        var raw = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var compressed = TestEncodeLzw(raw);

        var file = StandardRgbBuilder(false, width, height, 5, height)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 2, 3, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(4, 5, 6, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(7, 8, 9, 255), surface[2, 0]);
        Assert.Equal(new Rgba32(10, 11, 12, 255), surface[3, 0]);
    }

    /// <summary>
    ///     Verifies that Load correctly decodes LZW data containing repeating patterns, exercising the LZW dictionary growth logic.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_LzwCompressedRepeatingData_DecodesUsingIndependentEncoder()
    {
        const int width = 20;
        const int height = 1;
        var raw = new byte[width * 3];
        for (var i = 0; i < raw.Length; i++)
        {
            raw[i] = (byte)(i % 5);
        }

        var compressed = TestEncodeLzw(raw);

        var file = StandardRgbBuilder(false, width, height, 5, height)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        for (var x = 0; x < width; x++)
        {
            var expected = new Rgba32(raw[x * 3], raw[(x * 3) + 1], raw[(x * 3) + 2], 255);
            Assert.Equal(expected, surface[x, 0]);
        }
    }

    /// <summary>
    ///     Verifies that Load decodes a strip compressed with an independent, test-only zlib/Deflate encoder.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_DeflateCompressedStrip_DecodesUsingIndependentEncoder()
    {
        const int width = 4;
        const int height = 1;
        var raw = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var compressed = TestZlibCompress(raw);

        var file = StandardRgbBuilder(false, width, height, 8, height)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        Assert.Equal(new Rgba32(1, 2, 3, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(4, 5, 6, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(7, 8, 9, 255), surface[2, 0]);
        Assert.Equal(new Rgba32(10, 11, 12, 255), surface[3, 0]);
    }

    /// <summary>
    ///     Verifies that Load correctly reverses the horizontal-differencing predictor combined with LZW compression, using independent test-only implementations of both.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_PredictorWithLzw_DecodesUsingIndependentEncoder()
    {
        const int width = 4;
        const int height = 2;
        var row0 = new byte[] { 10, 20, 30, 15, 25, 35, 20, 30, 40, 25, 35, 45 };
        var row1 = new byte[] { 5, 5, 5, 8, 8, 8, 12, 12, 12, 16, 16, 16 };

        var predictedRow0 = TestApplyHorizontalPredictor(row0, 3);
        var predictedRow1 = TestApplyHorizontalPredictor(row1, 3);
        var predicted = new byte[predictedRow0.Length + predictedRow1.Length];
        predictedRow0.CopyTo(predicted, 0);
        predictedRow1.CopyTo(predicted, predictedRow0.Length);

        var compressed = TestEncodeLzw(predicted);

        var file = StandardRgbBuilder(false, width, height, 5, height)
            .Add(TagPredictor, TypeShort, 2)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        for (var x = 0; x < width; x++)
        {
            Assert.Equal(new Rgba32(row0[x * 3], row0[(x * 3) + 1], row0[(x * 3) + 2], 255), surface[x, 0]);
            Assert.Equal(new Rgba32(row1[x * 3], row1[(x * 3) + 1], row1[(x * 3) + 2], 255), surface[x, 1]);
        }
    }

    /// <summary>
    ///     Verifies that Load correctly reverses the horizontal-differencing predictor combined with Deflate compression, using independent test-only implementations of both.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_PredictorWithDeflate_DecodesUsingIndependentEncoder()
    {
        const int width = 3;
        const int height = 1;
        var row0 = new byte[] { 10, 20, 30, 15, 25, 35, 20, 30, 40 };
        var predicted = TestApplyHorizontalPredictor(row0, 3);
        var compressed = TestZlibCompress(predicted);

        var file = StandardRgbBuilder(false, width, height, 8, height)
            .Add(TagPredictor, TypeShort, 2)
            .WithStrips(compressed)
            .Build();

        var surface = TiffCodec.Load(new MemoryStream(file));

        for (var x = 0; x < width; x++)
        {
            Assert.Equal(new Rgba32(row0[x * 3], row0[(x * 3) + 1], row0[(x * 3) + 2], 255), surface[x, 0]);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Malformed-data rejection
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that Load rejects a file whose byte-order mark is neither "II" nor "MM".
    /// </summary>
    [Fact]
    public void TiffCodec_Load_BadByteOrderMark_ThrowsInvalidDataException()
    {
        var file = StandardRgbBuilder(false, 1, 1, 1, 1).WithStrips(new byte[] { 1, 2, 3 }).Build();
        file[0] = (byte)'X';
        file[1] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream that ends before the 8-byte TIFF header is complete.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_TruncatedHeader_ThrowsInvalidDataException()
    {
        var file = new byte[] { (byte)'I', (byte)'I', 42 };
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file whose strip data is shorter than the declared image dimensions require.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_TruncatedStripData_ThrowsInvalidDataException()
    {
        var file = StandardRgbBuilder(false, 4, 4, 1, 4).WithStrips(new byte[] { 1, 2, 3 }).Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file with a bits-per-sample value other than 8.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_UnsupportedBitsPerSample_ThrowsInvalidDataException()
    {
        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, 1)
            .Add(TagImageLength, TypeLong, 1)
            .Add(TagBitsPerSample, TypeShort, 16, 16, 16)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 3)
            .Add(TagRowsPerStrip, TypeLong, 1)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .WithStrips(new byte[] { 1, 2, 3, 4, 5, 6 })
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file with an unsupported photometric interpretation (Palette).
    /// </summary>
    [Fact]
    public void TiffCodec_Load_UnsupportedPhotometricPalette_ThrowsInvalidDataException()
    {
        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, 1)
            .Add(TagImageLength, TypeLong, 1)
            .Add(TagBitsPerSample, TypeShort, 8)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 3)
            .Add(TagSamplesPerPixel, TypeShort, 1)
            .Add(TagRowsPerStrip, TypeLong, 1)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .WithStrips(new byte[] { 1 })
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file with an unsupported compression value.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_UnsupportedCompression_ThrowsInvalidDataException()
    {
        var file = StandardRgbBuilder(false, 1, 1, 6, 1).WithStrips(new byte[] { 1, 2, 3 }).Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a width exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor, and before any row-byte-width arithmetic derived from the
    ///     declared width is ever performed.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var width = Surface.MaxDimension + 1;
        var file = StandardRgbBuilder(false, width, 1, 1, 1)
            .WithStrips(new byte[width * 3])
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a height exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var height = Surface.MaxDimension + 1;
        var file = StandardRgbBuilder(false, 1, height, 1, height)
            .WithStrips(new byte[height * 3])
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file with Planar (2) planar configuration.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_PlanarConfigurationPlanar_ThrowsInvalidDataException()
    {
        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, 1)
            .Add(TagImageLength, TypeLong, 1)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 3)
            .Add(TagRowsPerStrip, TypeLong, 1)
            .Add(TagPlanarConfiguration, TypeShort, 2)
            .WithStrips(new byte[] { 1, 2, 3 })
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a tiled TIFF (identified by the presence of TileWidth/TileLength tags).
    /// </summary>
    [Fact]
    public void TiffCodec_Load_TiledTiff_ThrowsInvalidDataException()
    {
        var file = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, 16)
            .Add(TagImageLength, TypeLong, 16)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 3)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .Add(TagTileWidth, TypeLong, 16)
            .Add(TagTileLength, TypeLong, 16)
            .Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }

    /// <summary>
    ///     Verifies that Load rejects a file missing any one of the mandatory TIFF tags.
    /// </summary>
    [Theory]
    [InlineData(TagImageWidth)]
    [InlineData(TagImageLength)]
    [InlineData(TagBitsPerSample)]
    [InlineData(TagCompression)]
    [InlineData(TagPhotometricInterpretation)]
    [InlineData(TagStripOffsets)]
    [InlineData(TagRowsPerStrip)]
    [InlineData(TagStripByteCounts)]
    public void TiffCodec_Load_MissingMandatoryTag_ThrowsInvalidDataException(ushort tagToOmit)
    {
        var builder = new TestTiffBuilder(false);
        if (tagToOmit != TagImageWidth)
        {
            builder.Add(TagImageWidth, TypeLong, 1);
        }

        if (tagToOmit != TagImageLength)
        {
            builder.Add(TagImageLength, TypeLong, 1);
        }

        if (tagToOmit != TagBitsPerSample)
        {
            builder.Add(TagBitsPerSample, TypeShort, 8, 8, 8);
        }

        if (tagToOmit != TagCompression)
        {
            builder.Add(TagCompression, TypeShort, 1);
        }

        if (tagToOmit != TagPhotometricInterpretation)
        {
            builder.Add(TagPhotometricInterpretation, TypeShort, 2);
        }

        builder.Add(TagSamplesPerPixel, TypeShort, 3);
        builder.Add(TagPlanarConfiguration, TypeShort, 1);

        if (tagToOmit != TagRowsPerStrip)
        {
            builder.Add(TagRowsPerStrip, TypeLong, 1);
        }

        if (tagToOmit is TagStripOffsets or TagStripByteCounts)
        {
            // Skip the WithStrips helper (which always adds both StripOffsets and
            // StripByteCounts) so the targeted tag can be genuinely absent.
            var otherTag = tagToOmit == TagStripOffsets ? TagStripByteCounts : TagStripOffsets;
            builder.Add(otherTag, TypeLong, 3);
        }
        else
        {
            builder.WithStrips(new byte[] { 1, 2, 3 });
        }

        var file = builder.Build();

        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(new MemoryStream(file)));
    }
}
