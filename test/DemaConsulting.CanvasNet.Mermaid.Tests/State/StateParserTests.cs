using DemaConsulting.CanvasNet.Mermaid.State;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.State;

/// <summary>
///     Unit tests of <see cref="StateParser"/>: the statements of Mermaid's state grammar, the quirks of its lexer, located
///     errors with jison's expected tokens, and the limits.
/// </summary>
public class StateParserTests
{
    /// <summary>
    ///     Validates that both headers read the same statements.
    /// </summary>
    /// <param name="header">The header.</param>
    [Theory]
    [InlineData("stateDiagram-v2")]
    [InlineData("stateDiagram")]
    public void StateParser_Parse_Header_ReadsTheSameStatements(string header)
    {
        // Act
        var model = StateFacts.Parse($"{header}\n[*] --> A\nA --> B : go\n");

        // Assert
        Assert.Equal("stateDiagram", model.MermaidId);
        Assert.Equal(["root_start", "A", "B"], model.States.Select(s => s.Id));
        Assert.Equal(["root_start->A:", "A->B:go"], model.Relations.Select(r => $"{r.Id1}->{r.Id2}:{r.RelationTitle}"));
    }

    /// <summary>
    ///     Validates that the keywords are case-insensitive, as in Mermaid, and that the position of a note keeps the case
    ///     it is written in.
    /// </summary>
    [Fact]
    public void StateParser_Parse_KeywordsInAnyCase_AreRecognized()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nSTATE Comp {\n  A\n}\nNote Right Of Comp : remark\nClassDef hot fill:#f00\nCLASS Comp hot\n");

        // Assert
        var comp = model.States.Single(s => s.Id == "Comp");
        Assert.Equal(("Right Of", "remark"), (comp.Note!.Position, comp.Note.Text));
        Assert.Equal(["hot"], comp.Classes);
        Assert.Equal("hot", Assert.Single(model.StyleClasses).Id);
    }

    /// <summary>
    ///     Validates the statements Mermaid's grammar keeps as text: they are in the root document without a kind, and the
    ///     database ignores them.
    /// </summary>
    [Fact]
    public void StateParser_Parse_IgnoredStatements_AreKeptAsText()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nhide empty description\nscale 200 width\nstate Lone\nnote \"floating\" as N1\nA\n");

        // Assert
        Assert.Equal(["hide empty description", "scale ", "Lone", "note "], model.RootDocument.Take(4).Select(s => s.Text));
        Assert.All(model.RootDocument.Take(4), s => Assert.Null(s.Stmt));
        Assert.Equal("A", Assert.Single(model.States).Id);
    }

    /// <summary>
    ///     Validates the forms of a state: a description after a colon, <c>state "description" as id</c> (with a colon in
    ///     the id splitting off a second description), the pseudo-states in both notations and a <c>:::</c> class.
    /// </summary>
    [Fact]
    public void StateParser_Parse_StateForms_GiveMermaidsStatements()
    {
        // Act
        var model = StateFacts.Parse(
            "stateDiagram-v2\nA : first: second\nstate \"Quoted\" as B:more\nstate f <<fork>>\nstate j [[join]]\nstate c <<CHOICE>>\nC:::warm\n");

        // Assert
        var statements = model.RootDocument;
        Assert.Equal("first: second", statements[0].Description);
        Assert.Equal("B", statements[1].Id);
        Assert.Equal(["Quoted", "more"], statements[1].DescriptionParts!);
        Assert.Equal(["fork", "join", "choice"], statements.Skip(2).Take(3).Select(s => s.Type));
        Assert.Equal(["f", "j", "c"], statements.Skip(2).Take(3).Select(s => s.Id));
        Assert.Equal(["warm"], statements[5].Classes!);
    }

    /// <summary>
    ///     Validates that an inline <c>%%</c> comment ends an id, and that a line with <c>direction</c> in it is a direction
    ///     statement, wherever the word stands.
    /// </summary>
    [Fact]
    public void StateParser_Parse_CommentsAndDirectionLines_FollowTheLexer()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nA --> B%%note\nC --> D %% direction LR is swallowed\nE\n");

        // Assert
        Assert.Equal(["A", "B", "E"], model.States.Select(s => s.Id));
        Assert.Equal("LR", model.Direction);
    }

    /// <summary>
    ///     Validates that the concurrency dividers are numbered in parse order (inner dividers first) and that a
    ///     composite state's own direction is its last direction statement.
    /// </summary>
    [Fact]
    public void StateParser_Parse_DividersAndDirections_AreReadPerComposite()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nstate Outer {\n  direction LR\n  state Inner {\n    a\n    --\n    b\n  }\n  --\n  c\n  direction BT\n}\n");

        // Assert
        var outer = model.RootDocument[0];
        Assert.Equal(["divider-id-2", "id-random-1"], outer.Doc!.Select(s => s.Id));
        var inner = outer.Doc![0].Doc!.Single(s => s.Id == "Inner");
        Assert.Equal(["divider-id-1", "id-random-2"], inner.Doc!.Select(s => s.Id));
        Assert.Equal("TB", model.Nodes.Single(n => n.Id == "Outer").Dir);
        Assert.Equal("BT", model.Nodes.Single(n => n.Id == "id-random-1").Dir);
    }

    /// <summary>
    ///     Validates that a missing transition target fails where Mermaid fails, with jison's expected tokens.
    /// </summary>
    [Fact]
    public void StateParser_Parse_MissingTarget_ThrowsLocatedErrorWithExpectedTokens()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nA -->\n", "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(["ID", "EDGE_STATE"], ex.Expected);
        Assert.Equal(("s.mmd", 2), (ex.Location.File, ex.Location.Line));
        Assert.Contains("found 'NL'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a divider outside a composite state, and a dash in an id, are the invalid token of Mermaid's
    ///     lexer.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="line">The line of the error.</param>
    [Theory]
    [InlineData("stateDiagram-v2\nA --> B\n--\nC\n", 3)]
    [InlineData("stateDiagram-v2\nfirst-state --> second\n", 2)]
    public void StateParser_Parse_DashOutsideComposite_IsAnInvalidToken(string text, int line)
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text));

        // Assert
        Assert.Contains("found 'INVALID' ('-')", ex.Message, StringComparison.Ordinal);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("'--'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a click inside a composite state is read as states, because the lexer knows neither the
    ///     <c>click</c> keyword nor strings there.
    /// </summary>
    [Fact]
    public void StateParser_Parse_ClickInsideComposite_IsReadAsStates()
    {
        // Act
        var model = StateFacts.Parse("stateDiagram-v2\nstate C {\n  A\n  click A \"u\" \"t\"\n}\n");

        // Assert
        Assert.Empty(model.Links);
        Assert.Equal(["C", "A", "click", "\"u\"", "\"t\""], model.Nodes.Select(n => n.Id));
        Assert.All(model.Nodes.Skip(1), n => Assert.Equal("C", n.ParentId));
    }

    /// <summary>
    ///     Validates the errors Mermaid throws without a token: a composite name of two words (from the lexer), located
    ///     at the name.
    /// </summary>
    [Fact]
    public void StateParser_Parse_TwoWordCompositeName_ThrowsMermaidsMessage()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nA\nstate Two Words {\n  B\n}\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Contains("Error: State name must be a single word. Found: \"Two Words {\"", ex.Message, StringComparison.Ordinal);
        Assert.Equal((3, 7), (ex.Location.Line, ex.Location.Column));
        Assert.Empty(ex.Expected);
    }

    /// <summary>
    ///     Validates that a note without <c>end note</c> is a lexical error with a hint for state diagrams.
    /// </summary>
    [Fact]
    public void StateParser_Parse_UnclosedNote_IsALexicalError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nA\nnote left of A\n  never closed\n"));

        // Assert
        Assert.Contains("unrecognized text", ex.Message, StringComparison.Ordinal);
        Assert.Contains("end note", ex.Hint, StringComparison.Ordinal);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that an unclosed composite state fails at the end of the text, with a hint to close it.
    /// </summary>
    [Fact]
    public void StateParser_Parse_UnclosedComposite_FailsAtTheEnd()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse("stateDiagram-v2\nstate A {\n  B\n"));

        // Assert
        Assert.Contains("found the end of the text", ex.Message, StringComparison.Ordinal);
        Assert.Contains("STRUCT_STOP", ex.Expected);
        Assert.Contains("'}'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that composite states nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> are refused at
    ///     the brace of the first one too deep, and that the deepest allowed nesting is accepted.
    /// </summary>
    [Fact]
    public void StateParser_Parse_CompositesTooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 2 };

        // Act
        var accepted = StateFacts.Parse("stateDiagram-v2\nstate A {\n  state B {\n    X\n  }\n}\n", null, limits);
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            StateFacts.Parse("stateDiagram-v2\nstate A {\n  state B {\n    state C {\n      X\n    }\n  }\n}\n", "s.mmd", limits));

        // Assert
        Assert.Equal(3, accepted.Nodes.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 4, 13), ex.Location);
        Assert.Contains("MermaidLimits.MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every token text a state diagram keeps is checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>: an 11-character text over a limit of 10 is refused, located at its
    ///     line, wherever it stands.
    /// </summary>
    /// <param name="text">The diagram, with one text over the limit.</param>
    /// <param name="line">The line of the long text.</param>
    [Theory]
    [InlineData("stateDiagram-v2\nDescription\n", 2)]
    [InlineData("stateDiagram-v2\nA : Description\n", 2)]
    [InlineData("stateDiagram-v2\nA --> B : Description\n", 2)]
    [InlineData("stateDiagram-v2\nA --> Description\n", 2)]
    [InlineData("stateDiagram-v2\nA:::Description\n", 2)]
    [InlineData("stateDiagram-v2\nstate \"Description\" as A\n", 2)]
    [InlineData("stateDiagram-v2\nstate \"x\" as Description\n", 2)]
    [InlineData("stateDiagram-v2\nstate Description {\n  A\n}\n", 2)]
    [InlineData("stateDiagram-v2\nstate Description <<fork>>\n", 2)]
    [InlineData("stateDiagram-v2\nstate Description [[choice]]\n", 2)]
    [InlineData("stateDiagram-v2\nA\nnote left of A : Description\n", 3)]
    [InlineData("stateDiagram-v2\nA\nnote right of Description : x\n", 3)]
    [InlineData("stateDiagram-v2\nA\nnote left of A\n  Description\nend note\n", 3)]
    [InlineData("stateDiagram-v2\nnote \"Description\" as N\n", 2)]
    [InlineData("stateDiagram-v2\nnote \"x\" as Description\n", 2)]
    [InlineData("stateDiagram-v2\nclassDef Description fill:#f00\n", 2)]
    [InlineData("stateDiagram-v2\nclassDef c fill:#ff00ff00\n", 2)]
    [InlineData("stateDiagram-v2\nA\nstyle A fill:#ff00ff00\n", 3)]
    [InlineData("stateDiagram-v2\nA\nstyle Description fill:red\n", 3)]
    [InlineData("stateDiagram-v2\nA\nclass A Description\n", 3)]
    [InlineData("stateDiagram-v2\nA\nclass Description,A c\n", 3)]
    [InlineData("stateDiagram-v2\nA\nclick A \"Description\" \"t\"\n", 3)]
    [InlineData("stateDiagram-v2\nA\nclick A \"u\" \"Description\"\n", 3)]
    [InlineData("stateDiagram-v2\nA\nclick A href \"Description\"\n", 3)]
    [InlineData("stateDiagram-v2\naccTitle: Description\n", 2)]
    [InlineData("stateDiagram-v2\naccDescr: Description\n", 2)]
    [InlineData("stateDiagram-v2\naccDescr {\nDescription\n}\n", 3)]
    [InlineData("stateDiagram-v2\nscale 12345678901 width\n", 2)]
    public void StateParser_Parse_LongTextAnywhere_ThrowsLimitExceeded(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that long lines of many tokens and thousands of statements are read without recursion, and that the
    ///     line searches of the lexer (directions and pseudo-states) do not rescan a line for every token.
    /// </summary>
    [Fact]
    public void StateParser_Parse_LongLinesAndManyStatements_AreReadIteratively()
    {
        // Arrange
        var words = string.Join(" ", Enumerable.Range(0, 7_000).Select(i => $"w{i}"));
        var statements = string.Join("\n", Enumerable.Range(0, 1_500).Select(i => $"s{i} --> s{i + 1} : step"));
        var limits = new MermaidLimits { MaxEdges = 2_000 };

        // Act
        var lineModel = StateFacts.Parse($"stateDiagram-v2\nstate {words}\nA\n");
        var model = StateFacts.Parse($"stateDiagram-v2\n{statements}\n", null, limits);

        // Assert
        Assert.Equal(7_001, lineModel.RootDocument.Count);
        Assert.Equal("A", Assert.Single(lineModel.States).Id);
        Assert.Equal(1_500, model.Relations.Count);
        Assert.Equal(1_501, model.Nodes.Count);
    }
}
