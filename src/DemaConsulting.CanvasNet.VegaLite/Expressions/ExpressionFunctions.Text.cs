using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The string and array functions of the closed function table (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     Strings are UTF-16 code unit sequences as in JavaScript: indices, lengths and slices count code units, case
///     conversion and comparison are culture invariant and ordinal, and a position outside a string is clamped. The
///     functions that accept both strings and arrays (<c>slice</c>, <c>indexof</c>, <c>lastindexof</c>,
///     <c>includes</c>, <c>length</c>) test the kind of their first argument; any other first argument is converted to
///     its JavaScript text. An optional argument that is missing or null counts as not supplied.
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>
    ///     Adds the string and array functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddText(Dictionary<string, ExpressionFunction> table)
    {
        table["lower"] = (a, c) => CaseConverted(Text(a, 0, c), ToJsLower, c);
        table["upper"] = (a, c) => CaseConverted(Text(a, 0, c), ToJsUpper, c);
        table["trim"] = (a, c) =>
        {
            var text = Text(a, 0, c);
            c.ChargeSize(text.Length);
            return DataValue.FromString(Trim(text));
        };
        table["substring"] = Substring;
        table["slice"] = Slice;
        table["indexof"] = (a, c) => IndexOf(a, c, last: false);
        table["lastindexof"] = (a, c) => IndexOf(a, c, last: true);
        table["startswith"] = StartsWith;
        table["endswith"] = EndsWith;
        table["includes"] = Includes;
        table["join"] = Join;
        table["pad"] = Pad;
        table["length"] = (a, _) => Arg(a, 0) switch
        {
            { IsString: true } s => DataValue.FromNumber(s.Text!.Length),
            { IsArray: true } array => DataValue.FromNumber(array.Elements.Length),
            _ => default,
        };
    }

    /// <summary>
    ///     Applies a case conversion, whose result can be longer than its input, within the string limit.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="convert">The conversion.</param>
    /// <param name="context">The context.</param>
    /// <returns>The converted text.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the result is longer than the string limit.</exception>
    private static DataValue CaseConverted(string text, Func<string, string> convert, ExpressionFunctionContext context)
    {
        // The conversion walks the whole text, so its size is charged before any work is done
        context.ChargeSize(text.Length);
        var converted = convert(text);
        context.CheckStringLength(converted.Length);
        return DataValue.FromString(converted);
    }

    /// <summary>
    ///     Removes JavaScript white space from both ends of a string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && ExpressionEvaluator.IsJsWhitespace(text[start]))
        {
            start++;
        }

        while (end > start && ExpressionEvaluator.IsJsWhitespace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    /// <summary>
    ///     Converts a number to an integer the way JavaScript's <c>ToIntegerOrInfinity</c> does.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Zero for NaN, the truncated number otherwise (infinities are kept).</returns>
    private static double ToInteger(double value) => double.IsNaN(value) ? 0 : Math.Truncate(value);

    /// <summary>
    ///     Clamps a number to a position in a sequence: negative values become zero.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="length">The length of the sequence.</param>
    /// <returns>The position from zero to <paramref name="length"/>.</returns>
    private static int ClampIndex(double value, int length) => (int)Math.Min(Math.Max(ToInteger(value), 0), length);

    /// <summary>
    ///     Converts a position that may count from the end of a sequence.
    /// </summary>
    /// <param name="value">The number; negative values count from the end.</param>
    /// <param name="length">The length of the sequence.</param>
    /// <returns>The position from zero to <paramref name="length"/>.</returns>
    private static int RelativeIndex(double value, int length)
    {
        var integer = ToInteger(value);
        return integer < 0 ? (int)Math.Max(length + integer, 0) : (int)Math.Min(integer, length);
    }

    /// <summary>
    ///     The <c>substring(string, start[, end])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The section; the two positions are clamped and swapped when the first is larger.</returns>
    private static DataValue Substring(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var text = Text(arguments, 0, context);
        var start = ClampIndex(Num(arguments, 1), text.Length);
        var end = IsAbsent(arguments, 2) ? text.Length : ClampIndex(Num(arguments, 2), text.Length);
        context.ChargeSize(end - start);
        return DataValue.FromString(start <= end ? text[start..end] : text[end..start]);
    }

    /// <summary>
    ///     The <c>slice(stringOrArray, start[, end])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The section; negative positions count from the end.</returns>
    private static DataValue Slice(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var first = Arg(arguments, 0);
        var length = first.IsArray ? first.Elements.Length : Text(arguments, 0, context).Length;
        var start = RelativeIndex(Num(arguments, 1), length);
        var end = IsAbsent(arguments, 2) ? length : RelativeIndex(Num(arguments, 2), length);
        var count = Math.Max(end - start, 0);
        context.ChargeSize(count);
        if (first.IsArray)
        {
            return DataValue.FromOwnedArray(first.Elements.Slice(start, count).ToArray());
        }

        return DataValue.FromString(count == 0 ? string.Empty : Text(arguments, 0, context).Substring(start, count));
    }

    /// <summary>
    ///     The <c>indexof</c> and <c>lastindexof</c> functions.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <param name="last">True for the last occurrence, false for the first.</param>
    /// <returns>The index, or -1 when the value does not occur.</returns>
    private static DataValue IndexOf(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, bool last)
    {
        var first = Arg(arguments, 0);
        if (first.IsArray)
        {
            var elements = first.Elements;
            var target = Arg(arguments, 1);
            context.ChargeSize(elements.Length);
            for (var i = 0; i < elements.Length; i++)
            {
                var position = last ? elements.Length - 1 - i : i;
                if (ExpressionEvaluator.StrictEquals(elements[position], target))
                {
                    return DataValue.FromNumber(position);
                }
            }

            return DataValue.FromNumber(-1);
        }

        var text = Text(arguments, 0, context);
        var search = Text(arguments, 1, context);
        context.ChargeSize(text.Length);
        if (last)
        {
            return DataValue.FromNumber(search.Length == 0 ? text.Length : text.LastIndexOf(search, StringComparison.Ordinal));
        }

        return DataValue.FromNumber(text.IndexOf(search, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The <c>startswith(string, search[, position])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>True when the string has <c>search</c> at <c>position</c>.</returns>
    private static DataValue StartsWith(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var text = Text(arguments, 0, context);
        var search = Text(arguments, 1, context);
        var position = IsAbsent(arguments, 2) ? 0 : ClampIndex(Num(arguments, 2), text.Length);
        context.ChargeSize(search.Length);
        return DataValue.FromBoolean(text.AsSpan(position).StartsWith(search, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The <c>endswith(string, search[, endPosition])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>True when the string, cut at <c>endPosition</c>, ends with <c>search</c>.</returns>
    private static DataValue EndsWith(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var text = Text(arguments, 0, context);
        var search = Text(arguments, 1, context);
        var end = IsAbsent(arguments, 2) ? text.Length : ClampIndex(Num(arguments, 2), text.Length);
        context.ChargeSize(search.Length);
        return DataValue.FromBoolean(text.AsSpan(0, end).EndsWith(search, StringComparison.Ordinal));
    }

    /// <summary>
    ///     The <c>includes(stringOrArray, value)</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>True when the array has an element the same value (SameValueZero) or the string contains the text.</returns>
    private static DataValue Includes(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var first = Arg(arguments, 0);
        if (first.IsArray)
        {
            var target = Arg(arguments, 1);
            context.ChargeSize(first.Elements.Length);
            foreach (var element in first.Elements)
            {
                if (element.Equals(target))
                {
                    return DataValue.FromBoolean(true);
                }
            }

            return DataValue.FromBoolean(false);
        }

        var text = Text(arguments, 0, context);
        context.ChargeSize(text.Length);
        return DataValue.FromBoolean(text.Contains(Text(arguments, 1, context), StringComparison.Ordinal));
    }

    /// <summary>
    ///     The <c>join(array[, separator])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The joined text (null elements are empty); null when the first argument is not an array.</returns>
    private static DataValue Join(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var first = Arg(arguments, 0);
        if (!first.IsArray)
        {
            return default;
        }

        var separator = IsAbsent(arguments, 1) ? "," : Text(arguments, 1, context);
        var text = new StringBuilder();
        var elements = first.Elements;
        context.ChargeSize(elements.Length);
        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                text.Append(separator);
            }

            if (!elements[i].IsNull)
            {
                text.Append(ExpressionEvaluator.ToJsString(elements[i], context.MaxStringLength));
            }

            context.CheckStringLength(text.Length);
        }

        context.ChargeSize(text.Length);
        return DataValue.FromString(text.ToString());
    }

    /// <summary>
    ///     The <c>pad(string, length[, character[, align]])</c> function.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>
    ///     The string padded with repeated <c>character</c> (a space by default) up to <c>length</c>: at the end by
    ///     default, at the beginning for <c>left</c>, on both sides for <c>center</c>.
    /// </returns>
    private static DataValue Pad(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var text = Text(arguments, 0, context);
        var padding = IsAbsent(arguments, 2) ? " " : Text(arguments, 2, context);
        if (padding.Length == 0)
        {
            padding = " ";
        }

        var align = IsAbsent(arguments, 3) ? "right" : Text(arguments, 3, context);

        // The number of repetitions is the missing length; check the result size before building it
        var missing = Num(arguments, 1) - text.Length;
        if (!(missing >= 1))
        {
            return DataValue.FromString(text);
        }

        var total = text.Length + (Math.Floor(missing) * padding.Length);
        context.CheckStringLength(total >= long.MaxValue ? long.MaxValue : (long)total);
        var count = (int)missing;
        context.ChargeSize((long)total);
        return DataValue.FromString(align switch
        {
            "left" => Repeat(padding, count) + text,
            "center" => Repeat(padding, count / 2) + text + Repeat(padding, count - (count / 2)),
            _ => text + Repeat(padding, count),
        });
    }

    /// <summary>
    ///     Repeats a string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="count">The number of repetitions.</param>
    /// <returns>The repeated text.</returns>
    private static string Repeat(string text, int count)
    {
        var result = new StringBuilder(text.Length * count);
        for (var i = 0; i < count; i++)
        {
            result.Append(text);
        }

        return result.ToString();
    }
}
