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
    ///     Validates that a span that overflows a double, or an infinite end, has no ticks and never yields a
    ///     non-finite tick value.
    /// </summary>
    [Fact]
    public void Ticks_Ticks_OverflowingSpanHasNoNonFiniteTicks()
    {
        // Arrange: the span of -1e308 to 1e308 is infinite as a double
        var spans = new (double Start, double Stop)[]
        {
            (-1e308, 1e308),
            (1e308, -1e308),
            (double.NegativeInfinity, 10),
            (0, double.PositiveInfinity),
            (double.PositiveInfinity, double.PositiveInfinity),
        };

        // Act / Assert
        foreach (var (start, stop) in spans)
        {
            Assert.Empty(Ticks.GetTicks(start, stop, 10));
            Assert.Empty(Ticks.AxisTicks(start, stop, 10));
        }

        // Assert: the largest finite spans still have finite ticks
        var wide = Ticks.GetTicks(-1e307, 1e307, 4);
        Assert.NotEmpty(wide);
        Assert.All(wide, tick => Assert.True(double.IsFinite(tick)));
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

    /// <summary>
    ///     Validates the minimum step on a log scale: the count is still limited to the steps that fit, but is not lowered by the d3 tick step
    ///     (the steps of log ticks are not even), as in Vega's <c>tickCount</c>.
    /// </summary>
    [Fact]
    public void Ticks_ResolveTickCount_LogarithmicScaleDoesNotLowerTheCountByTheStep()
    {
        // 1..100 with minimum step 25: at most 3 steps fit (99 / 25 = 3.96), so 4 ticks; the d3 step of 4 ticks is 20 (below 25), so a linear
        // scale goes down to 3 ticks (step 50), a log scale keeps the 4
        Assert.Equal(3, Ticks.ResolveTickCount(1, 100, 10, 25));
        Assert.Equal(4, Ticks.ResolveTickCount(1, 100, 10, 25, logarithmic: true));

        // Without a minimum step nothing changes for either
        Assert.Equal(10, Ticks.ResolveTickCount(1, 100, 10, null, logarithmic: true));
    }

    /// <summary>
    ///     Validates the ticks of a log scale against d3's <c>scaleLog.ticks</c>: the multiples of every power of the base inside the domain when
    ///     the domain spans fewer powers than the count (with exact decimals below one), the d3 ticks of the exponents when it spans more,
    ///     the ordinary ticks when the multiples are fewer than half the count, reflected ticks for a negative domain, descending ticks for
    ///     a descending domain, other bases, and no ticks for a domain that contains or crosses zero.
    /// </summary>
    [Fact]
    public void Ticks_LogTicks_MatchHandDerivedD3Vectors()
    {
        // Three decades with 10 ticks wanted: 1..9, 10..90 step 10, 100..900 step 100, then 1000 (2000 is past the end): 9 + 9 + 9 + 1
        var decades = LogTicks(1, 1000, 10, 10);
        Assert.Equal(28, decades.Length);
        Assert.Equal([1.0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 20], decades[..11]);
        Assert.Equal([800.0, 900, 1000], decades[^3..]);

        // Below one the multiples are divided by the power (exact decimals): 0.01..0.09, 0.1..0.9, 1
        var fractions = LogTicks(0.01, 1, 10, 10);
        Assert.Equal(19, fractions.Length);
        Assert.Equal([0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.1], fractions[..10]);
        Assert.Equal([0.9, 1.0], fractions[^2..]);

        // The domain starts between powers: 2..9, 10, 20, 30 (100 is past the end)
        Assert.Equal([2.0, 3, 4, 5, 6, 7, 8, 9, 10, 20, 30], LogTicks(2, 30, 10, 10));

        // Twelve decades with 5 ticks wanted: the span is not below the count, so the exponents get the d3 ticks of 0..12 (step 2) as powers
        Assert.Equal([1.0, 1e2, 1e4, 1e6, 1e8, 1e10, 1e12], LogTicks(1, 1e12, 5, 10));

        // Fewer multiples than half the count fall back to the ordinary ticks of the domain: 2..8 gives 7 multiples, below 10; the step of
        // 20 ticks over 6 is 0.3, which rounds to 0.2, so 2, 2.2, ..., 8
        var fallback = LogTicks(2, 8, 20, 10);
        Assert.Equal(31, fallback.Length);
        Assert.Equal([2.0, 2.2, 2.4], fallback[..3]);
        Assert.Equal(8.0, fallback[^1]);

        // A descending domain gives the same ticks backwards
        var backwards = LogTicks(1000, 1, 10, 10);
        Assert.Equal([.. decades.AsEnumerable().Reverse()], backwards);

        // A domain below zero is reflected: the ticks run from -1000 up to -1, 28 of them, in ascending order
        var negative = LogTicks(-1000, -1, 10, 10);
        Assert.Equal(28, negative.Length);
        Assert.Equal([-1000.0, -900, -800], negative[..3]);
        Assert.Equal([-2.0, -1.0], negative[^2..]);
        Assert.True(negative.Zip(negative.Skip(1), (a, b) => a < b).All(ascending => ascending));

        // The base 2 has one multiple per power: 1, 2, 4, 8, 16
        Assert.Equal([1.0, 2, 4, 8, 16], LogTicks(1, 16, 10, 2));

        // A base that is not a whole number takes the d3 ticks of the exponents (0..7 for 7.54 powers) as powers of the base
        var fractional = LogTicks(1, 1000, 10, 2.5);
        Assert.Equal(8, fractional.Length);
        Assert.Equal(1.0, fractional[0]);
        Assert.Equal(Math.Pow(2.5, 7), fractional[7], 9);

        // Domains the logarithm is not defined on have no ticks, and neither has a base that is not usable
        Assert.Empty(LogTicks(0, 10, 10, 10));
        Assert.Empty(LogTicks(-5, 5, 10, 10));
        Assert.Empty(LogTicks(1, double.PositiveInfinity, 10, 10));
        Assert.Empty(LogTicks(1, 1000, 10, 1));
        Assert.Empty(LogTicks(1, 1000, 10, 0));
        Assert.Empty(LogTicks(1, 1000, 10, -2));
        Assert.True(Ticks.IsLogDomain(0.5, 80));
        Assert.True(Ticks.IsLogDomain(-80, -0.5));
        Assert.False(Ticks.IsLogDomain(0, 80));
        Assert.False(Ticks.IsLogDomain(-1, 1));
    }

    /// <summary>
    ///     Gets the ticks of a log scale with the cancellation token of the running test.
    /// </summary>
    /// <param name="start">The first end of the domain.</param>
    /// <param name="stop">The second end of the domain.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <param name="logBase">The base of the logarithm.</param>
    /// <param name="maxTicks">The largest number of ticks accepted.</param>
    /// <returns>The tick values.</returns>
    private static double[] LogTicks(double start, double stop, double count, double logBase, int maxTicks = 1_000) =>
        Ticks.LogTicks(start, stop, count, logBase, maxTicks, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Validates that the ticks of a log scale respect the tick limit and the work limit that guards the multiples outside the domain,
    ///     poll the cancellation token, and give the located limit message the tool maps to a budget error.
    /// </summary>
    [Fact]
    public void Ticks_LogTicks_LimitsAndCancellationAreEnforced()
    {
        // The multiples inside the domain are more than the limit: 28 ticks against a limit of 5
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => LogTicks(1, 1000, 10, 10, maxTicks: 5));
        Assert.Contains("the limit of 5", tooMany.Message, StringComparison.Ordinal);

        // A huge base walks over hundreds of millions of multiples below the domain: the work is limited to a hundred times the tick limit
        var tooMuchWork = Assert.Throws<VegaLiteRenderException>(() => LogTicks(5e8, 1e9, 10, 1e9, maxTicks: 10));
        Assert.Contains("the limit of 10", tooMuchWork.Message, StringComparison.Ordinal);

        // The same walk polls the token, so a canceled one stops it long before the budget ends
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => Ticks.LogTicks(5e8, 1e9, 10, 1e9, 1_000, source.Token));

        // The ordinary ticks of the exponents keep their own limit: 1e-300..1e300 with a count of 100000 asks for more than 1000 ticks
        Assert.Throws<VegaLiteRenderException>(() => LogTicks(1e-300, 1e300, 100_000, 10, maxTicks: 1_000));
    }

    /// <summary>
    ///     Validates the <c>nice</c> extension of a log domain against d3's <c>scaleLog.nice</c>: the first end is floored and the last ceiled to a
    ///     power of the base (reflected below zero), a descending domain keeps its direction, and a domain the logarithm is not defined on
    ///     stays as it is.
    /// </summary>
    [Fact]
    public void Ticks_LogNice_ExtendsToPowersOfTheBase()
    {
        // Arrange: (start, stop, base, nice start, nice stop)
        var vectors = new (double Start, double Stop, double Base, double NiceStart, double NiceStop)[]
        {
            // 3..800: floor of log10 3 is 0 (1), ceil of log10 800 is 3 (1000)
            (3, 800, 10, 1, 1000),

            // 0.02..5: floor of -1.69 is -2 (0.01), ceil of 0.7 is 1 (10)
            (0.02, 5, 10, 0.01, 10),

            // already on powers: unchanged
            (1, 1000, 10, 1, 1000),

            // a descending domain keeps its direction
            (800, 3, 10, 1000, 1),

            // a domain below zero is reflected: -800..-3 becomes -1000..-1
            (-800, -3, 10, -1000, -1),

            // base 2: floor of log2 3 is 1 (2), ceil of log2 100 is 7 (128)
            (3, 100, 2, 2, 128),

            // a domain of one number is widened to its decade
            (5, 5, 10, 1, 10),

            // domains the logarithm is not defined on stay as they are
            (0, 10, 10, 0, 10),
            (-1, 1, 10, -1, 1),
            (1, 1000, 1, 1, 1000),
        };

        // Act / Assert
        foreach (var (start, stop, logBase, niceStart, niceStop) in vectors)
        {
            var (resultStart, resultStop) = Ticks.LogNice(start, stop, logBase);
            Assert.True(
                (niceStart, niceStop).Equals((resultStart, resultStop)),
                $"logNice({start}, {stop}, {logBase}) = ({resultStart}, {resultStop}), expected ({niceStart}, {niceStop})");
        }
    }

    /// <summary>
    ///     Validates the label filter of a log axis against d3's <c>scaleLog.tickFormat</c>: the leading digit of a tick must not exceed
    ///     <c>max(1, base * count / ticks)</c>, so many ticks label only the powers and the first multiples, few ticks label all of them, and
    ///     the digit of a fraction is read after the exponent (0.5 is a 5).
    /// </summary>
    [Fact]
    public void Ticks_LogLabelTest_LabelsTheLeadingDigitsThatFit()
    {
        // 28 ticks for a count of 10: k = 10 * 10 / 28 = 3.57, so the digits 1, 2 and 3 are labeled and 4 to 9 are not
        var crowded = Ticks.LogLabelTest(10, 10, 28);
        Assert.All([1.0, 2, 3, 10, 20, 30, 100, 300, 1000, 0.1, 0.2, 0.3], tick => Assert.True(crowded(tick), $"{tick} is labeled"));
        Assert.All([4.0, 5, 9, 40, 90, 400, 0.4, 0.5, 0.7, 0.9], tick => Assert.False(crowded(tick), $"{tick} is not labeled"));

        // A count of 3 with 28 ticks gives k = 1.07: only the powers
        var powers = Ticks.LogLabelTest(10, 3, 28);
        Assert.True(powers(1));
        Assert.True(powers(1000));
        Assert.True(powers(0.01));
        Assert.False(powers(2));

        // Few ticks label every digit (k = 20 here), also the 9
        var sparse = Ticks.LogLabelTest(10, 10, 5);
        Assert.True(sparse(9));
        Assert.True(sparse(900));

        // The magnitude decides, so the ticks of a negative domain are labeled by the same rule; zero has no leading digit
        Assert.True(crowded(-300));
        Assert.False(crowded(-400));
        Assert.False(crowded(0));
    }

    /// <summary>
    ///     Validates the tick dispatch by scale: log scales use the log ticks, pow, sqrt and symlog scales use the d3 ticks of their domain,
    ///     as the scales of d3 do.
    /// </summary>
    [Fact]
    public void Ticks_ModelTicks_DispatchesByTheTransform()
    {
        var log = ScaleModel.CreateLinear([1, 100], [0, 100], transform: ScaleTransform.Log(10));
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(LogTicks(1, 100, 10, 10), Ticks.ModelTicks(log, 10, 1_000, token));
        var square = ScaleModel.CreateLinear([0, 100], [0, 100], transform: ScaleTransform.Pow(2));
        Assert.Equal(Ticks.GetTicks(0, 100, 5), Ticks.ModelTicks(square, 5, 1_000, token));
        var symlog = ScaleModel.CreateLinear([-100, 100], [0, 100], transform: ScaleTransform.Symlog(1));
        Assert.Equal([-100.0, -50, 0, 50, 100], Ticks.ModelTicks(symlog, 4, 1_000, token));
        Assert.Equal(Ticks.GetTicks(0, 10, 5), Ticks.ModelTicks(ScaleModel.CreateLinear([0, 10], [0, 1]), 5, 1_000, token));
        Assert.Throws<ArgumentNullException>(() => Ticks.ModelTicks(null!, 5, 1_000, token));
    }
}
