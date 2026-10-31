using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Tests of <see cref="CurveBuilder"/>: the names, the linear, step, basis and bundle curves, the gaps of a line, the
///     degenerate point counts, the segment bound, cancellation and the guarantee that no coordinate is ever not finite. Every
///     expected path is derived by hand from the definition of the d3-shape curve (the formulas are quoted in each test).
/// </summary>
public class CurveBuilderTests
{
    /// <summary>The names of Vega-Lite's <c>Interpolate</c> in the order of <see cref="CurveKind"/> (monotone is <c>MonotoneX</c>).</summary>
    private static readonly (string Name, CurveKind Kind)[] Names =
    [
        ("linear", CurveKind.Linear),
        ("linear-closed", CurveKind.LinearClosed),
        ("step", CurveKind.Step),
        ("step-before", CurveKind.StepBefore),
        ("step-after", CurveKind.StepAfter),
        ("basis", CurveKind.Basis),
        ("basis-open", CurveKind.BasisOpen),
        ("basis-closed", CurveKind.BasisClosed),
        ("bundle", CurveKind.Bundle),
        ("cardinal", CurveKind.Cardinal),
        ("cardinal-open", CurveKind.CardinalOpen),
        ("cardinal-closed", CurveKind.CardinalClosed),
        ("catmull-rom", CurveKind.CatmullRom),
        ("monotone", CurveKind.MonotoneX),
        ("natural", CurveKind.Natural),
    ];

    /// <summary>A defined point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The point.</returns>
    internal static CurvePoint P(double x, double y) => new(x, y, true);

    /// <summary>An undefined point (it ends the current subpath).</summary>
    internal static readonly CurvePoint Gap = new(0, 0, false);

    /// <summary>
    ///     Rounds a number to four decimals the way the expected strings are written (no negative zero).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Num(double value) =>
        (Math.Round(value, 4, MidpointRounding.AwayFromZero) + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Formats segments as an SVG-like text: <c>M x,y</c>, <c>L x,y</c>, <c>C x1,y1 x2,y2 x,y</c> and <c>Z</c>.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The text.</returns>
    internal static string Text(IEnumerable<ScenePathSegment> segments) => string.Join(
        ' ',
        segments.Select(s => s.Kind switch
        {
            ScenePathSegmentKind.MoveTo => $"M{Num(s.X)},{Num(s.Y)}",
            ScenePathSegmentKind.LineTo => $"L{Num(s.X)},{Num(s.Y)}",
            ScenePathSegmentKind.CubicTo => $"C{Num(s.X1)},{Num(s.Y1)} {Num(s.X2)},{Num(s.Y2)} {Num(s.X)},{Num(s.Y)}",
            _ => "Z",
        }));

    /// <summary>
    ///     Generates the segments of a line.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <param name="tension">The tension.</param>
    /// <param name="points">The points.</param>
    /// <returns>The segments.</returns>
    internal static List<ScenePathSegment> Segments(CurveKind kind, double? tension, params CurvePoint[] points)
    {
        var output = new List<ScenePathSegment>();
        CurveBuilder.Line(kind, tension, points, output, CancellationToken.None);
        return output;
    }

    /// <summary>
    ///     Generates the text of a line.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <param name="tension">The tension.</param>
    /// <param name="points">The points.</param>
    /// <returns>The text.</returns>
    internal static string Run(CurveKind kind, double? tension, params CurvePoint[] points) => Text(Segments(kind, tension, points));

    /// <summary>
    ///     Validates that the linear curve is a polyline and the closed one adds the closing command.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_LinearAndLinearClosedMatchTheAnchors()
    {
        // Arrange: three points (0,0), (10,10), (20,0)
        CurvePoint[] points = [P(0, 0), P(10, 10), P(20, 0)];

        // Act / Assert: lineStart moves to the first point, every other point is a line; the closed curve ends with closePath
        Assert.Equal("M0,0 L10,10 L20,0", Run(CurveKind.Linear, null, points));
        Assert.Equal("M0,0 L10,10 L20,0 Z", Run(CurveKind.LinearClosed, null, points));

        // The tension is ignored by both
        Assert.Equal("M0,0 L10,10 L20,0", Run(CurveKind.Linear, 0.3, points));
    }

    /// <summary>
    ///     Validates the three step curves, including the final line of the centered step (the flush of <c>lineEnd</c>).
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_StepVariantsMatchTheHandDerivedAnchors()
    {
        // Arrange: (0,0), (10,10), (20,5)
        CurvePoint[] points = [P(0, 0), P(10, 10), P(20, 5)];

        // Act / Assert: with t = 0.5 each interval changes at its middle x1 = x0 (1 - t) + x t (5 and 15): a line at the old y to x1, a line to the new y;
        // lineEnd adds the last line to the final point because 0 < t < 1 and two points were seen
        Assert.Equal("M0,0 L5,0 L5,10 L15,10 L15,5 L20,5", Run(CurveKind.Step, null, points));

        // t = 1 (after): the change is at the new x; t = 0 (before): the change is at the old x; neither needs the flush
        Assert.Equal("M0,0 L10,0 L10,10 L20,10 L20,5", Run(CurveKind.StepAfter, null, points));
        Assert.Equal("M0,0 L0,10 L10,10 L10,5 L20,5", Run(CurveKind.StepBefore, null, points));
    }

    /// <summary>
    ///     Validates the basis curves against the Bezier formulas of the uniform cubic B-spline.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_BasisFamilyMatchesTheHandDerivedBeziers()
    {
        // basis of (0,0), (30,30), (60,0): the first knot is a sixth of the way ((5 P0 + P1) / 6 = (5,5)), a segment with the controls
        // (2 P0 + P1) / 3 = (10,10), (P0 + 2 P1) / 3 = (20,20) and the knot (P0 + 4 P1 + P2) / 6 = (30,20); the last segment is the same
        // formula with the last point duplicated: (40,20), (50,10), (P1 + 4 P2 + P2) / 6 = (55,5); a line to the last point ends the path
        Assert.Equal(
            "M0,0 L5,5 C10,10 20,20 30,20 C40,20 50,10 55,5 L60,0",
            Run(CurveKind.Basis, null, P(0, 0), P(30, 30), P(60, 0)));

        // basis-open of (0,0), (30,30), (60,0), (90,30): the path starts at the first knot (P0 + 4 P1 + P2) / 6 = (30,20) and has
        // one segment: controls (40,20), (50,10) and the knot (P1 + 4 P2 + P3) / 6 = (60,10)
        Assert.Equal(
            "M30,20 C40,20 50,10 60,10",
            Run(CurveKind.BasisOpen, null, P(0, 0), P(30, 30), P(60, 0), P(90, 30)));

        // basis-closed of (0,0), (30,30), (60,0): the knots are (30,20), (45,5) = (P1 + 4 P2 + P0) / 6 and (15,5) = (P2 + 4 P0 + P1) / 6,
        // each reached by the controls (2 A + B) / 3 and (A + 2 B) / 3 of the surrounding points; the loop returns to (30,20) and has no close
        Assert.Equal(
            "M30,20 C40,20 50,10 45,5 C40,0 20,0 15,5 C10,10 20,20 30,20",
            Run(CurveKind.BasisClosed, null, P(0, 0), P(30, 30), P(60, 0)));
    }

    /// <summary>
    ///     Validates the bundle curve: <c>beta = 1</c> is the basis curve, <c>beta = 0</c> straightens the points onto the chord.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_BundleWithBetaOneEqualsBasis()
    {
        // beta = 1 is the basis curve for any points
        CurvePoint[] points = [P(0, 0), P(30, 17), P(60, -4), P(75, 20), P(90, 3)];
        Assert.Equal(Run(CurveKind.Basis, null, points), Run(CurveKind.Bundle, 1, points));

        // beta = 0: every point moves onto the chord from the first to the last point, here y = 0, x = 0, 30, 60: a basis curve of collinear points
        CurvePoint[] triangle = [P(0, 0), P(30, 30), P(60, 0)];
        Assert.Equal("M0,0 L5,0 C10,0 20,0 30,0 C40,0 50,0 55,0 L60,0", Run(CurveKind.Bundle, 0, triangle));

        // The default beta is 0.85: the middle point becomes 0.85 * 30 + 0.15 * 0 = 25.5, so the basis curve of (0,0), (30,25.5), (60,0) is drawn
        Assert.Equal("M0,0 L5,4.25 C10,8.5 20,17 30,17 C40,17 50,8.5 55,4.25 L60,0", Run(CurveKind.Bundle, null, triangle));

        // A value that is not a number counts as absent
        Assert.Equal(Run(CurveKind.Bundle, null, triangle), Run(CurveKind.Bundle, double.NaN, triangle));
    }

    /// <summary>
    ///     Validates that every undefined point ends the subpath and the next defined point starts a new one.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_UndefinedPointsStartNewSubpaths()
    {
        // Two runs separated by a gap
        Assert.Equal(
            "M0,0 L10,10 M30,0 L40,10",
            Run(CurveKind.Linear, null, P(0, 0), P(10, 10), Gap, P(30, 0), P(40, 10)));

        // Leading and trailing gaps do nothing; a run of one point is a move and a close
        Assert.Equal(
            "M0,0 Z M5,5 L6,6",
            Run(CurveKind.Linear, null, Gap, P(0, 0), Gap, P(5, 5), P(6, 6), Gap));

        // A point with a coordinate that is not finite is a gap, too
        Assert.Equal(
            "M0,0 Z M10,10 Z",
            Run(CurveKind.Linear, null, P(0, 0), P(double.NaN, 1), P(10, 10)));
        Assert.Equal(
            "M0,0 L10,10 M30,0 L40,10",
            Run(CurveKind.Natural, null, P(0, 0), P(10, 10), P(5, double.PositiveInfinity), P(30, 0), P(40, 10)));

        // A step curve flushes each run on its own
        Assert.Equal(
            "M0,0 L5,0 L5,10 L10,10 M20,0 L25,0 L25,5 L30,5",
            Run(CurveKind.Step, null, P(0, 0), P(10, 10), Gap, P(20, 0), P(30, 5)));
    }

    /// <summary>
    ///     Validates the point counts that are too small for a curve (none, one, two) of every kind as d3-shape draws them.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_DegenerateCountsFollowD3()
    {
        var kinds = Enum.GetValues<CurveKind>();

        // No point: nothing at all
        Assert.All(kinds, kind => Assert.Equal(string.Empty, Run(kind, null)));

        // One point: a move and a close for the curves that draw the first point at once (lineEnd closes a lone point), nothing for the open
        // curves (they need three or four points before they draw) and for the bundle (it feeds the basis curve only with two or more points)
        (CurveKind Kind, string Text)[] onePoint =
        [
            (CurveKind.Linear, "M3,4 Z"), (CurveKind.LinearClosed, "M3,4 Z"), (CurveKind.Step, "M3,4 Z"), (CurveKind.StepBefore, "M3,4 Z"),
            (CurveKind.StepAfter, "M3,4 Z"), (CurveKind.Basis, "M3,4 Z"), (CurveKind.BasisOpen, string.Empty), (CurveKind.BasisClosed, "M3,4 Z"),
            (CurveKind.Bundle, string.Empty), (CurveKind.Cardinal, "M3,4 Z"), (CurveKind.CardinalOpen, string.Empty), (CurveKind.CardinalClosed, "M3,4 Z"),
            (CurveKind.CatmullRom, "M3,4 Z"), (CurveKind.MonotoneX, "M3,4 Z"), (CurveKind.MonotoneY, "M3,4 Z"), (CurveKind.Natural, "M3,4 Z"),
        ];
        Assert.Equal(
            onePoint.Select(e => (e.Kind, e.Text)),
            kinds.Select(kind => (kind, Run(kind, null, P(3, 4)))));

        // Two points (0,0), (10,10): a line for most curves; the centered step has its three lines, step-after and step-before two; the open curves draw
        // nothing; basis-closed has the points a third of the way ((P0 + 2 P1) / 3 and (P1 + 2 P0) / 3) and cardinal-closed the loop P1, P0
        (CurveKind Kind, string Text)[] twoPoints =
        [
            (CurveKind.Linear, "M0,0 L10,10"), (CurveKind.LinearClosed, "M0,0 L10,10 Z"), (CurveKind.Step, "M0,0 L5,0 L5,10 L10,10"),
            (CurveKind.StepBefore, "M0,0 L0,10 L10,10"), (CurveKind.StepAfter, "M0,0 L10,0 L10,10"), (CurveKind.Basis, "M0,0 L10,10"),
            (CurveKind.BasisOpen, string.Empty), (CurveKind.BasisClosed, "M6.6667,6.6667 L3.3333,3.3333 Z"), (CurveKind.Bundle, "M0,0 L10,10"),
            (CurveKind.Cardinal, "M0,0 L10,10"), (CurveKind.CardinalOpen, string.Empty), (CurveKind.CardinalClosed, "M10,10 L0,0 Z"),
            (CurveKind.CatmullRom, "M0,0 L10,10"), (CurveKind.MonotoneX, "M0,0 L10,10"), (CurveKind.MonotoneY, "M0,0 L10,10"),
            (CurveKind.Natural, "M0,0 L10,10"),
        ];
        Assert.Equal(
            twoPoints.Select(e => (e.Kind, e.Text)),
            kinds.Select(kind => (kind, Run(kind, null, P(0, 0), P(10, 10)))));

        // Three points of the open curves: basis-open and cardinal-open only draw the move to the first knot and close it
        Assert.Equal("M30,20 Z", Run(CurveKind.BasisOpen, null, P(0, 0), P(30, 30), P(60, 0)));
        Assert.Equal("M10,10 Z", Run(CurveKind.CardinalOpen, null, P(0, 0), P(10, 10), P(20, 0)));
    }

    /// <summary>
    ///     Validates the 15 names of Vega-Lite and that everything else is rejected.
    /// </summary>
    [Fact]
    public void CurveBuilder_TryParse_AcceptsTheFifteenVegaLiteNamesAndRejectsOthers()
    {
        // The 15 names map to their curves
        Assert.Equal(15, Names.Length);
        foreach (var (name, expected) in Names)
        {
            Assert.True(CurveBuilder.TryParse(name, false, out var kind), name);
            Assert.Equal(expected, kind);
        }

        // Other names are rejected: other cases, the Vega-only curves, nothing at all
        foreach (var name in new[] { string.Empty, "Linear", "LINEAR", "linear ", "catmull-rom-open", "catmull-rom-closed", "monotone-x", "cubic", "none" })
        {
            Assert.False(CurveBuilder.TryParse(name, false, out _), name);
        }

        Assert.False(CurveBuilder.TryParse(null, false, out _));
    }

    /// <summary>
    ///     Validates that <c>monotone</c> is the curve of y for a horizontal area only.
    /// </summary>
    [Fact]
    public void CurveBuilder_TryParse_MonotoneFollowsTheAreaOrientation()
    {
        // A vertical mark (and every line) has the monotone curve of x
        Assert.True(CurveBuilder.TryParse("monotone", false, out var vertical));
        Assert.Equal(CurveKind.MonotoneX, vertical);

        // A horizontal area has the monotone curve of y
        Assert.True(CurveBuilder.TryParse("monotone", true, out var horizontal));
        Assert.Equal(CurveKind.MonotoneY, horizontal);

        // The other names do not depend on the orientation
        Assert.True(CurveBuilder.TryParse("natural", true, out var natural));
        Assert.Equal(CurveKind.Natural, natural);
    }

    /// <summary>
    ///     Validates that the segment bound is never below what the generators write, for every kind, point count, gap pattern, and for lines and areas.
    /// </summary>
    [Fact]
    public void CurveBuilder_MaxSegments_IsAnUpperBoundForEveryKind()
    {
        var checkedCases = 0;
        foreach (var kind in Enum.GetValues<CurveKind>())
        {
            for (var count = 0; count <= 12; count++)
            {
                // Every defined/undefined pattern for up to 8 points, a few patterns for more
                var masks = count <= 8
                    ? Enumerable.Range(0, 1 << count).ToArray()
                    : [(1 << count) - 1, 0b101010101010 & ((1 << count) - 1), 0b110110110110 & ((1 << count) - 1), 0b111011101111 & ((1 << count) - 1)];
                foreach (var mask in masks)
                {
                    // The bumpy points make the curves non-degenerate; the defined count and the runs follow from the mask
                    var defined = 0;
                    var runs = 0;
                    var line = new CurvePoint[count];
                    var area = new AreaPoint[count];
                    for (var i = 0; i < count; i++)
                    {
                        var isDefined = (mask & (1 << i)) != 0;
                        defined += isDefined ? 1 : 0;
                        runs += isDefined && (i == 0 || (mask & (1 << (i - 1))) == 0) ? 1 : 0;
                        var x = (i * 3.0) + ((i % 2) * 0.5);
                        var y = ((i * 7) % 5) + (i % 3);
                        line[i] = new CurvePoint(x, y, isDefined);
                        area[i] = new AreaPoint(x, y, -1, isDefined);
                    }

                    var output = new List<ScenePathSegment>();
                    CurveBuilder.Line(kind, null, line, output, CancellationToken.None);
                    Assert.True(output.Count <= CurveBuilder.MaxSegments(kind, defined, runs, false), $"{kind} line n={count} mask={mask}: {output.Count}");

                    if (CurveBuilder.SupportsArea(kind))
                    {
                        foreach (var horizontal in new[] { false, true })
                        {
                            output.Clear();
                            CurveBuilder.Area(kind, null, horizontal, area, output, CancellationToken.None);
                            Assert.True(output.Count <= CurveBuilder.MaxSegments(kind, defined, runs, true), $"{kind} area n={count} mask={mask}: {output.Count}");
                        }
                    }

                    checkedCases++;
                }
            }
        }

        // The sweep is large enough to mean something; the edges of the formula
        Assert.True(checkedCases > 4000);
        Assert.Equal(0, CurveBuilder.MaxSegments(CurveKind.Linear, 0, 0, false));
        Assert.Equal(0, CurveBuilder.MaxSegments(CurveKind.Basis, -5, 3, true));
        Assert.Equal(long.MaxValue, CurveBuilder.MaxSegments(CurveKind.Step, long.MaxValue, 1, true));
        Assert.Equal((2 * 5 * 2) + 6, CurveBuilder.MaxSegments(CurveKind.Step, 5, 1, true));
        Assert.Equal(5 + 6, CurveBuilder.MaxSegments(CurveKind.Natural, 5, 1, false));
    }

    /// <summary>
    ///     Validates that the generators stop on a canceled token, however few the points.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_PollsCancellationEvery1024Points()
    {
        // Arrange: a canceled token and more than two polling intervals of points
        using var source = new CancellationTokenSource();
        source.Cancel();
        var points = Enumerable.Range(0, 3000).Select(i => P(i, i % 7)).ToArray();

        // Act / Assert: the line and the area (the same loop) throw OperationCanceledException and stop before writing the whole path
        var output = new List<ScenePathSegment>();
        Assert.Throws<OperationCanceledException>(() => CurveBuilder.Line(CurveKind.Linear, null, points, output, source.Token));
        Assert.True(output.Count < 3000);
        Assert.Throws<OperationCanceledException>(() => CurveBuilder.Line(CurveKind.Natural, null, [P(0, 0)], output, source.Token));
        Assert.Throws<OperationCanceledException>(() => CurveBuilder.Area(
            CurveKind.Linear, null, false, points.Select(p => new AreaPoint(p.X, p.Y, 0, true)).ToArray(), output, source.Token));

        // A token that is not canceled lets the long line through
        output.Clear();
        CurveBuilder.Line(CurveKind.Linear, null, points, output, CancellationToken.None);
        Assert.Equal(3000, output.Count);
    }

    /// <summary>
    ///     Validates that no curve ever writes a coordinate that is not finite, whatever the input.
    /// </summary>
    [Fact]
    public void CurveBuilder_Line_NeverEmitsNonFiniteCoordinates()
    {
        // Arrange: not-a-number and infinite coordinates (gaps), coincident points (zero chord lengths of the Catmull-Rom curve, flat monotone
        // intervals) and huge coordinates whose control points overflow
        double[] huge = [1e308, -1e308, 1e308, -1e308, 1e308];
        List<CurvePoint[]> inputs =
        [
            [P(0, 0), P(double.NaN, 3), P(5, 5), P(double.PositiveInfinity, 1), P(7, 1), P(8, double.NegativeInfinity), P(9, 9)],
            [P(1, 1), P(1, 1), P(1, 1), P(1, 1), P(2, 2), P(2, 2), P(3, 1)],
            [P(0, 0), P(0, 5), P(0, 5), P(0, 9), P(0, 9)],
            [.. huge.Select((v, i) => P(v, huge[(i + 1) % huge.Length]))],
            [.. huge.Select((v, i) => P(i, v))],
        ];

        // Act / Assert: every segment of every curve and tension is finite
        foreach (var input in inputs)
        {
            foreach (var kind in Enum.GetValues<CurveKind>())
            {
                foreach (double? tension in new double?[] { null, 0, 1, -3, 7, double.NaN })
                {
                    foreach (var segment in Segments(kind, tension, input))
                    {
                        Assert.True(
                            double.IsFinite(segment.X) && double.IsFinite(segment.Y) && double.IsFinite(segment.X1) && double.IsFinite(segment.Y1)
                            && double.IsFinite(segment.X2) && double.IsFinite(segment.Y2),
                            $"{kind} {tension}: {segment}");
                    }
                }
            }
        }
    }
}
