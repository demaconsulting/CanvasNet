using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     The outcome of evaluating and building a program in a test: the scene, its location map and the events the
///     evaluation and the geometry builder reported.
/// </summary>
/// <param name="Scene">The scene.</param>
/// <param name="Map">The node-to-location map.</param>
/// <param name="Warnings">The warnings, in order.</param>
/// <param name="Unsupported">The unsupported-feature reports, in order.</param>
internal sealed record ScadBuildResult(
    Scene Scene,
    ScadSceneMap Map,
    IReadOnlyList<ScadDiagnostic> Warnings,
    IReadOnlyList<ScadUnsupportedFeature> Unsupported)
{
    /// <summary>
    ///     Gets the warnings as <c>line:column: message</c>.
    /// </summary>
    public string[] WarningTexts => [.. Warnings.Select(w => $"{w.Location}: {w.Message}")];

    /// <summary>
    ///     Gets the feature identifiers of the unsupported-feature reports, in order.
    /// </summary>
    public string[] Features => [.. Unsupported.Select(u => u.Feature)];

    /// <summary>
    ///     Gets the mesh nodes of the scene in document order.
    /// </summary>
    public IReadOnlyList<MeshNode> Meshes => ScadGeometryTestSupport.MeshNodes(Scene);

    /// <summary>
    ///     Gets the mesh of the scene, asserting that the scene holds exactly one.
    /// </summary>
    public Mesh SingleMesh
    {
        get
        {
            var meshes = Meshes;
            Assert.Single(meshes);
            return meshes[0].Mesh;
        }
    }
}

/// <summary>
///     Shared helpers of the geometry tests: building a program, inspecting a scene and comparing surfaces.
/// </summary>
internal static class ScadGeometryTestSupport
{
    /// <summary>The stack size, in bytes, of the deliberately small caller threads.</summary>
    public const int SmallStack = 256 * 1024;

    /// <summary>
    ///     Gets the default geometry settings of the tests.
    /// </summary>
    /// <param name="maxTriangles">The triangle budget.</param>
    /// <param name="strict">Whether unsupported features throw.</param>
    /// <param name="thickness">The slab thickness of flat shapes.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The settings.</returns>
    public static ScadGeometrySettings Settings(
        long maxTriangles = 5_000_000,
        bool strict = false,
        double thickness = 0.1,
        CancellationToken? cancellation = null) =>
        new(maxTriangles, thickness, strict, cancellation ?? TestContext.Current.CancellationToken);

    /// <summary>
    ///     Evaluates a program and builds its geometry.
    /// </summary>
    /// <param name="source">The program text.</param>
    /// <param name="settings">The geometry settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="evaluation">The evaluation settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="files">The virtual files for <c>include</c>/<c>use</c>, or <see langword="null"/>.</param>
    /// <param name="sink">The sink to use, or <see langword="null"/> to create one.</param>
    /// <returns>The scene and the reports.</returns>
    public static ScadBuildResult Build(
        string source,
        ScadGeometrySettings? settings = null,
        ScadEvaluationSettings? evaluation = null,
        ScadVirtualFiles? files = null,
        ScadEvaluationSink? sink = null)
    {
        var effectiveEvaluation = evaluation ?? new ScadEvaluationSettings();
        var effectiveSink = sink ?? new ScadEvaluationSink(effectiveEvaluation.Limits);
        var output = ScadEvaluatorTestSupport.Run(source, effectiveEvaluation, files, effectiveSink);
        var built = ScadGeometryBuilder.Build(output.Roots, settings ?? Settings(cancellation: TestContext.Current.CancellationToken), effectiveSink);
        return new ScadBuildResult(built.Scene, built.Map, [.. effectiveSink.Diagnostics], [.. effectiveSink.UnsupportedFeatures]);
    }

    /// <summary>
    ///     Lists the mesh nodes of a scene in document order, walking iteratively.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The mesh nodes.</returns>
    public static IReadOnlyList<MeshNode> MeshNodes(Scene scene)
    {
        var result = new List<MeshNode>();
        var stack = new Stack<SceneNode>();
        stack.Push(scene.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            switch (node)
            {
                case MeshNode mesh:
                    result.Add(mesh);
                    break;
                case GroupNode group:
                    PushReversed(stack, group.Children);
                    break;
                case CsgNode csg:
                    PushReversed(stack, csg.Children);
                    break;
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the maximum nesting depth of a scene, walking iteratively (a lone root has depth one).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The depth.</returns>
    public static int Depth(Scene scene)
    {
        var deepest = 0;
        var stack = new Stack<(SceneNode Node, int Depth)>();
        stack.Push((scene.Root, 1));
        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            deepest = Math.Max(deepest, depth);
            IReadOnlyList<SceneNode> children = node switch
            {
                GroupNode group => group.Children,
                CsgNode csg => csg.Children,
                _ => [],
            };
            foreach (var child in children)
            {
                stack.Push((child, depth + 1));
            }
        }

        return deepest;
    }

    /// <summary>
    ///     Asserts that two surfaces have the same size and identical pixel bytes.
    /// </summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    public static void AssertSurfacesIdentical(Surface expected, Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            Assert.True(expected.GetRowSpanBytes(y).SequenceEqual(actual.GetRowSpanBytes(y)), $"Row {y} differs.");
        }
    }

    /// <summary>
    ///     Counts the pixels of a surface that differ from a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="background">The color to exclude.</param>
    /// <returns>The number of pixels that are not <paramref name="background"/>.</returns>
    public static int CountNon(Surface surface, Rgba32 background)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel != background)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Computes the exact area of a regular polygon inscribed in a circle.
    /// </summary>
    /// <param name="radius">The radius of the circle the polygon is inscribed in.</param>
    /// <param name="sides">The number of sides.</param>
    /// <returns>The area, <c>n / 2 * r^2 * sin(2 pi / n)</c>.</returns>
    public static double RegularPolygonArea(double radius, int sides) =>
        sides / 2.0 * radius * radius * Math.Sin(2.0 * Math.PI / sides);

    /// <summary>
    ///     Pushes children so that the first child is popped first.
    /// </summary>
    /// <param name="stack">The stack.</param>
    /// <param name="children">The children.</param>
    private static void PushReversed(Stack<SceneNode> stack, IReadOnlyList<SceneNode> children)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push(children[i]);
        }
    }
}
