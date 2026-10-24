using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadRange"/>.
/// </summary>
public class ScadRangeTests
{
    /// <summary>
    ///     Validates the element counts of ordinary ranges.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The increment.</param>
    /// <param name="end">The limit.</param>
    /// <param name="expected">The expected count.</param>
    [Theory]
    [InlineData(0, 1, 5, 6)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 2, 5, 3)]
    [InlineData(5, -1, 0, 6)]
    [InlineData(10, -3, 0, 4)]
    [InlineData(0, 0.1, 1, 11)]
    [InlineData(0, 0.25, 1, 5)]
    [InlineData(1, 1, 0, 0)]
    [InlineData(0, -1, 5, 0)]
    [InlineData(0, 0, 5, 0)]
    [InlineData(0, 1, double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 1, 0, 0)]
    [InlineData(0, double.NaN, 1, 0)]
    [InlineData(double.NaN, 1, 1, 0)]
    public void ScadRange_Count_ReturnsExpected(double start, double step, double end, long expected)
    {
        // Arrange
        var range = new ScadRange(start, step, end);

        // Act / Assert
        Assert.Equal(expected, range.Count);
    }

    /// <summary>
    ///     Validates that huge ranges saturate instead of overflowing.
    /// </summary>
    [Fact]
    public void ScadRange_Count_HugeRange_Saturates()
    {
        // Arrange
        var range = new ScadRange(0, 1, 1e300);

        // Act / Assert
        Assert.Equal(long.MaxValue - 1, range.Count);
        Assert.Equal(1_000_000_000_001, new ScadRange(0, 1, 1e12).Count);
    }

    /// <summary>
    ///     Validates that elements are computed from the index, never accumulated.
    /// </summary>
    [Fact]
    public void ScadRange_ElementAt_ComputesFromIndex()
    {
        // Arrange
        var range = new ScadRange(1, 0.1, 2);

        // Act / Assert
        Assert.Equal(1.0, range.ElementAt(0));
        Assert.Equal(1 + (7 * 0.1), range.ElementAt(7));
        Assert.Equal(11, range.Count);
    }

    /// <summary>
    ///     Validates the invalid-range flag.
    /// </summary>
    [Fact]
    public void ScadRange_IsInvalid_ZeroOrNonFiniteParts()
    {
        // Arrange / Act / Assert
        Assert.True(new ScadRange(0, 0, 1).IsInvalid);
        Assert.True(new ScadRange(double.NaN, 1, 1).IsInvalid);
        Assert.True(new ScadRange(0, double.PositiveInfinity, 1).IsInvalid);
        Assert.True(new ScadRange(0, 1, double.NegativeInfinity).IsInvalid);
        Assert.False(new ScadRange(5, 1, 0).IsInvalid);
        Assert.False(new ScadRange(0, 1, 5).IsInvalid);
    }

    /// <summary>
    ///     Validates value equality and hashing.
    /// </summary>
    [Fact]
    public void ScadRange_Equals_ComparesStartStepEnd()
    {
        // Arrange
        var range = new ScadRange(0, 1, 5);

        // Act / Assert
        Assert.True(range.Equals(new ScadRange(0, 1, 5)));
        Assert.True(range.Equals((object)new ScadRange(-0.0, 1, 5)));
        Assert.Equal(range.GetHashCode(), new ScadRange(-0.0, 1, 5).GetHashCode());
        Assert.False(range.Equals(new ScadRange(0, 2, 5)));
        Assert.False(range.Equals(new ScadRange(1, 1, 5)));
        Assert.False(range.Equals(new ScadRange(0, 1, 6)));
        Assert.False(range.Equals((ScadRange?)null));
        Assert.False(range.Equals("range"));
    }
}
