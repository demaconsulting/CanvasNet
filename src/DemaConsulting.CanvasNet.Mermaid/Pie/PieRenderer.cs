// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Pie;

/// <summary>
///     The pie renderer: a port of Mermaid's <c>pieRenderer.ts</c> <c>draw</c> that turns a <see cref="PieDiagram"/> into a
///     scene, with the paint of Mermaid's pie style sheet (<see cref="PieStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Layout.</b> As in Mermaid, the chart is 450 pixels high and its pie 450 wide, centered at (225, 225), with a
///         radius of 185 and an outer circle half its stroke width (the theme's <c>pieOuterStrokeWidth</c>, read with
///         <c>parseFontSize</c>) further out. Sections under 1% of the sum are left out of the pie, the others are cut by
///         d3's <c>pie()</c> in their order and labeled with their percentage (<c>toFixed(0)</c>) at the centroid of an arc
///         at <c>textPosition</c> times the radius. Colors come from the theme's <c>pie1</c> to <c>pie12</c> by each
///         section's index. The title is at (0, -200) of the pie's center. The legend lists every section, at the right
///         (the default), left, top, bottom or center, from the widest legend text the browser measures; the view box
///         takes in the chart, the legend and a title wider than the chart, as Mermaid's does.
///     </para>
///     <para>
///         <b>Texts.</b> A text is measured as the browser measures it when Mermaid draws (its entity placeholders not yet
///         decoded, white space collapsed) and drawn as it is shown (the placeholders decoded), with the box of the
///         browser's <c>getBBox</c> (<see cref="LabelBuilder"/>).
///     </para>
///     <para>
///         <b>Roles.</b> The items carry roles and ids for tests and the reference comparison: the <c>pie</c> group at the
///         pie's center, the <c>pie-chart</c> group (moved by a top or left legend) with the <c>pie-outer-circle</c>, the
///         <c>pie-slice</c> paths and the <c>pie-slice-label</c> texts (the section label as id), the <c>title</c>, and the
///         <c>pie-legend</c> groups (the section label as id) with a <c>pie-legend-rect</c> and a
///         <c>pie-legend-text</c>.
///     </para>
///     <para>
///         <b>Cost.</b> The work meter (<see cref="MermaidLimits.MaxLayoutWork"/>) is charged for each section and for the
///         length of each measured text, and checks cancellation.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed partial class PieRenderer
{
    /// <summary>The margin around the pie (<c>MARGIN</c>).</summary>
    private const double Margin = 40;

    /// <summary>The size of a legend square (<c>LEGEND_RECT_SIZE</c>).</summary>
    private const double LegendRectSize = 18;

    /// <summary>The space after a legend square (<c>LEGEND_SPACING</c>).</summary>
    private const double LegendSpacing = 4;

    /// <summary>The height of the chart (<c>height</c>).</summary>
    private const double Height = 450;

    /// <summary>The width of the pie (<c>pieWidth</c>).</summary>
    private const double PieWidth = Height;

    /// <summary>The diagram.</summary>
    private readonly PieDiagram _diagram;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The pie configuration of the render.</summary>
    private readonly PieConfig _conf;

    /// <summary>The style sheet of the render.</summary>
    private readonly PieStyles _styles;

    /// <summary>The label builder of the render (text metrics).</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>
    ///     Initializes a renderer for one render.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the render.</param>
    public PieRenderer(PieDiagram diagram, DiagramRenderContext context)
    {
        _diagram = diagram;
        _context = context;
        _conf = context.Config.Pie;
        _styles = PieStyles.Create(context.Config, context.Sink);
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, diagram.KeywordLocation, context.CancellationToken);
        _labels = new LabelBuilder(context.Fonts, context.Sink, null) { Meter = _meter };
    }

    /// <summary>
    ///     Gets the text the browser shows for a text that d3 sets as the content of an SVG text: Mermaid's entity
    ///     placeholders become character references when the SVG is written and are shown as their characters (anything
    ///     else is shown as written), and the white space is collapsed as in all SVG text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as shown.</returns>
    internal static string DisplayText(string text)
    {
        const string Prefix = "\ufb02\u00b0";
        const string Suffix = "\u00b6\u00df";
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf(Prefix, i, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(Suffix, start + Prefix.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                builder.Append(text, i, text.Length - i);
                break;
            }

            // The placeholder is decoded on its own (decodeEntities), then read as markup
            builder.Append(text, i, start - i);
            var reference = LabelBuilder.DecodeEntities(text[start..(end + Suffix.Length)]);
            builder.Append(HtmlSanitizer.DecodeReferences(reference));
            i = end + Suffix.Length;
        }

        return LabelBuilder.CollapseSvgWhiteSpace(builder.ToString());
    }

    /// <summary>
    ///     Draws the chart.
    /// </summary>
    /// <returns>The scene, whose view box is the natural size.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the work limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        var config = _context.Config;
        var sections = _diagram.Sections;
        _meter.Charge(1 + sections.Count);

        // The outer stroke width (parseFontSize of the theme variable, 2 when it has no number)
        var outerStrokeWidth = LabelBuilder.ParseFontSize(ConfigScalar.FromJson(config.ThemeVariables["pieOuterStrokeWidth"]) ?? new ConfigScalar(string.Empty, false)).Number ?? 2;
        var textPosition = _conf.TextPosition;
        var innerHole = _conf.DonutHole > 0 && _conf.DonutHole <= 0.9 ? _conf.DonutHole : 0;
        var radius = (Math.Min(PieWidth, Height) / 2) - Margin;

        // The pie: the outer circle, then the slices and their labels
        var pieItems = new List<SceneItem> { OuterCircle(radius + (outerStrokeWidth / 2)) };
        var sum = 0.0;
        foreach (var section in sections)
        {
            sum += section.Value;
        }

        var shown = sections.Where(section => section.Value / sum * 100 >= 1).ToList();
        var arcs = Pie([.. shown.Select(section => section.Value)]);
        var slices = shown.Zip(arcs).Where(pair => ToFixed0(pair.First.Value / sum * 100) != "0").ToList();
        foreach (var (section, arc) in slices)
        {
            pieItems.Add(Slice(section, arc, innerHole * radius, radius));
        }

        var sliceText = _styles.SliceText;
        var sliceFont = new SceneFont(sliceText.FontFamily, sliceText.FontSize, false, false);
        foreach (var (section, arc) in slices)
        {
            var (x, y) = Centroid(radius * textPosition, radius * textPosition, arc);
            var label = TextLine(ToFixed0(section.Value / sum * 100) + "%", 0, 0, sliceFont, sliceText.Color, "pie-slice-label", section.Label, section.Location).Text;
            pieItems.Add(new SceneGroup(x, y, [label]) { Role = "pie-slice-label", Id = section.Label, Location = section.Location });
        }

        // The title, at the top of the pie
        var title = _diagram.DiagramTitle;
        if (title.Length == 0 && _context.Source.Frontmatter?.Title is { Length: > 0 } frontmatter)
        {
            title = PieDiagram.Sanitize(frontmatter);
        }

        var titleText = _styles.TitleText;
        var (titleLine, titleWidth) = TextLine(title, 0, -(Height - 50) / 2, new SceneFont(titleText.FontFamily, titleText.FontSize, false, false), titleText.Color, "title", null, _diagram.KeywordLocation);

        // The legend entries and the widest legend text
        var legendText = _styles.LegendText;
        var legendFont = new SceneFont(legendText.FontFamily, legendText.FontSize, false, false);
        var entries = new List<(PieSection Section, Rgba32? Color, SceneText Text)>();
        var longestTextWidth = double.NegativeInfinity;
        foreach (var section in sections)
        {
            var (text, width) = TextLine(section.LegendText(_diagram.ShowData), LegendRectSize + LegendSpacing, LegendRectSize - LegendSpacing, legendFont, legendText.Color, "pie-legend-text", section.Label, section.Location);
            entries.Add((section, ColorOf(section.Label), text));
            longestTextWidth = Math.Max(longestTextWidth, width);
        }

        // The legend's place, the pie's move and the size of the chart and legend
        var chartAndLegendHeight = Height;
        var chartAndLegendWidth = PieWidth + Margin;
        const double LegendHeight = LegendRectSize + LegendSpacing;
        var totalLegendHeight = sections.Count * LegendHeight;
        var (pieX, pieY) = (0.0, 0.0);
        Func<int, (double X, double Y)> legendAt;
        switch (_conf.LegendPosition)
        {
            case "center":
                legendAt = index => ((-longestTextWidth / 2) - (LegendRectSize + LegendSpacing), (index * LegendHeight) - (LegendHeight * sections.Count / 2));
                break;
            case "top":
                chartAndLegendHeight += totalLegendHeight;
                legendAt = index => ((-longestTextWidth / 2) - (LegendRectSize + LegendSpacing), (index * LegendHeight) - radius);
                (pieX, pieY) = (0, totalLegendHeight + LegendHeight);
                break;
            case "bottom":
                chartAndLegendHeight += totalLegendHeight;
                legendAt = index => ((-longestTextWidth / 2) - (LegendRectSize + LegendSpacing), (index * LegendHeight) - (-radius - LegendHeight));
                break;
            case "left":
                chartAndLegendWidth += LegendRectSize + LegendSpacing + longestTextWidth;
                legendAt = index => (-radius - (LegendRectSize + LegendSpacing), (index * LegendHeight) - (LegendHeight * sections.Count / 2));
                (pieX, pieY) = (longestTextWidth + LegendRectSize + LegendSpacing, 0);
                break;
            default:
                chartAndLegendWidth += LegendRectSize + LegendSpacing + longestTextWidth;
                legendAt = index => (12 * LegendRectSize, (index * LegendHeight) - (LegendHeight * sections.Count / 2));
                break;
        }

        // The group of the chart: the pie (a transform that is not a number is ignored), the title, then the legend
        var items = new List<SceneItem>
        {
            Translated(pieX, pieY, pieItems, "pie-chart", null),
            titleLine,
        };
        for (var i = 0; i < entries.Count; i++)
        {
            var (section, color, text) = entries[i];
            var (x, y) = legendAt(i);
            var rect = new ScenePath(Rectangle(0, 0, LegendRectSize, LegendRectSize))
            {
                Role = "pie-legend-rect",
                Id = section.Label,
                Location = section.Location,
                Fill = color ?? Black,
                Stroke = color is { } stroke ? new SceneStroke(stroke, 1) : null,
            };
            items.Add(Translated(x, y, [rect, text], "pie-legend", section.Label, section.Location));
        }

        // The view box takes in a title wider than the chart
        var titleLeft = (PieWidth / 2) - (titleWidth / 2);
        var titleRight = (PieWidth / 2) + (titleWidth / 2);
        var viewBoxX = Math.Min(0, titleLeft);
        var viewBoxRight = Math.Max(chartAndLegendWidth, titleRight);
        var root = new SceneGroup(PieWidth / 2, Height / 2, items) { Role = "pie", Location = _diagram.KeywordLocation };
        return new Scene(new SceneRect(viewBoxX, 0, viewBoxRight - viewBoxX, chartAndLegendHeight), [root]);
    }

    /// <summary>Gets black, the default of SVG paint.</summary>
    private static Rgba32 Black => new(0, 0, 0, 255);

    /// <summary>
    ///     Makes a group translated by an offset; an offset that is not a number makes the transform invalid, which the
    ///     browser ignores.
    /// </summary>
    /// <param name="x">The horizontal offset.</param>
    /// <param name="y">The vertical offset.</param>
    /// <param name="children">The children.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The group.</returns>
    private static SceneGroup Translated(double x, double y, IReadOnlyList<SceneItem> children, string role, string? id, MermaidLocation location = default)
    {
        var valid = double.IsFinite(x) && double.IsFinite(y);
        return new SceneGroup(valid ? x : 0, valid ? y : 0, children) { Role = role, Id = id, Location = location };
    }

    /// <summary>
    ///     Gets the outline of a rectangle.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Rectangle(double x, double y, double width, double height) =>
    [
        SceneSegment.Move(x, y),
        SceneSegment.Line(x + width, y),
        SceneSegment.Line(x + width, y + height),
        SceneSegment.Line(x, y + height),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Draws the outer circle (<c>circle.pieOuterCircle</c> at the center).
    /// </summary>
    /// <param name="r">The radius.</param>
    /// <returns>The circle.</returns>
    private ScenePath OuterCircle(double r)
    {
        var paint = _styles.OuterCircle;
        return new ScenePath(
        [
            SceneSegment.Move(r, 0),
            SceneSegment.Arc(r, r, 0, false, true, -r, 0),
            SceneSegment.Arc(r, r, 0, false, true, r, 0),
            SceneSegment.Close(),
        ])
        {
            Role = "pie-outer-circle",
            Location = _diagram.KeywordLocation,
            Fill = paint.Fill,
            Stroke = paint.Stroke is { } stroke ? new SceneStroke(stroke, paint.StrokeWidth) : null,
        };
    }

    /// <summary>
    ///     Draws a slice (<c>path.pieCircle</c>): its arc, its color, and the enlargement of the highlighted slice.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <param name="arc">Its arc.</param>
    /// <param name="innerRadius">The inner radius.</param>
    /// <param name="outerRadius">The outer radius.</param>
    /// <returns>The slice.</returns>
    private ScenePath Slice(PieSection section, PieArc arc, double innerRadius, double outerRadius)
    {
        var highlighted = _conf.HighlightSlice != "hover" && _conf.HighlightSlice == section.Label;
        var paint = _styles.Slice(ColorOf(section.Label) ?? Black, highlighted);
        var (_, segments) = ArcPath(innerRadius, outerRadius, arc);
        var scale = paint.Scale;
        return new ScenePath([.. segments.Select(s => Scaled(s, scale))])
        {
            Role = "pie-slice",
            Id = section.Label,
            Location = section.Location,
            Fill = paint.Fill,
            Stroke = paint.Stroke is { } stroke ? new SceneStroke(stroke, paint.StrokeWidth * scale) : null,
        };
    }

    /// <summary>
    ///     Scales a segment around the origin.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The scaled segment.</returns>
    private static SceneSegment Scaled(SceneSegment segment, double scale) => segment with
    {
        X = segment.X * scale,
        Y = segment.Y * scale,
        X1 = segment.X1 * scale,
        Y1 = segment.Y1 * scale,
        X2 = segment.Kind == SceneSegmentKind.ArcTo ? segment.X2 : segment.X2 * scale,
        Y2 = segment.Y2 * scale,
    };

    /// <summary>
    ///     Gets the color of a section: the theme's <c>pie1</c> to <c>pie12</c> by the section's index (d3's
    ///     <c>scaleOrdinal</c> over every label), or null when the variable is not a color.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <returns>The color, or null.</returns>
    private Rgba32? ColorOf(string label)
    {
        var index = 0;
        foreach (var section in _diagram.Sections)
        {
            if (section.Label == label)
            {
                break;
            }

            index++;
        }

        var value = _context.Config.ThemeVariable($"pie{(index % 12) + 1}");
        return value is not null && StyleDeclarations.TryParsePaint(value, out var color) ? color : null;
    }

    /// <summary>
    ///     Makes one SVG text with the box the browser's <c>getBBox</c> gives it, and the width the browser measures when
    ///     Mermaid draws it.
    /// </summary>
    /// <param name="content">The text content (with Mermaid's entity placeholders).</param>
    /// <param name="x">The x of the anchor.</param>
    /// <param name="baseline">The y of the baseline.</param>
    /// <param name="font">The font.</param>
    /// <param name="color">The color.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The text, and its measured width.</returns>
    private (SceneText Text, double Width) TextLine(string content, double x, double baseline, SceneFont font, Rgba32 color, string role, string? id, MermaidLocation location)
    {
        var anchor = role is "pie-legend-text" ? SceneTextAnchor.Start : SceneTextAnchor.Middle;

        // Measured as drawn (the placeholders not decoded yet)
        var measured = LabelBuilder.CollapseSvgWhiteSpace(content);
        _meter.Charge(1 + measured.Length);
        var (_, measuredLeft, measuredRight) = measured.Length > 0 ? _labels.SvgLineExtent(measured, font) : (0, 0, 0);

        // Shown as decoded
        var text = DisplayText(content);
        _meter.Charge(1 + text.Length);
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var (advance, left, right) = text.Length > 0 ? _labels.SvgLineExtent(text, font) : (0, 0, 0);
        var start = anchor == SceneTextAnchor.Middle ? x - (advance / 2) : x;
        IReadOnlyList<SceneTextRun> runs = text.Length > 0 ? [new SceneTextRun(text, font, color)] : [];
        var line = new SceneText(x, baseline, anchor, runs, new SceneRect(start + left, baseline - ascent, right - left, ascent + descent))
        {
            Role = role,
            Id = id,
            Location = location,
        };
        return (line, measuredRight - measuredLeft);
    }
}
