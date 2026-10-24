namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     The boolean operation a <see cref="CsgNode"/> applies to the solids described by its
///     children (constructive solid geometry).
/// </summary>
/// <remarks>
///     The operations are n-ary and are evaluated on the <i>solids</i> the children enclose, so
///     every mesh that takes part in a <see cref="Difference"/> or <see cref="Intersection"/>
///     should be a closed, outward-oriented surface (see <see cref="Mesh.IsClosed"/> and
///     <see cref="Mesh.SignedVolume"/>). A node without children is empty (never inside), for
///     every operation.
/// </remarks>
public enum CsgOperation
{
    /// <summary>The union of all children: a point is inside when it is inside any child.</summary>
    Union = 0,

    /// <summary>
    ///     The first child minus all remaining children: a point is inside when it is inside the
    ///     first child and outside every other child. A single child is returned unchanged.
    /// </summary>
    Difference = 1,

    /// <summary>
    ///     The intersection of all children: a point is inside when it is inside every child. A
    ///     single child is returned unchanged.
    /// </summary>
    Intersection = 2,
}
