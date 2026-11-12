using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of flowcharts through the public API: every fixture renders strictly, the pixels carry the theme's
///     colors and the diagram's styles, the scale and the background behave, and rendering is deterministic.
/// </summary>
public class MermaidFlowchartIntegrationTests
{
    /// <summary>The warnings a fixture may give, by fixture (a missing glyph: the bundled fonts have no CJK).</summary>
    private static readonly Dictionary<string, string[]> ExpectedWarnings = new(StringComparer.Ordinal)
    {
        ["38-unicode"] = [MermaidDiagnosticCodes.TextMissingGlyph],
    };

    /// <summary>A small diagram with a node pair and a subgraph.</summary>
    private const string Simple = "flowchart LR\n  A[Start] --> B[End]\n  subgraph S [Group]\n    C[Inside]\n  end\n  B --> C\n";

    /// <summary>
    ///     Gets the names of the flowchart fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every flowchart fixture renders in strict mode, draws something, gives no report it should not
    ///     give, and measures as it renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_Flowchart_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        var allowed = ExpectedWarnings.GetValueOrDefault(name, []);
        Assert.All(warnings, warning => Assert.Contains(warning.Code, allowed));
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: node fill <c>#ECECFF</c>, node border <c>#9370DB</c>,
    ///     arrowhead <c>#333333</c> and subgraph fill <c>#ffffde</c>.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_DefaultTheme_PaintsMermaidsColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var nodes = SceneQuery.Nodes(scene);
        var a = nodes["flowchart-A-0"];
        var b = nodes["flowchart-B-1"];
        var cluster = SceneQuery.Clusters(scene)["S"];

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, a.Box.X + 4, a.Y, 4));
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), Pixel(surface, a.Box.X + 0.05, a.Y, 4));
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), Pixel(surface, b.Box.X - 6, b.Y - 2, 4));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xDE, 255), Pixel(surface, cluster.X + 3, cluster.Bottom - 3, 4));
    }

    /// <summary>
    ///     Validates that the theme changes the node fill.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("default", 0xEC, 0xEC, 0xFF)]
    [InlineData("dark", 0x1F, 0x20, 0x20)]
    [InlineData("forest", 0xCD, 0xE4, 0x98)]
    [InlineData("neutral", 0xEE, 0xEE, 0xEE)]
    public void CanvasNetMermaid_Flowchart_Theme_ChangesTheNodeFill(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var options = new MermaidRenderOptions { Scale = 2, Theme = theme };
        var a = SceneQuery.Nodes(SceneQuery.Layout(Simple, options))["flowchart-A-0"];

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, a.Box.X + 4, a.Y, 2));
    }

    /// <summary>
    ///     Validates that <c>classDef</c>, <c>class</c> and <c>style</c> paint the nodes.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_ClassDefAndStyle_PaintTheNodes()
    {
        // Arrange
        const string Text = "flowchart LR\n  A --> B\n  classDef hot fill:#f00\n  class A hot\n  style B fill:#0f0,stroke:#00f,stroke-width:4px\n";
        var document = MermaidDocument.Parse(Text);
        var nodes = SceneQuery.Nodes(SceneQuery.Layout(Text));

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), Pixel(surface, nodes["flowchart-A-0"].Box.X + 5, nodes["flowchart-A-0"].Y, 2));
        Assert.Equal(new Rgba32(0, 255, 0, 255), Pixel(surface, nodes["flowchart-B-1"].Box.X + 6, nodes["flowchart-B-1"].Y, 2));
        Assert.Equal(new Rgba32(0, 0, 255, 255), Pixel(surface, nodes["flowchart-B-1"].Box.X + 1, nodes["flowchart-B-1"].Y, 2));
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size and keeps the drawing where it was.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_Scale_KeepsTheDrawing()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var a = SceneQuery.Nodes(SceneQuery.Layout(Simple))["flowchart-A-0"];

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var tripled = document.Measure(new MermaidRenderOptions { Scale = 3 }, TestContext.Current.CancellationToken);
        using var one = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var three = document.Render(new MermaidRenderOptions { Scale = 3 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((natural.Width * 3, natural.Height * 3), (tripled.Width, tripled.Height));
        Assert.Equal((natural.Width, natural.Height), (one.Width, one.Height));
        Assert.Equal(Pixel(one, a.Box.X + 4, a.Y, 1), Pixel(three, a.Box.X + 4, a.Y, 3));
    }

    /// <summary>
    ///     Validates the background: transparent by default (as Mermaid's SVG), the given color otherwise.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_Background_IsTransparentByDefault()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);

        // Act
        using var transparent = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var white = document.Render(new MermaidRenderOptions { Background = new Rgba32(255, 255, 255, 255) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, transparent.GetRowSpan(0)[0].A);
        Assert.Equal(new Rgba32(255, 255, 255, 255), white.GetRowSpan(0)[0]);
    }

    /// <summary>
    ///     Validates that a fixed size fits the diagram into exactly that size.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_FixedSize_FitsTheDiagram()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);

        // Act
        using var surface = document.Render(300, 80, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((300, 80), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "flowchart", "40-mixed-document.mmd"));
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render();
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates the regression that a label's <c>&lt;u&gt;</c> and <c>&lt;ins&gt;</c> are underlined as Chrome
    ///     underlines them: the diagram keeps its size and the only pixels that change are a dark bar inside the node, below
    ///     the text, with no report.
    /// </summary>
    /// <param name="tag">The element.</param>
    [Theory]
    [InlineData("u")]
    [InlineData("ins")]
    public void CanvasNetMermaid_Flowchart_UnderlineElement_DrawsAnUnderline(string tag)
    {
        // Arrange
        var underlined = $"flowchart LR\n  a[\"<{tag}>under</{tag}>\"]\n";
        const string Plain = "flowchart LR\n  a[\"under\"]\n";
        var node = SceneQuery.Nodes(SceneQuery.Layout(underlined))["flowchart-a-0"];
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        using var withLine = MermaidDocument.Parse(underlined).Render(options, TestContext.Current.CancellationToken);
        using var without = MermaidDocument.Parse(Plain).Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: some pixels darken, all of them in the lower half of the node
        Assert.Equal((without.Width, without.Height), (withLine.Width, withLine.Height));
        var changed = new List<(int X, int Y)>();
        for (var y = 0; y < withLine.Height; y++)
        {
            var a = withLine.GetRowSpan(y);
            var b = without.GetRowSpan(y);
            for (var x = 0; x < withLine.Width; x++)
            {
                if (a[x] != b[x])
                {
                    changed.Add((x, y));
                    Assert.True(a[x].R + a[x].G + a[x].B < b[x].R + b[x].G + b[x].B, $"pixel ({x}, {y}) did not darken");
                }
            }
        }

        Assert.NotEmpty(changed);
        Assert.All(changed, p => Assert.True(p.X >= node.Box.X && p.X <= node.Box.Right && p.Y > node.Y && p.Y <= node.Box.Bottom, $"pixel {p} is outside the node's lower half"));
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
    }

    /// <summary>
    ///     Validates that a canceled token stops a flowchart render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Flowchart_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Reads the pixel under a point of the scene.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The scene x.</param>
    /// <param name="y">The scene y.</param>
    /// <param name="scale">The scale of the render.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor(y * scale))[(int)Math.Floor(x * scale)];
}
