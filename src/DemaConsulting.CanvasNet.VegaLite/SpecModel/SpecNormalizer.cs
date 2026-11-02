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
///     The <c>resolve</c> of a layered view: for each channel, whether its scale, axis and legend are shared by the layers or independent.
/// </summary>
/// <remarks>
///     <para>
///         A channel that <c>resolve</c> does not name is shared. As in Vega-Lite (<c>parseGuideResolve</c>), the axis or legend of a channel whose scale is
///         independent is independent as well (one guide cannot describe two scales), and an axis or legend that is not named follows the scale.
///     </para>
///     <para>The object is immutable and safe for concurrent use.</para>
/// </remarks>
internal sealed class LayerResolve
{
    /// <summary>The explicit modes of the scales by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _scale;

    /// <summary>The explicit modes of the axes by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _axis;

    /// <summary>The explicit modes of the legends by channel.</summary>
    private readonly Dictionary<string, ResolveMode> _legend;

    /// <summary>
    ///     Initializes the resolve.
    /// </summary>
    /// <param name="scale">The explicit scale modes.</param>
    /// <param name="axis">The explicit axis modes.</param>
    /// <param name="legend">The explicit legend modes.</param>
    private LayerResolve(Dictionary<string, ResolveMode> scale, Dictionary<string, ResolveMode> axis, Dictionary<string, ResolveMode> legend)
    {
        _scale = scale;
        _axis = axis;
        _legend = legend;
    }

    /// <summary>Gets the resolve of a view that has none: everything is shared.</summary>
    public static LayerResolve Default { get; } = new([], [], []);

    /// <summary>
    ///     Reads the <c>resolve</c> object of a view; the parser has validated its shape.
    /// </summary>
    /// <param name="resolve">The <c>resolve</c> value, or null.</param>
    /// <returns>The resolve; <see cref="Default"/> when there is none.</returns>
    public static LayerResolve Read(SpecNode? resolve)
    {
        if (resolve is not { IsObject: true })
        {
            return Default;
        }

        return new LayerResolve(ReadModes(resolve["scale"]), ReadModes(resolve["axis"]), ReadModes(resolve["legend"]));
    }

    /// <summary>
    ///     Gets how the scale of a channel is resolved.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The mode; shared unless <c>resolve.scale</c> says otherwise.</returns>
    public ResolveMode Scale(string channel) => _scale.GetValueOrDefault(channel, ResolveMode.Shared);

    /// <summary>
    ///     Gets how the axis of a position channel is resolved.
    /// </summary>
    /// <param name="channel">The channel (<c>x</c> or <c>y</c>).</param>
    /// <returns>Independent when the scale is independent or <c>resolve.axis</c> says so, otherwise shared.</returns>
    public ResolveMode Axis(string channel) => Guide(_axis, channel);

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
///     One view of the layer tree: a layered view, a unit view that is drawn (a leaf), or a composition that is not drawn yet.
/// </summary>
/// <remarks>
///     <para>
///         The tree is built over the original <see cref="SpecNode"/>s of the specification; nothing is copied. <see cref="Data"/> and
///         <see cref="Transform"/> are the view's own (the document pipeline applies them down the tree, so a table is computed once for all the layers
///         below it), and a leaf's <see cref="Unit"/> is the view the later stages read: its <c>mark</c>, its <c>encoding</c> merged with the
///         encodings it inherits, and the <c>width</c> and <c>height</c> of the top-level view.
///     </para>
///     <para>The nodes are immutable once <see cref="SpecNormalizer.Layers"/> returns the tree.</para>
/// </remarks>
internal sealed class LayerNode
{
    /// <summary>The children, in document order.</summary>
    private readonly List<LayerNode> _children = [];

    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="source">The view object of the specification.</param>
    /// <param name="parent">The layered view that holds this one, or null for the root.</param>
    /// <param name="depth">The number of layered views above this view (zero for the root).</param>
    internal LayerNode(SpecNode source, LayerNode? parent, int depth)
    {
        Source = source;
        Parent = parent;
        Depth = depth;
        Resolve = LayerResolve.Default;
    }

    /// <summary>Gets the view object of the specification this node stands for.</summary>
    public SpecNode Source { get; }

    /// <summary>Gets the layered view that holds this view, or null for the root.</summary>
    public LayerNode? Parent { get; }

    /// <summary>Gets the number of layered views above this view (zero for the root).</summary>
    public int Depth { get; }

    /// <summary>
    ///     Gets the <c>data</c> object of this view itself (the data it replaces the inherited data with), or null when it inherits. An explicit
    ///     <c>"data": null</c> in a layer inherits too: Vega-Lite's <c>parseRoot</c> only creates a source when <c>model.data || !model.parent</c> holds, so
    ///     for a child view <c>null</c> is as good as absent and only the top-level view's <c>null</c> gives the empty source (the table of an absent or
    ///     <c>null</c> top-level <c>data</c> is empty).
    /// </summary>
    public SpecNode? Data => Source["data"] is { IsObject: true } data ? data : null;

    /// <summary>Gets the <c>transform</c> array of this view itself, or null. A layered view's transforms apply to the data before those of each layer.</summary>
    public SpecNode? Transform => Source["transform"] is { IsArray: true } transform ? transform : null;

    /// <summary>Gets the <c>resolve</c> of a layered view; everything shared for the others.</summary>
    public LayerResolve Resolve { get; private set; }

    /// <summary>Gets the layers of a layered view, in document order; empty for a unit view and for a composition that is not drawn.</summary>
    public IReadOnlyList<LayerNode> Children => _children;

    /// <summary>Gets a value indicating whether the view has a <c>layer</c> property (and no composition that is not drawn).</summary>
    public bool IsLayer { get; private set; }

    /// <summary>Gets the name of the composition (<c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c> or <c>repeat</c>) that makes the view undrawn, or null.</summary>
    public string? UndrawnComposition { get; private set; }

    /// <summary>
    ///     Gets the unit view that the stages after the specification read, for a unit view; null for a layered view and for a composition that is not drawn.
    /// </summary>
    /// <remarks>
    ///     For a view at the root this is the root object itself. For a layer it is an overlay of the layer (the same pointer and position) whose
    ///     <c>mark</c> is the layer's own, whose <c>encoding</c> is the merge of the encodings of the layered views above it and its own (a channel of
    ///     the layer replaces the same channel of a view above in place, new channels are added after the inherited ones) and whose <c>width</c> and
    ///     <c>height</c> are those of the top-level view. The values in the overlay are the original nodes, so a diagnostic about one points at its text.
    /// </remarks>
    public SpecNode? Unit { get; private set; }

    /// <summary>Gets a value indicating whether this is a unit view that is drawn.</summary>
    public bool IsUnit => Unit is not null;

    /// <summary>
    ///     Gets a value indicating whether this view is a composite mark (<c>boxplot</c>, ...) that was replaced by layers (<see cref="SpecNormalizer.Expand"/>).
    ///     Its own <c>encoding</c> and that of the layered views above it were merged into the layers it expanded to, so they do not inherit them again.
    /// </summary>
    public bool IsExpansion { get; private set; }

    /// <summary>Gets a value indicating whether this view was written by an expansion (it is not in the text of the specification) and is not counted as a nesting level of layers.</summary>
    public bool IsSynthetic { get; private set; }

    /// <summary>
    ///     Adds a layer.
    /// </summary>
    /// <param name="child">The layer.</param>
    internal void AddChild(LayerNode child) => _children.Add(child);

    /// <summary>
    ///     Marks the node as a layered view.
    /// </summary>
    /// <param name="resolve">The resolve of the view.</param>
    internal void MakeLayer(LayerResolve resolve)
    {
        IsLayer = true;
        Resolve = resolve;
    }

    /// <summary>
    ///     Marks the node as a composite mark that is replaced by the layers added below it.
    /// </summary>
    internal void MakeExpansion()
    {
        IsLayer = true;
        IsExpansion = true;
    }

    /// <summary>
    ///     Marks the node as written by an expansion.
    /// </summary>
    internal void MakeSynthetic() => IsSynthetic = true;

    /// <summary>
    ///     Marks the node as a composition that is not drawn.
    /// </summary>
    /// <param name="composition">The composition name.</param>
    internal void MakeUndrawn(string composition) => UndrawnComposition = composition;

    /// <summary>
    ///     Sets the unit view of a leaf.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    internal void MakeUnit(SpecNode unit) => Unit = unit;
}

/// <summary>
///     The views of a specification that are drawn, as a tree of layers: see <see cref="SpecNormalizer.Layers"/>.
/// </summary>
internal sealed class LayerTree
{
    /// <summary>
    ///     Initializes the tree.
    /// </summary>
    /// <param name="root">The root view.</param>
    /// <param name="units">The unit views in document order.</param>
    /// <param name="undrawn">The compositions that are not drawn, in document order.</param>
    /// <param name="viewCount">The number of views.</param>
    internal LayerTree(LayerNode root, IReadOnlyList<LayerNode> units, IReadOnlyList<LayerNode> undrawn, int viewCount)
    {
        Root = root;
        Units = units;
        Undrawn = undrawn;
        ViewCount = viewCount;
    }

    /// <summary>Gets the root view.</summary>
    public LayerNode Root { get; }

    /// <summary>Gets the unit views that are drawn (the leaves), in document order. A specification without composition has exactly one, the root.</summary>
    public IReadOnlyList<LayerNode> Units { get; }

    /// <summary>Gets the compositions that are not drawn yet (<c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c>, <c>repeat</c>) that were met, in document order.</summary>
    public IReadOnlyList<LayerNode> Undrawn { get; }

    /// <summary>Gets the number of views in the tree (layered views, unit views and compositions that are not drawn).</summary>
    public int ViewCount { get; }

    /// <summary>Gets a value indicating whether a composition that is not drawn yet is part of the specification (then nothing is drawn).</summary>
    public bool HasUndrawn => Undrawn.Count > 0;
}

/// <summary>
///     Turns the views of a specification into a tree of layers whose leaves are the unit views that are drawn.
/// </summary>
/// <remarks>
///     <para>
///         A specification without <c>layer</c> is one leaf, the root itself, so the single view takes exactly the path it always took. A <c>layer</c> is a
///         layered view whose children are its layers (themselves unit views or layered views); the inheritance rules of Vega-Lite apply:
///     </para>
///     <list type="bullet">
///         <item><description><b>data</b>: a view's own <c>data</c> replaces the inherited one (<see cref="LayerNode.Data"/> is the own data; the pipeline passes the table down).</description></item>
///         <item><description><b>transform</b>: a layered view's <c>transform</c> applies to the data first, then the layer's own (<see cref="LayerNode.Transform"/> per view).</description></item>
///         <item><description><b>encoding</b>: merged into <see cref="LayerNode.Unit"/>; a layer's channel replaces the inherited channel in place.</description></item>
///         <item><description><b>size</b>: the <c>width</c> and <c>height</c> of the top-level view; those of a layer are ignored.</description></item>
///         <item><description><b>resolve</b>: read per layered view (<see cref="LayerNode.Resolve"/>).</description></item>
///     </list>
///     <para>
///         The walk uses an explicit stack, so the nesting cannot overflow the call stack. <see cref="VegaLiteLimits.MaxCompositionDepth"/> bounds the
///         nesting of layers and <see cref="VegaLiteLimits.MaxViews"/> the number of views; exceeding either is a <see cref="VegaLiteRenderException"/> at the
///         offending view. The merged views are overlays: the original nodes are never copied or modified.
///     </para>
/// </remarks>
internal static partial class SpecNormalizer
{
    /// <summary>The properties of a layer that the layered view above it decides; a layer's own value is ignored.</summary>
    private static readonly string[] SizeProperties = ["width", "height"];

    /// <summary>The compositions that are not drawn yet, in the order they are looked for.</summary>
    private static readonly string[] UndrawnCompositions = ["concat", "hconcat", "vconcat", "facet", "repeat"];

    /// <summary>
    ///     Builds the layer tree of a specification.
    /// </summary>
    /// <remarks>
    ///     A composite mark (<c>boxplot</c>, ...) is left as a unit view with that mark; <see cref="Expand"/> replaces it by the layers it stands for.
    /// </remarks>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <returns>The layer tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at the offending view, when the layers are nested deeper than <see cref="VegaLiteLimits.MaxCompositionDepth"/> or the views are more than <see cref="VegaLiteLimits.MaxViews"/>.</exception>
    public static LayerTree Layers(SpecTree tree, VegaLiteLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return Build(tree, limits ?? VegaLiteLimits.Default, null);
    }

    /// <summary>
    ///     Walks the views of a specification and builds the layer tree, optionally replacing the composite marks by their layers.
    /// </summary>
    /// <param name="tree">The parsed specification.</param>
    /// <param name="effective">The limits to enforce.</param>
    /// <param name="expander">Gives the views that a composite mark stands for (null when the unit view is not a composite mark that is expanded); null to expand nothing.</param>
    /// <returns>The layer tree.</returns>
    private static LayerTree Build(SpecTree tree, VegaLiteLimits effective, Func<SpecNode, IReadOnlyList<SpecNode>?>? expander)
    {
        var root = new LayerNode(tree.Root, null, 0);
        var units = new List<LayerNode>();
        var undrawn = new List<LayerNode>();
        var count = 0;
        var pending = new Stack<LayerNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (++count > effective.MaxViews)
            {
                throw new VegaLiteRenderException(
                    $"The specification has more than {effective.MaxViews} views, the limit (MaxViews).",
                    tree.Locate(node.Source));
            }

            var view = node.Source;
            var composition = FindComposition(view);
            if (composition is not null)
            {
                node.MakeUndrawn(composition);
                undrawn.Add(node);
                continue;
            }

            if (view["layer"] is not { IsArray: true } layers)
            {
                var unit = ReferenceEquals(view, tree.Root) ? view : CreateUnit(node, tree.Root);
                if (expander?.Invoke(unit) is { } expanded)
                {
                    // The composite mark is a layered view of the layers it stands for; its own encoding is merged into them already
                    node.MakeExpansion();
                    AddChildren(node, expanded, node.Depth + 1, synthetic: true, pending);
                    continue;
                }

                node.MakeUnit(unit);
                units.Add(node);
                continue;
            }

            var depth = node.Depth + 1;
            if (!node.IsSynthetic && depth > effective.MaxCompositionDepth)
            {
                throw new VegaLiteRenderException(
                    $"The layers are nested more than {effective.MaxCompositionDepth} levels deep, the limit (MaxCompositionDepth).",
                    tree.Locate(layers));
            }

            node.MakeLayer(LayerResolve.Read(view["resolve"]));
            AddChildren(node, layers.Items.Where(i => i.IsObject).ToList(), depth, node.IsSynthetic, pending);
        }

        return new LayerTree(root, units, undrawn, count);
    }

    /// <summary>
    ///     Adds the layers of a layered view as children and queues them.
    /// </summary>
    /// <param name="node">The layered view.</param>
    /// <param name="views">The view objects of its layers, in document order.</param>
    /// <param name="depth">The depth of the layers.</param>
    /// <param name="synthetic">Whether the layers were written by an expansion.</param>
    /// <param name="pending">The stack of views still to visit.</param>
    private static void AddChildren(LayerNode node, IReadOnlyList<SpecNode> views, int depth, bool synthetic, Stack<LayerNode> pending)
    {
        var children = new List<LayerNode>(views.Count);
        foreach (var item in views)
        {
            var child = new LayerNode(item, node, depth);
            if (synthetic)
            {
                child.MakeSynthetic();
            }

            node.AddChild(child);
            children.Add(child);
        }

        // Push in reverse so that the layers are visited in document order
        for (var i = children.Count - 1; i >= 0; i--)
        {
            pending.Push(children[i]);
        }
    }

    /// <summary>
    ///     Finds the composition that is not drawn yet on a view.
    /// </summary>
    /// <param name="view">The view object.</param>
    /// <returns>The first of <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c> and <c>repeat</c> that the view has; null when it has none.</returns>
    private static string? FindComposition(SpecNode view) =>
        UndrawnCompositions.FirstOrDefault(name => view[name] is not null);

    /// <summary>
    ///     Creates the unit view of a layer: its mark, the encoding merged from the layered views above it, and the size of the top-level view.
    /// </summary>
    /// <param name="node">The layer.</param>
    /// <param name="root">The top-level view.</param>
    /// <returns>The overlay of the layer.</returns>
    private static SpecNode CreateUnit(LayerNode node, SpecNode root)
    {
        var properties = new List<SpecNode>(4);
        if (node.Source["mark"] is { } mark)
        {
            properties.Add(mark);
        }

        if (MergeEncoding(node) is { } encoding)
        {
            properties.Add(encoding);
        }

        foreach (var name in SizeProperties)
        {
            if (root[name] is { } size)
            {
                properties.Add(size);
            }
        }

        return SpecNode.Overlay(node.Source, properties);
    }

    /// <summary>
    ///     Merges the encodings of a layer and of the layered views above it.
    /// </summary>
    /// <remarks>
    ///     Outer encodings come first; a channel that an inner encoding repeats replaces the outer channel in place, a new channel is added after the
    ///     existing ones. When only one encoding exists it is used as it is.
    /// </remarks>
    /// <param name="node">The layer.</param>
    /// <returns>The merged encoding; null when no view on the way has an encoding.</returns>
    private static SpecNode? MergeEncoding(LayerNode node)
    {
        // Outermost first; a composite mark that was expanded merged the encodings above it into its layers already
        var encodings = new List<SpecNode>();
        for (var current = (LayerNode?)node; current is not null && !current.IsExpansion; current = current.Parent)
        {
            if (current.Source["encoding"] is { IsObject: true } encoding)
            {
                encodings.Add(encoding);
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
}
