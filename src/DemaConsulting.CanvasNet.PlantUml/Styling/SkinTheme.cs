// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml.Styling;

/// <summary>
///     The elements of a diagram that have their own style in PlantUML's skin.
/// </summary>
internal enum SkinElement
{
    /// <summary>The document: background, margins, and the default text.</summary>
    Document,

    /// <summary>A <c>participant</c> head and tail.</summary>
    Participant,

    /// <summary>An <c>actor</c> stick figure.</summary>
    Actor,

    /// <summary>A <c>boundary</c> symbol.</summary>
    Boundary,

    /// <summary>A <c>control</c> symbol.</summary>
    Control,

    /// <summary>An <c>entity</c> symbol.</summary>
    Entity,

    /// <summary>A <c>database</c> symbol.</summary>
    Database,

    /// <summary>A <c>collections</c> box pair.</summary>
    Collections,

    /// <summary>A <c>queue</c> symbol.</summary>
    Queue,

    /// <summary>The dashed life line under a participant.</summary>
    LifeLine,

    /// <summary>An activation box on a life line.</summary>
    ActivationBox,

    /// <summary>A message arrow and its label.</summary>
    Arrow,

    /// <summary>A note.</summary>
    Note,

    /// <summary>The frame of a group (<c>alt</c>, <c>loop</c>, ...) and its condition texts.</summary>
    Group,

    /// <summary>The header tab of a group with its keyword.</summary>
    GroupHeader,

    /// <summary>A divider (<c>== text ==</c>).</summary>
    Separator,

    /// <summary>The text of a delay (<c>...text...</c>) and its dotted life lines.</summary>
    Delay,

    /// <summary>A box around participants (<c>box ... end box</c>).</summary>
    Box,

    /// <summary>The title.</summary>
    Title,

    /// <summary>The page header.</summary>
    Header,

    /// <summary>The page footer.</summary>
    Footer,

    /// <summary>The legend.</summary>
    Legend,

    /// <summary>The caption.</summary>
    Caption,

    /// <summary>An action box of an activity diagram.</summary>
    Activity,

    /// <summary>A decision diamond of an activity diagram.</summary>
    ActivityDiamond,

    /// <summary>An arrow of an activity diagram and its label.</summary>
    ActivityArrow,

    /// <summary>The start, stop, and end circles of an activity diagram.</summary>
    ActivityCircle,

    /// <summary>A fork or split bar of an activity diagram.</summary>
    ActivityBar,

    /// <summary>A swimlane and its title.</summary>
    Swimlane,

    /// <summary>A partition frame of an activity diagram.</summary>
    Partition,

    /// <summary>A link of a diagram that PlantUML lays out with Graphviz (class, state, use case, ...) and its labels.</summary>
    SvekArrow,

    /// <summary>A package cluster (its folder outline and bold title) of such a diagram.</summary>
    Package,

    /// <summary>A container cluster (rectangle, node, frame, cloud, database, card) of such a diagram.</summary>
    Container,

    /// <summary>A class box of a class diagram: its outline, its fill, its name, and its members.</summary>
    Class,

    /// <summary>The stereotypes and the generic parameters of a class or an object.</summary>
    ClassStereotype,

    /// <summary>The members (fields and methods) of a class or an object, and the titles of its separators.</summary>
    ClassAttribute,

    /// <summary>An object or a map box of an object diagram: its outline, its fill, its name, and its fields.</summary>
    Object,

    /// <summary>A state box and a composite state of a state diagram: its outline, its fill, and its name.</summary>
    State,

    /// <summary>The description lines of a state.</summary>
    StateAttribute,

    /// <summary>The start circle of a state diagram (<c>[*]</c> as a source).</summary>
    StateStart,

    /// <summary>The end circle of a state diagram (<c>[*]</c> as a target, <c>&lt;&lt;end&gt;&gt;</c>).</summary>
    StateEnd,

    /// <summary>The choice diamond and the history circles of a state diagram.</summary>
    StatePseudo,

    /// <summary>A component of a description diagram (<c>component</c>, <c>[x]</c>): its box, icon, and texts.</summary>
    Component,

    /// <summary>An interface circle of a description diagram (<c>interface</c>, <c>() x</c>) and its name.</summary>
    Interface,

    /// <summary>A node symbol of a description diagram drawn as an element (not as a container).</summary>
    Node,

    /// <summary>A rectangle symbol of a description diagram drawn as an element.</summary>
    Rectangle,

    /// <summary>A folder (or package) symbol of a description diagram drawn as an element.</summary>
    Folder,

    /// <summary>A frame symbol of a description diagram drawn as an element.</summary>
    Frame,

    /// <summary>A cloud symbol of a description diagram drawn as an element.</summary>
    Cloud,

    /// <summary>A card symbol of a description diagram drawn as an element.</summary>
    Card,

    /// <summary>A use case ellipse of a description diagram (<c>usecase</c>, <c>(x)</c>) and its texts.</summary>
    UseCase,

    /// <summary>The other elements of a description diagram (the symbols that have no style of their own yet).</summary>
    Description,
}

/// <summary>
///     The style of one element: its text, its fill, its outline and its shape parameters.
/// </summary>
/// <param name="FontFamily">The font family name as PlantUML writes it (<c>SansSerif</c> by default).</param>
/// <param name="FontSize">The font size in pixels.</param>
/// <param name="FontColor">The text color.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Fill">The fill color, or null for none.</param>
/// <param name="Line">The outline or line color, or null for none.</param>
/// <param name="LineWidth">The outline or line width in pixels.</param>
/// <param name="RoundCorner">The diameter of the rounded corners (an SVG radius of half of it), zero for square.</param>
/// <param name="Padding">The space between the outline and the text.</param>
internal sealed record ElementStyle(
    string FontFamily,
    double FontSize,
    Rgba32 FontColor,
    bool Bold,
    Rgba32? Fill,
    Rgba32? Line,
    double LineWidth,
    double RoundCorner,
    double Padding)
{
    /// <summary>
    ///     Gets a value indicating whether the text is italic.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    ///     Gets the dash pattern of the line (lengths in pixels), or null for a solid line.
    /// </summary>
    public IReadOnlyList<double>? Dash { get; init; }
}

/// <summary>
///     The default look of PlantUML (its <c>plantuml.skin</c>): the style of every element and the document settings,
///     with the values a diagram's <c>skinparam</c> statements change applied.
/// </summary>
/// <remarks>
///     <para>
///         The defaults are the values of PlantUML's default skin (the modern look, not the older yellow and red
///         "rose" look): a white document, <c>SansSerif</c> text (drawn with the bundled Liberation Sans, which has the
///         metrics of Arial, Java's <c>SansSerif</c> on Windows) of 14 pixels, participants filled with <c>#E2E2F0</c>
///         and outlined with <c>#181818</c> half a pixel wide, notes filled with <c>#FEFFDD</c>, group frames black and
///         1.5 pixels wide, the links of the graph diagrams with labels of 13 pixels, their package clusters (corners of 5) outlined in
///         black 1.5 pixels wide and their container clusters in `#181818` one pixel wide with corners of 5, both with bold
///         titles, and the sizes and paddings of each element.
///     </para>
///     <para>
///         A theme is immutable: <see cref="With(SkinElement, Func{ElementStyle, ElementStyle})"/> and the
///         <c>with</c> expressions return a new theme, so <see cref="Default"/> is shared safely between threads and
///         calls.
///     </para>
/// </remarks>
internal sealed record SkinTheme
{
    /// <summary>The line color of the default skin.</summary>
    public static readonly Rgba32 LineColor = Rgb(0x18, 0x18, 0x18);

    /// <summary>The fill of participants and their kin in the default skin (<c>--grey-blue</c>).</summary>
    public static readonly Rgba32 GreyBlue = Rgb(0xE2, 0xE2, 0xF0);

    /// <summary>The fill of notes in the default skin (<c>--note-background</c>).</summary>
    public static readonly Rgba32 NoteBackground = Rgb(0xFE, 0xFF, 0xDD);

    /// <summary>The fill of activities in the default skin (<c>--common-background</c>).</summary>
    public static readonly Rgba32 CommonBackground = Rgb(0xF1, 0xF1, 0xF1);

    /// <summary>Black.</summary>
    public static readonly Rgba32 Black = Rgb(0, 0, 0);

    /// <summary>White.</summary>
    public static readonly Rgba32 White = Rgb(0xFF, 0xFF, 0xFF);

    /// <summary>The family name of PlantUML's default font.</summary>
    public const string DefaultFontFamily = "SansSerif";

    /// <summary>
    ///     Initializes a theme from its styles.
    /// </summary>
    /// <param name="styles">The style of every element.</param>
    private SkinTheme(IReadOnlyDictionary<SkinElement, ElementStyle> styles)
    {
        Styles = styles;
    }

    /// <summary>
    ///     Gets the participant-like elements, which share PlantUML's participant defaults.
    /// </summary>
    public static IReadOnlyList<SkinElement> ParticipantElements { get; } =
    [
        SkinElement.Participant, SkinElement.Actor, SkinElement.Boundary, SkinElement.Control, SkinElement.Entity,
        SkinElement.Database, SkinElement.Collections, SkinElement.Queue,
    ];

    /// <summary>
    ///     Gets the elements of description diagrams (use case, component, and deployment), which share the defaults of
    ///     PlantUML's <c>componentDiagram</c> elements: the common background, an outline half a pixel wide, and corners
    ///     of 5.
    /// </summary>
    public static IReadOnlyList<SkinElement> DescriptionElements { get; } =
    [
        SkinElement.Component, SkinElement.Interface, SkinElement.Node, SkinElement.Rectangle, SkinElement.Folder,
        SkinElement.Frame, SkinElement.Cloud, SkinElement.Card, SkinElement.UseCase, SkinElement.Description,
    ];

    /// <summary>
    ///     Gets PlantUML's default look.
    /// </summary>
    public static SkinTheme Default { get; } = CreateDefault();

    /// <summary>
    ///     Gets the default look of description diagrams: <see cref="Default"/>, with the symbols that sequence diagrams
    ///     share (a <c>database</c> and an <c>actor</c>) filled with the common background, as PlantUML's
    ///     <c>componentDiagram</c> element style fills them.
    /// </summary>
    public static SkinTheme Description { get; } = Default
        .With(SkinElement.Database, s => s with { Fill = CommonBackground, Padding = 0 })
        .With(SkinElement.Actor, s => s with { Fill = CommonBackground, Padding = 0 });

    /// <summary>
    ///     Gets the background of the document, or null for a transparent document; white by default.
    /// </summary>
    public Rgba32? Background { get; init; } = White;

    /// <summary>
    ///     Gets a value indicating whether elements cast shadows; false by default (shadows are not drawn in this
    ///     version either way).
    /// </summary>
    public bool Shadowing { get; init; }

    /// <summary>
    ///     Gets the width at which message texts are wrapped, or zero for no wrapping (<c>maxMessageSize</c>).
    /// </summary>
    public double MaxMessageSize { get; init; }

    /// <summary>
    ///     Gets the alignment of message texts: <c>left</c> (the default), <c>center</c>, <c>right</c>, or
    ///     <c>direction</c> / <c>reverseDirection</c> (<c>sequenceMessageAlign</c>).
    /// </summary>
    public string MessageAlign { get; init; } = "left";

    /// <summary>
    ///     Gets a value indicating whether the text of a response (a dotted return arrow) is drawn below its arrow
    ///     (<c>responseMessageBelowArrow</c>); false by default.
    /// </summary>
    public bool ResponseBelowArrow { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the participants are drawn again at the bottom (the foot box); true by
    ///     default, false after <c>hide footbox</c> or <c>skinparam footbox hide</c>.
    /// </summary>
    public bool Footbox { get; init; } = true;

    /// <summary>
    ///     Gets the line width of message arrows (<c>sequenceArrowThickness</c>); 1 by default.
    /// </summary>
    public double ArrowThickness { get; init; } = 1;

    /// <summary>
    ///     Gets the size of the visibility icons of class members (<c>classAttributeIconSize</c>); 10 by default, 0 to
    ///     write the visibility characters instead of drawing icons.
    /// </summary>
    public double ClassAttributeIconSize { get; init; } = 10;

    /// <summary>
    ///     Gets the radius of the circle of a class header (<c>circledCharacterRadius</c>); 11 by default.
    /// </summary>
    public double CircledCharacterRadius { get; init; } = 11;

    /// <summary>
    ///     Gets the style of an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>Its style.</returns>
    public ElementStyle this[SkinElement element] => Styles[element];

    /// <summary>
    ///     Gets the styles by element.
    /// </summary>
    private IReadOnlyDictionary<SkinElement, ElementStyle> Styles { get; init; }

    /// <summary>
    ///     Returns a theme with one element's style changed.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="change">The change of its style.</param>
    /// <returns>The new theme.</returns>
    public SkinTheme With(SkinElement element, Func<ElementStyle, ElementStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var styles = new Dictionary<SkinElement, ElementStyle>(Styles) { [element] = change(Styles[element]) };
        return this with { Styles = styles };
    }

    /// <summary>
    ///     Creates an opaque color.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Rgb(byte r, byte g, byte b) => new(r, g, b, 255);

    /// <summary>
    ///     Builds the default look from PlantUML's default skin.
    /// </summary>
    /// <returns>The theme.</returns>
    private static SkinTheme CreateDefault()
    {
        // The root of the skin: SansSerif 14 in black, lines #181818 one pixel wide, no fill
        var root = new ElementStyle(DefaultFontFamily, 14, Black, false, null, LineColor, 1, 0, 0);
        var gray8 = Rgb(0x88, 0x88, 0x88);
        var grayE = Rgb(0xEE, 0xEE, 0xEE);
        var grayD = Rgb(0xDD, 0xDD, 0xDD);
        var styles = new Dictionary<SkinElement, ElementStyle>
        {
            [SkinElement.Document] = root with { Fill = White },
            [SkinElement.LifeLine] = root with { LineWidth = 0.5, Dash = [5, 5] },
            [SkinElement.ActivationBox] = root with { Fill = White },
            [SkinElement.Arrow] = root with { FontSize = 13 },
            [SkinElement.Note] = root with { FontSize = 13, Fill = NoteBackground, LineWidth = 0.5, Padding = 5 },
            [SkinElement.Group] = root with { FontSize = 11, Bold = true, Line = Black, LineWidth = 1.5 },
            [SkinElement.GroupHeader] = root with { FontSize = 13, Bold = true, Fill = grayE, Line = Black, LineWidth = 1.5 },
            [SkinElement.Separator] = root with { FontSize = 13, Bold = true, Fill = grayE, Line = Black, LineWidth = 2, Padding = 4 },
            [SkinElement.Delay] = root with { FontSize = 11, Dash = [1, 4], LineWidth = 0.5 },
            [SkinElement.Box] = root with { FontSize = 13, Bold = true, Fill = grayD, LineWidth = 0.5 },
            [SkinElement.Title] = root with { Bold = true, Line = null, Padding = 5 },
            [SkinElement.Header] = root with { FontSize = 10, FontColor = gray8, Line = null },
            [SkinElement.Footer] = root with { FontSize = 10, FontColor = gray8, Line = null },
            [SkinElement.Legend] = root with { Fill = grayD, Line = Black, RoundCorner = 15, Padding = 5 },
            [SkinElement.Caption] = root with { Line = null },
            [SkinElement.Activity] = root with { FontSize = 12, Fill = CommonBackground, LineWidth = 0.5, RoundCorner = 25, Padding = 10 },
            [SkinElement.ActivityDiamond] = root with { FontSize = 11, Fill = CommonBackground, LineWidth = 0.5 },
            [SkinElement.ActivityArrow] = root with { FontSize = 11 },
            [SkinElement.ActivityCircle] = root with { Fill = Rgb(0x22, 0x22, 0x22), Line = Rgb(0x22, 0x22, 0x22) },
            [SkinElement.ActivityBar] = root with { Fill = Rgb(0x55, 0x55, 0x55), LineWidth = 0.5 },
            [SkinElement.Swimlane] = root with { FontSize = 18, Line = Black, LineWidth = 1.5 },
            [SkinElement.Partition] = root with { Line = Black, LineWidth = 1.5 },
            [SkinElement.SvekArrow] = root with { FontSize = 13 },
            [SkinElement.Package] = root with { Bold = true, Line = Black, LineWidth = 1.5, RoundCorner = 5 },
            [SkinElement.Container] = root with { Bold = true, LineWidth = 1, RoundCorner = 5 },
            [SkinElement.Class] = root with { Fill = CommonBackground, LineWidth = 0.5, RoundCorner = 5 },
            [SkinElement.ClassStereotype] = root with { FontSize = 12, Italic = true },
            [SkinElement.ClassAttribute] = root,
            [SkinElement.Object] = root with { Fill = CommonBackground, LineWidth = 0.5, RoundCorner = 5 },
            [SkinElement.State] = root with { Fill = CommonBackground, LineWidth = 0.5, RoundCorner = 25 },
            [SkinElement.StateAttribute] = root,
            [SkinElement.StateStart] = root with { Fill = Rgb(0x22, 0x22, 0x22), Line = Rgb(0x22, 0x22, 0x22) },
            [SkinElement.StateEnd] = root with { Fill = Rgb(0x22, 0x22, 0x22), Line = Rgb(0x22, 0x22, 0x22) },
            [SkinElement.StatePseudo] = root with { Fill = CommonBackground, LineWidth = 0.5 },
        };

        // The elements of description diagrams: the common background, outlined half a pixel wide, corners of 5
        foreach (var element in DescriptionElements)
        {
            styles[element] = root with { Fill = CommonBackground, LineWidth = 0.5, RoundCorner = 5 };
        }

        // Participants and their kin: grey-blue, outlined half a pixel wide, padded 7; a participant has corners of 5
        foreach (var element in ParticipantElements)
        {
            styles[element] = root with
            {
                Fill = GreyBlue,
                LineWidth = 0.5,
                Padding = 7,
                RoundCorner = element == SkinElement.Participant ? 5 : 0,
            };
        }

        return new SkinTheme(styles);
    }
}
