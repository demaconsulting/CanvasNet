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
    private BoundingBox3D? _bounds;

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
    ///     Computes the tight world-space bounds of all geometry by transforming every instance
    ///     vertex in double precision (not the loose box of transformed boxes). The result is
    ///     cached.
    /// </summary>
    /// <returns>The bounds, or <see cref="BoundingBox3D.Empty"/> for a scene without vertices.</returns>
    public BoundingBox3D ComputeBounds()
    {
        if (_bounds is { } cached)
        {
            return cached;
        }

        var bounds = BoundingBox3D.Empty;
        foreach (var instance in EnumerateInstances())
        {
            foreach (var vertex in instance.Mesh.VertexSpan)
            {
                bounds = bounds.Union(instance.World.TransformPoint(vertex));
            }
        }

        _bounds = bounds;
        return bounds;
    }

    /// <summary>
    ///     Flattens the node hierarchy into mesh instances using an iterative, explicit-stack
    ///     depth-first pre-order traversal (no recursion, so arbitrarily deep nesting cannot
    ///     overflow the call stack). Each instance carries its composed world transform
    ///     (<c>node.Transform * parentWorld</c>), the innermost appearance and its pre-order index,
    ///     which the renderer uses as the draw priority.
    /// </summary>
    /// <returns>The instances in pre-order; a shared child is enumerated once per path.</returns>
    internal IEnumerable<SceneInstance> EnumerateInstances()
    {
        var order = 0;
        var stack = new Stack<(SceneNode Node, Matrix4x4D ParentWorld, NodeAppearance? ParentAppearance)>();
        stack.Push((Root, Matrix4x4D.Identity, null));

        while (stack.Count > 0)
        {
            var (node, parentWorld, parentAppearance) = stack.Pop();
            var world = node.Transform * parentWorld;
            var appearance = node.Appearance ?? parentAppearance;

            if (node is MeshNode meshNode)
            {
                yield return new SceneInstance(meshNode.Mesh, world, appearance, order++);
            }
            else if (node is GroupNode group)
            {
                // Push in reverse so the first child is popped (and numbered) first
                for (var i = group.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push((group.Children[i], world, appearance));
                }
            }
        }
    }
}
