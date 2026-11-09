namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Subsystem tests of the Sequence subsystem: the parser, the model, the layout and the renderer working together,
///     from a whole text to the database facts, the metadata, the located errors and the scene.
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
    ///     Validates that the document reports a sequence diagram as supported, with its metadata: participants as nodes,
    ///     arrows as edges, boxes as subgraphs and no direction.
    /// </summary>
    [Fact]
    public void Sequence_Document_IsSupportedWithMetadata()
    {
        // Act
        var document = MermaidDocument.Parse("sequenceDiagram\n    box Back end\n    participant A\n    end\n    A->>B: x\n    Note over A: n\n    B-->>A: y\n");

        // Assert
        Assert.Equal(("sequence", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((2, 2, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
    }

    /// <summary>
    ///     Validates that every original sequence fixture lays out into a scene with participants and a positive size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Sequence_Layout_Fixture_DrawsParticipants(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));

        // Act
        var scene = SceneQuery.Layout(text);

        // Assert
        Assert.True(scene.ViewBox.Width > 0 && scene.ViewBox.Height > 0);
        Assert.NotEmpty(SceneQuery.OfRole(scene, "actor-top"));
    }
}
