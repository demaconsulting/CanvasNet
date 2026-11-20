using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     The reference shape tests of the description diagrams: CanvasNet's drawing of the original component fixtures is
///     compared with the SVG that PlantUML draws for them (the goldens in <c>Fixtures/reference/component</c>).
/// </summary>
/// <remarks>
///     As for the class diagrams, absolute positions and the picture size are not compared. Per element: the outline
///     kind (its number of shapes), its size (within 1.5 pixels or 3 %), fill, outline color and width, and the offset of
///     every text from the outline's top-left corner; per container: the offsets of its title and stereotypes from its
///     outline; per link: its line style, decorations, and labels.
/// </remarks>
public partial class PlantUmlReferenceShapeTests
{
    /// <summary>
    ///     Gets the description fixtures as type and name pairs.
    /// </summary>
    /// <returns>The pairs.</returns>
    public static TheoryData<string, string> DescriptionFixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in new[] { "component" })
        {
            foreach (var name in FixturesOf(type))
            {
                data.Add(type, name);
            }
        }

        return data;
    }

    /// <summary>
    ///     Validates that the elements and notes of a description fixture are PlantUML's (a golden entity that is not
    ///     an element of the model is a note), and that every element has PlantUML's outline (the same number of
    ///     shapes), size, colors, and text offsets.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(DescriptionFixtures))]
    public void CanvasNetPlantUml_Description_ReferenceElements_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, model) = LoadDescription(type, name);
        var groups = Groups(golden, "entity").ToList();
        var ids = model.Elements.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var elements = groups.Where(g => ids.Contains(QualifiedName(g))).ToList();
        var notes = groups.Where(g => !ids.Contains(QualifiedName(g))).SelectMany(n => n.Elements(Svg + "text"))
            .Concat(Groups(golden, "link").SelectMany(LinkNoteTexts))
            .Select(t => t.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var outlines = scene.Items.OfType<ScenePath>().Where(p => p.Role == "description").ToList();
        var noteTexts = scene.Items.OfType<SceneText>().Where(t => t.Role == "note-text").Select(TextOf).Order(StringComparer.Ordinal).ToList();

        // Assert: the same elements and notes, each with PlantUML's shape
        Assert.Equal(elements.Select(QualifiedName).Order(StringComparer.Ordinal), outlines.Select(o => o.Id!).Order(StringComparer.Ordinal));
        Assert.Equal(model.Elements.Count(e => !e.IsContainer), outlines.Count);
        Assert.True(notes.SequenceEqual(noteTexts), $"notes: PlantUML {string.Join(" | ", notes)}, CanvasNet {string.Join(" | ", noteTexts)}");
        foreach (var group in elements)
        {
            AssertDescriptionElement(group, scene, QualifiedName(group));
        }
    }

    /// <summary>
    ///     Validates that the containers of a description fixture are PlantUML's, and that every title and stereotype
    ///     is where PlantUML puts it relative to the container's outline.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(DescriptionFixtures))]
    public void CanvasNetPlantUml_Description_ReferenceContainers_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, _) = LoadDescription(type, name);
        var clusters = Groups(golden, "cluster").ToList();

        // Act
        var ids = scene.Items.Where(i => i.Role == "cluster").Select(i => i.Id!).Distinct().Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(clusters.Select(QualifiedName).Order(StringComparer.Ordinal), ids);
        foreach (var group in clusters)
        {
            var id = QualifiedName(group);
            var (left, top) = OutlineCorner(group);
            var outline = Bounds(scene.Items.OfType<ScenePath>().First(i => i.Role == "cluster" && i.Id == id));
            var drawn = scene.Items.OfType<SceneText>().Where(t => t.Role is "cluster-title" or "cluster-stereotype" && t.Id == id).ToList();
            foreach (var expected in group.Elements(Svg + "text"))
            {
                var match = drawn.FirstOrDefault(t => TextOf(t) == expected.Value);
                Assert.True(match is not null, $"{id}: no text '{expected.Value}'");
                drawn.Remove(match);
                AssertTitleX(Number(expected, "x") - left, OutlineWidth(group), match, outline, id);
                AssertClose(Number(expected, "y") - top, match.Baseline - outline.Y, $"{id} '{expected.Value}' y");
            }

            Assert.Empty(drawn);
        }
    }

    /// <summary>
    ///     Validates that the links of a description fixture have PlantUML's line styles, decorations, and labels: the
    ///     links are matched by their ends.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(DescriptionFixtures))]
    public void CanvasNetPlantUml_Description_ReferenceLinks_MatchPlantUml(string type, string name)
    {
        // Arrange
        var (golden, scene, model) = LoadDescription(type, name);
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
    ///     Loads a description fixture, its golden, and CanvasNet's scene and model.
    /// </summary>
    /// <param name="type">The type folder.</param>
    /// <param name="name">The fixture name.</param>
    /// <returns>The golden, the scene, and the model.</returns>
    private static (XDocument Golden, Scene Scene, DescriptionModel Model) LoadDescription(string type, string name)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", type, name + ".puml"));
        var golden = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference", type, name + ".svg"));
        var document = PlantUmlDocument.Parse(text);
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        return (golden, scene, Assert.IsType<DescriptionModel>(document.Diagram));
    }

    /// <summary>
    ///     Compares one element of a description diagram with PlantUML's: the number of shapes of its outline, the size
    ///     of its outline, its fill, outline color and width, and the offset of every text from the outline's corner.
    /// </summary>
    /// <param name="group">The golden group of the element.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="id">The element id.</param>
    private static void AssertDescriptionElement(XElement group, Scene scene, string id)
    {
        var shape = FirstShape(group) ?? group.Element(Svg + "ellipse")!;
        var items = scene.Items.Where(i => i.Id == id).ToList();
        var outline = items.OfType<ScenePath>().Single(p => p.Role == "description");
        var (expected, actual) = (ShapeBounds(shape), Bounds(outline));

        // The outline: the same number of shapes, size, and colors
        var shapes = group.Elements().Count(e => e.Name.LocalName is "rect" or "path" or "polygon" or "ellipse" or "line");
        Assert.Equal(shapes, items.Count(i => i is ScenePath && i.Role.StartsWith("description", StringComparison.Ordinal)));
        AssertSize(expected.Width, actual.Width, $"{id} width");
        AssertSize(expected.Height, actual.Height, $"{id} height");
        Assert.Equal(Color((string)shape.Attribute("fill")!), outline.Fill);
        Assert.Equal(Color(StyleOf(shape, "stroke")!), outline.Stroke!.Color);
        Assert.Equal(double.Parse(StyleOf(shape, "stroke-width")!, CultureInfo.InvariantCulture), outline.Stroke.Width);

        // Every text at its offset from the outline, matched by content in order
        var texts = items.OfType<SceneText>().ToList();
        foreach (var text in group.Elements(Svg + "text"))
        {
            var match = texts.FirstOrDefault(t => TextOf(t) == text.Value);
            Assert.True(match is not null, $"{id}: no text '{text.Value}'");
            texts.Remove(match);
            AssertClose(Number(text, "x") - expected.X, match.X - actual.X, $"{id} '{text.Value}' x");
            AssertClose(Number(text, "y") - expected.Y, match.Baseline - actual.Y, $"{id} '{text.Value}' y");
        }

        Assert.Empty(texts);
    }

    /// <summary>
    ///     Gets the box of a golden shape: a rectangle's, an ellipse's, or the box of the end points of a polygon or a
    ///     path.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The box.</returns>
    private static SceneRect ShapeBounds(XElement shape)
    {
        switch (shape.Name.LocalName)
        {
            case "rect":
                return new SceneRect(Number(shape, "x"), Number(shape, "y"), Number(shape, "width"), Number(shape, "height"));
            case "ellipse":
                var (rx, ry) = (Number(shape, "rx"), Number(shape, "ry"));
                return new SceneRect(Number(shape, "cx") - rx, Number(shape, "cy") - ry, 2 * rx, 2 * ry);
            default:
                var points = shape.Name.LocalName == "polygon" ? Points((string)shape.Attribute("points")!) : PathPoints((string)shape.Attribute("d")!);
                var (left, top) = (points.Min(p => p.X), points.Min(p => p.Y));
                return new SceneRect(left, top, points.Max(p => p.X) - left, points.Max(p => p.Y) - top);
        }
    }

    /// <summary>
    ///     Gets the box of the end points of a scene path (as <see cref="ShapeBounds"/> reads a golden path).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The box.</returns>
    private static SceneRect Bounds(ScenePath path)
    {
        var points = path.Segments.Where(s => s.Kind != SceneSegmentKind.Close).ToList();
        var (left, top) = (points.Min(p => p.X), points.Min(p => p.Y));
        return new SceneRect(left, top, points.Max(p => p.X) - left, points.Max(p => p.Y) - top);
    }
}
