using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the flowchart layout against the reference implementation: each golden of
///     <c>Fixtures/reference/</c> (the layout that the pinned mermaid-cli and mermaid draw in headless Chrome with
///     Liberation Sans, see <c>Fixtures/reference/README.md</c>) is compared with CanvasNet's scene: the view box, the
///     node positions and boxes, the subgraph rectangles, the edge paths sampled along their length and the edge label
///     positions.
/// </summary>
public class MermaidReferenceLayoutTests
{
    /// <summary>
    ///     The tolerance in pixels that CanvasNet achieves on the fixtures (the plan allows 2 px; the observed
    ///     differences, at most 0.001 px, are the rounding of the goldens and of Mermaid's 3-decimal path text).
    /// </summary>
    private const double DefaultTolerance = 0.01;

    /// <summary>
    ///     The fixtures that need a looser tolerance, with the tolerance; the reasons are in
    ///     <c>Fixtures/reference/README.md</c>: Mermaid draws the circles of these shapes with rough.js (a path of
    ///     sampled points), CanvasNet as true circles, so their boxes differ by up to 0.06 px.
    /// </summary>
    private static readonly Dictionary<string, double> LooserTolerances = new(StringComparer.Ordinal)
    {
        ["12-new-shapes-flow"] = 0.1,
        ["14-new-shapes-symbols"] = 0.1,
    };

    /// <summary>
    ///     The fixtures without a golden: the reference depends on the machine (Chrome draws the CJK text of
    ///     <c>38-unicode</c> with a system font, which the bundled fonts do not have).
    /// </summary>
    private static readonly string[] Excluded = ["38-unicode"];

    /// <summary>The number of intervals the goldens sample each edge at.</summary>
    private const int SampleIntervals = 20;

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
    ///     Validates that every flowchart fixture has a golden, except the excluded ones.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Except(Excluded)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames();

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a fixture out as the reference does, within the fixture's tolerance.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Goldens))]
    public void CanvasNetMermaid_Flowchart_ReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart", (string)golden["source"]!));
        var tolerance = LooserTolerances.GetValueOrDefault(name, DefaultTolerance);

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison(tolerance);
        comparison.Compare(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) beyond {tolerance} px (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(20)));
    }

    /// <summary>Gets the folder of the goldens.</summary>
    private static string ReferenceFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference");

    /// <summary>
    ///     Gets the names of the goldens in order.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames() =>
        Directory.GetFiles(ReferenceFolder, "*.layout.json")
            .Select(file => Path.GetFileName(file)[..^".layout.json".Length])
            .Order(StringComparer.Ordinal);

    /// <summary>
    ///     Compares a golden with a scene and collects the differences beyond the tolerance.
    /// </summary>
    /// <param name="tolerance">The tolerance in pixels.</param>
    private sealed class Comparison(double tolerance)
    {
        /// <summary>Gets the differences beyond the tolerance, described.</summary>
        public List<string> Issues { get; } = [];

        /// <summary>Gets the largest difference seen.</summary>
        public double Largest { get; private set; }

        /// <summary>
        ///     Compares every fact of the golden.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void Compare(JsonObject golden, Scene scene)
        {
            var viewBox = Numbers(golden["viewBox"]);
            Check("viewBox", viewBox, [scene.ViewBox.X, scene.ViewBox.Y, scene.ViewBox.Width, scene.ViewBox.Height]);

            var nodes = SceneQuery.Nodes(scene);
            Same("nodes", golden["nodes"]!.AsArray(), nodes.Keys);
            foreach (var node in golden["nodes"]!.AsArray())
            {
                var id = (string)node!["id"]!;
                if (nodes.TryGetValue(id, out var ours))
                {
                    Check($"node {id} position", [(double)node["x"]!, (double)node["y"]!], [ours.X, ours.Y]);
                    Check($"node {id} box", Numbers(node["box"]), Box(ours.Box));
                }
            }

            var clusters = SceneQuery.Clusters(scene);
            Same("clusters", golden["clusters"]!.AsArray(), clusters.Keys);
            foreach (var cluster in golden["clusters"]!.AsArray())
            {
                var id = (string)cluster!["id"]!;
                if (clusters.TryGetValue(id, out var ours))
                {
                    Check($"cluster {id}", Numbers(cluster["box"]), Box(ours));
                }
            }

            var edges = SceneQuery.Edges(scene);
            Same("edges", golden["edges"]!.AsArray(), edges.Keys);
            foreach (var edge in golden["edges"]!.AsArray())
            {
                var id = (string)edge!["id"]!;
                if (edges.TryGetValue(id, out var ours))
                {
                    var (length, points) = SceneQuery.Sample(ours.Segments, SampleIntervals);
                    Check($"edge {id} length", [(double)edge["length"]!], [length]);
                    var expected = edge["points"]!.AsArray().SelectMany(Numbers).ToArray();
                    Check($"edge {id} points", expected, [.. points.SelectMany(p => new[] { p.X, p.Y })]);
                }
            }

            var labels = SceneQuery.EdgeLabels(scene);
            foreach (var label in golden["edgeLabels"]!.AsArray())
            {
                var id = (string)label!["id"]!;
                var box = Numbers(label["box"]);
                if (!labels.TryGetValue(id, out var center))
                {
                    Issues.Add($"edge label {id}: missing");
                    continue;
                }

                Check($"edge label {id} center", [box[0] + (box[2] / 2), box[1] + (box[3] / 2)], [center.X, center.Y]);
            }
        }

        /// <summary>
        ///     Checks that the ids of a golden list and of the scene agree.
        /// </summary>
        /// <param name="what">The kind of item.</param>
        /// <param name="expected">The golden items.</param>
        /// <param name="actual">The scene's ids.</param>
        private void Same(string what, JsonArray expected, IEnumerable<string> actual)
        {
            var golden = expected.Select(item => (string)item!["id"]!).ToHashSet(StringComparer.Ordinal);
            var ours = actual.ToHashSet(StringComparer.Ordinal);
            if (!golden.SetEquals(ours))
            {
                Issues.Add($"{what}: missing [{string.Join(", ", golden.Except(ours))}], unexpected [{string.Join(", ", ours.Except(golden))}]");
            }
        }

        /// <summary>
        ///     Checks numbers against the golden's.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden numbers.</param>
        /// <param name="actual">The scene's numbers.</param>
        private void Check(string what, double[] expected, double[] actual)
        {
            if (expected.Length != actual.Length)
            {
                Issues.Add($"{what}: {expected.Length} numbers expected, {actual.Length} found");
                return;
            }

            var worst = expected.Zip(actual, (e, a) => Math.Abs(e - a)).Max();
            Largest = Math.Max(Largest, worst);
            if (worst > tolerance)
            {
                Issues.Add($"{what}: off by {worst.ToString("0.###", CultureInfo.InvariantCulture)} px, expected [{Text(expected)}], got [{Text(actual)}]");
            }
        }

        /// <summary>Gets the numbers of a JSON array.</summary>
        /// <param name="node">The array.</param>
        /// <returns>The numbers.</returns>
        private static double[] Numbers(JsonNode? node) => [.. node!.AsArray().Select(item => (double)item!)];

        /// <summary>Gets a box as numbers.</summary>
        /// <param name="box">The box.</param>
        /// <returns>X, y, width and height.</returns>
        private static double[] Box(SceneRect box) => [box.X, box.Y, box.Width, box.Height];

        /// <summary>Formats numbers for a message.</summary>
        /// <param name="values">The numbers.</param>
        /// <returns>The text.</returns>
        private static string Text(double[] values) =>
            string.Join(", ", values.Take(8).Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))) + (values.Length > 8 ? ", ..." : string.Empty);
    }
}
