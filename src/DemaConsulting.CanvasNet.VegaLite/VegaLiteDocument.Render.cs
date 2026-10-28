// Portions derived from Vega-Lite (BSD-3-Clause): the default view size and padding come from the configuration; see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The measuring and rendering entry points of the document and the pipeline behind them.
/// </content>
/// <remarks>
///     Pipeline: the specification model reports the constructs that it recognizes but cannot draw; for a single view
///     with a mark the inline data is loaded, the scales are resolved, the axes, the marks and the title are built, the view
///     layout sizes the canvas around them and places everything in a scene, and the painter draws the scene. A composition
///     (layer, concat, facet, repeat) is not drawn yet: only its title and background are. The private pipeline
///     methods at the end of this file are the seam that later stages extend; the public methods, their validation order and
///     their event delivery are the stable contract.
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
    ///         The chart is laid out first (its size depends on the data and the axes); the scaled size and the parameter
    ///         names are then validated before any report is delivered. A specification or data error (an invalid axis
    ///         format, a data or scene limit) is thrown with its location after the reports found before it were
    ///         delivered. The document is immutable, so the method can be called from several threads at once.
    ///     </para>
    /// </remarks>
    /// <param name="options">Optional render options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>The size in pixels of the surface that <c>Render</c> would return.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scaled size is outside 1 to <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Parameters"/> names a parameter that the document does not declare.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before or during the work.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the construct, when the chart cannot be laid out.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the construct, when an axis, scale or format property has an invalid value.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public VegaLiteSize Measure(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var plan = PrepareChartOrReport(effective, cancellationToken);
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
    ///         were found, on the calling thread, once, before the method returns or throws. The chart is laid out first
    ///         (its size depends on the data and the axes); the scaled size and the
    ///         parameter names are then validated before any report is delivered. The caller owns and must dispose the returned
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
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the construct, when an axis, scale or format property has an invalid value.</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown when <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> is set and the specification uses an unsupported feature.</exception>
    public Surface Render(VegaLiteRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = options ?? new VegaLiteRenderOptions();
        var plan = PrepareChartOrReport(effective, cancellationToken);
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
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the construct, when an axis, scale or format property has an invalid value.</exception>
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
        var plan = PrepareChartOrReport(effective, cancellationToken);
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
    ///     Lays out the chart: resolves the configuration, loads the data and builds the scales, axes, marks and title of a
    ///     single view, and gives the scene and the natural size of the chart (the size of the surface at a scale of one).
    /// </summary>
    /// <remarks>
    ///     This is pure: it records nothing and delivers no event, so the size can be validated before any other work.
    ///     The data rectangle comes from <c>width</c>, <c>height</c>, the view configuration (300 by 300 in Vega-Lite 6) or the
    ///     step of a discrete scale, the canvas from the padding, <c>autosize</c> and the room the axes and the title need, and
    ///     the title and background from the specification and the configuration; the caller's background replaces the other
    ///     two. The configuration is resolved here, so a configuration the options cannot hold (not a JSON object) fails the
    ///     call like any other option error. Only a single view with a mark has data, scales, axes and marks; compositions are
    ///     not drawn yet.
    /// </remarks>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Config"/> is not a JSON object (it is checked when the options are created, so this only guards against a text that did not pass that check).</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location, when a scale, axis or format property has an invalid value.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location, when the data or the chart exceeds a limit.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled during the work.</exception>
    private ChartPlan PrepareChart(VegaLiteRenderOptions options, CancellationToken cancellationToken)
    {
        // Defaults < specification config < caller config
        var config = ConfigResolver.Resolve(_tree, options.Config);
        var measurer = new TextMeasurer(options.EffectiveFonts);
        var root = _tree.Root;

        // A composition has no single title position; layers share one. Only single views have a data rectangle drawn so far
        var isComposition = new[] { "concat", "hconcat", "vconcat", "facet", "repeat" }.Any(name => root[name] is not null);
        DataTable? table = null;
        ResolvedScales? scales = null;
        var markNode = root["mark"];
        var markType = markNode is { IsObject: true } ? markNode["type"]?.AsString() : markNode?.AsString();
        if (!isComposition && root["layer"] is null && MarkBuilder.IsSupported(markType))
        {
            table = DataLoader.Load(root["data"], _tree.SourceName, options.Limits, cancellationToken);
            scales = ScaleResolver.Resolve(root, table, config, _tree.SourceName, options.Limits, cancellationToken);
        }

        var hints = new ViewSizeHints(scales?.DiscreteWidth, scales?.DiscreteHeight, options.ContainerWidth, options.ContainerHeight);
        var background = options.Background is { } caller ? CssColor.ToCss(caller) : null;
        var request = ViewLayout.Resolve(root, config, hints, background);
        var title = new TitleBuilder(measurer).Build(root["title"], config, unitOrLayer: !isComposition);
        if (table is null || scales is null)
        {
            var layout = ViewLayout.Compute(request, default, title);
            return new ChartPlan(layout.Measure.NaturalSize, layout.Scene, measurer);
        }

        return BuildChart(new ChartInputs(request, title, scales, table, config, measurer, options.Limits), cancellationToken);
    }

    /// <summary>
    ///     Builds the axes and the marks of a single view and lays the chart out around them.
    /// </summary>
    /// <remarks>
    ///     The axes need the size of the data rectangle (tick counts, positions) and the data rectangle can depend on the
    ///     axes (a <c>"container"</c> size leaves the room the axes do not need), so the two are iterated until the rectangle
    ///     does not change; for a fixed size the first pass is final. The marks are built once, for the final rectangle. The
    ///     axes come first in the view group, so that they are drawn behind the marks (Vega-Lite's default axis z-index).
    /// </remarks>
    /// <param name="inputs">What the stages need.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildChart(ChartInputs inputs, CancellationToken cancellationToken)
    {
        var sourceName = _tree.SourceName;
        var budget = new SceneBudget(inputs.Limits.MaxSceneItems);
        var axisBuilder = new AxisBuilder(inputs.Measurer, inputs.Config, inputs.Limits, sourceName);
        var measure = ViewLayout.Measure(inputs.Request, default, inputs.Title);
        var axes = new List<AxisResult>();
        for (var pass = 0; pass < 3; pass++)
        {
            axes = BuildAxes(inputs, axisBuilder, measure.DataWidth, measure.DataHeight, cancellationToken);
            var extents = AxisBuilder.GetExtents(axes, measure.DataWidth, measure.DataHeight);
            var next = ViewLayout.Measure(inputs.Request, extents, inputs.Title);
            var stable = Math.Abs(next.DataWidth - measure.DataWidth) < 1e-9 && Math.Abs(next.DataHeight - measure.DataHeight) < 1e-9;
            measure = next;
            if (stable)
            {
                break;
            }
        }

        var location = _tree.Root["encoding"]?.GetLocation(sourceName) ?? default;
        var content = new List<SceneItem>(axes.Count * 2 + 1);
        foreach (var axis in axes)
        {
            budget.Add(axis.ItemCount + 2, location);
        }

        // Vega-Lite draws the grids first, then the axes that are behind the marks, the marks, and last the axes in front of them
        content.AddRange(axes.Where(a => a.Grid is not null).Select(a => (SceneItem)a.Grid!));
        content.AddRange(axes.Where(a => !a.InFront).Select(a => a.Group));
        var marks = new MarkBuilder(inputs.Config, inputs.Limits, budget, sourceName)
            .Build(_tree.Root, inputs.Table, inputs.Scales, measure.DataWidth, measure.DataHeight, cancellationToken);
        content.Add(marks);
        content.AddRange(axes.Where(a => a.InFront).Select(a => a.Group));
        return new ChartPlan(measure.NaturalSize, ViewLayout.Build(inputs.Request, measure, content), inputs.Measurer);
    }

    /// <summary>
    ///     Builds the axes of the position channels that have a scale, x first.
    /// </summary>
    /// <param name="inputs">What the stages need.</param>
    /// <param name="builder">The axis builder.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The axes; empty when no position channel has a scale or all axes are switched off.</returns>
    private List<AxisResult> BuildAxes(ChartInputs inputs, AxisBuilder builder, double width, double height, CancellationToken cancellationToken)
    {
        var axes = new List<AxisResult>(2);
        foreach (var channel in new[] { "x", "y" })
        {
            if (inputs.Scales[channel] is not { } scale || _tree.Root["encoding"]?[channel] is not { IsObject: true } definition)
            {
                continue;
            }

            var model = scale.CreateModel(channel == "x" ? width : height);
            if (builder.Build(new AxisRequest(channel, definition, scale, model, width, height, inputs.Scales.MarkType), cancellationToken) is { } axis)
            {
                axes.Add(axis);
            }
        }

        return axes;
    }

    /// <summary>
    ///     Prepares the chart; when that fails because of the specification or the data, delivers the reports that were
    ///     found before the failure, so that the caller sees them together with the error.
    /// </summary>
    /// <remarks>
    ///     A cancellation delivers nothing. In strict mode the first unsupported feature wins over a later error, as it does
    ///     for a chart that renders.
    /// </remarks>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan PrepareChartOrReport(VegaLiteRenderOptions options, CancellationToken cancellationToken)
    {
        try
        {
            return PrepareChart(options, cancellationToken);
        }
        catch (Exception ex) when (ex is VegaLiteSpecException or VegaLiteRenderException)
        {
            var sink = new VegaLiteReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
            try
            {
                RecordReports(sink, cancellationToken);
            }
            catch (VegaLiteUnsupportedFeatureException)
            {
                sink.DeliverIgnoringHandlerFailures(options.WarningHandler, options.UnsupportedFeatureHandler);
                throw;
            }

            sink.DeliverIgnoringHandlerFailures(options.WarningHandler, options.UnsupportedFeatureHandler);
            throw;
        }
    }

    /// <summary>
    ///     Records the findings of the specification model in the sink, in document order.
    /// </summary>
    /// <param name="sink">The sink.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    private void RecordReports(VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
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
    }

    /// <summary>
    ///     Builds the plan of the chart for delivery: the reports about what cannot be drawn go to the sink.
    /// </summary>
    /// <remarks>
    ///     The reports are the findings of the specification model (parse warnings, then the feature catalog) in
    ///     document order. Constructs that the renderer draws are not reported; those that it recognizes but cannot
    ///     draw yet are, with their stable identifiers.
    /// </remarks>
    /// <param name="plan">The prepared plan.</param>
    /// <param name="sink">The sink that records the reports.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildPlan(ChartPlan plan, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordReports(sink, cancellationToken);
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

    /// <summary>
    ///     What the data, scale, axis and mark stages of one view share.
    /// </summary>
    /// <param name="Request">The layout request of the view.</param>
    /// <param name="Title">The built title, or null.</param>
    /// <param name="Scales">The resolved scales.</param>
    /// <param name="Table">The data.</param>
    /// <param name="Config">The effective configuration.</param>
    /// <param name="Measurer">The text measurer.</param>
    /// <param name="Limits">The limits.</param>
    private sealed record ChartInputs(
        ViewLayoutRequest Request,
        TitleBlock? Title,
        ResolvedScales Scales,
        DataTable Table,
        ResolvedConfig Config,
        TextMeasurer Measurer,
        VegaLiteLimits Limits);
}
