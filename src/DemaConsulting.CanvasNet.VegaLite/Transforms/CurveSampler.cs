// Portions derived from Vega (BSD-3-Clause: vega-statistics sampleCurve); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Samples a function over an interval the way vega-statistics' <c>sampleCurve</c> does: on a uniform grid when the number of
///     steps is fixed, otherwise adaptively, subdividing where the curve bends.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a port, not a plain grid.</b> A regression curve or a density is drawn as a line through the samples, so the
///         number and position of the samples are visible. The adaptive scheme starts from <c>minSteps</c> evenly spaced points
///         and subdivides an interval at its midpoint when the angle between the chord to the midpoint and the chord to the
///         end differs by more than half a degree (measured in the unit square of the function's own extent) and the midpoint is
///         at least <c>span / maxSteps</c> from the start; it therefore draws a straight line with the minimum number of points
///         and a bending curve with up to <c>maxSteps</c>.
///     </para>
///     <para>
///         JavaScript arithmetic is kept: a NaN extent (the extent of nothing) gives NaN samples, a fractional step count is
///         legal, and the angle test is false for NaN. The point count is at most <c>maxSteps + minSteps + 2</c>, which the
///         caller checks against the row budget before sampling (<see cref="MaximumPoints"/>). The function is called once per
///         point, on the calling thread; the sampler checks no cancellation, so the cost of the function (a density over a
///         thousand rows, say) is the caller's to charge.
///     </para>
/// </remarks>
internal static class CurveSampler
{
    /// <summary>The default minimum number of steps (Vega's <c>25</c>).</summary>
    public const double DefaultMinSteps = 25;

    /// <summary>The default maximum number of steps (Vega's <c>200</c>).</summary>
    public const double DefaultMaxSteps = 200;

    /// <summary>The angle, in radians, below which a subdivision is skipped: half a degree.</summary>
    private static readonly double MinRadians = 0.5 * Math.PI / 180;

    /// <summary>
    ///     Gets the largest number of points that <see cref="Sample"/> can return for a step range.
    /// </summary>
    /// <param name="minSteps">The minimum steps as given (zero or NaN stands for the default).</param>
    /// <param name="maxSteps">The maximum steps as given (zero or NaN stands for the default).</param>
    /// <returns>The bound on the number of points; <see cref="double.PositiveInfinity"/> for an infinite step count.</returns>
    public static double MaximumPoints(double minSteps, double maxSteps)
    {
        var min = StatisticsPrimitives.IsTruthy(minSteps) ? minSteps : DefaultMinSteps;
        var max = StatisticsPrimitives.JsMax(min, StatisticsPrimitives.IsTruthy(maxSteps) ? maxSteps : DefaultMaxSteps);

        // The uniform grid has maxSteps + 1 points; the adaptive scheme has the minimum grid and at most one subdivision per interval of the finest resolution
        return Math.Ceiling(max) + Math.Ceiling(min) + 2;
    }

    /// <summary>
    ///     Samples a function over an extent.
    /// </summary>
    /// <param name="f">The function; it is called once for every point returned, and for the midpoints that the adaptive scheme tests.</param>
    /// <param name="minX">The start of the extent (NaN for an extent that does not exist).</param>
    /// <param name="maxX">The end of the extent.</param>
    /// <param name="minSteps">The minimum number of steps; zero or NaN means 25.</param>
    /// <param name="maxSteps">The maximum number of steps; zero or NaN means 200, and it is raised to <paramref name="minSteps"/> when smaller.</param>
    /// <param name="progress">Called before every refinement step with the number of points held so far, so that the caller can enforce its budget and its cancellation (a reversed extent can make the refinement of a curve with a kink unbounded); null for none.</param>
    /// <returns>The sampled points in increasing x for an increasing extent: the uniform grid (<c>maxSteps + 1</c> points) when the two step counts are equal, otherwise the adaptive sample.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="f"/> is null.</exception>
    public static List<(double X, double Y)> Sample(Func<double, double> f, double minX, double maxX, double minSteps, double maxSteps, Action<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        minSteps = StatisticsPrimitives.IsTruthy(minSteps) ? minSteps : DefaultMinSteps;
        maxSteps = StatisticsPrimitives.JsMax(minSteps, StatisticsPrimitives.IsTruthy(maxSteps) ? maxSteps : DefaultMaxSteps);
        var span = maxX - minX;
        var stop = span / maxSteps;
        var prev = new List<(double X, double Y)> { Point(f, minX) };
        var next = new List<(double X, double Y)>();

        // No adaptation: the uniform grid is sampled directly
        if (ExpressionEvaluator.NumbersEqual(minSteps, maxSteps))
        {
            for (var i = 1; i < maxSteps; ++i)
            {
                prev.Add(Point(f, minX + (i / minSteps * span)));
            }

            prev.Add(Point(f, maxX));
            return prev;
        }

        // The minimum points on a uniform grid, pushed so that the stack pops them from the start of the extent; then the adaptive refinement
        next.Add(Point(f, maxX));
        for (var i = minSteps; --i > 0;)
        {
            next.Add(Point(f, minX + (i / minSteps * span)));
        }

        var p0 = prev[0];
        var hasP1 = next.Count > 0;
        var p1 = hasP1 ? next[^1] : default;
        var sx = 1 / span;
        var sy = ScaleY(p0.Y, next);
        while (hasP1)
        {
            progress?.Invoke(prev.Count + next.Count);

            // The midpoint for a potential subdivision of the interval
            var pm = Point(f, (p0.X + p1.X) / 2);
            var farEnough = pm.X - p0.X >= stop;
            if (farEnough && AngleDelta(p0, pm, p1, sx, sy) > MinRadians)
            {
                // The resolution is not met and the midpoint differs enough from the chord: visit the midpoint first
                next.Add(pm);
            }
            else
            {
                // The midpoint is close to the chord: keep the end point and move on
                p0 = p1;
                prev.Add(p1);
                next.RemoveAt(next.Count - 1);
            }

            hasP1 = next.Count > 0;
            p1 = hasP1 ? next[^1] : default;
        }

        return prev;
    }

    /// <summary>
    ///     Creates a point of the function.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="x">The x.</param>
    /// <returns>The point <c>(x, f(x))</c>.</returns>
    private static (double X, double Y) Point(Func<double, double> f, double x) => (x, f(x));

    /// <summary>
    ///     Gets the factor that scales y to the unit interval of the y range of the initial points.
    /// </summary>
    /// <param name="init">The first y.</param>
    /// <param name="points">The initial points.</param>
    /// <returns><c>1 / (ymax - ymin)</c>; infinite when the function is constant, which makes every angle zero.</returns>
    private static double ScaleY(double init, List<(double X, double Y)> points)
    {
        var ymin = init;
        var ymax = init;
        foreach (var y in points.Select(point => point.Y))
        {
            if (y < ymin)
            {
                ymin = y;
            }

            if (y > ymax)
            {
                ymax = y;
            }
        }

        return 1 / (ymax - ymin);
    }

    /// <summary>
    ///     Gets the angle between the chord from one point to another and the chord from the first to a third.
    /// </summary>
    /// <param name="p">The common start.</param>
    /// <param name="q">The midpoint.</param>
    /// <param name="r">The end.</param>
    /// <param name="sx">The x scale.</param>
    /// <param name="sy">The y scale.</param>
    /// <returns>The absolute difference of the two angles, in radians.</returns>
    private static double AngleDelta((double X, double Y) p, (double X, double Y) q, (double X, double Y) r, double sx, double sy)
    {
        var a0 = Math.Atan2(sy * (r.Y - p.Y), sx * (r.X - p.X));
        var a1 = Math.Atan2(sy * (q.Y - p.Y), sx * (q.X - p.X));
        return Math.Abs(a0 - a1);
    }
}
