using DemaConsulting.CanvasNet.Mermaid.Mindmap;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Mindmap;

/// <summary>
///     Unit tests for <c>MindmapParser</c> (with <c>MindmapDiagram</c>): the jison lexer rules in their order and with their
///     quirks, every bracket and shape type, ids and descriptions, the quoted and markdown strings, the decorations, the
///     indentation levels and parents, the sections, classes and edges, the located errors with the expected tokens, the
///     metadata, and the limits on every kept and derived text.
/// </summary>
public sealed class MindmapParserTests
{
    /// <summary>
    ///     Validates that the indentation builds the tree: each node's parent is the last node with a smaller level, and the
    ///     levels are relative to the root's indentation.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Indentation_BuildsTreeRelativeToRoot()
    {
        // Act
        var diagram = Parse("mindmap\n    root\n      a\n          b\n        c\n      d\n");

        // Assert
        Assert.Equal(["root", "a", "b", "c", "d"], diagram.Nodes.Select(n => n.NodeId));
        Assert.Equal([0, 2, 6, 4, 2], diagram.Nodes.Select(n => n.Level));
        Assert.Equal([null, "root", "a", "a", "root"], diagram.Nodes.Select(n => n.Parent?.NodeId));
        Assert.Equal([0, 1, 2, 2, 1], diagram.Nodes.Select(n => n.Depth));
        Assert.True(diagram.Root!.IsRoot);
        Assert.Equal((5, 4), (diagram.NodeCount, diagram.EdgeCount));
    }

    /// <summary>
    ///     Validates that a tab counts as one character of indentation, as in Mermaid (the level is the length of the white
    ///     space).
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Tabs_CountOneEach()
    {
        // Act
        var diagram = Parse("mindmap\nroot\n\ta\n  b\n");

        // Assert
        Assert.Equal([0, 1, 2], diagram.Nodes.Select(n => n.Level));
        Assert.Equal("a", diagram.Nodes[2].Parent!.NodeId);
    }

    /// <summary>
    ///     Validates the shape type and shape of every bracket pair.
    /// </summary>
    /// <param name="node">The node text.</param>
    /// <param name="type">The expected type (Mermaid's <c>nodeType</c> number).</param>
    /// <param name="shape">The expected shape.</param>
    [Theory]
    [InlineData("id", 0, "defaultMindmapNode")]
    [InlineData("id[text]", 2, "rect")]
    [InlineData("id(text)", 1, "rounded")]
    [InlineData("id((text))", 3, "mindmapCircle")]
    [InlineData("id))text((", 5, "bang")]
    [InlineData("id)text(", 4, "cloud")]
    [InlineData("id{{text}}", 6, "hexagon")]
    [InlineData("id(text(", 4, "cloud")]
    [InlineData("id(text]", 4, "cloud")]
    [InlineData("id(-text-)", 0, "defaultMindmapNode")]
    public void MindmapParser_Parse_Brackets_GiveMermaidsType(string node, int type, string shape)
    {
        // Act
        var root = Parse("mindmap\n" + node + "\n").Root!;

        // Assert
        Assert.Equal((type, shape), ((int)root.Type, root.Shape));
        Assert.Equal("id", root.NodeId);
    }

    /// <summary>
    ///     Validates that a node without an id takes its description as its id, and that a node with both keeps both.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_IdsAndDescriptions_AreKept()
    {
        // Act
        var diagram = Parse("mindmap\n[The root]\n  child(The child)\n");

        // Assert
        Assert.Equal(("The root", "The root"), (diagram.Nodes[0].NodeId, diagram.Nodes[0].Description));
        Assert.Equal(("child", "The child"), (diagram.Nodes[1].NodeId, diagram.Nodes[1].Description));
    }

    /// <summary>
    ///     Validates the strings: a quoted description may hold brackets, a markdown string loses its quotes and backticks,
    ///     and a description runs over line feeds to its closing bracket.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Strings_KeepTheirText()
    {
        // Act
        var diagram = Parse("mindmap\nroot[\"a [b] (c)\"]\n  m[\"`**bold**`\"]\n  w[one\ntwo]\n");

        // Assert
        Assert.Equal(["a [b] (c)", "**bold**", "one\ntwo"], diagram.Nodes.Select(n => n.Description));
    }

    /// <summary>
    ///     Validates the lexer's quirk that an id runs to the first bracket or line feed, so a comment or a class after an
    ///     id is part of it.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_TextAfterId_IsPartOfTheId()
    {
        // Act
        var diagram = Parse("mindmap\nroot %% not a comment\n  a:::b\n");

        // Assert
        Assert.Equal(["root %% not a comment", "a:::b"], diagram.Nodes.Select(n => n.NodeId));
        Assert.Null(diagram.Nodes[1].Class);
    }

    /// <summary>
    ///     Validates the lexer's quirk that a blank line right after a statement is white space ending with a line feed
    ///     (<c>SPACELINE</c>), which a node at the start of the next line may not follow.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_BlankLineThenUnindentedNode_FailsAsMermaidDoes()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\n\nroot\n"));

        // Assert
        Assert.Equal(["SPACELINE", "NL", "EOF"], exception.Expected);
        Assert.Contains("found 'NODE_ID'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that the comment lines and indented blank lines between nodes are skipped.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_CommentsAndBlankLines_AreSkipped()
    {
        // Act
        var diagram = Parse("mindmap\nroot\n  a\n\n  %% a comment\n   \n  b\n");

        // Assert
        Assert.Equal(["root", "a", "b"], diagram.Nodes.Select(n => n.NodeId));
    }

    /// <summary>
    ///     Validates that an icon and a class decorate the last node added, whatever their indentation, and that the icon
    ///     keeps its location.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Decorations_ApplyToLastNode()
    {
        // Act
        var diagram = Parse("mindmap\nroot\n  a\n    b\n::icon(fa fa-book)\n  :::urgent large\n");

        // Assert
        var b = diagram.Nodes[2];
        Assert.Equal(("fa fa-book", "urgent large"), (b.Icon, b.Class));
        Assert.Equal((5, 8), (b.IconLocation.Line, b.IconLocation.Column));
        Assert.Equal("mindmap-node section-0 urgent large", b.CssClasses);
        Assert.Null(diagram.Nodes[1].Icon);
    }

    /// <summary>
    ///     Validates that a decoration before any node fails, located, as Mermaid does with a JavaScript error.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_DecorationBeforeNode_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\n  :::urgent\nroot\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal((2, 6), (exception.Location.Line, exception.Location.Column));
        Assert.Contains("Cannot set properties of undefined (setting 'class')", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a second node at the root's level fails with Mermaid's message, located at that node.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_SecondRoot_FailsWithMermaidsMessage()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\n  root\n    a\n  other[Other]\n"));

        // Assert
        Assert.Equal((4, 3), (exception.Location.Line, exception.Location.Column));
        Assert.Contains("There can be only one root. No parent could be found for (\"Other\")", exception.Message, StringComparison.Ordinal);
        Assert.Empty(exception.Expected);
    }

    /// <summary>
    ///     Validates the grammar errors with jison's expected tokens: a decoration on a node's line, an unterminated node
    ///     and the keyword alone.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="found">The token found.</param>
    /// <param name="expected">The expected tokens, comma-separated.</param>
    [Theory]
    [InlineData("mindmap\nroot[a]::icon(x)\n", "'ICON'", "SPACELINE,NL,EOF")]
    [InlineData("mindmap\nroot[a\n", "the end of the text", "NODE_DEND")]
    [InlineData("mindmap\nroot[]\n", "'NODE_DEND'", "NODE_DESCR")]
    [InlineData("mindmap", "'EOF'", "SPACELINE,SPACELIST,ICON,CLASS,NODE_DSTART,NODE_ID")]
    [InlineData("mindmap\nroot\n  MINDMAP\n", "'MINDMAP'", "SPACELINE,NL,EOF,ICON,CLASS,NODE_DSTART,NODE_ID")]
    [InlineData("mindmap2\n", "'NODE_ID'", "SPACELINE,MINDMAP")]
    public void MindmapParser_Parse_GrammarError_ListsJisonExpectedTokens(string text, string found, string expected)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Contains("found " + found, exception.Message, StringComparison.Ordinal);
        Assert.Equal(expected.Split(','), exception.Expected);
        Assert.NotNull(exception.Hint);
    }

    /// <summary>
    ///     Validates that a lone brace outside a description is not lexed, and the error is located at it.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_LoneBrace_FailsLexically()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\nroot\n  a{b\n"));

        // Assert
        Assert.Contains("unrecognized text '{'", exception.Message, StringComparison.Ordinal);
        Assert.Equal((3, 4), (exception.Location.Line, exception.Location.Column));
    }

    /// <summary>
    ///     Validates the empty mindmap: the keyword and blank lines give no nodes and no edges.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_KeywordOnly_IsEmpty()
    {
        // Act
        var diagram = Parse("mindmap\n");

        // Assert
        Assert.Null(diagram.Root);
        Assert.Equal((0, 0), (diagram.NodeCount, diagram.EdgeCount));
    }

    /// <summary>
    ///     Validates the sections: the root has none, its children cycle through 11, deeper nodes inherit, and the classes
    ///     and edges follow.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Sections_CycleThroughElevenAndInherit()
    {
        // Arrange
        var children = string.Concat(Enumerable.Range(0, 13).Select(i => $"  c{i}\n"));

        // Act
        var diagram = Parse("mindmap\nroot\n" + children + "    deep\n");

        // Assert
        Assert.Null(diagram.Root!.Section);
        Assert.Equal("mindmap-node section-root section--1", diagram.Root.CssClasses);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0, 1, 1], diagram.Nodes.Skip(1).Select(n => n.Section!.Value));
        Assert.Equal("mindmap-node section-1", diagram.Nodes[^1].CssClasses);
        Assert.Equal("edge_13_14", diagram.Edges[^1].Id);
        Assert.Equal("edge section-edge-1 edge-depth-3", diagram.Edges[^1].Classes);
    }

    /// <summary>
    ///     Validates that the depth class of an edge is the parent's level (its indentation difference) plus one, not the
    ///     depth in the tree, as Mermaid's <c>generateEdges</c> writes it.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_EdgeDepth_UsesIndentationLevel()
    {
        // Act
        var diagram = Parse("mindmap\nroot\n    a\n        b\n");

        // Assert
        Assert.Equal(["edge section-edge-0 edge-depth-1", "edge section-edge-0 edge-depth-5"], diagram.Edges.Select(e => e.Classes));
        Assert.Equal(["edge_0_1", "edge_1_2"], diagram.Edges.Select(e => e.Id));
    }

    /// <summary>
    ///     Validates that the ids, descriptions, icons and classes are sanitized as Mermaid's <c>sanitizeText</c> does.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_Texts_AreSanitized()
    {
        // Act
        var diagram = Parse("mindmap\nroot[<script>x</script>safe]\n::icon(<b>i</b>)\n");

        // Assert
        Assert.Equal("safe", diagram.Root!.Description);
        Assert.Equal("<b>i</b>", diagram.Root.Icon);
    }

    /// <summary>
    ///     Validates that every node counts against <see cref="MermaidLimits.MaxNodes"/>.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_TooManyNodes_FailsWithLimitExceeded()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\nroot\n  a\n  b\n  c\n", new MermaidLimits { MaxNodes = 3 }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(5, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that a node's depth is bounded by <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_TooDeep_FailsWithLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 2 };

        // Act
        var accepted = Parse("mindmap\na\n b\n  c\n", limits);
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\na\n b\n  c\n   d\n", limits));

        // Assert
        Assert.Equal(2, accepted.Nodes[^1].Depth);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MermaidLimits.MaxNestingDepth", exception.Message, StringComparison.Ordinal);
        Assert.Equal(5, exception.Location.Line);
    }

    /// <summary>
    ///     Validates that every kept text is checked against <see cref="MermaidLimits.MaxLabelLength"/>: the id, each form of
    ///     description, the icon and the class.
    /// </summary>
    /// <param name="line">The line after the root, or the root itself.</param>
    [Theory]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("id[xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx]")]
    [InlineData("id[\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"]")]
    [InlineData("id[\"`xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`\"]")]
    [InlineData("root\n::icon(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)")]
    [InlineData("root\n:::xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void MindmapParser_Parse_LongText_FailsWithLimitExceeded(string line)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\n" + line + "\n", new MermaidLimits { MaxLabelLength = 44 }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the class list a node derives (its section classes and its own classes) is checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/> even when each text is short enough.
    /// </summary>
    [Fact]
    public void MindmapParser_Parse_LongClassList_FailsWithLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 44 };

        // Act
        var accepted = Parse("mindmap\nroot\n:::abcdefg\n", limits);
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("mindmap\nroot\n:::abcdefgh\n", limits));

        // Assert
        Assert.Equal(44, accepted.Root!.CssClasses.Length);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(2, exception.Location.Line);
    }

    /// <summary>
    ///     Parses a mindmap text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The diagram.</returns>
    private static MindmapDiagram Parse(string text, MermaidLimits? limits = null)
    {
        limits ??= MermaidLimits.Default;
        var source = Preprocessor.Run(text, null, limits);
        return MindmapParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), limits);
    }
}
