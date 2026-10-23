using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="MeshTopology"/> (Mesh unit).
/// </summary>
public class MeshTopologyTests
{
    /// <summary>
    ///     Validates that a cube's index list is a closed, consistently oriented surface.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_Cube_True()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act / Assert
        Assert.True(MeshTopology.IsClosedOrientable(mesh.IndexSpan));
    }

    /// <summary>
    ///     Validates that removing one triangle from a cube opens the surface.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_MissingTriangle_False()
    {
        // Arrange: drop the last triangle
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = mesh.TriangleIndices.Take(mesh.TriangleIndices.Count - 3).ToArray();

        // Act / Assert
        Assert.False(MeshTopology.IsClosedOrientable(indices));
    }

    /// <summary>
    ///     Validates that one flipped triangle (inconsistent winding) makes the surface not closed-orientable.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_InconsistentWinding_False()
    {
        // Arrange: flip the first triangle only
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = mesh.TriangleIndices.ToArray();
        (indices[1], indices[2]) = (indices[2], indices[1]);

        // Act / Assert
        Assert.False(MeshTopology.IsClosedOrientable(indices));
    }

    /// <summary>
    ///     Validates that three triangles sharing one edge are not a closed surface.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_EdgeSharedByThreeTriangles_False()
    {
        // Arrange: a "book" of three triangles hinged on edge 0-1
        int[] indices = [0, 1, 2, 1, 0, 3, 0, 1, 4];

        // Act / Assert
        Assert.False(MeshTopology.IsClosedOrientable(indices));
    }

    /// <summary>
    ///     Validates that a multi-shell mesh (two disjoint cubes) is closed.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_TwoDisjointCubes_True()
    {
        // Arrange: second cube's indices are offset by 8
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = cube.TriangleIndices.Concat(cube.TriangleIndices.Select(i => i + 8)).ToArray();

        // Act / Assert
        Assert.True(MeshTopology.IsClosedOrientable(indices));
    }

    /// <summary>
    ///     Validates that a degenerate triangle with a repeated index is not closed.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_DegenerateTriangle_False()
    {
        // Arrange / Act / Assert
        Assert.False(MeshTopology.IsClosedOrientable([0, 0, 1]));
        Assert.False(MeshTopology.IsClosedOrientable([]));
    }
}
