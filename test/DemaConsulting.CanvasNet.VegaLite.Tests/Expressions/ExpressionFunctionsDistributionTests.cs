using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the distribution functions of the expression function table: the density, cumulative distribution and quantile of the normal, log-normal and uniform distributions of <c>vega-statistics</c>. The
///     expected numbers are those that <c>vega-statistics</c> 2.0.0 (the package that Vega 6.4 uses) returns for the same calls, run once in Node.js, and are compared at a relative tolerance of 1e-12 (the host's
///     <c>exp</c> and <c>log</c> can differ from V8's in the last bits); the closed forms (the symmetry of the normal distribution, the median of a log-normal distribution, the linear uniform quantile) are asserted
///     as well, and the JavaScript defaults and coercions are checked by hand.
/// </summary>
public class ExpressionFunctionsDistributionTests
{
    /// <summary>The relative tolerance of a comparison with a number of the reference.</summary>
    private const double RelativeTolerance = 1e-12;

    /// <summary>
    ///     Validates the density, cumulative and quantile functions of the normal, log-normal and uniform distributions against the numbers of <c>vega-statistics</c>: the defaults (mean 0, deviation 1, the interval from 0 to
    ///     1), other parameters, the forms of the uniform functions with one bound (the interval from 0 to that bound), a degenerate interval, a log-normal value that is not positive (the density is zero and the logarithm
    ///     is NaN for the cumulative function) and a quantile outside 0 to 1.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected number.</param>
    [Theory]
    [InlineData("densityNormal(0)", 0.3989422804014327)]
    [InlineData("densityNormal(1)", 0.24197072451914337)]
    [InlineData("densityNormal(0.5, 1, 2)", 0.19333405840142465)]
    [InlineData("densityNormal(-3.2, 0.7, 0.4)", 2.2713835779941664e-21)]
    [InlineData("densityNormal(10, 0, 1)", 7.69459862670642e-23)]
    [InlineData("densityNormal(2, 2, 3)", 0.1329807601338109)]
    [InlineData("cumulativeNormal(0)", 0.5)]
    [InlineData("cumulativeNormal(1)", 0.841344746068543)]
    [InlineData("cumulativeNormal(-1)", 0.15865525393145702)]
    [InlineData("cumulativeNormal(1.96)", 0.9750021048517795)]
    [InlineData("cumulativeNormal(0.5, 1, 2)", 0.40129367431707635)]
    [InlineData("cumulativeNormal(-3.2, 0.7, 0.4)", 9.223413490845477e-23)]
    [InlineData("cumulativeNormal(8)", 0.9999999999999993)]
    [InlineData("cumulativeNormal(-8)", 6.220960520142783e-16)]
    [InlineData("cumulativeNormal(10, 0, 1)", 1)]
    [InlineData("cumulativeNormal(40)", 1)]
    [InlineData("cumulativeNormal(-40)", 0)]
    [InlineData("cumulativeNormal(3, 3, 3)", 0.5)]
    [InlineData("quantileNormal(0.5)", 0)]
    [InlineData("quantileNormal(0.975)", 1.959963984540054)]
    [InlineData("quantileNormal(0.025)", -1.959963984540054)]
    [InlineData("quantileNormal(0.1, 1, 2)", -1.5631031310892012)]
    [InlineData("quantileNormal(0.9, 10, 0.5)", 10.6407757827723)]
    [InlineData("quantileNormal(0.999999)", 4.753424308817088)]
    [InlineData("quantileNormal(1e-10)", -6.361340889697421)]
    [InlineData("quantileNormal(0.3, 5, 0)", 5)]
    [InlineData("quantileNormal(0)", double.NegativeInfinity)]
    [InlineData("quantileNormal(1)", double.PositiveInfinity)]
    [InlineData("quantileNormal(1.5)", double.NaN)]
    [InlineData("quantileNormal(-0.1)", double.NaN)]
    [InlineData("densityLogNormal(1)", 0.3989422804014327)]
    [InlineData("densityLogNormal(2)", 0.15687401927898112)]
    [InlineData("densityLogNormal(0.5, 0, 1)", 0.6274960771159245)]
    [InlineData("densityLogNormal(3, 1, 0.5)", 0.26083887270192596)]
    [InlineData("densityLogNormal(0)", 0)]
    [InlineData("densityLogNormal(-1)", 0)]
    [InlineData("densityLogNormal(10, 2, 3)", 0.013230606542731352)]
    [InlineData("cumulativeLogNormal(1)", 0.5)]
    [InlineData("cumulativeLogNormal(2)", 0.7558914042144171)]
    [InlineData("cumulativeLogNormal(0.5, 0, 1)", 0.2441085957855828)]
    [InlineData("cumulativeLogNormal(3, 1, 0.5)", 0.5781741008028731)]
    [InlineData("cumulativeLogNormal(0)", 0)]
    [InlineData("cumulativeLogNormal(-1)", double.NaN)]
    [InlineData("cumulativeLogNormal(100, 2, 3)", 0.8074095832722964)]
    [InlineData("quantileLogNormal(0.5)", 1)]
    [InlineData("quantileLogNormal(0.9)", 3.6022244792791582)]
    [InlineData("quantileLogNormal(0.1, 1, 0.5)", 1.432217893498685)]
    [InlineData("quantileLogNormal(0.99, 2, 3)", 7935.039589099351)]
    [InlineData("quantileLogNormal(0)", 0)]
    [InlineData("quantileLogNormal(1)", double.PositiveInfinity)]
    [InlineData("quantileLogNormal(2)", double.NaN)]
    [InlineData("densityUniform(0.5)", 1)]
    [InlineData("densityUniform(0)", 1)]
    [InlineData("densityUniform(1)", 1)]
    [InlineData("densityUniform(2)", 0)]
    [InlineData("densityUniform(5, 2, 8)", 0.16666666666666666)]
    [InlineData("densityUniform(1, 2, 8)", 0)]
    [InlineData("densityUniform(3, 5)", 0.2)]
    [InlineData("densityUniform(-1, -3, 0)", 0.3333333333333333)]
    [InlineData("densityUniform(2, 2, 2)", double.PositiveInfinity)]
    [InlineData("cumulativeUniform(0.25)", 0.25)]
    [InlineData("cumulativeUniform(-1)", 0)]
    [InlineData("cumulativeUniform(2)", 1)]
    [InlineData("cumulativeUniform(5, 2, 8)", 0.5)]
    [InlineData("cumulativeUniform(3, 5)", 0.6)]
    [InlineData("cumulativeUniform(8, 2, 8)", 1)]
    [InlineData("cumulativeUniform(2, 2, 2)", double.NaN)]
    [InlineData("quantileUniform(0.25)", 0.25)]
    [InlineData("quantileUniform(0)", 0)]
    [InlineData("quantileUniform(1)", 1)]
    [InlineData("quantileUniform(1.5)", double.NaN)]
    [InlineData("quantileUniform(-0.5)", double.NaN)]
    [InlineData("quantileUniform(0.5, 2, 8)", 5)]
    [InlineData("quantileUniform(0.5, 4)", 2)]
    [InlineData("quantileUniform(0.1, -2, 2)", -1.6)]
    public void ExpressionFunctionsDistribution_Functions_MatchVegaStatistics(string text, double expected)
    {
        // Act
        var actual = EvalNumber(text);

        // Assert
        AssertClose(expected, actual, text);
    }

    /// <summary>
    ///     Validates the closed forms: the normal distribution is symmetric, the quantile inverts the cumulative distribution over the whole range of probabilities (including the three polynomial ranges of the inverse
    ///     error function), and the median of a log-normal distribution is the exponential of its mean.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsDistribution_ClosedForms_Hold()
    {
        // Arrange: probabilities from the far lower tail (w beyond 16) to the far upper tail
        double[] probabilities = [1e-10, 1e-8, 1e-6, 0.001, 0.02, 0.2, 0.5, 0.8, 0.98, 0.999, 1 - 1e-6];

        foreach (var p in probabilities)
        {
            // Act
            var z = EvalNumber($"quantileNormal({p:R})");
            var back = EvalNumber($"cumulativeNormal({z:R})");

            // Assert: the round trip is exact to the rounding of 2p - 1 (about 1e-16 absolute) and the accuracy of the approximation of West (about 1e-12 relative)
            Assert.True(Math.Abs(back - p) <= 2e-16 + (1e-12 * p), $"p={p:R}: quantile {z:R} maps back to {back:R}");
        }

        Assert.Equal(1, EvalNumber("cumulativeNormal(1.3) + cumulativeNormal(-1.3)"), 14);
        Assert.Equal(EvalNumber("densityNormal(1.3)"), EvalNumber("densityNormal(-1.3)"));
        Assert.Equal(Math.Exp(2.5), EvalNumber("quantileLogNormal(0.5, 2.5, 1.5)"), 12);
        Assert.Equal(0.5, EvalNumber("cumulativeLogNormal(exp(2.5), 2.5, 1.5)"), 14);
    }

    /// <summary>
    ///     Validates the numerical integration of the density: the normal density integrates to the cumulative distribution (the trapezoid rule over a fine grid), so the density and the cumulative function agree.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsDistribution_Density_IntegratesToTheCumulativeDistribution()
    {
        // Arrange: integrate the density of N(1, 2) from -9 to 4 in 20,000 steps
        const int steps = 20000;
        const double from = -9;
        const double to = 4;
        var step = (to - from) / steps;
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        double Density(double x) => ExpressionFunctions.Invoke("densityNormal", [DataValue.FromNumber(x), DataValue.FromNumber(1), DataValue.FromNumber(2)], context).Number;

        // Act
        var sum = (Density(from) + Density(to)) / 2;
        for (var i = 1; i < steps; i++)
        {
            sum += Density(from + (i * step));
        }

        var integral = sum * step;
        var expected = ExpressionFunctions.Invoke("cumulativeNormal", [DataValue.FromNumber(to), DataValue.FromNumber(1), DataValue.FromNumber(2)], context).Number
            - ExpressionFunctions.Invoke("cumulativeNormal", [DataValue.FromNumber(from), DataValue.FromNumber(1), DataValue.FromNumber(2)], context).Number;

        // Assert
        Assert.Equal(expected, integral, 8);
    }

    /// <summary>
    ///     Validates the JavaScript defaults and coercions of the arguments: a falsy mean is 0, a missing or null deviation is 1, the uniform functions with one bound or none, null is zero, a missing value is NaN and a
    ///     text is read as a number.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected number.</param>
    [Theory]
    [InlineData("densityNormal(0, null, null)", 0.3989422804014327)]
    [InlineData("densityNormal(0, 0, null)", 0.3989422804014327)]
    [InlineData("densityNormal(0, '', 1)", 0.3989422804014327)]
    [InlineData("densityNormal(null)", 0.3989422804014327)]
    [InlineData("densityNormal('1')", 0.24197072451914337)]
    [InlineData("densityNormal(1, '1')", 0.3989422804014327)]
    [InlineData("densityNormal(true)", 0.24197072451914337)]
    [InlineData("densityNormal()", double.NaN)]
    [InlineData("densityNormal(0, 0, 'x')", double.NaN)]
    [InlineData("cumulativeNormal(null)", 0.5)]
    [InlineData("cumulativeNormal(0, NaN, null)", 0.5)]
    [InlineData("cumulativeNormal()", double.NaN)]
    [InlineData("cumulativeNormal(1, 0, 0)", 1)]
    [InlineData("cumulativeNormal(-1, 0, 0)", 0)]
    [InlineData("cumulativeNormal(1, 1, 0)", double.NaN)]
    [InlineData("quantileNormal('0.5')", 0)]
    [InlineData("quantileNormal(null)", double.NegativeInfinity)]
    [InlineData("quantileNormal()", double.NaN)]
    [InlineData("quantileNormal(NaN)", double.NaN)]
    [InlineData("quantileNormal(0.5, 3, null)", 3)]
    [InlineData("quantileNormal(0.5, 3, 2)", 3)]
    [InlineData("quantileNormal(0.5, NaN)", 0)]
    [InlineData("densityLogNormal()", double.NaN)]
    [InlineData("densityLogNormal(null)", 0)]
    [InlineData("densityLogNormal('2')", 0.15687401927898112)]
    [InlineData("cumulativeLogNormal()", double.NaN)]
    [InlineData("densityUniform(0.5, null, null)", 1)]
    [InlineData("densityUniform(3, null, 5)", 0.2)]
    [InlineData("densityUniform(null, null, 5)", 0.2)]
    [InlineData("densityUniform(3, 5, null)", 0.2)]
    [InlineData("densityUniform()", 0)]
    [InlineData("cumulativeUniform(1, '', 4)", 0.25)]
    [InlineData("cumulativeUniform(null, 1, 3)", 0)]
    [InlineData("cumulativeUniform()", double.NaN)]
    [InlineData("quantileUniform('0.5', 2, 4)", 3)]
    [InlineData("quantileUniform(null)", 0)]
    [InlineData("quantileUniform()", double.NaN)]
    [InlineData("quantileUniform(0.5, 2, null)", 1)]
    public void ExpressionFunctionsDistribution_Arguments_FollowJavaScript(string text, double expected)
    {
        // Act
        var actual = EvalNumber(text);

        // Assert: the numbers of these calls are exact (a default, a coercion or a branch), so they are compared to 12 digits
        if (double.IsNaN(expected) || double.IsInfinity(expected))
        {
            Assert.Equal(expected, actual);
        }
        else
        {
            Assert.Equal(expected, actual, 12);
        }
    }

    /// <summary>
    ///     Validates that the comparisons of the uniform functions and the sum of <c>quantileNormal</c> are JavaScript's: two texts compare as texts, and a text mean makes the sum a concatenation (the expected values are
    ///     those of <c>vega-statistics</c>).
    /// </summary>
    [Fact]
    public void ExpressionFunctionsDistribution_TextOperands_FollowJavaScriptOperators()
    {
        // Act / Assert: 'b' is between 'a' and 'c' as texts, and the width 'c' - 'a' is NaN, so the density is 1 / NaN
        Assert.True(double.IsNaN(EvalNumber("densityUniform('b', 'a', 'c')")));

        // '10' is not below '1' and not above '9' as texts (the comparison is by characters), so the fraction is (10 - 1) / (9 - 1)
        Assert.Equal(1.125, EvalNumber("cumulativeUniform('10', '1', '9')"));

        // 'p' is a text that is not a number: neither p < 0 nor p > 1 (a NaN compare is false), so erfinv(NaN) is NaN
        Assert.True(double.IsNaN(EvalNumber("quantileNormal('p')")));

        // The text mean concatenates with the number as in JavaScript ('5' + 0 is '50')
        Assert.Equal("50", Eval("quantileNormal(0.5, '5')").Text);
        Assert.Equal("a0", Eval("quantileNormal(0.5, 'a')").Text);
        Assert.Equal("20.5", Eval("quantileUniform(0.5, '2', 3)").Text);
    }

    /// <summary>
    ///     Validates that the sampling functions stay outside the table (an expression must be a pure function of its datum): <c>random</c> and the three <c>sample</c> functions are not callable and the evaluator
    ///     yields null.
    /// </summary>
    /// <param name="name">The function name.</param>
    [Theory]
    [InlineData("random")]
    [InlineData("sampleNormal")]
    [InlineData("sampleLogNormal")]
    [InlineData("sampleUniform")]
    public void ExpressionFunctionsDistribution_Sampling_IsNotInTheTable(string name)
    {
        // Act
        var found = ExpressionFunctions.TryGet(name, out _);
        var compiled = Compile($"{name}()");
        var result = new ExpressionEvaluator(null, CancellationToken.None).Evaluate(compiled);

        // Assert: the call is reported as an unsupported function and evaluates to null
        Assert.False(found);
        Assert.Contains(compiled.UnsupportedConstructs, c => c.Identifier == "vl-expr-function-" + name.ToLowerInvariant());
        Assert.True(result.IsNull);
    }

    /// <summary>
    ///     Validates that the functions are deterministic and that a repeated call gives bit-identical results (no state).
    /// </summary>
    [Fact]
    public void ExpressionFunctionsDistribution_Repeated_ResultsAreIdentical()
    {
        // Arrange
        string[] calls = ["cumulativeNormal(0.3, 1, 2)", "quantileNormal(0.123)", "densityLogNormal(2.5, 1, 0.7)", "quantileUniform(0.3, 1, 5)"];

        foreach (var call in calls)
        {
            // Act
            var first = BitConverter.DoubleToInt64Bits(EvalNumber(call));
            var second = BitConverter.DoubleToInt64Bits(EvalNumber(call));

            // Assert
            Assert.Equal(first, second);
        }
    }

    /// <summary>
    ///     Compares a number with the reference at the relative tolerance; NaN and the infinities must be equal.
    /// </summary>
    /// <param name="expected">The reference number.</param>
    /// <param name="actual">The computed number.</param>
    /// <param name="text">The expression text for the failure message.</param>
    private static void AssertClose(double expected, double actual, string text)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual), $"{text} gave {actual:R}, expected NaN");
            return;
        }

        if (double.IsInfinity(expected) || expected == 0)
        {
            Assert.Equal(expected, actual);
            return;
        }

        Assert.True(Math.Abs(actual - expected) <= RelativeTolerance * Math.Abs(expected), $"{text} gave {actual:R}, expected {expected:R}");
    }
}
