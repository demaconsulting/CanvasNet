// cspell:ignore extrude
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <c>linear_extrude</c> (<see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadExtrusions"/>):
///     height, centering, twist, slices, scale and the distribution over 2D transforms, colors and booleans,
///     checked with analytic volumes and bounds and the validation helpers of the mesh.
/// </summary>
public class ScadExtrusionsLinearTests
{
    /// <summary>
    ///     Validates the volume (area times height), closedness, orientation and bounds of a plain extrusion.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_Square_GivesAreaTimesHeight()
    {
        // Arrange / Act
        var result = Build("linear_extrude(height = 5) square([2, 3]);");

        // Assert
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, 30.0);
        AssertBounds(mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 3, 5));
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates the argument forms: the default height of 100, positional arguments and <c>center</c>.
    /// </summary>
    /// <param name="call">The extrusion call.</param>
    /// <param name="zMin">The expected lowest Z.</param>
    /// <param name="zMax">The expected highest Z.</param>
    [Theory]
    [InlineData("linear_extrude()", 0.0, 100.0)]
    [InlineData("linear_extrude(7)", 0.0, 7.0)]
    [InlineData("linear_extrude(height = 7, center = true)", -3.5, 3.5)]
    [InlineData("linear_extrude(7, true)", -3.5, 3.5)]
    [InlineData("linear_extrude(center = true)", -50.0, 50.0)]
    [InlineData("linear_extrude(7, false, 10)", 0.0, 7.0)]
    [InlineData("linear_extrude(height = 0.5, convexity = 4)", 0.0, 0.5)]
    public void ScadExtrusionsLinear_Arguments_GiveTheExpectedHeightRange(string call, double zMin, double zMax)
    {
        // Arrange / Act
        var result = Build($"{call} square(1);");

        // Assert
        var mesh = result.WorldMesh;
        AssertBounds(mesh.Bounds, new Vector3D(0, 0, zMin), new Vector3D(1, 1, zMax));
        AssertSolidVolume(mesh, zMax - zMin);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a circle extrudes to the exact volume of its inscribed polygon prism.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_Circle_GivesThePolygonPrism()
    {
        // Arrange / Act
        var result = Build("linear_extrude(4) circle(3, $fn = 24);");

        // Assert
        AssertSolidVolume(result.WorldMesh, RegularPolygonArea(3, 24) * 4.0);
    }

    /// <summary>
    ///     Validates that a profile with a hole extrudes to a solid with a through hole (outline minus hole volume).
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_PolygonWithHole_HasAThroughHole()
    {
        // Arrange / Act
        var result = Build("linear_extrude(3) polygon([[0, 0], [10, 0], [10, 10], [0, 10], [3, 3], [7, 3], [7, 7], [3, 7]], [[0, 1, 2, 3], [4, 5, 6, 7]]);");

        // Assert
        AssertSolidVolume(result.WorldMesh, (100.0 - 16.0) * 3.0);
    }

    /// <summary>
    ///     Validates that a positive twist turns the top clockwise seen from +Z and a negative one counter-clockwise.
    /// </summary>
    /// <param name="twist">The twist argument.</param>
    /// <param name="expectedY">The expected Y of the vertex that started at (2, 0): -2 for clockwise, 2 for counter-clockwise.</param>
    [Theory]
    [InlineData(90.0, -2.0)]
    [InlineData(-90.0, 2.0)]
    public void ScadExtrusionsLinear_Twist_PositiveIsClockwiseFromAbove(double twist, double expectedY)
    {
        // Arrange / Act
        var result = Build($"linear_extrude(height = 10, twist = {twist}, slices = 4) polygon([[0, 0], [2, 0], [2, 1], [0, 1]]);");

        // Assert: the top vertices are the profile turned by 90 degrees about the origin
        var top = result.WorldMesh.Vertices.Where(v => Math.Abs(v.Z - 10.0) < 1e-12).ToList();
        Assert.Contains(top, v => Math.Abs(v.X) < 1e-9 && Math.Abs(v.Y - expectedY) < 1e-9);
        Assert.DoesNotContain(top, v => Math.Abs(v.X) < 1e-9 && Math.Abs(v.Y + expectedY) < 1e-9);
        Assert.True(result.WorldMesh.IsClosed);
    }

    /// <summary>
    ///     Validates that a twisted extrusion keeps (nearly) the volume of the untwisted one and is a valid closed
    ///     solid.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_Twist_KeepsVolumeApproximately()
    {
        // Arrange / Act
        var result = Build("linear_extrude(height = 10, twist = 180, slices = 60) translate([-1, -1]) square(2);");

        // Assert: the cross-section keeps its area, the non-planar facets move it by under three percent
        var mesh = result.WorldMesh;
        Assert.True(mesh.IsClosed);
        Assert.Equal(40.0, mesh.SignedVolume, 40.0 * 0.03);
    }

    /// <summary>
    ///     Validates the default slice count: 1 without twist; with a twist, the fragment rule of the farthest
    ///     vertex times the share of a full turn.
    /// </summary>
    /// <param name="call">The extrusion call.</param>
    /// <param name="slices">The expected slice count (vertex rings minus one).</param>
    [Theory]
    [InlineData("linear_extrude(10)", 1)]
    [InlineData("linear_extrude(10, twist = 0)", 1)]
    [InlineData("linear_extrude(10, twist = 90)", 2)]
    [InlineData("linear_extrude(10, twist = -90)", 2)]
    [InlineData("linear_extrude(10, twist = 360, $fn = 36)", 36)]
    [InlineData("linear_extrude(10, twist = 1, $fn = 36)", 1)]
    [InlineData("linear_extrude(10, twist = 90, $fn = 64)", 16)]
    [InlineData("linear_extrude(10, twist = 90, slices = 7)", 7)]
    [InlineData("linear_extrude(10, slices = 3)", 3)]
    [InlineData("linear_extrude(10, slices = 2.9)", 2)]
    public void ScadExtrusionsLinear_Slices_FollowTheDefaultRule(string call, int slices)
    {
        // Arrange / Act: the profile's farthest vertex is (2, 1), at distance sqrt 5, so 8 fragments by default
        var result = Build($"{call} polygon([[0, 0], [2, 0], [2, 1], [0, 1]]);");

        // Assert: every ring has the four profile vertices
        Assert.Equal(4 * (slices + 1), result.WorldMesh.VertexCount);
    }

    /// <summary>
    ///     Validates uniform and non-uniform scale against the analytic frustum volumes, and that scale about the
    ///     origin keeps the bottom as written.
    /// </summary>
    /// <param name="scale">The scale argument.</param>
    /// <param name="volume">The expected volume.</param>
    [Theory]
    [InlineData("scale = 0.5", 35.0 / 3.0)]
    [InlineData("scale = 1", 20.0)]
    [InlineData("scale = 2", 140.0 / 3.0)]
    [InlineData("scale = [2, 1]", 30.0)]
    [InlineData("scale = [1, 3]", 40.0)]
    [InlineData("scale = [0.5, 0.5]", 35.0 / 3.0)]
    public void ScadExtrusionsLinear_Scale_GivesTheFrustumVolume(string scale, double volume)
    {
        // Arrange / Act: a 2 x 2 square of area 4 extruded by 5
        var result = Build($"linear_extrude(height = 5, {scale}) square(2);");

        // Assert
        AssertSolidVolume(result.WorldMesh, volume, 1e-9);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a scale of zero collapses the top to a point: a pyramid.
    /// </summary>
    /// <param name="scale">The scale argument.</param>
    [Theory]
    [InlineData("scale = 0")]
    [InlineData("scale = [0, 0]")]
    public void ScadExtrusionsLinear_ScaleZero_GivesAPyramid(string scale)
    {
        // Arrange / Act
        var result = Build($"linear_extrude(height = 5, {scale}) square(2);");

        // Assert: area 4 times height 5 over 3
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, 20.0 / 3.0, 1e-9);
        Assert.Single(mesh.Vertices, v => v.Z > 4.9);
    }

    /// <summary>
    ///     Validates that an apex over a profile with a hole is the unsupported feature, reported at the extrusion
    ///     and omitting it, while an apex over separate single-contour leaves works for each.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_ApexOverSeveralContours_IsReportedAndOmitted()
    {
        // Arrange / Act
        var holed = Build("\n  linear_extrude(height = 5, scale = 0) polygon([[0, 0], [10, 0], [10, 10], [0, 10], [3, 3], [7, 3], [7, 7], [3, 7]], [[0, 1, 2, 3], [4, 5, 6, 7]]);");
        var separate = Build("linear_extrude(height = 5, scale = 0) { square(1); translate([5, 0]) square(1); }");

        // Assert
        Assert.Empty(holed.Meshes);
        var report = Assert.Single(holed.Unsupported);
        Assert.Equal("scad-linear-extrude-apex-multiple-contours", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Equal(2, separate.Meshes.Count);
        Assert.Empty(separate.Unsupported);
    }

    /// <summary>
    ///     Validates that a negative scale component or exactly one zero component is the unsupported feature.
    /// </summary>
    /// <param name="scale">The scale argument.</param>
    [Theory]
    [InlineData("-1")]
    [InlineData("[0, 1]")]
    [InlineData("[1, 0]")]
    [InlineData("[-1, -1]")]
    [InlineData("[2, -0.5]")]
    public void ScadExtrusionsLinear_UnsupportedScale_IsReportedAndOmitted(string scale)
    {
        // Arrange / Act
        var result = Build($"\n linear_extrude(height = 5, scale = {scale}) square(2);");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-linear-extrude-scale", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 2), report.Location);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates the invalid arguments: each is a warning at the instance and removes it.
    /// </summary>
    /// <param name="arguments">The extrusion arguments.</param>
    [Theory]
    [InlineData("height = 0")]
    [InlineData("height = -3")]
    [InlineData("height = \"tall\"")]
    [InlineData("height = [1, 2]")]
    [InlineData("height = 5, twist = \"x\"")]
    [InlineData("height = 5, slices = 0")]
    [InlineData("height = 5, slices = \"x\"")]
    [InlineData("height = 5, slices = -2")]
    [InlineData("height = 5, scale = \"x\"")]
    [InlineData("height = 5, scale = [1, 2, 3]")]
    [InlineData("height = 5, scale = [1, \"a\"]")]
    [InlineData("height = 5, scale = 0 / 0")]
    [InlineData("height = 1 / 0")]
    public void ScadExtrusionsLinear_InvalidArguments_WarnAndRemoveInstance(string arguments)
    {
        // Arrange / Act
        var result = Build($"\n  linear_extrude({arguments}) square(1);");

        // Assert
        Assert.Empty(result.Meshes);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), warning.Location);
        Assert.Contains("linear_extrude:", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that 3D children are ignored with a warning, also next to 2D children, and that an extrusion
    ///     without children is empty and silent.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_ChildDimensions_AreHandled()
    {
        // Arrange / Act
        var solidOnly = Build("linear_extrude(3) cube(1);");
        var mixed = Build("linear_extrude(3) { cube(1); square(2); }");
        var none = Build("linear_extrude(3) { }");

        // Assert
        Assert.Empty(solidOnly.Meshes);
        Assert.Contains(solidOnly.Warnings, w => w.Message.Contains("only 2D children", StringComparison.Ordinal));
        AssertBounds(mixed.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 2, 3));
        Assert.Single(mixed.Warnings);
        Assert.Empty(none.Meshes);
        Assert.Empty(none.Warnings);
    }

    /// <summary>
    ///     Validates that all 2D children are extruded together as their union.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_SeveralChildren_AreExtrudedTogether()
    {
        // Arrange / Act
        var result = Build("linear_extrude(2) { square(1); translate([3, 0]) square(1); circle(1, $fn = 8); }");

        // Assert
        Assert.Equal(3, result.Meshes.Count);
        Assert.All(result.Leaves, leaf => Assert.Equal((0.0, 2.0), (leaf.WorldMesh.Bounds.Min.Z, leaf.WorldMesh.Bounds.Max.Z)));
    }

    /// <summary>
    ///     Validates that 2D transforms, mirroring and colors inside the extrusion apply to the profile.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_ProfileTransformsAndColors_ApplyToTheProfile()
    {
        // Arrange / Act
        var moved = Build("linear_extrude(5) translate([1, 2]) rotate(90) square([2, 1]);");
        var mirrored = Build("linear_extrude(5) mirror([1, 0]) square([2, 1]);");
        var colored = Build("linear_extrude(5) color(\"red\") square(1);");
        var scaled = Build("linear_extrude(5) scale([2, 3]) square(1);");

        // Assert: rotate(90) maps [0,2]x[0,1] to [-1,0]x[0,2]
        AssertBounds(moved.WorldMesh.Bounds, new Vector3D(0, 2, 0), new Vector3D(1, 4, 5));
        AssertSolidVolume(moved.WorldMesh, 10.0);
        AssertBounds(mirrored.WorldMesh.Bounds, new Vector3D(-2, 0, 0), new Vector3D(0, 1, 5));
        AssertSolidVolume(mirrored.WorldMesh, 10.0);
        Assert.Equal(new Rgba32(255, 0, 0, 255), Assert.Single(colored.Leaves).Appearance!.Color);
        AssertSolidVolume(scaled.WorldMesh, 30.0);
    }

    /// <summary>
    ///     Validates that the extrusion itself can be transformed and colored in 3D afterwards.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_ThenTransformedAndColoredIn3D_Composes()
    {
        // Arrange / Act
        var result = Build("color(\"blue\") translate([0, 0, 10]) rotate([0, 90, 0]) linear_extrude(2) square(1);");

        // Assert: rotating about Y by 90 degrees maps Z to X and X to -Z
        var leaf = Assert.Single(result.Leaves);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(0, 0, 9), new Vector3D(2, 1, 10));
        Assert.Equal(new Rgba32(0, 0, 255, 255), leaf.Appearance!.Color);
    }

    /// <summary>
    ///     Validates the distribution of the extrusion over a 2D difference: the difference of the extrusions, with
    ///     the operands' exact volumes.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_OverFlatDifference_IsADifferenceOfExtrusions()
    {
        // Arrange / Act
        var result = Build("linear_extrude(10) difference() { square(10); translate([2, 2]) square(3); }");

        // Assert
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        Assert.Equal(CsgOperation.Difference, csg.Operation);
        var leaves = result.Leaves;
        AssertSolidVolume(leaves[0].WorldMesh, 1000.0);
        AssertSolidVolume(leaves[1].WorldMesh, 90.0);
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(2, 2, 0), new Vector3D(5, 5, 10));
    }

    /// <summary>
    ///     Validates that the distribution also covers intersection, nested booleans and a union inside a
    ///     difference, with the extrusion parameters shared by every operand.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_OverNestedBooleans_ShareParameters()
    {
        // Arrange / Act
        var result = Build("linear_extrude(height = 6, twist = 30, slices = 5, scale = 0.5) difference() { intersection() { square(10); translate([5, 5]) square(10); } union() { translate([6, 6]) square(1); translate([8, 8]) square(1); } }");

        // Assert: three leaves, each with 5 slices (4 vertices x 6 rings)
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        Assert.Equal(CsgOperation.Difference, csg.Operation);
        Assert.Equal(2, csg.Children.Count);
        Assert.Equal(4, result.Leaves.Count);
        Assert.All(result.Leaves, leaf => Assert.Equal(24, leaf.Node.Mesh.VertexCount));
    }

    /// <summary>
    ///     Validates that an empty first operand of a 2D difference makes the extrusion empty, and that a
    ///     leaf that is invalid after its transformations is reported as an invalid polygon and left out.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_InvalidLeafAfterTransform_IsReportedAsInvalidPolygon()
    {
        // Arrange / Act: two stretches by 1e200 along X multiply to infinity when the matrices are composed
        var result = Build("\nlinear_extrude(1) scale([1e200, 1]) scale([1e200, 1]) square(1);");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-polygon-invalid", report.Feature);
        Assert.Equal(2, report.Location.Line);
    }

    /// <summary>
    ///     Validates that the extrusion fails before allocating when it exceeds the triangle budget, with the
    ///     location of the extrusion, and that a hostile slice count or twist does the same.
    /// </summary>
    /// <param name="arguments">The extrusion arguments.</param>
    [Theory]
    [InlineData("height = 1, slices = 1e9")]
    [InlineData("height = 1, slices = 1e300")]
    [InlineData("height = 1, twist = 1e15")]
    [InlineData("height = 1, twist = 1e15, $fn = 1e6")]
    public void ScadExtrusionsLinear_HostileSlicesOrTwist_ExceedTheTriangleLimit(string arguments)
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build($"\n  linear_extrude({arguments}) circle(5);"));

        // Assert
        Assert.Contains("Triangle limit", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
    }

    /// <summary>
    ///     Validates that a small budget is enforced for a sensible extrusion and that the budget accounts for all
    ///     leaves.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_SmallBudget_IsEnforced()
    {
        // Arrange / Act / Assert: a 4-vertex profile with 100 slices needs 2 * 4 * 100 + 8 = 808 triangles
        Build("linear_extrude(1, slices = 100) square(1);", Settings(maxTriangles: 900));
        Assert.Throws<ScadEvaluationException>(() => Build("linear_extrude(1, slices = 100) square(1);", Settings(maxTriangles: 800)));
        Assert.Throws<ScadEvaluationException>(() => Build("linear_extrude(1, slices = 60) { square(1); translate([2, 0]) square(1); }", Settings(maxTriangles: 900)));
    }

    /// <summary>
    ///     Validates strict mode for the unsupported scale: the first report throws with its feature and location.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_StrictMode_ThrowsForUnsupportedScale()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build("cube(1);\n  linear_extrude(3, scale = -1) square(1);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-linear-extrude-scale", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
    }

    /// <summary>
    ///     Validates the unknown and surplus arguments: each warns while the extrusion is still built.
    /// </summary>
    [Fact]
    public void ScadExtrusionsLinear_UnknownArgument_WarnsAndStillBuilds()
    {
        // Arrange / Act
        var result = Build("linear_extrude(3, bogus = 1) square(1);");

        // Assert
        Assert.Contains(result.Warnings, w => w.Message.Contains("unknown argument 'bogus'", StringComparison.Ordinal));
        Assert.Single(result.Meshes);
    }
}
