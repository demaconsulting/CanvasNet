// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     A point of the shape geometry.
/// </summary>
/// <param name="X">The x.</param>
/// <param name="Y">The y.</param>
internal readonly record struct ShapePoint(double X, double Y);

/// <summary>
///     The box of a placed node as the intersect functions read it (Mermaid's <c>node.x</c>, <c>node.y</c>,
///     <c>node.width</c> and <c>node.height</c> when an edge is cut).
/// </summary>
/// <param name="X">The x of the node's center.</param>
/// <param name="Y">The y of the node's center.</param>
/// <param name="Width">The width the layout gave the node.</param>
/// <param name="Height">The height the layout gave the node.</param>
internal readonly record struct NodeBox(double X, double Y, double Width, double Height);

/// <summary>
///     The intersect function of a shape (Mermaid's <c>node.intersect</c>): where the line from the node's center to a
///     point leaves the shape.
/// </summary>
/// <param name="node">The node's box at the time of the call.</param>
/// <param name="point">The point outside.</param>
/// <returns>The point on the outline.</returns>
internal delegate ShapePoint ShapeIntersect(NodeBox node, ShapePoint point);

/// <summary>
///     The paint of the drawn parts of a node: the node rule of the style sheet (<c>.node rect</c> and its siblings), the
///     node's own styles (<c>classDef</c> and <c>style</c>, applied inline with <c>!important</c>) and the theme colors
///     some shapes use directly.
/// </summary>
/// <param name="Css">The node rule of the style sheet.</param>
/// <param name="User">The node's own non-label styles.</param>
/// <param name="Theme">The theme colors.</param>
internal sealed record NodePaint(ShapePaint Css, StyleDeclarationSet User, ShapeTheme Theme);

/// <summary>
///     The theme variables the shapes read directly.
/// </summary>
/// <param name="MainBkg">The node background (<c>mainBkg</c>).</param>
/// <param name="NodeBorder">The node border (<c>nodeBorder</c>).</param>
/// <param name="LineColor">The line color (<c>lineColor</c>).</param>
/// <param name="ClusterBorder">The subgraph border (<c>clusterBorder</c>).</param>
/// <param name="EdgeLabelBackground">The edge label background (<c>edgeLabelBackground</c>).</param>
internal sealed record ShapeTheme(Rgba32? MainBkg, Rgba32? NodeBorder, Rgba32? LineColor, Rgba32? ClusterBorder, Rgba32? EdgeLabelBackground)
{
    /// <summary>
    ///     Reads the colors from the theme variables of a configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The colors (null for a variable that is missing or not a color).</returns>
    public static ShapeTheme From(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Rgba32? Color(string name) =>
            config.ThemeVariable(name) is { } value && StyleDeclarations.TryParsePaint(value, out var paint) ? paint : null;
        return new ShapeTheme(Color("mainBkg"), Color("nodeBorder"), Color("lineColor"), Color("clusterBorder"), Color("edgeLabelBackground"));
    }
}

/// <summary>
///     The settings of one rendering that every node shape reads.
/// </summary>
/// <remarks>
///     <see cref="MeasureLabel"/> is the label measurement the shapes call (Mermaid's <c>labelHelper</c> measures the
///     label in the browser); tests replace it with a fixed box to check the shape geometry alone.
/// </remarks>
internal sealed class ShapeContext
{
    /// <summary>Gets the effective configuration.</summary>
    public required MermaidConfig Config { get; init; }

    /// <summary>Gets the style sheet of the diagram.</summary>
    public required UnifiedStyles Styles { get; init; }

    /// <summary>Gets the label measurement.</summary>
    public required Func<LabelRequest, LabelLayout> MeasureLabel { get; init; }

    /// <summary>Gets the pictures of the rendering, or null when none can be loaded.</summary>
    public NodeImages? Images { get; init; }

    /// <summary>Gets the report sink of the call, or null.</summary>
    public MermaidReportSink? Sink { get; init; }

    /// <summary>Gets the theme colors.</summary>
    public ShapeTheme Theme => _theme ??= ShapeTheme.From(Config);

    /// <summary>The theme colors, read once.</summary>
    private ShapeTheme? _theme;
}

/// <summary>
///     A drawn node: its size for the layout, its items around its center, its label and its intersect function.
/// </summary>
/// <param name="Handler">The drawing function that drew it (Mermaid's shape handler name).</param>
/// <param name="Bounds">The box of everything drawn, around the node's center (Mermaid's <c>getBBox</c> of the node group).</param>
/// <param name="Items">The items in drawing order, around the node's center.</param>
/// <param name="Label">The measured label.</param>
/// <param name="LabelOffset">The translation of the label's group (the label's box is <see cref="LabelLayout.Box"/> moved by it).</param>
/// <param name="Intersect">The intersect function.</param>
internal sealed record ShapeResult(
    string Handler,
    SceneRect Bounds,
    IReadOnlyList<SceneItem> Items,
    LabelLayout Label,
    ShapePoint LabelOffset,
    ShapeIntersect Intersect)
{
    /// <summary>Gets the width the layout gives the node (Mermaid's <c>insertMeasuredNode</c>).</summary>
    public double Width => Bounds.Width;

    /// <summary>Gets the height the layout gives the node.</summary>
    public double Height => Bounds.Height;

    /// <summary>Gets the box of the label around the node's center.</summary>
    public SceneRect LabelBox => Label.Box.Offset(LabelOffset.X, LabelOffset.Y);
}

/// <summary>
///     Mermaid's node shapes: the 53 documented shapes with their aliases, the legacy bracket shapes and the undocumented
///     shapes a flowchart accepts, each sized from its label and padding, drawn in the classic look and given its
///     intersect function, as the shapes of <c>rendering-elements/shapes/</c> do.
/// </summary>
/// <remarks>
///     <para>
///         <b>Sizing.</b> Each shape measures its label first (through <see cref="ShapeContext.MeasureLabel"/>), then
///         computes its geometry from the label box and the node padding with Mermaid's formulas. The size the layout
///         uses is the box of everything the shape draws, label included, as Mermaid's <c>insertMeasuredNode</c> takes
///         it from <c>getBBox</c>.
///     </para>
///     <para>
///         <b>Classic look.</b> The geometry is the one the classic look draws: the shapes that Mermaid draws through
///         rough.js with no roughness are drawn as their exact polygons, arcs and circles, so a rough circle is a true
///         circle (rough.js approximates it with a curve that can be up to 0.1 % wider).
///     </para>
///     <para>
///         <b>Paint.</b> The parts that Mermaid styles with the node rule and the node's own styles get
///         <see cref="NodePaint"/>'s cascade; the parts with their own paint (accents, glyphs, indicators) get it as
///         Mermaid's inline styles and style sheet give it.
///     </para>
///     <para>
///         <b>Icons and images.</b> Icons are not drawn (the model reports them); an icon shape keeps its frame and its
///         size, the icon taken as a square of its size. An image shape draws its picture through
///         <see cref="NodeImages"/>.
///     </para>
///     <para>
///         <b>The legacy <c>ellipse</c>.</b> The flowchart syntax <c>A(-text-)</c> asks for a shape named
///         <c>ellipse</c>, which Mermaid's shape registry no longer has (Mermaid fails to render it). This library draws
///         it as the ellipse earlier Mermaid versions drew: the label with its padding, inside an ellipse whose radii are
///         half the padded box, with an elliptical intersect function.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class ShapeLibrary
{
    /// <summary>The stroke width of the outline rough.js draws when the node rule does not apply (image and icon frames).</summary>
    internal const double RoughStrokeWidth = 1.3;

    /// <summary>
    ///     Draws a node.
    /// </summary>
    /// <param name="node">The node (not a cluster).</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the shape is not one this version draws.</exception>
    public static ShapeResult Draw(LayoutNode node, ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);
        var handler = node.Shape == "ellipse" ? "ellipse" : ShapeNames.HandlerOf(node.Shape);
        var input = new ShapeInput(node, context, handler ?? node.Shape);
        return handler switch
        {
            "squareRect" => SquareRect(input),
            "roundedRect" => RoundedRect(input),
            "state" => State(input),
            "composite" => Composite(input),
            "text" => Text(input),
            "datastore" => Datastore(input),
            "stadium" => Stadium(input),
            "subroutine" => Subroutine(input),
            "cylinder" => Cylinder(input),
            "circle" => Circle(input),
            "doublecircle" => DoubleCircle(input),
            "question" => Question(input),
            "hexagon" => Hexagon(input),
            "lean_right" => LeanRight(input),
            "lean_left" => LeanLeft(input),
            "trapezoid" => Trapezoid(input),
            "inv_trapezoid" => InvTrapezoid(input),
            "rect_left_inv_arrow" => RectLeftInvArrow(input),
            "ellipse" => Ellipse(input),
            "choice" => Choice(input),
            "note" => Note(input),
            "anchor" => Anchor(input),
            "card" => Card(input),
            "shadedProcess" => ShadedProcess(input),
            "stateStart" => StateStart(input),
            "stateEnd" => StateEnd(input),
            "forkJoin" => ForkJoin(input),
            "hourglass" => Hourglass(input),
            "curlyBraceLeft" => CurlyBraceLeft(input),
            "curlyBraceRight" => CurlyBraceRight(input),
            "curlyBraces" => CurlyBraces(input),
            "lightningBolt" => LightningBolt(input),
            "waveEdgedRectangle" => WaveEdgedRectangle(input),
            "halfRoundedRectangle" => HalfRoundedRectangle(input),
            "tiltedCylinder" => TiltedCylinder(input),
            "linedCylinder" => LinedCylinder(input),
            "curvedTrapezoid" => CurvedTrapezoid(input),
            "dividedRectangle" => DividedRectangle(input),
            "triangle" => Triangle(input),
            "windowPane" => WindowPane(input),
            "filledCircle" => FilledCircle(input),
            "trapezoidalPentagon" => TrapezoidalPentagon(input),
            "flippedTriangle" => FlippedTriangle(input),
            "slopedRect" => SlopedRect(input),
            "multiWaveEdgedRectangle" => MultiWaveEdgedRectangle(input),
            "multiRect" => MultiRect(input),
            "bowTieRect" => BowTieRect(input),
            "crossedCircle" => CrossedCircle(input),
            "taggedWaveEdgedRectangle" => TaggedWaveEdgedRectangle(input),
            "taggedRect" => TaggedRect(input),
            "waveRectangle" => WaveRectangle(input),
            "linedWaveEdgedRect" => LinedWaveEdgedRect(input),
            "folder" => Folder(input),
            "bucket" => Bucket(input),
            "consoleWindow" => ConsoleWindow(input),
            "browser" => Browser(input),
            "person" => Person(input),
            "bang" => Bang(input),
            "cloud" => Cloud(input),
            "collapsedGroup" => CollapsedGroup(input),
            "labelRect" => LabelRect(input),
            "imageSquare" => ImageSquare(input),
            "icon" => Icon(input),
            "iconSquare" => IconSquare(input),
            "iconCircle" => IconCircle(input),
            "iconRounded" => IconRounded(input),
            _ => throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"the shape '{node.Shape}' is not drawn by this version",
                node.Location,
                $"use one of the documented shapes, for example '{ShapeNames.Nearest(node.Shape)}'"),
        };
    }

    /// <summary>
    ///     Splits a node's styles as Mermaid's <c>styles2String</c> does: the compiled class styles, then the inline
    ///     styles, then the label style, each <c>key:value</c> (a later key wins), into the label styles (text properties)
    ///     and the node styles (everything else).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The label and node style items, as <c>key:value</c>.</returns>
    internal static (List<string> Label, List<string> Node) SplitStyles(LayoutNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var labelStyleItems = node.LabelStyle.Split(';').Select(item => item.Trim()).Where(item => item.Length > 0);
        return SplitStyles(node.CssCompiledStyles.Concat(node.CssStyles ?? []).Concat(labelStyleItems));
    }

    /// <summary>
    ///     Splits style items as Mermaid's <c>styles2String</c> does (each <c>key:value</c>, a later key wins) into the label
    ///     styles (text properties) and the other styles.
    /// </summary>
    /// <param name="styles">The style items in cascade order.</param>
    /// <returns>The label and other style items, as <c>key:value</c>.</returns>
    internal static (List<string> Label, List<string> Node) SplitStyles(IEnumerable<string> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        var map = new List<KeyValuePair<string, string>>();
        foreach (var style in styles)
        {
            // style.split(':') keeps the first two parts only
            var parts = style.Split(':');
            var key = parts[0].Trim();
            var value = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            var index = map.FindIndex(entry => entry.Key == key);
            if (index >= 0)
            {
                map[index] = new KeyValuePair<string, string>(key, value);
            }
            else
            {
                map.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        var label = new List<string>();
        var nodeStyles = new List<string>();
        foreach (var (key, value) in map)
        {
            (IsLabelStyle(key) ? label : nodeStyles).Add(key + ":" + value);
        }

        return (label, nodeStyles);
    }

    /// <summary>
    ///     Gets the font a label starts from: the style sheet's text rule with the element's own label styles
    ///     (<c>font-family</c>, <c>font-size</c>, <c>font-weight</c>, <c>font-style</c> and <c>color</c>) over it.
    /// </summary>
    /// <param name="labelStyles">The element's label styles.</param>
    /// <param name="css">The text rule of the style sheet.</param>
    /// <returns>The font.</returns>
    internal static LabelFont LabelFontOf(StyleDeclarationSet labelStyles, TextPaint css)
    {
        ArgumentNullException.ThrowIfNull(labelStyles);
        ArgumentNullException.ThrowIfNull(css);
        var family = labelStyles["font-family"] ?? css.FontFamily;
        var size = labelStyles["font-size"] is { } value && StyleDeclarations.TryParseFontSize(value, css.FontSize, out var length)
            ? length
            : css.FontSize;
        var bold = labelStyles["font-weight"] is { } weight && StyleDeclarations.TryParseBold(weight, out var isBold) && isBold;
        var italic = labelStyles["font-style"] is { } style && StyleDeclarations.TryParseItalic(style, out var isItalic) && isItalic;
        return new LabelFont(family, size, bold, italic, labelStyles.Paint("color") ?? css.Color);
    }

    /// <summary>
    ///     Gets whether a style key is a text property (Mermaid's <c>isLabelStyle</c>).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether it is.</returns>
    internal static bool IsLabelStyle(string key) => key is "color" or "font-size" or "font-family" or "font-weight" or "font-style"
        or "text-decoration" or "text-align" or "text-transform" or "line-height" or "letter-spacing" or "word-spacing"
        or "text-shadow" or "text-overflow" or "white-space" or "word-wrap" or "word-break" or "overflow-wrap" or "hyphens";

    /// <summary>
    ///     A node being drawn: the node, the settings, its styles and its label measurement.
    /// </summary>
    private sealed class ShapeInput
    {
        /// <summary>The node's own label styles.</summary>
        private readonly StyleDeclarationSet _labelStyles;

        /// <summary>
        ///     Initializes the input of a node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="context">The settings.</param>
        /// <param name="handler">The drawing function.</param>
        public ShapeInput(LayoutNode node, ShapeContext context, string handler)
        {
            Node = node;
            Context = context;
            Handler = handler;
            var (label, nodeStyles) = SplitStyles(node);
            _labelStyles = StyleDeclarations.ParseItems(label, node.Location, context.Sink);
            User = StyleDeclarations.ParseItems(nodeStyles, node.Location, context.Sink);
            Paint = new NodePaint(context.Styles.Node, User, context.Theme);
        }

        /// <summary>Gets the node.</summary>
        public LayoutNode Node { get; }

        /// <summary>Gets the settings.</summary>
        public ShapeContext Context { get; }

        /// <summary>Gets the drawing function.</summary>
        public string Handler { get; }

        /// <summary>Gets the node's own non-label styles.</summary>
        public StyleDeclarationSet User { get; }

        /// <summary>Gets the node's own label styles.</summary>
        public StyleDeclarationSet LabelStyles => _labelStyles;

        /// <summary>Gets the paint of the node.</summary>
        public NodePaint Paint { get; }

        /// <summary>Gets the node padding (<c>node.padding ?? 0</c>).</summary>
        public double Padding => Node.Padding;

        /// <summary>Gets a value indicating whether HTML labels are used.</summary>
        public bool HtmlLabels => Context.Config.HtmlLabels;

        /// <summary>
        ///     Measures the node's label as <c>labelHelper</c> does.
        /// </summary>
        /// <param name="width">The width the label wraps at (<c>node.width</c> when the shape set it), or null for the wrapping width.</param>
        /// <param name="assetShape">Whether the shape is an icon or image shape (the label gets a background and padding).</param>
        /// <param name="label">The label text, or null for the node's.</param>
        /// <returns>The measured label.</returns>
        public LabelLayout MeasureLabel(double? width = null, bool assetShape = false, string? label = null)
        {
            var css = Context.Styles.NodeLabel;
            var wrap = width is > 0 ? width.Value : Context.Config.Flowchart.WrappingWidth;
            var request = new LabelRequest(label ?? Node.Label ?? string.Empty, LabelFontOf(_labelStyles, css))
            {
                Markdown = Node.LabelType == "markdown",
                HtmlLabels = HtmlLabels,
                Width = wrap > 0 ? wrap : 200,
                AddBackground = assetShape && (Node.Icon is not null || Node.Img is not null),
                ParagraphPadding = assetShape ? 2 : 0,
                MarkdownAutoWrap = Context.Config.MarkdownAutoWrap,
                RootFontSize = css.FontSize,
                ConfigFontSize = Context.Config.FontSize,
                Location = Node.Location,
            };
            return Context.MeasureLabel(request);
        }

        /// <summary>
        ///     Gets the default translation of a label (<c>labelHelper</c>'s centering).
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="centerLabel">Whether the node asks for a centered label (<c>node.centerLabel</c>).</param>
        /// <returns>The translation.</returns>
        public static ShapePoint DefaultLabelOffset(LabelLayout label, bool centerLabel = false) =>
            label.IsHtml || centerLabel
                ? new ShapePoint(-label.Width / 2, -label.Height / 2)
                : new ShapePoint(0, -label.Height / 2);
    }
}
