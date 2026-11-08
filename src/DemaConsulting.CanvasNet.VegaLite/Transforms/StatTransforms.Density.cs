// Portions derived from Vega (BSD-3-Clause): the KDE transform of vega-transforms; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>density</c> transform (a partial file of the statistical transform unit).
/// </content>
internal sealed partial class StatTransforms
{
    /// <summary>The number of work elements that one kernel evaluation of a density is charged as (see <see cref="WorkMeter"/>): an exponential and a few multiplications, measured about 13 nanoseconds, so four at about three nanoseconds an element.</summary>
    private const int KernelWeight = 4;

    /// <summary>
    ///     Gets the work elements of one kernel evaluation of a density over the rows of a group, which is charged for every sample point.
    /// </summary>
    /// <remarks>
    ///     The product is widened to a long before it is multiplied: for a group of more than 536 million rows the product of two ints wrapped to a negative number, which the meter ignores, so the work of the density was free.
    /// </remarks>
    /// <param name="rows">The number of rows of the group.</param>
    /// <returns>The work elements.</returns>
    internal static long KernelCost(int rows) => (long)rows * KernelWeight;

    /// <summary>
    ///     Estimates the density (or the cumulative distribution) of a field for every group by a Gaussian kernel and generates the sampled curve.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Vega-Lite's defaults apply: the output fields are <c>value</c> and <c>density</c>, and the resolution is
    ///         <c>shared</c>. With a shared resolution the domain is <c>extent</c> or the extent of the whole column, and every group is sampled on
    ///         the same uniform grid of <c>steps</c> (default 200) intervals, which is what makes the densities stackable;
    ///         with an independent resolution each group has its own extent and an adaptive sample between <c>minsteps</c>
    ///         (25) and <c>maxsteps</c> (200); <c>steps</c> overrides both. The support of a group is the raw column
    ///         including the rows that are not numbers (see <see cref="KernelDensity"/>); <c>counts</c> multiplies the density by the
    ///         number of rows of the group; <c>cumulative</c> samples the distribution function.
    ///     </para>
    ///     <para>
    ///         Every sample point costs one kernel evaluation per row of the group, which is charged to the step budget as
    ///         the curve is sampled, so a large group sampled at many points is refused rather than run; the number of points is
    ///         bounded by the row budget before the sampling starts (a uniform grid) or while it refines (an adaptive one).
    ///     </para>
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose density is estimated (<c>density</c>).</param>
    /// <returns>The generated table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output exceeds a limit.</exception>
    private DataTable Density(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var fieldPath = operation.StringValue!;
        var groupBy = ReadGroupBy(entry);
        var names = ReadAs(entry, "value", "density");
        CheckTexts("A field or output name of the density", location, [fieldPath, names.First, names.Second, .. groupBy ?? []]);
        var cumulative = entry["cumulative"] is { IsBoolean: true, BoolValue: true };
        var counts = entry["counts"] is { IsBoolean: true, BoolValue: true };
        var bandwidth = entry["bandwidth"]?.AsNumber() ?? 0;
        var shared = (entry["resolve"]?.AsString() ?? "shared") == "shared";
        var steps = entry["steps"]?.AsNumber() ?? double.NaN;
        var minSteps = Truthy(steps) ?? Truthy(entry["minsteps"]?.AsNumber()) ?? CurveSampler.DefaultMinSteps;
        var maxSteps = Truthy(steps) ?? Truthy(entry["maxsteps"]?.AsNumber()) ?? CurveSampler.DefaultMaxSteps;
        double[]? domain = entry["extent"] is { IsArray: true } extent ? [extent.Items[0].NumberValue, extent.Items[1].NumberValue] : null;
        var support = ReadColumn(input, fieldPath);
        var groups = Partition(input, groupBy);

        // A shared resolution gives every group the same domain and the same uniform grid
        if (shared)
        {
            domain ??= ExtentOf(support);
            minSteps = maxSteps = Truthy(steps) ?? maxSteps;
        }

        var output = new OutputTable(this, GroupedLayout(input, groupBy, names, location), "density transform output", location);
        var xIndex = groupBy?.Length ?? 0;

        // The sampler raises the maximum to the minimum, and a uniform grid has maxSteps + 1 points; the minimum grid is always sampled
        var effectiveMin = StatisticsPrimitives.IsTruthy(minSteps) ? minSteps : CurveSampler.DefaultMinSteps;
        var effectiveMax = StatisticsPrimitives.JsMax(effectiveMin, StatisticsPrimitives.IsTruthy(maxSteps) ? maxSteps : CurveSampler.DefaultMaxSteps);
        var upFront = Math.Ceiling(ExpressionEvaluator.NumbersEqual(effectiveMin, effectiveMax) ? effectiveMax : effectiveMin) + 1;
        foreach (var group in groups)
        {
            var values = Pick(support, group.Rows);
            _meter.Add(values.Length * 8L);
            var kde = new KernelDensity(values, bandwidth);
            var scale = counts ? group.Rows.Count : 1;
            var local = domain ?? ExtentOf(values);

            // The points that are sampled whatever the curve looks like are checked before any is computed
            CheckRows(upFront, "density transform output", location);

            var rows = values.Length;
            Func<double, double> curve = cumulative ? kde.Cdf : kde.Pdf;
            var points = CurveSampler.Sample(
                x =>
                {
                    // One kernel evaluation for every row of the group, each counted as KernelWeight work elements (an evaluation is an exponential, measured about 13 nanoseconds)
                    _meter.Add(KernelCost(rows));
                    return curve(x);
                },
                local[0],
                local[1],
                minSteps,
                maxSteps,
                count => CheckProgress(count, "density transform output", location));
            foreach (var (x, y) in points)
            {
                var row = output.AddRow();
                WriteDimensions(output, row, group);
                output.Set(row, xIndex, Number(x));
                output.Set(row, xIndex + 1, Number(y * scale));
            }
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return output.Build();
    }

    /// <summary>
    ///     Gets a step count as Vega's <c>a || b</c> reads it: the number unless it is missing, zero or NaN.
    /// </summary>
    /// <param name="value">The number, or null.</param>
    /// <returns>The number, or null when it is missing, zero or NaN.</returns>
    private static double? Truthy(double? value) => value is { } number && StatisticsPrimitives.IsTruthy(number) ? number : null;

    /// <summary>
    ///     Gets the extent of a column as numbers (vega-util <c>extent</c>), NaN for an extent that does not exist.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The minimum and the maximum as numbers.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text of an array value would be longer than the string limit.</exception>
    private double[] ExtentOf(DataValue[] values) =>
        StatisticsPrimitives.Extent(values, _context.Limits.MaxStringLength) is { } found
            ? [ExpressionEvaluator.ToNumber(found.Min), ExpressionEvaluator.ToNumber(found.Max)]
            : [double.NaN, double.NaN];
}
