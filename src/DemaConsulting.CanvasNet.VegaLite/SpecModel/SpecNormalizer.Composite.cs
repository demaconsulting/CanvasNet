// Portions derived from Vega-Lite (BSD-3-Clause): compositemark/common.ts (partLayerMixins, compositeMarkOrient, compositeMarkContinuousAxis) and compositemark/index.ts; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The layer tree of a specification with its composite marks expanded, and the specification tree that holds the expressions that the expansion wrote.
/// </summary>
/// <param name="Layers">The layer tree; a composite mark is a layered view of the layers it stands for.</param>
/// <param name="Tree">The specification tree to read expressions from: the parsed tree when nothing was expanded, otherwise a tree with the same root whose expressions include those of the expansion.</param>
internal sealed record ExpandedLayers(LayerTree Layers, SpecTree Tree);

/// <summary>
///     The continuous axis of a composite mark: the position channel that holds the measure.
/// </summary>
/// <param name="Channel">The channel, <c>x</c> or <c>y</c>.</param>
/// <param name="Secondary">The secondary channel, <c>x2</c> or <c>y2</c>.</param>
/// <param name="Definition">The channel definition as written.</param>
/// <param name="Field">The field as written.</param>
/// <param name="Alias">The field without its path syntax, as the aggregated fields are named after it.</param>
/// <param name="Type">The type of the field.</param>
internal sealed record CompositeAxis(string Channel, string Secondary, SpecNode Definition, string Field, string Alias, string Type);

/// <summary>
///     Writes the synthetic specification nodes of a composite mark expansion, and reads the properties that every composite mark shares.
/// </summary>
/// <remarks>
///     <para>
///         Every node the builder creates is located at the <c>mark</c> of the composite mark (<see cref="SpecNode.Synthetic"/>), so a diagnostic about the expansion points at the mark
///         that caused it. The nodes of the specification (a channel definition, a <c>scale</c>) are used as they are inside the new objects, never copied or modified.
///     </para>
///     <para>An expression the builder writes is compiled when it is created and registered for the specification tree that the document reads (<see cref="SpecExpressions.With"/>).</para>
/// </remarks>
internal sealed class CompositeBuilder
{
    /// <summary>The properties of a part that the composite mark definition gives to every part when it is truthy.</summary>
    private static readonly string[] SharedPartProperties = ["clip", "color", "opacity"];

    /// <summary>The expressions written so far, with the nodes that hold them.</summary>
    private readonly List<(SpecNode Node, CompiledExpression Expression)> _expressions;

    /// <summary>The limits of the expression parser.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The source name for the locations of the compiled expressions.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Initializes the builder.
    /// </summary>
    /// <param name="tree">The specification (its source name locates the expressions).</param>
    /// <param name="config">The effective configuration (the composite mark's defaults).</param>
    /// <param name="limits">The limits of the expression parser.</param>
    /// <param name="mark">The <c>mark</c> node of the composite mark: a string or a mark definition.</param>
    /// <param name="expressions">Receives the compiled expressions of the expansion.</param>
    public CompositeBuilder(SpecTree tree, ResolvedConfig config, VegaLiteLimits limits, SpecNode mark, List<(SpecNode Node, CompiledExpression Expression)> expressions)
    {
        _sourceName = tree.SourceName;
        Config = config;
        _limits = limits;
        Mark = mark;
        _expressions = expressions;
        Type = (mark.IsString ? mark.StringValue : mark["type"]?.AsString()) ?? string.Empty;
    }

    /// <summary>Gets the effective configuration.</summary>
    public ResolvedConfig Config { get; }

    /// <summary>Gets the <c>mark</c> node of the composite mark.</summary>
    public SpecNode Mark { get; }

    /// <summary>Gets the type of the composite mark, such as <c>boxplot</c>.</summary>
    public string Type { get; }

    /// <summary>Gets the mark definition object, or null when the mark is written as its type only.</summary>
    public SpecNode? Definition => Mark.IsObject ? Mark : null;

    /// <summary>
    ///     Creates a synthetic object.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="properties">The property values.</param>
    /// <returns>The object.</returns>
    public SpecNode Obj(string? name, params SpecNode?[] properties) => ObjOf(name, properties);

    /// <summary>
    ///     Creates a synthetic object from a list of property values.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="properties">The property values; null entries are left out.</param>
    /// <returns>The object.</returns>
    public SpecNode ObjOf(string? name, IEnumerable<SpecNode?> properties) =>
        SpecNode.Synthetic(Mark, SpecNodeKind.Object, name, children: properties.OfType<SpecNode>());

    /// <summary>
    ///     Creates a synthetic array.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="items">The elements.</param>
    /// <returns>The array.</returns>
    public SpecNode Arr(string? name, IEnumerable<SpecNode> items) => SpecNode.Synthetic(Mark, SpecNodeKind.Array, name, children: items);

    /// <summary>
    ///     Creates a synthetic string.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="text">The text.</param>
    /// <returns>The string node.</returns>
    public SpecNode Str(string? name, string text) => SpecNode.Synthetic(Mark, SpecNodeKind.String, name, text);

    /// <summary>
    ///     Creates a synthetic number.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="value">The number.</param>
    /// <returns>The number node.</returns>
    public SpecNode Num(string? name, double value) => SpecNode.Synthetic(Mark, SpecNodeKind.Number, name, number: value);

    /// <summary>
    ///     Creates a synthetic expression: a string node whose text is compiled now and registered.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="source">The expression source.</param>
    /// <returns>The string node.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the expression cannot be compiled (a limit of the parser is exceeded).</exception>
    public SpecNode Expr(string? name, string source)
    {
        var node = Str(name, source);
        _expressions.Add((node, ExpressionParser.ParseGenerated(source, node.GetLocation(_sourceName), _limits)));
        return node;
    }

    /// <summary>
    ///     Writes an error at the composite mark.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception to throw.</returns>
    public VegaLiteSpecException Error(string message) => new(message, Mark.GetLocation(_sourceName));

    /// <summary>
    ///     Converts a configuration value to a synthetic node.
    /// </summary>
    /// <param name="name">The property name, or null for an array element.</param>
    /// <param name="value">The configuration value.</param>
    /// <returns>The node.</returns>
    public SpecNode FromConfig(string? name, ConfigNode value) => value.Kind switch
    {
        ConfigNodeKind.String => Str(name, value.StringValue!),
        ConfigNodeKind.Number => Num(name, value.NumberValue),
        ConfigNodeKind.Boolean => SpecNode.Synthetic(Mark, SpecNodeKind.Boolean, name, flag: value.BoolValue),
        ConfigNodeKind.Array => Arr(name, value.Items.Select(item => FromConfig(null, item))),
        ConfigNodeKind.Object => ObjOf(name, value.Properties.Select(p => (SpecNode?)FromConfig(p.Key, p.Value))),
        _ => SpecNode.Synthetic(Mark, SpecNodeKind.Null, name),
    };

    /// <summary>
    ///     Reads a property of the composite mark: its definition, else the configuration of the mark type.
    /// </summary>
    /// <param name="name">The property.</param>
    /// <returns>The value; null when neither has it.</returns>
    public SpecNode? Property(string name)
    {
        if (Definition?[name] is { } own)
        {
            return own;
        }

        return Config.Get(Type + "." + name) is { } configured ? FromConfig(name, configured) : null;
    }

    /// <summary>
    ///     Tells whether a part of the composite mark is drawn: the mark definition's own setting, else the configuration's (an object or <c>true</c>).
    /// </summary>
    /// <param name="part">The part name, such as <c>box</c>.</param>
    /// <returns><see langword="true"/> when the part is on.</returns>
    public bool HasPart(string part)
    {
        if (Definition?[part] is { } own)
        {
            return own.IsObject || (own.IsBoolean && own.BoolValue);
        }

        return Config.Get(Type + "." + part) is { IsObject: true } or { Kind: ConfigNodeKind.Boolean, BoolValue: true };
    }

    /// <summary>
    ///     Creates the <c>mark</c> of a part: the configuration of the part, the <c>clip</c>, <c>color</c> and <c>opacity</c> of the composite mark, the properties of the
    ///     part's own mark, the style <c>&lt;mark&gt;-&lt;part&gt;</c> and the part's properties of the mark definition, each overriding the one before.
    /// </summary>
    /// <param name="part">The part name, such as <c>box</c>.</param>
    /// <param name="own">The properties that the part's mark has by itself (its <c>type</c>, its size and orient).</param>
    /// <returns>The <c>mark</c> object.</returns>
    public SpecNode PartMark(string part, params SpecNode?[] own)
    {
        var properties = new List<SpecNode>();
        if (Config.Get(Type + "." + part) is { IsObject: true } configured)
        {
            foreach (var property in configured.Properties)
            {
                Put(properties, FromConfig(property.Key, property.Value));
            }
        }

        foreach (var name in SharedPartProperties)
        {
            if (Definition?[name] is { } shared && IsTruthy(shared))
            {
                Put(properties, shared);
            }
        }

        foreach (var property in own.OfType<SpecNode>())
        {
            Put(properties, property);
        }

        Put(properties, Str("style", Type + "-" + part));
        if (Definition?[part] is { IsObject: true } specified)
        {
            foreach (var property in specified.Properties)
            {
                Put(properties, property);
            }
        }

        return ObjOf("mark", properties);
    }

    /// <summary>
    ///     Reads the type of a channel definition, as Vega-Lite infers it (<see cref="ChannelDefinitions.TypeOf"/>; a composite mark infers the types of its encoding first).
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <param name="channel">The channel name.</param>
    /// <returns>The written type or the inferred one; null when there is no definition.</returns>
    public static string? TypeOf(SpecNode? definition, string channel) =>
        definition is { IsObject: true } ? ChannelDefinitions.TypeOf(definition, channel) : null;

    /// <summary>
    ///     Decides the orientation of a composite mark: the explicit <c>orient</c>, else the position channel that is not the measure.
    /// </summary>
    /// <remarks>
    ///     A vertical mark has its measure on <c>y</c> (Vega-Lite's <c>compositeMarkOrient</c>). When only <c>x</c> is continuous the mark is horizontal; when both are, the one whose
    ///     aggregate is the composite mark is the measure, otherwise a temporal <c>y</c> next to a <c>x</c> that is not makes it horizontal and all other cases are vertical.
    /// </remarks>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <returns><c>vertical</c> or <c>horizontal</c>.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when neither channel is continuous or both aggregate with the composite mark.</exception>
    public string Orient(SpecNode? encoding) =>
        TryOrient(Type, Definition, encoding, out var orient, out var problem) ? orient : throw Error(problem!);

    /// <summary>
    ///     Decides the orientation of a composite mark without failing.
    /// </summary>
    /// <param name="type">The composite mark type.</param>
    /// <param name="definition">The mark definition object, or null.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="orient">Receives <c>vertical</c> or <c>horizontal</c>.</param>
    /// <param name="problem">Receives the reason when the orientation cannot be decided.</param>
    /// <returns><see langword="true"/> when the orientation was decided.</returns>
    public static bool TryOrient(string type, SpecNode? definition, SpecNode? encoding, out string orient, out string? problem)
    {
        problem = null;
        orient = "vertical";
        if (definition?["orient"]?.AsString() is "horizontal" or "vertical")
        {
            orient = definition["orient"]!.StringValue!;
            return true;
        }

        var x = encoding?["x"] is { IsObject: true } xd ? xd : null;
        var y = encoding?["y"] is { IsObject: true } yd ? yd : null;
        var xContinuous = IsContinuous(x, "x");
        var yContinuous = IsContinuous(y, "y");
        if (xContinuous && yContinuous)
        {
            var xAggregate = x!["aggregate"]?.AsString();
            var yAggregate = y!["aggregate"]?.AsString();
            if (xAggregate is null && yAggregate == type)
            {
                orient = "vertical";
            }
            else if (yAggregate is null && xAggregate == type)
            {
                orient = "horizontal";
            }
            else if (xAggregate == type && yAggregate == type)
            {
                problem = $"The x and y channels of a {type} cannot both have the aggregate '{type}'.";
                return false;
            }
            else
            {
                orient = TypeOf(y, "y") == "temporal" && TypeOf(x, "x") != "temporal" ? "horizontal" : "vertical";
            }

            return true;
        }

        if (xContinuous || yContinuous)
        {
            orient = xContinuous ? "horizontal" : "vertical";
            return true;
        }

        problem = $"A {type} needs a quantitative or temporal field on the x or y channel (or an 'orient' and one such channel).";
        return false;
    }

    /// <summary>
    ///     Finds the continuous axis of a composite mark for an orientation.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="orient"><c>vertical</c> (the measure is on <c>y</c>) or <c>horizontal</c>.</param>
    /// <returns>The axis.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the channel is missing or has no field.</exception>
    public CompositeAxis ContinuousAxis(SpecNode? encoding, string orient)
    {
        var channel = orient == "vertical" ? "y" : "x";
        if (encoding?[channel] is not { IsObject: true } definition || definition["field"] is not { IsString: true } field)
        {
            throw Error($"The '{channel}' channel of a {orient} {Type} must be a field.");
        }

        return new CompositeAxis(channel, channel + "2", definition, field.StringValue!, AliasOf(field.StringValue!), TypeOf(definition, channel)!);
    }

    /// <summary>
    ///     Names a field the way the aggregated and computed rows carry it: its path segments joined with <c>.</c>.
    /// </summary>
    /// <param name="field">The field as written.</param>
    /// <returns>The name.</returns>
    public static string AliasOf(string field)
    {
        var segments = FieldPath.Parse(field);
        return segments is { Count: > 0 } ? string.Join('.', segments) : field;
    }

    /// <summary>
    ///     Tells whether a channel definition is continuous: a field of the type quantitative or temporal, or a numeric datum
    ///     (Vega-Lite's <c>isContinuousFieldOrDatumDef</c>; a string or date-time datum is not continuous).
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <param name="channel">The channel name.</param>
    /// <returns><see langword="true"/> for a continuous definition.</returns>
    public static bool IsContinuous(SpecNode? definition, string channel) =>
        definition is { IsObject: true }
        && (definition["datum"] is { } datum
            ? datum.IsNumber
            : (definition["field"] is not null || definition["aggregate"] is { IsNull: false }) && TypeOf(definition, channel) is "quantitative" or "temporal");

    /// <summary>
    ///     Creates a position channel definition of the continuous axis for a field that the expansion computes.
    /// </summary>
    /// <remarks>The <c>title</c> (the field when none is written), the <c>scale</c> and the <c>axis</c> of the written definition are kept.</remarks>
    /// <param name="axis">The continuous axis of the mark.</param>
    /// <param name="channel">The channel to define: the axis' own channel or its secondary.</param>
    /// <param name="field">The field.</param>
    /// <returns>The channel definition.</returns>
    public SpecNode PositionDefinition(CompositeAxis axis, string channel, string field)
    {
        var primary = channel == axis.Channel;
        return Obj(
            channel,
            Str("field", field),
            primary ? Str("type", axis.Type) : null,
            primary ? axis.Definition["title"] ?? Str("title", axis.Field) : null,
            primary ? axis.Definition["scale"] : null,
            primary ? axis.Definition["axis"] : null);
    }

    /// <summary>
    ///     Splits the encoding of a composite mark into the channels the parts share and the fields that group the rows.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite groups the rows by every channel that has a field (<c>extractTransformsFromEncoding</c> in <c>encoding.ts</c>). The <c>timeUnit</c> of such a channel (a unit that is
    ///     not <c>binned</c>) becomes a <c>timeUnit</c> transform that runs before the aggregate, and the channel reads the derived field (named like <c>month_date</c>) with the default
    ///     title of the original definition and, for a channel that is not temporal, the <c>formatType</c> <c>time</c> of its axis, legend or text; the derived field groups the rows.
    ///     A <c>bin</c> of such a channel is not applied (the raw field groups the rows) and a channel that aggregates is left out: the feature catalog reports both as <c>vl-composite-grouping</c>.
    ///     The channels of the measure (<c>x</c> or <c>y</c> and their second ends and errors) and the <c>tooltip</c> are not shared.
    /// </remarks>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="axis">The continuous axis.</param>
    /// <returns>The shared channels in order, the fields to group by, and the <c>timeUnit</c> transforms to run before the aggregate.</returns>
    public (List<SpecNode> Channels, List<string> GroupBy, List<SpecNode> TimeUnits) Grouping(SpecNode encoding, CompositeAxis axis)
    {
        var channels = new List<SpecNode>();
        var groupBy = new List<string>();
        var timeUnits = new List<SpecNode>();
        foreach (var channel in encoding.Properties)
        {
            if (channel.Name == axis.Channel || IsOwnChannel(channel.Name!))
            {
                continue;
            }

            if (!channel.IsObject)
            {
                channels.Add(channel);
                continue;
            }

            if (channel["aggregate"] is { IsNull: false })
            {
                continue;
            }

            string? groupField = channel["field"] is { IsString: true } field ? field.StringValue : null;
            SpecNode shared;
            if (channel["bin"] is null && channel["timeUnit"] is { } unitNode && groupField is not null && TimeUnitSpec.TryRead(unitNode, out var unit) && !unit.IsBinned && unit.Parts.Count > 0)
            {
                var source = groupField;
                groupField = unit.FieldName(source);
                timeUnits.Add(Obj(null, unitNode, Str("field", source), Str("as", groupField)));
                shared = TimeUnitChannel(channel, groupField, unit);
            }
            else if (channel["bin"] is not null || channel["timeUnit"] is not null)
            {
                shared = ObjOf(channel.Name, channel.Properties.Where(p => p.Name is not ("bin" or "timeUnit")));
            }
            else
            {
                shared = channel;
            }

            channels.Add(shared);
            if (groupField is not null && !groupBy.Contains(groupField, StringComparer.Ordinal))
            {
                groupBy.Add(groupField);
            }
        }

        return (channels, groupBy, timeUnits);
    }

    /// <summary>
    ///     Writes the definition of a channel that reads the field a <c>timeUnit</c> transform derived.
    /// </summary>
    /// <param name="channel">The channel as written.</param>
    /// <param name="derived">The name of the derived field.</param>
    /// <param name="unit">The time unit.</param>
    /// <returns>The definition without the <c>timeUnit</c>, with the derived field, a title and the format type for a field that is not temporal.</returns>
    private SpecNode TimeUnitChannel(SpecNode channel, string derived, TimeUnitSpec unit)
    {
        var properties = new List<SpecNode?> { Str("field", derived) };
        var hasTitle = channel["title"] is not null || channel["axis"]?["title"] is not null || channel["legend"]?["title"] is not null;
        if (!hasTitle)
        {
            properties.Add(Str("title", Transforms.EncodingPlan.DefaultTimeUnitTitle(channel["field"]!.StringValue!, unit, Config)));
        }

        var needsFormat = channel["type"]?.AsString() is { } type && type != "temporal";
        var guide = channel.Name switch
        {
            "x" or "y" => "axis",
            "text" => "formatType",
            _ => "legend",
        };
        foreach (var property in channel.Properties.Where(p => p.Name is not ("timeUnit" or "field" or "bin")))
        {
            if (needsFormat && property.Name == guide && property.IsObject)
            {
                properties.Add(ObjOf(property.Name, [Str("formatType", "time"), .. property.Properties.Where(p => p.Name != "formatType")]));
                continue;
            }

            properties.Add(property);
        }

        if (needsFormat && channel[guide] is null)
        {
            properties.Add(guide == "formatType" ? Str("formatType", "time") : Obj(guide, Str("formatType", "time")));
        }

        return ObjOf(channel.Name, properties);
    }

    /// <summary>
    ///     Writes the access of a field of the row in an expression: <c>datum['name']</c>.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <returns>The expression text.</returns>
    public static string Access(string field) => $"datum['{field.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}']";

    /// <summary>
    ///     Tells whether a channel is one that the composite mark computes or ignores rather than shares with its parts.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns><see langword="true"/> for the second ends and errors of the positions and the tooltip.</returns>
    private static bool IsOwnChannel(string channel) =>
        channel is "x2" or "y2" or "xError" or "xError2" or "yError" or "yError2" or "tooltip";

    /// <summary>
    ///     Adds a property to a list, replacing a property of the same name in place.
    /// </summary>
    /// <param name="properties">The properties.</param>
    /// <param name="property">The property to put.</param>
    private static void Put(List<SpecNode> properties, SpecNode property)
    {
        var existing = properties.FindIndex(p => string.Equals(p.Name, property.Name, StringComparison.Ordinal));
        if (existing >= 0)
        {
            properties[existing] = property;
        }
        else
        {
            properties.Add(property);
        }
    }

    /// <summary>
    ///     Tells whether a value is truthy the way JavaScript reads it: not null, false, zero or the empty string.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> when the value is truthy.</returns>
    private static bool IsTruthy(SpecNode node) => node.Kind switch
    {
        SpecNodeKind.Null => false,
        SpecNodeKind.Boolean => node.BoolValue,
        SpecNodeKind.Number => node.NumberValue != 0,
        SpecNodeKind.String => node.StringValue!.Length > 0,
        _ => true,
    };
}

/// <content>
///     The expansion of the composite marks into the layers they stand for.
/// </content>
internal static partial class SpecNormalizer
{
    /// <summary>
    ///     Builds the layer tree of a specification with its composite marks expanded into primitive layers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A <c>boxplot</c>, <c>errorbar</c> or <c>errorband</c> unit view becomes a layered view whose layers are the rule, tick, bar, rect, area, line and point views Vega-Lite normalizes it to (the aggregate, joinaggregate, calculate
    ///         and filter transforms are views' own <c>transform</c>s, so the data walk of the document runs them like those of the specification). The new views count as views
    ///         against <see cref="VegaLiteLimits.MaxViews"/>; they are not nesting levels of <c>layer</c>.
    ///     </para>
    ///     <para>
    ///         The returned tree reads the specification's own nodes where it can; the nodes the expansion writes are located at the composite mark's <c>mark</c>.
    ///     </para>
    /// </remarks>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="config">The effective configuration; the composite mark's defaults (<c>config.boxplot</c>) are read from it.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <returns>The expanded layer tree and the specification tree that holds the expressions of the expansion.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> or <paramref name="config"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the orientation of a composite mark cannot be decided or its measure is not a field.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at the offending view, when a limit of <see cref="Layers"/> is exceeded (the expanded views count).</exception>
    public static ExpandedLayers Expand(SpecTree tree, ResolvedConfig config, VegaLiteLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(config);
        var effective = limits ?? VegaLiteLimits.Default;
        var expressions = new List<(SpecNode Node, CompiledExpression Expression)>();
        var layers = Build(tree, effective, unit => ExpandComposite(tree, config, effective, unit, expressions), config);
        var derived = expressions.Count == 0 ? tree : new SpecTree(tree.Root, tree.SourceName, tree.Warnings, tree.Expressions.With(expressions));
        return new ExpandedLayers(layers, derived);
    }

    /// <summary>
    ///     Gets the type of a composite mark of a unit view that the expansion handles.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <returns>The composite mark type (<c>boxplot</c>); null when the mark is not one that is expanded.</returns>
    public static string? ExpandedCompositeType(SpecNode unit)
    {
        var mark = unit["mark"];
        var type = mark is { IsObject: true } ? mark["type"]?.AsString() : mark?.AsString();
        return type is "boxplot" or "errorbar" or "errorband" ? type : null;
    }

    /// <summary>
    ///     Expands the composite mark of a unit view.
    /// </summary>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="unit">The unit view, with its merged encoding.</param>
    /// <param name="expressions">Receives the expressions the expansion compiled.</param>
    /// <returns>The views of the layers; null when the unit view is not a composite mark that is expanded.</returns>
    private static IReadOnlyList<SpecNode>? ExpandComposite(
        SpecTree tree,
        ResolvedConfig config,
        VegaLiteLimits limits,
        SpecNode unit,
        List<(SpecNode Node, CompiledExpression Expression)> expressions)
    {
        if (ExpandedCompositeType(unit) is not { } type || unit["mark"] is not { } mark)
        {
            return null;
        }

        var builder = new CompositeBuilder(tree, config, limits, mark, expressions);
        return type == "boxplot" ? BoxPlot(builder, unit["encoding"]) : ErrorMark(builder, unit["encoding"]);
    }
}
