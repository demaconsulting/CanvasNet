using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Helpers shared by the tests of the grouped aggregating transforms: building a specification, loading its data, running its
///     transforms the way a render does and listing the rows of a table as text.
/// </summary>
internal static class AggregateTestSupport
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Parses a specification and builds everything a transform run shares, without running any transform.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="token">The token the transforms run under, or null for the token of the test.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits (so that the limits of the run can be lower than those of the parse).</param>
    /// <returns>The loaded table, the context, the loader and the parsed specification.</returns>
    public static (DataTable Table, TransformContext Context, DataLoader Loader, SpecTree Tree) Prepare(
        string json,
        VegaLiteLimits? limits = null,
        CancellationToken? token = null,
        bool parseWithDefaults = false)
    {
        var run = token ?? Ct;
        var tree = SpecParser.Parse(json, "t.vl.json", parseWithDefaults ? null : limits);
        var effective = limits ?? VegaLiteLimits.Default;
        var loader = new DataLoader(tree.SourceName, effective, null, tree.Root["datasets"], null, Ct);
        var table = loader.Load(tree.Root["data"]);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, run, values);
        ParameterEnvironment.Populate(tree, null, values, evaluator);
        return (table, new TransformContext(tree, loader, evaluator, values, effective, run, []), loader, tree);
    }

    /// <summary>
    ///     Parses a specification, loads its data and applies its transforms the way a render does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits.</param>
    /// <returns>The transformed table.</returns>
    public static DataTable Run(string json, VegaLiteLimits? limits = null, bool parseWithDefaults = false)
    {
        var (table, context, _, tree) = Prepare(json, limits, null, parseWithDefaults);
        return RowTransforms.Apply(table, tree.Root["transform"], context);
    }

    /// <summary>
    ///     Applies the first transform of a specification directly with the aggregate transform unit under a token that is already
    ///     canceled (so that the cancellation reaches the loops of the unit instead of the check of the transform pipeline).
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The transformed table (never returned when the unit polls the token).</returns>
    public static DataTable ApplyCanceled(string json)
    {
        using var source = new CancellationTokenSource();
        var (table, context, _, tree) = Prepare(json, null, source.Token);
        source.Cancel();
        var entry = tree.Root["transform"]!.Items[0];
        return AggregateTransforms.Apply(table, entry, SpecParser.FindTransformOperation(entry)!, context);
    }

    /// <summary>
    ///     Builds a specification with inline rows and the given transforms.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <returns>The specification text.</returns>
    public static string Spec(string rows, string transforms) =>
        "{\"data\":{\"values\":" + rows + "},\"transform\":" + transforms + ",\"mark\":\"point\"}";

    /// <summary>
    ///     Builds a specification whose data is a sequence of ids.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <returns>The specification text.</returns>
    public static string SequenceSpec(int count, string transforms) =>
        "{\"data\":{\"sequence\":{\"start\":0,\"stop\":" + count + ",\"as\":\"id\"}},\"transform\":" + transforms + ",\"mark\":\"point\"}";

    /// <summary>
    ///     Builds the JSON rows <c>{"id":i,"v":value}</c> for a list of values (null for a missing value).
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The JSON array.</returns>
    public static string IdRows(params double?[] values) =>
        "[" + string.Join(",", values.Select((v, i) => "{\"id\":" + i + ",\"v\":" + (v is { } n ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null") + "}")) + "]";

    /// <summary>
    ///     Lists the rows of a table as text: the values of the fields joined with a bar.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="fields">The fields to show.</param>
    /// <returns>One text per row.</returns>
    public static string[] Rows(DataTable table, params string[] fields) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => string.Join("|", fields.Select(f => table.GetValue(row, f).ToText())))];

    /// <summary>
    ///     Reads a numeric field of every row.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="field">The field.</param>
    /// <returns>The numbers (NaN for a value that is not a number).</returns>
    public static double[] Numbers(DataTable table, string field) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => table.GetValue(row, field) is { IsNumber: true } v ? v.Number : double.NaN)];
}
