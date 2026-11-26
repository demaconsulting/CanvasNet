using System.Collections.Frozen;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The <c>mathvariant</c> values of MathML 3: the styles of the Unicode Mathematical Alphanumeric Symbols.
/// </summary>
/// <remarks>
///     MathML Core only keeps <c>normal</c> (which cancels the automatic italic of <c>mi</c>); this package also draws the
///     other values, as Firefox does, by mapping each character to its styled code point (<see cref="MathVariants"/>).
/// </remarks>
internal enum MathVariant
{
    /// <summary><c>normal</c>: no transform (and no automatic italic).</summary>
    Normal,

    /// <summary><c>bold</c>.</summary>
    Bold,

    /// <summary><c>italic</c>.</summary>
    Italic,

    /// <summary><c>bold-italic</c>.</summary>
    BoldItalic,

    /// <summary><c>double-struck</c>.</summary>
    DoubleStruck,

    /// <summary><c>bold-fraktur</c>.</summary>
    BoldFraktur,

    /// <summary><c>script</c>.</summary>
    Script,

    /// <summary><c>bold-script</c>.</summary>
    BoldScript,

    /// <summary><c>fraktur</c>.</summary>
    Fraktur,

    /// <summary><c>sans-serif</c>.</summary>
    SansSerif,

    /// <summary><c>bold-sans-serif</c>.</summary>
    BoldSansSerif,

    /// <summary><c>sans-serif-italic</c>.</summary>
    SansSerifItalic,

    /// <summary><c>sans-serif-bold-italic</c>.</summary>
    SansSerifBoldItalic,

    /// <summary><c>monospace</c>.</summary>
    Monospace,

    /// <summary><c>initial</c> (Arabic).</summary>
    Initial,

    /// <summary><c>tailed</c> (Arabic).</summary>
    Tailed,

    /// <summary><c>looped</c> (Arabic).</summary>
    Looped,

    /// <summary><c>stretched</c> (Arabic).</summary>
    Stretched,
}

/// <summary>
///     Maps characters to the Unicode Mathematical Alphanumeric Symbols: the automatic italic of a single-character
///     <c>mi</c> and every <c>mathvariant</c> value.
/// </summary>
/// <remarks>
///     <para>
///         The automatic italic follows the italic table of MathML Core (Appendix C.1, the CSS
///         <c>text-transform: math-auto</c>): Latin letters, the dotless i and j, Greek letters, nabla and the partial
///         differential. The <c>mathvariant</c> values follow the <c>&lt;font&gt;</c> decompositions of UnicodeData.txt,
///         including the letterlike symbols that fill the holes of the alphanumeric block (for example the double-struck
///         capital C, U+2102, and the italic small h, the Planck constant U+210E). A character with no styled form (for
///         example a bold-italic digit) is left unchanged. The tables are generated (<c>MathVariants.Data.cs</c>, see
///         <c>third-party/README.md</c>).
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; the tables are built once.
///     </para>
/// </remarks>
internal static partial class MathVariants
{
    /// <summary>The names of the values as written in the attribute, ASCII case-insensitive.</summary>
    private static readonly FrozenDictionary<string, MathVariant> Names = new Dictionary<string, MathVariant>(StringComparer.OrdinalIgnoreCase)
    {
        ["normal"] = MathVariant.Normal,
        ["bold"] = MathVariant.Bold,
        ["italic"] = MathVariant.Italic,
        ["bold-italic"] = MathVariant.BoldItalic,
        ["double-struck"] = MathVariant.DoubleStruck,
        ["bold-fraktur"] = MathVariant.BoldFraktur,
        ["script"] = MathVariant.Script,
        ["bold-script"] = MathVariant.BoldScript,
        ["fraktur"] = MathVariant.Fraktur,
        ["sans-serif"] = MathVariant.SansSerif,
        ["bold-sans-serif"] = MathVariant.BoldSansSerif,
        ["sans-serif-italic"] = MathVariant.SansSerifItalic,
        ["sans-serif-bold-italic"] = MathVariant.SansSerifBoldItalic,
        ["monospace"] = MathVariant.Monospace,
        ["initial"] = MathVariant.Initial,
        ["tailed"] = MathVariant.Tailed,
        ["looped"] = MathVariant.Looped,
        ["stretched"] = MathVariant.Stretched,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The automatic italic table by base code point.</summary>
    private static readonly FrozenDictionary<int, int> AutoItalicTable = BuildPairs(AutoItalicPairs);

    /// <summary>The tables of the values other than <c>normal</c>, keyed by value and base code point.</summary>
    private static readonly FrozenDictionary<(MathVariant Variant, int CodePoint), int> VariantTable = BuildVariantTable();

    /// <summary>
    ///     Gets the number of values (18).
    /// </summary>
    public static int Count => Names.Count;

    /// <summary>
    ///     Parses a <c>mathvariant</c> value.
    /// </summary>
    /// <param name="text">The attribute value; whitespace around it makes it invalid, as MathML Core matches the values exactly.</param>
    /// <param name="variant">The value when the text is valid.</param>
    /// <returns>True when the text is one of the 18 values (ASCII case-insensitive).</returns>
    public static bool TryParse(string? text, out MathVariant variant)
    {
        variant = MathVariant.Normal;
        return text is not null && Names.TryGetValue(text, out variant);
    }

    /// <summary>
    ///     Maps a code point to its styled form.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="variant">The value.</param>
    /// <returns>The styled code point, or <paramref name="codePoint"/> when the value is <c>normal</c> or the style has no form of it.</returns>
    public static int Map(int codePoint, MathVariant variant) =>
        variant != MathVariant.Normal && VariantTable.TryGetValue((variant, codePoint), out var mapped) ? mapped : codePoint;

    /// <summary>
    ///     Maps a code point to its automatic italic form (MathML Core <c>text-transform: math-auto</c>).
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The mathematical italic code point, or <paramref name="codePoint"/> when the italic table has none (for example a digit or U+221E).</returns>
    public static int AutoItalic(int codePoint) => AutoItalicTable.TryGetValue(codePoint, out var mapped) ? mapped : codePoint;

    /// <summary>
    ///     Builds a table from base and mapped pairs.
    /// </summary>
    /// <param name="pairs">The pairs, flattened.</param>
    /// <returns>The table.</returns>
    private static FrozenDictionary<int, int> BuildPairs(ReadOnlySpan<int> pairs)
    {
        var table = new Dictionary<int, int>(pairs.Length / 2);
        for (var i = 0; i < pairs.Length; i += 2)
        {
            table[pairs[i]] = pairs[i + 1];
        }

        return table.ToFrozenDictionary();
    }

    /// <summary>
    ///     Builds the table of the values other than <c>normal</c>.
    /// </summary>
    /// <returns>The table.</returns>
    private static FrozenDictionary<(MathVariant Variant, int CodePoint), int> BuildVariantTable()
    {
        var table = new Dictionary<(MathVariant Variant, int CodePoint), int>();
        foreach (var (variant, pairs) in VariantPairs)
        {
            for (var i = 0; i < pairs.Length; i += 2)
            {
                table[(variant, pairs[i])] = pairs[i + 1];
            }
        }

        return table.ToFrozenDictionary();
    }
}
