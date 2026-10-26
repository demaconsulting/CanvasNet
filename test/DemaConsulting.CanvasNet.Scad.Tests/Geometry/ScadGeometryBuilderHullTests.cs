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

        // The message describes the cause in plain words: no text of the Render3D exception and no doubled punctuation
        Assert.Contains("do not span a volume", report.Message, StringComparison.Ordinal);
        Assert.Contains("fewer than four distinct points, or all on one line or in one plane", report.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("degenerate input", report.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(").", report.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("..", report.Message, StringComparison.Ordinal);
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
    ///     Validates that a hull and a sum over the budget fail with the located limit error, that the upper bound
    ///     of the triangles (2n - 4 for the 16 points of the hull, 2 * 8 * 8 - 4 for the sum) is the boundary (the
    ///     limit equal to the operands plus the bound accepts and one below rejects), and that after the mesh is
    ///     built the charge is settled to the actual twelve triangles.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_OverTheBudget_ThrowsLocatedEvaluationExceptionWithoutLeakingCharge()
    {
        // Arrange: the two cubes cost 24 triangles; the hull bound is 28 and the sum bound 124
        const string Hull = "\n  hull() { cube(1); translate([3, 0, 0]) cube(1); }";
        const string Sum = "\n  minkowski() { cube(1); cube(1); }";

        // Act
        var hullFits = Build(Hull, Settings(maxTriangles: 52));
        var sumFits = Build(Sum, Settings(maxTriangles: 148));
        var hullFails = Assert.Throws<ScadEvaluationException>(() => Build(Hull, Settings(maxTriangles: 51)));
        var sumFails = Assert.Throws<ScadEvaluationException>(() => Build(Sum, Settings(maxTriangles: 147)));

        // Assert: the charge settled to the actual twelve triangles of the box over the 24 of the cubes
        Assert.Equal(36, hullFits.TrianglesCharged);
        Assert.Equal(36, sumFits.TrianglesCharged);
        Assert.Equal(12, hullFits.MeshTriangles);
        Assert.Equal(12, sumFits.MeshTriangles);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), hullFails.Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), sumFails.Location);
        Assert.Equal("2:3: Triangle limit exceeded (limit 51)", hullFails.Message);
        Assert.Equal("2:3: Triangle limit exceeded (limit 147)", sumFails.Message);
    }

    /// <summary>
    ///     Validates the pre-charge protocol directly on a context: a hull or a sum whose upper bound does not fit the
    ///     remaining budget is rejected before its computation is started (the context counts the computations it
    ///     began), the rejection leaves no charge behind, a bound that fits is settled to the actual triangle count, a
    ///     degenerate hull refunds its whole pre-charge, and the temporary sums of a fold with several children are
    ///     pre-charged, settled and refunded so that the charge always equals the operands plus the meshes created.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_HullAndMinkowski_BoundChargedBeforeComputing_RejectsWithoutStartingAndSettlesToActual()
    {
        var location = new ScadSourceLocation(null, 1, 1);

        // Builds a context with the given limit and the charged operands: unit cubes at the given x offsets
        (ScadGeometryContext Context, ScadGeo[] Operands) Create(long limit, params double[] offsets)
        {
            var context = new ScadGeometryContext(Settings(maxTriangles: limit), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
            var operands = offsets.Select(x =>
            {
                var mesh = PrimitiveTessellator.Box(new Vector3D(1, 1, 1)).Transform(Matrix4x4D.CreateTranslation(new Vector3D(x, 0, 0)));
                context.ChargeTriangles(mesh.TriangleCount, location);
                return ScadGeo.FromMesh(context.CreateMeshNode(mesh, location));
            }).ToArray();
            return (context, operands);
        }

        // Hull: 16 points, bound 28, actual 12; the operands cost 24
        var (rejectedHull, hullOperands) = Create(51, 0, 3);
        Assert.Throws<ScadEvaluationException>(() => ScadHulls.Hull(Instance("hull", []), [.. hullOperands], rejectedHull));
        Assert.Equal(0, rejectedHull.HullsStarted);
        Assert.Equal(24, rejectedHull.TrianglesCharged);
        var (acceptedHull, acceptedHullOperands) = Create(52, 0, 3);
        var hull = ScadHulls.Hull(Instance("hull", []), [.. acceptedHullOperands], acceptedHull);
        Assert.Equal(1, acceptedHull.HullsStarted);
        Assert.Equal(12, Assert.IsType<MeshNode>(hull.Node).Mesh.TriangleCount);
        Assert.Equal(24 + 12, acceptedHull.TrianglesCharged);

        // Hull: a flat quad (four coplanar points) is degenerate; its pre-charge of four triangles is refunded
        var (degenerate, _) = Create(1000);
        var flat = new Mesh([Vector3D.Zero, Vector3D.UnitX, new Vector3D(1, 1, 0), Vector3D.UnitY], [0, 1, 2, 0, 2, 3]);
        degenerate.ChargeTriangles(flat.TriangleCount, location);
        var flatResult = ScadHulls.Hull(Instance("hull", []), [ScadGeo.FromMesh(degenerate.CreateMeshNode(flat, location))], degenerate);
        Assert.Equal(ScadGeoKind.Empty, flatResult.Kind);
        Assert.Equal(1, degenerate.HullsStarted);
        Assert.Equal(flat.TriangleCount, degenerate.TrianglesCharged);

        // Sum of two cubes: 8 * 8 points, bound 124, actual 12
        var (rejectedSum, sumOperands) = Create(147, 0, 3);
        Assert.Throws<ScadEvaluationException>(() => ScadHulls.Minkowski(Instance("minkowski", []), [.. sumOperands], rejectedSum));
        Assert.Equal(0, rejectedSum.SumsStarted);
        Assert.Equal(24, rejectedSum.TrianglesCharged);
        var (acceptedSum, acceptedSumOperands) = Create(148, 0, 3);
        var sum = ScadHulls.Minkowski(Instance("minkowski", []), [.. acceptedSumOperands], acceptedSum);
        Assert.Equal(1, acceptedSum.SumsStarted);
        Assert.Equal(12, Assert.IsType<MeshNode>(sum.Node).Mesh.TriangleCount);
        Assert.Equal(24 + 12, acceptedSum.TrianglesCharged);

        // Fold of three cubes: the second step needs the live sum of the first (12) and its own bound (124) on top of the
        // 36 of the operands
        var (rejectedFold, foldOperands) = Create(171, 0, 3, 6);
        Assert.Throws<ScadEvaluationException>(() => ScadHulls.Minkowski(Instance("minkowski", []), [.. foldOperands], rejectedFold));
        Assert.Equal(1, rejectedFold.SumsStarted);
        Assert.Equal(36, rejectedFold.TrianglesCharged);
        var (acceptedFold, acceptedFoldOperands) = Create(172, 0, 3, 6);
        var fold = ScadHulls.Minkowski(Instance("minkowski", []), [.. acceptedFoldOperands], acceptedFold);
        Assert.Equal(2, acceptedFold.SumsStarted);
        var foldMesh = Assert.IsType<MeshNode>(fold.Node).Mesh;
        Assert.Equal(12, foldMesh.TriangleCount);
        Assert.Equal(36 + foldMesh.TriangleCount, acceptedFold.TrianglesCharged);
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
    ///     Validates that the transformed copies of the convex parts of a <c>minkowski</c> are charged to the triangle
    ///     budget before they are allocated and refunded when they are consumed or the module ends: the copy of a
    ///     translated child costs its twelve triangles on top of the operands while it lives, a limit that does not
    ///     fit it fails and leaves only the operands charged, and after the fold the charge is the operands plus the
    ///     result; and that a lone child is verified as built, so it needs no copy and no charge whatever its transform
    ///     (translation, rotation, non-uniform scale, mirror) and a non-convex one is reported as built.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TransformedCopies_AreChargedWhileTheyLiveAndRefunded()
    {
        var location = new ScadSourceLocation(null, 1, 1);

        // Builds a context with the charged unit cubes; the cubes at the given x offsets are wrapped in a translation
        (ScadGeometryContext Context, ScadGeo[] Operands) Create(long limit, params double[] translations)
        {
            var context = new ScadGeometryContext(Settings(maxTriangles: limit), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
            var operands = translations.Select(x =>
            {
                var mesh = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
                context.ChargeTriangles(mesh.TriangleCount, location);
                var node = context.CreateMeshNode(mesh, location);
                return double.IsNaN(x)
                    ? ScadGeo.FromMesh(node)
                    : ScadGeo.FromSolid(new GroupNode([node]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(x, 0, 0)) }, mesh.Bounds, true, false);
            }).ToArray();
            return (context, operands);
        }

        // Fold: the operands (24), the copy of the translated child (12) and the bound of the sum (124) are alive at once
        var (tooSmall, tooSmallOperands) = Create(159, 3, double.NaN);
        Assert.Throws<ScadEvaluationException>(() => ScadHulls.Minkowski(Instance("minkowski", []), [.. tooSmallOperands], tooSmall));
        Assert.Equal(24, tooSmall.TrianglesCharged);
        var (copyDoesNotFit, copyDoesNotFitOperands) = Create(35, 3, double.NaN);
        Assert.Throws<ScadEvaluationException>(() => ScadHulls.Minkowski(Instance("minkowski", []), [.. copyDoesNotFitOperands], copyDoesNotFit));
        Assert.Equal(0, copyDoesNotFit.SumsStarted);
        Assert.Equal(24, copyDoesNotFit.TrianglesCharged);
        var (fits, fitsOperands) = Create(160, 3, double.NaN);
        var sum = ScadHulls.Minkowski(Instance("minkowski", []), [.. fitsOperands], fits);
        Assert.Equal(1, fits.SumsStarted);
        Assert.Equal(24 + Assert.IsType<MeshNode>(sum.Node).Mesh.TriangleCount, fits.TrianglesCharged);

        // Lone child: it is verified as built, so no copy is made or charged and a limit equal to the child's own
        // triangles is enough, whatever the transform (translation, rotation, non-uniform scale or mirror)
        Matrix4x4D[] transforms =
        [
            Matrix4x4D.CreateTranslation(new Vector3D(3, 0, 0)),
            Matrix4x4D.CreateRotationZ(30.0),
            Matrix4x4D.CreateScale(new Vector3D(1000, 1, 0.01)),
            Matrix4x4D.CreateMirror(new Vector3D(1, 0, 0)),
            Matrix4x4D.CreateScale(new Vector3D(-2, 3, 4)),
        ];
        foreach (var transform in transforms)
        {
            var lone = new ScadGeometryContext(Settings(maxTriangles: 12), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
            var cube = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
            lone.ChargeTriangles(cube.TriangleCount, location);
            var node = lone.CreateMeshNode(cube, location);
            var child = ScadGeo.FromSolid(new GroupNode([node]) { Transform = transform }, cube.Bounds, true, false);

            var loneResult = ScadHulls.Minkowski(Instance("minkowski", []), [child], lone);

            Assert.Equal(ScadGeoKind.Solid, loneResult.Kind);
            Assert.Same(child.Node, loneResult.Node);
            Assert.Equal(12, lone.TrianglesCharged);
        }

        // A lone child that is not convex is still reported, as built, and nothing stays charged
        var dented = new ScadGeometryContext(Settings(maxTriangles: 12), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
        var notConvex = Extruder.Linear(PrimitiveTessellator.Polygon([new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)]), 1.0);
        var notConvexNode = dented.CreateMeshNode(notConvex, location);
        var notConvexResult = ScadHulls.Minkowski(
            Instance("minkowski", []),
            [ScadGeo.FromSolid(new GroupNode([notConvexNode]) { Transform = Matrix4x4D.CreateRotationZ(30.0) }, notConvex.Bounds, true, false)],
            dented);
        Assert.Equal(ScadGeoKind.Empty, notConvexResult.Kind);
        Assert.Equal(0, dented.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that the distinct vertices of each convex part are counted once and reused by every pair: two
    ///     children of three parts each give nine sums with six scans (not a scan per pair), the pair cost is the
    ///     distinct count (an unwelded cube costs like a welded one, so the bound of its sum is the same), and the
    ///     results are those of the welded operands.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_DistinctVertexCounts_AreScannedOncePerMemberAndReused()
    {
        var location = new ScadSourceLocation(null, 1, 1);
        var context = new ScadGeometryContext(Settings(), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));

        // A child of three separate cubes (a union of three parts)
        ScadGeo Union(double offset)
        {
            var nodes = Enumerable.Range(0, 3).Select(i =>
            {
                var mesh = PrimitiveTessellator.Box(new Vector3D(1, 1, 1)).Transform(Matrix4x4D.CreateTranslation(new Vector3D(offset + (i * 4.0), 0, 0)));
                context.ChargeTriangles(mesh.TriangleCount, location);
                return (SceneNode)context.CreateMeshNode(mesh, location);
            }).ToList();
            return ScadGeo.FromSolid(new GroupNode(nodes), BoundingBox3D.Empty, true, false);
        }

        // Act
        var result = ScadHulls.Minkowski(Instance("minkowski", []), [Union(0), Union(20)], context);

        // Assert: 3 x 3 sums, each member scanned once (6 scans), and the nine boxes are those of the welded cubes
        Assert.Equal(9, context.SumsStarted);
        Assert.Equal(6, context.DistinctScans);
        var group = Assert.IsType<GroupNode>(result.Node);
        Assert.Equal(9, group.Children.Count);
        Assert.All(group.Children, c => Assert.Equal(12, Assert.IsType<MeshNode>(c).Mesh.TriangleCount));

        // Arrange: an unwelded cube (36 vertices, 8 distinct) and a cube; the pair costs 8 * 8, so the bound is 124
        var cube = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
        var unwelded = new Mesh(cube.TriangleIndices.Select(i => cube.Vertices[i]), Enumerable.Range(0, cube.TriangleIndices.Count));
        Assert.Equal(36, unwelded.VertexCount);
        var tight = new ScadGeometryContext(Settings(maxTriangles: 24 + 124), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
        tight.ChargeTriangles(12, location);
        tight.ChargeTriangles(12, location);
        var first = ScadGeo.FromMesh(tight.CreateMeshNode(unwelded, location));
        var second = ScadGeo.FromMesh(tight.CreateMeshNode(cube, location));

        // Act
        var tightResult = ScadHulls.Minkowski(Instance("minkowski", []), [first, second], tight);

        // Assert: accepted at the boundary of the distinct counts and settled to the twelve triangles of the box
        Assert.Equal(12, Assert.IsType<MeshNode>(tightResult.Node).Mesh.TriangleCount);
        Assert.Equal(24 + 12, tight.TrianglesCharged);
        Assert.Equal(2, tight.DistinctScans);
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
