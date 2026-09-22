// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="CmapTable"/>.
/// </summary>
public class CmapTableTests
{
    /// <summary>
    ///     Proves that CmapTable Format4 BmpLookup ReturnsMappedGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_BmpLookup_ReturnsMappedGlyphIndex()
    {
        var table = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 3), (66, 4)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(3, cmap.GetGlyphIndex(65));
        Assert.Equal(4, cmap.GetGlyphIndex(66));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 UnmappedCodepoint ReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_UnmappedCodepoint_ReturnsZero()
    {
        var table = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 3)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(0, cmap.GetGlyphIndex(999));
    }

    /// <summary>
    ///     Proves that CmapTable Format12 SupplementaryPlaneLookup ReturnsMappedGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Format12_SupplementaryPlaneLookup_ReturnsMappedGlyphIndex()
    {
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x1F600u, 0x1F600u, 5u)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(5, cmap.GetGlyphIndex(0x1F600));
    }

    /// <summary>
    ///     Proves that CmapTable Format12 GroupRange ReturnsOffsetGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Format12_GroupRange_ReturnsOffsetGlyphIndex()
    {
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x100u, 0x110u, 50u)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(55, cmap.GetGlyphIndex(0x105));
        Assert.Equal(0, cmap.GetGlyphIndex(0x111));
    }

    /// <summary>
    ///     Proves that CmapTable Platform3Encoding10Format12 HighestPriority ReturnsMappedGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Platform3Encoding10Format12_HighestPriority_ReturnsMappedGlyphIndex()
    {
        // (3,10) format-12 is priority 0 (the highest-priority subtable this implementation
        // recognizes) - confirms it is selected and produces a correct lookup.
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(65u, 65u, 9u)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(9, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable NoSupportedSubtable GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_NoSupportedSubtable_GetGlyphIndexReturnsZero()
    {
        // Platform/encoding pair (1,0) - classic Mac Roman - is never selected by this
        // implementation, regardless of subtable format.
        var table = SyntheticFontBuilder.CmapFormat4(1, 0, [(65, 3)]);

        var cmap = CmapTable.Parse(table, 0, table.Length);

        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable Empty GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Empty_GetGlyphIndexReturnsZero()
    {
        Assert.Equal(0, CmapTable.Empty.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable TruncatedTable GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_TruncatedTable_GetGlyphIndexReturnsZero()
    {
        var cmap = CmapTable.Parse([0, 0], 0, 2);

        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable MalformedSubtableOffset GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_MalformedSubtableOffset_GetGlyphIndexReturnsZero()
    {
        var buf = new List<byte>
        {
            0, 0, // version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 100000); // subtable offset far past the table end

        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable UnrecognizedSubtableFormat GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_UnrecognizedSubtableFormat_GetGlyphIndexReturnsZero()
    {
        // Format 6 (trimmed table mapping) is not understood by this implementation.
        var buf = new List<byte>
        {
            0, 0, // version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset
        SyntheticFontBuilder.WriteUInt16(buf, 6); // format 6 - unsupported

        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }
}
