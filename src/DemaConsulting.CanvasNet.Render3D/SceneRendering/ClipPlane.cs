namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A plane in view space, <c>A*X + B*Y + C*Z + D &gt;= 0</c> being the kept (inside) half-space.
/// </summary>
/// <remarks>
///     Both the near plane and the four guard-band side planes are linear in the view-space
///     coordinates (for perspective the homogeneous <c>w</c> is <c>-Z</c>), so one representation
///     serves every clip step.
/// </remarks>
/// <param name="A">The X coefficient.</param>
/// <param name="B">The Y coefficient.</param>
/// <param name="C">The Z coefficient.</param>
/// <param name="D">The constant term.</param>
internal readonly record struct ClipPlane(double A, double B, double C, double D)
{
    /// <summary>Computes the signed distance (up to scale) of a vertex; non-negative means inside.</summary>
    /// <param name="vertex">The vertex.</param>
    /// <returns><c>A*X + B*Y + C*Z + D</c>.</returns>
    public double Distance(in ClipVertex vertex) => (A * vertex.X) + (B * vertex.Y) + (C * vertex.Z) + D;
}
