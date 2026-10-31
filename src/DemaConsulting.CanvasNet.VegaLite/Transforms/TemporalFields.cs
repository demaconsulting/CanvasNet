// Portions derived from Vega-Lite (BSD-3-Clause): the implicit parse of temporal fields (the ParseNode rules); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Finds the fields of a view that are read as dates: Vega-Lite parses them as dates even when <c>format.parse</c> does
///     not name them (its implicit <c>parse</c>), so that a text such as <c>2024-03-05</c> becomes a time.
/// </summary>
/// <remarks>
///     <para>
///         <b>Encoding.</b> A field of the encoding is a date field when its definition has the type <c>temporal</c>, has a
///         <c>timeUnit</c>, or asks for the format type <c>time</c> (on the definition, its axis or its legend). The
///         secondary channels <c>x2</c>, <c>y2</c>, <c>theta2</c> and <c>radius2</c> take the type of their main channel,
///         and the field of a condition is judged by the definition of the condition itself. A field name is returned in its
///         canonical form, which is how the loader names the fields of a row.
///     </para>
///     <para>
///         <b>Cost.</b> One pass over the encoding of the specification; nothing is proportional to the data.
///     </para>
/// </remarks>
internal static class TemporalFields
{
    /// <summary>The secondary position and polar channels and their main channels.</summary>
    private static readonly IReadOnlyDictionary<string, string> MainChannels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["x2"] = "x",
        ["y2"] = "y",
        ["theta2"] = "theta",
        ["radius2"] = "radius",
    };

    /// <summary>
    ///     Collects the canonical names of the fields of the encoding of a view that are read as dates.
    /// </summary>
    /// <param name="view">The view object (its <c>encoding</c> is read).</param>
    /// <returns>The canonical field names; empty when the view has no encoding or no temporal field.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> is null.</exception>
    public static IReadOnlySet<string> Collect(SpecNode view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (view["encoding"] is not { IsObject: true } encoding)
        {
            return fields;
        }

        foreach (var channel in encoding.Properties)
        {
            foreach (var definition in channel.IsArray ? channel.Items : [channel])
            {
                if (!definition.IsObject)
                {
                    continue;
                }

                // A secondary channel without a type of its own is judged by the main channel
                var main = MainChannels.TryGetValue(channel.Name!, out var mainName) && encoding[mainName] is { IsObject: true } mainDefinition
                    ? mainDefinition
                    : null;
                AddDefinition(fields, definition, main);
                foreach (var condition in ChannelDefinitions.Conditions(definition))
                {
                    AddDefinition(fields, condition, main);
                }
            }
        }

        return fields;
    }

    /// <summary>
    ///     Adds the field of one definition when it is read as a date.
    /// </summary>
    /// <param name="fields">Receives the canonical name.</param>
    /// <param name="definition">The definition (or condition) object.</param>
    /// <param name="main">The definition of the main channel for a secondary channel, or null.</param>
    private static void AddDefinition(HashSet<string> fields, SpecNode definition, SpecNode? main)
    {
        if (definition["field"] is not { IsString: true } field || FieldPath.Canonicalize(field.StringValue) is not { } canonical)
        {
            return;
        }

        var judged = definition["type"] is null && main is not null ? main : definition;
        if (IsDate(judged) || IsDate(definition))
        {
            fields.Add(canonical);
        }
    }

    /// <summary>
    ///     Determines whether a definition reads its field as dates.
    /// </summary>
    /// <param name="definition">The definition object.</param>
    /// <returns><see langword="true"/> for the type <c>temporal</c>, a <c>timeUnit</c> or the format type <c>time</c>.</returns>
    private static bool IsDate(SpecNode definition)
    {
        if (definition["type"]?.AsString() == "temporal" || definition["timeUnit"] is { IsNull: false })
        {
            return true;
        }

        return IsTimeFormat(definition) || IsTimeFormat(definition["axis"]) || IsTimeFormat(definition["legend"]);
    }

    /// <summary>
    ///     Determines whether an object asks for the format type <c>time</c>.
    /// </summary>
    /// <param name="node">The definition, axis or legend object, or null.</param>
    /// <returns><see langword="true"/> when its <c>formatType</c> is <c>time</c>.</returns>
    private static bool IsTimeFormat(SpecNode? node) =>
        node is { IsObject: true } && node["formatType"]?.AsString() == "time";
}
