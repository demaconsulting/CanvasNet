// cspell:ignore scalings
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests that deep chains of transforms, colors and 2D transforms build iteratively: on 64 KiB and 256 KiB
///     caller stacks they give the same, analytically known, scene as on a big stack.
/// </summary>
public class ScadGeometryBuilderTransformDepthTests
{
    /// <summary>The stack size, in bytes, of a very small caller.</summary>
    private const int TinyStack = 64 * 1024;

    /// <summary>The stack size, in bytes, of a caller with plenty of stack.</summary>
    private const int BigStack = 256 * 1024 * 1024;

    /// <summary>The nesting depth of the hand-made chains.</summary>
    private const int HandMadeDepth = 100_000;

    /// <summary>The fragment settings of the hand-made instances.</summary>
    private static readonly ScadFragmentSettings Defaults = new(0, 12, 2);

    /// <summary>The location of the hand-made instances.</summary>
    private static readonly ScadSourceLocation Here = new(null, 1, 1);

    /// <summary>
    ///     Creates a chain of nested single-argument modules around a leaf.
    /// </summary>
    /// <param name="name">The module name of every level.</param>
    /// <param name="argument">The argument of every level.</param>
    /// <param name="leaf">The innermost instance.</param>
    /// <param name="depth">The number of levels.</param>
    /// <returns>The outermost instance.</returns>
    private static ScadInstance Chain(string name, ScadValue argument, ScadInstance leaf, int depth)
    {
        var node = leaf;
        for (var i = 0; i < depth; i++)
        {
            node = new ScadInstance(ScadInstanceKind.BuiltinModule, name, [new ScadInstanceArgument(null, argument)], [node], ScadModifiers.None, Defaults, Here);
        }

        return node;
    }

    /// <summary>
    ///     Creates a primitive with one positional argument.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The instance.</returns>
    private static ScadInstance Primitive(string name, ScadValue argument) =>
        new(ScadInstanceKind.BuiltinModule, name, [new ScadInstanceArgument(null, argument)], [], ScadModifiers.None, Defaults, Here);

    /// <summary>
    ///     Builds a hand-made tree on a caller with the given stack and describes its bounds.
    /// </summary>
    /// <param name="root">The tree.</param>
    /// <param name="stackSize">The caller's stack size.</param>
    /// <returns>The minimum and maximum X of the scene.</returns>
    private static (double Min, double Max, int Meshes) BuildBounds(ScadInstance root, int stackSize) =>
        OnSmallStack(
            () =>
            {
                var built = ScadGeometryBuilder.Build([root], Settings(), new ScadEvaluationSink(ScadLimits.Default));
                var bounds = built.Scene.ComputeBounds();
                return (bounds.Min.X, bounds.Max.X, MeshNodes(built.Scene).Count);
            },
            stackSize);

    /// <summary>
    ///     Validates that 100,000 nested translations compose to the exact sum on small stacks.
    /// </summary>
    /// <param name="stackSize">The caller's stack size.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepTranslateChain_OnSmallStack_ComposesExactly(int stackSize)
    {
        // Arrange
        var root = Chain("translate", Nums(1, 0, 0), Primitive("cube", N(1)), HandMadeDepth);

        // Act
        var big = BuildBounds(root, BigStack);
        var small = BuildBounds(root, stackSize);

        // Assert
        Assert.Equal(big, small);
        Assert.Equal(((double)HandMadeDepth, HandMadeDepth + 1.0, 1), small);
    }

    /// <summary>
    ///     Validates that 10,000 nested non-uniform scalings compose to the closed-form product on small stacks.
    /// </summary>
    /// <param name="stackSize">The caller's stack size.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepScaleChain_OnSmallStack_ComposesToTheProduct(int stackSize)
    {
        // Arrange
        const int Levels = 10_000;
        var root = Chain("scale", Nums(1.00001, 1, 1), Primitive("cube", N(1)), Levels);

        // Act
        var (min, max, meshes) = BuildBounds(root, stackSize);

        // Assert
        Assert.Equal(0.0, min, 1e-12);
        Assert.Equal(Math.Pow(1.00001, Levels), max, 1e-9);
        Assert.Equal(1, meshes);
    }

    /// <summary>
    ///     Validates that 100,000 nested colors build on a small stack and the innermost cube keeps the innermost
    ///     color (the walk of the scene's appearances is iterative too).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_DeepColorChain_OnSmallStack_Builds()
    {
        // Arrange
        var root = Chain("color", S("red"), Chain("color", S("blue"), Primitive("cube", N(1)), 1), HandMadeDepth);

        // Act
        var maxX = OnSmallStack(
            () => ScadGeometryBuilder.Build([root], Settings(), new ScadEvaluationSink(ScadLimits.Default)).Scene.ComputeBounds().Max.X,
            TinyStack);

        // Assert
        Assert.Equal(1.0, maxX, 1e-12);
    }

    /// <summary>
    ///     Validates that 100,000 nested translations of a 2D shape build its slab at the exact position on a small
    ///     stack (the 2D tree is built, transformed and extruded iteratively).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_DeepFlatTranslateChain_OnSmallStack_ComposesExactly()
    {
        // Arrange
        var root = Chain("translate", Nums(1, 0), Primitive("square", N(1)), HandMadeDepth);

        // Act
        var bounds = BuildBounds(root, TinyStack);

        // Assert
        Assert.Equal(((double)HandMadeDepth, HandMadeDepth + 1.0, 1), bounds);
    }

    /// <summary>
    ///     Validates a deep chain of transforms written in source, through the whole front end, on small stacks.
    /// </summary>
    /// <param name="stackSize">The caller's stack size.</param>
    [Theory]
    [InlineData(TinyStack)]
    [InlineData(SmallStack)]
    public void ScadGeometryBuilder_Build_DeepTranslateChainFromSource_OnSmallStack_ComposesExactly(int stackSize)
    {
        // Arrange
        const string source = "module t(n) { if (n > 0) translate([1, 0, 0]) t(n - 1); else cube(1); }\nt(3000);";
        var evaluation = new ScadEvaluationSettings { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var bounds = OnSmallStack(() => Build(source, evaluation: evaluation).Scene.ComputeBounds(), stackSize);

        // Assert
        Assert.Equal(3000.0, bounds.Min.X, 1e-9);
        Assert.Equal(3001.0, bounds.Max.X, 1e-9);
    }
}
