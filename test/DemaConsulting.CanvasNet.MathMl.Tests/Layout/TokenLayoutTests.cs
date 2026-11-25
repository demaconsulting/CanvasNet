using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="TokenLayout"/> (with <see cref="MathVariants"/>): automatic italic, every
///     <c>mathvariant</c>, the box metrics, invisible operators, spaces, strings, the operator properties, large
///     operators, stretched operators and missing glyphs.
/// </summary>
public class TokenLayoutTests
{
    /// <summary>The font size of the tests.</summary>
    private const double Size = 100;

    /// <summary>The upright style of the tests (no mathvariant in effect).</summary>
    private static readonly MathStyle Style = new(Size, false, 0, new Rgba32(0, 0, 0, 255), null);

    /// <summary>
    ///     Gets the mapping of one character by every value other than <c>normal</c>.
    /// </summary>
    /// <returns>The value, the character and its styled code point.</returns>
    public static TheoryData<string, int, int> EveryVariant() => new()
    {
        { "normal", 'A', 'A' },
        { "bold", 'A', 0x1D400 },
        { "italic", 'A', 0x1D434 },
        { "bold-italic", 'A', 0x1D468 },
        { "double-struck", 'A', 0x1D538 },
        { "bold-fraktur", 'A', 0x1D56C },
        { "script", 'A', 0x1D49C },
        { "bold-script", 'A', 0x1D4D0 },
        { "fraktur", 'A', 0x1D504 },
        { "sans-serif", 'A', 0x1D5A0 },
        { "bold-sans-serif", 'A', 0x1D5D4 },
        { "sans-serif-italic", 'A', 0x1D608 },
        { "sans-serif-bold-italic", 'A', 0x1D63C },
        { "monospace", 'A', 0x1D670 },
        { "initial", 0x0628, 0x1EE21 },
        { "tailed", 0x062C, 0x1EE42 },
        { "looped", 0x0628, 0x1EE81 },
        { "stretched", 0x0628, 0x1EE61 },
        { "bold", 0x03B1, 0x1D6C2 },
        { "bold", '1', 0x1D7CF },
        { "double-struck", '1', 0x1D7D9 },
        { "bold-italic", '1', '1' },
    };

    /// <summary>
    ///     Validates that a single-character <c>mi</c> draws its mathematical italic.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_SingleCharMi_IsItalic()
    {
        // Act
        var box = Tokens().LayoutToken(Token("mi", "x"), Style);

        // Assert
        Assert.Equal([0x1D465], DrawnCodePoints(box, ['x', 0x1D465]));
    }

    /// <summary>
    ///     Validates that an <c>mi</c> of several characters is upright.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_MultiCharMi_IsUpright()
    {
        // Act
        var box = Tokens().LayoutToken(Token("mi", "sin"), Style);

        // Assert
        Assert.Equal(['s', 'i', 'n'], DrawnCodePoints(box, ['s', 'i', 'n', 0x1D460, 0x1D456, 0x1D45B]));
    }

    /// <summary>
    ///     Validates that <c>mathvariant="normal"</c> cancels the automatic italic.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_MathvariantNormal_CancelsItalic()
    {
        // Act
        var box = Tokens().LayoutToken(Token("mi", "x"), Style with { Variant = MathVariant.Normal });

        // Assert
        Assert.Equal(['x'], DrawnCodePoints(box, ['x', 0x1D465]));
    }

    /// <summary>
    ///     Validates that every <c>mathvariant</c> value maps Latin, Greek, digits and Arabic letters to their styled code
    ///     points, and leaves a character without a styled form unchanged.
    /// </summary>
    /// <param name="variant">The value.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="expected">The styled code point.</param>
    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void TokenLayout_LayoutToken_EveryMathvariant_MapsLatinGreekDigits(string variant, int codePoint, int expected)
    {
        // Arrange
        Assert.True(MathVariants.TryParse(variant.ToUpperInvariant(), out var parsed));

        // Act
        var mapped = TokenLayout.Transform(char.ConvertFromUtf32(codePoint), parsed, autoItalic: true);

        // Assert
        Assert.Equal([expected], mapped);
        Assert.Equal(18, MathVariants.Count);
    }

    /// <summary>
    ///     Validates that the holes of the alphanumeric block map to the letterlike symbols.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_LetterlikeHoles_MapToLetterlikeSymbols()
    {
        // Act / Assert
        Assert.Equal([0x2102], TokenLayout.Transform("C", MathVariant.DoubleStruck, false));
        Assert.Equal([0x212C], TokenLayout.Transform("B", MathVariant.Script, false));
        Assert.Equal([0x212D], TokenLayout.Transform("C", MathVariant.Fraktur, false));
        Assert.Equal([0x210E], TokenLayout.Transform("h", MathVariant.Italic, false));
        Assert.Equal([0x210E], TokenLayout.Transform("h", null, true));
        Assert.Equal([0x1D4C1], TokenLayout.Transform("l", MathVariant.Script, false));
    }

    /// <summary>
    ///     Validates the box of a token: the advance width, the ink ascent and descent rounded out to whole pixels, and the
    ///     italic correction of a single glyph (the italic f: bounds 20, -240, 575, 765 and advance 450 of 1000 units;
    ///     italic correction 145).
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_InkAscentDescent_FollowGlyphBounds()
    {
        // Act
        var box = Tokens().LayoutToken(Token("mi", "f"), Style);
        var accent = Tokens().LayoutToken(Token("mi", "a"), Style with { Variant = MathVariant.Normal });

        // Assert
        Assert.Equal(45, box.Width, 6);
        Assert.Equal((77.0, 24.0), (box.Ascent, box.Descent));
        Assert.Equal(14.5, box.ItalicCorrection, 6);
        Assert.Equal(28.1, accent.TopAccentAttachment!.Value, 6);
    }

    /// <summary>
    ///     Validates that a text with a space has the ink metrics of its glyphs, as in Chrome, and the advance of the
    ///     space.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_TextWithSpace_UsesInkMetrics()
    {
        // Act
        var spaced = Tokens().LayoutToken(Token("mtext", "a b"), Style);
        var joined = Tokens().LayoutToken(Token("mtext", "ab"), Style);

        // Assert
        Assert.Equal((joined.Ascent, joined.Descent), (spaced.Ascent, spaced.Descent));
        Assert.True(spaced.Width > joined.Width);
        Assert.Equal(2, spaced.Items.Count);
        Assert.True(spaced.IsSpaceLike);
    }

    /// <summary>
    ///     Validates that the invisible operators take no space, draw nothing and report nothing.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_InvisibleOperators_HaveZeroWidthAndNoReport()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);

        // Act
        var boxes = new[] { "\u2061", "\u2062", "\u2063", "\u2064" }.Select(text => Tokens(sink).LayoutToken(Token("mo", text), Style)).ToList();

        // Assert
        Assert.All(boxes, box => Assert.Equal((0.0, 0), (box.Width, box.Items.Count)));
        Assert.Equal(0, sink.Count);
    }

    /// <summary>
    ///     Validates <c>mspace</c>: its width, height and depth; a negative depth clamped; a negative width ignored with a
    ///     warning; a percentage ignored with a report.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_Mspace_UsesLengths()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var warnings = new List<MathMlDiagnostic>();
        var unsupported = new List<MathMlUnsupportedFeature>();

        // Act
        var space = Tokens(sink).LayoutToken(Token("mspace", string.Empty, ("width", "1em"), ("height", "0.5em"), ("depth", "-5px")), Style);
        var negative = Tokens(sink).LayoutToken(Token("mspace", string.Empty, ("width", "-1em"), ("height", "50%")), Style);
        sink.Deliver(warnings.Add, unsupported.Add);

        // Assert
        Assert.Equal((100.0, 50.0, 0.0), (space.Width, space.Ascent, space.Descent));
        Assert.True(space.IsSpaceLike);
        Assert.Equal((0.0, 0.0), (negative.Width, negative.Ascent));
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, Assert.Single(warnings).Code);
        Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Validates that <c>ms</c> draws its text as written, without quotes, as MathML Core and Chrome do.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_Ms_DrawsTextWithoutQuotes()
    {
        // Act
        var ms = Tokens().LayoutToken(Token("ms", "abc"), Style);
        var mtext = Tokens().LayoutToken(Token("mtext", "abc"), Style);

        // Assert
        Assert.Equal(mtext.Width, ms.Width);
        Assert.Equal(3, ms.Items.Count);
    }

    /// <summary>
    ///     Validates the dictionary spacing of an operator: the category of its text and form (B for an infix plus,
    ///     4/18 em on each side; D for a prefix plus, none), explicit lengths, and a percentage of the dictionary value.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_Mo_HasDictionarySpacing()
    {
        // Act
        var plain = Tokens().LayoutToken(Token("mo", "+"), Style);
        var prefix = Tokens().LayoutToken(Token("mo", "+"), Style, new OperatorContext(OperatorForm.Prefix, null));
        var explicitSpacing = Tokens().LayoutToken(Token("mo", "+", ("lspace", "0.1em"), ("rspace", "50%")), Style);

        // Assert
        Assert.Equal((400 / 18.0, 400 / 18.0, OperatorCategory.B), (plain.Operator!.LeadingSpace, plain.Operator.TrailingSpace, plain.Operator.Category));
        Assert.Equal((0.0, 0.0, OperatorForm.Prefix), (prefix.Operator!.LeadingSpace, prefix.Operator.TrailingSpace, prefix.Operator.Form));
        Assert.Equal(10, explicitSpacing.Operator!.LeadingSpace, 9);
        Assert.Equal(200 / 18.0, explicitSpacing.Operator.TrailingSpace, 9);
        Assert.False(plain.IsSpaceLike);
    }

    /// <summary>
    ///     Validates that the attributes of an <c>mo</c> override every property of the dictionary, and that an invalid
    ///     value is reported and ignored.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_OperatorAttributes_OverrideDictionary()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var warnings = new List<MathMlDiagnostic>();
        var paren = Token(
            "mo",
            "(",
            ("form", "INFIX"),
            ("fence", "false"),
            ("separator", "true"),
            ("stretchy", "false"),
            ("symmetric", "false"),
            ("largeop", "true"),
            ("movablelimits", "true"),
            ("minsize", "2em"),
            ("maxsize", "300%"));
        var invalid = Token("mo", "+", ("form", "sideways"), ("stretchy", "maybe"));

        // Act
        var overridden = Tokens(sink).LayoutToken(paren, Style, new OperatorContext(OperatorForm.Prefix, null)).Operator!;
        var defaults = Tokens(sink).LayoutToken(Token("mo", "("), Style, new OperatorContext(OperatorForm.Prefix, null)).Operator!;
        var ignored = Tokens(sink).LayoutToken(invalid, Style).Operator!;
        sink.Deliver(warnings.Add, null);

        // Assert: the explicit infix form has no entry for "(", so the category is Default (no fallback to prefix)
        Assert.Equal((OperatorForm.Infix, OperatorCategory.Default), (overridden.Form, overridden.Category));
        Assert.Equal((false, true, false, false, true, true), (overridden.Fence, overridden.Separator, overridden.Stretchy, overridden.Symmetric, overridden.LargeOp, overridden.MovableLimits));
        Assert.Equal(200, overridden.MinSize, 9);
        Assert.True(overridden.MaxSize > 3 * 50);
        Assert.Equal((true, false, true, true, OperatorCategory.F), (defaults.Fence, defaults.Separator, defaults.Stretchy, defaults.Symmetric, defaults.Category));
        Assert.Equal(double.PositiveInfinity, defaults.MaxSize);
        Assert.Equal((OperatorForm.Infix, false, OperatorCategory.B), (ignored.Form, ignored.Stretchy, ignored.Category));
        Assert.Equal(2, warnings.Count(warning => warning.Code == MathMlDiagnosticCodes.AttributeInvalid));
    }

    /// <summary>
    ///     Validates that a large operator in display style draws its first size variant at least
    ///     DisplayOperatorMinHeight tall (else its largest), centered on the math axis.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_LargeOpDisplayStyle_UsesDisplayOperatorMinHeight()
    {
        // Arrange
        var font = MathFont.Default;
        var expected = StretchyGlyphs.LargeVariant(font, 0x2211, font.Constant(MathConstant.DisplayOperatorMinHeight, Size), Size)!;

        // Act
        var inline = Tokens().LayoutToken(Token("mo", "\u2211"), Style);
        var display = Tokens().LayoutToken(Token("mo", "\u2211"), Style with { DisplayStyle = true });

        // Assert
        var glyph = Assert.IsType<GlyphItem>(Assert.Single(display.Items));
        Assert.Equal(expected.Glyphs[0].Glyph, glyph.Glyph);
        Assert.True(display.Ascent + display.Descent > inline.Ascent + inline.Descent);
        Assert.Equal(font.Constant(MathConstant.AxisHeight, Size), (display.Ascent - display.Descent) / 2, 9);
        Assert.True(display.Operator!.LargeOp);
    }

    /// <summary>
    ///     Validates that a large operator outside display style, and one with <c>largeop="false"</c> in display style,
    ///     keep their base glyph.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_LargeOpInline_KeepsBaseGlyph()
    {
        // Arrange
        var (_, baseGlyph) = MathFont.Default.Resolve(0x2211);

        // Act
        var inline = Tokens().LayoutToken(Token("mo", "\u2211"), Style);
        var notLarge = Tokens().LayoutToken(Token("mo", "\u2211", ("largeop", "false")), Style with { DisplayStyle = true });

        // Assert
        Assert.Equal(baseGlyph, Assert.IsType<GlyphItem>(Assert.Single(inline.Items)).Glyph);
        Assert.Equal(baseGlyph, Assert.IsType<GlyphItem>(Assert.Single(notLarge.Items)).Glyph);
        Assert.Equal((inline.Width, inline.Ascent, inline.Descent), (notLarge.Width, notLarge.Ascent, notLarge.Descent));
    }

    /// <summary>
    ///     Validates the stretching of an operator by a block stretch size constraint: the symmetric target covers the
    ///     constraint about the math axis, the glyph is centered on it, the box is as wide as the preferred inline size, and
    ///     a stretchy operator without a constraint keeps its glyph.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_StretchConstraint_CoversTargetCenteredOnAxis()
    {
        // Arrange
        var axis = MathFont.Default.Constant(MathConstant.AxisHeight, Size);
        var constraint = new BlockStretchConstraint(200, 100);

        // Act
        var plain = Tokens().LayoutToken(Token("mo", "("), Style, new OperatorContext(OperatorForm.Prefix, null));
        var stretched = Tokens().LayoutToken(Token("mo", "("), Style, new OperatorContext(OperatorForm.Prefix, constraint));
        var nonSymmetric = Tokens().LayoutToken(Token("mo", "(", ("symmetric", "false")), Style, new OperatorContext(OperatorForm.Prefix, constraint));

        // Assert: symmetric about the axis, at least max(200 - axis, 100 + axis) on each side of it
        Assert.Equal(axis, (stretched.Ascent - stretched.Descent) / 2, 9);
        Assert.True(stretched.Ascent >= 200 && stretched.Descent >= 100);
        Assert.True(stretched.Ascent + stretched.Descent >= (2 * (200 - axis)) - 1e-9);
        Assert.Equal(StretchyGlyphs.PreferredInlineSize(MathFont.Default, '(', Size), stretched.Width, 9);
        Assert.Equal(50, (nonSymmetric.Ascent - nonSymmetric.Descent) / 2, 9);
        Assert.True(nonSymmetric.Ascent + nonSymmetric.Descent >= 300);
        Assert.True(plain.Ascent + plain.Descent < 120);
    }

    /// <summary>
    ///     Validates that a stretchy operator that nothing can stretch keeps its glyph centered on the target and is
    ///     reported when the target is taller than the glyph.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_NoConstruction_KeepsGlyphAndReports()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var warnings = new List<MathMlDiagnostic>();

        // Act
        var box = Tokens(sink).LayoutToken(Token("mo", "x", ("stretchy", "true")), Style, new OperatorContext(OperatorForm.Postfix, new BlockStretchConstraint(200, 100)));
        var small = Tokens(sink).LayoutToken(Token("mo", "y", ("stretchy", "true")), Style, new OperatorContext(OperatorForm.Postfix, new BlockStretchConstraint(10, 0)));
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(50, (box.Ascent - box.Descent) / 2, 9);
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.StretchUnavailable, warning.Code);
        Assert.Contains("'x'", warning.Message, StringComparison.Ordinal);
        Assert.NotNull(small.Operator);
    }

    /// <summary>
    ///     Validates the stretching of an operator by an inline stretch size constraint: the arrow is assembled, the over
    ///     brace takes a size variant or an assembly, the macron (which the math font has no glyph for) is a rule, each
    ///     centered in a box as wide as the unstretched glyph, as Chrome lays it out.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_InlineConstraint_StretchesHorizontally()
    {
        // Arrange
        OperatorContext Context(double width) => new(OperatorForm.Postfix, null, width);
        var (_, braceGlyph) = MathFont.Default.Resolve(0x23DE);

        // Act
        var plainArrow = Tokens().LayoutToken(Token("mo", "\u2192"), Style);
        var arrow = Tokens().LayoutToken(Token("mo", "\u2192"), Style, Context(600));
        var variant = Tokens().LayoutToken(Token("mo", "\u23DE"), Style, Context(120));
        var assembly = Tokens().LayoutToken(Token("mo", "\u23DE"), Style, Context(1000));
        var macron = Tokens().LayoutToken(Token("mo", "\u00AF", ("stretchy", "true")), Style, Context(300));

        // Assert: the arrow
        Assert.Equal(plainArrow.Width, arrow.Width, 9);
        Assert.True(arrow.Items.OfType<GlyphItem>().Count() >= 2);
        var ink = arrow.InkBounds()!.Value;
        Assert.True(ink.Width >= 590);
        Assert.InRange(ink.X + (ink.Width / 2), (arrow.Width / 2) - 10, (arrow.Width / 2) + 10);
        Assert.Equal(StretchAxis.Inline, arrow.Operator!.Axis);

        // Assert: the brace, the macron
        var variantGlyph = Assert.IsType<GlyphItem>(Assert.Single(variant.Items));
        Assert.NotEqual(braceGlyph, variantGlyph.Glyph);
        Assert.True(assembly.Items.Count >= 3);
        Assert.True(assembly.InkBounds()!.Value.Width >= 1000 - 1e-6);
        var rule = Assert.IsType<RuleItem>(Assert.Single(macron.Items));
        Assert.Equal(300, rule.Rect.Width, 9);
        Assert.Equal(macron.Width / 2, rule.Rect.X + 150, 9);
    }

    /// <summary>
    ///     Validates that an operator that nothing can stretch horizontally (Noto Sans Math has no horizontal construction
    ///     for <c>^</c> or <c>~</c>) keeps its glyph, and is reported only when the author wrote <c>stretchy="true"</c> and
    ///     the glyph is narrower than the target.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_InlineNoConstruction_KeepsGlyph_ReportsOnlyExplicitStretchy()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var warnings = new List<MathMlDiagnostic>();
        OperatorContext Context(double width) => new(OperatorForm.Postfix, null, width);

        // Act
        var plain = Tokens(sink).LayoutToken(Token("mo", "^"), Style);
        var explicitWide = Tokens(sink).LayoutToken(Token("mo", "^", ("stretchy", "true")), Style, Context(300));
        var explicitNarrow = Tokens(sink).LayoutToken(Token("mo", "^", ("stretchy", "true")), Style, Context(10));
        var implicitWide = Tokens(sink).LayoutToken(Token("mo", "~"), Style, Context(300));
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal((plain.Width, plain.Ascent, plain.Descent), (explicitWide.Width, explicitWide.Ascent, explicitWide.Descent));
        Assert.Single(explicitNarrow.Items);
        Assert.Single(implicitWide.Items);
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.StretchUnavailable, warning.Code);
        Assert.Contains("'^'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an operator drawn with its normal glyph has no italic correction (the scripts take it only from a
    ///     size variant or an assembly, as Chrome), while the display variant of an integral has the italic correction of
    ///     its glyph (500 units).
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_NormalGlyph_HasNoItalicCorrection()
    {
        // Act
        var inline = Tokens().LayoutToken(Token("mo", "\u222B"), Style);
        var display = Tokens().LayoutToken(Token("mo", "\u222B"), Style with { DisplayStyle = true });

        // Assert
        Assert.Equal(0, inline.ItalicCorrection);
        Assert.Equal(50, display.ItalicCorrection, 9);
    }

    /// <summary>
    ///     Validates <c>minsize</c> and <c>maxsize</c>: the target grows to the minimum or shrinks to the maximum about the
    ///     math axis, a percentage is of the unstretched height, and an empty constraint gives the minimum size centered on
    ///     the axis.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutOperator_MinMaxSize_ClampTarget()
    {
        // Arrange
        var axis = MathFont.Default.Constant(MathConstant.AxisHeight, Size);
        OperatorContext Context(double ascent, double descent) => new(OperatorForm.Prefix, new BlockStretchConstraint(ascent, descent));

        // Act
        var plain = Tokens().LayoutToken(Token("mo", "("), Style, new OperatorContext(OperatorForm.Prefix, null));
        var grown = Tokens().LayoutToken(Token("mo", "(", ("minsize", "3em")), Style, Context(54, 0));
        var shrunk = Tokens().LayoutToken(Token("mo", "(", ("maxsize", "1.2em")), Style, Context(200, 100));
        var percent = Tokens().LayoutToken(Token("mo", "(", ("maxsize", "150%")), Style, Context(200, 100));
        var empty = Tokens().LayoutToken(Token("mo", "(", ("symmetric", "false")), Style, Context(0, 0));

        // Assert
        Assert.True(grown.Ascent + grown.Descent >= 300);
        Assert.Equal(axis, (grown.Ascent - grown.Descent) / 2, 9);
        Assert.InRange(shrunk.Ascent + shrunk.Descent, 120, 150);
        Assert.InRange(percent.Ascent + percent.Descent, 1.5 * (plain.Ascent + plain.Descent), 200);
        Assert.Equal(axis, (empty.Ascent - empty.Descent) / 2, 9);
        Assert.Equal(plain.Ascent + plain.Descent, empty.Ascent + empty.Descent, 9);
    }

    /// <summary>
    ///     Validates that a character no bundled font has is drawn as the missing-glyph box and reported at the text.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_MissingGlyph_IsReported()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var warnings = new List<MathMlDiagnostic>();

        // Act
        var box = Tokens(sink).LayoutToken(Token("mtext", "\uE000"), Style);
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(0, Assert.IsType<GlyphItem>(Assert.Single(box.Items)).Glyph);
        var warning = Assert.Single(warnings);
        Assert.Equal((MathMlDiagnosticCodes.TextMissingGlyph, new MathMlLocation(null, 1, 1)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Creates the token layout of a call.
    /// </summary>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <returns>The token layout.</returns>
    private static TokenLayout Tokens(MathMlReportSink? sink = null)
    {
        sink ??= new MathMlReportSink(10, false);
        return new TokenLayout(MathFont.Default, new StyleResolver(MathFont.Default, sink), sink);
    }
}
