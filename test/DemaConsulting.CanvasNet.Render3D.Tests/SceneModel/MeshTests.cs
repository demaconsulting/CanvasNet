using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="Mesh"/> (Mesh unit).
/// </summary>
public class MeshTests
{
    /// <summary>
    ///     Validates that a valid single triangle reports its counts.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_ValidTriangle_SetsCounts()
    {
        // Arrange / Act
        var mesh = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1, 2]);

        // Assert
        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(1, mesh.TriangleCount);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(new[] { 0, 1, 2 }, mesh.TriangleIndices);
    }

    /// <summary>
    ///     Validates that an index count that is not a multiple of three is rejected.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_IndexCountNotMultipleOfThree_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1]));
    }

    /// <summary>
    ///     Validates that an out-of-range index is rejected.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_IndexOutOfRange_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1, 3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1, -1]));
    }

    /// <summary>
    ///     Validates that a non-finite vertex is rejected.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_NonFiniteVertex_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new Mesh([Vector3D.Zero, Vector3D.UnitX, new Vector3D(double.NaN, 0, 0)], [0, 1, 2]));
    }

    /// <summary>
    ///     Validates that null arguments are rejected.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_NullArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new Mesh(null!, [0, 1, 2]));
        Assert.Throws<ArgumentNullException>(() => new Mesh([Vector3D.Zero], null!));
    }

    /// <summary>
    ///     Validates that a mesh without triangles is allowed and has empty bounds.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_ZeroTriangles_AllowedWithEmptyBounds()
    {
        // Arrange / Act
        var mesh = new Mesh([], []);

        // Assert
        Assert.Equal(0, mesh.TriangleCount);
        Assert.True(mesh.Bounds.IsEmpty);
        Assert.False(mesh.IsClosed);
        Assert.Equal(0.0, mesh.SignedVolume);
        Assert.Equal(0.0, mesh.SurfaceArea);
    }

    /// <summary>
    ///     Validates that mutating the source collections after construction does not change the mesh.
    /// </summary>
    [Fact]
    public void Mesh_Constructor_MutatingSourceListsAfterwards_DoesNotAffectMesh()
    {
        // Arrange
        var vertices = new List<Vector3D> { Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY };
        var indices = new List<int> { 0, 1, 2 };
        var mesh = new Mesh(vertices, indices);

        // Act: mutate the sources
        vertices[1] = new Vector3D(100, 100, 100);
        indices[2] = 0;

        // Assert: the mesh kept its own private copies
        Assert.Equal(Vector3D.UnitX, mesh.Vertices[1]);
        Assert.Equal(2, mesh.TriangleIndices[2]);
        Assert.Equal(new Vector3D(1, 1, 0), mesh.Bounds.Max);
    }

    /// <summary>
    ///     Validates that the bounds of a cube equal its vertex extents.
    /// </summary>
    [Fact]
    public void Mesh_Bounds_Cube_MatchesVertexExtents()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, 2, 3), new Vector3D(2, 4, 6));

        // Act
        var bounds = mesh.Bounds;

        // Assert
        Assert.Equal(new Vector3D(-1, 2, 3), bounds.Min);
        Assert.Equal(new Vector3D(1, 6, 9), bounds.Max);
    }

    /// <summary>
    ///     Validates that a tetrahedron is a closed surface.
    /// </summary>
    [Fact]
    public void Mesh_IsClosed_Tetrahedron_True()
    {
        // Arrange / Act
        var mesh = Render3DTestHelpers.CreateTetrahedronMesh();

        // Assert
        Assert.True(mesh.IsClosed);
        Assert.True(mesh.IsClosed);
    }

    /// <summary>
    ///     Validates that the unit cube has signed volume one.
    /// </summary>
    [Fact]
    public void Mesh_SignedVolume_UnitCube_ReturnsOne()
    {
        // Arrange / Act
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Assert
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates the analytic volume 1/6 of the corner tetrahedron.
    /// </summary>
    [Fact]
    public void Mesh_SignedVolume_Tetrahedron_ReturnsAnalytic()
    {
        // Arrange / Act
        var mesh = Render3DTestHelpers.CreateTetrahedronMesh();

        // Assert
        Assert.Equal(1.0 / 6.0, mesh.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that the unit cube has surface area six.
    /// </summary>
    [Fact]
    public void Mesh_SurfaceArea_UnitCube_ReturnsSix()
    {
        // Arrange / Act
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Assert
        Assert.Equal(6.0, mesh.SurfaceArea, 1e-12);
    }

    /// <summary>
    ///     Validates that a translation moves the bounds and leaves the original unchanged.
    /// </summary>
    [Fact]
    public void Mesh_Transform_Translation_MovesBounds()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act
        var moved = mesh.Transform(Matrix4x4D.CreateTranslation(new Vector3D(10, 20, 30)));

        // Assert
        Assert.Equal(new Vector3D(10, 20, 30), moved.Bounds.Min);
        Assert.Equal(new Vector3D(11, 21, 31), moved.Bounds.Max);
        Assert.Equal(Vector3D.Zero, mesh.Bounds.Min);
        Assert.Equal(1.0, moved.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that a mirroring transform reverses winding so the volume stays positive and the mesh closed.
    /// </summary>
    [Fact]
    public void Mesh_Transform_MirrorScale_ReversesWindingAndKeepsPositiveVolume()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 1, 1), new Vector3D(2, 2, 2));

        // Act
        var mirrored = mesh.Transform(Matrix4x4D.CreateScale(new Vector3D(-1, 1, 1)));

        // Assert: bounds mirrored to x in [-3,-1]; volume stays 8 and positive; still closed
        Assert.Equal(-3.0, mirrored.Bounds.Min.X, 1e-12);
        Assert.Equal(8.0, mirrored.SignedVolume, 1e-12);
        Assert.True(mirrored.IsClosed);
        Assert.NotEqual(mesh.TriangleIndices[1], mirrored.TriangleIndices[1]);
    }

    /// <summary>
    ///     Validates that a transform producing a non-finite result is rejected.
    /// </summary>
    [Fact]
    public void Mesh_Transform_NonFiniteResult_Throws()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act / Assert: overflow to infinity and a NaN matrix both fail
        Assert.Throws<ArgumentException>(() => mesh.Transform(Matrix4x4D.CreateScale(double.MaxValue) * Matrix4x4D.CreateScale(double.MaxValue)));
        Assert.Throws<ArgumentException>(() => mesh.Transform(Matrix4x4D.CreateTranslation(new Vector3D(double.NaN, 0, 0))));
    }

    /// <summary>
    ///     Validates that reversing a closed mesh negates its signed volume and keeps it closed.
    /// </summary>
    [Fact]
    public void Mesh_Reverse_ClosedMesh_NegatesSignedVolume()
    {
        // Arrange
        var mesh = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act
        var reversed = mesh.Reverse();

        // Assert
        Assert.Equal(-1.0, reversed.SignedVolume, 1e-12);
        Assert.True(reversed.IsClosed);
        Assert.Equal(1.0, reversed.Reverse().SignedVolume, 1e-12);
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that the signed volume is precise for a mesh far from the origin.
    /// </summary>
    [Fact]
    public void Mesh_SignedVolume_FarFromOrigin_RetainsPrecision()
    {
        // Arrange: a unit cube one million units from the origin
        var mesh = Render3DTestHelpers.CreateBoxMesh(new Vector3D(1e6, 1e6, 1e6), Vector3D.One);

        // Act / Assert: computing relative to the bounds center keeps the result exact
        Assert.Equal(1.0, mesh.SignedVolume, 1e-9);
    }
}
