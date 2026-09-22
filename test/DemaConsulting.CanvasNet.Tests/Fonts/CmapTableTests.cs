// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore misaligns
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
    ///     Proves that CmapTable SubtableOffsetNearIntMaxValue GetGlyphIndexReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_SubtableOffsetNearIntMaxValue_GetGlyphIndexReturnsZero()
    {
        // Arrange: build a cmap header whose single subtable offset is near int.MaxValue, so that
        // a naive `subtableOffset + 2 > tableLength` bounds check would wrap around to a negative
        // value and incorrectly pass. The buffer is padded past the single 8-byte encoding record
        // (to at least `recordsEnd` = 8 + numTables * 8 = 16 bytes) so that `Parse` actually
        // reaches the per-record subtableOffset bounds check inside the loop, rather than
        // returning `Empty` early from the `recordsEnd > tableLength` guard before that check is
        // ever exercised.
        var buf = new List<byte>
        {
            0, 0, // version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 0x7FFFFFFF); // subtable offset near int.MaxValue
        buf.AddRange(new byte[4]); // padding so tableLength (16) reaches recordsEnd (16)

        // Act: parse the cmap table with the overflow-prone subtable offset
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the malformed offset is rejected (not wrapped-around-accepted) and lookups
        // resolve to glyph index zero, without throwing
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 DeclaredLengthShorterThanEnclosingTable IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_DeclaredLengthShorterThanEnclosingTable_IsRejected()
    {
        // Arrange: build a cmap with a single (3,1) format-4 subtable whose own declared `length`
        // field (20) is deliberately shorter than the bytes actually needed to complete parsing
        // (32 bytes: header + endCodes/startCodes/idDelta/idRangeOffset arrays for segCount=2).
        // The extra bytes beyond the declared length are physically present in the buffer (as
        // they would be for a genuine following subtable packed immediately afterward) and are
        // populated with a distinguishing, deliberately "foreign" mapping - codepoint 65 maps to
        // 999 - so that a decoder which is only bounded by the *enclosing* cmap table (and
        // ignores this subtable's own shorter declared length) can be proven to have read past
        // the subtable's own bounds.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset: immediately after this 12-byte header

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 20); // declared length: covers only the header + endCodes + reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 4); // segCountX2 (segCount = 2: one real segment + terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 65); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[1] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad - declared subtable length (20) ends here

        // Bytes beyond the declared length (20): a "foreign" startCodes/idDelta/idRangeOffset
        // region that a correct decoder must not treat as belonging to this subtable.
        SyntheticFontBuilder.WriteUInt16(buf, 65); // "foreign" startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // "foreign" startCodes[1]
        SyntheticFontBuilder.WriteUInt16(buf, 934); // "foreign" idDeltas[0]: (65 + 934) & 0xFFFF == 999
        SyntheticFontBuilder.WriteUInt16(buf, 1); // "foreign" idDeltas[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // "foreign" idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // "foreign" idRangeOffsets[1]

        // Act: parse the cmap table
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the subtable's own declared length is enforced - it cannot borrow the
        // "foreign" bytes beyond it - so parsing is rejected and the lookup resolves to zero
        // rather than the "foreign" bogus glyph index (999) those bytes would otherwise produce
        Assert.Equal(0, cmap.GetGlyphIndex(65));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 OddSegCountX2 IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_OddSegCountX2_IsRejected()
    {
        // Arrange: build a cmap with a single (3,1) format-4 subtable declaring an odd
        // segCountX2 (5), which the format-4 spec requires to always be even (segCount * 2). A
        // decoder that only truncates `segCount = segCountX2 / 2` (yielding 2) while still using
        // the untruncated odd segCountX2 to compute the startCode/idDelta/idRangeOffset array
        // base offsets misaligns those parallel arrays by one byte, and can still produce a
        // "successful" (but bogus) lookup rather than failing safely.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 36); // declared length (covers the full crafted subtable below)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 5); // segCountX2: odd - malformed
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 65); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // endCodes[1] (unused by this test's lookup path)
        buf.AddRange(new byte[3]); // unread gap before the (misaligned) startCodeOffset
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[1] (unused)
        buf.Add(0); // unread gap before the (misaligned) idDeltaOffset
        SyntheticFontBuilder.WriteUInt16(buf, 500); // idDeltas[0]: (65 + 500) & 0xFFFF == 565
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[1] (unused)
        buf.Add(0); // unread gap before the (misaligned) idRangeOffsetOffset
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[1] (unused)
        buf.Add(0); // pad out to the declared 36-byte subtable length

        // Act: parse the cmap table with the malformed odd segCountX2
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the odd segCountX2 is rejected outright rather than tolerated via truncating
        // division, so the lookup resolves to zero rather than the misaligned bogus glyph index
        // (565) that reading through the shifted arrays would otherwise produce
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
