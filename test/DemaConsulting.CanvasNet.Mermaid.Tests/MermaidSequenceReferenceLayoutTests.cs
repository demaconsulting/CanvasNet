using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the sequence layout against the reference implementation: each golden of
///     <c>Fixtures/reference/sequence/</c> (the layout that the pinned mermaid-cli and mermaid draw in headless Chrome with
///     Liberation Sans, see <c>Fixtures/reference/README.md</c>) is compared with CanvasNet's scene: the view box, the title,
///     every participant's drawings, parts, paint and labels, the lifelines, every message's line or self loop, markers,
///     text, central-connection circles and sequence number, the notes, the activations, the block frames with their
///     label boxes and titles, the <c>rect</c> backgrounds, the boxes with their shadows and titles, the menus, and the
///     paint order.
/// </summary>
public class MermaidSequenceReferenceLayoutTests
{
    /// <summary>
    ///     The tolerance in pixels of every position and size except text boxes (the goldens are rounded to 0.001 px).
    /// </summary>
    private const double DefaultTolerance = 0.01;

    /// <summary>
    ///     The tolerance in pixels of text boxes: Chrome snaps the baseline of a text to its layout unit of 1/64 pixel, so a
    ///     box can be 1/64 pixel taller than the rounded font metrics; the anchors of texts are compared at
    ///     <see cref="DefaultTolerance"/>.
    /// </summary>
    private const double TextTolerance = 0.02;

    /// <summary>
    ///     The tolerance in pixels of the box of a cylinder (a <c>queue</c> or <c>database</c> path of elliptical arcs):
    ///     Chrome's <c>getBBox</c> of an arc far from the origin can stop up to 0.02 pixel short of the ellipse's extreme
    ///     (its arc approximation; the mirrored queue at y 481 shows 0.017), while the geometry is the same.
    /// </summary>
    private const double ArcTolerance = 0.02;

    /// <summary>The number of intervals the goldens sample each self loop at.</summary>
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
    ///     Validates that every sequence fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Sequence_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames();

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a sequence fixture out and paints it as the reference does, within the
    ///     tolerances.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Goldens))]
    public void CanvasNetMermaid_SequenceReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", (string)golden["source"]!));

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison();
        comparison.Compare(golden, scene);

        // Assert
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s) (largest {comparison.Largest:0.###} px):\n" +
            string.Join('\n', comparison.Issues.Take(25)));
    }

    /// <summary>Gets the folder of the goldens.</summary>
    private static string ReferenceFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "sequence");

    /// <summary>
    ///     Gets the names of the goldens in order.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames() =>
        Directory.GetFiles(ReferenceFolder, "*.layout.json")
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
        ///     Compares every fact of the golden.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void Compare(JsonObject golden, Scene scene)
        {
            var viewBox = scene.ViewBox;
            Check("viewBox", Numbers(golden["viewBox"]), [viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height], DefaultTolerance);
            var items = SceneQuery.SequenceItems(scene);

            // The title
            var title = golden["title"];
            var ours = items.WithRole("title").OfType<SceneText>().ToList();
            if (title is null)
            {
                Count("title", 0, ours.Count);
            }
            else
            {
                Texts("title", new JsonArray(title.DeepClone()), ours);
            }

            // The participants and lifelines
            foreach (var actor in golden["actors"]!.AsArray())
            {
                var name = (string)actor!["name"]!;
                Drawing($"actor {name} top", actor["top"], items.Find("actor-top", name));
                Drawing($"actor {name} bottom", actor["bottom"], items.Find("actor-bottom", name));
                if (actor["lifeline"] is { } lifeline)
                {
                    if (items.Find("lifeline", name) is ScenePath path)
                    {
                        Check($"lifeline {name}", Numbers(lifeline["line"]), [path.Segments[0].X, path.Segments[0].Y, path.Segments[1].X, path.Segments[1].Y], DefaultTolerance);
                        Paint($"lifeline {name}", lifeline["paint"], path);
                    }
                    else
                    {
                        Issues.Add($"lifeline {name}: missing");
                    }
                }
            }

            // The messages
            foreach (var message in golden["messages"]!.AsArray())
            {
                Message(message!.AsObject(), items);
            }

            // The notes
            foreach (var note in golden["notes"]!.AsArray())
            {
                var id = (string)note!["id"]!;
                if (items.Find("note", id) is not SceneGroup group)
                {
                    Issues.Add($"note {id}: missing");
                    continue;
                }

                var rect = (ScenePath)group.Children[0];
                Check($"note {id}", Numbers(note["box"]), Box(Scene.BoundsOf([rect])), DefaultTolerance);
                Paint($"note {id}", note["paint"], rect);
                Texts($"note {id}", note["texts"]!.AsArray(), [.. group.Children.OfType<SceneText>()]);
            }

            // The activations, in paint order
            var activations = items.WithRole("activation").OfType<ScenePath>().ToList();
            var expected = golden["activations"]!.AsArray();
            Count("activations", expected.Count, activations.Count);
            foreach (var (activation, path) in expected.Zip(activations))
            {
                Same("activation class", (string)activation!["class"]!, path.Id);
                Check($"activation {path.Id}", Numbers(activation["box"]), Box(Scene.BoundsOf([path])), DefaultTolerance);
                Paint($"activation {path.Id}", activation["paint"], path);
            }

            // The block frames, rect backgrounds, boxes and menus
            foreach (var frame in golden["frames"]!.AsArray())
            {
                Frame(frame!.AsObject(), items);
            }

            var rects = scene.Items.Where(item => item.Role == "rect").OfType<ScenePath>().ToList();
            var goldenRects = golden["rects"]!.AsArray();
            Count("rects", goldenRects.Count, rects.Count);
            foreach (var (rect, path) in goldenRects.Zip(rects))
            {
                Check("rect", Numbers(rect!["box"]), Box(Scene.BoundsOf([path])), DefaultTolerance);
                Paint("rect", rect["paint"], path);
            }

            var boxes = scene.Items.Where(item => item.Role == "box").OfType<SceneGroup>().ToList();
            var goldenBoxes = golden["boxes"]!.AsArray();
            Count("boxes", goldenBoxes.Count, boxes.Count);
            foreach (var (box, group) in goldenBoxes.Zip(boxes))
            {
                var rect = (ScenePath)group.Children[0];
                Check("box", Numbers(box!["box"]), Box(Scene.BoundsOf([rect])), DefaultTolerance);
                Paint("box", box["paint"], rect);
                Texts("box", box["texts"]!.AsArray(), [.. group.Children.OfType<SceneText>()]);
            }

            foreach (var menu in golden["menus"]!.AsArray())
            {
                Menu(menu!.AsObject(), items);
            }

            // The paint order; the harness also lists a menu panel of a mirrored participant as an unnamed bottom drawing
            // (the panel has the participant box's classes, actor-bottom among them), which is left out
            var keys = golden["order"]!.AsArray().Select(key => (string)key!).ToList();
            var order = keys.Where((key, i) => !(key == "actor-bottom:null" && i + 1 < keys.Count && keys[i + 1].StartsWith("menu:", StringComparison.Ordinal))).ToList();
            if (!order.SequenceEqual(items.Order))
            {
                Issues.Add($"order: expected [{string.Join(", ", order)}], got [{string.Join(", ", items.Order)}]");
            }
        }

        /// <summary>
        ///     Compares a block frame: its sides and section separators, its label box, and its texts.
        /// </summary>
        /// <param name="frame">The golden frame.</param>
        /// <param name="items">The scene's items.</param>
        private void Frame(JsonObject frame, SceneQuery.SequenceScene items)
        {
            var id = (string)frame["id"]!;
            if (items.Find("frame", id) is not SceneGroup group)
            {
                Issues.Add($"frame {id}: missing");
                return;
            }

            var lines = group.Children.Where(child => child.Role == "frame-line").OfType<ScenePath>().ToList();
            var expected = frame["lines"]!.AsArray();
            Count($"frame {id} lines", expected.Count, lines.Count);
            foreach (var (line, path) in expected.Zip(lines))
            {
                Check($"frame {id} line", Numbers(line!["line"]), [path.Segments[0].X, path.Segments[0].Y, path.Segments[1].X, path.Segments[1].Y], DefaultTolerance);
                Paint($"frame {id} line", line["paint"], path);
            }

            if (frame["labelBox"] is { } labelBox && group.Children.FirstOrDefault(child => child.Role == "frame-label-box") is ScenePath polygon)
            {
                var points = labelBox["points"]!.AsArray().SelectMany(Numbers).ToArray();
                Check($"frame {id} label box", points, [.. polygon.Segments.Take(5).SelectMany(s => new[] { s.X, s.Y })], DefaultTolerance);
                Paint($"frame {id} label box", labelBox["paint"], polygon);
            }
            else
            {
                Issues.Add($"frame {id} label box: missing");
            }

            Texts($"frame {id}", frame["texts"]!.AsArray(), [.. group.Children.OfType<SceneText>()]);
        }

        /// <summary>
        ///     Compares a participant's menu: hidden (an empty group), or its panel, paint and link texts.
        /// </summary>
        /// <param name="menu">The golden menu.</param>
        /// <param name="items">The scene's items.</param>
        private void Menu(JsonObject menu, SceneQuery.SequenceScene items)
        {
            var id = (string)menu["id"]!;
            if (items.Find("menu", id) is not SceneGroup group)
            {
                Issues.Add($"menu {id}: missing");
                return;
            }

            if (!(bool)menu["displayed"]!)
            {
                Count($"menu {id} items", 0, group.Children.Count);
                return;
            }

            var panel = group.Children.OfType<ScenePath>().FirstOrDefault(child => child.Role == "menu-panel");
            if (panel is null)
            {
                Issues.Add($"menu {id} panel: missing");
                return;
            }

            Check($"menu {id} panel", Numbers(menu["panel"]), Box(Scene.BoundsOf([panel])), DefaultTolerance);
            Paint($"menu {id} panel", menu["paint"], panel);
            Texts($"menu {id}", menu["texts"]!.AsArray(), [.. group.Children.OfType<SceneText>()]);
        }

        /// <summary>
        ///     Compares a message: its line or self loop, markers, paint, texts, sequence number and circles.
        /// </summary>
        /// <param name="message">The golden message.</param>
        /// <param name="items">The scene's items.</param>
        private void Message(JsonObject message, SceneQuery.SequenceScene items)
        {
            var id = (string)message["id"]!;
            if (items.Find("message", id) is not SceneGroup group)
            {
                Issues.Add($"message {id}: missing");
                return;
            }

            var line = (ScenePath)group.Children.First(child => child.Role == "message-line");
            if (message["line"] is { } coordinates)
            {
                Check($"message {id} line", Numbers(coordinates), [line.Segments[0].X, line.Segments[0].Y, line.Segments[^1].X, line.Segments[^1].Y], DefaultTolerance);
            }
            else
            {
                var (length, points) = SceneQuery.Sample(line.Segments, SampleIntervals);
                Check($"message {id} length", [(double)message["length"]!], [length], DefaultTolerance);
                Check($"message {id} points", [.. message["points"]!.AsArray().SelectMany(Numbers)], [.. points.SelectMany(p => new[] { p.X, p.Y })], DefaultTolerance);
            }

            Paint($"message {id}", message["paint"], line);
            Same($"message {id} marker-start", (string?)message["markerStart"], group.Children.FirstOrDefault(child => child.Role == "marker-start")?.Id);
            Same($"message {id} marker-end", (string?)message["markerEnd"], group.Children.FirstOrDefault(child => child.Role == "marker-end")?.Id);
            Texts($"message {id}", message["texts"]!.AsArray(), [.. items.WithRole("message-text").OfType<SceneText>().Where(text => text.Id == id)]);

            // The sequence number: the marker's circle on the zero-length line, and the number
            var number = items.Find("sequence-number", id) as SceneGroup;
            if (message["number"] is { } expected)
            {
                if (number is null)
                {
                    Issues.Add($"message {id} number: missing");
                }
                else
                {
                    var center = Box(Scene.BoundsOf([number.Children[0]]));
                    var at = Numbers(expected["line"]);
                    Check($"message {id} number", [at[0], at[1]], [center[0] + (center[2] / 2), center[1] + (center[3] / 2)], DefaultTolerance);
                    Texts($"message {id} number", new JsonArray(expected["text"]!.DeepClone()), [(SceneText)number.Children[1]]);
                }
            }
            else if (number is not null)
            {
                Issues.Add($"message {id} number: unexpected");
            }

            // The central-connection circles
            var circles = (items.Find("connection-circles", id) as SceneGroup)?.Children ?? [];
            var golden = message["circles"]!.AsArray();
            Count($"message {id} circles", golden.Count, circles.Count);
            foreach (var (circle, path) in golden.Zip(circles))
            {
                var box = Box(Scene.BoundsOf([path]));
                var center = Numbers(circle!["center"]);
                Check($"message {id} circle", [center[0], center[1], (double)circle["r"]!], [box[0] + (box[2] / 2), box[1] + (box[3] / 2), box[2] / 2], DefaultTolerance);
                Paint($"message {id} circle", circle["paint"], (ScenePath)path);
            }
        }

        /// <summary>
        ///     Compares a participant drawing: its box, its parts in order with their paint, and its texts.
        /// </summary>
        /// <param name="what">The drawing.</param>
        /// <param name="golden">The golden drawing, or null when none is drawn.</param>
        /// <param name="ours">The scene's group, or null.</param>
        private void Drawing(string what, JsonNode? golden, SceneItem? ours)
        {
            if (golden is null || ours is not SceneGroup group)
            {
                if ((golden is null) != (ours is null))
                {
                    Issues.Add($"{what}: {(golden is null ? "unexpected" : "missing")}");
                }

                return;
            }

            Check($"{what} box", Numbers(golden["box"]), Box(Scene.BoundsOf([.. group.Children.Where(child => child.Role != "actor-marker")])), TextTolerance);
            var parts = group.Children.OfType<ScenePath>().Where(path => path.Role != "actor-marker").ToList();
            var expected = golden["parts"]!.AsArray();
            Count($"{what} parts", expected.Count, parts.Count);
            foreach (var (part, path) in expected.Zip(parts))
            {
                var tolerance = path.Segments.Any(s => s.Kind == SceneSegmentKind.ArcTo) && path.Role == "actor-cylinder" ? ArcTolerance : DefaultTolerance;
                Check($"{what} {(string)part!["tag"]!}", Numbers(part["box"]), Box(Scene.BoundsOf([path])), tolerance);
                Paint($"{what} {(string)part["tag"]!}", part["paint"], path);
            }

            Texts(what, golden["texts"]!.AsArray(), [.. group.Children.OfType<SceneText>()]);
        }

        /// <summary>
        ///     Compares texts in order: the lines, the anchor's x, the anchor, the font size and weight, the color and the box.
        /// </summary>
        /// <param name="what">The texts.</param>
        /// <param name="golden">The golden texts.</param>
        /// <param name="ours">The scene's lines.</param>
        private void Texts(string what, JsonArray golden, List<SceneText> ours)
        {
            Count($"{what} texts", golden.Count, ours.Count);
            foreach (var (text, line) in golden.Zip(ours))
            {
                // Mermaid draws an empty line as a zero-width space, which CanvasNet keeps as a line without runs
                var content = string.Concat(text!["lines"]!.AsArray().Select(l => (string)l!)).Replace("\u200B", string.Empty, StringComparison.Ordinal);
                Same($"{what} text", content, string.Concat(line.Runs.Select(run => run.Text)));
                Check($"{what} text '{content}' x", [Numbers(text["at"])[0]], [line.X], DefaultTolerance);
                Same($"{what} text '{content}' anchor", (string)text["anchor"]!, line.Anchor.ToString().ToLowerInvariant());
                if (text["box"] is { } box)
                {
                    Check($"{what} text '{content}' box", Numbers(box), Box(line.Box), TextTolerance);
                }

                if (line.Runs.Count == 0)
                {
                    continue;
                }

                var font = line.Runs[0].Font;
                Check($"{what} text '{content}' font size", [(double)text["fontSize"]!], [font.Size], DefaultTolerance);
                Same($"{what} text '{content}' bold", int.Parse((string)text["fontWeight"]!, CultureInfo.InvariantCulture) >= 600 ? "bold" : "normal", font.Bold ? "bold" : "normal");
                Color($"{what} text '{content}' fill", (string)text["fill"]!, line.Runs[0].Color);
            }
        }

        /// <summary>
        ///     Compares the paint of a path: its fill (left out for a line), stroke and stroke width.
        /// </summary>
        /// <param name="what">The path.</param>
        /// <param name="golden">The golden paint.</param>
        /// <param name="path">The scene's path.</param>
        private void Paint(string what, JsonNode? golden, ScenePath path)
        {
            if (golden is null)
            {
                return;
            }

            if ((string?)golden["fill"] is { } fill)
            {
                Color($"{what} fill", fill, path.Fill);
            }

            var stroke = (string)golden["stroke"]!;
            Color($"{what} stroke", stroke, path.Stroke?.Color);
            if (stroke != "none" && path.Stroke is { } ours)
            {
                Check($"{what} stroke width", [(double)golden["strokeWidth"]!], [ours.Width], DefaultTolerance);
                var dash = (string?)golden["dash"];
                var expectedDash = dash is null or "none" ? string.Empty : dash.Replace("px", string.Empty, StringComparison.Ordinal);
                var ourDash = ours.Dash is { } d ? string.Join(", ", d.Select(v => v.ToString(CultureInfo.InvariantCulture))) : string.Empty;
                Same($"{what} dash", expectedDash, ourDash);
            }

            // The drop shadow of the CSS filter
            var filter = (string?)golden["filter"];
            if (filter is null)
            {
                if (path.Shadow is not null)
                {
                    Issues.Add($"{what} shadow: unexpected");
                }
            }
            else if (!SequenceStyles.TryParseFilter(filter, out var shadow) || shadow is null || path.Shadow is not { } ourShadow)
            {
                Issues.Add($"{what} shadow: expected {filter}, got {(path.Shadow is null ? "none" : "a shadow")}");
            }
            else
            {
                Check($"{what} shadow", [shadow.OffsetX, shadow.OffsetY, shadow.Blur], [ourShadow.OffsetX, ourShadow.OffsetY, ourShadow.Blur], DefaultTolerance);
                Same($"{what} shadow color", shadow.Color.ToString(), ourShadow.Color.ToString());
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
                Issues.Add($"{what}: off by {worst.ToString("0.###", CultureInfo.InvariantCulture)} px, expected [{Text(expected)}], got [{Text(actual)}]");
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
        private static string Text(double[] values) =>
            string.Join(", ", values.Take(8).Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))) + (values.Length > 8 ? ", ..." : string.Empty);
    }
}
