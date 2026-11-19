// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <summary>
///     The image of one entity of a class or object diagram: its size, its rectangular outline, and the painter of
///     its items.
/// </summary>
/// <remarks>
///     The image is measured once, when the diagram is lowered for a call, and is immutable afterwards.
/// </remarks>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
/// <param name="draw">The painter: the items with the top-left corner at a point.</param>
internal sealed class ClassImage(double width, double height, Func<double, double, IReadOnlyList<SceneItem>> draw) : ISvekEntityImage
{
    /// <inheritdoc/>
    public double Width { get; } = width;

    /// <inheritdoc/>
    public double Height { get; } = height;

    /// <inheritdoc/>
    public SvekClip Clip => new(ClipShape.Rectangle, new SceneRect(0, 0, Width, Height));

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(double x, double y) => draw(x, y);
}

/// <summary>
///     Measures and paints the entities of class and object diagrams as PlantUML does (<c>svek/image/EntityImageClass</c>,
///     <c>EntityImageClassHeader</c>, <c>HeaderLayout</c>, <c>klimt/shape/CircledCharacter</c>,
///     <c>cucadiagram/MethodsOrFieldsArea</c>, <c>BodyEnhanced1</c>, <c>skin/VisibilityModifier</c>,
///     <c>EntityImageObject</c>, and <c>EntityImageMap</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Class.</b> A rounded rectangle (corners of 5) filled <c>#F1F1F1</c> and outlined <c>#181818</c> half a
///         pixel wide: a header with the circled character of its kind (a circle of radius 11 in the spot color, the
///         letter in bold monospaced 17), its stereotypes (italic 12) above its name (14, italic for an abstract class
///         and an interface), and its generic parameters in a dashed white box over the top-right corner; then the
///         fields and the methods, each compartment below a line, 6 from the sides and 4 from the lines, with a
///         visibility icon before each member that has one (a square, diamond, triangle, or circle, hollow for a
///         field and filled for a method). A body with separators (<c>--</c>, <c>..</c>, <c>==</c>, <c>__</c>, with
///         optional titles) is drawn in the blocks it separates instead.
///     </para>
///     <para>
///         <b>Object and map.</b> An object has a centered name and its fields below a line, without a spot; a map
///         has a table of its keys (centered) and values, one row per entry.
///     </para>
///     <para>
///         Every item carries the entity's id and one of the roles
///         <c>class</c>, <c>class-spot</c>, <c>class-spot-text</c>, <c>class-stereotype</c>, <c>class-name</c>,
///         <c>class-generic</c>, <c>class-generic-text</c>, <c>class-separator</c>, <c>class-separator-title</c>,
///         <c>class-icon</c>, and <c>class-member</c> (objects and maps use the same roles, with <c>map-separator</c>,
///         <c>map-key</c>, and <c>map-value</c> for their table). The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class ClassRenderer
{
    /// <summary>The margin of a compartment's members from the sides of the box (<c>BodyEnhanced1.getMarginX</c>).</summary>
    public const double MarginX = 6;

    /// <summary>The margin of a compartment's members from its line and from the next one.</summary>
    public const double MarginY = 4;

    /// <summary>The family of the circled character's font (PlantUML's <c>CIRCLED_CHARACTER</c>: monospaced bold 17).</summary>
    public const string SpotFontFamily = "Monospaced";

    /// <summary>The size of the circled character's font.</summary>
    public const double SpotFontSize = 17;

    /// <summary>
    ///     Builds the image of an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="portions">What is shown of it.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <returns>The image.</returns>
    public static ClassImage Image(ClassEntity entity, ClassPortions portions, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        var context = new Painter(entity, theme, text, sink);
        return entity.Kind switch
        {
            ClassKind.Object => ObjectImage(context, portions),
            ClassKind.Map => MapImage(context, portions),
            _ => ClassBoxImage(context, portions),
        };
    }

    /// <summary>
    ///     Gets the spot color of a kind (the <c>spot</c> section of PlantUML's default skin).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The color.</returns>
    public static Rgba32 SpotColor(ClassKind kind) => kind switch
    {
        ClassKind.AbstractClass => Rgb(0xA9DCDF),
        ClassKind.Interface => Rgb(0xB4A7E5),
        ClassKind.Enum => Rgb(0xEB937F),
        ClassKind.Annotation => Rgb(0xE3664A),
        ClassKind.Exception => Rgb(0xD94321),
        ClassKind.Metaclass => Rgb(0xCCCCCC),
        ClassKind.Stereotype => Rgb(0xFF77FF),
        ClassKind.DataClass => Rgb(0x7E57C2),
        ClassKind.Record => Rgb(0xFF8F00),
        ClassKind.Struct or ClassKind.Protocol => SkinTheme.White,
        _ => Rgb(0xADD1B2),
    };

    /// <summary>
    ///     Gets the circled character of a kind (<c>EntityImageClassHeader.getCircledChar</c>).
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The character.</returns>
    public static char SpotCharacter(ClassKind kind) => kind switch
    {
        ClassKind.AbstractClass => 'A',
        ClassKind.Interface => 'I',
        ClassKind.Enum or ClassKind.Entity => 'E',
        ClassKind.Annotation => '@',
        ClassKind.Exception => 'X',
        ClassKind.Struct or ClassKind.Stereotype => 'S',
        ClassKind.Protocol => 'P',
        ClassKind.Metaclass => 'M',
        ClassKind.DataClass => 'D',
        ClassKind.Record => 'R',
        _ => 'C',
    };

    /// <summary>
    ///     Gets the line and fill colors of a visibility icon (the <c>visibilityIcon</c> section of PlantUML's default
    ///     skin).
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <returns>The line color and the fill of a method's icon.</returns>
    public static (Rgba32 Line, Rgba32 Fill) IconColors(MemberVisibility visibility) => visibility switch
    {
        MemberVisibility.Private => (Rgb(0xC82930), Rgb(0xF24D5C)),
        MemberVisibility.Protected => (Rgb(0xB38D22), Rgb(0xFFFF44)),
        MemberVisibility.Package => (Rgb(0x1963A0), Rgb(0x4177AF)),
        MemberVisibility.Public => (Rgb(0x038048), Rgb(0x84BE84)),
        _ => (SkinTheme.Black, SkinTheme.Black),
    };

    /// <summary>
    ///     Creates an opaque color from its hexadecimal value.
    /// </summary>
    /// <param name="rgb">The value <c>0xRRGGBB</c>.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Rgb(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    /// <summary>
    ///     The shared state of the painting of one entity: its style, its fonts, and the text layout.
    /// </summary>
    private sealed class Painter
    {
        /// <summary>
        ///     Initializes a painter.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="theme">The look.</param>
        /// <param name="text">The text layout.</param>
        /// <param name="sink">The report sink, or null.</param>
        public Painter(ClassEntity entity, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
        {
            (Entity, Theme, Text, Sink) = (entity, theme, text, sink);
            Style = theme[entity.IsObjectLike ? SkinElement.Object : SkinElement.Class];
            StereotypeStyle = theme[SkinElement.ClassStereotype];
            MemberStyle = theme[SkinElement.ClassAttribute];
            Fill = entity.Fill ?? Style.Fill;
            LineColor = entity.Line ?? Style.Line ?? SkinTheme.LineColor;
        }

        /// <summary>Gets the entity.</summary>
        public ClassEntity Entity { get; }

        /// <summary>Gets the look.</summary>
        public SkinTheme Theme { get; }

        /// <summary>Gets the text layout.</summary>
        public TextLayout Text { get; }

        /// <summary>Gets the report sink, or null.</summary>
        public PlantUmlReportSink? Sink { get; }

        /// <summary>Gets the style of the box.</summary>
        public ElementStyle Style { get; }

        /// <summary>Gets the style of the stereotypes and generic parameters.</summary>
        public ElementStyle StereotypeStyle { get; }

        /// <summary>Gets the style of the members, the separator titles, and the map entries.</summary>
        public ElementStyle MemberStyle { get; }

        /// <summary>Gets the fill of the box, or null.</summary>
        public Rgba32? Fill { get; }

        /// <summary>Gets the line color of the box.</summary>
        public Rgba32 LineColor { get; }


        /// <summary>Gets the id the items carry.</summary>
        public string Id => Entity.Id;

        /// <summary>Gets the location the items carry.</summary>
        public PlantUmlLocation Location => Entity.Location;

        /// <summary>
        ///     Lays out a text in the box's font (or a variant).
        /// </summary>
        /// <param name="value">The text.</param>
        /// <param name="style">The style of the font.</param>
        /// <param name="italic">Whether the text is italic regardless of the style.</param>
        /// <returns>The block, in the entity's text color (<c>#back;text:color</c>) or the style's.</returns>
        public TextBlock Layout(string value, ElementStyle style, bool italic = false)
        {
            var font = new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic || italic);
            return Text.Layout(value, font, Entity.TextColor ?? style.FontColor, 0, Sink, Location);
        }

        /// <summary>
        ///     Draws a block with its items carrying the entity's id.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="x">The left.</param>
        /// <param name="y">The top.</param>
        /// <param name="align">The alignment of its lines.</param>
        /// <param name="role">The role.</param>
        /// <returns>The items.</returns>
        public IEnumerable<SceneItem> DrawText(TextBlock block, double x, double y, TextAlign align, string role) =>
            block.Draw(x, y, align, role, Location).Select(t => t with { Id = Id });

        /// <summary>
        ///     Creates a path item that carries the entity's id.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="role">The role.</param>
        /// <param name="fill">The fill, or null.</param>
        /// <param name="stroke">The stroke, or null.</param>
        /// <returns>The item.</returns>
        public ScenePath Path(IReadOnlyList<SceneSegment> segments, string role, Rgba32? fill, SceneStroke? stroke) =>
            new(segments) { Role = role, Id = Id, Location = Location, Fill = fill, Stroke = stroke };

        /// <summary>
        ///     Creates the stroke of the box's lines.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="dash">The dash pattern, or null.</param>
        /// <returns>The stroke, or null when the width is zero.</returns>
        public SceneStroke? Stroke(double width, IReadOnlyList<double>? dash = null) =>
            width > 0 ? new SceneStroke(LineColor, width) { Dash = dash } : null;
    }
}
