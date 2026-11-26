using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="FractionLayout"/>: the shifts and gaps of a fraction with a bar and of a stack (MathML Core
///     3.3.2) from the MATH constants of the bundled font, the bar on the math axis, the centering and padding of the
///     children, and the resolution of <c>linethickness</c>. The children are synthetic boxes; the font is the bundled
///     Noto Sans Math (1000 units per em, so a constant of n units is n/10 CSS pixels at 100 px).
/// </summary>
public class FractionLayoutTests
{
    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The compact style of the tests at 100 px.</summary>
    private static readonly MathStyle Compact = new(100, false, 0, Black, null);

    /// <summary>The default rule thickness at 100 px (FractionRuleThickness 71).</summary>
    private const double Thickness = 7.1;

    /// <summary>The math axis at 100 px (AxisHeight 278).</summary>
    private const double Axis = 27.8;

    /// <summary>
    ///     Validates that a fraction in display style uses the display-style shifts when the children are small:
    ///     FractionNumeratorDisplayStyleShiftUp (704) and FractionDenominatorDisplayStyleShiftDown (673).
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_DisplayStyle_UsesDisplayShiftsAndGaps()
    {
        // Arrange
        var numerator = new MathBox(20, 10, 0);
        var denominator = new MathBox(30, 10, 0);

        // Act
        var box = Layout().Layout(numerator, denominator, Compact with { DisplayStyle = true }, Thickness);

        // Assert
        Assert.Equal(-70.4, box.Children[0].Y, 9);
        Assert.Equal(67.3, box.Children[1].Y, 9);
        Assert.Equal(80.4, box.Ascent, 9);
        Assert.Equal(67.3, box.Descent, 9);
    }

    /// <summary>
    ///     Validates that the descent of a fraction is at least zero (MathML Core 3.3.2.1 and 3.3.2.2, as Chrome), with a
    ///     bar and as a stack, when its denominator lies wholly above the baseline (a raised mark such as a ring above).
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_RaisedDenominator_DescentIsZero()
    {
        // Arrange: a denominator whose ink is 10 to 110 px above its baseline
        var numerator = new MathBox(20, 10, 0);
        var denominator = new MathBox(20, 110, -100);

        // Act
        var bar = Layout().Layout(numerator, denominator, Compact, Thickness);
        var stack = Layout().Layout(numerator, denominator, Compact, 0);

        // Assert: the denominator ends above the baseline, and the box still reaches down to it
        Assert.True(bar.Children[1].Y + denominator.Descent < 0);
        Assert.True(stack.Children[1].Y + denominator.Descent < 0);
        Assert.Equal(0, bar.Descent);
        Assert.Equal(0, stack.Descent);
    }
    /// <summary>
    ///     Validates that a compact fraction uses the standard shifts when the children are small:
    ///     FractionNumeratorShiftUp (529) and FractionDenominatorShiftDown (320).
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_Compact_UsesTextShiftsAndGaps()
    {
        // Act
        var box = Layout().Layout(new MathBox(20, 10, 0), new MathBox(20, 10, 0), Compact, Thickness);

        // Assert
        Assert.Equal([-52.9, 32.0], box.Children.Select(child => System.Math.Round(child.Y, 9)));
    }

    /// <summary>
    ///     Validates that tall children are pushed away from the bar: the numerator keeps FractionNumeratorGapMin (71)
    ///     between its bottom and the bar, the denominator FractionDenominatorGapMin (71) between the bar and its top.
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_TallChildren_PushedByGapRules()
    {
        // Arrange: a numerator that descends 40 px and a denominator 60 px tall
        var numerator = new MathBox(20, 10, 40);
        var denominator = new MathBox(20, 60, 0);

        // Act
        var box = Layout().Layout(numerator, denominator, Compact, Thickness);

        // Assert: the gaps to the bar (the axis plus or minus half the thickness) are exactly the minimums
        var numeratorShift = -box.Children[0].Y;
        var denominatorShift = box.Children[1].Y;
        Assert.Equal(Axis + (Thickness / 2) + 7.1 + 40, numeratorShift, 9);
        Assert.Equal((Thickness / 2) + 7.1 + 60 - Axis, denominatorShift, 9);
        Assert.Equal(numeratorShift + 10, box.Ascent, 9);
    }

    /// <summary>
    ///     Validates the bar: a rule of the line thickness centered on the math axis across the content box, in the color
    ///     of the fraction.
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_Bar_IsCenteredOnAxisWithItsThickness()
    {
        // Arrange
        var red = Compact with { Color = new Rgba32(255, 0, 0, 255) };

        // Act
        var box = Layout().Layout(new MathBox(20, 10, 0), new MathBox(30, 10, 0), red, 3);

        // Assert
        var bar = Assert.IsType<RuleItem>(Assert.Single(box.Items));
        Assert.Equal(new Rgba32(255, 0, 0, 255), bar.Color);
        Assert.Equal(1, bar.Rect.X, 9);
        Assert.Equal(30, bar.Rect.Width, 9);
        Assert.Equal(3, bar.Rect.Height, 9);
        Assert.Equal(-Axis, bar.Rect.Y + (bar.Rect.Height / 2), 9);
    }

    /// <summary>
    ///     Validates that the children are centered in the content box and that the box is padded by 1 px on each side
    ///     (the <c>padding-inline</c> of the user agent stylesheet of MathML Core).
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_ChildrenCenteredAndPaddedByOnePixel()
    {
        // Arrange
        var op = new EmbellishedOperator(1, 2) { CoreText = "=" };

        // Act
        var box = Layout().Layout(new MathBox(20, 10, 0), new MathBox(30, 10, 0), Compact, Thickness, op);

        // Assert
        Assert.Equal(32, box.Width, 9);
        Assert.Equal([6.0, 1.0], box.Children.Select(child => child.X));
        Assert.Same(op, box.Operator);
    }

    /// <summary>
    ///     Validates the stack of a zero line thickness: StackTopShiftUp (529) and StackBottomShiftDown (320) when the gap
    ///     is large enough, both increased by half of the missing gap below StackGapMin (178), and no bar.
    /// </summary>
    [Fact]
    public void FractionLayout_Layout_ZeroThickness_UsesStackShiftsAndGap()
    {
        // Act
        var wide = Layout().Layout(new MathBox(20, 10, 5), new MathBox(20, 30, 0), Compact, 0);
        var narrow = Layout().Layout(new MathBox(20, 10, 40), new MathBox(20, 40, 0), Compact, 0);

        // Assert: (52.9 - 40) + (32 - 40) = 4.9 is 12.9 short of 17.8
        Assert.Empty(wide.Items);
        Assert.Equal([-52.9, 32.0], wide.Children.Select(child => System.Math.Round(child.Y, 9)));
        Assert.Equal([-59.35, 38.45], narrow.Children.Select(child => System.Math.Round(child.Y, 9)));
        Assert.Equal(17.8, (-narrow.Children[0].Y - 40) + (narrow.Children[1].Y - 40), 9);
    }

    /// <summary>
    ///     Validates <c>linethickness</c>: a length, a percentage of the default, a negative value (zero), the default when
    ///     absent, and the default with a warning for an invalid value such as the MathML 3 name <c>thick</c>.
    /// </summary>
    [Fact]
    public void FractionLayout_ResolveThickness_LengthPercentNegativeAndInvalid()
    {
        // Arrange
        var sink = new MathMlReportSink(10, false);
        var layout = Layout(sink);
        double Resolve(string? value) =>
            layout.ResolveThickness(value is null ? Node("mfrac", []) : Node("mfrac", [], ("linethickness", value)), Compact);

        // Act
        var values = new[] { Resolve("3px"), Resolve("200%"), Resolve("-1px"), Resolve("0"), Resolve(null), Resolve("thick") };
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal([3.0, 14.2, 0.0, 0.0, Thickness, Thickness], values.Select(value => System.Math.Round(value, 9)));
        Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Creates the fraction layout of a call.
    /// </summary>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <returns>The layout.</returns>
    private static FractionLayout Layout(MathMlReportSink? sink = null) =>
        new(MathFont.Default, new StyleResolver(MathFont.Default, sink ?? new MathMlReportSink(10, false)));
}
