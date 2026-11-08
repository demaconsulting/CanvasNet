// Portions derived from Vega (BSD-3-Clause) and Apache Commons Math (Apache-2.0); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The probability distribution functions of the closed function table (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     <para>
///         The nine functions are those that Vega's expression language takes from <c>vega-statistics</c> (<c>vega-functions</c>
///         <c>codegen.js</c> lists them in <c>functionContext</c>): <c>densityNormal</c>, <c>cumulativeNormal</c> and
///         <c>quantileNormal</c> (<c>normal.js</c>), <c>densityLogNormal</c>, <c>cumulativeLogNormal</c> and
///         <c>quantileLogNormal</c> (<c>lognormal.js</c>) and <c>densityUniform</c>, <c>cumulativeUniform</c> and
///         <c>quantileUniform</c> (<c>uniform.js</c>). The code follows the upstream source operation by operation, so the
///         results are those of the reference up to the host's <c>exp</c> and <c>log</c>: the cumulative normal is the
///         approximation of West (2009), shared with the kernel density (<see cref="KernelDensity.NormalCdf"/>), and the
///         quantile uses the inverse error function of Mike Giles (GPU Computing Gems, volume 2, 2010) in the form of Apache
///         Commons Math that Vega ports.
///     </para>
///     <para>
///         JavaScript's defaults and coercions are reproduced: a mean or minimum that is falsy (<c>0</c>, NaN, null, a missing
///         argument, the empty string, false) is <c>0</c> (<c>mean || 0</c>); a deviation that is missing or null is <c>1</c>
///         (<c>stdev == null ? 1 : stdev</c>); the uniform functions with a missing or null maximum take the minimum as the
///         maximum and zero as the minimum (a missing minimum gives the interval from 0 to 1); the arguments of the arithmetic
///         are read as JavaScript reads them (null is zero, a missing argument is NaN, a text is parsed) and the comparisons of
///         the uniform functions and of <c>quantileNormal</c> are JavaScript's relational comparison (two texts compare as
///         texts, a NaN compares false). A quantile outside 0 to 1 is NaN.
///     </para>
///     <para>
///         <c>random</c>, <c>sampleNormal</c>, <c>sampleLogNormal</c> and <c>sampleUniform</c> are not in the table: Vega's
///         <c>random</c> is the unseeded <c>Math.random</c>, so every sampling function would make an expression depend on
///         something other than its datum and parameters. A call is reported as <c>vl-expr-function-random</c> (and the
///         like) and gives null, like <c>now</c>. The functions here are pure and deterministic; each call is a constant
///         amount of work.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>The square root of two (vega-statistics <c>Sqrt2</c>).</summary>
    private static readonly double SquareRootOfTwo = Math.Sqrt(2);

    /// <summary>
    ///     Adds the distribution functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddDistributions(Dictionary<string, ExpressionFunction> table)
    {
        table["densityNormal"] = DensityNormal;
        table["cumulativeNormal"] = CumulativeNormal;
        table["quantileNormal"] = QuantileNormal;
        table["densityLogNormal"] = DensityLogNormal;
        table["cumulativeLogNormal"] = CumulativeLogNormal;
        table["quantileLogNormal"] = QuantileLogNormal;
        table["densityUniform"] = DensityUniform;
        table["cumulativeUniform"] = CumulativeUniform;
        table["quantileUniform"] = QuantileUniform;
    }

    /// <summary>
    ///     Reads an argument as a number as JavaScript's arithmetic does.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <param name="context">The evaluation to charge the reading of a text to.</param>
    /// <returns>The number; NaN for a missing argument (<c>undefined</c>).</returns>
    private static double Number(ReadOnlySpan<DataValue> arguments, int index, ExpressionFunctionContext context) =>
        index < arguments.Length ? ExpressionEvaluator.ToNumber(arguments[index], context) : double.NaN;

    /// <summary>
    ///     Gets the first argument of a distribution function as a value to compare and calculate with.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The argument; NaN when it is not supplied (JavaScript's <c>undefined</c> is not a number).</returns>
    private static DataValue Operand(ReadOnlySpan<DataValue> arguments, int index) =>
        index < arguments.Length ? arguments[index] : DataValue.FromNumber(double.NaN);

    /// <summary>
    ///     Applies <c>arguments[index] || 0</c>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The argument when it is truthy, else the number zero.</returns>
    private static DataValue OrZero(ReadOnlySpan<DataValue> arguments, int index) =>
        ExpressionEvaluator.IsTruthy(Arg(arguments, index)) ? arguments[index] : DataValue.FromNumber(0);

    /// <summary>
    ///     Applies <c>arguments[index] == null ? 1 : arguments[index]</c> and reads the result as a number.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <param name="context">The evaluation to charge the reading of a text to.</param>
    /// <returns>One when the argument is missing or null, else the argument as a number.</returns>
    private static double DeviationOrOne(ReadOnlySpan<DataValue> arguments, int index, ExpressionFunctionContext context) =>
        IsAbsent(arguments, index) ? 1 : ExpressionEvaluator.ToNumber(arguments[index], context);

    /// <summary>
    ///     The <c>densityNormal(value, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The probability density.</returns>
    private static DataValue DensityNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var stdev = DeviationOrOne(arguments, 2, context);
        var z = (Number(arguments, 0, context) - ExpressionEvaluator.ToNumber(OrZero(arguments, 1), context)) / stdev;
        return DataValue.FromNumber(Math.Exp(-0.5 * z * z) / (stdev * StatisticsPrimitives.SqrtTwoPi));
    }

    /// <summary>
    ///     The <c>cumulativeNormal(value, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The cumulative probability.</returns>
    private static DataValue CumulativeNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) =>
        DataValue.FromNumber(CumulativeNormalOf(Number(arguments, 0, context), arguments, context));

    /// <summary>
    ///     Evaluates <c>cumulativeNormal(value, mean, stdev)</c> for a value that is already a number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="arguments">The arguments of the call: the mean is the second and the deviation the third.</param>
    /// <param name="context">The context.</param>
    /// <returns>The cumulative probability.</returns>
    private static double CumulativeNormalOf(double value, ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var mean = ExpressionEvaluator.ToNumber(OrZero(arguments, 1), context);
        var stdev = DeviationOrOne(arguments, 2, context);
        return KernelDensity.NormalCdf((value - mean) / stdev);
    }

    /// <summary>
    ///     The <c>quantileNormal(p, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The probability, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The quantile; NaN for a probability outside 0 to 1; the text of a concatenation when the mean is a non-empty text (JavaScript's <c>+</c>).</returns>
    private static DataValue QuantileNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var p = Operand(arguments, 0);
        var limit = context.MaxStringLength;
        if (ExpressionEvaluator.RelationalLess(p, DataValue.FromNumber(0), limit, context) == 1
            || ExpressionEvaluator.RelationalLess(DataValue.FromNumber(1), p, limit, context) == 1)
        {
            return DataValue.FromNumber(double.NaN);
        }

        var scale = DeviationOrOne(arguments, 2, context) * SquareRootOfTwo;
        var offset = scale * InverseErrorFunction((2 * Number(arguments, 0, context)) - 1);
        return AddNumber(OrZero(arguments, 1), offset, context);
    }

    /// <summary>
    ///     The <c>densityLogNormal(value, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The probability density; zero for a value that is not positive.</returns>
    private static DataValue DensityLogNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        // vega-statistics lognormal.js: if (value <= 0) return 0, and a NaN value passes the test
        if (ExpressionEvaluator.RelationalLess(DataValue.FromNumber(0), Operand(arguments, 0), context.MaxStringLength, context) == 0)
        {
            return DataValue.FromNumber(0);
        }

        var value = Number(arguments, 0, context);
        var mean = ExpressionEvaluator.ToNumber(OrZero(arguments, 1), context);
        var stdev = DeviationOrOne(arguments, 2, context);
        var z = (Math.Log(value) - mean) / stdev;
        return DataValue.FromNumber(Math.Exp(-0.5 * z * z) / (stdev * StatisticsPrimitives.SqrtTwoPi * value));
    }

    /// <summary>
    ///     The <c>cumulativeLogNormal(value, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The cumulative probability of the logarithm of the value.</returns>
    private static DataValue CumulativeLogNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) =>
        DataValue.FromNumber(CumulativeNormalOf(Math.Log(Number(arguments, 0, context)), arguments, context));

    /// <summary>
    ///     The <c>quantileLogNormal(p, mean, stdev)</c> function.
    /// </summary>
    /// <param name="arguments">The probability, the mean and the deviation.</param>
    /// <param name="context">The context.</param>
    /// <returns>The exponential of the normal quantile.</returns>
    private static DataValue QuantileLogNormal(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) =>
        DataValue.FromNumber(Math.Exp(ExpressionEvaluator.ToNumber(QuantileNormal(arguments, context), context)));

    /// <summary>
    ///     Reads the interval of a uniform function: a missing or null maximum is the minimum (one without a minimum) and the minimum is then zero.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The minimum and the maximum as given (their types are kept, because the comparisons are JavaScript's).</returns>
    private static (DataValue Minimum, DataValue Maximum) UniformInterval(ReadOnlySpan<DataValue> arguments)
    {
        if (IsAbsent(arguments, 2))
        {
            return (DataValue.FromNumber(0), IsAbsent(arguments, 1) ? DataValue.FromNumber(1) : arguments[1]);
        }

        return (Arg(arguments, 1), arguments[2]);
    }

    /// <summary>
    ///     The <c>densityUniform(value, min, max)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the minimum and the maximum.</param>
    /// <param name="context">The context.</param>
    /// <returns>The probability density: one over the width inside the interval, else zero.</returns>
    private static DataValue DensityUniform(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var (minimum, maximum) = UniformInterval(arguments);
        var value = Operand(arguments, 0);
        var limit = context.MaxStringLength;

        // value >= min && value <= max (a comparison with NaN is false)
        if (ExpressionEvaluator.RelationalLess(value, minimum, limit, context) == 0
            && ExpressionEvaluator.RelationalLess(maximum, value, limit, context) == 0)
        {
            return DataValue.FromNumber(1 / (ExpressionEvaluator.ToNumber(maximum, context) - ExpressionEvaluator.ToNumber(minimum, context)));
        }

        return DataValue.FromNumber(0);
    }

    /// <summary>
    ///     The <c>cumulativeUniform(value, min, max)</c> function.
    /// </summary>
    /// <param name="arguments">The value, the minimum and the maximum.</param>
    /// <param name="context">The context.</param>
    /// <returns>Zero below the interval, one above it, else the fraction of the interval.</returns>
    private static DataValue CumulativeUniform(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var (minimum, maximum) = UniformInterval(arguments);
        var value = Operand(arguments, 0);
        var limit = context.MaxStringLength;
        if (ExpressionEvaluator.RelationalLess(value, minimum, limit, context) == 1)
        {
            return DataValue.FromNumber(0);
        }

        if (ExpressionEvaluator.RelationalLess(maximum, value, limit, context) == 1)
        {
            return DataValue.FromNumber(1);
        }

        var low = ExpressionEvaluator.ToNumber(minimum, context);
        return DataValue.FromNumber((Number(arguments, 0, context) - low) / (ExpressionEvaluator.ToNumber(maximum, context) - low));
    }

    /// <summary>
    ///     The <c>quantileUniform(p, min, max)</c> function.
    /// </summary>
    /// <param name="arguments">The probability, the minimum and the maximum.</param>
    /// <param name="context">The context.</param>
    /// <returns>The point of the interval at the probability; NaN outside 0 to 1; a text when the minimum is a non-empty text (JavaScript's <c>+</c>).</returns>
    private static DataValue QuantileUniform(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var (minimum, maximum) = UniformInterval(arguments);
        var p = Operand(arguments, 0);
        var limit = context.MaxStringLength;
        if (ExpressionEvaluator.RelationalLess(p, DataValue.FromNumber(0), limit, context) == 0
            && ExpressionEvaluator.RelationalLess(DataValue.FromNumber(1), p, limit, context) == 0)
        {
            var width = ExpressionEvaluator.ToNumber(maximum, context) - ExpressionEvaluator.ToNumber(minimum, context);
            return AddNumber(minimum, Number(arguments, 0, context) * width, context);
        }

        return DataValue.FromNumber(double.NaN);
    }

    /// <summary>
    ///     Adds a number to a value as JavaScript's <c>+</c> does.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The number.</param>
    /// <param name="context">The context that bounds the text and is charged for it.</param>
    /// <returns>The sum, or the concatenation when the primitive of the left operand is a text.</returns>
    private static DataValue AddNumber(DataValue left, double right, ExpressionFunctionContext context)
    {
        var limit = context.MaxStringLength;
        var primitive = ExpressionEvaluator.ToPrimitive(left, false, limit, context);
        if (!primitive.IsString)
        {
            return DataValue.FromNumber(ExpressionEvaluator.ToNumber(primitive, context) + right);
        }

        var first = ExpressionEvaluator.ToJsString(primitive, limit);
        var second = ExpressionEvaluator.ToJsString(DataValue.FromNumber(right), limit);
        var length = (long)first.Length + second.Length;
        context.CheckStringLength(length);
        context.ChargeSize(length);
        return DataValue.FromString(string.Concat(first, second));
    }

    /// <summary>
    ///     Approximates the inverse error function (vega-statistics <c>normal.js</c> <c>erfinv</c>).
    /// </summary>
    /// <remarks>
    ///     The approximation of Mike Giles, "Approximating the erfinv function" (GPU Computing Gems, volume 2, 2010), in the double precision
    ///     form of Apache Commons Math that Vega ports: a polynomial in <c>w = -ln((1 - x)(1 + x))</c> for <c>w &lt; 6.25</c>, one in
    ///     <c>sqrt(w)</c> up to <c>w = 16</c> and a third beyond; the product <c>(1 - x)(1 + x)</c> is not simplified because
    ///     that would lose the precision near plus and minus one. The result is infinite at plus and minus one.
    /// </remarks>
    /// <param name="x">The argument, from -1 to 1.</param>
    /// <returns>The value whose error function is <paramref name="x"/>; NaN for NaN and for an argument outside -1 to 1.</returns>
    private static double InverseErrorFunction(double x)
    {
        var w = -Math.Log((1 - x) * (1 + x));
        double p;
        if (w < 6.25)
        {
            w -= 3.125;
            p = -3.6444120640178196996e-21;
            p = -1.685059138182016589e-19 + (p * w);
            p = 1.2858480715256400167e-18 + (p * w);
            p = 1.115787767802518096e-17 + (p * w);
            p = -1.333171662854620906e-16 + (p * w);
            p = 2.0972767875968561637e-17 + (p * w);
            p = 6.6376381343583238325e-15 + (p * w);
            p = -4.0545662729752068639e-14 + (p * w);
            p = -8.1519341976054721522e-14 + (p * w);
            p = 2.6335093153082322977e-12 + (p * w);
            p = -1.2975133253453532498e-11 + (p * w);
            p = -5.4154120542946279317e-11 + (p * w);
            p = 1.051212273321532285e-09 + (p * w);
            p = -4.1126339803469836976e-09 + (p * w);
            p = -2.9070369957882005086e-08 + (p * w);
            p = 4.2347877827932403518e-07 + (p * w);
            p = -1.3654692000834678645e-06 + (p * w);
            p = -1.3882523362786468719e-05 + (p * w);
            p = 0.0001867342080340571352 + (p * w);
            p = -0.00074070253416626697512 + (p * w);
            p = -0.0060336708714301490533 + (p * w);
            p = 0.24015818242558961693 + (p * w);
            p = 1.6536545626831027356 + (p * w);
        }
        else if (w < 16.0)
        {
            w = Math.Sqrt(w) - 3.25;
            p = 2.2137376921775787049e-09;
            p = 9.0756561938885390979e-08 + (p * w);
            p = -2.7517406297064545428e-07 + (p * w);
            p = 1.8239629214389227755e-08 + (p * w);
            p = 1.5027403968909827627e-06 + (p * w);
            p = -4.013867526981545969e-06 + (p * w);
            p = 2.9234449089955446044e-06 + (p * w);
            p = 1.2475304481671778723e-05 + (p * w);
            p = -4.7318229009055733981e-05 + (p * w);
            p = 6.8284851459573175448e-05 + (p * w);
            p = 2.4031110387097893999e-05 + (p * w);
            p = -0.0003550375203628474796 + (p * w);
            p = 0.00095328937973738049703 + (p * w);
            p = -0.0016882755560235047313 + (p * w);
            p = 0.0024914420961078508066 + (p * w);
            p = -0.0037512085075692412107 + (p * w);
            p = 0.005370914553590063617 + (p * w);
            p = 1.0052589676941592334 + (p * w);
            p = 3.0838856104922207635 + (p * w);
        }
        else if (double.IsFinite(w))
        {
            w = Math.Sqrt(w) - 5.0;
            p = -2.7109920616438573243e-11;
            p = -2.5556418169965252055e-10 + (p * w);
            p = 1.5076572693500548083e-09 + (p * w);
            p = -3.7894654401267369937e-09 + (p * w);
            p = 7.6157012080783393804e-09 + (p * w);
            p = -1.4960026627149240478e-08 + (p * w);
            p = 2.9147953450901080826e-08 + (p * w);
            p = -6.7711997758452339498e-08 + (p * w);
            p = 2.2900482228026654717e-07 + (p * w);
            p = -9.9298272942317002539e-07 + (p * w);
            p = 4.5260625972231537039e-06 + (p * w);
            p = -1.9681778105531670567e-05 + (p * w);
            p = 7.5995277030017761139e-05 + (p * w);
            p = -0.00021503011930044477347 + (p * w);
            p = -0.00013871931833623122026 + (p * w);
            p = 1.0103004648645343977 + (p * w);
            p = 4.8499064014085844221 + (p * w);
        }
        else
        {
            p = double.PositiveInfinity;
        }

        return p * x;
    }
}
