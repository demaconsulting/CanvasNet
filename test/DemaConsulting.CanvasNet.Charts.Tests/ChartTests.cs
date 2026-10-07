using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="Chart"/>.
/// </summary>
public class ChartTests
{
    /// <summary>Proves constructing with only the required members sets them and leaves the rest unset.</summary>
    [Fact]
    public void Constructor_MinimalMembers_SetsRequiredAndLeavesOptionalsUnset()
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0, 3.0]);
        var chart = new Chart(ChartType.Bar, [series]);

        Assert.Equal(ChartType.Bar, chart.Type);
        Assert.Equal([series], chart.Series);
        Assert.Null(chart.CategoryAxis);
        Assert.Null(chart.ValueAxis);
        Assert.Null(chart.Legend);
        Assert.Null(chart.Title);
        Assert.Null(chart.ColorPalette);
    }

    /// <summary>Proves constructing with every optional member sets all of them.</summary>
    [Fact]
    public void Constructor_AllMembers_SetsAllMembers()
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0]);
        var categoryAxis = new ChartAxis(["Q1", "Q2"]);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var legend = new ChartLegend();
        var title = new ChartTitle("Revenue Chart");
        var palette = new Rgba32[] { new(255, 0, 0, 255) };

        var chart = new Chart(ChartType.Bar, [series], categoryAxis, valueAxis, legend, title, palette);

        Assert.Same(categoryAxis, chart.CategoryAxis);
        Assert.Same(valueAxis, chart.ValueAxis);
        Assert.Same(legend, chart.Legend);
        Assert.Same(title, chart.Title);
        Assert.Equal(palette, chart.ColorPalette);
    }

    /// <summary>Proves an undefined <see cref="ChartType"/> value is rejected.</summary>
    [Fact]
    public void Constructor_UndefinedType_ThrowsArgumentOutOfRangeException()
    {
        var series = new ChartSeries("Revenue", [1.0]);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Chart((ChartType)99, [series]));
    }

    /// <summary>Proves a null series argument is rejected.</summary>
    [Fact]
    public void Constructor_NullSeries_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Chart(ChartType.Bar, null!));
    }

    /// <summary>Proves an empty series argument is rejected.</summary>
    [Fact]
    public void Constructor_EmptySeries_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Chart(ChartType.Bar, []));
    }

    /// <summary>Proves a null entry in series is rejected.</summary>
    [Fact]
    public void Constructor_NullSeriesEntry_ThrowsArgumentException()
    {
        var series = new List<ChartSeries?> { new ChartSeries("Revenue", [1.0]), null };

        Assert.Throws<ArgumentException>(() => new Chart(ChartType.Bar, series!));
    }

    /// <summary>Proves category-based types accept a series whose values count matches the category axis label count.</summary>
    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    public void Constructor_CategoryBasedTypeWithMatchingLength_Succeeds(ChartType type)
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0]);
        var categoryAxis = new ChartAxis(["Q1", "Q2"]);

        var chart = new Chart(type, [series], categoryAxis);

        Assert.Equal(type, chart.Type);
    }

    /// <summary>Proves category-based types reject a series whose values count mismatches the category axis label count.</summary>
    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    public void Constructor_CategoryBasedTypeWithMismatchedLength_ThrowsArgumentException(ChartType type)
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0, 3.0]);
        var categoryAxis = new ChartAxis(["Q1", "Q2"]);

        Assert.Throws<ArgumentException>(() => new Chart(type, [series], categoryAxis));
    }

    /// <summary>Proves proportional-wedge types (pie/doughnut) are exempt from the series/category-axis length check.</summary>
    [Theory]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Doughnut)]
    public void Constructor_ProportionalType_IsExemptFromLengthCheck(ChartType type)
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0, 3.0]);
        var categoryAxis = new ChartAxis(["Q1", "Q2"]);

        var chart = new Chart(type, [series], categoryAxis);

        Assert.Equal(type, chart.Type);
    }

    /// <summary>Proves a category axis with no labels (a value-only axis) does not trigger the length check.</summary>
    [Fact]
    public void Constructor_CategoryAxisWithNoLabels_DoesNotTriggerLengthCheck()
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0, 3.0]);
        var categoryAxis = new ChartAxis(minimum: 0f, maximum: 10f);

        var chart = new Chart(ChartType.Bar, [series], categoryAxis);

        Assert.Equal(ChartType.Bar, chart.Type);
    }

    /// <summary>Proves an empty color palette is rejected.</summary>
    [Fact]
    public void Constructor_EmptyColorPalette_ThrowsArgumentException()
    {
        var series = new ChartSeries("Revenue", [1.0]);

        Assert.Throws<ArgumentException>(() => new Chart(ChartType.Bar, [series], colorPalette: []));
    }

    /// <summary>Proves mutating the caller's original series list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalSeriesList_DoesNotAffectStoredSnapshot()
    {
        var first = new ChartSeries("First", [1.0]);
        var second = new ChartSeries("Second", [2.0]);
        var original = new List<ChartSeries> { first };

        var chart = new Chart(ChartType.Bar, original);
        original.Add(second);

        Assert.Equal([first], chart.Series);
    }

    /// <summary>Proves mutating the caller's original color palette list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalColorPaletteList_DoesNotAffectStoredSnapshot()
    {
        var series = new ChartSeries("Revenue", [1.0]);
        var red = new Rgba32(255, 0, 0, 255);
        var blue = new Rgba32(0, 0, 255, 255);
        var original = new List<Rgba32> { red };

        var chart = new Chart(ChartType.Bar, [series], colorPalette: original);
        original.Add(blue);

        Assert.Equal([red], chart.ColorPalette);
    }
}
