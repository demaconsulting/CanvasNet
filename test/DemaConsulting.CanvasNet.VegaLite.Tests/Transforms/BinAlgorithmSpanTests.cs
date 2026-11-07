using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests of the span of the bin algorithm (Phase 12): Vega-Lite turns a <c>bin.extent</c> that is a parameter into the <c>span</c> of Vega's <c>bin</c>, which chooses the step
///     (<c>span || (max - min) || abs(min) || 1</c> in vega-statistics) while the bins still cover the extent of the data. Every number is derived from the step rule by hand.
/// </summary>
public class BinAlgorithmSpanTests
{
    /// <summary>
    ///     Validates that the span chooses the step and the extent of the data the bins: for the data from 1 to 8 and at most 20 bins, a span of 3 gives the step 0.2 (the step starts at
    ///     10^(round(log10 3) - 2) = 0.01, grows by 10 to 1 where 3 bins fit, and the divisor 5 gives 0.2 with 15 bins; 2 gives 0.1 with 30, too many), while the span of the data, 7, gives 0.5 (the
    ///     divisor 5 gives 0.2 with 35 bins, too many; 2 gives 0.5 with 14).
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_TheSpanChoosesTheStepAndTheDataTheBins()
    {
        // Arrange
        var spanned = new BinSettings { MaxBins = 20, Span = 3 };
        var plain = new BinSettings { MaxBins = 20 };

        // Act
        var withSpan = BinAlgorithm.Compute(1, 8, spanned, 1000, default!);
        var withoutSpan = BinAlgorithm.Compute(1, 8, plain, 1000, default!);

        // Assert: the bins start and stop at the data in both
        Assert.Equal((1.0, 8.0), (withSpan.Start, withSpan.Stop));
        Assert.Equal(0.2, withSpan.Step, 12);
        Assert.Equal((1.0, 8.0), (withoutSpan.Start, withoutSpan.Stop));
        Assert.Equal(0.5, withoutSpan.Step, 12);
    }

    /// <summary>
    ///     Validates that a span that is zero, or not a number, is as good as none (JavaScript's <c>||</c>): the step of the data's own span.
    /// </summary>
    /// <param name="span">The span.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void BinAlgorithm_Compute_AZeroOrNaNSpanFallsBackToTheDataSpan(double span)
    {
        // Act
        var bins = BinAlgorithm.Compute(1, 8, new BinSettings { MaxBins = 20, Span = span }, 1000, default!);

        // Assert
        Assert.Equal(0.5, bins.Step, 12);
    }

    /// <summary>
    ///     Validates that a span with no extent in the data still chooses the step: a single value of 5 with the span 3 and at most 10 bins has the step 0.5 (10^(0-1) = 0.1 grows to 1, and the divisor 2 gives 0.5
    ///     with 6 bins).
    /// </summary>
    [Fact]
    public void BinAlgorithm_Compute_ASpanAppliesToASingleValue()
    {
        // Act
        var bins = BinAlgorithm.Compute(5, 5, new BinSettings { MaxBins = 10, Span = 3 }, 1000, default!);

        // Assert: floor(5 / 0.5) * 0.5 = 5 and ceil(5 / 0.5) * 0.5 = 5, so the stop is one step above
        Assert.Equal((5.0, 5.5, 0.5), (bins.Start, bins.Stop, bins.Step));
    }

    /// <summary>
    ///     Validates the span of a value as Vega's <c>span</c> function reads it: the last element minus the first of an array; zero for anything else.
    /// </summary>
    [Fact]
    public void BinAlgorithm_SpanOf_IsTheLastMinusTheFirst()
    {
        // Assert
        Assert.Equal(3.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromNumber(2), DataValue.FromNumber(5)])));
        Assert.Equal(-3.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromNumber(5), DataValue.FromNumber(2)])));
        Assert.Equal(5.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromNumber(0), DataValue.FromNumber(3), DataValue.FromNumber(5)])));
        Assert.Equal(86_400_000.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromDate(0), DataValue.FromDate(86_400_000)])));
        Assert.Equal(0.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromNumber(2)])));
        Assert.Equal(0.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([])));
        Assert.Equal(0.0, BinAlgorithm.SpanOf(DataValue.FromNumber(5)));
        Assert.Equal(0.0, BinAlgorithm.SpanOf(default));
        Assert.Equal(0.0, BinAlgorithm.SpanOf(DataValue.FromOwnedArray([DataValue.FromString("x"), DataValue.FromNumber(2)])));
    }
}
