using DemaConsulting.CanvasNet.PlantUml.Activity;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Unit tests for the <c>ActivityRenderer</c> unit: the conversion of placed shapes into scene items.
/// </summary>
public class ActivityRendererTests
{
    /// <summary>
    ///     Validates that a rectangle becomes a path with its corners, paint, role and location.
    /// </summary>
    [Fact]
    public void PlantUmlActivityRenderer_Rect_BecomesARoundedPath()
    {
        // Arrange
        var shape = new ActivityShape(ActivityShapeKind.Rect, "activity")
        {
            X = 10,
            Y = 20,
            Width = 50,
            Height = 30,
            Radius = 12.5,
            Fill = SkinColors.White,
            Stroke = new SceneStroke(SkinColors.White, 0.5),
            Location = new PlantUmlLocation(null, 3, 1),
        };

        // Act
        var item = Assert.IsType<ScenePath>(ActivityRenderer.ToItem(shape));

        // Assert
        Assert.Equal(new SceneRect(10, 20, 50, 30), Scene.BoundsOf([item]));
        Assert.Equal(("activity", 3), (item.Role, item.Location.Line));
        Assert.Equal(SkinColors.White, item.Fill);
        Assert.Contains(item.Segments, s => s.Kind == SceneSegmentKind.CubicTo);
    }

    /// <summary>
    ///     Validates that ellipses, lines and polygons become paths at their places.
    /// </summary>
    [Fact]
    public void PlantUmlActivityRenderer_EllipseLineAndPolygon_BecomePaths()
    {
        // Arrange
        var ellipse = new ActivityShape(ActivityShapeKind.Ellipse, "start") { X = 0, Y = 0, Width = 20, Height = 20, Fill = SkinColors.White };
        var line = new ActivityShape(ActivityShapeKind.Line, "arrow") { X = 5, Y = 5, X2 = 5, Y2 = 25, Stroke = new SceneStroke(SkinColors.White, 1) };
        var polygon = new ActivityShape(ActivityShapeKind.Polygon, "diamond")
        {
            X = 100,
            Y = 50,
            Segments = [SceneSegment.Move(0, 12), SceneSegment.Line(12, 0), SceneSegment.Line(24, 12), SceneSegment.Line(12, 24), SceneSegment.Close()],
            Fill = SkinColors.White,
        };

        // Act
        var items = new[] { ellipse, line, polygon }.Select(s => Assert.IsType<ScenePath>(ActivityRenderer.ToItem(s))).ToList();

        // Assert
        Assert.Equal(new SceneRect(0, 0, 20, 20), Scene.BoundsOf([items[0]]));
        Assert.Equal(new SceneRect(5, 5, 0, 20), Scene.BoundsOf([items[1]]));
        Assert.Equal(new SceneRect(100, 50, 24, 24), Scene.BoundsOf([items[2]]));
    }

    /// <summary>
    ///     Validates that space without paint (empty space, an unpainted background) draws nothing.
    /// </summary>
    [Fact]
    public void PlantUmlActivityRenderer_UnpaintedShapes_DrawNothing()
    {
        // Arrange
        var empty = new ActivityShape(ActivityShapeKind.Empty, "empty") { Width = 10, Height = 10 };
        var back = new ActivityShape(ActivityShapeKind.Rect, "lane-titles") { Width = 10, Height = 10 };

        // Act
        var items = new[] { ActivityRenderer.ToItem(empty), ActivityRenderer.ToItem(back) };

        // Assert
        Assert.All(items, Assert.Null);
    }

    /// <summary>
    ///     Validates that a whole diagram becomes a scene of its size with the theme's background, actions, texts and
    ///     arrows.
    /// </summary>
    [Fact]
    public void PlantUmlActivityRenderer_Render_BuildsTheScene()
    {
        // Arrange
        var (model, geometry) = ActivityTestHelpers.Layout("start\n:Open the file;\n-> next;\n:Read it;\nstop");

        // Act
        var scene = ActivityRenderer.Render(model, geometry);

        // Assert
        Assert.Equal(new SceneRect(0, 0, geometry.Width, geometry.Height), scene.ViewBox);
        Assert.Equal(model.Theme.Background, scene.Background);
        Assert.Equal(2, ActivityTestHelpers.Bounds(scene, "activity").Count);
        Assert.Equal(["Open the file", "Read it"], ActivityTestHelpers.Texts(scene, "activity-text").Select(t => t.Text));
        Assert.Equal("next", Assert.Single(ActivityTestHelpers.Texts(scene, "arrow-text")).Text);
        Assert.Equal(3, ActivityTestHelpers.Bounds(scene, "arrow-head").Count);
    }
}
