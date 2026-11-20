using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using static DemaConsulting.CanvasNet.PlantUml.Tests.State.StateTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.State;

/// <summary>
///     Subsystem tests of the State subsystem: a state diagram read by <c>StateParser</c> into a <c>StateModel</c>,
///     whose elements <c>StateRenderer</c> draws, laid out by the Svek engine.
/// </summary>
public class StateTests
{
    /// <summary>
    ///     Validates the whole subsystem on a small model: states with descriptions, pseudo-states, a composite state with
    ///     concurrent regions, a history, notes, and labeled transitions are read, lowered, laid out, and drawn with
    ///     their roles and ids, and drawing twice gives the same scene.
    /// </summary>
    [Fact]
    public void PlantUmlState_ReadLayoutRender_DrawsTheModel()
    {
        // Arrange
        const string Body = "title Player\n[*] --> Stopped\nStopped : no sound\nstate Playing {\n[*] --> Normal\nNormal --> Fast : ff\n}\nstate Effects {\n[*] --> Echo\n--\n[*] --> Reverb\n}\nPlaying --> Effects\nstate check <<choice>>\nstate split <<fork>>\nStopped --> check : play\ncheck --> Playing : [ready]\ncheck --> split\nsplit --> Stopped\nPlaying --> Stopped : stop\nStopped --> Playing[H] : resume\nnote right of Stopped : idle\nStopped --> [*]";
        var model = Parse(Body);

        // Act
        var first = model.Layout(Context());
        var second = model.Layout(Context());

        // Assert
        Assert.Equal(2, model.SubgraphCount);
        Assert.Contains(first.Items, i => i is SceneText { Role: "state-name", Text: "Stopped" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "state-description", Text: "no sound" });
        Assert.Contains(first.Items, i => i.Role == "state-choice" && i.Id == "check");
        Assert.Contains(first.Items, i => i.Role == "state-bar" && i.Id == "split");
        Assert.Contains(first.Items, i => i.Role == "state-history" && i.Id == "Playing..historical.Playing");
        Assert.Contains(first.Items, i => i.Role == "cluster-separator" && i.Id == "Effects");
        Assert.Contains(first.Items, i => i is SceneText { Role: "note-text", Text: "idle" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "link-label", Text: "[ready]" });
        Assert.Contains(first.Items, i => i is SceneText { Role: "title", Text: "Player" });
        Assert.Equal(first.ViewBox, second.ViewBox);
        Assert.Equal(first.Items.Count, second.Items.Count);
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates that the reports of the parser are kept by the model in text order, and that the theme of the
    ///     diagram's skin parameters reaches the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlState_SkinAndReports_ReachTheDrawing()
    {
        // Arrange
        var model = Parse("skinparam stateBackgroundColor #FFEEDD\nskinparam stateBorderColor #884422\nskinparam stateAttributeFontColor #0000FF\nstate A : text\nA x--> B\nstate E <<entryPoint>>");

        // Act
        var scene = model.Layout(Context());

        // Assert
        var box = scene.Items.OfType<ScenePath>().First(p => p.Role == "state" && p.Id == "A");
        Assert.Equal((new Canvas.Rgba32(0xFF, 0xEE, 0xDD, 255), new Canvas.Rgba32(0x88, 0x44, 0x22, 255)), (box.Fill!.Value, box.Stroke!.Color));
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), scene.Items.OfType<SceneText>().Single(t => t.Role == "state-description").Runs[0].Color);
        Assert.Equal([6, 7], model.Reports.Select(r => r.Location.Line));
    }
}
