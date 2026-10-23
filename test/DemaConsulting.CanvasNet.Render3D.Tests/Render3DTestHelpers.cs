using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests;

/// <summary>
///     Shared assertion and construction helpers for the Render3D test project: vector and
///     matrix comparisons with tolerances and (in later units) pixel counting, mesh orientation
///     checks and fragment-counting visitors.
/// </summary>
internal static class Render3DTestHelpers
{
    /// <summary>
    ///     Asserts that two vectors are component-wise equal within an absolute tolerance.
    /// </summary>
    /// <param name="expected">The expected vector.</param>
    /// <param name="actual">The actual vector.</param>
    /// <param name="tolerance">The absolute per-component tolerance.</param>
    public static void AssertVector(Vector3D expected, Vector3D actual, double tolerance = 1e-12)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Z, actual.Z, tolerance);
    }

    /// <summary>
    ///     Asserts that two matrices are element-wise equal within an absolute tolerance.
    /// </summary>
    /// <param name="expected">The expected matrix.</param>
    /// <param name="actual">The actual matrix.</param>
    /// <param name="tolerance">The absolute per-element tolerance.</param>
    public static void AssertMatrix(Matrix4x4D expected, Matrix4x4D actual, double tolerance = 1e-12)
    {
        Assert.Equal(expected.M11, actual.M11, tolerance);
        Assert.Equal(expected.M12, actual.M12, tolerance);
        Assert.Equal(expected.M13, actual.M13, tolerance);
        Assert.Equal(expected.M14, actual.M14, tolerance);
        Assert.Equal(expected.M21, actual.M21, tolerance);
        Assert.Equal(expected.M22, actual.M22, tolerance);
        Assert.Equal(expected.M23, actual.M23, tolerance);
        Assert.Equal(expected.M24, actual.M24, tolerance);
        Assert.Equal(expected.M31, actual.M31, tolerance);
        Assert.Equal(expected.M32, actual.M32, tolerance);
        Assert.Equal(expected.M33, actual.M33, tolerance);
        Assert.Equal(expected.M34, actual.M34, tolerance);
        Assert.Equal(expected.M41, actual.M41, tolerance);
        Assert.Equal(expected.M42, actual.M42, tolerance);
        Assert.Equal(expected.M43, actual.M43, tolerance);
        Assert.Equal(expected.M44, actual.M44, tolerance);
    }
}
