// cspell:ignore minkowski Minkowski polyhedron
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the <c>minkowski</c> module of <see cref="ScadGeometryBuilder"/> (<c>ScadHulls</c>): the sum of convex
///     children, its distribution over unions, and the reports for operands that are not convex, that are 2D or of
///     mixed dimension, or that need more work than the documented bounds.
/// </summary>
public class ScadGeometryBuilderMinkowskiTests
{
    /// <summary>
    ///     Validates that a block summed with a ball is the rounded block: a closed mesh whose bounds are the block's
    ///     grown by the ball's extent and whose volume lies between the block with its faces added and the exact
    ///     rounded block.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_CubeAndSphere_IsTheRoundedBox()
    {
        // Arrange: the ball of 16 fragments has vertices on every axis, so it reaches its full radius
        const double reach = 1.0;

        // Act
        var result = Build("minkowski() { cube([10, 6, 4]); sphere(r = 1, $fn = 16); }");

        // Assert: the volume of the exact rounded block is 240 + 248 + 20 * pi + 4 / 3 * pi
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        AssertBounds(mesh.Bounds, new Vector3D(-reach, -reach, -reach), new Vector3D(10 + reach, 6 + reach, 4 + reach), 1e-9);
        Assert.InRange(mesh.SignedVolume, 240.0 + 248.0, 240.0 + 248.0 + (20.0 * Math.PI) + (4.0 / 3.0 * Math.PI));
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the children are summed in their transformed positions: the sum of two boxes is a box whose
    ///     size is the sum of their sizes.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TransformedConvexChildren_AreSummed()
    {
        // Arrange / Act: a box 2 x 2 x 2 at x 10 and a box 2 x 1 x 3 turned a quarter turn (x -2..0, y 1..2)
        var result = Build("minkowski() { translate([10, 0, 0]) cube(2); rotate([0, 0, 90]) translate([1, 0, 0]) cube([1, 2, 3]); }");

        // Assert: 4 x 3 x 5
        var mesh = result.SingleMesh;
        AssertSolidVolume(mesh, 60.0);
        AssertBounds(mesh.Bounds, new Vector3D(8, 1, 0), new Vector3D(12, 4, 5));
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the sum distributes over a union: a union of two cubes summed with a cube is the union of
    ///     two boxes, and a union on each side gives the four combinations.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_UnionOfConvexChildren_Distributes()
    {
        // Arrange / Act
        var one = Build("minkowski() { union() { cube(1); translate([4, 0, 0]) cube(1); } cube(2); }");
        var other = Build("minkowski() { cube(2); for (i = [0 : 1]) translate([i * 4, 0, 0]) cube(1); }");
        var four = Build("minkowski() { union() { cube(1); translate([10, 0, 0]) cube(1); } union() { cube(1); translate([0, 10, 0]) cube(1); } }");

        // Assert: two 3 x 3 x 3 blocks, at x 0..3 and x 4..7, whichever side carries the union
        Assert.Equal(2, one.Meshes.Count);
        AssertSolidVolume(one.Meshes[0].Mesh, 27.0);
        AssertSolidVolume(one.Meshes[1].Mesh, 27.0);
        AssertBounds(one.Meshes[0].Mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(3, 3, 3));
        AssertBounds(one.Meshes[1].Mesh.Bounds, new Vector3D(4, 0, 0), new Vector3D(7, 3, 3));
        Assert.Equal(2, other.Meshes.Count);
        AssertBounds(other.Meshes[1].Mesh.Bounds, new Vector3D(4, 0, 0), new Vector3D(7, 3, 3));
        Assert.Equal(4, four.Meshes.Count);
        Assert.All(four.Meshes, m => AssertSolidVolume(m.Mesh, 8.0));
        Assert.Empty(one.Features);
        Assert.Empty(four.Features);
    }

    /// <summary>
    ///     Validates that the <c>convexity</c> argument, named or positional, changes nothing and is not validated.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_ConvexityArgumentIsIgnored()
    {
        // Arrange / Act
        var plain = Build("minkowski() { cube(1); cube(2); }");
        var named = Build("minkowski(convexity = 10) { cube(1); cube(2); }");
        var positional = Build("minkowski(\"anything\") { cube(1); cube(2); }");

        // Assert
        Assert.Equal(plain.SingleMesh.Vertices, named.SingleMesh.Vertices);
        Assert.Equal(plain.SingleMesh.TriangleIndices, positional.SingleMesh.TriangleIndices);
        Assert.Empty(named.Warnings);
        Assert.Empty(positional.Features);
    }

    /// <summary>
    ///     Validates that the output of a hull is a convex operand of a sum, and that a lone convex child is itself.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_HullChild_IsConvex()
    {
        // Arrange / Act
        var hexagon = Build("minkowski() { hull() { cube(2); translate([6, 6, 0]) cube(2); } cube(1); }");
        var capsule = Build("minkowski() { hull() { sphere(1, $fn = 12); translate([5, 0, 0]) sphere(1, $fn = 12); } cube(1); }");
        var lone = Build("minkowski() cube(2);");

        // Assert
        AssertBounds(hexagon.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(9, 9, 3));
        Assert.True(hexagon.SingleMesh.IsClosed);
        Assert.True(capsule.SingleMesh.IsClosed);
        Assert.Empty(hexagon.Features);
        Assert.Empty(capsule.Features);

        // A lone child is the child (no new mesh, nothing charged beyond the cube)
        AssertSolidVolume(lone.SingleMesh, 8.0);
        Assert.Equal(12, lone.TrianglesCharged);
        Assert.Empty(lone.Features);
    }

    /// <summary>
    ///     Validates that a child that is not convex, a boolean or a mesh with a notch or a hole, is reported at the
    ///     module and the module is omitted whole, also when it is the only child.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_NonConvexChild_ReportsAndOmits()
    {
        // Arrange
        const string LShape = "linear_extrude(1) polygon([[0, 0], [3, 0], [3, 1], [1, 1], [1, 3], [0, 3]])";
        string[] sources =
        [
            $"\n  minkowski() {{ {LShape}; cube(1); }}",
            "\n  minkowski() { cube(1); difference() { cube(3); translate([1, 1, 1]) cube(2); } }",
            "\n  minkowski() { rotate_extrude($fn = 16) translate([3, 0]) square(1); sphere(1, $fn = 8); }",
            $"\n  minkowski() {LShape};",
            "\n  minkowski() difference() { cube(3); cube(2); }",
            "\n  minkowski() { cube(1); intersection() { cube(3); translate([1, 1, 1]) cube(3); } }",
        ];

        // Act / Assert
        foreach (var source in sources)
        {
            var result = Build(source);
            var report = Assert.Single(result.Unsupported);
            Assert.Equal("scad-minkowski-nonconvex", report.Feature);
            Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
            Assert.Contains("convex", report.Message, StringComparison.Ordinal);
            Assert.Empty(result.Meshes);
            Assert.Empty(result.Warnings);
        }
    }

    /// <summary>
    ///     Validates that the sum of 2D children is reported as deferred and builds nothing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TwoDimensionalChildren_ReportsDeferred()
    {
        // Arrange / Act
        var pair = Build("\n  minkowski() { square(2); circle(1); }");
        var lone = Build("minkowski() square(1);");

        // Assert
        var report = Assert.Single(pair.Unsupported);
        Assert.Equal("scad-minkowski-2d", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(pair.Meshes);
        Assert.Equal(["scad-minkowski-2d"], lone.Features);
    }

    /// <summary>
    ///     Validates that a sum of 2D and 3D children is reported at the module and omitted, leaving its siblings.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_MixedDimensions_ReportsAndOmits()
    {
        // Arrange / Act
        var result = Build("cube(2);\n  minkowski() { cube(1); square(1); }");

        // Assert
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-minkowski-mixed-dimensions", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        AssertSolidVolume(result.SingleMesh, 8.0);
    }

    /// <summary>
    ///     Validates the work bounds: a child with more convex parts than the limit, a step with more combinations than
    ///     the limit and two meshes with more pairwise vertex sums than the limit are reported and omitted.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TooManyPairsOrCombinations_ReportsTooComplex()
    {
        // Arrange
        string[] sources =
        [
            "minkowski() { for (i = [0 : 4096]) translate([i * 2, 0, 0]) cube(1); cube(1); }",
            "minkowski() { for (i = [0 : 64]) translate([i * 3, 0, 0]) cube(1); for (j = [0 : 64]) translate([0, j * 3, 0]) cube(1); }",
            "minkowski() { sphere(1, $fn = 100); sphere(1, $fn = 100); }",
        ];

        // Act / Assert
        foreach (var source in sources)
        {
            var result = Build(source);
            var report = Assert.Single(result.Unsupported);
            Assert.Equal("scad-minkowski-too-complex", report.Feature);
            Assert.Empty(result.Meshes);
        }
    }

    /// <summary>
    ///     Validates that a sum that leaves the coordinate range is rejected like any other geometry that does.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_SumBeyondTheCoordinateRange_IsRejected()
    {
        // Arrange / Act: each part is within 1e100, their sum is not
        var result = Build("minkowski() { translate([6e99, 0, 0]) cube(1e99); translate([6e99, 0, 0]) cube(1e99); }");

        // Assert
        Assert.Equal(["scad-geometry-rejected"], result.Features);
        Assert.Empty(result.Meshes);
    }
}
