using System.Globalization;
using System.Numerics;

namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The correctly rounded <c>Math.sin</c>, <c>Math.cos</c>, <c>Math.log</c> and <c>Math.pow</c> the cose layout uses
///     (original work of this repository).
/// </summary>
/// <remarks>
///     <para>
///         The cose layout is chaotic: one unit in the last place of a sine moves the nodes of a mid-sized tree by
///         hundreds of pixels. To give Mermaid's layout, the port needs the results the reference browser computes.
///         Chrome's <c>Math.sin</c>, <c>Math.cos</c> and <c>Math.log</c> are correctly rounded (the nearest double to
///         the exact value, ties to even), and so is its <c>Math.pow</c> on every argument pair the layout reaches up to
///         1,016 nodes. The host's C runtime is not, so these functions compute the exact rounding themselves and never
///         use <see cref="Math"/> for a transcendental function. The results are the same on every platform.
///     </para>
///     <para>
///         Each function evaluates the exact value in binary fixed point with <see cref="BigInteger"/> at a working
///         precision, with a bound on the error, and rounds both ends of the error interval to the nearest double
///         (Ziv's strategy). When both ends give the same double, it is the correctly rounded result. When they differ,
///         the exact value lies close to a midpoint between two doubles, and the evaluation is repeated at a higher
///         precision (<see cref="ZivPrecisions"/>). Sine and cosine reduce the argument by a multiple of π/2 and sum
///         Taylor series; the logarithm splits off a power of two and sums the series of <c>2·atanh((f-1)/(f+1))</c>;
///         the power is <c>exp(y·ln x)</c>, with the exponential reduced by a multiple of ln 2. The constants π and ln 2
///         are computed once, with Machin's formula and the series of <c>atanh(1/3)</c>.
///     </para>
///     <para>
///         The functions are pure and safe for concurrent use. Each reports how many higher precisions it needed, so the
///         layout can charge the work.
///     </para>
/// </remarks>
internal sealed partial class CoseLayout
{
    /// <summary>
    ///     The precisions, in bits, of Ziv's evaluation: the first one decides nearly every case, the others the cases
    ///     whose exact value lies close to a rounding midpoint.
    /// </summary>
    internal static readonly int[] ZivPrecisions = [192, 384, 768];

    /// <summary>
    ///     The bits of an approximation's error bound: an approximation <c>(v, e)</c> stands for an exact value within
    ///     <c>(v ± 2^ErrorBits)·2^e</c>. The evaluations below err by fewer than 2^13 units, so the bound is generous.
    /// </summary>
    internal const int ErrorBits = 24;

    /// <summary>The largest magnitude of an argument of <see cref="Sin"/> and <see cref="Cos"/> (the layout passes [0, 2π)).</summary>
    internal const double TrigLimit = 8;

    /// <summary>The bits kept beyond the precision in a working scale, so the rounding errors of the series stay far below the bound.</summary>
    private const int GuardBits = 48;

    /// <summary>The bits of the cached constants π and ln 2 (more than the largest working scale needs).</summary>
    private const int ConstantBits = 1024;

    /// <summary>The number of bits of a double's significand, the implicit bit included.</summary>
    private const int SignificandBits = 53;

    /// <summary>2^64: an exponent at least this large makes every power over- or underflow.</summary>
    private const double PowerExponentLimit = 18446744073709551616.0;

    /// <summary>π·2^<see cref="ConstantBits"/>, truncated.</summary>
    private static readonly BigInteger PiConstant = ComputePi();

    /// <summary>ln 2·2^<see cref="ConstantBits"/>, truncated.</summary>
    private static readonly BigInteger Ln2Constant = ComputeLn2();

    /// <summary>
    ///     An approximation of an exact value: the true value lies within <c>(Value ± 2^ErrorBits)·2^Exponent</c>.
    /// </summary>
    /// <param name="precision">The precision, in bits, to evaluate at (one of <see cref="ZivPrecisions"/>).</param>
    /// <returns>The approximation's integer value and binary exponent.</returns>
    internal delegate (BigInteger Value, int Exponent) Approximation(int precision);

    /// <summary>
    ///     Computes JavaScript's <c>Math.sin</c> correctly rounded.
    /// </summary>
    /// <param name="x">The angle in radians: finite, with a magnitude of at most <see cref="TrigLimit"/>.</param>
    /// <param name="steps">The number of higher precisions the rounding needed (0 for nearly every argument).</param>
    /// <returns>The double nearest to the sine of <paramref name="x"/>; a zero keeps its sign.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="x"/> is NaN, infinite or larger than <see cref="TrigLimit"/> in magnitude.
    /// </exception>
    internal static double Sin(double x, out int steps)
    {
        // The layout's angles lie in [0, 2π); the domain is limited so the reduction stays simple and exact
        CheckTrigArgument(x);
        if (JsEquals(x, 0))
        {
            steps = 0;
            return x;
        }

        // The sine is odd, and rounding to nearest is symmetric, so the magnitude's result is negated for x < 0
        var magnitude = Math.Abs(x);
        var result = RoundCorrectly(precision => Trigonometric(magnitude, precision, sine: true), out steps);
        return x < 0 ? -result : result;
    }

    /// <summary>
    ///     Computes JavaScript's <c>Math.cos</c> correctly rounded.
    /// </summary>
    /// <param name="x">The angle in radians: finite, with a magnitude of at most <see cref="TrigLimit"/>.</param>
    /// <param name="steps">The number of higher precisions the rounding needed (0 for nearly every argument).</param>
    /// <returns>The double nearest to the cosine of <paramref name="x"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="x"/> is NaN, infinite or larger than <see cref="TrigLimit"/> in magnitude.
    /// </exception>
    internal static double Cos(double x, out int steps)
    {
        // The cosine is even; cos(0) = 1 is exact
        CheckTrigArgument(x);
        if (JsEquals(x, 0))
        {
            steps = 0;
            return 1;
        }

        var magnitude = Math.Abs(x);
        return RoundCorrectly(precision => Trigonometric(magnitude, precision, sine: false), out steps);
    }

    /// <summary>
    ///     Computes JavaScript's <c>Math.log</c> (the natural logarithm) correctly rounded.
    /// </summary>
    /// <param name="x">The argument: positive and finite (subnormal values included).</param>
    /// <param name="steps">The number of higher precisions the rounding needed (0 for nearly every argument).</param>
    /// <returns>The double nearest to ln <paramref name="x"/>; exactly 0 for 1.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="x"/> is NaN, not positive or infinite (the layout never makes such an argument).
    /// </exception>
    internal static double Log(double x, out int steps)
    {
        // ln 1 = 0 is the only exact value (and the only one Ziv's test could not decide)
        if (!(x > 0) || double.IsPositiveInfinity(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "the logarithm is computed for positive finite arguments only");
        }

        if (JsEquals(x, 1))
        {
            steps = 0;
            return 0;
        }

        return RoundCorrectly(precision => (FixedLog(x, precision + GuardBits), -(precision + GuardBits)), out steps);
    }

    /// <summary>
    ///     Computes V8's <c>Math.pow</c>: its special cases, and the correctly rounded power of a positive base.
    /// </summary>
    /// <remarks>
    ///     V8 handles, in order: a NaN exponent (NaN); an infinite exponent with a base of ±1 (NaN); an exponent of 2
    ///     (<c>x*x</c>, as its optimizing compilers do); an exponent of 0.5 (<c>sqrt(x + 0)</c>, or +∞ for an infinite
    ///     base). ECMAScript then gives 1 for an exponent of ±0, NaN for a NaN base, and 1 for a base of 1. Any other pair
    ///     must have a positive finite base and a finite exponent; the result is the double nearest to <c>x^y</c>, which
    ///     over- or underflows to +∞ or +0 as rounding to nearest gives.
    /// </remarks>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <param name="steps">The number of higher precisions the rounding needed (0 for nearly every argument).</param>
    /// <returns>The power.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown for a pair outside the special cases whose base is not positive and finite or whose exponent is not
    ///     finite (the layout never makes such a pair), or whose power lies too close to a rounding midpoint to round.
    /// </exception>
    internal static double Pow(double x, double y, out int steps)
    {
        // The special cases of V8's math::pow and of ECMAScript's Number::exponentiate, in V8's order
        steps = 0;
        if (double.IsNaN(y) || (double.IsInfinity(y) && (JsEquals(x, 1) || JsEquals(x, -1))))
        {
            return double.NaN;
        }

        if (JsEquals(y, 2))
        {
            return x * x;
        }

        if (JsEquals(y, 0.5))
        {
            return double.IsInfinity(x) ? double.PositiveInfinity : Math.Sqrt(x + 0.0);
        }

        if (JsEquals(y, 0))
        {
            return 1;
        }

        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (JsEquals(x, 1))
        {
            return 1;
        }

        if (!(x > 0) || double.IsPositiveInfinity(x) || double.IsInfinity(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "the power is computed for positive finite bases and finite exponents only");
        }

        // |ln x| is at least 2^-53 for x ≠ 1, so |y·ln x| > 2048 when |y| ≥ 2^64: the power over- or underflows
        if (Math.Abs(y) >= PowerExponentLimit)
        {
            return (x > 1) == (y > 0) ? double.PositiveInfinity : 0.0;
        }

        return RoundCorrectly(precision => PowerApproximation(x, y, precision), out steps);
    }

    /// <summary>
    ///     Rounds an exact value to the nearest double with Ziv's strategy: approximates it at increasing precisions until
    ///     both ends of the error interval round to the same double.
    /// </summary>
    /// <param name="approximate">The approximation of the exact value at a precision.</param>
    /// <param name="steps">The number of precisions tried after the first one.</param>
    /// <returns>The double nearest to the exact value (ties to even, as the rounding of both ends is).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the highest precision still cannot decide: the exact value lies within about 2^-700 of a rounding
    ///     midpoint (no argument of the layout does).
    /// </exception>
    internal static double RoundCorrectly(Approximation approximate, out int steps)
    {
        var error = BigInteger.One << ErrorBits;
        for (var step = 0; step < ZivPrecisions.Length; step++)
        {
            // Rounding is monotonic: if both ends of the interval round to the same double, so does every value in it
            var (value, exponent) = approximate(ZivPrecisions[step]);
            var low = RoundToDouble(value - error, exponent);
            var high = RoundToDouble(value + error, exponent);
            if (BitConverter.DoubleToInt64Bits(low) == BitConverter.DoubleToInt64Bits(high))
            {
                steps = step;
                return low;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(approximate),
            "the value lies too close to a rounding midpoint to be rounded correctly");
    }

    /// <summary>
    ///     Rounds <c>value·2^exponent</c> to the nearest double, ties to even, with subnormal results and overflow to
    ///     infinity as IEEE 754 defines them.
    /// </summary>
    /// <param name="value">The integer value (any sign).</param>
    /// <param name="exponent">The binary exponent.</param>
    /// <returns>The nearest double; a nonzero value that rounds to zero keeps its sign.</returns>
    internal static double RoundToDouble(BigInteger value, int exponent)
    {
        if (value.IsZero)
        {
            return 0.0;
        }

        // The weight of the last bit kept: 53 significant bits for a normal result, 2^-1074 for a subnormal one
        var magnitude = BigInteger.Abs(value);
        var top = (int)magnitude.GetBitLength() - 1 + exponent;
        var last = Math.Max(top - (SignificandBits - 1), -1074);
        var drop = last - exponent;

        // Keep the bits above the last one and round the dropped ones to nearest, ties to even
        BigInteger significand;
        if (drop <= 0)
        {
            significand = magnitude << -drop;
        }
        else
        {
            significand = magnitude >> drop;
            var rest = magnitude - (significand << drop);
            var half = BigInteger.One << (drop - 1);
            if (rest > half || (rest == half && !significand.IsEven))
            {
                significand += 1;
            }
        }

        // A carry out of the top bit adds one to the exponent
        if (significand == BigInteger.One << SignificandBits)
        {
            significand >>= 1;
            last++;
        }

        // Assemble the bits: a subnormal (or zero) significand is the bit pattern itself; a normal one gets its exponent
        double result;
        var implicitBit = 1L << (SignificandBits - 1);
        var bits = (long)significand;
        if (bits < implicitBit)
        {
            result = BitConverter.Int64BitsToDouble(bits);
        }
        else
        {
            var biased = last + (SignificandBits - 1) + 1023;
            result = biased >= 2047
                ? double.PositiveInfinity
                : BitConverter.Int64BitsToDouble(((long)biased << (SignificandBits - 1)) | (bits - implicitBit));
        }

        return value.Sign < 0 ? -result : result;
    }

    /// <summary>
    ///     Checks the argument of a sine or cosine.
    /// </summary>
    /// <param name="x">The argument.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the argument is NaN, infinite or too large.</exception>
    private static void CheckTrigArgument(double x)
    {
        if (!(Math.Abs(x) <= TrigLimit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                x,
                string.Create(CultureInfo.InvariantCulture, $"the sine and cosine are computed for arguments of magnitude {TrigLimit} or less"));
        }
    }

    /// <summary>
    ///     Approximates the sine or cosine of a positive angle at a precision.
    /// </summary>
    /// <param name="angle">The angle: positive, at most <see cref="TrigLimit"/>.</param>
    /// <param name="precision">The precision in bits.</param>
    /// <param name="sine">Whether the sine (else the cosine) is wanted.</param>
    /// <returns>The approximation.</returns>
    private static (BigInteger Value, int Exponent) Trigonometric(double angle, int precision, bool sine)
    {
        var (significand, exponent) = Decompose(angle);
        int scale;
        BigInteger reduced;
        int quadrant;
        if (angle < Math.PI / 4)
        {
            // No reduction: the scale grows for a small angle, whose sine is about as small, so the result keeps its
            // relative precision (the shift is exact: the scale exceeds the angle's binary exponent)
            scale = precision + GuardBits + Math.Max(0, -ILogB(angle));
            reduced = significand << (exponent + scale);
            quadrant = 0;
        }
        else
        {
            // Reduce by the nearest multiple k of π/2 (k ≤ 5): r = x - k·π/2 with |r| ≤ π/4, erring by 2k units
            scale = precision + GuardBits + 8;
            var x = significand << (exponent + scale);
            var halfPi = FixedPi(scale - 1);
            var k = ((x << 1) + halfPi) / (halfPi << 1);
            reduced = x - (k * halfPi);
            quadrant = (int)(k % 4);
        }

        // sin(r + kπ/2) and cos(r + kπ/2) are ±sin r or ±cos r by the quadrant
        var useSine = sine ? quadrant % 2 == 0 : quadrant % 2 == 1;
        var negate = sine ? quadrant >= 2 : quadrant is 1 or 2;
        var value = useSine ? SineSeries(reduced, scale) : CosineSeries(reduced, scale);
        return (negate ? -value : value, -scale);
    }

    /// <summary>
    ///     Sums the Taylor series of the sine in fixed point.
    /// </summary>
    /// <param name="r">The angle at the scale, with a magnitude below 1.</param>
    /// <param name="scale">The scale (the number of fraction bits).</param>
    /// <returns>sin r at the scale; each term errs by less than two units.</returns>
    private static BigInteger SineSeries(BigInteger r, int scale)
    {
        // r - r^3/3! + r^5/5! - ..., each term the previous one times -r^2/((2k)(2k+1))
        var square = (r * r) >> scale;
        var term = r;
        var sum = r;
        var k = 0;
        while (!term.IsZero)
        {
            k++;
            term = -((term * square) >> scale) / (2 * k * ((2 * k) + 1));
            sum += term;
        }

        return sum;
    }

    /// <summary>
    ///     Sums the Taylor series of the cosine in fixed point.
    /// </summary>
    /// <param name="r">The angle at the scale, with a magnitude below 1.</param>
    /// <param name="scale">The scale (the number of fraction bits).</param>
    /// <returns>cos r at the scale; each term errs by less than two units.</returns>
    private static BigInteger CosineSeries(BigInteger r, int scale)
    {
        // 1 - r^2/2! + r^4/4! - ..., each term the previous one times -r^2/((2k-1)(2k))
        var square = (r * r) >> scale;
        var term = BigInteger.One << scale;
        var sum = term;
        var k = 0;
        while (!term.IsZero)
        {
            k++;
            term = -((term * square) >> scale) / (((2 * k) - 1) * 2 * k);
            sum += term;
        }

        return sum;
    }

    /// <summary>
    ///     Computes the natural logarithm of a positive finite double in fixed point.
    /// </summary>
    /// <param name="x">The argument: positive and finite.</param>
    /// <param name="scale">The scale (the number of fraction bits); at least 64.</param>
    /// <returns>ln x at the scale, within 2^12 units.</returns>
    private static BigInteger FixedLog(double x, int scale)
    {
        // x = f·2^k with f in [0.75, 1.5): the significand's leading bit gives k, and f ≥ 1.5 is halved
        var (significand, exponent) = Decompose(x);
        var length = (int)significand.GetBitLength();
        var k = exponent + length - 1;
        var shift = scale - length + 1;
        if (significand * 2 >= 3 * (BigInteger.One << (length - 1)))
        {
            k++;
            shift--;
        }

        // ln f = 2·atanh(t) with t = (f - 1)/(f + 1), |t| ≤ 1/5: t + t^3/3 + t^5/5 + ...; the series is summed for |t|
        // (atanh is odd), so the shifts, which round toward negative infinity, shrink every power to zero
        var one = BigInteger.One << scale;
        var f = significand << shift;
        var t = ((f - one) << scale) / (f + one);
        var square = (t * t) >> scale;
        var power = BigInteger.Abs(t);
        var sum = power;
        var j = 0;
        while (!power.IsZero)
        {
            j++;
            power = (power * square) >> scale;
            sum += power / ((2 * j) + 1);
        }

        // ln x = k·ln 2 + ln f (k·ln 2 errs by at most 2·1075 units)
        return (k * FixedLn2(scale)) + (t.Sign < 0 ? -(sum << 1) : sum << 1);
    }

    /// <summary>
    ///     Approximates <c>x^y = exp(y·ln x)</c> for a positive finite base other than 1 and a finite exponent of
    ///     magnitude below 2^64.
    /// </summary>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <param name="precision">The precision in bits.</param>
    /// <returns>The approximation.</returns>
    private static (BigInteger Value, int Exponent) PowerApproximation(double x, double y, int precision)
    {
        // z = y·ln x at the working scale: ln x gets the extra bits that the multiplication by y (< 2^(extra - 8))
        // consumes, so z errs by fewer than 17 units
        var scale = precision + GuardBits;
        var extra = Math.Max(0, ILogB(Math.Abs(y)) + 1) + 8;
        var log = FixedLog(x, scale + extra);
        var (ySignificand, yExponent) = Decompose(Math.Abs(y));
        var product = (y < 0 ? -ySignificand : ySignificand) * log;
        var shift = extra - yExponent;
        var z = shift >= 0 ? product >> shift : product << -shift;

        // Far beyond the double range the result is +∞ or +0: these approximations round to them at either end
        var one = BigInteger.One << scale;
        if (z > 710 * one)
        {
            return (BigInteger.One << 30, 1100);
        }

        if (z < -746 * one)
        {
            return (BigInteger.One << 30, -1200);
        }

        // exp(z) = 2^n·exp(r) with n the nearest integer to z/ln 2 and |r| ≤ ln 2/2 (|n| ≤ 1077, so r errs by fewer
        // than 2·1077 + 17 units)
        var ln2 = FixedLn2(scale);
        var n = FloorDivide((z << 1) + ln2, ln2 << 1);
        var r = z - (n * ln2);

        // exp(r) = 1 + r + r^2/2! + ..., each term the previous one times r/j
        var term = one;
        var sum = one;
        var j = 0;
        while (!term.IsZero)
        {
            j++;
            term = ((term * r) >> scale) / j;
            sum += term;
        }

        return (sum, (int)n - scale);
    }

    /// <summary>
    ///     Gets π at a scale, truncated from the cached constant.
    /// </summary>
    /// <param name="scale">The scale; at most <see cref="ConstantBits"/>.</param>
    /// <returns>π·2^scale within two units.</returns>
    private static BigInteger FixedPi(int scale) => PiConstant >> (ConstantBits - scale);

    /// <summary>
    ///     Gets ln 2 at a scale, truncated from the cached constant.
    /// </summary>
    /// <param name="scale">The scale; at most <see cref="ConstantBits"/>.</param>
    /// <returns>ln 2·2^scale within two units.</returns>
    private static BigInteger FixedLn2(int scale) => Ln2Constant >> (ConstantBits - scale);

    /// <summary>
    ///     Computes π with Machin's formula, π = 16·atan(1/5) - 4·atan(1/239).
    /// </summary>
    /// <returns>π·2^<see cref="ConstantBits"/>, within one unit.</returns>
    private static BigInteger ComputePi()
    {
        // 32 guard bits absorb the truncation of every series term
        var one = BigInteger.One << (ConstantBits + 32);
        var pi = (16 * InverseArcTangent(5, one)) - (4 * InverseArcTangent(239, one));
        return pi >> 32;
    }

    /// <summary>
    ///     Computes ln 2 = 2·atanh(1/3).
    /// </summary>
    /// <returns>ln 2·2^<see cref="ConstantBits"/>, within one unit.</returns>
    private static BigInteger ComputeLn2()
    {
        // 2·(1/3 + 1/(3·3^3) + 1/(5·3^5) + ...)
        var one = BigInteger.One << (ConstantBits + 32);
        var power = one / 3;
        var sum = power;
        var k = 0;
        while (!power.IsZero)
        {
            k++;
            power /= 9;
            sum += power / ((2 * k) + 1);
        }

        return (sum << 1) >> 32;
    }

    /// <summary>
    ///     Sums the series atan(1/n) = 1/n - 1/(3n^3) + 1/(5n^5) - ... in fixed point.
    /// </summary>
    /// <param name="n">The reciprocal of the argument (at least 2).</param>
    /// <param name="one">One at the scale.</param>
    /// <returns>atan(1/n) at the scale.</returns>
    private static BigInteger InverseArcTangent(int n, BigInteger one)
    {
        var power = one / n;
        var sum = power;
        var square = n * n;
        var k = 0;
        while (!power.IsZero)
        {
            k++;
            power /= square;
            var term = power / ((2 * k) + 1);
            sum += k % 2 == 1 ? -term : term;
        }

        return sum;
    }

    /// <summary>
    ///     Splits a positive finite double into an integer significand and a binary exponent.
    /// </summary>
    /// <param name="value">The value: positive and finite.</param>
    /// <returns>The significand (below 2^53) and exponent, with value = significand·2^exponent.</returns>
    private static (BigInteger Significand, int Exponent) Decompose(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biased = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & ((1L << 52) - 1);
        return biased == 0 ? (fraction, -1074) : (fraction | (1L << 52), biased - 1075);
    }

    /// <summary>
    ///     Gets the binary exponent of a positive finite double's leading bit (the floor of its base-2 logarithm).
    /// </summary>
    /// <param name="value">The value: positive and finite.</param>
    /// <returns>The exponent.</returns>
    private static int ILogB(double value)
    {
        var (significand, exponent) = Decompose(value);
        return (int)significand.GetBitLength() - 1 + exponent;
    }

    /// <summary>
    ///     Divides two integers, rounding toward negative infinity.
    /// </summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The divisor: positive.</param>
    /// <returns>The floor of the quotient.</returns>
    private static BigInteger FloorDivide(BigInteger dividend, BigInteger divisor)
    {
        var quotient = BigInteger.DivRem(dividend, divisor, out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }
}
