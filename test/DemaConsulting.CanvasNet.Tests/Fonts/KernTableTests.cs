// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
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
        // Arrange: build a format-0 kern subtable with two known glyph pairs
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120), (5, 6, -30)]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: both pairs resolve to their configured kerning values
        Assert.Equal(120, kern.GetKerning(3, 4));
        Assert.Equal(-30, kern.GetKerning(5, 6));
    }

    /// <summary>
    ///     Proves that KernTable Format0 UnknownPair ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Format0_UnknownPair_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable with a single known glyph pair
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120)]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: pairs absent from the table (including a swapped-order pair) resolve to zero
        Assert.Equal(0, kern.GetKerning(4, 3));
        Assert.Equal(0, kern.GetKerning(99, 100));
    }

    /// <summary>
    ///     Proves that KernTable Empty GetKerningReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Empty_GetKerningReturnsZero()
    {
        // Arrange/Act: use the empty kern table singleton
        // Assert: any lookup on the empty table resolves to zero
        Assert.Equal(0, KernTable.Empty.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable NoPairs ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_NoPairs_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable with zero pairs
        var table = SyntheticFontBuilder.KernFormat0([]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: with no pairs defined, any lookup resolves to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable ZeroLengthTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_ZeroLengthTable_IsTolerant_ReturnsZero()
    {
        // Arrange/Act: parse a zero-length kern table
        var kern = KernTable.Parse([], 0, 0);

        // Assert: the empty table is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable TruncatedTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_TruncatedTable_IsTolerant_ReturnsZero()
    {
        // Arrange: build a kern table truncated right after declaring one subtable
        byte[] data = [0, 0, 0, 1, 0]; // version=0, numSubtables=1, then truncated

        // Act: parse the truncated kern table
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the truncation is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable MalformedPairCount IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_MalformedPairCount_IsTolerant_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable declaring an nPairs value too large to fit
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

        // Act: parse the kern table with the malformed pair count
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the malformed pair count is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable UnsupportedFormat2Subtable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_UnsupportedFormat2Subtable_IsTolerant_ReturnsZero()
    {
        // Arrange: build a kern subtable declaring the unsupported format-2 coverage
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0201); // coverage: format 2, horizontal

        // Act: parse the kern table with the unsupported subtable format
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the unsupported format is ignored and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable CrossStreamCoverage IsSkipped ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_CrossStreamCoverage_IsSkipped_ReturnsZero()
    {
        // Arrange: build a kern subtable declaring the cross-stream coverage bit
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0005); // coverage: format 0, horizontal + cross-stream

        // Act: parse the kern table with the cross-stream subtable
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the cross-stream subtable is skipped and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }
}
