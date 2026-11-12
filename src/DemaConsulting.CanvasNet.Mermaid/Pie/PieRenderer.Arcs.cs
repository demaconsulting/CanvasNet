// Portions derived from d3 (ISC); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Pie;

/// <summary>
///     An arc of a pie: the angles d3's <c>pie()</c> gives a value, in radians clockwise from twelve o'clock.
/// </summary>
/// <param name="StartAngle">The start angle.</param>
/// <param name="EndAngle">The end angle.</param>
internal readonly record struct PieArc(double StartAngle, double EndAngle);

/// <content>
///     The d3 functions of the pie chart: d3-shape's <c>pie()</c> (with <c>sort(null)</c> and no padding), <c>arc()</c>
///     without corners or padding and its <c>centroid</c>, d3-path's <c>arc</c> with the shape's 3 digits, and
///     JavaScript's <c>toFixed(0)</c>.
/// </content>
internal sealed partial class PieRenderer
{
    /// <summary>The epsilon of d3-shape's math (<c>1e-12</c>).</summary>
    private const double ShapeEpsilon = 1e-12;

    /// <summary>The epsilon of d3-path (<c>1e-6</c>).</summary>
    private const double PathEpsilon = 1e-6;

    /// <summary>A full turn.</summary>
    private const double Tau = 2 * Math.PI;

    /// <summary>
    ///     Computes the arcs of d3-shape's <c>pie()</c> with <c>sort(null)</c>, the start angle 0, the end angle τ and no
    ///     padding: each value's arc in the data's order, its angle the value's share of the sum of the positive values.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The arcs, in the order of the values.</returns>
    internal static IReadOnlyList<PieArc> Pie(IReadOnlyList<double> values)
    {
        var sum = 0.0;
        foreach (var v in values.Where(v => v > 0))
        {
            sum += v;
        }

        var arcs = new PieArc[values.Count];
        var k = sum != 0 ? Tau / sum : 0;
        var a0 = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            var a1 = a0 + (v > 0 ? v * k : 0);
            arcs[i] = new PieArc(a0, a1);
            a0 = a1;
        }

        return arcs;
    }

    /// <summary>
    ///     Builds the path of d3-shape's <c>arc()</c> for an arc with an inner and an outer radius, no corner radius and no
    ///     padding, as d3-path writes it with 3 digits.
    /// </summary>
    /// <param name="innerRadius">The inner radius.</param>
    /// <param name="outerRadius">The outer radius.</param>
    /// <param name="arc">The arc.</param>
    /// <returns>The path data and its segments.</returns>
    internal static (string Data, IReadOnlyList<SceneSegment> Segments) ArcPath(double innerRadius, double outerRadius, PieArc arc)
    {
        var path = new ArcPathBuilder();
        var r0 = innerRadius;
        var r1 = outerRadius;
        var a0 = arc.StartAngle - (Math.PI / 2);
        var a1 = arc.EndAngle - (Math.PI / 2);
        var da = Math.Abs(a1 - a0);
        var cw = a1 > a0;

        // The outer radius is the larger
        if (r1 < r0)
        {
            (r0, r1) = (r1, r0);
        }

        if (!(r1 > ShapeEpsilon))
        {
            // A point
            path.MoveTo(0, 0);
        }
        else if (da > Tau - ShapeEpsilon)
        {
            // A circle or an annulus
            path.MoveTo(r1 * Math.Cos(a0), r1 * Math.Sin(a0));
            path.Arc(r1, a0, a1, !cw);
            if (r0 > ShapeEpsilon)
            {
                path.MoveTo(r0 * Math.Cos(a1), r0 * Math.Sin(a1));
                path.Arc(r0, a1, a0, cw);
            }
        }
        else
        {
            // A circular or annular sector
            var x01 = r1 * Math.Cos(a0);
            var y01 = r1 * Math.Sin(a0);
            var x10 = r0 * Math.Cos(a1);
            var y10 = r0 * Math.Sin(a1);
            path.MoveTo(x01, y01);
            if (da > ShapeEpsilon)
            {
                path.Arc(r1, a0, a1, !cw);
            }

            if (!(r0 > ShapeEpsilon) || !(da > ShapeEpsilon))
            {
                path.LineTo(x10, y10);
            }
            else
            {
                path.Arc(r0, a1, a0, cw);
            }
        }

        path.ClosePath();
        return (path.Data, path.Segments);
    }

    /// <summary>
    ///     Computes d3-shape's <c>arc.centroid</c>: the point at the middle radius and the middle angle.
    /// </summary>
    /// <param name="innerRadius">The inner radius.</param>
    /// <param name="outerRadius">The outer radius.</param>
    /// <param name="arc">The arc.</param>
    /// <returns>The point.</returns>
    internal static (double X, double Y) Centroid(double innerRadius, double outerRadius, PieArc arc)
    {
        var r = (innerRadius + outerRadius) / 2;
        var a = ((arc.StartAngle + arc.EndAngle) / 2) - (Math.PI / 2);
        return (Math.Cos(a) * r, Math.Sin(a) * r);
    }

    /// <summary>
    ///     Formats a number as JavaScript's <c>toFixed(0)</c> does: the integer nearest to its exact value, a tie away
    ///     from zero; <c>NaN</c> and the infinities as <c>String()</c> writes them, and a magnitude of 1e21 or more too.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    internal static string ToFixed0(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) >= 1e21)
        {
            return YamlSubsetParser.FormatJsNumber(value);
        }

        // The fraction of a double below 2^52 is exact, so the tie test is exact
        var magnitude = Math.Abs(value);
        var floor = Math.Floor(magnitude);
        var rounded = magnitude - floor >= 0.5 ? floor + 1 : floor;
        var text = YamlSubsetParser.FormatJsNumber(rounded);
        return value < 0 ? "-" + text : text;
    }

    /// <summary>
    ///     d3-path's <c>Path</c> with 3 digits for the commands of an arc: the path text, and its segments with the
    ///     rounded numbers the browser draws.
    /// </summary>
    private sealed class ArcPathBuilder
    {
        /// <summary>The path text.</summary>
        private readonly StringBuilder _data = new();

        /// <summary>The segments.</summary>
        private readonly List<SceneSegment> _segments = [];

        /// <summary>The unrounded end of the current subpath, or null before the first command (d3-path's <c>_x1</c>).</summary>
        private (double X, double Y)? _current;

        /// <summary>The unrounded start of the current subpath (d3-path's <c>_x0</c>).</summary>
        private (double X, double Y) _start;

        /// <summary>Gets the path text.</summary>
        public string Data => _data.ToString();

        /// <summary>Gets the segments.</summary>
        public IReadOnlyList<SceneSegment> Segments => _segments;

        /// <summary>
        ///     Starts a subpath (<c>moveTo</c>).
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void MoveTo(double x, double y)
        {
            _start = (x, y);
            _current = (x, y);
            var (rx, ry) = (CurvePath.Round(x), CurvePath.Round(y));
            Append('M', rx, ry);
            _segments.Add(SceneSegment.Move(rx, ry));
        }

        /// <summary>
        ///     Draws a line (<c>lineTo</c>).
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void LineTo(double x, double y)
        {
            _current = (x, y);
            var (rx, ry) = (CurvePath.Round(x), CurvePath.Round(y));
            Append('L', rx, ry);
            _segments.Add(SceneSegment.Line(rx, ry));
        }

        /// <summary>
        ///     Draws an arc of a circle around the origin (<c>arc(0, 0, r, a0, a1, ccw)</c>): a line to its start when the
        ///     current point is elsewhere, then one elliptical arc, or two for a whole circle.
        /// </summary>
        /// <param name="r">The radius; not negative.</param>
        /// <param name="from">The start angle.</param>
        /// <param name="to">The end angle.</param>
        /// <param name="ccw">Whether the arc runs counterclockwise.</param>
        public void Arc(double r, double from, double to, bool ccw)
        {
            var dx = r * Math.Cos(from);
            var dy = r * Math.Sin(from);
            var x0 = dx;
            var y0 = dy;
            var sweep = !ccw;
            var da = ccw ? from - to : to - from;

            if (_current is not { } current)
            {
                MoveTo(x0, y0);
            }
            else if (Math.Abs(current.X - x0) > PathEpsilon || Math.Abs(current.Y - y0) > PathEpsilon)
            {
                LineTo(x0, y0);
            }

            if (r == 0)
            {
                return;
            }

            // An arc the wrong way round is turned into the right one
            if (da < 0)
            {
                da = (da % Tau) + Tau;
            }

            if (da > Tau - PathEpsilon)
            {
                // A whole circle: two half circles
                ArcTo(r, true, sweep, -dx, -dy);
                ArcTo(r, true, sweep, x0, y0);
            }
            else if (da > PathEpsilon)
            {
                ArcTo(r, da >= Math.PI, sweep, r * Math.Cos(to), r * Math.Sin(to));
            }
        }

        /// <summary>
        ///     Closes the subpath (<c>closePath</c>), when one was started.
        /// </summary>
        public void ClosePath()
        {
            if (_current is null)
            {
                return;
            }

            _current = _start;
            _data.Append('Z');
            _segments.Add(SceneSegment.Close());
        }

        /// <summary>
        ///     Writes an elliptical arc command of a circle.
        /// </summary>
        /// <param name="r">The radius.</param>
        /// <param name="large">The large-arc flag.</param>
        /// <param name="sweep">The sweep flag.</param>
        /// <param name="x">The end x.</param>
        /// <param name="y">The end y.</param>
        private void ArcTo(double r, bool large, bool sweep, double x, double y)
        {
            _current = (x, y);
            var (rr, rx, ry) = (CurvePath.Round(r), CurvePath.Round(x), CurvePath.Round(y));
            Append('A', rr, rr, 0, large ? 1 : 0, sweep ? 1 : 0, rx, ry);
            _segments.Add(SceneSegment.Arc(rr, rr, 0, large, sweep, rx, ry));
        }

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
    }
}
