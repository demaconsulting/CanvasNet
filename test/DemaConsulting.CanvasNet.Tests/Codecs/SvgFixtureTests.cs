using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="SvgCodec"/> against the real-file SVG fixture corpus
///     in <c>SvgFixtures</c> (see <c>SvgFixtures\README.md</c> for provenance - most fixtures are
///     hand-authored for this repository), mirroring the pattern used by
///     <c>TiffFixtureTests</c>/<c>JpegFixtureTests</c>. Includes one real-font integration test
///     that reuses <c>FontFixtures\OpenSans-Regular.ttf</c>, mirroring
///     <c>TrueTypeFontRealFontIntegrationTests</c>, and two real-world, third-party,
///     Wikimedia-Commons-sourced fixtures (<c>SvgGradient.svg</c>/<c>InkscapeFilters.svg</c>; see
///     <c>SvgFixtures\WikimediaCommons.LICENSE</c> for provenance/licensing).
/// </summary>
public class SvgFixtureTests
{
    /// <summary>
    ///     The directory containing the SVG fixture corpus, copied to the test output directory
    ///     by the test project's <c>SvgFixtures\**</c> content item.
    /// </summary>
    private static string AssetsPath => Path.Combine(AppContext.BaseDirectory, "SvgFixtures");

    /// <summary>
    ///     The path to the real "Open Sans" TrueType font, copied to the test output directory by
    ///     the test project's <c>FontFixtures\**</c> content item.
    /// </summary>
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "FontFixtures", "OpenSans-Regular.ttf");

    /// <summary>
    ///     Proves that <c>shapes.svg</c>'s four basic shapes (rect/circle/ellipse/polygon) each
    ///     rasterize their expected solid fill color at a representative interior pixel, while a
    ///     point covered by none of them remains fully transparent.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ShapesFixture_RendersExpectedShapeColors()
    {
        // Arrange & Act: load the fixture at its native 1:1 viewBox-to-raster size
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "shapes.svg"), 100, 100);

        // Assert: each shape's expected solid color appears at a representative interior pixel
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 25]); // rect (red)
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[75, 25]); // circle (green)
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[25, 75]); // ellipse (blue)
        Assert.Equal(new Rgba32(255, 255, 0, 255), surface[75, 75]); // polygon (yellow)

        // Assert: a point covered by none of the four shapes remains fully transparent
        Assert.Equal(0, surface[95, 5].A);
    }

    /// <summary>
    ///     Proves that <c>groups-and-transforms.svg</c>'s nested group fill inheritance and
    ///     combined <c>translate</c>/<c>rotate</c> transform functions both bake correctly into
    ///     final pixel-space geometry - the first rectangle only appears at its translated
    ///     position (not its pre-translation local position), and the second rectangle only
    ///     appears within its rotated-then-translated bounding region.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupsAndTransformsFixture_RendersAtTransformedPositions()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "groups-and-transforms.svg"), 100, 100);

        // Assert: the first rect inherits its group's "orange" fill and renders at its
        // translate(50,0)-mapped position, not its pre-translation local (0,0)-(20,20) position
        Assert.Equal(new Rgba32(255, 165, 0, 255), surface[60, 10]);
        Assert.Equal(0, surface[10, 10].A);

        // Assert: the second rect's local (0,0)-(30,10) region, rotated 90 degrees then
        // translated by (20,60), maps to final pixel region x:[10,20], y:[60,90]
        Assert.Equal(new Rgba32(128, 0, 128, 255), surface[15, 75]);
        Assert.Equal(0, surface[5, 75].A);
    }

    /// <summary>
    ///     Proves that <c>gradient.svg</c>'s <c>linearGradient</c> (referenced via
    ///     <c>fill="url(#id)"</c>) actually varies across the filled rectangle - a monotonically
    ///     increasing red channel from the black stop at the left edge to the white stop at the
    ///     right edge - rather than degrading to a single flat color.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientFixture_RendersVaryingGradientColors()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "gradient.svg"), 100, 100);

        // Assert: the red channel increases monotonically from left to right, and is fully
        // opaque throughout - proving a real gradient (not a flat fallback color) was rendered
        var left = surface[5, 50];
        var middle = surface[50, 50];
        var right = surface[95, 50];
        Assert.True(left.R < middle.R, $"Expected left ({left.R}) < middle ({middle.R}).");
        Assert.True(middle.R < right.R, $"Expected middle ({middle.R}) < right ({right.R}).");
        Assert.Equal(255, left.A);
        Assert.Equal(255, right.A);
    }

    /// <summary>
    ///     Proves that <c>use-reference.svg</c>'s <c>use</c> element renders its <c>defs</c>-only
    ///     template at the <c>use</c> element's own <c>x</c>/<c>y</c> offset, and that the
    ///     template itself never renders directly at its own local position (since <c>defs</c>
    ///     content is only reachable via a reference).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseReferenceFixture_RendersAtOffsetPositionOnly()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "use-reference.svg"), 100, 100);

        // Assert: the referenced 20x20 "teal" box renders at its use-offset (30,40) position
        Assert.Equal(new Rgba32(0, 128, 128, 255), surface[40, 50]);

        // Assert: the box's own local (pre-offset) position, and a far corner, remain transparent
        Assert.Equal(0, surface[5, 5].A);
        Assert.Equal(0, surface[95, 95].A);
    }

    /// <summary>
    ///     Proves that <c>tolerant-unsupported.svg</c>'s well-formed but out-of-scope
    ///     <c>filter</c> element definition does not prevent the rest of the document (an
    ///     ordinary <c>rect</c>) from rendering normally.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ToleratesUnsupportedConstructFixture_StillRendersRemainingContent()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "tolerant-unsupported.svg"), 100, 100);

        // Assert: the rect still renders its "lime" fill despite the sibling <filter> element
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>
    ///     Proves that a real glyph, loaded from the real "Open Sans" production font and
    ///     rendered through <c>text.svg</c>'s <c>text</c> element, paints genuine, non-vacuous ink
    ///     within the text's expected region while the canvas's far corners remain fully
    ///     transparent - not merely "no exception was thrown" (mirrors
    ///     <c>TrueTypeFontRealFontIntegrationTests</c>'s pattern applied end to end through
    ///     <see cref="SvgCodec"/>).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFixtureWithRealFont_RendersVisibleGlyphInk()
    {
        // Arrange: load the real production font and supply it under the family name the
        // fixture's font-family attribute references
        var font = TrueTypeFont.Load(FontPath);
        var fonts = new Dictionary<string, TrueTypeFont> { ["Open Sans"] = font };

        // Act: rasterize the fixture with the font dictionary supplied
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "text.svg"), 200, 80, fonts);

        // Assert: at least one pixel within the text's expected region (below and around the
        // baseline at x=10, y=55, font-size 40) was actually painted (non-transparent)
        var foundInk = false;
        for (var y = 15; y < 70 && !foundInk; y++)
        {
            for (var x = 5; x < 90; x++)
            {
                if (surface[x, y].A > 0)
                {
                    foundInk = true;
                    break;
                }
            }
        }

        Assert.True(foundInk, "Expected at least one filled (non-transparent) pixel within the text's expected region.");

        // Assert: the canvas's far corners, well outside the text, remain fully transparent
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[199, 0].A);
        Assert.Equal(0, surface[0, 79].A);
        Assert.Equal(0, surface[199, 79].A);
    }

    /// <summary>
    ///     Proves that the real, unmodified, Wikimedia-Commons-sourced <c>SvgGradient.svg</c>
    ///     (see <c>SvgFixtures\WikimediaCommons.LICENSE</c> for provenance) renders its
    ///     <c>userSpaceOnUse</c> <c>linearGradient</c> bar as genuinely varying pixel colors
    ///     (bright near its white stop, dark near its black stop), rather than degrading to a
    ///     single flat color, and that its <c>fill="pink"</c> background rectangle is visible at
    ///     a point clearly outside every gradient bar and <c>use</c> shape. A regressed
    ///     <c>userSpaceOnUse</c> gradient-unit handling (for example, mistakenly treating the
    ///     gradient's <c>x1</c>/<c>x2</c> user-space coordinates as fractional
    ///     <c>objectBoundingBox</c> offsets) would clamp most of the bar to a single extreme
    ///     stop color, and a background-color-name or basic-rect regression would leave the
    ///     background pixel transparent or black instead of pink.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_SvgGradientFixture_RendersVaryingGradientAndPinkBackground()
    {
        // Arrange & Act: rasterize at the fixture's native 300x200 viewBox size
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "SvgGradient.svg"), 300, 200);

        // Assert: the pink background rect is visible at a point clearly outside the "bar" use
        // shape (y in [80,100]) and every gradient rect (rows at y in [30,70] and [110,190])
        Assert.Equal(new Rgba32(255, 192, 203, 255), surface[5, 5]);

        // Assert: the leftmost gradient bar (translate(50,50), the fixture's userSpaceOnUse
        // gradient) is bright near its x1=-40 (offset 0, white) end and much darker near its
        // offset=0.9 (black) stop further along the bar - a genuine, non-flat gradient
        var brightEnd = surface[15, 50];
        var darkEnd = surface[78, 50];
        Assert.True(brightEnd.R > 200, $"Expected bright end red channel > 200, got {brightEnd.R}.");
        Assert.True(darkEnd.R < 60, $"Expected dark end red channel < 60, got {darkEnd.R}.");
        Assert.True(
            brightEnd.R - darkEnd.R > 140,
            $"Expected a large red-channel drop from bright end ({brightEnd.R}) to dark end ({darkEnd.R}).");
        Assert.Equal(255, brightEnd.A);
        Assert.Equal(255, darkEnd.A);
    }

    /// <summary>
    ///     Proves that the real, unmodified, Wikimedia-Commons-sourced
    ///     <c>InkscapeFilters.svg</c> (see <c>SvgFixtures\WikimediaCommons.LICENSE</c> for
    ///     provenance) - a large, complex document built entirely from <c>defs</c>/<c>use</c>
    ///     templating, composed <c>transform</c> functions, and dozens of <c>filter="url(#...)"</c>
    ///     references, all of which reference out-of-scope <c>feGaussianBlur</c>/
    ///     <c>feComposite</c>/<c>feSpecularLighting</c> effects - loads without throwing despite
    ///     the numerous unsupported filter references (proving the tolerant-ignore policy holds
    ///     for a real, unmodified, complex third-party document, not only a small synthetic one),
    ///     that a flower shape rendered behind one such <c>filter</c> reference still paints its
    ///     genuine fill color (proving real content rendered, not just "didn't crash"), and that
    ///     a point in the gap between flowers remains transparent (proving the render is not a
    ///     degenerate whole-canvas fill that would make the previous assertion vacuous).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent()
    {
        // Arrange & Act: rasterize at the fixture's native 600x1000 width/height - this must not
        // throw despite every flower (other than the very first) referencing an unsupported
        // <filter> element
        var surface = SvgCodec.Load(Path.Combine(AssetsPath, "InkscapeFilters.svg"), 600, 1000);

        // Assert: a petal of the second flower - translate(150,50), filter="url(#filter48)" -
        // still renders its own "#ff8010" (255,128,16) fill, proving the unsupported <filter>
        // reference was tolerantly ignored rather than suppressing the shape it decorates
        Assert.Equal(new Rgba32(255, 128, 16, 255), surface[135, 30]);

        // Assert: the gap between flowers (the grid spacing is 100 units, and each flower's
        // petals only reach roughly 36 units from its own center) remains fully transparent -
        // proving the render did not degenerate into filling the whole canvas with one color
        Assert.Equal(0, surface[100, 50].A);
        Assert.Equal(0, surface[50, 100].A);
    }
}
