using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

// cspell:ignore vsdx Visio unitsperem unmirrored

/// <summary>
///     Unit-level tests for <c>VsdxDocument.TextRender.cs</c>'s glyph-painting primitive
///     (<see cref="VsdxDocument.PaintTextLayout"/>): proves ink paints at the expected,
///     transform-composed pixel location with the resolved run color, and specifically proves
///     the documented no-Y-flip deviation from <c>PptxDocument.PaintTextLayout</c> (Vsdx's
///     shape-local space is already y-up, matching TrueType's own y-up glyph-outline convention,
///     so no sign flip on the glyph scale's Y component is applied - see
///     <see cref="VsdxDocument.PaintTextLayout"/>'s own remarks).
/// </summary>
public class VsdxTextRenderTests
{
    /// <summary>Synthetic font: UnitsPerEm 1000, a single 'A' glyph (index 1) whose outline is a filled square spanning font-unit [100,900] x [100,900].</summary>
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

    /// <summary>
    ///     Synthetic font: UnitsPerEm 1000, a single 'A' glyph whose outline is confined to
    ///     font-unit y in <c>[600,900]</c> (the upper, ascender-ward half of the em-square) and x
    ///     in <c>[100,900]</c> - deliberately asymmetric about the baseline, so a Y-flip
    ///     regression relocates the painted ink to a disjoint pixel band rather than merely
    ///     repositioning it within a symmetric shape.
    /// </summary>
    private static TrueTypeFont NewUpperHalfInkFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(100, 600, true), (900, 600, true), (900, 900, true), (100, 900, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(900, 0, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 1000]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>Proves a glyph paints its resolved color at the expected, identity-transformed pixel location and leaves pixels outside its outline untouched.</summary>
    [Fact]
    public void PaintTextLayout_IdentityTransform_PaintsGlyphAtExpectedLocationWithResolvedColor()
    {
        // Arrange: Size 100 -> scale 100/1000 = 0.1. Font-unit square [100,900]x[100,900] scales
        // to [10,90]x[10,90]; translating by Origin (5,5) places the final square at
        // x in [15,95], y in [15,95] (no Y sign flip - see this class's own remarks).
        var font = NewFilledSquareFont();
        var glyphIndex = font.GetGlyphIndex('A');
        var color = new Rgba32(200, 30, 40, 255);
        var glyph = new VsdxGlyphPlacement(font, glyphIndex, OriginXInches: 5d, OriginYInches: 5d, SizeInches: 100d, color);
        var layout = new VsdxTextLayout([glyph]);

        using var surface = new Surface(100, 100);

        // Act
        VsdxDocument.PaintTextLayout(surface, layout, Matrix3x2.Identity);

        // Assert
        Assert.Equal(color, surface[50, 50]);
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
        var glyph = new VsdxGlyphPlacement(font, glyphIndex, OriginXInches: 0d, OriginYInches: 0d, SizeInches: 100d, color);
        var layout = new VsdxTextLayout([glyph]);

        using var surface = new Surface(100, 100);
        var shapeToSurfaceTransform = Matrix3x2.CreateTranslation(20f, 20f);

        // Act
        VsdxDocument.PaintTextLayout(surface, layout, shapeToSurfaceTransform);

        // Assert: without the translation, the square would occupy [10,90]x[10,90]; with it,
        // [30,110)x[30,110) clipped to the 100x100 surface - (50,50) is inside the shifted
        // square, (5,5) (inside the unshifted square) must no longer be painted.
        Assert.Equal(color, surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves no Y-flip is applied to the glyph scale: an outline confined to the upper
    ///     (ascender-ward) half of the em-square paints ink at the corresponding, un-mirrored
    ///     pixel band rather than the mirrored band a Pptx-style flip would produce.
    /// </summary>
    [Fact]
    public void PaintTextLayout_NoYFlipApplied_UpperHalfInkPaintsAtUnmirroredLocation()
    {
        // Arrange: Size 100 -> scale 0.1. Font-unit ink y in [600,900] scales to [60,90];
        // translating by Origin (10,5) places the final ink band at y in [65,95] (no flip).
        // A Y-flip regression would instead place it at y in [-85,-55] (entirely off-canvas,
        // nothing painted at all within the 100x100 surface).
        var font = NewUpperHalfInkFont();
        var glyphIndex = font.GetGlyphIndex('A');
        var color = new Rgba32(40, 60, 80, 255);
        var glyph = new VsdxGlyphPlacement(font, glyphIndex, OriginXInches: 10d, OriginYInches: 5d, SizeInches: 100d, color);
        var layout = new VsdxTextLayout([glyph]);

        using var surface = new Surface(100, 100);

        // Act
        VsdxDocument.PaintTextLayout(surface, layout, Matrix3x2.Identity);

        // Assert: inside the expected unmirrored ink band.
        Assert.Equal(color, surface[50, 80]);
        // Assert: below the ink band (would be inside a mirrored band instead - confirms no flip occurred).
        Assert.Equal(default, surface[50, 20]);
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

        var glyph = new VsdxGlyphPlacement(font, 0, OriginXInches: 5d, OriginYInches: 5d, SizeInches: 100d, new Rgba32(1, 2, 3, 255));
        var layout = new VsdxTextLayout([glyph]);
        using var surface = new Surface(100, 100);

        // Act
        VsdxDocument.PaintTextLayout(surface, layout, Matrix3x2.Identity);

        // Assert
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }
}
