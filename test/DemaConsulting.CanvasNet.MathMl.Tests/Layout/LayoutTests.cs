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
        Assert.Equal([0, enclosure.Width, enclosure.Width + box.Children[1].Box.Width], box.Children.Select(child => child.X));
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
    ///     Validates the bounds of a layout through the subsystem: a work budget smaller than the equation, a nesting depth
    ///     beyond the limit, and a canceled token stop it, a table with more grid slots than the limit fails, and the same
    ///     equation and table lay out within the default limits.
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
        var shallow = Engine(new MathMlLimits { MaxNestingDepth = 2 }, TestContext.Current.CancellationToken);
        var nesting = Assert.Throws<MathMlRenderException>(() => shallow.Layout(root, RootStyle(shallow)));
        var defaults = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        var tableBox = defaults.Layout(table, defaults.Styles.ResolveRoot(table, 16, new Rgba32(0, 0, 0, 255), null));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, work.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", work.Message, StringComparison.Ordinal);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, slots.Code);
        Assert.Contains("MathMlLimits.MaxTableCells", slots.Message, StringComparison.Ordinal);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, nesting.Code);
        Assert.Contains("MathMlLimits.MaxNestingDepth", nesting.Message, StringComparison.Ordinal);
        Assert.NotNull(cancellation);
        Assert.True(box.Width > 0);
        Assert.True(tableBox.Width > 0);
    }

    /// <summary>
    ///     Validates the operator forms through the subsystem: an explicit <c>form</c> wins over the position of the
    ///     operator in its row and selects the dictionary entry of that form, and an operator that is a script is postfix.
    /// </summary>
    [Fact]
    public void Layout_ExplicitFormAndScriptOperator_FollowTheDictionary()
    {
        // Arrange: a plus between two letters would be infix; the explicit prefix form selects its prefix entry
        var prefix = OperatorDictionary.Lookup("+", OperatorForm.Prefix, formIsExplicit: true);

        // Act
        var box = LayoutText(MathMlTestHelpers.Math("<mi>a</mi><mo form=\"prefix\">+</mo><mi>b</mi><msup><mi>x</mi><mo>+</mo></msup>"));

        // Assert: the explicit form and its spacing
        var plus = box.Children[1];
        Assert.Equal(OperatorForm.Prefix, plus.Box.Operator!.Form);
        Assert.Equal(box.Children[0].Box.Width + (prefix.LeadingSpace * 100), plus.X, 9);
        Assert.Equal(plus.X + plus.Box.Width + (prefix.TrailingSpace * 100), box.Children[2].X, 9);

        // The operator of the superscript is postfix
        var script = box.Children[3].Box.Children[1].Box;
        Assert.Equal(OperatorForm.Postfix, script.Operator!.Form);
    }

    /// <summary>
    ///     Validates the other stretching through the subsystem: an over brace stretched along the inline axis covers the
    ///     row under it, a summation in display style is drawn with a larger size variant of the font than inline,
    ///     and a stretchy operator that nothing can stretch is reported as <c>mathml-stretch-unavailable</c>.
    /// </summary>
    [Fact]
    public void Layout_InlineStretchLargeOperatorAndReport_FollowTheFont()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);

        // Act
        var box = LayoutText(
            MathMlTestHelpers.Math(
                "<mover><mrow><mi>a</mi><mi>b</mi><mi>c</mi><mi>d</mi><mi>e</mi></mrow><mo>&#x23DE;</mo></mover>"
                + "<mo>&#x2211;</mo><mstyle displaystyle=\"true\"><mo>&#x2211;</mo></mstyle>"
                + "<mo stretchy=\"true\">*</mo><mspace width=\"1em\" height=\"3em\" depth=\"1em\"/>"),
            100,
            sink);
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert: the brace covers its base
        var mover = box.Children[0].Box;
        var (@base, brace) = (mover.Children[0], mover.Children[1]);
        var glyphs = brace.Box.Items.OfType<GlyphItem>().ToList();
        Assert.True(glyphs.Count > 1);
        var inkLeft = brace.X + glyphs.Min(glyph => glyph.Ink.X);
        var inkRight = brace.X + glyphs.Max(glyph => glyph.Ink.X + glyph.Ink.Width);
        Assert.True(inkRight - inkLeft >= @base.Box.Width - 1);

        // The summation is large in display style only
        var inline = box.Children[1].Box;
        var display = Assert.Single(box.Children[2].Box.Children).Box;
        Assert.NotEqual(inline.Items.OfType<GlyphItem>().Single().Glyph, display.Items.OfType<GlyphItem>().Single().Glyph);
        Assert.True(display.Ascent + display.Descent > inline.Ascent + inline.Descent);

        // The star cannot stretch
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.StretchUnavailable, warning.Code);
        Assert.Contains("'*'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the computed style through the subsystem: <c>mathcolor</c> and the inline <c>style</c> color the
    ///     glyphs, <c>mathvariant="bold"</c> draws the bold letter of the Mathematical Alphanumeric Symbols, and an invalid
    ///     <c>mathvariant</c> is reported at its attribute and ignored.
    /// </summary>
    [Fact]
    public void Layout_ColorVariantAndInlineStyle_StyleTheGlyphs()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);
        var (boldFont, boldGlyph) = MathFont.Default.Resolve(0x1D41A);
        var (italicFont, italicGlyph) = MathFont.Default.Resolve(0x1D44E);

        // Act
        var box = LayoutText(
            "<math>\n<mi mathcolor=\"red\">x</mi><mi style=\"color: lime\">y</mi><mi mathvariant=\"bold\">a</mi>\n<mi mathvariant=\"heavy\">a</mi></math>",
            100,
            sink);
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert: the colors
        GlyphItem Glyph(int index) => box.Children[index].Box.Items.OfType<GlyphItem>().Single();
        Assert.Equal(new Rgba32(255, 0, 0, 255), Glyph(0).Color);
        Assert.Equal(new Rgba32(0, 255, 0, 255), Glyph(1).Color);

        // The bold letter, and the automatic italic where the variant is invalid
        Assert.Equal((boldFont, boldGlyph), (Glyph(2).Font, Glyph(2).Glyph));
        Assert.Equal((italicFont, italicGlyph), (Glyph(3).Font, Glyph(3).Glyph));
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, warning.Code);
        Assert.Equal(3, warning.Location.Line);
    }

    /// <summary>
    ///     Validates through the subsystem that a character no bundled font has is drawn as a missing-glyph box and
    ///     reported as <c>mathml-text-missing-glyph</c> at its token.
    /// </summary>
    [Fact]
    public void Layout_MissingGlyph_IsReported()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);

        // Act
        var box = LayoutText("<math>\n<mi>&#xE000;</mi></math>", 100, sink);
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Single(Assert.Single(box.Children).Box.Items.OfType<GlyphItem>());
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.TextMissingGlyph, warning.Code);
        Assert.Equal(2, warning.Location.Line);
    }

    /// <summary>
    ///     Validates the MathML 3 table attributes through the subsystem, as Firefox draws them: <c>columnalign</c> aligns
    ///     the contents left and right in their columns, <c>rowspacing</c> puts 2em between the rows instead of the inner
    ///     padding, <c>columnlines</c> draws a one-pixel rule between the columns, <c>frame</c> a rule around the table,
    ///     and an invalid <c>rowlines</c> is reported at its attribute and ignored.
    /// </summary>
    [Fact]
    public void Layout_TableExtensions_AlignSpaceAndRule()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);
        const string Narrow = "<mtd><mspace width=\"1em\" height=\"1em\"/></mtd>";
        const string Wide = "<mtd><mspace width=\"2em\" height=\"1em\"/></mtd>";
        static bool Near(double a, double b) => System.Math.Abs(a - b) < 1e-9;

        // Act
        var box = LayoutText(
            "<math>\n<mtable columnalign=\"left right\" rowspacing=\"2em\" columnlines=\"solid\" frame=\"solid\"\nrowlines=\"wavy\">"
            + $"<mtr>{Narrow}{Narrow}</mtr><mtr>{Wide}{Wide}</mtr></mtable></math>",
            100,
            sink);
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert: the contents (in document order: narrow, narrow, wide, wide)
        var table = Assert.Single(box.Children).Box;
        var spaces = Flatten(table).Where(entry => entry.Box.Element?.Name == "mspace").ToList();
        Assert.Equal(4, spaces.Count);
        Assert.Equal(spaces[2].X, spaces[0].X, 9);
        Assert.Equal(spaces[3].X + spaces[3].Box.Width, spaces[1].X + spaces[1].Box.Width, 9);
        Assert.Equal(200, (spaces[2].Y - spaces[2].Box.Ascent) - (spaces[0].Y + spaces[0].Box.Descent), 9);

        // The column line between the columns and the frame around the table
        var rules = table.Items.OfType<RuleItem>().Select(rule => rule.Rect).ToList();
        Assert.Contains(rules, rect => Near(rect.Width, 1) && rect.X > spaces[2].X + spaces[2].Box.Width && rect.X + 1 < spaces[3].X);
        Assert.Contains(rules, rect => Near(rect.X, 0) && Near(rect.Width, table.Width) && Near(rect.Height, 1));

        // The invalid row lines are reported and draw nothing between the rows
        var warning = Assert.Single(warnings);
        Assert.Equal((MathMlDiagnosticCodes.AttributeInvalid, 3), (warning.Code, warning.Location.Line));
        Assert.DoesNotContain(rules, rect => Near(rect.Height, 1) && rect.Width < table.Width - 1e-9);
    }

    /// <summary>
    ///     Validates through the subsystem that the ink of slanted letters does not collide: the italic f between braces
    ///     and parentheses (stretchy or not) and before the parenthesis of a function application, the digit superscript
    ///     of a zeta and of a b under a radical sign in display style and in the numerator of a fraction, with the
    ///     radical overbar at least its vertical gap (RadicalDisplayStyleVerticalGap or RadicalVerticalGap) above the
    ///     ink of its content, the superscript included.
    /// </summary>
    [Fact]
    public void Layout_SlantedLettersScriptsAndRadicals_InkDoesNotCollide()
    {
        // Arrange
        var font = MathFont.Default;
        static List<(string Name, MathRect Ink)> Glyphs(MathBox root) =>
            [.. Flatten(root).SelectMany(entry => entry.Box.Items.OfType<GlyphItem>().Select(glyph => (entry.Box.Element?.Name ?? "-", glyph.Ink.Offset(entry.X, entry.Y))))];
        static bool Overlap(MathRect a, MathRect b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

        // Act
        var fenced = LayoutText(MathMlTestHelpers.Math(
            "<mrow><mo>{</mo><mi>f</mi><mo>}</mo></mrow><mrow><mo stretchy=\"false\">{</mo><mi>f</mi><mo stretchy=\"false\">}</mo></mrow>"
            + "<mrow><mo>(</mo><mi>f</mi><mo>)</mo></mrow><mrow><mo stretchy=\"false\">(</mo><mi>f</mi><mo stretchy=\"false\">)</mo></mrow>"
            + "<mi>f</mi><mo>&#x2061;</mo><mrow><mo stretchy=\"false\">(</mo><mi>x</mi><mo stretchy=\"false\">)</mo></mrow>"));
        var damped = LayoutText(MathMlTestHelpers.Math("<msqrt><mn>1</mn><mo>&#x2212;</mo><msup><mi>&#x3B6;</mi><mn>2</mn></msup></msqrt>", "display=\"block\""));
        var quadratic = LayoutText(MathMlTestHelpers.Math(
            "<mfrac><mrow><msqrt><msup><mi>b</mi><mn>2</mn></msup><mo>&#x2212;</mo><mn>4</mn><mi>a</mi><mi>c</mi></msqrt></mrow><mn>2</mn></mfrac>",
            "display=\"block\""));

        // Assert: no two glyphs of the fenced letters overlap
        var glyphs = Glyphs(fenced);
        Assert.Equal(16, glyphs.Count);
        for (var i = 1; i < glyphs.Count; i++)
        {
            Assert.False(Overlap(glyphs[i - 1].Ink, glyphs[i].Ink), $"glyphs {i - 1} and {i} overlap");
        }

        // The superscripts are clear of their bases, and the overbar of each radical clears its superscript
        foreach (var (root, gap) in new[]
        {
            (damped, font.Constant(MathConstant.RadicalDisplayStyleVerticalGap, 100)),
            (quadratic, font.Constant(MathConstant.RadicalVerticalGap, 100)),
        })
        {
            var msup = Flatten(root).Single(entry => entry.Box.Element?.Name == "msup");
            var (@base, sup) = (Glyphs(msup.Box)[0].Ink, Glyphs(msup.Box)[1].Ink);
            Assert.False(Overlap(@base, sup));
            var sqrt = Flatten(root).Single(entry => entry.Box.Element?.Name == "msqrt");
            var bar = sqrt.Box.Items.OfType<RuleItem>().Single().Rect.Offset(sqrt.X, sqrt.Y);
            var contentTop = Glyphs(sqrt.Box).Skip(1).Min(glyph => glyph.Ink.Y) + sqrt.Y;
            Assert.True(contentTop - bar.Bottom >= gap - 1e-9, $"the overbar is {contentTop - bar.Bottom} above the content");
            Assert.Equal(sup.Y + msup.Y - sqrt.Y, Glyphs(sqrt.Box).Skip(1).Min(glyph => glyph.Ink.Y), 9);
        }
    }

    /// <summary>
    ///     Validates through the subsystem that the subscript of a single character does not depend on its descender: the n
    ///     of γ, y, and ψ sits at the same depth as the n of x (SubscriptShiftDown), as TeX sets it, while the n of a row
    ///     ending in a descender is kept SubscriptBaselineDropMin below the descent of the row (MathML Core 3.4.1.2).
    /// </summary>
    [Fact]
    public void Layout_SubscriptOfCharacter_IgnoresItsDescender()
    {
        // Arrange
        var font = MathFont.Default;

        // Act
        var box = LayoutText(MathMlTestHelpers.Math(
            "<msub><mi>x</mi><mi>n</mi></msub><msub><mi>&#x3B3;</mi><mi>n</mi></msub><msub><mi>y</mi><mi>n</mi></msub>"
            + "<msub><mi>&#x3C8;</mi><mi>n</mi></msub><msub><mrow><mi>a</mi><mi>y</mi></mrow><mi>n</mi></msub>"));

        // Assert
        var shifts = box.Children.Select(child => child.Box.Children[1].Y).ToList();
        Assert.Equal(font.Constant(MathConstant.SubscriptShiftDown, 100), shifts[0], 9);
        Assert.All(shifts.Take(4), shift => Assert.Equal(shifts[0], shift, 9));
        var row = box.Children[4].Box.Children[0].Box;
        Assert.Equal(font.Constant(MathConstant.SubscriptBaselineDropMin, 100) + row.Descent, shifts[4], 9);
        Assert.True(shifts[4] > shifts[0]);
    }

    /// <summary>
    ///     Validates through the subsystem that everything the painter draws, antialiasing included, lies inside the
    ///     content box united with the ink bounds of the equation rounded out to whole pixels: painted into a larger
    ///     surface at several font sizes, equations whose ink reaches past their content box (an integral with limits, a
    ///     final italic letter, nested stretched parentheses around a sum and fractions, the tail of a radical around
    ///     tall content) leave every pixel outside it transparent.
    /// </summary>
    [Fact]
    public void Layout_PaintedInk_StaysInsideTheExtent()
    {
        // Arrange
        string[] equations =
        [
            "<mi>f</mi><mo>=</mo><msubsup><mo>&#x222B;</mo><mn>1</mn><mi>&#x221E;</mi></msubsup><mi>x</mi>",
            "<mrow><mo>(</mo><mfrac><mn>1</mn><msup><mi>n</mi><mi>s</mi></msup></mfrac><mo>)</mo></mrow>",
            "<munderover><mo>&#x2211;</mo><mrow><mi>n</mi><mo>=</mo><mn>1</mn></mrow><mi>&#x221E;</mi></munderover><mi>j</mi>",
            "<mrow><mo>(</mo><mrow><mo>(</mo><munderover><mo>&#x2211;</mo><mi>k</mi><mi>m</mi></munderover><mfrac><mi>x</mi>"
                + "<mi>k</mi></mfrac><mo>)</mo></mrow><mo>&#x2212;</mo><mfrac><mn>1</mn><mi>n</mi></mfrac><mo>)</mo></mrow>",
            "<msqrt><mfrac><mfrac><mn>1</mn><mi>y</mi></mfrac><mfrac><mi>g</mi><mn>2</mn></mfrac></mfrac></msqrt>",
        ];

        foreach (var equation in equations)
        {
            foreach (var size in new[] { 16.0, 20, 23, 32, 57, 96 })
            {
                // Act
                var box = LayoutText(MathMlTestHelpers.Math(equation, "display=\"block\""), size);
                var extent = box.InkBounds() is { } ink ? box.ContentRect.Union(ink) : box.ContentRect;
                var left = System.Math.Floor(extent.X + 1e-6);
                var top = System.Math.Floor(extent.Y + 1e-6);
                var width = (int)(System.Math.Ceiling(extent.Right - 1e-6) - left);
                var height = (int)(System.Math.Ceiling(extent.Bottom - 1e-6) - top);
                const int Margin = 8;
                using var surface = new Surface(width + (2 * Margin), height + (2 * Margin));
                MathMlPainter.Paint(surface, box, new MathPlacement(1, Margin - left, Margin - top), null, 10_000, TestContext.Current.CancellationToken);

                // Assert: nothing is painted outside the extent, and the ink reaches into it
                var outside = 0;
                for (var y = 0; y < surface.Height; y++)
                {
                    for (var x = 0; x < surface.Width; x++)
                    {
                        var inside = x >= Margin && x < Margin + width && y >= Margin && y < Margin + height;
                        if (!inside && surface[x, y].A > 0)
                        {
                            outside++;
                        }
                    }
                }

                Assert.True(outside == 0, $"{equation} at {size} px paints {outside} pixels outside its extent");
                Assert.True(MathMlTestHelpers.InkedPixels(surface) > 0);
            }
        }
    }
}
