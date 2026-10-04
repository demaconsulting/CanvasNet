using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx unitsperem

/// <summary>
///     Unit-level tests for the Phase 1d glyph-painting primitive (<c>PptxDocument.TextRender.cs</c>'s
///     <see cref="PptxDocument.PaintTextLayout"/>): proves ink is painted at the expected,
///     transform-composed pixel location with the resolved run color, and that the shape's own
///     <see cref="PptxShapeFrame.Transform"/> composes correctly with the glyph's own local
///     origin/scale. Complements <see cref="PptxTextTests"/> (parsing/inheritance) and
///     <see cref="PptxTextLayoutTests"/> (word-wrap/alignment/anchor/autofit).
/// </summary>
public class PptxTextRenderTests
{
    // Synthetic font: UnitsPerEm 1000, a single 'A' glyph (index 1) whose outline is a filled
    // square spanning font-unit [100,900] x [100,900] - comfortably inside the em-square so a
    // small scale still produces an unambiguous interior/exterior pixel split.
    private static TrueTypeFont NewFilledSquareFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(100, 100, true), (900, 100, true), (900, 900, true), (100, 900, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 1000]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>Proves a glyph paints its resolved color at its expected, identity-transformed pixel location, and leaves pixels outside its outline untouched.</summary>
    [Fact]
    public void PaintTextLayout_IdentityTransform_PaintsGlyphAtExpectedLocationWithResolvedColor()
    {
        // Arrange: SizeEmu 100 -> scale 100/1000 = 0.1. Font-unit square [100,900]x[100,900]
        // scales to [10,90]x[10,90], then translates by Origin (5,5) -> surface [15,95]x[15,95].
        var font = NewFilledSquareFont();
        var glyphIndex = font.GetGlyphIndex('A');
        var color = new Rgba32(200, 30, 40, 255);
        var glyph = new PptxGlyphPlacement(font, glyphIndex, OriginXEmu: 5f, OriginYEmu: 5f, SizeEmu: 100f, color);
        var layout = new PptxTextLayout([glyph], AppliedFontScale: 1f);

        using var surface = new Surface(100, 100);

        // Act
        PptxDocument.PaintTextLayout(surface, layout, Matrix3x2.Identity);

        // Assert: comfortably inside the painted square.
        Assert.Equal(color, surface[50, 50]);
        // Assert: comfortably outside the painted square (untouched, still transparent black).
        Assert.Equal(default, surface[2, 2]);
        Assert.Equal(default, surface[98, 98]);
    }

    /// <summary>Proves the shape-to-surface transform composes with the glyph's own local scale/origin (a non-identity translation shifts the painted location accordingly).</summary>
    [Fact]
    public void PaintTextLayout_TranslatedShapeTransform_ShiftsPaintedLocation()
    {
        var font = NewFilledSquareFont();
        var glyphIndex = font.GetGlyphIndex('A');
        var color = new Rgba32(10, 220, 10, 255);
        var glyph = new PptxGlyphPlacement(font, glyphIndex, OriginXEmu: 0f, OriginYEmu: 0f, SizeEmu: 100f, color);
        var layout = new PptxTextLayout([glyph], AppliedFontScale: 1f);

        using var surface = new Surface(100, 100);
        var shapeToSurfaceTransform = Matrix3x2.CreateTranslation(20f, 20f);

        // Act
        PptxDocument.PaintTextLayout(surface, layout, shapeToSurfaceTransform);

        // Assert: without the translation, the square would occupy [10,90]x[10,90]; with it,
        // [30,110)x[30,110) clipped to the 100x100 surface - (50,50) is inside the shifted square,
        // (5,5) (inside the unshifted square) must no longer be painted.
        Assert.Equal(color, surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves a glyph with an empty outline (no subpaths - for example a whitespace glyph, defensively) is skipped rather than painting anything.</summary>
    [Fact]
    public void PaintTextLayout_EmptyOutlineGlyph_PaintsNothing()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(32, 0)]);
        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length], longFormat: false))
            .AddTable("glyf", [.. notdef])
            .AddTable("cmap", cmap)
            .Build();
        using var stream = new MemoryStream(data);
        var font = TrueTypeFont.Load(stream);

        var glyph = new PptxGlyphPlacement(font, 0, OriginXEmu: 5f, OriginYEmu: 5f, SizeEmu: 100f, new Rgba32(1, 2, 3, 255));
        var layout = new PptxTextLayout([glyph], AppliedFontScale: 1f);
        using var surface = new Surface(100, 100);

        PptxDocument.PaintTextLayout(surface, layout, Matrix3x2.Identity);

        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves <see cref="PptxDocument.ResolveTextFont"/> falls back to the bundled font when the family hint does not match any resolvable system font criterion and no installed font happens to be returned as a generic-family fallback (bundled fallback is always reachable regardless).</summary>
    [Fact]
    public void ResolveTextFont_AnyFamilyHint_ResolvesNonNullFont()
    {
        var font = PptxDocument.ResolveTextFont("SomeFamilyNameHint", bold: false, italic: false);

        Assert.NotNull(font);
        Assert.True(font.UnitsPerEm > 0);
    }
}
