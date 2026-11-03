// Portions derived from Vega-Lite (BSD-3-Clause): normalize/core.ts (mapRepeat, mapLayerRepeat) and normalize/repeater.ts (replaceRepeaterInEncoding); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The values that the <c>repeat</c>s around a view give to its <c>{"repeat": ...}</c> field references.
/// </summary>
/// <param name="Row">The value of <c>{"repeat": "row"}</c>, or null.</param>
/// <param name="Column">The value of <c>{"repeat": "column"}</c>, or null.</param>
/// <param name="Repeat">The value of <c>{"repeat": "repeat"}</c> (an array <c>repeat</c>), or null.</param>
/// <param name="Layer">The value of <c>{"repeat": "layer"}</c>, or null.</param>
internal sealed record RepeaterValues(string? Row, string? Column, string? Repeat, string? Layer)
{
    /// <summary>
    ///     Gets the value of a reference.
    /// </summary>
    /// <param name="name"><c>row</c>, <c>column</c>, <c>repeat</c> or <c>layer</c>.</param>
    /// <returns>The value; null when the repeats around the view define none (or the name is not one of the four).</returns>
    public string? Get(string name) => name switch
    {
        "row" => Row,
        "column" => Column,
        "repeat" => Repeat,
        "layer" => Layer,
        _ => null,
    };

    /// <summary>
    ///     Gets the values with one of them set (an inner <c>repeat</c> overrides the value of an outer one of the same name).
    /// </summary>
    /// <param name="name"><c>row</c>, <c>column</c>, <c>repeat</c> or <c>layer</c>.</param>
    /// <param name="value">The value.</param>
    /// <returns>The new values.</returns>
    public RepeaterValues With(string name, string? value) => name switch
    {
        "row" => this with { Row = value },
        "column" => this with { Column = value },
        "repeat" => this with { Repeat = value },
        _ => this with { Layer = value },
    };
}

/// <summary>
///     The <c>layer</c> list of a <c>repeat</c> and the view it repeats, for a node that stands for the layers of one row and column of the repeat.
/// </summary>
/// <param name="Values">The values of the <c>layer</c> list.</param>
/// <param name="Spec">The <c>spec</c> that is repeated for each value.</param>
internal sealed record RepeatLayers(IReadOnlyList<string> Values, SpecNode Spec);

/// <content>
///     The expansion of <c>repeat</c> into concatenations and layers, and the replacement of the <c>{"repeat": ...}</c> field references.
/// </content>
internal static partial class SpecNormalizer
{
    /// <summary>
    ///     Expands a view with a <c>repeat</c>: a concatenation over the values of an array, or of the product of the rows and the columns (row-major), or a layered view over the layer values.
    /// </summary>
    /// <remarks>
    ///     The expansion reads no data. The number of copies is computed before any copy is made and checked against <see cref="VegaLiteLimits.MaxViews"/>.
    ///     A <c>repeat</c> with <c>row</c> or <c>column</c> and <c>layer</c> is a repeat of rows and columns whose every cell is a repeat of the layers.
    /// </remarks>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the <c>repeat</c>, when the view has no <c>spec</c>.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at the <c>repeat</c>, when the copies would be more views than the limit allows.</exception>
    private static void VisitRepeat(BuildState state, LayerNode node)
    {
        var view = node.Source;
        var repeat = view["repeat"]!;
        if (view["spec"] is not { IsObject: true } spec)
        {
            throw new VegaLiteSpecException("A repeat needs a 'spec' view.", state.Tree.Locate(repeat));
        }

        var depth = EnterContainer(state, node, repeat, NestingMessage(state));
        var inherited = node.Repeater ?? new RepeaterValues(null, null, null, null);
        if (repeat.IsArray)
        {
            var values = StringValues(repeat);
            CheckCopies(state, repeat, values.Count);
            var columns = ReadColumns(view["columns"], state.Config.GetNumber("concat.columns"));
            FinishConcat(state, node, "repeat", ReadLayout(state, view, null, "concat", CompositionAlign.All, columns));
            foreach (var value in values)
            {
                AddCopy(node, spec, depth, inherited.With("repeat", value));
            }

            QueueChildren(state, node);
            return;
        }

        var rows = repeat["row"] is { IsArray: true } rowList ? StringValues(rowList) : null;
        var columnValues = repeat["column"] is { IsArray: true } columnList ? StringValues(columnList) : null;
        var layers = repeat["layer"] is { IsArray: true } layerList ? StringValues(layerList) : null;
        if (rows is null && columnValues is null)
        {
            // Only layers: the repeat is a layered view of the copies of the spec
            CheckCopies(state, repeat, layers?.Count ?? 0);
            node.MakeLayer(LayerResolve.Read(view["resolve"]));
            node.SetChartRoot(ChartOf(node));
            AddLayerCopies(state, node, spec, layers ?? [], depth);
            return;
        }

        // The product is checked in two steps so that it cannot overflow: the cells, then the layers of every cell
        long cells = (long)(rows?.Count ?? 1) * (columnValues?.Count ?? 1);
        CheckCopies(state, repeat, cells);
        if (layers is not null)
        {
            CheckCopies(state, repeat, cells, 1 + (long)layers.Count);
        }

        // The columns are those of the repeat: a row only has one, and 'columns' is dropped
        if (view["columns"] is { } written)
        {
            Warn(state, "vl-composition-columns-ignored", "The 'columns' are ignored for a repeat by 'row' and 'column': the columns are those of the 'column' list.", written);
        }

        FinishConcat(state, node, "repeat", ReadLayout(state, view, null, "concat", CompositionAlign.All, columnValues?.Count ?? 1));
        IReadOnlyList<string?> rowItems = rows is null ? [null] : [.. rows];
        IReadOnlyList<string?> columnItems = columnValues is null ? [null] : [.. columnValues];
        foreach (var row in rowItems)
        {
            foreach (var column in columnItems)
            {
                var values = inherited;
                values = row is null ? values : values.With("row", row);
                values = column is null ? values : values.With("column", column);

                // With layers, every cell is a layered view that has no properties of its own but the size of the repeated view (Vega-Lite takes the size of a layered view from its layers): a synthetic view located at the repeat
                var source = layers is null ? spec : SpecNode.Synthetic(repeat, SpecNodeKind.Object, null, children: SizeOf(spec));
                var copy = AddCopy(node, source, depth, values);
                if (layers is not null)
                {
                    copy.SetLayerRepeat(new RepeatLayers(layers, spec));
                }
            }
        }

        QueueChildren(state, node);
    }

    /// <summary>
    ///     Gets the <c>width</c>, <c>height</c> and <c>view</c> that a repeated view has, which the layered view of its layers takes.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite sizes a layered view from its layers when it has no size of its own, and the layers of a repeat of layers are copies of the <c>spec</c>, so the size
    ///     (and the appearance of the plot area) of the <c>spec</c> is the size of the layered view.
    /// </remarks>
    /// <param name="spec">The repeated view.</param>
    /// <returns>The properties, in the order <c>width</c>, <c>height</c>, <c>view</c>; empty when the view has none.</returns>
    internal static List<SpecNode> SizeOf(SpecNode spec) =>
        [.. new[] { "width", "height", "view" }.Select(name => spec[name]).OfType<SpecNode>()];

    /// <summary>
    ///     Makes a node the layered view of the layers of one cell of a <c>repeat</c> with a <c>layer</c> list.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The node.</param>
    /// <param name="layers">The layer values and the repeated view.</param>
    private static void VisitLayerRepeat(BuildState state, LayerNode node, RepeatLayers layers)
    {
        var depth = EnterContainer(state, node, layers.Spec, $"The layers are nested more than {state.Limits.MaxCompositionDepth} levels deep, the limit (MaxCompositionDepth).");
        node.MakeLayer(LayerResolve.Default);
        node.SetChartRoot(ChartOf(node));
        AddLayerCopies(state, node, layers.Spec, layers.Values, depth);
    }

    /// <summary>
    ///     Adds one copy of a view for each layer value, and queues the copies.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The layered view.</param>
    /// <param name="spec">The repeated view.</param>
    /// <param name="values">The layer values.</param>
    /// <param name="depth">The depth of the copies.</param>
    private static void AddLayerCopies(BuildState state, LayerNode node, SpecNode spec, IReadOnlyList<string> values, int depth)
    {
        var inherited = node.Repeater ?? new RepeaterValues(null, null, null, null);
        foreach (var value in values)
        {
            AddCopy(node, spec, depth, inherited.With("layer", value));
        }

        QueueChildren(state, node);
    }

    /// <summary>
    ///     Adds one copy of the repeated view as a child.
    /// </summary>
    /// <param name="node">The view that holds the copies.</param>
    /// <param name="source">The view object of the copy.</param>
    /// <param name="depth">The depth of the copy.</param>
    /// <param name="values">The values of the copy's repeats.</param>
    /// <returns>The copy.</returns>
    private static LayerNode AddCopy(LayerNode node, SpecNode source, int depth, RepeaterValues values)
    {
        var copy = new LayerNode(source, node, depth);
        copy.SetRepeater(values);
        node.AddChild(copy);
        return copy;
    }

    /// <summary>
    ///     Checks that the views of a repeat do not exceed the limit, before any of them is made.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeat">The <c>repeat</c> the error is located at.</param>
    /// <param name="copies">The number of copies the repeat would add.</param>
    /// <param name="viewsPerCopy">The number of views each copy holds.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the views so far and the copies are more than <see cref="VegaLiteLimits.MaxViews"/>.</exception>
    private static void CheckCopies(BuildState state, SpecNode repeat, long copies, long viewsPerCopy = 1)
    {
        if (ExceedsViews(state.Count + state.Pending.Count, copies, viewsPerCopy, state.Limits.MaxViews))
        {
            throw new VegaLiteRenderException(
                viewsPerCopy == 1
                    ? $"The repeat makes {copies} views, so the specification would have more than {state.Limits.MaxViews} views, the limit (MaxViews)."
                    : $"The repeat makes {copies} cells of {viewsPerCopy} views each, so the specification would have more than {state.Limits.MaxViews} views, the limit (MaxViews).",
                state.Tree.Locate(repeat));
        }
    }

    /// <summary>
    ///     Reads the strings of an array; the parser has validated that they are strings.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <returns>The strings.</returns>
    private static List<string> StringValues(SpecNode array) =>
        [.. array.Items.Where(i => i.IsString).Select(i => i.StringValue!)];

    /// <summary>
    ///     Replaces the <c>{"repeat": ...}</c> references in an encoding by the values of the repeats the view is in.
    /// </summary>
    /// <remarks>
    ///     A reference in the <c>field</c> or the <c>datum</c> of a channel, in the <c>field</c> of a <c>sort</c> and in the <c>field</c> or <c>datum</c> of a
    ///     <c>condition</c> is replaced by a string located at the reference. A reference the repeats do not define is reported (<c>vl-repeat-ref-unresolved</c>, once) and what it
    ///     belongs to is dropped: the channel, the <c>sort</c> or the condition (Vega-Lite's <c>noSuchRepeatedValue</c>).
    /// </remarks>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view whose repeats give the values.</param>
    /// <param name="encoding">The encoding object.</param>
    /// <returns>The encoding itself when it has no reference; otherwise an overlay of it.</returns>
    private static SpecNode ReplaceRepeaters(BuildState state, LayerNode node, SpecNode encoding)
    {
        var channels = ReplaceEach(encoding.Properties, channel => ReplaceRepeaterInChannel(state, node.Repeater, channel));
        return channels is null ? encoding : SpecNode.Overlay(encoding, channels);
    }

    /// <summary>
    ///     Replaces the references in one channel of an encoding: a definition, or an array of definitions.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeater">The values of the repeats, or null.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The channel itself when it has no reference; the replaced channel otherwise; null when it is dropped.</returns>
    private static SpecNode? ReplaceRepeaterInChannel(BuildState state, RepeaterValues? repeater, SpecNode channel)
    {
        if (channel.IsObject)
        {
            return ReplaceRepeaterInDefinition(state, repeater, channel);
        }

        return channel.IsArray ? ReplaceRepeaterInArray(state, repeater, channel) : channel;
    }

    /// <summary>
    ///     Replaces the references in the channel definitions of an array channel (<c>detail</c>, <c>order</c>, <c>tooltip</c>).
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeater">The values of the repeats, or null.</param>
    /// <param name="array">The array.</param>
    /// <returns>The array itself when nothing was replaced; a new array otherwise; null when every definition was dropped.</returns>
    private static SpecNode? ReplaceRepeaterInArray(BuildState state, RepeaterValues? repeater, SpecNode array)
    {
        var items = ReplaceEach(array.Items, item => item.IsObject ? ReplaceRepeaterInDefinition(state, repeater, item) : item);
        if (items is null)
        {
            return array;
        }

        return items.Count == 0 ? null : SpecNode.Synthetic(array, SpecNodeKind.Array, array.Name, children: items);
    }

    /// <summary>
    ///     Replaces the references in one channel definition (or condition).
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeater">The values of the repeats, or null.</param>
    /// <param name="definition">The definition object.</param>
    /// <returns>The definition itself when it has no reference; an overlay of it with the values otherwise; null when its field or datum reference has no value.</returns>
    private static SpecNode? ReplaceRepeaterInDefinition(BuildState state, RepeaterValues? repeater, SpecNode definition)
    {
        var properties = ReplaceEach(
            definition.Properties,
            property =>
            {
                switch (property.Name)
                {
                    case "field" or "datum" when IsRepeatReference(property):
                        return ResolveReference(state, repeater, property);
                    case "sort" when property.IsObject && property["field"] is { } sortField && IsRepeatReference(sortField):
                        return ResolveReference(state, repeater, sortField) is { } resolved
                            ? SpecNode.Overlay(property, property.Properties.Select(p => p.Name == "field" ? resolved : p))
                            : null;
                    case "condition":
                        return ReplaceRepeaterInCondition(state, repeater, property);
                    default:
                        return property;
                }
            });

        // A field or datum without a value drops the whole definition; the other references only drop their own property
        if (properties is not null && definition.Properties.Any(p => p.Name is "field" or "datum" && IsRepeatReference(p) && !HasProperty(properties, p.Name)))
        {
            return null;
        }

        return properties is null ? definition : SpecNode.Overlay(definition, properties);
    }

    /// <summary>
    ///     Replaces the references in the <c>condition</c> of a channel definition: one condition or an array of them.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeater">The values of the repeats, or null.</param>
    /// <param name="condition">The condition value.</param>
    /// <returns>The condition itself when nothing was replaced; the replaced condition otherwise; null when every condition was dropped.</returns>
    private static SpecNode? ReplaceRepeaterInCondition(BuildState state, RepeaterValues? repeater, SpecNode condition)
    {
        if (condition.IsObject)
        {
            return ReplaceRepeaterInDefinition(state, repeater, condition);
        }

        return condition.IsArray ? ReplaceRepeaterInArray(state, repeater, condition) : condition;
    }

    /// <summary>
    ///     Gets the value of a <c>{"repeat": ...}</c> reference as a string located at the reference.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="repeater">The values of the repeats, or null.</param>
    /// <param name="property">The <c>field</c>, <c>datum</c> or <c>sort.field</c> property whose value is the reference.</param>
    /// <returns>A string property of the same name; null (with a warning) when the repeats define no value.</returns>
    private static SpecNode? ResolveReference(BuildState state, RepeaterValues? repeater, SpecNode property)
    {
        var name = property["repeat"]!.StringValue!;
        if (repeater?.Get(name) is { } value)
        {
            return SpecNode.Synthetic(property, SpecNodeKind.String, property.Name, value);
        }

        if (state.UnresolvedReferences.Add(property))
        {
            Warn(
                state,
                "vl-repeat-ref-unresolved",
                $"The reference {{\"repeat\": \"{name}\"}} has no value: the repeats around the view define no '{name}', so what it belongs to is dropped.",
                property);
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a value is a <c>{"repeat": "row" | "column" | "repeat" | "layer"}</c> reference.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> for an object with a string <c>repeat</c>.</returns>
    private static bool IsRepeatReference(SpecNode node) => node.IsObject && node["repeat"] is { IsString: true };

    /// <summary>
    ///     Determines whether a list of properties has a property of a name.
    /// </summary>
    /// <param name="properties">The properties.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when one has the name.</returns>
    private static bool HasProperty(List<SpecNode> properties, string name) => properties.Exists(p => p.Name == name);

    /// <summary>
    ///     Applies a replacement to each node of a list and builds the new list only when something changed.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="replace">Gives the node itself (unchanged), another node, or null to drop it.</param>
    /// <returns>The new list; null when no node changed.</returns>
    private static List<SpecNode>? ReplaceEach(IReadOnlyList<SpecNode> nodes, Func<SpecNode, SpecNode?> replace)
    {
        List<SpecNode>? result = null;
        for (var i = 0; i < nodes.Count; i++)
        {
            var replaced = replace(nodes[i]);
            if (ReferenceEquals(replaced, nodes[i]))
            {
                result?.Add(nodes[i]);
                continue;
            }

            result ??= [.. nodes.Take(i)];
            if (replaced is not null)
            {
                result.Add(replaced);
            }
        }

        return result;
    }
}
