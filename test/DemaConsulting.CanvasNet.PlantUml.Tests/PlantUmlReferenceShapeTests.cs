using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Reference shape tests for the diagrams PlantUML lays out with Graphviz: CanvasNet's drawing of the original class
///     and object fixtures is compared with the SVG that PlantUML (plantuml-mit 1.2026.10, Smetana layout) draws for
///     them (the goldens in <c>Fixtures/reference/class</c> and <c>Fixtures/reference/object</c>; see the README there).
/// </summary>
/// <remarks>
///     CanvasNet lays these diagrams out with dagre instead of Graphviz, so absolute positions and the picture size
///     differ and are not compared. What is compared is everything local to an element: the set of entities, clusters,
///     and notes; the size of every entity box (within 1.5 pixels or 3 %), its fill, outline color and width; the
///     offset of every text from the box's top-left corner (within 1.5 pixels, matched by content in document order);
///     the number of spots, visibility icons, and separator lines; the offset of every cluster title from the
///     cluster's outline; and, per link, its line style, its decorations (congruent polygons within 0.5 pixels), and
///     its labels.
/// </remarks>
public class PlantUmlReferenceShapeTests
{
    /// <summary>The tolerance of an offset, in pixels.</summary>
    private const double Tolerance = 1.5;

    /// <summary>The SVG namespace.</summary>
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>
    ///     Gets the names of the fixtures of a diagram type.
    /// </summary>
    /// <param name="type">The type folder (<c>class</c>, <c>object</c>).</param>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> FixturesOf(string type)
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", type), "*.puml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Gets the class and object fixtures as type and name pairs.
    /// </summary>
    /// <returns>The pairs.</returns>
    public static TheoryData<string, string> GraphFixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in new[] { "class", "object" })
        {
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", type), "*.puml").Order(StringComparer.Ordinal))
            {
                data.Add(type, Path.GetFileNameWithoutExtension(file));
            }
        }

        return data;
    }

    /// <summary>
    ///     Validates that every class and object fixture has a reference golden and every golden has its fixture.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="minimum">The smallest number of fixtures.</param>
    [Theory]
    [InlineData("class", 12)]
    [InlineData("object", 4)]
    public void CanvasNetPlantUml_Graph_ReferenceGoldens_CoverEveryFixture(string type, int minimum)
    {
        // Arrange
        var fixtures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", type), "*.puml").Select(Path.GetFileNameWithoutExtension).ToList();
        var goldens = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", type), "*.svg").Select(Path.GetFileNameWithoutExtension).ToList();

        // Act
        var missing = fixtures.Except(goldens).ToList();
        var orphans = goldens.Except(fixtures).ToList();

        // Assert
        Assert.Empty(missing);
        Assert.Empty(orphans);
        Assert.True(fixtures.Count >= minimum);
    }

    /// <summary>
    ///     Validates that the entities, clusters, and notes of a fixture are PlantUML's, and that every entity box has
    ///     PlantUML's size, colors, texts, spots, icons, and separators.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(GraphFixtures))]
    public void CanvasNetPlantUml_Graph_ReferenceEntities_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, _) = Load(type, name);
        var entities = Groups(golden, "entity").Where(g => FirstShape(g)?.Name.LocalName == "rect").ToList();
        var notes = Groups(golden, "entity").Where(g => FirstShape(g)?.Name.LocalName == "path").SelectMany(n => n.Elements(Svg + "text"))
            .Concat(Groups(golden, "link").SelectMany(LinkNoteTexts))
            .Select(t => t.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var boxes = scene.Items.OfType<ScenePath>().Where(p => p.Role == "class").ToList();
        var noteTexts = scene.Items.OfType<SceneText>().Where(t => t.Role == "note-text").Select(TextOf).Order(StringComparer.Ordinal).ToList();

        // Assert: the same entities and notes
        Assert.Equal(entities.Select(QualifiedName).Order(StringComparer.Ordinal), boxes.Select(b => b.Id!).Order(StringComparer.Ordinal));
        Assert.Equal(notes, noteTexts);
        foreach (var group in entities)
        {
            AssertEntity(group, scene, QualifiedName(group));
        }
    }

    /// <summary>
    ///     Validates that the clusters of a fixture are PlantUML's and that every cluster title is where PlantUML puts
    ///     it relative to the cluster's outline.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(GraphFixtures))]
    public void CanvasNetPlantUml_Graph_ReferenceClusters_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, _) = Load(type, name);
        var clusters = Groups(golden, "cluster").ToList();

        // Act
        var ids = scene.Items.Where(i => i.Role == "cluster").Select(i => i.Id!).Distinct().Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(clusters.Select(QualifiedName).Order(StringComparer.Ordinal), ids);
        foreach (var group in clusters)
        {
            var id = QualifiedName(group);
            var (left, top) = OutlineCorner(group);
            var outline = Scene.BoundsOf(scene.Items.Where(i => i.Role == "cluster" && i.Id == id).ToList())!.Value;
            var title = group.Elements(Svg + "text").Single();
            var drawn = scene.Items.OfType<SceneText>().Single(t => t.Role == "cluster-title" && t.Id == id);
            Assert.Equal(title.Value, TextOf(drawn));
            AssertTitleX(Number(title, "x") - left, OutlineWidth(group), drawn, outline, id);
            AssertClose(Number(title, "y") - top, drawn.Baseline - outline.Y, $"{id} title y");
        }
    }

    /// <summary>
    ///     Validates that the links of a fixture have PlantUML's line styles, decorations, and labels: the links are
    ///     matched by their ends, and every decoration polygon is congruent with PlantUML's.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(GraphFixtures))]
    public void CanvasNetPlantUml_Graph_ReferenceLinks_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, model) = Load(type, name);
        var names = Groups(golden, "entity").Concat(Groups(golden, "cluster")).ToDictionary(g => (string)g.Attribute("id")!, QualifiedName, StringComparer.Ordinal);
        var links = Groups(golden, "link").ToList();

        // Act
        var drawn = model.Links.Where(l => scene.Items.Any(i => i.Role == "link" && i.Id == l.Id)).ToList();

        // Assert: one drawn link per golden link, matched by its ends (PlantUML writes an up or left link reversed)
        Assert.Equal(links.Count, drawn.Count);
        var unmatched = drawn.ToList();
        foreach (var link in links)
        {
            var from = names[(string)link.Attribute("data-entity-1")!];
            var to = names[(string)link.Attribute("data-entity-2")!];
            var match = unmatched.FirstOrDefault(l => l.From.Id == from && l.To.Id == to) ?? unmatched.FirstOrDefault(l => l.From.Id == to && l.To.Id == from);
            Assert.True(match is not null, $"no link from {from} to {to}");
            unmatched.Remove(match);
            AssertLink(link, scene, match.Id);
        }
    }

    /// <summary>
    ///     Loads a fixture, its golden, and CanvasNet's scene and model.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    /// <returns>The golden, the scene, and the model.</returns>
    private static (XDocument Golden, Scene Scene, ClassModel Model) Load(string type, string name)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", type, name + ".puml"));
        var golden = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", type, name + ".svg"));
        var document = PlantUmlDocument.Parse(text);
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        return (golden, scene, Assert.IsType<ClassModel>(document.Diagram));
    }

    /// <summary>
    ///     Compares one entity box with PlantUML's.
    /// </summary>
    /// <param name="group">The golden group of the entity.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="id">The entity id.</param>
    private static void AssertEntity(XElement group, Scene scene, string id)
    {
        var rect = group.Element(Svg + "rect")!;
        var items = scene.Items.Where(i => i.Id == id).ToList();
        var box = items.OfType<ScenePath>().Single(p => p.Role == "class");
        var bounds = Scene.BoundsOf([box])!.Value;
        var (x, y) = (Number(rect, "x"), Number(rect, "y"));

        // Size and colors
        AssertSize(Number(rect, "width"), bounds.Width, $"{id} width");
        AssertSize(Number(rect, "height"), bounds.Height, $"{id} height");
        Assert.Equal(Color((string)rect.Attribute("fill")!), box.Fill);
        Assert.Equal(Color(StyleOf(rect, "stroke")!), box.Stroke!.Color);
        Assert.Equal(double.Parse(StyleOf(rect, "stroke-width")!, CultureInfo.InvariantCulture), box.Stroke.Width);

        // Every text at its offset from the box, matched by content in order (the spot letter is a path in PlantUML)
        var texts = items.OfType<SceneText>().Where(t => t.Role != "class-spot-text").ToList();
        foreach (var expected in group.Descendants(Svg + "text"))
        {
            var match = texts.FirstOrDefault(t => TextOf(t) == expected.Value);
            Assert.True(match is not null, $"{id}: no text '{expected.Value}'");
            texts.Remove(match);
            AssertClose(Number(expected, "x") - x, match.X - bounds.X, $"{id} '{expected.Value}' x");
            AssertClose(Number(expected, "y") - y, match.Baseline - bounds.Y, $"{id} '{expected.Value}' y");
        }

        Assert.Empty(texts);

        // The same primitives: spots, visibility icons, and separator lines
        Assert.Equal(group.Elements(Svg + "ellipse").Count(), items.Count(i => i.Role == "class-spot"));
        Assert.Equal(group.Elements(Svg + "g").Count(g => g.Attribute("data-visibility-modifier") is not null), items.Count(i => i.Role == "class-icon"));
        Assert.Equal(group.Elements(Svg + "line").Count(), items.Count(i => i.Role is "class-separator" or "map-separator"));
    }

    /// <summary>
    ///     Compares one link with PlantUML's: dashed or solid, the decorations, and the labels.
    /// </summary>
    /// <param name="link">The golden group of the link.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="id">The id of CanvasNet's link.</param>
    private static void AssertLink(XElement link, Scene scene, string id)
    {
        var line = link.Elements(Svg + "path").First(p => (string?)p.Attribute("fill") == "none");
        var drawnLine = scene.Items.OfType<ScenePath>().Single(p => p.Role == "link" && p.Id == id);
        Assert.Equal(StyleOf(line, "stroke-dasharray") is not null, drawnLine.Stroke?.Dash is not null);

        // The decorations: congruent polygons (sorted side lengths), with the same fill
        var expected = link.Elements(Svg + "polygon")
            .Select(p => (Sides: Sides(Points((string)p.Attribute("points")!)), Filled: (string?)p.Attribute("fill") != "none"))
            .OrderBy(d => d.Sides.Sum())
            .ToList();
        var actual = scene.Items.OfType<ScenePath>()
            .Where(p => p.Role is "link-head" or "link-tail" && p.Id == id && p.Segments.Count(s => s.Kind == SceneSegmentKind.LineTo) >= 2)
            .Select(p => (Sides: Sides(p.Segments.Where(s => s.Kind != SceneSegmentKind.Close).Select(s => (s.X, s.Y)).ToList()), Filled: p.Fill is { } f && f != SkinColors.White))
            .OrderBy(d => d.Sides.Sum())
            .ToList();
        Assert.Equal(expected.Count, actual.Count);
        for (var k = 0; k < expected.Count; k++)
        {
            Assert.Equal(expected[k].Filled, actual[k].Filled);
            Assert.Equal(expected[k].Sides.Count, actual[k].Sides.Count);
            for (var s = 0; s < expected[k].Sides.Count; s++)
            {
                Assert.True(Math.Abs(expected[k].Sides[s] - actual[k].Sides[s]) <= 0.5, $"{id}: side {s} is {actual[k].Sides[s]}, PlantUML draws {expected[k].Sides[s]}");
            }
        }

        // The labels (a note on the link is drawn by the note, not the link)
        var notes = LinkNoteTexts(link).ToHashSet();
        var labels = link.Elements(Svg + "text").Where(t => !notes.Contains(t)).Select(t => t.Value).Order(StringComparer.Ordinal).ToList();
        var drawn = scene.Items.OfType<SceneText>().Where(t => t.Id == id && t.Role.StartsWith("link-", StringComparison.Ordinal)).Select(TextOf);
        Assert.Equal(labels, drawn.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Gets the texts of the note drawn on a golden link: the texts inside the outline of a note path.
    /// </summary>
    /// <param name="link">The golden group of the link.</param>
    /// <returns>The texts of its note.</returns>
    private static IEnumerable<XElement> LinkNoteTexts(XElement link)
    {
        var outline = link.Elements(Svg + "path").FirstOrDefault(p => string.Equals((string?)p.Attribute("fill"), "#FEFFDD", StringComparison.OrdinalIgnoreCase));
        if (outline is null)
        {
            return [];
        }

        var points = PathPoints((string)outline.Attribute("d")!);
        var (left, top, right, bottom) = (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        return link.Elements(Svg + "text").Where(t => Number(t, "x") >= left && Number(t, "x") <= right && Number(t, "y") >= top && Number(t, "y") <= bottom);
    }

    /// <summary>
    ///     Asserts that a cluster title is where PlantUML puts it horizontally: at the same offset from the left of the
    ///     outline (a folder or frame tab), or, for a centered title, at the same offset from the outline's middle (the
    ///     outlines are not as wide in both layouts).
    /// </summary>
    /// <param name="left">PlantUML's offset from the left of the outline.</param>
    /// <param name="width">PlantUML's outline width.</param>
    /// <param name="drawn">CanvasNet's title.</param>
    /// <param name="outline">CanvasNet's outline bounds.</param>
    /// <param name="id">The cluster id.</param>
    private static void AssertTitleX(double left, double width, SceneText drawn, SceneRect outline, string id)
    {
        var fromLeft = Math.Abs(left - (drawn.X - outline.X));
        var fromMiddle = Math.Abs((left - (width / 2)) - (drawn.X - outline.X - (outline.Width / 2)));
        Assert.True(Math.Min(fromLeft, fromMiddle) <= Tolerance, $"{id} title x: CanvasNet {drawn.X - outline.X:0.###} of {outline.Width:0.###}, PlantUML {left:0.###} of {width:0.###}");
    }

    /// <summary>
    ///     Gets the width of a cluster's outline.
    /// </summary>
    /// <param name="group">The cluster group.</param>
    /// <returns>The width.</returns>
    private static double OutlineWidth(XElement group)
    {
        var shape = FirstShape(group)!;
        if (shape.Name.LocalName == "rect")
        {
            return Number(shape, "width");
        }

        var points = shape.Name.LocalName == "polygon" ? Points((string)shape.Attribute("points")!) : PathPoints((string)shape.Attribute("d")!);
        return points.Max(p => p.X) - points.Min(p => p.X);
    }

    /// <summary>
    ///     Gets the groups of a class in a golden.
    /// </summary>
    /// <param name="golden">The golden.</param>
    /// <param name="cls">The class attribute.</param>
    /// <returns>The groups.</returns>
    private static IEnumerable<XElement> Groups(XDocument golden, string cls) =>
        golden.Descendants(Svg + "g").Where(g => (string?)g.Attribute("class") == cls);

    /// <summary>
    ///     Gets the first shape of a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The shape, or null.</returns>
    private static XElement? FirstShape(XElement group) => group.Elements().FirstOrDefault(e => e.Name.LocalName is "rect" or "path" or "polygon");

    /// <summary>
    ///     Gets the qualified name of a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The name.</returns>
    private static string QualifiedName(XElement group) => (string)group.Attribute("data-qualified-name")!;

    /// <summary>
    ///     Gets the top-left corner of a cluster's outline.
    /// </summary>
    /// <param name="group">The cluster group.</param>
    /// <returns>The smallest x and y of its outline.</returns>
    private static (double X, double Y) OutlineCorner(XElement group)
    {
        var shape = FirstShape(group)!;
        var points = shape.Name.LocalName switch
        {
            "rect" => [(Number(shape, "x"), Number(shape, "y"))],
            "polygon" => Points((string)shape.Attribute("points")!),
            _ => PathPoints((string)shape.Attribute("d")!),
        };
        return (points.Min(p => p.X), points.Min(p => p.Y));
    }

    /// <summary>
    ///     Reads the end points of the commands of an SVG path (the last pair of every command).
    /// </summary>
    /// <param name="d">The path data.</param>
    /// <returns>The points.</returns>
    private static List<(double X, double Y)> PathPoints(string d)
    {
        var result = new List<(double X, double Y)>();
        foreach (var command in d.Split(['M', 'L', 'A', 'C', 'Q', 'Z'], StringSplitOptions.RemoveEmptyEntries))
        {
            var numbers = command.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToList();
            if (numbers.Count >= 2)
            {
                result.Add((numbers[^2], numbers[^1]));
            }
        }

        return result;
    }

    /// <summary>
    ///     Reads the points of an SVG polygon.
    /// </summary>
    /// <param name="points">The points attribute.</param>
    /// <returns>The points.</returns>
    private static List<(double X, double Y)> Points(string points)
    {
        var numbers = points.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(n => double.Parse(n, CultureInfo.InvariantCulture)).ToList();
        return Enumerable.Range(0, numbers.Count / 2).Select(k => (numbers[2 * k], numbers[(2 * k) + 1])).ToList();
    }

    /// <summary>
    ///     Gets the sorted side lengths of a closed polygon (a repeated closing point is dropped).
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The side lengths, sorted.</returns>
    private static List<double> Sides(List<(double X, double Y)> points)
    {
        if (points.Count > 1 && Math.Abs(points[0].X - points[^1].X) < 1e-3 && Math.Abs(points[0].Y - points[^1].Y) < 1e-3)
        {
            points = points.Take(points.Count - 1).ToList();
        }

        return points.Select((p, k) => Math.Sqrt(Math.Pow(points[(k + 1) % points.Count].X - p.X, 2) + Math.Pow(points[(k + 1) % points.Count].Y - p.Y, 2)))
            .Order()
            .ToList();
    }

    /// <summary>
    ///     Reads a number attribute.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute.</param>
    /// <returns>The number.</returns>
    private static double Number(XElement element, string name) => double.Parse((string)element.Attribute(name)!, CultureInfo.InvariantCulture);

    /// <summary>
    ///     Reads a property of the style attribute.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The property.</param>
    /// <returns>The value, or null.</returns>
    private static string? StyleOf(XElement element, string property) =>
        ((string?)element.Attribute("style") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split(':'))
            .Where(p => p.Length == 2 && p[0].Trim() == property)
            .Select(p => p[1].Trim())
            .FirstOrDefault();

    /// <summary>
    ///     Reads an SVG color (<c>#RGB</c> or <c>#RRGGBB</c>).
    /// </summary>
    /// <param name="text">The color.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Color(string text)
    {
        var hex = text.TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        var value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
    }

    /// <summary>
    ///     Gets the text of a scene line.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>The text.</returns>
    private static string TextOf(SceneText text) => string.Concat(text.Runs.Select(r => r.Text));

    /// <summary>
    ///     Asserts that an offset is within the tolerance.
    /// </summary>
    /// <param name="expected">PlantUML's offset.</param>
    /// <param name="actual">CanvasNet's offset.</param>
    /// <param name="what">What is compared.</param>
    private static void AssertClose(double expected, double actual, string what) =>
        Assert.True(Math.Abs(expected - actual) <= Tolerance, $"{what}: CanvasNet {actual:0.###}, PlantUML {expected:0.###}");

    /// <summary>
    ///     Asserts that a size is within 1.5 pixels or 3 %.
    /// </summary>
    /// <param name="expected">PlantUML's size.</param>
    /// <param name="actual">CanvasNet's size.</param>
    /// <param name="what">What is compared.</param>
    private static void AssertSize(double expected, double actual, string what) =>
        Assert.True(Math.Abs(expected - actual) <= Math.Max(Tolerance, expected * 0.03), $"{what}: CanvasNet {actual:0.###}, PlantUML {expected:0.###}");
}
