using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="PolygonRegion"/>.
/// </summary>
public class PolygonRegionTests
{
    /// <summary>Creates an axis-aligned square contour in counter-clockwise order.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="size">The edge length.</param>
    /// <returns>The four corners.</returns>
    private static Vector2D[] Square(double x, double y, double size) =>
        [new(x, y), new(x + size, y), new(x + size, y + size), new(x, y + size)];

    /// <summary>Returns the contour with its winding reversed, keeping the first vertex first.</summary>
    /// <param name="points">The contour.</param>
    /// <returns>The reversed contour.</returns>
    private static Vector2D[] Reversed(Vector2D[] points) => [points[0], .. points.Skip(1).Reverse()];

    /// <summary>
    ///     Validates that a clockwise outer contour is normalized to counter-clockwise winding.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_ClockwiseOuter_IsNormalizedToCounterClockwise()
    {
        // Arrange: a square listed clockwise
        Vector2D[] clockwise = [new(0, 0), new(0, 2), new(2, 2), new(2, 0)];

        // Act
        var region = new PolygonRegion([clockwise]);

        // Assert: counter-clockwise, same first vertex, area 4
        Assert.Equal([new Vector2D(0, 0), new Vector2D(2, 0), new Vector2D(2, 2), new Vector2D(0, 2)], region.Contours[0]);
        Assert.Equal(4.0, region.Area, 1e-12);
        Assert.False(region.IsEmpty);
        Assert.Equal(4, region.VertexCount);
    }

    /// <summary>
    ///     Validates that a counter-clockwise hole is normalized to clockwise winding and subtracts its area.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_CounterClockwiseHole_IsNormalizedToClockwise()
    {
        // Arrange: a 10 x 10 square with a counter-clockwise 2 x 2 hole
        var region = new PolygonRegion([Square(0, 0, 10), Square(2, 2, 2)]);

        // Act / Assert: the hole is reversed (first vertex kept) and the area is 100 - 4
        Assert.Equal([new Vector2D(2, 2), new Vector2D(2, 4), new Vector2D(4, 4), new Vector2D(4, 2)], region.Contours[1]);
        Assert.Equal(96.0, region.Area, 1e-12);
        Assert.Equal(8, region.Vertices.Count);
    }

    /// <summary>
    ///     Validates that an island inside a hole has even depth and counter-clockwise winding regardless
    ///     of its input winding, and that the contour table records depth and parent.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_IslandInHole_HasEvenDepthAndCounterClockwise()
    {
        // Arrange: outer 20, hole 12 and island 4, with the island listed clockwise
        var region = new PolygonRegion([Reversed(Square(8, 8, 4)), Square(4, 4, 12), Square(0, 0, 20)]);

        // Act
        var table = region.ContourTable.ToArray();

        // Assert: depths follow the nesting, not the input order
        Assert.Equal([2, 1, 0], table.Select(info => info.Depth));
        Assert.Equal([1, 2, -1], table.Select(info => info.Parent));
        Assert.Equal([new Vector2D(8, 8), new Vector2D(12, 8), new Vector2D(12, 12), new Vector2D(8, 12)], region.Contours[0]);
        Assert.Equal(400.0 - 144.0 + 16.0, region.Area, 1e-9);
    }

    /// <summary>
    ///     Validates that a closing duplicate and consecutive duplicate points are removed.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_ClosingAndRepeatedPoints_AreRemoved()
    {
        // Arrange: explicit closing point and a repeated vertex
        Vector2D[] input = [new(0, 0), new(2, 0), new(2, 0), new(2, 2), new(0, 2), new(0, 0)];

        // Act
        var region = new PolygonRegion([input]);

        // Assert
        Assert.Equal(4, region.Contours[0].Count);
        Assert.Equal(4.0, region.Area, 1e-12);
    }

    /// <summary>
    ///     Validates that collinear points are kept as vertices.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_CollinearPoints_AreKept()
    {
        // Arrange: a square with an extra point in the middle of the bottom edge
        Vector2D[] input = [new(0, 0), new(1, 0), new(2, 0), new(2, 2), new(0, 2)];

        // Act
        var region = new PolygonRegion([input]);

        // Assert
        Assert.Equal(input, region.Contours[0]);
        Assert.Equal(5, region.VertexCount);
    }

    /// <summary>
    ///     Validates that a region without contours is empty and has zero area.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_NoContours_IsEmptyWithZeroArea()
    {
        // Arrange / Act
        var region = new PolygonRegion([]);

        // Assert
        Assert.True(region.IsEmpty);
        Assert.Equal(0.0, region.Area);
        Assert.Empty(region.Contours);
        Assert.Empty(region.Vertices);
        Assert.True(PolygonRegion.Empty.IsEmpty);
        Assert.Same(PolygonRegion.Empty, PolygonRegion.Empty);
    }

    /// <summary>
    ///     Validates the area of a ring with a hole and an island in the hole.
    /// </summary>
    [Fact]
    public void PolygonRegion_Area_RingWithHoleAndIsland_IsAnalytic()
    {
        // Arrange: 30 x 30 ring with a 20 x 20 hole and a 6 x 6 island, plus a separate 3 x 3 square
        var region = new PolygonRegion([Square(0, 0, 30), Square(5, 5, 20), Square(12, 12, 6), Square(40, 0, 3)]);

        // Act / Assert
        Assert.Equal(900.0 - 400.0 + 36.0 + 9.0, region.Area, 1e-9);
    }

    /// <summary>
    ///     Validates that a contour with fewer than three distinct points is rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_FewerThanThreeDistinctPoints_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1, 0) }]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1, 0), new(0, 0) }]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(1, 1), new(1, 1), new(1, 1) }]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([Array.Empty<Vector2D>()]));
    }

    /// <summary>
    ///     Validates that an all-collinear (zero area) contour is rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_ZeroAreaContour_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1, 1), new(2, 2) }]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(3, 0), new(1, 0), new(2, 0) }]));
    }

    /// <summary>
    ///     Validates that non-finite coordinates are rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_NonFinitePoint_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1, 0), new(double.NaN, 1) }]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(double.PositiveInfinity, 0), new(0, 1) }]));
    }

    /// <summary>
    ///     Validates that finite but extreme coordinates whose area overflows are rejected, while a large
    ///     region whose area still fits is accepted with a finite area.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_AreaNotFinite_Throws()
    {
        // Arrange / Act / Assert: 1e308 squared overflows (the shoelace terms become infinity / NaN)
        var huge = Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1e308, 0), new(1e308, 1e308), new(0, 1e308) }]));
        Assert.Contains("area", huge.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new PolygonRegion([new Vector2D[] { new(0, 0), new(1e200, 0), new(1e200, 1e200), new(0, 1e200) }]));

        // A 1e100 square has area 1e200, which is finite
        var large = new PolygonRegion([new Vector2D[] { new(0, 0), new(1e100, 0), new(1e100, 1e100), new(0, 1e100) }]);
        Assert.True(double.IsFinite(large.Area));
        Assert.Equal(1e200, large.Area, 1e185);
    }

    /// <summary>
    ///     Validates that null arguments are rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_NullArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new PolygonRegion(null!));
        Assert.Throws<ArgumentNullException>(() => new PolygonRegion([null!]));
    }

    /// <summary>
    ///     Validates that a self-crossing bow tie contour is rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_SelfIntersectingBowTie_Throws()
    {
        // Arrange: edges (0,0)-(2,2) and (2,0)-(0,2) cross
        Vector2D[] bowTie = [new(0, 0), new(2, 2), new(2, 0), new(0, 2)];

        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => new PolygonRegion([bowTie]));
        Assert.Contains("Contour 0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that edges folding back onto each other (a spike) are rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_SpikeOverlappingEdges_Throws()
    {
        // Arrange: the edge (0,4)-(0,8) is followed by (0,8)-(0,6), which overlaps it
        Vector2D[] spike = [new(0, 0), new(4, 0), new(4, 4), new(0, 4), new(0, 8), new(0, 6)];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([spike]));
    }

    /// <summary>
    ///     Validates that a contour visiting the same point twice (a figure eight) is rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_RepeatedNonConsecutivePoint_Throws()
    {
        // Arrange: (1,1) is visited twice
        Vector2D[] figureEight = [new(0, 0), new(2, 0), new(1, 1), new(2, 2), new(0, 2), new(1, 1)];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([figureEight]));
    }

    /// <summary>
    ///     Validates that two partially overlapping contours are rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_CrossingContours_Throws()
    {
        // Arrange / Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => new PolygonRegion([Square(0, 0, 4), Square(2, 2, 4)]));
        Assert.Contains("contour", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that contours touching at a shared vertex or with a vertex on another contour's edge
    ///     are rejected.
    /// </summary>
    [Fact]
    public void PolygonRegion_Constructor_TouchingContours_Throws()
    {
        // Arrange: two squares sharing the corner (2,2)
        var sharedVertex = new[] { Square(0, 0, 2), Square(2, 2, 2) };

        // A hole whose vertex (5,0) lies on the outer bottom edge
        var vertexOnEdge = new[] { Square(0, 0, 10), new Vector2D[] { new(5, 0), new(7, 3), new(3, 3) } };

        // A hole sharing a whole edge piece with the outer contour
        var sharedEdge = new[] { Square(0, 0, 10), new Vector2D[] { new(2, 0), new(4, 0), new(4, 2), new(2, 2) } };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion(sharedVertex));
        Assert.Throws<ArgumentException>(() => new PolygonRegion(vertexOnEdge));
        Assert.Throws<ArgumentException>(() => new PolygonRegion(sharedEdge));
    }

    /// <summary>
    ///     Validates that the region keeps private copies of its input.
    /// </summary>
    [Fact]
    public void PolygonRegion_Contours_AreSnapshotsOfInput()
    {
        // Arrange
        var input = new List<Vector2D>(Square(0, 0, 2));
        var region = new PolygonRegion([input]);

        // Act: mutate the caller's list after construction
        input[2] = new Vector2D(100, 100);
        input.Clear();

        // Assert: the region is unchanged
        Assert.Equal(Square(0, 0, 2), region.Contours[0]);
        Assert.Equal(4.0, region.Area, 1e-12);
    }
}
