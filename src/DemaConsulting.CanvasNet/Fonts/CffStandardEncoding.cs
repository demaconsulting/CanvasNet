// cspell:ignore seac bchar achar adx ady Agrave quoteleft quoteright exclamdown
// cspell:ignore endash questiondown caron emdash ordfeminine Lslash Oslash
// cspell:ignore ordmasculine dotlessi lslash oslash germandbls

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     The Adobe StandardEncoding table (CFF/Type 2 Charstring Format specification Appendix C;
///     equivalently, the Type 1 Font Format specification's own "StandardEncoding" appendix),
///     mapping each 8-bit character code <c>0</c>-<c>255</c> to its conventional PostScript glyph
///     name, for use by <see cref="CffTable"/>'s seac-style <c>endchar</c> accent composition
///     support (see <see cref="CffCharstringInterpreter"/>'s remarks).
/// </summary>
/// <remarks>
///     <para>
///     This table is distinct from, and must never be confused with or merged into,
///     <c>DemaConsulting.CanvasNet.Pdf</c>'s own <c>PdfDocument.Fonts.cs</c>
///     <c>StandardEncodingTable</c>: that table lives in a different assembly, maps each code to
///     a Unicode codepoint (for building a PDF-level code-to-glyph-index path through a
///     caller-supplied <c>codepointToGlyphName</c> map), and serves an entirely different
///     purpose. This table instead maps each code directly to a PostScript glyph name, for
///     resolving a seac-style <c>endchar</c>'s <c>bchar</c>/<c>achar</c> operands against
///     <em>this same CFF font's own charset</em> via <see cref="CffTable.TryGetGlyphIndex"/> -
///     no Unicode codepoint is ever involved in that resolution.
///     </para>
///     <para>
///     Built from a compact <c>code -&gt; SID</c> table (a code of <c>0</c> meaning "undefined" -
///     StandardEncoding never assigns any character code to SID <c>0</c>, <c>.notdef</c>, so this
///     is an unambiguous sentinel) indexing <see cref="CffTable.StandardStrings"/> directly,
///     mirroring that array's own documented Adobe Technical Note #5176 Appendix A source and
///     avoiding retyping roughly 150 glyph-name string literals by hand.
///     </para>
/// </remarks>
internal static class CffStandardEncoding
{
    /// <summary>
    ///     Every defined character code's Standard String ID (SID), indexing directly into
    ///     <see cref="CffTable.StandardStrings"/> (for example index <c>65</c> is <c>34</c>,
    ///     <see cref="CffTable.StandardStrings"/>[<c>34</c>] = <c>"A"</c>); <c>0</c> marks a code
    ///     StandardEncoding leaves undefined (control codes, and other gaps in the 161-255 range).
    /// </summary>
    private static readonly int[] CodeToSid =
    [
        // 0x00-0x0F
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 0x10-0x1F
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 0x20-0x2F (space .. slash)
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        // 0x30-0x3F (zero .. question)
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
        // 0x40-0x4F (at .. O)
        33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
        // 0x50-0x5F (P .. underscore)
        49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
        // 0x60-0x6F (quoteleft .. o)
        65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
        // 0x70-0x7F (p .. asciitilde, 0x7F undefined)
        81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 0,
        // 0x80-0x8F
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 0x90-0x9F
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 0xA0-0xAF (0xA0 undefined, exclamdown .. fl)
        0, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
        // 0xB0-0xBF (endash .. questiondown, two gaps)
        0, 111, 112, 113, 114, 0, 115, 116, 117, 118, 119, 120, 121, 122, 0, 123,
        // 0xC0-0xCF (0xC0 undefined, grave .. caron, two gaps)
        0, 124, 125, 126, 127, 128, 129, 130, 131, 0, 132, 133, 0, 134, 135, 136,
        // 0xD0-0xDF (emdash, rest undefined)
        137, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        // 0xE0-0xEF (AE, ordfeminine, Lslash, Oslash, OE, ordmasculine, rest undefined)
        0, 138, 0, 139, 0, 0, 0, 0, 140, 141, 142, 143, 0, 0, 0, 0,
        // 0xF0-0xFF (ae, dotlessi, lslash, oslash, oe, germandbls, rest undefined)
        0, 144, 0, 0, 0, 145, 0, 0, 146, 147, 148, 149, 0, 0, 0, 0,
    ];

    /// <summary>
    ///     The 256-entry code-to-glyph-name table, built by resolving each <see cref="CodeToSid"/>
    ///     entry against <see cref="CffTable.StandardStrings"/> (<see langword="null"/> for an
    ///     undefined code).
    /// </summary>
    internal static readonly string?[] CodeToGlyphName = BuildCodeToGlyphName();

    private static string?[] BuildCodeToGlyphName()
    {
        var names = new string?[CodeToSid.Length];
        for (var code = 0; code < CodeToSid.Length; code++)
        {
            var sid = CodeToSid[code];
            names[code] = sid == 0 ? null : CffTable.StandardStrings[sid];
        }

        return names;
    }
}
