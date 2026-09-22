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
        var data = BuildMinimalFont();

        var container = SfntContainer.Parse(data);

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
        var data = BuildMinimalFont();
        var container = SfntContainer.Parse(data);

        Assert.Throws<InvalidDataException>(() => container.RequireTable("cmap"));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TruncatedOffsetTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TruncatedOffsetTable_ThrowsInvalidDataException()
    {
        var data = new byte[8];

        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TruncatedTableDirectory ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TruncatedTableDirectory_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x00010000);
        SyntheticFontBuilder.WriteUInt16(buf, 5); // claims 5 tables but has none
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse OttoVersion ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_OttoVersion_ThrowsInvalidDataException()
    {
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F) // 'OTTO'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        var ex = Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
        Assert.Contains("OTTO", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that SfntContainer Parse MacTrueVersion Succeeds.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_MacTrueVersion_Succeeds()
    {
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x74727565) // 'true'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        var container = SfntContainer.Parse(data);

        Assert.True(container.TryGetTable("head", out _));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse UnrecognizedVersion ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_UnrecognizedVersion_ThrowsInvalidDataException()
    {
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x12345678)
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse(data));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TableDirectoryEntryOverflowsUint ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TableDirectoryEntryOverflowsUint_ThrowsInvalidDataException()
    {
        // Hand-build an offset table + a single table directory entry whose offset is
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

        Assert.Throws<InvalidDataException>(() => SfntContainer.Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that SfntContainer Parse TableDirectoryEntryOutOfBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void SfntContainer_Parse_TableDirectoryEntryOutOfBounds_ThrowsInvalidDataException()
    {
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
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, value);

        var result = SfntContainer.ReadUInt16([.. buf], 0);

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
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteInt16(buf, value);

        var result = SfntContainer.ReadInt16([.. buf], 0);

        Assert.Equal(value, result);
    }

    /// <summary>
    ///     Proves that SfntContainer ReadUInt32 BigEndianRegardlessOfHostEndianness ReturnsExpectedValue.
    /// </summary>
    [Fact]
    public void SfntContainer_ReadUInt32_BigEndianRegardlessOfHostEndianness_ReturnsExpectedValue()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0xDEADBEEF);

        var result = SfntContainer.ReadUInt32([.. buf], 0);

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
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteF2Dot14(buf, value);

        var result = SfntContainer.ReadF2Dot14([.. buf], 0);

        Assert.Equal(value, result, 3);
    }
}
