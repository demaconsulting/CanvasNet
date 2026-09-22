// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Fonts"/> namespace provides TrueType
///     (<c>glyf</c>-based) SFNT font parsing through <see cref="TrueTypeFont"/>: container/table-
///     directory parsing, <c>cmap</c> character-to-glyph mapping, <c>glyf</c>/<c>loca</c> outline
///     extraction (including composite glyphs) into <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>,
///     <c>hmtx</c>/<c>hhea</c> horizontal metrics, and basic (<c>kern</c> format 0) pairwise
///     kerning. CFF/OpenType (<c>OTTO</c>) outlines are out of scope and are rejected with a clear
///     <see cref="System.IO.InvalidDataException"/> rather than silently mis-parsed.
///     <see cref="TrueTypeFont"/> depends only on the <see cref="DemaConsulting.CanvasNet.Geometry"/>
///     namespace: it produces vector geometry and metrics, and has no notion of pixels, surfaces,
///     or rasterization - callers combine its output with their own point size and orientation
///     before feeding it to <see cref="DemaConsulting.CanvasNet.Drawing.PathFiller"/> or
///     <see cref="DemaConsulting.CanvasNet.Drawing.PathStroker"/>.
/// </summary>
internal static class NamespaceDoc
{
}
