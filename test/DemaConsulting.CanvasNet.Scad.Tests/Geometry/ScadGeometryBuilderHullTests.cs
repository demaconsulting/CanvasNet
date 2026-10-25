// cspell:ignore polyhedron minkowski extrude
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the <c>hull</c> module of <see cref="ScadGeometryBuilder"/> (<c>ScadHulls</c>): the hull of 3D and 2D
///     children, the reports for operands that have no hull, and the triangle budget, cancellation, determinism and
///     stack behavior that <c>hull</c> and <c>minkowski</c> share.
/// </summary>
public class ScadGeometryBuilderHullTests
{
    /// <summary>The fragment settings of the hand-made instances.</summary>
    private static readonly ScadFragmentSettings Defaults = new(0, 12, 2);

    /// <summary>A single triangle: three coplanar points that span no volume.</summary>
    private const string FlatTriangle = "polyhedron([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [[0, 1, 2]])";

    /// <summary>
    ///     Validates that the hull of two separated cubes is the convex box that spans both, with the analytic volume
    ///     and bounds, as one closed mesh at the root of the scene.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_TwoSeparatedCubes_IsTheConvexSolidSpanningBoth()
    {
        // Arrange / Act
        var result = Build("hull() { cube(1); translate([10, 0, 0]) cube(1); }");

        // Assert
        Assert.IsType<MeshNode>(result.Scene.Root);
        var mesh = result.SingleMesh;
        AssertSolidVolume(mesh, 11.0);
        AssertBounds(mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(11, 1, 1));
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
        Assert.Equal(12, mesh.TriangleCount);
    }

    /// <summary>
    ///     Validates that the children are hulled in their transformed positions: translations, a rotation about Z
    ///     and a scale all move the points the hull is taken over.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_ChildrenAreTransformedBeforeTheHull()
    {
        // Arrange / Act: x 5..6, a 2 x 1 x 1 box turned a quarter turn to x -1..0 and y 0..2, and a box 3 tall
        var result = Build("hull() { translate([5, 0, 0]) cube(1); rotate([0, 0, 90]) cube([2, 1, 1]); scale([1, 1, 3]) cube(1); }");

        // Assert
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.True(mesh.SignedVolume > 0.0);
        AssertBounds(mesh.Bounds, new Vector3D(-1, 0, 0), new Vector3D(6, 2, 3));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that mirrored (inside-out) and rotated children still give a closed, outward-wound hull with a
    ///     positive volume.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_MirroredAndRotatedChildren_GiveAPositiveVolumeMesh()
    {
        // Arrange / Act
        var result = Build("hull() { mirror([1, 0, 0]) cube(1); translate([3, 0, 0]) rotate([10, 20, 30]) cube(1); mirror([0, 0, 1]) translate([1, 1, 0]) cube(2); }");

        // Assert
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        Assert.True(mesh.SignedVolume > 0.0);
        Assert.Equal(-1.0, mesh.Bounds.Min.X, 1e-9);
        Assert.Equal(-2.0, mesh.Bounds.Min.Z, 1e-9);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Features);
    }

    /// <summary>
    ///     Validates that the children of a group, a loop and a user module are one union: the hull equals the hull
    ///     written with the cubes as direct children, byte for byte.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_UnionOfChildrenViaGroupsAndUserModules_IsOneHull()
    {
        // Arrange / Act
        var grouped = Build("module two() { cube(1); translate([4, 0, 0]) cube(1); }\nhull() { two(); for (i = [0 : 1]) translate([0, i * 3, 0]) cube(1); }");
        var direct = Build("hull() { cube(1); translate([4, 0, 0]) cube(1); translate([0, 0, 0]) cube(1); translate([0, 3, 0]) cube(1); }");
        var nested = Build("hull() union() { translate([0, 0, 0]) { cube(1); translate([4, 0, 0]) cube(1); } translate([0, 3, 0]) cube(1); }");

        // Assert
        AssertBounds(grouped.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(5, 4, 1));
        AssertSameMesh(direct.SingleMesh, grouped.SingleMesh);
        AssertSameMesh(direct.SingleMesh, nested.SingleMesh);
    }

    /// <summary>
    ///     Validates that the hull of 2D children is a convex region that an extrusion consumes: the extrusion has the
    ///     analytic volume of the hull's area times the height.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_TwoDimensionalChildren_ProduceAConvexRegionThatExtrudes()
    {
        // Arrange / Act: two squares make an 8 x 2 rectangle; a triangle with a square inside it stays the triangle
        var rectangle = Build("linear_extrude(3) hull() { square(2); translate([6, 0]) square(2); }");
        var triangle = Build("linear_extrude(2) hull() { polygon([[0, 0], [4, 0], [0, 4]]); translate([1, 1]) square(0.5); }");

        // Assert
        AssertSolidVolume(rectangle.SingleMesh, 8.0 * 2.0 * 3.0);
        AssertBounds(rectangle.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(8, 2, 3));
        AssertSolidVolume(triangle.SingleMesh, 8.0 * 2.0);
        AssertBounds(triangle.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(4, 4, 2));
        Assert.Empty(rectangle.Warnings);
        Assert.Empty(triangle.Features);
    }

    /// <summary>
    ///     Validates that planar transforms of the 2D children are applied and that a color inside a 2D or a 3D hull
    ///     is dropped with a warning at the hull.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_TwoDimensionalTransformsAndColors_AreApplied()
    {
        // Arrange / Act
        var flat = Build("linear_extrude(1) hull() { translate([10, 0]) square(2); rotate(90) square([4, 1]); color(\"red\") translate([0, 10]) square(1); }");
        var solid = Build("hull() { color(\"red\") cube(1); translate([3, 0, 0]) cube(1); }");

        // Assert: the square at x 10..12, the turned rectangle at x -1..0, y 0..4 and the red square at y 10..11
        AssertBounds(flat.SingleMesh.Bounds, new Vector3D(-1, 0, 0), new Vector3D(12, 11, 1));
        var flatWarning = Assert.Single(flat.Warnings);
        Assert.Contains("colors inside hull()", flatWarning.Message, StringComparison.Ordinal);
        Assert.Equal(new ScadSourceLocation(null, 1, 19), flatWarning.Location);
        Assert.Null(Assert.Single(flat.Leaves).Appearance);

        // The 3D hull drops the color of its child as well
        AssertBounds(solid.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(4, 1, 1));
        var solidWarning = Assert.Single(solid.Warnings);
        Assert.Contains("colors inside hull()", solidWarning.Message, StringComparison.Ordinal);
        Assert.Null(Assert.Single(solid.Leaves).Appearance);
    }

    /// <summary>
    ///     Validates that a colored lone 3D hull child is warned about and dropped while the hull preserves its
    ///     transformed geometry.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_LoneColoredChild_WarnsAndDropsAppearance()
    {
        // Arrange / Act
        var result = Build("hull() color(\"red\") translate([2, 3, 4]) cube(1);");

        // Assert: the hull warning is retained, but its newly created mesh is uncolored at the child's position
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("colors inside hull()", warning.Message, StringComparison.Ordinal);
        var leaf = Assert.Single(result.Leaves);
        Assert.Null(leaf.Appearance);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(2, 3, 4), new Vector3D(3, 4, 5));
        AssertSolidVolume(leaf.WorldMesh, 1.0);
    }

    /// <summary>
    ///     Validates that an enclosing color applies to a lone hull after the child's color has been dropped.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_EnclosingColorAroundLoneColoredChild_UsesEnclosingAppearance()
    {
        // Arrange / Act
        var result = Build("color(\"blue\") hull() color(\"red\") cube(1);");

        // Assert: the child warning remains, while the enclosing color is the only appearance
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("colors inside hull()", warning.Message, StringComparison.Ordinal);
        var leaf = Assert.Single(result.Leaves);
        Assert.Equal(new NodeAppearance(new Rgba32(0, 0, 255, 255)), leaf.Appearance);
    }

    /// <summary>
    ///     Validates that a hull of 2D and 3D children is reported at the hull, builds nothing, and leaves its siblings.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_MixedDimensions_ReportsAndOmits()
    {
        // Arrange / Act
        var result = Build("cube(2);\n  hull() { square(1); cube(1); }");

        // Assert: only the sibling cube is built
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-hull-mixed-dimensions", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Contains("hull", report.Message, StringComparison.Ordinal);
        AssertSolidVolume(result.SingleMesh, 8.0);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a difference or an intersection below a hull, in 3D or 2D, directly or under other
    ///     modules, is reported and omitted, while a union of the same operands is hulled.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_DifferenceOrIntersectionChild_ReportsAndOmits()
    {
        // Arrange
        string[] reported =
        [
            "hull() difference() { cube(2); cube(1); }",
            "hull() intersection() { cube(2); translate([1, 1, 1]) cube(2); }",
            "hull() { cube(1); translate([4, 0, 0]) union() { cube(1); difference() { cube(3); cube(2); } } }",
            "linear_extrude(1) hull() difference() { square(2); square(1); }",
            "linear_extrude(1) hull() { square(1); translate([5, 0]) intersection() { square(2); translate([1, 1]) square(2); } }",
        ];

        // Act / Assert
        foreach (var source in reported)
        {
            var result = Build(source);
            var report = Assert.Single(result.Unsupported);
            Assert.Equal("scad-hull-csg-operand", report.Feature);
            Assert.Empty(result.Meshes);
        }

        // The union of the same operands has a hull
        var union = Build("hull() union() { cube(2); translate([1, 1, 1]) cube(2); }");
        AssertBounds(union.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(3, 3, 3));
        Assert.Empty(union.Features);
    }

    /// <summary>
    ///     Validates that points that span no volume are reported as degenerate: a single triangle and two coplanar
    ///     triangles.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_FlatCollinearOrSinglePointChildren_ReportsDegenerate()
    {
        // Arrange / Act
        var single = Build($"\n  hull() {FlatTriangle};");
        var coplanar = Build($"hull() {{ {FlatTriangle}; translate([2, 2, 0]) {FlatTriangle}; }}");

        // Assert
        var report = Assert.Single(single.Unsupported);
        Assert.Equal("scad-hull-degenerate", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(single.Meshes);
        Assert.Equal(["scad-hull-degenerate"], coplanar.Features);
        Assert.Empty(coplanar.Meshes);
    }

    /// <summary>
    ///     Validates that a hull without children, or whose children are all empty (here an invalid cube that warns
    ///     by itself), yields nothing and reports nothing of its own.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Hull_NoChildrenOrEmptyChildren_YieldsNothingWithoutAReport()
    {
        // Arrange / Act
        var none = Build("hull();\nhull() { }");
        var empty = Build("hull() cube(0);\nminkowski() { cube(0); }\nminkowski();");

        // Assert
        Assert.Empty(none.Meshes);
        Assert.Empty(none.Features);
        Assert.Empty(none.Warnings);
        Assert.Empty(empty.Meshes);
        Assert.Empty(empty.Features);
        Assert.Equal(2, empty.Warnings.Count);
    }

    /// <summary>
    ///     Validates that the number charged against the budget equals the triangles of every mesh the build created
    ///     (the operands that hull and minkowski consume stay charged, the temporary sums of a fold do not), for the
    ///     successful paths and for the reported ones that omit their result.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="operands">The program of the operands alone, built to know what they cost.</param>
    [Theory]
    [InlineData("hull() { cube(1); translate([3, 0, 0]) cube(1); }", "cube(1); translate([3, 0, 0]) cube(1);")]
    [InlineData("hull() { sphere(2, $fn = 16); translate([5, 0, 0]) sphere(1, $fn = 9); }", "sphere(2, $fn = 16); translate([5, 0, 0]) sphere(1, $fn = 9);")]
    [InlineData("linear_extrude(1) hull() { square(1); translate([3, 3]) circle(1, $fn = 7); }", "")]
    [InlineData("minkowski() { cube(2); sphere(1, $fn = 12); }", "cube(2); sphere(1, $fn = 12);")]
    [InlineData("minkowski() { union() { cube(1); translate([4, 0, 0]) cube(1); } cube(1); }", "cube(1); translate([4, 0, 0]) cube(1); cube(1);")]
    [InlineData("minkowski() { cube(1); cube(1); cube(1); }", "cube(1); cube(1); cube(1);")]
    [InlineData("minkowski() { difference() { cube(2); cube(1); } cube(1); }", "difference() { cube(2); cube(1); } cube(1);")]
    [InlineData("hull() difference() { cube(2); cube(1); }", "difference() { cube(2); cube(1); }")]
    [InlineData("hull() polyhedron([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [[0, 1, 2]]);", "polyhedron([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [[0, 1, 2]]);")]
    [InlineData(
        "minkowski() { cube(1); for (i = [0 : 63]) translate([i * 3, 0, 0]) cube(1); for (j = [0 : 64]) translate([0, j * 3, 0]) cube(1); }",
        "cube(1); for (i = [0 : 63]) translate([i * 3, 0, 0]) cube(1); for (j = [0 : 64]) translate([0, j * 3, 0]) cube(1);")]
    public void ScadGeometryBuilder_HullAndMinkowski_ChargeExactlyTheTrianglesCreated(string source, string operands)
    {
        // Arrange / Act
        var result = Build(source);
        var consumed = Build(operands);

        // Assert: the charge is what the operands cost plus the meshes of the result
        Assert.Equal(consumed.TrianglesCharged + result.MeshTriangles, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that a hull and a sum over the budget fail with the located limit error, that the exact count is
    ///     the boundary (the limit equal to the charge accepts and one below rejects), and that failing leaves no charge
    ///     behind to fail a later build.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_OverTheBudget_ThrowsLocatedEvaluationExceptionWithoutLeakingCharge()
    {
        // Arrange: the two cubes cost 24 triangles and the box over them 12
        const string Hull = "\n  hull() { cube(1); translate([3, 0, 0]) cube(1); }";
        const string Sum = "\n  minkowski() { cube(1); cube(1); }";

        // Act
        var hullFits = Build(Hull, Settings(maxTriangles: 36));
        var sumFits = Build(Sum, Settings(maxTriangles: 36));
        var hullFails = Assert.Throws<ScadEvaluationException>(() => Build(Hull, Settings(maxTriangles: 35)));
        var sumFails = Assert.Throws<ScadEvaluationException>(() => Build(Sum, Settings(maxTriangles: 35)));

        // Assert
        Assert.Equal(36, hullFits.TrianglesCharged);
        Assert.Equal(36, sumFits.TrianglesCharged);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), hullFails.Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), sumFails.Location);
        Assert.Equal("2:3: Triangle limit exceeded (limit 35)", hullFails.Message);
        Assert.Equal("2:3: Triangle limit exceeded (limit 35)", sumFails.Message);
    }

    /// <summary>
    ///     Validates that a canceled token stops a hull and a sum: through the whole build, and directly in the
    ///     builders (which poll before the heavy work even when their children cost no polls).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_Canceled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = new ScadGeometryContext(Settings(cancellation: cts.Token), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
        var first = ScadGeo.FromMesh(new MeshNode(PrimitiveTessellator.Box(new Vector3D(1, 1, 1))));
        var second = ScadGeo.FromMesh(new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2))));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => Build("hull() { cube(1); translate([3, 0, 0]) cube(1); }", Settings(cancellation: cts.Token)));
        Assert.Throws<OperationCanceledException>(() => Build("minkowski() { cube(1); cube(1); }", Settings(cancellation: cts.Token)));
        Assert.Throws<OperationCanceledException>(() => ScadHulls.Hull(Instance("hull", []), [first, second], context));
        Assert.Throws<OperationCanceledException>(() => ScadHulls.Minkowski(Instance("minkowski", []), [first, second], context));
    }

    /// <summary>
    ///     Validates that building the same model twice gives identical meshes, vertex for vertex and index for index.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_SameModelTwice_BuildsIdenticalMeshes()
    {
        // Arrange
        const string Source =
            "hull() { sphere(2, $fn = 12); translate([5, 1, 2]) rotate([10, 20, 30]) cube(2); }\n"
            + "translate([20, 0, 0]) minkowski() { union() { cube(2); translate([5, 0, 0]) cube([1, 2, 3]); } sphere(1, $fn = 10); }\n"
            + "translate([40, 0, 0]) linear_extrude(2) hull() { circle(2, $fn = 9); translate([5, 1]) square(1); }";

        // Act
        var first = Build(Source);
        var second = Build(Source);

        // Assert
        Assert.Equal(first.Meshes.Count, second.Meshes.Count);
        Assert.Equal(4, first.Meshes.Count);
        for (var i = 0; i < first.Meshes.Count; i++)
        {
            AssertSameMesh(first.Meshes[i].Mesh, second.Meshes[i].Mesh);
        }

        Assert.Equal(first.TrianglesCharged, second.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that hulls and sums nested thousands of levels deep build on a 64 KiB caller stack exactly as on
    ///     a big one (the engine and the point walks are iterative).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_DeeplyNestedInstances_BuildOnASmallStack()
    {
        // Arrange / Act
        var big = OnSmallStack(() => BuildNested("hull", 3000), 256 * 1024 * 1024);
        var small = OnSmallStack(() => BuildNested("hull", 3000), 64 * 1024);
        var bigSum = OnSmallStack(() => BuildNested("minkowski", 800), 256 * 1024 * 1024);
        var smallSum = OnSmallStack(() => BuildNested("minkowski", 800), 64 * 1024);

        // Assert: the nesting does not change the solid
        Assert.Equal(big, small);
        Assert.Equal(bigSum, smallSum);
        Assert.Equal(1.0, big.Volume, 1e-9);
        Assert.Equal(1.0, bigSum.Volume, 1e-9);
        Assert.Equal(1, big.Meshes);
        Assert.Equal(1, bigSum.Meshes);
    }

    /// <summary>
    ///     Builds a hand-made chain of single-child instances of a module around a unit cube.
    /// </summary>
    /// <param name="module">The module name.</param>
    /// <param name="depth">The number of nested instances.</param>
    /// <returns>The number of meshes and their total volume.</returns>
    private static (int Meshes, double Volume) BuildNested(string module, int depth)
    {
        var node = Instance("cube", [], [new ScadInstanceArgument(null, N(1))]);
        for (var i = 0; i < depth; i++)
        {
            node = Instance(module, [node]);
        }

        var built = ScadGeometryBuilder.Build([node], Settings(), new ScadEvaluationSink(ScadLimits.Default));
        var meshes = MeshNodes(built.Scene);
        return (meshes.Count, meshes.Sum(m => m.Mesh.SignedVolume));
    }

    /// <summary>
    ///     Creates a hand-made instance of a built-in module.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="children">The children.</param>
    /// <param name="arguments">The arguments, or <see langword="null"/> for none.</param>
    /// <returns>The instance.</returns>
    private static ScadInstance Instance(string name, IReadOnlyList<ScadInstance> children, IReadOnlyList<ScadInstanceArgument>? arguments = null) =>
        new(ScadInstanceKind.BuiltinModule, name, arguments ?? [], children, ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));

    /// <summary>
    ///     Asserts that two meshes have the same vertices and triangle indices in the same order.
    /// </summary>
    /// <param name="expected">The expected mesh.</param>
    /// <param name="actual">The actual mesh.</param>
    private static void AssertSameMesh(Mesh expected, Mesh actual)
    {
        Assert.Equal(expected.Vertices, actual.Vertices);
        Assert.Equal(expected.TriangleIndices, actual.TriangleIndices);
    }
}
