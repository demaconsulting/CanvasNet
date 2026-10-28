using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the tick generation, the tick step, the nice extension of domains and the Vega tick-count rules. Every expected
///     number is derived by hand from the algorithm (a step of 1, 2 or 5 times a power of ten, chosen by the thresholds
///     sqrt(2), sqrt(10) and sqrt(50)).
/// </summary>
public class TicksTests
{
    /// <summary>
    ///     Validates the tick values of linear ranges against hand-derived vectors: whole steps, fractional steps (which
    ///     must be exact decimals), reversed ranges, negative ranges, degenerate ranges and invalid counts.
    /// </summary>
    [Fact]
    public void Ticks_Ticks_LinearRangeMatchesHandDerivedExpectedVector()
    {
        // Arrange: (start, stop, count, expected ticks); each row is derived below
        var vectors = new (double Start, double Stop, double Count, double[] Expected)[]
        {
            // step 2 for a range of 10 with 5 ticks: 10 / 5 = 2, which is 2 x 10^0
            (0, 10, 5, [0, 2, 4, 6, 8, 10]),

            // 1 / 10 = 0.1: a fractional step comes out as exact decimals
            (0, 1, 10, [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1]),

            // 100 / 5 = 20 = 2 x 10^1
            (0, 100, 5, [0, 20, 40, 60, 80, 100]),

            // 100 / 10 = 10 = 1 x 10^1
            (0, 100, 10, [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100]),

            // 1 / 5 = 0.2 = 2 x 10^-1
            (0, 1, 5, [0, 0.2, 0.4, 0.6, 0.8, 1]),

            // 9 / 10 = 0.9: error 9 >= sqrt(50), so the step is 10 x 10^-1 = 1
            (1, 10, 10, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),

            // step 1 again; the first multiple at or above 0.5 is 1 and the last at or below 9.7 is 9
            (0.5, 9.7, 10, [1, 2, 3, 4, 5, 6, 7, 8, 9]),

            // a reversed range gives the same ticks in descending order
            (10, 0, 5, [10, 8, 6, 4, 2, 0]),

            // negative range: step 2
            (-10, 0, 5, [-10, -8, -6, -4, -2, 0]),

            // a range around zero with step 0.5: error 5 is in [sqrt(10), sqrt(50)), so the factor is 5 and the step 5 x 10^-1
            (-1, 1, 4, [-1, -0.5, 0, 0.5, 1]),

            // 1e6 / 4 = 250000: error 2.5 is in [sqrt(2), sqrt(10)), so the step is 2 x 10^5
            (0, 1e6, 4, [0, 200000, 400000, 600000, 800000, 1e6]),

            // tiny numbers: 1e-6 / 5 = 2e-7, step 2 x 10^-7, exact decimals again
            (0, 1e-6, 5, [0, 2e-7, 4e-7, 6e-7, 8e-7, 1e-6]),

            // 1 / 1 = 1: one tick per unit
            (0, 1, 1, [0, 1]),

            // a degenerate range has its single value, even with a fractional count
            (3, 3, 0.2, [3]),
            (0, 0, 5, [0]),

            // a count below 2 that would leave no tick doubles until one fits: 0.1..0.9 with count 0.5 -> 1 -> 2 gives step 0.5
            (0.1, 0.9, 0.5, [0.5]),

            // 1000 / 7 = 142.86: error 1.43 >= sqrt(2), so the step is 2 x 10^2
            (0, 1000, 7, [0, 200, 400, 600, 800, 1000]),

            // a count that is not positive or not a number has no ticks
            (0, 10, 0, []),
            (0, 10, -1, []),
            (0, 10, double.NaN, []),
            (double.NaN, 10, 5, []),
        };

        // Act / Assert
        foreach (var (start, stop, count, expected) in vectors)
        {
            var ticks = Ticks.GetTicks(start, stop, count);
            Assert.True(
                expected.SequenceEqual(ticks),
                $"ticks({start}, {stop}, {count}) = [{string.Join(", ", ticks)}], expected [{string.Join(", ", expected)}]");
        }
    }

    /// <summary>
    ///     Validates that a result larger than the tick limit fails closed instead of allocating it.
    /// </summary>
    [Fact]
    public void Ticks_Ticks_ResultOverTheLimitFailsClosed()
    {
        // Arrange / Act / Assert: 0 to 100 with 50 ticks is 51 ticks (step 2), over a limit of 5
        Assert.Throws<VegaLiteRenderException>(() => Ticks.GetTicks(0, 100, 50, 5));
        Assert.Equal(51, Ticks.GetTicks(0, 100, 50, 51).Length);
        Assert.Throws<VegaLiteRenderException>(() => Ticks.GetTicks(0, 1e12, 1e9));
    }

    /// <summary>
    ///     Validates the tick step and the encoded tick increment against hand-derived values.
    /// </summary>
    [Fact]
    public void Ticks_TickStep_UsesHandDerivedExpectedStep()
    {
        // Arrange: (start, stop, count, step)
        var vectors = new (double Start, double Stop, double Count, double Step)[]
        {
            (0, 100, 5, 20),
            (0, 10, 5, 2),
            (0, 1, 10, 0.1),
            (0, 1, 5, 0.2),

            // 1 / 3 = 0.333: error 3.33 >= sqrt(10), so the step is 5 x 10^-1 = 0.5
            (0, 1, 3, 0.5),
            (0, 1000, 7, 200),
            (0, 1000, 10, 100),

            // 2.5e-4: error 2.5, factor 2, exponent -4: step 2e-4
            (0, 1e-3, 4, 0.0002),

            // 0.7: error 7 is below sqrt(50), so the factor is 5 and the step 0.5
            (0, 7, 10, 0.5),

            // a reversed range has a negative step
            (10, 0, 5, -2),
            (1, 0, 10, -0.1),
        };

        // Act / Assert
        foreach (var (start, stop, count, step) in vectors)
        {
            Assert.Equal(step, Ticks.TickStep(start, stop, count));
        }

        // Assert: the increment is the step itself from 1 up, and the negated reciprocal below 1
        Assert.Equal(2, Ticks.TickIncrement(0, 10, 5));
        Assert.Equal(20, Ticks.TickIncrement(0, 100, 5));
        Assert.Equal(-10, Ticks.TickIncrement(0, 1, 10));
        Assert.Equal(-5, Ticks.TickIncrement(0, 1, 5));
        Assert.Equal(-1, Ticks.TickIncrement(1, 10, 10));
        Assert.True(double.IsNaN(Ticks.TickIncrement(double.NaN, 1, 5)));
    }

    /// <summary>
    ///     Validates that nice extends a domain to round boundaries, settles when a wider domain needs another step,
    ///     keeps the direction of a reversed domain and leaves degenerate domains alone.
    /// </summary>
    [Fact]
    public void Ticks_Nice_ExpandsDomainToExpectedBoundaries()
    {
        // Arrange: (start, stop, count, nice start, nice stop)
        var vectors = new (double Start, double Stop, double Count, double NiceStart, double NiceStop)[]
        {
            // 0.5..9.7 has step 1, so the domain becomes 0..10
            (0.5, 9.7, 10, 0, 10),

            // already round: unchanged
            (0, 100, 10, 0, 100),

            // 1..99 has step 10 (error 9.8), so the domain becomes 0..100
            (1, 99, 10, 0, 100),

            // 0.12..0.87 with 5 ticks: step 0.2, so 0..1
            (0.12, 0.87, 5, 0, 1),

            // a negative start: -3.2..4.1 has step 1 (error 7.3), so -4..5
            (-3.2, 4.1, 10, -4, 5),

            // 0..0.97 has step 0.1 (error 9.7 -> factor 10 at exponent -2), so 0..1
            (0, 0.97, 10, 0, 1),

            // a reversed domain keeps its direction
            (9.7, 0.5, 10, 10, 0),

            // 0..97 with 5 ticks: step 20, so 0..100
            (0, 97, 5, 0, 100),

            // 10..35 has step 2 (error 2.5), so 10..36
            (10, 35, 10, 10, 36),

            // 0..15 has step 2 (error 1.5), so 0..16
            (0, 15, 10, 0, 16),

            // 0..12 has step 1 (error 1.2), so the domain is already nice
            (0, 12, 10, 0, 12),

            // degenerate or invalid domains stay as they are
            (5, 5, 10, 5, 5),
            (0, 0, 10, 0, 0),
        };

        // Act / Assert
        foreach (var (start, stop, count, niceStart, niceStop) in vectors)
        {
            var (resultStart, resultStop) = Ticks.Nice(start, stop, count);
            Assert.True(
                (niceStart, niceStop).Equals((resultStart, resultStop)),
                $"nice({start}, {stop}, {count}) = ({resultStart}, {resultStop}), expected ({niceStart}, {niceStop})");
        }

        // Assert: a count that is not a number leaves the domain alone, and the default count is 10
        Assert.Equal((0.5, 9.7), Ticks.Nice(0.5, 9.7, double.NaN));
        Assert.Equal((0.0, 10.0), Ticks.Nice(0.5, 9.7));
    }

    /// <summary>
    ///     Validates the Vega-Lite default tick count and minimum step.
    /// </summary>
    [Fact]
    public void Ticks_DefaultAxisTickCountAndMinStep_FollowVegaLite()
    {
        // Arrange / Act / Assert: one tick per 40 pixels, rounded up
        Assert.Equal(8, Ticks.DefaultAxisTickCount(300));
        Assert.Equal(5, Ticks.DefaultAxisTickCount(200));
        Assert.Equal(1, Ticks.DefaultAxisTickCount(20));
        Assert.Equal(2, Ticks.DefaultAxisTickCount(41));
        Assert.Equal(0, Ticks.DefaultAxisTickCount(0));

        // Assert: only the integer format has a minimum step
        Assert.Equal(1, Ticks.DefaultAxisTickMinStep("d"));
        Assert.Null(Ticks.DefaultAxisTickMinStep(".2f"));
        Assert.Null(Ticks.DefaultAxisTickMinStep(null));
    }

    /// <summary>
    ///     Validates how the tick count is reduced so that the step is not below the minimum step.
    /// </summary>
    [Fact]
    public void Ticks_ResolveTickCount_ReducesCountToRespectTheMinimumStep()
    {
        // Arrange / Act / Assert: without a minimum step the count is kept
        Assert.Equal(8, Ticks.ResolveTickCount(0, 10, 8, null));

        // 0..10 with 8 ticks has step 1 (error 1.25), which meets the minimum
        Assert.Equal(8, Ticks.ResolveTickCount(0, 10, 8, 1));

        // 0..3 fits only 3 + 1 = 4 steps of 1: the count falls to 4
        Assert.Equal(4, Ticks.ResolveTickCount(0, 3, 8, 1));

        // 0..1: two ticks would give step 0.5 (below 1), so the count falls to one
        Assert.Equal(1, Ticks.ResolveTickCount(0, 1, 5, 1));

        // a reversed domain gives the same answer, and an empty span counts as one step (so at most two ticks)
        Assert.Equal(4, Ticks.ResolveTickCount(3, 0, 8, 1));
        Assert.Equal(2, Ticks.ResolveTickCount(5, 5, 8, 1));

        // the ticks of an integer axis never have a fractional step
        Assert.Equal(new double[] { 0, 1, 2, 3, 4, 5 }, Ticks.AxisTicks(0, 5, 8, 1));
        Assert.Equal(new double[] { 0, 1 }, Ticks.AxisTicks(0, 1, 5, 1));
        Assert.Equal(new double[] { 0, 20, 40, 60, 80, 100 }, Ticks.AxisTicks(0, 100, Ticks.DefaultAxisTickCount(200)));
    }
}
