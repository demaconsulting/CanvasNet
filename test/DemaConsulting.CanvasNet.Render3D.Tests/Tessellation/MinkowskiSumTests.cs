// cspell:ignore Minkowski convexity coplanar chord octahedron pentagram welded ulp

using System.Diagnostics;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="MinkowskiSum"/>: analytic sums of convex solids, the soundness of the convexity
///     detection (non-convex operands are rejected, never approximated), argument validation and work bound,
///     determinism, validity of the result as a constructive-solid-geometry operand, and cancellation.
/// </summary>
public class MinkowskiSumTests
{
    /// <summary>Computes the sum with the test's cancellation token (as the test analyzers require).</summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The sum mesh.</returns>
    private static Mesh Sum(Mesh a, Mesh b) => MinkowskiSum.ConvexSum(a, b, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Gets the bit patterns of a mesh's vertices and its index list, so that two meshes can be compared
    ///     byte for byte.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The bits of every vertex component followed by the indices.</returns>
    private static long[] Fingerprint(Mesh mesh) =>
        mesh.Vertices.SelectMany(v => new[] { BitConverter.DoubleToInt64Bits(v.X), BitConverter.DoubleToInt64Bits(v.Y), BitConverter.DoubleToInt64Bits(v.Z) })
            .Concat(mesh.TriangleIndices.Select(i => (long)i))
            .ToArray();

    /// <summary>
    ///     Creates a unit cube whose bottom face is split into four triangles around a center vertex that is
    ///     pushed into the solid by <paramref name="depth"/>, which makes the solid slightly non-convex.
    /// </summary>
    /// <param name="depth">The depth of the dent.</param>
    /// <returns>The mesh (nine vertices, fourteen triangles).</returns>
    private static Mesh CubeWithDentedFace(double depth)
    {
        // Drop the two bottom triangles (the first six indices of the helper box) and fan the face around the center
        var box = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var vertices = box.Vertices.Append(new Vector3D(0.5, 0.5, depth)).ToList();
        var indices = box.TriangleIndices.Skip(6).ToList();
        int[] boundary = [0, 2, 3, 1];
        for (var i = 0; i < boundary.Length; i++)
        {
            indices.AddRange([boundary[i], boundary[(i + 1) % boundary.Length], 8]);
        }

        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Creates a closed, edge-manifold surface on the six vertices of an octahedron whose triangulation has
    ///     one edge flipped, so a chord through the interior replaces a convex edge.
    /// </summary>
    /// <returns>The chord-triangulated mesh.</returns>
    private static Mesh ChordTriangulatedOctahedron()
    {
        var octahedron = ConvexHull.Hull3D(
            [Vector3D.UnitX, -Vector3D.UnitX, Vector3D.UnitY, -Vector3D.UnitY, Vector3D.UnitZ, -Vector3D.UnitZ],
            TestContext.Current.CancellationToken);
        var indices = octahedron.TriangleIndices.ToArray();

        // The first triangle is (a, b, c); the other triangle across a-b traverses b -> a and has a third vertex d
        var a = indices[0];
        var b = indices[1];
        var c = indices[2];
        var other = -1;
        var d = -1;
        for (var t = 3; t < indices.Length && other < 0; t += 3)
        {
            for (var k = 0; k < 3; k++)
            {
                if (indices[t + k] == b && indices[t + ((k + 1) % 3)] == a)
                {
                    other = t;
                    d = indices[t + ((k + 2) % 3)];
                }
            }
        }

        // Flip the shared edge a-b into the chord c-d: (a,b,c) + (b,a,d) become (b,c,d) + (c,a,d)
        indices[0] = b;
        indices[1] = c;
        indices[2] = d;
        indices[other] = c;
        indices[other + 1] = a;
        indices[other + 2] = d;
        return new Mesh(octahedron.Vertices, indices);
    }

    /// <summary>
    ///     Creates a closed, edge-manifold cone over a pentagram whose apex fan winds twice around the axis, so
    ///     the apex is a double-wound vertex.
    /// </summary>
    /// <returns>The pentagram cone mesh.</returns>
    private static Mesh DoubleWoundCone()
    {
        var vertices = new List<Vector3D> { new(0, 0, 1) };
        for (var k = 0; k < 5; k++)
        {
            var angle = 2.0 * Math.PI * k / 5.0;
            vertices.Add(new Vector3D(Math.Cos(angle), Math.Sin(angle), 0));
        }

        // The star order visits the ring vertices 0, 2, 4, 1, 3; the side triangles follow it and the base closes it
        int[] star = [1, 3, 5, 2, 4];
        var indices = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            indices.AddRange([0, star[i], star[(i + 1) % 5]]);
        }

        indices.AddRange([star[0], star[2], star[1], star[0], star[3], star[2], star[0], star[4], star[3]]);
        return new Mesh(vertices, indices);
    }

    /// <summary>Creates a prism with the given counter-clockwise outline and an optional hole.</summary>
    /// <param name="height">The prism height.</param>
    /// <param name="outline">The outline of the cross section.</param>
    /// <param name="hole">The optional hole of the cross section.</param>
    /// <returns>The prism mesh.</returns>
    private static Mesh Prism(double height, Vector2D[] outline, Vector2D[]? hole = null) =>
        Extruder.Linear(new PolygonRegion(hole is null ? [outline] : [outline, hole]), height);

    /// <summary>
    ///     Creates the non-convex operand with the given case name for the rejection test.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <returns>The mesh.</returns>
    private static Mesh CreateNonConvex(string name) => name switch
    {
        "L-prism" => Prism(1.0, [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)]),
        "dented-cube" => CubeWithDentedFace(0.3),
        "ring" => Prism(1.0, [new(0, 0), new(4, 0), new(4, 4), new(0, 4)], [new(1, 1), new(1, 3), new(3, 3), new(3, 1)]),
        "two-disjoint-cubes" => Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(5, 0, 0), Vector3D.One)),
        "two-cubes-touching-at-an-edge" => Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 1, 0), Vector3D.One)),
        "two-cubes-touching-at-a-vertex" => Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 1, 1), Vector3D.One)),
        "chord-triangulated" => ChordTriangulatedOctahedron(),
        "double-wound-vertex" => DoubleWoundCone(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown case."),
    };

    /// <summary>
    ///     Validates that a box plus a box is the box with the summed edge lengths and the analytic volume.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_CubePlusCube_IsBoxWithAnalyticVolume()
    {
        // Arrange: a 2 x 3 x 4 box at (1,1,1) and a unit cube at (-3,5,0)
        var a = Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 1, 1), new Vector3D(2, 3, 4));
        var b = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-3, 5, 0), new Vector3D(1, 1, 1));

        // Act
        var sum = Sum(a, b);

        // Assert: the box from (-2,6,1) with edges 3 x 4 x 5
        Assert.Equal(8, sum.VertexCount);
        Assert.Equal(12, sum.TriangleCount);
        Assert.Equal(60.0, sum.SignedVolume, 10);
        Render3DTestHelpers.AssertVector(new Vector3D(-2, 6, 1), sum.Bounds.Min);
        Render3DTestHelpers.AssertVector(new Vector3D(1, 10, 6), sum.Bounds.Max);
    }

    /// <summary>
    ///     Validates that a cube plus a tessellated sphere is the rounded box: its volume lies between the box
    ///     with half-rounded faces and the analytic rounded-box volume, for several segment counts.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_CubePlusSphere_HasTheRoundedBoxVolume()
    {
        foreach (var segments in new[] { 8, 12, 16, 33, 64 })
        {
            // Arrange: a cube of side 10 and a sphere of radius 1
            var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(10, 10, 10));
            var sphere = PrimitiveTessellator.Sphere(1.0, segments);

            // Act
            var sum = Sum(cube, sphere);

            // Assert: exact mixed-volume formula of a cube plus a convex polytope S:
            // a^3 + a^2 (wx + wy + wz) + a (Ax + Ay + Az) + Vol(S), with the widths w and projected areas A of S
            var widths = sphere.Bounds.Max - sphere.Bounds.Min;
            var projectedX = 0.0;
            var projectedY = 0.0;
            var projectedZ = 0.0;
            var sphereIndices = sphere.TriangleIndices;
            for (var t = 0; t < sphereIndices.Count; t += 3)
            {
                var p = sphere.Vertices[sphereIndices[t]];
                var cross = Vector3D.Cross(sphere.Vertices[sphereIndices[t + 1]] - p, sphere.Vertices[sphereIndices[t + 2]] - p);
                projectedX += 0.25 * Math.Abs(cross.X);
                projectedY += 0.25 * Math.Abs(cross.Y);
                projectedZ += 0.25 * Math.Abs(cross.Z);
            }

            var expected = 1000.0 + (100.0 * (widths.X + widths.Y + widths.Z)) + (10.0 * (projectedX + projectedY + projectedZ)) + sphere.SignedVolume;
            Assert.Equal(expected, sum.SignedVolume, expected * 1e-9);

            // Assert: the rounded box lies between the box with slabs and the analytic rounded box
            var analytic = 1000.0 + (6.0 * 100.0) + (3.0 * Math.PI * 10.0) + (4.0 / 3.0 * Math.PI);
            Assert.True(sum.SignedVolume > 1600.0, $"{segments}: {sum.SignedVolume}");
            Assert.True(sum.SignedVolume <= analytic, $"{segments}: {sum.SignedVolume} > {analytic}");
            Render3DTestHelpers.AssertValidCsgOperand(sum);

            // Assert: the extents are the cube extents grown by the sphere extents on both sides
            Render3DTestHelpers.AssertVector(cube.Bounds.Min + sphere.Bounds.Min, sum.Bounds.Min, 1e-12);
            Render3DTestHelpers.AssertVector(cube.Bounds.Max + sphere.Bounds.Max, sum.Bounds.Max, 1e-12);
        }
    }

    /// <summary>
    ///     Validates that convex operands whose coordinates carry rounding noise (rotated and translated
    ///     solids, whose quads are no longer exactly planar) are accepted.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_RotatedConvexOperand_IsAccepted()
    {
        // Arrange: a cube rotated by 30 degrees about a skew axis and moved, and a mirrored, rotated sphere
        var rotation = Matrix4x4D.CreateFromAxisAngle(new Vector3D(1, 2, 3), 30.0) * Matrix4x4D.CreateTranslation(new Vector3D(100, -20, 7));
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(4, 5, 6)).Transform(rotation);
        var sphere = PrimitiveTessellator.Sphere(2.0, 24)
            .Transform(Matrix4x4D.CreateRotationX(25.0) * Matrix4x4D.CreateMirror(new Vector3D(1, 0, 0)));
        var cylinder = PrimitiveTessellator.Cylinder(3.0, 2.0, 1.0, 17).Transform(Matrix4x4D.CreateRotationY(40.0));

        // Act
        var sphereSum = Sum(cube, sphere);
        var cylinderSum = Sum(cylinder, cube);

        // Assert: valid, positive-volume solids that contain a translated copy of each operand's volume
        Render3DTestHelpers.AssertValidCsgOperand(sphereSum);
        Render3DTestHelpers.AssertValidCsgOperand(cylinderSum);
        Assert.True(sphereSum.SignedVolume > cube.SignedVolume + sphere.SignedVolume);
        Assert.True(cylinderSum.SignedVolume > cube.SignedVolume + cylinder.SignedVolume);
    }

    /// <summary>
    ///     Validates that the output of <see cref="ConvexHull"/> (which may contain flat vertices) is accepted as
    ///     an operand, and that its sum with a box is the exact box of the summed extents.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_HullOutputOperand_IsAccepted()
    {
        // Arrange: the hull of a lattice with many points on its faces and edges, and a box
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

        var hull = ConvexHull.Hull3D(lattice, TestContext.Current.CancellationToken);
        var box = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1, 2, 3));

        // Act
        var sum = Sum(hull, box);

        // Assert: a 4 x 5 x 6 box
        Assert.Equal(120.0, sum.SignedVolume, 9);
        Render3DTestHelpers.AssertValidCsgOperand(sum);
    }

    /// <summary>
    ///     Validates that non-convex operands of many kinds are rejected with <see cref="NotSupportedException"/>
    ///     rather than approximated, whichever side they are on.
    /// </summary>
    /// <param name="name">The name of the non-convex operand case.</param>
    [Theory]
    [InlineData("L-prism")]
    [InlineData("dented-cube")]
    [InlineData("ring")]
    [InlineData("two-disjoint-cubes")]
    [InlineData("two-cubes-touching-at-an-edge")]
    [InlineData("two-cubes-touching-at-a-vertex")]
    [InlineData("chord-triangulated")]
    [InlineData("double-wound-vertex")]
    public void MinkowskiSum_ConvexSum_NonConvexOperand_ThrowsNotSupported(string name)
    {
        // Arrange: the non-convex operand and a valid convex one
        var nonConvex = CreateNonConvex(name);
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);

        // Act / Assert: rejected in either position, and the message names the operand
        var first = Assert.Throws<NotSupportedException>(() => Sum(nonConvex, cube));
        var second = Assert.Throws<NotSupportedException>(() => Sum(cube, nonConvex));
        Assert.Contains("'a'", first.Message, StringComparison.Ordinal);
        Assert.Contains("'b'", second.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that open and inside-out operands are rejected with <see cref="NotSupportedException"/>.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_OpenOrInvertedOperand_ThrowsNotSupported()
    {
        // Arrange: a cube without its first two triangles (open), an inside-out cube and a cube with one flipped triangle
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var open = new Mesh(cube.Vertices, cube.TriangleIndices.Skip(6));
        var inverted = cube.Reverse();
        var indices = cube.TriangleIndices.ToArray();
        (indices[1], indices[2]) = (indices[2], indices[1]);
        var inconsistent = new Mesh(cube.Vertices, indices);
        var other = Render3DTestHelpers.CreateBoxMesh(new Vector3D(2, 2, 2), Vector3D.One);

        // Act / Assert
        Assert.Contains("open", Assert.Throws<NotSupportedException>(() => Sum(open, other)).Message, StringComparison.Ordinal);
        Assert.Contains("inside out", Assert.Throws<NotSupportedException>(() => Sum(other, inverted)).Message, StringComparison.Ordinal);
        Assert.Contains("wound", Assert.Throws<NotSupportedException>(() => Sum(inconsistent, other)).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the documented tolerance: a dent deeper than the relative tolerance (1e-9 of the hull volume)
    ///     is rejected, rounding-level noise is accepted.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_ToleranceBoundary_RejectsDentsAboveTauAndAcceptsRoundingNoise()
    {
        // Arrange: a unit cube whose face center is pushed in by the given depth (dent volume = depth / 3)
        var other = Render3DTestHelpers.CreateBoxMesh(new Vector3D(2, 2, 2), Vector3D.One);

        // Act / Assert: rounding-scale and sub-tolerance dents are accepted (the sum is the box of summed extents)
        foreach (var depth in new[] { 1e-13, 1e-10 })
        {
            var sum = Sum(CubeWithDentedFace(depth), other);
            Assert.Equal(8, sum.VertexCount);
            Assert.Equal(8.0, sum.SignedVolume, 9);
        }

        // Act / Assert: dents above 1e-9 of the volume are rejected
        foreach (var depth in new[] { 1e-8, 1e-6, 1e-3 })
        {
            Assert.Throws<NotSupportedException>(() => Sum(CubeWithDentedFace(depth), other));
        }

        // Assert: the tolerance is relative, so a dent of the same relative size on a cube 1000 times larger is accepted too
        var big = CubeWithDentedFace(1e-10).Transform(Matrix4x4D.CreateScale(1000.0));
        Assert.True(Sum(big, other).SignedVolume > 1e9);
    }

    /// <summary>
    ///     Validates the argument contract: null operands, operands without triangles and operands without
    ///     volume.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_NullEmptyOrFlatOperand_Throws()
    {
        // Arrange: a valid cube, an empty mesh and a flat double-sided triangle (closed, but without volume)
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var empty = new Mesh([], []);
        var flat = new Mesh([Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY], [0, 1, 2, 0, 2, 1]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Sum(null!, cube));
        Assert.Throws<ArgumentNullException>(() => Sum(cube, null!));
        Assert.Throws<ArgumentException>(() => Sum(empty, cube));
        Assert.Throws<ArgumentException>(() => Sum(cube, empty));
        Assert.Throws<ArgumentException>(() => Sum(flat, cube));
        Assert.Throws<ArgumentException>(() => Sum(cube, flat));
    }

    /// <summary>
    ///     Validates that vertex counts whose product exceeds <see cref="MinkowskiSum.MaxPairwiseSums"/> are rejected
    ///     immediately, before any convexity test or large allocation.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_TooManyPairwiseSums_ThrowsBeforeAllocating()
    {
        // Arrange: two spheres with about 1740 vertices each (a product of three million)
        var a = PrimitiveTessellator.Sphere(1.0, 60);
        var b = PrimitiveTessellator.Sphere(2.0, 60);
        Assert.True((long)a.VertexCount * b.VertexCount > MinkowskiSum.MaxPairwiseSums);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Sum(a, b));
        stopwatch.Stop();

        // Assert: a clear message, little allocation (far less than the 50 MB of the sums) and no hull computation
        Assert.Contains("pairwise sums", exception.Message, StringComparison.Ordinal);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 20_000_000);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal(2_097_152, MinkowskiSum.MaxPairwiseSums);
    }

    /// <summary>
    ///     Validates that the result is byte-identical when the operands are swapped, repeated, given with their
    ///     vertices in another order, or stored unwelded.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_OperandOrderAndRepeats_AreByteIdentical()
    {
        // Arrange: a sphere and a rotated box, plus the same solids with permuted vertex order and unwelded
        var sphere = PrimitiveTessellator.Sphere(2.0, 12);
        var box = Render3DTestHelpers.CreateBoxMesh(new Vector3D(1, 2, 3), new Vector3D(1, 2, 3)).Transform(Matrix4x4D.CreateRotationZ(20.0));
        var reference = Fingerprint(Sum(sphere, box));

        // Permute the sphere's vertices (and remap the indices)
        var random = new Random(3);
        var permutation = Enumerable.Range(0, sphere.VertexCount).OrderBy(_ => random.Next()).ToArray();
        var inverse = new int[permutation.Length];
        for (var i = 0; i < permutation.Length; i++)
        {
            inverse[permutation[i]] = i;
        }

        var permuted = new Mesh(permutation.Select(i => sphere.Vertices[i]), sphere.TriangleIndices.Select(i => inverse[i]));

        // Act / Assert
        Assert.Equal(reference, Fingerprint(Sum(box, sphere)));
        Assert.Equal(reference, Fingerprint(Sum(permuted, box)));
        Assert.Equal(reference, Fingerprint(Sum(Render3DTestHelpers.ToUnwelded(sphere), box)));
        Assert.Equal(reference, Fingerprint(Sum(sphere, box)));
    }

    /// <summary>
    ///     Validates that sums are closed, outward, positive-volume meshes that the renderer accepts as
    ///     constructive-solid-geometry operands.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_Results_AreClosedOutwardAndValidCsgOperands()
    {
        // Arrange: assorted convex pairs
        var tetra = Render3DTestHelpers.CreateTetrahedronMesh();
        var cube = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));
        var sphere = PrimitiveTessellator.Sphere(1.5, 16);
        var cone = PrimitiveTessellator.Cylinder(2.0, 1.0, 0.0, 9);

        foreach (var (first, second) in new[] { (tetra, cube), (cube, sphere), (sphere, cone), (cone, tetra), (tetra, tetra) })
        {
            // Act
            var sum = Sum(first, second);

            // Assert
            Render3DTestHelpers.AssertValidCsgOperand(sum);
            Render3DTestHelpers.AssertClosedAndOutward(sum);
        }
    }

    /// <summary>
    ///     Validates cancellation: an already canceled token throws <see cref="OperationCanceledException"/>
    ///     before any work is done.
    /// </summary>
    [Fact]
    public void MinkowskiSum_ConvexSum_Canceled_ThrowsOperationCanceledException()
    {
        // Arrange: valid operands and a canceled token
        var cube = Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One);
        var sphere = PrimitiveTessellator.Sphere(1.0, 48);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => MinkowskiSum.ConvexSum(cube, sphere, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => MinkowskiSum.ConvexSum(sphere, sphere, canceled.Token));
    }
}
