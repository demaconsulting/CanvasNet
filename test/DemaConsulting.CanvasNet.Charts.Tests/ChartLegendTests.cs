namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartLegend"/> and <see cref="ChartLegendPosition"/>.
/// </summary>
public class ChartLegendTests
{
    /// <summary>Proves the parameterless constructor defaults to a visible, right-positioned legend.</summary>
    [Fact]
    public void Constructor_Defaults_IsVisibleAndRightPositioned()
    {
        var legend = new ChartLegend();

        Assert.Equal(ChartLegendPosition.Right, legend.Position);
        Assert.True(legend.IsVisible);
    }

    /// <summary>Proves every defined <see cref="ChartLegendPosition"/> value is accepted.</summary>
    [Theory]
    [InlineData(ChartLegendPosition.Top)]
    [InlineData(ChartLegendPosition.Bottom)]
    [InlineData(ChartLegendPosition.Left)]
    [InlineData(ChartLegendPosition.Right)]
    [InlineData(ChartLegendPosition.None)]
    public void Constructor_EachDefinedPosition_IsAccepted(ChartLegendPosition position)
    {
        var legend = new ChartLegend(position);

        Assert.Equal(position, legend.Position);
    }

    /// <summary>Proves <see langword="false"/> isVisible is accepted and stored.</summary>
    [Fact]
    public void Constructor_IsVisibleFalse_IsStored()
    {
        var legend = new ChartLegend(isVisible: false);

        Assert.False(legend.IsVisible);
    }

    /// <summary>Proves an undefined <see cref="ChartLegendPosition"/> value is rejected.</summary>
    [Fact]
    public void Constructor_UndefinedPosition_ThrowsArgumentOutOfRangeException()
    {
        var undefined = (ChartLegendPosition)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartLegend(undefined));
    }
}
