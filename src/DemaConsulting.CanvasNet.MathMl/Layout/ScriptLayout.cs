namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     A subscript and superscript pair of <c>mmultiscripts</c> (MathML Core 3.4.3): the boxes of its two scripts (a
///     <c>none</c> element is an empty box).
/// </summary>
/// <param name="Sub">The box of the subscript.</param>
/// <param name="Sup">The box of the superscript.</param>
internal readonly record struct ScriptPair(MathBox Sub, MathBox Sup);

/// <summary>
///     Lays out <c>msub</c>, <c>msup</c>, <c>msubsup</c> (MathML Core 3.4.1) and <c>mmultiscripts</c> (3.4.3): the scripts
///     shifted down and up from the baseline of their base by the constants of the MATH table; also the under- and
///     overscript elements whose base has <c>movablelimits</c> outside display style (3.4.2.1).
/// </summary>
/// <remarks>
///     <para>
///         <b>Shifts (3.4.1.2-3.4.1.4).</b> A subscript is shifted down by the largest of SubscriptShiftDown, its ascent -
///         SubscriptTopMax, and SubscriptBaselineDropMin + the descent of the base; a superscript up by the largest of
///         SuperscriptShiftUp (SuperscriptShiftUpCramped when the computed <c>math-shift</c> of the scripted element is
///         <c>compact</c>), SuperscriptBottomMin + its descent, and the ascent of the base - SuperscriptBaselineDropMax.
///         With both scripts, when the gap between them is less than SubSuperscriptGapMin, the superscript is raised
///         first (while its bottom stays below SuperscriptBottomMaxWithSubscript), then the subscript is lowered. The
///         ascent and the descent of a script are those of its box (the ink of a token, as Chrome lays tokens out); the
///         ascent and the descent of the element are at least zero, as Chrome.
///     </para>
///     <para>
///         <b>Placement.</b> The subscript starts at the width of the base - LargeOpItalicCorrection (the italic
///         correction of a large operator base), the superscript at the width of the base + its italic correction (when it
///         is not a large operator); SpaceAfterScript follows the scripts. As Chrome, the italic correction is only that of
///         an operator drawn with a size variant or an assembly (an operator drawn with its normal glyph and a token such
///         as an italic <c>mi</c> have none; MathML Core 3.2.1.1 gives a single slanted glyph its italic correction),
///         except that a superscript whose ink would overlap the ink of a slanted token base (the hook of a zeta) moves
///         right by the italic correction of the base, or by the overlap when larger (<see cref="MathBox.Clearance"/>),
///         and widens the element by it, so glyphs never collide; and
///         the width is that of the base and the wider script with SpaceAfterScript, without the italic corrections (MathML
///         Core adds them), so a subscript of a large operator that is wider than its superscript is followed by
///         LargeOpItalicCorrection more space, as in Chrome (verified by the reference fixture
///         <c>scripts-largeop-wide-limit</c>).
///     </para>
///     <para>
///         <b>mmultiscripts (3.4.3.1).</b> The shifts are the largest over the pairs (each pair as an <c>msubsup</c>);
///         each prescript pair follows SpaceAfterScript and its scripts are aligned right in its column; the base and the
///         box of <c>mprescripts</c> follow the prescripts, then each postscript pair as the scripts of <c>msubsup</c>,
///         followed by SpaceAfterScript.
///     </para>
///     <para>
///         The class holds no state beyond the font; an instance is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ScriptLayout
{
    /// <summary>The math font, whose constants place the scripts.</summary>
    private readonly MathFont _font;

    /// <summary>
    ///     Initializes the script layout.
    /// </summary>
    /// <param name="font">The math font.</param>
    public ScriptLayout(MathFont font)
    {
        _font = font;
    }

    /// <summary>
    ///     Lays out a base with a subscript, a superscript or both (<c>msub</c>, <c>msup</c>, <c>msubsup</c>; MathML Core
    ///     3.4.1).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="sub">The box of the subscript, or null for <c>msup</c>.</param>
    /// <param name="sup">The box of the superscript, or null for <c>msub</c>.</param>
    /// <param name="style">The computed style of the scripted element (its font size scales the constants; its <c>math-shift</c> selects the cramped shift).</param>
    /// <param name="embellished">The operator of an embellished scripted element (of its base), or null.</param>
    /// <returns>The box.</returns>
    /// <exception cref="ArgumentException">Thrown when both scripts are null.</exception>
    public MathBox Layout(MathBox @base, MathBox? sub, MathBox? sup, MathStyle style, EmbellishedOperator? embellished = null)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(style);
        if (sub is null && sup is null)
        {
            throw new ArgumentException("A scripted element has at least one script.", nameof(sub));
        }

        // The shifts, with the gap between both scripts enforced
        var size = style.FontSize;
        var subShift = sub is null ? 0 : SubShift(@base, sub, size);
        var supShift = sup is null ? 0 : SuperShift(@base, sup, style);
        if (sub is not null && sup is not null)
        {
            (subShift, supShift) = EnforceGap(sub, sup, subShift, supShift, size);
        }

        // The scripts follow the base, moved by the italic corrections; the width is that of the base and the wider
        // script with SpaceAfterScript, without the italic corrections, as Chrome
        var (italic, largeOpItalic) = ItalicCorrections(@base);
        var children = new List<PlacedBox>(3) { new(@base, 0, 0) };
        var scripts = 0.0;
        var ascent = @base.Ascent;
        var descent = @base.Descent;
        if (sub is not null)
        {
            children.Add(new PlacedBox(sub, @base.Width - largeOpItalic, subShift));
            scripts = sub.Width;
            ascent = Math.Max(ascent, sub.Ascent - subShift);
            descent = Math.Max(descent, sub.Descent + subShift);
        }

        if (sup is not null)
        {
            // A superscript whose ink would collide with the ink of a slanted token base (the hook of a zeta) follows its
            // italic correction, as MathML Core places it; otherwise it starts at the advance, as Chrome places it
            var clearance = italic > 0 || @base.Operator is not null || @base.Children.Count > 0
                ? 0
                : MathBox.Clearance(@base.InkBounds(), @base.ItalicCorrection, sup, @base.Width, -supShift);
            children.Add(new PlacedBox(sup, @base.Width + italic + clearance, -supShift));
            scripts = Math.Max(scripts, clearance + sup.Width);
            ascent = Math.Max(ascent, sup.Ascent + supShift);
            descent = Math.Max(descent, sup.Descent - supShift);
        }

        var width = @base.Width + scripts + _font.Constant(MathConstant.SpaceAfterScript, size);
        return new MathBox(width, Math.Max(0, ascent), Math.Max(0, descent), children) { Operator = embellished };
    }

    /// <summary>
    ///     Lays out an <c>mmultiscripts</c> (MathML Core 3.4.3.1): its base with its postscript and prescript pairs.
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="postscripts">The postscript pairs in order.</param>
    /// <param name="prescripts">The prescript pairs in order.</param>
    /// <param name="separator">The box of the <c>mprescripts</c> element, or null when there is none.</param>
    /// <param name="style">The computed style of the element.</param>
    /// <param name="embellished">The operator of an embellished element (of its base), or null.</param>
    /// <returns>The box.</returns>
    public MathBox LayoutMultiscripts(
        MathBox @base,
        IReadOnlyList<ScriptPair> postscripts,
        IReadOnlyList<ScriptPair> prescripts,
        MathBox? separator,
        MathStyle style,
        EmbellishedOperator? embellished = null)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(postscripts);
        ArgumentNullException.ThrowIfNull(prescripts);
        ArgumentNullException.ThrowIfNull(style);

        // The shifts: the largest over the pairs, each pair laid out as msubsup
        var size = style.FontSize;
        var subShift = 0.0;
        var supShift = 0.0;
        foreach (var pair in postscripts.Concat(prescripts))
        {
            var (sub, sup) = EnforceGap(pair.Sub, pair.Sup, SubShift(@base, pair.Sub, size), SuperShift(@base, pair.Sup, style), size);
            subShift = Math.Max(subShift, sub);
            supShift = Math.Max(supShift, sup);
        }

        // The prescripts, aligned right in their columns, then the base, then the postscripts
        var space = _font.Constant(MathConstant.SpaceAfterScript, size);
        var children = new List<PlacedBox>((2 * (postscripts.Count + prescripts.Count)) + 2);
        var ascent = @base.Ascent;
        var descent = @base.Descent;
        var offset = 0.0;
        foreach (var pair in prescripts)
        {
            offset += space;
            var column = Math.Max(pair.Sub.Width, pair.Sup.Width);
            children.Add(new PlacedBox(pair.Sub, offset + column - pair.Sub.Width, subShift));
            children.Add(new PlacedBox(pair.Sup, offset + column - pair.Sup.Width, -supShift));
            (ascent, descent) = Extend(ascent, descent, pair, subShift, supShift);
            offset += column;
        }

        children.Add(new PlacedBox(@base, offset, 0));
        if (separator is not null)
        {
            children.Add(new PlacedBox(separator, offset, 0));
        }

        offset += @base.Width;
        var (italic, largeOpItalic) = ItalicCorrections(@base);
        foreach (var pair in postscripts)
        {
            children.Add(new PlacedBox(pair.Sub, offset - largeOpItalic, subShift));
            children.Add(new PlacedBox(pair.Sup, offset + italic, -supShift));
            (ascent, descent) = Extend(ascent, descent, pair, subShift, supShift);
            offset += Math.Max(pair.Sub.Width, pair.Sup.Width) + space;
        }

        return new MathBox(offset, Math.Max(0, ascent), Math.Max(0, descent), children) { Operator = embellished };
    }

    /// <summary>
    ///     Gets the italic corrections of a base: the one the superscript follows (none for a large operator) and the one
    ///     the subscript is pulled back by (only for a large operator). Only an operator has an italic correction, as in
    ///     Chrome (<see cref="TokenLayout"/> gives one to an operator drawn with a size variant or an assembly).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <returns>ItalicCorrection and LargeOpItalicCorrection of MathML Core 3.4.1.</returns>
    internal static (double Italic, double LargeOpItalic) ItalicCorrections(MathBox @base)
    {
        ArgumentNullException.ThrowIfNull(@base);
        if (@base.Operator is not { } op)
        {
            return (0, 0);
        }

        return op.LargeOp ? (0, @base.ItalicCorrection) : (@base.ItalicCorrection, 0);
    }

    /// <summary>
    ///     Computes the shift down of a subscript (MathML Core 3.4.1.2).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="sub">The box of the subscript.</param>
    /// <param name="size">The font size of the scripted element.</param>
    /// <returns>SubShift.</returns>
    private double SubShift(MathBox @base, MathBox sub, double size) =>
        Math.Max(
            Math.Max(Constant(MathConstant.SubscriptShiftDown, size), sub.Ascent - Constant(MathConstant.SubscriptTopMax, size)),
            Constant(MathConstant.SubscriptBaselineDropMin, size) + @base.Descent);

    /// <summary>
    ///     Computes the shift up of a superscript (MathML Core 3.4.1.3).
    /// </summary>
    /// <param name="base">The box of the base.</param>
    /// <param name="sup">The box of the superscript.</param>
    /// <param name="style">The computed style of the scripted element.</param>
    /// <returns>SuperShift.</returns>
    private double SuperShift(MathBox @base, MathBox sup, MathStyle style)
    {
        var size = style.FontSize;
        var standard = Constant(style.MathShiftCompact ? MathConstant.SuperscriptShiftUpCramped : MathConstant.SuperscriptShiftUp, size);
        return Math.Max(
            Math.Max(standard, Constant(MathConstant.SuperscriptBottomMin, size) + sup.Descent),
            @base.Ascent - Constant(MathConstant.SuperscriptBaselineDropMax, size));
    }

    /// <summary>
    ///     Enforces the minimum gap between a subscript and a superscript (MathML Core 3.4.1.4): the superscript is raised
    ///     first, while its bottom stays below SuperscriptBottomMaxWithSubscript, then the subscript is lowered.
    /// </summary>
    /// <param name="sub">The box of the subscript.</param>
    /// <param name="sup">The box of the superscript.</param>
    /// <param name="subShift">SubShift.</param>
    /// <param name="supShift">SuperShift.</param>
    /// <param name="size">The font size of the scripted element.</param>
    /// <returns>The adjusted shifts.</returns>
    private (double SubShift, double SupShift) EnforceGap(MathBox sub, MathBox sup, double subShift, double supShift, double size)
    {
        var gapMin = Constant(MathConstant.SubSuperscriptGapMin, size);
        var gap = (subShift - sub.Ascent) + (supShift - sup.Descent);
        if (gap >= gapMin)
        {
            return (subShift, supShift);
        }

        var delta = Constant(MathConstant.SuperscriptBottomMaxWithSubscript, size) - (supShift - sup.Descent);
        if (delta > 0)
        {
            delta = Math.Min(delta, gapMin - gap);
            supShift += delta;
            gap += delta;
        }

        delta = gapMin - gap;
        if (delta > 0)
        {
            subShift += delta;
        }

        return (subShift, supShift);
    }

    /// <summary>
    ///     Extends an ascent and a descent by a pair of scripts at their shifts.
    /// </summary>
    /// <param name="ascent">The ascent so far.</param>
    /// <param name="descent">The descent so far.</param>
    /// <param name="pair">The pair.</param>
    /// <param name="subShift">SubShift.</param>
    /// <param name="supShift">SuperShift.</param>
    /// <returns>The extended ascent and descent.</returns>
    private static (double Ascent, double Descent) Extend(double ascent, double descent, ScriptPair pair, double subShift, double supShift) =>
        (Math.Max(ascent, Math.Max(pair.Sub.Ascent - subShift, pair.Sup.Ascent + supShift)),
         Math.Max(descent, Math.Max(pair.Sub.Descent + subShift, pair.Sup.Descent - supShift)));

    /// <summary>
    ///     Gets a length constant at a font size.
    /// </summary>
    /// <param name="constant">The constant.</param>
    /// <param name="fontSize">The font size in CSS pixels.</param>
    /// <returns>The scaled value.</returns>
    private double Constant(MathConstant constant, double fontSize) => _font.Constant(constant, fontSize);
}
