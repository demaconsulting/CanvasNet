// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     A text placed by the layout: a link label, an end label, with its box and, for a label with a direction mark,
///     the direction of its triangle.
/// </summary>
/// <param name="Text">The laid-out text.</param>
/// <param name="Box">The box of the label (the text with a margin of 1 on every side, and the triangle at its left).</param>
internal sealed record PlacedLabel(TextBlock Text, SceneRect Box)
{
    /// <summary>
    ///     Gets the direction the triangle of the label points to, in radians (y down), or null for a label without a
    ///     direction mark.
    /// </summary>
    public double? ArrowAngle { get; init; }
}

/// <summary>
///     An entity placed by the layout.
/// </summary>
/// <param name="Entity">The entity.</param>
/// <param name="Box">Its box.</param>
internal sealed record PlacedEntity(SvekEntity Entity, SceneRect Box);

/// <summary>
///     A cluster placed by the layout.
/// </summary>
/// <param name="Cluster">The cluster.</param>
/// <param name="Box">Its box (the outline, the header band included).</param>
internal sealed record PlacedCluster(SvekCluster Cluster, SceneRect Box)
{
    /// <summary>
    ///     Gets the dashed lines between the concurrent regions of the cluster, from border to border.
    /// </summary>
    public IReadOnlyList<(SvekPoint From, SvekPoint To)> Separators { get; init; } = [];
}

/// <summary>
///     A note placed by the layout, with the pointer to what it is attached to.
/// </summary>
/// <param name="Note">The note.</param>
/// <param name="Text">The laid-out text.</param>
/// <param name="Box">The box of the note.</param>
internal sealed record PlacedNote(SvekNote Note, TextBlock Text, SceneRect Box)
{
    /// <summary>
    ///     Gets the side of the note the pointer leaves from, or null for a note without a pointer (a floating note, a
    ///     note on a link).
    /// </summary>
    public NotePosition? PointerSide { get; init; }

    /// <summary>
    ///     Gets the point on the border of the note where the pointer leaves.
    /// </summary>
    public SvekPoint PointerBase { get; init; }

    /// <summary>
    ///     Gets the point the pointer touches (on the border of the target).
    /// </summary>
    public SvekPoint PointerTip { get; init; }
}

/// <summary>
///     A link placed by the layout: its route from the first end to the second, clipped to the outlines of its ends, and
///     its labels.
/// </summary>
/// <param name="Link">The link.</param>
/// <param name="Route">The route, from the first end to the second; empty for a link that could not be routed.</param>
internal sealed record PlacedLink(SvekLink Link, IReadOnlyList<SvekPoint> Route)
{
    /// <summary>
    ///     Gets a value indicating whether the link is drawn (false for a hidden link, a link drawn as the pointer of a
    ///     note, and a link that could not be routed).
    /// </summary>
    public bool Drawn { get; init; } = true;

    /// <summary>Gets the label at the middle, or null.</summary>
    public PlacedLabel? Label { get; init; }

    /// <summary>Gets the label near the first end, or null.</summary>
    public PlacedLabel? TailLabel { get; init; }

    /// <summary>Gets the label near the second end, or null.</summary>
    public PlacedLabel? HeadLabel { get; init; }
}

/// <summary>
///     The result of a layout: the size of the drawing and every placed element, in scene units with the drawing's
///     top-left corner at the origin.
/// </summary>
/// <param name="Width">The width of the drawing.</param>
/// <param name="Height">The height of the drawing.</param>
/// <param name="Entities">The entities, in the order of the diagram.</param>
/// <param name="Clusters">The clusters, in the order of the diagram.</param>
/// <param name="Notes">The notes, in the order of the diagram.</param>
/// <param name="Links">The links, in the order of the diagram.</param>
internal sealed record SvekPlacement(
    double Width,
    double Height,
    IReadOnlyList<PlacedEntity> Entities,
    IReadOnlyList<PlacedCluster> Clusters,
    IReadOnlyList<PlacedNote> Notes,
    IReadOnlyList<PlacedLink> Links);

/// <summary>
///     The layout of the shared engine: places the entities, clusters, notes, and links of a <see cref="SvekDiagram"/>
///     with the dagre layered layout of the shared Layout package, as PlantUML places them with Graphviz.
/// </summary>
/// <remarks>
///     <para>
///         <b>Graph.</b> Every diagram is one compound multigraph per layout scope, with stable ids in the order of the
///         diagram (<c>e</c>, <c>n</c>, <c>c</c>, <c>a</c> followed by the index, never array-like keys, whose order
///         graphlib would change). The settings are PlantUML's: ranks top to bottom or left to right, a space of at least
///         35 between neighbors and 60 between ranks, raised by the widest and tallest label of a link (a tenth of it, as
///         PlantUML's <c>DotStringFactory</c> does) and replaced by <c>skinparam nodesep</c> and <c>ranksep</c>, and
///         edges 10 apart. A link of length <c>n</c> spans at least <c>n - 1</c> ranks (one dash keeps both ends in one
///         rank); <c>up</c> and <c>left</c> hints reverse the edge, and the route is turned back afterwards.
///     </para>
///     <para>
///         <b>Clusters.</b> A cluster that no link crosses is laid out on its own, innermost first, and becomes one node
///         of its parent's graph, as large as its members with its padding and header; a cluster that links cross is a
///         compound node of the graph, whose box is then made as tight as its members with its padding, and gets a
///         header band: everything below the cluster's top moves down by the height of the header, so nothing
///         overlaps by construction. A link to such a cluster goes to a hidden anchor inside it and is cut at its
///         border. The regions of a cluster with concurrent regions are laid out on their own and stacked or put side
///         by side, with dashed separators.
///     </para>
///     <para>
///         <b>Notes.</b> A note attached to an entity is a node of its own, joined to the entity by a hidden edge as
///         PlantUML joins them for Graphviz: a note left or right of the entity (top or bottom in a left to right
///         diagram) shares its rank, <see cref="NoteGap"/> away, and the others are one rank before or after it, so
///         the routes of the links keep clear of the notes. A floating note is a node of its own, and one with a
///         single link to an entity points at it instead of drawing the link. A note on a link enlarges the label of
///         the link.
///     </para>
///     <para>
///         <b>Routes.</b> dagre gives no route through the rank for an edge whose ends share a rank; such a link is
///         drawn straight between its ends, or as an arc over the rank when the straight line would cross another
///         node, with its label above its middle. Route ends are clipped to the outline the entity's image gives
///         (rectangle, ellipse, diamond), and end labels are placed beside the ends and moved off the entities.
///     </para>
///     <para>
///         <b>Limits.</b> One <see cref="PlantUmlLayoutWorkMeter"/> bounds the dagre runs and the engine's own loops;
///         a layout past <see cref="PlantUmlLimits.MaxLayoutWork"/> fails with a located
///         <see cref="PlantUmlRenderException"/>, and the meter observes the cancellation token. The layout is
///         deterministic and keeps no state between calls.
///     </para>
/// </remarks>
internal sealed partial class SvekLayout
{
    /// <summary>The smallest space between neighbors in a rank (PlantUML's <c>getMinNodeSep</c>).</summary>
    public const double MinNodeSep = 35;

    /// <summary>The smallest space between ranks (PlantUML's <c>getMinRankSep</c>).</summary>
    public const double MinRankSep = 60;

    /// <summary>How much further out each further link drawn in a row between the same two units bulges (at its control point).</summary>
    public const double ParallelArcStep = 50;

    /// <summary>The space between edges in a rank.</summary>
    public const double EdgeSep = 10;

    /// <summary>The space between an entity and a note attached to it (measured from the reference pictures).</summary>
    public const double NoteGap = 18;

    /// <summary>The space between two notes on the same side of an entity.</summary>
    public const double NoteStackGap = 10;

    /// <summary>The space around the members of a region of a cluster with concurrent regions that has no image.</summary>
    public const double RegionPadding = 10;

    /// <summary>The space between the sides of a cluster and the ends of the dashed separators of its regions.</summary>
    public const double SeparatorInset = 5;

    /// <summary>The margin around the text of a link label (PlantUML's <c>marginLabel</c>).</summary>
    public const double LabelMargin = 1;

    /// <summary>The space between a link and its label (dagre's <c>labeloffset</c>).</summary>
    public const double LabelOffset = 2;

    /// <summary>
    ///     The least space between a label and the drawn curves of the links (PlantUML leaves about 4 pixels), and the
    ///     space above or beside the middle of a link that dagre gives no label place.
    /// </summary>
    public const double LabelClearance = 4;

    /// <summary>
    ///     The space a label between two neighbors of a row leaves to them, together (PlantUML leaves about 10 pixels
    ///     on each side).
    /// </summary>
    public const double RowLabelSpace = 20;

    /// <summary>The space between a note and the text inside it, left (PlantUML's <c>Opale.marginX1</c>).</summary>
    public const double NoteMarginLeft = 6;

    /// <summary>The space between a note and the text inside it, right (PlantUML's <c>Opale.marginX2</c>).</summary>
    public const double NoteMarginRight = 15;

    /// <summary>The space between a note and the text inside it, above and below.</summary>
    public const double NoteMarginY = 5;

    /// <summary>The farthest, in pixels, a label is moved away from the curve of its link to keep its clearance.</summary>
    private const int LabelClearSteps = 32;

    /// <summary>The number of points each cubic piece of a curve is measured at when a label is kept clear of it.</summary>
    private const int CurveSteps = 16;

    /// <summary>The diagram.</summary>
    private readonly SvekDiagram _diagram;

    /// <summary>The context of the call.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The work meter.</summary>
    private readonly PlantUmlLayoutWorkMeter _meter;

    /// <summary>The graph id of every node.</summary>
    private readonly Dictionary<SvekNode, string> _ids = new(ReferenceEqualityComparer.Instance);

    /// <summary>The index of every link in the diagram.</summary>
    private readonly Dictionary<SvekLink, int> _linkIndex = new(ReferenceEqualityComparer.Instance);

    /// <summary>The clusters that are laid out on their own.</summary>
    private readonly HashSet<SvekCluster> _autonomous = new(ReferenceEqualityComparer.Instance);

    /// <summary>The notes attached to each entity, in the order of the diagram.</summary>
    private readonly Dictionary<SvekEntity, List<SvekNote>> _attached = new(ReferenceEqualityComparer.Instance);

    /// <summary>The note on each link.</summary>
    private readonly Dictionary<SvekLink, SvekNote> _linkNotes = new(ReferenceEqualityComparer.Instance);

    /// <summary>The floating notes drawn as pointers to the other end of their single link.</summary>
    private readonly Dictionary<SvekNote, SvekLink> _pointerNotes = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Initializes a layout.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="meter">The work meter.</param>
    private SvekLayout(SvekDiagram diagram, DiagramRenderContext context, PlantUmlLayoutWorkMeter meter)
    {
        _diagram = diagram;
        _context = context;
        _meter = meter;
    }

    /// <summary>
    ///     Gets the space between neighbors of the graphs of this layout.
    /// </summary>
    private double NodeSep { get; set; }

    /// <summary>
    ///     Gets the space between ranks of the graphs of this layout.
    /// </summary>
    private double RankSep { get; set; }

    /// <summary>
    ///     Lays a diagram out under the limits and the cancellation token of a call.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the call (text layout, report sink, limits, cancellation).</param>
    /// <returns>The placement.</returns>
    /// <exception cref="ArgumentException">Thrown when the diagram is not consistent (<see cref="SvekDiagram.Validate"/>).</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when the layout needs more work than the limit allows or fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static SvekPlacement Run(SvekDiagram diagram, DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(context);
        return Run(diagram, context, new PlantUmlLayoutWorkMeter(context.Options.Limits.MaxLayoutWork, diagram.Location, context.CancellationToken));
    }

    /// <summary>
    ///     Lays a diagram out, charging a given meter (the tests read the work it used).
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="meter">The work meter of the call.</param>
    /// <returns>The placement.</returns>
    /// <exception cref="ArgumentException">Thrown when the diagram is not consistent.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown, located, when the layout needs more work than the meter allows or fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static SvekPlacement Run(SvekDiagram diagram, DiagramRenderContext context, PlantUmlLayoutWorkMeter meter)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(meter);
        diagram.Validate();
        context.CancellationToken.ThrowIfCancellationRequested();
        return new SvekLayout(diagram, context, meter).Execute();
    }

    /// <summary>
    ///     Runs the layout.
    /// </summary>
    /// <returns>The placement.</returns>
    private SvekPlacement Execute()
    {
        Prepare();
        var root = LayoutScope(null);
        root.Normalize();
        return root.ToPlacement(_diagram);
    }

    /// <summary>
    ///     Assigns the graph ids, finds the clusters laid out on their own, groups the notes, and sets the spacing.
    /// </summary>
    private void Prepare()
    {
        // Stable ids in the order of the diagram
        for (var k = 0; k < _diagram.Entities.Count; k++)
        {
            _ids[_diagram.Entities[k]] = "e" + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var k = 0; k < _diagram.Notes.Count; k++)
        {
            _ids[_diagram.Notes[k]] = "n" + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var k = 0; k < _diagram.Clusters.Count; k++)
        {
            _ids[_diagram.Clusters[k]] = "c" + k.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var k = 0; k < _diagram.Links.Count; k++)
        {
            _linkIndex[_diagram.Links[k]] = k;
        }

        _meter.Charge(_diagram.Entities.Count + _diagram.Notes.Count + _diagram.Clusters.Count + _diagram.Links.Count + 1);

        // Notes attached to entities and to links
        foreach (var note in _diagram.Notes)
        {
            if (note.Target is { } target)
            {
                if (!_attached.TryGetValue(target, out var list))
                {
                    _attached[target] = list = [];
                }

                list.Add(note);
            }
            else if (note.Link is { } link)
            {
                _linkNotes.TryAdd(link, note);
            }
        }

        FindPointerNotes();
        FindAutonomousClusters();
        SetSpacing();
    }

    /// <summary>
    ///     Finds the floating notes that have exactly one link, to a node that is not a note: PlantUML draws them with a
    ///     pointer to that node instead of the link.
    /// </summary>
    private void FindPointerNotes()
    {
        foreach (var note in _diagram.Notes.Where(n => n.Target is null && n.Link is null))
        {
            var links = _diagram.Links.Where(l => ReferenceEquals(l.From, note) || ReferenceEquals(l.To, note)).Take(2).ToList();
            if (links.Count == 1 && ((links[0].From is not SvekNote) ^ (links[0].To is not SvekNote)) && !ReferenceEquals(links[0].From, links[0].To))
            {
                _pointerNotes[note] = links[0];
            }
        }
    }

    /// <summary>
    ///     Sets the spacing of the graphs: PlantUML's minimums raised by the labels of the links and replaced by the
    ///     diagram's settings, and at least what the cluster paddings need.
    /// </summary>
    private void SetSpacing()
    {
        // PlantUML's dzeta: a tenth of the widest horizontal and of the tallest vertical label sum of a link
        double horizontal = 0;
        double vertical = 0;
        foreach (var link in _diagram.Links)
        {
            var sizes = new[] { link.Label, link.TailLabel, link.HeadLabel }
                .Where(s => s is not null)
                .Select(s => Measure(s!, SkinElement.SvekArrow, link.Location))
                .ToList();
            var decor = SvekGeometry.Margin(link.TailDecor) + SvekGeometry.Margin(link.HeadDecor);
            if (ReferenceEquals(link.From, link.To))
            {
                horizontal = Math.Max(horizontal, decor);
                vertical = Math.Max(vertical, decor);
            }
            else if (link.Length == 1)
            {
                horizontal = Math.Max(horizontal, sizes.Sum(s => s.Width) + decor);
            }
            else
            {
                vertical = Math.Max(vertical, sizes.Sum(s => s.Height) + decor);
            }
        }

        NodeSep = _diagram.NodeSep ?? Math.Max(MinNodeSep, horizontal / 10);
        RankSep = _diagram.RankSep ?? Math.Max(MinRankSep, vertical / 10);

        // A compound cluster's outline must stay inside the box dagre keeps around its members
        var padding = _diagram.Clusters.Select(c => c.Image?.Padding ?? 0).DefaultIfEmpty(0).Max();
        NodeSep = Math.Max(NodeSep, (2 * padding) - EdgeSep);
        RankSep = Math.Max(RankSep, 2 * padding);
    }

    /// <summary>
    ///     Measures a text in the style of an element.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="element">The element whose style is used.</param>
    /// <param name="location">The location for reports.</param>
    /// <returns>The laid-out text.</returns>
    private TextBlock Measure(string text, SkinElement element, PlantUmlLocation location)
    {
        var style = _diagram.Theme[element];
        var font = new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic);
        return _context.Text.Layout(text, font, style.FontColor, 0, _context.Sink, location);
    }

    /// <summary>
    ///     Gets the ancestors of a node, nearest first (for an attached note, those of its target).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The clusters that contain it.</returns>
    private static IEnumerable<SvekCluster> Ancestors(SvekNode node)
    {
        var start = node is SvekNote { Target: { } target } ? target.Parent : node.Parent;
        for (var cluster = start; cluster is not null; cluster = cluster.Parent)
        {
            yield return cluster;
        }
    }
}
