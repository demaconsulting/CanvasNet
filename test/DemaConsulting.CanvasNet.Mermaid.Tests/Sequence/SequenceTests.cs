namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Subsystem tests of the Sequence subsystem: the parser and the model working together, from a whole text to the
///     database facts, the metadata and the located errors. Sequence diagrams are not drawn yet, so the document still
///     reports them as not supported.
/// </summary>
public class SequenceTests
{
    /// <summary>
    ///     Gets the names of the original sequence fixtures.
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
    ///     Gets the folder of the sequence fixtures.
    /// </summary>
    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence");

    /// <summary>
    ///     Validates that every original sequence fixture parses into participants and messages.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Sequence_Parse_Fixture_GivesParticipantsAndMessages(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));

        // Act
        var model = SequenceFacts.Parse(text, name);

        // Assert
        Assert.NotEmpty(model.Actors);
        Assert.NotEmpty(model.Messages);
    }

    /// <summary>
    ///     Validates the metadata of a whole text: title, accessible title and the counts of participants, messages and
    ///     boxes, with the frontmatter, a directive and comments handled by the preprocessing.
    /// </summary>
    [Fact]
    public void Sequence_Parse_WholeText_GivesMetadata()
    {
        // Arrange
        const string Text = "---\nconfig:\n  sequence:\n    wrap: true\n---\n%%{init: {\"theme\": \"forest\"}}%%\nsequenceDiagram\n" +
            "    %% a comment\n    title Checkout\n    accTitle: Paying for an order\n    box Aqua Shop\n    actor Buyer\n" +
            "    participant Cart\n    end\n    Buyer->>+Cart: Add\n    Cart-->>-Buyer: Added\n    Note right of Cart: Saved\n    Cart->>Payments: Charge\n";

        // Act
        var model = SequenceFacts.Parse(Text);

        // Assert
        Assert.Equal(("Checkout", "Paying for an order"), (model.Title, model.AccTitle));
        Assert.Equal((3, 3, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal(["actor", "participant", "participant"], model.Actors.Select(a => a.Type));
    }

    /// <summary>
    ///     Validates that the text fails closed: an error of Mermaid's database is a located syntax error, and no partial
    ///     model is returned.
    /// </summary>
    [Fact]
    public void Sequence_Parse_DatabaseError_FailsClosedWithLocation()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>B: x\n    A->>-B: y\n", "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 3, 5), ex.Location);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that the document still reports a sequence diagram as known but not supported, until it is drawn.
    /// </summary>
    [Fact]
    public void Sequence_Document_IsStillReportedAsNotSupported()
    {
        // Act
        var document = MermaidDocument.Parse("sequenceDiagram\n    A->>B: x\n");

        // Assert
        Assert.Equal(("sequence", false), (document.DiagramType, document.IsSupported));
    }
}
