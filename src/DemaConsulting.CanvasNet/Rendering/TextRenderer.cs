using System.Numerics;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using Rgba32 = DemaConsulting.CanvasNet.Canvas.Rgba32;

namespace DemaConsulting.CanvasNet.Rendering;

/// <summary>
///     Text measurement and rendering built on the <see cref="TrueTypeFont"/> outline-and-metrics
///     API. Every glyph outline is emitted as a transformed <see cref="Path"/> composed against
///     the target <see cref="Canvas"/>'s <see cref="Canvas.CurrentTransform"/>, so text follows
///     any transform (translate / rotate) already applied to the canvas.
/// </summary>
public static class TextRenderer
{
    /// <summary>
    ///     Measures the layout metrics of a text run at the requested pixel size.
    /// </summary>
    /// <param name="text">The text to measure. Must not be <see langword="null"/>.</param>
    /// <param name="font">The font to measure with. Must not be <see langword="null"/>.</param>
    /// <param name="size">
    ///     The text size in pixels (mapping <c>UnitsPerEm</c> font design units to
    ///     <paramref name="size"/> local-space units). Must be finite and positive.
    /// </param>
    /// <returns>
    ///     The measured <see cref="TextMetrics"/>: total advance width including kerning,
    ///     ascender height, and descender depth — all scaled to <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not finite or is not positive.</exception>
    public static TextMetrics MeasureText(string text, TrueTypeFont font, float size)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ValidateSize(size);

        var scale = size / font.UnitsPerEm;
        var width = 0f;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                width += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            width += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }

        var ascent = font.Ascender * scale;
        // Descent is documented as the absolute value of the font descender: most fonts store
        // hhea.descender as a negative value (below the baseline), but the font loader preserves
        // whatever sign the font file uses as-is, so a bare negation would yield a negative
        // Descent for a (non-conformant) font whose raw descender happens to be positive.
        var descent = MathF.Abs(font.Descender * scale);
        return new TextMetrics(width, ascent, descent);
    }

    /// <summary>
    ///     Renders a text run onto <paramref name="canvas"/> at the given baseline anchor.
    /// </summary>
    /// <param name="canvas">The target canvas. Must not be <see langword="null"/>.</param>
    /// <param name="text">The text to render. Must not be <see langword="null"/>.</param>
    /// <param name="x">The horizontal anchor coordinate, interpreted per <paramref name="align"/>.</param>
    /// <param name="y">The baseline y-coordinate. Glyphs extend upward from this line by their ascender height.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="x"/>.</param>
    /// <param name="font">The font to render with. Must not be <see langword="null"/>.</param>
    /// <param name="size">The text pixel size. Must be finite and positive.</param>
    /// <param name="color">The color to fill glyphs with.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="canvas"/>, <paramref name="text"/>, or <paramref name="font"/>
    ///     is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="align"/> is not a defined <see cref="TextAlign"/> value,
    ///     or <paramref name="size"/> is not finite or is not positive.
    /// </exception>
    public static void DrawText(this Canvas canvas, string text, float x, float y, TextAlign align, TrueTypeFont font, float size, Rgba32 color)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ValidateSize(size);
        if (!Enum.IsDefined(align))
        {
            throw new ArgumentOutOfRangeException(nameof(align), align, "Alignment must be a defined TextAlign value.");
        }

        var scale = size / font.UnitsPerEm;
        var totalAdvance = MeasureText(text, font, size).Width;
        var alignOffset = align switch
        {
            TextAlign.Center => -totalAdvance / 2f,
            TextAlign.Right => -totalAdvance,
            _ => 0f,
        };

        var pen = x + alignOffset;
        int? previousGlyph = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyphIndex = font.GetGlyphIndex(rune.Value);
            if (previousGlyph.HasValue)
            {
                pen += font.GetKerning(previousGlyph.Value, glyphIndex) * scale;
            }

            var outline = font.GetGlyphOutline(glyphIndex);
            if (outline.Subpaths.Count > 0)
            {
                // Bake the glyph's local-space y-flip + font-units-to-pixel scale + baseline
                // translation, then compose the canvas' current transform on the outside so the
                // glyph inherits any translate/rotate the caller has already stacked.
                var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, y) * canvas.CurrentTransform;
                var transformed = outline.Transform(glyphMatrix);
                Drawing.PathFiller.Fill(canvas.Surface, transformed, color);
            }

            pen += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }
    }

    private static void ValidateSize(float size)
    {
        if (!float.IsFinite(size) || size <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be a finite, positive value.");
        }
    }
}
