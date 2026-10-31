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
        ValidateAlign(align);

        var scale = size / font.UnitsPerEm;
        var totalAdvance = MeasureText(text, font, size).Width;
        var alignOffset = AlignmentOffset(align, totalAdvance);

        foreach (var (outline, pen) in EnumerateGlyphPlacements(text, font, scale, x + alignOffset))
        {
            // Bake the glyph's local-space y-flip + font-units-to-pixel scale + baseline
            // translation, then compose the canvas' current transform on the outside so the
            // glyph inherits any translate/rotate the caller has already stacked.
            var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, y) * canvas.CurrentTransform;
            var transformed = outline.Transform(glyphMatrix);
            Drawing.PathFiller.Fill(canvas.Surface, transformed, color);
        }
    }

    /// <summary>
    ///     Converts a text run into a single combined outline <see cref="Path"/> in the caller's own
    ///     coordinate space, ready to be transformed, filled, stroked, or used as a clip.
    /// </summary>
    /// <param name="text">The text to convert. Must not be <see langword="null"/>.</param>
    /// <param name="font">The font whose glyph outlines and metrics are used. Must not be <see langword="null"/>.</param>
    /// <param name="size">The text size in pixels. Must be finite and positive.</param>
    /// <param name="x">The horizontal anchor coordinate, interpreted per <paramref name="align"/>.</param>
    /// <param name="y">The baseline y-coordinate. Glyphs extend upward (toward smaller y) from this line.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="x"/>; defaults to <see cref="TextAlign.Left"/>.</param>
    /// <returns>
    ///     One path holding the outlines of every glyph with contour data, in y-down coordinates with
    ///     no canvas transform applied; <see cref="Path.Empty"/> when the text is empty or no glyph has
    ///     contours (for example whitespace only).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="size"/> is not finite or is not positive, or <paramref name="align"/>
    ///     is not a defined <see cref="TextAlign"/> value.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     Exists for callers that need the text as geometry instead of pixels: a vector renderer that
    ///     applies its own accumulated transform later, builds a clip or mask from text, or strokes the
    ///     glyph outlines. Pen advance, kerning, alignment, and the y-flip are exactly those of
    ///     <see cref="DrawText"/> and <see cref="MeasureText"/> (the same glyph-placement code runs for
    ///     all three), so <c>CreateTextPath</c> followed by a fill matches the drawn text.
    ///     </para>
    ///     <para>
    ///     The glyphs are merged into one path, so overlapping glyphs are filled as one shape under the
    ///     caller's fill rule rather than composited one after another as <see cref="DrawText"/> does.
    ///     Pure function: no shared state is touched, so it is safe for concurrent use.
    ///     </para>
    /// </remarks>
    public static Path CreateTextPath(string text, TrueTypeFont font, float size, float x, float y, TextAlign align = TextAlign.Left)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ValidateSize(size);
        ValidateAlign(align);

        var scale = size / font.UnitsPerEm;
        var totalAdvance = MeasureText(text, font, size).Width;
        var alignOffset = AlignmentOffset(align, totalAdvance);

        // Every glyph is appended to one builder so the run becomes a single path without an
        // intermediate Path per glyph.
        var builder = new PathBuilder();
        foreach (var (outline, pen) in EnumerateGlyphPlacements(text, font, scale, x + alignOffset))
        {
            // The font's outline is Y-up in design units; flipping the y scale maps it into
            // Y-down space in the same step as scaling to pixels and moving the origin to the pen.
            builder.AppendPath(outline, Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(pen, y));
        }

        return builder.Build();
    }

    /// <summary>
    ///     Walks a text run exactly as <see cref="MeasureText"/> measures it and yields, for every
    ///     glyph that has contour data, its outline and the pen position it is placed at.
    /// </summary>
    /// <param name="text">The text to walk.</param>
    /// <param name="font">The font providing glyph indices, kerning, advances, and outlines.</param>
    /// <param name="scale">The design-units-to-pixel scale (<c>size / UnitsPerEm</c>).</param>
    /// <param name="startPen">The pen x-position before the first glyph, with any alignment shift applied.</param>
    /// <returns>The outline and pen x-position of each glyph with at least one subpath, in text order.</returns>
    /// <remarks>
    ///     The single home of the rune iteration, kerning-before-advance order, and float accumulation
    ///     order, so <see cref="DrawText"/> and <see cref="CreateTextPath"/> cannot drift apart.
    ///     Glyphs without outlines (spaces) still advance the pen and take part in kerning.
    /// </remarks>
    private static IEnumerable<(Path Outline, float Pen)> EnumerateGlyphPlacements(string text, TrueTypeFont font, float scale, float startPen)
    {
        var pen = startPen;
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
                yield return (outline, pen);
            }

            pen += font.GetAdvanceWidth(glyphIndex) * scale;
            previousGlyph = glyphIndex;
        }
    }

    /// <summary>
    ///     Computes the shift applied to the pen start so a run of the given width is aligned
    ///     relative to its anchor.
    /// </summary>
    /// <param name="align">The alignment; any value other than center or right means left.</param>
    /// <param name="totalAdvance">The measured advance width of the run.</param>
    /// <returns>Zero for left, minus half the width for center, minus the full width for right.</returns>
    private static float AlignmentOffset(TextAlign align, float totalAdvance) =>
        align switch
        {
            TextAlign.Center => -totalAdvance / 2f,
            TextAlign.Right => -totalAdvance,
            _ => 0f,
        };

    /// <summary>
    ///     Rejects an alignment that is not a defined <see cref="TextAlign"/> value.
    /// </summary>
    /// <param name="align">The alignment to check.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="align"/> is not defined.</exception>
    private static void ValidateAlign(TextAlign align)
    {
        if (!Enum.IsDefined(align))
        {
            throw new ArgumentOutOfRangeException(nameof(align), align, "Alignment must be a defined TextAlign value.");
        }
    }

    /// <summary>
    ///     Rejects a text size that is not a finite, positive number.
    /// </summary>
    /// <param name="size">The size to check.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not finite or not positive.</exception>
    private static void ValidateSize(float size)
    {
        if (!float.IsFinite(size) || size <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be a finite, positive value.");
        }
    }
}
