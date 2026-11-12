using DemaConsulting.CanvasNet.Mermaid.Packet;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Packet;

/// <summary>
///     Subsystem tests of the Packet subsystem (<c>PacketParser</c> and <c>PacketRenderer</c> together): every packet
///     fixture parses to a diagram whose blocks the drawing shows in its rows, and a whole text gives the diagram and the
///     scene.
/// </summary>
public sealed class PacketTests
{
    /// <summary>
    ///     Gets the names of the packet fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that a fixture parses to a packet diagram and lays out with one label per row part, every block's
    ///     label among them, every item inside the view box, the same way twice.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_Packet_Fixture_ParsesAndLaysOutConsistently(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet", name + ".mmd"));
        var document = MermaidDocument.Parse(text);

        // Act
        var scene = SceneQuery.Layout(text);
        var again = SceneQuery.Layout(text);

        // Assert
        var diagram = Assert.IsType<PacketDiagram>(document.Diagram);
        var items = scene.Walk().Select(entry => entry.Item).ToList();
        var labels = items.Where(item => item.Role == "packet-label").Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(diagram.Blocks.Where(block => PacketRenderer.DisplayText(block.Label).Length > 0), block => Assert.Contains(block.Label, labels));
        if (Scene.BoundsOf(scene.Items) is { } bounds)
        {
            Assert.True(bounds.Y >= scene.ViewBox.Y && bounds.Bottom <= scene.ViewBox.Bottom, $"{bounds} outside {scene.ViewBox}");
        }

        Assert.Equal(scene.ViewBox, again.ViewBox);
        Assert.Equal(items.Count, again.Walk().Count());
    }

    /// <summary>
    ///     Validates the whole pipeline on a text: the diagram's metadata and title, the rows of the scene with a block
    ///     split over two of them, its label repeated, and the bit numbers of the parts.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Packet_Text_GivesTheDiagramAndTheScene()
    {
        // Arrange
        const string Text = "packet-beta\n    title Header\n    0-23: \"Address\"\n    +16: \"Length\"\n    40: \"F\"\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);

        // Assert
        Assert.Equal(("packet", 3, "Header"), (document.DiagramType, document.NodeCount, document.Title));
        Assert.Equal(2, SceneQuery.OfRole(scene, "packet-row").Count);
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().ToDictionary(text => text, text => string.Concat(text.Runs.Select(run => run.Text)));
        Assert.Equal(["Address", "Length", "Length", "F"], texts.Where(pair => pair.Key.Role == "packet-label").Select(pair => pair.Value));
        Assert.Equal(["0", "24", "32", "40"], texts.Where(pair => pair.Key.Role == "packet-bit-start").Select(pair => pair.Value));
        Assert.Equal(["23", "31", "39"], texts.Where(pair => pair.Key.Role == "packet-bit-end").Select(pair => pair.Value));
        Assert.Equal(["Header"], texts.Where(pair => pair.Key.Role == "title").Select(pair => pair.Value));
    }
}
