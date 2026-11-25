using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="MathFont"/>: the constants of the bundled Noto Sans Math, their scaling, the glyph
///     fallback, the ink bounds, the italic correction and the top accent attachment.
/// </summary>
public class MathFontTests
{
    /// <summary>
    ///     Validates the constants of the MATH table of the bundled font, raw and scaled.
    /// </summary>
    [Fact]
    public void MathFont_Default_HasMathConstants()
    {
        // Arrange
        var font = MathFont.Default;

        // Act / Assert
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(278, font.RawConstant(MathConstant.AxisHeight));
        Assert.Equal((60, 50), (font.RawConstant(MathConstant.ScriptPercentScaleDown), font.RawConstant(MathConstant.ScriptScriptPercentScaleDown)));
        Assert.Equal((71, 2300, 2300), (font.RawConstant(MathConstant.FractionRuleThickness), font.RawConstant(MathConstant.DisplayOperatorMinHeight), font.RawConstant(MathConstant.DelimitedSubFormulaMinHeight)));
        Assert.Equal(27.8, font.Constant(MathConstant.AxisHeight, 100), 9);
        Assert.Equal(4.448, font.Constant(MathConstant.AxisHeight, 16), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => font.RawConstant((MathConstant)99));
    }

    /// <summary>
    ///     Validates the fallback: a character of the math font, an accented letter from Noto Sans, and a character no
    ///     bundled font has.
    /// </summary>
    [Fact]
    public void MathFont_Resolve_FallsBackDeterministically()
    {
        // Arrange
        var font = MathFont.Default;

        // Act
        var x = font.Resolve('x');
        var accented = font.Resolve('\u00E9');
        var missing = font.Resolve(0xE000);

        // Assert
        Assert.Same(font.Primary, x.Font);
        Assert.NotSame(font.Primary, accented.Font);
        Assert.Equal("Noto Sans", accented.Font.GetNameInfo().FamilyName);
        Assert.NotEqual(0, accented.Glyph);
        Assert.Equal((font.Primary, 0), missing);
    }

    /// <summary>
    ///     Validates the ink bounds of the italic x (0, 0, 547, 536 of 1000 units), the empty bounds of a space, and that a
    ///     repeated lookup gives the same bounds.
    /// </summary>
    [Fact]
    public void MathFont_InkBounds_ScaleTheOutlineBounds()
    {
        // Arrange
        var font = MathFont.Default;
        var (italic, x) = font.Resolve(0x1D465);
        var (spaceFont, space) = font.Resolve(' ');

        // Act
        var bounds = font.InkBounds(italic, x, 100);
        var again = font.InkBounds(italic, x, 100);
        var empty = font.InkBounds(spaceFont, space, 100);

        // Assert
        Assert.Equal(0, bounds.X, 3);
        Assert.Equal(-53.6, bounds.Y, 3);
        Assert.Equal(54.7, bounds.Width, 3);
        Assert.Equal(53.6, bounds.Height, 3);
        Assert.Equal(bounds, again);
        Assert.Equal(new MathRect(0, 0, 0, 0), empty);
        Assert.Equal(53.6, font.XHeight(100), 3);
    }

    /// <summary>
    ///     Validates the italic correction (the italic f: 145 units) and the top accent attachment (a: 281 units) of the
    ///     primary font, and that a fallback glyph has neither.
    /// </summary>
    [Fact]
    public void MathFont_ItalicCorrectionAndTopAccent_ComeFromTheMathTable()
    {
        // Arrange
        var font = MathFont.Default;
        var (italicFont, f) = font.Resolve(0x1D453);
        var (aFont, a) = font.Resolve('a');
        var (fallback, accented) = font.Resolve('\u00E9');

        // Act / Assert
        Assert.Equal(14.5, font.ItalicCorrection(italicFont, f, 100), 9);
        Assert.Equal(28.1, font.TopAccentAttachment(aFont, a, 100)!.Value, 9);
        Assert.Equal(0, font.ItalicCorrection(fallback, accented, 100));
        Assert.Null(font.TopAccentAttachment(fallback, accented, 100));
        Assert.Equal(45, MathFont.Advance(italicFont, f, 100), 9);
    }

    /// <summary>
    ///     Validates that a font without a MATH table cannot be a math font.
    /// </summary>
    [Fact]
    public void MathFont_Constructor_FontWithoutMathTable_ThrowsInvalidDataException()
    {
        // Arrange
        var notoSans = GlyphFallback.BundledFonts[0];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => new MathFont(notoSans));
    }
}
