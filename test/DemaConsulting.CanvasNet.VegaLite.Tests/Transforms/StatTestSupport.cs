using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Helpers of the tests of the statistical transforms: generating the JSON rows of a numeric series with the invariant culture, so that the text of a number never depends on the
///     culture of the machine that runs the tests.
/// </summary>
internal static class StatTestSupport
{
    /// <summary>
    ///     Builds the JSON rows <c>{"x":x,"y":f(x)}</c> for a sequence of x values.
    /// </summary>
    /// <param name="xs">The x values.</param>
    /// <param name="f">The function that gives y for an x.</param>
    /// <returns>The JSON array.</returns>
    public static string XyRows(IEnumerable<int> xs, Func<int, double> f) =>
        "[" + string.Join(",", xs.Select(x => string.Create(CultureInfo.InvariantCulture, $"{{\"x\":{x},\"y\":{f(x)}}}"))) + "]";

    /// <summary>
    ///     Builds the JSON rows <c>{"v":v}</c> for a sequence of values.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The JSON array.</returns>
    public static string ValueRows(IEnumerable<int> values) =>
        "[" + string.Join(",", values.Select(v => string.Create(CultureInfo.InvariantCulture, $"{{\"v\":{v}}}"))) + "]";

    /// <summary>
    ///     Builds the JSON rows <c>{"g":g,"v":v}</c> for several groups that each hold the values 1 to <paramref name="count"/>.
    /// </summary>
    /// <param name="groups">The group names.</param>
    /// <param name="count">The number of values of each group.</param>
    /// <returns>The JSON array.</returns>
    public static string GroupRows(IEnumerable<string> groups, int count) =>
        "[" + string.Join(",", groups.SelectMany(g => Enumerable.Range(1, count).Select(v => string.Create(CultureInfo.InvariantCulture, $"{{\"g\":\"{g}\",\"v\":{v}}}")))) + "]";

    /// <summary>
    ///     Builds the JSON rows <c>{"g":k,"k":"key","v":1}</c> of a cross table with many groups and pivot values.
    /// </summary>
    /// <param name="groups">The number of groups (each row has its own group number).</param>
    /// <param name="keys">The number of distinct pivot values.</param>
    /// <returns>The JSON array.</returns>
    public static string PivotRows(int groups, int keys) =>
        "[" + string.Join(",", Enumerable.Range(0, groups).Select(k => string.Create(CultureInfo.InvariantCulture, $"{{\"g\":{k},\"k\":\"k{k % keys:D4}\",\"v\":1}}"))) + "]";
}
