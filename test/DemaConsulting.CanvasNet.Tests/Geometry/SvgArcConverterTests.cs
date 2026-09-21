using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests for the SvgArcConverter class.
/// </summary>
/// <remarks>
///     Verification against sampled points uses an independently written, double-precision
///     implementation of the SVG endpoint-to-center parameterization (<see cref="ComputeCenter"/>
///     and <see cref="EvaluateEllipse"/> below) as an oracle, rather than re-deriving or reusing
///     any part of <see cref="SvgArcConverter"/> itself, so that a shared bug in the algorithm
///     under test cannot also hide itself in the verification.
/// </remarks>
public class SvgArcConverterTests
{
    /// <summary>
    ///     Independently computes the SVG endpoint-to-center arc parameterization in double
    ///     precision, following the SVG 1.1 Appendix F algorithm directly from the specification
    ///     text (not by reading <see cref="SvgArcConverter"/>'s implementation).
    /// </summary>
    private static (double Cx, double Cy, double Theta1, double DeltaTheta) ComputeCenter(
        Vector2 start, Vector2 end, double rx, double ry, double rotationDegrees, bool largeArc, bool sweep)
    {
        var phi = rotationDegrees * Math.PI / 180.0;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);

        var dx2 = (start.X - end.X) / 2.0;
        var dy2 = (start.Y - end.Y) / 2.0;
        var x1P = cosPhi * dx2 + sinPhi * dy2;
        var y1P = -sinPhi * dx2 + cosPhi * dy2;

        var lambda = x1P * x1P / (rx * rx) + y1P * y1P / (ry * ry);
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }

        var num = rx * rx * ry * ry - rx * rx * y1P * y1P - ry * ry * x1P * x1P;
        var den = rx * rx * y1P * y1P + ry * ry * x1P * x1P;
        var co = num <= 0 ? 0 : Math.Sqrt(num / den);
        if (largeArc == sweep)
        {
            co = -co;
        }

        var cxP = co * rx * y1P / ry;
        var cyP = -co * ry * x1P / rx;

        var cx = cosPhi * cxP - sinPhi * cyP + (start.X + end.X) / 2.0;
        var cy = sinPhi * cxP + cosPhi * cyP + (start.Y + end.Y) / 2.0;

        var ux = (x1P - cxP) / rx;
        var uy = (y1P - cyP) / ry;
        var vx = (-x1P - cxP) / rx;
        var vy = (-y1P - cyP) / ry;

        var theta1 = SignedAngle(1, 0, ux, uy);
        var deltaTheta = SignedAngle(ux, uy, vx, vy);
        if (!sweep && deltaTheta > 0)
        {
            deltaTheta -= 2 * Math.PI;
        }
        else if (sweep && deltaTheta < 0)
        {
            deltaTheta += 2 * Math.PI;
        }

        return (cx, cy, theta1, deltaTheta);
    }

    private static double SignedAngle(double ux, double uy, double vx, double vy)
    {
        var dot = ux * vx + uy * vy;
        var lengths = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        var cosAngle = Math.Clamp(dot / lengths, -1, 1);
        var angle = Math.Acos(cosAngle);
        var cross = ux * vy - uy * vx;
        return cross < 0 ? -angle : angle;
    }

    /// <summary>
    ///     Evaluates the point on the true ellipse at angle theta, for the ellipse described by
    ///     center (cx,cy), radii (rx,ry), and x-axis rotation rotationDegrees.
    /// </summary>
    private static Vector2 EvaluateEllipse(double cx, double cy, double rx, double ry, double rotationDegrees, double theta)
    {
        var phi = rotationDegrees * Math.PI / 180.0;
        var cosPhi = Math.Cos(phi);
        var sinPhi = Math.Sin(phi);
        var ex = rx * Math.Cos(theta);
        var ey = ry * Math.Sin(theta);
        return new Vector2(
            (float)(cx + cosPhi * ex - sinPhi * ey),
            (float)(cy + sinPhi * ex + cosPhi * ey));
    }

    /// <summary>
    ///     Evaluates a cubic Bezier curve at parameter t.
    /// </summary>
    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>
    ///     Proves that a zero-length arc (start equals end) emits no Bezier segments, per the
    ///     SVG specification's documented degenerate case.
    /// </summary>
    [Fact]
    public void SvgArcConverter_ToBeziers_StartEqualsEnd_EmitsNoSegments()
    {
        // Arrange
        var point = new Vector2(10, 10);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(point, new Vector2(5, 5), 0, false, false, point, output);

        // Assert
        Assert.Empty(output);
    }

    /// <summary>
    ///     Proves that a zero x-radius emits exactly one synthetic straight-line-equivalent cubic
    ///     whose control points lie at one-third and two-thirds along the start-end chord.
    /// </summary>
    [Fact]
    public void SvgArcConverter_ToBeziers_ZeroXRadius_EmitsSingleStraightLineEquivalentCubic()
    {
        // Arrange
        var start = new Vector2(0, 0);
        var end = new Vector2(30, 0);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(start, new Vector2(0, 10), 0, false, false, end, output);

        // Assert: exactly one segment, with controls at 1/3 and 2/3 along the chord
        var segment = Assert.Single(output);
        Assert.Equal(new Vector2(10, 0), segment.Control1);
        Assert.Equal(new Vector2(20, 0), segment.Control2);
        Assert.Equal(end, segment.End);
    }

    /// <summary>
    ///     Proves that a zero y-radius likewise emits exactly one synthetic straight-line
    ///     cubic, exercising the other half of the "either radius is zero" degenerate condition.
    /// </summary>
    [Fact]
    public void SvgArcConverter_ToBeziers_ZeroYRadius_EmitsSingleStraightLineEquivalentCubic()
    {
        // Arrange
        var start = new Vector2(0, 0);
        var end = new Vector2(0, 30);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(start, new Vector2(10, 0), 0, false, false, end, output);

        // Assert
        var segment = Assert.Single(output);
        Assert.Equal(new Vector2(0, 10), segment.Control1);
        Assert.Equal(new Vector2(0, 20), segment.Control2);
        Assert.Equal(end, segment.End);
    }

    /// <summary>
    ///     Proves that a true semicircular arc (chord length equal to the diameter, so the arc
    ///     spans exactly 180 degrees regardless of flag selection) produces a Bezier chain that
    ///     connects continuously from start to end and whose sampled points lie on the expected
    ///     circle.
    /// </summary>
    [Fact]
    public void SvgArcConverter_ToBeziers_Semicircle_ProducesContinuousChainOnExpectedCircle()
    {
        // Arrange: a chord of length 200 with radius 100 - the chord equals the diameter, so this
        // is a true semicircle (180 degree sweep) for any flag combination
        var start = new Vector2(200, 200);
        var end = new Vector2(400, 200);
        var radius = new Vector2(100, 100);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(start, radius, 0, largeArc: true, sweep: false, end, output);

        // Assert: chain connects end-to-end from start to end
        AssertChainConnects(start, end, output);

        // Assert: the independently computed center is equidistant (100) from both endpoints,
        // and every sampled point on the Bezier chain lies within a small tolerance of that
        // radius from the center
        var (cx, cy, _, deltaTheta) = ComputeCenter(start, end, 100, 100, 0, largeArc: true, sweep: false);
        Assert.Equal(100, Math.Sqrt((start.X - cx) * (start.X - cx) + (start.Y - cy) * (start.Y - cy)), 3);
        AssertChainLiesOnCircle(start, output, cx, cy, 100, tolerance: 1.0);

        // A semicircle spans exactly 180 degrees
        Assert.Equal(Math.PI, Math.Abs(deltaTheta), 3);
    }

    /// <summary>
    ///     Proves, for every one of the four largeArc/sweep flag combinations, that the resulting
    ///     Bezier chain connects continuously and every sampled point lies on the circle computed
    ///     independently by <see cref="ComputeCenter"/>.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SvgArcConverter_ToBeziers_AllFourFlagCombinations_ProducesContinuousChainOnExpectedCircle(bool largeArc, bool sweep)
    {
        // Arrange: a circular arc where two candidate circles exist, so all four flag
        // combinations produce a distinct, well-defined arc
        var start = new Vector2(100, 0);
        var end = new Vector2(0, 100);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(start, new Vector2(100, 100), 0, largeArc, sweep, end, output);

        // Assert: chain connects, and independently-computed center/radius match every sampled point
        AssertChainConnects(start, end, output);
        var (cx, cy, _, deltaTheta) = ComputeCenter(start, end, 100, 100, 0, largeArc, sweep);
        AssertChainLiesOnCircle(start, output, cx, cy, 100, tolerance: 1.0);

        // largeArc selects a sweep of at least 180 degrees; otherwise, less than 180 degrees
        if (largeArc)
        {
            Assert.True(Math.Abs(deltaTheta) >= Math.PI - 1e-6);
        }
        else
        {
            Assert.True(Math.Abs(deltaTheta) <= Math.PI + 1e-6);
        }
    }

    /// <summary>
    ///     Proves that a rotated, non-circular ellipse arc still connects continuously from start
    ///     to end, and that its final segment endpoint matches the independently computed
    ///     ellipse point at the expected end angle.
    /// </summary>
    [Fact]
    public void SvgArcConverter_ToBeziers_RotatedEllipticalArc_ConnectsAndMatchesIndependentEllipse()
    {
        // Arrange: a rotated ellipse (rx != ry, 30 degree rotation)
        var start = new Vector2(0, 0);
        var end = new Vector2(80, 40);
        var radius = new Vector2(60, 30);
        var output = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();

        // Act
        SvgArcConverter.ToBeziers(start, radius, 30, largeArc: false, sweep: true, end, output);

        // Assert: chain connects end-to-end
        AssertChainConnects(start, end, output);

        // Assert: the independently computed ellipse's point at theta1 + deltaTheta (the end
        // angle) matches the arc's declared end point
        var (cx, cy, theta1, deltaTheta) = ComputeCenter(start, end, 60, 30, 30, largeArc: false, sweep: true);
        var expectedEnd = EvaluateEllipse(cx, cy, 60, 30, 30, theta1 + deltaTheta);
        Assert.Equal(expectedEnd.X, end.X, 1);
        Assert.Equal(expectedEnd.Y, end.Y, 1);
    }

    /// <summary>
    ///     Asserts that a Bezier chain's segments connect end-to-end from <paramref name="start"/>
    ///     through to <paramref name="end"/> with no gaps.
    /// </summary>
    private static void AssertChainConnects(Vector2 start, Vector2 end, IReadOnlyList<(Vector2 Control1, Vector2 Control2, Vector2 End)> chain)
    {
        Assert.NotEmpty(chain);
        Assert.Equal(end, chain[^1].End);

        var current = start;
        foreach (var segment in chain)
        {
            // Each segment's implicit start is the previous segment's End (or the arc's overall
            // start for the first segment) - there is no explicit "start" field to compare, so
            // this loop only needs to track the running point for the next iteration
            current = segment.End;
        }

        Assert.Equal(end, current);
    }

    /// <summary>
    ///     Asserts that every sampled point along a Bezier chain lies within <paramref name="tolerance"/>
    ///     of the given circle.
    /// </summary>
    private static void AssertChainLiesOnCircle(Vector2 start, IReadOnlyList<(Vector2 Control1, Vector2 Control2, Vector2 End)> chain, double cx, double cy, double radius, double tolerance)
    {
        var segmentStart = start;
        foreach (var segment in chain)
        {
            for (var i = 0; i <= 20; i++)
            {
                var t = i / 20f;
                var point = EvaluateCubic(segmentStart, segment.Control1, segment.Control2, segment.End, t);
                var distance = Math.Sqrt((point.X - cx) * (point.X - cx) + (point.Y - cy) * (point.Y - cy));
                Assert.True(Math.Abs(distance - radius) <= tolerance, $"Point {point} was distance {distance} from center, expected {radius}");
            }

            segmentStart = segment.End;
        }
    }
}
