using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Scenegraph;

/// <summary>
///     Subsystem tests for the <c>Scenegraph</c> subsystem: text laid out, placed in a scene with shapes, and painted.
/// </summary>
public class ScenegraphTests
{
    /// <summary>
    ///     Validates that a laid-out label inside a box is painted inside the box, with the box's fill around it, at a
    ///     scale.
    /// </summary>
    [Fact]
    public void PlantUmlScenegraph_LayoutAndPaint_LabelInsideBox()
    {
        // Arrange
        var layout = new TextLayout(TextLayout.CreateFontResolver());
        var block = layout.Layout("Alice", new SceneFont("SansSerif", 14, false, false), SkinColors.Black);
        var box = SceneShapes.Rect(10, 10, block.Width + 14, block.Height + 14, 2.5);
        var items = new List<SceneItem> { new ScenePath(box) { Role = "participant", Fill = SkinColors.GreyBlue } };
        items.AddRange(block.Draw(17, 17, TextAlign.Left, "label", default));
        var bounds = Scene.BoundsOf(items)!.Value;
        var scene = new Scene(new SceneRect(0, 0, bounds.Right + 10, bounds.Bottom + 10), items);

        // Act
        using var surface = new ScenePainter(layout).Paint(scene, 128, 104, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);

        // Assert: the box is 44.345 by 31.609 as PlantUML draws it, and its center holds dark ink of the text
        Assert.Equal(44.345, bounds.Width, 0.01);
        Assert.Equal(31.609, bounds.Height, 0.01);
        Assert.Equal(SkinColors.GreyBlue, surface[24, 24]);
        var dark = 0;
        for (var x = 34; x < 94; x++)
        {
            for (var y = 34; y < 66; y++)
            {
                dark += surface[x, y].R < 100 ? 1 : 0;
            }
        }

        Assert.True(dark > 50);
    }
}
