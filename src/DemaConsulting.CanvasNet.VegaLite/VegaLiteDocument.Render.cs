// Portions derived from Vega-Lite (BSD-3-Clause): the default view size and padding come from the configuration; see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The measuring and rendering entry points of the document and the pipeline behind them.
/// </content>
/// <remarks>
///     Pipeline: the specification model reports the constructs that it recognizes but cannot draw; for a single view
///     with a mark the data (inline rows, a local file, a named dataset or a sequence) is loaded first, under the limits and
///     the file access of this call, then the static parameters are evaluated and the row transforms (calculate, filter, fold,
///     flatten, sample, pivot, lookup, aggregate, joinaggregate, window, bin, stack, impute) run in the order of the specification, the
///     encoding's own bin, aggregate and stack run on the result (Vega-Lite's implicit stage, see <see cref="EncodingTransforms"/>), the scales are resolved over the
///     Raw and Main tables that stage produced,
///     the axes, the legends, the marks and the title are built, the view
///     layout sizes the canvas around them and places everything in a scene, and the painter draws the scene. A layered view (<c>layer</c>) is drawn layer by layer over the data and
///     transforms it inherits (see <c>VegaLiteDocument.Layers.cs</c>); a concatenation, a repeat or a facet is drawn chart by chart and laid out in a grid (see <c>VegaLiteDocument.Composition.cs</c>;
///     a facet splits its rows into cells first). The private pipeline
///     methods at the end of this file are the seam that later stages extend; the public methods, their validation order and
///     their event delivery are the stable contract. The findings of the data stage (parse failures, then the warnings of a
///     lookup source and of the transforms) are delivered after the reports of the specification.
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
        var placement = ScenePlacement.Uniform(effective.Scale);
        return Run(effective, cancellationToken, plan, placement, (_, _) => size);
    }

    /// <summary>
    ///     Renders the chart onto a new <see cref="Surface"/> at its natural size multiplied by
    ///     <see cref="VegaLiteRenderOptions.Scale"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The surface is filled with the background (<see cref="VegaLiteRenderOptions.Background"/>, else white)
    ///         and the chart is drawn on it. Rendering is deterministic: two renders of the same document and options
    ///         have identical pixels (an encoded PNG file is not byte-stable across .NET versions, so compare pixels), for every degree of parallelism and on every .NET version, because only the bundled
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
        var placement = ScenePlacement.Uniform(effective.Scale);
        return Run(effective, cancellationToken, plan, placement, (p, sink) => Paint(p, size.Width, size.Height, placement, effective, sink, cancellationToken));
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
        return Run(effective, cancellationToken, plan, placement, (p, sink) => Paint(p, width, height, placement, effective, sink, cancellationToken));
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
    /// <param name="placement">Where the scene goes on the surface; the missing-glyph report uses its scale, the same one the painter uses.</param>
    /// <param name="finish">Produces the result from the plan of the chart; the sink records what the painting finds (an unresolved picture).</param>
    /// <returns>The result.</returns>
    private T Run<T>(VegaLiteRenderOptions options, CancellationToken cancellationToken, ChartPlan plan, ScenePlacement placement, Func<ChartPlan, VegaLiteReportSink, T> finish)
    {
        var sink = new VegaLiteReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
        T result;
        try
        {
            result = finish(BuildPlan(plan, options, placement, sink, cancellationToken), sink);
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
    ///     Checks that the canvas of a laid-out chart has a size that can be drawn.
    /// </summary>
    /// <remarks>
    ///     The size grows with the reach of the guides, which is computed from scale positions: a scale whose values overflow (a <c>pow</c> scale with an
    ///     exponent of <c>1e300</c> maps every tick to NaN) would otherwise reach the scene as a NaN width and fail without saying where. The failure names the root of the specification.
    /// </remarks>
    /// <param name="measure">The measurements of the chart.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the width or the height of the canvas is not a finite number.</exception>
    private void RequireFiniteSize(ViewMeasure measure)
    {
        if (!double.IsFinite(measure.Width) || !double.IsFinite(measure.Height))
        {
            throw new VegaLiteRenderException(
                "The chart has a size that is not a finite number: a scale or an axis produced positions that overflow (for example a pow scale with a huge exponent), or a padding or a size is too large for a double, so the canvas cannot be sized.",
                _tree.Root.GetLocation(_tree.SourceName));
        }
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
    ///     Lays the chart out and returns its scene, without painting or delivering reports (internal; for tests that
    ///     count the scene items against <see cref="VegaLiteLimits.MaxSceneItems"/>).
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The scene.</returns>
    internal Scene BuildScene(VegaLiteRenderOptions options, CancellationToken cancellationToken = default) =>
        PrepareChart(options, cancellationToken).Scene;

    /// <summary>
    ///     Lays out the chart: evaluates the parameters and folds the expression references of the static sites into values, resolves the configuration, loads the data and builds the scales, axes, marks and title of a
    ///     single view, and gives the scene and the natural size of the chart (the size of the surface at a scale of one).
    /// </summary>
    /// <remarks>
    ///     This is pure: it records nothing and delivers no event, so the size can be validated before any other work.
    ///     The data rectangle comes from <c>width</c>, <c>height</c>, the view configuration (300 by 300 in Vega-Lite 6) or the
    ///     step of a discrete scale, the canvas from the padding, <c>autosize</c> and the room the axes and the title need, and
    ///     the title and background from the specification and the configuration; the caller's background replaces the other
    ///     two. The configuration is resolved here, so a configuration the options cannot hold (not a JSON object) fails the
    ///     call like any other option error. Only a view with a mark, or the layers of a layered view, or the charts of a concatenation, a facet or a repeat, have data, scales, axes and marks.
    ///     A tree whose root is a concatenation or a facet is drawn by <see cref="BuildComposition"/>; a single or layered view goes through <see cref="BuildChart"/>.
    /// </remarks>
    /// <param name="options">The effective options.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="dataWarningSink">Receives the warnings of the data stage as soon as the data is loaded, so that a later failure can still report them; null to not collect them.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Thrown when <see cref="VegaLiteRenderOptions.Config"/> is not a JSON object (it is checked when the options are created, so this only guards against a text that did not pass that check).</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location, when a scale, axis or format property has an invalid value.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location, when the data or the chart exceeds a limit (or a parameter or an expression reference exceeds a limit of the evaluation).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled during the work.</exception>
    private ChartPlan PrepareChart(VegaLiteRenderOptions options, CancellationToken cancellationToken, List<FeatureReport>? dataWarningSink = null)
    {
        // The parameters are evaluated first, once, because everything after reads them: the expression references of the static sites are replaced by their values
        // (so the configuration, the title, the scales and the guides see values, never expressions), and the data stage shares the evaluator (the step budget) and the calendar
        var calendar = TimeCalendar.Create(options.TimeZone);
        var parameters = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(options.Limits, cancellationToken, parameters, calendar);

        // The selections come first: a parameter expression and an expression reference can read a selection, and the predicates of the conditions and filters read the stores. The
        // model needs the views of the specification (the units and their encodings), not its data, so it is built from the specification as written and only when it declares a selection
        var selections = SelectionModel.Empty;
        if (SelectionModel.Declares(_tree))
        {
            var selectionConfig = ConfigResolver.Resolve(_tree, options.Config);
            var selectionLayers = SpecNormalizer.Expand(_tree, selectionConfig, options.Limits).Layers;
            selections = SelectionModel.Create(_tree, selectionLayers, selectionConfig, options.Parameters, calendar, evaluator.Context);
            evaluator.Context.SelectionStores = selections.Stores;
        }

        ParameterEnvironment.Populate(_tree, options.Parameters, parameters, evaluator, selections.Values);
        var folded = ExpressionReferenceFolder.Fold(_tree, evaluator);
        var specification = folded.Tree;
        var evaluation = new EvaluationState(calendar, parameters, evaluator);

        // Defaults < specification config < caller config
        var config = ConfigResolver.Resolve(specification, options.Config);
        var measurer = new TextMeasurer(options.EffectiveFonts);
        var root = specification.Root;

        // A composition has no single title position; layers share one. Only views with a mark have a data rectangle drawn: a single view, or the layers of a layered view
        // A composite mark (a boxplot) is a layered view of the layers it stands for; the expansion's own expressions are read from the derived tree
        var expanded = SpecNormalizer.Expand(specification, config, options.Limits);
        var layerTree = expanded.Layers;
        var isComposition = layerTree.Root.IsComposition;

        // The selections were declared for the units of the specification as written; the units of the tree that is drawn are the same views in the same order
        selections = selections.BindUnits(layerTree);

        // The findings of the view tree (a repeated value that is not defined, columns that a layout ignores, a shared axis of a concatenation) come from the one tree that is drawn;
        // an expression reference that could not be folded is reported first
        List<FeatureReport> treeWarnings = [.. folded.Warnings, .. layerTree.Warnings, .. selections.Warnings];
        dataWarningSink?.AddRange(treeWarnings);

        // The charts of a concatenation, a facet, a repeat and the layers are drawn; a facet is instantiated by the data stage even when no mark below it is drawn
        IReadOnlyList<LayerNode> drawn = [.. layerTree.Units.Where(u => MarkBuilder.IsSupported(MarkTypeOf(u.Unit!)))];
        DataStage? stage = null;
        if (drawn.Count > 0 || layerTree.Compositions.Any(c => c.Kind == ViewKind.Facet))
        {
            stage = RunDataStage(layerTree, expanded.Tree, drawn, config, options, evaluation, selections, cancellationToken, dataWarningSink);
        }

        // Every scene item is charged to one budget before it is created: first the root and view groups that every chart
        // has, then the title, the axes and the legends (per pass), the marks groups and the marks
        var budget = new SceneBudget(options.Limits.MaxSceneItems);
        budget.Add(2, root.GetLocation(_tree.SourceName));
        LayeredScales? scales = null;
        if (stage is not null && stage.Layers.Count > 0)
        {
            scales = ScaleResolver.ResolveLayers([.. stage.Layers.Select(l => new LayerScaleInput(l.Node, l.Plan, l.Data))], config, _tree.SourceName, options.Limits, cancellationToken, parameters, selections);
            dataWarningSink?.AddRange(scales.Warnings);
        }

        var background = options.Background is { } caller ? CssColor.ToCss(caller) : null;
        if (isComposition)
        {
            var composed = stage is null || scales is null ? [] : stage.Layers.Select((l, i) => new LayerChart(l.Node, l.Plan, l.Data, scales.PerLayer[i], selections)).ToList();
            IReadOnlyList<FeatureReport> composedWarnings = [.. treeWarnings, .. stage?.Warnings ?? [], .. scales?.Warnings ?? []];
            return BuildComposition(root, layerTree.Root, composed, scales, config, measurer, options.Limits, budget, stage?.Expressions, background, cancellationToken) with { DataWarnings = composedWarnings };
        }

        var hints = new ViewSizeHints(scales?.Merged.DiscreteWidth, scales?.Merged.DiscreteHeight, options.ContainerWidth, options.ContainerHeight);
        var request = ViewLayout.Resolve(SizeSource(root), config, hints, background);

        // A fit that a step size drops is the layout's finding, delivered with the data stage's after the reports of the specification
        var layoutWarnings = FitDroppedWarning(request, root, _tree);
        dataWarningSink?.AddRange(layoutWarnings);
        var title = new TitleBuilder(measurer).Build(root["title"], config, unitOrLayer: true, budget, _tree.SourceName);
        if (stage is null || scales is null)
        {
            // The measure is checked before the scene is built from it: a padding of 1e308 makes the canvas infinite, which the scene refuses without saying where
            var measure = ViewLayout.Measure(request, default, title);
            RequireFiniteSize(measure);
            return new ChartPlan(measure.NaturalSize, ViewLayout.Build(request, measure, []), measurer);
        }

        var layers = stage.Layers.Select((l, i) => new LayerChart(l.Node, l.Plan, l.Data, scales.PerLayer[i], selections)).ToList();
        IReadOnlyList<FeatureReport> dataWarnings = [.. treeWarnings, .. stage.Warnings, .. scales.Warnings, .. layoutWarnings];
        return BuildChart(new ChartInputs(request, title, layers, scales, config, measurer, options.Limits, budget, stage.Expressions), cancellationToken) with { DataWarnings = dataWarnings };
    }

    /// <summary>
    ///     Gets the view that decides the size of a chart: the view itself, or, for a repeat of layers or a facet that was dropped (its fields were unresolved repeat references), the view with the <c>width</c>, <c>height</c> and <c>view</c> of its <c>spec</c> where it has none.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite sizes a layered view that has no size of its own from its layers, and the layers of a repeat of layers (or the only layer of a dropped facet) are copies of the <c>spec</c>.
    /// </remarks>
    /// <param name="view">The view of the chart.</param>
    /// <returns>The view, or an overlay of it that adds the size of the repeated <c>spec</c>.</returns>
    private static SpecNode SizeSource(SpecNode view)
    {
        if ((view["repeat"] is { IsObject: true } || view["facet"] is { IsObject: true }) && view["spec"] is { IsObject: true } spec)
        {
            var missing = SpecNormalizer.SizeOf(spec).Where(p => view[p.Name!] is null).ToList();
            if (missing.Count > 0)
            {
                return SpecNode.Overlay(view, [.. view.Properties, .. missing]);
            }
        }

        return view;
    }

    /// <summary>
    ///     Gets the warning for an <c>autosize</c> fit that a step size dropped (Vega-Lite's <c>droppingFit</c>), if the layout dropped one.
    /// </summary>
    /// <remarks>
    ///     A dimension is sized by a step when its size is a <c>{"step": n}</c>, or missing on a discrete scale (the step of the configuration then applies). Only a drop that changes the
    ///     autosize is reported.
    /// </remarks>
    /// <param name="request">The layout request of the view.</param>
    /// <param name="root">The root of the specification.</param>
    /// <param name="tree">The specification tree, which locates the report.</param>
    /// <returns>The warning, or an empty list.</returns>
    private static IReadOnlyList<FeatureReport> FitDroppedWarning(ViewLayoutRequest request, SpecNode root, SpecTree tree)
    {
        if (request.DroppedFit is not { } dropped)
        {
            return [];
        }

        var dimension = dropped switch
        {
            "x" => "width",
            "y" => "height",
            _ => "width and height",
        };
        var result = request.Autosize switch
        {
            AutosizeType.FitX => "fit-x",
            AutosizeType.FitY => "fit-y",
            _ => "pad",
        };
        var at = root["autosize"] ?? (dropped == "y" ? root["height"] : root["width"]) ?? root;
        return [new FeatureReport("vl-layout-fit-step", $"The autosize fit does not apply to the {dimension}: a step (a discrete scale) sizes it; the view is laid out as '{result}'.", tree.Locate(at), FeatureReportKind.Warning)];
    }

    /// <summary>
    ///     Builds the axes, the legends and the marks of a single or layered view and lays the chart out around them.
    /// </summary>
    /// <remarks>
    ///     The axes and the legends need the size of the data rectangle (tick counts, positions, the length of a gradient) and the data
    ///     rectangle can depend on them (a <c>"container"</c> size leaves the room the axes and the legends do not need), so they are
    ///     iterated with the layout until the rectangle does not change (at most three passes; when the last pass still changed the rectangle, the guides are built once more for the final one); for a fixed size the first pass is final. The legends are placed
    ///     outside the axes and add their reach to that of the axes before the title is laid out, so a legend outside the data rectangle
    ///     makes the canvas larger. The marks are built once, for the final rectangle. The
    ///     axes come first in the view group, so that they are drawn behind the marks (Vega-Lite's default axis z-index), and the legends last.
    ///     The guides and the marks of the view are one cell (<see cref="CellInputs"/>, <see cref="BuildCellGuides"/>, <see cref="BuildCell"/>); the iteration with the layout is what only
    ///     the view that is the whole chart needs, a cell of a composition has a size that does not depend on its guides.
    /// </remarks>
    /// <param name="inputs">What the stages need.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildChart(ChartInputs inputs, CancellationToken cancellationToken)
    {
        var budget = inputs.Budget;
        var legendSpecs = LegendPlan.Create(LegendLayers(inputs.Layers, inputs.Scales), inputs.Config, _tree.SourceName, null, inputs.Layers.Select(l => l.Selections).FirstOrDefault(s => s is { HasSelections: true }));
        var axisPlans = PlanAxes(inputs.Layers, inputs.Scales);
        var cell = CreateCell(inputs.Layers, axisPlans, legendSpecs, inputs.Config, inputs.Measurer, inputs.Limits, _tree.SourceName, inputs.Expressions);
        var measure = ViewLayout.Measure(inputs.Request, default, inputs.Title);
        var guides = new CellGuides([], new LegendPlacement([], SceneBounds.Empty));
        var passBudget = budget;
        var (builtWidth, builtHeight) = (double.NaN, double.NaN);
        for (var pass = 0; pass < 3; pass++)
        {
            // Each pass builds the axes and the legends afresh, so each is charged to a fork; only the pass that is kept is adopted
            passBudget = budget.Fork();
            guides = BuildCellGuides(cell, measure.DataWidth, measure.DataHeight, passBudget, cancellationToken);
            (builtWidth, builtHeight) = (measure.DataWidth, measure.DataHeight);

            // The legends join the axes in what the data rectangle needs around it, before the title is laid out
            var extents = LegendLayout.Combine(AxisBuilder.GetExtents(guides.Axes, measure.DataWidth, measure.DataHeight), guides.Legends, measure.DataWidth, measure.DataHeight, inputs.Request.IsFit);
            var next = ViewLayout.Measure(inputs.Request, extents, inputs.Title);
            var stable = Math.Abs(next.DataWidth - measure.DataWidth) < 1e-9 && Math.Abs(next.DataHeight - measure.DataHeight) < 1e-9;
            measure = next;
            if (stable)
            {
                break;
            }
        }

        // A fit that did not settle in the passes allowed: the last pass measured a rectangle other than the one its guides were built for, so build them once more for the final rectangle
        if (Math.Abs(builtWidth - measure.DataWidth) >= 1e-9 || Math.Abs(builtHeight - measure.DataHeight) >= 1e-9)
        {
            passBudget = budget.Fork();
            guides = BuildCellGuides(cell, measure.DataWidth, measure.DataHeight, passBudget, cancellationToken);
        }

        budget.Adopt(passBudget);

        // A size that is not a finite number (the guides of a scale whose values overflow, such as a pow scale with a huge exponent) cannot be drawn: fail with the location
        RequireFiniteSize(measure);

        // The guides of the pass that was kept, and the marks for the final rectangle
        var content = BuildCell(cell, measure.DataWidth, measure.DataHeight, budget, cancellationToken, guides);
        return new ChartPlan(measure.NaturalSize, ViewLayout.Build(inputs.Request, measure, content), inputs.Measurer);
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
        var dataWarnings = new List<FeatureReport>();
        try
        {
            return PrepareChart(options, cancellationToken, dataWarnings);
        }
        catch (Exception ex) when (ex is VegaLiteSpecException or VegaLiteRenderException)
        {
            var sink = new VegaLiteReportSink(options.Limits.MaxDiagnostics, options.ThrowOnUnsupportedFeature);
            try
            {
                RecordReports(options, sink, cancellationToken);

                // The findings of the data stage come after those of the specification, as when the chart renders
                foreach (var warning in dataWarnings)
                {
                    sink.Warn($"{warning.Id}: {warning.Message}", warning.Location);
                }
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
    /// <param name="options">The effective options (the caller's configuration decides the reports that depend on it).</param>
    /// <param name="sink">The sink.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    private void RecordReports(VegaLiteRenderOptions options, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        foreach (var report in ReportsFor(options))
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
    /// <param name="options">The effective options (the caller's configuration decides the reports that depend on it).</param>
    /// <param name="placement">Where the scene goes on the surface; its scale decides which text is drawn.</param>
    /// <param name="sink">The sink that records the reports.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildPlan(ChartPlan plan, VegaLiteRenderOptions options, ScenePlacement placement, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordReports(options, sink, cancellationToken);

        // The data stage's own findings follow those of the specification text
        foreach (var warning in plan.DataWarnings ?? [])
        {
            sink.Warn($"{warning.Id}: {warning.Message}", warning.Location);
        }

        ReportMissingGlyphs(plan, placement.PaintScale, sink, cancellationToken);
        return plan;
    }

    /// <summary>
    ///     Reports the visible characters of the scene's text that no font has a glyph for, one warning per mark
    ///     location and distinct set of characters.
    /// </summary>
    /// <remarks>
    ///     A character the chosen font lacks is first looked up in the bundled fallback fonts
    ///     (<see cref="GlyphFallback"/>); only a character that none covers is drawn as the font's missing-glyph
    ///     box and reported here as <c>vl-text-missing-glyph</c>. The scene is walked in drawing order, so the
    ///     warnings come in a deterministic order. Text that the painter does not draw (see
    ///     <see cref="ScenePainter.IsDrawable(SceneText, float, out Rgba32)"/>: a fill that is not visibly painted, or a
    ///     font size, position or offset that is not drawable) is not reported; the check uses the placement scale of the
    ///     call (<see cref="VegaLiteRenderOptions.Scale"/> for <c>Measure</c> and <c>Render</c>, the fit scale for the size overload),
    ///     the scale the painter draws with, so <c>Measure</c> delivers the same warnings as <c>Render</c>. Text the chart builds itself (axis labels, legends, titles) has no
    ///     location in the specification and is reported at the unknown location, once per distinct set of
    ///     characters. The warning lists at most <see cref="MaxReportedMissingGlyphs"/> code points, but the
    ///     de-duplication uses the complete set, so two sets that share the listed prefix are both reported.
    /// </remarks>
    /// <param name="plan">The plan whose scene is checked.</param>
    /// <param name="scale">The scale the painter draws with (<see cref="ScenePlacement.PaintScale"/>).</param>
    /// <param name="sink">The sink that records the warnings.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    private static void ReportMissingGlyphs(ChartPlan plan, float scale, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        foreach (var entry in plan.Scene.Enumerate())
        {
            if (entry.Item is not SceneText text
                || !ScenePainter.IsDrawable(text, scale, out _))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var missing = plan.Measurer.FindMissingGlyphs(text);
            if (missing.Count == 0)
            {
                continue;
            }

            var list = string.Join(", ", missing.Take(MaxReportedMissingGlyphs).Select(c => string.Create(CultureInfo.InvariantCulture, $"U+{c:X4}")));
            var more = missing.Count > MaxReportedMissingGlyphs ? ", ..." : string.Empty;
            sink.Warn(
                $"vl-text-missing-glyph: no font has a glyph for {list}{more} (the bundled fonts do not cover them); the font's missing-glyph box is drawn instead.",
                text.Source,
                distinctKey: string.Join(',', missing));
        }
    }

    /// <summary>The most code points that one <c>vl-text-missing-glyph</c> warning lists.</summary>
    private const int MaxReportedMissingGlyphs = 8;


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
    /// <param name="options">The effective options (the image resolver and the limit of pictures).</param>
    /// <param name="sink">The sink that records what the painting finds: a picture the resolver did not resolve.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the mark, when the chart draws more pictures than <see cref="VegaLiteLimits.MaxImages"/>.</exception>
    private static Surface Paint(ChartPlan plan, int width, int height, ScenePlacement placement, VegaLiteRenderOptions options, VegaLiteReportSink sink, CancellationToken cancellationToken)
    {
        // The pictures of the image mark are loaded while the scene is painted (the scale is known only now) and every surface the resolver returned is released when the painting ends
        using var images = new SceneImageSource(options.ImageResolver, options.Limits.MaxImages, (message, location, key) => sink.Warn(message, location, key));
        return new ScenePainter(plan.Measurer, images, options.Limits.MaxPaintedPixels).Paint(plan.Scene, width, height, placement, cancellationToken);
    }

    /// <summary>
    ///     What the painter needs to draw the chart.
    /// </summary>
    /// <param name="Size">The natural size of the chart in pixels at a scale of one.</param>
    /// <param name="Scene">The scene: the canvas, its background and everything drawn on it.</param>
    /// <param name="Measurer">The text measurer with the fonts of this render.</param>
    /// <param name="DataWarnings">The warnings the data stage found (values that could not be converted), delivered after the reports of the specification; null for none.</param>
    private readonly record struct ChartPlan(VegaLiteSize Size, Scene Scene, TextMeasurer Measurer, IReadOnlyList<FeatureReport>? DataWarnings = null);

    /// <summary>
    ///     What the data, scale, axis and mark stages of one view share.
    /// </summary>
    /// <param name="Request">The layout request of the view.</param>
    /// <param name="Title">The built title, or null.</param>
    /// <param name="Layers">The layers that are drawn, with their scales and tables; a single view is one layer.</param>
    /// <param name="Scales">The scales of the layers and of the whole view.</param>
    /// <param name="Config">The effective configuration.</param>
    /// <param name="Measurer">The text measurer.</param>
    /// <param name="Limits">The limits.</param>
    /// <param name="Budget">The budget of scene items the chart is charged to, which already holds the root, view and title items.</param>
    /// <param name="Expressions">The compiled expressions and the evaluator of the call, for the row-bound values of the marks; null when there are none.</param>
    private sealed record ChartInputs(
        ViewLayoutRequest Request,
        TitleBlock? Title,
        IReadOnlyList<LayerChart> Layers,
        LayeredScales Scales,
        ResolvedConfig Config,
        TextMeasurer Measurer,
        VegaLiteLimits Limits,
        SceneBudget Budget,
        RowExpressions? Expressions);
}
