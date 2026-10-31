using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of <see cref="ArcGeometry"/>: the outline of a sector, derived by hand from the definition of the d3-shape
///     arc (the angle <c>a = angle - π/2</c>, the end points <c>(r cos a, r sin a)</c>, the pad angle
///     <c>asin(rp / r * sin(pad / 2))</c> with <c>rp = sqrt(r0² + r1²)</c>, the corner circles tangent to the edge and the ring).
/// </summary>
public class ArcGeometryTests
{
    /// <summary>The tolerance for coordinates (the path stores single precision numbers).</summary>
    private const double Tolerance = 1e-4;

    /// <summary>Half of pi.</summary>
    private const double HalfPi = Math.PI / 2;

    /// <summary>
    ///     Lists the commands of a path as readable tuples.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The kind, end point, radius, large-arc flag and sweep flag of each command; the move of each subpath is a tuple of its own.</returns>
    private static List<(string Kind, double X, double Y, double R, bool Large, bool Sweep)> Commands(Path path)
    {
        var result = new List<(string, double, double, double, bool, bool)>();
        foreach (var subpath in path.Subpaths)
        {
            result.Add(("M", subpath.Start.X, subpath.Start.Y, 0, false, false));
            foreach (var command in subpath.Commands)
            {
                result.Add(command.Type switch
                {
                    PathCommandType.LineTo => ("L", command.EndPoint.X, command.EndPoint.Y, 0, false, false),
                    PathCommandType.ArcTo => ("A", command.EndPoint.X, command.EndPoint.Y, command.Radius.X, command.LargeArc, command.Sweep),
                    PathCommandType.Close => ("Z", 0, 0, 0, false, false),
                    _ => ("?", 0, 0, 0, false, false),
                });
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the area that a path covers by filling it into a surface and adding up the coverage.
    /// </summary>
    /// <param name="path">The path centered on the origin.</param>
    /// <param name="half">Half the side of the square surface in pixels.</param>
    /// <returns>The covered area in square pixels.</returns>
    private static double FilledArea(Path path, int half)
    {
        using var surface = new Surface(2 * half, 2 * half);
        PathFiller.Fill(surface, path.Transform(Matrix3x2.CreateTranslation(half, half)), new Rgba32(0, 0, 0, 255), FillRule.NonZero, 0.02f);
        double total = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                total += surface[x, y].A / 255.0;
            }
        }

        return total;
    }

    /// <summary>
    ///     Validates the commands of a quarter pie slice: the point above the center, a clockwise arc to the right and the line back.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_QuarterSectorHasHandDerivedCommands()
    {
        // Arrange / Act: r0 = 0, r1 = 10, from 12 o'clock (0) to 3 o'clock (pi / 2): a0 = -pi/2, a1 = 0
        var commands = Commands(ArcGeometry.Build(0, 10, 0, HalfPi, 0, 0));

        // Assert: M(r1 cos a0, r1 sin a0) = (0, -10), A10,10 small sweep clockwise to (10, 0), L(0, 0), Z
        Assert.Equal(["M", "A", "L", "Z"], commands.Select(c => c.Kind));
        Assert.Equal(0, commands[0].X, Tolerance);
        Assert.Equal(-10, commands[0].Y, Tolerance);
        Assert.Equal(10, commands[1].R, Tolerance);
        Assert.False(commands[1].Large);
        Assert.True(commands[1].Sweep);
        Assert.Equal(10, commands[1].X, Tolerance);
        Assert.Equal(0, commands[1].Y, Tolerance);
        Assert.Equal((0.0, 0.0), (commands[2].X, commands[2].Y));

        // Assert: a sector of more than half a circle uses the large-arc flag (pi + 0.5 from 0): r = 10
        var large = Commands(ArcGeometry.Build(0, 10, 0, Math.PI + 0.5, 0, 0));
        Assert.True(large[1].Large);
        Assert.True(large[1].Sweep);

        // Assert: the end point is (r sin(end), -r cos(end)) in the usual clock convention
        Assert.Equal(10 * Math.Sin(Math.PI + 0.5), large[1].X, Tolerance);
        Assert.Equal(-10 * Math.Cos(Math.PI + 0.5), large[1].Y, Tolerance);
    }

    /// <summary>
    ///     Validates the filled area of sectors against 0.5 * (r1² - r0²) * angle.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_FlattenedAreaMatchesTheSectorFormula()
    {
        // Arrange / Act / Assert: a quarter pie of radius 100: 0.5 * 100² * pi/2 = 7853.98
        var quarter = FilledArea(ArcGeometry.Build(0, 100, 0, HalfPi, 0, 0), 110);
        Assert.Equal(0.5 * 100 * 100 * HalfPi, quarter, 0.005 * 0.5 * 100 * 100 * HalfPi);

        // Assert: an annulus sector of 1.7 radians: 0.5 * (100² - 40²) * 1.7 = 7140
        var annular = FilledArea(ArcGeometry.Build(40, 100, 0.3, 2.0, 0, 0), 110);
        Assert.Equal(0.5 * ((100 * 100) - (40 * 40)) * 1.7, annular, 0.005 * 7140);

        // Assert: the same sector with the angles reversed covers the same area
        var reversed = FilledArea(ArcGeometry.Build(40, 100, 2.0, 0.3, 0, 0), 110);
        Assert.Equal(annular, reversed, 1.0);

        // Assert: a pad angle removes a strip of constant width, so the area is smaller than without padding
        var padded = FilledArea(ArcGeometry.Build(40, 100, 0.3, 2.0, 0.1, 0), 110);
        Assert.True(padded < annular);
    }

    /// <summary>
    ///     Validates the complete circle and the annulus: two subpaths with opposite directions, so the nonzero fill leaves a hole.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_FullCircleAndAnnulusUseOppositeWindingSubpaths()
    {
        // Arrange / Act: a full disc has one subpath of two half circles; an annulus has two subpaths
        var disc = Commands(ArcGeometry.Build(0, 50, 0, 2 * Math.PI, 0, 0));
        var ring = ArcGeometry.Build(20, 50, 0, 2 * Math.PI, 0, 0);
        var ringCommands = Commands(ring);

        // Assert: the disc is M, A, A, Z; both half circles are large-arc flagged clockwise arcs of radius 50 that end opposite and back at the start
        Assert.Equal(["M", "A", "A", "Z"], disc.Select(c => c.Kind));
        Assert.Equal((0.0, -50.0), (Math.Round(disc[0].X, 3), disc[0].Y));
        Assert.Equal((0.0, 50.0), (Math.Round(disc[1].X, 3), disc[1].Y));
        Assert.Equal((0.0, -50.0), (Math.Round(disc[2].X, 3), disc[2].Y));
        Assert.All(disc.Where(c => c.Kind == "A"), c => Assert.Equal((50.0, true, true), (c.R, c.Large, c.Sweep)));

        // Assert: the annulus has an outer clockwise and an inner counter-clockwise circle (the sweep flags are opposite)
        Assert.Equal(2, ring.Subpaths.Count);
        Assert.Equal(["M", "A", "A", "M", "A", "A", "Z"], ringCommands.Select(c => c.Kind));
        Assert.Equal((50.0, true), (ringCommands[1].R, ringCommands[1].Sweep));
        Assert.Equal((20.0, false), (ringCommands[4].R, ringCommands[4].Sweep));

        // Assert: the nonzero fill leaves the hole empty and fills the ring (a 120 x 120 surface, center at 60, 60)
        using var surface = new Surface(120, 120);
        PathFiller.Fill(surface, ring.Transform(Matrix3x2.CreateTranslation(60, 60)), new Rgba32(0, 0, 0, 255), FillRule.NonZero, 0.02f);
        Assert.Equal(0, surface[60, 60].A);
        Assert.Equal(0, surface[70, 60].A);
        Assert.Equal(255, surface[95, 60].A);
        Assert.Equal(255, surface[60, 25].A);
        Assert.Equal(0, surface[115, 60].A);
        Assert.Equal(Math.PI * ((50 * 50) - (20 * 20)), FilledArea(ring, 60), 0.005 * Math.PI * 2100);
    }

    /// <summary>
    ///     Validates that the radii are swapped when the outer one is smaller and that reversed angles run counter-clockwise.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_SwapsRadiiAndAcceptsReversedAngles()
    {
        // Arrange / Act: swapped radii give exactly the commands of the ordered radii
        var ordered = Commands(ArcGeometry.Build(20, 50, 0.4, 1.9, 0, 0));
        var swapped = Commands(ArcGeometry.Build(50, 20, 0.4, 1.9, 0, 0));

        // Assert
        Assert.Equal(ordered, swapped);

        // Arrange / Act: from pi/2 to 0 the end angle is smaller: a0 = 0, a1 = -pi/2, counter-clockwise
        var reversed = Commands(ArcGeometry.Build(0, 10, HalfPi, 0, 0, 0));

        // Assert: M(10, 0), A10,10 small counter-clockwise (sweep false) to (0, -10), L(0, 0), Z
        Assert.Equal(["M", "A", "L", "Z"], reversed.Select(c => c.Kind));
        Assert.Equal((10.0, 0.0), (Math.Round(reversed[0].X, 3), Math.Round(reversed[0].Y, 3)));
        Assert.False(reversed[1].Sweep);
        Assert.False(reversed[1].Large);
        Assert.Equal(0, reversed[1].X, Tolerance);
        Assert.Equal(-10, reversed[1].Y, Tolerance);

        // Assert: a negative radius counts as zero
        Assert.Equal(Commands(ArcGeometry.Build(0, 10, 0, 1, 0, 0)), Commands(ArcGeometry.Build(-5, 10, 0, 1, 0, 0)));
    }

    /// <summary>
    ///     Validates the pad angle: the edges move by the derived angles, and the straight gap has the same width at both radii.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_PadAngleMovesTheEdgesByTheDerivedAngles()
    {
        // Arrange: pad 0.1, r0 = 20, r1 = 50, from 0 to pi/2 (a0 = -pi/2, a1 = 0): ap = 0.05, rp = sqrt(400 + 2500)
        const double pad = 0.1;
        const double r0 = 20;
        const double r1 = 50;
        var rp = Math.Sqrt((r0 * r0) + (r1 * r1));
        var p0 = Math.Asin(rp / r0 * Math.Sin(pad / 2));
        var p1 = Math.Asin(rp / r1 * Math.Sin(pad / 2));
        var a0 = -HalfPi;
        var a1 = 0.0;

        // Act
        var commands = Commands(ArcGeometry.Build(r0, r1, 0, HalfPi, pad, 0));

        // Assert: M outer start at a0 + p1, A outer to a1 - p1, L inner start at a1 - p0, A inner back to a0 + p0, Z
        Assert.Equal(["M", "A", "L", "A", "Z"], commands.Select(c => c.Kind));
        Assert.Equal(r1 * Math.Cos(a0 + p1), commands[0].X, Tolerance);
        Assert.Equal(r1 * Math.Sin(a0 + p1), commands[0].Y, Tolerance);
        Assert.Equal((r1, true), (commands[1].R, commands[1].Sweep));
        Assert.Equal(r1 * Math.Cos(a1 - p1), commands[1].X, Tolerance);
        Assert.Equal(r1 * Math.Sin(a1 - p1), commands[1].Y, Tolerance);
        Assert.Equal(r0 * Math.Cos(a1 - p0), commands[2].X, Tolerance);
        Assert.Equal(r0 * Math.Sin(a1 - p0), commands[2].Y, Tolerance);
        Assert.Equal((r0, false), (commands[3].R, commands[3].Sweep));
        Assert.Equal(r0 * Math.Cos(a0 + p0), commands[3].X, Tolerance);
        Assert.Equal(r0 * Math.Sin(a0 + p0), commands[3].Y, Tolerance);

        // Assert: the start edge runs along the line x = 0, and both of its end points are rp * sin(pad / 2) = 2.6915 away from it
        var gap = rp * Math.Sin(pad / 2);
        Assert.Equal(gap, commands[0].X, Tolerance);
        Assert.Equal(gap, commands[3].X, Tolerance);
        Assert.Equal(2.6915, gap, 1e-3);

        // Assert: the inner angle shift is larger than the outer one, as the inner arc is shorter
        Assert.True(p0 > p1);

        // Assert: a pad angle below zero counts as none
        Assert.Equal(Commands(ArcGeometry.Build(r0, r1, 0, HalfPi, 0, 0)), Commands(ArcGeometry.Build(r0, r1, 0, HalfPi, -0.3, 0)));
    }

    /// <summary>
    ///     Validates the corner radius: corner arcs of the requested radius tangent to the edge and the ring, and the clamp to half the ring width.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_CornerRadiusRoundsTheCornersAndClampsToHalfTheRing()
    {
        // Arrange / Act: r0 = 20, r1 = 50, a quarter turn from 12 o'clock, corner radius 5
        var commands = Commands(ArcGeometry.Build(20, 50, 0, HalfPi, 0, 5));

        // Assert: outer M, three arcs (corner, ring, corner), then L and three inner arcs, Z
        Assert.Equal(["M", "A", "A", "A", "L", "A", "A", "A", "Z"], commands.Select(c => c.Kind));
        Assert.Equal([5.0, 50.0, 5.0, 5.0, 20.0, 5.0], commands.Where(c => c.Kind == "A").Select(c => Math.Round(c.R, 4)));

        // Assert: the outer corner circle is tangent to the start edge (x = 0) and the outer ring: its center is 5 from the edge and
        // 45 from the origin, so the tangent point on the edge is at y = -sqrt(45² - 5²) = -44.7214
        Assert.Equal(0, commands[0].X, Tolerance);
        Assert.Equal(-Math.Sqrt(2000), commands[0].Y, Tolerance);

        // Assert: the arc ends on the outer ring at the center scaled to the ring radius: (5, -44.7214) * 50 / 45
        Assert.Equal(5 * 50 / 45.0, commands[1].X, Tolerance);
        Assert.Equal(-Math.Sqrt(2000) * 50 / 45.0, commands[1].Y, Tolerance);

        // Assert: the inner corner at the start edge is tangent to the edge and the inner ring from outside: its center is
        // 5 from the edge and 25 from the origin, so the last point is on the edge at y = -sqrt(25² - 5²) = -24.4949
        Assert.Equal(0, commands[7].X, Tolerance);
        Assert.Equal(-Math.Sqrt(600), commands[7].Y, Tolerance);

        // Assert: a corner radius above half the ring width (r1 - r0) / 2 = 5 is clamped to it
        var clamped = Commands(ArcGeometry.Build(20, 30, 0, HalfPi, 0, 100));
        Assert.Equal(Commands(ArcGeometry.Build(20, 30, 0, HalfPi, 0, 5)), clamped);
        Assert.All(clamped.Where(c => c.Kind == "A" && Math.Abs(c.R - 20) > Tolerance && Math.Abs(c.R - 30) > Tolerance), c => Assert.True(c.R <= 5 + Tolerance));

        // Assert: a corner radius below zero counts as none
        Assert.Equal(Commands(ArcGeometry.Build(20, 50, 0, HalfPi, 0, 0)), Commands(ArcGeometry.Build(20, 50, 0, HalfPi, 0, -4)));
    }

    /// <summary>
    ///     Validates that the method is total: points, collapsed sectors and inputs that are not finite or too large.
    /// </summary>
    [Fact]
    public void ArcGeometry_Build_DegenerateAndNonFiniteInputsGiveEmptyOrPointPaths()
    {
        // Arrange / Act: an outer radius of zero is a point: one closed subpath that only has the move to the origin
        var point = ArcGeometry.Build(0, 0, 0, 1, 0, 0);

        // Assert
        Assert.Single(point.Subpaths);
        Assert.Equal((0.0, 0.0), (point.Subpaths[0].Start.X, point.Subpaths[0].Start.Y));
        Assert.Equal(["M", "Z"], Commands(point).Select(c => c.Kind));
        Assert.Equal(Commands(point), Commands(ArcGeometry.Build(5e-13, 1e-13, 0, 1, 0, 0)));

        // Assert: a sector of zero angle collapses to its edge: M(r1 sin(1), -r1 cos(1)), L(0, 0), Z
        var edge = Commands(ArcGeometry.Build(0, 10, 1, 1, 0, 0));
        Assert.Equal(["M", "L", "Z"], edge.Select(c => c.Kind));
        Assert.Equal(10 * Math.Sin(1), edge[0].X, Tolerance);
        Assert.Equal(-10 * Math.Cos(1), edge[0].Y, Tolerance);
        Assert.Equal((0.0, 0.0), (edge[1].X, edge[1].Y));

        // Assert: any input that is not finite, and any magnitude above 1e9, gives the empty path
        Assert.Empty(ArcGeometry.Build(double.NaN, 10, 0, 1, 0, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, double.PositiveInfinity, 0, 1, 0, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 10, double.NaN, 1, 0, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 10, 0, double.NegativeInfinity, 0, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 10, 0, 1, double.NaN, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 10, 0, 1, 0, double.NaN).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 1e10, 0, 1, 0, 0).Subpaths);
        Assert.Empty(ArcGeometry.Build(0, 10, 0, 2e9, 0, 0).Subpaths);
        Assert.NotEmpty(ArcGeometry.Build(0, 1e9, 0, 1, 0, 0).Subpaths);

        // Assert: a pad angle as large as the sector collapses it without failing; a huge corner radius never throws
        Assert.NotEmpty(ArcGeometry.Build(20, 50, 0, 0.05, 0.2, 0).Subpaths);
        Assert.NotEmpty(ArcGeometry.Build(0, 50, 0, 0.02, 0, 1e6).Subpaths);
        Assert.NotEmpty(ArcGeometry.Build(20, 50, 0, 3 * HalfPi, 0.1, 50).Subpaths);
    }
}
