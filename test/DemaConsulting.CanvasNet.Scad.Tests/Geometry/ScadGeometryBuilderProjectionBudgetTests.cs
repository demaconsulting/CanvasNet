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
}
