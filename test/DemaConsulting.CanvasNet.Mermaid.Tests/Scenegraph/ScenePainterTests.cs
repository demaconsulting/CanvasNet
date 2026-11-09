using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Scenegraph;

/// <summary>
///     Unit tests for <see cref="ScenePainter"/>: fills, strokes and dashes, text, pictures, placement, the painted-pixel
///     limit and cancellation.
/// </summary>
public class ScenePainterTests
{
    /// <summary>Mermaid's default node fill.</summary>
    private static readonly Rgba32 NodeFill = new(0xEC, 0xEC, 0xFF, 255);

    /// <summary>Mermaid's default node stroke.</summary>
    private static readonly Rgba32 NodeStroke = new(0x93, 0x70, 0xDB, 255);

    /// <summary>
    ///     Creates a rectangle path item.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The segments.</returns>
    private static IReadOnlyList<SceneSegment> Rectangle(double x, double y, double width, double height) =>
    [
        SceneSegment.Move(x, y),
        SceneSegment.Line(x + width, y),
        SceneSegment.Line(x + width, y + height),
        SceneSegment.Line(x, y + height),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Paints a scene at scale 1 on a surface of its view box.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="maxPixels">The painted-pixel limit.</param>
    /// <param name="background">The background, or null.</param>
    /// <param name="images">The picture loader, or null.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(Scene scene, long maxPixels = long.MaxValue, Rgba32? background = null, ISceneImageLoader? images = null) =>
        new ScenePainter(ScenePainter.CreateFontResolver(), images, maxPixels).Paint(
            scene,
            (int)scene.ViewBox.Width,
            (int)scene.ViewBox.Height,
            ScenePlacement.Uniform(scene.ViewBox, 1),
            background,
            TestContext.Current.CancellationToken);

    /// <summary>
    ///     Validates that a filled and stroked rectangle has the fill inside, the stroke on its edge and transparency
    ///     outside, and that the view box origin maps to the surface origin.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_FilledStrokedRectangle_PaintsFillAndStroke()
    {
        // Arrange: a view box that starts at (-10, -10)
        var rect = new ScenePath(Rectangle(0, 0, 40, 30)) { Role = "node", Fill = NodeFill, Stroke = new SceneStroke(NodeStroke, 2) };
        var scene = new Scene(new SceneRect(-10, -10, 60, 50), [rect]);

        // Act
        using var surface = Paint(scene);

        // Assert
        Assert.Equal(NodeFill, surface[30, 25]);
        Assert.Equal(NodeStroke, surface[10, 25]);
        Assert.Equal(0, surface[2, 2].A);
    }

    /// <summary>
    ///     Validates that a dashed stroke leaves gaps and a solid one does not.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_DashedStroke_LeavesGaps()
    {
        // Arrange: a horizontal line with 9 on, 5 off (Mermaid's animated edge frame)
        IReadOnlyList<SceneSegment> line = [SceneSegment.Move(0, 5), SceneSegment.Line(100, 5)];
        var dashed = new ScenePath(line) { Role = "edge", Stroke = new SceneStroke(NodeStroke, 2) { Dash = [9, 5] } };
        var scene = new Scene(new SceneRect(0, 0, 100, 10), [dashed]);

        // Act
        using var surface = Paint(scene);

        // Assert: x = 4 is in the first dash, x = 11 in the first gap
        Assert.Equal(255, surface[4, 5].A);
        Assert.Equal(0, surface[11, 5].A);
    }

    /// <summary>
    ///     Validates that an invalid or too fine dash pattern is drawn solid.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_InvalidDash_IsSolid()
    {
        // Arrange
        IReadOnlyList<SceneSegment> line = [SceneSegment.Move(0, 5), SceneSegment.Line(100, 5)];
        var negative = new ScenePath(line) { Role = "edge", Stroke = new SceneStroke(NodeStroke, 2) { Dash = [9, -5] } };
        var fine = new ScenePath(line) { Role = "edge", Stroke = new SceneStroke(NodeStroke, 2) { Dash = [0.01, 0.01] } };

        // Act
        using var first = Paint(new Scene(new SceneRect(0, 0, 100, 10), [negative]));
        using var second = Paint(new Scene(new SceneRect(0, 0, 100, 10), [fine]));

        // Assert
        Assert.Equal(255, first[11, 5].A);
        Assert.Equal(255, second[11, 5].A);
    }

    /// <summary>
    ///     Validates that a dash pattern with an entry longer than the largest drawn magnitude (whose single-precision
    ///     value could be infinite) is drawn solid, and that a huge dash offset is reduced by whole periods.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_HugeDashOrOffset_DrawsWithoutFailing()
    {
        // Arrange: entries beyond float range, and a 9-on 5-off pattern 1e300 periods-ish along
        IReadOnlyList<SceneSegment> line = [SceneSegment.Move(0, 5), SceneSegment.Line(100, 5)];
        var huge = new ScenePath(line) { Role = "edge", Stroke = new SceneStroke(NodeStroke, 2) { Dash = [1e39, 1] } };
        var offset = new ScenePath(line) { Role = "edge", Stroke = new SceneStroke(NodeStroke, 2) { Dash = [9, 5], DashOffset = 1e300 } };

        // Act
        using var first = Paint(new Scene(new SceneRect(0, 0, 100, 10), [huge]));
        using var second = Paint(new Scene(new SceneRect(0, 0, 100, 10), [offset]));

        // Assert: the huge pattern is solid (no gap at x = 11); the offset pattern still has dashes and gaps
        Assert.Equal(255, first[11, 5].A);
        var painted = Enumerable.Range(0, 100).Count(x => second[x, 5].A == 255);
        Assert.InRange(painted, 40, 90);
    }

    /// <summary>
    ///     Validates that text is drawn in the bundled font, anchored at its middle.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_MiddleAnchoredText_IsCentered()
    {
        // Arrange
        var font = new SceneFont("\"trebuchet ms\", verdana, arial, sans-serif", 16, false, false);
        var text = new SceneText(50, 20, SceneTextAnchor.Middle, [new SceneTextRun("HHHH", font, new Rgba32(0x33, 0x33, 0x33, 255))], new SceneRect(30, 4, 40, 20)) { Role = "label" };
        var scene = new Scene(new SceneRect(0, 0, 100, 30), [text]);

        // Act
        using var surface = Paint(scene);
        var columns = Enumerable.Range(0, 100).Where(x => Enumerable.Range(0, 30).Any(y => surface[x, y].A > 0)).ToList();

        // Assert: the ink is centered on x = 50 within a pixel or two
        Assert.NotEmpty(columns);
        Assert.InRange((columns[0] + columns[^1]) / 2.0, 48, 52);
    }

    /// <summary>
    ///     Validates that runs are drawn one after the other with their own styles, and that an end anchor ends the line.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_EndAnchoredRuns_EndAtAnchor()
    {
        // Arrange
        var regular = new SceneFont("sans-serif", 16, false, false);
        var bold = regular with { Bold = true };
        var black = new Rgba32(0, 0, 0, 255);
        var text = new SceneText(90, 20, SceneTextAnchor.End, [new SceneTextRun("ab", regular, black), new SceneTextRun("CD", bold, black)], new SceneRect(50, 4, 40, 20)) { Role = "label" };

        // Act
        using var surface = Paint(new Scene(new SceneRect(0, 0, 100, 30), [text]));
        var last = Enumerable.Range(0, 100).Last(x => Enumerable.Range(0, 30).Any(y => surface[x, y].A > 0));

        // Assert
        Assert.InRange(last, 85, 90);
    }

    /// <summary>
    ///     Validates the background fill and that a scaled placement scales geometry.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ScaledWithBackground_ScalesGeometry()
    {
        // Arrange
        var white = new Rgba32(255, 255, 255, 255);
        var rect = new ScenePath(Rectangle(0, 0, 10, 10)) { Role = "node", Fill = NodeFill };
        var scene = new Scene(new SceneRect(0, 0, 20, 20), [rect]);

        // Act
        using var surface = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 40, 40, ScenePlacement.Uniform(scene.ViewBox, 2), white, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(NodeFill, surface[18, 18]);
        Assert.Equal(white, surface[22, 22]);
    }

    /// <summary>
    ///     Validates the fit placement: scaled to fit and centered.
    /// </summary>
    [Fact]
    public void ScenePlacement_Fit_ScalesAndCenters()
    {
        // Arrange / Act
        var placement = ScenePlacement.Fit(new SceneRect(10, 0, 20, 10), 100, 100);
        var empty = ScenePlacement.Fit(new SceneRect(3, 4, 0, 10), 100, 100);

        // Assert: scale 5, the 100 by 50 picture is centered vertically, and the view box origin moves to the left edge
        Assert.Equal(new ScenePlacement(5, -50, 25), placement);
        Assert.Equal(new ScenePlacement(1, -3, -4), empty);
    }

    /// <summary>
    ///     Validates the painted-pixel limit: the item that would pass it fails with its location, before painting.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_OverPaintedPixelLimit_ThrowsLimitExceeded()
    {
        // Arrange: two 10 by 10 fills against a limit of 150 pixels
        var where = new MermaidLocation("x.mmd", 3, 1);
        var first = new ScenePath(Rectangle(0, 0, 10, 10)) { Role = "node", Fill = NodeFill };
        var second = new ScenePath(Rectangle(10, 0, 10, 10)) { Role = "node", Fill = NodeFill, Location = where };
        var scene = new Scene(new SceneRect(0, 0, 20, 10), [first, second]);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => Paint(scene, maxPixels: 150));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, where), (ex.Code, ex.Location));
        Assert.Contains("MermaidLimits.MaxPaintedPixels", ex.Message, StringComparison.Ordinal);
        using var fits = Paint(scene, maxPixels: 200);
        Assert.Equal(NodeFill, fits[15, 5]);
    }

    /// <summary>
    ///     Validates that items far outside every surface or with values that are not finite are skipped, not drawn.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_UnboundedGeometry_IsSkipped()
    {
        // Arrange
        var far = new ScenePath(Rectangle(1e9, 0, 10, 10)) { Role = "node", Fill = NodeFill };
        var nan = new ScenePath(Rectangle(double.NaN, 0, 10, 10)) { Role = "node", Fill = NodeFill };
        var group = new SceneGroup(double.PositiveInfinity, 0, [new ScenePath(Rectangle(0, 0, 10, 10)) { Role = "node", Fill = NodeFill }]) { Role = "cluster" };

        // Act
        using var surface = Paint(new Scene(new SceneRect(0, 0, 20, 20), [far, nan, group]), maxPixels: 1);

        // Assert
        Assert.True(MermaidTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that a picture is loaded at its device size and composited with its opacity.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Image_LoadsAtDeviceSizeAndComposites()
    {
        // Arrange
        var loader = new SolidLoader(new Rgba32(255, 0, 0, 255));
        var image = new SceneImage(new SceneRect(2, 2, 10.5, 6), "red.png") { Role = "image", Opacity = 0.5 };
        var scene = new Scene(new SceneRect(0, 0, 20, 10), [image]);

        // Act
        using var surface = Paint(scene, images: loader);

        // Assert
        Assert.Equal(("red.png", 11, 6), loader.Requests.Single());
        Assert.Equal(255, surface[5, 5].R);
        Assert.InRange(surface[5, 5].A, 120, 135);
        Assert.Equal(0, surface[15, 5].A);
    }

    /// <summary>
    ///     Validates that a picture that comes back at another size than its box (a raster at its own size) is stretched to the
    ///     whole box, as Mermaid's <c>image</c> element has <c>preserveAspectRatio="none"</c>.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageAtItsOwnSize_IsStretchedToTheBox()
    {
        // Arrange: a loader that always gives a 2 by 1 picture, and a 12 by 8 box
        var loader = new SolidLoader(new Rgba32(0, 0, 255, 255), (2, 1));
        var image = new SceneImage(new SceneRect(4, 1, 12, 8), "blue.png") { Role = "image" };
        var scene = new Scene(new SceneRect(0, 0, 20, 10), [image]);

        // Act
        using var surface = Paint(scene, images: loader);

        // Assert: the box is covered from corner to corner, and nothing outside it
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[4, 1]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[15, 8]);
        Assert.Equal(0, surface[3, 5].A);
        Assert.Equal(0, surface[16, 5].A);
        Assert.Equal(0, surface[10, 9].A);
    }

    /// <summary>
    ///     Validates that a painter without a loader draws no picture.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_ImageWithoutLoader_DrawsNothing()
    {
        // Arrange
        var image = new SceneImage(new SceneRect(0, 0, 10, 10), "red.png") { Role = "image" };

        // Act
        using var surface = Paint(new Scene(new SceneRect(0, 0, 10, 10), [image]));

        // Assert
        Assert.True(MermaidTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that a canceled token stops the painting.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_Canceled_Throws()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = new Scene(new SceneRect(0, 0, 10, 10), [new ScenePath(Rectangle(0, 0, 5, 5)) { Role = "node", Fill = NodeFill }]);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() =>
            new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 10, 10, ScenePlacement.Uniform(scene.ViewBox, 1), null, source.Token));
    }

    /// <summary>
    ///     Validates that the even-odd rule leaves the hole of a ring.
    /// </summary>
    [Fact]
    public void ScenePainter_Paint_EvenOdd_LeavesHole()
    {
        // Arrange
        var ring = new ScenePath([.. Rectangle(0, 0, 20, 20), .. Rectangle(5, 5, 10, 10)]) { Role = "node", Fill = NodeFill, FillRule = FillRule.EvenOdd };

        // Act
        using var surface = Paint(new Scene(new SceneRect(0, 0, 20, 20), [ring]));

        // Assert
        Assert.Equal(NodeFill, surface[2, 2]);
        Assert.Equal(0, surface[10, 10].A);
    }

    /// <summary>
    ///     A picture loader that returns solid pictures and records the requests.
    /// </summary>
    /// <param name="color">The color of the pictures.</param>
    /// <param name="ownSize">The size every picture comes back at, or null for the requested size.</param>
    private sealed class SolidLoader(Rgba32 color, (int Width, int Height)? ownSize = null) : ISceneImageLoader
    {
        /// <summary>
        ///     Gets the requests made.
        /// </summary>
        public List<(string Href, int Width, int Height)> Requests { get; } = [];

        /// <inheritdoc />
        public Surface? Load(string href, int pixelWidth, int pixelHeight)
        {
            Requests.Add((href, pixelWidth, pixelHeight));
            var (width, height) = ownSize ?? (pixelWidth, pixelHeight);
            var surface = new Surface(width, height);
            surface.Clear(color);
            return surface;
        }
    }
}
