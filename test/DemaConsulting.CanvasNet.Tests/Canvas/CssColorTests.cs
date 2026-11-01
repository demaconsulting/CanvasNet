using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the shared CSS color grammar of <see cref="CssColor"/>: hexadecimal, named colors,
///     <c>rgb()</c>/<c>hsl()</c> functions, rounding and clamping, invalid text, and formatting.
/// </summary>
public class CssColorTests
{
    /// <summary>
    ///     Validates the hexadecimal forms, short and long, with and without alpha, in any letter case.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_HexForms_MatchExpectedPixels()
    {
        // Arrange: text and the straight-alpha pixel that CSS defines for it (the alpha pair comes last)
        var expected = new (string Text, Rgba32 Color)[]
        {
            ("#4c78a8", new Rgba32(76, 120, 168, 255)),
            ("#abc", new Rgba32(170, 187, 204, 255)),
            ("#abcd", new Rgba32(170, 187, 204, 221)),
            ("#4c78a8cc", new Rgba32(76, 120, 168, 204)),
            ("#FFF", new Rgba32(255, 255, 255, 255)),
            ("#00000000", new Rgba32(0, 0, 0, 0)),
            ("  #ddd  ", new Rgba32(221, 221, 221, 255)),
            ("#80112233", new Rgba32(0x80, 0x11, 0x22, 0x33)),
        };

        // Act / Assert: both entry points agree
        foreach (var (text, color) in expected)
        {
            Assert.True(CssColor.TryParse(text, out var parsed), text);
            Assert.Equal(color, parsed);
            Assert.Equal(color, CssColor.Parse(text));
        }
    }

    /// <summary>
    ///     Validates that the digit parser accepts exactly 3, 4, 6 and 8 hexadecimal digits and nothing else.
    /// </summary>
    [Fact]
    public void CssColor_TryParseHex_DigitsOnly_AcceptsExactLengthsAndRejectsEverythingElse()
    {
        // Arrange / Act / Assert: valid lengths, short digits doubled, alpha last and defaulting to opaque
        Assert.True(CssColor.TryParseHex("f80", out var shortRgb));
        Assert.Equal(new Rgba32(255, 0x88, 0, 255), shortRgb);
        Assert.True(CssColor.TryParseHex("f808", out var shortRgba));
        Assert.Equal(new Rgba32(255, 0x88, 0, 0x88), shortRgba);
        Assert.True(CssColor.TryParseHex("0A1b2C", out var longRgb));
        Assert.Equal(new Rgba32(0x0A, 0x1B, 0x2C, 255), longRgb);
        Assert.True(CssColor.TryParseHex("0a1b2c3d", out var longRgba));
        Assert.Equal(new Rgba32(0x0A, 0x1B, 0x2C, 0x3D), longRgba);

        // Assert: wrong lengths, a leading hash, signs, white space and non-hex characters are rejected with a default color
        foreach (var digits in new[] { "", "1", "12", "12345", "1234567", "123456789", "#123", "-12", "+12", " 12", "1 2", "ggg", "12345g", "\u0661\u0662\u0663" })
        {
            Assert.False(CssColor.TryParseHex(digits, out var color), digits);
            Assert.Equal(default, color);
        }
    }

    /// <summary>
    ///     Validates named colors in any letter case, <c>transparent</c>, and the size of the table.
    /// </summary>
    [Fact]
    public void CssColor_TryGetNamedColor_Names_ReturnTheCssTableEntries()
    {
        // Arrange: a spot check across the alphabet of values taken from the CSS Color specification
        var spot = new (string Name, string Hex)[]
        {
            ("aliceblue", "#f0f8ff"), ("aqua", "#00ffff"), ("crimson", "#dc143c"), ("darkslategrey", "#2f4f4f"),
            ("dodgerblue", "#1e90ff"), ("gold", "#ffd700"), ("lightgoldenrodyellow", "#fafad2"), ("lime", "#00ff00"),
            ("mediumvioletred", "#c71585"), ("orange", "#ffa500"), ("peachpuff", "#ffdab9"), ("salmon", "#fa8072"),
            ("tomato", "#ff6347"), ("whitesmoke", "#f5f5f5"), ("yellowgreen", "#9acd32"), ("rebeccapurple", "#663399"),
            ("steelblue", "#4682b4"), ("black", "#000000"), ("white", "#ffffff"),
        };

        // Act / Assert: 148 names (the 147 of CSS Color 3 plus rebeccapurple), the spot values, case and grey spellings
        Assert.Equal(148, CssColor.NamedColorCount);
        foreach (var (name, hex) in spot)
        {
            Assert.True(CssColor.TryGetNamedColor(name, out var named), name);
            Assert.Equal(CssColor.Parse(hex), named);
            Assert.Equal(named, CssColor.Parse(name));
            Assert.True(CssColor.TryGetNamedColor(name.ToUpperInvariant(), out var upper), name);
            Assert.Equal(named, upper);
        }

        Assert.True(CssColor.TryGetNamedColor("RebeccaPurple", out var mixedCase));
        Assert.Equal(new Rgba32(102, 51, 153, 255), mixedCase);
        Assert.True(CssColor.TryGetNamedColor("grey", out var grey));
        Assert.True(CssColor.TryGetNamedColor("gray", out var gray));
        Assert.Equal(gray, grey);
        Assert.True(CssColor.TryGetNamedColor("transparent", out var transparent));
        Assert.Equal(new Rgba32(0, 0, 0, 0), transparent);
    }

    /// <summary>
    ///     Validates that the name lookup is exact: no trimming, no functions, no hexadecimal and no partial names.
    /// </summary>
    [Fact]
    public void CssColor_TryGetNamedColor_NotAName_ReturnsFalseWithDefaultColor()
    {
        // Arrange / Act / Assert
        foreach (var text in new string?[] { null, string.Empty, " red", "red ", "#f00", "rgb(255,0,0)", "bogus", "none", "re", "currentColor" })
        {
            Assert.False(CssColor.TryGetNamedColor(text, out var color), text ?? "null");
            Assert.Equal(default, color);
        }
    }

    /// <summary>
    ///     Validates named colors through <see cref="CssColor.TryParse"/>, including surrounding white space.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_Names_MatchExpectedPixels()
    {
        // Arrange / Act / Assert
        Assert.True(CssColor.TryParse("  SteelBlue ", out var steel));
        Assert.Equal(new Rgba32(70, 130, 180, 255), steel);
        Assert.True(CssColor.TryParse("transparent", out var transparent));
        Assert.Equal(new Rgba32(0, 0, 0, 0), transparent);
    }

    /// <summary>
    ///     Validates the <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> and <c>hsla()</c> forms, in comma and space/slash
    ///     syntax, with numbers, percentages and hue units.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_FunctionForms_MatchExpectedPixels()
    {
        // Arrange
        var expected = new (string Text, Rgba32 Color)[]
        {
            // rgb()/rgba(): integers, percentages, alpha as number or percentage, comma or space syntax
            ("rgb(10, 20, 30)", new Rgba32(10, 20, 30, 255)),
            ("rgba(255,0,0,0.5)", new Rgba32(255, 0, 0, 128)),
            ("rgb(10% 20% 30% / 50%)", new Rgba32(26, 51, 77, 128)),
            ("rgb(10 20 30 / 0.2)", new Rgba32(10, 20, 30, 51)),
            ("RGB(0, 128.4, 255)", new Rgba32(0, 128, 255, 255)),
            ("rgb(none 20 30)", new Rgba32(0, 20, 30, 255)),

            // hsl()/hsla(): hue units, percentages, alpha
            ("hsl(0, 100%, 50%)", new Rgba32(255, 0, 0, 255)),
            ("hsl(120 100% 25%)", new Rgba32(0, 128, 0, 255)),
            ("hsla(240, 100%, 50%, 0.25)", new Rgba32(0, 0, 255, 64)),
            ("hsl(0.5turn 100% 50%)", new Rgba32(0, 255, 255, 255)),
            ("hsl(180deg, 100%, 50%)", new Rgba32(0, 255, 255, 255)),
            ("hsl(-120, 100%, 50%)", new Rgba32(0, 0, 255, 255)),
            ("hsl(200 0% 40%)", new Rgba32(102, 102, 102, 255)),
            ("hsl(0, 100%, 100%)", new Rgba32(255, 255, 255, 255)),

            // The space syntax takes numbers as well as percentages for saturation and lightness
            ("hsl(0 100 50)", new Rgba32(255, 0, 0, 255)),
            ("hsl(120deg 100% 50% / 0.5)", new Rgba32(0, 255, 0, 128)),

            // Both syntaxes, written cleanly
            ("rgba(1,2,3,0.5)", new Rgba32(1, 2, 3, 128)),
            ("rgb(1 2 3 / 50%)", new Rgba32(1, 2, 3, 128)),
            ("rgb(1 2 3/0.5)", new Rgba32(1, 2, 3, 128)),
            ("rgb( 1 , 2 , 3 )", new Rgba32(1, 2, 3, 255)),
            ("rgb(none none none / none)", new Rgba32(0, 0, 0, 0)),
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
    ///     Validates that out-of-range channels are clamped and that fractions round half away from zero.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_OutOfRangeAndFractions_ClampAndRoundHalfAwayFromZero()
    {
        // Arrange: 30% of 255 is 76.5 and an alpha of 0.5 is 127.5, which half-to-even would give as 76 and 128
        var expected = new (string Text, Rgba32 Color)[]
        {
            ("rgb(300, -5, 0)", new Rgba32(255, 0, 0, 255)),
            ("rgba(1, 2, 3, 7)", new Rgba32(1, 2, 3, 255)),
            ("rgba(1, 2, 3, -1)", new Rgba32(1, 2, 3, 0)),
            ("rgb(30%, 0, 0)", new Rgba32(77, 0, 0, 255)),
            ("rgb(0.5, 1.5, 2.5)", new Rgba32(1, 2, 3, 255)),
            ("rgba(0, 0, 0, 0.5)", new Rgba32(0, 0, 0, 128)),
            ("rgb(150%, 0, 0)", new Rgba32(255, 0, 0, 255)),
        };

        // Act / Assert
        foreach (var (text, color) in expected)
        {
            Assert.True(CssColor.TryParse(text, out var parsed), text);
            Assert.Equal(color, parsed);
        }
    }

    /// <summary>
    ///     Validates that bad text fails cleanly: false and a default color from the try method.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_InvalidText_ReturnsFalseWithDefaultColor()
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
    }

    /// <summary>
    ///     Validates that the two CSS function syntaxes are not mixed: the comma syntax has three or four components and
    ///     no slash or <c>none</c>, the space syntax has three components and an optional slash and alpha, and a
    ///     comma-syntax <c>hsl()</c> needs percentages.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_MixedOrMalformedSyntax_ReturnsFalseWithDefaultColor()
    {
        // Arrange: hybrids, empty components, trailing separators, none in the comma syntax, bare numbers in comma hsl()
        string[] invalid =
        [
            "rgb(1 2 3 0.5)", "rgb(1,2,3 / 0.5)", "rgb(1, 2 3)", "rgb(1 2, 3)", "rgba(1 2 3 4)", "rgb(1,2,3,)", "rgb(,1,2,3)",
            "rgb(1,,3)", "rgb(1 2 3 /)", "rgb(1 2 3 / 0.5 0.5)", "rgb(1 2 3 / 0.5 / 0.5)", "rgb(1 2 / 3)", "rgb(/ 1 2 3)",
            "rgb(none,2,3)", "rgb(1,2,3,none)", "hsla(0,100%,50%,none)", "hsl(0,100,50)", "hsl(0,100%,50)", "hsl(0,100,50%)",
            "hsl(0 100% 50% 0.5)", "hsl(0, 100% 50%)", "hsl(0,100%,50%,)",
        ];

        // Act / Assert
        foreach (var text in invalid)
        {
            Assert.False(CssColor.TryParse(text, out var color), text);
            Assert.Equal(default, color);
        }
    }

    /// <summary>
    ///     Validates that a hue whose unit conversion overflows to infinity is rejected instead of becoming black.
    /// </summary>
    [Fact]
    public void CssColor_TryParse_HueOverflowingAfterUnitConversion_ReturnsFalse()
    {
        // Arrange / Act / Assert: 1e308 is finite, but 1e308 turns is 3.6e310 degrees
        Assert.False(CssColor.TryParse("hsl(1e308turn,100%,50%)", out var color));
        Assert.Equal(default, color);
        Assert.False(CssColor.TryParse("hsl(1e308turn 100% 50%)", out _));
        Assert.True(CssColor.TryParse("hsl(1e300deg 100% 50%)", out _));
    }

    /// <summary>
    ///     Validates that the strict parser throws a format exception for bad text.
    /// </summary>
    [Fact]
    public void CssColor_Parse_InvalidText_ThrowsFormatException()
    {
        // Arrange / Act / Assert
        var error = Assert.Throws<FormatException>(() => CssColor.Parse("bogus"));
        Assert.Contains("bogus", error.Message, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => CssColor.Parse(string.Empty));
        Assert.Throws<FormatException>(() => CssColor.Parse("none"));
    }

    /// <summary>
    ///     Validates that the strict parser rejects a null argument with an argument exception.
    /// </summary>
    [Fact]
    public void CssColor_Parse_Null_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => CssColor.Parse(null!));

    /// <summary>
    ///     Validates that <see cref="CssColor.ToCss"/> gives the shortest exact hexadecimal form and that it reads back.
    /// </summary>
    [Fact]
    public void CssColor_ToCss_Colors_FormatAsHexAndRoundTrip()
    {
        // Arrange / Act / Assert: opaque colors are six digits, others eight, always lower case
        Assert.Equal("#4c78a8", CssColor.ToCss(new Rgba32(76, 120, 168, 255)));
        Assert.Equal("#4c78a880", CssColor.ToCss(new Rgba32(76, 120, 168, 128)));
        Assert.Equal("#abcdef", CssColor.ToCss(new Rgba32(0xAB, 0xCD, 0xEF, 255)));
        foreach (var color in new[] { new Rgba32(0, 0, 0, 0), new Rgba32(1, 2, 3, 4), new Rgba32(255, 255, 255, 255) })
        {
            Assert.Equal(color, CssColor.Parse(CssColor.ToCss(color)));
        }
    }

    /// <summary>
    ///     Validates that the alpha pair is last for CSS colors and first for <see cref="Rgba32.Parse(string)"/>.
    /// </summary>
    [Fact]
    public void CssColor_Parse_EightDigitHex_PutsAlphaLastUnlikeRgba32Parse()
    {
        // Arrange / Act / Assert
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x80), CssColor.Parse("#11223380"));
        Assert.Equal(new Rgba32(0x22, 0x33, 0x80, 0x11), Rgba32.Parse("#11223380"));
    }
}
