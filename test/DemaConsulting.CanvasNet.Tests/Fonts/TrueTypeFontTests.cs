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
        // Arrange: build a complete, well-formed synthetic font
        var data = BuildWellFormedFont();

        // Act: load the font
        var font = TrueTypeFont.Load(new MemoryStream(data));

        // Assert: font-level metrics are exposed as configured
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
        // Arrange: build a complete, well-formed synthetic font
        var data = BuildWellFormedFont();

        // Act: load the font and query glyph index, outline, advance width, and kerning
        var font = TrueTypeFont.Load(new MemoryStream(data));
        var glyphIndex = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyphIndex);
        var advance = font.GetAdvanceWidth(glyphIndex);
        var kerning = font.GetKerning(0, glyphIndex);

        // Assert: every stage of the end-to-end pipeline returns the expected values
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
        // Arrange: write a well-formed font to a temporary file
        var data = BuildWellFormedFont();
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, data);

            // Act: load the font from the file path
            var font = TrueTypeFont.Load(path);

            // Assert: the font loads correctly from the file
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
        // Arrange/Act/Assert: loading from a null stream throws
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Load((Stream)null!));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NullPath ThrowsArgumentNullException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NullPath_ThrowsArgumentNullException()
    {
        // Arrange/Act/Assert: loading from a null path throws
        Assert.Throws<ArgumentNullException>(() => TrueTypeFont.Load((string)null!));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load EmptyPath ThrowsArgumentException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_EmptyPath_ThrowsArgumentException()
    {
        // Arrange/Act/Assert: loading from an empty path throws
        Assert.Throws<ArgumentException>(() => TrueTypeFont.Load(string.Empty));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load MissingRequiredTable ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_MissingRequiredTable_ThrowsInvalidDataException()
    {
        // Arrange: build a font missing maxp/hhea/hmtx/loca/glyf
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build(); // missing maxp/hhea/hmtx/loca/glyf

        // Act/Assert: loading the font with missing required tables throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load OttoFont ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_OttoFont_ThrowsInvalidDataException()
    {
        // Arrange: build a font with the unsupported 'OTTO' (CFF-flavored) sfnt version tag
        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F)
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .Build();

        // Act/Assert: loading the OTTO font throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load MaxpVersion05 ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_MaxpVersion05_ThrowsInvalidDataException()
    {
        // Arrange: build a font whose maxp table declares the unsupported version 0.5 (CFF-only)
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1, version: 0x00005000))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        // Act/Assert: loading the font with the unsupported maxp version throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load ZeroUnitsPerEm ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_ZeroUnitsPerEm_ThrowsInvalidDataException()
    {
        // Arrange: build a font whose head table declares zero unitsPerEm
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(0, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        // Act/Assert: loading the font with zero unitsPerEm throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load InvalidIndexToLocFormat ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_InvalidIndexToLocFormat_ThrowsInvalidDataException()
    {
        // Arrange: build a font whose head table declares an invalid indexToLocFormat (only 0 or 1 are valid)
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 2)) // only 0 or 1 are valid
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        // Act/Assert: loading the font with the invalid indexToLocFormat throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(data)));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NoCmapOrKern FallsBackGracefully.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NoCmapOrKern_FallsBackGracefully()
    {
        // Arrange: build a font with no cmap or kern tables
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        // Act: load the font and query codepoint lookup and kerning
        var font = TrueTypeFont.Load(new MemoryStream(data));

        // Assert: missing optional tables fall back gracefully to default values
        Assert.Equal(0, font.GetGlyphIndex('A'));
        Assert.Equal(0, font.GetKerning(0, 0));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphOutline NegativeGlyphIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: load a well-formed font
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        // Act/Assert: requesting a negative glyph index throws
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphOutline GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: load a well-formed font
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        // Act/Assert: requesting a glyph index at/beyond the glyph count throws
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetGlyphOutline(font.GlyphCount));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetAdvanceWidth GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetAdvanceWidth_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: load a well-formed font
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        // Act/Assert: requesting the advance width of an out-of-range glyph index throws
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetAdvanceWidth(font.GlyphCount));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetKerning OutOfRangeGlyphIndex NeverThrows.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetKerning_OutOfRangeGlyphIndex_NeverThrows()
    {
        // Arrange: load a well-formed font
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        // Act: query kerning with out-of-range glyph indices
        var result = font.GetKerning(-1, 99999);

        // Assert: out-of-range glyph indices resolve to zero rather than throwing
        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphIndex UnmappedCodepoint NeverThrows.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphIndex_UnmappedCodepoint_NeverThrows()
    {
        // Arrange: load a well-formed font
        var font = TrueTypeFont.Load(new MemoryStream(BuildWellFormedFont()));

        // Act: query the glyph index for a codepoint absent from the cmap
        var result = font.GetGlyphIndex(0x10FFFF);

        // Assert: the unmapped codepoint resolves to glyph index zero rather than throwing
        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphIndex MalformedCmapOutOfRangeGlyphId ClampsToZero.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphIndex_MalformedCmapOutOfRangeGlyphId_ClampsToZero()
    {
        // Arrange: build a well-formed font (two glyphs, GlyphCount == 2) whose cmap maps
        // codepoint 'A' to glyph index 999 - a malformed mapping outside [0, GlyphCount)
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 999)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .AddTable("cmap", cmap)
            .Build();

        // Act: load the font and resolve the malformed-mapped codepoint
        var font = TrueTypeFont.Load(new MemoryStream(data));
        var glyphIndex = font.GetGlyphIndex(65);

        // Assert: the out-of-range mapped glyph index is clamped to zero rather than propagated,
        // so it never breaks GetGlyphOutline's [0, GlyphCount) contract
        Assert.Equal(0, glyphIndex);
        font.GetGlyphOutline(glyphIndex); // must not throw
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TruncatedStream ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TruncatedStream_ThrowsInvalidDataException()
    {
        // Arrange: build a well-formed font, then truncate it to half its length
        var data = BuildWellFormedFont();
        var truncated = data[..(data.Length / 2)];

        // Act/Assert: loading the truncated stream throws
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new MemoryStream(truncated)));
    }
}
