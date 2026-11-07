// Portions derived from Vega-Lite (BSD-3-Clause): the selection components and their assembly (compile/selection/*); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     How the user could drive a selection: what its <c>bind</c> says. A static chart ignores the interaction; the model keeps the kind because a later stage reads it.
/// </summary>
internal enum SelectionBind
{
    /// <summary>The selection has no <c>bind</c>.</summary>
    None,

    /// <summary>The selection is populated by input widgets (<c>bind</c> is a Vega binding, or a map of bindings).</summary>
    Widget,

    /// <summary>The selection is populated by the legend (<c>"bind": "legend"</c>).</summary>
    Legend,

    /// <summary>The interval selection is bound to the scales of the view (<c>"bind": "scales"</c>).</summary>
    Scales,
}

/// <summary>
///     One selection as one view declares it: its projection, the tuples of its initial state and the unit that holds them.
/// </summary>
/// <remarks>
///     A top-level selection of a concatenation, a facet or a layer is declared once for every unit view it applies to; each declaration is a component of its own unit
///     (the projection depends on the encoding of the unit). The store of the selection is the one of the first component of that name in document order, as in Vega-Lite.
/// </remarks>
internal sealed class SelectionComponent
{
    /// <summary>Gets the name of the parameter as written.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the name of the parameter as a variable (Vega-Lite's <c>varName</c>): the name of its signal; its store is this followed by <c>_store</c>.</summary>
    public required string VariableName { get; init; }

    /// <summary>Gets the type of the selection: <c>point</c> or <c>interval</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Gets how the tuples of the units are combined: <c>global</c>, <c>union</c> or <c>intersect</c>.</summary>
    public required string Resolve { get; init; }

    /// <summary>Gets the node that declares the selection.</summary>
    public required SpecNode Declaration { get; init; }

    /// <summary>Gets the position of the unit view that holds the component in the units of the layer tree (document order).</summary>
    public required int UnitIndex { get; init; }

    /// <summary>Gets the name of the unit that holds the component: the <c>name</c> of the view, otherwise a name made from the position of the unit in the tree (informative: the stores identify tuples by it).</summary>
    public required string UnitName { get; init; }

    /// <summary>Gets the fields the selection constrains.</summary>
    public required IReadOnlyList<SelectionTupleField> Projection { get; init; }

    /// <summary>
    ///     Gets the time units that derive the projected fields that have one (Vega-Lite's <c>project.timeUnit</c> node): a table that a predicate on the selection tests needs these fields,
    ///     which the encoding of the unit that declares the selection writes itself.
    /// </summary>
    public IReadOnlyList<TimeUnitRequest> TimeUnits { get; init; } = [];

    /// <summary>Gets the tuples of the initial state: one list of values for each initial value, with one value for each field of <see cref="Projection"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<DataValue>> Init { get; init; }

    /// <summary>Gets a value indicating whether the projection is over the identity of the rows (<c>_vgsid_</c>), which the renderer does not model.</summary>
    public bool UsesRowIdentity => Projection.Any(p => p.Field == SelectionTestSemantics.SelectionId);

    /// <summary>Gets what the <c>bind</c> of the selection says.</summary>
    public SelectionBind Bind { get; init; }

    /// <summary>Gets a value indicating whether the unit is the whole view or a layer of a layered view that is the whole view (see <see cref="SelectionModel"/>).</summary>
    public bool IsTopLevelView { get; init; }

    /// <summary>
    ///     Gets the <c>mark</c> of the brush of an interval selection: the <c>mark</c> of the default configuration of the type (<c>config.selection.interval.mark</c>) with the <c>mark</c> of the
    ///     selection over it, property by property (Vega-Lite's <c>{...cfg.mark, ...def.mark}</c>); null for a point selection.
    /// </summary>
    public ConfigNode? Mark { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the selection is bound to the scales of its view (<c>"bind": "scales"</c> on an interval selection that is resolved globally; Vega-Lite's
    ///     <c>scales.defined</c>): it zooms the scales instead of drawing a brush.
    /// </summary>
    public bool IsScaleBound => Type == "interval" && Resolve == "global" && Bind == SelectionBind.Scales;

    /// <summary>Gets a value indicating whether the component is dropped (applied as an empty selection) because Vega rejects what Vega-Lite compiles for it.</summary>
    public bool IsDropped { get; init; }
}

/// <summary>
///     The selections of a specification in their initial state: the stores of the tuples that the selection functions of the expression engine read, and the value of each selection
///     parameter (what Vega-Lite's signal of the parameter holds).
/// </summary>
/// <remarks>
///     <para>
///         A static chart never receives an event, so a selection is what its <c>value</c> says (or what the caller put in <see cref="VegaLiteRenderOptions.Parameters"/>): no
///         <c>value</c> is an empty selection, in which a predicate with the default <c>empty</c> selects everything. The model is built once per call from the layer tree, before the
///         parameters are evaluated and before any data is read, so every later stage sees the same stores. It declares a selection for every unit view it applies to
///         (<see cref="SelectionDeclarations.Assign"/>), projects it over the fields or encodings the
///         way Vega-Lite does (<c>project.ts</c>: explicit <c>fields</c> and <c>encodings</c>, else the keys of the initial value, else the defaults of the selection type), reads the initial
///         value into tuples, and builds the store of the first declaration of each name. A selection that Vega rejects (a top-level selection of a layer with two or more
///         units) or that the renderer does not model (a projection over the identity of the rows with an initial value) is applied as an empty selection and reported.
///     </para>
///     <para>The model is immutable once created and is safe to share between threads; creating it does not read any data.</para>
/// </remarks>
internal sealed partial class SelectionModel
{
    /// <summary>The model of a specification that declares no selection.</summary>
    public static readonly SelectionModel Empty = new([], new Dictionary<string, SelectionStore>(StringComparer.Ordinal), new Dictionary<string, DataValue>(StringComparer.Ordinal), []);

    /// <summary>The components by variable name; the first of each name.</summary>
    private readonly Dictionary<string, SelectionComponent> _byName;

    /// <summary>The position of each unit view of the layer tree that is drawn (by the node of its template) among the units of the layer tree the components were declared for; null when the model is not bound to a layer tree.</summary>
    private readonly Dictionary<LayerNode, int>? _unitIndexes;

    /// <summary>
    ///     Initializes a model.
    /// </summary>
    /// <param name="components">Every component, in document order.</param>
    /// <param name="stores">The stores by data set name.</param>
    /// <param name="values">The value of each selection parameter by variable name.</param>
    /// <param name="warnings">The warnings of the projection and of the initial values.</param>
    /// <param name="unitIndexes">The positions of the units of the layer tree that the model is bound to, or null.</param>
    private SelectionModel(
        IReadOnlyList<SelectionComponent> components,
        IReadOnlyDictionary<string, SelectionStore> stores,
        IReadOnlyDictionary<string, DataValue> values,
        IReadOnlyList<FeatureReport> warnings,
        Dictionary<LayerNode, int>? unitIndexes = null)
    {
        Components = components;
        Stores = stores;
        Values = values;
        Warnings = warnings;
        _unitIndexes = unitIndexes;
        _byName = new Dictionary<string, SelectionComponent>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            _byName.TryAdd(component.VariableName, component);
        }
    }

    /// <summary>Gets every component, in document order: one for each unit that a selection applies to.</summary>
    public IReadOnlyList<SelectionComponent> Components { get; }

    /// <summary>Gets the stores by data set name (<c>brush_store</c>): what <c>data("brush_store")</c> and the selection functions read.</summary>
    public IReadOnlyDictionary<string, SelectionStore> Stores { get; }

    /// <summary>Gets the value of each selection parameter by its variable name: the resolved selection (an object with the selected values of each field), the value of the signal that Vega-Lite compiles.</summary>
    public IReadOnlyDictionary<string, DataValue> Values { get; }

    /// <summary>Gets the warnings of the projection and of the initial values (<c>vl-param-selection-projection</c>, <c>vl-param-selection-id</c>), located at the declaration.</summary>
    public IReadOnlyList<FeatureReport> Warnings { get; }

    /// <summary>Gets a value indicating whether the specification declares a selection.</summary>
    public bool HasSelections => Components.Count > 0;

    /// <summary>
    ///     Finds the component that the store of a selection comes from: the first declaration of the name.
    /// </summary>
    /// <param name="variableName">The name of the parameter as a variable.</param>
    /// <returns>The component; null when no selection has the name.</returns>
    public SelectionComponent? Find(string variableName) => _byName.GetValueOrDefault(variableName);

    /// <summary>
    ///     Tells whether a specification declares a selection anywhere (a parameter with <c>select</c>, or a legacy <c>selection</c>).
    /// </summary>
    /// <remarks>The scan reads the view objects only, never the data, so a specification without a selection pays nothing for the model.</remarks>
    /// <param name="tree">The specification.</param>
    /// <returns><see langword="true"/> when a selection is declared.</returns>
    public static bool Declares(SpecTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.EnumerateViews().Any(view => SelectionDeclarations.DeclarationsOf(view).Any());
    }
}
