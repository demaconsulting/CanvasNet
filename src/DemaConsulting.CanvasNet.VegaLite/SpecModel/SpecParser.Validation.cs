// cspell:ignore SNZE BOSZ SAOZ STZE BNSO
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     Structural validation of the properties of a Vega-Lite specification that the renderer depends on, and the
///     detection of unknown properties.
/// </content>
/// <remarks>
///     <para>
///         The rule of this stage: a property that Vega-Lite defines but whose value has the wrong type or value is a
///         <see cref="VegaLiteSpecException"/> with the pointer of that value (the specification cannot be what its
///         author meant, so rendering something would be a guess), while a property that Vega-Lite does not define is
///         only a <c>vl-unknown-property</c> warning at the key (the text may come from a newer version, a tool, or
///         a typing error, and ignoring it is safe). Values of properties that this stage does not check are left to
///         the stage that uses them.
///     </para>
///     <para>
///         The sets of known property names were taken from the public Vega-Lite version 6 JSON schema (property
///         names only) and are deliberately generous: they are the union of every variant of a construct (all
///         top-level specification kinds, all mark definitions including composite marks, all channel definitions), so
///         a valid Vega-Lite key never warns. The legacy top-level <c>selection</c> and the channel-definition key
///         <c>band</c> of version 5 are accepted as well.
///     </para>
///     <para>
///         Validation never walks inline data rows: a <c>values</c> array is checked for being an array and no more,
///         so the cost does not depend on the amount of data. Nested views are visited with an explicit work stack in
///         document order, so the first problem reported is the first one in the text and nesting cannot overflow the
///         call stack.
///     </para>
/// </remarks>
internal sealed partial class SpecParser
{
    /// <summary>The primitive and composite mark types of Vega-Lite version 6.</summary>
    private static readonly HashSet<string> MarkTypes = Set(
        "arc area bar image line point rect rule text tick trail circle square geoshape boxplot errorbar errorband");

    /// <summary>The names of the properties of a view (a top-level specification or a nested view).</summary>
    private static readonly HashSet<string> ViewKeys = Set(
        "$schema align autosize background bounds center columns concat config data datasets description "
        + "encoding facet hconcat height layer mark name padding params projection repeat resolve selection "
        + "spacing spec title transform usermeta vconcat view width");

    /// <summary>The names of the properties of a mark definition (the union of the primitive and composite marks).</summary>
    private static readonly HashSet<string> MarkKeys = Set(
        "align angle aria ariaRole ariaRoleDescription aspect band bandSize baseline binSpacing blend borders "
        + "box clip color continuousBandSize cornerRadius cornerRadiusBottomLeft cornerRadiusBottomRight "
        + "cornerRadiusEnd cornerRadiusTopLeft cornerRadiusTopRight cursor description dir discreteBandSize dx "
        + "dy ellipsis extent fill fillOpacity filled font fontSize fontStyle fontWeight height href "
        + "innerRadius interpolate invalid limit line lineBreak lineHeight median minBandSize opacity order "
        + "orient outerRadius outliers padAngle point radius radius2 radius2Offset radiusOffset rule shape size "
        + "smooth stroke strokeCap strokeDash strokeDashOffset strokeJoin strokeMiterLimit strokeOffset "
        + "strokeOpacity strokeWidth style tension text theta theta2 theta2Offset thetaOffset thickness ticks "
        + "time timeUnitBandPosition timeUnitBandSize tooltip type url width x x2 x2Offset xOffset y y2 "
        + "y2Offset yOffset");

    /// <summary>The names of the encoding channels, including the faceting channels.</summary>
    private static readonly HashSet<string> ChannelNames = Set(
        "angle color column description detail facet fill fillOpacity href key latitude latitude2 longitude "
        + "longitude2 opacity order radius radius2 row shape size stroke strokeDash strokeOpacity strokeWidth "
        + "text theta theta2 time tooltip url x x2 xError xError2 xOffset y y2 yError yError2 yOffset");

    /// <summary>The names of the properties of a channel definition (the union of every variant).</summary>
    private static readonly HashSet<string> ChannelDefKeys = Set(
        "aggregate align axis band bandPosition bin bounds center columns condition datum empty expr field "
        + "format formatType header impute legend param rescale scale sort spacing stack test timeUnit title "
        + "type value");

    /// <summary>The channels whose definition may be an array of definitions (or, for tooltip, null).</summary>
    private static readonly HashSet<string> ArrayChannels = Set("detail order tooltip");

    /// <summary>The field types of Vega-Lite.</summary>
    private static readonly string[] FieldTypes = ["quantitative", "ordinal", "nominal", "temporal", "geojson"];

    /// <summary>The scale types of Vega-Lite version 6.</summary>
    private static readonly string[] ScaleTypes =
    [
        "linear", "log", "pow", "sqrt", "symlog", "identity", "sequential", "time", "utc", "quantile", "quantize",
        "threshold", "bin-ordinal", "ordinal", "point", "band",
    ];

    /// <summary>The names of the transforms; the first of them found in an entry names the entry.</summary>
    private static readonly HashSet<string> TransformNames = Set(
        "aggregate bin calculate density extent filter flatten fold impute joinaggregate loess lookup pivot "
        + "quantile regression sample stack timeUnit window");

    /// <summary>The rules for the properties of a title definition.</summary>
    private static readonly Dictionary<string, Rule> TitleRules = Rules(
        "text: STE", "subtitle: STE", "anchor: SZE=start|middle|end", "orient: SE=none|left|right|top|bottom",
        "frame: SE", "offset: NE", "angle: NE", "dx: NE", "dy: NE", "fontSize: NE", "limit: NE", "lineHeight: NE",
        "subtitleFontSize: NE", "subtitleLineHeight: NE", "subtitlePadding: NE", "zindex: NE", "align: SE",
        "baseline: SE", "color: SZO", "subtitleColor: SZO", "font: SE", "fontStyle: SE", "fontWeight: SNZE",
        "subtitleFont: SE", "subtitleFontStyle: SE", "subtitleFontWeight: SNZE", "aria: BE", "style: ST");

    /// <summary>The rules for the properties of a padding object.</summary>
    private static readonly Dictionary<string, Rule> PaddingRules = Rules("left: NE", "right: NE", "top: NE", "bottom: NE");

    /// <summary>The rules for the properties of an autosize object.</summary>
    private static readonly Dictionary<string, Rule> AutosizeRules = Rules(
        "type: S=pad|none|fit|fit-x|fit-y", "resize: B", "contains: S=content|padding");

    /// <summary>The rules for the properties of a mark definition.</summary>
    private static readonly Dictionary<string, Rule> MarkRules = Rules(
        "color: SZO", "fill: SZO", "stroke: SZO", "opacity: NE", "fillOpacity: NE", "strokeOpacity: NE", "size: NE",
        "strokeWidth: NE", "thickness: NE", "binSpacing: NE", "continuousBandSize: NE", "minBandSize: NE",
        "discreteBandSize: NO", "cornerRadius: NE", "angle: NE", "dx: NE", "dy: NE", "filled: BE",
        "orient: SE=horizontal|vertical", "style: ST", "clip: BE", "shape: SE", "strokeDash: AE", "strokeCap: SE",
        "strokeJoin: SE", "interpolate: SE", "align: SE", "baseline: SE", "font: SE", "fontSize: NE",
        "x: NSO", "y: NSO", "x2: NSO", "y2: NSO", "width: NO", "height: NO");

    /// <summary>The rules for the properties of a channel definition.</summary>
    private static readonly Dictionary<string, Rule> ChannelDefRules = Rules(
        "field: SO", "type: S=" + string.Join('|', FieldTypes), "aggregate: SOZ", "bin: BOSZ", "timeUnit: SOZ",
        "scale: OZ", "axis: OZ", "legend: OZ", "sort: SAOZ", "title: STZE", "format: SO", "formatType: S",
        "condition: OA", "stack: SBZ", "impute: OZ", "bandPosition: NE", "header: OZ");

    /// <summary>The rules for the properties of a scale definition.</summary>
    private static readonly Dictionary<string, Rule> ScaleRules = Rules(
        "type: S=" + string.Join('|', ScaleTypes), "zero: BE", "nice: BNSO", "padding: NE", "paddingInner: NE",
        "paddingOuter: NE", "reverse: BE", "round: BE", "clamp: BE", "domain: ASO", "range: ASO", "scheme: SO",
        "exponent: NE", "base: NE", "constant: NE");

    /// <summary>The rules for the properties of an axis definition.</summary>
    private static readonly Dictionary<string, Rule> AxisRules = Rules(
        "orient: SE=top|bottom|left|right", "grid: BE", "ticks: BE", "labels: BE", "domain: BE",
        "title: STZE", "tickCount: NSO", "tickSize: NE", "tickWidth: NE", "offset: NE", "labelAngle: NE",
        "labelFontSize: NE", "titleFontSize: NE", "domainWidth: NE", "gridWidth: NE", "labelPadding: NE",
        "titlePadding: NE", "labelLimit: NE", "titleLimit: NE", "maxExtent: NE", "minExtent: NE",
        "values: AE", "format: SO", "formatType: S", "encode: O", "labelExpr: S", "style: ST", "zindex: NE",
        "domainColor: SZE", "gridColor: SZE", "tickColor: SZE", "labelColor: SZE", "titleColor: SZE",
        "labelFont: SE", "titleFont: SE", "labelFontWeight: SNE", "titleFontWeight: SNE", "labelOverlap: BSE");

    /// <summary>The rules for the properties of a legend definition.</summary>
    private static readonly Dictionary<string, Rule> LegendRules = Rules(
        "orient: SE=none|left|right|top|bottom|top-left|top-right|bottom-left|bottom-right",
        "type: S=symbol|gradient", "title: STZE", "encode: O", "values: AE", "format: SO", "tickCount: NSO",
        "direction: S=horizontal|vertical");

    /// <summary>The rules for the properties of a data definition.</summary>
    private static readonly Dictionary<string, Rule> DataRules = Rules(
        "values: SAO", "url: S", "name: S", "sequence: O", "format: O", "sphere: BO", "graticule: BO");

    /// <summary>The rules for the properties of a data format.</summary>
    private static readonly Dictionary<string, Rule> FormatRules = Rules(
        "type: S=json|csv|tsv|dsv|topojson", "parse: SOZ", "property: S", "header: A", "delimiter: S", "feature: S",
        "mesh: S");

    /// <summary>The rules for the properties of a sequence generator.</summary>
    private static readonly Dictionary<string, Rule> SequenceRules = Rules("start: N", "stop: N", "step: N", "as: S");

    /// <summary>The rules for the properties of a view.</summary>
    private static readonly Dictionary<string, Rule> ViewRules = Rules(
        "description: S", "name: S", "usermeta: O", "background: SE", "bounds: S=full|flush", "config: O",
        "projection: O", "view: O", "selection: O", "resolve: O", "facet: O", "spec: O", "repeat: AO",
        "align: SO", "center: BO", "columns: NO", "spacing: NO");

    /// <summary>
    ///     Determines whether a name is an encoding channel of Vega-Lite.
    /// </summary>
    /// <param name="name">The channel name.</param>
    /// <returns><see langword="true"/> when Vega-Lite defines the channel (the feature catalog uses this so that a name the parser already warned about is not reported a second time).</returns>
    public static bool IsChannelName(string name) => ChannelNames.Contains(name);

    /// <summary>
    ///     Finds the property that names a transform: the first property of the entry that is a transform name.
    /// </summary>
    /// <param name="entry">The transform entry object.</param>
    /// <returns>The property value (its <see cref="SpecNode.Name"/> is the transform name), or null when the entry names no known transform.</returns>
    public static SpecNode? FindTransformOperation(SpecNode entry) =>
        entry.Properties.FirstOrDefault(p => TransformNames.Contains(p.Name!));

    /// <summary>
    ///     Validates the whole tree: every view, in document order.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for the first known property with a wrong type or value.</exception>
    private void Validate(SpecNode root)
    {
        // Each work item is a view and the index of the next property to check, so that a nested view is checked
        // exactly where its composition property sits in the text
        var pending = new Stack<(SpecNode View, int Next)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (view, next) = pending.Pop();
            var isRoot = ReferenceEquals(view, root);
            var properties = view.Properties;
            for (var i = next; i < properties.Count; i++)
            {
                var property = properties[i];
                var children = ValidateViewProperty(property, isRoot);
                if (children is null)
                {
                    continue;
                }

                // Resume after this property once the nested views have been checked
                pending.Push((view, i + 1));
                for (var c = children.Count - 1; c >= 0; c--)
                {
                    pending.Push((children[c], 0));
                }

                break;
            }
        }
    }

    /// <summary>
    ///     Validates one property of a view.
    /// </summary>
    /// <param name="property">The property value.</param>
    /// <param name="isRoot">Whether the view is the top-level specification.</param>
    /// <returns>The nested views to check next, or null when the property has none.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private List<SpecNode>? ValidateViewProperty(SpecNode property, bool isRoot)
    {
        var name = property.Name!;
        if (!ViewKeys.Contains(name))
        {
            WarnUnknown(property, "a view");
            return null;
        }

        switch (name)
        {
            case "$schema":
                ValidateSchema(property, isRoot);
                break;
            case "title":
                ValidateTitle(property);
                break;
            case "width" or "height":
                ValidateSize(property);
                break;
            case "padding":
                ValidatePadding(property);
                break;
            case "autosize":
                ValidateAutosize(property);
                break;
            case "data":
                ValidateData(property);
                break;
            case "datasets":
                ValidateDatasets(property);
                break;
            case "mark":
                ValidateMark(property);
                break;
            case "encoding":
                ValidateEncoding(property);
                break;
            case "params":
                ValidateParams(property);
                break;
            case "transform":
                ValidateTransforms(property);
                break;
            case "layer" or "concat" or "hconcat" or "vconcat":
                return ValidateViewList(property);
            case "spec":
                Check(property, ViewRules["spec"], name);
                return [property];
            default:
                Check(property, ViewRules[name], name);
                break;
        }

        return null;
    }

    /// <summary>
    ///     Validates the <c>$schema</c> of a view.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="isRoot">Whether the view is the top-level specification, which alone is checked for the Vega schema.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not a non-empty string, or names plain Vega.</exception>
    private void ValidateSchema(SpecNode node, bool isRoot)
    {
        if (!node.IsString)
        {
            throw Error(node, "The $schema must be a string.");
        }

        if (!isRoot)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(node.StringValue))
        {
            throw Error(node, "The $schema must not be empty.");
        }

        if (node.StringValue.Contains(VegaSchemaFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw Error(node, "The $schema names plain Vega; only Vega-Lite specifications are supported.");
        }
    }

    /// <summary>
    ///     Validates a title: a string, an array of strings, null, or a title definition object.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateTitle(SpecNode node)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Null or SpecNodeKind.String:
                return;
            case SpecNodeKind.Array:
                CheckStringItems(node, "The lines of a text must be strings.");
                return;
            case SpecNodeKind.Object:
                CheckObject(node, TitleRules);
                return;
            default:
                throw Error(node, "The title must be a string, an array of strings or an object.");
        }
    }

    /// <summary>
    ///     Validates a <c>width</c> or <c>height</c>: a number, <c>"container"</c>, a <c>{ "step": n }</c> object or an expression.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateSize(SpecNode node)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Number:
                return;
            case SpecNodeKind.String when node.StringValue == "container":
                return;
            case SpecNodeKind.Object when node["expr"] is { IsString: true }:
                return;
            case SpecNodeKind.Object:
                var step = node["step"];
                if (step is null || !step.IsNumber)
                {
                    throw Error(step ?? node, $"The {node.Name} object needs a numeric 'step'.");
                }

                return;
            default:
                throw Error(node, $"The {node.Name} must be a number, \"container\" or an object with a numeric 'step'.");
        }
    }

    /// <summary>
    ///     Validates a padding: a number, an expression, or an object with <c>left</c>, <c>right</c>, <c>top</c> and <c>bottom</c>.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidatePadding(SpecNode node)
    {
        if (node.IsNumber || node is { IsObject: true } && node["expr"] is { IsString: true })
        {
            return;
        }

        if (!node.IsObject)
        {
            throw Error(node, "The padding must be a number or an object.");
        }

        CheckObject(node, PaddingRules);
    }

    /// <summary>
    ///     Validates an autosize: a type name or an object with a <c>type</c>.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateAutosize(SpecNode node)
    {
        if (node.IsObject)
        {
            CheckObject(node, AutosizeRules);
            return;
        }

        Check(node, AutosizeRules["type"], "autosize");
    }

    /// <summary>
    ///     Validates a <c>data</c> definition.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateData(SpecNode node)
    {
        if (node.IsNull)
        {
            return;
        }

        if (!node.IsObject)
        {
            throw Error(node, "The data must be an object.");
        }

        CheckObject(node, DataRules);
        if (node["format"] is { IsObject: true } format)
        {
            CheckObject(format, FormatRules);
            ValidateFormat(format);
        }

        if (node["sequence"] is { IsObject: true } sequence)
        {
            CheckObject(sequence, SequenceRules);
            var missing = new[] { "start", "stop" }.FirstOrDefault(name => sequence[name] is null);
            if (missing is not null)
            {
                throw Error(sequence, $"The sequence needs a numeric '{missing}'.");
            }

            // An empty field name cannot be addressed by an encoding. (A zero step is not rejected here: the data reference
            // reports an unknown length for it, and the data stage fails the render with its location.)
            if (sequence["as"] is { IsString: true, StringValue.Length: 0 } empty)
            {
                throw Error(empty, "The sequence field name ('as') must not be empty.");
            }
        }
    }

    /// <summary>
    ///     Validates the parts of a data <c>format</c> that the data stage reads: the delimiter and the <c>parse</c> directives.
    /// </summary>
    /// <remarks>
    ///     A delimiter is exactly one character that can separate fields (not a quote, a line break or half of a surrogate
    ///     pair). <c>parse</c> is <c>"auto"</c>, <c>null</c>, or an object that maps field names to <c>number</c>,
    ///     <c>boolean</c>, <c>date</c>, <c>string</c> or a custom date format (<c>date:'%fmt'</c>, <c>utc:'%fmt'</c>); a
    ///     custom date format is valid here and reported by the feature catalog, because it is not applied.
    /// </remarks>
    /// <param name="format">The <c>format</c> object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a delimiter that is not one valid character or a parse directive that is not known.</exception>
    private void ValidateFormat(SpecNode format)
    {
        if (format["delimiter"] is { IsString: true } delimiter
            && (delimiter.StringValue!.Length != 1 || !DelimitedTextParser.IsValidDelimiter(delimiter.StringValue[0])))
        {
            throw Error(delimiter, "The 'delimiter' must be a single character other than a quote or a line break.");
        }

        var parse = format["parse"];
        if (parse is { IsString: true } && parse.StringValue != "auto")
        {
            throw Error(parse, "The 'parse' must be \"auto\", null or an object that maps field names to data types.");
        }

        if (parse is not { IsObject: true })
        {
            return;
        }

        foreach (var entry in parse.Properties)
        {
            if (!entry.IsString || !ParseDirectives.TryClassify(entry.StringValue!, out _))
            {
                throw Error(entry, $"The parse type of '{entry.Name}' must be \"number\", \"boolean\", \"date\", \"string\" or a date format such as \"date:'%Y-%m-%d'\".");
            }
        }
    }

    /// <summary>
    ///     Validates the top-level <c>datasets</c>: an object whose values are arrays of rows, text or objects.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type.</exception>
    private void ValidateDatasets(SpecNode node)
    {
        if (!node.IsObject)
        {
            throw Error(node, "The datasets must be an object.");
        }

        foreach (var dataset in node.Properties)
        {
            if (dataset.IsNull || dataset.IsNumber || dataset.IsBoolean)
            {
                throw Error(dataset, $"The dataset '{dataset.Name}' must be an array of rows, a text or an object.");
            }
        }
    }

    /// <summary>
    ///     Validates a <c>mark</c>: a mark type or a mark definition object with a <c>type</c>.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateMark(SpecNode node)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                if (!MarkTypes.Contains(node.StringValue!))
                {
                    throw Error(node, $"'{node.StringValue}' is not a Vega-Lite mark type.");
                }

                return;
            case SpecNodeKind.Object:
                var type = node["type"];
                if (type is null)
                {
                    throw Error(node, "A mark definition needs a string 'type'.");
                }

                if (!type.IsString)
                {
                    throw Error(type, "The mark type must be a string.");
                }

                if (!MarkTypes.Contains(type.StringValue!))
                {
                    throw Error(type, $"'{type.StringValue}' is not a Vega-Lite mark type.");
                }

                CheckObject(node, MarkRules);
                foreach (var property in node.Properties.Where(p => !MarkKeys.Contains(p.Name!)))
                {
                    WarnUnknown(property, "a mark definition");
                }

                return;
            default:
                throw Error(node, "The mark must be a string or an object.");
        }
    }

    /// <summary>
    ///     Validates an <c>encoding</c>: an object of channel definitions.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateEncoding(SpecNode node)
    {
        if (!node.IsObject)
        {
            throw Error(node, "The encoding must be an object.");
        }

        foreach (var channel in node.Properties)
        {
            if (!ChannelNames.Contains(channel.Name!))
            {
                WarnUnknown(channel, "an encoding");
                continue;
            }

            ValidateChannel(channel);
        }
    }

    /// <summary>
    ///     Validates one channel definition (or an array of them for the channels that allow it).
    /// </summary>
    /// <param name="channel">The channel definition value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateChannel(SpecNode channel)
    {
        var name = channel.Name!;
        if (channel.IsNull && name == "tooltip")
        {
            return;
        }

        if (channel.IsArray && ArrayChannels.Contains(name))
        {
            foreach (var item in channel.Items)
            {
                if (!item.IsObject)
                {
                    throw Error(item, $"Each definition of the '{name}' channel must be an object.");
                }

                ValidateChannelDefinition(item);
            }

            return;
        }

        if (!channel.IsObject)
        {
            throw Error(channel, $"The '{name}' channel must be an object.");
        }

        ValidateChannelDefinition(channel);
    }

    /// <summary>
    ///     Validates the properties of one channel definition object, including its scale, axis and legend.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or value.</exception>
    private void ValidateChannelDefinition(SpecNode definition)
    {
        CheckObject(definition, ChannelDefRules);
        foreach (var property in definition.Properties.Where(p => !ChannelDefKeys.Contains(p.Name!)))
        {
            WarnUnknown(property, "a channel definition");
        }

        if (definition["scale"] is { IsObject: true } scale)
        {
            CheckObject(scale, ScaleRules);
        }

        if (definition["axis"] is { IsObject: true } axis)
        {
            CheckObject(axis, AxisRules);
        }

        if (definition["legend"] is { IsObject: true } legend)
        {
            CheckObject(legend, LegendRules);
        }
    }

    /// <summary>
    ///     Validates <c>params</c>: an array of parameter objects, each with a string <c>name</c>.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or a parameter without a name.</exception>
    private void ValidateParams(SpecNode node)
    {
        if (!node.IsArray)
        {
            throw Error(node, "The params must be an array of parameters.");
        }

        foreach (var parameter in node.Items)
        {
            if (!parameter.IsObject)
            {
                throw Error(parameter, "A parameter must be an object.");
            }

            var name = parameter["name"];
            if (name is null || !name.IsString || string.IsNullOrEmpty(name.StringValue))
            {
                throw Error(name ?? parameter, "A parameter needs a non-empty string 'name'.");
            }

            if (parameter["expr"] is { } expr && !expr.IsString)
            {
                throw Error(expr, "The expr of a parameter must be a string.");
            }
        }
    }

    /// <summary>
    ///     Validates <c>transform</c>: an array of transform objects, each naming a known transform.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a wrong type or an entry that is not a transform.</exception>
    private void ValidateTransforms(SpecNode node)
    {
        if (!node.IsArray)
        {
            throw Error(node, "The transform must be an array of transforms.");
        }

        foreach (var entry in node.Items)
        {
            if (!entry.IsObject)
            {
                throw Error(entry, "A transform must be an object.");
            }

            var operation = FindTransformOperation(entry);
            if (operation is null)
            {
                throw Error(entry, "The object is not a Vega-Lite transform: it has none of the transform properties (aggregate, bin, calculate, filter, ...).");
            }

            if (operation.Name == "calculate" && !operation.IsString)
            {
                throw Error(operation, "The calculate of a transform must be an expression string.");
            }
        }
    }

    /// <summary>
    ///     Validates a composition property (<c>layer</c>, <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>): an array of views.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The nested views, in order.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the value is not an array of objects.</exception>
    private List<SpecNode> ValidateViewList(SpecNode node)
    {
        if (!node.IsArray)
        {
            throw Error(node, "A view composition must be an array of views.");
        }

        var views = new List<SpecNode>(node.Items.Count);
        foreach (var item in node.Items)
        {
            if (!item.IsObject)
            {
                throw Error(item, "A view must be an object.");
            }

            views.Add(item);
        }

        return views;
    }

    /// <summary>
    ///     Checks every property of an object that has a rule against its rule.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="rules">The rules by property name.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for the first property whose value does not satisfy its rule.</exception>
    private void CheckObject(SpecNode node, Dictionary<string, Rule> rules)
    {
        foreach (var property in node.Properties)
        {
            if (rules.TryGetValue(property.Name!, out var rule))
            {
                Check(property, rule, property.Name!);
            }
        }
    }

    /// <summary>
    ///     Checks one value against a rule.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="name">The name of the property for the message.</param>
    /// <exception cref="VegaLiteSpecException">Thrown when the value does not satisfy the rule.</exception>
    private void Check(SpecNode node, Rule rule, string name)
    {
        if (!rule.Matches(node))
        {
            throw Error(node, $"The '{name}' must be {rule.Describe()}.");
        }

        if (node.IsString && rule.Values is not null && !rule.Values.Contains(node.StringValue!))
        {
            throw Error(node, $"The '{name}' must be one of {string.Join(", ", rule.Values.Select(v => "'" + v + "'"))}.");
        }

        if (node.IsArray && rule.StringArrayOnly)
        {
            CheckStringItems(node, $"The items of '{name}' must be strings.");
        }
    }

    /// <summary>
    ///     Checks that every element of an array is a string.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="message">The description of the error.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending element, when an element is not a string.</exception>
    private void CheckStringItems(SpecNode array, string message)
    {
        foreach (var item in array.Items)
        {
            if (!item.IsString)
            {
                throw Error(item, message);
            }
        }
    }

    /// <summary>
    ///     Records the warning for a property that Vega-Lite does not define.
    /// </summary>
    /// <param name="property">The property value (the warning is placed at its key).</param>
    /// <param name="owner">A short description of what the property belongs to, for the message.</param>
    private void WarnUnknown(SpecNode property, string owner) =>
        _warnings.Add(new FeatureReport(
            "vl-unknown-property",
            $"The property '{property.Name}' is not a Vega-Lite property of {owner} and is ignored.",
            property.GetKeyLocation(_file),
            FeatureReportKind.Warning));

    /// <summary>
    ///     Creates the exception for a wrong value.
    /// </summary>
    /// <param name="node">The offending value.</param>
    /// <param name="description">The description of the problem.</param>
    /// <returns>The exception, ready to throw.</returns>
    private VegaLiteSpecException Error(SpecNode node, string description) =>
        new(description, node.GetLocation(_file));

    /// <summary>
    ///     Creates a set of names from a space-separated list.
    /// </summary>
    /// <param name="names">The names separated by spaces.</param>
    /// <returns>The set (ordinal comparison).</returns>
    private static HashSet<string> Set(string names) =>
        new(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    /// <summary>
    ///     Builds a rule table from compact definitions of the form <c>name: KINDS</c> or <c>name: KINDS=a|b</c>.
    /// </summary>
    /// <remarks>
    ///     The kind letters are S string, N number, B boolean, O object, A array, T array of strings, Z null and E
    ///     expression reference (an object with a string <c>expr</c>). A list after <c>=</c> restricts the accepted
    ///     strings.
    /// </remarks>
    /// <param name="definitions">The definitions.</param>
    /// <returns>The rules by property name.</returns>
    private static Dictionary<string, Rule> Rules(params string[] definitions)
    {
        var rules = new Dictionary<string, Rule>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var colon = definition.IndexOf(':', StringComparison.Ordinal);
            var name = definition[..colon];
            var spec = definition[(colon + 1)..].Trim();
            var equals = spec.IndexOf('=', StringComparison.Ordinal);
            var letters = equals < 0 ? spec : spec[..equals];
            var values = equals < 0 ? null : spec[(equals + 1)..].Split('|');
            rules[name] = new Rule(letters, values);
        }

        return rules;
    }

    /// <summary>
    ///     What a property accepts: the kinds of JSON value and, for strings, an optional list of allowed values.
    /// </summary>
    private sealed class Rule
    {
        /// <summary>The kind letters of the rule.</summary>
        private readonly string _letters;

        /// <summary>
        ///     Initializes a rule.
        /// </summary>
        /// <param name="letters">The accepted kinds as letters (see <see cref="Rules"/>).</param>
        /// <param name="values">The allowed strings, or null for any string.</param>
        public Rule(string letters, string[]? values)
        {
            _letters = letters;
            Values = values;
        }

        /// <summary>Gets the allowed strings, or null when any string is accepted.</summary>
        public string[]? Values { get; }

        /// <summary>Gets a value indicating whether an array must hold only strings.</summary>
        public bool StringArrayOnly => _letters.Contains('T', StringComparison.Ordinal) && !_letters.Contains('A', StringComparison.Ordinal);

        /// <summary>
        ///     Determines whether a value has an accepted kind.
        /// </summary>
        /// <param name="node">The value.</param>
        /// <returns><see langword="true"/> when the kind is accepted (the allowed strings are checked separately).</returns>
        public bool Matches(SpecNode node) => node.Kind switch
        {
            SpecNodeKind.String => Has('S'),
            SpecNodeKind.Number => Has('N'),
            SpecNodeKind.Boolean => Has('B'),
            SpecNodeKind.Null => Has('Z'),
            SpecNodeKind.Array => Has('A') || Has('T'),
            _ => Has('O') || (Has('E') && node["expr"] is { IsString: true }),
        };

        /// <summary>
        ///     Describes the accepted kinds for an error message.
        /// </summary>
        /// <returns>For example <c>a string or a number</c>.</returns>
        public string Describe()
        {
            var parts = new List<string>();
            AddIf(parts, 'S', "a string");
            AddIf(parts, 'N', "a number");
            AddIf(parts, 'B', "a boolean");
            AddIf(parts, 'O', "an object");
            AddIf(parts, 'E', "an expression reference");
            if (Has('A'))
            {
                parts.Add("an array");
            }
            else if (Has('T'))
            {
                parts.Add("an array of strings");
            }

            AddIf(parts, 'Z', "null");
            var text = new StringBuilder();
            for (var i = 0; i < parts.Count; i++)
            {
                var separator = "";
                if (i == parts.Count - 1 && i > 0)
                {
                    separator = " or ";
                }
                else if (i > 0)
                {
                    separator = ", ";
                }

                text.Append(separator).Append(parts[i]);
            }

            return text.ToString();
        }

        /// <summary>
        ///     Determines whether the rule contains a kind letter.
        /// </summary>
        /// <param name="letter">The letter.</param>
        /// <returns><see langword="true"/> when the rule accepts that kind.</returns>
        private bool Has(char letter) => _letters.Contains(letter, StringComparison.Ordinal);

        /// <summary>
        ///     Adds a description when the rule contains a kind letter.
        /// </summary>
        /// <param name="parts">The descriptions collected so far.</param>
        /// <param name="letter">The letter.</param>
        /// <param name="text">The description.</param>
        private void AddIf(List<string> parts, char letter, string text)
        {
            if (Has(letter))
            {
                parts.Add(text);
            }
        }
    }
}
