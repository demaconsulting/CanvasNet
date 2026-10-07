namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartAxis"/>.
/// </summary>
public class ChartAxisTests
{
    /// <summary>Proves constructing with only labels sets labels and leaves the range unset.</summary>
    [Fact]
    public void Constructor_LabelsOnly_SetsLabelsAndLeavesRangeUnset()
    {
        var axis = new ChartAxis(["Q1", "Q2", "Q3"]);

        Assert.Equal(["Q1", "Q2", "Q3"], axis.Labels);
        Assert.Null(axis.Minimum);
        Assert.Null(axis.Maximum);
        Assert.Null(axis.TickInterval);
        Assert.Null(axis.Title);
    }

    /// <summary>Proves constructing with only a value range sets the range and leaves labels unset.</summary>
    [Fact]
    public void Constructor_ValueRangeOnly_SetsRangeAndLeavesLabelsUnset()
    {
        var axis = new ChartAxis(minimum: 0f, maximum: 100f, tickInterval: 10f);

        Assert.Null(axis.Labels);
        Assert.Equal(0f, axis.Minimum);
        Assert.Equal(100f, axis.Maximum);
        Assert.Equal(10f, axis.TickInterval);
    }

    /// <summary>Proves constructing with labels, a full range, and a title sets every member.</summary>
    [Fact]
    public void Constructor_LabelsAndRangeAndTitle_SetsAllMembers()
    {
        var axis = new ChartAxis(["A", "B"], 0f, 10f, 1f, "Value");

        Assert.Equal(["A", "B"], axis.Labels);
        Assert.Equal(0f, axis.Minimum);
        Assert.Equal(10f, axis.Maximum);
        Assert.Equal(1f, axis.TickInterval);
        Assert.Equal("Value", axis.Title);
    }

    /// <summary>Proves an empty-string label entry is accepted as a legitimate blank label.</summary>
    [Fact]
    public void Constructor_EmptyStringLabel_IsAccepted()
    {
        var axis = new ChartAxis(["", "B"]);

        Assert.Equal(["", "B"], axis.Labels);
    }

    /// <summary>Proves a null label entry is rejected.</summary>
    [Fact]
    public void Constructor_NullLabelEntry_ThrowsArgumentException()
    {
        var labels = new List<string?> { "A", null };

        Assert.Throws<ArgumentException>(() => new ChartAxis(labels!));
    }

    /// <summary>Proves a non-finite minimum is rejected.</summary>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_NonFiniteMinimum_ThrowsArgumentOutOfRangeException(float minimum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartAxis(minimum: minimum));
    }

    /// <summary>Proves a non-finite maximum is rejected.</summary>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_NonFiniteMaximum_ThrowsArgumentOutOfRangeException(float maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartAxis(maximum: maximum));
    }

    /// <summary>Proves a minimum equal to the maximum is rejected.</summary>
    [Fact]
    public void Constructor_MinimumEqualToMaximum_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartAxis(minimum: 5f, maximum: 5f));
    }

    /// <summary>Proves a minimum greater than the maximum is rejected.</summary>
    [Fact]
    public void Constructor_MinimumGreaterThanMaximum_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartAxis(minimum: 10f, maximum: 5f));
    }

    /// <summary>Proves an invalid tick interval (zero, negative, or non-finite) is rejected.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_InvalidTickInterval_ThrowsArgumentOutOfRangeException(float tickInterval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartAxis(tickInterval: tickInterval));
    }

    /// <summary>Proves a whitespace-only title is rejected.</summary>
    [Fact]
    public void Constructor_WhitespaceTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartAxis(title: "   "));
    }

    /// <summary>Proves an empty title is rejected.</summary>
    [Fact]
    public void Constructor_EmptyTitle_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartAxis(title: ""));
    }

    /// <summary>Proves mutating the caller's original labels list after construction does not affect the stored snapshot.</summary>
    [Fact]
    public void Constructor_MutatingOriginalLabelsList_DoesNotAffectStoredSnapshot()
    {
        var original = new List<string> { "A", "B" };
        var axis = new ChartAxis(original);

        original.Add("C");
        original[0] = "Changed";

        Assert.Equal(["A", "B"], axis.Labels);
    }
}
