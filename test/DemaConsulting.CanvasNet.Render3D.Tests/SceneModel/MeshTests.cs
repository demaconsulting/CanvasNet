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

    /// <summary>
    ///     Validates that the cached topology defects classify a closed, an open and a degenerate mesh, and that <see cref="Mesh.IsClosed"/> is consistent with them.
    /// </summary>
    [Fact]
    public void Mesh_TopologyDefects_ClassifiesClosedOpenAndDegenerate()
    {
        // Arrange
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var open = new Mesh(cube.Vertices, cube.TriangleIndices.Take(cube.TriangleIndices.Count - 3));
        var degenerate = new Mesh(cube.Vertices, cube.TriangleIndices.Concat([0, 0, 1]));
        var empty = new Mesh([], []);

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.None, cube.TopologyDefects);
        Assert.Equal(MeshTopologyDefects.None, cube.TopologyDefects);
        Assert.True(cube.IsClosed);
        Assert.Equal(MeshTopologyDefects.BoundaryEdge, open.TopologyDefects);
        Assert.False(open.IsClosed);
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, degenerate.TopologyDefects);
        Assert.False(degenerate.IsClosed);
        Assert.Equal(MeshTopologyDefects.None, empty.TopologyDefects);
        Assert.False(empty.IsClosed);
    }

    /// <summary>
    ///     Validates that an outward closed mesh is not inverted while its reversed copy is.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_ReversedClosedMesh_True()
    {
        // Arrange
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act / Assert
        Assert.False(cube.IsInverted);
        Assert.True(cube.Reverse().IsInverted);
        Assert.False(cube.Reverse().Reverse().IsInverted);
        Assert.False(Render3DTestHelpers.CreateTetrahedronMesh().IsInverted);
        Assert.True(Render3DTestHelpers.CreateTetrahedronMesh().Reverse().IsInverted);
    }

    /// <summary>
    ///     Validates that a hollow solid (an outward outer shell plus an inward inner shell) keeps a positive total volume and is not inverted.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_CavityMesh_False()
    {
        // Arrange: 10-cube with a reversed 4-cube cavity (volume 1000 - 64 > 0)
        var outer = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(10, 10, 10));
        var inner = Render3DTestHelpers.CreateBoxMesh(new Vector3D(3, 3, 3), new Vector3D(4, 4, 4)).Reverse();
        var hollow = new Mesh(
            outer.Vertices.Concat(inner.Vertices),
            outer.TriangleIndices.Concat(inner.TriangleIndices.Select(i => i + outer.VertexCount)));

        // Act / Assert
        Assert.True(hollow.IsClosed);
        Assert.Equal(936.0, hollow.SignedVolume, 1e-9);
        Assert.False(hollow.IsInverted);
    }

    /// <summary>
    ///     Validates that a valid shell plus a disjoint, non-nested inside-out shell is inverted although the total volume is positive.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_DisjointInvertedShell_True()
    {
        // Arrange: 4-cube plus a reversed unit cube at x = 5 (total volume 64 - 1 = 63 > 0)
        var mesh = Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(4, 4, 4)),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(5, 0, 0), Vector3D.One).Reverse());

        // Act / Assert
        Assert.True(mesh.IsClosed);
        Assert.Equal(63.0, mesh.SignedVolume, 1e-9);
        Assert.True(mesh.IsInverted);
        Assert.True(mesh.IsInverted);
    }

    /// <summary>
    ///     Validates that a disjoint outward shell next to a hollow solid does not disturb the hollow solid's validity.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_HollowSolidPlusDisjointSolid_False()
    {
        // Arrange
        var hollow = Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(10, 10, 10)),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(3, 3, 3), new Vector3D(4, 4, 4)).Reverse(),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(20, 0, 0), Vector3D.One));

        // Act / Assert
        Assert.False(hollow.IsInverted);
    }

    /// <summary>
    ///     Validates that three nested shells (solid, cavity, solid in the cavity) are valid and that a wrongly oriented level at any depth is inverted.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_NestedThreeLevelShells_JudgedByDepthParity()
    {
        // Arrange
        var outer = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10));
        var cavity = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-3, -3, -3), new Vector3D(6, 6, 6)).Reverse();
        var core = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));

        // Act / Assert: outward / inward / outward is valid; any other combination is not
        Assert.False(Render3DTestHelpers.Combine(outer, cavity, core).IsInverted);
        Assert.False(Render3DTestHelpers.Combine(core, outer, cavity).IsInverted);
        Assert.True(Render3DTestHelpers.Combine(outer, cavity, core.Reverse()).IsInverted);
        Assert.True(Render3DTestHelpers.Combine(outer, cavity.Reverse(), core).IsInverted);
        Assert.True(Render3DTestHelpers.Combine(outer.Reverse(), cavity, core).IsInverted);
    }

    /// <summary>
    ///     Validates that a closed mesh with unwelded duplicate vertices (36-vertex cube, STL style) is closed by welded
    ///     topology, free of defects and not inverted, and that its reversed form is inverted.
    /// </summary>
    [Fact]
    public void Mesh_IsClosed_UnweldedCube_ClosedByWeldedTopology()
    {
        // Arrange
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(2, 2, 2));
        var unwelded = Render3DTestHelpers.ToUnwelded(cube);

        // Act / Assert
        Assert.Equal(36, unwelded.VertexCount);
        Assert.True(unwelded.IsClosed);
        Assert.Equal(MeshTopologyDefects.None, unwelded.TopologyDefects);
        Assert.Equal(MeshTopologyDefects.None, unwelded.OperandDefects);
        Assert.False(unwelded.IsInverted);
        Assert.Equal(cube.SignedVolume, unwelded.SignedVolume, 1e-12);
        Assert.True(Render3DTestHelpers.ToUnwelded(cube.Reverse()).IsInverted);
    }

    /// <summary>
    ///     Validates that welding does not hide a real hole: an unwelded cube missing a triangle is still open.
    /// </summary>
    [Fact]
    public void Mesh_IsClosed_UnweldedOpenCube_False()
    {
        // Arrange
        var unwelded = Render3DTestHelpers.ToUnwelded(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));
        var open = new Mesh(unwelded.Vertices, unwelded.TriangleIndices.Take(unwelded.TriangleIndices.Count - 3));

        // Act / Assert
        Assert.False(open.IsClosed);
        Assert.Equal(MeshTopologyDefects.BoundaryEdge, open.TopologyDefects);
    }

    /// <summary>
    ///     Validates that vertices at the same position are welded before analysis: two triangles on coincident vertices
    ///     traverse the same edges in the same direction, and a triangle whose distinct vertices share a position is degenerate.
    /// </summary>
    [Fact]
    public void Mesh_TopologyDefects_CoincidentDistinctVertices_WeldedBeforeAnalysis()
    {
        // Arrange: vertices 0 and 3 coincide
        var mesh = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.Zero], [0, 1, 2, 3, 1, 2]);

        // Act / Assert
        Assert.True(mesh.TopologyDefects.HasFlag(MeshTopologyDefects.InconsistentWinding));
        Assert.False(mesh.IsClosed);
        var collapsed = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.Zero], [0, 1, 2]);
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, collapsed.TopologyDefects);
    }

    /// <summary>
    ///     Validates that a thin but valid solid has its orientation detected: a reversed 1 x 1 x 1e-12 box is inverted
    ///     and the correctly wound thin box is not (the criterion is scale-aware, not a fixed fraction of the bounds).
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_ThinBox_DetectedByOrientation()
    {
        // Arrange
        var thin = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1.0, 1.0, 1e-12));

        // Act / Assert
        Assert.True(thin.IsClosed);
        Assert.Equal(1e-12, thin.SignedVolume, 1e-15);
        Assert.False(thin.IsInverted);
        Assert.True(thin.Reverse().IsInverted);
        Assert.False(thin.Reverse().Reverse().IsInverted);
    }

    /// <summary>
    ///     Validates that a closed mesh with (numerically) zero volume is not reported as inverted, since its sign is only rounding noise.
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_ZeroVolumeClosedMesh_False()
    {
        // Arrange: a double-sided triangle (a closed surface with no enclosed volume) in both windings, and a
        // box flattened to zero thickness (which collapses onto itself when welded, so it is not even closed)
        var flat = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1, 2, 0, 2, 1]);
        var collapsedBox = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1.0, 1.0, 0.0));

        // Act / Assert
        Assert.True(flat.IsClosed);
        Assert.False(flat.IsInverted);
        Assert.False(flat.Reverse().IsInverted);
        Assert.False(collapsedBox.IsInverted);
        Assert.False(collapsedBox.Reverse().IsInverted);
    }

    /// <summary>
    ///     Validates that the operand defects add geometrically degenerate (zero-area) triangles to the topology defects
    ///     while <see cref="Mesh.IsClosed"/> stays purely topological.
    /// </summary>
    [Fact]
    public void Mesh_OperandDefects_CollinearFace_ReportsDegenerate()
    {
        // Arrange: a tetrahedron whose face (0, 2, 1) is collinear, plus a proper tetrahedron
        var collinear = new Mesh(
            [Vector3D.Zero, new Vector3D(1, 0, 0), new Vector3D(2, 0, 0), Vector3D.UnitZ],
            [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]);
        var proper = Render3DTestHelpers.CreateTetrahedronMesh();

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.None, collinear.TopologyDefects);
        Assert.True(collinear.IsClosed);
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, collinear.OperandDefects);
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, collinear.OperandDefects);
        Assert.Equal(MeshTopologyDefects.None, proper.OperandDefects);
    }

    /// <summary>
    ///     Validates that open and empty meshes are never reported as inverted (their volume is meaningless).
    /// </summary>
    [Fact]
    public void Mesh_IsInverted_OpenOrEmptyMesh_False()
    {
        // Arrange: a downward-facing open triangle and an empty mesh
        var open = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 2, 1]);

        // Act / Assert
        Assert.False(open.IsInverted);
        Assert.False(new Mesh([], []).IsInverted);
    }
}
