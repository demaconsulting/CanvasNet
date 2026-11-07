// Portions derived from Vega-Lite (BSD-3-Clause): getSelectionComponent (compile/model.ts), parseSelectionExtent (compile/selection/parse.ts) and the scale bindings (compile/selection/scales.ts); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The value that a scale domain or a bin extent takes from a parameter: whether the parameter is a selection, and the value (the selected range or values of the field the extent names).
/// </summary>
/// <param name="IsSelection">Whether the parameter is a selection; when it is not, the caller reads the variable parameter itself.</param>
/// <param name="Value">The selected values of the field (an array for a range or a list); null when the selection holds nothing for the field.</param>
internal readonly record struct SelectionExtentValue(bool IsSelection, DataValue Value);

/// <content>
///     What the later stages ask of the model: the selections that a view sees, the brushes of a unit and the extents that scales and bins follow (a partial file of the selection model unit).
/// </content>
internal sealed partial class SelectionModel
{
    /// <summary>
    ///     Binds the model to the layer tree that is drawn: the units of that tree are the units the components were declared for, in the same document order.
    /// </summary>
    /// <remarks>
    ///     The model is built from the specification as written, before the expression references are folded; the layer tree that is drawn comes from the folded one. Both have the same views in
    ///     the same order, so the units are matched by position. A facet that is instantiated later copies its template, and a copy finds its template (<see cref="LayerNode.Template"/>).
    /// </remarks>
    /// <param name="tree">The layer tree, before any facet is instantiated.</param>
    /// <returns>The model bound to the tree; this model when it declares no selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    public SelectionModel BindUnits(LayerTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (!HasSelections)
        {
            return this;
        }

        var indexes = new Dictionary<LayerNode, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < tree.Units.Count; i++)
        {
            indexes[tree.Units[i]] = i;
        }

        return new SelectionModel(Components, Stores, Values, Warnings, indexes);
    }

    /// <summary>
    ///     Gets the components that a unit view declares, in declaration order (the order Vega-Lite draws their brushes in).
    /// </summary>
    /// <param name="unit">The unit view; a copy of a facet's template is matched with the template.</param>
    /// <returns>The components; none when the model is not bound to the layer tree or the unit declares none.</returns>
    public IEnumerable<SelectionComponent> ComponentsOf(LayerNode unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (_unitIndexes is null || !_unitIndexes.TryGetValue(unit.Template, out var index))
        {
            return [];
        }

        return Components.Where(c => c.UnitIndex == index);
    }

    /// <summary>
    ///     Finds the selection that a view sees under a name: its own, or that of the nearest view above it that has one (Vega-Lite's <c>getSelectionComponent</c>; a view that holds other views
    ///     has the selections of its units, the last unit that declares a name deciding).
    /// </summary>
    /// <param name="view">The view that asks; null asks for the first declaration of the name.</param>
    /// <param name="variableName">The name of the parameter as a variable.</param>
    /// <returns>The component; null when no selection has the name.</returns>
    public SelectionComponent? FindComponent(LayerNode? view, string variableName)
    {
        ArgumentNullException.ThrowIfNull(variableName);
        if (view is null || _unitIndexes is null)
        {
            return Find(variableName);
        }

        for (var node = view; node is not null; node = node.Parent)
        {
            var indexes = UnitIndexesUnder(node);
            SelectionComponent? last = null;
            foreach (var component in Components)
            {
                if (component.VariableName == variableName && indexes.Contains(component.UnitIndex))
                {
                    last = component;
                }
            }

            if (last is not null)
            {
                return last;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds the selection bound to the scale of a channel of a unit: the interval selection with <c>"bind": "scales"</c> that projects the channel over a continuous domain (Vega-Lite's
    ///     <c>scale.set('selectionExtent', {param, field})</c>; the last such selection wins).
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <param name="channel">The channel of the scale.</param>
    /// <returns>The selection and the field it constrains on the channel; null when no selection binds the scale.</returns>
    public (SelectionComponent Component, string Field)? BoundExtentOf(LayerNode unit, string channel)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(channel);
        (SelectionComponent, string)? found = null;
        foreach (var component in ComponentsOf(unit).Where(c => c.IsScaleBound && !c.IsDropped))
        {
            var item = component.Projection.FirstOrDefault(p => p.Channel == channel && ScaleChannels.Contains(channel) && p.Type == "R");
            if (item is not null)
            {
                found = (component, item.Field);
            }
        }

        return found;
    }

    /// <summary>
    ///     Reads the extent that a scale domain or a bin extent takes from a parameter (Vega-Lite's <c>parseSelectionExtent</c>): the selected values of one field of the selection.
    /// </summary>
    /// <remarks>
    ///     The field is the one the extent names; with an encoding, the field the selection projects over it; with neither (or when the encoding does not name exactly one field) the first projected
    ///     field, which is reported when the selection has several. The value is what the top-level signal of the selection holds for the field.
    /// </remarks>
    /// <param name="view">The view that asks (a unit, or the view whose <c>transform</c> asks).</param>
    /// <param name="param">The name of the parameter.</param>
    /// <param name="field">The <c>field</c> of the extent, or null.</param>
    /// <param name="encoding">The <c>encoding</c> of the extent, or null.</param>
    /// <param name="location">The location of the extent, for the reports.</param>
    /// <param name="warnings">Receives the reports; null to not report.</param>
    /// <returns>Whether the parameter is a selection, and the value.</returns>
    public SelectionExtentValue ResolveExtent(LayerNode? view, string param, string? field, string? encoding, VegaLiteLocation location, List<FeatureReport>? warnings)
    {
        ArgumentNullException.ThrowIfNull(param);
        var variableName = SelectionDeclarations.VarName(param);
        if (FindComponent(view, variableName) is not { } component)
        {
            return new SelectionExtentValue(false, default);
        }

        var items = component.Projection;
        if (field is null && encoding is null)
        {
            field = items.Count > 0 ? items[0].Field : null;
            if (items.Count > 1)
            {
                Report(warnings, location, $"The selection '{param}' is projected over several fields and its use as a scale domain or a bin extent names neither a field nor an encoding; the first field, '{field}', is used.");
            }
        }
        else if (encoding is not null && field is null)
        {
            var matching = items.Where(p => p.Channel == encoding).ToList();
            if (matching.Count != 1)
            {
                field = items.Count > 0 ? items[0].Field : null;
                Report(warnings, location, $"The selection '{param}' is not projected over exactly one field of the encoding '{encoding}'; the first field, '{field}', is used.");
            }
            else
            {
                field = matching[0].Field;
            }
        }

        if (field is null || !Values.TryGetValue(variableName, out var resolved) || resolved.ObjectValue is not { } members || !members.TryGetValue(field, out var value))
        {
            return new SelectionExtentValue(true, default);
        }

        return new SelectionExtentValue(true, value);
    }

    /// <summary>
    ///     Gets the positions of the units under a view (the view itself for a unit view).
    /// </summary>
    /// <param name="node">The view.</param>
    /// <returns>The positions, among the units the components were declared for.</returns>
    private HashSet<int> UnitIndexesUnder(LayerNode node)
    {
        var result = new HashSet<int>();
        var pending = new Stack<LayerNode>();
        pending.Push(node);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current.IsUnit && _unitIndexes!.TryGetValue(current.Template, out var index))
            {
                result.Add(index);
            }

            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }

        return result;
    }

    /// <summary>
    ///     Adds a warning of the projection once for a location.
    /// </summary>
    /// <param name="warnings">The list, or null.</param>
    /// <param name="location">The location.</param>
    /// <param name="message">The message.</param>
    private static void Report(List<FeatureReport>? warnings, VegaLiteLocation location, string message)
    {
        const string id = "vl-param-selection-projection";
        if (warnings is not null && !warnings.Any(w => w.Id == id && w.Location == location))
        {
            warnings.Add(new FeatureReport(id, message, location, FeatureReportKind.Warning));
        }
    }
}
