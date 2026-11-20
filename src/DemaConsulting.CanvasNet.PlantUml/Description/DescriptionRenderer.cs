// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The image of one leaf element of a description diagram: its size, the outline its links end on, and the painter
///     of its items.
/// </summary>
/// <remarks>
///     The image is measured once, when the diagram is lowered for a call, and is immutable afterwards.
/// </remarks>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
/// <param name="clip">The outline the links end on, relative to the top-left corner.</param>
/// <param name="draw">The painter: the items with the top-left corner at a point.</param>
internal sealed class DescriptionImage(double width, double height, SvekClip clip, Func<double, double, IReadOnlyList<SceneItem>> draw) : ISvekEntityImage
{
    /// <inheritdoc/>
    public double Width { get; } = width;

    /// <inheritdoc/>
    public double Height { get; } = height;

    /// <inheritdoc/>
    public SvekClip Clip { get; } = clip;

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(double x, double y) => draw(x, y);
}

/// <summary>
///     Measures and paints the elements and containers of description diagrams as PlantUML does
///     (<c>svek/image/EntityImageDescription</c>, the small and large forms of <c>decoration/symbol/USymbolComponent2</c>,
///     <c>USymbolInterface</c> with <c>svek/CircleInterface2</c>, <c>USymbolNode</c>, <c>USymbolRectangle</c>,
///     <c>USymbolFolder</c>, <c>USymbolFrame</c>, <c>USymbolCloud</c>, <c>USymbolDatabase</c>, <c>USymbolCard</c>,
///     <c>USymbolActor</c>, <c>USymbolActorBusiness</c>, and <c>USymbolUsecase</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Texts.</b> An element shows its stereotypes (each <c>«name»</c> on its own line, italic, centered, with a
///         margin of 1 on each side) above its name or multi-line description (14, its lines aligned left); the two
///         blocks are centered on each other.
///     </para>
///     <para>
///         <b>Component.</b> A rectangle with corners of 5 filled <c>#F1F1F1</c> and outlined <c>#181818</c> half a
///         pixel wide, the texts 15 from the left and 20 from the top, 25 from the right and 10 from the bottom, and the
///         UML component icon: a box of 15 by 10, 20 from the right and 5 from the top, with two tabs of 4 by 2 over its
///         left side. <b>Interface.</b> A circle of radius 8 (in a box of 18), the name 8 below it and the stereotypes 8
///         above it; links end on the circle. <b>Node</b>, <b>rectangle</b>, <b>folder</b>, <b>package</b>,
///         <b>frame</b>, <b>cloud</b>, <b>database</b>, and <b>card</b> are drawn with their symbol's outline and
///         margins. <b>Actor.</b> The stick figure (head filled <c>#F1F1F1</c>) with the stereotypes above it and the
///         name below it; a business actor's head is crossed by a slash. <b>Use case.</b> The texts, centered, in the
///         smallest ellipse of their proportions that holds every line, grown by 6; links end on the ellipse; a
///         business use case has a margin of 7 on each side of its texts and a slash across its lower right side. Every
///         other symbol is drawn as a rectangle (and reported by the parser).
///     </para>
///     <para>
///         <b>Containers.</b> A component container is a rectangle (corners of 5, <c>#181818</c> one pixel wide, not
///         filled) with the component icon at its top right, its stereotypes 13 from the top, and its bold title below
///         them, centered; the other containers are the Svek cluster symbols (<see cref="SvekRenderer.ClusterImage"/>)
///         with stereotypes of 14.
///     </para>
///     <para>
///         Every item of an element carries its id and one of the roles <c>description</c> (the outline),
///         <c>description-detail</c> (the other lines of an outline), <c>description-icon</c>,
///         <c>description-stereotype</c>, and <c>description-name</c>; the items of a component container carry the
///         roles <c>cluster</c>, <c>cluster-stereotype</c>, and <c>cluster-title</c>. The class is stateless and safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static partial class DescriptionRenderer
{
    /// <summary>The radius of an interface circle (<c>CircleInterface2</c>).</summary>
    public const double InterfaceRadius = 8;

    /// <summary>The side of the box of an interface circle: the circle and a margin of 1.</summary>
    public const double InterfaceBox = (2 * InterfaceRadius) + 2;

    /// <summary>The space between an interface circle and its name or stereotypes.</summary>
    public const double InterfaceSpace = 8;

    /// <summary>The leaf symbols that have their own drawing.</summary>
    private static readonly HashSet<DescriptionSymbol> DrawnLeaves =
    [
        DescriptionSymbol.Component, DescriptionSymbol.Interface, DescriptionSymbol.Node, DescriptionSymbol.Rectangle,
        DescriptionSymbol.Folder, DescriptionSymbol.Package, DescriptionSymbol.Frame, DescriptionSymbol.Cloud,
        DescriptionSymbol.Database, DescriptionSymbol.Card, DescriptionSymbol.Actor, DescriptionSymbol.BusinessActor,
        DescriptionSymbol.UseCase, DescriptionSymbol.BusinessUseCase, DescriptionSymbol.Artifact, DescriptionSymbol.Storage,
        DescriptionSymbol.File, DescriptionSymbol.Queue, DescriptionSymbol.Stack, DescriptionSymbol.Agent,
        DescriptionSymbol.Hexagon, DescriptionSymbol.Person, DescriptionSymbol.Boundary, DescriptionSymbol.Control,
        DescriptionSymbol.Entity, DescriptionSymbol.Collections, DescriptionSymbol.Label, DescriptionSymbol.Circle,
    ];

    /// <summary>The container symbols that have their own drawing.</summary>
    private static readonly HashSet<DescriptionSymbol> DrawnContainers =
    [
        DescriptionSymbol.Component, DescriptionSymbol.Package, DescriptionSymbol.Folder, DescriptionSymbol.Rectangle,
        DescriptionSymbol.Node, DescriptionSymbol.Frame, DescriptionSymbol.Cloud, DescriptionSymbol.Database,
        DescriptionSymbol.Card, DescriptionSymbol.Artifact, DescriptionSymbol.Storage, DescriptionSymbol.File,
        DescriptionSymbol.Queue, DescriptionSymbol.Stack, DescriptionSymbol.Hexagon,
    ];

    /// <summary>
    ///     Decides whether a symbol has its own drawing (otherwise it is drawn as a rectangle).
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="container">Whether the element is a container.</param>
    /// <returns>True when it has.</returns>
    public static bool IsDrawn(DescriptionSymbol symbol, bool container) =>
        container ? DrawnContainers.Contains(symbol) : DrawnLeaves.Contains(symbol);

    /// <summary>
    ///     Gets the style element of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The element of the theme that styles it.</returns>
    public static SkinElement StyleOf(DescriptionSymbol symbol) => symbol switch
    {
        DescriptionSymbol.Component => SkinElement.Component,
        DescriptionSymbol.Interface or DescriptionSymbol.Circle => SkinElement.Interface,
        DescriptionSymbol.Node => SkinElement.Node,
        DescriptionSymbol.Rectangle => SkinElement.Rectangle,
        DescriptionSymbol.Folder or DescriptionSymbol.Package => SkinElement.Folder,
        DescriptionSymbol.Frame => SkinElement.Frame,
        DescriptionSymbol.Cloud => SkinElement.Cloud,
        DescriptionSymbol.Database => SkinElement.Database,
        DescriptionSymbol.Card => SkinElement.Card,
        DescriptionSymbol.Actor or DescriptionSymbol.BusinessActor => SkinElement.Actor,
        DescriptionSymbol.UseCase or DescriptionSymbol.BusinessUseCase => SkinElement.UseCase,
        DescriptionSymbol.Artifact => SkinElement.Artifact,
        DescriptionSymbol.Storage => SkinElement.Storage,
        DescriptionSymbol.File => SkinElement.File,
        DescriptionSymbol.Queue => SkinElement.Queue,
        DescriptionSymbol.Stack => SkinElement.Stack,
        DescriptionSymbol.Agent => SkinElement.Agent,
        DescriptionSymbol.Hexagon => SkinElement.Hexagon,
        DescriptionSymbol.Person => SkinElement.Person,
        DescriptionSymbol.Boundary => SkinElement.Boundary,
        DescriptionSymbol.Control => SkinElement.Control,
        DescriptionSymbol.Entity => SkinElement.Entity,
        DescriptionSymbol.Collections => SkinElement.Collections,
        DescriptionSymbol.Label => SkinElement.Label,
        _ => SkinElement.Description,
    };

    /// <summary>
    ///     Builds the image of a leaf element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <returns>The image.</returns>
    public static DescriptionImage Image(DescriptionElement element, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        var painter = new Painter(element, theme, text, sink);
        return element.Symbol switch
        {
            DescriptionSymbol.Component => ComponentImage(painter),
            DescriptionSymbol.Interface or DescriptionSymbol.Circle => InterfaceImage(painter),
            DescriptionSymbol.Node => NodeImage(painter),
            DescriptionSymbol.Folder => FolderImage(painter, packageTitle: false),
            DescriptionSymbol.Package => FolderImage(painter, packageTitle: true),
            DescriptionSymbol.Frame => FrameImage(painter),
            DescriptionSymbol.Cloud => CloudImage(painter),
            DescriptionSymbol.Database => DatabaseImage(painter),
            DescriptionSymbol.Card => CardImage(painter),
            DescriptionSymbol.Actor => ActorImage(painter, business: false),
            DescriptionSymbol.BusinessActor => ActorImage(painter, business: true),
            DescriptionSymbol.UseCase => UseCaseImage(painter, business: false),
            DescriptionSymbol.BusinessUseCase => UseCaseImage(painter, business: true),
            DescriptionSymbol.Artifact => ArtifactImage(painter),
            DescriptionSymbol.Storage => StorageImage(painter),
            DescriptionSymbol.File => FileImage(painter),
            DescriptionSymbol.Queue => QueueImage(painter),
            DescriptionSymbol.Stack => StackImage(painter),
            DescriptionSymbol.Hexagon => HexagonImage(painter),
            DescriptionSymbol.Person => PersonImage(painter),
            DescriptionSymbol.Boundary => SymbolImage(painter, ParticipantKind.Boundary),
            DescriptionSymbol.Control => SymbolImage(painter, ParticipantKind.Control),
            DescriptionSymbol.Entity => SymbolImage(painter, ParticipantKind.Entity),
            DescriptionSymbol.Collections => CollectionsImage(painter),
            DescriptionSymbol.Label => LabelImage(painter),
            _ => RectangleImage(painter),
        };
    }

    /// <summary>
    ///     Builds the image of a container.
    /// </summary>
    /// <param name="element">The container.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <returns>The image.</returns>
    public static ISvekClusterImage ContainerImage(DescriptionElement element, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        var title = element.Text.Length == 0 ? null : element.Text;
        var (fill, line) = ContainerColors(element, theme);
        if (element.Symbol is DescriptionSymbol.Component or DescriptionSymbol.Artifact or DescriptionSymbol.Storage or DescriptionSymbol.File
            or DescriptionSymbol.Queue or DescriptionSymbol.Stack or DescriptionSymbol.Hexagon)
        {
            return new DescriptionContainerImage(element, title, fill, line, theme, text, sink);
        }

        var symbol = element.Symbol switch
        {
            DescriptionSymbol.Package or DescriptionSymbol.Folder => ClusterSymbol.Folder,
            DescriptionSymbol.Node => ClusterSymbol.Node,
            DescriptionSymbol.Frame => ClusterSymbol.Frame,
            DescriptionSymbol.Cloud => ClusterSymbol.Cloud,
            DescriptionSymbol.Database => ClusterSymbol.Database,
            DescriptionSymbol.Card => ClusterSymbol.Card,
            _ => ClusterSymbol.Rectangle,
        };
        return SvekRenderer.ClusterImage(symbol, element.Id, title, element.Stereotypes, theme, text, sink, element.Location, fill, line, stereotypeSize: 14);
    }

    /// <summary>
    ///     Gets the fill and line color of a container: its own colors, else the colors a skin parameter of its symbol
    ///     sets (<c>skinparam nodeBackgroundColor</c> colors node containers too, as PlantUML's style of the symbol
    ///     applies to its groups), else none (the cluster style's).
    /// </summary>
    /// <param name="element">The container.</param>
    /// <param name="theme">The look.</param>
    /// <returns>The fill and the line color, each null for the cluster style's.</returns>
    public static (Rgba32? Fill, Rgba32? Line) ContainerColors(DescriptionElement element, SkinTheme theme)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        var styleElement = StyleOf(element.Symbol);
        var (set, defaults) = (theme[styleElement], SkinTheme.Description[styleElement]);
        var fill = element.Fill ?? (set.Fill != defaults.Fill ? set.Fill : null);
        var line = element.Line ?? (set.Line != defaults.Line ? set.Line : null);
        return (fill, line);
    }

    /// <summary>
    ///     Builds the image of a component (<c>USymbolComponent2</c>): margins of 15, 25, 20, and 10 and the icon.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage ComponentImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 40, p.TextHeight + 30);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x, y, w, h, p.Radius), "description"));
            ComponentIcon(items, x + w, y, (segments, role) => p.Path(segments, role));
            p.DrawTexts(items, x + 15, y + 20);
        });
    }

    /// <summary>
    ///     Adds the UML component icon at the top right of a box: a box of 15 by 10 with two tabs of 4 by 2.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="right">The right of the box.</param>
    /// <param name="top">The top of the box.</param>
    /// <param name="path">Creates a path item with a role.</param>
    internal static void ComponentIcon(List<SceneItem> items, double right, double top, Func<IReadOnlyList<SceneSegment>, string, SceneItem> path)
    {
        items.Add(path(SceneShapes.Rect(right - 20, top + 5, 15, 10), "description-icon"));
        items.Add(path(SceneShapes.Rect(right - 22, top + 7, 4, 2), "description-icon"));
        items.Add(path(SceneShapes.Rect(right - 22, top + 11, 4, 2), "description-icon"));
    }

    /// <summary>
    ///     Builds the image of an interface (<c>USymbolInterface</c> with <c>CircleInterface2</c> and the texts of
    ///     <c>EntityImageDescription</c> drawn around it): a circle of radius 8 in a box of 18, the name 8 below it and
    ///     the stereotypes 8 above it. The box is as tall above the circle as below it, so the circle is the center of
    ///     the layout node, as PlantUML's shield makes it; links end on the circle's box.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage InterfaceImage(Painter p)
    {
        var name = p.Name;
        var stereo = p.Stereotypes;
        var stereoWidth = stereo is null ? 0 : stereo.Width + 2;
        var stereoHeight = stereo?.Height ?? 0;
        var w = Math.Max(InterfaceBox, Math.Max(name.Width, stereoWidth));
        var margin = name.Height + stereoHeight > 0 ? Math.Max(name.Height, stereoHeight) + InterfaceSpace : 0;
        var h = InterfaceBox + (2 * margin);
        var circle = new SceneRect((w - InterfaceBox) / 2, margin, InterfaceBox, InterfaceBox);
        return new DescriptionImage(w, h, new SvekClip(ClipShape.Ellipse, circle), (x, y) =>
        {
            var items = new List<SceneItem>
            {
                p.Path(SceneShapes.Ellipse(x + (w / 2), y + margin + (InterfaceBox / 2), InterfaceRadius, InterfaceRadius), "description"),
            };
            if (stereo is not null)
            {
                items.AddRange(p.DrawText(stereo, x + ((w - stereo.Width) / 2), y + margin - InterfaceSpace - stereoHeight, TextAlign.Center, "description-stereotype"));
            }

            items.AddRange(p.DrawText(name, x + ((w - name.Width) / 2), y + margin + InterfaceBox + InterfaceSpace, TextAlign.Left, "description-name"));
            return items;
        });
    }

    /// <summary>
    ///     Builds the image of a rectangle (<c>USymbolRectangle</c>): margins of 10. Every symbol without its own drawing
    ///     is drawn this way.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage RectangleImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 20);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x, y, w, h, p.Radius), "description"));
            p.DrawTexts(items, x + 10, y + 10);
        });
    }

    /// <summary>
    ///     The shared state of the painting of one element: its style, its fonts, its texts, and the text layout.
    /// </summary>
    private sealed class Painter
    {
        /// <summary>The element.</summary>
        private readonly DescriptionElement _element;

        /// <summary>The fill, or null.</summary>
        private readonly Rgba32? _fill;

        /// <summary>The stroke, or null.</summary>
        private readonly SceneStroke? _stroke;

        /// <summary>
        ///     Initializes a painter: lays out the stereotypes and the name or description of an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="theme">The look.</param>
        /// <param name="text">The text layout.</param>
        /// <param name="sink">The report sink, or null.</param>
        public Painter(DescriptionElement element, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
        {
            _element = element;
            Style = theme[StyleOf(element.Symbol)];
            _fill = element.Fill ?? Style.Fill;
            var line = element.Line ?? Style.Line ?? SkinTheme.LineColor;
            _stroke = Style.LineWidth > 0 ? new SceneStroke(line, Style.LineWidth) : null;
            Radius = Style.RoundCorner / 2;
            var color = element.TextColor ?? Style.FontColor;
            Name = text.Layout(element.Text, new SceneFont(Style.FontFamily, Style.FontSize, Style.Bold, Style.Italic), color, 0, sink, element.Location);
            Bold = text.Layout(element.Name, new SceneFont(Style.FontFamily, Style.FontSize, true, Style.Italic), color, 0, sink, element.Location);
            Stereotypes = element.Stereotypes.Count == 0
                ? null
                : text.Layout(string.Join("\n", element.Stereotypes.Select(s => "\u00AB" + s + "\u00BB")), new SceneFont(Style.FontFamily, Style.FontSize, false, true), color, 0, sink, element.Location);
        }

        /// <summary>Gets the style.</summary>
        public ElementStyle Style { get; }

        /// <summary>Gets the corner radius of the outline.</summary>
        public double Radius { get; }

        /// <summary>Gets the fill, or null.</summary>
        public Rgba32? Fill => _fill;

        /// <summary>Gets the stroke, or null.</summary>
        public SceneStroke? Stroke => _stroke;

        /// <summary>Gets the laid-out name or description.</summary>
        public TextBlock Name { get; }

        /// <summary>Gets the laid-out name in bold (the title in the tab of a package).</summary>
        public TextBlock Bold { get; }

        /// <summary>Gets the laid-out stereotypes, or null.</summary>
        public TextBlock? Stereotypes { get; }

        /// <summary>Gets or sets the block below the stereotypes: the name, or nothing for a package.</summary>
        public TextBlock? Label { get; set; }

        /// <summary>Gets the width of the stereotypes with their margins.</summary>
        public double StereotypeWidth => Stereotypes is null ? 0 : Stereotypes.Width + 2;

        /// <summary>Gets the width of the texts: the stereotypes (with their margins) and the label, centered on each other.</summary>
        public double TextWidth => Math.Max(StereotypeWidth, (Label ?? Name).Width);

        /// <summary>Gets the height of the texts: the stereotypes over the label.</summary>
        public double TextHeight => (Stereotypes?.Height ?? 0) + (Label ?? Name).Height;

        /// <summary>
        ///     Creates an image of a box that clips to its rectangle.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="paint">Adds the items with the top-left corner at a point.</param>
        /// <returns>The image.</returns>
        public static DescriptionImage Box(double width, double height, Action<double, double, List<SceneItem>> paint) =>
            new(width, height, new SvekClip(ClipShape.Rectangle, new SceneRect(0, 0, width, height)), (x, y) =>
            {
                var items = new List<SceneItem>();
                paint(x, y, items);
                return items;
            });

        /// <summary>
        ///     Creates a path item that carries the element's id, filled and outlined in its colors.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="role">The role.</param>
        /// <param name="filled">Whether the path is filled.</param>
        /// <returns>The item.</returns>
        public ScenePath Path(IReadOnlyList<SceneSegment> segments, string role, bool filled = true) =>
            new(segments) { Role = role, Id = _element.Id, Location = _element.Location, Fill = filled ? _fill : null, Stroke = _stroke };

        /// <summary>
        ///     Gives a path built elsewhere (a stick figure) a role and the element's id and location.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="role">The role.</param>
        /// <returns>The item.</returns>
        public ScenePath Own(ScenePath path, string role) => path with { Role = role, Id = _element.Id, Location = _element.Location };

        /// <summary>
        ///     Draws a block with its items carrying the element's id.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="x">The left.</param>
        /// <param name="y">The top.</param>
        /// <param name="align">The alignment of its lines.</param>
        /// <param name="role">The role.</param>
        /// <returns>The items.</returns>
        public IEnumerable<SceneItem> DrawText(TextBlock block, double x, double y, TextAlign align, string role) =>
            block.Draw(x, y, align, role, _element.Location).Select(t => t with { Id = _element.Id });

        /// <summary>
        ///     Draws the stereotypes and the label with the top-left corner of their block at a point: the stereotypes
        ///     centered over the label (with a margin of 1 on each side), the label's lines aligned left.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="x">The left of the texts.</param>
        /// <param name="y">The top of the texts.</param>
        public void DrawTexts(List<SceneItem> items, double x, double y)
        {
            var width = TextWidth;
            if (Stereotypes is not null)
            {
                items.AddRange(DrawText(Stereotypes, x + ((width - StereotypeWidth) / 2) + 1, y, TextAlign.Center, "description-stereotype"));
                y += Stereotypes.Height;
            }

            var label = Label ?? Name;
            items.AddRange(DrawText(label, x + ((width - label.Width) / 2), y, TextAlign.Left, "description-name"));
        }
    }
}
