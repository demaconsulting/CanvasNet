// Portions derived from sanitize-url (MIT) and mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     The link check Mermaid applies to every link a diagram keeps: an exact port of <c>sanitizeUrl</c> of
///     <c>@braintree/sanitize-url</c> (the version the pinned Mermaid bundles), with the parts of the WHATWG URL parser it
///     calls for <c>http</c> and <c>https</c> links, and of Mermaid's <c>formatUrl</c> at the <c>strict</c> security level.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> A diagram can carry links (the <c>link</c>, <c>click ... href</c> and sequence <c>links</c>/<c>link</c>
///         statements), and a link whose scheme is <c>javascript</c>, <c>data</c> or <c>vbscript</c> runs code in a page that
///         uses it. Mermaid refuses those schemes with sanitize-url, which first undoes every encoding that can hide a
///         scheme; a partial port lets an encoded scheme through. Every diagram type therefore checks its links here, one
///         shared port, and keeps only what this class returns.
///     </para>
///     <para>
///         <b>What is reproduced.</b> The whole of <c>sanitizeUrl</c>: <c>about:blank</c> for no text; JavaScript's
///         <c>trim</c> and <c>decodeURIComponent</c> (a malformed escape keeps the text as it is); the decoding loop, run
///         until no control character (<c>[\u0000-\u001F\u007F-\u009F\u2000-\u200D\uFEFF]</c>), numeric character reference
///         (<c>&amp;#(\w+)(^\w|;)?</c>, decoded as <c>String.fromCharCode</c> reads the number: decimal with an exponent,
///         <c>0x</c>, <c>0o</c> and <c>0b</c>, modulo 65536, anything else U+0000), <c>&amp;newline;</c> or
///         <c>&amp;tab;</c> (any case), or escaped white space (<c>(\\|%5[cC])((%(6[eE]|72|74))|[nrt])</c>) is left; a
///         text that starts with <c>.</c> or <c>/</c> kept as it is; the scheme found as <c>/^.+(:|&amp;colon;)/gim</c>
///         finds it (the first line with a colon, up to its last colon); the refused schemes
///         <c>/^([^\w]*)(javascript|data|vbscript)/im</c> with JavaScript's ASCII-only <c>\w</c> and <c>toLowerCase</c>;
///         backslashes turned into slashes; <c>mailto:</c> and schemes followed by <c>://</c> kept; and an <c>http:</c> or
///         <c>https:</c> link parsed and written back by the WHATWG URL parser (<c>new URL(...).toString()</c>), or
///         <c>about:blank</c> when it does not parse. The parser part covers what such a link can contain: user
///         information, domains (percent-decoded, lower-cased, refused with a forbidden code point), IPv4 hosts in every
///         number form, IPv6 hosts with their compressed serialization, ports with the default port dropped, dot segments,
///         and the percent-encode sets of the user information, path, query and fragment as Node's URL applies them.
///     </para>
///     <para>
///         <b>Known difference.</b> A host with characters outside ASCII, or with a label that starts with <c>xn--</c>, is
///         converted with .NET's <see cref="IdnMapping"/> in place of the browser's UTS #46 processing; .NET is stricter on
///         hyphens at the start, the end, or the third and fourth places of a label, on empty labels and on label lengths,
///         so such a link can become <c>about:blank</c> where Mermaid keeps it. Only <c>http</c> and <c>https</c> links are
///         affected; the refusal of the unsafe schemes is not.
///     </para>
///     <para>
///         <b>Security level.</b> Mermaid's <c>formatUrl</c> skips the check only at the <c>loose</c> security level. The
///         library models the <c>strict</c> level alone, so every link is checked whatever the configuration says.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use. Every pass of the decoding loop that runs again makes the
///         text shorter, so the loop ends; a link is at most the text size limit long, which bounds the work.
///     </para>
/// </remarks>
internal static class UrlSanitizer
{
    /// <summary>The link sanitize-url gives for a refused or empty URL (its <c>BLANK_URL</c>).</summary>
    public const string BlankUrl = "about:blank";

    /// <summary>The value JavaScript's <c>String.fromCharCode</c> takes a number modulo (2^16).</summary>
    private const int CharCodeModulus = 65536;

    /// <summary>The number of significant bits of a JavaScript number (a double).</summary>
    private const int DoubleMantissaBits = 53;

    /// <summary>The bit length from which a number no longer fits in a double (2^1024 and above is infinity).</summary>
    private const int DoubleOverflowBits = 1025;

    /// <summary>The marker of the end of the input in the URL parser (no code point is negative).</summary>
    private const int EndOfInput = -1;

    /// <summary>The upper-case hexadecimal digits of percent-encoding.</summary>
    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>The largest port number.</summary>
    private const int MaxPort = 65535;

    /// <summary>The character a numeric character reference, <c>&amp;newline;</c> or <c>&amp;tab;</c> starts with.</summary>
    private static readonly char[] AmpersandStart = ['&'];

    /// <summary>The characters an escaped white space starts with (a backslash, or the percent of <c>%5C</c>).</summary>
    private static readonly char[] EscapeStarts = ['\\', '%'];

    /// <summary>
    ///     Checks a link as Mermaid's <c>formatUrl</c> does at the <c>strict</c> security level: the link is trimmed, an
    ///     empty one is no link, and any other is checked by <see cref="Sanitize(string?)"/>.
    /// </summary>
    /// <remarks>
    ///     The class, flowchart and state databases of Mermaid call <c>formatUrl</c> for the links they keep, so a model
    ///     that keeps a link calls this and stores only its result.
    /// </remarks>
    /// <param name="link">The link as written; not null.</param>
    /// <returns>The checked link, or null when the trimmed link is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="link"/> is null.</exception>
    public static string? FormatUrl(string link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var url = Preprocessor.TrimJs(link);
        return url.Length == 0 ? null : Sanitize(url);
    }

    /// <summary>
    ///     Checks a URL exactly as sanitize-url's <c>sanitizeUrl</c> does: the URL with its hiding encodings undone, with
    ///     <c>about:blank</c> in place of a <c>javascript</c>, <c>data</c> or <c>vbscript</c> link, an empty link, or an
    ///     <c>http</c> or <c>https</c> link that does not parse.
    /// </summary>
    /// <remarks>
    ///     The result can differ from the input even for a safe link (control characters removed, references and escapes
    ///     decoded, backslashes as slashes, <c>http</c> and <c>https</c> links normalized), as in Mermaid; a caller keeps
    ///     the result, never the input.
    /// </remarks>
    /// <param name="url">The URL, or null (which gives <c>about:blank</c>).</param>
    /// <returns>The sanitized URL; never null.</returns>
    public static string Sanitize(string? url)
    {
        // No text at all gives the blank URL
        if (string.IsNullOrEmpty(url))
        {
            return BlankUrl;
        }

        // Undo the encodings until nothing that can hide a character is left. A pass that runs again always removes or
        // decodes something, so the text gets shorter; the length check only guards that argument
        var decoded = DecodeUriComponent(Preprocessor.TrimJs(url));
        bool more;
        do
        {
            var previous = decoded;
            decoded = DecodeHtmlCharacters(decoded);
            decoded = RemoveControlEntities(decoded);
            decoded = RemoveControlCharacters(decoded);
            decoded = RemoveWhitespaceEscapes(decoded);
            decoded = DecodeUriComponent(Preprocessor.TrimJs(decoded));
            more = NeedsDecoding(decoded);
            if (more && decoded.Length >= previous.Length)
            {
                break;
            }
        }
        while (more);

        if (decoded.Length == 0)
        {
            return BlankUrl;
        }

        // A relative URL without a scheme is kept as it is
        if (decoded[0] is '.' or '/')
        {
            return decoded;
        }

        // The scheme: the first line with a colon (or &colon;), up to its last one
        var trimmed = TrimStartJs(decoded);
        var match = MatchScheme(trimmed);
        if (match is null)
        {
            return decoded;
        }

        var scheme = Preprocessor.TrimJs(ToLowerJs(match));
        if (IsInvalidProtocol(scheme))
        {
            return BlankUrl;
        }

        // mailto and deep links are kept, with backslashes as slashes
        var slashed = trimmed.Replace('\\', '/');
        if (scheme == "mailto:" || scheme.Contains("://", StringComparison.Ordinal))
        {
            return slashed;
        }

        // http and https links must parse, and are written back as the URL parser writes them
        if (scheme is "http:" or "https:")
        {
            return ParseHttpUrl(slashed) ?? BlankUrl;
        }

        return slashed;
    }

    /// <summary>
    ///     Gets a value indicating whether a character is one of sanitize-url's control characters
    ///     (<c>[\u0000-\u001F\u007F-\u009F\u2000-\u200D\uFEFF]</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsControlCharacter(char c) =>
        c <= '\u001f' || (c >= '\u007f' && c <= '\u009f') || (c >= '\u2000' && c <= '\u200d') || c == '\ufeff';

    /// <summary>
    ///     Removes sanitize-url's control characters (its <c>ctrlCharactersRegex</c> replaced by nothing).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without them.</returns>
    private static string RemoveControlCharacters(string text)
    {
        if (!text.Any(IsControlCharacter))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Where(c => !IsControlCharacter(c)))
        {
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     sanitize-url's <c>decodeHtmlCharacters</c>: the control characters are removed, then every numeric character
    ///     reference (<c>/&amp;#(\w+)(^\w|;)?/g</c>, where <c>^\w</c> never matches inside the text, so the semicolon is
    ///     optional) becomes the character <c>String.fromCharCode</c> gives for its word.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The decoded text.</returns>
    private static string DecodeHtmlCharacters(string text)
    {
        var stripped = RemoveControlCharacters(text);
        if (!stripped.Contains("&#", StringComparison.Ordinal))
        {
            return stripped;
        }

        var builder = new StringBuilder(stripped.Length);
        var i = 0;
        while (i < stripped.Length)
        {
            var end = ReferenceWordEnd(stripped, i);
            if (end < 0)
            {
                builder.Append(stripped[i]);
                i++;
                continue;
            }

            // The word is greedy (\w+), and one semicolon after it belongs to the reference
            builder.Append(FromCharCode(stripped[(i + 2)..end]));
            i = end < stripped.Length && stripped[end] == ';' ? end + 1 : end;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Finds the end of the word of a numeric character reference (<c>&amp;#\w+</c>) that starts at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the ampersand.</param>
    /// <returns>The position after the last word character, or -1 when no reference starts there.</returns>
    private static int ReferenceWordEnd(string text, int start)
    {
        if (start + 2 >= text.Length || text[start] != '&' || text[start + 1] != '#' || !ScanPatterns.IsJsWordChar(text[start + 2]))
        {
            return -1;
        }

        var end = start + 3;
        while (end < text.Length && ScanPatterns.IsJsWordChar(text[end]))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    ///     JavaScript's <c>String.fromCharCode(word)</c>: the word is converted as <c>Number(word)</c> converts it and taken
    ///     modulo 2^16 (<c>ToUint16</c>), so a word that is not a number gives U+0000.
    /// </summary>
    /// <remarks>
    ///     A word of <c>\w</c> characters can only be a decimal number with an optional exponent, a <c>0x</c>, <c>0o</c> or
    ///     <c>0b</c> integer, <c>Infinity</c> (which gives 0) or not a number (which gives 0): signs, points, white space and
    ///     separators are not word characters, and <c>Number</c> refuses the underscore.
    /// </remarks>
    /// <param name="word">The word of the reference.</param>
    /// <returns>The character.</returns>
    private static char FromCharCode(string word)
    {
        // 0x, 0o and 0b integers: the exact value, rounded to a double as JavaScript reads it
        if (word.Length > 2 && word[0] == '0' && (word[1] | 0x20) is 'x' or 'o' or 'b')
        {
            var radix = (word[1] | 0x20) switch
            {
                'x' => 16,
                'o' => 8,
                _ => 2,
            };
            var value = BigInteger.Zero;
            foreach (var c in word.AsSpan(2))
            {
                var digit = DigitValue(c);
                if (digit < 0 || digit >= radix)
                {
                    return '\0';
                }

                value = (value * radix) + digit;
            }

            return ToUint16(RoundToDouble(value));
        }

        // Decimal digits with an optional exponent (no sign: it is not a word character)
        if (!IsDecimalWord(word))
        {
            return '\0';
        }

        var number = double.Parse(word, NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
        return double.IsFinite(number) ? ToUint16(new BigInteger(Math.Truncate(number))) : '\0';
    }

    /// <summary>
    ///     Gets a value indicating whether a word is a decimal number as <c>Number</c> reads one from word characters:
    ///     digits, then optionally <c>e</c> or <c>E</c> and digits.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsDecimalWord(string word)
    {
        var i = 0;
        while (i < word.Length && char.IsAsciiDigit(word[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return false;
        }

        if (i == word.Length)
        {
            return true;
        }

        if ((word[i] | 0x20) != 'e' || i + 1 == word.Length)
        {
            return false;
        }

        return word.AsSpan(i + 1).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>
    ///     Gets the value of an ASCII digit or letter as a digit of a base up to 36.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The value, or -1 for any other character.</returns>
    private static int DigitValue(int c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'z' => c - 'a' + 10,
        >= 'A' and <= 'Z' => c - 'A' + 10,
        _ => -1,
    };

    /// <summary>
    ///     Gets a value indicating whether a code point is an ASCII letter or digit (a code point outside the BMP is not
    ///     truncated to a character first).
    /// </summary>
    /// <param name="c">The code point, or <see cref="EndOfInput"/>.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsAsciiAlphanumeric(int c) => DigitValue(c) >= 0;

    /// <summary>
    ///     Rounds a non-negative integer to the nearest double (ties to even), as JavaScript reads a long integer literal.
    /// </summary>
    /// <param name="value">The integer.</param>
    /// <returns>The value of the double, or null when it is infinity.</returns>
    private static BigInteger? RoundToDouble(BigInteger value)
    {
        var bits = (int)value.GetBitLength();
        if (bits <= DoubleMantissaBits)
        {
            return value;
        }

        // Keep the 53 leading bits and round the rest to the nearest, ties to even
        var shift = bits - DoubleMantissaBits;
        var kept = value >> shift;
        var rest = value - (kept << shift);
        var half = BigInteger.One << (shift - 1);
        if (rest > half || (rest == half && !kept.IsEven))
        {
            kept++;
        }

        var rounded = kept << shift;
        return rounded.GetBitLength() >= DoubleOverflowBits ? null : rounded;
    }

    /// <summary>
    ///     JavaScript's <c>ToUint16</c> of an integer: the integer modulo 2^16, with infinity as 0.
    /// </summary>
    /// <param name="value">The non-negative integer, or null for infinity.</param>
    /// <returns>The UTF-16 code unit.</returns>
    private static char ToUint16(BigInteger? value) => value is { } integer ? (char)(int)(integer % CharCodeModulus) : '\0';

    /// <summary>
    ///     Removes <c>&amp;newline;</c> and <c>&amp;tab;</c> in any ASCII case (sanitize-url's <c>htmlCtrlEntityRegex</c>,
    ///     <c>/&amp;(newline|tab);/gi</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without them.</returns>
    private static string RemoveControlEntities(string text) => RemoveMatches(text, AmpersandStart, ControlEntityLength);

    /// <summary>
    ///     Removes every match of a pattern, scanning left to right as a global JavaScript <c>replace</c> with an empty
    ///     replacement does (a match is skipped whole, and the scan goes on after it).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="starts">The characters a match can start with.</param>
    /// <param name="lengthAt">The length of the match at a position, or 0 when there is none.</param>
    /// <returns>The text without the matches; the text itself when there is none.</returns>
    private static string RemoveMatches(string text, char[] starts, Func<string, int, int> lengthAt)
    {
        var first = FirstMatch(text, starts, lengthAt);
        if (first < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        builder.Append(text, 0, first);
        var i = first;
        while (i < text.Length)
        {
            var length = lengthAt(text, i);
            if (length > 0)
            {
                i += length;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Finds the first match of a pattern, trying only the positions of the characters it can start with.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="starts">The characters a match can start with.</param>
    /// <param name="lengthAt">The length of the match at a position, or 0 when there is none.</param>
    /// <returns>The position of the first match, or -1 when there is none.</returns>
    private static int FirstMatch(string text, char[] starts, Func<string, int, int> lengthAt)
    {
        for (var i = text.IndexOfAny(starts); i >= 0; i = text.IndexOfAny(starts, i + 1))
        {
            if (lengthAt(text, i) > 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Gets the length of an <c>&amp;newline;</c> or <c>&amp;tab;</c> (any ASCII case) at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position.</param>
    /// <returns>The length, or 0 when there is none.</returns>
    private static int ControlEntityLength(string text, int start)
    {
        if (StartsWithAsciiIgnoreCase(text, start, "&newline;"))
        {
            return "&newline;".Length;
        }

        return StartsWithAsciiIgnoreCase(text, start, "&tab;") ? "&tab;".Length : 0;
    }

    /// <summary>
    ///     Gets a value indicating whether a text has a word at a position, ASCII letters in any case (the <c>i</c> flag of a
    ///     JavaScript regular expression without the <c>u</c> flag, which never matches a letter outside ASCII to an ASCII
    ///     one).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position.</param>
    /// <param name="word">The word, lower case.</param>
    /// <returns>Whether it has.</returns>
    private static bool StartsWithAsciiIgnoreCase(string text, int start, string word)
    {
        if (start + word.Length > text.Length)
        {
            return false;
        }

        for (var j = 0; j < word.Length; j++)
        {
            if (AsciiLower(text[start + j]) != word[j])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Lower-cases an ASCII letter and keeps any other character.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The character, lower case when it is an ASCII letter.</returns>
    private static char AsciiLower(char c) => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;

    /// <summary>
    ///     Removes escaped white space (sanitize-url's <c>whitespaceEscapeCharsRegex</c>,
    ///     <c>/(\\|%5[cC])((%(6[eE]|72|74))|[nrt])/g</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without it.</returns>
    private static string RemoveWhitespaceEscapes(string text) => RemoveMatches(text, EscapeStarts, WhitespaceEscapeLength);


    /// <summary>
    ///     Gets the length of an escaped white space at a position: a backslash or <c>%5C</c> (<c>%5c</c>), then
    ///     <c>%6E</c> (<c>%6e</c>), <c>%72</c>, <c>%74</c>, <c>n</c>, <c>r</c> or <c>t</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position.</param>
    /// <returns>The length, or 0 when there is none.</returns>
    private static int WhitespaceEscapeLength(string text, int start)
    {
        // The escape: a backslash, or %5C in either case of the C
        int after;
        if (text[start] == '\\')
        {
            after = start + 1;
        }
        else if (start + 2 < text.Length && text[start] == '%' && text[start + 1] == '5' && text[start + 2] is 'c' or 'C')
        {
            after = start + 3;
        }
        else
        {
            return 0;
        }

        // The letter: a percent escape of n, r or t first, then the letter itself
        if (after + 2 < text.Length && text[after] == '%' &&
            ((text[after + 1] == '6' && text[after + 2] is 'e' or 'E') || (text[after + 1] == '7' && text[after + 2] is '2' or '4')))
        {
            return after + 3 - start;
        }

        return after < text.Length && text[after] is 'n' or 'r' or 't' ? after + 1 - start : 0;
    }

    /// <summary>
    ///     Gets a value indicating whether sanitize-url decodes a text again: it has a control character, a numeric
    ///     character reference, an <c>&amp;newline;</c> or <c>&amp;tab;</c>, or an escaped white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool NeedsDecoding(string text)
    {
        if (text.Any(IsControlCharacter))
        {
            return true;
        }

        return FirstMatch(text, AmpersandStart, (s, i) => ReferenceWordEnd(s, i) >= 0 ? 1 : ControlEntityLength(s, i)) >= 0 ||
            FirstMatch(text, EscapeStarts, WhitespaceEscapeLength) >= 0;
    }

    /// <summary>
    ///     JavaScript's <c>decodeURIComponent</c> as sanitize-url calls it: every percent escape decoded (a run of escapes
    ///     as one UTF-8 sequence), and the text kept as it is when the call would throw (a <c>%</c> without two
    ///     hexadecimal digits, a byte that cannot start a sequence, a sequence cut short, or bytes that are not the valid
    ///     UTF-8 of one code point).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The decoded text, or the text when it is malformed.</returns>
    private static string DecodeUriComponent(string text)
    {
        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        Span<byte> octets = stackalloc byte[4];
        var k = 0;
        while (k < text.Length)
        {
            if (text[k] != '%')
            {
                builder.Append(text[k]);
                k++;
                continue;
            }

            // The first byte, and the number of bytes its leading one bits announce
            if (k + 2 >= text.Length || !TryParseHexOctet(text, k + 1, out var first))
            {
                return text;
            }

            k += 2;
            var count = BitOperations.LeadingZeroCount((uint)(byte)~first) - 24;
            if (count == 0)
            {
                builder.Append((char)first);
                k++;
                continue;
            }

            if (count == 1 || count > 4)
            {
                return text;
            }

            // The continuation bytes, each its own escape
            octets[0] = first;
            for (var j = 1; j < count; j++)
            {
                k++;
                if (k + 2 >= text.Length || text[k] != '%' || !TryParseHexOctet(text, k + 1, out var next))
                {
                    return text;
                }

                octets[j] = next;
                k += 2;
            }

            if (Rune.DecodeFromUtf8(octets[..count], out var rune, out var consumed) != System.Buffers.OperationStatus.Done || consumed != count)
            {
                return text;
            }

            builder.Append(rune.ToString());
            k++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Reads two hexadecimal digits as a byte.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the first digit; the second must exist.</param>
    /// <param name="value">The byte.</param>
    /// <returns>Whether both are hexadecimal digits.</returns>
    private static bool TryParseHexOctet(string text, int start, out byte value)
    {
        value = 0;
        if (!char.IsAsciiHexDigit(text[start]) || !char.IsAsciiHexDigit(text[start + 1]))
        {
            return false;
        }

        value = (byte)((DigitValue(text[start]) << 4) | DigitValue(text[start + 1]));
        return true;
    }

    /// <summary>
    ///     Removes JavaScript white space from the start of a text (JavaScript's <c>trimStart</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without leading white space.</returns>
    private static string TrimStartJs(string text)
    {
        var start = 0;
        while (start < text.Length && ScanPatterns.IsJsWhitespace(text[start]))
        {
            start++;
        }

        return text[start..];
    }

    /// <summary>
    ///     Finds the scheme as sanitize-url's <c>urlSchemeRegex</c> (<c>/^.+(:|&amp;colon;)/gim</c>) does with
    ///     <c>match</c>: the first line (lines end at a JavaScript line terminator) that has a colon or an
    ///     <c>&amp;colon;</c> (any case) after its first character, from its start to the end of the last one.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The match, or null when there is none.</returns>
    private static string? MatchScheme(string text)
    {
        var lineStart = 0;
        while (lineStart <= text.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < text.Length && !ScanPatterns.IsJsLineTerminator(text[lineEnd]))
            {
                lineEnd++;
            }

            // The greedy .+ gives characters back from the end of the line until a colon or &colon; follows it
            for (var k = lineEnd - 1; k > lineStart; k--)
            {
                if (text[k] == ':')
                {
                    return text[lineStart..(k + 1)];
                }

                if (k + "&colon;".Length <= lineEnd && StartsWithAsciiIgnoreCase(text, k, "&colon;"))
                {
                    return text[lineStart..(k + "&colon;".Length)];
                }
            }

            lineStart = lineEnd + 1;
        }

        return null;
    }

    /// <summary>
    ///     JavaScript's <c>toLowerCase</c> as far as the scheme checks can see it: ASCII letters are lower-cased, U+0130 becomes
    ///     <c>i</c> followed by U+0307, and the Kelvin sign becomes <c>k</c> (the only characters outside ASCII whose lower
    ///     case has ASCII letters); every other character is kept, since its lower case is never ASCII and the checks only
    ///     compare ASCII text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lower-case text.</returns>
    private static string ToLowerJs(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\u0130':
                    builder.Append("i\u0307");
                    break;
                case '\u212a':
                    builder.Append('k');
                    break;
                default:
                    builder.Append(AsciiLower(c));
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets a value indicating whether a scheme is refused (sanitize-url's <c>invalidProtocolRegex</c>,
    ///     <c>/^([^\w]*)(javascript|data|vbscript)/im</c>): after its leading characters that are not ASCII word
    ///     characters, it starts with <c>javascript</c>, <c>data</c> or <c>vbscript</c>.
    /// </summary>
    /// <param name="scheme">The lower-case scheme, on one line.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsInvalidProtocol(string scheme)
    {
        var start = 0;
        while (start < scheme.Length && !ScanPatterns.IsJsWordChar(scheme[start]))
        {
            start++;
        }

        var rest = scheme.AsSpan(start);
        return rest.StartsWith("javascript", StringComparison.Ordinal) || rest.StartsWith("data", StringComparison.Ordinal) ||
            rest.StartsWith("vbscript", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Parses an <c>http</c> or <c>https</c> link with the WHATWG basic URL parser (no base URL) and writes it back as
    ///     <c>URL.toString()</c> does, as sanitize-url's <c>new URL(...)</c> with its lower-cased protocol and host name.
    /// </summary>
    /// <remarks>
    ///     sanitize-url calls this only for a text whose scheme check found <c>http:</c> or <c>https:</c>, so another
    ///     scheme, or none, can only fail to parse; it is reported as a failure. The setters of the protocol and the host
    ///     name change nothing, since the parser already lower-cases both.
    /// </remarks>
    /// <param name="input">The link, backslashes already slashes.</param>
    /// <returns>The serialized URL, or null when the parser fails.</returns>
    private static string? ParseHttpUrl(string input)
    {
        var points = PrepareInput(input);
        var p = 0;
        int At(int index) => index < points.Count ? points[index] : EndOfInput;

        // The scheme: http or https, then a colon
        var schemeStart = p;
        while (IsAsciiAlphanumeric(At(p)) || At(p) is '+' or '-' or '.')
        {
            p++;
        }

        if (p == schemeStart || DigitValue(points[schemeStart]) < 10 || At(p) != ':')
        {
            return null;
        }

        var scheme = string.Concat(points.Skip(schemeStart).Take(p - schemeStart).Select(c => AsciiLower((char)c)));
        int? defaultPort = scheme switch
        {
            "http" => 80,
            "https" => 443,
            _ => null,
        };
        if (defaultPort is null)
        {
            return null;
        }

        p++;

        // Special authority slashes, then any further slashes
        if (At(p) == '/' && At(p + 1) == '/')
        {
            p += 2;
        }

        while (At(p) is '/' or '\\')
        {
            p++;
        }

        // The authority: user information before the last at sign
        var authorityEnd = p;
        while (!IsAuthorityEnd(At(authorityEnd)))
        {
            authorityEnd++;
        }

        var username = new StringBuilder();
        var password = new StringBuilder();
        var hostStart = ReadUserInfo(points, p, authorityEnd, username, password);
        if (hostStart < 0)
        {
            return null;
        }

        // The host, up to a colon outside brackets or the end of the authority
        p = hostStart;
        var insideBrackets = false;
        while (!IsAuthorityEnd(At(p)) && (At(p) != ':' || insideBrackets))
        {
            insideBrackets = At(p) == '[' || (insideBrackets && At(p) != ']');
            p++;
        }

        if (p == hostStart)
        {
            return null;
        }

        var host = ParseHost(CodePointsToString(points, hostStart, p));
        if (host is null)
        {
            return null;
        }

        // The port: digits up to the end of the authority, the default port dropped
        int? port = null;
        if (At(p) == ':')
        {
            p++;
            var portStart = p;
            var value = 0;
            while (At(p) is >= '0' and <= '9')
            {
                value = Math.Min((value * 10) + (At(p) - '0'), MaxPort + 1);
                p++;
            }

            if (!IsAuthorityEnd(At(p)) || value > MaxPort)
            {
                return null;
            }

            port = p > portStart && value != defaultPort ? value : null;
        }

        // The path, the query and the fragment
        var builder = new StringBuilder(scheme).Append("://");
        if (username.Length > 0 || password.Length > 0)
        {
            builder.Append(username);
            if (password.Length > 0)
            {
                builder.Append(':').Append(password);
            }

            builder.Append('@');
        }

        builder.Append(host);
        if (port is { } portNumber)
        {
            builder.Append(':').Append(portNumber.ToString(CultureInfo.InvariantCulture));
        }

        AppendPathQueryAndFragment(builder, points, p);
        return builder.ToString();
    }

    /// <summary>
    ///     Prepares the input of the URL parser: the text as Unicode scalar values (a lone surrogate becomes U+FFFD, as a
    ///     JavaScript string becomes a <c>USVString</c>), without leading and trailing C0 controls and spaces, and without
    ///     ASCII tabs and newlines anywhere.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <returns>The code points.</returns>
    private static List<int> PrepareInput(string input)
    {
        var all = new List<int>(input.Length);
        var index = 0;
        while (index < input.Length)
        {
            if (Rune.TryGetRuneAt(input, index, out var rune))
            {
                all.Add(rune.Value);
                index += rune.Utf16SequenceLength;
            }
            else
            {
                all.Add(Rune.ReplacementChar.Value);
                index++;
            }
        }

        var start = 0;
        var end = all.Count;
        while (start < end && all[start] <= ' ')
        {
            start++;
        }

        while (end > start && all[end - 1] <= ' ')
        {
            end--;
        }

        return [.. all.Skip(start).Take(end - start).Where(c => c is not ('\t' or '\n' or '\r'))];
    }

    /// <summary>
    ///     Gets a value indicating whether a code point ends the authority of a special URL (the end of the input, a slash,
    ///     a backslash, a question mark or a number sign).
    /// </summary>
    /// <param name="c">The code point, or <see cref="EndOfInput"/>.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsAuthorityEnd(int c) => c is EndOfInput or '/' or '\\' or '?' or '#';

    /// <summary>
    ///     Reads the user information of an authority (the authority state): each at sign ends a part of it, an earlier at
    ///     sign is kept as <c>%40</c>, the first colon separates the user name from the password, and both are
    ///     percent-encoded with the user information set.
    /// </summary>
    /// <param name="points">The code points.</param>
    /// <param name="start">The start of the authority.</param>
    /// <param name="end">The end of the authority.</param>
    /// <param name="username">The user name, appended to.</param>
    /// <param name="password">The password, appended to.</param>
    /// <returns>The start of the host, or -1 when an at sign is followed by no host.</returns>
    private static int ReadUserInfo(List<int> points, int start, int end, StringBuilder username, StringBuilder password)
    {
        var atSignSeen = false;
        var passwordTokenSeen = false;
        var bufferStart = start;
        for (var i = start; i < end; i++)
        {
            if (points[i] != '@')
            {
                continue;
            }

            // The text since the previous at sign joins the user information, after a %40 for that at sign
            if (atSignSeen)
            {
                (passwordTokenSeen ? password : username).Append("%40");
            }

            atSignSeen = true;
            for (var j = bufferStart; j < i; j++)
            {
                if (points[j] == ':' && !passwordTokenSeen)
                {
                    passwordTokenSeen = true;
                    continue;
                }

                AppendEncoded(passwordTokenSeen ? password : username, points[j], IsUserInfoSet);
            }

            bufferStart = i + 1;
        }

        return atSignSeen && bufferStart == end ? -1 : bufferStart;
    }

    /// <summary>
    ///     Reads the path, the query and the fragment of a special URL and appends them as the serializer writes them:
    ///     the path segments (dot segments resolved, at least one segment), then the query and the fragment when present.
    /// </summary>
    /// <param name="builder">The serialized URL, appended to.</param>
    /// <param name="points">The code points.</param>
    /// <param name="start">The position after the host and port.</param>
    private static void AppendPathQueryAndFragment(StringBuilder builder, List<int> points, int start)
    {
        var p = start;
        int At(int index) => index < points.Count ? points[index] : EndOfInput;

        // The path start state: a slash or backslash belongs to it
        if (At(p) is '/' or '\\')
        {
            p++;
        }

        // The path state: segments up to a slash, a question mark, a number sign or the end
        var path = new List<string>();
        var segment = new StringBuilder();
        while (true)
        {
            var c = At(p);
            if (c is not (EndOfInput or '/' or '\\' or '?' or '#'))
            {
                AppendEncoded(segment, c, IsPathSet);
                p++;
                continue;
            }

            var text = segment.ToString();
            segment.Clear();
            var slash = c is '/' or '\\';
            if (IsDoubleDotSegment(text))
            {
                if (path.Count > 0)
                {
                    path.RemoveAt(path.Count - 1);
                }

                if (!slash)
                {
                    path.Add(string.Empty);
                }
            }
            else if (IsSingleDotSegment(text))
            {
                if (!slash)
                {
                    path.Add(string.Empty);
                }
            }
            else
            {
                path.Add(text);
            }

            p++;
            if (!slash)
            {
                break;
            }
        }

        foreach (var item in path)
        {
            builder.Append('/').Append(item);
        }

        // The query state, up to a number sign, then the fragment state
        if (At(p - 1) == '?')
        {
            builder.Append('?');
            while (At(p) is not (EndOfInput or '#'))
            {
                AppendEncoded(builder, At(p), IsSpecialQuerySet);
                p++;
            }

            p++;
        }

        if (At(p - 1) == '#')
        {
            builder.Append('#');
            while (At(p) != EndOfInput)
            {
                AppendEncoded(builder, At(p), IsFragmentSet);
                p++;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether a path segment is a single-dot segment (<c>.</c> or <c>%2e</c> in any case).
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsSingleDotSegment(string segment) =>
        segment == "." || segment.Equals("%2e", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets a value indicating whether a path segment is a double-dot segment (<c>..</c>, with either dot as
    ///     <c>%2e</c> in any case).
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsDoubleDotSegment(string segment) =>
        segment is ".." || segment.Equals(".%2e", StringComparison.OrdinalIgnoreCase) ||
        segment.Equals("%2e.", StringComparison.OrdinalIgnoreCase) || segment.Equals("%2e%2e", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets a value indicating whether a code point is in the C0 control percent-encode set (C0 controls and every code
    ///     point above U+007E).
    /// </summary>
    /// <param name="c">The code point.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsC0ControlSet(int c) => c is < 0x20 or > 0x7e;

    /// <summary>
    ///     Gets a value indicating whether a code point is in the fragment percent-encode set.
    /// </summary>
    /// <param name="c">The code point.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsFragmentSet(int c) => IsC0ControlSet(c) || c is ' ' or '"' or '<' or '>' or '`';

    /// <summary>
    ///     Gets a value indicating whether a code point is in the special-query percent-encode set (the query set and the
    ///     apostrophe).
    /// </summary>
    /// <param name="c">The code point.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsSpecialQuerySet(int c) => IsC0ControlSet(c) || c is ' ' or '"' or '#' or '<' or '>' or '\'';

    /// <summary>
    ///     Gets a value indicating whether a code point is in the path percent-encode set as Node's URL applies it (the
    ///     query set and <c>?</c>, <c>`</c>, <c>{</c> and <c>}</c>).
    /// </summary>
    /// <param name="c">The code point.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsPathSet(int c) => IsC0ControlSet(c) || c is ' ' or '"' or '#' or '<' or '>' or '?' or '`' or '{' or '}';

    /// <summary>
    ///     Gets a value indicating whether a code point is in the user information percent-encode set (the path set and
    ///     <c>/</c>, <c>:</c>, <c>;</c>, <c>=</c>, <c>@</c>, <c>[</c> to <c>^</c>, and <c>|</c>).
    /// </summary>
    /// <param name="c">The code point.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsUserInfoSet(int c) =>
        IsPathSet(c) || c is '/' or ':' or ';' or '=' or '@' or '[' or '\\' or ']' or '^' or '|';

    /// <summary>
    ///     Appends a code point, UTF-8 percent-encoded with upper-case digits when it is in a percent-encode set.
    /// </summary>
    /// <param name="builder">The text, appended to.</param>
    /// <param name="c">The code point.</param>
    /// <param name="inSet">The percent-encode set.</param>
    private static void AppendEncoded(StringBuilder builder, int c, Func<int, bool> inSet)
    {
        var rune = new Rune(c);
        if (!inSet(c))
        {
            builder.Append(rune.ToString());
            return;
        }

        Span<byte> bytes = stackalloc byte[4];
        var length = rune.EncodeToUtf8(bytes);
        foreach (var b in bytes[..length])
        {
            builder.Append('%').Append(HexDigits[b >> 4]).Append(HexDigits[b & 0xf]);
        }
    }

    /// <summary>
    ///     Writes a range of code points as a string.
    /// </summary>
    /// <param name="points">The code points.</param>
    /// <param name="start">The start of the range.</param>
    /// <param name="end">The end of the range.</param>
    /// <returns>The text.</returns>
    private static string CodePointsToString(List<int> points, int start, int end)
    {
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            builder.Append(new Rune(points[i]).ToString());
        }

        return builder.ToString();
    }

    /// <summary>
    ///     The host parser of a special URL: an IPv6 address in brackets, or a domain (percent-decoded, converted to ASCII,
    ///     refused with a forbidden code point) that is an IPv4 address when its last label is a number.
    /// </summary>
    /// <param name="input">The host text; not empty.</param>
    /// <returns>The serialized host, or null when the parser fails.</returns>
    private static string? ParseHost(string input)
    {
        if (input[0] == '[')
        {
            if (input[^1] != ']')
            {
                return null;
            }

            return ParseIPv6(input[1..^1]) is { } address ? "[" + SerializeIPv6(address) + "]" : null;
        }

        var domain = new UTF8Encoding(false, false).GetString(PercentDecode(Encoding.UTF8.GetBytes(input)));
        var ascii = DomainToAscii(domain);
        if (ascii is null)
        {
            return null;
        }

        if (!EndsInNumber(ascii))
        {
            return ascii;
        }

        return ParseIPv4(ascii) is { } ipv4
            ? string.Create(CultureInfo.InvariantCulture, $"{ipv4 >> 24}.{(ipv4 >> 16) & 0xff}.{(ipv4 >> 8) & 0xff}.{ipv4 & 0xff}")
            : null;
    }

    /// <summary>
    ///     Percent-decodes bytes: <c>%</c> and two hexadecimal digits become their byte, anything else is kept.
    /// </summary>
    /// <param name="input">The bytes.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] PercentDecode(byte[] input)
    {
        var output = new List<byte>(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == '%' && i + 2 < input.Length && char.IsAsciiHexDigit((char)input[i + 1]) && char.IsAsciiHexDigit((char)input[i + 2]))
            {
                output.Add((byte)((DigitValue(input[i + 1]) << 4) | DigitValue(input[i + 2])));
                i += 3;
            }
            else
            {
                output.Add(input[i]);
                i++;
            }
        }

        return [.. output];
    }

    /// <summary>
    ///     The WHATWG "domain to ASCII" without strictness: an ASCII domain without <c>xn--</c> labels is lower-cased (the
    ///     shortcut the standard allows), any other goes through <see cref="IdnMapping"/>; an empty result or one with a
    ///     forbidden domain code point fails.
    /// </summary>
    /// <param name="domain">The decoded domain.</param>
    /// <returns>The ASCII domain, or null when it fails.</returns>
    private static string? DomainToAscii(string domain)
    {
        string result;
        if (domain.All(char.IsAscii) && !domain.Split('.').Any(label => label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase)))
        {
            result = string.Concat(domain.Select(AsciiLower));
        }
        else
        {
            try
            {
                // UTS #46 maps ASCII capitals to small letters; .NET keeps the case of an ASCII label, so it is lowered first
                result = new IdnMapping().GetAscii(string.Concat(domain.Select(AsciiLower)));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return result.Length == 0 || result.Any(IsForbiddenDomainCodePoint) ? null : result;
    }

    /// <summary>
    ///     Gets a value indicating whether a character is a forbidden domain code point (a C0 control, space, <c>#</c>,
    ///     <c>%</c>, <c>/</c>, <c>:</c>, <c>&lt;</c>, <c>&gt;</c>, <c>?</c>, <c>@</c>, <c>[</c>, <c>\</c>, <c>]</c>,
    ///     <c>^</c>, <c>|</c> or DEL).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsForbiddenDomainCodePoint(char c) =>
        c <= '\u001f' || c is ' ' or '#' or '%' or '/' or ':' or '<' or '>' or '?' or '@' or '[' or '\\' or ']' or '^' or '|' or '\u007f';

    /// <summary>
    ///     The WHATWG "ends in a number" check: the last label (before a final dot) is all digits or an IPv4 number.
    /// </summary>
    /// <param name="domain">The ASCII domain.</param>
    /// <returns>Whether the domain is to be read as an IPv4 address.</returns>
    private static bool EndsInNumber(string domain)
    {
        var parts = domain.Split('.');
        var last = parts.Length;
        if (parts[last - 1].Length == 0)
        {
            if (last == 1)
            {
                return false;
            }

            last--;
        }

        var label = parts[last - 1];
        return (label.Length > 0 && label.All(char.IsAsciiDigit)) || ParseIPv4Number(label) is not null;
    }

    /// <summary>
    ///     The WHATWG IPv4 number parser: decimal, <c>0x</c> hexadecimal, or octal with a leading zero.
    /// </summary>
    /// <param name="input">The part.</param>
    /// <returns>The number, or null when the part is not one.</returns>
    private static BigInteger? ParseIPv4Number(string input)
    {
        if (input.Length == 0)
        {
            return null;
        }

        var radix = 10;
        var digits = input.AsSpan();
        if (digits.Length >= 2 && digits[0] == '0' && digits[1] is 'x' or 'X')
        {
            digits = digits[2..];
            radix = 16;
        }
        else if (digits.Length >= 2 && digits[0] == '0')
        {
            digits = digits[1..];
            radix = 8;
        }

        var value = BigInteger.Zero;
        foreach (var c in digits)
        {
            var digit = DigitValue(c);
            if (digit < 0 || digit >= radix)
            {
                return null;
            }

            value = (value * radix) + digit;
        }

        return value;
    }

    /// <summary>
    ///     The WHATWG IPv4 parser: one to four numbers, every one but the last at most 255, and the last filling the rest
    ///     of the 32 bits.
    /// </summary>
    /// <param name="input">The ASCII domain.</param>
    /// <returns>The address, or null when it is not one.</returns>
    private static uint? ParseIPv4(string input)
    {
        var parts = input.Split('.').ToList();
        if (parts[^1].Length == 0 && parts.Count > 1)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count > 4)
        {
            return null;
        }

        var numbers = new List<BigInteger>(parts.Count);
        foreach (var part in parts)
        {
            if (ParseIPv4Number(part) is not { } number)
            {
                return null;
            }

            numbers.Add(number);
        }

        if (numbers.Take(numbers.Count - 1).Any(n => n > 255) || numbers[^1] >= BigInteger.Pow(256, 5 - numbers.Count))
        {
            return null;
        }

        var address = numbers[^1];
        for (var i = 0; i < numbers.Count - 1; i++)
        {
            address += numbers[i] * BigInteger.Pow(256, 3 - i);
        }

        return (uint)address;
    }

    /// <summary>
    ///     The WHATWG IPv6 parser: up to eight hexadecimal pieces, one <c>::</c> compression, and an IPv4 address in place
    ///     of the last two pieces.
    /// </summary>
    /// <param name="input">The text between the brackets.</param>
    /// <returns>The eight pieces, or null when the text is not an address.</returns>
    private static ushort[]? ParseIPv6(string input)
    {
        var address = new ushort[8];
        var pieceIndex = 0;
        int? compress = null;
        var p = 0;
        int At(int index) => index < input.Length ? input[index] : EndOfInput;

        // A leading :: compresses the first pieces
        if (At(p) == ':')
        {
            if (At(p + 1) != ':')
            {
                return null;
            }

            p += 2;
            pieceIndex++;
            compress = pieceIndex;
        }

        while (At(p) != EndOfInput)
        {
            if (pieceIndex == 8)
            {
                return null;
            }

            // A second colon in a row is the compression
            if (At(p) == ':')
            {
                if (compress is not null)
                {
                    return null;
                }

                p++;
                pieceIndex++;
                compress = pieceIndex;
                continue;
            }

            // A piece of up to four hexadecimal digits
            var value = 0;
            var length = 0;
            while (length < 4 && char.IsAsciiHexDigit((char)Math.Max(At(p), 0)))
            {
                value = (value * 0x10) + DigitValue(At(p));
                p++;
                length++;
            }

            // An IPv4 address in the last two pieces
            if (At(p) == '.')
            {
                if (length == 0)
                {
                    return null;
                }

                p -= length;
                return ParseIPv6Ipv4(input, p, address, pieceIndex, compress);
            }

            if (At(p) == ':')
            {
                p++;
                if (At(p) == EndOfInput)
                {
                    return null;
                }
            }
            else if (At(p) != EndOfInput)
            {
                return null;
            }

            address[pieceIndex] = (ushort)value;
            pieceIndex++;
        }

        return FinishIPv6(address, pieceIndex, compress);
    }

    /// <summary>
    ///     Reads the IPv4 address at the end of an IPv6 address into two pieces, then finishes the address.
    /// </summary>
    /// <param name="input">The text between the brackets.</param>
    /// <param name="start">The start of the IPv4 address.</param>
    /// <param name="address">The pieces so far.</param>
    /// <param name="pieceIndex">The index of the next piece.</param>
    /// <param name="compress">The index of the compression, or null.</param>
    /// <returns>The eight pieces, or null when the text is not an address.</returns>
    private static ushort[]? ParseIPv6Ipv4(string input, int start, ushort[] address, int pieceIndex, int? compress)
    {
        if (pieceIndex > 6)
        {
            return null;
        }

        var p = start;
        var numbersSeen = 0;
        while (p < input.Length)
        {
            // The dot before every number but the first
            if (numbersSeen > 0)
            {
                if (input[p] != '.' || numbersSeen >= 4)
                {
                    return null;
                }

                p++;
            }

            if (p >= input.Length || !char.IsAsciiDigit(input[p]))
            {
                return null;
            }

            // A decimal number up to 255, without a leading zero
            int? piece = null;
            while (p < input.Length && char.IsAsciiDigit(input[p]))
            {
                var number = input[p] - '0';
                if (piece is null)
                {
                    piece = number;
                }
                else if (piece == 0)
                {
                    return null;
                }
                else
                {
                    piece = (piece * 10) + number;
                }

                if (piece > 255)
                {
                    return null;
                }

                p++;
            }

            address[pieceIndex] = (ushort)((address[pieceIndex] * 0x100) + piece!.Value);
            numbersSeen++;
            if (numbersSeen is 2 or 4)
            {
                pieceIndex++;
            }
        }

        return numbersSeen == 4 ? FinishIPv6(address, pieceIndex, compress) : null;
    }

    /// <summary>
    ///     Moves the pieces after the compression to the end of an IPv6 address, or checks that there are eight without
    ///     one.
    /// </summary>
    /// <param name="address">The pieces.</param>
    /// <param name="pieceIndex">The index after the last piece read.</param>
    /// <param name="compress">The index of the compression, or null.</param>
    /// <returns>The address, or null when it has too few pieces.</returns>
    private static ushort[]? FinishIPv6(ushort[] address, int pieceIndex, int? compress)
    {
        if (compress is not { } at)
        {
            return pieceIndex == 8 ? address : null;
        }

        var swaps = pieceIndex - at;
        pieceIndex = 7;
        while (pieceIndex != 0 && swaps > 0)
        {
            (address[pieceIndex], address[at + swaps - 1]) = (address[at + swaps - 1], address[pieceIndex]);
            pieceIndex--;
            swaps--;
        }

        return address;
    }

    /// <summary>
    ///     The WHATWG IPv6 serializer: lower-case hexadecimal pieces, the first longest run of two zero pieces or more
    ///     written as <c>::</c>.
    /// </summary>
    /// <param name="address">The eight pieces.</param>
    /// <returns>The text, without brackets.</returns>
    private static string SerializeIPv6(ushort[] address)
    {
        // The first longest run of zero pieces, when it is two pieces long at least
        int? compress = null;
        var longest = 1;
        for (var i = 0; i < 8;)
        {
            var run = 0;
            while (i + run < 8 && address[i + run] == 0)
            {
                run++;
            }

            if (run > longest)
            {
                longest = run;
                compress = i;
            }

            i += Math.Max(run, 1);
        }

        var builder = new StringBuilder();
        var ignoreZero = false;
        for (var i = 0; i < 8; i++)
        {
            if (ignoreZero && address[i] == 0)
            {
                continue;
            }

            ignoreZero = false;
            if (compress == i)
            {
                builder.Append(i == 0 ? "::" : ":");
                ignoreZero = true;
                continue;
            }

            builder.Append(address[i].ToString("x", CultureInfo.InvariantCulture));
            if (i != 7)
            {
                builder.Append(':');
            }
        }

        return builder.ToString();
    }
}
