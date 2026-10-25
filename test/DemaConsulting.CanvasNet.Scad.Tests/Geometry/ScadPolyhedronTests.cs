// cspell:ignore polyhedron tetrahedron Newell

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the <c>polyhedron</c> builder of <see cref="ScadPrimitives"/>:
///     winding reversal, face triangulation, the legacy <c>triangles</c> parameter, argument errors and the
///     unsupported-feature report of a face that cannot be triangulated.
/// </summary>
public class ScadPolyhedronTests
{
    /// <summary>The corners of the unit cube; corner <c>i</c> has x = bit 0, y = bit 1, z = bit 2 of <c>i</c>.</summary>
    private const string CubePoints = "[[0,0,0],[1,0,0],[0,1,0],[1,1,0],[0,0,1],[1,0,1],[0,1,1],[1,1,1]]";

    /// <summary>The faces of the unit cube listed clockwise when seen from outside, as the language expects.</summary>
    private const string CubeFacesClockwise = "[[1,3,2,0],[6,7,5,4],[4,5,1,0],[3,7,6,2],[2,6,4,0],[5,7,3,1]]";

    /// <summary>The faces of the unit cube listed counter-clockwise when seen from outside (the wrong way for the language).</summary>
    private const string CubeFacesCounterClockwise = "[[0,2,3,1],[4,5,7,6],[0,1,5,4],[2,6,7,3],[0,4,6,2],[1,3,7,5]]";

    /// <summary>The corners of a tetrahedron.</summary>
    private const string TetrahedronPoints = "[[0,0,0],[1,0,0],[0,1,0],[0,0,1]]";

    /// <summary>The faces of the tetrahedron listed clockwise when seen from outside.</summary>
    private const string TetrahedronFaces = "[[1,2,0],[3,1,0],[2,3,0],[3,2,1]]";

    /// <summary>The corners of an L-shaped prism: the L at z = 0 (indices 0 to 5) and at z = 1 (6 to 11).</summary>
    private const string LPrismPoints =
        "[[0,0,0],[2,0,0],[2,1,0],[1,1,0],[1,2,0],[0,2,0],[0,0,1],[2,0,1],[2,1,1],[1,1,1],[1,2,1],[0,2,1]]";

    /// <summary>The faces of the L prism listed clockwise from outside; the top and bottom faces are concave.</summary>
    private const string LPrismFaces =
        "[[0,1,2,3,4,5],[11,10,9,8,7,6],[6,7,1,0],[7,8,2,1],[8,9,3,2],[9,10,4,3],[10,11,5,4],[11,6,0,5]]";

    /// <summary>
    ///     Validates that a cube listed clockwise from outside is closed with positive volume and no warning, with
    ///     every face reversed to counter-clockwise.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_ClockwiseCube_IsClosedWithPositiveVolume()
    {
        // Arrange / Act
        var result = Build($"polyhedron({CubePoints}, {CubeFacesClockwise});");

        // Assert
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
        Assert.Equal(8, mesh.VertexCount);
        Assert.Equal(12, mesh.TriangleCount);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a cube listed counter-clockwise from outside is turned right way round with a warning.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_CounterClockwiseCube_IsReversedWithWarning()
    {
        // Arrange / Act
        var result = Build($"\n polyhedron(points = {CubePoints}, faces = {CubeFacesCounterClockwise});");

        // Assert
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
        Assert.Equal(
            ["2:2: polyhedron: the faces appear to be listed counter-clockwise when seen from outside; the winding was reversed"],
            result.WarningTexts);
    }

    /// <summary>
    ///     Validates a tetrahedron: closed, a sixth of a unit cube, and the positional argument order.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_Tetrahedron_HasExpectedVolume()
    {
        // Arrange / Act
        var named = Build($"polyhedron(faces = {TetrahedronFaces}, points = {TetrahedronPoints});").SingleMesh;
        var positional = Build($"polyhedron({TetrahedronPoints}, {TetrahedronFaces});").SingleMesh;

        // Assert
        Assert.True(named.IsClosed);
        Assert.Equal(1.0 / 6.0, named.SignedVolume, 1e-12);
        Assert.Equal(named.SignedVolume, positional.SignedVolume, 1e-15);
    }

    /// <summary>
    ///     Validates that concave planar faces are triangulated correctly: the L prism is closed with the exact
    ///     volume (the L has area 3) and the expected triangle count.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_ConcaveFaces_AreTriangulated()
    {
        // Arrange / Act
        var result = Build($"polyhedron({LPrismPoints}, {LPrismFaces});");

        // Assert: two concave hexagons give 4 triangles each, six quads give 2 each
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.Equal(3.0, mesh.SignedVolume, 1e-12);
        Assert.Equal((2 * 4) + (6 * 2), mesh.TriangleCount);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a non-planar convex quad is accepted and split by a fan, giving a closed mesh.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_NonPlanarQuad_IsFannedAndClosed()
    {
        // Arrange: the unit cube whose corner 7 is raised, so the top face is not planar
        const string raised = "[[0,0,0],[1,0,0],[0,1,0],[1,1,0],[0,0,1],[1,0,1],[0,1,1],[1,1,1.5]]";

        // Act
        var result = Build($"polyhedron({raised}, {CubeFacesClockwise});");

        // Assert
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.InRange(mesh.SignedVolume, 1.0, 1.5);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the legacy <c>triangles</c> parameter is used when <c>faces</c> is absent and that
    ///     <c>faces</c> wins when both are given.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_LegacyTriangles_UsedOnlyWithoutFaces()
    {
        // Arrange / Act
        var legacy = Build($"polyhedron(points = {TetrahedronPoints}, triangles = {TetrahedronFaces});").SingleMesh;
        var both = Build($"polyhedron(points = {TetrahedronPoints}, faces = {TetrahedronFaces}, triangles = [[0,1,2]]);").SingleMesh;

        // Assert
        Assert.True(legacy.IsClosed);
        Assert.Equal(1.0 / 6.0, legacy.SignedVolume, 1e-12);
        Assert.Equal(1.0 / 6.0, both.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that an open shape is kept as given (a single triangle here), without a warning, and that a
    ///     point no face uses is harmless.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_OpenShapeAndUnusedPoint_AreKept()
    {
        // Arrange / Act
        var open = Build("polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]);");

        // Assert
        var mesh = open.SingleMesh;
        Assert.Equal(1, mesh.TriangleCount);
        Assert.False(mesh.IsClosed);
        Assert.Empty(open.Warnings);
    }

    /// <summary>
    ///     Validates the argument errors as warnings.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="problem">The expected part of the warning.</param>
    [Theory]
    [InlineData("polyhedron();", "points is required")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0]]);", "faces is required")]
    [InlineData("polyhedron(points = 5, faces = [[0,1,2]]);", "points must be a list of [x, y, z] triples of finite numbers")]
    [InlineData("polyhedron(points = [[0,0],[1,0],[0,1]], faces = [[0,1,2]]);", "points must be a list of [x, y, z] triples of finite numbers")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,\"z\"]], faces = [[0,1,2]]);", "points must be a list of [x, y, z] triples of finite numbers")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = []);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = [[0,1,3]]);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = [[0,1]]);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = [[0,1,1]]);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = [[0,1,-2]]);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    [InlineData("polyhedron(points = [[0,0,0],[1,0,0],[0,1,0]], faces = 3);", "faces must be a non-empty list of faces, each at least three distinct point indices in range")]
    public void ScadPrimitives_Polyhedron_InvalidArguments_WarnAndBuildNothing(string source, string problem)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal([$"1:1: polyhedron: {problem}; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that a face that cannot be triangulated (collinear points, a self-crossing face) reports
    ///     <c>scad-polyhedron-face-invalid</c> at the instance's location and omits the polyhedron.
    /// </summary>
    /// <param name="points">The points.</param>
    [Theory]
    [InlineData("[[0,0,0],[1,0,0],[2,0,0],[3,0,0]]")]
    [InlineData("[[0,0,0],[2,2,0],[2,0,0],[0,2,0]]")]
    public void ScadPrimitives_Polyhedron_FaceThatCannotBeTriangulated_ReportsFaceInvalid(string points)
    {
        // Arrange / Act
        var result = Build($"\n  polyhedron({points}, [[0,1,2,3]]);");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-polyhedron-face-invalid", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
    }

    /// <summary>
    ///     Validates that strict mode turns the face report into an exception.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_FaceThatCannotBeTriangulated_StrictThrows()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => Build("polyhedron([[0,0,0],[1,0,0],[2,0,0],[3,0,0]], [[0,1,2,3]]);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-polyhedron-face-invalid", ex.Feature);
    }

    /// <summary>
    ///     Validates that a face with two indices addressing identical coordinates is reported (as a face that cannot
    ///     be triangulated or as a tessellator rejection), never as a crash.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_WeldedDuplicatePointsInOneFace_ReportsGeometryRejected()
    {
        // Arrange / Act: points 0 and 3 coincide, so the quad collapses once welded
        var result = Build("polyhedron([[0,0,0],[1,0,0],[1,1,0],[0,0,0]], [[0,1,2,3]]);");

        // Assert: the build never crashes; the collapsed quad is either reported or becomes one open triangle
        if (result.Unsupported.Count > 0)
        {
            Assert.Empty(result.Meshes);
            Assert.Contains(result.Unsupported[0].Feature, new[] { "scad-geometry-rejected", "scad-polyhedron-face-invalid" });
        }
        else
        {
            Assert.False(result.SingleMesh.IsClosed);
            Assert.Equal(1, result.SingleMesh.TriangleCount);
        }
    }

    /// <summary>
    ///     Validates that the triangle budget is charged for a polyhedron before it is triangulated.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Polyhedron_ExceedsTriangleBudget_ThrowsWithLocation()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(
            () => Build($"\npolyhedron({CubePoints}, {CubeFacesClockwise});", Settings(maxTriangles: 11)));

        // Assert
        Assert.Equal(new ScadSourceLocation(null, 2, 1), ex.Location);
        Assert.Contains("Triangle limit exceeded (limit 11)", ex.Message, StringComparison.Ordinal);
    }
}
