namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Unit tests for <c>font-size</c>, <c>line-height</c> and the <c>font</c> shorthand handling
///     of <see cref="SvgCodec"/>: every source (presentation attribute, inline style, stylesheet
///     rule, shorthand, <c>font</c> presentation attribute), every unit and keyword, the CSS-wide
///     keywords, precedence, the reset semantics of the shorthand, the tolerant malformed-value
///     policy, and the text <c>dx</c>/<c>dy</c> offsets that font-relative units position.
/// </summary>
/// <remarks>
///     The measurement trick (see <see cref="SvgLengthTestSupport.MeasureFontSize"/>): the synthetic
///     <c>A</c> glyph is a square whose side is half the font size, so the rendered ink area is
///     <c>S^2 / 4</c> and the effective font size is recovered as <c>2 * sqrt(area)</c> - independent
///     of how the size was written.
/// </remarks>
public class SvgFontSizeTests
{
    /// <summary>
    ///     The absolute tolerance, in user units, for a measured font size. The ink area is
    ///     accumulated from 8-bit anti-aliased coverage, so a few hundredths of a unit is the
    ///     measurement resolution, not slack in the feature.
    /// </summary>
    private const double Tolerance = 0.1;

    /// <summary>Asserts two same-size surfaces are pixel-identical.</summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    private static void AssertSurfacesEqual(DemaConsulting.CanvasNet.Canvas.Surface expected, DemaConsulting.CanvasNet.Canvas.Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    // ================================================================================================
    // The reported case and plain units
    // ================================================================================================

    /// <summary>
    ///     Proves the reported defect is fixed: <c>font-size="10px"</c> loads (it used to fail with
    ///     "invalid numeric data") and renders pixel-identically to <c>font-size="10"</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FontSizePx_RendersSameAsUnitless()
    {
        // Arrange
        const string Template = "<svg viewBox='0 0 200 200'>{0}</svg>";

        // Act
        var withUnit = SvgLengthTestSupport.Render(string.Format(Template, SvgLengthTestSupport.Glyph("font-size='10px'")), 200, 200);
        var unitless = SvgLengthTestSupport.Render(string.Format(Template, SvgLengthTestSupport.Glyph("font-size='10'")), 200, 200);

        // Assert
        AssertSurfacesEqual(unitless, withUnit);
        Assert.True(SvgLengthTestSupport.TotalCoverage(withUnit) > 0.0);
        Assert.Equal(10.0, SvgLengthTestSupport.MeasureFontSizeOf(SvgLengthTestSupport.Glyph("font-size='10px'")), Tolerance);
    }

    /// <summary>Proves every length form is accepted for <c>font-size</c> and converted correctly.</summary>
    /// <param name="fontSize">The raw <c>font-size</c> value.</param>
    /// <param name="expected">The expected effective font size.</param>
    [Theory]
    [InlineData("10px", 10.0)]
    [InlineData("10PX", 10.0)]
    [InlineData("12pt", 16.0)]
    [InlineData("1pc", 16.0)]
    [InlineData("1in", 96.0)]
    [InlineData("10mm", 37.7952756)]
    [InlineData("2.54cm", 96.0)]
    [InlineData("40Q", 37.7952756)]
    [InlineData("1.5em", 24.0)]
    [InlineData("200%", 32.0)]
    [InlineData("2rem", 32.0)]
    [InlineData("calc(10px + 2px)", 12.0)]
    [InlineData("calc(100% / 2)", 8.0)]
    [InlineData("min(10px, 20px)", 10.0)]
    [InlineData("max(1em, 20px)", 20.0)]
    [InlineData("clamp(10px, 1em, 12px)", 12.0)]
    [InlineData("20", 20.0)]
    public void SvgCodec_Load_FontSizeLength_ResolvesAgainstParent(string fontSize, double expected) =>
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(SvgLengthTestSupport.Glyph($"font-size='{fontSize}'")), Tolerance);

    /// <summary>Proves <c>em</c>/<c>%</c> in <c>font-size</c> use the PARENT's size and compound down the tree.</summary>
    [Fact]
    public void SvgCodec_Load_FontSizeEm_UsesParentNotSelf()
    {
        // Arrange: the group is 20; the text asks for 2em of its parent, not of itself
        var inner = $"<g font-size='20'>{SvgLengthTestSupport.Glyph("font-size='2em'")}</g>";

        // Act / Assert
        Assert.Equal(40.0, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    /// <summary>Proves <c>rem</c> in <c>font-size</c> uses the root's own size.</summary>
    [Fact]
    public void SvgCodec_Load_FontSizeRem_UsesRootFontSize()
    {
        // Arrange: root 10, a deeper group 50, text 2rem
        var inner = $"<g font-size='50'>{SvgLengthTestSupport.Glyph("font-size='2rem'")}</g>";

        // Act / Assert
        Assert.Equal(20.0, SvgLengthTestSupport.MeasureFontSizeOf(inner, "font-size='10'"), Tolerance);
    }

    /// <summary>Proves <c>ex</c>/<c>ch</c> in <c>font-size</c> measure the PARENT's font (here the glyph-less test font, so the 0.5em fallback).</summary>
    /// <param name="unit">The unit under test.</param>
    [Theory]
    [InlineData("ex")]
    [InlineData("ch")]
    public void SvgCodec_Load_FontSizeExAndCh_UseParentFontFallback(string unit)
    {
        // Arrange: parent is TestFont at 20 (no x or 0 glyph); the text asks for 2ex/2ch
        var inner = $"<g font-family='TestFont' font-size='20'>{SvgLengthTestSupport.Glyph("font-size='2" + unit + "'")}</g>";

        // Act / Assert: 2 * 0.5 * 20
        Assert.Equal(20.0, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    /// <summary>Proves <c>lh</c> in <c>font-size</c> uses the parent's line height.</summary>
    [Fact]
    public void SvgCodec_Load_FontSizeLh_UsesParentLineHeight()
    {
        // Arrange: parent 10px with normal line height (1.2em = 12)
        var inner = $"<g font-size='10'>{SvgLengthTestSupport.Glyph("font-size='2lh'")}</g>";

        // Act / Assert
        Assert.Equal(24.0, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    // ================================================================================================
    // Keywords
    // ================================================================================================

    /// <summary>Proves each absolute-size keyword maps to the browser pixel table, ignoring the parent.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="expected">The expected size.</param>
    [Theory]
    [InlineData("xx-small", 9.0)]
    [InlineData("x-small", 10.0)]
    [InlineData("small", 13.0)]
    [InlineData("medium", 16.0)]
    [InlineData("large", 18.0)]
    [InlineData("x-large", 24.0)]
    [InlineData("xx-large", 32.0)]
    [InlineData("xxx-large", 48.0)]
    [InlineData("LARGE", 18.0)]
    public void SvgCodec_Load_AbsoluteSizeKeyword_MapsToBrowserTable(string keyword, double expected)
    {
        // Arrange: a parent of 40 proves the keyword does not depend on the parent
        var inner = $"<g font-size='40'>{SvgLengthTestSupport.Glyph($"font-size='{keyword}'")}</g>";

        // Act / Assert
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    /// <summary>Proves <c>larger</c>/<c>smaller</c> scale the parent by 1.2 and the CSS-wide keywords behave per CSS.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="expected">The expected size under a parent of 20.</param>
    [Theory]
    [InlineData("larger", 24.0)]
    [InlineData("smaller", 16.6666667)]
    [InlineData("inherit", 20.0)]
    [InlineData("unset", 20.0)]
    [InlineData("initial", 16.0)]
    [InlineData("revert", 20.0)]
    public void SvgCodec_Load_RelativeAndCssWideKeyword_ResolvesAgainstParent(string keyword, double expected)
    {
        // Arrange
        var inner = $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"font-size='{keyword}'")}</g>";

        // Act / Assert
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    /// <summary>Proves repeated <c>larger</c> compounds through nesting.</summary>
    [Fact]
    public void SvgCodec_Load_NestedLarger_Compounds()
    {
        // Arrange: 20 -> 24 -> 28.8
        var inner = $"<g font-size='20'><g font-size='larger'>{SvgLengthTestSupport.Glyph("font-size='larger'")}</g></g>";

        // Act / Assert
        Assert.Equal(28.8, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    // ================================================================================================
    // Sources and precedence
    // ================================================================================================

    /// <summary>Proves <c>font-size</c> is honored from an inline style, a type rule, a class rule and the presentation attribute.</summary>
    /// <param name="markup">The document body.</param>
    /// <param name="expected">The expected size.</param>
    [Theory]
    [InlineData("<text x='20' y='150' font-family='TestFont' style='font-size: 12px'>A</text>", 12.0)]
    [InlineData("<style>text { font-size: 14px }</style><text x='20' y='150' font-family='TestFont'>A</text>", 14.0)]
    [InlineData("<style>.big { font-size: 1.5em }</style><text class='big' x='20' y='150' font-family='TestFont'>A</text>", 24.0)]
    [InlineData("<style>.t { font-size: x-large }</style><text class='t' x='20' y='150' font-family='TestFont'>A</text>", 24.0)]
    [InlineData("<style>svg { font-size: 12px }</style><text x='20' y='150' font-family='TestFont' font-size='2em'>A</text>", 24.0)]
    public void SvgCodec_Load_FontSizeSources_AreHonored(string markup, double expected) =>
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(markup), Tolerance);

    /// <summary>Proves the cascade order: an inline style beats a stylesheet rule beats the presentation attribute.</summary>
    [Fact]
    public void SvgCodec_Load_FontSizePrecedence_InlineBeatsRuleBeatsAttribute()
    {
        // Arrange / Act
        var attributeOnly = SvgLengthTestSupport.MeasureFontSizeOf("<text x='20' y='150' font-family='TestFont' font-size='30'>A</text>");
        var ruleBeatsAttribute = SvgLengthTestSupport.MeasureFontSizeOf(
            "<style>text { font-size: 20px }</style><text x='20' y='150' font-family='TestFont' font-size='30'>A</text>");
        var inlineBeatsRule = SvgLengthTestSupport.MeasureFontSizeOf(
            "<style>text { font-size: 20px }</style><text x='20' y='150' font-family='TestFont' font-size='30' style='font-size: 10px'>A</text>");

        // Assert
        Assert.Equal(30.0, attributeOnly, Tolerance);
        Assert.Equal(20.0, ruleBeatsAttribute, Tolerance);
        Assert.Equal(10.0, inlineBeatsRule, Tolerance);
    }

    // ================================================================================================
    // The font shorthand
    // ================================================================================================

    /// <summary>Proves the <c>font</c> shorthand sets the size from every source, in the grammar variants CSS allows.</summary>
    /// <param name="markup">The document body.</param>
    /// <param name="expected">The expected size.</param>
    [Theory]
    [InlineData("<text x='20' y='150' style='font: italic bold 12px/1.5 TestFont, Arial, sans-serif'>A</text>", 12.0)]
    [InlineData("<text x='20' y='150' style='font: 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: 700 oblique 14px / 2 TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: small-caps condensed normal 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 10deg 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: 12px \"Some, Family\", TestFont'>A</text>", 12.0)]
    [InlineData("<text x='20' y='150' style='font: x-large TestFont'>A</text>", 24.0)]
    [InlineData("<text x='20' y='150' style='font: 150% TestFont'>A</text>", 24.0)]
    [InlineData("<text x='20' y='150' style='font: calc(10px + 2px) TestFont'>A</text>", 12.0)]
    [InlineData("<style>text { font: 18px/1 TestFont }</style><text x='20' y='150'>A</text>", 18.0)]
    [InlineData("<text x='20' y='150' font='14px TestFont'>A</text>", 14.0)]
    public void SvgCodec_Load_FontShorthand_SetsSize(string markup, double expected) =>
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(markup), Tolerance);

    /// <summary>Proves ordering rules: within one block the later of shorthand and longhand wins; the <c>font</c> attribute is the lowest tier.</summary>
    [Fact]
    public void SvgCodec_Load_FontShorthandPrecedence_FollowsDeclarationOrderAndTier()
    {
        // Arrange / Act
        var longhandAfter = SvgLengthTestSupport.MeasureFontSizeOf(
            "<text x='20' y='150' style='font: 12px TestFont; font-size: 30px'>A</text>");
        var shorthandAfter = SvgLengthTestSupport.MeasureFontSizeOf(
            "<text x='20' y='150' style='font-size: 30px; font: 12px TestFont'>A</text>");
        var attributeBeatsFontAttribute = SvgLengthTestSupport.MeasureFontSizeOf(
            "<text x='20' y='150' font-family='TestFont' font-size='20' font='14px Other'>A</text>");
        var ruleBeatsFontAttribute = SvgLengthTestSupport.MeasureFontSizeOf(
            "<style>text { font-size: 22px }</style><text x='20' y='150' font-family='TestFont' font='14px Other'>A</text>");
        var familyFromLonghandAttribute = SvgLengthTestSupport.MeasureFontSizeOf(
            "<text x='20' y='150' font-family='TestFont' font='14px Other'>A</text>");

        // Assert
        Assert.Equal(30.0, longhandAfter, Tolerance);
        Assert.Equal(12.0, shorthandAfter, Tolerance);
        Assert.Equal(20.0, attributeBeatsFontAttribute, Tolerance);
        Assert.Equal(22.0, ruleBeatsFontAttribute, Tolerance);
        Assert.Equal(14.0, familyFromLonghandAttribute, Tolerance);
    }

    /// <summary>Proves the shorthand resets the longhands it omits (weight returns to normal) and a bold keyword inside it selects the bold face.</summary>
    [Fact]
    public void SvgCodec_Load_FontShorthand_ResetsAndSetsWeight()
    {
        // Arrange: regular glyph is a 10x10 square at size 20; the bold face is 16x10
        const double Regular = 100.0;
        const double Bold = 160.0;
        const string Wrapper = "<svg viewBox='0 0 200 200'>{0}</svg>";

        // Act
        var boldAttribute = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFaces(
            string.Format(Wrapper, "<text x='20' y='150' font-family='TestFont' font-size='20' font-weight='bold'>A</text>"), 200, 200));
        var shorthandResetsWeight = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFaces(
            string.Format(Wrapper, "<text x='20' y='150' font-weight='bold' style='font: 20px TestFont'>A</text>"), 200, 200));
        var shorthandSetsBold = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFaces(
            string.Format(Wrapper, "<text x='20' y='150' style='font: bold 20px TestFont'>A</text>"), 200, 200));
        var numericWeight = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFaces(
            string.Format(Wrapper, "<text x='20' y='150' style='font: 700 20px TestFont'>A</text>"), 200, 200));

        // Assert
        Assert.Equal(Bold, boldAttribute, 1.0);
        Assert.Equal(Regular, shorthandResetsWeight, 1.0);
        Assert.Equal(Bold, shorthandSetsBold, 1.0);
        Assert.Equal(Bold, numericWeight, 1.0);
    }

    /// <summary>Proves an invalid shorthand (no family, bad size, system-font keyword, CSS-wide revert) is dropped whole, leaving other sources in force.</summary>
    /// <param name="style">The inline style holding the invalid shorthand.</param>
    [Theory]
    [InlineData("font: 12px")]
    [InlineData("font: caption")]
    [InlineData("font: icon")]
    [InlineData("font: 12xyz TestFont")]
    [InlineData("font: bold TestFont")]
    [InlineData("font: 12px/ TestFont")]
    [InlineData("font: 12px/abc TestFont")]
    [InlineData("font: revert")]
    public void SvgCodec_Load_InvalidFontShorthand_IsDropped(string style) =>
        Assert.Equal(30.0, SvgLengthTestSupport.MeasureFontSizeOf(
            $"<text x='20' y='150' font-family='TestFont' font-size='30' style='{style}'>A</text>"), Tolerance);

    /// <summary>Proves the CSS-wide keywords work as the whole shorthand value.</summary>
    [Fact]
    public void SvgCodec_Load_FontShorthandCssWideKeyword_AppliesToFontSize()
    {
        // Arrange: parent 20
        var inherited = SvgLengthTestSupport.MeasureFontSizeOf(
            "<g font-size='20'><text x='20' y='150' font-family='TestFont' font-size='40' style='font: inherit'>A</text></g>");
        var initial = SvgLengthTestSupport.MeasureFontSizeOf(
            "<g font-size='20'><text x='20' y='150' font-family='TestFont' font-size='40' style='font: initial'>A</text></g>");

        // Assert
        Assert.Equal(20.0, inherited, Tolerance);
        Assert.Equal(16.0, initial, Tolerance);
    }

    // ================================================================================================
    // Malformed values (class F: ignored, inherited)
    // ================================================================================================

    /// <summary>Proves a malformed <c>font-size</c> is ignored (the size is inherited) instead of failing the document.</summary>
    /// <param name="fontSize">The malformed value.</param>
    [Theory]
    [InlineData("10xyz")]
    [InlineData("abc")]
    [InlineData("huge")]
    [InlineData("12 px")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e6in")]
    [InlineData("calc(")]
    [InlineData("calc(1px + 2)")]
    [InlineData("calc(1px / 0)")]
    [InlineData("-")]
    public void SvgCodec_Load_MalformedFontSize_IsIgnoredAndInherited(string fontSize)
    {
        // Arrange: parent 20, and the same malformed value through the attribute and the inline style
        var viaAttribute = $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"font-size='{fontSize}'")}</g>";
        var viaStyle = $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"style='font-size: {fontSize}'")}</g>";

        // Act / Assert: inherited, and no exception
        Assert.Equal(20.0, SvgLengthTestSupport.MeasureFontSizeOf(viaAttribute), Tolerance);
        Assert.Equal(20.0, SvgLengthTestSupport.MeasureFontSizeOf(viaStyle), Tolerance);
    }

    /// <summary>Proves a malformed <c>line-height</c> is ignored rather than failing the document.</summary>
    [Fact]
    public void SvgCodec_Load_MalformedLineHeight_IsIgnored()
    {
        // Arrange / Act: no exception, and the text still renders at its size
        var measured = SvgLengthTestSupport.MeasureFontSizeOf(
            SvgLengthTestSupport.Glyph("font-size='20' style='line-height: 12xyz'"));

        // Assert
        Assert.Equal(20.0, measured, Tolerance);
    }

    /// <summary>Proves existing behavior is unchanged: a zero or negative font size still renders nothing.</summary>
    /// <param name="fontSize">The size.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("0px")]
    [InlineData("-10")]
    [InlineData("-1em")]
    public void SvgCodec_Load_NonPositiveFontSize_RendersNoText(string fontSize) =>
        Assert.Equal(0.0, SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.Render(
            $"<svg viewBox='0 0 200 200'>{SvgLengthTestSupport.Glyph($"font-size='{fontSize}'")}</svg>", 200, 200)), 0.0001);

    // ================================================================================================
    // Text dx / dy
    // ================================================================================================

    /// <summary>Proves the first <c>dx</c>/<c>dy</c> value offsets the text origin, including em and percentage values.</summary>
    /// <param name="offsets">The <c>dx</c>/<c>dy</c> attributes.</param>
    /// <param name="expectedLeft">The expected left edge of the glyph square.</param>
    /// <param name="expectedBottom">The expected baseline (bottom edge) of the glyph square.</param>
    [Theory]
    [InlineData("", 20.0, 100.0)]
    [InlineData("dx='10' dy='5'", 30.0, 105.0)]
    [InlineData("dx='1em' dy='0.5em'", 40.0, 110.0)]
    [InlineData("dx='0.5em' dy='-0.5em'", 30.0, 90.0)]
    [InlineData("dx='5%' dy='10%'", 30.0, 120.0)]
    [InlineData("dx='1in' dy='3pt'", 116.0, 104.0)]
    [InlineData("dx='5 100' dy='5, 100'", 25.0, 105.0)]
    [InlineData("dx='calc(1em + 5px)'", 45.0, 100.0)]
    public void SvgCodec_Load_TextDxDy_OffsetsOrigin(string offsets, double expectedLeft, double expectedBottom)
    {
        // Arrange: a 20px glyph (a 10 unit square) at x=20, y=100 in a 200x200 viewBox
        var svg = $"<svg viewBox='0 0 200 200'><text x='20' y='100' font-family='TestFont' font-size='20' {offsets}>A</text></svg>";

        // Act
        var surface = SvgLengthTestSupport.Render(svg, 200, 200);

        // Assert: the 10x10 square spans [expectedLeft, expectedLeft + 10) x [expectedBottom - 10, expectedBottom)
        var midX = (int)Math.Floor(expectedLeft + 5);
        var midY = (int)Math.Floor(expectedBottom - 5);
        Assert.Equal(255, surface[midX, midY].A);
        Assert.Equal(10.0, SvgLengthTestSupport.RowCoverage(surface, midY), 0.05);
        Assert.Equal(10.0, SvgLengthTestSupport.ColumnCoverage(surface, midX), 0.05);
        Assert.Equal(0, surface[(int)expectedLeft - 2, midY].A);
        Assert.Equal(0, surface[midX, (int)expectedBottom + 1].A);
    }

    /// <summary>Proves a malformed <c>dx</c>/<c>dy</c> is ignored and never fails the document.</summary>
    [Fact]
    public void SvgCodec_Load_MalformedTextDxDy_IsIgnored()
    {
        // Arrange
        const string Template =
            "<svg viewBox='0 0 200 200'><text x='20' y='100' font-family='TestFont' font-size='20' {0}>A</text></svg>";

        // Act
        var malformed = SvgLengthTestSupport.Render(string.Format(Template, "dx='10xyz' dy='calc('"), 200, 200);
        var plain = SvgLengthTestSupport.Render(string.Format(Template, string.Empty), 200, 200);

        // Assert
        AssertSurfacesEqual(plain, malformed);
    }
}
