// cspell:ignore Menlo Consolas Monaco Verdana Tahoma Trebuchet Segoe Roboto Garamond Palatino Cambria blinkmacsystemfont
using System.Text;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Classifies a CSS font family name as one of the generic families (sans-serif, serif, monospace) that the
///     bundled Liberation fonts stand in for, and splits a CSS <c>font-family</c> list into family names.
/// </summary>
/// <remarks>
///     <para>
///         <b>Classification.</b> These CSS generic keywords - <c>sans-serif</c>, <c>system-ui</c>, <c>ui-sans-serif</c>,
///         <c>cursive</c> and <c>fantasy</c> (all sans-serif), <c>serif</c> and <c>ui-serif</c> (serif),
///         <c>monospace</c> and <c>ui-monospace</c> (monospace) - are always recognized; other CSS generic names
///         (<c>ui-rounded</c>, <c>math</c>, <c>emoji</c>, <c>fangsong</c>) are not, and are treated as ordinary family
///         names. A caller that wants
///         well-known family names to resolve the same way on every machine can also ask for the <i>common
///         aliases</i>: names such as <c>Arial</c>, <c>Helvetica</c> and <c>Segoe UI</c> (sans-serif), <c>Times New
///         Roman</c> and <c>Georgia</c> (serif), <c>Courier New</c> and <c>Consolas</c> (monospace), and the three
///         bundled family names <c>Liberation Sans</c>, <c>Liberation Serif</c> and <c>Liberation Mono</c>. Names
///         are compared ignoring case (ordinal), and nothing else is folded.
///     </para>
///     <para>
///         <b>Splitting.</b> <see cref="SplitFamilyList"/> reads the list left to right: a comma ends a family
///         unless it is inside a single or double quote, the quotes themselves are dropped, each name is trimmed of
///         surrounding white space, and empty names are dropped. A CSS backslash escape is decoded wherever it
///         appears: a backslash followed by a character that is not a hexadecimal digit is that character taken
///         literally (so <c>\"</c>, <c>\'</c>, <c>\\</c> and <c>\,</c> never end a string or split the list), a
///         backslash followed by one to six hexadecimal digits (and one optional CSS white-space - a space, a tab
///         or a newline, where CR LF is one newline) is that Unicode code point, and a backslash at the very end
///         of the text is dropped. A backslash followed by a newline (line feed, carriage return, form feed or
///         CR LF) is a line continuation inside a quoted string and contributes nothing; outside a quoted string
///         it is not a valid escape, so the whole list is invalid and <see cref="SplitFamilyList"/> returns an
///         empty list.
///     </para>
///     <para>
///         <b>Thread safety</b>: the class is stateless apart from read-only tables, so every member may be called
///         concurrently from any number of threads.
///     </para>
/// </remarks>
public static class GenericFontFamilies
{
    /// <summary>The CSS generic keywords that select sans-serif.</summary>
    private static readonly HashSet<string> SansKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "sans-serif", "system-ui", "ui-sans-serif", "cursive", "fantasy",
    };

    /// <summary>The CSS generic keywords that select serif.</summary>
    private static readonly HashSet<string> SerifKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "ui-serif",
    };

    /// <summary>The CSS generic keywords that select monospace.</summary>
    private static readonly HashSet<string> MonospaceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "monospace", "ui-monospace",
    };

    /// <summary>The common alias names (beyond the keywords) that select sans-serif.</summary>
    private static readonly HashSet<string> SansAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "-apple-system", "blinkmacsystemfont", "helvetica", "helvetica neue", "arial", "verdana", "tahoma",
        "trebuchet ms", "segoe ui", "liberation sans", "dejavu sans", "roboto", "open sans",
    };

    /// <summary>The common alias names (beyond the keywords) that select serif.</summary>
    private static readonly HashSet<string> SerifAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "times", "times new roman", "georgia", "liberation serif", "palatino", "palatino linotype", "book antiqua",
        "cambria", "garamond",
    };

    /// <summary>The common alias names (beyond the keywords) that select monospace.</summary>
    private static readonly HashSet<string> MonospaceAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "courier", "courier new", "menlo", "consolas", "monaco", "lucida console", "liberation mono",
        "dejavu sans mono",
    };

    /// <summary>
    ///     Determines whether a family name is a generic family (or, optionally, a common alias of one) and which
    ///     bundled design stands in for it.
    /// </summary>
    /// <param name="familyName">A single family name, unquoted (see <see cref="SplitFamilyList"/>).</param>
    /// <param name="includeCommonAliases">
    ///     <see langword="true"/> to also recognize the common aliases (<c>Arial</c>, <c>Times New Roman</c>,
    ///     <c>Courier New</c>, the Liberation family names, and so on); <see langword="false"/> to recognize the CSS
    ///     generic keywords only.
    /// </param>
    /// <param name="serif">Receives <see langword="true"/> when the name selects the serif design.</param>
    /// <param name="fixedPitch">Receives <see langword="true"/> when the name selects the monospace design.</param>
    /// <returns>
    ///     <see langword="true"/> when the name is recognized (sans-serif when both outputs are
    ///     <see langword="false"/>); <see langword="false"/> for any other name, including <see langword="null"/>
    ///     and the empty string.
    /// </returns>
    public static bool TryClassify(string? familyName, bool includeCommonAliases, out bool serif, out bool fixedPitch)
    {
        serif = false;
        fixedPitch = false;
        if (string.IsNullOrEmpty(familyName))
        {
            return false;
        }

        if (SansKeywords.Contains(familyName) || (includeCommonAliases && SansAliases.Contains(familyName)))
        {
            return true;
        }

        if (SerifKeywords.Contains(familyName) || (includeCommonAliases && SerifAliases.Contains(familyName)))
        {
            serif = true;
            return true;
        }

        if (MonospaceKeywords.Contains(familyName) || (includeCommonAliases && MonospaceAliases.Contains(familyName)))
        {
            fixedPitch = true;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Splits a CSS <c>font-family</c> list into family names.
    /// </summary>
    /// <param name="cssFontFamily">The list, for example <c>"Helvetica Neue", Arial, sans-serif</c>; may be <see langword="null"/>.</param>
    /// <returns>
    ///     The names without quotes or surrounding white space, in order, with CSS backslash escapes decoded
    ///     (see this class's remarks); empty names are dropped, and a
    ///     <see langword="null"/> or white-space list, or a list with a backslash-newline outside a quoted
    ///     string (an invalid escape), yields an empty list.
    /// </returns>
    public static IReadOnlyList<string> SplitFamilyList(string? cssFontFamily)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(cssFontFamily))
        {
            return result;
        }

        var current = new StringBuilder();
        var quote = '\0';
        void Flush()
        {
            var name = current.ToString().Trim();
            current.Clear();
            if (name.Length > 0)
            {
                result.Add(name);
            }
        }

        var i = 0;
        while (i < cssFontFamily.Length)
        {
            var c = cssFontFamily[i++];
            if (c == '\\')
            {
                // A trailing lone backslash is dropped; otherwise the escape never acts as a quote or comma
                if (i < cssFontFamily.Length)
                {
                    if (IsCssNewline(cssFontFamily[i]))
                    {
                        // A backslash before a newline is a line continuation inside a string (it
                        // contributes nothing), but not a valid escape in an identifier: the list is invalid
                        if (quote == '\0')
                        {
                            return new List<string>();
                        }

                        i = SkipCssNewline(cssFontFamily, i);
                    }
                    else
                    {
                        i = AppendEscape(cssFontFamily, i, current);
                    }
                }
            }
            else if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == ',')
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return result;
    }

    /// <summary>
    ///     Decodes one CSS escape whose backslash has just been consumed, appending the result.
    /// </summary>
    /// <param name="text">The text being split.</param>
    /// <param name="start">The index of the character after the backslash.</param>
    /// <param name="target">Receives the decoded character or code point.</param>
    /// <returns>The index of the first character after the escape.</returns>
    private static int AppendEscape(string text, int start, StringBuilder target)
    {
        if (!char.IsAsciiHexDigit(text[start]))
        {
            target.Append(text[start]);
            return start + 1;
        }

        var end = start;
        var codePoint = 0;
        while (end < text.Length && end - start < 6 && char.IsAsciiHexDigit(text[end]))
        {
            codePoint = (codePoint * 16) + Convert.ToInt32(text[end].ToString(), 16);
            end++;
        }

        // One CSS white-space (space, tab or a newline; CR LF counts as one) after a hexadecimal
        // escape belongs to it; other Unicode white space (NBSP, U+2028) does not
        if (end < text.Length && IsCssWhitespace(text[end]))
        {
            end = SkipCssNewline(text, end);
        }

        // Zero, a surrogate or a value beyond Unicode decodes to the replacement character
        target.Append(codePoint is 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF)
            ? "\uFFFD"
            : char.ConvertFromUtf32(codePoint));
        return end;
    }

    /// <summary>Determines whether a character is a CSS newline (line feed, carriage return or form feed; CSS Syntax 3, preprocessing).</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for <c>\n</c>, <c>\r</c> and <c>\f</c>.</returns>
    private static bool IsCssNewline(char c) => c is '\n' or '\r' or '\f';

    /// <summary>Determines whether a character is CSS white space (space, tab or a CSS newline; CSS Syntax 3).</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a space, a tab and a CSS newline.</returns>
    private static bool IsCssWhitespace(char c) => c is ' ' or '\t' || IsCssNewline(c);

    /// <summary>Skips one CSS white-space unit, treating a carriage return followed by a line feed as a single newline.</summary>
    /// <param name="text">The text being split.</param>
    /// <param name="index">The index of the white-space character to skip.</param>
    /// <returns>The index after the skipped unit.</returns>
    private static int SkipCssNewline(string text, int index) =>
        text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? index + 2 : index + 1;
}
