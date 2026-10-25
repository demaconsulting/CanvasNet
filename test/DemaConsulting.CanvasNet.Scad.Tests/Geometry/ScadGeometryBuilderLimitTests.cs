using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the triangle budget of <see cref="ScadGeometryBuilder"/>: estimates are charged before anything is
///     allocated, accumulate across instances and loops, and fail with the offending instance's location.
/// </summary>
public class ScadGeometryBuilderLimitTests
{
    /// <summary>
    ///     Validates that a hostile fragment count fails with the located limit error before any mesh is allocated.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("sphere(1, $fn = 1000000);")]
    [InlineData("cylinder(h = 1, r = 1, $fn = 1000000000);")]
    [InlineData("circle(1, $fn = 1000000000);")]
    [InlineData("polygon([[0,0],[1,0],[0,1]]); circle(1, $fn = 1000000000);")]
    public void ScadGeometryBuilder_Build_HostileFragmentCount_ThrowsLocatedLimitError(string source)
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("\n  " + source, Settings(maxTriangles: 1000)));

        // Assert
        Assert.Contains("Triangle limit exceeded (limit 1000)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that the default budget alone stops a sphere with a huge fragment count.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_HugeSphereUnderDefaultBudget_ThrowsLocatedLimitError()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("\nsphere(1, $fn = 1000000);"));

        // Assert
        Assert.Equal("2:1: Triangle limit exceeded (limit 5000000)", ex.Message);
    }

    /// <summary>
    ///     Validates that the budget accumulates across the instances of a loop: nine cubes (12 triangles each) do not
    ///     fit in 100 triangles, eight do.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_BudgetAccumulatesAcrossLoopIterations()
    {
        // Arrange / Act
        var fits = Build("for (i = [1:8]) cube(1);", Settings(maxTriangles: 100));
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("for (i = [1:9]) cube(1);", Settings(maxTriangles: 100)));

        // Assert
        Assert.Equal(8, fits.Meshes.Count);
        Assert.Equal(new ScadSourceLocation(null, 1, 17), ex.Location);
        Assert.Equal("1:17: Triangle limit exceeded (limit 100)", ex.Message);
    }

    /// <summary>
    ///     Validates the exact boundary of the estimates of a cylinder (four triangles per fragment at most).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_CylinderEstimate_IsFourTrianglesPerFragment()
    {
        // Arrange / Act
        var fits = Build("cylinder(h = 1, r = 1, $fn = 100);", Settings(maxTriangles: 400));
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("cylinder(h = 1, r = 1, $fn = 100);", Settings(maxTriangles: 399)));

        // Assert
        Assert.True(fits.SingleMesh.TriangleCount <= 400);
        Assert.Contains("limit 399", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a 2D shape is charged for the slab it becomes, with the exact count (<c>4V - 4</c> for a
    ///     shape with one contour of <c>V</c> vertices: <c>2V</c> wall and two caps of <c>V - 2</c> triangles), checked
    ///     when the slab is made.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_FlatShape_IsChargedForItsSlab()
    {
        // Arrange / Act
        var fits = Build("circle(1, $fn = 1000);", Settings(maxTriangles: 3996));
        var tooSmall = Assert.Throws<ScadEvaluationException>(() => Build("circle(1, $fn = 1000);", Settings(maxTriangles: 3995)));
        var twoShapes = Assert.Throws<ScadEvaluationException>(() => Build("square(1); circle(1, $fn = 10);", Settings(maxTriangles: 19)));

        // Assert
        Assert.Single(fits.Meshes);
        Assert.Equal(3996, fits.TrianglesCharged);
        Assert.Contains("limit 3995", tooSmall.Message, StringComparison.Ordinal);
        Assert.Contains("limit 19", twoShapes.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the limit reaches the builder through the public options: a model over
    ///     <see cref="ScadLimits.MaxTriangles"/> fails <see cref="ScadDocument.BuildScene"/> with the located error.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_PublicMaxTriangles_FailsBuildScene()
    {
        // Arrange
        var document = ScadDocument.Parse("sphere(10, $fn = 100);");
        var options = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxTriangles = 1000 } } };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => document.BuildScene(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("1:1: Triangle limit exceeded (limit 1000)", ex.Message);
    }
}
