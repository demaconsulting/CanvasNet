// Portions derived from Vega (BSD-3-Clause: vega-statistics regression), d3-regression (BSD-3-Clause, Harry Stevens) and regression-js (MIT, Tom Alexander); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The result of fitting a regression model: the coefficients, the function that predicts a value, and the coefficient of
///     determination.
/// </summary>
/// <param name="Coefficients">The coefficients of the model: <c>[intercept, slope]</c> for the linear, logarithmic and power models (the power model's first coefficient is the multiplier), <c>[multiplier, rate]</c> for the exponential one, and the polynomial coefficients from the constant term up.</param>
/// <param name="Predict">The function that gives the fitted value for an x; it is only valid while the model that created it is.</param>
/// <param name="RSquared">The coefficient of determination: <c>1 - SSE / SST</c>; NaN when the values do not vary.</param>
internal sealed record RegressionModel(double[] Coefficients, Func<double, double> Predict, double RSquared);

/// <summary>
///     The regression models of Vega (<c>vega-statistics/src/regression</c>): constant, linear, logarithmic, exponential, power,
///     quadratic and polynomial, ported operation by operation so that the floating-point results agree with Vega's.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a literal port.</b> The fit of a trend line is part of what a chart shows, so the models keep Vega's order of
///         operations: running means (<c>X += (x - X) / n</c>) instead of sums, the mean-centered points of the exponential,
///         quadratic and polynomial models, the ordinary-least-squares slope that is zero when the variance of x is below
///         1e-24, the normal equations and the particular Gaussian elimination of the polynomial model (with its own pivoting,
///         which does not undo the column swaps), and the expansion of the centered polynomial back to the original x. The models
///         come from d3-regression by Harry Stevens, which adapted the polynomial from regression-js by Tom Alexander.
///     </para>
///     <para>
///         <b>Valid points.</b> A pair takes part when both values are not null and convert to numbers (JavaScript's unary plus
///         gives a number that is not NaN); the other pairs are ignored by every model. The polynomial of order zero is Vega's
///         <c>constant</c> model, which has its own rule (the x must only be not null, the y must not be NaN after conversion)
///         and a coefficient of determination of zero. The caller decides how many pairs are enough
///         (<see cref="DegreesOfFreedom"/>); a fit of too few points gives the NaN a JavaScript fit would give.
///     </para>
///     <para>
///         The functions are pure and thread-safe; the cost of a fit is proportional to the number of points times the
///         number of coefficients squared.
///     </para>
/// </remarks>
internal static class RegressionFit
{
    /// <summary>The variance of x below which the slope of ordinary least squares is taken as zero (a vertical or constant x).</summary>
    private const double SingularityLimit = 1e-24;

    /// <summary>The most coefficients (order + 1) a polynomial may have; the normal equations cost the cube of it.</summary>
    public const int MaxPolynomialOrder = 64;

    /// <summary>
    ///     Gets the number of coefficients that a method estimates, the count that the number of points must exceed.
    /// </summary>
    /// <param name="method">The method name.</param>
    /// <param name="order">The polynomial order.</param>
    /// <returns><paramref name="order"/> for <c>poly</c>, 2 for <c>quad</c> and 1 for every other method (Vega's <c>degreesOfFreedom</c>).</returns>
    public static int DegreesOfFreedom(string method, int order) => method switch
    {
        "poly" => order,
        "quad" => 2,
        _ => 1,
    };

    /// <summary>
    ///     Fits a model to the points.
    /// </summary>
    /// <param name="method">The method: <c>linear</c>, <c>log</c>, <c>exp</c>, <c>pow</c>, <c>quad</c>, <c>poly</c> or <c>constant</c>.</param>
    /// <param name="order">The polynomial order (<c>poly</c> only): 0 is the constant model, 1 the linear and 2 the quadratic.</param>
    /// <param name="x">The x of every row.</param>
    /// <param name="y">The y of every row, as many as <paramref name="x"/>.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentException">Thrown for a method that is not known, an order outside 0 to <see cref="MaxPolynomialOrder"/>, or lists of different lengths.</exception>
    public static RegressionModel Fit(string method, int order, IReadOnlyList<DataValue> x, IReadOnlyList<DataValue> y)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Count != y.Count)
        {
            throw new ArgumentException("The x and the y of a regression must have the same length.", nameof(y));
        }

        if (order is < 0 or > MaxPolynomialOrder)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, $"The order of a polynomial must be from 0 to {MaxPolynomialOrder}.");
        }

        // Vega's polynomial delegates the low orders to the dedicated models
        if (method == "constant" || (method == "poly" && order == 0))
        {
            return Constant(x, y);
        }

        var points = ValidPoints(x, y);
        return method switch
        {
            "linear" => Linear(points),
            "log" => Log(points),
            "exp" => Exp(points),
            "pow" => Pow(points),
            "quad" => Quad(points),
            "poly" when order == 1 => Linear(points),
            "poly" when order == 2 => Quad(points),
            "poly" => Poly(points, order),
            _ => throw new ArgumentException($"The regression method '{method}' is not known.", nameof(method)),
        };
    }

    /// <summary>
    ///     Extracts the valid points: the pairs whose x and y are not null and convert to numbers.
    /// </summary>
    /// <param name="x">The x values.</param>
    /// <param name="y">The y values.</param>
    /// <returns>The numbers of the valid pairs, in order.</returns>
    private static (double[] X, double[] Y) ValidPoints(IReadOnlyList<DataValue> x, IReadOnlyList<DataValue> y)
    {
        var xs = new List<double>(x.Count);
        var ys = new List<double>(x.Count);
        for (var i = 0; i < x.Count; i++)
        {
            if (x[i].IsNull || y[i].IsNull)
            {
                continue;
            }

            var u = ExpressionEvaluator.ToNumber(x[i]);
            var v = ExpressionEvaluator.ToNumber(y[i]);

            // `(u = +u) >= u` is false for NaN
            if (double.IsNaN(u) || double.IsNaN(v))
            {
                continue;
            }

            xs.Add(u);
            ys.Add(v);
        }

        return ([.. xs], [.. ys]);
    }

    /// <summary>
    ///     Ordinary least squares from the means (vega-statistics <c>ols</c>).
    /// </summary>
    /// <param name="uX">The mean of x.</param>
    /// <param name="uY">The mean of y.</param>
    /// <param name="uXY">The mean of the products.</param>
    /// <param name="uX2">The mean of the squares of x.</param>
    /// <returns><c>[intercept, slope]</c>; the slope is zero when the variance of x is below the singularity limit.</returns>
    internal static double[] Ols(double uX, double uY, double uXY, double uX2)
    {
        var delta = uX2 - (uX * uX);
        var slope = Math.Abs(delta) < SingularityLimit ? 0 : (uXY - (uX * uY)) / delta;
        var intercept = uY - (slope * uX);
        return [intercept, slope];
    }

    /// <summary>
    ///     Computes the coefficient of determination (<c>1 - SSE / SST</c>) over the valid points.
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <param name="meanY">The mean of y that the total sum of squares is taken around.</param>
    /// <param name="predict">The model.</param>
    /// <returns>The coefficient.</returns>
    private static double RSquared((double[] X, double[] Y) points, double meanY, Func<double, double> predict)
    {
        double sse = 0;
        double sst = 0;
        for (var i = 0; i < points.X.Length; i++)
        {
            var residual = points.Y[i] - predict(points.X[i]);
            var total = points.Y[i] - meanY;
            sse += residual * residual;
            sst += total * total;
        }

        return 1 - (sse / sst);
    }

    /// <summary>
    ///     The mean-centered points of the exponential, quadratic and polynomial models (vega-statistics <c>points</c>).
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The centered x, the centered y, the mean of x and the mean of y.</returns>
    private static (double[] X, double[] Y, double MeanX, double MeanY) Centered((double[] X, double[] Y) points)
    {
        var n = points.X.Length;
        var xs = new double[n];
        var ys = new double[n];
        double ux = 0;
        double uy = 0;
        for (var i = 0; i < n; i++)
        {
            xs[i] = points.X[i];
            ys[i] = points.Y[i];
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
    ///     Fits the constant model: the mean of y (Vega's <c>constant</c>, used for a polynomial of order zero).
    /// </summary>
    /// <param name="x">The x values.</param>
    /// <param name="y">The y values.</param>
    /// <returns>The model; its coefficient of determination is zero.</returns>
    private static RegressionModel Constant(IReadOnlyList<DataValue> x, IReadOnlyList<DataValue> y)
    {
        double mean = 0;
        long n = 0;
        for (var i = 0; i < x.Count; i++)
        {
            // This model checks the x only for null, and the y for null and for NaN after conversion
            if (x[i].IsNull || y[i].IsNull)
            {
                continue;
            }

            var value = ExpressionEvaluator.ToNumber(y[i]);
            if (double.IsNaN(value))
            {
                continue;
            }

            mean += (value - mean) / ++n;
        }

        var constant = mean;
        return new RegressionModel([constant], _ => constant, 0);
    }

    /// <summary>
    ///     Fits <c>y = a + b x</c> (vega-statistics <c>linear</c>).
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Linear((double[] X, double[] Y) points)
    {
        double sx = 0;
        double sy = 0;
        double sxy = 0;
        double sx2 = 0;
        long n = 0;
        for (var i = 0; i < points.X.Length; i++)
        {
            var dx = points.X[i];
            var dy = points.Y[i];
            ++n;
            sx += (dx - sx) / n;
            sy += (dy - sy) / n;
            sxy += ((dx * dy) - sxy) / n;
            sx2 += ((dx * dx) - sx2) / n;
        }

        var coefficients = Ols(sx, sy, sxy, sx2);
        double Predict(double v) => coefficients[0] + (coefficients[1] * v);
        return new RegressionModel(coefficients, Predict, RSquared(points, sy, Predict));
    }

    /// <summary>
    ///     Fits <c>y = a + b ln x</c> (vega-statistics <c>log</c>).
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Log((double[] X, double[] Y) points)
    {
        double sx = 0;
        double sy = 0;
        double sxy = 0;
        double sx2 = 0;
        long n = 0;
        for (var i = 0; i < points.X.Length; i++)
        {
            var dx = Math.Log(points.X[i]);
            var dy = points.Y[i];
            ++n;
            sx += (dx - sx) / n;
            sy += (dy - sy) / n;
            sxy += ((dx * dy) - sxy) / n;
            sx2 += ((dx * dx) - sx2) / n;
        }

        var coefficients = Ols(sx, sy, sxy, sx2);
        double Predict(double v) => coefficients[0] + (coefficients[1] * Math.Log(v));
        return new RegressionModel(coefficients, Predict, RSquared(points, sy, Predict));
    }

    /// <summary>
    ///     Fits <c>y = a e^(b x)</c> (vega-statistics <c>exp</c>), by least squares on the weighted logarithm.
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Exp((double[] X, double[] Y) points)
    {
        var (xv, _, ux, uy) = Centered(points);
        double yl = 0;
        double xy = 0;
        double xyl = 0;
        double x2y = 0;
        long n = 0;
        for (var i = 0; i < points.X.Length; i++)
        {
            var dy = points.Y[i];
            var dx = xv[n++];
            var ly = Math.Log(dy);
            var product = dx * dy;
            yl += ((dy * ly) - yl) / n;
            xy += (product - xy) / n;
            xyl += ((product * ly) - xyl) / n;
            x2y += ((dx * product) - x2y) / n;
        }

        var fit = Ols(xy / uy, yl / uy, xyl / uy, x2y / uy);
        var c0 = fit[0];
        var c1 = fit[1];
        double Predict(double v) => Math.Exp(c0 + (c1 * (v - ux)));
        return new RegressionModel([Math.Exp(c0 - (c1 * ux)), c1], Predict, RSquared(points, uy, Predict));
    }

    /// <summary>
    ///     Fits <c>y = a x^b</c> (vega-statistics <c>pow</c>), by least squares on the logarithms.
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Pow((double[] X, double[] Y) points)
    {
        double sx = 0;
        double sy = 0;
        double sxy = 0;
        double sx2 = 0;
        double ys = 0;
        long n = 0;
        for (var i = 0; i < points.X.Length; i++)
        {
            var dy = points.Y[i];
            var lx = Math.Log(points.X[i]);
            var ly = Math.Log(dy);
            ++n;
            sx += (lx - sx) / n;
            sy += (ly - sy) / n;
            sxy += ((lx * ly) - sxy) / n;
            sx2 += ((lx * lx) - sx2) / n;
            ys += (dy - ys) / n;
        }

        var coefficients = Ols(sx, sy, sxy, sx2);

        // The prediction reads the coefficient array, which is changed below: the multiplier is raised with exp after the model is built, as in Vega,
        // so the prediction (and the coefficient of determination) use the multiplier itself
        double Predict(double v) => coefficients[0] * Math.Pow(v, coefficients[1]);
        coefficients[0] = Math.Exp(coefficients[0]);
        return new RegressionModel(coefficients, Predict, RSquared(points, ys, Predict));
    }

    /// <summary>
    ///     Fits <c>y = a + b x + c x²</c> (vega-statistics <c>quad</c>) from the moments of the centered points.
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Quad((double[] X, double[] Y) points)
    {
        var (xv, yv, ux, uy) = Centered(points);
        var n = xv.Length;
        double x2s = 0;
        double x3s = 0;
        double x4s = 0;
        double xys = 0;
        double x2ys = 0;
        for (var i = 0; i < n;)
        {
            var dx = xv[i];
            var dy = yv[i++];
            var x2 = dx * dx;
            x2s += (x2 - x2s) / i;
            x3s += ((x2 * dx) - x3s) / i;
            x4s += ((x2 * x2) - x4s) / i;
            xys += ((dx * dy) - xys) / i;
            x2ys += ((x2 * dy) - x2ys) / i;
        }

        var x2x2 = x4s - (x2s * x2s);
        var d = (x2s * x2x2) - (x3s * x3s);
        var a = ((x2ys * x2s) - (xys * x3s)) / d;
        var b = ((xys * x2x2) - (x2ys * x3s)) / d;
        var c = -a * x2s;
        double Predict(double v)
        {
            v -= ux;
            return (a * v * v) + (b * v) + c + uy;
        }

        // The coefficients are transformed back from the mean-centered space
        return new RegressionModel(
            [c - (b * ux) + (a * ux * ux) + uy, b - (2 * a * ux), a],
            Predict,
            RSquared(points, uy, Predict));
    }

    /// <summary>
    ///     Fits a polynomial of order three or more (vega-statistics <c>poly</c>) by the normal equations on the centered points.
    /// </summary>
    /// <param name="points">The valid points.</param>
    /// <param name="order">The order, at least three.</param>
    /// <returns>The model.</returns>
    private static RegressionModel Poly((double[] X, double[] Y) points, int order)
    {
        var (xv, yv, ux, uy) = Centered(points);
        var n = xv.Length;
        var k = order + 1;

        // The normal equations: k rows of k sums of powers, then the right-hand side as the last "row" (Vega's layout, indexed matrix[column][row])
        var matrix = new double[k + 1][];
        var rhs = new double[k];
        for (var i = 0; i < k; i++)
        {
            double v = 0;
            for (var l = 0; l < n; l++)
            {
                v += Math.Pow(xv[l], i) * yv[l];
            }

            rhs[i] = v;
            var c = new double[k];
            for (var j = 0; j < k; j++)
            {
                v = 0;
                for (var l = 0; l < n; l++)
                {
                    v += Math.Pow(xv[l], i + j);
                }

                c[j] = v;
            }

            matrix[i] = c;
        }

        matrix[k] = rhs;
        var coefficients = GaussianElimination(matrix);
        double Predict(double v)
        {
            v -= ux;
            var y = uy + coefficients[0] + (coefficients[1] * v) + (coefficients[2] * v * v);
            for (var i = 3; i < k; i++)
            {
                y += coefficients[i] * Math.Pow(v, i);
            }

            return y;
        }

        return new RegressionModel(Uncenter(k, coefficients, -ux, uy), Predict, RSquared(points, uy, Predict));
    }

    /// <summary>
    ///     Expands a polynomial of the centered x back to the original x (vega-statistics <c>uncenter</c>).
    /// </summary>
    /// <param name="k">The number of coefficients.</param>
    /// <param name="a">The coefficients in the centered space.</param>
    /// <param name="x">The shift of x (the negated mean).</param>
    /// <param name="y">The shift of y (the mean).</param>
    /// <returns>The coefficients in the original space.</returns>
    private static double[] Uncenter(int k, double[] a, double x, double y)
    {
        var z = new double[k];
        for (var i = k - 1; i >= 0; --i)
        {
            var v = a[i];
            double c = 1;
            z[i] += v;
            for (var j = 1; j <= i; ++j)
            {
                // The binomial coefficient, built up one factor at a time
                c *= (i + 1.0 - j) / j;
                z[i - j] += v * Math.Pow(x, j) * c;
            }
        }

        // The bias term
        z[0] += y;
        return z;
    }

    /// <summary>
    ///     Solves the normal equations by Gaussian elimination exactly as vega-statistics does (including its pivoting, which swaps
    ///     columns of every row and does not undo the swaps).
    /// </summary>
    /// <param name="matrix">The matrix, <c>k + 1</c> arrays of <c>k</c> numbers; the last array is the right-hand side. It is modified.</param>
    /// <returns>The solution.</returns>
    private static double[] GaussianElimination(double[][] matrix)
    {
        var n = matrix.Length - 1;
        var coefficients = new double[n];
        for (var i = 0; i < n; ++i)
        {
            // The row with the largest entry becomes the pivot row
            var r = i;
            for (var j = i + 1; j < n; ++j)
            {
                if (Math.Abs(matrix[i][j]) > Math.Abs(matrix[i][r]))
                {
                    r = j;
                }
            }

            for (var k = i; k < n + 1; ++k)
            {
                (matrix[k][i], matrix[k][r]) = (matrix[k][r], matrix[k][i]);
            }

            for (var j = i + 1; j < n; ++j)
            {
                for (var k = n; k >= i; k--)
                {
                    matrix[k][j] -= matrix[k][i] * matrix[i][j] / matrix[i][i];
                }
            }
        }

        for (var j = n - 1; j >= 0; --j)
        {
            double t = 0;
            for (var k = j + 1; k < n; ++k)
            {
                t += matrix[k][j] * coefficients[k];
            }

            coefficients[j] = (matrix[n][j] - t) / matrix[j][j];
        }

        return coefficients;
    }
}
