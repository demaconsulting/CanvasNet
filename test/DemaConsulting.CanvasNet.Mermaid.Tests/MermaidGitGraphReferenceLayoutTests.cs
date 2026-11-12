using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Mermaid.GitGraph;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the git graph layout against the reference implementation: each golden of
///     <c>Fixtures/reference/gitgraph/</c> (the layout that the pinned mermaid-cli and mermaid draw in headless Chrome with
///     Liberation Sans and the seeded commit ids, see <c>Fixtures/reference/README.md</c>) is compared with CanvasNet's
///     scene: the view box, every branch line, label rectangle and label row, every arrow's path data, every bullet, every
///     commit label and tag (mapped through their transforms), the title, and the computed paint of each.
/// </summary>
public sealed partial class MermaidGitGraphReferenceLayoutTests
{
    /// <summary>
    ///     The tolerance in pixels of every position and size except text boxes (the goldens are rounded to 0.001 px).
    /// </summary>
    private const double DefaultTolerance = 0.01;

    /// <summary>
    ///     The tolerance in pixels of text boxes: Chrome snaps the baseline of a text to its layout unit of 1/64 pixel, so a
    ///     box can be 1/64 pixel taller than the rounded font metrics.
    /// </summary>
    private const double TextTolerance = 0.02;

    /// <summary>
    ///     Gets the names of the git graph goldens.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    public static TheoryData<string> GitGraphGoldens()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenNames())
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>
    ///     Validates that every git graph fixture has a golden.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_GitGraph_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph"), "*.mmd")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        // Act
        var goldens = GoldenNames();

        // Assert
        Assert.Equal(fixtures, goldens);
    }

    /// <summary>
    ///     Validates that CanvasNet lays a git graph fixture out and paints it as the reference does, within the
    ///     tolerances.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(GitGraphGoldens))]
    public void CanvasNetMermaid_GitGraphReferenceLayout_MatchesMermaid(string name)
    {
        // Arrange
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(ReferenceFolder, name + ".layout.json")))!.AsObject();
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gitgraph", (string)golden["source"]!));

        // Act
        var scene = SceneQuery.Layout(text);
        var comparison = new Comparison();
        comparison.Compare(golden, scene);

        // Assert
        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{name}: largest geometry difference {comparison.LargestGeometry:0.######} px, largest text box difference {comparison.LargestText:0.######} px"));
        Assert.True(
            comparison.Issues.Count == 0,
            $"{name}: {comparison.Issues.Count} difference(s):\n" + string.Join('\n', comparison.Issues.Take(25)));
    }

    /// <summary>Gets the folder of the git graph goldens.</summary>
    private static string ReferenceFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "gitgraph");

    /// <summary>
    ///     Gets the names of the goldens in order.
    /// </summary>
    /// <returns>The names (the fixture names).</returns>
    private static IEnumerable<string> GoldenNames() =>
        Directory.GetFiles(ReferenceFolder, "*.layout.json")
            .Select(file => Path.GetFileName(file)[..^".layout.json".Length])
            .Order(StringComparer.Ordinal);

    /// <summary>Matches one transform function of an SVG transform list.</summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"(translate|rotate)\(([^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex TransformFunction();

    /// <summary>Matches the numbers of SVG path data.</summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex PathNumber();

    /// <summary>
    ///     An affine transform of the plane: <c>x' = a x + c y + e</c>, <c>y' = b x + d y + f</c>.
    /// </summary>
    /// <param name="A">The a coefficient.</param>
    /// <param name="B">The b coefficient.</param>
    /// <param name="C">The c coefficient.</param>
    /// <param name="D">The d coefficient.</param>
    /// <param name="E">The e coefficient.</param>
    /// <param name="F">The f coefficient.</param>
    private readonly record struct Affine(double A, double B, double C, double D, double E, double F)
    {
        /// <summary>Gets the identity.</summary>
        public static Affine Identity => new(1, 0, 0, 1, 0, 0);

        /// <summary>
        ///     Parses an SVG transform list of <c>translate</c> and <c>rotate</c> functions (null is the identity).
        /// </summary>
        /// <param name="text">The transform attribute, or null.</param>
        /// <returns>The transform.</returns>
        public static Affine Parse(string? text)
        {
            var result = Identity;
            if (text is null)
            {
                return result;
            }

            foreach (Match match in TransformFunction().Matches(text))
            {
                var args = match.Groups[2].Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(arg => double.Parse(arg, CultureInfo.InvariantCulture)).ToArray();
                Affine step;
                if (match.Groups[1].Value == "translate")
                {
                    step = new Affine(1, 0, 0, 1, args[0], args.Length > 1 ? args[1] : 0);
                }
                else
                {
                    var (sin, cos) = Math.SinCos(args[0] * Math.PI / 180);
                    var (cx, cy) = args.Length > 2 ? (args[1], args[2]) : (0, 0);
                    step = new Affine(cos, sin, -sin, cos, cx - (cos * cx) + (sin * cy), cy - (sin * cx) - (cos * cy));
                }

                result = result.Then(step);
            }

            return result;
        }

        /// <summary>
        ///     Composes this transform with one applied before it (an SVG transform list's next function).
        /// </summary>
        /// <param name="inner">The transform applied first.</param>
        /// <returns>The composition.</returns>
        public Affine Then(Affine inner) => new(
            (A * inner.A) + (C * inner.B),
            (B * inner.A) + (D * inner.B),
            (A * inner.C) + (C * inner.D),
            (B * inner.C) + (D * inner.D),
            (A * inner.E) + (C * inner.F) + E,
            (B * inner.E) + (D * inner.F) + F);

        /// <summary>
        ///     Maps a point.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The mapped point.</returns>
        public (double X, double Y) Map(double x, double y) => ((A * x) + (C * y) + E, (B * x) + (D * y) + F);

        /// <summary>
        ///     Maps a box: the box of its mapped corners.
        /// </summary>
        /// <param name="box">The box (x, y, width, height).</param>
        /// <returns>The mapped box.</returns>
        public double[] MapBox(double[] box)
        {
            var corners = new[] { Map(box[0], box[1]), Map(box[0] + box[2], box[1]), Map(box[0] + box[2], box[1] + box[3]), Map(box[0], box[1] + box[3]) };
            var left = corners.Min(c => c.X);
            var top = corners.Min(c => c.Y);
            return [left, top, corners.Max(c => c.X) - left, corners.Max(c => c.Y) - top];
        }
    }

    /// <summary>
    ///     Compares a golden with a scene and collects the differences.
    /// </summary>
    private sealed class Comparison
    {
        /// <summary>Gets the differences, described.</summary>
        public List<string> Issues { get; } = [];

        /// <summary>Gets the largest difference of a position or size other than a text box.</summary>
        public double LargestGeometry { get; private set; }

        /// <summary>Gets the largest difference of a text box.</summary>
        public double LargestText { get; private set; }

        /// <summary>
        ///     Compares every fact of a golden.
        /// </summary>
        /// <param name="golden">The golden.</param>
        /// <param name="scene">The scene.</param>
        public void Compare(JsonObject golden, Scene scene)
        {
            var viewBox = scene.ViewBox;
            Check("viewBox", Numbers(golden["viewBox"]), [viewBox.X, viewBox.Y, viewBox.Width, viewBox.Height], DefaultTolerance);
            var root = (SceneGroup)scene.Items.Single();
            var groups = root.Children.OfType<SceneGroup>().ToDictionary(group => group.Role, StringComparer.Ordinal);
            Branches(golden["branches"]!.AsArray(), groups.GetValueOrDefault("git-branches"));
            Arrows(golden["arrows"]!.AsArray(), groups["git-arrows"]);
            Bullets(golden["bullets"]!.AsArray(), groups["git-commits"]);
            Labels(golden["labels"]!.AsArray(), groups["git-labels"]);
            var title = root.Children.OfType<SceneText>().SingleOrDefault(item => item.Role == "title");
            if (golden["title"] is JsonObject goldenTitle)
            {
                if (title is null)
                {
                    Issues.Add("title: expected, not drawn");
                }
                else
                {
                    Text("title", goldenTitle, title, Affine.Identity);
                }
            }
            else if (title is not null)
            {
                Issues.Add("title: drawn, not expected");
            }
        }

        /// <summary>
        ///     Compares the branches: each line, label rectangle and label rows.
        /// </summary>
        /// <param name="golden">The golden branches.</param>
        /// <param name="group">The scene's group of the branches, or null.</param>
        private void Branches(JsonArray golden, SceneGroup? group)
        {
            var items = group?.Children ?? [];
            Count("branch items", golden.Count * 3, items.Count);
            for (var i = 0; i < golden.Count && (i * 3) + 2 < items.Count; i++)
            {
                var branch = golden[i]!;
                var line = (ScenePath)items[i * 3];
                var what = $"branch {line.Id}";
                var g = branch["line"]!;
                Check($"{what} line", [(double)g["x1"]!, (double)g["y1"]!, (double)g["x2"]!, (double)g["y2"]!], SegmentNumbers(line.Segments), DefaultTolerance);
                Stroke($"{what} line", g["paint"]!, line);
                var rect = branch["rect"]!;
                var transform = Affine.Parse((string?)rect["transform"]);
                var box = transform.MapBox([(double)rect["x"]!, (double)rect["y"]!, (double)rect["width"]!, (double)rect["height"]!]);
                var path = (ScenePath)items[(i * 3) + 1];
                Check($"{what} rect", box, Box(Scene.BoundsOf([path])), DefaultTolerance);
                Paint($"{what} rect", rect["paint"]!, path);
                var label = (SceneGroup)items[(i * 3) + 2];
                var offset = Affine.Parse((string)branch["label"]!["transform"]!);
                Check($"{what} label at", [offset.E, offset.F], [label.OffsetX, label.OffsetY], DefaultTolerance);
                var text = branch["label"]!["text"]!;
                var rows = text["rows"]!.AsArray().Where(row => row!["box"] is not null).ToList();
                Count($"{what} rows", rows.Count, label.Children.Count);
                foreach (var (row, item) in rows.Zip(label.Children.Cast<SceneText>()))
                {
                    Same($"{what} row", (string)row!["text"]!, string.Concat(item.Runs.Select(run => run.Text)));
                    CheckText($"{what} row '{(string)row["text"]!}' box", Numbers(row["box"]), Box(item.Box));
                    Check($"{what} row font size", [(double)text["fontSize"]!], [item.Runs[0].Font.Size], DefaultTolerance);
                    Color($"{what} row fill", (string)text["fill"]!, 1, item.Runs[0].Color);
                }
            }
        }

        /// <summary>
        ///     Compares the arrows: the numbers of each path's data and the paint.
        /// </summary>
        /// <param name="golden">The golden arrows.</param>
        /// <param name="group">The scene's group of the arrows.</param>
        private void Arrows(JsonArray golden, SceneGroup group)
        {
            Count("arrows", golden.Count, group.Children.Count);
            foreach (var (arrow, item) in golden.Zip(group.Children))
            {
                var path = (ScenePath)item;
                var what = $"arrow {path.Id}";
                Check($"{what} d", PathNumbers((string)arrow!["d"]!), SegmentNumbers(path.Segments), DefaultTolerance);
                Stroke(what, arrow["paint"]!, path);
                var cap = (string)arrow["paint"]!["lineCap"]!;
                Same($"{what} cap", cap, path.Stroke?.Cap.ToString().ToLowerInvariant());
            }
        }

        /// <summary>
        ///     Compares the bullets: each circle, rectangle, line and cross with its paint and the commit id in its class.
        /// </summary>
        /// <param name="golden">The golden bullets.</param>
        /// <param name="group">The scene's group of the bullets.</param>
        private void Bullets(JsonArray golden, SceneGroup group)
        {
            Count("bullets", golden.Count, group.Children.Count);
            foreach (var (bullet, item) in golden.Zip(group.Children))
            {
                var path = (ScenePath)item;
                var what = $"bullet {(string)bullet!["class"]!}";
                if (!$" {(string)bullet["class"]!} ".Contains($" {GitGraphRenderer.Decode(path.Id!)} ", StringComparison.Ordinal))
                {
                    Issues.Add($"{what}: the scene's commit is {path.Id}");
                }

                switch ((string)bullet["tag"]!)
                {
                    case "circle":
                        var (cx, cy, r) = ((double)bullet["cx"]!, (double)bullet["cy"]!, (double)bullet["r"]!);
                        Check(what, [cx - r, cy - r, 2 * r, 2 * r], Box(Scene.BoundsOf([path])), DefaultTolerance);
                        break;
                    case "rect":
                        Check(what, [(double)bullet["x"]!, (double)bullet["y"]!, (double)bullet["width"]!, (double)bullet["height"]!], Box(Scene.BoundsOf([path])), DefaultTolerance);
                        break;
                    case "line":
                        Check(what, [(double)bullet["x1"]!, (double)bullet["y1"]!, (double)bullet["x2"]!, (double)bullet["y2"]!], SegmentNumbers(path.Segments), DefaultTolerance);
                        break;
                    default:
                        Check(what, PathNumbers((string)bullet["d"]!), SegmentNumbers(path.Segments), DefaultTolerance);
                        break;
                }

                if ((string)bullet["tag"]! == "line")
                {
                    Stroke(what, bullet["paint"]!, path);
                }
                else
                {
                    Paint(what, bullet["paint"]!, path);
                }
            }
        }

        /// <summary>
        ///     Compares the commit labels and tags: their geometry mapped through their transforms, their texts and paint.
        /// </summary>
        /// <param name="golden">The golden labels and tags.</param>
        /// <param name="group">The scene's group of the labels.</param>
        private void Labels(JsonArray golden, SceneGroup group)
        {
            Count("labels", golden.Count, group.Children.Count);
            foreach (var (label, item) in golden.Zip(group.Children.Cast<SceneGroup>()))
            {
                var kind = (string)label!["kind"]!;
                var what = $"{kind} {item.Id}";
                Same($"{what} role", kind == "commit" ? "git-commit-label" : "git-tag", item.Role);
                if (kind == "commit")
                {
                    var wrapper = Affine.Parse((string?)label["transform"]);
                    var rect = label["rect"]!;
                    var transform = wrapper.Then(Affine.Parse((string?)rect["transform"]));
                    var (x, y, w, h) = ((double)rect["x"]!, (double)rect["y"]!, (double)rect["width"]!, (double)rect["height"]!);
                    var corners = new[] { transform.Map(x, y), transform.Map(x + w, y), transform.Map(x + w, y + h), transform.Map(x, y + h) };
                    var path = (ScenePath)item.Children[0];
                    Check($"{what} rect", [.. corners.SelectMany(c => new[] { c.X, c.Y })], SegmentNumbers(path.Segments), DefaultTolerance);
                    Paint($"{what} rect", rect["paint"]!, path);
                    Text($"{what} text", label["text"]!, item.Children.OfType<SceneText>().SingleOrDefault(), wrapper);
                }
                else
                {
                    var polygon = label["polygon"]!;
                    var transform = Affine.Parse((string?)polygon["transform"]);
                    var points = polygon["points"]!.AsArray().Select(p => transform.Map((double)p![0]!, (double)p[1]!)).SelectMany(p => new[] { p.X, p.Y });
                    var path = (ScenePath)item.Children[0];
                    Check($"{what} polygon", [.. points], SegmentNumbers(path.Segments), DefaultTolerance);
                    Paint($"{what} polygon", polygon["paint"]!, path);
                    var hole = label["hole"]!;
                    var center = Affine.Parse((string?)hole["transform"]).Map((double)hole["cx"]!, (double)hole["cy"]!);
                    var r = (double)hole["r"]!;
                    var holePath = (ScenePath)item.Children[1];
                    Check($"{what} hole", [center.X - r, center.Y - r, 2 * r, 2 * r], Box(Scene.BoundsOf([holePath])), DefaultTolerance);
                    Paint($"{what} hole", hole["paint"]!, holePath);
                    Text($"{what} text", label["text"]!, item.Children.OfType<SceneText>().SingleOrDefault(), Affine.Identity);
                }
            }
        }

        /// <summary>
        ///     Compares a text: its content (white space collapsed), its anchor point and box mapped through the transforms,
        ///     its rotation, font size and fill.
        /// </summary>
        /// <param name="what">The text.</param>
        /// <param name="golden">The golden text.</param>
        /// <param name="line">The scene's text, or null when it shows nothing.</param>
        /// <param name="outer">The transform of the text's groups.</param>
        private void Text(string what, JsonNode golden, SceneText? line, Affine outer)
        {
            var content = Regex.Replace((string)golden["text"]!, "\\s+", " ").Trim();
            if (line is null)
            {
                if (content.Length > 0)
                {
                    Issues.Add($"{what}: '{content}' not drawn");
                }

                return;
            }

            Same($"{what} text", content, string.Concat(line.Runs.Select(run => run.Text)));
            var transform = outer.Then(Affine.Parse((string?)golden["transform"]));
            var at = transform.Map((double)golden["x"]!, (double)golden["y"]!);
            var ours = (line.X, line.Baseline);
            if (line.Rotation is { } rotation)
            {
                ours = Affine.Parse(string.Create(CultureInfo.InvariantCulture, $"rotate({rotation.Angle}, {rotation.CenterX}, {rotation.CenterY})")).Map(line.X, line.Baseline);
            }

            Check($"{what} '{content}' at", [at.X, at.Y], [ours.X, ours.Item2], DefaultTolerance);
            CheckText($"{what} '{content}' box", transform.MapBox(Numbers(golden["box"])), Box(line.Box));
            Same($"{what} '{content}' anchor", (string)golden["anchor"]!, line.Anchor.ToString().ToLowerInvariant());
            Check($"{what} '{content}' font size", [(double)golden["fontSize"]!], [line.Runs[0].Font.Size], DefaultTolerance);
            Color($"{what} '{content}' fill", (string)golden["fill"]!, (double)golden["opacity"]!, line.Runs[0].Color);
        }

        /// <summary>
        ///     Compares the fill, stroke and shadow of a shape.
        /// </summary>
        /// <param name="what">The shape.</param>
        /// <param name="golden">The golden paint.</param>
        /// <param name="path">The scene's path.</param>
        private void Paint(string what, JsonNode golden, ScenePath path)
        {
            Color($"{what} fill", (string)golden["fill"]!, (double)golden["opacity"]!, path.Fill);
            Stroke(what, golden, path);
            var filter = (string)golden["filter"]!;
            Same($"{what} shadow", filter == "none" ? "none" : "shadow", path.Shadow is null ? "none" : "shadow");
        }

        /// <summary>
        ///     Compares the stroke of a shape: its color, width and dashes.
        /// </summary>
        /// <param name="what">The shape.</param>
        /// <param name="golden">The golden paint.</param>
        /// <param name="path">The scene's path.</param>
        private void Stroke(string what, JsonNode golden, ScenePath path)
        {
            var stroke = (string)golden["stroke"]!;
            Color($"{what} stroke", stroke, (double)golden["opacity"]!, path.Stroke?.Color);
            if (stroke != "none" && path.Stroke is { } ours)
            {
                Check($"{what} stroke width", [(double)golden["strokeWidth"]!], [ours.Width], DefaultTolerance);
                var dash = (string)golden["dasharray"]!;
                double[] dashes = dash == "none" ? [] : [.. PathNumber().Matches(dash).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))];
                Check($"{what} dash", dashes.Length % 2 == 1 ? [.. dashes, .. dashes] : dashes, [.. ours.Dash ?? []], DefaultTolerance);
                Same($"{what} cap", (string)golden["lineCap"]!, ours.Cap switch { LineCap.Round => "round", LineCap.Square => "square", _ => "butt" });
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

            expected = expected is { A: 0 } ? null : expected;
            ours = ours is { A: 0 } ? null : ours;
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
        ///     Checks a text box against the golden's.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden box.</param>
        /// <param name="actual">The scene's box.</param>
        private void CheckText(string what, double[] expected, double[] actual)
        {
            var worst = Worst(expected, actual);
            LargestText = Math.Max(LargestText, worst);
            Report(what, expected, actual, worst, TextTolerance);
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
            var worst = Worst(expected, actual);
            LargestGeometry = Math.Max(LargestGeometry, worst);
            Report(what, expected, actual, worst, tolerance);
        }

        /// <summary>
        ///     Gets the largest difference of two lists of numbers (infinite when their lengths differ).
        /// </summary>
        /// <param name="expected">The golden numbers.</param>
        /// <param name="actual">The scene's numbers.</param>
        /// <returns>The difference.</returns>
        private static double Worst(double[] expected, double[] actual)
        {
            if (expected.Length != actual.Length)
            {
                return double.PositiveInfinity;
            }

            var worst = expected.Length == 0 ? 0 : expected.Zip(actual, (e, a) => Math.Abs(e - a)).Max();
            return double.IsNaN(worst) ? double.PositiveInfinity : worst;
        }

        /// <summary>
        ///     Records a difference beyond a tolerance.
        /// </summary>
        /// <param name="what">The fact.</param>
        /// <param name="expected">The golden numbers.</param>
        /// <param name="actual">The scene's numbers.</param>
        /// <param name="worst">The largest difference.</param>
        /// <param name="tolerance">The tolerance.</param>
        private void Report(string what, double[] expected, double[] actual, double worst, double tolerance)
        {
            if (!(worst <= tolerance))
            {
                Issues.Add($"{what}: off by {worst.ToString("0.###", CultureInfo.InvariantCulture)} px, expected [{Format(expected)}], got [{Format(actual)}]");
            }
        }

        /// <summary>
        ///     Reads the numbers of SVG path data in order (an arc's radii, rotation and flags included).
        /// </summary>
        /// <param name="data">The path data.</param>
        /// <returns>The numbers.</returns>
        private static double[] PathNumbers(string data) =>
            [.. PathNumber().Matches(data).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))];

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
