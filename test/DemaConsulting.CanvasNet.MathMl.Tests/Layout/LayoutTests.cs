using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Subsystem tests for the <c>Layout</c> subsystem: rows of tokens, stretchy fences, fractions, radicals, scripts,
///     limits and tables laid out through the engine with the real math font, checked against the metrics and constants
///     of the font and the operator spacing, with the reports and bounds of the layout.
/// </summary>
public class LayoutTests
{
    /// <summary>
    ///     Validates the box of <c>x + 1</c> at 100 px: the advances of the glyphs plus the dictionary spacing of the
    ///     operator (category B, 4/18 em on each side), and the ink ascent and descent rounded out.
    /// </summary>
    [Fact]
    public void Layout_TokenRow_MatchesFontMetricsAndSpacing()
    {
        // Arrange: the advances and ink of the glyphs drawn (the italic x, the plus, the one)
        var font = MathFont.Default;
        double Advance(int codePoint)
        {
            var (glyphFont, glyph) = font.Resolve(codePoint);
            return MathFont.Advance(glyphFont, glyph, 100);
        }

        double Top(int codePoint)
        {
            var (glyphFont, glyph) = font.Resolve(codePoint);
            return -font.InkBounds(glyphFont, glyph, 100).Y;
        }

        var space = 400 / 18.0;

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mi>x</mi><mo>+</mo><mn>1</mn>"));

        // Assert
        Assert.Equal(Advance(0x1D465) + space + Advance('+') + space + Advance('1'), box.Width, 9);
        Assert.Equal(System.Math.Ceiling(new[] { Top(0x1D465), Top('+'), Top('1') }.Max()), box.Ascent);
        Assert.Equal([0, Advance(0x1D465) + space, Advance(0x1D465) + space + Advance('+') + space], box.Children.Select(child => child.X));
    }

    /// <summary>
    ///     Validates stretchy fences around a tall row through the subsystem: each parenthesis covers the row symmetrically
    ///     about the math axis of the MATH table, in a box as wide as its widest construction, without spacing (categories F
    ///     and G of the operator dictionary).
    /// </summary>
    [Fact]
    public void Layout_StretchyFencesAroundTallRow_MatchConstants()
    {
        // Arrange
        var font = MathFont.Default;
        var axis = font.Constant(MathConstant.AxisHeight, 100);
        var half = System.Math.Max(200 - axis, 100 + axis);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mo>(</mo><mspace width=\"10px\" height=\"2em\" depth=\"1em\"/><mo>)</mo>"));

        // Assert
        var width = StretchyGlyphs.PreferredInlineSize(font, '(', 100);
        Assert.Equal([0, width, width + 10], box.Children.Select(child => child.X));
        foreach (var paren in new[] { box.Children[0].Box, box.Children[2].Box })
        {
            Assert.Equal(axis, (paren.Ascent - paren.Descent) / 2, 9);
            Assert.True(paren.Ascent >= half + axis - 1e-9);
            Assert.Equal(width, paren.Width, 9);
        }
    }

    /// <summary>
    ///     Validates that a script level scales the tokens with the MATH constants: at depth 1 the glyphs are 60% as wide.
    /// </summary>
    [Fact]
    public void Layout_ScriptLevel_ScalesTokensWithMathConstants()
    {
        // Act
        var plain = LayoutText(MathMlTestHelpers.Math("<mn>1234</mn>"));
        var script = LayoutText(MathMlTestHelpers.Math("<mstyle scriptlevel=\"1\"><mn>1234</mn></mstyle>"));

        // Assert
        Assert.Equal(plain.Width * 0.6, script.Width, 9);
    }

    /// <summary>
    ///     Validates a fraction and a square root through the subsystem with the real font: the numerator of
    ///     <c>x/2</c> (at the script size of 60%) sits FractionNumeratorShiftUp above the baseline unless its gap to the
    ///     bar is larger, the bar is centered on the math axis, and the square root of 2 rises by RadicalVerticalGap,
    ///     RadicalRuleThickness and RadicalExtraAscender above its base.
    /// </summary>
    [Fact]
    public void Layout_FractionsAndRadicals_MatchConstants()
    {
        // Arrange
        var font = MathFont.Default;
        var axis = font.Constant(MathConstant.AxisHeight, 100);
        var thickness = font.Constant(MathConstant.FractionRuleThickness, 100);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mfrac><mi>x</mi><mn>2</mn></mfrac><msqrt><mn>2</mn></msqrt>"));

        // Assert: the fraction
        var fraction = box.Children[0].Box;
        var numerator = fraction.Children[0];
        Assert.Equal(60, numerator.Box.Items.OfType<GlyphItem>().Single().Size, 9);
        var expectedShift = System.Math.Max(font.Constant(MathConstant.FractionNumeratorShiftUp, 100), axis + (thickness / 2) + font.Constant(MathConstant.FractionNumeratorGapMin, 100) + numerator.Box.Descent);
        Assert.Equal(-expectedShift, numerator.Y, 9);
        var bar = Assert.IsType<RuleItem>(Assert.Single(fraction.Items));
        Assert.Equal(-axis, bar.Rect.Y + (bar.Rect.Height / 2), 9);
        Assert.Equal(thickness, bar.Rect.Height, 9);

        // The square root
        var sqrt = box.Children[1].Box;
        var radicand = Assert.Single(sqrt.Children).Box;
        var rise = font.Constant(MathConstant.RadicalVerticalGap, 100) + font.Constant(MathConstant.RadicalRuleThickness, 100) + font.Constant(MathConstant.RadicalExtraAscender, 100);
        Assert.Equal(radicand.Ascent + rise, sqrt.Ascent, 9);
    }

    /// <summary>
    ///     Validates scripts and limits through the subsystem with the real font: the subscript of <c>x_i</c> sits
    ///     SubscriptShiftDown below the baseline unless its ascent or the base pushes it further, after SpaceAfterScript
    ///     the box ends; the upper limit of a summation in display style keeps UpperLimitGapMin above it with its baseline at
    ///     least UpperLimitBaselineRiseMin above the operator; and the hat of an accent keeps the font size of its base.
    /// </summary>
    [Fact]
    public void Layout_ScriptsAndLimits_MatchConstants()
    {
        // Arrange
        var font = MathFont.Default;
        double Constant(MathConstant constant) => font.Constant(constant, 100);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math(
            "<msub><mi>x</mi><mi>i</mi></msub><mstyle displaystyle=\"true\"><mover><mo>&#x2211;</mo><mi>n</mi></mover></mstyle>"
            + "<mover accent=\"true\"><mi>x</mi><mo>&#x302;</mo></mover>"));

        // Assert: the subscript
        var msub = box.Children[0].Box;
        var (@base, sub) = (msub.Children[0].Box, msub.Children[1]);
        var subShift = System.Math.Max(System.Math.Max(Constant(MathConstant.SubscriptShiftDown), sub.Box.Ascent - Constant(MathConstant.SubscriptTopMax)), Constant(MathConstant.SubscriptBaselineDropMin) + @base.Descent);
        Assert.Equal(subShift, sub.Y, 9);
        Assert.Equal(@base.Width + sub.Box.Width + Constant(MathConstant.SpaceAfterScript), msub.Width, 9);
        Assert.Equal(60, sub.Box.Items.OfType<GlyphItem>().Single().Size, 9);

        // The upper limit
        var mover = Assert.Single(box.Children[1].Box.Children).Box;
        var (sum, limit) = (mover.Children[0].Box, mover.Children[1]);
        var limitBottom = -limit.Y - limit.Box.Descent;
        var gap = System.Math.Max(Constant(MathConstant.UpperLimitGapMin), Constant(MathConstant.UpperLimitBaselineRiseMin) - limit.Box.Descent);
        Assert.Equal(sum.Ascent + gap, limitBottom, 9);

        // The accent
        var accent = box.Children[2].Box;
        Assert.Equal(100, accent.Children[1].Box.Items.OfType<GlyphItem>().Single().Size, 9);
    }

    /// <summary>
    ///     Validates through the subsystem that a fraction with three children is laid out as a row of them, side by side
    ///     on its baseline, with a located <c>mathml-child-count</c> warning.
    /// </summary>
    [Fact]
    public void Layout_WrongChildCount_IsRowWithWarning()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);

        // Act
        var box = LayoutText("<math>\n<mfrac><mi>a</mi><mi>b</mi><mi>c</mi></mfrac></math>", 100, sink);
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        var fraction = Assert.Single(box.Children).Box;
        Assert.Equal(3, fraction.Children.Count);
        Assert.All(fraction.Children, child => Assert.Equal(0, child.Y));
        Assert.Empty(fraction.Items);
        var warning = Assert.Single(warnings);
        Assert.Equal((MathMlDiagnosticCodes.ChildCount, new MathMlLocation(null, 2, 2)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Validates that an element MathML Core does not define is laid out as a row of its children, and that the element,
    ///     an attribute without effect and an invalid attribute value are reported while the layout continues.
    /// </summary>
    [Fact]
    public void Layout_UnsupportedElementsAndAttributes_AreRowsWithReports()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<menclose><mi>a</mi><mn>2</mn></menclose><mi dir=\"rtl\">b</mi><mn mathcolor=\"bogus\">3</mn>"), 100, sink);
        var warnings = new List<MathMlDiagnostic>();
        var unsupported = new List<MathMlUnsupportedFeature>();
        sink.Deliver(warnings.Add, unsupported.Add);

        // Assert: the enclosure is a row (its children side by side on its baseline), and the three tokens follow it
        var enclosure = Flatten(box).Single(entry => entry.Box.Element?.Name == "menclose").Box;
        Assert.Equal([0, enclosure.Children[0].Box.Width], enclosure.Children.Select(child => child.X));
        Assert.All(enclosure.Children, child => Assert.Equal(0, child.Y));
        Assert.Equal(3, box.Children.Count);
        Assert.Equal([MathMlDiagnosticCodes.ElementUnsupported, MathMlDiagnosticCodes.AttributeUnsupported], unsupported.Select(report => report.Feature));
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Validates a table through the subsystem: its cells are padded by 0.4em and 0.5ex (the x-height of the font) as
    ///     the user agent stylesheet of MathML Core asks, its rows hold their baselines, and the table is centered on the
    ///     math axis (AxisHeight of the MATH table) of the row it is in.
    /// </summary>
    [Fact]
    public void Layout_Tables_CenterOnAxisWithCorePadding()
    {
        // Arrange
        var font = MathFont.Default;
        var axis = font.Constant(MathConstant.AxisHeight, 100);
        var blockPad = font.XHeight(100) / 2;

        // Act
        var box = LayoutText(MathMlTestHelpers.Math(
            "<mtable><mtr><mtd><mspace width=\"1em\" height=\"0.5em\" depth=\"0.25em\"/></mtd><mtd><mspace width=\"2em\" height=\"1em\" depth=\"0\"/></mtd></mtr>" +
            "<mtr><mtd><mspace width=\"0.5em\" height=\"0.5em\" depth=\"0.5em\"/></mtd><mtd/></mtr></mtable>"));

        // Assert
        var table = Assert.Single(box.Children).Box;
        var height = (100 + 25 + (2 * blockPad)) + (50 + 50 + (2 * blockPad));
        Assert.Equal(height, table.Ascent + table.Descent, 9);
        Assert.Equal((height / 2) + axis, table.Ascent, 9);
        Assert.Equal(100 + 80 + 200 + 80, table.Width, 9);
        var first = table.Children[0].Box;
        Assert.Equal(blockPad + 100, first.Ascent, 9);
        Assert.Equal([40.0, 40.0], first.Children.Select(cell => System.Math.Round(Assert.Single(cell.Box.Children).X, 9)));
    }

    /// <summary>
    ///     Validates the bounds of a layout through the subsystem: a work budget smaller than the equation and a canceled
    ///     token stop it, a table with more grid slots than the limit fails, and the same equation lays out within the
    ///     default limits.
    /// </summary>
    [Fact]
    public void Layout_Bounds_WorkBudgetAndCancellation_StopTheLayout()
    {
        // Arrange
        var root = Tree(MathMlTestHelpers.Math("<mrow><mi>x</mi><mo>+</mo><mn>12</mn></mrow>"));
        using var source = new CancellationTokenSource();
        source.Cancel();
        LayoutEngine Engine(MathMlLimits limits, CancellationToken token) => new(MathFont.Default, limits, new MathMlReportSink(100, false), token);
        MathStyle RootStyle(LayoutEngine engine) => engine.Styles.ResolveRoot(root, 16, new Rgba32(0, 0, 0, 255), null);

        // Act
        var small = Engine(new MathMlLimits { MaxLayoutWork = 3 }, TestContext.Current.CancellationToken);
        var work = Assert.Throws<MathMlRenderException>(() => small.Layout(root, RootStyle(small)));
        var canceled = Engine(MathMlLimits.Default, source.Token);
        var cancellation = Assert.ThrowsAny<OperationCanceledException>(() => canceled.Layout(root, RootStyle(canceled)));
        var normal = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        var box = normal.Layout(root, RootStyle(normal));
        var table = Tree(MathMlTestHelpers.Math("<mtable><mtr><mtd columnspan=\"20\"/></mtr><mtr/></mtable>"));
        var cells = Engine(new MathMlLimits { MaxTableCells = 39 }, TestContext.Current.CancellationToken);
        var slots = Assert.Throws<MathMlRenderException>(() => cells.Layout(table, cells.Styles.ResolveRoot(table, 16, new Rgba32(0, 0, 0, 255), null)));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, work.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", work.Message, StringComparison.Ordinal);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, slots.Code);
        Assert.Contains("MathMlLimits.MaxTableCells", slots.Message, StringComparison.Ordinal);
        Assert.NotNull(cancellation);
        Assert.True(box.Width > 0);
    }
}
