// cspell:ignore polyhedron
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadProjectionTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the <c>projection</c> module with <c>cut = true</c> (the cross-section at <c>z = 0</c>): sections of
///     solids of every kind, the tie rule for faces on the plane, sections of booleans, and meshes that are not closed.
/// </summary>
public class ScadGeometryBuilderProjectionCutTests
{
    /// <summary>
    ///     Validates that a cube that spans the plane has its footprint as section.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_CubeSpanningThePlane_IsItsFootprint()
    {
        // Arrange / Act
        var area = Area("projection(cut = true) cube([30, 20, 15], center = true);", widthUnits: 60, heightUnits: 40);

        // Assert
        Assert.Equal(600.0, area);
    }

    /// <summary>
    ///     Validates the tie rule: a cube that touches the plane with its underside or its top is cut by the face on the
    ///     plane (a closed set), whichever side it is on.
    /// </summary>
    /// <param name="source">The program text.</param>
    [Theory]
    [InlineData("projection(cut = true) cube(10);")]
    [InlineData("projection(cut = true) translate([0, 0, -10]) cube(10);")]
    public void ScadGeometryBuilderProjectionCut_CubeTouchingThePlane_IsCutByTheFaceOnIt(string source)
    {
        // Arrange / Act
        var area = Area(source, centerX: 5, centerY: 5);

        // Assert
        Assert.Equal(100.0, area);
    }

    /// <summary>
    ///     Validates that a plane that misses every solid gives nothing, silently.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_PlaneMissesTheSolid_IsEmptyAndSilent()
    {
        // Arrange / Act
        var above = BuildSource("projection(cut = true) translate([0, 0, 5]) cube(10);");
        var below = BuildSource("projection(cut = true) translate([0, 0, -20]) cube(10);");

        // Assert
        Assert.Empty(above.Meshes);
        Assert.Empty(above.Warnings);
        Assert.Empty(above.Unsupported);
        Assert.Empty(below.Meshes);
        Assert.Empty(below.Unsupported);
    }

    /// <summary>
    ///     Validates that the section of a tube is an annulus (the difference of the sections of its two cylinders).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_Tube_IsAnAnnulus()
    {
        // Arrange
        const string source = "projection(cut = true) difference() { cylinder(r = 10, h = 10, center = true, $fn = 64); cylinder(r = 6, h = 20, center = true, $fn = 64); }";

        // Act
        var area = Area(source);
        var covers = Covers(source, [(0, 0), (4, 0), (8, 0), (11, 0)]);

        // Assert
        Assert.Equal(PolygonArea(10, 64) - PolygonArea(6, 64), area, 2.0);
        Assert.Equal([false, false, true, false], covers);
    }

    /// <summary>
    ///     Validates that the operands of a boolean are sliced and then combined: a cube with a smaller cube cut out of it.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_Difference_IsTheDifferenceOfTheSections()
    {
        // Arrange / Act
        var area = Area("projection(cut = true) difference() { cube(10, center = true); cube(4, center = true); }");

        // Assert: 100 - 16
        Assert.Equal(84.0, area);
    }

    /// <summary>
    ///     Validates that the section of several solids is the union of their sections.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_SeveralSolids_AreUnited()
    {
        // Arrange / Act
        var area = Area("projection(cut = true) { cube(4, center = true); translate([10, 0, 0]) cube(2, center = true); }", centerX: 5);

        // Assert: 16 + 4
        Assert.Equal(20.0, area);
    }

    /// <summary>
    ///     Validates that a sphere is cut to a polygon a little smaller than its great circle.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_Sphere_IsADisc()
    {
        // Arrange / Act
        var area = Area("projection(cut = true) sphere(r = 5, $fn = 48);");

        // Assert
        Assert.InRange(area, 0.97 * Math.PI * 25.0, Math.PI * 25.0);
    }

    /// <summary>
    ///     Validates the section of a tilted solid: a cube turned 30 degrees about X is cut through its center into a
    ///     rectangle that is 10 wide and 10 / cos(30) long.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_TiltedCube_IsALongerRectangle()
    {
        // Arrange / Act
        var area = Area("projection(cut = true) rotate([30, 0, 0]) cube(10, center = true);", pixelsPerUnit: 20);

        // Assert
        Assert.Equal(100.0 / Math.Cos(Math.PI / 6.0), area, 1.0);
    }

    /// <summary>
    ///     Validates the tie rule for vertices: an octahedron with its four equator vertices exactly on the plane is cut
    ///     to its equator square.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_VerticesOnThePlane_GiveTheEquatorSquare()
    {
        // Arrange: points are +-3 on each axis; the faces are wound clockwise seen from outside, as the language wants
        const string source =
            "projection(cut = true) polyhedron(points = [[3, 0, 0], [0, 3, 0], [-3, 0, 0], [0, -3, 0], [0, 0, 3], [0, 0, -3]], "
            + "faces = [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4], [1, 0, 5], [2, 1, 5], [3, 2, 5], [0, 3, 5]]);";

        // Act
        var area = Area(source, widthUnits: 12, heightUnits: 12);
        var covers = Covers(source, [(0, 0), (1.4, 1.4), (2.6, 0), (2, 2)], widthUnits: 12, heightUnits: 12);

        // Assert: the diamond with diagonals 6 (area 18)
        Assert.Equal(18.0, area, 0.5);
        Assert.Equal([true, true, true, false], covers);
    }

    /// <summary>
    ///     Validates that a mesh that is not closed is reported and the projection is omitted, never approximated.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_OpenMesh_IsReportedAndOmitted()
    {
        // Arrange: a tetrahedron without one face, cut through the middle
        const string source = "projection(cut = true) polyhedron(points = [[0,0,-1],[4,0,-1],[0,4,-1],[0,0,3]], faces = [[0,2,1],[0,1,3],[1,2,3]]);";

        // Act
        var result = BuildSource(source);

        // Assert
        Assert.Equal(["scad-projection-open-mesh"], result.Features);
        Assert.Empty(result.Meshes);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), Assert.Single(result.Unsupported).Location);
    }

    /// <summary>
    ///     Validates that the section is a flat shape like any other: it can be extruded.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_Result_CanBeExtruded()
    {
        // Arrange / Act
        var result = BuildSource("linear_extrude(2) projection(cut = true) cube(3, center = true);");

        // Assert
        Assert.Equal(18.0, result.SingleMesh.SignedVolume, 1e-9);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the cut does not depend on the translation: a small cube far from the origin that spans the
    ///     plane has its footprint as section (no vertex of it counts as being on the plane).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCut_FarTranslatedCube_IsItsFootprint()
    {
        // Arrange / Act
        var result = BuildSource("projection(cut = true) translate([1e9, 0, -0.5]) cube(1);");

        // Assert
        var bounds = result.SingleMesh.Bounds;
        Assert.Equal(1e9, bounds.Min.X);
        Assert.Equal(1e9 + 1.0, bounds.Max.X);
        Assert.Equal(0.0, bounds.Min.Y);
        Assert.Equal(1.0, bounds.Max.Y);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a small solid just above or below the plane is not cut by it: the tolerance follows the size
    ///     of the model, so a gap of 5e-10 on a cube of 1e-3 is a gap.
    /// </summary>
    /// <param name="source">The program text.</param>
    [Theory]
    [InlineData("projection(cut = true) translate([0, 0, 5e-10]) cube(1e-3);")]
    [InlineData("projection(cut = true) translate([0, 0, -1.0000005e-3]) cube(1e-3);")]
    public void ScadGeometryBuilderProjectionCut_SmallSolidJustOffThePlane_IsEmpty(string source)
    {
        // Arrange / Act
        var result = BuildSource(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that every solid is judged by its own tolerance: a small solid just off the plane is still not cut
    ///     when a larger solid is next to it. A solid of size 1e4 has a tolerance of about 1e-5 and one of size 1e6 of
    ///     about 1e-3; with a tolerance shared by all solids the vertices 5e-10 off the plane would count as on it.
    /// </summary>
    /// <param name="small">The small solid, just off the plane.</param>
    /// <param name="size">The size of the solid beside it.</param>
    [Theory]
    [InlineData("translate([0, 0, 5e-10]) cube(1e-3);", "1e4")]
    [InlineData("translate([0, 0, 5e-10]) cube(1e-3);", "1e6")]
    [InlineData("translate([0, 0, -1.0000005e-3]) cube(1e-3);", "1e4")]
    [InlineData("translate([0, 0, -1.0000005e-3]) cube(1e-3);", "1e6")]
    public void ScadGeometryBuilderProjectionCut_SmallSolidJustOffThePlaneBesideAHugeSolid_IsEmpty(string small, string size)
    {
        // Arrange: the larger solid is wholly above the plane
        var source = $"projection(cut = true) {{ {small} translate([2e6, 0, 10]) cube({size}); }}";

        // Act
        var result = BuildSource(source);

        // Assert: the small solid is off the plane by more than its own tolerance
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a small solid that really is on the plane (within its own tolerance) is still cut when a solid
    ///     of size 1e6 is next to it, so the per-solid tolerance is not zero.
    /// </summary>
    /// <param name="source">The program text.</param>
    [Theory]
    [InlineData("projection(cut = true) { cube(1e-3); translate([2e6, 0, 10]) cube(1e6); }")]
    [InlineData("projection(cut = true) { translate([0, 0, 5e-13]) cube(1e-3); translate([2e6, 0, 10]) cube(1e6); }")]
    public void ScadGeometryBuilderProjectionCut_SmallSolidOnThePlaneBesideAHugeSolid_IsCut(string source)
    {
        // Arrange / Act
        var result = BuildSource(source);

        // Assert: one slab with the footprint of the small cube
        var bounds = result.SingleMesh.Bounds;
        Assert.Equal(0.0, bounds.Min.X, 1e-15);
        Assert.Equal(1e-3, bounds.Max.X, 1e-15);
        Assert.Equal(0.0, bounds.Min.Y, 1e-15);
        Assert.Equal(1e-3, bounds.Max.Y, 1e-15);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }
}
