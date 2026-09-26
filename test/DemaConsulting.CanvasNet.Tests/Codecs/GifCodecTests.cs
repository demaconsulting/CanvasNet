using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the GifCodec class, using hand-built byte-array GIF streams to exercise
///     argument validation, malformed/unsupported input handling, and correctness for
///     scenarios (interlacing, transparency, sub-region blitting, multi-frame tolerance) that no
///     real-world fixture in <c>GifFixtures</c> exercises. See <see cref="GifFixtureTests"/> for
///     conformance tests against the real fixture corpus.
/// </summary>
public class GifCodecTests
{
    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit value to a stream.
    /// </summary>
    private static void WriteU16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    /// <summary>
    ///     Computes the 3-bit color table size field such that <c>2 &lt;&lt; field == entryCount</c>.
    /// </summary>
    private static byte ColorTableSizeField(int entryCount)
    {
        var field = 0;
        while (2 << field < entryCount)
        {
            field++;
        }

        return (byte)field;
    }

    /// <summary>
    ///     Builds a 3-byte-per-entry RGB color table from a list of (R, G, B) tuples.
    /// </summary>
    private static byte[] BuildColorTable(params (byte R, byte G, byte B)[] colors)
    {
        using var stream = new MemoryStream();
        foreach (var (r, g, b) in colors)
        {
            stream.WriteByte(r);
            stream.WriteByte(g);
            stream.WriteByte(b);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Writes the 6-byte signature and 7-byte Logical Screen Descriptor, followed by the
    ///     Global Color Table (if any), to a stream.
    /// </summary>
    private static void WriteHeader(
        Stream stream,
        int width,
        int height,
        byte[]? globalColorTable,
        string signature = "GIF89a")
    {
        stream.Write(Encoding.ASCII.GetBytes(signature));
        WriteU16(stream, width);
        WriteU16(stream, height);

        byte packed = 0;
        if (globalColorTable is not null)
        {
            packed = (byte)(0x80 | ColorTableSizeField(globalColorTable.Length / 3));
        }

        stream.WriteByte(packed);
        stream.WriteByte(0); // background color index (ignored)
        stream.WriteByte(0); // pixel aspect ratio (ignored)

        if (globalColorTable is not null)
        {
            stream.Write(globalColorTable);
        }
    }

    /// <summary>
    ///     Writes a chain of length-prefixed sub-blocks (max 255 bytes each), terminated by a
    ///     zero-length sub-block.
    /// </summary>
    private static void WriteSubBlocks(Stream stream, byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var chunkSize = Math.Min(255, data.Length - offset);
            stream.WriteByte((byte)chunkSize);
            stream.Write(data, offset, chunkSize);
            offset += chunkSize;
        }

        stream.WriteByte(0);
    }

    /// <summary>
    ///     Writes a Graphic Control Extension (<c>0x21 0xF9</c>) with the given transparency flag
    ///     and transparent color index, deliberately using a different value for the (ignored)
    ///     Delay Time low byte so a test can distinguish reading the correct offset (byte 3) from
    ///     the incorrect one (byte 1).
    /// </summary>
    private static void WriteGraphicControlExtension(
        Stream stream,
        bool transparencyFlag,
        byte transparentColorIndex,
        ushort delayTime = 0)
    {
        stream.WriteByte(0x21);
        stream.WriteByte(0xF9);
        stream.WriteByte(4);
        stream.WriteByte((byte)(transparencyFlag ? 0x01 : 0x00));
        WriteU16(stream, delayTime);
        stream.WriteByte(transparentColorIndex);
        stream.WriteByte(0);
    }

    /// <summary>
    ///     Writes an Image Descriptor (<c>0x2C</c>), any local color table, the LZW minimum code
    ///     size byte, and the LZW-compressed <paramref name="indices"/> as a sub-block chain.
    /// </summary>
    private static void WriteImageDescriptor(
        Stream stream,
        int left,
        int top,
        int width,
        int height,
        bool interlace,
        byte[]? localColorTable,
        int minCodeSize,
        byte[] indices)
    {
        stream.WriteByte(0x2C);
        WriteU16(stream, left);
        WriteU16(stream, top);
        WriteU16(stream, width);
        WriteU16(stream, height);

        byte packed = 0;
        if (localColorTable is not null)
        {
            packed |= (byte)(0x80 | ColorTableSizeField(localColorTable.Length / 3));
        }

        if (interlace)
        {
            packed |= 0x40;
        }

        stream.WriteByte(packed);

        if (localColorTable is not null)
        {
            stream.Write(localColorTable);
        }

        stream.WriteByte((byte)minCodeSize);
        WriteSubBlocks(stream, EncodeGifLzw(minCodeSize, indices));
    }

    /// <summary>
    ///     Writes the GIF Trailer (<c>0x3B</c>).
    /// </summary>
    private static void WriteTrailer(Stream stream) => stream.WriteByte(0x3B);

    /// <summary>
    ///     Re-orders a normal, top-to-bottom row-major array of palette indices into GIF's 4-pass
    ///     interlace row order, the inverse of <c>GifCodec</c>'s own de-interlace step - used to
    ///     build a hand-crafted interlaced test fixture whose *decoded* (post-de-interlace) pixels
    ///     are known in advance.
    /// </summary>
    private static byte[] ToInterlacedOrder(byte[] normalOrder, int width, int height)
    {
        var result = new byte[normalOrder.Length];
        var destRow = 0;

        void CopyPass(int start, int step)
        {
            for (var row = start; row < height; row += step)
            {
                Array.Copy(normalOrder, row * width, result, destRow * width, width);
                destRow++;
            }
        }

        CopyPass(0, 8);
        CopyPass(4, 8);
        CopyPass(2, 4);
        CopyPass(1, 2);

        return result;
    }

    /// <summary>
    ///     Encodes a sequence of palette-index bytes as a GIF-flavor LZW stream (variable-width
    ///     codes packed least-significant-bit-first) that always emits literal codes (never a
    ///     back-reference), while still tracking the same code-table growth <c>GifCodec</c>'s own
    ///     decoder tracks, so the two stay bit-for-bit compatible. This is a valid, if
    ///     unoptimized, encoding - real GIF encoders normally emit back-references for
    ///     compression, but a decoder must accept literal-only streams too.
    /// </summary>
    private static byte[] EncodeGifLzw(int minCodeSize, byte[] indices)
    {
        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var firstAvailableCode = eoiCode + 1;

        var writer = new LzwBitWriter();
        var codeSize = minCodeSize + 1;
        var tableCount = 0;
        var haveIndex = false;

        writer.WriteCode(clearCode, codeSize);
        foreach (var index in indices)
        {
            writer.WriteCode(index, codeSize);

            if (haveIndex && firstAvailableCode + tableCount < 4096)
            {
                tableCount++;
                var nextCode = firstAvailableCode + tableCount;
                if (nextCode == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }

            haveIndex = true;
        }

        writer.WriteCode(eoiCode, codeSize);
        return writer.ToArray();
    }

    /// <summary>
    ///     Identical to <see cref="EncodeGifLzw"/> except it never writes the final
    ///     End-of-Information code, used to build a fixture that decodes exactly the expected
    ///     number of palette-index bytes but omits the terminating EOI code entirely.
    /// </summary>
    private static byte[] EncodeGifLzwWithoutEoi(int minCodeSize, byte[] indices)
    {
        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var firstAvailableCode = eoiCode + 1;

        var writer = new LzwBitWriter();
        var codeSize = minCodeSize + 1;
        var tableCount = 0;
        var haveIndex = false;

        writer.WriteCode(clearCode, codeSize);
        foreach (var index in indices)
        {
            writer.WriteCode(index, codeSize);

            if (haveIndex && firstAvailableCode + tableCount < 4096)
            {
                tableCount++;
                var nextCode = firstAvailableCode + tableCount;
                if (nextCode == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }

            haveIndex = true;
        }

        return writer.ToArray();
    }

    /// <summary>
    ///     Packs variable-width codes into bytes least-significant-bit-first, the same bit order
    ///     <c>GifCodec</c>'s own private LZW bit reader requires.
    /// </summary>
    private sealed class LzwBitWriter
    {
        private readonly List<byte> _bytes = [];
        private uint _bitBuffer;
        private int _bitCount;

        public void WriteCode(int code, int bits)
        {
            _bitBuffer |= (uint)code << _bitCount;
            _bitCount += bits;
            while (_bitCount >= 8)
            {
                _bytes.Add((byte)(_bitBuffer & 0xFF));
                _bitBuffer >>= 8;
                _bitCount -= 8;
            }
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                _bytes.Add((byte)(_bitBuffer & 0xFF));
            }

            return [.. _bytes];
        }
    }

    // ---------------------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------------------

    /// <summary>Load(Stream) rejects a null stream with ArgumentNullException.</summary>
    [Fact]
    public void GifCodec_Load_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GifCodec.Load((Stream)null!));
    }

    /// <summary>Load(string) rejects a null path with ArgumentNullException.</summary>
    [Fact]
    public void GifCodec_Load_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GifCodec.Load((string)null!));
    }

    /// <summary>Load(string) rejects an empty path with ArgumentException.</summary>
    [Fact]
    public void GifCodec_Load_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GifCodec.Load(string.Empty));
    }

    /// <summary>GetInfo(Stream) rejects a null stream with ArgumentNullException.</summary>
    [Fact]
    public void GifCodec_GetInfo_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GifCodec.GetInfo((Stream)null!));
    }

    /// <summary>GetInfo(string) rejects a null path with ArgumentNullException.</summary>
    [Fact]
    public void GifCodec_GetInfo_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GifCodec.GetInfo((string)null!));
    }

    /// <summary>GetInfo(string) rejects an empty path with ArgumentException.</summary>
    [Fact]
    public void GifCodec_GetInfo_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GifCodec.GetInfo(string.Empty));
    }

    // ---------------------------------------------------------------------------------------
    // Signature validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Load rejects a stream not beginning with "GIF87a"/"GIF89a".</summary>
    [Fact]
    public void GifCodec_Load_BadSignature_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 1, null, "NOTAGIF");
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>GetInfo rejects a stream not beginning with "GIF87a"/"GIF89a".</summary>
    [Fact]
    public void GifCodec_GetInfo_BadSignature_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 1, null, "NOTAGIF");
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }

    // ---------------------------------------------------------------------------------------
    // Dimension validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, Surface.MaxDimension + 1, 1, null);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, Surface.MaxDimension + 1, null);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows.</summary>
    [Fact]
    public void GifCodec_GetInfo_OversizedDimensions_ReturnsRawValue_ButLoadThrows()
    {
        var oversized = Surface.MaxDimension + 1;
        using var stream = new MemoryStream();
        var lct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, oversized, oversized, null);
        WriteImageDescriptor(stream, 0, 0, 1, 1, false, lct, 2, [0]);
        WriteTrailer(stream);
        var bytes = stream.ToArray();

        using var infoStream = new MemoryStream(bytes);
        var info = GifCodec.GetInfo(infoStream);
        Assert.Equal(oversized, info.Width);
        Assert.Equal(oversized, info.Height);
        Assert.Equal(1, info.FrameCount);

        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => GifCodec.Load(loadStream));
    }

    /// <summary>Test: GifCodec_GetInfo_ZeroWidth_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_GetInfo_ZeroWidth_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 0, 1, null);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }

    /// <summary>Test: GifCodec_GetInfo_ZeroHeight_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_GetInfo_ZeroHeight_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 0, null);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }

    // ---------------------------------------------------------------------------------------
    // Block-structure / color-table validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_NoColorTable_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_NoColorTable_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 2, 2, null);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_SecondFrameMissingColorTable_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_SecondFrameMissingColorTable_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var lct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 2, null); // no Global Color Table at all

        // First frame supplies its own Local Color Table, so it decodes successfully...
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, lct, 2, [0, 0, 0, 0]);

        // ...but the second frame has neither a Local Color Table nor a Global Color Table to
        // fall back on, which is structurally malformed even though this codec never decodes its
        // pixel data (only the first frame is ever decoded).
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_SecondFrameOutOfRangeMinCodeSize_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_SecondFrameOutOfRangeMinCodeSize_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 2, gct);

        // First frame uses a valid minimum code size, so it decodes successfully...
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);

        // ...but the second frame declares a minimum code size of 9, outside the valid 2-8 range
        // enforced by DecodeGifLzw, which is structurally malformed even though this codec never
        // decodes this frame's pixel data (only the first frame is ever decoded).
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 9, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_SecondFrameZeroMinCodeSize_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_SecondFrameZeroMinCodeSize_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 2, gct);

        // First frame uses a valid minimum code size, so it decodes successfully...
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);

        // ...but the second frame declares a minimum code size of 0, outside the valid 2-8 range.
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 0, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_MalformedGraphicControlExtension_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_MalformedGraphicControlExtension_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 2, 2, BuildColorTable((255, 0, 0), (0, 0, 255)));

        // A Graphic Control Extension whose data is only 3 bytes, not the required 4
        stream.WriteByte(0x21);
        stream.WriteByte(0xF9);
        stream.WriteByte(3);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0);

        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>
    ///     Test: GifCodec_Load_FragmentedGraphicControlExtension_ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GifCodec_Load_FragmentedGraphicControlExtension_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 2, 2, BuildColorTable((255, 0, 0), (0, 0, 255)));

        // A Graphic Control Extension split across two sub-blocks (2 bytes, then 2 more bytes)
        // that together total the correct 4 bytes of data. The GIF89a specification requires the
        // Graphic Control Extension's data to be exactly one 4-byte sub-block followed by the
        // block terminator; this fragmentation - while it would reassemble to the right byte
        // count under a naive concatenating reader - is not spec-compliant and must be rejected.
        stream.WriteByte(0x21);
        stream.WriteByte(0xF9);
        stream.WriteByte(2); // first sub-block: 2 bytes
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(2); // second sub-block: 2 more bytes
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0); // block terminator

        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_UnexpectedBlockIntroducer_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_UnexpectedBlockIntroducer_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 2, 2, BuildColorTable((255, 0, 0), (0, 0, 255)));
        stream.WriteByte(0x99); // not 0x21, 0x2C, or 0x3B
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_TrailingDataAfterTrailer_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_TrailingDataAfterTrailer_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 1, 1, gct);
        WriteImageDescriptor(stream, 0, 0, 1, 1, false, null, 2, [0]);
        WriteTrailer(stream);
        stream.WriteByte(0xFF); // trailing garbage
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_NoImageDescriptor_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_NoImageDescriptor_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 2, 2, BuildColorTable((255, 0, 0), (0, 0, 255)));
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_ImageDescriptorOutOfBounds_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_ImageDescriptorOutOfBounds_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 4, 4, gct);
        // left(2) + width(4) = 6 > canvas width 4
        WriteImageDescriptor(stream, 2, 2, 4, 4, false, null, 2, new byte[16]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    // ---------------------------------------------------------------------------------------
    // Truncated-stream validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_TruncatedAtSignature_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_TruncatedAtSignature_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream("GIF"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_TruncatedAtColorTable_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_TruncatedAtColorTable_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        stream.Write("GIF89a"u8);
        WriteU16(stream, 2);
        WriteU16(stream, 2);
        stream.WriteByte((byte)(0x80 | ColorTableSizeField(4))); // declares a 4-entry global color table
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.Write([1, 2, 3]); // only 3 of the required 12 color-table bytes
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_TruncatedAtSubBlock_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_TruncatedAtSubBlock_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 1, 1, gct);
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 1);
        WriteU16(stream, 1);
        stream.WriteByte(0); // no local color table, not interlaced
        stream.WriteByte(2); // LZW minimum code size
        stream.WriteByte(10); // sub-block declares 10 bytes...
        stream.Write([1, 2, 3]); // ...but only 3 are actually present, and no terminator follows
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    // ---------------------------------------------------------------------------------------
    // Sub-block budget
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_ExcessiveSubBlockData_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_ExcessiveSubBlockData_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 1, null); // tiny declared dimensions

        // A single Comment Extension (label 0xFE) whose sub-block chain's cumulative declared
        // size is one byte more than GifCodec.MaxTotalSubBlockBytes. Content is irrelevant - only
        // total volume matters - so a single reusable zero-filled 255-byte buffer is written
        // repeatedly rather than allocating one huge array up front.
        stream.WriteByte(0x21); // Extension Introducer
        stream.WriteByte(0xFE); // Comment Extension label

        var remaining = GifCodec.MaxTotalSubBlockBytes + 1;
        var chunk = new byte[255];
        while (remaining > 0)
        {
            var chunkSize = (int)Math.Min(255, remaining);
            stream.WriteByte((byte)chunkSize);
            stream.Write(chunk, 0, chunkSize);
            remaining -= chunkSize;
        }

        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>
    ///     Test: GifCodec_Load_GraphicControlExtensionExceedsRemainingSubBlockBudget_ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GifCodec_Load_GraphicControlExtensionExceedsRemainingSubBlockBudget_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 1, null); // tiny declared dimensions

        // A preceding Comment Extension consumes all but 2 bytes of the shared sub-block budget,
        // leaving too little remaining for a subsequent Graphic Control Extension's required 4
        // data bytes. This proves ReadGraphicControlExtensionData checks the shared budget before
        // decrementing it - like ReadSubBlocks does - rather than letting the budget go negative
        // and silently accepting the GCE (which would let repeated GCEs bypass the total
        // sub-block bound entirely).
        stream.WriteByte(0x21); // Extension Introducer
        stream.WriteByte(0xFE); // Comment Extension label

        var remaining = GifCodec.MaxTotalSubBlockBytes - 2;
        var chunk = new byte[255];
        while (remaining > 0)
        {
            var chunkSize = (int)Math.Min(255, remaining);
            stream.WriteByte((byte)chunkSize);
            stream.Write(chunk, 0, chunkSize);
            remaining -= chunkSize;
        }

        stream.WriteByte(0); // Comment Extension block terminator

        // A Graphic Control Extension declaring its (otherwise valid) 4-byte data sub-block -
        // which now exceeds the 2 bytes remaining in the shared budget.
        stream.WriteByte(0x21); // Extension Introducer
        stream.WriteByte(0xF9); // Graphic Control Extension label
        stream.WriteByte(4);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0);
        stream.WriteByte(0); // block terminator

        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    // ---------------------------------------------------------------------------------------
    // LZW validation
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_InvalidLzwCode_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_InvalidLzwCode_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 1, 1, gct);
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 1);
        WriteU16(stream, 1);
        stream.WriteByte(0);
        stream.WriteByte(2); // minCodeSize=2 => clearCode=4, eoiCode=5, firstAvailableCode=6

        // Clear code (4) followed immediately by code 6, which is invalid because no table
        // entries exist yet and there is no previous entry for KwKwK reconstruction
        var writer = new LzwBitWriter();
        writer.WriteCode(4, 3);
        writer.WriteCode(6, 3);
        WriteSubBlocks(stream, writer.ToArray());
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_InsufficientLzwOutput_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_InsufficientLzwOutput_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255), (0, 255, 0), (255, 255, 0));
        WriteHeader(stream, 2, 2, gct);
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 2);
        WriteU16(stream, 2); // 4 pixels expected
        stream.WriteByte(0);
        stream.WriteByte(2);

        // Clear code plus a single literal code, then nothing else - no EOI, and not enough
        // output to satisfy the 4 expected pixel indices
        var writer = new LzwBitWriter();
        writer.WriteCode(4, 3); // Clear
        writer.WriteCode(0, 3); // literal index 0
        WriteSubBlocks(stream, writer.ToArray());
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_LzwStreamMissingEoi_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_LzwStreamMissingEoi_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 2, gct);
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 2);
        WriteU16(stream, 2); // 4 pixels expected
        stream.WriteByte(0);
        stream.WriteByte(2);

        // Decodes exactly the 4 expected palette-index bytes, but the stream ends immediately
        // afterward without ever emitting the End-of-Information code.
        var lzwBytes = EncodeGifLzwWithoutEoi(2, [0, 1, 1, 0]);
        WriteSubBlocks(stream, lzwBytes);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    /// <summary>Test: GifCodec_Load_LzwStreamOverrunsExpectedCount_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_Load_LzwStreamOverrunsExpectedCount_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 1, gct);
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 2);
        WriteU16(stream, 1); // 2 pixels expected
        stream.WriteByte(0);
        stream.WriteByte(2); // minCodeSize=2 => clearCode=4, eoiCode=5, firstAvailableCode=6

        // Clear, literal 0, literal 0 (defines table entry code 6 = [0, 0] via KwKwK growth),
        // then code 6 itself - a 2-byte back-reference that would push total output to 4 bytes,
        // exceeding the 2 expected palette-index bytes, before any End-of-Information code.
        var writer = new LzwBitWriter();
        writer.WriteCode(4, 3); // Clear
        writer.WriteCode(0, 3); // literal 0 -> outputCount=1
        writer.WriteCode(0, 3); // literal 0 -> outputCount=2, table entry 6 = [0, 0] defined
        writer.WriteCode(6, 3); // back-reference [0, 0] -> would push outputCount to 4
        WriteSubBlocks(stream, writer.ToArray());
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.Load(stream));
    }

    // ---------------------------------------------------------------------------------------
    // Correctness
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_Load_FirstFrame_DecodesExpectedPixels.</summary>
    [Fact]
    public void GifCodec_Load_FirstFrame_DecodesExpectedPixels()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 2, gct);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 1, 1, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        Assert.Equal(2, surface.Width);
        Assert.Equal(2, surface.Height);
        Assert.Equal(new Rgba32(254, 0, 0, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 254, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 254, 255), surface[0, 1]);
        Assert.Equal(new Rgba32(254, 0, 0, 255), surface[1, 1]);
    }

    /// <summary>Test: GifCodec_Load_LocalColorTableOnly_DecodesExpectedPixels.</summary>
    [Fact]
    public void GifCodec_Load_LocalColorTableOnly_DecodesExpectedPixels()
    {
        using var stream = new MemoryStream();
        var lct = BuildColorTable((10, 20, 30), (200, 210, 220));
        WriteHeader(stream, 2, 2, null); // no Global Color Table
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, lct, 2, [0, 1, 1, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        Assert.Equal(2, surface.Width);
        Assert.Equal(2, surface.Height);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(200, 210, 220, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(200, 210, 220, 255), surface[0, 1]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[1, 1]);
    }

    /// <summary>Test: GifCodec_Load_FromFilePath_ReturnsExpectedPixels.</summary>
    [Fact]
    public void GifCodec_Load_FromFilePath_ReturnsExpectedPixels()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var gct = BuildColorTable((10, 20, 30), (40, 50, 60));
                WriteHeader(fileStream, 1, 1, gct);
                WriteImageDescriptor(fileStream, 0, 0, 1, 1, false, null, 2, [1]);
                WriteTrailer(fileStream);
            }

            using var surface = GifCodec.Load(path);

            Assert.Equal(1, surface.Width);
            Assert.Equal(1, surface.Height);
            Assert.Equal(new Rgba32(40, 50, 60, 255), surface[0, 0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Test: GifCodec_Load_MultiFrame_DecodesFirstFrameOnlyWithoutThrowing.</summary>
    [Fact]
    public void GifCodec_Load_MultiFrame_DecodesFirstFrameOnlyWithoutThrowing()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 2, gct);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [1, 1, 1, 1]);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 1, 0, 1]);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        Assert.Equal(2, surface.Width);
        Assert.Equal(2, surface.Height);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                Assert.Equal(new Rgba32(254, 0, 0, 255), surface[x, y]);
            }
        }
    }

    /// <summary>Test: GifCodec_Load_InterlacedImage_DeinterlacesCorrectly.</summary>
    [Fact]
    public void GifCodec_Load_InterlacedImage_DeinterlacesCorrectly()
    {
        const int width = 4;
        const int height = 8;

        // Build a normal-order image where every row has a distinct, recognizable index value
        var normalOrder = new byte[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                normalOrder[row * width + col] = (byte)(row % 2);
            }
        }

        var interlacedOrder = ToInterlacedOrder(normalOrder, width, height);

        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, width, height, gct);
        WriteImageDescriptor(stream, 0, 0, width, height, true, null, 2, interlacedOrder);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        for (var row = 0; row < height; row++)
        {
            var expected = row % 2 == 0 ? new Rgba32(254, 0, 0, 255) : new Rgba32(0, 0, 254, 255);
            for (var col = 0; col < width; col++)
            {
                Assert.Equal(expected, surface[col, row]);
            }
        }
    }

    /// <summary>Test: GifCodec_Load_TransparentColorIndex_ProducesAlphaZero.</summary>
    [Fact]
    public void GifCodec_Load_TransparentColorIndex_ProducesAlphaZero()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 1, gct);

        // Transparency flag set; transparent index = 1 (byte 3), with a deliberately different
        // (ignored) Delay Time low byte (0) so a wrong data[1]-based implementation would
        // observably treat index 0, not index 1, as transparent
        WriteGraphicControlExtension(stream, transparencyFlag: true, transparentColorIndex: 1, delayTime: 0);
        WriteImageDescriptor(stream, 0, 0, 2, 1, false, null, 2, [0, 1]);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        Assert.Equal((byte)255, surface[0, 0].A);
        Assert.Equal((byte)0, surface[1, 0].A);
    }

    /// <summary>Test: GifCodec_Load_SubRegionImageDescriptor_BlitsAtCorrectOffset.</summary>
    [Fact]
    public void GifCodec_Load_SubRegionImageDescriptor_BlitsAtCorrectOffset()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 4, 4, gct);
        WriteImageDescriptor(stream, 1, 1, 2, 2, false, null, 2, [1, 1, 1, 1]);
        WriteTrailer(stream);
        stream.Position = 0;

        using var surface = GifCodec.Load(stream);

        Assert.Equal(4, surface.Width);
        Assert.Equal(4, surface.Height);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var inRegion = x is >= 1 and <= 2 && y is >= 1 and <= 2;
                var pixel = surface[x, y];
                if (inRegion)
                {
                    Assert.Equal(new Rgba32(0, 0, 254, 255), pixel);
                }
                else
                {
                    Assert.Equal((byte)0, pixel.A);
                }
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // GetInfo behavior
    // ---------------------------------------------------------------------------------------

    /// <summary>Test: GifCodec_GetInfo_ReturnsExpectedDimensionsChannelsAndCanDecode.</summary>
    [Fact]
    public void GifCodec_GetInfo_ReturnsExpectedDimensionsChannelsAndCanDecode()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 12, 7, gct);
        WriteImageDescriptor(stream, 0, 0, 12, 7, false, null, 2, new byte[12 * 7]);
        WriteTrailer(stream);
        stream.Position = 0;

        var info = GifCodec.GetInfo(stream);

        Assert.Equal(12, info.Width);
        Assert.Equal(7, info.Height);
        Assert.Equal(1, info.Channels);
        Assert.False(info.HasAlpha);
        Assert.True(info.CanDecode);
        Assert.Equal(1, info.FrameCount);
    }

    /// <summary>Test: GifCodec_GetInfo_NeverDecodesLzwPixelData_AcceptsCorruptFirstFrameCompressedData.</summary>
    [Fact]
    public void GifCodec_GetInfo_NeverDecodesLzwPixelData_AcceptsCorruptFirstFrameCompressedData()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 2, gct);

        // Manually write an Image Descriptor whose compressed sub-block data is not a valid GIF
        // LZW stream (it does not start with a Clear code) - Load's DecodeGifLzw rejects this
        // when it decodes the first frame's pixels, but GetInfo never invokes the LZW decoder
        // for any frame, so it must still succeed and report the correct frame count.
        stream.WriteByte(0x2C);
        WriteU16(stream, 0);
        WriteU16(stream, 0);
        WriteU16(stream, 2);
        WriteU16(stream, 2);
        stream.WriteByte(0); // packed: no local color table, no interlace
        stream.WriteByte(2); // LZW minimum code size
        WriteSubBlocks(stream, [0xFF, 0xFF, 0xFF, 0xFF]);
        WriteTrailer(stream);
        var bytes = stream.ToArray();

        using var infoStream = new MemoryStream(bytes);
        var info = GifCodec.GetInfo(infoStream);
        Assert.Equal(2, info.Width);
        Assert.Equal(2, info.Height);
        Assert.Equal(1, info.FrameCount);

        using var loadStream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => GifCodec.Load(loadStream));
    }

    /// <summary>Test: GifCodec_GetInfo_SingleFrame_ReportsFrameCountOne.</summary>
    [Fact]
    public void GifCodec_GetInfo_SingleFrame_ReportsFrameCountOne()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 2, gct);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        var info = GifCodec.GetInfo(stream);

        Assert.Equal(1, info.FrameCount);
    }

    /// <summary>Test: GifCodec_GetInfo_MultiFrame_ReportsCorrectFrameCount.</summary>
    [Fact]
    public void GifCodec_GetInfo_MultiFrame_ReportsCorrectFrameCount()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((254, 0, 0), (0, 0, 254));
        WriteHeader(stream, 2, 2, gct);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [1, 1, 1, 1]);
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 1, 0, 1]);
        WriteTrailer(stream);
        stream.Position = 0;

        var info = GifCodec.GetInfo(stream);

        Assert.Equal(3, info.FrameCount);
    }

    /// <summary>Test: GifCodec_GetInfo_SecondFrameMissingColorTable_ThrowsInvalidDataException.</summary>
    [Fact]
    public void GifCodec_GetInfo_SecondFrameMissingColorTable_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var lct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 2, 2, null); // no Global Color Table at all

        // First frame supplies its own Local Color Table, so it is structurally valid...
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, lct, 2, [0, 0, 0, 0]);

        // ...but the second frame has neither a Local Color Table nor a Global Color Table to
        // fall back on - structurally malformed, and frame counting must not weaken this
        // rejection merely because it never decodes any frame's pixel data.
        WriteImageDescriptor(stream, 0, 0, 2, 2, false, null, 2, [0, 0, 0, 0]);
        WriteTrailer(stream);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }

    /// <summary>
    ///     Test: GifCodec_GetInfo_TrailingDataAfterTrailer_ThrowsInvalidDataException.
    ///     GetInfo must reject a stream with trailing data after the GIF Trailer exactly as
    ///     Load does, preserving the "GetInfo never accepts what Load would reject" parity - the
    ///     frame-counting walk must not stop merely upon seeing the Trailer byte without also
    ///     checking for extra data beyond it.
    /// </summary>
    [Fact]
    public void GifCodec_GetInfo_TrailingDataAfterTrailer_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var gct = BuildColorTable((255, 0, 0), (0, 0, 255));
        WriteHeader(stream, 1, 1, gct);
        WriteImageDescriptor(stream, 0, 0, 1, 1, false, null, 2, [0]);
        WriteTrailer(stream);
        stream.WriteByte(0xFF); // trailing garbage
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }

    /// <summary>
    ///     Test: GifCodec_GetInfo_ExcessiveSubBlockData_ThrowsInvalidDataException.
    ///     GetInfo's frame-counting walk shares the exact same cumulative sub-block byte budget
    ///     Load enforces. This proves the budget guard still applies identically when the
    ///     sub-block chain is only skipped (never buffered into a byte array) by GetInfo's
    ///     internal skip helper - the switch away from buffering must not weaken this limit.
    /// </summary>
    [Fact]
    public void GifCodec_GetInfo_ExcessiveSubBlockData_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        WriteHeader(stream, 1, 1, null); // tiny declared dimensions

        // A single Comment Extension (label 0xFE) whose sub-block chain's cumulative declared
        // size is one byte more than GifCodec.MaxTotalSubBlockBytes. Content is irrelevant - only
        // total volume matters - so a single reusable zero-filled 255-byte buffer is written
        // repeatedly rather than allocating one huge array up front.
        stream.WriteByte(0x21); // Extension Introducer
        stream.WriteByte(0xFE); // Comment Extension label

        var remaining = GifCodec.MaxTotalSubBlockBytes + 1;
        var chunk = new byte[255];
        while (remaining > 0)
        {
            var chunkSize = (int)Math.Min(255, remaining);
            stream.WriteByte((byte)chunkSize);
            stream.Write(chunk, 0, chunkSize);
            remaining -= chunkSize;
        }

        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => GifCodec.GetInfo(stream));
    }
}
