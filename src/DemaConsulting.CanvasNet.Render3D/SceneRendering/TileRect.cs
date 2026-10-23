namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A rectangle of samples in the global sample grid, half-open: <c>[X0, X1) x [Y0, Y1)</c>.
/// </summary>
/// <remarks>Used to restrict rasterization to a single tile.</remarks>
/// <param name="X0">The first column (inclusive).</param>
/// <param name="Y0">The first row (inclusive).</param>
/// <param name="X1">The end column (exclusive).</param>
/// <param name="Y1">The end row (exclusive).</param>
internal readonly record struct TileRect(int X0, int Y0, int X1, int Y1)
{
    /// <summary>Gets the width in samples.</summary>
    public int Width => X1 - X0;

    /// <summary>Gets the height in samples.</summary>
    public int Height => Y1 - Y0;
}
