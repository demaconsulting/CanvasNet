// Portions derived from Vega (BSD-3-Clause) and d3-array (ISC, Mike Bostock); see third-party/README.md
using System.Numerics;
using System.Runtime.InteropServices;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     One measure of an aggregating transform: the operation, the field it reads, its parameter and the name of the result.
/// </summary>
/// <param name="Op">The operation.</param>
/// <param name="Field">The field path (as written in the specification) the operation reads; null for <see cref="AggregateOp.Count"/> of rows.</param>
/// <param name="Param">The weighting parameter <c>r</c> of the exponential operations (ignored by the others).</param>
/// <param name="Name">The name of the output field as written in the specification (a literal name).</param>
internal sealed record AggregateSpec(AggregateOp Op, string? Field, double Param, string Name)
{
    /// <summary>The weighting parameter of an exponential operation that gives none.</summary>
    public const double DefaultExponentialParameter = 0.5;

    /// <summary>
    ///     Gets the name of the output field when the specification gives no <c>as</c>: the operation, then an underscore and the field.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="field">The field path, or null.</param>
    /// <returns><c>count</c> for a count without a field, otherwise <c>op_field</c> (the canonical operation name, so <c>average</c> is <c>mean_x</c>).</returns>
    public static string DefaultName(AggregateOp op, string? field) =>
        field is null ? AggregateMeasures.GetName(op) : $"{AggregateMeasures.GetName(op)}_{field}";
}

/// <summary>
///     What a measure needs when it works: the evaluation whose step budget and string limit it respects and the meter that
///     charges its work.
/// </summary>
/// <param name="Context">The evaluation (step budget, cancellation, string limit) of the call.</param>
/// <param name="Meter">The meter that charges proportional work to the budget.</param>
internal sealed record MeasureEnvironment(ExpressionFunctionContext Context, WorkMeter Meter);

/// <summary>
///     The running state of one aggregate operation over the rows of one group (or one window frame): it takes the values one by
///     one and answers with the result of the operation.
/// </summary>
/// <remarks>
///     <para>
///         The semantics are those of Vega's aggregate operations. A value is <i>missing</i> when it is null or the empty text, and
///         <i>valid</i> unless it is missing or the number NaN (NaN is neither). The operations see only the valid values, except
///         <c>count</c> (every row), <c>missing</c>, <c>distinct</c> (the text of every value, missing ones included) and <c>values</c>
///         (every value in row order). <c>sum</c>, <c>product</c>, <c>mean</c>, <c>exponential</c> and the measures of spread read a
///         value as a number the way JavaScript's unary plus does (text that is not a number is NaN and poisons the result); the
///         variance is accumulated with Welford's update; <c>median</c>, <c>q1</c> and <c>q3</c> are the quantiles 0.5, 0.25 and 0.75 of the
///         non-NaN numbers by linear interpolation (the R-7 method of d3); <c>min</c> and <c>max</c> compare the values with
///         JavaScript's relational operators, so text and dates work and the value returned is the original one; <c>argmin</c> and
///         <c>argmax</c> keep the first row that holds the extreme value. A result that Vega leaves undefined (a sum with no valid
///         value, a variance of fewer than two values) is null. Numbers follow IEEE 754: a sum can overflow to infinity and a
///         NaN stays NaN.
///     </para>
///     <para>
///         <b>Confidence interval.</b> <c>ci0</c> and <c>ci1</c> are the 2.5% and 97.5% quantiles of 1,000 bootstrap means. Vega draws the
///         resamples with <c>Math.random</c>; here they come from a SplitMix64 generator whose seed the caller derives from the JSON
///         pointer of the transform and the group, so the result is the same on every call, and <c>ci0</c> and <c>ci1</c> of one field in
///         one group share their resamples (so <c>ci0 &lt;= ci1</c>).
///     </para>
///     <para>
///         <b>Cost.</b> The cell is a value type of about 80 bytes (<see cref="StateCells"/> cells of the table budget). The operations
///         that keep the values (<c>distinct</c>, <c>values</c>, the quantiles and the confidence interval) allocate a store that
///         the caller bounds up front (rows times such operations against <see cref="TableLayout.MaxCells"/>); sorting a store is
///         charged as n·⌈log₂ n⌉ elements, a bootstrap as 1,000·n, and a distinct key as its text length. A cell is used by one
///         thread.
///     </para>
/// </remarks>
internal struct MeasureCell
{
    /// <summary>The number of table cells that one state is counted as when the memory of the states is bounded.</summary>
    public const int StateCells = 4;

    /// <summary>The number of bootstrap resamples of the confidence interval.</summary>
    internal const int BootstrapSamples = 1000;

    /// <summary>The increment that mixes the group number into a bootstrap seed (the golden-ratio constant of SplitMix64).</summary>
    private const ulong GroupSeedMultiplier = 0x9E3779B97F4A7C15;

    /// <summary>The number of rows added.</summary>
    private long _count;

    /// <summary>The number of valid values.</summary>
    private long _valid;

    /// <summary>The number of missing values.</summary>
    private long _missing;

    /// <summary>The first accumulator: the sum, the product, the mean (of the variance) or the weighted sum.</summary>
    private double _a;

    /// <summary>The second accumulator: the sum of squared deviations.</summary>
    private double _b;

    /// <summary>The row (plus one) that holds the extreme value; zero when there is none.</summary>
    private int _row;

    /// <summary>The extreme value of <c>min</c> and <c>max</c>.</summary>
    private DataValue _extreme;

    /// <summary>The store of the operations that keep values: a list of numbers, a set of texts or a list of values.</summary>
    private object? _store;

    /// <summary>Gets the number of rows added.</summary>
    public readonly long Count => _count;

    /// <summary>Gets the number of valid values added.</summary>
    public readonly long Valid => _valid;

    /// <summary>
    ///     Determines whether an operation keeps the values it sees (so that its memory grows with the rows).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns><see langword="true"/> for <c>distinct</c>, <c>values</c>, the quantiles and the confidence interval.</returns>
    public static bool StoresValues(AggregateOp op) =>
        op is AggregateOp.Distinct or AggregateOp.Values or AggregateOp.Median or AggregateOp.Q1 or AggregateOp.Q3
            or AggregateOp.Ci0 or AggregateOp.Ci1;

    /// <summary>
    ///     Derives the bootstrap seed of a group from the seed of the transform.
    /// </summary>
    /// <param name="transformSeed">The seed of the transform (<see cref="RowSampler.Seed"/> of its JSON pointer).</param>
    /// <param name="group">The number of the group.</param>
    /// <returns>The seed of the group.</returns>
    public static ulong GroupSeed(ulong transformSeed, long group) => unchecked(transformSeed ^ ((ulong)(group + 1) * GroupSeedMultiplier));

    /// <summary>
    ///     Adds the value of one row.
    /// </summary>
    /// <param name="op">The operation of this cell.</param>
    /// <param name="param">The weighting parameter of the exponential operations.</param>
    /// <param name="value">The value of the field in the row (ignored by <see cref="AggregateOp.Count"/>).</param>
    /// <param name="row">The index of the row, remembered by <see cref="AggregateOp.ArgMin"/> and <see cref="AggregateOp.ArgMax"/>.</param>
    /// <param name="env">The evaluation and the meter to charge.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget or the string limit is exceeded.</exception>
    public void Add(AggregateOp op, double param, DataValue value, int row, MeasureEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        _count++;
        if (op == AggregateOp.Count)
        {
            return;
        }

        // The operations that see every row, missing and NaN values included
        if (op == AggregateOp.Distinct)
        {
            var key = ExpressionEvaluator.ToJsString(value, env.Context.MaxStringLength, env.Context);
            env.Meter.Add(1 + key.Length);
            ((HashSet<string>)(_store ??= new HashSet<string>(StringComparer.Ordinal))).Add(key);
            return;
        }

        if (op == AggregateOp.Values)
        {
            env.Meter.Add(1);
            ((List<DataValue>)(_store ??= new List<DataValue>())).Add(value);
            return;
        }

        if (value.IsNull || (value.IsString && value.Text!.Length == 0))
        {
            _missing++;
            return;
        }

        if (value.IsNumber && double.IsNaN(value.Number))
        {
            return;
        }

        _valid++;
        AddValid(op, param, value, row, env);
    }

    /// <summary>
    ///     Gets the result of the operation over the values added.
    /// </summary>
    /// <param name="op">The operation of this cell.</param>
    /// <param name="param">The weighting parameter of the exponential operations.</param>
    /// <param name="seed">The bootstrap seed (<see cref="GroupSeed"/>); read by <c>ci0</c> and <c>ci1</c> only.</param>
    /// <param name="env">The evaluation and the meter to charge.</param>
    /// <param name="rowOf">Reads a whole row as an object by its index, for <c>argmin</c> and <c>argmax</c>; null when they are not used.</param>
    /// <returns>The result; null where Vega's result is undefined.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exceeded.</exception>
    public readonly DataValue Result(AggregateOp op, double param, ulong seed, MeasureEnvironment env, Func<int, DataValue>? rowOf)
    {
        ArgumentNullException.ThrowIfNull(env);
        switch (op)
        {
            case AggregateOp.Count:
                return DataValue.FromNumber(_count);
            case AggregateOp.Valid:
                return DataValue.FromNumber(_valid);
            case AggregateOp.Missing:
                return DataValue.FromNumber(_missing);
            case AggregateOp.Distinct:
                return DataValue.FromNumber(_store is HashSet<string> set ? set.Count : 0);
            case AggregateOp.Values:
                return DataValue.FromOwnedArray(_store is List<DataValue> values ? values.ToArray() : Array.Empty<DataValue>());
            case AggregateOp.Median:
            case AggregateOp.Q1:
            case AggregateOp.Q3:
                var sorted = SortedNumbers(env);
                return sorted.Length == 0 ? default : DataValue.FromNumber(AggregateMeasures.Quantile(sorted, Probability(op)));
            case AggregateOp.Ci0:
            case AggregateOp.Ci1:
                var numbers = SortedNumbers(env);
                return numbers.Length == 0 ? default : DataValue.FromNumber(Bootstrap(numbers, Probability(op), seed, env));
            case AggregateOp.Min:
            case AggregateOp.Max:
                return _valid > 0 ? _extreme : default;
            case AggregateOp.ArgMin:
            case AggregateOp.ArgMax:
                return _valid > 0 && rowOf is not null ? rowOf(_row - 1) : default;
            default:
                return NumericResult(op, param);
        }
    }

    /// <summary>
    ///     Adds a valid value (neither missing nor NaN).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="param">The weighting parameter.</param>
    /// <param name="value">The value.</param>
    /// <param name="row">The row.</param>
    /// <param name="env">The evaluation and the meter.</param>
    private void AddValid(AggregateOp op, double param, DataValue value, int row, MeasureEnvironment env)
    {
        switch (op)
        {
            case AggregateOp.Min or AggregateOp.Max or AggregateOp.ArgMin or AggregateOp.ArgMax:
                AddExtreme(op, value, row, env);
                return;
            case AggregateOp.Valid or AggregateOp.Missing:
                return;
            default:
                break;
        }

        var x = ExpressionEvaluator.ToNumber(value, env.Context);
        switch (op)
        {
            case AggregateOp.Sum:
                _a += x;
                break;
            case AggregateOp.Product:
                _a = _valid == 1 ? x : _a * x;
                break;
            case AggregateOp.Mean:
                _a += (x - _a) / _valid;
                break;
            case AggregateOp.Variance or AggregateOp.VarianceP or AggregateOp.Stdev or AggregateOp.StdevP or AggregateOp.Stderr:
                var delta = x - _a;
                _a += delta / _valid;
                _b += delta * (x - _a);
                break;
            case AggregateOp.Exponential or AggregateOp.ExponentialB:
                _a = (_a * param) + x;
                break;
            case AggregateOp.Median or AggregateOp.Q1 or AggregateOp.Q3 or AggregateOp.Ci0 or AggregateOp.Ci1:
                // d3's quantile ignores NaN, so text that is not a number does not enter the order statistics
                if (!double.IsNaN(x))
                {
                    env.Meter.Add(1);
                    ((List<double>)(_store ??= new List<double>())).Add(x);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Adds a valid value to a minimum or maximum.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="value">The value.</param>
    /// <param name="row">The row.</param>
    /// <param name="env">The evaluation and the meter.</param>
    private void AddExtreme(AggregateOp op, DataValue value, int row, MeasureEnvironment env)
    {
        var minimum = op is AggregateOp.Min or AggregateOp.ArgMin;
        var better = _valid == 1
            || (minimum
                ? ExpressionEvaluator.RelationalLess(value, _extreme, env.Context.MaxStringLength, env.Context) == 1
                : ExpressionEvaluator.RelationalLess(_extreme, value, env.Context.MaxStringLength, env.Context) == 1);
        if (better)
        {
            _extreme = value;
            _row = row + 1;
        }
    }

    /// <summary>
    ///     Gets the result of an operation that is a number computed from the accumulators.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="param">The weighting parameter.</param>
    /// <returns>The result; null when Vega's is undefined.</returns>
    private readonly DataValue NumericResult(AggregateOp op, double param)
    {
        var valid = (double)_valid;
        switch (op)
        {
            case AggregateOp.Sum or AggregateOp.Product or AggregateOp.Mean:
                return _valid > 0 ? DataValue.FromNumber(_a) : default;
            case AggregateOp.Variance:
                return _valid > 1 ? DataValue.FromNumber(_b / (valid - 1)) : default;
            case AggregateOp.VarianceP:
                return _valid > 1 ? DataValue.FromNumber(_b / valid) : default;
            case AggregateOp.Stdev:
                return _valid > 1 ? DataValue.FromNumber(Math.Sqrt(_b / (valid - 1))) : default;
            case AggregateOp.StdevP:
                return _valid > 1 ? DataValue.FromNumber(Math.Sqrt(_b / valid)) : default;
            case AggregateOp.Stderr:
                return _valid > 1 ? DataValue.FromNumber(Math.Sqrt(_b / (valid * (valid - 1)))) : default;
            case AggregateOp.Exponential:
                if (_valid == 0)
                {
                    return default;
                }

                // The weights r^i of the most recent values sum to (1 - r^n) / (1 - r); at r = 1 they are all 1 and the result is the mean
                return DataValue.FromNumber(param >= 1 ? _a / valid : _a * (1 - param) / (1 - Math.Pow(param, valid)));
            case AggregateOp.ExponentialB:
                return _valid > 0 ? DataValue.FromNumber(_a * (1 - param)) : default;
            default:
                return default;
        }
    }

    /// <summary>
    ///     Gets the probability of the quantile that an operation reports.
    /// </summary>
    /// <param name="op">The operation: a quartile, the median or a bound of the confidence interval.</param>
    /// <returns>The probability.</returns>
    private static double Probability(AggregateOp op) => op switch
    {
        AggregateOp.Q1 => 0.25,
        AggregateOp.Q3 => 0.75,
        AggregateOp.Ci0 => 0.025,
        AggregateOp.Ci1 => 0.975,
        _ => 0.5,
    };

    /// <summary>
    ///     Sorts the numbers kept (charging the sort) and returns them.
    /// </summary>
    /// <param name="env">The evaluation and the meter.</param>
    /// <returns>The numbers in ascending order; empty when none were kept.</returns>
    private readonly Span<double> SortedNumbers(MeasureEnvironment env)
    {
        if (_store is not List<double> list)
        {
            return default;
        }

        var span = CollectionsMarshal.AsSpan(list);
        if (span.Length > 1)
        {
            env.Meter.Add((long)span.Length * (BitOperations.Log2((uint)(span.Length - 1)) + 1));
            span.Sort();
        }

        return span;
    }

    /// <summary>
    ///     Computes a quantile of the bootstrap distribution of the mean.
    /// </summary>
    /// <param name="values">The numbers; not empty.</param>
    /// <param name="p">The probability.</param>
    /// <param name="seed">The seed of the generator.</param>
    /// <param name="env">The evaluation and the meter.</param>
    /// <returns>The quantile of the means of 1,000 resamples of the numbers.</returns>
    private static double Bootstrap(ReadOnlySpan<double> values, double p, ulong seed, MeasureEnvironment env)
    {
        var n = values.Length;
        env.Meter.Add((long)BootstrapSamples * n);
        var means = new double[BootstrapSamples];
        var state = seed;
        for (var sample = 0; sample < BootstrapSamples; sample++)
        {
            env.Context.ThrowIfCanceled();
            var sum = 0.0;
            for (var j = 0; j < n; j++)
            {
                sum += values[(int)(RowSampler.Next(ref state) % (ulong)n)];
            }

            means[sample] = sum / n;
        }

        Array.Sort(means);
        return AggregateMeasures.Quantile(means, p);
    }
}
