// cspell:ignore charstring charstrings subr subrs notdef hsbw rlineto closepath endchar
// cspell:ignore lenIV rmoveto
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="Type1Table"/>.
/// </summary>
public class Type1TableTests
{
    /// <summary>
    ///     Builds a simple charstring: <c>hsbw</c> then <c>rmoveto</c> to <c>(10, 10)</c>, a line
    ///     to <c>(20, 10)</c>, <c>closepath</c>, then <c>endchar</c>.
    /// </summary>
    private static byte[] SimpleCharstring(int sbx = 0, int width = 600)
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, sbx);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, width);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 10);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 10);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 21); // rmoveto
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 10);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 9); // closepath
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    /// <summary>
    ///     Builds an empty-outline charstring (a <c>space</c> glyph): just <c>hsbw</c> then
    ///     <c>endchar</c>, with no path operators.
    /// </summary>
    private static byte[] SpaceCharstring(int width = 300)
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, width);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    private static Type1Table Parse((byte[] FontFileBytes, int Length1, int Length2) program) =>
        Type1Table.Parse(program.FontFileBytes, program.Length1, program.Length2);

    /// <summary>
    ///     Proves that Type1Table Parse WellFormedFont ExposesGlyphCountAndOutlines.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_WellFormedFont_ExposesGlyphCountAndOutlines()
    {
        var program = SyntheticFontBuilder.Type1(
            [(".notdef", SpaceCharstring()), ("space", SpaceCharstring()), ("A", SimpleCharstring())]);
        var table = Parse(program);

        Assert.Equal(3, table.GlyphCount);
        Assert.Empty(table.GetGlyphOutline(1).Subpaths); // space - no contour
        Assert.Single(table.GetGlyphOutline(2).Subpaths); // A - one contour
    }

    /// <summary>
    ///     Proves that Type1Table Parse NotdefNotFirst ReindexesToGlyphZero.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_NotdefNotFirst_ReindexesToGlyphZero()
    {
        var program = SyntheticFontBuilder.Type1(
            [("space", SpaceCharstring()), ("A", SimpleCharstring()), (".notdef", SpaceCharstring(width: 999))]);
        var table = Parse(program);

        Assert.True(table.TryGetGlyphIndex(".notdef", out var notdefIndex));
        Assert.Equal(0, notdefIndex);
        Assert.Equal(999, table.GetAdvanceWidth(0));

        Assert.True(table.TryGetGlyphIndex("space", out var spaceIndex));
        Assert.Equal(1, spaceIndex);

        Assert.True(table.TryGetGlyphIndex("A", out var aIndex));
        Assert.Equal(2, aIndex);
    }

    /// <summary>
    ///     Proves that Type1Table GetAdvanceWidth ReturnsHsbwWidth.
    /// </summary>
    [Fact]
    public void Type1Table_GetAdvanceWidth_ReturnsHsbwWidth()
    {
        var program = SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring(width: 742))]);
        var table = Parse(program);

        Assert.Equal(742, table.GetAdvanceWidth(0));
    }

    /// <summary>
    ///     Proves that Type1Table TryGetGlyphIndex UnknownName ReturnsFalse.
    /// </summary>
    [Fact]
    public void Type1Table_TryGetGlyphIndex_UnknownName_ReturnsFalse()
    {
        var program = SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring())]);
        var table = Parse(program);

        Assert.False(table.TryGetGlyphIndex("nonexistent", out _));
    }

    /// <summary>
    ///     Proves that Type1Table GetGlyphOutline OutOfRangeIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void Type1Table_GetGlyphOutline_OutOfRangeIndex_ThrowsArgumentOutOfRangeException()
    {
        var table = Parse(SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring())]));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetGlyphOutline(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that Type1Table Parse WithLocalSubrs DecodesGlyphUsingCallSubr.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_WithLocalSubrs_DecodesGlyphUsingCallSubr()
    {
        var subr = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteType1CharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteType1CharstringOperator(subr, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringOperator(subr, 11); // return

        var main = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 600);
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 10);
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 10);
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 21); // rmoveto
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 0); // subr index - no bias in Type 1
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 10); // callsubr
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 9); // closepath
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 14); // endchar

        var program = SyntheticFontBuilder.Type1([(".notdef", [.. main])], subrs: [[.. subr]]);
        var table = Parse(program);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that Type1Table Parse RdNdNpTokens ScannerIsProcedureNameAgnostic.
    /// </summary>
    [Theory]
    [InlineData("RD", "ND", "NP")]
    [InlineData("-|", "|-", "|-")]
    [InlineData("Foo123", "Bar456", "Baz789")]
    public void Type1Table_Parse_VariousProcNameTokens_ScannerIsProcedureNameAgnostic(string readToken, string defToken, string subrDefToken)
    {
        var subr = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteType1CharstringNumber(subr, 5);
        SyntheticFontBuilder.WriteType1CharstringOperator(subr, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringOperator(subr, 11); // return

        var main = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 600);
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 10);
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 10);
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 21); // rmoveto
        SyntheticFontBuilder.WriteType1CharstringNumber(main, 0);
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 10); // callsubr
        SyntheticFontBuilder.WriteType1CharstringOperator(main, 14); // endchar

        // Subrs use 'defToken' for its own dedicated (font-specific) define-procedure token
        // (conventionally 'NP'/'|-'), which may differ from CharStrings' own 'ND'/'|-' token -
        // both are passed through the same 'defToken' parameter here since Type1's own builder
        // applies one token uniformly, but the scanner never inspects either spelling regardless.
        _ = subrDefToken;
        var program = SyntheticFontBuilder.Type1([(".notdef", [.. main])], subrs: [[.. subr]], readToken: readToken, defToken: defToken);
        var table = Parse(program);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that Type1Table Parse CustomLenIv DecodesCorrectly.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void Type1Table_Parse_CustomLenIv_DecodesCorrectly(int lenIv)
    {
        var program = SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring())], lenIv: lenIv);
        var table = Parse(program);

        Assert.Single(table.GetGlyphOutline(0).Subpaths);
    }

    /// <summary>
    ///     Proves that Type1Table Parse SeacOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_SeacOperator_ThrowsInvalidDataException()
    {
        // 'seac' is rejected while Type1Table.Parse itself peeks the glyph's advance width (the
        // peek requires the charstring to begin with 'hsbw'/'sbw', which a 'seac'-based
        // accented-composite glyph never does), so the rejection surfaces at Parse time here
        // rather than at GetGlyphOutline time - Type1CharstringInterpreterTests covers the
        // interpreter's own direct 'seac' rejection for a well-formed (hsbw-prefixed) charstring.
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 65);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 66);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 1206); // escape 12 6 = seac

        var program = SyntheticFontBuilder.Type1([(".notdef", [.. buf])]);

        Assert.Throws<InvalidDataException>(() => Parse(program));
    }

    /// <summary>
    ///     Proves that Type1Table Parse MissingCharStrings ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_MissingCharStrings_ThrowsInvalidDataException()
    {
        var fontFileBytes = System.Text.Encoding.ASCII.GetBytes("%!PS-AdobeFont\ncurrentfile eexec\n");
        Assert.Throws<InvalidDataException>(() => Type1Table.Parse(fontFileBytes, fontFileBytes.Length, 0));
    }

    /// <summary>
    ///     Proves that Type1Table Parse NegativeLength ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_NegativeLength_ThrowsInvalidDataException()
    {
        var program = SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring())]);
        Assert.Throws<InvalidDataException>(() => Type1Table.Parse(program.FontFileBytes, -1, program.Length2));
        Assert.Throws<InvalidDataException>(() => Type1Table.Parse(program.FontFileBytes, program.Length1, -1));
    }

    /// <summary>
    ///     Proves that Type1Table Parse LengthsExceedFileBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_LengthsExceedFileBounds_ThrowsInvalidDataException()
    {
        var program = SyntheticFontBuilder.Type1([(".notdef", SimpleCharstring())]);
        Assert.Throws<InvalidDataException>(() => Type1Table.Parse(program.FontFileBytes, program.Length1, program.Length2 + 1000));
    }

    /// <summary>
    ///     Proves that Type1Table Parse EmptyCharStrings ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1Table_Parse_EmptyCharStrings_ThrowsInvalidDataException()
    {
        var program = SyntheticFontBuilder.Type1([]);
        Assert.Throws<InvalidDataException>(() => Parse(program));
    }
}
