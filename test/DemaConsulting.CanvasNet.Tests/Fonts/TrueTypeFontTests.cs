// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
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
        using var ms50 = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms50);

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
        using var ms70 = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms70);
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
        using var ms150 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms150));
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
        using var ms166 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms166));
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
        using var ms187 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms187));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TruncatedMaxpVersion10 ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TruncatedMaxpVersion10_ThrowsInvalidDataException()
    {
        // Arrange: build a font whose maxp table declares version 1.0 but supplies only the
        // 6-byte version+numGlyphs prefix, well short of the 32 bytes a version 1.0 maxp table
        // is required to contain
        var truncatedMaxp = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(truncatedMaxp, 0x00010000); // version 1.0
        SyntheticFontBuilder.WriteUInt16(truncatedMaxp, 1); // numGlyphs

        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", [.. truncatedMaxp])
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();

        // Act/Assert: loading the font with the truncated version 1.0 maxp table throws
        using var ms214 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms214));
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
        using var ms235 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms235));
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
        using var ms256 = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms256));
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
        using var ms277 = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms277);

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
        using var ms291 = new MemoryStream(BuildWellFormedFont());
        var font = TrueTypeFont.Load(ms291);

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
        using var ms304 = new MemoryStream(BuildWellFormedFont());
        var font = TrueTypeFont.Load(ms304);

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
        using var ms317 = new MemoryStream(BuildWellFormedFont());
        var font = TrueTypeFont.Load(ms317);

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
        using var ms330 = new MemoryStream(BuildWellFormedFont());
        var font = TrueTypeFont.Load(ms330);

        // Act: query kerning with out-of-range glyph indices
        var result = font.GetKerning(-1, 99999);

        // Assert: out-of-range glyph indices resolve to zero rather than throwing
        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetKerning GlyphIndexValidUshortButBeyondGlyphCount ReturnsZero.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetKerning_GlyphIndexValidUshortButBeyondGlyphCount_ReturnsZero()
    {
        // Arrange: build a font with a small GlyphCount (2) whose kern table nonetheless carries
        // a pair (100, 101) - both values are valid ushort glyph indices (so KernTable itself,
        // which only bounds against ushort.MaxValue, would happily resolve them), but neither
        // glyph exists in this particular font.
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var kern = SyntheticFontBuilder.KernFormat0([(100, 101, 42)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .AddTable("kern", kern)
            .Build();

        using var ms365 = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms365);

        // Act: query kerning for the pair recorded in the kern table
        var result = font.GetKerning(100, 101);

        // Assert: even though the pair is present in the kern table and both indices are valid
        // ushort values, neither glyph exists in this font (GlyphCount is 2), so the result is
        // zero rather than the recorded kerning value of 42
        Assert.Equal(0, result);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetGlyphIndex UnmappedCodepoint NeverThrows.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetGlyphIndex_UnmappedCodepoint_NeverThrows()
    {
        // Arrange: load a well-formed font
        using var ms383 = new MemoryStream(BuildWellFormedFont());
        var font = TrueTypeFont.Load(ms383);

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
        using var ms418 = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms418);
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
        using var ms438 = new MemoryStream(truncated);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms438));
    }

    /// <summary>
    ///     Builds a minimal, well-formed synthetic CFF/OTTO font with a single glyph, glyph 0
    ///     being a 10x10 square outline via a Type 2 charstring.
    /// </summary>
    private static byte[] BuildWellFormedCffFont()
    {
        var cs = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringOperator(cs, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringNumber(cs, 10);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 10);
        SyntheticFontBuilder.WriteCharstringNumber(cs, -10);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringOperator(cs, 5); // rlineto
        SyntheticFontBuilder.WriteCharstringOperator(cs, 14); // endchar

        var cff = SyntheticFontBuilder.Cff([[.. cs]]);

        return new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F) // 'OTTO'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1, version: 0x00005000))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("CFF ", cff)
            .Build();
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load OttoFontWithCffTable Succeeds.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_OttoFontWithCffTable_Succeeds()
    {
        // Arrange: build a well-formed synthetic OTTO/CFF font
        var data = BuildWellFormedCffFont();

        // Act: load the font and decode its single glyph's outline
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var outline = font.GetGlyphOutline(0);

        // Assert: the font loads successfully and the CFF outline is decoded
        Assert.Equal(1, font.GlyphCount);
        Assert.Single(outline.Subpaths);
        Assert.Contains(outline.Subpaths[0].Commands, c => c.Type == PathCommandType.LineTo);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load CffGlyphCountMismatchesMaxp ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_CffGlyphCountMismatchesMaxp_ThrowsInvalidDataException()
    {
        // Arrange: build an OTTO/CFF font whose maxp.numGlyphs (2) does not match the CFF
        // CharStrings INDEX's actual glyph count (1)
        var cs = new List<byte>();
        SyntheticFontBuilder.WriteCharstringOperator(cs, 14); // endchar
        var cff = SyntheticFontBuilder.Cff([[.. cs]]);

        var data = new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F)
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2, version: 0x00005000))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500, 500]))
            .AddTable("CFF ", cff)
            .Build();

        // Act/Assert: loading the font with the mismatched glyph count throws
        using var ms = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms));
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetFaceCount PlainSfnt ReturnsOne.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetFaceCount_PlainSfnt_ReturnsOne()
    {
        // Arrange: a well-formed, single-face (non-collection) font
        var data = BuildWellFormedFont();

        // Act: query the face count
        using var ms = new MemoryStream(data);
        var faceCount = TrueTypeFont.GetFaceCount(ms);

        // Assert: an ordinary SFNT font always reports exactly one face
        Assert.Equal(1, faceCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetFaceCount TtcContainer ReturnsFaceCount.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetFaceCount_TtcContainer_ReturnsFaceCount()
    {
        // Arrange: a synthetic 2-face ttcf container wrapping two independent, well-formed fonts
        var ttc = SyntheticFontBuilder.Ttc([BuildWellFormedFont(), BuildWellFormedCffFont()]);

        // Act: query the face count
        using var ms = new MemoryStream(ttc);
        var faceCount = TrueTypeFont.GetFaceCount(ms);

        // Assert: both faces are reported
        Assert.Equal(2, faceCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetFaceCount Path ReadsFromFile.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetFaceCount_Path_ReadsFromFile()
    {
        // Arrange: write a well-formed font to a temporary file
        var data = BuildWellFormedFont();
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, data);

            // Act: query the face count from the file path
            var faceCount = TrueTypeFont.GetFaceCount(path);

            // Assert: an ordinary SFNT font always reports exactly one face
            Assert.Equal(1, faceCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TtcContainer DefaultsToFaceZero.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TtcContainer_DefaultsToFaceZero()
    {
        // Arrange: a synthetic 2-face ttcf container; face 0 is the TrueType font, face 1 is CFF
        var ttc = SyntheticFontBuilder.Ttc([BuildWellFormedFont(), BuildWellFormedCffFont()]);

        // Act: load without an explicit face index
        using var ms = new MemoryStream(ttc);
        var font = TrueTypeFont.Load(ms);

        // Assert: face 0 (the TrueType font, GlyphCount 2) was selected, not face 1
        Assert.Equal(2, font.GlyphCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TtcContainer ExplicitFaceIndex SelectsThatFace.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TtcContainer_ExplicitFaceIndex_SelectsThatFace()
    {
        // Arrange: a synthetic 2-face ttcf container; face 0 is the TrueType font, face 1 is CFF
        var ttc = SyntheticFontBuilder.Ttc([BuildWellFormedFont(), BuildWellFormedCffFont()]);

        // Act: load face 1 explicitly
        using var ms = new MemoryStream(ttc);
        var font = TrueTypeFont.Load(ms, 1);

        // Assert: face 1 (the CFF font, GlyphCount 1) was selected
        Assert.Equal(1, font.GlyphCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TtcContainer FaceIndexOutOfRange ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TtcContainer_FaceIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a synthetic 2-face ttcf container
        var ttc = SyntheticFontBuilder.Ttc([BuildWellFormedFont(), BuildWellFormedCffFont()]);

        // Act/Assert: an out-of-range face index throws
        using var ms = new MemoryStream(ttc);
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Load(ms, 2));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NonTtcFont FaceIndexOne ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NonTtcFont_FaceIndexOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: an ordinary, single-face (non-collection) font
        var data = BuildWellFormedFont();

        // Act/Assert: requesting face index 1 on a single-face file throws
        using var ms = new MemoryStream(data);
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Load(ms, 1));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NonTtcFont FaceIndexZero BehavesIdenticallyToLoad.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NonTtcFont_FaceIndexZero_BehavesIdenticallyToLoad()
    {
        // Arrange: an ordinary, single-face (non-collection) font
        var data = BuildWellFormedFont();

        // Act: load with explicit face index 0
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms, 0);

        // Assert: behaves identically to the parameterless Load
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(2, font.GlyphCount);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load TtcContainer Path FaceIndex ReadsFromFile.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_TtcContainer_Path_FaceIndex_ReadsFromFile()
    {
        // Arrange: write a synthetic 2-face ttcf container to a temporary file
        var ttc = SyntheticFontBuilder.Ttc([BuildWellFormedFont(), BuildWellFormedCffFont()]);
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, ttc);

            // Act: load face 1 explicitly from the file path
            var font = TrueTypeFont.Load(path, 1);

            // Assert: face 1 (the CFF font, GlyphCount 1) was selected
            Assert.Equal(1, font.GlyphCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load NegativeFaceIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_NegativeFaceIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: an ordinary, well-formed font
        var data = BuildWellFormedFont();

        // Act/Assert: a negative face index throws
        using var ms = new MemoryStream(data);
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeFont.Load(ms, -1));
    }

    /// <summary>
    ///     Proves that TrueTypeFont Load MalformedTtcHeader ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Load_MalformedTtcHeader_ThrowsInvalidDataException()
    {
        // Arrange: a 'ttcf'-tagged file too short to contain its declared face offset table
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt32(buf, 0x74746366); // 'ttcf'
        SyntheticFontBuilder.WriteUInt16(buf, 1);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt32(buf, 2); // numFonts = 2, but no offsets follow

        // Act/Assert: loading the malformed ttcf header throws
        using var ms = new MemoryStream(buf.ToArray());
        Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(ms));
    }

    /// <summary>
    ///     Builds a well-formed synthetic font (as <see cref="BuildWellFormedFont"/>) that also
    ///     carries the given optional <c>name</c>/<c>OS/2</c>/<c>post</c> tables and
    ///     <c>head.macStyle</c> value, for exercising <see cref="TrueTypeFont.GetNameInfo"/> and
    ///     the <see cref="TrueTypeFont.IsBold"/>/<see cref="TrueTypeFont.IsItalic"/>/
    ///     <see cref="TrueTypeFont.IsFixedPitch"/> properties end to end.
    /// </summary>
    private static byte[] BuildFontWithNameAndStyle(
        byte[]? name = null, byte[]? os2 = null, byte[]? post = null, int macStyle = 0)
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);

        var builder = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0, macStyle))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([glyph.Length], longFormat: false))
            .AddTable("glyf", glyph);

        if (name != null)
        {
            builder.AddTable("name", name);
        }

        if (os2 != null)
        {
            builder.AddTable("OS/2", os2);
        }

        if (post != null)
        {
            builder.AddTable("post", post);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo TypographicNamesPresent PrefersNameId16And17.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_TypographicNamesPresent_PrefersNameId16And17()
    {
        // Arrange: a font whose name table has both standard (1/2) and typographic (16/17) names
        var name = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Standard Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 2, "Standard Subfamily"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 16, "Typographic Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 17, "Typographic Subfamily"),
        ]);
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the typographic names are preferred over the standard names
        Assert.Equal("Typographic Family", info.FamilyName);
        Assert.Equal("Typographic Subfamily", info.SubfamilyName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo TypographicNamesAbsent FallsBackToNameId1And2.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_TypographicNamesAbsent_FallsBackToNameId1And2()
    {
        // Arrange: a font whose name table has only standard (1/2) names
        var name = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Standard Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 2, "Standard Subfamily"),
        ]);
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the standard names are resolved
        Assert.Equal("Standard Family", info.FamilyName);
        Assert.Equal("Standard Subfamily", info.SubfamilyName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo WindowsAndMacintoshRecordsPresent PrefersWindowsRecord.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_WindowsAndMacintoshRecordsPresent_PrefersWindowsRecord()
    {
        // Arrange: a font whose name table has both a Windows and a Macintosh record for nameID 1
        var name = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Windows Family"),
            new SyntheticFontBuilder.NameRecord(1, 0, 0, 1, "Macintosh Family"),
        ]);
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the Windows record is preferred
        Assert.Equal("Windows Family", info.FamilyName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo OnlyMacintoshRecordPresent ResolvesFromMacintoshRecord.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_OnlyMacintoshRecordPresent_ResolvesFromMacintoshRecord()
    {
        // Arrange: a font whose name table has only a Macintosh record for nameID 1
        var name = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(1, 0, 0, 1, "Macintosh Only Family"),
        ]);
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the Macintosh record resolves as a fallback
        Assert.Equal("Macintosh Only Family", info.FamilyName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo PostScriptNameRecordMissing ReturnsNullPostScriptName.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_PostScriptNameRecordMissing_ReturnsNullPostScriptName()
    {
        // Arrange: a font whose name table has a family name but no PostScript name (nameID 6)
        var name = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Some Family"),
        ]);
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the present field resolves, and the missing field is null
        Assert.Equal("Some Family", info.FamilyName);
        Assert.Null(info.PostScriptName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo NoNameTable ReturnsAllNullFontNameInfo.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_NoNameTable_ReturnsAllNullFontNameInfo()
    {
        // Arrange: a well-formed font with no name table at all
        var data = BuildFontWithNameAndStyle();

        // Act: load the font and resolve its name info
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: every field is null
        Assert.Equal(default, info);
        Assert.Null(info.FamilyName);
        Assert.Null(info.SubfamilyName);
        Assert.Null(info.FullName);
        Assert.Null(info.PostScriptName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont GetNameInfo MalformedNameRecord IgnoresRecordWithoutThrowing.
    /// </summary>
    [Fact]
    public void TrueTypeFont_GetNameInfo_MalformedNameRecord_IgnoresRecordWithoutThrowing()
    {
        // Arrange: hand-build a name table with one well-formed record and one record whose
        // string bytes fall outside the table's bounds
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // format
        SyntheticFontBuilder.WriteUInt16(buf, 2); // count
        SyntheticFontBuilder.WriteUInt16(buf, 6 + 2 * 12); // stringOffset

        SyntheticFontBuilder.WriteUInt16(buf, 3); // platformID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // encodingID
        SyntheticFontBuilder.WriteUInt16(buf, 0x0409); // languageID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nameID
        SyntheticFontBuilder.WriteUInt16(buf, 4); // length
        SyntheticFontBuilder.WriteUInt16(buf, 0); // offset

        SyntheticFontBuilder.WriteUInt16(buf, 3); // platformID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // encodingID
        SyntheticFontBuilder.WriteUInt16(buf, 0x0409); // languageID
        SyntheticFontBuilder.WriteUInt16(buf, 4); // nameID (full name)
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // length: absurdly large
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // offset: absurdly large

        buf.AddRange(System.Text.Encoding.BigEndianUnicode.GetBytes("OK"));
        var name = buf.ToArray();
        var data = BuildFontWithNameAndStyle(name: name);

        // Act: load the font and resolve its name info - never throws despite the malformed
        // second record
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);
        var info = font.GetNameInfo();

        // Assert: the well-formed record still resolves, and the malformed record is ignored
        Assert.Equal("OK", info.FamilyName);
        Assert.Null(info.FullName);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsBold Os2FsSelectionBoldBitSet ReturnsTrue.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsBold_Os2FsSelectionBoldBitSet_ReturnsTrue()
    {
        // Arrange: a font whose OS/2 table sets only the BOLD fsSelection bit
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0x20);
        var data = BuildFontWithNameAndStyle(os2: os2);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsBold is true, IsItalic is false
        Assert.True(font.IsBold);
        Assert.False(font.IsItalic);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsItalic Os2FsSelectionItalicBitSet ReturnsTrue.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsItalic_Os2FsSelectionItalicBitSet_ReturnsTrue()
    {
        // Arrange: a font whose OS/2 table sets only the ITALIC fsSelection bit
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 400, fsSelection: 0x1);
        var data = BuildFontWithNameAndStyle(os2: os2);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsItalic is true, IsBold is false
        Assert.True(font.IsItalic);
        Assert.False(font.IsBold);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsBold Os2WeightClassAtLeast600 ReturnsTrue.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsBold_Os2WeightClassAtLeast600_ReturnsTrue()
    {
        // Arrange: a font whose OS/2 table declares a heavy usWeightClass with no fsSelection bits
        var os2 = SyntheticFontBuilder.Os2(usWeightClass: 700, fsSelection: 0);
        var data = BuildFontWithNameAndStyle(os2: os2);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsBold is true purely from the weight class
        Assert.True(font.IsBold);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsItalic PostItalicAngleNonZero ReturnsTrue.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsItalic_PostItalicAngleNonZero_ReturnsTrue()
    {
        // Arrange: a font with a post table declaring a nonzero italicAngle and no OS/2 table
        var post = SyntheticFontBuilder.Post(isFixedPitch: 0, italicAngle: -12.0);
        var data = BuildFontWithNameAndStyle(post: post);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsItalic is true purely from the post table's italic angle
        Assert.True(font.IsItalic);
    }

    /// <summary>
    ///     Proves that TrueTypeFont Style Os2TableAbsent FallsBackToHeadMacStyle.
    /// </summary>
    [Fact]
    public void TrueTypeFont_Style_Os2TableAbsent_FallsBackToHeadMacStyle()
    {
        // Arrange: a font with no OS/2 or post table at all, but head.macStyle's Bold and Italic
        // bits both set
        const int macStyle = 0x1 | 0x2;
        var data = BuildFontWithNameAndStyle(macStyle: macStyle);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: both IsBold and IsItalic are true, derived purely from head.macStyle
        Assert.True(font.IsBold);
        Assert.True(font.IsItalic);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsFixedPitch PostIsFixedPitchNonZero ReturnsTrue.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsFixedPitch_PostIsFixedPitchNonZero_ReturnsTrue()
    {
        // Arrange: a font whose post table declares isFixedPitch
        var post = SyntheticFontBuilder.Post(isFixedPitch: 1);
        var data = BuildFontWithNameAndStyle(post: post);

        // Act: load the font
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsFixedPitch is true
        Assert.True(font.IsFixedPitch);
    }

    /// <summary>
    ///     Proves that TrueTypeFont IsFixedPitch PostTableAbsent ReturnsFalseWithoutThrowing.
    /// </summary>
    [Fact]
    public void TrueTypeFont_IsFixedPitch_PostTableAbsent_ReturnsFalseWithoutThrowing()
    {
        // Arrange: a well-formed font with no post table at all
        var data = BuildFontWithNameAndStyle();

        // Act: load the font (never throws over the missing post table)
        using var ms = new MemoryStream(data);
        var font = TrueTypeFont.Load(ms);

        // Assert: IsFixedPitch is false
        Assert.False(font.IsFixedPitch);
    }
}
