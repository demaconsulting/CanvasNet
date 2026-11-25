using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="MathFont"/>: the constants of the bundled Noto Sans Math, their scaling, the glyph
///     fallback, the ink bounds, the italic correction, the top accent attachment and the stretch constructions.
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
    ///     Validates the fraction, stack and radical constants of the MATH table of the bundled font (font design units, 1000
    ///     per em; a percentage for RadicalDegreeBottomRaisePercent).
    /// </summary>
    /// <param name="constant">The name of the constant.</param>
    /// <param name="expected">Its value in Noto Sans Math.</param>
    [Theory]
    [InlineData(nameof(MathConstant.FractionNumeratorShiftUp), 529)]
    [InlineData(nameof(MathConstant.FractionNumeratorDisplayStyleShiftUp), 704)]
    [InlineData(nameof(MathConstant.FractionDenominatorShiftDown), 320)]
    [InlineData(nameof(MathConstant.FractionDenominatorDisplayStyleShiftDown), 673)]
    [InlineData(nameof(MathConstant.FractionNumeratorGapMin), 71)]
    [InlineData(nameof(MathConstant.FractionNumDisplayStyleGapMin), 150)]
    [InlineData(nameof(MathConstant.FractionDenominatorGapMin), 71)]
    [InlineData(nameof(MathConstant.FractionDenomDisplayStyleGapMin), 150)]
    [InlineData(nameof(MathConstant.StackTopShiftUp), 529)]
    [InlineData(nameof(MathConstant.StackTopDisplayStyleShiftUp), 704)]
    [InlineData(nameof(MathConstant.StackBottomShiftDown), 320)]
    [InlineData(nameof(MathConstant.StackBottomDisplayStyleShiftDown), 673)]
    [InlineData(nameof(MathConstant.StackGapMin), 178)]
    [InlineData(nameof(MathConstant.StackDisplayStyleGapMin), 336)]
    [InlineData(nameof(MathConstant.RadicalVerticalGap), 89)]
    [InlineData(nameof(MathConstant.RadicalDisplayStyleVerticalGap), 190)]
    [InlineData(nameof(MathConstant.RadicalRuleThickness), 71)]
    [InlineData(nameof(MathConstant.RadicalExtraAscender), 71)]
    [InlineData(nameof(MathConstant.RadicalKernBeforeDegree), 51)]
    [InlineData(nameof(MathConstant.RadicalKernAfterDegree), -450)]
    [InlineData(nameof(MathConstant.RadicalDegreeBottomRaisePercent), 56)]
    public void MathFont_RawConstant_FractionAndRadicalConstants_MatchTheFont(string constant, int expected)
    {
        // Act
        var value = MathFont.Default.RawConstant(Enum.Parse<MathConstant>(constant));

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates the script, limit, stretch-stack, overbar, underbar and accent constants of the MATH table of the
    ///     bundled font (font design units, 1000 per em), and their scaling to CSS pixels.
    /// </summary>
    /// <param name="constant">The name of the constant.</param>
    /// <param name="expected">Its value in Noto Sans Math.</param>
    [Theory]
    [InlineData(nameof(MathConstant.SubscriptShiftDown), 210)]
    [InlineData(nameof(MathConstant.SubscriptTopMax), 430)]
    [InlineData(nameof(MathConstant.SubscriptBaselineDropMin), 140)]
    [InlineData(nameof(MathConstant.SuperscriptShiftUp), 390)]
    [InlineData(nameof(MathConstant.SuperscriptShiftUpCramped), 280)]
    [InlineData(nameof(MathConstant.SuperscriptBottomMin), 134)]
    [InlineData(nameof(MathConstant.SuperscriptBaselineDropMax), 240)]
    [InlineData(nameof(MathConstant.SubSuperscriptGapMin), 160)]
    [InlineData(nameof(MathConstant.SuperscriptBottomMaxWithSubscript), 430)]
    [InlineData(nameof(MathConstant.SpaceAfterScript), 40)]
    [InlineData(nameof(MathConstant.UpperLimitGapMin), 100)]
    [InlineData(nameof(MathConstant.UpperLimitBaselineRiseMin), 244)]
    [InlineData(nameof(MathConstant.LowerLimitGapMin), 100)]
    [InlineData(nameof(MathConstant.LowerLimitBaselineDropMin), 630)]
    [InlineData(nameof(MathConstant.StretchStackTopShiftUp), 244)]
    [InlineData(nameof(MathConstant.StretchStackBottomShiftDown), 559)]
    [InlineData(nameof(MathConstant.StretchStackGapAboveMin), 100)]
    [InlineData(nameof(MathConstant.StretchStackGapBelowMin), 100)]
    [InlineData(nameof(MathConstant.OverbarVerticalGap), 150)]
    [InlineData(nameof(MathConstant.OverbarExtraAscender), 71)]
    [InlineData(nameof(MathConstant.UnderbarVerticalGap), 150)]
    [InlineData(nameof(MathConstant.UnderbarExtraDescender), 71)]
    [InlineData(nameof(MathConstant.AccentBaseHeight), 556)]
    public void MathFont_RawConstant_ScriptAndLimitConstants_MatchTheFont(string constant, int expected)
    {
        // Act
        var parsed = Enum.Parse<MathConstant>(constant);
        var raw = MathFont.Default.RawConstant(parsed);
        var scaled = MathFont.Default.Constant(parsed, 100);

        // Assert: the value, and its scaling to 100 px
        Assert.Equal(expected, raw);
        Assert.Equal(expected / 10.0, scaled, 9);
    }
    /// <summary>
    ///     Validates the constructions of the MATH table scaled to a font size: the vertical variants and assembly of the
    ///     parenthesis, the horizontal assembly of the right arrow, none for a fallback font or a letter, and the minimum
    ///     connector overlap and overbar thickness.
    /// </summary>
    [Fact]
    public void MathFont_TryGetConstruction_ScalesVariantsAndParts()
    {
        // Arrange
        var font = MathFont.Default;
        var (parenFont, paren) = font.Resolve('(');
        var (arrowFont, arrow) = font.Resolve(0x2192);
        var (accentedFont, accented) = font.Resolve('\u00E9');
        var (xFont, x) = font.Resolve('x');

        // Act
        var hasParen = font.TryGetConstruction(parenFont, paren, StretchAxis.Block, 100, out var vertical);
        var hasArrow = font.TryGetConstruction(arrowFont, arrow, StretchAxis.Inline, 100, out var horizontal);
        var hasFallback = font.TryGetConstruction(accentedFont, accented, StretchAxis.Block, 100, out _);
        var hasLetter = font.TryGetConstruction(xFont, x, StretchAxis.Block, 100, out _);

        // Assert
        Assert.True(hasParen && hasArrow);
        Assert.False(hasFallback || hasLetter);
        Assert.Equal(13, vertical!.Variants.Count);
        Assert.Equal((paren, 94.2), (vertical.Variants[0].Glyph, Math.Round(vertical.Variants[0].Advance, 9)));
        Assert.Equal([false, true, false], vertical.Parts!.Select(part => part.IsExtender));
        Assert.Equal((0.0, 62.7, 106.9), (vertical.Parts![0].StartConnector, Math.Round(vertical.Parts[0].EndConnector, 9), Math.Round(vertical.Parts[0].FullAdvance, 9)));
        Assert.Empty(horizontal!.Variants);
        Assert.NotEmpty(horizontal.Parts!);
        Assert.Equal(10, font.MinConnectorOverlap(100), 9);
        Assert.True(font.Constant(MathConstant.OverbarRuleThickness, 100) > 0);
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
