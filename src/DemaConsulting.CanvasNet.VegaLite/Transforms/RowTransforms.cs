using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Applies the row transforms of a specification to the rows of a chart: <c>calculate</c>, <c>filter</c>, <c>fold</c>,
///     <c>flatten</c>, <c>sample</c>, <c>pivot</c> and <c>lookup</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Execution.</b> The transforms of the <c>transform</c> array run in the order of the text, each on the table that
///         the previous one produced, after the data loader and before the scales. Every transform is eager and immutable: it
///         reads a <see cref="DataTable"/> and builds a new one (a transform that keeps every row may return its input).
///         Entries that name a transform of a later phase are skipped here; the feature catalog reports them. The
///         expressions were compiled when the specification was parsed, so no text is parsed here.
///     </para>
///     <list type="bullet">
///         <item><description>
///             <b>calculate</b> evaluates the expression for every row with <c>datum</c> bound to the row and stores the result
///             in the field <c>as</c> (a scalar, an array or an object; a regular expression cannot be stored and gives null
///             and the warning <c>vl-expr-runtime</c>). An existing field of that name is replaced.
///         </description></item>
///         <item><description>
///             <b>filter</b> keeps the rows for which an expression is truthy (JavaScript truthiness) or a predicate holds. The
///             predicates are <c>equal</c>, <c>lt</c>, <c>lte</c>, <c>gt</c>, <c>gte</c>, <c>range</c> (inclusive, an endpoint of
///             null is open, reversed endpoints are swapped), <c>oneOf</c> and <c>valid</c> on a <c>field</c> with the
///             comparison semantics of JavaScript, composed with <c>and</c>, <c>or</c> and <c>not</c>, and <c>param</c>, which
///             reads the truthiness of a variable parameter (a selection parameter keeps every row and is reported). A time unit
///             of a predicate is ignored and a predicate on a date object is ignored, both reported by the catalog.
///         </description></item>
///         <item><description>
///             <b>fold</b> turns every row into one row per folded field with the field name in <c>as[0]</c> (default
///             <c>key</c>) and its value in <c>as[1]</c> (default <c>value</c>), in the order of the rows and of the fields; the
///             folded fields stay in the row. <b>flatten</b> turns every row into as many rows as its longest array: the arrays of
///             several fields are expanded in parallel (shorter ones padded with null), a scalar counts as an array of one value,
///             null and an empty array as none, and a row whose fields are all empty is dropped; the element goes to the field
///             <c>as[i]</c> (default the field itself).
///         </description></item>
///         <item><description>
///             <b>sample</b> keeps at most <c>sample</c> rows (<see cref="RowSampler"/>: reservoir sampling seeded from the JSON
///             pointer of the transform, deterministic and in source order). <b>pivot</b> groups the rows by <c>groupby</c> and
///             turns the values of the <c>pivot</c> field into columns (named by their text, in ordinal order, at most
///             <c>limit</c> of them when given, applied before the field bound, which only checks the columns kept) holding the sum of <c>value</c> per group (<c>op</c> sum, the default) or the
///             number of rows (<c>count</c>); a combination without rows is 0; rows whose pivot value is null are skipped with the
///             warning <c>vl-transform-runtime</c>; the result has the group fields and the pivot columns only.
///         </description></item>
///         <item><description>
///             <b>lookup</b> finds, for the value of the <c>lookup</c> field, the first row of <c>from.data</c> (inline, named or a
///             local file, loaded by the loader of the call under the same sandbox and limits) whose <c>from.key</c> has the same
///             text, and copies <c>from.fields</c> into the <c>as</c> fields (without <c>fields</c> the whole row goes into the one
///             field <c>as</c>, <c>data</c> by default, as an object); a row without a match gets <c>default</c> (null by default).
///         </description></item>
///     </list>
///     <para>
///         <b>Limits and cancellation.</b> Fold and flatten reserve the rows of their output in the row budget of the loader
///         before they allocate it, and release the rows of their input when the output is complete, so the peak of two tables
///         counts; filter, sample and pivot release the rows they drop; a lookup source counts as any loaded source. Every
///         derived table respects <see cref="VegaLiteLimits.MaxFieldCount"/> and a bound of <see cref="TableLayout.MaxCells"/>
///         cells. The step budget bounds expressions and the comparisons of filter predicates (a comparison of long texts, the members
///         of <c>oneOf</c> and the rebuilding of a flattened field are charged to it), strings are bounded by <see cref="VegaLiteLimits.MaxStringLength"/>, and
///         the cancellation token is checked every 1,024 rows and between transforms. Failures are
///         <see cref="VegaLiteRenderException"/>s located at the transform. An instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed partial class RowTransforms
{
    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>What the run shares.</summary>
    private readonly TransformContext _context;

    /// <summary>
    ///     Initializes a run.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    private RowTransforms(TransformContext context) => _context = context;

    /// <summary>
    ///     Applies the transforms of a view to a table.
    /// </summary>
    /// <param name="input">The loaded table.</param>
    /// <param name="transforms">The <c>transform</c> array of the view, or null.</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The transformed table; <paramref name="input"/> itself when there is nothing to do.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the transform, when a limit is exceeded or an expression fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static DataTable Apply(DataTable input, SpecNode? transforms, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);
        if (transforms is not { IsArray: true } || transforms.Items.Count == 0)
        {
            return input;
        }

        return new RowTransforms(context).Run(input, transforms);
    }

    /// <summary>
    ///     Gets the cancellation token.
    /// </summary>
    private CancellationToken Cancellation => _context.CancellationToken;

    /// <summary>
    ///     Runs the transforms in order.
    /// </summary>
    /// <param name="input">The loaded table.</param>
    /// <param name="transforms">The <c>transform</c> array.</param>
    /// <returns>The transformed table.</returns>
    private DataTable Run(DataTable input, SpecNode transforms)
    {
        var table = input;
        foreach (var entry in transforms.Items)
        {
            Cancellation.ThrowIfCancellationRequested();
            var operation = SpecParser.FindTransformOperation(entry);
            if (operation is null || !SpecParser.IsSupportedTransform(operation.Name!))
            {
                continue;
            }

            table = operation.Name switch
            {
                "calculate" => Calculate(table, entry, operation),
                "filter" => Filter(table, operation),
                "fold" => Fold(table, entry, operation),
                "flatten" => Flatten(table, entry, operation),
                "sample" => Sample(table, entry, operation),
                "pivot" => Pivot(table, entry, operation),
                _ => Lookup(table, entry, operation),
            };
        }

        Cancellation.ThrowIfCancellationRequested();
        return table;
    }

    /// <summary>
    ///     Gets the location of a node of the specification.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location.</returns>
    private VegaLiteLocation Locate(SpecNode node) => _context.Tree.Locate(node);

    /// <summary>
    ///     Checks the cancellation token once per batch of rows.
    /// </summary>
    /// <param name="row">The row that is about to be processed.</param>
    private void Tick(long row)
    {
        if (row % CancellationInterval == 0)
        {
            Cancellation.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Checks that a value that the specification gives a transform, and that a transform copies into the cells of its
    ///     output, has no text (at any depth) longer than the limit of the call.
    /// </summary>
    /// <remarks>
    ///     The limit of the call can be lower than the one the specification was parsed with, so a text of the specification
    ///     is not trusted to fit. The check is made once, before any output is allocated.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when a text is longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
    private void CheckText(DataValue value, string what, VegaLiteLocation location)
    {
        var maxLength = _context.Limits.MaxStringLength;
        if (value.HasTextLongerThan(maxLength))
        {
            throw new VegaLiteRenderException(
                $"{what} has a text longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).",
                location);
        }
    }

    /// <summary>
    ///     Records a recoverable finding of a transform.
    /// </summary>
    /// <param name="id">The identifier (<c>vl-transform-runtime</c> or <c>vl-expr-runtime</c>).</param>
    /// <param name="message">The description.</param>
    /// <param name="location">The location of the transform or expression.</param>
    private void Warn(string id, string message, VegaLiteLocation location) =>
        _context.Warnings.Add(new FeatureReport(id, message, location, FeatureReportKind.Warning));

    /// <summary>
    ///     Counts the rows that had a recoverable problem and remembers the first.
    /// </summary>
    private sealed class RowFinding
    {
        /// <summary>Gets the number of rows.</summary>
        public int Count { get; private set; }

        /// <summary>Gets the first affected row, or -1.</summary>
        public int FirstRow { get; private set; } = -1;

        /// <summary>
        ///     Adds an affected row.
        /// </summary>
        /// <param name="row">The row.</param>
        public void Add(int row)
        {
            if (Count++ == 0)
            {
                FirstRow = row;
            }
        }

        /// <summary>
        ///     Describes the rows for a message.
        /// </summary>
        /// <returns>The count and the first row.</returns>
        public string Describe() =>
            string.Create(CultureInfo.InvariantCulture, $"{Count} row{(Count == 1 ? string.Empty : "s")}, the first at row index {FirstRow}");
    }

    /// <summary>
    ///     Computes a new field for every row.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The expression text.</param>
    /// <returns>The table with the field.</returns>
    private DataTable Calculate(DataTable input, SpecNode entry, SpecNode operation)
    {
        if (_context.Tree.Expressions.Get(operation) is not { } expression)
        {
            return input;
        }

        var location = Locate(entry);
        var name = TableLayout.Canonical(entry["as"]!.StringValue!);
        var layout = new TableLayout(input, [name], keepInput: true, _context.Limits, location);
        var cells = layout.Allocate(input.RowCount);
        var target = layout.Added[0];
        var stored = new RowFinding();
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            layout.CopyRow(input, row, cells, row);
            var value = _context.Evaluator.Evaluate(expression, input, row);
            if (value.IsRegExp)
            {
                stored.Add(row);
                value = DataValue.Null;
            }

            cells[((long)row * layout.Fields.Length) + target] = value;
        }

        if (stored.Count > 0)
        {
            Warn(
                "vl-expr-runtime",
                $"The calculate expression produced a regular expression, which cannot be stored in a field; null is used ({stored.Describe()}).",
                expression.Location);
        }

        return layout.Build(cells, input.RowCount);
    }

    /// <summary>
    ///     Keeps the rows for which a filter holds.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="filter">The <c>filter</c> value: an expression text or a predicate.</param>
    /// <returns>The table of the rows kept.</returns>
    private DataTable Filter(DataTable input, SpecNode filter)
    {
        var kept = new List<int>();
        try
        {
            var predicate = BuildPredicate(filter, input);
            for (var row = 0; row < input.RowCount; row++)
            {
                Tick(row);
                if (predicate.Test(input, row))
                {
                    kept.Add(row);
                }
            }
        }
        catch (ExpressionRuntimeException ex)
        {
            // A comparison of a predicate can exhaust the step budget or convert an array to text beyond the string limit; the
            // message names the limit, so the failure is reported like one of an expression, located at the filter
            throw new VegaLiteRenderException(ex.Message, Locate(filter));
        }

        return SelectRows(input, kept, Locate(filter));
    }

    /// <summary>
    ///     Builds the table of some rows of another, in the order given, and gives the dropped rows back to the budget.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="rows">The rows to keep, in the order they have in the new table.</param>
    /// <param name="location">The location reported when the table would exceed the cell limit.</param>
    /// <returns>The new table; the input itself when every row is kept in order.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the table would have more than <see cref="TableLayout.MaxCells"/> cells.</exception>
    private DataTable SelectRows(DataTable input, IReadOnlyList<int> rows, VegaLiteLocation location)
    {
        if (rows.Count == input.RowCount)
        {
            return input;
        }

        // The layout keeps every input field; its allocation checks the cell limit before anything is allocated
        var layout = new TableLayout(input, [], keepInput: true, _context.Limits, location);
        var cells = layout.Allocate(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            Tick(i);
            layout.CopyRow(input, rows[i], cells, i);
        }

        _context.Loader.ReleaseRows(input.RowCount - rows.Count);
        return layout.Build(cells, rows.Count);
    }
}
