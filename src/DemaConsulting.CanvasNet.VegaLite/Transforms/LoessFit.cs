// Portions derived from Vega (BSD-3-Clause: vega-statistics loess) and science.js (BSD-3-Clause, Jason Davies); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Locally weighted regression (LOESS) as vega-statistics computes it: a tricube-weighted local linear fit over a sliding
///     window of nearest neighbors, refined by three robustness iterations.
/// </summary>
/// <remarks>
///     <para>
///         <b>Algorithm.</b> The valid points are sorted by x and mean-centered. For each point the window holds the
///         <c>max(2, trunc(bandwidth * n))</c> nearest neighbors (it slides right as x grows); the weights are the tricube kernel of
///         the distance to the farther edge of the window times the robustness weight; the local line comes from ordinary least
///         squares on the weighted moments. After each of the first two passes the residuals' median sets the robustness weights
///         (the bisquare of residual / (6 median)); a median below 1e-12 stops the iteration. Points with the same x are merged
///         by the online update of Vega's output step, which averages the fitted values of the repeats.
///     </para>
///     <para>
///         <b>JavaScript semantics kept on purpose.</b> A window that reaches past the data (a group of one point has a
///         two-point window) reads <c>undefined</c> in JavaScript, which is NaN in arithmetic; every read here goes through
///         <see cref="At"/>, so such a group gives a NaN value instead of an exception, exactly as Vega does. The merge of
///         repeated x values starts its average at the second repeat (the first repeat replaces the fitted value), which is
///         what Vega's code does and is kept. The function is pure and thread-safe; its cost is about
///         <c>n * window * 3</c> weight evaluations plus a sort.
///     </para>
/// </remarks>
internal static class LoessFit
{
    /// <summary>The number of robustness iterations after the first fit (Vega's <c>maxiters</c>).</summary>
    private const int MaxIterations = 2;

    /// <summary>
    ///     The number of work elements that one weight evaluation of the window loop is charged as. A work element is calibrated at about three nanoseconds (see <see cref="WorkMeter"/>: the default
    ///     budget of 50,000,000 steps of 64 elements stops a transform in about ten seconds); one evaluation is a tricube kernel, five multiply-adds and a bounds-checked read, measured at about nine
    ///     nanoseconds on a current desktop processor, so it counts three.
    /// </summary>
    private const long EvaluationWeight = 3;

    /// <summary>The smallest median residual that is still iterated on, and the weight of a point that is an extreme outlier.</summary>
    private const double Epsilon = 1e-12;

    /// <summary>
    ///     Estimates the work of a fit, in work elements, so that the caller can charge it before it starts.
    /// </summary>
    /// <param name="n">The number of rows of the group (an upper bound of the valid points).</param>
    /// <param name="bandwidth">The bandwidth.</param>
    /// <returns>The work elements: the weight evaluations (the points times the window times the passes, each counted <see cref="EvaluationWeight"/> times), and the sort.</returns>
    public static long Cost(int n, double bandwidth)
    {
        var window = Math.Max(2, NumericConversions.ToInt64Saturating(Math.Max(0, bandwidth) * n));
        return ((long)n * (Math.Min(window, n) + 2) * (MaxIterations + 1) * EvaluationWeight) + ((long)n * 16);
    }

    /// <summary>
    ///     Fits a loess curve to a group of points.
    /// </summary>
    /// <param name="x">The x of every row.</param>
    /// <param name="y">The y of every row, as many as <paramref name="x"/>.</param>
    /// <param name="bandwidth">The share of the points in each window, from 0 to 1; Vega's default is 0.3.</param>
    /// <param name="progress">Called once per fitted point with the index of the pass, so that the caller can poll cancellation; null for none.</param>
    /// <returns>The smoothed points in increasing x; points with the same x are merged. Empty when no pair is valid.</returns>
    /// <exception cref="ArgumentException">Thrown when the lists differ in length.</exception>
    public static List<(double X, double Y)> Fit(IReadOnlyList<DataValue> x, IReadOnlyList<DataValue> y, double bandwidth, Action<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Count != y.Count)
        {
            throw new ArgumentException("The x and the y of a loess fit must have the same length.", nameof(y));
        }

        // The valid pairs, stably sorted by x, and their means
        var (xv, yv, ux, uy) = SortedCenteredPoints(x, y);
        var n = xv.Length;
        var bw = Math.Max(2, ExpressionEvaluator.ToInt32(bandwidth * n));
        var yhat = new double[n];
        var residuals = new double[n];
        var robustWeights = new double[n];
        Array.Fill(robustWeights, 1.0);
        for (var iter = 0; iter <= MaxIterations; iter++)
        {
            var i0 = 0;
            var i1 = bw - 1;
            for (var i = 0; i < n; ++i)
            {
                progress?.Invoke(i);
                var dx = xv[i];

                // The edge of the window that is farther from the point
                var edge = (dx - At(xv, i0)) > (At(xv, i1) - dx) ? i0 : i1;
                double w = 0;
                double sx = 0;
                double sy = 0;
                double sxy = 0;
                double sx2 = 0;

                // Avoid the singularity of a zero distance: `|d| || 1`
                var distance = Math.Abs(At(xv, edge) - dx);
                var denom = 1 / (StatisticsPrimitives.IsTruthy(distance) ? distance : 1);
                for (var k = i0; k <= i1; ++k)
                {
                    var xk = At(xv, k);
                    var yk = At(yv, k);
                    var weight = Tricube(Math.Abs(dx - xk) * denom) * At(robustWeights, k);
                    var xkw = xk * weight;
                    w += weight;
                    sx += xkw;
                    sy += yk * weight;
                    sxy += yk * xkw;
                    sx2 += xk * xkw;
                }

                // The local linear fit
                var line = RegressionFit.Ols(sx / w, sy / w, sxy / w, sx2 / w);
                yhat[i] = line[0] + (line[1] * dx);
                residuals[i] = Math.Abs(yv[i] - yhat[i]);
                UpdateInterval(xv, i + 1, ref i0, ref i1);
            }

            if (iter == MaxIterations)
            {
                break;
            }

            var medianResidual = Median(residuals);
            if (Math.Abs(medianResidual) < Epsilon)
            {
                break;
            }

            for (var i = 0; i < n; ++i)
            {
                var arg = residuals[i] / (6 * medianResidual);

                // A large deviation gets epsilon rather than zero: tiny but non-zero weights prevent singularities
                if (arg >= 1)
                {
                    robustWeights[i] = Epsilon;
                }
                else
                {
                    var bisquare = 1 - (arg * arg);
                    robustWeights[i] = bisquare * bisquare;
                }
            }
        }

        return Output(xv, yhat, ux, uy);
    }

    /// <summary>
    ///     Reads an element the way JavaScript does: NaN past either end.
    /// </summary>
    /// <param name="values">The array.</param>
    /// <param name="index">The index, possibly outside the array.</param>
    /// <returns>The element, or NaN (JavaScript's <c>undefined</c> in arithmetic).</returns>
    private static double At(double[] values, int index) => (uint)index < (uint)values.Length ? values[index] : double.NaN;

    /// <summary>
    ///     The weighting kernel of the local regression: <c>(1 - x³)³</c>.
    /// </summary>
    /// <param name="x">The scaled distance.</param>
    /// <returns>The weight.</returns>
    private static double Tricube(double x)
    {
        var t = 1 - (x * x * x);
        return t * t * t;
    }

    /// <summary>
    ///     Advances the window of nearest neighbors for the next point.
    /// </summary>
    /// <param name="xv">The sorted x.</param>
    /// <param name="i">The index of the next point.</param>
    /// <param name="left">The first index of the window; updated.</param>
    /// <param name="right">The last index of the window; updated.</param>
    private static void UpdateInterval(double[] xv, int i, ref int left, ref int right)
    {
        var val = At(xv, i);
        var next = right + 1;
        if (next >= xv.Length)
        {
            return;
        }

        // Step right while the distance to the new right edge is at most the distance to the old left edge (a step on equality moves over repeated x values);
        // a read past the end is NaN in JavaScript, which fails the comparison and stops the loop
        while (i > left && (At(xv, next) - val) <= (val - At(xv, left)))
        {
            left++;
            right = next;
            next++;
        }
    }

    /// <summary>
    ///     Gets the median of the numbers of an array (d3-array's <c>median</c>: NaN values are ignored).
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The median; NaN (JavaScript's <c>undefined</c>) when there is no number.</returns>
    private static double Median(double[] values)
    {
        var numbers = values.Where(v => !double.IsNaN(v)).ToArray();
        Array.Sort(numbers);
        return AggregateMeasures.Quantile(numbers, 0.5);
    }

    /// <summary>
    ///     Extracts the valid pairs, sorts them stably by x and centers them on their means (vega-statistics <c>points</c> with the sort flag).
    /// </summary>
    /// <param name="x">The x values.</param>
    /// <param name="y">The y values.</param>
    /// <returns>The centered x, the centered y, and the two means.</returns>
    private static (double[] X, double[] Y, double MeanX, double MeanY) SortedCenteredPoints(IReadOnlyList<DataValue> x, IReadOnlyList<DataValue> y)
    {
        var points = new List<(double X, double Y, int Order)>(x.Count);
        for (var i = 0; i < x.Count; i++)
        {
            if (x[i].IsNull || y[i].IsNull)
            {
                continue;
            }

            var u = ExpressionEvaluator.ToNumber(x[i]);
            var v = ExpressionEvaluator.ToNumber(y[i]);
            if (!double.IsNaN(u) && !double.IsNaN(v))
            {
                points.Add((u, v, i));
            }
        }

        // JavaScript's sort is stable, so equal x keep the order of the rows
        points.Sort((a, b) =>
        {
            var byX = a.X.CompareTo(b.X);
            return byX != 0 ? byX : a.Order.CompareTo(b.Order);
        });
        var n = points.Count;
        var xs = new double[n];
        var ys = new double[n];
        double ux = 0;
        double uy = 0;
        for (var i = 0; i < n; i++)
        {
            xs[i] = points[i].X;
            ys[i] = points[i].Y;
            ux += (xs[i] - ux) / (i + 1);
            uy += (ys[i] - uy) / (i + 1);
        }

        for (var i = 0; i < n; i++)
        {
            xs[i] -= ux;
            ys[i] -= uy;
        }

        return (xs, ys, ux, uy);
    }

    /// <summary>
    ///     Generates the smoothed points, averaging the points that share an x (Vega's <c>output</c>).
    /// </summary>
    /// <param name="xv">The sorted, centered x.</param>
    /// <param name="yhat">The fitted values (centered).</param>
    /// <param name="ux">The mean of x.</param>
    /// <param name="uy">The mean of y.</param>
    /// <returns>The points in the original coordinates.</returns>
    private static List<(double X, double Y)> Output(double[] xv, double[] yhat, double ux, double uy)
    {
        var output = new List<(double X, double Y)>();
        var count = 0;
        for (var i = 0; i < xv.Length; ++i)
        {
            var v = xv[i] + ux;
            if (output.Count > 0 && ExpressionEvaluator.NumbersEqual(output[^1].X, v))
            {
                // Average the output values by the online update
                var last = output[^1];
                output[^1] = (last.X, last.Y + ((yhat[i] - last.Y) / ++count));
            }
            else
            {
                // Add a new output point; the previous one gets its mean back
                count = 0;
                if (output.Count > 0)
                {
                    output[^1] = (output[^1].X, output[^1].Y + uy);
                }

                output.Add((v, yhat[i]));
            }
        }

        if (output.Count > 0)
        {
            output[^1] = (output[^1].X, output[^1].Y + uy);
        }

        return output;
    }
}
