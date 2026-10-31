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
///         <b>Transforms and predicates.</b> The <c>field</c> of a <c>timeUnit</c> transform, and the field of a predicate (of a <c>filter</c> or of the
///         <c>test</c> of a condition, also inside <c>and</c>, <c>or</c> and <c>not</c>) that has a <c>timeUnit</c> or is compared with a date-time object, are date fields too.
///         A field that a transform derives (an <c>as</c>) is not read from the data, so it is not collected.
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
    ///     Collects the canonical names of the fields of a view that are read as dates: those of its encoding, its time unit transforms and its predicates.
    /// </summary>
    /// <param name="view">The view object (its <c>encoding</c> and <c>transform</c> are read).</param>
    /// <returns>The canonical field names; empty when the view has no temporal field.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> is null.</exception>
    public static IReadOnlySet<string> Collect(SpecNode view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        AddTransforms(fields, view["transform"]);
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
                    if (condition["test"] is { } test)
                    {
                        AddPredicate(fields, test);
                    }
                }
            }
        }

        return fields;
    }

    /// <summary>
    ///     Adds the fields that the transforms of a view read as dates: the <c>field</c> of a <c>timeUnit</c> transform and the fields of the filter predicates that
    ///     have a time unit or compare with a date-time object.
    /// </summary>
    /// <param name="fields">Receives the canonical names.</param>
    /// <param name="transforms">The <c>transform</c> array, or null.</param>
    private static void AddTransforms(HashSet<string> fields, SpecNode? transforms)
    {
        if (transforms is not { IsArray: true })
        {
            return;
        }

        foreach (var entry in transforms.Items.Where(e => e.IsObject))
        {
            if (entry["timeUnit"] is { IsNull: false } && entry["field"] is { IsString: true } field && FieldPath.Canonicalize(field.StringValue) is { } canonical)
            {
                fields.Add(canonical);
            }

            if (entry["filter"] is { } filter)
            {
                AddPredicate(fields, filter);
            }
        }
    }

    /// <summary>
    ///     Adds the fields of the predicates of a filter or of the test of a condition that are compared as dates: a predicate with a <c>timeUnit</c>, or one whose
    ///     compared values include a date-time object.
    /// </summary>
    /// <remarks>
    ///     The walk uses an explicit stack, so the nesting of <c>and</c>, <c>or</c> and <c>not</c> cannot overflow the call stack. An expression text compares nothing by field.
    /// </remarks>
    /// <param name="fields">Receives the canonical names.</param>
    /// <param name="predicate">The predicate: an expression text, a composition or a field predicate.</param>
    private static void AddPredicate(HashSet<string> fields, SpecNode predicate)
    {
        var pending = new Stack<SpecNode>();
        pending.Push(predicate);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!node.IsObject)
            {
                continue;
            }

            var composed = node["and"] ?? node["or"];
            if (composed is { IsArray: true })
            {
                foreach (var item in composed.Items)
                {
                    pending.Push(item);
                }
            }
            else if (node["not"] is { } negated)
            {
                pending.Push(negated);
            }
            else if (node["field"] is { IsString: true } field && FieldPath.Canonicalize(field.StringValue) is { } canonical && IsDatePredicate(node))
            {
                fields.Add(canonical);
            }
        }
    }

    /// <summary>
    ///     Determines whether a field predicate compares its field as dates.
    /// </summary>
    /// <param name="predicate">The field predicate object.</param>
    /// <returns><see langword="true"/> for a <c>timeUnit</c>, or a compared value that is a date-time object (an object that is not an expression reference).</returns>
    private static bool IsDatePredicate(SpecNode predicate)
    {
        if (predicate["timeUnit"] is { IsNull: false })
        {
            return true;
        }

        return new[] { "equal", "lt", "lte", "gt", "gte", "range", "oneOf" }
            .Select(name => predicate[name])
            .OfType<SpecNode>()
            .Any(value => IsDateObject(value) || value.Items.Any(IsDateObject));
    }

    /// <summary>
    ///     Determines whether a value is a date-time object: an object that is not an expression reference.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for an object without an <c>expr</c> text.</returns>
    private static bool IsDateObject(SpecNode value) => value.IsObject && value["expr"] is not { IsString: true };

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
