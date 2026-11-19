using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Subsystem tests for the <c>Sequence</c> subsystem: the parser, the layout and the renderer together, from
///     preprocessed text to a scene.
/// </summary>
public class SequenceTests
{
    /// <summary>
    ///     Validates that a sequence diagram becomes a scene with its participants, life lines, messages and texts in
    ///     PlantUML's places.
    /// </summary>
    [Fact]
    public void PlantUmlSequence_ParseLayoutRender_DrawsParticipantsAndMessages()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("participant Alice\nparticipant Bob\nAlice -> Bob : hello\nBob --> Alice : hi");

        // Assert
        var alice = SequenceTestHelpers.Bounds(scene, "participant-head", "Alice")[0];
        Assert.Equal(new SceneRect(10, 10, 30.345 + 14, 31.609), Round(alice));
        Assert.Equal(2, SequenceTestHelpers.Bounds(scene, "message").Count);
        Assert.Equal(["hello", "hi"], SequenceTestHelpers.Texts(scene, "message-text").Select(t => t.Text));
        Assert.Equal(2, scene.Items.OfType<ScenePath>().Count(p => p.Role == "lifeline"));
    }

    /// <summary>
    ///     Validates that groups, notes, dividers, delays and activations are drawn together without overlap of their
    ///     rows.
    /// </summary>
    [Fact]
    public void PlantUmlSequence_Blocks_AreDrawnInRows()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("loop again\nA -> B ++ : x\nnote right of B : n\nB --> A -- : y\nend\n== next ==\n...\nA -> B");

        // Assert
        var frame = SequenceTestHelpers.Bounds(scene, "group")[0];
        var note = SequenceTestHelpers.Bounds(scene, "note")[0];
        var divider = SequenceTestHelpers.Bounds(scene, "divider")[0];
        Assert.True(note.Y > frame.Y && note.Bottom < frame.Bottom);
        Assert.True(divider.Y > frame.Bottom);
        Assert.Single(SequenceTestHelpers.Bounds(scene, "activation"));
    }

    /// <summary>
    ///     Validates that the skin parameters of a diagram change the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlSequence_SkinParams_ChangeTheDrawing()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("skinparam participantBackgroundColor #00FF00\nskinparam arrowColor #0000FF\nA -> B");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0xFF, 0, 255), scene.Items.OfType<ScenePath>().First(p => p.Role == "participant-head").Fill);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), scene.Items.OfType<ScenePath>().First(p => p.Role == "message").Stroke!.Color);
        Assert.Equal(SkinTheme.White, scene.Background);
    }

    /// <summary>
    ///     Validates that the model of every fixture lays out and draws.
    /// </summary>
    [Fact]
    public void PlantUmlSequence_Fixtures_AllLayOutAndDraw()
    {
        // Arrange
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.puml");

        // Act
        var scenes = files.Select(f => PlantUmlDocument.Parse(File.ReadAllText(f)).LayoutScene(cancellationToken: TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.NotEmpty(scenes);
        Assert.All(scenes, s => Assert.True(s.ViewBox.Width > 20 && s.ViewBox.Height > 20));
    }

    /// <summary>
    ///     Validates that the registry hands a sequence diagram to the sequence parser.
    /// </summary>
    [Fact]
    public void PlantUmlSequence_Registry_ParsesSequenceDiagrams()
    {
        // Act
        var document = PlantUmlDocument.Parse("@startuml\nA -> B\n@enduml\n");

        // Assert
        Assert.True(document.IsSupported);
        Assert.IsType<SequenceModel>(document.Diagram);
    }

    /// <summary>
    ///     Rounds a rectangle to three decimals.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The rounded rectangle.</returns>
    private static SceneRect Round(SceneRect rect) =>
        new(Math.Round(rect.X, 3), Math.Round(rect.Y, 3), Math.Round(rect.Width, 3), Math.Round(rect.Height, 3));
}
