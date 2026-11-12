using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of pie charts through the public API: every fixture renders strictly, the metadata is reported, the
///     pixels carry the theme's slice and legend colors, themes change them, the background stays transparent, the scale
///     behaves, failures are located, and rendering is deterministic, concurrent and can be canceled.
/// </summary>
public class MermaidPieIntegrationTests
{
    /// <summary>A small chart: three sections and a title.</summary>
    private const string Simple = "pie title Pets\n    \"Dogs\" : 386\n    \"Cats\" : 85\n    \"Rats\" : 15\n";

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
    ///     Validates that every pie fixture renders in strict mode, draws something, gives no report, and measures as it
    ///     renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_PieChart_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("pie", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a pie chart: the sections are the nodes (a repeated label once), there are no edges,
    ///     subgraphs or direction, and the title and accessible title are reported; the frontmatter title is the
    ///     fallback.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_Metadata_CountsTheSections()
    {
        // Arrange
        const string Text = "pie title Pets\n    accTitle: Animals\n    \"Dogs\" : 3\n    \"Cats\" : 2\n    \"Dogs\" : 1\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var fallback = MermaidDocument.Parse("---\ntitle: Front\n---\npie\n\"a\" : 1\n");

        // Assert
        Assert.Equal(("pie", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((2, 0, 0), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
        Assert.Equal(("Pets", "Animals"), (document.Title, document.AccTitle));
        Assert.Equal("Front", fallback.Title);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: the legend squares in <c>pie1</c> <c>#ECECFF</c> and
    ///     <c>pie2</c> <c>#FFFFDE</c>, and the first slice in <c>pie1</c> at an opacity of 0.7 over the white background.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_DefaultTheme_PaintsSlicesAndLegend()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var legend = Legend(scene);

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2, Background = new Rgba32(255, 255, 255, 255) }, TestContext.Current.CancellationToken);

        // Assert: the legend squares, and a point inside the first slice (the right half, below its label)
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, legend[0].X + 9, legend[0].Y + 9, 2));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xDE, 255), Pixel(surface, scene, legend[1].X + 9, legend[1].Y + 9, 2));
        var slice = Pixel(surface, scene, 225 + 100, 225 + 30, 2);
        Assert.InRange(slice.R, 240, 244);
        Assert.InRange(slice.G, 240, 244);
        Assert.Equal(255, slice.B);
    }

    /// <summary>
    ///     Validates that the theme changes the colors of the sections.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="r">The red of <c>pie1</c>.</param>
    /// <param name="g">The green of <c>pie1</c>.</param>
    /// <param name="b">The blue of <c>pie1</c>.</param>
    [Theory]
    [InlineData("default", 0xEC, 0xEC, 0xFF)]
    [InlineData("forest", 0xCD, 0xE4, 0x98)]
    public void CanvasNetMermaid_PieChart_Theme_ChangesTheSectionColors(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var text = $"%%{{init: {{'theme': '{theme}'}}}}%%\n" + Simple;
        var document = MermaidDocument.Parse(text);
        var scene = SceneQuery.Layout(text);
        var legend = Legend(scene);

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, scene, legend[0].X + 9, legend[0].Y + 9, 2));
    }

    /// <summary>
    ///     Validates that without a background the corners outside the chart stay transparent.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_NoBackground_LeavesTheCornersTransparent()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, surface.GetRowSpan(1)[1].A);
        Assert.Equal(0, surface.GetRowSpan(surface.Height - 2)[1].A);
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size, which is the view box's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_Scale_MultipliesTheNaturalSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var viewBox = SceneQuery.Layout(Simple).ViewBox;

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var doubled = document.Measure(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(((int)Math.Ceiling(viewBox.Width), (int)Math.Ceiling(viewBox.Height)), (natural.Width, natural.Height));
        Assert.Equal(450, natural.Height);
        Assert.Equal((natural.Width * 2, natural.Height * 2), (doubled.Width, doubled.Height));
    }

    /// <summary>
    ///     Validates that a negative value fails at open, located at its section, with Mermaid's message.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_NegativeValue_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("pie\n  \"a\" : 1\n  \"b\" : -2\n", new MermaidOpenOptions { SourceName = "chart.mmd" }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal(("chart.mmd", 3, 3), (exception.Location.File, exception.Location.Line, exception.Location.Column));
        Assert.Contains("Negative values are not allowed in pie charts", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pie", "05-many-slices.mmd"));
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
    ///     Validates that a canceled token stops a pie chart render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a layout that needs more work than allowed fails with the limit code instead of running on.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PieChart_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 20 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the top-left corners of the legend squares in scene coordinates.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The corners, in legend order.</returns>
    private static List<(double X, double Y)> Legend(Scene scene) =>
    [
        .. scene.Walk()
            .Where(entry => entry.Item.Role == "pie-legend")
            .Select(entry => (entry.OffsetX + ((SceneGroup)entry.Item).OffsetX, entry.OffsetY + ((SceneGroup)entry.Item).OffsetY)),
    ];

    /// <summary>
    ///     Reads the pixel under a point of the scene.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="x">The x in scene units.</param>
    /// <param name="y">The y in scene units.</param>
    /// <param name="scale">The render scale.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, Scene scene, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor((y - scene.ViewBox.Y) * scale))[(int)Math.Floor((x - scene.ViewBox.X) * scale)];
}
