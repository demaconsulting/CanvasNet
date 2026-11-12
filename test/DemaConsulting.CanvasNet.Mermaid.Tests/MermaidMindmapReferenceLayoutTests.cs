using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Cose;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Mindmap;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the mindmap layout against the reference implementation: each golden of
///     <c>Fixtures/reference/mindmap/</c> (the layout that the pinned mermaid-cli and mermaid draw in headless Chrome with
///     Liberation Sans, see <c>Fixtures/reference/README.md</c>) is compared with CanvasNet in three tiers, because the
///     cose-bilkent layout is chaotic (a node size that differs in its last bit can move every node): the node sizes the
///     layout is given; the layout alone, given the golden's own sizes; and the whole drawing (view box, node centers,
///     boxes and paint, and the edges sampled along their length with their paint).
/// </summary>
public class MermaidMindmapReferenceLayoutTests
{
    /// <summary>
    ///     The tolerance in pixels of every position and size of the whole drawing except the node sizes (the goldens'
    ///     boxes and edge samples are rounded to 0.001 px).
    /// </summary>
    private const double DefaultTolerance = 0.01;

    /// <summary>
    ///     The tolerance in pixels of a node size: Chrome measures text on its layout unit of 1/64 pixel.
    /// </summary>
    private const double SizeTolerance = 0.02;

    /// <summary>The number of intervals the goldens sample each edge at.</summary>
    private const int SampleIntervals = 20;

    /// <summary>
    ///     The fixtures whose whole drawing is not compared, with the reason (see <c>Fixtures/reference/README.md</c>):
    ///     their node sizes match the golden's within <see cref="SizeTolerance"/> but not in every bit, and the chaotic
    ///     layout turns that into a different picture. The sizes and the layout given the golden's sizes are still
    ///     compared.
    /// </summary>
    private static readonly Dictionary<string, string> EndToEndExclusions = new(StringComparer.Ordinal)
    {
        ["02-shapes"] = "Chrome's box of the cloud node is 8e-6 px taller (it bounds the arcs in single precision), and the layout moves every node",
    };

    /// <summary>
    ///     Gets the names of the goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> Goldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames())
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Gets the names of the goldens whose whole drawing is compared.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> EndToEndGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames().Where(name => !EndToEndExclusions.ContainsKey(name)))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every mindmap fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mindmap"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames();

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet gives the layout of a mindmap fixture the node sizes Chrome measured (each node
    ///     group's <c>getBBox</c>), within <see cref="SizeTolerance"/>.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Goldens))]
    public void CanvasNetMermaid_MindmapReferenceSizes_MatchMermaid(string name)
    {
        // Arrange
        var (golden, text) = Load(name);

        // Act
        var sizes = Sizes(text);
        var comparison = new Comparison();
        foreach (var node in golden["nodes"]!.AsArray())
        {
            var id = (string)node!["id"]!;
            if (!sizes.TryGetValue(id, out var size))
            {
                comparison.Issues.Add($"node {id}: missing");
                continue;
            }

            comparison.Check($"node {id} size", Numbers(node["size"]), [size.Width, size.Height], SizeTolerance);
        }

        // Assert
        Assert.Equal(golden["nodes"]!.AsArray().Count, sizes.Count);
        Assert.True(comparison.Issues.Count == 0, comparison.Describe(name));
    }

    /// <summary>
    ///     Validates that the cose-bilkent layout, given the node sizes and edges of a golden, gives Chrome's node
    ///     centers and cytoscape's edge points bit for bit.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Goldens))]
    public void CanvasNetMermaid_MindmapReferenceSeededLayout_MatchesMermaid(string name)
    {
        // Arrange: the nodes in their order (node_<k> is the k-th node) and the edges in the golden's order
        var (golden, _) = Load(name);
        var nodes = golden["nodes"]!.AsArray();
        var edges = golden["edges"]!.AsArray();
        var sizes = nodes.Select(node => Numbers(node!["size"])).Select(size => new CoseSize(size[0], size[1])).ToList();
        var links = edges.Select(edge => ((string)edge!["id"]!).Split('_')).Select(parts =>
            new CoseLink(int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture))).ToList();
        var meter = new LayoutWorkMeter(long.MaxValue, default, TestContext.Current.CancellationToken);

        // Act
        var layout = CoseLayout.Run(sizes, links, meter);

        // Assert: every center and every edge point is the same double
        var issues = new List<string>();
        foreach (var node in nodes)
        {
            var id = (string)node!["id"]!;
            var center = layout.Centers[int.Parse(id["node_".Length..], CultureInfo.InvariantCulture)];
            Exact(issues, $"node {id} center", Numbers(node["center"]), [center.X, center.Y]);
        }

        for (var i = 0; i < edges.Count; i++)
        {
            var points = layout.Edges[i];
            var expected = edges[i]!["dataPoints"]!.AsArray().SelectMany(point => point!.AsArray().Select(Number)).ToArray();
            Exact(issues, $"edge {edges[i]!["id"]} points", expected, [points.Start.X, points.Start.Y, points.Mid.X, points.Mid.Y, points.End.X, points.End.Y]);
        }

        Assert.True(issues.Count == 0, $"{name}: {issues.Count} difference(s):\n" + string.Join('\n', issues.Take(20)));
    }

    /// <summary>
    ///     Validates that CanvasNet draws a mindmap fixture as the reference does, within the tolerances: the view box,
    ///     each node's center, box and paint (the shape's fill and stroke, the default node's line and the label color),
    ///     and each edge sampled along its length with its stroke.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(EndToEndGoldens))]
    public void CanvasNetMermaid_MindmapReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var (golden, text) = Load(name);

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison();
        comparison.Compare(golden, scene);

        // Assert
        Assert.True(comparison.Issues.Count == 0, comparison.Describe(name));
    }

    /// <summary>
    ///     Validates that the end-to-end exclusions name fixtures that have goldens.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_ReferenceExclusions_NameGoldens()
    {
        // Arrange
        var goldens = GoldenNames().ToHashSet(StringComparer.Ordinal);

        // Act
        var unknown = EndToEndExclusions.Keys.Where(name => !goldens.Contains(name)).ToList();

        // Assert
        Assert.Empty(unknown);
    }

    /// <summary>Gets the folder of the goldens.</summary>
    private static string ReferenceFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "mindmap");

    /// <summary>
    ///     Gets the names of the goldens in order.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames() =>
        Directory.GetFiles(ReferenceFolder, "*.layout.json")
            .Select(file => Path.GetFileName(file)[..^".layout.json".Length])
            .Order(StringComparer.Ordinal);

    /// <summary>
    ///     Loads a golden and the text of its fixture.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    /// <returns>The golden and the text.</returns>
    private static (JsonObject Golden, string Text) Load(string name)
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "mindmap", (string)golden["source"]!));
        return (golden, text);
    }

    /// <summary>
    ///     Gets the size each node gives the layout (its box in single precision, as the renderer measures it), by DOM
    ///     id: the sizes of a <see cref="MindmapRenderer"/> draw of the text.
    /// </summary>
    /// <param name="text">The mindmap text.</param>
    /// <returns>The sizes.</returns>
    internal static Dictionary<string, CoseSize> Sizes(string text)
    {
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var diagram = MindmapParser.Parse(source, detected, MermaidLimits.Default);
        var options = new MermaidRenderOptions();
        var config = MermaidConfigResolver.Resolve(source, "mindmap", options, null);
        var context = new DiagramRenderContext(source, detected, options, config, new MermaidReportSink(100, false), ScenePainter.CreateFontResolver(), TestContext.Current.CancellationToken);
        var renderer = new MindmapRenderer(diagram, context);
        renderer.Draw();
        return diagram.Nodes.ToDictionary(node => "node_" + node.Id.ToString(CultureInfo.InvariantCulture), node => renderer.Sizes[node.Id], StringComparer.Ordinal);
    }

    /// <summary>
    ///     Checks that numbers are the golden's exactly (NaN, which the golden writes as null, equals NaN).
    /// </summary>
    /// <param name="issues">The differences found so far.</param>
    /// <param name="what">The fact.</param>
    /// <param name="expected">The golden numbers.</param>
    /// <param name="actual">The computed numbers.</param>
    private static void Exact(List<string> issues, string what, double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length || expected.Zip(actual).Any(pair => pair.First.CompareTo(pair.Second) != 0))
        {
            issues.Add($"{what}: expected [{string.Join(", ", expected.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}], " +
                $"got [{string.Join(", ", actual.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}]");
        }
    }

    /// <summary>Reads a number of a golden: a JSON number, or null for NaN.</summary>
    /// <param name="node">The JSON value.</param>
    /// <returns>The number.</returns>
    private static double Number(JsonNode? node) => node is null ? double.NaN : (double)node;

    /// <summary>Gets the numbers of a JSON array.</summary>
    /// <param name="node">The array.</param>
    /// <returns>The numbers.</returns>
    private static double[] Numbers(JsonNode? node) => [.. node!.AsArray().Select(Number)];

    /// <summary>
    ///     Compares a golden with a scene and collects the differences.
    /// </summary>
    private sealed class Comparison
    {
        /// <summary>Gets the differences, described.</summary>
        public List<string> Issues { get; } = [];

        /// <summary>Gets the largest difference seen.</summary>
        public double Largest { get; private set; }

        /// <summary>
        ///     Describes the differences for an assertion message.
        /// </summary>
        /// <param name="name">The fixture name.</param>
        /// <returns>The description.</returns>
        public string Describe(string name) =>
            $"{name}: {Issues.Count} difference(s) (largest {Largest.ToString("0.###", CultureInfo.InvariantCulture)} px):\n" +
            string.Join('\n', Issues.Take(25));

        /// <summary>
        ///     Compares the whole drawing.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void Compare(JsonObject golden, Scene scene)
        {
            Check("viewBox", Numbers(golden["viewBox"]), [scene.ViewBox.X, scene.ViewBox.Y, scene.ViewBox.Width, scene.ViewBox.Height], DefaultTolerance);

            var groups = scene.Walk()
                .Where(entry => entry.Item is SceneGroup { Role: "node-group", Id: not null })
                .ToDictionary(entry => entry.Item.Id!, entry => (Group: (SceneGroup)entry.Item, entry.OffsetX, entry.OffsetY), StringComparer.Ordinal);
            if (groups.Count != golden["nodes"]!.AsArray().Count)
            {
                Issues.Add($"nodes: {golden["nodes"]!.AsArray().Count} expected, {groups.Count} found");
            }

            foreach (var node in golden["nodes"]!.AsArray())
            {
                var id = (string)node!["id"]!;
                if (!groups.TryGetValue(id, out var entry))
                {
                    Issues.Add($"node {id}: missing");
                    continue;
                }

                var x = entry.OffsetX + entry.Group.OffsetX;
                var y = entry.OffsetY + entry.Group.OffsetY;
                Check($"node {id} center", Numbers(node["center"]), [x, y], DefaultTolerance);
                var box = Scene.BoundsOf(entry.Group.Children)?.Offset(x, y);
                Check($"node {id} box", Numbers(node["box"]), box is { } b ? [b.X, b.Y, b.Width, b.Height] : [], DefaultTolerance);
                ComparePaint(id, node, entry.Group);
            }

            var edges = SceneQuery.Edges(scene);
            if (edges.Count != golden["edges"]!.AsArray().Count)
            {
                Issues.Add($"edges: {golden["edges"]!.AsArray().Count} expected, {edges.Count} found");
            }

            foreach (var edge in golden["edges"]!.AsArray())
            {
                var id = (string)edge!["id"]!;
                if (!edges.TryGetValue(id, out var ours))
                {
                    Issues.Add($"edge {id}: missing");
                    continue;
                }

                var (length, points) = SceneQuery.Sample(ours.Segments, SampleIntervals);
                Check($"edge {id} length", [(double)edge["length"]!], [length], DefaultTolerance);
                Check($"edge {id} points", edge["points"]!.AsArray().SelectMany(Numbers).ToArray(), [.. points.SelectMany(p => new[] { p.X, p.Y })], DefaultTolerance);
                Color($"edge {id} fill", (string)edge["fill"]!, ours.Fill);
                Color($"edge {id} stroke", (string)edge["stroke"]!, ours.Stroke?.Color);
                Check($"edge {id} stroke width", [(double)edge["strokeWidth"]!], [ours.Stroke?.Width ?? 0], DefaultTolerance);
            }
        }

        /// <summary>
        ///     Checks numbers against the golden's.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden numbers.</param>
        /// <param name="actual">The scene's numbers.</param>
        /// <param name="tolerance">The tolerance.</param>
        public void Check(string what, double[] expected, double[] actual, double tolerance)
        {
            if (expected.Length != actual.Length)
            {
                Issues.Add($"{what}: {expected.Length} numbers expected, {actual.Length} found");
                return;
            }

            var worst = expected.Length == 0 ? 0 : expected.Zip(actual, (e, a) => Math.Abs(e - a)).Max();
            Largest = Math.Max(Largest, double.IsNaN(worst) ? double.PositiveInfinity : worst);
            if (!(worst <= tolerance))
            {
                Issues.Add($"{what}: off by {worst.ToString("0.###", CultureInfo.InvariantCulture)} px, expected [{Format(expected)}], got [{Format(actual)}]");
            }
        }

        /// <summary>
        ///     Compares the paint of a node: the fill and stroke of its shape, the stroke of a default node's line, and the
        ///     color of every run of its label.
        /// </summary>
        /// <param name="id">The node id.</param>
        /// <param name="golden">The golden node.</param>
        /// <param name="group">The node's group.</param>
        private void ComparePaint(string id, JsonNode golden, SceneGroup group)
        {
            var paints = golden["paint"]!.AsArray();
            var shape = group.Children.OfType<ScenePath>().FirstOrDefault(path => path.Role == "node");
            var line = group.Children.OfType<ScenePath>().FirstOrDefault(path => path.Role == "node-line");
            var shapePaint = paints.FirstOrDefault(paint => (string)paint!["tag"]! != "line");
            var linePaint = paints.FirstOrDefault(paint => (string)paint!["tag"]! == "line");
            if (shapePaint is null || shape is null)
            {
                Issues.Add($"node {id} shape: {(shapePaint is null ? "not in the golden" : "not drawn")}");
            }
            else
            {
                Color($"node {id} fill", (string)shapePaint["fill"]!, shape.Fill);
                Color($"node {id} stroke", (string)shapePaint["stroke"]!, shape.Stroke?.Color);
            }

            if ((linePaint is null) != (line is null))
            {
                Issues.Add($"node {id} line: {(linePaint is null ? "not in the golden" : "not drawn")}");
            }
            else if (linePaint is not null && line is not null)
            {
                var width = (double)linePaint["strokeWidth"]!;
                Color($"node {id} line stroke", width > 0 ? (string)linePaint["stroke"]! : "none", line.Stroke is { Width: > 0 } stroke ? stroke.Color : null);
                Check($"node {id} line width", [width], [line.Stroke?.Width ?? 0], DefaultTolerance);
            }

            var color = (string?)golden["labelColor"];
            foreach (var run in group.Children.OfType<SceneText>().SelectMany(text => text.Runs))
            {
                Color($"node {id} label '{run.Text}'", color ?? "none", run.Color);
            }
        }

        /// <summary>
        ///     Compares a CSS color of the golden (<c>rgb()</c>, <c>rgba()</c> or <c>none</c>) with a scene color.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="golden">The golden color.</param>
        /// <param name="ours">The scene's color, or null for none.</param>
        private void Color(string what, string golden, Rgba32? ours)
        {
            Rgba32? expected = golden == "none" ? null : CssColor.Parse(golden);
            if (expected is { A: 0 })
            {
                expected = null;
            }

            if (ours is { A: 0 })
            {
                ours = null;
            }

            if (expected != ours)
            {
                Issues.Add($"{what}: expected {golden}, got {(ours is { } c ? $"rgba({c.R}, {c.G}, {c.B}, {c.A})" : "none")}");
            }
        }

        /// <summary>Formats numbers for a message.</summary>
        /// <param name="values">The numbers.</param>
        /// <returns>The text.</returns>
        private static string Format(double[] values) =>
            string.Join(", ", values.Take(8).Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))) + (values.Length > 8 ? ", ..." : string.Empty);
    }
}
