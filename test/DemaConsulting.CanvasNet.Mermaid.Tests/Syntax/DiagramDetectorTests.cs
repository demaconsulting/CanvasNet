using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="DiagramDetector"/>: Mermaid's detectors, their order, their quirks, the settings that
///     choose between variants, and the errors.
/// </summary>
public class DiagramDetectorTests
{
    /// <summary>
    ///     Detects a text with the default settings.
    /// </summary>
    /// <param name="text">The diagram code.</param>
    /// <returns>The detected type.</returns>
    private static DetectedDiagram Detect(string text) => DiagramDetector.Detect(MermaidTestHelpers.Mapped(text), DetectorSettings.Default);

    /// <summary>
    ///     Validates the detector id, type and support of every known keyword.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="id">The expected detector id.</param>
    /// <param name="type">The expected type.</param>
    /// <param name="support">The expected support, by name.</param>
    [Theory]
    [InlineData("flowchart TD", "flowchart-v2", "flowchart", "Supported")]
    [InlineData("graph LR", "flowchart-v2", "flowchart", "Supported")]
    [InlineData("flowchart-elk TD", "flowchart-elk", "flowchart", "Supported")]
    [InlineData("sequenceDiagram", "sequence", "sequence", "Supported")]
    [InlineData("classDiagram", "classDiagram", "class", "Supported")]
    [InlineData("classDiagram-v2", "classDiagram", "class", "Supported")]
    [InlineData("stateDiagram", "stateDiagram", "state", "Supported")]
    [InlineData("stateDiagram-v2", "stateDiagram", "state", "Supported")]
    [InlineData("erDiagram", "er", "er", "Supported")]
    [InlineData("gantt", "gantt", "gantt", "Planned")]
    [InlineData("pie showData", "pie", "pie", "Planned")]
    [InlineData("mindmap", "mindmap", "mindmap", "Planned")]
    [InlineData("architecture-beta", "architecture", "architecture", "NotPlanned")]
    [InlineData("C4Context", "c4", "c4", "NotPlanned")]
    [InlineData("kanban", "kanban", "kanban", "NotPlanned")]
    [InlineData("info", "info", "info", "NotPlanned")]
    [InlineData("requirementDiagram", "requirement", "requirement", "NotPlanned")]
    [InlineData("swimlane-beta", "swimlane", "swimlane", "NotPlanned")]
    [InlineData("timeline", "timeline", "timeline", "NotPlanned")]
    [InlineData("gitGraph", "gitGraph", "gitGraph", "NotPlanned")]
    [InlineData("journey", "journey", "journey", "NotPlanned")]
    [InlineData("quadrantChart", "quadrantChart", "quadrantChart", "NotPlanned")]
    [InlineData("sankey-beta", "sankey", "sankey", "NotPlanned")]
    [InlineData("packet-beta", "packet", "packet", "NotPlanned")]
    [InlineData("xychart-beta", "xychart", "xychart", "NotPlanned")]
    [InlineData("block-beta", "block", "block", "NotPlanned")]
    [InlineData("eventmodeling", "eventmodeling", "eventmodeling", "NotPlanned")]
    [InlineData("treeView-beta", "treeView", "treeView", "NotPlanned")]
    [InlineData("radar-beta", "radar", "radar", "NotPlanned")]
    [InlineData("Ishikawa-Beta", "ishikawa", "ishikawa", "NotPlanned")]
    [InlineData("treemap", "treemap", "treemap", "NotPlanned")]
    [InlineData("RAILROAD-BETA", "railroad", "railroad", "NotPlanned")]
    [InlineData("railroad-ebnf-beta", "railroadEbnf", "railroadEbnf", "NotPlanned")]
    [InlineData("railroad-abnf-beta", "railroadAbnf", "railroadAbnf", "NotPlanned")]
    [InlineData("railroad-peg-beta", "railroadPeg", "railroadPeg", "NotPlanned")]
    [InlineData("venn-beta", "venn", "venn", "NotPlanned")]
    [InlineData("wardley-beta", "wardley", "wardley", "NotPlanned")]
    [InlineData("cynefin-beta:", "cynefin", "cynefin", "NotPlanned")]
    [InlineData("Error", "error", "error", "NotPlanned")]
    public void DiagramDetector_Detect_KnownKeyword_GivesTypeAndSupport(string text, string id, string type, string support)
    {
        // Arrange / Act
        var detected = Detect(text);

        // Assert
        Assert.Equal((id, type, support), (detected.MermaidId, detected.DiagramType, detected.Support.ToString()));
    }

    /// <summary>
    ///     Validates the detection order: Mermaid's registration order, with the large features first.
    /// </summary>
    [Fact]
    public void DiagramDetector_DetectorIds_FollowMermaidRegistrationOrder()
    {
        // Arrange / Act
        var ids = DiagramDetector.DetectorIds;

        // Assert
        Assert.Equal(["error", "---", "flowchart-elk", "mindmap", "architecture", "c4", "kanban", "classDiagram", "class"], ids.Take(9));
        Assert.True(ids.ToList().IndexOf("sequence") < ids.ToList().IndexOf("flowchart-v2"));
        Assert.Equal("cynefin", ids[^1]);
        Assert.Equal(40, ids.Count);
    }

    /// <summary>
    ///     Validates the quirks of Mermaid's expressions: the <c>c4</c> detector matches its later keywords anywhere, a
    ///     keyword prefix is enough, and the word-boundary detectors need a boundary.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_MermaidQuirks_AreKept()
    {
        // Arrange / Act / Assert
        Assert.Equal("c4", Detect("graph TD\n A[C4Container]").MermaidId);
        Assert.Equal("flowchart", Detect("graphs are fun").DiagramType);
        Assert.Equal("pie", Detect("pieces").DiagramType);
        Assert.Equal("ishikawa", Detect("ishikawa-beta_x").DiagramType);
        Assert.Throws<MermaidSyntaxException>(() => Detect("ishikawa_x"));
        Assert.Throws<MermaidSyntaxException>(() => Detect("swimlane-beta_x"));
        Assert.Throws<MermaidSyntaxException>(() => Detect("cynefin-beta_x"));
    }

    /// <summary>
    ///     Validates the renderer settings that choose between the variants of the flowchart, class and state detectors.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_RendererSettings_ChooseVariant()
    {
        // Arrange
        var elk = new DetectorSettings("elk", "dagre-wrapper", "dagre-wrapper");
        var legacy = new DetectorSettings("dagre-d3", "dagre-d3", "dagre-d3");

        // Act / Assert
        Assert.Equal("flowchart-elk", DiagramDetector.Detect(MermaidTestHelpers.Mapped("graph TD"), elk).MermaidId);
        Assert.Equal("flowchart", DiagramDetector.Detect(MermaidTestHelpers.Mapped("graph TD"), legacy).MermaidId);
        Assert.Equal("class", DiagramDetector.Detect(MermaidTestHelpers.Mapped("classDiagram"), legacy).MermaidId);
        Assert.Equal(("state", DiagramSupport.Supported), (DiagramDetector.Detect(MermaidTestHelpers.Mapped("stateDiagram"), legacy).MermaidId, DiagramDetector.Detect(MermaidTestHelpers.Mapped("stateDiagram"), legacy).Support));
        Assert.Equal("stateDiagram", DiagramDetector.Detect(MermaidTestHelpers.Mapped("stateDiagram-v2"), legacy).MermaidId);
        Assert.Throws<MermaidSyntaxException>(() => DiagramDetector.Detect(MermaidTestHelpers.Mapped("flowchart TD"), legacy));
    }

    /// <summary>
    ///     Validates the clean-up before detection: a second frontmatter, directives and comments are skipped, and the
    ///     location is that of the keyword.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_CleanupBeforeDetection_SkipsCommentsAndDirectives()
    {
        // Arrange
        var code = MermaidTestHelpers.Mapped("%%{init: {}}%%\n  %% note\n  sequenceDiagram\n", "d.mmd");

        // Act
        var detected = DiagramDetector.Detect(code, DetectorSettings.Default);

        // Assert
        Assert.Equal(("sequence", "sequenceDiagram"), (detected.DiagramType, detected.Keyword));
        Assert.Equal(new MermaidLocation("d.mmd", 3, 3), detected.Location);
    }

    /// <summary>
    ///     Validates the unknown-diagram error for a word far from every keyword and for an empty text.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_Unknown_ThrowsWithNearestKeyword()
    {
        // Arrange / Act
        var far = Assert.Throws<MermaidSyntaxException>(() => Detect("hello world"));
        var empty = Assert.Throws<MermaidSyntaxException>(() => Detect("   \n"));

        // Assert: a word far from every keyword gets no suggestion; an empty text says so
        Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, far.Code);
        Assert.Contains("'hello'", far.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("did you mean", far.Hint, StringComparison.Ordinal);
        Assert.Contains("no diagram type keyword", empty.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the nearest-keyword hint for a typo.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_Typo_HintsNearestKeyword()
    {
        // Arrange / Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Detect("SequenceDiagram\n A->>B: x"));

        // Assert
        Assert.Equal("did you mean 'sequenceDiagram'? start the diagram with a type keyword, for example 'flowchart TD' or 'graph LR'", ex.Hint);
        Assert.Equal(new MermaidLocation(null, 1, 1), ex.Location);
    }

    /// <summary>
    ///     Validates that text that starts with <c>---</c> (an unclosed frontmatter) is Mermaid's own error.
    /// </summary>
    [Fact]
    public void DiagramDetector_Detect_DashDashDash_ThrowsMermaidMessage()
    {
        // Arrange / Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Detect("---\ntitle: x\nflowchart LR"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Contains("Diagrams beginning with --- are not valid", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the hints of the unsupported-type report.
    /// </summary>
    [Fact]
    public void DiagramDetector_UnsupportedHint_NamesPlanAndSupportedKeywords()
    {
        // Arrange / Act / Assert
        Assert.Equal("pie diagrams are planned; supported in this version: flowchart, graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram", DiagramDetector.UnsupportedHint(Detect("pie")));
        Assert.Equal("gitGraph diagrams are not supported; supported in this version: flowchart, graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram", DiagramDetector.UnsupportedHint(Detect("gitGraph")));
        Assert.Equal(["flowchart", "graph", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram"], DiagramDetector.SupportedDiagramKeywords);
    }

    /// <summary>
    ///     Validates the comment replacement of <c>detectType</c>: the white space before a comment, the comment and its
    ///     line feed become one line feed; a comment without a final line feed stays.
    /// </summary>
    [Fact]
    public void DiagramDetector_ReplaceComments_FollowsAnyCommentExpression()
    {
        // Arrange / Act
        var replaced = DiagramDetector.ReplaceComments(MermaidTestHelpers.Mapped("  %% c\n %%x\ngraph %% y\nA %% z"));

        // Assert
        Assert.Equal("\n\ngraph\nA %% z", replaced.Text);
    }
}
