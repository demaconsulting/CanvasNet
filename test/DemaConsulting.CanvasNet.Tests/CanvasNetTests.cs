// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests;

/// <summary>
///     System-level integration tests for the CanvasNet system.
/// </summary>
public class CanvasNetTests
{
    /// <summary>
    ///     Proves that the system can construct a Surface and set a pixel through the public
    ///     API, and that the pixel is observable through subsequent reads.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CanvasConstructAndSetPixel_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API
        var surface = new Surface(2, 2);
        var pixel = new Rgba32(12, 34, 56, 78);

        // Act: set a pixel and read it back
        surface[1, 1] = pixel;
        var result = surface[1, 1];

        // Assert: the system produces the expected integrated pixel value
        Assert.Equal(pixel, result);
    }

    /// <summary>
    ///     Proves that the system can crop a Surface to an independent sub-region, and that the
    ///     result and source do not share storage.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CanvasCrop_ReturnsIndependentSubRegion()
    {
        // Arrange: construct a surface and set a distinct pixel in the region to be cropped
        var surface = new Surface(3, 3);
        surface[1, 1] = new Rgba32(1, 2, 3, 4);

        // Act: crop the region containing the pixel, then mutate the source
        var cropped = surface.Crop(1, 1, 2, 2);
        surface[1, 1] = new Rgba32(9, 9, 9, 9);

        // Assert: the cropped result retains the original pixel, independent of the source
        Assert.Equal(new Rgba32(1, 2, 3, 4), cropped[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to BMP and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_BmpSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 255);
        using var stream = new MemoryStream();

        // Act: save the surface to BMP and load it back through the public API
        BmpCodec.Save(surface, stream);
        stream.Position = 0;
        var loaded = BmpCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to PNG and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_PngSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 200);
        using var stream = new MemoryStream();

        // Act: save the surface to PNG and load it back through the public API
        PngCodec.Save(surface, stream);
        stream.Position = 0;
        var loaded = PngCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to TIFF and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_TiffSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 200);
        using var stream = new MemoryStream();

        // Act: save the surface to TIFF and load it back through the public API
        TiffCodec.Save(surface, stream, TiffCompression.Lzw);
        stream.Position = 0;
        var loaded = TiffCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to JPEG and load it back through the public
    ///     API, reproducing pixel values within JPEG's lossy compression tolerance.
    /// </summary>
    /// <remarks>
    ///     Unlike the BMP/PNG/TIFF system-integration tests above, this test does not assert
    ///     exact pixel equality: JPEG is a lossy format, so a per-channel tolerance is used
    ///     instead, matching the tolerance-based approach used throughout
    ///     <c>JpegCodecTests</c>/<c>JpegFixtureTests</c>.
    /// </remarks>
    [Fact]
    public void CanvasNet_SystemIntegration_JpegSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(16, 16);
        surface[1, 0] = new Rgba32(11, 22, 33, 255);
        using var stream = new MemoryStream();

        // Act: save the surface to JPEG and load it back through the public API
        JpegCodec.Save(surface, stream, 90);
        stream.Position = 0;
        var loaded = JpegCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value, within
        // JPEG's lossy compression tolerance
        var expected = surface[1, 0];
        var actual = loaded[1, 0];
        // Note: a single isolated distinct pixel against an otherwise uniform background induces
        // DCT ringing across its entire 8x8 block, producing a larger per-channel delta than the
        // gradient/flat-region round-trips exercised in JpegCodecTests; a generous tolerance of
        // 40 is used here to accommodate that worst case while still detecting a functionally
        // broken round-trip.
        const int tolerance = 40;
        Assert.True(Math.Abs(expected.R - actual.R) <= tolerance, $"R delta {Math.Abs(expected.R - actual.R)} exceeded tolerance");
        Assert.True(Math.Abs(expected.G - actual.G) <= tolerance, $"G delta {Math.Abs(expected.G - actual.G)} exceeded tolerance");
        Assert.True(Math.Abs(expected.B - actual.B) <= tolerance, $"B delta {Math.Abs(expected.B - actual.B)} exceeded tolerance");
    }

    /// <summary>
    ///     Proves that the system can rasterize an SVG document into a Surface through the public
    ///     API, producing the expected integrated pixel result for a shape filled with a solid
    ///     color. Unlike the BMP/PNG/TIFF/JPEG system-integration tests above, there is no "Save"
    ///     half to this round-trip: SvgCodec is decode/rasterize-only (see <c>SvgCodecTests</c>/
    ///     <c>SvgFixtureTests</c> for the full unit test coverage).
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_SvgLoad_ReturnsExpectedPixel()
    {
        // Arrange: a minimal SVG document with a viewBox matching the requested raster exactly,
        // containing one rectangle filled with a distinct, fully opaque color
        const string svg = "<svg viewBox='0 0 10 10'><rect x='0' y='0' width='10' height='10' fill='rgb(11,22,33)'/></svg>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));

        // Act: rasterize the document onto a new Surface through the public API
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the system produces the expected integrated rasterized pixel value
        Assert.Equal(new Rgba32(11, 22, 33, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves that the system can composite a semi-transparent constant color over a Surface
    ///     through the public API, producing the expected Porter-Duff "over" result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CompositeColorOverSurface_ReturnsExpectedPixel()
    {
        // Arrange: construct an opaque green background surface through the public API
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(0, 255, 0, 255);

        // Act: composite a semi-transparent red overlay over the surface in place
        surface.CompositeOver(new Rgba32(255, 0, 0, 128));

        // Assert: the system produces the expected integrated compositing result
        Assert.Equal(new Rgba32(128, 127, 0, 255), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can composite a semi-transparent Surface over another Surface
    ///     through the public API, producing the expected Porter-Duff "over" result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CompositeSurfaceOverSurface_ReturnsExpectedPixel()
    {
        // Arrange: construct an opaque blue background surface, and a semi-transparent yellow
        // foreground surface, both through the public API
        var background = new Surface(2, 2);
        background[0, 0] = new Rgba32(0, 0, 255, 255);
        var foreground = new Surface(2, 2);
        foreground[0, 0] = new Rgba32(255, 255, 0, 128);

        // Act: composite the foreground surface over the background surface in place
        background.CompositeOver(foreground);

        // Assert: expected value independently computed (Porter-Duff "over", normalized [0, 1]
        // math, round-half-away-from-zero, clamped) - not copied from the CompositeOver(Rgba32)
        // test above, since fgA=128/255, bgA=1 gives outA=1 exactly, outR=outG=255*128/255=128,
        // and outB=255*(1-128/255)=127
        Assert.Equal(new Rgba32(128, 128, 127, 255), background[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can convert a Surface's pixel buffer from straight to
    ///     premultiplied alpha through the public API, producing the expected rounded result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_PremultiplyAlpha_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a straight-alpha pixel
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(200, 100, 50, 128);

        // Act: premultiply the surface's alpha in place
        surface.PremultiplyAlpha();

        // Assert: the system produces the expected integrated premultiplied pixel value
        // (round(200*128/255)=100, round(100*128/255)=50, round(50*128/255)=25; alpha unchanged)
        Assert.Equal(new Rgba32(100, 50, 25, 128), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can convert a Surface's pixel buffer from premultiplied back to
    ///     straight alpha through the public API, including the load-bearing clamp when the
    ///     division overshoots 255.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_UnpremultiplyAlpha_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a premultiplied pixel whose
        // red channel overshoots 255 when unpremultiplied, exercising the documented clamp
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(100, 10, 0, 50);

        // Act: unpremultiply the surface's alpha in place
        surface.UnpremultiplyAlpha();

        // Assert: the system produces the expected integrated unpremultiplied pixel value
        // (round(100*255/50)=510, clamped to 255; round(10*255/50)=51; 0 stays 0; alpha unchanged)
        Assert.Equal(new Rgba32(255, 51, 0, 50), surface[0, 0]);
    }

    /// <summary>
    ///     Row widths, in pixels, that exercise Surface's internal row-padding boundary (padding
    ///     is applied in multiples of 16 pixels): widths at, just below, and just above each
    ///     boundary, plus a couple of larger "normal" widths.
    /// </summary>
    public static TheoryData<int> BoundaryWidths =>
    [
        1, 15, 16, 17, 31, 32, 33, 100, 257
    ];

    /// <summary>
    ///     Proves that a PNG save/load round-trip remains byte-exact at Surface's internal
    ///     row-padding boundary widths, confirming Surface's stride/padding storage detail is not
    ///     observable through the PNG codec. This is a system-level test (not a Surface unit
    ///     test) because it exercises the Codecs -> Surface integration boundary rather than
    ///     Surface in isolation: Surface's unit tests must only depend on Surface itself, and
    ///     Codecs depend on Surface (not vice versa), so a codec round-trip belongs here.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void CanvasNet_SystemIntegration_PngCodecRoundTrip_BoundaryWidths_ReturnsExpectedPixels(int width)
    {
        // Arrange: build a surface at a boundary width with distinct, non-trivial pixel values
        var surface = new Surface(width, 3);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 17 + 1), (byte)(y * 23 + 2), (byte)(x + y + 3), (byte)(200 - (x % 200)));
            }
        }

        using var stream = new MemoryStream();

        // Act: save and reload the surface through the PNG codec
        PngCodec.Save(surface, stream, PngColorType.Rgba);
        stream.Position = 0;
        var reloaded = PngCodec.Load(stream);

        // Assert: every pixel must round-trip exactly
        Assert.Equal(surface.Width, reloaded.Width);
        Assert.Equal(surface.Height, reloaded.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], reloaded[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the system can construct a Path via PathBuilder using a MoveTo/
    ///     CubicBezierTo/ArcTo/Close sequence, flatten it to a polyline, and compute its
    ///     axis-aligned bounding Rect - exercising the Geometry namespace's public types end to
    ///     end through the public API.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_BuildFlattenAndBoundPath_ReturnsExpectedBounds()
    {
        // Arrange: build a path with a move, a cubic Bezier, an SVG-style arc, and a close
        var builder = new PathBuilder();
        var path = builder
            .MoveTo(new Vector2(0, 0))
            .CubicBezierTo(new Vector2(0, 50), new Vector2(50, 50), new Vector2(50, 0))
            .ArcTo(new Vector2(25, 25), 0, largeArc: false, sweep: true, new Vector2(0, 0))
            .Close()
            .Build();

        // Act: flatten the cubic segment directly through the public API, and compute the path's
        // conservative bounding rectangle
        var flattened = new List<Vector2>();
        BezierFlattening.FlattenCubic(
            new Vector2(0, 0),
            new Vector2(0, 50),
            new Vector2(50, 50),
            new Vector2(50, 0),
            0.5f,
            flattened);
        var bounds = path.GetBounds();

        // Assert: the system produces one closed subpath, a non-empty flattened polyline ending
        // at the curve's true end point, and a bounding rectangle enclosing every command
        var subpath = Assert.Single(path.Subpaths);
        Assert.True(subpath.IsClosed);
        Assert.NotEmpty(flattened);
        Assert.Equal(new Vector2(50, 0), flattened[^1]);
        Assert.False(bounds.IsEmpty);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    /// <summary>
    ///     Proves that the system silently no-ops, through PathFiller's public API, when filling
    ///     an empty Path or a Path whose bounds do not intersect the target Surface - in both
    ///     cases leaving every pixel of the Surface at its initial, fully transparent state.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_FillEmptyOrOutOfBoundsPath_NoOpLeavesSurfaceUnchanged()
    {
        // Arrange: a surface, an empty path built through the public PathBuilder API, and a
        // closed path entirely outside the surface's bounds
        var surface = new Surface(4, 4);
        var emptyPath = new PathBuilder().Build();
        var outOfBoundsPath = new PathBuilder()
            .MoveTo(new Vector2(100, 100))
            .LineTo(new Vector2(120, 100))
            .LineTo(new Vector2(110, 120))
            .Close()
            .Build();
        var color = new Rgba32(255, 0, 0, 255);

        // Act: fill both paths through the public PathFiller API
        PathFiller.Fill(surface, emptyPath, color);
        PathFiller.Fill(surface, outOfBoundsPath, color);

        // Assert: every pixel remains at the surface's initial, fully transparent state
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(new Rgba32(0, 0, 0, 0), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the system can build a closed triangular Path via PathBuilder and fill it
    ///     onto a Surface with a solid color through PathFiller's public API, exercising the
    ///     Geometry -> Drawing -> Canvas integration end to end: interior pixels are fully
    ///     opaque, exterior pixels are untouched, and a slanted-edge pixel is antialiased to a
    ///     fractional coverage strictly between fully transparent and fully opaque.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_BuildAndFillTrianglePath_ReturnsExpectedPixels()
    {
        // Arrange: an 8x8 surface, and a right triangle with one vertical edge (x=1), one
        // horizontal edge (y=6), and one slanted hypotenuse - built entirely through the public
        // PathBuilder API
        var surface = new Surface(8, 8);
        var path = new PathBuilder()
            .MoveTo(new Vector2(1, 1))
            .LineTo(new Vector2(1, 6))
            .LineTo(new Vector2(6, 6))
            .Close()
            .Build();
        var color = new Rgba32(255, 0, 0, 255);

        // Act: fill the path through the public PathFiller API
        PathFiller.Fill(surface, path, color);

        // Assert: a pixel deep in the triangle's interior is fully opaque red
        Assert.Equal(color, surface[2, 5]);

        // Assert: a pixel well outside the triangle's bounding box is untouched (still fully
        // transparent, the surface's initial state)
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[7, 0]);

        // Assert: a pixel straddling the slanted hypotenuse is antialiased to a fractional
        // coverage - neither fully transparent nor fully opaque
        var edgePixel = surface[4, 4];
        Assert.True(edgePixel.A > 0 && edgePixel.A < 255, $"Expected a fractional alpha, got {edgePixel.A}");
    }

    /// <summary>
    ///     Builds a minimal, complete, well-formed synthetic TrueType font with a single simple
    ///     glyph (a diamond, mixing on-curve and off-curve quadratic points) mapped from codepoint
    ///     'A', for the Fonts -> Geometry -> Drawing -> Canvas system-integration tests below.
    /// </summary>
    private static byte[] BuildSyntheticFontWithDiamondGlyph()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(400, 0, true), (800, 400, false), (400, 800, true), (0, 400, false)]
        ]);

        return new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 1000]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .AddTable("cmap", SyntheticFontBuilder.CmapFormat4(3, 1, [('A', 1)]))
            .Build();
    }

    /// <summary>
    ///     Proves that the system can load a synthetic TrueType font through
    ///     <see cref="TrueTypeFont"/>'s public API, map a codepoint to a glyph index, extract that
    ///     glyph's outline as a <see cref="DemaConsulting.CanvasNet.Geometry.Path"/> (raw font-design-unit space), and fill it onto a
    ///     <see cref="Surface"/> via <see cref="PathFiller"/> - exercising the
    ///     Fonts -> Geometry -> Drawing -> Canvas integration end to end, and confirming
    ///     non-trivial rendered pixel coverage (an interior pixel is fully opaque, and at least
    ///     one edge pixel is antialiased to a fractional coverage).
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_LoadFontAndFillGlyphOutline_ReturnsExpectedPixels()
    {
        // Arrange: load a synthetic font, look up 'A', and extract its glyph outline in raw font
        // design units (0..1000, matching this font's UnitsPerEm)
        var fontData = BuildSyntheticFontWithDiamondGlyph();
        var font = TrueTypeFont.Load(new MemoryStream(fontData));
        var glyphIndex = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyphIndex);

        // Act: scale the raw font-unit outline down to fit a small surface, then fill it through
        // the public PathFiller API
        var surface = new Surface(10, 10);
        const float scale = 10f / 1000f;
        var scaledBuilder = new PathBuilder();
        var subpath = Assert.Single(outline.Subpaths);
        scaledBuilder.MoveTo(subpath.Start * scale);
        foreach (var command in subpath.Commands)
        {
            switch (command.Type)
            {
                case PathCommandType.QuadraticBezierTo:
                    scaledBuilder.QuadraticBezierTo(command.Control1 * scale, command.EndPoint * scale);
                    break;
                case PathCommandType.LineTo:
                    scaledBuilder.LineTo(command.EndPoint * scale);
                    break;
                case PathCommandType.Close:
                    scaledBuilder.Close();
                    break;
            }
        }

        var scaledPath = scaledBuilder.Build();
        var color = new Rgba32(0, 128, 255, 255);
        PathFiller.Fill(surface, scaledPath, color);

        // Assert: the glyph index resolved correctly, the outline decoded to a single non-trivial
        // subpath, the diamond's center is fully covered, and at least one pixel near its edge is
        // antialiased to a fractional (neither fully transparent nor fully opaque) coverage
        Assert.Equal(1, glyphIndex);
        Assert.Equal(3, subpath.Commands.Count); // 2 QuadraticBezierTo (implied on-curve joins) + Close
        Assert.Equal(color, surface[4, 4]);

        var coveredPixelCount = 0;
        var fractionalCoveragePixelCount = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var alpha = surface[x, y].A;
                if (alpha > 0)
                {
                    coveredPixelCount++;
                }

                if (alpha > 0 && alpha < 255)
                {
                    fractionalCoveragePixelCount++;
                }
            }
        }

        Assert.True(coveredPixelCount > 1, "Expected non-trivial rendered pixel coverage.");
        Assert.True(fractionalCoveragePixelCount > 0, "Expected at least one antialiased edge pixel.");
    }

    /// <summary>
    ///     Proves that the system reports zero advance-width/kerning-driven pen movement
    ///     differences between two glyphs of a synthetic font loaded through
    ///     <see cref="TrueTypeFont"/>'s public API consistently with its declared metrics,
    ///     confirming the Fonts namespace's metrics API integrates correctly end to end (a
    ///     second, independent system-integration test alongside the glyph-fill test above, per
    ///     this namespace's companion-artifact plan).
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_LoadFontAndQueryMetrics_ReturnsExpectedValues()
    {
        // Arrange/Act: load a synthetic font and query its top-level metrics and the glyph's
        // advance width through the public API
        var fontData = BuildSyntheticFontWithDiamondGlyph();
        var font = TrueTypeFont.Load(new MemoryStream(fontData));
        var glyphIndex = font.GetGlyphIndex('A');
        var advanceWidth = font.GetAdvanceWidth(glyphIndex);

        // Assert: the system's integrated metrics match the synthetic font's declared values
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(800, font.Ascender);
        Assert.Equal(-200, font.Descender);
        Assert.Equal(2, font.GlyphCount);
        Assert.Equal(1000, advanceWidth);
        Assert.Equal(0, font.GetKerning(0, glyphIndex));
    }

    /// <summary>
    ///     Proves that the system can parse a web-style hex color literal through
    ///     <see cref="Rgba32.Parse"/> and use the resulting value end to end to set and read
    ///     back a pixel on a <see cref="Surface"/> constructed through its public API,
    ///     confirming the <c>Canvas</c> subsystem's hex-color-parsing boundary integrates
    ///     correctly with its pixel-buffer boundary.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_ParseHexColorAndSetSurfacePixel_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API
        var surface = new Surface(4, 4);

        // Act: parse a hex color literal and set/read a pixel with it
        var color = Rgba32.Parse("#80112233");
        surface[2, 2] = color;
        var result = surface[2, 2];

        // Assert: the system's integrated parse-then-store-then-read pipeline round-trips
        // exactly
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x80), result);
    }

    /// <summary>
    ///     Proves that the system can build a closed polyline path, apply
    ///     <see cref="CornerRoundEffect.Apply"/> to round its corners, and fill the result
    ///     through <see cref="PathFiller"/>'s <c>Fill</c> entry point onto a
    ///     <see cref="Surface"/>, confirming the <c>Geometry</c> subsystem's corner-round
    ///     pre-processing boundary integrates correctly with the <c>Drawing</c> and
    ///     <c>Canvas</c> subsystems: a pixel at the shape's original sharp corner must be
    ///     clipped away (fully transparent) by the rounding, while a pixel well inside the
    ///     shape remains fully painted.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_RoundPathCornersAndFillOntoSurface_ClipsSharpCorner()
    {
        // Arrange: a 40x40 square positioned so its top-left corner sits well inside the surface
        var square = new PathBuilder()
            .MoveTo(new Vector2(5, 5))
            .LineTo(new Vector2(45, 5))
            .LineTo(new Vector2(45, 45))
            .LineTo(new Vector2(5, 45))
            .Close()
            .Build();
        var surface = new Surface(50, 50);

        // Act: round every corner with a generous radius, then fill the rounded path
        var rounded = CornerRoundEffect.Apply(square, 12f);
        PathFiller.Fill(surface, rounded, new Rgba32(255, 255, 255, 255));

        // Assert: the original sharp top-left corner pixel is clipped away by the rounding...
        Assert.Equal((byte)0, surface[6, 6].A);

        // ...while a pixel well inside the shape (away from every rounded corner) remains fully
        // painted
        Assert.Equal((byte)255, surface[25, 25].A);
    }

    /// <summary>
    ///     Proves that the system's transform-aware <c>Rendering.Canvas</c> wrapper, composed
    ///     with the <c>Rendering.Shapes</c> convenience helpers, honors an applied translation
    ///     end to end: a rounded rectangle filled at the origin under a translated Canvas paints
    ///     the same pixels as the same rounded rectangle filled directly at the translated
    ///     coordinates on an untransformed Canvas, confirming the <c>Rendering</c> subsystem's
    ///     shape-helper boundary integrates correctly with its transform-stack boundary.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_FillRoundRectUnderTranslatedCanvas_MatchesDirectPlacement()
    {
        // Arrange: two identical surfaces, one drawn through a translated Canvas, one drawn
        // directly at the equivalent absolute coordinates
        var translatedSurface = new Surface(40, 40);
        var directSurface = new Surface(40, 40);
        var color = new Rgba32(10, 20, 30, 255);

        // Act
        var translatedCanvas = new DemaConsulting.CanvasNet.Rendering.Canvas(translatedSurface);
        translatedCanvas.Translate(10, 10);
        translatedCanvas.FillRoundRect(0, 0, 15, 15, 4, color);

        var directCanvas = new DemaConsulting.CanvasNet.Rendering.Canvas(directSurface);
        directCanvas.FillRoundRect(10, 10, 15, 15, 4, color);

        // Assert: both surfaces are painted identically, pixel for pixel
        for (var y = 0; y < translatedSurface.Height; y++)
        {
            for (var x = 0; x < translatedSurface.Width; x++)
            {
                Assert.Equal(directSurface[x, y], translatedSurface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the system can load a synthetic TrueType font through
    ///     <see cref="TrueTypeFont"/>'s public API and, through the fully public
    ///     <c>Rendering</c> surface, both measure and draw a text run with it: measures "A" with
    ///     <see cref="TextRenderer.MeasureText"/> and confirms the reported width/ascent/descent
    ///     agree with the font's declared advance width and metrics, then draws the same text
    ///     onto a <see cref="DemaConsulting.CanvasNet.Rendering.Canvas"/> with
    ///     <see cref="TextRenderer.DrawText"/> and confirms non-trivial rendered pixel coverage,
    ///     confirming the <c>Rendering</c> subsystem's text-measurement and text-drawing
    ///     boundaries integrate correctly end to end with the <c>Fonts</c>, <c>Geometry</c>,
    ///     <c>Drawing</c>, and <c>Canvas</c> subsystems.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_DrawAndMeasureTextViaCanvas_RendersAndMeasuresExpectedResult()
    {
        // Arrange: load a synthetic font with a single diamond-shaped glyph mapped from 'A'
        var fontData = BuildSyntheticFontWithDiamondGlyph();
        var font = TrueTypeFont.Load(new MemoryStream(fontData));
        const float size = 32f;
        var color = new Rgba32(0, 128, 255, 255);

        // Act: measure the text through the public MeasureText API
        var metrics = TextRenderer.MeasureText("A", font, size);

        // Assert: measured metrics match the font's declared advance width (1000 units) and
        // ascender/descender (800/-200 units), scaled by size / UnitsPerEm (32/1000)
        Assert.Equal(32f, metrics.Width, 3);
        Assert.Equal(25.6f, metrics.Ascent, 3);
        Assert.Equal(6.4f, metrics.Descent, 3);

        // Act: draw the same text onto a public Canvas via the public DrawText API
        var canvas = new DemaConsulting.CanvasNet.Rendering.Canvas(new Surface(48, 48));
        canvas.DrawText("A", 8, 40, TextAlign.Left, font, size, color);

        // Assert: the glyph produced non-trivial rendered pixel coverage on the public Surface
        var coveredPixelCount = 0;
        for (var y = 0; y < canvas.Surface.Height; y++)
        {
            for (var x = 0; x < canvas.Surface.Width; x++)
            {
                if (canvas.Surface[x, y].A > 0)
                {
                    coveredPixelCount++;
                }
            }
        }

        Assert.True(coveredPixelCount > 0, "DrawText produced no rendered pixels");
    }
}
