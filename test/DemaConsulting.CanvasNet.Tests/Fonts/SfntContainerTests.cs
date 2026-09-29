// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="SfntContainer"/>.
/// </summary>
public class SfntContainerTests
{
    private static byte[] BuildMinimalFont() =>
        new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("glyf", [])
            .Build();

    /// <summary>
    ///     Proves that SfntContainer Parse WellFormedFont ExposesTables.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_WellFormedFont_ExposesTables()
    {
        // Arrange: build a minimal well-formed SFNT font with head and glyf tables
        var data = BuildMinimalFont();

        // Act: parse the font data
        var container = SfntContainer.Parse(data);

        // Assert: known tables are exposed with correct lengths, unknown tables are not
        Assert.True(container.TryGetTable("head", out var head));
        Assert.Equal(54, head.Length);
        Assert.True(container.TryGetTable("glyf", out _));
        Assert.False(container.TryGetTable("nope", out _));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse MissingRequiredTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_MissingRequiredTable_ThrowsInvalidDataException()
    {
        // Arrange: parse a font that has no cmap table
        var data = BuildMinimalFont();
        var container = SfntContainer.Parse(data);

        // Act/Assert: requiring the missing table throws
        Assert.Throws<InvalidDataException>(() => container.RequireTable("cmap"));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TruncatedOffsetTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TruncatedOffsetTable_ThrowsInvalidDataException()
    {
        // Arrange: build data too short to contain a valid SFNT offset table
        var data = new byte[8];

        // Act/Assert: parsing the truncated data throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TruncatedTableDirectory ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TruncatedTableDirectory_ThrowsInvalidDataException()
    {
        // Arrange: build an offset table claiming 5 table directory entries but supplying none
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x00010000);
        SyntheticFontBuilder.WriteUInt16(buf, 5); // claims 5 tables but has none
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        // Act/Assert: parsing the truncated table directory throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse OttoVersion ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_OttoVersion_ThrowsInvalidDataException()
    {
        // Arrange: build a font with the 'OTTO' (CFF-flavored) sfnt version tag but no 'CFF '
        // table - structurally inconsistent, and still rejected
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F) // 'OTTO'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        // Act/Assert: parsing throws, and the message identifies the unsupported version
        var ex = Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
        Assert.Contains("OTTO", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that SfntContainer Parse OttoVersionWithCffTable Succeeds.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_OttoVersionWithCffTable_Succeeds()
    {
        // Arrange: build a font with the 'OTTO' sfnt version tag and a 'CFF ' table present
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F) // 'OTTO'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("CFF ", [1, 0, 4, 4])
            .Build();

        // Act: parse the font data
        var container = SfntContainer.Parse(data);

        // Assert: parsing succeeds and both tables are exposed
        Assert.True(container.TryGetTable("head", out _));
        Assert.True(container.TryGetTable("CFF ", out _));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse MacTrueVersion Succeeds.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_MacTrueVersion_Succeeds()
    {
        // Arrange: build a font using the legacy Mac 'true' sfnt version tag
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x74727565) // 'true'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        // Act: parse the font data
        var container = SfntContainer.Parse(data);

        // Assert: parsing succeeds and the head table is exposed
        Assert.True(container.TryGetTable("head", out _));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse UnrecognizedVersion ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_UnrecognizedVersion_ThrowsInvalidDataException()
    {
        // Arrange: build a font with an sfnt version tag not recognized by this implementation
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x12345678)
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        // Act/Assert: parsing the unrecognized version throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TableDirectoryEntryOverflowsUint ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TableDirectoryEntryOverflowsUint_ThrowsInvalidDataException()
    {
        // Arrange: hand-build an offset table + a single table directory entry whose offset is
        // uint.MaxValue: a naive raw-uint "offset + length" bounds check would wrap around
        // (4294967295 + 54 mod 2^32 == 53), which could deceptively pass a small data-length
        // bounds check despite the offset itself being nonsensical. Computing the bound in
        // long/checked arithmetic (never raw uint + uint) must reject this instead.
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x00010000);
        SyntheticFontBuilder.WriteUInt16(buf, 1);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        buf.AddRange("head"u8.ToArray());
        SyntheticFontBuilder.WriteUInt32(buf, 0); // checksum
        SyntheticFontBuilder.WriteUInt32(buf, 0xFFFFFFFF); // offset = uint.MaxValue
        SyntheticFontBuilder.WriteUInt32(buf, 54); // length

        // Act/Assert: parsing the overflowing table directory entry throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TableDirectoryEntryOutOfBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TableDirectoryEntryOutOfBounds_ThrowsInvalidDataException()
    {
        // Arrange: build a table directory entry whose offset lies well past the actual data
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x00010000);
        SyntheticFontBuilder.WriteUInt16(buf, 1);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        buf.AddRange("head"u8.ToArray());
        SyntheticFontBuilder.WriteUInt32(buf, 0);
        SyntheticFontBuilder.WriteUInt32(buf, 1000); // offset well past the actual data length
        SyntheticFontBuilder.WriteUInt32(buf, 54);

        // Act/Assert: parsing the out-of-bounds table directory entry throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that SfntContainer ReadUInt16 BigEndianRegardlessOfHostEndianness ReturnsExpectedValue.
    /// </summary>
    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)0x1234)]
    [InlineData((ushort)0xFFFF)]
    public void SfntContainer_ReadUInt16_BigEndianRegardlessOfHostEndianness_ReturnsExpectedValue(ushort value)
    {
        // Arrange: write the value as big-endian bytes
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, value);

        // Act: read the value back as a big-endian UInt16
        var result = SfntContainer.ReadUInt16([.. buf], 0);

        // Assert: the read value matches the original regardless of host endianness
        Assert.Equal(value, result);
    }

    /// <summary>
    ///     Proves that SfntContainer ReadInt16 BigEndianRegardlessOfHostEndianness ReturnsExpectedValue.
    /// </summary>
    [Theory]
    [InlineData(short.MinValue)]
    [InlineData((short)-1)]
    [InlineData(short.MaxValue)]
    public void SfntContainer_ReadInt16_BigEndianRegardlessOfHostEndianness_ReturnsExpectedValue(short value)
    {
        // Arrange: write the value as big-endian bytes
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteInt16(buf, value);

        // Act: read the value back as a big-endian Int16
        var result = SfntContainer.ReadInt16([.. buf], 0);

        // Assert: the read value matches the original regardless of host endianness
        Assert.Equal(value, result);
    }

    /// <summary>
    ///     Proves that SfntContainer ReadUInt32 BigEndianRegardlessOfHostEndianness ReturnsExpectedValue.
    /// </summary>
    [Fact]
    public void SfntContainer_ReadUInt32_BigEndianRegardlessOfHostEndianness_ReturnsExpectedValue()
    {
        // Arrange: write a value as big-endian bytes
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0xDEADBEEF);

        // Act: read the value back as a big-endian UInt32
        var result = SfntContainer.ReadUInt32([.. buf], 0);

        // Assert: the read value matches the original regardless of host endianness
        Assert.Equal(0xDEADBEEFu, result);
    }

    /// <summary>
    ///     Proves that SfntContainer ReadF2Dot14 ReturnsExpectedValue.
    /// </summary>
    [Theory]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(0.5f)]
    public void SfntContainer_ReadF2Dot14_ReturnsExpectedValue(float value)
    {
        // Arrange: write the value as an F2Dot14 fixed-point encoding
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteF2Dot14(buf, value);

        // Act: read the value back as F2Dot14
        var result = SfntContainer.ReadF2Dot14([.. buf], 0);

        // Assert: the read value matches the original within F2Dot14 precision
        Assert.Equal(value, result, 3);
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader WellFormedContainer ReturnsFaceOffsets.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_WellFormedContainer_ReturnsFaceOffsets()
    {
        // Arrange: build two independent single-face fonts, then wrap them in a synthetic ttcf
        // container
        var face0 = BuildMinimalFont();
        var face1 = BuildMinimalFont();
        var ttc = SyntheticFontBuilder.Ttc([face0, face1]);

        // Act: read the ttcf header
        var isTtc = SfntContainer.TryReadTtcHeader(ttc, out var faceOffsets);

        // Assert: the container is recognized and both face offsets are reported
        Assert.True(isTtc);
        Assert.Equal(2, faceOffsets.Count);
        Assert.True(faceOffsets[1] > faceOffsets[0]);
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader NonTtcFont ReturnsFalse.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_NonTtcFont_ReturnsFalse()
    {
        // Arrange: an ordinary, non-collection SFNT font
        var data = BuildMinimalFont();

        // Act: attempt to read a ttcf header
        var isTtc = SfntContainer.TryReadTtcHeader(data, out var faceOffsets);

        // Assert: not recognized as a collection, and no face offsets are reported
        Assert.False(isTtc);
        Assert.Empty(faceOffsets);
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader EachFaceIndependentlyParsable.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_EachFaceIndependentlyParsable()
    {
        // Arrange: wrap two single-face fonts in a synthetic ttcf container
        var face0 = BuildMinimalFont();
        var face1 = BuildMinimalFont();
        var ttc = SyntheticFontBuilder.Ttc([face0, face1]);
        Assert.True(SfntContainer.TryReadTtcHeader(ttc, out var faceOffsets));

        // Act: parse each face independently via its own offset table start
        var container0 = SfntContainer.Parse(ttc, faceOffsets[0]);
        var container1 = SfntContainer.Parse(ttc, faceOffsets[1]);

        // Assert: both faces' tables are independently resolvable
        Assert.True(container0.TryGetTable("head", out _));
        Assert.True(container1.TryGetTable("head", out _));
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader TruncatedHeader ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_TruncatedHeader_ThrowsInvalidDataException()
    {
        // Arrange: only the 4-byte 'ttcf' tag, missing the rest of the fixed 12-byte prefix
        byte[] data = [0x74, 0x74, 0x63, 0x66];

        // Act/Assert: reading the truncated header throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.TryReadTtcHeader(data, out _));
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader ZeroFonts ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_ZeroFonts_ThrowsInvalidDataException()
    {
        // Arrange: a ttcf header declaring zero faces
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x74746366); // 'ttcf'
        SyntheticFontBuilder.WriteUInt16(buf, 1);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt32(buf, 0); // numFonts = 0

        // Act/Assert: reading the header throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.TryReadTtcHeader([.. buf], out _));
    }

    /// <summary>
    ///     Proves that SfntContainer TryReadTtcHeader TruncatedFaceOffsetTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_TryReadTtcHeader_TruncatedFaceOffsetTable_ThrowsInvalidDataException()
    {
        // Arrange: a ttcf header declaring 2 faces but supplying no face offset table at all
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x74746366); // 'ttcf'
        SyntheticFontBuilder.WriteUInt16(buf, 1);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt32(buf, 2); // numFonts = 2, but no offsets follow

        // Act/Assert: reading the header throws
        Assert.Throws<InvalidDataException>(() => SfntContainer.TryReadTtcHeader([.. buf], out _));
    }
}
