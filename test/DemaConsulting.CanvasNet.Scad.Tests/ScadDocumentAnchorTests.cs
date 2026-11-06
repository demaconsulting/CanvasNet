// cspell:ignore vpt vpr vpd
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;
using static DemaConsulting.CanvasNet.Scad.Tests.ScadRenderTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     System tests of the anchors through the public API: the handler of <see cref="ScadRenderOptions"/> is given the
///     name, the world point, the pixel and the inside flag of every declared anchor, and the pixel is checked against
///     the rendered image itself (the extent of the covered pixels), never against a second implementation of the
///     camera.
/// </summary>
public class ScadDocumentAnchorTests
{
    /// <summary>The pixel tolerance between a projected corner and the extent of the covered pixels.</summary>
    private const double PixelTolerance = 1.0;

    /// <summary>The corners of the test box <c>cube([40, 20, 10])</c>, named by their position.</summary>
    private const string BoxAnchors =
        "echo(\"anchor\", \"x0y0z0\", [0, 0, 0]); echo(\"anchor\", \"x1y0z0\", [40, 0, 0]); echo(\"anchor\", \"x1y1z0\", [40, 20, 0]); echo(\"anchor\", \"x0y1z0\", [0, 20, 0]);\n"
        + "echo(\"anchor\", \"x0y0z1\", [0, 0, 10]); echo(\"anchor\", \"x1y0z1\", [40, 0, 10]); echo(\"anchor\", \"x1y1z1\", [40, 20, 10]); echo(\"anchor\", \"x0y1z1\", [0, 20, 10]);\n";

    /// <summary>
    ///     Renders a program and collects the anchors the handler is given.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="configure">Adds the view and the other options to the render options being built.</param>
    /// <param name="anchors">Receives the anchors in the order the handler got them.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface Render(
        string source,
        int width,
        int height,
        Func<Action<ScadAnchor>, ScadRenderOptions> configure,
        List<ScadAnchor> anchors) =>
        ScadDocument.Parse(source).Render(width, height, configure(anchors.Add), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Finds the extent of the covered pixels as continuous edges: the left and top edge of the first covered column
    ///     and row, and the right and bottom edge of the last ones.
    /// </summary>
    /// <param name="surface">The rendered surface.</param>
    /// <returns>The left, top, right and bottom edges.</returns>
    private static (double Left, double Top, double Right, double Bottom) CoveredEdges(Surface surface)
    {
        var rows = Enumerable.Range(0, surface.Height).Select(y => (Row: y, Extent: RowExtent(surface, y, White))).Where(r => r.Extent is not null).ToList();
        Assert.NotEmpty(rows);
        return (rows.Min(r => r.Extent!.Value.First), rows[0].Row, rows.Max(r => r.Extent!.Value.Last) + 1.0, rows[^1].Row + 1.0);
    }

    /// <summary>
    ///     Asserts that an anchor's pixel is within the tolerance of a point.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="x">The expected horizontal position.</param>
    /// <param name="y">The expected vertical position.</param>
    private static void AssertPixel(ScadAnchor anchor, double x, double y)
    {
        Assert.NotNull(anchor.Pixel);
        Assert.InRange(anchor.Pixel.Value.X, x - PixelTolerance, x + PixelTolerance);
        Assert.InRange(anchor.Pixel.Value.Y, y - PixelTolerance, y + PixelTolerance);
    }

    /// <summary>
    ///     Validates the projection against the image: in a top orthographic view of a box the anchors at the corners are
    ///     on the corners of the covered rectangle, whatever the size and aspect ratio of the image.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    [Theory]
    [InlineData(400, 300)]
    [InlineData(800, 400)]
    [InlineData(300, 600)]
    public void ScadDocumentAnchor_TopOrthographicBox_CornersMatchTheRenderedImage(int width, int height)
    {
        // Arrange: auto-fit top view; +X is to the right and +Y up the image
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "cube([40, 20, 10]);\n" + BoxAnchors,
            width,
            height,
            handler => new ScadRenderOptions
            {
                ViewAzimuthDegrees = 0,
                ViewElevationDegrees = 90,
                Projection = CameraProjection.Orthographic,
                SceneOptions = Flat,
                AnchorHandler = handler,
            },
            anchors);
        var (left, top, right, bottom) = CoveredEdges(surface);

        // Assert: the top face covers the rectangle; low and high corners project to the same pixel in an orthographic top view
        Assert.Equal(8, anchors.Count);
        Assert.All(anchors, a => Assert.True(a.IsInsideImage));
        AssertPixel(anchors.Single(a => a.Name == "x0y1z1"), left, top);
        AssertPixel(anchors.Single(a => a.Name == "x1y1z1"), right, top);
        AssertPixel(anchors.Single(a => a.Name == "x1y0z1"), right, bottom);
        AssertPixel(anchors.Single(a => a.Name == "x0y0z1"), left, bottom);
        AssertPixel(anchors.Single(a => a.Name == "x0y0z0"), left, bottom);
        AssertPixel(anchors.Single(a => a.Name == "x1y1z0"), right, top);
    }

    /// <summary>
    ///     Validates the projection with an explicit orthographic camera against the analytic pixel of a known mapping
    ///     (10 pixels per model unit), and that the handler gets the world point after the transforms.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_ExplicitOrthographicCamera_GivesTheAnalyticPixel()
    {
        // Arrange: x from -10 to 50 and y from -5 to 25 at 10 pixels per unit
        var view = ScadOrthoView.Top(20, 10, 60, 30, 10);
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "translate([5, 5, 0]) { cube(1); echo(\"anchor\", \"corner\", [0, 15, 10]); }\necho(\"anchor\", \"origin\", [0, 0, 0]);",
            view.Width,
            view.Height,
            handler => new ScadRenderOptions { Camera = view.Camera, SceneOptions = Flat, AnchorHandler = handler },
            anchors);

        // Assert
        Assert.Equal(2, anchors.Count);
        Assert.Equal(new Vector3D(5, 20, 10), anchors[0].Point);
        AssertPixel(anchors[0], 150, 50);
        Assert.Equal(new Vector3D(0, 0, 0), anchors[1].Point);
        AssertPixel(anchors[1], 100, 250);
        Assert.All(anchors, a => Assert.True(a.IsInsideImage));
        Assert.Equal(new ScadSourceLocation(null, 1, 33), anchors[0].Location);
    }

    /// <summary>
    ///     Validates the perspective projection against the image: the silhouette of a box seen from the front is its
    ///     nearer face, and the anchors on that face's corners are on the corners of the covered rectangle.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_PerspectiveFront_CornersMatchTheRenderedImage()
    {
        // Arrange: a front view (eye at -Y); the face at y = 0 is nearer, so it is the bigger one
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "cube([40, 20, 10]);\n" + BoxAnchors,
            600,
            400,
            handler => new ScadRenderOptions
            {
                ViewAzimuthDegrees = 0,
                ViewElevationDegrees = 0,
                Projection = CameraProjection.Perspective,
                SceneOptions = Flat,
                AnchorHandler = handler,
            },
            anchors);
        var (left, top, right, bottom) = CoveredEdges(surface);

        // Assert: image y grows downward, so the z = 10 corners are at the top
        AssertPixel(anchors.Single(a => a.Name == "x0y0z1"), left, top);
        AssertPixel(anchors.Single(a => a.Name == "x1y0z1"), right, top);
        AssertPixel(anchors.Single(a => a.Name == "x1y0z0"), right, bottom);
        AssertPixel(anchors.Single(a => a.Name == "x0y0z0"), left, bottom);

        // The far face is smaller than the near face, which only a perspective projection does
        var near = anchors.Single(a => a.Name == "x1y0z1").Pixel!.Value.X - anchors.Single(a => a.Name == "x0y0z1").Pixel!.Value.X;
        var far = anchors.Single(a => a.Name == "x1y1z1").Pixel!.Value.X - anchors.Single(a => a.Name == "x0y1z1").Pixel!.Value.X;
        Assert.True(far < near);
    }

    /// <summary>
    ///     Validates the auto-fit framing: with the default isometric view the extremes of the projected corners are the
    ///     extremes of the covered pixels, every corner is inside the image, and the 5 percent margin of the fit leaves
    ///     the model clear of the edges.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="perspective"><see langword="true"/> for a perspective camera.</param>
    [Theory]
    [InlineData(800, 400, false)]
    [InlineData(400, 800, false)]
    [InlineData(640, 640, false)]
    [InlineData(800, 400, true)]
    public void ScadDocumentAnchor_AutoFitIsometric_CornersMatchTheExtentAndStayInside(int width, int height, bool perspective)
    {
        // Arrange
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "cube([40, 20, 10]);\n" + BoxAnchors,
            width,
            height,
            handler => new ScadRenderOptions
            {
                Projection = perspective ? CameraProjection.Perspective : CameraProjection.Orthographic,
                SceneOptions = Flat,
                AnchorHandler = handler,
            },
            anchors);
        var (left, top, right, bottom) = CoveredEdges(surface);

        // Assert: the silhouette of a box is spanned by its corners
        Assert.Equal(8, anchors.Count);
        Assert.All(anchors, a => Assert.True(a.IsInsideImage));
        Assert.InRange(anchors.Min(a => a.Pixel!.Value.X), left - PixelTolerance, left + PixelTolerance);
        Assert.InRange(anchors.Max(a => a.Pixel!.Value.X), right - PixelTolerance, right + PixelTolerance);
        Assert.InRange(anchors.Min(a => a.Pixel!.Value.Y), top - PixelTolerance, top + PixelTolerance);
        Assert.InRange(anchors.Max(a => a.Pixel!.Value.Y), bottom - PixelTolerance, bottom + PixelTolerance);

        // The fitted margin keeps the model off the edges: the tighter dimension leaves about 5 percent
        var clearance = Math.Min(Math.Min(left, width - right) / width, Math.Min(top, height - bottom) / height);
        Assert.InRange(clearance, 0.02, 0.5);
    }

    /// <summary>
    ///     Validates that a model that is entirely 2D, drawn with the flat default top view, reports pixels on the
    ///     corners of its covered rectangle.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_FlatModel_CornersMatchTheRenderedImage()
    {
        // Arrange
        var anchors = new List<ScadAnchor>();
        var views = new List<ScadResolvedView>();

        // Act
        using var surface = Render(
            "square([40, 20]);\necho(\"anchor\", \"lowerLeft\", [0, 0, 0]); echo(\"anchor\", \"upperRight\", [40, 20, 0]);",
            500,
            300,
            handler => new ScadRenderOptions { SceneOptions = Flat, AnchorHandler = handler, ResolvedViewHandler = views.Add },
            anchors);
        var (left, top, right, bottom) = CoveredEdges(surface);

        // Assert
        Assert.Equal(ScadViewSource.FlatDefault, Assert.Single(views).Source);
        AssertPixel(anchors.Single(a => a.Name == "lowerLeft"), left, bottom);
        AssertPixel(anchors.Single(a => a.Name == "upperRight"), right, top);
    }

    /// <summary>
    ///     Validates that the document's own camera (<c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c>) is the one the pixels are
    ///     projected with: the viewport target is at the center of the image.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_DocumentViewport_TargetIsAtTheImageCenter()
    {
        // Arrange
        const string Source = "$vpt = [20, 10, 5]; $vpr = [60, 0, 30]; $vpd = 150;\ncube([40, 20, 10]);\necho(\"anchor\", \"target\", [20, 10, 5]);";
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(Source, 640, 480, handler => new ScadRenderOptions { SceneOptions = Flat, AnchorHandler = handler }, anchors);

        // Assert
        var anchor = Assert.Single(anchors);
        AssertPixel(anchor, 320, 240);
        Assert.True(anchor.IsInsideImage);
    }

    /// <summary>
    ///     Validates the inside flag and the missing pixel: a point far off to the side has a pixel outside the image,
    ///     and a point behind a perspective eye has no pixel.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_OutsideAndBehindTheEye_AreFlagged()
    {
        // Arrange: a perspective front view from 100 units away
        var camera = Camera.LookAt(new Vector3D(0, -100, 0), Vector3D.Zero, Vector3D.UnitZ);
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "cube(10, center = true);\necho(\"anchor\", \"near\", [0, 0, 0]); echo(\"anchor\", \"far\", [1000, 0, 0]); echo(\"anchor\", \"behind\", [0, -500, 0]);",
            200,
            100,
            handler => new ScadRenderOptions { Camera = camera, SceneOptions = Flat, AnchorHandler = handler },
            anchors);

        // Assert
        var near = anchors.Single(a => a.Name == "near");
        var far = anchors.Single(a => a.Name == "far");
        var behind = anchors.Single(a => a.Name == "behind");
        Assert.True(near.IsInsideImage);
        AssertPixel(near, 100, 50);
        Assert.NotNull(far.Pixel);
        Assert.True(far.Pixel.Value.X > 200);
        Assert.False(far.IsInsideImage);
        Assert.Null(behind.Pixel);
        Assert.False(behind.IsInsideImage);
        Assert.Equal(new Vector3D(0, -500, 0), behind.Point);
    }

    /// <summary>
    ///     Validates that the anchors come in echo order with duplicates kept, and that an echo that is not an anchor is
    ///     not reported.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_DuplicatesAndPlainEchoes_FollowTheEchoOrder()
    {
        // Arrange
        const string Source = "echo(\"anchor\", \"a\", [1, 0, 0]); echo(\"plain\", 1); echo(\"anchor\", \"b\", [2, 0, 0]); echo(\"anchor\", \"a\", [3, 0, 0]); cube(1);";
        var anchors = new List<ScadAnchor>();
        var echo = new List<string>();

        // Act
        using var surface = Render(
            Source,
            100,
            100,
            handler => new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { EchoHandler = echo.Add }, AnchorHandler = handler },
            anchors);

        // Assert
        Assert.Equal(["a", "b", "a"], anchors.Select(a => a.Name));
        Assert.Equal([1.0, 2.0, 3.0], anchors.Select(a => a.Point.X));
        Assert.Equal(4, echo.Count);
        Assert.Equal("ECHO: \"plain\", 1", echo[1]);
    }

    /// <summary>
    ///     Validates that the handler runs once the view is chosen, after the resolved-view handler.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_Handler_RunsAfterTheResolvedViewHandler()
    {
        // Arrange
        var calls = new List<string>();
        var options = new ScadRenderOptions
        {
            ResolvedViewHandler = _ => calls.Add("view"),
            AnchorHandler = a => calls.Add(a.Name),
        };

        // Act
        using var surface = ScadDocument.Parse("cube(1); echo(\"anchor\", \"p\", [0, 0, 0]); echo(\"anchor\", \"q\", [1, 1, 1]);").Render(64, 64, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["view", "p", "q"], calls);
    }

    /// <summary>
    ///     Validates that building the scene (no image, no camera) never calls the handler.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_BuildScene_NeverCallsTheHandler()
    {
        // Arrange
        var anchors = new List<ScadAnchor>();
        var options = new ScadRenderOptions { AnchorHandler = anchors.Add };

        // Act
        var scene = ScadDocument.Parse("cube(1); echo(\"anchor\", \"p\", [0, 0, 0]);").BuildScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(anchors);
        Assert.NotNull(scene);
    }

    /// <summary>
    ///     Validates that declaring anchors does not change the picture: the pixels are byte-identical with and without
    ///     the anchor statements, and the evaluation reports the echo lines once.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_Declarations_DoNotChangeThePixels()
    {
        // Arrange
        const string Plain = "difference() { cube(10); translate([5, 5, 5]) sphere(4); }";
        const string Anchored = "difference() { echo(\"anchor\", \"c\", [0, 0, 0]); cube(10); translate([5, 5, 5]) { sphere(4); echo(\"anchor\", \"s\", [0, 0, 4]); } }";
        var anchors = new List<ScadAnchor>();
        var echo = new List<string>();

        // Act
        using var plain = ScadDocument.Parse(Plain).Render(200, 150, null, TestContext.Current.CancellationToken);
        using var anchored = ScadDocument.Parse(Anchored).Render(
            200,
            150,
            new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { EchoHandler = echo.Add }, AnchorHandler = anchors.Add },
            TestContext.Current.CancellationToken);

        // Assert
        AssertSurfacesIdentical(plain, anchored);
        Assert.Equal(2, echo.Count);
        Assert.Equal(["c", "s"], anchors.Select(a => a.Name));
        Assert.Equal(new Vector3D(5, 5, 9), anchors[1].Point);
    }

    /// <summary>
    ///     Validates that a malformed anchor is one warning that starts with the stable identifier, reported once by a
    ///     render, and that the valid anchors are still delivered.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_Malformed_WarnsOnceAndTheRestAreDelivered()
    {
        // Arrange
        var log = new ScadReportLog();
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render(
            "cube(1);\necho(\"anchor\", \"bad\", [1, 2]);\necho(\"anchor\", \"good\", [1, 2, 3]);",
            64,
            64,
            handler => new ScadRenderOptions { Evaluation = log.Options(), AnchorHandler = handler },
            anchors);

        // Assert
        var warning = Assert.Single(log.Warnings);
        Assert.StartsWith("scad-anchor-invalid:", warning.Message, StringComparison.Ordinal);
        Assert.Equal(ScadDiagnosticCodes.AnchorInvalid, warning.Code);
        Assert.Equal(2, warning.Location.Line);
        Assert.Equal("good", Assert.Single(anchors).Name);
        Assert.Equal(2, log.Echo.Count);
    }

    /// <summary>
    ///     Validates that an exception of the handler propagates, and that the echo lines were still delivered once.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_HandlerException_Propagates()
    {
        // Arrange
        var echo = new List<string>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { EchoHandler = echo.Add },
            AnchorHandler = _ => throw new InvalidOperationException("handler failed"),
        };
        var document = ScadDocument.Parse("cube(1); echo(\"anchor\", \"p\", [0, 0, 0]);");

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => document.Render(32, 32, options, TestContext.Current.CancellationToken));
        Assert.Equal("handler failed", ex.Message);
        Assert.Single(echo);
    }

    /// <summary>
    ///     Validates that a model without anchors never calls the handler.
    /// </summary>
    [Fact]
    public void ScadDocumentAnchor_NoDeclaration_NeverCallsTheHandler()
    {
        // Arrange
        var anchors = new List<ScadAnchor>();

        // Act
        using var surface = Render("cube(1);", 32, 32, handler => new ScadRenderOptions { AnchorHandler = handler }, anchors);

        // Assert
        Assert.Empty(anchors);
    }
}
