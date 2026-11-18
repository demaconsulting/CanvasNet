using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Reference layout tests: CanvasNet's layout of the original sequence fixtures is compared with the SVG that
///     PlantUML (plantuml-mit 1.2026.10) draws for them (the goldens in <c>Fixtures/reference/sequence</c>; see the
///     README there).
/// </summary>
/// <remarks>
///     Positions agree within 1.5 pixels and sizes within 3 % (and at least 2 pixels): the diagram size, the anchor of
///     every text (matched by its content, in document order), the boxes of the participant heads and tails, the life
///     lines, and the message lines.
/// </remarks>
public class PlantUmlReferenceLayoutTests
{
    /// <summary>The tolerance of a position, in pixels.</summary>
    private const double Tolerance = 1.5;

    /// <summary>The SVG namespace.</summary>
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>
    ///     Gets the names of the sequence fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> SequenceFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.puml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every sequence fixture has a reference golden and every golden has its fixture.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Sequence_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence"), "*.puml").Select(Path.GetFileNameWithoutExtension);
        var goldens = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "sequence"), "*.svg").Select(Path.GetFileNameWithoutExtension);

        // Act
        var missing = fixtures.Except(goldens).ToList();
        var orphans = goldens.Except(fixtures).ToList();

        // Assert
        Assert.Empty(missing);
        Assert.Empty(orphans);
        Assert.True(fixtures.Count() >= 8);
    }

    /// <summary>
    ///     Validates that the layout of a sequence fixture matches PlantUML's: size, texts, participants, life lines and
    ///     message lines.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(SequenceFixtures))]
    public void CanvasNetPlantUml_Sequence_ReferenceLayout_MatchesPlantUml(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sequence", name + ".puml"));
        var golden = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", "sequence", name + ".svg"));
        var document = PlantUmlDocument.Parse(text);

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        AssertSize(golden, size);
        AssertTexts(golden, scene);
        AssertParticipants(golden, scene);
        AssertLifelines(golden, scene);
        AssertMessageLines(golden, scene);
    }

    /// <summary>
    ///     Checks the size of the picture.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="size">The measured size.</param>
    private static void AssertSize(XDocument golden, PlantUmlSize size)
    {
        var root = golden.Root!;
        var width = Number(root.Attribute("width")!.Value.Replace("px", string.Empty, StringComparison.Ordinal));
        var height = Number(root.Attribute("height")!.Value.Replace("px", string.Empty, StringComparison.Ordinal));
        Assert.InRange(size.Width, width - Math.Max(2, width * 0.03), width + Math.Max(2, width * 0.03));
        Assert.InRange(size.Height, height - Math.Max(2, height * 0.03), height + Math.Max(2, height * 0.03));
    }

    /// <summary>
    ///     Checks the anchor of every text, matched by content in document order.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="scene">The scene.</param>
    private static void AssertTexts(XDocument golden, Scene scene)
    {
        var ours = scene.Items.OfType<SceneText>().ToList();
        foreach (var text in golden.Descendants(Svg + "text"))
        {
            var content = text.Value.Trim();
            var match = ours.FirstOrDefault(t => t.Text.Trim() == content);
            Assert.True(match is not null, $"no text '{content}'");
            ours.Remove(match);
            AssertNear(Number(text.Attribute("x")!.Value), match.X, $"x of '{content}'");
            AssertNear(Number(text.Attribute("y")!.Value), match.Baseline, $"baseline of '{content}'");
        }
    }

    /// <summary>
    ///     Checks the box of every participant head and tail drawn as a rectangle.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="scene">The scene.</param>
    private static void AssertParticipants(XDocument golden, Scene scene)
    {
        foreach (var group in Groups(golden, "participant-head").Concat(Groups(golden, "participant-tail")))
        {
            var role = Class(group).Contains("participant-head", StringComparison.Ordinal) ? "participant-head" : "participant-tail";
            var id = group.Attribute("data-qualified-name")?.Value;
            var rect = group.Descendants(Svg + "rect").LastOrDefault();
            if (rect is null || id is null)
            {
                continue;
            }

            var ours = scene.Items.OfType<ScenePath>().Where(p => p.Role == role && p.Id == id).Select(p => Scene.BoundsOf([p])!.Value).ToList();
            Assert.NotEmpty(ours);
            var (x, y) = (Number(rect.Attribute("x")!.Value), Number(rect.Attribute("y")!.Value));
            Assert.Contains(ours, b => Math.Abs(b.X - x) <= Tolerance && Math.Abs(b.Y - y) <= Tolerance);
        }
    }

    /// <summary>
    ///     Checks the x and the ends of every life line.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="scene">The scene.</param>
    private static void AssertLifelines(XDocument golden, Scene scene)
    {
        foreach (var group in Groups(golden, "participant-lifeline"))
        {
            var id = group.Attribute("data-qualified-name")!.Value;
            var lines = group.Descendants(Svg + "line").ToList();
            var ours = scene.Items.OfType<ScenePath>().Where(p => p.Role == "lifeline" && p.Id == id).Select(p => Scene.BoundsOf([p])!.Value).ToList();
            Assert.NotEmpty(ours);
            var top = lines.Min(l => Number(l.Attribute("y1")!.Value));
            var bottom = lines.Max(l => Number(l.Attribute("y2")!.Value));
            AssertNear(Number(lines[0].Attribute("x1")!.Value), ours[0].X, $"x of the life line of {id}");
            AssertNear(top, ours.Min(b => b.Y), $"top of the life line of {id}");
            AssertNear(bottom, ours.Max(b => b.Bottom), $"bottom of the life line of {id}");
        }
    }

    /// <summary>
    ///     Checks the lines of the messages, in order.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="scene">The scene.</param>
    private static void AssertMessageLines(XDocument golden, Scene scene)
    {
        var expected = Groups(golden, "message")
            .SelectMany(g => g.Elements(Svg + "line"))
            .Select(l => (X1: Number(l.Attribute("x1")!.Value), Y1: Number(l.Attribute("y1")!.Value), X2: Number(l.Attribute("x2")!.Value), Y2: Number(l.Attribute("y2")!.Value)))
            .Where(l => Math.Abs(l.Y1 - l.Y2) < 1e-9 || Math.Abs(l.X1 - l.X2) < 1e-9)
            .Where(l => Math.Abs(l.X2 - l.X1) > 10 || Math.Abs(l.Y2 - l.Y1) > 11)
            .ToList();
        var ours = scene.Items.OfType<ScenePath>().Where(p => p.Role == "message").Select(p => Scene.BoundsOf([p])!.Value).ToList();
        Assert.Equal(expected.Count, ours.Count);
        foreach (var line in expected)
        {
            var (left, right) = (Math.Min(line.X1, line.X2), Math.Max(line.X1, line.X2));
            var (top, bottom) = (Math.Min(line.Y1, line.Y2), Math.Max(line.Y1, line.Y2));
            Assert.True(
                ours.Exists(b => Math.Abs(b.X - left) <= Tolerance && Math.Abs(b.Right - right) <= Tolerance && Math.Abs(b.Y - top) <= Tolerance && Math.Abs(b.Bottom - bottom) <= Tolerance),
                string.Create(CultureInfo.InvariantCulture, $"no message line from ({line.X1}, {line.Y1}) to ({line.X2}, {line.Y2})"));
        }
    }

    /// <summary>
    ///     Gets the groups of the golden with a class.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="name">The class.</param>
    /// <returns>The groups.</returns>
    private static IEnumerable<XElement> Groups(XDocument golden, string name) =>
        golden.Descendants(Svg + "g").Where(g => Class(g).Split(' ').Contains(name, StringComparer.Ordinal));

    /// <summary>
    ///     Gets the class attribute of an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The class, or empty.</returns>
    private static string Class(XElement element) => element.Attribute("class")?.Value ?? string.Empty;

    /// <summary>
    ///     Reads a number of the golden.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Asserts that a position is within the tolerance of PlantUML's.
    /// </summary>
    /// <param name="expected">PlantUML's value.</param>
    /// <param name="actual">CanvasNet's value.</param>
    /// <param name="what">What is compared.</param>
    private static void AssertNear(double expected, double actual, string what) =>
        Assert.True(Math.Abs(expected - actual) <= Tolerance, string.Create(CultureInfo.InvariantCulture, $"{what}: expected {expected}, got {actual}"));
}
