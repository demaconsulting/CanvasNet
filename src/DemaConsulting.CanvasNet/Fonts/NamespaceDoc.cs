// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO ttcf
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Fonts"/> namespace provides SFNT font parsing
///     through <see cref="TrueTypeFont"/>: container/table-directory parsing, <c>cmap</c>
///     character-to-glyph mapping, glyph outline extraction into
///     <see cref="DemaConsulting.CanvasNet.Geometry.Path"/> from either TrueType
///     <c>glyf</c>/<c>loca</c> outlines (including composite glyphs) or CFF/OpenType (<c>CFF </c>,
///     <c>OTTO</c>-tagged) Type 2 charstring outlines, <c>hmtx</c>/<c>hhea</c> horizontal metrics,
///     and basic (<c>kern</c> format 0) pairwise kerning. TrueType Collection (<c>ttcf</c>)
///     containers holding one or more faces are also supported, with explicit face selection and
///     face-count querying, including the flex shortcuts (<c>hflex</c>/<c>flex</c>/
///     <c>hflex1</c>/<c>flex1</c>) and the deprecated 4-operand <c>seac</c>-style accent
///     composition form of <c>endchar</c>. CID-keyed CFF fonts (a Top DICT declaring the
///     <c>ROS</c> operator), a doubly-nested <c>seac</c>-style composition, and any Type 2
///     charstring operator outside the supported set are out of scope and are rejected with a
///     clear <see cref="System.IO.InvalidDataException"/> rather than silently mis-parsed.
///     <see cref="TrueTypeFont"/> depends only on the
///     <see cref="DemaConsulting.CanvasNet.Geometry"/> namespace: it produces vector geometry and
///     metrics, and has no notion of pixels, surfaces, or rasterization - callers combine its
///     output with their own point size and orientation before feeding it to
///     <see cref="DemaConsulting.CanvasNet.Drawing.PathFiller"/> or
///     <see cref="DemaConsulting.CanvasNet.Drawing.PathStroker"/>.
/// </summary>
internal static class NamespaceDoc
{
}
