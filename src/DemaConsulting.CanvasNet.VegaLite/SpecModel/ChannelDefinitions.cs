// Portions derived from Vega-Lite (BSD-3-Clause): the field definition of a conditional channel (getFieldOrDatumDef); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     Answers which object of a channel definition carries the field, and which objects are its conditions.
/// </summary>
/// <remarks>
///     <para>
///         In Vega-Lite a channel definition with a <c>condition</c> is either a field definition whose conditions are value definitions
///         (<c>{"field": ..., "condition": {"test": ..., "value": ...}}</c>) or a value definition whose condition is a field definition
///         (<c>{"condition": {"test": ..., "field": ...}, "value": ...}</c>). The scale, the axis and the legend of the channel belong to the
///         field definition, wherever it is (Vega-Lite's <c>getFieldOrDatumDef</c>), so a condition that holds a field is the definition that the
///         scale resolver, the encoding plan and the legends read; a condition that holds only a value draws no scale and no legend by itself.
///     </para>
///     <para>The class is static and pure; it reads the specification tree only.</para>
/// </remarks>
internal static class ChannelDefinitions
{
    /// <summary>The channels that a condition may be put on: the ones that are drawn from a scale and have no position. The polar position channels <c>theta</c> and <c>radius</c> take none, as in Vega-Lite.</summary>
    private static readonly HashSet<string> ConditionChannels =
        new(["color", "fill", "stroke", "opacity", "fillOpacity", "strokeOpacity", "strokeWidth", "strokeDash", "size", "shape", "angle"], StringComparer.Ordinal);

    /// <summary>
    ///     Tells whether a channel accepts a <c>condition</c> in this renderer.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns><see langword="true"/> for the color, opacity, size, shape, stroke, and angle channels (Vega-Lite does not condition the position channels).</returns>
    public static bool CanCondition(string channel) => ConditionChannels.Contains(channel);

    /// <summary>
    ///     Tells whether a definition object is a field definition: it names a field, a datum, or an aggregate (a count needs no field).
    /// </summary>
    /// <param name="definition">The definition object.</param>
    /// <returns><see langword="true"/> when the object carries the field of a channel.</returns>
    public static bool HasField(SpecNode definition) =>
        definition["field"] is not null || definition["datum"] is not null || definition["aggregate"] is { IsNull: false };

    /// <summary>
    ///     Gets the main channel of a secondary range channel: <c>x</c> for <c>x2</c>, <c>y</c> for <c>y2</c>, <c>theta</c> for <c>theta2</c>, <c>radius</c> for <c>radius2</c>,
    ///     <c>latitude</c> for <c>latitude2</c> and <c>longitude</c> for <c>longitude2</c> (Vega-Lite's <c>getMainRangeChannel</c>).
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns>The main channel; null when the channel is not a secondary range channel.</returns>
    public static string? MainChannelOf(string channel) => channel switch
    {
        "x2" => "x",
        "y2" => "y",
        "theta2" => "theta",
        "radius2" => "radius",
        "latitude2" => "latitude",
        "longitude2" => "longitude",
        _ => null,
    };

    /// <summary>
    ///     Gets the effective type of a field (or datum) definition for a channel: the written <c>type</c>, or else the type Vega-Lite infers (its <c>defaultType</c>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A <c>type</c> is optional in Vega-Lite 5. When the definition has none, the type is, in this order: <c>quantitative</c> for <c>latitude</c> and <c>longitude</c>; <c>nominal</c> for
    ///         <c>row</c>, <c>column</c>, <c>facet</c>, <c>shape</c> and <c>strokeDash</c>; <c>ordinal</c> for <c>order</c>; <c>ordinal</c> for a <c>sort</c> array;
    ///         <c>temporal</c> for a <c>timeUnit</c>; <c>quantitative</c> for a <c>bin</c> or an <c>aggregate</c> (except <c>argmin</c> and <c>argmax</c>); the type of the
    ///         <c>scale.type</c> (<c>quantitative</c> for a numeric or discretizing scale, <c>temporal</c> for <c>time</c> and <c>utc</c>); and <c>nominal</c> otherwise.
    ///         The channel does not matter beyond the first three rules, so a field on <c>x</c> or <c>y</c> without a type is nominal.
    ///         A secondary channel (<c>x2</c>, <c>y2</c>, <c>theta2</c>, <c>radius2</c>) takes the type of its main channel. A datum takes its type from its value: a number is
    ///         quantitative, a text or a boolean nominal, a date-time object temporal.
    ///     </para>
    ///     <para>The method reads the specification tree only and is constant work.</para>
    /// </remarks>
    /// <param name="definition">The field or datum definition object (the condition that holds the field, for a conditional channel).</param>
    /// <param name="channel">The channel name.</param>
    /// <param name="encoding">The encoding object that holds the main channel of a secondary channel, or null.</param>
    /// <returns>The type text: <c>quantitative</c>, <c>nominal</c>, <c>ordinal</c>, <c>temporal</c> or <c>geojson</c>.</returns>
    public static string TypeOf(SpecNode definition, string channel, SpecNode? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(channel);
        if (definition["type"] is { IsString: true } written)
        {
            return written.StringValue!;
        }

        // A secondary channel has the type of its main channel
        if (MainChannelOf(channel) is { } main)
        {
            if (encoding?[main] is { IsObject: true } mainDefinition)
            {
                return TypeOf(FieldDefinitionOf(mainDefinition), main, encoding);
            }

            channel = main;
        }

        return InferType(definition, channel);
    }

    /// <summary>
    ///     Infers the type of a definition that has none (Vega-Lite's <c>defaultType</c> and the type of a datum).
    /// </summary>
    /// <param name="definition">The definition object.</param>
    /// <param name="channel">The channel name.</param>
    /// <returns>The inferred type.</returns>
    private static string InferType(SpecNode definition, string channel)
    {
        // A datum has the type of its value, whatever the channel
        if (definition["datum"] is { } datum && definition["field"] is null)
        {
            return datum.Kind switch
            {
                SpecNodeKind.Number => "quantitative",
                SpecNodeKind.Object => "temporal",
                _ => "nominal",
            };
        }

        switch (channel)
        {
            case "latitude" or "longitude":
                return "quantitative";
            case "row" or "column" or "facet" or "shape" or "strokeDash":
                return "nominal";
            case "order":
                return "ordinal";
            default:
                break;
        }

        if (definition["sort"] is { IsArray: true })
        {
            return "ordinal";
        }

        if (definition["timeUnit"] is { IsNull: false } and not { IsBoolean: true, BoolValue: false })
        {
            return "temporal";
        }

        if (IsOn(definition["bin"]) || (definition["aggregate"] is { IsNull: false } aggregate && !IsArgAggregate(aggregate)))
        {
            return "quantitative";
        }

        return definition["scale"]?["type"]?.AsString() switch
        {
            "linear" or "log" or "pow" or "sqrt" or "symlog" or "identity" or "sequential" or "quantile" or "quantize" or "threshold" => "quantitative",
            "time" or "utc" => "temporal",
            _ => "nominal",
        };
    }

    /// <summary>Tells whether an aggregate is the <c>argmin</c> or <c>argmax</c> object, which keeps the type of the field.</summary>
    /// <param name="aggregate">The <c>aggregate</c> value.</param>
    /// <returns><see langword="true"/> for an object with <c>argmin</c> or <c>argmax</c>.</returns>
    private static bool IsArgAggregate(SpecNode aggregate) =>
        aggregate.IsObject && (aggregate["argmin"] is not null || aggregate["argmax"] is not null);

    /// <summary>Tells whether a switch property is on: anything except absent, null and false.</summary>
    /// <param name="node">The value, or null when absent.</param>
    /// <returns><see langword="true"/> when the property is on.</returns>
    private static bool IsOn(SpecNode? node) => node is { IsNull: false } && !(node.IsBoolean && !node.BoolValue);

    /// <summary>
    ///     Lists the conditions of a channel definition: the object of a single condition, or the objects of an array of conditions.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <returns>The condition objects in order; none when the definition has no condition.</returns>
    public static IEnumerable<SpecNode> Conditions(SpecNode definition)
    {
        if (definition["condition"] is not { } condition)
        {
            yield break;
        }

        if (condition.IsObject)
        {
            yield return condition;
        }
        else if (condition.IsArray)
        {
            foreach (var item in condition.Items.Where(i => i.IsObject))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    ///     Finds the object that carries the field of a channel: the definition itself, or else the condition that holds a field.
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <returns>The definition when it names a field, a datum, or an aggregate; otherwise the first condition on a test that does (a condition on a parameter is not supported and has no scale); otherwise the definition.</returns>
    public static SpecNode FieldDefinitionOf(SpecNode definition) =>
        HasField(definition) ? definition : TestConditions(definition).FirstOrDefault(HasField) ?? definition;

    /// <summary>
    ///     Lists the conditions of a channel definition that hold a test (a condition on a parameter is not evaluated).
    /// </summary>
    /// <param name="definition">The channel definition object.</param>
    /// <returns>The condition objects with a <c>test</c> and no <c>param</c>, in order.</returns>
    public static IEnumerable<SpecNode> TestConditions(SpecNode definition) =>
        Conditions(definition).Where(c => c["test"] is not null && c["param"] is null);

    /// <summary>
    ///     Finds the first value of a channel that a legend shows for a channel that no field drives: the value of the first condition, else the
    ///     value of the definition (Vega-Lite's <c>getFirstConditionValue</c>).
    /// </summary>
    /// <param name="definition">The channel definition object, or null.</param>
    /// <returns>The value node; null when the channel is driven by a field or has no value.</returns>
    public static SpecNode? FirstValue(SpecNode? definition) =>
        Values(definition).FirstOrDefault();

    /// <summary>
    ///     Lists the constant values of a channel that no field drives: the values of its conditions on a test, in order, then the value of the definition.
    /// </summary>
    /// <param name="definition">The channel definition object, or null.</param>
    /// <returns>The value nodes; none when a field drives the channel.</returns>
    public static IEnumerable<SpecNode> Values(SpecNode? definition)
    {
        if (definition is not { IsObject: true } || HasField(definition) || !ReferenceEquals(FieldDefinitionOf(definition), definition))
        {
            return [];
        }

        var values = TestConditions(definition).Select(c => c["value"]).OfType<SpecNode>().ToList();
        if (definition["value"] is { } value)
        {
            values.Add(value);
        }

        return values;
    }
}
