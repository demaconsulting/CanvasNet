using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of mindmaps through the public API: the metadata is reported, the pixels carry the theme's root and
///     section colors, themes change them, the background stays transparent, the scale behaves, unsupported features are
///     reported (and fail in strict mode), an empty mindmap is an empty picture, failures are located, and rendering is
///     deterministic, concurrent, bounded and can be canceled.
/// </summary>
public class MermaidMindmapIntegrationTests
{
    /// <summary>A small mindmap: a root, three sections and a grandchild.</summary>
    private const string Simple = "mindmap\n  root\n    Origins\n      History\n    Research\n    Tools\n";

    /// <summary>
    ///     Validates the metadata of a mindmap: the nodes and the edges (one fewer), no subgraphs, no direction and no title
    ///     of its own; the frontmatter title is the fallback.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_Metadata_CountsNodesAndEdges()
    {
        // Act
        var document = MermaidDocument.Parse(Simple);
        var titled = MermaidDocument.Parse("---\ntitle: Front\n---\nmindmap\n  root\n");

        // Assert
        Assert.Equal(("mindmap", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((5, 4, 0), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
        Assert.Null(document.Title);
        Assert.Null(document.AccTitle);
        Assert.Equal("Front", titled.Title);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: the root in <c>git0</c> <c>#0000EC</c> and the first section
    ///     in <c>cScale1</c> <c>#FFFF78</c>, each read inside the node's body beside its label.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_DefaultTheme_PaintsRootAndSections()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var nodes = SceneQuery.Nodes(scene);

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2, Background = new Rgba32(255, 255, 255, 255) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0x00, 0x00, 0xEC, 255), BodyPixel(surface, scene, nodes["node_0"]));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0x78, 255), BodyPixel(surface, scene, nodes["node_1"]));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0x78, 255), BodyPixel(surface, scene, nodes["node_2"]));
    }

    /// <summary>
    ///     Validates that the theme changes the colors of the root and the sections.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_Theme_ChangesTheColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var nodes = SceneQuery.Nodes(scene);
        var white = new Rgba32(255, 255, 255, 255);

        // Act
        using var standard = document.Render(new MermaidRenderOptions { Scale = 2, Background = white }, TestContext.Current.CancellationToken);
        using var forest = document.Render(new MermaidRenderOptions { Scale = 2, Background = white, Theme = "forest" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(BodyPixel(standard, scene, nodes["node_0"]), BodyPixel(forest, scene, nodes["node_0"]));
        Assert.NotEqual(BodyPixel(standard, scene, nodes["node_1"]), BodyPixel(forest, scene, nodes["node_1"]));
    }

    /// <summary>
    ///     Validates that without a background the corners outside the drawing stay transparent.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_NoBackground_LeavesTheCornersTransparent()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Equal(0, surface.GetRowSpan(0)[0].A);
        Assert.Equal(0, surface.GetRowSpan(surface.Height - 1)[surface.Width - 1].A);
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size, which is the view box's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_Scale_MultipliesTheNaturalSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var viewBox = SceneQuery.Layout(Simple).ViewBox;

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var doubled = document.Measure(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(((int)Math.Ceiling(viewBox.Width), (int)Math.Ceiling(viewBox.Height)), (natural.Width, natural.Height));
        Assert.Equal((natural.Width * 2, natural.Height * 2), (doubled.Width, doubled.Height));
    }

    /// <summary>
    ///     Validates that each unsupported feature of a mindmap fails in strict mode with its code: an icon, a layout other
    ///     than cose-bilkent, a look other than classic, and a Font Awesome icon in a label.
    /// </summary>
    /// <param name="text">The diagram.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData("mindmap\n  root\n  ::icon(fa fa-book)\n", MermaidDiagnosticCodes.IconUnsupported)]
    [InlineData("---\nconfig:\n  layout: elk\n---\nmindmap\n  root\n", MermaidDiagnosticCodes.LayoutUnsupported)]
    [InlineData("---\nconfig:\n  look: handDrawn\n---\nmindmap\n  root\n", MermaidDiagnosticCodes.LookUnsupported)]
    [InlineData("mindmap\n  root[fa:fa-book Books]\n", MermaidDiagnosticCodes.IconUnsupported)]
    public void CanvasNetMermaid_Mindmap_UnsupportedFeatureStrict_Throws(string text, string code)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var exception = Assert.Throws<MermaidUnsupportedFeatureException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(code, exception.Feature);
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that a mindmap of its keyword alone renders, without a report, as a transparent picture of twice the
    ///     padding.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_Empty_IsATransparentPaddedPicture()
    {
        // Arrange
        var document = MermaidDocument.Parse("mindmap\n");
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((0, 0), (document.NodeCount, document.EdgeCount));
        Assert.Equal((20, 20), (surface.Width, surface.Height));
        Assert.True(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
    }

    /// <summary>
    ///     Validates that a node that follows the root at the root's indentation fails at open, located, with Mermaid's
    ///     message.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_SecondRoot_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("mindmap\n  a\n    b\n  c\n", new MermaidOpenOptions { SourceName = "map.mmd" }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal(("map.mmd", 4, 3), (exception.Location.File, exception.Location.Line, exception.Location.Column));
        Assert.Contains("There can be only one root. No parent could be found for (\"c\")", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Parse("mindmap\n  root((Center))\n    a[A]\n      a1\n      a2)Cloud(\n    b(B)\n      b1))Bang((\n    c{{C}}\n");
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
    ///     Validates that a canceled token stops a mindmap render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a layout that needs more work than allowed fails with the limit code, located at the keyword,
    ///     instead of running on.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 500 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 1), (exception.Code, exception.Location.Line));
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Reads the pixel inside a default node's body, beside its label: six units in from its left side, at its
    ///     center's height.
    /// </summary>
    /// <param name="surface">The surface, rendered at scale 2.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 BodyPixel(Surface surface, Scene scene, (double X, double Y, SceneRect Box) node) =>
        surface.GetRowSpan((int)Math.Floor((node.Y - scene.ViewBox.Y) * 2))[(int)Math.Floor((node.Box.X + 6 - scene.ViewBox.X) * 2)];
}
