using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the CSS color parser.
/// </summary>
public class CssColorTests
{
    /// <summary>
    ///     Validates every supported form against the pixel value it must produce.
    /// </summary>
    [Fact]
    public void CssColor_Parse_SupportedFormsMatchRgba32()
    {
        // Arrange: text and the straight-alpha pixel that CSS defines for it
        var expected = new (string Text, Rgba32 Color)[]
        {
            // Hexadecimal, short and long, with and without alpha, any letter case
            ("#4c78a8", new Rgba32(76, 120, 168, 255)),
            ("#abc", new Rgba32(170, 187, 204, 255)),
            ("#abcd", new Rgba32(170, 187, 204, 221)),
            ("#4c78a8cc", new Rgba32(76, 120, 168, 204)),
            ("#FFF", new Rgba32(255, 255, 255, 255)),
            ("#00000000", new Rgba32(0, 0, 0, 0)),
            ("  #ddd  ", new Rgba32(221, 221, 221, 255)),

            // Named colors in any letter case, and transparent
            ("steelblue", new Rgba32(70, 130, 180, 255)),
            ("RebeccaPurple", new Rgba32(102, 51, 153, 255)),
            ("grey", new Rgba32(128, 128, 128, 255)),
            ("gray", new Rgba32(128, 128, 128, 255)),
            ("white", new Rgba32(255, 255, 255, 255)),
            ("black", new Rgba32(0, 0, 0, 255)),
            ("transparent", new Rgba32(0, 0, 0, 0)),

            // rgb()/rgba(): integers, percentages (25.5 rounds up), alpha as number or percentage, comma or space syntax
            ("rgb(10, 20, 30)", new Rgba32(10, 20, 30, 255)),
            ("rgba(255,0,0,0.5)", new Rgba32(255, 0, 0, 128)),
            ("rgb(10% 20% 30% / 50%)", new Rgba32(26, 51, 77, 128)),
            ("rgb(10 20 30 / 0.2)", new Rgba32(10, 20, 30, 51)),
            ("RGB(0, 128.4, 255)", new Rgba32(0, 128, 255, 255)),
            ("rgb(300, -5, 0)", new Rgba32(255, 0, 0, 255)),
            ("rgba(1, 2, 3, 7)", new Rgba32(1, 2, 3, 255)),
            ("rgba(1, 2, 3, -1)", new Rgba32(1, 2, 3, 0)),

            // hsl()/hsla(): hue units, percentages, alpha
            ("hsl(0, 100%, 50%)", new Rgba32(255, 0, 0, 255)),
            ("hsl(120 100% 25%)", new Rgba32(0, 128, 0, 255)),
            ("hsla(240, 100%, 50%, 0.25)", new Rgba32(0, 0, 255, 64)),
            ("hsl(0.5turn 100% 50%)", new Rgba32(0, 255, 255, 255)),
            ("hsl(180deg, 100%, 50%)", new Rgba32(0, 255, 255, 255)),
            ("hsl(-120, 100%, 50%)", new Rgba32(0, 0, 255, 255)),
            ("hsl(200 0% 40%)", new Rgba32(102, 102, 102, 255)),
            ("hsl(0, 100%, 100%)", new Rgba32(255, 255, 255, 255)),
        };

        // Act / Assert
        foreach (var (text, color) in expected)
        {
            Assert.True(CssColor.TryParse(text, out var parsed), text);
            Assert.Equal(color, parsed);
            Assert.Equal(color, CssColor.Parse(text));
        }
    }

    /// <summary>
    ///     Validates that the table of named colors is complete and agrees with hexadecimal spellings of the same values.
    /// </summary>
    [Fact]
    public void CssColor_Parse_NamedColorsAreComplete()
    {
        // Arrange: a spot check across the alphabet of values taken from the CSS Color specification
        var spot = new (string Name, string Hex)[]
        {
            ("aliceblue", "#f0f8ff"), ("aqua", "#00ffff"), ("crimson", "#dc143c"), ("darkslategrey", "#2f4f4f"),
            ("dodgerblue", "#1e90ff"), ("gold", "#ffd700"), ("lightgoldenrodyellow", "#fafad2"), ("lime", "#00ff00"),
            ("mediumvioletred", "#c71585"), ("orange", "#ffa500"), ("peachpuff", "#ffdab9"), ("salmon", "#fa8072"),
            ("tomato", "#ff6347"), ("whitesmoke", "#f5f5f5"), ("yellowgreen", "#9acd32"),
        };

        // Act / Assert: 148 names (the 147 of CSS Color 3 plus rebeccapurple), and the spot values
        Assert.Equal(148, CssColor.NamedColorCount);
        foreach (var (name, hex) in spot)
        {
            Assert.Equal(CssColor.Parse(hex), CssColor.Parse(name));
        }
    }

    /// <summary>
    ///     Validates that bad text fails cleanly: false from the try method, a format exception from the strict one.
    /// </summary>
    [Fact]
    public void CssColor_Parse_InvalidTextFailsCleanly()
    {
        // Arrange
        string?[] invalid =
        [
            null, string.Empty, "   ", "none", "None", "#", "#12", "#12345", "#1234567", "#ggg", "#12 456", "bogus", "rgb", "rgb()",
            "rgb(1,2)", "rgb(1,2,3,4,5)", "rgb(1 2 3 4 5 6)", "rgb(1 2 3 / )", "rgb(1, 2, 3", "rgb(NaN, 0, 0)", "rgb(1e999, 0, 0)",
            "rgb(a, b, c)", "rgba(1,2,3,x)", "hsl(a, 100%, 50%)", "hsl(0, 100%)", "xyz(1,2,3)", "rgb(1,2,3) extra", "(1,2,3)",
        ];

        // Act / Assert
        foreach (var text in invalid)
        {
            Assert.False(CssColor.TryParse(text, out var color), text ?? "null");
            Assert.Equal(default, color);
        }

        Assert.Throws<FormatException>(() => CssColor.Parse("bogus"));
        Assert.Throws<FormatException>(() => CssColor.Parse(null));
    }

    /// <summary>
    ///     Validates the paint rules: none, opacity folding and the CSS round trip.
    /// </summary>
    [Fact]
    public void CssColor_ResolvePaint_NoneOpacityAndRoundTrip()
    {
        // Arrange / Act / Assert: no paint
        Assert.True(CssColor.IsNone(null));
        Assert.True(CssColor.IsNone(""));
        Assert.True(CssColor.IsNone(" NONE "));
        Assert.False(CssColor.IsNone("transparent"));
        Assert.False(CssColor.TryResolvePaint(null, 1, out _));
        Assert.False(CssColor.TryResolvePaint("none", 1, out _));
        Assert.False(CssColor.TryResolvePaint("garbage", 1, out _));
        Assert.False(CssColor.TryResolvePaint("transparent", 1, out _));
        Assert.False(CssColor.TryResolvePaint("#000", 0, out _));

        // Assert: the opacity multiplies the alpha of the color, is clamped to 0..1 and NaN counts as 1
        Assert.True(CssColor.TryResolvePaint("#000", 0.5, out var half));
        Assert.Equal(new Rgba32(0, 0, 0, 128), half);
        Assert.True(CssColor.TryResolvePaint("rgba(0,0,0,0.5)", 0.5, out var quarter));
        Assert.Equal(new Rgba32(0, 0, 0, 64), quarter);
        Assert.True(CssColor.TryResolvePaint("#123456", 7, out var over));
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 255), over);
        Assert.Equal(new Rgba32(1, 2, 3, 255), CssColor.WithOpacity(new Rgba32(1, 2, 3, 255), double.NaN));

        // Assert: ToCss gives the shortest exact hexadecimal form that parses back to the same color
        Assert.Equal("#4c78a8", CssColor.ToCss(new Rgba32(76, 120, 168, 255)));
        Assert.Equal("#4c78a880", CssColor.ToCss(new Rgba32(76, 120, 168, 128)));
        foreach (var color in new[] { new Rgba32(0, 0, 0, 0), new Rgba32(1, 2, 3, 4), new Rgba32(255, 255, 255, 255) })
        {
            Assert.Equal(color, CssColor.Parse(CssColor.ToCss(color)));
        }
    }
}
