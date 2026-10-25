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
///     The result of building one instance subtree: nothing, a 3D scene node or a 2D shape tree, with bounds.
/// </summary>
/// <remarks>
///     <para>
///         A result is consumed exactly once by its parent (the instantiation tree has no shared nodes), so the
///         builder may hand the contained node or shape on without copying. The union of several solids becomes a
///         <see cref="GroupNode"/> only when there is more than one, which keeps a deep chain of single-child
///         modules from growing wrapper nodes. A 2D result is a <see cref="ScadShape2D"/> tree (see there for why it
///         is not evaluated into one region).
///     </para>
///     <para>
///         <b>Bounds.</b> Every result carries a box that is an upper bound of the extent of the geometry (for a 2D
///         result, a box at Z = 0), computed bottom-up in O(1) per node, for <c>resize</c>. Two facts qualify it:
///         <see cref="IsApproximate"/> is set when a difference or an intersection with several operands is inside
///         (the box then is the first operand's, respectively the overlap of the operand boxes, and is not
///         necessarily the true extent); <see cref="IsTight"/> is set when the box is exactly the extent of the
///         geometry's vertices (it is lost when a rotation that is not a multiple of 90 degrees turned the box into
///         the box of the rotated box, and for an approximate result). A <c>resize</c> over a result that is
///         exact but not tight recomputes the exact vertex bounds on demand.
///     </para>
///     <para>
///         Instances are immutable and safe to share; they hold no resources.
///     </para>
/// </remarks>
internal sealed class ScadGeo
{
    /// <summary>The shared empty result.</summary>
    public static readonly ScadGeo Empty = new(ScadGeoKind.Empty, null, null, BoundingBox3D.Empty, true, false, default);

    /// <summary>
    ///     Initializes a result.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="node">The scene node of a solid.</param>
    /// <param name="shape">The shape of a flat result.</param>
    /// <param name="bounds">The bounds.</param>
    /// <param name="isTight">Whether the bounds are exactly the extent of the vertices.</param>
    /// <param name="isApproximate">Whether a difference or intersection makes the bounds an upper bound only.</param>
    /// <param name="flatLocation">The location of the first 2D leaf of a flat result.</param>
    private ScadGeo(
        ScadGeoKind kind,
        SceneNode? node,
        ScadShape2D? shape,
        BoundingBox3D bounds,
        bool isTight,
        bool isApproximate,
        ScadSourceLocation flatLocation)
    {
        Kind = kind;
        Node = node;
        Shape = shape;
        Bounds = bounds;
        IsTight = isTight;
        IsApproximate = isApproximate;
        FlatLocation = flatLocation;
    }

    /// <summary>Gets the kind of the result.</summary>
    public ScadGeoKind Kind { get; }

    /// <summary>Gets the scene node of a <see cref="ScadGeoKind.Solid"/> result; <see langword="null"/> otherwise.</summary>
    public SceneNode? Node { get; }

    /// <summary>Gets the shape of a <see cref="ScadGeoKind.Flat"/> result; <see langword="null"/> otherwise.</summary>
    public ScadShape2D? Shape { get; }

    /// <summary>Gets the upper bound of the extent of the geometry (see the type remarks).</summary>
    public BoundingBox3D Bounds { get; }

    /// <summary>Gets a value indicating whether <see cref="Bounds"/> is exactly the extent of the vertices.</summary>
    public bool IsTight { get; }

    /// <summary>Gets a value indicating whether a difference or an intersection makes <see cref="Bounds"/> an upper bound only.</summary>
    public bool IsApproximate { get; }

    /// <summary>Gets the location of the first 2D leaf of a flat result (used to place a mixed-dimension warning).</summary>
    public ScadSourceLocation FlatLocation { get; }

    /// <summary>
    ///     Creates a solid result from one mesh node.
    /// </summary>
    /// <param name="node">The mesh node.</param>
    /// <returns>The result, tight, with the bounds of the mesh.</returns>
    public static ScadGeo FromMesh(MeshNode node) => new(ScadGeoKind.Solid, node, null, node.Mesh.Bounds, true, false, default);

    /// <summary>
    ///     Creates a solid result.
    /// </summary>
    /// <param name="node">The scene node.</param>
    /// <param name="bounds">The bounds.</param>
    /// <param name="isTight">Whether the bounds are exactly the extent of the vertices.</param>
    /// <param name="isApproximate">Whether the bounds are an upper bound only.</param>
    /// <returns>The result.</returns>
    public static ScadGeo FromSolid(SceneNode node, BoundingBox3D bounds, bool isTight, bool isApproximate) =>
        new(ScadGeoKind.Solid, node, null, bounds, isTight && !isApproximate, isApproximate, default);

    /// <summary>
    ///     Creates a flat result from one region.
    /// </summary>
    /// <param name="region">The (non-empty) region.</param>
    /// <param name="location">The location of the producing instance.</param>
    /// <returns>The result, tight, with the bounds of the region in the XY plane.</returns>
    public static ScadGeo FromRegion(PolygonRegion region, ScadSourceLocation location)
    {
        var bounds = BoundingBox3D.Empty;
        foreach (var vertex in region.Vertices)
        {
            bounds = bounds.Union(new Vector3D(vertex.X, vertex.Y, 0.0));
        }

        return new ScadGeo(ScadGeoKind.Flat, null, ScadShape2D.CreateLeaf(region, location), bounds, true, false, location);
    }

    /// <summary>
    ///     Creates a flat result from a shape tree.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="bounds">The bounds.</param>
    /// <param name="isTight">Whether the bounds are exactly the extent of the vertices.</param>
    /// <param name="isApproximate">Whether the bounds are an upper bound only.</param>
    /// <param name="flatLocation">The location of the first leaf.</param>
    /// <returns>The result.</returns>
    public static ScadGeo FromShape(ScadShape2D shape, BoundingBox3D bounds, bool isTight, bool isApproximate, ScadSourceLocation flatLocation) =>
        new(ScadGeoKind.Flat, null, shape, bounds, isTight && !isApproximate, isApproximate, flatLocation);
}
