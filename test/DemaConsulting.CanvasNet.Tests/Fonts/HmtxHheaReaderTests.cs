// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="HmtxHheaReader"/>.
/// </summary>
public class HmtxHheaReaderTests
{
    /// <summary>
    ///     Packs an already-encoded <c>hhea</c> byte array and an already-encoded <c>hmtx</c> byte
    ///     array into a single combined buffer (the two tables are independent regions of the
    ///     same font file, per the real <see cref="HmtxHheaReader.Parse"/> contract) and parses it.
    /// </summary>
    private static HmtxHheaReader Parse(byte[] hhea, byte[] hmtx, int numGlyphs)
    {
        var data = new byte[hhea.Length + hmtx.Length];
        hhea.CopyTo(data, 0);
        hmtx.CopyTo(data, hhea.Length);
        return HmtxHheaReader.Parse(data, 0, hhea.Length, hhea.Length, hmtx.Length, numGlyphs);
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse ExposesMetrics.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_ExposesMetrics()
    {
        // Arrange: build hhea/hmtx tables for two glyphs with distinct advance widths
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600, 700]);

        // Act: parse the combined hhea/hmtx tables
        var reader = Parse(hhea, hmtx, 2);

        // Assert: the ascender/descender/line gap and each glyph's advance width are exposed
        Assert.Equal(2048, reader.Ascender);
        Assert.Equal(-512, reader.Descender);
        Assert.Equal(100, reader.LineGap);
        Assert.Equal(600, reader.GetAdvanceWidth(0));
        Assert.Equal(700, reader.GetAdvanceWidth(1));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader GetAdvanceWidth TailGlyph ReusesLastEntry.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_GetAdvanceWidth_TailGlyph_ReusesLastEntry()
    {
        // Arrange: build hmtx with two width entries followed by trailing left-side-bearing-only entries
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600, 700], tailLsbCount: 3);

        // Act: parse the combined hhea/hmtx tables
        var reader = Parse(hhea, hmtx, 5);

        // Assert: glyphs beyond the last width entry reuse that last entry's advance width
        Assert.Equal(700, reader.GetAdvanceWidth(2));
        Assert.Equal(700, reader.GetAdvanceWidth(4));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse TruncatedHhea ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_TruncatedHhea_ThrowsInvalidDataException()
    {
        // Arrange: build an hhea buffer too short to contain a valid header
        var hhea = new byte[10];

        // Act/Assert: parsing the truncated hhea table throws
        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse NumOfLongHorMetricsZero ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_NumOfLongHorMetricsZero_ThrowsInvalidDataException()
    {
        // Arrange: build an hhea table declaring zero long horizontal metrics entries
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 0);

        // Act/Assert: parsing with zero numOfLongHorMetrics throws
        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse NumOfLongHorMetricsExceedsNumGlyphs ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_NumOfLongHorMetricsExceedsNumGlyphs_ThrowsInvalidDataException()
    {
        // Arrange: build an hhea table declaring more long horizontal metrics than glyphs exist
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 10);

        // Act/Assert: parsing with numOfLongHorMetrics exceeding numGlyphs throws
        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse TruncatedHmtx ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_TruncatedHmtx_ThrowsInvalidDataException()
    {
        // Arrange: build an hmtx table with fewer entries than the hhea table declares
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600]); // only 1 entry, but hhea declares 2

        // Act/Assert: parsing the truncated hmtx table throws
        Assert.Throws<InvalidDataException>(() => Parse(hhea, hmtx, 2));
    }
}
