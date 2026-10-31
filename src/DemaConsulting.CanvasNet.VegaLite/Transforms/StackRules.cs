// Portions derived from Vega-Lite (BSD-3-Clause): the stack decision; see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The decision that a view stacks its marks: which position channel is stacked, how, and by which fields.
/// </summary>
/// <param name="Channel">The stacked (measure) channel: <c>x</c>, <c>y</c>, <c>theta</c> or <c>radius</c>.</param>
/// <param name="Offset">The stack offset: <c>zero</c>, <c>center</c> or <c>normalize</c>.</param>
/// <param name="StackBy">The definitions of the non-position channels whose fields split a group of rows into the layers of the stack, in channel order.</param>
internal sealed record StackDecision(string Channel, string Offset, IReadOnlyList<SpecNode> StackBy);

/// <summary>
///     Decides, from the specification alone, whether the marks of a view are stacked, the way Vega-Lite does.
/// </summary>
/// <remarks>
///     <para>
///         The rule is the one of Vega-Lite's <c>stack</c> function (public algorithm, re-implemented here). A mark is stackable when
///         it is one of arc, area, bar, rule, point, circle, square, line, text and tick; bar, area and arc stack by default and
///         the others only with an explicit <c>stack</c>. The stacked channel is the unbinned quantitative one of <c>x</c> and
///         <c>y</c>; when both are (and neither says <c>stack</c>) the one that aggregates and the other does not wins, and then the
///         explicit <c>orient</c> of a bar or area decides. The other position channel names the group
///         of the stack. A stack needs at least one stack-by field when the encoding aggregates, never exists for a range
///         (a secondary channel <c>x2</c> or <c>y2</c> with a field or datum) or for a stacked channel whose <c>scale.type</c> is log, pow, sqrt or
///         symlog (Vega-Lite does not stack on a scale that is not linear), and an explicit <c>stack</c> of false or null switches
///         it off. The Cartesian channels <c>x</c> and <c>y</c> are decided first; when they give no stack the same rule is applied to the polar
///         channels <c>theta</c> and <c>radius</c> (with <c>radius</c> as the dimension of <c>theta</c> and the other way round; the <c>orient</c> of a bar
///         or area applies to the Cartesian channels only), and a secondary <c>theta2</c> or <c>radius2</c> with a field makes a ranged mark.
///     </para>
///     <para>
///         The analysis reads the specification only (no data), so the feature catalog can use it to decide whether the
///         <c>order</c> channel orders a stack that will exist, and the encoding stage can use it to plan one.
///     </para>
/// </remarks>
internal static class StackRules
{
    /// <summary>The marks that can be stacked.</summary>
    private static readonly HashSet<string> StackableMarks = ["arc", "area", "bar", "rule", "point", "circle", "square", "line", "text", "tick"];

    /// <summary>The marks that stack without an explicit <c>stack</c>.</summary>
    private static readonly HashSet<string> StackByDefaultMarks = ["arc", "area", "bar"];

    /// <summary>The non-position channels whose fields split a stack (the tooltip is deliberately left out, as Vega-Lite does).</summary>
    private static readonly string[] StackByChannels =
    [
        "color", "fill", "stroke", "opacity", "fillOpacity", "strokeOpacity", "strokeWidth", "strokeDash", "size", "angle", "shape",
        "order", "text", "detail", "key", "href", "url", "description",
    ];

    /// <summary>The stack offsets.</summary>
    private static readonly HashSet<string> Offsets = ["zero", "center", "normalize"];

    /// <summary>
    ///     Decides whether the marks of a view are stacked.
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="orient">The <c>orient</c> written in the mark definition, or null.</param>
    /// <param name="encoding">The <c>encoding</c> object, or null.</param>
    /// <returns>The decision, or null when the marks are not stacked.</returns>
    public static StackDecision? Decide(string markType, string? orient, SpecNode? encoding)
    {
        ArgumentNullException.ThrowIfNull(markType);
        if (encoding is not { IsObject: true } || !StackableMarks.Contains(markType))
        {
            return null;
        }

        // Vega-Lite decides the Cartesian channels first and the polar ones when those give no stack, so a text mark can be stacked in either
        var channel = PotentialStackedChannel(encoding, "x", markType, orient) ?? PotentialStackedChannel(encoding, "theta", markType, orient);
        if (channel is null)
        {
            return null;
        }

        var stacked = encoding[channel]!;
        var other = DimensionChannel(channel);

        // Vega-Lite warns and does not stack on a scale that is not linear (log, pow, sqrt, symlog): every mark is drawn from the baseline
        if (stacked["scale"]?["type"]?.AsString() is "log" or "pow" or "sqrt" or "symlog")
        {
            return null;
        }

        var offset = Offset(stacked["stack"], markType);
        if (offset is null)
        {
            return null;
        }

        var groupBy = new HashSet<string>(StringComparer.Ordinal);
        var stackedKey = FieldKey(stacked);
        if (encoding[other] is { IsObject: true } dimension && IsFieldDef(dimension) && FieldKey(dimension) is { } key && key != stackedKey)
        {
            groupBy.Add(key);
        }

        var stackBy = new List<SpecNode>();
        foreach (var name in StackByChannels)
        {
            if (encoding[name] is not { } definition)
            {
                continue;
            }

            foreach (var item in definition.IsArray ? definition.Items : [definition])
            {
                // A channel without a field (a value) never splits a stack; an aggregated one is a measure, not a layer
                if (!item.IsObject || (item["field"] is null && item["aggregate"] is null) || item["aggregate"] is { IsNull: false })
                {
                    continue;
                }

                if (FieldKey(item) is not { } fieldKey || !groupBy.Contains(fieldKey))
                {
                    stackBy.Add(item);
                }
            }
        }

        // An aggregating encoding that has nothing to split the groups by has one row per group: nothing to stack
        if (stackBy.Count == 0 && IsAggregating(encoding))
        {
            return null;
        }

        // A ranged mark (a secondary channel that is a field or a datum, such as x2 or theta2) draws its own extent
        var secondary = encoding[channel + "2"];
        if (secondary is { IsObject: true } && (secondary["field"] is not null || secondary["datum"] is not null || secondary["aggregate"] is { IsNull: false }))
        {
            return null;
        }

        return new StackDecision(channel, offset, stackBy);
    }

    /// <summary>
    ///     Determines whether a channel definition is a field definition (it has a field, or counts the rows).
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns><see langword="true"/> for a field with a name or an aggregate <c>count</c>.</returns>
    public static bool IsFieldDef([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] SpecNode? definition) =>
        definition is { IsObject: true } && (definition["field"] is { IsString: true } || definition["aggregate"]?.AsString() == "count");

    /// <summary>
    ///     Reads the type of a field definition the way Vega-Lite defaults it (a field without a type is quantitative).
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The type text.</returns>
    public static string TypeOf(SpecNode definition) => definition["type"]?.AsString() ?? "quantitative";

    /// <summary>
    ///     Determines whether a switch property is on: anything except absent, null and false.
    /// </summary>
    /// <param name="node">The value, or null when absent.</param>
    /// <returns><see langword="true"/> when the feature is switched on.</returns>
    public static bool IsEnabled(SpecNode? node) => node is { IsNull: false } && !(node.IsBoolean && !node.BoolValue);

    /// <summary>
    ///     Gets the channel that is the dimension of a stacked channel: the one whose field names the stack.
    /// </summary>
    /// <param name="channel">The stacked channel: <c>x</c>, <c>y</c>, <c>theta</c> or <c>radius</c>.</param>
    /// <returns><c>y</c> for <c>x</c>, <c>x</c> for <c>y</c>, <c>radius</c> for <c>theta</c> and <c>theta</c> for <c>radius</c>.</returns>
    public static string DimensionChannel(string channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return channel switch
        {
            "x" => "y",
            "y" => "x",
            "theta" => "radius",
            "radius" => "theta",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "The channel cannot be stacked."),
        };
    }

    /// <summary>
    ///     Finds the channel of a pair that carries the stacked measure (Vega-Lite's <c>potentialStackedChannel</c>).
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="first">The first channel of the pair: <c>x</c> (the pair is <c>x</c> and <c>y</c>) or <c>theta</c> (the pair is <c>theta</c> and <c>radius</c>).</param>
    /// <param name="markType">The mark type.</param>
    /// <param name="orient">The orient written in the mark definition, or null.</param>
    /// <returns>One of the two channels of the pair, or null when none of them can be stacked.</returns>
    private static string? PotentialStackedChannel(SpecNode encoding, string first, string markType, string? orient)
    {
        var second = first == "x" ? "y" : "radius";
        var x = encoding[first];
        var y = encoding[second];

        // The orientation of a bar or an area only applies to Cartesian positions
        var cartesianBarOrArea = first == "x" && markType is "bar" or "area";
        if (IsFieldDef(x) && IsFieldDef(y))
        {
            if (IsUnbinnedQuantitative(x) && IsUnbinnedQuantitative(y))
            {
                if (IsStackSet(x["stack"]))
                {
                    return first;
                }

                if (IsStackSet(y["stack"]))
                {
                    return second;
                }

                // With no explicit stack only the one that aggregates is stacked; with the same on both the orientation decides
                var xAggregates = x["aggregate"] is { IsNull: false };
                var yAggregates = y["aggregate"] is { IsNull: false };
                if (xAggregates != yAggregates)
                {
                    return xAggregates ? first : second;
                }

                return cartesianBarOrArea ? orient switch { "vertical" => second, "horizontal" => first, _ => null } : null;
            }

            if (IsUnbinnedQuantitative(x))
            {
                return first;
            }

            return IsUnbinnedQuantitative(y) ? second : null;
        }

        if (IsUnbinnedQuantitative(x))
        {
            return cartesianBarOrArea && orient == "vertical" ? null : first;
        }

        if (IsUnbinnedQuantitative(y))
        {
            return cartesianBarOrArea && orient == "horizontal" ? null : second;
        }

        return null;
    }

    /// <summary>
    ///     Determines the stack offset: the explicit one, or <c>zero</c> for the marks that stack by default.
    /// </summary>
    /// <param name="stack">The <c>stack</c> property of the stacked channel, or null when absent.</param>
    /// <param name="markType">The mark type.</param>
    /// <returns>The offset, or null when there is no stack.</returns>
    private static string? Offset(SpecNode? stack, string markType)
    {
        if (stack is null)
        {
            return StackByDefaultMarks.Contains(markType) ? "zero" : null;
        }

        return stack.Kind switch
        {
            SpecNodeKind.Boolean => stack.BoolValue ? "zero" : null,
            SpecNodeKind.String when Offsets.Contains(stack.StringValue!) => stack.StringValue,
            _ => null,
        };
    }

    /// <summary>
    ///     Determines whether a <c>stack</c> property asks for a stack (a non-empty string or true).
    /// </summary>
    /// <param name="stack">The property, or null.</param>
    /// <returns><see langword="true"/> when the property is set to something that stacks.</returns>
    private static bool IsStackSet(SpecNode? stack) =>
        stack is { IsString: true } || (stack is { IsBoolean: true } && stack.BoolValue);

    /// <summary>
    ///     Determines whether a definition is an unbinned quantitative field.
    /// </summary>
    /// <param name="definition">The definition, or null.</param>
    /// <returns><see langword="true"/> for a measure that is not binned.</returns>
    private static bool IsUnbinnedQuantitative([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] SpecNode? definition) =>
        IsFieldDef(definition) && TypeOf(definition) == "quantitative" && !IsEnabled(definition["bin"]);

    /// <summary>
    ///     Determines whether any channel of an encoding aggregates.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <returns><see langword="true"/> when a definition has an <c>aggregate</c>.</returns>
    private static bool IsAggregating(SpecNode encoding)
    {
        foreach (var channel in encoding.Properties)
        {
            foreach (var item in channel.IsArray ? channel.Items : [channel])
            {
                if (item.IsObject && item["aggregate"] is { IsNull: false })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Builds a text that identifies the field a definition reads, so that two definitions that read the same derived field compare equal.
    /// </summary>
    /// <remarks>
    ///     The key is the aggregate, the field, the bin parameters and the time unit as written. It is not the Vega field name of
    ///     the derived field (the encoding plan builds that); it only has to be equal exactly when that name would be.
    /// </remarks>
    /// <param name="definition">The definition.</param>
    /// <returns>The key, or null when the definition has no field.</returns>
    private static string? FieldKey(SpecNode definition)
    {
        if (!IsFieldDef(definition))
        {
            return null;
        }

        var key = new StringBuilder();
        key.Append(definition["aggregate"] is { IsNull: false } aggregate ? Describe(aggregate) : "-").Append('|');
        key.Append(definition["field"]?.AsString() ?? "-").Append('|');
        key.Append(IsEnabled(definition["bin"]) ? Describe(definition["bin"]!) : "-").Append('|');
        key.Append(definition["timeUnit"] is { IsNull: false } timeUnit ? Describe(timeUnit) : "-");
        return key.ToString();
    }

    /// <summary>
    ///     Writes a value as compact text, with the properties of an object in the order they were written.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The text.</returns>
    private static string Describe(SpecNode node)
    {
        var text = new StringBuilder();
        Append(text, node);
        return text.ToString();
    }

    /// <summary>
    ///     Appends the compact text of a value.
    /// </summary>
    /// <param name="text">The builder.</param>
    /// <param name="node">The value.</param>
    private static void Append(StringBuilder text, SpecNode node)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                text.Append('"').Append(node.StringValue).Append('"');
                break;
            case SpecNodeKind.Number:
                text.Append(node.NumberValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case SpecNodeKind.Boolean:
                text.Append(node.BoolValue ? "true" : "false");
                break;
            case SpecNodeKind.Null:
                text.Append("null");
                break;
            case SpecNodeKind.Array:
                text.Append('[');
                foreach (var item in node.Items)
                {
                    Append(text, item);
                    text.Append(',');
                }

                text.Append(']');
                break;
            default:
                text.Append('{');
                foreach (var property in node.Properties)
                {
                    text.Append(property.Name).Append(':');
                    Append(text, property);
                    text.Append(',');
                }

                text.Append('}');
                break;
        }
    }
}
