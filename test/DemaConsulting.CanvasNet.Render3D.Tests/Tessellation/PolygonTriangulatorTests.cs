// cspell:ignore triangulator triangulation Ngon

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="PolygonTriangulator"/>.
/// </summary>
public class PolygonTriangulatorTests
{
    /// <summary>Creates a counter-clockwise axis-aligned rectangle.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The four corners.</returns>
    private static Vector2D[] Rect(double x, double y, double width, double height) =>
        [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)];

    /// <summary>Returns the contour with its winding reversed, keeping the first vertex first.</summary>
    /// <param name="points">The contour.</param>
    /// <returns>The reversed contour.</returns>
    private static Vector2D[] Reversed(Vector2D[] points) => [points[0], .. points.Skip(1).Reverse()];

    /// <summary>Creates a star-shaped polygon about a center with strictly increasing angles.</summary>
    /// <param name="random">The random source.</param>
    /// <param name="center">The center, which lies inside the polygon.</param>
    /// <param name="count">The number of vertices (at least 5).</param>
    /// <param name="minimumRadius">The smallest vertex radius.</param>
    /// <param name="maximumRadius">The largest vertex radius.</param>
    /// <returns>The counter-clockwise contour.</returns>
    private static Vector2D[] Star(Random random, Vector2D center, int count, double minimumRadius, double maximumRadius)
    {
        var points = new Vector2D[count];
        for (var i = 0; i < count; i++)
        {
            // Stratified angles guarantee increasing angles with gaps below 180 degrees
            var angle = (i + 0.05 + (0.9 * random.NextDouble())) * 2.0 * Math.PI / count;
            var radius = minimumRadius + ((maximumRadius - minimumRadius) * random.NextDouble());
            points[i] = new Vector2D(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));
        }

        return points;
    }

    /// <summary>
    ///     Asserts every invariant of an exact triangulation: triangle count, counter-clockwise triangles,
    ///     total area, use of every vertex, and edge usage (contour edges once in contour direction, all other
    ///     edges once in each direction).
    /// </summary>
    /// <param name="region">The triangulated region.</param>
    /// <param name="indices">The triangle indices returned by the triangulator.</param>
    private static void AssertExactTriangulation(PolygonRegion region, IReadOnlyList<int> indices)
    {
        var points = region.Vertices;
        var table = region.ContourTable.ToArray();
        var holes = table.Count(info => !info.IsSolid);
        var solids = table.Count(info => info.IsSolid);

        // Exact triangle count V + 2H - 2P
        Assert.Equal(0, indices.Count % 3);
        Assert.Equal(points.Count + (2 * holes) - (2 * solids), indices.Count / 3);

        // Strictly counter-clockwise triangles whose areas add up to the region area
        var used = new bool[points.Count];
        var directed = new Dictionary<(int, int), int>();
        var area = 0.0;
        for (var t = 0; t < indices.Count; t += 3)
        {
            int[] corner = [indices[t], indices[t + 1], indices[t + 2]];
            Assert.True(PolygonPredicates.Orient2D(points[corner[0]], points[corner[1]], points[corner[2]]) > 0, $"Triangle {t / 3} is not strictly counter-clockwise.");
            area += 0.5 * Vector2D.Cross(points[corner[1]] - points[corner[0]], points[corner[2]] - points[corner[0]]);
            for (var k = 0; k < 3; k++)
            {
                used[corner[k]] = true;
                var edge = (corner[k], corner[(k + 1) % 3]);
                directed[edge] = directed.GetValueOrDefault(edge) + 1;
            }
        }

        Assert.Equal(region.Area, area, region.Area * 1e-12);
        Assert.DoesNotContain(false, used);

        // Every directed edge occurs once; contour edges only in their contour direction
        var contourEdges = new HashSet<(int, int)>();
        foreach (var info in table)
        {
            for (var i = 0; i < info.Count; i++)
            {
                contourEdges.Add((info.Start + i, info.Start + ((i + 1) % info.Count)));
            }
        }

        foreach (var (edge, count) in directed)
        {
            Assert.Equal(1, count);
            var reverse = (edge.Item2, edge.Item1);
            if (contourEdges.Contains(edge))
            {
                Assert.False(directed.ContainsKey(reverse), $"Contour edge {edge} is used in both directions.");
            }
            else
            {
                Assert.True(directed.ContainsKey(reverse), $"Interior edge {edge} has no opposite edge.");
            }
        }

        foreach (var edge in contourEdges)
        {
            Assert.True(directed.ContainsKey(edge), $"Contour edge {edge} is not covered.");
        }
    }

    /// <summary>
    ///     Triangulates a region and asserts all invariants.
    /// </summary>
    /// <param name="contours">The contours of the region.</param>
    /// <returns>The region and its triangle indices.</returns>
    private static (PolygonRegion Region, IReadOnlyList<int> Indices) TriangulateAndCheck(params Vector2D[][] contours)
    {
        var region = new PolygonRegion(contours);
        var indices = PolygonTriangulator.Triangulate(region);
        AssertExactTriangulation(region, indices);
        return (region, indices);
    }

    /// <summary>Returns a named concave test polygon in counter-clockwise order.</summary>
    /// <param name="name">The polygon name.</param>
    /// <returns>The polygon.</returns>
    private static Vector2D[] NamedPolygon(string name) => name switch
    {
        "L" => [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)],
        "Arrow" => [new(0, 0), new(3, 0), new(3, -1), new(5, 1), new(3, 3), new(3, 2), new(0, 2)],
        "Comb" => [new(0, 0), new(8, 0), new(8, 4), new(7, 4), new(7, 1), new(5, 1), new(5, 4), new(3, 4), new(3, 1), new(1, 1), new(1, 4), new(0, 4)],
        "C" => [new(0, 0), new(6, 0), new(6, 1), new(1, 1), new(1, 4), new(6, 4), new(6, 5), new(0, 5)],
        "Sawtooth" => [new(0, 0), new(9, 0), new(9, 3), new(7, 1), new(7, 3), new(5, 1), new(5, 3), new(3, 1), new(3, 3), new(1, 1), new(1, 3), new(0, 3)],
        "Star" => Enumerable.Range(0, 10)
            .Select(i => new Vector2D(
                (i % 2 == 0 ? 10.0 : 4.0) * Math.Cos(i * Math.PI / 5.0),
                (i % 2 == 0 ? 10.0 : 4.0) * Math.Sin(i * Math.PI / 5.0)))
            .ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>
    ///     Validates that a square yields two counter-clockwise triangles.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_Square_ReturnsTwoCounterClockwiseTriangles()
    {
        // Arrange / Act
        var (region, indices) = TriangulateAndCheck(Rect(0, 0, 2, 2));

        // Assert
        Assert.Equal(6, indices.Count);
        Assert.Equal(4.0, region.Area, 1e-12);
    }

    /// <summary>
    ///     Validates that concave polygons are covered exactly.
    /// </summary>
    /// <param name="name">The polygon name.</param>
    [Theory]
    [InlineData("L")]
    [InlineData("Arrow")]
    [InlineData("Comb")]
    [InlineData("C")]
    [InlineData("Sawtooth")]
    [InlineData("Star")]
    public void PolygonTriangulator_Triangulate_ConcavePolygons_CoverRegionExactly(string name)
    {
        // Arrange / Act / Assert: all invariants hold in both windings
        var polygon = NamedPolygon(name);
        TriangulateAndCheck(polygon);
        TriangulateAndCheck(Enumerable.Reverse(polygon).ToArray());
    }

    /// <summary>
    ///     Validates that input winding does not change the result when the first vertex is kept, and that a
    ///     fully reversed input is also triangulated exactly.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_ClockwiseAndCounterClockwiseInput_GiveIdenticalTriangulation()
    {
        // Arrange
        var polygon = NamedPolygon("Comb");

        // Act
        var counterClockwise = PolygonTriangulator.Triangulate(new PolygonRegion([polygon]));
        var clockwise = PolygonTriangulator.Triangulate(new PolygonRegion([Reversed(polygon)]));

        // Assert
        Assert.Equal(counterClockwise, clockwise);
    }

    /// <summary>
    ///     Validates that a regular n-gon has n - 2 triangles.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RegularNgon_HasNMinusTwoTriangles()
    {
        // Arrange
        const int n = 64;
        var points = Enumerable.Range(0, n)
            .Select(i => new Vector2D(3.0 * Math.Cos(2.0 * Math.PI * i / n), 3.0 * Math.Sin(2.0 * Math.PI * i / n)))
            .ToArray();

        // Act
        var (_, indices) = TriangulateAndCheck(points);

        // Assert
        Assert.Equal(n - 2, indices.Count / 3);
    }

    /// <summary>
    ///     Validates that collinear vertices on edges are all used and no degenerate triangle appears.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_CollinearVerticesOnEdges_UseEveryVertexWithoutDegenerateTriangles()
    {
        // Arrange: an L shape with a collinear point on every edge
        Vector2D[] polygon =
        [
            new(0, 0), new(2, 0), new(4, 0), new(4, 1), new(4, 2), new(3, 2), new(2, 2), new(2, 3), new(2, 4), new(1, 4), new(0, 4), new(0, 2),
        ];

        // Act / Assert: the helper asserts strict counter-clockwise triangles and the use of every vertex
        var (region, indices) = TriangulateAndCheck(polygon);
        Assert.Equal(12 - 2, indices.Count / 3);
        Assert.Equal(12, region.VertexCount);
    }

    /// <summary>
    ///     Validates a rectangle with many collinear points on all four sides.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RectangleWithManyCollinearPointsOnAllSides_IsExact()
    {
        // Arrange: 10 x 6 rectangle with a point at every integer position of its boundary
        var points = new List<Vector2D>();
        for (var x = 0; x < 10; x++)
        {
            points.Add(new Vector2D(x, 0));
        }

        for (var y = 0; y < 6; y++)
        {
            points.Add(new Vector2D(10, y));
        }

        for (var x = 10; x > 0; x--)
        {
            points.Add(new Vector2D(x, 6));
        }

        for (var y = 6; y > 0; y--)
        {
            points.Add(new Vector2D(0, y));
        }

        // Act / Assert
        var (region, indices) = TriangulateAndCheck([.. points]);
        Assert.Equal(60.0, region.Area, 1e-12);
        Assert.Equal(32 - 2, indices.Count / 3);
    }

    /// <summary>
    ///     Validates a ring with one hole.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RingWithOneHole_CoversRegionAndLeavesHoleEmpty()
    {
        // Arrange / Act: 10 x 10 square with a 4 x 4 hole; the helper proves the hole stays uncovered
        var (region, indices) = TriangulateAndCheck(Rect(0, 0, 10, 10), Rect(3, 3, 4, 4));

        // Assert: V + 2H - 2P = 8 + 2 - 2 = 8 triangles
        Assert.Equal(8, indices.Count / 3);
        Assert.Equal(84.0, region.Area, 1e-12);
    }

    /// <summary>
    ///     Validates a rectangle with a regular grid of holes whose vertices are mutually collinear.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RectangleWithGridOfHoles_IsExact()
    {
        // Arrange: 3 x 3 grid of 4 x 4 holes on a 10 pitch, so rows and columns of vertices are collinear
        var contours = new List<Vector2D[]> { Rect(0, 0, 40, 40) };
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                contours.Add(Rect(5 + (10 * column), 5 + (10 * row), 4, 4));
            }
        }

        // Act / Assert
        var (region, _) = TriangulateAndCheck([.. contours]);
        Assert.Equal(1600.0 - (9 * 16.0), region.Area, 1e-9);
    }

    /// <summary>
    ///     Validates a hole containing an island, next to a separate second outer contour.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_HoleWithIslandAndSecondOuterContour_IsExact()
    {
        // Arrange: outer 40, hole 30, island 10, and a separate square to the right
        var (region, _) = TriangulateAndCheck(Rect(0, 0, 40, 40), Rect(5, 5, 30, 30), Rect(15, 15, 10, 10), Rect(50, 0, 10, 10));

        // Assert
        Assert.Equal(1600.0 - 900.0 + 100.0 + 100.0, region.Area, 1e-9);
    }

    /// <summary>
    ///     Validates a hole whose rightward ray passes exactly through a reflex vertex of the outer contour.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_HoleWhoseBridgeRayHitsReflexVertex_IsExact()
    {
        // Arrange: a notch whose tip (12,10) is hit by the ray from the diamond hole's rightmost vertex (7,10)
        Vector2D[] outer = [new(0, 0), new(20, 0), new(20, 8), new(12, 10), new(20, 12), new(20, 20), new(0, 20)];
        Vector2D[] hole = [new(3, 10), new(5, 8), new(7, 10), new(5, 12)];

        // Act / Assert
        TriangulateAndCheck(outer, hole);
        TriangulateAndCheck(outer, Enumerable.Reverse(hole).ToArray());
    }

    /// <summary>
    ///     Validates holes whose vertices are collinear with outer vertices.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_HoleVertexCollinearWithOuterVertices_BridgesToNearestVisible()
    {
        // Arrange: outer vertices (10,4) and (10,6) share the lines y = 4 and y = 6 with the hole's right side
        Vector2D[] outer = [new(0, 0), new(10, 0), new(10, 4), new(10, 6), new(10, 10), new(0, 10)];
        Vector2D[] hole = [new(4, 4), new(6, 4), new(6, 6), new(4, 6)];

        // Act / Assert
        var (_, indices) = TriangulateAndCheck(outer, hole);
        Assert.Equal(10 + 2 - 2, indices.Count / 3);
    }

    /// <summary>
    ///     Validates random star polygons, with a fixed seed per case.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RandomStarPolygons_Fuzz_CoverRegionExactly()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            // Arrange
            var random = new Random(seed);
            var center = new Vector2D(random.NextDouble() * 100.0, random.NextDouble() * 100.0);
            var polygon = Star(random, center, 5 + random.Next(0, 60), 0.5, 10.0);

            // Act / Assert
            TriangulateAndCheck(polygon);
        }
    }

    /// <summary>
    ///     Validates random layouts of holes (some with islands) inside a rectangle with collinear boundary
    ///     points, with a fixed seed per case.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_RandomHoleLayouts_Fuzz_CoverRegionExactly()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            // Arrange: a 100 x 100 outer contour with extra points on the bottom edge, and one candidate hole per
            // 20 x 20 grid cell, each kept at least one unit away from the cell border
            var random = new Random(seed);
            var outer = new List<Vector2D>();
            for (var x = 0; x < 100; x += random.Next(0, 2) == 0 ? 10 : 50)
            {
                outer.Add(new Vector2D(x, 0));
            }

            outer.AddRange([new Vector2D(100, 0), new Vector2D(100, 100), new Vector2D(0, 100)]);
            var contours = new List<Vector2D[]> { outer.ToArray() };
            for (var row = 0; row < 5; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    if (random.NextDouble() < 0.4)
                    {
                        continue;
                    }

                    var center = new Vector2D(10.0 + (20.0 * column) + (random.NextDouble() - 0.5), 10.0 + (20.0 * row) + (random.NextDouble() - 0.5));
                    var radius = 4.0 + (4.0 * random.NextDouble());

                    // Some holes contain an island that fits inside the hole's inscribed circle (needs more vertices)
                    var withIsland = random.NextDouble() < 0.3;
                    var hole = Star(random, center, withIsland ? 8 + random.Next(0, 4) : 5 + random.Next(0, 6), radius * 0.6, radius);
                    contours.Add(random.Next(0, 2) == 0 ? hole : Enumerable.Reverse(hole).ToArray());
                    if (withIsland)
                    {
                        contours.Add(Star(random, center, 5, 0.3, radius * 0.2));
                    }
                }
            }

            // Act / Assert
            TriangulateAndCheck([.. contours]);
        }
    }

    /// <summary>
    ///     Validates vertices that are almost, but not exactly, collinear.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_NearlyCollinearVertices_StillTriangulates()
    {
        // Arrange: tiny notches along the bottom edge (a 1e-9 dent and a smallest-positive-double dent)
        Vector2D[] polygon = [new(0, 0), new(1, 0), new(2, 1e-9), new(3, 0), new(4, double.Epsilon), new(5, 0), new(5, 1), new(0, 1)];

        // Act / Assert
        var (_, indices) = TriangulateAndCheck(polygon);
        Assert.Equal(6, indices.Count / 3);
    }

    /// <summary>
    ///     Validates that an empty region has no triangles.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_EmptyRegion_ReturnsNoTriangles()
    {
        // Arrange / Act / Assert
        Assert.Empty(PolygonTriangulator.Triangulate(PolygonRegion.Empty));
    }

    /// <summary>
    ///     Validates that repeated calls give identical results.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_SameInput_IsDeterministic()
    {
        // Arrange
        var region = new PolygonRegion([Rect(0, 0, 40, 40), Rect(5, 5, 10, 10), Rect(20, 20, 10, 10)]);

        // Act
        var first = PolygonTriangulator.Triangulate(region);
        var second = PolygonTriangulator.Triangulate(region);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that a null region is rejected.
    /// </summary>
    [Fact]
    public void PolygonTriangulator_Triangulate_NullRegion_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => PolygonTriangulator.Triangulate(null!));
    }
}
