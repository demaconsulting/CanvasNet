using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A view-space triangle together with the per-triangle attributes needed to set it up for rasterization.
/// </summary>
/// <remarks>
///     Vertices are in <b>counter-clockwise outward</b> order as the mesh defines them; the
///     rasterizer decides from the projected winding whether the viewer sees the front or the back
///     and picks <see cref="FrontColor"/> or <see cref="BackColor"/> accordingly.
/// </remarks>
internal readonly struct ViewTriangle
{
    /// <summary>Gets the first vertex.</summary>
    public ClipVertex A { get; init; }

    /// <summary>Gets the second vertex.</summary>
    public ClipVertex B { get; init; }

    /// <summary>Gets the third vertex.</summary>
    public ClipVertex C { get; init; }

    /// <summary>Gets the draw priority (scene pre-order index of the owning instance).</summary>
    public int Priority { get; init; }

    /// <summary>Gets the shade used when the triangle is seen from its front (outside).</summary>
    public Rgba32 FrontColor { get; init; }

    /// <summary>Gets the shade used when the triangle is seen from behind.</summary>
    public Rgba32 BackColor { get; init; }

    /// <summary>Gets the CSG leaf (collapsed union-only subtree) the triangle belongs to; ignored by the z-buffer path.</summary>
    public int Leaf { get; init; }

    /// <summary>
    ///     Gets the CSG shade when the triangle is seen from its front and the visible event enters the
    ///     solid (the result surface faces the viewer); ignored by the z-buffer path.
    /// </summary>
    public Rgba32 FrontEnterColor { get; init; }

    /// <summary>
    ///     Gets the CSG shade when the triangle is seen from its front and the visible event leaves the
    ///     solid (the viewer is inside material); ignored by the z-buffer path.
    /// </summary>
    public Rgba32 FrontExitColor { get; init; }

    /// <summary>
    ///     Gets the CSG shade when the triangle is seen from behind and the visible event enters the
    ///     solid (a cut surface exposed by a subtrahend, shaded from the flipped normal); ignored by the
    ///     z-buffer path.
    /// </summary>
    public Rgba32 BackEnterColor { get; init; }

    /// <summary>
    ///     Gets the CSG shade when the triangle is seen from behind and the visible event leaves the
    ///     solid (the viewer is inside material); ignored by the z-buffer path.
    /// </summary>
    public Rgba32 BackExitColor { get; init; }

    /// <summary>
    ///     Gets the unit normal of the triangle's supporting plane in view space (zero when unknown); used only by
    ///     the fragment-list CSG path to recognize coincident faces of different meshes.
    /// </summary>
    public Vector3 PlaneNormal { get; init; }

    /// <summary>Gets a point on the triangle's supporting plane in view space (its centroid); used with <see cref="PlaneNormal"/>.</summary>
    public Vector3 PlanePoint { get; init; }
}
