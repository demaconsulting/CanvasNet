// cspell:ignore multmatrix minkowski

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadGeometryBuilder"/>'s walk: implicit unions, modifiers, the report of modules that
///     are not built, locations, de-duplication and strict mode.
/// </summary>
public class ScadGeometryBuilderWalkTests
{
    /// <summary>The built-in modules the geometry builder builds (or combines) in this phase.</summary>
    private static readonly HashSet<string> Implemented =
        [
            "cube", "sphere", "cylinder", "polyhedron", "square", "circle", "polygon", "union", "render", "difference", "intersection",
            "translate", "rotate", "scale", "mirror", "multmatrix", "resize", "color", "linear_extrude", "rotate_extrude", "hull", "minkowski",
        ];

    /// <summary>Gets the recognized built-in module names that are not built yet, as theory data.</summary>
    public static TheoryData<string> UnsupportedModules
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in ScadBuiltinModules.Names.Except(Implemented).Order(StringComparer.Ordinal))
            {
                data.Add(name);
            }

            return data;
        }
    }

    /// <summary>
    ///     Validates that an empty document builds an empty scene.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_EmptyDocument_GivesEmptyScene()
    {
        // Arrange / Act
        var result = Build(string.Empty);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.IsType<GroupNode>(result.Scene.Root);
        Assert.True(result.Scene.ComputeBounds().IsEmpty);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a single solid is the scene root itself, without a wrapper group.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_SingleSolid_IsTheRootNode()
    {
        // Arrange / Act
        var result = Build("cube(1);");

        // Assert
        Assert.IsType<MeshNode>(result.Scene.Root);
    }

    /// <summary>
    ///     Validates that user modules, <c>for</c>, <c>union</c> and <c>render</c> flatten into the implicit union
    ///     in document order.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_GroupsAndUnions_FlattenInDocumentOrder()
    {
        // Arrange / Act
        var result = Build("module m() { cube(1); cube(2); }\nunion() { m(); for (i = [3:4]) cube(i); }\nrender() cube(5);");

        // Assert
        Assert.Equal([1.0, 8.0, 27.0, 64.0, 125.0], result.Meshes.Select(m => m.Mesh.SignedVolume).ToArray(), new Tolerance());
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a chain of single-child modules adds no wrapper nodes to the scene.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_SingleChildChain_AddsNoWrappers()
    {
        // Arrange / Act
        var result = Build("module a() b();\nmodule b() c();\nmodule c() cube(1);\na();");

        // Assert
        Assert.IsType<MeshNode>(result.Scene.Root);
        Assert.Equal(1, Depth(result.Scene));
    }

    /// <summary>
    ///     Validates that <c>!</c> makes the marked subtree the whole model, without the ancestors that surround it
    ///     (here a module that is not built and would otherwise be reported).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_ShowOnly_ReplacesModelWithoutAncestors()
    {
        // Arrange / Act
        var result = Build("cube(1);\ntranslate([5, 0, 0]) { cube(3); ! cube(2); }");

        // Assert
        Assert.Equal(8.0, result.SingleMesh.SignedVolume, 1e-12);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that <c>!</c> makes the marked subtree the new root of the design: it is rendered without the
    ///     transforms and colors of its ancestors, so <c>translate([10, 0, 0]) { !cube(1); }</c> renders the cube at
    ///     the origin, and a mark under a color and a rotation is neither colored nor rotated.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_ShowOnly_IsTheNewRootWithoutAncestorTransformsOrColors()
    {
        // Arrange / Act
        var translated = Build("translate([10, 0, 0]) { !cube(1); }");
        var nested = Build("color(\"red\") rotate([0, 0, 90]) translate([10, 0, 0]) { cube(5); !cube(1); }");

        // Assert
        var cube = Assert.Single(translated.Leaves);
        AssertBounds(cube.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 1, 1), 1e-12);
        var inner = Assert.Single(nested.Leaves);
        AssertBounds(inner.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 1, 1), 1e-12);
        Assert.Null(inner.Appearance);
    }

    /// <summary>
    ///     Validates that several <c>!</c> marks are combined in document order and that a mark inside a marked
    ///     subtree has no extra effect.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_SeveralShowOnly_UnionInOrderAndNestedMarkIgnored()
    {
        // Arrange / Act
        var result = Build("! cube(1);\ncube(5);\n! union() { ! cube(2); cube(3); }");

        // Assert
        Assert.Equal([1.0, 8.0, 27.0], result.Meshes.Select(m => m.Mesh.SignedVolume).ToArray(), new Tolerance());
    }

    /// <summary>
    ///     Validates that a <c>!</c> inside a <c>%</c> subtree does not take part, so the model stays the union of
    ///     the ordinary instances (plus the ghost), and that a subtree marked both <c>!</c> and <c>%</c> is the whole
    ///     model as a ghost only: no ordinary geometry and no report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_ShowOnlyInsideBackground_IsIgnored()
    {
        // Arrange / Act
        var inside = Build("% { ! cube(1); cube(3); }\ncube(2);");
        var both = Build("!% cube(1);\ncube(2);");

        // Assert
        Assert.Equal(8.0, inside.SingleMesh.SignedVolume, 1e-12);
        Assert.Empty(inside.Features);
        Assert.Empty(both.Meshes);
        Assert.IsType<OverlayNode>(both.Scene.Root);
        Assert.Empty(both.Features);
    }

    /// <summary>
    ///     Validates that a <c>%</c> block around a single marked instance OR-s both modifiers onto that instance (as the
    ///     evaluator does), so the instance is both the <c>!</c> root and a ghost: nothing ordinary is built and the
    ///     model is the ghost, without a report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_BackgroundBlockAroundSingleShowOnly_BuildsOnlyTheGhost()
    {
        // Arrange / Act
        var result = Build("% { ! cube(1); }\ncube(2);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.IsType<OverlayNode>(result.Scene.Root);
        Assert.Empty(result.Features);
    }

    /// <summary>
    ///     Validates that <c>#</c> renders normally, adds an overlay, and reports nothing for a solid.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_Highlight_RendersNormallyWithOverlayAndNoReport()
    {
        // Arrange / Act
        var result = Build("#cube(1);");

        // Assert
        Assert.Equal(1.0, result.SingleMesh.SignedVolume, 1e-12);
        Assert.IsType<GroupNode>(result.Scene.Root);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that <c>%</c> draws the subtree as a ghost instead of building it as geometry (the ghost is not
    ///     part of the model) and reports nothing for a solid.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_Background_DrawsGhostInsteadOfTheSubtreeAndNoReport()
    {
        // Arrange / Act
        var result = Build("% union() { cube(1); translate([1, 0, 0]) cube(1); }\ncube(2);");

        // Assert: one ordinary cube, plus a ghost group of the other two
        Assert.Equal(8.0, result.SingleMesh.SignedVolume, 1e-12);
        var group = Assert.IsType<GroupNode>(result.Scene.Root);
        Assert.Contains(group.Children, child => child is OverlayNode);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that <c>*</c> removes the instance without any report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_Disable_IsAbsentAndSilent()
    {
        // Arrange / Act
        var result = Build("*cube(1);\ncube(2);");

        // Assert
        Assert.Equal(8.0, result.SingleMesh.SignedVolume, 1e-12);
        Assert.Empty(result.Unsupported);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that every recognized module that is not built yet is reported as
    ///     <c>scad-module-{name}</c> at its own location, builds nothing, and does not visit its children.
    /// </summary>
    /// <param name="name">The module name.</param>
    [Theory]
    [MemberData(nameof(UnsupportedModules))]
    public void ScadGeometryBuilder_Build_UnsupportedModule_ReportsFeatureWithLocation(string name)
    {
        // Arrange / Act
        var result = Build($"\n  {name}() cube(1);");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal($"scad-module-{name}", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Contains($"'{name}'", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a user module with the name of a built-in shadows it, so nothing is reported.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_UserModuleShadowingBuiltin_IsBuiltNotReported()
    {
        // Arrange / Act
        var result = Build("module text() cube(1);\ntext();");

        // Assert
        Assert.Equal(1.0, result.SingleMesh.SignedVolume, 1e-12);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that the same unsupported module at the same location is reported once even when a loop
    ///     instantiates it repeatedly.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_RepeatedUnsupportedInstance_ReportedOnce()
    {
        // Arrange / Act
        var result = Build("for (i = [1:3]) roof() cube(1);");

        // Assert
        Assert.Single(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a report carries the file of an included or used source.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_UnsupportedInUsedFile_CarriesFileLocation()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "\nmodule m() { roof() cube(1); }\n"));

        // Act
        var result = Build("use <lib.scad>\nm();", files: files);

        // Assert
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(new ScadSourceLocation("lib.scad", 2, 14), report.Location);
    }

    /// <summary>
    ///     Validates that strict mode throws at the first unsupported feature with its feature and location, and
    ///     that the sink still holds that report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_StrictMode_ThrowsAtFirstUnsupported()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => Build("cube(1);\n  roof() cube(1);\nsurface(\"f.dat\");", Settings(strict: true), sink: sink));

        // Assert
        Assert.Equal("scad-module-roof", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
        Assert.Equal(["scad-module-roof"], sink.UnsupportedFeatures.Select(u => u.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that strict mode also throws for the degraded modifiers (a <c>#</c> on a 2D shape that is not
    ///     extruded).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_StrictMode_ThrowsForHighlightOnAFlatShape()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build("#square(1);", Settings(strict: true)));

        // Assert
        Assert.Equal("scad-modifier-highlight", ex.Feature);
    }

    /// <summary>
    ///     Validates that evaluation reports and geometry reports share one ordered sink: a warning of the
    ///     evaluator precedes the geometry's unsupported report.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_SharedSink_KeepsEvaluationBeforeGeometryEvents()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var order = new List<string>();

        // Act
        Build("assert(true);\necho(1);\nroof() cube(1);\ncube(1, bogus = 1);", sink: sink);
        sink.Deliver(e => order.Add("echo"), w => order.Add("warn"), u => order.Add("unsupported"));

        // Assert
        Assert.Equal(["echo", "unsupported", "warn"], order);
    }

    /// <summary>
    ///     Compares doubles with a small absolute tolerance.
    /// </summary>
    private sealed class Tolerance : IEqualityComparer<double>
    {
        /// <inheritdoc />
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        /// <inheritdoc />
        public int GetHashCode(double obj) => 0;
    }
}
