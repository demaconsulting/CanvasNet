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
