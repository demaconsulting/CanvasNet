namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Receives the samples a triangle covers, one call per covered sample, from
///     <see cref="TriangleRasterizer.Rasterize{TVisitor}"/>.
/// </summary>
/// <remarks>
///     The rasterizer is generic over a <see langword="struct"/> visitor so the call is resolved and
///     inlined at JIT time (no interface dispatch in the per-sample loop). The depth test visitor
///     used for rendering and the counting visitor used by tests both implement this interface.
/// </remarks>
internal interface IFragmentVisitor
{
    /// <summary>
    ///     Called for every sample whose centre is covered by the triangle (after the top-left
    ///     rule has been applied).
    /// </summary>
    /// <param name="sampleX">The sample column in the global sample grid.</param>
    /// <param name="sampleY">The sample row in the global sample grid.</param>
    /// <param name="depth">The interpolated view-space depth (distance along the view direction) at the sample centre.</param>
    void Visit(int sampleX, int sampleY, float depth);
}
