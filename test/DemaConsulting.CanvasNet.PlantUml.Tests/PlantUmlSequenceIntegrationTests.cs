using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for sequence diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlSequenceIntegrationTests
{
    /// <summary>
    ///     Gets the names of the sequence fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> Fixtures() => PlantUmlReferenceLayoutTests.SequenceFixtures();

    /// <summary>
    ///     Validates that a sequence diagram is painted with PlantUML's default colors: a white background, grey-blue
    ///     participants outlined in #181818, and pale yellow notes.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nparticipant Alice\nparticipant Bob\nAlice -> Bob : hello\nnote over Alice : remember\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var head = scene.Items.OfType<ScenePath>().First(p => p.Role == "participant-head" && p.Id == "Alice");
        var note = scene.Items.OfType<ScenePath>().First(p => p.Role == "note");
        var headBox = Scene.BoundsOf([head])!.Value;
        var noteBox = Scene.BoundsOf([note])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(SkinColors.GreyBlue, surface.GetRowSpan((int)(headBox.Y + headBox.Height - 4))[(int)headBox.X + 3]);
        Assert.Equal(SkinColors.Note, surface.GetRowSpan((int)(noteBox.Y + noteBox.Height - 3))[(int)noteBox.X + 2]);
    }

    /// <summary>
    ///     Validates that every original fixture renders with the features it uses drawn: no unsupported feature, no
    ///     warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetPlantUml_Sequence_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sequence", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of a sequence diagram: participants are nodes, messages are edges, boxes are subgraphs,
    ///     and the title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_Facts_CountParticipantsMessagesAndBoxes()
    {
        // Act
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", "07-page.puml"));

        // Assert
        Assert.Equal("Order Flow", document.Title);
        Assert.Equal(3, document.NodeCount);
        Assert.Equal(4, document.EdgeCount);
        Assert.Equal(1, document.SubgraphCount);
    }

    /// <summary>
    ///     Validates that two renders of the same document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", "05-groups.puml"));
        var document = PlantUmlDocument.Parse(text);
        var scaled = PlantUmlDocument.Parse(text.Replace("@startuml\n", "@startuml\nscale 2\n", StringComparison.Ordinal));

        // Act
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        var size = scaled.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(PlantUmlTestHelpers.PixelsEqual(first, second));
        Assert.Equal((first.Width * 2, first.Height * 2), (size.Width, size.Height));
    }

    /// <summary>
    ///     Validates that statements that are not drawn are reported at their lines, drawn without them, and fail in
    ///     strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nA -> B : one\nnewpage\nref over A, B : other\nA -> B : two\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 4], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a layout over the work limit fails with plantuml-limit-exceeded.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_LayoutWorkLimit_Fails()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", "05-groups.puml"));
        var options = new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 20 } };

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => document.Measure(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
    }
}
