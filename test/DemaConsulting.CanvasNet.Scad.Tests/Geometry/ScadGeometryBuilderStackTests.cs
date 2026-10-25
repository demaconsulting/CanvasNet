using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests that <see cref="ScadGeometryBuilder"/> is iterative: trees far deeper than any recursive walk could
///     survive on a 64 KiB or 256 KiB caller stack build to exactly the same scene as on a big stack.
/// </summary>
public class ScadGeometryBuilderStackTests
{
    /// <summary>The stack size, in bytes, of a very small caller.</summary>
    private const int TinyStack = 64 * 1024;

    /// <summary>The stack size, in bytes, of a caller with plenty of stack.</summary>
    private const int BigStack = 256 * 1024 * 1024;

    /// <summary>The nesting depth of the hand-made trees: far beyond what a recursive walk survives on any stack.</summary>
    private const int HandMadeDepth = 100_000;

    /// <summary>The fragment settings of the hand-made instances.</summary>
    private static readonly ScadFragmentSettings Defaults = new(0, 12, 2);

    /// <summary>
    ///     Creates a chain of nested groups, each holding a cube and the next group, ending in a cube.
    /// </summary>
    /// <param name="depth">The number of nested groups.</param>
    /// <returns>The outermost instance.</returns>
    private static ScadInstance Chain(int depth)
    {
        ScadInstance Cube() =>
            new(ScadInstanceKind.BuiltinModule, "cube", [new ScadInstanceArgument(null, N(1))], [], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));

        var node = Cube();
        for (var i = 0; i < depth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.Group, "m", [], [Cube(), node], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));
        }

        return node;
    }

    /// <summary>
    ///     Builds a hand-made tree and describes the scene.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The mesh count, the scene depth and the total volume.</returns>
    private static (int Meshes, int Depth, double Volume) BuildChain(int depth)
    {
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var built = ScadGeometryBuilder.Build([Chain(depth)], Settings(), sink);
        var meshes = MeshNodes(built.Scene);
        return (meshes.Count, Depth(built.Scene), meshes.Sum(m => m.Mesh.SignedVolume));
    }

    /// <summary>
    ///     Validates that a 100,000-level tree builds on a 64 KiB and a 256 KiB caller exactly as on a big stack.
    /// </summary>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_HandMadeDeepTree_OnSmallStack_MatchesBigStack(int stackSize)
    {
        // Arrange / Act
        var big = OnSmallStack(() => BuildChain(HandMadeDepth), BigStack);
        var small = OnSmallStack(() => BuildChain(HandMadeDepth), stackSize);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal(HandMadeDepth + 1, big.Meshes);
        Assert.Equal(HandMadeDepth + 1, big.Depth);
        Assert.Equal(HandMadeDepth + 1.0, big.Volume, 1e-6);
    }

    /// <summary>
    ///     Validates that a deep chain of user modules written in source (the deepest the evaluator allows) builds
    ///     through the whole front end on small caller stacks to the same scene as on a big stack.
    /// </summary>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepModuleChainFromSource_OnSmallStack_MatchesBigStack(int stackSize)
    {
        // Arrange: each level holds a cube and the next level; 5,000 levels need about 15,000 evaluation frames
        const string source = "module r(n) { cube(1); if (n > 0) r(n - 1); }\nr(5000);";
        var evaluation = new ScadEvaluationSettings { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var big = OnSmallStack(() => Describe(Build(source, evaluation: evaluation)), BigStack);
        var small = OnSmallStack(() => Describe(Build(source, evaluation: evaluation)), stackSize);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal(5001, big.Meshes);
        Assert.True(big.Depth > 4999);
    }

    /// <summary>
    ///     Validates that a deep <c>!</c> search (a mark at the bottom of a deep tree) is also iterative.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_DeepShowOnlySearch_OnSmallStack_FindsTheMark()
    {
        // Arrange: the mark sits at the bottom of a 100,000-level tree
        var deepest = new ScadInstance(
            ScadInstanceKind.BuiltinModule,
            "cube",
            [new ScadInstanceArgument(null, N(2))],
            [],
            ScadModifiers.ShowOnly,
            Defaults,
            new ScadSourceLocation(null, 1, 1));
        var node = deepest;
        for (var i = 0; i < HandMadeDepth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.Group, "m", [], [node], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));
        }

        // Act
        var volume = OnSmallStack(
            () => MeshNodes(ScadGeometryBuilder.Build([node], Settings(), new ScadEvaluationSink(ScadLimits.Default)).Scene).Single().Mesh.SignedVolume,
            TinyStack);

        // Assert
        Assert.Equal(8.0, volume, 1e-12);
    }

    /// <summary>
    ///     Builds a hand-made nest of differences (each removes a cube from the previous result) and describes it.
    /// </summary>
    /// <param name="name">The boolean module name.</param>
    /// <param name="depth">The number of nested booleans.</param>
    /// <returns>The mesh count, the scene depth and whether the root is a boolean node.</returns>
    private static (int Meshes, int Depth, bool RootIsCsg) BuildBooleanChain(string name, int depth)
    {
        ScadInstance Cube() =>
            new(ScadInstanceKind.BuiltinModule, "cube", [new ScadInstanceArgument(null, N(1))], [], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));

        var node = Cube();
        for (var i = 0; i < depth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.BuiltinModule, name, [], [node, Cube()], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));
        }

        var built = ScadGeometryBuilder.Build([node], Settings(), new ScadEvaluationSink(ScadLimits.Default));
        return (MeshNodes(built.Scene).Count, Depth(built.Scene), built.Scene.Root is CsgNode);
    }

    /// <summary>
    ///     Validates that a 20,000-level nest of differences or intersections builds on 64 KiB and 256 KiB callers to
    ///     exactly the scene a big stack gives (a node per level and an operand per level).
    /// </summary>
    /// <param name="name">The boolean module.</param>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData("difference", TinyStack)]
    [InlineData("difference", SmallStack)]
    [InlineData("intersection", TinyStack)]
    [InlineData("intersection", SmallStack)]
    public void ScadGeometryBuilder_Build_DeepBooleanChain_OnSmallStack_MatchesBigStack(string name, int stackSize)
    {
        // Arrange
        const int Levels = 20_000;

        // Act
        var big = OnSmallStack(() => BuildBooleanChain(name, Levels), BigStack);
        var small = OnSmallStack(() => BuildBooleanChain(name, Levels), stackSize);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal((Levels + 1, Levels + 1, true), big);
    }

    /// <summary>
    ///     Builds a hand-made extrusion of a nest of 2D differences (each removes a square from the previous result).
    /// </summary>
    /// <param name="depth">The number of nested 2D differences.</param>
    /// <returns>The mesh count, the scene depth and whether the root is a boolean node.</returns>
    private static (int Meshes, int Depth, bool RootIsCsg) BuildExtrudedBooleanChain(int depth)
    {
        ScadInstance Square() =>
            new(ScadInstanceKind.BuiltinModule, "square", [new ScadInstanceArgument(null, N(1))], [], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));

        var node = Square();
        for (var i = 0; i < depth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.BuiltinModule, "difference", [], [node, Square()], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));
        }

        var extrusion = new ScadInstance(ScadInstanceKind.BuiltinModule, "linear_extrude", [new ScadInstanceArgument(null, N(1))], [node], ScadModifiers.None, Defaults, new ScadSourceLocation(null, 1, 1));
        var built = ScadGeometryBuilder.Build([extrusion], Settings(), new ScadEvaluationSink(ScadLimits.Default));
        return (MeshNodes(built.Scene).Count, Depth(built.Scene), built.Scene.Root is CsgNode);
    }

    /// <summary>
    ///     Validates that extruding a 20,000-level nest of 2D differences (the distribution over the 2D tree) works
    ///     on 64 KiB and 256 KiB callers and gives the scene a big stack gives.
    /// </summary>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepExtrudedBooleanChain_OnSmallStack_MatchesBigStack(int stackSize)
    {
        // Arrange
        const int Levels = 20_000;

        // Act
        var big = OnSmallStack(() => BuildExtrudedBooleanChain(Levels), BigStack);
        var small = OnSmallStack(() => BuildExtrudedBooleanChain(Levels), stackSize);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal((Levels + 1, Levels + 1, true), big);
    }

    /// <summary>
    ///     Validates that a deep chain of 2D transforms written in source, extruded linearly and by rotation, builds
    ///     through the whole front end on small caller stacks.
    /// </summary>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepFlatTransformChainFromSource_OnSmallStack_Extrudes(int stackSize)
    {
        // Arrange
        const string linear = "module t(n) { if (n > 0) translate([1, 0]) t(n - 1); else square(1); }\nlinear_extrude(1) t(3000);";
        const string rotate = "module t(n) { if (n > 0) translate([1, 0]) t(n - 1); else square(1); }\nrotate_extrude($fn = 3) t(3000);";
        var evaluation = new ScadEvaluationSettings { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var linearBounds = OnSmallStack(() => Build(linear, evaluation: evaluation).Scene.ComputeBounds(), stackSize);
        var rotateBounds = OnSmallStack(() => Build(rotate, evaluation: evaluation).Scene.ComputeBounds(), stackSize);

        // Assert
        Assert.Equal(3000.0, linearBounds.Min.X, 1e-9);
        Assert.Equal(3001.0, linearBounds.Max.X, 1e-9);
        Assert.Equal(3001.0, rotateBounds.Max.X, 1e-9);
    }

    /// <summary>
    ///     Describes a built scene by its mesh count, depth and total volume.
    /// </summary>
    /// <param name="result">The build result.</param>
    /// <returns>The description.</returns>
    private static (int Meshes, int Depth, double Volume) Describe(ScadBuildResult result) =>
        (result.Meshes.Count, Depth(result.Scene), result.Meshes.Sum(m => m.Mesh.SignedVolume));
}
