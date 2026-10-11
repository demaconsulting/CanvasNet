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
/// <param name="Decorations">Positioned underline/strikethrough/overline bars, in the same shape-local space as <paramref name="Glyphs"/>.</param>
internal sealed record VsdxTextLayout(IReadOnlyList<VsdxGlyphPlacement> Glyphs, IReadOnlyList<VsdxDecorationBar>? Decorations = null)
{
    /// <summary>The decoration bars, never <see langword="null"/>.</summary>
    public IReadOnlyList<VsdxDecorationBar> DecorationBars => Decorations ?? [];
}

/// <summary>A single filled horizontal decoration bar (underline/strikethrough/overline), in shape-local inches.</summary>
/// <param name="X0Inches">Left edge.</param>
/// <param name="X1Inches">Right edge.</param>
/// <param name="CenterYInches">Vertical center of the bar.</param>
/// <param name="ThicknessInches">Bar thickness.</param>
/// <param name="Color">Bar color.</param>
internal sealed record VsdxDecorationBar(double X0Inches, double X1Inches, double CenterYInches, double ThicknessInches, Rgba32 Color);

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
