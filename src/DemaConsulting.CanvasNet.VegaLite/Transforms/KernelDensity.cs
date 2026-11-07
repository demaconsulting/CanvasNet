// Portions derived from Vega (BSD-3-Clause: vega-statistics kde, bandwidth, normal, quartiles); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     A Gaussian kernel density estimate over a support of values: the density, the cumulative distribution, and Scott's rule for
///     the bandwidth, as vega-statistics computes them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a port.</b> A density curve is drawn from these numbers, so the three details that decide them are kept as Vega
///         has them: (1) the support is the raw column, and every row counts toward <c>n</c> (the divisor), including rows whose
///         value is null (read as zero by JavaScript arithmetic, so a null adds a bump at zero) or text that is not a number
///         (NaN, which makes the whole curve NaN); (2) the cumulative distribution of the Gaussian kernel is the approximation
///         of West (2009), <i>Better approximations to cumulative normal functions</i>, a rational function with a different
///         branch above 7.07 deviations, not the error function; (3) a bandwidth of zero is estimated by Scott's rule,
///         <c>1.06 * min(sd, IQR / 1.34) * n^-0.2</c>, where a falsy minimum falls back to the deviation, then to the absolute
///         first quartile, then to 1.
///     </para>
///     <para>
///         The instance is immutable after construction and safe for concurrent reads; evaluating the density at one point costs
///         one kernel evaluation per row, which the caller charges to the step budget.
///     </para>
/// </remarks>
internal sealed class KernelDensity
{
    /// <summary>The support as numbers, one per row: JavaScript's <c>x - value</c> reads null as zero and every other value as its number.</summary>
    private readonly double[] _support;

    /// <summary>
    ///     Initializes a density estimate.
    /// </summary>
    /// <param name="support">The raw values of the column, one per row (null and non-numbers included).</param>
    /// <param name="bandwidth">The bandwidth, or zero, NaN or negative infinity to estimate it by Scott's rule.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="support"/> is null.</exception>
    public KernelDensity(IReadOnlyList<DataValue> support, double bandwidth)
    {
        ArgumentNullException.ThrowIfNull(support);
        _support = new double[support.Count];
        for (var i = 0; i < _support.Length; i++)
        {
            _support[i] = ExpressionEvaluator.ToNumber(support[i]);
        }

        Bandwidth = StatisticsPrimitives.IsTruthy(bandwidth) ? bandwidth : EstimateBandwidth(support);
    }

    /// <summary>Gets the bandwidth in use: the one given, or Scott's estimate.</summary>
    public double Bandwidth { get; }

    /// <summary>Gets the number of rows of the support (the divisor of the estimate).</summary>
    public int Count => _support.Length;

    /// <summary>
    ///     Estimates the bandwidth by Scott's rule (vega-statistics <c>bandwidth</c>).
    /// </summary>
    /// <param name="values">The raw values.</param>
    /// <returns><c>1.06 * v * n^-0.2</c> with <c>v = min(sd, (q3 - q1) / 1.34) || sd || |q1| || 1</c>; infinite for an empty support.</returns>
    public static double EstimateBandwidth(IReadOnlyList<DataValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var n = values.Count;
        var deviation = StatisticsPrimitives.Deviation(values);
        var quartiles = StatisticsPrimitives.Quantiles(values, [0.25, 0.5, 0.75]);
        var h = (quartiles[2] - quartiles[0]) / 1.34;
        var v = StatisticsPrimitives.JsMin(deviation, h);
        if (!StatisticsPrimitives.IsTruthy(v))
        {
            v = deviation;
        }

        if (!StatisticsPrimitives.IsTruthy(v))
        {
            v = Math.Abs(quartiles[0]);
        }

        if (!StatisticsPrimitives.IsTruthy(v))
        {
            v = 1;
        }

        return 1.06 * v * Math.Pow(n, -0.2);
    }

    /// <summary>
    ///     Evaluates the density at a point: the mean of the Gaussian kernels centered on the support, scaled by the bandwidth.
    /// </summary>
    /// <param name="x">The point.</param>
    /// <returns>The density; NaN when any support value is not a number or the support is empty.</returns>
    public double Pdf(double x)
    {
        double y = 0;
        for (var i = 0; i < _support.Length; i++)
        {
            y += NormalPdf((x - _support[i]) / Bandwidth);
        }

        return y / Bandwidth / _support.Length;
    }

    /// <summary>
    ///     Evaluates the cumulative distribution at a point: the mean of the Gaussian cumulative distributions centered on the support.
    /// </summary>
    /// <param name="x">The point.</param>
    /// <returns>The cumulative probability; NaN when any support value is not a number or the support is empty.</returns>
    public double Cdf(double x)
    {
        double y = 0;
        for (var i = 0; i < _support.Length; i++)
        {
            y += NormalCdf((x - _support[i]) / Bandwidth);
        }

        return y / _support.Length;
    }

    /// <summary>
    ///     Evaluates the standard normal density (vega-statistics <c>densityNormal</c> with the mean 0 and the deviation 1).
    /// </summary>
    /// <param name="z">The point.</param>
    /// <returns>The density.</returns>
    internal static double NormalPdf(double z) => Math.Exp(-0.5 * z * z) / StatisticsPrimitives.SqrtTwoPi;

    /// <summary>
    ///     Evaluates the standard normal cumulative distribution by the approximation of West (2009), as vega-statistics does
    ///     (<c>cumulativeNormal</c> with the mean 0 and the deviation 1).
    /// </summary>
    /// <remarks>
    ///     The function is a ratio of polynomials in |z| times <c>exp(-z²/2)</c> up to 7.07106781186547 deviations, a continued
    ///     fraction beyond, and zero beyond 37; the lower tail is the result and the upper tail is its complement.
    /// </remarks>
    /// <param name="z">The point.</param>
    /// <returns>The probability that a standard normal variable is at most <paramref name="z"/>.</returns>
    internal static double NormalCdf(double z)
    {
        var abs = Math.Abs(z);
        double cd;
        if (abs > 37)
        {
            cd = 0;
        }
        else
        {
            var exp = Math.Exp(-abs * abs / 2);
            double sum;
            if (abs < 7.07106781186547)
            {
                sum = (3.52624965998911e-02 * abs) + 0.700383064443688;
                sum = (sum * abs) + 6.37396220353165;
                sum = (sum * abs) + 33.912866078383;
                sum = (sum * abs) + 112.079291497871;
                sum = (sum * abs) + 221.213596169931;
                sum = (sum * abs) + 220.206867912376;
                cd = exp * sum;
                sum = (8.83883476483184e-02 * abs) + 1.75566716318264;
                sum = (sum * abs) + 16.064177579207;
                sum = (sum * abs) + 86.7807322029461;
                sum = (sum * abs) + 296.564248779674;
                sum = (sum * abs) + 637.333633378831;
                sum = (sum * abs) + 793.826512519948;
                sum = (sum * abs) + 440.413735824752;
                cd /= sum;
            }
            else
            {
                sum = abs + 0.65;
                sum = abs + (4 / sum);
                sum = abs + (3 / sum);
                sum = abs + (2 / sum);
                sum = abs + (1 / sum);
                cd = exp / sum / 2.506628274631;
            }
        }

        return z > 0 ? 1 - cd : cd;
    }
}
