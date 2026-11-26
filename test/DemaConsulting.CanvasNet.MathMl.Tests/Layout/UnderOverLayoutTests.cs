using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="UnderOverLayout"/>: the gaps of limits, accents, other scripts and the scripts of a
///     horizontally stretchy operator (MathML Core 3.4.2.3-3.4.2.5) from the MATH constants of the bundled font, the
///     centering of the children, and the inline stretching of operators (3.4.2.2). The children are synthetic boxes; the
///     font is the bundled Noto Sans Math (1000 units per em, so a constant of n units is n/10 CSS pixels at 100 px).
/// </summary>
public class UnderOverLayoutTests
{
    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The compact style of the tests at 100 px.</summary>
    private static readonly MathStyle Compact = new(100, false, 0, Black, null);

    /// <summary>
    ///     Validates the limits of a large operator: the overscript UpperLimitGapMin (100) above it with its baseline at
    ///     least UpperLimitBaselineRiseMin (244) above its top, the underscript LowerLimitGapMin (100) below it with its
    ///     baseline at least LowerLimitBaselineDropMin (630) below its bottom, no extra space, and the scripts moved by
    ///     half the italic correction of the operator.
    /// </summary>
    [Fact]
    public void UnderOverLayout_LayoutUnderOver_LargeOp_UsesLimitConstants()
    {
        // Arrange
        var sum = new MathBox(100, 80, 50) { ItalicCorrection = 10, Operator = new EmbellishedOperator(0, 0) { LargeOp = true } };
        var under = new MathBox(30, 20, 5);
        var over = new MathBox(40, 30, 3);

        // Act
        var box = new UnderOverLayout(MathFont.Default).Layout(sum, under, over, Compact, false, false, sum.Operator);

        // Assert: base, overscript, underscript
        Assert.Equal(-(80 + 21.4 + 3), box.Children[1].Y, 9);
        Assert.Equal(50 + 43 + 20, box.Children[2].Y, 9);
        Assert.Equal(104.4 + 30, box.Ascent, 9);
        Assert.Equal(113 + 5, box.Descent, 9);
        Assert.Equal([0.0, 35.0, 30.0], box.Children.Select(child => Math.Round(child.X, 9)));
        Assert.Same(sum.Operator, box.Operator);
    }

    /// <summary>
    ///     Validates an accent overscript: it sits on the base, raised so that its bottom is at least AccentBaseHeight
    ///     (556) above the baseline, centered, with OverbarExtraAscender (71) above it.
    /// </summary>
    [Fact]
    public void UnderOverLayout_LayoutOver_Accent_UsesAccentBaseHeight()
    {
        // Arrange: a hat whose ink is above its baseline (a negative descent)
        var layout = new UnderOverLayout(MathFont.Default);
        var hat = new MathBox(0, 77, -60);

        // Act
        var low = layout.Layout(new MathBox(52, 54, 0), null, hat, Compact, false, true);
        var tall = layout.Layout(new MathBox(52, 70, 0), null, hat, Compact, false, true);

        // Assert
        Assert.Equal(-(54 + 1.6 - 60), low.Children[1].Y, 9);
        Assert.Equal(26, low.Children[1].X, 9);
        Assert.Equal(54 + 1.6 + 17 + 7.1, low.Ascent, 9);
        Assert.Equal(-(70 - 60), tall.Children[1].Y, 9);
    }

    /// <summary>
    ///     Validates the scripts that are neither limits nor accents: OverbarVerticalGap (150) and UnderbarVerticalGap
    ///     (150) between the boxes, with OverbarExtraAscender and UnderbarExtraDescender (71); an accent underscript
    ///     touches the base.
    /// </summary>
    [Fact]
    public void UnderOverLayout_LayoutOver_NotAccent_UsesOverbarGap()
    {
        // Arrange
        var layout = new UnderOverLayout(MathFont.Default);
        var @base = new MathBox(52, 54, 0);

        // Act
        var over = layout.Layout(@base, null, new MathBox(34, 33, 1), Compact, false, false);
        var under = layout.Layout(@base, new MathBox(30, 20, 5), null, Compact, false, false);
        var accentUnder = layout.Layout(@base, new MathBox(30, 20, 5), null, Compact, true, false);

        // Assert
        Assert.Equal(-70, over.Children[1].Y, 9);
        Assert.Equal(70 + 33 + 7.1, over.Ascent, 9);
        Assert.Equal(35, under.Children[1].Y, 9);
        Assert.Equal(35 + 5 + 7.1, under.Descent, 9);
        Assert.Equal(20, accentUnder.Children[1].Y, 9);
    }

    /// <summary>
    ///     Validates that the children are centered on each other and the box is as wide as the widest.
    /// </summary>
    [Fact]
    public void UnderOverLayout_LayoutUnderOver_CentersOnTheWidestChild()
    {
        // Act
        var box = new UnderOverLayout(MathFont.Default).Layout(new MathBox(40, 10, 0), new MathBox(100, 10, 0), new MathBox(60, 10, 0), Compact, false, false);

        // Assert: base, overscript, underscript
        Assert.Equal(100, box.Width, 9);
        Assert.Equal([30.0, 20.0, 0.0], box.Children.Select(child => Math.Round(child.X, 9)));
        Assert.Throws<ArgumentException>(() => new UnderOverLayout(MathFont.Default).Layout(new MathBox(1, 1, 1), null, null, Compact, false, false));
    }

    /// <summary>
    ///     Validates the scripts of an operator stretchy along the inline axis: StretchStackGapBelowMin (100) above it with
    ///     the baseline of the overscript at least StretchStackTopShiftUp (244) above its top, StretchStackGapAboveMin (100)
    ///     below it with the baseline of the underscript at least StretchStackBottomShiftDown (559) below its bottom, no
    ///     extra space, and a box that reaches down to the baseline.
    /// </summary>
    [Fact]
    public void UnderOverLayout_LayoutUnderOver_StretchyBase_UsesStretchStackConstants()
    {
        // Arrange: a brace above the baseline
        var brace = new MathBox(100, 90, -65) { Operator = new EmbellishedOperator(0, 0) { Stretchy = true, Axis = StretchAxis.Inline } };

        // Act
        var box = new UnderOverLayout(MathFont.Default).Layout(brace, new MathBox(30, 20, 0), new MathBox(30, 33, 0), Compact, false, false);

        // Assert
        Assert.Equal(-(90 + 24.4), box.Children[1].Y, 9);
        Assert.Equal(-65 + 35.9 + 20, box.Children[2].Y, 9);
        Assert.Equal(90 + 24.4 + 33, box.Ascent, 9);
        Assert.Equal(0, box.Descent, 9);
    }

    /// <summary>
    ///     Validates that the operators stretchy along the inline axis are laid out again with the width of the widest
    ///     other child, and that a base laid out with the constraint of its parent does not take part.
    /// </summary>
    [Fact]
    public void UnderOverLayout_StretchInline_StretchesOperatorsToTheWidestChild()
    {
        // Arrange
        var stretchy = new EmbellishedOperator(0, 0) { Stretchy = true, Axis = StretchAxis.Inline };
        var children = new[] { new MathBox(50, 10, 0), new MathBox(10, 5, 0) { Operator = stretchy }, new MathBox(30, 10, 0) };
        var fixedBase = new[] { new MathBox(80, 10, 0) { Operator = stretchy }, new MathBox(10, 5, 0) { Operator = stretchy } };
        var calls = new List<(int Index, double Width)>();
        MathBox Relayout(int index, double width)
        {
            calls.Add((index, width));
            return new MathBox(width, 5, 0) { Operator = stretchy };
        }

        // Act
        var result = UnderOverLayout.StretchInline(children, false, Relayout);
        var fixedResult = UnderOverLayout.StretchInline(fixedBase, true, Relayout);
        var none = UnderOverLayout.StretchInline([new MathBox(1, 1, 1)], false, Relayout);

        // Assert
        Assert.Equal([(1, 50.0), (1, 80.0)], calls);
        Assert.Equal(50, result[1].Width);
        Assert.Same(fixedBase[0], fixedResult[0]);
        Assert.Single(none);
    }

    /// <summary>
    ///     Validates that when every child stretches, they are laid out with a zero constraint first and then with the width
    ///     of the widest of them.
    /// </summary>
    [Fact]
    public void UnderOverLayout_StretchInline_AllStretchy_UsesZeroTarget()
    {
        // Arrange
        var stretchy = new EmbellishedOperator(0, 0) { Stretchy = true, Axis = StretchAxis.Inline };
        var children = new[] { new MathBox(10, 5, 0) { Operator = stretchy }, new MathBox(20, 5, 0) { Operator = stretchy } };
        var calls = new List<(int Index, double Width)>();
        MathBox Relayout(int index, double width)
        {
            calls.Add((index, width));
            return new MathBox(width == 0 ? 15 + (10 * index) : width, 5, 0) { Operator = stretchy };
        }

        // Act
        var result = UnderOverLayout.StretchInline(children, false, Relayout);

        // Assert
        Assert.Equal([(0, 0.0), (1, 0.0), (0, 25.0), (1, 25.0)], calls);
        Assert.All(result, box => Assert.Equal(25, box.Width));
    }
}
