using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="MathBox"/> and <see cref="MathRect"/>: the content box, the ink bounds of a tree, and
///     phantoms.
/// </summary>
public class MathBoxTests
{
    /// <summary>A color of the tests.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     Validates the content box of a box: its width, ascent above and descent below the baseline.
    /// </summary>
    [Fact]
    public void MathBox_ContentRect_SpansAscentAndDescent()
    {
        // Act
        var box = new MathBox(10, 7, 3);

        // Assert
        Assert.Equal(new MathRect(0, -7, 10, 10), box.ContentRect);
        Assert.Empty(box.Children);
        Assert.Empty(box.Items);
    }

    /// <summary>
    ///     Validates that the ink bounds unite the items of the box and of its children at their offsets.
    /// </summary>
    [Fact]
    public void MathBox_InkBounds_UnitesItemsAndChildren()
    {
        // Arrange
        var child = new MathBox(5, 5, 0, null, [new RuleItem(new MathRect(0, -5, 5, 5), Blue)]);
        var box = new MathBox(20, 5, 0, [new PlacedBox(child, 30, 4)], [new BackgroundItem(new MathRect(-2, -5, 10, 6), Blue)]);

        // Act
        var bounds = box.InkBounds();

        // Assert
        Assert.Equal(new MathRect(-2, -5, 37, 9), bounds);
    }

    /// <summary>
    ///     Validates that a phantom paints nothing and that an empty box has no ink.
    /// </summary>
    [Fact]
    public void MathBox_InkBounds_PhantomAndEmpty_AreNull()
    {
        // Arrange
        var phantom = new MathBox(5, 5, 0, null, [new RuleItem(new MathRect(0, -5, 5, 5), Blue)]) { IsPhantom = true };
        var parent = new MathBox(5, 5, 0, [new PlacedBox(phantom, 0, 0)]);

        // Act / Assert
        Assert.Null(phantom.InkBounds());
        Assert.Null(parent.InkBounds());
        Assert.Null(new MathBox(1, 1, 1).InkBounds());
    }

    /// <summary>
    ///     Validates the bounds of the paint items: a glyph without contours has none, a border its rectangle.
    /// </summary>
    [Fact]
    public void MathBox_PaintItems_ReportTheirBounds()
    {
        // Arrange
        var (font, space) = MathFont.Default.Resolve(' ');
        var glyph = new GlyphItem(font, space, 10, 0, 0, Blue, new MathRect(0, 0, 0, 0));
        var border = new BorderItem(new MathRect(1, 2, 3, 4), 1, Blue);

        // Act / Assert
        Assert.Null(glyph.Bounds);
        Assert.Equal(new MathRect(1, 2, 3, 4), border.Bounds);
        Assert.Equal(new MathRect(2, 4, 3, 4), border.Rect.Offset(1, 2));
        Assert.Equal((4.0, 6.0), (border.Rect.Right, border.Rect.Bottom));
    }

    /// <summary>
    ///     Validates the defaults of the operator records: an embellished operator neither stretches nor grows and has no
    ///     maximum size, and an empty operator context carries neither a form nor a constraint.
    /// </summary>
    [Fact]
    public void MathBox_EmbellishedOperator_DefaultsDoNotStretch()
    {
        // Act
        var op = new EmbellishedOperator(1, 2);
        var context = default(OperatorContext);

        // Assert
        Assert.Equal((1.0, 2.0, string.Empty), (op.LeadingSpace, op.TrailingSpace, op.CoreText));
        Assert.Equal((OperatorForm.Infix, OperatorCategory.Default, StretchAxis.Block), (op.Form, op.Category, op.Axis));
        Assert.False(op.Stretchy || op.Symmetric || op.LargeOp || op.MovableLimits || op.Fence || op.Separator);
        Assert.Equal((0.0, double.PositiveInfinity), (op.MinSize, op.MaxSize));
        Assert.Equal(((OperatorForm?)null, (BlockStretchConstraint?)null), (context.Form, context.Stretch));
        Assert.Equal(new BlockStretchConstraint(3, 4), (context with { Stretch = new BlockStretchConstraint(3, 4) }).Stretch);
        Assert.Null(context.InlineStretch);
        Assert.False(context.HasStretch);
    }

    /// <summary>
    ///     Validates the inline stretch size constraint of an operator context: it is carried with the form and counts as a
    ///     stretch constraint, like a block one.
    /// </summary>
    [Fact]
    public void MathBox_OperatorContext_InlineConstraint_IsAStretchConstraint()
    {
        // Act
        var inline = new OperatorContext(OperatorForm.Postfix, null, 120);
        var block = new OperatorContext(null, new BlockStretchConstraint(1, 2));

        // Assert
        Assert.Equal((OperatorForm.Postfix, 120.0), (inline.Form, inline.InlineStretch!.Value));
        Assert.True(inline.HasStretch && block.HasStretch);
        Assert.Null(block.InlineStretch);
    }
}
