using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An interior scene node that groups child nodes under one shared transform and appearance.
/// </summary>
/// <remarks>
///     The children are copied at construction, so a group is immutable; because a child must
///     exist before its parent, cycles are impossible. In Phase 1 a group means the plain union of
///     its children (no boolean operations). The same child instance may appear under several
///     groups (a directed acyclic graph); it is then rendered once per path.
/// </remarks>
public sealed class GroupNode : SceneNode
{
    /// <summary>Read-only view over the private child array.</summary>
    private readonly ReadOnlyCollection<SceneNode> _children;

    /// <summary>Creates a group from <paramref name="children"/>.</summary>
    /// <param name="children">The child nodes (may be empty); copied defensively.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="children"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any child is <see langword="null"/>.</exception>
    public GroupNode(IEnumerable<SceneNode> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        // Copy so later mutation of the caller's collection cannot alter the scene
        var copy = children.ToArray();
        if (Array.Exists(copy, child => child is null))
        {
            throw new ArgumentException("A group child must not be null.", nameof(children));
        }

        _children = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the child nodes in draw-priority order.</summary>
    public IReadOnlyList<SceneNode> Children => _children;
}
