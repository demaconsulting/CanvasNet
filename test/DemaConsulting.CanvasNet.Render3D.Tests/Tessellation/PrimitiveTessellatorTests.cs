// cspell:ignore Tessellator Uncentered

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="PrimitiveTessellator"/>.
/// </summary>
public class PrimitiveTessellatorTests
{
    /// <summary>
    ///     Computes the area-correction factor of an inscribed regular n-gon relative to its circle.
    /// </summary>
    /// <param name="segments">The number of polygon sides.</param>
    /// <returns><c>n sin(2 pi / n) / (2 pi)</c>.</returns>
    private static double PolygonFactor(int segments) => segments * Math.Sin(2.0 * Math.PI / segments) / (2.0 * Math.PI);

    /// <summary>
    ///     Validates that a box has 12 triangles, 8 vertices and the analytic volume.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Box_Size_HasTwelveTrianglesAndAnalyticVolume()
    {
        // Arrange / Act
        var box = PrimitiveTessellator.Box(new Vector3D(2, 3, 4));

        // Assert
        Assert.Equal(12, box.TriangleCount);
        Assert.Equal(8, box.VertexCount);
        Assert.Equal(24.0, box.SignedVolume, 1e-12);
        Assert.Equal(2.0 * ((2 * 3) + (2 * 4) + (3 * 4)), box.SurfaceArea, 1e-12);
    }

    /// <summary>
    ///     Validates that a centered box is symmetric about the origin.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Box_Centered_IsSymmetricAboutOrigin()
    {
        // Arrange / Act
        var box = PrimitiveTessellator.Box(new Vector3D(2, 4, 6), center: true);

        // Assert
        Assert.Equal(new Vector3D(-1, -2, -3), box.Bounds.Min);
        Assert.Equal(new Vector3D(1, 2, 3), box.Bounds.Max);
    }

    /// <summary>
    ///     Validates that an non-centered box spans from the origin to its size.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Box_Uncentered_SpansZeroToSize()
    {
        // Arrange / Act
        var box = PrimitiveTessellator.Box(new Vector3D(2, 4, 6));

        // Assert
        Assert.Equal(Vector3D.Zero, box.Bounds.Min);
        Assert.Equal(new Vector3D(2, 4, 6), box.Bounds.Max);
    }

    /// <summary>
    ///     Validates that every sphere vertex lies on the requested radius.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Sphere_AllVerticesOnRadius()
    {
        // Arrange / Act
        var sphere = PrimitiveTessellator.Sphere(2.5, 24);

        // Assert
        foreach (var vertex in sphere.Vertices)
        {
            Assert.Equal(2.5, vertex.Length, 1e-12);
        }
    }

    /// <summary>
    ///     Validates that the sphere volume increases monotonically with segments, stays below the
    ///     analytic value and is within one percent at 48 segments.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Sphere_VolumeConvergesBelowAnalytic()
    {
        // Arrange
        var analytic = 4.0 / 3.0 * Math.PI;

        // Act
        var v12 = PrimitiveTessellator.Sphere(1.0, 12).SignedVolume;
        var v24 = PrimitiveTessellator.Sphere(1.0, 24).SignedVolume;
        var v48 = PrimitiveTessellator.Sphere(1.0, 48).SignedVolume;

        // Assert
        Assert.True(v12 < v24);
        Assert.True(v24 < v48);
        Assert.True(v48 < analytic);
        Assert.True(v48 > analytic * 0.99);
    }

    /// <summary>
    ///     Validates that the smallest sphere (3 segments) is still a closed outward mesh.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Sphere_MinimumSegments_IsClosed()
    {
        // Arrange / Act
        var sphere = PrimitiveTessellator.Sphere(1.0, 3);

        // Assert
        Render3DTestHelpers.AssertClosedAndOutward(sphere);
    }

    /// <summary>
    ///     Validates that the sphere has exact pole vertices at plus and minus the radius.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Sphere_PolesAtPlusMinusRadius()
    {
        // Arrange / Act
        var sphere = PrimitiveTessellator.Sphere(3.0, 16);

        // Assert
        Assert.Equal(3.0, sphere.Bounds.Max.Z);
        Assert.Equal(-3.0, sphere.Bounds.Min.Z);
        Assert.Contains(new Vector3D(0, 0, 3.0), sphere.Vertices);
        Assert.Contains(new Vector3D(0, 0, -3.0), sphere.Vertices);
    }

    /// <summary>
    ///     Validates the cylinder volume against the polygon-corrected analytic value.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Cylinder_VolumeMatchesPolygonCorrectedAnalytic()
    {
        // Arrange
        const int segments = 32;

        // Act
        var cylinder = PrimitiveTessellator.Cylinder(5.0, 2.0, 2.0, segments);

        // Assert: V = n sin(2pi/n)/(2pi) * pi r^2 h
        Assert.Equal(PolygonFactor(segments) * Math.PI * 4.0 * 5.0, cylinder.SignedVolume, 1e-9);
        Assert.Equal(4 * segments, cylinder.TriangleCount);
    }

    /// <summary>
    ///     Validates that a centered cylinder spans plus and minus half its height.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Cylinder_Centered_SpansPlusMinusHalfHeight()
    {
        // Arrange / Act
        var cylinder = PrimitiveTessellator.Cylinder(6.0, 1.0, 1.0, 12, center: true);

        // Assert
        Assert.Equal(-3.0, cylinder.Bounds.Min.Z, 1e-12);
        Assert.Equal(3.0, cylinder.Bounds.Max.Z, 1e-12);
    }

    /// <summary>
    ///     Validates the frustum volume against the analytic formula with polygon correction.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Cylinder_FrustumVolume_MatchesAnalytic()
    {
        // Arrange
        const int segments = 24;
        const double h = 4.0;
        const double r1 = 3.0;
        const double r2 = 1.0;

        // Act
        var frustum = PrimitiveTessellator.Cylinder(h, r1, r2, segments);

        // Assert: V = k * pi * h / 3 * (r1^2 + r1 r2 + r2^2)
        var expected = PolygonFactor(segments) * Math.PI * h / 3.0 * ((r1 * r1) + (r1 * r2) + (r2 * r2));
        Assert.Equal(expected, frustum.SignedVolume, 1e-9);
        Render3DTestHelpers.AssertClosedAndOutward(frustum);
    }

    /// <summary>
    ///     Validates that a zero radius yields a cone with a single apex vertex and no degenerate triangles.
    /// </summary>
    [Theory]
    [InlineData(2.0, 0.0)]
    [InlineData(0.0, 2.0)]
    public void PrimitiveTessellator_Cylinder_ZeroRadius_IsConeWithSingleApexAndNoDegenerateTriangles(double r1, double r2)
    {
        // Arrange
        const int segments = 16;

        // Act
        var cone = PrimitiveTessellator.Cylinder(3.0, r1, r2, segments);

        // Assert: ring + apex + cap centre; side + cap triangles; positive area everywhere; analytic volume
        Assert.Equal(segments + 2, cone.VertexCount);
        Assert.Equal(2 * segments, cone.TriangleCount);
        Render3DTestHelpers.AssertClosedAndOutward(cone);
        Assert.Equal(PolygonFactor(segments) * Math.PI * 4.0 * 3.0 / 3.0, cone.SignedVolume, 1e-9);
    }

    /// <summary>
    ///     Validates that a tetrahedron polyhedron is closed with the analytic volume.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_Tetrahedron_IsClosedWithAnalyticVolume()
    {
        // Arrange
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ];
        int[][] faces = [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]];

        // Act
        var mesh = PrimitiveTessellator.Polyhedron(points, faces);

        // Assert
        Assert.True(mesh.IsClosed);
        Assert.Equal(1.0 / 6.0, mesh.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that quad faces are fan-triangulated into two triangles each.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_QuadFaces_FanTriangulates()
    {
        // Arrange: a unit cube given as six outward counter-clockwise quads
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        int[][] faces = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];

        // Act
        var mesh = PrimitiveTessellator.Polyhedron(cube.Vertices, faces);

        // Assert
        Assert.Equal(12, mesh.TriangleCount);
        Assert.True(mesh.IsClosed);
        Assert.Equal(1.0, mesh.SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that an inverted polyhedron is reported by a negative volume and repaired by Reverse.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_InvertedWinding_HasNegativeVolumeAndReverseFixes()
    {
        // Arrange: the tetrahedron with every face listed clockwise from outside
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ];
        int[][] faces = [[0, 1, 2], [0, 3, 1], [0, 2, 3], [1, 3, 2]];

        // Act
        var mesh = PrimitiveTessellator.Polyhedron(points, faces);

        // Assert: orientation is the caller's job, so the volume is negative until reversed
        Assert.True(mesh.IsClosed);
        Assert.True(mesh.SignedVolume < 0.0);
        Assert.Equal(1.0 / 6.0, mesh.Reverse().SignedVolume, 1e-12);
    }

    /// <summary>
    ///     Validates that an open (unclosed) face set is built exactly as supplied: faces and winding are
    ///     preserved and the caller's responsibility for closure is not overridden.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_OpenFaceSet_PreservesSuppliedFacesAndWinding()
    {
        // Arrange: an outward unit cube with one face omitted, and a single face listed in the opposite sense to its natural one
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        int[][] openFaces = [[0, 2, 3, 1], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]];
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY];
        int[][] singleFace = [[0, 2, 1]];

        // Act
        var open = PrimitiveTessellator.Polyhedron(cube.Vertices, openFaces);
        var single = PrimitiveTessellator.Polyhedron(points, singleFace);

        // Assert: no face is added, so the mesh stays open with exactly the fan triangulation of the supplied faces
        Assert.Equal(10, open.TriangleCount);
        Assert.False(open.IsClosed);

        // Assert: the supplied winding is reproduced as given, not reoriented
        Assert.Equal([0, 2, 1], single.TriangleIndices);
        Assert.Equal(points, single.Vertices);
        Assert.Equal([0, 2, 3, 0, 3, 1], open.TriangleIndices.Take(6));
    }

    /// <summary>
    ///     Validates that non-positive or non-finite box sizes are rejected.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Box_NonPositiveSize_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Box(new Vector3D(0, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Box(new Vector3D(1, -1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Box(new Vector3D(1, 1, double.NaN)));
    }

    /// <summary>
    ///     Validates that fewer than three segments (or a bad radius) is rejected for spheres.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Sphere_TooFewSegments_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Sphere(1.0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Sphere(0.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Sphere(double.PositiveInfinity, 8));
    }

    /// <summary>
    ///     Validates that cylinders reject two zero radii, negative radii, bad heights and few segments.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Cylinder_BothRadiiZero_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Cylinder(1.0, 0.0, 0.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Cylinder(1.0, -1.0, 1.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Cylinder(0.0, 1.0, 1.0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Cylinder(1.0, 1.0, 1.0, 2));
    }

    /// <summary>
    ///     Validates that polyhedra reject bad indices, short faces and null arguments.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_BadIndexOrShortFace_Throws()
    {
        // Arrange
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY];

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 3]]));
        Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1]]));
        Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 1]]));
        Assert.Throws<ArgumentNullException>(() => PrimitiveTessellator.Polyhedron(null!, [[0, 1, 2]]));
        Assert.Throws<ArgumentNullException>(() => PrimitiveTessellator.Polyhedron(points, null!));
    }

    /// <summary>
    ///     Validates that a face repeating an input index is rejected wherever the repeat falls in the fan.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_RepeatedIndex_Throws()
    {
        // Arrange
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ];

        // Act
        var nonAdjacent = Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[1, 2, 3, 2]]));
        var firstAndLast = Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 2, 3, 0]]));
        var adjacent = Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 1, 2]]));

        // Assert
        Assert.Equal("facesCcw", nonAdjacent.ParamName);
        Assert.Equal("facesCcw", firstAndLast.ParamName);
        Assert.Equal("facesCcw", adjacent.ParamName);
        Assert.Contains("repeat", nonAdjacent.Message);
    }

    /// <summary>
    ///     Validates that a face is rejected when distinct indices address identical points that weld together.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_DistinctIndicesOfIdenticalPoints_Throws()
    {
        // Arrange: points 0 and 3 are identical and weld to one vertex
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.Zero];

        // Act
        var triangle = Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 3]]));
        var quad = Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polyhedron(points, [[0, 1, 2, 3]]));

        // Assert
        Assert.Equal("facesCcw", triangle.ParamName);
        Assert.Equal("facesCcw", quad.ParamName);
        Assert.Contains("weld", triangle.Message);
    }

    /// <summary>
    ///     Validates that a valid face set containing identical points on different faces is still accepted.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_Polyhedron_DuplicatePointsOnSeparateFaces_WeldsAndSucceeds()
    {
        // Arrange: index 4 duplicates index 0, but no single face uses both
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ, Vector3D.Zero];
        int[][] faces = [[0, 2, 1], [4, 1, 3], [0, 3, 2], [1, 2, 3]];

        // Act
        var mesh = PrimitiveTessellator.Polyhedron(points, faces);

        // Assert
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(4, mesh.TriangleCount);
        Assert.True(mesh.IsClosed);
    }

    /// <summary>
    ///     Validates that every convex primitive is closed and outward oriented.
    /// </summary>
    [Fact]
    public void PrimitiveTessellator_AllPrimitives_AreClosedAndOutwardOriented()
    {
        // Arrange / Act / Assert
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Box(new Vector3D(1, 2, 3)));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Box(new Vector3D(1, 2, 3), center: true));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Sphere(2.0, 3));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Sphere(2.0, 7));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Sphere(2.0, 32));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Cylinder(2.0, 1.0, 1.0, 3));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Cylinder(2.0, 1.0, 2.0, 9, center: true));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Cylinder(2.0, 0.0, 2.0, 9));
        Render3DTestHelpers.AssertClosedAndOutward(PrimitiveTessellator.Cylinder(2.0, 2.0, 0.0, 9));
    }
}
