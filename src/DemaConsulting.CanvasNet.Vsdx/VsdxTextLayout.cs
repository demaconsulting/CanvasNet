using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba

/// <summary>
///     The fully-resolved, laid-out glyph stream for a single shape's <c>&lt;Text&gt;</c>
///     element, produced by <c>VsdxDocument.TextLayout.cs</c>'s <c>ResolveTextLayout</c> and
///     painted onto a <see cref="Surface"/> by <c>VsdxDocument.TextRender.cs</c>'s own
///     <c>PaintTextLayout</c> as part of the public <c>Render</c> API - see
///     <c>VsdxDocument.Render.cs</c>'s <c>RenderShapeRecursive</c> for the call site.
/// </summary>
/// <param name="Glyphs">
///     Every non-whitespace glyph to paint, in reading order, already positioned in the owning
///     shape's own local, unrotated/unflipped coordinate space (the same space
///     <see cref="VsdxShapeTransform"/> maps from).
/// </param>
internal sealed record VsdxTextLayout(IReadOnlyList<VsdxGlyphPlacement> Glyphs);

/// <summary>A single resolved, positioned glyph ready for <c>VsdxDocument.TextRender.cs</c>'s <c>PaintTextLayout</c> to paint.</summary>
/// <param name="Font">The resolved <see cref="TrueTypeFont"/> supplying this glyph's outline.</param>
/// <param name="GlyphIndex">The glyph's index within <see cref="Font"/> (as returned by <see cref="TrueTypeFont.GetGlyphIndex"/>).</param>
/// <param name="OriginXInches">The glyph's baseline origin X, in shape-local inches.</param>
/// <param name="OriginYInches">The glyph's baseline origin Y, in shape-local inches.</param>
/// <param name="SizeInches">The glyph's resolved font em-height, in inches.</param>
/// <param name="Color">The glyph's resolved ink color.</param>
internal sealed record VsdxGlyphPlacement(
    TrueTypeFont Font,
    int GlyphIndex,
    double OriginXInches,
    double OriginYInches,
    double SizeInches,
    Rgba32 Color);
