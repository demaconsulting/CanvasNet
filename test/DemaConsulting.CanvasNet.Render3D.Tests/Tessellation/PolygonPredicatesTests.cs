// cspell:ignore subnormal ulps ulp

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for the exact geometric predicates of <see cref="PolygonPredicates"/>.
/// </summary>
public class PolygonPredicatesTests
{
    /// <summary>
    ///     Computes the exact orientation sign of integer-lattice points (multiples of 2^-20) with 128-bit
    ///     integers, independently of the production implementation.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <param name="c">The third point.</param>
    /// <returns>The exact orientation sign.</returns>
    private static int ReferenceOrientation(Vector2D a, Vector2D b, Vector2D c)
    {
        static Int128 Scaled(double value) => (Int128)Math.ScaleB(value, 20);

        var abx = Scaled(b.X) - Scaled(a.X);
        var aby = Scaled(b.Y) - Scaled(a.Y);
        var acx = Scaled(c.X) - Scaled(a.X);
        var acy = Scaled(c.Y) - Scaled(a.Y);
        return (abx * acy - aby * acx).CompareTo(Int128.Zero);
    }

    /// <summary>
    ///     Validates the classic near-collinear counter-example: points that differ from the line y = x by a
    ///     few units in the last place have an exactly known orientation, which a naive double determinant
    ///     gets wrong for some of them.
    /// </summary>
    [Fact]
    public void PolygonPredicates_Orient2D_NearCollinearPoints_ReturnsExactSign()
    {
        // Arrange: a = (0.5 + i u, 0.5 + j u) relative to the line through (12,12) and (24,24); the exact
        // cross product is 12 (a.Y - a.X) = 12 (j - i) u, so the sign is sign(j - i)
        var unit = Math.BitIncrement(0.5) - 0.5;
        var b = new Vector2D(12.0, 12.0);
        var c = new Vector2D(24.0, 24.0);
        var naiveMistakes = 0;

        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var a = new Vector2D(0.5 + (i * unit), 0.5 + (j * unit));

                // Act: the probe is a, tested against the directed line b -> c
                var actual = PolygonPredicates.Orient2D(b, c, a);
                var naive = Math.Sign(((c.X - b.X) * (a.Y - b.Y)) - ((c.Y - b.Y) * (a.X - b.X)));

                // Assert: left of the directed line b->c exactly when a.Y > a.X, i.e. j > i
                Assert.Equal(Math.Sign(j - i), actual);
                if (naive != actual)
                {
                    naiveMistakes++;
                }
            }
        }

        // The scenario is meaningful only if the naive evaluation really fails here
        Assert.True(naiveMistakes > 0, "The naive determinant was expected to misjudge at least one case.");
    }

    /// <summary>
    ///     Validates that exactly collinear points, including subnormal and huge magnitudes where a double
    ///     determinant loses all information, are classified exactly.
    /// </summary>
    [Fact]
    public void PolygonPredicates_Orient2D_ExactCollinearAndExtremeMagnitudes_ReturnExactSign()
    {
        // Arrange / Act / Assert: plain collinear points
        Assert.Equal(0, PolygonPredicates.Orient2D(new Vector2D(0, 0), new Vector2D(1, 1), new Vector2D(3, 3)));

        // Subnormal coordinates: the determinant rounds to zero in double arithmetic
        var tiny = double.Epsilon;
        Assert.Equal(1, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(tiny, tiny), new Vector2D(2 * tiny, 4 * tiny)));
        Assert.Equal(-1, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(tiny, tiny), new Vector2D(4 * tiny, 2 * tiny)));
        Assert.Equal(0, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(tiny, tiny), new Vector2D(3 * tiny, 3 * tiny)));

        // Huge coordinates: the products overflow to infinity in double arithmetic
        var huge = 1e200;
        Assert.Equal(1, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(huge, 0), new Vector2D(0, huge)));
        Assert.Equal(-1, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(0, huge), new Vector2D(huge, 0)));
        Assert.Equal(0, PolygonPredicates.Orient2D(Vector2D.Zero, new Vector2D(huge, huge), new Vector2D(-huge, -huge)));
    }

    /// <summary>
    ///     Validates the filtered predicate against an independent exact 128-bit reference on lattice
    ///     points, including many exactly collinear and nearly collinear triples.
    /// </summary>
    [Fact]
    public void PolygonPredicates_Orient2D_RandomPoints_MatchIntegerReference()
    {
        // Arrange: fixed-seed points that are multiples of 2^-20, so the reference is exact in Int128
        var random = new Random(12345);
        double Coordinate() => random.Next(-(1 << 24), 1 << 24) / (double)(1 << 20);
        var zeros = 0;

        for (var n = 0; n < 5000; n++)
        {
            var a = new Vector2D(Coordinate(), Coordinate());
            var b = new Vector2D(Coordinate(), Coordinate());

            // Every other probe is placed exactly or almost on the line a-b
            var t = random.Next(-4, 5);
            var c = (n % 2 == 0)
                ? new Vector2D(Coordinate(), Coordinate())
                : new Vector2D(a.X + (t * (b.X - a.X)), a.Y + (t * (b.Y - a.Y)) + (random.Next(0, 3) - 1) / (double)(1 << 20));

            // Act
            var expected = ReferenceOrientation(a, b, c);
            var actual = PolygonPredicates.Orient2D(a, b, c);

            // Assert
            Assert.Equal(expected, actual);
            zeros += expected == 0 ? 1 : 0;
        }

        Assert.True(zeros > 100, "The sample must contain many exactly collinear triples.");
    }

    /// <summary>
    ///     Validates segment intersection for crossing, touching, collinear-overlapping and disjoint segments.
    /// </summary>
    [Fact]
    public void PolygonPredicates_SegmentsIntersect_TouchingCollinearAndCrossing_AreClassified()
    {
        // Arrange
        var a = new Vector2D(0, 0);
        var b = new Vector2D(4, 4);

        // Act / Assert: proper crossing
        Assert.True(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(0, 4), new Vector2D(4, 0)));

        // An end touching the interior of the other segment (T contact)
        Assert.True(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(2, 2), new Vector2D(5, 0)));

        // Shared end point only
        Assert.True(PolygonPredicates.SegmentsIntersect(a, b, b, new Vector2D(8, 0)));

        // Collinear: overlapping, touching at an end, and disjoint
        Assert.True(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(2, 2), new Vector2D(6, 6)));
        Assert.True(PolygonPredicates.SegmentsIntersect(a, b, b, new Vector2D(6, 6)));
        Assert.False(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(5, 5), new Vector2D(6, 6)));

        // Parallel and skew disjoint segments
        Assert.False(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(0, 1), new Vector2D(4, 5)));
        Assert.False(PolygonPredicates.SegmentsIntersect(a, b, new Vector2D(5, 0), new Vector2D(5, 3)));

        // Lines would cross but the segments stop short
        Assert.False(PolygonPredicates.SegmentsIntersect(a, new Vector2D(1, 1), new Vector2D(0, 4), new Vector2D(4, 0)));
    }

    /// <summary>
    ///     Validates point-in-polygon classification for convex and concave polygons in both windings,
    ///     including a ray that passes exactly through a vertex.
    /// </summary>
    [Fact]
    public void PolygonPredicates_PointInPolygon_InsideAndOutside_AreClassified()
    {
        // Arrange: an L shape (concave) and its clockwise version
        Vector2D[] ccw = [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)];
        var cw = Enumerable.Reverse(ccw).ToArray();

        foreach (var polygon in new[] { ccw, cw })
        {
            // Act / Assert: inside the two arms and the corner
            Assert.True(PolygonPredicates.PointInPolygon(new Vector2D(1, 1), polygon));
            Assert.True(PolygonPredicates.PointInPolygon(new Vector2D(3, 1), polygon));
            Assert.True(PolygonPredicates.PointInPolygon(new Vector2D(1, 3), polygon));

            // Outside: in the notch and beyond the bounds
            Assert.False(PolygonPredicates.PointInPolygon(new Vector2D(3, 3), polygon));
            Assert.False(PolygonPredicates.PointInPolygon(new Vector2D(-1, 1), polygon));
            Assert.False(PolygonPredicates.PointInPolygon(new Vector2D(5, 2), polygon));
            Assert.False(PolygonPredicates.PointInPolygon(new Vector2D(2.5, 4), polygon));

            // The ray of this point runs exactly through the vertices (2,2) and (4,2)
            Assert.True(PolygonPredicates.PointInPolygon(new Vector2D(1, 2), polygon));
        }
    }

    /// <summary>
    ///     Validates the interior-wedge test at convex, reflex and straight corners, including directions
    ///     along the two edges, which are not strictly inside.
    /// </summary>
    [Fact]
    public void PolygonPredicates_InsideCorner_ConvexReflexAndStraightCorners_AreClassified()
    {
        // Arrange: a boundary running west to east and then turning north is a left turn, so the interior wedge
        // is the quadrant between the outgoing edge (north) and the reversed incoming edge (west)
        var origin = Vector2D.Zero;
        var previous = new Vector2D(-1, 0);
        var next = new Vector2D(0, 1);
        Assert.True(PolygonPredicates.Orient2D(previous, origin, next) > 0);

        // Act / Assert: convex corner
        Assert.True(PolygonPredicates.InsideCorner(previous, origin, next, new Vector2D(-1, 1)));
        Assert.False(PolygonPredicates.InsideCorner(previous, origin, next, new Vector2D(1, 1)));
        Assert.False(PolygonPredicates.InsideCorner(previous, origin, next, new Vector2D(0, 2)));
        Assert.False(PolygonPredicates.InsideCorner(previous, origin, next, new Vector2D(-2, 0)));

        // Reflex corner: a boundary heading south and then west is a right turn; the interior is the 270 degree
        // sweep from the outgoing edge (west) counter-clockwise to the reversed incoming edge (north)
        var north = new Vector2D(0, 1);
        var west = new Vector2D(-1, 0);
        Assert.True(PolygonPredicates.Orient2D(north, origin, west) < 0);
        Assert.True(PolygonPredicates.InsideCorner(north, origin, west, new Vector2D(-1, -1)));
        Assert.True(PolygonPredicates.InsideCorner(north, origin, west, new Vector2D(1, 1)));
        Assert.False(PolygonPredicates.InsideCorner(north, origin, west, new Vector2D(-1, 1)));
        Assert.False(PolygonPredicates.InsideCorner(north, origin, west, new Vector2D(0, 2)));
        Assert.False(PolygonPredicates.InsideCorner(north, origin, west, new Vector2D(-2, 0)));

        // Straight corner (west to east): the interior is the half plane left of the direction of travel
        var east = new Vector2D(1, 0);
        Assert.False(PolygonPredicates.InsideCorner(west, origin, east, new Vector2D(0, -1)));
        Assert.True(PolygonPredicates.InsideCorner(west, origin, east, new Vector2D(0, 1)));
        Assert.False(PolygonPredicates.InsideCorner(west, origin, east, new Vector2D(3, 0)));
    }

    /// <summary>
    ///     Validates the closed-triangle containment helper for interior, boundary and exterior points.
    /// </summary>
    [Fact]
    public void PolygonPredicates_PointInClosedTriangle_InteriorBoundaryAndExterior_AreClassified()
    {
        // Arrange
        var origin = new Vector2D(0, 0);
        var east = new Vector2D(4, 0);
        var north = new Vector2D(0, 4);

        // Act / Assert
        Assert.True(PolygonPredicates.PointInClosedTriangle(origin, east, north, new Vector2D(1, 1), counterClockwise: true));
        Assert.True(PolygonPredicates.PointInClosedTriangle(origin, east, north, new Vector2D(2, 2), counterClockwise: true));
        Assert.True(PolygonPredicates.PointInClosedTriangle(origin, east, north, east, counterClockwise: true));
        Assert.False(PolygonPredicates.PointInClosedTriangle(origin, east, north, new Vector2D(3, 3), counterClockwise: true));
        Assert.True(PolygonPredicates.PointInClosedTriangle(origin, north, east, new Vector2D(1, 1), counterClockwise: false));
        Assert.False(PolygonPredicates.PointInClosedTriangle(origin, north, east, new Vector2D(-1, 1), counterClockwise: false));
    }
}
