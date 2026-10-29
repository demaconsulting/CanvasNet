using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The regular expression functions and the functions that accept a regular expression (a partial file of the
///     functions unit).
/// </summary>
/// <remarks>
///     <para>
///         <c>regexp(pattern[, flags])</c> builds a regular expression, <c>test(regexp, string)</c> tests a string,
///         <c>replace(string, pattern, replacement)</c> replaces the first occurrence of a text pattern or the matches of a
///         regular expression (every match with the <c>g</c> flag, with JavaScript's <c>$&amp;</c>, <c>$1</c>,
///         <c>$&lt;name&gt;</c>, <c>$$</c>, <c>$`</c> and <c>$'</c> replacement patterns), and
///         <c>split(string, separator[, limit])</c> splits at a text or a regular expression.
///     </para>
///     <para>
///         Regular expressions are built by <see cref="ExpressionRegex"/> (linear-time engine where possible, bounded
///         pattern length, timeout that fails closed). A regular expression that exceeds its time limit, an invalid
///         pattern or an unsupported flag throws <see cref="ExpressionRuntimeException"/>, which the evaluator reports
///         with the location of the expression. Matching work is charged to the step budget.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>
    ///     Adds the regular expression functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddRegex(Dictionary<string, ExpressionFunction> table)
    {
        table["regexp"] = (a, c) => DataValue.FromRegExp(CreateRegex(a, c));
        table["test"] = Test;
        table["replace"] = Replace;
        table["split"] = Split;
    }

    /// <summary>
    ///     Builds the regular expression of a <c>regexp</c> call.
    /// </summary>
    /// <param name="arguments">The pattern and optional flags.</param>
    /// <param name="context">The context.</param>
    /// <returns>The regular expression.</returns>
    private static ExpressionRegex CreateRegex(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var flags = IsAbsent(arguments, 1) ? string.Empty : Text(arguments, 1, context);
        var first = Arg(arguments, 0);
        if (first.IsRegExp)
        {
            var existing = (ExpressionRegex)first.RegExpPayload!;
            return IsAbsent(arguments, 1) ? existing : context.GetRegex(existing.Source, flags);
        }

        return context.GetRegex(first.IsNull && arguments.Length == 0 ? string.Empty : Text(arguments, 0, context), flags);
    }

    /// <summary>
    ///     Gets a regular expression from an argument: a regular expression value, or text taken as a pattern.
    /// </summary>
    /// <param name="value">The argument.</param>
    /// <param name="context">The context.</param>
    /// <returns>The regular expression.</returns>
    private static ExpressionRegex AsRegex(DataValue value, ExpressionFunctionContext context) =>
        value.IsRegExp
            ? (ExpressionRegex)value.RegExpPayload!
            : context.GetRegex(ExpressionEvaluator.ToJsString(value, context.MaxStringLength), string.Empty);

    /// <summary>
    ///     The <c>test(regexp, string)</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>True when the string matches.</returns>
    private static DataValue Test(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var regex = AsRegex(Arg(arguments, 0), context);
        var text = Text(arguments, 1, context);
        context.ChargeSize(text.Length);
        try
        {
            return DataValue.FromBoolean(regex.Regex.IsMatch(text));
        }
        catch (RegexMatchTimeoutException)
        {
            throw TimedOut(regex);
        }
    }

    /// <summary>
    ///     The <c>replace(string, pattern, replacement)</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The new string.</returns>
    private static DataValue Replace(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var input = Text(arguments, 0, context);
        var replacement = Text(arguments, 2, context);
        var pattern = Arg(arguments, 1);
        context.ChargeSize(input.Length);
        var result = new StringBuilder();
        if (!pattern.IsRegExp)
        {
            // A text pattern replaces its first occurrence only
            var search = Text(arguments, 1, context);
            var index = input.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
            {
                return DataValue.FromString(input);
            }

            result.Append(input, 0, index);
            ExpandReplacement(result, replacement, input, index, search.Length, null);
            result.Append(input, index + search.Length, input.Length - index - search.Length);
            context.CheckStringLength(result.Length);
            return DataValue.FromString(result.ToString());
        }

        var regex = (ExpressionRegex)pattern.RegExpPayload!;
        try
        {
            var last = 0;
            var match = regex.Regex.Match(input);
            while (match.Success)
            {
                context.ChargeSize(64);
                result.Append(input, last, match.Index - last);
                ExpandReplacement(result, replacement, input, match.Index, match.Length, match);
                context.CheckStringLength(result.Length);
                last = match.Index + match.Length;
                if (!regex.IsGlobal)
                {
                    break;
                }

                match = match.NextMatch();
            }

            result.Append(input, last, input.Length - last);
        }
        catch (RegexMatchTimeoutException)
        {
            throw TimedOut(regex);
        }

        context.CheckStringLength(result.Length);
        return DataValue.FromString(result.ToString());
    }

    /// <summary>
    ///     The <c>split(string, separator[, limit])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The array of parts.</returns>
    private static DataValue Split(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var input = Text(arguments, 0, context);
        var separator = Arg(arguments, 1);
        var limit = IsAbsent(arguments, 2) ? uint.MaxValue : unchecked((uint)ExpressionEvaluator.ToInt32(Num(arguments, 2)));
        context.ChargeSize(input.Length);
        var parts = new List<DataValue>();
        if (limit == 0)
        {
            return DataValue.FromOwnedArray([]);
        }

        if (separator.IsNull)
        {
            parts.Add(DataValue.FromString(input));
        }
        else if (separator.IsRegExp)
        {
            SplitByRegex((ExpressionRegex)separator.RegExpPayload!, input, limit, parts);
        }
        else
        {
            SplitByText(input, Text(arguments, 1, context), limit, parts);
        }

        context.ChargeSize(parts.Count * 64L);
        return DataValue.FromOwnedArray(parts.ToArray());
    }

    /// <summary>
    ///     Splits a string at a text separator.
    /// </summary>
    /// <param name="input">The string.</param>
    /// <param name="separator">The separator; the empty text splits into UTF-16 code units.</param>
    /// <param name="limit">The most parts to produce.</param>
    /// <param name="parts">Receives the parts.</param>
    private static void SplitByText(string input, string separator, uint limit, List<DataValue> parts)
    {
        if (separator.Length == 0)
        {
            for (var i = 0; i < input.Length && parts.Count < limit; i++)
            {
                parts.Add(DataValue.FromString(input[i].ToString()));
            }

            return;
        }

        var start = 0;
        while (parts.Count < limit)
        {
            var index = input.IndexOf(separator, start, StringComparison.Ordinal);
            if (index < 0)
            {
                parts.Add(DataValue.FromString(input[start..]));
                return;
            }

            parts.Add(DataValue.FromString(input[start..index]));
            start = index + separator.Length;
        }
    }

    /// <summary>
    ///     Splits a string at the matches of a regular expression, including captured groups like JavaScript.
    /// </summary>
    /// <param name="regex">The regular expression.</param>
    /// <param name="input">The string.</param>
    /// <param name="limit">The most parts to produce.</param>
    /// <param name="parts">Receives the parts.</param>
    private static void SplitByRegex(ExpressionRegex regex, string input, uint limit, List<DataValue> parts)
    {
        try
        {
            if (input.Length == 0)
            {
                if (!regex.Regex.IsMatch(input))
                {
                    parts.Add(DataValue.FromString(input));
                }

                return;
            }

            var start = 0;
            for (var match = regex.Regex.Match(input); match.Success; match = match.NextMatch())
            {
                // An empty match at the start of the pending part or at the end of the string splits nothing
                if (match.Index >= input.Length || match.Index + match.Length == start)
                {
                    continue;
                }

                parts.Add(DataValue.FromString(input[start..match.Index]));
                if (parts.Count >= limit)
                {
                    return;
                }

                for (var g = 1; g < match.Groups.Count && parts.Count < limit; g++)
                {
                    parts.Add(match.Groups[g].Success ? DataValue.FromString(match.Groups[g].Value) : default);
                }

                start = match.Index + match.Length;
                if (parts.Count >= limit)
                {
                    return;
                }
            }

            parts.Add(DataValue.FromString(input[start..]));
        }
        catch (RegexMatchTimeoutException)
        {
            throw TimedOut(regex);
        }
    }

    /// <summary>
    ///     Appends a replacement text, expanding JavaScript's <c>$</c> patterns.
    /// </summary>
    /// <param name="result">The text being built.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="input">The string being searched.</param>
    /// <param name="index">The position of the match.</param>
    /// <param name="length">The length of the match.</param>
    /// <param name="match">The regular expression match, or null for a text pattern (which has no groups).</param>
    private static void ExpandReplacement(StringBuilder result, string replacement, string input, int index, int length, Match? match)
    {
        var i = 0;
        while (i < replacement.Length)
        {
            var c = replacement[i];
            if (c != '$' || i + 1 >= replacement.Length)
            {
                result.Append(c);
                i++;
                continue;
            }

            var consumed = 2;
            switch (replacement[i + 1])
            {
                case '$':
                    result.Append('$');
                    break;
                case '&':
                    result.Append(input, index, length);
                    break;
                case '`':
                    result.Append(input, 0, index);
                    break;
                case '\'':
                    result.Append(input, index + length, input.Length - index - length);
                    break;
                default:
                    consumed = 1 + ExpandGroup(result, replacement, i, match);
                    break;
            }

            i += consumed;
        }
    }

    /// <summary>
    ///     Expands a <c>$n</c>, <c>$nn</c> or <c>$&lt;name&gt;</c> group reference at a position of the replacement text.
    /// </summary>
    /// <param name="result">The text being built.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="dollar">The position of the dollar sign.</param>
    /// <param name="match">The regular expression match, or null.</param>
    /// <returns>The number of characters after the dollar sign that were consumed; zero when the text is literal.</returns>
    private static int ExpandGroup(StringBuilder result, string replacement, int dollar, Match? match)
    {
        var next = replacement[dollar + 1];
        if (match is not null && next == '<')
        {
            var close = replacement.IndexOf('>', dollar + 2);
            if (close > 0)
            {
                var name = replacement[(dollar + 2)..close];
                var group = match.Groups[name];
                result.Append(group.Success ? group.Value : string.Empty);
                return close - dollar;
            }
        }

        if (match is not null && next is >= '0' and <= '9')
        {
            var groups = match.Groups.Count - 1;
            var number = next - '0';
            var consumed = 1;
            if (dollar + 2 < replacement.Length && replacement[dollar + 2] is >= '0' and <= '9')
            {
                var twoDigits = (number * 10) + (replacement[dollar + 2] - '0');
                if (twoDigits >= 1 && twoDigits <= groups)
                {
                    number = twoDigits;
                    consumed = 2;
                }
            }

            if (number >= 1 && number <= groups)
            {
                result.Append(match.Groups[number].Success ? match.Groups[number].Value : string.Empty);
                return consumed;
            }
        }

        result.Append('$');
        return 0;
    }

    /// <summary>
    ///     Creates the exception for a regular expression that exceeded its time limit.
    /// </summary>
    /// <param name="regex">The regular expression.</param>
    /// <returns>The exception.</returns>
    private static ExpressionRuntimeException TimedOut(ExpressionRegex regex) =>
        new($"The regular expression {regex} exceeded its match time limit and was stopped.");
}
