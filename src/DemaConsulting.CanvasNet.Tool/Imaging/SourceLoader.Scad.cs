// cspell:ignore scad openscad
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     OpenSCAD sources of <see cref="SourceLoader"/>: a model drawn from one viewpoint.
/// </summary>
/// <remarks>
///     <para>
///         A model has no pages and no natural size: it is evaluated and drawn at the requested size, and
///         <see cref="DefaultScadSize"/> pixels square is the natural size that <c>--scale</c> multiplies and that
///         is used when no size option is given. The viewpoint is a named one (<see cref="ScadViews"/>) whose azimuth
///         and elevation <c>--azimuth</c> and <c>--elevation</c> can replace; the projection is orthographic unless
///         perspective is asked for. The renderer draws over the background color directly, so a background with
///         zero alpha gives a transparent image.
///     </para>
///     <para>
///         The <c>include</c> and <c>use</c> directives of the model are resolved by the SCAD library: a relative path is
///         looked up from the folder of the file that names it, and no file outside the folder of the model (and its
///         folders below it) is read; a link below that folder is not followed. A directive whose file cannot be found is an
///         error (<c>scad-error</c>), because the model would otherwise be drawn without what it asked for.
///         Warnings, unsupported features, and <c>echo</c> output of the model are returned as diagnostics and echo
///         lines; with <see cref="SourceOptions.Strict"/> an unsupported feature is an <c>unsupported-feature</c>
///         error instead.
///     </para>
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     The name of the viewpoint used when none is chosen.
    /// </summary>
    internal const string DefaultScadView = "iso";

    /// <summary>
    ///     The width and height in pixels of a model view when no size option is given; the natural size of a model.
    /// </summary>
    internal const int DefaultScadSize = 800;

    /// <summary>
    ///     The beginning of the warning the library reports for an <c>include</c> or <c>use</c> it cannot open.
    /// </summary>
    private const string MissingIncludeWarning = "Can't open include file";

    /// <summary>
    ///     The named viewpoints, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     The camera of a view is on the side of the model its name says, looking at the model; the Z axis is up. A
    ///     front view looks along +Y from the -Y side, a right view looks along -X from the +X side, and a top view
    ///     looks down along -Z.
    /// </remarks>
    internal static readonly IReadOnlyList<ScadView> ScadViews =
    [
        new(DefaultScadView, Camera.DefaultAzimuthDegrees, Camera.DefaultElevationDegrees),
        new("front", 0, 0),
        new("back", 180, 0),
        new("left", 270, 0),
        new("right", 90, 0),
        new("top", 0, 90),
        new("bottom", 0, -90)
    ];

    /// <summary>
    ///     Reads what the source of a model declares, by parsing it; the model is not evaluated.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>
    ///     The facts about the file: the names of its modules and functions, its <c>include</c> and <c>use</c>
    ///     directives, and <see cref="DefaultScadSize"/> as its natural size.
    /// </returns>
    /// <exception cref="ToolException">Thrown with code <c>scad-error</c> when the source has a syntax error.</exception>
    private static SourceInfo ProbeScad(string path, long bytes)
    {
        var document = Read(path, SourceKind.Scad, () => ScadDocument.Open(path));
        var directives = document.Directives
            .Select(d => new SourceDirective(d.Kind == ScadDirectiveKind.Use ? "use" : "include", d.Path, d.Location.Line))
            .ToList();
        return new SourceInfo(
            path,
            SourceKind.Scad,
            bytes,
            DefaultScadSize,
            DefaultScadSize,
            3,
            false,
            true,
            1,
            1,
            null,
            [],
            [.. document.ModuleNames],
            [.. document.FunctionNames])
        {
            Directives = directives
        };
    }

    /// <summary>
    ///     Maps a failure of the evaluation of a model to a <see cref="ToolException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>budget-exceeded</c> when the model exceeded a limit (a limit of the SCAD library,
    ///     such as the triangle limit, or the fragment budget of the renderer), otherwise <c>scad-error</c> (a failed
    ///     <c>assert</c>, an include cycle, an include outside the folder of the model, and so on).
    /// </returns>
    /// <remarks>
    ///     The library names every limit failure with the text <c>(limit N)</c> and reports the renderer's budget with the
    ///     budget exception as the cause; a test pins the texts, because the library gives no other way to tell the two
    ///     kinds of failure apart.
    /// </remarks>
    internal static ToolException MapScadEvaluation(ScadEvaluationException exception)
    {
        var message = OneLine(exception.Message);
        var limit = message.Contains("(limit ", StringComparison.Ordinal) || exception.InnerException is RenderBudgetExceededException;
        if (!limit)
        {
            return new ToolException(ErrorCodes.ScadError, $"SCAD evaluation error: {message}", null, exception);
        }

        var hint = message.Contains("Triangle limit", StringComparison.Ordinal)
            ? $"Raise --max-triangles (the default is {ScadLimits.Default.MaxTriangles}) or simplify the model: fewer or smaller $fn values, loops, or instances."
            : "Simplify the model; this limit cannot be changed from the command line.";
        return new ToolException(ErrorCodes.BudgetExceeded, $"The SCAD model exceeds a limit: {message}", hint, exception);
    }

    /// <summary>
    ///     A model opened for drawing.
    /// </summary>
    private sealed class ScadPageSource : PageSource
    {
        /// <summary>
        ///     The parsed model.
        /// </summary>
        private readonly ScadDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ScadPageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed model.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        private ScadPageSource(ScadDocument document, SourceInfo info, SourceOptions options)
            : base(info, options)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a model.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ToolException">Thrown when the file cannot be read or has a syntax error.</exception>
        public static ScadPageSource Open(SourceInfo info, SourceOptions options)
        {
            var document = Read(info.Path, SourceKind.Scad, () => ScadDocument.Open(info.Path));
            return new ScadPageSource(document, info, options);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var view = Options.Scad ?? new ScadViewOptions();
            var (azimuth, elevation) = ResolveView(view);
            var (width, height) = ComputeTargetSize(Info.Path, Info.Width, Info.Height, Options);
            var background = Options.Background ?? new Rgba32(255, 255, 255, 255);

            var echo = new List<string>();
            var notes = new List<ImageDiagnostic>();
            var missingInclude = new List<ScadDiagnostic>();
            var renderOptions = CreateRenderOptions(view, azimuth, elevation, background, echo, notes, missingInclude);

            var surface = Read(Info.Path, SourceKind.Scad, () => _document.Render(width, height, renderOptions));
            if (missingInclude.Count > 0)
            {
                surface.Dispose();
                var first = missingInclude[0];
                throw new ToolException(
                    ErrorCodes.ScadError,
                    $"SCAD include not found: {OneLine(first.ToString())}",
                    "A file named by include or use is looked up from the folder of the file that names it, and only inside the folder of the model.");
            }

            var operations = new List<AppliedOperation>
            {
                Operation("render", DescribeRender(view, azimuth, elevation, width, height, background))
            };
            return new LoadedSource(surface, Info, 1, true, notes, echo, operations);
        }

        /// <summary>
        ///     Finds the azimuth and elevation of a view: those of the named viewpoint, with the overrides applied.
        /// </summary>
        /// <param name="view">The view options.</param>
        /// <returns>The azimuth and the elevation in degrees.</returns>
        /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the name is not a viewpoint.</exception>
        private static (double Azimuth, double Elevation) ResolveView(ScadViewOptions view)
        {
            var named = ScadViews.FirstOrDefault(v => string.Equals(v.Name, view.View, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ToolException(
                            ErrorCodes.UsageError,
                            $"'{view.View}' is not a view.",
                            $"The views are: {string.Join(", ", ScadViews.Select(v => v.Name))}.");
            return (view.Azimuth ?? named.Azimuth, view.Elevation ?? named.Elevation);
        }

        /// <summary>
        ///     Creates the options of one render of the model.
        /// </summary>
        /// <param name="view">The view options.</param>
        /// <param name="azimuth">The azimuth in degrees.</param>
        /// <param name="elevation">The elevation in degrees.</param>
        /// <param name="background">The background color.</param>
        /// <param name="echo">The list the <c>echo</c> lines of the model are added to.</param>
        /// <param name="notes">The list the warnings and unsupported features of the model are added to.</param>
        /// <param name="missingInclude">The list the warnings about an <c>include</c> or <c>use</c> that cannot be opened are added to.</param>
        /// <returns>The options.</returns>
        /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when a variable has a name the library does not accept.</exception>
        private ScadRenderOptions CreateRenderOptions(
            ScadViewOptions view,
            double azimuth,
            double elevation,
            Rgba32 background,
            List<string> echo,
            List<ImageDiagnostic> notes,
            List<ScadDiagnostic> missingInclude)
        {
            try
            {
                var limits = view.MaxTriangles is { } triangles ? new ScadLimits { MaxTriangles = triangles } : ScadLimits.Default;
                var evaluation = new ScadEvaluationOptions
                {
                    Variables = view.Defines.Count > 0 ? view.Defines : null,
                    Time = view.Time ?? 0,
                    Limits = limits,
                    EchoHandler = echo.Add,
                    WarningHandler = warning =>
                    {
                        if (warning.Message.StartsWith(MissingIncludeWarning, StringComparison.Ordinal))
                        {
                            missingInclude.Add(warning);
                        }

                        notes.Add(new ImageDiagnostic("scad", "warning", "scad-warning", OneLine(warning.ToString())));
                    },
                    UnsupportedFeatureHandler = unsupported => notes.Add(
                        new ImageDiagnostic("scad", "unsupported", unsupported.Feature, OneLine($"{unsupported.Location}: {unsupported.Message}"))),
                    ThrowOnUnsupportedFeature = Options.Strict
                };
                var scene = view.DefaultColor is { } color
                    ? new SceneRenderOptions { BackgroundColor = background, DefaultColor = color }
                    : new SceneRenderOptions { BackgroundColor = background };
                return new ScadRenderOptions
                {
                    Evaluation = evaluation,
                    ViewAzimuthDegrees = azimuth,
                    ViewElevationDegrees = elevation,
                    Projection = view.Perspective ? CameraProjection.Perspective : CameraProjection.Orthographic,
                    SceneOptions = scene
                };
            }
            catch (ArgumentException ex)
            {
                // The library rejects a variable name it does not accept; that is a mistake in the command line
                throw new ToolException(ErrorCodes.UsageError, $"A -D definition is not valid: {OneLine(ex.Message)}", null, ex);
            }
        }

        /// <summary>
        ///     Describes a render for the <c>operations</c> of a result.
        /// </summary>
        /// <param name="view">The view options.</param>
        /// <param name="azimuth">The azimuth in degrees.</param>
        /// <param name="elevation">The elevation in degrees.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="background">The background color.</param>
        /// <returns>The parameters of the operation, in the order they are written.</returns>
        private static (string Name, object Value)[] DescribeRender(
            ScadViewOptions view,
            double azimuth,
            double elevation,
            int width,
            int height,
            Rgba32 background)
        {
            var fields = new List<(string Name, object Value)>
            {
                ("view", view.View.ToLowerInvariant()),
                ("azimuth", azimuth),
                ("elevation", elevation),
                ("projection", view.Perspective ? "perspective" : "orthographic"),
                ("width", width),
                ("height", height),
                ("background", FormatColor(background))
            };
            if (view.Defines.Count > 0)
            {
                fields.Add(("defines", view.Defines.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => FormatDefine(d.Key, d.Value)).ToList()));
            }

            if (view.Time is { } time)
            {
                fields.Add(("time", time));
            }

            if (view.MaxTriangles is { } triangles)
            {
                fields.Add(("maxTriangles", triangles));
            }

            if (view.DefaultColor is { } color)
            {
                fields.Add(("defaultColor", FormatColor(color)));
            }

            return [.. fields];
        }

        /// <summary>
        ///     Formats a variable definition as <c>name=value</c>, with a text value in quotes.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value: a number, a flag, or text.</param>
        /// <returns>The text.</returns>
        private static string FormatDefine(string name, object? value)
        {
            var text = value switch
            {
                bool flag => flag ? "true" : "false",
                string content => $"\"{content}\"",
                IFormattable number => number.ToString("R", CultureInfo.InvariantCulture),
                _ => "undef"
            };
            return $"{name}={text}";
        }
    }
}
