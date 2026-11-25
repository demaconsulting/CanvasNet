using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="LayoutEngine"/>: the dispatch (fractions, radicals, scripts and under- and overscripts
///     included), the fallback of the elements this version does not draw, the child-count rules, hidden elements, the
///     first-child rule, attribute reports, the forms and stretching of embellished operators (embellished fractions and
///     scripted elements, scripts as postfix operators, horizontal stretching), <c>movablelimits</c>, and the depth, work
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
    ///     Validates that elements MathML Core does not define are rows of their children, reported.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_NonCoreElement_FallsBackToRowWithReport()
    {
        // Act
        var (box, _, unsupported) = Run("<mfenced><mi>a</mi><mi>b</mi></mfenced><menclose><mi>c</mi></menclose>");
        var (row, _, _) = Run("<mrow><mi>a</mi><mi>b</mi></mrow><mrow><mi>c</mi></mrow>");

        // Assert
        Assert.Equal(row.Width, box.Width);
        Assert.Equal([2, 1], box.Children.Select(child => child.Box.Children.Count));
        Assert.Equal(2, unsupported.Count);
        Assert.All(unsupported, report => Assert.Contains("not part of MathML Core", report.Message, StringComparison.Ordinal));
        Assert.Contains("<mo>", unsupported[0].Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every element of MathML Core is drawn: none is reported as unsupported.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_EveryCoreElement_IsDrawn()
    {
        // Act
        var (box, warnings, unsupported) = Run(
            "<mrow><mi>a</mi><mn>1</mn><mo>+</mo><mtext>t</mtext><ms>s</ms><mspace width=\"1em\"/></mrow>" +
            "<mstyle><merror><mi>e</mi></merror><mphantom><mi>p</mi></mphantom><mpadded><mi>q</mi></mpadded></mstyle>" +
            "<mfrac><mi>a</mi><mi>b</mi></mfrac><msqrt><mi>c</mi></msqrt><mroot><mi>d</mi><mn>3</mn></mroot>" +
            "<msub><mi>x</mi><mn>1</mn></msub><msup><mi>x</mi><mn>2</mn></msup><msubsup><mi>x</mi><mn>1</mn><mn>2</mn></msubsup>" +
            "<munder><mi>x</mi><mo>_</mo></munder><mover><mi>x</mi><mo>^</mo></mover><munderover><mo>&#x2211;</mo><mi>i</mi><mi>n</mi></munderover>" +
            "<mmultiscripts><mi>R</mi><mi>i</mi><none/><mprescripts/><mi>j</mi><none/></mmultiscripts>" +
            "<mtable><mtr><mtd><mi>a</mi></mtd></mtr></mtable>" +
            "<semantics><mi>s</mi><annotation encoding=\"text/plain\">s</annotation><annotation-xml encoding=\"MathML-Content\"><ci>s</ci></annotation-xml></semantics>" +
            "<maction actiontype=\"toggle\"><mi>m</mi><mi>n</mi></maction>");

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal(15, box.Children.Count);
    }

    /// <summary>
    ///     Validates the dispatch of tables: an <c>mtable</c> holds its rows, which hold their cells, which hold the row of
    ///     their content; the boxes keep their elements.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Tables_AreDispatched()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mtable><mtr><mtd><mi>a</mi><mi>b</mi></mtd><mtd><mn>1</mn></mtd></mtr><mtr><mtd><mn>2</mn></mtd></mtr></mtable>");

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        var table = Assert.Single(box.Children).Box;
        Assert.Equal("mtable", table.Element!.Name);
        Assert.Equal(["mtr", "mtr"], table.Children.Select(child => child.Box.Element!.Name));
        var cells = table.Children[0].Box.Children;
        Assert.Equal(["mtd", "mtd"], cells.Select(child => child.Box.Element!.Name));
        var content = Assert.Single(cells[0].Box.Children).Box;
        Assert.Null(content.Element);
        Assert.Equal(["mi", "mi"], content.Children.Select(child => child.Box.Element!.Name));
    }

    /// <summary>
    ///     Validates the anonymous table objects: children of <c>mtable</c> that are not rows are wrapped in one
    ///     anonymous row, and children of <c>mtr</c> that are not cells in one anonymous cell, each run reported once.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_TableStructure_AnonymousRowsAndCellsWarn()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mtable><mi>a</mi><mi>b</mi><mtr><mtd><mi>c</mi></mtd><mi>d</mi><mi>e</mi></mtr><mtd><mi>f</mi></mtd></mtable>");

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal(3, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MathMlDiagnosticCodes.ChildCount, warning.Code));
        Assert.Contains("<mi> inside <mtable> is not an <mtr>", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("<mi> inside <mtr> is not an <mtd>", warnings[1].Message, StringComparison.Ordinal);
        Assert.Contains("<mtd> inside <mtable> is not an <mtr>", warnings[2].Message, StringComparison.Ordinal);
        var rows = Assert.Single(box.Children).Box.Children.Select(child => child.Box).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Null(rows[0].Element);
        Assert.Equal(2, Assert.Single(Assert.Single(rows[0].Children).Box.Children).Box.Children.Count);
        Assert.Equal("mtr", rows[1].Element!.Name);
        Assert.Equal(2, rows[1].Children.Count);
        Assert.Null(rows[1].Children[1].Box.Element);
        Assert.Null(rows[2].Element);
        Assert.Equal("mtd", Assert.Single(rows[2].Children).Box.Element!.Name);
    }

    /// <summary>
    ///     Validates that an <c>mtr</c> or <c>mtd</c> outside a table is a row of its children with a child-count warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_CellOutsideTable_IsRowWithWarning()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mtd><mi>a</mi><mi>b</mi></mtd><mtr><mi>c</mi></mtr>");
        var (row, _, _) = Run("<mrow><mi>a</mi><mi>b</mi></mrow><mrow><mi>c</mi></mrow>");

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal(row.Width, box.Width);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MathMlDiagnosticCodes.ChildCount, warning.Code));
        Assert.Contains("<mtd> is not inside an <mtr>", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("<mtr> is not inside an <mtable>", warnings[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a table charges every slot of its grid to the work budget.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Table_ChargesGridSlots()
    {
        // Arrange
        var small = Tree(MathMlTestHelpers.Math("<mtable><mtr><mtd/></mtr></mtable>"));
        var spanned = Tree(MathMlTestHelpers.Math("<mtable><mtr><mtd columnspan=\"50\" rowspan=\"0\"/></mtr><mtr/><mtr/></mtable>"));
        var engine = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);
        var other = Engine(MathMlLimits.Default, TestContext.Current.CancellationToken);

        // Act
        engine.Layout(small, RootStyle(small));
        other.Layout(spanned, RootStyle(spanned));

        // Assert: the 150 slots of the grid (one more than the single slot) and the 50 columns of the span are charged
        Assert.True(other.Work - engine.Work >= 149 + 49);
    }

    /// <summary>
    ///     Validates that <c>mtable</c> gets <c>math-style: compact</c>: a fraction in a cell of a display equation is in
    ///     text style.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Table_IsCompact()
    {
        // Act
        var (table, _, _) = Run("<mstyle displaystyle=\"true\"><mtable><mtr><mtd><mfrac><mi>a</mi><mi>b</mi></mfrac></mtd></mtr></mtable></mstyle>");
        var (inline, _, _) = Run("<mfrac><mi>a</mi><mi>b</mi></mfrac>");
        var (display, _, _) = Run("<mstyle displaystyle=\"true\"><mfrac><mi>a</mi><mi>b</mi></mfrac></mstyle>");

        // Assert
        var fraction = Flatten(table).Single(entry => entry.Box.Element?.Name == "mfrac").Box;
        var compact = Flatten(inline).Single(entry => entry.Box.Element?.Name == "mfrac").Box;
        var normal = Flatten(display).Single(entry => entry.Box.Element?.Name == "mfrac").Box;
        Assert.Equal(compact.Ascent + compact.Descent, fraction.Ascent + fraction.Descent, 6);
        Assert.NotEqual(normal.Ascent + normal.Descent, fraction.Ascent + fraction.Descent, 3);
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
    ///     Validates the dispatch of the scripted elements: none is reported, subscripts and underscripts are below the
    ///     baseline, superscripts and overscripts above it, and the scripts are one script level smaller.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_ScriptsAndUnderOver_AreDispatched()
    {
        // Act
        var (box, warnings, unsupported) = Run(
            "<msub><mi>a</mi><mi>i</mi></msub><msup><mi>a</mi><mi>n</mi></msup><msubsup><mi>a</mi><mi>i</mi><mi>n</mi></msubsup>"
            + "<munder><mi>a</mi><mi>i</mi></munder><mover><mi>a</mi><mi>n</mi></mover><munderover><mi>a</mi><mi>i</mi><mi>n</mi></munderover>"
            + "<mmultiscripts><mi>a</mi><mi>i</mi><mi>n</mi></mmultiscripts>");

        // Assert: the scripts by element, in order: sub, sup, sub sup, under, over, over under (placed in that order)
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal(["msub", "msup", "msubsup", "munder", "mover", "munderover", "mmultiscripts"], box.Children.Select(child => child.Box.Element!.Name));
        var scripts = box.Children.Select(child => child.Box.Children.Skip(1).Select(script => script.Y).ToList()).ToList();
        Assert.True(scripts[0][0] > 0 && scripts[1][0] < 0 && scripts[2][0] > 0 && scripts[2][1] < 0);
        Assert.True(scripts[3][0] > 0 && scripts[4][0] < 0 && scripts[5][0] < 0 && scripts[5][1] > 0);
        Assert.True(scripts[6][0] > 0 && scripts[6][1] < 0);
        Assert.All(box.Children, child => Assert.Equal(60, child.Box.Children[1].Box.Items.OfType<GlyphItem>().Single().Size, 9));
    }

    /// <summary>
    ///     Validates that a script is a postfix operator (MathML Core 3.2.4.2), through an embellished row too, and that an
    ///     <c>accent</c> value other than <c>true</c> or <c>false</c> is reported and an attribute an element does not
    ///     define (<c>accent</c> on <c>munder</c>) is unsupported.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_Scripts_ArePostfixOperators()
    {
        // Act
        var (box, warnings, unsupported) = Run("<msup><mi>x</mi><mo>+</mo></msup><mover accent=\"maybe\"><mi>x</mi><mrow><mo>^</mo></mrow></mover><munder accent=\"true\"><mi>x</mi><mo>-</mo></munder>");

        // Assert
        var forms = Flatten(box).Where(entry => entry.Box.Element?.Name == "mo").Select(entry => entry.Box.Operator!.Form);
        Assert.Equal([OperatorForm.Postfix, OperatorForm.Postfix, OperatorForm.Postfix], forms);
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, warning.Code);
        Assert.Contains("'accent'", warning.Message, StringComparison.Ordinal);
        Assert.Contains("'accent' of <munder>", Assert.Single(unsupported).Message, StringComparison.Ordinal);
        Assert.Equal(60, box.Children[1].Box.Children[1].Box.Children[0].Box.Items.OfType<GlyphItem>().Single().Size, 9);
    }

    /// <summary>
    ///     Validates <c>movablelimits</c>: outside display style, the limits of a summation are laid out as scripts after
    ///     it; in display style, and with <c>movablelimits="false"</c>, they are centered under and over it.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_MovableLimits_InlineBecomeScripts()
    {
        // Act
        var (inline, _, _) = Run("<munderover><mo>&#x2211;</mo><mi>i</mi><mi>n</mi></munderover>");
        var (display, _, _) = Run("<mstyle displaystyle=\"true\"><munderover><mo>&#x2211;</mo><mi>i</mi><mi>n</mi></munderover></mstyle>");
        var (fixedLimits, _, _) = Run("<munderover><mo movablelimits=\"false\">&#x2211;</mo><mi>i</mi><mi>n</mi></munderover>");

        // Assert
        var scripts = Assert.Single(inline.Children).Box;
        Assert.All(scripts.Children.Skip(1), script => Assert.Equal(scripts.Children[0].Box.Width, script.X, 9));
        var limits = Assert.Single(Assert.Single(display.Children).Box.Children).Box;
        Assert.All(limits.Children.Skip(1), limit => Assert.True(limit.X > 0 && limit.X + limit.Box.Width < limits.Width));
        var centered = Assert.Single(fixedLimits.Children).Box;
        Assert.All(centered.Children.Skip(1), limit => Assert.True(limit.X + limit.Box.Width < centered.Width));
        Assert.NotNull(scripts.Operator);
    }

    /// <summary>
    ///     Validates that a scripted element whose base is a stretchy operator is an embellished operator that its row
    ///     stretches through it (MathML Core 3.2.4.1 and 3.3.1.1; Chrome does not stretch it), and that an over brace is
    ///     stretched to the width of its base.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_EmbellishedScripted_StretchesInRow()
    {
        // Act
        var (box, _, _) = Run("<msup><mo>(</mo><mi>n</mi></msup><mspace width=\"1em\" height=\"2em\" depth=\"1em\"/><mover><mrow><mi>a</mi><mi>b</mi><mi>c</mi><mi>d</mi></mrow><mo>&#x23DE;</mo></mover>");

        // Assert
        var scripted = box.Children[0].Box;
        Assert.Equal(("(", OperatorForm.Prefix), (scripted.Operator!.CoreText, scripted.Operator.Form));
        var paren = scripted.Children[0].Box;
        Assert.True(paren.Ascent >= 200 && paren.Descent >= 100);
        var mover = box.Children[2].Box;
        var brace = mover.Children[1];
        Assert.True(brace.Box.InkBounds()!.Value.Width >= mover.Children[0].Box.Width - 1e-6);
    }

    /// <summary>
    ///     Validates that scripted elements without the in-flow children MathML Core requires, and invalid
    ///     <c>mmultiscripts</c>, are rows of their children with located <see cref="MathMlDiagnosticCodes.ChildCount"/>
    ///     warnings.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_InvalidMultiscripts_IsRowWithChildCountWarning()
    {
        // Act
        var (box, warnings, unsupported) = Run(
            "<msub><mi>a</mi></msub><munderover><mi>a</mi><mi>b</mi></munderover>"
            + "<mmultiscripts><mi>a</mi><mi>b</mi></mmultiscripts><mmultiscripts><mprescripts/><mi>a</mi><mi>b</mi></mmultiscripts>"
            + "<mmultiscripts><mi>a</mi><mprescripts/><mprescripts/></mmultiscripts><mmultiscripts><mi>a</mi><mprescripts/><mi>b</mi></mmultiscripts><mmultiscripts/>");

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal(7, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MathMlDiagnosticCodes.ChildCount, warning.Code));
        Assert.Contains("<msub> has 1 in-flow children instead of two", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("instead of three", warnings[1].Message, StringComparison.Ordinal);
        Assert.Contains("odd number of postscripts", warnings[2].Message, StringComparison.Ordinal);
        Assert.Contains("base is <mprescripts>", warnings[3].Message, StringComparison.Ordinal);
        Assert.Contains("more than one <mprescripts>", warnings[4].Message, StringComparison.Ordinal);
        Assert.Contains("odd number of prescripts", warnings[5].Message, StringComparison.Ordinal);
        Assert.Contains("no base", warnings[6].Message, StringComparison.Ordinal);
        Assert.All(box.Children.Take(3), child => Assert.All(child.Box.Children, grandChild => Assert.Equal(0, grandChild.Y)));
    }

    /// <summary>
    ///     Validates that <c>none</c> and <c>mprescripts</c> are laid out as empty rows without a report, inside and
    ///     outside <c>mmultiscripts</c>.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_NoneAndPrescripts_AreNotReported()
    {
        // Act
        var (box, warnings, unsupported) = Run("<mmultiscripts><mi>a</mi><none/><mi>b</mi><mprescripts/><mi>c</mi><none/></mmultiscripts><none/><mprescripts/>");

        // Assert
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        var empties = Flatten(box).Where(entry => entry.Box.Element?.Name is "none" or "mprescripts").ToList();
        Assert.Equal(5, empties.Count);
        Assert.All(empties, entry => Assert.Equal((0.0, 0.0, 0.0), (entry.Box.Width, entry.Box.Ascent, entry.Box.Descent)));
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
