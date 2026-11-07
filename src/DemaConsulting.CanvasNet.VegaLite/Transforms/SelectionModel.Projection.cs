// Portions derived from Vega-Lite (BSD-3-Clause): the projection and the initial tuples of a selection (compile/selection/project.ts, assemble.ts); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The creation of the model: the projection of each selection and its initial tuples (a partial file of the selection model unit).
/// </content>
internal sealed partial class SelectionModel
{
    /// <summary>The channels that have a scale (Vega-Lite's <c>isScaleChannel</c>).</summary>
    private static readonly HashSet<string> ScaleChannels = new(
        [
            "x", "y", "xOffset", "yOffset", "theta", "radius", "color", "fill", "stroke", "opacity", "fillOpacity", "strokeOpacity", "strokeWidth",
            "strokeDash", "size", "angle", "shape", "time",
        ],
        StringComparer.Ordinal);

    /// <summary>
    ///     Creates the model of a specification that declares selections.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The initial value of a selection is its <c>value</c>, or, when the caller passed one for the name (<paramref name="overrides"/>), that value instead (a caller's <see langword="null"/> clears it).
    ///         The caller's value has the same shapes as <c>value</c>: a mapping from the channels or fields to values (an interval to a range), a list of mappings, or a scalar. Dates of a <c>DateTime</c>
    ///         object in the specification are read in the calendar of the call (<paramref name="calendar"/>). The resolved value of each selection is computed with
    ///         <see cref="SelectionTestSemantics.Resolve"/>, charged to <paramref name="context"/>.
    ///     </para>
    ///     <para>Creating the model reads no data and does no work that grows with the size of the data.</para>
    /// </remarks>
    /// <param name="tree">The specification, which locates the reports.</param>
    /// <param name="layers">The layer tree of the specification.</param>
    /// <param name="config">The resolved configuration (the defaults of the selection types).</param>
    /// <param name="overrides">The values the caller supplied for parameters, or null.</param>
    /// <param name="calendar">The calendar that dates of the specification are read in.</param>
    /// <param name="context">The evaluation state that the resolution is charged to.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="overrides"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the value of the caller has a text longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
    public static SelectionModel Create(
        SpecTree tree,
        LayerTree layers,
        ResolvedConfig config,
        IReadOnlyDictionary<string, object?>? overrides,
        TimeCalendar calendar,
        ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(context);
        var assignments = SelectionDeclarations.Assign(layers);
        if (assignments.Count == 0)
        {
            return Empty;
        }

        var unitIndexes = new Dictionary<LayerNode, int>();
        for (var i = 0; i < layers.Units.Count; i++)
        {
            unitIndexes[layers.Units[i]] = i;
        }

        var components = new List<SelectionComponent>();
        var warnings = new List<FeatureReport>();
        var rowIdentityFromCaller = new HashSet<SelectionComponent>();
        foreach (var assignment in assignments)
        {
            if (SelectionDeclarations.TypeOf(assignment.Declaration) is not { } type || SelectionDeclarations.NameOf(assignment.Declaration) is not { } name)
            {
                continue;
            }

            var override_ = overrides is not null && overrides.TryGetValue(name, out var supplied) ? (true, supplied) : (false, null);
            var component = Declare(tree, assignment, name, type, unitIndexes, config, override_, calendar, warnings);
            components.Add(component);
            if (override_.Item1 && component.UsesRowIdentity && component.Init.Count > 0)
            {
                rowIdentityFromCaller.Add(component);
            }
        }

        // The store of a name is the one of its first declaration; a dropped selection and a projection over row identities have no tuples
        var stores = new Dictionary<string, SelectionStore>(StringComparer.Ordinal);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            var storeName = component.VariableName + "_store";
            if (stores.ContainsKey(storeName))
            {
                continue;
            }

            var empty = component.IsDropped || component.UsesRowIdentity;
            stores[storeName] = new SelectionStore(
                storeName,
                empty ? [] : [.. component.Init.Select(tuple => new SelectionEntry(component.UnitName, component.Projection, tuple))]);
            if (rowIdentityFromCaller.Contains(component))
            {
                warnings.Add(new FeatureReport(
                    "vl-param-selection-id",
                    $"The selection '{component.Name}' is projected over the identity of the rows (it names no fields or encodings), which is not modeled; the value of the caller is ignored and the selection is empty.",
                    tree.Locate(component.Declaration),
                    FeatureReportKind.Warning));
            }

            var operation = component.Resolve == "global" ? SelectionTestSemantics.Union : component.Resolve;
            values[component.VariableName] = SelectionTestSemantics.Resolve(stores[storeName], operation, component.Type == "point", true, context);
        }

        // A selection bound to the scales of a unit that is not the whole view (a unit of a concatenation, a facet or a repeat) has its top-level signal rebuilt from the signals of the
        // scales, which hold no value in a static chart: the selection reads as nothing there, whatever its initial value says
        var empties = components
            .Where(c => c.IsScaleBound && !c.IsDropped && !c.IsTopLevelView && c.Projection.Any(p => p.Channel is not null && p.Type == "R" && ScaleChannels.Contains(p.Channel)))
            .Select(c => c.VariableName)
            .Distinct(StringComparer.Ordinal);
        foreach (var name in empties)
        {
            values[name] = DataValue.FromObject(DataObject.Create(Enumerable.Empty<KeyValuePair<string, DataValue>>()));
        }

        return new SelectionModel(components, stores, values, warnings);
    }

    /// <summary>
    ///     Declares one selection for one unit: projects it and reads its initial tuples.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <param name="assignment">The declaration and the unit.</param>
    /// <param name="name">The name of the selection.</param>
    /// <param name="type">The type: <c>point</c> or <c>interval</c>.</param>
    /// <param name="unitIndexes">The positions of the units.</param>
    /// <param name="config">The resolved configuration.</param>
    /// <param name="caller">Whether the caller supplied a value, and the value.</param>
    /// <param name="calendar">The calendar of the dates.</param>
    /// <param name="warnings">Receives the warnings.</param>
    /// <returns>The component.</returns>
    private static SelectionComponent Declare(
        SpecTree tree,
        SelectionDeclarations.Assignment assignment,
        string name,
        string type,
        Dictionary<LayerNode, int> unitIndexes,
        ResolvedConfig config,
        (bool Supplied, object? Value) caller,
        TimeCalendar calendar,
        List<FeatureReport> warnings)
    {
        var declaration = assignment.Declaration;
        var unit = assignment.Unit;
        var bind = BindOf(declaration);
        var resolve = ResolveOf(declaration, type, config);
        if (assignment.IsDuplicate)
        {
            return new SelectionComponent
            {
                Name = name,
                VariableName = SelectionDeclarations.VarName(name),
                Type = type,
                Resolve = resolve,
                Declaration = declaration,
                UnitIndex = unitIndexes[unit],
                UnitName = SelectionDeclarations.UnitNameOf(unit),
                Projection = [],
                Init = [],
                Bind = bind,
                IsDropped = true,
            };
        }

        // The initial value: the caller's, otherwise the specification's
        List<DataValue>? init = null;
        if (caller.Supplied)
        {
            init = AsList(DatesToNumbers(ParameterEnvironment.FromCaller(caller.Value, tree.Locate(declaration))));
        }
        else if (SelectionDeclarations.ValueOf(declaration) is { } value)
        {
            if (value.IsArray)
            {
                init = [.. value.Items.Select(item => ReadTuple(item, calendar))];
            }
            else
            {
                init = value.IsNull ? [] : [ReadTuple(value, calendar)];
            }
        }

        var projection = Project(tree, declaration, name, type, unit, config, init, warnings, out var items, out var timeUnits);
        var tuples = init is null
            ? []
            : init.Select(v => (IReadOnlyList<DataValue>)[.. items.Select(p => ValueFor(v, p))]).ToList();
        var component = new SelectionComponent
        {
            Name = name,
            VariableName = SelectionDeclarations.VarName(name),
            Type = type,
            Resolve = resolve,
            Declaration = declaration,
            UnitIndex = unitIndexes[unit],
            UnitName = SelectionDeclarations.UnitNameOf(unit),
            Projection = projection,
            TimeUnits = timeUnits,
            Init = tuples,
            Bind = bind,
            Mark = type == "interval" ? MarkOf(declaration, config) : null,
            IsTopLevelView = IsTopLevelView(unit),
        };
        if (component.IsScaleBound)
        {
            // Vega-Lite's SCALE_BINDINGS_CONTINUOUS: only a continuous scale can be bound; the other projected channels with a scale are skipped
            foreach (var item in projection.Where(p => p.Channel is not null && ScaleChannels.Contains(p.Channel) && p.Type != "R"))
            {
                warnings.Add(new FeatureReport(
                    "vl-param-selection-projection",
                    $"The selection '{name}' is bound to the scales, which only works for a scale with a continuous domain; the scale of the channel '{item.Channel}' is not bound.",
                    tree.Locate(declaration),
                    FeatureReportKind.Warning));
            }
        }

        if (component.Bind == SelectionBind.Legend && resolve == "global" && !(projection.Count == 1 && projection[0].Field != SelectionTestSemantics.SelectionId))
        {
            // Vega-Lite's LEGEND_BINDINGS_MUST_HAVE_PROJECTION: the legend binding needs one field of the data to tell which entry was picked
            warnings.Add(new FeatureReport(
                "vl-param-selection-projection",
                $"The selection '{name}' is bound to the legend, which needs a projection over exactly one field (or encoding); the legend is not bound.",
                tree.Locate(declaration),
                FeatureReportKind.Warning));
        }

        return component;
    }

    /// <summary>
    ///     Gets the <c>mark</c> of the brush of an interval selection: the configuration's with the selection's own over it (Vega-Lite's <c>{...cfg.mark, ...def.mark}</c>).
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="config">The resolved configuration.</param>
    /// <returns>The merged mark; an empty object when neither has one.</returns>
    private static ConfigNode MarkOf(SpecNode declaration, ResolvedConfig config)
    {
        var mark = config.VegaLite["selection"]?["interval"]?["mark"] is { IsObject: true } configured ? configured : ConfigNode.EmptyObject;
        return SelectionDeclarations.OptionsOf(declaration)?["mark"] is { IsObject: true } own ? ConfigNode.Merge(mark, ConfigNode.FromSpec(own)) : mark;
    }

    /// <summary>
    ///     Tells whether a unit is the whole view or one of the layers of a layered view that is the whole view (Vega-Lite's <c>!model.parent || isTopLevelLayer(model)</c>): the selections of
    ///     such a view bind its scales directly; the units of a concatenation, a facet or a repeat push their state to the top level instead, which a static chart never does.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns><see langword="true"/> when no view above the unit is anything but a layered view.</returns>
    private static bool IsTopLevelView(LayerNode unit)
    {
        for (var parent = unit.Parent; parent is not null; parent = parent.Parent)
        {
            if (!parent.IsLayer)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets how the tuples of the units are combined, with the configuration applied.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="type">The selection type.</param>
    /// <param name="config">The resolved configuration.</param>
    /// <returns><c>global</c>, <c>union</c> or <c>intersect</c>.</returns>
    private static string ResolveOf(SpecNode declaration, string type, ResolvedConfig config)
    {
        if (SelectionDeclarations.OptionsOf(declaration)?["resolve"] is { IsString: true } own)
        {
            return own.StringValue!;
        }

        return config.VegaLite["selection"]?[type]?["resolve"]?.AsString() ?? "global";
    }

    /// <summary>
    ///     Reads what the <c>bind</c> of a selection says.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The kind of binding.</returns>
    private static SelectionBind BindOf(SpecNode declaration)
    {
        var bind = declaration["bind"];
        if (bind is null || bind.IsNull)
        {
            return SelectionBind.None;
        }

        if (bind.IsString)
        {
            return bind.StringValue switch
            {
                "legend" => SelectionBind.Legend,
                "scales" => SelectionBind.Scales,
                _ => SelectionBind.Widget,
            };
        }

        return bind.IsObject && bind["legend"] is not null ? SelectionBind.Legend : SelectionBind.Widget;
    }

    /// <summary>
    ///     Reads one initial value of the specification: a mapping or a scalar, with the date-time objects of its members turned into milliseconds.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="calendar">The calendar that local dates are read in.</param>
    /// <returns>The value.</returns>
    private static DataValue ReadTuple(SpecNode node, TimeCalendar calendar) =>
        node.IsObject
            ? DataValue.FromObject(DataObject.Create(node.Properties.Select(p => new KeyValuePair<string, DataValue>(p.Name!, ReadMember(p, calendar)))))
            : ReadMember(node, calendar);

    /// <summary>
    ///     Reads a value of an initial mapping: a scalar, a range or list, or a date-time object (which becomes the number of milliseconds of the instant, as Vega-Lite stores it).
    /// </summary>
    /// <remarks>The recursion follows the nesting of an array, which the parser bounded.</remarks>
    /// <param name="node">The value.</param>
    /// <param name="calendar">The calendar that local dates are read in.</param>
    /// <returns>The value.</returns>
    private static DataValue ReadMember(SpecNode node, TimeCalendar calendar)
    {
        if (node.IsObject && SpecParser.IsDateTimeShaped(node) && DateTimeObject.TryRead(node, out var date, out _, out _))
        {
            return DataValue.FromNumber(date.ToMilliseconds(calendar));
        }

        return node.IsArray ? DataValue.FromOwnedArray([.. node.Items.Select(i => ReadMember(i, calendar))]) : SpecValues.ToDataValue(node);
    }

    /// <summary>
    ///     Replaces every date of a value of the caller by its number of milliseconds, inside arrays and objects, as Vega-Lite stores the dates of a selection.
    /// </summary>
    /// <remarks>The nesting of the value was bounded when the options were created, so the recursion is bounded.</remarks>
    /// <param name="value">The value.</param>
    /// <returns>The value with numbers instead of dates.</returns>
    private static DataValue DatesToNumbers(DataValue value)
    {
        if (value.IsDate)
        {
            return DataValue.FromNumber(value.Milliseconds);
        }

        if (value.IsArray)
        {
            return DataValue.FromOwnedArray([.. value.Elements.ToArray().Select(DatesToNumbers)]);
        }

        if (value.IsObject)
        {
            var members = value.ObjectValue!;
            return DataValue.FromObject(DataObject.Create(members.Keys.Select(k => new KeyValuePair<string, DataValue>(k, members.TryGetValue(k, out var member) ? DatesToNumbers(member) : default))));
        }

        return value;
    }

    /// <summary>
    ///     Converts a value to a list the way Vega's <c>array</c> does.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The elements of an array, nothing for null, otherwise the value alone.</returns>
    private static List<DataValue> AsList(DataValue value)
    {
        if (value.IsArray)
        {
            return [.. value.Elements.ToArray()];
        }

        return value.IsNull ? [] : [value];
    }

    /// <summary>
    ///     Picks the value that one projected field takes from an initial value: from a mapping the entry of the channel, else that of the field; a scalar applies to every field.
    /// </summary>
    /// <param name="initial">The initial value (a mapping or a scalar).</param>
    /// <param name="item">The projected field.</param>
    /// <returns>The value; null when the mapping has neither entry.</returns>
    private static DataValue ValueFor(DataValue initial, SelectionTupleField item)
    {
        if (!initial.IsObject)
        {
            return initial;
        }

        var members = initial.ObjectValue!;
        if (item.Channel is not null && members.TryGetValue(item.Channel, out var byChannel))
        {
            return byChannel;
        }

        return members.TryGetValue(item.Field, out var byField) ? byField : default;
    }

    /// <summary>
    ///     Projects a selection over the fields and encodings it constrains (Vega-Lite's <c>project.ts</c>).
    /// </summary>
    /// <remarks>
    ///     An explicit <c>fields</c> or <c>encodings</c> decides; without one the keys of the initial value do (a key that names a channel is an encoding, any other key a field; an interval
    ///     with a key that is not a channel takes the default encodings); without those the defaults of the selection type decide (<c>x</c> and <c>y</c> for an interval, the row identity
    ///     for a point). A channel without a field or with an aggregate cannot be projected and is reported. The tuple type is <c>R</c> for an interval over a continuous domain, <c>R-RE</c> for
    ///     a binned field and <c>E</c> otherwise. A field is projected once.
    /// </remarks>
    /// <param name="tree">The specification, which locates the reports.</param>
    /// <param name="declaration">The declaration.</param>
    /// <param name="name">The name of the selection.</param>
    /// <param name="type">The type of the selection.</param>
    /// <param name="unit">The unit that the selection is declared for.</param>
    /// <param name="config">The resolved configuration.</param>
    /// <param name="init">The initial values, or null.</param>
    /// <param name="warnings">Receives the warnings.</param>
    /// <param name="items">Receives the projected fields, in order.</param>
    /// <param name="timeUnits">Receives the time units the projected fields are derived with (a selection that is tested on a table without them needs them computed first).</param>
    /// <returns>The projected fields.</returns>
    private static IReadOnlyList<SelectionTupleField> Project(
        SpecTree tree,
        SpecNode declaration,
        string name,
        string type,
        LayerNode unit,
        ResolvedConfig config,
        List<DataValue>? init,
        List<FeatureReport> warnings,
        out List<SelectionTupleField> items,
        out List<TimeUnitRequest> timeUnits)
    {
        var options = SelectionDeclarations.OptionsOf(declaration);
        var defaults = config.VegaLite["selection"]?[type];
        List<string>? fields = options?["fields"] is { IsArray: true } ownFields ? [.. ownFields.Items.Select(f => f.StringValue!)] : null;
        List<string>? encodings = options?["encodings"] is { IsArray: true } ownEncodings ? [.. ownEncodings.Items.Select(f => f.StringValue!)] : null;
        var defaultEncodings = StringList(defaults?["encodings"]) ?? (type == "interval" ? ["x", "y"] : null);
        var defaultFields = StringList(defaults?["fields"]) ?? (type == "point" ? [SelectionTestSemantics.SelectionId] : null);
        void Warn(string message) => warnings.Add(new FeatureReport("vl-param-selection-projection", message, tree.Locate(declaration), FeatureReportKind.Warning));

        // Without an explicit projection the keys of the initial value say what it is
        if (fields is null && encodings is null && init is not null)
        {
            foreach (var value in init.Where(v => v.IsObject))
            {
                foreach (var key in value.ObjectValue!.Keys)
                {
                    if (SelectionDeclarations.IsProjectionChannel(key))
                    {
                        (encodings ??= []).Add(key);
                    }
                    else if (type == "interval")
                    {
                        Warn($"The interval selection '{name}' is initialized with the key '{key}', which is not an encoding channel; an interval is projected over encodings, so the default encodings are used.");
                        encodings = defaultEncodings is null ? null : [.. defaultEncodings];
                    }
                    else
                    {
                        (fields ??= []).Add(key);
                    }
                }
            }
        }

        // Without an initial value or a usable key the defaults of the selection type apply
        if (fields is null && encodings is null)
        {
            encodings = defaultEncodings is null ? null : [.. defaultEncodings];
            fields = defaultFields is null ? null : [.. defaultFields];
        }

        items = [];
        timeUnits = [];
        var encoding = unit.Unit?["encoding"] is { IsObject: true } e ? e : null;
        var markType = MarkTypeOf(unit.Unit);
        var projected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var channel in encodings ?? [])
        {
            if (encoding?[channel] is not { IsObject: true } definition || ChannelDefinitions.FieldDefinitionOf(definition) is not { } fieldDefinition
                || (fieldDefinition["field"] is null && fieldDefinition["aggregate"] is not { IsNull: false }))
            {
                Warn($"The selection '{name}' cannot be projected over the channel '{channel}': the view has no field on it.");
                continue;
            }

            if (fieldDefinition["aggregate"] is { IsNull: false })
            {
                Warn($"The selection '{name}' cannot be projected over the channel '{channel}': its field is aggregated.");
                continue;
            }

            if (fieldDefinition["field"] is not { IsString: true } fieldNode)
            {
                Warn($"The selection '{name}' cannot be projected over the channel '{channel}': the view has no field on it.");
                continue;
            }

            // A time unit selects over the derived field of the unit, never the original one
            var field = fieldNode.StringValue!;
            TimeUnitRequest? request = null;
            if (fieldDefinition["timeUnit"] is { IsNull: false } unitNode && TimeUnitSpec.TryRead(unitNode, out var timeUnit) && !timeUnit.IsBinned)
            {
                field = timeUnit.FieldName(field);
                request = new TimeUnitRequest(fieldNode.StringValue!, field, field + "_end", timeUnit, unitNode);
            }

            if (!projected.Add(field))
            {
                continue;
            }

            if (request is not null)
            {
                timeUnits.Add(request);
            }

            var binned = fieldDefinition["bin"] is { IsNull: false } bin && !(bin.IsBoolean && !bin.BoolValue);
            string tupleType;
            if (type == "interval" && ScaleChannels.Contains(channel) && ScaleResolver.HasContinuousDomain(channel, fieldDefinition, markType, encoding))
            {
                tupleType = "R";
            }
            else
            {
                tupleType = binned ? "R-RE" : "E";
            }

            items.Add(new SelectionTupleField(field, channel, tupleType));
        }

        foreach (var field in (fields ?? []).Where(projected.Add))
        {
            items.Add(new SelectionTupleField(field, null, "E"));
        }

        return items;
    }

    /// <summary>
    ///     Reads a list of texts from a configuration value.
    /// </summary>
    /// <param name="node">The value, or null.</param>
    /// <returns>The texts; null when the value is not an array.</returns>
    private static List<string>? StringList(ConfigNode? node) =>
        node is { Kind: ConfigNodeKind.Array } ? [.. node.Items.Where(i => i.Kind == ConfigNodeKind.String).Select(i => i.StringValue!)] : null;

    /// <summary>
    ///     Gets the mark type of a unit view.
    /// </summary>
    /// <param name="unit">The unit view, or null.</param>
    /// <returns>The mark type; <c>point</c> when the view has none that is readable.</returns>
    private static string MarkTypeOf(SpecNode? unit)
    {
        var mark = unit?["mark"];
        return (mark is { IsObject: true } ? mark["type"]?.StringValue : mark?.StringValue) ?? "point";
    }
}
