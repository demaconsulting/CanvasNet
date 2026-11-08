// cspell:ignore rlh vmin vmax rem dasharray dashoffset objectboundingbox userspaceonuse Hhea
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
