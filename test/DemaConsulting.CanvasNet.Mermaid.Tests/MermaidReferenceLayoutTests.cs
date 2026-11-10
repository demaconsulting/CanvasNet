using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the flowchart, class, state and entity relationship diagram layouts against the reference
///     implementation: each golden of <c>Fixtures/reference/</c>, <c>Fixtures/reference/class/</c>,
///     <c>Fixtures/reference/state/</c> and <c>Fixtures/reference/er/</c> (the layout that the pinned mermaid-cli and
///     mermaid draw in headless Chrome with Liberation Sans, see <c>Fixtures/reference/README.md</c>) is compared with
///     CanvasNet's scene: the view box, the node positions and boxes, the subgraph rectangles, the edge paths sampled
///     along their length and the edge label positions; for a class, state or ER diagram also the drawn parts of each
///     node, the subgraph titles, the terminal labels and the title, and for a state diagram the inner rectangles of the
///     composite states.
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
    ///     The class diagram fixtures that need a looser tolerance, with the tolerance; the reasons are in
    ///     <c>Fixtures/reference/README.md</c>: <c>16-config-title-links</c> draws its class box texts as SVG text
    ///     (<c>htmlLabels: false</c>), and Chrome's box of the bold <c>d</c> that ends <c>Backend</c> reaches one pixel
    ///     further than the glyph box rule gives, so that class box is 0.219 px narrower and the boxes and edges laid out
    ///     beside it move by up to 0.06 px.
    /// </summary>
    private static readonly Dictionary<string, double> ClassLooserTolerances = new(StringComparer.Ordinal)
    {
        ["16-config-title-links"] = 0.23,
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

    /// <summary>
    ///     Gets the names of the class diagram goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> ClassGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames(ClassReferenceFolder))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every class diagram fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames(ClassReferenceFolder);

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a class diagram fixture out as the reference does: the view box, the nodes and
    ///     their drawn parts (the class box rows and dividers), the namespaces and their titles, the edges, the edge
    ///     labels, the terminal labels and the title.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(ClassGoldens))]
    public void CanvasNetMermaid_ClassDiagram_ReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ClassReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", (string)golden["source"]!));
        var tolerance = ClassLooserTolerances.GetValueOrDefault(name, DefaultTolerance);

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison(tolerance);
        comparison.Compare(golden, scene);
        comparison.CompareExtended(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) beyond {tolerance} px (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(20)));
    }

    /// <summary>
    ///     Gets the names of the state diagram goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> StateGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames(StateReferenceFolder))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every state diagram fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_StateDiagram_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames(StateReferenceFolder);

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a state diagram fixture out as the reference does: the view box, the nodes and
    ///     their drawn parts, the clusters (composite states with their inner rectangles and titles, concurrency regions
    ///     and note groups), the edges, the edge labels and the title.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(StateGoldens))]
    public void CanvasNetMermaid_StateDiagram_ReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(StateReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", (string)golden["source"]!));
        var tolerance = DefaultTolerance;

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison(tolerance);
        comparison.Compare(golden, scene);
        comparison.CompareExtended(golden, scene);
        comparison.CompareClusterInners(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) beyond {tolerance} px (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(20)));
    }

    /// <summary>
    ///     Gets the names of the entity relationship diagram goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> ErGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames(ErReferenceFolder))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every entity relationship diagram fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ErDiagram_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames(ErReferenceFolder);

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays an entity relationship diagram fixture out as the reference does: the view box,
    ///     the entities and their drawn parts (the outline, the rows, each text of the table and the dividers), the
    ///     subgraphs and their titles, the relationships, their labels and the title.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(ErGoldens))]
    public void CanvasNetMermaid_ErDiagram_ReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ErReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "er", (string)golden["source"]!));
        var tolerance = DefaultTolerance;

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison(tolerance);
        comparison.Compare(golden, scene);
        comparison.CompareExtended(golden, scene);
        comparison.CompareClusterInners(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) beyond {tolerance} px (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(20)));
    }

    /// <summary>Gets the folder of the goldens.</summary>
    private static string ReferenceFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference");

    /// <summary>Gets the folder of the class diagram goldens.</summary>
    private static string ClassReferenceFolder => Path.Combine(ReferenceFolder, "class");

    /// <summary>Gets the folder of the state diagram goldens.</summary>
    private static string StateReferenceFolder => Path.Combine(ReferenceFolder, "state");

    /// <summary>Gets the folder of the entity relationship diagram goldens.</summary>
    private static string ErReferenceFolder => Path.Combine(ReferenceFolder, "er");

    /// <summary>
    ///     Gets the names of the goldens in order.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames() => GoldenNames(ReferenceFolder);

    /// <summary>
    ///     Gets the names of the goldens of a folder in order.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames(string folder) =>
        Directory.GetFiles(folder, "*.layout.json")
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
        ///     Compares the facts of an extended golden (a diagram other than a flowchart): the drawn parts of each node,
        ///     the subgraph titles, the terminal labels and the title.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void CompareExtended(JsonObject golden, Scene scene)
        {
            var parts = SceneQuery.Parts(scene);
            foreach (var node in golden["nodes"]!.AsArray())
            {
                var id = (string)node!["id"]!;
                if (!parts.TryGetValue(id, out var ours))
                {
                    continue;
                }

                var expected = new List<(string Kind, SceneRect Box)>();
                foreach (var part in node["parts"]!.AsArray())
                {
                    var box = Numbers(part!["box"]);
                    SceneQuery.AddPart(expected, PartKind(part.AsObject()), new SceneRect(box[0], box[1], box[2], box[3]));
                }

                var kinds = string.Join(", ", expected.Select(p => p.Kind));
                var ourKinds = string.Join(", ", ours.Select(p => p.Kind));
                if (kinds != ourKinds)
                {
                    Issues.Add($"node {id} parts: expected [{kinds}], got [{ourKinds}]");
                    continue;
                }

                for (var i = 0; i < expected.Count; i++)
                {
                    Check($"node {id} part {i} ({expected[i].Kind})", Box(expected[i].Box), Box(ours[i].Box));
                }
            }

            var clusterIds = golden["clusters"]!.AsArray().Select(c => (string)c!["id"]!).ToHashSet(StringComparer.Ordinal);
            var clusterLabels = SceneQuery.ClusterLabels(scene, clusterIds);
            foreach (var cluster in golden["clusters"]!.AsArray())
            {
                var id = (string)cluster!["id"]!;
                if (cluster["label"] is JsonObject label)
                {
                    if (clusterLabels.TryGetValue(id, out var ours))
                    {
                        Check($"cluster {id} label", Numbers(label["box"]), Box(ours));
                    }
                    else
                    {
                        Issues.Add($"cluster {id} label: missing");
                    }
                }
            }

            var terminals = SceneQuery.Terminals(scene);
            foreach (var group in golden["terminals"]!.AsArray().GroupBy(t => (string?)t!["edge"] ?? string.Empty))
            {
                var expected = group.OrderBy(t => (string)t!["end"]! == "start" ? 0 : 1).ToList();
                if (!terminals.TryGetValue(group.Key, out var ours) || ours.Count != expected.Count)
                {
                    Issues.Add($"terminals of {group.Key}: {expected.Count} expected, {ours?.Count ?? 0} found");
                    continue;
                }

                for (var i = 0; i < expected.Count; i++)
                {
                    Check($"terminal {group.Key} {expected[i]!["end"]}", [(double)expected[i]!["x"]!, (double)expected[i]!["y"]!], [ours[i].X, ours[i].Y]);
                }
            }

            var title = SceneQuery.Title(scene);
            if (golden["title"] is JsonObject expectedTitle)
            {
                if (title is { } box)
                {
                    Check("title", Numbers(expectedTitle["box"]), Box(box));
                }
                else
                {
                    Issues.Add("title: missing");
                }
            }
            else if (title is not null)
            {
                Issues.Add("title: unexpected");
            }
        }

        /// <summary>
        ///     Compares the inner rectangles of the clusters that have one (a composite state's area under its title).
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void CompareClusterInners(JsonObject golden, Scene scene)
        {
            var inners = SceneQuery.ClusterInners(scene);
            foreach (var cluster in golden["clusters"]!.AsArray())
            {
                var id = (string)cluster!["id"]!;
                var expected = cluster["inner"] is JsonArray box ? Numbers(box) : null;
                var found = inners.TryGetValue(id, out var ours);
                if (expected is null && found)
                {
                    Issues.Add($"cluster {id} inner: unexpected");
                }
                else if (expected is not null && !found)
                {
                    Issues.Add($"cluster {id} inner: missing");
                }
                else if (expected is not null)
                {
                    Check($"cluster {id} inner", expected, Box(ours));
                }
            }
        }

        /// <summary>
        ///     Gets the kind of a golden part (the names <see cref="SceneQuery.Parts"/> uses).
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns>The kind.</returns>
        private static string PartKind(JsonObject part)
        {
            var tag = (string)part["tag"]!;
            var group = (string?)part["group"];
            return (tag, group) switch
            {
                ("g", "annotation-group text") => "class-annotation",
                ("g", "label-group text") => "class-label",
                ("g", "members-group text") => "class-member",
                ("g", "methods-group text") => "class-method",
                ("g", null) when (string?)part["class"] is "label name" or "label attribute-type" or "label attribute-name" or "label attribute-keys" or "label attribute-comment" =>
                    "er-" + ((string)part["class"]!)["label ".Length..],
                ("g" or "text", _) => "label",
                (_, "divider") => "divider",
                ("line", _) when (string?)part["class"] == "divider" => "divider",
                _ => "path",
            };
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
