using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Flowchart;

/// <summary>
///     Unit tests of <see cref="FlowchartModel"/>: the database operations, the layout data mapping and the reports of
///     unsupported features.
/// </summary>
public class FlowchartModelTests
{
    /// <summary>The location used for elements added directly.</summary>
    private static readonly MermaidLocation Here = new(null, 1, 1);

    /// <summary>
    ///     Validates how link tokens are read (Mermaid's <c>destructLink</c>): arrow type, stroke and length.
    /// </summary>
    /// <param name="link">The link token.</param>
    /// <param name="start">The start of a link with text, or null.</param>
    /// <param name="type">The arrow type.</param>
    /// <param name="stroke">The stroke.</param>
    /// <param name="length">The length, or -1 for none.</param>
    [Theory]
    [InlineData("-->", null, "arrow_point", "normal", 1)]
    [InlineData(" --- ", null, "arrow_open", "normal", 1)]
    [InlineData("---->", null, "arrow_point", "normal", 3)]
    [InlineData("-.->", null, "arrow_point", "dotted", 1)]
    [InlineData("-...-", null, "arrow_open", "dotted", 3)]
    [InlineData("==>", null, "arrow_point", "thick", 1)]
    [InlineData("~~~", null, "arrow_open", "invisible", 1)]
    [InlineData("<-->", null, "double_arrow_point", "normal", 1)]
    [InlineData("x--x", null, "double_arrow_cross", "normal", 1)]
    [InlineData("o==o", null, "double_arrow_circle", "thick", 1)]
    [InlineData("-->", "--", "arrow_point", "normal", 1)]
    [InlineData("-->", "<--", "double_arrow_point", "normal", 1)]
    [InlineData(".->", "-.", "arrow_point", "dotted", 1)]
    [InlineData("~~~", "--", "INVALID", "INVALID", -1)]
    [InlineData("-->", "x--", "INVALID", "INVALID", -1)]
    public void FlowchartModel_DestructLink_Token_GivesTypeStrokeAndLength(string link, string? start, string type, string stroke, int length)
    {
        // Act
        var result = FlowchartModel.DestructLink(link, start, null, null);

        // Assert
        Assert.Equal((type, stroke, length < 0 ? null : length), (result.Type, result.Stroke, result.Length));
    }

    /// <summary>
    ///     Validates that a vertex declared again keeps its DOM id and takes the new label and shape, while a plain
    ///     mention keeps the label.
    /// </summary>
    [Fact]
    public void FlowchartModel_AddVertex_Redeclared_MergesIntoFirst()
    {
        // Arrange
        var model = NewModel();

        // Act
        model.AddVertex("A", Here, new FlowText("First", "text"), "square");
        model.AddVertex("B", Here);
        model.AddVertex("A", Here, new FlowText("\"Second\"", "string"), "round", ["fill:red"]);
        model.AddVertex("A", Here);

        // Assert
        var a = model.Vertices[0];
        Assert.Equal(("flowchart-A-0", "Second", "string", "round"), (a.DomId, a.Text, a.LabelType, a.Type));
        Assert.Equal(["fill:red"], a.Styles);
        Assert.Equal("flowchart-B-1", model.Vertices[1].DomId);
    }

    /// <summary>
    ///     Validates the generated edge ids: repeated pairs count from 0 and skip 1, a given id is used once.
    /// </summary>
    [Fact]
    public void FlowchartModel_AddLink_RepeatedPairsAndIds_GetMermaidIds()
    {
        // Arrange
        var model = NewModel();
        var plain = FlowchartModel.DestructLink("-->", null, null, null);
        var named = FlowchartModel.DestructLink("-->", null, null, "e1@");

        // Act
        model.AddLink(["a"], ["b"], plain, Here);
        model.AddLink(["a"], ["b"], plain, Here);
        model.AddLink(["a"], ["b"], named, Here);
        model.AddLink(["a"], ["b"], named, Here);

        // Assert
        Assert.Equal(["L_a_b_0", "L_a_b_2", "e1", "L_a_b_4"], model.Edges.Select(e => e.Id));
        Assert.Equal([false, false, true, false], model.Edges.Select(e => e.IsUserDefinedId));
    }

    /// <summary>
    ///     Validates <c>classDef</c> styles: commas split, an escaped comma stays, a color also makes a text style.
    /// </summary>
    [Fact]
    public void FlowchartModel_AddClass_Styles_SplitAndTextStyles()
    {
        // Arrange
        var model = NewModel();

        // Act
        model.AddClass("c,d", ["fill:#f00", "color:#fff", "font-family:a\\", "b"]);

        // Assert
        var c = model.Classes[0];
        Assert.Equal(["c", "d"], model.Classes.Select(k => k.Id));
        Assert.Equal(["fill:#f00", "color:#fff", "font-family:a,b"], c.Styles);
        Assert.Equal(["color:#fff"], c.TextStyles);
    }

    /// <summary>
    ///     Validates that the edges of one <c>linkStyle</c> share one style list that gets <c>fill:none</c> once.
    /// </summary>
    [Fact]
    public void FlowchartModel_UpdateLink_SeveralEdges_ShareOneList()
    {
        // Arrange
        var model = NewModel();
        var link = FlowchartModel.DestructLink("-->", null, null, null);
        model.AddLink(["a"], ["b", "c"], link, Here);

        // Act
        model.UpdateLink(["0", "1"], ["stroke:red"], Here);

        // Assert
        Assert.Same(model.Edges[0].Style, model.Edges[1].Style);
        Assert.Equal(["stroke:red", "fill:none"], model.Edges[0].Style!);
    }

    /// <summary>
    ///     Validates that a <c>linkStyle</c> index that names no edge (or is not a plain index) is a located error.
    /// </summary>
    /// <param name="index">The index as written.</param>
    [Theory]
    [InlineData("1")]
    [InlineData("00")]
    public void FlowchartModel_UpdateLink_IndexOutOfRange_ThrowsLocated(string index)
    {
        // Arrange
        var model = NewModel();
        model.AddLink(["a"], ["b"], FlowchartModel.DestructLink("-->", null, null, null), Here);
        var location = new MermaidLocation("f.mmd", 3, 3);

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => model.UpdateLink([index], ["stroke:red"], location));

        // Assert
        Assert.Equal(location, ex.Location);
        Assert.Contains("use 0 to 0", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates subgraph ids and members: a title with spaces gets a generated id, members of an earlier subgraph
    ///     are not members again.
    /// </summary>
    [Fact]
    public void FlowchartModel_AddSubgraph_IdsAndMembers_FollowMermaid()
    {
        // Arrange
        var model = NewModel();
        var title = new FlowText("My group", "text");

        // Act
        var first = model.AddSubgraph(title, [new DocumentItem("a", null), new DocumentItem("b", null)], title, true, Here);
        var second = model.AddSubgraph(
            new FlowText("two", "text"),
            [new DocumentItem("b", null), new DocumentItem(";", null), new DocumentItem("\n", null), new DocumentItem("c", null), new DocumentItem(null, "LR")],
            new FlowText("Two", "markdown"),
            false,
            Here);

        // Assert
        Assert.Equal(("subGraph0", "two"), (first, second));
        Assert.Equal(["a", "b"], model.Subgraphs[0].Nodes);
        Assert.Equal([";", "c"], model.Subgraphs[1].Nodes);
        Assert.Equal(("LR", "markdown"), (model.Subgraphs[1].Direction, model.Subgraphs[1].LabelType));
    }

    /// <summary>
    ///     Validates the metadata counts: subgraph ids used as vertices are not nodes.
    /// </summary>
    [Fact]
    public void FlowchartModel_Counts_ExcludeSubgraphVertices()
    {
        // Act
        var model = Parse("flowchart LR\n  subgraph s\n    a\n  end\n  s --> b\n");

        // Assert
        Assert.Equal((2, 1, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Null(model.AccTitle);
        Assert.Null(model.Title);
    }

    /// <summary>
    ///     Validates the shapes of the layout data: legacy bracket names, images and icon forms.
    /// </summary>
    [Fact]
    public void FlowchartModel_GetData_Shapes_FollowGetTypeFromVertex()
    {
        // Arrange
        var model = Parse("flowchart LR\n  a[x] --> b(y)\n  c(-z-)\n  d@{ img: \"p.png\" }\n  e@{ icon: \"fa:car\", form: circle }\n  f@{ shape: diam }\n");

        // Act
        var data = model.GetData(Config("flowchart LR\n"), "flowchart-v2");

        // Assert
        Assert.Equal(
            ["squareRect", "roundedRect", "ellipse", "imageSquare", "iconCircle", "diam"],
            data.Nodes.Select(n => n.Shape));
        Assert.Equal(["point", "circle", "cross"], data.Markers);
        Assert.Equal(("LR", 50.0, 50.0, "dagre"), (data.Direction, data.NodeSpacing, data.RankSpacing, data.LayoutAlgorithm));
    }

    /// <summary>
    ///     Validates collapsed subgraphs: their content is hidden, crossing edges end at them, inner edges disappear.
    /// </summary>
    [Fact]
    public void FlowchartModel_GetData_CollapsedSubgraph_RedirectsEdges()
    {
        // Arrange
        var model = Parse("flowchart TB\n  subgraph s\n    a --> b\n  end\n  s@{ view: collapsed }\n  x --> a\n  b --> y\n");

        // Act
        var data = model.GetData(Config("flowchart TB\n"), "flowchart-v2");

        // Assert
        Assert.Equal(["s", "x", "y"], data.Nodes.Select(n => n.Id));
        Assert.Equal(("collapsedGroup", false), (data.Nodes[0].Shape, data.Nodes[0].IsGroup));
        Assert.Equal([("x", "s"), ("s", "y")], data.Edges.Select(e => (e.Start, e.End)));
        Assert.Empty(data.Clusters);
    }

    /// <summary>
    ///     Validates the regression that subgraphs nested through their ids (a subgraph listed in another one that is
    ///     declared elsewhere, before or after it) count against <see cref="MermaidLimits.MaxNestingDepth"/>: a chain of
    ///     four with X inside all of them fails a limit of 3, located at X, also when the root S0 is collapsed and hides
    ///     the whole chain from the layout, and a chain of three passes with the parents the ids give.
    /// </summary>
    [Fact]
    public void FlowchartModel_GetData_SubgraphsNestedThroughIds_ThrowsLimitExceeded()
    {
        // Arrange: subgraph Si lists S(i+1) on lines 2 + 3i to 4 + 3i; the last lists X; the reused chain declares S1 first
        static string Chain(int count) =>
            "flowchart TB\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"subgraph S{i}\n{(i == count - 1 ? "X" : $"S{i + 1}")}\nend\n"));
        const string Reused = "flowchart TB\nsubgraph S1\nX\nend\nsubgraph S0\nS1\nend\n";
        var options = new MermaidOpenOptions { Limits = new MermaidLimits { MaxNestingDepth = 3 } };
        var refused = Assert.IsType<FlowchartModel>(MermaidDocument.Parse(Chain(4), options).Diagram);
        var accepted = Assert.IsType<FlowchartModel>(MermaidDocument.Parse(Chain(3), options).Diagram);
        var reused = Assert.IsType<FlowchartModel>(MermaidDocument.Parse(Reused, options).Diagram);
        var collapsed = Assert.IsType<FlowchartModel>(MermaidDocument.Parse(Chain(4) + "S0@{ view: collapsed }\n", options).Diagram);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => refused.GetData(Config(Chain(4)), "flowchart-v2"));
        var hiddenEx = Assert.Throws<MermaidRenderException>(() => collapsed.GetData(Config(Chain(4)), "flowchart-v2"));
        var data = accepted.GetData(Config(Chain(3)), "flowchart-v2");
        var reusedData = reused.GetData(Config(Reused), "flowchart-v2");

        // Assert: X is inside S3, S2, S1 and S0
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(12, ex.Location.Line);
        Assert.Contains("subgraphs are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)", ex.Message, StringComparison.Ordinal);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 12), (hiddenEx.Code, hiddenEx.Location.Line));
        Assert.Equal(("S2", "S1", "S0"), (data.Nodes.Single(n => n.Id == "X").ParentId, data.Nodes.Single(n => n.Id == "S2").ParentId, data.Nodes.Single(n => n.Id == "S1").ParentId));
        Assert.Equal(("S1", "S0"), (reusedData.Nodes.Single(n => n.Id == "X").ParentId, reusedData.Nodes.Single(n => n.Id == "S1").ParentId));
    }

    /// <summary>
    ///     Validates that <c>flowchart.inheritDir</c> gives subgraphs without a direction statement the diagram's.
    /// </summary>
    [Fact]
    public void FlowchartModel_GetData_InheritDirection_GivesDiagramDirection()
    {
        // Arrange
        var model = Parse("flowchart LR\n  subgraph s\n    a\n  end\n");
        const string Inherit = "---\nconfig:\n  flowchart:\n    inheritDir: true\n---\nflowchart LR\n";

        // Act
        var plain = model.GetData(Config("flowchart LR\n"), "flowchart-v2");
        var inherited = model.GetData(Config(Inherit), "flowchart-v2");

        // Assert
        Assert.Null(plain.Clusters.Single().Dir);
        Assert.Equal("LR", inherited.Clusters.Single().Dir);
    }

    /// <summary>
    ///     Validates the reports of what is not drawn, in order: the look, the layout, then the icons.
    /// </summary>
    [Fact]
    public void FlowchartModel_ReportUnsupported_LookLayoutAndIcons_AreReportedInOrder()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nflowchart LR\n  a@{ icon: \"fa:car\" } --> b[fa:fa-user User]\n";
        var model = Parse(Text);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        model.ReportUnsupported(Config(Text), sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(
            [MermaidDiagnosticCodes.LookUnsupported, MermaidDiagnosticCodes.LayoutUnsupported, MermaidDiagnosticCodes.IconUnsupported, MermaidDiagnosticCodes.IconUnsupported],
            reports.Select(r => r.Feature));
        Assert.Equal([3, 4, 7, 7], reports.Select(r => r.Location.Line));
        Assert.All(reports, r => Assert.NotNull(r.Hint));
    }

    /// <summary>
    ///     Validates that laying the model out delivers its reports and then draws the diagram.
    /// </summary>
    [Fact]
    public void FlowchartModel_Layout_IconLabel_ReportsThenRenders()
    {
        // Arrange
        var document = MermaidDocument.Parse("flowchart LR\n  a[fa:fa-car Car]\n");
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Equal(MermaidDiagnosticCodes.IconUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Creates an empty model.
    /// </summary>
    /// <returns>The model.</returns>
    private static FlowchartModel NewModel() => new(MermaidLimits.Default, Here);

    /// <summary>
    ///     Parses a flowchart text into its model.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The model.</returns>
    private static FlowchartModel Parse(string text) =>
        Assert.IsType<FlowchartModel>(MermaidDocument.Parse(text).Diagram);

    /// <summary>
    ///     Resolves the configuration of a text with the default site configuration.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The configuration.</returns>
    private static MermaidConfig Config(string text) => MermaidConfigResolverVectorTests.ResolveLikeMermaid([], text);
}
