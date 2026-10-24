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
}
