// cspell:ignore scad openscad
using System.Collections.ObjectModel;
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     OpenSCAD sources of <see cref="SourceLoader"/>: a model drawn from one viewpoint.
/// </summary>
/// <remarks>
///     <para>
///         A model has no pages and no natural size: it is evaluated and drawn at the requested size, and
///         <see cref="DefaultScadSize"/> pixels square is the natural size that <c>--scale</c> multiplies and that
///         is used when no size option is given. The viewpoint is a named one (<see cref="ScadViews"/>) whose azimuth
///         and elevation <c>--azimuth</c> and <c>--elevation</c> can replace; when none of the three is given the
///         library chooses the view (isometric, or a flat top view for a model that is entirely 2D, or the view the
///         model sets with its viewport variables). The projection is orthographic unless
///         perspective is asked for. The renderer draws over the background color directly, so a background with
///         zero alpha gives a transparent image.
///     </para>
///     <para>
///         The <c>include</c> and <c>use</c> directives of the model are resolved by the SCAD library: a relative path is
///         looked up from the folder of the file that names it, and the file is read under
///         <see cref="SourceOptions.FileAccess"/> (every regular local file by default, like OpenSCAD). A directive whose
///         file cannot be found is an error (<c>scad-error</c>), because the model would otherwise be drawn without what it
///         asked for; a file the policy denies is the error <c>file-access-denied</c>.
///         Warnings, unsupported features, and <c>echo</c> output of the model are returned as diagnostics and echo
///         lines; with <see cref="SourceOptions.Strict"/> an unsupported feature is an <c>unsupported-feature</c>
///         error instead. The anchors the model declares with <c>echo("anchor", "name", [x, y, z])</c> are collected
///         with their pixel positions in the render (<see cref="LoadedSource.Anchors"/>); a malformed anchor is a
///         <c>scad-warning</c> whose message starts with <c>scad-anchor-invalid</c>.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     The name of the viewpoint used when none is chosen.
    /// </summary>
    public const string DefaultScadView = "iso";

    /// <summary>
    ///     The width and height in pixels of a model view when no size option is given; the natural size of a model.
    /// </summary>
    internal const int DefaultScadSize = 800;

    /// <summary>
    ///     The named viewpoints, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     The camera of a view is on the side of the model its name says, looking at the model; the Z axis is up. A
    ///     front view looks along +Y from the -Y side, a right view looks along -X from the +X side, and a top view
    ///     looks down along -Z. The list cannot be changed, not even through a cast.
    /// </remarks>
    public static readonly IReadOnlyList<ScadView> ScadViews = new ReadOnlyCollection<ScadView>(
    [
        new(DefaultScadView, Camera.DefaultAzimuthDegrees, Camera.DefaultElevationDegrees),
        new("front", 0, 0),
        new("back", 180, 0),
        new("left", 270, 0),
        new("right", 90, 0),
        new("top", 0, 90),
        new("bottom", 0, -90)
    ]);

    /// <summary>
    ///     Reads what the source of a model declares, by parsing it; the model is not evaluated.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <param name="fileAccess">The file-access policy of the files the source reads beside it.</param>
    /// <returns>
    ///     The facts about the file: the names of its modules and functions, its <c>include</c> and <c>use</c>
    ///     directives, and <see cref="DefaultScadSize"/> as its natural size.
    /// </returns>
    /// <exception cref="ViewerException">Thrown with code <c>scad-error</c> when the source has a syntax error.</exception>
    private static SourceInfo ProbeScad(SourceInput input, long bytes, FileAccessPolicy fileAccess)
    {
        var document = Read(input.Name, SourceKind.Scad, () => OpenScad(input, fileAccess));
        var directives = document.Directives
            .Select(d => new SourceDirective(d.Kind == ScadDirectiveKind.Use ? "use" : "include", d.Path, d.Location.Line))
            .ToList();
        return new SourceInfo(
            input.Name,
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
    ///     Parses a model from its source: the name of the source is the name of the library's messages, the folder of
    ///     the source is where its relative includes are looked up, and the policy decides which files they may read.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="fileAccess">The file-access policy of the includes.</param>
    /// <returns>The parsed model.</returns>
    private static ScadDocument OpenScad(SourceInput input, FileAccessPolicy fileAccess)
    {
        using var stream = input.Open();
        return ScadDocument.Open(
            stream,
            new ScadOpenOptions { BaseDirectory = input.Directory, SourceName = input.Name, FileAccess = fileAccess });
    }

    /// <summary>
    ///     Determines whether a failure of a library was caused by a denial of the file-access policy (or a policy that
    ///     could not be applied).
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when its inner exception is such a denial.</returns>
    private static bool IsFileAccessDenial(Exception exception) =>
        exception.InnerException is FileReferenceException
        {
            Kind: FileReferenceFailureKind.AccessDenied or FileReferenceFailureKind.RealPathUnavailable
        };

    /// <summary>The hint of the error <c>file-access-denied</c>.</summary>
    private const string FileAccessDeniedHint = "Allow the file in the file-access policy, or reference a file it allows.";

    /// <summary>
    ///     Maps a failure of the evaluation of a model to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>file-access-denied</c> when the file-access policy denied an include,
    ///     <c>budget-exceeded</c> when the model exceeded a limit (a limit of the SCAD library, such as the triangle
    ///     limit, or the fragment budget of the renderer), otherwise <c>scad-error</c> (a failed <c>assert</c>, an
    ///     include cycle, an include that cannot be read, and so on).
    /// </returns>
    /// <remarks>
    ///     The library names every limit failure with the text <c>(limit N)</c> and reports the renderer's budget with the
    ///     budget exception as the cause; a test pins the texts, because the library gives no other way to tell the two
    ///     kinds of failure apart.
    /// </remarks>
    internal static ViewerException MapScadEvaluation(ScadEvaluationException exception)
    {
        var message = OneLine(exception.Message);
        if (IsFileAccessDenial(exception))
        {
            return new ViewerException(ErrorCodes.FileAccessDenied, $"SCAD include denied: {message}", FileAccessDeniedHint, exception);
        }

        var limit = message.Contains("(limit ", StringComparison.Ordinal) || exception.InnerException is RenderBudgetExceededException;
        if (!limit)
        {
            return new ViewerException(ErrorCodes.ScadError, $"SCAD evaluation error: {message}", null, exception);
        }

        var hint = message.Contains("Triangle limit", StringComparison.Ordinal)
            ? $"Raise --max-triangles (the default is {ScadLimits.Default.MaxTriangles}) or simplify the model: fewer or smaller $fn values, loops, or instances."
            : "Simplify the model; this limit cannot be changed from the command line.";
        return new ViewerException(ErrorCodes.BudgetExceeded, $"The SCAD model exceeds a limit: {message}", hint, exception);
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
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private ScadPageSource(ScadDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a model.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be read or has a syntax error.</exception>
        public static ScadPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context)
        {
            var document = Read(info.Path, SourceKind.Scad, () => OpenScad(input, options.FileAccess));
            return new ScadPageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var view = Options.Scad ?? new ScadViewOptions();
            var (azimuth, elevation) = ResolveView(view);
            var (width, height) = ComputeTargetSize(Info.Path, Info.Width, Info.Height, Options);
            var background = Options.Background ?? new Rgba32(255, 255, 255, 255);

            // The surface is charged before the model is evaluated, so a model that cannot be paid for costs nothing
            Context.ChargeSurface(width, height);
            var echo = new List<string>();
            var notes = new List<ImageDiagnostic>();
            var missingInclude = new List<ScadDiagnostic>();
            var anchors = new List<SourceAnchor>();
            ScadResolvedView? resolved = null;
            var renderOptions = CreateRenderOptions(view, azimuth, elevation, background, echo, notes, missingInclude, anchors, v => resolved = v);

            var surface = Read(Info.Path, SourceKind.Scad, () => _document.Render(width, height, renderOptions, Context.CancellationToken));
            if (missingInclude.Count > 0)
            {
                surface.Dispose();
                var first = missingInclude[0];
                throw new ViewerException(
                    ErrorCodes.ScadError,
                    $"SCAD include not found: {OneLine(first.ToString())}",
                    "A file named by include or use is looked up relative to the folder of the file that names it.");
            }

            var operations = new List<AppliedOperation>
            {
                Operation("render", DescribeRender(view, azimuth, elevation, resolved, width, height, background))
            };
            return new LoadedSource(surface, Info, 1, true, notes, echo, operations) { Anchors = anchors };
        }

        /// <summary>
        ///     Converts the position the SCAD library reports to the location of a note.
        /// </summary>
        /// <param name="location">The position in the source.</param>
        /// <returns>The location, or <see langword="null"/> when the library gave no line (line 0).</returns>
        private static ImageDiagnosticLocation? ToScadLocation(ScadSourceLocation location)
        {
            if (location.Line <= 0)
            {
                return null;
            }

            int? column = location.Column > 0 ? location.Column : null;
            return new ImageDiagnosticLocation(location.File, location.Line, column, null);
        }

        /// <summary>
        ///     Finds the azimuth and elevation of a view: those of the named viewpoint, with the overrides applied.
        /// </summary>
        /// <param name="view">The view options.</param>
        /// <returns>The azimuth and the elevation in degrees.</returns>
        /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the name is not a viewpoint.</exception>
        private static (double Azimuth, double Elevation) ResolveView(ScadViewOptions view)
        {
            var named = ScadViews.FirstOrDefault(v => string.Equals(v.Name, view.View, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ViewerException(
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
        /// <param name="anchors">The list the declared anchors (with their pixel positions in the render) are added to.</param>
        /// <param name="resolvedView">Receives the view the library used.</param>
        /// <returns>The options.</returns>
        /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when a variable has a name the library does not accept.</exception>
        private ScadRenderOptions CreateRenderOptions(
            ScadViewOptions view,
            double azimuth,
            double elevation,
            Rgba32 background,
            List<string> echo,
            List<ImageDiagnostic> notes,
            List<ScadDiagnostic> missingInclude,
            List<SourceAnchor> anchors,
            Action<ScadResolvedView> resolvedView)
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
                        if (warning.Code == ScadDiagnosticCodes.IncludeNotFound)
                        {
                            missingInclude.Add(warning);
                        }

                        notes.Add(
                            new ImageDiagnostic("scad", "warning", "scad-warning", OneLine(warning.ToString()))
                            {
                                Detail = warning.Code == ScadDiagnosticCodes.Warning ? null : warning.Code,
                                Location = ToScadLocation(warning.Location)
                            });
                    },
                    UnsupportedFeatureHandler = unsupported => notes.Add(
                        new ImageDiagnostic("scad", "unsupported", unsupported.Feature, OneLine($"{unsupported.Location}: {unsupported.Message}"))
                        {
                            Location = ToScadLocation(unsupported.Location)
                        }),
                    ThrowOnUnsupportedFeature = Options.Strict
                };
                var scene = view.DefaultColor is { } color
                    ? new SceneRenderOptions { BackgroundColor = background, DefaultColor = color }
                    : new SceneRenderOptions { BackgroundColor = background };
                var projection = view.Perspective ? CameraProjection.Perspective : CameraProjection.Orthographic;
                Action<ScadAnchor> anchorHandler = a => anchors.Add(
                    new SourceAnchor(a.Name, a.Point.X, a.Point.Y, a.Point.Z, a.Pixel?.X, a.Pixel?.Y));

                // Without a chosen view the library picks it: isometric for a 3D model, a flat top view for a 2D one
                return view.IsViewExplicit
                    ? new ScadRenderOptions
                    {
                        Evaluation = evaluation,
                        ViewAzimuthDegrees = azimuth,
                        ViewElevationDegrees = elevation,
                        Projection = projection,
                        SceneOptions = scene,
                        ResolvedViewHandler = resolvedView,
                        AnchorHandler = anchorHandler
                    }
                    : new ScadRenderOptions
                    {
                        Evaluation = evaluation,
                        Projection = projection,
                        SceneOptions = scene,
                        ResolvedViewHandler = resolvedView,
                        AnchorHandler = anchorHandler
                    };
            }
            catch (ArgumentException ex)
            {
                // The library rejects a variable name it does not accept; that is a mistake in the command line
                throw new ViewerException(ErrorCodes.UsageError, $"A -D definition is not valid: {OneLine(ex.Message)}", null, ex);
            }
        }

        /// <summary>
        ///     Describes a render for the <c>operations</c> of a result.
        /// </summary>
        /// <param name="view">The view options.</param>
        /// <param name="azimuth">The azimuth in degrees that was requested.</param>
        /// <param name="elevation">The elevation in degrees that was requested.</param>
        /// <param name="resolved">The view the library used, or <see langword="null"/> when it reported none.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="background">The background color.</param>
        /// <returns>The parameters of the operation, in the order they are written.</returns>
        /// <remarks>
        ///     An explicit view (a viewpoint, an azimuth, or an elevation was given) is described as it was asked for.
        ///     Otherwise the library chose the view and the operation reports what it used: <c>iso</c> for the default
        ///     view, <c>top</c> for the flat top view of a model that is entirely 2D, and <c>custom</c> for a view
        ///     the model set with its viewport variables.
        /// </remarks>
        private static (string Name, object Value)[] DescribeRender(
            ScadViewOptions view,
            double azimuth,
            double elevation,
            ScadResolvedView? resolved,
            int width,
            int height,
            Rgba32 background)
        {
            var name = view.View.ToLowerInvariant();
            var projection = view.Perspective ? CameraProjection.Perspective : CameraProjection.Orthographic;
            if (!view.IsViewExplicit && resolved is not null)
            {
                (name, azimuth, elevation, projection) = (NameView(resolved), resolved.AzimuthDegrees, resolved.ElevationDegrees, resolved.Projection);
            }

            var fields = new List<(string Name, object Value)>
            {
                ("view", name),
                ("azimuth", azimuth),
                ("elevation", elevation),
                ("projection", projection == CameraProjection.Perspective ? "perspective" : "orthographic"),
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
        ///     Names the view the library used: the viewpoint whose angles it matches, otherwise <c>custom</c>.
        /// </summary>
        /// <param name="resolved">The view the library used.</param>
        /// <returns>The name.</returns>
        private static string NameView(ScadResolvedView resolved)
        {
            const double tolerance = 1e-6;
            var match = ScadViews.FirstOrDefault(v =>
                Math.Abs(v.Elevation - resolved.ElevationDegrees) < tolerance
                && (Math.Abs(Math.Abs(v.Elevation) - 90) < tolerance || Math.Abs(AngleDifference(v.Azimuth, resolved.AzimuthDegrees)) < tolerance));
            return match?.Name ?? "custom";
        }

        /// <summary>
        ///     Gets the signed difference of two angles, wrapped into -180 to 180 degrees.
        /// </summary>
        /// <param name="a">The first angle in degrees.</param>
        /// <param name="b">The second angle in degrees.</param>
        /// <returns>The difference <paramref name="a"/> minus <paramref name="b"/>.</returns>
        private static double AngleDifference(double a, double b)
        {
            var difference = (a - b) % 360.0;
            if (difference > 180.0)
            {
                return difference - 360.0;
            }

            return difference < -180.0 ? difference + 360.0 : difference;
        }

        /// <summary>
        ///     Formats a variable definition as <c>name=value</c>, with a text value in quotes.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="value">The value: a number, a flag, or text.</param>
        /// <returns>The text.</returns>
        private static string FormatDefine(string name, object? value)
        {
            return $"{name}={FormatValue(value)}";
        }

        /// <summary>
        ///     Formats the value of a variable definition: a vector as <c>[a, b, c]</c>, text in quotes.
        /// </summary>
        /// <param name="value">The value: a number, a flag, text, <see langword="null"/>, or a vector of these.</param>
        /// <returns>The text.</returns>
        private static string FormatValue(object? value)
        {
            return value switch
            {
                bool flag => flag ? "true" : "false",
                string content => $"\"{content}\"",
                IFormattable number => number.ToString("R", CultureInfo.InvariantCulture),
                System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(FormatValue)) + "]",
                _ => "undef"
            };
        }
    }
}
