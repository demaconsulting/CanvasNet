using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Packet;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Packet;

/// <summary>
///     Subsystem tests of the Packet subsystem (<c>PacketParser</c> and <c>PacketRenderer</c> together): every packet
///     fixture parses to a diagram whose blocks the drawing shows in its rows, a whole text gives the diagram and the
///     scene, invalid text fails closed, the rows follow the configured row size, and the themes paint the drawing.
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

    /// <summary>
    ///     Validates that a whole text the grammar does not accept, a block that does not follow the one before it, and
    ///     more blocks than the open limit allows are refused when the document is opened, located in the original text
    ///     (frontmatter lines included) with a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Packet_InvalidText_FailsClosedWithLocation()
    {
        // Arrange
        var options = new MermaidOpenOptions { SourceName = "p.mmd" };
        var limited = new MermaidOpenOptions { Limits = new MermaidLimits { MaxNodes = 2 } };

        // Act
        var grammar = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("---\ntitle: T\n---\npacket\n    0-7: \"a\"\n    8-15 \"b\"\n", options));
        var gap = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("packet\n    0-7: \"a\"\n    9-15: \"b\"\n", options));
        var limit = Assert.Throws<MermaidSyntaxException>(() =>
            MermaidDocument.Parse("packet\n    +1: \"a\"\n    +1: \"b\"\n    +1: \"c\"\n", limited));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, "p.mmd", 6), (grammar.Code, grammar.Location.File, grammar.Location.Line));
        Assert.NotEmpty(grammar.Expected);
        Assert.NotNull(grammar.Hint);
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, 3), (gap.Code, gap.Location.Line));
        Assert.Contains("Packet block 9 - 15 is not contiguous. It should start from 8.", gap.Message, StringComparison.Ordinal);
        Assert.NotNull(gap.Hint);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 4), (limit.Code, limit.Location.Line));
    }

    /// <summary>
    ///     Validates that the rows of a drawing follow the row size of the render's configuration, a block split at the
    ///     end of each row with its label on every part, and that a split the parts limit or Mermaid's own split refuses
    ///     fails the render, located at the block.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Packet_Rows_FollowTheConfiguredRowSize()
    {
        // Arrange
        var document = MermaidDocument.Parse("packet\n    0-15: \"Wide\"\n    16: \"F\"\n");
        var narrow = new MermaidRenderOptions { Config = "{\"packet\":{\"bitsPerRow\":8}}" };
        var fractional = new MermaidRenderOptions { Config = "{\"packet\":{\"bitsPerRow\":1.5}}" };
        var bits = MermaidDocument.Parse("packet\n    0: \"a\"\n    1: \"b\"\n    2-3: \"c\"\n");
        var huge = MermaidDocument.Parse("packet\n    0: \"a\"\n    1-99999999: \"huge\"\n");

        // Act
        var wide = document.LayoutScene(null, TestContext.Current.CancellationToken);
        var split = document.LayoutScene(narrow, TestContext.Current.CancellationToken);
        var failed = Assert.Throws<MermaidRenderException>(() => bits.LayoutScene(fractional, TestContext.Current.CancellationToken));
        var limited = Assert.Throws<MermaidRenderException>(() => huge.LayoutScene(null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Single(SceneQuery.OfRole(wide, "packet-row"));
        Assert.Equal(3, SceneQuery.OfRole(split, "packet-row").Count);
        Assert.Equal(["Wide", "Wide", "F"], SceneQuery.OfRole(split, "packet-label").Select(item => item.Id));
        Assert.Equal(258.0, split.ViewBox.Width);
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 3), (failed.Code, failed.Location.Line));
        Assert.Contains("Block start 1.5 is greater than block end 1.", failed.Message, StringComparison.Ordinal);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 3), (limited.Code, limited.Location.Line));
    }

    /// <summary>
    ///     Validates the paint of the drawing in the default and the dark theme, and that a <c>packet</c> theme variable
    ///     replaces the theme's packet colors, the keys it leaves out falling back to the default sheet.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Packet_Themes_PaintTheBlocksAndTexts()
    {
        // Arrange
        const string Text = "packet\n    0-7: \"a\"\n";
        var dark = new MermaidRenderOptions { Theme = "dark" };
        var replaced = new MermaidRenderOptions { Theme = "dark", Config = "{\"themeVariables\":{\"packet\":{\"labelColor\":\"red\"}}}" };
        var document = MermaidDocument.Parse(Text);

        // Act
        var light = document.LayoutScene(null, TestContext.Current.CancellationToken);
        var darkScene = document.LayoutScene(dark, TestContext.Current.CancellationToken);
        var replacedScene = document.LayoutScene(replaced, TestContext.Current.CancellationToken);

        // Assert
        static (Rgba32 Fill, Rgba32 Label) PaintOf(Scene scene) =>
            (Assert.IsType<ScenePath>(Assert.Single(SceneQuery.OfRole(scene, "packet-block"))).Fill!.Value,
                Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "packet-label"))).Runs[0].Color);
        Assert.Equal((new Rgba32(0xef, 0xef, 0xef, 255), new Rgba32(0, 0, 0, 255)), PaintOf(light));
        Assert.Equal((new Rgba32(0x33, 0x33, 0x33, 255), new Rgba32(0xe0, 0xdf, 0xdf, 255)), PaintOf(darkScene));
        Assert.Equal((new Rgba32(0xef, 0xef, 0xef, 255), new Rgba32(255, 0, 0, 255)), PaintOf(replacedScene));
    }
}
