// Portions derived from Vega-Lite (BSD-3-Clause): the time unit node that a selection predicate inserts (compile/selection/parse.ts parseSelectionPredicate); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Computes the time units that the selections tested by a predicate project over, before the predicate is tested.
/// </summary>
/// <remarks>
///     <para>
///         A selection that is projected over a field with a <c>timeUnit</c> constrains the derived field (<c>month_date</c>), which only the encoding that declares the selection writes. Vega-Lite
///         inserts a time unit node above every filter and above the data of every unit whose conditions test such a selection (<c>parseSelectionPredicate</c>), so that the field is there for the
///         test. This does the same for a <c>filter</c> and for the conditions of a unit: the start and the end of the unit are added to the table when a predicate names a selection with a time
///         unit (a field of that name that the table has is replaced, as the explicit <c>timeUnit</c> transform does, so the encoding's own time unit finds the same values).
///     </para>
///     <para>The recursion follows the nesting of a predicate, which the specification parser bounded by the JSON depth limit. The class is static and pure.</para>
/// </remarks>
internal static class SelectionTimeUnits
{
    /// <summary>
    ///     Adds the time units of the selections that some predicates test to a table.
    /// </summary>
    /// <param name="table">The table the predicates test.</param>
    /// <param name="predicates">The predicate objects: a filter's, or the conditions of the encoding of a unit.</param>
    /// <param name="context">The context of the call: its selections and the view whose selections they are.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <returns>The table with the fields added; <paramref name="table"/> itself when no predicate names a selection with a time unit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    public static DataTable Apply(DataTable table, IEnumerable<SpecNode> predicates, TransformContext context, VegaLiteLocation location)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(predicates);
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Selections.HasSelections)
        {
            return table;
        }

        var requests = new List<TimeUnitRequest>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var predicate in predicates)
        {
            Collect(predicate, context, requests, seen);
        }

        return requests.Count == 0 ? table : TimeUnitTransform.Apply(table, requests, context, location);
    }

    /// <summary>
    ///     Lists the predicates that the conditions of the encoding of a unit hold: a condition on a parameter (or a legacy selection) is itself one, and the <c>test</c> of a condition is one when it is an object.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <returns>The predicate objects.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> is null.</exception>
    public static IEnumerable<SpecNode> ConditionsOf(SpecNode unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return ReadConditions(unit);
    }

    /// <summary>
    ///     Reads the predicates of the conditions of a unit lazily.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <returns>The predicate objects.</returns>
    private static IEnumerable<SpecNode> ReadConditions(SpecNode unit)
    {
        if (unit["encoding"] is not { IsObject: true } encoding)
        {
            yield break;
        }

        foreach (var channel in encoding.Properties.Where(p => p.IsObject))
        {
            if (channel["condition"] is not { } condition)
            {
                continue;
            }

            foreach (var entry in condition.IsArray ? condition.Items : [condition])
            {
                if (!entry.IsObject)
                {
                    continue;
                }

                yield return entry;
                if (entry["test"] is { IsObject: true } test)
                {
                    yield return test;
                }
            }
        }
    }

    /// <summary>
    ///     Collects the time units of the selections a predicate names.
    /// </summary>
    /// <param name="node">The predicate.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="requests">Receives the requests.</param>
    /// <param name="seen">The start names that have a request already.</param>
    private static void Collect(SpecNode node, TransformContext context, List<TimeUnitRequest> requests, HashSet<string> seen)
    {
        if (!node.IsObject)
        {
            return;
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (node[composition] is { IsArray: true } items)
            {
                foreach (var item in items.Items)
                {
                    Collect(item, context, requests, seen);
                }
            }
        }

        if (node["not"] is { } negated)
        {
            Collect(negated, context, requests, seen);
        }

        if (node["param"] is { IsString: true } parameter)
        {
            Add(parameter.StringValue!, context, requests, seen);
        }

        if (node["selection"] is { } legacy)
        {
            CollectLegacy(legacy, context, requests, seen);
        }
    }

    /// <summary>
    ///     Collects the time units of the selections that a legacy <c>selection</c> predicate names: a name, or a composition of names.
    /// </summary>
    /// <param name="node">The value of <c>selection</c>.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="requests">Receives the requests.</param>
    /// <param name="seen">The start names that have a request already.</param>
    private static void CollectLegacy(SpecNode node, TransformContext context, List<TimeUnitRequest> requests, HashSet<string> seen)
    {
        if (node.IsString)
        {
            Add(node.StringValue!, context, requests, seen);
            return;
        }

        if (!node.IsObject)
        {
            return;
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (node[composition] is { IsArray: true } items)
            {
                foreach (var item in items.Items)
                {
                    CollectLegacy(item, context, requests, seen);
                }
            }
        }

        if (node["not"] is { } negated)
        {
            CollectLegacy(negated, context, requests, seen);
        }
    }

    /// <summary>
    ///     Adds the time units of the selection that a name refers to, as the view of the context sees it.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="requests">Receives the requests.</param>
    /// <param name="seen">The start names that have a request already.</param>
    private static void Add(string name, TransformContext context, List<TimeUnitRequest> requests, HashSet<string> seen)
    {
        if (context.Selections.FindComponent(context.View, SelectionDeclarations.VarName(name)) is not { } component)
        {
            return;
        }

        foreach (var request in component.TimeUnits.Where(r => seen.Add(r.StartName)))
        {
            requests.Add(request);
        }
    }
}
