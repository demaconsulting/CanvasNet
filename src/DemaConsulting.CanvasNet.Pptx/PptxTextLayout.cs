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
/// <param name="Underlines">
///     Every underline segment to paint (Phase 2 Follow-Up: Underline Rendering), in reading
///     order, already positioned in the same shape-local coordinate space as
///     <paramref name="Glyphs"/>. Defaults to an empty list so every pre-existing direct
///     construction call site (for example in tests) keeps compiling unchanged.
/// </param>
internal sealed record PptxTextLayout(
    IReadOnlyList<PptxGlyphPlacement> Glyphs,
    float AppliedFontScale,
    IReadOnlyList<PptxUnderlineSegment> Underlines)
{
    /// <summary>Convenience constructor for direct-construction call sites that predate underline support (defaults to no underline segments).</summary>
    internal PptxTextLayout(IReadOnlyList<PptxGlyphPlacement> Glyphs, float AppliedFontScale)
        : this(Glyphs, AppliedFontScale, Array.Empty<PptxUnderlineSegment>())
    {
    }
}

/// <summary>
///     A single resolved, positioned underline segment ready for
///     <see cref="PptxDocument.PaintTextLayout"/> to paint (Phase 2 Follow-Up: Underline
///     Rendering), spanning one contiguous run of underlined tokens on a single line.
/// </summary>
/// <param name="StartXEmu">The segment's start X, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="EndXEmu">The segment's end X, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="BaselineYEmu">The owning line's baseline Y, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="SizeEmu">The underlined run's resolved (already autofit-scaled) font size, in EMU - used to derive the painted stroke's thickness/offset (see <see cref="PptxDocument.PaintTextLayout"/>).</param>
/// <param name="Style">The resolved underline style.</param>
/// <param name="Color">The resolved underline ink color.</param>
internal readonly record struct PptxUnderlineSegment(
    float StartXEmu,
    float EndXEmu,
    float BaselineYEmu,
    float SizeEmu,
    PptxUnderlineStyle Style,
    Rgba32 Color);

/// <summary>A single resolved, positioned glyph ready for <see cref="PptxDocument.PaintTextLayout"/> to paint.</summary>
/// <param name="Font">The resolved <see cref="TrueTypeFont"/> supplying this glyph's outline.</param>
/// <param name="GlyphIndex">The glyph's index within <see cref="Font"/> (as returned by <see cref="TrueTypeFont.GetGlyphIndex"/>).</param>
/// <param name="OriginXEmu">The glyph's baseline origin X, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="OriginYEmu">The glyph's baseline origin Y, in EMU, in the owning shape's own local coordinate space.</param>
/// <param name="SizeEmu">The glyph's resolved (already autofit-scaled) font size, in EMU.</param>
/// <param name="Color">The glyph's resolved ink color.</param>
/// <param name="OutlineWidthEmu">
///     The glyph's resolved text-outline (stroke) width, in EMU (Phase 2 Follow-Up: Run Text
///     Outline), already scaled by the same autofit factor as <paramref name="SizeEmu"/> - see
///     <see cref="PptxEffectiveRunProperties.OutlineWidthEmu"/>. <see langword="null"/> means "no
///     outline is painted for this glyph", the default for every pre-existing direct-construction
///     call site (for example in tests) that predates outline support.
/// </param>
/// <param name="OutlineColor">The glyph's resolved outline ink color, only meaningful when <paramref name="OutlineWidthEmu"/> is non-<see langword="null"/>.</param>
internal sealed record PptxGlyphPlacement(
    TrueTypeFont Font,
    int GlyphIndex,
    float OriginXEmu,
    float OriginYEmu,
    float SizeEmu,
    Rgba32 Color,
    float? OutlineWidthEmu = null,
    Rgba32 OutlineColor = default);
