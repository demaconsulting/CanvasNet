using System.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Unit tests for the CSS <c>&lt;length&gt;</c> unit handling of <see cref="SvgCodec"/>:
///     absolute, font-relative and viewport-relative units, percentages, <c>calc()</c> and its
///     siblings, the three-class malformed-value policy, and the root <c>width</c>/<c>height</c>
///     conversion reported by <see cref="SvgCodec.GetInfo(Stream)"/>.
/// </summary>
/// <remarks>
///     Each test measures a rendered bar's coverage (see <see cref="SvgLengthTestSupport"/>), so an
///     assertion reads as "this attribute produced a bar N user units wide" rather than checking an
///     arbitrary pixel. All documents render 1:1 (the surface equals the viewBox size).
/// </remarks>
public class SvgLengthUnitTests
{
    /// <summary>The absolute tolerance, in pixels, for a measured bar width (8-bit alpha quantization of the edge pixel).</summary>
    private const double Tolerance = 0.02;

    /// <summary>
    ///     Renders a 10-high bar whose <c>width</c> attribute is <paramref name="width"/> inside a
    ///     400x40 document and returns its measured width.
    /// </summary>
    /// <param name="width">The raw <c>width</c> attribute value under test.</param>
    /// <param name="rootAttributes">Extra attributes for the root <c>svg</c> element.</param>
    /// <param name="groupAttributes">Attributes for a <c>g</c> wrapping the bar (for example a <c>font-size</c>).</param>
    /// <returns>The bar's measured width in user units.</returns>
    private static double BarWidth(string width, string rootAttributes = "", string groupAttributes = "")
    {
        var svg = $"<svg viewBox='0 0 400 40' {rootAttributes}><g {groupAttributes}><rect x='0' y='0' width='{width}' height='10' fill='black'/></g></svg>";
        return SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(svg, 400, 40), 5);
    }

    /// <summary>Asserts two same-size surfaces are pixel-identical.</summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    private static void AssertSurfacesEqual(Surface expected, Surface actual)
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
    // Absolute units
    // ================================================================================================

    /// <summary>Proves each absolute unit converts to user units at the CSS reference ratio (1in = 96).</summary>
    /// <param name="width">The raw width value.</param>
    /// <param name="expected">The expected width in user units.</param>
    [Theory]
    [InlineData("96px", 96.0)]
    [InlineData("1in", 96.0)]
    [InlineData("72pt", 96.0)]
    [InlineData("6pc", 96.0)]
    [InlineData("2.54cm", 96.0)]
    [InlineData("25.4mm", 96.0)]
    [InlineData("101.6Q", 96.0)]
    [InlineData("12pt", 16.0)]
    [InlineData("1pc", 16.0)]
    [InlineData("10mm", 37.7952756)]
    [InlineData("40Q", 37.7952756)]
    [InlineData(".5in", 48.0)]
    [InlineData("+5px", 5.0)]
    [InlineData("1e1px", 10.0)]
    [InlineData("1e2", 100.0)]
    [InlineData("10PX", 10.0)]
    [InlineData("10Px", 10.0)]
    [InlineData("1IN", 96.0)]
    [InlineData("2.54CM", 96.0)]
    [InlineData("72PT", 96.0)]
    [InlineData("101.6q", 96.0)]
    public void SvgCodec_Load_AbsoluteUnit_ConvertsToUserUnits(string width, double expected)
    {
        // Arrange / Act: render a bar whose width carries the unit (units match case-insensitively, as CSS requires)
        var measured = BarWidth(width);

        // Assert: the bar is the converted width
        Assert.Equal(expected, measured, Tolerance);
    }

    /// <summary>Proves equivalent absolute lengths render pixel-identically (the conversion is done in double precision).</summary>
    /// <param name="equivalent">A length equal to one inch.</param>
    [Theory]
    [InlineData("96px")]
    [InlineData("2.54cm")]
    [InlineData("25.4mm")]
    [InlineData("72pt")]
    [InlineData("6pc")]
    [InlineData("101.6Q")]
    [InlineData("calc(2.54cm * 1)")]
    public void SvgCodec_Load_EquivalentInch_RendersPixelIdentical(string equivalent)
    {
        // Arrange: one document sized in plain user units, one in the equivalent unit
        const string Template = "<svg viewBox='0 0 120 120'><rect x='3.5' y='3.5' width='{0}' height='{0}' fill='black'/></svg>";

        // Act
        var baseline = SvgLengthTestSupport.Render(string.Format(Template, "1in"), 120, 120);
        var other = SvgLengthTestSupport.Render(string.Format(Template, equivalent), 120, 120);

        // Assert
        AssertSurfacesEqual(baseline, other);
    }

    /// <summary>Proves a unit-less value and the <c>px</c> form of the same value render identically (no regression for unit-less input).</summary>
    [Fact]
    public void SvgCodec_Load_PxAndUnitless_RenderPixelIdentical()
    {
        // Arrange
        const string Template = "<svg viewBox='0 0 60 60'><rect x='{0}' y='{0}' width='{1}' height='{1}' fill='black' stroke='black' stroke-width='{2}'/></svg>";

        // Act
        var plain = SvgLengthTestSupport.Render(string.Format(Template, "2.25", "30.5", "1.5"), 60, 60);
        var units = SvgLengthTestSupport.Render(string.Format(Template, "2.25px", "30.5px", "1.5px"), 60, 60);

        // Assert
        AssertSurfacesEqual(plain, units);
    }

    /// <summary>Proves the stroke properties accept units: <c>stroke-width</c>, <c>stroke-dashoffset</c> and each <c>stroke-dasharray</c> entry.</summary>
    [Fact]
    public void SvgCodec_Load_StrokeLengths_AcceptUnits()
    {
        // Arrange: a stroke of 3pt (= 4px) through the middle of a bar
        const string Thick = "<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-width='3pt'/></svg>";

        // Act
        var thick = SvgLengthTestSupport.Render(Thick, 100, 40);

        // Assert: the column through the line is 4 pixels of ink
        Assert.Equal(4.0, SvgLengthTestSupport.ColumnCoverage(thick, 50), Tolerance);

        // Arrange: dash pattern and offset in units versus the same values in user units
        const string DashTemplate =
            "<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-width='6' stroke-dasharray='{0}' stroke-dashoffset='{1}'/></svg>";

        // Act
        var withUnits = SvgLengthTestSupport.Render(string.Format(DashTemplate, "0.5in, 10px 15pt", "7.5pt"), 100, 40);
        var plain = SvgLengthTestSupport.Render(string.Format(DashTemplate, "48, 10 20", "10"), 100, 40);

        // Assert: pixel-identical, and actually dashed
        AssertSurfacesEqual(plain, withUnits);
        Assert.InRange(SvgLengthTestSupport.RowCoverage(withUnits, 20), 1.0, 99.0);
    }

    /// <summary>Proves a dash-array entry may be a math function whose arguments contain spaces and commas.</summary>
    [Fact]
    public void SvgCodec_Load_DashArrayEntryMathFunction_IsOneEntry()
    {
        // Arrange
        const string Template =
            "<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-width='6' stroke-dasharray='{0}'/></svg>";

        // Act
        var viaMath = SvgLengthTestSupport.Render(string.Format(Template, "max(10px, 2em) calc(5px + 5px)"), 100, 40);
        var plain = SvgLengthTestSupport.Render(string.Format(Template, "32 10"), 100, 40);

        // Assert: max(10px, 2em) is 32 (em = 16 at the root)
        AssertSurfacesEqual(plain, viaMath);
    }

    /// <summary>Proves geometry attributes other than <c>width</c> accept units: <c>x</c>, <c>r</c>, <c>rx</c>, <c>x1</c>/<c>x2</c>.</summary>
    [Fact]
    public void SvgCodec_Load_OtherGeometryAttributes_AcceptUnits()
    {
        // Arrange: shapes positioned and sized with a mix of units, versus plain user units
        const string WithUnits =
            "<svg viewBox='0 0 200 100'>" +
            "<rect x='0.5in' y='1in' width='1in' height='0.25in' rx='6pt' fill='black'/>" +
            "<circle cx='25mm' cy='20mm' r='5mm' fill='black'/>" +
            "<ellipse cx='8pc' cy='3pc' rx='2pc' ry='1pc' fill='black'/>" +
            "<line x1='1in' y1='5pt' x2='2in' y2='5pt' stroke='black' stroke-width='1pt'/>" +
            "</svg>";
        var plainDoc = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "<svg viewBox='0 0 200 100'>" +
            "<rect x='48' y='96' width='96' height='24' rx='8' fill='black'/>" +
            "<circle cx='{0}' cy='{1}' r='{2}' fill='black'/>" +
            "<ellipse cx='128' cy='48' rx='32' ry='16' fill='black'/>" +
            "<line x1='96' y1='{3}' x2='192' y2='{3}' stroke='black' stroke-width='{4}'/>" +
            "</svg>",
            25.0 * 96.0 / 25.4,
            20.0 * 96.0 / 25.4,
            5.0 * 96.0 / 25.4,
            5.0 * 96.0 / 72.0,
            96.0 / 72.0);

        // Act
        var viaUnits = SvgLengthTestSupport.Render(WithUnits, 200, 100);
        var plain = SvgLengthTestSupport.Render(plainDoc, 200, 100);

        // Assert
        AssertSurfacesEqual(plain, viaUnits);
    }

    // ================================================================================================
    // Percentages (regression) and font-relative units
    // ================================================================================================

    /// <summary>Proves a percentage still resolves against the viewport width (existing behavior).</summary>
    [Fact]
    public void SvgCodec_Load_PercentWidth_ResolvesAgainstViewportWidth() =>
        Assert.Equal(200.0, BarWidth("50%"), Tolerance);

    /// <summary>Proves <c>em</c> resolves against the element's own computed font size.</summary>
    [Fact]
    public void SvgCodec_Load_EmLength_UsesElementFontSize() =>
        Assert.Equal(20.0, BarWidth("2em", groupAttributes: "font-size='10'"), Tolerance);

    /// <summary>Proves <c>em</c> on a property of the same element that sets <c>font-size</c> uses that element's own new size.</summary>
    [Fact]
    public void SvgCodec_Load_EmOnElementWithOwnFontSize_UsesItsOwnSize()
    {
        // Arrange: the rect sets both font-size and width in em
        const string Svg = "<svg viewBox='0 0 400 40' font-size='10'><rect x='0' y='0' width='3em' height='10' font-size='20' fill='black'/></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(Svg, 400, 40), 5);

        // Assert: 3 * 20, not 3 * 10 (the inherited size)
        Assert.Equal(60.0, measured, Tolerance);
    }

    /// <summary>Proves the initial font size (16) is the <c>em</c> basis when no font size is set.</summary>
    [Fact]
    public void SvgCodec_Load_EmWithoutFontSize_Uses16() =>
        Assert.Equal(32.0, BarWidth("2em"), Tolerance);

    /// <summary>Proves a nested <c>font-size</c> in <c>em</c> compounds through the parent, and <c>em</c> in width then uses the result.</summary>
    [Fact]
    public void SvgCodec_Load_NestedEmFontSize_ResolvesAgainstParent()
    {
        // Arrange: 20 then 2em (= 40) then width 1em (= 40)
        const string Svg =
            "<svg viewBox='0 0 400 40'><g font-size='20'><g font-size='2em'><rect x='0' y='0' width='1em' height='10' fill='black'/></g></g></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(Svg, 400, 40), 5);

        // Assert
        Assert.Equal(40.0, measured, Tolerance);
    }

    /// <summary>Proves <c>rem</c> resolves against the root element's font size regardless of any deeper font size.</summary>
    [Fact]
    public void SvgCodec_Load_RemLength_UsesRootFontSize() =>
        Assert.Equal(20.0, BarWidth("2rem", rootAttributes: "font-size='10'", groupAttributes: "font-size='50'"), Tolerance);

    /// <summary>Proves <c>rem</c> is 16-based when the root sets no font size.</summary>
    [Fact]
    public void SvgCodec_Load_RemWithoutRootFontSize_Uses16() =>
        Assert.Equal(32.0, BarWidth("2rem", groupAttributes: "font-size='50'"), Tolerance);

    /// <summary>Proves <c>rem</c> keeps its meaning inside mask content, which restarts the presentation cascade.</summary>
    [Fact]
    public void SvgCodec_Load_RemInsideMaskContent_UsesRootFontSize()
    {
        // Arrange: a mask whose only content is a white bar 2rem wide; the root font size is 10
        const string Svg =
            "<svg viewBox='0 0 400 40' font-size='10'>" +
            "<mask id='m'><rect x='0' y='0' width='2rem' height='10' fill='white'/></mask>" +
            "<rect x='0' y='0' width='400' height='10' fill='black' mask='url(#m)'/></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(Svg, 400, 40), 5);

        // Assert: 2 * 10 (the root's size), proving the restarted cascade kept the root basis
        Assert.Equal(20.0, measured, 0.05);
    }

    /// <summary>Proves <c>ex</c> and <c>ch</c> fall back to half an em when the font has no <c>x</c>/<c>0</c> glyph.</summary>
    /// <param name="unit">The unit under test.</param>
    [Theory]
    [InlineData("ex")]
    [InlineData("ch")]
    public void SvgCodec_Load_ExAndChWithoutGlyph_FallBackToHalfEm(string unit) =>
        Assert.Equal(20.0, BarWidth("2" + unit, groupAttributes: "font-family='TestFont' font-size='20'"), Tolerance);

    /// <summary>Proves <c>ex</c> and <c>ch</c> measure the real font's <c>x</c> height and <c>0</c> advance.</summary>
    [Fact]
    public void SvgCodec_Load_ExAndChWithRealFont_MeasureTheFont()
    {
        // Arrange: measure the bundled sans-serif font directly, as the codec's resolver does
        var resolver = new FontResolver(new FontResolverOptions { UseSystemFonts = false });
        var font = resolver.Resolve("sans-serif", bold: false, italic: false, out _).Font;
        var xHeight = font.GetGlyphOutline(font.GetGlyphIndex('x')).GetBounds(1f).Height / (double)font.UnitsPerEm;
        var zeroAdvance = font.GetAdvanceWidth(font.GetGlyphIndex('0')) / (double)font.UnitsPerEm;

        // Act: bars 10ex and 10ch wide at font size 20, rendered with only the bundled fonts
        var exWidth = MeasureBar("10ex");
        var chWidth = MeasureBar("10ch");

        // Assert: within a hair of the directly measured ratios, and clearly not the 0.5em fallback
        Assert.Equal(10 * 20 * xHeight, exWidth, 0.05);
        Assert.Equal(10 * 20 * zeroAdvance, chWidth, 0.05);
        Assert.NotEqual(100.0, exWidth, 1.0);

        static double MeasureBar(string width)
        {
            var svg = $"<svg viewBox='0 0 400 40'><g font-family='sans-serif' font-size='20'><rect width='{width}' height='10' fill='black'/></g></svg>";
            using var stream = SvgLengthTestSupport.ToStream(svg);
            var surface = SvgCodec.Load(stream, 400, 40, null, new SvgLoadOptions { UseSystemFonts = false });
            return SvgLengthTestSupport.RowCoverage(surface, 5);
        }
    }

    /// <summary>Proves <c>lh</c> is 1.2em for <c>normal</c> and follows a declared <c>line-height</c> (number, length, shorthand).</summary>
    /// <param name="groupAttributes">The attributes on the wrapping group.</param>
    /// <param name="expected">The expected width of a <c>1lh</c> bar.</param>
    [Theory]
    [InlineData("font-size='10'", 12.0)]
    [InlineData("font-size='10' style='line-height:2'", 20.0)]
    [InlineData("font-size='10' style='line-height:30px'", 30.0)]
    [InlineData("font-size='10' style='line-height:150%'", 15.0)]
    [InlineData("style='font:10px/1.5 serif'", 15.0)]
    [InlineData("font-size='10' style='line-height:normal'", 12.0)]
    public void SvgCodec_Load_LhUnit_FollowsLineHeight(string groupAttributes, double expected) =>
        Assert.Equal(expected, BarWidth("1lh", groupAttributes: groupAttributes), Tolerance);

    /// <summary>Proves a unitless <c>line-height</c> re-multiplies by each descendant's own font size, while a length is fixed at the declaring element.</summary>
    [Fact]
    public void SvgCodec_Load_LineHeightInheritance_NumberVersusLength()
    {
        // Arrange: parents declare 2 (a number) and 20px (a length); the child changes font-size to 50
        var number = BarWidth("1lh", groupAttributes: "style='line-height:2'", rootAttributes: string.Empty);
        const string NestedNumber =
            "<svg viewBox='0 0 400 40'><g style='line-height:2' font-size='10'><g font-size='50'><rect width='1lh' height='10' fill='black'/></g></g></svg>";
        const string NestedLength =
            "<svg viewBox='0 0 400 40'><g style='line-height:20px' font-size='10'><g font-size='50'><rect width='1lh' height='10' fill='black'/></g></g></svg>";

        // Act
        var viaNumber = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(NestedNumber, 400, 40), 5);
        var viaLength = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(NestedLength, 400, 40), 5);

        // Assert: 2 * 50 = 100 for the number; 20 for the length; and the plain-group sanity value
        Assert.Equal(100.0, viaNumber, Tolerance);
        Assert.Equal(20.0, viaLength, Tolerance);
        Assert.Equal(32.0, number, Tolerance);
    }

    /// <summary>Proves <c>rlh</c> resolves against the root element's line height and font size.</summary>
    [Fact]
    public void SvgCodec_Load_RlhUnit_UsesRootLineHeight()
    {
        // Arrange: root font-size 10, line-height 2; the group changes both
        const string Svg =
            "<svg viewBox='0 0 400 40' font-size='10' style='line-height:2'>" +
            "<g font-size='50' style='line-height:5'><rect width='1rlh' height='10' fill='black'/></g></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(Svg, 400, 40), 5);

        // Assert: 2 * 10 (root multiplier times root font size)
        Assert.Equal(20.0, measured, Tolerance);
    }

    // ================================================================================================
    // Viewport-relative units
    // ================================================================================================

    /// <summary>Proves the viewport units are percentages of the root viewport (400x40 here).</summary>
    /// <param name="width">The raw width value.</param>
    /// <param name="expected">The expected width in user units.</param>
    [Theory]
    [InlineData("10vw", 40.0)]
    [InlineData("10vh", 4.0)]
    [InlineData("10vmin", 4.0)]
    [InlineData("10vmax", 40.0)]
    [InlineData("10VW", 40.0)]
    public void SvgCodec_Load_ViewportUnit_ResolvesAgainstRootViewport(string width, double expected) =>
        Assert.Equal(expected, BarWidth(width), Tolerance);

    /// <summary>Proves a symbol viewport changes what <c>%</c> means but never what <c>vw</c> means.</summary>
    [Fact]
    public void SvgCodec_Load_ViewportUnitInsideSymbol_StillUsesRootViewport()
    {
        // Arrange: a symbol instantiated 100 wide (so % is 100-based) inside a 400-wide root
        const string Template =
            "<svg viewBox='0 0 400 40'><defs><symbol id='s'><rect x='0' y='0' width='{0}' height='10' fill='black'/></symbol></defs>" +
            "<use href='#s' x='0' y='0' width='100' height='20'/></svg>";

        // Act
        var viaPercent = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(string.Format(Template, "10%"), 400, 40), 5);
        var viaVw = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(string.Format(Template, "10vw"), 400, 40), 5);

        // Assert: 10% of the symbol viewport (100) versus 10vw of the root (400)
        Assert.Equal(10.0, viaPercent, Tolerance);
        Assert.Equal(40.0, viaVw, Tolerance);
    }

    // ================================================================================================
    // calc(), min(), max(), clamp()
    // ================================================================================================

    /// <summary>Proves <c>calc()</c> and friends evaluate mixed units, percentages and nesting.</summary>
    /// <param name="width">The raw width value.</param>
    /// <param name="expected">The expected width in user units.</param>
    [Theory]
    [InlineData("calc(10px + 1em)", 20.0)]
    [InlineData("calc(100% - 2*5px)", 390.0)]
    [InlineData("calc(2 * (3px + 1px))", 8.0)]
    [InlineData("calc(1in / 2)", 48.0)]
    [InlineData("calc(10px*2)", 20.0)]
    [InlineData("calc(-5px + 15px)", 10.0)]
    [InlineData("CALC(1PX + 1PX)", 2.0)]
    [InlineData("calc(calc(1px + 2px) * 2)", 6.0)]
    [InlineData("calc( 1px  +  2px )", 3.0)]
    [InlineData("min(10px, 2em)", 10.0)]
    [InlineData("max(10px, 2em)", 20.0)]
    [InlineData("clamp(5px, 50px, 20px)", 20.0)]
    [InlineData("clamp(5px, 1px, 20px)", 5.0)]
    [InlineData("min(1px, 2px, 3px)", 1.0)]
    [InlineData("calc(min(10px, 5px) + max(1px, 2px))", 7.0)]
    [InlineData("calc(10% / 2)", 20.0)]
    public void SvgCodec_Load_MathFunction_Evaluates(string width, double expected) =>
        Assert.Equal(expected, BarWidth(width, groupAttributes: "font-size='10'"), Tolerance);

    // ================================================================================================
    // Malformed values (class S: strict)
    // ================================================================================================

    /// <summary>Proves a malformed strict length throws <see cref="InvalidDataException"/> - and nothing else, in particular never a raw <see cref="FormatException"/>.</summary>
    /// <param name="width">The malformed width value.</param>
    [Theory]
    [InlineData("10xyz")]
    [InlineData("10furlong")]
    [InlineData("10vi")]
    [InlineData("12 px")]
    [InlineData("px")]
    [InlineData("1e")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e6in")]
    [InlineData("1e400px")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1px 2px")]
    [InlineData("calc(")]
    [InlineData("calc()")]
    [InlineData("calc(1px +)")]
    [InlineData("calc(1px + 2)")]
    [InlineData("calc(1px+2px)")]
    [InlineData("calc(1px -2px)")]
    [InlineData("calc(1px / 0)")]
    [InlineData("calc(1px * 2px)")]
    [InlineData("calc(1px / 2px)")]
    [InlineData("calc(1px) trailing")]
    [InlineData("calc(foo(1px))")]
    [InlineData("calc(var(--x))")]
    [InlineData("clamp(1px, 2px)")]
    [InlineData("min()")]
    [InlineData("min(1px, 2)")]
    [InlineData("calc(1e6px * 1e6)")]
    public void SvgCodec_Load_MalformedGeometryLength_ThrowsInvalidDataException(string width)
    {
        // Arrange
        var svg = $"<svg viewBox='0 0 400 40'><rect x='0' y='0' width='{width}' height='10' fill='black'/></svg>";

        // Act / Assert: exactly InvalidDataException (Assert.Throws rejects derived or different types)
        using var stream = SvgLengthTestSupport.ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 400, 40));
    }

    /// <summary>Proves an over-deep or over-long math expression is rejected rather than evaluated.</summary>
    [Fact]
    public void SvgCodec_Load_OverComplexMathExpression_ThrowsInvalidDataException()
    {
        // Arrange: 40 nested parentheses (beyond the depth cap) and a ~2,000 character sum (beyond the length cap)
        var deep = "calc(" + new string('(', 40) + "1px" + new string(')', 40) + ")";
        var longSum = "calc(" + string.Join(" + ", Enumerable.Repeat("1px", 500)) + ")";

        foreach (var width in new[] { deep, longSum })
        {
            var svg = $"<svg viewBox='0 0 400 40'><rect x='0' y='0' width='{width}' height='10' fill='black'/></svg>";

            // Act / Assert
            using var stream = SvgLengthTestSupport.ToStream(svg);
            Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 400, 40));
        }
    }

    /// <summary>Proves the strict class also covers the stroke properties, each reporting <see cref="InvalidDataException"/>.</summary>
    /// <param name="attributes">The malformed stroke attributes.</param>
    [Theory]
    [InlineData("stroke-width='3furlong'")]
    [InlineData("stroke-dashoffset='abc'")]
    [InlineData("stroke-dasharray='5px 3zz'")]
    [InlineData("stroke-dasharray='calc('")]
    public void SvgCodec_Load_MalformedStrokeLength_ThrowsInvalidDataException(string attributes)
    {
        // Arrange
        var svg = $"<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' {attributes}/></svg>";

        // Act / Assert
        using var stream = SvgLengthTestSupport.ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 100, 40));
    }

    // ================================================================================================
    // Tolerant sites (class T): gradients, patterns, mask regions
    // ================================================================================================

    /// <summary>Proves a userSpaceOnUse gradient coordinate with a unit converts to user units.</summary>
    [Fact]
    public void SvgCodec_Load_GradientCoordinateWithUnit_ConvertsToUserUnits()
    {
        // Arrange: a horizontal gradient spanning 0..1in, versus the same gradient in plain user units
        const string Template =
            "<svg viewBox='0 0 200 20'><defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0' y1='0' x2='{0}' y2='0'>" +
            "<stop offset='0' stop-color='black'/><stop offset='1' stop-color='white'/></linearGradient></defs>" +
            "<rect width='200' height='20' fill='url(#g)'/></svg>";

        // Act
        var viaUnit = SvgLengthTestSupport.Render(string.Format(Template, "1in"), 200, 20);
        var plain = SvgLengthTestSupport.Render(string.Format(Template, "96"), 200, 20);

        // Assert
        AssertSurfacesEqual(plain, viaUnit);
    }

    /// <summary>Proves an unparseable gradient coordinate falls back like an absent one rather than failing the document.</summary>
    [Fact]
    public void SvgCodec_Load_GradientCoordinateMalformed_FallsBackToDefault()
    {
        // Arrange: x2 is garbage, so the default (objectBoundingBox 1) must apply
        const string Template =
            "<svg viewBox='0 0 100 10'><defs><linearGradient id='g' {0}>" +
            "<stop offset='0' stop-color='black'/><stop offset='1' stop-color='white'/></linearGradient></defs>" +
            "<rect width='100' height='10' fill='url(#g)'/></svg>";

        // Act
        var malformed = SvgLengthTestSupport.Render(string.Format(Template, "x2='10xyz'"), 100, 10);
        var absent = SvgLengthTestSupport.Render(string.Format(Template, string.Empty), 100, 10);

        // Assert
        AssertSurfacesEqual(absent, malformed);
    }

    /// <summary>Proves a userSpaceOnUse mask region accepts units and a malformed region value falls back.</summary>
    [Fact]
    public void SvgCodec_Load_MaskRegionWithUnits_ConvertsAndMalformedFallsBack()
    {
        // Arrange: a userSpaceOnUse mask region of 1in x 1in, versus 96 x 96
        const string Template =
            "<svg viewBox='0 0 200 200'><mask id='m' maskUnits='userSpaceOnUse' x='0' y='0' width='{0}' height='{0}'>" +
            "<rect width='200' height='200' fill='white'/></mask><rect width='200' height='200' fill='black' mask='url(#m)'/></svg>";

        // Act
        var viaUnit = SvgLengthTestSupport.Render(string.Format(Template, "1in"), 200, 200);
        var plain = SvgLengthTestSupport.Render(string.Format(Template, "96"), 200, 200);
        var malformed = SvgLengthTestSupport.Render(string.Format(Template, "10xyz"), 200, 200);

        // Assert: units convert; the malformed region equals the default region (-10%..120% of the shape)
        AssertSurfacesEqual(plain, viaUnit);
        Assert.Equal(96.0, SvgLengthTestSupport.RowCoverage(viaUnit, 50), 0.05);
        Assert.Equal(200.0, SvgLengthTestSupport.RowCoverage(malformed, 50), 0.05);
    }

    /// <summary>Proves a pattern tile written in units matches the same tile in user units.</summary>
    [Fact]
    public void SvgCodec_Load_PatternTileWithUnits_MatchesUserUnits()
    {
        // Arrange: a userSpaceOnUse pattern whose tile is 0.5in square
        const string Template =
            "<svg viewBox='0 0 100 100'><defs><pattern id='p' patternUnits='userSpaceOnUse' x='0' y='0' width='{0}' height='{0}'>" +
            "<rect width='24' height='24' fill='black'/></pattern></defs><rect width='100' height='100' fill='url(#p)'/></svg>";

        // Act
        var viaUnit = SvgLengthTestSupport.Render(string.Format(Template, "0.5in"), 100, 100);
        var plain = SvgLengthTestSupport.Render(string.Format(Template, "48"), 100, 100);

        // Assert
        AssertSurfacesEqual(plain, viaUnit);
        Assert.True(SvgLengthTestSupport.TotalCoverage(viaUnit) > 0.0);
    }

    // ================================================================================================
    // Math result type: a number-typed result is user units where SVG 2 allows a <number>
    // ================================================================================================

    /// <summary>
    ///     Builds a document whose line carries one stroke property in the requested source.
    /// </summary>
    /// <param name="source">
    ///     <c>attribute</c> (presentation attribute), <c>style</c> (inline style) or <c>stylesheet</c>
    ///     (a <c>&lt;style&gt;</c> rule).
    /// </param>
    /// <param name="property">The stroke property name.</param>
    /// <param name="value">The property value; <see langword="null"/> omits the property.</param>
    /// <returns>The SVG document text.</returns>
    private static string StrokeDocument(string source, string property, string? value)
    {
        var extra = property == "stroke-dashoffset" ? " stroke-dasharray='5'" : string.Empty;
        const string Line = "x1='0' y1='20' x2='100' y2='20' stroke='black'";
        if (value == null)
        {
            return $"<svg viewBox='0 0 100 40'><line {Line}{extra}/></svg>";
        }

        return source switch
        {
            "attribute" => $"<svg viewBox='0 0 100 40'><line {Line}{extra} {property}='{value}'/></svg>",
            "style" => $"<svg viewBox='0 0 100 40'><line {Line}{extra} style='{property}: {value}'/></svg>",
            _ => $"<svg viewBox='0 0 100 40'><style>line {{ {property}: {value} }}</style><line {Line}{extra}/></svg>"
        };
    }

    /// <summary>
    ///     Proves a math expression that resolves to a <em>number</em> is user units in
    ///     <c>stroke-width</c>, each <c>stroke-dasharray</c> entry and <c>stroke-dashoffset</c>
    ///     (SVG 2 Painting: <c>&lt;length-percentage&gt; | &lt;number&gt;</c>), from an attribute, an
    ///     inline style and a stylesheet rule, rendering pixel-identically to the literal number.
    /// </summary>
    /// <param name="value">The number-typed math expression.</param>
    /// <param name="expected">The user-unit value it resolves to.</param>
    [Theory]
    [InlineData("calc(1 + 1)", 2)]
    [InlineData("min(1, 2)", 1)]
    [InlineData("max(1, 2)", 2)]
    [InlineData("clamp(1, 2, 3)", 2)]
    [InlineData("calc(2 * 3)", 6)]
    [InlineData("calc(0)", 0)]
    public void SvgCodec_Load_StrokeNumberMath_IsUserUnits(string value, int expected)
    {
        // Arrange: (property, text before the value, the value that equals the property's initial value)
        var sites = new (string Property, string Prefix, int Neutral)[]
        {
            ("stroke-width", string.Empty, 1),
            ("stroke-dasharray", "5 ", 0),
            ("stroke-dashoffset", string.Empty, 0)
        };

        foreach (var (property, prefix, neutral) in sites)
        {
            var absent = SvgLengthTestSupport.Render(StrokeDocument("attribute", property, null), 100, 40);
            foreach (var source in new[] { "attribute", "style", "stylesheet" })
            {
                // Act
                var math = SvgLengthTestSupport.Render(StrokeDocument(source, property, prefix + value), 100, 40);
                var literal = SvgLengthTestSupport.Render(StrokeDocument(source, property, prefix + expected), 100, 40);

                // Assert: identical to the literal, and (unless it is the initial value) not to the absent property
                AssertSurfacesEqual(literal, math);
                if (expected != neutral)
                {
                    Assert.False(SvgLengthTestSupport.AreIdentical(absent, math), $"{property} via {source}");
                }
            }
        }
    }

    /// <summary>
    ///     Proves a number-typed math result is user units in a geometry presentation attribute (SVG 2
    ///     "Attribute syntax", Types 4.2, inserts <c>&lt;number&gt;</c> next to <c>&lt;length&gt;</c> and
    ///     <c>&lt;length-percentage&gt;</c>): one representative per grammar family - shape size, circle
    ///     radius, line endpoint, <c>use</c> size, <c>marker</c> size and text position.
    /// </summary>
    /// <param name="value">The number-typed math expression.</param>
    /// <param name="expected">The user-unit value it resolves to.</param>
    [Theory]
    [InlineData("calc(1 + 1)", 2)]
    [InlineData("min(1, 2)", 1)]
    [InlineData("max(1, 2)", 2)]
    [InlineData("clamp(1, 2, 3)", 2)]
    [InlineData("calc(2 * 3)", 6)]
    [InlineData("calc(0)", 0)]
    public void SvgCodec_Load_AttributeNumberMath_GeometryIsUserUnits(string value, int expected)
    {
        // Arrange: each builder places the value in one attribute of a different grammar family
        var builders = new (string Name, Func<string, string> Build)[]
        {
            ("rect width", v => $"<rect x='0' y='0' width='{v}' height='10' fill='black'/>"),
            ("circle r", v => $"<circle cx='50' cy='20' r='{v}' fill='black'/>"),
            ("line x2", v => $"<line x1='0' y1='20' x2='{v}' y2='20' stroke='black' stroke-width='4'/>"),
            ("use width",
                v => "<defs><symbol id='s' viewBox='0 0 10 10'><rect width='10' height='10' fill='black'/></symbol></defs>" +
                     $"<use href='#s' x='10' y='5' width='{v}' height='10'/>"),
            ("marker markerWidth",
                v => $"<defs><marker id='m' viewBox='0 0 10 4' markerWidth='{v}' markerHeight='4' refX='0' refY='2'><rect width='10' height='4' fill='black'/></marker></defs>" +
                     "<line x1='10' y1='20' x2='90' y2='20' stroke='black' marker-end='url(#m)'/>"),
            ("text x", v => $"<text x='{v}' y='30' font-family='TestFont' font-size='20'>A</text>")
        };

        foreach (var (name, build) in builders)
        {
            // Act
            var math = SvgLengthTestSupport.Render($"<svg viewBox='0 0 100 40'>{build(value)}</svg>", 100, 40);
            var literal = SvgLengthTestSupport.Render($"<svg viewBox='0 0 100 40'>{build(expected.ToString(System.Globalization.CultureInfo.InvariantCulture))}</svg>", 100, 40);
            var other = SvgLengthTestSupport.Render($"<svg viewBox='0 0 100 40'>{build((expected + 10).ToString(System.Globalization.CultureInfo.InvariantCulture))}</svg>", 100, 40);

            // Assert: the math result is the literal's user units (and the attribute is not ignored)
            AssertSurfacesEqual(literal, math);
            Assert.False(SvgLengthTestSupport.AreIdentical(other, math), name);
        }
    }

    /// <summary>
    ///     Proves a mixed-type math expression (a number combined with a length or percentage) stays
    ///     invalid in the strict class, for geometry, <c>stroke-width</c>, a dash entry and the dash offset.
    /// </summary>
    /// <param name="value">The mixed-type math expression.</param>
    [Theory]
    [InlineData("calc(1 + 1px)")]
    [InlineData("calc(10% + 1)")]
    [InlineData("calc(1px * 1px)")]
    [InlineData("min(1, 1px)")]
    public void SvgCodec_Load_MathTypeMix_StillThrowsInvalidDataException(string value)
    {
        // Arrange: the same expression as a geometry length, a stroke width, a dash entry and a dash offset
        var documents = new[]
        {
            $"<svg viewBox='0 0 100 40'><rect x='0' y='0' width='{value}' height='10' fill='black'/></svg>",
            $"<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-width='{value}'/></svg>",
            $"<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-dasharray='5 {value}'/></svg>",
            $"<svg viewBox='0 0 100 40'><line x1='0' y1='20' x2='100' y2='20' stroke='black' stroke-dasharray='5' stroke-dashoffset='{value}'/></svg>"
        };

        foreach (var svg in documents)
        {
            // Act / Assert
            using var stream = SvgLengthTestSupport.ToStream(svg);
            Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 100, 40));
        }
    }

    /// <summary>Proves the controls for the number-typed acceptance: a literal number and length-typed math still render as before.</summary>
    /// <param name="width">The accepted width value.</param>
    /// <param name="expected">The expected width in user units.</param>
    [Theory]
    [InlineData("10", 10.0)]
    [InlineData("calc(2 * 10px)", 20.0)]
    [InlineData("max(10px, 2em)", 32.0)]
    [InlineData("calc(1px * 2 + 3px)", 5.0)]
    public void SvgCodec_Load_MathResultIsLengthOrLiteralNumber_StillAccepted(string width, double expected) =>
        Assert.Equal(expected, BarWidth(width), Tolerance);

    /// <summary>
    ///     Proves a number-typed math result in the root <c>width</c>/<c>height</c> is user units (px),
    ///     like a literal (SVG 2 Types 4.2; Coordinates 8.1), and that <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
    ///     agrees with <see cref="SvgCodec.GetInfo(Stream)"/>.
    /// </summary>
    /// <param name="attributes">The root size attributes.</param>
    /// <param name="expectedWidth">The expected reported width.</param>
    /// <param name="expectedHeight">The expected reported height.</param>
    [Theory]
    [InlineData("width='calc(1 + 1)' height='min(1, 2)'", 2, 1)]
    [InlineData("width='max(10, 20)' height='clamp(1, 30, 40)'", 20, 30)]
    [InlineData("width='calc(2 * 10)' height='calc(0 + 5)'", 20, 5)]
    public void SvgCodec_GetInfo_RootSizeNumberMath_ResolvesToUserUnits(string attributes, int expectedWidth, int expectedHeight)
    {
        // Arrange: a rect that exactly fills the converted size, so Load can prove it used the same size
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' {attributes}><rect width='{expectedWidth}' height='{expectedHeight}' fill='black'/></svg>";
        using var infoStream = SvgLengthTestSupport.ToStream(svg);

        // Act
        var info = SvgCodec.GetInfo(infoStream);
        using var loadStream = SvgLengthTestSupport.ToStream(svg);
        var surface = SvgCodec.Load(loadStream, info.Width, info.Height);

        // Assert: GetInfo reports the user units and Load fills the surface with the rect
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
        Assert.Equal(255, surface[0, 0].A);
        Assert.Equal(255, surface[expectedWidth - 1, expectedHeight - 1].A);
    }

    /// <summary>Proves a mixed-type math result in a tolerant (class T) site is treated as absent: the root size falls back to 300x150.</summary>
    [Fact]
    public void SvgCodec_GetInfo_RootSizeMixedTypeMath_FallsBackTo300x150()
    {
        // Arrange
        using var stream = SvgLengthTestSupport.ToStream("<svg xmlns='http://www.w3.org/2000/svg' width='calc(1 + 1px)' height='calc(10% + 1)'/>");

        // Act
        var info = SvgCodec.GetInfo(stream);

        // Assert
        Assert.Equal(300, info.Width);
        Assert.Equal(150, info.Height);
    }

    /// <summary>Proves a number-typed math result in text <c>dx</c>/<c>dy</c> is user units, like the literal offsets.</summary>
    [Fact]
    public void SvgCodec_Load_TextDxDyNumberMath_IsUserUnits()
    {
        // Arrange
        const string Template =
            "<svg viewBox='0 0 200 200'><text x='20' y='100' font-family='TestFont' font-size='20' {0}>A</text></svg>";

        // Act
        var numberMath = SvgLengthTestSupport.Render(string.Format(Template, "dx='calc(1 + 1)' dy='min(1, 2)'"), 200, 200);
        var literal = SvgLengthTestSupport.Render(string.Format(Template, "dx='2' dy='1'"), 200, 200);
        var absent = SvgLengthTestSupport.Render(string.Format(Template, string.Empty), 200, 200);

        // Assert
        AssertSurfacesEqual(literal, numberMath);
        Assert.False(SvgLengthTestSupport.AreIdentical(absent, numberMath));
    }

    /// <summary>
    ///     Proves <c>line-height</c> is the one length property where a number-typed math result is
    ///     valid, and then it is a <em>multiplier</em> re-multiplied by each descendant's font size,
    ///     exactly like a literal number.
    /// </summary>
    /// <param name="declaration">The attribute declaring the line height on the wrapping group (font size 10).</param>
    [Theory]
    [InlineData("style='line-height: calc(1 + 1)'")]
    [InlineData("style='line-height: min(2, 3)'")]
    [InlineData("style='line-height: max(1, 2)'")]
    [InlineData("style='font: 10px/calc(1 + 1) serif'")]
    public void SvgCodec_Load_LineHeightNumberMath_IsMultiplier(string declaration)
    {
        // Arrange: the group is 10px; the child group changes the font size to 50 and measures 1lh
        var svg =
            $"<svg viewBox='0 0 400 40'><g {declaration} font-size='10'><g font-size='50'><rect width='1lh' height='10' fill='black'/></g></g></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(svg, 400, 40), 5);

        // Assert: 2 * 50 for a multiplier (it would be a fixed 2 if the math result were treated as a length)
        Assert.Equal(100.0, measured, Tolerance);
    }

    /// <summary>Proves a length-typed math <c>line-height</c> stays an absolute length, and a type mix is invalid and ignored.</summary>
    /// <param name="declaration">The <c>line-height</c> declaration.</param>
    /// <param name="expected">The width of a <c>1lh</c> bar under a child font size of 50.</param>
    [Theory]
    [InlineData("line-height: calc(1px + 1px)", 2.0)]
    [InlineData("line-height: calc(20% + 1px)", 3.0)]
    [InlineData("line-height: calc(10% + 1)", 60.0)]
    public void SvgCodec_Load_LineHeightLengthMathAndTypeMix_FollowsType(string declaration, double expected)
    {
        // Arrange: the invalid mix is ignored, so the child falls back to a normal line height (1.2 * 50)
        var svg =
            $"<svg viewBox='0 0 400 40'><g style='{declaration}' font-size='10'><g font-size='50'><rect width='1lh' height='10' fill='black'/></g></g></svg>";

        // Act
        var measured = SvgLengthTestSupport.RowCoverage(SvgLengthTestSupport.Render(svg, 400, 40), 5);

        // Assert
        Assert.Equal(expected, measured, Tolerance);
    }

    // ================================================================================================
    // objectBoundingBox mask regions evaluate math on the fraction axis
    // ================================================================================================

    /// <summary>Renders a 200x100 document whose mask region (default <c>objectBoundingBox</c> units) carries <paramref name="maskAttributes"/>.</summary>
    /// <param name="maskAttributes">Attributes for the <c>mask</c> element.</param>
    /// <returns>The rendered surface.</returns>
    private static Surface RenderBoundingBoxMask(string maskAttributes)
    {
        var svg =
            $"<svg viewBox='0 0 200 100'><mask id='m' {maskAttributes}><rect width='200' height='100' fill='white'/></mask>" +
            "<rect x='50' y='25' width='100' height='50' fill='black' mask='url(#m)'/></svg>";
        return SvgLengthTestSupport.Render(svg, 200, 100);
    }

    /// <summary>
    ///     Proves an <c>objectBoundingBox</c> mask region honors math functions with a basis of 1 -
    ///     a percentage is a fraction, a number-typed result is a fraction - on a non-square document
    ///     (so neither viewport axis can be mistaken for the fraction), rendering identically to the
    ///     plain value rather than silently falling back to the default region.
    /// </summary>
    /// <param name="attribute">The region attribute.</param>
    /// <param name="math">The math expression.</param>
    /// <param name="plain">The equivalent plain value.</param>
    [Theory]
    [InlineData("x", "calc(10% + 5%)", "15%")]
    [InlineData("y", "calc(10% + 5%)", "15%")]
    [InlineData("width", "calc(100% / 4)", "25%")]
    [InlineData("height", "min(10%, 20%)", "10%")]
    [InlineData("width", "calc(50% + 0px)", "50%")]
    [InlineData("x", "calc(0.1 + 0.2)", "0.3")]
    [InlineData("height", "calc(0.1 + 0.2)", "0.3")]
    public void SvgCodec_Load_MaskRegionBoundingBoxMath_UsesFractionBasis(string attribute, string math, string plain)
    {
        // Arrange / Act
        var viaMath = RenderBoundingBoxMask($"{attribute}='{math}'");
        var viaPlain = RenderBoundingBoxMask($"{attribute}='{plain}'");
        var absent = RenderBoundingBoxMask(string.Empty);

        // Assert: identical to the plain value, and the attribute genuinely changes the region
        AssertSurfacesEqual(viaPlain, viaMath);
        Assert.False(SvgLengthTestSupport.AreIdentical(absent, viaPlain));
    }

    /// <summary>
    ///     Proves a region value that mixes a percentage and a number is invalid (CSS Values 4,
    ///     "Type Checking": <c>opacity: calc(.25 + 25%)</c> is invalid) and falls back to the default region.
    /// </summary>
    /// <param name="attribute">The region attribute.</param>
    [Theory]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("width")]
    [InlineData("height")]
    public void SvgCodec_Load_MaskRegionBoundingBoxMixedPercentAndNumber_FallsBackToDefault(string attribute)
    {
        // Arrange / Act
        var mixed = RenderBoundingBoxMask($"{attribute}='calc(10% + 0.1)'");
        var absent = RenderBoundingBoxMask(string.Empty);

        // Assert
        AssertSurfacesEqual(absent, mixed);
    }

    // ================================================================================================
    // Gradient, pattern and mask coordinates: plain and compound percentages agree
    // ================================================================================================

    /// <summary>Gets the 200x100 viewport's percentage basis for a paint-server attribute under <c>userSpaceOnUse</c>.</summary>
    /// <param name="attribute">The attribute name.</param>
    /// <returns>The width, height or diagonal basis.</returns>
    private static float ViewportBasis(string attribute) => attribute switch
    {
        "x" or "x1" or "x2" or "cx" or "fx" or "width" => 200f,
        "y" or "y1" or "y2" or "cy" or "fy" or "height" => 100f,
        _ => MathF.Sqrt((200f * 200f) + (100f * 100f)) / MathF.Sqrt(2f)
    };

    /// <summary>Formats a float so it parses back to exactly the same value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The round-trip text.</returns>
    private static string Exact(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Builds an attribute string from a base set with <paramref name="attribute"/> set (or overridden) to <paramref name="value"/>.</summary>
    /// <param name="baseValues">The base attribute values, in order.</param>
    /// <param name="attribute">The tested attribute.</param>
    /// <param name="value">The tested value.</param>
    /// <returns>The attribute text.</returns>
    private static string WithAttribute(IReadOnlyList<(string Name, string Value)> baseValues, string attribute, string? value)
    {
        var merged = baseValues.Where(pair => pair.Name != attribute).ToList();
        if (value != null)
        {
            merged.Add((attribute, value));
        }

        return string.Join(' ', merged.Select(pair => $"{pair.Name}='{pair.Value}'"));
    }

    /// <summary>Renders a 200x100 document with a gradient painted over its full-size rectangle.</summary>
    /// <param name="element">The gradient element name.</param>
    /// <param name="attributes">The gradient attributes.</param>
    /// <param name="userSpace">Whether the gradient uses <c>userSpaceOnUse</c> units.</param>
    /// <returns>The rendered surface.</returns>
    private static Surface RenderGradient(string element, string attributes, bool userSpace)
    {
        var units = userSpace ? " gradientUnits='userSpaceOnUse'" : string.Empty;
        var svg =
            $"<svg viewBox='0 0 200 100'><defs><{element} id='g'{units} {attributes}>" +
            "<stop offset='0' stop-color='black'/><stop offset='1' stop-color='white'/>" +
            $"</{element}></defs><rect width='200' height='100' fill='url(#g)'/></svg>";
        return SvgLengthTestSupport.Render(svg, 200, 100);
    }

    /// <summary>
    ///     Proves a plain percentage and the same percentage inside <c>calc()</c> resolve against the
    ///     same basis for every gradient coordinate, in both units modes: a fraction of 1 under
    ///     <c>objectBoundingBox</c>, and the viewport axis of the attribute (width, height, diagonal)
    ///     under <c>userSpaceOnUse</c> (SVG 2, Paint Servers). Previously a plain <c>%</c> was always a
    ///     fraction of 1 while <c>calc()</c> always used the viewport width.
    /// </summary>
    /// <param name="element">The gradient element name.</param>
    /// <param name="attribute">The coordinate attribute.</param>
    /// <param name="userSpace">Whether the gradient uses <c>userSpaceOnUse</c> units.</param>
    [Theory]
    [InlineData("linearGradient", "x1", false)]
    [InlineData("linearGradient", "y1", false)]
    [InlineData("linearGradient", "x2", false)]
    [InlineData("linearGradient", "y2", false)]
    [InlineData("radialGradient", "cx", false)]
    [InlineData("radialGradient", "cy", false)]
    [InlineData("radialGradient", "r", false)]
    [InlineData("radialGradient", "fx", false)]
    [InlineData("radialGradient", "fy", false)]
    [InlineData("radialGradient", "fr", false)]
    [InlineData("linearGradient", "x1", true)]
    [InlineData("linearGradient", "y1", true)]
    [InlineData("linearGradient", "x2", true)]
    [InlineData("linearGradient", "y2", true)]
    [InlineData("radialGradient", "cx", true)]
    [InlineData("radialGradient", "cy", true)]
    [InlineData("radialGradient", "r", true)]
    [InlineData("radialGradient", "fx", true)]
    [InlineData("radialGradient", "fy", true)]
    [InlineData("radialGradient", "fr", true)]
    public void SvgCodec_Load_PaintServerCoordinateMath_AgreesWithPlainPercentage(string element, string attribute, bool userSpace)
    {
        // Arrange: a base geometry that makes every tested attribute visible, and the expected number
        // (a quarter is exact in binary, so the plain, compound and numeric forms are bit-identical)
        var baseValues = (element, userSpace) switch
        {
            ("linearGradient", true) => new[] { ("x1", "0"), ("y1", "0"), ("x2", "200"), ("y2", "0") },
            ("radialGradient", true) => new[] { ("cx", "100"), ("cy", "50"), ("r", "75") },
            _ => Array.Empty<(string, string)>()
        };
        var numeric = userSpace ? Exact(0.25f * ViewportBasis(attribute)) : "0.25";

        // Act
        var plain = RenderGradient(element, WithAttribute(baseValues, attribute, "25%"), userSpace);
        var compound = RenderGradient(element, WithAttribute(baseValues, attribute, "calc(25% + 0px)"), userSpace);
        var literal = RenderGradient(element, WithAttribute(baseValues, attribute, numeric), userSpace);
        var untouched = RenderGradient(element, WithAttribute(baseValues, attribute, null), userSpace);

        // Assert
        AssertSurfacesEqual(literal, plain);
        AssertSurfacesEqual(literal, compound);
        Assert.False(SvgLengthTestSupport.AreIdentical(untouched, plain));
    }

    /// <summary>Proves a number-typed math result is valid in an <c>objectBoundingBox</c> coordinate (the grammar is number-or-percentage).</summary>
    [Fact]
    public void SvgCodec_Load_GradientBoundingBoxNumberMath_IsAccepted()
    {
        // Arrange / Act
        var math = RenderGradient("linearGradient", "x2='calc(0.5 + 0.25)'", userSpace: false);
        var plain = RenderGradient("linearGradient", "x2='0.75'", userSpace: false);

        // Assert
        AssertSurfacesEqual(plain, math);
    }

    /// <summary>Proves a number-typed math result is user units in a <c>userSpaceOnUse</c> gradient coordinate, like the literal (SVG 2 Types 4.2).</summary>
    [Fact]
    public void SvgCodec_Load_GradientUserSpaceNumberMath_IsUserUnits()
    {
        // Arrange / Act
        var numberMath = RenderGradient("linearGradient", "x2='calc(1 + 1)'", userSpace: true);
        var absent = RenderGradient("linearGradient", string.Empty, userSpace: true);
        var literal = RenderGradient("linearGradient", "x2='2'", userSpace: true);

        // Assert: the literal 2 applies (not the default the unparseable value would have given)
        AssertSurfacesEqual(literal, numberMath);
        Assert.False(SvgLengthTestSupport.AreIdentical(absent, numberMath));
    }

    /// <summary>Proves a mixed-type math result in a <c>userSpaceOnUse</c> gradient coordinate is unparseable and falls back to the default.</summary>
    [Fact]
    public void SvgCodec_Load_GradientUserSpaceMixedTypeMath_FallsBackToDefault()
    {
        // Arrange / Act
        var mixed = RenderGradient("linearGradient", "x2='calc(1 + 1px)'", userSpace: true);
        var absent = RenderGradient("linearGradient", string.Empty, userSpace: true);

        // Assert
        AssertSurfacesEqual(absent, mixed);
    }

    /// <summary>Proves a number-typed math result is user units in a <c>userSpaceOnUse</c> pattern tile, like the literal.</summary>
    [Fact]
    public void SvgCodec_Load_PatternUserSpaceNumberMath_IsUserUnits()
    {
        // Arrange / Act
        var numberMath = RenderPattern("x='0' y='0' width='calc(16 * 2)' height='min(16, 20)'", userSpace: true);
        var literal = RenderPattern("x='0' y='0' width='32' height='16'", userSpace: true);
        var other = RenderPattern("x='0' y='0' width='40' height='16'", userSpace: true);

        // Assert
        AssertSurfacesEqual(literal, numberMath);
        Assert.False(SvgLengthTestSupport.AreIdentical(other, numberMath));
    }

    /// <summary>Proves a number-typed math result is user units in a <c>userSpaceOnUse</c> mask region, like the literal.</summary>
    [Fact]
    public void SvgCodec_Load_MaskRegionUserSpaceNumberMath_IsUserUnits()
    {
        // Arrange / Act: a region 20..80 wide over a shape spanning 20..120 (the default region shows all of it)
        var numberMath = RenderUserSpaceMask("x='calc(10 + 10)' y='0' width='calc(30 + 30)' height='min(100, 200)'");
        var literal = RenderUserSpaceMask("x='20' y='0' width='60' height='100'");
        var absent = RenderUserSpaceMask(string.Empty);

        // Assert
        AssertSurfacesEqual(literal, numberMath);
        Assert.False(SvgLengthTestSupport.AreIdentical(absent, numberMath));
    }

    /// <summary>Renders a 200x100 document with a pattern painted over a shape that is smaller than the viewport.</summary>
    /// <param name="attributes">The pattern attributes.</param>
    /// <param name="userSpace">Whether the pattern uses <c>userSpaceOnUse</c> units.</param>
    /// <returns>The rendered surface.</returns>
    private static Surface RenderPattern(string attributes, bool userSpace)
    {
        var units = userSpace ? " patternUnits='userSpaceOnUse'" : string.Empty;
        var svg =
            $"<svg viewBox='0 0 200 100'><defs><pattern id='p'{units} {attributes}><polygon points='0,0 40,0 200,100 160,100' fill='black'/></pattern></defs>" +
            "<rect x='20' y='10' width='160' height='80' fill='url(#p)'/></svg>";
        return SvgLengthTestSupport.Render(svg, 200, 100);
    }

    /// <summary>Proves a plain and a compound percentage agree for every pattern tile attribute in both units modes.</summary>
    /// <param name="attribute">The tile attribute.</param>
    /// <param name="userSpace">Whether the pattern uses <c>userSpaceOnUse</c> units.</param>
    [Theory]
    [InlineData("x", false)]
    [InlineData("y", false)]
    [InlineData("width", false)]
    [InlineData("height", false)]
    [InlineData("x", true)]
    [InlineData("y", true)]
    [InlineData("width", true)]
    [InlineData("height", true)]
    public void SvgCodec_Load_PatternTileMath_AgreesWithPlainPercentage(string attribute, bool userSpace)
    {
        // Arrange: a base tile that is not a divisor of the tested offsets/sizes
        var baseValues = userSpace
            ? new[] { ("x", "0"), ("y", "0"), ("width", "32"), ("height", "16") }
            : new[] { ("x", "0"), ("y", "0"), ("width", "0.2"), ("height", "0.2") };
        var numeric = userSpace ? Exact(0.25f * ViewportBasis(attribute)) : "0.25";

        // Act
        var plain = RenderPattern(WithAttribute(baseValues, attribute, "25%"), userSpace);
        var compound = RenderPattern(WithAttribute(baseValues, attribute, "calc(25% + 0px)"), userSpace);
        var literal = RenderPattern(WithAttribute(baseValues, attribute, numeric), userSpace);
        var untouched = RenderPattern(WithAttribute(baseValues, attribute, null), userSpace);

        // Assert
        AssertSurfacesEqual(literal, plain);
        AssertSurfacesEqual(literal, compound);
        Assert.False(SvgLengthTestSupport.AreIdentical(untouched, plain));
    }

    /// <summary>Renders a 200x100 document whose <c>userSpaceOnUse</c> mask region carries <paramref name="maskAttributes"/> over a shape smaller than the viewport.</summary>
    /// <param name="maskAttributes">Attributes for the <c>mask</c> element.</param>
    /// <returns>The rendered surface.</returns>
    private static Surface RenderUserSpaceMask(string maskAttributes)
    {
        var svg =
            $"<svg viewBox='0 0 200 100'><mask id='m' maskUnits='userSpaceOnUse' {maskAttributes}><rect width='200' height='100' fill='white'/></mask>" +
            "<rect x='20' y='10' width='100' height='50' fill='black' mask='url(#m)'/></svg>";
        return SvgLengthTestSupport.Render(svg, 200, 100);
    }

    /// <summary>
    ///     Proves a <c>userSpaceOnUse</c> mask region resolves a plain and a compound percentage against
    ///     the viewport on the attribute's axis (the shape is smaller than the viewport, so a
    ///     bounds-relative reading would differ).
    /// </summary>
    /// <param name="attribute">The region attribute.</param>
    [Theory]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("width")]
    [InlineData("height")]
    public void SvgCodec_Load_MaskRegionUserSpacePercentage_ResolvesAgainstViewport(string attribute)
    {
        // Arrange: the base region covers the whole viewport; the tested value is half of the viewport axis
        var baseValues = new[] { ("x", "0"), ("y", "0"), ("width", "200"), ("height", "100") };
        var numeric = Exact(0.5f * ViewportBasis(attribute));

        // Act
        var plain = RenderUserSpaceMask(WithAttribute(baseValues, attribute, "50%"));
        var compound = RenderUserSpaceMask(WithAttribute(baseValues, attribute, "calc(50% + 0px)"));
        var literal = RenderUserSpaceMask(WithAttribute(baseValues, attribute, numeric));
        var untouched = RenderUserSpaceMask(WithAttribute(baseValues, attribute, null));

        // Assert
        AssertSurfacesEqual(literal, plain);
        AssertSurfacesEqual(literal, compound);
        Assert.False(SvgLengthTestSupport.AreIdentical(untouched, plain));
    }

    // ================================================================================================
    // Root width/height (GetInfo and Load)
    // ================================================================================================

    /// <summary>Proves root <c>width</c>/<c>height</c> are converted to user units, not stripped.</summary>
    /// <param name="attributes">The root size attributes.</param>
    /// <param name="expectedWidth">The expected reported width.</param>
    /// <param name="expectedHeight">The expected reported height.</param>
    [Theory]
    [InlineData("width='1in' height='1in'", 96, 96)]
    [InlineData("width='210mm' height='297mm'", 794, 1123)]
    [InlineData("width='10rem' height='5rem'", 160, 80)]
    [InlineData("width='10em' height='5em' font-size='20'", 200, 100)]
    [InlineData("width='10em' height='5em' style='font-size: 20px'", 200, 100)]
    [InlineData("width='100pt' height='50pt'", 133, 67)]
    [InlineData("width='2.54cm' height='1.27cm'", 96, 48)]
    [InlineData("width='120PX' height='60Px'", 120, 60)]
    [InlineData("width='calc(1in + 4px)' height='calc(2 * 10px)'", 100, 20)]
    [InlineData("width='100' height='50'", 100, 50)]
    [InlineData("width='50%' height='50%'", 300, 150)]
    [InlineData("width='10vw' height='10vh'", 300, 150)]
    [InlineData("width='10xyz' height='10xyz'", 300, 150)]
    [InlineData("width='12 px' height='12 px'", 300, 150)]
    [InlineData("width='1in' height='1in' viewBox='0 0 40 30'", 40, 30)]
    [InlineData("width='1in'", 300, 150)]
    public void SvgCodec_GetInfo_RootSize_ConvertsUnitsOrFallsBack(string attributes, int expectedWidth, int expectedHeight)
    {
        // Arrange: units convert; a percentage/viewport unit has no basis and counts as absent (the
        // 300x150 default), as does an unrecognized unit; a viewBox still wins over any units
        using var stream = SvgLengthTestSupport.ToStream($"<svg xmlns='http://www.w3.org/2000/svg' {attributes}/>");

        // Act
        var info = SvgCodec.GetInfo(stream);

        // Assert
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
    }

    /// <summary>Proves rendering a root sized in units produces the converted size's content (Load agrees with GetInfo).</summary>
    [Fact]
    public void SvgCodec_Load_RootSizeInInches_FitsContentToConvertedSize()
    {
        // Arrange: a 1in x 1in document with no viewBox, containing a rect covering its whole converted area
        const string Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='1in' height='1in'><rect width='96' height='96' fill='black'/></svg>";
        using var infoStream = SvgLengthTestSupport.ToStream(Svg);
        var info = SvgCodec.GetInfo(infoStream);

        // Act: render at the reported size
        using var stream = SvgLengthTestSupport.ToStream(Svg);
        var surface = SvgCodec.Load(stream, info.Width, info.Height);

        // Assert: the rect exactly fills the surface (it would fill only 1/96 of it if the unit were stripped)
        Assert.Equal(96, info.Width);
        Assert.Equal(255, surface[0, 0].A);
        Assert.Equal(255, surface[95, 95].A);
    }

    // ================================================================================================
    // Documents that previously used units on non-geometry paths keep working
    // ================================================================================================

    /// <summary>Proves a document built only from unit-less numbers still renders (guard for the shared parser's legacy forms).</summary>
    [Theory]
    [InlineData("+10")]
    [InlineData("10.")]
    [InlineData(".1e2")]
    [InlineData("1E1")]
    [InlineData("  10  ")]
    public void SvgCodec_Load_LegacyNumberForms_StillParse(string width) =>
        Assert.Equal(10.0, BarWidth(width), Tolerance);
}
