using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Subsystem tests for the <c>Activity</c> subsystem: the parser, the model, the layout and the renderer together,
///     from preprocessed text to a scene.
/// </summary>
public class ActivityTests
{
    /// <summary>
    ///     Validates that an activity diagram becomes a scene with its nodes, actions, decisions and arrows in PlantUML's
    ///     places.
    /// </summary>
    [Fact]
    public void PlantUmlActivity_ParseLayoutRender_DrawsNodesActionsAndArrows()
    {
        // Act
        var scene = ActivityTestHelpers.Draw("start\n:Read input;\nif (valid?) then (yes)\n:Process the record;\nelse (no)\n:Reject;\nendif\n:Log;\nstop");

        // Assert
        Assert.Equal(new SceneRect(0, 0, 230, 326), new SceneRect(0, 0, Math.Ceiling(scene.ViewBox.Width), Math.Ceiling(scene.ViewBox.Height)));
        Assert.Equal(4, ActivityTestHelpers.Bounds(scene, "activity").Count);
        Assert.Equal(2, ActivityTestHelpers.Bounds(scene, "diamond").Count);
        Assert.Single(ActivityTestHelpers.Bounds(scene, "start"));
        Assert.Equal(2, ActivityTestHelpers.Bounds(scene, "stop").Count);
        Assert.Equal(["valid?", "yes", "no"], ActivityTestHelpers.Texts(scene, "diamond-text").Concat(ActivityTestHelpers.Texts(scene, "arrow-text")).Select(t => t.Text));
    }

    /// <summary>
    ///     Validates that the blocks of the language nest: a loop in a branch of a fork in a swimlane, with notes and a
    ///     partition.
    /// </summary>
    [Fact]
    public void PlantUmlActivity_NestedBlocks_AreDrawnInsideEachOther()
    {
        // Act
        var scene = ActivityTestHelpers.Draw("|Team|\nstart\npartition Work {\nfork\nwhile (more?)\n:item;\nnote right: each\nendwhile\nfork again\nrepeat\n:try;\nrepeat while (again?)\nend fork\n}\n|Other|\n:done;\nstop");

        // Assert
        var frame = ActivityTestHelpers.Bounds(scene, "partition")[0];
        Assert.All(ActivityTestHelpers.Bounds(scene, "activity").Take(2), b => Assert.True(b.X > frame.X && b.Bottom < frame.Bottom));
        Assert.NotEmpty(ActivityTestHelpers.Bounds(scene, "note"));
        Assert.Equal(2, ActivityTestHelpers.Texts(scene, "lane-title").Count);
        Assert.Equal(2, ActivityTestHelpers.Bounds(scene, "bar").Count);
    }

    /// <summary>
    ///     Validates that the skin parameters of a diagram change the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlActivity_SkinParams_ChangeTheDrawing()
    {
        // Act
        var scene = ActivityTestHelpers.Draw("skinparam activityBackgroundColor #00FF00\nskinparam arrowColor #0000FF\nstart\n:a;\nstop");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0xFF, 0, 255), scene.Items.OfType<ScenePath>().First(p => p.Role == "activity").Fill);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), scene.Items.OfType<ScenePath>().First(p => p.Role == "arrow").Stroke!.Color);
    }

    /// <summary>
    ///     Validates that every fixture parses, lays out and draws.
    /// </summary>
    [Fact]
    public void PlantUmlActivity_Fixtures_AllLayOutAndDraw()
    {
        // Arrange
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity"), "*.puml");

        // Act
        var documents = files.Select(f => PlantUmlDocument.Parse(File.ReadAllText(f))).ToList();
        var scenes = documents.Select(d => d.LayoutScene(cancellationToken: TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.True(documents.Count >= 15);
        Assert.All(documents, d => Assert.IsType<ActivityModel>(d.Diagram));
        Assert.All(scenes, s => Assert.True(s.ViewBox.Width > 20 && s.ViewBox.Height > 20));
    }
}
