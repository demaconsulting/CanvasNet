// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <content>
///     The measuring and rendering entry points of the document and the pipeline behind them.
/// </content>
/// <remarks>
///     Pipeline of one call: the diagram of a supported type is laid out into a scene by its subsystem
///     (<see cref="IMermaidDiagram.Layout"/>), whose view box is the natural size; a type that is not drawn reports
///     <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> and gets an empty scene of 16 by 16. The painter draws the
///     scene. The reports of the call are buffered and delivered once, on the calling thread, after the work.
/// </remarks>
public sealed partial class MermaidDocument
{
    /// <summary>The side of the empty picture of a type that is not drawn: twice Mermaid's default <c>diagramPadding</c> of 8.</summary>
    internal const int UnsupportedDiagramSize = 16;

    /// <summary>
    ///     Computes the size the diagram renders at without drawing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The result is the size of the surface that <see cref="Render(MermaidRenderOptions?, CancellationToken)"/>
    ///         returns for the same options: the natural size (Mermaid's SVG view box, rounded up to whole pixels as
    ///         <c>mmdc</c> does) multiplied by <see cref="MermaidRenderOptions.Scale"/> and rounded. The diagram is laid out,
    ///         and the same warnings and unsupported-feature reports are delivered as for rendering, in the same order and on
    ///         the calling thread, so measuring first never hides a problem that rendering would report. Nothing is drawn.
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
    /// <exception cref="MermaidRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown when <see cref="MermaidRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public MermaidSize Measure(MermaidRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MermaidRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, _) => ScaledSize(scene, effective.Scale));
    }

    /// <summary>
    ///     Renders the diagram onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="MermaidRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with <see cref="MermaidRenderOptions.Background"/> (transparent when it is null, as
    ///         Mermaid's SVG is) and the diagram is drawn on it. Rendering is deterministic: two renders of the same document
    ///         and options have identical pixels, for every degree of parallelism and on every .NET version, because only the
    ///         bundled fonts are used.
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
    /// <exception cref="MermaidRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown when <see cref="MermaidRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public Surface Render(MermaidRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new MermaidRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, context) =>
        {
            var size = ScaledSize(scene, effective.Scale);
            return Paint(scene, size.Width, size.Height, ScenePlacement.Uniform(scene.ViewBox, effective.Scale), context);
        });
    }

    /// <summary>
    ///     Renders the diagram onto a new surface of an exact size: the natural diagram is scaled uniformly to fit and
    ///     centered over the background.
    /// </summary>
    /// <remarks>
    ///     Use this overload when the destination size is fixed (a page, a slide, a command-line <c>--width</c>/<c>--height</c>).
    ///     The diagram keeps its aspect ratio; the margins are filled with the background.
    ///     <see cref="MermaidRenderOptions.Scale"/> is not used, because the size decides the scale. Events, determinism,
    ///     ownership and thread safety are as for <see cref="Render(MermaidRenderOptions?, CancellationToken)"/>. The size
    ///     is validated before any other work.
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="MermaidRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown when <see cref="MermaidRenderOptions.ThrowOnUnsupportedFeature"/> is set and the diagram uses an unsupported feature (or its type is not drawn).</exception>
    public Surface Render(int width, int height, MermaidRenderOptions? options = null, CancellationToken cancellationToken = default)
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

        var effective = options ?? new MermaidRenderOptions();
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, (scene, context) =>
            Paint(scene, width, height, ScenePlacement.Fit(scene.ViewBox, width, height), context));
    }

    /// <summary>
    ///     Gets the natural size of a scene: its view box rounded up to whole pixels (as <c>mmdc</c> rounds the bounding box
    ///     of the SVG), at least one pixel.
    /// </summary>
    /// <param name="viewBox">The view box.</param>
    /// <returns>The natural size.</returns>
    internal static (double Width, double Height) NaturalSize(SceneRect viewBox)
    {
        // A tiny tolerance keeps 16.0000000001 from becoming 17
        const double Tolerance = 1e-6;
        static double Side(double value) => double.IsFinite(value) ? Math.Max(1, Math.Ceiling(value - Tolerance)) : double.NaN;
        return (Side(viewBox.Width), Side(viewBox.Height));
    }

    /// <summary>
    ///     Validates that the natural size times the scale is a size a surface can have.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The scaled size in whole pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a side is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static MermaidSize ScaledSize(Scene scene, double scale)
    {
        var (naturalWidth, naturalHeight) = NaturalSize(scene.ViewBox);
        var width = Math.Round(naturalWidth * scale, MidpointRounding.AwayFromZero);
        var height = Math.Round(naturalHeight * scale, MidpointRounding.AwayFromZero);
        if (!(width >= 1 && width <= Surface.MaxDimension && height >= 1 && height <= Surface.MaxDimension))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                string.Create(CultureInfo.InvariantCulture, $"The scale gives a size of {width} by {height}; each side must be between 1 and {Surface.MaxDimension}."));
        }

        return new MermaidSize((int)width, (int)height);
    }

    /// <summary>
    ///     Paints a scene with the painter of the call.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <param name="placement">The placement.</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The surface.</returns>
    private static Surface Paint(Scene scene, int width, int height, ScenePlacement placement, DiagramRenderContext context)
    {
        var painter = new ScenePainter(context.Fonts, images: null, context.Options.Limits.MaxPaintedPixels);
        return painter.Paint(scene, width, height, placement, context.Options.Background, context.CancellationToken);
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
    private T Run<T>(MermaidRenderOptions options, CancellationToken cancellationToken, Func<Scene, DiagramRenderContext, T> finish)
    {
        var sink = new MermaidReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            var context = new DiagramRenderContext(Source, Detected, options, sink, ScenePainter.CreateFontResolver(), cancellationToken);
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
    ///     Builds the scene of the call: the layout of a supported diagram, or the empty scene of a type that is not drawn
    ///     after its report.
    /// </summary>
    /// <param name="context">The context of the call.</param>
    /// <returns>The scene.</returns>
    private Scene BuildScene(DiagramRenderContext context)
    {
        if (Diagram is not null)
        {
            return Diagram.Layout(context);
        }

        // A known type that is not drawn: one report (strict mode throws here) and nothing to draw
        context.Sink.Unsupported(
            MermaidDiagnosticCodes.DiagramUnsupported,
            $"'{Detected.Keyword}' diagrams ({Detected.DiagramType}) are not drawn by this version; the picture is empty",
            Detected.Location,
            DiagramDetector.UnsupportedHint(Detected));
        return new Scene(new SceneRect(0, 0, UnsupportedDiagramSize, UnsupportedDiagramSize), []);
    }
}
