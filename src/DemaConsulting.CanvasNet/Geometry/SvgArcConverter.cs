using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Converts SVG-style endpoint-parameterized elliptical arcs into one or more cubic Bezier
///     curves, following the SVG 1.1 Appendix F "Elliptical arc implementation notes" algorithm.
/// </summary>
/// <remarks>
///     <see cref="PathBuilder.ArcTo"/> always stores the raw SVG arc parameters supplied by the
///     caller, without pre-inspecting or pre-converting them; this class is the only place in the
///     <c>Geometry</c> namespace responsible for SVG-spec fidelity, including its documented
///     degenerate cases, so a consumer that never needs Bezier segments (for example, a
///     hit-testing implementation with its own arc math) never pays for the conversion.
/// </remarks>
public static class SvgArcConverter
{
    /// <summary>
    ///     The maximum angular span, in radians, converted to a single cubic Bezier segment
    ///     (90 degrees). Splitting larger sweeps into multiple sub-arcs of at most this size keeps
    ///     each individual cubic's approximation error negligible - a single cubic Bezier cannot
    ///     accurately approximate an elliptical arc much larger than a quarter turn.
    /// </summary>
    private const float MaxSegmentAngle = MathF.PI / 2f;

    /// <summary>
    ///     Converts an SVG-style elliptical arc, from <paramref name="start"/> to
    ///     <paramref name="end"/>, into a sequence of cubic Bezier curves appended to
    ///     <paramref name="output"/>.
    /// </summary>
    /// <param name="start">The arc's start point (the current point before the arc).</param>
    /// <param name="radius">The arc's x- and y-radii (rx, ry), as supplied to the SVG "A"/"a" command.</param>
    /// <param name="rotationDegrees">The arc's x-axis rotation, in degrees.</param>
    /// <param name="largeArc">The SVG arc "large-arc-flag".</param>
    /// <param name="sweep">The SVG arc "sweep-flag".</param>
    /// <param name="end">The arc's end point.</param>
    /// <param name="output">
    ///     The list each resulting cubic Bezier segment (as a <c>(Control1, Control2, End)</c>
    ///     tuple) is appended to, in end-to-end order so that a caller building a continuous
    ///     polyline/curve chain never needs to re-derive the implicit start point of each segment
    ///     (it is the previous segment's <c>End</c>, or <paramref name="start"/> for the first).
    /// </param>
    /// <remarks>
    ///     Handles both degenerate cases the SVG specification itself defines: if
    ///     <paramref name="start"/> equals <paramref name="end"/>, no segment is emitted (per
    ///     spec, a zero-length arc is not rendered); if either radius is zero, a single synthetic
    ///     straight-line-equivalent cubic Bezier is emitted instead (control points placed at 1/3
    ///     and 2/3 along the <paramref name="start"/>-<paramref name="end"/> chord), since the SVG
    ///     specification itself defines this case as "treated as a straight line". Every other
    ///     SVG-valid input (including out-of-range radii, corrected per spec, and all four
    ///     <paramref name="largeArc"/>/<paramref name="sweep"/> combinations) is converted without
    ///     throwing.
    /// </remarks>
    public static void ToBeziers(
        Vector2 start,
        Vector2 radius,
        float rotationDegrees,
        bool largeArc,
        bool sweep,
        Vector2 end,
        IList<(Vector2 Control1, Vector2 Control2, Vector2 End)> output)
    {
        // Degenerate case 1 (SVG spec): a zero-length arc renders nothing
        if (start == end)
        {
            return;
        }

        var rx = MathF.Abs(radius.X);
        var ry = MathF.Abs(radius.Y);

        // Degenerate case 2 (SVG spec, Open Question #3 default): a zero radius on either axis is
        // defined by the spec as "treated as a straight line" - emit one synthetic cubic whose
        // controls lie on the chord, rather than attempting an ellipse with zero extent
        if (rx == 0 || ry == 0)
        {
            var control1 = start + (end - start) / 3f;
            var control2 = start + (end - start) * 2f / 3f;
            output.Add((control1, control2, end));
            return;
        }

        var phi = rotationDegrees * MathF.PI / 180f;
        var cosPhi = MathF.Cos(phi);
        var sinPhi = MathF.Sin(phi);

        // Step 1: compute (x1', y1') - the start point in a coordinate system where the ellipse's
        // rotation is undone and the midpoint of start/end is the origin
        var dx2 = (start.X - end.X) / 2f;
        var dy2 = (start.Y - end.Y) / 2f;
        var x1P = cosPhi * dx2 + sinPhi * dy2;
        var y1P = -sinPhi * dx2 + cosPhi * dy2;

        // Step 2: correct out-of-range radii per the spec's Lambda scale-factor formula - if the
        // requested radii are too small to reach between start and end at all, scale both up by
        // the same factor so a valid ellipse exists
        var lambda = x1P * x1P / (rx * rx) + y1P * y1P / (ry * ry);
        if (lambda > 1)
        {
            var scale = MathF.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        // Step 3: compute the center (cx', cy') in the rotated/translated coordinate system, then
        // transform back to the original coordinate system
        var rxSq = rx * rx;
        var rySq = ry * ry;
        var x1PSq = x1P * x1P;
        var y1PSq = y1P * y1P;

        var numerator = rxSq * rySq - rxSq * y1PSq - rySq * x1PSq;
        var denominator = rxSq * y1PSq + rySq * x1PSq;

        // Floating-point round-trip through the Lambda correction above can leave numerator
        // fractionally negative when it is mathematically exactly zero - clamp to zero rather
        // than taking the square root of a negative number
        var coefficient = numerator <= 0 ? 0f : MathF.Sqrt(numerator / denominator);
        if (largeArc == sweep)
        {
            coefficient = -coefficient;
        }

        var cxP = coefficient * rx * y1P / ry;
        var cyP = -coefficient * ry * x1P / rx;

        var cx = cosPhi * cxP - sinPhi * cyP + (start.X + end.X) / 2f;
        var cy = sinPhi * cxP + cosPhi * cyP + (start.Y + end.Y) / 2f;

        // Step 4: compute the start angle theta1 and angular sweep delta-theta
        var ux = (x1P - cxP) / rx;
        var uy = (y1P - cyP) / ry;
        var vx = (-x1P - cxP) / rx;
        var vy = (-y1P - cyP) / ry;

        var theta1 = SignedAngleBetween(1f, 0f, ux, uy);
        var deltaTheta = SignedAngleBetween(ux, uy, vx, vy);

        if (!sweep && deltaTheta > 0)
        {
            deltaTheta -= 2 * MathF.PI;
        }
        else if (sweep && deltaTheta < 0)
        {
            deltaTheta += 2 * MathF.PI;
        }

        // Step 5: split the total sweep into segments of at most 90 degrees each, converting each
        // to a cubic Bezier via the standard kappa = 4/3 * tan(delta/4) control-point-distance
        // formula, expressed on the unit circle then mapped through the ellipse radii, rotation,
        // and center offset
        var segmentCount = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(deltaTheta) / MaxSegmentAngle));
        var segmentAngle = deltaTheta / segmentCount;

        var angle = theta1;
        for (var i = 0; i < segmentCount; i++)
        {
            var nextAngle = angle + segmentAngle;
            var segment = ConvertSegment(angle, nextAngle, rx, ry, cosPhi, sinPhi, cx, cy);

            // The final segment's End is forced to exactly the caller-supplied end point (rather
            // than the value obtained by evaluating trigonometric functions at the target angle)
            // so that a caller chaining segments into a continuous curve/polyline always lands
            // exactly on the requested end point, with no residual floating-point drift from the
            // angle computation above.
            if (i == segmentCount - 1)
            {
                segment = (segment.Control1, segment.Control2, end);
            }

            output.Add(segment);
            angle = nextAngle;
        }
    }

    /// <summary>
    ///     Converts a single elliptical arc segment (angular span at most <see cref="MaxSegmentAngle"/>)
    ///     from <paramref name="theta1"/> to <paramref name="theta2"/> into one cubic Bezier curve.
    /// </summary>
    private static (Vector2 Control1, Vector2 Control2, Vector2 End) ConvertSegment(
        float theta1, float theta2, float rx, float ry, float cosPhi, float sinPhi, float cx, float cy)
    {
        var delta = theta2 - theta1;
        var t = 4f / 3f * MathF.Tan(delta / 4f);

        var cosT1 = MathF.Cos(theta1);
        var sinT1 = MathF.Sin(theta1);
        var cosT2 = MathF.Cos(theta2);
        var sinT2 = MathF.Sin(theta2);

        // Unit-circle control points: the standard cubic-Bezier-approximates-a-circular-arc
        // construction, using the tangent direction (-sin, cos) at each endpoint
        var u3 = new Vector2(cosT2, sinT2);
        var u1 = new Vector2(cosT1 - t * sinT1, sinT1 + t * cosT1);
        var u2 = new Vector2(cosT2 + t * sinT2, sinT2 - t * cosT2);

        return (MapUnitCircleToEllipse(u1, rx, ry, cosPhi, sinPhi, cx, cy),
            MapUnitCircleToEllipse(u2, rx, ry, cosPhi, sinPhi, cx, cy),
            MapUnitCircleToEllipse(u3, rx, ry, cosPhi, sinPhi, cx, cy));
    }

    /// <summary>
    ///     Maps a point on the unit circle to the corresponding point on the rotated, translated
    ///     ellipse: scale by the radii, rotate by the x-axis rotation, then translate by the
    ///     center.
    /// </summary>
    private static Vector2 MapUnitCircleToEllipse(Vector2 unitPoint, float rx, float ry, float cosPhi, float sinPhi, float cx, float cy)
    {
        var ex = rx * unitPoint.X;
        var ey = ry * unitPoint.Y;
        return new Vector2(
            cosPhi * ex - sinPhi * ey + cx,
            sinPhi * ex + cosPhi * ey + cy);
    }

    /// <summary>
    ///     Computes the signed angle, in radians, from vector <c>(ux, uy)</c> to vector
    ///     <c>(vx, vy)</c>, per the SVG specification's angle function.
    /// </summary>
    private static float SignedAngleBetween(float ux, float uy, float vx, float vy)
    {
        var dot = ux * vx + uy * vy;
        var lengths = MathF.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));

        // Clamp before acos to guard against floating-point round-off pushing the ratio very
        // slightly outside [-1, 1], which would otherwise produce NaN
        var cosAngle = Math.Clamp(dot / lengths, -1f, 1f);
        var angle = MathF.Acos(cosAngle);

        var cross = ux * vy - uy * vx;
        return cross < 0 ? -angle : angle;
    }
}
