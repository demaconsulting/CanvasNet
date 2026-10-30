using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Applies the aggregating transforms of a specification: <c>aggregate</c> and <c>bin</c> (the others, <c>joinaggregate</c>,
///     <c>window</c>, <c>stack</c> and <c>impute</c>, are dispatched here too and pass the table through until they are implemented).
/// </summary>
/// <remarks>
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
    /// <returns>The transformed table; <paramref name="input"/> itself for a transform that is not implemented yet.</returns>
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
            "bin" => run.Bin(input, entry, operation),

            // joinaggregate, window, stack and impute are accepted and validated; they compute in a later work package
            _ => input,
        };
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
