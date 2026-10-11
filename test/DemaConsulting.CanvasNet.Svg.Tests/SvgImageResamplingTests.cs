using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests that SVG <c>&lt;image&gt;</c> elements are smoothly resampled and that the
///     <c>image-rendering</c> property selects nearest-neighbor sampling. Timing is never asserted.
/// </summary>
public class SvgImageResamplingTests
{
    /// <summary>Builds a 2x1 black/white PNG data URI.</summary>
    private static string TwoPixelPng()
    {
        using var source = new Surface(2, 1);
        source[0, 0] = new Rgba32(0, 0, 0, 255);
        source[1, 0] = new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        PngCodec.Save(source, stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>Renders a 2x1 image enlarged to 40x4 using the given extra markup.</summary>
    private static Surface Render(string imageAttributes, string styleBlock = "")
    {
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 4'>{styleBlock}" +
                  $"<image href='{TwoPixelPng()}' x='0' y='0' width='40' height='4' preserveAspectRatio='none' {imageAttributes}/></svg>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return SvgCodec.Load(stream, 40, 4);
    }

    /// <summary>Counts distinct gray levels in the middle row.</summary>
    private static int DistinctLevels(Surface surface) =>
        Enumerable.Range(0, surface.Width).Select(x => (int)surface[x, 2].R).Distinct().Count();

    /// <summary>By default an enlarged image is smoothly interpolated.</summary>
    [Fact]
    public void Image_Enlarged_IsSmooth()
    {
        using var surface = Render(string.Empty);
        Assert.True(DistinctLevels(surface) > 8);
    }

    /// <summary>image-rendering as an attribute selects nearest-neighbor.</summary>
    [Theory]
    [InlineData("pixelated")]
    [InlineData("crisp-edges")]
    public void Image_RenderingAttribute_IsNearest(string value)
    {
        using var surface = Render($"image-rendering='{value}'");
        Assert.True(DistinctLevels(surface) <= 2);
        Assert.Equal(0, surface[5, 2].R);
        Assert.Equal(255, surface[35, 2].R);
    }

    /// <summary>image-rendering in a style attribute selects nearest-neighbor.</summary>
    [Fact]
    public void Image_RenderingStyle_IsNearest()
    {
        using var surface = Render("style='image-rendering:pixelated'");
        Assert.True(DistinctLevels(surface) <= 2);
    }

    /// <summary>image-rendering from a stylesheet selects nearest-neighbor.</summary>
    [Fact]
    public void Image_RenderingStylesheet_IsNearest()
    {
        using var surface = Render("class='px'", "<style>.px { image-rendering: pixelated; }</style>");
        Assert.True(DistinctLevels(surface) <= 2);
    }

    /// <summary>A shrunken image keeps a one-pixel line instead of dropping it.</summary>
    [Fact]
    public void Image_Shrunk_KeepsOnePixelLine()
    {
        using var source = new Surface(100, 1);
        source.Clear(new Rgba32(255, 255, 255, 255));
        source[37, 0] = new Rgba32(0, 0, 0, 255);
        using var stream = new MemoryStream();
        PngCodec.Save(source, stream);
        var uri = "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 17 1'><image href='{uri}' x='0' y='0' width='17' height='1' preserveAspectRatio='none'/></svg>";
        using var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var surface = SvgCodec.Load(svgStream, 17, 1);

        double ink = 0;
        for (var x = 0; x < 17; x++)
        {
            ink += 255 - surface[x, 0].R;
        }

        Assert.InRange(ink, 255 * 0.17 * 0.8, 255 * 0.17 * 1.2 + 1);
    }

    /// <summary>Renders the enlarged image inside a group with the given group attributes and image attributes.</summary>
    private static Surface RenderInGroup(string groupAttributes, string imageAttributes)
    {
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 4'><g {groupAttributes}>" +
                  $"<image href='{TwoPixelPng()}' x='0' y='0' width='40' height='4' preserveAspectRatio='none' {imageAttributes}/></g></svg>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return SvgCodec.Load(stream, 40, 4);
    }

    /// <summary>image-rendering is inherited from a parent group.</summary>
    [Fact]
    public void Image_RenderingInheritedFromGroup_IsNearest()
    {
        using var surface = RenderInGroup("image-rendering='pixelated'", string.Empty);
        Assert.True(DistinctLevels(surface) <= 2);
    }

    /// <summary>An unrecognized image-rendering value is ignored and the parent's pixelated value is inherited.</summary>
    [Fact]
    public void Image_RenderingBogus_InheritsPixelatedFromParent()
    {
        using var surface = RenderInGroup("image-rendering='pixelated'", "image-rendering='bogus'");
        Assert.True(DistinctLevels(surface) <= 2);
    }

    /// <summary>An explicit smooth keyword overrides a pixelated parent.</summary>
    [Theory]
    [InlineData("auto")]
    [InlineData("optimizeQuality")]
    [InlineData("smooth")]
    [InlineData("high-quality")]
    public void Image_RenderingSmoothKeyword_OverridesPixelatedParent(string value)
    {
        using var surface = RenderInGroup("image-rendering='pixelated'", $"image-rendering='{value}'");
        Assert.True(DistinctLevels(surface) > 8);
    }
}
