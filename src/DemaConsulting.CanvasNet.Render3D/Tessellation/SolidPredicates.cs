// cspell:ignore subnormal subnormals coplanar coplanarity collinearity underflow underflowed Shewchuk
// cspell:ignore bdxcdy cdxbdy cdxady adxcdy adxbdy bdxady

using System.Numerics;
using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Exact (epsilon-free) geometric predicates on double-precision 3D points, the single source of
///     truth for every visibility decision of <see cref="ConvexHull"/> and the exact degeneracy tests
///     (collinearity, coplanarity) that choose its initial simplex.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why exact.</b> An incremental convex hull decides, for every inserted point, which faces see
///         it. A single wrong sign on a nearly coplanar quadruple produces an overlapping, open or
///         self-intersecting hull, and a tolerance only moves the failure to another input. Every decision is
///         therefore made with <see cref="Orient3D"/>, whose result is always the true sign of the
///         determinant of the given double values.
///     </para>
///     <para>
///         <b>How.</b> The determinant is evaluated in three stages, each used only when it provably yields
///         the right sign: (1) double arithmetic with the classic rigorous static error bound for the 3D
///         orientation test (Shewchuk's first-stage filter, <c>(7 + 56 eps) eps</c> times the permanent of the
///         difference matrix with eps = 2^-53), guarded against overflow and underflow; (2) when the filter is
///         inconclusive and all twelve coordinates are short binary fractions (at most 40 significant bits
///         once aligned to a common exponent, which covers integers, grid coordinates and short decimals
///         scaled by powers of two) a 128-bit integer evaluation that cannot overflow; (3) otherwise an exact
///         <see cref="BigInteger"/> evaluation, because every finite double is an integer multiple of
///         2^-1074. Stages two and three are slower, but they are only reached for (nearly) degenerate
///         input. All members are pure and thread-safe.
///     </para>
/// </remarks>
internal static class SolidPredicates
{
    /// <summary>
    ///     Relative error bound of the double-precision orientation determinant, <c>(7 + 56 eps) eps</c> with
    ///     eps = 2^-53, rounded up. The computed value is trusted only when it exceeds this multiple of the permanent.
    /// </summary>
    private const double FilterErrorBound = 7.7716e-16;

    /// <summary>
    ///     Smallest permanent for which the filter is trusted; below it intermediate products may have
    ///     underflowed further than the relative bound covers.
    /// </summary>
    private const double FilterMinimumMagnitude = 1e-150;

    /// <summary>
    ///     Largest coordinate difference for which the filter is trusted; with every difference at most this
    ///     large no product or sum of products can overflow, and an underflowed product contributes an absolute
    ///     error far below the bound.
    /// </summary>
    private const double FilterMaximumDifference = 1e100;

    /// <summary>Number of mantissa bits stored explicitly in a double.</summary>
    private const int MantissaBits = 52;

    /// <summary>
    ///     Largest bit length of an aligned coordinate for which the 128-bit evaluation is guaranteed not to
    ///     overflow (differences have at most 41 bits, the determinant at most 126 bits).
    /// </summary>
    private const int SmallIntegerBits = 40;

    /// <summary>
    ///     Returns the sign of the determinant of the rows <c>b - a</c>, <c>c - a</c>, <c>d - a</c>: positive
    ///     when <paramref name="d"/> lies above the plane through <paramref name="a"/>, <paramref name="b"/>
    ///     and <paramref name="c"/> on the side its counter-clockwise normal <c>(b - a) x (c - a)</c> points to
    ///     (the mesh winding contract), negative when below, zero when the four points are coplanar.
    /// </summary>
    /// <param name="a">The first point of the plane (finite).</param>
    /// <param name="b">The second point of the plane (finite).</param>
    /// <param name="c">The third point of the plane (finite).</param>
    /// <param name="d">The point to classify (finite).</param>
    /// <returns>1, -1 or 0; always the exact sign for the given double values.</returns>
    public static int Orient3D(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        // Differences relative to d, as in the classic formulation (its determinant is positive when d is
        // BELOW the plane, the opposite of this contract, hence the negation of the result)
        var adx = a.X - d.X;
        var bdx = b.X - d.X;
        var cdx = c.X - d.X;
        var ady = a.Y - d.Y;
        var bdy = b.Y - d.Y;
        var cdy = c.Y - d.Y;
        var adz = a.Z - d.Z;
        var bdz = b.Z - d.Z;
        var cdz = c.Z - d.Z;

        var bdxcdy = bdx * cdy;
        var cdxbdy = cdx * bdy;
        var cdxady = cdx * ady;
        var adxcdy = adx * cdy;
        var adxbdy = adx * bdy;
        var bdxady = bdx * ady;

        var determinant = (adz * (bdxcdy - cdxbdy)) + (bdz * (cdxady - adxcdy)) + (cdz * (adxbdy - bdxady));
        var permanent = ((Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * Math.Abs(adz)) +
                        ((Math.Abs(cdxady) + Math.Abs(adxcdy)) * Math.Abs(bdz)) +
                        ((Math.Abs(adxbdy) + Math.Abs(bdxady)) * Math.Abs(cdz));

        // Fast path: the double determinant is accepted only when it provably has the right sign (a non-finite
        // difference makes the largest difference compare false, so overflow always takes the exact path)
        var largest = Math.Max(
            Math.Max(Math.Max(Math.Abs(adx), Math.Abs(bdx)), Math.Max(Math.Abs(cdx), Math.Abs(ady))),
            Math.Max(Math.Max(Math.Abs(bdy), Math.Abs(cdy)), Math.Max(Math.Abs(adz), Math.Max(Math.Abs(bdz), Math.Abs(cdz)))));
        if (largest <= FilterMaximumDifference && permanent >= FilterMinimumMagnitude &&
            Math.Abs(determinant) > FilterErrorBound * permanent)
        {
            return determinant > 0.0 ? -1 : 1;
        }

        // Slow paths (near or exactly coplanar, or extreme magnitudes)
        return TryOrient3DSmallIntegers(a, b, c, d, out var sign) ? sign : Orient3DExact(a, b, c, d);
    }

    /// <summary>
    ///     Evaluates the orientation sign exactly with arbitrary-precision integers. Exposed internally so
    ///     tests can compare the filtered predicate against it.
    /// </summary>
    /// <param name="a">The first point of the plane (finite).</param>
    /// <param name="b">The second point of the plane (finite).</param>
    /// <param name="c">The third point of the plane (finite).</param>
    /// <param name="d">The point to classify (finite).</param>
    /// <returns>1, -1 or 0, with the sign convention of <see cref="Orient3D"/>.</returns>
    public static int Orient3DExact(Vector3D a, Vector3D b, Vector3D c, Vector3D d)
    {
        // Every difference is an exact integer multiple of 2^-1074, so scaling by 2^1074 keeps the sign
        var ux = PolygonPredicates.ToScaledInteger(b.X) - PolygonPredicates.ToScaledInteger(a.X);
        var uy = PolygonPredicates.ToScaledInteger(b.Y) - PolygonPredicates.ToScaledInteger(a.Y);
        var uz = PolygonPredicates.ToScaledInteger(b.Z) - PolygonPredicates.ToScaledInteger(a.Z);
        var vx = PolygonPredicates.ToScaledInteger(c.X) - PolygonPredicates.ToScaledInteger(a.X);
        var vy = PolygonPredicates.ToScaledInteger(c.Y) - PolygonPredicates.ToScaledInteger(a.Y);
        var vz = PolygonPredicates.ToScaledInteger(c.Z) - PolygonPredicates.ToScaledInteger(a.Z);
        var wx = PolygonPredicates.ToScaledInteger(d.X) - PolygonPredicates.ToScaledInteger(a.X);
        var wy = PolygonPredicates.ToScaledInteger(d.Y) - PolygonPredicates.ToScaledInteger(a.Y);
        var wz = PolygonPredicates.ToScaledInteger(d.Z) - PolygonPredicates.ToScaledInteger(a.Z);

        // u . (v x w)
        var determinant = (ux * ((vy * wz) - (vz * wy))) -
                          (uy * ((vx * wz) - (vz * wx))) +
                          (uz * ((vx * wy) - (vy * wx)));
        return determinant.Sign;
    }

    /// <summary>
    ///     Chooses a coordinate plane in which the triangle <paramref name="a"/>, <paramref name="b"/>,
    ///     <paramref name="c"/> has a non-zero projected area (exactly), or reports that the three points
    ///     are collinear.
    /// </summary>
    /// <remarks>
    ///     The triangle is exactly collinear precisely when its projection onto all three coordinate planes
    ///     is degenerate (the components of the cross product are the three projected areas), so the
    ///     answer is exact. The planes are tried in a fixed order (drop Z, then Y, then X), which keeps the
    ///     choice deterministic.
    /// </remarks>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <param name="c">The third point.</param>
    /// <returns>The dropped axis (2 = Z, 1 = Y, 0 = X) of a non-degenerate projection, or -1 when collinear.</returns>
    public static int NonDegenerateProjection(Vector3D a, Vector3D b, Vector3D c)
    {
        if (PolygonPredicates.Orient2D(Project(a, 2), Project(b, 2), Project(c, 2)) != 0)
        {
            return 2;
        }

        if (PolygonPredicates.Orient2D(Project(a, 1), Project(b, 1), Project(c, 1)) != 0)
        {
            return 1;
        }

        return PolygonPredicates.Orient2D(Project(a, 0), Project(b, 0), Project(c, 0)) != 0 ? 0 : -1;
    }

    /// <summary>
    ///     Projects a point onto a coordinate plane by dropping one axis. The remaining axes keep their
    ///     cyclic order (X,Y / Y,Z / Z,X), so the projected orientation equals the sign of the corresponding
    ///     component of the cross product.
    /// </summary>
    /// <param name="point">The point to project.</param>
    /// <param name="droppedAxis">The dropped axis: 0 = X, 1 = Y, 2 = Z.</param>
    /// <returns>The projected point.</returns>
    public static Vector2D Project(Vector3D point, int droppedAxis) => droppedAxis switch
    {
        0 => new Vector2D(point.Y, point.Z),
        1 => new Vector2D(point.Z, point.X),
        _ => new Vector2D(point.X, point.Y),
    };

    /// <summary>
    ///     Evaluates the orientation determinant with 128-bit integers when every coordinate is a short
    ///     binary fraction; declines (returns <see langword="false"/>) otherwise.
    /// </summary>
    /// <remarks>
    ///     Each coordinate is decomposed into an odd integer mantissa and a binary exponent. After aligning
    ///     all non-zero coordinates to the smallest exponent they must each fit in 40 bits; the differences
    ///     then have at most 41 bits, the products of two at most 82, the 2x2 minors at most 83 and the
    ///     determinant at most 126 bits, so 128-bit arithmetic is exact.
    /// </remarks>
    /// <param name="a">The first point of the plane.</param>
    /// <param name="b">The second point of the plane.</param>
    /// <param name="c">The third point of the plane.</param>
    /// <param name="d">The point to classify.</param>
    /// <param name="sign">Receives the exact orientation sign when the method succeeds.</param>
    /// <returns><see langword="true"/> when the coordinates qualified and <paramref name="sign"/> is valid.</returns>
    private static bool TryOrient3DSmallIntegers(Vector3D a, Vector3D b, Vector3D c, Vector3D d, out int sign)
    {
        sign = 0;
        Span<long> mantissa = stackalloc long[12];
        Span<int> exponent = stackalloc int[12];
        Decompose(a, mantissa, exponent, 0);
        Decompose(b, mantissa, exponent, 3);
        Decompose(c, mantissa, exponent, 6);
        Decompose(d, mantissa, exponent, 9);

        // The smallest exponent among the non-zero coordinates is the common unit
        var minimum = int.MaxValue;
        for (var i = 0; i < 12; i++)
        {
            if (mantissa[i] != 0 && exponent[i] < minimum)
            {
                minimum = exponent[i];
            }
        }

        // All coordinates zero: the points coincide, so the determinant is zero
        if (minimum == int.MaxValue)
        {
            return true;
        }

        // Align every coordinate to the common unit, declining when one would not fit in 40 bits
        Span<long> scaled = stackalloc long[12];
        for (var i = 0; i < 12; i++)
        {
            if (mantissa[i] == 0)
            {
                continue;
            }

            var shift = exponent[i] - minimum;
            var length = 64 - BitOperations.LeadingZeroCount((ulong)Math.Abs(mantissa[i]));
            if (shift + length > SmallIntegerBits)
            {
                return false;
            }

            scaled[i] = mantissa[i] << shift;
        }

        // u . (v x w) with u = b - a, v = c - a, w = d - a
        Int128 ux = scaled[3] - scaled[0];
        Int128 uy = scaled[4] - scaled[1];
        Int128 uz = scaled[5] - scaled[2];
        Int128 vx = scaled[6] - scaled[0];
        Int128 vy = scaled[7] - scaled[1];
        Int128 vz = scaled[8] - scaled[2];
        Int128 wx = scaled[9] - scaled[0];
        Int128 wy = scaled[10] - scaled[1];
        Int128 wz = scaled[11] - scaled[2];
        var determinant = (ux * ((vy * wz) - (vz * wy))) -
                          (uy * ((vx * wz) - (vz * wx))) +
                          (uz * ((vx * wy) - (vy * wx)));
        sign = determinant.CompareTo(Int128.Zero);
        return true;
    }

    /// <summary>
    ///     Decomposes the three coordinates of a point into odd integer mantissas and binary exponents
    ///     (<c>value = mantissa * 2^exponent</c>; zero has mantissa zero).
    /// </summary>
    /// <param name="point">The finite point.</param>
    /// <param name="mantissa">Receives the signed mantissas at <paramref name="offset"/>.</param>
    /// <param name="exponent">Receives the exponents at <paramref name="offset"/>.</param>
    /// <param name="offset">The index of the point's first coordinate in the output spans.</param>
    private static void Decompose(Vector3D point, Span<long> mantissa, Span<int> exponent, int offset)
    {
        Decompose(point.X, out mantissa[offset], out exponent[offset]);
        Decompose(point.Y, out mantissa[offset + 1], out exponent[offset + 1]);
        Decompose(point.Z, out mantissa[offset + 2], out exponent[offset + 2]);
    }

    /// <summary>
    ///     Decomposes a finite double into an odd integer mantissa and a binary exponent.
    /// </summary>
    /// <param name="value">The finite value.</param>
    /// <param name="mantissa">Receives the signed odd mantissa (zero for zero).</param>
    /// <param name="exponent">Receives the exponent (zero for zero).</param>
    private static void Decompose(double value, out long mantissa, out int exponent)
    {
        // Split the IEEE 754 representation into biased exponent and fraction
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biased = (int)((bits >> MantissaBits) & 0x7FF);
        var fraction = bits & ((1L << MantissaBits) - 1);

        // Normal numbers have an implicit leading one; subnormals do not and use the minimum exponent
        var magnitude = biased == 0 ? fraction : fraction | (1L << MantissaBits);
        var power = biased == 0 ? -1074 : biased - 1075;
        if (magnitude == 0)
        {
            mantissa = 0;
            exponent = 0;
            return;
        }

        // Strip trailing zero bits so that short fractions stay short after alignment
        var zeros = BitOperations.TrailingZeroCount(magnitude);
        magnitude >>= zeros;
        mantissa = bits < 0 ? -magnitude : magnitude;
        exponent = power + zeros;
    }
}
