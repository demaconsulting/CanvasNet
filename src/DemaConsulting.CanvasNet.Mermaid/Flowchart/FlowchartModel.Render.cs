// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

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

        // The drawing, the title above it and the view box grown by the diagram padding
        return new ClusterLayout(shapes, meter).RenderDiagram(
            data,
            context.Source.Frontmatter?.Title,
            config.Flowchart.TitleTopMargin,
            config.Flowchart.DiagramPadding,
            labels);
    }
}
