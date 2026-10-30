using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the bin algorithm: the choice of the bin step for an extent (hand-derived from the algorithm), the alignment
///     to an anchor, the assignment of values to bins, the bounds on the number of bins and the reading of the bin parameters.
///     The expected values are worked out by hand from the published description of the algorithm.
/// </summary>
public class BinAlgorithmTests
{
    /// <summary>The location the algorithm reports failures at.</summary>
    private static readonly VegaLiteLocation Where = new(null, "/transform/0", 1, 1);

    /// <summary>The default limit on the number of bins.</summary>
    private const int Limit = 100_000;

    /// <summary>
    ///     Computes the bins of an extent.
    /// </summary>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The bins.</returns>
    private static BinSpec Compute(double min, double max, BinSettings settings) => BinAlgorithm.Compute(min, max, settings, Limit, Where);

    /// <summary>
    ///     Validates the step and the boundaries that the algorithm chooses for a range of extents.
    /// </summary>
    /// <param name="maxBins">The most bins.</param>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    /// <param name="start">The expected start.</param>
    /// <param name="stop">The expected stop.</param>
    /// <param name="step">The expected step.</param>
    [Theory]
    [InlineData(10, 0, 100, 0, 100, 10)]
    [InlineData(20, 0, 100, 0, 100, 5)]
    [InlineData(10, 3.7, 98.2, 0, 100, 10)]
    [InlineData(10, 0, 1, 0, 1, 0.1)]
    [InlineData(10, 5, 5, 5, 5.5, 0.5)]
    [InlineData(10, 0.1, 9.7, 0, 10, 1)]
    [InlineData(10, 0, 7, 0, 7, 1)]
    [InlineData(10, 0, 3, 0, 3, 0.5)]
    [InlineData(10, 0, 1_000_000, 0, 1_000_000, 100_000)]
    [InlineData(10, -50, 50, -50, 50, 10)]
    [InlineData(10, -3.2, 3.2, -4, 4, 1)]
    [InlineData(10, 0, 0, 0, 0.1, 0.1)]
    [InlineData(10, -8, -8, -8, -7, 1)]
    public void BinAlgorithm_Compute_MatchesHandDerivedSteps(double maxBins, double min, double max, double start, double stop, double step)
    {
        // Arrange / Act
        var bins = Compute(min, max, new BinSettings { MaxBins = maxBins });

        // Assert
        Assert.Equal(start, bins.Start, 9);
        Assert.Equal(stop, bins.Stop, 9);
        Assert.Equal(step, bins.Step, 9);
    }

    /// <summary>
    ///     Validates that the default maximum is 20 bins, so a hundred-wide extent gets the step 5.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_DefaultsToTwentyBins()
    {
        // Arrange / Act
        var bins = Compute(0, 100, new BinSettings());

        // Assert
        Assert.Equal(5, bins.Step, 9);
        Assert.Equal(20, bins.BinCount, 9);
    }

    /// <summary>
    ///     Validates that an exact step is used as given and rounds the extent out to whole steps.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_UsesAnExactStep()
    {
        // Arrange / Act
        var whole = Compute(0, 100, new BinSettings { Step = 25 });
        var rounded = Compute(3, 61, new BinSettings { Step = 20 });

        // Assert: [0, 100] by 25 is four bins; [3, 61] by 20 is rounded out to [0, 80]
        Assert.Equal((0.0, 100.0, 25.0), (whole.Start, whole.Stop, whole.Step));
        Assert.Equal((0.0, 80.0, 20.0), (rounded.Start, rounded.Stop, rounded.Step));
    }

    /// <summary>
    ///     Validates that a list of allowed steps picks the largest step below the span divided by the bin count, or the first one
    ///     when every step is larger.
    /// </summary>
    /// <param name="steps">The allowed steps.</param>
    /// <param name="maxBins">The bin count.</param>
    /// <param name="expected">The expected step.</param>
    [Theory]
    [InlineData(new double[] { 1, 2, 5, 10 }, 10, 5)]
    [InlineData(new double[] { 1, 2, 5, 10, 20, 50 }, 5, 10)]
    [InlineData(new double[] { 50, 100 }, 10, 50)]
    [InlineData(new double[] { 3 }, 10, 3)]
    public void BinAlgorithm_Compute_ChoosesTheLargestAllowedStepBelowTheTarget(double[] steps, double maxBins, double expected)
    {
        // Arrange / Act: the extent is [0, 100], so the target step is 100 / maxBins
        var bins = Compute(0, 100, new BinSettings { MaxBins = maxBins, Steps = steps });

        // Assert
        Assert.Equal(expected, bins.Step, 9);
    }

    /// <summary>
    ///     Validates that the minimum step, the divisors and the base change the step as the algorithm describes.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_HonorsMinStepDivideAndBase()
    {
        // Arrange / Act
        var minStep = Compute(0, 100, new BinSettings { MaxBins = 10, MinStep = 25 });
        var divideByTwo = Compute(0, 100, new BinSettings { MaxBins = 20, Divide = [2] });
        var divideByTen = Compute(0, 100, new BinSettings { MaxBins = 20, Divide = [10] });
        var baseTwo = Compute(0, 100, new BinSettings { MaxBins = 10, Base = 2 });

        // Assert: minstep 25 (an extent of 100 is four bins); with maxbins 20 the step 10 is halved to 5 by a divisor of 2, and
        // stays 10 for a divisor of 10 (a step of 1 would need 100 bins); base 2 has the steps 8 (13 bins) then 16 (7 bins), nice to 112
        Assert.Equal((0.0, 100.0, 25.0), (minStep.Start, minStep.Stop, minStep.Step));
        Assert.Equal(5, divideByTwo.Step, 9);
        Assert.Equal(10, divideByTen.Step, 9);
        Assert.Equal((0.0, 112.0, 16.0), (baseTwo.Start, baseTwo.Stop, baseTwo.Step));
    }

    /// <summary>
    ///     Validates that without <c>nice</c> the extent is kept as it is (a single value still gets one step of width).
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_NiceFalseKeepsTheExtent()
    {
        // Arrange / Act
        var kept = Compute(3.7, 98.2, new BinSettings { MaxBins = 10, Nice = false });
        var single = Compute(5, 5, new BinSettings { MaxBins = 10, Nice = false });

        // Assert
        Assert.Equal(3.7, kept.Start, 9);
        Assert.Equal(98.2, kept.Stop, 9);
        Assert.Equal(10, kept.Step, 9);
        Assert.Equal(5, single.Start, 9);
        Assert.Equal(5.5, single.Stop, 9);
    }

    /// <summary>
    ///     Validates that the lower boundary is moved down a step when the rounding tolerance would put it above the minimum:
    ///     2.9995 / 0.1 + 0.01 rounds to 30, so the boundary 3 is above the value and the first bin starts at 2.9.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_MovesTheStartDownWhenToleranceRoundsAboveTheMinimum()
    {
        // Arrange / Act
        var bins = Compute(2.9995, 3.5, new BinSettings { Step = 0.1 });

        // Assert
        Assert.Equal(2.9, bins.Start, 9);
        Assert.Equal(3.5, bins.Stop, 9);
        Assert.Equal(6, (bins.Stop - bins.Start) / bins.Step, 6);
    }

    /// <summary>
    ///     Validates that an extent without a width uses its magnitude, and that zero uses a span of one.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_DegenerateExtentUsesTheMagnitude()
    {
        // Arrange / Act
        var large = Compute(50, 50, new BinSettings { MaxBins = 10 });
        var zero = Compute(0, 0, new BinSettings { MaxBins = 10 });

        // Assert: the span of the single value 50 is 50: the step starts at 10 (five bins), the divisor 5 gives the step 2 which needs
        // 25 bins, the divisor 2 gives 5 which needs 10; a single value is one step wide
        Assert.Equal(5, large.Step, 9);
        Assert.Equal(50, large.Start, 9);
        Assert.Equal(55, large.Stop, 9);
        Assert.Equal(0.1, zero.Step, 9);
    }

    /// <summary>
    ///     Validates that extents that no finite step can bin fail with a located exception that says so.
    /// </summary>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    [Theory]
    [InlineData(0, double.Epsilon)]
    [InlineData(-1e308, 1e308)]
    [InlineData(double.NegativeInfinity, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(5, 1)]
    public void BinAlgorithm_Compute_TinyHugeOrInvalidExtentsFail(double min, double max)
    {
        // Arrange / Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Compute(min, max, new BinSettings { MaxBins = 10 }));

        // Assert
        Assert.Contains("The values cannot be binned", exception.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", exception.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a bin count above the limit fails with the limit named, for a requested count and for a tiny exact step.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_MoreBinsThanTheLimitFails()
    {
        // Arrange / Act
        var requested = Assert.Throws<VegaLiteRenderException>(() => BinAlgorithm.Compute(0, 1, new BinSettings { MaxBins = 101 }, 100, Where));
        var tinyStep = Assert.Throws<VegaLiteRenderException>(() => BinAlgorithm.Compute(0, 1e9, new BinSettings { Step = 1e-9 }, 100, Where));
        var atLimit = BinAlgorithm.Compute(0, 1, new BinSettings { MaxBins = 100 }, 100, Where);

        // Assert
        Assert.Contains("the limit of 100 (VegaLiteLimits.MaxScaleDomainSize)", requested.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 100 (VegaLiteLimits.MaxScaleDomainSize)", tinyStep.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", tinyStep.Location.Pointer);
        Assert.Equal(0.01, atLimit.Step, 9);
    }

    /// <summary>
    ///     Validates that an anchor shifts the boundaries so that one lines up with it, like the transform of Vega.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Align_AnchorShiftsTheBins()
    {
        // Arrange
        var bins = Compute(0, 100, new BinSettings { MaxBins = 10 });

        // Act
        var plain = BinAlgorithm.Align(bins, null);
        var anchoredAtFive = BinAlgorithm.Align(bins, 5);
        var anchoredAtTwentyThree = BinAlgorithm.Align(bins, 23);
        var anchoredOnABoundary = BinAlgorithm.Align(bins, 40);
        var anchoredBelow = BinAlgorithm.Align(bins, -7);

        // Assert: the shift is anchor - (start + step * floor((anchor - start) / step)), added to start and stop
        Assert.Equal((0.0, 100.0, 10.0), (plain.Start, plain.Stop, plain.Step));
        Assert.Equal((5.0, 105.0, 10.0), (anchoredAtFive.Start, anchoredAtFive.Stop, anchoredAtFive.Step));
        Assert.Equal((3.0, 103.0, 10.0), (anchoredAtTwentyThree.Start, anchoredAtTwentyThree.Stop, anchoredAtTwentyThree.Step));
        Assert.Equal((0.0, 100.0, 10.0), (anchoredOnABoundary.Start, anchoredOnABoundary.Stop, anchoredOnABoundary.Step));
        Assert.Equal(3, anchoredBelow.Start, 9);
        Assert.Equal(103, anchoredBelow.Stop, 9);
    }

    /// <summary>
    ///     Validates that the stop is rounded up to a whole step before an anchor is applied.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Align_RoundsTheStopUpToAWholeStep()
    {
        // Arrange: a bin spec whose stop is not a whole step from the start (as with nice off)
        var spec = new BinSpec(0, 95, 10);

        // Act
        var aligned = BinAlgorithm.Align(spec, null);

        // Assert
        Assert.Equal(100, aligned.Stop, 9);
    }

    /// <summary>
    ///     Validates that resolving applies the anchor of the settings and fails when it moves the bins out of range.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Resolve_AppliesTheAnchor()
    {
        // Arrange / Act
        var bins = BinAlgorithm.Resolve(0, 100, new BinSettings { MaxBins = 10, Anchor = 5 }, Limit, Where);

        // Assert
        Assert.Equal((5.0, 105.0, 10.0), (bins.Start, bins.Stop, bins.Step));
    }

    /// <summary>
    ///     Validates the lower boundary, the upper boundary and the infinite bins of values around the bins 0 to 100 by 10.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="start">The expected lower boundary.</param>
    /// <param name="end">The expected upper boundary.</param>
    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(9.999, 0, 10)]
    [InlineData(10, 10, 20)]
    [InlineData(55.5, 50, 60)]
    [InlineData(99.9, 90, 100)]
    [InlineData(100, 90, 100)]
    [InlineData(-1, double.NegativeInfinity, double.NegativeInfinity)]
    [InlineData(101, double.PositiveInfinity, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity)]
    public void BinAlgorithm_Assign_PlacesValuesInBins(double value, double start, double end)
    {
        // Arrange
        var bins = new BinSpec(0, 100, 10);

        // Act
        var lower = BinAlgorithm.Assign(value, bins);
        var upper = BinAlgorithm.End(lower, bins);

        // Assert
        Assert.Equal(start, lower, 9);
        Assert.Equal(end, upper, 9);
    }

    /// <summary>
    ///     Validates that the rounding tolerance keeps 0.3 in the bin that starts at 0.3 even though 0.3 / 0.1 is 2.9999999999999996.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Assign_ToleranceKeepsABoundaryValueInTheBinAbove()
    {
        // Arrange
        var bins = new BinSpec(0, 1, 0.1);

        // Act
        var start = BinAlgorithm.Assign(0.3, bins);
        var end = BinAlgorithm.End(start, bins);

        // Assert
        Assert.Equal(0.3, start, 9);
        Assert.Equal(0.4, end, 9);
    }

    /// <summary>
    ///     Validates that the bins of an anchored algorithm leave values below the shifted start in the bin at negative infinity.
    /// </summary>
    [Fact]
    public void BinAlgorithm_Assign_AnchoredBinsLeaveSmallValuesBelowTheStart()
    {
        // Arrange
        var bins = BinAlgorithm.Resolve(0, 100, new BinSettings { MaxBins = 10, Anchor = 5 }, Limit, Where);

        // Act / Assert: the bins now run from 5 to 105
        Assert.Equal(double.NegativeInfinity, BinAlgorithm.Assign(4.9, bins));
        Assert.Equal(5, BinAlgorithm.Assign(5, bins), 9);
        Assert.Equal(95, BinAlgorithm.Assign(104.9, bins), 9);
    }

    /// <summary>
    ///     Validates how the bin parameters are read from a specification: every parameter, the defaults, and the way a missing
    ///     <c>maxbins</c> depends on a <c>step</c>.
    /// </summary>
    [Fact]
    public void BinAlgorithm_BinSettingsRead_ReadsEveryParameter()
    {
        // Arrange
        var all = ReadBin("{\"maxbins\":7,\"base\":2,\"divide\":[3],\"step\":4,\"steps\":[1,2],\"minstep\":0.5,\"nice\":false,\"anchor\":1,\"extent\":[2,9]}", 10);
        var empty = ReadBin("{}", 10);
        var withStep = ReadBin("{\"step\":5}", 10);
        var plain = ReadBin("true", 6);

        // Assert
        Assert.Equal(7, all.MaxBins);
        Assert.Equal(2, all.Base);
        Assert.Equal(new double[] { 3 }, all.Divide);
        Assert.Equal(4.0, all.Step);
        Assert.Equal(new double[] { 1, 2 }, all.Steps);
        Assert.Equal(0.5, all.MinStep);
        Assert.False(all.Nice);
        Assert.Equal(1.0, all.Anchor);
        Assert.Equal((2.0, 9.0), all.Extent);
        Assert.Equal(10, empty.MaxBins);
        Assert.Equal(10, empty.Base);
        Assert.Null(empty.Step);
        Assert.True(empty.Nice);
        Assert.Null(empty.Extent);
        Assert.Equal(20, withStep.MaxBins);
        Assert.Equal(5.0, withStep.Step);
        Assert.Equal(6, plain.MaxBins);
        Assert.Equal(6, BinSettings.Read(null, 6).MaxBins);
    }

    /// <summary>
    ///     Reads the bin parameters of a <c>bin</c> value by parsing a specification around it.
    /// </summary>
    /// <param name="binJson">The JSON of the <c>bin</c> value.</param>
    /// <param name="defaultMaxBins">The default number of bins.</param>
    /// <returns>The settings.</returns>
    private static BinSettings ReadBin(string binJson, double defaultMaxBins)
    {
        var tree = SpecParser.Parse("{\"data\":{\"values\":[]},\"transform\":[{\"bin\":" + binJson + ",\"field\":\"x\",\"as\":\"b\"}],\"mark\":\"point\"}", "t.vl.json", null);
        return BinSettings.Read(tree.Root["transform"]!.Items[0]["bin"], defaultMaxBins);
    }
}
