using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <content>
///     The measuring and rendering entry points of the document and the pipeline behind them.
/// </content>
/// <remarks>
///     Pipeline of one call: the reports found while reading are recorded first; the diagram of a supported type is laid
///     out into a scene by its subsystem (<see cref="IPlantUmlDiagram.Layout"/>), whose view box is the natural size; a
///     type that is not drawn reports <see cref="PlantUmlDiagnosticCodes.DiagramUnsupported"/> and gets an empty,
///     transparent scene of 20 by 20. The painter draws the scene. The reports of the call are buffered and delivered
///     once, on the calling thread, after the work.
/// </remarks>
public sealed partial class PlantUmlDocument
{
    /// <summary>The side of the empty picture of a type that is not drawn: twice PlantUML's document margin of 10.</summary>
    internal const int UnsupportedDiagramSize = 20;

    /// <summary>
    ///     Computes the size the diagram renders at without drawing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The result is the size of the surface that <see cref="Render(PlantUmlRenderOptions?, CancellationToken)"/>
    ///         returns for the same options: the natural size (PlantUML's SVG size, rounded up to whole pixels) multiplied
    ///         by <see cref="PlantUmlRenderOptions.Scale"/> and by the diagram's own <c>scale</c>, and rounded. The diagram
    ///         is laid out, and the same warnings and unsupported-feature reports are delivered as for rendering, in the
    ///         same order and on the calling thread, so measuring first never hides a problem that rendering would report.
    ///         Nothing is drawn.
    ///     </para>
    ///     <para>
    ///         The document is immutable, so the method can be called from several threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels of the surface that <c>Render</c> would return.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown when <see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public PlantUmlSize Measure(PlantUmlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new PlantUmlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, _) => ScaledSize(scene, effective.Scale));
    }

    /// <summary>
    ///     Renders the diagram onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="PlantUmlRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with <see cref="PlantUmlRenderOptions.Background"/>, or with the diagram's own
    ///         background when it is null (white, as PlantUML draws it, unless <c>skinparam backgroundColor</c> changes
    ///         it; the empty picture of a type that is not drawn is transparent), and the diagram is drawn on it.
    ///         Rendering is deterministic: two renders of the same document and options have identical pixels, for every
    ///         degree of parallelism and on every .NET version, because only the bundled fonts are used.
    ///     </para>
    ///     <para>
    ///         Warnings and unsupported-feature reports go through the handlers of the options, in the order they were
    ///         found, on the calling thread, once, before the method returns or throws. The caller owns and must dispose the
    ///         returned surface. The document holds no state between calls, so it can be rendered from several threads at
    ///         once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new surface of the size that <see cref="Measure"/> reports.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown when <see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public Surface Render(PlantUmlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new PlantUmlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, context) =>
        {
            var size = ScaledSize(scene, effective.Scale);
            var placement = ScenePlacement.Uniform(scene.ViewBox, effective.Scale * scene.Scale);
            return Paint(scene, size.Width, size.Height, placement, context);
        });
    }

    /// <summary>
    ///     Renders the diagram onto a new surface of an exact size: the natural diagram is scaled uniformly to fit and
    ///     centered over the background.
    /// </summary>
    /// <remarks>
    ///     Use this overload when the destination size is fixed (a page, a slide, a command-line <c>--width</c> and
    ///     <c>--height</c>). The diagram keeps its aspect ratio; the margins are filled with the background.
    ///     <see cref="PlantUmlRenderOptions.Scale"/> and the diagram's <c>scale</c> are not used, because the size decides
    ///     the scale. Events, determinism, ownership and thread safety are as for
    ///     <see cref="Render(PlantUmlRenderOptions?, CancellationToken)"/>. The size is validated before any other work.
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown when <see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public Surface Render(int width, int height, PlantUmlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the cheap arguments before any work
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }

        var effective = options ?? new PlantUmlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, context) =>
            Paint(scene, width, height, ScenePlacement.Fit(scene.ViewBox, width, height), context));
    }

    /// <summary>
    ///     Lays the diagram out under options and returns its scene without drawing it (internal; the scene model the
    ///     tests and the reference comparison read). The reports are delivered as for <see cref="Measure"/>.
    /// </summary>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The scene, whose view box is the natural size.</returns>
    internal Scene LayoutScene(PlantUmlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new PlantUmlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, _) => scene);
    }

    /// <summary>
    ///     Lays the diagram out under options and returns its natural size in whole pixels (the view box rounded up, times
    ///     the diagram's own scale) without the check that it fits a surface (internal; an image source needs the natural
    ///     size, which may be larger than <see cref="Surface.MaxDimension"/>, only for the aspect ratio of a smaller
    ///     requested size). The reports are delivered as for <see cref="Measure"/>.
    /// </summary>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The natural size, each side from 1 to <see cref="int.MaxValue"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded, the layout fails, or the size is not finite.</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown when <see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    internal PlantUmlSize MeasureNatural(PlantUmlRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new PlantUmlRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, _) =>
        {
            var (width, height) = NaturalSize(scene.ViewBox);
            width = Math.Max(1, Math.Round(width * scene.Scale, MidpointRounding.AwayFromZero));
            height = Math.Max(1, Math.Round(height * scene.Scale, MidpointRounding.AwayFromZero));
            if (!double.IsFinite(width) || !double.IsFinite(height))
            {
                throw new PlantUmlRenderException(PlantUmlDiagnosticCodes.RenderFailed, "the diagram has no finite size", Detected.Location);
            }

            return new PlantUmlSize((int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue));
        });
    }

    /// <summary>
    ///     Gets the natural size of a scene: its view box rounded up to whole pixels, at least one pixel.
    /// </summary>
    /// <param name="viewBox">The view box.</param>
    /// <returns>The natural size.</returns>
    internal static (double Width, double Height) NaturalSize(SceneRect viewBox)
    {
        // A tiny tolerance keeps 20.0000000001 from becoming 21
        const double Tolerance = 1e-6;
        static double Side(double value) => double.IsFinite(value) ? Math.Max(1, Math.Ceiling(value - Tolerance)) : double.NaN;
        return (Side(viewBox.Width), Side(viewBox.Height));
    }

    /// <summary>
    ///     Validates that the natural size times the scales is a size a surface can have.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="scale">The scale of the call.</param>
    /// <returns>The scaled size in whole pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a side is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static PlantUmlSize ScaledSize(Scene scene, double scale)
    {
        var (naturalWidth, naturalHeight) = NaturalSize(scene.ViewBox);
        var factor = scale * scene.Scale;
        var width = Math.Round(naturalWidth * factor, MidpointRounding.AwayFromZero);
        var height = Math.Round(naturalHeight * factor, MidpointRounding.AwayFromZero);
        if (!(width >= 1 && width <= Surface.MaxDimension && height >= 1 && height <= Surface.MaxDimension))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                string.Create(CultureInfo.InvariantCulture, $"The scale gives a size of {width} by {height}; each side must be between 1 and {Surface.MaxDimension}."));
        }

        return new PlantUmlSize((int)width, (int)height);
    }

    /// <summary>
    ///     Paints a scene with the painter of the call over the background of the call or of the scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="placement">The placement.</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(Scene scene, int width, int height, ScenePlacement placement, DiagramRenderContext context)
    {
        var painter = new ScenePainter(context.Text, context.Options.Limits.MaxPaintedPixels);
        var background = context.Options.Background ?? scene.Background;
        return painter.Paint(scene, width, height, placement, background, context.CancellationToken);
    }

    /// <summary>
    ///     Runs the pipeline and delivers the recorded reports around the result.
    /// </summary>
    /// <remarks>
    ///     Reports are delivered exactly once on the calling thread. After a failure the reports recorded before it are still
    ///     delivered, but a failing handler cannot hide the failure; after success a failing handler propagates and must not
    ///     leak the result.
    /// </remarks>
    /// <typeparam name="T">The kind of result.</typeparam>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="finish">Produces the result from the scene of the diagram and the context of the call.</param>
    /// <returns>The result.</returns>
    private T Run<T>(PlantUmlRenderOptions options, CancellationToken cancellationToken, Func<Scene, DiagramRenderContext, T> finish)
    {
        var sink = new PlantUmlReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            var context = new DiagramRenderContext(options, sink, new TextLayout(TextLayout.CreateFontResolver()), cancellationToken);
            result = finish(BuildScene(context), context);
        }
        catch
        {
            sink.DeliverIgnoringHandlerFailures(options.WarningHandler, options.UnsupportedFeatureHandler);
            throw;
        }

        try
        {
            sink.Deliver(options.WarningHandler, options.UnsupportedFeatureHandler);
        }
        catch
        {
            (result as IDisposable)?.Dispose();
            throw;
        }

        return result;
    }

    /// <summary>
    ///     Builds the scene of the call: the reports found while reading, then the layout of a supported diagram, or the
    ///     empty scene of a type that is not drawn after its report.
    /// </summary>
    /// <remarks>
    ///     The layout is fail-closed: an exception that the layout does not document (a defect) becomes a
    ///     <see cref="PlantUmlRenderException"/> with the code <see cref="PlantUmlDiagnosticCodes.RenderFailed"/>, located at
    ///     the construct the type was detected by, with the original exception as its inner exception. Cancellation, limit
    ///     and unsupported-feature failures pass unchanged.
    /// </remarks>
    /// <param name="context">The context of the call.</param>
    /// <returns>The scene.</returns>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    private Scene BuildScene(DiagramRenderContext context)
    {
        // The reports of reading come first: the preprocessor's, then the parser's, each in text order (strict mode
        // throws at the first unsupported one)
        foreach (var report in Source.Reports.Concat(Diagram?.Reports ?? []))
        {
            report.Replay(context.Sink);
        }

        if (Diagram is not null)
        {
            try
            {
                return Diagram.Layout(context);
            }
            catch (Exception ex) when (IsLayoutDefect(ex))
            {
                throw new PlantUmlRenderException(
                    PlantUmlDiagnosticCodes.RenderFailed,
                    $"the layout failed unexpectedly ({ex.GetType().Name}: {ex.Message})",
                    Detected.Location,
                    "simplify the construct the diagram fails at, and report the diagram to the CanvasNet project",
                    ex);
            }
        }

        // A known type that is not drawn: one report (strict mode throws here) and nothing to draw
        context.Sink.Unsupported(
            PlantUmlDiagnosticCodes.DiagramUnsupported,
            $"'{Detected.DiagramType}' diagrams are not drawn by this version; the picture is empty",
            Detected.Location,
            DiagramDetector.UnsupportedHint(Detected));
        return new Scene(new SceneRect(0, 0, UnsupportedDiagramSize, UnsupportedDiagramSize), []);
    }

    /// <summary>
    ///     Decides whether an exception that escaped the layout is a defect to report as a located render failure: one of
    ///     the runtime's programming-error exceptions, never the documented failures
    ///     (<see cref="PlantUmlRenderException"/>, <see cref="PlantUmlUnsupportedFeatureException"/>) nor
    ///     <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>True when the exception is a defect of the layout.</returns>
    internal static bool IsLayoutDefect(Exception ex) =>
        ex is NullReferenceException
            or IndexOutOfRangeException
            or KeyNotFoundException
            or InvalidCastException
            or ArgumentException
            or ArithmeticException
            or (InvalidOperationException and not PlantUmlRenderException and not ObjectDisposedException);
}
