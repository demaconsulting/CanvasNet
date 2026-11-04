// cspell:ignore halign valign
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Tests.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.ScadRenderTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     System tests of the viewport variables and of the flat default view through the public API: the drawing sheet and
///     the head-up display of the feature request, the precedence of the sources of the view, what the document sees in
///     <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c>, and the view that a render reports.
/// </summary>
public class ScadDocumentViewportRenderTests
{
    /// <summary>The default surface color of the renderer, as a flat unlit shape shows it.</summary>
    private static readonly Rgba32 DefaultGray = Gray;

    /// <summary>
    ///     Renders a document with a view handler and returns the picture; every view the handler is given is added to a list.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="create">Creates the render options around the handler.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="views">Receives every view the handler was given.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface RenderWithView(ScadDocument document, Func<Action<ScadResolvedView>, ScadRenderOptions> create, int width, int height, List<ScadResolvedView> views) =>
        document.Render(width, height, create(views.Add), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Validates the drawing sheet of the feature request: no warnings, a flat top view reported as such, and every
    ///     covered pixel exactly the default gray (flat, unlit), framed with the 5 percent margin of the fit.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_ProjectionSheet_IsDrawnFlatFromTheTop()
    {
        // Arrange
        var log = new ScadReportLog();
        var views = new List<ScadResolvedView>();

        // Act
        using var surface = RenderWithView(
            Fixture("projection-sheet"),
            handler => new ScadRenderOptions { Evaluation = log.Options(), SceneOptions = new SceneRenderOptions { Supersampling = 1 }, ResolvedViewHandler = handler },
            800,
            600,
            views);

        // Assert: nothing reported, a top view, and one flat color
        Assert.Empty(log.Warnings);
        Assert.Empty(log.Unsupported);
        var view = Assert.Single(views);
        Assert.Equal(new ScadResolvedView(0.0, 90.0, CameraProjection.Orthographic, true, ScadViewSource.FlatDefault), view);
        Assert.True(Count(surface, p => p == White) > 0);
        Assert.Equal(0, Count(surface, p => p != White && p != DefaultGray));
        Assert.True(Count(surface, p => p == DefaultGray) > 10_000);

        // The model spans 135 x 87.5 units: the width fills the picture but for the margin of 2.5 percent on each side
        var columns = Enumerable.Range(0, surface.Width).Where(x => ColumnExtent(surface, x, White) is not null).ToList();
        Assert.InRange(columns[0], 15, 25);
        Assert.InRange(columns[^1], 774, 785);
    }

    /// <summary>
    ///     Validates the content of the drawing sheet by sampling a top view with a known mapping: the two projections of
    ///     the cube are 30 x 20 and 30 x 15 around their centers, the border is two units wide, and the text is drawn.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_ProjectionSheet_ShowsBothViewsTheBorderAndTheText()
    {
        // Arrange: x from -20 to 130 and y from -15 to 90 at 6 pixels per unit
        var view = ScadOrthoView.Top(55, 37.5, 150, 105, 6);
        using var surface = view.Render(Fixture("projection-sheet"));

        // Act
        var inFirst = new[] { (-10.0, 40.0), (10.0, 40.0), (-14.0, 31.0), (14.0, 49.0) };
        var inSecond = new[] { (-10.0, 0.0), (10.0, -5.0), (-14.0, -7.0), (14.0, 7.0) };
        var outside = new[] { (-17.0, 40.0), (-10.0, 52.0), (60.0, 40.0), (125.0, 40.0), (-10.0, -10.0) };
        var border = new[] { (1.0, 60.0), (60.0, 79.0), (119.0, 30.0), (60.0, 1.0) };

        // Assert: shapes and border in the default gray, white everywhere else
        Assert.All(inFirst.Concat(inSecond).Concat(border), p => Assert.Equal(DefaultGray, At(view, surface, p.Item1, p.Item2)));
        Assert.All(outside, p => Assert.Equal(White, At(view, surface, p.Item1, p.Item2)));

        // The title line is drawn between x 40 and 100 at y 5 to 9: some pixels are covered and some are not
        var title = CountIn(surface, p => p == DefaultGray, view, 41, 100, 5.5, 8.5);
        var gaps = CountIn(surface, p => p == White, view, 41, 100, 5.5, 8.5);
        Assert.True(title > 200);
        Assert.True(gaps > 200);
    }

    /// <summary>
    ///     Validates that the bushing sheet draws the shadows and the section: the holes in the top view, the stepped
    ///     outline of the front view and the cut through the bore in the section.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_ProjectionViews_ShowTheHolesTheStepsAndTheSection()
    {
        // Arrange: the whole sheet at 4 pixels per unit
        var view = ScadOrthoView.Top(80, 55, 160, 110, 4);
        using var surface = view.Render(Fixture("projection-views"));

        // Assert: the top view (centered at 45, 75) is a 50 x 30 plate with the bore and two mounting holes
        Assert.Equal(DefaultGray, At(view, surface, 45 + 12, 75 + 8));
        Assert.Equal(White, At(view, surface, 45, 75));
        Assert.Equal(White, At(view, surface, 45 - 18, 75));
        Assert.Equal(White, At(view, surface, 45 + 18, 75));
        Assert.Equal(White, At(view, surface, 45 + 30, 75));

        // The front view (centered at 45, 25) is the plate (6 tall) with the boss (14 more, 20 wide) on it
        Assert.Equal(DefaultGray, At(view, surface, 45, 25 + 18));
        Assert.Equal(White, At(view, surface, 45 + 15, 25 + 18));
        Assert.Equal(DefaultGray, At(view, surface, 45 + 20, 25 + 3));
        Assert.Equal(White, At(view, surface, 45 + 20, 25 + 8));

        // The section (centered at 112, 75) has the bore (x within 5 of the center) empty and the wall of the boss and the plate solid
        Assert.Equal(White, At(view, surface, 112, 75 + 10));
        Assert.Equal(DefaultGray, At(view, surface, 112 + 7.5, 75 + 10));
        Assert.Equal(DefaultGray, At(view, surface, 112 + 15, 75 + 3));
        Assert.Equal(White, At(view, surface, 112 + 15, 75 + 10));
        Assert.Equal(White, At(view, surface, 112 + 18, 75 + 3));
    }

    /// <summary>
    ///     Validates the head-up display exactly as the requester wrote it: it evaluates without a warning (the viewport
    ///     variables are defined), the text is built, and the text plane is the one that the three negated rotations
    ///     give, which is a tilted plane for the default view (it faces the camera only from the top).
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_HudAsWritten_EvaluatesAndIsPlacedByItsRotations()
    {
        // Arrange
        var log = new ScadReportLog();
        var options = new ScadRenderOptions { Evaluation = log.Options() };
        var vpr = ScadViewport.Plan(options).Defaults.Rotation;

        // Act
        var scene = Fixture("viewport-hud").BuildScene(options, TestContext.Current.CancellationToken);
        var leaves = ScadGeometryTestSupport.Leaves(scene);

        // Assert: no unknown-variable warning, the cube and the text exist
        Assert.Empty(log.Warnings);
        Assert.True(leaves.Count > 1);
        var text = leaves.Skip(1).ToList();
        var vertices = text.SelectMany(l => l.WorldMesh.Vertices).ToList();

        // The chain turns +Z by -vpr[0] about X, -vpr[1] about Y and -vpr[2] about Z, in that order
        var chain = Matrix4x4D.CreateRotationX(-vpr.X) * Matrix4x4D.CreateRotationY(-vpr.Y) * Matrix4x4D.CreateRotationZ(-vpr.Z);
        var normal = chain.TransformDirection(Vector3D.UnitZ);
        var towardViewer = ViewDirection(ScadViewport.Plan(options).Defaults.Rotation);

        // Assert: the thin text lies in the plane of that normal (and is not facing the viewer under the faithful meaning)
        Assert.True(ExtentAlong(vertices, normal) <= 0.1 + 1e-6);
        Assert.True(ExtentAlong(vertices, towardViewer) > 1.0);
    }

    /// <summary>
    ///     Validates the idiom that does face the camera: a thin text placed with <c>translate($vpt) rotate($vpr)</c> has no
    ///     extent across the view direction beyond its thickness and its glyph height along the up direction of the picture,
    ///     for the default view, explicit views and a view the document sets.
    /// </summary>
    /// <param name="prelude">The text put before the model (an assignment of the viewport variables, or nothing).</param>
    /// <param name="azimuth">The azimuth of the explicit view, or NaN for none.</param>
    /// <param name="elevation">The elevation of the explicit view, or NaN for none.</param>
    [Theory]
    [InlineData("", double.NaN, double.NaN)]
    [InlineData("", 0.0, 0.0)]
    [InlineData("", 0.0, 90.0)]
    [InlineData("", 30.0, 20.0)]
    [InlineData("", 200.0, -35.0)]
    [InlineData("$vpr = [60, 0, 20];", double.NaN, double.NaN)]
    public void ScadDocumentViewport_RotateVpr_FacesTheCamera(string prelude, double azimuth, double elevation)
    {
        // Arrange: a flat text H of height 5 placed at the target of the view
        var source = prelude + "\ntranslate($vpt) rotate($vpr) linear_extrude(0.1) text(\"H\", size = 5);";
        ScadRenderOptions Create(Action<ScadResolvedView>? handler) => double.IsNaN(azimuth)
            ? new ScadRenderOptions { ResolvedViewHandler = handler }
            : new ScadRenderOptions { ViewAzimuthDegrees = azimuth, ViewElevationDegrees = elevation, ResolvedViewHandler = handler };
        var document = ScadDocument.Parse(source);
        var views = new List<ScadResolvedView>();

        // Act
        using var surface = RenderWithView(document, Create, 8, 8, views);
        var scene = document.BuildScene(Create(null), TestContext.Current.CancellationToken);
        var vertices = ScadGeometryTestSupport.Leaves(scene).SelectMany(l => l.WorldMesh.Vertices).ToList();

        // Assert: the direction to the viewer is the one of the reported view
        var view = Assert.Single(views);
        var az = view.AzimuthDegrees * Math.PI / 180.0;
        var el = view.ElevationDegrees * Math.PI / 180.0;
        var toViewer = new Vector3D(Math.Sin(az) * Math.Cos(el), -Math.Cos(az) * Math.Cos(el), Math.Sin(el));
        var right = Vector3D.Cross(-toViewer, Vector3D.UnitZ);
        right = right.Length < 1e-9 ? Vector3D.UnitX : right.Normalize();
        var up = Vector3D.Cross(right, -toViewer).Normalize();

        // The text is as thick as it was extruded across the view and as tall as the glyph along the picture's vertical
        Assert.True(ExtentAlong(vertices, toViewer) <= 0.1 + 1e-6, $"depth {ExtentAlong(vertices, toViewer)}");
        Assert.InRange(ExtentAlong(vertices, up), 4.5, 5.5);
    }

    /// <summary>
    ///     Validates that the render reports the view it used, once, for the default 3D view (nothing changes for a model
    ///     with 3D objects) and that the picture is the one the explicit default angles give.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_DefaultThreeDimensionalView_IsUnchangedAndReportedOnce()
    {
        // Arrange
        var document = ScadDocument.Parse("cube([3, 2, 1]);");
        var views = new List<ScadResolvedView>();

        // Act
        using var defaults = RenderWithView(document, handler => new ScadRenderOptions { ResolvedViewHandler = handler }, 64, 64, views);
        using var explicitAngles = document.Render(
            64,
            64,
            new ScadRenderOptions { ViewAzimuthDegrees = Camera.DefaultAzimuthDegrees, ViewElevationDegrees = Camera.DefaultElevationDegrees },
            TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.Single(views);
        Assert.Equal(new ScadResolvedView(45.0, Camera.DefaultElevationDegrees, CameraProjection.Perspective, false, ScadViewSource.Default), view);
        ScadGeometryTestSupport.AssertSurfacesIdentical(explicitAngles, defaults);
    }

    /// <summary>
    ///     Validates that <c>BuildScene</c> does not choose a camera and never calls the view handler.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_BuildScene_DoesNotCallTheViewHandler()
    {
        // Arrange
        var views = new List<ScadResolvedView>();

        // Act
        ScadDocument.Parse("cube(1);").BuildScene(new ScadRenderOptions { ResolvedViewHandler = views.Add }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(views);
    }

    /// <summary>
    ///     Validates that a model that mixes 2D and 3D objects keeps the behavior it had: the 2D objects are dropped with the
    ///     usual warning and the 3D ones are drawn isometrically with the normal lighting.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_MixedTwoAndThreeDimensions_KeepsTheThreeDimensionalView()
    {
        // Arrange
        var log = new ScadReportLog();
        var views = new List<ScadResolvedView>();
        var document = ScadDocument.Parse("square(10);\ncube(5);");

        // Act
        using var mixed = RenderWithView(document, handler => new ScadRenderOptions { Evaluation = log.Options(), ResolvedViewHandler = handler }, 64, 64, views);
        using var cubeOnly = ScadDocument.Parse("cube(5);").Render(64, 64, new ScadRenderOptions(), TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.Single(views);
        Assert.Equal(ScadViewSource.Default, view.Source);
        Assert.False(view.IsFlatModel);
        Assert.Equal(["A union mixes 2D and 3D objects; the 2D objects are ignored"], log.Warnings.Select(w => w.Message).ToArray());
        ScadGeometryTestSupport.AssertSurfacesIdentical(cubeOnly, mixed);
    }

    /// <summary>
    ///     Validates what overrides the flat default: explicit angles, an explicit camera, and a <c>$vpr</c> of the document
    ///     each give their own view for a model that is entirely 2D, and an explicit projection only changes the projection.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_FlatModel_ExplicitViewsOverrideTheFlatDefault()
    {
        // Arrange
        var document = ScadDocument.Parse("square(10);");
        var withVpr = ScadDocument.Parse("$vpr = [60, 0, 30];\nsquare(10);");
        var camera = Camera.LookAt(new Vector3D(5, -40, 20), new Vector3D(5, 5, 0), Vector3D.UnitZ);
        var cases = new (ScadDocument Document, Func<Action<ScadResolvedView>, ScadRenderOptions> Create)[]
        {
            (document, h => new ScadRenderOptions { ViewAzimuthDegrees = 30, ResolvedViewHandler = h }),
            (document, h => new ScadRenderOptions { Camera = camera, ResolvedViewHandler = h }),
            (withVpr, h => new ScadRenderOptions { ResolvedViewHandler = h }),
            (document, h => new ScadRenderOptions { Projection = CameraProjection.Perspective, ResolvedViewHandler = h }),
        };

        // Act
        var views = cases.Select(c =>
        {
            var seen = new List<ScadResolvedView>();
            using var surface = RenderWithView(c.Document, c.Create, 32, 32, seen);
            return Assert.Single(seen);
        }).ToList();

        // Assert
        Assert.Equal(ScadViewSource.Options, views[0].Source);
        Assert.Equal(30.0, views[0].AzimuthDegrees);
        Assert.Equal(Camera.DefaultElevationDegrees, views[0].ElevationDegrees, 1e-9);
        Assert.Equal(ScadViewSource.Camera, views[1].Source);
        Assert.Equal(ScadViewSource.Document, views[2].Source);
        Assert.Equal(30.0, views[2].AzimuthDegrees, 1e-9);
        Assert.Equal(30.0, views[2].ElevationDegrees, 1e-9);
        Assert.Equal(new ScadResolvedView(0.0, 90.0, CameraProjection.Perspective, true, ScadViewSource.FlatDefault), views[3]);
    }

    /// <summary>
    ///     Validates the explicit camera as the source of the view: it is reported as such, with its own direction.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_ExplicitCamera_IsReportedAsTheSource()
    {
        // Arrange
        var camera = Camera.LookAt(new Vector3D(0, -40, 0), Vector3D.Zero, Vector3D.UnitZ);
        var views = new List<ScadResolvedView>();
        var options = new ScadRenderOptions { Camera = camera, ResolvedViewHandler = views.Add };

        // Act
        using var surface = ScadDocument.Parse("cube(5);").Render(16, 16, options, TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.Single(views);
        Assert.Equal(ScadViewSource.Camera, view.Source);
        Assert.Equal(0.0, view.AzimuthDegrees, 1e-9);
        Assert.Equal(0.0, view.ElevationDegrees, 1e-9);
    }

    /// <summary>
    ///     Validates the look of a flat drawing: the default is flat gray, a <c>color()</c> keeps its exact color, and lights
    ///     that the caller chose are respected.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_FlatDrawing_IsUnlitUnlessLightsAreChosen()
    {
        // Arrange
        var plain = ScadDocument.Parse("square(10, center = true);");
        var red = ScadDocument.Parse("color(\"red\") square(10, center = true);");
        var opaque = new SceneRenderOptions { Supersampling = 1 };
        var dim = new SceneRenderOptions { Supersampling = 1, Lights = [new AmbientLight { Intensity = 0.5f }] };

        // Act
        using var gray = plain.Render(33, 33, new ScadRenderOptions { SceneOptions = opaque }, TestContext.Current.CancellationToken);
        using var colored = red.Render(33, 33, new ScadRenderOptions { SceneOptions = opaque }, TestContext.Current.CancellationToken);
        using var dimmed = plain.Render(33, 33, new ScadRenderOptions { SceneOptions = dim }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DefaultGray, gray[16, 16]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), colored[16, 16]);
        Assert.Equal(new Rgba32(90, 90, 90, 255), dimmed[16, 16]);
    }

    /// <summary>
    ///     Validates that a flat model that sets <c>$vpr</c> is lit like any other (the flat look only applies to the view
    ///     chosen for it).
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_FlatModelWithDocumentView_IsShaded()
    {
        // Arrange: a view from the front at 45 degrees
        var document = ScadDocument.Parse("$vpr = [45, 0, 0];\nsquare(10, center = true);");
        var options = new ScadRenderOptions { SceneOptions = new SceneRenderOptions { Supersampling = 1 }, Projection = CameraProjection.Orthographic };

        // Act
        using var surface = document.Render(33, 33, options, TestContext.Current.CancellationToken);

        // Assert: the center is covered and not the flat gray
        Assert.NotEqual(White, surface[16, 16]);
        Assert.NotEqual(DefaultGray, surface[16, 16]);
    }

    /// <summary>
    ///     Validates what the document sees in the viewport variables for every source, and that the explicit option wins
    ///     over a variable override, which wins over the document's assignment, which wins over the default.
    /// </summary>
    /// <param name="source">The program: it echoes the four variables.</param>
    /// <param name="mode">Which options to render with.</param>
    /// <param name="expected">The expected echo line.</param>
    [Theory]
    [InlineData("", "default", "ECHO: [0, 0, 0], [54.7356, 0, 45], 140, 22.5")]
    [InlineData("", "angles", "ECHO: [0, 0, 0], [70, 0, 30], 140, 22.5")]
    [InlineData("", "camera", "ECHO: [1, 2, 3], [90, 0, 0], 50, 22.5")]
    [InlineData("$vpd = 60;", "default", "ECHO: [0, 0, 0], [54.7356, 0, 45], 60, 22.5")]
    [InlineData("$vpd = 60;", "variables", "ECHO: [0, 0, 0], [10, 20, 30], 70, 22.5")]
    [InlineData("$vpd = 60;", "camera", "ECHO: [1, 2, 3], [90, 0, 0], 50, 22.5")]
    [InlineData("$vpd = 60;", "camera-variables", "ECHO: [1, 2, 3], [90, 0, 0], 50, 22.5")]
    [InlineData("$vpr = [1, 2, 3];", "variables", "ECHO: [0, 0, 0], [10, 20, 30], 70, 22.5")]
    [InlineData("$vpr = [1, 2, 3];", "angles", "ECHO: [0, 0, 0], [70, 0, 30], 140, 22.5")]
    [InlineData("$vpr = [1, 2, 3];\n$vpt = [4, 5];", "default", "ECHO: [4, 5], [1, 2, 3], 140, 22.5")]
    public void ScadDocumentViewport_Variables_FollowTheDocumentedPrecedence(string source, string mode, string expected)
    {
        // Arrange
        var log = new ScadReportLog();
        var variables = new Dictionary<string, object?> { ["$vpd"] = 70.0, ["$vpr"] = new object?[] { 10.0, 20.0, 30.0 } };
        var evaluation = mode.EndsWith("variables", StringComparison.Ordinal) ? log.Options(variables) : log.Options();
        var camera = Camera.LookAt(new Vector3D(1, -48, 3), new Vector3D(1, 2, 3), Vector3D.UnitZ);
        var options = mode switch
        {
            "angles" => new ScadRenderOptions { Evaluation = evaluation, ViewAzimuthDegrees = 30, ViewElevationDegrees = 20 },
            "camera" or "camera-variables" => new ScadRenderOptions { Evaluation = evaluation, Camera = camera },
            _ => new ScadRenderOptions { Evaluation = evaluation },
        };
        var document = ScadDocument.Parse(source + "\necho($vpt, $vpr, $vpd, $vpf);\ncube(1);");

        // Act
        document.BuildScene(options, TestContext.Current.CancellationToken);

        // Assert: the numbers print with six significant digits
        Assert.Empty(log.Warnings);
        Assert.Equal([expected], log.Echo);
    }

    /// <summary>
    ///     Validates that evaluating without any render options shows the documented defaults of a fresh viewer, and that
    ///     reading the viewport variables is not reported as an unknown variable.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_Evaluate_ShowsTheViewerDefaults()
    {
        // Arrange
        var document = ScadDocument.Parse("echo($vpt, $vpr, $vpd, $vpf);");

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: [0, 0, 0], [55, 0, 25], 140, 22.5"], result.Echo);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Validates the manual camera through a render: assigning <c>$vpd</c> turns the automatic framing off and the
    ///     view is reported as the document's.
    /// </summary>
    [Fact]
    public void ScadDocumentViewport_AssignedDistance_GivesAManualCamera()
    {
        // Arrange: the camera 30 units above the origin looks at a cube that is far to the side and so is not in the picture
        var document = ScadDocument.Parse("$vpd = 30;\n$vpr = [0, 0, 0];\ntranslate([500, 0, 0]) cube(1);");
        var views = new List<ScadResolvedView>();

        // Act
        using var surface = RenderWithView(
            document,
            handler => new ScadRenderOptions { Projection = CameraProjection.Orthographic, SceneOptions = new SceneRenderOptions { Supersampling = 1 }, ResolvedViewHandler = handler },
            32,
            32,
            views);

        // Assert: nothing is in the picture (a fitted view would have framed the cube), looking straight down
        Assert.Equal(0, Count(surface, p => p != White));
        var view = Assert.Single(views);
        Assert.Equal(ScadViewSource.Document, view.Source);
        Assert.Equal(90.0, view.ElevationDegrees, 1e-9);
    }

    /// <summary>
    ///     Counts the pixels of a window of a top view that satisfy a predicate.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="view">The view that produced the surface.</param>
    /// <param name="left">The left model coordinate of the window.</param>
    /// <param name="right">The right model coordinate of the window.</param>
    /// <param name="bottom">The bottom model coordinate of the window.</param>
    /// <param name="top">The top model coordinate of the window.</param>
    /// <returns>The number of matching pixels in the window.</returns>
    private static int CountIn(Surface surface, Func<Rgba32, bool> predicate, ScadOrthoView view, double left, double right, double bottom, double top)
    {
        var count = 0;
        for (var column = view.Column(left); column <= view.Column(right); column++)
        {
            for (var row = view.Row(top); row <= view.Row(bottom); row++)
            {
                count += predicate(surface[column, row]) ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>
    ///     Gets the unit direction from the target to the viewer for a viewport rotation.
    /// </summary>
    /// <param name="rotation">The <c>$vpr</c> value.</param>
    /// <returns>The direction.</returns>
    private static Vector3D ViewDirection(Vector3D rotation) =>
        ScadViewport.CameraToWorld(rotation).TransformDirection(Vector3D.UnitZ).Normalize();

    /// <summary>
    ///     Gets how far apart the vertices are along a direction.
    /// </summary>
    /// <param name="vertices">The vertices.</param>
    /// <param name="direction">The unit direction.</param>
    /// <returns>The largest minus the smallest projection.</returns>
    private static double ExtentAlong(IReadOnlyList<Vector3D> vertices, Vector3D direction)
    {
        var projections = vertices.Select(v => Vector3D.Dot(v, direction)).ToList();
        return projections.Max() - projections.Min();
    }
}
