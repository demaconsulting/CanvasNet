using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the chart paint rules that sit on top of the shared CSS color parser.
/// </summary>
public class CssPaintTests
{
    /// <summary>
    ///     Validates which paints mean "do not draw".
    /// </summary>
    [Fact]
    public void CssPaint_IsNone_NullEmptyAndNone_AreNoPaint()
    {
        // Arrange / Act / Assert: null, empty, blank and none in any case are no paint; transparent is a color
        Assert.True(CssPaint.IsNone(null));
        Assert.True(CssPaint.IsNone(""));
        Assert.True(CssPaint.IsNone("   "));
        Assert.True(CssPaint.IsNone(" NONE "));
        Assert.False(CssPaint.IsNone("transparent"));
        Assert.False(CssPaint.IsNone("#000"));
    }

    /// <summary>
    ///     Validates that no paint, an unparseable paint, and a fully transparent result resolve to nothing.
    /// </summary>
    [Fact]
    public void CssPaint_TryResolvePaint_NothingToDraw_ReturnsFalseWithDefaultColor()
    {
        // Arrange / Act / Assert
        foreach (var (paint, opacity) in new (string?, double)[]
        {
            (null, 1), ("none", 1), ("garbage", 1), ("transparent", 1), ("#000", 0), ("rgba(0,0,0,0)", 1),
        })
        {
            Assert.False(CssPaint.TryResolvePaint(paint, opacity, out var color), paint ?? "null");
            Assert.Equal(default, color);
        }
    }

    /// <summary>
    ///     Validates that the opacity multiplies the alpha of the color, is clamped to 0 to 1, and that NaN counts as 1.
    /// </summary>
    [Fact]
    public void CssPaint_TryResolvePaint_Opacity_MultipliesTheAlphaAndRoundsHalfAwayFromZero()
    {
        // Arrange / Act / Assert: 0.5 x 255 = 127.5 rounds up, 0.25 x 255 = 63.75 rounds up, above 1 stays opaque
        Assert.True(CssPaint.TryResolvePaint("#000", 0.5, out var half));
        Assert.Equal(new Rgba32(0, 0, 0, 128), half);
        Assert.True(CssPaint.TryResolvePaint("rgba(0,0,0,0.5)", 0.5, out var quarter));
        Assert.Equal(new Rgba32(0, 0, 0, 64), quarter);
        Assert.True(CssPaint.TryResolvePaint("#123456", 7, out var over));
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 255), over);
        Assert.Equal(new Rgba32(1, 2, 3, 255), CssPaint.WithOpacity(new Rgba32(1, 2, 3, 255), double.NaN));
        Assert.Equal(new Rgba32(1, 2, 3, 0), CssPaint.WithOpacity(new Rgba32(1, 2, 3, 255), -3));
    }

    /// <summary>
    ///     Validates that paints are parsed with the shared CSS color grammar of the core library.
    /// </summary>
    [Fact]
    public void CssPaint_TryResolvePaint_ColorText_UsesTheSharedCssGrammar()
    {
        // Arrange / Act / Assert: a name, a short hexadecimal with alpha, and the hsl() function
        Assert.True(CssPaint.TryResolvePaint("steelblue", 1, out var named));
        Assert.Equal(new Rgba32(70, 130, 180, 255), named);
        Assert.True(CssPaint.TryResolvePaint("#abcd", 1, out var shortHex));
        Assert.Equal(new Rgba32(170, 187, 204, 221), shortHex);
        Assert.True(CssPaint.TryResolvePaint("hsl(120 100% 25%)", 1, out var hsl));
        Assert.Equal(new Rgba32(0, 128, 0, 255), hsl);
    }
}
