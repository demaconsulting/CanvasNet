using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the regression models: every model is checked against the closed form of data that the model reproduces exactly (a line, an exponential, a power, a logarithm, a
///     quadratic, a cubic), against the moments of noisy data computed by hand, and, with a tolerance, against the numbers that Vega's <c>vega-statistics</c> produces for the same
///     points (the oracle values in the comments were produced by the reference implementation and are only a cross-check of the derivations).
/// </summary>
public class RegressionFitTests
{
    /// <summary>The relative tolerance of an exact-fit comparison: the last bits of <c>Math.Pow</c>, <c>Math.Exp</c> and <c>Math.Log</c> differ between engines.</summary>
    private const double Tolerance = 1e-12;

    /// <summary>
    ///     Makes the values of a sequence of numbers.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The values.</returns>
    private static DataValue[] Values(IEnumerable<double> values) => [.. values.Select(DataValue.FromNumber)];

    /// <summary>
    ///     Asserts that two numbers agree to a relative tolerance (an absolute one near zero).
    /// </summary>
    /// <param name="expected">The expected number.</param>
    /// <param name="actual">The actual number.</param>
    private static void AssertNear(double expected, double actual) =>
        Assert.True(Math.Abs(expected - actual) <= Tolerance * Math.Max(1, Math.Abs(expected)), $"expected {expected:R}, actual {actual:R}");

    /// <summary>
    ///     Validates that the linear model reproduces <c>y = 2x + 1</c> exactly: the coefficients are 1 and 2, the coefficient of determination is one and the prediction is the line.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_LinearReproducesALineExactly()
    {
        // Arrange
        var x = Values(Enumerable.Range(0, 6).Select(i => (double)i));
        var y = Values(Enumerable.Range(0, 6).Select(i => (2.0 * i) + 1));

        // Act
        var model = RegressionFit.Fit("linear", 3, x, y);

        // Assert
        AssertNear(1, model.Coefficients[0]);
        AssertNear(2, model.Coefficients[1]);
        AssertNear(1, model.RSquared);
        AssertNear(41, model.Predict(20));
    }

    /// <summary>
    ///     Validates the linear model on noisy data against the moments computed by hand: for x = 1..6 and y = 2.1, 3.9, 6.2, 7.8, 10.1, 12.2 the means are 3.5 and 7.05, Sxx is 17.5,
    ///     Sxy is 35.35 and Syy is 71.535, so the slope is 2.02, the intercept is -0.02 and R² is Sxy² / (Sxx · Syy).
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_LinearOnNoisyDataMatchesTheNormalEquations()
    {
        // Arrange
        var x = Values([1, 2, 3, 4, 5, 6]);
        var y = Values([2.1, 3.9, 6.2, 7.8, 10.1, 12.2]);

        // Act
        var model = RegressionFit.Fit("linear", 3, x, y);

        // Assert: the oracle gives [-0.019999999999992468, 2.019999999999998] and 0.9982106661074999
        AssertNear(-0.02, model.Coefficients[0]);
        AssertNear(2.02, model.Coefficients[1]);
        AssertNear(35.35 * 35.35 / (17.5 * 71.535), model.RSquared);
    }

    /// <summary>
    ///     Validates that pairs with a null or a non-numeric value are ignored by the models, so the fit is that of the valid pairs only.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_IgnoresPairsWithoutNumbers()
    {
        // Arrange: the valid pairs are on y = 2x + 1; the others have a null, a text that is not a number, or NaN
        DataValue[] x = [DataValue.FromNumber(0), default, DataValue.FromNumber(1), DataValue.FromNumber(2), DataValue.FromNumber(3), DataValue.FromString("abc"), DataValue.FromNumber(4), DataValue.FromNumber(double.NaN)];
        DataValue[] y = [DataValue.FromNumber(1), DataValue.FromNumber(5), DataValue.FromNumber(3), default, DataValue.FromNumber(7), DataValue.FromNumber(9), DataValue.FromNumber(9), DataValue.FromNumber(2)];

        // Act
        var model = RegressionFit.Fit("linear", 3, x, y);

        // Assert
        AssertNear(1, model.Coefficients[0]);
        AssertNear(2, model.Coefficients[1]);
        AssertNear(1, model.RSquared);
    }

    /// <summary>
    ///     Validates that a column of equal x values has no slope (the variance of x is below the singularity limit): the line is the mean of y and R² is zero (the oracle gives [3, 0] and 0).
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_LinearOnConstantXHasZeroSlope()
    {
        // Arrange
        var x = Values([2, 2, 2]);
        var y = Values([1, 3, 5]);

        // Act
        var model = RegressionFit.Fit("linear", 3, x, y);

        // Assert
        AssertNear(3, model.Coefficients[0]);
        Assert.Equal(0, model.Coefficients[1]);
        AssertNear(0, model.RSquared);
    }

    /// <summary>
    ///     Validates that fitting no valid point gives the NaN of the JavaScript computation (a coefficient of determination of 0/0) instead of an exception.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_WithoutValidPointsGivesNaNRSquared()
    {
        // Arrange
        DataValue[] x = [default, default];
        DataValue[] y = [DataValue.FromNumber(1), DataValue.FromNumber(2)];

        // Act
        var model = RegressionFit.Fit("linear", 3, x, y);

        // Assert
        Assert.Equal([0.0, 0.0], model.Coefficients);
        Assert.True(double.IsNaN(model.RSquared));
    }

    /// <summary>
    ///     Validates the logarithmic model on <c>y = 2 + 3 ln x</c>: coefficients 2 and 3, and the prediction at e² is 8.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_LogReproducesALogarithmicCurve()
    {
        // Arrange
        var x = Values(Enumerable.Range(1, 7).Select(i => (double)i));
        var y = Values(Enumerable.Range(1, 7).Select(i => 2 + (3 * Math.Log(i))));

        // Act
        var model = RegressionFit.Fit("log", 3, x, y);

        // Assert: the oracle gives [2.0000000000000044, 2.999999999999996]
        AssertNear(2, model.Coefficients[0]);
        AssertNear(3, model.Coefficients[1]);
        AssertNear(1, model.RSquared);
        AssertNear(8, model.Predict(Math.Exp(2)));
    }

    /// <summary>
    ///     Validates the exponential model on <c>y = 3 e^(0.5 x)</c>: the multiplier is 3, the rate is 0.5 and the curve predicts <c>3 e^2</c> at 4.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_ExpReproducesAnExponentialCurve()
    {
        // Arrange
        var x = Values(Enumerable.Range(0, 7).Select(i => (double)i));
        var y = Values(Enumerable.Range(0, 7).Select(i => 3 * Math.Exp(0.5 * i)));

        // Act
        var model = RegressionFit.Fit("exp", 3, x, y);

        // Assert: the oracle gives [2.9999999999999982, 0.5000000000000001]
        AssertNear(3, model.Coefficients[0]);
        AssertNear(0.5, model.Coefficients[1]);
        AssertNear(1, model.RSquared);
        AssertNear(3 * Math.Exp(2), model.Predict(4));
    }

    /// <summary>
    ///     Validates the power model on <c>y = 2 x³</c>: the multiplier is 2 (the exp of the log-space fit) and the exponent is 3, and the prediction uses the multiplier, not its logarithm.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_PowReproducesAPowerCurve()
    {
        // Arrange
        var x = Values(Enumerable.Range(1, 7).Select(i => (double)i));
        var y = Values(Enumerable.Range(1, 7).Select(i => 2.0 * i * i * i));

        // Act
        var model = RegressionFit.Fit("pow", 3, x, y);

        // Assert: the oracle gives [1.9999999999999984, 3.0000000000000004]
        AssertNear(2, model.Coefficients[0]);
        AssertNear(3, model.Coefficients[1]);
        AssertNear(1, model.RSquared);
        AssertNear(2000, model.Predict(10));
    }

    /// <summary>
    ///     Validates the quadratic model on <c>y = 1 + 2x + 3x²</c> over x = -3..3: the coefficients are 1, 2 and 3 once the centered fit is transformed back.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_QuadReproducesAParabola()
    {
        // Arrange
        var x = Values(Enumerable.Range(-3, 7).Select(i => (double)i));
        var y = Values(Enumerable.Range(-3, 7).Select(i => 1 + (2.0 * i) + (3.0 * i * i)));

        // Act
        var model = RegressionFit.Fit("quad", 3, x, y);

        // Assert: the oracle gives [1, 2.0000000000000004, 3]
        AssertNear(1, model.Coefficients[0]);
        AssertNear(2, model.Coefficients[1]);
        AssertNear(3, model.Coefficients[2]);
        AssertNear(1, model.RSquared);
        AssertNear(34, model.Predict(3));
    }

    /// <summary>
    ///     Validates the polynomial model of order three on <c>y = 1 - x + 0.5x² + 0.25x³</c> over x = -4..4, and that order four fits the same cubic with a vanishing fourth coefficient.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_PolyReproducesACubic()
    {
        // Arrange
        var xs = Enumerable.Range(-4, 9).Select(i => (double)i).ToArray();
        var x = Values(xs);
        var y = Values(xs.Select(v => 1 - v + (0.5 * v * v) + (0.25 * v * v * v)));

        // Act
        var cubic = RegressionFit.Fit("poly", 3, x, y);
        var quartic = RegressionFit.Fit("poly", 4, x, y);

        // Assert: the oracle gives [0.9999999999999991, -1.0000000000000013, 0.5, 0.2500000000000001] and, for order four, a fourth coefficient of -1.5e-16
        Assert.Equal(4, cubic.Coefficients.Length);
        AssertNear(1, cubic.Coefficients[0]);
        AssertNear(-1, cubic.Coefficients[1]);
        AssertNear(0.5, cubic.Coefficients[2]);
        AssertNear(0.25, cubic.Coefficients[3]);
        AssertNear(1, cubic.RSquared);
        AssertNear(21, cubic.Predict(4));
        Assert.Equal(5, quartic.Coefficients.Length);
        AssertNear(0.25, quartic.Coefficients[3]);
        Assert.True(Math.Abs(quartic.Coefficients[4]) < 1e-12);
    }

    /// <summary>
    ///     Validates that a polynomial of order zero is the constant model (the mean of y, R² of zero), order one is the linear model and order two is the quadratic model.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_LowPolynomialOrdersUseTheDedicatedModels()
    {
        // Arrange
        var x = Values([0, 1, 2, 3, 4, 5]);
        var line = Values([1, 3, 5, 7, 9, 11]);
        var parabola = Values([1, 6, 17, 34, 57, 86]);

        // Act
        var constant = RegressionFit.Fit("poly", 0, x, line);
        var first = RegressionFit.Fit("poly", 1, x, line);
        var second = RegressionFit.Fit("poly", 2, x, parabola);
        var named = RegressionFit.Fit("constant", 3, x, line);

        // Assert: the oracle gives [6] and 0 for the constant, [1, 2] for order one and [1, 2, 3] for order two (y = 1 + 2x + 3x²)
        Assert.Equal([6.0], constant.Coefficients);
        Assert.Equal(0, constant.RSquared);
        Assert.Equal(6, constant.Predict(100));
        Assert.Equal([6.0], named.Coefficients);
        AssertNear(1, first.Coefficients[0]);
        AssertNear(2, first.Coefficients[1]);
        Assert.Equal(3, second.Coefficients.Length);
        AssertNear(1, second.Coefficients[0]);
        AssertNear(2, second.Coefficients[1]);
        AssertNear(3, second.Coefficients[2]);
        AssertNear(1, second.RSquared);
    }

    /// <summary>
    ///     Validates the rule of Vega's constant model, which differs from the other models: it needs only a non-null x, so a pair whose x is text still counts, while a y that is NaN does not.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_ConstantChecksTheXOnlyForNull()
    {
        // Arrange
        DataValue[] x = [DataValue.FromString("abc"), default, DataValue.FromNumber(1), DataValue.FromNumber(2)];
        DataValue[] y = [DataValue.FromNumber(4), DataValue.FromNumber(100), DataValue.FromString("n/a"), DataValue.FromNumber(8)];

        // Act
        var model = RegressionFit.Fit("poly", 0, x, y);

        // Assert: the pairs (abc, 4) and (2, 8) count, so the mean is 6
        Assert.Equal([6.0], model.Coefficients);
    }

    /// <summary>
    ///     Validates the degrees of freedom of each method (the number of rows that a fit needs to exceed).
    /// </summary>
    [Fact]
    public void RegressionFit_DegreesOfFreedom_AreTheParametersOfTheMethod()
    {
        // Arrange / Act / Assert
        Assert.Equal(1, RegressionFit.DegreesOfFreedom("linear", 3));
        Assert.Equal(1, RegressionFit.DegreesOfFreedom("exp", 3));
        Assert.Equal(2, RegressionFit.DegreesOfFreedom("quad", 3));
        Assert.Equal(5, RegressionFit.DegreesOfFreedom("poly", 5));
        Assert.Equal(0, RegressionFit.DegreesOfFreedom("poly", 0));
    }

    /// <summary>
    ///     Validates that the argument errors are reported: an unknown method, an order outside the range, lists of different lengths and a null list.
    /// </summary>
    [Fact]
    public void RegressionFit_Fit_RejectsInvalidArguments()
    {
        // Arrange
        var x = Values([1, 2, 3]);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => RegressionFit.Fit("cubic", 3, x, x));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegressionFit.Fit("poly", -1, x, x));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegressionFit.Fit("poly", RegressionFit.MaxPolynomialOrder + 1, x, x));
        Assert.Throws<ArgumentException>(() => RegressionFit.Fit("linear", 3, x, Values([1, 2])));
        Assert.Throws<ArgumentNullException>(() => RegressionFit.Fit("linear", 3, null!, x));
        Assert.Throws<ArgumentNullException>(() => RegressionFit.Fit("linear", 3, x, null!));
        Assert.Throws<ArgumentNullException>(() => RegressionFit.Fit(null!, 3, x, x));
    }
}
