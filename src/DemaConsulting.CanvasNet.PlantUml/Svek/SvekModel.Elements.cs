// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The decoration at one end of a link (PlantUML's <c>LinkDecor</c>): what is drawn where the link meets an entity.
/// </summary>
/// <remarks>
///     The names follow PlantUML's. The crow's foot family (<see cref="Crowfoot"/>, <see cref="CircleCrowfoot"/>,
///     <see cref="CircleLine"/>, <see cref="DoubleLine"/>, <see cref="LineCrowfoot"/>) and the extension variants
///     <see cref="Redefines"/> and <see cref="DefinedBy"/> are read but not drawn (the line ends plain);
///     <see cref="SvekGeometry.IsDrawn(LinkDecor)"/> tells a diagram which ones to report.
/// </remarks>
internal enum LinkDecor
{
    /// <summary>No decoration: the line ends at the entity.</summary>
    None,

    /// <summary>The hollow triangle of an extension or implementation (<c>&lt;|</c>, <c>|&gt;</c>, <c>^</c>).</summary>
    Extends,

    /// <summary>The filled diamond of a composition (<c>*</c>).</summary>
    Composition,

    /// <summary>The hollow diamond of an aggregation (<c>o</c>).</summary>
    Aggregation,

    /// <summary>The cross of a non-navigable end (<c>x</c>).</summary>
    NotNavigable,

    /// <summary>The redefinition triangle (<c>&lt;||</c>, <c>||&gt;</c>); read, not drawn.</summary>
    Redefines,

    /// <summary>The definition triangle (<c>&lt;|:</c>, <c>:|&gt;</c>); read, not drawn.</summary>
    DefinedBy,

    /// <summary>The crow's foot (<c>}</c>, <c>{</c>); read, not drawn.</summary>
    Crowfoot,

    /// <summary>The crow's foot with a circle (<c>}o</c>, <c>o{</c>); read, not drawn.</summary>
    CircleCrowfoot,

    /// <summary>The bar with a circle (<c>|o</c>, <c>o|</c>); read, not drawn.</summary>
    CircleLine,

    /// <summary>The double bar (<c>||</c>); read, not drawn.</summary>
    DoubleLine,

    /// <summary>The crow's foot with a bar (<c>}|</c>, <c>|{</c>); read, not drawn.</summary>
    LineCrowfoot,

    /// <summary>The filled arrow head (<c>&lt;</c>, <c>&gt;</c>, <c>&lt;_</c>, <c>_&gt;</c>).</summary>
    Arrow,

    /// <summary>The small hollow triangle (<c>&lt;&lt;</c>, <c>&gt;&gt;</c>).</summary>
    ArrowTriangle,

    /// <summary>The hollow circle (<c>0</c>).</summary>
    Circle,

    /// <summary>The filled circle (<c>@</c>).</summary>
    CircleFill,

    /// <summary>The circle with a socket arc (<c>0)</c>, <c>(0</c>).</summary>
    CircleConnect,

    /// <summary>The socket arc of a required interface (<c>)</c>, <c>(</c>).</summary>
    Parenthesis,

    /// <summary>The hollow square (<c>#</c>).</summary>
    Square,

    /// <summary>The circled plus of a nested element (<c>+</c>).</summary>
    Plus,

    /// <summary>The upper half arrow (<c>\\</c>).</summary>
    HalfArrowUp,

    /// <summary>The lower half arrow (<c>//</c>).</summary>
    HalfArrowDown,
}

/// <summary>
///     The line style of a link.
/// </summary>
internal enum LinkStyle
{
    /// <summary>A solid line.</summary>
    Solid,

    /// <summary>A dashed line (dashes of 7 and gaps of 7).</summary>
    Dashed,

    /// <summary>A dotted line (dashes of 1 and gaps of 3).</summary>
    Dotted,

    /// <summary>A solid line 2 pixels wide.</summary>
    Bold,

    /// <summary>A link that is laid out but not drawn.</summary>
    Hidden,
}

/// <summary>
///     The direction hint of a link arrow (<c>-up-&gt;</c>, <c>-down-&gt;</c>, <c>-left-&gt;</c>, <c>-right-&gt;</c>).
/// </summary>
internal enum LinkDirection
{
    /// <summary>No hint: the length decides.</summary>
    None,

    /// <summary>The second end above the first.</summary>
    Up,

    /// <summary>The second end below the first.</summary>
    Down,

    /// <summary>The second end left of the first, in the same rank.</summary>
    Left,

    /// <summary>The second end right of the first, in the same rank.</summary>
    Right,
}

/// <summary>
///     The direction mark of a link label (<c>: label &gt;</c> or <c>: &lt; label</c>), drawn as a small triangle beside
///     the label that points along the link.
/// </summary>
internal enum LinkLabelArrow
{
    /// <summary>No mark.</summary>
    None,

    /// <summary>The triangle points from the first end to the second.</summary>
    Forward,

    /// <summary>The triangle points from the second end to the first.</summary>
    Backward,
}

/// <summary>
///     The rank direction of a diagram.
/// </summary>
internal enum SvekRankDir
{
    /// <summary>Ranks from top to bottom (the default).</summary>
    TopToBottom,

    /// <summary>Ranks from left to right (<c>left to right direction</c>).</summary>
    LeftToRight,
}

/// <summary>
///     The kind of outline that links end on.
/// </summary>
internal enum ClipShape
{
    /// <summary>A rectangle.</summary>
    Rectangle,

    /// <summary>An ellipse inscribed in the box (a circle when the box is square).</summary>
    Ellipse,

    /// <summary>A diamond whose corners are the middles of the sides of the box.</summary>
    Diamond,
}

/// <summary>
///     The outline of a cluster (PlantUML's container symbols).
/// </summary>
internal enum ClusterSymbol
{
    /// <summary>No outline (a region of a composite state, or a cluster that the diagram draws itself).</summary>
    None,

    /// <summary>A folder with a title tab (a <c>package</c> and a <c>folder</c>).</summary>
    Folder,

    /// <summary>A rectangle with rounded corners and a centered title.</summary>
    Rectangle,

    /// <summary>A frame with the title in a cut tab at the top left.</summary>
    Frame,

    /// <summary>A box with a 3D top and right side.</summary>
    Node,

    /// <summary>A cloud outline.</summary>
    Cloud,

    /// <summary>A cylinder.</summary>
    Database,

    /// <summary>A card: a rectangle with a line under the title.</summary>
    Card,
}

/// <summary>
///     The side of an entity, or of a link label, that a note is attached to.
/// </summary>
internal enum NotePosition
{
    /// <summary>Left of it.</summary>
    Left,

    /// <summary>Right of it.</summary>
    Right,

    /// <summary>Above it.</summary>
    Top,

    /// <summary>Below it.</summary>
    Bottom,
}

/// <summary>
///     How the regions of a cluster with concurrent regions (a composite state split by <c>--</c> or <c>||</c>) are
///     arranged.
/// </summary>
internal enum ClusterConcurrency
{
    /// <summary>No concurrent regions.</summary>
    None,

    /// <summary>The regions are stacked, separated by horizontal dashed lines (<c>--</c>).</summary>
    Stacked,

    /// <summary>The regions are side by side, separated by vertical dashed lines (<c>||</c>).</summary>
    SideBySide,
}

/// <summary>
///     A point in scene units.
/// </summary>
/// <param name="X">The x.</param>
/// <param name="Y">The y.</param>
internal readonly record struct SvekPoint(double X, double Y);

/// <summary>
///     The outline that links end on, inside the box of an entity.
/// </summary>
/// <param name="Shape">The kind of outline.</param>
/// <param name="Box">The box of the outline relative to the top-left corner of the entity.</param>
internal readonly record struct SvekClip(ClipShape Shape, SceneRect Box);

/// <summary>
///     The drawing of one entity of a diagram (PlantUML's <c>IEntityImage</c>): its size, the outline its links end on,
///     and the painter of its items.
/// </summary>
/// <remarks>
///     Each diagram subsystem implements its own images (class boxes, states, use cases, actors, components, ...),
///     measured with the text layout of the call before the diagram is laid out. An image is immutable and is drawn
///     once per call at the place the layout chose.
/// </remarks>
internal interface ISvekEntityImage
{
    /// <summary>
    ///     Gets the width of the entity; not negative.
    /// </summary>
    double Width { get; }

    /// <summary>
    ///     Gets the height of the entity; not negative.
    /// </summary>
    double Height { get; }

    /// <summary>
    ///     Gets the outline that links end on, relative to the top-left corner (usually the whole box; an interface
    ///     circle with its name below it clips to the circle only).
    /// </summary>
    SvekClip Clip { get; }

    /// <summary>
    ///     Draws the entity with its top-left corner at a point.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <returns>The scene items in paint order.</returns>
    IReadOnlyList<SceneItem> Draw(double x, double y);
}

/// <summary>
///     The drawing of one cluster (a package, a container, a composite state): the space its header needs and the
///     painter of its outline and title.
/// </summary>
/// <remarks>
///     <see cref="SvekRenderer.ClusterImage"/> builds the images of the standard container symbols; a diagram can supply
///     its own (a composite state with its name band). An image is immutable.
/// </remarks>
internal interface ISvekClusterImage
{
    /// <summary>
    ///     Gets the height of the header band above the members (the title and stereotypes); zero for none.
    /// </summary>
    double HeaderHeight { get; }

    /// <summary>
    ///     Gets the narrowest width that shows the header.
    /// </summary>
    double MinWidth { get; }

    /// <summary>
    ///     Gets the space between the members and the outline on every side.
    /// </summary>
    double Padding { get; }

    /// <summary>
    ///     Draws the cluster in its box.
    /// </summary>
    /// <param name="box">The box the layout gave the cluster.</param>
    /// <returns>The scene items in paint order (drawn before the members).</returns>
    IReadOnlyList<SceneItem> Draw(SceneRect box);
}

/// <summary>
///     An element of a diagram that the layout places and links connect: an entity, a note, or a cluster.
/// </summary>
/// <remarks>
///     The id is the qualified name the scene items carry (the reference comparison and the hosts find elements by
///     it); ids are unique in a diagram.
/// </remarks>
internal abstract class SvekNode
{
    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="id">The id; not null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when the id is null or empty.</exception>
    protected SvekNode(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
    }

    /// <summary>
    ///     Gets the id (the qualified name).
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the cluster that contains the node, or null at the top of the diagram.
    /// </summary>
    public SvekCluster? Parent { get; init; }

    /// <summary>
    ///     Gets the location of the statement that declared the node; unknown by default.
    /// </summary>
    public PlantUmlLocation Location { get; init; }
}

/// <summary>
///     An entity of a diagram: a class, a state, a use case, a component, ...
/// </summary>
/// <param name="id">The id (the qualified name).</param>
/// <param name="image">The drawing of the entity.</param>
internal sealed class SvekEntity(string id, ISvekEntityImage image) : SvekNode(id)
{
    /// <summary>
    ///     Gets the drawing of the entity.
    /// </summary>
    public ISvekEntityImage Image { get; } = image ?? throw new ArgumentNullException(nameof(image));
}

/// <summary>
///     A cluster of a diagram: a package, a container, or a composite state, with its members (the nodes whose
///     <see cref="SvekNode.Parent"/> it is).
/// </summary>
/// <param name="id">The id (the qualified name).</param>
internal sealed class SvekCluster(string id) : SvekNode(id)
{
    /// <summary>
    ///     Gets the drawing of the cluster, or null for a cluster that only groups its members (a region of a composite
    ///     state: no outline, no header, no padding).
    /// </summary>
    public ISvekClusterImage? Image { get; init; }

    /// <summary>
    ///     Gets how the regions of the cluster are arranged; <see cref="ClusterConcurrency.None"/> by default. A cluster
    ///     with concurrent regions has only clusters (its regions) as members and is always laid out on its own.
    /// </summary>
    public ClusterConcurrency Concurrency { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the cluster is always laid out on its own, as one node of its parent, even
    ///     when links cross its border (a composite state); false by default, when only a cluster without crossing
    ///     links is.
    /// </summary>
    public bool Autonomous { get; init; }
}

/// <summary>
///     A note of a diagram: attached to a side of an entity, attached to the label of a link, or floating.
/// </summary>
/// <remarks>
///     An attached note is placed by its target (its <see cref="SvekNode.Parent"/> is not used) and points at it; a
///     floating note is a node of its own that links may connect, and a floating note with exactly one link to an
///     entity points at that entity instead of drawing the link, as PlantUML does.
/// </remarks>
/// <param name="id">The id.</param>
/// <param name="text">The text (creole markup and <c>\n</c> allowed).</param>
internal sealed class SvekNote(string id, string text) : SvekNode(id)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>
    ///     Gets the fill, or null for the theme's note background.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets the entity the note is attached to, or null.
    /// </summary>
    public SvekEntity? Target { get; init; }

    /// <summary>
    ///     Gets the link whose label the note is attached to, or null.
    /// </summary>
    public SvekLink? Link { get; init; }

    /// <summary>
    ///     Gets the side of the target (or of the link label) the note is on; <see cref="NotePosition.Right"/> by default.
    /// </summary>
    public NotePosition Position { get; init; } = NotePosition.Right;
}

/// <summary>
///     A link between two nodes of a diagram, with its decorations, style, labels, length, and direction hint.
/// </summary>
/// <param name="id">The id; unique among the links of a diagram.</param>
/// <param name="from">The first end (the entity written first).</param>
/// <param name="to">The second end.</param>
internal sealed class SvekLink(string id, SvekNode from, SvekNode to)
{
    /// <summary>
    ///     Gets the id.
    /// </summary>
    public string Id { get; } = string.IsNullOrEmpty(id) ? throw new ArgumentException("The id must not be empty.", nameof(id)) : id;

    /// <summary>
    ///     Gets the first end.
    /// </summary>
    public SvekNode From { get; } = from ?? throw new ArgumentNullException(nameof(from));

    /// <summary>
    ///     Gets the second end.
    /// </summary>
    public SvekNode To { get; } = to ?? throw new ArgumentNullException(nameof(to));

    /// <summary>
    ///     Gets the decoration at the first end; none by default.
    /// </summary>
    public LinkDecor TailDecor { get; init; }

    /// <summary>
    ///     Gets the decoration at the second end; none by default.
    /// </summary>
    public LinkDecor HeadDecor { get; init; }

    /// <summary>
    ///     Gets the line style; solid by default.
    /// </summary>
    public LinkStyle Style { get; init; }

    /// <summary>
    ///     Gets the line width, or null for the style's (1, or 2 for bold).
    /// </summary>
    public double? Thickness { get; init; }

    /// <summary>
    ///     Gets the line color, or null for the theme's arrow color.
    /// </summary>
    public Rgba32? Color { get; init; }

    /// <summary>
    ///     Gets the length of the arrow body (the number of <c>-</c>, <c>.</c>, and <c>=</c>): a length of 1 keeps both ends
    ///     in the same rank, each further character adds a rank; 2 by default. At least 1.
    /// </summary>
    public int Length { get; init; } = 2;

    /// <summary>
    ///     Gets the direction hint; none by default.
    /// </summary>
    public LinkDirection Direction { get; init; }

    /// <summary>
    ///     Gets the label at the middle of the link, or null.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    ///     Gets the direction mark of the label; none by default.
    /// </summary>
    public LinkLabelArrow LabelArrow { get; init; }

    /// <summary>
    ///     Gets the label near the first end (a multiplicity), or null.
    /// </summary>
    public string? TailLabel { get; init; }

    /// <summary>
    ///     Gets the label near the second end, or null.
    /// </summary>
    public string? HeadLabel { get; init; }

    /// <summary>
    ///     Gets the weight that pulls the ends together (<c>@weight</c>); 1 by default. Positive.
    /// </summary>
    public double Weight { get; init; } = 1;

    /// <summary>
    ///     Gets the location of the statement; unknown by default.
    /// </summary>
    public PlantUmlLocation Location { get; init; }
}
