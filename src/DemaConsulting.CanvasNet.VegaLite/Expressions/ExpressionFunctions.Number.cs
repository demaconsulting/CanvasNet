// Portions derived from d3-format (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The number formatting function of the closed function table (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     <para>
///         <c>format(value, specifier)</c> is Vega's function of the same name: it formats a number with a d3-format specifier of the
///         en-US locale (<see cref="Scales.NumberFormat"/>, so the minus sign is U+2212 and the thousands separator a comma). A null value gives the
///         text <c>null</c> as in Vega, any other value is read as a number as JavaScript's unary plus reads it (a missing value, an unreadable text and
///         an object are NaN, which formats as <c>NaN</c>), except for the type <c>c</c>, which prints the original value as JavaScript text
///         (<c>x + ""</c>: the text <c>foo</c> stays <c>foo</c> and <c>true</c> is <c>true</c>; the prefix, suffix, width and padding of the specifier still
///         apply). The specifier is read as a text (a missing specifier is the text <c>undefined</c>, which is
///         not a valid specifier). An invalid specifier is a runtime error, as in d3-format which throws.
///     </para>
///     <para>
///         Work is bounded like the other text functions: the specifier and the result are charged by their length, the width of a specifier is capped by
///         <see cref="Scales.NumberFormat"/> and a result longer than <see cref="ExpressionFunctionContext.MaxStringLength"/> is a runtime error. Results are
///         culture invariant.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>
    ///     Adds the number formatting function.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddNumber(Dictionary<string, ExpressionFunction> table) =>
        table["format"] = FormatNumber;

    /// <summary>
    ///     The <c>format</c> function.
    /// </summary>
    /// <param name="arguments">The value and the d3-format specifier.</param>
    /// <param name="context">The context.</param>
    /// <returns>The text; the text <c>null</c> for a null value, after the specifier has been compiled (as Vega), so an invalid or missing specifier is an error even for null.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the specifier is not valid or the text would be longer than the string limit.</exception>
    private static DataValue FormatNumber(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var value = Arg(arguments, 0);

        // Vega compiles the specifier before it looks at the value, so a bad or missing specifier fails even for null
        var specifier = Text(arguments, 1, context);
        context.ChargeSize(specifier.Length);
        var formatter = context.GetNumberFormatter(specifier);
        if (arguments.Length > 0 && value.IsNull)
        {
            return DataValue.FromString("null");
        }

        string text;
        if (formatter.IsText)
        {
            // The type c prints the original value as JavaScript text (x + ""), without reading it as a number
            text = formatter.FormatText(ExpressionEvaluator.ToJsString(value, context.MaxStringLength, context));
        }
        else
        {
            text = formatter.Format(arguments.Length > 0 ? ExpressionEvaluator.ToNumber(value, context) : double.NaN);
        }

        if (text.Length > context.MaxStringLength)
        {
            throw context.StringLimitExceeded();
        }

        context.ChargeSize(text.Length);
        return DataValue.FromString(text);
    }
}
