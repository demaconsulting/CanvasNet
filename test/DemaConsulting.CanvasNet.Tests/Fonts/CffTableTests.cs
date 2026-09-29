// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore endchar hstem
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
}
