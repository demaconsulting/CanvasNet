using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Flowchart;

/// <summary>
///     Subsystem tests of the Flowchart subsystem: the parser and the model working together behind the document, from
///     a whole text to the document metadata, the layout data and the reports.
/// </summary>
public class FlowchartTests
{
    /// <summary>
    ///     Gets the names of the original flowchart fixtures.
    /// </summary>
    public static TheoryData<string> Fixtures
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory.GetFiles(FixtureFolder, "*.mmd").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(path));
            }

            return data;
        }
    }

    /// <summary>
    ///     Gets the folder of the flowchart fixtures.
    /// </summary>
    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart");

    /// <summary>
    ///     Validates the document metadata of a flowchart: type, direction, title, accessible title and counts.
    /// </summary>
    [Fact]
    public void Flowchart_Open_Document_GivesMetadata()
    {
        // Arrange
        const string Text = "---\ntitle: Pipeline\n---\nflowchart LR\n  accTitle: Steps\n  subgraph s [Stage]\n    a --> b\n  end\n  b --> c & d\n";

        // Act
        var document = MermaidDocument.Parse(Text);

        // Assert
        Assert.Equal(("flowchart", true, "LR"), (document.DiagramType, document.IsSupported, document.Direction));
        Assert.Equal(("Pipeline", "Steps"), (document.Title, document.AccTitle));
        Assert.Equal((4, 3, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
    }

    /// <summary>
    ///     Validates the layout data of a whole text: clusters first, then vertices with their parents, classes and
    ///     styles, and edges with markers and curves from the configuration.
    /// </summary>
    [Fact]
    public void Flowchart_GetData_WholeText_MapsLikeMermaid()
    {
        // Arrange
        const string Text = "%%{init: {\"flowchart\": {\"curve\": \"linear\", \"padding\": 9}}}%%\nflowchart TB\n" +
                            "  subgraph g [Group]\n    a:::hot --> b\n  end\n  b -.-x c\n  style c stroke:#f00\n  classDef hot fill:#f66\n";
        var document = MermaidDocument.Parse(Text);
        var model = Assert.IsType<FlowchartModel>(document.Diagram);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid([], Text);

        // Act
        var data = model.GetData(config, document.Detected.MermaidId);

        // Assert
        Assert.Equal(["g", "a", "b", "c"], data.Nodes.Select(n => n.Id));
        Assert.Equal([null, "g", "g", null], data.Nodes.Select(n => n.ParentId));
        Assert.Equal(["fill:#f66"], data.Nodes[1].CssCompiledStyles);
        Assert.Equal("default hot", data.Nodes[1].CssClasses);
        Assert.Equal(["stroke:#f00"], data.Nodes[3].CssStyles!);
        Assert.Equal(9, data.Nodes[1].Padding);
        Assert.Equal([("none", "arrow_point"), ("none", "arrow_cross")], data.Edges.Select(e => (e.ArrowTypeStart, e.ArrowTypeEnd)));
        Assert.Equal(["linear", "linear"], data.Edges.Select(e => e.Curve));
        Assert.Equal("dotted", data.Edges[1].Pattern);
    }

    /// <summary>
    ///     Validates that a look, a layout and icons are reported at their locations, and that the render then fails
    ///     because drawing is not wired yet.
    /// </summary>
    [Fact]
    public void Flowchart_Render_UnsupportedFeatures_AreReported()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nflowchart LR\n  a@{ icon: \"fa:car\" } --> b[fa:fa-user User]\n";
        var document = MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "f.mmd" });
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        Assert.Throws<MermaidRenderException>(() => document.Measure(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(
            [
                (MermaidDiagnosticCodes.LookUnsupported, 3),
                (MermaidDiagnosticCodes.LayoutUnsupported, 4),
                (MermaidDiagnosticCodes.IconUnsupported, 7),
                (MermaidDiagnosticCodes.IconUnsupported, 7),
            ],
            unsupported.Select(u => (u.Feature, u.Location.Line)));
        Assert.All(unsupported, u => Assert.Equal("f.mmd", u.Location.File));
    }

    /// <summary>
    ///     Validates that strict mode stops at the first unsupported feature.
    /// </summary>
    [Fact]
    public void Flowchart_Render_StrictMode_ThrowsAtFirstUnsupportedFeature()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  look: handDrawn\n---\nflowchart LR\n  a --> b\n");
        var (options, _, _) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var ex = Assert.Throws<MermaidUnsupportedFeatureException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LookUnsupported, ex.Feature);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that the <c>flowchart-elk</c> keyword opens as a flowchart whose layout falls back to dagre with a
    ///     report.
    /// </summary>
    [Fact]
    public void Flowchart_Render_ElkKeyword_ReportsLayout()
    {
        // Arrange
        var document = MermaidDocument.Parse("flowchart-elk TD\n  a --> b\n");
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        Assert.Throws<MermaidRenderException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(("flowchart", "flowchart-elk", "TB"), (document.DiagramType, document.Detected.MermaidId, document.Direction));
        Assert.Equal(MermaidDiagnosticCodes.LayoutUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Validates that a syntax error after a frontmatter is located on its line of the original text.
    /// </summary>
    [Fact]
    public void Flowchart_Open_SyntaxErrorAfterFrontmatter_IsLocatedInOriginalText()
    {
        // Arrange
        const string Text = "---\ntitle: T\n---\n%% comment\nflowchart LR\n  a --> b\n  b --> [c]\n";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "x.mmd" }));

        // Assert
        Assert.Equal(new MermaidLocation("x.mmd", 7, 9), ex.Location);
        Assert.Contains("NODE_STRING", ex.Expected);
        Assert.StartsWith("x.mmd:7:9: mermaid-syntax-error: expected ", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every original fixture opens as a flowchart with nodes and maps to layout data without reports.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Flowchart_Open_Fixture_ParsesWithoutReports(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));
        var document = MermaidDocument.Parse(text);
        var model = Assert.IsType<FlowchartModel>(document.Diagram);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid([], text);
        var sink = new MermaidReportSink(100, throwOnUnsupported: true);

        // Act
        var data = model.GetData(config, document.Detected.MermaidId);
        model.ReportUnsupported(config, sink);

        // Assert
        Assert.Equal("flowchart", document.DiagramType);
        Assert.True(document.NodeCount >= 2, name);
        Assert.Equal(document.NodeCount + document.SubgraphCount, data.Nodes.Count);
        Assert.Equal(document.EdgeCount, data.Edges.Count);
        Assert.Equal(0, sink.Count);
    }

    /// <summary>
    ///     Validates that the fixtures cover the plan's set of about forty original diagrams.
    /// </summary>
    [Fact]
    public void Flowchart_Fixtures_AreAboutForty()
    {
        // Act
        var count = Directory.GetFiles(FixtureFolder, "*.mmd").Length;

        // Assert
        Assert.InRange(count, 40, 60);
    }

}
