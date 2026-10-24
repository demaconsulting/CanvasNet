using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An interior scene node that combines the solids described by its children with a boolean
///     <see cref="CsgOperation"/> (constructive solid geometry), under one shared transform and
///     appearance.
/// </summary>
/// <remarks>
///     <para>
///         Like <see cref="GroupNode"/> the children are copied at construction, so the node is
///         immutable and cycles are impossible, and a child may be shared by several parents (a
///         directed acyclic graph). The transform and appearance of the node apply to every
///         child exactly as for a group; an operand without its own appearance inherits the
///         nearest ancestor's.
///     </para>
///     <para>
///         Semantics of degenerate nodes: a node without children is empty (it is never inside)
///         for <b>every</b> operation, including <see cref="CsgOperation.Intersection"/>; a node
///         with one child is that child for every operation. The scene bounds
///         (<see cref="Scene.ComputeBounds"/>) always include every operand, so they conservatively
///         enclose the result of a difference or an intersection.
///     </para>
///     <para>
///         The operands of a difference or intersection should be closed, outward-oriented meshes;
///         the renderer reports (and, on request, rejects) operands that are not.
///     </para>
/// </remarks>
public sealed class CsgNode : SceneNode
{
    /// <summary>Read-only view over the private child array.</summary>
    private readonly ReadOnlyCollection<SceneNode> _children;

    /// <summary>Creates a node that applies <paramref name="operation"/> to <paramref name="children"/>.</summary>
    /// <param name="operation">The boolean operation; must be a defined <see cref="CsgOperation"/> value.</param>
    /// <param name="children">The operand nodes (may be empty); copied defensively. For <see cref="CsgOperation.Difference"/> the first child is the minuend.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="operation"/> is not a defined value.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="children"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any child is <see langword="null"/>.</exception>
    public CsgNode(CsgOperation operation, IEnumerable<SceneNode> children)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "The CSG operation is not a defined value.");
        }

        ArgumentNullException.ThrowIfNull(children);

        // Copy so later mutation of the caller's collection cannot alter the scene
        var copy = children.ToArray();
        if (Array.Exists(copy, child => child is null))
        {
            throw new ArgumentException("A CSG child must not be null.", nameof(children));
        }

        Operation = operation;
        _children = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the boolean operation applied to the children.</summary>
    public CsgOperation Operation { get; }

    /// <summary>Gets the operand nodes in order; for <see cref="CsgOperation.Difference"/> the first is the minuend.</summary>
    public IReadOnlyList<SceneNode> Children => _children;
}
