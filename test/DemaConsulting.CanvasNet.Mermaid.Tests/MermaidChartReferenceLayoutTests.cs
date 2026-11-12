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
///     legend entry; each golden of <c>Fixtures/reference/gantt/</c> and <c>Fixtures/reference/packet/</c> likewise with
///     the facts of its type (for a packet diagram: the view box, every block's rectangle and paint, every label and bit
///     number, and the title).
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

    /// <summary>The clock of the reference renders (the harness's fixed clock).</summary>
    private static readonly DateTimeOffset ReferenceNow = new(2024, 6, 12, 10, 30, 0, TimeSpan.Zero);

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
    ///     Gets the names of the gantt goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> GanttGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames("gantt"))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every gantt fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Gantt_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames("gantt");

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a gantt fixture out and paints it as the reference does, within the tolerances,
    ///     with the clock of the reference (2024-06-12T10:30:00Z, the harness's fixed clock).
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(GanttGoldens))]
    public void CanvasNetMermaid_GanttReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder("gantt"), name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gantt", (string)golden["source"]!));

        // Act
        var scene = SceneQuery.Layout(text, new MermaidRenderOptions { Now = ReferenceNow });
        var comparison = new Comparison();
        comparison.CompareGantt(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(25)));
    }

    /// <summary>
    ///     Gets the names of the packet goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> PacketGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames("packet"))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every packet fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Packet_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames("packet");

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a packet fixture out and paints it as the reference does, within the tolerances.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(PacketGoldens))]
    public void CanvasNetMermaid_PacketReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder("packet"), name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet", (string)golden["source"]!));

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison();
        comparison.ComparePacket(golden, scene);

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
        ///     Compares every fact of a gantt golden: the view box, the excluded days, each axis's ticks and labels, the
        ///     section rows, the bars (their corners after a milestone's transform) and texts, the section titles, the
        ///     today line and the title. A rectangle the golden gives without area is not drawn, so it is not compared.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void CompareGantt(JsonObject golden, Scene scene)
        {
            var viewBox = scene.ViewBox;
            Check("viewBox", Numbers(golden["viewBox"]), [viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height], DefaultTolerance);
            var items = scene.Walk().ToList();
            List<(SceneItem Item, double X, double Y)> OfRole(string role) => [.. items.Where(entry => entry.Item.Role == role)];

            Rects("exclude", golden["excludes"]!.AsArray(), OfRole("gantt-exclude"));
            Rects("section", golden["sections"]!.AsArray(), OfRole("gantt-section"));
            Rects("task", golden["tasks"]!.AsArray(), OfRole("gantt-task"));

            // The axes: each tick's line and label, with the tick group's opacity
            var axes = OfRole("gantt-axis");
            var expectedAxes = golden["axes"]!.AsArray();
            Count("axes", expectedAxes.Count, axes.Count);
            foreach (var (axis, (item, ax, ay)) in expectedAxes.Zip(axes))
            {
                var group = (SceneGroup)item;
                var (gx, gy) = (ax + group.OffsetX, ay + group.OffsetY);
                Check($"axis {group.Id} origin", Numbers(axis!["origin"]), [gx, gy], DefaultTolerance);
                var lines = group.Children.Where(c => c.Role == "gantt-tick").Cast<ScenePath>().ToList();
                var labels = group.Children.Where(c => c.Role == "gantt-tick-label").Cast<SceneText>().ToList();
                var ticks = axis["ticks"]!.AsArray();
                Count($"axis {group.Id} ticks", ticks.Count, lines.Count);
                Count($"axis {group.Id} labels", ticks.Count(t => ((string)t!["text"]!["text"]!).Length > 0), labels.Count);
                foreach (var (tick, line) in ticks.Zip(lines))
                {
                    var what = $"axis {group.Id} tick '{(string)tick!["text"]!["text"]!}'";
                    Check($"{what} line", [.. Numbers(tick["at"]), .. Numbers(tick["end"])], [line.Segments[0].X + gx, line.Segments[0].Y + gy, line.Segments[1].X + gx, line.Segments[1].Y + gy], DefaultTolerance);
                    var opacity = (double)tick["tick"]!["opacity"]! * (double)tick["line"]!["opacity"]!;
                    Color($"{what} line stroke", (string)tick["line"]!["stroke"]!, opacity, line.Stroke?.Color);
                }

                foreach (var (tick, label) in ticks.Where(t => ((string)t!["text"]!["text"]!).Length > 0).Zip(labels))
                {
                    GanttText($"axis {group.Id} label", tick!["text"]!, label, gx, gy, (double)tick["tick"]!["opacity"]!);
                }
            }

            // The task texts, the section titles, the today line and the title
            var texts = OfRole("gantt-task-text");
            var expectedTexts = golden["texts"]!.AsArray().Where(t => ((string)t!["text"]!).Trim().Length > 0).ToList();
            Count("task texts", expectedTexts.Count, texts.Count);
            foreach (var (text, (item, x, y)) in expectedTexts.Zip(texts))
            {
                Same("task text id", (string)text!["id"]!, item.Id);
                GanttText($"task text {item.Id}", text, (SceneText)item, x, y, 1);
            }

            var titles = OfRole("gantt-section-title");
            var expectedLines = golden["sectionTitles"]!.AsArray()
                .SelectMany(title => title!["lines"]!.AsArray().Select(line => (Title: title, Line: line!)))
                .Where(pair => ((string)pair.Line["text"]!).Trim().Length > 0)
                .ToList();
            Count("section title lines", expectedLines.Count, titles.Count);
            foreach (var ((title, line), (item, x, y)) in expectedLines.Zip(titles))
            {
                var ours = (SceneText)item;
                var content = (string)line["text"]!;
                Same("section title text", content.Trim(), string.Concat(ours.Runs.Select(run => run.Text)));
                Check($"section title '{content}' box", Numbers(line["box"]), Box(ours.Box.Offset(x, y)), TextTolerance);
                Check($"section title '{content}' font size", [(double)title["fontSize"]!], [ours.Runs[0].Font.Size], DefaultTolerance);
                Color($"section title '{content}' fill", (string)title["fill"]!, 1, ours.Runs[0].Color);
            }

            var today = OfRole("gantt-today");
            if (golden["today"] is JsonObject expectedToday)
            {
                if (today.Count == 1 && today[0].Item is ScenePath line)
                {
                    var (x, y) = (today[0].X, today[0].Y);
                    Check("today line", [.. Numbers(expectedToday["start"]), .. Numbers(expectedToday["end"])], [line.Segments[0].X + x, line.Segments[0].Y + y, line.Segments[1].X + x, line.Segments[1].Y + y], DefaultTolerance);
                    var paint = expectedToday["paint"]!;
                    Color("today stroke", (string)paint["stroke"]!, (double)paint["opacity"]!, line.Stroke?.Color);
                    Check("today stroke width", [(double)paint["strokeWidth"]!], [line.Stroke?.Width ?? 0], DefaultTolerance);
                }
                else
                {
                    Issues.Add($"today: 1 line expected, {today.Count} found");
                }
            }
            else
            {
                Count("today", 0, today.Count);
            }

            var title2 = OfRole("title");
            if (golden["title"] is JsonObject expectedTitle && ((string)expectedTitle["text"]!).Length > 0)
            {
                if (title2.Count == 1)
                {
                    GanttText("title", expectedTitle, (SceneText)title2[0].Item, title2[0].X, title2[0].Y, 1);
                }
                else
                {
                    Issues.Add("title: missing");
                }
            }
            else
            {
                Count("title", 0, title2.Count);
            }
        }

        /// <summary>
        ///     Compares every fact of a packet golden: the view box, the rows, every drawn block's rectangle and paint, every
        ///     text that shows something (an empty text is no item of the scene) and the title.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void ComparePacket(JsonObject golden, Scene scene)
        {
            var viewBox = scene.ViewBox;
            Check("viewBox", Numbers(golden["viewBox"]), [viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height], DefaultTolerance);

            // The rows, then their blocks and texts in order
            var items = scene.Walk().ToList();
            var goldenRows = golden["rows"]!.AsArray();
            Count("rows", goldenRows.Count, items.Count(entry => entry.Item.Role == "packet-row"));
            var blocks = items.Where(entry => entry.Item.Role == "packet-block").ToList();
            var goldenBlocks = goldenRows.SelectMany(row => row!["blocks"]!.AsArray())
                .Where(block => Numbers(block!["rect"]) is [_, _, > 0, > 0])
                .ToList();
            Count("blocks", goldenBlocks.Count, blocks.Count);
            foreach (var (block, (item, x, y)) in goldenBlocks.Zip(blocks))
            {
                var path = (ScenePath)item;
                var rect = Numbers(block!["rect"]);
                var label = $"block {path.Id}";
                Check($"{label} rect", rect, Scene.BoundsOf([path]) is { } b ? [b.X + x, b.Y + y, b.Width, b.Height] : [], DefaultTolerance);
                var paint = block["paint"]!;
                Color($"{label} fill", (string)paint["fill"]!, 1, path.Fill);
                var stroke = (string)paint["stroke"]!;
                Color($"{label} stroke", stroke, 1, path.Stroke?.Color);
                if (stroke != "none" && path.Stroke is { } ours)
                {
                    Check($"{label} stroke width", [(double)paint["strokeWidth"]!], [ours.Width], DefaultTolerance);
                }
            }

            var roles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packetLabel"] = "packet-label",
                ["packetByte start"] = "packet-bit-start",
                ["packetByte end"] = "packet-bit-end",
            };
            var texts = items.Where(entry => entry.Item is SceneText && entry.Item.Role != "title").ToList();
            var goldenTexts = goldenRows.SelectMany(row => row!["texts"]!.AsArray()).Where(text => text!["box"] is not null).ToList();
            Count("texts", goldenTexts.Count, texts.Count);
            foreach (var (text, (item, x, y)) in goldenTexts.Zip(texts))
            {
                Same("text role", roles[(string)text!["class"]!], item.Role);
                PacketText((string)text["class"]!, text, (SceneText)item, x, y);
            }

            // The title, drawn when it shows something
            var titles = items.Where(entry => entry.Item.Role == "title").ToList();
            var goldenTitle = golden["title"]!;
            Count("titles", ((string)goldenTitle["text"]!).Length > 0 ? 1 : 0, titles.Count);
            if (titles.Count == 1 && ((string)goldenTitle["text"]!).Length > 0)
            {
                PacketText("title", goldenTitle, (SceneText)titles[0].Item, titles[0].OffsetX, titles[0].OffsetY);
            }
        }

        /// <summary>
        ///     Compares a packet text: its content (white space collapsed), the x of its anchor (and the y of an
        ///     <c>auto</c> baseline, which is the baseline; a <c>middle</c> baseline is checked by the box), anchor, box,
        ///     font size and color.
        /// </summary>
        /// <param name="what">The text.</param>
        /// <param name="golden">The golden text.</param>
        /// <param name="line">The scene's text.</param>
        /// <param name="dx">The absolute offset of the text.</param>
        /// <param name="dy">The absolute offset of the text.</param>
        private void PacketText(string what, JsonNode golden, SceneText line, double dx, double dy)
        {
            var content = System.Text.RegularExpressions.Regex.Replace((string)golden["text"]!, "\\s+", " ").Trim();
            Same($"{what} text", content, string.Concat(line.Runs.Select(run => run.Text)));
            var at = Numbers(golden["at"]);
            if ((string)golden["baseline"]! == "auto")
            {
                Check($"{what} '{content}' at", at, [dx + line.X, dy + line.Baseline], DefaultTolerance);
            }
            else
            {
                Check($"{what} '{content}' at x", [at[0]], [dx + line.X], DefaultTolerance);
            }

            Same($"{what} '{content}' anchor", (string)golden["anchor"]!, line.Anchor.ToString().ToLowerInvariant());
            Check($"{what} '{content}' box", Numbers(golden["box"]), Box(line.Box.Offset(dx, dy)), TextTolerance);
            Check($"{what} '{content}' font size", [(double)golden["fontSize"]!], [line.Runs[0].Font.Size], DefaultTolerance);
            Color($"{what} '{content}' fill", (string)golden["fill"]!, 1, line.Runs[0].Color);
        }

        /// <summary>
        ///     Compares rectangles of a gantt chart: their corners (after a transform) and their paint; a golden
        ///     rectangle without area is skipped, as it is not drawn.
        /// </summary>
        /// <param name="what">The kind of rectangle.</param>
        /// <param name="expected">The golden rectangles.</param>
        /// <param name="actual">The scene's paths with their offsets.</param>
        private void Rects(string what, JsonArray expected, List<(SceneItem Item, double X, double Y)> actual)
        {
            var drawn = expected.Where(rect =>
            {
                var corners = rect!["corners"]!.AsArray().Select(Numbers).ToList();
                var width = Math.Sqrt(Math.Pow(corners[1][0] - corners[0][0], 2) + Math.Pow(corners[1][1] - corners[0][1], 2));
                var height = Math.Sqrt(Math.Pow(corners[3][0] - corners[0][0], 2) + Math.Pow(corners[3][1] - corners[0][1], 2));
                return width > 0 && height > 0;
            }).ToList();
            Count(what + "s", drawn.Count, actual.Count);
            foreach (var (rect, (item, x, y)) in drawn.Zip(actual))
            {
                var path = (ScenePath)item;
                var label = $"{what} {rect!["id"] ?? rect["class"]}";
                Check($"{label} corners", [.. rect["corners"]!.AsArray().SelectMany(Numbers)], Corners(path, x, y), DefaultTolerance);
                var paint = rect["paint"]!;
                var opacity = (double)paint["opacity"]!;
                Color($"{label} fill", (string)paint["fill"]!, opacity, path.Fill);
                var stroke = (string)paint["stroke"]!;
                Color($"{label} stroke", stroke, opacity, path.Stroke?.Color);
                if (stroke != "none" && path.Stroke is { } ours)
                {
                    var scale = ((string?)rect["class"])?.Split(' ').Contains("milestone") == true ? 0.8 : 1;
                    Check($"{label} stroke width", [(double)paint["strokeWidth"]! * scale], [ours.Width], DefaultTolerance);
                }
            }
        }

        /// <summary>
        ///     Gets the corners of a rectangle path (rounded or not, possibly turned): the intersections of its edges, from
        ///     the top left clockwise.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="dx">The absolute offset.</param>
        /// <param name="dy">The absolute offset.</param>
        /// <returns>The corners as x and y pairs.</returns>
        private static double[] Corners(ScenePath path, double dx, double dy)
        {
            var s = path.Segments;
            var axisAligned = Math.Abs(s[0].Y - s[1].Y) < 1e-9 && s.Where(seg => seg.Kind != SceneSegmentKind.Close).All(seg => seg.Kind != SceneSegmentKind.ArcTo || Math.Abs(seg.X2) < 1e-9);
            if (axisAligned && Scene.BoundsOf([path]) is { } b)
            {
                return [b.X + dx, b.Y + dy, b.Right + dx, b.Y + dy, b.Right + dx, b.Bottom + dy, b.X + dx, b.Bottom + dy];
            }

            // A rounded rectangle: top (0, 1), right (2, 3), bottom (4, 5) and left (6, 7) edges
            (double X, double Y) Meet(int a, int b, int c, int d)
            {
                var (x1, y1, x2, y2) = (s[a].X, s[a].Y, s[b].X, s[b].Y);
                var (x3, y3, x4, y4) = (s[c].X, s[c].Y, s[d].X, s[d].Y);
                var den = ((x1 - x2) * (y3 - y4)) - ((y1 - y2) * (x3 - x4));
                var t = (((x1 - x3) * (y3 - y4)) - ((y1 - y3) * (x3 - x4))) / den;
                return (x1 + (t * (x2 - x1)) + dx, y1 + (t * (y2 - y1)) + dy);
            }

            var tl = Meet(6, 7, 0, 1);
            var tr = Meet(0, 1, 2, 3);
            var br = Meet(2, 3, 4, 5);
            var bl = Meet(4, 5, 6, 7);
            return [tl.X, tl.Y, tr.X, tr.Y, br.X, br.Y, bl.X, bl.Y];
        }

        /// <summary>
        ///     Compares a text of a gantt chart: its content (white space collapsed), anchor point (its baseline moved by
        ///     its <c>dy</c>), anchor, box, font size, weight, style and color under a group opacity.
        /// </summary>
        /// <param name="what">The text.</param>
        /// <param name="golden">The golden text.</param>
        /// <param name="line">The scene's text.</param>
        /// <param name="dx">The absolute offset of the text.</param>
        /// <param name="dy">The absolute offset of the text.</param>
        /// <param name="opacity">The opacity of the text's groups.</param>
        private void GanttText(string what, JsonNode golden, SceneText line, double dx, double dy, double opacity)
        {
            var content = System.Text.RegularExpressions.Regex.Replace((string)golden["text"]!, "\\s+", " ").Trim();
            Same($"{what} text", content, string.Concat(line.Runs.Select(run => run.Text)));
            var fontSize = (double)golden["fontSize"]!;
            var em = (string?)golden["dy"] is { } dyText && dyText.EndsWith("em", StringComparison.Ordinal)
                ? double.Parse(dyText[..^2], CultureInfo.InvariantCulture) * fontSize
                : 0;
            var at = Numbers(golden["at"]);
            Check($"{what} '{content}' at", [at[0], at[1] + em], [dx + line.X, dy + line.Baseline], DefaultTolerance);
            Same($"{what} '{content}' anchor", (string)golden["anchor"]!, line.Anchor.ToString().ToLowerInvariant());
            if (line.Runs.Count == 0)
            {
                return;
            }

            Check($"{what} '{content}' box", Numbers(golden["box"]), Box(line.Box.Offset(dx, dy)), TextTolerance);
            var font = line.Runs[0].Font;
            Check($"{what} '{content}' font size", [fontSize], [font.Size], DefaultTolerance);
            Same($"{what} '{content}' bold", int.Parse((string)golden["fontWeight"]!, CultureInfo.InvariantCulture) >= 600 ? "bold" : "normal", font.Bold ? "bold" : "normal");
            Same($"{what} '{content}' italic", (string)golden["fontStyle"]! == "italic" ? "italic" : "normal", font.Italic ? "italic" : "normal");
            Color($"{what} '{content}' fill", (string)golden["fill"]!, opacity, line.Runs[0].Color);
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
