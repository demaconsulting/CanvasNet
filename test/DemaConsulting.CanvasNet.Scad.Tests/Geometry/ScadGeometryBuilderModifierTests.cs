// cspell:ignore minkowski
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the <c>#</c> (highlight) and <c>%</c> (background) modifiers in <see cref="ScadGeometryBuilder"/>: the
///     overlay structure they build, their effect on booleans, bounds and the triangle budget, the 2D edge case, and
///     their interaction with <c>!</c>, <c>hull</c>, <c>resize</c> and the guards.
/// </summary>
public class ScadGeometryBuilderModifierTests
{
    /// <summary>The default highlight color of the builder.</summary>
    private static readonly Rgba32 DefaultHighlight = new(255, 64, 128, 110);

    /// <summary>The default ghost color of the builder.</summary>
    private static readonly Rgba32 DefaultGhost = new(128, 128, 128, 80);

    /// <summary>The nesting depth of the hand-made chain.</summary>
    private const int ChainDepth = 20_000;

    /// <summary>The fragment settings of the hand-made instances.</summary>
    private static readonly ScadFragmentSettings Defaults = new(0, 12, 2);

    /// <summary>
    ///     Lists the overlay nodes of a scene in document order, not descending into an overlay, walking iteratively.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The overlays.</returns>
    private static List<OverlayNode> Overlays(Scene scene)
    {
        var found = new List<OverlayNode>();
        var stack = new Stack<SceneNode>();
        stack.Push(scene.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            IReadOnlyList<SceneNode> children = [];
            switch (node)
            {
                case OverlayNode overlay:
                    found.Add(overlay);
                    break;
                case GroupNode group:
                    children = group.Children;
                    break;
                case CsgNode csg:
                    children = csg.Children;
                    break;
            }

            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }

        return found;
    }

    /// <summary>
    ///     Validates that <c>#</c> on a solid builds the normal geometry and an overlay in the highlight color that holds
    ///     the same node, and reports nothing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Highlight3D_BuildsNormalGeometryAndOverlayWithoutReport()
    {
        // Arrange / Act
        var result = Build("#cube(1);");

        // Assert
        var group = Assert.IsType<GroupNode>(result.Scene.Root);
        Assert.Equal(2, group.Children.Count);
        var mesh = Assert.IsType<MeshNode>(group.Children[0]);
        var overlay = Assert.IsType<OverlayNode>(group.Children[1]);
        Assert.Equal(DefaultHighlight, overlay.Color);
        Assert.Same(mesh, Assert.Single(overlay.Children));
        Assert.Equal(1.0, mesh.Mesh.SignedVolume, 1e-12);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the highlight shares the mesh nodes (no second mesh exists) and that the copy the renderer
    ///     draws is charged against the triangle budget once more.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Highlight_SharesMeshNodes_ChargesTheOverlayCopyAgainstTheBudget()
    {
        // Arrange / Act
        var plain = Build("cube(1);");
        var highlighted = Build("#cube(1);");

        // Assert: one mesh either way; the highlight doubles the charge, which is the triangles the renderer draws
        Assert.Single(highlighted.Meshes);
        Assert.Equal(12, plain.TrianglesCharged);
        Assert.Equal(2 * plain.TrianglesCharged, highlighted.TrianglesCharged);
        Assert.Equal(plain.MeshTriangles, highlighted.MeshTriangles);
    }

    /// <summary>
    ///     Validates that a highlight that does not fit the triangle budget twice fails with the located budget error
    ///     before anything is rendered.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_HighlightCopyOverTheBudget_ThrowsLocatedEvaluationException()
    {
        // Arrange / Act: a cube is 12 triangles, so 20 fit once and not twice
        var ex = Assert.Throws<ScadEvaluationException>(() => Build("\n  #cube(1);", Settings(maxTriangles: 20)));

        // Assert
        Assert.Equal(2, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that nested highlights, whose paths would double with every level, are stopped by the triangle
    ///     budget instead of exhausting the renderer.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_NestedHighlights_AreBoundedByTheTriangleBudget()
    {
        // Arrange: forty nested highlights would be 2^40 paths to the innermost cube
        const string Source = "module m(n) #union() { cube(1); if (n > 0) m(n - 1); }\nm(40);";

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => Build(Source));
    }

    /// <summary>
    ///     Validates that <c>%</c> on a solid builds only an overlay in the ghost color, with no ordinary geometry, and
    ///     reports nothing; a ghost is not drawn twice, so it adds no charge.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Background3D_BuildsOnlyAnOverlay_NoMainGeometry()
    {
        // Arrange / Act
        var result = Build("%cube(1);");

        // Assert
        var overlay = Assert.IsType<OverlayNode>(result.Scene.Root);
        Assert.Equal(DefaultGhost, overlay.Color);
        Assert.IsType<MeshNode>(Assert.Single(overlay.Children));
        Assert.Empty(result.Meshes);
        Assert.Equal(12, result.TrianglesCharged);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the colors of the settings replace the defaults for both overlays.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_CustomColors_AreUsedForBothOverlays()
    {
        // Arrange
        var highlight = new Rgba32(1, 2, 3, 4);
        var ghost = new Rgba32(5, 6, 7, 255);
        var settings = new ScadGeometrySettings(5_000_000, 0.1, false, TestContext.Current.CancellationToken, highlight, ghost);

        // Act
        var result = Build("#cube(1);\n%translate([3, 0, 0]) cube(1);", settings);

        // Assert
        var overlays = Overlays(result.Scene);
        Assert.Equal([highlight, ghost], overlays.Select(o => o.Color).ToArray());
    }

    /// <summary>
    ///     Validates that a ghost contributes no bounds (<c>resize</c> sizes the ordinary geometry only, even when it
    ///     must recompute the exact bounds of a rotated shape) while still being drawn.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Background_ContributesNoBounds_ButIsInReach()
    {
        // Arrange / Act: the rotated cube makes the propagated box loose, so resize recomputes the exact bounds
        var result = Build("resize([4, 4, 4]) { rotate([0, 0, 45]) cube(1); %translate([50, 0, 0]) cube(1); }");

        // Assert: the ordinary cube is 4 by 4 by 4; the ghost exists and was scaled with it, never counted
        var leaf = Assert.Single(result.Leaves);
        var size = leaf.WorldMesh.Bounds.Size;
        Assert.Equal(4.0, size.X, 1e-9);
        Assert.Equal(4.0, size.Y, 1e-9);
        Assert.Equal(4.0, size.Z, 1e-9);
        Assert.Single(Overlays(result.Scene));
    }

    /// <summary>
    ///     Validates that a ghost is false in an enclosing difference: the difference keeps the operand structure, and the
    ///     ghost operand is an overlay (the renderer treats it as nothing).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_BackgroundInsideDifference_DoesNotSubtract()
    {
        // Arrange / Act
        var result = Build("difference() { cube(2); %translate([1, 0, 0]) cube(2); }");

        // Assert
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        Assert.Equal(CsgOperation.Difference, csg.Operation);
        Assert.IsType<MeshNode>(csg.Children[0]);
        Assert.IsType<OverlayNode>(csg.Children[1]);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a highlighted subtrahend still subtracts (it is an ordinary operand) and also has its overlay.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_HighlightInsideDifference_StillSubtractsAndHasOverlay()
    {
        // Arrange / Act
        var result = Build("difference() { cube(4); #translate([1, 1, 1]) cube(2); }");

        // Assert
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        var cutter = Assert.IsType<GroupNode>(csg.Children[1]);
        Assert.IsType<GroupNode>(cutter.Children[0]);
        var overlay = Assert.IsType<OverlayNode>(cutter.Children[1]);
        Assert.Same(cutter.Children[0], Assert.Single(overlay.Children));
        Assert.Equal(2, result.Meshes.Count);
    }

    /// <summary>
    ///     Validates that a ghost as the first operand of a difference is a false minuend: the operands keep their
    ///     structure and the first one is the overlay (so nothing but the ghost is drawn).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_BackgroundFirstOperandOfDifference_LeavesMainEmpty()
    {
        // Arrange / Act
        var result = Build("difference() { %cube(2); cube(1); }");

        // Assert
        var csg = Assert.IsType<CsgNode>(result.Scene.Root);
        Assert.Equal(CsgOperation.Difference, csg.Operation);
        Assert.IsType<OverlayNode>(csg.Children[0]);
        Assert.IsType<MeshNode>(csg.Children[1]);
    }

    /// <summary>
    ///     Validates that a subtree marked with both modifiers is a ghost (the background wins).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_BothHighlightAndBackground_BackgroundWins()
    {
        // Arrange / Act
        var result = Build("#%cube(1);");

        // Assert
        var overlay = Assert.IsType<OverlayNode>(result.Scene.Root);
        Assert.Equal(DefaultGhost, overlay.Color);
        Assert.Empty(result.Meshes);
    }

    /// <summary>
    ///     Validates that a transform and a color around a ghost move it and leave its forced color alone: the bounds of
    ///     the scene (which include overlays) are the translated cube's.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_UnderTransformAndColor_AppliesTransformAndKeepsOverlayColor()
    {
        // Arrange / Act
        var result = Build("translate([5, 0, 0]) color(\"red\") %cube(1);");

        // Assert
        AssertBounds(result.Scene.ComputeBounds(), new Vector3D(5, 0, 0), new Vector3D(6, 1, 1));
        Assert.True(result.Scene.ComputeBounds(includeOverlays: false).IsEmpty);
        var overlay = Assert.Single(Overlays(result.Scene));
        Assert.Equal(DefaultGhost, overlay.Color);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that <c>#</c> on a 2D shape that is not extruded renders the slab normally and reports the
    ///     degradation once at the instance.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Highlight2D_IsRenderedNormallyAndReported()
    {
        // Arrange / Act
        var result = Build("\n  #square(1);");

        // Assert
        Assert.Single(result.Meshes);
        Assert.Empty(Overlays(result.Scene));
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-modifier-highlight", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 4), report.Location);
    }

    /// <summary>
    ///     Validates that <c>%</c> on a 2D shape that is not extruded omits the shape and reports it once at the instance.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Background2D_IsOmittedAndReported()
    {
        // Arrange / Act
        var result = Build("\n  %circle(2);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(Overlays(result.Scene));
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-modifier-background", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 4), report.Location);
    }

    /// <summary>
    ///     Validates that the modifier on an extrusion of a 2D shape (a solid) is a full ghost, with its own color, and
    ///     that the modifier on the 2D child inside the extrusion is the reported edge case.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Background3DOfExtrudedShape_Works()
    {
        // Arrange / Act
        var ghost = Build("%linear_extrude(2) square(1);");
        var inner = Build("linear_extrude(2) %square(1);");

        // Assert
        Assert.Equal(DefaultGhost, Assert.IsType<OverlayNode>(ghost.Scene.Root).Color);
        Assert.Empty(ghost.Unsupported);
        Assert.Equal(["scad-modifier-background"], inner.Features);
        Assert.Empty(inner.Meshes);
    }

    /// <summary>
    ///     Validates that the children of a ghost are visited like any others: an unsupported module and an invalid
    ///     argument inside it are still reported.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_ChildrenOfBackground_AreVisited_UnsupportedInsideIsReported()
    {
        // Arrange / Act
        var result = Build("% {\n  roof() cube(1);\n  cube(-1);\n  cube(1);\n}");

        // Assert
        Assert.Equal(["scad-module-roof"], result.Features);
        Assert.Contains(result.Warnings, w => w.Location.Line == 3);
        Assert.IsType<OverlayNode>(result.Scene.Root);
    }

    /// <summary>
    ///     Validates that a <c>!</c> inside a ghost does not take part in the show-only pre-pass: the model stays the
    ///     ordinary geometry, and the ghost holds both cubes.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_ShowOnly_StillIgnoresBackgroundSubtree()
    {
        // Arrange / Act
        var result = Build("%translate([1, 0, 0]) { cube(1); !cube(5); }\ncube(2);");

        // Assert
        Assert.Equal(8.0, result.SingleMesh.SignedVolume, 1e-12);
        var overlay = Assert.Single(Overlays(result.Scene));
        Assert.Equal(2, MeshCount(overlay));
    }

    /// <summary>
    ///     Counts the mesh nodes below a node (shared ones once per path), walking iteratively.
    /// </summary>
    /// <param name="root">The node.</param>
    /// <returns>The count.</returns>
    private static int MeshCount(SceneNode root)
    {
        var count = 0;
        var stack = new Stack<SceneNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            switch (node)
            {
                case MeshNode:
                    count++;
                    break;
                case GroupNode group:
                    group.Children.ToList().ForEach(stack.Push);
                    break;
                case CsgNode csg:
                    csg.Children.ToList().ForEach(stack.Push);
                    break;
                case OverlayNode overlay:
                    overlay.Children.ToList().ForEach(stack.Push);
                    break;
            }
        }

        return count;
    }

    /// <summary>
    ///     Validates that a <c>#</c> or <c>%</c> child of a <c>hull</c> is not part of the hull (the hull is of the
    ///     ordinary meshes), builds without throwing (also with an appearance, which strips colors from a copy), and
    ///     leaves no overlay behind.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_InsideHullChild_IsNotPartOfTheHullAndDoesNotThrow()
    {
        // Arrange / Act
        var ghost = Build("hull() { color(\"red\") cube(1); translate([5, 0, 0]) %cube(1); translate([0, 5, 0]) cube(1); }");
        var highlight = Build("hull() { cube(1); translate([5, 0, 0]) #cube(1); }");

        // Assert: the ghost does not stretch the hull, the highlighted cube does (it is an ordinary member too)
        var ghostLeaf = Assert.Single(ghost.Leaves);
        AssertBounds(ghostLeaf.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 6, 1), 1e-9);
        var highlightLeaf = Assert.Single(highlight.Leaves);
        AssertBounds(highlightLeaf.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(6, 1, 1), 1e-9);
        Assert.Empty(Overlays(ghost.Scene));
        Assert.Empty(Overlays(highlight.Scene));
    }

    /// <summary>
    ///     Validates that a <c>#</c> or <c>%</c> child of a <c>minkowski</c> is not part of the sum as an overlay: the
    ///     highlighted child is an ordinary summand (its overlay is not drawn) and a ghost child contributes nothing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_InsideMinkowskiChild_IsOrdinarySummandOrNothing()
    {
        // Arrange / Act
        var highlight = Build("minkowski() { cube(4); #cube(1); }");
        var ghost = Build("minkowski() { cube(4); %translate([9, 0, 0]) cube(1); }");

        // Assert: the sum of two boxes is the box of the summed sizes; a ghost leaves the lone block unchanged
        var summed = Assert.Single(highlight.Leaves);
        AssertBounds(summed.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(5, 5, 5), 1e-9);
        Assert.Empty(Overlays(highlight.Scene));
        Assert.Empty(highlight.Warnings);
        Assert.Empty(ghost.Warnings);
        Assert.Empty(Overlays(ghost.Scene));
    }

    /// <summary>
    ///     Validates that a ghost that is the only member of a <c>hull</c> leaves no points to hull: nothing is built
    ///     and nothing throws.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_HullOfOnlyAGhost_BuildsNothing()
    {
        // Arrange / Act
        var result = Build("hull() { %cube(1); }");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(Overlays(result.Scene));
    }

    /// <summary>
    ///     Validates that a hand-made tree 20,000 levels deep with a ghost on every group builds on a 64 KiB caller
    ///     stack exactly as on a big stack (the modifier code never recurses).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_DeeplyNested_IsIterative()
    {
        // Arrange / Act
        var big = OnSmallStack(BuildGhostChain, 256 * 1024 * 1024);
        var small = OnSmallStack(BuildGhostChain, 64 * 1024);

        // Assert: one overlay per group, nested in each other
        Assert.Equal(big, small);
        Assert.Equal(ChainDepth, big.Overlays);
    }

    /// <summary>
    ///     Builds the chain and describes it.
    /// </summary>
    /// <returns>The number of overlays and meshes counted per path.</returns>
    private static (int Overlays, int Meshes) BuildGhostChain()
    {
        ScadInstance Cube() =>
            new(ScadInstanceKind.BuiltinModule, "cube", [new ScadInstanceArgument(null, N(1))], [], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));

        var node = Cube();
        for (var i = 0; i < ChainDepth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.Group, "m", [], [Cube(), node], ScadModifiers.Background, Defaults, new ScadSourceLocation(null, 1, 1));
        }

        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var built = ScadGeometryBuilder.Build([node], Settings(), sink);

        // Count the overlays down the chain: each group is Overlay(Group[cube, Overlay(...)]), nested one in the other
        var overlays = 0;
        var meshes = 0;
        var stack = new Stack<SceneNode>();
        stack.Push(built.Scene.Root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            switch (current)
            {
                case OverlayNode overlay:
                    overlays++;
                    overlay.Children.ToList().ForEach(stack.Push);
                    break;
                case GroupNode group:
                    group.Children.ToList().ForEach(stack.Push);
                    break;
                case MeshNode:
                    meshes++;
                    break;
            }
        }

        return (overlays, meshes);
    }

    /// <summary>
    ///     Validates that cancellation is polled while building highlights and ghosts.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_Cancellation_IsPolledWhileBuildingOverlays()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert: 1,000 highlighted and ghosted instances
        Assert.Throws<OperationCanceledException>(
            () => Build("for (i = [1:1000]) { #translate([i, 0, 0]) cube(1); %translate([i, 5, 0]) cube(1); }", Settings(cancellation: cts.Token)));
    }

    /// <summary>
    ///     Validates that a ghost far out of range is rejected at the instance by the coordinate guard (the ghost
    ///     still counts as reach even though it has no bounds), also when only the enclosing transform pushes it out.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_OutOfRangeBackground_IsRejectedByCoordinateGuard()
    {
        // Arrange / Act
        var direct = Build("\n  %translate([2e100, 0, 0]) cube(1);");
        var viaReach = Build("\n  translate([6e99, 0, 0]) %translate([6e99, 0, 0]) cube(1);");

        // Assert
        Assert.Equal(["scad-geometry-rejected"], direct.Features);
        Assert.Equal(new ScadSourceLocation(null, 2, 4), direct.Unsupported[0].Location);
        Assert.Empty(Overlays(direct.Scene));
        Assert.Equal(["scad-geometry-rejected"], viaReach.Features);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), viaReach.Unsupported[0].Location);
        Assert.Empty(Overlays(viaReach.Scene));
    }

    /// <summary>
    ///     Validates that strict mode does not throw for the modifiers on solids or for a color alpha: they are rendered,
    ///     not degraded.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Modifiers_StrictMode_DoesNotThrowFor3DModifiersOrAlpha()
    {
        // Arrange / Act
        var result = Build("#cube(1);\n%translate([3, 0, 0]) cube(1);\ncolor(\"red\", 0.5) translate([6, 0, 0]) cube(1);", Settings(strict: true));

        // Assert
        Assert.Empty(result.Unsupported);
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal(2, Overlays(result.Scene).Count);
    }
}
