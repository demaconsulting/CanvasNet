// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore endchar hstem nosuchglyph charsets isoadobe
// cspell:ignore seac bchar achar adx ady rmoveto rlineto Agrave
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="CffTable"/>.
/// </summary>
public class CffTableTests
{
    /// <summary>
    ///     Builds a single-glyph charstring: <c>rmoveto</c> to <c>(0, 0)</c> then <c>endchar</c>.
    /// </summary>
    private static byte[] SimpleCharstring()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringOperator(buf, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    private static CffTable Parse(byte[] cffTable) => CffTable.Parse(cffTable, 0, cffTable.Length);

    /// <summary>
    ///     Proves that CffTable Parse WellFormedFont ExposesGlyphCountAndOutlines.
    /// </summary>
    [Fact]
    public void CffTable_Parse_WellFormedFont_ExposesGlyphCountAndOutlines()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), SimpleCharstring()]);
        var table = Parse(cff);

        Assert.Equal(2, table.GlyphCount);
        var outline = table.GetGlyphOutline(0);
        Assert.Single(outline.Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable Parse CidKeyedRos ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CidKeyedRos_ThrowsInvalidDataException()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring()], includeRos: true);
        Assert.Throws<InvalidDataException>(() => Parse(cff));
    }

    /// <summary>
    ///     Proves that CffTable Parse NoPrivateDict SucceedsWithNoLocalSubrs.
    /// </summary>
    [Fact]
    public void CffTable_Parse_NoPrivateDict_SucceedsWithNoLocalSubrs()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring()], includePrivate: false);
        var table = Parse(cff);

        Assert.Equal(1, table.GlyphCount);
        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable Parse WithLocalSubrs DecodesGlyphUsingCallSubr.
    /// </summary>
    [Fact]
    public void CffTable_Parse_WithLocalSubrs_DecodesGlyphUsingCallSubr()
    {
        var subr = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteCharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteCharstringOperator(subr, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringOperator(subr, 11); // return

        var main = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(main, -107); // subr index before bias (count=1 -> bias 107)
        SyntheticFontBuilder.WriteCharstringOperator(main, 10); // callsubr
        SyntheticFontBuilder.WriteCharstringOperator(main, 14); // endchar

        var cff = SyntheticFontBuilder.Cff([[.. main]], localSubrs: [[.. subr]]);
        var table = Parse(cff);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable Parse WithGlobalSubrs DecodesGlyphUsingCallGSubr.
    /// </summary>
    [Fact]
    public void CffTable_Parse_WithGlobalSubrs_DecodesGlyphUsingCallGSubr()
    {
        var subr = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(subr, 3);
        SyntheticFontBuilder.WriteCharstringNumber(subr, 3);
        SyntheticFontBuilder.WriteCharstringOperator(subr, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringOperator(subr, 11); // return

        var main = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(main, -107);
        SyntheticFontBuilder.WriteCharstringOperator(main, 29); // callgsubr
        SyntheticFontBuilder.WriteCharstringOperator(main, 14); // endchar

        var cff = SyntheticFontBuilder.Cff([[.. main]], globalSubrs: [[.. subr]]);
        var table = Parse(cff);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline OutOfRangeIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_OutOfRangeIndex_ThrowsArgumentOutOfRangeException()
    {
        var table = Parse(SyntheticFontBuilder.Cff([SimpleCharstring()]));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetGlyphOutline(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline OneCorruptGlyph DoesNotBreakOtherGlyphs.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_OneCorruptGlyph_DoesNotBreakOtherGlyphs()
    {
        var corrupt = new List<byte>();
        SyntheticFontBuilder.WriteCharstringOperator(corrupt, 9); // unsupported operator

        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), [.. corrupt], SimpleCharstring()]);
        var table = Parse(cff);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
        Assert.Throws<InvalidDataException>(() => table.GetGlyphOutline(1));
        Assert.Single(table.GetGlyphOutline(2).Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable Parse MissingCharStringsOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_MissingCharStringsOperator_ThrowsInvalidDataException()
    {
        // A CFF table truncated to just the header + empty Name/Top DICT/String/Global Subr
        // INDEXes has a Top DICT with no operators at all, so 'CharStrings' is missing.
        byte[] header = [1, 0, 4, 4];
        var emptyIndex = SyntheticFontBuilder.WriteCffIndex([]);
        var emptyTopDict = SyntheticFontBuilder.WriteCffIndex([[]]);

        var buf = new List<byte>();
        buf.AddRange(header);
        buf.AddRange(emptyIndex); // Name INDEX
        buf.AddRange(emptyTopDict); // Top DICT INDEX (one empty entry)
        buf.AddRange(emptyIndex); // String INDEX
        buf.AddRange(emptyIndex); // Global Subr INDEX

        Assert.Throws<InvalidDataException>(() => Parse([.. buf]));
    }

    /// <summary>
    ///     Proves that CffTable Parse UnsupportedMajorVersion ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_UnsupportedMajorVersion_ThrowsInvalidDataException()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring()]);
        cff[0] = 2; // major version 2 is not supported

        Assert.Throws<InvalidDataException>(() => Parse(cff));
    }

    /// <summary>
    ///     Proves that CffTable Parse TruncatedTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_TruncatedTable_ThrowsInvalidDataException()
    {
        byte[] tooShort = [1, 0, 4];
        Assert.Throws<InvalidDataException>(() => Parse(tooShort));
    }

    /// <summary>
    ///     Proves that CffTable Parse EmptyCharStringsIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_EmptyCharStringsIndex_ThrowsInvalidDataException()
    {
        var cff = SyntheticFontBuilder.Cff([]);
        Assert.Throws<InvalidDataException>(() => Parse(cff));
    }

    /// <summary>
    ///     Proves that CffTable's transcribed Standard Strings table has the exact length and
    ///     spot-checked entries documented by Adobe Technical Note #5176 Appendix A.
    /// </summary>
    [Fact]
    public void CffTable_StandardStrings_HasExpectedLengthAndSpotCheckedEntries()
    {
        Assert.Equal(391, CffTable.StandardStrings.Length);
        Assert.Equal(".notdef", CffTable.StandardStrings[0]);
        Assert.Equal("space", CffTable.StandardStrings[1]);
        Assert.Equal("A", CffTable.StandardStrings[34]);
        Assert.Equal("Z", CffTable.StandardStrings[59]);
        Assert.Equal("a", CffTable.StandardStrings[66]);
        Assert.Equal("z", CffTable.StandardStrings[91]);
        Assert.Equal("copyright", CffTable.StandardStrings[170]);
        Assert.Equal("001.000", CffTable.StandardStrings[379]);
        Assert.Equal("Semibold", CffTable.StandardStrings[390]);
    }

    /// <summary>
    ///     Proves that CffTable Parse AbsentCharset DefaultsToIsoAdobeAndResolvesByName.
    /// </summary>
    [Fact]
    public void CffTable_Parse_AbsentCharset_DefaultsToIsoAdobeAndResolvesByName()
    {
        // No 'charset' operator at all: glyph i's SID is i, so glyph 1 is SID 1 ("space") and
        // glyph 2 is SID 2 ("exclam"), per the Standard Strings table.
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), SimpleCharstring(), SimpleCharstring()]);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("space", out var spaceIndex));
        Assert.Equal(1, spaceIndex);
        Assert.True(table.TryGetGlyphIndex("exclam", out var exclamIndex));
        Assert.Equal(2, exclamIndex);
        Assert.False(table.TryGetGlyphIndex("nosuchglyph", out _));
    }

    /// <summary>
    ///     Proves that CffTable Parse PredefinedIsoAdobeCharsetId MatchesAbsentCharset.
    /// </summary>
    [Fact]
    public void CffTable_Parse_PredefinedIsoAdobeCharsetId_MatchesAbsentCharset()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), SimpleCharstring()], charsetId: 0);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("space", out var spaceIndex));
        Assert.Equal(1, spaceIndex);
    }

    /// <summary>
    ///     Proves that CffTable Parse PredefinedExpertOrExpertSubsetCharset
    ///     NeverResolvesByNameButNeverThrows - a deliberate, documented scope boundary.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CffTable_Parse_PredefinedExpertOrExpertSubsetCharset_NeverResolvesByNameButNeverThrows(int charsetId)
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), SimpleCharstring()], charsetId: charsetId);
        var table = Parse(cff);

        Assert.False(table.TryGetGlyphIndex("space", out _));
        Assert.False(table.TryGetGlyphIndex("A", out _));
        // Outline decoding is unaffected by the charset at all - only name resolution is scoped out.
        Assert.Single(table.GetGlyphOutline(1).Subpaths);
    }

    /// <summary>
    ///     Proves that CffTable Parse CustomCharsetFormat0 ResolvesEachGlyphBySid.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CustomCharsetFormat0_ResolvesEachGlyphBySid()
    {
        // Format 0: a flat array of 2-byte SIDs, one per non-.notdef glyph. Glyph 1 -> SID 34
        // ("A"), glyph 2 -> SID 66 ("a").
        byte[] charsetTable = [0, 0, 34, 0, 66];
        var cff = SyntheticFontBuilder.Cff(
            [SimpleCharstring(), SimpleCharstring(), SimpleCharstring()], charsetTable: charsetTable);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("A", out var aIndex));
        Assert.Equal(1, aIndex);
        Assert.True(table.TryGetGlyphIndex("a", out var lowerAIndex));
        Assert.Equal(2, lowerAIndex);
    }

    /// <summary>
    ///     Proves that CffTable Parse CustomCharsetFormat1 ResolvesRangesOfGlyphs.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CustomCharsetFormat1_ResolvesRangesOfGlyphs()
    {
        // Format 1: ranges of (first SID: 2 bytes, nLeft: 1 byte). A single range starting at SID
        // 34 ("A") with nLeft=2 covers glyphs 1, 2, 3 as SIDs 34, 35, 36 ("A", "B", "C").
        byte[] charsetTable = [1, 0, 34, 2];
        var cff = SyntheticFontBuilder.Cff(
            [SimpleCharstring(), SimpleCharstring(), SimpleCharstring(), SimpleCharstring()], charsetTable: charsetTable);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("A", out var aIndex));
        Assert.Equal(1, aIndex);
        Assert.True(table.TryGetGlyphIndex("B", out var bIndex));
        Assert.Equal(2, bIndex);
        Assert.True(table.TryGetGlyphIndex("C", out var cIndex));
        Assert.Equal(3, cIndex);
    }

    /// <summary>
    ///     Proves that CffTable Parse CustomCharsetFormat2 ResolvesRangesWithTwoByteNLeft.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CustomCharsetFormat2_ResolvesRangesWithTwoByteNLeft()
    {
        // Format 2: identical to format 1, but nLeft is 2 bytes - exercised with a small nLeft
        // value (1) to keep the test glyph count small, proving the wider field is read correctly.
        byte[] charsetTable = [2, 0, 34, 0, 1];
        var cff = SyntheticFontBuilder.Cff(
            [SimpleCharstring(), SimpleCharstring(), SimpleCharstring()], charsetTable: charsetTable);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("A", out var aIndex));
        Assert.Equal(1, aIndex);
        Assert.True(table.TryGetGlyphIndex("B", out var bIndex));
        Assert.Equal(2, bIndex);
    }

    /// <summary>
    ///     Proves that CffTable Parse CustomCharsetUnsupportedFormat ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CustomCharsetUnsupportedFormat_ThrowsInvalidDataException()
    {
        byte[] charsetTable = [3, 0, 0];
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring(), SimpleCharstring()], charsetTable: charsetTable);

        Assert.Throws<InvalidDataException>(() => Parse(cff));
    }

    /// <summary>
    ///     Proves that CffTable Parse CharsetOffsetOutOfBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CharsetOffsetOutOfBounds_ThrowsInvalidDataException()
    {
        var cff = SyntheticFontBuilder.Cff([SimpleCharstring()], charsetId: 999_999);
        Assert.Throws<InvalidDataException>(() => Parse(cff));
    }

    /// <summary>
    ///     Proves that CffTable Parse CustomStringIndex ResolvesCustomGlyphNameBySid.
    /// </summary>
    [Fact]
    public void CffTable_Parse_CustomStringIndex_ResolvesCustomGlyphNameBySid()
    {
        // SID 391 is the first entry of the font's own String INDEX (391 = StandardStrings.Length).
        byte[] charsetTable = [0, 1, 135]; // glyph 1 -> SID 391 (0x0187)
        var cff = SyntheticFontBuilder.Cff(
            [SimpleCharstring(), SimpleCharstring()],
            stringIndexEntries: [System.Text.Encoding.ASCII.GetBytes("Alpha")],
            charsetTable: charsetTable);
        var table = Parse(cff);

        Assert.True(table.TryGetGlyphIndex("Alpha", out var alphaIndex));
        Assert.Equal(1, alphaIndex);
    }

    /// <summary>
    ///     Proves that CffTable GetAdvanceWidth NoWidthOperand UsesDefaultWidthX.
    /// </summary>
    [Fact]
    public void CffTable_GetAdvanceWidth_NoWidthOperand_UsesDefaultWidthX()
    {
        // 'endchar' with zero operands (the width-parsed flag consumes nothing, since the operand
        // count is already 0): the glyph's width is the Private DICT's defaultWidthX unchanged.
        var cs = new List<byte>();
        SyntheticFontBuilder.WriteCharstringOperator(cs, 14); // endchar

        var cff = SyntheticFontBuilder.Cff([[.. cs]], defaultWidthX: 500, nominalWidthX: 100);
        var table = Parse(cff);

        Assert.Equal(500, table.GetAdvanceWidth(0));
    }

    /// <summary>
    ///     Proves that CffTable GetAdvanceWidth WithWidthOperand UsesNominalWidthXPlusDelta.
    /// </summary>
    [Fact]
    public void CffTable_GetAdvanceWidth_WithWidthOperand_UsesNominalWidthXPlusDelta()
    {
        // 'endchar' with a single leading operand: that operand is the width delta, so the
        // glyph's resolved width is nominalWidthX + delta, not defaultWidthX.
        var cs = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(cs, 30); // width delta
        SyntheticFontBuilder.WriteCharstringOperator(cs, 14); // endchar

        var cff = SyntheticFontBuilder.Cff([[.. cs]], defaultWidthX: 500, nominalWidthX: 100);
        var table = Parse(cff);

        Assert.Equal(130, table.GetAdvanceWidth(0));
    }

    /// <summary>
    ///     Proves that CffTable GetAdvanceWidth OutOfRangeIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void CffTable_GetAdvanceWidth_OutOfRangeIndex_ThrowsArgumentOutOfRangeException()
    {
        var table = Parse(SyntheticFontBuilder.Cff([SimpleCharstring()]));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetAdvanceWidth(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetAdvanceWidth(-1));
    }

    /// <summary>
    ///     Builds a small outline charstring: <c>rmoveto</c> to <c>(0, 0)</c>, two <c>rlineto</c>
    ///     steps of the given size, then <c>endchar</c> - with no leading width operand, so the
    ///     glyph's resolved width is always the font's <c>defaultWidthX</c>.
    /// </summary>
    private static byte[] OutlineCharstring(double dx, double dy)
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringOperator(buf, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringNumber(buf, dx);
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteCharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteCharstringNumber(buf, dy);
        SyntheticFontBuilder.WriteCharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteCharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    /// <summary>
    ///     Builds a seac-style 4-operand <c>endchar</c> charstring: <c>width? adx ady bchar achar
    ///     endchar</c> - <paramref name="width"/> is written as a leading width operand only when
    ///     supplied.
    /// </summary>
    private static byte[] SeacCharstring(double adx, double ady, int bchar, int achar, double? width = null)
    {
        var buf = new List<byte>();
        if (width.HasValue)
        {
            SyntheticFontBuilder.WriteCharstringNumber(buf, width.Value);
        }

        SyntheticFontBuilder.WriteCharstringNumber(buf, adx);
        SyntheticFontBuilder.WriteCharstringNumber(buf, ady);
        SyntheticFontBuilder.WriteCharstringNumber(buf, bchar);
        SyntheticFontBuilder.WriteCharstringNumber(buf, achar);
        SyntheticFontBuilder.WriteCharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    /// <summary>
    ///     Builds a 4-glyph synthetic font (<c>.notdef</c>, <c>A</c>, <c>grave</c>,
    ///     <c>Agrave</c>) with a format-0 charset mapping glyph indices to SIDs 34, 124, 174 -
    ///     "A", "grave", "Agrave" respectively - where <c>Agrave</c> (glyph 3) is defined as a
    ///     seac-style <c>endchar</c> composing StandardEncoding codes <c>65</c> ("A") and
    ///     <c>193</c> ("grave") at offset <c>(20, 30)</c>, with its own leading width operand of
    ///     <c>700</c> (distinct from the font's <c>defaultWidthX</c> of <c>500</c>, which
    ///     <c>A</c>/<c>grave</c> themselves resolve to, having no leading width operand of their
    ///     own).
    /// </summary>
    private static byte[] SeacFixtureCff()
    {
        byte[] charsetTable = [0, 0, 34, 0, 124, 0, 174];
        return SyntheticFontBuilder.Cff(
            [
                SimpleCharstring(),
                OutlineCharstring(10, 10),
                OutlineCharstring(5, 5),
                SeacCharstring(20, 30, bchar: 65, achar: 193, width: 700),
            ],
            charsetTable: charsetTable,
            defaultWidthX: 500,
            nominalWidthX: 0);
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline SeacStyleEndChar ComposesBaseAndTranslatedAccent.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_SeacStyleEndChar_ComposesBaseAndTranslatedAccent()
    {
        var table = Parse(SeacFixtureCff());

        var baseOutline = table.GetGlyphOutline(1); // A
        var accentOutline = table.GetGlyphOutline(2); // grave
        var composite = table.GetGlyphOutline(3); // Agrave

        var expectedAccent = accentOutline.Transform(System.Numerics.Matrix3x2.CreateTranslation(20, 30));
        Assert.Equal(baseOutline.Subpaths.Count + expectedAccent.Subpaths.Count, composite.Subpaths.Count);
        Assert.Equal(baseOutline.Subpaths[0].Start, composite.Subpaths[0].Start);
        Assert.Equal(expectedAccent.Subpaths[0].Start, composite.Subpaths[1].Start);
    }

    /// <summary>
    ///     Proves that CffTable GetAdvanceWidth SeacStyleEndChar UsesOwnWidthNotComponentWidths.
    /// </summary>
    [Fact]
    public void CffTable_GetAdvanceWidth_SeacStyleEndChar_UsesOwnWidthNotComponentWidths()
    {
        var table = Parse(SeacFixtureCff());

        Assert.Equal(500, table.GetAdvanceWidth(1)); // A -> defaultWidthX
        Assert.Equal(500, table.GetAdvanceWidth(2)); // grave -> defaultWidthX
        Assert.Equal(700, table.GetAdvanceWidth(3)); // Agrave -> its own leading width operand
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline SeacStyleEndChar
    ///     UndefinedStandardEncodingCode ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_SeacStyleEndChar_UndefinedStandardEncodingCode_ThrowsInvalidDataException()
    {
        byte[] charsetTable = [0, 0, 34, 0, 124, 0, 174];
        var cff = SyntheticFontBuilder.Cff(
            [
                SimpleCharstring(),
                OutlineCharstring(10, 10),
                OutlineCharstring(5, 5),
                SeacCharstring(20, 30, bchar: 65, achar: 127), // code 127 is undefined in StandardEncoding
            ],
            charsetTable: charsetTable,
            defaultWidthX: 500,
            nominalWidthX: 0);
        var table = Parse(cff);

        Assert.Throws<InvalidDataException>(() => table.GetGlyphOutline(3));
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline SeacStyleEndChar
    ///     GlyphNameNotInCharset ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_SeacStyleEndChar_GlyphNameNotInCharset_ThrowsInvalidDataException()
    {
        byte[] charsetTable = [0, 0, 34, 0, 124, 0, 174];
        var cff = SyntheticFontBuilder.Cff(
            [
                SimpleCharstring(),
                OutlineCharstring(10, 10),
                OutlineCharstring(5, 5),
                SeacCharstring(20, 30, bchar: 66, achar: 193), // code 66 ("B") is not in this font's charset
            ],
            charsetTable: charsetTable,
            defaultWidthX: 500,
            nominalWidthX: 0);
        var table = Parse(cff);

        Assert.Throws<InvalidDataException>(() => table.GetGlyphOutline(3));
    }

    /// <summary>
    ///     Proves that CffTable GetGlyphOutline SeacStyleEndChar
    ///     ComponentItselfSeacStyle ThrowsInvalidDataException - a seac component's own charstring
    ///     being itself a (illegal, nested) seac-style <c>endchar</c> is rejected rather than
    ///     recursed into, because <see cref="CffTable"/> deliberately omits the resolver on the
    ///     component's own recursive decode.
    /// </summary>
    [Fact]
    public void CffTable_GetGlyphOutline_SeacStyleEndChar_ComponentItselfSeacStyle_ThrowsInvalidDataException()
    {
        byte[] charsetTable = [0, 0, 34, 0, 124, 0, 174];
        var cff = SyntheticFontBuilder.Cff(
            [
                SimpleCharstring(),
                SeacCharstring(1, 1, bchar: 65, achar: 193), // "A" is itself (illegally) seac-style
                OutlineCharstring(5, 5),
                SeacCharstring(20, 30, bchar: 65, achar: 193),
            ],
            charsetTable: charsetTable,
            defaultWidthX: 500,
            nominalWidthX: 0);
        var table = Parse(cff);

        Assert.Throws<InvalidDataException>(() => table.GetGlyphOutline(3));
    }
}
