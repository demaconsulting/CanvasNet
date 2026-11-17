using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Shared helpers of the system-level render tests: the original <c>render-*.scad</c> fixtures, orthographic
///     views with a known pixel-to-model mapping, flat lighting, pixel sampling and analytic coverage predicates.
///     Everything is computed from the geometry of the fixtures, never from a reference image.
/// </summary>
internal static class ScadRenderTestHelpers
{
    /// <summary>The default background of the renderer.</summary>
    public static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The default surface color of the renderer, a neutral gray.</summary>
    public static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>The scene options that give flat, unshaded colors without antialiasing.</summary>
    public static readonly SceneRenderOptions Flat = new()
    {
        Lights = [new AmbientLight { Intensity = 1.0f }],
        Supersampling = 1,
    };

    /// <summary>
    ///     Opens a <c>render-*.scad</c> fixture by its short name.
    /// </summary>
    /// <param name="name">The name without the <c>render-</c> prefix and the extension.</param>
    /// <returns>The parsed document, whose source name is the fixture path.</returns>
    public static ScadDocument Fixture(string name) => ScadDocument.Open(ScadFixtureHelpers.PathOf($"render-{name}.scad"));

    /// <summary>
    ///     Enumerates the short names of every <c>render-*.scad</c> fixture.
    /// </summary>
    /// <returns>The names, ordered.</returns>
    public static IReadOnlyList<string> FixtureNames() =>
        ScadFixtureHelpers.ValidFixtures()
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => n.StartsWith("render-", StringComparison.Ordinal))
            .Select(n => n["render-".Length..])
            .ToList();

    /// <summary>
    ///     Gets whether a pixel is a neutral gray (equal red, green and blue), as every default-colored surface is.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><see langword="true"/> when the three color channels are equal.</returns>
    public static bool IsNeutral(Rgba32 pixel) => pixel.R == pixel.G && pixel.G == pixel.B;

    /// <summary>
    ///     Counts the pixels that satisfy a predicate.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The number of matching pixels.</returns>
    public static int Count(Surface surface, Func<Rgba32, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (predicate(pixel))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Finds the first and last covered column of a row.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="row">The row.</param>
    /// <param name="background">The background color.</param>
    /// <returns>The first and last column whose pixel is not the background, or <see langword="null"/> for an empty row.</returns>
    public static (int First, int Last)? RowExtent(Surface surface, int row, Rgba32 background)
    {
        var span = surface.GetRowSpan(row);
        var first = -1;
        var last = -1;
        for (var x = 0; x < span.Length; x++)
        {
            if (span[x] != background)
            {
                first = first < 0 ? x : first;
                last = x;
            }
        }

        return first < 0 ? null : (first, last);
    }

    /// <summary>
    ///     Finds the first and last covered row of a column.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="column">The column.</param>
    /// <param name="background">The background color.</param>
    /// <returns>The first and last row whose pixel is not the background, or <see langword="null"/> for an empty column.</returns>
    public static (int First, int Last)? ColumnExtent(Surface surface, int column, Rgba32 background)
    {
        var first = -1;
        var last = -1;
        for (var y = 0; y < surface.Height; y++)
        {
            if (surface[column, y] != background)
            {
                first = first < 0 ? y : first;
                last = y;
            }
        }

        return first < 0 ? null : (first, last);
    }

    /// <summary>
    ///     Tests whether a point is inside a regular polygon whose first vertex is on the +X side of its center.
    /// </summary>
    /// <param name="x">The point X.</param>
    /// <param name="y">The point Y.</param>
    /// <param name="centerX">The center X.</param>
    /// <param name="centerY">The center Y.</param>
    /// <param name="radius">The circumradius.</param>
    /// <param name="sides">The number of sides.</param>
    /// <returns><see langword="true"/> when the point is inside the polygon.</returns>
    public static bool InRegularPolygon(double x, double y, double centerX, double centerY, double radius, int sides)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        var step = 2.0 * Math.PI / sides;
        var angle = Math.Atan2(dy, dx);
        if (angle < 0.0)
        {
            angle += 2.0 * Math.PI;
        }

        // The edge of the sector that contains the point is perpendicular to the sector's middle direction
        var middle = (Math.Floor(angle / step) + 0.5) * step;
        return (dx * Math.Cos(middle)) + (dy * Math.Sin(middle)) <= radius * Math.Cos(Math.PI / sides);
    }

    /// <summary>
    ///     Counts the pixels whose coverage disagrees with an analytic prediction.
    /// </summary>
    /// <param name="view">The view that produced the surface.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="background">The background color.</param>
    /// <param name="expectedCovered">Predicts, from the model coordinates of a pixel center, whether the model covers it.</param>
    /// <returns>The number of pixels where the prediction and the image differ.</returns>
    public static int CountMismatches(ScadOrthoView view, Surface surface, Rgba32 background, Func<double, double, bool> expectedCovered)
    {
        var mismatches = 0;
        for (var row = 0; row < surface.Height; row++)
        {
            var v = view.V(row);
            for (var column = 0; column < surface.Width; column++)
            {
                var covered = surface[column, row] != background;
                if (covered != expectedCovered(view.U(column), v))
                {
                    mismatches++;
                }
            }
        }

        return mismatches;
    }

    /// <summary>
    ///     Gets the pixel at a model position of a view.
    /// </summary>
    /// <param name="view">The view that produced the surface.</param>
    /// <param name="surface">The surface.</param>
    /// <param name="u">The horizontal model coordinate.</param>
    /// <param name="v">The vertical model coordinate.</param>
    /// <returns>The pixel that contains the point.</returns>
    public static Rgba32 At(ScadOrthoView view, Surface surface, double u, double v) =>
        surface[view.Column(u), view.Row(v)];
}

/// <summary>
///     An orthographic view of a model along one axis with a known mapping between model coordinates and pixels:
///     a top view (looking down -Z, model X to the right, model Y up) or a front view (looking along +Y, model X
///     to the right, model Z up). Pixel edges fall on model coordinates that are multiples of one pixel.
/// </summary>
internal sealed class ScadOrthoView
{
    /// <summary>How far the eye is from the target, in model units; it only has to clear the model.</summary>
    private const double EyeDistance = 500.0;

    /// <summary>The horizontal model coordinate at the left edge of the image.</summary>
    private readonly double _left;

    /// <summary>The vertical model coordinate at the top edge of the image.</summary>
    private readonly double _top;

    /// <summary>The number of pixels per model unit.</summary>
    private readonly int _pixelsPerUnit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadOrthoView"/> class.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="left">The horizontal model coordinate at the left edge.</param>
    /// <param name="top">The vertical model coordinate at the top edge.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    private ScadOrthoView(Camera camera, double left, double top, int width, int height, int pixelsPerUnit)
    {
        Camera = camera;
        _left = left;
        _top = top;
        Width = width;
        Height = height;
        _pixelsPerUnit = pixelsPerUnit;
    }

    /// <summary>Gets the camera of the view.</summary>
    public Camera Camera { get; }

    /// <summary>Gets the image width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the image height in pixels.</summary>
    public int Height { get; }

    /// <summary>
    ///     Creates a view straight down onto the XY plane.
    /// </summary>
    /// <param name="centerX">The model X at the center of the image.</param>
    /// <param name="centerY">The model Y at the center of the image.</param>
    /// <param name="widthUnits">The model width the image covers.</param>
    /// <param name="heightUnits">The model height the image covers.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    /// <returns>The view.</returns>
    public static ScadOrthoView Top(double centerX, double centerY, double widthUnits, double heightUnits, int pixelsPerUnit = 1) =>
        Create(
            new Vector3D(centerX, centerY, EyeDistance),
            new Vector3D(centerX, centerY, 0),
            Vector3D.UnitY,
            centerX,
            centerY,
            widthUnits,
            heightUnits,
            pixelsPerUnit);

    /// <summary>
    ///     Creates a view from the front (the eye at negative Y, looking toward +Y).
    /// </summary>
    /// <param name="centerX">The model X at the center of the image.</param>
    /// <param name="centerZ">The model Z at the center of the image.</param>
    /// <param name="widthUnits">The model width the image covers.</param>
    /// <param name="heightUnits">The model height the image covers.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    /// <returns>The view.</returns>
    public static ScadOrthoView Front(double centerX, double centerZ, double widthUnits, double heightUnits, int pixelsPerUnit = 1) =>
        Create(
            new Vector3D(centerX, -EyeDistance, centerZ),
            new Vector3D(centerX, 0, centerZ),
            Vector3D.UnitZ,
            centerX,
            centerZ,
            widthUnits,
            heightUnits,
            pixelsPerUnit);

    /// <summary>
    ///     Gets the column that contains a horizontal model coordinate.
    /// </summary>
    /// <param name="u">The model coordinate.</param>
    /// <returns>The column.</returns>
    public int Column(double u) => (int)Math.Floor((u - _left) * _pixelsPerUnit);

    /// <summary>
    ///     Gets the row that contains a vertical model coordinate.
    /// </summary>
    /// <param name="v">The model coordinate.</param>
    /// <returns>The row.</returns>
    public int Row(double v) => (int)Math.Floor((_top - v) * _pixelsPerUnit);

    /// <summary>
    ///     Gets the horizontal model coordinate of the center of a column.
    /// </summary>
    /// <param name="column">The column.</param>
    /// <returns>The model coordinate.</returns>
    public double U(int column) => _left + ((column + 0.5) / _pixelsPerUnit);

    /// <summary>
    ///     Gets the vertical model coordinate of the center of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The model coordinate.</returns>
    public double V(int row) => _top - ((row + 0.5) / _pixelsPerUnit);

    /// <summary>
    ///     Creates render options for this view.
    /// </summary>
    /// <param name="scene">The look options; flat unshaded colors without antialiasing when <see langword="null"/>.</param>
    /// <param name="evaluation">The evaluation options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The options.</returns>
    public ScadRenderOptions Options(SceneRenderOptions? scene = null, ScadEvaluationOptions? evaluation = null) => new()
    {
        Camera = Camera,
        SceneOptions = scene ?? ScadRenderTestHelpers.Flat,
        Evaluation = evaluation,
    };

    /// <summary>
    ///     Renders a document in this view.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="scene">The look options; flat unshaded colors without antialiasing when <see langword="null"/>.</param>
    /// <param name="evaluation">The evaluation options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    public Surface Render(ScadDocument document, SceneRenderOptions? scene = null, ScadEvaluationOptions? evaluation = null) =>
        document.Render(Width, Height, Options(scene, evaluation), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Builds a view from a camera placement and the model window it covers.
    /// </summary>
    /// <param name="eye">The eye.</param>
    /// <param name="target">The target.</param>
    /// <param name="up">The up direction.</param>
    /// <param name="centerU">The horizontal model coordinate at the center of the image.</param>
    /// <param name="centerV">The vertical model coordinate at the center of the image.</param>
    /// <param name="widthUnits">The model width the image covers.</param>
    /// <param name="heightUnits">The model height the image covers.</param>
    /// <param name="pixelsPerUnit">The number of pixels per model unit.</param>
    /// <returns>The view.</returns>
    private static ScadOrthoView Create(
        Vector3D eye,
        Vector3D target,
        Vector3D up,
        double centerU,
        double centerV,
        double widthUnits,
        double heightUnits,
        int pixelsPerUnit)
    {
        var camera = new Camera
        {
            Eye = eye,
            Target = target,
            Up = up,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = heightUnits,
        };
        return new ScadOrthoView(
            camera,
            centerU - (widthUnits / 2.0),
            centerV + (heightUnits / 2.0),
            (int)Math.Round(widthUnits * pixelsPerUnit),
            (int)Math.Round(heightUnits * pixelsPerUnit),
            pixelsPerUnit);
    }
}

/// <summary>
///     Collects what the evaluation, geometry and render stages report through the public handlers of
///     <see cref="ScadEvaluationOptions"/>.
/// </summary>
internal sealed class ScadReportLog
{
    /// <summary>Gets the echo lines in order.</summary>
    public List<string> Echo { get; } = [];

    /// <summary>Gets the warnings in order.</summary>
    public List<ScadDiagnostic> Warnings { get; } = [];

    /// <summary>Gets the unsupported-feature reports in order.</summary>
    public List<ScadUnsupportedFeature> Unsupported { get; } = [];

    /// <summary>
    ///     Creates evaluation options whose handlers record into this log.
    /// </summary>
    /// <param name="variables">The variable overrides, or <see langword="null"/> for none.</param>
    /// <param name="strict">Whether the first unsupported feature throws.</param>
    /// <returns>The options.</returns>
    public ScadEvaluationOptions Options(IReadOnlyDictionary<string, object?>? variables = null, bool strict = false) => new()
    {
        Variables = variables,
        EchoHandler = Echo.Add,
        WarningHandler = Warnings.Add,
        UnsupportedFeatureHandler = Unsupported.Add,
        ThrowOnUnsupportedFeature = strict,
    };
}
