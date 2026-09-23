using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="SvgCodec"/> against the real-file SVG fixture corpus
///     in <c>SvgFixtures</c> (see <c>SvgFixtures\README.md</c> for provenance - every fixture is
///     hand-authored for this repository), mirroring the pattern used by
///     <c>TiffFixtureTests</c>/<c>JpegFixtureTests</c>. Includes one real-font integration test
///     that reuses <c>FontFixtures\OpenSans-Regular.ttf</c>, mirroring
///     <c>TrueTypeFontRealFontIntegrationTests</c>.
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
}
