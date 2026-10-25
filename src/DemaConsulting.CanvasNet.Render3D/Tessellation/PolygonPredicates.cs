// cspell:ignore subnormal subnormals

using System.Numerics;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Exact (epsilon-free) geometric predicates on double-precision 2D points, the single source of
///     truth for every ear, containment, intersection and visibility decision of the polygon code.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why exact.</b> Ear clipping and polygon validation make hundreds of orientation decisions
///         per polygon; a single wrong sign on a nearly collinear triple yields overlapping or missing
///         triangles. Tolerances only move the failure to a different input, so every decision is made
///         with <see cref="Orient2D"/>, whose result is always the true sign of the determinant of the
///         given double values.
///     </para>
///     <para>
///         <b>How.</b> The determinant is first evaluated in double arithmetic together with a rigorous
///         forward error bound (the classic static filter for the 2D orientation test); only when the
///         magnitude of the result does not exceed that bound (for example for exactly collinear points)
///         is it re-evaluated exactly with <see cref="BigInteger"/>, because every finite double is an
///         integer multiple of 2^-1074. The exact path is rare and its cost is irrelevant for CAD-sized
///         profiles. All members are pure and thread-safe.
///     </para>
/// </remarks>
internal static class PolygonPredicates
{
    /// <summary>
    ///     Relative error bound of the double-precision determinant: (3 + 16 eps) eps with eps = 2^-53.
    ///     The computed value is trusted only when it exceeds this multiple of the sum of the two products.
    /// </summary>
    private const double FilterErrorBound = 3.3306690738754716e-16;

    /// <summary>
    ///     Smallest sum of absolute products for which the filter is trusted; below it the products may
    ///     have rounded to zero, which the relative bound does not cover.
    /// </summary>
    private const double FilterMinimumMagnitude = 1e-280;

    /// <summary>
    ///     Largest sum of absolute products for which the filter is trusted; above it the products may
    ///     have overflowed.
    /// </summary>
    private const double FilterMaximumMagnitude = 1e280;

    /// <summary>Number of mantissa bits stored explicitly in a double.</summary>
    private const int MantissaBits = 52;

    /// <summary>
    ///     Returns the sign of the cross product <c>(second - first) x (third - first)</c>: positive when
    ///     <paramref name="third"/> lies to the left of the directed line from <paramref name="first"/> to
    ///     <paramref name="second"/> (counter-clockwise turn), negative to the right, zero when collinear.
    /// </summary>
    /// <param name="first">The first point (finite).</param>
    /// <param name="second">The second point (finite).</param>
    /// <param name="third">The third point (finite).</param>
    /// <returns>1, -1 or 0; always the exact sign for the given double values.</returns>
    public static int Orient2D(Vector2D first, Vector2D second, Vector2D third)
    {
        // Fast path: double determinant accepted only when it provably has the right sign
        var left = (first.X - third.X) * (second.Y - third.Y);
        var right = (first.Y - third.Y) * (second.X - third.X);
        var determinant = left - right;
        var magnitude = Math.Abs(left) + Math.Abs(right);
        if (magnitude is > FilterMinimumMagnitude and < FilterMaximumMagnitude &&
            Math.Abs(determinant) > FilterErrorBound * magnitude)
        {
            return determinant > 0.0 ? 1 : -1;
        }

        // Slow path (near or exactly collinear, or extreme magnitudes): exact integer arithmetic
        return Orient2DExact(first, second, third);
    }

    /// <summary>
    ///     Evaluates the orientation sign exactly with arbitrary-precision integers. Exposed internally so
    ///     tests can compare the filtered predicate against it.
    /// </summary>
    /// <param name="first">The first point (finite).</param>
    /// <param name="second">The second point (finite).</param>
    /// <param name="third">The third point (finite).</param>
    /// <returns>1, -1 or 0.</returns>
    public static int Orient2DExact(Vector2D first, Vector2D second, Vector2D third)
    {
        // Every difference is an exact integer multiple of 2^-1074, so scaling by 2^1074 keeps the sign
        var ax = ToScaledInteger(first.X) - ToScaledInteger(third.X);
        var ay = ToScaledInteger(first.Y) - ToScaledInteger(third.Y);
        var bx = ToScaledInteger(second.X) - ToScaledInteger(third.X);
        var by = ToScaledInteger(second.Y) - ToScaledInteger(third.Y);
        return ((ax * by) - (ay * bx)).Sign;
    }

    /// <summary>
    ///     Tests whether two closed line segments share at least one point (crossing, touching at an end,
    ///     or overlapping along a common line).
    /// </summary>
    /// <param name="a">The first end of the first segment.</param>
    /// <param name="b">The second end of the first segment.</param>
    /// <param name="c">The first end of the second segment.</param>
    /// <param name="d">The second end of the second segment.</param>
    /// <returns><see langword="true"/> when the segments have a common point.</returns>
    public static bool SegmentsIntersect(Vector2D a, Vector2D b, Vector2D c, Vector2D d)
    {
        var o1 = Orient2D(a, b, c);
        var o2 = Orient2D(a, b, d);
        var o3 = Orient2D(c, d, a);
        var o4 = Orient2D(c, d, b);

        // General position and the cases where an end lies on the other line but the ends straddle it
        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        // Remaining intersections are collinear contacts: an end lying on the other closed segment
        return (o1 == 0 && InBoundingBox(a, b, c)) ||
               (o2 == 0 && InBoundingBox(a, b, d)) ||
               (o3 == 0 && InBoundingBox(c, d, a)) ||
               (o4 == 0 && InBoundingBox(c, d, b));
    }

    /// <summary>
    ///     Tests whether <paramref name="point"/> lies on the closed segment from <paramref name="a"/> to
    ///     <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The first segment end.</param>
    /// <param name="b">The second segment end.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> when the point is on the segment (ends included).</returns>
    public static bool PointOnSegment(Vector2D a, Vector2D b, Vector2D point) =>
        Orient2D(a, b, point) == 0 && InBoundingBox(a, b, point);

    /// <summary>
    ///     Tests whether a point lies strictly inside a simple polygon using the even-odd crossing rule
    ///     with a half-open edge convention.
    /// </summary>
    /// <param name="point">The point to classify; the caller guarantees it is not on the polygon boundary.</param>
    /// <param name="contour">The polygon vertices in either winding order (implicitly closed).</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    public static bool PointInPolygon(Vector2D point, ReadOnlySpan<Vector2D> contour)
    {
        var inside = false;
        var previous = contour[^1];
        foreach (var current in contour)
        {
            // Only edges straddling the horizontal line through the point can cross the +X ray
            if ((previous.Y > point.Y) != (current.Y > point.Y))
            {
                // The ray hits the edge when the point is left of an upward edge or right of a downward one
                var side = Orient2D(previous, current, point);
                if (current.Y > previous.Y ? side > 0 : side < 0)
                {
                    inside = !inside;
                }
            }

            previous = current;
        }

        return inside;
    }

    /// <summary>
    ///     Tests whether <paramref name="point"/> lies in the closed triangle <paramref name="a"/>,
    ///     <paramref name="b"/>, <paramref name="c"/> (boundary included).
    /// </summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="point">The point to test.</param>
    /// <param name="counterClockwise">
    ///     <see langword="true"/> when the corners are known to be counter-clockwise (the caller has
    ///     already established it); the test is then three sign checks.
    /// </param>
    /// <returns><see langword="true"/> when the point is inside or on the boundary.</returns>
    public static bool PointInClosedTriangle(Vector2D a, Vector2D b, Vector2D c, Vector2D point, bool counterClockwise)
    {
        var o1 = Orient2D(a, b, point);
        var o2 = Orient2D(b, c, point);
        var o3 = Orient2D(c, a, point);
        return counterClockwise
            ? o1 >= 0 && o2 >= 0 && o3 >= 0
            : o1 <= 0 && o2 <= 0 && o3 <= 0;
    }

    /// <summary>
    ///     Tests whether the direction from <paramref name="vertex"/> to <paramref name="target"/> lies
    ///     strictly inside the interior wedge of a boundary corner whose interior is on the left of
    ///     <c>previous -&gt; vertex -&gt; next</c>.
    /// </summary>
    /// <remarks>
    ///     The interior wedge is the open angular sweep counter-clockwise from the outgoing edge to the
    ///     incoming edge reversed. A direction along either edge is therefore not strictly inside.
    /// </remarks>
    /// <param name="previous">The previous boundary vertex.</param>
    /// <param name="vertex">The corner vertex.</param>
    /// <param name="next">The next boundary vertex.</param>
    /// <param name="target">The point whose direction is tested.</param>
    /// <returns><see langword="true"/> when the direction is strictly inside the interior wedge.</returns>
    public static bool InsideCorner(Vector2D previous, Vector2D vertex, Vector2D next, Vector2D target)
    {
        var turn = Orient2D(previous, vertex, next);

        // Convex corner: left of the outgoing edge and right of the incoming edge reversed
        if (turn > 0)
        {
            return Orient2D(vertex, next, target) > 0 && Orient2D(vertex, target, previous) > 0;
        }

        // Reflex corner: everything except the closed exterior wedge
        if (turn < 0)
        {
            return !(Orient2D(vertex, previous, target) >= 0 && Orient2D(vertex, target, next) >= 0);
        }

        // Straight corner: the interior is the open half plane on the left of the outgoing edge
        return Orient2D(vertex, next, target) > 0;
    }

    /// <summary>
    ///     Tests whether a point known to be collinear with a segment lies within the segment's
    ///     axis-aligned bounding box, which for collinear points is equivalent to lying on the segment.
    /// </summary>
    /// <param name="a">The first segment end.</param>
    /// <param name="b">The second segment end.</param>
    /// <param name="point">The collinear point.</param>
    /// <returns><see langword="true"/> when the point is between (or at) the ends.</returns>
    public static bool InBoundingBox(Vector2D a, Vector2D b, Vector2D point) =>
        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);

    /// <summary>
    ///     Converts a finite double into the exact integer <c>value * 2^1074</c> (every finite double is an
    ///     integer multiple of 2^-1074). Shared with <see cref="SolidPredicates"/>, whose exact path uses the
    ///     same scaling.
    /// </summary>
    /// <param name="value">The finite value.</param>
    /// <returns>The exact scaled integer.</returns>
    internal static BigInteger ToScaledInteger(double value)
    {
        // Decompose the IEEE 754 representation into sign, biased exponent and fraction
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> MantissaBits) & 0x7FF);
        var fraction = bits & ((1L << MantissaBits) - 1);

        // Normal numbers have an implicit leading one and value = m * 2^(e - 1075); subnormals are
        // fraction * 2^-1074 already, so after scaling by 2^1074 only the shift differs
        BigInteger magnitude;
        if (exponent == 0)
        {
            magnitude = new BigInteger(fraction);
        }
        else
        {
            magnitude = new BigInteger(fraction | (1L << MantissaBits)) << (exponent - 1);
        }

        return bits < 0 ? -magnitude : magnitude;
    }
}
