using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The string built-in functions: <c>str</c>, <c>chr</c> and <c>ord</c>, and the shared <c>search</c> helpers.
/// </summary>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>
    ///     Gets the descriptors of the string functions.
    /// </summary>
    /// <returns>The descriptors.</returns>
    private static IEnumerable<Descriptor> StringFunctions()
    {
        yield return Define("str", 0, -1, Str);
        yield return Define("chr", 1, -1, Chr);
        yield return Define("ord", 1, 1, Ord);
    }

    /// <summary>
    ///     Implements <c>str</c>: concatenates the text forms of the arguments.
    /// </summary>
    /// <remarks>
    ///     A string argument contributes its raw text, every other value its text form (see <see cref="ScadValue.ToStr"/>).
    ///     Formatting stops as soon as the result would exceed the string length limit, so a huge value never builds
    ///     a huge string.
    /// </remarks>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The concatenated string.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the result would be longer than the string length limit.</exception>
    private static ScadValue Str(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        var builder = new StringBuilder();
        foreach (var arg in args)
        {
            if (!arg.TryToStr(host.MaxStringLength - builder.Length, out var text, host))
            {
                // The limit check throws; the value is only returned if a host chooses not to
                host.CheckStringLength((long)host.MaxStringLength + 1);
                return ScadValue.Undef;
            }

            builder.Append(text);
        }

        host.Charge(1 + (builder.Length / 16));
        return ScadValue.FromString(builder.ToString());
    }

    /// <summary>
    ///     Implements <c>chr</c>: converts numbers (or vectors of numbers) to the string of their code points.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The string; an element that is not a valid Unicode scalar value contributes nothing and warns.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the result would be longer than the string length limit.</exception>
    private static ScadValue Chr(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        var builder = new StringBuilder();
        foreach (var arg in args)
        {
            if (arg.Kind == ScadValueKind.Vector)
            {
                host.Charge(Math.Max(1, arg.AsVector.Count));
                foreach (var element in arg.AsVector.AsSpan())
                {
                    AppendCodePoint(builder, element, host);
                }
            }
            else
            {
                AppendCodePoint(builder, arg, host);
            }
        }

        return ScadValue.FromString(builder.ToString());
    }

    /// <summary>
    ///     Appends the character of one code point value for <c>chr</c>.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="value">The code point value.</param>
    /// <param name="host">The evaluator services.</param>
    private static void AppendCodePoint(StringBuilder builder, ScadValue value, IScadBuiltinHost host)
    {
        if (value.Kind != ScadValueKind.Number)
        {
            BadArgument(host, "chr", "a number or a vector of numbers", value);
            return;
        }

        var text = ScadText.FromCodePoint(value.AsNumber);
        if (text is null)
        {
            host.Warn("Function 'chr' was given a value that is not a valid Unicode code point");
            return;
        }

        host.CheckStringLength((long)builder.Length + text.Length);
        builder.Append(text);
    }

    /// <summary>
    ///     Implements <c>ord</c>: the code point of the first character of a string.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The code point; <c>undef</c> for the empty string, and <c>undef</c> with a warning for a non-string.</returns>
    private static ScadValue Ord(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        if (args[0].Kind != ScadValueKind.String)
        {
            return BadArgument(host, "ord", "a string", args[0]);
        }

        var text = args[0].AsString;
        return text.Length == 0 ? ScadValue.Undef : ScadValue.FromNumber(ScadText.FirstCodePoint(text));
    }
}
