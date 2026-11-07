// Portions derived from Vega (BSD-3-Clause): the regression, loess, KDE, quantile and extent transforms; see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The statistical transforms of Vega-Lite: <c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c> and <c>extent</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Semantics.</b> The transforms follow Vega's operators (vega-regression, vega-transforms <c>KDE</c>, <c>Quantile</c> and
///         <c>Extent</c>) with Vega-Lite's defaults, and are ported with their JavaScript details. A transform that generates rows
///         (<c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c>) <i>replaces</i> the table: its output has the
///         group fields (the values of the first row of each group) and the two output fields, or, for a regression with
///         <c>params: true</c>, one row per group with <c>keys</c>, <c>coef</c> and <c>rSquared</c>. The groups are those of
///         Vega's <c>partition</c>: the key of a row is JavaScript's text of the array of its group values
///         (<c>String([1, "a"])</c> is <c>1,a</c>), so the number 1 and the text "1" are one group, null, undefined and a missing
///         field are one group, and the groups are in order of first appearance; a transform without <c>groupby</c> has one
///         group, which may be empty. <c>extent</c> does not change the table: it computes <c>[min, max]</c> of a field and
///         stores it in the parameter that <c>param</c> names, which the transforms that follow it, <c>bin.extent</c> and
///         <c>scale.domain</c> read.
///     </para>
///     <para>
///         <b>Vega-Lite defaults.</b> The output fields of a regression and a loess are the <c>on</c> and the fitted field
///         (<c>as</c> may give one or both), of a density <c>value</c> and <c>density</c> with the resolution <c>shared</c>
///         (the groups share the domain and the grid of sample points, so the areas can be stacked), and of a quantile
///         <c>prob</c> and <c>value</c>.
///     </para>
///     <para>
///         <b>Limits and cancellation.</b> The rows of the output are counted in the row budget of the loader before they exist
///         (an upper bound of the samples of a curve before it is sampled), the input is released when the output is complete,
///         the output respects <see cref="VegaLiteLimits.MaxFieldCount"/> and <see cref="TableLayout.MaxCells"/>, the work is
///         charged to the step budget (a fit by the points and the coefficients, a loess by the points and the window, a
///         density by the rows for every sample point, a quantile by the sort), and the cancellation token is checked every
///         1,024 rows and during the long loops. Failures are <see cref="VegaLiteRenderException"/>s located at the transform.
///         Recoverable findings (a regression skipped for too few points, a log regression whose extent is not positive, an
///         extent without a finite value) are the warning <c>vl-transform-runtime</c>. An instance serves one call on one
///         thread.
///     </para>
/// </remarks>
internal sealed partial class StatTransforms
{
    /// <summary>The id of the warning that every recoverable finding of the transforms uses.</summary>
    private const string RuntimeWarning = "vl-transform-runtime";

    /// <summary>What the run shares.</summary>
    private readonly TransformContext _context;

    /// <summary>Charges the work of the transform to the step budget.</summary>
    private readonly WorkMeter _meter;

    /// <summary>
    ///     Initializes a run.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    private StatTransforms(TransformContext context)
    {
        _context = context;
        _meter = new WorkMeter(context.Evaluator.Context);
    }

    /// <summary>
    ///     Applies one statistical transform to a table.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The member that names the transform (<c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c> or <c>extent</c>).</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The generated table; <paramref name="input"/> itself for <c>extent</c> (and for a name that is not one of the five).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the transform, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static DataTable Apply(DataTable input, SpecNode entry, SpecNode operation, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        var run = new StatTransforms(context);
        try
        {
            return operation.Name switch
            {
                "regression" => run.Regression(input, entry, operation),
                "loess" => run.Loess(input, entry, operation),
                "density" => run.Density(input, entry, operation),
                "quantile" => run.Quantile(input, entry, operation),
                "extent" => run.Extent(input, entry, operation),
                _ => input,
            };
        }
        catch (ExpressionRuntimeException ex)
        {
            // The step budget or the string limit of the evaluation ran out inside the transform
            throw new VegaLiteRenderException(ex.Message, context.Locate(entry));
        }
    }

    /// <summary>
    ///     One group of rows: the rows that share the text of their group values, and the values of the first of them.
    /// </summary>
    /// <param name="Rows">The row numbers, in order.</param>
    /// <param name="Dimensions">The values of the group fields in the first row of the group; null for the single group of a transform without <c>groupby</c>.</param>
    private sealed record StatGroup(List<int> Rows, DataValue[]? Dimensions);

    /// <summary>
    ///     Gets the location of a node of the specification.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location.</returns>
    private VegaLiteLocation Locate(SpecNode node) => _context.Tree.Locate(node);

    /// <summary>
    ///     Records a recoverable finding of the transform.
    /// </summary>
    /// <param name="message">The description.</param>
    /// <param name="location">The location of the transform.</param>
    private void Warn(string message, VegaLiteLocation location) =>
        _context.Warnings.Add(new FeatureReport(RuntimeWarning, message, location, FeatureReportKind.Warning));

    /// <summary>
    ///     Reads the <c>groupby</c> field list of a transform.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <returns>The field paths; null when the entry has no <c>groupby</c> (Vega then has one group, which may be empty).</returns>
    private static string[]? ReadGroupBy(SpecNode entry) =>
        entry["groupby"] is { IsArray: true } list ? [.. list.Items.Select(i => i.StringValue!)] : null;

    /// <summary>
    ///     Reads the <c>as</c> names of a transform and fills the missing ones with the defaults.
    /// </summary>
    /// <param name="entry">The transform object.</param>
    /// <param name="first">The default of the first name.</param>
    /// <param name="second">The default of the second name.</param>
    /// <returns>The two names, as written (literal names).</returns>
    private static (string First, string Second) ReadAs(SpecNode entry, string first, string second)
    {
        var given = entry["as"] is { IsArray: true } list ? list.Items : [];
        return (given.Count > 0 ? given[0].StringValue ?? first : first, given.Count > 1 ? given[1].StringValue ?? second : second);
    }

    /// <summary>
    ///     Checks texts that the specification gives against the string limit of the call.
    /// </summary>
    /// <param name="what">What the texts are, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <param name="texts">The texts.</param>
    private void CheckTexts(string what, VegaLiteLocation location, IEnumerable<string?> texts)
    {
        foreach (var text in texts)
        {
            _context.CheckText(DataValue.FromString(text), what, location);
        }
    }

    /// <summary>
    ///     Reads one field of every row of a table.
    /// </summary>
    /// <param name="input">The table.</param>
    /// <param name="path">The field path as written in the specification.</param>
    /// <returns>The values, one per row.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    private DataValue[] ReadColumn(DataTable input, string path)
    {
        var reader = new FieldReader(input, path);
        var evaluation = _context.Evaluator.Context;
        var values = new DataValue[input.RowCount];
        for (var row = 0; row < values.Length; row++)
        {
            _context.Tick(row);
            values[row] = reader.Read(row, evaluation);
        }

        _meter.Add(values.Length);
        return values;
    }

    /// <summary>
    ///     Picks the values of some rows out of a column.
    /// </summary>
    /// <param name="column">The values of every row.</param>
    /// <param name="rows">The rows to pick, in order.</param>
    /// <returns>The picked values.</returns>
    private static DataValue[] Pick(DataValue[] column, List<int> rows)
    {
        var values = new DataValue[rows.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = column[rows[i]];
        }

        return values;
    }

    /// <summary>
    ///     Partitions the rows into groups the way Vega's <c>partition</c> does.
    /// </summary>
    /// <remarks>
    ///     The key of a row is the text of the array of its group values as JavaScript writes it (<c>String(array)</c>: the
    ///     elements joined with a comma, null as nothing), so values of different types can share a group and values that
    ///     contain a comma can collide; this is Vega's behavior and is kept. Without <c>groupby</c> there is exactly one group,
    ///     also for an empty table.
    /// </remarks>
    /// <param name="input">The table.</param>
    /// <param name="groupBy">The group-by field paths, or null for no <c>groupby</c>.</param>
    /// <returns>The groups in order of first appearance.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text of a group value is too long or the step budget is exhausted.</exception>
    private List<StatGroup> Partition(DataTable input, string[]? groupBy)
    {
        if (groupBy is null)
        {
            return [new StatGroup([.. Enumerable.Range(0, input.RowCount)], null)];
        }

        var readers = groupBy.Select(path => new FieldReader(input, path)).ToArray();
        var evaluation = _context.Evaluator.Context;
        var maxLength = _context.Limits.MaxStringLength;
        var groups = new List<StatGroup>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var key = new StringBuilder();
        var values = new DataValue[readers.Length];
        for (var row = 0; row < input.RowCount; row++)
        {
            _context.Tick(row);
            key.Clear();
            for (var i = 0; i < readers.Length; i++)
            {
                values[i] = readers[i].Read(row, evaluation);
                if (i > 0)
                {
                    key.Append(',');
                }

                // An undefined or null element is written as nothing; every other value as JavaScript's String() writes it
                if (!values[i].IsNull)
                {
                    key.Append(ExpressionEvaluator.ToJsString(values[i], maxLength, evaluation));
                }
            }

            _meter.Add(key.Length + 1);
            var text = key.ToString();
            if (!index.TryGetValue(text, out var group))
            {
                group = groups.Count;
                index[text] = group;
                groups.Add(new StatGroup([], [.. values]));
            }

            groups[group].Rows.Add(row);
        }

        return groups;
    }

    /// <summary>
    ///     Checks that a curve of at most a number of points fits in the row budget, without keeping the rows.
    /// </summary>
    /// <param name="points">The number of points.</param>
    /// <param name="what">What the rows are, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the rows would exceed <see cref="VegaLiteLimits.MaxDataRows"/>.</exception>
    private void CheckRows(double points, string what, VegaLiteLocation location)
    {
        var rows = Math.Min(points, (double)long.MaxValue / 2);
        _context.Loader.ReserveDerivedRows(rows, what, location);
        _context.Loader.ReleaseRows((long)rows);
    }

    /// <summary>
    ///     The table that a transform generates: rows are added one at a time, each counted in the row budget first.
    /// </summary>
    private sealed class OutputTable
    {
        /// <summary>The transform whose row budget and limits apply.</summary>
        private readonly StatTransforms _owner;

        /// <summary>The field layout of the output.</summary>
        private readonly TableLayout _layout;

        /// <summary>What the rows are, for the message of the row budget.</summary>
        private readonly string _what;

        /// <summary>The location of the transform.</summary>
        private readonly VegaLiteLocation _location;

        /// <summary>The cells written so far (a prefix of the array) and the room for more.</summary>
        private DataValue[] _cells = [];

        /// <summary>
        ///     Initializes an output table.
        /// </summary>
        /// <param name="owner">The transform.</param>
        /// <param name="layout">The field layout.</param>
        /// <param name="what">What the rows are.</param>
        /// <param name="location">The location of the transform.</param>
        public OutputTable(StatTransforms owner, TableLayout layout, string what, VegaLiteLocation location)
        {
            _owner = owner;
            _layout = layout;
            _what = what;
            _location = location;
        }

        /// <summary>Gets the number of rows added.</summary>
        public int Rows { get; private set; }

        /// <summary>
        ///     Adds an empty row.
        /// </summary>
        /// <returns>The index of the row.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown when the row exceeds the row budget or the cell limit.</exception>
        public int AddRow()
        {
            _owner._context.Loader.ReserveDerivedRows(1, _what, _location);
            _layout.Check(Rows + 1L);
            var needed = (Rows + 1L) * _layout.Fields.Length;
            if (needed > _cells.Length)
            {
                Array.Resize(ref _cells, (int)Math.Min(Math.Max(needed, _cells.Length * 2L), TableLayout.MaxCells));
            }

            return Rows++;
        }

        /// <summary>
        ///     Sets the value of an added field in a row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="added">The index of the field in the list the layout was created with.</param>
        /// <param name="value">The value.</param>
        public void Set(int row, int added, DataValue value) =>
            _cells[((long)row * _layout.Fields.Length) + _layout.Added[added]] = value;

        /// <summary>
        ///     Builds the table.
        /// </summary>
        /// <returns>The table of the rows added.</returns>
        public DataTable Build()
        {
            Array.Resize(ref _cells, Rows * _layout.Fields.Length);
            return _layout.Build(_cells, Rows);
        }
    }

    /// <summary>
    ///     Creates the output layout of a transform that has group fields and two output fields.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="groupBy">The group-by field paths, or null.</param>
    /// <param name="outputs">The two output names (literal names).</param>
    /// <param name="location">The location of the transform.</param>
    /// <returns>The layout; the group fields come first in the list of added fields, then the two outputs.</returns>
    private TableLayout GroupedLayout(DataTable input, string[]? groupBy, (string First, string Second) outputs, VegaLiteLocation location) =>
        new(
            input,
            [.. (groupBy ?? []).Select(g => FieldPath.Canonicalize(g)!), TableLayout.Canonical(outputs.First), TableLayout.Canonical(outputs.Second)],
            keepInput: false,
            _context.Limits,
            location);

    /// <summary>
    ///     Writes the group fields of a group into a row.
    /// </summary>
    /// <param name="output">The output table.</param>
    /// <param name="row">The row.</param>
    /// <param name="group">The group.</param>
    private static void WriteDimensions(OutputTable output, int row, StatGroup group)
    {
        if (group.Dimensions is null)
        {
            return;
        }

        for (var i = 0; i < group.Dimensions.Length; i++)
        {
            output.Set(row, i, group.Dimensions[i]);
        }
    }

    /// <summary>
    ///     Gets a number as a value, with NaN (JavaScript's NaN and <c>undefined</c> alike) kept as the number NaN.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    private static DataValue Number(double value) => DataValue.FromNumber(value);

    /// <summary>
    ///     Gets a number as a value, with NaN (JavaScript's <c>undefined</c>) as the null value.
    /// </summary>
    /// <param name="value">The number, or NaN for no value.</param>
    /// <returns>The value.</returns>
    private static DataValue NumberOrNull(double value) => double.IsNaN(value) ? default : DataValue.FromNumber(value);

    /// <summary>
    ///     Estimates the work of a fit over some rows, for the step budget: a unit for each coefficient of each row.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="coefficients">The number of coefficients.</param>
    /// <returns>The cost.</returns>
    private static long FitCost(int rows, int coefficients) => (long)rows * ((coefficients * coefficients) + 8);
}
