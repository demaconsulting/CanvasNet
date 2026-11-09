// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <content>
///     The flowchart renderer (<c>flowRenderer-v3-unified.ts</c>'s <c>draw</c>): the layout data of the call through
///     the cluster layout, the diagram title (<c>insertTitle</c>) and the view box with the diagram padding
///     (<c>setupViewPortForSVG</c>).
/// </content>
internal sealed partial class FlowchartModel
{
    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The reports of what is not drawn come first
        var config = context.Config;
        var sink = context.Sink;
        ReportUnsupported(config, sink);
        var data = GetData(config, context.Diagram.MermaidId);

        // The settings of the rendering: pictures, labels, style sheet and the shared work budget
        var limits = context.Options.Limits;
        var images = new NodeImages(context.Options.ImageResolver, limits.MaxImages, sink);
        var labels = new LabelBuilder(context.Fonts, sink, images);
        var shapes = new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, sink),
            MeasureLabel = labels.Build,
            Images = images,
            Sink = sink,
        };
        var meter = new LayoutWorkMeter(limits.MaxLayoutWork, KeywordLocation, context.CancellationToken);
        var drawing = new ClusterLayout(shapes, meter).Render(data);

        // The root group, then the title above it
        var items = new List<SceneItem>(drawing.Items.Count + 1);
        items.Add(new SceneGroup(0, 0, drawing.Items) { Role = "root" });
        var bounds = drawing.Bounds;
        if (context.Source.Frontmatter?.Title is { Length: > 0 } title)
        {
            var text = DrawTitle(HtmlSanitizer.Sanitize(title), bounds, config.Flowchart.TitleTopMargin, shapes.Styles, labels);
            items.Add(text);
            bounds = bounds is { } b ? b.Union(text.Box) : text.Box;
        }

        // The view box: the bounds grown by the diagram padding
        var padding = config.Flowchart.DiagramPadding;
        var box = bounds ?? new SceneRect(0, 0, 0, 0);
        var viewBox = new SceneRect(box.X - padding, box.Y - padding, box.Width + (2 * padding), box.Height + (2 * padding));
        return new Scene(viewBox, items) { Images = images };
    }

    /// <summary>
    ///     Draws the diagram title (Mermaid's <c>insertTitle</c>): centered over the bounds of the drawing, its baseline
    ///     the title margin above the origin.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="bounds">The bounds of the drawing, or null when nothing is drawn (Mermaid then takes an empty box at the origin).</param>
    /// <param name="topMargin">The title margin (<c>titleTopMargin</c>).</param>
    /// <param name="styles">The style sheet.</param>
    /// <param name="labels">The label builder (for the text metrics).</param>
    /// <returns>The title line.</returns>
    private static SceneText DrawTitle(string title, SceneRect? bounds, double topMargin, FlowchartStyles styles, LabelBuilder labels)
    {
        var paint = styles.Title;
        var font = new SceneFont(paint.FontFamily, paint.FontSize, false, false);
        var box = bounds ?? new SceneRect(0, 0, 0, 0);
        var x = box.X + (box.Width / 2);
        var baseline = -topMargin;
        var width = labels.Advance(title, font);
        var (ascent, descent) = labels.VerticalMetrics(font);
        return new SceneText(
            x,
            baseline,
            SceneTextAnchor.Middle,
            [new SceneTextRun(title, font, paint.Color)],
            new SceneRect(x - (width / 2), baseline - ascent, width, ascent + descent))
        {
            Role = "title",
        };
    }
}
