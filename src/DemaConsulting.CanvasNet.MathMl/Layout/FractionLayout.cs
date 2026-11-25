using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays out <c>mfrac</c> (MathML Core 3.3.2): the numerator over the denominator, with a fraction bar on the math axis
///     or, with a zero line thickness, as a stack.
/// </summary>
/// <remarks>
///     <para>
///         <b>Line thickness.</b> The <c>linethickness</c> attribute is a CSS length or a percentage of the default
///         thickness (FractionRuleThickness of the MATH table); a negative value is a zero thickness, as Chrome draws it;
///         an invalid value (including the MathML 3 names <c>thin</c>, <c>medium</c> and <c>thick</c>, which MathML Core
///         does not define) keeps the default and is reported as <see cref="MathMlDiagnosticCodes.AttributeInvalid"/>.
///     </para>
///     <para>
///         <b>Bar (3.3.2.1).</b> With a nonzero thickness t, the numerator is shifted up by the larger of
///         FractionNumeratorShiftUp and AxisHeight + t/2 + FractionNumeratorGapMin + its descent, the denominator down by
///         the larger of FractionDenominatorShiftDown and t/2 + FractionDenominatorGapMin + its ascent - AxisHeight (the
///         display-style constants in display style); the bar is a rule of thickness t centered on the math axis across
///         the content box.
///     </para>
///     <para>
///         <b>Stack (3.3.2.2).</b> With a zero thickness, the shifts are StackTopShiftUp and StackBottomShiftDown, and
///         when the gap between the two children is less than StackGapMin, both are increased by half of the missing
///         gap.
///     </para>
///     <para>
///         <b>Box.</b> The content box is as wide as the wider child, each child centered in it; the user agent stylesheet
///         of MathML Core pads the fraction with 1px on both sides (<c>padding-inline: 1px</c>), so the box is 2px wider
///         than the content and the children are moved right by 1px. The ascent and the descent of a child are those of
///         its box (the ink of a token, as Chrome lays tokens out). An embellished fraction (its numerator is an
///         embellished operator) keeps the operator that the layout engine gives it.
///     </para>
///     <para>
///         An instance serves one call on one thread (it reports into the sink of the call).
///     </para>
/// </remarks>
internal sealed class FractionLayout
{
    /// <summary>The inline padding of a fraction on each side in CSS pixels (the user agent stylesheet of MathML Core).</summary>
    public const double InlinePadding = 1;

    /// <summary>The math font, whose constants place the children.</summary>
    private readonly MathFont _font;

    /// <summary>The style resolver of the call, which resolves <c>linethickness</c>.</summary>
    private readonly StyleResolver _styles;

    /// <summary>
    ///     Initializes the fraction layout of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="styles">The style resolver of the call (it reports an invalid <c>linethickness</c>).</param>
    public FractionLayout(MathFont font, StyleResolver styles)
    {
        _font = font;
        _styles = styles;
    }

    /// <summary>
    ///     Resolves the line thickness of a fraction: its <c>linethickness</c> attribute (a length, or a percentage of the
    ///     default), the default FractionRuleThickness when absent or invalid (reported), zero when negative.
    /// </summary>
    /// <param name="fraction">The <c>mfrac</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <returns>The thickness in CSS pixels; not negative.</returns>
    public double ResolveThickness(MathElement fraction, MathStyle style)
    {
        ArgumentNullException.ThrowIfNull(fraction);
        ArgumentNullException.ThrowIfNull(style);
        var standard = _font.Constant(MathConstant.FractionRuleThickness, style.FontSize);
        var thickness = _styles.ResolveLength(fraction, "linethickness", style, standard) ?? standard;
        return Math.Max(0, thickness);
    }

    /// <summary>
    ///     Lays out a fraction from the boxes of its numerator and denominator (MathML Core 3.3.2).
    /// </summary>
    /// <param name="numerator">The box of the numerator.</param>
    /// <param name="denominator">The box of the denominator.</param>
    /// <param name="style">The computed style of the fraction (its math style selects the constants; its color paints the bar).</param>
    /// <param name="thickness">The line thickness (<see cref="ResolveThickness"/>); zero for a stack.</param>
    /// <param name="embellished">The operator of an embellished fraction (of its numerator), or null.</param>
    /// <returns>The box of the fraction, padding included.</returns>
    public MathBox Layout(MathBox numerator, MathBox denominator, MathStyle style, double thickness, EmbellishedOperator? embellished = null)
    {
        ArgumentNullException.ThrowIfNull(numerator);
        ArgumentNullException.ThrowIfNull(denominator);
        ArgumentNullException.ThrowIfNull(style);
        var size = style.FontSize;
        var display = style.DisplayStyle;
        double numeratorShift;
        double denominatorShift;
        double ascent;
        double descent;
        if (thickness > 0)
        {
            // A fraction bar (3.3.2.1): the children keep their gaps to the bar, which is centered on the math axis
            var axis = _font.Constant(MathConstant.AxisHeight, size);
            var half = thickness / 2;
            numeratorShift = Math.Max(
                Constant(display ? MathConstant.FractionNumeratorDisplayStyleShiftUp : MathConstant.FractionNumeratorShiftUp, size),
                axis + half + Constant(display ? MathConstant.FractionNumDisplayStyleGapMin : MathConstant.FractionNumeratorGapMin, size) + numerator.Descent);
            denominatorShift = Math.Max(
                Constant(display ? MathConstant.FractionDenominatorDisplayStyleShiftDown : MathConstant.FractionDenominatorShiftDown, size),
                half + Constant(display ? MathConstant.FractionDenomDisplayStyleGapMin : MathConstant.FractionDenominatorGapMin, size) + denominator.Ascent - axis);
            ascent = Math.Max(Math.Max(numeratorShift + numerator.Ascent, denominator.Ascent - denominatorShift), axis + half);
            descent = Math.Max(Math.Max(numerator.Descent - numeratorShift, denominatorShift + denominator.Descent), half - axis);
        }
        else
        {
            // A stack (3.3.2.2): the standard shifts, both increased by half of a missing gap
            numeratorShift = Constant(display ? MathConstant.StackTopDisplayStyleShiftUp : MathConstant.StackTopShiftUp, size);
            denominatorShift = Constant(display ? MathConstant.StackBottomDisplayStyleShiftDown : MathConstant.StackBottomShiftDown, size);
            var gapMin = Constant(display ? MathConstant.StackDisplayStyleGapMin : MathConstant.StackGapMin, size);
            var gap = (numeratorShift - numerator.Descent) + (denominatorShift - denominator.Ascent);
            if (gap < gapMin)
            {
                var delta = gapMin - gap;
                numeratorShift += delta / 2;
                denominatorShift += delta - (delta / 2);
            }

            ascent = Math.Max(numeratorShift + numerator.Ascent, denominator.Ascent - denominatorShift);
            descent = Math.Max(numerator.Descent - numeratorShift, denominatorShift + denominator.Descent);
        }

        // The children centered in the content box, which the padding moves right
        var content = Math.Max(numerator.Width, denominator.Width);
        PlacedBox[] children =
        [
            new(numerator, InlinePadding + ((content - numerator.Width) / 2), -numeratorShift),
            new(denominator, InlinePadding + ((content - denominator.Width) / 2), denominatorShift),
        ];
        IPaintItem[] items = thickness > 0
            ? [new RuleItem(new MathRect(InlinePadding, -_font.Constant(MathConstant.AxisHeight, size) - (thickness / 2), content, thickness), style.Color)]
            : [];
        return new MathBox(content + (2 * InlinePadding), ascent, descent, children, items) { Operator = embellished };
    }

    /// <summary>
    ///     Gets a length constant at a font size.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The scaled value.</returns>
    private double Constant(MathConstant constant, double fontSize) => _font.Constant(constant, fontSize);
}
