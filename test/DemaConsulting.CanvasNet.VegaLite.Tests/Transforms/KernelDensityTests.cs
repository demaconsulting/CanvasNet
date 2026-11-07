using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the Gaussian kernel density estimate: the density of one and two points is a closed form (Gaussians), the cumulative distribution of a symmetric pair is one half
///     at the middle, the West (2009) approximation of the normal distribution is checked against known values, Scott's rule against its formula, and the JavaScript treatment of null
///     and non-numeric support (every row counts) against the reference numbers of vega-statistics.
/// </summary>
public class KernelDensityTests
{
    /// <summary>The density of the standard normal distribution at 1: <c>exp(-1/2) / sqrt(2π)</c>.</summary>
    private static readonly double PhiOfOne = Math.Exp(-0.5) / Math.Sqrt(2 * Math.PI);

    /// <summary>
    ///     Makes the values of a sequence of numbers.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The values.</returns>
    private static DataValue[] Values(params double[] values) => [.. values.Select(DataValue.FromNumber)];

    /// <summary>
    ///     Validates the density of two points with the bandwidth 1: it is the mean of two Gaussians, so at the middle of 0 and 2 it is the Gaussian density at distance one, and at a
    ///     support point it is the mean of the density at distance zero and two.
    /// </summary>
    [Fact]
    public void KernelDensity_Pdf_OfTwoPointsIsTheMeanOfTwoGaussians()
    {
        // Arrange
        var kde = new KernelDensity(Values(0, 2), 1);

        // Act
        var middle = kde.Pdf(1);
        var atSupport = kde.Pdf(0);

        // Assert
        Assert.Equal(PhiOfOne, middle, 14);
        Assert.Equal(((1 / Math.Sqrt(2 * Math.PI)) + (Math.Exp(-2) / Math.Sqrt(2 * Math.PI))) / 2, atSupport, 14);
        Assert.Equal(1, kde.Bandwidth);
        Assert.Equal(2, kde.Count);
    }

    /// <summary>
    ///     Validates that the density scales with the bandwidth: with the support {0} and the bandwidth 2 it is <c>φ(x / 2) / 2</c>.
    /// </summary>
    [Fact]
    public void KernelDensity_Pdf_ScalesWithTheBandwidth()
    {
        // Arrange
        var kde = new KernelDensity(Values(0), 2);

        // Act / Assert
        Assert.Equal(PhiOfOne / 2, kde.Pdf(2), 14);
        Assert.Equal(1 / Math.Sqrt(2 * Math.PI) / 2, kde.Pdf(0), 14);
    }

    /// <summary>
    ///     Validates that the cumulative distribution of the symmetric pair {0, 2} at the middle is one half, is monotone, and tends to 0 and 1 far away.
    /// </summary>
    [Fact]
    public void KernelDensity_Cdf_OfASymmetricPairIsOneHalfAtTheMiddle()
    {
        // Arrange
        var kde = new KernelDensity(Values(0, 2), 1);

        // Act
        var middle = kde.Cdf(1);
        var samples = new[] { -10.0, -2, 0, 1, 2, 4, 12 }.Select(kde.Cdf).ToArray();

        // Assert
        Assert.Equal(0.5, middle, 14);
        Assert.Equal(samples.Order(), samples);
        Assert.Equal(0, samples[0], 12);
        Assert.Equal(1, samples[^1], 12);
    }

    /// <summary>
    ///     Validates the normal cumulative distribution (West 2009) at 0 (exactly one half), at known quantiles (Φ(1), Φ(1.96) and Φ(-3)), across the branch at 7.07 deviations, and at the
    ///     cut-offs beyond 37 deviations; the reference values are those of vega-statistics.
    /// </summary>
    [Fact]
    public void KernelDensity_NormalCdf_MatchesKnownValuesAndTheReference()
    {
        // Arrange / Act / Assert: exact at the center, then the well-known quantiles of the normal distribution
        Assert.Equal(0.5, KernelDensity.NormalCdf(0));
        Assert.Equal(0.8413447460685429, KernelDensity.NormalCdf(1), 12);
        Assert.Equal(0.9750021048517795, KernelDensity.NormalCdf(1.96), 12);
        Assert.Equal(0.001349898031630095, KernelDensity.NormalCdf(-3), 12);

        // Assert: the reference values on both sides of the branch at 7.07106781186547 and in the tails
        Assert.Equal(0.9999999999992253, KernelDensity.NormalCdf(7.07), 13);
        Assert.Equal(0.9999999999993762, KernelDensity.NormalCdf(7.1), 13);
        Assert.Equal(6.220960520142783e-16, KernelDensity.NormalCdf(-8), 1e-28);
        Assert.Equal(1, KernelDensity.NormalCdf(37.5));
        Assert.Equal(0, KernelDensity.NormalCdf(-37.5));
        Assert.Equal(0.3989422804014327, KernelDensity.NormalPdf(0), 14);
        Assert.True(double.IsNaN(KernelDensity.NormalCdf(double.NaN)));
    }

    /// <summary>
    ///     Validates the density and the cumulative distribution of the support {1, 2, 4, 7} with Scott's bandwidth against the reference numbers.
    /// </summary>
    [Fact]
    public void KernelDensity_PdfAndCdf_WithScottBandwidthMatchTheReference()
    {
        // Arrange
        var kde = new KernelDensity(Values(1, 2, 4, 7), 0);

        // Act / Assert: the reference bandwidth is 1.798499508023532
        Assert.Equal(1.798499508023532, kde.Bandwidth, 12);
        Assert.Equal(0.08209824809179514, kde.Pdf(0), 12);
        Assert.Equal(0.11697547331069819, kde.Pdf(1), 12);
        Assert.Equal(0.13410533715508258, kde.Pdf(2.5), 12);
        Assert.Equal(0.05242868315629938, kde.Pdf(8), 12);
        Assert.Equal(0.10882028417686626, kde.Cdf(0), 12);
        Assert.Equal(0.40391766964012943, kde.Cdf(2.5), 12);
        Assert.Equal(0.9243387384832086, kde.Cdf(8), 12);
    }

    /// <summary>
    ///     Validates Scott's rule against its formula for the support {1, 2, 4, 7}: the deviation is √7, the quartiles by linear interpolation are 1.75 and 4.75, so the smaller of √7
    ///     and (4.75 - 1.75) / 1.34 is used, times 1.06 and n to the power -0.2.
    /// </summary>
    [Fact]
    public void KernelDensity_EstimateBandwidth_FollowsScottsFormula()
    {
        // Arrange
        var iqr = (4.75 - 1.75) / 1.34;
        var expected = 1.06 * Math.Min(Math.Sqrt(7), iqr) * Math.Pow(4, -0.2);

        // Act
        var bandwidth = KernelDensity.EstimateBandwidth(Values(1, 2, 4, 7));

        // Assert
        Assert.Equal(expected, bandwidth, 14);
    }

    /// <summary>
    ///     Validates the fallbacks of Scott's rule: equal values (a deviation of zero) use the absolute first quartile, zeros use one, a single value uses its absolute value, the
    ///     empty support is infinite, and null, the empty text and text are handled as the reference does.
    /// </summary>
    [Fact]
    public void KernelDensity_EstimateBandwidth_FallsBackWhenTheSpreadIsZero()
    {
        // Arrange / Act / Assert: the reference gives 4.2545302773292235, 0.8509060554658446, 2.12, Infinity, 1.1466663335796377, 0.6350045190043616, 2.7683507912816747
        Assert.Equal(1.06 * 5 * Math.Pow(3, -0.2), KernelDensity.EstimateBandwidth(Values(5, 5, 5)), 14);
        Assert.Equal(1.06 * Math.Pow(3, -0.2), KernelDensity.EstimateBandwidth(Values(0, 0, 0)), 14);
        Assert.Equal(2.12, KernelDensity.EstimateBandwidth(Values(2)), 14);
        Assert.Equal(1.06 * 3 * Math.Pow(2, -0.2), KernelDensity.EstimateBandwidth(Values(-3, -3)), 14);
        Assert.True(double.IsPositiveInfinity(KernelDensity.EstimateBandwidth([])));
        Assert.Equal(1.1466663335796377, KernelDensity.EstimateBandwidth([DataValue.FromNumber(1), default, DataValue.FromNumber(3), DataValue.FromNumber(5), DataValue.FromString("")]), 13);
        Assert.Equal(0.6350045190043616, KernelDensity.EstimateBandwidth([DataValue.FromNumber(1), DataValue.FromString("x"), DataValue.FromNumber(3)]), 13);
    }

    /// <summary>
    ///     Validates that every row counts toward the divisor and that a null reads as zero in the kernel: for the support {1, null, 3, 5, ""} the reference density at 0, 1, 2 and 3 is
    ///     computed over five rows, two of which are at zero.
    /// </summary>
    [Fact]
    public void KernelDensity_Pdf_CountsNullAndEmptyTextAsZero()
    {
        // Arrange
        var kde = new KernelDensity([DataValue.FromNumber(1), default, DataValue.FromNumber(3), DataValue.FromNumber(5), DataValue.FromString("")], 0);

        // Act / Assert
        Assert.Equal(5, kde.Count);
        Assert.Equal(0.18901386778688611, kde.Pdf(0), 12);
        Assert.Equal(0.18008770675214553, kde.Pdf(1), 12);
        Assert.Equal(0.12781883666604243, kde.Pdf(2), 12);
        Assert.Equal(0.1045282347715673, kde.Pdf(3), 12);
    }

    /// <summary>
    ///     Validates that a value that is not a number makes the whole density NaN (the reference gives NaN everywhere), and that the empty support gives NaN with an infinite bandwidth.
    /// </summary>
    [Fact]
    public void KernelDensity_Pdf_OfNonNumericOrEmptySupportIsNaN()
    {
        // Arrange
        var text = new KernelDensity([DataValue.FromNumber(1), DataValue.FromString("abc"), DataValue.FromNumber(3)], 1);
        var empty = new KernelDensity([], 0);

        // Act / Assert
        Assert.True(double.IsNaN(text.Pdf(2)));
        Assert.True(double.IsNaN(text.Cdf(2)));
        Assert.True(double.IsPositiveInfinity(empty.Bandwidth));
        Assert.True(double.IsNaN(empty.Pdf(0)));
    }

    /// <summary>
    ///     Validates the argument check.
    /// </summary>
    [Fact]
    public void KernelDensity_Constructor_RejectsNull()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new KernelDensity(null!, 1));
        Assert.Throws<ArgumentNullException>(() => KernelDensity.EstimateBandwidth(null!));
    }
}
