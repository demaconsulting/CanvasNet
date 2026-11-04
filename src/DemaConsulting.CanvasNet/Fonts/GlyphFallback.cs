using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Per-character glyph fallback to the bundled Noto fonts, for a character that the font a caller
///     chose has no glyph for.
/// </summary>
/// <remarks>
///     <para>
///     The bundled Liberation fonts cover Latin, Greek, Cyrillic, and common punctuation, but not
///     mathematical operators, most arrows, or many symbols, so a character such as U+221D (proportional
///     to) or U+226B (much greater than) would otherwise be drawn as the font's missing-glyph box
///     (<c>.notdef</c>) without any diagnostic. The fallback chain is the three Noto fonts that are
///     already embedded in this assembly (OFL-1.1, see <c>Fonts/BundledFonts/README.md</c>) in this
///     order: Noto Sans (Greek, Latin extensions, general punctuation), Noto Sans Math (mathematical
///     operators, arrows, supplemental math), Noto Sans Symbols 2 (shapes and rare symbols).
///     </para>
///     <para>
///     The chain is <em>deterministic and machine independent</em>: only these bundled fonts are
///     consulted, never an installed font. It is the regular style only; a bold or italic request is not
///     synthesized for a fallback glyph. A character that no chain font covers stays unresolved
///     (<see cref="Resolve"/> returns glyph index 0 of the primary font, <see cref="FindMissing"/> lists
///     it) so a caller can report it. The Noto fonts are loaded lazily on the first character the primary
///     font misses, so text that the primary font covers never pays for them.
///     </para>
///     <para>
///     Pure and thread-safe: the chain is built once under a lock by
///     <see cref="SystemFontCatalog.LoadBundledFallbackCore"/> and read-only afterwards.
///     </para>
/// </remarks>
public static class GlyphFallback
{
    /// <summary>
    ///     The bundled file names of the chain, in lookup order.
    /// </summary>
    private static readonly string[] ChainFileNames =
    [
        "NotoSans-Regular.ttf",
        "NotoSansMath-Regular.ttf",
        "NotoSansSymbols2-Regular.ttf",
    ];

    /// <summary>
    ///     The lazily built chain; <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> so concurrent
    ///     first users share one construction.
    /// </summary>
    private static readonly Lazy<ReadOnlyCollection<TrueTypeFont>> LazyChain =
        new(BuildChain, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets the ordered bundled fallback fonts: Noto Sans, Noto Sans Math, Noto Sans Symbols 2.
    /// </summary>
    /// <value>
    ///     The same three font instances on every access for the lifetime of the process, in a read-only
    ///     collection that a caller cannot mutate (it cannot be cast back to an array). Reading this
    ///     property loads them if they are not loaded yet.
    /// </value>
    public static IReadOnlyList<TrueTypeFont> BundledFonts => LazyChain.Value;

    /// <summary>
    ///     Tells whether a code point is a character that is expected to have a visible glyph.
    /// </summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>
    ///     <see langword="false"/> for control characters, white space, format characters (zero-width joiners,
    ///     bidirectional marks), variation selectors, surrogates, and non-characters, which are never
    ///     replaced by a fallback glyph and never reported as missing; otherwise <see langword="true"/>.
    /// </returns>
    public static bool NeedsGlyph(int codePoint)
    {
        // Ignorable characters never have a glyph; checking them first also guarantees that no Rune is
        // built for an invalid code point. Every remaining white-space character (Unicode White_Space is
        // exactly the Control, SpaceSeparator, LineSeparator, and ParagraphSeparator members) is invisible
        // too, so what is left is a character that is expected to draw.
        return !IsIgnorable(codePoint) && !Rune.IsWhiteSpace(new Rune(codePoint));
    }

    /// <summary>
    ///     Tells whether a code point is invisible and zero width, so that a text layout must skip it
    ///     without drawing a glyph, without advancing the pen, and without breaking kerning.
    /// </summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>
    ///     <see langword="true"/> for an out-of-range value, a surrogate, a non-character, a variation
    ///     selector (U+FE00-U+FE0F, U+E0100-U+E01EF), a format character (zero-width joiners, bidirectional
    ///     marks, U+00AD soft hyphen, U+FEFF), or a control character that is not white space; otherwise
    ///     <see langword="false"/>. White space (including the tab and line-break controls) is
    ///     <em>not</em> ignorable: a layout handles it itself.
    /// </returns>
    /// <remarks>
    ///     This is the single definition shared by <see cref="NeedsGlyph"/> and the text layouts, so an
    ///     invisible character is never drawn as the missing-glyph box (<c>.notdef</c>) and never
    ///     reported as missing. Pure and thread-safe.
    /// </remarks>
    public static bool IsIgnorable(int codePoint)
    {
        // Out-of-range values are not Unicode scalar values at all.
        if (codePoint < 0 || codePoint > 0x10FFFF)
        {
            return true;
        }

        // Rune.GetUnicodeCategory rejects surrogates, so they are handled before constructing a Rune.
        if (codePoint is >= 0xD800 and <= 0xDFFF)
        {
            return true;
        }

        // Variation selectors modify the previous character and have no glyph of their own.
        if (codePoint is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)
        {
            return true;
        }

        // Non-characters are never assigned.
        if (codePoint is >= 0xFDD0 and <= 0xFDEF || (codePoint & 0xFFFE) == 0xFFFE)
        {
            return true;
        }

        // Format characters (joiners, bidirectional marks, soft hyphen, BOM) are invisible; a control character is
        // too unless it is white space, which a layout treats as a space or line break itself.
        var rune = new Rune(codePoint);
        return Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format
            || (Rune.IsControl(rune) && !Rune.IsWhiteSpace(rune));
    }

    /// <summary>
    ///     Resolves the font and glyph index to use for a code point, falling back to the bundled fonts
    ///     when the primary font has no glyph for it.
    /// </summary>
    /// <param name="primary">The font the caller chose. Must not be <see langword="null"/>.</param>
    /// <param name="codePoint">The Unicode code point to draw.</param>
    /// <returns>
    ///     The primary font and its glyph index when the primary font maps the code point; otherwise, when
    ///     <see cref="NeedsGlyph"/> accepts the code point, the first bundled font (other than
    ///     <paramref name="primary"/> itself) that maps it, with that font's glyph index; otherwise the
    ///     primary font with glyph index 0 (its missing-glyph glyph).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="primary"/> is <see langword="null"/>.</exception>
    public static (TrueTypeFont Font, int GlyphIndex) Resolve(TrueTypeFont primary, int codePoint)
    {
        ArgumentNullException.ThrowIfNull(primary);

        // Fast path: nothing is loaded and nothing changes for a character the primary font covers.
        var glyph = primary.GetGlyphIndex(codePoint);
        if (glyph != 0 || !NeedsGlyph(codePoint))
        {
            return (primary, glyph);
        }

        foreach (var candidate in LazyChain.Value)
        {
            if (ReferenceEquals(candidate, primary))
            {
                continue;
            }

            var candidateGlyph = candidate.GetGlyphIndex(codePoint);
            if (candidateGlyph != 0)
            {
                return (candidate, candidateGlyph);
            }
        }

        return (primary, 0);
    }

    /// <summary>
    ///     Lists the characters of a text that neither the primary font nor the bundled fallback fonts cover.
    /// </summary>
    /// <param name="primary">The font the caller chose. Must not be <see langword="null"/>.</param>
    /// <param name="text">The text to check. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     The distinct code points of <paramref name="text"/> that would be drawn as the missing-glyph box,
    ///     in ascending order; empty when every visible character resolves. A lone surrogate counts as
    ///     U+FFFD, as it does when the text is drawn.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="primary"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<int> FindMissing(TrueTypeFont primary, string text)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(text);

        var missing = new SortedSet<int>();
        foreach (var codePoint in text.EnumerateRunes().Select(rune => rune.Value))
        {
            if (!NeedsGlyph(codePoint) || missing.Contains(codePoint))
            {
                continue;
            }

            if (Resolve(primary, codePoint).GlyphIndex == 0)
            {
                missing.Add(codePoint);
            }
        }

        return [.. missing];
    }

    /// <summary>
    ///     Loads the chain from the embedded resources.
    /// </summary>
    /// <returns>The chain fonts in lookup order, wrapped read-only; the array never escapes.</returns>
    private static ReadOnlyCollection<TrueTypeFont> BuildChain()
    {
        var fonts = new TrueTypeFont[ChainFileNames.Length];
        for (var i = 0; i < fonts.Length; i++)
        {
            fonts[i] = SystemFontCatalog.LoadBundledFallbackCore(ChainFileNames[i]);
        }

        return Array.AsReadOnly(fonts);
    }
}
