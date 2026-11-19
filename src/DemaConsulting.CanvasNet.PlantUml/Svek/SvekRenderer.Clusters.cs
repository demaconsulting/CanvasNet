// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The image of a cluster with one of PlantUML's container symbols (<c>decoration/symbol/USymbol*.java</c>, drawn
///     "big" around members): its outline, its title, and its stereotypes.
/// </summary>
/// <remarks>
///     The header band is the height of the title and the stereotypes (rounded down, plus the extra height of the
///     symbol: 5 for a node, 15 for a database) plus 11, as PlantUML's Smetana pipeline reserves it
///     (<c>ClusterHeader</c> and the label of the cluster); the members are 16 from the outline (20 for a node, whose
///     3D side needs room). An image is immutable and safe for concurrent use.
/// </remarks>
internal sealed class SvekClusterImage : ISvekClusterImage
{
    /// <summary>The symbol.</summary>
    private readonly ClusterSymbol _symbol;

    /// <summary>The id the items carry.</summary>
    private readonly string _id;

    /// <summary>The title, or null.</summary>
    private readonly TextBlock? _title;

    /// <summary>The stereotypes, or null.</summary>
    private readonly TextBlock? _stereotype;

    /// <summary>The fill, or null.</summary>
    private readonly Rgba32? _fill;

    /// <summary>The stroke of the outline, or null.</summary>
    private readonly SceneStroke? _stroke;

    /// <summary>The corner diameter.</summary>
    private readonly double _roundCorner;

    /// <summary>The location the items carry.</summary>
    private readonly PlantUmlLocation _location;

    /// <summary>
    ///     Initializes an image.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="id">The id of the cluster.</param>
    /// <param name="title">The laid-out title, or null.</param>
    /// <param name="stereotype">The laid-out stereotypes, or null.</param>
    /// <param name="style">The style of the outline.</param>
    /// <param name="fill">The fill, or null for the style's.</param>
    /// <param name="line">The line color, or null for the style's.</param>
    /// <param name="location">The location of the cluster.</param>
    public SvekClusterImage(ClusterSymbol symbol, string id, TextBlock? title, TextBlock? stereotype, ElementStyle style, Rgba32? fill, Rgba32? line, PlantUmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(style);
        (_symbol, _id, _title, _stereotype, _location) = (symbol, id, title, stereotype, location);
        _fill = fill ?? style.Fill;
        var color = line ?? style.Line;
        _stroke = color is { } c && style.LineWidth > 0 ? new SceneStroke(c, style.LineWidth) : null;
        _roundCorner = style.RoundCorner;
        var (titleW, titleH) = (title?.Width ?? 0, title?.Height ?? 0);
        var (stereoW, stereoH) = (stereotype?.Width ?? 0, stereotype?.Height ?? 0);
        var (suppW, suppH) = symbol switch
        {
            ClusterSymbol.Node => (60.0, 5.0),
            ClusterSymbol.Database => (0.0, 15.0),
            _ => (0.0, 0.0),
        };
        Padding = symbol switch
        {
            ClusterSymbol.None => 0,
            ClusterSymbol.Node => 20,
            _ => 16,
        };
        var labeled = symbol != ClusterSymbol.None && (titleW > 0 || stereoW > 0);
        HeaderHeight = labeled ? Math.Floor(titleH + stereoH + suppH) + 11 : 0;
        MinWidth = labeled ? Math.Floor(Math.Max(titleW, stereoW)) + suppW + (2 * Padding) : 0;
    }

    /// <inheritdoc/>
    public double HeaderHeight { get; }

    /// <inheritdoc/>
    public double MinWidth { get; }

    /// <inheritdoc/>
    public double Padding { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(SceneRect box)
    {
        var items = new List<SceneItem>();
        if (_symbol == ClusterSymbol.None)
        {
            return items;
        }

        var (x, y, w, h) = (box.X, box.Y, box.Width, box.Height);
        var (titleW, titleH) = (_title?.Width ?? 0, _title?.Height ?? 0);
        var (stereoW, stereoH) = (_stereotype?.Width ?? 0, _stereotype?.Height ?? 0);
        void Shape(IReadOnlyList<SceneSegment> segments, bool filled = true) => items.Add(new ScenePath(segments)
        {
            Role = "cluster",
            Id = _id,
            Location = _location,
            Fill = filled ? _fill : null,
            Stroke = _stroke,
        });

        // The outline, and where the title and the stereotypes go (relative to the box)
        (double X, double Y) title;
        (double X, double Y) stereo;
        switch (_symbol)
        {
            case ClusterSymbol.Folder:
                {
                    var tabWidth = titleW > 0 ? titleW + 6 : Math.Max(30, w / 4);
                    var tabHeight = titleW > 0 ? titleH + 6 : 10;
                    Shape(SvekRenderer.FolderOutline(x, y, w, h, tabWidth, tabHeight, _roundCorner));
                    Shape(SceneShapes.Line(x, y + tabHeight, x + tabWidth + 7, y + tabHeight), filled: false);
                    title = (4, 2);
                    stereo = (4 + ((w - stereoW) / 2), 2 + tabHeight);
                    break;
                }

            case ClusterSymbol.Frame:
                {
                    Shape(SceneShapes.Rect(x, y, w, h, _roundCorner / 2));
                    var (textWidth, corner) = titleW > 0 ? (titleW + 10, 10.0) : (w / 3, 7.0);
                    var textHeight = titleW > 0 ? titleH + 3 : 12;
                    Shape(SceneShapes.Polyline((x + textWidth, y), (x + textWidth, y + textHeight - corner), (x + textWidth - corner, y + textHeight), (x, y + textHeight)), filled: false);
                    title = (3, 1);
                    stereo = (4 + ((w - stereoW) / 2), 2 + textHeight);
                    break;
                }

            case ClusterSymbol.Node:
                Shape(SceneShapes.Polygon((x, y + 10), (x + 10, y), (x + w, y), (x + w, y + h - 10), (x + w - 10, y + h), (x, y + h)));
                Shape(SceneShapes.Line(x + w - 10, y + 10, x + w, y), filled: false);
                Shape(SceneShapes.Line(x, y + 10, x + w - 10, y + 10), filled: false);
                Shape(SceneShapes.Line(x + w - 10, y + 10, x + w - 10, y + h), filled: false);
                title = (((w - titleW) / 2) - 4, 13 + stereoH);
                stereo = (((w - stereoW) / 2) - 4, 13);
                break;
            case ClusterSymbol.Database:
                Shape(SvekRenderer.DatabaseOutline(x, y, w, h));
                Shape(
                    [
                        SceneSegment.Move(x, y + 10),
                        SceneSegment.Cubic(x, y + 20, x + (w / 2), y + 20, x + (w / 2), y + 20),
                        SceneSegment.Cubic(x + (w / 2), y + 20, x + w, y + 20, x + w, y + 10),
                    ],
                    filled: false);
                title = ((w - titleW) / 2, 22 + stereoH);
                stereo = ((w - stereoW) / 2, 22);
                break;
            case ClusterSymbol.Cloud:
                Shape(SvekRenderer.CloudOutline(x, y, w, h));
                title = ((w - titleW) / 2, 13 + stereoH);
                stereo = ((w - stereoW) / 2, 13);
                break;
            case ClusterSymbol.Card:
                {
                    Shape(SceneShapes.Rect(x, y, w, h, _roundCorner / 2));
                    var top = titleH + stereoH + 4;
                    if (titleH + stereoH > 0)
                    {
                        Shape(SceneShapes.Line(x, y + top, x + w, y + top), filled: false);
                    }

                    title = ((w - titleW) / 2, 2 + stereoH);
                    stereo = ((w - stereoW) / 2, 2);
                    break;
                }

            default:
                Shape(SceneShapes.Rect(x, y, w, h, _roundCorner / 2));
                title = ((w - titleW) / 2, 2 + stereoH);
                stereo = ((w - stereoW) / 2, 2);
                break;
        }

        if (_stereotype is not null)
        {
            items.AddRange(_stereotype.Draw(x + stereo.X, y + stereo.Y, TextAlign.Center, "cluster-stereotype", _location).Select(t => t with { Id = _id }));
        }

        if (_title is not null)
        {
            items.AddRange(_title.Draw(x + title.X, y + title.Y, TextAlign.Left, "cluster-title", _location).Select(t => t with { Id = _id }));
        }

        return items;
    }
}

/// <summary>
///     The cluster outlines of <see cref="SvekRenderer"/>.
/// </summary>
internal static partial class SvekRenderer
{
    /// <summary>The control distance of the cubic approximation of a quarter circle, as a fraction of the radius.</summary>
    private const double Kappa = 0.5522847498;

    /// <summary>
    ///     Builds the image of a cluster with one of PlantUML's container symbols, measuring its title and stereotypes.
    /// </summary>
    /// <remarks>
    ///     A <see cref="ClusterSymbol.Folder"/> (a package) uses the theme's <see cref="SkinElement.Package"/> style
    ///     (black, 1.5 wide, corners of 5, bold title); the other symbols use <see cref="SkinElement.Container"/>
    ///     (<c>#181818</c>, 1 wide, corners of 5, bold title). Stereotypes are written <c>«name»</c> in italic 12, one
    ///     per line, above the title (below it for a folder and a frame).
    /// </remarks>
    /// <param name="symbol">The symbol.</param>
    /// <param name="id">The id of the cluster.</param>
    /// <param name="title">The title (creole allowed), or null.</param>
    /// <param name="stereotypes">The stereotypes, without the guillemets.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <param name="location">The location of the cluster.</param>
    /// <param name="fill">The fill (<c>#color</c> of the cluster), or null for none.</param>
    /// <param name="line">The line color, or null for the style's.</param>
    /// <returns>The image.</returns>
    public static SvekClusterImage ClusterImage(
        ClusterSymbol symbol,
        string id,
        string? title,
        IReadOnlyList<string> stereotypes,
        SkinTheme theme,
        TextLayout text,
        PlantUmlReportSink? sink,
        PlantUmlLocation location,
        Rgba32? fill = null,
        Rgba32? line = null)
    {
        ArgumentNullException.ThrowIfNull(stereotypes);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        var style = theme[symbol == ClusterSymbol.Folder ? SkinElement.Package : SkinElement.Container];
        var titleBlock = string.IsNullOrEmpty(title)
            ? null
            : text.Layout(title, new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic), style.FontColor, 0, sink, location);
        var stereoBlock = stereotypes.Count == 0
            ? null
            : text.Layout(string.Join("\n", stereotypes.Select(s => "\u00AB" + s + "\u00BB")), new SceneFont(style.FontFamily, 12, false, true), style.FontColor, 0, sink, location);
        return new SvekClusterImage(symbol, id, titleBlock, stereoBlock, style, fill, line, location);
    }

    /// <summary>
    ///     Builds the outline of a folder (PlantUML's <c>USymbolFolder</c>): a tab as wide as the title at the top left,
    ///     sloping down 7 to the top of the body.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="tabWidth">The width of the tab.</param>
    /// <param name="tabHeight">The height of the tab.</param>
    /// <param name="roundCorner">The corner diameter; zero for square corners.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> FolderOutline(double x, double y, double width, double height, double tabWidth, double tabHeight, double roundCorner)
    {
        if (roundCorner <= 0)
        {
            return SceneShapes.Polygon((x, y), (x + tabWidth, y), (x + tabWidth + 7, y + tabHeight), (x + width, y + tabHeight), (x + width, y + height), (x, y + height));
        }

        var r = roundCorner / 2;
        var segments = new List<SceneSegment> { SceneSegment.Move(x + r, y), SceneSegment.Line(x + tabWidth - r, y) };
        Corner(segments, (x + tabWidth - r, y), (x + tabWidth, y), (x + tabWidth, y + r));
        segments.Add(SceneSegment.Line(x + tabWidth + 7, y + tabHeight));
        segments.Add(SceneSegment.Line(x + width - r, y + tabHeight));
        Corner(segments, (x + width - r, y + tabHeight), (x + width, y + tabHeight), (x + width, y + tabHeight + r));
        segments.Add(SceneSegment.Line(x + width, y + height - r));
        Corner(segments, (x + width, y + height - r), (x + width, y + height), (x + width - r, y + height));
        segments.Add(SceneSegment.Line(x + r, y + height));
        Corner(segments, (x + r, y + height), (x, y + height), (x, y + height - r));
        segments.Add(SceneSegment.Line(x, y + r));
        Corner(segments, (x, y + r), (x, y), (x + r, y));
        segments.Add(SceneSegment.Close());
        return segments;
    }

    /// <summary>
    ///     Builds the outline of a database (PlantUML's <c>USymbolDatabase</c>): a cylinder with curved ends 10 high.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The closed outline (the front curve of the top is drawn separately).</returns>
    public static IReadOnlyList<SceneSegment> DatabaseOutline(double x, double y, double width, double height)
    {
        var mid = x + (width / 2);
        return
        [
            SceneSegment.Move(x, y + 10),
            SceneSegment.Cubic(x, y, mid, y, mid, y),
            SceneSegment.Cubic(mid, y, x + width, y, x + width, y + 10),
            SceneSegment.Line(x + width, y + height - 10),
            SceneSegment.Cubic(x + width, y + height, mid, y + height, mid, y + height),
            SceneSegment.Cubic(mid, y + height, x, y + height, x, y + height - 10),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds the outline of a cloud (PlantUML's <c>USymbolCloud</c>): bubbles along the sides of the box, 8 inside
    ///     it, with the pseudo-random bulges PlantUML draws from a generator seeded by the size of the box, so the same
    ///     box always gets the same cloud.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> CloudOutline(double x, double y, double width, double height)
    {
        var random = new JavaRandom((long)width + (7919L * (long)height));
        var points = new List<SvekPoint>();
        var bubble = 11.0;
        if (Math.Max(width, height) / bubble > 16)
        {
            bubble = Math.Max(width, height) / 16;
        }

        const double Margin1 = 8;
        const double Margin2 = 7;
        var (a, b) = (new SvekPoint(Margin1, Margin1), new SvekPoint(width - Margin1, Margin1));
        var (c, d) = (new SvekPoint(width - Margin1, height - Margin1), new SvekPoint(Margin1, height - Margin1));
        static SvekPoint Dx(SvekPoint p, double dx) => p with { X = p.X + dx };
        static SvekPoint Dy(SvekPoint p, double dy) => p with { Y = p.Y + dy };
        if (width > 100 && height > 100)
        {
            CloudLine(random, points, bubble, Dx(a, Margin2), Dx(b, -Margin2));
            points.Add(Dy(b, Margin2));
            CloudLine(random, points, bubble, Dy(b, Margin2), Dy(c, -Margin2));
            points.Add(Dx(c, -Margin2));
            CloudLine(random, points, bubble, Dx(c, -Margin2), Dx(d, Margin2));
            points.Add(Dy(d, -Margin2));
            CloudLine(random, points, bubble, Dy(d, -Margin2), Dy(a, Margin2));
            points.Add(Dx(a, Margin2));
        }
        else
        {
            CloudLine(random, points, bubble, a, b);
            CloudLine(random, points, bubble, b, c);
            CloudLine(random, points, bubble, c, d);
            CloudLine(random, points, bubble, d, a);
        }

        if (points.Count == 0)
        {
            return SceneShapes.Rect(x, y, width, height);
        }

        // One bulging curve between every two points
        points.Add(points[0]);
        var segments = new List<SceneSegment> { SceneSegment.Move(x + points[0].X, y + points[0].Y) };
        for (var k = 0; k + 1 < points.Count; k++)
        {
            var (p1, p2) = (points[k], points[k + 1]);
            var length = Distance(p1, p2);
            if (length == 0)
            {
                continue;
            }

            var coef = random.Between(0.25, 0.35);
            var m1 = Along(p1, p2, length * coef, -length * random.Between(0.4, 0.55));
            var m2 = Along(p1, p2, length * (1 - coef), -length * random.Between(0.4, 0.55));
            segments.Add(SceneSegment.Cubic(x + m1.X, y + m1.Y, x + m2.X, y + m2.Y, x + p2.X, y + p2.Y));
        }

        segments.Add(SceneSegment.Close());
        return segments;
    }

    /// <summary>
    ///     Adds the bubble points of one side of a cloud: two runs of bubbles meeting at a middle point pushed out.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="points">The points.</param>
    /// <param name="bubble">The size of a bubble.</param>
    /// <param name="p1">The start of the side.</param>
    /// <param name="p2">The end of the side.</param>
    private static void CloudLine(JavaRandom random, List<SvekPoint> points, double bubble, SvekPoint p1, SvekPoint p2)
    {
        var length = Distance(p1, p2);
        if (length == 0)
        {
            return;
        }

        var middle = Along(p1, p2, length / 2, -random.Between(1, 1 + Math.Min(12, bubble * 0.8)));
        BubbleLine(random, points, p1, middle, bubble);
        BubbleLine(random, points, middle, p2, bubble);
    }

    /// <summary>
    ///     Adds the bubble points of a run, each moved by a fifth of a bubble at random.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="points">The points.</param>
    /// <param name="p1">The start.</param>
    /// <param name="p2">The end.</param>
    /// <param name="bubble">The size of a bubble.</param>
    private static void BubbleLine(JavaRandom random, List<SvekPoint> points, SvekPoint p1, SvekPoint p2, double bubble)
    {
        var length = Distance(p1, p2);
        if (length == 0)
        {
            return;
        }

        var count = (int)(length / bubble);
        if (count == 0)
        {
            bubble = length / 2;
            count = (int)(length / bubble);
        }

        for (var i = 0; i < count; i++)
        {
            var p = Along(p1, p2, i * length / count, 0);
            var px = p.X + (bubble * 0.2 * random.NextDouble());
            var py = p.Y + (bubble * 0.2 * random.NextDouble());
            points.Add(new SvekPoint(px, py));
        }
    }

    /// <summary>
    ///     Gets a point in the frame of a segment (PlantUML's <c>CoordinateChange</c>): along it from its start, and
    ///     across it to its left.
    /// </summary>
    /// <param name="p1">The start.</param>
    /// <param name="p2">The end.</param>
    /// <param name="along">The distance along.</param>
    /// <param name="across">The distance across.</param>
    /// <returns>The point.</returns>
    private static SvekPoint Along(SvekPoint p1, SvekPoint p2, double along, double across)
    {
        var length = Distance(p1, p2);
        var (ux, uy) = ((p2.X - p1.X) / length, (p2.Y - p1.Y) / length);
        return new SvekPoint(p1.X + (along * ux) - (across * uy), p1.Y + (along * uy) + (across * ux));
    }

    /// <summary>
    ///     Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The distance.</returns>
    private static double Distance(SvekPoint a, SvekPoint b) => Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    /// <summary>
    ///     Adds a rounded corner: a cubic from a point to another, bending toward the corner between them (a quarter
    ///     circle when both are equally far from it at a right angle).
    /// </summary>
    /// <param name="segments">The path.</param>
    /// <param name="from">The start (the current point).</param>
    /// <param name="corner">The corner.</param>
    /// <param name="to">The end.</param>
    private static void Corner(List<SceneSegment> segments, (double X, double Y) from, (double X, double Y) corner, (double X, double Y) to) =>
        segments.Add(SceneSegment.Cubic(
            from.X + (Kappa * (corner.X - from.X)),
            from.Y + (Kappa * (corner.Y - from.Y)),
            to.X + (Kappa * (corner.X - to.X)),
            to.Y + (Kappa * (corner.Y - to.Y)),
            to.X,
            to.Y));

    /// <summary>
    ///     The pseudo-random generator that PlantUML's cloud uses (<c>java.util.Random</c>: the 48-bit linear
    ///     congruential generator with the multiplier <c>0x5DEECE66D</c> and the increment 11 that the Java platform
    ///     specifies), so the bulges are PlantUML's.
    /// </summary>
    /// <param name="seed">The seed.</param>
    private sealed class JavaRandom(long seed)
    {
        /// <summary>The multiplier.</summary>
        private const long Multiplier = 0x5DEECE66DL;

        /// <summary>The 48-bit mask.</summary>
        private const long Mask = (1L << 48) - 1;

        /// <summary>The state.</summary>
        private long _seed = (seed ^ Multiplier) & Mask;

        /// <summary>
        ///     Gets the next double in [0, 1).
        /// </summary>
        /// <returns>The value.</returns>
        public double NextDouble() => ((((long)Next(26)) << 27) + Next(27)) * (1.0 / (1L << 53));

        /// <summary>
        ///     Gets the next double in [a, b).
        /// </summary>
        /// <param name="a">The lower bound.</param>
        /// <param name="b">The upper bound.</param>
        /// <returns>The value.</returns>
        public double Between(double a, double b) => (NextDouble() * (b - a)) + a;

        /// <summary>
        ///     Advances the state and gives its upper bits.
        /// </summary>
        /// <param name="bits">The number of bits.</param>
        /// <returns>The bits.</returns>
        private int Next(int bits)
        {
            _seed = unchecked((_seed * Multiplier) + 0xBL) & Mask;
            return (int)((ulong)_seed >> (48 - bits));
        }
    }
}
