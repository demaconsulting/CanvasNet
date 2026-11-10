using DemaConsulting.CanvasNet.Mermaid.Er;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Er;

/// <summary>
///     Unit tests of <see cref="ErParser"/>: the statements of Mermaid's ER grammar, the order of its cardinality rules
///     and the quirks of its lexer, located errors with jison's expected tokens, and the limits.
/// </summary>
public class ErParserTests
{
    /// <summary>
    ///     Validates that every cardinality, written as a symbol or in words, is read in Mermaid's lexer order: the left
    ///     one becomes <c>cardB</c>.
    /// </summary>
    /// <param name="cardinality">The cardinality as written.</param>
    /// <param name="expected">Mermaid's cardinality.</param>
    [Theory]
    [InlineData("|o", "ZERO_OR_ONE")]
    [InlineData("o|", "ZERO_OR_ONE")]
    [InlineData("one or zero", "ZERO_OR_ONE")]
    [InlineData("zero or one", "ZERO_OR_ONE")]
    [InlineData("}o", "ZERO_OR_MORE")]
    [InlineData("o{", "ZERO_OR_MORE")]
    [InlineData("zero or more", "ZERO_OR_MORE")]
    [InlineData("zero or many", "ZERO_OR_MORE")]
    [InlineData("0+", "ZERO_OR_MORE")]
    [InlineData("many(0)", "ZERO_OR_MORE")]
    [InlineData("many", "ZERO_OR_MORE")]
    [InlineData("}|", "ONE_OR_MORE")]
    [InlineData("|{", "ONE_OR_MORE")]
    [InlineData("one or more", "ONE_OR_MORE")]
    [InlineData("one or many", "ONE_OR_MORE")]
    [InlineData("1+", "ONE_OR_MORE")]
    [InlineData("many(1)", "ONE_OR_MORE")]
    [InlineData("||", "ONLY_ONE")]
    [InlineData("one", "ONLY_ONE")]
    [InlineData("only one", "ONLY_ONE")]
    [InlineData("1", "ONLY_ONE")]
    [InlineData("u", "MD_PARENT")]
    public void ErParser_Parse_LeftCardinality_FollowsMermaidsLexerOrder(string cardinality, string expected)
    {
        // Act
        var model = ErFacts.Parse($"erDiagram\nA {cardinality}--o{{ B : r\n");

        // Assert
        var spec = Assert.Single(model.Relationships).RelSpec;
        Assert.Equal((expected, "ZERO_OR_MORE"), (spec.CardB, spec.CardA));
    }

    /// <summary>
    ///     Validates the relationship types: <c>--</c> and <c>to</c> are identifying, <c>..</c>, <c>.-</c>, <c>-.</c> and
    ///     <c>optionally to</c> are not.
    /// </summary>
    /// <param name="type">The type as written.</param>
    /// <param name="expected">Mermaid's identification.</param>
    [Theory]
    [InlineData("--", "IDENTIFYING")]
    [InlineData(" to ", "IDENTIFYING")]
    [InlineData("..", "NON_IDENTIFYING")]
    [InlineData(".-", "NON_IDENTIFYING")]
    [InlineData("-.", "NON_IDENTIFYING")]
    [InlineData(" optionally to ", "NON_IDENTIFYING")]
    public void ErParser_Parse_RelationshipType_GivesTheIdentification(string type, string expected)
    {
        // Act
        var model = ErFacts.Parse($"erDiagram\nA ||{type}|| B : r\n");

        // Assert
        Assert.Equal(expected, Assert.Single(model.Relationships).RelSpec.RelType);
    }

    /// <summary>
    ///     Validates that names that start like keywords are names, while the keywords in any case are keywords.
    /// </summary>
    [Fact]
    public void ErParser_Parse_KeywordLikeNamesAndKeywordsInAnyCase_FollowTheLexer()
    {
        // Act
        var names = ErFacts.Parse("erDiagram\nonerous ||--o{ toaster : uses\nmanyfold ||--|| user : has\nonly ||--|| zero : rel\n");
        var keywords = ErFacts.Parse("erDiagram\nCLASSDEF c fill:red\nA\nCLASS A c\nSTYLE A stroke:blue\nA ONE OR MANY TO MANY B : r\n");

        // Assert
        Assert.Equal(["onerous", "toaster", "manyfold", "user", "only", "zero"], names.Entities.Select(e => e.Name));
        var a = keywords.Entities[0];
        Assert.Equal(("default c", "stroke:blue"), (a.CssClasses, Assert.Single(a.CssStyles)));
        Assert.Equal(new ErRelationshipSpec("ZERO_OR_MORE", "IDENTIFYING", "ONE_OR_MORE"), Assert.Single(keywords.Relationships).RelSpec);
    }

    /// <summary>
    ///     Validates the roles: quoted (the quotes removed, empty allowed) or one unquoted word; the other words of an
    ///     unquoted role become entities, as in Mermaid.
    /// </summary>
    [Fact]
    public void ErParser_Parse_Roles_AreQuotedOrOneWord()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\nA ||--|| B : \"\"\nB ||--|| C : plain words here\nC ||--|| D : \"quoted: with colon\"\n");

        // Assert
        Assert.Equal([string.Empty, "plain", "quoted: with colon"], model.Relationships.Select(r => r.RoleA));
        Assert.Equal(["A", "B", "C", "words", "here", "D"], model.Entities.Select(e => e.Name));
    }

    /// <summary>
    ///     Validates the attribute forms: generic, array, optional and backtick types, keys in any case and in comma lists,
    ///     and comments without their quotes.
    /// </summary>
    [Fact]
    public void ErParser_Parse_Attributes_KeepTypesKeysAndComments()
    {
        // Act
        var model = ErFacts.Parse(
            "erDiagram\nT {\n  List~int~ ids\n  string[] tags PK, FK \"key\"\n  decimal? amount\n  `geo.point` `display.name` uk\n  int code FK,UK,PK \"all\"\n  int g \"comment only\"\n}\n");

        // Assert
        Assert.Equal(
            [
                new ErAttribute("List~int~", "ids", [], string.Empty),
                new ErAttribute("string[]", "tags", ["PK", "FK"], "key"),
                new ErAttribute("decimal?", "amount", [], string.Empty),
                new ErAttribute("geo.point", "display.name", ["uk"], string.Empty),
                new ErAttribute("int", "code", ["FK", "UK", "PK"], "all"),
                new ErAttribute("int", "g", [], "comment only"),
            ],
            Assert.Single(model.Entities).Attributes,
            (x, y) => (x.Type, x.Name, x.Comment) == (y.Type, y.Name, y.Comment) && x.Keys.SequenceEqual(y.Keys));
    }

    /// <summary>
    ///     Validates the quirk of a generic type: from a tilde, everything up to the last tilde of the line is one
    ///     attribute word.
    /// </summary>
    [Fact]
    public void ErParser_Parse_GenericType_TakesTheLineUpToItsLastTilde()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\nT {\n  List~int~ a, Map~x~ b\n}\n");

        // Assert
        var attribute = Assert.Single(Assert.Single(model.Entities).Attributes);
        Assert.Equal(("List~int~ a, Map~x~", "b"), (attribute.Type, attribute.Name));
    }

    /// <summary>
    ///     Validates the alias forms: quoted or bare, with classes and a block; only the first non-empty alias is kept.
    /// </summary>
    [Fact]
    public void ErParser_Parse_Aliases_KeepTheFirst()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\na[\"Quoted alias\"]\nb[Bare]\nc[\"First\"]\nc[\"Second\"]\nd\nd[\"Late\"]\ne[\"E\"]:::hot {\n  int id\n}\n");

        // Assert
        Assert.Equal(["Quoted alias", "Bare", "First", "Late", "E"], model.Entities.Select(e => e.Alias));
        var e = model.Entities[4];
        Assert.Equal(("default hot", 1), (e.CssClasses, e.Attributes.Count));
    }

    /// <summary>
    ///     Validates that a line with <c>direction LR</c> anywhere in it is a direction statement, and that a direction
    ///     inside a subgraph is the subgraph's.
    /// </summary>
    [Fact]
    public void ErParser_Parse_DirectionLines_FollowTheLexer()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\nA ||--o{ B : r %% direction LR\nsubgraph S\n  direction BT\n  C\n  direction RL\nend\n");

        // Assert
        Assert.Equal("LR", model.Direction);
        Assert.Equal(["C"], model.Entities.Select(e => e.Name));
        Assert.Equal("RL", Assert.Single(model.Subgraphs).Dir);
    }

    /// <summary>
    ///     Validates that the values of the statements of a subgraph are its nodes, as in Mermaid: the accessibility texts
    ///     and the keywords of style statements too.
    /// </summary>
    [Fact]
    public void ErParser_Parse_StatementValues_BecomeSubgraphNodes()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\nsubgraph S[\"Sales\" area]\n  accTitle: hello\n  classDef c fill:red\n  class A c\n  style A fill:red\n  A\n  B ||--o{ C : r\nend\n");

        // Assert
        var subgraph = Assert.Single(model.Subgraphs);
        Assert.Equal(("S", "Sales area"), (subgraph.Id, subgraph.Title));
        Assert.Equal(["hello", "classDef", "class", "style", "A", "B", "C"], subgraph.Nodes);
    }

    /// <summary>
    ///     Validates that grammar errors fail where Mermaid fails, located, with jison's expected tokens.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="expected">The first expected token.</param>
    /// <param name="count">The number of expected tokens.</param>
    [Theory]
    [InlineData("erDiagram\nA ||--o{ B\n", 2, "COLON", 2)]
    [InlineData("erDiagram\nend\n", 2, "EOF", 19)]
    [InlineData("erDiagram\nT {\n  int a PK,\n}\n", 4, "ATTRIBUTE_KEY", 1)]
    [InlineData("erDiagram\nT {\n  string?\n}\n", 4, "ATTRIBUTE_WORD", 1)]
    [InlineData("erDiagram\nA ||--o{ B : one\n", 2, "UNICODE_TEXT", 3)]
    [InlineData("erDiagram\nA || to || B : to\n", 2, "UNICODE_TEXT", 3)]
    [InlineData("erDiagram\nsubgraph one[\"Title\"]\nend\n", 2, "UNICODE_TEXT", 5)]
    [InlineData("erDiagram\nA ||--|| B : r;\n", 2, "EOF", 20)]
    [InlineData("erDiagram\nA ||--|| B : r %% note\n", 2, "EOF", 20)]
    [InlineData("erDiagram\n1\nA\n", 2, "EOF", 20)]
    public void ErParser_Parse_GrammarError_ThrowsLocatedErrorWithExpectedTokens(string text, int line, string expected, int count)
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Equal(expected, ex.Expected[0]);
        Assert.Equal(count, ex.Expected.Count);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
    }

    /// <summary>
    ///     Validates that an unclosed subgraph fails at the end of the text with <c>END</c> expected.
    /// </summary>
    [Fact]
    public void ErParser_Parse_UnclosedSubgraph_FailsAtTheEnd()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse("erDiagram\nsubgraph S\n  A\n"));

        // Assert
        Assert.Contains("found the end of the text", ex.Message, StringComparison.Ordinal);
        Assert.Contains("END", ex.Expected);
        Assert.Contains("'end'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that text no rule of a style statement matches (a dot) is a located lexical error.
    /// </summary>
    [Fact]
    public void ErParser_Parse_DotInAStyle_IsALexicalError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse("erDiagram\nA\nstyle A stroke-width:1.5px\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Contains("unrecognized text '.'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation(null, 3, 23), ex.Location);
    }

    /// <summary>
    ///     Validates that subgraphs nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> fail, located at the
    ///     subgraph that is one too deep.
    /// </summary>
    [Fact]
    public void ErParser_Parse_SubgraphsTooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 2 };

        // Act
        var accepted = ErFacts.Parse("erDiagram\nsubgraph A\nsubgraph B\nX\nend\nend\n", null, limits);
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            ErFacts.Parse("erDiagram\nsubgraph A\nsubgraph B\nsubgraph C\nX\nend\nend\nend\n", null, limits));

        // Assert
        Assert.Equal(2, accepted.Subgraphs.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(4, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every text the parser keeps, and the styles and titles it joins, are checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, located at the text.
    /// </summary>
    /// <param name="text">The text, with one text over the limit of 10.</param>
    /// <param name="line">The line of the long text.</param>
    [Theory]
    [InlineData("erDiagram\nDescription\n", 2)]
    [InlineData("erDiagram\n\"Two Words!\"\n", 2)]
    [InlineData("erDiagram\nA[Description]\n", 2)]
    [InlineData("erDiagram\nA ||--o{ Description : r\n", 2)]
    [InlineData("erDiagram\nA ||--o{ B : Description\n", 2)]
    [InlineData("erDiagram\nA ||--o{ B : \"long role\"\n", 2)]
    [InlineData("erDiagram\nA:::Description\n", 2)]
    [InlineData("erDiagram\nA {\n  Description a\n}\n", 3)]
    [InlineData("erDiagram\nA {\n  int Description\n}\n", 3)]
    [InlineData("erDiagram\nA {\n  abcdefghij? a\n}\n", 3)]
    [InlineData("erDiagram\nA {\n  `Description` a\n}\n", 3)]
    [InlineData("erDiagram\nA {\n  int a \"Description\"\n}\n", 3)]
    [InlineData("erDiagram\nA\nclass A Description\n", 3)]
    [InlineData("erDiagram\nclassDef Description fill:red\n", 2)]
    [InlineData("erDiagram\nclassDef c Description\n", 2)]
    [InlineData("erDiagram\nclassDef c fill:#abcde\n", 2)]
    [InlineData("erDiagram\nA\nstyle A fill:#abcde\n", 3)]
    [InlineData("erDiagram\nsubgraph Description\nend\n", 2)]
    [InlineData("erDiagram\nsubgraph S[Description]\nend\n", 2)]
    [InlineData("erDiagram\nsubgraph S[abc def ghi]\nend\n", 2)]
    [InlineData("erDiagram\naccTitle: Description\n", 2)]
    [InlineData("erDiagram\naccDescr: Description\n", 2)]
    [InlineData("erDiagram\naccDescr {\nDescription\n}\n", 3)]
    public void ErParser_Parse_LongTextAnywhere_ThrowsLimitExceeded(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse(text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that long lines of many tokens and thousands of statements are read without recursion, and that the
    ///     line searches of the lexer (directions and tildes) do not rescan a line for every token.
    /// </summary>
    [Fact]
    public void ErParser_Parse_LongLinesAndManyStatements_AreReadIteratively()
    {
        // Arrange
        var words = string.Join(" ", Enumerable.Range(0, 5_000).Select(i => $"w{i}"));
        var keys = string.Join(",", Enumerable.Repeat("PK", 5_000));
        var statements = string.Join("\n", Enumerable.Range(0, 1_500).Select(i => $"e{i} ||--o{{ e{i + 1} : step"));
        var limits = new MermaidLimits { MaxNodes = 6_000, MaxEdges = 2_000 };

        // Act
        var lineModel = ErFacts.Parse($"erDiagram\n{words}\n", null, limits);
        var keyModel = ErFacts.Parse($"erDiagram\nT {{\n  int a {keys} \"~\"\n}}\n");
        var model = ErFacts.Parse($"erDiagram\n{statements}\n", null, limits);

        // Assert
        Assert.Equal(5_000, lineModel.Entities.Count);
        Assert.Equal(5_000, Assert.Single(Assert.Single(keyModel.Entities).Attributes).Keys.Count);
        Assert.Equal(1_500, model.Relationships.Count);
        Assert.Equal(1_501, model.Entities.Count);
    }
}
