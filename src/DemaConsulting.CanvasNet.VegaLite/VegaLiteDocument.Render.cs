// Portions derived from Vega-Lite (BSD-3-Clause): the default view size and padding come from the configuration; see third-party/README.md
// cspell:ignore hconcat scenegraph vconcat
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The measuring and rendering entry points of the document and the (foundation-shell) pipeline behind them.
/// </content>
/// <remarks>
///     Foundation pipeline: the specification model reports the constructs that it recognizes but cannot draw yet, and
///     the view layout turns the title, the padding, the size and the background of the view into a scene that the
///     painter draws (a view without marks or axes: its plot area, border and title). The private pipeline
///     methods at the end of this file are the seam that the data, scale, mark and guide stages extend; the public
///     methods, their validation order and their event delivery are the stable contract.
/// </remarks>
public sealed partial class VegaLiteDocument
{
    /// <summary>
    ///     Computes the size the chart renders at without drawing it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The result is the size of the surface that <see cref="Render(VegaLiteRenderOptions?, CancellationToken)"/>
    ///         returns for the same options: the natural size of the chart (its data rectangle, axes, legends, title
    ///         and padding) multiplied by <see cref="VegaLiteRenderOptions.Scale"/> and rounded to whole pixels.
    ///         The same checks run and the same warnings and unsupported-feature reports are delivered as for
    ///         rendering, in the same order and on the calling thread, so measuring first never hides a problem that
    ///         rendering would report. Nothing is drawn.
    ///     </para>
    ///     <para>
    ///         The scaled size is validated before any other work. The document is immutable, so the method can be
    ///         called from several threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels of the surface that <c>Render</c> would return.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when the chart cannot be laid out.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public VegaLiteSize Measure(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var plan = PrepareChart(effective);
        var size = ValidateScaledSize(plan.Size, effective);
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, plan, _ => size);
    }

    /// <summary>
    ///     Renders the chart onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="VegaLiteRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with the background (<see cref="VegaLiteRenderOptions.Background"/>, else white)
    ///         and the chart is drawn on it. Rendering is deterministic: two renders of the same document and options
    ///         are byte-identical, for every degree of parallelism and on every machine, because only the bundled
    ///         fonts are used unless <see cref="VegaLiteRenderOptions.Fonts"/> says otherwise.
    ///     </para>
    ///     <para>
    ///         Warnings and unsupported-feature reports go through the handlers of the options, in the order they
    ///         were found, on the calling thread, once, before the method returns or throws. The scaled size and the
    ///         parameter names are validated before any other work. The caller owns and must dispose the returned
    ///         surface; it is not retained by the document.
    ///     </para>
    ///     <para>
    ///         The document is immutable and holds no state between calls, so it can be rendered from several
    ///         threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new surface of the size that <see cref="Measure"/> reports.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when data is missing or invalid or a limit is exceeded.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public Surface Render(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var plan = PrepareChart(effective);
        var size = ValidateScaledSize(plan.Size, effective);
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(effective, cancellationToken, plan, p => Paint(p, size.Width, size.Height, ScenePlacement.Uniform(effective.Scale), cancellationToken));
    }

    /// <summary>
    ///     Renders the chart onto a new surface of an exact size: the natural chart is scaled uniformly to fit and
    ///     centered over the background.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this overload when the destination size is fixed (a page, a slide, a command-line
    ///         <c>--width</c>/<c>--height</c>). The chart keeps its aspect ratio: it is scaled by the largest factor
    ///         that fits inside <paramref name="width"/> by <paramref name="height"/> and centered, and the margins
    ///         that remain are filled with the background (<see cref="VegaLiteRenderOptions.Background"/>, else
    ///         white). <see cref="VegaLiteRenderOptions.Scale"/> is not used, because the size decides the scale.
    ///     </para>
    ///     <para>
    ///         Events, determinism, ownership and thread safety are as for
    ///         <see cref="Render(VegaLiteRenderOptions?, CancellationToken)"/>. The size and the parameter names are
    ///         validated before any other work.
    ///     </para>
    /// </remarks>
    /// <param name="width">The width in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A new <paramref name="width"/> by <paramref name="height"/> surface.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when data is missing or invalid or a limit is exceeded.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public Surface Render(int width, int height, VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Validate the cheap arguments before any work: the size first, then the options
        if (width < 1 || width > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"The width must be between 1 and {Surface.MaxDimension}.");
        }

        if (height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"The height must be between 1 and {Surface.MaxDimension}.");
        }

        var effective = options ?? new VegaLiteRenderOptions();
        ValidateParameters(effective);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = PrepareChart(effective);
        var placement = ScenePlacement.Fit(plan.Size.Width, plan.Size.Height, width, height);
        return Run(effective, cancellationToken, plan, p => Paint(p, width, height, placement, cancellationToken));
    }

    /// <summary>
    ///     Runs the pipeline and delivers the recorded events around the result.
    /// </summary>
    /// <remarks>
    ///     Events are delivered exactly once on the calling thread. After a failure the events recorded before it are
    ///     still delivered, but a failing handler cannot hide the failure; after success a failing handler
    ///     propagates and must not leak the result.
    /// </remarks>
    /// <typeparam name="T">The kind of result.</typeparam>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="plan">The plan of the chart.</param>
    /// <param name="finish">Produces the result from the plan of the chart.</param>
    /// <returns>The result.</returns>
    private T Run<T>(VegaLiteRenderOptions options, CancellationToken cancellationToken, ChartPlan plan, Func<ChartPlan, T> finish)
    {
        var sink = new VegaLiteReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            result = finish(BuildPlan(plan, sink, cancellationToken));
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
    ///     Validates that the natural size times the scale is a size a surface can have.
    /// </summary>
    /// <param name="natural">The natural size of the chart.</param>
    /// <param name="options">The effective options.</param>
    /// <returns>The scaled size in whole pixels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a side is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static VegaLiteSize ValidateScaledSize(VegaLiteSize natural, VegaLiteRenderOptions options)
    {
        var width = ScaleDimension(natural.Width, options.Scale);
        var height = ScaleDimension(natural.Height, options.Scale);
        if (width < 1 || width > Surface.MaxDimension || height < 1 || height > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Scale,
                $"The scale gives a size of {width} by {height}; each side must be between 1 and {Surface.MaxDimension}.");
        }

        return new VegaLiteSize((int)width, (int)height);
    }

    /// <summary>
    ///     Multiplies a dimension by the scale and rounds half away from zero.
    /// </summary>
    /// <param name="dimension">The natural dimension.</param>
    /// <param name="scale">The scale factor.</param>
    /// <returns>The scaled dimension, as a double so that huge results cannot overflow.</returns>
    private static double ScaleDimension(int dimension, double scale) =>
        Math.Round(dimension * scale, MidpointRounding.AwayFromZero);

    /// <summary>
    ///     Checks that every parameter override names a parameter the document declares.
    /// </summary>
    /// <param name="options">The effective options.</param>
    /// <exception cref="ArgumentException">Thrown when an override names an unknown parameter.</exception>
    private void ValidateParameters(VegaLiteRenderOptions options)
    {
        if (options.Parameters is null)
        {
            return;
        }

        // Ordinal order makes the reported name independent of the caller's dictionary order
        var known = new HashSet<string>(ParameterNames, StringComparer.Ordinal);
        var unknown = options.Parameters.Keys.Where(name => !known.Contains(name)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
        {
            throw new ArgumentException($"The specification declares no parameter named '{unknown}'.", nameof(options));
        }
    }

    /// <summary>
    ///     Lays out the chart: resolves the configuration, builds the title and the view, and gives the scene and the
    ///     natural size of the chart (the size of the surface at a scale of one).
    /// </summary>
    /// <remarks>
    ///     This is pure: it records nothing and delivers no event, so the size can be validated before any other work.
    ///     The data rectangle comes from <c>width</c>, <c>height</c> and the view configuration (300 by 300 in Vega-Lite 6),
    ///     the canvas from the padding and <c>autosize</c>, and the title and background from the specification and the
    ///     configuration; the caller's background replaces the other two. The configuration is resolved here, so a
    ///     configuration the options cannot hold (not a JSON object) fails the call like any other option error.
    /// </remarks>
    /// <param name="options">The effective options.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Config"/> is not a JSON object (it is checked when the options are created, so this only guards against a text that did not pass that check).</exception>
    private ChartPlan PrepareChart(VegaLiteRenderOptions options)
    {
        // Defaults < specification config < caller config
        var config = ConfigResolver.Resolve(_tree, options.Config);
        var measurer = new TextMeasurer(options.EffectiveFonts);
        var root = _tree.Root;
        var hints = new ViewSizeHints(ContainerWidth: options.ContainerWidth, ContainerHeight: options.ContainerHeight);
        var background = options.Background is { } caller ? CssColor.ToCss(caller) : null;
        var request = ViewLayout.Resolve(root, config, hints, background);

        // A composition has no single title position; only single and layered views are drawn so far
        var isComposition = new[] { "concat", "hconcat", "vconcat", "facet", "repeat" }.Any(name => root[name] is not null);
        var title = new TitleBuilder(measurer).Build(root["title"], config, unitOrLayer: !isComposition);
        var layout = ViewLayout.Compute(request, default, title);
        return new ChartPlan(layout.Measure.NaturalSize, layout.Scene, measurer);
    }

    /// <summary>
    ///     Builds the plan of the chart for delivery: the reports about what cannot be drawn go to the sink.
    /// </summary>
    /// <remarks>
    ///     The reports are the findings of the specification model (parse warnings, then the feature catalog) in
    ///     document order. Constructs that the renderer will draw are not reported; those that it recognizes but cannot
    ///     draw yet are, with their stable identifiers.
    /// </remarks>
    /// <param name="plan">The prepared plan.</param>
    /// <param name="sink">The sink that records the reports.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildPlan(ChartPlan plan, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Deliver the findings of the text in document order; strict mode throws at the first unsupported feature
        foreach (var report in Reports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (report.Kind == FeatureReportKind.Warning)
            {
                sink.Warn($"{report.Id}: {report.Message}", report.Location);
            }
            else
            {
                sink.Unsupported(report.Id, report.Message, report.Location);
            }
        }

        return plan;
    }

    /// <summary>
    ///     Paints the plan onto a new surface of the given size.
    /// </summary>
    /// <remarks>
    ///     The scene is drawn at the placement: a uniform scale and an offset, so that an explicit size fits the natural
    ///     chart and centers it over the background, which also fills the margins.
    /// </remarks>
    /// <param name="plan">The plan.</param>
    /// <param name="width">The width of the surface in pixels.</param>
    /// <param name="height">The height of the surface in pixels.</param>
    /// <param name="placement">Where the scene goes on the surface.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The surface; the caller owns it.</returns>
    private static Surface Paint(ChartPlan plan, int width, int height, ScenePlacement placement, CancellationToken cancellationToken) =>
        new ScenePainter(plan.Measurer).Paint(plan.Scene, width, height, placement, cancellationToken);

    /// <summary>
    ///     What the painter needs to draw the chart.
    /// </summary>
    /// <param name="Size">The natural size of the chart in pixels at a scale of one.</param>
    /// <param name="Scene">The scene: the canvas, its background and everything drawn on it.</param>
    /// <param name="Measurer">The text measurer with the fonts of this render.</param>
    private readonly record struct ChartPlan(VegaLiteSize Size, Scene Scene, TextMeasurer Measurer);
}
