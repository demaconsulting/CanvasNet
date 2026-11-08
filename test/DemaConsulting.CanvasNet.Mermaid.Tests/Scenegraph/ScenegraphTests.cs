using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Scenegraph;

/// <summary>
///     Subsystem tests of the Scenegraph subsystem: a scene of a node with its label, painted at two scales.
/// </summary>
public class ScenegraphTests
{
    /// <summary>
    ///     Builds a scene of one node: a rectangle with a centered label inside a translated group, as the layout produces it.
    /// </summary>
    /// <returns>The scene.</returns>
    private static Scene NodeScene()
    {
        var font = new SceneFont("\"trebuchet ms\", verdana, arial, sans-serif", 16, false, false);
        IReadOnlyList<SceneSegment> box =
        [
            SceneSegment.Move(-40, -20), SceneSegment.Line(40, -20), SceneSegment.Line(40, 20), SceneSegment.Line(-40, 20), SceneSegment.Close(),
        ];
        var node = new SceneGroup(
            48,
            28,
            [
                new ScenePath(box) { Role = "node", Id = "flowchart-A-0", Fill = new Rgba32(0xEC, 0xEC, 0xFF, 255), Stroke = new SceneStroke(new Rgba32(0x93, 0x70, 0xDB, 255), 1) },
                new SceneText(0, 6, SceneTextAnchor.Middle, [new SceneTextRun("Start", font, new Rgba32(0x33, 0x33, 0x33, 255))], new SceneRect(-18, -12, 36, 24)) { Role = "label" },
            ])
        { Role = "node" };
        return new Scene(new SceneRect(0, 0, 96, 56), [node]);
    }

    /// <summary>
    ///     Validates that the bounds of the scene come from the translated node and that the painting is deterministic and
    ///     scales: the node center is filled at both scales and the label leaves ink at its center.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled()
    {
        // Arrange
        var scene = NodeScene();
        var painter = new ScenePainter(ScenePainter.CreateFontResolver());

        // Act
        var bounds = Scene.BoundsOf(scene.Items);
        using var once = painter.Paint(scene, 96, 56, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken);
        using var twice = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 96, 56, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken);
        using var large = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 192, 112, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new SceneRect(8, 8, 80, 40), bounds);
        Assert.True(MermaidTestHelpers.PixelsEqual(once, twice));
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), once[12, 30]);
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), large[24, 60]);
        Assert.Contains(Enumerable.Range(30, 36), x => once[x, 30] != new Rgba32(0xEC, 0xEC, 0xFF, 255));
    }
}
