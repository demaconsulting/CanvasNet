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
    ///     Validates that the last element survives floating-point rounding error (the end is reached up to a few
    ///     ULPs) but that a value that is genuinely beyond the end is never included.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The increment.</param>
    /// <param name="end">The limit.</param>
    /// <param name="expected">The expected count.</param>
    [Theory]
    [InlineData(0, 0.1, 0.3, 4)]
    [InlineData(0, 0.1, 0.2999999995, 3)]
    [InlineData(0, 0.1, 0.29999999999, 3)]
    [InlineData(0, 0.1, 0.7, 8)]
    [InlineData(0, 0.1, 0.6999999, 7)]
    [InlineData(0, 0.01, 1, 101)]
    [InlineData(0, 0.01, 0.9999999, 100)]
    [InlineData(-1, 0.1, 1, 21)]
    [InlineData(0.3, -0.1, 0, 4)]
    [InlineData(0.3, -0.1, 0.0000000005, 3)]
    [InlineData(1, -0.1, 0, 11)]
    [InlineData(0, -0.1, -0.3, 4)]
    [InlineData(0, -0.1, -0.2999999995, 3)]
    [InlineData(1e15, 0.5, 1e15 + 2, 5)]
    [InlineData(1e15, 0.5, 1e15 + 1.75, 4)]
    [InlineData(1e15, 1, 1e15 + 10, 11)]
    [InlineData(1e16, 1, 1e16 + 10, 11)]
    [InlineData(0, 1e10, 1e12, 101)]
    [InlineData(0, 1e-10, 3e-10, 4)]
    [InlineData(0, 1e-10, 2.99e-10, 3)]
    public void ScadRange_Count_AbsorbsRoundingErrorButNotRealOvershoot(double start, double step, double end, long expected)
    {
        // Arrange
        var range = new ScadRange(start, step, end);

        // Act
        var count = range.Count;

        // Assert: the count is exact, and the elements start at the start and run in the direction of the step
        Assert.Equal(expected, count);
        Assert.Equal(start, range.ElementAt(0));
        Assert.True(Math.Sign(step) * (range.ElementAt(count - 1) - start) >= 0);
    }

    /// <summary>
    ///     Validates that the last element of a range is never beyond the end by more than rounding error, and that
    ///     the first element past the count would be.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The increment.</param>
    /// <param name="end">The limit.</param>
    [Theory]
    [InlineData(0, 0.1, 0.3)]
    [InlineData(0, 0.1, 0.2999999995)]
    [InlineData(-5, 0.7, 50)]
    [InlineData(5, -0.3, -40)]
    [InlineData(1e9, 0.001, 1e9 + 1)]
    public void ScadRange_ElementAt_LastElementIsWithinEndAndNextIsBeyond(double start, double step, double end)
    {
        // Arrange
        var range = new ScadRange(start, step, end);
        var direction = Math.Sign(step);
        var tolerance = Math.Max(Math.Abs(end), Math.Abs(start)) * 1e-15;

        // Act
        var last = range.ElementAt(range.Count - 1);
        var next = range.ElementAt(range.Count);

        // Assert: the last element does not pass the end by more than rounding error; the next one is clearly beyond
        Assert.True(direction * (last - end) <= tolerance, $"last element {last} passes the end {end}");
        Assert.True(direction * (next - end) > 0, $"element {next} after the last is not beyond the end {end}");
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
