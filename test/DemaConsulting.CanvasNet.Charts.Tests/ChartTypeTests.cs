namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for the <see cref="ChartType"/> enum.
/// </summary>
public class ChartTypeTests
{
    /// <summary>Proves every v1 <see cref="ChartType"/> member is a defined enum value.</summary>
    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Doughnut)]
    [InlineData(ChartType.Area)]
    public void AllV1Members_AreDefined(ChartType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    /// <summary>Proves an out-of-range cast value is not a defined <see cref="ChartType"/> value.</summary>
    [Fact]
    public void UndefinedValue_IsNotDefined()
    {
        var undefined = (ChartType)99;

        Assert.False(Enum.IsDefined(undefined));
    }

    /// <summary>Proves <see cref="Chart"/>'s constructor rejects an undefined <see cref="ChartType"/> value.</summary>
    [Fact]
    public void UndefinedValue_RejectedByChartConstructor()
    {
        var undefined = (ChartType)99;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Chart(undefined, [new ChartSeries("Series 1", [1.0])]));
    }
}
