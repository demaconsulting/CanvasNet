// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     A place in the paint order of a sequence drawing (an SVG group Mermaid appends or lowers), filled with items
///     when they are drawn, which may be later than the place is made.
/// </summary>
internal sealed class SceneSlot
{
    /// <summary>Gets the items of the place, in paint order.</summary>
    public List<SceneItem> Items { get; } = [];
}

/// <summary>
///     The sequence renderer: a port of Mermaid's sequence drawing functions (<c>svgDraw.js</c>) that turns the laid-out
///     participants, messages, notes and activations into scene items, with the paint of Mermaid's style sheet
///     (<see cref="SequenceStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Paint order.</b> Mermaid appends most elements to the diagram, but lowers some groups to the back (the
///         participant boxes with their lifelines, the lifelines of the figures, then the <c>rect</c> backgrounds and the
///         boxes). The renderer keeps both lists, so
///         the scene has Mermaid's paint order: the lowered places from the last lowered to the first, then the appended
///         ones in order. An activation is drawn into the group Mermaid made for it when it started.
///     </para>
///     <para>
///         <b>Roles.</b> The items carry roles and ids for tests and the reference comparison: <c>actor-top</c> and
///         <c>actor-bottom</c> groups (the participant's name), <c>lifeline</c> paths, <c>message</c> groups with the
///         <c>message-line</c> and its <c>marker-start</c> and <c>marker-end</c> (the marker name as id),
///         <c>message-text</c>, <c>connection-circles</c> and <c>sequence-number</c> groups (Mermaid's <c>i</c> and
///         message index), <c>note</c> groups, <c>activation</c> rectangles (the activation class as id), <c>frame</c>
///         groups of blocks (the id of the message that ends the block) with <c>frame-line</c>, <c>frame-label-box</c>,
///         <c>frame-label</c>, <c>frame-title</c> and <c>frame-section-title</c> items, <c>rect</c> backgrounds,
///         <c>box</c> groups with a <c>box-rect</c> and a <c>box-title</c>, <c>menu</c> groups (Mermaid's
///         <c>actor{n}_popup</c> id, empty while the menu is hidden) with a <c>menu-panel</c> and <c>menu-text</c> lines,
///         and the <c>title</c>.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed partial class SequenceRenderer
{
    /// <summary>The fill of an actor rectangle's attribute (Mermaid's <c>#eaeaea</c>).</summary>
    private static readonly Rgba32 ActorRectFill = CssColor.Parse("#eaeaea");

    /// <summary>The fill of a note rectangle's attribute (<c>getNoteRect</c>'s <c>#EDF2AE</c>).</summary>
    private static readonly Rgba32 NoteRectFill = CssColor.Parse("#EDF2AE");

    /// <summary>The stroke of a note rectangle's attribute (<c>getNoteRect</c>'s <c>#666</c>).</summary>
    private static readonly Rgba32 NoteRectStroke = CssColor.Parse("#666");

    /// <summary>Black, the default of SVG paint.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The sequence configuration of the render.</summary>
    private readonly SequenceConfig _conf;

    /// <summary>The style sheet of the render.</summary>
    private readonly SequenceStyles _styles;

    /// <summary>The label builder of the render (text metrics).</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The effective configuration of the render (theme variables).</summary>
    private readonly MermaidConfig _config;

    /// <summary>The lowered places, in the order they were lowered.</summary>
    private readonly List<SceneSlot> _lowered = [];

    /// <summary>The appended places, in order.</summary>
    private readonly List<SceneSlot> _appended = [];

    /// <summary>The lifelines, by participant id, completed when their heights are fixed.</summary>
    private readonly Dictionary<string, Lifeline> _lifelines = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a renderer.
    /// </summary>
    /// <param name="conf">The sequence configuration of the render.</param>
    /// <param name="styles">The style sheet of the render.</param>
    /// <param name="labels">The label builder of the render.</param>
    /// <param name="config">The effective configuration of the render.</param>
    public SequenceRenderer(SequenceConfig conf, SequenceStyles styles, LabelBuilder labels, MermaidConfig config)
    {
        _conf = conf;
        _styles = styles;
        _labels = labels;
        _config = config;
    }

    /// <summary>
    ///     Gets the fill the root rule gives the diagram (the theme's <c>textColor</c>), which elements without a fill of
    ///     their own inherit.
    /// </summary>
    private Rgba32 RootFill => _styles.Resolve(DiagramStyles.Root).Paint("fill") ?? Black;

    /// <summary>
    ///     Makes a place at the end of the paint order (an appended group).
    /// </summary>
    /// <returns>The place.</returns>
    public SceneSlot Append()
    {
        var slot = new SceneSlot();
        _appended.Add(slot);
        return slot;
    }

    /// <summary>
    ///     Makes a place at the start of the paint order (a lowered group).
    /// </summary>
    /// <returns>The place.</returns>
    public SceneSlot Lower()
    {
        var slot = new SceneSlot();
        _lowered.Add(slot);
        return slot;
    }

    /// <summary>
    ///     Makes the group an activation is drawn into later (Mermaid's <c>anchorElement</c>).
    /// </summary>
    /// <returns>The place.</returns>
    public SceneSlot AnchorElement() => Append();

    /// <summary>
    ///     Sets where the lifelines end (Mermaid's <c>fixLifeLineHeights</c>): at the mirrored participant, or, without
    ///     mirrored participants, half a participant below where a destroyed participant ends (else at y 2000).
    /// </summary>
    /// <param name="actors">The participants drawn.</param>
    public void FixLifeLineHeights(IEnumerable<SequenceActorLayout> actors)
    {
        foreach (var actor in actors)
        {
            if (!_lifelines.TryGetValue(actor.Actor.Id, out var lifeline))
            {
                continue;
            }

            if (!_conf.MirrorActors && actor.StopY is { } stopY && stopY != 0 && !double.IsNaN(stopY))
            {
                lifeline.Y2 = stopY + (actor.Height / 2);
            }
            else if (_conf.MirrorActors)
            {
                lifeline.Y2 = actor.StopY ?? double.NaN;
            }
        }
    }

    /// <summary>
    ///     Draws the diagram title (the text Mermaid appends at the end of <c>draw</c>, in the root font).
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="x">The x of its start.</param>
    /// <param name="y">The y of its baseline.</param>
    public void DrawTitle(string title, double x, double y)
    {
        var root = _styles.Resolve(DiagramStyles.Root);
        var font = new SceneFont(root["font-family"] ?? MermaidConfigResolver.DefaultFontFamily, root.Length("font-size", 16), false, false);
        Append().Items.Add(TextLine(DisplayText(title), x, y, SceneTextAnchor.Start, font, RootFill, "title", null, default));
    }

    /// <summary>
    ///     Gets the scene items in paint order: the lowered places from the last lowered, then the appended ones, with
    ///     the lifelines in their places.
    /// </summary>
    /// <returns>The items.</returns>
    public IReadOnlyList<SceneItem> Items()
    {
        var paint = _styles.Lifeline;
        foreach (var lifeline in _lifelines.Values)
        {
            lifeline.Slot.Items.Insert(0, new ScenePath([SceneSegment.Move(lifeline.X, lifeline.Y1), SceneSegment.Line(lifeline.X, lifeline.Y2)])
            {
                Role = "lifeline",
                Id = lifeline.Name,
                Location = lifeline.Location,
                Stroke = Stroke(paint),
            });
        }

        _lifelines.Clear();
        var items = new List<SceneItem>();
        for (var i = _lowered.Count - 1; i >= 0; i--)
        {
            items.AddRange(_lowered[i].Items);
        }

        foreach (var slot in _appended)
        {
            items.AddRange(slot.Items);
        }

        return items;
    }

    /// <summary>
    ///     Gets the segments of an SVG rectangle, with its corners rounded by radii as SVG clamps them.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="rx">The horizontal corner radius.</param>
    /// <param name="ry">The vertical corner radius.</param>
    /// <returns>The segments.</returns>
    internal static IReadOnlyList<SceneSegment> RectSegments(double x, double y, double width, double height, double rx = 0, double ry = 0)
    {
        rx = Math.Min(Math.Max(rx, 0), width / 2);
        ry = Math.Min(Math.Max(ry, 0), height / 2);
        if (rx <= 0 || ry <= 0)
        {
            return
            [
                SceneSegment.Move(x, y), SceneSegment.Line(x + width, y), SceneSegment.Line(x + width, y + height),
                SceneSegment.Line(x, y + height), SceneSegment.Close(),
            ];
        }

        return
        [
            SceneSegment.Move(x + rx, y),
            SceneSegment.Line(x + width - rx, y),
            SceneSegment.Arc(rx, ry, 0, false, true, x + width, y + ry),
            SceneSegment.Line(x + width, y + height - ry),
            SceneSegment.Arc(rx, ry, 0, false, true, x + width - rx, y + height),
            SceneSegment.Line(x + rx, y + height),
            SceneSegment.Arc(rx, ry, 0, false, true, x, y + height - ry),
            SceneSegment.Line(x, y + ry),
            SceneSegment.Arc(rx, ry, 0, false, true, x + rx, y),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Gets the segments of a circle, as four quarter arcs starting at its right end.
    /// </summary>
    /// <param name="cx">The center's x.</param>
    /// <param name="cy">The center's y.</param>
    /// <param name="r">The radius.</param>
    /// <returns>The segments.</returns>
    internal static IReadOnlyList<SceneSegment> CircleSegments(double cx, double cy, double r) =>
    [
        SceneSegment.Move(cx + r, cy),
        SceneSegment.Arc(r, r, 0, false, true, cx, cy + r),
        SceneSegment.Arc(r, r, 0, false, true, cx - r, cy),
        SceneSegment.Arc(r, r, 0, false, true, cx, cy - r),
        SceneSegment.Arc(r, r, 0, false, true, cx + r, cy),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Gets the segments of a line.
    /// </summary>
    /// <param name="x1">The start's x.</param>
    /// <param name="y1">The start's y.</param>
    /// <param name="x2">The end's x.</param>
    /// <param name="y2">The end's y.</param>
    /// <returns>The segments.</returns>
    internal static IReadOnlyList<SceneSegment> LineSegments(double x1, double y1, double x2, double y2) =>
        [SceneSegment.Move(x1, y1), SceneSegment.Line(x2, y2)];

    /// <summary>
    ///     Gets the scene stroke of a resolved line stroke.
    /// </summary>
    /// <param name="paint">The stroke.</param>
    /// <returns>The scene stroke, or null when it draws nothing.</returns>
    private static SceneStroke? Stroke(StrokePaint paint)
    {
        if (paint.Color is not { } color || paint.Width <= 0)
        {
            return null;
        }

        return new SceneStroke(color, paint.Width) { Dash = paint.Dash.Count > 0 ? paint.Dash : null };
    }

    /// <summary>
    ///     Gets the scene stroke of a resolved shape paint.
    /// </summary>
    /// <param name="paint">The shape paint.</param>
    /// <returns>The scene stroke, or null when it draws nothing.</returns>
    private static SceneStroke? Stroke(ShapePaint paint) =>
        paint.Stroke is { } color && paint.StrokeWidth > 0 ? new SceneStroke(color, paint.StrokeWidth) : null;

    /// <summary>
    ///     Makes a filled and stroked rectangle (Mermaid's <c>drawRect</c>) with the paint of its attributes under the
    ///     style sheet's rules.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The corner radius.</param>
    /// <param name="paint">The resolved paint.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The path.</returns>
    private static ScenePath Rect(double x, double y, double width, double height, double radius, ShapePaint paint, string role, string? id, MermaidLocation location) =>
        new(RectSegments(x, y, width, height, radius, radius))
        {
            Role = role,
            Id = id,
            Location = location,
            Fill = paint.Fill,
            Stroke = Stroke(paint),
        };

    /// <summary>
    ///     A lifeline whose end is fixed after the participants are drawn.
    /// </summary>
    /// <param name="slot">The place of the lifeline (it is the first item of the place).</param>
    /// <param name="name">The name of the participant.</param>
    /// <param name="x">The x.</param>
    /// <param name="y1">The top.</param>
    /// <param name="location">The location of the participant.</param>
    private sealed class Lifeline(SceneSlot slot, string name, double x, double y1, MermaidLocation location)
    {
        /// <summary>Gets the place of the lifeline.</summary>
        public SceneSlot Slot { get; } = slot;

        /// <summary>Gets the name of the participant.</summary>
        public string Name { get; } = name;

        /// <summary>Gets the x.</summary>
        public double X { get; } = x;

        /// <summary>Gets the top.</summary>
        public double Y1 { get; } = y1;

        /// <summary>Gets or sets the bottom (2000 until it is fixed).</summary>
        public double Y2 { get; set; } = 2000;

        /// <summary>Gets the location of the participant.</summary>
        public MermaidLocation Location { get; } = location;
    }
}
