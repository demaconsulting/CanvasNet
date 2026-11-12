using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of packet diagrams through the public API: every fixture renders strictly, the metadata is reported,
///     the pixels carry the theme's block colors, <c>packet</c> and <c>packet-beta</c> draw the same, failures and limits
///     are located, and rendering is deterministic and can be canceled.
/// </summary>
public class MermaidPacketIntegrationTests
{
    /// <summary>A small diagram: a title and three blocks over two rows.</summary>
    private const string Simple = "packet\n    title UDP\n    0-15: \"Source Port\"\n    16-31: \"Destination Port\"\n    32-63: \"Length and Checksum\"\n";

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
    ///     Validates that every packet fixture renders in strict mode, gives no report, measures as it renders, and draws
    ///     something unless it has no block.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_PacketDiagram_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("packet", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.Equal(document.NodeCount > 0, !MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a packet diagram: the block statements are the nodes, there are no edges, subgraphs
    ///     or direction, and the title and accessible title are reported; the frontmatter title is the fallback.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_Metadata_CountsTheBlocks()
    {
        // Arrange
        const string Text = "packet-beta\n    title Frame\n    accTitle: A frame\n    0-99: \"Long\"\n    +4: \"Tail\"\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var fallback = MermaidDocument.Parse("---\ntitle: Front\n---\npacket\n0: \"a\"\n");

        // Assert
        Assert.Equal(("packet", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((2, 0, 0), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Null(document.Direction);
        Assert.Equal(("Frame", "A frame"), (document.Title, document.AccTitle));
        Assert.Equal("Front", fallback.Title);
    }

    /// <summary>
    ///     Validates the pixels: the default theme's <c>#efefef</c> block fill inside a block and its black stroke on the
    ///     block's edge, the dark theme's <c>#333</c> fill, and the natural size of the view box.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_Pixels_CarryTheThemeColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var dark = MermaidDocument.Parse("%%{init: {'theme': 'dark'}}%%\n" + Simple);
        var options = new MermaidRenderOptions { Background = new Rgba32(255, 255, 255, 255) };

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var darkSurface = dark.Render(options, TestContext.Current.CancellationToken);

        // Assert: (100, 20) is inside the first block (1, 15, 507, 32), away from its label; its top edge is at y 15
        Assert.Equal((1026, 141), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(0xef, 0xef, 0xef, 255), surface.GetRowSpan(20)[100]);
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), darkSurface.GetRowSpan(20)[100]);
        Assert.True(surface.GetRowSpan(15)[100].R < 0xc0, "the block's top edge is stroked");
    }

    /// <summary>
    ///     Validates that the two keywords draw the same pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_PacketBeta_DrawsAsPacket()
    {
        // Arrange
        var packet = MermaidDocument.Parse(Simple);
        var beta = MermaidDocument.Parse(Simple.Replace("packet\n", "packet-beta\n", StringComparison.Ordinal));

        // Act
        using var first = packet.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = beta.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
    }

    /// <summary>
    ///     Validates that a block that is not contiguous fails at open, located at its statement, with Mermaid's message.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_GapBetweenBlocks_FailsLocated()
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("packet\n  0-7: \"a\"\n  9-15: \"b\"\n", new MermaidOpenOptions { SourceName = "frame.mmd" }));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, exception.Code);
        Assert.Equal(("frame.mmd", 3, 3), (exception.Location.File, exception.Location.Line, exception.Location.Column));
        Assert.Contains("Packet block 9 - 15 is not contiguous. It should start from 8.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a huge bit range, which Mermaid splits into 10,000 rows, fails with the node limit at its
    ///     statement instead of drawing them.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_HugeRange_FailsWithLimitExceeded()
    {
        // Arrange
        var document = MermaidDocument.Parse("packet\n0-99999999: \"huge\"\n");

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 2), (exception.Code, exception.Location.Line));
        Assert.Contains("MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "packet", "04-row-splits.mmd"));
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
    ///     Validates that a canceled token stops a packet render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_PacketDiagram_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }
}
