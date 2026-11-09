using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of <see cref="SequenceParser"/>: the lexer quirks of Mermaid's sequence grammar, the statements it lists
///     for the database, located errors with jison's expected tokens, and the limits.
/// </summary>
public class SequenceParserTests
{
    /// <summary>The 26 arrows and the line types the grammar maps them to.</summary>
    public static TheoryData<string, int> ArrowCases => new()
    {
        { "->", SequenceLineType.SolidOpen },
        { "-->", SequenceLineType.DottedOpen },
        { "->>", SequenceLineType.Solid },
        { "-->>", SequenceLineType.Dotted },
        { "<<->>", SequenceLineType.BidirectionalSolid },
        { "<<-->>", SequenceLineType.BidirectionalDotted },
        { "-x", SequenceLineType.SolidCross },
        { "--x", SequenceLineType.DottedCross },
        { "-)", SequenceLineType.SolidPoint },
        { "--)", SequenceLineType.DottedPoint },
        { "-|\\", SequenceLineType.SolidTop },
        { "-|/", SequenceLineType.SolidBottom },
        { "-\\\\", SequenceLineType.StickTop },
        { "-//", SequenceLineType.StickBottom },
        { "--|\\", SequenceLineType.SolidTopDotted },
        { "--|/", SequenceLineType.SolidBottomDotted },
        { "--\\\\", SequenceLineType.StickTopDotted },
        { "--//", SequenceLineType.StickBottomDotted },
        { "/|-", SequenceLineType.SolidArrowTopReverse },
        { "\\|-", SequenceLineType.SolidArrowBottomReverse },
        { "//-", SequenceLineType.StickArrowTopReverse },
        { "\\\\-", SequenceLineType.StickArrowBottomReverse },
        { "/|--", SequenceLineType.SolidArrowTopReverseDotted },
        { "\\|--", SequenceLineType.SolidArrowBottomReverseDotted },
        { "//--", SequenceLineType.StickArrowTopReverseDotted },
        { "\\\\--", SequenceLineType.StickArrowBottomReverseDotted },
    };

    /// <summary>
    ///     Validates that a signal lists both participants, then the message with its text.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_Signal_AddsParticipantsAndMessage()
    {
        // Act
        var model = SequenceFacts.Parse("sequenceDiagram\n    Client->>Server: Request\n    Server-->>Client: Reply\n");

        // Assert
        Assert.Equal(["Client", "Server"], model.Actors.Select(a => a.Id));
        Assert.Equal(
            [("Client", "Server", "Request", SequenceLineType.Solid), ("Server", "Client", "Reply", SequenceLineType.Dotted)],
            model.Messages.Select(m => (m.From, m.To, m.Text, m.Type)));
        Assert.Equal(["0", "1"], model.Messages.Select(m => m.Id));
    }

    /// <summary>
    ///     Validates that every arrow, with and without spaces around it, maps to its line type.
    /// </summary>
    /// <param name="arrow">The arrow.</param>
    /// <param name="lineType">The expected line type.</param>
    [Theory]
    [MemberData(nameof(ArrowCases))]
    public void SequenceParser_Parse_Arrow_MapsToLineType(string arrow, int lineType)
    {
        // Act
        var tight = SequenceFacts.Parse($"sequenceDiagram\n    Left{arrow}Right: x\n");
        var spaced = SequenceFacts.Parse($"sequenceDiagram\n    Left {arrow} Right: x\n");

        // Assert
        Assert.Equal(("Left", "Right", lineType), (tight.Messages[0].From, tight.Messages[0].To, tight.Messages[0].Type));
        Assert.Equal(("Left", "Right", lineType), (spaced.Messages[0].From, spaced.Messages[0].To, spaced.Messages[0].Type));
    }

    /// <summary>
    ///     Validates the participant names Mermaid's lexer makes: dashes inside names, spaces, a comment and an alias after
    ///     a spaced name all stay in the name.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_ParticipantNames_FollowMermaidLexer()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    participant Back Office as BO\n    participant Alpha # the first\n    Front-End->>Big Server: call\n");

        // Assert
        Assert.Equal(["Back Office as BO", "Alpha # the first", "Front-End", "Big Server"], model.Actors.Select(a => a.Id));
    }

    /// <summary>
    ///     Validates that a keyword at the start of a line is a keyword even before an arrow, so the line fails as in
    ///     Mermaid, at the end of the text where the block is still open.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_KeywordAsName_FailsAtTheEndLikeMermaid()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    Loop->>Loop: x\n"));

        // Assert
        Assert.Contains("found the end of the text", ex.Message, StringComparison.Ordinal);
        Assert.Contains("end", ex.Expected);
        Assert.Contains("'end'", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a missing message text is a located syntax error listing jison's expected token.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_MissingText_ThrowsLocatedErrorWithExpectedTokens()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    Left->>Right\n", "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 2, 17), ex.Location);
        Assert.Equal(["TXT"], ex.Expected);
        Assert.Contains("found 'NEWLINE'", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that a token that cannot follow a whole statement lists the merged lookahead of jison's state: every
    ///     line start and the keywords that end or continue a block.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_TokenAfterStatement_ListsMergedFollowers()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>B: x\n    , y\n"));

        // Assert
        Assert.Contains("found ','", ex.Message, StringComparison.Ordinal);
        Assert.Equal(34, ex.Expected.Count);
        Assert.Equal(["break", "option", "and", "else", "participant"], ex.Expected.Skip(21).Take(5));
    }

    /// <summary>
    ///     Validates that a participant name whose lookahead state rejects the next token fails in that state, listing
    ///     the merged followers of a name.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_MetadataAfterActivatedName_ListsNameFollowers()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    activate Item@{ type: queue }\n"));

        // Assert
        Assert.Contains("found 'CONFIG_START'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["NEWLINE", ",", "()"], ex.Expected.Take(3));
        Assert.Equal("TXT", ex.Expected[^1]);
    }

    /// <summary>
    ///     Validates that a stray <c>end</c> fails in the document state, without <c>end</c> among the expected tokens.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_StrayEnd_ListsLineStartsOnly()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>B: x\n    end\n"));

        // Assert
        Assert.Contains("found 'end'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("end", ex.Expected);
        Assert.Equal(30, ex.Expected.Count);
    }

    /// <summary>
    ///     Validates that a participant keyword without a name is a located lexical error.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_MissingParticipantName_ThrowsLexicalError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    participant\n    A->>B: x\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 16), ex.Location);
        Assert.Contains("unrecognized text", ex.Message, StringComparison.Ordinal);
        Assert.Contains("participant", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the participant statements: keyword types, aliases with their wrap prefix, metadata and
    ///     <c>create</c>.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_ParticipantStatements_SetTypesAliasesAndMetadata()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    actor User as wrap:The user\n    participant Store@{ \"type\": \"database\", \"alias\": \"Data\" }\n" +
            "    User->>Store: hi\n    create participant Temp\n    Store->>Temp: make\n");

        // Assert
        Assert.Equal(
            [("User", "The user", "actor", (bool?)true), ("Store", "Data", "database", null), ("Temp", "Temp", "participant", null)],
            model.Actors.Select(a => (a.Id, a.Description, a.Type, a.Wrap)));
        Assert.Equal(1, model.CreatedActors["Temp"]);
    }

    /// <summary>
    ///     Validates that invalid participant metadata is a YAML error located inside the metadata.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_InvalidMetadata_ThrowsLocatedYamlError()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    participant Item@{ \"type\": [ }\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlInvalid, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.InRange(ex.Location.Column, 23, 34);
    }

    /// <summary>
    ///     Validates the notes, the activation statements and shorthands, and the central connections, in Mermaid's
    ///     order of database calls.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_NotesActivationsAndCentralConnections_ListMessagesInOrder()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    Note over A,B: both\n    A->>+B: on\n    B-->>-A: off\n    activate A\n    deactivate A\n    A()->>()B: dual\n");

        // Assert
        Assert.Equal(
            [
                SequenceLineType.Note, SequenceLineType.Solid, SequenceLineType.ActiveStart, SequenceLineType.Dotted,
                SequenceLineType.ActiveEnd, SequenceLineType.ActiveStart, SequenceLineType.ActiveEnd, SequenceLineType.Solid,
                SequenceLineType.CentralConnection, SequenceLineType.CentralConnectionReverse,
            ],
            model.Messages.Select(m => m.Type));
        Assert.Equal(("A", "B", SequencePlacement.Over), (model.Notes[0].From, model.Notes[0].To, model.Notes[0].Placement));
        Assert.Equal((true, SequenceLineType.CentralConnectionDual), (model.Messages[7].Activate, model.Messages[7].CentralConnection));
    }

    /// <summary>
    ///     Validates that blocks list their start, section and end markers with their titles in order.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_Blocks_ListMarkersInOrder()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    alt Yes\n      A->>B: a\n    else No\n    end\n    par_over P\n    and Q\n    end\n" +
            "    critical C\n    option O\n    end\n    rect rgb(1, 2, 3)\n    end\n");

        // Assert
        Assert.Equal(
            [
                (SequenceLineType.AltStart, "Yes"), (SequenceLineType.Solid, "a"), (SequenceLineType.AltElse, "No"),
                (SequenceLineType.AltEnd, ""), (SequenceLineType.ParOverStart, "P"), (SequenceLineType.ParAnd, "Q"),
                (SequenceLineType.ParEnd, ""), (SequenceLineType.CriticalStart, "C"), (SequenceLineType.CriticalOption, "O"),
                (SequenceLineType.CriticalEnd, ""), (SequenceLineType.RectStart, "rgb(1, 2, 3)"), (SequenceLineType.RectEnd, ""),
            ],
            model.Messages.Select(m => (m.Type, m.Text)));
    }

    /// <summary>
    ///     Validates the <c>autonumber</c> forms, with JavaScript numbers.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_Autonumber_KeepsStartStepAndVisibility()
    {
        // Act
        var model = SequenceFacts.Parse("sequenceDiagram\n    autonumber\n    autonumber 10\n    autonumber .5 1.25\n    autonumber off\n");

        // Assert
        Assert.Equal(
            [new SequenceAutonumber(null, null, true), new SequenceAutonumber(10, 1, true), new SequenceAutonumber(0.5, 1.25, true), new SequenceAutonumber(null, null, false)],
            model.Messages.Select(m => m.Autonumber));
    }

    /// <summary>
    ///     Validates the title and accessibility statements: the title is cut after its keyword and keeps trailing space;
    ///     the accessible texts are trimmed.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_TitleAndAccessibility_SetTexts()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    title Before # after\n    accTitle:   Title  \n    accDescr {\n      One\n      Two\n    }\n");

        // Assert
        Assert.Equal(("Before ", "Title", "One\nTwo"), (model.DiagramTitle, model.AccessibleTitle, model.AccessibleDescription));
    }

    /// <summary>
    ///     Validates that blocks nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> are refused, located at
    ///     the block that is too deep.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_NestingDeeperThanLimit_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 3 };
        const string Text = "sequenceDiagram\nloop a\nopt b\nbox c\nend\nend\nend\nloop a\nopt b\nalt c\nbreak d\nend\nend\nend\nend\n";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse(Text, limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 11, 1), ex.Location);
    }

    /// <summary>
    ///     Validates that a text longer than <see cref="MermaidLimits.MaxLabelLength"/> is refused.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_LongText_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>B: a text that is too long\n", limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 10), ex.Location);
    }

    /// <summary>
    ///     Validates that long names made of dash-separated parts and many statements parse with the expected result
    ///     (the lexer is linear, without backtracking).
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_LongDashedNamesAndManyStatements_Parses()
    {
        // Arrange
        var limits = new MermaidLimits { MaxTextSize = 2_000_000, MaxLabelLength = 1_000_000 };
        var name = "a" + string.Concat(Enumerable.Repeat("-b", 50_000));
        var lines = string.Concat(Enumerable.Range(0, 5_000).Select(i => $"P{i % 50}->>P{(i + 1) % 50}: m{i}\n"));

        // Act
        var model = SequenceFacts.Parse($"sequenceDiagram\n{name}->>B: x\n{lines}", limits: limits);

        // Assert
        Assert.Equal(name, model.Actors[0].Id);
        Assert.Equal(5_001, model.Messages.Count);
        Assert.Equal(52, model.Actors.Count);
    }

    /// <summary>
    ///     Validates that a grammar error later in the text is reported before a database error earlier in it, because
    ///     the statements are applied only after the whole text has parsed.
    /// </summary>
    [Fact]
    public void SequenceParser_Parse_GrammarAndDatabaseErrors_GrammarErrorFirst()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    deactivate A\n    A->>B\n"));

        // Assert
        Assert.Equal(["TXT"], ex.Expected);
    }
}
