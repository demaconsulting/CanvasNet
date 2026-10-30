using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The order of the categories of a discrete scale (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Sort forms.</b> <c>"ascending"</c> and <c>"descending"</c> order the categories by value (a stable sort, null first, the
///         order of <see cref="DataValue.Compare"/>); <c>null</c> keeps the order of the data; an array lists the categories (those it
///         does not list come first). The other forms order the categories by an aggregate of a field over the rows of the <b>Raw</b> table
///         (the rows before the implicit aggregate, as Vega-Lite does): <c>{"op", "field", "order"}</c> with any aggregate
///         operation that yields a comparable value (default operation <c>min</c>, default field the field of the channel);
///         <c>{"encoding": "y", "order"}</c> and the shorthands <c>"y"</c> and <c>"-y"</c> order by another channel: the operation is
///         that channel's aggregate, <c>sum</c> when it is the stacked channel and <c>min</c> otherwise, and the field is the field the channel
///         aggregates (or reads). A category whose result is null (no valid value) sorts first in either direction. A discrete channel
///         that shows the bins of a field is ordered by the start of its bins (ascending, or descending with <c>"descending"</c>).
///         Categories with the same result keep the order of first appearance, so the sort is stable and culture-independent.
///     </para>
///     <para>
///         <b>Limits.</b> The aggregates are computed with the engine of the aggregate transform (<see cref="MeasureCell"/>): one cell per
///         category (at most <see cref="VegaLiteLimits.MaxScaleDomainSize"/>), the work of every row charged to the evaluation-step budget of
///         the call, an operation that keeps values bounded by <see cref="TableLayout.MaxCells"/>, the cancellation token checked as the rows are read.
///         A failure is a <see cref="VegaLiteRenderException"/> located at the channel definition.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>
    ///     Determines whether a <c>sort</c> object is a sort by the aggregate of a field that the resolver can compute.
    /// </summary>
    /// <remarks>
    ///     Supported is an object with an <c>op</c> that can order categories (every aggregate operation except <c>argmin</c>, <c>argmax</c>,
    ///     <c>values</c> and the exponential ones), an optional <c>field</c> (a valid field path; the field of the channel when absent) and an
    ///     optional <c>order</c> (<c>ascending</c> or <c>descending</c>). An object that sorts by another encoding channel (<c>encoding</c>)
    ///     is not one of these; see <see cref="SpecParser.IsSupportedSort"/> for the rule that decides what the feature catalog reports.
    /// </remarks>
    /// <param name="sort">The <c>sort</c> value.</param>
    /// <returns><see langword="true"/> for a supported sort by field.</returns>
    public static bool IsSupportedFieldSort(SpecNode sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        if (!sort.IsObject || sort["encoding"] is not null || sort["op"] is not { IsString: true } op
            || !AggregateMeasures.TryParse(op.StringValue, out var parsed) || !AggregateMeasures.CanOrderCategories(parsed))
        {
            return false;
        }

        var field = sort["field"];
        var fieldOk = field is null || (field.IsString && FieldPath.Canonicalize(field.StringValue) is not null);
        var order = sort["order"];
        var orderOk = order is null || order.IsNull || (order.IsString && order.StringValue is "ascending" or "descending");
        return fieldOk && orderOk;
    }

    /// <summary>
    ///     Orders the categories of a discrete domain as the <c>sort</c> of the channel asks.
    /// </summary>
    /// <param name="domain">The categories in order of first appearance.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The categories in the requested order (ascending without a <c>sort</c>).</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the work exceeds a limit.</exception>
    private static List<DataValue> SortDomain(List<DataValue> domain, CommonInputs input, Context context)
    {
        var sort = input.Definition["sort"];
        if (sort is { IsNull: true })
        {
            return domain;
        }

        if (sort is { IsArray: true } order)
        {
            // Values missing from the sort array come first (index -1), then the array order; the first index of each
            // listed value is recorded once so ordering a domain stays linear in the domain size
            var firstIndex = new Dictionary<DataValue, int>();
            var position = 0;
            foreach (var item in order.Items)
            {
                firstIndex.TryAdd(ToDataValue(item), position++);
            }

            return [.. domain.OrderBy(value => firstIndex.TryGetValue(value, out var index) ? index : -1)];
        }

        var descending = IsDescending(sort);

        // The text of a bin is ordered by where the bin starts
        if (input.Plan is { Bin: BinKind.Binning, BinPlan.RangeName: not null } binned && input.FieldPath is not null)
        {
            return OrderByAggregate(domain, context.Data.Main, input.FieldPath, FieldPath.Escape(binned.BinPlan.StartName), AggregateOp.Min, descending, input, context);
        }

        if (ReadCategorySort(sort, context) is { } by)
        {
            return OrderByAggregate(domain, context.Data.Raw, input.Plan?.RawField ?? input.FieldPath, by.Field ?? input.Plan?.RawField, by.Op, by.Descending, input, context);
        }

        return descending
            ? [.. domain.OrderBy(v => v, Comparer<DataValue>.Create((x, y) => DataValue.Compare(y, x)))]
            : [.. domain.OrderBy(v => v, Comparer<DataValue>.Create(DataValue.Compare))];
    }

    /// <summary>
    ///     Determines whether a <c>sort</c> asks for the descending order of the values themselves.
    /// </summary>
    /// <param name="sort">The <c>sort</c> value, or null.</param>
    /// <returns><see langword="true"/> for <c>"descending"</c> or an object that only has <c>order: "descending"</c>.</returns>
    private static bool IsDescending(SpecNode? sort) =>
        sort?.AsString() == "descending"
        || (sort is { IsObject: true } && sort["op"] is null && sort["field"] is null && sort["encoding"] is null && sort["order"]?.AsString() == "descending");

    /// <summary>
    ///     Reads a <c>sort</c> that orders by an aggregate: an object with an operation and a field, an object with an encoding channel,
    ///     or a channel shorthand.
    /// </summary>
    /// <param name="sort">The <c>sort</c> value, or null.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The operation, the field (null for the field of the channel) and the direction; null when the sort is not a sort by aggregate.</returns>
    private static CategorySort? ReadCategorySort(SpecNode? sort, Context context)
    {
        if (sort is { IsString: true } && SpecParser.TryParseSortShorthand(sort.StringValue, out var channel, out var shorthandDescending))
        {
            return FromChannel(channel, shorthandDescending, context);
        }

        if (sort is not { IsObject: true })
        {
            return null;
        }

        var descending = sort["order"]?.AsString() == "descending";
        if (sort["encoding"] is { IsString: true } encoding)
        {
            return FromChannel(encoding.StringValue!, descending, context);
        }

        var field = sort["field"]?.AsString();
        if (sort["op"] is { IsString: true } opNode)
        {
            return AggregateMeasures.TryParse(opNode.StringValue, out var op) && AggregateMeasures.CanOrderCategories(op)
                ? new CategorySort(op, field, descending)
                : null;
        }

        return field is null ? null : new CategorySort(AggregateOp.Min, field, descending);
    }

    /// <summary>
    ///     Reads a sort by another channel (Vega-Lite's <c>sort: {"encoding": ...}</c>).
    /// </summary>
    /// <param name="channel">The channel that gives the sort values.</param>
    /// <param name="descending">Whether the order is descending.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The sort; null when the channel reads no field or its aggregate cannot order categories.</returns>
    private static CategorySort? FromChannel(string channel, bool descending, Context context)
    {
        var other = context.Plan.Channel(channel);
        if (other is not { HasField: true })
        {
            return null;
        }

        AggregateOp op;
        if (other.Measure is { } measure)
        {
            if (!AggregateMeasures.CanOrderCategories(measure.Op))
            {
                return null;
            }

            op = measure.Op;
        }
        else
        {
            op = other.IsStacked ? AggregateOp.Sum : AggregateOp.Min;
        }

        return new CategorySort(op, op == AggregateOp.Count ? null : other.RawField, descending);
    }

    /// <summary>
    ///     Orders the categories by an aggregate of a field over the rows of a table.
    /// </summary>
    /// <remarks>
    ///     A category whose result is null sorts first in either direction; only the comparison of values reverses. Categories with the
    ///     same result keep the order of first appearance.
    /// </remarks>
    /// <param name="domain">The categories in order of first appearance.</param>
    /// <param name="table">The table that holds the rows (the Raw table for a sort by field, the Main table for a bin).</param>
    /// <param name="keyPath">The field of the table that holds the category.</param>
    /// <param name="valuePath">The field the aggregate reads; null when the operation reads none.</param>
    /// <param name="op">The operation.</param>
    /// <param name="descending">Whether the largest result comes first.</param>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The categories in the requested order; the domain itself when the table lacks the category field.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the work exceeds a limit.</exception>
    private static List<DataValue> OrderByAggregate(
        List<DataValue> domain,
        DataTable table,
        string? keyPath,
        string? valuePath,
        AggregateOp op,
        bool descending,
        CommonInputs input,
        Context context)
    {
        var keyField = keyPath is null ? -1 : table.FindField(keyPath);
        if (keyField < 0)
        {
            return domain;
        }

        if (MeasureCell.StoresValues(op) && table.RowCount > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The sort would keep {table.RowCount} values in memory, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells).",
                input.Location);
        }

        var position = new Dictionary<DataValue, int>();
        for (var i = 0; i < domain.Count; i++)
        {
            position[domain[i]] = i;
        }

        var evaluation = context.Data.Evaluation;
        var environment = new MeasureEnvironment(evaluation, new WorkMeter(evaluation));
        var reader = op == AggregateOp.Count || valuePath is null ? null : new FieldReader(table, valuePath);
        var cells = new MeasureCell[domain.Count];
        var keys = new DataValue[domain.Count];
        try
        {
            for (var row = 0; row < table.RowCount; row++)
            {
                if (row % CancellationInterval == 0)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                }

                environment.Meter.Add(1);
                if (position.TryGetValue(table.GetValue(row, keyField), out var category))
                {
                    cells[category].Add(op, AggregateSpec.DefaultExponentialParameter, reader is null ? default : reader.Read(row, evaluation), row, environment);
                }
            }

            var seed = RowSampler.Seed(input.Definition.Pointer + "/sort");
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = cells[i].Result(op, AggregateSpec.DefaultExponentialParameter, MeasureCell.GroupSeed(seed, i), environment, null);
            }
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, input.Location);
        }

        var comparer = Comparer<DataValue>.Create((x, y) =>
            x.IsNull || y.IsNull || !descending ? DataValue.Compare(x, y) : DataValue.Compare(y, x));
        return [.. Enumerable.Range(0, domain.Count).OrderBy(i => keys[i], comparer).Select(i => domain[i])];
    }

    /// <summary>
    ///     A sort of the categories by an aggregate.
    /// </summary>
    /// <param name="Op">The operation.</param>
    /// <param name="Field">The field the operation reads; null for the field of the channel (or none for a count).</param>
    /// <param name="Descending">Whether the largest result comes first.</param>
    private readonly record struct CategorySort(AggregateOp Op, string? Field, bool Descending);
}
