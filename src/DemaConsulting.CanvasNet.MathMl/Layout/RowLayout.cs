using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     Lays out <c>mrow</c> and the elements that are laid out like it (the inferred row of <c>math</c>, <c>mstyle</c>,
///     <c>mphantom</c>, <c>merror</c>, <c>semantics</c>, <c>maction</c>, and every element this version does not draw),
///     and the boxes that <c>mpadded</c> and <c>merror</c> put around a row.
/// </summary>
/// <remarks>
///     <para>
///         <b>Row.</b> As MathML Core 3.3.1.2: the children are placed left to right with their alphabetic baselines
///         aligned; the ascent and the descent are the maximums of the children's, and at least zero, as Chrome (a row of
///         a minus sign reaches down to its baseline). An embellished operator child gets its
///         leading space before it and its trailing space after it, unless the row is itself an embellished operator (it
///         then passes its operator to its parent) and is not <c>math</c>. The italic correction of the last child
///         becomes the italic correction of the row, but it is not added between
///         children: MathML Core 3.3.1.2 adds the italic correction of a slanted child before the next upright one, and
///         Chrome does not, so CanvasNet lays rows out as Chrome does (a documented difference from the text of MathML
///         Core). Before the children are placed, the stretchy embellished operators with a block stretch axis are laid out
///         again with a block stretch size constraint (MathML Core 3.3.1.1, <see cref="Stretch"/>): the largest ascent and
///         descent of the other children, or, when every child stretches, of the operators at their own size. A row that
///         is itself an embellished operator with a constraint from its parent passes that constraint to its operator
///         instead (the layout engine decides).
///     </para>
///     <para>
///         <b>Grouping.</b> A grouping element or <c>mpadded</c> whose children are one embellished operator and any
///         number of space-like elements is itself an embellished operator (MathML Core 3.2.4.1); one whose children are
///         all space-like is space-like (3.2.5.1).
///     </para>
///     <para>
///         <b>mpadded.</b> As MathML Core 3.3.6: the requested width, height and depth replace the inner box's (absent,
///         invalid or percentage values keep the inner box's; negative height and depth are clamped to zero), and the
///         inner row is moved right by <c>lspace</c> (clamped to zero) and up by <c>voffset</c>.
///     </para>
///     <para>
///         <b>merror.</b> The user agent style of MathML Core: a 1px solid red border around the row (the box grows by the
///         border on every side) over a light yellow background.
///     </para>
///     <para>
///         The class holds no state beyond the resolver of the call; an instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed class RowLayout
{
    /// <summary>The width of the border of <c>merror</c> in CSS pixels.</summary>
    public const double ErrorBorderWidth = 1;

    /// <summary>The border color of <c>merror</c> (CSS <c>red</c>).</summary>
    private static readonly Rgba32 ErrorBorderColor = new(255, 0, 0, 255);

    /// <summary>The style resolver of the call, which resolves the length attributes of <c>mpadded</c>.</summary>
    private readonly StyleResolver _styles;

    /// <summary>
    ///     Initializes the row layout of one call.
    /// </summary>
    /// <param name="styles">The style resolver of the call.</param>
    public RowLayout(StyleResolver styles)
    {
        _styles = styles;
    }

    /// <summary>
    ///     Lays out a row of children (MathML Core 3.3.1.2), stretching its stretchy operators first (3.3.1.1).
    /// </summary>
    /// <param name="children">The boxes of the in-flow children, in order, laid out without a stretch constraint.</param>
    /// <param name="isMath">Whether the row is the inferred row of the <c>math</c> element or of <c>msqrt</c>, which is never an embellished operator and always adds operator spacing.</param>
    /// <param name="relayout">
    ///     Lays a child out again with a block stretch size constraint (by its index), or null when the row does not
    ///     stretch its operators (it passes the constraint of its parent to its embellished operator instead).
    /// </param>
    /// <returns>The box of the row.</returns>
    public MathBox LayoutRow(IReadOnlyList<MathBox> children, bool isMath, Func<int, BlockStretchConstraint, MathBox>? relayout = null)
    {
        if (relayout is not null)
        {
            children = Stretch(children, relayout);
        }

        // The row is an embellished operator when its children are one operator and space-like elements
        var embellished = EmbellishedOperatorOf(children);
        var addSpace = isMath || embellished is null;

        // Place the children on the shared baseline (MathML Core 3.3.1.2, without the italic corrections, as Chrome)
        var placed = new List<PlacedBox>(children.Count);
        var offset = 0.0;
        var ascent = double.NegativeInfinity;
        var descent = double.NegativeInfinity;
        foreach (var child in children)
        {
            if (child.Operator is { } leading && addSpace)
            {
                offset += leading.LeadingSpace;
            }

            placed.Add(new PlacedBox(child, offset, 0));
            offset += child.Width;
            if (child.Operator is { } trailing && addSpace)
            {
                offset += trailing.TrailingSpace;
            }

            ascent = Math.Max(ascent, child.Ascent);
            descent = Math.Max(descent, child.Descent);
        }

        // The italic correction of a slanted last child (not an operator) is the row's
        var last = children.Count == 0 ? null : children[^1];
        return new MathBox(offset, Math.Max(0, ascent), Math.Max(0, descent), placed)
        {
            ItalicCorrection = last is { Operator: null } ? last.ItalicCorrection : 0,
            Operator = isMath ? null : embellished,
            IsSpaceLike = children.All(child => child.IsSpaceLike),
        };
    }

    /// <summary>
    ///     Stretches the stretchy operators of a row along the block axis (MathML Core 3.3.1.1): the target is the largest
    ///     ascent and descent of the other children, or, when every child stretches, of the operators laid out with the
    ///     empty constraint (0, 0).
    /// </summary>
    /// <param name="children">The boxes of the children, laid out without a constraint.</param>
    /// <param name="relayout">Lays a child out again with a constraint.</param>
    /// <returns>The boxes with the stretchy operators laid out again.</returns>
    public static IReadOnlyList<MathBox> Stretch(IReadOnlyList<MathBox> children, Func<int, BlockStretchConstraint, MathBox> relayout)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(relayout);
        var toStretch = Enumerable.Range(0, children.Count).Where(i => children[i].Operator is { Stretchy: true, Axis: StretchAxis.Block }).ToList();
        if (toStretch.Count == 0)
        {
            return children;
        }

        // The children that do not stretch give the target; when there are none, the operators at their own size do
        var boxes = children.ToArray();
        var measured = Enumerable.Range(0, boxes.Length).Except(toStretch).ToList();
        if (measured.Count == 0)
        {
            foreach (var i in toStretch)
            {
                boxes[i] = relayout(i, new BlockStretchConstraint(0, 0));
            }

            measured = toStretch;
        }

        var constraint = new BlockStretchConstraint(measured.Max(i => boxes[i].Ascent), measured.Max(i => boxes[i].Descent));
        foreach (var i in toStretch)
        {
            boxes[i] = relayout(i, constraint);
        }

        return boxes;
    }

    /// <summary>
    ///     Lays out an <c>mpadded</c> around its inner row (MathML Core 3.3.6).
    /// </summary>
    /// <param name="element">The <c>mpadded</c> element.</param>
    /// <param name="style">Its computed style.</param>
    /// <param name="inner">The box of the inner row.</param>
    /// <returns>The box.</returns>
    public MathBox LayoutPadded(MathElement element, MathStyle style, MathBox inner)
    {
        // The requested parameters; percentages are ignored, as MathML Core ignores them
        var width = _styles.ResolveLength(element, "width", style, null);
        if (width < 0)
        {
            // A negative width is invalid CSS
            width = null;
        }

        var height = _styles.ResolveLength(element, "height", style, null) is { } h ? Math.Max(0, h) : inner.Ascent;
        var depth = _styles.ResolveLength(element, "depth", style, null) is { } d ? Math.Max(0, d) : inner.Descent;
        var leftSpace = Math.Max(0, _styles.ResolveLength(element, "lspace", style, null) ?? 0);
        var verticalOffset = _styles.ResolveLength(element, "voffset", style, null) ?? 0;

        // The inner box moves right by lspace and up by voffset
        return new MathBox(width ?? inner.Width, height, depth, [new PlacedBox(inner, leftSpace, -verticalOffset)])
        {
            ItalicCorrection = inner.ItalicCorrection,
            Operator = inner.Operator,
            IsSpaceLike = inner.IsSpaceLike,
        };
    }

    /// <summary>
    ///     Puts the border of <c>merror</c> around a row: the box grows by the border width on every side.
    /// </summary>
    /// <param name="inner">The box of the row.</param>
    /// <returns>The box with its border.</returns>
    public static MathBox LayoutError(MathBox inner)
    {
        var width = inner.Width + (2 * ErrorBorderWidth);
        var ascent = inner.Ascent + ErrorBorderWidth;
        var descent = inner.Descent + ErrorBorderWidth;
        var border = new BorderItem(new MathRect(0, -ascent, width, ascent + descent), ErrorBorderWidth, ErrorBorderColor);
        return new MathBox(width, ascent, descent, [new PlacedBox(inner, ErrorBorderWidth, 0)], [border])
        {
            Operator = inner.Operator,
            IsSpaceLike = inner.IsSpaceLike,
        };
    }

    /// <summary>
    ///     Finds the operator of a grouping element: the operator of its only embellished-operator child when every other
    ///     child is space-like (MathML Core 3.2.4.1).
    /// </summary>
    /// <param name="children">The boxes of the in-flow children.</param>
    /// <returns>The operator data, or null when the group is not an embellished operator.</returns>
    private static EmbellishedOperator? EmbellishedOperatorOf(IReadOnlyList<MathBox> children)
    {
        EmbellishedOperator? found = null;
        foreach (var child in children)
        {
            if (child.Operator is { } candidate && found is null)
            {
                found = candidate;
            }
            else if (!child.IsSpaceLike)
            {
                return null;
            }
        }

        return found;
    }
}
