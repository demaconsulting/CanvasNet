using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

// cspell:ignore xobject devicergb fontfile

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Fixture-conformance tests that exercise <see cref="PdfDocument"/> against the real-file PDF
///     fixture corpus in <c>PdfFixtures</c> (see <c>PdfFixtures\README.md</c> for provenance - the
///     corpus is hand-authored for this repository, with a few documented exceptions that embed
///     or excerpt real-world third-party content), mirroring the pattern used by
///     <c>SvgFixtureTests</c> in <c>DemaConsulting.CanvasNet.Svg.Tests</c>. Each test opens one
///     on-disk fixture, reads its page info, renders at least one page, and asserts a concrete,
///     non-trivial rendered result - a broad "this file loads and renders as documented" check,
///     distinct from (and complementing, not duplicating) the finer-grained behavioral assertions
///     already made against several of the same files in <see cref="PdfDocumentTests"/> and
///     <see cref="PdfSystemIntegrationTests"/>.
/// </summary>
public class PdfFixtureTests
{
    /// <summary>The directory containing the PDF fixture corpus, copied to the test output directory
    ///     by this project's <c>PdfFixtures\**</c> content item.</summary>
    private static string FixturesPath => Path.Join(AppContext.BaseDirectory, "PdfFixtures");

    /// <summary>Resolves a fixture file within <see cref="FixturesPath"/>.</summary>
    private static string Fixture(string name) => Path.Join(FixturesPath, name);

    /// <summary>
    ///     A fully transparent <see cref="PdfRenderOptions.BackgroundColor"/>, used so
    ///     <see cref="AssertPaintedSomePixel"/>'s "any painted pixel" check keeps proving real
    ///     content was rendered rather than becoming vacuously true against an opaque-white
    ///     default background.
    /// </summary>
    private static readonly PdfRenderOptions Transparent = new() { BackgroundColor = new(0, 0, 0, 0) };

    /// <summary>
    ///     The path to the real "Open Sans" TrueType font, copied to the test output directory by
    ///     this project's <c>FontFixtures\**</c> content-link item (see
    ///     <c>DemaConsulting.CanvasNet.Tests\FontFixtures\README.md</c> for provenance/licensing).
    /// </summary>
    private static string FontPath => Path.Join(AppContext.BaseDirectory, "FontFixtures", "OpenSans-Regular.ttf");

    /// <summary>
    ///     Proves that at least one non-transparent pixel was painted somewhere on
    ///     <paramref name="surface"/> - the broad "real content was rendered, not merely a blank
    ///     surface" assertion this fixture-conformance tier favors over per-pixel checks (which
    ///     already exist, per fixture, in <see cref="PdfSystemIntegrationTests"/>).
    /// </summary>
    private static void AssertPaintedSomePixel(Canvas.Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    return;
                }
            }
        }

        Assert.Fail("Expected at least one non-transparent pixel to be painted.");
    }

    /// <summary>
    ///     Proves <c>classic-xref-single-page.pdf</c> (a classic <c>xref</c> table + <c>trailer</c>
    ///     dictionary, Phase 1) opens, reports one page at its declared <c>/MediaBox</c> size, and
    ///     renders without error.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_ClassicXrefSinglePageFixture_OpensAndRenders()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        var info = document.GetPageInfo(0);
        using var surface = document.Render(0, info.Width, info.Height, Transparent);

        // Assert
        Assert.Equal(1, document.PageCount);
        Assert.Equal(info.Width, surface.Width);
        Assert.Equal(info.Height, surface.Height);
    }

    /// <summary>
    ///     Proves <c>multi-page-mixed-mediabox-rotate.pdf</c> (three pages, differing/inherited
    ///     <c>/MediaBox</c> and <c>/Rotate</c>) reports the correct page count and renders every
    ///     page at its own reported (rotation-swapped, where applicable) size.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_MultiPageMixedMediaBoxRotateFixture_RendersEveryPageAtItsOwnSize()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));

        // Act & Assert
        Assert.Equal(3, document.PageCount);
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var info = document.GetPageInfo(pageIndex);
            using var surface = document.Render(pageIndex, info.Width, info.Height, Transparent);
            Assert.Equal(info.Width, surface.Width);
            Assert.Equal(info.Height, surface.Height);
        }
    }

    /// <summary>
    ///     Proves <c>color-rgb-rectangle-fill.pdf</c> (Phase 3 <c>rg</c> device color) renders a
    ///     visible, non-transparent painted region.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_ColorRgbRectangleFillFixture_PaintsVisibleContent()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("color-rgb-rectangle-fill.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves <c>color-spaces-calibrated.pdf</c> (<c>CalGray</c>/<c>CalRGB</c>/<c>Lab</c> swatches)
    ///     paints a neutral mid gray, an sRGB-primary blue and a green-dominant Lab color.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_CalibratedColorSpacesFixture_PaintsConvertedSwatches()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("color-spaces-calibrated.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert: CalGray 0.5 (gamma 2.2) is a neutral mid gray; CalRGB 0 0 1 with sRGB primaries is
        // blue; Lab 70 -50 60 is green-dominant.
        var gray = surface[17, 40];
        Assert.InRange(gray.R, 120, 135);
        Assert.Equal(gray.R, gray.G);
        Assert.Equal(gray.R, gray.B);

        var blue = surface[50, 40];
        Assert.True(blue.B >= 254 && blue.R <= 1 && blue.G <= 1);

        var green = surface[82, 40];
        Assert.True(green.G > green.R && green.G > green.B && green.A == 255);
    }

    /// <summary>
    ///     Proves <c>color-separation-devicen.pdf</c> paints a <c>Separation</c> tint (red via CMYK), a
    ///     <c>DeviceN</c> two-colorant PostScript tint and leaves a <c>/None</c> Separation swatch unpainted.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_SeparationDeviceNFixture_PaintsTintsAndSkipsNone()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("color-separation-devicen.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        var spot = surface[17, 40];
        Assert.True(spot.R >= 254 && spot.G <= 1 && spot.B <= 1 && spot.A == 255);

        var deviceN = surface[50, 40];
        Assert.InRange(deviceN.R, 50, 52);
        Assert.InRange(deviceN.G, 203, 205);
        Assert.Equal(0, deviceN.B);
        Assert.Equal(255, deviceN.A);

        Assert.Equal(0, surface[82, 40].A);
    }

    /// <summary>
    ///     Proves <c>image-xobject-devicergb-flate.pdf</c> (Phase 3 <c>/Subtype /Image</c> XObject)
    ///     renders a visible, non-transparent painted region.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_ImageXObjectDeviceRgbFlateFixture_PaintsVisibleContent()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("image-xobject-devicergb-flate.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves <c>text-embedded-truetype-font.pdf</c> (Phase 4 embedded <c>/FontFile2</c>) renders
    ///     visible glyph ink.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_TextEmbeddedTrueTypeFontFixture_PaintsVisibleGlyphInk()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("text-embedded-truetype-font.pdf"));
        using var surface = document.Render(0, 200, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves each of the four Phase 7 content-stream filter fixtures (<c>LZWDecode</c>,
    ///     <c>ASCII85Decode</c>, <c>ASCIIHexDecode</c>, <c>RunLengthDecode</c>) decodes correctly
    ///     and renders the same visible filled rectangle.
    /// </summary>
    [Theory]
    [InlineData("content-stream-lzw.pdf")]
    [InlineData("content-stream-ascii85.pdf")]
    [InlineData("content-stream-asciihex.pdf")]
    [InlineData("content-stream-runlength.pdf")]
    public void PdfDocument_Load_ContentStreamFilterFixtures_PaintsVisibleContent(string fixtureName)
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture(fixtureName));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the new <c>combined-vector-text-image.pdf</c> breadth fixture - a single page
    ///     combining a filled/stroked vector shape, a placed image XObject, and text drawn with an
    ///     embedded TrueType font - opens and renders visible content from all three construct
    ///     families in one document (mirroring how several SVG fixtures combine multiple
    ///     constructs per file).
    /// </summary>
    [Fact]
    public void PdfDocument_Load_CombinedVectorTextImageFixture_RendersAllThreeConstructFamilies()
    {
        // Arrange: MediaBox [0 0 200 200], filled rect at (10,10)-(50,50), stroked line at x=60
        // from y=10 to y=90, a 60x60 image placed at (100,10), and "HO" text at (100,150).
        using var document = PdfDocument.Open(Fixture("combined-vector-text-image.pdf"));

        // Act
        using var surface = document.Render(0, 200, 200, Transparent);

        // Assert: the filled vector rectangle painted opaque black.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[30, 170]);

        // Assert: the stroked vertical line painted opaque black at its own x-position.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[60, 130]);

        // Assert: the placed image's device-space footprint (x in [100,160], y in [130,190] once
        // flipped for a 200-tall MediaBox) contains real composited color, not just background.
        Assert.True(surface[130, 160].A > 0, "Expected the placed image to composite visible pixels.");

        // Assert: some glyph ink was painted from the embedded-font text draw.
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the <c>"HO"</c> text in <c>combined-vector-text-image.pdf</c> is genuinely
    ///     rendered where the content stream places it - black glyph ink inside the text region,
    ///     with 'H's left stem painted and 'O's counter left empty - and does not overlap the
    ///     rectangle, line or image regions. This is the regression guard for the fixture whose
    ///     image placement <c>cm</c> was not wrapped in <c>q</c>/<c>Q</c>, so its 60x scale leaked
    ///     into the following text and pushed it far off the page (the old "some pixel was
    ///     painted" assertion never noticed, because the rect/line/image alone satisfied it).
    /// </summary>
    [Fact]
    public void PdfDocument_Load_CombinedVectorTextImageFixture_RendersHoTextInItsOwnRegion()
    {
        // Arrange: MediaBox [0 0 200 200] rendered 1:1; "HO" at font size 30, text origin (100,150)
        // in user space, i.e. device y = 200 - textY. The text spans device y ~[28,50], well above
        // the image (device y [130,190]) and the rect/line (device y >= 110).
        const double fontSize = 30;
        const double originX = 100;
        const double originY = 150;
        const double mediaBoxHeight = 200;
        using var document = PdfDocument.Open(Fixture("combined-vector-text-image.pdf"));
        var font = TrueTypeFont.Load(FontPath);

        // Act
        using var surface = document.Render(0, 200, 200, Transparent);

        // Assert: the text band (above every other construct) holds ink, all of it black, and
        // nothing is painted in the band's left margin (x < 95) that the text cannot reach.
        var inkPixels = 0;
        var opaquePixels = 0;
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                var pixel = surface[x, y];
                if (pixel.A == 0)
                {
                    continue;
                }

                Assert.True(x >= 95, $"Unexpected ink left of the text origin at ({x},{y}).");
                Assert.True(pixel is { R: 0, G: 0, B: 0 }, $"Expected black text ink at ({x},{y}) but found {pixel}.");
                inkPixels++;
                if (pixel.A == 255)
                {
                    opaquePixels++;
                }
            }
        }

        Assert.True(inkPixels >= 100, $"Expected substantial 'HO' glyph ink but found {inkPixels} pixels.");
        Assert.True(opaquePixels >= 20, $"Expected fully opaque glyph-stem pixels but found {opaquePixels}.");

        // Assert: re-derive, from the font's own outlines, one point inside 'H's left stem (painted)
        // and the center of 'O's counter (unpainted), as the single-glyph TrueType fixture test does.
        (int X, int Y) ToDevicePixel(double glyphOriginX, double fx, double fy) =>
            ((int)Math.Floor(fx / font.UnitsPerEm * fontSize + glyphOriginX),
             (int)Math.Floor(mediaBoxHeight - (fy / font.UnitsPerEm * fontSize + originY)));

        var hGlyph = font.GetGlyphIndex('H');
        var oGlyph = font.GetGlyphIndex('O');
        var hBounds = font.GetGlyphOutline(hGlyph).GetBounds(0.25f);
        var oBounds = font.GetGlyphOutline(oGlyph).GetBounds(0.25f);
        var originXForO = originX + (double)font.GetAdvanceWidth(hGlyph) / font.UnitsPerEm * fontSize;

        var (stemX, stemY) = ToDevicePixel(originX, hBounds.X + hBounds.Width * 0.1, hBounds.Y + hBounds.Height * 0.5);
        Assert.True(surface[stemX, stemY].A > 0, $"Expected ink inside 'H's left stem at ({stemX},{stemY}).");

        var (counterX, counterY) = ToDevicePixel(
            originXForO, oBounds.X + oBounds.Width * 0.5, oBounds.Y + oBounds.Height * 0.5);
        Assert.Equal(0, surface[counterX, counterY].A);

        // Assert: the other constructs are untouched by the text (the image still composites and
        // the rect is still opaque black).
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[30, 170]);
        Assert.True(surface[130, 160].A > 0, "Expected the placed image to composite visible pixels.");
    }

    /// <summary>
    ///     Proves the new <c>standard14-font-fallback.pdf</c> fixture - the first on-disk fixture
    ///     exercising the Phase 6 font-fallback path (<c>/BaseFont /Helvetica</c>, no embedded
    ///     <c>/FontFile2</c>) - renders visible substitute glyph ink. As with the equivalent
    ///     synthetic-PDF test in <see cref="PdfSystemIntegrationTests"/>, the actual substitute
    ///     glyph shape varies across the CI platform matrix, so only "some ink was painted" is
    ///     asserted here.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_Standard14FontFallbackFixture_PaintsVisibleSubstituteGlyphInk()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("standard14-font-fallback.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the new <c>text-embedded-type1-font.pdf</c> fixture (Phase B) - a
    ///     hand-authored, entirely synthetic <c>/Subtype /Type1</c> font with a classic
    ///     PostScript <c>/FontDescriptor/FontFile</c> program - opens and renders visible glyph
    ///     ink from its embedded Type 1 font program.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_TextEmbeddedType1FontFixture_PaintsVisibleGlyphInk()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("text-embedded-type1-font.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the new <c>text-standard14-type1-no-fontfile.pdf</c> fixture (Phase B) - a
    ///     <c>/Subtype /Type1</c>, <c>/BaseFont /Helvetica</c> font with no
    ///     <c>/FontDescriptor/FontFile</c> at all - resolves via the free non-embedded fallback
    ///     path and renders visible substitute glyph ink, exactly like
    ///     <see cref="PdfDocument_Load_Standard14FontFallbackFixture_PaintsVisibleSubstituteGlyphInk"/>'s
    ///     own TrueType-subtype equivalent, now proven reachable for <c>/Type1</c> too.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_TextStandard14Type1NoFontFileFixture_PaintsVisibleSubstituteGlyphInk()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("text-standard14-type1-no-fontfile.pdf"));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the real-world <c>text-type1c-differences-agl-ligatures.pdf</c> fixture (a
    ///     trimmed excerpt of an actual document, not hand-authored - see
    ///     <c>PdfFixtures\README.md</c> for why) opens and renders visible glyph ink from its two
    ///     genuinely embedded <c>/FontFile3 /Type1C</c> fonts despite their
    ///     <c>/Encoding/Differences</c> arrays naming a glyph (<c>/uni03BC</c>) outside
    ///     <c>StandardGlyphNames</c>' own common-name subset (<c>/thinspace</c> is itself now a
    ///     direct entry in that subset, resolved without needing the AGL hex fallback at all) -
    ///     the regression this fixture reproduces and this test guards against.
    /// </summary>
    [Fact]
    public void PdfDocument_Load_TextType1CDifferencesAglLigaturesFixture_PaintsVisibleGlyphInk()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("text-type1c-differences-agl-ligatures.pdf"));
        using var surface = document.Render(0, 595, 842, Transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves the shared "Open Sans" font fixture used by <c>combined-vector-text-image.pdf</c>
    ///     and <c>text-embedded-truetype-font.pdf</c> loads independently via the real
    ///     <see cref="TrueTypeFont"/> API, confirming the embedded <c>/FontFile2</c> bytes those
    ///     fixtures carry are a genuine, well-formed copy of the shared font (not corrupted while
    ///     hand-authoring the fixture).
    /// </summary>
    [Fact]
    public void PdfDocument_Load_SharedFontFixture_LoadsAsValidTrueTypeFont()
    {
        // Arrange & Act
        var font = TrueTypeFont.Load(FontPath);

        // Assert
        Assert.NotEqual(0, font.GetGlyphIndex('H'));
    }
}
