// Portions derived from Vega-Lite (BSD-3-Clause): compile/concat.ts, compile/model.ts and the layout size rules of its compile folder (the drawing of a concatenation: self-sized children, a grid, shared legends beside it, the title anchored at the start); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The cell: a unit or layered view built on its own, the unit that a composition lays out (Phase 11).
/// </content>
/// <remarks>
///     <para>
///         A cell is the guides (axes and legends) and the marks of one chart for a data rectangle of a given size. A single or layered view is the one cell of its chart; the
///         <see cref="BuildChart"/> of the whole chart iterates the cell with the layout until the data rectangle does not change (a <c>"container"</c> size depends on the room
///         the guides need). A composition builds the cells of its charts independently: the size of a chart in a composition does not depend on its guides, so a cell is built once
///         (<see cref="BuildCell"/> without guides) and the composition (concatenation, facet) lays the cells out with the <c>CompositionLayout</c> of the Layout subsystem.
///     </para>
///     <para>
///         <b>What a cell builds from.</b> The layers of the cell, the axes that were planned for them (<see cref="PlanAxes"/> plans over all the layers of the tree and says, for each axis,
///         which layers share it and which view owns it, so that a composition keeps the axes that its cell owns and moves the axes that a facet shares to its headers), and the
///         legends that were planned for them (<see cref="LegendPlan"/>: the legends that the cell owns; the legends owned by a concatenation or a facet are built with
///         <see cref="BuildLegends"/> against the bounds of that view's grid). The marks group of each layer is built for the data rectangle; the cell's content is in Vega-Lite's
///         order: grids, the axes behind the marks, the marks of each layer, the axes in front of them, the legends.
///     </para>
///     <para>
///         <b>The composition.</b> A concatenation (<c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, and the concatenations that a <c>repeat</c> normalizes to) is drawn by <see cref="Composer"/>: each chart
///         of it (a unit or layered view that is a child of a concatenation) is built as a cell of the size it resolves for itself (<see cref="ViewLayout.ResolveSize"/> with the step sizes of its own
///         scales), its bounds (<see cref="ViewLayout.CellBounds"/>) go to the <c>CompositionLayout</c>, which places the cells in a grid, and the concatenation adds the legends that it owns (the
///         legends that its children share, placed beside the grid) and its title. Everything is built once, bottom up, with an explicit stack, so the nesting cannot overflow the call stack.
///         A facet is built the same way from the cells that the data stage made for it (one for each value of its fields; the cells are laid out with headers, footers and titles
///         around them, and the axes that the cells share are drawn once in the outer headers and footers).
///     </para>
/// </remarks>
public sealed partial class VegaLiteDocument
{
    /// <summary>
    ///     What one cell needs to build its guides and its marks.
    /// </summary>
    /// <param name="Layers">The layers of the cell, in drawing order (the last one is on top); a single view is one layer.</param>
    /// <param name="AxisPlans">The axes that the cell draws, planned by <see cref="PlanAxes"/>.</param>
    /// <param name="LegendSpecs">The legends that the cell draws, planned by <see cref="LegendPlan"/>.</param>
    /// <param name="Config">The effective configuration.</param>
    /// <param name="Limits">The limits.</param>
    /// <param name="SourceName">The name of the specification, for the locations of errors; null for none.</param>
    /// <param name="Expressions">The compiled expressions and the evaluator of the call, for the row-bound values of the marks; null when there are none.</param>
    /// <param name="AxisBuilder">The axis builder of the cell.</param>
    /// <param name="LegendBuilder">The legend builder of the cell.</param>
    /// <param name="HeaderAxisPlans">The axes that the cell shares with the other cells of its facet: they are drawn in the headers and footers of the facet, and the cell keeps only their grids; null for none.</param>
    /// <param name="KeepHeaderAxis">Whether the facet takes the main part of a shared axis from this cell for a side (<see cref="AxisRequest.KeepInHeader"/>); null for every side.</param>
    private sealed record CellInputs(
        IReadOnlyList<LayerChart> Layers,
        IReadOnlyList<AxisPlan> AxisPlans,
        IReadOnlyList<LegendSpec> LegendSpecs,
        ResolvedConfig Config,
        VegaLiteLimits Limits,
        string? SourceName,
        RowExpressions? Expressions,
        AxisBuilder AxisBuilder,
        LegendBuilder LegendBuilder,
        IReadOnlyList<AxisPlan>? HeaderAxisPlans = null,
        Func<string, bool>? KeepHeaderAxis = null);

    /// <summary>
    ///     The guides of a cell for one data rectangle.
    /// </summary>
    /// <param name="Axes">The axes, with their grids.</param>
    /// <param name="Legends">The legends, placed next to the data rectangle and the axes.</param>
    /// <param name="HeaderAxes">The axes that the cell shares with its facet, built for the headers (<see cref="AxisRequest.InHeader"/>): the cell keeps their grids, the facet takes their axes; null for none.</param>
    private sealed record CellGuides(IReadOnlyList<AxisResult> Axes, LegendPlacement Legends, IReadOnlyList<AxisResult>? HeaderAxes = null);

    /// <summary>
    ///     What a facet needs of one of its channels (<c>row</c>, <c>column</c> or the wrapped <c>facet</c>) to draw its headers.
    /// </summary>
    /// <param name="Settings">The header properties of the channel (the sides that the renderer does not honor read as not given).</param>
    /// <param name="Label">Turns the value of a cell into the lines of its label; null when the labels are switched off or the facet has no such channel.</param>
    /// <param name="TitleLines">The lines of the title of the channel; none when it has none.</param>
    /// <param name="Location">The location of the field definition.</param>
    private sealed record FacetChannel(HeaderSettings Settings, Func<DataValue, IReadOnlyList<string>>? Label, IReadOnlyList<string> TitleLines, VegaLiteLocation Location);

    /// <summary>
    ///     Creates the inputs of a cell.
    /// </summary>
    /// <param name="layers">The layers of the cell.</param>
    /// <param name="axisPlans">The axes that the cell draws.</param>
    /// <param name="legendSpecs">The legends that the cell draws.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The name of the specification; null for none.</param>
    /// <param name="expressions">The compiled expressions of the call; null when there are none.</param>
    /// <param name="headerAxisPlans">The axes that the cell shares with its facet (drawn in the headers; the cell keeps their grids); null for none.</param>
    /// <param name="keepHeaderAxis">Whether the facet takes the main part of a shared axis from this cell for a side; null for every side.</param>
    /// <returns>The inputs; the calendar of the axes is that of the first layer.</returns>
    private static CellInputs CreateCell(
        IReadOnlyList<LayerChart> layers,
        IReadOnlyList<AxisPlan> axisPlans,
        IReadOnlyList<LegendSpec> legendSpecs,
        ResolvedConfig config,
        TextMeasurer measurer,
        VegaLiteLimits limits,
        string? sourceName,
        RowExpressions? expressions,
        IReadOnlyList<AxisPlan>? headerAxisPlans = null,
        Func<string, bool>? keepHeaderAxis = null) =>
        new(
            layers,
            axisPlans,
            legendSpecs,
            config,
            limits,
            sourceName,
            expressions,
            new AxisBuilder(measurer, config, limits, sourceName, layers[0].Data.Evaluation.Calendar, expressions),
            new LegendBuilder(measurer, limits, sourceName, expressions),
            headerAxisPlans,
            keepHeaderAxis);

    /// <summary>
    ///     Builds the guides of a cell for a data rectangle: its axes, then its legends placed outside the axes.
    /// </summary>
    /// <remarks>
    ///     The axes that the cell shares with its facet are built for the headers (their grids belong to the cell); they do not reach into the room of the cell, so the legends are placed
    ///     outside the axes that the cell keeps.
    /// </remarks>
    /// <param name="cell">The cell.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="budget">The budget of scene items the guides are charged to.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The guides.</returns>
    private static CellGuides BuildCellGuides(CellInputs cell, double width, double height, SceneBudget budget, CancellationToken cancellationToken)
    {
        var axes = BuildAxes(cell.AxisPlans, cell.AxisBuilder, width, height, budget, cancellationToken);
        var headerAxes = cell.HeaderAxisPlans is { Count: > 0 } shared ? BuildAxes(shared, cell.AxisBuilder, width, height, budget, cancellationToken, inHeader: true, cell.KeepHeaderAxis) : null;
        var (horizontal, vertical) = LegendLayout.AxisBounds(axes, width, height);
        var legends = BuildLegends(cell.LegendSpecs, cell.LegendBuilder, cell.Config, horizontal, vertical, width, height, budget, cancellationToken);
        return new CellGuides(axes, legends, headerAxes);
    }

    /// <summary>
    ///     Builds the content of a cell for a data rectangle: the grids, the axes behind the marks, the marks of each layer, the axes in front of them and the legends.
    /// </summary>
    /// <remarks>
    ///     The marks group of each layer and each mark are charged to the budget by the mark builder before they are created. When <paramref name="guides"/> is null the guides are
    ///     built here for the same rectangle (a cell of a composition, whose size is fixed); the chart that iterates its guides with the layout passes the guides of the pass it kept.
    /// </remarks>
    /// <param name="cell">The cell.</param>
    /// <param name="width">The width of the data rectangle that the marks are built for.</param>
    /// <param name="height">The height of the data rectangle that the marks are built for.</param>
    /// <param name="budget">The budget of scene items the content is charged to.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="guides">The guides that were built already, or null to build them for the same data rectangle.</param>
    /// <returns>The content of the cell in drawing order.</returns>
    private static List<SceneItem> BuildCell(CellInputs cell, double width, double height, SceneBudget budget, CancellationToken cancellationToken, CellGuides? guides = null)
    {
        guides ??= BuildCellGuides(cell, width, height, budget, cancellationToken);
        var axes = guides.Axes;
        var content = new List<SceneItem>(axes.Count * 2 + guides.Legends.Groups.Count + cell.Layers.Count);

        // Vega-Lite draws the grids first, then the axes that are behind the marks, the marks, the axes in front of them, and last the legends; the axes that the cell shares with its
        // facet are in the headers, but their grids are drawn here (x before y, as the axes of one view are)
        if (guides.HeaderAxes is { Count: > 0 } headerAxes)
        {
            content.AddRange(headerAxes.Concat(axes).Where(a => a.Grid is not null).OrderBy(a => a.Orient is "top" or "bottom" ? 0 : 1).Select(a => (SceneItem)a.Grid!));
        }
        else
        {
            content.AddRange(axes.Where(a => a.Grid is not null).Select(a => (SceneItem)a.Grid!));
        }

        content.AddRange(axes.Where(a => !a.InFront).Select(a => a.Group));

        // The marks group of each layer and each mark are charged by the mark builder before they are created; the layers are drawn in order, the last one on top, with the brushes of the
        // interval selections around them
        var markBuilder = new MarkBuilder(cell.Config, cell.Limits, budget, cell.SourceName, cell.Expressions);
        content.AddRange(BuildMarkGroups(cell, markBuilder, width, height, cancellationToken));

        content.AddRange(axes.Where(a => a.InFront).Select(a => a.Group));
        content.AddRange(guides.Legends.Groups);
        return content;
    }

    /// <summary>
    ///     Builds the marks groups of the layers of a cell with the brushes of the interval selections around them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A unit that is the whole chart, or a unit of a concatenation or of a facet, draws the shade of the brushes of its selections first and the outlines last (<c>[brush_bg, marks, brush]</c>, the
    ///         selections of the unit one around the other in the order they are declared). The units of a layered view do it together, as Vega-Lite's <c>assembleLayerSelectionMarks</c> does,
    ///         wrapping the marks of all the layers child by child: <c>[bg_n, ..., bg_0, marks of every layer, brush_0, ..., brush_n]</c>, so that the shade of every selection is behind every layer and the outline in front.
    ///         A layered view in a layered view wraps the marks of its own units first. Without a selection the layers are drawn in order and nothing else is made.
    ///     </para>
    ///     <para>The recursion follows the nesting of the layered views of one chart, which <see cref="VegaLiteLimits.MaxCompositionDepth"/> bounds.</para>
    /// </remarks>
    /// <param name="cell">The cell.</param>
    /// <param name="markBuilder">The mark builder of the cell.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The groups in drawing order.</returns>
    private static List<SceneItem> BuildMarkGroups(CellInputs cell, MarkBuilder markBuilder, double width, double height, CancellationToken cancellationToken)
    {
        var selections = cell.Layers.Select(l => l.Selections).FirstOrDefault(s => s is { HasSelections: true });
        var root = cell.Layers[0].Node.ChartRoot ?? cell.Layers[0].Node;
        if (selections is not null && cell.Layers.All(l => IsBelow(l.Node, root)))
        {
            var byNode = cell.Layers.ToDictionary(l => l.Node);
            return DrawWithBrushes(root, markBuilder, selections, byNode, width, height, cancellationToken);
        }

        var groups = new List<SceneItem>(cell.Layers.Count);
        foreach (var layer in cell.Layers)
        {
            groups.Add(markBuilder.Build(layer.Unit, layer.Data.Main, layer.Scales, width, height, cancellationToken));
        }

        return groups;
    }

    /// <summary>
    ///     Tells whether a view is the root or below it.
    /// </summary>
    /// <param name="node">The view.</param>
    /// <param name="root">The root.</param>
    /// <returns><see langword="true"/> when walking up from the view reaches the root.</returns>
    private static bool IsBelow(LayerNode node, LayerNode root)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Draws the layers below a view with the brushes of their selections around them.
    /// </summary>
    /// <param name="node">The view: the chart, then the layered views and units below it.</param>
    /// <param name="markBuilder">The mark builder of the cell.</param>
    /// <param name="selections">The selections of the specification.</param>
    /// <param name="byNode">The layers of the cell by their unit.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The groups in drawing order.</returns>
    private static List<SceneItem> DrawWithBrushes(
        LayerNode node,
        MarkBuilder markBuilder,
        SelectionModel selections,
        Dictionary<LayerNode, LayerChart> byNode,
        double width,
        double height,
        CancellationToken cancellationToken)
    {
        if (byNode.TryGetValue(node, out var layer))
        {
            List<SceneItem> own = [markBuilder.Build(layer.Unit, layer.Data.Main, layer.Scales, width, height, cancellationToken)];

            // The marks of a unit that is a layer are wrapped by the layered view above it; any other unit wraps its own
            return node.Parent is { IsLayer: true } ? own : WrapWithBrushes(node, own, markBuilder, selections, byNode, width, height);
        }

        var items = new List<SceneItem>();
        foreach (var child in node.Children)
        {
            items.AddRange(DrawWithBrushes(child, markBuilder, selections, byNode, width, height, cancellationToken));
        }

        if (node.IsLayer)
        {
            foreach (var child in node.Children.Where(byNode.ContainsKey))
            {
                items = WrapWithBrushes(child, items, markBuilder, selections, byNode, width, height);
            }
        }

        return items;
    }

    /// <summary>
    ///     Puts the brushes of the selections of a unit around some marks: the shade of each before them and its outline after them, the selections one around the other in declaration order.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <param name="marks">The groups to wrap.</param>
    /// <param name="markBuilder">The mark builder of the cell.</param>
    /// <param name="selections">The selections of the specification.</param>
    /// <param name="byNode">The layers of the cell by their unit.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <returns>The wrapped groups.</returns>
    private static List<SceneItem> WrapWithBrushes(
        LayerNode unit,
        List<SceneItem> marks,
        MarkBuilder markBuilder,
        SelectionModel selections,
        Dictionary<LayerNode, LayerChart> byNode,
        double width,
        double height)
    {
        var result = marks;
        foreach (var component in selections.ComponentsOf(unit))
        {
            if (markBuilder.BuildBrush(component, selections, byNode[unit].Scales, unit, width, height) is { } brush)
            {
                result = [brush.Background, .. result, brush.Outline];
            }
        }

        return result;
    }

    /// <summary>
    ///     Builds the legends of a view and places them against the bounds that they go outside of.
    /// </summary>
    /// <remarks>
    ///     A chart places its legends outside its axes (<see cref="LegendLayout.AxisBounds"/> gives the bounds); a concatenation or a facet that owns a legend places it outside the bounds of
    ///     its grid, with the data rectangle given as zero by zero (Vega's group of a composition has no size of its own).
    /// </remarks>
    /// <param name="specs">The planned legends.</param>
    /// <param name="builder">The legend builder.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="horizontal">The bounds that a legend at the left or right goes outside of.</param>
    /// <param name="vertical">The bounds that a legend at the top or bottom goes outside of.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="budget">The budget of scene items the legends are charged to.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The placed legends; empty when the view has none.</returns>
    private static LegendPlacement BuildLegends(IReadOnlyList<LegendSpec> specs, LegendBuilder builder, ResolvedConfig config, SceneBounds horizontal, SceneBounds vertical, double width, double height, SceneBudget budget, CancellationToken cancellationToken)
    {
        if (specs.Count == 0)
        {
            return new LegendPlacement([], SceneBounds.Empty);
        }

        var built = new List<LegendResult>(specs.Count);
        foreach (var spec in specs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            built.Add(builder.Build(spec, width, height, budget, cancellationToken));
        }

        return LegendLayout.Place(built, config, horizontal, vertical, width, height);
    }

    /// <summary>
    ///     Builds the axes of the plan for a data rectangle.
    /// </summary>
    /// <param name="plans">The planned axes: one for each position channel that has a scale (x first), more for a layered view with independent axes.</param>
    /// <param name="builder">The axis builder.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="budget">The budget of scene items the axes are charged to.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="inHeader">Whether the axes are built for the headers and footers of a facet (<see cref="AxisRequest.InHeader"/>).</param>
    /// <param name="keepInHeader">For axes built for the headers: whether the facet takes the main part of the axis of a side (<see cref="AxisRequest.KeepInHeader"/>).</param>
    /// <returns>The axes; empty when no position channel has a scale or all axes are switched off.</returns>
    private static List<AxisResult> BuildAxes(IReadOnlyList<AxisPlan> plans, AxisBuilder builder, double width, double height, SceneBudget budget, CancellationToken cancellationToken, bool inHeader = false, Func<string, bool>? keepInHeader = null)
    {
        var axes = new List<AxisResult>(plans.Count);
        foreach (var plan in plans)
        {
            var model = plan.Scale.CreateModel(plan.Channel == "x" ? width : height);
            if (builder.Build(new AxisRequest(plan.Channel, plan.Definition, plan.Scale, model, width, height, plan.MarkType, budget, plan.DefaultOrient, plan.DefaultGrid, inHeader, keepInHeader), cancellationToken) is { } axis)
            {
                axes.Add(axis);
            }
        }

        return axes;
    }

    /// <summary>
    ///     Draws a composition: builds the charts and concatenations of the tree bottom up, places the cells of each concatenation, and gives the content to the canvas of the root.
    /// </summary>
    /// <remarks>
    ///     The root has no data rectangle of its own (<see cref="ViewLayout.Resolve"/> with <c>composition</c>): the canvas is the bounds of the content plus the padding, so what a chart
    ///     draws outside its cell (an axis, a legend) enlarges the canvas as it does for a single view.
    /// </remarks>
    /// <param name="view">The top-level view of the specification as it is drawn (the expression references folded), whose padding, background and view are read.</param>
    /// <param name="root">The root of the view tree (a concatenation or a facet).</param>
    /// <param name="layers">The layers that are drawn, with their scales, in document order.</param>
    /// <param name="scales">The scales of the layers; null when no layer is drawn.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="budget">The budget of scene items, which already holds the root and view groups.</param>
    /// <param name="expressions">The compiled expressions of the call; null when there are none.</param>
    /// <param name="background">The caller's background as CSS, or null.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The plan.</returns>
    private ChartPlan BuildComposition(
        SpecNode view,
        LayerNode root,
        IReadOnlyList<LayerChart> layers,
        LayeredScales? scales,
        ResolvedConfig config,
        TextMeasurer measurer,
        VegaLiteLimits limits,
        SceneBudget budget,
        RowExpressions? expressions,
        string? background,
        CancellationToken cancellationToken)
    {
        var composer = new Composer(layers, scales, config, measurer, limits, budget, expressions, _tree.SourceName, cancellationToken);
        var content = composer.Compose(root);
        var request = ViewLayout.Resolve(view, config, default, background, composition: true);

        // The root is zero by zero and its "guides" are the bounds of the content, so the canvas is the content plus the padding
        var extents = LegendLayout.Combine(default, content.Bounds, 0, 0);
        var measure = ViewLayout.Measure(request, extents);
        return new ChartPlan(measure.NaturalSize, ViewLayout.Build(request, measure, [content.Group]), measurer);
    }

    /// <summary>
    ///     Builds the cells of a composition and lays them out.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Charts.</b> A chart is a unit or layered view that is the root, a child of a concatenation or the body of a facet (<see cref="LayerNode.ChartRoot"/>). It sizes itself (its <c>width</c>
    ///         and <c>height</c>, else the step of its own discrete scale, else the continuous size) and builds its cell: the axes that it owns (a concatenation never shares an axis), the marks of its layers, the
    ///         legends that it owns, and its title inside the cell. The cell group has the style of the chart's <c>view</c>, and its bounds include the half of the stroke (Vega unions the bounds of a stroked group).
    ///     </para>
    ///     <para>
    ///         <b>Concatenations.</b> A concatenation places the cells of its children with the layout it carries (<c>columns</c>, <c>spacing</c>, <c>align</c>, <c>center</c>, <c>bounds</c>), then adds the legends that it
    ///         owns (the legends that its children share, planned with <see cref="LegendPlan"/> and placed against the bounds of the grid with a data rectangle of zero by zero, as Vega places the legends of a group
    ///         that has no size) and its title (anchored at the start, over the bounds of everything the concatenation holds). Its group has the role <c>scope</c>, and its bounds are those of the grid, the legends and the title.
    ///     </para>
    ///     <para>
    ///         <b>Facets.</b> A facet places the charts of its cells (copies of its template, made by the data stage) in a grid with the headers, footers and titles of its fields (see
    ///         <see cref="ComposeFacet"/>): the labels of a <c>row</c> and <c>column</c> field are headers, the labels of a wrapped facet are the titles of its cells, and the axes that the
    ///         cells share are in the outer headers and footers, the cells keeping their grids. The legends that its cells share are the facet's own, placed beside the grid.
    ///     </para>
    /// </remarks>
    private sealed class Composer
    {
        /// <summary>The layers that are drawn, in document order.</summary>
        private readonly IReadOnlyList<LayerChart> _layers;

        /// <summary>The scales of the layers; null when no layer is drawn.</summary>
        private readonly LayeredScales? _scales;

        /// <summary>The effective configuration.</summary>
        private readonly ResolvedConfig _config;

        /// <summary>The text measurer.</summary>
        private readonly TextMeasurer _measurer;

        /// <summary>The limits.</summary>
        private readonly VegaLiteLimits _limits;

        /// <summary>The budget of scene items.</summary>
        private readonly SceneBudget _budget;

        /// <summary>The compiled expressions of the call.</summary>
        private readonly RowExpressions? _expressions;

        /// <summary>The evaluation of the <c>labelExpr</c> of the headers.</summary>
        private readonly LabelExpressions _labelExpressions;

        /// <summary>The name of the specification.</summary>
        private readonly string? _sourceName;

        /// <summary>The token that stops the work.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The indexes of the layers under each view of the tree (every view above a layer has its index).</summary>
        private readonly Dictionary<LayerNode, List<int>> _leavesUnder = new(ReferenceEqualityComparer.Instance);

        /// <summary>The axes that each chart owns, by chart.</summary>
        private readonly Dictionary<LayerNode, List<AxisPlan>> _axesOfChart = new(ReferenceEqualityComparer.Instance);

        /// <summary>The legends that each chart owns, by chart.</summary>
        private readonly Dictionary<LayerNode, List<LegendSpec>> _legendsOfChart = new(ReferenceEqualityComparer.Instance);

        /// <summary>The legends that each concatenation or facet owns, by view.</summary>
        private readonly Dictionary<LayerNode, IReadOnlyList<LegendSpec>> _legendsOfComposition = new(ReferenceEqualityComparer.Instance);

        /// <summary>The axes that each cell of a facet shares with the other cells, built for the headers and footers, by chart.</summary>
        private readonly Dictionary<LayerNode, IReadOnlyList<AxisResult>> _headerAxesOfChart = new(ReferenceEqualityComparer.Instance);

        /// <summary>The header properties, labels and titles of the channels of each facet, by facet and channel (<c>row</c>, <c>column</c> or <c>facet</c>).</summary>
        private readonly Dictionary<(LayerNode Facet, string Channel), FacetChannel> _facetChannels = new();

        /// <summary>
        ///     Initializes the composer and plans the guides of all the charts.
        /// </summary>
        /// <param name="layers">The layers that are drawn, with their scales.</param>
        /// <param name="scales">The scales of the layers; null when no layer is drawn.</param>
        /// <param name="config">The effective configuration.</param>
        /// <param name="measurer">The text measurer.</param>
        /// <param name="limits">The limits.</param>
        /// <param name="budget">The budget of scene items.</param>
        /// <param name="expressions">The compiled expressions of the call; null when there are none.</param>
        /// <param name="sourceName">The name of the specification; null for none.</param>
        /// <param name="cancellationToken">The token that stops the work.</param>
        public Composer(
            IReadOnlyList<LayerChart> layers,
            LayeredScales? scales,
            ResolvedConfig config,
            TextMeasurer measurer,
            VegaLiteLimits limits,
            SceneBudget budget,
            RowExpressions? expressions,
            string? sourceName,
            CancellationToken cancellationToken)
        {
            _layers = layers;
            _scales = scales;
            _config = config;
            _measurer = measurer;
            _limits = limits;
            _budget = budget;
            _expressions = expressions;
            _labelExpressions = new LabelExpressions(expressions, limits, layers.Count > 0 ? layers[0].Data.Evaluation.Calendar : null, cancellationToken);
            _sourceName = sourceName;
            _cancellationToken = cancellationToken;
            for (var i = 0; i < layers.Count; i++)
            {
                for (var node = layers[i].Node; node is not null; node = node.Parent)
                {
                    if (!_leavesUnder.TryGetValue(node, out var leaves))
                    {
                        leaves = [];
                        _leavesUnder[node] = leaves;
                    }

                    leaves.Add(i);
                }
            }

            if (scales is not null)
            {
                PlanGuides(scales);
            }
        }

        /// <summary>
        ///     Builds the content of the tree.
        /// </summary>
        /// <param name="root">The root of the tree.</param>
        /// <returns>The group of the root in its own coordinates, and its bounds.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the view, when the scene is larger than the limit.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        public CompositionCell Compose(LayerNode root)
        {
            // Children come after their parent in this order, so the reverse order builds the children first; a chart is a leaf of the walk
            var order = new List<LayerNode>();
            var pending = new Stack<LayerNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                order.Add(node);
                if (node.Kind is ViewKind.Concat or ViewKind.Facet)
                {
                    foreach (var child in node.Children)
                    {
                        pending.Push(child);
                    }
                }
            }

            var built = new Dictionary<LayerNode, CompositionCell>(ReferenceEqualityComparer.Instance);
            for (var i = order.Count - 1; i >= 0; i--)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var node = order[i];
                built[node] = node.Kind switch
                {
                    ViewKind.Concat => ComposeConcat(node, built),
                    ViewKind.Facet => ComposeFacet(node, built),
                    _ => ComposeChart(node),
                };
            }

            return built[root];
        }

        /// <summary>
        ///     Plans the axes and the legends of every chart: the axes of the whole tree are planned together (and each belongs to the chart that owns it), and each owner of a legend plans it.
        /// </summary>
        /// <param name="scales">The scales of the layers.</param>
        private void PlanGuides(LayeredScales scales)
        {
            foreach (var plan in PlanAxes(_layers, scales))
            {
                var chart = plan.Owner.ChartRoot ?? plan.Owner;
                if (!_axesOfChart.TryGetValue(chart, out var plans))
                {
                    plans = [];
                    _axesOfChart[chart] = plans;
                }

                plans.Add(plan);
            }

            var legendLayers = LegendLayers(_layers, scales);
            var selections = _layers.Select(l => l.Selections).FirstOrDefault(s => s is { HasSelections: true });
            foreach (var owner in LegendPlan.Owners(legendLayers))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var specs = LegendPlan.Create([.. _leavesUnder[owner].Select(i => legendLayers[i])], _config, _sourceName, owner, selections);
                if (owner.ChartRoot is { } chart)
                {
                    if (!_legendsOfChart.TryGetValue(chart, out var own))
                    {
                        own = [];
                        _legendsOfChart[chart] = own;
                    }

                    own.AddRange(specs);
                }
                else
                {
                    _legendsOfComposition[owner] = specs;
                }
            }
        }

        /// <summary>
        ///     Tells which sides of the shared axes of a facet are taken from a cell: the facet draws the left and right axes in the header of a row, which is that of the cell in the first column,
        ///     and the top and bottom axes in the header of a column, which is that of the cell in the first row (<see cref="FacetHeaders"/>).
        /// </summary>
        /// <param name="chart">The chart of the cell.</param>
        /// <param name="facet">The facet.</param>
        /// <returns>The predicate on the side of an axis.</returns>
        private static Func<string, bool> HeaderSides(LayerNode chart, LayerNode facet)
        {
            var cell = chart.Cell!;
            bool firstRow;
            bool firstColumn;
            if (facet.Facet!.IsWrap)
            {
                var columns = facet.Layout!.Columns is > 0 ? facet.Layout.Columns.Value : Math.Max(1, facet.Children.Count);
                (firstRow, firstColumn) = (cell.Index < columns, cell.Index % columns == 0);
            }
            else
            {
                (firstRow, firstColumn) = (cell.RowIndex == 0, cell.ColumnIndex == 0);
            }

            return side => side is "left" or "right" ? firstColumn : firstRow;
        }

        /// <summary>
        ///     Builds the cell of a chart.
        /// </summary>
        /// <param name="chart">The chart: a unit view, a layered view or a composite mark.</param>
        /// <returns>The cell group at the origin and its bounds (the data rectangle, the guides, the title and the half of the stroke).</returns>
        private CompositionCell ComposeChart(LayerNode chart)
        {
            var leaves = _leavesUnder.GetValueOrDefault(chart) ?? [];
            var hints = _scales is null || leaves.Count == 0 ? default : _scales.HintsFor(leaves);
            var sizeSource = SizeSource(chart.Source);
            var size = ViewLayout.ResolveSize(sizeSource, _config, hints);
            var style = ViewLayout.Resolve(sizeSource, _config, hints).View;
            var location = chart.Source.GetLocation(_sourceName);
            _budget.Add(1, location);
            var facet = chart.Parent is { Kind: ViewKind.Facet } parent ? parent : null;
            var title = CellTitle(chart, facet);

            // A chart that draws nothing (its marks are not supported) is an empty cell, as a single view with such a mark is an empty plot area
            var content = new List<SceneItem>();
            var extents = default(ViewExtents);
            if (leaves.Count > 0)
            {
                // The axes that the cells of a facet share are drawn in its headers and footers; the cell keeps their grids
                var cell = CreateCell(
                    [.. leaves.Select(i => _layers[i])],
                    _axesOfChart.GetValueOrDefault(chart) ?? [],
                    _legendsOfChart.GetValueOrDefault(chart) ?? [],
                    _config,
                    _measurer,
                    _limits,
                    _sourceName,
                    _expressions,
                    facet is null ? null : _axesOfChart.GetValueOrDefault(facet),
                    facet is null ? null : HeaderSides(chart, facet));
                var guides = BuildCellGuides(cell, size.Width, size.Height, _budget, _cancellationToken);
                if (guides.HeaderAxes is { } headerAxes)
                {
                    _headerAxesOfChart[chart] = headerAxes;
                }

                extents = LegendLayout.Combine(AxisBuilder.GetExtents(guides.Axes, size.Width, size.Height), guides.Legends.Bounds, size.Width, size.Height);
                content = BuildCell(cell, size.Width, size.Height, _budget, _cancellationToken, guides);
            }

            // Vega's bounds of a stroked group include half the stroke, so the cells are spaced from the outside of their border
            var strokeWidth = style.Stroke is null ? 0 : style.StrokeWidth ?? 1;
            var (bounds, placedTitle) = ViewLayout.CellBounds(size.Width, size.Height, extents, title, strokeWidth);
            if (placedTitle is not null)
            {
                content.Add(placedTitle);
            }

            return new CompositionCell(ViewLayout.CreateViewGroup(style, 0, 0, size.Width, size.Height, null, content), bounds);
        }

        /// <summary>
        ///     Builds the title of the cell of a chart: the label of its value in a wrapped facet, else the title of the chart.
        /// </summary>
        /// <remarks>
        ///     A wrapped facet draws the value of the cell as the title of its group (Vega-Lite's <c>assembleLabelTitle</c> of a facet), which replaces the title of the chart; when the
        ///     labels are switched off the chart keeps its own. The body of a faceted unit view has no title of its own: the title of the view is the title of the facet.
        /// </remarks>
        /// <param name="chart">The chart.</param>
        /// <param name="facet">The facet that holds the chart, or null.</param>
        /// <returns>The title; null when the cell has none.</returns>
        private TitleBlock? CellTitle(LayerNode chart, LayerNode? facet)
        {
            var builder = new TitleBuilder(_measurer);
            if (facet?.Facet is { IsWrap: true } && chart.Cell is { } cell && ChannelOf(facet, "facet") is { Label: { } format } wrap)
            {
                IReadOnlyList<string> text = cell.WrapValue.IsNull ? ["null"] : format(cell.WrapValue);
                if (builder.BuildHeaderBlock(text, wrap.Settings, HeaderPart.Label, _budget, wrap.Location) is { } label)
                {
                    return label;
                }
            }

            return chart.IsFacetBody ? null : builder.Build(chart.Source["title"], _config, unitOrLayer: true, _budget, _sourceName);
        }

        /// <summary>
        ///     Gets the header properties, the label formatter and the title of one channel of a facet.
        /// </summary>
        /// <param name="facet">The facet.</param>
        /// <param name="channel">The channel: <c>row</c>, <c>column</c> or <c>facet</c>.</param>
        /// <returns>The channel; the settings of an absent channel are those of the configuration, without labels or title.</returns>
        private FacetChannel ChannelOf(LayerNode facet, string channel)
        {
            if (_facetChannels.TryGetValue((facet, channel), out var found))
            {
                return found;
            }

            var definition = channel switch
            {
                "row" => facet.Facet!.Row,
                "column" => facet.Facet!.Column,
                _ => facet.Facet!.Wrap,
            };
            FacetChannel created;
            if (definition is null)
            {
                created = new FacetChannel(HeaderSettings.Read(_config, null, channel, honoredOrientsOnly: true), null, [], default);
            }
            else
            {
                // Side that are not honored read as not given (the feature catalog reports them as vl-header-orient)
                var settings = HeaderSettings.Read(_config, definition["header"], channel, honoredOrientsOnly: true);
                var location = definition.GetLocation(_sourceName);
                var calendar = _layers.Count > 0 ? _layers[0].Data.Evaluation.Calendar : null;
                var unit = definition["timeUnit"] is { } unitNode && TimeUnitSpec.TryRead(unitNode, out var parsedUnit) && !parsedUnit.IsBinned && parsedUnit.Parts.Count > 0 ? parsedUnit : null;
                Func<DataValue, IReadOnlyList<string>>? label = settings.ShowLabels
                    ? WithLabelExpression(
                        settings,
                        settings.CreateLabelFormatter(new HeaderLabelFormat(ChannelDefinitions.TypeOf(definition, channel), unit, calendar, _limits.MaxStringLength, location), _sourceName),
                        location)
                    : null;
                created = new FacetChannel(settings, label, TitleLinesOf(definition, settings), location);
            }

            _facetChannels[(facet, channel)] = created;
            return created;
        }

        /// <summary>
        ///     Wraps the label formatter of a header in the <c>labelExpr</c> of the header, when it has one (the header, the configuration of its channel or <c>config.header</c>).
        /// </summary>
        /// <remarks>
        ///     The datum of the expression has the value of the cell (<c>datum.value</c>) and the formatted label (<c>datum.label</c>), as Vega-Lite's header gives it; an array result is
        ///     several lines of the header (a string result stays one line, however many line feeds it holds).
        /// </remarks>
        /// <param name="settings">The header properties.</param>
        /// <param name="formatter">The formatter that gives the label without the expression.</param>
        /// <param name="location">The location of the field definition.</param>
        /// <returns>The function that gives the lines of a label: the formatted label alone when the header has no label expression.</returns>
        private Func<DataValue, IReadOnlyList<string>> WithLabelExpression(HeaderSettings settings, Func<DataValue, string> formatter, VegaLiteLocation location)
        {
            var node = settings.ExplicitNode("labelExpr");
            var expression = _labelExpressions.Resolve(node, node is null ? settings.Text("labelExpr") : null, location);
            if (expression is null)
            {
                return value => [formatter(value)];
            }

            return value =>
            {
                var result = _labelExpressions.Evaluate(expression, LabelExpressions.Datum(value, formatter(value)));
                return result.Lines ?? [result.Text];
            };
        }

        /// <summary>
        ///     Gets the lines of the title of a facet channel: the title of the header, else the title of the field definition, else the name of the field.
        /// </summary>
        /// <param name="definition">The field definition of the channel.</param>
        /// <param name="settings">The header properties of the channel.</param>
        /// <returns>The lines; none when the title is switched off (<c>null</c>, or <c>header: null</c>).</returns>
        private static IReadOnlyList<string> TitleLinesOf(SpecNode definition, HeaderSettings settings)
        {
            if (settings.Hidden)
            {
                return [];
            }

            var written = settings.Title ?? definition["title"];
            if (written is not null)
            {
                if (written.IsString)
                {
                    return [written.StringValue!];
                }

                return written.IsArray ? [.. written.Items.Where(i => i.IsString).Select(i => i.StringValue!)] : [];
            }

            return settings.IncludeDefaultTitle && definition["field"]?.AsString() is { } field ? [field] : [];
        }

        /// <summary>
        ///     Builds a concatenation: its cells in a grid, the legends that it owns, and its title.
        /// </summary>
        /// <param name="node">The concatenation.</param>
        /// <param name="built">The cells of the views built so far, by view; every child of the concatenation is in it.</param>
        /// <returns>The group of the concatenation at the origin and its bounds.</returns>
        private CompositionCell ComposeConcat(LayerNode node, Dictionary<LayerNode, CompositionCell> built)
        {
            var location = node.Source.GetLocation(_sourceName);
            _budget.Add(1, location);
            var placement = CompositionLayout.Place([.. node.Children.Select(c => (CompositionCell?)built[c])], null, node.Layout!);
            var items = new List<SceneItem>(placement.Items);
            var bounds = AddOwnedLegends(node, items, placement.Bounds);
            return Finish(node, items, bounds);
        }

        /// <summary>
        ///     Adds the legends that a concatenation or a facet owns, beside the bounds of its grid.
        /// </summary>
        /// <remarks>
        ///     The group of a composition has no size of its own: the legends go beside the bounds of the grid (the cells with their headers and footers), from the origin, as Vega
        ///     places the legends of a group of zero size.
        /// </remarks>
        /// <param name="node">The concatenation or facet.</param>
        /// <param name="items">The content so far; receives the legend groups.</param>
        /// <param name="bounds">The bounds of the grid.</param>
        /// <returns>The bounds of the grid and the legends.</returns>
        private SceneBounds AddOwnedLegends(LayerNode node, List<SceneItem> items, SceneBounds bounds)
        {
            if (_legendsOfComposition.GetValueOrDefault(node) is not { Count: > 0 } specs)
            {
                return bounds;
            }

            var around = bounds.IsEmpty ? new SceneBounds(0, 0, 0, 0) : bounds;
            var legendBuilder = new LegendBuilder(_measurer, _limits, _sourceName, _expressions);
            var legends = BuildLegends(specs, legendBuilder, _config, around, around, 0, 0, _budget, _cancellationToken);
            items.AddRange(legends.Groups);
            return bounds.Union(legends.Bounds);
        }

        /// <summary>
        ///     Builds a facet: its cells in a grid, the headers, footers and titles around them, the legends that it owns and its title.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The cells were built as charts and copied from the facet's template by the data stage (one for each value of a wrapped facet, one for each combination of a row value and a column
        ///         value otherwise, in row-major order). The axes that the cells share are in the headers and footers of the rows and columns (<see cref="FacetHeaders"/>); the labels of the values of
        ///         a <c>row</c> and a <c>column</c> field are the headers of their rows and columns, those of a wrapped facet are the titles of the cells (<see cref="CellTitle"/>). The titles of
        ///         the fields are the <c>row-title</c> and <c>column-title</c> groups, ten pixels outside the headers for a <c>row</c> and <c>column</c> facet and at the edge of the cells for a wrapped one.
        ///     </para>
        ///     <para>
        ///         Vega-Lite lays a facet out with <c>bounds: full</c> and, unless the specification says otherwise, <c>align: all</c> (<c>none</c> when the scale of the direction without a facet
        ///         field is independent); the columns are those of the <c>column</c> field, one for a <c>row</c> facet, or the <c>columns</c> of a wrapped facet.
        ///     </para>
        /// </remarks>
        /// <param name="node">The facet.</param>
        /// <param name="built">The cells of the views built so far, by view; every child of the facet is in it.</param>
        /// <returns>The group of the facet at the origin and its bounds.</returns>
        private CompositionCell ComposeFacet(LayerNode node, Dictionary<LayerNode, CompositionCell> built)
        {
            _budget.Add(1, node.Source.GetLocation(_sourceName));
            var charts = node.HasCells ? node.Children : [];
            var cells = new List<CompositionCell?>(charts.Count);
            foreach (var chart in charts)
            {
                cells.Add(built[chart]);
            }

            var definition = node.Facet!;
            var layout = node.Layout!;
            int columns;
            if (definition.IsWrap)
            {
                columns = layout.Columns is > 0 ? layout.Columns.Value : Math.Max(1, charts.Count);
            }
            else
            {
                columns = charts.Count == 0 ? 1 : charts.Max(c => c.Cell!.ColumnIndex) + 1;
            }

            layout = FacetLayout(node, definition.IsWrap ? layout.Columns : columns);
            var headers = FacetHeaders(node, charts, cells, columns);
            var placement = CompositionLayout.Place(cells, headers, layout);
            var items = new List<SceneItem>(placement.Items);
            var bounds = AddOwnedLegends(node, items, placement.Bounds);
            return Finish(node, items, bounds);
        }

        /// <summary>
        ///     Gets the layout of a facet: its settings, with the columns of the grid and the alignment that Vega-Lite gives a facet.
        /// </summary>
        /// <remarks>
        ///     The cells are aligned <c>all</c> unless the facet says otherwise; they are not aligned (<c>none</c>) when the facet has no <c>row</c> field and the x scale is independent, or no
        ///     <c>column</c> field and the y scale is independent (the cells then are not the same size in that direction).
        /// </remarks>
        /// <param name="node">The facet.</param>
        /// <param name="columns">The columns of the grid; null for one row.</param>
        /// <returns>The settings.</returns>
        private static CompositionSettings FacetLayout(LayerNode node, int? columns)
        {
            var layout = node.Layout! with { Columns = columns };
            var written = node.Source["align"] is not null || (node.Source["encoding"]?["facet"]?["align"] is not null && node.Facet!.IsWrap);
            var definition = node.Facet!;
            var unaligned = (definition.Row is null && node.Resolve.Scale("x") == ResolveMode.Independent) || (definition.Column is null && node.Resolve.Scale("y") == ResolveMode.Independent);
            return written || !unaligned ? layout : layout with { AlignColumn = CompositionAlign.None, AlignRow = CompositionAlign.None };
        }

        /// <summary>
        ///     Builds the headers, footers and titles of a facet.
        /// </summary>
        /// <param name="node">The facet.</param>
        /// <param name="charts">The charts of its cells, in the order of the grid.</param>
        /// <param name="cells">The built cells, in the same order.</param>
        /// <param name="columns">The number of columns of the grid.</param>
        /// <returns>The headers; empty lists for a facet that has none.</returns>
        private CompositionHeaders FacetHeaders(LayerNode node, IReadOnlyList<LayerNode> charts, IReadOnlyList<CompositionCell?> cells, int columns)
        {
            var definition = node.Facet!;
            var rowChannel = ChannelOf(node, "row");
            var columnChannel = ChannelOf(node, definition.IsWrap ? "facet" : "column");
            var rows = charts.Count == 0 ? 0 : (int)Math.Ceiling(charts.Count / (double)columns);
            var rowHeaders = new List<HeaderGroup?>();
            var rowFooters = new List<HeaderGroup?>();
            var columnHeaders = new List<HeaderGroup?>();
            var columnFooters = new List<HeaderGroup?>();

            // The header of a row is that of the cell in its first column: the label of its value, and the y axes that the cells share
            for (var row = 0; row < rows; row++)
            {
                var index = row * columns;
                var axes = _headerAxesOfChart.GetValueOrDefault(charts[index]) ?? [];
                var height = cells[index]!.Group.Height;
                var label = definition.Row is null ? null : LabelOf(rowChannel, charts[index].Cell!.RowValue);
                var footerLabel = rowChannel.Settings.LabelsInFooter;
                rowHeaders.Add(Header("row-header", footerLabel ? null : label, rowChannel, 0, height, [.. axes.Where(a => a.Orient == "left")], node));
                rowFooters.Add(Header("row-footer", footerLabel ? label : null, rowChannel, 0, height, [.. axes.Where(a => a.Orient == "right")], node));
            }

            // The header of a column is that of the cell in its first row
            for (var column = 0; column < Math.Min(columns, charts.Count); column++)
            {
                var axes = _headerAxesOfChart.GetValueOrDefault(charts[column]) ?? [];
                var width = cells[column]!.Group.Width;
                var label = definition.IsWrap || definition.Column is null ? null : LabelOf(columnChannel, charts[column].Cell!.ColumnValue);
                var footerLabel = columnChannel.Settings.LabelsInFooter;
                columnHeaders.Add(Header("column-header", footerLabel ? null : label, columnChannel, width, 0, [.. axes.Where(a => a.Orient == "top")], node));
                columnFooters.Add(Header("column-footer", footerLabel ? label : null, columnChannel, width, 0, [.. axes.Where(a => a.Orient == "bottom")], node));
            }

            // The titles of the fields; a row and column facet puts them ten pixels outside the headers (Vega-Lite's layout offset), a wrapped facet at the edge of the cells
            var rowTitle = definition.Row is null ? null : TitleOf("row-title", rowChannel);
            var columnTitle = (definition.IsWrap || definition.Column is not null) ? TitleOf("column-title", columnChannel) : null;
            var cellWidth = cells.Count == 0 ? 0 : cells[0]!.Group.Width;
            var cellHeight = cells.Count == 0 ? 0 : cells[0]!.Group.Height;
            return new CompositionHeaders
            {
                RowHeaders = Complete(rowHeaders, "row-header", 0, cellHeight),
                RowFooters = Complete(rowFooters, "row-footer", 0, cellHeight),
                ColumnHeaders = Complete(columnHeaders, "column-header", cellWidth, 0),
                ColumnFooters = Complete(columnFooters, "column-footer", cellWidth, 0),
                RowTitle = rowTitle,
                ColumnTitle = columnTitle,
                RowTitleOffset = definition.Row is null ? 0 : 10,
                ColumnTitleOffset = definition.Column is null ? 0 : 10,
                RowTitleAtEnd = rowChannel.Settings.TitleAtEnd,
                ColumnTitleAtEnd = columnChannel.Settings.TitleAtEnd,
                RowTitleBand = rowChannel.Settings.TitleBand ?? 0.5,
                ColumnTitleBand = columnChannel.Settings.TitleBand ?? 0.5,
            };
        }

        /// <summary>
        ///     Gets the text of the label of a cell value.
        /// </summary>
        /// <param name="channel">The channel of the field.</param>
        /// <param name="value">The value of the field in the cell.</param>
        /// <returns>The lines (a null value is labeled <c>null</c>, as Vega-Lite does); null when the labels are switched off.</returns>
        private static IReadOnlyList<string>? LabelOf(FacetChannel channel, DataValue value)
        {
            if (channel.Label is not { } format)
            {
                return null;
            }

            return value.IsNull ? ["null"] : format(value);
        }

        /// <summary>
        ///     Builds one header or footer of a row or a column.
        /// </summary>
        /// <param name="role">The role of the group.</param>
        /// <param name="label">The lines of the label, or null for none.</param>
        /// <param name="channel">The channel whose header properties the label has.</param>
        /// <param name="width">The width of the group: the width of the cell for a column.</param>
        /// <param name="height">The height of the group: the height of the cell for a row.</param>
        /// <param name="axes">The axes the group holds.</param>
        /// <param name="facet">The facet, for the location of the failure.</param>
        /// <returns>The group; null when it has neither a label nor an axis.</returns>
        private HeaderGroup? Header(string role, IReadOnlyList<string>? label, FacetChannel channel, double width, double height, IReadOnlyList<AxisResult> axes, LayerNode facet) =>
            new TitleBuilder(_measurer).BuildHeader(role, label ?? [], channel.Settings, HeaderPart.Label, width, height, axes, _budget, facet.Source.GetLocation(_sourceName));

        /// <summary>
        ///     Builds the title of a row or a column field.
        /// </summary>
        /// <param name="role">The role: <c>row-title</c> or <c>column-title</c>.</param>
        /// <param name="channel">The channel.</param>
        /// <returns>The group; null when the title is switched off.</returns>
        private HeaderGroup? TitleOf(string role, FacetChannel channel) =>
            new TitleBuilder(_measurer).BuildHeader(role, channel.TitleLines, channel.Settings, HeaderPart.Title, 0, 0, null, _budget, channel.Location);

        /// <summary>
        ///     Turns the optional headers of the rows or columns into the list that the layout takes: none when no row (column) has one, else one for each, an empty group for the ones that have none.
        /// </summary>
        /// <param name="groups">The headers, in order; null for a row or column without one.</param>
        /// <param name="role">The role of the empty groups.</param>
        /// <param name="width">The width of an empty group.</param>
        /// <param name="height">The height of an empty group.</param>
        /// <returns>The headers.</returns>
        private IReadOnlyList<HeaderGroup> Complete(List<HeaderGroup?> groups, string role, double width, double height)
        {
            if (groups.All(g => g is null))
            {
                return [];
            }

            var completed = new List<HeaderGroup>(groups.Count);
            foreach (var group in groups)
            {
                if (group is null)
                {
                    _budget.Add(1, default);
                }

                completed.Add(group ?? new HeaderGroup(new SceneGroup(role, 0, 0) { Width = width, Height = height }, SceneBounds.Empty));
            }

            return completed;
        }

        /// <summary>
        ///     Adds the title to the content of a concatenation or a facet and makes its group.
        /// </summary>
        /// <param name="node">The view.</param>
        /// <param name="items">The content so far.</param>
        /// <param name="bounds">The bounds of the content.</param>
        /// <returns>The group at the origin and its bounds.</returns>
        private CompositionCell Finish(LayerNode node, List<SceneItem> items, SceneBounds bounds)
        {
            // A composition has no data rectangle: its title is anchored at the start of the bounds of everything it holds
            var title = new TitleBuilder(_measurer).Build(node.Source["title"], _config, unitOrLayer: false, _budget, _sourceName);
            if (title is not null)
            {
                var (group, titleBounds) = title.Place(0, 0, bounds.IsEmpty ? new SceneBounds(0, 0, 0, 0) : bounds);
                items.Add(group);
                bounds = bounds.Union(titleBounds);
            }

            return new CompositionCell(
                new SceneGroup("scope", 0, 0)
                {
                    Width = bounds.IsEmpty ? 0 : Math.Max(0, bounds.X2),
                    Height = bounds.IsEmpty ? 0 : Math.Max(0, bounds.Y2),
                    Children = items,
                },
                bounds);
        }
    }
}
