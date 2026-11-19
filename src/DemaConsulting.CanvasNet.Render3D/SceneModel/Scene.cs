namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable 3D scene: a root <see cref="SceneNode"/> describing geometry only.
/// </summary>
/// <remarks>
///     A scene is pure geometry and appearance; the look of a render (camera, lights, background,
///     default colors) is chosen at render time so the same scene can be rendered many ways.
///     Scenes are thread-safe and may be rendered concurrently.
/// </remarks>
public sealed class Scene
{
    /// <summary>Lazily computed tight bounds (idempotent, so a benign race merely recomputes).</summary>
    /// <remarks>Held as a boxed immutable value so a reference write publishes it atomically.</remarks>
    private object? _bounds;

    /// <summary>Lazily computed tight bounds that exclude <see cref="OverlayNode"/> bodies (same publication rule as <see cref="_bounds"/>).</summary>
    private object? _boundsWithoutOverlays;

    /// <summary>Creates a scene from a root node.</summary>
    /// <param name="root">The root node; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    public Scene(SceneNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Root = root;
    }

    /// <summary>Gets the root node.</summary>
    public SceneNode Root { get; }

    /// <summary>
    ///     Computes the world-space bounds of what the scene draws: the bounds of the result of its
    ///     boolean structure plus the bodies of its <see cref="OverlayNode"/> instances (this equals
    ///     <c>ComputeBounds(true)</c>). The result is cached.
    /// </summary>
    /// <remarks>
    ///     Mesh geometry is bounded tightly by transforming every vertex in double precision (not the loose
    ///     box of transformed boxes). A <see cref="CsgNode"/> combines the bounds of its operands like its
    ///     operation combines the solids: a union has the union of the boxes, a difference the box of its
    ///     first operand (the geometry it removes does not count), and an intersection the overlap of the
    ///     boxes (empty when they are disjoint). The bounds of a difference or an intersection are therefore
    ///     a tight upper bound of the (possibly smaller) result. Overlay bodies count wherever they are, even
    ///     inside a subtracted operand, because they are drawn as independent bodies. Automatic camera
    ///     fitting uses these bounds, so a large cutter does not shrink the framed model.
    /// </remarks>
    /// <returns>The bounds, or <see cref="BoundingBox3D.Empty"/> for a scene that draws nothing.</returns>
    public BoundingBox3D ComputeBounds() => ComputeBounds(true);

    /// <summary>
    ///     Computes the world-space bounds of the geometry like <see cref="ComputeBounds()"/>,
    ///     optionally leaving out the bodies of <see cref="OverlayNode"/> instances. The result is cached.
    /// </summary>
    /// <remarks>
    ///     The walk uses an explicit stack (no recursion), so arbitrarily deep nesting cannot overflow the
    ///     call stack. A shared child is bounded once per path, like <see cref="EnumerateInstances"/>.
    /// </remarks>
    /// <param name="includeOverlays">
    ///     <see langword="true"/> to include overlay bodies (the same result as <see cref="ComputeBounds()"/>);
    ///     <see langword="false"/> to bound only the result of the scene's own solid.
    /// </param>
    /// <returns>The bounds, or <see cref="BoundingBox3D.Empty"/> when nothing counted has a vertex.</returns>
    public BoundingBox3D ComputeBounds(bool includeOverlays)
    {
        if ((includeOverlays ? Volatile.Read(ref _bounds) : Volatile.Read(ref _boundsWithoutOverlays)) is BoundingBox3D cached)
        {
            return cached;
        }

        // One walk yields both results, so both caches are filled at once
        var (solid, overlay) = ComputeResultBounds(Root);
        var all = solid.Union(overlay);
        Volatile.Write(ref _bounds, all);
        Volatile.Write(ref _boundsWithoutOverlays, solid);
        return includeOverlays ? all : solid;
    }

    /// <summary>
    ///     Bounds the result of the boolean structure beneath <paramref name="root"/> and, separately, its
    ///     overlay bodies, with an iterative post-order walk.
    /// </summary>
    /// <remarks>
    ///     Each node yields a pair of world-space boxes: <c>Solid</c> bounds the node's contribution to the
    ///     enclosing solid and <c>Overlay</c> the overlay bodies beneath it. A mesh yields its transformed
    ///     vertex box as the solid. A group, a union and a nested overlay (a plain group, as in
    ///     <see cref="EnumerateInstances"/>) union their children's solids; a difference keeps its first
    ///     child's solid; an intersection keeps the overlap of its children's solids. The outermost overlay
    ///     turns everything beneath it into overlay, which is never removed by an enclosing boolean. Interior
    ///     nodes are pushed twice: once to expand their children, then again to combine the children's
    ///     results, which are popped from a separate result stack.
    /// </remarks>
    /// <param name="root">The root of the walk.</param>
    /// <returns>The solid-result box and the overlay box (each possibly <see cref="BoundingBox3D.Empty"/>).</returns>
    private static (BoundingBox3D Solid, BoundingBox3D Overlay) ComputeResultBounds(SceneNode root)
    {
        var work = new Stack<(SceneNode Node, Matrix4x4D ParentWorld, bool InOverlay, bool Expanded)>();
        var results = new Stack<(BoundingBox3D Solid, BoundingBox3D Overlay)>();
        work.Push((root, Matrix4x4D.Identity, false, false));

        while (work.Count > 0)
        {
            var (node, parentWorld, inOverlay, expanded) = work.Pop();
            var world = node.Transform * parentWorld;

            // A mesh is a leaf: its exact vertex box under the composed transform is its solid
            if (node is MeshNode meshNode)
            {
                var box = BoundingBox3D.Empty;
                foreach (var vertex in meshNode.Mesh.VertexSpan)
                {
                    box = box.Union(world.TransformPoint(vertex));
                }

                results.Push((box, BoundingBox3D.Empty));
                continue;
            }

            var children = ChildrenOf(node);
            var startsOverlay = node is OverlayNode && !inOverlay;

            // First visit: come back after the children; they are pushed in reverse so the first child is
            // bounded first and each child's subtree leaves exactly one result, in child order
            if (!expanded)
            {
                work.Push((node, parentWorld, inOverlay, true));
                for (var i = children.Count - 1; i >= 0; i--)
                {
                    work.Push((children[i], world, inOverlay || startsOverlay, false));
                }

                continue;
            }

            // Second visit: pop the children's results (last child on top) back into child order
            var childResults = new (BoundingBox3D Solid, BoundingBox3D Overlay)[children.Count];
            for (var i = children.Count - 1; i >= 0; i--)
            {
                childResults[i] = results.Pop();
            }

            results.Push(CombineResultBounds(node, childResults, startsOverlay));
        }

        return results.Pop();
    }

    /// <summary>Gets the children of an interior node (none for any other node).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The children in order.</returns>
    private static IReadOnlyList<SceneNode> ChildrenOf(SceneNode node) => node switch
    {
        GroupNode group => group.Children,
        CsgNode csg => csg.Children,
        OverlayNode overlay => overlay.Children,
        _ => [],
    };

    /// <summary>Combines the bounds of the children of an interior node into the node's own result bounds.</summary>
    /// <param name="node">The interior node.</param>
    /// <param name="children">The result bounds of its children, in child order.</param>
    /// <param name="startsOverlay"><see langword="true"/> when the node is the outermost overlay on its path.</param>
    /// <returns>The node's solid-result box and overlay box.</returns>
    private static (BoundingBox3D Solid, BoundingBox3D Overlay) CombineResultBounds(
        SceneNode node,
        (BoundingBox3D Solid, BoundingBox3D Overlay)[] children,
        bool startsOverlay)
    {
        // Overlay bodies always pass upward: no boolean ancestor removes them
        var solid = BoundingBox3D.Empty;
        var overlay = BoundingBox3D.Empty;
        foreach (var child in children)
        {
            overlay = overlay.Union(child.Overlay);
        }

        if (startsOverlay)
        {
            // The outermost overlay draws everything beneath it as an independent body
            foreach (var child in children)
            {
                overlay = overlay.Union(child.Solid);
            }

            return (solid, overlay);
        }

        var operation = node is CsgNode csg ? csg.Operation : CsgOperation.Union;
        if (operation == CsgOperation.Difference)
        {
            // Only the minuend can contribute; what the other operands remove does not count
            solid = children.Length > 0 ? children[0].Solid : BoundingBox3D.Empty;
        }
        else if (operation == CsgOperation.Intersection)
        {
            // The result lies in every operand, so at most in the overlap of their boxes
            solid = children.Length > 0 ? children[0].Solid : BoundingBox3D.Empty;
            for (var i = 1; i < children.Length; i++)
            {
                solid = Overlap(solid, children[i].Solid);
            }
        }
        else
        {
            // Groups, unions and nested overlays (plain groups) keep every child
            foreach (var child in children)
            {
                solid = solid.Union(child.Solid);
            }
        }

        return (solid, overlay);
    }

    /// <summary>Computes the overlap of two boxes.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns>The common box, or <see cref="BoundingBox3D.Empty"/> when either is empty or they are disjoint.</returns>
    private static BoundingBox3D Overlap(BoundingBox3D a, BoundingBox3D b)
    {
        if (a.IsEmpty || b.IsEmpty)
        {
            return BoundingBox3D.Empty;
        }

        // The overlap starts at the larger minimum and ends at the smaller maximum on every axis
        var low = Vector3D.Max(a.Min, b.Min);
        var high = Vector3D.Min(a.Max, b.Max);
        var overlap = new BoundingBox3D(low, high);
        return overlap.IsEmpty ? BoundingBox3D.Empty : overlap;
    }

    /// <summary>
    ///     Flattens the node hierarchy into mesh instances using an iterative, explicit-stack
    ///     depth-first pre-order traversal (no recursion, so arbitrarily deep nesting cannot
    ///     overflow the call stack). Each instance carries its composed world transform
    ///     (<c>node.Transform * parentWorld</c>), the innermost appearance and its pre-order index,
    ///     which the renderer uses as the draw priority.
    /// </summary>
    /// <remarks>
    ///     Both <see cref="GroupNode"/> and <see cref="CsgNode"/> children are descended, so every
    ///     operand of a boolean node is an instance; the operation itself is not represented here.
    ///     The children of an <see cref="OverlayNode"/> are descended too: every instance beneath an
    ///     overlay carries the overlay's color as a forced appearance (replacing inner appearances) and is
    ///     marked <see cref="SceneInstance.InOverlay"/>; an overlay nested inside an overlay is a plain group.
    ///     Each instance also carries the <see cref="MeshNode"/> it came from (for diagnostics).
    /// </remarks>
    /// <returns>The instances in pre-order; a shared child is enumerated once per path.</returns>
    internal IEnumerable<SceneInstance> EnumerateInstances()
    {
        var order = 0;
        var stack = new Stack<(SceneNode Node, Matrix4x4D ParentWorld, NodeAppearance? ParentAppearance, NodeAppearance? Forced)>();
        stack.Push((Root, Matrix4x4D.Identity, null, null));

        while (stack.Count > 0)
        {
            var (node, parentWorld, parentAppearance, forced) = stack.Pop();
            var world = node.Transform * parentWorld;

            // A forced (overlay) appearance beats every appearance beneath the overlay
            var appearance = forced ?? node.Appearance ?? parentAppearance;

            if (node is MeshNode meshNode)
            {
                yield return new SceneInstance(meshNode.Mesh, world, appearance, order++, meshNode, forced is not null);
            }
            else if (node is GroupNode group)
            {
                PushChildren(stack, group.Children, world, appearance, forced);
            }
            else if (node is CsgNode csg)
            {
                // A CSG node enumerates its operands exactly like a group; the boolean structure is
                // recovered by the renderer from the same pre-order walk, so no operand is ever dropped
                PushChildren(stack, csg.Children, world, appearance, forced);
            }
            else if (node is OverlayNode overlay)
            {
                // The outermost overlay forces its color; one nested inside is a plain group
                forced ??= new NodeAppearance(overlay.Color);
                PushChildren(stack, overlay.Children, world, forced, forced);
            }
        }
    }

    /// <summary>
    ///     Pushes <paramref name="children"/> in reverse so the first child is popped (and numbered)
    ///     first, keeping the traversal a pre-order depth-first walk.
    /// </summary>
    /// <param name="stack">The traversal stack.</param>
    /// <param name="children">The children of the node being expanded.</param>
    /// <param name="world">The world transform composed so far for the expanded node.</param>
    /// <param name="appearance">The innermost appearance of the expanded node.</param>
    /// <param name="forced">The forced appearance of the enclosing overlay, or <see langword="null"/> outside overlays.</param>
    private static void PushChildren(
        Stack<(SceneNode Node, Matrix4x4D ParentWorld, NodeAppearance? ParentAppearance, NodeAppearance? Forced)> stack,
        IReadOnlyList<SceneNode> children,
        Matrix4x4D world,
        NodeAppearance? appearance,
        NodeAppearance? forced)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push((children[i], world, appearance, forced));
        }
    }
}
