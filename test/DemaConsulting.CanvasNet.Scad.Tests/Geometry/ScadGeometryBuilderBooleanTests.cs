// cspell:ignore intersection_for
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of the boolean modules of <see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadBooleans"/>:
///     <c>union</c>, <c>difference</c> and <c>intersection</c> for solids and for 2D shapes, with the structure of
///     the produced scene asserted node by node.
/// </summary>
public class ScadGeometryBuilderBooleanTests
{
    /// <summary>
    ///     Asserts that a node is a boolean node with the given operation and number of operands.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="operation">The expected operation.</param>
    /// <param name="operands">The expected number of operands.</param>
    /// <returns>The boolean node.</returns>
    private static CsgNode AssertCsg(SceneNode node, CsgOperation operation, int operands)
    {
        var csg = Assert.IsType<CsgNode>(node);
        Assert.Equal(operation, csg.Operation);
        Assert.Equal(operands, csg.Children.Count);
        return csg;
    }

    /// <summary>
    ///     Validates that a difference of two solids is a difference node of the two meshes in order.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_TwoSolids_IsADifferenceNode()
    {
        // Arrange / Act
        var result = Build("difference() { cube(10); translate([5, 5, -1]) cylinder(r = 2, h = 12, $fn = 16); }");

        // Assert
        var csg = AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        var minuend = Assert.IsType<MeshNode>(csg.Children[0]);
        AssertSolidVolume(minuend.Mesh, 1000.0);
        var subtrahend = Assert.IsType<GroupNode>(csg.Children[1]);
        Assert.IsType<MeshNode>(Assert.Single(subtrahend.Children));
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that the first child is the minuend, not whatever comes first in the document order of meshes.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_FirstChildIsTheMinuend()
    {
        // Arrange / Act
        var result = Build("difference() { cube(1); cube(10); }");

        // Assert
        var csg = AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        Assert.Equal(1.0, ((MeshNode)csg.Children[0]).Mesh.SignedVolume, 1e-12);
        Assert.Equal(1000.0, ((MeshNode)csg.Children[1]).Mesh.SignedVolume, 1e-9);
    }

    /// <summary>
    ///     Validates that all children after the first are separate subtrahends.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_SeveralSubtrahends_AreSeparateOperands()
    {
        // Arrange / Act
        var result = Build("difference() { cube(10); cube(1); cube(2); cube(3); }");

        // Assert
        AssertCsg(result.Scene.Root, CsgOperation.Difference, 4);
    }

    /// <summary>
    ///     Validates that nested differences nest as nodes.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_Nested_NestsNodes()
    {
        // Arrange / Act
        var result = Build("difference() { difference() { cube(10); cube(2); } cube(3); }");

        // Assert
        var outer = AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        AssertCsg(outer.Children[0], CsgOperation.Difference, 2);
        Assert.IsType<MeshNode>(outer.Children[1]);
    }

    /// <summary>
    ///     Validates that a loop under a difference is one operand (its own union), not one operand per iteration.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_LoopChild_IsOneUnionOperand()
    {
        // Arrange / Act
        var result = Build("difference() { cube(20); for (i = [0:2]) translate([i * 5, 0, 0]) cube(1); }");

        // Assert
        var csg = AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        var union = Assert.IsType<GroupNode>(csg.Children[1]);
        Assert.Equal(3, union.Children.Count);
    }

    /// <summary>
    ///     Validates that a user module call and an explicit <c>union</c> under a difference are one operand each.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_ModuleAndUnionChildren_AreOneOperandEach()
    {
        // Arrange / Act
        var result = Build("module two() { cube(1); translate([3, 0, 0]) cube(1); }\ndifference() { cube(20); two(); union() { cube(2); cube(3); } }");

        // Assert
        var csg = AssertCsg(result.Scene.Root, CsgOperation.Difference, 3);
        Assert.Equal(2, ((GroupNode)csg.Children[1]).Children.Count);
        Assert.Equal(2, ((GroupNode)csg.Children[2]).Children.Count);
    }

    /// <summary>
    ///     Validates that an empty first operand makes the difference empty (nothing minus something is nothing),
    ///     whether the first operand is an invalid primitive or an empty group.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("difference() { cube(0); cube(1); }")]
    [InlineData("difference() { translate([1, 0, 0]) { } cube(1); }")]
    [InlineData("difference() { color(\"red\") { } cube(1); cube(2); }")]
    public void ScadBooleans_Difference_EmptyFirstOperand_IsEmpty(string source)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        Assert.IsType<GroupNode>(result.Scene.Root);
    }

    /// <summary>
    ///     Validates that empty later operands vanish and that a difference left with one operand is that operand
    ///     itself (no boolean node).
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_EmptyLaterOperands_Vanish()
    {
        // Arrange / Act
        var single = Build("difference() { cube(2); translate([1, 0, 0]) { } }");
        var two = Build("difference() { cube(5); translate([1, 0, 0]) { } cube(1); }");

        // Assert
        Assert.IsType<MeshNode>(single.Scene.Root);
        AssertCsg(two.Scene.Root, CsgOperation.Difference, 2);
    }

    /// <summary>
    ///     Validates that a difference with a single child is that child, and one without children is empty.
    /// </summary>
    [Fact]
    public void ScadBooleans_Difference_SingleOrNoChild_IsTheChildOrNothing()
    {
        // Arrange / Act
        var single = Build("difference() cube(2);");
        var none = Build("difference() { }");

        // Assert
        Assert.IsType<MeshNode>(single.Scene.Root);
        Assert.Empty(none.Meshes);
        Assert.Empty(none.Warnings);
    }

    /// <summary>
    ///     Validates the structure of an intersection and that any empty operand empties it.
    /// </summary>
    [Fact]
    public void ScadBooleans_Intersection_IsAnIntersectionNodeAndAnyEmptyOperandEmptiesIt()
    {
        // Arrange / Act
        var normal = Build("intersection() { cube(10); sphere(6, $fn = 16); }");
        var emptied = Build("intersection() { cube(10); translate([1, 0, 0]) { } sphere(6); }");
        var single = Build("intersection() cube(2);");

        // Assert
        AssertCsg(normal.Scene.Root, CsgOperation.Intersection, 2);
        Assert.Empty(emptied.Meshes);
        Assert.IsType<MeshNode>(single.Scene.Root);
    }

    /// <summary>
    ///     Validates that <c>intersection_for</c> becomes an intersection of its iterations.
    /// </summary>
    [Fact]
    public void ScadBooleans_IntersectionFor_IsAnIntersectionOfTheIterations()
    {
        // Arrange / Act
        var result = Build("intersection_for (i = [0:2]) translate([i, 0, 0]) cube(5);");

        // Assert
        AssertCsg(result.Scene.Root, CsgOperation.Intersection, 3);
        AssertBounds(result.Scene.ComputeBounds(), new Vector3D(0, 0, 0), new Vector3D(7, 5, 5));
    }

    /// <summary>
    ///     Validates that <c>union</c> and <c>render</c> flatten to one group and that a lone child is itself.
    /// </summary>
    [Fact]
    public void ScadBooleans_UnionAndRender_AreGroups()
    {
        // Arrange / Act
        var union = Build("union() { cube(1); cube(2); }");
        var render = Build("render() { cube(1); cube(2); }");
        var single = Build("union() cube(1);");

        // Assert
        Assert.Equal(2, Assert.IsType<GroupNode>(union.Scene.Root).Children.Count);
        Assert.Equal(2, Assert.IsType<GroupNode>(render.Scene.Root).Children.Count);
        Assert.IsType<MeshNode>(single.Scene.Root);
    }

    /// <summary>
    ///     Validates that a boolean keeps transforms and colors of its operands on the operands, and of itself on
    ///     the node, exactly where they were written.
    /// </summary>
    [Fact]
    public void ScadBooleans_TransformsAndColors_StayWhereTheyWereWritten()
    {
        // Arrange / Act
        var result = Build("translate([100, 0, 0]) color(\"red\") difference() { cube(10); translate([1, 1, 1]) color(\"blue\") cube(2); }");

        // Assert
        var leaves = result.Leaves;
        Assert.Equal(2, leaves.Count);
        Assert.Equal(new Rgba32(255, 0, 0, 255), leaves[0].Appearance!.Color);
        Assert.Equal(new Rgba32(0, 0, 255, 255), leaves[1].Appearance!.Color);
        AssertBounds(leaves[0].WorldMesh.Bounds, new Vector3D(100, 0, 0), new Vector3D(110, 10, 10));
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(101, 1, 1), new Vector3D(103, 3, 3));
    }

    /// <summary>
    ///     Validates that a difference of solids mixed with a 2D shape drops the 2D shape with a warning at the
    ///     boolean, and that a 2D first operand drops the solids.
    /// </summary>
    [Fact]
    public void ScadBooleans_MixedDimensions_KeepTheDimensionOfTheFirstOperand()
    {
        // Arrange / Act
        var solidFirst = Build("\ndifference() { cube(2); square(1); }");
        var flatFirst = Build("\nintersection() { square(2); cube(1); }");

        // Assert
        Assert.IsType<MeshNode>(solidFirst.Scene.Root);
        var warning = Assert.Single(solidFirst.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), warning.Location);
        Assert.Contains("mixes 2D and 3D", warning.Message, StringComparison.Ordinal);
        var slab = Assert.Single(flatFirst.Leaves).WorldMesh;
        AssertBounds(slab.Bounds, new Vector3D(0, 0, 0), new Vector3D(2, 2, 0.1));
        Assert.Contains(flatFirst.Warnings, w => w.Message.Contains("mixes 2D and 3D", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a union of 2D and 3D keeps the 3D members (the Stage 2 rule) also inside a difference
    ///     operand.
    /// </summary>
    [Fact]
    public void ScadBooleans_UnionOfMixedDimensions_KeepsTheSolids()
    {
        // Arrange / Act
        var result = Build("difference() { union() { cube(2); square(5); } cube(1); }");

        // Assert
        AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that a top-level 2D difference becomes a difference of slabs of the same thickness, one slab
    ///     per operand, with the exact footprints.
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_Difference_BecomesADifferenceOfSlabs()
    {
        // Arrange / Act
        var result = Build("difference() { square(10); translate([2, 2]) square(3); }");

        // Assert
        var csg = AssertCsg(result.Scene.Root, CsgOperation.Difference, 2);
        var leaves = result.Leaves;
        Assert.Equal(2, leaves.Count);
        AssertSolidVolume(leaves[0].WorldMesh, 100.0 * 0.1);
        AssertSolidVolume(leaves[1].WorldMesh, 9.0 * 0.1);
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(2, 2, 0), new Vector3D(5, 5, 0.1));
        Assert.IsType<MeshNode>(csg.Children[0]);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates 2D intersection, nested 2D booleans and the union of 2D operands under a difference.
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_IntersectionAndNesting_FollowTheTree()
    {
        // Arrange / Act
        var intersection = Build("intersection() { square(4); translate([2, 2]) square(4); }");
        var nested = Build("difference() { square(20); union() { square(2); translate([5, 0]) square(2); } intersection() { square(3); translate([1, 1]) square(3); } }");

        // Assert
        AssertCsg(intersection.Scene.Root, CsgOperation.Intersection, 2);
        var csg = AssertCsg(nested.Scene.Root, CsgOperation.Difference, 3);
        Assert.Equal(2, Assert.IsType<GroupNode>(csg.Children[1]).Children.Count);
        AssertCsg(csg.Children[2], CsgOperation.Intersection, 2);
    }

    /// <summary>
    ///     Validates that 2D transforms inside a 2D difference are applied to their operand only.
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_TransformsApplyToTheirOperandOnly()
    {
        // Arrange / Act
        var result = Build("difference() { square(10); translate([1, 1]) rotate(45) square(2); }");

        // Assert: the square turned by 45 degrees about the origin spans x -sqrt(2)..sqrt(2), y 0..2 sqrt(2), then moves by 1
        var leaves = result.Leaves;
        var root2 = Math.Sqrt(2.0);
        AssertBounds(leaves[0].WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(10, 10, 0.1));
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(1 - root2, 1, 0), new Vector3D(1 + root2, 1 + (2 * root2), 0.1), 1e-9);
        AssertSolidVolume(leaves[1].WorldMesh, 4.0 * 0.1);
    }

    /// <summary>
    ///     Validates that a 2D shape with a hole and a mirrored 2D operand work in a boolean (winding is normalized).
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_HoleAndMirroredOperands_AreValid()
    {
        // Arrange / Act
        var result = Build("difference() { polygon([[0, 0], [10, 0], [10, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]); mirror([1, 0]) translate([1, 1]) square(1); }");

        // Assert
        var leaves = result.Leaves;
        AssertSolidVolume(leaves[0].WorldMesh, (100.0 - 36.0) * 0.1);
        AssertSolidVolume(leaves[1].WorldMesh, 1.0 * 0.1);
        AssertBounds(leaves[1].WorldMesh.Bounds, new Vector3D(-2, 1, 0), new Vector3D(-1, 2, 0.1));
    }

    /// <summary>
    ///     Validates that a color around a 2D boolean colors every slab operand.
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_ColorAroundDifference_ColorsAllOperands()
    {
        // Arrange / Act
        var result = Build("color(\"red\") difference() { square(5); square(2); }");

        // Assert
        Assert.All(result.Leaves, leaf => Assert.Equal(new Rgba32(255, 0, 0, 255), leaf.Appearance!.Color));
        Assert.Equal(2, result.Leaves.Count);
    }

    /// <summary>
    ///     Validates that an empty first 2D operand empties the 2D difference, and an empty later one vanishes.
    /// </summary>
    [Fact]
    public void ScadBooleans_Flat_EmptyOperands_FollowTheSameRules()
    {
        // Arrange / Act
        var emptyFirst = Build("difference() { square(0); square(1); }");
        var emptyLater = Build("difference() { square(2); translate([0, 1]) { } }");

        // Assert
        Assert.Empty(emptyFirst.Meshes);
        Assert.IsType<MeshNode>(emptyLater.Scene.Root);
    }

    /// <summary>
    ///     Validates the bounds a difference reports to <c>resize</c>: the extent of its first operand, flagged
    ///     approximate; and the overlap for an intersection.
    /// </summary>
    /// <param name="children">The boolean.</param>
    [Theory]
    [InlineData("difference() { cube(2); translate([1, 1, 1]) cube(2); }")]
    [InlineData("intersection() { cube(2); translate([1, 1, 1]) cube(2); }")]
    [InlineData("difference() { square(2); translate([1, 1]) square(2); }")]
    public void ScadBooleans_Resize_OverBoolean_ReportsApproximateBoundsAndStillResizes(string children)
    {
        // Arrange / Act
        var result = Build($"\n  resize([10, 10, 10]) {children}");

        // Assert
        var report = Assert.Single(result.Unsupported, u => u.Feature == "scad-resize-approximate");
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.NotEmpty(result.Meshes);
    }

    /// <summary>
    ///     Validates that a difference estimates its size from the first operand: with a first operand of 2 and
    ///     <c>resize</c> to 10, the factor is 5 for every operand.
    /// </summary>
    [Fact]
    public void ScadBooleans_Resize_OverDifference_UsesTheFirstOperandExtent()
    {
        // Arrange / Act
        var result = Build("resize([10, 10, 10]) difference() { cube(2); translate([5, 5, 5]) cube(1); }");

        // Assert
        AssertBounds(result.Leaves[0].WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(10, 10, 10));
        AssertBounds(result.Leaves[1].WorldMesh.Bounds, new Vector3D(25, 25, 25), new Vector3D(30, 30, 30));
    }

    /// <summary>
    ///     Validates that an intersection estimates its size from the overlap of the operand boxes.
    /// </summary>
    [Fact]
    public void ScadBooleans_Resize_OverIntersection_UsesTheOverlap()
    {
        // Arrange / Act: the boxes overlap in 1..2, a size of 1, so resizing to 4 scales by 4
        var result = Build("resize([4, 4, 4]) intersection() { cube(2); translate([1, 1, 1]) cube(2); }");

        // Assert
        AssertBounds(result.Leaves[0].WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(8, 8, 8));
    }

    /// <summary>
    ///     Validates that a boolean without any approximate content stays exact for <c>resize</c> even after a
    ///     union with a boolean-free sibling: only a boolean taints it.
    /// </summary>
    [Fact]
    public void ScadBooleans_Resize_OverUnionOfPlainSolids_IsNotReported()
    {
        // Arrange / Act
        var result = Build("resize([4, 4, 4]) union() { cube(1); translate([1, 0, 0]) cube(1); }");

        // Assert
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a single-operand boolean keeps the operand's exactness (no spurious approximate report).
    /// </summary>
    [Fact]
    public void ScadBooleans_Resize_OverSingleOperandBoolean_IsNotReported()
    {
        // Arrange / Act
        var result = Build("resize([4, 4, 4]) difference() { cube(1); translate([9, 0, 0]) { } }");

        // Assert
        Assert.Empty(result.Unsupported);
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(4, 4, 4));
    }
}
