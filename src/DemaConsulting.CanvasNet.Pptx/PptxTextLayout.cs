using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     The fully-resolved, laid-out glyph stream for a single <c>&lt;p:txBody&gt;</c>, produced by
///     <see cref="PptxDocument.ResolveTextLayout"/> and consumed by
///     <see cref="PptxDocument.PaintTextLayout"/>.
/// </summary>
/// <param name="Glyphs">
///     Every non-whitespace glyph to paint, in reading order, already positioned in the owning
///     shape's own local, unrotated/unflipped geometry coordinate space (the same space
///     <see cref="PptxShapeFrame.Transform"/> maps from).
/// </param>
/// <param name="AppliedFontScale">
///     The autofit font-size scale factor actually applied (<c>1.0</c> = no scaling) - see the
///     design document's "Text Layout and Rendering (Phase 1d)" autofit policy for when this is
///     read verbatim from <c>&lt;a:normAutofit fontScale="..."/&gt;</c>, computed by the bounded
///     shrink loop, or left at <c>1.0</c>.
/// </param>
internal sealed record PptxTextLayout(IReadOnlyList<PptxGlyphPlacement> Glyphs, float AppliedFontScale);

/// <summary>A single resolved, positioned glyph ready for <see cref="PptxDocument.PaintTextLayout"/> to paint.</summary>
/// <param name="Font">The resolved <see cref="TrueTypeFont"/> supplying this glyph's outline.</param>
/// <param name="GlyphIndex">The glyph's index within <see cref="Font"/> (as returned by <see cref="TrueTypeFont.GetGlyphIndex"/>).</param>
/// <param name="OriginXEmu">The glyph's baseline origin X, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="OriginYEmu">The glyph's baseline origin Y, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="SizeEmu">The glyph's resolved (already autofit-scaled) font size, in EMU.</param>
/// <param name="Color">The glyph's resolved ink color.</param>
internal sealed record PptxGlyphPlacement(
    TrueTypeFont Font,
    int GlyphIndex,
    float OriginXEmu,
    float OriginYEmu,
    float SizeEmu,
    Rgba32 Color);
