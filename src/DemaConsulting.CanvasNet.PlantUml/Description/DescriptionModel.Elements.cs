// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The symbol of an element of a description diagram (PlantUML's <c>USymbols</c> and the leaf types of the
///     description diagram): what the element is drawn as.
/// </summary>
internal enum DescriptionSymbol
{
    /// <summary>A name only written in links, resolved when the diagram is read to an actor or an interface.</summary>
    Unknown,

    /// <summary>A component (<c>component</c>, <c>[x]</c>).</summary>
    Component,

    /// <summary>An interface (<c>interface</c>, <c>() x</c>, and a name only written in links of a component diagram).</summary>
    Interface,

    /// <summary>An actor (<c>actor</c>, <c>:x:</c>).</summary>
    Actor,

    /// <summary>A business actor (<c>actor/</c>, <c>:x:/</c>).</summary>
    BusinessActor,

    /// <summary>A use case (<c>usecase</c>, <c>(x)</c>).</summary>
    UseCase,

    /// <summary>A business use case (<c>usecase/</c>, <c>(x)/</c>).</summary>
    BusinessUseCase,

    /// <summary>A node (<c>node</c>).</summary>
    Node,

    /// <summary>A rectangle (<c>rectangle</c>).</summary>
    Rectangle,

    /// <summary>A folder (<c>folder</c>).</summary>
    Folder,

    /// <summary>A package (<c>package</c>): a folder with its name in the tab.</summary>
    Package,

    /// <summary>A frame (<c>frame</c>).</summary>
    Frame,

    /// <summary>A cloud (<c>cloud</c>).</summary>
    Cloud,

    /// <summary>A database (<c>database</c>).</summary>
    Database,

    /// <summary>A card (<c>card</c>).</summary>
    Card,

    /// <summary>An artifact (<c>artifact</c>).</summary>
    Artifact,

    /// <summary>A storage (<c>storage</c>).</summary>
    Storage,

    /// <summary>A file (<c>file</c>).</summary>
    File,

    /// <summary>A queue (<c>queue</c>).</summary>
    Queue,

    /// <summary>A stack (<c>stack</c>).</summary>
    Stack,

    /// <summary>An agent (<c>agent</c>).</summary>
    Agent,

    /// <summary>A hexagon (<c>hexagon</c>).</summary>
    Hexagon,

    /// <summary>A person (<c>person</c>).</summary>
    Person,

    /// <summary>A boundary (<c>boundary</c>).</summary>
    Boundary,

    /// <summary>A control (<c>control</c>).</summary>
    Control,

    /// <summary>An entity (<c>entity</c>).</summary>
    Entity,

    /// <summary>Collections (<c>collections</c>).</summary>
    Collections,

    /// <summary>A label (<c>label</c>).</summary>
    Label,

    /// <summary>An action (<c>action</c>).</summary>
    Action,

    /// <summary>A process (<c>process</c>).</summary>
    Process,

    /// <summary>A circle (<c>circle</c>).</summary>
    Circle,

    /// <summary>An archimate element (<c>archimate</c>).</summary>
    Archimate,
}

/// <summary>
///     A node of a description diagram: an element (a leaf or a container) or a note.
/// </summary>
/// <param name="id">The id: the qualified name PlantUML gives the node (<c>Container.Name</c>).</param>
/// <param name="location">The location of the statement that created it.</param>
internal abstract class DescriptionNode(string id, PlantUmlLocation location)
{
    /// <summary>
    ///     Gets the id.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    ///     Gets the location of the statement that created the node.
    /// </summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>
    ///     Gets or sets the container the node is in, or null at the top of the diagram.
    /// </summary>
    public DescriptionElement? Container { get; set; }
}

/// <summary>
///     An element of a description diagram: a leaf (an actor, a use case, a component, an interface, a node, ...) or a
///     container (an element declared with a block).
/// </summary>
/// <param name="id">The id.</param>
/// <param name="name">The name it was created with (the last part of its id).</param>
/// <param name="symbol">The symbol.</param>
/// <param name="location">The location of the statement that created it.</param>
internal sealed class DescriptionElement(string id, string name, DescriptionSymbol symbol, PlantUmlLocation location) : DescriptionNode(id, location)
{
    /// <summary>
    ///     Gets the name the element was created with, which a link outside its container may name it by.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///     Gets or sets the text that is drawn (several lines for a multi-line description), or null for the name; the
    ///     title of a container, empty for an anonymous container.
    /// </summary>
    public string? Display { get; set; }

    /// <summary>
    ///     Gets or sets the symbol.
    /// </summary>
    public DescriptionSymbol Symbol { get; set; } = symbol;

    /// <summary>
    ///     Gets the stereotypes, in order.
    /// </summary>
    public List<string> Stereotypes { get; } = [];

    /// <summary>
    ///     Gets or sets the fill (<c>#color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the line color (<c>##color</c>, <c>#back;line:color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Line { get; set; }

    /// <summary>
    ///     Gets or sets the text color (<c>#back;text:color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? TextColor { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the element is a container (declared with a block).
    /// </summary>
    public bool IsContainer { get; set; }

    /// <summary>
    ///     Gets the text that is drawn: the display, or the name.
    /// </summary>
    public string Text => Display ?? Name;

    /// <summary>
    ///     Gets the nesting depth: zero at the top of the diagram, one inside a container, and so on.
    /// </summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            for (var container = Container; container is not null; container = container.Container)
            {
                depth++;
            }

            return depth;
        }
    }
}

/// <summary>
///     A note of a description diagram: attached to a side of an element, to the last link, or floating.
/// </summary>
/// <param name="id">The id (its alias, or a generated id).</param>
/// <param name="text">The text.</param>
/// <param name="location">The location.</param>
internal sealed class DescriptionNote(string id, string text, PlantUmlLocation location) : DescriptionNode(id, location)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Gets or sets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the leaf element the note is attached to, or null.
    /// </summary>
    public DescriptionElement? Target { get; set; }

    /// <summary>
    ///     Gets or sets the link the note is attached to, or null.
    /// </summary>
    public DescriptionLink? OnLink { get; set; }

    /// <summary>
    ///     Gets or sets the side of the target the note is on.
    /// </summary>
    public NotePosition Position { get; set; } = NotePosition.Right;

    /// <summary>
    ///     Gets a value indicating whether the note floats (has no target and no link).
    /// </summary>
    public bool IsFloating => Target is null && OnLink is null;
}

/// <summary>
///     A link of a description diagram (or a link to a floating note), with its arrow's settings and its labels.
/// </summary>
/// <param name="id">The id.</param>
/// <param name="from">The first end as written.</param>
/// <param name="to">The second end as written.</param>
/// <param name="location">The location of the statement.</param>
internal sealed class DescriptionLink(string id, DescriptionNode from, DescriptionNode to, PlantUmlLocation location)
{
    /// <summary>Gets the id.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the first end.</summary>
    public DescriptionNode From { get; } = from;

    /// <summary>Gets the second end.</summary>
    public DescriptionNode To { get; } = to;

    /// <summary>Gets the location of the statement.</summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>Gets the decoration at the first end.</summary>
    public LinkDecor TailDecor { get; init; }

    /// <summary>Gets the decoration at the second end.</summary>
    public LinkDecor HeadDecor { get; init; }

    /// <summary>Gets the line style (a body of <c>.</c> is dashed, of <c>=</c> bold).</summary>
    public LinkStyle Style { get; init; }

    /// <summary>Gets the line width, or null.</summary>
    public double? Thickness { get; init; }

    /// <summary>Gets the line color, or null.</summary>
    public Rgba32? Color { get; init; }

    /// <summary>Gets the length of the arrow body.</summary>
    public int Length { get; init; } = 2;

    /// <summary>Gets the direction hint.</summary>
    public LinkDirection Direction { get; init; }

    /// <summary>Gets the label after the colon, or null.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the direction mark of the label.</summary>
    public LinkLabelArrow LabelArrow { get; init; }

    /// <summary>Gets the label near the first end, or null.</summary>
    public string? TailLabel { get; init; }

    /// <summary>Gets the label near the second end, or null.</summary>
    public string? HeadLabel { get; init; }

    /// <summary>Gets the weight (<c>@weight</c>); 1 by default.</summary>
    public double Weight { get; init; } = 1;

    /// <summary>Gets a value indicating whether the link touches a note (it is not counted).</summary>
    public bool TouchesNote => From is DescriptionNote || To is DescriptionNote;
}
