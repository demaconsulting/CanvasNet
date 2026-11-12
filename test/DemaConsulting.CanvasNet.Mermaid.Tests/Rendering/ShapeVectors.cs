using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Access to the shape and label vectors of <c>Fixtures/upstream-vectors/shapes.json</c>, recorded from the pinned
///     Mermaid in headless Chrome (with the bundled Liberation Sans) by <c>gen-shape-vectors.mjs</c>, and the setup that
///     draws a vector's node with this library.
/// </summary>
internal static class ShapeVectors
{
    /// <summary>The vectors, read once.</summary>
    private static readonly Lazy<JsonObject> File = new(() => UpstreamVectors.Load("shapes.json"));

    /// <summary>
    ///     Gets the cases.
    /// </summary>
    public static JsonArray Cases => File.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the natural sizes of the vector pictures, by link.
    /// </summary>
    public static IReadOnlyDictionary<string, (int Width, int Height)> Images =>
        File.Value["images"]!.AsObject().ToDictionary(entry => entry.Key, entry => ((int)entry.Value![0]!, (int)entry.Value[1]!));

    /// <summary>
    ///     Gets the case of a name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The case.</returns>
    public static JsonObject Case(string name) => Cases.Select(item => item!.AsObject()).Single(item => (string?)item["name"] == name);

    /// <summary>
    ///     Gets the names of the cases whose name starts with a prefix, as theory data.
    /// </summary>
    /// <param name="predicate">Which names to take.</param>
    /// <returns>The names.</returns>
    public static TheoryData<string> Names(Func<string, bool> predicate)
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases.Select(item => (string)item!["name"]!).Where(predicate))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Gets the node and the configuration of a case.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <returns>
    ///     The node (the first vertex, with the case's node fields: the shape, padding and width the probe gave Mermaid's
    ///     node before drawing it) and the effective configuration.
    /// </returns>
    public static (LayoutNode Node, MermaidConfig Config) NodeOf(JsonObject vector)
    {
        var text = (string)vector["text"]!;
        var site = vector["config"]?.AsObject().DeepClone().AsObject() ?? [];
        var document = MermaidDocument.Parse(text);
        var model = Assert.IsType<FlowchartModel>(document.Diagram);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(site, text);
        var data = model.GetData(config, document.Detected.MermaidId);
        var node = data.Nodes.First(node => !node.IsGroup);
        if (vector["node"] is JsonObject fields)
        {
            node = node with
            {
                Shape = (string?)fields["shape"] ?? node.Shape,
                Padding = (double?)fields["padding"] ?? node.Padding,
                Width = (double?)fields["width"] ?? node.Width,
            };
        }

        return (node, config);
    }

    /// <summary>
    ///     Draws the node of a case.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <param name="chromeLabel">Whether the label is Chrome's measured box (the shape geometry alone) instead of this library's measurement.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The drawn node.</returns>
    public static ShapeResult Draw(JsonObject vector, bool chromeLabel, MermaidReportSink? sink = null)
    {
        var (node, config) = NodeOf(vector);
        var reports = sink ?? new MermaidReportSink(1000, false);
        var images = new NodeImages(new SizedResolver(Images), 256, reports);
        var labels = new LabelBuilder(ScenePainter.CreateFontResolver(), reports, images);
        var expected = vector["expected"]![0]!.AsObject();
        var context = new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, reports),
            MeasureLabel = chromeLabel ? request => ChromeLabel(expected, request) : labels.Build,
            Images = images,
            Sink = reports,
        };
        return ShapeLibrary.Draw(node, context);
    }

    /// <summary>
    ///     Gets Chrome's measured label of a case as a label layout.
    /// </summary>
    /// <param name="expected">The recorded node.</param>
    /// <param name="request">The request (its kind decides whether the label is HTML).</param>
    /// <returns>The label.</returns>
    public static LabelLayout ChromeLabel(JsonObject expected, LabelRequest request)
    {
        if (expected["label"] is not JsonObject label)
        {
            return LabelLayout.Of(request.HtmlLabels, new SceneRect(0, 0, 0, 0));
        }

        var box = label["box"]!.AsObject();
        return LabelLayout.Of(
            (bool)label["html"]!,
            new SceneRect((double)box["x"]!, (double)box["y"]!, (double)box["width"]!, (double)box["height"]!));
    }

    /// <summary>
    ///     A resolver that gives every known link a picture of its natural size.
    /// </summary>
    /// <param name="sizes">The natural sizes by link.</param>
    private sealed class SizedResolver(IReadOnlyDictionary<string, (int Width, int Height)> sizes) : IImageResolver
    {
        /// <inheritdoc/>
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight) =>
            sizes.TryGetValue(href, out var size) ? new Surface(size.Width, size.Height) : null;
    }
}
