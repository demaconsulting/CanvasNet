// cspell:ignore coplanar collinear lexicographically canonicalized ulp ulps Caratheodory simplices simplex tetrahedra tetrahedron

using System.Diagnostics;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="ConvexHull"/> (<c>Hull3D</c> and <c>Hull2D</c>): analytic solids, an
///     independent brute-force oracle over degenerate lattice inputs, degenerate and invalid input,
///     determinism, validity as a constructive-solid-geometry operand, work bounds and cancellation.
/// </summary>
public class ConvexHullTests
{
    /// <summary>An integer lattice point used by the independent oracle (exact long arithmetic).</summary>
    /// <param name="X">The X coordinate.</param>
    /// <param name="Y">The Y coordinate.</param>
    /// <param name="Z">The Z coordinate.</param>
    private readonly record struct L(long X, long Y, long Z)
    {
        /// <summary>Gets the difference of two points.</summary>
        /// <param name="a">The minuend.</param>
        /// <param name="b">The subtrahend.</param>
        /// <returns>The difference vector.</returns>
        public static L operator -(L a, L b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Gets the cross product.</summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The cross product.</returns>
        public static L Cross(L a, L b) => new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

        /// <summary>Gets the dot product.</summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The dot product.</returns>
        public static long Dot(L a, L b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        /// <summary>Gets the determinant of three row vectors.</summary>
        /// <param name="a">The first row.</param>
        /// <param name="b">The second row.</param>
        /// <param name="c">The third row.</param>
        /// <returns>The determinant.</returns>
        public static long Det(L a, L b, L c) => Dot(a, Cross(b, c));
    }

    /// <summary>Converts a lattice point to the vector type of the production code.</summary>
    /// <param name="p">The lattice point.</param>
    /// <returns>The vector.</returns>
    private static Vector3D ToVector(L p) => new(p.X, p.Y, p.Z);

    /// <summary>Computes the 3D hull with the test's cancellation token (as the test analyzers require).</summary>
    /// <param name="points">The points.</param>
    /// <returns>The hull mesh.</returns>
    private static Mesh Hull3D(IEnumerable<Vector3D> points) => ConvexHull.Hull3D(points, TestContext.Current.CancellationToken);

    /// <summary>Computes the planar hull with the test's cancellation token (as the test analyzers require).</summary>
    /// <param name="points">The points.</param>
    /// <returns>The hull polygon.</returns>
    private static IReadOnlyList<Vector2D> Hull2D(IEnumerable<Vector2D> points) => ConvexHull.Hull2D(points, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Determines whether a point lies in a closed simplex (segment, triangle or tetrahedron) of affinely
    ///     independent lattice points, with exact integer arithmetic.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <param name="s">The two, three or four affinely independent simplex corners.</param>
    /// <returns><see langword="true"/> when the point is inside or on the boundary.</returns>
    private static bool InSimplex(L p, L[] s)
    {
        if (s.Length == 2)
        {
            return L.Cross(s[1] - s[0], p - s[0]) == default && L.Dot(p - s[0], p - s[1]) <= 0;
        }

        if (s.Length == 3)
        {
            var n = L.Cross(s[1] - s[0], s[2] - s[0]);
            return L.Dot(n, p - s[0]) == 0 &&
                   L.Dot(n, L.Cross(s[1] - s[0], p - s[0])) >= 0 &&
                   L.Dot(n, L.Cross(s[2] - s[1], p - s[1])) >= 0 &&
                   L.Dot(n, L.Cross(s[0] - s[2], p - s[2])) >= 0;
        }

        // A point is in a tetrahedron when it is on the same side of every face as the opposite corner
        for (var i = 0; i < 4; i++)
        {
            var f0 = s[(i + 1) % 4];
            var f1 = s[(i + 2) % 4];
            var f2 = s[(i + 3) % 4];
            var corner = Math.Sign(L.Det(f1 - f0, f2 - f0, s[i] - f0));
            var probe = Math.Sign(L.Det(f1 - f0, f2 - f0, p - f0));
            if (probe != 0 && probe != corner)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a lattice point is an extreme point of a set: it is not in the convex hull of any
    ///     affinely independent subset of at most four other points (Caratheodory), found by exhaustive search.
    /// </summary>
    /// <param name="p">The point to classify.</param>
    /// <param name="points">All distinct points (including <paramref name="p"/>).</param>
    /// <returns><see langword="true"/> when the point is a vertex of the hull.</returns>
    private static bool IsExtreme(L p, List<L> points)
    {
        var others = points.Where(q => q != p).ToList();
        var n = others.Count;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (InSimplex(p, [others[i], others[j]]))
                {
                    return false;
                }

                for (var k = j + 1; k < n; k++)
                {
                    var normal = L.Cross(others[j] - others[i], others[k] - others[i]);
                    if (normal == default)
                    {
                        continue;
                    }

                    if (InSimplex(p, [others[i], others[j], others[k]]))
                    {
                        return false;
                    }

                    for (var m = k + 1; m < n; m++)
                    {
                        if (L.Dot(normal, others[m] - others[i]) != 0 && InSimplex(p, [others[i], others[j], others[k], others[m]]))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a lattice point set spans three dimensions (contains four points with non-zero volume).
    /// </summary>
    /// <param name="points">The distinct points.</param>
    /// <returns><see langword="true"/> when some tetrahedron has a non-zero volume.</returns>
    private static bool SpansVolume(List<L> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                for (var k = j + 1; k < points.Count; k++)
                {
                    for (var m = k + 1; m < points.Count; m++)
                    {
                        if (L.Det(points[j] - points[i], points[k] - points[i], points[m] - points[i]) != 0)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Asserts the complete characterization of a convex hull: the mesh is a closed, positive-volume surface
    ///     with <c>F = 2V - 4</c>, every input point is on or below the plane of every triangle (so every
    ///     triangle is supporting), and every vertex is an input point.
    /// </summary>
    /// <param name="hull">The hull mesh.</param>
    /// <param name="points">The input points.</param>
    private static void AssertSupportingClosedSurface(Mesh hull, IReadOnlyCollection<Vector3D> points)
    {
        Assert.True(hull.IsClosed);
        Assert.True(hull.SignedVolume > 0.0);
        Assert.Equal((2 * hull.VertexCount) - 4, hull.TriangleCount);
        var set = points.ToHashSet();
        Assert.All(hull.Vertices, v => Assert.Contains(v, set));
        var indices = hull.TriangleIndices;
        for (var t = 0; t < indices.Count; t += 3)
        {
            var a = hull.Vertices[indices[t]];
            var b = hull.Vertices[indices[t + 1]];
            var c = hull.Vertices[indices[t + 2]];
            foreach (var point in points)
            {
                Assert.True(SolidPredicates.Orient3DExact(a, b, c, point) <= 0, "A point lies above a hull triangle.");
            }
        }
    }

    /// <summary>
    ///     Gets the bit patterns of a mesh's vertices and its index list, so that two meshes can be compared
    ///     byte for byte (distinguishing positive and negative zero).
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The bits of every vertex component followed by the indices.</returns>
    private static long[] Fingerprint(Mesh mesh) =>
        mesh.Vertices.SelectMany(v => new[] { BitConverter.DoubleToInt64Bits(v.X), BitConverter.DoubleToInt64Bits(v.Y), BitConverter.DoubleToInt64Bits(v.Z) })
            .Concat(mesh.TriangleIndices.Select(i => (long)i))
            .Append(mesh.VertexCount)
            .ToArray();

    /// <summary>Creates the eight corners of an axis-aligned cube.</summary>
    /// <param name="min">The minimum corner.</param>
    /// <param name="size">The edge length.</param>
    /// <returns>The corners.</returns>
    private static List<Vector3D> CubeCorners(Vector3D min, double size)
    {
        var corners = new List<Vector3D>();
        for (var i = 0; i < 8; i++)
        {
            corners.Add(new Vector3D(min.X + ((i & 1) * size), min.Y + (((i >> 1) & 1) * size), min.Z + (((i >> 2) & 1) * size)));
        }

        return corners;
    }

    /// <summary>
    ///     Validates that the hull of a cube's corners and many interior points is the closed box with
    ///     twelve triangles, exactly the corners as vertices and the analytic volume.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_CubeWithInteriorPoints_IsClosedBoxWithAnalyticVolume()
    {
        // Arrange: the corners of a 2 x 2 x 2 cube at (1,2,3) and 200 strictly interior points
        var random = new Random(11);
        var points = CubeCorners(new Vector3D(1, 2, 3), 2.0);
        for (var i = 0; i < 200; i++)
        {
            points.Add(new Vector3D(1.01 + (random.NextDouble() * 1.98), 2.01 + (random.NextDouble() * 1.98), 3.01 + (random.NextDouble() * 1.98)));
        }

        // Act
        var hull = Hull3D(points);

        // Assert
        Assert.Equal(8, hull.VertexCount);
        Assert.Equal(12, hull.TriangleCount);
        Assert.Equal(8.0, hull.SignedVolume, 12);
        Assert.Equal(24.0, hull.SurfaceArea, 12);
        Render3DTestHelpers.AssertClosedAndOutward(hull);
        Assert.Equal(new HashSet<Vector3D>(CubeCorners(new Vector3D(1, 2, 3), 2.0)), hull.Vertices.ToHashSet());
    }

    /// <summary>
    ///     Validates that four affinely independent points give a tetrahedron with four faces and the
    ///     analytic volume.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_Tetrahedron_HasFourFacesAndAnalyticVolume()
    {
        // Arrange: the unit-corner tetrahedron in a scrambled order
        Vector3D[] points = [Vector3D.UnitZ, Vector3D.UnitX, Vector3D.Zero, Vector3D.UnitY];

        // Act
        var hull = Hull3D(points);

        // Assert
        Assert.Equal(4, hull.VertexCount);
        Assert.Equal(4, hull.TriangleCount);
        Assert.Equal(1.0 / 6.0, hull.SignedVolume, 14);
        Render3DTestHelpers.AssertClosedAndOutward(hull);
    }

    /// <summary>
    ///     Validates that the hull of the vertices of a tessellated sphere has the volume of that sphere
    ///     polytope (which is convex), for several segment counts.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_SphereVertices_VolumeMatchesTheSpherePolytope()
    {
        foreach (var segments in new[] { 8, 12, 16, 33, 64 })
        {
            // Arrange: the vertices of the tessellated sphere
            var sphere = PrimitiveTessellator.Sphere(5.0, segments);

            // Act
            var hull = Hull3D(sphere.Vertices);

            // Assert: equal volume and area up to rounding, and a valid hull
            Assert.Equal(sphere.SignedVolume, hull.SignedVolume, sphere.SignedVolume * 1e-12);
            Assert.Equal(sphere.SurfaceArea, hull.SurfaceArea, sphere.SurfaceArea * 1e-12);
            Render3DTestHelpers.AssertClosedAndOutward(hull);
        }
    }

    /// <summary>
    ///     Validates that interior points, points in the middle of a face and points in the middle of an edge never
    ///     become vertices: the hull of a cube with such extra points is the plain twelve-triangle cube.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_InteriorAndFacetPoints_AreNotVertices()
    {
        // Arrange: a unit cube with its center, six face centers and twelve edge midpoints
        var corners = CubeCorners(Vector3D.Zero, 1.0);
        var points = new List<Vector3D>(corners) { new(0.5, 0.5, 0.5) };
        var flat = new List<Vector3D>();
        for (var axis = 0; axis < 3; axis++)
        {
            for (var side = 0; side < 2; side++)
            {
                var center = new[] { 0.5, 0.5, 0.5 };
                center[axis] = side;
                flat.Add(new Vector3D(center[0], center[1], center[2]));
            }
        }

        foreach (var a in corners)
        {
            foreach (var b in corners)
            {
                if (Math.Abs((a - b).LengthSquared - 1.0) < 1e-9 && a.X + a.Y + a.Z < b.X + b.Y + b.Z)
                {
                    flat.Add((a + b) / 2.0);
                }
            }
        }

        points.AddRange(flat);

        // Act
        var hull = Hull3D(points);

        // Assert: only the corners remain - the interior point, the face centers and the edge midpoints are not vertices
        Assert.Equal(8, hull.VertexCount);
        Assert.Equal(12, hull.TriangleCount);
        Assert.Equal(corners.ToHashSet(), hull.Vertices.ToHashSet());
        Assert.DoesNotContain(new Vector3D(0.5, 0.5, 0.5), hull.Vertices);
        Assert.Equal(1.0, hull.SignedVolume, 12);
        Assert.Equal(6.0, hull.SurfaceArea, 12);
        AssertSupportingClosedSurface(hull, points);
        Render3DTestHelpers.AssertValidCsgOperand(hull);
    }

    /// <summary>
    ///     Validates the hull against an independent exhaustive oracle on small integer lattices, where
    ///     collinear, coplanar and duplicate points are the rule: degenerate sets are rejected, and
    ///     otherwise the mesh is a closed surface whose every triangle supports all points and whose vertices
    ///     include exactly every extreme point that the Caratheodory search finds.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_RandomLatticeInputs_MatchTheBruteForceOracle()
    {
        // Arrange: seeded random subsets of 3x3x3 and 4x4x4 lattices, shuffled and with repeated points
        var random = new Random(424242);
        var degenerate = 0;
        var solid = 0;
        for (var testCase = 0; testCase < 420; testCase++)
        {
            var extent = testCase % 3 == 0 ? 3 : 4;
            var count = testCase < 400 ? random.Next(4, 15) : random.Next(24, 31);
            var cells = new List<L>();
            for (var x = 0; x < extent; x++)
            {
                for (var y = 0; y < extent; y++)
                {
                    for (var z = 0; z < extent; z++)
                    {
                        cells.Add(new L(x - 1, y, z));
                    }
                }
            }

            var distinct = cells.OrderBy(_ => random.Next()).Take(Math.Min(count, cells.Count)).ToList();

            // Every seventh case is restricted to one plane (a quarter of them to a line) to produce degenerate sets
            if (testCase % 7 == 0)
            {
                var flat = cells.Where(c => c.Z == 1).ToList();
                if (testCase % 4 == 0)
                {
                    flat = flat.Where(c => c.Y == 2).ToList();
                }

                distinct = flat.OrderBy(_ => random.Next()).Take(Math.Min(count, flat.Count)).ToList();
            }

            var input = distinct.Concat(distinct.Where(_ => random.Next(3) == 0)).OrderBy(_ => random.Next()).Select(ToVector).ToList();

            // Act / Assert
            if (!SpansVolume(distinct))
            {
                degenerate++;
                Assert.Throws<ArgumentException>(() => Hull3D(input));
                continue;
            }

            solid++;
            var hull = Hull3D(input);
            AssertSupportingClosedSurface(hull, input);
            var vertices = hull.Vertices.ToHashSet();
            foreach (var point in distinct.Where(p => IsExtreme(p, distinct)))
            {
                Assert.Contains(ToVector(point), vertices);
            }
        }

        // The scenario is meaningful only if both degenerate and full-dimensional sets occurred often
        Assert.True(degenerate >= 5, "Degenerate sets were expected.");
        Assert.True(solid >= 300, "Full-dimensional sets were expected.");
    }

    /// <summary>
    ///     Validates the degenerate prefixes of the sorted insertion: a collinear start, a coplanar start with
    ///     many points (the apex last), an apex that sorts first, and duplicated points.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_CollinearCoplanarAndDuplicatePrefix_IsHandled()
    {
        // Arrange / Act: a collinear prefix (four points on the Z axis) followed by off-line points
        var collinear = Hull3D(
        [
            new(0, 0, 0), new(0, 0, 1), new(0, 0, 2), new(0, 0, 3), new(1, 0, 0), new(0, 1, 1), new(2, 2, 2),
        ]);
        AssertSupportingClosedSurface(collinear, [new(0, 0, 0), new(0, 0, 3), new(1, 0, 0), new(0, 1, 1), new(2, 2, 2)]);
        Assert.Contains(new Vector3D(0, 0, 0), collinear.Vertices);
        Assert.Contains(new Vector3D(0, 0, 3), collinear.Vertices);

        // Arrange / Act: a 5x5 grid in the plane x = 0 (sorted first) and an apex last: a square pyramid
        var grid = new List<Vector3D>();
        for (var y = 0; y < 5; y++)
        {
            for (var z = 0; z < 5; z++)
            {
                grid.Add(new Vector3D(0, y, z));
            }
        }

        var pyramidLast = Hull3D([.. grid, new Vector3D(1, 2, 2)]);

        // Assert: five vertices, six triangles and volume base * height / 3 = 16 / 3
        Assert.Equal(5, pyramidLast.VertexCount);
        Assert.Equal(6, pyramidLast.TriangleCount);
        Assert.Equal(16.0 / 3.0, pyramidLast.SignedVolume, 12);
        Render3DTestHelpers.AssertValidCsgOperand(pyramidLast);

        // Arrange / Act: the apex sorts before the plane (x = -1 against x = 0), so the grid points are inserted
        // while they are still extreme; the canonical reduction must still give the plain pyramid
        var pyramidFirst = Hull3D([.. grid, new Vector3D(-1, 2, 2)]);
        Assert.Equal(16.0 / 3.0, pyramidFirst.SignedVolume, 12);
        Assert.Equal(5, pyramidFirst.VertexCount);
        Assert.Equal(6, pyramidFirst.TriangleCount);
        Render3DTestHelpers.AssertValidCsgOperand(pyramidFirst);

        // Arrange / Act: every point repeated three times gives the identical mesh
        var repeated = grid.Concat(grid).Concat(grid).Append(new Vector3D(1, 2, 2)).Append(new Vector3D(1, 2, 2)).ToList();
        Assert.Equal(Fingerprint(pyramidLast), Fingerprint(Hull3D(repeated)));

        // Arrange / Act: a coplanar prefix with the apex in the middle of the sort order (flat vertices stay valid)
        var middle = Hull3D([.. grid.Select(p => new Vector3D(p.Y, p.X + 1, p.Z)), new Vector3D(2, 0, 2)]);
        Assert.True(middle.IsClosed);
        Assert.Equal(16.0 / 3.0, middle.SignedVolume, 12);
    }

    /// <summary>
    ///     Validates that input spanning no volume (too few distinct points, collinear, coplanar) is rejected
    ///     with an <see cref="ArgumentException"/> naming the degenerate case.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_FewerThanFourOrFlatInput_ThrowsArgumentException()
    {
        // Arrange: degenerate sets of every kind
        var square = new List<Vector3D> { new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1) };
        var grid = new List<Vector3D>();
        for (var x = 0; x < 20; x++)
        {
            for (var y = 0; y < 20; y++)
            {
                grid.Add(new Vector3D(x, y, (2 * x) + (3 * y)));
            }
        }

        static List<Vector3D> P(params (double X, double Y, double Z)[] t) => t.Select(p => new Vector3D(p.X, p.Y, p.Z)).ToList();
        var cases = new List<List<Vector3D>>
        {
            P(),
            P((1, 2, 3)),
            P((1, 2, 3), (4, 5, 6)),
            P((1, 2, 3), (4, 5, 6), (7, 9, 1)),
            P((1, 2, 3), (1, 2, 3), (1, 2, 3), (1, 2, 3), (4, 5, 6), (7, 9, 1)),
            P((0, 0, 0), (1, 1, 1), (2, 2, 2), (3, 3, 3)),
            P((0, 0, 0), (1, 1, 1), (2, 2, 2), (3, 3, 3), (-1, -1, -1), (0.5, 0.5, 0.5)),
            square,
            grid,
        };

        foreach (var points in cases)
        {
            // Act
            var exception = Assert.Throws<ArgumentException>(() => Hull3D(points));

            // Assert: the message names the degenerate case
            Assert.Contains("degenerate", exception.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that a null sequence and non-finite coordinates are rejected by <c>Hull3D</c>.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_NullOrNonFiniteInput_Throws()
    {
        // Arrange: a valid tetrahedron with one coordinate replaced by each non-finite value
        Vector3D[] tetrahedron = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ];

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Hull3D(null!));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => Hull3D([.. tetrahedron, new Vector3D(bad, 0, 0)]));
            Assert.Throws<ArgumentException>(() => Hull3D([.. tetrahedron, new Vector3D(0, bad, 0)]));
            Assert.Throws<ArgumentException>(() => Hull3D([.. tetrahedron, new Vector3D(0, 0, bad)]));
        }
    }

    /// <summary>
    ///     Validates that a null sequence and non-finite coordinates are rejected by <c>Hull2D</c>.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_NullOrNonFiniteInput_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Hull2D(null!));
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => Hull2D([Vector2D.Zero, new Vector2D(bad, 1)]));
            Assert.Throws<ArgumentException>(() => Hull2D([Vector2D.Zero, new Vector2D(1, bad)]));
        }
    }

    /// <summary>
    ///     Validates that huge and tiny coordinates and points one unit in the last place off a face are
    ///     classified exactly.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_ExtremeMagnitudes_AreExact()
    {
        // Arrange / Act / Assert: cubes at the extremes of the supported range keep eight vertices and twelve triangles
        foreach (var size in new[] { 1e200, 1e-100, 3.0e-5 })
        {
            var corners = CubeCorners(new Vector3D(size, -size, 2 * size), size);
            var interior = new Vector3D(1.5 * size, -0.5 * size, 2.5 * size);
            var hull = Hull3D([.. corners, interior]);
            Assert.Equal(8, hull.VertexCount);
            Assert.Equal(12, hull.TriangleCount);
            Assert.True(hull.IsClosed);
        }

        // Arrange / Act / Assert: a point one ulp above the center of the top face is extreme, so the face becomes a pyramid
        var unit = CubeCorners(Vector3D.Zero, 1.0);
        var raised = new Vector3D(0.5, 0.5, Math.BitIncrement(1.0));
        var pyramid = Hull3D([.. unit, raised]);
        Assert.Equal(9, pyramid.VertexCount);
        Assert.Equal(14, pyramid.TriangleCount);
        Assert.Contains(raised, pyramid.Vertices);

        // Assert: one ulp below the face it is interior
        var sunken = new Vector3D(0.5, 0.5, Math.BitDecrement(1.0));
        Assert.Equal(8, Hull3D([.. unit, sunken]).VertexCount);

        // Assert: a point outside a huge cube by a tiny distance is extreme, one inside by a tiny distance is not
        var big = CubeCorners(Vector3D.Zero, 1e150);
        Assert.Equal(9, Hull3D([.. big, new Vector3D(-1e-150, 0.5e150, 0.5e150)]).VertexCount);
        Assert.Equal(8, Hull3D([.. big, new Vector3D(1e-150, 0.5e150, 0.5e150)]).VertexCount);
    }

    /// <summary>
    ///     Validates that any permutation and repetition of the same point set gives a byte-identical mesh.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_SamePointSetInAnyOrder_IsByteIdentical()
    {
        // Arrange: a point cloud with heavy degeneracy (lattice) and a random cloud
        var random = new Random(99);
        var lattice = new List<Vector3D>();
        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < 4; y++)
            {
                for (var z = 0; z < 4; z++)
                {
                    lattice.Add(new Vector3D(x, y, z));
                }
            }
        }

        var cloud = Enumerable.Range(0, 300).Select(_ => new Vector3D(random.NextDouble(), random.NextDouble() * 2, random.NextDouble() * 3)).ToList();

        foreach (var points in new[] { lattice, cloud })
        {
            var reference = Fingerprint(Hull3D(points));
            for (var attempt = 0; attempt < 12; attempt++)
            {
                // Act: a shuffled copy with some points repeated
                var shuffled = points.Concat(points.Where(_ => random.Next(4) == 0)).OrderBy(_ => random.Next()).ToList();
                var again = Fingerprint(Hull3D(shuffled));

                // Assert
                Assert.Equal(reference, again);
            }
        }
    }

    /// <summary>
    ///     Validates that negative zero and exact duplicates are canonicalized: the output contains no negative
    ///     zero and equals the hull of the clean set byte for byte.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_NegativeZeroAndDuplicates_AreCanonicalized()
    {
        // Arrange: a cube spanning the origin written with positive and negative zeros, plus duplicates
        var clean = CubeCorners(new Vector3D(0, 0, 0), 1.0);
        var noisy = new List<Vector3D>();
        foreach (var corner in clean)
        {
            noisy.Add(new Vector3D(corner.X == 0.0 ? -0.0 : 1.0, corner.Y == 0.0 ? -0.0 : 1.0, corner.Z == 0.0 ? -0.0 : 1.0));
            noisy.Add(corner);
        }

        // Act
        var expected = Hull3D(clean);
        var actual = Hull3D(noisy);

        // Assert: identical bits, hence no negative zero anywhere
        Assert.Equal(Fingerprint(expected), Fingerprint(actual));
        Assert.All(actual.Vertices, v =>
        {
            Assert.False(double.IsNegative(v.X) && v.X == 0.0);
            Assert.False(double.IsNegative(v.Y) && v.Y == 0.0);
            Assert.False(double.IsNegative(v.Z) && v.Z == 0.0);
        });
    }

    /// <summary>
    ///     Validates that the hull of the same planar set in any order and with repetitions is identical.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_SamePointSetInAnyOrder_IsIdentical()
    {
        // Arrange: random points on a small lattice (many collinear and duplicate points)
        var random = new Random(5);
        var points = Enumerable.Range(0, 200).Select(_ => new Vector2D(random.Next(0, 9), random.Next(0, 9))).ToList();
        var reference = Hull2D(points);

        for (var attempt = 0; attempt < 12; attempt++)
        {
            // Act
            var shuffled = points.Concat(points.Take(30)).OrderBy(_ => random.Next()).Append(new Vector2D(-0.0, 4)).ToList();
            var again = Hull2D(shuffled);

            // Assert: the same polygon (the extra point (0, 4) lies on the boundary, never a new vertex)
            Assert.Equal(reference, again);
        }
    }

    /// <summary>
    ///     Validates that results are closed, outward, positive-volume meshes that the renderer accepts as
    ///     constructive-solid-geometry operands, for assorted inputs.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_Results_AreClosedOutwardAndValidCsgOperands()
    {
        // Arrange: random clouds, a sphere's vertices, a lattice with flat vertices and a coplanar-prefix pyramid
        var random = new Random(31);
        var cloud = Enumerable.Range(0, 500).Select(_ => new Vector3D((random.NextDouble() * 10) - 5, random.NextDouble() * 4, (random.NextDouble() * 3) - 1)).ToList();
        var lattice = new List<Vector3D>();
        for (var x = 0; x < 5; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                for (var z = 0; z < 5; z++)
                {
                    lattice.Add(new Vector3D(x * 0.5, y * 0.25, z));
                }
            }
        }

        var sets = new List<IReadOnlyList<Vector3D>>
        {
            cloud,
            PrimitiveTessellator.Sphere(3.0, 20).Vertices,
            lattice,
            CubeCorners(new Vector3D(-1, -1, -1), 2.0),
            new Vector3D[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0), new(0.5, 0.5, 1) },
        };

        foreach (var points in sets)
        {
            // Act
            var hull = Hull3D(points);

            // Assert
            Render3DTestHelpers.AssertValidCsgOperand(hull);
            Render3DTestHelpers.AssertClosedAndOutward(hull);
        }
    }

    /// <summary>
    ///     Validates the monotone chain on a square with interior and collinear boundary points: the result is the
    ///     strictly convex counter-clockwise square starting at the lexicographically smallest corner.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_SquareWithInteriorAndCollinearPoints_IsStrictCounterClockwise()
    {
        // Arrange: corners, edge midpoints, the center and a scrambled order
        Vector2D[] points =
        [
            new(2, 2), new(1, 1), new(0, 2), new(1, 0), new(0, 0), new(2, 1), new(1, 2), new(0, 1), new(2, 0), new(0.5, 0.5), new(1.5, 1.2),
        ];

        // Act
        var hull = Hull2D(points);

        // Assert
        Assert.Equal([new Vector2D(0, 0), new Vector2D(2, 0), new Vector2D(2, 2), new Vector2D(0, 2)], hull);
    }

    /// <summary>
    ///     Validates that degenerate planar sets do not throw: nothing, one point (also repeated) or the two
    ///     extreme points of collinear input are returned.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_DegenerateInputs_ReturnThePointOrTheSegment()
    {
        // Act / Assert: empty, single and repeated points
        Assert.Empty(Hull2D([]));
        Assert.Equal([new Vector2D(3, 4)], Hull2D([new Vector2D(3, 4)]));
        Assert.Equal([new Vector2D(3, 4)], Hull2D([new Vector2D(3, 4), new Vector2D(3, 4), new Vector2D(3, 4)]));

        // Act / Assert: two points and collinear points give the extreme points, whatever the order
        Assert.Equal([new Vector2D(0, 0), new Vector2D(1, 1)], Hull2D([new Vector2D(1, 1), new Vector2D(0, 0)]));
        Assert.Equal(
            [new Vector2D(-1, -2), new Vector2D(3, 6)],
            Hull2D([new Vector2D(1, 2), new Vector2D(3, 6), new Vector2D(0, 0), new Vector2D(-1, -2), new Vector2D(2, 4)]));
        Assert.Equal([new Vector2D(0, 0), new Vector2D(0, 5)], Hull2D([new Vector2D(0, 3), new Vector2D(0, 5), new Vector2D(0, 0)]));
    }

    /// <summary>
    ///     Validates that nearly collinear points are decided with the exact predicate: a point one unit in the
    ///     last place off the line is a hull vertex on the correct side, and an exactly collinear one is not.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_NearlyCollinearPoints_UseTheExactPredicate()
    {
        // Arrange: the diagonal from (0.5, 0.5) to (24, 24) and a probe at its middle moved one ulp
        var start = new Vector2D(0.5, 0.5);
        var end = new Vector2D(24.0, 24.0);
        var below = new Vector2D(12.0, Math.BitDecrement(12.0));
        var above = new Vector2D(12.0, Math.BitIncrement(12.0));
        var on = new Vector2D(12.0, 12.0);

        // Act
        var withBelow = Hull2D([start, end, below]);
        var withAbove = Hull2D([start, end, above]);
        var withOn = Hull2D([start, end, on]);

        // Assert: a triangle counter-clockwise from the start (a point below the line comes before the end)
        Assert.Equal([start, below, end], withBelow);
        Assert.Equal([start, end, above], withAbove);
        Assert.Equal([start, end], withOn);
    }

    /// <summary>
    ///     Validates the work bound: a large random-plus-lattice input completes quickly with a closed surface
    ///     satisfying the Euler relation, and a huge coplanar prefix does not degrade the insertion.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_LargeInput_CompletesWithEulerCounts()
    {
        // Arrange: 200 000 points - random in a ball, a coarse lattice, and points on a sphere shell
        var random = new Random(2024);
        var points = new List<Vector3D>(200_000);
        while (points.Count < 150_000)
        {
            var p = new Vector3D((random.NextDouble() * 2) - 1, (random.NextDouble() * 2) - 1, (random.NextDouble() * 2) - 1);
            if (p.LengthSquared <= 1.0)
            {
                points.Add(p * 100.0);
            }
        }

        for (var x = -10; x <= 10; x++)
        {
            for (var y = -10; y <= 10; y++)
            {
                for (var z = -10; z <= 10; z++)
                {
                    points.Add(new Vector3D(x * 4.0, y * 4.0, z * 4.0).LengthSquared <= 10000.0 ? new Vector3D(x * 4.0, y * 4.0, z * 4.0) : Vector3D.Zero);
                }
            }
        }

        while (points.Count < 200_000)
        {
            var direction = new Vector3D(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
            if (direction.LengthSquared > 0.0)
            {
                points.Add(direction.Normalize() * 100.0);
            }
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var hull = Hull3D(points);
        stopwatch.Stop();

        // Assert: closed, Euler counts, a sample of the points inside every sampled triangle's plane
        Assert.True(hull.IsClosed);
        Assert.Equal((2 * hull.VertexCount) - 4, hull.TriangleCount);
        Assert.True(hull.SignedVolume > 0.0);
        var indices = hull.TriangleIndices;
        for (var t = 0; t < indices.Count; t += 3 * 7)
        {
            for (var s = 0; s < points.Count; s += 997)
            {
                Assert.True(SolidPredicates.Orient3D(hull.Vertices[indices[t]], hull.Vertices[indices[t + 1]], hull.Vertices[indices[t + 2]], points[s]) <= 0);
            }
        }

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"The hull took {stopwatch.Elapsed}.");

        // Arrange / Act / Assert: a 141 x 141 coplanar grid (20 000 points) with an apex last is a pyramid
        var grid = new List<Vector3D>();
        for (var y = 0; y < 141; y++)
        {
            for (var z = 0; z < 141; z++)
            {
                grid.Add(new Vector3D(0, y, z));
            }
        }

        grid.Add(new Vector3D(3, 70, 70));
        stopwatch.Restart();
        var pyramid = Hull3D(grid);
        Assert.Equal(5, pyramid.VertexCount);
        Assert.Equal(6, pyramid.TriangleCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The pyramid took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Validates cancellation of <c>Hull3D</c>: an already canceled token and a token canceled while the
    ///     input is being read both throw <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull3D_Canceled_ThrowsOperationCanceledException()
    {
        // Arrange: a valid input and a canceled token
        var points = CubeCorners(Vector3D.Zero, 1.0);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert: canceled before the call
        Assert.Throws<OperationCanceledException>(() => ConvexHull.Hull3D(points, canceled.Token));

        // Arrange / Act / Assert: canceled while a long lazy sequence is consumed (polled every few thousand points)
        using var during = new CancellationTokenSource();
        IEnumerable<Vector3D> Lazy()
        {
            for (var i = 0; i < 100_000; i++)
            {
                if (i == 10_000)
                {
                    during.Cancel();
                }

                yield return new Vector3D(i % 50, (i / 50) % 50, i / 2500);
            }
        }

        Assert.Throws<OperationCanceledException>(() => ConvexHull.Hull3D(Lazy(), during.Token));
    }

    /// <summary>
    ///     Validates cancellation of <c>Hull2D</c>: an already canceled token and a token canceled while the
    ///     input is being read both throw <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void ConvexHull_Hull2D_Canceled_ThrowsOperationCanceledException()
    {
        // Arrange: a canceled token
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert: canceled before the call
        Assert.Throws<OperationCanceledException>(() => ConvexHull.Hull2D([Vector2D.Zero, Vector2D.UnitX, Vector2D.UnitY], canceled.Token));

        // Arrange / Act / Assert: canceled while a long lazy sequence is consumed
        using var during = new CancellationTokenSource();
        IEnumerable<Vector2D> Lazy()
        {
            for (var i = 0; i < 100_000; i++)
            {
                if (i == 10_000)
                {
                    during.Cancel();
                }

                yield return new Vector2D(i % 300, i / 300);
            }
        }

        Assert.Throws<OperationCanceledException>(() => ConvexHull.Hull2D(Lazy(), during.Token));
    }
}
