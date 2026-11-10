using DemaConsulting.CanvasNet.Mermaid.Pie;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Pie;

/// <summary>
///     Subsystem tests of the Pie subsystem (<c>PieParser</c> and <c>PieRenderer</c> together): every pie fixture parses
///     to a diagram whose sections the drawing shows, and a whole text gives the diagram and the scene.
/// </summary>
public sealed class PieTests
{
    /// <summary>
    ///     Gets the names of the pie fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that a fixture parses to a pie diagram and lays out with one legend entry per section and one slice
    ///     and label per section of 1% or more, inside the view box, the same way twice.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_Pie_Fixture_ParsesAndLaysOutConsistently(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie", name + ".mmd"));
        var document = MermaidDocument.Parse(text);

        // Act
        var scene = SceneQuery.Layout(text);
        var again = SceneQuery.Layout(text);

        // Assert
        var diagram = Assert.IsType<PieDiagram>(document.Diagram);
        var sum = diagram.Sections.Sum(section => section.Value);
        var shown = diagram.Sections.Count(section => section.Value / sum * 100 >= 1);
        var items = scene.Walk().Select(entry => entry.Item).ToList();
        Assert.Equal(diagram.Sections.Count, items.Count(item => item.Role == "pie-legend"));
        Assert.Equal(shown, items.Count(item => item.Role == "pie-slice"));
        Assert.Equal(shown, items.Count(item => item is SceneText { Role: "pie-slice-label" }));
        var bounds = Scene.BoundsOf(scene.Items)!.Value;
        Assert.True(bounds.X >= scene.ViewBox.X - 3 && bounds.Right <= scene.ViewBox.Right + 3, $"{bounds} outside {scene.ViewBox}");
        Assert.Equal(scene.ViewBox, again.ViewBox);
        Assert.Equal(items.Count, again.Walk().Count());
    }

    /// <summary>
    ///     Validates the whole pipeline on a text: the diagram's metadata and title, the percentages the scene labels the
    ///     slices with, and the legend texts with <c>showData</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Pie_Text_GivesTheDiagramAndTheScene()
    {
        // Arrange
        const string Text = "pie showData title Pets\n    \"Dogs\" : 3\n    \"Cats\" : 1\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);

        // Assert
        Assert.Equal(("pie", 2, "Pets"), (document.DiagramType, document.NodeCount, document.Title));
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().ToDictionary(text => text, text => string.Concat(text.Runs.Select(run => run.Text)));
        Assert.Equal(["75%", "25%"], texts.Where(pair => pair.Key.Role == "pie-slice-label").Select(pair => pair.Value));
        Assert.Equal(["Dogs [3]", "Cats [1]"], texts.Where(pair => pair.Key.Role == "pie-legend-text").Select(pair => pair.Value));
        Assert.Equal(["Pets"], texts.Where(pair => pair.Key.Role == "title").Select(pair => pair.Value));
    }
}
