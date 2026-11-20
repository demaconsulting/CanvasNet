using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

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

    /// <summary>
    ///     Validates the page texts: the header right aligned at the top, the title centered below it, the body moved
    ///     below them and centered, and the legend, caption and footer below the body, as PlantUML places them.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_ScenePage_PlacesPageTexts()
    {
        // Arrange
        var body = new Scene(new SceneRect(0, 0, 78.167, 133.57), [new ScenePath(SceneShapes.Rect(10, 10, 20, 20)) { Role = "body" }]);
        var page = new PageTexts
        {
            Header = new PageText("head", TextAlign.Right, default),
            Title = new PageText("My Title", TextAlign.Center, default),
            Legend = new PageText("leg", TextAlign.Center, default),
            Caption = new PageText("cap", TextAlign.Center, default),
            Footer = new PageText("foot", TextAlign.Center, default),
        };
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var scene = ScenePage.Decorate(body, page, SkinTheme.Default, text, null);

        // Assert
        SceneText Text(string role) => scene.Items.OfType<SceneText>().Single(t => t.Role == role);
        Assert.Equal(83.117, scene.ViewBox.Width, 2);
        Assert.Equal(273, Math.Ceiling(scene.ViewBox.Height));
        Assert.Equal(54.871, Text("header").X, 2);
        Assert.Equal(15.381, Text("header").Baseline, 2);
        Assert.Equal(15, Text("title").X, 2);
        Assert.Equal(43.111, Text("title").Baseline, 2);
        Assert.Equal(212.291, Text("legend").Baseline, 2);
        Assert.Equal(248.9, Text("caption").Baseline, 2);
        Assert.Equal(264.357, Text("footer").Baseline, 2);
        var moved = Scene.BoundsOf(scene.Items.Where(i => i.Role == "body"))!.Value;
        Assert.Equal((12.475, 62.188), (Math.Round(moved.X, 3), Math.Round(moved.Y, 3)));
    }

    /// <summary>
    ///     Validates the spacing of an activity diagram's page: 5 pixels more above the header and the title, beside
    ///     the header and the footer, and below the page, and the body counted 5 pixels shorter.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_ScenePage_ActivitySpacing()
    {
        // Arrange
        var body = new Scene(new SceneRect(0, 0, 78.167, 133.57), [new ScenePath(SceneShapes.Rect(10, 10, 20, 20)) { Role = "body" }]);
        var page = new PageTexts
        {
            Header = new PageText("head", TextAlign.Left, default),
            Title = new PageText("My Title", TextAlign.Center, default),
            Legend = new PageText("leg", TextAlign.Center, default),
        };
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var sequence = ScenePage.Decorate(body, page, SkinTheme.Default, text, null);
        var activity = ScenePage.Decorate(body, page, SkinTheme.Default, text, null, PageMetrics.Activity);

        // Assert
        SceneText Text(Scene scene, string role) => scene.Items.OfType<SceneText>().Single(t => t.Role == role);
        Assert.Equal(5, Text(activity, "header").X - Text(sequence, "header").X, 3);
        Assert.Equal(5, Text(activity, "header").Baseline - Text(sequence, "header").Baseline, 3);
        Assert.Equal(5, Text(activity, "title").Baseline - Text(sequence, "title").Baseline, 3);
        Assert.Equal(0, activity.ViewBox.Height - sequence.ViewBox.Height, 3);
        Assert.Equal(PageMetrics.Sequence, new PageMetrics(0, 0, 46));
    }

    /// <summary>
    ///     Validates the spacing of the page of a graph diagram (class and object diagrams), measured from the
    ///     pictures PlantUML draws: the header at the top, the title 10 below it, the centered texts centered 6 left of
    ///     the middle, a legend on the left 12 from the edge and 6 below the body, and the narrower page margins.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_ScenePage_SvekSpacing()
    {
        // Arrange
        var body = new Scene(new SceneRect(0, 0, 265, 76), [new ScenePath(SceneShapes.Rect(6, 12, 244, 48)) { Role = "body" }]);
        var page = new PageTexts
        {
            Header = new PageText("Head", TextAlign.Right, default),
            Title = new PageText("T", TextAlign.Center, default),
            Legend = new PageText("short", TextAlign.Left, default),
            Caption = new PageText("Cap", TextAlign.Center, default),
            Footer = new PageText("Foot", TextAlign.Center, default),
        };
        var text = new TextLayout(TextLayout.CreateFontResolver());

        // Act
        var scene = ScenePage.Decorate(body, page, SkinTheme.Default, text, null, PageMetrics.Svek);

        // Assert
        SceneText Text(string role) => scene.Items.OfType<SceneText>().Single(t => t.Role == role);
        Assert.Equal(10.381, Text("header").Baseline, 2);
        Assert.Equal(259, Text("header").X + Text("header").Box.Width, 2);
        Assert.Equal(38.111, Text("title").Baseline, 2);
        Assert.Equal(129.5, Text("title").X + (Text("title").Box.Width / 2), 2);
        Assert.Equal(129.5, Text("caption").X + (Text("caption").Box.Width / 2), 2);
        Assert.Equal(129.5, Text("footer").X + (Text("footer").Box.Width / 2), 2);
        var legend = Scene.BoundsOf(scene.Items.Where(i => i.Role == "legend" && i is ScenePath))!.Value;
        Assert.Equal((12.0, 134.188), (Math.Round(legend.X, 3), Math.Round(legend.Y, 3)));
        var moved = Scene.BoundsOf(scene.Items.Where(i => i.Role == "body"))!.Value;
        Assert.Equal(64.188, moved.Y, 3);
    }

    /// <summary>
    ///     Validates that a body without page texts is returned unchanged, and that translation moves paths and texts
    ///     and leaves other items alone.
    /// </summary>
    [Fact]
    public void PlantUmlSceneModel_ScenePage_NoTextsAndTranslate()
    {
        // Arrange
        var body = new Scene(new SceneRect(0, 0, 50, 50), []);
        var font = new SceneFont("SansSerif", 14, false, false);
        var line = new SceneText(1, 2, [new SceneTextRun("x", font, SkinColors.Black, 8)], new SceneRect(1, -10, 8, 17)) { Role = "t" };
        var path = new ScenePath(SceneShapes.Line(0, 0, 10, 0)) { Role = "p" };

        // Act
        var same = ScenePage.Decorate(body, PageTexts.None, SkinTheme.Default, new TextLayout(TextLayout.CreateFontResolver()), null);
        var movedText = Assert.IsType<SceneText>(ScenePage.Translate(line, 5, 7));
        var movedPath = Assert.IsType<ScenePath>(ScenePage.Translate(path, 5, 7));

        // Assert
        Assert.Same(body, same);
        Assert.Equal((6, 9, 6, -3), (movedText.X, movedText.Baseline, movedText.Box.X, movedText.Box.Y));
        Assert.Equal(new SceneRect(5, 7, 10, 0), Scene.BoundsOf([movedPath]));
        Assert.Same(path, ScenePage.Translate(path, 0, 0));
    }
}
