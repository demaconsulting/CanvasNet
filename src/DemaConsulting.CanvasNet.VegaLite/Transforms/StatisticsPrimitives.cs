// Portions derived from Vega (BSD-3-Clause: vega-statistics, vega-util) and d3-array (ISC, Mike Bostock); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The small JavaScript-faithful building blocks that the statistical transforms share: the validity tests of
///     vega-statistics (<c>numbers</c>, the points of a regression), vega-util's <c>extent</c>, d3-array's <c>deviation</c> and
///     <c>range</c>, and the JavaScript truthiness of a number.
/// </summary>
/// <remarks>
///     <para>
///         Vega computes on JavaScript values, so the same data can be valid for one statistic and invalid for another: the
///         <c>numbers</c> filter of the quantiles drops <c>null</c>, the empty text and NaN; d3's <c>deviation</c> keeps the
///         empty text (it is the number zero); a regression keeps a pair when both values are not null and convert to
///         numbers. These helpers reproduce each rule separately instead of unifying them, because unifying them would change
///         the result for a column that mixes numbers, texts and nulls. A JavaScript <c>undefined</c> result (the quantile of
///         nothing, the deviation of one value) is NaN here, and a caller that must tell the two apart converts NaN to null.
///     </para>
///     <para>
///         All helpers are pure and thread-safe. The work they do is proportional to the length of their input; the callers
///         charge it to the step budget of the call.
///     </para>
/// </remarks>
internal static class StatisticsPrimitives
{
    /// <summary>The square root of two pi, the normalizing constant of the Gaussian density (vega-statistics <c>constants.js</c>).</summary>
    public static readonly double SqrtTwoPi = Math.Sqrt(2 * Math.PI);

    /// <summary>
    ///     Gets the JavaScript truthiness of a number: false for zero and NaN.
    /// </summary>
    /// <remarks>
    ///     Vega writes <c>a || b</c> for "a, unless it is zero or undefined"; this is the test of that idiom.
    /// </remarks>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when the number is neither zero nor NaN.</returns>
    public static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    ///     Gets the smaller of two numbers the way JavaScript's <c>Math.min</c> does: NaN when either is NaN.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The minimum, or NaN.</returns>
    public static double JsMin(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Min(a, b);

    /// <summary>
    ///     Gets the larger of two numbers the way JavaScript's <c>Math.max</c> does: NaN when either is NaN.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The maximum, or NaN.</returns>
    public static double JsMax(double a, double b) => double.IsNaN(a) || double.IsNaN(b) ? double.NaN : Math.Max(a, b);

    /// <summary>
    ///     Reads a value the way vega-statistics' <c>numbers()</c> does and keeps it only when it is a number.
    /// </summary>
    /// <remarks>
    ///     The rule is <c>value != null &amp;&amp; value !== '' &amp;&amp; (value = +value) &gt;= value</c>: null, the empty text and every
    ///     value that converts to NaN are dropped; booleans, dates, numeric texts and single-element arrays are converted.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="number">Receives the number.</param>
    /// <returns><see langword="true"/> when the value is a number for the statistics.</returns>
    public static bool TryNumber(DataValue value, out double number)
    {
        number = 0;
        if (value.IsNull || (value.IsString && value.Text!.Length == 0))
        {
            return false;
        }

        number = ExpressionEvaluator.ToNumber(value);
        return !double.IsNaN(number);
    }

    /// <summary>
    ///     Collects the numbers of a list of values (vega-statistics <c>numbers</c>), in order.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The numbers; the values that are not numbers are left out.</returns>
    public static double[] Numbers(IReadOnlyList<DataValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var numbers = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (TryNumber(value, out var number))
            {
                numbers.Add(number);
            }
        }

        return [.. numbers];
    }

    /// <summary>
    ///     Gets quantiles of a list of values (vega-statistics <c>quantiles</c>): the numbers of the values are sorted and each
    ///     probability is read by linear interpolation between the closest ranks.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="probabilities">The probabilities.</param>
    /// <returns>One quantile per probability; NaN (JavaScript <c>undefined</c>) when there are no numbers or the probability is NaN.</returns>
    public static double[] Quantiles(IReadOnlyList<DataValue> values, IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);
        var sorted = Numbers(values);
        Array.Sort(sorted);
        var result = new double[probabilities.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = AggregateMeasures.Quantile(sorted, probabilities[i]);
        }

        return result;
    }

    /// <summary>
    ///     Gets the sample standard deviation the way d3-array's <c>deviation</c> does.
    /// </summary>
    /// <remarks>
    ///     The values that count are those that are not null and whose number is not NaN, which keeps the empty text (it is
    ///     zero). The variance is accumulated with Welford's update, as d3 does, so the last bits agree.
    /// </remarks>
    /// <param name="values">The values.</param>
    /// <returns>The deviation; NaN (JavaScript <c>undefined</c>) when fewer than two values count.</returns>
    public static double Deviation(IReadOnlyList<DataValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        long count = 0;
        double mean = 0;
        double sum = 0;
        foreach (var value in values)
        {
            if (value.IsNull)
            {
                continue;
            }

            var x = ExpressionEvaluator.ToNumber(value);
            if (double.IsNaN(x))
            {
                continue;
            }

            var delta = x - mean;
            mean += delta / ++count;
            sum += delta * (x - mean);
        }

        // d3: variance is undefined for fewer than two values; the deviation is the square root unless the variance is falsy
        if (count <= 1)
        {
            return double.NaN;
        }

        var variance = sum / (count - 1);
        return IsTruthy(variance) ? Math.Sqrt(variance) : variance;
    }

    /// <summary>
    ///     Finds the smallest and the largest value of a list the way vega-util's <c>extent</c> does.
    /// </summary>
    /// <remarks>
    ///     The first value that is neither null nor NaN starts the extent; later values that are not null are compared with
    ///     JavaScript's relational operators, so texts compare as texts, numbers as numbers and dates by their milliseconds,
    ///     and a NaN fails every comparison. A list without such a value has no extent.
    /// </remarks>
    /// <param name="values">The values.</param>
    /// <param name="maxLength">The longest text the comparison of an array may build (<see cref="VegaLiteLimits.MaxStringLength"/>).</param>
    /// <returns>The minimum and the maximum, or <see langword="null"/> for a list without a valid first value.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text of an array value would be longer than <paramref name="maxLength"/>.</exception>
    public static (DataValue Min, DataValue Max)? Extent(IReadOnlyList<DataValue> values, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(values);
        var i = 0;
        while (i < values.Count && (values[i].IsNull || (values[i].IsNumber && double.IsNaN(values[i].Number))))
        {
            i++;
        }

        if (i >= values.Count)
        {
            return null;
        }

        var min = values[i];
        var max = min;
        for (; i < values.Count; i++)
        {
            var value = values[i];
            if (value.IsNull)
            {
                continue;
            }

            if (ExpressionEvaluator.RelationalLess(value, min, maxLength) == 1)
            {
                min = value;
            }

            if (ExpressionEvaluator.RelationalLess(max, value, maxLength) == 1)
            {
                max = value;
            }
        }

        return (min, max);
    }

    /// <summary>
    ///     Counts the values of an arithmetic range the way d3-array's <c>range(start, stop, step)</c> does.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="stop">The exclusive end.</param>
    /// <param name="step">The step.</param>
    /// <returns>The number of values: <c>max(0, ceil((stop - start) / step))</c>, saturating at <see cref="long.MaxValue"/> for a quotient that no integer can hold; zero when the quotient is NaN.</returns>
    public static long RangeCount(double start, double stop, double step)
    {
        var count = Math.Max(0, Math.Ceiling((stop - start) / step));

        // d3 writes `| 0`, which wraps modulo 2^32; the callers bound the count long before it matters, so the exact quotient is kept,
        // saturated (the same on every runtime: a plain cast of an out-of-range number is not defined) and NaN (a zero step of zero) is empty
        return NumericConversions.ToInt64Saturating(count);
    }
}
