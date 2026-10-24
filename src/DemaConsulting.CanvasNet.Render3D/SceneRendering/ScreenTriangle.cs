using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A triangle prepared for rasterization: fixed-point sample-space vertices, winding, depths and shade.
/// </summary>
/// <remarks>
///     <para>
///         Coordinates are in the global <b>sample space</b> (output pixel coordinates multiplied by
///         the supersampling factor, y pointing down) in 24.8 fixed point: one sample is 256 units
///         and the centre of sample <c>(i, j)</c> is <c>(256 i + 128, 256 j + 128)</c>. Vertices are
///         normalized so <see cref="Area2"/> is positive (clockwise on a y-down screen), which the
///         rasterizer's edge functions and top-left rule assume. <see cref="BackFacing"/> records
///         whether the original winding showed the triangle's back.
///     </para>
///     <para>
///         Shading is flat, so the color of the visible side is precomputed once per triangle
///         (<see cref="Color"/>); the per-sample work is only the coverage and depth test.
///     </para>
/// </remarks>
internal readonly struct ScreenTriangle
{
    /// <summary>Gets vertex 0 X in 24.8 fixed point.</summary>
    public int X0 { get; init; }

    /// <summary>Gets vertex 0 Y in 24.8 fixed point.</summary>
    public int Y0 { get; init; }

    /// <summary>Gets vertex 1 X in 24.8 fixed point.</summary>
    public int X1 { get; init; }

    /// <summary>Gets vertex 1 Y in 24.8 fixed point.</summary>
    public int Y1 { get; init; }

    /// <summary>Gets vertex 2 X in 24.8 fixed point.</summary>
    public int X2 { get; init; }

    /// <summary>Gets vertex 2 Y in 24.8 fixed point.</summary>
    public int Y2 { get; init; }

    /// <summary>
    ///     Gets twice the signed area in fixed-point units squared
    ///     (<c>(x1-x0)(y2-y0) - (y1-y0)(x2-x0)</c>); always positive for a prepared triangle.
    /// </summary>
    public long Area2 { get; init; }

    /// <summary>Gets the view depth (distance along the view direction) at vertex 0.</summary>
    public float Depth0 { get; init; }

    /// <summary>Gets the view depth at vertex 1.</summary>
    public float Depth1 { get; init; }

    /// <summary>Gets the view depth at vertex 2.</summary>
    public float Depth2 { get; init; }

    /// <summary>Gets a value indicating whether depth is interpolated perspective-correctly (1/depth is linear on screen) rather than linearly.</summary>
    public bool PerspectiveDepth { get; init; }

    /// <summary>Gets the draw priority (the scene pre-order index of the owning mesh instance); lower wins coincident-depth ties.</summary>
    public int Priority { get; init; }

    /// <summary>Gets a value indicating whether the viewer sees the triangle's back side.</summary>
    public bool BackFacing { get; init; }

    /// <summary>Gets the opaque flat-shaded color of the visible side.</summary>
    public Rgba32 Color { get; init; }

    /// <summary>
    ///     Gets the largest rate of change of depth per sample step (the magnitude of the screen-space depth
    ///     gradient, in depth units per sample); used only by the fragment-list CSG path to size the tie
    ///     tolerance of coincident faces of different meshes, whose rounded vertices can differ by a
    ///     fixed-point quantum. Zero (the default) means "flat or unknown".
    /// </summary>
    public float DepthSlope { get; init; }

    /// <summary>Gets the CSG leaf (collapsed union-only subtree) the triangle belongs to; used only by the fragment-list CSG path.</summary>
    public int Leaf { get; init; }

    /// <summary>
    ///     Gets the shade used when this triangle is the visible surface where the CSG result changes
    ///     from outside to inside; used only by the fragment-list CSG path.
    /// </summary>
    public Rgba32 EnterColor { get; init; }

    /// <summary>
    ///     Gets the shade used when this triangle is the visible surface where the CSG result changes
    ///     from inside to outside (the viewer starts inside material); used only by the fragment-list CSG path.
    /// </summary>
    public Rgba32 ExitColor { get; init; }
}
