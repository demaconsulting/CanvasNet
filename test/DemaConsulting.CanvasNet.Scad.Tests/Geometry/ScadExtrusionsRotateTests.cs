// cspell:ignore extrude Pappus
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <c>rotate_extrude</c> (<see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadExtrusions"/>):
///     angles and directions, the segment rule, profiles on either side of the axis, the axis crossing and
///     contact reports, hollow profiles and the distribution over 2D booleans, checked against the exact closed
///     form of the faceted volume (<c>n sin(2 pi / n) times the first moment of the profile</c>, which approaches
///     the Pappus volume).
/// </summary>
public class ScadExtrusionsRotateTests
{
    /// <summary>
    ///     Computes the exact volume of a faceted revolution from the first moment of the profile about the axis.
    /// </summary>
    /// <param name="moment">The integral of the radius over the profile area.</param>
    /// <param name="angleDegrees">The sweep angle in degrees.</param>
    /// <param name="segments">The number of segments.</param>
    /// <returns>The volume.</returns>
    private static double Revolved(double moment, double angleDegrees, int segments) =>
        segments * Math.Sin(angleDegrees * Math.PI / 180.0 / segments) * moment;

    /// <summary>
    ///     Validates the exact volume, closedness and bounds of a full revolution of a rectangle (a ring tube).
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_FullRevolution_GivesTheFacetedPappusVolume()
    {
        // Arrange / Act: a rectangle [2, 3] x [0, 5] has the radius moment (9 - 4) / 2 * 5 = 12.5
        var result = Build("rotate_extrude($fn = 64) translate([2, 0]) square([1, 5]);");

        // Assert
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, Revolved(12.5, 360, 64));
        AssertBounds(mesh.Bounds, new Vector3D(-3, -3, 0), new Vector3D(3, 3, 5));
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates the default segment count: the fragment rule of the largest radius (3 gives 10 fragments), with
    ///     no duplicated seam of a full revolution.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_DefaultSegments_FollowTheFragmentRule()
    {
        // Arrange / Act
        var result = Build("rotate_extrude() translate([2, 0]) square(1);");

        // Assert: four profile vertices on each of 10 rings
        Assert.Equal(40, result.WorldMesh.VertexCount);
    }

    /// <summary>
    ///     Validates a partial sweep: its segment count, its flat end caps (a closed solid), exact volume and bounds.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_PartialSweep_IsAClosedWedge()
    {
        // Arrange / Act: 90 degrees of 12 fragments are 3 segments, so there are 4 rings
        var result = Build("rotate_extrude(angle = 90, $fn = 12) translate([2, 0]) square([1, 5]);");

        // Assert
        var mesh = result.WorldMesh;
        Assert.Equal(16, mesh.VertexCount);
        AssertSolidVolume(mesh, Revolved(12.5, 90, 3));
        AssertBounds(mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(3, 3, 5));
    }

    /// <summary>
    ///     Validates that a negative angle sweeps clockwise (into negative Y) with the same volume, and that an
    ///     angle beyond a full turn is a full turn.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_NegativeAndOversizedAngles_ClockwiseAndClamped()
    {
        // Arrange / Act
        var positive = Build("rotate_extrude(angle = 90, $fn = 16) translate([2, 0]) square([1, 5]);");
        var negative = Build("rotate_extrude(angle = -90, $fn = 16) translate([2, 0]) square([1, 5]);");
        var full = Build("rotate_extrude(angle = 360, $fn = 16) translate([2, 0]) square([1, 5]);");
        var larger = Build("rotate_extrude(angle = 720, $fn = 16) translate([2, 0]) square([1, 5]);");

        // Assert
        AssertBounds(negative.WorldMesh.Bounds, new Vector3D(0, -3, 0), new Vector3D(3, 0, 5));
        AssertSolidVolume(negative.WorldMesh, positive.WorldMesh.SignedVolume);
        Assert.Equal(full.WorldMesh.VertexCount, larger.WorldMesh.VertexCount);
        AssertSolidVolume(larger.WorldMesh, full.WorldMesh.SignedVolume);
        AssertSolidVolume(full.WorldMesh, Revolved(12.5, 360, 16));
    }

    /// <summary>
    ///     Validates that an angle of zero and a non-number each warn at the instance and remove it.
    /// </summary>
    /// <param name="angle">The angle argument.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("\"x\"")]
    [InlineData("1 / 0")]
    [InlineData("[90]")]
    public void ScadExtrusionsRotate_InvalidAngle_WarnsAndRemovesInstance(string angle)
    {
        // Arrange / Act
        var result = Build($"\n  rotate_extrude(angle = {angle}) translate([1, 0]) square(1);");

        // Assert
        Assert.Empty(result.Meshes);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), warning.Location);
        Assert.Contains("rotate_extrude: angle", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a profile entirely on the negative side is mirrored to the positive side, so it gives the
    ///     same solid as the mirrored profile, also for a partial sweep.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_NegativeSideProfile_IsMirroredToTheSweepStart()
    {
        // Arrange / Act
        var negative = Build("rotate_extrude(angle = 90, $fn = 8) translate([-4, 0]) square([1, 2]);");
        var positive = Build("rotate_extrude(angle = 90, $fn = 8) translate([3, 0]) square([1, 2]);");

        // Assert
        AssertBounds(negative.WorldMesh.Bounds, positive.WorldMesh.Bounds.Min, positive.WorldMesh.Bounds.Max, 1e-9);
        AssertBounds(negative.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(4, 4, 2), 1e-9);
        AssertSolidVolume(negative.WorldMesh, positive.WorldMesh.SignedVolume);
        Assert.Empty(negative.Warnings);
        Assert.Empty(negative.Unsupported);
    }

    /// <summary>
    ///     Validates that a profile on both sides of the axis is reported at the extrusion and omitted: a single
    ///     shape that crosses, separate shapes on both sides, and a difference operand that crosses.
    /// </summary>
    /// <param name="profile">The 2D children.</param>
    [Theory]
    [InlineData("translate([-1, 0]) square(2);")]
    [InlineData("{ translate([1, 0]) square(1); translate([-3, 0]) square(1); }")]
    [InlineData("difference() { translate([-1, 0]) square([4, 4]); translate([1, 1]) square(1); }")]
    [InlineData("circle(2);")]
    public void ScadExtrusionsRotate_AxisCrossing_IsReportedAndOmitted(string profile)
    {
        // Arrange / Act
        var result = Build($"\n  rotate_extrude() {profile}");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-rotate-extrude-axis-crossing", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that touching the axis at one vertex only is reported at the extrusion and omitted, while a
    ///     vertex that ends an edge along the axis (a pole) is fine.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_AxisContact_IsReportedAndPolesAreFine()
    {
        // Arrange / Act
        var contact = Build("\n  rotate_extrude() polygon([[0, 0], [2, -1], [2, 1]]);");
        var cone = Build("rotate_extrude($fn = 32) polygon([[0, 0], [2, 0], [0, 3]]);");
        var cylinder = Build("rotate_extrude($fn = 32) square([2, 3]);");

        // Assert
        Assert.Empty(contact.Meshes);
        var report = Assert.Single(contact.Unsupported);
        Assert.Equal("scad-rotate-extrude-axis-contact", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        AssertSolidVolume(cone.WorldMesh, Revolved(2.0, 360, 32));
        AssertSolidVolume(cylinder.WorldMesh, Revolved(6.0, 360, 32));
        Assert.Empty(cone.Unsupported);
        Assert.Empty(cylinder.Unsupported);
    }

    /// <summary>
    ///     Validates that a profile with a hole gives a hollow solid (outer shell minus cavity) that is closed.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_ProfileWithHole_IsHollow()
    {
        // Arrange / Act: moments (16 - 1) / 2 * 4 = 30 for the outline and (9 - 4) / 2 * 2 = 5 for the hole
        var result = Build("rotate_extrude($fn = 16) polygon([[1, 0], [4, 0], [4, 4], [1, 4], [2, 1], [3, 1], [3, 3], [2, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);");

        // Assert
        AssertSolidVolume(result.WorldMesh, Revolved(25.0, 360, 16));
    }

    /// <summary>
    ///     Validates a torus: the exact faceted volume, and that it is within one percent of the Pappus volume.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_Torus_MatchesPappus()
    {
        // Arrange / Act
        var result = Build("rotate_extrude($fn = 64) translate([5, 0]) circle(1, $fn = 32);");

        // Assert: the area of the 32-gon times its centroid radius 5 is the moment
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, Revolved(RegularPolygonArea(1, 32) * 5.0, 360, 64));
        Assert.Equal(2.0 * Math.PI * Math.PI * 5.0, mesh.SignedVolume, 2.0 * Math.PI * Math.PI * 5.0 * 0.01);
    }

    /// <summary>
    ///     Validates that the steps never exceed the renderer's limit: every angle and fragment count builds a closed
    ///     solid without any report.
    /// </summary>
    /// <param name="angle">The angle.</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(30.0)]
    [InlineData(90.0)]
    [InlineData(119.0)]
    [InlineData(120.0)]
    [InlineData(121.0)]
    [InlineData(180.0)]
    [InlineData(240.0)]
    [InlineData(241.0)]
    [InlineData(300.0)]
    [InlineData(359.0)]
    [InlineData(360.0)]
    [InlineData(361.0)]
    [InlineData(-200.0)]
    public void ScadExtrusionsRotate_AnyAngleAndFragmentCount_BuildsAClosedSolid(double angle)
    {
        // Arrange / Act / Assert
        foreach (var fragments in new[] { 3, 4, 5, 7, 24 })
        {
            var result = Build($"rotate_extrude(angle = {angle}, $fn = {fragments}) translate([2, 0]) square([1, 1]);");
            var mesh = result.WorldMesh;
            Assert.True(mesh.IsClosed, $"angle {angle} fragments {fragments}");
            Assert.True(mesh.SignedVolume > 0.0, $"angle {angle} fragments {fragments}");
            Assert.Empty(result.Unsupported);
            Assert.Empty(result.Warnings);
        }
    }

    /// <summary>
    ///     Validates the distribution over a 2D difference: a difference of two revolved solids with identical
    ///     segment counts and exact volumes.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_OverFlatDifference_IsADifferenceOfSolidsWithSharedSegments()
    {
        // Arrange / Act: moments (25 - 4) / 2 * 3 = 31.5 and (16 - 9) / 2 * 1 = 3.5
        var result = Build("rotate_extrude($fn = 16) difference() { translate([2, 0]) square([3, 3]); translate([3, 1]) square(1); }");

        // Assert
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        Assert.Equal(CsgOperation.Difference, csg.Operation);
        var leaves = result.Leaves;
        AssertSolidVolume(leaves[0].WorldMesh, Revolved(31.5, 360, 16));
        AssertSolidVolume(leaves[1].WorldMesh, Revolved(3.5, 360, 16));
        Assert.Equal(leaves[0].Node.Mesh.VertexCount, leaves[1].Node.Mesh.VertexCount);
    }

    /// <summary>
    ///     Validates that all leaves of one extrusion use the segment count of the largest radius.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_SeveralLeaves_ShareTheSegmentCountOfTheLargestRadius()
    {
        // Arrange / Act: the near leaf alone would get 5 fragments, the far one (radius 11) the angle-limited 30
        var result = Build("rotate_extrude() { translate([0.5, 0]) square(0.1); translate([10, 0]) square(1); }");

        // Assert
        var counts = result.Leaves.Select(l => l.Node.Mesh.VertexCount).Distinct().ToList();
        Assert.Single(counts);
        Assert.Equal(4 * 30, counts[0]);
    }

    /// <summary>
    ///     Validates profile transforms, colors and 3D transforms around the extrusion.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_TransformsAndColors_Compose()
    {
        // Arrange / Act
        var result = Build("translate([0, 0, 5]) color(\"red\") rotate_extrude($fn = 8) rotate(90) translate([0, -4]) square([2, 3]);");

        // Assert: the profile [0,2]x[-4,-1] turned by 90 degrees is x in [1, 4], y in [0, 2]
        var leaf = Assert.Single(result.Leaves);
        Assert.Equal(new Rgba32(255, 0, 0, 255), leaf.Appearance!.Color);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(-4, -4, 5), new Vector3D(4, 4, 7));
        AssertSolidVolume(leaf.WorldMesh, Revolved(((16.0 - 1.0) / 2.0) * 2.0, 360, 8));
    }

    /// <summary>
    ///     Validates the child dimension rules: 3D children are ignored with a warning, no children is silent.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_ChildDimensions_AreHandled()
    {
        // Arrange / Act
        var solid = Build("rotate_extrude() translate([1, 0, 0]) cube(1);");
        var none = Build("rotate_extrude() { }");

        // Assert
        Assert.Empty(solid.Meshes);
        Assert.Contains(solid.Warnings, w => w.Message.Contains("only 2D children", StringComparison.Ordinal));
        Assert.Empty(none.Meshes);
        Assert.Empty(none.Warnings);
    }

    /// <summary>
    ///     Validates that a hostile fragment count fails before allocating with the triangle limit and the location.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_HostileFragmentCount_ExceedsTheTriangleLimit()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("\n  rotate_extrude($fn = 1e9) translate([1, 0]) square(1);"));

        // Assert
        Assert.Contains("Triangle limit", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
    }

    /// <summary>
    ///     Validates that <c>convexity</c> is accepted and ignored, and an unknown argument warns.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_ConvexityAndUnknownArguments_AreHandled()
    {
        // Arrange / Act
        var convex = Build("rotate_extrude(360, 10, $fn = 8) translate([1, 0]) square(1);");
        var unknown = Build("rotate_extrude(start = 90, $fn = 8) translate([1, 0]) square(1);");

        // Assert
        Assert.Empty(convex.Warnings);
        Assert.Contains(unknown.Warnings, w => w.Message.Contains("unknown argument 'start'", StringComparison.Ordinal));
        Assert.Single(unknown.Meshes);
    }

    /// <summary>
    ///     Validates strict mode for an axis crossing.
    /// </summary>
    [Fact]
    public void ScadExtrusionsRotate_StrictMode_ThrowsForAxisCrossing()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build("cube(1);\n  rotate_extrude() translate([-1, 0]) square(2);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-rotate-extrude-axis-crossing", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
    }
}
