using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="NodeImages"/>: pictures only through the caller's resolver, natural sizes, the reports of
///     pictures that are not drawn and the <see cref="MermaidLimits.MaxImages"/> budget.
/// </summary>
public sealed class NodeImagesTests
{
    /// <summary>
    ///     Checks that without a resolver nothing is loaded and the link is reported once.
    /// </summary>
    [Fact]
    public void NodeImages_NaturalSize_NoResolver_ReportsOnceAndGivesNull()
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var images = new NodeImages(null, 10, sink);
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var first = images.NaturalSize("pic.png", new MermaidLocation(null, 1, 2));
        var second = images.NaturalSize("pic.png", new MermaidLocation(null, 5, 6));
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ImageUnresolved, warning.Code);
        Assert.Equal(new MermaidLocation(null, 1, 2), warning.Location);
        Assert.Equal(0, images.Calls);
    }

    /// <summary>
    ///     Checks that the natural size is the size of what the resolver returns, asked for once per link.
    /// </summary>
    [Fact]
    public void NodeImages_NaturalSize_Resolver_AsksOnce()
    {
        // Arrange
        var resolver = new CountingResolver(40, 30);
        var images = new NodeImages(resolver, 10, new MermaidReportSink(100, false));

        // Act
        var first = images.NaturalSize("pic.png", default);
        var second = images.NaturalSize("pic.png", default);

        // Assert
        Assert.Equal((40.0, 30.0), first);
        Assert.Equal(first, second);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal([(NodeImages.ProbePixels, NodeImages.ProbePixels)], resolver.Requests);
    }

    /// <summary>
    ///     Checks that a link the resolver gives nothing for is reported.
    /// </summary>
    [Fact]
    public void NodeImages_NaturalSize_ResolverGivesNothing_Reports()
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var images = new NodeImages(new CountingResolver(0, 0), 10, sink);
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var size = images.NaturalSize("https://example.com/pic.png", default);
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Null(size);
        Assert.Equal(MermaidDiagnosticCodes.ImageUnresolved, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Checks that the painter's loads go to the resolver at the device size.
    /// </summary>
    [Fact]
    public void NodeImages_Load_Resolver_AsksAtDeviceSize()
    {
        // Arrange
        var resolver = new CountingResolver(40, 30);
        var images = new NodeImages(resolver, 10, new MermaidReportSink(100, false));

        // Act
        using var picture = images.Load("pic.png", 80, 60);

        // Assert
        Assert.NotNull(picture);
        Assert.Equal([(80, 60)], resolver.Requests);
    }

    /// <summary>
    ///     Checks that the call that would pass <see cref="MermaidLimits.MaxImages"/> fails, located at the element that
    ///     named the link, without calling the resolver.
    /// </summary>
    [Fact]
    public void NodeImages_NaturalSize_BeyondMaxImages_ThrowsLimitExceeded()
    {
        // Arrange
        var resolver = new CountingResolver(10, 10);
        var images = new NodeImages(resolver, 2, new MermaidReportSink(100, false));
        images.NaturalSize("a.png", default);
        images.NaturalSize("b.png", default);

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => images.NaturalSize("c.png", new MermaidLocation(null, 7, 3)));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(7, exception.Location.Line);
        Assert.Contains("MaxImages", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, resolver.Calls);
    }

    /// <summary>
    ///     A resolver that counts its calls and gives a picture of one size (nothing for a size of zero).
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private sealed class CountingResolver(int width, int height) : IImageResolver
    {
        /// <summary>Gets the number of calls.</summary>
        public int Calls => Requests.Count;

        /// <summary>Gets the sizes asked for.</summary>
        public List<(int Width, int Height)> Requests { get; } = [];

        /// <inheritdoc/>
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Requests.Add((pixelWidth, pixelHeight));
            return width > 0 ? new Surface(width, height) : null;
        }
    }
}
