using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Marks.CurveBuilderTests;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Tests of the spline curves of <see cref="CurveBuilder"/>: cardinal (open, closed), Catmull-Rom, monotone and natural. Every
///     expected path is derived by hand from the formulas of the d3-shape curve, which each test quotes.
/// </summary>
public class CurveBuilderSplineTests
{
    /// <summary>
    ///     Validates the cardinal curves against the tangent formula: the controls are <c>P1 + k (P2 - P0)</c> and <c>P2 + k (P1 - P3)</c>
    ///     with <c>k = (1 - tension) / 6</c>.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_CardinalFamilyUsesTheTensionFormula()
    {
        // Arrange: P0 = (0,0), P1 = (10,10), P2 = (20,0), P3 = (30,10)
        CurvePoint[] points = [P(0, 0), P(10, 10), P(20, 0), P(30, 10)];

        // cardinal, tension 0 (k = 1/6): the ends are duplicated, so the first controls are P0 and P1 + (P0 - P2) / 6 = (6.6667,10);
        // the middle controls are P1 + (P2 - P0) / 6 = (13.3333,10) and P2 + (P1 - P3) / 6 = (16.6667,0); the last are P2 + (P3 - P1) / 6 = (23.3333,0), P3
        Assert.Equal(
            "M0,0 C0,0 6.6667,10 10,10 C13.3333,10 16.6667,0 20,0 C23.3333,0 30,10 30,10",
            Run(CurveKind.Cardinal, null, points));

        // tension 0.5 (k = 1/12): the offsets halve
        Assert.Equal(
            "M0,0 C0,0 8.3333,10 10,10 C11.6667,10 18.3333,0 20,0 C21.6667,0 30,10 30,10",
            Run(CurveKind.Cardinal, 0.5, points));

        // tension 1 (k = 0): the controls are the end points, a polyline of flat Beziers
        Assert.Equal(
            "M0,0 C0,0 10,10 10,10 C10,10 20,0 20,0 C20,0 30,10 30,10",
            Run(CurveKind.Cardinal, 1, points));

        // cardinal-open starts at P1 and ends at P2: one segment with the middle controls
        Assert.Equal("M10,10 C13.3333,10 16.6667,0 20,0", Run(CurveKind.CardinalOpen, null, points));

        // cardinal-closed of (0,0), (10,10), (20,0): the loop starts at P1; the segment to P2 has the controls P1 + (P2 - P0) / 6 and
        // P2 + (P1 - P0) / 6, the segment to P0 has P2 + (P0 - P1) / 6 and P0 + (P2 - P1) / 6, the last to P1 has P0 + (P1 - P2) / 6 and P1 + (P0 - P2) / 6
        Assert.Equal(
            "M10,10 C13.3333,10 21.6667,1.6667 20,0 C18.3333,-1.6667 1.6667,-1.6667 0,0 C-1.6667,1.6667 6.6667,10 10,10",
            Run(CurveKind.CardinalClosed, null, P(0, 0), P(10, 10), P(20, 0)));
    }

    /// <summary>
    ///     Validates the Catmull-Rom curve: <c>alpha = 0</c> is the cardinal curve; <c>alpha = 1</c> has hand-derived chord corrections.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_CatmullRomWithAlphaZeroEqualsCardinal()
    {
        // alpha 0 gives the cardinal curve without tension (all chord lengths are 1), also with coincident points
        CurvePoint[] points = [P(0, 0), P(10, 10), P(20, 0), P(30, 10), P(35, -5)];
        Assert.Equal(Run(CurveKind.Cardinal, null, points), Run(CurveKind.CatmullRom, 0, points));
        CurvePoint[] repeated = [P(0, 0), P(0, 0), P(10, 10), P(10, 10), P(20, 0)];
        Assert.Equal(Run(CurveKind.Cardinal, null, repeated), Run(CurveKind.CatmullRom, 0, repeated));

        // alpha 1 (chordal) of A = (0,0), B = (10,0), C = (10,30): the chords have the lengths |AB| = 10 and |BC| = 30, so l^alpha = 10, 30 and l^(2 alpha) = 100, 900.
        // First segment (no chord before it, so the first control stays A): b = 2 * 900 + 3 * 30 * 10 + 100 = 2800 and m = 3 * 30 * (30 + 10) = 3600, giving the
        // second control (B * b + A * 900 - C * 100) / m = (7.5, -0.8333). Last segment (no chord after it, so the second control stays C): a = 2 * 100 + 3 * 10 * 30
        // + 900 = 2000 and n = 3 * 10 * (10 + 30) = 1200, giving the first control (B * a - A * 900 + C * 100) / n = (17.5, 2.5)
        Assert.Equal(
            "M0,0 C0,0 7.5,-0.8333 10,0 C17.5,2.5 10,30 10,30",
            Run(CurveKind.CatmullRom, 1, P(0, 0), P(10, 0), P(10, 30)));

        // The default alpha is 0.5 (centripetal); for evenly spaced collinear points all chords are equal (l^alpha = sqrt(10), l^(2 alpha) = 10), so
        // b = 2 * 10 + 3 * 10 + 10 = 60 and m = 3 * sqrt(10) * 2 * sqrt(10) = 60 and the corrections reproduce the cardinal controls, e.g. the first
        // second control (10 * 60 + 0 * 10 - 20 * 10) / 60 = 6.6667 = P1 + (P0 - P2) / 6
        Assert.Equal(
            "M0,0 C0,0 6.6667,0 10,0 C13.3333,0 16.6667,0 20,0 C23.3333,0 30,0 30,0",
            Run(CurveKind.CatmullRom, null, P(0, 0), P(10, 0), P(20, 0), P(30, 0)));
    }

    /// <summary>
    ///     Validates the monotone curve of x: the tangents keep monotone data monotone, a flat segment stays flat and coincident points are ignored.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_MonotoneXKeepsMonotoneDataMonotone()
    {
        // The symmetric peak (0,0), (10,10), (20,0): the slopes of the two intervals are 1 and -1, so the tangent at the peak is 0; the end tangent is the
        // one-sided slope (3 * 10 / 10 - 0) / 2 = 1.5; the controls are P0 + (dx, dx t0) = (3.3333, 5) and P1 - (dx, dx t1) = (6.6667, 10) with dx = 10 / 3
        Assert.Equal(
            "M0,0 C3.3333,5 6.6667,10 10,10 C13.3333,10 16.6667,5 20,0",
            Run(CurveKind.MonotoneX, null, P(0, 0), P(10, 10), P(20, 0)));

        // Monotone data (with a plateau): every Bezier has x and y controls within and in the order of its end points
        var data = new[] { (0.0, 0.0), (1.0, 1.0), (2.0, 4.0), (3.0, 9.0), (4.0, 10.0), (5.0, 10.0), (6.0, 12.0), (8.0, 20.0) };
        var segments = Segments(CurveKind.MonotoneX, null, [.. data.Select(d => P(d.Item1, d.Item2))]);
        Assert.Equal(data.Length, segments.Count);
        for (var i = 1; i < segments.Count; i++)
        {
            var (x0, y0) = data[i - 1];
            var (x1, y1) = data[i];
            var s = segments[i];
            Assert.True(x0 <= s.X1 && s.X1 <= s.X2 && s.X2 <= x1, $"x of segment {i}");
            Assert.True(y0 <= s.Y1 && s.Y1 <= s.Y2 && s.Y2 <= y1, $"y of segment {i}");
            Assert.Equal((x1, y1), (s.X, s.Y));
        }

        // The plateau (4,10) to (5,10) has zero tangents at both ends (the neighbor slope 0 limits the slope), so it stays exactly flat
        Assert.Equal(10, segments[5].Y1);
        Assert.Equal(10, segments[5].Y2);

        // Coincident points are ignored: (0,0), (0,0), (10,10) is a line; the same points alone are a single point
        Assert.Equal("M0,0 L10,10", Run(CurveKind.MonotoneX, null, P(0, 0), P(0, 0), P(10, 10)));
        Assert.Equal("M4,4 Z", Run(CurveKind.MonotoneX, null, P(4, 4), P(4, 4)));
    }

    /// <summary>
    ///     Validates that the monotone curve of y is the monotone curve of x on the swapped points with the swapped path.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_MonotoneYIsMonotoneXOfTheSwappedPoints()
    {
        // The hand-derived anchor: (0,0), (10,10), (0,20) swapped is the peak of the x curve (0,0), (10,10), (20,0), whose path is
        // C 3.3333,5 6.6667,10 10,10 and C 13.3333,10 16.6667,5 20,0; swapping each pair gives the curve of y
        Assert.Equal(
            "M0,0 C5,3.3333 10,6.6667 10,10 C10,13.3333 5,16.6667 0,20",
            Run(CurveKind.MonotoneY, null, P(0, 0), P(10, 10), P(0, 20)));

        // In general: Y of the points equals X of the swapped points with every pair of the output swapped
        CurvePoint[] points = [P(3, 0), P(5, 2), P(4, 3), P(9, 7), P(9, 8), P(2, 12), P(2, 12), P(6, 15)];
        var swapped = points.Select(p => P(p.Y, p.X)).ToArray();
        var expected = Segments(CurveKind.MonotoneX, null, swapped).Select(s => s.Kind switch
        {
            ScenePathSegmentKind.MoveTo => ScenePathSegment.Move(s.Y, s.X),
            ScenePathSegmentKind.LineTo => ScenePathSegment.Line(s.Y, s.X),
            ScenePathSegmentKind.CubicTo => ScenePathSegment.Cubic(s.Y1, s.X1, s.Y2, s.X2, s.Y, s.X),
            _ => s,
        }).ToList();
        Assert.Equal(expected, Segments(CurveKind.MonotoneY, null, points));
    }

    /// <summary>
    ///     Validates the natural spline against the solution of the tri-diagonal system.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_NaturalMatchesTheHandDerivedControlPoints()
    {
        // x = 0, 10, 20 (n = 2 intervals): the rows 2 a0 + ... and the elimination give a1 = 80 / 6 = 13.3333 and a0 = (20 - a1) / 2 = 3.3333, so the first controls are
        // 3.3333 and 2 * 10 - a1 = 6.6667 and the second interval has 13.3333 and (20 + a1) / 2 = 16.6667; y = 0, 10, 0 gives a1 = 10, a0 = 5 and the controls 5, 10 | 10, 5
        Assert.Equal(
            "M0,0 C3.3333,5 6.6667,10 10,10 C13.3333,10 16.6667,5 20,0",
            Run(CurveKind.Natural, null, P(0, 0), P(10, 10), P(20, 0)));

        // Four collinear, evenly spaced points (the inner rows 1, 4, 1 take part): the natural spline is the straight line, with the controls at the thirds
        Assert.Equal(
            "M0,0 C3.3333,0 6.6667,0 10,0 C13.3333,0 16.6667,0 20,0 C23.3333,0 26.6667,0 30,0",
            Run(CurveKind.Natural, null, P(0, 0), P(10, 0), P(20, 0), P(30, 0)));

        // Two points are a line (no Bezier), one is a lone point
        Assert.Equal("M1,2 L3,4", Run(CurveKind.Natural, null, P(1, 2), P(3, 4)));
        Assert.Equal("M1,2 Z", Run(CurveKind.Natural, null, P(1, 2)));
    }
}
