using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="KernTable"/>.
/// </summary>
public class KernTableTests
{
    /// <summary>
    ///     Proves that KernTable Format0 KnownPair ReturnsValue.
    /// </summary>
    [Fact]
    public void KernTable_Format0_KnownPair_ReturnsValue()
    {
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120), (5, 6, -30)]);

        var kern = KernTable.Parse(table, 0, table.Length);

        Assert.Equal(120, kern.GetKerning(3, 4));
        Assert.Equal(-30, kern.GetKerning(5, 6));
    }

    /// <summary>
    ///     Proves that KernTable Format0 UnknownPair ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Format0_UnknownPair_ReturnsZero()
    {
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120)]);

        var kern = KernTable.Parse(table, 0, table.Length);

        Assert.Equal(0, kern.GetKerning(4, 3));
        Assert.Equal(0, kern.GetKerning(99, 100));
    }

    /// <summary>
    ///     Proves that KernTable Empty GetKerningReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Empty_GetKerningReturnsZero()
    {
        Assert.Equal(0, KernTable.Empty.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable NoPairs ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_NoPairs_ReturnsZero()
    {
        var table = SyntheticFontBuilder.KernFormat0([]);

        var kern = KernTable.Parse(table, 0, table.Length);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable ZeroLengthTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_ZeroLengthTable_IsTolerant_ReturnsZero()
    {
        var kern = KernTable.Parse([], 0, 0);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable TruncatedTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_TruncatedTable_IsTolerant_ReturnsZero()
    {
        byte[] data = [0, 0, 0, 1, 0]; // version=0, numSubtables=1, then truncated

        var kern = KernTable.Parse(data, 0, data.Length);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable MalformedPairCount IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_MalformedPairCount_IsTolerant_ReturnsZero()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 14); // subtable length
        SyntheticFontBuilder.WriteUInt16(buf, 0x0001); // coverage: format 0, horizontal
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // nPairs: absurdly large, does not fit
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable UnsupportedFormat2Subtable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_UnsupportedFormat2Subtable_IsTolerant_ReturnsZero()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0201); // coverage: format 2, horizontal

        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable CrossStreamCoverage IsSkipped ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_CrossStreamCoverage_IsSkipped_ReturnsZero()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0005); // coverage: format 0, horizontal + cross-stream

        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        Assert.Equal(0, kern.GetKerning(3, 4));
    }
}
