namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     How the scale, axis or legend of a channel is shared between the layers of a layered view (Vega-Lite's <c>resolve</c>).
/// </summary>
internal enum ResolveMode
{
    /// <summary>One scale, axis or legend for all the layers; the default for scales.</summary>
    Shared,

    /// <summary>One scale, axis or legend for each layer.</summary>
    Independent,
}

/// <summary>
///     The kind of a view of the view tree (<see cref="LayerNode.Kind"/>).
/// </summary>
internal enum ViewKind
{
    /// <summary>A unit view (a leaf): a mark with an encoding.</summary>
    Unit,

    /// <summary>A layered view (<c>layer</c>), or the layers a <c>repeat</c> with a <c>layer</c> list stands for.</summary>
    Layer,

    /// <summary>A composite mark (<c>boxplot</c>, ...) that was replaced by the layers added below it.</summary>
    Expansion,

    /// <summary>A concatenation (<c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, or a <c>repeat</c> over an array, rows or columns).</summary>
    Concat,

    /// <summary>A facet (<c>facet</c> with a <c>spec</c>, or a unit view with a <c>row</c>, <c>column</c> or <c>facet</c> encoding channel).</summary>
    Facet,
}

/// <summary>
///     The <c>resolve</c> of a view that holds other views: for each channel, whether its scale, axis and legend are shared by the views or independent.
/// </summary>
/// <remarks>
///     <para>
///         The default of a channel that <c>resolve</c> does not name depends on the kind of view (Vega-Lite's <c>defaultScaleResolve</c>): the layers of a layered view
///         and the cells of a facet share every scale (a facet keeps <c>theta</c> independent), the children of a concatenation keep the scales of the position channels
///         (<c>x</c>, <c>y</c>, <c>xOffset</c>, <c>yOffset</c>) and of <c>theta</c> and <c>radius</c> independent and share the others (so a legend of a
///         <c>color</c> is merged). As in Vega-Lite (<c>parseGuideResolve</c>), the axis or legend of a channel whose scale is
///         independent is independent as well (one guide cannot describe two scales), and an axis or legend that is not named follows the scale. The children of a
///         concatenation never share an axis (<c>CONCAT_CANNOT_SHARE_AXIS</c>): <c>resolve.axis</c> is ignored for them.
///     </para>
///     <para>The object is immutable and safe for concurrent use.</para>
/// </remarks>
internal sealed class LayerResolve
{
    /// <summary>The channels whose scales the children of a concatenation keep independent by default.</summary>
    private static readonly HashSet<string> ConcatIndependentChannels = new(["x", "y", "xOffset", "yOffset", "theta", "radius"], StringComparer.Ordinal);

    /// <summary>The kind of view the resolve belongs to.</summary>
    private readonly ViewKind _kind;

    /// <summary>The explicit modes of the scales by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _scale;

    /// <summary>The explicit modes of the axes by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _axis;

    /// <summary>The explicit modes of the legends by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _legend;

    /// <summary>
    ///     Initializes the resolve.
    /// </summary>
    /// <param name="kind">The kind of view the resolve belongs to.</param>
    /// <param name="scale">The explicit scale modes.</param>
    /// <param name="axis">The explicit axis modes.</param>
    /// <param name="legend">The explicit legend modes.</param>
    private LayerResolve(ViewKind kind, Dictionary<string, ResolveMode> scale, Dictionary<string, ResolveMode> axis, Dictionary<string, ResolveMode> legend)
    {
        _kind = kind;
        _scale = scale;
        _axis = axis;
        _legend = legend;
    }

    /// <summary>Gets the resolve of a layered view that has none: everything is shared.</summary>
    public static LayerResolve Default { get; } = new(ViewKind.Layer, [], [], []);

    /// <summary>Gets the resolve of a concatenation that has none: the position scales are independent and the other scales are shared.</summary>
    public static LayerResolve ConcatDefault { get; } = new(ViewKind.Concat, [], [], []);

    /// <summary>Gets the resolve of a facet that has none: everything is shared except the <c>theta</c> scale.</summary>
    public static LayerResolve FacetDefault { get; } = new(ViewKind.Facet, [], [], []);

    /// <summary>
    ///     Reads the <c>resolve</c> object of a view; the parser has validated its shape.
    /// </summary>
    /// <param name="resolve">The <c>resolve</c> value, or null.</param>
    /// <param name="kind">The kind of view: <see cref="ViewKind.Concat"/> or <see cref="ViewKind.Facet"/> for a composition, otherwise a layered view.</param>
    /// <returns>The resolve; the default of the kind when there is none.</returns>
    public static LayerResolve Read(SpecNode? resolve, ViewKind kind = ViewKind.Layer)
    {
        if (resolve is not { IsObject: true })
        {
            return kind switch
            {
                ViewKind.Concat => ConcatDefault,
                ViewKind.Facet => FacetDefault,
                _ => Default,
            };
        }

        return new LayerResolve(
            kind is ViewKind.Concat or ViewKind.Facet ? kind : ViewKind.Layer,
            ReadModes(resolve["scale"]),
            ReadModes(resolve["axis"]),
            ReadModes(resolve["legend"]));
    }

    /// <summary>
    ///     Gets how the scale of a channel is resolved.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The mode that <c>resolve.scale</c> says; otherwise the default of the kind of view.</returns>
    public ResolveMode Scale(string channel) =>
        _scale.TryGetValue(channel, out var mode) ? mode : DefaultScale(channel);

    /// <summary>
    ///     Gets how the axis of a position channel is resolved.
    /// </summary>
    /// <param name="channel">The channel (<c>x</c> or <c>y</c>).</param>
    /// <returns>
    ///     Independent for the children of a concatenation; for the others independent when the scale is independent or <c>resolve.axis</c> says so, otherwise shared.
    /// </returns>
    public ResolveMode Axis(string channel) => _kind == ViewKind.Concat ? ResolveMode.Independent : Guide(_axis, channel);

    /// <summary>
    ///     Gets the mode of the scale of a channel that <c>resolve.scale</c> does not name.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The default mode of the kind of view.</returns>
    private ResolveMode DefaultScale(string channel) => _kind switch
    {
        ViewKind.Concat when ConcatIndependentChannels.Contains(channel) => ResolveMode.Independent,
        ViewKind.Facet when channel == "theta" => ResolveMode.Independent,
        _ => ResolveMode.Shared,
    };

    /// <summary>
    ///     Gets how the legend of a channel is resolved.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>Independent when the scale is independent or <c>resolve.legend</c> says so, otherwise shared.</returns>
    public ResolveMode Legend(string channel) => Guide(_legend, channel);

    /// <summary>
    ///     Decides the mode of a guide.
    /// </summary>
    /// <param name="modes">The explicit modes of the guide kind.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The mode.</returns>
    private ResolveMode Guide(Dictionary<string, ResolveMode> modes, string channel) =>
        Scale(channel) == ResolveMode.Independent ? ResolveMode.Independent : modes.GetValueOrDefault(channel, ResolveMode.Shared);

    /// <summary>
    ///     Reads the channel modes of one of <c>scale</c>, <c>axis</c> and <c>legend</c>.
    /// </summary>
    /// <param name="node">The object, or null.</param>
    /// <returns>The modes by channel.</returns>
    private static Dictionary<string, ResolveMode> ReadModes(SpecNode? node)
    {
        var modes = new Dictionary<string, ResolveMode>(StringComparer.Ordinal);
        if (node is { IsObject: true })
        {
            foreach (var entry in node.Properties)
            {
                modes[entry.Name!] = entry.StringValue == "independent" ? ResolveMode.Independent : ResolveMode.Shared;
            }
        }

        return modes;
    }
}

/// <summary>
///     One view of the view tree: a unit view (a leaf), a layered view, a composite mark, a concatenation or a facet.
/// </summary>
/// <remarks>
///     <para>
///         The tree is built over the original <see cref="SpecNode"/>s of the specification; nothing is copied. <see cref="Data"/> and
///         <see cref="Transform"/> are the view's own (the document pipeline applies them down the tree, so a table is computed once for all the views below it), and a
///         leaf's <see cref="Unit"/> is the view the later stages read: its <c>mark</c>, its <c>encoding</c> merged with the
///         encodings it inherits, and the <c>width</c> and <c>height</c> of its <see cref="ChartRoot"/>.
///     </para>
///     <para>
///         A <c>repeat</c> never survives as a node of its own: it is a <see cref="ViewKind.Concat"/> (an array, rows or columns) or a <see cref="ViewKind.Layer"/> (a
///         <c>layer</c> list) whose children are the copies of its <c>spec</c> (each with its <see cref="Repeater"/>). A unit view that has a <c>row</c>, <c>column</c>
///         or <c>facet</c> encoding channel is a <see cref="ViewKind.Facet"/> over one chart (<see cref="IsFacetBody"/>), as in Vega-Lite's normalization.
///     </para>
///     <para>
///         The nodes are immutable once <see cref="SpecNormalizer.Layers"/> returns the tree, with one exception: the data stage of one render or measure call replaces the template of a
///         facet by a copy for each cell (<see cref="LayerTree.InstantiateFacet"/>), because the cells depend on the data. The tree belongs to that call.
///     </para>
/// </remarks>
internal sealed class LayerNode
{
    /// <summary>The children, in document order.</summary>
    private readonly List<LayerNode> _children = [];

    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="source">The view object of the specification.</param>
    /// <param name="parent">The view that holds this one, or null for the root.</param>
    /// <param name="depth">The number of views that hold this view (zero for the root).</param>
    internal LayerNode(SpecNode source, LayerNode? parent, int depth)
    {
        Source = source;
        Parent = parent;
        Depth = depth;
        Resolve = LayerResolve.Default;
        Repeater = parent?.Repeater;
    }

    /// <summary>Gets the view object of the specification this node stands for.</summary>
    public SpecNode Source { get; }

    /// <summary>Gets the view that holds this view, or null for the root.</summary>
    public LayerNode? Parent { get; }

    /// <summary>Gets the number of views that hold this view (zero for the root).</summary>
    public int Depth { get; }

    /// <summary>
    ///     Gets the <c>data</c> object of this view itself (the data it replaces the inherited data with), or null when it inherits. An explicit
    ///     <c>"data": null</c> in a layer inherits too: Vega-Lite's <c>parseRoot</c> only creates a source when <c>model.data || !model.parent</c> holds, so
    ///     for a child view <c>null</c> is as good as absent and only the top-level view's <c>null</c> gives the empty source (the table of an absent or
    ///     <c>null</c> top-level <c>data</c> is empty). The body of a faceted unit view has none: the data belongs to the facet.
    /// </summary>
    public SpecNode? Data => !IsFacetBody && Source["data"] is { IsObject: true } data ? data : null;

    /// <summary>
    ///     Gets the <c>transform</c> array of this view itself, or null. A view's transforms apply to the data before those of the views below it. The body of a
    ///     faceted unit view has none: the transform belongs to the facet.
    /// </summary>
    public SpecNode? Transform => !IsFacetBody && Source["transform"] is { IsArray: true } transform ? transform : null;

    /// <summary>Gets the <c>resolve</c> of a view that holds other views (the default of its kind when it has none); everything shared for a unit view.</summary>
    public LayerResolve Resolve { get; private set; }

    /// <summary>Gets the views a layered view, a concatenation, a facet or a composite mark holds, in document order; empty for a unit view.</summary>
    public IReadOnlyList<LayerNode> Children => _children;

    /// <summary>Gets the kind of the view.</summary>
    public ViewKind Kind { get; private set; } = ViewKind.Unit;

    /// <summary>Gets a value indicating whether the view is layered (a <c>layer</c>, a composite mark that was expanded, or the layers of a <c>repeat</c>).</summary>
    public bool IsLayer => Kind is ViewKind.Layer or ViewKind.Expansion;

    /// <summary>Gets a value indicating whether the view is a concatenation or a facet.</summary>
    public bool IsComposition => Kind is ViewKind.Concat or ViewKind.Facet;

    /// <summary>
    ///     Gets the name of the operator that made the view a concatenation or a facet: <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c> or <c>repeat</c>
    ///     (a concatenation made from a <c>repeat</c>); a unit view with faceting channels is a <c>facet</c>. Null for the other kinds.
    /// </summary>
    public string? Operator { get; private set; }

    /// <summary>
    ///     Gets the layout of a concatenation or a facet (<c>columns</c>, <c>spacing</c>, <c>align</c>, <c>center</c> and <c>bounds</c>, with the configuration applied); null for the other kinds.
    /// </summary>
    public CompositionSettings? Layout { get; private set; }

    /// <summary>Gets what a facet is faceted by; null for the other kinds.</summary>
    public FacetDefinition? Facet { get; private set; }

    /// <summary>
    ///     Gets the values of the <c>repeat</c>s that this view is a copy of (and every view below a copy has the same); null when the view is not inside a repeat.
    /// </summary>
    public RepeaterValues? Repeater { get; private set; }

    /// <summary>
    ///     Gets the chart that this view belongs to: the nearest view, this one or above, that is a unit view, layered view or composite mark and that is the root, a child of a
    ///     concatenation, or the body of a facet. The chart is what has one size, one set of axes and one view background; its <c>width</c>, <c>height</c>, <c>view</c> and
    ///     <c>title</c> are the chart's. Null for a concatenation and a facet.
    /// </summary>
    public LayerNode? ChartRoot { get; private set; }

    /// <summary>Gets a value indicating whether this is the synthetic chart of a unit view that has faceting channels: its mark and encoding (without those channels) are the view's, its data and transform are the facet's.</summary>
    public bool IsFacetBody { get; private set; }

    /// <summary>Gets a value indicating whether a concatenation or a facet holds this view or a view above it.</summary>
    public bool InComposition
    {
        get
        {
            for (var current = Parent; current is not null; current = current.Parent)
            {
                if (current.IsComposition)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    ///     Gets the unit view that the stages after the specification read, for a unit view; null for any other kind.
    /// </summary>
    /// <remarks>
    ///     For a chart that has no encoding to merge and no repeated value to replace this is the view object itself (so the single view, as always, is the root object).
    ///     For a layer it is an overlay of the layer (the same pointer and position) whose
    ///     <c>mark</c> is the layer's own, whose <c>encoding</c> is the merge of the encodings of the layered views above it and its own (a channel of
    ///     the layer replaces the same channel of a view above in place, new channels are added after the inherited ones) and whose <c>width</c> and
    ///     <c>height</c> are those of the chart. The values in the overlay are the original nodes, so a diagnostic about one points at its text.
    /// </remarks>
    public SpecNode? Unit { get; private set; }

    /// <summary>Gets a value indicating whether this is a unit view that is drawn.</summary>
    public bool IsUnit => Unit is not null;

    /// <summary>
    ///     Gets a value indicating whether this view is a composite mark (<c>boxplot</c>, ...) that was replaced by layers (<see cref="SpecNormalizer.Expand"/>).
    ///     Its own <c>encoding</c> and that of the layered views above it were merged into the layers it expanded to, so they do not inherit them again.
    /// </summary>
    public bool IsExpansion => Kind == ViewKind.Expansion;

    /// <summary>Gets a value indicating whether this view was written by an expansion (it is not in the text of the specification) and is not counted as a nesting level.</summary>
    public bool IsSynthetic { get; private set; }

    /// <summary>
    ///     Gets the <c>layer</c> list of a <c>repeat</c> that this node, a child of a repeated row or column, stands for (the node has no view of its own in the
    ///     specification); null for every other node.
    /// </summary>
    internal RepeatLayers? LayerRepeat { get; private set; }

    /// <summary>
    ///     Gets the cell of a facet that this view is a copy of (every node of a cell, the cell's chart and the views below it, has the same value); null for a view that
    ///     is not part of an instantiated facet.
    /// </summary>
    public FacetCellInfo? Cell { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether a facet has been instantiated: its single template child was replaced by one copy for each cell (see <see cref="LayerTree.InstantiateFacet"/>).
    ///     Always false for the other kinds.
    /// </summary>
    public bool HasCells { get; private set; }

    /// <summary>
    ///     Adds a child view.
    /// </summary>
    /// <param name="child">The child.</param>
    internal void AddChild(LayerNode child) => _children.Add(child);

    /// <summary>
    ///     Marks the node as a layered view.
    /// </summary>
    /// <param name="resolve">The resolve of the view.</param>
    internal void MakeLayer(LayerResolve resolve)
    {
        Kind = ViewKind.Layer;
        Resolve = resolve;
    }

    /// <summary>
    ///     Marks the node as a composite mark that is replaced by the layers added below it.
    /// </summary>
    internal void MakeExpansion() => Kind = ViewKind.Expansion;

    /// <summary>
    ///     Marks the node as written by an expansion.
    /// </summary>
    internal void MakeSynthetic() => IsSynthetic = true;

    /// <summary>
    ///     Marks the node as a concatenation.
    /// </summary>
    /// <param name="operatorName">The operator: <c>concat</c>, <c>hconcat</c>, <c>vconcat</c> or <c>repeat</c>.</param>
    /// <param name="resolve">The resolve of the view.</param>
    /// <param name="layout">The layout.</param>
    internal void MakeConcat(string operatorName, LayerResolve resolve, CompositionSettings layout)
    {
        Kind = ViewKind.Concat;
        Operator = operatorName;
        Resolve = resolve;
        Layout = layout;
    }

    /// <summary>
    ///     Marks the node as a facet.
    /// </summary>
    /// <param name="operatorName">The operator: <c>facet</c>.</param>
    /// <param name="resolve">The resolve of the view.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="facet">What the facet is faceted by.</param>
    internal void MakeFacet(string operatorName, LayerResolve resolve, CompositionSettings layout, FacetDefinition facet)
    {
        Kind = ViewKind.Facet;
        Operator = operatorName;
        Resolve = resolve;
        Layout = layout;
        Facet = facet;
    }

    /// <summary>
    ///     Sets the unit view of a leaf.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    internal void MakeUnit(SpecNode unit) => Unit = unit;

    /// <summary>
    ///     Sets the chart of the view.
    /// </summary>
    /// <param name="chart">The chart root.</param>
    internal void SetChartRoot(LayerNode chart) => ChartRoot = chart;

    /// <summary>
    ///     Marks the node as the synthetic chart of a faceted unit view.
    /// </summary>
    internal void MakeFacetBody() => IsFacetBody = true;

    /// <summary>
    ///     Sets the repeated values of the view and of the views below it.
    /// </summary>
    /// <param name="repeater">The values.</param>
    internal void SetRepeater(RepeaterValues? repeater) => Repeater = repeater;

    /// <summary>
    ///     Marks the node as the layers of a <c>repeat</c> over a list of layer values.
    /// </summary>
    /// <param name="layers">The layer values and the <c>spec</c> that is repeated.</param>
    internal void SetLayerRepeat(RepeatLayers layers) => LayerRepeat = layers;

    /// <summary>
    ///     Replaces the template of a facet by the copies of it, one for each cell.
    /// </summary>
    /// <param name="cells">The copies of the facet's template, cell by cell.</param>
    internal void ReplaceChildrenWithCells(IEnumerable<LayerNode> cells)
    {
        _children.Clear();
        _children.AddRange(cells);
        HasCells = true;
    }

    /// <summary>
    ///     Copies this view and the views below it (a facet's template) for one cell of the facet.
    /// </summary>
    /// <remarks>
    ///     The copy has the same view objects, unit views, resolves and layout (nothing of the specification is copied), the given parent and the cell on every node. The copy is made
    ///     with an explicit stack, so a deep template cannot overflow the call stack. A chart root inside the template maps to its copy.
    /// </remarks>
    /// <param name="parent">The view that holds the copy: the facet.</param>
    /// <param name="cell">The cell the copy is for.</param>
    /// <returns>The copy of this view.</returns>
    internal LayerNode CloneFor(LayerNode parent, FacetCellInfo cell)
    {
        var copies = new Dictionary<LayerNode, LayerNode>(ReferenceEqualityComparer.Instance);
        var root = CopyNode(this, parent, cell, copies);
        var pending = new Stack<LayerNode>();
        pending.Push(this);
        while (pending.Count > 0)
        {
            var original = pending.Pop();
            var copy = copies[original];
            foreach (var child in original.Children)
            {
                copy.AddChild(CopyNode(child, copy, cell, copies));
                pending.Push(child);
            }
        }

        return root;
    }

    /// <summary>
    ///     Copies one node without its children.
    /// </summary>
    /// <param name="original">The node.</param>
    /// <param name="parent">The parent of the copy.</param>
    /// <param name="cell">The cell of the copy.</param>
    /// <param name="copies">The copies made so far by original; receives this one.</param>
    /// <returns>The copy.</returns>
    private static LayerNode CopyNode(LayerNode original, LayerNode parent, FacetCellInfo cell, Dictionary<LayerNode, LayerNode> copies)
    {
        var copy = new LayerNode(original.Source, parent, original.Depth)
        {
            Resolve = original.Resolve,
            Kind = original.Kind,
            Operator = original.Operator,
            Layout = original.Layout,
            Facet = original.Facet,
            Repeater = original.Repeater,
            IsFacetBody = original.IsFacetBody,
            Unit = original.Unit,
            IsSynthetic = original.IsSynthetic,
            LayerRepeat = original.LayerRepeat,
            Cell = cell,
            HasCells = original.HasCells,
        };
        if (original.ChartRoot is { } chart)
        {
            // The chart is the node itself or a node above it inside the template, which was copied first
            copy.ChartRoot = ReferenceEquals(chart, original) ? copy : copies.GetValueOrDefault(chart) ?? chart;
        }

        copies[original] = copy;
        return copy;
    }
}

/// <summary>
///     The views of a specification as a tree whose leaves are the unit views that are drawn: see <see cref="SpecNormalizer.Layers"/>.
/// </summary>
internal sealed class LayerTree
{
    /// <summary>
    ///     Initializes the tree.
    /// </summary>
    /// <param name="root">The root view.</param>
    /// <param name="units">The unit views in document order.</param>
    /// <param name="compositions">The concatenations and facets, in document order.</param>
    /// <param name="viewCount">The number of views.</param>
    /// <param name="warnings">The warnings of the normalization.</param>
    internal LayerTree(LayerNode root, IReadOnlyList<LayerNode> units, IReadOnlyList<LayerNode> compositions, int viewCount, IReadOnlyList<FeatureReport> warnings)
    {
        Root = root;
        Units = units;
        Compositions = compositions;
        ViewCount = viewCount;
        Warnings = warnings;
    }

    /// <summary>Gets the root view.</summary>
    public LayerNode Root { get; }

    /// <summary>Gets the unit views (the leaves), in document order. A specification without composition has exactly one, the root.</summary>
    public IReadOnlyList<LayerNode> Units { get; private set; }

    /// <summary>Gets the concatenations and facets of the specification (a <c>repeat</c> is a concatenation), in document order.</summary>
    public IReadOnlyList<LayerNode> Compositions { get; private set; }

    /// <summary>Gets the number of views in the tree (every node: unit views, layered views, concatenations, facets and the copies of a repeat or of a facet's cells).</summary>
    public int ViewCount { get; private set; }

    /// <summary>
    ///     Gets the warnings that normalizing the views produced, in the order they were found (<c>vl-composition-columns-ignored</c>, <c>vl-concat-axis-shared</c>, <c>vl-repeat-ref-unresolved</c>).
    /// </summary>
    public IReadOnlyList<FeatureReport> Warnings { get; }

    /// <summary>
    ///     Gets a value indicating whether the specification has a concatenation, a facet or a repeat (the views of a <c>repeat</c> of layers are layered views, but they come from a repeat).
    /// </summary>
    public bool HasComposition => Compositions.Count > 0 || Units.Any(u => u.Repeater is not null);

    /// <summary>
    ///     Recomputes <see cref="Units"/>, <see cref="Compositions"/> and <see cref="ViewCount"/> from the nodes of the tree, in document order.
    /// </summary>
    /// <remarks>
    ///     Called after the shape of the tree changed (<see cref="InstantiateFacet"/>). The walk uses an explicit stack and visits every node once.
    /// </remarks>
    public void Refresh()
    {
        var units = new List<LayerNode>();
        var compositions = new List<LayerNode>();
        var count = 0;
        var pending = new Stack<LayerNode>();
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            count++;
            if (node.IsUnit)
            {
                units.Add(node);
            }

            if (node.IsComposition)
            {
                compositions.Add(node);
            }

            // Pushed in reverse so that the children are visited in document order
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                pending.Push(node.Children[i]);
            }
        }

        Units = units;
        Compositions = compositions;
        ViewCount = count;
    }

    /// <summary>
    ///     Counts the views that one cell of a facet holds and the views of the tree that are not part of the facet's cells.
    /// </summary>
    /// <remarks>
    ///     A facet that has not been instantiated holds one template (a unit view, or a layered view and its layers): that is what each cell will hold. For an
    ///     instantiated facet the views of all its cells are counted as part of the facet, so <c>Outside</c> is the number of the views of the rest of the tree
    ///     plus the facet itself, and <c>PerCell</c> the views of its first cell (zero when it has no cell). The caller checks <see cref="VegaLiteLimits.MaxViews"/> with these
    ///     numbers before it creates the cells (<see cref="Transforms.EncodingTransforms"/>).
    /// </remarks>
    /// <param name="facet">The facet.</param>
    /// <returns>The views of one cell, and the views that are not in the cells.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="facet"/> is not a facet.</exception>
    public (int PerCell, int Outside) FacetViews(LayerNode facet)
    {
        ArgumentNullException.ThrowIfNull(facet);
        if (facet.Kind != ViewKind.Facet)
        {
            throw new ArgumentException("The view is not a facet.", nameof(facet));
        }

        var perCell = facet.Children.Count == 0 ? 0 : CountViews(facet.Children[0]);
        var inCells = facet.Children.Sum(CountViews);
        return (perCell, ViewCount - inCells);
    }

    /// <summary>
    ///     Replaces the template of a facet by a copy of it for each cell, and refreshes the tree.
    /// </summary>
    /// <remarks>
    ///     The tree belongs to one render or measure call, so the replacement in place is not shared with another thread. The caller has checked the number of views
    ///     against <see cref="VegaLiteLimits.MaxViews"/> (<see cref="FacetViews"/>). Each copy holds the same view objects as the template and the cell on every node;
    ///     a facet whose template has its own <c>data</c> is instantiated the same way (the cells then share that data).
    /// </remarks>
    /// <param name="facet">The facet, not yet instantiated.</param>
    /// <param name="cells">The cells, in the order they are laid out.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="facet"/> is not a facet.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the facet was instantiated already.</exception>
    public void InstantiateFacet(LayerNode facet, IReadOnlyList<FacetCellInfo> cells)
    {
        ArgumentNullException.ThrowIfNull(facet);
        ArgumentNullException.ThrowIfNull(cells);
        if (facet.Kind != ViewKind.Facet)
        {
            throw new ArgumentException("The view is not a facet.", nameof(facet));
        }

        if (facet.HasCells)
        {
            throw new InvalidOperationException("The facet has its cells already.");
        }

        var templates = facet.Children.ToList();
        var copies = new List<LayerNode>(cells.Count * templates.Count);
        foreach (var cell in cells)
        {
            foreach (var template in templates)
            {
                copies.Add(template.CloneFor(facet, cell));
            }
        }

        facet.ReplaceChildrenWithCells(copies);
        Refresh();
    }

    /// <summary>
    ///     Groups the views of the tree by the <c>data</c> they load, so that the data stage loads a table once for all the views that use it (the copies of a
    ///     <c>repeat</c>, the cells of a facet).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A site is the <c>data</c> object of the specification (by identity: copies of one view have the same object) of the views that declare it, or the root's absent
    ///         data (the first site, always present). The views that inherit the table of a site are the declaring views and the views below them up to the next <c>data</c>;
    ///         <see cref="DataSite.Views"/> lists their view objects and merged unit views, and <see cref="DataSite.Facets"/> their facets, which is what the loader needs to know the fields
    ///         that are read as dates. Sites are in order of the first view that declares them.
    ///     </para>
    ///     <para>The walk uses an explicit stack and visits every view once.</para>
    /// </remarks>
    /// <param name="needed">The views that the data stage visits, or null for all of them.</param>
    /// <returns>The sites.</returns>
    public IReadOnlyList<DataSite> DataSites(IReadOnlySet<LayerNode>? needed = null)
    {
        var sites = new List<DataSiteBuilder>();
        var byData = new Dictionary<SpecNode, DataSiteBuilder>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(LayerNode Node, DataSiteBuilder? Site)>();
        pending.Push((Root, null));
        while (pending.Count > 0)
        {
            var (node, inherited) = pending.Pop();
            var site = inherited;
            if (ReferenceEquals(node, Root) || node.Data is not null)
            {
                var data = node.Data;
                if (data is null)
                {
                    // The root without data (or with data that is not an object): the site of the absent data
                    site = sites.FirstOrDefault(s => s.Data is null);
                    if (site is null)
                    {
                        site = new DataSiteBuilder(null);
                        sites.Add(site);
                    }
                }
                else if (!byData.TryGetValue(data, out site))
                {
                    site = new DataSiteBuilder(data);
                    byData[data] = site;
                    sites.Add(site);
                }

                site.Nodes.Add(node);
            }

            site!.AddView(node);
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if (needed is null || needed.Contains(child))
                {
                    pending.Push((child, site));
                }
            }
        }

        return [.. sites.Select(s => s.Build())];
    }

    /// <summary>
    ///     Counts a view and the views below it.
    /// </summary>
    /// <param name="node">The view.</param>
    /// <returns>The number of views.</returns>
    private static int CountViews(LayerNode node)
    {
        var count = 0;
        var pending = new Stack<LayerNode>();
        pending.Push(node);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            count++;
            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }

        return count;
    }

    /// <summary>
    ///     Collects the views of one <see cref="DataSite"/>.
    /// </summary>
    private sealed class DataSiteBuilder
    {
        /// <summary>The distinct view objects found.</summary>
        private readonly HashSet<SpecNode> _seen = new(ReferenceEqualityComparer.Instance);

        /// <summary>The view objects, in the order found.</summary>
        private readonly List<SpecNode> _views = [];

        /// <summary>The facets, in the order found.</summary>
        private readonly List<FacetDefinition> _facets = [];

        /// <summary>
        ///     Initializes the builder.
        /// </summary>
        /// <param name="data">The <c>data</c> object, or null.</param>
        public DataSiteBuilder(SpecNode? data) => Data = data;

        /// <summary>Gets the <c>data</c> object, or null for the absent data of the root.</summary>
        public SpecNode? Data { get; }

        /// <summary>Gets the views that declare the data.</summary>
        public List<LayerNode> Nodes { get; } = [];

        /// <summary>
        ///     Adds the view objects of a node that uses the table of the site.
        /// </summary>
        /// <param name="node">The node.</param>
        public void AddView(LayerNode node)
        {
            if (_seen.Add(node.Source))
            {
                _views.Add(node.Source);
            }

            if (node.Unit is { } unit && _seen.Add(unit))
            {
                _views.Add(unit);
            }

            if (node.Facet is { } facet && !_facets.Any(f => ReferenceEquals(f, facet)))
            {
                _facets.Add(facet);
            }
        }

        /// <summary>
        ///     Builds the site.
        /// </summary>
        /// <returns>The site.</returns>
        public DataSite Build() => new(Data, Nodes, _views, _facets);
    }
}

/// <summary>
///     The views that load one <c>data</c> object, and the views that read the table it makes: see <see cref="LayerTree.DataSites"/>.
/// </summary>
/// <param name="Data">The <c>data</c> object by identity, or null for the data the root does not have.</param>
/// <param name="Nodes">The views that declare it (the copies of a repeated or faceted view declare the same object), in document order.</param>
/// <param name="Views">The view objects and merged unit views of the views that read the table: the declaring views and those below them up to the next <c>data</c>.</param>
/// <param name="Facets">The facets among those views.</param>
internal sealed record DataSite(SpecNode? Data, IReadOnlyList<LayerNode> Nodes, IReadOnlyList<SpecNode> Views, IReadOnlyList<FacetDefinition> Facets);

/// <summary>
///     Turns the views of a specification into a tree of views whose leaves are the unit views that are drawn.
/// </summary>
/// <remarks>
///     <para>
///         A specification without composition is one leaf, the root itself, so the single view takes exactly the path it always took. The other views are
///         nodes whose children are the views they hold; the inheritance rules of Vega-Lite apply:
///     </para>
///     <list type="bullet">
///         <item><description><b>data</b>: a view's own <c>data</c> replaces the inherited one (<see cref="LayerNode.Data"/> is the own data; the pipeline passes the table down).</description></item>
///         <item><description><b>transform</b>: a view's <c>transform</c> applies to the data first, then those of the views below it (<see cref="LayerNode.Transform"/> per view).</description></item>
///         <item><description><b>encoding</b>: merged into <see cref="LayerNode.Unit"/> through the layered views of a chart; a layer's channel replaces the inherited channel in place. A concatenation or a facet hands no encoding down.</description></item>
///         <item><description><b>size</b>: the <c>width</c> and <c>height</c> of the chart (<see cref="LayerNode.ChartRoot"/>); those of a layer are ignored.</description></item>
///         <item><description><b>resolve</b>: read per view that holds views (<see cref="LayerNode.Resolve"/>), with the defaults of Vega-Lite for its kind.</description></item>
///     </list>
///     <para>
///         <c>concat</c>, <c>hconcat</c> and <c>vconcat</c> are <see cref="ViewKind.Concat"/> nodes. A <c>facet</c> with a <c>spec</c>, and a unit view with a
///         <c>row</c>, <c>column</c> or <c>facet</c> encoding channel (outside a layer), are <see cref="ViewKind.Facet"/> nodes over one chart. A <c>repeat</c> is
///         expanded without reading any data: a <see cref="ViewKind.Concat"/> (an array, <c>row</c> and <c>column</c>; row-major) or a <see cref="ViewKind.Layer"/>
///         (<c>layer</c>) over one copy of the <c>spec</c> per value, each with its <see cref="LayerNode.Repeater"/>, whose <c>{"repeat": ...}</c> field references the
///         unit views replace.
///     </para>
///     <para>
///         The walk uses an explicit stack, so the nesting cannot overflow the call stack. <see cref="VegaLiteLimits.MaxCompositionDepth"/> bounds the
///         nesting of layers, concatenations, facets and repeats and <see cref="VegaLiteLimits.MaxViews"/> the number of views (a repeat is checked before its
///         copies are made); exceeding either is a <see cref="VegaLiteRenderException"/> at the offending view. The merged views are overlays: the original nodes
///         are never copied or modified.
///     </para>
/// </remarks>
internal static partial class SpecNormalizer
{
    /// <summary>The properties of a layer that the chart above it decides; a layer's own value is ignored.</summary>
    private static readonly string[] SizeProperties = ["width", "height"];

    /// <summary>The operators that make a view a concatenation, a facet or a repeat, in the order they are looked for.</summary>
    private static readonly string[] CompositionOperators = ["concat", "hconcat", "vconcat", "facet", "repeat"];

    /// <summary>The faceting channels of a unit view.</summary>
    private static readonly string[] FacetChannels = ["row", "column", "facet"];

    /// <summary>The properties of a faceted unit view that move into the body of the facet (Vega-Lite's <c>normalizeFacetedUnit</c>).</summary>
    private static readonly string[] FacetBodyProperties = ["mark", "encoding", "width", "height", "view", "projection", "params"];

    /// <summary>
    ///     Builds the view tree of a specification.
    /// </summary>
    /// <remarks>
    ///     A composite mark (<c>boxplot</c>, ...) is left as a unit view with that mark; <see cref="Expand"/> replaces it by the layers it stands for.
    /// </remarks>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="config">The effective configuration (<c>concat</c> and <c>facet</c> spacing and columns); the defaults when null.</param>
    /// <returns>The view tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown, at the offending view, when a <c>facet</c> or a <c>repeat</c> has no <c>spec</c>.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at the offending view, when the views are nested deeper than <see cref="VegaLiteLimits.MaxCompositionDepth"/> or the views are more than <see cref="VegaLiteLimits.MaxViews"/>.</exception>
    public static LayerTree Layers(SpecTree tree, VegaLiteLimits? limits = null, ResolvedConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return Build(tree, limits ?? VegaLiteLimits.Default, null, config ?? ConfigResolver.Defaults);
    }

    /// <summary>
    ///     Walks the views of a specification and builds the view tree, optionally replacing the composite marks by their layers.
    /// </summary>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="effective">The limits to enforce.</param>
    /// <param name="expander">Gives the views that a composite mark stands for (null when the unit view is not a composite mark that is expanded); null to expand nothing.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The view tree.</returns>
    private static LayerTree Build(SpecTree tree, VegaLiteLimits effective, Func<SpecNode, IReadOnlyList<SpecNode>?>? expander, ResolvedConfig config)
    {
        var state = new BuildState(tree, effective, expander, config);
        var root = new LayerNode(tree.Root, null, 0);
        state.Pending.Push(root);
        while (state.Pending.Count > 0)
        {
            var node = state.Pending.Pop();
            if (++state.Count > effective.MaxViews)
            {
                throw new VegaLiteRenderException(
                    $"The specification has more than {effective.MaxViews} views, the limit (MaxViews).",
                    tree.Locate(node.Source));
            }

            Visit(state, node);
        }

        return new LayerTree(root, state.Units, state.Compositions, state.Count, state.Warnings);
    }

    /// <summary>
    ///     Decides what a view is and makes it so: the children of a view that holds views are created and queued.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    private static void Visit(BuildState state, LayerNode node)
    {
        var view = node.Source;
        if (node.LayerRepeat is { } layerRepeat)
        {
            VisitLayerRepeat(state, node, layerRepeat);
            return;
        }

        if (!node.IsFacetBody)
        {
            var composition = FindComposition(view);
            switch (composition)
            {
                case "concat" or "hconcat" or "vconcat":
                    VisitConcat(state, node, composition);
                    return;
                case "facet":
                    VisitFacet(state, node);
                    return;
                case "repeat":
                    VisitRepeat(state, node);
                    return;
                default:
                    break;
            }

            if (view["layer"] is { IsArray: true } layers)
            {
                VisitLayer(state, node, layers);
                return;
            }

            if (IsFacetedUnit(node) && VisitFacetedUnit(state, node))
            {
                return;
            }
        }

        VisitUnit(state, node);
    }

    /// <summary>
    ///     Makes a view a layered view and queues its layers.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <param name="layers">The <c>layer</c> array.</param>
    private static void VisitLayer(BuildState state, LayerNode node, SpecNode layers)
    {
        var depth = EnterContainer(state, node, layers, $"The layers are nested more than {state.Limits.MaxCompositionDepth} levels deep, the limit (MaxCompositionDepth).");
        node.MakeLayer(LayerResolve.Read(node.Source["resolve"]));
        node.SetChartRoot(ChartOf(node));
        AddChildren(state, node, layers.Items.Where(i => i.IsObject).ToList(), depth, node.IsSynthetic);
    }

    /// <summary>
    ///     Makes a view a unit view, or a composite mark that is expanded into layers.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    private static void VisitUnit(BuildState state, LayerNode node)
    {
        node.SetChartRoot(ChartOf(node));
        var unit = CreateUnit(state, node);
        if (state.Expander?.Invoke(unit) is { } expanded)
        {
            // The composite mark is a layered view of the layers it stands for; its own encoding is merged into them already
            node.MakeExpansion();
            AddChildren(state, node, expanded, node.Depth + 1, synthetic: true);
            return;
        }

        node.MakeUnit(unit);
        state.Units.Add(node);
    }

    /// <summary>
    ///     Checks the nesting of a view that holds views.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <param name="location">The node the error is located at.</param>
    /// <param name="message">The message of the error.</param>
    /// <returns>The depth of the views it holds.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the views are nested deeper than <see cref="VegaLiteLimits.MaxCompositionDepth"/>.</exception>
    private static int EnterContainer(BuildState state, LayerNode node, SpecNode location, string message)
    {
        var depth = node.Depth + 1;
        if (!node.IsSynthetic && depth > state.Limits.MaxCompositionDepth)
        {
            throw new VegaLiteRenderException(message, state.Tree.Locate(location));
        }

        return depth;
    }

    /// <summary>
    ///     Gets the chart that a view belongs to.
    /// </summary>
    /// <param name="node">The view, which is not a concatenation or a facet.</param>
    /// <returns>The view itself when it is the root or a child of a concatenation or a facet; otherwise the chart of its parent.</returns>
    private static LayerNode ChartOf(LayerNode node) =>
        node.Parent is null || node.Parent.IsComposition ? node : node.Parent.ChartRoot!;

    /// <summary>
    ///     Adds the views a view holds as children and queues them.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view that holds them.</param>
    /// <param name="views">The view objects, in document order.</param>
    /// <param name="depth">The depth of the children.</param>
    /// <param name="synthetic">Whether the children were written by an expansion.</param>
    private static void AddChildren(BuildState state, LayerNode node, IReadOnlyList<SpecNode> views, int depth, bool synthetic)
    {
        foreach (var item in views)
        {
            var child = new LayerNode(item, node, depth);
            if (synthetic)
            {
                child.MakeSynthetic();
            }

            node.AddChild(child);
        }

        QueueChildren(state, node);
    }

    /// <summary>
    ///     Queues the children of a view so that they are visited in document order.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    private static void QueueChildren(BuildState state, LayerNode node)
    {
        // Push in reverse so that the children are visited in document order
        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            state.Pending.Push(node.Children[i]);
        }
    }

    /// <summary>
    ///     Finds the operator that makes a view a concatenation, a facet or a repeat.
    /// </summary>
    /// <param name="view">The view object.</param>
    /// <returns>The first of <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c> and <c>repeat</c> that the view has; null when it has none.</returns>
    private static string? FindComposition(SpecNode view) =>
        CompositionOperators.FirstOrDefault(name => view[name] is not null);

    /// <summary>
    ///     Writes a warning.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="id">The identifier of the warning.</param>
    /// <param name="message">The message.</param>
    /// <param name="at">The node the warning is located at.</param>
    private static void Warn(BuildState state, string id, string message, SpecNode at) =>
        state.Warnings.Add(new FeatureReport(id, message, state.Tree.Locate(at), FeatureReportKind.Warning));

    /// <summary>
    ///     Creates the unit view of a view: its mark, the encoding merged from the layered views above it in its chart, and the size of the chart.
    /// </summary>
    /// <remarks>
    ///     A chart that has nothing to merge or to replace is its own unit view (no overlay), so a single view is the root object itself.
    /// </remarks>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <returns>The unit view: the view object itself, or an overlay of it.</returns>
    private static SpecNode CreateUnit(BuildState state, LayerNode node)
    {
        var chart = node.ChartRoot!;
        var merged = MergeEncoding(node);
        var encoding = merged is null ? null : ReplaceRepeaters(state, node, merged);
        var isChart = ReferenceEquals(chart, node);
        if (isChart && !node.IsFacetBody && ReferenceEquals(encoding, node.Source["encoding"]))
        {
            return node.Source;
        }

        var properties = new List<SpecNode>(8);
        if (isChart)
        {
            // The chart keeps every property it was written with, except that its encoding is the merged one and that the body of a facet only has what the facet hands down
            foreach (var property in node.Source.Properties)
            {
                if (node.IsFacetBody && Array.IndexOf(FacetBodyProperties, property.Name) < 0)
                {
                    continue;
                }

                if (property.Name == "encoding")
                {
                    if (encoding is not null)
                    {
                        properties.Add(encoding);
                    }

                    continue;
                }

                properties.Add(property);
            }

            return SpecNode.Overlay(node.Source, properties);
        }

        if (node.Source["mark"] is { } mark)
        {
            properties.Add(mark);
        }

        if (encoding is not null)
        {
            properties.Add(encoding);
        }

        foreach (var name in SizeProperties)
        {
            if (chart.Source[name] is { } size)
            {
                properties.Add(size);
            }
        }

        return SpecNode.Overlay(node.Source, properties);
    }

    /// <summary>
    ///     Merges the encodings of a layer and of the layered views above it in its chart.
    /// </summary>
    /// <remarks>
    ///     Outer encodings come first; a channel that an inner encoding repeats replaces the outer channel in place, a new channel is added after the
    ///     existing ones. When only one encoding exists it is used as it is. The merge stops at the chart: a concatenation or a facet hands no encoding down.
    ///     The body of a faceted unit view has the encoding without its faceting channels.
    /// </remarks>
    /// <param name="node">The layer.</param>
    /// <returns>The merged encoding; null when no view on the way has an encoding.</returns>
    private static SpecNode? MergeEncoding(LayerNode node)
    {
        // Outermost first; a composite mark that was expanded merged the encodings above it into its layers already
        var encodings = new List<SpecNode>();
        for (var current = (LayerNode?)node; current is not null && !current.IsExpansion; current = ReferenceEquals(current, node.ChartRoot) ? null : current.Parent)
        {
            if (current.Source["encoding"] is { IsObject: true } encoding)
            {
                encodings.Add(current.IsFacetBody ? WithoutFacetChannels(encoding) : encoding);
            }
        }

        if (encodings.Count == 0)
        {
            return null;
        }

        if (encodings.Count == 1)
        {
            return encodings[0];
        }

        encodings.Reverse();
        var channels = new List<SpecNode>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var channel in encodings.SelectMany(e => e.Properties))
        {
            if (positions.TryGetValue(channel.Name!, out var position))
            {
                channels[position] = channel;
            }
            else
            {
                positions[channel.Name!] = channels.Count;
                channels.Add(channel);
            }
        }

        // The merged encoding is located at the layer's own encoding, or else at the innermost encoding above it
        return SpecNode.Overlay(encodings[^1], channels);
    }

    /// <summary>
    ///     Gets an encoding without its faceting channels.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <returns>An overlay of the encoding with the channels other than <c>row</c>, <c>column</c> and <c>facet</c>.</returns>
    private static SpecNode WithoutFacetChannels(SpecNode encoding) =>
        SpecNode.Overlay(encoding, encoding.Properties.Where(p => Array.IndexOf(FacetChannels, p.Name) < 0));

    /// <summary>
    ///     The state of one walk over the views of a specification.
    /// </summary>
    private sealed class BuildState
    {
        /// <summary>
        ///     Initializes the state.
        /// </summary>
        /// <param name="tree">The parsed specification.</param>
        /// <param name="limits">The limits to enforce.</param>
        /// <param name="expander">Gives the views a composite mark stands for, or null.</param>
        /// <param name="config">The effective configuration.</param>
        public BuildState(SpecTree tree, VegaLiteLimits limits, Func<SpecNode, IReadOnlyList<SpecNode>?>? expander, ResolvedConfig config)
        {
            Tree = tree;
            Limits = limits;
            Expander = expander;
            Config = config;
        }

        /// <summary>Gets the parsed specification.</summary>
        public SpecTree Tree { get; }

        /// <summary>Gets the limits to enforce.</summary>
        public VegaLiteLimits Limits { get; }

        /// <summary>Gets the function that gives the views a composite mark stands for, or null.</summary>
        public Func<SpecNode, IReadOnlyList<SpecNode>?>? Expander { get; }

        /// <summary>Gets the effective configuration.</summary>
        public ResolvedConfig Config { get; }

        /// <summary>Gets the unit views found so far, in document order.</summary>
        public List<LayerNode> Units { get; } = [];

        /// <summary>Gets the concatenations and facets found so far, in document order.</summary>
        public List<LayerNode> Compositions { get; } = [];

        /// <summary>Gets the warnings found so far.</summary>
        public List<FeatureReport> Warnings { get; } = [];

        /// <summary>Gets the views still to visit.</summary>
        public Stack<LayerNode> Pending { get; } = new();

        /// <summary>Gets the repeat references that were reported as unresolved (a reference is reported once, however many copies it is in).</summary>
        public HashSet<SpecNode> UnresolvedReferences { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>Gets or sets the number of views visited so far.</summary>
        public int Count { get; set; }
    }
}
