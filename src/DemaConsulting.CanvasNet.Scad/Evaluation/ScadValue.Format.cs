using System.Runtime.CompilerServices;
using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Text forms of <see cref="ScadValue"/> for <c>str()</c> and <c>echo</c>.
/// </summary>
/// <remarks>
///     <para>
///         The forms are deterministic and culture-invariant: numbers use <see cref="ScadNumberFormatter"/>;
///         booleans are <c>true</c> and <c>false</c>; <c>undef</c> is <c>undef</c>; a vector is <c>[1, 2, 3]</c>
///         (elements separated by a comma and a space); a range is <c>[start : step : end]</c> in evaluation order;
///         a function is <c>function(a, b)</c> with its parameter names.
///     </para>
///     <para>
///         The <c>str()</c> form (<see cref="ToStr"/>) writes a top-level string raw while strings nested in a vector
///         are quoted. The echo form (<see cref="ToEchoText"/>) quotes every string. Quoted strings escape the
///         backslash, the double quote and the characters line feed, carriage return and tab, so a formatted value
///         never contains a raw line break unless a raw top-level <c>str()</c> string did.
///     </para>
/// </remarks>
internal readonly partial struct ScadValue
{
    /// <summary>
    ///     Gets the <c>str()</c> text of the value.
    /// </summary>
    /// <returns>The text; a top-level string is returned unchanged.</returns>
    public string ToStr() => Format(quoteStrings: false);

    /// <summary>
    ///     Gets the echo text of the value.
    /// </summary>
    /// <returns>The text; every string, including a top-level one, is quoted and escaped.</returns>
    public string ToEchoText() => Format(quoteStrings: true);

    /// <summary>
    ///     Gets the <c>str()</c> text unless it would be longer than a limit.
    /// </summary>
    /// <remarks>
    ///     Formatting stops as soon as the limit is exceeded, so a very large value never builds a very large string.
    /// </remarks>
    /// <param name="maxLength">The maximum text length in UTF-16 units.</param>
    /// <param name="text">The text when it fits; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the text fits within <paramref name="maxLength"/>.</returns>
    public bool TryToStr(int maxLength, out string text) => TryFormat(quoteStrings: false, maxLength, out text);

    /// <summary>
    ///     Gets the echo text unless it would be longer than a limit.
    /// </summary>
    /// <param name="maxLength">The maximum text length in UTF-16 units.</param>
    /// <param name="text">The text when it fits; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the text fits within <paramref name="maxLength"/>.</returns>
    public bool TryToEchoText(int maxLength, out string text) => TryFormat(quoteStrings: true, maxLength, out text);

    /// <summary>
    ///     Formats the value without a length limit.
    /// </summary>
    /// <param name="quoteStrings">Whether a top-level string is quoted.</param>
    /// <returns>The text.</returns>
    private string Format(bool quoteStrings)
    {
        TryFormat(quoteStrings, int.MaxValue, out var text);
        return text;
    }

    /// <summary>
    ///     Formats the value within a length limit.
    /// </summary>
    /// <param name="quoteStrings">Whether a top-level string is quoted.</param>
    /// <param name="maxLength">The maximum text length.</param>
    /// <param name="text">The text when it fits; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the text fits.</returns>
    private bool TryFormat(bool quoteStrings, int maxLength, out string text)
    {
        var builder = new StringBuilder();
        var fits = Write(builder, quoteStrings, maxLength);
        text = fits ? builder.ToString() : string.Empty;
        return fits;
    }

    /// <summary>
    ///     Appends the text of this value to a builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="quoteStrings">Whether a string value is quoted.</param>
    /// <param name="maxLength">The maximum builder length.</param>
    /// <returns><see langword="false"/> as soon as the builder exceeds <paramref name="maxLength"/>.</returns>
    private bool Write(StringBuilder builder, bool quoteStrings, int maxLength)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        switch (_kind)
        {
            case ScadValueKind.Undef:
                builder.Append("undef");
                break;
            case ScadValueKind.Boolean:
                builder.Append(_number != 0 ? "true" : "false");
                break;
            case ScadValueKind.Number:
                builder.Append(ScadNumberFormatter.Format(_number));
                break;
            case ScadValueKind.String:
                AppendString(builder, (string)_reference!, quoteStrings);
                break;
            case ScadValueKind.Vector:
                return WriteVector(builder, (ScadVector)_reference!, maxLength);
            case ScadValueKind.Range:
                WriteRange(builder, (ScadRange)_reference!);
                break;
            default:
                builder.Append("function(").AppendJoin(", ", ((IScadFunctionValue)_reference!).ParameterNames).Append(')');
                break;
        }

        return builder.Length <= maxLength;
    }

    /// <summary>
    ///     Appends a vector as <c>[a, b, c]</c>; nested strings are quoted.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="vector">The vector.</param>
    /// <param name="maxLength">The maximum builder length.</param>
    /// <returns><see langword="false"/> as soon as the builder exceeds <paramref name="maxLength"/>.</returns>
    private static bool WriteVector(StringBuilder builder, ScadVector vector, int maxLength)
    {
        builder.Append('[');
        for (var i = 0; i < vector.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            if (!vector[i].Write(builder, quoteStrings: true, maxLength))
            {
                return false;
            }
        }

        builder.Append(']');
        return builder.Length <= maxLength;
    }

    /// <summary>
    ///     Appends a range as <c>[start : step : end]</c>.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="range">The range.</param>
    private static void WriteRange(StringBuilder builder, ScadRange range) =>
        builder.Append('[')
            .Append(ScadNumberFormatter.Format(range.Start)).Append(" : ")
            .Append(ScadNumberFormatter.Format(range.Step)).Append(" : ")
            .Append(ScadNumberFormatter.Format(range.End)).Append(']');

    /// <summary>
    ///     Appends a string, optionally quoted and escaped.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="text">The string.</param>
    /// <param name="quote">Whether to quote and escape.</param>
    private static void AppendString(StringBuilder builder, string text, bool quote)
    {
        if (!quote)
        {
            builder.Append(text);
            return;
        }

        builder.Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        builder.Append('"');
    }
}
