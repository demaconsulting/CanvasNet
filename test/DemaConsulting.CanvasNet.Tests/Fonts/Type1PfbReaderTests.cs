// cspell:ignore eexec sniff
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="Type1PfbReader"/>.
/// </summary>
public class Type1PfbReaderTests
{
    private static (byte[] FontFileBytes, int Length1, int Length2) BuildSimpleFontFileBytes()
    {
        var charstring = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(charstring, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(charstring, 600);
        SyntheticFontBuilder.WriteType1CharstringOperator(charstring, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringOperator(charstring, 14); // endchar

        return SyntheticFontBuilder.Type1([(".notdef", [.. charstring])]);
    }

    /// <summary>
    ///     Proves that Type1PfbReader TrySniff RecognizesPfbHeader.
    /// </summary>
    [Fact]
    public void Type1PfbReader_TrySniff_RecognizesPfbHeader()
    {
        var program = BuildSimpleFontFileBytes();
        var pfb = SyntheticFontBuilder.Type1Pfb(program.FontFileBytes, program.Length1, program.Length2);

        Assert.True(Type1PfbReader.TrySniff(pfb));
    }

    /// <summary>
    ///     Proves that Type1PfbReader TrySniff NonPfbData ReturnsFalse.
    /// </summary>
    [Fact]
    public void Type1PfbReader_TrySniff_NonPfbData_ReturnsFalse()
    {
        Assert.False(Type1PfbReader.TrySniff("%!PS-AdobeFont"u8.ToArray()));
        Assert.False(Type1PfbReader.TrySniff([]));
        Assert.False(Type1PfbReader.TrySniff([0x80]));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read TwoSegmentFile ReassemblesLength1AndLength2.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_TwoSegmentFile_ReassemblesLength1AndLength2()
    {
        var program = BuildSimpleFontFileBytes();
        var pfb = SyntheticFontBuilder.Type1Pfb(program.FontFileBytes, program.Length1, program.Length2);

        var (fontFileBytes, length1, length2) = Type1PfbReader.Read(pfb);

        Assert.Equal(program.Length1, length1);
        Assert.Equal(program.Length2, length2);
        Assert.Equal(program.FontFileBytes, fontFileBytes);
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read MultiSegmentFile ConcatenatesLikeTypedSegments.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_MultiSegmentFile_ConcatenatesLikeTypedSegments()
    {
        var program = BuildSimpleFontFileBytes();

        // Split the cleartext region into two separate 0x01 segments, and the encrypted region
        // into two separate 0x02 segments - proving the reader's segment loop is generic, not
        // hard-coded to exactly one segment of each type.
        var clearSplit = program.Length1 / 2;
        var encSplit = program.Length2 / 2;

        var buf = new List<byte>();
        AppendSegment(buf, 0x01, program.FontFileBytes, 0, clearSplit);
        AppendSegment(buf, 0x01, program.FontFileBytes, clearSplit, program.Length1 - clearSplit);
        AppendSegment(buf, 0x02, program.FontFileBytes, program.Length1, encSplit);
        AppendSegment(buf, 0x02, program.FontFileBytes, program.Length1 + encSplit, program.Length2 - encSplit);
        buf.Add(0x80);
        buf.Add(0x03);

        var (fontFileBytes, length1, length2) = Type1PfbReader.Read([.. buf]);

        Assert.Equal(program.Length1, length1);
        Assert.Equal(program.Length2, length2);
        Assert.Equal(program.FontFileBytes, fontFileBytes);
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read TrailingAsciiSegment DiscardsItAsTrailer.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_TrailingAsciiSegment_DiscardsItAsTrailer()
    {
        var program = BuildSimpleFontFileBytes();

        var buf = new List<byte>();
        AppendSegment(buf, 0x01, program.FontFileBytes, 0, program.Length1);
        AppendSegment(buf, 0x02, program.FontFileBytes, program.Length1, program.Length2);
        AppendSegment(buf, 0x01, "0000\ncleartomark\n"u8.ToArray(), 0, 17); // conventional trailer
        buf.Add(0x80);
        buf.Add(0x03);

        var (fontFileBytes, length1, length2) = Type1PfbReader.Read([.. buf]);

        Assert.Equal(program.Length1, length1);
        Assert.Equal(program.Length2, length2);
        Assert.Equal(program.FontFileBytes, fontFileBytes);
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read NoBinarySegment ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_NoBinarySegment_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        AppendSegment(buf, 0x01, "hello"u8.ToArray(), 0, 5);
        buf.Add(0x80);
        buf.Add(0x03);

        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read([.. buf]));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read MissingEndOfFileMarker ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_MissingEndOfFileMarker_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        AppendSegment(buf, 0x02, "hello"u8.ToArray(), 0, 5);

        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read([.. buf]));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read UnrecognizedSegmentType ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_UnrecognizedSegmentType_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        buf.Add(0x80);
        buf.Add(0x05); // unrecognized segment type
        buf.Add(0);
        buf.Add(0);
        buf.Add(0);
        buf.Add(0);

        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read([.. buf]));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read TruncatedHeader ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_TruncatedHeader_ThrowsInvalidDataException()
    {
        byte[] data = [0x80, 0x02, 0, 0]; // segment header cut short (needs 6 bytes)
        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read(data));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read TruncatedPayload ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_TruncatedPayload_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        buf.Add(0x80);
        buf.Add(0x02);
        buf.Add(100); // declares 100 bytes of payload
        buf.Add(0);
        buf.Add(0);
        buf.Add(0);
        buf.AddRange("short"u8.ToArray()); // but only 5 bytes actually follow

        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read([.. buf]));
    }

    /// <summary>
    ///     Proves that Type1PfbReader Read MissingSegmentMarkerByte ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfbReader_Read_MissingSegmentMarkerByte_ThrowsInvalidDataException()
    {
        byte[] data = [0x00, 0x02, 0, 0, 0, 0];
        Assert.Throws<InvalidDataException>(() => Type1PfbReader.Read(data));
    }

    private static void AppendSegment(List<byte> buf, byte segmentType, byte[] data, int offset, int length)
    {
        buf.Add(0x80);
        buf.Add(segmentType);
        buf.Add((byte)(length & 0xFF));
        buf.Add((byte)((length >> 8) & 0xFF));
        buf.Add((byte)((length >> 16) & 0xFF));
        buf.Add((byte)((length >> 24) & 0xFF));
        buf.AddRange(new ArraySegment<byte>(data, offset, length));
    }
}
