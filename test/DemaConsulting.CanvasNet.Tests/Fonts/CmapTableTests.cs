// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore misaligns unwidened
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
    ///     Proves that CmapTable Format4 SegmentStartCodeExceedsEndCode IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_SegmentStartCodeExceedsEndCode_IsRejected()
    {
        // Arrange: build a cmap with two encoding records: a higher-priority (3,1) format-4
        // subtable whose only real segment has startCode (50) greater than endCode (10) - which
        // the format-4 spec forbids - and a lower-priority, otherwise-unreachable (0,3) format-4
        // subtable that validly maps codepoint 200 to glyph 7. Because subtable selection is
        // priority-ordered and stops at the first successfully-parsed candidate, a decoder that
        // fails to reject the malformed higher-priority subtable will select it (even though it
        // never actually maps codepoint 200), silently starving the valid fallback subtable of a
        // chance to be tried - rather than rejecting the malformed one and falling through to the
        // valid mapping, as required by the documented malformed-input contract.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 2, // numTables
            0, 3, // platformId (record 0: higher priority)
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 20); // record 0 subtable offset
        SyntheticFontBuilder.WriteUInt16(buf, 0); // platformId (record 1: lower priority)
        SyntheticFontBuilder.WriteUInt16(buf, 3); // encodingId
        SyntheticFontBuilder.WriteUInt32(buf, 52); // record 1 subtable offset (20 + 32, after subtable 0)

        // Subtable 0 (offset 20): malformed - segment 0 has startCode (50) > endCode (10)
        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 32); // declared length (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 4); // segCountX2 (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 10); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[1] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 50); // startCodes[0]: exceeds endCodes[0] (10) - malformed
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // startCodes[1]
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[1]

        // Subtable 1 (offset 52): valid - one real segment mapping codepoint 200 to glyph 7,
        // followed by the mandatory 0xFFFF/0xFFFF terminator segment
        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 32); // declared length (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 4); // segCountX2 (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 200); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[1] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 200); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // startCodes[1] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 7 - 200); // idDeltas[0]: (200 + (7-200)) & 0xFFFF == 7
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[1]

        // Act: parse the cmap table
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the malformed, higher-priority subtable is rejected, so the valid,
        // lower-priority fallback subtable is used and correctly maps codepoint 200 to glyph 7
        Assert.Equal(7, cmap.GetGlyphIndex(200));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 SegmentsNotStrictlyIncreasing IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_SegmentsNotStrictlyIncreasing_IsRejected()
    {
        // Arrange: build a (3,1) format-4 subtable with three segments whose endCodes are not
        // strictly increasing (100, then 50 - out of order), followed by the mandatory 0xFFFF
        // terminator segment.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 40); // declared length (segCount = 3)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 6); // segCountX2 (segCount = 3)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 100); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 50); // endCodes[1]: not greater than endCodes[0] - malformed
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[2] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // startCodes[2]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[1]
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[2]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[2]

        // Act: parse the cmap table with the out-of-order segments
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the out-of-order segments are rejected and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(60));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 MissingTerminatorSegment IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_MissingTerminatorSegment_IsRejected()
    {
        // Arrange: build a (3,1) format-4 subtable with a single, otherwise well-formed segment
        // whose endCode (100) is not the mandatory 0xFFFF terminator value.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 24); // declared length (segCount = 1)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 2); // segCountX2 (segCount = 1)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 100); // endCodes[0]: not the mandatory 0xFFFF terminator
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 5); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]

        // Act: parse the cmap table missing its mandatory terminator segment
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the missing terminator is rejected and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(10));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 TerminatorStartCodeNotFFFF IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_TerminatorStartCodeNotFFFF_IsRejected()
    {
        // Arrange: build a (3,1) format-4 subtable with a single segment whose endCode is the
        // mandatory 0xFFFF terminator value, but whose startCode is 0 rather than the also-mandatory
        // 0xFFFF - a spec-violating "catch-all" segment (matching every codepoint 0..0xFFFF) rather
        // than the required 0xFFFF/0xFFFF sentinel.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 24); // declared length (segCount = 1)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 2); // segCountX2 (segCount = 1)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[0]: the mandatory terminator value
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[0]: not the mandatory 0xFFFF terminator value
        SyntheticFontBuilder.WriteUInt16(buf, 5); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]

        // Act: parse the cmap table whose terminator segment has a non-0xFFFF startCode
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the spec-violating catch-all segment is rejected and lookups resolve to glyph
        // index zero
        Assert.Equal(0, cmap.GetGlyphIndex(10));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 OverlappingSegmentsWithIncreasingEndCode IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_OverlappingSegmentsWithIncreasingEndCode_IsRejected()
    {
        // Arrange: build a (3,1) format-4 subtable with two segments whose endCodes are strictly
        // increasing (10, then 20 - passing a naive strictly-increasing-endCode check) but whose
        // codepoint ranges nonetheless overlap: segment 0 covers 0..10 and segment 1 covers 5..20,
        // so codepoint 5..10 is covered by both. Followed by the mandatory 0xFFFF terminator.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 40); // declared length (segCount = 3)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 6); // segCountX2 (segCount = 3)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 10); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 20); // endCodes[1]: greater than endCodes[0], but overlapping
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[2] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 0); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 5); // startCodes[1]: <= endCodes[0] - overlapping range
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // startCodes[2]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[1]
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[2]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idRangeOffsets[2]

        // Act: parse the cmap table with overlapping segments
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the overlapping segments are rejected and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(7));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 IdRangeOffsetPointsBackward ReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_IdRangeOffsetPointsBackward_ReturnsZero()
    {
        // Arrange: build a (3,1) format-4 subtable, segCount = 2 (one real segment + terminator),
        // whose real segment's idRangeOffset is deliberately crafted to point backward - into the
        // subtable's own idRangeOffset array bytes rather than forward into glyphIdArray. A
        // correct decoder must reject this rather than reading and returning whatever
        // (unrelated/attacker-controlled) 16-bit value happens to live at that backward address.
        var buf = new List<byte>
        {
            0, 0, // cmap version
            0, 1, // numTables
            0, 3, // platformId
            0, 1, // encodingId
        };
        SyntheticFontBuilder.WriteUInt32(buf, 12); // subtable offset

        SyntheticFontBuilder.WriteUInt16(buf, 4); // format
        SyntheticFontBuilder.WriteUInt16(buf, 32); // declared length (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // language
        SyntheticFontBuilder.WriteUInt16(buf, 4); // segCountX2 (segCount = 2)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // searchRange
        SyntheticFontBuilder.WriteUInt16(buf, 0); // entrySelector
        SyntheticFontBuilder.WriteUInt16(buf, 0); // rangeShift
        SyntheticFontBuilder.WriteUInt16(buf, 10); // endCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // endCodes[1] (terminator)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // reservedPad
        SyntheticFontBuilder.WriteUInt16(buf, 10); // startCodes[0]
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // startCodes[1]
        SyntheticFontBuilder.WriteUInt16(buf, 0); // idDeltas[0]
        SyntheticFontBuilder.WriteUInt16(buf, 1); // idDeltas[1]

        // idRangeOffsetOffset is at buf position 14 + 2*4 + 2 + 4 + 4 = 32 (relative to subtable
        // start at absolute offset 12), i.e. absolute offset 44. idRangeOffsets[0] here is set to
        // 2, which - per the spec formula - computes to
        // idRangeOffsetOffset + 0*2 + 2 + 2*(10-10) = 46, pointing at idRangeOffsets[1] itself
        // (still inside the idRangeOffset array, before glyphIdArrayOffset), not forward into a
        // (nonexistent) glyphIdArray.
        SyntheticFontBuilder.WriteUInt16(buf, 2); // idRangeOffsets[0]: points backward, not forward
        SyntheticFontBuilder.WriteUInt16(buf, 0xBEEF); // idRangeOffsets[1] slot: "bogus" value at the backward address

        // Act: parse the cmap table with the backward-pointing idRangeOffset
        var data = buf.ToArray();
        var cmap = CmapTable.Parse(data, 0, data.Length);

        // Assert: the backward-pointing idRangeOffset is rejected and the lookup resolves to
        // glyph index zero rather than the bogus 0xBEEF-derived value
        Assert.Equal(0, cmap.GetGlyphIndex(10));
    }

    /// <summary>
    ///     Proves that CmapTable Format4 GlyphIndexAddressArithmeticOverflow ReturnsZero.
    /// </summary>
    [Fact]
    public void CmapTable_Format4_GlyphIndexAddressArithmeticOverflow_ReturnsZero()
    {
        // Arrange: choose a subtable position (near the top of what a real byte array can
        // address, per Array.MaxLength) large enough that computing
        // `idRangeOffsetOffset + i * 2 + idRangeOffsets[i] + 2 * (codepoint - startCodes[i])`
        // with unwidened 32-bit `int` arithmetic wraps past `int.MaxValue` to a negative value,
        // which would incorrectly pass the `glyphIndexAddress + 2 > tableEnd` bounds guard and
        // then be used directly as a negative array index. The margin is provided by
        // `idRangeOffsets[0]` (its 0xFFFF maximum), mirroring the technique used by
        // <c>KernTable_SubtableLengthOverflowsPosition_IsTolerant_ReturnsZero</c>. Note that only
        // the *positions* need to be this large; the declared cmap `tableLength` (and hence the
        // portion of the array that is actually populated/read on the corrected, non-overflowing
        // path) stays tiny, so the backing array itself only needs to be a little over
        // `idRangeOffsetOffset` bytes - not anywhere near `int.MaxValue` bytes twice over.
        const int segCount = 2;
        const int segCountX2 = segCount * 2;
        const int cmapHeaderSize = 12;

        // Field layout (relative to subtableOffset): format/length/language/segCountX2/
        // searchRange/entrySelector/rangeShift = 14 bytes, then endCodes(segCountX2) +
        // reservedPad(2) + startCodes(segCountX2) + idDeltas(segCountX2) = idRangeOffsetOffset.
        var idRangeOffsetOffset = Array.MaxLength - 30;
        var subtableOffset = idRangeOffsetOffset - (14 + segCountX2 + 2 + segCountX2 + segCountX2);
        var tableOffset = subtableOffset - cmapHeaderSize;
        Assert.True(tableOffset >= 0, "test setup: tableOffset must be non-negative");

        var declaredLength = subtableOffset - tableOffset + 14 + segCountX2 + 2 + segCountX2 + segCountX2 + segCountX2;
        var data = new byte[tableOffset + declaredLength];

        void WriteU16(int offset, int value)
        {
            data[offset] = (byte)((value >> 8) & 0xFF);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        void WriteU32(int offset, long value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        WriteU16(tableOffset, 0); // cmap version
        WriteU16(tableOffset + 2, 1); // numTables
        WriteU16(tableOffset + 4, 3); // platformId
        WriteU16(tableOffset + 6, 1); // encodingId
        WriteU32(tableOffset + 8, subtableOffset - tableOffset); // subtable offset, relative to cmap table start

        WriteU16(subtableOffset, 4); // format
        WriteU16(subtableOffset + 2, declaredLength - (subtableOffset - tableOffset)); // declared length
        WriteU16(subtableOffset + 4, 0); // language
        WriteU16(subtableOffset + 6, segCountX2);
        WriteU16(subtableOffset + 8, 0); // searchRange
        WriteU16(subtableOffset + 10, 0); // entrySelector
        WriteU16(subtableOffset + 12, 0); // rangeShift

        var endCodeOffset = subtableOffset + 14;
        WriteU16(endCodeOffset, 0); // endCodes[0]: codepoint 0 maps via this segment
        WriteU16(endCodeOffset + 2, 0xFFFF); // endCodes[1] (terminator)

        var startCodeOffset = endCodeOffset + segCountX2 + 2;
        WriteU16(startCodeOffset, 0); // startCodes[0]
        WriteU16(startCodeOffset + 2, 0xFFFF); // startCodes[1]

        var idDeltaOffset = startCodeOffset + segCountX2;
        WriteU16(idDeltaOffset, 0); // idDeltas[0]
        WriteU16(idDeltaOffset + 2, 1); // idDeltas[1]

        Assert.Equal(idRangeOffsetOffset, idDeltaOffset + segCountX2);
        WriteU16(idRangeOffsetOffset, 0xFFFF); // idRangeOffsets[0]: maximal, to maximize the overflow margin
        WriteU16(idRangeOffsetOffset + 2, 0); // idRangeOffsets[1]

        // Act: parse the cmap table and look up codepoint 0, which resolves to segment 0's
        // indirect (idRangeOffset != 0) lookup path
        var cmap = CmapTable.Parse(data, tableOffset, declaredLength);

        // Assert: the overflow-prone address computation is rejected (rather than wrapping to a
        // negative value that would be misread as in-bounds) and the lookup resolves to zero
        Assert.Equal(0, cmap.GetGlyphIndex(0));
    }

    /// <summary>
    ///     Proves that CmapTable Format12 GroupsOverlap IsRejected.
    /// </summary>
    [Fact]
    public void CmapTable_Format12_GroupsOverlap_IsRejected()
    {
        // Arrange: build a format-12 cmap subtable with two groups whose charCode ranges overlap
        // (group 0 covers 0x00-0x10, group 1 covers 0x08-0x20), which the spec forbids (groups
        // must be sorted, non-overlapping, by startCharCode).
        var table = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x00u, 0x10u, 1u), (0x08u, 0x20u, 100u)]);

        // Act: parse the cmap table with the overlapping groups
        var cmap = CmapTable.Parse(table, 0, table.Length);

        // Assert: the overlapping groups are rejected and lookups resolve to glyph index zero
        Assert.Equal(0, cmap.GetGlyphIndex(0x09));
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
