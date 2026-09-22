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
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600, 700]);

        var reader = Parse(hhea, hmtx, 2);

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
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600, 700], tailLsbCount: 3);

        var reader = Parse(hhea, hmtx, 5);

        Assert.Equal(700, reader.GetAdvanceWidth(2));
        Assert.Equal(700, reader.GetAdvanceWidth(4));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse TruncatedHhea ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_TruncatedHhea_ThrowsInvalidDataException()
    {
        var hhea = new byte[10];

        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse NumOfLongHorMetricsZero ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_NumOfLongHorMetricsZero_ThrowsInvalidDataException()
    {
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 0);

        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse NumOfLongHorMetricsExceedsNumGlyphs ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_NumOfLongHorMetricsExceedsNumGlyphs_ThrowsInvalidDataException()
    {
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 10);

        Assert.Throws<InvalidDataException>(() => HmtxHheaReader.Parse(hhea, 0, hhea.Length, 0, 100, 2));
    }

    /// <summary>
    ///     Proves that HmtxHheaReader Parse TruncatedHmtx ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void HmtxHheaReader_Parse_TruncatedHmtx_ThrowsInvalidDataException()
    {
        var hhea = SyntheticFontBuilder.Hhea(2048, -512, 100, 2);
        var hmtx = SyntheticFontBuilder.Hmtx([600]); // only 1 entry, but hhea declares 2

        Assert.Throws<InvalidDataException>(() => Parse(hhea, hmtx, 2));
    }
}
