using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     System-level integration tests for SVG rasterization via the CanvasNet.Svg package.
/// </summary>
public class SvgSystemIntegrationTests
{
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
    ///     Proves that the system can rasterize an SVG document whose color comes entirely from a
    ///     <c>&lt;style&gt;</c> element's CSS class selector - rather than a plain presentation
    ///     attribute - through the public API, integrating the whole CSS engine (style-element
    ///     parsing, selector matching, and the cascade chokepoint) with the rest of the rendering
    ///     pipeline. See <c>SvgCodecTests</c>/<c>SvgFixtureTests</c> for the full unit test
    ///     coverage of individual selectors/combinators/precedence tiers.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_SvgLoadWithCssStyleElement_ReturnsExpectedPixel()
    {
        // Arrange: a minimal SVG document whose only source of color is a stylesheet class rule -
        // the rect itself carries no fill attribute at all
        const string svg = "<svg viewBox='0 0 10 10'><style>.solid { fill: rgb(11,22,33); }</style>" +
                            "<rect x='0' y='0' width='10' height='10' class='solid'/></svg>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));

        // Act: rasterize the document onto a new Surface through the public API
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the system produces the expected integrated rasterized pixel value
        Assert.Equal(new Rgba32(11, 22, 33, 255), surface[5, 5]);
    }
}
