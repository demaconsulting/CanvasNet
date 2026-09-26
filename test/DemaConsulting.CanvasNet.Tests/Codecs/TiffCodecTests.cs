using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

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

    /// <summary>
    ///     Adds the standard set of mandatory tags for an 8-bit RGB (3 samples per pixel) image,
    ///     deliberately <em>omitting</em> the <c>SamplesPerPixel</c> tag entirely (unlike
    ///     <see cref="StandardRgbBuilder"/>, which always adds it explicitly), so that tests can
    ///     exercise the absent-tag defaulting behavior (defaulting to <c>BitsPerSample</c>'s entry
    ///     count, per <see cref="ImageInfo"/>'s documented TIFF contract). To be combined with
    ///     <see cref="TestTiffBuilder.WithStrips"/>.
    /// </summary>
    private static TestTiffBuilder StandardRgbBuilderWithoutSamplesPerPixel(bool bigEndian, int width, int height, int compression, int rowsPerStrip) =>
        new TestTiffBuilder(bigEndian)
            .Add(TagImageWidth, TypeLong, (uint)width)
            .Add(TagImageLength, TypeLong, (uint)height)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8)
            .Add(TagCompression, TypeShort, (uint)compression)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagRowsPerStrip, TypeLong, (uint)rowsPerStrip)
            .Add(TagPlanarConfiguration, TypeShort, 1);

    /// <summary>
    ///     Adds the standard set of mandatory tags for an 8-bit RGBA (4 samples per pixel, with
    ///     an ExtraSamples tag marking the 4th sample as unassociated alpha) image, to be
    ///     combined with <see cref="TestTiffBuilder.WithStrips"/>.
    /// </summary>
    private static TestTiffBuilder StandardRgbaBuilder(bool bigEndian, int width, int height, int compression, int rowsPerStrip) =>
        new TestTiffBuilder(bigEndian)
            .Add(TagImageWidth, TypeLong, (uint)width)
            .Add(TagImageLength, TypeLong, (uint)height)
            .Add(TagBitsPerSample, TypeShort, 8, 8, 8, 8)
            .Add(TagCompression, TypeShort, (uint)compression)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 4)
            .Add(TagRowsPerStrip, TypeLong, (uint)rowsPerStrip)
            .Add(TagPlanarConfiguration, TypeShort, 1)
            .Add(TagExtraSamples, TypeShort, 2);

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

        using var ms923 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms923);

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

        using var ms949 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms949);

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

        using var ms978 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms978);

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

        using var ms1006 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1006);

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

        using var ms1029 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1029);

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

        using var ms1056 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1056);

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

        using var ms1078 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1078);

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

        using var ms1101 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1101);

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

        using var ms1129 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1129);

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

        using var ms1153 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1153);

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

        using var ms1185 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1185);

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

        using var ms1211 = new MemoryStream(file);
        var surface = TiffCodec.Load(ms1211);

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

        using var ms1233 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1233));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream that ends before the 8-byte TIFF header is complete.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_TruncatedHeader_ThrowsInvalidDataException()
    {
        var file = new byte[] { (byte)'I', (byte)'I', 42 };
        using var ms1243 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1243));
    }

    /// <summary>
    ///     Verifies that Load rejects a file whose strip data is shorter than the declared image dimensions require.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_TruncatedStripData_ThrowsInvalidDataException()
    {
        var file = StandardRgbBuilder(false, 4, 4, 1, 4).WithStrips(new byte[] { 1, 2, 3 }).Build();

        using var ms1254 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1254));
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

        using var ms1275 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1275));
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

        using var ms1296 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1296));
    }

    /// <summary>
    ///     Verifies that Load rejects a file with an unsupported compression value.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_UnsupportedCompression_ThrowsInvalidDataException()
    {
        var file = StandardRgbBuilder(false, 1, 1, 6, 1).WithStrips(new byte[] { 1, 2, 3 }).Build();

        using var ms1307 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1307));
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

        using var ms1324 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1324));
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

        using var ms1340 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1340));
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

        using var ms1361 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1361));
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

        using var ms1382 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1382));
    }

    /// <summary>
    ///     Regression test for finding #10: the tiled-TIFF rejection used to live only in
    ///     <see cref="TiffCodec.Load(Stream)"/>, so <see cref="TiffCodec.GetInfo(Stream)"/>'s
    ///     seekable fast path could return a plausible-looking <see cref="ImageInfo"/> for a
    ///     tiled TIFF that <c>Load</c> unconditionally rejects. Verifies the tiled check (now
    ///     shared inside <c>ReadTiffImageInfo</c>) also runs for <c>GetInfo</c>'s seekable path,
    ///     using the identical fixture as <see cref="TiffCodec_Load_TiledTiff_ThrowsInvalidDataException"/>.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_TiledTiff_ThrowsInvalidDataException()
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

        using var ms1408 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1408));
    }

    /// <summary>
    ///     Regression test for finding #7: the seekable-stream TIFF data source used to seek to
    ///     each requested TIFF-file-relative position as an absolute offset from byte 0 of the
    ///     underlying stream, ignoring wherever the caller's stream was actually positioned when
    ///     <see cref="TiffCodec.GetInfo(Stream)"/> was invoked. Builds a stream with a non-empty,
    ///     arbitrary prefix followed by a valid TIFF file, positions the stream past the prefix,
    ///     and asserts GetInfo still returns the correct <see cref="ImageInfo"/> for the TIFF
    ///     bytes that actually follow.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_StreamNotAtPositionZero_ReturnsCorrectImageInfo()
    {
        const int width = 4;
        const int height = 3;
        var tiffBytes = StandardRgbBuilder(bigEndian: false, width, height, compression: 1, rowsPerStrip: height)
            .WithStrips(new byte[width * height * 3])
            .Build();

        var prefix = new byte[37];
        Array.Fill(prefix, (byte)0xAB);

        var combined = new byte[prefix.Length + tiffBytes.Length];
        prefix.CopyTo(combined, 0);
        tiffBytes.CopyTo(combined, prefix.Length);

        using var stream = new MemoryStream(combined);
        stream.Position = prefix.Length;

        var info = TiffCodec.GetInfo(stream);

        Assert.Equal(new ImageInfo(width, height, 3, false), info);
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

        using var ms1506 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1506));
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, channel count, and no-alpha flag
    ///     for a seekable RGB TIFF, without needing to Load (decode) the strip data.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_Rgb_ReturnsExpectedInfoWithoutAlpha()
    {
        var file = StandardRgbBuilder(false, 3, 2, 1, 2)
            .WithStrips(new byte[3 * 3 * 2])
            .Build();

        using var ms1520 = new MemoryStream(file);
        var info = TiffCodec.GetInfo(ms1520);

        Assert.Equal(new ImageInfo(3, 2, 3, false), info);
    }

    /// <summary>
    ///     Proves that GetInfo reports the correct dimensions, channel count, and alpha flag for
    ///     a seekable RGBA TIFF (with an ExtraSamples tag), without needing to Load (decode) the
    ///     strip data.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_Rgba_ReturnsExpectedInfoWithAlpha()
    {
        var file = StandardRgbaBuilder(false, 3, 2, 1, 2)
            .WithStrips(new byte[3 * 4 * 2])
            .Build();

        using var ms1537 = new MemoryStream(file);
        var info = TiffCodec.GetInfo(ms1537);

        Assert.Equal(new ImageInfo(3, 2, 4, true), info);
    }

    /// <summary>
    ///     Proves that GetInfo's seekable probe path leaves the stream positioned well before the
    ///     end of a file with substantial strip data, demonstrating that the strip data itself is
    ///     never read.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_PositionStaysWellBelowFullLength()
    {
        const int width = 100;
        const int height = 100;
        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();
        using var stream = new MemoryStream(file);

        var info = TiffCodec.GetInfo(stream);

        Assert.Equal(new ImageInfo(width, height, 3, false), info);
        Assert.True(stream.Position < stream.Length / 2);
    }

    /// <summary>
    ///     Proves that GetInfo succeeds on a non-seekable stream that carries a completely valid,
    ///     well-formed TIFF image, buffering the whole stream, and that its reported dimensions
    ///     match the dimensions <see cref="TiffCodec.Load(Stream)"/> actually decodes from the
    ///     identical bytes - the GetInfo/Load parity invariant. Uses
    ///     <see cref="FunctionallySeekableButCanSeekFalseStream"/> (which reports
    ///     <c>CanSeek == false</c> but otherwise forwards every member to a fully functional inner
    ///     <see cref="MemoryStream"/>) so the test genuinely exercises the non-seekable buffering
    ///     fallback path rather than incidentally succeeding via some other stream member.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_NonSeekableStream_SucceedsAndMatchesLoadResult()
    {
        // Arrange
        const int width = 3;
        const int height = 2;
        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();
        using var nonSeekableStream = new FunctionallySeekableButCanSeekFalseStream(new MemoryStream(file));
        using var ms1583 = new MemoryStream(file);
        var loadedSurface = TiffCodec.Load(ms1583);

        // Act
        var info = TiffCodec.GetInfo(nonSeekableStream);

        // Assert: GetInfo succeeds without throwing on a non-seekable stream, and its reported
        // dimensions match what Load actually decodes from the identical bytes.
        Assert.Equal(new ImageInfo(width, height, 3, false), info);
        Assert.Equal(loadedSurface.Width, info.Width);
        Assert.Equal(loadedSurface.Height, info.Height);
    }

    /// <summary>
    ///     A test-only stream that reports <see cref="CanSeek"/> as <see langword="false"/> while
    ///     forwarding <see cref="Position"/>, <see cref="Seek"/>, <see cref="Read(byte[], int, int)"/>,
    ///     and <see cref="Length"/> to a fully functional inner stream, used to prove
    ///     <see cref="TiffCodec.GetInfo(Stream)"/>'s non-seekable buffering fallback succeeds on
    ///     genuinely non-seekable input rather than merely happening to work because the stream
    ///     is functionally seekable underneath.
    /// </summary>
    private sealed class FunctionallySeekableButCanSeekFalseStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    ///     Proves that GetInfo(Stream) rejects a null stream with ArgumentNullException.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TiffCodec.GetInfo((Stream)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects a null path with ArgumentNullException.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TiffCodec.GetInfo((string)null!));
    }

    /// <summary>
    ///     Proves that GetInfo(string) rejects an empty path with ArgumentException.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TiffCodec.GetInfo(string.Empty));
    }

    /// <summary>
    ///     Proves that GetInfo rejects a stream with an invalid byte-order mark with
    ///     InvalidDataException, mirroring Load's malformed-header rejection.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_BadByteOrderMark_ThrowsInvalidDataException()
    {
        var bytes = new byte[8];
        bytes[0] = (byte)'X';
        bytes[1] = (byte)'X';

        using var ms1678 = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1678));
    }

    /// <summary>
    ///     Proves that GetInfo does not enforce Surface.MaxDimension - it returns the raw
    ///     oversized header dimensions rather than throwing - while Load on the exact same
    ///     bytes still throws InvalidDataException.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows()
    {
        var width = Surface.MaxDimension + 1;
        var file = StandardRgbBuilder(false, width, 1, 1, 1)
            .WithStrips(new byte[width * 3])
            .Build();

        using var ms1694 = new MemoryStream(file);
        var info = TiffCodec.GetInfo(ms1694);
        Assert.Equal(width, info.Width);

        using var ms1697 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1697));
    }

    /// <summary>
    ///     Proves that GetInfo (seekable path) succeeds on a file with a valid header/IFD but
    ///     truncated (missing) strip data - which GetInfo never reads - while Load on the same
    ///     bytes still throws.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_SucceedsWithTruncatedStripData_ButLoadThrows()
    {
        const int width = 4;
        const int height = 4;
        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();

        // Truncate away the trailing strip data entirely, leaving the header and IFD intact
        var truncated = file[..(file.Length - (width * height * 3))];

        using var ms1717 = new MemoryStream(truncated);
        var info = TiffCodec.GetInfo(ms1717);
        Assert.Equal(new ImageInfo(width, height, 3, false), info);

        using var ms1720 = new MemoryStream(truncated);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1720));
    }

    /// <summary>
    ///     Regression test for the historical architectural divergence between GetInfo's seekable
    ///     and (formerly-existing) non-seekable code paths: when SamplesPerPixel is absent, the
    ///     seekable path used to default it to 1 while the non-seekable fallback (reusing Load's
    ///     parser) correctly defaulted it to BitsPerSample's entry count. Proves the seekable path
    ///     now defaults Channels to BitsPerSample's entry count (3), not 1.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_SamplesPerPixelTagOmitted_DefaultsToBitsPerSampleCount()
    {
        // Arrange: build a valid RGB TIFF (BitsPerSample = 8,8,8) with no SamplesPerPixel tag
        const int width = 3;
        const int height = 2;
        var file = StandardRgbBuilderWithoutSamplesPerPixel(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();

        // Act
        using var ms1741 = new MemoryStream(file);
        var seekableInfo = TiffCodec.GetInfo(ms1741);

        // Assert: Channels defaults to BitsPerSample's entry count (3)
        var expected = new ImageInfo(width, height, 3, false);
        Assert.Equal(expected, seekableInfo);
    }

    /// <summary>
    ///     Regression test for the historical architectural divergence between GetInfo's seekable
    ///     and (formerly-existing) non-seekable code paths: the seekable path used to skip most of
    ///     ReadTiffImageInfo's format-support validation (including the BitsPerSample == 8 check),
    ///     so a file rejected by the non-seekable fallback (and by Load) could still succeed on
    ///     the seekable path. Proves the seekable path now throws InvalidDataException for an
    ///     unsupported bit depth.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_UnsupportedBitsPerSample_ThrowsInvalidDataException()
    {
        // Arrange: build an otherwise-valid RGB TIFF declaring 16 bits per sample (unsupported)
        const int width = 3;
        const int height = 2;
        var builder = new TestTiffBuilder(false)
            .Add(TagImageWidth, TypeLong, (uint)width)
            .Add(TagImageLength, TypeLong, (uint)height)
            .Add(TagBitsPerSample, TypeShort, 16, 16, 16)
            .Add(TagCompression, TypeShort, 1)
            .Add(TagPhotometricInterpretation, TypeShort, 2)
            .Add(TagSamplesPerPixel, TypeShort, 3)
            .Add(TagRowsPerStrip, TypeLong, (uint)height)
            .Add(TagPlanarConfiguration, TypeShort, 1);
        var file = builder.WithStrips(new byte[width * height * 3 * 2]).Build();

        // Act / Assert: the seekable stream rejects the file
        using var ms1774 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1774));
    }

    /// <summary>
    ///     Regression test for a memory-exhaustion vulnerability in the internal seekable-stream
    ///     TIFF data source used by <see cref="TiffCodec.GetInfo(Stream)"/>'s fast path: it used to allocate
    ///     <c>new byte[length]</c> for an out-of-line tag value array before validating
    ///     <c>length</c> against the stream's actual size, so a tiny, mostly-garbage TIFF file
    ///     declaring an attacker-controlled tag <c>Count</c> in the hundreds of millions could
    ///     force a huge up-front allocation on the very fast path <see cref="TiffCodec.GetInfo(Stream)"/>
    ///     exists to make safe for untrusted input. Builds a 66-byte file whose
    ///     <c>BitsPerSample</c> entry declares 900,000,000 out-of-line SHORT values (~1.8 GB) at
    ///     an offset that only has 16 real trailing bytes, and proves (by measuring actual bytes
    ///     allocated, not wall-clock time) that the count is rejected (now by the
    ///     <c>BitsPerSample</c> cardinality cap, before even the out-of-range offset/length
    ///     bounds check runs) rather than merely failing quickly after allocating.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_MaliciousOutOfLineTagLength_ThrowsWithoutUnboundedAllocation()
    {
        const uint maliciousCount = 900_000_000;
        var file = new byte[66];

        // Header: little-endian ("II"), magic 42, first IFD at offset 8
        file[0] = (byte)'I';
        file[1] = (byte)'I';
        file[2] = 42;
        file[4] = 8;

        // IFD at offset 8: entry count = 3, followed by 3 x 12-byte entries, then a 4-byte
        // next-IFD offset (left as 0)
        file[8] = 3;

        WriteMaliciousEntry(file, 0, TagImageWidth, TypeLong, 1, 1);
        WriteMaliciousEntry(file, 1, TagImageLength, TypeLong, 1, 1);
        WriteMaliciousEntry(file, 2, TagBitsPerSample, TypeShort, maliciousCount, 50);

        // Measure actual bytes allocated by the call, not wall-clock time: a modern allocator can
        // zero and hand back a ~1.8 GB buffer in single-digit milliseconds, and the resulting
        // InvalidDataException's message text is identical whether it comes from the (fixed)
        // bounds check or from the pre-existing end-of-stream check inside the (buggy) attempted
        // read - so neither timing nor message content reliably distinguishes the vulnerable code
        // from the fixed code. The allocated-byte count does: the bug allocates the full
        // ~1.8 GB claimed by the malicious Count before discovering the stream is too short.
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        using var ms1819 = new MemoryStream(file);
        var ex = Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1819));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Contains("implausibly large declared value count", ex.Message);

        // The fixed code rejects the range against stream.Length before allocating anything
        // beyond small, fixed-size working buffers; the vulnerable code allocates ~1.8 GB
        // (900,000,000 SHORTs * 2 bytes) before failing. 1 MB is a generous margin above any
        // legitimate fixed-size buffer this call path could use.
        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Regression test for finding #2: a mandatory tag with a declared value <c>Count</c> of
    ///     0 used to flow through <c>ReadTagValues</c> as an empty array, then throw
    ///     <see cref="IndexOutOfRangeException"/> (not the documented
    ///     <see cref="InvalidDataException"/>) the moment the caller indexed <c>[0]</c> into it.
    ///     Builds a minimal single-entry IFD declaring <c>ImageWidth</c> with <c>Count = 0</c>
    ///     and verifies all three callers (<see cref="TiffCodec.Load(Stream)"/> and both
    ///     <see cref="TiffCodec.GetInfo(Stream)"/> paths) now reject it with
    ///     <see cref="InvalidDataException"/> specifically.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_ImageWidthZeroCount_ThrowsInvalidDataExceptionNotIndexOutOfRange()
    {
        var file = BuildFileWithZeroCountImageWidth();

        using var ms1849 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1849));
    }

    /// <summary>
    ///     Same as <see cref="TiffCodec_Load_ImageWidthZeroCount_ThrowsInvalidDataExceptionNotIndexOutOfRange"/>,
    ///     for <see cref="TiffCodec.GetInfo(Stream)"/>'s seekable fast path.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_ImageWidthZeroCount_ThrowsInvalidDataExceptionNotIndexOutOfRange()
    {
        var file = BuildFileWithZeroCountImageWidth();

        using var ms1861 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1861));
    }

    /// <summary>
    ///     Builds a minimal (26-byte) TIFF file whose only IFD entry is <c>ImageWidth</c> with a
    ///     declared value <c>Count</c> of 0, used only by the finding #2 zero-count regression
    ///     tests above.
    /// </summary>
    private static byte[] BuildFileWithZeroCountImageWidth()
    {
        var file = new byte[8 + 2 + 12 + 4];
        file[0] = (byte)'I';
        file[1] = (byte)'I';
        file[2] = 42;
        file[4] = 8;
        file[8] = 1; // entry count = 1

        WriteMaliciousEntry(file, 0, TagImageWidth, TypeLong, count: 0, valueOrOffset: 1);

        return file;
    }

    /// <summary>
    ///     Regression test for finding #2: a TIFF header declaring an IFD offset above
    ///     <see cref="int.MaxValue"/> used to be cast via an unguarded <c>checked((int)...)</c>,
    ///     letting an <see cref="OverflowException"/> (not the documented
    ///     <see cref="InvalidDataException"/>) escape from <c>ParseIfd</c>. Verifies all three
    ///     callers now reject it with <see cref="InvalidDataException"/> specifically.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_IfdOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        var file = BuildHeaderOnlyFileWithIfdOffset(3_000_000_000);

        using var ms1895 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1895));
    }

    /// <summary>
    ///     Same as <see cref="TiffCodec_Load_IfdOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException"/>,
    ///     for <see cref="TiffCodec.GetInfo(Stream)"/>'s seekable fast path.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_IfdOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        var file = BuildHeaderOnlyFileWithIfdOffset(3_000_000_000);

        using var ms1907 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1907));
    }

    /// <summary>
    ///     Builds an 8-byte TIFF header (no IFD data at all) declaring the given IFD offset, used
    ///     only by the finding #2 IFD-offset-overflow regression tests above.
    /// </summary>
    private static byte[] BuildHeaderOnlyFileWithIfdOffset(uint ifdOffset)
    {
        var file = new byte[8];
        file[0] = (byte)'I';
        file[1] = (byte)'I';
        file[2] = 42;
        file[4] = (byte)ifdOffset;
        file[5] = (byte)(ifdOffset >> 8);
        file[6] = (byte)(ifdOffset >> 16);
        file[7] = (byte)(ifdOffset >> 24);
        return file;
    }

    /// <summary>
    ///     Regression test for finding #2: an out-of-line tag value array's stored offset field
    ///     (a raw <c>uint</c> read directly from the file) above <see cref="int.MaxValue"/> used
    ///     to be cast via an unguarded <c>checked((int)...)</c> in <c>ReadTagValues</c>, letting
    ///     an <see cref="OverflowException"/> escape instead of the documented
    ///     <see cref="InvalidDataException"/>. Verifies all three callers now reject it with
    ///     <see cref="InvalidDataException"/> specifically.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_OutOfLineTagOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        var file = BuildFileWithBitsPerSampleOverride(count: 3, valueOrOffset: 3_000_000_000);

        using var ms1940 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms1940));
    }

    /// <summary>
    ///     Same as <see cref="TiffCodec_Load_OutOfLineTagOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException"/>,
    ///     for <see cref="TiffCodec.GetInfo(Stream)"/>'s seekable fast path.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_OutOfLineTagOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        var file = BuildFileWithBitsPerSampleOverride(count: 3, valueOrOffset: 3_000_000_000);

        using var ms1952 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms1952));
    }

    /// <summary>
    ///     Builds a minimal (50-byte) TIFF file with valid <c>ImageWidth</c>/<c>ImageLength</c>
    ///     entries (so control reaches <c>BitsPerSample</c> processing) and a <c>BitsPerSample</c>
    ///     entry with the given declared <c>Count</c> and value-or-offset field, used by both the
    ///     finding #2 (offset overflow) and finding #3 (cardinality cap) regression tests above/
    ///     below.
    /// </summary>
    private static byte[] BuildFileWithBitsPerSampleOverride(uint count, uint valueOrOffset, ushort type = TypeShort)
    {
        var file = new byte[50];
        file[0] = (byte)'I';
        file[1] = (byte)'I';
        file[2] = 42;
        file[4] = 8;
        file[8] = 3; // entry count = 3

        WriteMaliciousEntry(file, 0, TagImageWidth, TypeLong, count: 1, valueOrOffset: 1);
        WriteMaliciousEntry(file, 1, TagImageLength, TypeLong, count: 1, valueOrOffset: 1);
        WriteMaliciousEntry(file, 2, TagBitsPerSample, type, count, valueOrOffset);

        return file;
    }

    /// <summary>
    ///     Regression test for finding #3: even after finding #2's stream-bounds fix, a tag's
    ///     declared <c>Count</c> had no upper-bound sanity check, so a genuinely large (but
    ///     otherwise valid, bounds-check-satisfying) TIFF file declaring an implausibly large
    ///     <c>BitsPerSample</c> count (this codec never resolves more than 4 <c>BitsPerSample</c>
    ///     values) could still force a large, count-proportional <c>uint[]</c> allocation on
    ///     <see cref="TiffCodec.GetInfo(Stream)"/>'s seekable fast path - defeating the "cheap
    ///     triage of untrusted input" purpose <c>GetInfo</c> exists for. Builds a real ~4 MB file
    ///     (so the pre-existing stream-bounds check alone would <em>not</em> reject it) declaring
    ///     2,000,000 <c>BitsPerSample</c> values, and proves (by measuring actual bytes
    ///     allocated, not wall-clock time) that the new cardinality cap rejects it before the
    ///     large <c>uint[]</c> allocation.
    /// </summary>
    [Fact]
    public void TiffCodec_GetInfo_Seekable_BitsPerSampleCountImplausiblyLarge_ThrowsWithoutLargeAllocation()
    {
        const uint bitsPerSampleCount = 2_000_000;
        const int ifdEndOffset = 50;
        var file = new byte[ifdEndOffset + (bitsPerSampleCount * 2)];

        file[0] = (byte)'I';
        file[1] = (byte)'I';
        file[2] = 42;
        file[4] = 8;
        file[8] = 3; // entry count = 3

        WriteMaliciousEntry(file, 0, TagImageWidth, TypeLong, count: 1, valueOrOffset: 1);
        WriteMaliciousEntry(file, 1, TagImageLength, TypeLong, count: 1, valueOrOffset: 1);
        WriteMaliciousEntry(file, 2, TagBitsPerSample, TypeShort, bitsPerSampleCount, (uint)ifdEndOffset);

        // This file is large enough (~4 MB) that offset + declared length <= file.Length holds,
        // so the pre-existing stream-bounds check (finding #2/#7's prior-round fix) alone would
        // not reject it - only the new cardinality cap can.
        Assert.True(ifdEndOffset + (bitsPerSampleCount * 2) <= file.Length);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        using var ms2014 = new MemoryStream(file);
        var ex = Assert.Throws<InvalidDataException>(() => TiffCodec.GetInfo(ms2014));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Contains("implausibly large declared value count", ex.Message);

        const long maxExpectedAllocatedBytes = 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Regression test for a gap the code-review pass found in the finding #2 fix: unlike
    ///     every other attacker-controlled TIFF position/length value, <c>DecodeStrip</c>'s
    ///     <c>StripOffsets</c>/<c>StripByteCounts</c> handling still cast those raw <c>uint</c>
    ///     tag values via a bare <c>checked((int)...)</c>, so a file declaring a
    ///     <c>StripOffsets</c> value above <see cref="int.MaxValue"/> let an
    ///     <see cref="OverflowException"/> escape from <see cref="TiffCodec.Load(Stream)"/>
    ///     instead of the documented <see cref="InvalidDataException"/>. Only <c>Load</c> ever
    ///     reaches <c>DecodeStrip</c> (neither <c>GetInfo</c> path resolves strip tags), so this
    ///     is a <c>Load</c>-only regression test.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_StripOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        const int width = 2;
        const int height = 2;
        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();

        // StandardRgbBuilder's 8 tags (256, 257, 258, 259, 262, 277, 278, 284) plus the
        // StripOffsets (273) and StripByteCounts (279) tags WithStrips adds sort ascending to
        // 256, 257, 258, 259, 262, 273, 277, 278, 279, 284 - StripOffsets is entry index 5. With
        // a single strip, its Count is 1, so its 4-byte LONG value fits inline in the entry
        // itself; overwriting it in place does not disturb the file's layout.
        PatchInlineTagEntryValue(file, entryIndex: 5, value: 3_000_000_000);

        using var ms2052 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms2052));
    }

    /// <summary>
    ///     Same as <see cref="TiffCodec_Load_StripOffsetAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException"/>,
    ///     but for the <c>StripByteCounts</c> tag (entry index 8; see that test for the sorted
    ///     entry-index derivation) instead of <c>StripOffsets</c>.
    /// </summary>
    [Fact]
    public void TiffCodec_Load_StripByteCountAboveInt32Range_ThrowsInvalidDataExceptionNotOverflowException()
    {
        const int width = 2;
        const int height = 2;
        var file = StandardRgbBuilder(false, width, height, 1, height)
            .WithStrips(new byte[width * height * 3])
            .Build();

        PatchInlineTagEntryValue(file, entryIndex: 8, value: 3_000_000_000);

        using var ms2071 = new MemoryStream(file);
        Assert.Throws<InvalidDataException>(() => TiffCodec.Load(ms2071));
    }

    /// <summary>
    ///     Overwrites the inline 4-byte value field of the IFD entry at the given zero-based entry
    ///     index (within the IFD starting at offset 8) in little-endian byte order, leaving the
    ///     entry's tag/type/count untouched. Only valid when that entry's value is known to fit
    ///     inline (total size &lt;= 4 bytes); used by the strip-offset/strip-byte-count overflow
    ///     regression tests above to substitute an out-of-range value into an otherwise valid,
    ///     builder-produced file without needing to hand-craft the whole file.
    /// </summary>
    private static void PatchInlineTagEntryValue(byte[] file, int entryIndex, uint value)
    {
        var pos = 10 + (entryIndex * 12) + 8;
        file[pos] = (byte)value;
        file[pos + 1] = (byte)(value >> 8);
        file[pos + 2] = (byte)(value >> 16);
        file[pos + 3] = (byte)(value >> 24);
    }

    /// <summary>
    ///     Writes one raw 12-byte IFD entry (tag, type, count, value-or-offset) directly into
    ///     <paramref name="file"/> at the given zero-based entry index within the IFD that starts
    ///     at offset 8, in little-endian byte order. Used only by
    ///     <see cref="TiffCodec_GetInfo_Seekable_MaliciousOutOfLineTagLength_ThrowsWithoutUnboundedAllocation"/>
    ///     to hand-craft a malformed entry that <see cref="TestTiffBuilder"/> cannot express (a
    ///     declared <c>Count</c> that deliberately does not match the number of real trailing
    ///     bytes available).
    /// </summary>
    private static void WriteMaliciousEntry(byte[] file, int entryIndex, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        var pos = 10 + (entryIndex * 12);
        file[pos] = (byte)tag;
        file[pos + 1] = (byte)(tag >> 8);
        file[pos + 2] = (byte)type;
        file[pos + 3] = (byte)(type >> 8);
        file[pos + 4] = (byte)count;
        file[pos + 5] = (byte)(count >> 8);
        file[pos + 6] = (byte)(count >> 16);
        file[pos + 7] = (byte)(count >> 24);
        file[pos + 8] = (byte)valueOrOffset;
        file[pos + 9] = (byte)(valueOrOffset >> 8);
        file[pos + 10] = (byte)(valueOrOffset >> 16);
        file[pos + 11] = (byte)(valueOrOffset >> 24);
    }
}
