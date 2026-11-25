using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     A rectangle of the layout in CSS pixels, relative to the origin of a box (its left edge on its baseline), with y
///     growing downward.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width; not negative.</param>
/// <param name="Height">The height; not negative.</param>
internal readonly record struct MathRect(double X, double Y, double Width, double Height)
{
    /// <summary>Gets the right edge.</summary>
    public double Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    public double Bottom => Y + Height;

    /// <summary>
    ///     Gets the smallest rectangle that holds two rectangles.
    /// </summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union.</returns>
    public MathRect Union(MathRect other)
    {
        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        return new MathRect(left, top, Math.Max(Right, other.Right) - left, Math.Max(Bottom, other.Bottom) - top);
    }

    /// <summary>
    ///     Gets the rectangle moved by an offset.
    /// </summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset (down).</param>
    /// <returns>The moved rectangle.</returns>
    public MathRect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}

/// <summary>
///     Something a box paints, positioned relative to the origin of its box.
/// </summary>
internal interface IPaintItem
{
    /// <summary>
    ///     Gets the bounds of what the item paints, relative to the origin of its box, or null when it paints nothing (a
    ///     glyph without contours).
    /// </summary>
    MathRect? Bounds { get; }
}

/// <summary>
///     A glyph painted in a color.
/// </summary>
/// <param name="Font">The font of the glyph (the math font or a fallback font).</param>
/// <param name="Glyph">The glyph index.</param>
/// <param name="Size">The font size in CSS pixels.</param>
/// <param name="X">The pen position of the glyph origin.</param>
/// <param name="Baseline">The baseline of the glyph (y, down).</param>
/// <param name="Color">The color.</param>
/// <param name="Ink">The ink bounds of the glyph at this position, empty for a glyph without contours.</param>
internal sealed record GlyphItem(TrueTypeFont Font, int Glyph, double Size, double X, double Baseline, Rgba32 Color, MathRect Ink) : IPaintItem
{
    /// <inheritdoc/>
    public MathRect? Bounds => Ink.Width > 0 || Ink.Height > 0 ? Ink : null;
}

/// <summary>
///     A filled rectangle that belongs to the content (a stretched line character, a fraction bar, or the overbar of a
///     radical).
/// </summary>
/// <param name="Rect">The rectangle.</param>
/// <param name="Color">The color.</param>
internal sealed record RuleItem(MathRect Rect, Rgba32 Color) : IPaintItem
{
    /// <inheritdoc/>
    public MathRect? Bounds => Rect;
}

/// <summary>
///     A background rectangle (<c>mathbackground</c>, <c>background-color</c>, the light yellow of <c>merror</c>),
///     painted before every glyph.
/// </summary>
/// <param name="Rect">The rectangle.</param>
/// <param name="Color">The color.</param>
internal sealed record BackgroundItem(MathRect Rect, Rgba32 Color) : IPaintItem
{
    /// <inheritdoc/>
    public MathRect? Bounds => Rect;
}

/// <summary>
///     A rectangular border (the red border of <c>merror</c>), painted after every glyph.
/// </summary>
/// <param name="Rect">The outer rectangle of the border.</param>
/// <param name="Width">The width of the border lines.</param>
/// <param name="Color">The color.</param>
internal sealed record BorderItem(MathRect Rect, double Width, Rgba32 Color) : IPaintItem
{
    /// <inheritdoc/>
    public MathRect? Bounds => Rect;
}

/// <summary>
///     The properties of an embellished operator (MathML Core 3.2.4.2), resolved from the operator dictionary and the
///     attributes of its core <c>mo</c>: what a row adds before and after it and how it stretches.
/// </summary>
/// <remarks>
///     A row reads <see cref="LeadingSpace"/> and <see cref="TrailingSpace"/> for its spacing and <see cref="Stretchy"/>
///     with <see cref="Axis"/> to decide whether to stretch the operator; the under- and overscript layouts read
///     <see cref="LargeOp"/>, <see cref="Stretchy"/> with <see cref="Axis"/> and <see cref="MovableLimits"/>; the other
///     properties are kept for the tests.
/// </remarks>
/// <param name="LeadingSpace">The space before the operator (<c>lspace</c>) in CSS pixels.</param>
/// <param name="TrailingSpace">The space after the operator (<c>rspace</c>) in CSS pixels.</param>
internal sealed record EmbellishedOperator(double LeadingSpace, double TrailingSpace)
{
    /// <summary>Gets the text of the core operator.</summary>
    public string CoreText { get; init; } = string.Empty;

    /// <summary>Gets the form (explicit, or inferred from the position in the row).</summary>
    public OperatorForm Form { get; init; }

    /// <summary>Gets the category of the operator dictionary that gave the defaults.</summary>
    public OperatorCategory Category { get; init; }

    /// <summary>Gets a value indicating whether the operator stretches.</summary>
    public bool Stretchy { get; init; }

    /// <summary>Gets a value indicating whether the operator stretches symmetrically about the math axis.</summary>
    public bool Symmetric { get; init; }

    /// <summary>Gets a value indicating whether the operator is drawn larger in display style.</summary>
    public bool LargeOp { get; init; }

    /// <summary>Gets a value indicating whether the under- and overscripts of the operator move to the script positions outside display style.</summary>
    public bool MovableLimits { get; init; }

    /// <summary>Gets a value indicating whether the operator is a fence (no effect on the layout).</summary>
    public bool Fence { get; init; }

    /// <summary>Gets a value indicating whether the operator is a separator (no effect on the layout).</summary>
    public bool Separator { get; init; }

    /// <summary>Gets the stretch axis.</summary>
    public StretchAxis Axis { get; init; }

    /// <summary>Gets the minimum stretched size in CSS pixels.</summary>
    public double MinSize { get; init; }

    /// <summary>Gets the maximum stretched size in CSS pixels; infinity when there is none.</summary>
    public double MaxSize { get; init; } = double.PositiveInfinity;
}

/// <summary>
///     A block stretch size constraint (MathML Core 3.3.1.1): the ascent and the descent that a stretchy operator of a row
///     covers.
/// </summary>
/// <param name="Ascent">The target ascent in CSS pixels.</param>
/// <param name="Descent">The target descent in CSS pixels.</param>
internal readonly record struct BlockStretchConstraint(double Ascent, double Descent);

/// <summary>
///     What the parent of an element tells the layout of an embellished operator: its form and its stretch constraint.
/// </summary>
/// <param name="Form">The form inferred from the position of the embellished operator (in its row, or as a script), or null for infix.</param>
/// <param name="Stretch">The block stretch size constraint, or null when the operator is laid out without one.</param>
/// <param name="InlineStretch">
///     The inline stretch size constraint in CSS pixels (MathML Core 3.4.2.2: the width of the widest child of an
///     under- or overscript element that does not stretch), or null when the operator is laid out without one.
/// </param>
internal readonly record struct OperatorContext(OperatorForm? Form, BlockStretchConstraint? Stretch, double? InlineStretch = null)
{
    /// <summary>Gets a value indicating whether the context carries a stretch size constraint along either axis.</summary>
    public bool HasStretch => Stretch is not null || InlineStretch is not null;
}

/// <summary>
///     A child box placed inside its parent.
/// </summary>
/// <param name="Box">The child box.</param>
/// <param name="X">The position of the child origin from the parent origin.</param>
/// <param name="Y">The position of the child baseline from the parent baseline (down).</param>
internal readonly record struct PlacedBox(MathBox Box, double X, double Y);

/// <summary>
///     The laid-out box of an element: its metrics, its children placed relative to its origin, and what it paints.
/// </summary>
/// <remarks>
///     <para>
///         The origin of a box is the left edge of its content box on its alphabetic baseline; lengths are CSS pixels at
///         the font size of the call, y growing downward. The content box spans <c>[0, Width]</c> horizontally and
///         <c>[-Ascent, Descent]</c> vertically (MathML Core: inline size, line-ascent and line-descent). Ink may overhang
///         the content box (an italic glyph); <see cref="InkBounds"/> covers both, and the picture is their union.
///     </para>
///     <para>
///         Boxes are built bottom-up by the layout and never changed afterwards, so a finished box tree is safe for
///         concurrent reads.
///     </para>
/// </remarks>
internal sealed class MathBox
{
    /// <summary>
    ///     Initializes a box.
    /// </summary>
    /// <param name="width">The inline size.</param>
    /// <param name="ascent">The line-ascent (above the baseline).</param>
    /// <param name="descent">The line-descent (below the baseline).</param>
    /// <param name="children">The placed children.</param>
    /// <param name="items">The paint items.</param>
    public MathBox(double width, double ascent, double descent, IReadOnlyList<PlacedBox>? children = null, IReadOnlyList<IPaintItem>? items = null)
    {
        Width = width;
        Ascent = ascent;
        Descent = descent;
        Children = children ?? [];
        Items = items ?? [];
    }

    /// <summary>Gets the inline size in CSS pixels.</summary>
    public double Width { get; }

    /// <summary>Gets the line-ascent in CSS pixels (the extent above the baseline).</summary>
    public double Ascent { get; }

    /// <summary>Gets the line-descent in CSS pixels (the extent below the baseline).</summary>
    public double Descent { get; }

    /// <summary>Gets the placed children.</summary>
    public IReadOnlyList<PlacedBox> Children { get; }

    /// <summary>Gets the paint items of the box itself (not of its children).</summary>
    public IReadOnlyList<IPaintItem> Items { get; }

    /// <summary>Gets the italic correction in CSS pixels (MathML Core: of a single slanted glyph, or of the last child of a row; the scripts read it only from a large or stretched operator, as Chrome).</summary>
    public double ItalicCorrection { get; init; }

    /// <summary>Gets the top accent attachment in CSS pixels from the left edge, or null when the box has none.</summary>
    public double? TopAccentAttachment { get; init; }

    /// <summary>Gets the embellished-operator data when the box is an embellished operator, or null.</summary>
    public EmbellishedOperator? Operator { get; init; }

    /// <summary>Gets a value indicating whether the box is space-like (MathML Core 3.2.5.1: <c>mtext</c>, <c>mspace</c>, and groups of them).</summary>
    public bool IsSpaceLike { get; init; }

    /// <summary>Gets the element the box was laid out for, or null for an anonymous box.</summary>
    public MathElement? Element { get; init; }

    /// <summary>Gets a value indicating whether the box and its subtree are invisible (<c>mphantom</c>): laid out, never painted.</summary>
    public bool IsPhantom { get; init; }

    /// <summary>Gets the content box relative to the origin.</summary>
    public MathRect ContentRect => new(0, -Ascent, Math.Max(0, Width), Math.Max(0, Ascent + Descent));

    /// <summary>
    ///     Computes the bounds of everything the box paints (its own items and its children's, phantoms excluded),
    ///     relative to its origin.
    /// </summary>
    /// <returns>The bounds, or null when the box paints nothing.</returns>
    public MathRect? InkBounds()
    {
        if (IsPhantom)
        {
            return null;
        }

        MathRect? bounds = null;
        foreach (var item in Items)
        {
            bounds = Union(bounds, item.Bounds);
        }

        foreach (var child in Children)
        {
            bounds = Union(bounds, child.Box.InkBounds()?.Offset(child.X, child.Y));
        }

        return bounds;
    }

    /// <summary>
    ///     Unions two optional rectangles.
    /// </summary>
    /// <param name="a">The first rectangle, or null.</param>
    /// <param name="b">The second rectangle, or null.</param>
    /// <returns>The union, or null when both are null.</returns>
    private static MathRect? Union(MathRect? a, MathRect? b)
    {
        if (a is null)
        {
            return b;
        }

        return b is null ? a : a.Value.Union(b.Value);
    }
}
