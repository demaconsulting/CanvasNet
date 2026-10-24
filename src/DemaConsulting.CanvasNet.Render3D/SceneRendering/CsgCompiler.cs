using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The result of compiling a scene's node tree: the CSG program (when one is needed) and the leaf of every mesh instance.
/// </summary>
/// <param name="Program">The compiled program, or <see langword="null"/> when the scene is union-only and the z-buffer path renders it.</param>
/// <param name="LeafOfInstance">The leaf index of every mesh instance, indexed by <see cref="SceneInstance.Order"/> (pre-order position).</param>
/// <param name="LeafCount">The number of leaves (collapsed union-only subtrees that contain at least one instance).</param>
internal readonly record struct CsgCompilation(CsgProgram? Program, int[] LeafOfInstance, int LeafCount);

/// <summary>
///     Compiles a scene's node tree into a <see cref="CsgProgram"/> over union-collapsed leaves.
/// </summary>
/// <remarks>
///     <para>
///         <b>Union collapse.</b> A subtree is <em>union-only</em> when it contains no
///         <see cref="CsgOperation.Difference"/> or <see cref="CsgOperation.Intersection"/> node with
///         two or more children (a single-child boolean is just that child). Each maximal union-only
///         subtree becomes one <b>leaf</b>: all its mesh instances share the leaf's signed crossing
///         counter, whose positivity is the union of its meshes. A union-only root therefore needs no
///         program at all (<see cref="CsgCompilation.Program"/> is <see langword="null"/>) and is
///         rendered by the z-buffer fast path.
///     </para>
///     <para>
///         <b>Other nodes.</b> A group or union node is the <c>Or</c> of its children,
///         <c>Difference</c> is <c>first AND NOT second AND NOT third ...</c>, <c>Intersection</c> is the
///         <c>And</c> chain, and a node with no children (or a subtree without any mesh) is
///         <c>FALSE</c>, for every operation.
///     </para>
///     <para>
///         <b>Instance mapping.</b> The tree is walked in the same pre-order, with children in order,
///         as <see cref="Scene.EnumerateInstances"/>, so the leaf of the instance numbered <c>n</c> is
///         <c>LeafOfInstance[n]</c>; a node shared by several parents is expanded once per path, exactly
///         like the enumeration.
///     </para>
///     <para>
///         <b>No recursion.</b> Every traversal uses an explicit stack so arbitrarily deep scenes
///         cannot overflow the call stack. Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class CsgCompiler
{
    /// <summary>
    ///     Compiles a node tree.
    /// </summary>
    /// <param name="root">The scene root.</param>
    /// <returns>The program (or <see langword="null"/> for a union-only scene) and the instance-to-leaf mapping.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    public static CsgCompilation Compile(SceneNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var hasBoolean = ComputeHasBoolean(root);
        var code = new List<CsgInstruction>();
        var leafOfInstance = new List<int>();
        var leafCount = 0;

        // Walk the tree in pre-order, emitting postfix code; frames resume a node after each child
        var stack = new Stack<Frame>();
        EnterNode(root);
        while (stack.Count > 0)
        {
            var frame = stack.Pop();

            // The child that just finished (index Next - 1) is combined into the accumulated result
            if (frame.Next > 1)
            {
                code.Add(new CsgInstruction(frame.Combiner, 0));
            }

            if (frame.Next < frame.Children.Count)
            {
                stack.Push(frame with { Next = frame.Next + 1 });
                EnterNode(frame.Children[frame.Next]);
            }
        }

        // A union-only scene needs no program: the z-buffer renders it exactly
        if (!hasBoolean[root])
        {
            return new CsgCompilation(null, [.. leafOfInstance], leafCount);
        }

        return new CsgCompilation(new CsgProgram(code, leafCount), [.. leafOfInstance], leafCount);

        // Emits a collapsed leaf / FALSE for a union-only subtree, or opens a frame for a boolean node
        void EnterNode(SceneNode node)
        {
            if (!hasBoolean[node])
            {
                var count = CountInstances(node);
                if (count == 0)
                {
                    code.Add(new CsgInstruction(CsgOpCode.PushFalse, 0));
                    return;
                }

                // All instances of the collapsed subtree share one leaf
                var leaf = leafCount++;
                for (var i = 0; i < count; i++)
                {
                    leafOfInstance.Add(leaf);
                }

                code.Add(new CsgInstruction(CsgOpCode.PushLeaf, leaf));
                return;
            }

            // A boolean node is a group or CSG node; an empty one is FALSE for every operation
            var children = ChildrenOf(node, out var operation)!;
            if (children.Count == 0)
            {
                code.Add(new CsgInstruction(CsgOpCode.PushFalse, 0));
                return;
            }

            stack.Push(new Frame(children, CombinerOf(operation), 0));
        }
    }

    /// <summary>The state of a boolean node whose children are being emitted.</summary>
    /// <param name="Children">The node's children.</param>
    /// <param name="Combiner">The instruction combining each child after the first into the result.</param>
    /// <param name="Next">The index of the next child to emit.</param>
    private readonly record struct Frame(IReadOnlyList<SceneNode> Children, CsgOpCode Combiner, int Next);

    /// <summary>Maps a boolean operation to the instruction that folds a further child into the result.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The instruction.</returns>
    private static CsgOpCode CombinerOf(CsgOperation operation) => operation switch
    {
        CsgOperation.Difference => CsgOpCode.AndNot,
        CsgOperation.Intersection => CsgOpCode.And,
        _ => CsgOpCode.Or,
    };

    /// <summary>
    ///     Gets the children of a group or CSG node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="operation">Receives the node's operation (union for a group).</param>
    /// <returns>The children, or <see langword="null"/> for a mesh node.</returns>
    private static IReadOnlyList<SceneNode>? ChildrenOf(SceneNode node, out CsgOperation operation)
    {
        switch (node)
        {
            case GroupNode group:
                operation = CsgOperation.Union;
                return group.Children;
            case CsgNode csg:
                operation = csg.Operation;
                return csg.Children;
            default:
                operation = CsgOperation.Union;
                return null;
        }
    }

    /// <summary>
    ///     Determines, for every node reachable from the root, whether its subtree contains a
    ///     difference or intersection with two or more children. Shared nodes are evaluated once.
    /// </summary>
    /// <param name="root">The scene root.</param>
    /// <returns>The flag per node, compared by reference.</returns>
    private static Dictionary<SceneNode, bool> ComputeHasBoolean(SceneNode root)
    {
        var result = new Dictionary<SceneNode, bool>(ReferenceEqualityComparer.Instance);
        var stack = new List<(SceneNode Node, int Next)> { (root, 0) };

        while (stack.Count > 0)
        {
            var (node, next) = stack[^1];
            var children = ChildrenOf(node, out var operation);

            // A mesh node is a leaf of the tree
            if (children is null)
            {
                result[node] = false;
                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            // Descend into the next child that has not been evaluated yet
            if (next < children.Count)
            {
                stack[^1] = (node, next + 1);
                var child = children[next];
                if (!result.ContainsKey(child))
                {
                    stack.Add((child, 0));
                }

                continue;
            }

            // All children done: the node is boolean itself or because some child is
            var boolean = operation != CsgOperation.Union && children.Count >= 2;
            foreach (var child in children)
            {
                boolean |= result[child];
            }

            result[node] = boolean;
            stack.RemoveAt(stack.Count - 1);
        }

        return result;
    }

    /// <summary>
    ///     Counts the mesh instances of a subtree (a node shared by several parents counts once per path).
    /// </summary>
    /// <param name="node">The subtree root.</param>
    /// <returns>The number of mesh instances.</returns>
    private static int CountInstances(SceneNode node)
    {
        var count = 0;
        var stack = new Stack<SceneNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var children = ChildrenOf(current, out _);
            if (children is null)
            {
                count++;
                continue;
            }

            foreach (var child in children)
            {
                stack.Push(child);
            }
        }

        return count;
    }
}
