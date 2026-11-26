namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays out <c>munder</c>, <c>mover</c> and <c>munderover</c> (MathML Core 3.4.2.3-3.4.2.5): the scripts centered
///     under and over their base, as limits of a large operator, as the stack of a horizontally stretched operator, or as
///     accents; and stretches their stretchy operators along the inline axis (3.4.2.2).
/// </summary>
/// <remarks>
///     <para>
///         <b>Inline stretching (3.4.2.2).</b> The children that are embellished operators stretchy along the inline
///         axis are laid out again with an inline stretch size constraint: the width of the widest other child (or, when
///         every child stretches, of the widest of them laid out with a zero constraint). A base laid out with the
///         constraint of its parent (the element is itself an embellished operator) does not stretch again but counts
///         toward the width, where MathML Core takes the width from the other children only (a documented difference).
///     </para>
///     <para>
///         <b>Horizontal placement.</b> The children are centered on each other, the underscript moved left and the
///         overscript right by half the italic correction of a large operator base (LargeOpItalicCorrection); the box is
///         as wide as the union of the children. As Chrome, an overscript is centered rather than placed by its top accent
///         attachment (MathML Core places it by the top accent attachment of the overscript when it has one).
///     </para>
///     <para>
///         <b>Vertical placement.</b> The gap between a script and the base depends on the base, in the order of MathML
///         Core: a large operator gets its limits at UpperLimitGapMin and LowerLimitGapMin, with their baselines at least
///         UpperLimitBaselineRiseMin above and LowerLimitBaselineDropMin below it; an operator stretchy along the inline
///         axis gets its scripts at StretchStackGapBelowMin and StretchStackGapAboveMin, with their baselines at least
///         StretchStackTopShiftUp above and StretchStackBottomShiftDown below it; otherwise an accent overscript sits on the
///         base, raised so that its bottom is at least AccentBaseHeight above the baseline, an accent underscript touches
///         the base, and other scripts are OverbarVerticalGap above and UnderbarVerticalGap below it, with
///         OverbarExtraAscender above and UnderbarExtraDescender below the box. As Chrome, the gaps are measured between the
///         boxes (MathML Core measures the gap of an accent and of a script that is not a limit from the baseline of the
///         script, which would move it by its descent or ascent). The ascent and the descent of the element are at least
///         zero, as Chrome.
///     </para>
///     <para>
///         The class holds no state beyond the font; an instance is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class UnderOverLayout
{
    /// <summary>The math font, whose constants place the scripts.</summary>
    private readonly MathFont _font;

    /// <summary>
    ///     Initializes the under- and overscript layout.
    /// </summary>
    /// <param name="font">The math font.</param>
    public UnderOverLayout(MathFont font)
    {
        _font = font;
    }

    /// <summary>
    ///     Stretches the operators of an under- or overscript element along the inline axis (MathML Core 3.4.2.2).
    /// </summary>
    /// <param name="children">The boxes of the base and the scripts in order, laid out without an inline stretch constraint.</param>
    /// <param name="baseIsFixed">Whether the base was laid out with the stretch constraint of the parent, so it does not stretch here.</param>
    /// <param name="relayout">Lays a child out again with an inline stretch size constraint (by its index).</param>
    /// <returns>The boxes with the stretchy operators laid out again.</returns>
    public static IReadOnlyList<MathBox> StretchInline(IReadOnlyList<MathBox> children, bool baseIsFixed, Func<int, double, MathBox> relayout)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(relayout);
        var toStretch = Enumerable.Range(0, children.Count)
            .Where(i => (i > 0 || !baseIsFixed) && children[i].Operator is { Stretchy: true, Axis: StretchAxis.Inline })
            .ToList();
        if (toStretch.Count == 0)
        {
            return children;
        }

        // The children that do not stretch give the target; when there are none, the operators at their smallest size do
        var boxes = children.ToArray();
        var measured = Enumerable.Range(0, boxes.Length).Except(toStretch).ToList();
        if (measured.Count == 0)
        {
            foreach (var i in toStretch)
            {
                boxes[i] = relayout(i, 0);
            }

            measured = toStretch;
        }

        var target = measured.Max(i => boxes[i].Width);
        foreach (var i in toStretch)
        {
            boxes[i] = relayout(i, target);
        }

        return boxes;
    }

    /// <summary>
    ///     Lays out a base with an underscript, an overscript or both (MathML Core 3.4.2.3-3.4.2.5).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="under">The box of the underscript, or null for <c>mover</c>.</param>
    /// <param name="over">The box of the overscript, or null for <c>munder</c>.</param>
    /// <param name="style">The computed style of the element (its font size scales the constants).</param>
    /// <param name="accentUnder">Whether the underscript is an accent (<c>accentunder="true"</c>).</param>
    /// <param name="accent">Whether the overscript is an accent (<c>accent="true"</c>).</param>
    /// <param name="embellished">The operator of an embellished element (of its base), or null.</param>
    /// <returns>The box.</returns>
    /// <exception cref="ArgumentException">Thrown when both scripts are null.</exception>
    public MathBox Layout(MathBox @base, MathBox? under, MathBox? over, MathStyle style, bool accentUnder, bool accent, EmbellishedOperator? embellished = null)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(style);
        if (under is null && over is null)
        {
            throw new ArgumentException("An under- or overscript element has at least one script.", nameof(under));
        }

        // The horizontal extents about the center of the base; the scripts of a large operator move by its italic correction
        var largeOpItalic = @base.Operator is { LargeOp: true } ? @base.ItalicCorrection : 0;
        var left = -@base.Width / 2;
        var right = @base.Width / 2;
        if (under is not null)
        {
            left = Math.Min(left, (-under.Width / 2) - (largeOpItalic / 2));
            right = Math.Max(right, (under.Width / 2) - (largeOpItalic / 2));
        }

        if (over is not null)
        {
            left = Math.Min(left, (-over.Width / 2) + (largeOpItalic / 2));
            right = Math.Max(right, (over.Width / 2) + (largeOpItalic / 2));
        }

        // The vertical gaps by the kind of base, then the boxes stacked
        var size = style.FontSize;
        var children = new List<PlacedBox>(3) { new(@base, -left - (@base.Width / 2), 0) };
        var ascent = @base.Ascent;
        var descent = @base.Descent;
        if (over is not null)
        {
            var (gap, extra) = OverGap(@base, over, accent, size);
            var baseline = @base.Ascent + gap + over.Descent;
            children.Add(new PlacedBox(over, -left - (over.Width / 2) + (largeOpItalic / 2), -baseline));
            ascent = Math.Max(ascent, baseline + over.Ascent + extra);
            descent = Math.Max(descent, over.Descent - baseline);
        }

        if (under is not null)
        {
            var (gap, extra) = UnderGap(@base, under, accentUnder, size);
            var baseline = @base.Descent + gap + under.Ascent;
            children.Add(new PlacedBox(under, -left - (under.Width / 2) - (largeOpItalic / 2), baseline));
            ascent = Math.Max(ascent, under.Ascent - baseline);
            descent = Math.Max(descent, baseline + under.Descent + extra);
        }

        return new MathBox(right - left, Math.Max(0, ascent), Math.Max(0, descent), children) { Operator = embellished };
    }

    /// <summary>
    ///     Computes the gap between the top of the base and the bottom of the overscript, and the extra space above the
    ///     overscript (MathML Core 3.4.2.4, measured between the boxes as Chrome does).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="over">The box of the overscript.</param>
    /// <param name="accent">Whether the overscript is an accent.</param>
    /// <param name="size">The font size of the element.</param>
    /// <returns>The gap and OverExtraAscender.</returns>
    private (double Gap, double Extra) OverGap(MathBox @base, MathBox over, bool accent, double size)
    {
        if (@base.Operator is { LargeOp: true })
        {
            return (Math.Max(Constant(MathConstant.UpperLimitGapMin, size), Constant(MathConstant.UpperLimitBaselineRiseMin, size) - over.Descent), 0);
        }

        if (@base.Operator is { Stretchy: true, Axis: StretchAxis.Inline })
        {
            return (Math.Max(Constant(MathConstant.StretchStackGapBelowMin, size), Constant(MathConstant.StretchStackTopShiftUp, size) - over.Descent), 0);
        }

        var gap = accent ? Math.Max(0, Constant(MathConstant.AccentBaseHeight, size) - @base.Ascent) : Constant(MathConstant.OverbarVerticalGap, size);
        return (gap, Constant(MathConstant.OverbarExtraAscender, size));
    }

    /// <summary>
    ///     Computes the gap between the bottom of the base and the top of the underscript, and the extra space below the
    ///     underscript (MathML Core 3.4.2.3, measured between the boxes as Chrome does).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="under">The box of the underscript.</param>
    /// <param name="accentUnder">Whether the underscript is an accent.</param>
    /// <param name="size">The font size of the element.</param>
    /// <returns>The gap and UnderExtraDescender.</returns>
    private (double Gap, double Extra) UnderGap(MathBox @base, MathBox under, bool accentUnder, double size)
    {
        if (@base.Operator is { LargeOp: true })
        {
            return (Math.Max(Constant(MathConstant.LowerLimitGapMin, size), Constant(MathConstant.LowerLimitBaselineDropMin, size) - under.Ascent), 0);
        }

        if (@base.Operator is { Stretchy: true, Axis: StretchAxis.Inline })
        {
            return (Math.Max(Constant(MathConstant.StretchStackGapAboveMin, size), Constant(MathConstant.StretchStackBottomShiftDown, size) - under.Ascent), 0);
        }

        var gap = accentUnder ? 0 : Constant(MathConstant.UnderbarVerticalGap, size);
        return (gap, Constant(MathConstant.UnderbarExtraDescender, size));
    }

    /// <summary>
    ///     Gets a length constant at a font size.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The scaled value.</returns>
    private double Constant(MathConstant constant, double fontSize) => _font.Constant(constant, fontSize);
}
