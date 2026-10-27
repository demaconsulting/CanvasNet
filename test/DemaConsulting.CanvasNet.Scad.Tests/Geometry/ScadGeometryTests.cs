using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Subsystem tests for the internal <c>ScadGeometry</c> subsystem: the geometry builder, the primitive
///     builders, the transforms and colors and the extrusions working together on whole programs, through the
///     evaluator and <c>ScadGeometryBuilder.Build</c>.
/// </summary>
public class ScadGeometryTests
{
    /// <summary>
    ///     Validates that the primitive solids of one program build to closed, outward-oriented meshes with
    ///     analytic bounds, in document order.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_PrimitiveSolids_AreClosedOutwardMeshesInDocumentOrder()
    {
        // Arrange / Act: a cube, a cylinder and a sphere side by side
        var result = Build("cube([2, 3, 4]);\ntranslate([10, 0, 0]) cylinder(h = 5, r = 1, $fn = 16);\ntranslate([20, 0, 0]) sphere(r = 2, $fn = 24);");

        // Assert: three closed meshes with positive volume, in source order
        var leaves = result.Leaves;
        Assert.Equal(3, leaves.Count);
        Assert.All(leaves, leaf =>
        {
            Assert.True(leaf.WorldMesh.IsClosed);
            Assert.True(leaf.WorldMesh.SignedVolume > 0.0);
        });
        AssertSolidVolume(leaves[0].WorldMesh, 24.0);
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(9, -1, 0), new Vector3D(11, 1, 5), 1e-9);
        AssertBounds(leaves[2].WorldMesh.Bounds, new Vector3D(18, -2, -2), new Vector3D(22, 2, 2), 0.3);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Features);
    }

    /// <summary>
    ///     Validates that transforms and colors compose over primitives: the world placement of a transformed solid
    ///     is analytic and the innermost color is the appearance of the leaf.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_TransformsAndColorsOverPrimitives_ComposeWorldPlacementAndAppearance()
    {
        // Arrange / Act: rotate a 2x1x1 box a quarter turn about Z and move it 10 along X, in red
        var result = Build("color(\"red\") translate([10, 0, 0]) rotate([0, 0, 90]) cube([2, 1, 1]);");

        // Assert: the box covers x 9..10, y 0..2, z 0..1 and carries the red appearance
        var leaf = Assert.Single(result.Leaves);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(9, 0, 0), new Vector3D(10, 2, 1), 1e-9);
        Assert.NotNull(leaf.Appearance);
        Assert.Equal(new Rgba32(255, 0, 0, 255), leaf.Appearance.Color);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a difference of primitives becomes a boolean node whose first operand is the minuend.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_DifferenceOfPrimitives_BuildsDifferenceNodeWithMinuendFirst()
    {
        // Arrange / Act
        var result = Build("difference() { cube(10); translate([5, 5, -1]) cylinder(r = 2, h = 12, $fn = 12); }");

        // Assert: one CSG difference with two operands, the first of which is the cube
        var difference = FindDifference(result.Scene);
        Assert.NotNull(difference);
        Assert.Equal(2, difference.Children.Count);
        var meshes = result.Meshes;
        Assert.Equal(2, meshes.Count);
        AssertBounds(meshes[0].Mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(10, 10, 10), 1e-9);
    }

    /// <summary>
    ///     Validates that extrusions of 2D shapes build closed solids with analytic bounds.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_ExtrusionsOfShapes_BuildClosedSolids()
    {
        // Arrange / Act: a linear extrusion of a square and a revolved rectangle
        var result = Build("linear_extrude(height = 5) square(10);\ntranslate([20, 0, 0]) rotate_extrude($fn = 16) translate([5, 0]) square([2, 3]);");

        // Assert: both are closed with positive volume; the prism has the exact volume
        var leaves = result.Leaves;
        Assert.Equal(2, leaves.Count);
        AssertSolidVolume(leaves[0].WorldMesh, 500.0);
        AssertBounds(leaves[0].WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(10, 10, 5), 1e-9);
        Assert.True(leaves[1].WorldMesh.IsClosed);
        Assert.True(leaves[1].WorldMesh.SignedVolume > 0.0);
        Assert.Equal(0.0, leaves[1].WorldMesh.Bounds.Min.Z, 1e-9);
        Assert.Equal(3.0, leaves[1].WorldMesh.Bounds.Max.Z, 1e-9);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that invalid and unsupported constructs are reported with their locations and never silently
    ///     skipped, while the supported geometry around them is still built.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_InvalidAndUnsupportedConstructs_AreReportedAtTheirLocations()
    {
        // Arrange: one construct per line
        const string Source = "cube(2);\ncube(-1);\nsurface(\"a\");\nroof() cube(1);\noffset(1) square(1);";

        // Act
        var result = Build(Source);

        // Assert: the valid cube is built; the invalid cube is a located warning; the others are unsupported features
        Assert.Single(result.Meshes);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(2, warning.Location.Line);
        Assert.Equal(["scad-module-surface", "scad-module-roof", "scad-module-offset"], result.Features);
        Assert.Equal([3, 4, 5], result.Unsupported.Select(u => u.Location.Line).ToArray());
    }

    /// <summary>
    ///     Validates that hull and minkowski work with the rest of the subsystem: a hull of transformed solids, a
    ///     rounded box from a minkowski sum, a 2D hull that extrudes, and a hull that is a valid boolean operand,
    ///     all closed and with analytic bounds and without reports.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_HullAndMinkowski_BuildClosedSolids()
    {
        // Arrange / Act
        var result = Build(
            "hull() { cube(1); translate([10, 0, 0]) cube(1); }\n"
            + "translate([0, 5, 0]) minkowski() { cube([4, 4, 4]); sphere(r = 1, $fn = 16); }\n"
            + "translate([0, 20, 0]) linear_extrude(height = 2) hull() { square(2); translate([6, 6]) square(1); }\n"
            + "difference() { translate([0, 30, 0]) cube(20); translate([5, 35, 5]) hull() { cube(2); translate([4, 4, 4]) cube(2); } }");

        // Assert: every mesh is closed with positive volume, the hull spans both cubes, the sum rounds the box
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Features);
        var leaves = result.Leaves;
        Assert.Equal(5, leaves.Count);
        Assert.All(leaves, leaf =>
        {
            Assert.True(leaf.WorldMesh.IsClosed);
            Assert.True(leaf.WorldMesh.SignedVolume > 0.0);
        });
        AssertBounds(leaves[0].WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(11, 1, 1), 1e-9);
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(-1, 4, -1), new Vector3D(5, 10, 5), 1e-9);
        AssertBounds(leaves[2].WorldMesh.Bounds, new Vector3D(0, 20, 0), new Vector3D(7, 27, 2), 1e-9);
        Assert.NotNull(FindDifference(result.Scene));
    }

    /// <summary>
    ///     Validates that a text region works with the rest of the subsystem: it is a flat shape that takes part in a
    ///     2D difference and is consumed by <c>linear_extrude</c>, without warnings or unsupported reports, and the
    ///     extruded glyph has the height that <c>size</c> asks for.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_TextRegion_ParticipatesIn2DBooleanAndLinearExtrusion()
    {
        // Arrange / Act: an H (10 tall) with a slot cut out of its crossbar, extruded 2 units
        var result = Build("linear_extrude(height = 2) difference() { text(\"H\", size = 10); translate([2, 4]) square([2, 2]); }");

        // Assert: one difference of two closed extrusions, the first being the H
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Features);
        Assert.NotNull(FindDifference(result.Scene));
        var leaves = result.Leaves;
        Assert.Equal(2, leaves.Count);
        var glyph = leaves[0].WorldMesh;
        Assert.True(glyph.IsClosed);
        Assert.Equal(0.0, glyph.Bounds.Min.Y, 1e-9);
        Assert.Equal(10.0, glyph.Bounds.Max.Y, 1e-9);
        Assert.Equal(0.0, glyph.Bounds.Min.Z, 1e-9);
        Assert.Equal(2.0, glyph.Bounds.Max.Z, 1e-9);
    }

    /// <summary>
    ///     Validates that strict mode stops at the first unsupported feature with its identifier and location.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_StrictMode_ThrowsAtTheFirstUnsupportedFeature()
    {
        // Arrange
        var settings = Settings(strict: true);

        // Act
        var exception = Assert.Throws<ScadUnsupportedFeatureException>(
            () => Build("cube(1);\nprojection() cube(1);\nsurface(\"a\");", settings));

        // Assert
        Assert.Equal("scad-module-projection", exception.Feature);
        Assert.Equal(2, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that the triangle budget is enforced before a hostile model allocates its geometry, with the
    ///     location of the module that exceeded it.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_TriangleBudgetExceeded_ThrowsLocatedEvaluationException()
    {
        // Arrange
        var settings = Settings(maxTriangles: 100);

        // Act
        var exception = Assert.Throws<ScadEvaluationException>(() => Build("cube(1);\nsphere(r = 1, $fn = 64);", settings));

        // Assert
        Assert.Equal(2, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that a deeply nested model builds on a small caller stack to the same scene as on a big stack.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_DeepNestedModel_OnSmallStack_MatchesBigStack()
    {
        // Arrange: 2,000 nested translations, each holding a cube
        const string Source = "module r(n) { translate([1, 0, 0]) { cube(1); if (n > 0) r(n - 1); } }\nr(2000);";
        var evaluation = new ScadEvaluationSettings { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var big = OnSmallStack(() => Describe(Build(Source, evaluation: evaluation)), 256 * 1024 * 1024);
        var small = OnSmallStack(() => Describe(Build(Source, evaluation: evaluation)), SmallStack);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal(2001, big.Meshes);
        Assert.True(big.Depth > 2000);
    }

    /// <summary>
    ///     Validates that a color alpha and the modifiers on solids are rendered, not reported: the alpha reaches the
    ///     appearance, and the highlight and the ghost are overlays, with no warning and no unsupported feature.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_ColorAlphaAndModifiersOnSolids_AreRenderedNotReported()
    {
        // Arrange / Act
        var result = Build("color(\"red\", 0.5) cube(1);\n#translate([3, 0, 0]) cube(1);\n%translate([6, 0, 0]) cube(1);");

        // Assert
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Leaves.Count);
        Assert.Equal(128, result.Leaves[0].Appearance!.Color.A);
        var group = Assert.IsType<GroupNode>(result.Scene.Root);
        Assert.Equal(2, group.Children.OfType<OverlayNode>().Count() + group.Children.OfType<GroupNode>().SelectMany(g => g.Children.OfType<OverlayNode>()).Count());
    }

    /// <summary>
    ///     Validates that the show-only modifier makes the marked subtree the whole model, without the transform of
    ///     its ancestors, and that the highlight modifier still builds the geometry and overlays it without a report.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_Modifiers_ShowOnlyReplacesTheModelAndHighlightIsOverlaid()
    {
        // Arrange / Act: the marked cube sits under a translation that must not apply to it
        var showOnly = Build("translate([100, 0, 0]) { cube(1); !cube(2); }");
        var highlight = Build("#cube(1);");

        // Assert: only the marked cube is built, at the origin; the highlighted cube is built and overlaid
        var leaf = Assert.Single(showOnly.Leaves);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 2, 2), 1e-9);
        Assert.Single(highlight.Meshes);
        Assert.Empty(highlight.Features);
        var group = Assert.IsType<GroupNode>(highlight.Scene.Root);
        Assert.Contains(group.Children, child => child is OverlayNode);
    }

    /// <summary>
    ///     Validates that every mesh node of the scene maps back to the location of the module that produced it.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_MeshNodes_MapBackToTheirModuleLocations()
    {
        // Arrange / Act: a cube on line 1 and a sphere on line 2
        var result = Build("cube(1);\ntranslate([5, 0, 0]) sphere(1);");

        // Assert: the map answers with each module's line
        var meshes = result.Meshes;
        Assert.Equal(2, meshes.Count);
        Assert.Equal(1, result.Map.Find(meshes[0]).Line);
        Assert.Equal(2, result.Map.Find(meshes[1]).Line);
    }

    /// <summary>
    ///     Validates that a 2D shape at the top of the model becomes a slab of the configured thickness.
    /// </summary>
    [Fact]
    public void ScadGeometry_Build_TopLevelFlatShape_BecomesASlabOfTheConfiguredThickness()
    {
        // Arrange
        var settings = Settings(thickness: 0.5);

        // Act
        var result = Build("square(10);", settings);

        // Assert: a closed slab of 10 x 10 x 0.5
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, 50.0);
        Assert.Equal(0.5, mesh.Bounds.Max.Z - mesh.Bounds.Min.Z, 1e-9);
    }

    /// <summary>
    ///     Describes a built scene for comparison: the mesh count, the nesting depth and the total volume.
    /// </summary>
    /// <param name="result">The built scene.</param>
    /// <returns>The description.</returns>
    private static (int Meshes, int Depth, double Volume) Describe(ScadBuildResult result) =>
        (result.Meshes.Count, Depth(result.Scene), result.Meshes.Sum(m => m.Mesh.SignedVolume));

    /// <summary>
    ///     Finds the first difference node of a scene, walking iteratively.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The node, or <see langword="null"/> when there is none.</returns>
    private static CsgNode? FindDifference(Scene scene)
    {
        var stack = new Stack<SceneNode>();
        stack.Push(scene.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is CsgNode { Operation: CsgOperation.Difference } csg)
            {
                return csg;
            }

            IReadOnlyList<SceneNode> children = node switch
            {
                GroupNode group => group.Children,
                CsgNode other => other.Children,
                _ => [],
            };
            foreach (var child in children)
            {
                stack.Push(child);
            }
        }

        return null;
    }
}
