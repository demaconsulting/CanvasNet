using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Scenegraph;

/// <summary>
///     Unit tests for the <c>SceneModel</c> unit: the shape builders, the bounds of a scene, and the device path.
/// </summary>
public class SceneModelTests
{
    /// <summary>
    ///     Validates the bounds of the shape builders: rectangles with and without corners, ellipses, polygons and lines.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_Shapes_HaveTheirBounds()
    {
        // Arrange
        ScenePath Path(IReadOnlyList<SceneSegment> segments) => new(segments) { Role = "shape" };

        // Act
        var rect = Scene.BoundsOf([Path(SceneShapes.Rect(10, 20, 30, 40))]);
        var rounded = Scene.BoundsOf([Path(SceneShapes.Rect(10, 20, 30, 40, 5))]);
        var ellipse = Scene.BoundsOf([Path(SceneShapes.Ellipse(50, 50, 8, 4))]);
        var polygon = Scene.BoundsOf([Path(SceneShapes.Polygon((0, 0), (10, 4), (0, 8)))]);
        var line = Scene.BoundsOf([Path(SceneShapes.Line(5, 7, 25, 7))]);

        // Assert
        Assert.Equal(new SceneRect(10, 20, 30, 40), rect);
        Assert.Equal(new SceneRect(10, 20, 30, 40), rounded);
        Assert.Equal(new SceneRect(42, 46, 16, 8), ellipse);
        Assert.Equal(new SceneRect(0, 0, 10, 8), polygon);
        Assert.Equal(new SceneRect(5, 7, 20, 0), line);
        Assert.Equal(10, SceneShapes.Rect(0, 0, 10, 10, 2).Count);
        Assert.Equal(SceneSegmentKind.Close, SceneShapes.Polygon((0, 0), (1, 1))[^1].Kind);
    }

    /// <summary>
    ///     Validates that the bounds of a scene combine paths and text boxes, and that nothing has no bounds.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_BoundsOf_CombinesPathsAndText()
    {
        // Arrange
        var font = new SceneFont("SansSerif", 14, false, false);
        var items = new SceneItem[]
        {
            new ScenePath(SceneShapes.Rect(0, 0, 10, 10)) { Role = "box" },
            new SceneText(20, 30, [new SceneTextRun("x", font, SkinColors.Black, 8)], new SceneRect(20, 15, 8, 17)) { Role = "label" },
        };

        // Act
        var bounds = Scene.BoundsOf(items);

        // Assert
        Assert.Equal(new SceneRect(0, 0, 28, 32), bounds);
        Assert.Null(Scene.BoundsOf([]));
        Assert.Equal("x", ((SceneText)items[1]).Text);
    }

    /// <summary>
    ///     Validates the device path: scaled and translated, and a path without a move starts at the origin.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_ToPath_ScalesAndTranslates()
    {
        // Arrange
        var segments = SceneShapes.Rect(1, 2, 3, 4);

        // Act
        var bounds = Scene.ToPath(segments, 2, 10, 20).GetBounds();
        var implicitStart = Scene.ToPath([SceneSegment.Line(4, 4)], 1, 0, 0).GetBounds();

        // Assert
        Assert.Equal((12f, 24f, 6f, 8f), (bounds.X, bounds.Y, bounds.Width, bounds.Height));
        Assert.Equal((0f, 0f, 4f, 4f), (implicitStart.X, implicitStart.Y, implicitStart.Width, implicitStart.Height));
    }
}
