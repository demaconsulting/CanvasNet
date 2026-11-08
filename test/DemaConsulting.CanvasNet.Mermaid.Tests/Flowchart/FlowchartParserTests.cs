using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Flowchart;

/// <summary>
///     Unit tests of <see cref="FlowchartParser"/>: the grammar, the lexer quirks, located errors with jison's expected
///     tokens, and the limits.
/// </summary>
public class FlowchartParserTests
{
    /// <summary>
    ///     Validates a statement with a chain of links: vertices in order, edges between consecutive groups.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_LinkChain_AddsVerticesAndEdges()
    {
        // Act
        var model = Parse("flowchart LR\n  A[Start] --> B{Choice} -- yes --> C((Done))\n");

        // Assert
        Assert.Equal("LR", model.Direction);
        Assert.Equal(["A", "B", "C"], model.Vertices.Select(v => v.Id));
        Assert.Equal(["square", "diamond", "circle"], model.Vertices.Select(v => v.Type));
        Assert.Equal([("A", "B", ""), ("B", "C", "yes")], model.Edges.Select(e => (e.Start, e.End, e.Text)));
    }

    /// <summary>
    ///     Validates that a token the grammar does not accept is a located syntax error listing jison's expected tokens.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_WrongCloser_ThrowsLocatedErrorWithExpectedTokens()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph TD\n  A[[a] --> B\n", "f.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("f.mmd", 2, 7), ex.Location);
        Assert.Equal(["SUBROUTINEEND", "TAGEND", "UNICODE_TEXT", "TEXT", "TAGSTART"], ex.Expected);
        Assert.Contains("found 'SQE' (']')", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that a label that never closes lists the closers of every shape, as jison does.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_UnclosedLabel_ListsEveryCloser()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph TD\n  A[text --> B\n"));

        // Assert
        Assert.Equal(
            ["SQE", "DOUBLECIRCLEEND", "PE", "-)", "STADIUMEND", "SUBROUTINEEND", "PIPE", "CYLINDEREND", "DIAMOND_STOP", "TAGEND", "TRAPEND", "INVTRAPEND", "UNICODE_TEXT", "TEXT", "TAGSTART"],
            ex.Expected);
        Assert.Contains("found the end of the text", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that text no lexer rule matches is a located lexical error.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_UnknownDirection_ThrowsLexicalError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph XY\n  A --> B\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation(null, 1, 6), ex.Location);
        Assert.Contains("unrecognized text", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a statement that does not end lists the separators and the continuations.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_TwoNodesOnALine_ListsSeparators()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph LR\n  c - d\n"));

        // Assert
        Assert.Equal(["SEMI", "NEWLINE", "EOF", "AMP", "START_LINK", "LINK", "LINK_ID"], ex.Expected);
        Assert.Equal(new MermaidLocation(null, 2, 5), ex.Location);
    }

    /// <summary>
    ///     Validates that a subgraph that is never closed asks for <c>end</c>.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_UnclosedSubgraph_AsksForEnd()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("flowchart LR\n  subgraph s\n  a --> b\n"));

        // Assert
        Assert.Contains("end", ex.Expected);
        Assert.Contains("'end'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a subgraph without id or title is refused at its keyword (Mermaid fails on it too).
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_AnonymousSubgraph_ThrowsAtKeyword()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("flowchart LR\n  subgraph\n  a\n  end\n"));

        // Assert
        Assert.Equal(new MermaidLocation(null, 2, 3), ex.Location);
        Assert.Contains("id or a title", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the subgraph nesting limit.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_NestingDeeperThanLimit_ThrowsLimitExceeded()
    {
        // Arrange
        const string Text = "flowchart LR\n subgraph a\n  subgraph b\n   subgraph c\n    x\n   end\n  end\n end\n";
        var limits = new MermaidLimits { MaxNestingDepth = 2 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse(Text, limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 4, 4), ex.Location);
        Assert.Contains("MaxNestingDepth", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, Parse(Text, limits: new MermaidLimits { MaxNestingDepth = 3 }).Subgraphs.Count);
    }

    /// <summary>
    ///     Validates the edge limit (Mermaid's <c>maxEdges</c>).
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_MoreEdgesThanLimit_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxEdges = 2 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph LR\n a --> b\n b --> c\n c --> d\n", limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(4, ex.Location.Line);
        Assert.Contains("MaxEdges", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the node limit, which counts subgraphs too.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_MoreNodesThanLimit_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("graph LR\n subgraph s\n  a --> b\n end\n c\n", limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(5, ex.Location.Line);
        Assert.Contains("MaxNodes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the label length limit on every kind of label and on the accessibility texts.
    /// </summary>
    /// <param name="text">A text with a label of 11 characters.</param>
    [Theory]
    [InlineData("graph LR\n a[abcdefghijk]\n")]
    [InlineData("graph LR\n a -- abcdefghijk --> b\n")]
    [InlineData("graph LR\n a -->|abcdefghijk| b\n")]
    [InlineData("graph LR\n subgraph s [abcdefghijk]\n a\n end\n")]
    [InlineData("graph LR\n a@{ label: abcdefghijk }\n")]
    [InlineData("graph LR\n accTitle: abcdefghijk\n")]
    [InlineData("graph LR\n accDescr { abcdefghijk }\n")]
    public void FlowchartParser_Parse_LabelLongerThanLimit_ThrowsLimitExceeded(string text)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse(text, limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.Contains("MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unknown shape name is refused at its value with the nearest known name as the hint.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_UnknownShape_HintsNearestName()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("flowchart LR\n  A@{ shape: rectangles }\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.ShapeUnknown, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 14), ex.Location);
        Assert.Contains("'rectangles'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'rectangle'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a shape name in the wrong case is refused with the lower-case rule in the message.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_UpperCaseShape_ExplainsLowerCaseRule()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse("flowchart LR\n  A@{ shape: Circle }\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.ShapeUnknown, ex.Code);
        Assert.Contains("lower case", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'circle'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every form of <c>click</c> is parsed and only adds the <c>clickable</c> class.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_ClickStatements_OnlyAddClickableClass()
    {
        // Arrange
        const string Text = "flowchart LR\n  A --> B --> C --> D\n  click A callback \"tip\"\n  click B call go(1, \"x\")\n" +
                            "  click C href \"https://example.com\" \"tip\" _blank\n  click D \"https://example.com\" _self\n";

        // Act
        var model = Parse(Text);

        // Assert
        Assert.All(model.Vertices, v => Assert.Equal(["clickable"], v.Classes));
        Assert.Equal(3, model.Edges.Count);
    }

    /// <summary>
    ///     Validates the accessibility statements, which are kept as metadata only.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_AccessibilityStatements_SetTitleAndDescription()
    {
        // Act
        var model = Parse("flowchart LR\n  accTitle: Orders\n  accDescr {\n    How orders\n      flow\n  }\n  A --> B\n");

        // Assert
        Assert.Equal("Orders", model.AccTitle);
        Assert.Equal("How orders\nflow", model.AccessibleDescription);
    }

    /// <summary>
    ///     Validates that words that start with a keyword are ids, while a keyword alone is not.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_KeywordPrefixes_AreIds()
    {
        // Act
        var model = Parse("graph TD\n  styled --> subgraphs\n  endpoint --> classy\n  default --> v\n");

        // Assert
        Assert.Equal(["styled", "subgraphs", "endpoint", "classy", "default", "v"], model.Vertices.Select(v => v.Id));
    }

    /// <summary>
    ///     Validates that a line with <c>direction XX</c> anywhere on it is a direction statement, as Mermaid's lexer
    ///     reads it.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_DirectionAnywhereOnLine_IsDirectionStatement()
    {
        // Act
        var model = Parse("flowchart TB\n  subgraph s\n    A --> B direction LR\n    C\n  end\n");

        // Assert
        Assert.Equal("LR", model.Subgraphs[0].Direction);
        Assert.Equal(["C"], model.Vertices.Select(v => v.Id));
    }

    /// <summary>
    ///     Validates that the white space after a closing brace is removed up to the line feed, keeping locations.
    /// </summary>
    [Fact]
    public void FlowchartParser_TrimSpaceAfterBraces_TrailingSpaces_AreRemovedWithLocations()
    {
        // Arrange
        var text = MermaidTestHelpers.Mapped("A@{ x }   \n  \nB} C\n");

        // Act
        var trimmed = FlowchartParser.TrimSpaceAfterBraces(text);

        // Assert
        Assert.Equal("A@{ x }\nB} C\n", trimmed.Text);
        Assert.Equal(new MermaidLocation(null, 3, 1), trimmed.Locate(8));
    }

    /// <summary>
    ///     Validates that a long line of chained nodes, ids with <c>@</c> and repeated direction words parses: the lexer
    ///     caches keep such lines linear.
    /// </summary>
    [Fact]
    public void FlowchartParser_Parse_LongLines_Parse()
    {
        // Arrange
        var chain = string.Join(" --> ", Enumerable.Range(0, 400).Select(i => $"n{i}"));
        var ids = string.Join(" ", Enumerable.Range(0, 400).Select(i => $"a{i}@b"));
        var words = string.Join(" & ", Enumerable.Repeat("direction", 400));
        var text = $"flowchart LR\n  {chain}\n  {words}\n";

        // Act
        var model = Parse(text);
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse($"flowchart LR\n  {ids}\n"));

        // Assert
        Assert.Equal(399, model.Edges.Count);
        Assert.Equal(401, model.Vertices.Count);
        Assert.Contains("'LINK_ID'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Parses a flowchart text into its model.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    private static FlowchartModel Parse(string text, string? sourceName = null, MermaidLimits? limits = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, sourceName, effective);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        return FlowchartParser.Parse(source, detected, effective);
    }
}
