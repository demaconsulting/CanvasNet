// Portions derived from Vega (BSD-3-Clause): the regression transform of vega-regression; see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>regression</c> transform (a partial file of the statistical transform unit).
/// </content>
internal sealed partial class StatTransforms
{
    /// <summary>The number of steps of the minimum grid of a regression curve (Vega's <c>sampleCurve(predict, domain, 25, 200)</c>).</summary>
    private const double RegressionMinSteps = 25;

    /// <summary>The largest number of steps of a regression curve.</summary>
    private const double RegressionMaxSteps = 200;

    /// <summary>
    ///     Fits a regression model to every group and generates the trend line or the parameters of the fit.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Like Vega, a group whose number of rows (all rows, valid or not) is not more than the parameters of the model is
    ///         skipped. A linear or constant model gives its two end points (the extent of the group or the <c>extent</c> of the
    ///         transform); every other model gives the adaptive sample of its curve over that domain. With <c>params: true</c>
    ///         the output is one row per group with <c>keys</c> (the group values), <c>coef</c> and <c>rSquared</c>.
    ///     </para>
    ///     <para>
    ///         A log model ignores an <c>extent</c> that starts at or below zero (with a warning). The x of the end points of a
    ///         linear model is the extent value itself; the x of a sampled curve is a number (Vega samples with arithmetic that
    ///         turns a date into a text, which this port does not reproduce: a date is its milliseconds).
    ///     </para>
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is predicted (<c>regression</c>).</param>
    /// <returns>The generated table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output exceeds a limit.</exception>
    private DataTable Regression(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var yPath = operation.StringValue!;
        var xPath = entry["on"]!.StringValue!;
        var method = entry["method"]?.AsString() ?? "linear";
        var order = entry["order"] is { IsNumber: true } given ? (int)given.NumberValue : 3;
        var paramsOnly = entry["params"] is { IsBoolean: true, BoolValue: true };
        var groupBy = ReadGroupBy(entry);
        var names = ReadAs(entry, xPath, yPath);
        CheckTexts("A field or output name of the regression", location, [xPath, yPath, names.First, names.Second, .. groupBy ?? []]);

        // The extent of a log regression must be positive; Vega ignores one that is not
        double[]? domain = entry["extent"] is { IsArray: true } extent ? [extent.Items[0].NumberValue, extent.Items[1].NumberValue] : null;
        if (domain is not null && method == "log" && domain[0] <= 0)
        {
            Warn("Ignoring the extent of a log regression, which has values that are not positive.", location);
            domain = null;
        }

        var xs = ReadColumn(input, xPath);
        var ys = ReadColumn(input, yPath);
        var groups = Partition(input, groupBy);
        var layout = paramsOnly
            ? new TableLayout(input, ["keys", "coef", "rSquared"], keepInput: false, _context.Limits, location)
            : GroupedLayout(input, groupBy, names, location);
        var output = new OutputTable(this, layout, "regression transform output", location);
        var coefficients = method == "poly" ? order + 1 : 3;
        var degrees = RegressionFit.DegreesOfFreedom(method, order);
        var skipped = 0;
        foreach (var group in groups)
        {
            // Every row of the group counts, whether or not it is a valid point
            var n = group.Rows.Count;
            if (n <= degrees)
            {
                skipped++;
                continue;
            }

            var groupX = Pick(xs, group.Rows);
            var groupY = Pick(ys, group.Rows);
            _meter.Add(FitCost(n, coefficients));
            var model = RegressionFit.Fit(method, order, groupX, groupY);
            if (paramsOnly)
            {
                var row = output.AddRow();
                output.Set(row, 0, group.Dimensions is null ? default : DataValue.FromArray(group.Dimensions));
                output.Set(row, 1, DataValue.FromArray(model.Coefficients.Select(Number)));
                output.Set(row, 2, Number(model.RSquared));
                continue;
            }

            WriteTrend(output, group, groupX, model, method, domain, location);
        }

        if (skipped > 0)
        {
            Warn(
                string.Create(CultureInfo.InvariantCulture, $"The regression skipped {skipped} group{(skipped == 1 ? string.Empty : "s")} with no more rows than the {degrees} parameter{(degrees == 1 ? string.Empty : "s")} of the model."),
                location);
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return output.Build();
    }

    /// <summary>
    ///     Writes the trend line of one group: the end points of a linear or constant model, or the sample of a curve.
    /// </summary>
    /// <param name="output">The output table.</param>
    /// <param name="group">The group.</param>
    /// <param name="groupX">The x values of the rows of the group.</param>
    /// <param name="model">The fitted model.</param>
    /// <param name="method">The method.</param>
    /// <param name="domain">The extent of the transform, or null for the extent of the group.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the points exceed the row budget.</exception>
    private void WriteTrend(OutputTable output, StatGroup group, DataValue[] groupX, RegressionModel model, string method, double[]? domain, VegaLiteLocation location)
    {
        // The two output fields follow the group fields in the list the layout was created with
        var xIndex = group.Dimensions?.Length ?? 0;
        DataValue first;
        DataValue last;
        if (domain is not null)
        {
            (first, last) = (Number(domain[0]), Number(domain[1]));
        }
        else if (StatisticsPrimitives.Extent(groupX, _context.Limits.MaxStringLength) is { } extent)
        {
            (first, last) = (extent.Min, extent.Max);
        }
        else
        {
            // A group without any value in x has no extent: JavaScript's [undefined, undefined]
            (first, last) = (default, default);
        }

        if (method is "linear" or "constant")
        {
            // A line needs only its end points
            foreach (var x in new[] { first, last })
            {
                var row = output.AddRow();
                WriteDimensions(output, row, group);
                output.Set(row, xIndex, x);
                output.Set(row, xIndex + 1, Number(model.Predict(x.IsNull ? double.NaN : ExpressionEvaluator.ToNumber(x))));
            }

            return;
        }

        // Any other model is sampled along its curve; the refinement is bounded by the row budget and the token
        var points = CurveSampler.Sample(
            model.Predict,
            first.IsNull ? double.NaN : ExpressionEvaluator.ToNumber(first),
            last.IsNull ? double.NaN : ExpressionEvaluator.ToNumber(last),
            RegressionMinSteps,
            RegressionMaxSteps,
            count => CheckProgress(count, "regression transform output", location));
        _meter.Add(points.Count);
        foreach (var (x, y) in points)
        {
            var row = output.AddRow();
            WriteDimensions(output, row, group);
            output.Set(row, xIndex, Number(x));
            output.Set(row, xIndex + 1, Number(y));
        }
    }

    /// <summary>
    ///     Checks, while a curve is sampled, that the points held fit in the row budget and that the call is not canceled.
    /// </summary>
    /// <param name="points">The number of points held.</param>
    /// <param name="what">What the rows are, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the points exceed the row budget.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    private void CheckProgress(int points, string what, VegaLiteLocation location)
    {
        _context.CancellationToken.ThrowIfCancellationRequested();
        CheckRows(points, what, location);
    }
}
