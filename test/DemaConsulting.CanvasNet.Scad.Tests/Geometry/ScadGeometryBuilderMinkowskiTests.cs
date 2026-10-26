// cspell:ignore minkowski Minkowski polyhedron
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the <c>minkowski</c> module of <see cref="ScadGeometryBuilder"/> (<c>ScadHulls</c>): the sum of convex
///     children, its distribution over unions, and the reports for operands that are not convex, that are 2D or of
///     mixed dimension, or that need more work than the documented bounds.
/// </summary>
public class ScadGeometryBuilderMinkowskiTests
{
    /// <summary>
    ///     Validates that a block summed with a ball is the rounded block: a closed mesh whose bounds are the block's
    ///     grown by the ball's extent and whose volume lies between the block with its faces added and the exact
    ///     rounded block.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_CubeAndSphere_IsTheRoundedBox()
    {
        // Arrange: the ball of 16 fragments has vertices on every axis, so it reaches its full radius
        const double reach = 1.0;

        // Act
        var result = Build("minkowski() { cube([10, 6, 4]); sphere(r = 1, $fn = 16); }");

        // Assert: the volume of the exact rounded block is 240 + 248 + 20 * pi + 4 / 3 * pi
        var mesh = result.SingleMesh;
        Assert.True(mesh.IsClosed);
        AssertBounds(mesh.Bounds, new Vector3D(-reach, -reach, -reach), new Vector3D(10 + reach, 6 + reach, 4 + reach), 1e-9);
        Assert.InRange(mesh.SignedVolume, 240.0 + 248.0, 240.0 + 248.0 + (20.0 * Math.PI) + (4.0 / 3.0 * Math.PI));
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the children are summed in their transformed positions: the sum of two boxes is a box whose
    ///     size is the sum of their sizes.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TransformedConvexChildren_AreSummed()
    {
        // Arrange / Act: a box 2 x 2 x 2 at x 10 and a box 2 x 1 x 3 turned a quarter turn (x -2..0, y 1..2)
        var result = Build("minkowski() { translate([10, 0, 0]) cube(2); rotate([0, 0, 90]) translate([1, 0, 0]) cube([1, 2, 3]); }");

        // Assert: 4 x 3 x 5
        var mesh = result.SingleMesh;
        AssertSolidVolume(mesh, 60.0);
        AssertBounds(mesh.Bounds, new Vector3D(8, 1, 0), new Vector3D(12, 4, 5));
        Assert.Empty(result.Features);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that mirrored and non-uniformly scaled children are verified as built and summed in the frame of
    ///     the module: a mirrored unit cube and a 2 x 2 x 2 box (scaled by two along x) sum to the 3 x 3 x 3 box, as a lone mirrored,
    ///     scaled or rotated child is itself.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_MirroredAndScaledChildren_AreVerifiedAsBuiltAndSummedInFrame()
    {
        // Arrange / Act
        var sum = Build("minkowski() { mirror([1, 0, 0]) cube(1); scale([2, 1, 1]) cube([1, 2, 2]); }");
        var lone = Build("minkowski() mirror([0, 1, 0]) scale([3, 1, 1]) rotate([0, 0, 20]) cube(1);");

        // Assert: x -1..2, y 0..3, z 0..3 for the sum (volume 27), nothing reported
        var mesh = sum.SingleMesh;
        AssertSolidVolume(mesh, 27.0);
        AssertBounds(mesh.Bounds, new Vector3D(-1, 0, 0), new Vector3D(2, 3, 3));
        Assert.Empty(sum.Features);
        Assert.Empty(sum.Warnings);
        AssertSolidVolume(lone.WorldMesh, 3.0);
        Assert.Empty(lone.Features);
        Assert.Equal(lone.MeshTriangles, lone.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that the sum distributes over a union: a union of two cubes summed with a cube is the union of
    ///     two boxes, and a union on each side gives the four combinations.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_UnionOfConvexChildren_Distributes()
    {
        // Arrange / Act
        var one = Build("minkowski() { union() { cube(1); translate([4, 0, 0]) cube(1); } cube(2); }");
        var other = Build("minkowski() { cube(2); for (i = [0 : 1]) translate([i * 4, 0, 0]) cube(1); }");
        var four = Build("minkowski() { union() { cube(1); translate([10, 0, 0]) cube(1); } union() { cube(1); translate([0, 10, 0]) cube(1); } }");

        // Assert: two 3 x 3 x 3 blocks, at x 0..3 and x 4..7, whichever side carries the union
        Assert.Equal(2, one.Meshes.Count);
        AssertSolidVolume(one.Meshes[0].Mesh, 27.0);
        AssertSolidVolume(one.Meshes[1].Mesh, 27.0);
        AssertBounds(one.Meshes[0].Mesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(3, 3, 3));
        AssertBounds(one.Meshes[1].Mesh.Bounds, new Vector3D(4, 0, 0), new Vector3D(7, 3, 3));
        Assert.Equal(2, other.Meshes.Count);
        AssertBounds(other.Meshes[1].Mesh.Bounds, new Vector3D(4, 0, 0), new Vector3D(7, 3, 3));
        Assert.Equal(4, four.Meshes.Count);
        Assert.All(four.Meshes, m => AssertSolidVolume(m.Mesh, 8.0));
        Assert.Empty(one.Features);
        Assert.Empty(four.Features);
    }

    /// <summary>
    ///     Validates that the <c>convexity</c> argument, named or positional, changes nothing and is not validated.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_ConvexityArgumentIsIgnored()
    {
        // Arrange / Act
        var plain = Build("minkowski() { cube(1); cube(2); }");
        var named = Build("minkowski(convexity = 10) { cube(1); cube(2); }");
        var positional = Build("minkowski(\"anything\") { cube(1); cube(2); }");

        // Assert
        Assert.Equal(plain.SingleMesh.Vertices, named.SingleMesh.Vertices);
        Assert.Equal(plain.SingleMesh.TriangleIndices, positional.SingleMesh.TriangleIndices);
        Assert.Empty(named.Warnings);
        Assert.Empty(positional.Features);
    }

    /// <summary>
    ///     Validates that the output of a hull is a convex operand of a sum, and that a lone convex child is itself.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_HullChild_IsConvex()
    {
        // Arrange / Act
        var hexagon = Build("minkowski() { hull() { cube(2); translate([6, 6, 0]) cube(2); } cube(1); }");
        var capsule = Build("minkowski() { hull() { sphere(1, $fn = 12); translate([5, 0, 0]) sphere(1, $fn = 12); } cube(1); }");
        var lone = Build("minkowski() cube(2);");

        // Assert
        AssertBounds(hexagon.SingleMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(9, 9, 3));
        Assert.True(hexagon.SingleMesh.IsClosed);
        Assert.True(capsule.SingleMesh.IsClosed);
        Assert.Empty(hexagon.Features);
        Assert.Empty(capsule.Features);

        // A lone child is the child (no new mesh, nothing charged beyond the cube)
        AssertSolidVolume(lone.SingleMesh, 8.0);
        Assert.Equal(12, lone.TrianglesCharged);
        Assert.Empty(lone.Features);
    }

    /// <summary>
    ///     Validates that a colored lone child is verified without retaining its child appearance, while its transform
    ///     and geometry remain unchanged.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_LoneColoredChild_WarnsAndStripsAppearance()
    {
        // Arrange / Act
        var result = Build("minkowski() color(\"red\") translate([2, 3, 4]) cube(1);");

        // Assert: the warning is retained, but the lone result has no child color and remains translated
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("colors inside minkowski()", warning.Message, StringComparison.Ordinal);
        var leaf = Assert.Single(result.Leaves);
        Assert.Null(leaf.Appearance);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(2, 3, 4), new Vector3D(3, 4, 5));
        AssertSolidVolume(leaf.WorldMesh, 1.0);
        Assert.Equal(result.MeshTriangles, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that an enclosing color applies to the geometry after a lone colored Minkowski child has been
    ///     stripped.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_EnclosingColorAroundLoneColoredChild_UsesEnclosingAppearance()
    {
        // Arrange / Act
        var result = Build("color(\"blue\") minkowski() color(\"red\") cube(1);");

        // Assert: the child warning remains, while the enclosing color is the only appearance
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("colors inside minkowski()", warning.Message, StringComparison.Ordinal);
        var leaf = Assert.Single(result.Leaves);
        Assert.Equal(new NodeAppearance(new Rgba32(0, 0, 255, 255)), leaf.Appearance);
    }

    /// <summary>
    ///     Validates that a child that is not convex, a boolean or a mesh with a notch or a hole, is reported at the
    ///     module and the module is omitted whole, also when it is the only child.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_NonConvexChild_ReportsAndOmits()
    {
        // Arrange
        const string LShape = "linear_extrude(1) polygon([[0, 0], [3, 0], [3, 1], [1, 1], [1, 3], [0, 3]])";
        string[] sources =
        [
            $"\n  minkowski() {{ {LShape}; cube(1); }}",
            "\n  minkowski() { cube(1); difference() { cube(3); translate([1, 1, 1]) cube(2); } }",
            "\n  minkowski() { rotate_extrude($fn = 16) translate([3, 0]) square(1); sphere(1, $fn = 8); }",
            $"\n  minkowski() {LShape};",
            "\n  minkowski() difference() { cube(3); cube(2); }",
            "\n  minkowski() { cube(1); intersection() { cube(3); translate([1, 1, 1]) cube(3); } }",
        ];

        // Act / Assert
        foreach (var source in sources)
        {
            var result = Build(source);
            var report = Assert.Single(result.Unsupported);
            Assert.Equal("scad-minkowski-nonconvex", report.Feature);
            Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
            Assert.Contains("convex", report.Message, StringComparison.Ordinal);
            Assert.Empty(result.Meshes);
            Assert.Empty(result.Warnings);
        }
    }

    /// <summary>
    ///     Validates that the sum of 2D children is reported as deferred and builds nothing.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TwoDimensionalChildren_ReportsDeferred()
    {
        // Arrange / Act
        var pair = Build("\n  minkowski() { square(2); circle(1); }");
        var lone = Build("minkowski() square(1);");

        // Assert
        var report = Assert.Single(pair.Unsupported);
        Assert.Equal("scad-minkowski-2d", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(pair.Meshes);
        Assert.Equal(["scad-minkowski-2d"], lone.Features);
    }

    /// <summary>
    ///     Validates that a sum of 2D and 3D children is reported at the module and omitted, leaving its siblings.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_MixedDimensions_ReportsAndOmits()
    {
        // Arrange / Act
        var result = Build("cube(2);\n  minkowski() { cube(1); square(1); }");

        // Assert
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-minkowski-mixed-dimensions", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        AssertSolidVolume(result.SingleMesh, 8.0);
    }

    /// <summary>
    ///     Validates the work bounds: a child with more convex parts than the limit, a step with more combinations than
    ///     the limit and two meshes with more pairwise vertex sums than the limit are reported and omitted.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_TooManyPairsOrCombinations_ReportsTooComplex()
    {
        // Arrange
        string[] sources =
        [
            "minkowski() { for (i = [0 : 4096]) translate([i * 2, 0, 0]) cube(1); cube(1); }",
            "minkowski() { for (i = [0 : 64]) translate([i * 3, 0, 0]) cube(1); for (j = [0 : 64]) translate([0, j * 3, 0]) cube(1); }",
            "minkowski() { sphere(1, $fn = 100); sphere(1, $fn = 100); }",
        ];

        // Act / Assert
        foreach (var source in sources)
        {
            var result = Build(source);
            var report = Assert.Single(result.Unsupported);
            Assert.Equal("scad-minkowski-too-complex", report.Feature);
            Assert.Empty(result.Meshes);
        }
    }

    /// <summary>
    ///     Validates that the user-facing message of a non-convex child names the child by its position and source
    ///     location, in the author's terms: no parameter names or wording of the Render3D exception, and no doubled
    ///     punctuation.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_NonConvexChild_MessageNamesTheChildInUserTerms()
    {
        // Arrange
        const string LShape = "linear_extrude(1) polygon([[0, 0], [3, 0], [3, 1], [1, 1], [1, 3], [0, 3]])";

        // Act: the second of three children, a lone child, and a boolean child
        var second = Build($"minkowski() {{\n  cube(1);\n  {LShape};\n  cube(2);\n}}");
        var lone = Build($"minkowski() {LShape};");
        var boolean = Build("minkowski() {\n  cube(1);\n  difference() { cube(3); cube(2); }\n}");
        var ring = Build("minkowski() { cube(1); cube(2); rotate_extrude($fn = 16) translate([3, 0]) square(1); }");

        // Assert
        var secondReport = Assert.Single(second.Unsupported);
        Assert.Equal("scad-minkowski-nonconvex", secondReport.Feature);
        Assert.Equal(
            "minkowski: child 2 (at 3:3) is not a closed convex solid; only convex operands are supported and the minkowski is omitted",
            secondReport.Message);
        var loneReport = Assert.Single(lone.Unsupported);
        Assert.Contains("child 1 (at 1:13) is not a closed convex solid;", loneReport.Message, StringComparison.Ordinal);
        var booleanReport = Assert.Single(boolean.Unsupported);
        Assert.Equal(
            "minkowski: child 2 (at 3:3) contains a difference or an intersection, which is not provably convex; only convex operands are supported and the minkowski is omitted",
            booleanReport.Message);
        var ringReport = Assert.Single(ring.Unsupported);
        Assert.Contains("child 3 (at 1:", ringReport.Message, StringComparison.Ordinal);
        foreach (var report in new[] { secondReport, loneReport, booleanReport, ringReport })
        {
            Assert.DoesNotContain("'a'", report.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("'b'", report.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("operand '", report.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(".;", report.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("..", report.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that a child without volume (a closed, double-sided flat triangle) is rejected as geometry with a
    ///     message that names the child, not the Render3D parameter, and that the module is omitted.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_ChildWithoutVolume_IsRejectedNamingTheChild()
    {
        // Arrange / Act
        var result = Build("minkowski() {\n  cube(1);\n  polyhedron([[0, 0, 0], [1, 0, 0], [0, 1, 0]], [[0, 1, 2], [0, 2, 1]]);\n}");

        // Assert
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-geometry-rejected", report.Feature);
        Assert.Equal("minkowski: child 2 (at 3:3) has no volume; the minkowski is omitted", report.Message);
        Assert.Empty(result.Meshes);
    }

    /// <summary>
    ///     Validates that the cumulative work of one module is capped: the pair of two cylinders (1,026 and 2,042
    ///     vertices, a product just under the 2^21 bound of one pair) is within the per-pair bound, but with the cost of
    ///     verifying its members the total is over the module's cap, which is reported as too complex with the total in
    ///     the message before the sum is computed; a fold of smaller steps that stays under the cap is accepted.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_CumulativeWorkAboveTheTotalCap_ReportsTooComplex()
    {
        // Arrange / Act
        var over = Build("minkowski() { cylinder(h = 1, r = 1, $fn = 512); cylinder(h = 1, r = 1, $fn = 1020); }");
        var within = Build("minkowski() { cylinder(h = 1, r = 1, $fn = 64); cylinder(h = 1, r = 1, $fn = 64); cylinder(h = 1, r = 1, $fn = 64); }");

        // Assert: reported at the module, with the total (not the per-pair message), and omitted whole
        var report = Assert.Single(over.Unsupported);
        Assert.Equal("scad-minkowski-too-complex", report.Feature);
        Assert.Equal(
            $"minkowski: the children need more than {ScadHulls.MaxMinkowskiWork} pairwise vertex sums in total, which is above the supported work bound; the minkowski is omitted",
            report.Message);
        Assert.Empty(over.Meshes);
        Assert.Equal(2_097_152, ScadHulls.MaxMinkowskiWork);
        Assert.Empty(within.Features);
        Assert.True(within.SingleMesh.IsClosed);
    }
    /// <summary>
    ///     Validates the iterative appearance-stripping copy on a shared subtree: with the root holding [X, S], X
    ///     holding P and P holding S, the second parent to reach S used to be completed before S was copied and the
    ///     copy threw; every node is now copied once, after all its children, and S is shared by both parents of the
    ///     copy.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_StripAppearances_SharedSubtree_IsCopiedOnceAndShared()
    {
        // Arrange: S is a colored group of one mesh; the root also holds X = [P] and P = [S]
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        var mesh = PrimitiveTessellator.Box(new Vector3D(1, 1, 1));
        var leaf = new MeshNode(mesh) { Appearance = red };
        var shared = new GroupNode([leaf]) { Appearance = red, Transform = Matrix4x4D.CreateTranslation(new Vector3D(1, 0, 0)) };
        var p = new GroupNode([shared]) { Appearance = red };
        var x = new GroupNode([p]);
        var context = new ScadGeometryContext(Settings(), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));

        foreach (var children in new SceneNode[][] { [x, shared], [shared, x], [x, shared, shared] })
        {
            var root = new GroupNode(children) { Appearance = red };
            var child = ScadGeo.FromSolid(root, mesh.Bounds, true, false);

            // Act
            var stripped = ScadHulls.StripAppearances(child, context);

            // Assert: no appearance anywhere, same shape, and the shared node is one copy under both parents
            var copy = Assert.IsType<GroupNode>(stripped.Node);
            Assert.NotSame(root, copy);
            Assert.Equal(children.Length, copy.Children.Count);
            var all = Descendants(copy);
            Assert.All(all, node => Assert.Null(node.Appearance));
            var copiedX = Assert.IsType<GroupNode>(copy.Children[Array.IndexOf(children, x)]);
            var copiedP = Assert.IsType<GroupNode>(Assert.Single(copiedX.Children));
            var sharedViaP = Assert.IsType<GroupNode>(Assert.Single(copiedP.Children));
            var sharedViaRoot = Assert.IsType<GroupNode>(copy.Children[Array.IndexOf(children, shared)]);
            Assert.Same(sharedViaRoot, sharedViaP);
            Assert.NotSame(shared, sharedViaP);
            Assert.Equal(shared.Transform, sharedViaP.Transform);
            Assert.Same(mesh, Assert.IsType<MeshNode>(Assert.Single(sharedViaP.Children)).Mesh);

            // The nodes of the original are untouched
            Assert.Same(red, shared.Appearance);
            Assert.Same(shared, p.Children[0]);
        }
    }

    /// <summary>
    ///     Validates the appearance-stripping copy on a deep DAG: a chain of a hundred thousand groups whose two children
    ///     are the same next group has 2^100000 paths, so the copy must visit each node once (and iteratively), and every
    ///     copy must be shared by both of its parents.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_StripAppearances_DeepSharedDag_CopiesEachNodeOnce()
    {
        // Arrange: bottom is a mesh; each level has the level below twice
        const int Depth = 100_000;
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        SceneNode node = new MeshNode(PrimitiveTessellator.Box(new Vector3D(1, 1, 1))) { Appearance = red };
        for (var i = 0; i < Depth; i++)
        {
            node = new CsgNode(CsgOperation.Union, [node, node]) { Appearance = red };
        }

        var context = new ScadGeometryContext(Settings(), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));

        // Act
        var stripped = ScadHulls.StripAppearances(ScadGeo.FromSolid(node, BoundingBox3D.Empty, true, false), context);

        // Assert: Depth + 1 distinct copies, none with an appearance, each shared by both children of its parent
        var seen = 0;
        var current = stripped.Node!;
        while (current is CsgNode csg)
        {
            Assert.Null(csg.Appearance);
            Assert.Equal(CsgOperation.Union, csg.Operation);
            Assert.Same(csg.Children[0], csg.Children[1]);
            current = csg.Children[0];
            seen++;
        }

        Assert.Equal(Depth, seen);
        Assert.Null(Assert.IsType<MeshNode>(current).Appearance);
    }

    /// <summary>
    ///     Validates that the mesh node of a stripped copy is recorded in the location map under the location of the
    ///     original node, so that a render-time report about it points at the instance that produced the mesh; every
    ///     copy of a larger stripped scene is recorded too.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_StripAppearances_CopiesKeepTheOriginalSourceLocation()
    {
        // Arrange: the lone colored child is a union of two cubes at known columns of line 2
        const string Line = "  minkowski() color(\"red\") union() { cube(1); translate([5, 0, 0]) cube(1); }";
        var firstColumn = Line.IndexOf("cube(1)", StringComparison.Ordinal) + 1;
        var secondColumn = Line.LastIndexOf("cube(1)", StringComparison.Ordinal) + 1;

        // Act
        var result = Build($"\n{Line}");

        // Assert: the copies (no appearance) are mapped to their original cubes, not to the minkowski or to nothing
        var leaves = result.Leaves;
        Assert.Equal(2, leaves.Count);
        Assert.All(leaves, leaf => Assert.Null(leaf.Appearance));
        Assert.Equal(new ScadSourceLocation(null, 2, firstColumn), result.Map.Find(leaves[0].Node));
        Assert.Equal(new ScadSourceLocation(null, 2, secondColumn), result.Map.Find(leaves[1].Node));

        // Assert: the copy made directly from a node of the map carries that node's location as well
        var original = new MeshNode(PrimitiveTessellator.Box(new Vector3D(1, 1, 1)));
        var context = new ScadGeometryContext(Settings(), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
        var location = new ScadSourceLocation("file.scad", 7, 9);
        context.Map.Add(original, location);
        var copy = Assert.IsType<MeshNode>(ScadHulls.StripAppearances(ScadGeo.FromSolid(original, original.Mesh.Bounds, true, false), context).Node);
        Assert.NotSame(original, copy);
        Assert.Equal(location, context.Map.Find(copy));
    }

    /// <summary>
    ///     Lists a scene node and everything below it, each distinct node once, walking iteratively.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>The distinct nodes.</returns>
    private static List<SceneNode> Descendants(SceneNode root)
    {
        var seen = new HashSet<SceneNode>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<SceneNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node))
            {
                continue;
            }

            var children = node switch
            {
                GroupNode group => group.Children,
                CsgNode csg => csg.Children,
                _ => [],
            };
            foreach (var child in children)
            {
                stack.Push(child);
            }
        }

        return [.. seen];
    }

    /// <summary>
    ///     Validates that a sum that leaves the coordinate range is rejected like any other geometry that does.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Minkowski_SumBeyondTheCoordinateRange_IsRejected()
    {
        // Arrange / Act: each part is within 1e100, their sum is not
        var result = Build("minkowski() { translate([6e99, 0, 0]) cube(1e99); translate([6e99, 0, 0]) cube(1e99); }");

        // Assert
        Assert.Equal(["scad-geometry-rejected"], result.Features);
        Assert.Empty(result.Meshes);
    }
}
