using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx pythonpptx samplelib groupshape autoshape autoshapes paintable sppr
// cspell:ignore aiden0z blipfill pattfill custgeom avlst gridcol tblgrid srcrect cxnsp lummod prstdash cmpd thickthin xfrm Xfrm FAFAF
// cspell:ignore slidenum

/// <summary>
///     Fixture-conformance tests that exercise <see cref="PptxDocument"/>'s full public
///     <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> API against the
///     real-world <c>.pptx</c> fixture corpus in <c>PptxFixtures</c> (see
///     <c>PptxFixtures\README.md</c> for provenance - twenty real files from three independent
///     sources, <c>python-pptx</c>'s own MIT-licensed test corpus, samplelib.com's
///     permissively-stated sample downloads, and <c>aiden0z/pptx-renderer</c>'s own
///     Apache-2.0-licensed example corpus). Each test opens one on-disk fixture and renders
///     every one of its slides, asserting a broad "this real file opens and renders every slide
///     without an ungraceful failure, painting real content where content is expected" result -
///     a tier distinct from (and complementing, not duplicating) the finer-grained,
///     single-construct unit tests already made against hand-authored synthetic packages
///     elsewhere in this project (for example <see cref="PptxRenderTests"/>,
///     <see cref="PptxTablesTests"/>, <see cref="PptxGroupsTests"/>).
/// </summary>
/// <remarks>
///     Two specific slides in this corpus declare a chart (and, in one case, also a SmartArt/
///     diagram) <c>&lt;p:graphicFrame&gt;</c> - a construct explicitly out of scope for this
///     project (see <c>pptx-document.md</c>'s deferred-items lists). <see cref="PptxDocument"/>'s
///     own <c>ParseTable</c> resolver already throws <see cref="PptxUnsupportedFeatureException"/>
///     (feature token <c>"pptx-graphic-frame-kind"</c>) for any non-table graphic-frame kind
///     before attempting to read any chart/diagram-specific XML - this is the established,
///     already-graceful convention for a recognized-but-unsupported construct, confirmed here
///     against real files rather than merely predicted from static code reading (see
///     <c>PptxFixtures\README.md</c>'s own "what this corpus can - and cannot - prove" section for
///     the exact honesty boundary of that claim).
/// </remarks>
public class PptxFixturesCorpusTests
{
    /// <summary>The PresentationML namespace, used to inspect a parsed shape element's own name.</summary>
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>The directory containing the PPTX fixture corpus, copied to the test output directory
    ///     by this project's <c>PptxFixtures\**</c> content item.</summary>
    private static string FixturesPath => Path.Join(AppContext.BaseDirectory, "PptxFixtures");

    /// <summary>Resolves a fixture file within <see cref="FixturesPath"/>.</summary>
    private static string Fixture(string name) => Path.Join(FixturesPath, name);

    /// <summary>
    ///     A fully transparent <see cref="PptxRenderOptions.BackgroundColor"/>, used so
    ///     <see cref="AssertPaintedSomePixel"/>'s "any painted pixel" check keeps proving real
    ///     content was rendered rather than becoming vacuously true against an opaque-white
    ///     default background.
    /// </summary>
    private static readonly PptxRenderOptions Transparent = new() { BackgroundColor = new Rgba32(0, 0, 0, 0) };

    /// <summary>
    ///     The rendering resolution (dots per inch) used for every fixture render in this file -
    ///     passed to <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/>, which
    ///     derives pixel dimensions from each fixture's own <see cref="PptxDocument.SlideSize"/>,
    ///     preserving its own aspect ratio rather than forcing a fixed width/height regardless of
    ///     the slide's declared shape.
    /// </summary>
    private const float Dpi = 96f;

    /// <summary>
    ///     Proves that at least one non-transparent pixel was painted somewhere on
    ///     <paramref name="surface"/> - the broad "real content was rendered, not merely a blank
    ///     surface" assertion this fixture-conformance tier favors over per-pixel checks.
    /// </summary>
    private static void AssertPaintedSomePixel(Surface surface)
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
    ///     Proves both <paramref name="first"/> and <paramref name="second"/> appear somewhere
    ///     within the pixel rectangle <c>[x0, x1) x [y0, y1)</c> of <paramref name="surface"/> -
    ///     used to prove a pattern-fill tile genuinely paints real foreground/background variation
    ///     rather than a flat fill, by sampling a strip well inside a pattern-filled shape's own
    ///     rounded-rectangle bounds (away from its stroke, rounded corners, and centered text) so
    ///     only the tile's own two colors are expected to appear.
    /// </summary>
    private static void AssertRegionContainsBothColors(
        Surface surface,
        int x0,
        int y0,
        int x1,
        int y1,
        Rgba32 first,
        Rgba32 second)
    {
        var sawFirst = false;
        var sawSecond = false;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var pixel = surface[x, y];
                if (pixel == first)
                {
                    sawFirst = true;
                }
                else if (pixel == second)
                {
                    sawSecond = true;
                }
            }
        }

        Assert.True(sawFirst, $"Expected color {first} to appear within [{x0},{y0})-[{x1},{y1}).");
        Assert.True(sawSecond, $"Expected color {second} to appear within [{x0},{y0})-[{x1},{y1}).");
    }

    /// <summary>
    ///     Proves <paramref name="surface"/> is fully opaque pure white (no color override at all)
    ///     throughout the pixel rectangle <c>[x0, x1) x [y0, y1)</c> - used to prove a bare,
    ///     attribute-less <c>&lt;a:pattFill/&gt;</c> (no <c>prst</c> at all) resolves to
    ///     <see cref="PptxNoFill"/> rather than falling back to the shape's own
    ///     <c>&lt;p:style&gt;/&lt;a:fillRef&gt;</c> (which, for this fixture shape, resolves to a
    ///     visibly non-white accent1 gradient/solid - see the fixture's own
    ///     <c>fillRef idx="3"</c>). The slide's own background renders fully opaque white (not
    ///     literally transparent - see this test's own remarks), so "no override fill painted"
    ///     is proven by the shape's own interior matching that same opaque white, not by a
    ///     literal zero-alpha pixel. The sampled strip is chosen well inside the shape's own
    ///     bounds, away from its stroke and its own centered text, so only the shape's own fill
    ///     (or lack of it) could plausibly affect these pixels.
    /// </summary>
    private static void AssertRegionIsUnfilledWhite(Surface surface, int x0, int y0, int x1, int y1)
    {
        var white = new Rgba32(255, 255, 255, 255);
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                Assert.True(
                    surface[x, y] == white,
                    $"Expected unfilled pure-white pixel at ({x},{y}), found {surface[x, y]}.");
            }
        }
    }

    /// <summary>
    ///     Proves <c>pythonpptx-sld-blank.pptx</c> and <c>samplelib-sample-blank.pptx</c> - one
    ///     truly empty slide each, from two independent real-world sources - open, report exactly
    ///     one slide at a sane (positive) declared size, and render without error. Unlike every
    ///     other fixture in this file, no painted-pixel assertion is made here: an empty slide
    ///     genuinely has no shape tree content to paint, so asserting painted ink against a
    ///     transparent background would either be vacuously false or would require abandoning the
    ///     transparent background this file otherwise relies on to keep its other assertions
    ///     meaningful - "opens and renders a correctly-sized blank surface without throwing" is the
    ///     one honest claim a truly blank slide fixture can support.
    /// </summary>
    [Theory]
    [InlineData("pythonpptx-sld-blank.pptx")]
    [InlineData("samplelib-sample-blank.pptx")]
    public void PptxDocument_Render_BlankSlideFixtures_RendersWithoutError(string fixtureName)
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture(fixtureName));

        // Assert
        Assert.Equal(1, document.SlideCount);
        Assert.True(document.SlideSize.WidthEmu > 0);
        Assert.True(document.SlideSize.HeightEmu > 0);

        using var surface = document.Render(0, Dpi, Transparent);
        Assert.True(surface.Width > 0);
        Assert.True(surface.Height > 0);
    }

    /// <summary>
    ///     Proves <c>pythonpptx-shp-autoshape-props.pptx</c> (a single autoshape with an
    ///     adjustment-value handle, resolved via the default-proportion approximation - full
    ///     <c>avLst</c> parsing itself remains deferred) renders its one slide and paints visible
    ///     content.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_ShpAutoshapePropsFixture_PaintsVisibleContent()
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture("pythonpptx-shp-autoshape-props.pptx"));

        // Assert
        Assert.Equal(1, document.SlideCount);
        using var surface = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves <c>pythonpptx-shp-groupshape.pptx</c> (a <c>&lt;p:grpSp&gt;</c> group shape with
    ///     nested children and several <c>avLst</c>-bearing autoshapes) opens, reports a sane
    ///     slide count/size, and renders its one slide without throwing - group-transform
    ///     composition exercised against a real file. Unlike this file's other content fixtures,
    ///     no painted-pixel assertion is made here: every one of this fixture's four shapes
    ///     declares its fill/line/font exclusively via a <c>&lt;p:style&gt;</c> shape-style-matrix
    ///     reference (<c>&lt;a:fillRef&gt;</c>/<c>&lt;a:lnRef&gt;</c>/<c>&lt;a:effectRef&gt;</c>/
    ///     <c>&lt;a:fontRef&gt;</c>, each only an <c>idx</c> plus a <c>&lt;a:schemeClr&gt;</c>),
    ///     never an explicit <c>&lt;a:solidFill&gt;</c>/<c>&lt;a:ln&gt;</c> inside its own
    ///     <c>&lt;p:spPr&gt;</c> - and every shape's <c>&lt;p:txBody&gt;</c> is empty (an
    ///     <c>&lt;a:endParaRPr/&gt;</c> only, no actual run). Resolving a shape-style-matrix
    ///     reference into a concrete fill/line color is explicitly out of this project's
    ///     documented scope (see <c>pptx-document.md</c>'s own "phClr... only meaningful inside a
    ///     shape-style-reference context this phase does not thread through" and "Group-level
    ///     style cascading... is not implemented" remarks) - <see cref="PptxDocument.ResolveFill"/>
    ///     already gracefully resolves an unrecognized/absent fill definition to
    ///     <c>PptxNoFill</c> rather than throwing (a deliberate, documented Phase 1c
    ///     simplification, not a bug), so this fixture, as authored, genuinely has zero paintable
    ///     ink under the currently-implemented feature set - this smoke test can't prove more than
    ///     "a real group-shape-bearing file opens and renders without an ungraceful failure" for
    ///     it (see <c>PptxFixtures\README.md</c>'s own "what this corpus can - and cannot - prove"
    ///     section for the identical honesty treatment applied to the chart-exception slides).
    /// </summary>
    [Fact]
    public void PptxDocument_Render_ShpGroupShapeFixture_RendersWithoutError()
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture("pythonpptx-shp-groupshape.pptx"));

        // Assert
        Assert.Equal(1, document.SlideCount);
        Assert.True(document.SlideSize.WidthEmu > 0);
        Assert.True(document.SlideSize.HeightEmu > 0);

        using var surface = document.Render(0, Dpi, Transparent);
        Assert.True(surface.Width > 0);
        Assert.True(surface.Height > 0);
    }

    /// <summary>
    ///     Proves every slide of <c>pythonpptx-shp-picture.pptx</c> (two slides, each placing an
    ///     embedded PNG raster picture) renders and paints visible content - picture decode/
    ///     composite exercised against real embedded image bytes.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_ShpPictureFixture_RendersEverySlideWithVisibleContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-shp-picture.pptx"));

        // Act & Assert
        Assert.Equal(2, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves every slide of <c>pythonpptx-tbl-cell.pptx</c> (three slides of real
    ///     <c>&lt;a:tbl&gt;</c> tables, including merged cells, and no chart/OLE relationships at
    ///     all) renders and paints visible content - table/merge-resolution exercised against a
    ///     real file with no graphic-frame-kind ambiguity.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_TblCellFixture_RendersEverySlideWithVisibleContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-tbl-cell.pptx"));

        // Act & Assert
        Assert.Equal(3, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves every slide of <c>pythonpptx-txt-font-props.pptx</c> (five text-heavy slides
    ///     exercising run/paragraph-level font properties) renders and paints visible glyph ink.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_TxtFontPropsFixture_RendersEverySlideWithVisibleContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-txt-font-props.pptx"));

        // Act & Assert
        Assert.Equal(5, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves every slide of <c>pythonpptx-txt-text-frame.pptx</c> (two slides exercising
    ///     text-frame-level properties such as margins, wrapping, and anchoring) renders and
    ///     paints visible glyph ink.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_TxtTextFrameFixture_RendersEverySlideWithVisibleContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-txt-text-frame.pptx"));

        // Act & Assert
        Assert.Equal(2, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves <c>pythonpptx-shp-shapes.pptx</c>'s richest, two-slide mix of real-world
    ///     constructs behaves exactly as the corpus README documents: slide index 0 (a real
    ///     <c>&lt;a:tbl&gt;</c> table graphic frame alongside a chart and a SmartArt/diagram
    ///     graphic frame) throws <see cref="PptxUnsupportedFeatureException"/> - the already-
    ///     graceful, already-designed "unsupported graphic-frame kind" path - while slide index 1
    ///     (connectors, a nested group, several <c>avLst</c>-bearing autoshapes, and a GIF image)
    ///     renders cleanly and paints visible content.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_ShpShapesFixture_Slide0ThrowsUnsupportedFeatureSlide1PaintsContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-shp-shapes.pptx"));

        // Assert: slide count
        Assert.Equal(2, document.SlideCount);

        // Act & Assert: slide 0's chart/diagram graphic frame is a recognized-but-unsupported
        // construct - the whole slide's Render call throws, rather than silently skipping or
        // crashing ungracefully.
        var exception = Assert.Throws<PptxUnsupportedFeatureException>(() => document.Render(0, Dpi, Transparent));
        Assert.Equal("pptx-graphic-frame-kind", exception.Feature);

        // Act & Assert: slide 1 has no chart/diagram content and renders cleanly.
        using var surface = document.Render(1, Dpi, Transparent);
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves <c>samplelib-sample-presentation.pptx</c> (an eight-slide, independently-
    ///     sourced real-world deck) behaves exactly as the corpus README documents: slide index 4
    ///     (a chart graphic frame) throws <see cref="PptxUnsupportedFeatureException"/>, while
    ///     every other slide (0-3 and 5-7, including slide 3's own real <c>&lt;a:tbl&gt;</c>
    ///     table) renders cleanly and paints visible content.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_SamplelibSamplePresentationFixture_Slide4ThrowsUnsupportedFeatureOthersPaintContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("samplelib-sample-presentation.pptx"));

        // Assert: slide count
        Assert.Equal(8, document.SlideCount);

        // Act & Assert: every slide except index 4 renders cleanly and paints visible content.
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            if (slideIndex == 4)
            {
                var exception = Assert.Throws<PptxUnsupportedFeatureException>(() => document.Render(slideIndex, Dpi, Transparent));
                Assert.Equal("pptx-graphic-frame-kind", exception.Feature);
                continue;
            }

            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves both slides of <c>pythonpptx-sld-background.pptx</c> render without error, and
    ///     that slide index 1's own declared <c>&lt;p:bg&gt;</c> solid-red fill is now painted -
    ///     a pixel-level real-file regression test for the Phase 2 Follow-Up background-fill
    ///     feature (see <c>pptx-document.md</c>'s "Phase 2 Follow-Up: Slide/Layout/Master
    ///     Background Fill" design section). Slide index 0 has no shape-tree content and declares
    ///     no <c>&lt;p:bg&gt;</c> of its own, so it still renders as a blank, fully transparent
    ///     surface.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_SldBackgroundFixture_RendersAndPaintsSlideBackgroundFill()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-sld-background.pptx"));

        // Act & Assert
        Assert.Equal(2, document.SlideCount);
        using var surface0 = document.Render(0, Dpi, Transparent);
        Assert.True(surface0.Width > 0);
        Assert.True(surface0.Height > 0);

        using var surface1 = document.Render(1, Dpi, Transparent);
        Assert.True(surface1.Width > 0);
        Assert.True(surface1.Height > 0);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0x00, 255), surface1[surface1.Width / 2, surface1.Height / 2]);
    }

    /// <summary>
    ///     Proves both slides of <c>pythonpptx-ph-inherit-props.pptx</c> render without error -
    ///     real-file regression coverage for placeholder <c>&lt;a:xfrm&gt;</c>/geometry
    ///     inheritance (see <c>Render_PlaceholderShapeWithEmptySpPr_InheritsXfrmAndGeometryFromLayout</c>
    ///     and this unit's own Phase 2 design-doc section for the original bug this guards
    ///     against). Slide index 0 paints (an idx-matched, no-<c>type</c> content placeholder with
    ///     its own chevron geometry); slide index 1 does not paint (both placeholders have empty
    ///     text and rely entirely on inherited geometry) - both asserted explicitly, mirroring the
    ///     existing <c>ShpGroupShapeFixture</c> precedent's honesty about exactly what each slide
    ///     can and cannot prove.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_PhInheritPropsFixture_Slide0PaintsSlide1RendersWithoutError()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-ph-inherit-props.pptx"));

        // Assert: slide count
        Assert.Equal(2, document.SlideCount);

        // Act & Assert: slide 0 has its own chevron geometry and paints.
        using var surface0 = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface0);

        // Act & Assert: slide 1's placeholders are both empty text, inherited geometry only.
        using var surface1 = document.Render(1, Dpi, Transparent);
        Assert.True(surface1.Width > 0);
        Assert.True(surface1.Height > 0);
    }

    /// <summary>
    ///     Proves every one of <c>pythonpptx-ph-unpopulated-placeholders.pptx</c>'s nine slides
    ///     renders without error. Each slide contains exactly one empty, fully-inherited
    ///     placeholder of a distinct declared type (title/body/chart/table/diagram/media/clip art/
    ///     picture) - no slide has any populated text or non-placeholder shape content, so no
    ///     painted-pixel assertion is made for any slide (the same honest "renders without an
    ///     ungraceful failure" claim already established for <c>ShpGroupShapeFixture</c> and the
    ///     blank-slide fixtures above).
    /// </summary>
    [Fact]
    public void PptxDocument_Render_PhUnpopulatedPlaceholdersFixture_RendersEverySlideWithoutError()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-ph-unpopulated-placeholders.pptx"));

        // Act & Assert
        Assert.Equal(9, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            Assert.True(surface.Width > 0);
            Assert.True(surface.Height > 0);
        }
    }

    /// <summary>
    ///     Proves <c>pythonpptx-txt-fit-text.pptx</c> (a single slide with a real paragraph using
    ///     <c>wrap="none"</c> combined with <c>&lt;a:spAutoFit/&gt;</c> - shape-resize autofit
    ///     itself remains deferred, see <c>pptx-document.md</c>'s deferred-items list, so no
    ///     scaling is applied) renders its one slide and paints visible glyph ink.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_TxtFitTextFixture_PaintsVisibleContent()
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture("pythonpptx-txt-fit-text.pptx"));

        // Assert
        Assert.Equal(1, document.SlideCount);
        using var surface = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves both slides of <c>pythonpptx-shp-connector-props.pptx</c> render without error
    ///     and now actually paint their own connector's visible line (Phase 2 Follow-Up:
    ///     Connector Shape Rendering). Slide index 0 contains only a lone, diagonal
    ///     <c>&lt;p:cxnSp&gt;</c> connector with a <c>&lt;a:tailEnd type="arrow"/&gt;</c>
    ///     arrowhead and no own width/fill (relying entirely on its own <c>&lt;p:style&gt;/
    ///     &lt;a:lnRef idx="2"/&gt;</c> for its visible color/width - see
    ///     <see cref="PptxDocument.ResolveConnectorLineStyle"/>'s own merge-resolver remarks for
    ///     why this is a real, representative case, not a contrived one); slide index 1
    ///     additionally places a picture alongside its own (perfectly horizontal,
    ///     <c>&lt;a:ext cy="0"/&gt;</c>) connector, so slide 1 paints both the picture and the
    ///     connector's own line.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_ShpConnectorPropsFixture_ConnectorsPaintVisibleLines()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-shp-connector-props.pptx"));

        // Assert: slide count
        Assert.Equal(2, document.SlideCount);

        // Act & Assert: slide 0 has only a connector, now painting its own diagonal line (and
        // tailEnd arrow), resolved entirely from its own <p:style>/<a:lnRef> fallback.
        using var surface0 = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface0);

        // Act & Assert: slide 1 has a picture alongside its own zero-height connector - both
        // paint (the picture, and the connector's own now-fixed horizontal line).
        using var surface1 = document.Render(1, Dpi, Transparent);
        AssertPaintedSomePixel(surface1);
    }

    /// <summary>
    ///     Proves slide 0 of <c>pythonpptx-dml-fill.pptx</c> - which places six shapes in document
    ///     order (inherited fill, explicit <c>&lt;a:noFill/&gt;</c>, solid RGB fill, a real embedded
    ///     <c>&lt;a:blipFill&gt;&lt;a:blip r:embed="..."/&gt;&lt;a:tile tx="0" ty="0" sx="100000"
    ///     sy="100000"/&gt;&lt;/a:blipFill&gt;</c> picture fill, a linear gradient fill, and finally
    ///     an <c>&lt;a:pattFill prst="divot"&gt;</c> pattern fill - on this <em>same</em> slide -
    ///     now renders completely without throwing. Before picture-fill support, rendering slide 0
    ///     threw <c>"pptx-picture-fill"</c> as soon as the shape-tree walk reached the fourth
    ///     (blip/tile-filled) shape; after picture-fill support, it threw <c>"pptx-pattern-fill"</c>
    ///     instead, once the walk reached the sixth (and last, divot-pattern-filled) shape. Now
    ///     that pattern fill is also supported (see <c>PptxPaint.cs</c>'s
    ///     <see cref="PptxPatternFill"/> and <c>PptxDocument.Paint.cs</c>'s <c>ResolveFill</c>),
    ///     every shape on this slide resolves and paints successfully - proven here by sampling a
    ///     strip well inside the divot shape's own bounds and asserting both its resolved
    ///     foreground (<c>srgbClr val="2CB731"</c>) and background (<c>prstClr val="white"</c>)
    ///     colors actually appear, confirming real tile variation rather than a flat/degenerate
    ///     fill. A genuine, full end-to-end "renders without throwing" assertion for an
    ///     <c>&lt;a:blipFill&gt;</c>/<c>&lt;a:tile&gt;</c> shape fill (this slide cannot isolate
    ///     that alone, since it also contains other shapes) lives in
    ///     <see cref="PptxSystemIntegrationTests.CanvasNetPptx_SystemIntegration_Images_TileBlipFillShapeDecodesAndRendersEndToEnd"/>
    ///     instead, against a minimal, purpose-built single-shape package.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_DmlFillFixture_Slide0RendersPictureAndPatternFillShapesWithoutThrowing()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-dml-fill.pptx"));

        // Act: the walk now gets past both the blip/tile picture-fill shape and the
        // divot-pattern-filled shape without throwing.
        using var surface = document.Render(0, Dpi, Transparent);

        // Assert: real content was painted somewhere on the slide.
        AssertPaintedSomePixel(surface);

        // Assert: the last shape ("Rounded Rectangle 9", off (457200,1836780), ext
        // (1368245,914400) EMU - see PptxFixtures\README.md's dml-fill.pptx provenance notes)
        // genuinely paints both its resolved divot foreground and background colors - not a flat
        // fill - sampled well inside its rounded-rectangle bounds, away from its stroke, rounded
        // corners, and centered text.
        var divotFg = new Rgba32(0x2C, 0xB7, 0x31, 255);
        var divotBg = new Rgba32(0xFF, 0xFF, 0xFF, 255);
        AssertRegionContainsBothColors(surface, 68, 213, 171, 233, divotFg, divotBg);
        AssertRegionContainsBothColors(surface, 68, 249, 171, 269, divotFg, divotBg);
    }

    /// <summary>
    ///     Proves slide 1 of <c>pythonpptx-dml-fill.pptx</c> - three shapes: a bare, attribute-less
    ///     <c>&lt;a:pattFill/&gt;</c> (no <c>prst</c> at all), a <c>prst="divot"</c> pattern fill,
    ///     and a <c>prst="wave"</c> pattern fill - renders completely without throwing, now that
    ///     pattern fill is supported (feature token <c>"pptx-pattern-fill"</c> no longer applies to
    ///     either covered preset in this fixture). The bare <c>&lt;a:pattFill/&gt;</c> shape (named
    ///     "autoshape with inherited fill" in its own text run, but in fact resolving to
    ///     <see cref="PptxNoFill"/> rather than its own style's <c>&lt;a:fillRef idx="3"&gt;</c> -
    ///     a deliberate, documented design decision; see the planning report's own Assumption 3)
    ///     is proven to paint no fill-color override of its own: the slide's own background
    ///     renders fully opaque white (not literally transparent), so its sampled interior (away
    ///     from its own stroke and centered text) is proven to remain that same unfilled white
    ///     rather than painting its style's own fillRef accent1 fill. The two named-preset shapes
    ///     are each proven to paint both their resolved foreground and background colors.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_DmlFillFixture_Slide1PatternFillShapesRenderAndBareFillIsTransparent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-dml-fill.pptx"));

        // Assert: slide count
        Assert.Equal(2, document.SlideCount);

        // Act: slide 1 renders completely without throwing.
        using var surface = document.Render(1, Dpi, Transparent);

        // Assert: "Rounded Rectangle 1" (off (457200,457200), ext (1368245,914400) EMU) has a
        // bare <a:pattFill/> with no prst attribute at all, resolving to PptxNoFill - its interior
        // (sampled away from its own stroke and centered text) remains unfilled white, not
        // falling back to its style's fillRef accent1 fill.
        AssertRegionIsUnfilledWhite(surface, 65, 58, 175, 76);
        AssertRegionIsUnfilledWhite(surface, 65, 128, 175, 140);

        // Assert: "Rounded Rectangle 2" (off (2291174,457200), ext (1368245,914400) EMU) has
        // prst="divot" with the same fg/bg colors as slide 0's divot shape, and genuinely paints
        // both.
        var divotFg = new Rgba32(0x2C, 0xB7, 0x31, 255);
        var divotBg = new Rgba32(0xFF, 0xFF, 0xFF, 255);
        AssertRegionContainsBothColors(surface, 260, 68, 363, 88, divotFg, divotBg);
        AssertRegionContainsBothColors(surface, 260, 104, 363, 124, divotFg, divotBg);

        // Assert: "Rounded Rectangle 3" (off (4114800,457200), ext (1368245,914400) EMU) has
        // prst="wave" with its own distinct fg/bg colors, and genuinely paints both.
        var waveFg = new Rgba32(0xC0, 0x50, 0x4D, 255);
        var waveBg = new Rgba32(0xFF, 0xFF, 0xFF, 255);
        AssertRegionContainsBothColors(surface, 452, 68, 555, 88, waveFg, waveBg);
        AssertRegionContainsBothColors(surface, 452, 104, 555, 124, waveFg, waveBg);
    }

    /// <summary>
    ///     Proves every slide of <c>pythonpptx-dml-line.pptx</c> (four slides with real, explicit
    ///     <c>&lt;a:solidFill&gt;</c>/<c>&lt;a:ln&gt;</c> stroke-property variety - RGB and
    ///     scheme+<c>lumMod</c> colors, a <c>cmpd="thickThin"</c> compound line, and several
    ///     <c>prstDash</c> values) renders and paints visible content - genuinely new in-scope
    ///     stroke-property variety not covered by any style-matrix-only existing fixture.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_DmlLineFixture_RendersEverySlideWithVisibleContent()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("pythonpptx-dml-line.pptx"));

        // Act & Assert
        Assert.Equal(4, document.SlideCount);
        for (var slideIndex = 0; slideIndex < document.SlideCount; slideIndex++)
        {
            using var surface = document.Render(slideIndex, Dpi, Transparent);
            AssertPaintedSomePixel(surface);
        }
    }

    /// <summary>
    ///     Proves <c>aiden0z-1-chart-and-complex.pptx</c> (two slides from
    ///     <c>aiden0z/pptx-renderer</c>'s own Apache-2.0-licensed example corpus) behaves exactly
    ///     as the corpus README documents: slide index 0 is a dense, real org-chart-style slide
    ///     (58 shapes, 17 connectors, a <c>&lt;a:custGeom&gt;</c> freeform, and 11 <c>avLst</c>
    ///     adjustment overrides across varied preset geometries) that renders cleanly and paints
    ///     visible content with no chart graphic frame of its own, while slide index 1 declares a
    ///     chart graphic frame and throws <see cref="PptxUnsupportedFeatureException"/> - the
    ///     same already-graceful, already-designed "unsupported graphic-frame kind" path proven
    ///     elsewhere in this corpus against the <c>python-pptx</c>/<c>samplelib.com</c> fixtures.
    /// </summary>
    /// <remarks>
    ///     Slide index 0 uses <c>ppt/slideLayouts/slideLayout1.xml</c>, which (per the companion
    ///     Phase 2 Follow-Up planning report) declares a real, non-placeholder, <c>userDrawn="1"</c>
    ///     <c>&lt;a:custGeom&gt;</c> freeform shape named "Freeform 5" (<c>&lt;a:off x="750334"
    ///     y="762000"/&gt;</c>, <c>&lt;a:ext cx="3941064" cy="312470"/&gt;</c>), filled
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c> - this fixture's own theme resolves <c>bg1</c> to
    ///     pure white, the same color as this fixture's own slide master background
    ///     (<c>&lt;p:bgRef idx="1001"&gt;&lt;a:schemeClr val="bg1"/&gt;&lt;/p:bgRef&gt;</c>), so a
    ///     pixel sample against this shape cannot prove a visually dramatic z-order/contrast
    ///     difference (the synthetic <see cref="PptxRenderTests"/> master/layout tests carry that
    ///     proof with deliberately contrasting colors) - this real-world regression test's primary
    ///     proof is instead structural: that this layout's own parsed shape tree genuinely
    ///     contains "Freeform 5" as a non-placeholder shape node at all. A near-white pixel sample
    ///     in its own bounding box is kept only as a secondary, no-throw regression check, since a
    ///     pure-white pixel there is also what an unimplemented or broken dispatch path would
    ///     produce (this fixture's own background is pure white too).
    /// </remarks>
    [Fact]
    public void PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0PaintsSlide1ThrowsUnsupportedFeature()
    {
        // Arrange
        using var document = PptxDocument.Open(Fixture("aiden0z-1-chart-and-complex.pptx"));

        // Assert: slide count
        Assert.Equal(2, document.SlideCount);

        // Act & Assert: slide 0 has no chart content and renders cleanly with real paint.
        using var surface0 = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface0);

        // Act & Assert (primary, structurally-discriminating proof): slide layout 1's own parsed
        // shape tree genuinely contains "Freeform 5" as a real, non-placeholder PptxSpShapeNode -
        // proving the master/layout decorative-shape parsing path actually walks this real file's
        // own layout shape tree, independent of this shape's own paint color (see this test's own
        // remarks for why a pixel sample alone cannot discriminate this shape from its background).
        var layout = document.GetLayout("ppt/slideLayouts/slideLayout1.xml");
        var freeform5 = layout.ShapeTree
            .OfType<PptxSpShapeNode>()
            .SingleOrDefault(node => node.ShapeElement
                .Descendants(P + "cNvPr")
                .Any(cNvPr => (string?)cNvPr.Attribute("name") == "Freeform 5"));
        Assert.NotNull(freeform5);
        Assert.Null(freeform5.Placeholder);

        // Act & Assert (secondary, no-throw regression check): slide layout 1's own "Freeform 5"
        // decorative custGeom shape (sampled near its own <a:off>/<a:ext> bounding box, in the
        // vertical band above the slide's own title placeholder so the sample isn't masked by
        // slide-level content painted on top) renders without throwing; the sampled pixel is at
        // least near-white (a tolerance, not an exact match, because this real custGeom path is a
        // thin decorative flourish rather than a solid rectangle - most of its own bounding box is
        // unfilled background, and the rasterizer anti-aliases the path's own edges). This pixel
        // sample is deliberately NOT the primary proof above, since a pure-white pixel here is
        // consistent with both a correctly-rendered shape and a silently-skipped one.
        var slideSize = document.SlideSize;
        var freeformMidXEmu = 750334 + 3941064 / 2.0;
        var freeformMidYEmu = (762000 + 989358) / 2.0;
        var x = (int)(freeformMidXEmu / slideSize.WidthEmu * surface0.Width);
        var y = (int)(freeformMidYEmu / slideSize.HeightEmu * surface0.Height);
        var actual = surface0[Math.Clamp(x, 0, surface0.Width - 1), Math.Clamp(y, 0, surface0.Height - 1)];
        Assert.True(actual is { R: >= 240, G: >= 240, B: >= 240, A: 255 },
            $"Expected a near-white pixel at ({x}, {y}) (this fixture's own bg1 theme color and master background both resolve to pure white), got R{actual.R} G{actual.G} B{actual.B} A{actual.A}.");

        // Act & Assert (Phase 2 Follow-Up: Bullets and Numbering): "Rectangle 5"
        // (<a:off x="5322536" y="5031720"/>, <a:ext cx="6030522" cy="1080000"/>,
        // <a:bodyPr lIns="72000" .../>) declares two consecutive
        // <a:pPr marL="171450" indent="-171450"><a:buChar char="&#8226;"/></a:pPr> paragraphs -
        // the real-world hanging-indent bullet idiom this phase targets. Because marL+indent==0,
        // the bullet-gutter X this phase computes (insetLeft+marL+indent) collapses to exactly
        // insetLeft (72000 EMU in from the shape's own left edge) - strictly left of where any
        // paragraph's own text can ever start (insetLeft+marL = 243450 EMU in) - so a dark pixel
        // found in that narrow gutter column can only be the bullet glyph itself, never run text.
        // A second sample just inside the shape's own left inset (before the gutter) proves the
        // gutter ink does not simply extend all the way to the shape's own edge.
        var shapeOffXEmu = 5322536d;
        var shapeOffYEmu = 5031720d;
        var shapeCyEmu = 1080000d;
        var bulletGutterXEmu = shapeOffXEmu + 72000d;
        var beforeGutterXEmu = shapeOffXEmu + 20000d;
        var topYEmu = shapeOffYEmu + 40000d;
        var bottomYEmu = shapeOffYEmu + shapeCyEmu - 40000d;

        static int ToPixelX(double emu, PptxSlideSize slideSize, int surfaceWidth) =>
            Math.Clamp((int)(emu / slideSize.WidthEmu * surfaceWidth), 0, surfaceWidth - 1);

        static int ToPixelY(double emu, PptxSlideSize slideSize, int surfaceHeight) =>
            Math.Clamp((int)(emu / slideSize.HeightEmu * surfaceHeight), 0, surfaceHeight - 1);

        static int Luminance(Rgba32 pixel) => pixel.R + pixel.G + pixel.B;

        var bulletGutterPx = ToPixelX(bulletGutterXEmu, slideSize, surface0.Width);
        var beforeGutterPx = ToPixelX(beforeGutterXEmu, slideSize, surface0.Width);
        var topPx = ToPixelY(topYEmu, slideSize, surface0.Height);
        var bottomPx = ToPixelY(bottomYEmu, slideSize, surface0.Height);

        var darkestInGutter = int.MaxValue;
        var darkestBeforeGutter = int.MaxValue;
        for (var py = topPx; py <= bottomPx; py++)
        {
            darkestInGutter = Math.Min(darkestInGutter, Luminance(surface0[bulletGutterPx, py]));
            darkestBeforeGutter = Math.Min(darkestBeforeGutter, Luminance(surface0[beforeGutterPx, py]));
        }

        Assert.True(darkestInGutter < darkestBeforeGutter - 60,
            $"Expected the bullet-gutter column (x={bulletGutterPx}) to contain visibly darker ink than the shape's own left-inset column (x={beforeGutterPx}) just to its left: darkestInGutter={darkestInGutter}, darkestBeforeGutter={darkestBeforeGutter}.");

        // Act & Assert (stray-bullet-on-sldNum regression guard - see the companion quality report
        // this cycle closes): the slide's own "Slide Number Placeholder 3" (<p:ph type="sldNum"
        // idx="10"/>) has no own geometry, inheriting the slide master's "Slide Number
        // Placeholder 5" (idx="4") geometry by type-match: <a:off x="11669529" y="6400800"/>,
        // <a:ext cx="438912" cy="155448"/>, this fixture's slide size cx="12188825" cy="6858000".
        // Before the fix, this placeholder's paragraph (a bare <a:fld type="slidenum">, no own
        // bullet markup anywhere in its own chain) wrongly inherited the master's bodyStyle
        // lvl1pPr's "<a:buChar char=\u2022/>" bullet (SelectMasterTextStyle routes "sldNum" to
        // bodyStyle instead of the bullet-free otherStyle), painting an isolated bullet dot with
        // no accompanying text - independently confirmed via a standalone scratch render at 192
        // DPI (darkest pixel luminance 0 before the fix, 765/pure-background after). A baseline
        // column just above the placeholder's own bounding box (same slide background, outside any
        // shape) proves the comparison isn't simply "both happen to be white".
        const double sldNumOffXEmu = 11669529d;
        const double sldNumOffYEmu = 6400800d;
        const double sldNumCxEmu = 438912d;
        const double sldNumCyEmu = 155448d;

        var sldNumX0 = ToPixelX(sldNumOffXEmu, slideSize, surface0.Width);
        var sldNumX1 = ToPixelX(sldNumOffXEmu + sldNumCxEmu, slideSize, surface0.Width);
        var sldNumY0 = ToPixelY(sldNumOffYEmu, slideSize, surface0.Height);
        var sldNumY1 = ToPixelY(sldNumOffYEmu + sldNumCyEmu, slideSize, surface0.Height);
        var sldNumBaselineY = ToPixelY(sldNumOffYEmu - sldNumCyEmu, slideSize, surface0.Height);

        var darkestAtSldNum = int.MaxValue;
        var darkestAtSldNumBaseline = int.MaxValue;
        for (var px = sldNumX0; px <= sldNumX1; px++)
        {
            for (var py = sldNumY0; py <= sldNumY1; py++)
            {
                darkestAtSldNum = Math.Min(darkestAtSldNum, Luminance(surface0[px, py]));
            }

            darkestAtSldNumBaseline = Math.Min(darkestAtSldNumBaseline, Luminance(surface0[px, sldNumBaselineY]));
        }

        Assert.True(darkestAtSldNum >= darkestAtSldNumBaseline - 60,
            $"Expected no stray bullet ink at the slide-number placeholder's own geometry (darkest luminance {darkestAtSldNum}), no meaningfully darker than its own background baseline row (darkest luminance {darkestAtSldNumBaseline}).");

        // Act & Assert: slide 1's chart graphic frame throws.
        var exception = Assert.Throws<PptxUnsupportedFeatureException>(() => document.Render(1, Dpi, Transparent));
        Assert.Equal("pptx-graphic-frame-kind", exception.Feature);
    }

    /// <summary>
    ///     Proves <c>aiden0z-image-crop-css-reset.pptx</c> (a single slide from
    ///     <c>aiden0z/pptx-renderer</c>'s own example corpus) renders and paints visible content.
    ///     The slide declares a <c>&lt;p:bg&gt;</c> solid <c>FAFAF9</c> fill, now painted (a
    ///     pixel-level real-file regression test for the Phase 2 Follow-Up background-fill
    ///     feature - see <see cref="PptxDocument_Render_SldBackgroundFixture_RendersAndPaintsSlideBackgroundFill"/>'s
    ///     own remarks for the companion <c>pythonpptx-sld-background.pptx</c> fixture) beneath
    ///     four embedded raster pictures, three with distinct <c>&lt;a:srcRect&gt;</c> crop
    ///     rectangles - new crop-rectangle variety not covered by any existing fixture.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_Aiden0zImageCropCssResetFixture_PaintsVisibleContentAndBackgroundFill()
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture("aiden0z-image-crop-css-reset.pptx"));

        // Assert
        Assert.Equal(1, document.SlideCount);
        using var surface = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface);
        Assert.Equal(new Rgba32(0xFA, 0xFA, 0xF9, 255), surface[0, 0]);
    }

    /// <summary>
    ///     Proves <c>aiden0z-table-stale-frame.pptx</c> (a single slide from
    ///     <c>aiden0z/pptx-renderer</c>'s own example corpus) renders and paints visible content
    ///     despite a real-world authoring artifact: its one <c>&lt;a:tbl&gt;</c> graphic frame's
    ///     declared <c>&lt;p:xfrm&gt;</c> extent does not match the sum of its own
    ///     <c>&lt;a:gridCol&gt;</c> widths. Table column/row sizing is derived entirely from
    ///     <c>&lt;a:tblGrid&gt;</c>/<c>&lt;a:tr h&gt;</c>, independent of the graphic frame's own
    ///     declared extent, so this "stale frame size" mismatch renders and paints without
    ///     exception - a genuine, confirmed-graceful real-world edge case, not a bug.
    /// </summary>
    [Fact]
    public void PptxDocument_Render_Aiden0zTableStaleFrameFixture_PaintsVisibleContentDespiteFrameSizeMismatch()
    {
        // Arrange & Act
        using var document = PptxDocument.Open(Fixture("aiden0z-table-stale-frame.pptx"));

        // Assert
        Assert.Equal(1, document.SlideCount);
        using var surface = document.Render(0, Dpi, Transparent);
        AssertPaintedSomePixel(surface);
    }
}
