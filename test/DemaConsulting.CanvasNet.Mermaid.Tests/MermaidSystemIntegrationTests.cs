using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the Mermaid package through its public API: the entry points, the metadata, the error and report
///     contract, the configuration precedence, node pictures, the image source, determinism, cancellation, concurrency
///     and the third-party notices that ship with it.
/// </summary>
public class MermaidSystemIntegrationTests
{
    /// <summary>A flowchart with a frontmatter title, an accessible title, a decision, a link label and a subgraph.</summary>
    private const string Described =
        "---\ntitle: Order flow\n---\nflowchart LR\n  accTitle: Orders\n  A[Start] --> B{Ok?}\n  B -->|yes| C[Ship]\n" +
        "  subgraph S [Group]\n    C\n  end\n";

    /// <summary>The default theme's node fill.</summary>
    private static readonly Rgba32 DefaultFill = new(0xEC, 0xEC, 0xFF, 255);

    /// <summary>The forest theme's node fill.</summary>
    private static readonly Rgba32 ForestFill = new(0xCD, 0xE4, 0x98, 255);

    /// <summary>The neutral theme's node fill.</summary>
    private static readonly Rgba32 NeutralFill = new(0xEE, 0xEE, 0xEE, 255);

    /// <summary>The dark theme's node fill.</summary>
    private static readonly Rgba32 DarkFill = new(0x1F, 0x20, 0x20, 255);

    /// <summary>
    ///     Validates that a string, a stream with a byte-order mark and a file path give the same document and the same
    ///     pixels, and that a file is named by its path.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Open_StringStreamAndPath_Agree()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(Described);
        var path = MermaidTestHelpers.WriteTempFile(bytes);
        try
        {
            using var stream = new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. bytes]);

            // Act
            var parsed = MermaidDocument.Parse(Described);
            var opened = MermaidDocument.Open(path);
            var streamed = MermaidDocument.Open(stream);
            using var fromText = parsed.Render(cancellationToken: TestContext.Current.CancellationToken);
            using var fromPath = opened.Render(cancellationToken: TestContext.Current.CancellationToken);
            using var fromStream = streamed.Render(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(path, opened.SourceName);
            Assert.Equal(
                (parsed.DiagramType, parsed.Title, parsed.NodeCount, parsed.EdgeCount),
                (streamed.DiagramType, streamed.Title, streamed.NodeCount, streamed.EdgeCount));
            Assert.True(MermaidTestHelpers.PixelsEqual(fromText, fromPath));
            Assert.True(MermaidTestHelpers.PixelsEqual(fromText, fromStream));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a parsed flowchart describes itself without rendering: type, support, titles, direction and
    ///     counts.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Metadata_Flowchart_DescribesTheDiagram()
    {
        // Act
        var document = MermaidDocument.Parse(Described);

        // Assert
        Assert.Equal("flowchart", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Equal("Order flow", document.Title);
        Assert.Equal("Orders", document.AccTitle);
        Assert.Equal("LR", document.Direction);
        Assert.Equal((3, 2, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
    }

    /// <summary>
    ///     Validates that invalid text fails at open with a coded, located exception that lists what was expected, and
    ///     that an unknown diagram keyword names the nearest known keyword.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_SyntaxErrors_AreLocatedCodedAndHinted()
    {
        // Act
        var syntax = Assert.Throws<MermaidSyntaxException>(
            () => MermaidDocument.Parse("flowchart LR\n  A[open --> B\n", new MermaidOpenOptions { SourceName = "bad.mmd" }));
        var unknown = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("Flowchart LR\n  A --> B\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, syntax.Code);
        Assert.Equal("bad.mmd", syntax.Location.File);
        Assert.True(syntax.Location.Line >= 2);
        Assert.NotEmpty(syntax.Expected);
        Assert.StartsWith("bad.mmd:", syntax.Message, StringComparison.Ordinal);
        Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, unknown.Code);
        Assert.Contains("did you mean 'flowchart'", unknown.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the configuration precedence: the caller's theme is replaced by the frontmatter's, which is replaced
    ///     by an init directive.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Configuration_TextOverridesTheCaller()
    {
        // Arrange
        const string Body = "flowchart LR\n  A[Start] --> B[End]\n";
        const string Frontmatter = "---\nconfig:\n  theme: forest\n---\n";
        const string Directive = "%%{init: {\"theme\": \"neutral\"}}%%\n";
        var options = new MermaidRenderOptions { Scale = 2, Theme = "dark" };

        // Act
        using var caller = MermaidDocument.Parse(Body).Render(options, TestContext.Current.CancellationToken);
        using var frontmatter = MermaidDocument.Parse(Frontmatter + Body).Render(options, TestContext.Current.CancellationToken);
        using var directive = MermaidDocument.Parse(Frontmatter + Directive + Body).Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Contains(caller, DarkFill));
        Assert.True(Contains(frontmatter, ForestFill));
        Assert.False(Contains(frontmatter, DarkFill));
        Assert.True(Contains(directive, NeutralFill));
        Assert.False(Contains(directive, ForestFill));
    }

    /// <summary>
    ///     Validates that a node picture is loaded only through the caller's resolver and drawn, and that without a
    ///     resolver the link is reported and the diagram is still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ImageEmbedding_PictureComesFromTheResolver()
    {
        // Arrange
        const string Text = "flowchart LR\n  A@{ img: \"pic.png\", label: \"Part\", w: 60, h: 60 } --> B[Next]\n";
        var red = new Rgba32(255, 0, 0, 255);
        var resolver = new SolidResolver(red);
        var (options, warnings, _) = MermaidTestHelpers.Collecting();
        var document = MermaidDocument.Parse(Text);

        // Act
        using var drawn = document.Render(new MermaidRenderOptions { ImageResolver = resolver }, TestContext.Current.CancellationToken);
        using var unresolved = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["pic.png"], resolver.Links.Distinct());
        Assert.True(Contains(drawn, red));
        Assert.False(Contains(unresolved, red));
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ImageUnresolved, 2), (warning.Code, warning.Location.Line));
    }

    /// <summary>
    ///     Validates that unsupported features and ignored styles are reported with stable codes and locations (a style
    ///     at the node it paints), while the diagram is still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Reports_AreCodedAndLocated()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n---\nflowchart LR\n  A --> B\n  style A text-decoration:underline\n";
        var document = MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "r.mmd" });
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Contains(surface, DefaultFill));
        var feature = Assert.Single(unsupported);
        Assert.Equal((MermaidDiagnosticCodes.LookUnsupported, "r.mmd", 3), (feature.Feature, feature.Location.File, feature.Location.Line));
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.StyleIgnored, 6), (warning.Code, warning.Location.Line));
    }

    /// <summary>
    ///     Validates that the image source draws Mermaid bytes at the document's natural size and at a requested size.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ImageSource_DrawsDiagramBytes()
    {
        // Arrange
        var source = new MermaidImageSource();
        var natural = MermaidDocument.Parse(Described).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = source.Render(new ImageSourceRequest
        {
            Href = "flow.mmd",
            FileName = "flow.mmd",
            Content = Encoding.UTF8.GetBytes(Described),
            CancellationToken = TestContext.Current.CancellationToken,
        });
        using var surface = result.Surface;

        // Assert
        Assert.Equal((natural.Width, natural.Height), (surface.Width, surface.Height));
        Assert.True(Contains(surface, DefaultFill));
        Assert.Empty(result.Diagnostics);
    }
    /// <summary>
    ///     Validates that the license notices of the ported projects ship with the package and that the provenance names
    ///     each of them.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ThirdPartyNotices_ShipWithPackage()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, "Notices");

        // Act
        var mermaid = File.ReadAllText(Path.Combine(folder, "LICENSE-mermaid.txt"));
        var yaml = File.ReadAllText(Path.Combine(folder, "LICENSE-js-yaml.txt"));
        var khroma = File.ReadAllText(Path.Combine(folder, "LICENSE-khroma.txt"));
        var dagre = File.ReadAllText(Path.Combine(folder, "LICENSE-dagre.txt"));
        var lodash = File.ReadAllText(Path.Combine(folder, "LICENSE-lodash.txt"));
        var marked = File.ReadAllText(Path.Combine(folder, "LICENSE-marked.txt"));
        var d3 = File.ReadAllText(Path.Combine(folder, "LICENSE-d3.txt"));
        var provenance = File.ReadAllText(Path.Combine(folder, "third-party-provenance.txt"));

        // Assert
        Assert.Contains("Copyright (c) 2014 - 2022 Knut Sveidqvist", mermaid, StringComparison.Ordinal);
        Assert.Contains("Copyright (C) 2011-2015 by Vitaly Puzrin", yaml, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2019-present Fabio Spampinato, Andrew Maney", khroma, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2012-2014 Chris Pettitt", dagre, StringComparison.Ordinal);
        Assert.Contains("dagre-es contributors", dagre, StringComparison.Ordinal);
        Assert.Contains("Copyright OpenJS Foundation and other contributors", lodash, StringComparison.Ordinal);
        Assert.Contains("Christopher Jeffrey", marked, StringComparison.Ordinal);
        Assert.Contains("Copyright 2010-2022 Mike Bostock", d3, StringComparison.Ordinal);
        Assert.Contains("not affiliated", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-mermaid.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-js-yaml.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-khroma.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-dagre.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-lodash.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-marked.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-d3.txt", provenance, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops measuring and rendering.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.ClassText);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Measure(cancellationToken: source.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(10, 10, cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a document can be rendered from several threads at once with identical results and reports.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_Concurrent_IsDeterministic()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.ClassText);

        // Act
        var results = Enumerable.Range(0, 8).AsParallel().Select(_ =>
        {
            var (options, _, unsupported) = MermaidTestHelpers.Collecting();
            using var surface = document.Render(options);
            return (surface.Width, surface.Height, Report: unsupported.Single().ToString());
        }).ToList();

        // Assert
        Assert.All(results, r => Assert.Equal(results[0], r));
    }

    /// <summary>
    ///     Gets whether any pixel of a surface has exactly a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <returns>Whether one does.</returns>
    private static bool Contains(Surface surface, Rgba32 color)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            if (surface.GetRowSpan(y).Contains(color))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     A resolver that answers every link with a solid picture and records the links.
    /// </summary>
    /// <param name="color">The picture's color.</param>
    private sealed class SolidResolver(Rgba32 color) : IImageResolver
    {
        /// <summary>Gets the links asked for, in order.</summary>
        public List<string> Links { get; } = [];

        /// <inheritdoc/>
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Links.Add(href);
            var surface = new Surface(8, 8);
            surface.Clear(color);
            return surface;
        }
    }
}
