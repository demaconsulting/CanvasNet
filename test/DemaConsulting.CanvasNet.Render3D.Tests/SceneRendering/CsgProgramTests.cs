// cspell:ignore ANDNOT

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>CsgProgram</c> and <c>CsgCompiler</c> (CsgResolver unit).
/// </summary>
public class CsgProgramTests
{
    /// <summary>The mesh shared by every node; the compiler never looks at geometry.</summary>
    private static readonly Mesh SharedMesh = Render3DTestHelpers.CreateTetrahedronMesh();

    /// <summary>
    ///     Creates a mesh node.
    /// </summary>
    /// <returns>A new node.</returns>
    private static MeshNode M() => new(SharedMesh);

    /// <summary>
    ///     Creates a CSG node.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="children">The children.</param>
    /// <returns>The node.</returns>
    private static CsgNode Csg(CsgOperation operation, params SceneNode[] children) => new(operation, children);

    /// <summary>
    ///     Creates a group node.
    /// </summary>
    /// <param name="children">The children.</param>
    /// <returns>The node.</returns>
    private static GroupNode Group(params SceneNode[] children) => new(children);

    /// <summary>
    ///     Evaluates a program for the leaves in <paramref name="inside"/>.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <param name="inside">The inside leaf indices.</param>
    /// <returns>The result.</returns>
    private static bool Eval(CsgProgram program, params int[] inside)
    {
        var bits = new ulong[program.BitsetWords];
        foreach (var leaf in inside)
        {
            bits[leaf >> 6] |= 1UL << (leaf & 63);
        }

        return program.Evaluate(bits, new bool[program.MaxStackDepth]);
    }

    /// <summary>
    ///     Validates that a union-only scene needs no program and all instances map to one leaf.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_UnionOnlyScene_HasNoProgramAndOneLeaf()
    {
        // Arrange: groups, union nodes and single-child booleans nested around four meshes
        var root = Group(M(), Group(M(), Csg(CsgOperation.Union, M())), Csg(CsgOperation.Difference, Group(M())));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Null(result.Program);
        Assert.Equal(1, result.LeafCount);
        Assert.Equal([0, 0, 0, 0], result.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that a single mesh and an empty node are union-only.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_SingleMeshAndEmptyNodes_HaveNoProgram()
    {
        // Arrange / Act
        var single = CsgCompiler.Compile(M());
        var emptyGroup = CsgCompiler.Compile(Group());
        var emptyIntersection = CsgCompiler.Compile(Csg(CsgOperation.Intersection));

        // Assert
        Assert.Null(single.Program);
        Assert.Equal([0], single.LeafOfInstance);
        Assert.Null(emptyGroup.Program);
        Assert.Empty(emptyGroup.LeafOfInstance);
        Assert.Equal(0, emptyGroup.LeafCount);
        Assert.Null(emptyIntersection.Program);
        Assert.Empty(emptyIntersection.LeafOfInstance);
    }

    /// <summary>
    ///     Validates the n-ary difference A - B - C and the union collapse of its operands.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NaryDifference_CollapsesUnionOperandsIntoLeaves()
    {
        // Arrange: (m0 + m1) - m2 - (m3 + m4)
        var root = Csg(CsgOperation.Difference, Group(M(), M()), M(), Group(M(), M()));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.NotNull(result.Program);
        Assert.Equal("L0 L1 ANDNOT L2 ANDNOT", result.Program.Disassemble());
        Assert.Equal([0, 0, 1, 2, 2], result.LeafOfInstance);
        Assert.Equal(3, result.LeafCount);
        Assert.True(Eval(result.Program, 0));
        Assert.False(Eval(result.Program, 0, 1));
        Assert.False(Eval(result.Program, 0, 2));
        Assert.False(Eval(result.Program, 1));
    }

    /// <summary>
    ///     Validates the intersection of N children.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NaryIntersection_AndsAllChildren()
    {
        // Arrange
        var root = Csg(CsgOperation.Intersection, M(), M(), M(), M());

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.NotNull(result.Program);
        Assert.Equal("L0 L1 AND L2 AND L3 AND", result.Program.Disassemble());
        Assert.True(Eval(result.Program, 0, 1, 2, 3));
        Assert.False(Eval(result.Program, 0, 1, 3));
    }

    /// <summary>
    ///     Validates nested differences: (A - B) - (C - D) and a difference inside an intersection.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NestedBooleans_ProducesPostfixOfTheTree()
    {
        // Arrange
        var diffOfDiffs = Csg(
            CsgOperation.Difference,
            Csg(CsgOperation.Difference, M(), M()),
            Csg(CsgOperation.Difference, M(), M()));
        var intersectionOfDiff = Csg(CsgOperation.Intersection, Csg(CsgOperation.Difference, M(), M()), M());

        // Act
        var first = CsgCompiler.Compile(diffOfDiffs);
        var second = CsgCompiler.Compile(intersectionOfDiff);

        // Assert
        Assert.Equal("L0 L1 ANDNOT L2 L3 ANDNOT ANDNOT", first.Program!.Disassemble());
        Assert.Equal([0, 1, 2, 3], first.LeafOfInstance);
        Assert.Equal("L0 L1 ANDNOT L2 AND", second.Program!.Disassemble());

        // Truth table of (A - B) - (C - D) over all 16 leaf states
        for (var state = 0; state < 16; state++)
        {
            var a = (state & 1) != 0;
            var b = (state & 2) != 0;
            var c = (state & 4) != 0;
            var d = (state & 8) != 0;
            var inside = Enumerable.Range(0, 4).Where(i => (state & (1 << i)) != 0).ToArray();
            Assert.Equal(a && !b && !(c && !d), Eval(first.Program, inside));
        }
    }

    /// <summary>
    ///     Validates that a group mixing a plain mesh and a difference ORs them, and that single-child booleans are transparent.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_MixedGroupAndSingleChildBooleans_OrsChildren()
    {
        // Arrange: m0 + (m1 - (m2 + m3)) + single-child intersection(m4) + single-child difference(m5)
        var root = Group(
            M(),
            Csg(CsgOperation.Difference, M(), Group(M(), M())),
            Csg(CsgOperation.Intersection, M()),
            Csg(CsgOperation.Difference, M()));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Equal("L0 L1 L2 ANDNOT OR L3 OR L4 OR", result.Program!.Disassemble());
        Assert.Equal([0, 1, 2, 2, 3, 4], result.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that an empty node is FALSE for every operation, including an intersection, and an empty operand never matches.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_EmptyNodesInsideBooleans_AreFalse()
    {
        // Arrange: m0 - empty, empty ∩ m0 (empty intersection node), and (m0 + empty intersection)-m1
        var differenceOfEmpty = Csg(CsgOperation.Difference, M(), Group());
        var intersectionWithEmpty = Csg(CsgOperation.Intersection, M(), Csg(CsgOperation.Intersection));
        var unionWithEmpty = Csg(CsgOperation.Difference, Group(M(), Csg(CsgOperation.Intersection)), M());

        // Act
        var first = CsgCompiler.Compile(differenceOfEmpty);
        var second = CsgCompiler.Compile(intersectionWithEmpty);
        var third = CsgCompiler.Compile(unionWithEmpty);

        // Assert
        Assert.Equal("L0 FALSE ANDNOT", first.Program!.Disassemble());
        Assert.True(Eval(first.Program, 0));
        Assert.Equal("L0 FALSE AND", second.Program!.Disassemble());
        Assert.False(Eval(second.Program, 0));
        Assert.Equal("L0 L1 ANDNOT", third.Program!.Disassemble());
        Assert.Equal([0, 1], third.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that a shared node is expanded once per path, like Scene.EnumerateInstances, and the mapping matches the enumeration.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_SharedNodeDag_MatchesInstanceEnumerationPerPath()
    {
        // Arrange: shared mesh and shared sub-difference appear on several paths
        var sharedMesh = M();
        var sharedDifference = Csg(CsgOperation.Difference, M(), sharedMesh);
        var root = Csg(
            CsgOperation.Difference,
            Group(sharedMesh, sharedDifference),
            sharedMesh,
            Csg(CsgOperation.Intersection, sharedDifference, sharedMesh));

        // Act
        var result = CsgCompiler.Compile(root);
        var instances = new Scene(root).EnumerateInstances().ToList();

        // Assert: one mapping entry per enumerated instance and leaves assigned per path
        Assert.Equal(7, instances.Count);
        Assert.Equal(instances.Count, result.LeafOfInstance.Length);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, result.LeafOfInstance);
        Assert.Equal(7, result.LeafCount);
        Assert.Equal("L0 L1 L2 ANDNOT OR L3 ANDNOT L4 L5 ANDNOT L6 AND ANDNOT", result.Program!.Disassemble());
        var innerMesh = sharedDifference.Children[0];
        Assert.Equal(
            new SceneNode[] { sharedMesh, innerMesh, sharedMesh, sharedMesh, innerMesh, sharedMesh, sharedMesh },
            instances.Select(i => (SceneNode)i.Node).ToArray());
    }

    /// <summary>
    ///     Validates that the leaf mapping follows the pre-order of Scene.EnumerateInstances on a mixed scene.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_MixedScene_LeafOfInstanceFollowsEnumerationOrder()
    {
        // Arrange: distinct nodes so the instance order can be identified by node reference (node 0 is shared)
        var nodes = Enumerable.Range(0, 6).Select(_ => M()).ToArray();
        var root = Group(
            nodes[0],
            Csg(CsgOperation.Difference, Group(nodes[1], nodes[2]), nodes[3]),
            Csg(CsgOperation.Union, nodes[4], Csg(CsgOperation.Intersection, nodes[5], nodes[0])));

        // Act
        var result = CsgCompiler.Compile(root);
        var instanceNodes = new Scene(root).EnumerateInstances().Select(i => i.Node).ToArray();

        // Assert: the leaf of each enumerated instance equals the expected leaf of its node on that path
        Assert.Equal(new[] { nodes[0], nodes[1], nodes[2], nodes[3], nodes[4], nodes[5], nodes[0] }, instanceNodes);
        Assert.Equal(new[] { 0, 1, 1, 2, 3, 4, 5 }, result.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that more than 64 leaves are supported by the bitset (word boundaries 63/64/65/127/128).
    /// </summary>
    [Fact]
    public void CsgProgram_Evaluate_MoreThan64Leaves_UsesMultipleBitsetWords()
    {
        // Arrange: A - (B1 ... B129) as one difference, and an intersection of 130 leaves
        var children = Enumerable.Range(0, 130).Select(_ => (SceneNode)M()).ToArray();
        var difference = CsgCompiler.Compile(new CsgNode(CsgOperation.Difference, children)).Program!;
        var intersection = CsgCompiler.Compile(new CsgNode(CsgOperation.Intersection, children)).Program!;

        // Act / Assert
        Assert.Equal(130, difference.LeafCount);
        Assert.Equal(3, difference.BitsetWords);
        Assert.True(Eval(difference, 0));
        foreach (var blocker in new[] { 1, 63, 64, 65, 127, 128, 129 })
        {
            Assert.False(Eval(difference, 0, blocker));
        }

        Assert.False(Eval(difference, 64));
        var all = Enumerable.Range(0, 130).ToArray();
        Assert.True(Eval(intersection, all));
        foreach (var missing in new[] { 0, 63, 64, 65, 127, 128, 129 })
        {
            Assert.False(Eval(intersection, all.Where(i => i != missing).ToArray()));
        }
    }

    /// <summary>
    ///     Validates that a 10 000-deep left-nested and right-nested difference compile and evaluate without stack overflow.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_DeeplyNestedDifferences_AreIterative()
    {
        // Arrange
        const int Depth = 10_000;
        SceneNode left = M();
        SceneNode right = M();
        for (var i = 0; i < Depth; i++)
        {
            left = Csg(CsgOperation.Difference, left, M());
            right = Csg(CsgOperation.Difference, M(), right);
        }

        // Act
        var leftResult = CsgCompiler.Compile(left);
        var rightResult = CsgCompiler.Compile(right);

        // Assert: left nesting is ((m0 - m1) - m2) ... so leaf 0 is the minuend; right nesting is m0 - (m1 - (m2 - ...))
        Assert.Equal(Depth + 1, leftResult.LeafCount);
        Assert.Equal(Depth + 1, rightResult.LeafCount);
        Assert.True(Eval(leftResult.Program!, 0));
        Assert.False(Eval(leftResult.Program!, 0, Depth));
        Assert.False(Eval(leftResult.Program!, 0, Depth / 2));
        Assert.True(rightResult.Program!.MaxStackDepth > Depth);

        // The right-nested value is b0 AND NOT (b1 AND NOT (b2 ...)); compare against the iterative definition for random states
        var random = new Random(12345);
        for (var trial = 0; trial < 20; trial++)
        {
            var states = Enumerable.Range(0, Depth + 1).Select(_ => random.Next(3) != 0).ToArray();
            var expected = states[Depth];
            for (var k = Depth - 1; k >= 0; k--)
            {
                expected = states[k] && !expected;
            }

            var inside = Enumerable.Range(0, Depth + 1).Where(k => states[k]).ToArray();
            Assert.Equal(expected, Eval(rightResult.Program, inside));
        }
    }

    /// <summary>
    ///     Validates that a 10 000-deep union nest is collapsed iteratively into one leaf.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_DeeplyNestedUnion_CollapsesIteratively()
    {
        // Arrange
        SceneNode node = M();
        for (var i = 0; i < 10_000; i++)
        {
            node = Group(node, M());
        }

        // Act
        var result = CsgCompiler.Compile(node);

        // Assert
        Assert.Null(result.Program);
        Assert.Equal(10_001, result.LeafOfInstance.Length);
        Assert.All(result.LeafOfInstance, leaf => Assert.Equal(0, leaf));
    }

    /// <summary>
    ///     Validates the argument checks of the compiler.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NullRoot_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => CsgCompiler.Compile(null!));
    }

    /// <summary>
    ///     Validates the evaluator truth tables of every opcode.
    /// </summary>
    [Fact]
    public void CsgProgram_Evaluate_EveryOpcode_FollowsBooleanTruthTable()
    {
        // Arrange
        var or = new CsgProgram([new(CsgOpCode.PushLeaf, 0), new(CsgOpCode.PushLeaf, 1), new(CsgOpCode.Or, 0)], 2);
        var and = new CsgProgram([new(CsgOpCode.PushLeaf, 0), new(CsgOpCode.PushLeaf, 1), new(CsgOpCode.And, 0)], 2);
        var andNot = new CsgProgram([new(CsgOpCode.PushLeaf, 0), new(CsgOpCode.PushLeaf, 1), new(CsgOpCode.AndNot, 0)], 2);
        var falseProgram = new CsgProgram([new(CsgOpCode.PushFalse, 0)], 1);

        // Act / Assert: every state of the two leaves
        for (var state = 0; state < 4; state++)
        {
            var a = (state & 1) != 0;
            var b = (state & 2) != 0;
            var inside = Enumerable.Range(0, 2).Where(i => (state & (1 << i)) != 0).ToArray();
            Assert.Equal(a || b, Eval(or, inside));
            Assert.Equal(a && b, Eval(and, inside));
            Assert.Equal(a && !b, Eval(andNot, inside));
            Assert.False(Eval(falseProgram, inside));
        }

        Assert.Equal(2, or.MaxStackDepth);
        Assert.Equal(1, falseProgram.MaxStackDepth);
        Assert.Equal(3, or.Instructions.Length);
    }

    /// <summary>
    ///     Validates that malformed instruction sequences are rejected.
    /// </summary>
    [Fact]
    public void CsgProgram_Constructor_MalformedInstructions_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new CsgProgram([], 1));
        Assert.Throws<ArgumentException>(() => new CsgProgram([new(CsgOpCode.Or, 0)], 1));
        Assert.Throws<ArgumentException>(() => new CsgProgram([new(CsgOpCode.PushFalse, 0), new(CsgOpCode.PushFalse, 0)], 1));
        Assert.Throws<ArgumentException>(() => new CsgProgram([new(CsgOpCode.PushLeaf, 1)], 1));
        Assert.Throws<ArgumentException>(() => new CsgProgram([new((CsgOpCode)99, 0)], 1));
        Assert.Throws<ArgumentNullException>(() => new CsgProgram(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsgProgram([new(CsgOpCode.PushFalse, 0)], -1));
    }

    /// <summary>
    ///     Validates that an all-outside leaf state evaluates to outside for every compiled program (no program can turn nothing into something).
    /// </summary>
    [Fact]
    public void CsgProgram_Evaluate_AllLeavesOutside_IsOutside()
    {
        // Arrange
        var root = Csg(CsgOperation.Difference, Group(M(), Csg(CsgOperation.Intersection, M(), M())), M(), Csg(CsgOperation.Union));

        // Act
        var program = CsgCompiler.Compile(root).Program!;

        // Assert
        Assert.False(Eval(program));
    }

    /// <summary>
    ///     Creates an overlay node.
    /// </summary>
    /// <param name="children">The children.</param>
    /// <returns>The node.</returns>
    private static OverlayNode Overlay(params SceneNode[] children) => new(new Rgba32(255, 0, 0, 100), children);

    /// <summary>
    ///     Validates that an overlay is FALSE in the main program and its body gets a program of its own.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_OverlayNode_IsFalseInMainAndGetsOwnProgram()
    {
        // Arrange: m0 - overlay(m1)
        var root = Csg(CsgOperation.Difference, M(), Overlay(M()));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Equal("L0 FALSE ANDNOT", result.Program!.Disassemble());
        Assert.Equal([0, 1], result.LeafOfInstance);
        Assert.Equal(2, result.LeafCount);
        var overlay = Assert.Single(result.OverlayPrograms!);
        Assert.Equal("L1", overlay.Disassemble());
        Assert.True(Eval(result.Program, 0));
        Assert.False(Eval(result.Program, 1));
    }

    /// <summary>
    ///     Validates that an overlay prevents collapsing its own ancestors but not sibling subtrees.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_OverlayInsideUnion_DoesNotCollapseMainLeaf_ButSiblingsStillCollapse()
    {
        // Arrange: group(group(m, m), overlay(m), group(m, m))
        var root = Group(Group(M(), M()), Overlay(M()), Group(M(), M()));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Equal("L0 FALSE OR L2 OR", result.Program!.Disassemble());
        Assert.Equal([0, 0, 1, 2, 2], result.LeafOfInstance);
        Assert.Equal(3, result.LeafCount);
        Assert.Equal("L1", Assert.Single(result.OverlayPrograms!).Disassemble());
    }

    /// <summary>
    ///     Validates that leaf numbers follow the pre-order of the instance enumeration when an overlay sits inside a difference.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_OverlayInsideDifference_LeafOfInstanceFollowsPreOrder()
    {
        // Arrange: m0 - overlay(m1, m2) - m3, scene built so the enumeration can be compared
        var root = Csg(CsgOperation.Difference, M(), Overlay(M(), M()), M());
        var scene = new Scene(root);

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Equal("L0 FALSE ANDNOT L2 ANDNOT", result.Program!.Disassemble());
        Assert.Equal([0, 1, 1, 2], result.LeafOfInstance);
        Assert.Equal(scene.EnumerateInstances().Count(), result.LeafOfInstance.Length);
        Assert.Equal([false, true, true, false], scene.EnumerateInstances().Select(i => i.InOverlay));
        Assert.Equal("L1", Assert.Single(result.OverlayPrograms!).Disassemble());
    }

    /// <summary>
    ///     Validates that an overlay nested in an overlay body is a plain group, also inside a boolean body.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NestedOverlay_IsGroupInsideOverlayBody()
    {
        // Arrange: a union-only body with a nested overlay, and a body whose difference holds a nested overlay
        var unionScene = Group(M(), Overlay(M(), Overlay(M())));
        var booleanScene = Overlay(Csg(CsgOperation.Difference, M(), Overlay(M())));

        // Act
        var union = CsgCompiler.Compile(unionScene);
        var boolean = CsgCompiler.Compile(booleanScene);

        // Assert: the whole union body is one leaf; the boolean body is a normal difference of two leaves
        Assert.Equal("L0 FALSE OR", union.Program!.Disassemble());
        Assert.Equal([0, 1, 1], union.LeafOfInstance);
        Assert.Equal("L1", Assert.Single(union.OverlayPrograms!).Disassemble());
        Assert.Equal("FALSE", boolean.Program!.Disassemble());
        Assert.Equal("L0 L1 ANDNOT", Assert.Single(boolean.OverlayPrograms!).Disassemble());
        Assert.Equal([0, 1], boolean.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that a node shared by the main tree and an overlay body is classified separately per context.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_SharedNodeInMainAndOverlay_ClassifiedPerContext()
    {
        // Arrange: shared = group(m, overlay(m)); root = group(shared, overlay(shared))
        var shared = Group(M(), Overlay(M()));
        var root = Group(shared, Overlay(shared));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert: in the main tree the shared node holds an overlay, inside the overlay body it is plain union
        Assert.Equal("L0 FALSE OR FALSE OR", result.Program!.Disassemble());
        Assert.Equal([0, 1, 2, 2], result.LeafOfInstance);
        Assert.Equal(["L1", "L2"], result.OverlayPrograms!.Select(p => p.Disassemble()));
    }

    /// <summary>
    ///     Validates that an overlay without meshes adds no program.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_EmptyOverlay_AddsNoProgram()
    {
        // Arrange
        var root = Group(M(), Overlay(), Overlay(Group()));

        // Act
        var result = CsgCompiler.Compile(root);

        // Assert
        Assert.Equal("L0 FALSE OR FALSE OR", result.Program!.Disassemble());
        Assert.Empty(result.OverlayPrograms!);
        Assert.Equal([0], result.LeafOfInstance);
    }

    /// <summary>
    ///     Validates that a scene made only of an overlay has a FALSE main program.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_OnlyOverlays_MainProgramIsFalse()
    {
        // Arrange / Act
        var result = CsgCompiler.Compile(Overlay(M(), M()));

        // Assert
        Assert.Equal("FALSE", result.Program!.Disassemble());
        Assert.False(Eval(result.Program));
        Assert.Equal(1, result.LeafCount);
        Assert.Equal([0, 0], result.LeafOfInstance);
        Assert.Equal("L0", Assert.Single(result.OverlayPrograms!).Disassemble());
    }

    /// <summary>
    ///     Validates that a scene without overlays compiles exactly as before and has no overlay programs.
    /// </summary>
    [Fact]
    public void CsgCompiler_Compile_NoOverlay_ProgramAndLeavesUnchanged()
    {
        // Arrange
        var difference = Csg(CsgOperation.Difference, Group(M(), M()), M());

        // Act
        var boolean = CsgCompiler.Compile(difference);
        var unionOnly = CsgCompiler.Compile(Group(M(), M()));

        // Assert
        Assert.Equal("L0 L1 ANDNOT", boolean.Program!.Disassemble());
        Assert.Empty(boolean.OverlayPrograms ?? []);
        Assert.Equal([0, 0, 1], boolean.LeafOfInstance);
        Assert.Null(unionOnly.Program);
        Assert.Empty(unionOnly.OverlayPrograms ?? []);
    }
}
