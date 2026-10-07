using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartSeries"/>.
/// </summary>
public class ChartSeriesTests
{
    /// <summary>Proves constructing with only a name and values sets those members and leaves the rest unset.</summary>
    [Fact]
    public void Constructor_NameAndValuesOnly_SetsMembersAndLeavesOptionalsUnset()
    {
        var series = new ChartSeries("Revenue", [1.0, 2.0, 3.0]);

        Assert.Equal("Revenue", series.Name);
        Assert.Equal([1.0, 2.0, 3.0], series.Values);
        Assert.Null(series.PointColors);
        Assert.Null(series.PointLabels);
        Assert.Null(series.Color);
    }

    /// <summary>Proves constructing with every optional member sets all of them.</summary>
    [Fact]
    public void Constructor_AllMembers_SetsAllMembers()
    {
        var red = new Rgba32(255, 0, 0, 255);
        var blue = new Rgba32(0, 0, 255, 255);

        var series = new ChartSeries(
            "Revenue",
            [1.0, 2.0],
            [red, blue],
            ["First", "Second"],
            red);

        Assert.Equal([red, blue], series.PointColors);
        Assert.Equal(["First", "Second"], series.PointLabels);
        Assert.Equal(red, series.Color);
    }

    /// <summary>Proves a null name is rejected.</summary>
    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ChartSeries(null!, [1.0]));
    }

    /// <summary>Proves an empty or whitespace-only name is rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => new ChartSeries(name, [1.0]));
    }

    /// <summary>Proves a null values argument is rejected.</summary>
    [Fact]
    public void Constructor_NullValues_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ChartSeries("Revenue", null!));
    }

    /// <summary>Proves an empty values argument is rejected.</summary>
    [Fact]
    public void Constructor_EmptyValues_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartSeries("Revenue", []));
    }

    /// <summary>Proves a non-finite entry in values is rejected.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonFiniteValueEntry_ThrowsArgumentException(double value)
    {
        Assert.Throws<ArgumentException>(() => new ChartSeries("Revenue", [1.0, value]));
    }

    /// <summary>Proves a point colors count mismatched with the values count is rejected.</summary>
    [Fact]
    public void Constructor_PointColorsCountMismatch_ThrowsArgumentException()
    {
        var red = new Rgba32(255, 0, 0, 255);

        Assert.Throws<ArgumentException>(() => new ChartSeries("Revenue", [1.0, 2.0], [red]));
    }

    /// <summary>Proves a point labels count mismatched with the values count is rejected.</summary>
    [Fact]
    public void Constructor_PointLabelsCountMismatch_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartSeries("Revenue", [1.0, 2.0], pointLabels: ["Only one"]));
    }

    /// <summary>Proves a null entry in point labels is rejected.</summary>
    [Fact]
    public void Constructor_NullPointLabelEntry_ThrowsArgumentException()
    {
        var labels = new List<string?> { "First", null };

        Assert.Throws<ArgumentException>(() => new ChartSeries("Revenue", [1.0, 2.0], pointLabels: labels!));
    }

    /// <summary>Proves mutating the caller's original values list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalValuesList_DoesNotAffectStoredSnapshot()
    {
        var original = new List<double> { 1.0, 2.0 };
        var series = new ChartSeries("Revenue", original);

        original.Add(3.0);
        original[0] = 99.0;

        Assert.Equal([1.0, 2.0], series.Values);
    }

    /// <summary>Proves mutating the caller's original point colors list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalPointColorsList_DoesNotAffectStoredSnapshot()
    {
        var red = new Rgba32(255, 0, 0, 255);
        var blue = new Rgba32(0, 0, 255, 255);
        var original = new List<Rgba32> { red, blue };
        var series = new ChartSeries("Revenue", [1.0, 2.0], original);

        original[0] = blue;

        Assert.Equal([red, blue], series.PointColors);
    }

    /// <summary>Proves mutating the caller's original point labels list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalPointLabelsList_DoesNotAffectStoredSnapshot()
    {
        var original = new List<string> { "First", "Second" };
        var series = new ChartSeries("Revenue", [1.0, 2.0], pointLabels: original);

        original[0] = "Changed";

        Assert.Equal(["First", "Second"], series.PointLabels);
    }
}
