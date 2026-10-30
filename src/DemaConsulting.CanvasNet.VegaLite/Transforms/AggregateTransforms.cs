// Portions derived from Vega (BSD-3-Clause): the grouping and the aggregate transforms; see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Applies the aggregating transforms of a specification: <c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>,
///     <c>stack</c> and <c>impute</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Joinaggregate.</b> The rows are grouped like those of <c>aggregate</c>, but every input row is kept (in its place) and
///         receives the measures of its group as new fields; a <c>values</c> measure hands the same array to every row of the group.
///     </para>
///     <para>
///         <b>Window.</b> The rows are partitioned by <c>groupby</c> (structural keys, first-seen order) and each partition is sorted
///         by <c>sort</c> with a stable merge sort (null first, NaN next, then by value; ties keep the input order). Every row then gets
///         the ranking, offset and positional operations of its position, or the aggregate operations over its <c>frame</c>: the frame
///         is two row offsets (null is unbounded; default <c>[null, 0]</c>) that are clamped to the partition, and with a <c>sort</c>
///         and no <c>ignorePeers</c> the frame is widened to take in the peers (rows with the same sort values) at both ends. The
///         output keeps the input row order. A frame that starts at the first row (<c>[null, n]</c>) is computed incrementally, in
///         <c>O(n)</c> for the whole partition; any other frame is recomputed per row and its size is charged to the step budget.
///         <c>cume_dist</c> and <c>ntile</c> count the peers of the first rows of a partition correctly (Vega under-reports them for
///         the first row when it has peers). An empty frame gives null for the positional operations.
///     </para>
///     <para>
///         <b>Stack.</b> Within each group (the rows with the same <c>groupby</c> values) the rows are put in the order of <c>sort</c>
///         (input order without one) and stacked as Vega's <c>stack</c> does: <c>zero</c> adds the values as given from 0, the negative
///         ones downwards from 0 separately from the positive ones (null counts as 0); <c>center</c> and <c>normalize</c> stack the
///         absolute values, <c>center</c> centered on the widest group and <c>normalize</c> scaled so every group sums to 1. The two
///         output fields (<c>as</c>; default <c>field_start</c> and <c>field_end</c>) hold the lower and the upper end; a NaN or a
///         zero sum under <c>normalize</c> follows IEEE arithmetic exactly as in Vega (NaN ends for the rows it affects).
///     </para>
///     <para>
///         <b>Impute.</b> For every group (<c>groupby</c> values) and every key of the key domain (<c>keyvals</c> first, then the keys
///         observed in first-seen order) that the group lacks, a row is appended after the input rows: the group fields, the key and the
///         imputed field, every other field null. The imputed field is the <c>value</c> (default 0) or, for <c>mean</c>, <c>median</c>,
///         <c>min</c> and <c>max</c>, that operation over the <c>frame</c> (default the whole group) in data order, as Vega-Lite does
///         with a window transform; a null in an existing row is replaced as well. The size of the result is bounded before it is
///         allocated.
///     </para>
///     <para>
///         <b>Aggregate.</b> The rows are grouped by the <c>groupby</c> fields (none: one group) and each group becomes one output row with the
///         group fields followed by one field per measure. The groups are keyed by the <i>structure</i> of the values (a number and a text
///         with the same spelling are different groups, <c>NaN</c> equals <c>NaN</c>, zero equals negative zero, an array or object is
///         compared by its content) and appear in the order in which they are first seen. An input without rows has no groups, so
///         the output has no rows. The measures are those of <see cref="MeasureCell"/>; the output name of a measure is its <c>as</c>
///         or <c>op_field</c> (<c>count</c> for a count of rows); a later field of the same name replaces an earlier one.
///     </para>
///     <para>
///         <b>Bin.</b> The <c>bin</c> transform chooses the bins of a field with <see cref="BinAlgorithm"/> from the extent of its
///         finite numeric values (or the <c>extent</c> parameter) and writes the lower and the upper boundary of the bin of every row
///         to the two fields of <c>as</c> (a single name gives <c>name</c> and <c>name_end</c>; none gives <c>bin0</c> and <c>bin1</c>);
///         a row whose value is null or not a finite number gets null in both.
///     </para>
///     <para>
///         <b>Limits, charging and cancellation.</b> The number of groups is counted in the row budget of the loader as groups appear
///         (the input counts until the output is complete, as for fold), so a hostile group-by fails early and located at the
///         transform; the per-group states count as <see cref="MeasureCell.StateCells"/> table cells each and, with the output, are
///         checked against <see cref="TableLayout.MaxCells"/> as each group appears; the memory of the operations that keep values
///         (<c>distinct</c>, <c>values</c>, the quantiles, the confidence interval) is bounded up front by rows times such operations
///         against the same cell limit. The work of every row, of every sort (n·⌈log₂ n⌉), of the bootstrap (1,000·n) and of every
///         text conversion is charged to the evaluation-step budget (<see cref="WorkMeter"/>), the strings the transform builds are
///         bounded by <see cref="VegaLiteLimits.MaxStringLength"/>, and the cancellation token is checked every 1,024 rows. A
///         failure is a <see cref="VegaLiteRenderException"/> located at the transform whose message names the limit. The result is a
///         pure function of the input, the specification and its JSON pointer (the seed of the bootstrap): no clock, no random source and
///         no culture-dependent text. An instance serves one call on one thread.
///     </para>
///     <para>
///         <b>Limits of the grouped transforms.</b> <c>joinaggregate</c>, <c>window</c> and <c>stack</c> keep every row, so the row budget is
///         unchanged and the new fields are bounded by <see cref="TableLayout"/> (the field and the cell limits); their groups cost a few
///         table cells each, checked as they appear. The sort keys are bounded by rows times sort fields against the cell limit, a sort is
///         a stable merge sort charged n·⌈log₂ n⌉ that polls the cancellation token, a window frame that is not cumulative is recomputed
///         for each row and charged one step for every 16 rows of the frame, and the arrays that a window <c>values</c> operation builds
///         are bounded by <see cref="VegaLiteLimits.MaxDataRows"/> (rows that share a frame share one array). <c>impute</c> bounds the
///         result before it allocates it: the number of <c>keyvals</c> and the product of groups and keys are counted in the row budget
///         (the product can only be smaller than the result), the presence of every combination takes one bit, and the output cells
///         are checked against <see cref="TableLayout.MaxCells"/> before they are allocated.
///     </para>
/// </remarks>
internal sealed partial class AggregateTransforms
{
    /// <summary>What the run shares.</summary>
    private readonly TransformContext _context;

    /// <summary>Charges the work of the transform to the step budget.</summary>
    private readonly WorkMeter _meter;

    /// <summary>The environment the measures work in.</summary>
    private readonly MeasureEnvironment _environment;

    /// <summary>
    ///     Initializes a run.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    internal AggregateTransforms(TransformContext context)
    {
        _context = context;
        _meter = new WorkMeter(context.Evaluator.Context);
        _environment = new MeasureEnvironment(context.Evaluator.Context, _meter);
    }

    /// <summary>
    ///     Applies one aggregating transform to a table.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The member that names the transform (<c>aggregate</c>, <c>bin</c>, ...).</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The transformed table; <paramref name="input"/> itself for a name that is not one of the six transforms.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the transform, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static DataTable Apply(DataTable input, SpecNode entry, SpecNode operation, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        var run = new AggregateTransforms(context);
        return operation.Name switch
        {
            "aggregate" => run.Aggregate(input, entry, operation),
            "joinaggregate" => run.JoinAggregate(input, entry, operation),
            "window" => run.Window(input, entry, operation),
            "bin" => run.Bin(input, entry, operation),
            "stack" => run.Stack(input, entry, operation),
            "impute" => run.Impute(input, entry, operation),
            _ => input,
        };
    }

    /// <summary>
    ///     Reads the <c>groupby</c> field list of a transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <returns>The field paths; empty when there is no <c>groupby</c>.</returns>
    private static string[] ReadGroupBy(SpecNode entry) =>
        entry["groupby"] is { IsArray: true } list ? [.. list.Items.Select(i => i.StringValue!)] : [];

    /// <summary>
    ///     Reads a <c>sort</c> array of <c>{field, order}</c> objects.
    /// </summary>
    /// <param name="sort">The array (validated by the parser), or null.</param>
    /// <returns>The sort fields in order of significance; empty for no sort.</returns>
    internal static List<SortField> ReadSortFields(SpecNode? sort)
    {
        var fields = new List<SortField>();
        if (sort is { IsArray: true })
        {
            foreach (var item in sort.Items)
            {
                fields.Add(new SortField(item["field"]!.StringValue!, string.Equals(item["order"]?.AsString(), "descending", StringComparison.Ordinal)));
            }
        }

        return fields;
    }

    /// <summary>
    ///     Checks the texts a transform takes from the specification against the string limit of the call.
    /// </summary>
    /// <param name="what">What the texts are, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <param name="texts">The texts (nulls are skipped).</param>
    private void CheckTexts(string what, VegaLiteLocation location, params IEnumerable<string?>[] texts)
    {
        foreach (var text in texts.SelectMany(t => t))
        {
            _context.CheckText(DataValue.FromString(text), what, location);
        }
    }

    /// <summary>
    ///     Reads the values of the sort fields of every row.
    /// </summary>
    /// <param name="input">The table.</param>
    /// <param name="sort">The sort fields (at least one).</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <returns>The values, row by row (<c>row * sort.Count + field</c>).</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the values would exceed the cell limit.</exception>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    private DataValue[] ReadSortKeys(DataTable input, IReadOnlyList<SortField> sort, VegaLiteLocation location)
    {
        if ((long)input.RowCount * sort.Count > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The sort would read {(long)input.RowCount * sort.Count} values, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells).",
                location);
        }

        var readers = sort.Select(s => new FieldReader(input, s.Field)).ToArray();
        var keys = new DataValue[input.RowCount * sort.Count];
        var evaluation = _context.Evaluator.Context;
        for (var row = 0; row < input.RowCount; row++)
        {
            _context.Tick(row);
            _meter.Add(readers.Length);
            for (var f = 0; f < readers.Length; f++)
            {
                keys[(row * readers.Length) + f] = readers[f].Read(row, evaluation);
            }
        }

        return keys;
    }

    /// <summary>
    ///     Builds the comparison of two rows by their sort values.
    /// </summary>
    /// <param name="keys">The values of <see cref="ReadSortKeys"/>.</param>
    /// <param name="sort">The sort fields.</param>
    /// <returns>A comparison that is zero for rows with equal sort values (peers); the sort stays stable.</returns>
    private static Func<int, int, int> RowComparison(DataValue[] keys, IReadOnlyList<SortField> sort)
    {
        var width = sort.Count;
        var descending = sort.Select(s => s.Descending).ToArray();
        return (a, b) =>
        {
            for (var f = 0; f < width; f++)
            {
                var c = DataValue.Compare(keys[(a * width) + f], keys[(b * width) + f]);
                if (c != 0)
                {
                    return descending[f] ? -c : c;
                }
            }

            return 0;
        };
    }

    /// <summary>
    ///     Sorts a range of row numbers stably, charging the comparisons to the step budget.
    /// </summary>
    /// <param name="rows">The array of row numbers.</param>
    /// <param name="start">The first position of the range.</param>
    /// <param name="count">The size of the range.</param>
    /// <param name="buffer">Scratch space of at least <paramref name="count"/> items.</param>
    /// <param name="compare">The comparison of two rows.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    private void SortRows(int[] rows, int start, int count, int[] buffer, Func<int, int, int> compare)
    {
        if (count < 2)
        {
            return;
        }

        _meter.Add((long)count * (System.Numerics.BitOperations.Log2((uint)(count - 1)) + 1));
        StableSorter.Sort(rows, start, count, buffer, compare, _context.CancellationToken);
    }

    /// <summary>
    ///     Gets the location of a node of the specification.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location.</returns>
    private VegaLiteLocation Locate(SpecNode node) => _context.Tree.Locate(node);

    /// <summary>
    ///     Creates the located failure for an evaluation that exceeded its budget or a string limit inside a transform.
    /// </summary>
    /// <param name="exception">The failure of the evaluation; its message names the limit.</param>
    /// <param name="location">The location of the transform.</param>
    /// <returns>The exception to throw.</returns>
    private static VegaLiteRenderException Located(ExpressionRuntimeException exception, VegaLiteLocation location) =>
        new(exception.Message, location);

    /// <summary>
    ///     Builds the key that identifies the group of a row.
    /// </summary>
    /// <param name="readers">The readers of the group-by fields.</param>
    /// <param name="row">The row.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when a composite value is too long or the step budget is exhausted.</exception>
    private GroupKey BuildKey(FieldReader[] readers, int row)
    {
        var values = new DataValue[readers.Length];
        DataValue[]? identity = null;
        char[]? tags = null;
        var context = _context.Evaluator.Context;
        for (var i = 0; i < readers.Length; i++)
        {
            var value = readers[i].Read(row, context);
            values[i] = value;
            if (value.IsArray || value.IsObject)
            {
                // An array or object read from a table is rebuilt for every row, so it is compared by content, not by identity
                identity ??= [.. values];
                tags ??= new string('v', readers.Length).ToCharArray();
                identity[i] = DataValue.FromString(CanonicalText(value));
                tags[i] = 'c';
            }
            else if (identity is not null)
            {
                identity[i] = value;
            }
        }

        return new GroupKey(values, identity ?? values, tags is null ? null : new string(tags));
    }

    /// <summary>
    ///     Spells an array or object as a text that is equal exactly when the values have the same structure.
    /// </summary>
    /// <remarks>
    ///     The walk is iterative (the depth of a value cannot exhaust the stack), every element is prefixed by its kind and every text
    ///     and collection by its length (so the spelling is unambiguous), the members of an object are in ordinal order of their names,
    ///     and the text is bounded by the string limit and charged to the step budget.
    /// </remarks>
    /// <param name="value">The array or object.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text would exceed <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
    private string CanonicalText(DataValue value)
    {
        var maxLength = _context.Limits.MaxStringLength;
        var text = new StringBuilder();
        var pending = new Stack<DataValue>();
        pending.Push(value);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            switch (current.Kind)
            {
                case DataValueKind.Array:
                    var elements = current.Elements;
                    text.Append('[').Append(elements.Length).Append(':');
                    for (var i = elements.Length - 1; i >= 0; i--)
                    {
                        pending.Push(elements[i]);
                    }

                    break;
                case DataValueKind.Object:
                    var members = current.ObjectValue!;
                    var order = Enumerable.Range(0, members.Count).OrderBy(i => members.Keys[i], StringComparer.Ordinal).ToArray();
                    text.Append('{').Append(members.Count).Append(':');
                    for (var i = order.Length - 1; i >= 0; i--)
                    {
                        pending.Push(members.ValueAt(order[i]));
                        pending.Push(DataValue.FromString(members.Keys[order[i]]));
                    }

                    break;
                case DataValueKind.String:
                    text.Append('s').Append(current.Text!.Length).Append(':').Append(current.Text);
                    break;
                case DataValueKind.Number:
                    text.Append('d').Append(JsNumber.ToString(current.Number)).Append(';');
                    break;
                case DataValueKind.Date:
                    text.Append('t').Append(JsNumber.ToString(current.Milliseconds)).Append(';');
                    break;
                case DataValueKind.Boolean:
                    text.Append(current.Boolean ? "b1" : "b0");
                    break;
                default:
                    text.Append(current.IsNull ? "n" : "x");
                    break;
            }

            if (text.Length > maxLength)
            {
                throw new ExpressionRuntimeException(
                    $"The group value is longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).");
            }
        }

        _meter.Add(text.Length);
        return text.ToString();
    }

    /// <summary>
    ///     The values of the group fields of a row as a key that compares by structure.
    /// </summary>
    internal sealed class GroupKey : IEquatable<GroupKey>
    {
        /// <summary>The values that decide equality: the values themselves, with a composite replaced by its structural text.</summary>
        private readonly DataValue[] _identity;

        /// <summary>For each field <c>c</c> when its value is composite and <c>v</c> otherwise; null when no field is composite.</summary>
        private readonly string? _tags;

        /// <summary>The hash code, computed once.</summary>
        private readonly int _hash;

        /// <summary>
        ///     Initializes a key.
        /// </summary>
        /// <param name="values">The values of the row, which the output keeps; ownership passes to the key.</param>
        /// <param name="identity">The values that decide equality (the same array as <paramref name="values"/> when none is composite).</param>
        /// <param name="tags">Which values are composite, or null.</param>
        public GroupKey(DataValue[] values, DataValue[] identity, string? tags)
        {
            Values = values;
            _identity = identity;
            _tags = tags;
            var hash = new HashCode();
            foreach (var value in identity)
            {
                hash.Add(value);
            }

            hash.Add(tags, StringComparer.Ordinal);
            _hash = hash.ToHashCode();
        }

        /// <summary>Gets the values of the group fields.</summary>
        public DataValue[] Values { get; }

        /// <inheritdoc/>
        public bool Equals(GroupKey? other) =>
            other is not null && string.Equals(_tags, other._tags, StringComparison.Ordinal) && _identity.AsSpan().SequenceEqual(other._identity);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as GroupKey);

        /// <inheritdoc/>
        public override int GetHashCode() => _hash;
    }
}
