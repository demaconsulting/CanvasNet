// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
// cspell:ignore neue ordinally scenegraph steelblue
using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     A point of a <see cref="ScenePath"/>.
/// </summary>
/// <param name="X">The x coordinate in the coordinate system of the enclosing group.</param>
/// <param name="Y">The y coordinate in the coordinate system of the enclosing group.</param>
internal readonly record struct ScenePoint(double X, double Y);

/// <summary>
///     A rectangular clip of a <see cref="SceneGroup"/>, in the coordinate system of the group.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width; a clip that is not wider than zero hides everything.</param>
/// <param name="Height">The height; a clip that is not taller than zero hides everything.</param>
internal readonly record struct SceneClip(double X, double Y, double Width, double Height);

/// <summary>
///     An item that a scene visit found, with the position of its enclosing coordinate system.
/// </summary>
/// <param name="Item">The item.</param>
/// <param name="OffsetX">The x translation that maps the coordinate system of the item's group to the scene.</param>
/// <param name="OffsetY">The y translation that maps the coordinate system of the item's group to the scene.</param>
/// <param name="Depth">The number of groups above the item (the root group has depth 0).</param>
internal readonly record struct SceneEntry(SceneItem Item, double OffsetX, double OffsetY, int Depth);

/// <summary>
///     One element of a scene: the common part of groups, rectangles, rules, paths, symbols and text.
/// </summary>
/// <remarks>
///     <para>
///         <b>Immutability.</b> Items are immutable records; a changed copy is made with a <c>with</c> expression.
///         List-valued members are copied when they are set and cannot be changed through the property afterwards.
///         Record equality compares lists by reference, so tests compare members, not whole items.
///     </para>
///     <para>
///         <b>Paints.</b> Colors are CSS color strings exactly as the specification and the configuration spell them
///         (<c>#4c78a8</c>, <c>rgba(0,0,0,0.5)</c>, <c>steelblue</c>); <see langword="null"/> (or <c>none</c>) means "no
///         paint". They are parsed by <see cref="CssColor"/> when the scene is painted, so a test can assert the exact
///         string a mark received. Opacities multiply: the fill is drawn with <c>FillOpacity * Opacity</c> and the stroke
///         with <c>StrokeOpacity * Opacity</c>. A stroke without a <see cref="StrokeWidth"/> is one pixel wide, as in Vega.
///     </para>
///     <para>
///         <b>Order and roles.</b> The children of a group are drawn in list order (later items cover earlier ones), and
///         the scene builders add items in a fixed order, so two runs produce the same list. Every item has a
///         <see cref="Role"/>, a stable lower-case name such as <c>view</c>, <c>title-text</c>, <c>axis-label</c>
///         or <c>mark-bar</c>, which is how tests and later stages find items.
///     </para>
/// </remarks>
internal abstract record SceneItem
{
    /// <summary>The backing field of <see cref="StrokeDash"/>.</summary>
    private readonly IReadOnlyList<double>? _strokeDash;

    /// <summary>
    ///     Initializes an item.
    /// </summary>
    /// <param name="role">The role of the item; must not be null, empty or white space.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="role"/> is null, empty or white space.</exception>
    protected SceneItem(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        Role = role;
    }

    /// <summary>Gets the role of the item, for example <c>mark-bar</c>.</summary>
    public string Role { get; }

    /// <summary>Gets the fill paint as a CSS color, or <see langword="null"/> for no fill.</summary>
    public string? Fill { get; init; }

    /// <summary>Gets the stroke paint as a CSS color, or <see langword="null"/> for no stroke.</summary>
    public string? Stroke { get; init; }

    /// <summary>Gets the stroke width in pixels, or <see langword="null"/> for the default of 1.</summary>
    public double? StrokeWidth { get; init; }

    /// <summary>Gets the opacity of the whole item, from 0 to 1; 1 by default.</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>Gets the opacity of the fill, from 0 to 1; 1 by default.</summary>
    public double FillOpacity { get; init; } = 1;

    /// <summary>Gets the opacity of the stroke, from 0 to 1; 1 by default.</summary>
    public double StrokeOpacity { get; init; } = 1;

    /// <summary>Gets the stroke dash pattern (alternating on and off lengths in pixels), or <see langword="null"/> for a solid stroke.</summary>
    public IReadOnlyList<double>? StrokeDash
    {
        get => _strokeDash;
        init => _strokeDash = value is null ? null : SceneLists.Copy(value);
    }

    /// <summary>Gets the distance into the dash pattern at which the stroke starts; 0 by default.</summary>
    public double StrokeDashOffset { get; init; }

    /// <summary>Gets the line cap of the stroke: <c>butt</c> (default), <c>round</c> or <c>square</c>.</summary>
    public string? StrokeCap { get; init; }

    /// <summary>Gets the line join of the stroke: <c>miter</c> (default), <c>round</c> or <c>bevel</c>.</summary>
    public string? StrokeJoin { get; init; }
}

/// <summary>
///     Copies for the list-valued members of the scene items.
/// </summary>
internal static class SceneLists
{
    /// <summary>
    ///     Copies a sequence into a list that cannot be changed through the reference that is handed out.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The elements.</param>
    /// <returns>A read-only copy that keeps the order of <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    public static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReadOnlyCollection<T>(source.ToList());
    }
}

/// <summary>
///     A group: a coordinate system (everything inside is translated by the group's position) that can also paint its
///     own background and border and clip its children to a rectangle.
/// </summary>
/// <remarks>
///     The group's box is <see cref="Width"/> by <see cref="Height"/> at its origin; with a fill or stroke it is drawn
///     as a (rounded) rectangle behind the children, which is how the plot area of a view is painted. A stroked group
///     is drawn offset by <see cref="StrokeOffset"/> (Vega's rule when <see langword="null"/>: half a pixel for a
///     one-pixel stroke, so that the line is crisp on the pixel grid).
/// </remarks>
internal sealed record SceneGroup : SceneItem
{
    /// <summary>The backing field of <see cref="Children"/>.</summary>
    private readonly IReadOnlyList<SceneItem> _children = [];

    /// <summary>
    ///     Initializes a group.
    /// </summary>
    /// <param name="role">The role of the group.</param>
    /// <param name="x">The x position of the group's origin in its parent.</param>
    /// <param name="y">The y position of the group's origin in its parent.</param>
    public SceneGroup(string role, double x, double y)
        : base(role)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the x position of the group's origin in its parent.</summary>
    public double X { get; }

    /// <summary>Gets the y position of the group's origin in its parent.</summary>
    public double Y { get; }

    /// <summary>Gets the width of the group's box (the background rectangle); 0 by default.</summary>
    public double Width { get; init; }

    /// <summary>Gets the height of the group's box (the background rectangle); 0 by default.</summary>
    public double Height { get; init; }

    /// <summary>Gets the corner radius of the background rectangle and of the clip; 0 by default.</summary>
    public double CornerRadius { get; init; }

    /// <summary>Gets the clip rectangle in the group's coordinates, or <see langword="null"/> when the children are not clipped.</summary>
    public SceneClip? Clip { get; init; }

    /// <summary>Gets the offset of the background rectangle when it is stroked, or <see langword="null"/> for Vega's rule.</summary>
    public double? StrokeOffset { get; init; }

    /// <summary>Gets the children in drawing order (the last one is on top).</summary>
    public IReadOnlyList<SceneItem> Children
    {
        get => _children;
        init => _children = SceneLists.Copy(value);
    }
}

/// <summary>
///     A rectangle, optionally with rounded corners.
/// </summary>
internal sealed record SceneRect : SceneItem
{
    /// <summary>
    ///     Initializes a rectangle.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width; a rectangle that is not wider than zero is not drawn.</param>
    /// <param name="height">The height; a rectangle that is not taller than zero is not drawn.</param>
    public SceneRect(string role, double x, double y, double width, double height)
        : base(role)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the left edge.</summary>
    public double X { get; }

    /// <summary>Gets the top edge.</summary>
    public double Y { get; }

    /// <summary>Gets the width.</summary>
    public double Width { get; }

    /// <summary>Gets the height.</summary>
    public double Height { get; }

    /// <summary>Gets the corner radius (clamped to half the smaller side when drawn); 0 by default.</summary>
    public double CornerRadius { get; init; }
}

/// <summary>
///     A straight line segment (a rule): an axis tick, a grid line, an axis domain or a <c>rule</c> mark.
/// </summary>
/// <remarks>A rule is only visible with a <see cref="SceneItem.Stroke"/>; it has no fill.</remarks>
internal sealed record SceneRule : SceneItem
{
    /// <summary>
    ///     Initializes a rule.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x coordinate of the start.</param>
    /// <param name="y">The y coordinate of the start.</param>
    /// <param name="x2">The x coordinate of the end.</param>
    /// <param name="y2">The y coordinate of the end.</param>
    public SceneRule(string role, double x, double y, double x2, double y2)
        : base(role)
    {
        X = x;
        Y = y;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>Gets the x coordinate of the start.</summary>
    public double X { get; }

    /// <summary>Gets the y coordinate of the start.</summary>
    public double Y { get; }

    /// <summary>Gets the x coordinate of the end.</summary>
    public double X2 { get; }

    /// <summary>Gets the y coordinate of the end.</summary>
    public double Y2 { get; }
}

/// <summary>
///     A polyline or polygon through straight segments: a <c>line</c> mark or an axis domain with several legs.
/// </summary>
internal sealed record ScenePath : SceneItem
{
    /// <summary>The backing field of <see cref="Points"/>.</summary>
    private readonly IReadOnlyList<ScenePoint> _points;

    /// <summary>
    ///     Initializes a path.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="points">The points in drawing order; the list is copied.</param>
    public ScenePath(string role, IEnumerable<ScenePoint> points)
        : base(role)
    {
        _points = SceneLists.Copy(points);
    }

    /// <summary>Gets the points in drawing order.</summary>
    public IReadOnlyList<ScenePoint> Points => _points;

    /// <summary>Gets a value indicating whether the last point is joined to the first (a polygon); <see langword="false"/> by default.</summary>
    public bool Closed { get; init; }
}

/// <summary>
///     A symbol: a small shape of a given pixel area centered at a position (a <c>point</c>, <c>circle</c> or
///     <c>square</c> mark).
/// </summary>
/// <remarks>
///     <see cref="Size"/> is the area of the symbol's bounding box in square pixels, as in Vega: a circle of size 64 has a
///     diameter of 8. Shapes are <c>circle</c>, <c>square</c>, <c>cross</c>, <c>diamond</c>, <c>triangle</c>,
///     <c>triangle-up</c>, <c>triangle-down</c>, <c>triangle-left</c>, <c>triangle-right</c>, <c>arrow</c>,
///     <c>wedge</c> and <c>stroke</c>; any other name is drawn as a circle.
/// </remarks>
internal sealed record SceneSymbol : SceneItem
{
    /// <summary>
    ///     Initializes a symbol.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x coordinate of the center.</param>
    /// <param name="y">The y coordinate of the center.</param>
    public SceneSymbol(string role, double x, double y)
        : base(role)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the x coordinate of the center.</summary>
    public double X { get; }

    /// <summary>Gets the y coordinate of the center.</summary>
    public double Y { get; }

    /// <summary>Gets the shape name; <c>circle</c> by default.</summary>
    public string Shape { get; init; } = "circle";

    /// <summary>Gets the area of the symbol's bounding box in square pixels; 64 by default (Vega's default).</summary>
    public double Size { get; init; } = 64;

    /// <summary>Gets the rotation in degrees (clockwise, as the y axis points down); 0 by default.</summary>
    public double Angle { get; init; }
}

/// <summary>
///     A text run of one or more lines.
/// </summary>
/// <remarks>
///     <para>
///         The position (<see cref="X"/>, <see cref="Y"/>) plus the offsets (<see cref="Dx"/>, <see cref="Dy"/>) is the
///         anchor; <see cref="Align"/> decides how the lines sit horizontally around it and <see cref="Baseline"/> how
///         the first line sits vertically (the offsets of Vega's text mark: <c>top</c> puts the top of the text at the
///         anchor, <c>middle</c> its middle, <c>bottom</c> its bottom, <c>line-top</c> and <c>line-bottom</c> use the
///         line height). <see cref="Angle"/> rotates the whole run clockwise about the position (not the anchor).
///     </para>
///     <para>
///         The lines are stored as given; truncation to <see cref="Limit"/> happens when the text is measured and
///         painted, so the scene keeps the full text. Text needs a <see cref="SceneItem.Fill"/> to be visible.
///     </para>
/// </remarks>
internal sealed record SceneText : SceneItem
{
    /// <summary>The backing field of <see cref="Lines"/>.</summary>
    private readonly IReadOnlyList<string> _lines;

    /// <summary>
    ///     Initializes a text run.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x coordinate of the position.</param>
    /// <param name="y">The y coordinate of the position.</param>
    /// <param name="lines">The lines of text; the list is copied.</param>
    public SceneText(string role, double x, double y, IEnumerable<string> lines)
        : base(role)
    {
        X = x;
        Y = y;
        _lines = SceneLists.Copy(lines);
    }

    /// <summary>Gets the x coordinate of the position.</summary>
    public double X { get; }

    /// <summary>Gets the y coordinate of the position.</summary>
    public double Y { get; }

    /// <summary>Gets the lines of text.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Gets the text with the lines joined by a line feed.</summary>
    public string Text => string.Join('\n', _lines);

    /// <summary>Gets the horizontal offset added to the position before alignment; 0 by default.</summary>
    public double Dx { get; init; }

    /// <summary>Gets the vertical offset added to the position and the baseline offset; 0 by default.</summary>
    public double Dy { get; init; }

    /// <summary>Gets the font family as a CSS list (<c>"Helvetica Neue", sans-serif</c>), or <see langword="null"/> for <c>sans-serif</c>.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the font size in pixels; 11 by default (Vega's default).</summary>
    public double FontSize { get; init; } = 11;

    /// <summary>Gets the font weight (<c>normal</c>, <c>bold</c> or a number; 600 and above is bold), or <see langword="null"/> for normal.</summary>
    public string? FontWeight { get; init; }

    /// <summary>Gets the font style (<c>normal</c> or <c>italic</c>/<c>oblique</c>), or <see langword="null"/> for normal.</summary>
    public string? FontStyle { get; init; }

    /// <summary>Gets the horizontal alignment: <c>left</c> (default), <c>center</c> or <c>right</c>.</summary>
    public string Align { get; init; } = "left";

    /// <summary>Gets the vertical alignment: <c>alphabetic</c> (default), <c>top</c>, <c>middle</c>, <c>bottom</c>, <c>line-top</c> or <c>line-bottom</c>.</summary>
    public string Baseline { get; init; } = "alphabetic";

    /// <summary>Gets the rotation in degrees (clockwise) about the position; 0 by default.</summary>
    public double Angle { get; init; }

    /// <summary>Gets the maximum width of a line in pixels (longer lines are cut and end with the ellipsis), or 0 for no limit.</summary>
    public double Limit { get; init; }

    /// <summary>Gets the distance between the baselines of consecutive lines, or <see langword="null"/> for the font size plus 2.</summary>
    public double? LineHeight { get; init; }

    /// <summary>Gets the text that ends a truncated line; the horizontal ellipsis by default.</summary>
    public string Ellipsis { get; init; } = "\u2026";
}

/// <summary>
///     A complete scene: the canvas size, the background and the root group.
/// </summary>
/// <remarks>
///     <para>
///         The scene's coordinate system is the canvas at a scale of one: <see cref="Width"/> by <see cref="Height"/>
///         pixels, y down. The root group sits at the origin; the view group (the plot area) is its child, translated
///         by the padding and the room the guides need.
///     </para>
///     <para>
///         The enumeration helpers visit items in drawing order (parents before children, siblings in list order), with
///         no recursion, and report the accumulated group translation so that a test can compare absolute positions.
///     </para>
/// </remarks>
internal sealed record Scene
{
    /// <summary>
    ///     Initializes a scene.
    /// </summary>
    /// <param name="width">The canvas width in pixels at a scale of one.</param>
    /// <param name="height">The canvas height in pixels at a scale of one.</param>
    /// <param name="background">The background as a CSS color, or <see langword="null"/> for a transparent canvas.</param>
    /// <param name="root">The root group.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is not finite or is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
    public Scene(double width, double height, string? background, SceneGroup root)
    {
        if (!double.IsFinite(width) || width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The width must be a finite number that is not negative.");
        }

        if (!double.IsFinite(height) || height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The height must be a finite number that is not negative.");
        }

        ArgumentNullException.ThrowIfNull(root);
        Width = width;
        Height = height;
        Background = background;
        Root = root;
    }

    /// <summary>Gets the canvas width in pixels at a scale of one.</summary>
    public double Width { get; }

    /// <summary>Gets the canvas height in pixels at a scale of one.</summary>
    public double Height { get; }

    /// <summary>Gets the background as a CSS color, or <see langword="null"/> for a transparent canvas.</summary>
    public string? Background { get; }

    /// <summary>Gets the root group.</summary>
    public SceneGroup Root { get; }

    /// <summary>
    ///     Enumerates every item of the scene, the root group included, in drawing order.
    /// </summary>
    /// <returns>The entries; the same scene always yields the same sequence.</returns>
    public IEnumerable<SceneEntry> Enumerate() => Enumerate(Root);

    /// <summary>
    ///     Enumerates a group and everything below it in drawing order.
    /// </summary>
    /// <param name="start">The group to start at; its own translation is included in the offsets of its children.</param>
    /// <returns>The entries, starting with <paramref name="start"/> itself.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="start"/> is null.</exception>
    public static IEnumerable<SceneEntry> Enumerate(SceneGroup start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return EnumerateCore(start);
    }

    /// <summary>
    ///     Walks a group and everything below it with an explicit stack.
    /// </summary>
    /// <param name="start">The group to start at.</param>
    /// <returns>The entries in drawing order.</returns>
    private static IEnumerable<SceneEntry> EnumerateCore(SceneGroup start)
    {
        var pending = new Stack<SceneEntry>();
        pending.Push(new SceneEntry(start, 0, 0, 0));
        while (pending.Count > 0)
        {
            var entry = pending.Pop();
            yield return entry;
            if (entry.Item is SceneGroup group)
            {
                var childX = entry.OffsetX + group.X;
                var childY = entry.OffsetY + group.Y;
                for (var i = group.Children.Count - 1; i >= 0; i--)
                {
                    pending.Push(new SceneEntry(group.Children[i], childX, childY, entry.Depth + 1));
                }
            }
        }
    }

    /// <summary>
    ///     Enumerates the items that have a role, in drawing order.
    /// </summary>
    /// <param name="role">The role, compared ordinally.</param>
    /// <returns>The matching entries.</returns>
    public IEnumerable<SceneEntry> FindAll(string role) => Enumerate().Where(e => string.Equals(e.Item.Role, role, StringComparison.Ordinal));

    /// <summary>
    ///     Enumerates the items of a type, in drawing order.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>The matching items.</returns>
    public IEnumerable<T> OfType<T>()
        where T : SceneItem => Enumerate().Select(e => e.Item).OfType<T>();

    /// <summary>
    ///     Finds the first item that has a role.
    /// </summary>
    /// <param name="role">The role, compared ordinally.</param>
    /// <returns>The first entry in drawing order, or <see langword="null"/> when no item has the role.</returns>
    public SceneEntry? Find(string role) => FindAll(role).Select(e => (SceneEntry?)e).FirstOrDefault();

    /// <summary>
    ///     Counts the items of the scene, the root group included.
    /// </summary>
    /// <returns>The number of items.</returns>
    public int CountItems() => Enumerate().Count();
}
