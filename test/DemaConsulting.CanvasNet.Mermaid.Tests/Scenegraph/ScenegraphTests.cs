using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Scenegraph;

/// <summary>
///     Subsystem tests of the Scenegraph subsystem: scenes of a node with its label and of a picture, painted by the
///     painter with its placement, picture loader, painted-pixel limit and cancellation.
/// </summary>
public class ScenegraphTests
{
    /// <summary>The location of the node of the scene in its diagram.</summary>
    private static readonly MermaidLocation NodeLocation = new("n.mmd", 2, 3);

    /// <summary>The red of the pictures.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

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
                new ScenePath(box) { Role = "node", Id = "flowchart-A-0", Location = NodeLocation, Fill = new Rgba32(0xEC, 0xEC, 0xFF, 255), Stroke = new SceneStroke(new Rgba32(0x93, 0x70, 0xDB, 255), 1) },
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

    /// <summary>
    ///     Validates that an underlined run of a text line is painted with a bar under its baseline across its own advance
    ///     only, at the scale of the placement, while the plain run beside it gets none.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintUnderlinedRun_BarsOnlyThatRun()
    {
        // Arrange: two runs of capitals without descenders on a baseline at y = 20, 32 pixels high, the first underlined
        var font = new SceneFont("\"trebuchet ms\", verdana, arial, sans-serif", 32, false, false);
        var black = new Rgba32(0, 0, 0, 255);
        var line = new SceneText(
            10,
            20,
            SceneTextAnchor.Start,
            [new SceneTextRun("HHH", font, black) { Underline = true }, new SceneTextRun("HHH", font, black)],
            new SceneRect(10, 0, 140, 30))
        { Role = "label" };
        var scene = new Scene(new SceneRect(0, 0, 160, 30), [line]);

        // Act
        using var surface = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 320, 60, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);
        var underFirst = Enumerable.Range(20, 140).Count(x => surface[x, 44].A == 255);
        var underSecond = Enumerable.Range(164, 130).Count(x => surface[x, 44].A > 0);

        // Assert: the bar spans the first run's advance of about 69 units, twice that in device pixels
        Assert.InRange(underFirst, 130, 140);
        Assert.Equal(0, underSecond);
    }

    /// <summary>
    ///     Validates that the drop shadow of a path is painted under it, moved by its offset at the scale of the
    ///     placement, and that it does not change the bounds of the scene.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintBoxScene_ShadowIsUnderTheBoxAndOutsideTheBounds()
    {
        // Arrange: a white 20 by 20 box in a group at (10, 10), with a sharp black shadow 4 right and 4 down
        var white = new Rgba32(255, 255, 255, 255);
        var black = new Rgba32(0, 0, 0, 255);
        IReadOnlyList<SceneSegment> box =
        [
            SceneSegment.Move(0, 0), SceneSegment.Line(20, 0), SceneSegment.Line(20, 20), SceneSegment.Line(0, 20), SceneSegment.Close(),
        ];
        var group = new SceneGroup(10, 10, [new ScenePath(box) { Role = "box", Fill = white, Shadow = new SceneShadow(4, 4, 0, black) }]) { Role = "box" };
        var scene = new Scene(new SceneRect(0, 0, 40, 40), [group]);

        // Act
        var bounds = Scene.BoundsOf(scene.Items);
        using var surface = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 80, 80, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);

        // Assert: the box covers 20 to 60 device pixels and its shadow 28 to 68, under the box
        Assert.Equal(new SceneRect(10, 10, 20, 20), bounds);
        Assert.Equal(white, surface[40, 40]);
        Assert.Equal(black, surface[64, 40]);
        Assert.Equal(black, surface[40, 64]);
        Assert.Equal(0, surface[72, 40].A);
        Assert.Equal(0, surface[24, 64].A);
    }

    /// <summary>
    ///     Validates that the picture of a scene comes only from the loader the painter is given: it is loaded at the
    ///     device size of its box and fills the box, and without a loader nothing is drawn.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintPictureScene_PicturesComeOnlyFromTheLoader()
    {
        // Arrange: a 10 by 6 picture centered in a group at (10, 5) of a 20 by 10 view box
        var picture = new SceneGroup(10, 5, [new SceneImage(new SceneRect(-5, -3, 10, 6), "pic.png") { Role = "node-image" }]) { Role = "node" };
        var scene = new Scene(new SceneRect(0, 0, 20, 10), [picture]);
        var loader = new RecordingLoader();

        // Act
        using var loaded = new ScenePainter(ScenePainter.CreateFontResolver(), loader).Paint(scene, 40, 20, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);
        using var withoutLoader = new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 40, 20, ScenePlacement.Uniform(scene.ViewBox, 2), null, TestContext.Current.CancellationToken);

        // Assert: the box covers device pixels 10 to 29 across and 4 to 15 down
        Assert.Equal([("pic.png", 20, 12)], loader.Requests);
        Assert.Equal(Red, loaded[10, 4]);
        Assert.Equal(Red, loaded[29, 15]);
        Assert.Equal(0, loaded[9, 10].A);
        Assert.Equal(0, loaded[30, 10].A);
        Assert.True(MermaidTestHelpers.IsFullyTransparent(withoutLoader));
    }

    /// <summary>
    ///     Validates that painting the node scene with a painted-pixel limit smaller than the node fails before the node
    ///     is painted, located at the node, and that a limit large enough paints it.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintNodeScene_OverPaintedPixelLimit_FailsAtTheNode()
    {
        // Arrange: the node fills 80 by 40 device pixels at scale 1
        var scene = NodeScene();

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() =>
            new ScenePainter(ScenePainter.CreateFontResolver(), null, 1_000).Paint(scene, 96, 56, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken));
        using var fits = new ScenePainter(ScenePainter.CreateFontResolver(), null, 100_000).Paint(scene, 96, 56, ScenePlacement.Uniform(scene.ViewBox, 1), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, NodeLocation), (ex.Code, ex.Location));
        Assert.Contains("MermaidLimits.MaxPaintedPixels", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), fits[12, 30]);
    }

    /// <summary>
    ///     Validates that painting the node scene with a canceled token stops with an operation-canceled error.
    /// </summary>
    [Fact]
    public void Scenegraph_PaintNodeScene_CanceledToken_Throws()
    {
        // Arrange
        var scene = NodeScene();
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new ScenePainter(ScenePainter.CreateFontResolver()).Paint(scene, 96, 56, ScenePlacement.Uniform(scene.ViewBox, 1), null, source.Token));
    }

    /// <summary>
    ///     A picture loader that gives a red picture of the requested size and records the requests.
    /// </summary>
    private sealed class RecordingLoader : ISceneImageLoader
    {
        /// <summary>Gets the requests made.</summary>
        public List<(string Href, int Width, int Height)> Requests { get; } = [];

        /// <inheritdoc />
        public Surface? Load(string href, int pixelWidth, int pixelHeight)
        {
            Requests.Add((href, pixelWidth, pixelHeight));
            var surface = new Surface(pixelWidth, pixelHeight);
            surface.Clear(Red);
            return surface;
        }
    }
}
