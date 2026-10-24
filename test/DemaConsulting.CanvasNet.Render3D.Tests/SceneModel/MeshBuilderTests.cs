using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneModel;

/// <summary>
///     Unit tests for <see cref="MeshBuilder"/> (Mesh unit).
/// </summary>
public class MeshBuilderTests
{
    /// <summary>
    ///     Validates that adding an identical position twice returns the same vertex index.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddVertex_IdenticalPoints_AreWelded()
    {
        // Arrange
        var builder = new MeshBuilder();

        // Act
        var first = builder.AddVertex(new Vector3D(1, 2, 3));
        var second = builder.AddVertex(new Vector3D(1, 2, 3));
        var third = builder.AddVertex(new Vector3D(1, 2, 3.0000001));

        // Assert: exact equality welds; a tiny difference does not
        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
        Assert.Equal(2, builder.VertexCount);
    }

    /// <summary>
    ///     Validates that negative zero welds with positive zero.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddVertex_NegativeZero_WeldsWithPositiveZero()
    {
        // Arrange
        var builder = new MeshBuilder();

        // Act
        var positive = builder.AddVertex(new Vector3D(0.0, 0.0, 0.0));
        var negative = builder.AddVertex(new Vector3D(-0.0, -0.0, -0.0));

        // Assert
        Assert.Equal(positive, negative);
        Assert.Throws<ArgumentException>(() => builder.AddVertex(new Vector3D(double.NaN, 0, 0)));
    }

    /// <summary>
    ///     Validates that a triangle with a repeated index is rejected.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddTriangle_RepeatedIndex_Throws()
    {
        // Arrange
        var builder = new MeshBuilder();
        var a = builder.AddVertex(Vector3D.Zero);
        var b = builder.AddVertex(Vector3D.UnitX);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.AddTriangle(a, b, a));
        Assert.Equal(0, builder.TriangleCount);
    }

    /// <summary>
    ///     Validates that an out-of-range triangle index is rejected.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddTriangle_IndexOutOfRange_Throws()
    {
        // Arrange
        var builder = new MeshBuilder();
        builder.AddVertex(Vector3D.Zero);
        builder.AddVertex(Vector3D.UnitX);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddTriangle(0, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddTriangle(-1, 0, 1));
    }

    /// <summary>
    ///     Validates that a quad polygon is fan-triangulated into two triangles.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddPolygon_Quad_FansToTwoTriangles()
    {
        // Arrange
        var builder = new MeshBuilder();
        var i0 = builder.AddVertex(new Vector3D(0, 0, 0));
        var i1 = builder.AddVertex(new Vector3D(1, 0, 0));
        var i2 = builder.AddVertex(new Vector3D(1, 1, 0));
        var i3 = builder.AddVertex(new Vector3D(0, 1, 0));

        // Act
        builder.AddPolygon([i0, i1, i2, i3]);
        var mesh = builder.Build();

        // Assert: (0,1,2) and (0,2,3)
        Assert.Equal(2, mesh.TriangleCount);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, mesh.TriangleIndices);
        Assert.Equal(1.0, mesh.SurfaceArea, 1e-12);
    }

    /// <summary>
    ///     Validates that a polygon with fewer than three indices is rejected.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddPolygon_TwoIndices_Throws()
    {
        // Arrange
        var builder = new MeshBuilder();
        builder.AddVertex(Vector3D.Zero);
        builder.AddVertex(Vector3D.UnitX);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => builder.AddPolygon([0, 1]));
    }

    /// <summary>
    ///     Validates that a polygon repeating a non-adjacent index is rejected atomically.
    /// </summary>
    [Fact]
    public void MeshBuilder_AddPolygon_RepeatedIndex_ThrowsAndLeavesBuilderUnchanged()
    {
        // Arrange
        var builder = new MeshBuilder();
        builder.AddVertex(Vector3D.Zero);
        builder.AddVertex(Vector3D.UnitX);
        builder.AddVertex(Vector3D.UnitY);
        builder.AddVertex(Vector3D.UnitZ);

        // Act / Assert: the fan (0,1,2),(0,2,1) would pass the per-triangle check
        Assert.Throws<ArgumentException>(() => builder.AddPolygon([0, 1, 2, 1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddPolygon([0, 1, 2, 9]));

        // Assert: no partial triangles were added
        Assert.Equal(0, builder.Build().TriangleCount);
    }

    /// <summary>
    ///     Validates that Build can be called repeatedly and each mesh is independent of later additions.
    /// </summary>
    [Fact]
    public void MeshBuilder_Build_CalledTwice_ReturnsIndependentMeshes()
    {
        // Arrange
        var builder = new MeshBuilder();
        builder.AddVertex(Vector3D.Zero);
        builder.AddVertex(Vector3D.UnitX);
        builder.AddVertex(Vector3D.UnitY);
        builder.AddTriangle(0, 1, 2);

        // Act
        var first = builder.Build();
        builder.AddVertex(Vector3D.UnitZ);
        builder.AddTriangle(0, 1, 3);
        var second = builder.Build();

        // Assert
        Assert.NotSame(first, second);
        Assert.Equal(1, first.TriangleCount);
        Assert.Equal(3, first.VertexCount);
        Assert.Equal(2, second.TriangleCount);
        Assert.Equal(4, second.VertexCount);
    }
}
