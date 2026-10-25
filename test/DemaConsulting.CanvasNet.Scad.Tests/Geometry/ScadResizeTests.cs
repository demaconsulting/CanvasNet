// cspell:ignore polyhedron
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <c>resize</c> (<see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadTransforms"/>): the size
///     of the bounding box of the children, <c>auto</c> proportions, 2D shapes and the approximate-bounds report.
/// </summary>
public class ScadResizeTests
{
    /// <summary>
    ///     Validates resizing every axis explicitly.
    /// </summary>
    [Fact]
    public void ScadResize_AllAxes_GivesTheRequestedSize()
    {
        // Arrange / Act
        var result = Build("resize([2, 4, 6]) cube([1, 2, 3]);");

        // Assert
        var mesh = result.WorldMesh;
        AssertBounds(mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 4, 6));
        AssertSolidVolume(mesh, 48.0);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a zero component leaves its axis alone, without and with <c>auto</c>.
    /// </summary>
    /// <param name="call">The resize call.</param>
    /// <param name="max">The expected maximum corner of the box that starts at the origin.</param>
    [Theory]
    [InlineData("resize([10, 0, 0])", new[] { 10.0, 2, 3 })]
    [InlineData("resize(newsize = [10, 0, 0], auto = false)", new[] { 10.0, 2, 3 })]
    [InlineData("resize([10, 0, 0], true)", new[] { 10.0, 20, 30 })]
    [InlineData("resize([0, 8, 0], auto = true)", new[] { 4.0, 8, 12 })]
    [InlineData("resize([0, 0, 6], auto = [true, true, false])", new[] { 2.0, 4, 6 })]
    [InlineData("resize([0, 0, 6], auto = [false, true, false])", new[] { 1.0, 4, 6 })]
    [InlineData("resize([0, 0, 6], auto = [false, false, true])", new[] { 1.0, 2, 6 })]
    [InlineData("resize([6, 0, 0], auto = [false, true])", new[] { 6.0, 12, 3 })]
    [InlineData("resize([0, 0, 0], true)", new[] { 1.0, 2, 3 })]
    [InlineData("resize([5])", new[] { 5.0, 2, 3 })]
    [InlineData("resize([5, 6])", new[] { 5.0, 6, 3 })]
    public void ScadResize_AutoAndZero_KeepOrFollowProportions(string call, double[] max)
    {
        // Arrange / Act
        var result = Build($"{call} cube([1, 2, 3]);");

        // Assert
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(max[0], max[1], max[2]));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that resize scales about the origin, so an offset child moves with the factor.
    /// </summary>
    [Fact]
    public void ScadResize_OffsetChild_ScalesAboutTheOrigin()
    {
        // Arrange / Act
        var result = Build("resize([4, 4, 4]) translate([2, 0, 0]) cube(1);");

        // Assert: the size 1 becomes 4, so the offset 2 becomes 8
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(8, 0, 0), new Vector3D(12, 4, 4));
    }

    /// <summary>
    ///     Validates that the extent of rotated children is their exact vertex extent (not the extent of a rotated
    ///     bounding box), so the result has precisely the requested size.
    /// </summary>
    [Fact]
    public void ScadResize_RotatedChild_UsesExactVertexExtent()
    {
        // Arrange / Act
        var result = Build("resize([10, 0, 0]) rotate([0, 0, 30]) cylinder(r = 1, h = 1, $fn = 6);");

        // Assert
        var bounds = result.WorldMesh.Bounds;
        Assert.Equal(10.0, bounds.Max.X - bounds.Min.X, 1e-9);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that resizing a union uses the extent of the whole union.
    /// </summary>
    [Fact]
    public void ScadResize_Union_UsesTheWholeExtent()
    {
        // Arrange / Act
        var result = Build("resize([10, 0, 0]) { cube(1); translate([3, 0, 0]) cube(1); }");

        // Assert: x spans 0..4 before, so the factor is 2.5
        AssertBounds(result.Scene.ComputeBounds(), new Vector3D(0, 0, 0), new Vector3D(10, 1, 1));
    }

    /// <summary>
    ///     Validates resizing 2D shapes (only X and Y count).
    /// </summary>
    /// <param name="call">The resize call.</param>
    /// <param name="max">The expected maximum X and Y of the slab.</param>
    [Theory]
    [InlineData("resize([4, 0])", new[] { 4.0, 2 })]
    [InlineData("resize([4, 4])", new[] { 4.0, 4 })]
    [InlineData("resize([4, 0], true)", new[] { 4.0, 8 })]
    [InlineData("resize([4, 6, 99])", new[] { 4.0, 6 })]
    public void ScadResize_Flat_ResizesInThePlane(string call, double[] max)
    {
        // Arrange / Act
        var result = Build($"{call} square([1, 2]);");

        // Assert
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(max[0], max[1], 0.1));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a plain union or transform chain is never reported as approximate.
    /// </summary>
    [Fact]
    public void ScadResize_ExactChildren_ReportNothing()
    {
        // Arrange / Act
        var result = Build("resize([4, 4, 4]) translate([1, 1, 1]) rotate(45) union() { cube(1); sphere(1); }");

        // Assert
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that resizing along an axis where the children have no extent warns and skips that axis.
    /// </summary>
    [Fact]
    public void ScadResize_AxisWithoutExtent_WarnsAndSkipsIt()
    {
        // Arrange / Act: a flat triangle (two coincident faces) has no extent along Z
        var result = Build("\nresize([2, 2, 2]) polyhedron([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [[0, 1, 2], [0, 2, 1]]);");

        // Assert
        Assert.Contains(result.Warnings, w => w.Message.Contains("no extent along Z", StringComparison.Ordinal) && w.Location == new ScadSourceLocation(null, 2, 1));
    }

    /// <summary>
    ///     Validates that a missing <c>newsize</c> is the identity and that empty children stay empty.
    /// </summary>
    [Fact]
    public void ScadResize_NoSizeOrNoChildren_IsHarmless()
    {
        // Arrange / Act
        var identity = Build("resize() cube([1, 2, 3]);");
        var empty = Build("resize([1, 2, 3]) { }");

        // Assert
        AssertBounds(identity.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 2, 3));
        Assert.Empty(empty.Meshes);
        Assert.Empty(empty.Warnings);
    }

    /// <summary>
    ///     Validates the invalid resize arguments: each warns and removes the instance.
    /// </summary>
    /// <param name="call">The resize call.</param>
    [Theory]
    [InlineData("resize(\"x\")")]
    [InlineData("resize([1, -2, 3])")]
    [InlineData("resize([1, 2, 3, 4])")]
    [InlineData("resize(5)")]
    [InlineData("resize([1, 2, 3], auto = [true, false, true, false])")]
    [InlineData("resize([1, 2, 3], auto = [])")]
    public void ScadResize_InvalidArguments_WarnAndRemoveInstance(string call)
    {
        // Arrange / Act
        var result = Build($"{call} cube(1);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that <c>convexity</c> is accepted and ignored without a warning.
    /// </summary>
    [Fact]
    public void ScadResize_Convexity_IsAcceptedSilently()
    {
        // Arrange / Act
        var result = Build("resize([2, 2, 2], false, 5) cube(1);");

        // Assert
        Assert.Empty(result.Warnings);
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 2, 2));
    }
}
