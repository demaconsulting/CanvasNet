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
        // Arrange: build a format-4 cmap subtable mapping two BMP codepoints
        var table = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 3), (66, 4)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: both codepoints resolve to their mapped glyph indices
        Assert.Equal(3, cmap.GetGlyphIndex(65));
        Assert.Equal(4, cmap.GetGlyphIndex(66));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 UnmappedCodepoint ReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_UnmappedCodepoint_ReturnsZero()
    {
        // Arrange: build a format-4 cmap subtable mapping a single codepoint
        var table = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 3)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: a codepoint absent from the table resolves to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(999));
    }

    /// <summary>
    ///     Proves that CmapTable Format12 SupplementaryPlaneLookup ReturnsMappedGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Format12_SupplementaryPlaneLookup_ReturnsMappedGlyphIndex()
    {
        // Arrange: build a format-12 cmap subtable mapping a single supplementary-plane codepoint
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x1F600u, 0x1F600u, 5u)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: the supplementary-plane codepoint resolves to its mapped glyph index
        Assert.Equal(5, cmap.GetGlyphIndex(0x1F600));
    }

    /// <summary>
    ///     Proves that CmapTable Format12 GroupRange ReturnsOffsetGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Format12_GroupRange_ReturnsOffsetGlyphIndex()
    {
        // Arrange: build a format-12 cmap subtable with a single codepoint-range group
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x100u, 0x110u, 50u)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: a codepoint within the range resolves with the group's offset, outside resolves to zero
        Assert.Equal(55, cmap.GetGlyphIndex(0x105));
        Assert.Equal(0, cmap.GetGlyphIndex(0x111));
    }

    /// <summary>
    ///     Proves that CmapTable Platform3Encoding10Format12 HighestPriority ReturnsMappedGlyphIndex.
    /// </summary>
    [Fact]
    public void CmapTable_Platform3Encoding10Format12_HighestPriority_ReturnsMappedGlyphIndex()
    {
        // Arrange: build a (3,10) format-12 cmap subtable - priority 0, the highest-priority
        // subtable this implementation recognizes - to confirm it is selected
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(65u, 65u, 9u)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: the highest-priority subtable is selected and produces a correct lookup
        Assert.Equal(9, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable NoSupportedSubtable GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_NoSupportedSubtable_GetGlyphIndexReturnsZero()
    {
        // Arrange: build a cmap subtable with platform/encoding pair (1,0) - classic Mac Roman -
        // which is never selected by this implementation, regardless of subtable format
        var table = SyntheticFontBuilder.CmapFormat4(1, 0, [(65, 3)]);

        // Act: parse the cmap table
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: with no supported subtable selected, lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable Empty GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Empty_GetGlyphIndexReturnsZero()
    {
        // Arrange/Act: use the empty cmap table singleton
        // Assert: any lookup on the empty table resolves to glyph index zero
        Assert.Equal(0, CmapTable.Empty.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable TruncatedTable GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_TruncatedTable_GetGlyphIndexReturnsZero()
    {
        // Arrange/Act: parse a truncated table too short to contain a header
        var cmap = CmapTable.Parse([0, 0], 0, 2);

        // Assert: parsing tolerates the truncation and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable MalformedSubtableOffset GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_MalformedSubtableOffset_GetGlyphIndexReturnsZero()
    {
        // Arrange: build a cmap header whose single subtable offset points far past the table end
        var buf = new List<byte>
        {
            0, 0, // version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 100000); // subtable offset far past the table end

        // Act: parse the cmap table with the malformed subtable offset
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the malformed offset is tolerated and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable UnrecognizedSubtableFormat GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_UnrecognizedSubtableFormat_GetGlyphIndexReturnsZero()
    {
        // Arrange: build a cmap subtable using format 6 (trimmed table mapping), which is not
        // understood by this implementation
        var buf = new List<byte>
        {
            0, 0, // version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset
        SyntheticFontBuilder.WriteUInt16(buf, 6); // format 6 - unsupported

        // Act: parse the cmap table with the unrecognized subtable format
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the unrecognized format is ignored and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }
}
