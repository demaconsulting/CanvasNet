// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The spline curves of <see cref="CurveBuilder"/>: cardinal (open, closed), Catmull-Rom, monotone and natural.
/// </content>
internal static partial class CurveBuilder
{
    /// <summary>
    ///     Writes a segment of a cardinal spline (<c>point</c> of <c>cardinal.js</c>).
    /// </summary>
    /// <param name="sink">The path context.</param>
    /// <param name="k">The scale of the tangents, <c>(1 - tension) / 6</c>.</param>
    /// <param name="x0">The x coordinate of the point before the segment.</param>
    /// <param name="y0">The y coordinate of the point before the segment.</param>
    /// <param name="x1">The x coordinate of the start of the segment.</param>
    /// <param name="y1">The y coordinate of the start of the segment.</param>
    /// <param name="x2">The x coordinate of the end of the segment.</param>
    /// <param name="y2">The y coordinate of the end of the segment.</param>
    /// <param name="x">The x coordinate of the point after the segment.</param>
    /// <param name="y">The y coordinate of the point after the segment.</param>
    private static void CardinalSegment(SegmentSink sink, double k, double x0, double y0, double x1, double y1, double x2, double y2, double x, double y) =>
        sink.CubicTo(x1 + (k * (x2 - x0)), y1 + (k * (y2 - y0)), x2 + (k * (x1 - x)), y2 + (k * (y1 - y)), x2, y2);

    /// <summary>The <c>cardinal</c> curve; the ends are duplicated so that the spline reaches the first and last point.</summary>
    private sealed class CardinalCurve : Curve
    {
        /// <summary>The scale of the tangents.</summary>
        private readonly double _k;

        /// <summary>The last three points.</summary>
        private double _x0, _y0, _x1, _y1, _x2, _y2;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="CardinalCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="tension">The tension; 0 is the Catmull-Rom tangent scale of one sixth.</param>
        public CardinalCurve(SegmentSink sink, double tension)
            : base(sink) => _k = (1 - tension) / 6;

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (_point == 2)
            {
                Out.LineTo(_x2, _y2);
            }
            else if (_point == 3)
            {
                CardinalSegment(Out, _k, _x0, _y0, _x1, _y1, _x2, _y2, _x1, _y1);
            }

            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    Begin(x, y);
                    break;
                case 1:
                    _point = 2;
                    _x1 = x;
                    _y1 = y;
                    break;
                case 2:
                    _point = 3;
                    CardinalSegment(Out, _k, _x0, _y0, _x1, _y1, _x2, _y2, x, y);
                    break;
                default:
                    CardinalSegment(Out, _k, _x0, _y0, _x1, _y1, _x2, _y2, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = _x2;
            _x2 = x;
            _y0 = _y1;
            _y1 = _y2;
            _y2 = y;
        }
    }

    /// <summary>The <c>cardinalOpen</c> curve: it starts at the second point and ends at the second to last.</summary>
    private sealed class CardinalOpenCurve : Curve
    {
        /// <summary>The scale of the tangents.</summary>
        private readonly double _k;

        /// <summary>The last three points.</summary>
        private double _x0, _y0, _x1, _y1, _x2, _y2;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="CardinalOpenCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="tension">The tension.</param>
        public CardinalOpenCurve(SegmentSink sink, double tension)
            : base(sink) => _k = (1 - tension) / 6;

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (ClosesAt(_point, 3))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    _point = 3;
                    Begin(_x2, _y2);
                    break;
                default:
                    _point = 4;
                    CardinalSegment(Out, _k, _x0, _y0, _x1, _y1, _x2, _y2, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = _x2;
            _x2 = x;
            _y0 = _y1;
            _y1 = _y2;
            _y2 = y;
        }
    }

    /// <summary>The <c>cardinalClosed</c> curve (no area passes).</summary>
    private sealed class CardinalClosedCurve : Curve
    {
        /// <summary>The scale of the tangents.</summary>
        private readonly double _k;

        /// <summary>The last three points.</summary>
        private double _x0, _y0, _x1, _y1, _x2, _y2;

        /// <summary>The first three points of the loop, replayed at the end.</summary>
        private double _x3, _y3, _x4, _y4, _x5, _y5;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="CardinalClosedCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="tension">The tension.</param>
        public CardinalClosedCurve(SegmentSink sink, double tension)
            : base(sink) => _k = (1 - tension) / 6;

        /// <inheritdoc/>
        public override void AreaStart()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void AreaEnd()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _x2 = _x3 = _x4 = _x5 = double.NaN;
            _y0 = _y1 = _y2 = _y3 = _y4 = _y5 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            switch (_point)
            {
                case 1:
                    Out.MoveTo(_x3, _y3);
                    Out.Close();
                    break;
                case 2:
                    Out.LineTo(_x3, _y3);
                    Out.Close();
                    break;
                case 3:
                    // Replay the first three points to close the loop
                    Point(_x3, _y3);
                    Point(_x4, _y4);
                    Point(_x5, _y5);
                    break;
            }
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    _x3 = x;
                    _y3 = y;
                    break;
                case 1:
                    _point = 2;
                    _x4 = x;
                    _y4 = y;
                    Out.MoveTo(x, y);
                    break;
                case 2:
                    _point = 3;
                    _x5 = x;
                    _y5 = y;
                    break;
                default:
                    CardinalSegment(Out, _k, _x0, _y0, _x1, _y1, _x2, _y2, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = _x2;
            _x2 = x;
            _y0 = _y1;
            _y1 = _y2;
            _y2 = y;
        }
    }

    /// <summary>The <c>catmullRom</c> curve with <c>alpha</c> other than 0 (chordal parameterization).</summary>
    private sealed class CatmullRomCurve : Curve
    {
        /// <summary>The exponent of the chord length (0.5 is centripetal, 1 chordal).</summary>
        private readonly double _alpha;

        /// <summary>The last three points.</summary>
        private double _x0, _y0, _x1, _y1, _x2, _y2;

        /// <summary>The lengths <c>l^alpha</c> of the last three chords (0-1, 1-2, 2-3).</summary>
        private double _l01a, _l12a, _l23a;

        /// <summary>The lengths <c>l^(2 alpha)</c> of the last three chords.</summary>
        private double _l012a, _l122a, _l232a;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="CatmullRomCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="alpha">The exponent of the chord length; not 0.</param>
        public CatmullRomCurve(SegmentSink sink, double alpha)
            : base(sink) => _alpha = alpha;

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
            _l01a = _l12a = _l23a = _l012a = _l122a = _l232a = 0;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (_point == 2)
            {
                Out.LineTo(_x2, _y2);
            }
            else if (_point == 3)
            {
                Point(_x2, _y2);
            }

            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            // The length of the chord that ends at the new point
            if (_point != 0)
            {
                var x23 = _x2 - x;
                var y23 = _y2 - y;
                _l232a = Math.Pow((x23 * x23) + (y23 * y23), _alpha);
                _l23a = Math.Sqrt(_l232a);
            }

            switch (_point)
            {
                case 0:
                    _point = 1;
                    Begin(x, y);
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    _point = 3;
                    Segment(x, y);
                    break;
                default:
                    Segment(x, y);
                    break;
            }

            _l01a = _l12a;
            _l12a = _l23a;
            _l012a = _l122a;
            _l122a = _l232a;
            _x0 = _x1;
            _x1 = _x2;
            _x2 = x;
            _y0 = _y1;
            _y1 = _y2;
            _y2 = y;
        }

        /// <summary>
        ///     Writes a segment of the spline (<c>point</c> of <c>catmullRom.js</c>): the control points are the cardinal ones
        ///     corrected by the chord lengths, skipped for a chord that is shorter than epsilon.
        /// </summary>
        /// <param name="x">The x coordinate of the point after the segment.</param>
        /// <param name="y">The y coordinate of the point after the segment.</param>
        private void Segment(double x, double y)
        {
            var x1 = _x1;
            var y1 = _y1;
            var x2 = _x2;
            var y2 = _y2;
            if (_l01a > Epsilon)
            {
                var a = (2 * _l012a) + (3 * _l01a * _l12a) + _l122a;
                var n = 3 * _l01a * (_l01a + _l12a);
                x1 = ((x1 * a) - (_x0 * _l122a) + (_x2 * _l012a)) / n;
                y1 = ((y1 * a) - (_y0 * _l122a) + (_y2 * _l012a)) / n;
            }

            if (_l23a > Epsilon)
            {
                var b = (2 * _l232a) + (3 * _l23a * _l12a) + _l122a;
                var m = 3 * _l23a * (_l23a + _l12a);
                x2 = ((x2 * b) + (_x1 * _l232a) - (x * _l122a)) / m;
                y2 = ((y2 * b) + (_y1 * _l232a) - (y * _l122a)) / m;
            }

            Out.CubicTo(x1, y1, x2, y2, _x2, _y2);
        }
    }

    /// <summary>The <c>monotoneX</c> and <c>monotoneY</c> curves: cubic Hermite splines whose tangents (the monotone slope of d3-shape) keep the data monotone.</summary>
    private sealed class MonotoneCurve : Curve
    {
        /// <summary>Whether the curve is a function of y: the points go in swapped and the path comes out swapped.</summary>
        private readonly bool _reflect;

        /// <summary>The previous two points (in the coordinates of the curve).</summary>
        private double _x0, _y0, _x1, _y1;

        /// <summary>The tangent at the previous point.</summary>
        private double _t0;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="MonotoneCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="reflect">Whether the curve is <c>monotoneY</c>.</param>
        public MonotoneCurve(SegmentSink sink, bool reflect)
            : base(sink) => _reflect = reflect;

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _y0 = _y1 = _t0 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (_point == 2)
            {
                LineTo(_x1, _y1);
            }
            else if (_point == 3)
            {
                Segment(_t0, Slope2(_t0));
            }

            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            // The curve of y is the curve of x on the swapped points
            if (_reflect)
            {
                (x, y) = (y, x);
            }

            // Coincident points are ignored
            if (x.CompareTo(_x1) == 0 && y.CompareTo(_y1) == 0)
            {
                return;
            }

            var t1 = double.NaN;
            switch (_point)
            {
                case 0:
                    _point = 1;
                    if (Continues)
                    {
                        LineTo(x, y);
                    }
                    else
                    {
                        MoveTo(x, y);
                    }

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

            _x0 = _x1;
            _x1 = x;
            _y0 = _y1;
            _y1 = y;
            _t0 = t1;
        }

        /// <summary>
        ///     Gives the sign as d3 does: -1 for a negative number and 1 for everything else (0 and NaN included).
        /// </summary>
        /// <param name="value">The number.</param>
        /// <returns>-1 or 1.</returns>
        private static double Sign(double value) => value < 0 ? -1 : 1;

        /// <summary>
        ///     Gives the divisor of a slope the way <c>h0 || h1 &lt; 0 &amp;&amp; -0</c> does: a zero width takes the sign of the other.
        /// </summary>
        /// <param name="h">The width of the interval.</param>
        /// <param name="other">The width of the other interval.</param>
        /// <returns>The width, or a zero with the sign of the other width when the width is zero.</returns>
        private static double Divisor(double h, double other)
        {
            // A width that is neither zero nor NaN is used as it is
            if (Math.Abs(h) > 0)
            {
                return h;
            }

            return other < 0 ? double.NegativeZero : 0.0;
        }

        /// <summary>
        ///     Calculates the tangent at the previous point from both neighbor intervals (the monotone slope).
        /// </summary>
        /// <param name="x2">The x coordinate of the new point.</param>
        /// <param name="y2">The y coordinate of the new point.</param>
        /// <returns>The slope; 0 when it is not a number.</returns>
        private double Slope3(double x2, double y2)
        {
            var h0 = _x1 - _x0;
            var h1 = x2 - _x1;
            var s0 = (_y1 - _y0) / Divisor(h0, h1);
            var s1 = (y2 - _y1) / Divisor(h1, h0);
            var p = ((s0 * h1) + (s1 * h0)) / (h0 + h1);
            var result = (Sign(s0) + Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(p));

            // JavaScript's "|| 0" turns NaN and -0 into 0
            return Math.Abs(result) > 0 ? result : 0;
        }

        /// <summary>
        ///     Calculates the one-sided slope at the end of the line from the neighbor tangent.
        /// </summary>
        /// <param name="t">The tangent at the other end of the last interval.</param>
        /// <returns>The tangent at the previous point.</returns>
        private double Slope2(double t)
        {
            var h = _x1 - _x0;
            return Math.Abs(h) > 0 ? ((3 * (_y1 - _y0) / h) - t) / 2 : t;
        }

        /// <summary>
        ///     Writes the cubic Bezier segment of a cubic Hermite interval from the previous two points.
        /// </summary>
        /// <param name="t0">The tangent at the start of the interval.</param>
        /// <param name="t1">The tangent at the end of the interval.</param>
        private void Segment(double t0, double t1)
        {
            var dx = (_x1 - _x0) / 3;
            CubicTo(_x0 + dx, _y0 + (dx * t0), _x1 - dx, _y1 - (dx * t1), _x1, _y1);
        }

        /// <summary>Moves in the output coordinates (swapped for <c>monotoneY</c>).</summary>
        /// <param name="x">The x coordinate of the curve.</param>
        /// <param name="y">The y coordinate of the curve.</param>
        private void MoveTo(double x, double y)
        {
            var (px, py) = Reflect(x, y);
            Out.MoveTo(px, py);
        }

        /// <summary>Draws a line in the output coordinates (swapped for <c>monotoneY</c>).</summary>
        /// <param name="x">The x coordinate of the curve.</param>
        /// <param name="y">The y coordinate of the curve.</param>
        private void LineTo(double x, double y)
        {
            var (px, py) = Reflect(x, y);
            Out.LineTo(px, py);
        }

        /// <summary>Draws a cubic Bezier segment in the output coordinates (swapped for <c>monotoneY</c>).</summary>
        /// <param name="x1">The x coordinate of the first control point.</param>
        /// <param name="y1">The y coordinate of the first control point.</param>
        /// <param name="x2">The x coordinate of the second control point.</param>
        /// <param name="y2">The y coordinate of the second control point.</param>
        /// <param name="x">The x coordinate of the end point.</param>
        /// <param name="y">The y coordinate of the end point.</param>
        private void CubicTo(double x1, double y1, double x2, double y2, double x, double y)
        {
            var (p1x, p1y) = Reflect(x1, y1);
            var (p2x, p2y) = Reflect(x2, y2);
            var (px, py) = Reflect(x, y);
            Out.CubicTo(p1x, p1y, p2x, p2y, px, py);
        }

        /// <summary>Converts a point of the curve to the output coordinates.</summary>
        /// <param name="x">The x coordinate of the curve.</param>
        /// <param name="y">The y coordinate of the curve.</param>
        /// <returns>The point, swapped for <c>monotoneY</c>.</returns>
        private (double X, double Y) Reflect(double x, double y) => _reflect ? (y, x) : (x, y);
    }

    /// <summary>The <c>natural</c> curve: a cubic spline with zero second derivative at the ends, solved as a tri-diagonal system.</summary>
    private sealed class NaturalCurve : Curve
    {
        /// <summary>The buffered coordinates of the current line.</summary>
        private List<double> _x = [], _y = [];

        /// <summary>Initializes a new instance of the <see cref="NaturalCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public NaturalCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x = [];
            _y = [];
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            var n = _x.Count;
            if (n > 0)
            {
                Begin(_x[0], _y[0]);
                if (n == 2)
                {
                    Out.LineTo(_x[1], _y[1]);
                }
                else if (n > 2)
                {
                    // Two control points per interval from the tri-diagonal solves of x and y
                    var (ax, bx) = ControlPoints(_x);
                    var (ay, by) = ControlPoints(_y);
                    for (var i = 0; i < n - 1; i++)
                    {
                        Out.CubicTo(ax[i], ay[i], bx[i], by[i], _x[i + 1], _y[i + 1]);
                    }
                }
            }

            if (ClosesAt(n, 1))
            {
                Out.Close();
            }

            FlipPass();
            _x = [];
            _y = [];
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            _x.Add(x);
            _y.Add(y);
        }

        /// <summary>
        ///     Solves the tri-diagonal system of a natural cubic spline for the Bezier control points of one coordinate
        ///     (see https://www.particleincell.com/2012/bezier-splines/).
        /// </summary>
        /// <param name="x">The coordinates of at least three knots.</param>
        /// <returns>The first and the second control point coordinates of each of the <c>x.Count - 1</c> intervals.</returns>
        private static (double[] First, double[] Second) ControlPoints(List<double> x)
        {
            var n = x.Count - 1;
            var a = new double[n];
            var b = new double[n];
            var r = new double[n];

            // The rows of the tri-diagonal system: the first has the diagonal 2 and the right side x0 + 2 x1, the inner rows have
            // 1, 4, 1 and 4 xi + 2 x(i+1), the last has 2, 7 and 8 x(n-1) + xn
            a[0] = 0;
            b[0] = 2;
            r[0] = x[0] + (2 * x[1]);
            for (var i = 1; i < n - 1; i++)
            {
                a[i] = 1;
                b[i] = 4;
                r[i] = (4 * x[i]) + (2 * x[i + 1]);
            }

            a[n - 1] = 2;
            b[n - 1] = 7;
            r[n - 1] = (8 * x[n - 1]) + x[n];

            // Forward elimination, then back substitution: a becomes the first control points
            for (var i = 1; i < n; i++)
            {
                var m = a[i] / b[i - 1];
                b[i] -= m;
                r[i] -= m * r[i - 1];
            }

            a[n - 1] = r[n - 1] / b[n - 1];
            for (var i = n - 2; i >= 0; i--)
            {
                a[i] = (r[i] - a[i + 1]) / b[i];
            }

            // The second control points follow from the continuity of the second derivative
            b[n - 1] = (x[n] + a[n - 1]) / 2;
            for (var i = 0; i < n - 1; i++)
            {
                b[i] = (2 * x[i + 1]) - a[i + 1];
            }

            return (a, b);
        }
    }
}
