using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for the internal <see cref="OpenTypeCoverage"/> helper of the
///     <see cref="TrueTypeFont"/> unit.
/// </summary>
public class OpenTypeCoverageTests
{
    /// <summary>
    ///     Proves that a format-1 coverage maps each listed glyph to its array position.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_IndexOf_Format1_ReturnsArrayPosition()
    {
        // Arrange: a format-1 coverage of three glyphs
        var data = SyntheticFontBuilder.Coverage([5, 9, 40], 1);

        // Act
        var coverage = OpenTypeCoverage.Parse(data, 0, data.Length);

        // Assert: each glyph maps to its position, and the index count is the glyph count
        Assert.Equal(0, coverage.IndexOf(5));
        Assert.Equal(1, coverage.IndexOf(9));
        Assert.Equal(2, coverage.IndexOf(40));
        Assert.Equal(3, coverage.IndexCount);
    }

    /// <summary>
    ///     Proves that a format-2 coverage maps a glyph inside a range to the range's start index
    ///     plus its distance from the range start, including a range whose start index is not
    ///     the count of the ranges before it.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_IndexOf_Format2_ReturnsRangeIndex()
    {
        // Arrange: two ranges, 10-12 starting at index 0 and 20-21 starting at index 5
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 2);
        SyntheticFontBuilder.WriteUInt16(buf, 2);
        foreach (var value in new[] { 10, 12, 0, 20, 21, 5 })
        {
            SyntheticFontBuilder.WriteUInt16(buf, value);
        }

        var data = buf.ToArray();

        // Act
        var coverage = OpenTypeCoverage.Parse(data, 0, data.Length);

        // Assert: indexes follow each range's start index
        Assert.Equal(0, coverage.IndexOf(10));
        Assert.Equal(2, coverage.IndexOf(12));
        Assert.Equal(5, coverage.IndexOf(20));
        Assert.Equal(6, coverage.IndexOf(21));
        Assert.Equal(7, coverage.IndexCount);
    }

    /// <summary>
    ///     Proves that a glyph not covered (between, before, or after the ranges, or outside the
    ///     16-bit glyph range) maps to -1 without throwing.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_IndexOf_AbsentGlyph_ReturnsMinusOne()
    {
        // Arrange: a format-2 coverage of 10-12 and 20-21, and the empty coverage
        var data = SyntheticFontBuilder.Coverage([10, 11, 12, 20, 21], 2);
        var coverage = OpenTypeCoverage.Parse(data, 0, data.Length);

        // Act / Assert: uncovered glyphs give -1
        Assert.Equal(-1, coverage.IndexOf(9));
        Assert.Equal(-1, coverage.IndexOf(15));
        Assert.Equal(-1, coverage.IndexOf(22));
        Assert.Equal(-1, coverage.IndexOf(-1));
        Assert.Equal(-1, coverage.IndexOf(70000));
        Assert.Equal(-1, OpenTypeCoverage.Empty.IndexOf(10));
        Assert.Equal(0, OpenTypeCoverage.Empty.IndexCount);
    }

    /// <summary>
    ///     Proves that a coverage of an undefined format is rejected.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_Parse_UnknownFormat_ThrowsInvalidDataException()
    {
        // Arrange: format 3 with no records
        byte[] data = [0, 3, 0, 0];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => OpenTypeCoverage.Parse(data, 0, data.Length));
    }

    /// <summary>
    ///     Proves that a format-2 range that ends before it starts is rejected.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_Parse_InvertedRange_ThrowsInvalidDataException()
    {
        // Arrange: one range from glyph 20 to glyph 10
        var buf = new List<byte>();
        foreach (var value in new[] { 2, 1, 20, 10, 0 })
        {
            SyntheticFontBuilder.WriteUInt16(buf, value);
        }

        var data = buf.ToArray();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => OpenTypeCoverage.Parse(data, 0, data.Length));
    }

    /// <summary>
    ///     Proves that a coverage whose declared count (or header) does not fit before the end of
    ///     its enclosing table is rejected, including a maximal count.
    /// </summary>
    [Fact]
    public void OpenTypeCoverage_Parse_CountBeyondEnd_ThrowsInvalidDataException()
    {
        // Arrange: format 1 and format 2 headers declaring 0xFFFF records with none present
        byte[] format1 = [0, 1, 0xFF, 0xFF, 0, 1];
        byte[] format2 = [0, 2, 0xFF, 0xFF, 0, 1, 0, 2, 0, 0];

        // Act / Assert: the records do not fit, nor does a header beyond the end
        Assert.Throws<InvalidDataException>(() => OpenTypeCoverage.Parse(format1, 0, format1.Length));
        Assert.Throws<InvalidDataException>(() => OpenTypeCoverage.Parse(format2, 0, format2.Length));
        Assert.Throws<InvalidDataException>(() => OpenTypeCoverage.Parse(format1, 4, format1.Length));
    }
}
