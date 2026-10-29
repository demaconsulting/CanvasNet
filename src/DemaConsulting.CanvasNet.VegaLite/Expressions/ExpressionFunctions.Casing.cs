using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The case conversion of <c>lower</c> and <c>upper</c> (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     <para>
///         JavaScript's <c>toUpperCase</c> and <c>toLowerCase</c> apply the full case mappings of the Unicode character
///         database, which can change the length of a string (<c>'ß'</c> becomes <c>"SS"</c>), while the invariant culture of
///         .NET applies only the simple one-to-one mappings. This layer applies the unconditional multi-character mappings
///         of <c>SpecialCasing.txt</c> that do not depend on a language (the sharp s, the Latin and Armenian ligatures, the
///         letters with a combining mark, and the Greek letters with an iota subscript or a combining tone, which are all
///         in the basic multilingual plane), the lower case of the capital I with a dot above, and the final sigma rule, and
///         leaves every other character to <see cref="string.ToUpperInvariant"/> and <see cref="string.ToLowerInvariant"/>.
///         Nothing depends on the current culture and the result is the same on every platform.
///     </para>
///     <para>
///         The result can be up to three times as long as the text, so the length of the result is checked against the
///         limit of the evaluation.
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>The combining dot above, which follows the <c>i</c> that the capital I with a dot above lowers to.</summary>
    private const char CombiningDotAbove = '\u0307';

    /// <summary>The Greek capital sigma.</summary>
    private const char CapitalSigma = '\u03A3';

    /// <summary>The Greek small final sigma.</summary>
    private const char FinalSigma = '\u03C2';

    /// <summary>The Greek small sigma.</summary>
    private const char SmallSigma = '\u03C3';

    /// <summary>
    ///     The upper case of the characters whose full mapping is not what the invariant culture of .NET gives: the characters
    ///     whose mapping is not one character, from <c>SpecialCasing.txt</c>, and the dotless i, which the invariant culture
    ///     leaves alone to keep the Turkish letters apart from the Latin ones while JavaScript maps it to <c>I</c>.
    /// </summary>
    /// <remarks>
    ///     The Greek characters with an iota subscript (U+1F80 to U+1FAF) follow a rule and are computed by
    ///     <see cref="TryUpperSpecial"/>.
    /// </remarks>
    private static readonly Dictionary<char, string> UpperSpecials = new()
    {
        ['\u00DF'] = "SS",
        ['\u0131'] = "I",
        ['\u0149'] = "\u02BCN",
        ['\u01F0'] = "J\u030C",
        ['\u0390'] = "\u0399\u0308\u0301",
        ['\u03B0'] = "\u03A5\u0308\u0301",
        ['\u0587'] = "\u0535\u0552",
        ['\u1E96'] = "H\u0331",
        ['\u1E97'] = "T\u0308",
        ['\u1E98'] = "W\u030A",
        ['\u1E99'] = "Y\u030A",
        ['\u1E9A'] = "A\u02BE",
        ['\u1F50'] = "\u03A5\u0313",
        ['\u1F52'] = "\u03A5\u0313\u0300",
        ['\u1F54'] = "\u03A5\u0313\u0301",
        ['\u1F56'] = "\u03A5\u0313\u0342",
        ['\u1FB2'] = "\u1FBA\u0399",
        ['\u1FB3'] = "\u0391\u0399",
        ['\u1FB4'] = "\u0386\u0399",
        ['\u1FB6'] = "\u0391\u0342",
        ['\u1FB7'] = "\u0391\u0342\u0399",
        ['\u1FBC'] = "\u0391\u0399",
        ['\u1FC2'] = "\u1FCA\u0399",
        ['\u1FC3'] = "\u0397\u0399",
        ['\u1FC4'] = "\u0389\u0399",
        ['\u1FC6'] = "\u0397\u0342",
        ['\u1FC7'] = "\u0397\u0342\u0399",
        ['\u1FCC'] = "\u0397\u0399",
        ['\u1FD2'] = "\u0399\u0308\u0300",
        ['\u1FD3'] = "\u0399\u0308\u0301",
        ['\u1FD6'] = "\u0399\u0342",
        ['\u1FD7'] = "\u0399\u0308\u0342",
        ['\u1FE2'] = "\u03A5\u0308\u0300",
        ['\u1FE3'] = "\u03A5\u0308\u0301",
        ['\u1FE4'] = "\u03A1\u0313",
        ['\u1FE6'] = "\u03A5\u0342",
        ['\u1FE7'] = "\u03A5\u0308\u0342",
        ['\u1FF2'] = "\u1FFA\u0399",
        ['\u1FF3'] = "\u03A9\u0399",
        ['\u1FF4'] = "\u038F\u0399",
        ['\u1FF6'] = "\u03A9\u0342",
        ['\u1FF7'] = "\u03A9\u0342\u0399",
        ['\u1FFC'] = "\u03A9\u0399",
        ['\uFB00'] = "FF",
        ['\uFB01'] = "FI",
        ['\uFB02'] = "FL",
        ['\uFB03'] = "FFI",
        ['\uFB04'] = "FFL",
        ['\uFB05'] = "ST",
        ['\uFB06'] = "ST",
        ['\uFB13'] = "\u0544\u0546",
        ['\uFB14'] = "\u0544\u0535",
        ['\uFB15'] = "\u0544\u053B",
        ['\uFB16'] = "\u054E\u0546",
        ['\uFB17'] = "\u0544\u053D",
    };

    /// <summary>
    ///     Converts a string to upper case the way JavaScript's <c>toUpperCase</c> does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The upper case text, which can be longer than <paramref name="text"/>.</returns>
    internal static string ToJsUpper(string text)
    {
        StringBuilder? result = null;
        var run = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!TryUpperSpecial(text[i], out var upper))
            {
                continue;
            }

            result ??= new StringBuilder(text.Length + 8);
            result.Append(text.AsSpan(run, i - run).ToString().ToUpperInvariant()).Append(upper);
            run = i + 1;
        }

        return result is null
            ? text.ToUpperInvariant()
            : result.Append(text.AsSpan(run).ToString().ToUpperInvariant()).ToString();
    }

    /// <summary>
    ///     Converts a string to lower case the way JavaScript's <c>toLowerCase</c> does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lower case text, which can be longer than <paramref name="text"/>.</returns>
    internal static string ToJsLower(string text)
    {
        StringBuilder? result = null;
        var run = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            string? lower = c switch
            {
                '\u0130' => "i" + CombiningDotAbove,
                CapitalSigma => IsFinalSigma(text, i) ? FinalSigma.ToString() : SmallSigma.ToString(),
                _ => null,
            };
            if (lower is null)
            {
                continue;
            }

            result ??= new StringBuilder(text.Length + 8);
            result.Append(text.AsSpan(run, i - run).ToString().ToLowerInvariant()).Append(lower);
            run = i + 1;
        }

        return result is null
            ? text.ToLowerInvariant()
            : result.Append(text.AsSpan(run).ToString().ToLowerInvariant()).ToString();
    }

    /// <summary>
    ///     Gets the full upper case of a character when it is not a single character.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <param name="upper">Receives the upper case text.</param>
    /// <returns><see langword="true"/> when the character has a special mapping.</returns>
    private static bool TryUpperSpecial(char c, out string upper)
    {
        if (c is >= '\u1F80' and <= '\u1FAF')
        {
            // The Greek letters with an iota subscript (or its capital form): the capital letter and a capital iota
            var baseLetter = '\u1F68';
            if (c < '\u1F90')
            {
                baseLetter = '\u1F08';
            }
            else if (c < '\u1FA0')
            {
                baseLetter = '\u1F28';
            }

            upper = string.Concat((char)(baseLetter + (c & 7)), '\u0399');
            return true;
        }

        if (c >= '\u00DF' && UpperSpecials.TryGetValue(c, out var found))
        {
            upper = found;
            return true;
        }

        upper = string.Empty;
        return false;
    }

    /// <summary>
    ///     Determines whether a capital sigma is lowered to the final form: after a cased letter and not before one,
    ///     ignoring the case-ignorable characters between (the Final_Sigma condition of the Unicode standard).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The position of the sigma.</param>
    /// <returns><see langword="true"/> when the final sigma is used.</returns>
    private static bool IsFinalSigma(string text, int index)
    {
        // Before: a cased letter, skipping the case-ignorable characters
        var before = index;
        var precededByCased = false;
        while (before > 0)
        {
            var point = CodePointBefore(text, before, out var length);
            before -= length;
            if (IsCaseIgnorable(point))
            {
                continue;
            }

            precededByCased = IsCased(point);
            break;
        }

        if (!precededByCased)
        {
            return false;
        }

        // After: no cased letter, skipping the case-ignorable characters
        var after = index + 1;
        while (after < text.Length)
        {
            var length = char.IsHighSurrogate(text[after]) && after + 1 < text.Length && char.IsLowSurrogate(text[after + 1]) ? 2 : 1;
            var point = length == 2 ? char.ConvertToUtf32(text[after], text[after + 1]) : text[after];
            after += length;
            if (IsCaseIgnorable(point))
            {
                continue;
            }

            return !IsCased(point);
        }

        return true;
    }

    /// <summary>
    ///     Reads the code point that ends at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="end">The position after the code point.</param>
    /// <param name="length">Receives the number of code units of the code point.</param>
    /// <returns>The code point; a lone surrogate is returned as itself.</returns>
    private static int CodePointBefore(string text, int end, out int length)
    {
        if (end >= 2 && char.IsLowSurrogate(text[end - 1]) && char.IsHighSurrogate(text[end - 2]))
        {
            length = 2;
            return char.ConvertToUtf32(text[end - 2], text[end - 1]);
        }

        length = 1;
        return text[end - 1];
    }

    /// <summary>
    ///     Determines whether a code point is a cased letter: upper case, lower case or title case.
    /// </summary>
    /// <remarks>
    ///     The letters that the character database only lists as other lower case or other upper case are included for the
    ///     ranges that carry a case in practice (the feminine and masculine ordinal indicators, the modifier letters,
    ///     the phonetic extensions, the Roman numerals and the circled letters).
    /// </remarks>
    /// <param name="point">The code point.</param>
    /// <returns><see langword="true"/> when the code point is cased.</returns>
    private static bool IsCased(int point)
    {
        if (point is '\u00AA' or '\u00BA' or (>= '\u02B0' and <= '\u02B8') or '\u02C0' or '\u02C1' or (>= '\u02E0' and <= '\u02E4')
            or (>= '\u1D2C' and <= '\u1D6A') or '\u1D78' or (>= '\u1D9B' and <= '\u1DBF') or '\u2071' or '\u207F'
            or (>= '\u2090' and <= '\u209C') or (>= '\u2160' and <= '\u217F') or (>= '\u24B6' and <= '\u24E9'))
        {
            return true;
        }

        // The category with the value 2 is the title case letter (a few digraph capitals such as the Croatian Dz)
        return CharUnicodeInfo.GetUnicodeCategory(point) is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter or (UnicodeCategory)2;
    }

    /// <summary>
    ///     Determines whether a code point is case-ignorable: a mark, a format character, a modifier or one of the
    ///     punctuation characters that can occur inside a word.
    /// </summary>
    /// <param name="point">The code point.</param>
    /// <returns><see langword="true"/> when the code point is ignored when the context of a case mapping is read.</returns>
    private static bool IsCaseIgnorable(int point)
    {
        if (point is '\'' or '.' or ':' or '\u00B7' or '\u0387' or '\u055F' or '\u05F4' or '\u2018' or '\u2019' or '\u2024'
            or '\u2027' or '\uFE13' or '\uFE52' or '\uFE55' or '\uFF07' or '\uFF0E' or '\uFF1A')
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(point) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol;
    }
}
