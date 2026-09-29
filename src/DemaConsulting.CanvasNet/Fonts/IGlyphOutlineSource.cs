// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Common contract implemented by every glyph outline decoding backend
///     (<see cref="GlyfLocaReader"/> for TrueType <c>glyf</c> outlines, <see cref="CffTable"/> for
///     CFF outlines), letting <see cref="TrueTypeFont"/> dispatch <see cref="TrueTypeFont.GetGlyphOutline"/>
///     without a type-check against which outline flavor the loaded font actually uses.
/// </summary>
internal interface IGlyphOutlineSource
{
    /// <summary>
    ///     The total number of glyphs this outline source can decode.
    /// </summary>
    int GlyphCount { get; }

    /// <summary>
    ///     Decodes a single glyph's outline.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to decode.</param>
    /// <returns>
    ///     The glyph's outline in raw font design units, or <see cref="Path.Empty"/> for a glyph
    ///     with no contour data.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's outline data is malformed, truncated, or exceeds an internal
    ///     resource bound.
    /// </exception>
    Path GetGlyphOutline(int glyphIndex);
}
