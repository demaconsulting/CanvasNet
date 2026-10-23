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
    ///     Creates an axis-aligned box mesh (8 vertices, 12 triangles, outward counter-clockwise
    ///     winding) without using the production tessellator, so mesh tests stay independent.
    /// </summary>
    /// <param name="min">The minimum corner.</param>
    /// <param name="size">The edge lengths along X, Y and Z.</param>
    /// <returns>The box mesh.</returns>
    public static Mesh CreateBoxMesh(Vector3D min, Vector3D size)
    {
        // Vertex i has x at bit 0, y at bit 1 and z at bit 2 of i
        var vertices = new Vector3D[8];
        for (var i = 0; i < 8; i++)
        {
            vertices[i] = new Vector3D(
                min.X + (((i & 1) != 0) ? size.X : 0.0),
                min.Y + (((i & 2) != 0) ? size.Y : 0.0),
                min.Z + (((i & 4) != 0) ? size.Z : 0.0));
        }

        int[] indices =
        [
            0, 2, 1, 1, 2, 3, // -Z
            4, 5, 7, 4, 7, 6, // +Z
            0, 1, 5, 0, 5, 4, // -Y
            2, 7, 3, 2, 6, 7, // +Y
            0, 4, 6, 0, 6, 2, // -X
            1, 3, 7, 1, 7, 5, // +X
        ];
        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Creates the unit-corner tetrahedron <c>(0,0,0) (1,0,0) (0,1,0) (0,0,1)</c> with
    ///     outward counter-clockwise winding (volume 1/6).
    /// </summary>
    /// <returns>The tetrahedron mesh.</returns>
    public static Mesh CreateTetrahedronMesh() =>
        new(
            [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ],
            [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]);

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
