using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for the two-dimensional shapes of <see cref="PrimitiveTessellator"/>.
/// </summary>
public class PrimitiveTessellator2DTests
{
    /// <summary>
    ///     Validates the area and extent of a rectangle, with and without centering.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Rectangle_SizeAndCenter_HaveAnalyticAreaAndExtent()
    {
        // Arrange / Act
        var rectangle = PrimitiveTessellator.Rectangle(new Vector2D(4, 2));
        var centered = PrimitiveTessellator.Rectangle(new Vector2D(4, 2), center: true);

        // Assert: counter-clockwise from the origin, then shifted by half the size
        Assert.Equal([new Vector2D(0, 0), new Vector2D(4, 0), new Vector2D(4, 2), new Vector2D(0, 2)], rectangle.Contours[0]);
        Assert.Equal(8.0, rectangle.Area, 1e-12);
        Assert.Equal([new Vector2D(-2, -1), new Vector2D(2, -1), new Vector2D(2, 1), new Vector2D(-2, 1)], centered.Contours[0]);
        Assert.Equal(8.0, centered.Area, 1e-12);
    }

    /// <summary>
    ///     Validates that ellipse vertices lie on the ellipse in counter-clockwise order from the +X axis.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Ellipse_VerticesLieOnEllipseAndAreCounterClockwise()
    {
        // Arrange / Act
        var ellipse = PrimitiveTessellator.Ellipse(3.0, 2.0, 12);

        // Assert
        var points = ellipse.Contours[0];
        Assert.Equal(12, points.Count);
        Assert.Equal(new Vector2D(3.0, 0.0), points[0]);
        foreach (var point in points)
        {
            Assert.Equal(1.0, ((point.X / 3.0) * (point.X / 3.0)) + ((point.Y / 2.0) * (point.Y / 2.0)), 1e-12);
        }

        // Counter-clockwise: the second vertex has a positive Y and the signed area is positive
        Assert.True(points[1].Y > 0.0);
        Assert.True(ellipse.Area > 0.0);
    }

    /// <summary>
    ///     Validates the inscribed-polygon area of an ellipse.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Ellipse_AreaIsPolygonCorrectedAnalytic()
    {
        // Arrange
        const int n = 40;

        // Act
        var ellipse = PrimitiveTessellator.Ellipse(3.0, 2.0, n);

        // Assert: n / 2 * rx * ry * sin(2 pi / n)
        var expected = 0.5 * n * 3.0 * 2.0 * Math.Sin(2.0 * Math.PI / n);
        Assert.Equal(expected, ellipse.Area, expected * 1e-12);
    }

    /// <summary>
    ///     Validates that a circle equals an ellipse with equal radii.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Circle_EqualsEllipseWithEqualRadii()
    {
        // Arrange / Act
        var circle = PrimitiveTessellator.Circle(2.5, 16);
        var ellipse = PrimitiveTessellator.Ellipse(2.5, 2.5, 16);

        // Assert
        Assert.Equal(ellipse.Contours[0], circle.Contours[0]);
    }

    /// <summary>
    ///     Validates that a point list without paths is a single contour of all points in order.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polygon_PointsOnly_IsSingleContour()
    {
        // Arrange
        Vector2D[] points = [new(0, 0), new(4, 0), new(4, 3)];

        // Act
        var region = PrimitiveTessellator.Polygon(points);

        // Assert
        Assert.Single(region.Contours);
        Assert.Equal(points, region.Contours[0]);
        Assert.Equal(6.0, region.Area, 1e-12);
    }

    /// <summary>
    ///     Validates that paths select the contours and that nesting makes a hole.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polygon_WithPaths_BuildsHole()
    {
        // Arrange: outer 10 x 10 then a 2 x 2 hole, referenced by index
        Vector2D[] points = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(4, 4), new(6, 4), new(6, 6), new(4, 6)];
        int[][] paths = [[0, 1, 2, 3], [4, 5, 6, 7]];

        // Act
        var region = PrimitiveTessellator.Polygon(points, paths);

        // Assert
        Assert.Equal(2, region.Contours.Count);
        Assert.Equal(96.0, region.Area, 1e-12);
        Assert.Equal(1, region.ContourTable[1].Depth);
    }

    /// <summary>
    ///     Validates that invalid arguments of the 2D shapes are rejected.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Shapes2D_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert: rectangle sizes
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Rectangle(new Vector2D(0, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Rectangle(new Vector2D(1, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Rectangle(new Vector2D(double.NaN, 1)));

        // Ellipse and circle radii and segment counts
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Ellipse(0.0, 1.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Ellipse(1.0, double.PositiveInfinity, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Ellipse(1.0, 1.0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Circle(-1.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Circle(1.0, 2));

        // Polygon: null arguments, out-of-range path indices and invalid geometry
        Vector2D[] points = [new(0, 0), new(4, 0), new(4, 3)];
        Assert.Throws<ArgumentNullException>(() => PrimitiveTessellator.Polygon(null!));
        Assert.Throws<ArgumentNullException>(() => PrimitiveTessellator.Polygon(points, [null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Polygon(points, [[0, 1, 3]]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Polygon(points, [[0, -1, 2]]));
        Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polygon([new(0, 0), new(1, 1), new(2, 2)]));
    }
}
