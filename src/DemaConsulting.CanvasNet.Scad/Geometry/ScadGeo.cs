using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The kind of a <see cref="ScadGeo"/>.
/// </summary>
internal enum ScadGeoKind
{
    /// <summary>No geometry (an unsupported module, an invalid instance or an empty group).</summary>
    Empty,

    /// <summary>A three-dimensional scene node.</summary>
    Solid,

    /// <summary>A two-dimensional shape that has not been extruded.</summary>
    Flat,
}

/// <summary>
///     One 2D leaf of a flat result: a region and the location of the instance that produced it.
/// </summary>
/// <param name="Region">The region in the XY plane.</param>
/// <param name="Location">The location of the producing instance.</param>
internal readonly record struct ScadFlatLeaf(PolygonRegion Region, ScadSourceLocation Location);

/// <summary>
///     The result of building one instance subtree: nothing, a 3D scene node or a 2D shape.
/// </summary>
/// <remarks>
///     <para>
///         A result is consumed exactly once by its parent (the instantiation tree has no shared nodes), so the
///         builder may hand the contained list or node on without copying. The union of several solids becomes a
///         <see cref="GroupNode"/> only when there is more than one, which keeps a deep chain of single-child
///         modules from growing wrapper nodes. A 2D result is a list of leaves whose union is the shape; later
///         phases replace the list by a tree when 2D booleans and transforms are added.
///     </para>
///     <para>
///         Instances are immutable and safe to share; they hold no resources.
///     </para>
/// </remarks>
internal sealed class ScadGeo
{
    /// <summary>The shared empty result.</summary>
    public static readonly ScadGeo Empty = new(ScadGeoKind.Empty, null, null);

    /// <summary>
    ///     Initializes a result.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="node">The scene node of a solid.</param>
    /// <param name="leaves">The leaves of a flat shape.</param>
    private ScadGeo(ScadGeoKind kind, SceneNode? node, IReadOnlyList<ScadFlatLeaf>? leaves)
    {
        Kind = kind;
        Node = node;
        Leaves = leaves;
    }

    /// <summary>Gets the kind of the result.</summary>
    public ScadGeoKind Kind { get; }

    /// <summary>Gets the scene node of a <see cref="ScadGeoKind.Solid"/> result; <see langword="null"/> otherwise.</summary>
    public SceneNode? Node { get; }

    /// <summary>Gets the leaves of a <see cref="ScadGeoKind.Flat"/> result (never empty); <see langword="null"/> otherwise.</summary>
    public IReadOnlyList<ScadFlatLeaf>? Leaves { get; }

    /// <summary>
    ///     Creates a solid result.
    /// </summary>
    /// <param name="node">The scene node.</param>
    /// <returns>The result.</returns>
    public static ScadGeo FromSolid(SceneNode node) => new(ScadGeoKind.Solid, node, null);

    /// <summary>
    ///     Creates a flat result from a list of leaves (their union).
    /// </summary>
    /// <param name="leaves">The leaves; must not be empty.</param>
    /// <returns>The result.</returns>
    public static ScadGeo FromFlat(IReadOnlyList<ScadFlatLeaf> leaves) => new(ScadGeoKind.Flat, null, leaves);
}
