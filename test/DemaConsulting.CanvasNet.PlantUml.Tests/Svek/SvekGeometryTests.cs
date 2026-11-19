using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Unit tests of <see cref="SvekGeometry"/>: the spline of a route, the clipping to outlines and cluster borders,
///     the shortening of routes, the arcs, and the link decorations compared with the shapes of PlantUML's pictures.
/// </summary>
public class SvekGeometryTests
{
    /// <summary>
    ///     Validates that a route of two points is drawn as a straight line.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Spline_TwoPoints_IsALine()
    {
        // Act
        var segments = SvekGeometry.Spline([new(1, 2), new(3, 4)]);

        // Assert
        Assert.Equal([SceneSegment.Move(1, 2), SceneSegment.Line(3, 4)], segments);
    }

    /// <summary>
    ///     Validates that a route of three points is the quadratic curve of its points raised to one cubic.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Spline_ThreePoints_IsTheQuadraticCurve()
    {
        // Act
        var segments = SvekGeometry.Spline([new(0, 0), new(30, 60), new(90, 0)]);

        // Assert
        var cubic = Assert.Single(segments.Skip(1));
        Assert.Equal(SceneSegmentKind.CubicTo, cubic.Kind);
        Assert.Equal((20, 40, 50, 40, 90, 0), (cubic.X1, cubic.Y1, cubic.X2, cubic.Y2, cubic.X, cubic.Y));
    }

    /// <summary>
    ///     Validates that the clamped cubic B-spline of four points is the Bezier curve of the points.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Spline_FourPoints_IsTheBezierOfThePoints()
    {
        // Act
        var segments = SvekGeometry.Spline([new(0, 0), new(10, 20), new(30, 20), new(40, 0)]);

        // Assert
        Assert.Equal([SceneSegment.Move(0, 0), SceneSegment.Cubic(10, 20, 30, 20, 40, 0)], segments);
    }

    /// <summary>
    ///     Validates the spline of a long route: one cubic per interior span, starting and ending at the ends of the
    ///     route, leaving them along the first and last legs, and smooth (C1) where the cubics meet.
    /// </summary>
    /// <param name="count">The number of points.</param>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    public void PlantUmlSvekGeometry_Spline_LongRoute_IsSmoothAndClamped(int count)
    {
        // Arrange: a zig-zag route
        var points = Enumerable.Range(0, count).Select(k => new SvekPoint(k * 20, k % 2 == 0 ? 0 : 30)).ToList();

        // Act
        var segments = SvekGeometry.Spline(points);

        // Assert: the ends, the end tangents, and the joins
        var cubics = segments.Skip(1).ToList();
        Assert.Equal(count - 3, cubics.Count);
        Assert.Equal((0, 0), (segments[0].X, segments[0].Y));
        Assert.Equal((points[^1].X, points[^1].Y), (cubics[^1].X, cubics[^1].Y));
        Assert.Equal(0, Cross(points[1].X - points[0].X, points[1].Y - points[0].Y, cubics[0].X1, cubics[0].Y1), 9);
        Assert.Equal(0, Cross(points[^2].X - points[^1].X, points[^2].Y - points[^1].Y, cubics[^1].X2 - points[^1].X, cubics[^1].Y2 - points[^1].Y), 9);
        for (var k = 1; k < cubics.Count; k++)
        {
            var (jx, jy) = (cubics[k - 1].X, cubics[k - 1].Y);
            Assert.Equal(jx - cubics[k - 1].X2, cubics[k].X1 - jx, 9);
            Assert.Equal(jy - cubics[k - 1].Y2, cubics[k].Y1 - jy, 9);
        }
    }

    /// <summary>
    ///     Validates that a route needs two points.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Spline_OnePoint_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => SvekGeometry.Spline([new(0, 0)]));
    }

    /// <summary>
    ///     Validates the clipping of a direction from the center of an outline to the rectangle, the ellipse, the circle,
    ///     and the diamond.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Clip_EveryShape_EndsOnTheOutline()
    {
        // Arrange
        var box = new SceneRect(0, 0, 100, 50);
        var circle = new SceneRect(0, 0, 20, 20);

        // Act / Assert: a rectangle along both axes and a corner direction
        Assert.Equal(new SvekPoint(100, 25), SvekGeometry.Clip(ClipShape.Rectangle, box, new(200, 25)));
        Assert.Equal(new SvekPoint(50, 0), SvekGeometry.Clip(ClipShape.Rectangle, box, new(50, -100)));
        Assert.Equal(new SvekPoint(75, 50), SvekGeometry.Clip(ClipShape.Rectangle, box, new(100, 75)));

        // An ellipse and a circle: the point satisfies the equation of the outline
        var onEllipse = SvekGeometry.Clip(ClipShape.Ellipse, box, new(150, 75));
        Assert.Equal(1, (Math.Pow(onEllipse.X - 50, 2) / 2500) + (Math.Pow(onEllipse.Y - 25, 2) / 625), 9);
        var onCircle = SvekGeometry.Clip(ClipShape.Ellipse, circle, new(40, 40));
        Assert.Equal(10, Math.Sqrt(Math.Pow(onCircle.X - 10, 2) + Math.Pow(onCircle.Y - 10, 2)), 9);

        // A diamond: the corners and a point on a side
        Assert.Equal(new SvekPoint(100, 25), SvekGeometry.Clip(ClipShape.Diamond, box, new(200, 25)));
        Assert.Equal(new SvekPoint(75, 37.5), SvekGeometry.Clip(ClipShape.Diamond, box, new(100, 50)));

        // The center itself
        Assert.Equal(new SvekPoint(50, 25), SvekGeometry.Clip(ClipShape.Ellipse, box, new(50, 25)));
    }

    /// <summary>
    ///     Validates the containment test of every outline.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Contains_EveryShape()
    {
        // Arrange
        var box = new SceneRect(0, 0, 100, 50);

        // Act / Assert: the corner region is inside the rectangle only
        Assert.True(SvekGeometry.Contains(ClipShape.Rectangle, box, new(95, 45)));
        Assert.False(SvekGeometry.Contains(ClipShape.Ellipse, box, new(95, 45)));
        Assert.False(SvekGeometry.Contains(ClipShape.Diamond, box, new(95, 45)));
        Assert.True(SvekGeometry.Contains(ClipShape.Ellipse, box, new(50, 25)));
        Assert.True(SvekGeometry.Contains(ClipShape.Diamond, box, new(75, 37.5)));
        Assert.False(SvekGeometry.Contains(ClipShape.Rectangle, box, new(101, 25)));
    }

    /// <summary>
    ///     Validates that a route is cut where it enters a box, at either end, and kept when it never enters it.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_CutAtBox_BothEnds()
    {
        // Arrange
        var box = new SceneRect(-10, 0, 20, 20);
        IReadOnlyList<SvekPoint> down = [new(0, -50), new(0, -10), new(0, 10)];
        IReadOnlyList<SvekPoint> up = [new(0, 10), new(0, -10), new(0, -50)];

        // Act
        var atEnd = SvekGeometry.CutAtBox(down, box, atEnd: true);
        var atStart = SvekGeometry.CutAtBox(up, box, atEnd: false);
        var outside = SvekGeometry.CutAtBox([new(50, 0), new(60, 0)], box, atEnd: true);

        // Assert
        Assert.Equal([new SvekPoint(0, -50), new SvekPoint(0, -10), new SvekPoint(0, 0)], atEnd);
        Assert.Equal([new SvekPoint(0, 0), new SvekPoint(0, -10), new SvekPoint(0, -50)], atStart);
        Assert.Equal(2, outside.Count);
    }

    /// <summary>
    ///     Validates that the ends of a route move back along its end legs, never past the next point.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Shorten_MovesEndsAlongTheirLegs()
    {
        // Act
        var shortened = SvekGeometry.Shorten([new(0, 0), new(0, 10), new(0, 100)], 5, 18);
        var clamped = SvekGeometry.Shorten([new(0, 0), new(0, 4)], 10, 0);

        // Assert
        Assert.Equal([new SvekPoint(0, 5), new SvekPoint(0, 10), new SvekPoint(0, 82)], shortened);
        Assert.Equal(new SvekPoint(0, 4), clamped[0]);
    }

    /// <summary>
    ///     Validates the direction in which a route arrives at each end, skipping legs without length.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_ArrivalAngle_BothEnds()
    {
        // Arrange
        IReadOnlyList<SvekPoint> route = [new(0, 0), new(0, 0), new(10, 0), new(10, 10)];

        // Act / Assert
        Assert.Equal(Math.PI / 2, SvekGeometry.ArrivalAngle(route, atEnd: true), 9);
        Assert.Equal(Math.PI, SvekGeometry.ArrivalAngle(route, atEnd: false), 9);
    }

    /// <summary>
    ///     Validates that an arc starts and ends on its circle and is split in pieces of at most a quarter circle.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Arc_StaysOnTheCircle()
    {
        // Act
        var arc = SvekGeometry.Arc(10, 10, 9, 0, 140 * Math.PI / 180);

        // Assert
        Assert.Equal(3, arc.Count);
        Assert.Equal((19, 10), (arc[0].X, arc[0].Y));
        Assert.Equal(9, Math.Sqrt(Math.Pow(arc[^1].X - 10, 2) + Math.Pow(arc[^1].Y - 10, 2)), 9);
        Assert.Equal(Math.Cos(140 * Math.PI / 180), (arc[^1].X - 10) / 9, 9);
    }

    /// <summary>
    ///     Validates the arrow head against PlantUML's picture: a filled head 9 long and 8 wide whose line stops 5
    ///     before the tip (the golden head points down at 128.5, 628.896).
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Extremity_Arrow_MatchesPlantUml()
    {
        // Act
        var arrow = SvekGeometry.Extremity(LinkDecor.Arrow, new SvekPoint(128.5, 628.896), Math.PI / 2)!;

        // Assert
        var part = Assert.Single(arrow.Parts);
        Assert.Equal(ExtremityFill.Line, part.Fill);
        Assert.Equal(5, arrow.ContactLength);
        AssertPoints([(128.5, 628.896), (132.5, 619.896), (128.5, 623.896), (124.5, 619.896)], part.Segments);
    }

    /// <summary>
    ///     Validates the extension triangle against PlantUML's picture: hollow, 18 long and 12 wide (the golden one
    ///     points up at 128.5, 452.794).
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Extremity_Extends_MatchesPlantUml()
    {
        // Act
        var extends = SvekGeometry.Extremity(LinkDecor.Extends, new SvekPoint(128.5, 452.794), -Math.PI / 2)!;

        // Assert
        var part = Assert.Single(extends.Parts);
        Assert.Equal((ExtremityFill.None, 18.0), (part.Fill, extends.ContactLength));
        AssertPoints([(128.5, 452.794), (122.5, 470.794), (134.5, 470.794)], part.Segments);
    }

    /// <summary>
    ///     Validates the diamonds against PlantUML's pictures: the filled composition diamond and the hollow aggregation
    ///     diamond, 12 long and 8 wide, along an oblique link.
    /// </summary>
    /// <param name="decor">The decoration.</param>
    /// <param name="fill">The expected fill.</param>
    /// <param name="golden">The golden polygon: the tip, a side, the far corner, the other side.</param>
    [Theory]
    [InlineData("Composition", "Line", new[] { 106.021, 555.704, 99.303, 558.324, 99.137, 565.533, 105.856, 562.913 })]
    [InlineData("Aggregation", "None", new[] { 203.197, 142.993, 204.804, 150.023, 211.911, 151.243, 210.304, 144.214 })]
    public void PlantUmlSvekGeometry_Extremity_Diamonds_MatchPlantUml(string decor, string fill, double[] golden)
    {
        // Arrange: the link arrives at the tip from the far corner
        var tip = new SvekPoint(golden[0], golden[1]);
        var angle = Math.Atan2(golden[1] - golden[5], golden[0] - golden[4]);

        // Act
        var diamond = SvekGeometry.Extremity(Enum.Parse<LinkDecor>(decor), tip, angle)!;

        // Assert: the same corners as the golden polygon, in either order of the sides
        var part = Assert.Single(diamond.Parts);
        Assert.Equal((Enum.Parse<ExtremityFill>(fill), 12.0), (part.Fill, diamond.ContactLength));
        var points = Corners(part.Segments);
        Assert.Equal(4, points.Count);
        for (var k = 0; k < 4; k++)
        {
            var (gx, gy) = (golden[2 * k], golden[(2 * k) + 1]);
            Assert.Contains(points, p => Math.Abs(p.X - gx) < 0.02 && Math.Abs(p.Y - gy) < 0.02);
        }
    }

    /// <summary>
    ///     Validates the decorations made of circles, squares, arcs, and lines: their sizes, fills, strokes, and contact
    ///     lengths.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_Extremity_OtherShapes_HavePlantUmlSizes()
    {
        // Arrange: every decoration at the origin, arriving downward
        var tip = new SvekPoint(0, 0);
        SvekExtremity Make(LinkDecor decor) => SvekGeometry.Extremity(decor, tip, Math.PI / 2)!;

        // Act
        var circle = Make(LinkDecor.Circle);
        var filled = Make(LinkDecor.CircleFill);
        var square = Make(LinkDecor.Square);
        var plus = Make(LinkDecor.Plus);
        var socket = Make(LinkDecor.Parenthesis);
        var connect = Make(LinkDecor.CircleConnect);
        var cross = Make(LinkDecor.NotNavigable);
        var small = Make(LinkDecor.ArrowTriangle);
        var half = Make(LinkDecor.HalfArrowUp);

        // Assert: a circle of 6 centered 6 before the tip, stroke 1.5
        Assert.Equal((ExtremityFill.Background, 1.5, 12.0), (circle.Parts[0].Fill, circle.Parts[0].StrokeWidth!.Value, circle.ContactLength));
        AssertRect(new SceneRect(-6, -12, 12, 12), Scene.BoundsOf([new ScenePath(circle.Parts[0].Segments) { Role = "x" }])!.Value);
        Assert.Equal(ExtremityFill.Line, filled.Parts[0].Fill);

        // A square of 10 centered on the tip, a circled plus of 8, a socket arc of 9 behind the tip
        AssertRect(new SceneRect(-5, -5, 10, 10), Scene.BoundsOf([new ScenePath(square.Parts[0].Segments) { Role = "x" }])!.Value);
        Assert.Equal((3, 16.0), (plus.Parts.Count, plus.ContactLength));
        AssertRect(new SceneRect(-8, -16, 16, 16), Scene.BoundsOf([new ScenePath(plus.Parts[0].Segments) { Role = "x" }])!.Value);
        var arcEnd = socket.Parts[0].Segments[^1];
        Assert.Equal(9, Math.Sqrt((arcEnd.X * arcEnd.X) + (arcEnd.Y * arcEnd.Y)), 9);
        Assert.True(arcEnd.Y < 0);
        Assert.Equal((2, 10.0), (connect.Parts.Count, connect.ContactLength));

        // The cross 5 to 13 before the tip, the small triangle, and the half arrow
        AssertRect(new SceneRect(-4, -13, 8, 8), Scene.BoundsOf(cross.Parts.Select(p => new ScenePath(p.Segments) { Role = "x" }))!.Value);
        Assert.Equal((ExtremityFill.None, 8.0), (small.Parts[0].Fill, small.ContactLength));
        Assert.Equal(2, half.Parts.Count);
    }

    /// <summary>
    ///     Validates that the crow's foot family and the redefinition triangles are not drawn, and the margins of the
    ///     decorations.
    /// </summary>
    [Fact]
    public void PlantUmlSvekGeometry_IsDrawnAndMargin_FollowPlantUml()
    {
        // Act / Assert
        Assert.False(SvekGeometry.IsDrawn(LinkDecor.Crowfoot));
        Assert.False(SvekGeometry.IsDrawn(LinkDecor.Redefines));
        Assert.True(SvekGeometry.IsDrawn(LinkDecor.Arrow));
        Assert.Null(SvekGeometry.Extremity(LinkDecor.LineCrowfoot, new SvekPoint(0, 0), 0));
        Assert.Null(SvekGeometry.Extremity(LinkDecor.None, new SvekPoint(0, 0), 0));
        Assert.Equal((30.0, 15.0, 10.0, 2.0), (SvekGeometry.Margin(LinkDecor.Extends), SvekGeometry.Margin(LinkDecor.Composition), SvekGeometry.Margin(LinkDecor.Arrow), SvekGeometry.Margin(LinkDecor.None)));
    }

    /// <summary>
    ///     Asserts that a rectangle equals another within 1e-9.
    /// </summary>
    /// <param name="expected">The expected rectangle.</param>
    /// <param name="actual">The rectangle.</param>
    private static void AssertRect(SceneRect expected, SceneRect actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Width, actual.Width, 9);
        Assert.Equal(expected.Height, actual.Height, 9);
    }

    /// <summary>
    ///     Asserts that a polygon has the given corners in order.
    /// </summary>
    /// <param name="expected">The corners.</param>
    /// <param name="segments">The polygon.</param>
    private static void AssertPoints((double X, double Y)[] expected, IReadOnlyList<SceneSegment> segments)
    {
        var points = Corners(segments);
        Assert.Equal(expected.Length, points.Count);
        for (var k = 0; k < expected.Length; k++)
        {
            Assert.Equal(expected[k].X, points[k].X, 3);
            Assert.Equal(expected[k].Y, points[k].Y, 3);
        }
    }

    /// <summary>
    ///     Gets the corners of a polygon (the end points of its moves and lines).
    /// </summary>
    /// <param name="segments">The polygon.</param>
    /// <returns>The corners.</returns>
    private static List<SvekPoint> Corners(IReadOnlyList<SceneSegment> segments) =>
        segments.Where(s => s.Kind != SceneSegmentKind.Close).Select(s => new SvekPoint(s.X, s.Y)).ToList();

    /// <summary>
    ///     Gets the cross product of two vectors.
    /// </summary>
    /// <param name="ax">The first x.</param>
    /// <param name="ay">The first y.</param>
    /// <param name="bx">The second x.</param>
    /// <param name="by">The second y.</param>
    /// <returns>The cross product.</returns>
    private static double Cross(double ax, double ay, double bx, double by) => (ax * by) - (ay * bx);
}
