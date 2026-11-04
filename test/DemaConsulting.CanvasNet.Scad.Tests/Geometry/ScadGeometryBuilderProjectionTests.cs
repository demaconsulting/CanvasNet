// cspell:ignore polyhedron
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadProjectionTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the <c>projection</c> module with <c>cut = false</c> (the shadow): silhouettes of solids, holes,
///     tubes and pockets, the arguments and children it accepts, and how its 2D result is consumed. The shapes are
///     measured by drawing them from above in flat colors.
/// </summary>
public class ScadGeometryBuilderProjectionTests
{
    /// <summary>
    ///     Validates that the shadow of a cube is its footprint, exactly.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Cube_IsItsFootprint()
    {
        // Arrange / Act
        var area = Area("projection() cube([30, 20, 15], center = true);", widthUnits: 60, heightUnits: 40);

        // Assert: 30 x 20 exactly (every pixel edge falls on a model coordinate)
        Assert.Equal(600.0, area);
    }

    /// <summary>
    ///     Validates that the tolerances do not depend on the translation: a small cube far from the origin projects to
    ///     its footprint, as it does at the origin.
    /// </summary>
    /// <param name="source">The program text.</param>
    /// <param name="minX">The expected smallest X of the footprint.</param>
    /// <param name="minY">The expected smallest Y of the footprint.</param>
    [Theory]
    [InlineData("projection() translate([1e9, 0, 0]) cube(1);", 1e9, 0.0)]
    [InlineData("projection() translate([0, 0, 1e9]) cube(1);", 0.0, 0.0)]
    [InlineData("projection() translate([-1e9, 1e9, 0]) cube(1);", -1e9, 1e9)]
    public void ScadGeometryBuilderProjection_FarTranslatedSmallCube_StillProjects(string source, double minX, double minY)
    {
        // Arrange / Act
        var result = BuildSource(source);

        // Assert: one slab with the footprint of the cube (compared as doubles, not through a picture)
        var bounds = result.SingleMesh.Bounds;
        Assert.Equal(minX, bounds.Min.X);
        Assert.Equal(minX + 1.0, bounds.Max.X);
        Assert.Equal(minY, bounds.Min.Y);
        Assert.Equal(minY + 1.0, bounds.Max.Y);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a boolean far from the origin is classified like one at the origin: a cube with a through hole
    ///     keeps the hole when translated by a large distance.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_FarTranslatedDifference_KeepsTheHole()
    {
        // Arrange
        const string source = "projection() translate([1e7, 1e7, 0]) difference() { cube(10); translate([3, 3, -1]) cube([4, 4, 12]); }";

        // Act
        var result = BuildSource(source);

        // Assert: a frame of four slabs or more (a solid square would be one slab), no report
        Assert.True(result.Meshes.Count > 1);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
        Assert.Equal(1e7, result.Meshes.Min(m => m.Mesh.Bounds.Min.X));
        Assert.Equal(1e7 + 10.0, result.Meshes.Max(m => m.Mesh.Bounds.Max.X));
    }

    /// <summary>
    ///     Validates that the tolerances follow the size of the model: a tiny cube still projects to its footprint.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_SmallScaleCube_Projects()
    {
        // Arrange / Act
        var result = BuildSource("projection() cube(1e-4);");

        // Assert
        var bounds = result.SingleMesh.Bounds;
        Assert.Equal(0.0, bounds.Min.X);
        Assert.Equal(1e-4, bounds.Max.X, 1e-15);
        Assert.Equal(1e-4, bounds.Max.Y, 1e-15);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a small through hole stays a hole at small scale (the vertical walls and the cut faces are
    ///     told apart by a tolerance that follows the model).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_SmallScaleDifference_KeepsTheHole()
    {
        // Arrange
        const string source = "projection() difference() { cube(1e-3); translate([3e-4, 3e-4, -1e-3]) cube([4e-4, 4e-4, 3e-3]); }";

        // Act
        var result = BuildSource(source);

        // Assert
        Assert.True(result.Meshes.Count > 1);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a solid turned about the X axis projects to the extent it has in X and Z.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_CubeTurnedAboutX_IsItsFrontView()
    {
        // Arrange / Act
        var area = Area("projection() rotate([90, 0, 0]) cube([30, 20, 15], center = true);", widthUnits: 60, heightUnits: 40);

        // Assert: 30 wide and 15 tall
        Assert.Equal(450.0, area);
    }

    /// <summary>
    ///     Validates that a solid turned about the Z axis projects to the turned footprint (same area, a diamond).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_CubeTurnedAboutZ_IsADiamond()
    {
        // Arrange / Act
        var area = Area("projection() rotate([0, 0, 45]) cube(10, center = true);");
        var covers = Covers("projection() rotate([0, 0, 45]) cube(10, center = true);", [(0, 0), (6, 0), (0, 6), (4.9, 4.9), (6.5, 6.5)]);

        // Assert: still 100 square units (within the antialiasing of the edges), and shaped like a diamond
        Assert.Equal(100.0, area, 1.5);
        Assert.Equal([true, true, true, false, false], covers);
    }

    /// <summary>
    ///     Validates the shadow of a cylinder: the polygon of its caps, so its area is that of the regular polygon.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Cylinder_IsThePolygonOfItsCap()
    {
        // Arrange / Act
        var area = Area("projection() cylinder(r = 5, h = 3, $fn = 64);");

        // Assert
        Assert.Equal(PolygonArea(5, 64), area, 1.0);
    }

    /// <summary>
    ///     Validates that a sphere projects to a disc a little smaller than its radius (its widest ring is below the equator).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Sphere_IsADisc()
    {
        // Arrange / Act
        var area = Area("projection() sphere(r = 5, $fn = 48);");

        // Assert: between 97 percent of the circle and the circle
        Assert.InRange(area, 0.97 * Math.PI * 25.0, Math.PI * 25.0);
    }

    /// <summary>
    ///     Validates that a difference with a through hole projects to the plate with the hole in it.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_PlateWithThroughHole_HasTheHole()
    {
        // Arrange
        const string source = "projection() difference() { cube([20, 20, 10], center = true); cylinder(r = 4, h = 12, center = true, $fn = 64); }";

        // Act
        var area = Area(source);
        var covers = Covers(source, [(0, 0), (3, 0), (6, 0), (9, 9)]);

        // Assert: the plate minus the polygon of the hole, and the hole is empty
        Assert.Equal(400.0 - PolygonArea(4, 64), area, 1.5);
        Assert.Equal([false, false, true, true], covers);
    }

    /// <summary>
    ///     Validates that a blind pocket (a cutter that does not reach the underside) leaves the silhouette whole.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_BlindPocket_HasNoHole()
    {
        // Arrange: the pocket is cut from the top down to the middle of the plate
        const string source = "projection() difference() { cube([20, 20, 10], center = true); translate([0, 0, 2]) cylinder(r = 4, h = 12, center = true, $fn = 64); }";

        // Act
        var area = Area(source);

        // Assert: the whole 20 x 20 outline
        Assert.Equal(400.0, area);
    }

    /// <summary>
    ///     Validates that a pocket from below is just as invisible from above.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_PocketFromBelow_HasNoHole()
    {
        // Arrange: the cutter starts below the plate and ends in its middle
        const string source = "projection() difference() { cube([20, 20, 10], center = true); translate([0, 0, -6]) cylinder(r = 4, h = 12, center = true, $fn = 64); }";

        // Act
        var area = Area(source);

        // Assert
        Assert.Equal(400.0, area);
    }

    /// <summary>
    ///     Validates the hardest case of a hole: a tube made of two cylinders of the same height (the end faces of the
    ///     cutter are flush with the ones of the tube), which must be an annulus.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_FlushTube_IsAnAnnulus()
    {
        // Arrange
        const string source = "projection() difference() { cylinder(r = 10, h = 10, $fn = 64); cylinder(r = 6, h = 10, $fn = 64); }";

        // Act
        var area = Area(source);
        var covers = Covers(source, [(0, 0), (4, 0), (8, 0), (11, 0)]);

        // Assert: the area of the ring and a hole in the middle
        Assert.Equal(PolygonArea(10, 64) - PolygonArea(6, 64), area, 2.0);
        Assert.Equal([false, false, true, false], covers);
    }

    /// <summary>
    ///     Validates that the extrusion of a ring (which is itself a difference of two extrusions) projects to the ring.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_ExtrudedRing_IsTheRing()
    {
        // Arrange
        const string source = "projection() linear_extrude(5) difference() { circle(r = 8, $fn = 64); circle(r = 5, $fn = 64); }";

        // Act
        var area = Area(source);

        // Assert
        Assert.Equal(PolygonArea(8, 64) - PolygonArea(5, 64), area, 1.5);
    }

    /// <summary>
    ///     Validates that a difference whose cutter is much taller than the solid, and a cutter that is far away from it,
    ///     both give the outline that is left.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_TallAndFarCutters_GiveTheRemainingOutline()
    {
        // Arrange
        const string tall = "projection() difference() { cube([10, 10, 2], center = true); cylinder(r = 2, h = 50, center = true, $fn = 32); }";
        const string far = "projection() difference() { cube([10, 10, 2], center = true); translate([30, 0, 0]) cylinder(r = 2, h = 50, center = true, $fn = 32); }";

        // Act / Assert: the first has a hole, the second is a plain square
        Assert.Equal(100.0 - PolygonArea(2, 32), Area(tall), 1.0);
        Assert.Equal(100.0, Area(far, widthUnits: 80), 1e-9);
    }

    /// <summary>
    ///     Validates the shadow of an intersection: the overlap of the two footprints where both solids exist.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Intersection_IsTheOverlap()
    {
        // Arrange: a 10 x 10 slab through a 10 x 10 x 10 cube, shifted by 4 in X
        const string source = "projection() intersection() { cube([10, 10, 10], center = true); translate([4, 0, 0]) cube([10, 10, 2], center = true); }";

        // Act
        var area = Area(source);

        // Assert: x from -1 to 5, y from -5 to 5
        Assert.Equal(60.0, area);
    }

    /// <summary>
    ///     Validates the shadow of a union whose members overlap in the plane at different heights.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_UnionOfOverlappingFootprints_IsTheUnionOfTheOutlines()
    {
        // Arrange: two 10 x 10 squares, one 5 to the right and 7 higher
        const string source = "projection() { cube(10); translate([5, 0, 7]) cube(10); }";

        // Act
        var area = Area(source, centerX: 5, centerY: 5);

        // Assert: 100 + 100 - the 5 x 10 overlap
        Assert.Equal(150.0, area);
    }

    /// <summary>
    ///     Validates that the transforms around the projection apply to the 2D result and the ones inside to the solid.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_TransformsOutsideAndInside_ApplyToTheirOwnDimension()
    {
        // Arrange: a 2 x 2 x 2 cube shifted by 5 in X before projecting and by 3 in Y afterward
        const string source = "translate([0, 3]) projection() translate([5, 0, 9]) cube(2);";

        // Act
        var covers = Covers(source, [(6, 4), (6, 1), (1, 4), (5.5, 3.5)]);

        // Assert: the square stands at x 5..7, y 3..5
        Assert.Equal([true, false, false, true], covers);
    }

    /// <summary>
    ///     Validates that the 2D result of a projection is an ordinary shape: it can be extruded, subtracted from,
    ///     and put into a hull.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Result_IsConsumedByOtherModules()
    {
        // Arrange / Act
        var extruded = BuildSource("linear_extrude(3) projection() cube(2);");
        var subtracted = Area("difference() { square(10, center = true); projection() cube(2, center = true); }");
        var hull = Area("hull() { projection() cube(2); translate([6, 0]) projection() cube(2); }", centerX: 4, centerY: 1);

        // Assert
        Assert.Equal(12.0, extruded.SingleMesh.SignedVolume, 1e-9);
        Assert.Empty(extruded.Warnings);
        Assert.Equal(96.0, subtracted);
        Assert.Equal(16.0, hull);
    }

    /// <summary>
    ///     Validates that <c>%</c> children are skipped and a <c>#</c> child is projected once (the highlight is not
    ///     a second solid).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_OverlayChildren_AreNotProjectedTwice()
    {
        // Arrange / Act
        var background = Area("projection() { cube(2); %translate([10, 0, 0]) cube(2); }", centerX: 5, centerY: 1);
        var highlight = BuildSource("projection() #cube(2);");

        // Assert: the ghost has no shadow, and the highlighted cube projects to one square leaf
        Assert.Equal(4.0, background);
        Assert.Empty(highlight.Warnings);
        Assert.Single(highlight.Meshes);
    }

    /// <summary>
    ///     Validates that a 2D child is ignored with a warning, and that nothing to project is empty and silent.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_TwoDimensionalAndNoChildren_WarnOrStaySilent()
    {
        // Arrange / Act
        var mixed = BuildSource("projection() {\n  square(3);\n  cube(2);\n}");
        var onlyFlat = BuildSource("projection() square(3);");
        var none = BuildSource("projection() {}");

        // Assert: the 2D child is reported once at the module, and the cube is still projected
        Assert.Equal(["1:1: projection: only 3D children can be projected; the 2D children are ignored"], mixed.WarningTexts);
        Assert.Single(mixed.Meshes);
        Assert.Single(onlyFlat.Warnings);
        Assert.Empty(onlyFlat.Meshes);
        Assert.Empty(none.Warnings);
        Assert.Empty(none.Unsupported);
        Assert.Empty(none.Meshes);
    }

    /// <summary>
    ///     Validates the arguments: a boolean <c>cut</c> is honored, another value is reported and counts as false,
    ///     <c>convexity</c> is ignored silently, and an unknown argument is reported like for any module.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Arguments_AreValidated()
    {
        // Arrange / Act
        var convexity = BuildSource("projection(convexity = 4) cube(2);");
        var number = BuildSource("projection(cut = 2) cube(2);");
        var unknown = BuildSource("projection(wat = 1) cube(2);");

        // Assert
        Assert.Empty(convexity.Warnings);
        Assert.Equal(["1:1: projection: 'cut' must be true or false; false is used"], number.WarningTexts);
        Assert.Single(number.Meshes);
        Assert.Equal(["1:1: Ignoring unknown argument 'wat' of projection"], unknown.WarningTexts);
    }

    /// <summary>
    ///     Validates that colors inside the projection are not kept, with one warning, and that a color around it applies.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_Colors_AreNotKeptInsideAndApplyOutside()
    {
        // Arrange / Act
        var inside = BuildSource("projection() color(\"red\") cube(2);");
        var outside = BuildSource("color(\"red\") projection() cube(2);");

        // Assert
        Assert.Equal(["1:1: projection: colors inside projection() are not kept; the result takes the enclosing color"], inside.WarningTexts);
        Assert.Null(Assert.Single(inside.Leaves).Appearance);
        Assert.Empty(outside.Warnings);
        Assert.NotNull(Assert.Single(outside.Leaves).Appearance);
    }

    /// <summary>
    ///     Validates the shadow of an open mesh (a single triangle): every triangle that is not vertical counts, whichever way it faces.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_OpenMesh_ProjectsItsTriangles()
    {
        // Arrange: a lone triangle tilted out of the XY plane
        const string source = "projection() polyhedron(points = [[0, 0, 0], [4, 0, 0], [0, 4, 1]], faces = [[0, 1, 2]]);";

        // Act
        var area = Area(source, centerX: 2, centerY: 2, widthUnits: 10, heightUnits: 10);

        // Assert: the triangle with legs 4 and 4
        Assert.Equal(8.0, area, 0.5);
    }

    /// <summary>
    ///     Validates that vertical and degenerate faces have no footprint: a vertical wall alone projects to nothing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_VerticalFaces_HaveNoFootprint()
    {
        // Arrange / Act: a triangle standing on the X axis
        var result = BuildSource("projection() polyhedron(points = [[0, 0, 0], [4, 0, 0], [0, 0, 3]], faces = [[0, 1, 2]]);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that the triangles of a face that lie in one plane are merged into one region: a cube top and a
    ///     cylinder cap are one slab each, instead of one per triangle.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_CoplanarFaces_AreMergedIntoOneRegion()
    {
        // Arrange / Act
        var cube = BuildSource("projection() cube(5);");
        var cylinder = BuildSource("projection() cylinder(r = 4, h = 2, $fn = 64);");

        // Assert: one slab each
        Assert.Single(cube.Meshes);
        Assert.Single(cylinder.Meshes);
    }

    /// <summary>
    ///     Validates the fallback of the face merge: a face whose boundary touches itself at a vertex cannot be one
    ///     region, so it falls back to one region per triangle, which covers the same area.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_FaceWithATouchingHole_FallsBackToTriangles()
    {
        // Arrange: a 4 x 4 square with a quadrilateral hole that touches its corner at the origin (area 16 - 3), six triangles
        const string points = "[[0, 0, 0], [4, 0, 0], [4, 4, 0], [0, 4, 0], [2, 1, 0], [3, 3, 0], [1, 2, 0]]";
        const string faces = "[[0, 4, 1], [1, 5, 2], [1, 4, 5], [2, 5, 3], [3, 5, 6], [3, 6, 0]]";
        var source = $"projection() polyhedron(points = {points}, faces = {faces});";

        // Act
        var result = BuildSource(source);
        var area = Area(source, centerX: 2, centerY: 2, widthUnits: 10, heightUnits: 10);

        // Assert: six leaves and the exact area
        Assert.Equal(6, result.Meshes.Count);
        Assert.Equal(13.0, area, 0.2);
    }

    /// <summary>
    ///     Validates that a deeply nested projection builds on a small caller stack (nothing recurses).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjection_DeepNesting_BuildsOnASmallStack()
    {
        // Arrange: 3,000 nested translates (a recursive module) around a cube, under a projection
        const string source = "module r(n) { translate([0.001, 0, 0]) { if (n > 0) r(n - 1); else cube(1); } }\nprojection() r(3000);";
        var evaluation = new ScadEvaluationSettings { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var meshes = OnSmallStack(() => Build(source, evaluation: evaluation).Meshes.Count, SmallStack);

        // Assert: one square slab
        Assert.Equal(1, meshes);
    }

    /// <summary>
    ///     Runs a function on a thread with a deliberately small stack and returns its result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="work">The function.</param>
    /// <param name="stackSize">The stack size in bytes.</param>
    /// <returns>The result.</returns>
    private static T OnSmallStack<T>(Func<T> work, int stackSize)
    {
        var result = default(T);
        Exception? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            },
            stackSize);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("The work failed on the small stack.", failure);
        }

        return result!;
    }
}
