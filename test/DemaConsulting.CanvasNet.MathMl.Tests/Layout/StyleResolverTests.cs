using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="StyleResolver"/>: the display mode, <c>displaystyle</c>, <c>scriptlevel</c> and the
///     font-size scaling of MathML Core, <c>mathsize</c>, colors, backgrounds, <c>mathvariant</c>, the inline style, the
///     user agent rules of the children of fractions and roots, and <c>math-shift</c>.
/// </summary>
public class StyleResolverTests
{
    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The inherited style of the tests: 100 px, compact, depth 0.</summary>
    private static readonly MathStyle Parent = new(100, false, 0, Black, null);

    /// <summary>
    ///     Validates that the display mode decides the math style of the <c>math</c> element, that the option overrides
    ///     it, and that an invalid value is a warning.
    /// </summary>
    [Fact]
    public void StyleResolver_ResolveRoot_Display_SetsMathStyle()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var block = Node("math", [], ("display", "BLOCK"));
        var invalid = Node("math", [], ("display", "wide"));

        // Act
        var own = Styles(sink).ResolveRoot(block, 16, Black, null);
        var overridden = Styles(sink).ResolveRoot(block, 16, Black, MathMlDisplay.Inline);
        var fallback = Styles(sink).ResolveRoot(invalid, 16, Black, null);

        // Assert
        Assert.Equal((true, false, false), (own.DisplayStyle, overridden.DisplayStyle, fallback.DisplayStyle));
        Assert.Equal((16.0, 0), (own.FontSize, own.MathDepth));
        Assert.Equal(1, sink.Count);
    }

    /// <summary>
    ///     Validates the three forms of <c>scriptlevel</c> and <c>displaystyle</c>.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_ScriptLevelAndDisplayStyle_SetDepthAndStyle()
    {
        // Arrange
        var parent = Parent with { MathDepth = 2 };

        // Act
        var plus = Styles().Resolve(Node("mstyle", [], ("scriptlevel", "+1")), parent);
        var minus = Styles().Resolve(Node("mstyle", [], ("scriptlevel", "-2")), parent);
        var set = Styles().Resolve(Node("mstyle", [], ("scriptlevel", "0"), ("displaystyle", "true")), parent);

        // Assert
        Assert.Equal((3, 0, 0), (plus.MathDepth, minus.MathDepth, set.MathDepth));
        Assert.True(set.DisplayStyle);
    }

    /// <summary>
    ///     Validates the font-size scaling of MathML Core with the constants of the font (ScriptPercentScaleDown 60 and
    ///     ScriptScriptPercentScaleDown 50) and 0.71 beyond, in both directions.
    /// </summary>
    [Fact]
    public void StyleResolver_ScaleFactor_UsesScriptPercentScaleDownConstants()
    {
        // Arrange
        var styles = Styles();

        // Act / Assert
        Assert.Equal(0.6, styles.ScaleFactor(0, 1), 9);
        Assert.Equal(0.5, styles.ScaleFactor(0, 2), 9);
        Assert.Equal(0.5 / 0.6, styles.ScaleFactor(1, 2), 9);
        Assert.Equal(0.5 * 0.71, styles.ScaleFactor(0, 3), 9);
        Assert.Equal(0.71, styles.ScaleFactor(2, 3), 9);
        Assert.Equal(1 / 0.6, styles.ScaleFactor(1, 0), 9);
        Assert.Equal(1, styles.ScaleFactor(4, 4));
        Assert.Equal(50, styles.Resolve(Node("mstyle", [], ("scriptlevel", "2")), Parent).FontSize, 9);
    }

    /// <summary>
    ///     Validates <c>mathsize</c>: a percentage and an em of the inherited size; an invalid value is a warning; it wins
    ///     over the scaling of the script level.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_MathSize_SetsFontSize()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);

        // Act
        var percent = Styles(sink).Resolve(Node("mstyle", [], ("mathsize", "200%")), Parent);
        var em = Styles(sink).Resolve(Node("mstyle", [], ("mathsize", "0.5em"), ("scriptlevel", "1")), Parent);
        var invalid = Styles(sink).Resolve(Node("mstyle", [], ("mathsize", "-1em")), Parent);

        // Assert
        Assert.Equal((200.0, 50.0, 100.0), (percent.FontSize, em.FontSize, invalid.FontSize));
        Assert.Equal(1, sink.Count);
    }

    /// <summary>
    ///     Validates that the color is inherited and the background is not, and that invalid colors are warnings.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_Colors_InheritColorNotBackground()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var styles = Styles(sink);

        // Act
        var parent = styles.Resolve(Node("mstyle", [], ("mathcolor", "red"), ("mathbackground", "blue")), Parent);
        var child = styles.Resolve(Node("mi", []), parent);
        var invalid = styles.Resolve(Node("mi", [], ("mathcolor", "reddish")), Parent);

        // Assert
        Assert.Equal((new Rgba32(255, 0, 0, 255), (Rgba32?)new Rgba32(0, 0, 255, 255)), (parent.Color, parent.Background));
        Assert.Equal((new Rgba32(255, 0, 0, 255), (Rgba32?)null), (child.Color, child.Background));
        Assert.Equal(Black, invalid.Color);
        Assert.Equal(1, sink.Count);
    }

    /// <summary>
    ///     Validates that <c>mathvariant</c> is inherited, that <c>normal</c> cancels the automatic italic, and that an
    ///     invalid value is a warning.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_MathVariant_IsInherited()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var styles = Styles(sink);

        // Act
        var bold = styles.Resolve(Node("mstyle", [], ("mathvariant", "bold")), Parent);
        var child = styles.Resolve(Node("mi", []), bold);
        var normal = styles.Resolve(Node("mi", [], ("mathvariant", "normal")), Parent);
        var invalid = styles.Resolve(Node("mi", [], ("mathvariant", "heavy")), Parent);

        // Assert
        Assert.Equal((MathVariant?)MathVariant.Bold, child.Variant);
        Assert.Equal((MathVariant?)MathVariant.Normal, normal.Variant);
        Assert.Null(invalid.Variant);
        Assert.Equal(1, sink.Count);
    }

    /// <summary>
    ///     Validates the inline style: it overrides the attributes, sets the math depth and font style, hides with
    ///     <c>display: none</c>, and reports declarations outside the subset.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_InlineStyle_OverridesAttributes()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var unsupported = new List<MathMlUnsupportedFeature>();
        var element = Node("mi", [], ("mathcolor", "red"), ("style", "color: lime; math-depth: auto-add; font-style: normal; display: none; border: 1px"));

        // Act
        var style = Styles(sink).Resolve(element, Parent);
        sink.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal(new Rgba32(0, 255, 0, 255), style.Color);
        Assert.Equal((1, (MathVariant?)MathVariant.Normal, true), (style.MathDepth, style.Variant, style.DisplayNone));
        Assert.Equal(60, style.FontSize, 9);
        Assert.Equal(MathMlDiagnosticCodes.CssUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Validates the user agent style of <c>merror</c> (a light yellow background) and that an attribute overrides it.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_Merror_HasLightYellowBackground()
    {
        // Act
        var error = Styles().Resolve(Node("merror", []), Parent);
        var colored = Styles().Resolve(Node("merror", [], ("mathbackground", "white")), Parent);

        // Assert
        Assert.Equal((Rgba32?)new Rgba32(255, 255, 224, 255), error.Background);
        Assert.Equal((Rgba32?)new Rgba32(255, 255, 255, 255), colored.Background);
    }

    /// <summary>
    ///     Validates the clamping of the math depth, which keeps the font size finite.
    /// </summary>
    [Fact]
    public void StyleResolver_Resolve_ExtremeScriptLevel_IsClamped()
    {
        // Act
        var deep = Styles().Resolve(Node("mstyle", [], ("scriptlevel", "1000000")), Parent);
        var shallow = Styles().Resolve(Node("mstyle", [], ("scriptlevel", "-1000000")), Parent);

        // Assert
        Assert.Equal((StyleResolver.MaxMathDepth, -StyleResolver.MaxMathDepth), (deep.MathDepth, shallow.MathDepth));
        Assert.True(double.IsFinite(shallow.FontSize));
    }

    /// <summary>
    ///     Validates the user agent rules of the children of a fraction: <c>math-depth: auto-add</c> adds a level only
    ///     under a compact fraction, and both children get <c>math-style: compact</c> and the font size of their depth.
    /// </summary>
    [Fact]
    public void StyleResolver_ResolveChild_FractionChildren_AutoAddAndCompact()
    {
        // Arrange
        var display = Parent with { DisplayStyle = true };

        // Act
        var inline = Styles().ResolveChild(Node("mi", []), Parent, ChildStyleRule.FractionNumerator);
        var underDisplay = Styles().ResolveChild(Node("mi", []), display, ChildStyleRule.FractionNumerator);
        var denominator = Styles().ResolveChild(Node("mi", []), Parent, ChildStyleRule.FractionDenominator);

        // Assert
        Assert.Equal((1, false, 60.0), (inline.MathDepth, inline.DisplayStyle, System.Math.Round(inline.FontSize, 9)));
        Assert.Equal((0, false, 100.0), (underDisplay.MathDepth, underDisplay.DisplayStyle, underDisplay.FontSize));
        Assert.Equal((1, false), (denominator.MathDepth, denominator.DisplayStyle));
    }

    /// <summary>
    ///     Validates the user agent rule of the index of a root: <c>math-depth: add(2); math-style: compact</c>, whatever
    ///     the math style of the root.
    /// </summary>
    [Fact]
    public void StyleResolver_ResolveChild_RootIndex_AddsTwoLevels()
    {
        // Act
        var index = Styles().ResolveChild(Node("mn", []), Parent with { DisplayStyle = true }, ChildStyleRule.RootIndex);
        var none = Styles().ResolveChild(Node("mi", []), Parent with { DisplayStyle = true }, default);

        // Assert
        Assert.Equal((2, false, 50.0), (index.MathDepth, index.DisplayStyle, System.Math.Round(index.FontSize, 9)));
        Assert.Equal((0, true, 100.0), (none.MathDepth, none.DisplayStyle, none.FontSize));
    }

    /// <summary>
    ///     Validates <c>math-shift</c>: normal on the <c>math</c> element, compact on a denominator, on <c>msqrt</c> and
    ///     <c>mroot</c> and inherited by their descendants, and unchanged by the numerator rule.
    /// </summary>
    [Fact]
    public void StyleResolver_ResolveChild_MathShift_CompactAndInherited()
    {
        // Arrange
        var styles = Styles();
        var cramped = Parent with { MathShiftCompact = true };

        // Act
        var root = styles.ResolveRoot(Node("math", []), 16, Black, null);
        var denominator = styles.ResolveChild(Node("mi", []), Parent, ChildStyleRule.FractionDenominator);
        var numerator = styles.ResolveChild(Node("mi", []), Parent, ChildStyleRule.FractionNumerator);
        var radical = styles.Resolve(Node("msqrt", []), Parent);
        var root3 = styles.Resolve(Node("mroot", []), Parent);
        var inherited = styles.Resolve(Node("mrow", []), cramped);
        var underCramped = styles.ResolveChild(Node("mi", []), cramped, ChildStyleRule.FractionNumerator);

        // Assert
        Assert.False(root.MathShiftCompact);
        Assert.False(numerator.MathShiftCompact);
        Assert.True(denominator.MathShiftCompact && radical.MathShiftCompact && root3.MathShiftCompact);
        Assert.True(inherited.MathShiftCompact && underCramped.MathShiftCompact);
    }

    /// <summary>
    ///     Validates that the presentational hints of a child replace the user agent rule of its position:
    ///     <c>scriptlevel="+1"</c> is one level over the parent (not over the rule's), <c>displaystyle="true"</c> undoes
    ///     <c>math-style: compact</c>, and <c>mathsize</c> sets the font size.
    /// </summary>
    [Fact]
    public void StyleResolver_ResolveChild_Hints_ReplaceUaRules()
    {
        // Act
        var relative = Styles().ResolveChild(Node("mi", [], ("scriptlevel", "+1")), Parent, ChildStyleRule.RootIndex);
        var absolute = Styles().ResolveChild(Node("mi", [], ("scriptlevel", "0"), ("displaystyle", "true")), Parent, ChildStyleRule.FractionNumerator);
        var sized = Styles().ResolveChild(Node("mi", [], ("mathsize", "30px")), Parent, ChildStyleRule.FractionNumerator);

        // Assert
        Assert.Equal(1, relative.MathDepth);
        Assert.Equal((0, true, 100.0), (absolute.MathDepth, absolute.DisplayStyle, absolute.FontSize));
        Assert.Equal((1, 30.0), (sized.MathDepth, sized.FontSize));
    }

    /// <summary>
    ///     Creates the style resolver of a call.
    /// </summary>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <returns>The resolver.</returns>
    private static StyleResolver Styles(MathMlReportSink? sink = null) => new(MathFont.Default, sink ?? new MathMlReportSink(10, false));
}
