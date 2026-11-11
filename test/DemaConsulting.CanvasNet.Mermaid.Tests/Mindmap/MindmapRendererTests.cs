using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Mindmap;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Mindmap;

/// <summary>
///     Unit tests for <c>MindmapRenderer</c>: the node data of <c>getData</c> (paddings and widths, checked against the
///     upstream vectors), the per-shape changes of <c>draw</c>, the single-precision box, the shapes' sizes and wrapping
///     widths, the section paint and edge widths of the style sheet, the uncut edges, the reports, the view box, the empty
///     mindmap, the render-time label limit, the pinned work and cancellation.
/// </summary>
public sealed class MindmapRendererTests
{
    /// <summary>The work of the mindmap of <see cref="MindmapRenderer_Draw_SmallMindmap_ChargesPinnedWork"/>.</summary>
    private const long PinnedWork = 1_271;

    /// <summary>A mindmap of one node of every shape under the root.</summary>
    private const string Shapes = "mindmap\nroot\n  a[Square]\n  b(Rounded)\n  c{{Hexagon}}\n  d((Circle))\n  e))Bang((\n  f)Cloud(\n  g\n";

    /// <summary>
    ///     The limits of the vectors: the defaults, with the deepest nesting allowed (one case is a chain of 40 nodes).
    /// </summary>
    private static readonly MermaidLimits VectorLimits = new() { MaxNestingDepth = 256 };

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Mindmaps);

    /// <summary>
    ///     Validates the padding and the width <c>getData</c> gives each node of an upstream vector under its site
    ///     configuration (the generator applies no frontmatter or directive configuration): the configured padding (doubled for a rectangle, a rounded rectangle and a
    ///     hexagon), <c>mindmap.maxNodeWidth</c>, and the look.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void MindmapRenderer_DataOf_UpstreamVector_MatchesMermaidPaddingAndWidth(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Mindmaps[index]!.AsObject();
        var name = (string)vector["name"]!;
        var expected = vector["expected"]!.AsObject();
        if (expected["error"] is not null)
        {
            return;
        }

        var source = Preprocessor.Run((string)vector["text"]!, null, MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var diagram = MindmapParser.Parse(source, detected, VectorLimits);
        var options = new MermaidRenderOptions { Config = (vector["site"] as JsonObject)?.ToJsonString() };
        var config = MermaidConfigResolver.Resolve(Preprocessor.Run("mindmap\n", null, MermaidLimits.Default), "mindmap", options, null);

        // Act
        var data = diagram.Nodes.Select(node => MindmapRenderer.DataOf(node, config)).ToList();

        // Assert
        var expectedNodes = expected["data"]!["nodes"]!.AsArray();
        Assert.Equal(expectedNodes.Count, data.Count);
        for (var i = 0; i < data.Count; i++)
        {
            var node = expectedNodes[i]!;
            Assert.Equal(((double)node["padding"]!, (double)node["width"]!), (data[i].Padding, data[i].Width ?? double.NaN));
            Assert.True((string)node["look"]! == data[i].Look, $"{name}: node {i} look {data[i].Look}");
            Assert.Equal((string)node["shape"]!, data[i].Shape);
            Assert.Equal((string)node["domId"]!, data[i].DomId);
        }
    }

    /// <summary>
    ///     Validates the changes <c>draw</c> makes per shape: the rounded rectangle's padding of 15 and the rectangle's of
    ///     10, a width of zero for both and the hexagon, and no change for the other shapes (the circle branch is dead).
    /// </summary>
    [Fact]
    public void MindmapRenderer_Prepared_PerShapeChanges_MatchDraw()
    {
        // Arrange
        LayoutNode Node(string shape) => new() { Id = "0", Shape = shape, Width = 200, Padding = 20 };

        // Act
        var prepared = new[] { "rounded", "rect", "hexagon", "mindmapCircle", "defaultMindmapNode", "cloud", "bang", "circle" }
            .Select(shape => MindmapRenderer.Prepared(Node(shape)))
            .Select(node => (node.Shape, node.Width, node.Padding))
            .ToList();

        // Assert
        Assert.Equal(
            [
                ("rounded", 0.0, 15.0), ("rect", 0.0, 10.0), ("hexagon", 0.0, 20.0), ("mindmapCircle", 200.0, 20.0),
                ("defaultMindmapNode", 200.0, 20.0), ("cloud", 200.0, 20.0), ("bang", 200.0, 20.0), ("circle", 200.0, 20.0),
            ],
            prepared.Select(p => (p.Shape, p.Width ?? double.NaN, p.Padding)));
    }

    /// <summary>
    ///     Validates the box the layout is given: each side in single precision and the size subtracted in single
    ///     precision, as the browser's <c>getBBox</c> computes it; a box on the 1/64-pixel grid is unchanged.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Box32_RoundsSidesToSinglePrecision()
    {
        // Act
        var odd = MindmapRenderer.Box32(new SceneRect(-50.1, -0.3, 100.2, 0.6));
        var grid = MindmapRenderer.Box32(new SceneRect(-44.6875, -17, 89.375, 34));

        // Assert
        Assert.Equal((double)(float)-50.1, odd.X);
        Assert.Equal((double)((float)50.1 - (float)-50.1), odd.Width);
        Assert.Equal((double)((float)0.3 - (float)-0.3), odd.Height);
        Assert.Equal(new SceneRect(-44.6875, -17, 89.375, 34), grid);
    }

    /// <summary>
    ///     Validates the sizes of the shapes under two paddings: a default node grows by four paddings across and one
    ///     down, a circle by two paddings, a hexagon (twice the padding, with its slanted sides) by the doubled padding,
    ///     and the rectangle and the rounded rectangle keep the fixed paddings <c>draw</c> gives them.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_Padding_SizesEachShapeAsMermaid()
    {
        // Act
        var small = SceneQuery.Nodes(SceneQuery.Layout(Shapes));
        var large = SceneQuery.Nodes(SceneQuery.Layout("---\nconfig:\n  mindmap:\n    padding: 20\n---\n" + Shapes));
        (double Width, double Height) Growth(string id) => (large[id].Box.Width - small[id].Box.Width, large[id].Box.Height - small[id].Box.Height);

        // Assert: g (default), a (rect), b (rounded), c (hexagon), d (circle)
        Assert.Equal((40.0, 10.0), Round(Growth("node_7")));
        Assert.Equal((0.0, 0.0), Round(Growth("node_1")));
        Assert.Equal((0.0, 0.0), Round(Growth("node_2")));
        Assert.Equal((30.0, 20.0), Round(Growth("node_3")));
        Assert.Equal((20.0, 20.0), Round(Growth("node_4")));
    }

    /// <summary>
    ///     Validates the wrapping widths: a default node's label wraps at <c>mindmap.maxNodeWidth</c>, and a rectangle's
    ///     (whose width <c>draw</c> clears) at <c>flowchart.wrappingWidth</c>.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_LongLabels_WrapAtTheShapesWidth()
    {
        // Arrange
        const string Words = "many words that make a label much wider than one hundred pixels";

        // Act
        var nodes = SceneQuery.Nodes(SceneQuery.Layout($"---\nconfig:\n  mindmap:\n    maxNodeWidth: 100\n---\nmindmap\nroot\n  {Words}\n  r[{Words}]\n"));

        // Assert: the default node's label is at most 100 wide (plus 4 paddings), the rectangle's at most 200 (plus 2 x 2 x 10)
        Assert.InRange(nodes["node_1"].Box.Width, 40, 140);
        Assert.InRange(nodes["node_2"].Box.Width, 160, 240);
        Assert.True(nodes["node_1"].Box.Height > nodes["node_2"].Box.Height, "the default node wraps on more lines");
    }

    /// <summary>
    ///     Validates the paint of the sheet: the root takes <c>git0</c> (its rules come after <c>.section--1</c>'s), each
    ///     child of the root the next section color, a deeper node its parent's, a default node's line the section's
    ///     inverse color 3 pixels wide and no stroke on the body, and a node's own class that names a later section takes
    ///     that section's color (its rule comes later in the sheet).
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_Sections_PaintNodesBySection()
    {
        // Arrange
        const string Text = "mindmap\nroot\n  a\n  :::section-9\n    a1\n  b\n  c\n";
        var config = MermaidConfigResolver.Resolve(Preprocessor.Run(Text, null, MermaidLimits.Default), "mindmap", null, null);
        Rgba32? Variable(string name) => StyleDeclarations.TryParsePaint(config.ThemeVariable(name)!, out var color) ? color : null;

        // Act
        var scene = SceneQuery.Layout(Text);

        // Assert
        Assert.Equal(Variable("git0"), Body(scene, "node_0").Fill);
        Assert.Equal(Variable("cScale10"), Body(scene, "node_1").Fill);
        Assert.Equal(Variable("cScale1"), Body(scene, "node_2").Fill);
        Assert.Equal(Variable("cScale2"), Body(scene, "node_3").Fill);
        Assert.Equal(Variable("cScale3"), Body(scene, "node_4").Fill);
        Assert.NotEqual(Variable("cScale1"), Variable("cScale10"));
        var line = Part(scene, "node_2", "node-line");
        Assert.Equal((Variable("cScaleInv1"), 3.0), (line.Stroke?.Color, line.Stroke?.Width ?? 0));
        Assert.Null(Body(scene, "node_2").Stroke);
    }

    /// <summary>
    ///     Validates the edges: the section color, the width of each indentation depth (17 less 3 per level, and the
    ///     sheet's 3 where that would be negative), and the route through cytoscape's points, which starts 15 pixels from
    ///     the parent's center and is not cut at the node's outline.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_Edges_FollowDepthAndAreNotCut()
    {
        // Arrange: each node one column deeper than its parent, so the depths are 1 to 6
        const string Text = "mindmap\nr\n a\n  b\n   c\n    d\n     e\n      f\n";

        // Act
        var scene = SceneQuery.Layout(Text);
        var edges = SceneQuery.Edges(scene);
        var nodes = SceneQuery.Nodes(scene);

        // Assert
        Assert.Equal([11.0, 8.0, 5.0, 2.0, 3.0, 3.0], new[] { "edge_0_1", "edge_1_2", "edge_2_3", "edge_3_4", "edge_4_5", "edge_5_6" }.Select(id => edges[id].Stroke!.Width));
        Assert.All(edges.Values, edge => Assert.Null(edge.Fill));
        var start = edges["edge_0_1"].Segments[0];
        var center = nodes["node_0"];
        Assert.Equal(15, Math.Sqrt(Math.Pow(start.X - center.X, 2) + Math.Pow(start.Y - center.Y, 2)), 1e-3);
    }

    /// <summary>
    ///     Validates the reports in order (look, layout, then each node's icon and Font Awesome label), the located icon,
    ///     and that the mindmap is still drawn with the classic look and the cose-bilkent layout.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_UnsupportedFeatures_AreReportedInOrder()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nmindmap\nroot\n  a\n  ::icon(fa fa-book)\n  b[fa:fa-car Car]\n";
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var scene = MermaidDocument.Parse(Text).LayoutScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [MermaidDiagnosticCodes.LookUnsupported, MermaidDiagnosticCodes.LayoutUnsupported, MermaidDiagnosticCodes.IconUnsupported, MermaidDiagnosticCodes.IconUnsupported],
            unsupported.Select(u => u.Feature));
        Assert.Contains("laid out with cose-bilkent", unsupported[1].Message, StringComparison.Ordinal);
        Assert.Equal("the icon 'fa fa-book' of node 'a' is not drawn; the node keeps its shape and label", unsupported[2].Message);
        Assert.Equal((9, 10), (unsupported[2].Location.Line, unsupported[2].Location.Column));
        Assert.Equal(10, unsupported[3].Location.Line);
        Assert.Equal(3, SceneQuery.Nodes(scene).Count);
    }

    /// <summary>
    ///     Validates that a mindmap with the default layout and look, no icon and no Font Awesome label gives no report.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_PlainMindmap_ReportsNothing()
    {
        // Arrange
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var scene = MermaidDocument.Parse(Shapes + "    **bold** `code`\n").LayoutScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(9, SceneQuery.Nodes(scene).Count);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
    }

    /// <summary>
    ///     Validates the view box: the box of the drawing grown by <c>mindmap.padding</c>, with the layout's margin of 15
    ///     at the top and the left.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_ViewBox_IsTheDrawingGrownByThePadding()
    {
        // Act
        var scene = SceneQuery.Layout("---\nconfig:\n  mindmap:\n    padding: 7\n---\nmindmap\nroot\n  a\n  b\n  c\n");
        var boxes = SceneQuery.Nodes(scene).Values.Select(node => node.Box).ToList();

        // Assert
        var left = boxes.Min(box => box.X);
        var top = boxes.Min(box => box.Y);
        Assert.Equal((15.0, 15.0), (Math.Round(left, 9), Math.Round(top, 9)));
        Assert.Equal(left - 7, scene.ViewBox.X, 1e-4);
        Assert.Equal(top - 7, scene.ViewBox.Y, 1e-4);
        Assert.Equal(boxes.Max(box => box.Right) + 7, scene.ViewBox.Right, 1e-4);
        Assert.Equal(boxes.Max(box => box.Bottom) + 7, scene.ViewBox.Bottom, 1e-4);
    }

    /// <summary>
    ///     Validates the empty mindmap (the keyword alone): nothing is drawn and the view box is the empty box grown by the
    ///     padding.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_EmptyMindmap_GivesThePaddedEmptyBox()
    {
        // Act
        var scene = SceneQuery.Layout("mindmap\n");
        var padded = SceneQuery.Layout("---\nconfig:\n  mindmap:\n    padding: 5\n---\nmindmap\n");

        // Assert
        Assert.Equal(new SceneRect(-10, -10, 20, 20), scene.ViewBox);
        Assert.Equal(new SceneRect(-5, -5, 10, 10), padded.ViewBox);
        Assert.Empty(scene.Walk());
    }

    /// <summary>
    ///     Validates that a label longer than the render's <see cref="MermaidLimits.MaxLabelLength"/> fails at render,
    ///     located at its node, though the document was opened under larger limits.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_LabelOverTheRenderLimit_IsALocatedLimit()
    {
        // Arrange
        var document = MermaidDocument.Parse("mindmap\nroot\n  a label of some length\n");
        var options = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLabelLength = 10 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(3, exception.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the pinned work of a small mindmap: it is drawn with a work limit of exactly its work, and fails,
    ///     located at the keyword, with one step less.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_SmallMindmap_ChargesPinnedWork()
    {
        // Arrange
        const long Work = PinnedWork;
        var document = MermaidDocument.Parse("mindmap\nroot\n  a\n  b[B]\n");
        MermaidRenderOptions Limited(long work) => new() { Limits = new MermaidLimits { MaxLayoutWork = work } };

        // Act
        var scene = document.LayoutScene(Limited(Work), TestContext.Current.CancellationToken);
        var exception = Assert.Throws<MermaidRenderException>(() => document.LayoutScene(Limited(Work - 1), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(3, SceneQuery.Nodes(scene).Count);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 1), (exception.Code, exception.Location.Line));
    }

    /// <summary>
    ///     Validates that a render whose token is canceled stops at the work meter with
    ///     <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void MindmapRenderer_Draw_Canceled_Throws()
    {
        // Arrange
        var source = Preprocessor.Run(Shapes, null, MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var diagram = MindmapParser.Parse(source, detected, MermaidLimits.Default);
        var options = new MermaidRenderOptions();
        var config = MermaidConfigResolver.Resolve(source, "mindmap", options, null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DiagramRenderContext(source, detected, options, config, new MermaidReportSink(100, false), ScenePainter.CreateFontResolver(), cancellation.Token);

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => new MindmapRenderer(diagram, context).Draw());
    }

    /// <summary>
    ///     Rounds a size difference to whole pixels of a thousandth.
    /// </summary>
    /// <param name="size">The difference.</param>
    /// <returns>The rounded difference.</returns>
    private static (double Width, double Height) Round((double Width, double Height) size) =>
        (Math.Round(size.Width, 3), Math.Round(size.Height, 3));

    /// <summary>
    ///     Gets the body of a node: its part of role <c>node</c>.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="domId">The node's DOM id.</param>
    /// <returns>The body.</returns>
    private static ScenePath Body(Scene scene, string domId) => Part(scene, domId, "node");

    /// <summary>
    ///     Gets a part of a node by role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="domId">The node's DOM id.</param>
    /// <param name="role">The role.</param>
    /// <returns>The part.</returns>
    private static ScenePath Part(Scene scene, string domId, string role) =>
        scene.Walk().Select(entry => entry.Item).OfType<ScenePath>().First(path => path.Role == role && path.Id == domId);
}
