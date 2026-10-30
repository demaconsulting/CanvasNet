// Portions derived from Vega-Lite (BSD-3-Clause): the order of the implicit transforms; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The tables of the implicit pipeline of a view and what the bins of its encoding turned out to be.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Raw"/> is the table after the explicit transforms and the implicit bins, before the implicit aggregate; a scale
///         that sorts its categories by an aggregate of a field, or whose domain is <c>unaggregated</c>, reads it. <see cref="Main"/> is
///         the table after the aggregate and the stack; the marks and every other scale read it. The two are the same instance when
///         the encoding neither aggregates nor stacks, or when no scale needs the Raw table.
///     </para>
///     <para>
///         The class is immutable (the tables are).
///     </para>
/// </remarks>
internal sealed class EncodingData
{
    /// <summary>
    ///     Initializes the data.
    /// </summary>
    /// <param name="raw">The table before the implicit aggregate.</param>
    /// <param name="main">The table the marks draw.</param>
    /// <param name="bins">The bins of the encoding by the literal name of their start field.</param>
    /// <param name="evaluation">The evaluation (step budget and cancellation) of the call, which the scales charge their work to.</param>
    public EncodingData(DataTable raw, DataTable main, IReadOnlyDictionary<string, BinSpec> bins, ExpressionFunctionContext evaluation)
    {
        Raw = raw;
        Main = main;
        Bins = bins;
        Evaluation = evaluation;
    }

    /// <summary>Gets the table before the implicit aggregate.</summary>
    public DataTable Raw { get; }

    /// <summary>Gets the table the marks draw.</summary>
    public DataTable Main { get; }

    /// <summary>Gets the bins of the encoding by the literal name of their start field (a field without a valid value has none).</summary>
    public IReadOnlyDictionary<string, BinSpec> Bins { get; }

    /// <summary>Gets the evaluation of the call.</summary>
    public ExpressionFunctionContext Evaluation { get; }

    /// <summary>
    ///     Creates the data of an encoding that asks for no implicit transform.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="evaluation">The evaluation of the call.</param>
    /// <returns>The data, whose Raw and Main tables are <paramref name="table"/>.</returns>
    public static EncodingData Identity(DataTable table, ExpressionFunctionContext evaluation) =>
        new(table, table, new Dictionary<string, BinSpec>(StringComparer.Ordinal), evaluation);
}

/// <summary>
///     Runs the implicit transforms of an <see cref="EncodingPlan"/> on the rows of a view: the bins of the encoding, then the
///     aggregate, then the stack (Vega-Lite's order: explicit transforms, bin, Raw, aggregate, stack, Main).
/// </summary>
/// <remarks>
///     <para>
///         <b>Bin.</b> Every distinct bin (field and parameters) is computed once, from the extent of the finite values of the
///         field (or the <c>extent</c> parameter) by <see cref="BinAlgorithm"/>, and written to the start and end fields of the plan
///         (and, for a discrete channel, to the text of the bin: the two boundaries formatted and joined with an en dash, <c>null</c>
///         for a row without a valid value). <b>Aggregate.</b> One pass of the engine of <see cref="AggregateTransforms"/> with the
///         plan's dimensions and measures; the input rows stay in use as the Raw table when a scale needs them. The row an
///         <c>argmax</c> or <c>argmin</c> measure returns is read into a scalar field for the channel. <b>Stack.</b> The engine of
///         <see cref="AggregateTransforms"/> with the plan's stacks and layer order. The invalid-row filter of Vega-Lite's last stage is
///         done by the marks, which skip a row whose position is not a number.
///     </para>
///     <para>
///         <b>Limits.</b> The groups of the aggregate are counted in the row budget as they appear (the Raw table stays counted while it is in
///         use, and is released when no scale needs it); a stack that keeps the Raw table next to it counts its rows too; the bin count
///         is bounded by <see cref="VegaLiteLimits.MaxScaleDomainSize"/>; every derived table is allocated through <see cref="TableLayout"/>
///         (field and cell limits); the work of every row is charged to the evaluation-step budget and the cancellation token is checked
///         every 1,024 rows. A failure is a <see cref="VegaLiteRenderException"/> located at the channel definition (or the <c>bin</c> value)
///         that asked for it. Everything is deterministic: first-seen group order, stable sorts, invariant culture.
///     </para>
/// </remarks>
internal sealed class EncodingTransforms
{
    /// <summary>What the run shares.</summary>
    private readonly TransformContext _context;

    /// <summary>Charges the work of the run to the step budget.</summary>
    private readonly WorkMeter _meter;

    /// <summary>The engine of the aggregating transforms.</summary>
    private readonly AggregateTransforms _engine;

    /// <summary>
    ///     Initializes a run.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    private EncodingTransforms(TransformContext context)
    {
        _context = context;
        _meter = new WorkMeter(context.Evaluator.Context);
        _engine = new AggregateTransforms(context);
    }

    /// <summary>
    ///     Runs the implicit transforms of a plan on a table.
    /// </summary>
    /// <param name="plan">The plan of the view.</param>
    /// <param name="table">The rows after the explicit transforms.</param>
    /// <param name="context">The context of the call (row budget, evaluator, limits, token).</param>
    /// <returns>The Raw and Main tables and the bins.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the channel, when a limit is exceeded or the values cannot be binned.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static EncodingData Apply(EncodingPlan plan, DataTable table, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(context);
        if (plan.IsIdentity)
        {
            return EncodingData.Identity(table, context.Evaluator.Context);
        }

        return new EncodingTransforms(context).Execute(plan, table);
    }

    /// <summary>
    ///     Runs the implicit transforms with a context of its own, for callers that have only a view and a table (the scale resolver's convenience entry).
    /// </summary>
    /// <param name="plan">The plan of the view.</param>
    /// <param name="table">The rows.</param>
    /// <param name="view">The view object (the root of the tree that locates failures).</param>
    /// <param name="sourceName">The source name used in locations, or null.</param>
    /// <param name="limits">The limits; the defaults when null.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The data.</returns>
    public static EncodingData ApplyStandalone(
        EncodingPlan plan,
        DataTable table,
        SpecNode view,
        string? sourceName,
        VegaLiteLimits? limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(view);
        var effective = limits ?? VegaLiteLimits.Default;
        var parameters = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, cancellationToken, parameters);
        var loader = new DataLoader(sourceName, effective, cancellationToken: cancellationToken);
        var context = new TransformContext(new SpecTree(view, sourceName, []), loader, evaluator, parameters, effective, cancellationToken, []);
        return Apply(plan, table, context);
    }

    /// <summary>
    ///     Runs the stages.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="table">The input rows.</param>
    /// <returns>The data.</returns>
    private EncodingData Execute(EncodingPlan plan, DataTable table)
    {
        var bins = new Dictionary<string, BinSpec>(StringComparer.Ordinal);
        var raw = plan.Bins.Count > 0 ? BinFields(plan, table, bins) : table;
        var main = raw;
        if (plan.Aggregate is { } aggregate)
        {
            var definition = plan.Channels.First(c => c.Measure is not null).Definition;
            var location = _context.Locate(definition);
            main = _engine.Aggregate(raw, aggregate.GroupBy, aggregate.Measures, location, RowSampler.Seed(definition.Pointer), releaseInput: false);
            if (aggregate.ArgFields.Count > 0)
            {
                main = ReadArgFields(main, aggregate.ArgFields, location);
            }

            if (!plan.NeedsRaw)
            {
                _context.Loader.ReleaseRows(raw.RowCount);
                raw = main;
            }
        }

        if (plan.Stack is { } stack)
        {
            var location = _context.Locate(stack.Definition);
            if (plan.NeedsRaw)
            {
                // The stacked copy lives next to the rows a scale still reads, whether or not an aggregate came first
                _context.Loader.ReserveDerivedRows(main.RowCount, "The stacked rows", location);
            }

            var stacked = _engine.Stack(main, stack.Field, stack.GroupBy, stack.Sort, stack.Offset, stack.StartName, stack.EndName, location);
            if (plan.NeedsRaw && plan.Aggregate is not null)
            {
                // The aggregated rows are replaced by the stacked copy (the unaggregated rows stay), so their count is given back
                _context.Loader.ReleaseRows(main.RowCount);
            }

            main = stacked;
            if (!plan.NeedsRaw)
            {
                raw = main;
            }
        }

        return new EncodingData(raw, main, bins, _context.Evaluator.Context);
    }

    /// <summary>
    ///     Computes the bins and writes them to the table.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="input">The input table.</param>
    /// <param name="specs">Receives the bins by start name.</param>
    /// <returns>The table with the bin fields added; the rows keep their order.</returns>
    private DataTable BinFields(EncodingPlan plan, DataTable input, Dictionary<string, BinSpec> specs)
    {
        var location = _context.Locate(plan.Bins[0].Node);
        try
        {
            var evaluation = _context.Evaluator.Context;
            var readers = plan.Bins.Select(b => new FieldReader(input, b.SourceField)).ToArray();
            var resolved = new BinSpec?[plan.Bins.Count];
            for (var b = 0; b < plan.Bins.Count; b++)
            {
                resolved[b] = ResolveBin(plan.Bins[b], readers[b], input.RowCount, evaluation);
                if (resolved[b] is { } found)
                {
                    specs[plan.Bins[b].StartName] = found;
                }
            }

            var names = new List<string>();
            foreach (var bin in plan.Bins)
            {
                names.Add(TableLayout.Canonical(bin.StartName));
                names.Add(TableLayout.Canonical(bin.EndName));
                if (bin.RangeName is { } range)
                {
                    names.Add(TableLayout.Canonical(range));
                }
            }

            var layout = new TableLayout(input, names, keepInput: true, _context.Limits, location);
            var cells = layout.Allocate(input.RowCount);
            var width = layout.Fields.Length;
            var formats = plan.Bins.Select(b => b.RangeName is null ? null : CreateFormat(b.Format)).ToArray();
            var labels = plan.Bins.Select(b => b.RangeName is null ? null : new Dictionary<double, string>()).ToArray();
            for (var row = 0; row < input.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(plan.Bins.Count);
                layout.CopyRow(input, row, cells, row);
                var offset = (long)row * width;
                var slot = 0;
                for (var b = 0; b < plan.Bins.Count; b++)
                {
                    var hasRange = plan.Bins[b].RangeName is not null;
                    var number = 0.0;
                    if (resolved[b] is { } spec && readers[b].Read(row, evaluation).TryGetQuantitative(out number))
                    {
                        var start = BinAlgorithm.Assign(number, spec);
                        var end = BinAlgorithm.End(start, spec);
                        cells[offset + layout.Added[slot]] = DataValue.FromNumber(start);
                        cells[offset + layout.Added[slot + 1]] = DataValue.FromNumber(end);
                        if (hasRange)
                        {
                            cells[offset + layout.Added[slot + 2]] = DataValue.FromString(Label(labels[b]!, formats[b]!, start, end));
                        }
                    }

                    // A value that is not a finite number keeps the default (null) cells: it has no bin, so its category stays null rather than the text "null"
                    slot += hasRange ? 3 : 2;
                }
            }

            return layout.Build(cells, input.RowCount);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }
    }

    /// <summary>
    ///     Chooses the bins of one field from the extent of its finite values (or the extent the specification gives).
    /// </summary>
    /// <param name="bin">The bin.</param>
    /// <param name="reader">The reader of the field.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="evaluation">The evaluation to charge.</param>
    /// <returns>The bins; null when the field has no finite value.</returns>
    private BinSpec? ResolveBin(BinPlan bin, FieldReader reader, int rows, ExpressionFunctionContext evaluation)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        if (bin.Settings.Extent is { } given)
        {
            (min, max) = given;
        }
        else
        {
            for (var row = 0; row < rows; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                if (reader.Read(row, evaluation).TryGetQuantitative(out var number))
                {
                    min = Math.Min(min, number);
                    max = Math.Max(max, number);
                }
            }
        }

        return min <= max
            ? BinAlgorithm.Resolve(min, max, bin.Settings, _context.Limits.MaxScaleDomainSize, _context.Locate(bin.Node))
            : null;
    }

    /// <summary>
    ///     Creates the formatter of the boundaries in the text of a bin.
    /// </summary>
    /// <param name="specifier">The specifier text; the default (empty) specifier when it is not valid.</param>
    /// <returns>The formatter.</returns>
    private static NumberFormat CreateFormat(string specifier) =>
        NumberFormat.TryParseSpecifier(specifier, out var parsed) ? NumberFormat.Create(parsed) : NumberFormat.Create(string.Empty);

    /// <summary>
    ///     Gets the text of a bin: its two boundaries formatted and joined with an en dash (Vega-Lite's <c>bin_range</c>).
    /// </summary>
    /// <param name="cache">The texts made so far by the start of the bin.</param>
    /// <param name="format">The formatter.</param>
    /// <param name="start">The lower boundary.</param>
    /// <param name="end">The upper boundary.</param>
    /// <returns>The text.</returns>
    private string Label(Dictionary<double, string> cache, NumberFormat format, double start, double end)
    {
        if (!cache.TryGetValue(start, out var label))
        {
            label = format.Format(start) + " \u2013 " + format.Format(end);
            _meter.Add(label.Length);
            cache[start] = label;
        }

        return label;
    }

    /// <summary>
    ///     Reads the fields of the rows that <c>argmax</c> and <c>argmin</c> measures returned into scalar fields.
    /// </summary>
    /// <param name="table">The aggregated table.</param>
    /// <param name="fields">The fields to read.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <returns>The table with the scalar fields added.</returns>
    private DataTable ReadArgFields(DataTable table, IReadOnlyList<ArgField> fields, VegaLiteLocation location)
    {
        try
        {
            var layout = new TableLayout(table, [.. fields.Select(f => f.Output)], keepInput: true, _context.Limits, location);
            var cells = layout.Allocate(table.RowCount);
            var width = layout.Fields.Length;
            var holders = fields.Select(f => table.FindField(FieldPath.Escape(f.Measure))).ToArray();
            var paths = fields.Select(f => FieldPath.Parse(f.Source) ?? []).ToArray();
            for (var row = 0; row < table.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(fields.Count);
                layout.CopyRow(table, row, cells, row);
                for (var f = 0; f < fields.Count; f++)
                {
                    cells[((long)row * width) + layout.Added[f]] = holders[f] < 0 ? default : Walk(table.GetValue(row, holders[f]), paths[f]);
                }
            }

            return layout.Build(cells, table.RowCount);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }
    }

    /// <summary>
    ///     Follows a path of property names through nested objects.
    /// </summary>
    /// <param name="value">The value to start from.</param>
    /// <param name="segments">The property names.</param>
    /// <returns>The value at the path; null when a step is missing.</returns>
    private static DataValue Walk(DataValue value, IReadOnlyList<string> segments)
    {
        var current = value;
        foreach (var segment in segments)
        {
            if (current.ObjectValue is not { } members || !members.TryGetValue(segment, out current))
            {
                return default;
            }
        }

        return current;
    }
}
