using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Helpers shared by the tests of the time unit transform, the time predicates and the implicit time units of an encoding: they run the
///     data stage with the calendar and the implicit date parse of a render, but never read the host's zone, clock or culture. The class holds no tests.
/// </summary>
internal static class TimeTestPipeline
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Parses a specification, loads its data with the implicit date parse and the calendar of a zone, and builds the context a transform run shares.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="zone">The zone of the chart, or null for UTC.</param>
    /// <param name="limits">The limits of the run, or null for the defaults.</param>
    /// <param name="token">The token the transforms run under, or null for the token of the test.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits (so that the limits of the run can be lower than those of the parse).</param>
    /// <returns>The loaded table, the context, the loader, the parsed specification and the calendar.</returns>
    public static (DataTable Table, TransformContext Context, DataLoader Loader, SpecTree Tree, TimeCalendar Calendar) Prepare(
        string json,
        TimeZoneInfo? zone = null,
        VegaLiteLimits? limits = null,
        CancellationToken? token = null,
        bool parseWithDefaults = false)
    {
        var run = token ?? Ct;
        var tree = SpecParser.Parse(json, "t.vl.json", parseWithDefaults ? null : limits);
        var effective = limits ?? VegaLiteLimits.Default;
        var calendar = TimeCalendar.Create(zone);
        var loader = new DataLoader(tree.SourceName, effective, null, tree.Root["datasets"], null, Ct, calendar: calendar);
        var table = loader.Load(tree.Root["data"], TemporalFields.Collect(tree.Root));
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, run, values, calendar);
        ParameterEnvironment.Populate(tree, null, values, evaluator);
        return (table, new TransformContext(tree, loader, evaluator, values, effective, run, []), loader, tree, calendar);
    }

    /// <summary>
    ///     Parses a specification, loads its data and applies its transforms the way a render does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="zone">The zone of the chart, or null for UTC.</param>
    /// <param name="limits">The limits of the run, or null for the defaults.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits.</param>
    /// <returns>The transformed table.</returns>
    public static DataTable Run(string json, TimeZoneInfo? zone = null, VegaLiteLimits? limits = null, bool parseWithDefaults = false)
    {
        var (table, context, _, tree, _) = Prepare(json, zone, limits, null, parseWithDefaults);
        return RowTransforms.Apply(table, tree.Root["transform"], context);
    }

    /// <summary>
    ///     Runs the data stage, the explicit transforms and the implicit encoding stage of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="zone">The zone of the chart, or null for UTC.</param>
    /// <param name="limits">The limits of the run, or null for the defaults.</param>
    /// <returns>The plan and the data of the encoding.</returns>
    public static (EncodingPlan Plan, EncodingData Data) Encode(string json, TimeZoneInfo? zone = null, VegaLiteLimits? limits = null)
    {
        var (table, context, _, tree, _) = Prepare(json, zone, limits, Ct);
        var rows = RowTransforms.Apply(table, tree.Root["transform"], context);
        var plan = EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));
        return (plan, EncodingTransforms.Apply(plan, rows, context));
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
    ///     Reads a date field of a row.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="row">The row.</param>
    /// <param name="field">The field.</param>
    /// <returns>The milliseconds since the epoch of a date, or NaN when the value is not a date.</returns>
    public static double Ms(DataTable table, int row, string field) =>
        table.GetValue(row, field) is { IsDate: true } date ? date.Milliseconds : double.NaN;

    /// <summary>
    ///     Reads a date field of every row.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="field">The field.</param>
    /// <returns>The milliseconds of the dates (NaN for a value that is not a date).</returns>
    public static double[] Dates(DataTable table, string field) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => Ms(table, row, field))];

    /// <summary>
    ///     Lists the rows of a table as text: the values of the fields joined with a bar.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="fields">The fields to show.</param>
    /// <returns>One text per row.</returns>
    public static string[] Rows(DataTable table, params string[] fields) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => string.Join("|", fields.Select(f => table.GetValue(row, f).ToText())))];
}
