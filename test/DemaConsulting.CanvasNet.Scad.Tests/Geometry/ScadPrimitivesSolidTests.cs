// cspell:ignore frustum

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the solid primitives of <see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadPrimitives"/>:
///     <c>cube</c>, <c>sphere</c> and <c>cylinder</c>, built through the geometry builder.
/// </summary>
public class ScadPrimitivesSolidTests
{
    /// <summary>
    ///     Asserts that a mesh is closed, outward oriented and has the expected volume.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="volume">The expected volume.</param>
    private static void AssertSolid(Mesh mesh, double volume)
    {
        Assert.True(mesh.IsClosed);
        Assert.Equal(volume, mesh.SignedVolume, Math.Abs(volume) * 1e-9);
    }

    /// <summary>
    ///     Asserts the bounds of a mesh.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    private static void AssertBounds(Mesh mesh, Vector3D min, Vector3D max)
    {
        Assert.Equal(min.X, mesh.Bounds.Min.X, 1e-9);
        Assert.Equal(min.Y, mesh.Bounds.Min.Y, 1e-9);
        Assert.Equal(min.Z, mesh.Bounds.Min.Z, 1e-9);
        Assert.Equal(max.X, mesh.Bounds.Max.X, 1e-9);
        Assert.Equal(max.Y, mesh.Bounds.Max.Y, 1e-9);
        Assert.Equal(max.Z, mesh.Bounds.Max.Z, 1e-9);
    }

    /// <summary>
    ///     Computes the largest distance from the Z axis of the vertices at a given height.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="z">The height.</param>
    /// <returns>The largest radius at that height.</returns>
    private static double RadiusAt(Mesh mesh, double z) =>
        mesh.Vertices.Where(v => Math.Abs(v.Z - z) < 1e-9).Max(v => Math.Sqrt((v.X * v.X) + (v.Y * v.Y)));

    /// <summary>
    ///     Validates that a scalar size gives an axis-aligned cube from the origin with the exact volume.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Cube_ScalarSize_IsClosedCubeFromOrigin()
    {
        // Arrange / Act
        var result = Build("cube(10);");

        // Assert
        var mesh = result.SingleMesh;
        AssertSolid(mesh, 1000.0);
        AssertBounds(mesh, new Vector3D(0, 0, 0), new Vector3D(10, 10, 10));
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates the default size, a vector size, <c>center</c> and the positional and named forms.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    [Theory]
    [InlineData("cube();", new[] { 0.0, 0, 0 }, new[] { 1.0, 1, 1 })]
    [InlineData("cube([1, 2, 3]);", new[] { 0.0, 0, 0 }, new[] { 1.0, 2, 3 })]
    [InlineData("cube(size = [1, 2, 3], center = true);", new[] { -0.5, -1, -1.5 }, new[] { 0.5, 1, 1.5 })]
    [InlineData("cube([4, 6, 8], true);", new[] { -2.0, -3, -4 }, new[] { 2.0, 3, 4 })]
    [InlineData("cube(center = true, size = 2);", new[] { -1.0, -1, -1 }, new[] { 1.0, 1, 1 })]
    [InlineData("cube(2, center = 1);", new[] { -1.0, -1, -1 }, new[] { 1.0, 1, 1 })]
    [InlineData("cube(2, center = 0);", new[] { 0.0, 0, 0 }, new[] { 2.0, 2, 2 })]
    public void ScadPrimitives_Cube_ArgumentForms_GiveExpectedBounds(string source, double[] min, double[] max)
    {
        // Arrange / Act
        var mesh = Build(source).SingleMesh;

        // Assert
        AssertBounds(mesh, new Vector3D(min[0], min[1], min[2]), new Vector3D(max[0], max[1], max[2]));
        AssertSolid(mesh, (max[0] - min[0]) * (max[1] - min[1]) * (max[2] - min[2]));
    }

    /// <summary>
    ///     Validates that an invalid cube is reported as a warning at the instance location and builds nothing.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="problem">The expected part of the warning.</param>
    [Theory]
    [InlineData("cube(0);", "size must be positive")]
    [InlineData("cube(-3);", "size must be positive")]
    [InlineData("cube([1, 0, 1]);", "size must be positive")]
    [InlineData("cube(\"big\");", "size must be a positive number or a vector of three positive numbers")]
    [InlineData("cube([1, 2]);", "size must be a positive number or a vector of three positive numbers")]
    [InlineData("cube([1, 2, \"x\"]);", "size must be a positive number or a vector of three positive numbers")]
    public void ScadPrimitives_Cube_InvalidSize_WarnsAndBuildsNothing(string source, string problem)
    {
        // Arrange / Act
        var result = Build("\n  " + source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal([$"2:3: cube: {problem}; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that an unknown argument and child objects are warned about while the cube is still built.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Cube_UnknownArgumentAndChildren_WarnButStillBuild()
    {
        // Arrange / Act
        var result = Build("cube(1, foo = 2) { sphere(1); }");

        // Assert
        Assert.Equal(
            ["1:1: cube() takes no child objects; ignoring them", "1:1: Ignoring unknown argument 'foo' of cube"],
            result.WarningTexts);
        AssertSolid(result.SingleMesh, 1.0);
    }

    /// <summary>
    ///     Validates that the location map records the location of each instance that produced a mesh.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Cube_TwoInstances_MapRecordsEachLocation()
    {
        // Arrange / Act
        var result = Build("cube(1);\n  cube(2);");

        // Assert
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), result.Map.Find(result.Meshes[0]));
        Assert.Equal(new ScadSourceLocation(null, 2, 3), result.Map.Find(result.Meshes[1]));
        Assert.Equal(default, result.Map.Find(null));
    }

    /// <summary>
    ///     Validates the sphere: closed, poles on the axis, a volume just below the analytic one, and that a
    ///     diameter gives the same sphere as the radius.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Sphere_RadiusAndDiameter_ClosedWithFacetedVolume()
    {
        // Arrange / Act
        var byRadius = Build("sphere(r = 10, $fn = 64);").SingleMesh;
        var byDiameter = Build("sphere(d = 20, $fn = 64);").SingleMesh;

        // Assert
        Assert.True(byRadius.IsClosed);
        AssertBounds(byRadius, new Vector3D(-10, -10, -10), new Vector3D(10, 10, 10));
        var analytic = 4.0 / 3.0 * Math.PI * 1000.0;
        Assert.InRange(byRadius.SignedVolume, analytic * 0.98, analytic);
        Assert.Equal(byRadius.SignedVolume, byDiameter.SignedVolume, 1e-9);
        Assert.Equal(byRadius.VertexCount, byDiameter.VertexCount);
    }

    /// <summary>
    ///     Validates that a diameter overrides the radius and that the positional form is the radius.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Sphere_DiameterOverridesRadius_PositionalIsRadius()
    {
        // Arrange / Act
        var both = Build("sphere(r = 1, d = 8, $fn = 16);").SingleMesh;
        var positional = Build("sphere(3, $fn = 16);").SingleMesh;

        // Assert
        Assert.Equal(4.0, both.Bounds.Max.Z, 1e-9);
        Assert.Equal(3.0, positional.Bounds.Max.Z, 1e-9);
    }

    /// <summary>
    ///     Validates the fragment settings of a sphere: the default count, <c>$fn</c> from an argument and from the
    ///     dynamic scope, and the <c>$fa</c>/<c>$fs</c> rule.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="vertices">The expected vertex count: two poles plus the rings.</param>
    [Theory]
    [InlineData("sphere(1);", 12)]
    [InlineData("sphere(2, $fn = 16);", 114)]
    [InlineData("$fn = 16; sphere(2);", 114)]
    [InlineData("$fn = 16; sphere(2, $fn = 8);", 26)]
    [InlineData("sphere(10, $fa = 30, $fs = 1);", 62)]
    [InlineData("sphere(1, $fn = 1);", 5)]
    public void ScadPrimitives_Sphere_FragmentSettings_GiveExpectedVertexCount(string source, int vertices)
    {
        // Arrange / Act
        var mesh = Build(source).SingleMesh;

        // Assert
        Assert.Equal(vertices, mesh.VertexCount);
        Assert.True(mesh.IsClosed);
    }

    /// <summary>
    ///     Validates that an invalid sphere is reported and builds nothing.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("sphere(0);")]
    [InlineData("sphere(-1);")]
    [InlineData("sphere(\"x\");")]
    [InlineData("sphere(r = 1, d = \"x\");")]
    [InlineData("sphere(d = 0);")]
    public void ScadPrimitives_Sphere_InvalidSize_WarnsAndBuildsNothing(string source)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal(["1:1: sphere: the radius and the diameter must be positive numbers; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates the cylinder: the exact faceted volume, the height and <c>center</c>.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="zMin">The expected lowest Z.</param>
    /// <param name="zMax">The expected highest Z.</param>
    [Theory]
    [InlineData("cylinder(h = 10, r = 5, $fn = 32);", 0.0, 10.0)]
    [InlineData("cylinder(h = 10, r = 5, center = true, $fn = 32);", -5.0, 5.0)]
    [InlineData("cylinder(10, 5, 5, true, $fn = 32);", -5.0, 5.0)]
    [InlineData("cylinder(10, 5, 5, $fn = 32);", 0.0, 10.0)]
    [InlineData("cylinder(h = 10, d = 10, $fn = 32);", 0.0, 10.0)]
    public void ScadPrimitives_Cylinder_Forms_HaveExactFacetedVolume(string source, double zMin, double zMax)
    {
        // Arrange / Act
        var mesh = Build(source).SingleMesh;

        // Assert
        AssertSolid(mesh, RegularPolygonArea(5.0, 32) * 10.0);
        Assert.Equal(zMin, mesh.Bounds.Min.Z, 1e-9);
        Assert.Equal(zMax, mesh.Bounds.Max.Z, 1e-9);
        Assert.Equal(5.0, mesh.Bounds.Max.X, 1e-9);
    }

    /// <summary>
    ///     Validates the frustum and the cone volumes (exact for similar polygons) and the positional radii.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="bottom">The expected bottom radius.</param>
    /// <param name="top">The expected top radius.</param>
    [Theory]
    [InlineData("cylinder(h = 6, r1 = 3, r2 = 1, $fn = 16);", 3.0, 1.0)]
    [InlineData("cylinder(6, 3, 1, $fn = 16);", 3.0, 1.0)]
    [InlineData("cylinder(h = 6, d1 = 6, d2 = 2, $fn = 16);", 3.0, 1.0)]
    [InlineData("cylinder(h = 6, r1 = 3, r2 = 0, $fn = 16);", 3.0, 0.0)]
    [InlineData("cylinder(h = 6, r1 = 0, r2 = 3, $fn = 16);", 0.0, 3.0)]
    public void ScadPrimitives_Cylinder_ConeAndFrustum_HaveExactFacetedVolume(string source, double bottom, double top)
    {
        // Arrange
        var a1 = RegularPolygonArea(bottom, 16);
        var a2 = RegularPolygonArea(top, 16);

        // Act
        var mesh = Build(source).SingleMesh;

        // Assert: the volume of a prismatoid of similar sections is h / 3 (A1 + A2 + sqrt(A1 A2))
        AssertSolid(mesh, 6.0 / 3.0 * (a1 + a2 + Math.Sqrt(a1 * a2)));
        Assert.Equal(bottom, RadiusAt(mesh, 0.0), 1e-9);
        Assert.Equal(top, RadiusAt(mesh, 6.0), 1e-9);
    }

    /// <summary>
    ///     Validates the precedence of the radius parameters: the common radius, a common diameter, the per-end
    ///     radii and the per-end diameters, from weakest to strongest.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="bottom">The expected bottom radius.</param>
    /// <param name="top">The expected top radius.</param>
    [Theory]
    [InlineData("cylinder(h = 2, r = 9, $fn = 8);", 9.0, 9.0)]
    [InlineData("cylinder(h = 2, r = 9, d = 4, $fn = 8);", 2.0, 2.0)]
    [InlineData("cylinder(h = 2, r = 9, r1 = 3, $fn = 8);", 3.0, 9.0)]
    [InlineData("cylinder(h = 2, d = 4, r2 = 7, $fn = 8);", 2.0, 7.0)]
    [InlineData("cylinder(h = 2, r1 = 9, d1 = 4, r2 = 5, $fn = 8);", 2.0, 5.0)]
    [InlineData("cylinder(h = 2, r2 = 9, d2 = 4, r1 = 5, $fn = 8);", 5.0, 2.0)]
    [InlineData("cylinder(h = 2, $fn = 8);", 1.0, 1.0)]
    public void ScadPrimitives_Cylinder_RadiusPrecedence_FollowsDocumentedOrder(string source, double bottom, double top)
    {
        // Arrange / Act
        var mesh = Build(source).SingleMesh;

        // Assert
        Assert.Equal(bottom, RadiusAt(mesh, 0.0), 1e-9);
        Assert.Equal(top, RadiusAt(mesh, 2.0), 1e-9);
    }

    /// <summary>
    ///     Validates that a cylinder takes its fragment count from the larger radius.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Cylinder_FragmentsFollowLargerRadius()
    {
        // Arrange / Act: radius 10 gives 30 fragments, radius 1 would give only 5
        var mesh = Build("cylinder(h = 1, r1 = 1, r2 = 10);").SingleMesh;

        // Assert: two rings of 30 vertices plus two cap centers
        Assert.Equal(62, mesh.VertexCount);
    }

    /// <summary>
    ///     Validates that an invalid cylinder is reported and builds nothing.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="problem">The expected part of the warning.</param>
    [Theory]
    [InlineData("cylinder(h = 0);", "h must be a positive number")]
    [InlineData("cylinder(h = -2);", "h must be a positive number")]
    [InlineData("cylinder(h = \"x\");", "h must be a positive number")]
    [InlineData("cylinder(r = -1);", "the radii and diameters must be non-negative numbers")]
    [InlineData("cylinder(r1 = \"x\");", "the radii and diameters must be non-negative numbers")]
    [InlineData("cylinder(d2 = -1);", "the radii and diameters must be non-negative numbers")]
    [InlineData("cylinder(r1 = 0, r2 = 0);", "at least one radius must be greater than zero")]
    [InlineData("cylinder(r = 0);", "at least one radius must be greater than zero")]
    public void ScadPrimitives_Cylinder_InvalidArguments_WarnAndBuildNothing(string source, string problem)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Equal([$"1:1: cylinder: {problem}; the instance is ignored"], result.WarningTexts);
    }

    /// <summary>
    ///     Validates that a primitive built under a user module and a loop keeps its own location and fragments.
    /// </summary>
    [Fact]
    public void ScadPrimitives_Cube_InLoopAndModule_BuildsEveryInstance()
    {
        // Arrange / Act
        var result = Build("module m(s) cube(s);\nfor (i = [1:3]) m(i);");

        // Assert
        Assert.Equal(3, result.Meshes.Count);
        Assert.Equal(
            [1.0, 8.0, 27.0],
            result.Meshes.Select(m => m.Mesh.SignedVolume).ToArray());
    }
}
