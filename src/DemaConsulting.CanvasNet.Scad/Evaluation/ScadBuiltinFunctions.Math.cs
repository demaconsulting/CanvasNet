namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The mathematical built-in functions.
/// </summary>
/// <remarks>
///     Trigonometric functions work in degrees. The sine, cosine and tangent are exact (0, 1 or -1) at every
///     multiple of 90 degrees, because the argument is reduced with the exact floating-point remainder before the
///     platform library is used for the remaining angles. Inverse trigonometric functions return degrees.
/// </remarks>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>The factor that converts degrees to radians.</summary>
    private const double RadiansPerDegree = Math.PI / 180.0;

    /// <summary>The factor that converts radians to degrees.</summary>
    private const double DegreesPerRadian = 180.0 / Math.PI;

    /// <summary>
    ///     Gets the descriptors of the mathematical functions.
    /// </summary>
    /// <returns>The descriptors.</returns>
    private static IEnumerable<Descriptor> MathFunctions()
    {
        yield return Scalar1("abs", Math.Abs);
        yield return Scalar1("sign", Sign);
        yield return Scalar1("sin", SinDegrees);
        yield return Scalar1("cos", CosDegrees);
        yield return Scalar1("tan", TanDegrees);
        yield return Scalar1("asin", x => Math.Asin(x) * DegreesPerRadian);
        yield return Scalar1("acos", x => Math.Acos(x) * DegreesPerRadian);
        yield return Scalar1("atan", x => Math.Atan(x) * DegreesPerRadian);
        yield return Scalar1("floor", Math.Floor);
        yield return Scalar1("ceil", Math.Ceiling);
        yield return Scalar1("round", x => Math.Round(x, MidpointRounding.AwayFromZero));
        yield return Scalar1("ln", Math.Log);
        yield return Scalar1("log", Math.Log10);
        yield return Scalar1("sqrt", Math.Sqrt);
        yield return Scalar1("exp", Math.Exp);
        yield return Scalar2("atan2", (y, x) => Math.Atan2(y, x) * DegreesPerRadian, "y", "x");
        yield return Scalar2("pow", Math.Pow, "base", "exponent");
        yield return Define("min", 1, -1, (args, host) => Extreme("min", args, host, takeMaximum: false));
        yield return Define("max", 1, -1, (args, host) => Extreme("max", args, host, takeMaximum: true));
    }

    /// <summary>
    ///     Creates a descriptor for a one-argument numeric function.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The numeric function.</param>
    /// <returns>The descriptor.</returns>
    private static Descriptor Scalar1(string name, Func<double, double> function) =>
        Define(
            name,
            1,
            1,
            (args, host) => args[0].Kind == ScadValueKind.Number
                ? ScadValue.FromNumber(function(args[0].AsNumber))
                : BadArgument(host, name, "a number", args[0]));

    /// <summary>
    ///     Creates a descriptor for a two-argument numeric function.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="function">The numeric function.</param>
    /// <param name="first">The name of the first parameter.</param>
    /// <param name="second">The name of the second parameter.</param>
    /// <returns>The descriptor.</returns>
    private static Descriptor Scalar2(string name, Func<double, double, double> function, string first, string second) =>
        Define(
            name,
            2,
            2,
            (args, host) =>
            {
                if (args[0].Kind != ScadValueKind.Number)
                {
                    return BadArgument(host, name, "a number", args[0]);
                }

                return args[1].Kind == ScadValueKind.Number
                    ? ScadValue.FromNumber(function(args[0].AsNumber, args[1].AsNumber))
                    : BadArgument(host, name, "a number", args[1]);
            },
            first,
            second);

    /// <summary>
    ///     Computes the sign of a number.
    /// </summary>
    /// <param name="x">The number.</param>
    /// <returns>-1, 0 or 1; <c>NaN</c> for <c>NaN</c>.</returns>
    private static double Sign(double x)
    {
        if (double.IsNaN(x))
        {
            return double.NaN;
        }

        if (x > 0)
        {
            return 1;
        }

        return x < 0 ? -1 : 0;
    }

    /// <summary>
    ///     Computes the sine of an angle in degrees.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The sine; exactly 0, 1 or -1 at multiples of 90 degrees.</returns>
    private static double SinDegrees(double degrees)
    {
        var quadrant = ExactQuadrant(degrees);
        if (quadrant >= 0)
        {
            return quadrant switch { 1 => 1, 3 => -1, _ => 0 };
        }

        return Math.Sin(degrees % 360.0 * RadiansPerDegree);
    }

    /// <summary>
    ///     Computes the cosine of an angle in degrees.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The cosine; exactly 0, 1 or -1 at multiples of 90 degrees.</returns>
    private static double CosDegrees(double degrees)
    {
        var quadrant = ExactQuadrant(degrees);
        if (quadrant >= 0)
        {
            return quadrant switch { 0 => 1, 2 => -1, _ => 0 };
        }

        return Math.Cos(degrees % 360.0 * RadiansPerDegree);
    }

    /// <summary>
    ///     Computes the tangent of an angle in degrees.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The tangent; 0 at 0 and 180 degrees, positive infinity at 90 and negative infinity at 270.</returns>
    private static double TanDegrees(double degrees)
    {
        var quadrant = ExactQuadrant(degrees);
        if (quadrant >= 0)
        {
            return quadrant switch { 1 => double.PositiveInfinity, 3 => double.NegativeInfinity, _ => 0 };
        }

        return Math.Tan(degrees % 360.0 * RadiansPerDegree);
    }

    /// <summary>
    ///     Finds out whether an angle is an exact multiple of 90 degrees.
    /// </summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The quadrant index (0 for 0 degrees, 1 for 90, 2 for 180, 3 for 270) modulo 360 degrees; -1 when the angle is not an exact multiple or is not finite.</returns>
    private static int ExactQuadrant(double degrees)
    {
        // The remainder is exact in floating point, so a multiple of 90 is recognized without any tolerance
        var remainder = degrees % 360.0;
        if (remainder < 0)
        {
            remainder += 360.0;
        }

        var quarters = remainder / 90.0;
        var whole = Math.Floor(quarters);
        return ScadNumbers.AreEqual(quarters, whole) ? (int)whole % 4 : -1;
    }

    /// <summary>
    ///     Computes the minimum or maximum of numbers given as arguments or as one vector.
    /// </summary>
    /// <param name="name">The function name, for warnings.</param>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <param name="takeMaximum"><see langword="true"/> for the maximum; <see langword="false"/> for the minimum.</param>
    /// <returns>The extreme value, or <c>undef</c> with a warning when the operands are not numbers or there are none. <c>NaN</c> wins when present.</returns>
    private static ScadValue Extreme(string name, ReadOnlySpan<ScadValue> args, IScadBuiltinHost host, bool takeMaximum)
    {
        // A single vector argument supplies the operands
        var operands = args.Length == 1 && args[0].Kind == ScadValueKind.Vector ? args[0].AsVector.AsSpan() : args;
        if (operands.Length == 0)
        {
            return BadArgument(host, name, "at least one number", args[0]);
        }

        host.Charge(operands.Length);
        var result = double.NaN;
        for (var i = 0; i < operands.Length; i++)
        {
            if (operands[i].Kind != ScadValueKind.Number)
            {
                return BadArgument(host, name, "numbers or a vector of numbers", operands[i]);
            }

            var value = operands[i].AsNumber;
            if (i == 0)
            {
                result = value;
            }
            else
            {
                result = takeMaximum ? Math.Max(result, value) : Math.Min(result, value);
            }
        }

        return ScadValue.FromNumber(result);
    }
}
