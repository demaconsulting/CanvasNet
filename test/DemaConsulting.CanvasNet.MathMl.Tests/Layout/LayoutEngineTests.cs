using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="LayoutEngine"/>: the dispatch (fractions and radicals included), the fallback of the
///     elements this version does not draw, the child-count rule, hidden elements, the first-child rule, attribute
///     reports, the forms and stretching of embellished operators (embellished fractions included), and the depth, work
///     and cancellation bounds.
/// </summary>
public class LayoutEngineTests
{
    /// <summary>
    ///     Validates the dispatch: tokens paint glyphs, rows place their children, and the boxes keep their elements.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Dispatch_LaysOutTokensAndRows()
    {
        // Act
        var (box, _, _) = Run("<mrow><mi>x</mi><mn>2</mn></mrow>");

        // Assert
        var mrow = Assert.Single(box.Children).Box;
        Assert.Equal("mrow", mrow.Element!.Name);
        Assert.Equal(["mi", "mn"], mrow.Children.Select(child => child.Box.Element!.Name));
        Assert.All(mrow.Children, child => Assert.IsType<GlyphItem>(Assert.Single(child.Box.Items)));
    }

    /// <summary>
    ///     Validates that an element of a later version and an unknown element are rows of their children, reported.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_StageBElement_FallsBackToRowWithReport()
    {
        // Act
        var (box, _, unsupported) = Run("<mtable><mi>a</mi><mi>b</mi></mtable><menclose><mi>c</mi></menclose>");
        var (row, _, _) = Run("<mrow><mi>a</mi><mi>b</mi></mrow><mrow><mi>c</mi></mrow>");

        // Assert
        Assert.Equal(row.Width, box.Width);
        Assert.Equal([2, 1], box.Children.Select(child => child.Box.Children.Count));
        Assert.Equal(2, unsupported.Count);
        Assert.Contains("later version", unsupported[0].Hint, StringComparison.Ordinal);
        Assert.Contains("not part of MathML Core", unsupported[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that elements with <c>display: none</c> and annotations get no box, and that <c>semantics</c> and
    ///     <c>maction</c> lay out their first child only.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_HiddenElements_GetNoBox()
    {
        // Act
        var (box, _, _) = Run("<mi style=\"display: none\">h</mi><semantics><mi>a</mi><annotation>x</annotation></semantics><maction><mi>b</mi><mi>c</mi></maction>");

        // Assert
        Assert.Equal(["semantics", "maction"], box.Children.Select(child => child.Box.Element!.Name));
        Assert.All(box.Children, child => Assert.Single(child.Box.Children));
    }

    /// <summary>
    ///     Validates that <c>mphantom</c> is marked as a phantom and keeps its size.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Mphantom_IsPhantomWithSize()
    {
        // Act
        var (box, _, _) = Run("<mphantom><mi>x</mi></mphantom>");

        // Assert
        var phantom = Assert.Single(box.Children).Box;
        Assert.True(phantom.IsPhantom);
        Assert.True(phantom.Width > 0);
        Assert.Null(box.InkBounds());
    }

    /// <summary>
    ///     Validates the attribute reports: legacy and unknown attributes and right-to-left math are reported; global
    ///     attributes that do not change a picture are silent.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_UnsupportedAttributes_AreReported()
    {
        // Act
        var (_, _, unsupported) = Run("<mi fontweight=\"bold\" id=\"a\" class=\"b\" data-x=\"c\" onclick=\"d\">x</mi><mrow dir=\"rtl\"><ms lquote=\"'\">s</ms></mrow>");

        // Assert
        Assert.Equal(3, unsupported.Count);
        Assert.Contains("'fontweight'", unsupported[0].Message, StringComparison.Ordinal);
        Assert.Contains("right-to-left", unsupported[1].Message, StringComparison.Ordinal);
        Assert.Contains("'lquote'", unsupported[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that text directly inside a row is laid out as a space-like text box.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_TextInRow_IsLaidOutLikeMtext()
    {
        // Act
        var (box, _, _) = Run("<mrow>ab</mrow>");

        // Assert
        var text = Assert.Single(Assert.Single(box.Children).Box.Children).Box;
        Assert.True(text.IsSpaceLike);
        Assert.Equal(2, text.Items.Count);
    }

    /// <summary>
    ///     Validates the bounds: the nesting depth, the work budget and cancellation.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Bounds_AreEnforced()
    {
        // Arrange
        var root = Tree(MathMlTestHelpers.Math("<mrow><mrow><mi>abc</mi></mrow></mrow>"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act
        var depth = Assert.Throws<MathMlRenderException>(() => Engine(new MathMlLimits { MaxNestingDepth = 2 }, TestContext.Current.CancellationToken).Layout(root, RootStyle(root)));
        var work = Assert.Throws<MathMlRenderException>(() => Engine(new MathMlLimits { MaxLayoutWork = 5 }, TestContext.Current.CancellationToken).Layout(root, RootStyle(root)));
        var canceled = Assert.ThrowsAny<OperationCanceledException>(() => Engine(MathMlLimits.Default, source.Token).Layout(root, RootStyle(root)));
        var engine = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        engine.Layout(root, RootStyle(root));

        // Assert
        Assert.Contains("MathMlLimits.MaxNestingDepth", depth.Message, StringComparison.Ordinal);
        Assert.Contains("MathMlLimits.MaxLayoutWork", work.Message, StringComparison.Ordinal);
        Assert.NotNull(canceled);
        Assert.Equal(7, engine.Work);
    }

    /// <summary>
    ///     Validates the form inference of the engine: the first and last of several children that are not space-like are
    ///     prefix and postfix, the others infix; space-like children do not count; an embellished row passes its own form
    ///     to its operator; and an explicit form wins.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OperatorForms_FollowPositionInRow()
    {
        // Act
        var (box, _, _) = Run("<mo>-</mo><mtext> </mtext><mi>x</mi><mo>+</mo><mi>y</mi><mrow><mo>!</mo><mspace width=\"1em\"/></mrow><mo form=\"prefix\">+</mo><mi>z</mi>");
        var (lone, _, _) = Run("<mo>+</mo>");

        // Assert
        var forms = Flatten(box).Where(entry => entry.Box.Element?.Name == "mo").Select(entry => entry.Box.Operator!.Form);
        Assert.Equal([OperatorForm.Prefix, OperatorForm.Infix, OperatorForm.Infix, OperatorForm.Prefix], forms);
        var row = box.Children.Single(child => child.Box.Element?.Name == "mrow").Box;
        Assert.Equal(OperatorForm.Infix, row.Operator!.Form);
        Assert.Equal(OperatorForm.Infix, Assert.Single(lone.Children).Box.Operator!.Form);
    }

    /// <summary>
    ///     Validates that an embellished operator inside an <c>mrow</c> takes the form of the <c>mrow</c> in its row and is
    ///     stretched through it: the core operator grows to the tallest sibling of the <c>mrow</c>.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_EmbellishedInMrow_StretchesCoreOperator()
    {
        // Act
        var (box, _, _) = Run("<mrow><mo>[</mo></mrow><mspace width=\"1em\" height=\"2em\" depth=\"1em\"/><mstyle mathcolor=\"red\"><mo>]</mo></mstyle>");

        // Assert
        var open = box.Children[0].Box;
        var close = box.Children[2].Box;
        Assert.Equal((OperatorForm.Prefix, OperatorForm.Postfix), (open.Operator!.Form, close.Operator!.Form));
        Assert.True(open.Ascent >= 200 && open.Descent >= 100);
        Assert.Equal((open.Ascent, open.Descent), (close.Ascent, close.Descent));
        Assert.Equal(open.Ascent, Assert.Single(open.Children).Box.Ascent);
    }

    /// <summary>
    ///     Validates that an operator laid out again to stretch is charged to the work budget again.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_StretchRelayout_IsCharged()
    {
        // Arrange
        var stretchy = Tree(MathMlTestHelpers.Math("<mo>(</mo><mi>x</mi>"));
        var plain = Tree(MathMlTestHelpers.Math("<mo stretchy=\"false\">(</mo><mi>x</mi>"));
        var first = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        var second = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);

        // Act
        first.Layout(stretchy, RootStyle(stretchy));
        second.Layout(plain, RootStyle(plain));

        // Assert: the parenthesis (one element, one character) is laid out a second time
        Assert.Equal(second.Work + 2, first.Work);
    }

    /// <summary>
    ///     Validates the dispatch of fractions and radicals: none is reported, the numerator of a fraction is above its
    ///     denominator with a bar between them, a square root paints its symbol and overbar around the row of its
    ///     children, and a root places its index before its square root.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FractionsAndRadicals_AreDispatched()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mfrac><mi>a</mi><mi>b</mi></mfrac><msqrt><mi>x</mi><mn>2</mn></msqrt><mroot><mi>y</mi><mn>3</mn></mroot>");

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        var fraction = box.Children[0].Box;
        Assert.Equal("mfrac", fraction.Element!.Name);
        Assert.True(fraction.Children[0].Y < 0 && fraction.Children[1].Y > 0);
        Assert.IsType<RuleItem>(Assert.Single(fraction.Items));
        var sqrt = box.Children[1].Box;
        Assert.Equal(["mi", "mn"], Assert.Single(sqrt.Children).Box.Children.Select(child => child.Box.Element!.Name));
        Assert.Equal((1, 1), (sqrt.Items.OfType<GlyphItem>().Count(), sqrt.Items.OfType<RuleItem>().Count()));
        var root = box.Children[2].Box;
        Assert.Equal("mn", root.Children[0].Box.Element!.Name);
        Assert.Equal(50, root.Children[0].Box.Items.OfType<GlyphItem>().Single().Size, 9);
    }

    /// <summary>
    ///     Validates that a fraction or a root without exactly two in-flow children is a row of its children with a located
    ///     <see cref="MathMlDiagnosticCodes.ChildCount"/> warning (not an unsupported feature), its children still under the
    ///     user agent rules of their positions.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_WrongChildCount_IsRowWithChildCountWarning()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mfrac><mi>a</mi></mfrac><mroot><mi>a</mi><mi>b</mi><mi>c</mi></mroot><mfrac><mi>a</mi><mi style=\"display: none\">h</mi><mi>b</mi></mfrac>");

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MathMlDiagnosticCodes.ChildCount, warning.Code));
        Assert.Contains("<mfrac> has 1 in-flow children", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("<mroot> has 3", warnings[1].Message, StringComparison.Ordinal);
        Assert.All(box.Children.Take(2), child => Assert.All(child.Box.Children, grandChild => Assert.Equal(0, grandChild.Y)));
        Assert.Equal(60, box.Children[0].Box.Children[0].Box.Items.OfType<GlyphItem>().Single().Size, 9);
        Assert.IsType<RuleItem>(Assert.Single(box.Children[2].Box.Items));
    }

    /// <summary>
    ///     Validates an embellished fraction (its numerator is an operator): it carries the operator, gets the form of its
    ///     position from its row, and its row spaces it like the operator.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_EmbellishedFraction_CarriesItsOperator()
    {
        // Act
        var (box, _, _) = Run("<mi>a</mi><mfrac><mo>=</mo><mi>b</mi></mfrac><mi>c</mi>");

        // Assert
        var fraction = box.Children[1];
        Assert.Equal(("=", OperatorForm.Infix), (fraction.Box.Operator!.CoreText, fraction.Box.Operator.Form));
        Assert.Equal(box.Children[0].Box.Width + fraction.Box.Operator.LeadingSpace, fraction.X, 9);
        Assert.Equal(fraction.X + fraction.Box.Width + fraction.Box.Operator.TrailingSpace, box.Children[2].X, 9);
    }

    /// <summary>
    ///     Validates the forms of the operators inside <c>msqrt</c>: the first and last of several children are prefix and
    ///     postfix, and a lone operator is infix and keeps its spacing (the row of a square root is not an embellished
    ///     operator).
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_SqrtChildren_InferForms()
    {
        // Act
        var (box, _, _) = Run("<msqrt><mo>-</mo><mi>x</mi><mo>!</mo></msqrt><msqrt><mo>+</mo></msqrt>");

        // Assert
        var forms = Flatten(box.Children[0].Box).Where(entry => entry.Box.Element?.Name == "mo").Select(entry => entry.Box.Operator!.Form);
        Assert.Equal([OperatorForm.Prefix, OperatorForm.Postfix], forms);
        var lone = Assert.Single(box.Children[1].Box.Children).Box;
        var plus = Assert.Single(lone.Children);
        Assert.Equal(OperatorForm.Infix, plus.Box.Operator!.Form);
        Assert.Equal(plus.Box.Operator.LeadingSpace, plus.X, 9);
        Assert.Null(lone.Operator);
        Assert.Null(box.Children[1].Box.Operator);
    }

    /// <summary>
    ///     Lays out content of a <c>math</c> element and collects the reports.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>The box and the reports.</returns>
    private static (MathBox Box, List<MathMlDiagnostic> Warnings, List<MathMlUnsupportedFeature> Unsupported) Run(string content)
    {
        var sink = new MathMlReportSink(100, false);
        var box = LayoutText(MathMlTestHelpers.Math(content), 100, sink);
        var warnings = new List<MathMlDiagnostic>();
        var unsupported = new List<MathMlUnsupportedFeature>();
        sink.Deliver(warnings.Add, unsupported.Add);
        return (box, warnings, unsupported);
    }

    /// <summary>
    ///     Creates an engine.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The engine.</returns>
    private static LayoutEngine Engine(MathMlLimits limits, CancellationToken cancellationToken) =>
        new(MathFont.Default, limits, new MathMlReportSink(10, false), cancellationToken);

    /// <summary>
    ///     Computes the style of a root element at 100 px.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>The style.</returns>
    private static MathStyle RootStyle(DemaConsulting.CanvasNet.MathMl.Syntax.MathElement root) =>
        new StyleResolver(MathFont.Default, new MathMlReportSink(10, false)).ResolveRoot(root, 100, new Rgba32(0, 0, 0, 255), null);
}
