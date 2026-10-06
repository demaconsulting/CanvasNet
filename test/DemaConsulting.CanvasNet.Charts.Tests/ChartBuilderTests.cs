using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartBuilder"/>.
/// </summary>
public class ChartBuilderTests
{
    /// <summary>Proves every fluent method returns the same <see cref="ChartBuilder"/> instance, enabling chaining.</summary>
    [Fact]
    public void FluentMethods_EachReturnSameBuilderInstance()
    {
        var builder = new ChartBuilder();

        Assert.Same(builder, builder.OfType(ChartType.Bar));
        Assert.Same(builder, builder.AddSeries("Revenue", [1.0]));
        Assert.Same(builder, builder.AddSeries(new ChartSeries("Other", [2.0])));
        Assert.Same(builder, builder.WithCategoryAxis(new ChartAxis(["A", "B"])));
        Assert.Same(builder, builder.WithCategoryAxis(["A", "B"]));
        Assert.Same(builder, builder.WithValueAxis(new ChartAxis(minimum: 0f, maximum: 1f)));
        Assert.Same(builder, builder.WithValueAxis(0f, 1f));
        Assert.Same(builder, builder.WithLegend(new ChartLegend()));
        Assert.Same(builder, builder.WithLegend(ChartLegendPosition.Top));
        Assert.Same(builder, builder.WithTitle("Title"));
        Assert.Same(builder, builder.WithColorPalette([new Rgba32(1, 2, 3, 255)]));
    }

    /// <summary>Proves a fully configured builder produces a <see cref="Chart"/> reflecting every configured member.</summary>
    [Fact]
    public void Build_FullConfiguration_ProducesChartRoundTrippingBuilderInputs()
    {
        var chart = new ChartBuilder()
            .OfType(ChartType.Bar)
            .WithCategoryAxis(["Q1", "Q2"])
            .AddSeries("Revenue", [120.0, 150.0])
            .WithValueAxis(0f, 200f, 50f, "Dollars")
            .WithLegend(ChartLegendPosition.Bottom, isVisible: true)
            .WithTitle("Quarterly Revenue", 24f)
            .WithColorPalette([new Rgba32(255, 0, 0, 255)])
            .Build();

        Assert.Equal(ChartType.Bar, chart.Type);
        Assert.Single(chart.Series);
        Assert.Equal("Revenue", chart.Series[0].Name);
        Assert.Equal([120.0, 150.0], chart.Series[0].Values);
        Assert.Equal(["Q1", "Q2"], chart.CategoryAxis!.Labels);
        Assert.Equal(0f, chart.ValueAxis!.Minimum);
        Assert.Equal(200f, chart.ValueAxis.Maximum);
        Assert.Equal(50f, chart.ValueAxis.TickInterval);
        Assert.Equal("Dollars", chart.ValueAxis.Title);
        Assert.Equal(ChartLegendPosition.Bottom, chart.Legend!.Position);
        Assert.True(chart.Legend.IsVisible);
        Assert.Equal("Quarterly Revenue", chart.Title!.Text);
        Assert.Equal(24f, chart.Title.FontSize);
        Assert.Equal([new Rgba32(255, 0, 0, 255)], chart.ColorPalette);
    }

    /// <summary>Proves multiple series added via the pre-built <see cref="ChartSeries"/> overload preserve insertion order.</summary>
    [Fact]
    public void Build_WithMultipleSeriesAddedViaChartSeriesOverload_PreservesOrder()
    {
        var chart = new ChartBuilder()
            .OfType(ChartType.Line)
            .AddSeries(new ChartSeries("First", [1.0]))
            .AddSeries(new ChartSeries("Second", [2.0]))
            .Build();

        Assert.Equal(["First", "Second"], chart.Series.Select(s => s.Name));
    }

    /// <summary>Proves calling <see cref="ChartBuilder.Build"/> without ever calling <see cref="ChartBuilder.OfType"/> throws.</summary>
    [Fact]
    public void Build_TypeNeverSet_ThrowsInvalidOperationException()
    {
        var builder = new ChartBuilder().AddSeries("Revenue", [1.0]);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    /// <summary>Proves calling <see cref="ChartBuilder.Build"/> without ever adding a series throws.</summary>
    [Fact]
    public void Build_NoSeriesAdded_ThrowsInvalidOperationException()
    {
        var builder = new ChartBuilder().OfType(ChartType.Bar);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    /// <summary>Proves an invalid series name propagates the same exception <see cref="ChartSeries"/>'s constructor would throw.</summary>
    [Fact]
    public void AddSeries_InvalidName_PropagatesChartSeriesConstructorException()
    {
        var builder = new ChartBuilder().OfType(ChartType.Bar);

        Assert.Throws<ArgumentException>(() => builder.AddSeries("", [1.0]));
    }

    /// <summary>Proves a null pre-built series is rejected.</summary>
    [Fact]
    public void AddSeries_NullChartSeries_PropagatesArgumentNullException()
    {
        var builder = new ChartBuilder().OfType(ChartType.Bar);

        Assert.Throws<ArgumentNullException>(() => builder.AddSeries((ChartSeries)null!));
    }

    /// <summary>Proves a null pre-built category axis is rejected.</summary>
    [Fact]
    public void WithCategoryAxis_NullChartAxis_PropagatesArgumentNullException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithCategoryAxis((ChartAxis)null!));
    }

    /// <summary>Proves a null pre-built value axis is rejected.</summary>
    [Fact]
    public void WithValueAxis_NullChartAxis_PropagatesArgumentNullException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithValueAxis((ChartAxis)null!));
    }

    /// <summary>Proves an invalid value-axis range propagates the same exception <see cref="ChartAxis"/>'s constructor would throw.</summary>
    [Fact]
    public void WithValueAxis_InvalidRange_PropagatesChartAxisConstructorException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithValueAxis(10f, 5f));
    }

    /// <summary>Proves a null pre-built legend is rejected.</summary>
    [Fact]
    public void WithLegend_NullChartLegend_PropagatesArgumentNullException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithLegend((ChartLegend)null!));
    }

    /// <summary>Proves whitespace-only title text propagates the same exception <see cref="ChartTitle"/>'s constructor would throw.</summary>
    [Fact]
    public void WithTitle_WhitespaceText_PropagatesChartTitleConstructorException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentException>(() => builder.WithTitle("   "));
    }

    /// <summary>Proves a null color palette is rejected.</summary>
    [Fact]
    public void WithColorPalette_Null_ThrowsArgumentNullException()
    {
        var builder = new ChartBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithColorPalette(null!));
    }

    /// <summary>Proves a series/category-axis length mismatch surfaces at <see cref="ChartBuilder.Build"/>, from <see cref="Chart"/>'s own constructor.</summary>
    [Fact]
    public void Build_SeriesLengthMismatchedWithCategoryAxis_PropagatesChartConstructorException()
    {
        var builder = new ChartBuilder()
            .OfType(ChartType.Bar)
            .WithCategoryAxis(["Q1", "Q2"])
            .AddSeries("Revenue", [1.0, 2.0, 3.0]);

        Assert.Throws<ArgumentException>(() => builder.Build());
    }
}
