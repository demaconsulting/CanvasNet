using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

// cspell:ignore vsdx Visio

/// <summary>
///     Tests for per-glyph font fallback in <c>VsdxDocument.TextLayout.cs</c>: a code point the
///     run's font lacks is drawn (and measured) with a fallback font instead of the missing-glyph
///     box. Uses deterministic synthetic fonts and an injected fallback resolver.
/// </summary>
public class VsdxGlyphFallbackTests
{
    // Builds a UnitsPerEm-1000 font mapping each (codepoint, advance) pair to its own triangle glyph.
    private static TrueTypeFont NewFont(params (int CodePoint, int Advance)[] glyphs)
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var triangle = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (500, 0, true), (250, 800, true)]);
        var count = glyphs.Length + 1;
        var advances = new List<int> { 0 };
        advances.AddRange(glyphs.Select(static g => g.Advance));
        var lengths = new List<int> { notdef.Length };
        lengths.AddRange(glyphs.Select(_ => triangle.Length));
        var glyf = new List<byte>(notdef);
        foreach (var _ in glyphs)
        {
            glyf.AddRange(triangle);
        }

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [.. glyphs.Select((g, i) => (g.CodePoint, i + 1))]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(count))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, count))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([.. advances]))
            .AddTable("loca", SyntheticFontBuilder.Loca([.. lengths], longFormat: false))
            .AddTable("glyf", [.. glyf])
            .AddTable("cmap", cmap)
            .Build();
        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    private static readonly VsdxEffectiveParagraphProperties Left = new(VsdxHorizontalAlign.Left, 0, 0, 0, 0, 0, 0);

    private static VsdxTextLayout Layout(string text, TrueTypeFont primary, Func<int, bool, bool, TrueTypeFont?>? fallback) =>
        VsdxDocument.ResolveTextLayout(
            [new VsdxEffectiveTextRun(text, "Synthetic", 1.0, false, false, new Rgba32(0, 0, 0, 255), Left)],
            new VsdxTextBoxTransform(5, 5, 10, 10, 5, 5, 0),
            new VsdxEffectiveTextBoxStyle(VsdxVerticalAlign.Top, 0, 0, 0, 0),
            (_, _, _) => primary,
            fallback);

    /// <summary>A code point missing from the primary font is placed with the fallback font and its advance.</summary>
    [Fact]
    public void GlyphFallback_MissingCodePoint_UsesFallbackFontAndAdvance()
    {
        var primary = NewFont((65, 500));
        var symbol = NewFont((0x2794, 1000));
        var layout = Layout("A\u2794A", primary, (cp, _, _) => cp == 0x2794 ? symbol : null);

        Assert.Equal(3, layout.Glyphs.Count);
        Assert.Same(primary, layout.Glyphs[0].Font);
        Assert.Same(symbol, layout.Glyphs[1].Font);
        Assert.NotEqual(0, layout.Glyphs[1].GlyphIndex);
        Assert.Equal(0.5, layout.Glyphs[1].OriginXInches, 6);
        Assert.Equal(1.5, layout.Glyphs[2].OriginXInches, 6);
    }

    /// <summary>A glyph the primary font has never consults the fallback.</summary>
    [Fact]
    public void GlyphFallback_CoveredCodePoint_DoesNotConsultFallback()
    {
        var primary = NewFont((65, 500));
        var calls = 0;
        var layout = Layout("AA", primary, (_, _, _) => { calls++; return null; });

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(0, calls);
    }

    /// <summary>With no fallback font available the primary's missing glyph is kept.</summary>
    [Fact]
    public void GlyphFallback_NoFontCoversCodePoint_KeepsPrimaryGlyph()
    {
        var primary = NewFont((65, 500));

        var layout = Layout("\u2794", primary, (_, _, _) => null);

        var glyph = Assert.Single(layout.Glyphs);
        Assert.Same(primary, glyph.Font);
        Assert.Equal(0, glyph.GlyphIndex);
    }

    /// <summary>The default catalog-based resolver returns a font that really covers the code point, or null.</summary>
    [Fact]
    public void GlyphFallback_DefaultResolver_ReturnsCoveringFontOrNull()
    {
        var font = VsdxDocument.ResolveGlyphFallbackFont(0x2794, false, false);

        if (font is not null)
        {
            Assert.NotEqual(0, font.GetGlyphIndex(0x2794));
        }

        Assert.Null(VsdxDocument.ResolveGlyphFallbackFont(0x10FFFF, false, false));
    }
}
