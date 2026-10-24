// cspell:ignore Orientable

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

    /// <summary>
    ///     Validates that a closed cube, a multi-shell mesh and an empty index list have no defects.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_ClosedSurfaces_NoDefects()
    {
        // Arrange
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var twoCubes = cube.TriangleIndices.Concat(cube.TriangleIndices.Select(i => i + 8)).ToArray();

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.None, MeshTopology.Analyze(cube.IndexSpan));
        Assert.Equal(MeshTopologyDefects.None, MeshTopology.Analyze(twoCubes));
        Assert.Equal(MeshTopologyDefects.None, MeshTopology.Analyze([]));
    }

    /// <summary>
    ///     Validates that a lone triangle and a cube missing a triangle report boundary edges only.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_OpenSurface_BoundaryEdgeOnly()
    {
        // Arrange: one quad (open) and a cube without its last triangle
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var missing = cube.TriangleIndices.Take(cube.TriangleIndices.Count - 3).ToArray();

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.BoundaryEdge, MeshTopology.Analyze([0, 1, 2]));
        Assert.Equal(MeshTopologyDefects.BoundaryEdge, MeshTopology.Analyze([0, 1, 2, 0, 2, 3]));
        Assert.Equal(MeshTopologyDefects.BoundaryEdge, MeshTopology.Analyze(missing));
    }

    /// <summary>
    ///     Validates that an edge shared by three triangles is reported as non-manifold.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_EdgeSharedByThreeTriangles_NonManifold()
    {
        // Arrange: a "book" of three triangles hinged on edge 0-1
        int[] indices = [0, 1, 2, 1, 0, 3, 0, 1, 4];

        // Act
        var defects = MeshTopology.Analyze(indices);

        // Assert: the shared edge is non-manifold; the free edges are boundary edges
        Assert.True(defects.HasFlag(MeshTopologyDefects.NonManifoldEdge));
        Assert.True(defects.HasFlag(MeshTopologyDefects.BoundaryEdge));
        Assert.False(defects.HasFlag(MeshTopologyDefects.InconsistentWinding));
    }

    /// <summary>
    ///     Validates that two closed shells touching along one edge are reported as non-manifold.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_TwoShellsSharingAnEdge_NonManifold()
    {
        // Arrange: two cubes whose vertex sets are welded together along edge (0,1) of the first
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var second = cube.TriangleIndices.Select(i => i <= 1 ? i : i + 8).ToArray();
        var indices = cube.TriangleIndices.Concat(second).ToArray();

        // Act / Assert: the shared edge is traversed twice in each direction
        Assert.True(MeshTopology.Analyze(indices).HasFlag(MeshTopologyDefects.NonManifoldEdge));
    }

    /// <summary>
    ///     Validates that one flipped triangle is reported as inconsistent winding and nothing else.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_FlippedTriangle_InconsistentWindingOnly()
    {
        // Arrange: flip the first triangle of a cube
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = cube.TriangleIndices.ToArray();
        (indices[1], indices[2]) = (indices[2], indices[1]);

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.InconsistentWinding, MeshTopology.Analyze(indices));
    }

    /// <summary>
    ///     Validates that a repeated index is reported as a degenerate triangle without extra edge defects.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_RepeatedIndex_DegenerateTriangleOnly()
    {
        // Arrange: a closed cube with one collapsed triangle appended
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = cube.TriangleIndices.Concat([0, 0, 1]).ToArray();

        // Act / Assert
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, MeshTopology.Analyze([0, 0, 1]));
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, MeshTopology.Analyze(indices));
    }

    /// <summary>
    ///     Validates that triangles with distinct but coincident or collinear vertices are reported as degenerate
    ///     by the vertex-aware analysis (the criterion the renderer discards by), without extra edge defects.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_CollinearOrCoincidentVertices_DegenerateTriangleOnly()
    {
        // Arrange: a closed tetrahedron with face (0, 2, 1) collinear, one with two coincident vertices, and a proper one
        int[] indices = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
        Vector3D[] collinear = [Vector3D.Zero, new(1, 0, 0), new(2, 0, 0), Vector3D.UnitZ];
        Vector3D[] coincident = [Vector3D.Zero, Vector3D.Zero, Vector3D.UnitY, Vector3D.UnitZ];
        var proper = Render3DTestHelpers.CreateTetrahedronMesh();

        // Act / Assert: the index-only analysis cannot see it, the vertex-aware one reports exactly that defect
        Assert.Equal(MeshTopologyDefects.None, MeshTopology.Analyze(indices));
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, MeshTopology.Analyze(collinear, indices));
        Assert.Equal(MeshTopologyDefects.DegenerateTriangle, MeshTopology.Analyze(coincident, indices));
        Assert.Equal(MeshTopologyDefects.None, MeshTopology.Analyze(proper.VertexSpan, proper.IndexSpan));
    }

    /// <summary>
    ///     Validates the shared face-normal criterion: a proper triangle has a unit normal, zero-area, tiny and huge
    ///     triangles are decided by exact zero cross product, and an overflowing triangle has none.
    /// </summary>
    [Fact]
    public void MeshTopology_TryGetFaceNormal_ZeroAreaAndExtremeTriangles()
    {
        // Act / Assert
        Assert.True(MeshTopology.TryGetFaceNormal(Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, out var normal));
        Assert.Equal(Vector3D.UnitZ, normal);
        Assert.False(MeshTopology.TryGetFaceNormal(Vector3D.Zero, Vector3D.UnitX, new Vector3D(2, 0, 0), out _));
        Assert.False(MeshTopology.TryGetFaceNormal(Vector3D.UnitX, Vector3D.UnitX, Vector3D.UnitY, out _));
        Assert.True(MeshTopology.TryGetFaceNormal(Vector3D.Zero, new Vector3D(1e-150, 0, 0), new Vector3D(0, 1e-150, 0), out _));
        Assert.False(MeshTopology.TryGetFaceNormal(Vector3D.Zero, new Vector3D(1e200, 0, 0), new Vector3D(0, 1e200, 0), out _));
    }

    /// <summary>
    ///     Validates that the volume error bound is tiny relative to a thin box's volume (so its sign is trusted)
    ///     and covers the rounding noise of a zero-volume surface.
    /// </summary>
    [Fact]
    public void MeshTopology_SignedVolume_ErrorBound_ScalesWithMagnitude()
    {
        // Arrange
        var thin = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1.0, 1.0, 1e-12));
        var flat = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1.0, 1.0, 0.0));

        // Act
        var thinVolume = MeshTopology.SignedVolume(thin.VertexSpan, thin.IndexSpan, thin.Bounds.Center, out var thinBound);
        var flatVolume = MeshTopology.SignedVolume(flat.VertexSpan, flat.IndexSpan, flat.Bounds.Center, out var flatBound);

        // Assert
        Assert.True(thinBound > 0.0);
        Assert.True(thinVolume > 1000.0 * thinBound);
        Assert.True(Math.Abs(flatVolume) <= flatBound);
    }

    /// <summary>
    ///     Validates that independent defects combine as flags.
    /// </summary>
    [Fact]
    public void MeshTopology_Analyze_MultipleDefects_CombineFlags()
    {
        // Arrange: an open cube (missing triangle) with a flipped neighbor and a collapsed triangle
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = cube.TriangleIndices.Take(cube.TriangleIndices.Count - 3).ToArray();
        (indices[1], indices[2]) = (indices[2], indices[1]);
        indices = [.. indices, 1, 1, 1];

        // Act
        var defects = MeshTopology.Analyze(indices);

        // Assert
        Assert.True(defects.HasFlag(MeshTopologyDefects.BoundaryEdge));
        Assert.True(defects.HasFlag(MeshTopologyDefects.InconsistentWinding));
        Assert.True(defects.HasFlag(MeshTopologyDefects.DegenerateTriangle));
        Assert.False(defects.HasFlag(MeshTopologyDefects.NonManifoldEdge));
    }

    /// <summary>
    ///     Validates over a seeded corpus of random triangle soups that <see cref="MeshTopology.IsClosedOrientable"/>
    ///     (now expressed through <see cref="MeshTopology.Analyze(ReadOnlySpan{int})"/>) agrees with an independent directed-edge reference.
    /// </summary>
    [Fact]
    public void MeshTopology_IsClosed_RandomSoups_MatchesDirectedEdgeReference()
    {
        // Arrange: random soups plus (mutated) tetrahedra, so the closed branch is exercised too
        var random = new Random(20240607);
        var closedSeen = 0;
        for (var round = 0; round < 3000; round++)
        {
            int[] indices;
            if (round % 2 == 0)
            {
                var vertexCount = random.Next(3, 7);
                indices = new int[random.Next(1, 9) * 3];
                for (var i = 0; i < indices.Length; i++)
                {
                    indices[i] = random.Next(vertexCount);
                }
            }
            else
            {
                indices = [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3];
                if (round % 4 == 3)
                {
                    indices[random.Next(indices.Length)] = random.Next(5);
                }
            }

            // Act
            var expected = ReferenceIsClosed(indices);
            var actual = MeshTopology.IsClosedOrientable(indices);

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(expected, MeshTopology.Analyze(indices) == MeshTopologyDefects.None);
            closedSeen += expected ? 1 : 0;
        }

        // The corpus must exercise the closed branch too, or the comparison proves little
        Assert.True(closedSeen > 0);
    }

    /// <summary>
    ///     Validates that welding returns the very same index array for an already welded mesh and otherwise maps every
    ///     index to the first vertex at exactly the same position (negative and positive zero are the same position).
    /// </summary>
    [Fact]
    public void MeshTopology_Weld_MergesExactDuplicatesOnly()
    {
        // Arrange
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var indices = new[] { 0, 1, 2, 3, 4, 5 };
        var vertices = new[]
        {
            new Vector3D(0.0, 1.0, 2.0),
            new Vector3D(1.0, 1.0, 1.0),
            new Vector3D(-0.0, 1.0, 2.0),
            new Vector3D(0.0, 1.0, Math.BitIncrement(2.0)),
            new Vector3D(1.0, 1.0, 1.0),
            new Vector3D(5.0, 5.0, 5.0),
        };

        // Act
        var same = MeshTopology.Weld(cube.VertexSpan, [.. cube.TriangleIndices]);
        var welded = MeshTopology.Weld(vertices, indices);

        // Assert
        Assert.Equal([.. cube.TriangleIndices], same);
        Assert.Equal([0, 1, 0, 3, 1, 5], welded);
        Assert.Equal([0, 1, 2, 3, 4, 5], indices);
    }

    /// <summary>
    ///     Validates the shell-orientation analysis over a seeded corpus of concentric (and unwelded) shells: a mesh is
    ///     inverted exactly when some shell's orientation disagrees with its nesting depth parity.
    /// </summary>
    [Fact]
    public void MeshTopology_HasInvertedShell_ConcentricShells_MatchesDepthParity()
    {
        var random = new Random(20250611);
        var invertedSeen = 0;
        for (var round = 0; round < 200; round++)
        {
            // Arrange: 1 to 4 nested boxes (depth = nesting level) with random winding, optionally unwelded
            var levels = random.Next(1, 5);
            var shells = new List<Mesh>();
            var expectedInverted = false;
            for (var level = 0; level < levels; level++)
            {
                var half = 10.0 - (2.0 * level) - (random.NextDouble() * 0.5);
                var shell = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-half, -half * 0.9, -half * 1.1), new Vector3D(2 * half, 1.8 * half, 2.2 * half));
                var reversed = random.Next(2) == 0;
                expectedInverted |= reversed != (level % 2 == 1);
                shells.Add(reversed ? shell.Reverse() : shell);
            }

            // Disjoint extra shell elsewhere with its own random winding
            var extraReversed = random.Next(3) == 0;
            expectedInverted |= extraReversed;
            var extra = Render3DTestHelpers.CreateBoxMesh(new Vector3D(30, 0, 0), Vector3D.One);
            shells.Add(extraReversed ? extra.Reverse() : extra);
            var mesh = Render3DTestHelpers.Combine([.. shells]);
            var unwelded = Render3DTestHelpers.ToUnwelded(mesh);

            // Act
            var welded = MeshTopology.Weld(unwelded.VertexSpan, [.. unwelded.TriangleIndices]);
            var actual = MeshTopology.HasInvertedShell(mesh.VertexSpan, mesh.IndexSpan);
            var actualUnwelded = MeshTopology.HasInvertedShell(unwelded.VertexSpan, welded);

            // Assert
            Assert.Equal(expectedInverted, actual);
            Assert.Equal(expectedInverted, actualUnwelded);
            invertedSeen += expectedInverted ? 1 : 0;
        }

        // Both outcomes must occur or the comparison proves little
        Assert.InRange(invertedSeen, 1, 199);
    }

    /// <summary>
    ///     Independent reference: every directed edge exactly once and its reverse exactly once, with no repeated index.
    /// </summary>
    /// <param name="indices">The triangle index list.</param>
    /// <returns><see langword="true"/> when the list is a closed, consistently oriented surface.</returns>
    private static bool ReferenceIsClosed(int[] indices)
    {
        var counts = new Dictionary<(int, int), int>();
        for (var t = 0; t < indices.Length; t += 3)
        {
            for (var e = 0; e < 3; e++)
            {
                var a = indices[t + e];
                var b = indices[t + ((e + 1) % 3)];
                if (a == b)
                {
                    return false;
                }

                counts[(a, b)] = counts.GetValueOrDefault((a, b)) + 1;
            }
        }

        return counts.All(pair => pair.Value == 1 && counts.GetValueOrDefault((pair.Key.Item2, pair.Key.Item1)) == 1);
    }
}
