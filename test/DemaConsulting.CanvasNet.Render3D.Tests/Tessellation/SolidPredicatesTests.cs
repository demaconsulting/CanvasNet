// cspell:ignore subnormal subnormals coplanar ulps ulp

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for the exact orientation predicate <see cref="SolidPredicates.Orient3D"/>.
/// </summary>
public class SolidPredicatesTests
{
    /// <summary>
    ///     Computes the exact orientation sign of integer-lattice points (multiples of 2^-18 below 64) with
    ///     128-bit integers, independently of the production implementation.
    /// </summary>
    /// <param name="a">The first plane point.</param>
    /// <param name="b">The second plane point.</param>
    /// <param name="c">The third plane point.</param>
    /// <param name="d">The probe point.</param>
    /// <returns>The exact orientation sign.</returns>
    private static int ReferenceOrientation(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        static Int128 Scaled(double value) => (Int128)Math.ScaleB(value, 18);

        var ux = Scaled(b.X) - Scaled(a.X);
        var uy = Scaled(b.Y) - Scaled(a.Y);
        var uz = Scaled(b.Z) - Scaled(a.Z);
        var vx = Scaled(c.X) - Scaled(a.X);
        var vy = Scaled(c.Y) - Scaled(a.Y);
        var vz = Scaled(c.Z) - Scaled(a.Z);
        var wx = Scaled(d.X) - Scaled(a.X);
        var wy = Scaled(d.Y) - Scaled(a.Y);
        var wz = Scaled(d.Z) - Scaled(a.Z);
        var determinant = (ux * ((vy * wz) - (vz * wy))) - (uy * ((vx * wz) - (vz * wx))) + (uz * ((vx * wy) - (vy * wx)));
        return determinant.CompareTo(Int128.Zero);
    }

    /// <summary>
    ///     Creates a random point with coordinates that are multiples of 2^-16 below 64 (so the reference
    ///     evaluation is exact).
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <returns>The point.</returns>
    private static Vector3D RandomLatticePoint(Random random) =>
        new(random.Next(-4_194_304, 4_194_304) / 65536.0, random.Next(-4_194_304, 4_194_304) / 65536.0, random.Next(-4_194_304, 4_194_304) / 65536.0);

    /// <summary>
    ///     Validates that the filtered predicate always returns the exact sign for random inputs at many
    ///     scales, including inputs designed to be coplanar and nearly coplanar.
    /// </summary>
    [Fact]
    public void SolidPredicates_Orient3D_RandomInputs_MatchesTheExactSign()
    {
        // Arrange: a seeded generator so the test is deterministic
        var random = new Random(20260101);
        var coplanarSeen = 0;

        for (var i = 0; i < 6000; i++)
        {
            // Act: general random doubles at a random scale, against the arbitrary-precision evaluation
            var scale = Math.Pow(10.0, random.Next(-30, 30));
            Vector3D Next() => new((random.NextDouble() - 0.5) * scale, (random.NextDouble() - 0.5) * scale, (random.NextDouble() - 0.5) * scale);
            var a = Next();
            var b = Next();
            var c = Next();
            var d = Next();
            Assert.Equal(SolidPredicates.Orient3DExact(a, b, c, d), SolidPredicates.Orient3D(a, b, c, d));

            // Act: lattice points, whose sign is also known from the independent 128-bit reference, with a
            // probe on the plane (an affine combination of integers, exactly representable) every third time
            var p = RandomLatticePoint(random);
            var q = RandomLatticePoint(random);
            var r = RandomLatticePoint(random);
            var s = RandomLatticePoint(random);
            if (i % 3 == 0)
            {
                s = p + (0.5 * (q - p)) + (0.25 * (r - p));
            }

            var expected = ReferenceOrientation(p, q, r, s);
            if (expected == 0)
            {
                coplanarSeen++;
            }

            // Assert
            Assert.Equal(expected, SolidPredicates.Orient3D(p, q, r, s));
        }

        // The scenario is meaningful only if coplanar cases really occurred
        Assert.True(coplanarSeen > 1000, "Exactly coplanar probes were expected.");
    }

    /// <summary>
    ///     Validates that probes a few units in the last place off a plane that is not axis aligned get the
    ///     exact sign, which a plain double determinant gets wrong for some of them.
    /// </summary>
    [Fact]
    public void SolidPredicates_Orient3D_NearlyCoplanar_ReturnsTheExactSign()
    {
        // Arrange: the plane through three non-axis-aligned points, and probes at the centroid moved by ulps
        var a = new Vector3D(0.1, 0.2, 0.3);
        var b = new Vector3D(12.1, 7.3, 4.9);
        var c = new Vector3D(3.7, 15.2, 9.1);
        var centroid = (a + b + c) / 3.0;
        var naiveMistakes = 0;

        for (var i = -16; i <= 16; i++)
        {
            var z = centroid.Z;
            for (var step = 0; step < Math.Abs(i); step++)
            {
                z = i > 0 ? Math.BitIncrement(z) : Math.BitDecrement(z);
            }

            var d = new Vector3D(centroid.X, centroid.Y, z);

            // Act
            var actual = SolidPredicates.Orient3D(a, b, c, d);
            var exact = SolidPredicates.Orient3DExact(a, b, c, d);
            var naive = Math.Sign(Vector3D.Dot(Vector3D.Cross(b - a, c - a), d - a));

            // Assert: the exact sign; the sign relative to the plane also follows the sign of the offset
            // once it is far enough from the rounded centroid
            Assert.Equal(exact, actual);
            if (naive != exact)
            {
                naiveMistakes++;
            }
        }

        // The scenario is meaningful only if the naive evaluation really fails here
        Assert.True(naiveMistakes > 0, "The naive determinant was expected to misjudge at least one case.");
    }

    /// <summary>
    ///     Validates that extreme magnitudes (overflowing differences, subnormal offsets, huge coordinates)
    ///     take the exact path and still give the exact sign.
    /// </summary>
    [Fact]
    public void SolidPredicates_Orient3D_ExtremeMagnitudes_FallBackToExact()
    {
        // Arrange: a huge triangle with a probe a subnormal distance above and below its plane
        var origin = Vector3D.Zero;
        var b = new Vector3D(1e300, 0.0, 0.0);
        var c = new Vector3D(0.0, 1e300, 0.0);

        // Act / Assert: the determinant is about 1e600 times the offset, far outside the double range
        Assert.Equal(1, SolidPredicates.Orient3D(origin, b, c, new Vector3D(1.0, 1.0, double.Epsilon)));
        Assert.Equal(-1, SolidPredicates.Orient3D(origin, b, c, new Vector3D(1.0, 1.0, -double.Epsilon)));
        Assert.Equal(0, SolidPredicates.Orient3D(origin, b, c, new Vector3D(1e300, 1e300, 0.0)));

        // Act / Assert: overflowing differences (the extremes of the double range) still agree with the exact value
        var huge = double.MaxValue;
        var p = new Vector3D(-huge, -huge, 0.0);
        var q = new Vector3D(huge, -huge, 0.0);
        var r = new Vector3D(-huge, huge, 0.0);
        Assert.Equal(1, SolidPredicates.Orient3D(p, q, r, new Vector3D(0.0, 0.0, double.Epsilon)));
        Assert.Equal(-1, SolidPredicates.Orient3D(p, q, r, new Vector3D(0.0, 0.0, -double.Epsilon)));
        Assert.Equal(0, SolidPredicates.Orient3D(p, q, r, new Vector3D(huge, huge, 0.0)));

        // Act / Assert: tiny coordinates whose products underflow
        var tiny = 1e-200;
        Assert.Equal(1, SolidPredicates.Orient3D(origin, new Vector3D(tiny, 0, 0), new Vector3D(0, tiny, 0), new Vector3D(0, 0, tiny)));
        Assert.Equal(-1, SolidPredicates.Orient3D(origin, new Vector3D(tiny, 0, 0), new Vector3D(0, tiny, 0), new Vector3D(0, 0, -tiny)));

        // Act / Assert: random extreme inputs match the exact evaluation
        var random = new Random(7);
        double Extreme() => (random.NextDouble() - 0.5) * Math.Pow(10.0, random.Next(-320, 308));
        for (var i = 0; i < 2000; i++)
        {
            var firstRandomPoint = new Vector3D(Extreme(), Extreme(), Extreme());
            var secondRandomPoint = new Vector3D(Extreme(), Extreme(), Extreme());
            var thirdRandomPoint = new Vector3D(Extreme(), Extreme(), Extreme());
            var fourthRandomPoint = new Vector3D(Extreme(), Extreme(), Extreme());
            Assert.Equal(
                SolidPredicates.Orient3DExact(firstRandomPoint, secondRandomPoint, thirdRandomPoint, fourthRandomPoint),
                SolidPredicates.Orient3D(firstRandomPoint, secondRandomPoint, thirdRandomPoint, fourthRandomPoint));
        }
    }

    /// <summary>
    ///     Validates the sign convention: positive above the plane of a counter-clockwise triangle (on the
    ///     side its normal points to), negative below, zero in the plane.
    /// </summary>
    [Fact]
    public void SolidPredicates_Orient3D_PositiveAboveTheCounterClockwisePlane()
    {
        // Arrange: a counter-clockwise triangle in the XY plane, whose normal points to +Z
        var origin = Vector3D.Zero;
        var xAxis = Vector3D.UnitX;
        var yAxis = Vector3D.UnitY;

        // Act / Assert: above, below and in the plane
        Assert.Equal(1, SolidPredicates.Orient3D(origin, xAxis, yAxis, new Vector3D(0.2, 0.2, 1.0)));
        Assert.Equal(-1, SolidPredicates.Orient3D(origin, xAxis, yAxis, new Vector3D(0.2, 0.2, -1.0)));
        Assert.Equal(0, SolidPredicates.Orient3D(origin, xAxis, yAxis, new Vector3D(5.0, -3.0, 0.0)));

        // Act / Assert: the opposite winding flips the sign, and the sign agrees with the mesh normal
        Assert.Equal(-1, SolidPredicates.Orient3D(origin, yAxis, xAxis, new Vector3D(0.2, 0.2, 1.0)));
        var tetrahedron = Render3DTestHelpers.CreateTetrahedronMesh();
        var indices = tetrahedron.TriangleIndices;
        var vertices = tetrahedron.Vertices;
        for (var t = 0; t < indices.Count; t += 3)
        {
            // An outward-wound face has every other vertex of the (convex) solid below its plane
            for (var v = 0; v < vertices.Count; v++)
            {
                if (v != indices[t] && v != indices[t + 1] && v != indices[t + 2])
                {
                    Assert.Equal(-1, SolidPredicates.Orient3D(vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]], vertices[v]));
                }
            }
        }
    }
}
