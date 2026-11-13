// Portions derived from Vega-Lite (BSD-3-Clause): compile/axis/parse.ts (parseLayerAxes) and compile/resolve.ts; see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <content>
///     The layers of a view: the data walk that applies the inherited data and transforms, and the guides of the layers that share or keep apart their axes and legends.
/// </content>
/// <remarks>
///     <para>
///         A view without <c>layer</c> is one layer, the view itself, and takes exactly the steps it always took. For a layered view the <see cref="SpecNormalizer"/> yields the
///         layers (unit views with their merged encodings); the data of a view is loaded where the nearest <c>data</c> is, its <c>transform</c> runs on it before the views below see the
///         table (so a top-level <c>calculate</c> is applied once and every layer reads the calculated field), and each layer then runs its own transforms and the implicit
///         bin, aggregate and stack of its encoding.
///     </para>
///     <para>
///         The axes follow Vega-Lite's <c>parseLayerAxes</c>: the layers that share a position scale and do not resolve their axis as independent share one axis (a written title of a layer wins
///         over the titles derived from fields, which join as <c>a, b</c>); otherwise each group has an axis of its own, a second axis moves to the opposite side unless an <c>axis.orient</c> says otherwise, and only the first
///         has a grid. The legends are planned over all the layers (<see cref="LegendPlan"/>).
///     </para>
/// </remarks>
public sealed partial class VegaLiteDocument
{
    /// <summary>
    ///     One layer that is drawn: its unit view and the tables of its pipeline.
    /// </summary>
    /// <param name="Node">The unit view in the layer tree.</param>
    /// <param name="Plan">The plan of the layer's encoding (field names, titles, bins, stack).</param>
    /// <param name="Data">The Raw and Main tables of the layer's implicit pipeline.</param>
    private sealed record LayerData(LayerNode Node, EncodingPlan Plan, EncodingData Data);

    /// <summary>
    ///     One layer that is drawn, with its scales.
    /// </summary>
    /// <param name="Node">The unit view in the layer tree.</param>
    /// <param name="Plan">The plan of the layer's encoding (field names, titles, bins, stack).</param>
    /// <param name="Data">The Raw and Main tables of the layer's implicit pipeline; the marks draw the Main table.</param>
    /// <param name="Scales">The scales the layer's marks use.</param>
    /// <param name="Selections">The selections of the specification, bound to the layer tree (the brushes of the layer's selections are drawn around the marks); null when there are none.</param>
    private sealed record LayerChart(LayerNode Node, EncodingPlan Plan, EncodingData Data, ResolvedScales Scales, SelectionModel? Selections = null)
    {
        /// <summary>Gets the unit view that the stages after the specification read.</summary>
        public SpecNode Unit => Node.Unit!;
    }

    /// <summary>
    ///     What the evaluation of the parameters leaves for the rest of the call: the calendar, the parameter values and the evaluator whose step budget is the call's.
    /// </summary>
    /// <param name="Calendar">The calendar of the chart (the time zone of the options).</param>
    /// <param name="Parameters">The values of the variable parameters by name; the evaluator reads this dictionary.</param>
    /// <param name="Evaluator">The evaluator of the call; one thread only.</param>
    private sealed record EvaluationState(TimeCalendar Calendar, Dictionary<string, DataValue> Parameters, ExpressionEvaluator Evaluator);

    /// <summary>
    ///     What the data stage produced.
    /// </summary>
    /// <param name="Layers">The layers that are drawn, in document order.</param>
    /// <param name="Warnings">The findings of the data stage: the loader's, then those of the transforms.</param>
    /// <param name="Expressions">The compiled expressions and the evaluator of the call.</param>
    private sealed record DataStage(IReadOnlyList<LayerData> Layers, IReadOnlyList<FeatureReport> Warnings, RowExpressions Expressions);

    /// <summary>
    ///     One axis that is drawn: the channel, the definition that decides its properties, its scale and where it goes.
    /// </summary>
    /// <param name="Channel">The channel, <c>x</c> or <c>y</c>.</param>
    /// <param name="Definition">The channel definition whose <c>axis</c>, <c>title</c> and field the axis reads.</param>
    /// <param name="Scale">The scale of the axis, with the titles of all the layers that share it.</param>
    /// <param name="MarkType">The mark type of the layer that the definition comes from.</param>
    /// <param name="DefaultOrient">The side when the axis names none, or null for the usual side.</param>
    /// <param name="DefaultGrid">Whether the axis has a grid when it says nothing, or null for the usual rule.</param>
    /// <param name="Members">The indexes (in the list of layers the axes were planned for) of the layers that share the axis.</param>
    /// <param name="Owner">The highest view that shares the axis with its layers (the view that owns it; a layer for an axis of its own).</param>
    private sealed record AxisPlan(string Channel, SpecNode Definition, ResolvedScale Scale, string? MarkType, string? DefaultOrient, bool? DefaultGrid, IReadOnlyList<int> Members, LayerNode Owner);

    /// <summary>
    ///     A group of layers that share one axis.
    /// </summary>
    /// <param name="Members">The indexes of the layers.</param>
    /// <param name="Owner">The highest view that shares the axis, or the layer itself for an axis of its own.</param>
    private sealed record AxisGroup(List<int> Members, LayerNode Owner);

    /// <summary>
    ///     Reads the mark type of a unit view.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <returns>The mark type; null when the view has no mark.</returns>
    private static string? MarkTypeOf(SpecNode unit)
    {
        var mark = unit["mark"];
        return mark is { IsObject: true } ? mark["type"]?.AsString() : mark?.AsString();
    }

    /// <summary>
    ///     Gets the nodes that are on the way to a layer that is drawn: the layers and the layered views above them.
    /// </summary>
    /// <param name="drawn">The layers that are drawn.</param>
    /// <returns>The nodes.</returns>
    private static HashSet<LayerNode> NodesToVisit(IEnumerable<LayerNode> drawn)
    {
        var needed = new HashSet<LayerNode>();
        foreach (var unit in drawn)
        {
            var node = unit;
            while (needed.Add(node) && node.Parent is { } parent)
            {
                node = parent;
            }
        }

        return needed;
    }

    /// <summary>
    ///     Loads the data and runs the transforms down the view tree, and the implicit pipeline of each unit view that is drawn.
    /// </summary>
    /// <remarks>
    ///     The walk is generic over the nodes of the tree (a unit view, a layered view, and the concatenations and facets of a composition). The data is loaded where the nearest
    ///     <c>data</c> is, once for each <c>data</c> object of the specification (<see cref="LayerTree.DataSites"/>: the copies of a repeated view and the cells of a facet read one
    ///     table, which is shared, never copied, by the views that inherit it) with the fields that every view that reads it needs as dates
    ///     (<see cref="TemporalFields.CollectAll(IEnumerable{SpecNode}, IEnumerable{FacetDefinition})"/>); a view's <c>transform</c> runs before the views below it read the table.
    ///     One loader (file cache, file count, row budget), one evaluator (steps) and one set of parameters serve the whole call; the evaluator and the parameters are those that evaluated the parameters before the data was read.
    /// </remarks>
    /// <param name="tree">The layer tree.</param>
    /// <param name="specification">The specification tree whose expressions the run evaluates: the parsed one, with the expressions of the composite marks' expansion.</param>
    /// <param name="drawn">The layers that are drawn, in document order.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="options">The effective options.</param>
    /// <param name="evaluation">The calendar, the parameter values and the evaluator of the call.</param>
    /// <param name="selections">The selections of the specification, bound to the layer tree (a bin extent can follow a selection).</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <param name="dataWarningSink">Receives the findings as soon as they are known, so that a later failure can still report them; null to not collect them.</param>
    /// <returns>The layers with their tables, the findings and the expressions.</returns>
    private DataStage RunDataStage(LayerTree tree, SpecTree specification, IReadOnlyList<LayerNode> drawn, ResolvedConfig config, VegaLiteRenderOptions options, EvaluationState evaluation, SelectionModel selections, CancellationToken cancellationToken, List<FeatureReport>? dataWarningSink)
    {
        // The data is read first, before any scene item is charged and before any scale exists: one loader (so one file
        // cache, one file count and one row budget) serves this call, from the places and under the file-access policy that the effective open options
        // say, under the limits of the render options
        var root = _tree.Root;

        // A facet is instantiated by this walk, whether or not a view below it is drawn (its cells are drawn empty then), so it is visited too
        var needed = NodesToVisit(drawn.Concat(tree.Compositions.Where(c => c.Kind == ViewKind.Facet)));
        var calendar = evaluation.Calendar;
        var loader = new DataLoader(_tree.SourceName, options.Limits, Options, root["datasets"], options.Datasets, cancellationToken, calendar: calendar);
        var sites = tree.DataSites(needed);
        var siteOfData = new Dictionary<SpecNode, DataSite>(ReferenceEqualityComparer.Instance);
        foreach (var candidate in sites.Where(s => s.Data is not null))
        {
            siteOfData[candidate.Data!] = candidate;
        }
        var rootSite = sites[0];
        var rootTable = loader.Load(root["data"], TemporalFields.CollectAll(rootSite.Views, rootSite.Facets));
        var loaded = new Dictionary<SpecNode, DataTable>(ReferenceEqualityComparer.Instance);

        // Recorded as soon as the data is loaded, so a later failure of the build can still deliver them
        var reported = loader.Warnings.Count;
        dataWarningSink?.AddRange(loader.Warnings);

        // The parameters were evaluated before the data stage; the row transforms run in the order of the specification, on the
        // loaded rows and before any scale exists; they share the loader (row budget, file access) and the evaluator (steps)
        var parameters = evaluation.Parameters;
        var evaluator = evaluation.Evaluator;
        var expressions = new RowExpressions(specification.Expressions, evaluator);
        var transformWarnings = new List<FeatureReport>();
        var layers = new List<LayerData>(drawn.Count);
        var units = new HashSet<LayerNode>(drawn);
        try
        {
            var transformContext = new TransformContext(specification, loader, evaluator, parameters, options.Limits, cancellationToken, transformWarnings) { Selections = selections };

            // Depth first in document order: a view's table is the table of the nearest data above it, with the transforms of the views on the way applied
            var pending = new Stack<(LayerNode Node, DataTable Inherited)>();
            pending.Push((tree.Root, rootTable));
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (node, inherited) = pending.Pop();
                var nodeContext = transformContext with { View = node };
                var table = inherited;
                if (!ReferenceEquals(node, tree.Root) && node.Data is { } data)
                {
                    // A table is loaded once for each data object, however many views (copies of a repeat, cells of a facet) declare it
                    if (!loaded.TryGetValue(data, out var own))
                    {
                        var site = siteOfData[data];
                        own = loader.Load(data, TemporalFields.CollectAll(site.Views, site.Facets));
                        loaded[data] = own;
                    }

                    table = own;
                }

                table = RowTransforms.Apply(table, node.Transform, nodeContext);
                if (units.Contains(node))
                {
                    // Then the encoding's own bin, aggregate and stack, in Vega-Lite's order, on the rows the transforms left; the conditions that test a selection projected over a time unit
                    // need the derived field, which Vega-Lite's time unit node adds above the data of the unit
                    var plan = EncodingPlan.Create(node.Unit!, config);
                    var unitTable = SelectionTimeUnits.Apply(table, SelectionTimeUnits.ConditionsOf(node.Unit!), nodeContext, nodeContext.Locate(node.Unit!));
                    layers.Add(new LayerData(node, plan, EncodingTransforms.Apply(plan, unitTable, nodeContext)));
                }

                // A facet splits its rows into the cells and replaces its template by a copy for each cell; the copies are walked like any view, with the rows of their cell
                IReadOnlyList<FacetCellData>? cellTables = null;
                if (node.Kind == ViewKind.Facet && !node.HasCells)
                {
                    cellTables = InstantiateFacet(tree, node, table, nodeContext, needed, units, config);
                }

                for (var i = node.Children.Count - 1; i >= 0; i--)
                {
                    if (needed.Contains(node.Children[i]))
                    {
                        var child = node.Children[i];
                        pending.Push((child, cellTables is null ? table : cellTables[child.Cell!.Index].Table));
                    }
                }
            }
        }
        finally
        {
            // Findings of a lookup source come before those of the transforms; on a failure they are still delivered
            dataWarningSink?.AddRange(loader.Warnings.Skip(reported));
            dataWarningSink?.AddRange(transformWarnings);
        }

        return new DataStage(layers, [.. loader.Warnings, .. transformWarnings], expressions);
    }

    /// <summary>
    ///     Splits the rows of a facet into its cells and gives the facet a copy of its template for each cell.
    /// </summary>
    /// <remarks>
    ///     The cells are those of the rows of the facet's own table (after its own transforms), also when the view below the facet has data of its own: that data is then read in
    ///     every cell and is not partitioned (Vega-Lite's rule). The copies of the units that are drawn join the units that the walk builds a layer for, and the views above them join
    ///     the views it visits.
    /// </remarks>
    /// <param name="tree">The layer tree, which belongs to this call.</param>
    /// <param name="facet">The facet.</param>
    /// <param name="table">The rows of the facet.</param>
    /// <param name="context">The context of the transforms (row budget, limits, token).</param>
    /// <param name="needed">The views that the walk visits; receives the views of the cells that are drawn.</param>
    /// <param name="units">The units that the walk builds a layer for; receives the units of the cells that are drawn.</param>
    /// <param name="config">The effective configuration (the number format of the text of a binned facet field).</param>
    /// <returns>The cells with their rows, in the order of the facet's children.</returns>
    private static IReadOnlyList<FacetCellData> InstantiateFacet(LayerTree tree, LayerNode facet, DataTable table, TransformContext context, HashSet<LayerNode> needed, HashSet<LayerNode> units, ResolvedConfig config)
    {
        var (perCell, outside) = tree.FacetViews(facet);
        var partition = EncodingTransforms.Facet(facet.Facet!, table, perCell, outside, context, config);
        tree.InstantiateFacet(facet, partition.Infos);

        // The copies of the units that are drawn: a walk below the facet (the tree is small enough: the number of views was checked against MaxViews)
        var below = new Stack<LayerNode>(facet.Children);
        var drawnCopies = new List<LayerNode>();
        while (below.Count > 0)
        {
            var node = below.Pop();
            if (node.IsUnit && MarkBuilder.IsSupported(MarkTypeOf(node.Unit!)))
            {
                drawnCopies.Add(node);
                units.Add(node);
            }

            foreach (var child in node.Children)
            {
                below.Push(child);
            }
        }

        needed.UnionWith(NodesToVisit(drawnCopies));
        return partition.Cells;
    }

    /// <summary>
    ///     Plans the axes of the position channels: one axis for the layers that share a scale and an axis, one for each of the others.
    /// </summary>
    /// <remarks>
    ///     The layers are those of the whole tree (every chart of a composition); the second axis of a channel, its side and its grid are decided chart by chart (the charts of a
    ///     concatenation each have their own first axis), so the plan of a single or layered view is the plan of its one chart.
    /// </remarks>
    /// <param name="layers">The layers with their scales.</param>
    /// <param name="scales">The scales of the layers and of the whole view.</param>
    /// <returns>The axes in the order x, then y, and in layer order within a channel.</returns>
    private static List<AxisPlan> PlanAxes(IReadOnlyList<LayerChart> layers, LayeredScales scales)
    {
        var plans = new List<AxisPlan>(2);
        foreach (var channel in new[] { "x", "y" })
        {
            var carriers = Enumerable.Range(0, layers.Count)
                .Where(i => layers[i].Scales[channel] is not null && layers[i].Unit["encoding"]?[channel] is { IsObject: true })
                .ToList();
            if (carriers.Count == 0)
            {
                continue;
            }

            var channelPlans = new List<AxisPlan>();
            foreach (var group in GroupAxes(layers, scales, channel, carriers))
            {
                channelPlans.Add(PlanAxis(layers, channel, group));
            }

            // The second axis of a channel is one of the same chart: the charts of a concatenation each have their own axes, so the axes are separated chart by chart
            foreach (var chart in channelPlans.GroupBy(p => p.Owner.ChartRoot ?? p.Owner, ReferenceEqualityComparer.Instance))
            {
                var ofChart = chart.ToList();
                plans.AddRange(ofChart.Count > 1 ? SeparateAxes(channel, ofChart) : ofChart);
            }
        }

        return plans;
    }

    /// <summary>
    ///     Divides the layers that carry a position channel into the groups that share one axis.
    /// </summary>
    /// <remarks>
    ///     Layers share an axis when they share the scale and no layered view above them (up to the highest one such that every one on the way resolves the axis as shared) separates them.
    ///     Layers that name different sides for the axis (<c>axis.orient</c>) cannot share it.
    /// </remarks>
    /// <param name="layers">The layers.</param>
    /// <param name="scales">The scales of the layers and of the whole view.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="carriers">The indexes of the layers that carry the channel.</param>
    /// <returns>The groups of layer indexes and their owners, in order of their first layer.</returns>
    private static List<AxisGroup> GroupAxes(IReadOnlyList<LayerChart> layers, LayeredScales scales, string channel, List<int> carriers)
    {
        var scaleGroups = scales.Groups.TryGetValue(channel, out var found) ? found : [];
        var byKey = new Dictionary<(int Scale, LayerNode Top), AxisGroup>();
        var order = new List<AxisGroup>();
        foreach (var index in carriers)
        {
            var top = layers[index].Node;
            while (top.Parent is { } parent && parent.Resolve.Axis(channel) == ResolveMode.Shared)
            {
                top = parent;
            }

            var scaleGroup = scaleGroups.ToList().FindIndex(g => g.Contains(index));
            if (!byKey.TryGetValue((scaleGroup, top), out var group))
            {
                group = new AxisGroup([], top);
                byKey[(scaleGroup, top)] = group;
                order.Add(group);
            }

            group.Members.Add(index);
        }

        // Axes that name different sides cannot be merged into one
        var result = new List<AxisGroup>(order.Count);
        foreach (var group in order)
        {
            var orients = group.Members.Select(i => layers[i].Unit["encoding"]![channel]!["axis"]?["orient"]?.AsString()).Where(o => o is not null).Distinct().Count();
            if (orients > 1)
            {
                result.AddRange(group.Members.Select(i => new AxisGroup([i], layers[i].Node)));
            }
            else
            {
                result.Add(group);
            }
        }

        return result;
    }

    /// <summary>
    ///     Plans the axis that a group of layers shares.
    /// </summary>
    /// <remarks>
    ///     The scale is that of the first layer. The title of the axis merges the titles of the layers as <see cref="GuideTitle"/> says (Vega-Lite's <c>mergeAxisComponent</c>): a written title wins
    ///     over a derived one, the derived titles are joined by a comma, and different written titles are joined too. The channel definition is the one that decides the properties of the axis: a
    ///     definition that switches the axis off wins (an explicit <c>null</c> merges over an implicit axis, as in Vega-Lite), then the first that has an <c>axis</c>, then the first that has a field.
    /// </remarks>
    /// <param name="layers">The layers.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="group">The layers that share the axis, and the view that owns it.</param>
    /// <returns>The plan.</returns>
    private static AxisPlan PlanAxis(IReadOnlyList<LayerChart> layers, string channel, AxisGroup group)
    {
        var members = group.Members;
        var definitions = members.Select(i => (Index: i, Node: layers[i].Unit["encoding"]![channel]!)).ToList();
        var pick = definitions.FindIndex(d => d.Node["axis"] is { IsNull: true } or { IsBoolean: true, BoolValue: false });
        if (pick < 0)
        {
            pick = definitions.FindIndex(d => d.Node["axis"] is { IsObject: true });
        }

        if (pick < 0)
        {
            pick = definitions.FindIndex(d => d.Node["field"] is not null || d.Node["aggregate"] is { IsNull: false });
        }

        var chosen = definitions[Math.Max(pick, 0)];
        var scale = layers[members[0]].Scales[channel]!;
        var definition = chosen.Node;
        if (members.Count > 1)
        {
            // The titles of the layers merge as Vega-Lite's mergeAxisComponent does: a written title (axis.title, the title of the channel definition, which a composite mark also writes
            // for the fields it computes) wins over a derived one (Mean of x, the field); equal titles are one, others join as a, b. An axis that is switched off has no title
            var merged = members
                .Where(m => layers[m].Unit["encoding"]![channel]!["axis"] is not ({ IsNull: true } or { IsBoolean: true, BoolValue: false }))
                .Select(m => WrittenOrDerivedTitle(layers[m], channel))
                .Aggregate((GuideTitle?)null, (current, next) => current is null ? next : GuideTitle.Merge(current, next));
            if (merged is { IsExplicit: true } written)
            {
                definition = WithTitle(definition, written.Lines);
            }
            else if (merged is not null)
            {
                scale = scale with { Title = merged.Resolve()?[0] };
            }
        }

        return new AxisPlan(channel, definition, scale, layers[chosen.Index].Scales.MarkType, null, null, members, group.Owner);
    }

    /// <summary>
    ///     Gets the title that one layer gives the axis of a channel: the written <c>axis.title</c>, else the written <c>title</c> of the channel definition, else the title derived from the field.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The title.</returns>
    private static GuideTitle WrittenOrDerivedTitle(LayerChart layer, string channel)
    {
        var definition = layer.Unit["encoding"]![channel]!;
        var node = (definition["axis"] is { IsObject: true } axis ? axis["title"] : null) ?? definition["title"];
        if (node is not null)
        {
            return GuideTitle.FromWritten(node);
        }

        var own = layer.Scales[channel]!;
        return GuideTitle.FromField(own.Title is { Length: > 0 } title ? title : own.FieldPath);
    }

    /// <summary>
    ///     Gives a channel definition a written title that the axis shows: the <c>title</c> of the definition becomes the given lines and the <c>axis.title</c> (which would win) is dropped.
    /// </summary>
    /// <param name="definition">The channel definition.</param>
    /// <param name="lines">The lines of the title; empty for a title without text, null for no title.</param>
    /// <returns>The definition.</returns>
    private static SpecNode WithTitle(SpecNode definition, IReadOnlyList<string>? lines)
    {
        var title = lines switch
        {
            null => SpecNode.Synthetic(definition, SpecNodeKind.Null, "title"),
            { Count: 0 } => SpecNode.Synthetic(definition, SpecNodeKind.String, "title", string.Empty),
            { Count: 1 } => SpecNode.Synthetic(definition, SpecNodeKind.String, "title", lines[0]),
            _ => SpecNode.Synthetic(definition, SpecNodeKind.Array, "title", children: lines.Select(l => SpecNode.Synthetic(definition, SpecNodeKind.String, null, l))),
        };
        var properties = definition.Properties
            .Where(p => p.Name != "title")
            .Select(p => p.Name == "axis" && p.IsObject ? SpecNode.Overlay(p, p.Properties.Where(a => a.Name != "title")) : p)
            .Append(title);
        return SpecNode.Overlay(definition, properties);
    }

    /// <summary>
    ///     Places the axes of a channel that has several: Vega-Lite's <c>parseLayerAxes</c> moves an axis to the opposite side when more axes are on its side than on the other and it names no
    ///     side, and keeps the grid of the first axis only.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="plans">The axes in layer order.</param>
    /// <returns>The axes with their default sides and grids.</returns>
    private static List<AxisPlan> SeparateAxes(string channel, List<AxisPlan> plans)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal) { ["top"] = 0, ["bottom"] = 0, ["left"] = 0, ["right"] = 0 };
        var result = new List<AxisPlan>(plans.Count);
        for (var i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            var explicitOrient = plan.Definition["axis"]?["orient"]?.AsString();
            var orient = explicitOrient ?? (channel == "x" ? "bottom" : "left");
            if (!counts.ContainsKey(orient))
            {
                // A side that is not valid for the channel is an error of the axis builder
                result.Add(plan with { DefaultGrid = i > 0 ? false : null });
                continue;
            }

            var opposite = Opposite(orient);
            string? defaultOrient = null;
            if (explicitOrient is null && counts[orient] > 0 && counts[orient] > counts[opposite])
            {
                defaultOrient = opposite;
            }

            // Vega-Lite counts the side the axis had before it moved
            counts[orient]++;
            result.Add(plan with { DefaultOrient = defaultOrient, DefaultGrid = i > 0 ? false : null });
        }

        return result;
    }

    /// <summary>
    ///     Gets the side of the data rectangle opposite to another.
    /// </summary>
    /// <param name="orient">The side.</param>
    /// <returns>The opposite side.</returns>
    private static string Opposite(string orient) => orient switch
    {
        "top" => "bottom",
        "bottom" => "top",
        "left" => "right",
        _ => "left",
    };

    /// <summary>
    ///     Gives each layer the scope of its legends: layers share the legend of a channel when they share its scale and no layered view above them makes the legend independent.
    ///     The layer's node is passed on, so that <see cref="LegendPlan.Owners"/> and <see cref="LegendLayer.OwnerOf"/> know the view that owns each legend (the highest view that shares it).
    /// </summary>
    /// <param name="layers">The layers with their scales.</param>
    /// <param name="scales">The scales of the layers and of the whole view.</param>
    /// <returns>The layers for the legend plan.</returns>
    private static List<LegendLayer> LegendLayers(IReadOnlyList<LayerChart> layers, LayeredScales scales)
    {
        var ids = new Dictionary<LayerNode, int>();
        var result = new List<LegendLayer>(layers.Count);
        for (var i = 0; i < layers.Count; i++)
        {
            var index = i;
            result.Add(new LegendLayer(
                layers[i].Unit,
                layers[i].Scales,
                channel =>
                {
                    var top = layers[index].Node;
                    while (top.Parent is { } parent && parent.Resolve.Legend(channel) == ResolveMode.Shared)
                    {
                        top = parent;
                    }

                    if (!ids.TryGetValue(top, out var id))
                    {
                        id = ids.Count;
                        ids[top] = id;
                    }

                    var scaleGroup = scales.Groups.TryGetValue(channel, out var groups) ? groups.ToList().FindIndex(g => g.Contains(index)) : -1;
                    return string.Create(CultureInfo.InvariantCulture, $"{id}:{scaleGroup}");
                },
                layers[i].Node));
        }

        return result;
    }
}
