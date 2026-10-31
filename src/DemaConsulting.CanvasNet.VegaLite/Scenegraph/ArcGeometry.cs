// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Builds the outline of a circular or annular sector: the geometry of an <c>arc</c> mark.
/// </summary>
/// <remarks>
///     <para>
///         <b>Origin.</b> <see cref="Build"/> is a port of the d3-shape <c>arc</c> generator (<c>arc.js</c>), which Vega's
///         arc mark uses, for an arc that is centered on the origin; the caller translates the path. Angles are in radians,
///         clockwise from 12 o'clock (the angle <c>a</c> of the generator is <c>angle - π/2</c> in the usual canvas
///         convention). The sector supports a pad angle (the gap between neighbors is constant in pixels, as the pad
///         radius is <c>sqrt(r0² + r1²)</c>) and rounded corners with the corner radius limited to half the ring width and
///         to what fits into the sector.
///     </para>
///     <para>
///         <b>Arcs.</b> The generator draws with the canvas call <c>arc(x, y, r, a0, a1, ccw)</c> of d3-path. The core
///         <see cref="PathBuilder"/> has no such call, so <see cref="ArcPathWriter"/> emulates it with an SVG elliptical arc
///         command: a move or line to the start of the arc when the current point is not coincident with it (within
///         1e-6), a single arc with the large-arc flag set for a sweep of at least π (the sweep flag is "clockwise"), and
///         two half circles for a full circle.
///     </para>
///     <para>
///         <b>Totality.</b> The method never throws and always returns in constant time. A radius below zero counts as
///         zero, a pad angle or corner radius below zero counts as zero, and an input that is not finite or has a magnitude
///         above 1e9 gives <see cref="Path.Empty"/>; so does an outline that would contain a coordinate that is not finite.
///         A sector with an outer radius of at most 1e-12 is a single point (a closed path with no area). The path has at
///         most about 20 commands.
///     </para>
/// </remarks>
internal static class ArcGeometry
{
    /// <summary>The tolerance of the d3-shape generator.</summary>
    private const double Epsilon = 1e-12;

    /// <summary>Half of pi.</summary>
    private const double HalfPi = Math.PI / 2;

    /// <summary>The full circle in radians.</summary>
    private const double Tau = 2 * Math.PI;

    /// <summary>The largest magnitude of an input; a larger one gives the empty path.</summary>
    private const double MaxMagnitude = 1e9;

    /// <summary>
    ///     Builds the outline of a sector centered on the origin.
    /// </summary>
    /// <param name="innerRadius">The inner radius in pixels; 0 gives a pie slice.</param>
    /// <param name="outerRadius">The outer radius in pixels; the two radii are swapped when the outer one is smaller.</param>
    /// <param name="startAngle">The start angle in radians, clockwise from 12 o'clock.</param>
    /// <param name="endAngle">The end angle in radians, clockwise from 12 o'clock; it may be smaller than the start angle.</param>
    /// <param name="padAngle">The angular padding in radians; a value below 0 counts as 0.</param>
    /// <param name="cornerRadius">The corner radius in pixels; a value below 0 counts as 0.</param>
    /// <returns>The path; empty for an input that is not finite or too large.</returns>
    public static Path Build(double innerRadius, double outerRadius, double startAngle, double endAngle, double padAngle, double cornerRadius)
    {
        if (!IsUsable(innerRadius) || !IsUsable(outerRadius) || !IsUsable(startAngle) || !IsUsable(endAngle)
            || !IsUsable(padAngle) || !IsUsable(cornerRadius))
        {
            return Path.Empty;
        }

        var writer = new ArcPathWriter();
        var r0 = Math.Max(0, innerRadius);
        var r1 = Math.Max(0, outerRadius);
        var a0 = startAngle - HalfPi;
        var a1 = endAngle - HalfPi;
        var da = Math.Abs(a1 - a0);
        var cw = a1 > a0;

        // The outer radius is always the larger one
        if (r1 < r0)
        {
            (r0, r1) = (r1, r0);
        }

        if (!(r1 > Epsilon))
        {
            // A point
            writer.MoveTo(0, 0);
        }
        else if (da > Tau - Epsilon)
        {
            // A circle or an annulus: two subpaths with opposite directions, so that the nonzero fill leaves the hole
            writer.MoveTo(r1 * Math.Cos(a0), r1 * Math.Sin(a0));
            writer.Arc(0, 0, r1, a0, a1, !cw);
            if (r0 > Epsilon)
            {
                writer.MoveTo(r0 * Math.Cos(a1), r0 * Math.Sin(a1));
                writer.Arc(0, 0, r0, a1, a0, cw);
            }
        }
        else
        {
            Sector(writer, new SectorSpec(r0, r1, a0, a1, da, cw, Math.Max(0, padAngle) / 2, Math.Max(0, cornerRadius)));
        }

        writer.Close();
        return writer.Build();
    }

    /// <summary>
    ///     Determines whether an input can be used.
    /// </summary>
    /// <param name="value">The input.</param>
    /// <returns><see langword="true"/> for a finite number whose magnitude is at most 1e9.</returns>
    private static bool IsUsable(double value) => double.IsFinite(value) && Math.Abs(value) <= MaxMagnitude;

    /// <summary>
    ///     Writes a circular or annular sector (the case that is neither a point nor a full circle).
    /// </summary>
    /// <param name="writer">The path writer.</param>
    /// <param name="spec">The sector.</param>
    private static void Sector(ArcPathWriter writer, SectorSpec spec)
    {
        var (r0, r1, a0, a1, da, cw) = (spec.R0, spec.R1, spec.A0, spec.A1, spec.Da, spec.Cw);
        var a01 = a0;
        var a11 = a1;
        var a00 = a0;
        var a10 = a1;
        var da0 = da;
        var da1 = da;
        var ap = spec.HalfPad;
        var rp = ap > Epsilon ? Math.Sqrt((r0 * r0) + (r1 * r1)) : 0;
        var rc = Math.Min(Math.Abs(r1 - r0) / 2, spec.CornerRadius);
        var rc0 = rc;
        var rc1 = rc;

        // Apply padding; since r1 >= r0, da1 >= da0
        if (rp > Epsilon)
        {
            // With no inner radius rp / r0 is infinite and the clamped asin gives a quarter turn, which collapses the
            // inner ring to a point at the origin exactly as d3-shape does
            var p0 = ClampedAsin(rp / r0 * Math.Sin(ap));
            var p1 = ClampedAsin(rp / r1 * Math.Sin(ap));
            da0 -= p0 * 2;
            if (da0 > Epsilon)
            {
                p0 *= cw ? 1 : -1;
                a00 += p0;
                a10 -= p0;
            }
            else
            {
                da0 = 0;
                a00 = a10 = (a0 + a1) / 2;
            }

            da1 -= p1 * 2;
            if (da1 > Epsilon)
            {
                p1 *= cw ? 1 : -1;
                a01 += p1;
                a11 -= p1;
            }
            else
            {
                da1 = 0;
                a01 = a11 = (a0 + a1) / 2;
            }
        }

        var x01 = r1 * Math.Cos(a01);
        var y01 = r1 * Math.Sin(a01);
        var x10 = r0 * Math.Cos(a10);
        var y10 = r0 * Math.Sin(a10);
        var x11 = r1 * Math.Cos(a11);
        var y11 = r1 * Math.Sin(a11);
        var x00 = r0 * Math.Cos(a00);
        var y00 = r0 * Math.Sin(a00);

        // Restrict the corner radius according to the sector angle; when the corner circles do not fit, the arc is
        // probably too small, so the corner radius is disabled
        if (rc > Epsilon && da < Math.PI)
        {
            if (Intersect(x01, y01, x00, y00, x11, y11, x10, y10, out var ocx, out var ocy))
            {
                var ax = x01 - ocx;
                var ay = y01 - ocy;
                var bx = x11 - ocx;
                var by = y11 - ocy;
                var kc = 1 / Math.Sin(ClampedAcos(((ax * bx) + (ay * by)) / (Math.Sqrt((ax * ax) + (ay * ay)) * Math.Sqrt((bx * bx) + (by * by)))) / 2);
                var lc = Math.Sqrt((ocx * ocx) + (ocy * ocy));
                rc0 = Math.Min(rc, (r0 - lc) / (kc - 1));
                rc1 = Math.Min(rc, (r1 - lc) / (kc + 1));

                // A degenerate corner (a zero-length edge or a zero angle) gives a quotient that is not finite: no corner
                if (!double.IsFinite(rc0))
                {
                    rc0 = 0;
                }

                if (!double.IsFinite(rc1))
                {
                    rc1 = 0;
                }
            }
            else
            {
                rc0 = rc1 = 0;
            }
        }

        // The outer ring: a point when the sector collapsed to a line, with rounded corners, or a circular arc
        CornerCircle? o0 = null;
        CornerCircle? o1 = null;
        if (da1 > Epsilon && rc1 > Epsilon)
        {
            o0 = CornerTangents(x00, y00, x01, y01, r1, rc1, cw);
            o1 = CornerTangents(x11, y11, x10, y10, r1, rc1, cw);
            if (!o0.Value.IsFinite || !o1.Value.IsFinite)
            {
                o0 = o1 = null;
            }
        }

        if (!(da1 > Epsilon))
        {
            writer.MoveTo(x01, y01);
        }
        else if (o0 is { } t0 && o1 is { } t1)
        {
            writer.MoveTo(t0.Cx + t0.X01, t0.Cy + t0.Y01);
            if (rc1 < rc)
            {
                // The corners merged
                writer.Arc(t0.Cx, t0.Cy, rc1, Math.Atan2(t0.Y01, t0.X01), Math.Atan2(t1.Y01, t1.X01), !cw);
            }
            else
            {
                writer.Arc(t0.Cx, t0.Cy, rc1, Math.Atan2(t0.Y01, t0.X01), Math.Atan2(t0.Y11, t0.X11), !cw);
                writer.Arc(0, 0, r1, Math.Atan2(t0.Cy + t0.Y11, t0.Cx + t0.X11), Math.Atan2(t1.Cy + t1.Y11, t1.Cx + t1.X11), !cw);
                writer.Arc(t1.Cx, t1.Cy, rc1, Math.Atan2(t1.Y11, t1.X11), Math.Atan2(t1.Y01, t1.X01), !cw);
            }
        }
        else
        {
            writer.MoveTo(x01, y01);
            writer.Arc(0, 0, r1, a01, a11, !cw);
        }

        // The inner ring: a point (no inner radius, or collapsed by the padding), with rounded corners, or a circular arc
        CornerCircle? i0 = null;
        CornerCircle? i1 = null;
        if (r0 > Epsilon && da0 > Epsilon && rc0 > Epsilon)
        {
            i0 = CornerTangents(x10, y10, x11, y11, r0, -rc0, cw);
            i1 = CornerTangents(x01, y01, x00, y00, r0, -rc0, cw);
            if (!i0.Value.IsFinite || !i1.Value.IsFinite)
            {
                i0 = i1 = null;
            }
        }

        if (!(r0 > Epsilon) || !(da0 > Epsilon))
        {
            writer.LineTo(x10, y10);
        }
        else if (i0 is { } u0 && i1 is { } u1)
        {
            writer.LineTo(u0.Cx + u0.X01, u0.Cy + u0.Y01);
            if (rc0 < rc)
            {
                writer.Arc(u0.Cx, u0.Cy, rc0, Math.Atan2(u0.Y01, u0.X01), Math.Atan2(u1.Y01, u1.X01), !cw);
            }
            else
            {
                writer.Arc(u0.Cx, u0.Cy, rc0, Math.Atan2(u0.Y01, u0.X01), Math.Atan2(u0.Y11, u0.X11), !cw);
                writer.Arc(0, 0, r0, Math.Atan2(u0.Cy + u0.Y11, u0.Cx + u0.X11), Math.Atan2(u1.Cy + u1.Y11, u1.Cx + u1.X11), cw);
                writer.Arc(u1.Cx, u1.Cy, rc0, Math.Atan2(u1.Y11, u1.X11), Math.Atan2(u1.Y01, u1.X01), !cw);
            }
        }
        else
        {
            writer.Arc(0, 0, r0, a10, a00, cw);
        }
    }

    /// <summary>
    ///     The arc sine of d3-shape: an argument beyond the range of the function gives a quarter turn instead of NaN.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <returns>The angle in radians.</returns>
    private static double ClampedAsin(double x)
    {
        if (x >= 1)
        {
            return HalfPi;
        }

        return x <= -1 ? -HalfPi : Math.Asin(x);
    }

    /// <summary>
    ///     The arc cosine of d3-shape: an argument beyond the range of the function gives 0 or π instead of NaN.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <returns>The angle in radians.</returns>
    private static double ClampedAcos(double x)
    {
        if (x > 1)
        {
            return 0;
        }

        return x < -1 ? Math.PI : Math.Acos(x);
    }

    /// <summary>
    ///     Finds the intersection of the line through two points with the line through two other points.
    /// </summary>
    /// <param name="x0">The x coordinate of the first point of the first line.</param>
    /// <param name="y0">The y coordinate of the first point of the first line.</param>
    /// <param name="x1">The x coordinate of the second point of the first line.</param>
    /// <param name="y1">The y coordinate of the second point of the first line.</param>
    /// <param name="x2">The x coordinate of the first point of the second line.</param>
    /// <param name="y2">The y coordinate of the first point of the second line.</param>
    /// <param name="x3">The x coordinate of the second point of the second line.</param>
    /// <param name="y3">The y coordinate of the second point of the second line.</param>
    /// <param name="x">The x coordinate of the intersection.</param>
    /// <param name="y">The y coordinate of the intersection.</param>
    /// <returns><see langword="false"/> when the lines are (nearly) parallel.</returns>
    private static bool Intersect(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3, out double x, out double y)
    {
        var x10 = x1 - x0;
        var y10 = y1 - y0;
        var x32 = x3 - x2;
        var y32 = y3 - y2;
        var t = (y32 * x10) - (x32 * y10);
        if (t * t < Epsilon)
        {
            x = y = 0;
            return false;
        }

        t = ((x32 * (y0 - y2)) - (y32 * (x0 - x2))) / t;
        x = x0 + (t * x10);
        y = y0 + (t * y10);
        return true;
    }

    /// <summary>
    ///     Computes the center of a corner circle and the offsets of its tangent points (the perpendicular offset line of
    ///     length <paramref name="rc"/>; see the Wolfram MathWorld page on circle-line intersection).
    /// </summary>
    /// <param name="x0">The x coordinate of the first end of the edge.</param>
    /// <param name="y0">The y coordinate of the first end of the edge.</param>
    /// <param name="x1">The x coordinate of the second end of the edge.</param>
    /// <param name="y1">The y coordinate of the second end of the edge.</param>
    /// <param name="r1">The radius of the ring the corner is on.</param>
    /// <param name="rc">The corner radius; negative for an inner corner.</param>
    /// <param name="cw">Whether the sector runs clockwise.</param>
    /// <returns>The corner circle.</returns>
    private static CornerCircle CornerTangents(double x0, double y0, double x1, double y1, double r1, double rc, bool cw)
    {
        var x01 = x0 - x1;
        var y01 = y0 - y1;
        var lo = (cw ? rc : -rc) / Math.Sqrt((x01 * x01) + (y01 * y01));
        var ox = lo * y01;
        var oy = -lo * x01;
        var x11 = x0 + ox;
        var y11 = y0 + oy;
        var x10 = x1 + ox;
        var y10 = y1 + oy;
        var x00 = (x11 + x10) / 2;
        var y00 = (y11 + y10) / 2;
        var dx = x10 - x11;
        var dy = y10 - y11;
        var d2 = (dx * dx) + (dy * dy);
        var r = r1 - rc;
        var determinant = (x11 * y10) - (x10 * y11);
        var d = (dy < 0 ? -1 : 1) * Math.Sqrt(Math.Max(0, (r * r * d2) - (determinant * determinant)));
        var cx0 = ((determinant * dy) - (dx * d)) / d2;
        var cy0 = ((-determinant * dx) - (dy * d)) / d2;
        var cx1 = ((determinant * dy) + (dx * d)) / d2;
        var cy1 = ((-determinant * dx) + (dy * d)) / d2;
        var dx0 = cx0 - x00;
        var dy0 = cy0 - y00;
        var dx1 = cx1 - x00;
        var dy1 = cy1 - y00;

        // Pick the closer of the two intersection points
        if (((dx0 * dx0) + (dy0 * dy0)) > ((dx1 * dx1) + (dy1 * dy1)))
        {
            cx0 = cx1;
            cy0 = cy1;
        }

        return new CornerCircle(cx0, cy0, -ox, -oy, cx0 * ((r1 / r) - 1), cy0 * ((r1 / r) - 1));
    }

    /// <summary>
    ///     The input of the sector case.
    /// </summary>
    /// <param name="R0">The inner radius.</param>
    /// <param name="R1">The outer radius.</param>
    /// <param name="A0">The start angle in the canvas convention.</param>
    /// <param name="A1">The end angle in the canvas convention.</param>
    /// <param name="Da">The absolute angle between the two.</param>
    /// <param name="Cw">Whether the end angle is larger than the start angle.</param>
    /// <param name="HalfPad">Half of the pad angle.</param>
    /// <param name="CornerRadius">The requested corner radius.</param>
    private readonly record struct SectorSpec(double R0, double R1, double A0, double A1, double Da, bool Cw, double HalfPad, double CornerRadius);

    /// <summary>
    ///     A corner circle.
    /// </summary>
    /// <param name="Cx">The x coordinate of the center.</param>
    /// <param name="Cy">The y coordinate of the center.</param>
    /// <param name="X01">The x offset of the tangent point on the edge.</param>
    /// <param name="Y01">The y offset of the tangent point on the edge.</param>
    /// <param name="X11">The x offset of the tangent point on the ring.</param>
    /// <param name="Y11">The y offset of the tangent point on the ring.</param>
    private readonly record struct CornerCircle(double Cx, double Cy, double X01, double Y01, double X11, double Y11)
    {
        /// <summary>Gets a value indicating whether every member is finite.</summary>
        public bool IsFinite => double.IsFinite(Cx) && double.IsFinite(Cy) && double.IsFinite(X01) && double.IsFinite(Y01)
            && double.IsFinite(X11) && double.IsFinite(Y11);
    }
}

/// <summary>
///     Records a path with the canvas calls of d3-path (<c>moveTo</c>, <c>lineTo</c>, <c>arc</c>, <c>closePath</c>) on a core
///     <see cref="PathBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         The writer works in double precision and converts to the core path's single precision when it records a
///         command. It keeps d3-path's current point and start point. The canvas <c>arc</c> is emulated with an SVG arc
///         command (see <see cref="ArcGeometry"/>). A call after <see cref="Close"/> continues at the start of the closed
///         subpath, as in d3-path.
///     </para>
///     <para>
///         A writer is total: a coordinate that is not finite, or a negative radius, makes it invalid, ignores the remaining
///         calls and makes <see cref="Build"/> return <see cref="Path.Empty"/>. A writer is not thread safe and is meant to
///         build one path.
///     </para>
/// </remarks>
internal sealed class ArcPathWriter
{
    /// <summary>The tolerance of d3-path.</summary>
    private const double PathEpsilon = 1e-6;

    /// <summary>The full circle in radians.</summary>
    private const double Tau = 2 * Math.PI;

    /// <summary>The smallest sweep that d3-path treats as a full circle.</summary>
    private const double TauEpsilon = Tau - PathEpsilon;

    /// <summary>The core builder.</summary>
    private readonly PathBuilder _builder = new();

    /// <summary>The x coordinate of the start of the current subpath.</summary>
    private double _x0;

    /// <summary>The y coordinate of the start of the current subpath.</summary>
    private double _y0;

    /// <summary>The x coordinate of the current point.</summary>
    private double _x1;

    /// <summary>The y coordinate of the current point.</summary>
    private double _y1;

    /// <summary>Whether a subpath was started (d3-path's current point is not null).</summary>
    private bool _hasCurrent;

    /// <summary>Whether the core builder has an open subpath (false after a close, until the next move).</summary>
    private bool _isOpen;

    /// <summary>Whether every call so far was valid.</summary>
    private bool _isValid = true;

    /// <summary>
    ///     Starts a new subpath.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void MoveTo(double x, double y)
    {
        if (!Accept(x, y))
        {
            return;
        }

        _builder.MoveTo(new Vector2((float)x, (float)y));
        _x0 = _x1 = x;
        _y0 = _y1 = y;
        _hasCurrent = true;
        _isOpen = true;
    }

    /// <summary>
    ///     Draws a straight line from the current point.
    /// </summary>
    /// <param name="x">The x coordinate of the end.</param>
    /// <param name="y">The y coordinate of the end.</param>
    public void LineTo(double x, double y)
    {
        if (!Accept(x, y))
        {
            return;
        }

        if (!_hasCurrent)
        {
            MoveTo(x, y);
            return;
        }

        EnsureOpen();
        _builder.LineTo(new Vector2((float)x, (float)y));
        _x1 = x;
        _y1 = y;
    }

    /// <summary>
    ///     Closes the current subpath; nothing happens before the first move.
    /// </summary>
    public void Close()
    {
        if (!_isValid || !_hasCurrent || !_isOpen)
        {
            return;
        }

        _builder.Close();
        _isOpen = false;
        _x1 = _x0;
        _y1 = _y0;
    }

    /// <summary>
    ///     Draws a circular arc as the canvas <c>arc</c> call of d3-path does.
    /// </summary>
    /// <param name="x">The x coordinate of the center.</param>
    /// <param name="y">The y coordinate of the center.</param>
    /// <param name="r">The radius; a negative radius makes the writer invalid.</param>
    /// <param name="startAngle">The start angle in radians (0 points right, angles grow clockwise on screen).</param>
    /// <param name="endAngle">The end angle in radians.</param>
    /// <param name="ccw">Whether the arc runs counter-clockwise.</param>
    public void Arc(double x, double y, double r, double startAngle, double endAngle, bool ccw)
    {
        if (!_isValid)
        {
            return;
        }

        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(r) || r < 0 || !double.IsFinite(startAngle) || !double.IsFinite(endAngle))
        {
            _isValid = false;
            return;
        }

        var dx = r * Math.Cos(startAngle);
        var dy = r * Math.Sin(startAngle);
        var x0 = x + dx;
        var y0 = y + dy;
        var cw = !ccw;
        var da = ccw ? startAngle - endAngle : endAngle - startAngle;

        // Move to the start of the arc, or line to it when the current point is elsewhere
        if (!_hasCurrent)
        {
            MoveTo(x0, y0);
        }
        else if (Math.Abs(_x1 - x0) > PathEpsilon || Math.Abs(_y1 - y0) > PathEpsilon)
        {
            LineTo(x0, y0);
        }

        // An empty arc is done
        if (r == 0)
        {
            return;
        }

        // An angle that goes the wrong way flips the direction
        if (da < 0)
        {
            da = (da % Tau) + Tau;
        }

        if (da > TauEpsilon)
        {
            // A complete circle: two half circles
            ArcSegment(r, true, cw, x - dx, y - dy);
            ArcSegment(r, true, cw, x0, y0);
        }
        else if (da > PathEpsilon)
        {
            ArcSegment(r, da >= Math.PI, cw, x + (r * Math.Cos(endAngle)), y + (r * Math.Sin(endAngle)));
        }
    }

    /// <summary>
    ///     Gets the path that was written.
    /// </summary>
    /// <returns>The path, or <see cref="Path.Empty"/> when a call was not valid.</returns>
    public Path Build() => _isValid ? _builder.Build() : Path.Empty;

    /// <summary>
    ///     Records one SVG arc command from the current point.
    /// </summary>
    /// <param name="r">The radius.</param>
    /// <param name="largeArc">The large-arc flag.</param>
    /// <param name="sweep">The sweep flag (true for clockwise on screen).</param>
    /// <param name="x">The x coordinate of the end.</param>
    /// <param name="y">The y coordinate of the end.</param>
    private void ArcSegment(double r, bool largeArc, bool sweep, double x, double y)
    {
        if (!Accept(x, y))
        {
            return;
        }

        EnsureOpen();
        _builder.ArcTo(new Vector2((float)r, (float)r), 0f, largeArc, sweep, new Vector2((float)x, (float)y));
        _x1 = x;
        _y1 = y;
    }

    /// <summary>
    ///     Reopens the subpath that a close ended, at its start point.
    /// </summary>
    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            _builder.MoveTo(new Vector2((float)_x0, (float)_y0));
            _isOpen = true;
        }
    }

    /// <summary>
    ///     Checks a coordinate pair; a pair that is not finite (or does not fit a single) makes the writer invalid.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns><see langword="true"/> when the writer is valid and the pair can be recorded.</returns>
    private bool Accept(double x, double y)
    {
        if (_isValid && (!float.IsFinite((float)x) || !float.IsFinite((float)y)))
        {
            _isValid = false;
        }

        return _isValid;
    }
}
