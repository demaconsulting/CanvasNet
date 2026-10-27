using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An independent, typically translucent body: its children (a union) are drawn with one forced
///     <see cref="Color"/> and do not take part in the boolean structure of any ancestor.
/// </summary>
/// <remarks>
///     <para>
///         <b>Independence.</b> In the enclosing boolean expression an overlay is <em>false</em>: it is not
///         added to the solid, does not subtract from it, and does not intersect it. Instead the renderer
///         resolves the overlay as a separate solid and depth-composites its surfaces with the rest of the
///         scene, so a translucent ghost can sit in front of, inside or behind other geometry. A
///         highlighted subtrahend is expressed by placing the same node in the boolean and, wrapped in an
///         overlay, beside it.
///     </para>
///     <para>
///         <b>Color.</b> <see cref="Color"/> replaces the appearance of every mesh beneath the overlay (it is a
///         forced override, unlike the "innermost wins" rule of <see cref="NodeAppearance"/>); the
///         <see cref="SceneNode.Appearance"/> of the overlay node itself is ignored. Its alpha channel is
///         honored (255 is an opaque independent body). An overlay nested inside another overlay is a plain
///         group, so the outer color wins. The node's transform composes like a group's.
///     </para>
///     <para>
///         <b>Solids.</b> Like transparency and boolean operations, overlays are resolved with signed crossing
///         counters, so the meshes beneath an overlay must be closed, outward-wound solids.
///         The children are copied at construction; the node is immutable and may be shared.
///     </para>
/// </remarks>
public sealed class OverlayNode : SceneNode
{
    /// <summary>Read-only view over the private child array.</summary>
    private readonly ReadOnlyCollection<SceneNode> _children;

    /// <summary>Creates an overlay from a color and its children.</summary>
    /// <param name="color">The forced surface color before lighting; alpha is honored.</param>
    /// <param name="children">The child nodes (a union, may be empty); copied defensively.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="children"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any child is <see langword="null"/>.</exception>
    public OverlayNode(Rgba32 color, IEnumerable<SceneNode> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        // Copy so later mutation of the caller's collection cannot alter the scene
        var copy = children.ToArray();
        if (Array.Exists(copy, child => child is null))
        {
            throw new ArgumentException("An overlay child must not be null.", nameof(children));
        }

        Color = color;
        _children = Array.AsReadOnly(copy);
    }

    /// <summary>Gets the forced surface color of every mesh beneath the overlay; alpha is honored.</summary>
    public Rgba32 Color { get; }

    /// <summary>Gets the child nodes (their union is the overlay body) in draw-priority order.</summary>
    public IReadOnlyList<SceneNode> Children => _children;
}
