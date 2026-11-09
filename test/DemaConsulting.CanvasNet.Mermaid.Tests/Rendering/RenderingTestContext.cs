using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Builds the settings of a rendering for the rendering unit tests: the configuration of a text, its style sheet and
///     the real label measurement.
/// </summary>
internal static class RenderingTestContext
{
    /// <summary>
    ///     Creates the settings of a text.
    /// </summary>
    /// <param name="text">The diagram text (its frontmatter and directives configure the rendering).</param>
    /// <param name="sink">The report sink, or null for a new one.</param>
    /// <returns>The settings.</returns>
    public static ShapeContext Create(string text = "flowchart TD\n  A\n", MermaidReportSink? sink = null)
    {
        var reports = sink ?? new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), text);
        var labels = new LabelBuilder(ScenePainter.CreateFontResolver(), reports, null);
        return new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, reports),
            MeasureLabel = labels.Build,
            Sink = reports,
        };
    }

    /// <summary>
    ///     Creates the settings of a class diagram text: its configuration with the class diagram's style sheet.
    /// </summary>
    /// <param name="text">The class diagram text (its frontmatter and directives configure the rendering).</param>
    /// <returns>The settings.</returns>
    public static ShapeContext CreateClass(string text = "classDiagram\n  class A\n")
    {
        var reports = new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), text);
        var labels = new LabelBuilder(ScenePainter.CreateFontResolver(), reports, null);
        return new ShapeContext
        {
            Config = config,
            Styles = ClassStyles.Create(config, reports),
            MeasureLabel = labels.Build,
            Sink = reports,
        };
    }

    /// <summary>
    ///     Lays out and draws layout data with the cluster layout.
    /// </summary>
    /// <param name="data">The layout data.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn root graph.</returns>
    public static RenderedGraph RenderData(LayoutData data, ShapeContext context)
    {
        var meter = new LayoutWorkMeter(50_000_000, new MermaidLocation(null, 1, 1), TestContext.Current.CancellationToken);
        return new ClusterLayout(context, meter).Render(data);
    }

    /// <summary>
    ///     Parses a flowchart and maps it to layout data under the configuration of its text.
    /// </summary>
    /// <param name="text">The flowchart text.</param>
    /// <returns>The layout data and the settings.</returns>
    public static (LayoutData Data, ShapeContext Context) Flowchart(string text)
    {
        var context = Create(text);
        var model = Assert.IsType<FlowchartModel>(MermaidDocument.Parse(text).Diagram);
        return (model.GetData(context.Config, "flowchart-v2"), context);
    }

    /// <summary>
    ///     Lays out and draws a flowchart with the cluster layout.
    /// </summary>
    /// <param name="text">The flowchart text.</param>
    /// <param name="maxWork">The work budget.</param>
    /// <returns>The drawn root graph.</returns>
    public static RenderedGraph Render(string text, long maxWork = 50_000_000)
    {
        var (data, context) = Flowchart(text);
        var meter = new LayoutWorkMeter(maxWork, new MermaidLocation(null, 1, 1), TestContext.Current.CancellationToken);
        return new ClusterLayout(context, meter).Render(data);
    }

    /// <summary>
    ///     Wraps a drawn graph in a scene so the scene queries can read it.
    /// </summary>
    /// <param name="graph">The drawn graph.</param>
    /// <returns>The scene.</returns>
    public static Scene SceneOf(RenderedGraph graph) => new(graph.Bounds ?? new SceneRect(0, 0, 0, 0), graph.Items);
}
