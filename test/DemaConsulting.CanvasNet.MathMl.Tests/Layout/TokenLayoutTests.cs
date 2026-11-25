using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="TokenLayout"/> (with <see cref="MathVariants"/>): automatic italic, every
///     <c>mathvariant</c>, the box metrics, invisible operators, spaces, strings, operator spacing and missing glyphs.
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
    ///     Validates the interim spacing of an operator: the Default category (5/18 em on each side), explicit lengths,
    ///     and a percentage of the default.
    /// </summary>
    [Fact]
    public void TokenLayout_LayoutToken_Mo_HasInterimSpacing()
    {
        // Act
        var plain = Tokens().LayoutToken(Token("mo", "+"), Style);
        var explicitSpacing = Tokens().LayoutToken(Token("mo", "+", ("lspace", "0.1em"), ("rspace", "50%")), Style);

        // Assert
        Assert.Equal(new EmbellishedOperator(500 / 18.0, 500 / 18.0), plain.Operator);
        Assert.Equal(10, explicitSpacing.Operator!.LeadingSpace, 9);
        Assert.Equal(250 / 18.0, explicitSpacing.Operator.TrailingSpace, 9);
        Assert.False(plain.IsSpaceLike);
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
