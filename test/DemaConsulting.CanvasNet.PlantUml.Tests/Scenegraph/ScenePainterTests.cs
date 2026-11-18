using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Scenegraph;

/// <summary>
///     Unit tests for the <c>ScenePainter</c> unit: fills, strokes, dashes, text, the background, the bounds checks, the
///     painted-pixel budget and cancellation.
/// </summary>
public class ScenePainterTests
{
    /// <summary>A red paint.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Creates a painter over the bundled fonts.
    /// </summary>
    /// <param name="maxPixels">The painted-pixel budget.</param>
    /// <returns>The painter.</returns>
    private static ScenePainter CreatePainter(long maxPixels = long.MaxValue) => new(new TextLayout(TextLayout.CreateFontResolver()), maxPixels);

    /// <summary>
    ///     Creates a scene of items on a 40 by 40 view box.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(params SceneItem[] items) => new(new SceneRect(0, 0, 40, 40), items);

    /// <summary>
    ///     Validates that a filled and stroked rectangle paints its fill inside and its stroke on its edge, over the
    ///     background.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_FillStrokeAndBackground()
    {
        // Arrange
        var scene = SceneOf(new ScenePath(SceneShapes.Rect(10, 10, 20, 20)) { Role = "box", Fill = Red, Stroke = new SceneStroke(SkinColors.Black, 2) });

        // Act
        using var surface = CreatePainter().Paint(scene, 40, 40, ScenePlacement.Uniform(scene.ViewBox, 1), SkinColors.GreyBlue, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Red, surface[20, 20]);
        Assert.Equal(SkinColors.Black, surface[10, 20]);
        Assert.Equal(SkinColors.GreyBlue, surface[2, 2]);
    }

    /// <summary>
    ///     Validates that a dashed line leaves gaps and that a scale enlarges the drawing.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_DashedLineAndScale()
    {
        // Arrange
        var stroke = new SceneStroke(SkinColors.Black, 2) { Dash = [5, 5] };
        var scene = SceneOf(new ScenePath(SceneShapes.Line(0, 10, 40, 10)) { Role = "line", Stroke = stroke });

        // Act
        using var surface = CreatePainter().Paint(scene, 80, 80, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);

        // Assert: dashes of 10 device pixels from x 0, gaps of 10 from x 10
        Assert.Equal(255, surface[5, 20].A);
        Assert.Equal(0, surface[15, 20].A);
        Assert.Equal(255, surface[25, 20].A);
    }

    /// <summary>
    ///     Validates that text paints ink inside its box and nothing outside it, and that a struck run gets a bar.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_TextInsideItsBox()
    {
        // Arrange
        var layout = new TextLayout(TextLayout.CreateFontResolver());
        var block = layout.Layout("--Hi--", new SceneFont("SansSerif", 20, false, false), SkinColors.Black);
        var scene = new Scene(new SceneRect(0, 0, 60, 40), [.. block.Draw(5, 5, TextAlign.Left, "label", default)]);

        // Act
        using var surface = CreatePainter().Paint(scene, 60, 40, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken);

        // Assert
        var ink = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    ink++;
                    Assert.InRange(x, 4, 6 + (int)block.Width);
                }
            }
        }

        Assert.True(ink > 30);
        Assert.True(block.Lines[0].Runs[0].Strike);
    }

    /// <summary>
    ///     Validates that an item with a coordinate that is not finite is skipped, not painted.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_NonFiniteItem_IsSkipped()
    {
        // Arrange
        var scene = SceneOf(new ScenePath(SceneShapes.Rect(double.NaN, 0, 10, 10)) { Role = "box", Fill = Red });

        // Act
        using var surface = CreatePainter().Paint(scene, 40, 40, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Validates that the item that would pass the painted-pixel budget fails the call with a located limit error.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_OverPixelBudget_ThrowsLocated()
    {
        // Arrange
        var where = new PlantUmlLocation("x.puml", 4, 1);
        var scene = SceneOf(new ScenePath(SceneShapes.Rect(0, 0, 40, 40)) { Role = "box", Fill = Red, Location = where });

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => CreatePainter(100).Paint(scene, 40, 40, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.LimitExceeded, where), (ex.Code, ex.Location));
        Assert.Contains("MaxPaintedPixels", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops the painting.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Paint_Canceled_Throws()
    {
        // Arrange
        var scene = SceneOf(new ScenePath(SceneShapes.Rect(0, 0, 10, 10)) { Role = "box", Fill = Red });
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => CreatePainter().Paint(scene, 40, 40, ScenePlacement.Uniform(scene.ViewBox, 1), null, source.Token));
    }

    /// <summary>
    ///     Validates the fitted placement: uniform scale and centering.
    /// </summary>
    [Fact]
    public void PlantUmlScenePainter_Fit_ScalesUniformlyAndCenters()
    {
        // Arrange / Act
        var placement = ScenePlacement.Fit(new SceneRect(0, 0, 20, 10), 100, 100);

        // Assert
        Assert.Equal(new ScenePlacement(5, 0, 25), placement);
        Assert.Equal(new ScenePlacement(1, -2, -3), ScenePlacement.Fit(new SceneRect(2, 3, 0, 10), 10, 10));
    }
}
