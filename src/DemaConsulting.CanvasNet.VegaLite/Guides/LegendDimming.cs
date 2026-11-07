// Portions derived from Vega-Lite (BSD-3-Clause): interactive legends (compile/selection/legends.ts, compile/legend/encode.ts); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     The dimming of the entries of a legend that a selection is bound to (<c>"bind": "legend"</c>): the entries whose value the selection does not hold are drawn with
///     <c>config.legend.unselectedOpacity</c>.
/// </summary>
/// <remarks>
///     <para>
///         Vega-Lite adds to the symbols and the labels of such a legend the opacity rule
///         <c>[{test: "(!length(data(store)) || (N[field] &amp;&amp; indexof(N[field], datum.value) &gt;= 0))", value: opacity}, {value: unselectedOpacity}]</c> (<c>selectedCondition</c> in
///         <c>legend/encode.ts</c>; the labels use the value 1). In the initial state of a chart the store holds the tuples of the <c>value</c> of the selection, so the rule is evaluated
///         once for each entry: an empty store selects every entry, otherwise an entry is selected when the values the selection holds for the field (the field of the channel definition) contain
///         its value, compared the way JavaScript's <c>indexOf</c> does (strict equality: the number 1 is not the text <c>"1"</c>, a date is not a number of milliseconds). A selection stores the
///         milliseconds of a date as a number, so the value of an entry is what Vega holds in <c>datum.value</c> (<see cref="LegendBuilder"/> converts it): the number of milliseconds for the domain
///         value of a discrete scale over a field of dates, a <c>Date</c> (never held) for a tick of a time scale.
///     </para>
///     <para>
///         The rule reads the field of the channel definition, not the projected field, as Vega-Lite does: a selection projected over a field with a time unit stores its values under the derived field
///         (<c>month_d</c>), which the rule does not read, so such a legend dims every entry while the store has a tuple.
///     </para>
///     <para>
///         A legend is bound when the selection is global (<c>resolve</c>), is bound to the legend, and projects exactly one field that is not the identity of the rows
///         (<c>legendBindings.defined</c>); the projection must be the field of the channel definition or the channel (<c>parseInteractiveLegend</c>). When legends of several channels or layers merge,
///         the encoding of the first one is kept, so only the first member (and its layer) decides. A symbol legend that also explains the <c>opacity</c> channel is not dimmed
///         (Vega-Lite removes the symbol opacity rule when the legend encodes opacity itself); its labels are.
///     </para>
///     <para>The class is immutable and safe for concurrent use.</para>
/// </remarks>
internal sealed class LegendDimming
{
    /// <summary>The default of <c>config.legend.unselectedOpacity</c>.</summary>
    private const double DefaultUnselectedOpacity = 0.35;

    /// <summary>The selections that bind the legend, in the order of their declaration.</summary>
    private readonly IReadOnlyList<Bound> _selections;

    /// <summary>
    ///     Initializes the dimming.
    /// </summary>
    /// <param name="selections">The selections that bind the legend.</param>
    /// <param name="unselectedOpacity">The opacity of the entries that are not selected.</param>
    /// <param name="dimsSymbols">Whether the symbols are dimmed (a symbol legend that does not explain the <c>opacity</c> channel).</param>
    private LegendDimming(IReadOnlyList<Bound> selections, double unselectedOpacity, bool dimsSymbols)
    {
        _selections = selections;
        UnselectedOpacity = unselectedOpacity;
        DimsSymbols = dimsSymbols;
    }

    /// <summary>Gets the opacity of the entries that the selection does not hold (<c>config.legend.unselectedOpacity</c>).</summary>
    public double UnselectedOpacity { get; }

    /// <summary>Gets a value indicating whether the symbols of the entries are dimmed (the labels always are).</summary>
    public bool DimsSymbols { get; }

    /// <summary>
    ///     Finds the selections that a legend is bound to.
    /// </summary>
    /// <param name="model">The selections of the specification, bound to the layer tree; null when there are none.</param>
    /// <param name="unit">The unit view whose legend it is (the first layer of the legend), or null.</param>
    /// <param name="members">The channels the legend explains, canonical channel first.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The dimming; null when no selection is bound to the legend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> or <paramref name="config"/> is null.</exception>
    public static LegendDimming? Create(SelectionModel? model, LayerNode? unit, IReadOnlyList<LegendMember> members, ResolvedConfig config)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(config);
        if (model is null || !model.HasSelections || unit is null || members.Count == 0)
        {
            return null;
        }

        // The encoding of the first channel is the one that is kept when the legends of several channels merge
        var first = members[0];
        var bound = new List<Bound>();
        foreach (var component in model.ComponentsOf(unit))
        {
            if (!IsBound(component, first))
            {
                continue;
            }

            // The rule reads the field of the channel definition (not the projected field) and only exists for a field definition
            if (first.Definition["field"] is not { IsString: true } fieldNode)
            {
                continue;
            }

            var store = model.Stores.GetValueOrDefault(component.VariableName + "_store");
            var resolved = model.Values.GetValueOrDefault(component.VariableName);
            DataValue? held = null;
            if (resolved.IsObject && resolved.ObjectValue!.TryGetValue(fieldNode.StringValue!, out var member))
            {
                held = member;
            }

            bound.Add(new Bound(store is null || store.Entries.Count == 0, held));
        }

        if (bound.Count == 0)
        {
            return null;
        }

        var unselected = config.GetNumber("legend.unselectedOpacity") ?? DefaultUnselectedOpacity;
        return new LegendDimming(bound, unselected, !members.Any(m => m.Channel == "opacity"));
    }

    /// <summary>
    ///     Tells whether a selection binds the legend of a channel (Vega-Lite's <c>legendBindings.defined</c> and <c>parseInteractiveLegend</c>).
    /// </summary>
    /// <param name="component">The selection of the unit.</param>
    /// <param name="member">The first channel of the legend.</param>
    /// <returns><see langword="true"/> when the selection is bound to the legend and projects its field or channel.</returns>
    internal static bool IsBound(SelectionComponent component, LegendMember member)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(member);
        if (component.IsDropped || component.Bind != SelectionBind.Legend || component.Resolve != "global" || component.Projection.Count != 1)
        {
            return false;
        }

        var item = component.Projection[0];
        if (item.Field == SelectionTestSemantics.SelectionId)
        {
            return false;
        }

        // hasField[field] ?? hasChannel[channel]
        var field = member.Definition["field"] is { IsString: true } node ? node.StringValue : null;
        return (field is not null && item.Field == field) || item.Channel == member.Channel;
    }

    /// <summary>
    ///     Tells whether an entry is selected: the test of Vega-Lite's <c>selectedCondition</c> for the value of the entry.
    /// </summary>
    /// <param name="value">The value of the entry (<c>datum.value</c>).</param>
    /// <param name="context">The evaluation that the comparisons are charged to.</param>
    /// <returns><see langword="true"/> when the entry is drawn undimmed.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    public bool IsSelected(DataValue value, ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var selection in _selections)
        {
            if (selection.IsEmpty)
            {
                return true;
            }

            if (selection.Held is { IsArray: true } held)
            {
                context.ChargeSteps(Math.Max(1, held.Elements.Length));
                for (var i = 0; i < held.Elements.Length; i++)
                {
                    if (ExpressionEvaluator.StrictEquals(held.Elements[i], value))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     One selection that binds the legend.
    /// </summary>
    /// <param name="IsEmpty">Whether its store has no tuple (<c>!length(data(store))</c>).</param>
    /// <param name="Held">What the selection holds for the field (<c>N[field]</c>), or null when it holds nothing for it.</param>
    private sealed record Bound(bool IsEmpty, DataValue? Held);
}
