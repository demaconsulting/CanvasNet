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
///         surrounding white space, and empty names are dropped.
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
    ///     The names without quotes or surrounding white space, in order; empty names are dropped, and a
    ///     <see langword="null"/> or white-space list yields an empty list.
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

        foreach (var c in cssFontFamily)
        {
            if (quote != '\0')
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
}
