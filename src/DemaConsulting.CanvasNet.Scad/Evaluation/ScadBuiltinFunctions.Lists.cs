namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The list-oriented built-in functions: <c>len</c>, <c>concat</c>, <c>norm</c>, <c>cross</c>, <c>lookup</c> and <c>search</c>.
/// </summary>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>
    ///     Gets the descriptors of the list functions.
    /// </summary>
    /// <returns>The descriptors.</returns>
    private static IEnumerable<Descriptor> ListFunctions()
    {
        yield return Define("len", 1, 1, Len);
        yield return Define("concat", 0, -1, Concat);
        yield return Define("norm", 1, 1, Norm);
        yield return Define("cross", 2, 2, Cross);
        yield return Define("lookup", 2, 2, Lookup, "key", "table");
        yield return Define("search", 2, 4, Search, "match_value", "search_vector", "num_returns_per_match", "index_col_num");
    }

    /// <summary>
    ///     Implements <c>len</c>: the number of elements of a vector or Unicode scalars of a string.
    /// </summary>
    /// <remarks>
    ///     Counting the scalars of a string is linear, so one step is charged per 256 characters on top of the base step.
    /// </remarks>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The length, or <c>undef</c> with a warning for any other kind (ranges included).</returns>
    private static ScadValue Len(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        switch (args[0].Kind)
        {
            case ScadValueKind.Vector:
                return ScadValue.FromNumber(args[0].AsVector.Count);
            case ScadValueKind.String:
                var text = args[0].AsString;
                host.Charge(1 + (text.Length / 256));
                return ScadValue.FromNumber(ScadText.ScalarCount(text));
            default:
                return BadArgument(host, "len", "a vector or a string", args[0]);
        }
    }

    /// <summary>
    ///     Implements <c>concat</c>: appends the arguments, flattening one level of vectors.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The combined vector.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the result would exceed the list length limit.</exception>
    private static ScadValue Concat(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        // The result length is known up front, so the limit is checked before anything is allocated
        long total = 0;
        foreach (var arg in args)
        {
            total += arg.Kind == ScadValueKind.Vector ? arg.AsVector.Count : 1;
        }

        host.CheckListLength(total);
        host.Charge(Math.Max(1, total));

        var items = new List<ScadValue>((int)total);
        foreach (var arg in args)
        {
            if (arg.Kind == ScadValueKind.Vector)
            {
                items.AddRange(arg.AsVector.AsSpan());
            }
            else
            {
                items.Add(arg);
            }
        }

        return ScadValue.FromVector(items);
    }

    /// <summary>
    ///     Implements <c>norm</c>: the Euclidean length of a numeric vector.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The length (zero for the empty vector), or <c>undef</c> with a warning.</returns>
    private static ScadValue Norm(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        if (!TryGetNumbers(args[0], out var numbers))
        {
            return BadArgument(host, "norm", "a vector of numbers", args[0]);
        }

        host.Charge(Math.Max(1, numbers.Length));
        var sum = 0.0;
        foreach (var number in numbers)
        {
            sum += number * number;
        }

        return ScadValue.FromNumber(Math.Sqrt(sum));
    }

    /// <summary>
    ///     Implements <c>cross</c>: the cross product of two 3-vectors, or the scalar cross product of two 2-vectors.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The product, or <c>undef</c> with a warning.</returns>
    private static ScadValue Cross(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        if (!TryGetNumbers(args[0], out var a) || !TryGetNumbers(args[1], out var b) || a.Length != b.Length || a.Length is not (2 or 3))
        {
            return BadArgument(host, "cross", "two numeric vectors of length 2 or 3", args[0]);
        }

        if (a.Length == 2)
        {
            return ScadValue.FromNumber((a[0] * b[1]) - (a[1] * b[0]));
        }

        return ScadValue.FromVector(
        [
            ScadValue.FromNumber((a[1] * b[2]) - (a[2] * b[1])),
            ScadValue.FromNumber((a[2] * b[0]) - (a[0] * b[2])),
            ScadValue.FromNumber((a[0] * b[1]) - (a[1] * b[0])),
        ]);
    }

    /// <summary>
    ///     Implements <c>lookup</c>: linear interpolation in a table of key-value pairs.
    /// </summary>
    /// <remarks>
    ///     The table need not be sorted: the nearest keys below and above the lookup key are found by scanning every
    ///     valid pair (a vector whose first two elements are numbers). A key outside the table's range is clamped to the
    ///     value of the nearest extreme key; an exact match returns that pair's value (the first such pair wins).
    /// </remarks>
    /// <param name="args">The bound arguments: the key and the table.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The interpolated value, or <c>undef</c> with a warning for a bad key or a table without valid pairs.</returns>
    private static ScadValue Lookup(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        if (args[0].Kind != ScadValueKind.Number || double.IsNaN(args[0].AsNumber))
        {
            return BadArgument(host, "lookup", "a number as the key", args[0]);
        }

        if (args[1].Kind != ScadValueKind.Vector)
        {
            return BadArgument(host, "lookup", "a table of [key, value] pairs", args[1]);
        }

        // Scan every pair once, remembering the closest key at or below and at or above the lookup key
        var key = args[0].AsNumber;
        var table = args[1].AsVector;
        host.Charge(Math.Max(1, table.Count));
        var bracket = default(Bracket);
        foreach (var entry in table.AsSpan())
        {
            if (TryGetPair(entry, out var pairKey, out var pairValue))
            {
                bracket.Offer(key, pairKey, pairValue);
            }
        }

        return bracket.HasLow || bracket.HasHigh
            ? ScadValue.FromNumber(bracket.Interpolate(key))
            : BadArgument(host, "lookup", "a table of [key, value] pairs", args[1]);
    }

    /// <summary>
    ///     Reads a key-value pair from a table entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the entry is a vector whose first two elements are numbers.</returns>
    private static bool TryGetPair(ScadValue entry, out double key, out double value)
    {
        key = 0;
        value = 0;
        if (entry.Kind != ScadValueKind.Vector || entry.AsVector.Count < 2)
        {
            return false;
        }

        var pair = entry.AsVector;
        if (pair[0].Kind != ScadValueKind.Number || pair[1].Kind != ScadValueKind.Number)
        {
            return false;
        }

        key = pair[0].AsNumber;
        value = pair[1].AsNumber;
        return true;
    }

    /// <summary>
    ///     Reads a vector of numbers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="numbers">The numbers when the value is a vector of numbers (possibly empty); otherwise an empty array.</param>
    /// <returns><see langword="true"/> when every element is a number.</returns>
    private static bool TryGetNumbers(ScadValue value, out double[] numbers)
    {
        numbers = [];
        if (value.Kind != ScadValueKind.Vector)
        {
            return false;
        }

        var vector = value.AsVector;
        var result = new double[vector.Count];
        for (var i = 0; i < result.Length; i++)
        {
            if (vector[i].Kind != ScadValueKind.Number)
            {
                return false;
            }

            result[i] = vector[i].AsNumber;
        }

        numbers = result;
        return true;
    }

    /// <summary>
    ///     Tracks the table entries closest to a lookup key from below and from above.
    /// </summary>
    private struct Bracket
    {
        /// <summary>Gets a value indicating whether a key at or below the lookup key was seen.</summary>
        public bool HasLow { get; private set; }

        /// <summary>Gets a value indicating whether a key at or above the lookup key was seen.</summary>
        public bool HasHigh { get; private set; }

        /// <summary>The largest key at or below the lookup key.</summary>
        private double _lowKey;

        /// <summary>The value belonging to <see cref="_lowKey"/>.</summary>
        private double _lowValue;

        /// <summary>The smallest key at or above the lookup key.</summary>
        private double _highKey;

        /// <summary>The value belonging to <see cref="_highKey"/>.</summary>
        private double _highValue;

        /// <summary>
        ///     Considers a table pair as the closest entry below and/or above the lookup key; the first of equal keys wins.
        /// </summary>
        /// <param name="key">The lookup key.</param>
        /// <param name="pairKey">The pair's key.</param>
        /// <param name="pairValue">The pair's value.</param>
        public void Offer(double key, double pairKey, double pairValue)
        {
            if (pairKey <= key && (!HasLow || pairKey > _lowKey))
            {
                (HasLow, _lowKey, _lowValue) = (true, pairKey, pairValue);
            }

            if (pairKey >= key && (!HasHigh || pairKey < _highKey))
            {
                (HasHigh, _highKey, _highValue) = (true, pairKey, pairValue);
            }
        }

        /// <summary>
        ///     Computes the interpolated (or clamped) value.
        /// </summary>
        /// <param name="key">The lookup key.</param>
        /// <returns>The value.</returns>
        public readonly double Interpolate(double key)
        {
            // Outside the table the nearest extreme wins; on an exact key the low entry is that key
            if (!HasLow)
            {
                return _highValue;
            }

            if (!HasHigh || ScadNumbers.AreEqual(_lowKey, _highKey))
            {
                return _lowValue;
            }

            return _lowValue + ((key - _lowKey) / (_highKey - _lowKey) * (_highValue - _lowValue));
        }
    }
}
