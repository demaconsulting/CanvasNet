using DemaConsulting.CanvasNet.Mermaid.Class;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Class;

/// <summary>
///     Unit tests of <see cref="ClassParser"/>: the statements of Mermaid's class grammar, the quirks of its lexer, located
///     errors with jison's expected tokens, and the limits.
/// </summary>
public class ClassParserTests
{
    /// <summary>The relation forms and the ends and line Mermaid's grammar gives them.</summary>
    public static TheoryData<string, int?, int?, int> RelationCases => new()
    {
        { "<|--", ClassModel.Extension, null, ClassModel.SolidLine },
        { "*--", ClassModel.Composition, null, ClassModel.SolidLine },
        { "o--", ClassModel.Aggregation, null, ClassModel.SolidLine },
        { "<--", ClassModel.Dependency, null, ClassModel.SolidLine },
        { "--", null, null, ClassModel.SolidLine },
        { "--|>", null, ClassModel.Extension, ClassModel.SolidLine },
        { "--*", null, ClassModel.Composition, ClassModel.SolidLine },
        { "--o", null, ClassModel.Aggregation, ClassModel.SolidLine },
        { "-->", null, ClassModel.Dependency, ClassModel.SolidLine },
        { "<|..", ClassModel.Extension, null, ClassModel.DottedLine },
        { "..>", null, ClassModel.Dependency, ClassModel.DottedLine },
        { "..|>", null, ClassModel.Extension, ClassModel.DottedLine },
        { "..", null, null, ClassModel.DottedLine },
        { "<|--|>", ClassModel.Extension, ClassModel.Extension, ClassModel.SolidLine },
        { "*--o", ClassModel.Composition, ClassModel.Aggregation, ClassModel.SolidLine },
        { "<..>", ClassModel.Dependency, ClassModel.Dependency, ClassModel.DottedLine },
    };

    /// <summary>
    ///     Validates that a class statement adds the class with its label, its <c>:::</c> class and its body members in
    ///     order.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_ClassStatement_AddsClassWithLabelClassAndMembers()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    class Account[\"Bank account\"]:::warm {\n        +String owner\n        +deposit(amount)\n    }\n");

        // Assert
        var account = Assert.Single(model.Classes);
        Assert.Equal(("Account", "Bank account", "default warm"), (account.Id, account.Label, account.CssClasses));
        Assert.Equal(["String owner"], account.Members.Select(m => m.Id));
        Assert.Equal(["deposit"], account.Methods.Select(m => m.Id));
    }

    /// <summary>
    ///     Validates that every relation form, written with spaces and without (an <c>o</c> keeps its spaces, or it would join a name),
    ///     maps to Mermaid's ends and line.
    /// </summary>
    /// <param name="relation">The relation as written.</param>
    /// <param name="type1">The expected end at the first class.</param>
    /// <param name="type2">The expected end at the second class.</param>
    /// <param name="line">The expected line.</param>
    [Theory]
    [MemberData(nameof(RelationCases))]
    public void ClassParser_Parse_RelationForm_MapsToRelationCodes(string relation, int? type1, int? type2, int line)
    {
        // Act
        var spaced = ClassFacts.Parse($"classDiagram\n    Left {relation} Right\n");
        var tight = ClassFacts.Parse($"classDiagram\n    Left{relation.Replace("o", " o ", StringComparison.Ordinal)}Right\n");

        // Assert
        Assert.Equal(new ClassRelationType(type1, type2, line), Assert.Single(spaced.Relations).Relation);
        Assert.Equal(new ClassRelationType(type1, type2, line), Assert.Single(tight.Relations).Relation);
        Assert.Equal(("Left", "Right"), (spaced.Relations[0].Id1, spaced.Relations[0].Id2));
    }

    /// <summary>
    ///     Validates that the cardinalities and the label of a relation are kept, and a missing cardinality is <c>none</c>.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_RelationWithCardinalitiesAndLabel_KeepsThem()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    Customer \"1\" --> \"*\" Order : places\n    Order --> \"1\" Payment\n");

        // Assert
        Assert.Equal(
            [("1", "*", "places"), ("none", "1", (string?)null)],
            model.Relations.Select(r => (r.RelationTitle1, r.RelationTitle2, r.Title)));
    }

    /// <summary>
    ///     Validates that a line with a direction anywhere in it is a direction statement, and that the last one wins.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_DirectionAnywhereOnTheLine_IsDirectionStatement()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    direction LR\n    class Alpha\n    whatever direction BT and more\n");

        // Assert
        Assert.Equal("BT", model.Direction);
        Assert.Equal(["Alpha"], model.Classes.Select(c => c.Id));
    }

    /// <summary>
    ///     Validates that only an <c>o</c> on its own is an aggregation end: names that start with <c>o</c> are names.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_NamesStartingWithO_AreNames()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    order o-- owner\n");

        // Assert
        var relation = Assert.Single(model.Relations);
        Assert.Equal(("order", "owner", (int?)ClassModel.Aggregation), (relation.Id1, relation.Id2, relation.Relation.Type1));
    }

    /// <summary>
    ///     Validates that dotted, concatenated, backticked and generic class names are read as Mermaid's grammar builds
    ///     them.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_ClassNames_FollowTheGrammar()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    class Ca-r\n    class `Line Item`\n    class Box~T~\n    class Città\n");

        // Assert
        Assert.Equal(["Ca-r", "Line Item", "Box", "Città"], model.Classes.Select(c => c.Id));
        Assert.Equal("T", model.Classes[2].Type);
    }

    /// <summary>
    ///     Validates that a lone class name is a member statement that does nothing, as in Mermaid (so <c>title</c> lines
    ///     are ignored).
    /// </summary>
    [Fact]
    public void ClassParser_Parse_LoneName_DoesNothing()
    {
        // Act
        var model = ClassFacts.Parse("classDiagram\n    title A title\n    Alpha\n");

        // Assert
        Assert.Empty(model.Classes);
    }

    /// <summary>
    ///     Validates that the click, link and callback statements keep their data and make the class clickable.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_ClickStatements_KeepLinkData()
    {
        // Act
        var model = ClassFacts.Parse(
            "classDiagram\n    class Gateway\n    class Docs\n    click Gateway href \"https://example.com/g\" \"Gateway docs\" _self\n    callback Docs \"show\"\n");

        // Assert
        var gateway = model.Classes[0];
        Assert.Equal(("https://example.com/g", "_self", "Gateway docs"), (gateway.Link, gateway.LinkTarget, gateway.Tooltip));
        Assert.Equal("default clickable", gateway.CssClasses);
        Assert.True(model.Classes[1].HaveCallback);
    }

    /// <summary>
    ///     Validates that a relation without its second class is a located syntax error listing jison's expected tokens.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_MissingTarget_ThrowsLocatedErrorWithExpectedTokens()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nAlpha -->\n", "c.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("c.mmd", 2, 10), ex.Location);
        Assert.Equal(["STR", "ALPHA", "LINE", "DOTTED_LINE", "NUM", "MINUS", "UNICODE_TEXT", "BQUOTE_STR"], ex.Expected);
        Assert.Contains("found 'NEWLINE'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unclosed class body fails at the end of the text with a hint to close it.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_UnclosedBody_FailsAtTheEndOfTheBody()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass Alpha {\n  +int id\n"));

        // Assert
        Assert.Equal(["STRUCT_STOP", "MEMBER"], ex.Expected);
        Assert.Contains("found 'EOF_IN_STRUCT'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'}'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a semicolon at the end of a statement is a lexical error, as in Mermaid's class grammar.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_Semicolon_IsLexicalError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nAlpha --> Beta;\n", "c.mmd"));

        // Assert
        Assert.Equal(new MermaidLocation("c.mmd", 2, 15), ex.Location);
        Assert.Contains("unrecognized text ';'", ex.Message, StringComparison.Ordinal);
        Assert.Empty(ex.Expected);
    }

    /// <summary>
    ///     Validates that a direction on the keyword line turns the whole line into a direction statement, so the text has
    ///     no header and its <c>EOF</c> is refused, as in Mermaid.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_DirectionOnTheKeywordLine_FailsLikeMermaid()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram direction LR\nclass Alpha\n"));

        // Assert
        Assert.Empty(ex.Expected);
        Assert.Contains("found 'EOF'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that namespaces nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> are refused, located at
    ///     the namespace that is too deep.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_NamespacesTooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 2 };
        const string Text = "classDiagram\nnamespace A {\nnamespace B {\nnamespace C {\nclass X\n}\n}\n}\n";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(Text, "c.mmd", limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation("c.mmd", 4, 1), ex.Location);
        Assert.Contains("MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that nested <c>namespace</c> keywords and the dots in their names count together against
    ///     <see cref="MermaidLimits.MaxNestingDepth"/>: two keyword levels whose names nest four namespaces are refused,
    ///     located at the inner name, while two keyword levels that nest three are accepted.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_NestedDottedNamespacesTooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 3 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(
            "classDiagram\nnamespace A.B {\nnamespace C.D {\nclass X\n}\n}\n", "c.mmd", limits));
        var accepted = ClassFacts.Parse("classDiagram\nnamespace A.B {\nnamespace C {\nclass X\n}\n}\n", "c.mmd", limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation("c.mmd", 3, 11), ex.Location);
        Assert.Contains("MaxNestingDepth", ex.Message, StringComparison.Ordinal);
        Assert.Equal("A.B.C", accepted.Namespaces[^1].Id);
    }

    /// <summary>
    ///     Validates that a text longer than <see cref="MermaidLimits.MaxLabelLength"/> is refused.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_LongMember_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass A {\n+an extremely long member name\n}\n", null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every text a class diagram draws or reports is checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>: an 11-character text over a limit of 10 is refused, located at its
    ///     line, wherever it stands.
    /// </summary>
    /// <param name="text">The diagram, with one text one character over the limit.</param>
    /// <param name="line">The line of the long text.</param>
    [Theory]
    [InlineData("classDiagram\nclass Description\n", 2)]
    [InlineData("classDiagram\nclass A[\"Description\"]\n", 2)]
    [InlineData("classDiagram\nclass A~Description~\n", 2)]
    [InlineData("classDiagram\nclass A:::Description\n", 2)]
    [InlineData("classDiagram\nclass A {\n<<Description>>\n}\n", 3)]
    [InlineData("classDiagram\nclass A\n<<Description>> A\n", 3)]
    [InlineData("classDiagram\nA : +Description\n", 2)]
    [InlineData("classDiagram\nA : method(Description)\n", 2)]
    [InlineData("classDiagram\nA --> B : Description\n", 2)]
    [InlineData("classDiagram\nA \"Description\" --> B\n", 2)]
    [InlineData("classDiagram\nA --> \"Description\" B\n", 2)]
    [InlineData("classDiagram\nA --> Description\n", 2)]
    [InlineData("classDiagram\nnote \"Description\"\n", 2)]
    [InlineData("classDiagram\nclass A\nnote for A \"Description\"\n", 3)]
    [InlineData("classDiagram\nnamespace Description {\nclass A\n}\n", 2)]
    [InlineData("classDiagram\nnamespace N[\"Description\"] {\nclass A\n}\n", 2)]
    [InlineData("classDiagram\nclassDef Description fill:#f00\n", 2)]
    [InlineData("classDiagram\nclassDef c fill:#ff00ff00\n", 2)]
    [InlineData("classDiagram\nclass A\nstyle A fill:#ff00ff00\n", 3)]
    [InlineData("classDiagram\nclass A\ncssClass \"A\" Description\n", 3)]
    [InlineData("classDiagram\nclass A\nlink A \"Description\"\n", 3)]
    [InlineData("classDiagram\nclass A\nlink A \"x\" \"Description\"\n", 3)]
    [InlineData("classDiagram\nclass A\ncallback A \"Description\"\n", 3)]
    [InlineData("classDiagram\nclass A\nclick A call Description()\n", 3)]
    [InlineData("classDiagram\nclass A\nclick A href \"Description\"\n", 3)]
    [InlineData("classDiagram\naccTitle: Description\n", 2)]
    [InlineData("classDiagram\naccDescr: Description\n", 2)]
    [InlineData("classDiagram\naccDescr {\nDescription\n}\n", 3)]
    public void ClassParser_Parse_LongTextAnywhere_ThrowsLimitExceeded(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a class name of thousands of tokens is read without recursion.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_NameOfManyTokens_IsReadIteratively()
    {
        // Arrange
        var name = string.Join("-", Enumerable.Repeat("a", 15_000));
        var limits = new MermaidLimits { MaxLabelLength = 100_000 };

        // Act
        var model = ClassFacts.Parse($"classDiagram\nclass {name}\n", null, limits);

        // Assert
        Assert.Equal(name, Assert.Single(model.Classes).Id);
    }

    /// <summary>
    ///     Validates that a text of thousands of class, member, method and relation statements is read without recursion
    ///     into all of its classes, members and relations.
    /// </summary>
    [Fact]
    public void ClassParser_Parse_TextOfManyStatements_IsReadIteratively()
    {
        // Arrange
        const int count = 1_500;
        var text = new System.Text.StringBuilder("classDiagram\n");
        for (var i = 0; i < count; i++)
        {
            text.Append("class C").Append(i).Append('\n');
            text.Append('C').Append(i).Append(" : +int value\n");
            text.Append('C').Append(i).Append(" : +run()\n");
            text.Append('C').Append(i).Append(" <|-- C").Append(i + 1).Append('\n');
        }

        var limits = new MermaidLimits { MaxTextSize = 1_000_000, MaxNodes = 10_000, MaxEdges = 10_000 };

        // Act
        var model = ClassFacts.Parse(text.ToString(), null, limits);

        // Assert
        Assert.Equal(count + 1, model.Classes.Count);
        Assert.Equal(count, model.Relations.Count);
        Assert.Equal(count, model.Classes.Count(c => c.Members.Count == 1 && c.Methods.Count == 1));
    }
}
