using System.Collections.Frozen;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The closed table of functions that expressions may call, and the math functions among them.
/// </summary>
/// <remarks>
///     <para>
///         The table is the whole security boundary of the expression language: a function is available only when it is
///         listed here, there is no reflection, no <c>eval</c>, no file or network access, no user-registered function
///         and no source of randomness or time, so an expression is a pure, deterministic function of its datum and its
///         parameters. The families are the math functions (<c>abs</c> to <c>trunc</c>), the string and array functions
///         (<c>lower</c>, <c>upper</c>, <c>trim</c>, <c>substring</c>, <c>slice</c>, <c>indexof</c>, <c>lastindexof</c>,
///         <c>startswith</c>, <c>endswith</c>, <c>includes</c>, <c>replace</c>, <c>split</c>, <c>join</c>, <c>pad</c>,
///         <c>length</c>), the type and conversion functions (<c>isArray</c> to <c>toString</c>) and the regular expression
///         functions (<c>regexp</c>, <c>test</c> and the regular expression form of <c>replace</c>) and the date and time
///         functions (<c>datetime</c>, <c>year</c>, <c>timeFormat</c>, <c>timeSequence</c> and their relatives, but not the
///         clock function <c>now</c>). Number formatting, color, geographic, scale, statistical and data-flow functions are
///         deferred; an unlisted function is
///         not an error of this unit: the parser reports it and the evaluator gives null.
///     </para>
///     <para>
///         Functions follow JavaScript and Vega semantics with the invariant culture. An argument that is not supplied
///         is JavaScript's <c>undefined</c> (NaN as a number, the text <c>undefined</c> as a string) and null arguments
///         convert as in <see cref="ExpressionEvaluator"/>. Results that could grow without bound (strings, arrays) are
///         checked against <see cref="VegaLiteLimits.MaxStringLength"/>, and work proportional to the input is charged to
///         the step budget. Every function is stateless and thread-safe; the regular expression cache lives in the
///         per-evaluation <see cref="ExpressionFunctionContext"/>. Math results come from the host's math library and
///         can differ from a browser in the last bit for transcendental functions.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>The function table by name.</summary>
    private static readonly FrozenDictionary<string, ExpressionFunction> Table = BuildTable();

    /// <summary>
    ///     Gets the names of all functions in ordinal order.
    /// </summary>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> Names() => Table.Keys.Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    ///     Finds a function by name.
    /// </summary>
    /// <param name="name">The case-sensitive function name.</param>
    /// <param name="function">Receives the function.</param>
    /// <returns><see langword="true"/> when the function is in the table.</returns>
    public static bool TryGet(string name, out ExpressionFunction function)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Table.TryGetValue(name, out function!);
    }

    /// <summary>
    ///     Calls a function by name.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The per-evaluation context.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">Thrown when the function is not in the table.</exception>
    /// <exception cref="ExpressionRuntimeException">Thrown when the call fails (limit exceeded, invalid regular expression).</exception>
    public static DataValue Invoke(string name, ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGet(name, out var function))
        {
            throw new ArgumentException($"The function '{name}' is not in the function table.", nameof(name));
        }

        return function(arguments, context);
    }

    /// <summary>
    ///     Builds the table from the function families.
    /// </summary>
    /// <returns>The table.</returns>
    private static FrozenDictionary<string, ExpressionFunction> BuildTable()
    {
        var table = new Dictionary<string, ExpressionFunction>(StringComparer.Ordinal);
        AddMath(table);
        AddText(table);
        AddTypes(table);
        AddRegex(table);
        AddTime(table);
        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Adds the math functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddMath(Dictionary<string, ExpressionFunction> table)
    {
        table["abs"] = Math1(Math.Abs);
        table["acos"] = Math1(Math.Acos);
        table["acosh"] = Math1(Math.Acosh);
        table["asin"] = Math1(Math.Asin);
        table["asinh"] = Math1(Math.Asinh);
        table["atan"] = Math1(Math.Atan);
        table["atanh"] = Math1(Math.Atanh);
        table["ceil"] = Math1(Math.Ceiling);
        table["cos"] = Math1(Math.Cos);
        table["cosh"] = Math1(Math.Cosh);
        table["exp"] = Math1(Math.Exp);
        table["floor"] = Math1(Math.Floor);
        table["log"] = Math1(Math.Log);
        table["log10"] = Math1(Math.Log10);
        table["round"] = Math1(JsNumber.Round);
        table["sign"] = Math1(Sign);
        table["sin"] = Math1(Math.Sin);
        table["sinh"] = Math1(Math.Sinh);
        table["sqrt"] = Math1(Math.Sqrt);
        table["tan"] = Math1(Math.Tan);
        table["tanh"] = Math1(Math.Tanh);
        table["trunc"] = Math1(Math.Truncate);
        table["atan2"] = (a, _) => DataValue.FromNumber(Math.Atan2(Num(a, 0), Num(a, 1)));
        table["pow"] = (a, _) => DataValue.FromNumber(Power(Num(a, 0), Num(a, 1)));
        table["clamp"] = (a, _) => DataValue.FromNumber(Math.Max(Num(a, 1), Math.Min(Num(a, 2), Num(a, 0))));
        table["max"] = (a, _) => DataValue.FromNumber(Extreme(a, true));
        table["min"] = (a, _) => DataValue.FromNumber(Extreme(a, false));
    }

    /// <summary>
    ///     Creates a function of one numeric argument.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The function.</returns>
    private static ExpressionFunction Math1(Func<double, double> operation) =>
        (a, _) => DataValue.FromNumber(operation(Num(a, 0)));

    /// <summary>
    ///     Gets an argument as a number.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The number; NaN when the argument is not supplied (JavaScript's <c>undefined</c>).</returns>
    private static double Num(ReadOnlySpan<DataValue> arguments, int index) =>
        index < arguments.Length ? ExpressionEvaluator.ToNumber(arguments[index]) : double.NaN;

    /// <summary>
    ///     Gets an argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The argument; null when it is not supplied.</returns>
    private static DataValue Arg(ReadOnlySpan<DataValue> arguments, int index) =>
        index < arguments.Length ? arguments[index] : default;

    /// <summary>
    ///     Gets an argument as text.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <param name="context">The context that supplies the string limit.</param>
    /// <returns>The text; <c>undefined</c> when the argument is not supplied.</returns>
    private static string Text(ReadOnlySpan<DataValue> arguments, int index, ExpressionFunctionContext context) =>
        index < arguments.Length ? ExpressionEvaluator.ToJsString(arguments[index], context.MaxStringLength) : "undefined";

    /// <summary>
    ///     Tells whether an optional argument was not given: it is missing or null.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns><see langword="true"/> when the argument is missing or null.</returns>
    private static bool IsAbsent(ReadOnlySpan<DataValue> arguments, int index) =>
        index >= arguments.Length || arguments[index].IsNull;

    /// <summary>
    ///     The sign of a number as JavaScript's <c>Math.sign</c> defines it.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>1, -1, the zero itself, or NaN.</returns>
    private static double Sign(double value)
    {
        if (double.IsNaN(value) || value == 0)
        {
            return value;
        }

        return value > 0 ? 1 : -1;
    }

    /// <summary>
    ///     Raises a number to a power with JavaScript's special cases.
    /// </summary>
    /// <param name="x">The base.</param>
    /// <param name="y">The exponent.</param>
    /// <returns>The power.</returns>
    private static double Power(double x, double y)
    {
        // JavaScript differs from the C library for a NaN exponent and for +-1 raised to an infinity
        if (double.IsNaN(y) || (double.IsInfinity(y) && Math.Abs(x).CompareTo(1.0) == 0))
        {
            return double.NaN;
        }

        return Math.Pow(x, y);
    }

    /// <summary>
    ///     Computes the maximum or minimum of all arguments like JavaScript's <c>Math.max</c> and <c>Math.min</c>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="maximum">True for the maximum, false for the minimum.</param>
    /// <returns>The extreme; negative or positive infinity without arguments; NaN when any argument is NaN.</returns>
    private static double Extreme(ReadOnlySpan<DataValue> arguments, bool maximum)
    {
        var result = maximum ? double.NegativeInfinity : double.PositiveInfinity;
        foreach (var argument in arguments)
        {
            var number = ExpressionEvaluator.ToNumber(argument);
            if (double.IsNaN(number))
            {
                return double.NaN;
            }

            result = maximum ? Math.Max(result, number) : Math.Min(result, number);
        }

        return result;
    }
}
