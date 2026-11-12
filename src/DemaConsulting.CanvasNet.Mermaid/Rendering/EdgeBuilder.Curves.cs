// Portions derived from d3-shape (ISC); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The drawing context a curve writes to (the subset of the canvas path API d3's curves call).
/// </summary>
internal interface ICurveContext
{
    /// <summary>Starts a subpath at a point.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    void MoveTo(double x, double y);

    /// <summary>Draws a line to a point.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    void LineTo(double x, double y);

    /// <summary>Draws a cubic Bezier curve.</summary>
    /// <param name="x1">The first control point's x.</param>
    /// <param name="y1">The first control point's y.</param>
    /// <param name="x2">The second control point's x.</param>
    /// <param name="y2">The second control point's y.</param>
    /// <param name="x">The end point's x.</param>
    /// <param name="y">The end point's y.</param>
    void BezierCurveTo(double x1, double y1, double x2, double y2, double x, double y);

    /// <summary>Closes the current subpath.</summary>
    void ClosePath();
}

/// <summary>
///     The path a d3 line generator builds (d3-path's <c>Path</c> with the line's default of 3 digits): the SVG path data
///     text with every number rounded to 3 decimals, and the same geometry as scene segments.
/// </summary>
/// <remarks>
///     The segments carry the rounded numbers, because the browser draws the path from its text. A number that is not
///     finite is written as JavaScript writes it (<c>NaN</c>), which ends the path for a browser; the segments stop
///     there too.
/// </remarks>
internal sealed class CurvePath : ICurveContext
{
    /// <summary>The path data text.</summary>
    private readonly StringBuilder _data = new();

    /// <summary>The segments.</summary>
    private readonly List<SceneSegment> _segments = [];

    /// <summary>Whether a number that is not finite was written (the rest of the path is not drawn).</summary>
    private bool _broken;

    /// <summary>Whether a subpath was started (d3-path's <c>_x1 !== null</c>).</summary>
    private bool _started;

    /// <summary>Gets the path data, or null when it is empty (d3's <c>buffer + "" || null</c>).</summary>
    public string? Data => _data.Length == 0 ? null : _data.ToString();

    /// <summary>Gets the segments that a browser draws from the path data.</summary>
    public IReadOnlyList<SceneSegment> Segments => _segments;

    /// <inheritdoc />
    public void MoveTo(double x, double y)
    {
        var (rx, ry) = (Round(x), Round(y));
        _started = true;
        Append('M', rx, ry);
        Add(SceneSegment.Move(rx, ry), rx, ry);
    }

    /// <inheritdoc />
    public void LineTo(double x, double y)
    {
        var (rx, ry) = (Round(x), Round(y));
        Append('L', rx, ry);
        Add(SceneSegment.Line(rx, ry), rx, ry);
    }

    /// <inheritdoc />
    public void BezierCurveTo(double x1, double y1, double x2, double y2, double x, double y)
    {
        var values = new[] { Round(x1), Round(y1), Round(x2), Round(y2), Round(x), Round(y) };
        Append('C', values);
        Add(SceneSegment.Cubic(values[0], values[1], values[2], values[3], values[4], values[5]), values);
    }

    /// <inheritdoc />
    public void ClosePath()
    {
        if (!_started)
        {
            return;
        }

        _data.Append('Z');
        if (!_broken)
        {
            _segments.Add(SceneSegment.Close());
        }
    }

    /// <summary>
    ///     Rounds a number to 3 decimals as d3-path's <c>appendRound</c> does (<c>Math.round(x * 1000) / 1000</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The rounded number.</returns>
    internal static double Round(double value) => Math.Floor((value * 1000) + 0.5) / 1000;

    /// <summary>
    ///     Writes a command and its numbers, separated by commas as d3-path writes them.
    /// </summary>
    /// <param name="command">The command letter.</param>
    /// <param name="values">The numbers.</param>
    private void Append(char command, params double[] values)
    {
        _data.Append(command);
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                _data.Append(',');
            }

            _data.Append(YamlSubsetParser.FormatJsNumber(values[i]));
        }
    }

    /// <summary>
    ///     Adds a segment unless the path is already broken by a number that is not finite.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="values">Its numbers.</param>
    private void Add(SceneSegment segment, params double[] values)
    {
        if (_broken || !Array.TrueForAll(values, double.IsFinite))
        {
            _broken = true;
            return;
        }

        _segments.Add(segment);
    }
}

/// <content>
///     The d3-shape curves Mermaid lists for edges (<c>basis</c>, <c>linear</c>, <c>natural</c>, <c>step</c>,
///     <c>stepBefore</c>, <c>stepAfter</c>, <c>monotoneX</c>, <c>monotoneY</c>, <c>cardinal</c>, <c>catmullRom</c>,
///     <c>bumpX</c> and <c>bumpY</c>) and d3's line generator, ported from d3-shape 3.2.0.
/// </content>
internal static partial class EdgeBuilder
{

    /// <summary>
    ///     A d3 curve: it receives the points of one line between <see cref="LineStart"/> and <see cref="LineEnd"/> and
    ///     writes the path to its context.
    /// </summary>
    internal interface ICurve
    {
        /// <summary>Starts a line.</summary>
        void LineStart();

        /// <summary>Ends a line.</summary>
        void LineEnd();

        /// <summary>Adds a point of the line.</summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        void Point(double x, double y);
    }

    /// <summary>
    ///     Draws points with d3's line generator and a named curve (no defined test, every point is drawn), as
    ///     <c>line().x(x).y(y).curve(curve)(data)</c> does.
    /// </summary>
    /// <param name="count">The number of points.</param>
    /// <param name="x">The x accessor of a point index.</param>
    /// <param name="y">The y accessor of a point index.</param>
    /// <param name="curve">The curve name (see <see cref="CreateCurve"/>).</param>
    /// <returns>The path.</returns>
    internal static CurvePath Line(int count, Func<int, double> x, Func<int, double> y, string curve)
    {
        var path = new CurvePath();
        if (count == 0)
        {
            return path;
        }

        var output = CreateCurve(curve, path);
        output.LineStart();
        for (var i = 0; i < count; i++)
        {
            output.Point(x(i), y(i));
        }

        output.LineEnd();
        return path;
    }

    /// <summary>
    ///     Draws points with d3's line generator and a named curve, without offsets.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="curve">The curve name.</param>
    /// <returns>The path.</returns>
    internal static CurvePath Line(IReadOnlyList<ShapePoint> points, string curve) =>
        Line(points.Count, i => points[i].X, i => points[i].Y, curve);

    /// <summary>
    ///     Creates the d3 curve of <c>insertEdge</c>'s switch: <c>rounded</c> is drawn linear (the rounding is done on the
    ///     points), and a name that is not listed draws <c>basis</c>.
    /// </summary>
    /// <param name="name">The curve name.</param>
    /// <param name="context">The context.</param>
    /// <returns>The curve.</returns>
    internal static ICurve CreateCurve(string name, ICurveContext context) => name switch
    {
        "linear" or "rounded" => new LinearCurve(context),
        "cardinal" => new CardinalCurve(context, 0),
        "bumpX" => new BumpCurve(context, true),
        "bumpY" => new BumpCurve(context, false),
        "catmullRom" => new CatmullRomCurve(context, 0.5),
        "monotoneX" => new MonotoneCurve(context),
        "monotoneY" => new MonotoneCurve(new ReflectContext(context)) { Reflect = true },
        "natural" => new NaturalCurve(context),
        "step" => new StepCurve(context, 0.5),
        "stepAfter" => new StepCurve(context, 1),
        "stepBefore" => new StepCurve(context, 0),
        _ => new BasisCurve(context),
    };

    /// <summary>
    ///     Gets JavaScript's truthiness of a number (not zero and not NaN).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is truthy.</returns>
    private static bool Truthy(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    ///     d3's <c>curveLinear</c>: straight lines between the points.
    /// </summary>
    /// <param name="context">The context.</param>
    private sealed class LinearCurve(ICurveContext context) : ICurve
    {
        /// <summary>The number of points so far (capped at 2).</summary>
        private int _point;

        /// <inheritdoc />
        public void LineStart() => _point = 0;

        /// <inheritdoc />
        public void LineEnd()
        {
            // The line generator's undefined _line closes a line of one point
            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            if (_point == 0)
            {
                _point = 1;
                context.MoveTo(x, y);
                return;
            }

            _point = 2;
            context.LineTo(x, y);
        }
    }

    /// <summary>
    ///     d3's <c>curveBasis</c>: a cubic basis spline through the points' convex hull, ending at the first and last point.
    /// </summary>
    /// <param name="context">The context.</param>
    private sealed class BasisCurve(ICurveContext context) : ICurve
    {
        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous points.</summary>
        private double _x0, _x1, _y0, _y1;

        /// <inheritdoc />
        public void LineStart()
        {
            _x0 = _x1 = _y0 = _y1 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            if (_point == 3)
            {
                Segment(_x1, _y1);
            }

            if (_point is 2 or 3)
            {
                context.LineTo(_x1, _y1);
            }

            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    context.MoveTo(x, y);
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    _point = 3;
                    context.LineTo(((5 * _x0) + _x1) / 6, ((5 * _y0) + _y1) / 6);
                    Segment(x, y);
                    break;
                default:
                    Segment(x, y);
                    break;
            }

            (_x0, _x1) = (_x1, x);
            (_y0, _y1) = (_y1, y);
        }

        /// <summary>
        ///     Writes one basis segment (d3's <c>point</c>).
        /// </summary>
        /// <param name="x">The x of the new point.</param>
        /// <param name="y">The y of the new point.</param>
        private void Segment(double x, double y) =>
            context.BezierCurveTo(
                ((2 * _x0) + _x1) / 3,
                ((2 * _y0) + _y1) / 3,
                (_x0 + (2 * _x1)) / 3,
                (_y0 + (2 * _y1)) / 3,
                (_x0 + (4 * _x1) + x) / 6,
                (_y0 + (4 * _y1) + y) / 6);
    }

    /// <summary>
    ///     d3's <c>curveStep</c>, <c>curveStepBefore</c> and <c>curveStepAfter</c>: horizontal then vertical steps.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="t">Where the step is between two points: 0.5 (step), 0 (before) or 1 (after).</param>
    private sealed class StepCurve(ICurveContext context, double t) : ICurve
    {
        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous point.</summary>
        private double _x, _y;

        /// <inheritdoc />
        public void LineStart()
        {
            _x = _y = double.NaN;
            _point = 0;
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            if (t > 0 && t < 1 && _point == 2)
            {
                context.LineTo(_x, _y);
            }

            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            if (_point == 0)
            {
                _point = 1;
                context.MoveTo(x, y);
            }
            else
            {
                _point = 2;
                if (t <= 0)
                {
                    context.LineTo(_x, y);
                    context.LineTo(x, y);
                }
                else
                {
                    var x1 = (_x * (1 - t)) + (x * t);
                    context.LineTo(x1, _y);
                    context.LineTo(x1, y);
                }
            }

            (_x, _y) = (x, y);
        }
    }

    /// <summary>
    ///     d3's <c>curveBumpX</c> and <c>curveBumpY</c>: a cubic curve between each two points with tangents along x or y.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="horizontal">Whether the tangents are horizontal (bumpX).</param>
    private sealed class BumpCurve(ICurveContext context, bool horizontal) : ICurve
    {
        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous point.</summary>
        private double _x0, _y0;

        /// <inheritdoc />
        public void LineStart() => _point = 0;

        /// <inheritdoc />
        public void LineEnd()
        {
            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            if (_point == 0)
            {
                _point = 1;
                context.MoveTo(x, y);
            }
            else
            {
                _point = 2;
                if (horizontal)
                {
                    _x0 = (_x0 + x) / 2;
                    context.BezierCurveTo(_x0, _y0, _x0, y, x, y);
                }
                else
                {
                    _y0 = (_y0 + y) / 2;
                    context.BezierCurveTo(_x0, _y0, x, _y0, x, y);
                }
            }

            (_x0, _y0) = (x, y);
        }
    }

    /// <summary>
    ///     d3's <c>curveNatural</c>: a natural cubic spline through every point.
    /// </summary>
    /// <param name="context">The context.</param>
    private sealed class NaturalCurve(ICurveContext context) : ICurve
    {
        /// <summary>The x of the points.</summary>
        private readonly List<double> _x = [];

        /// <summary>The y of the points.</summary>
        private readonly List<double> _y = [];

        /// <inheritdoc />
        public void LineStart()
        {
            _x.Clear();
            _y.Clear();
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            var n = _x.Count;
            if (n > 0)
            {
                context.MoveTo(_x[0], _y[0]);
                if (n == 2)
                {
                    context.LineTo(_x[1], _y[1]);
                }
                else if (n > 2)
                {
                    var (ax, bx) = ControlPoints(_x);
                    var (ay, by) = ControlPoints(_y);
                    for (int i0 = 0, i1 = 1; i1 < n; ++i0, ++i1)
                    {
                        context.BezierCurveTo(ax[i0], ay[i0], bx[i0], by[i0], _x[i1], _y[i1]);
                    }
                }
            }

            if (n == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            _x.Add(x);
            _y.Add(y);
        }

        /// <summary>
        ///     Solves the tridiagonal system of the control points (d3's <c>controlPoints</c>).
        /// </summary>
        /// <param name="x">The coordinates (at least three).</param>
        /// <returns>The first and second control coordinates of each segment.</returns>
        private static (double[] A, double[] B) ControlPoints(List<double> x)
        {
            var n = x.Count - 1;
            var a = new double[n];
            var b = new double[n];
            var r = new double[n];
            (a[0], b[0], r[0]) = (0, 2, x[0] + (2 * x[1]));
            for (var i = 1; i < n - 1; ++i)
            {
                (a[i], b[i], r[i]) = (1, 4, (4 * x[i]) + (2 * x[i + 1]));
            }

            (a[n - 1], b[n - 1], r[n - 1]) = (2, 7, (8 * x[n - 1]) + x[n]);
            for (var i = 1; i < n; ++i)
            {
                var m = a[i] / b[i - 1];
                b[i] -= m;
                r[i] -= m * r[i - 1];
            }

            a[n - 1] = r[n - 1] / b[n - 1];
            for (var i = n - 2; i >= 0; --i)
            {
                a[i] = (r[i] - a[i + 1]) / b[i];
            }

            b[n - 1] = (x[n] + a[n - 1]) / 2;
            for (var i = 0; i < n - 1; ++i)
            {
                b[i] = (2 * x[i + 1]) - a[i + 1];
            }

            return (a, b);
        }
    }

    /// <summary>
    ///     d3's <c>curveMonotoneX</c> (and, through a reflecting context, <c>curveMonotoneY</c>): a cubic Hermite spline
    ///     that keeps monotonicity (Steffen's method).
    /// </summary>
    /// <param name="context">The context.</param>
    private sealed class MonotoneCurve(ICurveContext context) : ICurve
    {
        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous points and tangent.</summary>
        private double _x0, _x1, _y0, _y1, _t0;

        /// <summary>Gets a value indicating whether x and y are swapped (monotoneY).</summary>
        public bool Reflect { get; init; }

        /// <inheritdoc />
        public void LineStart()
        {
            _x0 = _x1 = _y0 = _y1 = _t0 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            if (_point == 2)
            {
                context.LineTo(_x1, _y1);
            }
            else if (_point == 3)
            {
                Segment(_t0, Slope2(_t0));
            }

            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            if (Reflect)
            {
                (x, y) = (y, x);
            }

            var t1 = double.NaN;

            // Coincident points are ignored
            if (DagreLayout.Same(x, _x1) && DagreLayout.Same(y, _y1))
            {
                return;
            }

            switch (_point)
            {
                case 0:
                    _point = 1;
                    context.MoveTo(x, y);
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    _point = 3;
                    t1 = Slope3(x, y);
                    Segment(Slope2(t1), t1);
                    break;
                default:
                    t1 = Slope3(x, y);
                    Segment(_t0, t1);
                    break;
            }

            (_x0, _x1) = (_x1, x);
            (_y0, _y1) = (_y1, y);
            _t0 = t1;
        }

        /// <summary>
        ///     The sign of a number as d3 takes it (-1 for negatives, else 1).
        /// </summary>
        /// <param name="x">The number.</param>
        /// <returns>The sign.</returns>
        private static double Sign(double x) => x < 0 ? -1 : 1;

        /// <summary>
        ///     The tangent at the middle point of three (d3's <c>slope3</c>).
        /// </summary>
        /// <param name="x2">The x of the third point.</param>
        /// <param name="y2">The y of the third point.</param>
        /// <returns>The tangent.</returns>
        private double Slope3(double x2, double y2)
        {
            var h0 = _x1 - _x0;
            var h1 = x2 - _x1;

            // (h0 || h1 < 0 && -0): a zero step divides by a signed zero
            var s0 = (_y1 - _y0) / Denominator(h0, h1);
            var s1 = (y2 - _y1) / Denominator(h1, h0);
            var p = ((s0 * h1) + (s1 * h0)) / (h0 + h1);
            var result = (Sign(s0) + Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(p));
            return Truthy(result) ? result : 0;
        }

        /// <summary>
        ///     The divisor of a slope (d3's <c>h || other &lt; 0 &amp;&amp; -0</c>): the step, or a zero signed by the
        ///     other step.
        /// </summary>
        /// <param name="h">The step.</param>
        /// <param name="other">The other step.</param>
        /// <returns>The divisor.</returns>
        private static double Denominator(double h, double other)
        {
            if (Truthy(h))
            {
                return h;
            }

            return other < 0 ? -0.0 : 0.0;
        }

        /// <summary>
        ///     A one-sided tangent (d3's <c>slope2</c>).
        /// </summary>
        /// <param name="t">The other tangent.</param>
        /// <returns>The tangent.</returns>
        private double Slope2(double t)
        {
            var h = _x1 - _x0;
            return Truthy(h) ? (((3 * (_y1 - _y0)) / h) - t) / 2 : t;
        }

        /// <summary>
        ///     Writes the Hermite segment between the last two points as a Bezier curve (d3's <c>point</c>).
        /// </summary>
        /// <param name="t0">The tangent at the start.</param>
        /// <param name="t1">The tangent at the end.</param>
        private void Segment(double t0, double t1)
        {
            var dx = (_x1 - _x0) / 3;
            context.BezierCurveTo(_x0 + dx, _y0 + (dx * t0), _x1 - dx, _y1 - (dx * t1), _x1, _y1);
        }
    }

    /// <summary>
    ///     The context of <c>curveMonotoneY</c>, which swaps x and y on the way out (d3's <c>ReflectContext</c>).
    /// </summary>
    /// <param name="context">The real context.</param>
    private sealed class ReflectContext(ICurveContext context) : ICurveContext
    {
        /// <inheritdoc />
        public void MoveTo(double x, double y)
        {
            var (reflectedX, reflectedY) = (y, x);
            context.MoveTo(reflectedX, reflectedY);
        }

        /// <inheritdoc />
        public void LineTo(double x, double y)
        {
            var (reflectedX, reflectedY) = (y, x);
            context.LineTo(reflectedX, reflectedY);
        }

        /// <inheritdoc />
        public void BezierCurveTo(double x1, double y1, double x2, double y2, double x, double y)
        {
            var (rx1, ry1, rx2, ry2, rx, ry) = (y1, x1, y2, x2, y, x);
            context.BezierCurveTo(rx1, ry1, rx2, ry2, rx, ry);
        }

        /// <inheritdoc />
        public void ClosePath() => context.ClosePath();
    }

    /// <summary>
    ///     d3's <c>curveCardinal</c>: a cardinal spline with a tension (Mermaid uses the default of 0).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="tension">The tension.</param>
    private sealed class CardinalCurve(ICurveContext context, double tension) : ICurve
    {
        /// <summary>The tangent factor.</summary>
        private readonly double _k = (1 - tension) / 6;

        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous points.</summary>
        private double _x0, _x1, _x2, _y0, _y1, _y2;

        /// <inheritdoc />
        public void LineStart()
        {
            _x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            if (_point == 2)
            {
                context.LineTo(_x2, _y2);
            }
            else if (_point == 3)
            {
                Segment(_x1, _y1);
            }

            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    context.MoveTo(x, y);
                    break;
                case 1:
                    _point = 2;
                    (_x1, _y1) = (x, y);
                    break;
                default:
                    _point = 3;
                    Segment(x, y);
                    break;
            }

            (_x0, _x1, _x2) = (_x1, _x2, x);
            (_y0, _y1, _y2) = (_y1, _y2, y);
        }

        /// <summary>
        ///     Writes one cardinal segment (d3's <c>point</c>).
        /// </summary>
        /// <param name="x">The x of the next point.</param>
        /// <param name="y">The y of the next point.</param>
        private void Segment(double x, double y) =>
            context.BezierCurveTo(
                _x1 + (_k * (_x2 - _x0)),
                _y1 + (_k * (_y2 - _y0)),
                _x2 + (_k * (_x1 - x)),
                _y2 + (_k * (_y1 - y)),
                _x2,
                _y2);
    }

    /// <summary>
    ///     d3's <c>curveCatmullRom</c>: a centripetal Catmull-Rom spline (alpha 0.5).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="alpha">The alpha.</param>
    private sealed class CatmullRomCurve(ICurveContext context, double alpha) : ICurve
    {
        /// <summary>d3-shape's epsilon.</summary>
        private const double Epsilon = 1e-12;

        /// <summary>The state.</summary>
        private int _point;

        /// <summary>The previous points.</summary>
        private double _x0, _x1, _x2, _y0, _y1, _y2;

        /// <summary>The distances between the previous points to the powers of alpha.</summary>
        private double _l01A, _l12A, _l23A, _l012A, _l122A, _l232A;

        /// <inheritdoc />
        public void LineStart()
        {
            _x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
            _l01A = _l12A = _l23A = _l012A = _l122A = _l232A = 0;
            _point = 0;
        }

        /// <inheritdoc />
        public void LineEnd()
        {
            if (_point == 2)
            {
                context.LineTo(_x2, _y2);
            }
            else if (_point == 3)
            {
                Point(_x2, _y2);
            }

            if (_point == 1)
            {
                context.ClosePath();
            }
        }

        /// <inheritdoc />
        public void Point(double x, double y)
        {
            if (_point != 0)
            {
                var x23 = _x2 - x;
                var y23 = _y2 - y;
                _l232A = Math.Pow((x23 * x23) + (y23 * y23), alpha);
                _l23A = Math.Sqrt(_l232A);
            }

            switch (_point)
            {
                case 0:
                    _point = 1;
                    context.MoveTo(x, y);
                    break;
                case 1:
                    _point = 2;
                    break;
                default:
                    _point = 3;
                    Segment(x, y);
                    break;
            }

            (_l01A, _l12A) = (_l12A, _l23A);
            (_l012A, _l122A) = (_l122A, _l232A);
            (_x0, _x1, _x2) = (_x1, _x2, x);
            (_y0, _y1, _y2) = (_y1, _y2, y);
        }

        /// <summary>
        ///     Writes one Catmull-Rom segment (d3's <c>point</c>).
        /// </summary>
        /// <param name="x">The x of the next point.</param>
        /// <param name="y">The y of the next point.</param>
        private void Segment(double x, double y)
        {
            var (x1, y1, x2, y2) = (_x1, _y1, _x2, _y2);
            if (_l01A > Epsilon)
            {
                var a = (2 * _l012A) + (3 * _l01A * _l12A) + _l122A;
                var n = 3 * _l01A * (_l01A + _l12A);
                x1 = ((x1 * a) - (_x0 * _l122A) + (_x2 * _l012A)) / n;
                y1 = ((y1 * a) - (_y0 * _l122A) + (_y2 * _l012A)) / n;
            }

            if (_l23A > Epsilon)
            {
                var b = (2 * _l232A) + (3 * _l23A * _l12A) + _l122A;
                var m = 3 * _l23A * (_l23A + _l12A);
                x2 = ((x2 * b) + (_x1 * _l232A) - (x * _l122A)) / m;
                y2 = ((y2 * b) + (_y1 * _l232A) - (y * _l122A)) / m;
            }

            context.BezierCurveTo(x1, y1, x2, y2, _x2, _y2);
        }
    }

    /// <summary>
    ///     Formats a number as a JavaScript template literal does (used by the rounded curve's path text).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Js(double value) => YamlSubsetParser.FormatJsNumber(value);
}
