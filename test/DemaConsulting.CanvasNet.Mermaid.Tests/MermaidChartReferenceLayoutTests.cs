using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the chart layouts against the reference implementation: each golden of
///     <c>Fixtures/reference/pie/</c> (the layout that the pinned mermaid-cli and mermaid draw in headless Chrome with
///     Liberation Sans, see <c>Fixtures/reference/README.md</c>) is compared with CanvasNet's scene: the view box, the
///     pie's place, the outer circle, every slice's path data, box and paint, every percentage label, the title and every
///     legend entry.
/// </summary>
public class MermaidChartReferenceLayoutTests
{
    /// <summary>
    ///     The tolerance in pixels of every position and size except text and arc boxes (the goldens are rounded to
    ///     0.001 px).
    /// </summary>
    private const double DefaultTolerance = 0.01;

    /// <summary>
    ///     The tolerance in pixels of text boxes: Chrome snaps the baseline of a text to its layout unit of 1/64 pixel, so a
    ///     box can be 1/64 pixel taller than the rounded font metrics.
    /// </summary>
    private const double TextTolerance = 0.02;

    /// <summary>
    ///     The tolerance in pixels of the box of a slice: Chrome's <c>getBBox</c> of an elliptical arc can stop up to
    ///     0.02 pixel short of the circle's extreme (its arc approximation), while the path data is the same.
    /// </summary>
    private const double ArcTolerance = 0.02;

    /// <summary>
    ///     Gets the names of the pie goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> PieGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames("pie"))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every pie fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Pie_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames("pie");

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a pie fixture out and paints it as the reference does, within the tolerances.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(PieGoldens))]
    public void CanvasNetMermaid_PieReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder("pie"), name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie", (string)golden["source"]!));

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison();
        comparison.ComparePie(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(25)));
    }

    /// <summary>
    ///     Gets the folder of the goldens of a diagram type.
    /// </summary>
    /// <param name="type">The diagram type.</param>
    /// <returns>The folder.</returns>
    private static string ReferenceFolder(string type) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", type);

    /// <summary>
    ///     Gets the names of the goldens of a diagram type in order.
    /// </summary>
    /// <param name="type">The diagram type.</param>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames(string type) =>
        Directory.GetFiles(ReferenceFolder(type), "*.layout.json")
            .Select(file => Path.GetFileName(file)[..^".layout.json".Length])
            .Order(StringComparer.Ordinal);

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
        ///     Compares every fact of a pie golden.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void ComparePie(JsonObject golden, Scene scene)
        {
            var viewBox = scene.ViewBox;
            Check("viewBox", Numbers(golden["viewBox"]), [viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height], DefaultTolerance);

            // Every item with its absolute offset
            var items = scene.Walk().ToList();
            (SceneItem Item, double X, double Y)? Single(string role)
            {
                var found = items.Where(entry => entry.Item.Role == role).ToList();
                return found.Count == 1 ? found[0] : null;
            }

            // The pie group and its outer circle
            if (Single("pie-chart") is not { Item: SceneGroup chart } chartEntry)
            {
                Issues.Add("pie-chart: missing");
                return;
            }

            var (originX, originY) = (chartEntry.X + chart.OffsetX, chartEntry.Y + chart.OffsetY);
            Check("origin", Numbers(golden["origin"]), [originX, originY], DefaultTolerance);
            if (Single("pie-outer-circle") is { Item: ScenePath circle } circleEntry && Scene.BoundsOf([circle]) is { } bounds)
            {
                var outer = golden["outerCircle"]!;
                var box = bounds.Offset(circleEntry.X, circleEntry.Y);
                Check("outer circle center", Numbers(outer["center"]), [box.X + (box.Width / 2), box.Y + (box.Height / 2)], DefaultTolerance);
                Check("outer circle r", [(double)outer["r"]!], [box.Width / 2], DefaultTolerance);
                Paint("outer circle", outer["paint"]!, circle);
            }
            else
            {
                Issues.Add("outer circle: missing");
            }

            // The slices in order: their path data (scaled as a highlighted slice is), box and paint
            var slices = items.Where(entry => entry.Item.Role == "pie-slice").ToList();
            var expectedSlices = golden["slices"]!.AsArray();
            Count("slices", expectedSlices.Count, slices.Count);
            foreach (var (slice, (item, x, y)) in expectedSlices.Zip(slices))
            {
                var path = (ScenePath)item;
                var what = $"slice {path.Id}";
                var scale = ScaleOf(slice!["paint"]!);
                Check($"{what} path", PathNumbers((string)slice["d"]!, scale), SegmentNumbers(path.Segments), DefaultTolerance);
                Check($"{what} box", Numbers(slice["box"]), Box(ArcBounds(path.Segments)?.Offset(x, y)), ArcTolerance);
                Paint(what, slice["paint"]!, path);
            }

            // The percentage labels, the title and the legend
            var labels = items.Where(entry => entry.Item.Role == "pie-slice-label" && entry.Item is SceneText).ToList();
            var expectedLabels = golden["labels"]!.AsArray();
            Count("labels", expectedLabels.Count, labels.Count);
            foreach (var (label, (item, x, y)) in expectedLabels.Zip(labels))
            {
                Text($"label {item.Id}", label!, (SceneText)item, x, y);
            }

            if (golden["title"] is { } title && Single("title") is { Item: SceneText ourTitle } titleEntry)
            {
                Text("title", title, ourTitle, titleEntry.X, titleEntry.Y);
            }
            else if (golden["title"] is not null)
            {
                Issues.Add("title: missing");
            }

            var legend = items.Where(entry => entry.Item.Role == "pie-legend").ToList();
            var expectedLegend = golden["legend"]!.AsArray();
            Count("legend", expectedLegend.Count, legend.Count);
            foreach (var (entry, (item, x, y)) in expectedLegend.Zip(legend))
            {
                var group = (SceneGroup)item;
                var (gx, gy) = (x + group.OffsetX, y + group.OffsetY);
                var what = $"legend {group.Id}";
                Check($"{what} at", Numbers(entry!["at"]), [gx, gy], DefaultTolerance);
                var rect = group.Children.OfType<ScenePath>().Single();
                Check($"{what} rect", Numbers(entry["rect"]!["box"]), Box(Scene.BoundsOf([rect])?.Offset(gx, gy)), DefaultTolerance);
                Paint($"{what} rect", entry["rect"]!["paint"]!, rect);
                Text($"{what} text", entry["text"]!, group.Children.OfType<SceneText>().Single(), gx, gy);
            }
        }

        /// <summary>
        ///     Gets the CSS <c>scale</c> of a computed paint (<c>none</c> is 1).
        /// </summary>
        /// <param name="paint">The golden paint.</param>
        /// <returns>The scale.</returns>
        private static double ScaleOf(JsonNode paint) =>
            (string?)paint["scale"] is { } text && text != "none" ? double.Parse(text, CultureInfo.InvariantCulture) : 1;

        /// <summary>
        ///     Reads the numbers of SVG path data written by d3-path (commands followed by comma-separated numbers), the
        ///     coordinates and radii scaled and the arc flags and rotation kept.
        /// </summary>
        /// <param name="data">The path data.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The numbers in order.</returns>
        private static double[] PathNumbers(string data, double scale)
        {
            var numbers = new List<double>();
            var command = ' ';
            var index = 0;
            foreach (var token in System.Text.RegularExpressions.Regex.Split(data, "(?=[A-Za-z])|,").Where(t => t.Length > 0))
            {
                var value = token;
                if (char.IsLetter(token[0]))
                {
                    command = token[0];
                    index = 0;
                    value = token[1..];
                    if (value.Length == 0)
                    {
                        continue;
                    }
                }

                var number = double.Parse(value, CultureInfo.InvariantCulture);
                var kept = command == 'A' && index is 2 or 3 or 4;
                numbers.Add(kept ? number : number * scale);
                index++;
            }

            return [.. numbers];
        }

        /// <summary>
        ///     Writes the numbers of segments in the order of their path data.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <returns>The numbers.</returns>
        private static double[] SegmentNumbers(IReadOnlyList<SceneSegment> segments) =>
        [
            .. segments.SelectMany(s => s.Kind switch
            {
                SceneSegmentKind.ArcTo => new[] { s.X1, s.Y1, s.X2, s.LargeArc ? 1 : 0, s.Sweep ? 1 : 0, s.X, s.Y },
                SceneSegmentKind.Close => [],
                _ => [s.X, s.Y],
            }),
        ];

        /// <summary>
        ///     Gets the exact bounds of a slice outline (moves, lines and arcs of circles around the origin), as the
        ///     browser's <c>getBBox</c> gives them: the points of the path and the extremes of each arc.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <returns>The bounds, or null for an empty path.</returns>
        private static SceneRect? ArcBounds(IReadOnlyList<SceneSegment> segments)
        {
            var xs = new List<double>();
            var ys = new List<double>();
            var (cx, cy) = (0.0, 0.0);
            foreach (var segment in segments.Where(s => s.Kind != SceneSegmentKind.Close))
            {
                if (segment.Kind == SceneSegmentKind.ArcTo)
                {
                    // The extremes on the axes that the arc passes, from the start angle in the direction of the sweep
                    var r = segment.X1;
                    var from = Math.Atan2(cy, cx);
                    var sweep = Math.Atan2(segment.Y, segment.X) - from;
                    if (segment.Sweep && sweep <= 0)
                    {
                        sweep += 2 * Math.PI;
                    }
                    else if (!segment.Sweep && sweep >= 0)
                    {
                        sweep -= 2 * Math.PI;
                    }
                    for (var quarter = -8; quarter <= 8; quarter++)
                    {
                        var angle = quarter * Math.PI / 2;
                        var t = (angle - from) / sweep;
                        if (t > 0 && t < 1)
                        {
                            xs.Add(r * Math.Cos(angle));
                            ys.Add(r * Math.Sin(angle));
                        }
                    }
                }

                (cx, cy) = (segment.X, segment.Y);
                xs.Add(cx);
                ys.Add(cy);
            }

            return xs.Count == 0 ? null : new SceneRect(xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
        }

        /// <summary>
        ///     Compares a text: its content, anchor point, anchor, box, font size, weight and color.
        /// </summary>
        /// <param name="what">The text.</param>
        /// <param name="golden">The golden text.</param>
        /// <param name="line">The scene's text.</param>
        /// <param name="dx">The absolute offset of the text.</param>
        /// <param name="dy">The absolute offset of the text.</param>
        private void Text(string what, JsonNode golden, SceneText line, double dx, double dy)
        {
            var content = (string)golden["text"]!;
            Same($"{what} text", content, string.Concat(line.Runs.Select(run => run.Text)));
            Check($"{what} '{content}' at", Numbers(golden["at"]), [dx + line.X, dy + line.Baseline], DefaultTolerance);
            Same($"{what} '{content}' anchor", (string)golden["anchor"]!, line.Anchor.ToString().ToLowerInvariant());
            if (line.Runs.Count == 0)
            {
                return;
            }

            Check($"{what} '{content}' box", Numbers(golden["box"]), Box(line.Box.Offset(dx, dy)), TextTolerance);
            var font = line.Runs[0].Font;
            Check($"{what} '{content}' font size", [(double)golden["fontSize"]!], [font.Size], DefaultTolerance);
            Same($"{what} '{content}' bold", int.Parse((string)golden["fontWeight"]!, CultureInfo.InvariantCulture) >= 600 ? "bold" : "normal", font.Bold ? "bold" : "normal");
            Color($"{what} '{content}' fill", (string)golden["fill"]!, 1, line.Runs[0].Color);
        }

        /// <summary>
        ///     Compares the paint of a path: its fill and stroke with the opacity folded in, and its stroke width with the
        ///     scale.
        /// </summary>
        /// <param name="what">The path.</param>
        /// <param name="golden">The golden paint.</param>
        /// <param name="path">The scene's path.</param>
        private void Paint(string what, JsonNode golden, ScenePath path)
        {
            var opacity = (double)golden["opacity"]!;
            Color($"{what} fill", (string)golden["fill"]!, opacity, path.Fill);
            var stroke = (string)golden["stroke"]!;
            Color($"{what} stroke", stroke, opacity, path.Stroke?.Color);
            if (stroke != "none" && path.Stroke is { } ours)
            {
                Check($"{what} stroke width", [(double)golden["strokeWidth"]! * ScaleOf(golden)], [ours.Width], DefaultTolerance);
            }
        }

        /// <summary>
        ///     Compares a CSS color of the golden (<c>rgb()</c>, <c>rgba()</c> or <c>none</c>) under an opacity with a scene
        ///     color.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="golden">The golden color.</param>
        /// <param name="opacity">The element opacity.</param>
        /// <param name="ours">The scene's color, or null for none.</param>
        private void Color(string what, string golden, double opacity, Rgba32? ours)
        {
            Rgba32? expected = golden == "none" ? null : CssColor.Parse(golden);
            if (expected is { } color)
            {
                expected = color with { A = (byte)Math.Round(color.A * opacity, MidpointRounding.AwayFromZero) };
            }

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
                Issues.Add($"{what}: expected {golden} at opacity {opacity}, got {(ours is { } c ? $"rgba({c.R}, {c.G}, {c.B}, {c.A})" : "none")}");
            }
        }

        /// <summary>
        ///     Checks that two values agree.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden value.</param>
        /// <param name="actual">The scene's value.</param>
        private void Same(string what, string? expected, string? actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                Issues.Add($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        /// <summary>
        ///     Checks that two counts agree.
        /// </summary>
        /// <param name="what">The items.</param>
        /// <param name="expected">The golden count.</param>
        /// <param name="actual">The scene's count.</param>
        private void Count(string what, int expected, int actual)
        {
            if (expected != actual)
            {
                Issues.Add($"{what}: {expected} expected, {actual} found");
            }
        }

        /// <summary>
        ///     Checks numbers against the golden's.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden numbers.</param>
        /// <param name="actual">The scene's numbers.</param>
        /// <param name="tolerance">The tolerance.</param>
        private void Check(string what, double[] expected, double[] actual, double tolerance)
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

        /// <summary>Gets the numbers of a JSON array.</summary>
        /// <param name="node">The array.</param>
        /// <returns>The numbers.</returns>
        private static double[] Numbers(JsonNode? node) => [.. node!.AsArray().Select(item => (double)item!)];

        /// <summary>Gets a box as numbers.</summary>
        /// <param name="box">The box, or null for none.</param>
        /// <returns>X, y, width and height.</returns>
        private static double[] Box(SceneRect? box) => box is { } b ? [b.X, b.Y, b.Width, b.Height] : [];

        /// <summary>Formats numbers for a message.</summary>
        /// <param name="values">The numbers.</param>
        /// <returns>The text.</returns>
        private static string Format(double[] values) =>
            string.Join(", ", values.Take(8).Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))) + (values.Length > 8 ? ", ..." : string.Empty);
    }
}
