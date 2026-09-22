// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="TrueTypeFont"/>.
/// </summary>
public class TrueTypeFontTests
{
    /// <summary>
    ///     Builds a minimal, complete, well-formed synthetic font with a single simple glyph (a
    ///     10x10 square) mapped from codepoint 'A' (65), one kerning pair, and every required
    ///     table present.
    /// </summary>
    private static byte[] BuildWellFormedFont()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);
        var kern = SyntheticFontBuilder.KernFormat0([(0, 1, 42)]);

        return new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load WellFormedFont ExposesMetrics.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_WellFormedFont_ExposesMetrics()
    {
        var data = BuildWellFormedFont();

        var font = TrueTypeFont.Load(new MemoryStream(data));

        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(800, font.Ascender);
        Assert.Equal(-200, font.Descender);
        Assert.Equal(50, font.LineGap);
        Assert.Equal(2, font.GlyphCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load EndToEnd GlyphIndexOutlineAdvanceAndKerning.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_EndToEnd_GlyphIndexOutlineAdvanceAndKerning()
    {
        var data = BuildWellFormedFont();

        var font = TrueTypeFont.Load(new MemoryStream(data));
        var glyphIndex = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyphIndex);
        var advance = font.GetAdvanceWidth(glyphIndex);
        var kerning = font.GetKerning(0, glyphIndex);

        Assert.Equal(1, glyphIndex);
        Assert.Single(outline.Subpaths);
        Assert.Equal(500, advance);
        Assert.Equal(42, kerning);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load Path ReadsFromFile.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_Path_ReadsFromFile()
    {
        var data = BuildWellFormedFont();
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, data);

            var font = TrueTypeFont.Load(path);

            Assert.Equal(1000, font.UnitsPerEm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NullStream ThrowsArgumentNullException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Load((Stream)null!));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NullPath ThrowsArgumentNullException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Load((string)null!));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load EmptyPath ThrowsArgumentException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TrueTypeFont.Load(string.Empty));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load MissingRequiredTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_MissingRequiredTable_ThrowsInvalidDataException()
    {
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build(); // missing maxp/hhea/hmtx/loca/glyf

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load OttoFont ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_OttoFont_ThrowsInvalidDataException()
    {
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F)
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load MaxpVersion05 ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_MaxpVersion05_ThrowsInvalidDataException()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1, version: 0x00005000))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load ZeroUnitsPerEm ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_ZeroUnitsPerEm_ThrowsInvalidDataException()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(0, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load InvalidIndexToLocFormat ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_InvalidIndexToLocFormat_ThrowsInvalidDataException()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 2)) // only 0 or 1 are valid
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NoCmapOrKern FallsBackGracefully.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NoCmapOrKern_FallsBackGracefully()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        var font = TrueTypeFont.Load(new MemoryStream(data));

        Assert.Equal(0, font.GetGlyphIndex('A'));
        Assert.Equal(0, font.GetKerning(0, 0));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphOutline NegativeGlyphIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException()
    {
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphOutline GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetGlyphOutline(font.GlyphCount));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetAdvanceWidth GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetAdvanceWidth_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetAdvanceWidth(font.GlyphCount));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetKerning OutOfRangeGlyphIndex NeverThrows.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetKerning_OutOfRangeGlyphIndex_NeverThrows()
    {
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        var result = font.GetKerning(-1, 99999);

        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphIndex UnmappedCodepoint NeverThrows.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphIndex_UnmappedCodepoint_NeverThrows()
    {
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        var result = font.GetGlyphIndex(0x10FFFF);

        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TruncatedStream ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TruncatedStream_ThrowsInvalidDataException()
    {
        var data = BuildWellFormedFont();
        var truncated = data[..(data.Length / 2)];

        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(truncated)));
    }
}
