using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the 2D shapes of <see cref="ScadPrimitives"/> (<c>square</c>, <c>circle</c>, <c>polygon</c>) as
///     they appear in a built scene: a top-level 2D shape is a thin slab, so its area is checked through the slab's
///     volume.
/// </summary>
public class ScadPrimitivesShape2DTests
{
    /// <summary>The default slab thickness.</summary>
    private const double Thickness = 0.1;

    /// <summary>
    ///     Asserts that the scene holds one closed slab with the given area and the default thickness.
    /// </summary>
    /// <param name="result">The build result.</param>
    /// <param name="area">The expected area.</param>
    private static void AssertSlab(ScadBuildResult result, double area)
    {
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.Equal(area * Thickness, mesh.SignedVolume, area * Thickness * 1e-9);
        Assert.Equal(0.0, mesh.Bounds.Min.Z, 1e-12);
        Assert.Equal(Thickness, mesh.Bounds.Max.Z, 1e-12);
    }

    /// <summary>
    ///     Validates that a square renders as a slab of its area, with the size forms and <c>center</c>.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="minX">The expected minimum X.</param>
    /// <param name="minY">The expected minimum Y.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="depth">The expected depth.</param>
    [Theory]
    [InlineData("square(5);", 0.0, 0.0, 5.0, 5.0)]
    [InlineData("square();", 0.0, 0.0, 1.0, 1.0)]
    [InlineData("square([2, 3]);", 0.0, 0.0, 2.0, 3.0)]
    [InlineData("square([2, 3], center = true);", -1.0, -1.5, 2.0, 3.0)]
    [InlineData("square(size = 4, center = true);", -2.0, -2.0, 4.0, 4.0)]
    [InlineData("square(4, true);", -2.0, -2.0, 4.0, 4.0)]
    public void ScadPrimitives_Square_Forms_GiveSlabOfExpectedArea(string source, double minX, double minY, double width, double depth)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        AssertSlab(result, width * depth);
        var bounds = result.SingleMesh.Bounds;
        Assert.Equal(minX, bounds.Min.X, 1e-12);
        Assert.Equal(minY, bounds.Min.Y, 1e-12);
        Assert.Equal(minX + width, bounds.Max.X, 1e-12);
        Assert.Equal(minY + depth, bounds.Max.Y, 1e-12);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that an invalid square is reported and builds nothing.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="problem">The expected part of the warning.</param>
    [Theory]
    [InlineData("square(0);", "size must be positive")]
    [InlineData("square([1, -1]);", "size must be positive")]
    [InlineData("square(\"x\");", "size must be a positive number or a vector of two positive numbers")]
    [InlineData("square([1, 2, 3]);", "size must be a positive number or a vector of two positive numbers")]
    public void ScadPrimitives_Square_InvalidSize_WarnsAndBuildsNothing(string source, string problem)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal([$"1:1: square: {problem}; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that a circle renders as a slab whose area is that of the inscribed polygon the fragment rule
    ///     selects, for radius, diameter, <c>$fn</c> and the default count.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="radius">The expected radius of the circle the polygon is inscribed in.</param>
    /// <param name="sides">The expected number of sides.</param>
    [Theory]
    [InlineData("circle(r = 2, $fn = 24);", 2.0, 24)]
    [InlineData("circle(d = 6, $fn = 24);", 3.0, 24)]
    [InlineData("circle(r = 1, d = 8, $fn = 12);", 4.0, 12)]
    [InlineData("circle(5, $fn = 12);", 5.0, 12)]
    [InlineData("circle();", 1.0, 5)]
    [InlineData("circle(10);", 10.0, 30)]
    [InlineData("$fn = 8; circle(3);", 3.0, 8)]
    public void ScadPrimitives_Circle_Forms_GiveInscribedPolygonSlab(string source, double radius, int sides)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        AssertSlab(result, RegularPolygonArea(radius, sides));
    }

    /// <summary>
    ///     Validates that an invalid circle is reported and builds nothing.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("circle(0);")]
    [InlineData("circle(-2);")]
    [InlineData("circle(\"x\");")]
    [InlineData("circle(d = -1);")]
    public void ScadPrimitives_Circle_InvalidSize_WarnsAndBuildsNothing(string source)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal(["1:1: circle: the radius and the diameter must be positive numbers; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that a polygon of one contour renders as a slab of its area whatever the winding.
    /// </summary>
    /// <param name="points">The points of a right triangle with area 6.</param>
    [Theory]
    [InlineData("[[0, 0], [4, 0], [0, 3]]")]
    [InlineData("[[0, 0], [0, 3], [4, 0]]")]
    public void ScadPrimitives_Polygon_SingleContour_WindingIndependent(string points)
    {
        // Arrange / Act
        var result = Build($"polygon({points});");

        // Assert
        AssertSlab(result, 6.0);
    }

    /// <summary>
    ///     Validates that polygon paths describe holes by nesting, regardless of the winding of the paths, and that
    ///     separate paths describe separate islands.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="area">The expected area.</param>
    [Theory]
    [InlineData("polygon(points = [[0,0],[10,0],[10,10],[0,10],[3,3],[7,3],[7,7],[3,7]], paths = [[0,1,2,3],[4,5,6,7]]);", 84.0)]
    [InlineData("polygon([[0,0],[10,0],[10,10],[0,10],[3,3],[3,7],[7,7],[7,3]], [[0,1,2,3],[4,5,6,7]]);", 84.0)]
    [InlineData("polygon([[0,0],[2,0],[2,2],[0,2],[5,0],[7,0],[7,2],[5,2]], [[0,1,2,3],[4,5,6,7]]);", 8.0)]
    [InlineData("polygon([[0,0],[10,0],[10,10],[0,10],[2,2],[8,2],[8,8],[2,8],[4,4],[6,4],[6,6],[4,6]], [[0,1,2,3],[4,5,6,7],[8,9,10,11]]);", 100.0 - 36.0 + 4.0)]
    public void ScadPrimitives_Polygon_Paths_DescribeHolesAndIslands(string source, double area)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        AssertSlab(result, area);
    }

    /// <summary>
    ///     Validates that unused points do not inflate the triangle-budget pre-check when paths select one triangle.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polygon_Paths_IgnoreUnusedPointsForTriangleBudget()
    {
        // Arrange
        var source = PolygonWithPaths(100, "[0, 1, 2]");

        // Act
        var result = Build(source, Settings(maxTriangles: 100));

        // Assert
        Assert.Equal(8, result.SingleMesh.TriangleCount);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a large path still fails the triangle-budget pre-check even when the point list is valid.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polygon_Paths_LargeReferencedPath_ExceedsTriangleBudget()
    {
        // Arrange
        var path = $"[{string.Join(", ", Enumerable.Range(0, 100))}]";
        var source = PolygonWithPaths(100, path);

        // Act
        var exception = Assert.Throws<ScadEvaluationException>(() => Build(source, Settings(maxTriangles: 100)));

        // Assert
        Assert.Contains("Triangle limit exceeded (limit 100)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the exact triangle charge for a sparse polygon equals the triangles in the created slab.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polygon_Paths_ChargeEqualsCreatedTriangles()
    {
        // Arrange
        var source = PolygonWithPaths(100, "[0, 1, 2]");

        // Act
        var result = Build(source, Settings(maxTriangles: 100));

        // Assert
        Assert.Equal(result.MeshTriangles, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that a polygon the region validation rejects is reported as the unsupported feature
    ///     <c>scad-polygon-invalid</c> at the instance's location and builds nothing.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polygon_SelfCrossing_ReportsPolygonInvalid()
    {
        // Arrange / Act: a bow tie crosses itself
        var result = Build("\npolygon([[0,0],[2,2],[2,0],[0,2]]);");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-polygon-invalid", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), report.Location);
        Assert.StartsWith("polygon: the shape was rejected (it is not a valid planar shape", report.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("..", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that strict mode turns the polygon rejection into an exception with the feature and location.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polygon_SelfCrossing_StrictThrows()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => Build("polygon([[0,0],[2,2],[2,0],[0,2]]);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-polygon-invalid", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), ex.Location);
    }

    /// <summary>
    ///     Validates the argument errors of <c>polygon</c> as warnings.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="problem">The expected part of the warning.</param>
    [Theory]
    [InlineData("polygon();", "points is required")]
    [InlineData("polygon([[0,0],[1,0]]);", "points must be a list of at least three [x, y] pairs of finite numbers")]
    [InlineData("polygon(5);", "points must be a list of at least three [x, y] pairs of finite numbers")]
    [InlineData("polygon([[0,0],[1,0],[0,\"a\"]]);", "points must be a list of at least three [x, y] pairs of finite numbers")]
    [InlineData("polygon([[0,0],[1,0],[0,1,2]]);", "points must be a list of at least three [x, y] pairs of finite numbers")]
    [InlineData("polygon([[0,0],[1,0],[0,1]], []);", "paths must be a non-empty list of lists of valid point indices")]
    [InlineData("polygon([[0,0],[1,0],[0,1]], [[0,1,3]]);", "paths must be a non-empty list of lists of valid point indices")]
    [InlineData("polygon([[0,0],[1,0],[0,1]], [[0,1,-1]]);", "paths must be a non-empty list of lists of valid point indices")]
    [InlineData("polygon([[0,0],[1,0],[0,1]], [0,1,2]);", "paths must be a non-empty list of lists of valid point indices")]
    [InlineData("polygon([[0,0],[1,0],[0,1]], 7);", "paths must be a non-empty list of lists of valid point indices")]
    public void ScadPrimitives_Polygon_InvalidArguments_WarnAndBuildNothing(string source, string problem)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal([$"1:1: polygon: {problem}; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that the slab thickness follows the setting.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_FlatShapeThickness_SetsSlabHeight()
    {
        // Arrange / Act
        var mesh = Build("square(2);", Settings(thickness: 2.5)).SingleMesh;

        // Assert
        Assert.Equal(2.5, mesh.Bounds.Max.Z, 1e-12);
        Assert.Equal(4.0 * 2.5, mesh.SignedVolume, 1e-9);
    }

    /// <summary>
    ///     Validates that several 2D shapes, also through groups, are all rendered as slabs.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_SeveralFlatShapes_AreAllSlabs()
    {
        // Arrange / Act
        var result = Build("module m() { square(1); circle(1, $fn = 8); }\nm();\nfor (i = [1:2]) square(i);");

        // Assert
        Assert.Equal(4, result.Meshes.Count);
        Assert.All(result.Meshes, m => Assert.True(m.Mesh.IsClosed));
        Assert.Equal([0.1, RegularPolygonArea(1, 8) * 0.1, 0.1, 0.4], result.Meshes.Select(m => m.Mesh.SignedVolume).ToArray(), new ToleranceComparer());
    }

    /// <summary>
    ///     Validates that mixing 2D and 3D children keeps the 3D ones and warns about the 2D ones at the first
    ///     dropped shape's location.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_MixedDimensions_KeepsSolidsAndWarns()
    {
        // Arrange / Act
        var result = Build("cube(1);\n  square(2);");

        // Assert
        AssertSolidCube(result);
        Assert.Equal(["2:3: A union mixes 2D and 3D objects; the 2D objects are ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that an ordinary flat shape becomes a slab without any report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_FlatShape_ReportsNothing()
    {
        // Arrange / Act
        var result = Build("circle(5);");

        // Assert
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Asserts that the scene holds exactly the unit cube.
    /// </summary>
    /// <param name="result">The build result.</param>
    private static void AssertSolidCube(ScadBuildResult result)
    {
        var mesh = result.SingleMesh;
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
        Assert.Equal(1.0, mesh.Bounds.Max.Z, 1e-12);
    }

    /// <summary>
    ///     Creates a polygon source with a triangle path and a configurable number of points.
    /// </summary>
    /// <param name="pointCount">The number of points.</param>
    /// <param name="path">The path expression.</param>
    /// <returns>The polygon source.</returns>
    private static string PolygonWithPaths(int pointCount, string path)
    {
        var points = string.Join(
            ", ",
            Enumerable.Range(0, pointCount).Select(
                index => index switch
                {
                    0 => "[0, 0]",
                    1 => "[4, 0]",
                    2 => "[0, 3]",
                    _ => $"[{index}, {index}]",
                }));
        return $"polygon(points = [{points}], paths = [{path}]);";
    }

    /// <summary>
    ///     Compares doubles with a small absolute tolerance.
    /// </summary>
    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        /// <inheritdoc />
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        /// <inheritdoc />
        public int GetHashCode(double obj) => 0;
    }
}
