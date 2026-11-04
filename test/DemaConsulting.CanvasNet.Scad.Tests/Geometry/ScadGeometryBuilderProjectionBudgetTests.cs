using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadProjectionTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the budgets of the <c>projection</c> module: work that is over the ceiling is reported and omitted
///     before anything is allocated, the slabs of the footprints must fit the triangle budget, and what is charged
///     matches what is built.
/// </summary>
public class ScadGeometryBuilderProjectionBudgetTests
{
    /// <summary>A silhouette whose classification costs more than the ceiling: a fine sphere with a cutter.</summary>
    private const string TooComplex = "projection() difference() { sphere(r = 10, $fn = 100); cube(5, center = true); }";

    /// <summary>A plain silhouette: a sphere without a boolean, so the projection never slices.</summary>
    private const string Sphere = "sphere(r = 5, $fn = 24);";

    /// <summary>
    ///     Validates that a classification that would cost more than the ceiling is reported as too complex, the
    ///     projection is omitted, and nothing but the children was charged.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_TooComplexClassification_IsReportedAndOmitted()
    {
        // Arrange: the children alone, to know what they cost
        var children = Build("difference() { sphere(r = 10, $fn = 100); cube(5, center = true); }");

        // Act
        var result = Build(TooComplex);

        // Assert: reported at the module, no slab built, and no triangle charged beyond the children
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(ScadFeatureIds.ProjectionTooComplex, report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), report.Location);
        Assert.Contains("is omitted", report.Message, StringComparison.Ordinal);
        Assert.Empty(result.Meshes);
        Assert.Equal(children.TrianglesCharged, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that a plain silhouette (no boolean, so no slice) is bound by the work ceiling too: the passes that
    ///     group and merge its faces are charged, so a ceiling that holds only the children is reported and omitted.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_SilhouetteOverTheWorkCeiling_IsReportedAndOmitted()
    {
        // Arrange: the children alone, and the work they cost (triangles plus vertices); one more visit is allowed
        var children = Build(Sphere);
        var work = ChildrenWork(children);
        var settings = Settings(maxProjectionWork: work + 1);

        // Act
        var result = Build("projection() " + Sphere, settings);

        // Assert: reported at the module, no slab built, and no triangle charged beyond the children
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(ScadFeatureIds.ProjectionTooComplex, report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), report.Location);
        Assert.Contains("is omitted", report.Message, StringComparison.Ordinal);
        Assert.Empty(result.Meshes);
        Assert.Equal(children.TrianglesCharged, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that the same silhouette is built when the ceiling is the default or ample, so an ordinary render
    ///     is not affected by the charges.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void ScadGeometryBuilderProjectionBudget_SilhouetteWithinTheWorkCeiling_IsBuilt(int multiple)
    {
        // Arrange: the default ceiling, or a multiple of the work of the children
        var work = ChildrenWork(Build(Sphere));
        var settings = multiple == 0 ? Settings() : Settings(maxProjectionWork: multiple * work);

        // Act
        var result = Build("projection() " + Sphere, settings);

        // Assert
        Assert.Empty(result.Unsupported);
        Assert.NotEmpty(result.Meshes);
    }

    /// <summary>
    ///     Validates the exact cost of a flat silhouette: the children, the scan, the merge and the footprint pass are each
    ///     charged, and the footprint pass is the one that goes over the ceiling when only it is missing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_SilhouetteFootprintPass_IsCharged()
    {
        // Arrange: the cost of the passes before the footprint pass, from the children (the cap is the only upward face)
        const string cylinder = "cylinder(r = 10, h = 1, $fn = 200);";
        var children = Build(cylinder);
        var work = ChildrenWork(children);
        var mesh = children.Meshes[0].Mesh;
        var upward = UpwardTriangles(mesh);
        var triangles = (long)mesh.TriangleCount;
        var beforeFootprint = work + triangles + (2L * upward);

        // Act
        var missing = Build("projection() " + cylinder, Settings(maxProjectionWork: beforeFootprint + (2L * upward) - 1));
        var enough = Build("projection() " + cylinder, Settings(maxProjectionWork: beforeFootprint + (2L * upward)));

        // Assert: one visit short of the footprint pass is reported, the exact cost is built
        var report = Assert.Single(missing.Unsupported);
        Assert.Equal(ScadFeatureIds.ProjectionTooComplex, report.Feature);
        Assert.Empty(missing.Meshes);
        Assert.Empty(enough.Unsupported);
        Assert.NotEmpty(enough.Meshes);
    }

    /// <summary>
    ///     Validates that a polyhedron with a huge list of points and one face is bound by its vertices, not only by its
    ///     triangle: the preflight counts the vertices that the per-vertex storage is sized by.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_PolyhedronWithManyPoints_IsBoundedByItsVertices()
    {
        // Arrange: 2,003 distinct points and one triangle, with a ceiling of 1,000 visits
        const string source = "projection() polyhedron(points = concat([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [for (i = [1 : 2000]) [i, 2 * i, 1]]), faces = [[0, 1, 2]]);";
        var settings = Settings(maxProjectionWork: 1_000);

        // Act
        var result = Build(source, settings);

        // Assert: reported at the module and omitted
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(ScadFeatureIds.ProjectionTooComplex, report.Feature);
        Assert.Contains("vertices", report.Message, StringComparison.Ordinal);
        Assert.Empty(result.Meshes);
    }

    /// <summary>
    ///     Validates that the sparse vertex remap indexes correctly when most of the vertices are not referenced and the
    ///     face uses the highest indices: the shadow is the triangle.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_PolyhedronWithManyPoints_ProjectsItsFace()
    {
        // Arrange: 200,000 unreferenced points, then the three points of the face at the highest indices
        const string source = "projection() polyhedron(points = concat([for (i = [1 : 200000]) [i, 2 * i, 1]], [[0, 0, 0], [10, 0, 0], [0, 10, 0]]), faces = [[200000, 200001, 200002]]);";

        // Act
        var result = Build(source);
        var area = Area(source, 5, 5, 20, 20, 10);

        // Assert: the triangle with the legs of 10 (area 50), nothing reported
        Assert.Empty(result.Unsupported);
        Assert.Single(result.Meshes);
        Assert.Equal(50.0, area, 1.0);
    }

    /// <summary>
    ///     Validates that strict mode turns the too-complex report into the unsupported-feature exception.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_TooComplexClassification_ThrowsInStrictMode()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build(TooComplex, Settings(strict: true)));

        // Assert
        Assert.Equal(ScadFeatureIds.ProjectionTooComplex, ex.Feature);
    }

    /// <summary>
    ///     Validates that the slabs the footprints could need are checked against the triangle budget before any region
    ///     is built, with the located triangle-limit error.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_SlabsOverTheBudget_FailBeforeAllocating()
    {
        // Arrange: a budget that holds the sphere but not the (at most) eight triangles per candidate its slabs could need
        var sphere = Build("sphere(r = 5, $fn = 24);");
        var settings = Settings(maxTriangles: sphere.TrianglesCharged + 8);

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("\n  projection() sphere(r = 5, $fn = 24);", settings));

        // Assert: the limit and the location of the projection
        Assert.Contains("Triangle limit exceeded", ex.Message, StringComparison.Ordinal);
        Assert.Contains("2:3", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the triangles charged are exactly the ones of the meshes created: the cube that is consumed
    ///     and the slab of its shadow.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_Charge_EqualsTheMeshesCreated()
    {
        // Arrange / Act
        var result = Build("projection() cube(5);");

        // Assert: the 12 triangles of the cube plus the triangles of the one slab that is left
        Assert.Equal(12L + result.MeshTriangles, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that a budget that holds exactly the work a plain shadow needs builds it.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_RoomyBudget_BuildsTheShadow()
    {
        // Arrange / Act
        var result = Build("projection() cube(5);", Settings(maxTriangles: 1_000));

        // Assert
        Assert.Single(result.Meshes);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the shadow of a boolean with an ordinary amount of detail is classified within the ceiling.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionBudget_OrdinaryBoolean_IsClassified()
    {
        // Arrange / Act
        var result = Build("projection() difference() { cylinder(r = 10, h = 4, $fn = 64); cylinder(r = 3, h = 8, center = true, $fn = 64); }");

        // Assert
        Assert.Empty(result.Unsupported);
        Assert.NotEmpty(result.Meshes);
    }

    /// <summary>
    ///     Gets the work the projection charges for its children: the triangles plus the vertices of every mesh.
    /// </summary>
    /// <param name="children">The build of the children alone.</param>
    /// <returns>The work, in triangle visits.</returns>
    private static long ChildrenWork(ScadBuildResult children) =>
        children.Meshes.Sum(m => (long)m.Mesh.TriangleCount + m.Mesh.VertexCount);

    /// <summary>
    ///     Counts the triangles of a mesh that face up.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The number of triangles whose normal has a positive Z.</returns>
    private static long UpwardTriangles(Mesh mesh)
    {
        long count = 0;
        var indices = mesh.TriangleIndices;
        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
            var a = mesh.Vertices[indices[i]];
            var b = mesh.Vertices[indices[i + 1]];
            var c = mesh.Vertices[indices[i + 2]];
            if (((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X)) > 1e-9)
            {
                count++;
            }
        }

        return count;
    }
}
