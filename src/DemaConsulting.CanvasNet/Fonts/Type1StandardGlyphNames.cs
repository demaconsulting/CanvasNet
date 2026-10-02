namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     A small, curated table mapping Unicode codepoints to their conventional Adobe Glyph List
///     (AGL)-style Type 1 glyph names, covering common Latin/ASCII characters - used as the
///     default encoding for a standalone <c>.pfb</c>/<c>.pfa</c> font program auto-detected by
///     <see cref="TrueTypeFont.Load(Stream)"/>, which (unlike <see cref="TrueTypeFont.LoadType1"/>)
///     has no caller-supplied codepoint-to-glyph-name mapping available.
/// </summary>
/// <remarks>
///     This table intentionally duplicates (by content, not by shared code) the small subset of
///     the Adobe Glyph List that a PDF-aware caller's own, more complete glyph-name table would
///     also cover - the core <c>DemaConsulting.CanvasNet</c> assembly cannot depend on the
///     downstream <c>DemaConsulting.CanvasNet.Pdf</c> assembly (the dependency direction is the
///     other way around), so a small, self-contained, core-owned table is required for this
///     assembly's own standalone-loading feature to work without that dependency. Only glyphs
///     needed to resolve common Latin/ASCII text are covered; a font whose glyph names fall
///     outside this vocabulary simply has no <c>cmap</c> entry synthesized for the corresponding
///     codepoint (see <see cref="CmapTable.FromMap"/>), matching this library's existing
///     "gracefully degrade" posture for unmapped codepoints elsewhere.
/// </remarks>
internal static class Type1StandardGlyphNames
{
    /// <summary>
    ///     The reverse mapping (Unicode codepoint to glyph name) built from this class's own
    ///     curated glyph-name-to-codepoint vocabulary. When more than one glyph name maps to the
    ///     same codepoint, the first one encountered wins.
    /// </summary>
    internal static readonly IReadOnlyDictionary<int, string> CodepointToGlyphName = BuildCodepointToGlyphName();

    private static IReadOnlyDictionary<int, string> BuildCodepointToGlyphName()
    {
        var glyphNameToCodepoint = new (string Name, int Codepoint)[]
        {
            ("space", ' '),
            ("exclam", '!'),
            ("quotedbl", '"'),
            ("numbersign", '#'),
            ("dollar", '$'),
            ("percent", '%'),
            ("ampersand", '&'),
            ("quotesingle", '\''),
            ("parenleft", '('),
            ("parenright", ')'),
            ("asterisk", '*'),
            ("plus", '+'),
            ("comma", ','),
            ("hyphen", '-'),
            ("period", '.'),
            ("slash", '/'),
            ("zero", '0'),
            ("one", '1'),
            ("two", '2'),
            ("three", '3'),
            ("four", '4'),
            ("five", '5'),
            ("six", '6'),
            ("seven", '7'),
            ("eight", '8'),
            ("nine", '9'),
            ("colon", ':'),
            ("semicolon", ';'),
            ("less", '<'),
            ("equal", '='),
            ("greater", '>'),
            ("question", '?'),
            ("at", '@'),
            ("A", 'A'),
            ("B", 'B'),
            ("C", 'C'),
            ("D", 'D'),
            ("E", 'E'),
            ("F", 'F'),
            ("G", 'G'),
            ("H", 'H'),
            ("I", 'I'),
            ("J", 'J'),
            ("K", 'K'),
            ("L", 'L'),
            ("M", 'M'),
            ("N", 'N'),
            ("O", 'O'),
            ("P", 'P'),
            ("Q", 'Q'),
            ("R", 'R'),
            ("S", 'S'),
            ("T", 'T'),
            ("U", 'U'),
            ("V", 'V'),
            ("W", 'W'),
            ("X", 'X'),
            ("Y", 'Y'),
            ("Z", 'Z'),
            ("bracketleft", '['),
            ("backslash", '\\'),
            ("bracketright", ']'),
            ("asciicircum", '^'),
            ("underscore", '_'),
            ("grave", '`'),
            ("a", 'a'),
            ("b", 'b'),
            ("c", 'c'),
            ("d", 'd'),
            ("e", 'e'),
            ("f", 'f'),
            ("g", 'g'),
            ("h", 'h'),
            ("i", 'i'),
            ("j", 'j'),
            ("k", 'k'),
            ("l", 'l'),
            ("m", 'm'),
            ("n", 'n'),
            ("o", 'o'),
            ("p", 'p'),
            ("q", 'q'),
            ("r", 'r'),
            ("s", 's'),
            ("t", 't'),
            ("u", 'u'),
            ("v", 'v'),
            ("w", 'w'),
            ("x", 'x'),
            ("y", 'y'),
            ("z", 'z'),
            ("braceleft", '{'),
            ("bar", '|'),
            ("braceright", '}'),
            ("asciitilde", '~'),
        };

        var result = new Dictionary<int, string>();
        foreach (var (name, codepoint) in glyphNameToCodepoint)
        {
            result.TryAdd(codepoint, name); // first-wins on a (not expected) collision
        }

        return result;
    }
}
