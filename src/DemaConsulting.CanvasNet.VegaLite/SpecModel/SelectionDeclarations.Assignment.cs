// Portions derived from Vega-Lite (BSD-3-Clause): the normalization that pushes top-level selections down to the unit views (normalize/toplevelselection.ts); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     The assignment of selections to unit views (a partial file of the selection declaration helpers).
/// </content>
internal static partial class SelectionDeclarations
{
    /// <summary>
    ///     One selection declaration that applies to one unit view.
    /// </summary>
    /// <param name="Declaration">The declaration node.</param>
    /// <param name="Unit">The unit view.</param>
    /// <param name="IsDuplicate">Whether Vega rejects the compiled specification because two units of one layered view declare the same selection.</param>
    internal sealed record Assignment(SpecNode Declaration, LayerNode Unit, bool IsDuplicate);

    /// <summary>
    ///     Decides which selections apply to which unit views (Vega-Lite's <c>TopLevelSelectionsNormalizer</c>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The selections declared by a view that is not a unit view are pushed down to its units: all of them, or those that the <c>views</c> of the selection names (the <c>name</c> of
    ///         a unit, or of a view above it). Vega-Lite shares one list of selections for the whole walk, so a selection of a nested composition also reaches the units that come after it in
    ///         document order, whatever view they are in; this does the same. A unit view that receives selections this way gets exactly those, instead of its own; otherwise it keeps its own.
    ///     </para>
    ///     <para>
    ///         The two or more units of one layered view that receive the same selection declare duplicate signals, and Vega rejects the specification; those assignments are marked.
    ///     </para>
    /// </remarks>
    /// <param name="tree">The layer tree.</param>
    /// <returns>The assignments in document order.</returns>
    public static IReadOnlyList<Assignment> Assign(LayerTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var result = new List<(SpecNode Declaration, LayerNode Unit)>();
        var shared = new List<SpecNode>();
        Visit(tree.Root, [], shared, result);

        // Units of one layered view that receive the same declaration
        var duplicates = new HashSet<(SpecNode, LayerNode)>();
        foreach (var group in result.GroupBy(r => (r.Declaration, Layer: OutermostLayer(r.Unit))).Where(g => g.Key.Layer is not null && g.Count() >= 2))
        {
            foreach (var member in group)
            {
                duplicates.Add((member.Declaration, member.Unit));
            }
        }

        return [.. result.Select(r => new Assignment(r.Declaration, r.Unit, duplicates.Contains((r.Declaration, r.Unit))))];
    }

    /// <summary>
    ///     Walks a view of the tree: a view that is not a unit adds its selections to the shared list and visits its children; a unit takes what applies to it.
    /// </summary>
    /// <remarks>The recursion follows the nesting of the views, which <see cref="VegaLiteLimits.MaxCompositionDepth"/> bounds.</remarks>
    /// <param name="node">The view.</param>
    /// <param name="path">The names of the named views above the view.</param>
    /// <param name="shared">The selections pushed down so far.</param>
    /// <param name="result">Receives the assignments.</param>
    private static void Visit(LayerNode node, IReadOnlyList<string> path, List<SpecNode> shared, List<(SpecNode, LayerNode)> result)
    {
        if (!node.IsUnit)
        {
            // The unit view with faceting channels is a facet in this tree; its selections belong to the unit inside
            if (!(node.Kind == ViewKind.Facet && node.Children.Any(c => c.IsFacetBody)))
            {
                shared.AddRange(DeclarationsOf(node.Source));
            }

            IReadOnlyList<string> below = node.Source["name"]?.StringValue is { } name ? [.. path, name] : path;
            foreach (var child in node.Children)
            {
                Visit(child, below, shared, result);
            }

            return;
        }

        var unitName = node.Source["name"]?.StringValue;
        IReadOnlyList<string> unitPath = unitName is null ? path : [.. path, unitName];
        var applied = shared.Where(s => AppliesTo(s, unitName, unitPath)).ToList();
        foreach (var declaration in applied.Count > 0 ? applied : DeclarationsOf(node.Source).ToList())
        {
            result.Add((declaration, node));
        }
    }

    /// <summary>
    ///     Tells whether a top-level selection applies to a unit: it has no <c>views</c>, or one of them is the name of the unit or of a view above it.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    /// <param name="unitName">The <c>name</c> of the unit, or null.</param>
    /// <param name="path">The names of the views above the unit, and the unit.</param>
    /// <returns><see langword="true"/> when the selection applies.</returns>
    private static bool AppliesTo(SpecNode declaration, string? unitName, IReadOnlyList<string> path)
    {
        if (declaration["views"] is not { IsArray: true } views || views.Items.Count == 0)
        {
            return true;
        }

        foreach (var view in views.Items)
        {
            if (view.IsString && (view.StringValue == unitName || path.Contains(view.StringValue!)))
            {
                return true;
            }

            // A list of names is a path through the tree: each name is below the one before
            if (view.IsArray && view.Items.All(v => v.IsString) && FollowsPath(path, view.Items.Select(v => v.StringValue!)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether the names occur in a path in the given order.
    /// </summary>
    /// <param name="path">The names of the views from the root down.</param>
    /// <param name="names">The names to find, in order.</param>
    /// <returns><see langword="true"/> when each name is found after the one before.</returns>
    private static bool FollowsPath(IReadOnlyList<string> path, IEnumerable<string> names)
    {
        var next = 0;
        foreach (var name in names)
        {
            while (next < path.Count && path[next] != name)
            {
                next++;
            }

            if (next == path.Count)
            {
                return false;
            }

            next++;
        }

        return true;
    }

    /// <summary>
    ///     Finds the outermost layered view that holds a unit through a chain of layered views.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The layered view; null when the unit is not a layer of a layered view.</returns>
    private static LayerNode? OutermostLayer(LayerNode unit)
    {
        LayerNode? layer = null;
        for (var parent = unit.Parent; parent is { Kind: ViewKind.Layer }; parent = parent.Parent)
        {
            layer = parent;
        }

        return layer;
    }

    /// <summary>
    ///     Gets the name of a unit: its <c>name</c>, otherwise a name made from the kinds and positions of the views above it.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The name; empty for the root unit.</returns>
    public static string UnitNameOf(LayerNode unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.Source["name"]?.StringValue is { } name)
        {
            return name;
        }

        var parts = new List<string>();
        for (var node = unit; node.Parent is { } parent; node = parent)
        {
            var kind = parent.Kind is ViewKind.Layer or ViewKind.Expansion ? "layer" : "concat";
            parts.Add(parent.Kind == ViewKind.Facet ? "child" : kind + "_" + parent.Children.ToList().IndexOf(node));
        }

        parts.Reverse();
        return string.Join("_", parts);
    }
}
