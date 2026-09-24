using DemaConsulting.CanvasNet.Canvas;
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

        return TrueTypeFont.Load(new MemoryStream(data));
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
}
