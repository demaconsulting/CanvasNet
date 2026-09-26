using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Rendering;

/// <summary>Unit tests for <see cref="TextRenderer"/>.</summary>
public class TextRendererTests
{
    // Synthetic font: two glyphs.
    //   .notdef (index 0) with advance 0
    //   'A' (codepoint 65, index 1) with advance 500 units, outline is a triangle.
    // Ascender 800, descender -200. UnitsPerEm 1000.
    // Kerning pair (1, 1): -50.
    private static TrueTypeFont NewFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph(); // zero contours
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(0, 0, true), (500, 0, true), (250, 800, true)]);

        var kern = SyntheticFontBuilder.KernFormat0([(1, 1, -50)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth.</summary>
    [Fact]
    public void TextRenderer_MeasureText_EmptyString_ReturnsZeroWidth()
    {
        var font = NewFont();
        Assert.Equal(0f, TextRenderer.MeasureText("", font, 16f).Width);
    }

    /// <summary>TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize.</summary>
    [Fact]
    public void TextRenderer_MeasureText_SingleGlyph_ReturnsAdvanceWidthScaledBySize()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 20f);
        // Advance 500 units * (20 / 1000) = 10.
        Assert.Equal(10f, metrics.Width, 4);
    }

    /// <summary>TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning.</summary>
    [Fact]
    public void TextRenderer_MeasureText_MultipleGlyphs_SumsAdvancesAndAppliesKerning()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("AA", font, 100f);
        // Two glyphs: 500 + kern(1,1) = -50 + 500 = 950 units * (100/1000) = 95.
        Assert.Equal(95f, metrics.Width, 3);
    }

    /// <summary>TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics.</summary>
    [Fact]
    public void TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 100f);
        Assert.Equal(80f, metrics.Ascent, 3);
        Assert.Equal(20f, metrics.Descent, 3);
    }

    /// <summary>
    ///     A synthetic font whose <c>hhea.descender</c> is stored as a POSITIVE value, unlike
    ///     the normal (and every other test's) negative-descender convention. The font loader
    ///     preserves whatever sign the font file happens to use, without enforcing negativity, so
    ///     this exercises the documented "Descent is the absolute value of the descender"
    ///     contract against a font that violates the usual sign convention.
    /// </summary>
    private static TrueTypeFont NewFontWithPositiveDescender()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(0, 0, true), (500, 0, true), (250, 800, true)]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, 200, 0, 2)) // descender is POSITIVE 200
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent.
    /// </summary>
    /// <remarks>
    ///     Regression test: <see cref="TextMetrics.Descent"/> is documented as the absolute value
    ///     of the font descender. A bare negation of a POSITIVE raw <c>hhea.descender</c> (as
    ///     produced by a font that doesn't follow the usual negative-descender convention) would
    ///     incorrectly yield a negative Descent; taking the absolute value keeps it positive
    ///     regardless of the font's sign convention. Distinct from
    ///     <see cref="TextRenderer_MeasureText_ReturnsAscentAndDescentFromFontMetrics"/>, which
    ///     covers the normal negative-descender case.
    /// </remarks>
    [Fact]
    public void TextRenderer_MeasureText_PositiveRawDescender_ReturnsPositiveAbsoluteDescent()
    {
        var font = NewFontWithPositiveDescender();
        var metrics = TextRenderer.MeasureText("A", font, 100f);

        // Raw descender is +200 units * (100/1000) = 20; the absolute value is still +20, not
        // -20 (which a bare `-font.Descender * scale` negation would have produced).
        Assert.Equal(20f, metrics.Descent, 3);
        Assert.True(metrics.Descent >= 0f, "Descent must be non-negative regardless of the font's descender sign convention.");
    }

    /// <summary>TextRenderer_MeasureText_NonFiniteSize_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NonFiniteSize_ThrowsArgumentOutOfRangeException()
    {
        var font = NewFont();
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRenderer.MeasureText("A", font, -1f));
    }

    /// <summary>TextRenderer_MeasureText_NullText_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NullText_ThrowsArgumentNullException()
    {
        var font = NewFont();
        Assert.Throws<ArgumentNullException>(() => TextRenderer.MeasureText(null!, font, 16f));
    }

    /// <summary>TextRenderer_MeasureText_NullFont_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_MeasureText_NullFont_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextRenderer.MeasureText("A", null!, 16f));
    }

    /// <summary>TextRenderer_DrawText_NullText_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_DrawText_NullText_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawText(null!, 0, 0, TextAlign.Left, NewFont(), 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_NullFont_ThrowsArgumentNullException.</summary>
    [Fact]
    public void TextRenderer_DrawText_NullFont_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawText("A", 0, 0, TextAlign.Left, null!, 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_InvalidAlign_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void TextRenderer_DrawText_InvalidAlign_ThrowsArgumentOutOfRangeException()
    {
        var canvas = new RenderCanvas(new Surface(64, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.DrawText("A", 0, 0, (TextAlign)99, NewFont(), 16f, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing.</summary>
    [Fact]
    public void TextRenderer_DrawText_LeftAlign_RendersGlyphsWithoutThrowing()
    {
        var canvas = new RenderCanvas(new Surface(128, 64));
        canvas.DrawText("A", 10, 40, TextAlign.Left, NewFont(), 24f, new Rgba32(255, 0, 0, 255));

        // Verify some pixels were painted.
        var count = 0;
        for (var y = 0; y < canvas.Surface.Height; y++)
        {
            foreach (var px in canvas.Surface.GetRowSpan(y))
            {
                if (px.A > 0)
                {
                    count++;
                }
            }
        }

        Assert.True(count > 0, "DrawText produced no pixels");
    }

    /// <summary>TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX.</summary>
    [Fact]
    public void TextRenderer_DrawText_CenterAlign_CentersRunAroundGivenX()
    {
        // With Center alignment and text width W, the run left edge lands at x - W/2. Rendering
        // then produces pixels centered around x. We assert consistency by comparing to Left
        // alignment at x - W/2.
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 24f);
        var color = new Rgba32(0, 128, 0, 255);

        var s1 = new Surface(128, 64);
        new RenderCanvas(s1).DrawText("A", 64, 40, TextAlign.Center, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 64 - metrics.Width / 2f, 40, TextAlign.Left, font, 24f, color);

        // Byte-identical.
        for (var y = 0; y < s1.Height; y++)
        {
            var r1 = s1.GetRowSpanBytes(y);
            var r2 = s2.GetRowSpanBytes(y);
            Assert.True(r1.SequenceEqual(r2), $"Row {y} mismatch");
        }
    }

    /// <summary>TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX.</summary>
    [Fact]
    public void TextRenderer_DrawText_RightAlign_PlacesLastGlyphEndAtGivenX()
    {
        var font = NewFont();
        var metrics = TextRenderer.MeasureText("A", font, 24f);
        var color = new Rgba32(0, 128, 0, 255);

        var s1 = new Surface(128, 64);
        new RenderCanvas(s1).DrawText("A", 100, 40, TextAlign.Right, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 100 - metrics.Width, 40, TextAlign.Left, font, 24f, color);

        for (var y = 0; y < s1.Height; y++)
        {
            Assert.True(s1.GetRowSpanBytes(y).SequenceEqual(s2.GetRowSpanBytes(y)));
        }
    }

    /// <summary>TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation.</summary>
    [Fact]
    public void TextRenderer_DrawText_UnderTranslatedCanvas_ShiftsGlyphsByTranslation()
    {
        var font = NewFont();
        var color = new Rgba32(0, 0, 0, 255);

        var s1 = new Surface(128, 64);
        var c1 = new RenderCanvas(s1);
        c1.Translate(10, 0);
        c1.DrawText("A", 5, 40, TextAlign.Left, font, 24f, color);

        var s2 = new Surface(128, 64);
        new RenderCanvas(s2).DrawText("A", 15, 40, TextAlign.Left, font, 24f, color);

        for (var y = 0; y < s1.Height; y++)
        {
            Assert.True(s1.GetRowSpanBytes(y).SequenceEqual(s2.GetRowSpanBytes(y)));
        }
    }

    /// <summary>TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill.</summary>
    /// <remarks>
    ///     Regression test: translation alone cannot catch an incorrect transform-composition
    ///     order or a sign error in the glyph's y-flip-vs-rotation interaction, since rotation and
    ///     y-flip don't commute. Draws text via <see cref="RenderCanvas"/> + <see cref="TextRenderer.DrawText"/>
    ///     inside a Save/Translate/RotateDegrees/Restore block for a 90-degree rotation (re-centered
    ///     onto the surface, since rotating strictly about the origin would carry the glyph
    ///     off-canvas), then independently re-derives the same glyph-space transform (scale +
    ///     y-flip + baseline translation, composed with the same rotation-then-recenter current
    ///     transform) and fills the raw glyph outline directly through the static
    ///     <see cref="PathFiller"/> - matching the "compare against manual pre-transform"
    ///     technique used for the gradient-transform fix elsewhere in this PR cycle. The two
    ///     surfaces must be byte-identical.
    /// </remarks>
    [Fact]
    public void TextRenderer_DrawText_UnderRotatedCanvas_MatchesManuallyPreTransformedGlyphFill()
    {
        var font = NewFont();
        var color = new Rgba32(0, 200, 100, 255);
        const float x = 0f;
        const float y = 0f;
        const float size = 24f;
        const float angleDegrees = 90f;
        const float centerX = 48f;
        const float centerY = 48f;

        // Act: render "A" via the public Canvas + TextRenderer API inside a rotated Save/Restore
        // block. Rotating strictly around the origin would carry the glyph off-canvas (its
        // baseline anchor sits at positive x/y), so a translate-to-center is composed after the
        // rotation: per Canvas' documented row-vector prepend order, calling Translate then
        // RotateDegrees means "rotate first (around the origin), then translate" - i.e. the
        // rotated glyph is re-centered onto the surface.
        var rotatedSurface = new Surface(96, 96);
        var rotatedCanvas = new RenderCanvas(rotatedSurface);
        rotatedCanvas.Save();
        rotatedCanvas.Translate(centerX, centerY);
        rotatedCanvas.RotateDegrees(angleDegrees);
        rotatedCanvas.DrawText("A", x, y, TextAlign.Left, font, size, color);
        rotatedCanvas.Restore();

        // Assert (manual re-derivation): independently reconstruct the same glyph-space
        // transform TextRenderer.DrawText bakes internally (scale + y-flip, baseline
        // translation, then the canvas' current transform composed on the outside) and fill the
        // raw glyph outline directly via the static PathFiller, entirely independently of
        // TextRenderer's own implementation.
        var glyphIndex = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyphIndex);
        var scale = size / font.UnitsPerEm;
        var rotation = Matrix3x2.CreateRotation(angleDegrees * (MathF.PI / 180f));
        var recenter = Matrix3x2.CreateTranslation(centerX, centerY);
        var currentTransform = rotation * recenter; // matches Canvas' Translate-then-RotateDegrees prepend order
        var glyphMatrix = Matrix3x2.CreateScale(scale, -scale) * Matrix3x2.CreateTranslation(x, y) * currentTransform;
        var manualSurface = new Surface(96, 96);
        PathFiller.Fill(manualSurface, outline.Transform(glyphMatrix), color);

        // Assert: both surfaces are painted identically, pixel for pixel, and the render actually
        // produced some non-trivial coverage (guards against a degenerate transform collapsing
        // the glyph to nothing).
        var paintedPixelCount = 0;
        for (var py = 0; py < rotatedSurface.Height; py++)
        {
            var rotatedRow = rotatedSurface.GetRowSpanBytes(py);
            var manualRow = manualSurface.GetRowSpanBytes(py);
            Assert.True(rotatedRow.SequenceEqual(manualRow), $"Row {py} mismatch");

            foreach (var px in rotatedSurface.GetRowSpan(py))
            {
                if (px.A > 0)
                {
                    paintedPixelCount++;
                }
            }
        }

        Assert.True(paintedPixelCount > 0, "DrawText under a rotated canvas produced no pixels");
    }
}
