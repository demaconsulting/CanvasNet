using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="RowLayout"/>: baseline alignment, operator spacing, embellished and space-like groups,
///     the italic correction, the stretching of stretchy operators, <c>mpadded</c> and <c>merror</c>, on boxes built
///     directly.
/// </summary>
public class RowLayoutTests
{
    /// <summary>The style of the tests: a font size of 100.</summary>
    private static readonly MathStyle Style = new(100, false, 0, new Rgba32(0, 0, 0, 255), null);

    /// <summary>
    ///     Validates that the children are placed left to right on a shared baseline, with the maximum ascent and
    ///     descent.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_Children_ShareBaselineAndTakeMaximumExtents()
    {
        // Arrange
        var a = new MathBox(10, 5, 2);
        var b = new MathBox(20, 8, 1);

        // Act
        var row = Rows().LayoutRow([a, b], isMath: false);

        // Assert
        Assert.Equal((30.0, 8.0, 2.0), (row.Width, row.Ascent, row.Descent));
        Assert.Equal([(0.0, 0.0), (10.0, 0.0)], row.Children.Select(child => (child.X, child.Y)));
        Assert.Equal((0.0, 0.0, 0.0), (Rows().LayoutRow([], false).Width, Rows().LayoutRow([], false).Ascent, Rows().LayoutRow([], false).Descent));
    }

    /// <summary>
    ///     Validates that an operator child gets its leading and trailing spaces.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_Operator_GetsItsSpacing()
    {
        // Arrange
        var operand = new MathBox(10, 5, 0);
        var op = new MathBox(6, 4, 0) { Operator = new EmbellishedOperator(3, 4) };

        // Act
        var row = Rows().LayoutRow([operand, op, operand], isMath: false);

        // Assert
        Assert.Equal([0.0, 13.0, 23.0], row.Children.Select(child => child.X));
        Assert.Equal(33, row.Width);
        Assert.Null(row.Operator);
    }

    /// <summary>
    ///     Validates the grouping rules: a row of one operator and space-like elements is an embellished operator that
    ///     adds no spacing inside (unless it is <c>math</c>), and a row of space-like elements is space-like.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_Grouping_PropagatesOperatorAndSpaceLikeness()
    {
        // Arrange
        var op = new MathBox(6, 4, 0) { Operator = new EmbellishedOperator(3, 4) };
        var space = new MathBox(2, 0, 0) { IsSpaceLike = true };
        var operand = new MathBox(10, 5, 0);

        // Act
        var embellished = Rows().LayoutRow([space, op], isMath: false);
        var math = Rows().LayoutRow([space, op], isMath: true);
        var plain = Rows().LayoutRow([operand, op], isMath: false);
        var spaces = Rows().LayoutRow([space, space], isMath: false);

        // Assert
        Assert.Equal((op.Operator, 8.0), (embellished.Operator, embellished.Width));
        Assert.Equal(((EmbellishedOperator?)null, 15.0), (math.Operator, math.Width));
        Assert.Null(plain.Operator);
        Assert.True(spaces.IsSpaceLike);
        Assert.False(plain.IsSpaceLike);
    }

    /// <summary>
    ///     Validates that the italic correction is not added between children (as Chrome) and that the row takes the
    ///     italic correction of its last child.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_ItalicCorrection_ComesFromTheLastChild()
    {
        // Arrange
        var slanted = new MathBox(10, 5, 0) { ItalicCorrection = 2 };
        var upright = new MathBox(10, 5, 0);

        // Act
        var row = Rows().LayoutRow([slanted, upright, slanted], isMath: false);

        // Assert
        Assert.Equal([0.0, 10.0, 20.0], row.Children.Select(child => child.X));
        Assert.Equal((30.0, 2.0), (row.Width, row.ItalicCorrection));
    }

    /// <summary>
    ///     Validates every attribute of <c>mpadded</c>: the requested width, height and depth, the lspace and voffset that
    ///     move the inner box, and the percentages and negative values that are ignored or clamped.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutPadded_Attributes_SetSizeAndMoveContent()
    {
        // Arrange
        var inner = new MathBox(30, 40, 10);
        var sink = new MathMlReportSink(10, false);
        var padded = Node("mpadded", [], ("width", "2em"), ("height", "1em"), ("depth", "0.5em"), ("lspace", "0.25em"), ("voffset", "0.2em"));
        var percentages = Node("mpadded", [], ("width", "50%"), ("height", "-1em"), ("lspace", "-1em"));
        var unsupported = new List<MathMlUnsupportedFeature>();

        // Act
        var box = Rows(sink).LayoutPadded(padded, Style, inner);
        var clamped = Rows(sink).LayoutPadded(percentages, Style, inner);
        sink.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal((200.0, 100.0, 50.0), (box.Width, box.Ascent, box.Descent));
        Assert.Equal((25.0, -20.0), (box.Children[0].X, box.Children[0].Y));
        Assert.Equal((30.0, 0.0, 10.0, 0.0), (clamped.Width, clamped.Ascent, clamped.Descent, clamped.Children[0].X));
        Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Validates the border of <c>merror</c>: the box grows by one pixel on every side and the border covers it.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutError_AddsBorderPadding()
    {
        // Arrange
        var inner = new MathBox(10, 5, 2);

        // Act
        var box = RowLayout.LayoutError(inner);

        // Assert
        Assert.Equal((12.0, 6.0, 3.0), (box.Width, box.Ascent, box.Descent));
        Assert.Equal(1, box.Children[0].X);
        var border = Assert.IsType<BorderItem>(Assert.Single(box.Items));
        Assert.Equal((new MathRect(0, -6, 12, 9), 1.0, new Rgba32(255, 0, 0, 255)), (border.Rect, border.Width, border.Color));
    }

    /// <summary>
    ///     Validates the stretch pass: a stretchy operator with a block stretch axis is laid out again with the largest
    ///     ascent and descent of its siblings, and its new box is placed.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_StretchyParens_CoverTallestSibling()
    {
        // Arrange
        var paren = Stretchy(StretchAxis.Block);
        var tall = new MathBox(10, 50, 5);
        var deep = new MathBox(10, 20, 30);
        var constraints = new List<(int Index, BlockStretchConstraint Constraint)>();
        MathBox Relayout(int index, BlockStretchConstraint constraint)
        {
            constraints.Add((index, constraint));
            return new MathBox(8, constraint.Ascent, constraint.Descent) { Operator = paren.Operator };
        }

        // Act
        var row = Rows().LayoutRow([paren, tall, deep, paren], isMath: false, Relayout);

        // Assert
        Assert.Equal([(0, new BlockStretchConstraint(50, 30)), (3, new BlockStretchConstraint(50, 30))], constraints);
        Assert.Equal((50.0, 30.0), (row.Ascent, row.Descent));
        Assert.Equal(8, row.Children[0].Box.Width);
    }

    /// <summary>
    ///     Validates that when every child stretches, the operators are first laid out with the constraint (0, 0) and then
    ///     stretched to the largest of those sizes (MathML Core 3.3.1.1).
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_AllChildrenStretchy_UseUnconstrainedSize()
    {
        // Arrange
        var op = Stretchy(StretchAxis.Block);
        var constraints = new List<BlockStretchConstraint>();
        MathBox Relayout(int index, BlockStretchConstraint constraint)
        {
            constraints.Add(constraint);
            var zero = constraint == new BlockStretchConstraint(0, 0);
            return new MathBox(8, zero ? 40 + index : constraint.Ascent, zero ? 10 : constraint.Descent) { Operator = op.Operator };
        }

        // Act
        Rows().LayoutRow([op, op], isMath: false, Relayout);

        // Assert
        Assert.Equal(
            [new BlockStretchConstraint(0, 0), new BlockStretchConstraint(0, 0), new BlockStretchConstraint(41, 10), new BlockStretchConstraint(41, 10)],
            constraints);
    }

    /// <summary>
    ///     Validates that an operator that is not stretchy, or stretches along the inline axis, is not laid out again, and
    ///     that a row without a relayout (one that passes its parent's constraint on) stretches nothing.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_NonStretchyOrInlineAxis_AreNotStretched()
    {
        // Arrange
        var calls = 0;
        MathBox Relayout(int index, BlockStretchConstraint constraint)
        {
            calls++;
            return new MathBox(1, 1, 1);
        }

        var tall = new MathBox(10, 50, 5);
        var arrow = Stretchy(StretchAxis.Inline);
        var plain = new MathBox(6, 4, 0) { Operator = new EmbellishedOperator(3, 4) };

        // Act
        Rows().LayoutRow([arrow, tall, plain], isMath: false, Relayout);
        var unstretched = Rows().LayoutRow([Stretchy(StretchAxis.Block), tall], isMath: false);

        // Assert
        Assert.Equal(0, calls);
        Assert.Equal(6, unstretched.Children[0].Box.Width);
    }

    /// <summary>
    ///     Validates that the spacing of a stretched operator comes from its embellished-operator data.
    /// </summary>
    [Fact]
    public void RowLayout_LayoutRow_OperatorSpacing_FromEmbellishedOperator()
    {
        // Arrange
        var operand = new MathBox(10, 50, 0);
        var op = Stretchy(StretchAxis.Block);
        MathBox Relayout(int index, BlockStretchConstraint constraint) =>
            new(8, constraint.Ascent, constraint.Descent) { Operator = new EmbellishedOperator(2, 5) { Stretchy = true } };

        // Act
        var row = Rows().LayoutRow([operand, op, operand], isMath: false, Relayout);

        // Assert
        Assert.Equal([0.0, 12.0, 25.0], row.Children.Select(child => child.X));
        Assert.Equal(35, row.Width);
    }

    /// <summary>
    ///     Creates a box of a stretchy embellished operator.
    /// </summary>
    /// <param name="axis">Its stretch axis.</param>
    /// <returns>The box.</returns>
    private static MathBox Stretchy(StretchAxis axis) =>
        new(6, 4, 1) { Operator = new EmbellishedOperator(0, 0) { Stretchy = true, Axis = axis } };

    /// <summary>
    ///     Creates the row layout of a call.
    /// </summary>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <returns>The row layout.</returns>
    private static RowLayout Rows(MathMlReportSink? sink = null) =>
        new(new StyleResolver(MathFont.Default, sink ?? new MathMlReportSink(10, false)));
}
