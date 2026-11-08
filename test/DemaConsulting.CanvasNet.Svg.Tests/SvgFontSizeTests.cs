using DemaConsulting.CanvasNet.Fonts;

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
    [InlineData("<text x='20' y='150' style='font: oblique 10grad 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 0.2rad 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 0.05turn 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique -10DEG 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 10Grad 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 90deg 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 100grad 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: oblique 0.25turn 14px TestFont'>A</text>", 14.0)]
    [InlineData("<text x='20' y='150' style='font: BOLD ITALIC 14px TestFont'>A</text>", 14.0)]
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

    /// <summary>
    ///     Proves the CSS-wide keywords work as the whole shorthand value: <c>inherit</c> takes the
    ///     parent's size and family, <c>initial</c> resets the size to <c>medium</c> and the family to the default.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FontShorthandCssWideKeyword_AppliesToFontSize()
    {
        // Arrange / Act: the parent supplies the family (the shorthand keyword resets family too)
        var inherited = SvgLengthTestSupport.MeasureFontSizeOf(
            "<g font-family='TestFont' font-size='20'><text x='20' y='150' font-family='Other' font-size='40' style='font: inherit'>A</text></g>");
        var initial = SvgLengthTestSupport.Render(
            "<svg viewBox='0 0 200 200'><g font-size='20'><text x='20' y='150' font-family='TestFont' font-size='40' style='font: initial'>A</text></g></svg>", 200, 200);
        var defaults = SvgLengthTestSupport.Render("<svg viewBox='0 0 200 200'><text x='20' y='150'>A</text></svg>", 200, 200);

        // Assert: initial is the default family at 16, exactly as a text element with no font properties
        Assert.Equal(20.0, inherited, Tolerance);
        AssertSurfacesEqual(defaults, initial);
    }

    // ================================================================================================
    // CSS-wide keywords reset every font longhand, family included
    // ================================================================================================

    /// <summary>A parent font (bold italic <c>TestFont</c> at 20) every CSS-wide keyword test inherits from or resets.</summary>
    private const string StyledParent = "font-family='TestFont' font-weight='bold' font-style='italic' font-size='20'";

    /// <summary>
    ///     Proves <c>inherit</c>/<c>initial</c>/<c>unset</c> apply to <c>font-family</c> (via the
    ///     <c>font</c> shorthand and as longhands) as well as weight, style and size, with correct
    ///     semantics: <c>inherit</c>/<c>unset</c> take the parent's value, <c>initial</c> the property's
    ///     initial value, and a quoted <c>"inherit"</c> is a family name. The child must render
    ///     pixel-identically to a document written without the keyword.
    /// </summary>
    /// <param name="parentAttributes">The attributes of the parent group.</param>
    /// <param name="childAttributes">The attributes of the text element under test.</param>
    /// <param name="expectedParentAttributes">The parent attributes of the equivalent keyword-free document.</param>
    /// <param name="expectedChildAttributes">The text attributes of the equivalent keyword-free document.</param>
    [Theory]
    [InlineData(StyledParent, "font-family='Other' style='font: inherit'", StyledParent, "")]
    [InlineData(StyledParent, "font-family='Other' style='font: unset'", StyledParent, "")]
    [InlineData(StyledParent, "font-family='Other' style='FONT: INHERIT'", StyledParent, "")]
    [InlineData(StyledParent, "style='font: initial'", "", "")]
    [InlineData(StyledParent, "style='FONT: Initial'", "", "")]
    [InlineData(StyledParent, "font-family='Other' style='font-family: inherit'", StyledParent, "")]
    [InlineData(StyledParent, "font-family='Other' style='font-family: unset'", StyledParent, "")]
    [InlineData(StyledParent, "style='font-family: initial'", "font-weight='bold' font-style='italic' font-size='20'", "")]
    [InlineData(StyledParent, "style='font-weight: initial'", "font-family='TestFont' font-style='italic' font-size='20'", "")]
    [InlineData(StyledParent, "font-weight='INITIAL'", "font-family='TestFont' font-style='italic' font-size='20'", "")]
    [InlineData(StyledParent, "style='font-style: initial'", "font-family='TestFont' font-weight='bold' font-size='20'", "")]
    [InlineData(StyledParent, "font-style='Initial'", "font-family='TestFont' font-weight='bold' font-size='20'", "")]
    [InlineData(StyledParent, "style='font-family: \"inherit\"'", "font-family='Nonexistent' font-weight='bold' font-style='italic' font-size='20'", "")]
    public void SvgCodec_Load_CssWideKeywordOnFontProperties_ResetsWithCorrectSemantics(
        string parentAttributes, string childAttributes, string expectedParentAttributes, string expectedChildAttributes)
    {
        // Arrange
        const string Template = "<svg viewBox='0 0 200 200'><g {0}><text x='20' y='150' {1}>A</text></g></svg>";

        // Act
        var actual = SvgLengthTestSupport.RenderWithStyledFaces(string.Format(Template, parentAttributes, childAttributes), 200, 200);
        var expected = SvgLengthTestSupport.RenderWithStyledFaces(
            string.Format(Template, expectedParentAttributes, expectedChildAttributes), 200, 200);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    // ================================================================================================
    // oblique angles
    // ================================================================================================

    /// <summary>Proves every CSS angle unit (<c>deg</c>, <c>grad</c>, <c>rad</c>, <c>turn</c>, ASCII case-insensitive) is accepted after <c>oblique</c> up to +-90deg, and the face is italic.</summary>
    /// <param name="angle">The oblique angle.</param>
    [Theory]
    [InlineData("10deg")]
    [InlineData("10grad")]
    [InlineData("0.2rad")]
    [InlineData("0.05turn")]
    [InlineData("-10DEG")]
    [InlineData("10Grad")]
    [InlineData("90deg")]
    [InlineData("-90deg")]
    [InlineData("100grad")]
    [InlineData("0.25turn")]
    [InlineData("+15deg")]
    public void SvgCodec_Load_FontShorthandObliqueAngle_SelectsItalicFace(string angle)
    {
        // Arrange: the italic face is 65 font units wide (130 square pixels at size 20); regular would be 100
        var svg = $"<svg viewBox='0 0 200 200'><text x='20' y='150' style='font: oblique {angle} 20px TestFont'>A</text></svg>";

        // Act
        var coverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithStyledFaces(svg, 200, 200));

        // Assert
        Assert.Equal(130.0, coverage, 1.0);
    }

    /// <summary>Proves an out-of-range, wrongly unit-ed or misplaced oblique angle makes the whole <c>font</c> shorthand invalid, so it is dropped.</summary>
    /// <param name="style">The inline style holding the invalid shorthand.</param>
    [Theory]
    [InlineData("font: oblique 100deg 12px TestFont")]
    [InlineData("font: oblique -100deg 12px TestFont")]
    [InlineData("font: oblique 101grad 12px TestFont")]
    [InlineData("font: oblique 2rad 12px TestFont")]
    [InlineData("font: oblique 0.3turn 12px TestFont")]
    [InlineData("font: oblique 10xyz 12px TestFont")]
    [InlineData("font: oblique 10 deg 12px TestFont")]
    [InlineData("font: 10deg oblique 12px TestFont")]
    [InlineData("font: oblique 10 TestFont")]
    public void SvgCodec_Load_InvalidObliqueAngle_DropsShorthand(string style) =>
        Assert.Equal(30.0, SvgLengthTestSupport.MeasureFontSizeOf(
            $"<text x='20' y='150' font-family='TestFont' font-size='30' style='{style}'>A</text>"), Tolerance);

    /// <summary>
    ///     Proves the <c>font-style</c> longhand accepts <c>oblique</c> with any valid angle unit and
    ///     ignores anything that is not <c>normal</c>, <c>italic</c>, <c>oblique</c> or
    ///     <c>oblique &lt;angle&gt;</c> (extra tokens, a bad or out-of-range angle).
    /// </summary>
    /// <param name="value">The <c>font-style</c> value.</param>
    /// <param name="expectedCoverage">The ink area: 130 for the italic face, 100 for the regular face.</param>
    [Theory]
    [InlineData("italic", 130.0)]
    [InlineData("oblique", 130.0)]
    [InlineData("oblique 10grad", 130.0)]
    [InlineData("oblique 0.1turn", 130.0)]
    [InlineData("OBLIQUE 10DEG", 130.0)]
    [InlineData("normal", 100.0)]
    [InlineData("oblique abc", 100.0)]
    [InlineData("italic bold", 100.0)]
    [InlineData("oblique 100deg", 100.0)]
    [InlineData("oblique 10", 100.0)]
    [InlineData("oblique 10deg 5deg", 100.0)]
    public void SvgCodec_Load_FontStyleLonghandObliqueAngle_SelectsFaceOrIsIgnored(string value, double expectedCoverage)
    {
        // Arrange
        var svg = $"<svg viewBox='0 0 200 200'><text x='20' y='150' font-family='TestFont' font-size='20' style='font-style: {value}'>A</text></svg>";

        // Act
        var coverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithStyledFaces(svg, 200, 200));

        // Assert
        Assert.Equal(expectedCoverage, coverage, 1.0);
    }

    // ================================================================================================
    // font-family list splitting
    // ================================================================================================

    /// <summary>
    ///     Proves a registered family whose name contains a comma matches when quoted (or escaped) in
    ///     the <c>font-family</c> list, instead of the list being split at every comma, and that an
    ///     unquoted comma still separates families.
    /// </summary>
    /// <param name="family">The <c>font-family</c> value.</param>
    /// <param name="expectedCoverage">The ink area: 160 for the <c>Acme, Sans</c> face, 100 for <c>TestFont</c>.</param>
    [Theory]
    [InlineData("\"Acme, Sans\", TestFont", 160.0)]
    [InlineData("'Acme, Sans', TestFont", 160.0)]
    [InlineData("\"Acme, Sans\"", 160.0)]
    [InlineData("Acme\\, Sans, TestFont", 160.0)]
    [InlineData("\"Acme\\, Sans\", TestFont", 160.0)]
    [InlineData("Acme, Sans, TestFont", 100.0)]
    [InlineData("Nonexistent, TestFont", 100.0)]
    [InlineData("'Nonexistent', \"TestFont\"", 100.0)]
    public void SvgCodec_Load_FontFamilyQuotedComma_MatchesWholeName(string family, double expectedCoverage)
    {
        // Arrange: the Acme face is 80 font units wide (160 square pixels at size 20), TestFont is 50 (100)
        var fonts = new Dictionary<string, TrueTypeFont>
        {
            ["Acme, Sans"] = SvgLengthTestSupport.BuildSquareFont(80),
            ["TestFont"] = SvgLengthTestSupport.BuildSquareFont()
        };
        var escaped = System.Security.SecurityElement.Escape(family);
        var svg = $"<svg viewBox=\"0 0 200 200\"><text x=\"20\" y=\"150\" font-size=\"20\" font-family=\"{escaped}\">A</text></svg>";

        // Act
        var coverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFonts(svg, 200, 200, fonts));

        // Assert
        Assert.Equal(expectedCoverage, coverage, 1.0);
    }

    // ================================================================================================
    // Keyword case
    // ================================================================================================

    /// <summary>
    ///     Proves <c>font-weight</c> and <c>font-style</c> keywords are ASCII case-insensitive (CSS
    ///     Values 4, "Keywords") from a presentation attribute, an inline style and a stylesheet rule.
    /// </summary>
    /// <param name="markup">The document body.</param>
    /// <param name="expectedCoverage">The ink area: 100 regular, 160 bold, 130 italic.</param>
    [Theory]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-weight='BOLD'>A</text>", 160.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-weight='Bold'>A</text>", 160.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-weight='bOlD'>A</text>", 160.0)]
    [InlineData("<g font-weight='bold'><text x='20' y='150' font-family='TestFont' font-size='20' font-weight='NORMAL'>A</text></g>", 100.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' style='font-weight: BOLD'>A</text>", 160.0)]
    [InlineData("<g font-weight='bold'><text x='20' y='150' font-family='TestFont' font-size='20' style='font-weight: Normal'>A</text></g>", 100.0)]
    [InlineData("<style>text { font-weight: BOLD }</style><text x='20' y='150' font-family='TestFont' font-size='20'>A</text>", 160.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-style='ITALIC'>A</text>", 130.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-style='Oblique'>A</text>", 130.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' font-style='OBLIQUE 10DEG'>A</text>", 130.0)]
    [InlineData("<text x='20' y='150' font-family='TestFont' font-size='20' style='font-style: ITALIC'>A</text>", 130.0)]
    [InlineData("<g font-style='italic'><text x='20' y='150' font-family='TestFont' font-size='20' style='font-style: NORMAL'>A</text></g>", 100.0)]
    [InlineData("<g font-style='italic'><text x='20' y='150' font-family='TestFont' font-size='20' font-style='NORMAL'>A</text></g>", 100.0)]
    [InlineData("<style>text { font-style: ITALIC }</style><text x='20' y='150' font-family='TestFont' font-size='20'>A</text>", 130.0)]
    public void SvgCodec_Load_FontWeightAndStyleKeywords_AreCaseInsensitive(string markup, double expectedCoverage)
    {
        // Arrange / Act
        var coverage = SvgLengthTestSupport.TotalCoverage(
            SvgLengthTestSupport.RenderWithStyledFaces($"<svg viewBox='0 0 200 200'>{markup}</svg>", 200, 200));

        // Assert
        Assert.Equal(expectedCoverage, coverage, 1.0);
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
    [InlineData("min(1, 1px)")]
    [InlineData("calc(10% + 1)")]
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

    /// <summary>
    ///     Proves a math expression that resolves to a <em>number</em> is ignored (the size is
    ///     inherited) when <c>font-size</c> is declared in CSS - an inline style, a stylesheet rule, the
    ///     <c>font</c> shorthand or the <c>font</c> attribute - because the CSS Fonts 4 grammar
    ///     <c>&lt;length-percentage [0,inf]&gt;</c> has no <c>&lt;number&gt;</c> and SVG 2 "Attribute
    ///     syntax" applies only to a presentation attribute.
    /// </summary>
    /// <param name="fontSize">The number-typed math expression.</param>
    [Theory]
    [InlineData("calc(1 + 1)")]
    [InlineData("min(1, 2)")]
    [InlineData("max(1, 2)")]
    [InlineData("clamp(1, 2, 3)")]
    public void SvgCodec_Load_FontSizeNumberMathFromCss_IsIgnoredAndInherited(string fontSize)
    {
        // Arrange: parent 20; the value comes from a CSS source on the text element
        var sources = new[]
        {
            $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"style='font-size: {fontSize}'")}</g>",
            $"<style>text {{ font-size: {fontSize} }}</style><g font-size='20'>{SvgLengthTestSupport.Glyph(string.Empty)}</g>",
            $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"style='font: {fontSize} TestFont'")}</g>",
            $"<g font-size='20'>{SvgLengthTestSupport.Glyph($"font='{fontSize} TestFont'")}</g>"
        };

        foreach (var inner in sources)
        {
            // Act / Assert: inherited, and no exception
            Assert.Equal(20.0, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
        }
    }

    /// <summary>
    ///     Proves a number-typed math result in the <c>font-size</c> presentation <em>attribute</em> is
    ///     user units, exactly like the literal number (SVG 2 "Attribute syntax", Types 4.2).
    /// </summary>
    /// <param name="fontSize">The number-typed math expression.</param>
    /// <param name="expected">The expected font size.</param>
    [Theory]
    [InlineData("calc(5 + 5)", 10.0)]
    [InlineData("min(10, 20)", 10.0)]
    [InlineData("max(10, 20)", 20.0)]
    [InlineData("clamp(10, 15, 20)", 15.0)]
    public void SvgCodec_Load_FontSizeAttributeNumberMath_IsUserUnits(string fontSize, double expected)
    {
        // Arrange: parent 40, so an ignored value would measure 40
        var inner = $"<g font-size='40'>{SvgLengthTestSupport.Glyph($"font-size='{fontSize}'")}</g>";

        // Act / Assert
        Assert.Equal(expected, SvgLengthTestSupport.MeasureFontSizeOf(inner), Tolerance);
    }

    /// <summary>
    ///     Proves the root <c>font-size</c> attribute accepts a number-typed math result in both
    ///     <see cref="SvgCodec.GetInfo(Stream)"/> (resolving <c>2em</c>) and <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>,
    ///     while the same value in the root's inline style is ignored by both.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfoAndLoad_RootFontSizeAttributeNumberMath_Agree()
    {
        // Arrange: 2em is 20 when the attribute font size is 10, and 32 when it is ignored (16)
        const string Template =
            "<svg xmlns='http://www.w3.org/2000/svg' width='2em' height='2em' {0}><rect width='20' height='20' fill='black'/></svg>";
        var attribute = string.Format(Template, "font-size='calc(5 + 5)'");
        var inline = string.Format(Template, "style='font-size: calc(5 + 5)'");

        // Act
        using var attributeInfoStream = SvgLengthTestSupport.ToStream(attribute);
        var attributeInfo = SvgCodec.GetInfo(attributeInfoStream);
        using var attributeStream = SvgLengthTestSupport.ToStream(attribute);
        var attributeSurface = SvgCodec.Load(attributeStream, attributeInfo.Width, attributeInfo.Height);
        using var inlineInfoStream = SvgLengthTestSupport.ToStream(inline);
        var inlineInfo = SvgCodec.GetInfo(inlineInfoStream);
        using var inlineStream = SvgLengthTestSupport.ToStream(inline);
        var inlineSurface = SvgCodec.Load(inlineStream, inlineInfo.Width, inlineInfo.Height);

        // Assert: the attribute gives 20x20 and the 20x20 rect fills it (a 16 root size would give 32 and leave the corner empty)
        Assert.Equal(20, attributeInfo.Width);
        Assert.Equal(20, attributeInfo.Height);
        Assert.Equal(255, attributeSurface[19, 19].A);
        Assert.Equal(32, inlineInfo.Width);
        Assert.Equal(32, inlineInfo.Height);
        Assert.Equal(0, inlineSurface[31, 31].A);
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
    // An invalid declaration is dropped, so the next source applies (it never masks a lower source)
    // ================================================================================================

    /// <summary>
    ///     Proves an invalid <c>font-size</c> declaration is dropped when parsed, so the next valid
    ///     source applies instead of the size being inherited: the inline style, a stylesheet rule
    ///     (including a more specific rule), the presentation attribute and the <c>font</c> attribute
    ///     are each tried in turn.
    /// </summary>
    /// <param name="stylesheet">An optional <c>style</c> element.</param>
    /// <param name="textAttributes">The attributes of the <c>text</c> element.</param>
    /// <param name="expected">The size of the first valid source; the parent supplies 40, so an ignored value would measure 40.</param>
    [Theory]
    [InlineData("", "font-size='30' style='font-size: bad'", 30.0)]
    [InlineData("<style>text { font-size: 20px }</style>", "style='font-size: bad'", 20.0)]
    [InlineData("<style>text { font-size: bad }</style>", "font-size='30'", 30.0)]
    [InlineData("", "style='font-size: 20px; font-size: bad'", 20.0)]
    [InlineData("<style>#t { font-size: bad } text { font-size: 22px }</style>", "id='t'", 22.0)]
    [InlineData("", "font-size='bad' font='14px TestFont'", 14.0)]
    [InlineData("", "style='font: 12xyz TestFont' font-size='30'", 30.0)]
    [InlineData("", "font-size='bad' style='font: bad'", 40.0)]
    [InlineData("", "style='font-size: calc(5 + 5)' font-size='calc(5 + 5)'", 10.0)]
    public void SvgCodec_Load_InvalidFontSizeDeclaration_FallsThroughToNextSource(string stylesheet, string textAttributes, double expected)
    {
        // Arrange
        var inner = $"{stylesheet}<g font-size='40'>{SvgLengthTestSupport.Glyph(textAttributes)}</g>";

        // Act
        var measured = SvgLengthTestSupport.MeasureFontSizeOf(inner);

        // Assert
        Assert.Equal(expected, measured, Tolerance);
    }

    /// <summary>
    ///     Proves an invalid <c>font-weight</c>, <c>font-style</c> or <c>font-family</c> declaration is
    ///     dropped so a lower source applies, while a valid but not modeled <c>bolder</c> still masks a
    ///     lower <c>bold</c> (the declaration is valid, it just has no modeled effect).
    /// </summary>
    /// <param name="textAttributes">The attributes of the <c>text</c> element.</param>
    /// <param name="expectedCoverage">The ink area: 100 regular, 160 bold, 130 italic.</param>
    [Theory]
    [InlineData("font-weight='bold' style='font-weight: abc'", 160.0)]
    [InlineData("font-weight='bold' style='font-weight: 700; font-weight: abc'", 160.0)]
    [InlineData("font-weight='bold' style='font-weight: bolder'", 100.0)]
    [InlineData("font-style='italic' style='font-style: tilted'", 130.0)]
    [InlineData("font-style='italic' style='font-style: oblique 100deg'", 130.0)]
    [InlineData("style='font-family: &quot;&quot;'", 100.0)]
    public void SvgCodec_Load_InvalidFontWeightStyleFamily_FallsThroughToNextSource(string textAttributes, double expectedCoverage)
    {
        // Arrange: size 20, so the faces measure 100 (regular), 160 (bold) and 130 (italic)
        var svg = $"<svg viewBox='0 0 200 200'><text x='20' y='150' font-family='TestFont' font-size='20' {textAttributes}>A</text></svg>";

        // Act
        var coverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithStyledFaces(svg, 200, 200));

        // Assert
        Assert.Equal(expectedCoverage, coverage, 1.0);
    }

    /// <summary>
    ///     Proves an invalid <c>line-height</c> is dropped so a stylesheet rule applies: <c>2lh</c> in
    ///     the child's <c>font-size</c> measures the group's line height (20px, not the <c>normal</c> 12px).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_InvalidLineHeightDeclaration_FallsThroughToNextSource()
    {
        // Arrange: the group is 10px; its inline line-height is invalid, its rule says 20px
        var inner = "<style>g { line-height: 20px }</style>" +
                    $"<g font-size='10' style='line-height: 12xyz'>{SvgLengthTestSupport.Glyph("font-size='2lh'")}</g>";

        // Act
        var measured = SvgLengthTestSupport.MeasureFontSizeOf(inner);

        // Assert
        Assert.Equal(40.0, measured, Tolerance);
    }

    /// <summary>
    ///     Proves only font declarations are filtered at parse time: an invalid value of any other
    ///     property still masks a lower source exactly as before, so a stylesheet's <c>fill</c> does
    ///     not show through an unparsable inline <c>fill</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_InvalidNonFontDeclaration_StillMasksLowerSources()
    {
        // Arrange
        const string Shape = "<rect width='50' height='50' style='fill: not-a-paint'/>";
        const string Rule = "<style>rect { fill: red }</style>";

        // Act
        var withRule = SvgLengthTestSupport.Render($"<svg viewBox='0 0 200 200'>{Rule}{Shape}</svg>", 200, 200);
        var withoutRule = SvgLengthTestSupport.Render($"<svg viewBox='0 0 200 200'>{Shape}</svg>", 200, 200);

        // Assert
        Assert.True(SvgLengthTestSupport.AreIdentical(withoutRule, withRule));
    }

    /// <summary>
    ///     Proves the root <c>font-size</c> follows the same fall-through in both
    ///     <see cref="SvgCodec.GetInfo(Stream)"/> (resolving <c>2em</c>) and <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>:
    ///     an invalid inline <c>font-size</c> is dropped and the attribute applies.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfoAndLoad_RootInvalidInlineFontSize_FallsThroughToAttribute()
    {
        // Arrange: 2em is 20 with the attribute's 10, and 32 when the invalid inline value masked it (16)
        const string Svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='2em' height='2em' font-size='10' style='font-size: bad'>" +
            "<rect width='20' height='20' fill='black'/></svg>";

        // Act
        using var infoStream = SvgLengthTestSupport.ToStream(Svg);
        var info = SvgCodec.GetInfo(infoStream);
        using var stream = SvgLengthTestSupport.ToStream(Svg);
        var surface = SvgCodec.Load(stream, info.Width, info.Height);

        // Assert
        Assert.Equal(20, info.Width);
        Assert.Equal(20, info.Height);
        Assert.Equal(255, surface[19, 19].A);
    }

    // ================================================================================================
    // font shorthand: each component at most once, size needs a unit
    // ================================================================================================

    /// <summary>
    ///     Proves a <c>font</c> shorthand that repeats a style, variant, weight or stretch component (or
    ///     has more <c>normal</c> tokens than free slots) is invalid as a whole, leaving the lower source in force.
    /// </summary>
    /// <param name="shorthand">The invalid shorthand value.</param>
    [Theory]
    [InlineData("bold bold 12px TestFont")]
    [InlineData("bold 700 12px TestFont")]
    [InlineData("bold bolder 12px TestFont")]
    [InlineData("italic italic 12px TestFont")]
    [InlineData("italic oblique 12px TestFont")]
    [InlineData("oblique 10deg italic 12px TestFont")]
    [InlineData("small-caps small-caps 12px TestFont")]
    [InlineData("condensed expanded 12px TestFont")]
    [InlineData("normal normal normal normal bold 12px TestFont")]
    public void SvgCodec_Load_FontShorthandDuplicateComponent_IsDropped(string shorthand) =>
        Assert.Equal(30.0, SvgLengthTestSupport.MeasureFontSizeOf(
            $"<text x='20' y='150' font-family='TestFont' font-size='30' style='font: {shorthand}'>A</text>"), Tolerance);

    /// <summary>
    ///     Proves <c>normal</c> may repeat up to the number of free component slots (it is a member
    ///     of the style, variant, weight and stretch components alike).
    /// </summary>
    /// <param name="shorthand">The valid shorthand value.</param>
    [Theory]
    [InlineData("normal normal 12px TestFont")]
    [InlineData("normal normal normal normal 12px TestFont")]
    [InlineData("normal bold normal 12px TestFont")]
    [InlineData("italic normal bold small-caps 12px TestFont")]
    public void SvgCodec_Load_FontShorthandRepeatedNormal_IsAccepted(string shorthand) =>
        Assert.Equal(12.0, SvgLengthTestSupport.MeasureFontSizeOf(
            $"<text x='20' y='150' font-family='Other' font-size='30' style='font: {shorthand}'>A</text>"), Tolerance);

    /// <summary>
    ///     Proves the shorthand size must carry a unit: a unitless non-zero number is not a length (CSS
    ///     Values 4), and a number before the family is read as the weight, which leaves no size.
    /// </summary>
    /// <param name="shorthand">The invalid shorthand value.</param>
    [Theory]
    [InlineData("0.5 TestFont")]
    [InlineData("bold 0.5 TestFont")]
    [InlineData("12 TestFont")]
    [InlineData("400 12 TestFont")]
    [InlineData("bold 12.5 TestFont")]
    public void SvgCodec_Load_FontShorthandUnitlessSize_IsDropped(string shorthand) =>
        Assert.Equal(30.0, SvgLengthTestSupport.MeasureFontSizeOf(
            $"<text x='20' y='150' font-family='TestFont' font-size='30' style='font: {shorthand}'>A</text>"), Tolerance);

    /// <summary>
    ///     Proves a bare <c>0</c> is a valid shorthand size: the declaration is accepted (it masks the
    ///     <c>font-size</c> attribute) and a zero size renders no text.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FontShorthandZeroSize_IsAccepted()
    {
        // Arrange
        var svg = "<svg viewBox='0 0 200 200'><text x='20' y='150' font-family='TestFont' font-size='30' style='font: 0 TestFont'>A</text></svg>";

        // Act
        var coverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.Render(svg, 200, 200));

        // Assert
        Assert.Equal(0.0, coverage, 0.0001);
    }

    // ================================================================================================
    // font-family: backslash-newline
    // ================================================================================================

    /// <summary>
    ///     Proves a backslash-newline inside a quoted <c>font-family</c> name is a line continuation (the
    ///     lines join), and outside a quoted name is an invalid escape that drops the declaration so the
    ///     <c>font-family</c> attribute applies. The newline needs a <c>style</c> element, because XML
    ///     attribute-value normalization turns a newline in an attribute into a space.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FontFamilyBackslashNewline_JoinsLinesInStringAndDropsInIdent()
    {
        // Arrange: FooBar is 80 font units wide (160 square pixels at size 20), TestFont 50 (100)
        var fonts = new Dictionary<string, TrueTypeFont>
        {
            ["FooBar"] = SvgLengthTestSupport.BuildSquareFont(80),
            ["TestFont"] = SvgLengthTestSupport.BuildSquareFont()
        };
        const string Text = "<text x='20' y='150' font-size='20' font-family='TestFont'>A</text>";
        var quoted = $"<svg viewBox='0 0 200 200'><style>text {{ font-family: \"Foo\\\nBar\", TestFont }}</style>{Text}</svg>";
        var ident = $"<svg viewBox='0 0 200 200'><style>text {{ font-family: Foo\\\nBar }}</style>{Text}</svg>";

        // Act
        var quotedCoverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFonts(quoted, 200, 200, fonts));
        var identCoverage = SvgLengthTestSupport.TotalCoverage(SvgLengthTestSupport.RenderWithFonts(ident, 200, 200, fonts));

        // Assert
        Assert.Equal(160.0, quotedCoverage, 1.0);
        Assert.Equal(100.0, identCoverage, 1.0);
    }

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
