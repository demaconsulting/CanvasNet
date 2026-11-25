using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="LayoutEngine"/>: the dispatch, the fallback of the elements this version does not draw,
///     hidden elements, the first-child rule, attribute reports, the forms and stretching of embellished operators, and
///     the depth, work and cancellation bounds.
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
        var (box, _, unsupported) = Run("<mfrac><mi>a</mi><mi>b</mi></mfrac><mfoo><mi>c</mi></mfoo>");
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
