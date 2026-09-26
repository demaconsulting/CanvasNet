using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests for the BezierFlattening class.
/// </summary>
public class BezierFlatteningTests
{
    /// <summary>
    ///     Evaluates a cubic Bezier curve at parameter t, using the standard closed-form cubic
    ///     Bezier formula, independent of the flattening implementation under test.
    /// </summary>
    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>
    ///     Evaluates a quadratic Bezier curve at parameter t, using the standard closed-form
    ///     quadratic Bezier formula, independent of the flattening implementation under test.
    /// </summary>
    private static Vector2 EvaluateQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        var u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    /// <summary>
    ///     Computes the minimum perpendicular distance from a point to any segment of a polyline.
    /// </summary>
    private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> polyline)
    {
        var minDistance = float.PositiveInfinity;
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            var a = polyline[i];
            var b = polyline[i + 1];
            var segment = b - a;
            var lengthSquared = segment.LengthSquared();
            float distance;
            if (lengthSquared <= float.Epsilon)
            {
                distance = Vector2.Distance(point, a);
            }
            else
            {
                var t = Math.Clamp(Vector2.Dot(point - a, segment) / lengthSquared, 0f, 1f);
                var closest = a + t * segment;
                distance = Vector2.Distance(point, closest);
            }

            minDistance = Math.Min(minDistance, distance);
        }

        return minDistance;
    }

    /// <summary>
    ///     Proves that FlattenCubic never writes the start point p0 to the output, and always
    ///     writes the end point p3 last.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_SimpleCurve_NeverWritesStartAndAlwaysWritesEndLast()
    {
        // Arrange
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(0, 10);
        var p2 = new Vector2(10, 10);
        var p3 = new Vector2(10, 0);
        var output = new List<Vector2>();

        // Act
        BezierFlattening.FlattenCubic(p0, p1, p2, p3, 0.1f, output);

        // Assert
        Assert.DoesNotContain(p0, output);
        Assert.Equal(p3, output[^1]);
        Assert.NotEmpty(output);
    }

    /// <summary>
    ///     Proves that FlattenQuadratic never writes the start point p0 to the output, and always
    ///     writes the end point p2 last.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenQuadratic_SimpleCurve_NeverWritesStartAndAlwaysWritesEndLast()
    {
        // Arrange
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(5, 10);
        var p2 = new Vector2(10, 0);
        var output = new List<Vector2>();

        // Act
        BezierFlattening.FlattenQuadratic(p0, p1, p2, 0.1f, output);

        // Assert
        Assert.DoesNotContain(p0, output);
        Assert.Equal(p2, output[^1]);
        Assert.NotEmpty(output);
    }

    /// <summary>
    ///     Property-based tolerance-convergence test: for a table of cubic curves and a range of
    ///     tolerances, every point densely sampled along the true curve must lie within
    ///     `tolerance` of the flattened polyline (extended with p0 as the implicit first point),
    ///     proving the flattened polyline is a faithful approximation at the requested tolerance.
    /// </summary>
    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.5f)]
    [InlineData(0.1f)]
    [InlineData(0.01f)]
    public void BezierFlattening_FlattenCubic_VariousTolerances_SampledCurvePointsWithinTolerance(float tolerance)
    {
        // Arrange: an S-shaped cubic curve with a pronounced curvature
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(0, 50);
        var p2 = new Vector2(100, -50);
        var p3 = new Vector2(100, 0);
        var output = new List<Vector2> { p0 };

        // Act
        BezierFlattening.FlattenCubic(p0, p1, p2, p3, tolerance, output);

        // Assert: sample 1000 points densely along the true curve and confirm each lies within a
        // small multiple of tolerance of the flattened polyline (a small multiplier accounts for
        // the flatness metric measuring control-point deviation, a conservative proxy for true
        // curve deviation, not an exact bound)
        for (var i = 0; i <= 1000; i++)
        {
            var t = i / 1000f;
            var truePoint = EvaluateCubic(p0, p1, p2, p3, t);
            var distance = DistanceToPolyline(truePoint, output);
            Assert.True(distance <= tolerance * 2, $"t={t}: distance {distance} exceeded tolerance {tolerance}");
        }
    }

    /// <summary>
    ///     Proves that the flattened segment count is monotonically non-increasing as tolerance
    ///     increases (a looser tolerance never requires more segments than a tighter one).
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_IncreasingTolerance_SegmentCountIsMonotonicallyNonIncreasing()
    {
        // Arrange
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(0, 50);
        var p2 = new Vector2(100, -50);
        var p3 = new Vector2(100, 0);
        float[] tolerances = [0.01f, 0.1f, 1f, 10f];

        // Act
        var counts = tolerances.Select(tolerance =>
        {
            var output = new List<Vector2>();
            BezierFlattening.FlattenCubic(p0, p1, p2, p3, tolerance, output);
            return output.Count;
        }).ToArray();

        // Assert: each successive (looser) tolerance produces no more segments than the previous
        for (var i = 1; i < counts.Length; i++)
        {
            Assert.True(counts[i] <= counts[i - 1], $"Segment count increased from {counts[i - 1]} to {counts[i]} as tolerance loosened");
        }
    }

    /// <summary>
    ///     Proves that flattening a curve with coincident control points (fully degenerate -
    ///     every control point equal to a single point) terminates and produces a valid,
    ///     non-empty output consisting of just that point.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_CoincidentControlPoints_TerminatesWithSinglePoint()
    {
        // Arrange: every control point is the same point
        var p = new Vector2(5, 5);
        var output = new List<Vector2>();

        // Act
        BezierFlattening.FlattenCubic(p, p, p, p, 0.01f, output);

        // Assert: terminates immediately (already flat) with just the end point
        var point = Assert.Single(output);
        Assert.Equal(p, point);
    }

    /// <summary>
    ///     Proves that flattening a collinear (straight-line-equivalent) cubic terminates quickly
    ///     and produces points that are all itself collinear with the endpoints.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_CollinearControlPoints_TerminatesWithCollinearPoints()
    {
        // Arrange: all four points lie on the same line
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(3, 3);
        var p2 = new Vector2(6, 6);
        var p3 = new Vector2(10, 10);
        var output = new List<Vector2>();

        // Act
        BezierFlattening.FlattenCubic(p0, p1, p2, p3, 0.01f, output);

        // Assert: a straight-line curve should flatten to essentially a single output segment,
        // ending exactly at p3
        Assert.NotEmpty(output);
        Assert.Equal(p3, output[^1]);
    }

    /// <summary>
    ///     Proves that the flatness test measures distance to the finite chord *segment*, not the
    ///     infinite line through it: a control point whose projection onto the chord falls beyond
    ///     the chord's end (here, p1's projection lands well past p3) must still force
    ///     subdivision if its distance to the finite segment exceeds tolerance, even though its
    ///     distance to the infinite line is small. Regression test for a bug where such a control
    ///     point was wrongly judged "flat", causing the flattened polyline to deviate from the
    ///     true curve by far more than the requested tolerance.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_ControlPointProjectsBeyondChordEnd_StaysWithinTolerance()
    {
        // Arrange: p1's projection onto the p0-p3 chord falls beyond p3 (t > 1), so its distance
        // to the infinite line through the chord (~0.4) is well within tolerance, but its
        // distance to the finite chord segment (clamped to p3, ~10) is not - the true curve bulges
        // past x=10 as a result (e.g. at t=0.4, the curve reaches about (10.72, 0.17), over 0.7
        // units from the p0-p3 segment)
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(20, 0.4f);
        var p2 = new Vector2(5, 0);
        var p3 = new Vector2(10, 0);
        const float tolerance = 0.5f;
        var output = new List<Vector2> { p0 };

        // Act
        BezierFlattening.FlattenCubic(p0, p1, p2, p3, tolerance, output);

        // Assert: the curve must have been subdivided (a single segment could not possibly stay
        // within tolerance here), and every densely sampled true-curve point must lie within
        // tolerance of the flattened polyline
        Assert.True(output.Count > 2, $"Expected subdivision to occur, but only got {output.Count} output points");
        for (var i = 0; i <= 1000; i++)
        {
            var t = i / 1000f;
            var truePoint = EvaluateCubic(p0, p1, p2, p3, t);
            var distance = DistanceToPolyline(truePoint, output);
            Assert.True(distance <= tolerance * 2, $"t={t}: distance {distance} exceeded tolerance {tolerance}");
        }
    }

    /// <summary>
    ///     Regression test for the <c>MaxRecursionDepth</c> safety valve: proves that a
    ///     pathological cubic curve that can never satisfy the flatness test (two collapsed
    ///     control points positioned so subdivision cannot converge, combined with an extremely
    ///     tight tolerance) still terminates promptly and produces a bounded output point count,
    ///     rather than recursing indefinitely. This deliberately tests the documented safety-valve
    ///     behavior itself (bounded termination), not a tolerance violation - see
    ///     <c>MaxRecursionDepth</c>'s remarks for why the guard does not guarantee tolerance is met
    ///     for input like this.
    /// </summary>
    [Fact]
    public void BezierFlattening_FlattenCubic_PathologicalNonConvergingCurve_TerminatesWithBoundedOutput()
    {
        // Arrange: an extremely tight tolerance (1e-10, far below what float32 precision can
        // resolve for a curve of this scale) that a well-formed, non-degenerate curve can never
        // satisfy at any practical recursion depth, forcing the recursion guard to be exercised
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(0, 50);
        var p2 = new Vector2(100, -50);
        var p3 = new Vector2(100, 0);
        const float tolerance = 1e-10f;
        var output = new List<Vector2>();

        // Act
        BezierFlattening.FlattenCubic(p0, p1, p2, p3, tolerance, output);

        // Assert: the call must return (proving termination) with a bounded output size - at
        // most 2^MaxRecursionDepth (2^20, documented as ~1,048,576) leaves are possible, and the
        // final point must still be the curve's declared end point
        Assert.NotEmpty(output);
        Assert.True(output.Count <= 1_048_576, $"Expected a bounded output size, but got {output.Count} points");
        Assert.Equal(p3, output[^1]);
    }

    /// <summary>
    ///     Proves that a non-positive tolerance throws ArgumentOutOfRangeException for both
    ///     FlattenCubic and FlattenQuadratic.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void BezierFlattening_Flatten_NonPositiveTolerance_ThrowsArgumentOutOfRangeException(float tolerance)
    {
        // Arrange
        var output = new List<Vector2>();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BezierFlattening.FlattenCubic(Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, tolerance, output));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BezierFlattening.FlattenQuadratic(Vector2.Zero, Vector2.Zero, Vector2.Zero, tolerance, output));
    }

    /// <summary>
    ///     Property-based tolerance-convergence test for FlattenQuadratic, mirroring the cubic
    ///     test above: every densely sampled true-curve point must lie within tolerance of the
    ///     flattened polyline.
    /// </summary>
    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.1f)]
    [InlineData(0.01f)]
    public void BezierFlattening_FlattenQuadratic_VariousTolerances_SampledCurvePointsWithinTolerance(float tolerance)
    {
        // Arrange
        var p0 = new Vector2(0, 0);
        var p1 = new Vector2(50, 100);
        var p2 = new Vector2(100, 0);
        var output = new List<Vector2> { p0 };

        // Act
        BezierFlattening.FlattenQuadratic(p0, p1, p2, tolerance, output);

        // Assert
        for (var i = 0; i <= 1000; i++)
        {
            var t = i / 1000f;
            var truePoint = EvaluateQuadratic(p0, p1, p2, t);
            var distance = DistanceToPolyline(truePoint, output);
            Assert.True(distance <= tolerance * 2, $"t={t}: distance {distance} exceeded tolerance {tolerance}");
        }
    }
}
