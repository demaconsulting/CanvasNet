using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the GridOverlay class: lines, blending, labels, and counts.
/// </summary>
public class GridOverlayTests
{
    /// <summary>The opaque mid-gray the backing tests draw on.</summary>
    private static readonly Rgba32 Gray = new(128, 128, 128, 255);

    /// <summary>
    ///     Creates a surface filled with one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            surface.GetRowSpan(y).Fill(color);
        }

        return surface;
    }

    /// <summary>
    ///     Verifies that lines are one pixel wide at the multiples of the spacing.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_Spacing_DrawsOnePixelLinesAtMultiples()
    {
        var red = new Rgba32(255, 0, 0, 255);
        using var surface = Solid(100, 100, new Rgba32(255, 255, 255, 255));

        var result = GridOverlay.Draw(surface, 32, red);

        Assert.Equal(4, result.VerticalLines);
        Assert.Equal(4, result.HorizontalLines);
        Assert.Equal(red, surface[32, 60]);
        Assert.Equal(red, surface[96, 60]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[31, 60]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[33, 60]);
        Assert.Equal(red, surface[50, 64]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[50, 63]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[50, 65]);
        Assert.Equal(red, surface[50, 0]);
        Assert.Equal(red, surface[0, 80]);
    }

    /// <summary>
    ///     Verifies that a pixel where two lines cross is blended once, not twice.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_Intersections_AreBlendedOnce()
    {
        using var surface = Solid(100, 100, new Rgba32(255, 255, 255, 255));

        GridOverlay.Draw(surface, 32, new Rgba32(0, 0, 0, 128));

        // (0 * 128 + 255 * 127 + 127) / 255 = 127 with integer division.
        var once = new Rgba32(127, 127, 127, 255);
        Assert.Equal(once, surface[32, 60]);
        Assert.Equal(once, surface[60, 32]);
        Assert.Equal(once, surface[64, 64]);
    }

    /// <summary>
    ///     Verifies that an opaque line color replaces the pixels, whatever they were.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_OpaqueColor_ReplacesPixels()
    {
        var blue = new Rgba32(0, 0, 255, 255);
        using var surface = Solid(100, 100, new Rgba32(0, 0, 0, 0));

        GridOverlay.Draw(surface, 50, blue);

        Assert.Equal(blue, surface[50, 20]);
        Assert.Equal(blue, surface[20, 50]);
        Assert.Equal(blue, surface[50, 50]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[49, 20]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[20, 51]);
    }

    /// <summary>
    ///     Verifies the integer source-over formula over an opaque pixel.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_TranslucentColorOverOpaque_UsesIntegerSourceOver()
    {
        using var surface = Solid(64, 64, new Rgba32(100, 150, 200, 255));

        GridOverlay.Draw(surface, 32, new Rgba32(200, 100, 0, 100));

        // R (200 * 100 + 100 * 155 + 127) / 255 = 139, G (100 * 100 + 150 * 155 + 127) / 255 = 130, B (0 + 200 * 155 + 127) / 255 = 122.
        Assert.Equal(new Rgba32(139, 130, 122, 255), surface[32, 40]);
        Assert.Equal(new Rgba32(100, 150, 200, 255), surface[40, 40]);
    }

    /// <summary>
    ///     Verifies the result alpha and color over transparent and half-transparent pixels.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_TranslucentColorOverTransparent_ComputesResultAlpha()
    {
        using var surface = Solid(64, 64, new Rgba32(0, 0, 0, 0));

        GridOverlay.Draw(surface, 32, new Rgba32(200, 100, 0, 100));

        // outA = (100 * 255 + 0 + 127) / 255 = 100 and the color is the line color.
        Assert.Equal(new Rgba32(200, 100, 0, 100), surface[32, 40]);

        // Over (50, 50, 50, 128): outA = (128 * 255 + 128 * 127 + 127) / 255 = 192.
        var pixel = new Rgba32(50, 50, 50, 128);
        GridOverlay.Blend(ref pixel, new Rgba32(250, 0, 0, 128), 128);
        Assert.Equal(new Rgba32(183, 17, 17, 192), pixel);

        // A result alpha of 0 is transparent black.
        var empty = new Rgba32(9, 9, 9, 0);
        GridOverlay.Blend(ref empty, new Rgba32(1, 2, 3, 0), 0);
        Assert.Equal(new Rgba32(9, 9, 9, 0), empty);
    }

    /// <summary>
    ///     Verifies that the labels are drawn in boxes at the top and left edges.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_Labels_DrawnAtTopAndLeftEdgesInBundledFont()
    {
        using var surface = Solid(200, 200, Gray);

        var result = GridOverlay.Draw(surface, 50, new Rgba32(0, 0, 0, 255));

        // Every vertical line and every horizontal line but the first has a label.
        Assert.Equal(4, result.VerticalLines);
        Assert.Equal(4, result.HorizontalLines);
        Assert.Equal(7, result.Labels);

        // A white backing at alpha 192 over gray 128: (255 * 192 + 128 * 63 + 127) / 255 = 224.
        var backing = new Rgba32(224, 224, 224, 255);
        Assert.Equal(backing, surface[1, 1]);
        Assert.Equal(backing, surface[51, 1]);
        Assert.Equal(backing, surface[1, 51]);

        // The text is dark inside the box; nothing is drawn away from the lines and labels.
        Assert.Contains(Enumerable.Range(1, 14).SelectMany(y => Enumerable.Range(1, 12).Select(x => surface[x + 50, y])), p => p.R < 100);
        Assert.Equal(Gray, surface[155, 155]);
        Assert.Equal(Gray, surface[10, 40]);
    }

    /// <summary>
    ///     Verifies that a small spacing labels only every n-th line so that labels do not overlap.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_SmallSpacing_LabelsOnlyEveryNthLineWithoutOverlap()
    {
        using var surface = Solid(200, 200, Gray);

        var result = GridOverlay.Draw(surface, 8, new Rgba32(0, 0, 0, 255));

        Assert.Equal(25, result.VerticalLines);
        Assert.Equal(25, result.HorizontalLines);

        // The widest label is 3 digits (about 20 pixels): every 4th vertical line is labeled (k = 0, 4, 8, ...), and every
        // 3rd horizontal line (box height 16 + 2 = 18 pixels over 8).
        var backing = new Rgba32(224, 224, 224, 255);
        Assert.Equal(backing, surface[34, 1]);
        Assert.Equal(Gray, surface[20, 2]);
        Assert.Equal(backing, surface[1, 25]);
        Assert.Equal(Gray, surface[3, 20]);
        Assert.Equal(Gray, surface[3, 18]);
        Assert.True(result.Labels < result.VerticalLines + result.HorizontalLines - 1);
    }

    /// <summary>
    ///     Verifies that a label whose box does not fit inside the image is skipped.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_LabelOutsideImage_IsSkipped()
    {
        using var narrow = Solid(100, 100, Gray);
        var result = GridOverlay.Draw(narrow, 96, new Rgba32(0, 0, 0, 255));

        // The vertical label at 96 would end beyond the right edge and the horizontal label at 96 below the bottom edge.
        Assert.Equal(2, result.VerticalLines);
        Assert.Equal(2, result.HorizontalLines);
        Assert.Equal(1, result.Labels);

        using var flat = Solid(30, 10, Gray);
        var none = GridOverlay.Draw(flat, 8, new Rgba32(0, 0, 0, 255));
        Assert.Equal(4, none.VerticalLines);
        Assert.Equal(2, none.HorizontalLines);
        Assert.Equal(0, none.Labels);
        Assert.Equal(Gray, flat[3, 5]);
    }

    /// <summary>
    ///     Verifies that a dark line color gets a light backing and a light line color gets a dark backing.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_DarkAndLightColors_ChooseLightAndDarkBacking()
    {
        using var dark = Solid(100, 100, Gray);
        using var light = Solid(100, 100, Gray);

        GridOverlay.Draw(dark, 50, new Rgba32(0, 0, 0, 255));
        GridOverlay.Draw(light, 50, new Rgba32(255, 255, 255, 255));

        // White backing: (255 * 192 + 128 * 63 + 127) / 255 = 224; black backing: (128 * 63 + 127) / 255 = 32.
        Assert.Equal(new Rgba32(224, 224, 224, 255), dark[1, 1]);
        Assert.Equal(new Rgba32(32, 32, 32, 255), light[1, 1]);
    }

    /// <summary>
    ///     Verifies the counts of lines and labels.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_ReturnsLineAndLabelCounts()
    {
        using var surface = Solid(250, 120, Gray);

        var result = GridOverlay.Draw(surface, 100, new Rgba32(255, 0, 255, 160));

        // Vertical lines at 0, 100, 200; horizontal at 0, 100. Labels: all three vertical, and the horizontal one at 100.
        Assert.Equal(new GridResult(3, 2, 4), result);
    }

    /// <summary>
    ///     Verifies that the same input gives the same pixels.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_SameInputTwice_ProducesIdenticalPixels()
    {
        using var first = Solid(300, 200, new Rgba32(10, 200, 30, 255));
        using var second = Solid(300, 200, new Rgba32(10, 200, 30, 255));

        var one = GridOverlay.Draw(first, 40, GridOverlay.DefaultColor);
        var two = GridOverlay.Draw(second, 40, GridOverlay.DefaultColor);

        Assert.Equal(one, two);
        SurfaceAssert.Equal(first, second);
        Assert.Equal(new Rgba32(255, 0, 255, 160), GridOverlay.DefaultColor);
    }

    /// <summary>
    ///     Verifies that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void GridOverlay_Draw_InvalidArguments_Throw()
    {
        using var surface = Solid(20, 20, Gray);

        Assert.Throws<ArgumentNullException>(() => GridOverlay.Draw(null!, 10, Gray));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridOverlay.Draw(surface, 7, Gray));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridOverlay.Draw(surface, 4097, Gray));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridOverlay.Draw(surface, -1, Gray));
    }
}
