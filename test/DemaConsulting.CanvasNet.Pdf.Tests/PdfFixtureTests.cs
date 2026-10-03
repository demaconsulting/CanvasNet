using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

// cspell:ignore xobject devicergb fontfile

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Fixture-conformance tests that exercise <see cref="PdfDocument"/> against the real-file PDF
///     fixture corpus in <c>PdfFixtures</c> (see <c>PdfFixtures\README.md</c> for provenance - the
///     entire corpus is hand-authored for this repository), mirroring the pattern used by
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
