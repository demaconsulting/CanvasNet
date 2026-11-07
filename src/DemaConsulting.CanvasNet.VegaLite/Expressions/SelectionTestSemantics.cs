// Portions derived from Vega's vega-selections and vega-util (BSD-3-Clause): selectionTest, selectionIdTest, selectionResolve and inrange; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The meaning of a selection store: whether a datum is in the selection, and the fields and values the selection stands for.
/// </summary>
/// <remarks>
///     <para>
///         This is the algorithm of Vega's selection functions (<c>vlSelectionTest</c>, <c>vlSelectionIdTest</c> and <c>vlSelectionResolve</c>), re-implemented
///         over <see cref="DataValue"/> with JavaScript's comparison rules. A tuple holds, for each projected field, an enumerated value, a list of
///         values or a range; a datum matches a tuple when it matches every field (an enumerated scalar by strict equality, a list by membership,
///         a range by inclusive bounds that are swapped when they are reversed, and a binned range with an exclusive end). By default the tuples are
///         joined by union (a datum in any tuple is selected); with the operation <c>intersect</c> the tuples of one unit are joined by union and the
///         units by intersection. A datum that does not have a projected field never matches that field.
///     </para>
///     <para>
///         Every function is pure and charges the work to the step budget of the evaluation (one step for each field it tests), so a store of
///         many tuples over many rows cannot run past <see cref="VegaLiteLimits.MaxEvaluationSteps"/>.
///     </para>
/// </remarks>
internal static class SelectionTestSemantics
{
    /// <summary>The name of the field that holds the identity of a row in a row-identity selection.</summary>
    public const string SelectionId = "_vgsid_";

    /// <summary>The operation that intersects the units of a selection.</summary>
    public const string Intersect = "intersect";

    /// <summary>The operation that unites the units of a selection; the default.</summary>
    public const string Union = "union";

    /// <summary>
    ///     Tests whether a datum is in a selection (Vega's <c>vlSelectionTest</c>).
    /// </summary>
    /// <param name="store">The store, or null when the data set does not exist.</param>
    /// <param name="datum">The datum, an object.</param>
    /// <param name="operation">The operation, <c>intersect</c> or anything else for the union.</param>
    /// <param name="context">The evaluation that the work is charged to.</param>
    /// <returns>True when the datum is in the selection; the number zero for a store without tuples (as Vega returns it), which is falsy.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    public static DataValue Test(SelectionStore? store, DataValue datum, string? operation, ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entries = store?.Entries ?? [];
        var intersect = string.Equals(operation, Intersect, StringComparison.Ordinal);

        // The units of the store and how many tuples each holds (Vega keeps this index for the intersect operation)
        Dictionary<string, int>? unitCounts = null;
        Dictionary<string, int>? misses = null;
        if (intersect && entries.Count > 0)
        {
            unitCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var unit in entries.Select(entry => entry.Unit))
            {
                unitCounts[unit] = unitCounts.GetValueOrDefault(unit) + 1;
            }

            misses = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        foreach (var entry in entries)
        {
            if (unitCounts is not null && misses is not null)
            {
                // Tuples of one unit are united and the units are intersected: a unit that matched is done, a unit whose tuples all missed fails the test
                var count = misses.GetValueOrDefault(entry.Unit);
                if (count == -1)
                {
                    continue;
                }

                var matched = TestPoint(datum, entry, context);
                misses[entry.Unit] = matched ? -1 : ++count;
                if (matched && unitCounts.Count == 1)
                {
                    return DataValue.FromBoolean(true);
                }

                if (!matched && count == unitCounts[entry.Unit])
                {
                    return DataValue.FromBoolean(false);
                }
            }
            else
            {
                // A miss that must intersect fails the test, a match that need not intersect passes it
                var matched = TestPoint(datum, entry, context);
                if (intersect != matched)
                {
                    return DataValue.FromBoolean(matched);
                }
            }
        }

        // No miss was seen when intersecting, no match was seen when uniting, and nothing at all is falsy
        return entries.Count == 0 ? DataValue.FromNumber(0) : DataValue.FromBoolean(intersect);
    }

    /// <summary>
    ///     Tests whether a row is in a row-identity selection (Vega's <c>vlSelectionIdTest</c>). A store that is projected over fields (not the identity) is tested as <see cref="Test"/> does: the
    ///     specification chooses the function by the projection it can see, and a default projection of the caller's configuration (or a value of the caller) can replace it; Vega-Lite never
    ///     compiles the identity test for such a store.
    /// </summary>
    /// <param name="store">The store, or null when the data set does not exist.</param>
    /// <param name="datum">The datum, an object that has <c>_vgsid_</c>.</param>
    /// <param name="operation">The operation, <c>intersect</c> or anything else for the union.</param>
    /// <param name="context">The evaluation that the work is charged to.</param>
    /// <returns>True when the row identity is in the store (in every unit when intersecting).</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    public static DataValue IdTest(SelectionStore? store, DataValue datum, string? operation, ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entries = store?.Entries ?? [];
        var intersect = string.Equals(operation, Intersect, StringComparison.Ordinal);
        if (entries.Any(entry => entry.Fields is not null))
        {
            // The store is projected over fields, so the identity test was named by a projection that the specification could not see (a default projection
            // of the caller's configuration, or a value of the caller): the predicate Vega-Lite compiles for such a store is the field test
            return Test(store, datum, operation, context);
        }

        if (!TryGet(datum, [SelectionId], out var value))
        {
            return DataValue.FromBoolean(false);
        }

        context.ChargeSteps(entries.Count);
        var holders = 0;
        foreach (var _ in entries.Where(entry => entry.Fields is null && ExpressionEvaluator.StrictEquals(entry.Id, value)))
        {
            holders++;
        }

        if (holders == 0)
        {
            return DataValue.FromBoolean(false);
        }

        if (intersect)
        {
            // Every unit of the store must hold the identity
            var units = entries.Select(e => e.Unit).Distinct(StringComparer.Ordinal).Count();
            return DataValue.FromBoolean(units == 1 || holders >= units);
        }

        return DataValue.FromBoolean(true);
    }

    /// <summary>
    ///     Tests a datum against one tuple: every projected field must match.
    /// </summary>
    /// <param name="datum">The datum.</param>
    /// <param name="entry">The tuple.</param>
    /// <param name="context">The evaluation that the work is charged to.</param>
    /// <returns>True when every field matches.</returns>
    private static bool TestPoint(DataValue datum, SelectionEntry entry, ExpressionFunctionContext context)
    {
        var fields = entry.Fields;
        if (fields is null)
        {
            // A row-identity tuple has no fields to compare: it holds when the identity of the datum is the one stored
            context.Step();
            return TryGet(datum, [SelectionId], out var id) && ExpressionEvaluator.StrictEquals(id, entry.Id, context);
        }

        for (var i = 0; i < fields.Count; i++)
        {
            context.Step();
            var field = fields[i];
            if (!field.HasPath || !TryGet(datum, field.Path, out var value))
            {
                return false;
            }

            if (value.IsDate)
            {
                value = DataValue.FromNumber(value.Milliseconds);
            }

            var stored = entry.Values[i];
            var max = context.MaxStringLength;
            switch (field.Type)
            {
                case "E":
                    // Enumerated fields hold one value (single and multi selections) or a list of values (interval selections)
                    context.ChargeSize(stored.IsArray ? stored.Elements.Length : 1);
                    if (stored.IsArray ? !Includes(stored, value) : !ExpressionEvaluator.StrictEquals(value, stored, context))
                    {
                        return false;
                    }

                    break;
                case "R":
                    if (!InRange(value, stored, true, true, max, context))
                    {
                        return false;
                    }

                    break;
                case "R-RE":
                    // The range of a bin includes its start and not its end
                    if (!InRange(value, stored, true, false, max, context))
                    {
                        return false;
                    }

                    break;
                case "R-E":
                    if (!InRange(value, stored, false, false, max, context))
                    {
                        return false;
                    }

                    break;
                case "R-LE":
                    if (!InRange(value, stored, false, true, max, context))
                    {
                        return false;
                    }

                    break;
                case "E-LT":
                    // dval >= value is a miss; an undefined comparison (NaN) is not
                    if (ExpressionEvaluator.RelationalLess(value, stored, max, context) == 0)
                    {
                        return false;
                    }

                    break;
                case "E-LTE":
                    if (ExpressionEvaluator.RelationalLess(stored, value, max, context) == 1)
                    {
                        return false;
                    }

                    break;
                case "E-GT":
                    if (ExpressionEvaluator.RelationalLess(stored, value, max, context) == 0)
                    {
                        return false;
                    }

                    break;
                case "E-GTE":
                    if (ExpressionEvaluator.RelationalLess(value, stored, max, context) == 1)
                    {
                        return false;
                    }

                    break;
                case "E-VALID":
                    if (value.IsNull || double.IsNaN(ExpressionEvaluator.ToNumber(value, context)))
                    {
                        return false;
                    }

                    break;
                case "E-ONE":
                    context.ChargeSize(stored.IsArray ? stored.Elements.Length : 0);
                    if (!stored.IsArray || !stored.Elements.ToArray().Any(e => ExpressionEvaluator.StrictEquals(e, value)))
                    {
                        return false;
                    }

                    break;
                default:
                    // A type that Vega does not know does not constrain the field
                    break;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tests a value against a range the way Vega's <c>inrange</c> does: the ends are the first and last element of the range, swapped when reversed.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="range">The range, an array; anything else holds no value.</param>
    /// <param name="includeStart">Whether the lower end is included.</param>
    /// <param name="includeEnd">Whether the upper end is included.</param>
    /// <param name="maxLength">The longest text that a comparison converts to.</param>
    /// <param name="context">The evaluation that the work is charged to.</param>
    /// <returns>True when the value is in the range; false when a comparison is undefined (a NaN).</returns>
    private static bool InRange(DataValue value, DataValue range, bool includeStart, bool includeEnd, int maxLength, ExpressionFunctionContext context)
    {
        if (!range.IsArray || range.Elements.Length == 0)
        {
            return false;
        }

        var low = range.Elements[0];
        var high = range.Elements[^1];
        if (ExpressionEvaluator.RelationalLess(high, low, maxLength, context) == 1)
        {
            (low, high) = (high, low);
        }

        var lowerHolds = includeStart
            ? ExpressionEvaluator.RelationalLess(value, low, maxLength, context) == 0
            : ExpressionEvaluator.RelationalLess(low, value, maxLength, context) == 1;
        var upperHolds = includeEnd
            ? ExpressionEvaluator.RelationalLess(high, value, maxLength, context) == 0
            : ExpressionEvaluator.RelationalLess(value, high, maxLength, context) == 1;
        return lowerHolds && upperHolds;
    }

    /// <summary>
    ///     Tests whether an array holds a value the way JavaScript's <c>Array.prototype.includes</c> does (strict equality, but NaN equals NaN).
    /// </summary>
    /// <param name="list">The array.</param>
    /// <param name="value">The value.</param>
    /// <returns>True when the array holds the value.</returns>
    private static bool Includes(DataValue list, DataValue value)
    {
        foreach (var item in list.Elements)
        {
            if (SameValueZero(item, value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Compares two values with JavaScript's SameValueZero rule: strict equality, except that NaN equals NaN.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>True when the values are the same.</returns>
    private static bool SameValueZero(DataValue a, DataValue b) =>
        ExpressionEvaluator.StrictEquals(a, b) || (a.IsNumber && b.IsNumber && double.IsNaN(a.Number) && double.IsNaN(b.Number));

    /// <summary>
    ///     Reads the value at a path of a datum (Vega's <c>field</c> accessor).
    /// </summary>
    /// <param name="datum">The datum.</param>
    /// <param name="path">The segments of the path.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns>False when the datum has no member at the path (JavaScript's <c>undefined</c>).</returns>
    private static bool TryGet(DataValue datum, IReadOnlyList<string> path, out DataValue value)
    {
        value = datum;
        foreach (var segment in path)
        {
            if (value.IsObject && value.ObjectValue!.TryGetValue(segment, out var member))
            {
                value = member;
            }
            else if (value.IsArray && int.TryParse(segment, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) && index < value.Elements.Length)
            {
                value = value.Elements[index];
            }
            else
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Resolves a selection into the fields and values it stands for (Vega's <c>vlSelectionResolve</c>): each projected field with the values or the range
    ///     that is selected, and, for a point selection, <c>vlPoint</c>, the logical query that describes it.
    /// </summary>
    /// <param name="store">The store, or null when the data set does not exist.</param>
    /// <param name="operation">How the units are combined: <c>intersect</c>, otherwise the union.</param>
    /// <param name="isMulti">Whether the selection is a point selection (which also gets <c>vlPoint</c>).</param>
    /// <param name="vegaLite5">Whether the query is named <c>vlPoint</c> (Vega-Lite 5 and later) rather than <c>vlMulti</c>.</param>
    /// <param name="context">The evaluation that the work is charged to.</param>
    /// <returns>An object with one member for each field; an empty object for an empty store.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    public static DataValue Resolve(SelectionStore? store, string? operation, bool isMulti, bool vegaLite5, ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var entries = store?.Entries ?? [];
        var resolved = new Ordered<Ordered<List<DataValue>>>();
        var types = new Dictionary<string, char>(StringComparer.Ordinal);
        var multi = new Ordered<List<DataValue>>();

        // First unite all entries of the same unit
        foreach (var entry in entries)
        {
            context.ChargeSteps(Math.Max(1, entry.Fields?.Count ?? 1));
            if (entry.Fields is { } fields)
            {
                for (var j = 0; j < fields.Count; j++)
                {
                    var perUnit = resolved.GetOrAdd(fields[j].Field, () => new Ordered<List<DataValue>>());
                    var list = perUnit.GetOrAdd(entry.Unit, () => []);
                    var type = fields[j].Type[0];
                    types[fields[j].Field] = type;
                    var incoming = AsList(entry.Values[j]);
                    perUnit.Set(entry.Unit, type == 'E' ? UniteValues(list, incoming) : UniteRange(list, incoming));
                }

                if (isMulti)
                {
                    var members = fields.Select((f, j) => new KeyValuePair<string, DataValue>(f.Field, entry.Values[j]));
                    multi.GetOrAdd(entry.Unit, () => []).Add(DataValue.FromObject(DataObject.Create(members)));
                }
            }
            else
            {
                // A row-identity store holds identities that are unique to each unit
                var perUnit = resolved.GetOrAdd(SelectionId, () => new Ordered<List<DataValue>>());
                perUnit.GetOrAdd(entry.Unit, () => []).Add(entry.Id);
                if (isMulti)
                {
                    multi.GetOrAdd(entry.Unit, () => []).Add(DataValue.FromObject(DataObject.Create([new KeyValuePair<string, DataValue>(SelectionId, entry.Id)])));
                }
            }
        }

        // Then resolve the fields across the units as the operation says
        var op = string.IsNullOrEmpty(operation) ? Union : operation;
        var intersect = string.Equals(op, Intersect, StringComparison.Ordinal);
        var members2 = new List<KeyValuePair<string, DataValue>>();
        if (resolved.TryGetValue(SelectionId, out var ids))
        {
            members2.Add(new KeyValuePair<string, DataValue>(SelectionId, DataValue.FromOwnedArray([.. CombineIds(ids.Values.ToList(), intersect)])));
        }
        else
        {
            foreach (var key in resolved.Keys)
            {
                var units = resolved.Get(key).Values.ToList();
                var acc = units[0];
                for (var k = 1; k < units.Count; k++)
                {
                    acc = CombineUnits(types[key], intersect, acc, units[k]);
                }

                members2.Add(new KeyValuePair<string, DataValue>(key, DataValue.FromOwnedArray([.. acc])));
            }
        }

        // A point selection also states the query that describes it
        if (isMulti && multi.Keys.Count > 0)
        {
            DataValue query;
            if (intersect)
            {
                var and = multi.Keys.Select(k => Single("or", DataValue.FromOwnedArray([.. multi.Get(k)]))).ToArray();
                query = Single("and", DataValue.FromOwnedArray(and));
            }
            else
            {
                query = Single("or", DataValue.FromOwnedArray([.. multi.Keys.SelectMany(k => multi.Get(k))]));
            }

            members2.Add(new KeyValuePair<string, DataValue>(vegaLite5 ? "vlPoint" : "vlMulti", query));
        }

        return DataValue.FromObject(DataObject.Create(members2));
    }

    /// <summary>
    ///     Makes an object with one member.
    /// </summary>
    /// <param name="name">The member name.</param>
    /// <param name="value">The member value.</param>
    /// <returns>The object.</returns>
    private static DataValue Single(string name, DataValue value) =>
        DataValue.FromObject(DataObject.Create([new KeyValuePair<string, DataValue>(name, value)]));

    /// <summary>
    ///     Combines the values that two units selected for a field.
    /// </summary>
    /// <param name="type">The tuple type character: <c>E</c> for values, anything else for a range.</param>
    /// <param name="intersect">Whether to intersect (otherwise unite).</param>
    /// <param name="accumulated">The values of the units so far.</param>
    /// <param name="next">The values of the next unit.</param>
    /// <returns>The combined values.</returns>
    private static List<DataValue> CombineUnits(char type, bool intersect, List<DataValue> accumulated, List<DataValue> next)
    {
        if (type == 'E')
        {
            return intersect ? IntersectValues(accumulated, next) : UniteValues(accumulated, next);
        }

        return intersect ? IntersectRange(accumulated, next) : UniteRange(accumulated, next);
    }

    /// <summary>
    ///     Converts a value to a list the way Vega's <c>array</c> does: an array stays, null is empty, anything else is a list of one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A new list.</returns>
    private static List<DataValue> AsList(DataValue value)
    {
        if (value.IsArray)
        {
            return [.. value.Elements.ToArray()];
        }

        return value.IsNull ? [] : [value];
    }

    /// <summary>
    ///     United enumerated values: the values of the base followed by those of the incoming list that it does not hold.
    /// </summary>
    /// <param name="baseList">The values so far.</param>
    /// <param name="incoming">The values to add.</param>
    /// <returns>A new list.</returns>
    private static List<DataValue> UniteValues(List<DataValue> baseList, List<DataValue> incoming)
    {
        if (baseList.Count == 0)
        {
            return [.. incoming];
        }

        var result = new List<DataValue>(baseList);
        foreach (var value in incoming.Where(v => !result.Any(r => SameValueZero(r, v))))
        {
            result.Add(value);
        }

        return result;
    }

    /// <summary>
    ///     Intersects enumerated values.
    /// </summary>
    /// <param name="baseList">The values so far.</param>
    /// <param name="incoming">The values of the next unit.</param>
    /// <returns>A new list: the values of the base that the incoming list holds; the incoming list when the base is empty.</returns>
    private static List<DataValue> IntersectValues(List<DataValue> baseList, List<DataValue> incoming) =>
        baseList.Count == 0 ? [.. incoming] : [.. baseList.Where(v => incoming.Any(i => SameValueZero(i, v)))];

    /// <summary>
    ///     Reads the ends of a range as numbers, the smaller first.
    /// </summary>
    /// <param name="range">The range list.</param>
    /// <returns>The lower and the upper end.</returns>
    private static (double Low, double High) Ends(List<DataValue> range)
    {
        var low = range.Count > 0 ? ExpressionEvaluator.ToNumber(range[0]) : double.NaN;
        var high = range.Count > 1 ? ExpressionEvaluator.ToNumber(range[1]) : double.NaN;
        return low > high ? (high, low) : (low, high);
    }

    /// <summary>
    ///     Unites ranges: the smallest range that holds both.
    /// </summary>
    /// <param name="baseList">The range so far (empty, or two numbers).</param>
    /// <param name="incoming">The range to add.</param>
    /// <returns>A new range.</returns>
    private static List<DataValue> UniteRange(List<DataValue> baseList, List<DataValue> incoming)
    {
        var (low, high) = Ends(incoming);
        if (baseList.Count == 0)
        {
            return [DataValue.FromNumber(low), DataValue.FromNumber(high)];
        }

        var start = ExpressionEvaluator.ToNumber(baseList[0]);
        var end = ExpressionEvaluator.ToNumber(baseList[1]);
        return [DataValue.FromNumber(start > low ? low : start), DataValue.FromNumber(end < high ? high : end)];
    }

    /// <summary>
    ///     Intersects ranges.
    /// </summary>
    /// <param name="baseList">The range so far (empty, or two numbers).</param>
    /// <param name="incoming">The range of the next unit.</param>
    /// <returns>A new range; empty when the ranges do not overlap.</returns>
    private static List<DataValue> IntersectRange(List<DataValue> baseList, List<DataValue> incoming)
    {
        var (low, high) = Ends(incoming);
        if (baseList.Count == 0)
        {
            return [DataValue.FromNumber(low), DataValue.FromNumber(high)];
        }

        var start = ExpressionEvaluator.ToNumber(baseList[0]);
        var end = ExpressionEvaluator.ToNumber(baseList[1]);
        if (high < start || end < low)
        {
            return [];
        }

        return [DataValue.FromNumber(start < low ? low : start), DataValue.FromNumber(end > high ? high : end)];
    }

    /// <summary>
    ///     Combines the row identities of the units: the union, or the intersection, without repeats.
    /// </summary>
    /// <param name="units">The identities of each unit.</param>
    /// <param name="intersect">Whether to intersect.</param>
    /// <returns>The combined identities.</returns>
    private static List<DataValue> CombineIds(List<List<DataValue>> units, bool intersect)
    {
        var result = new List<DataValue>();
        foreach (var id in units.SelectMany(u => u))
        {
            if (!result.Any(r => SameValueZero(r, id)) && (!intersect || units.All(u => u.Any(x => SameValueZero(x, id)))))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    ///     A map that keeps the order in which keys were added (the order in which a JavaScript object lists them).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    private sealed class Ordered<T>
    {
        /// <summary>The values by key.</summary>
        private readonly Dictionary<string, T> _values = new(StringComparer.Ordinal);

        /// <summary>The keys in the order they were added.</summary>
        private readonly List<string> _keys = [];

        /// <summary>Gets the keys in the order they were added.</summary>
        public IReadOnlyList<string> Keys => _keys;

        /// <summary>Gets the values in the order of the keys.</summary>
        public IEnumerable<T> Values => _keys.Select(k => _values[k]);

        /// <summary>
        ///     Gets a value, creating it when the key is new.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="create">Creates the value.</param>
        /// <returns>The value.</returns>
        public T GetOrAdd(string key, Func<T> create)
        {
            if (!_values.TryGetValue(key, out var value))
            {
                value = create();
                _values[key] = value;
                _keys.Add(key);
            }

            return value;
        }

        /// <summary>
        ///     Gets the value of an existing key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public T Get(string key) => _values[key];

        /// <summary>
        ///     Gets the value of a key when it exists.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">Receives the value.</param>
        /// <returns>True when the key exists.</returns>
        public bool TryGetValue(string key, out T value) => _values.TryGetValue(key, out value!);

        /// <summary>
        ///     Replaces the value of an existing key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The new value.</param>
        public void Set(string key, T value) => _values[key] = value;
    }
}
