// cspell:ignore href
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests of <see cref="SvgImageSource"/>: the SVG file as an embedded picture, drawn at the size of its placement
///     over a transparent background, with its own <c>image</c> elements resolved through the scoped resolver.
/// </summary>
public class SvgImageSourceTests
{
    /// <summary>The red the test documents are filled with.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>The green the nested picture has.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>
    ///     An <see cref="IImageResolver"/> that records what it is asked for and returns a green surface.
    /// </summary>
    private sealed class RecordingResolver : IImageResolver
    {
        /// <summary>Gets the requests the resolver received.</summary>
        public List<(string Href, int Width, int Height)> Requests { get; } = [];

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Requests.Add((href, pixelWidth, pixelHeight));
            var surface = new Surface(2, 2);
            surface.Clear(Green);
            return surface;
        }
    }

    /// <summary>
    ///     Creates a request for an SVG document.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="resolver">The resolver of the nested links, or <see langword="null"/>.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string svg, int? width = null, int? height = null, IImageResolver? resolver = null, long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "diagram.svg",
            FileName = "diagram.svg",
            Content = Encoding.UTF8.GetBytes(svg),
            PixelWidth = width,
            PixelHeight = height,
            Resolver = resolver,
            RemainingPixels = remaining
        };
    }

    /// <summary>
    ///     A 40 by 30 document that is red except for a transparent margin.
    /// </summary>
    private const string RedDocument =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='30' viewBox='0 0 40 30'>"
        + "<rect x='10' y='10' width='20' height='10' fill='#ff0000'/></svg>";

    /// <summary>
    ///     Test that the document is drawn at its natural size over a transparent background when no size is requested.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act
        var result = source.Render(Request(RedDocument));
        using var surface = result.Surface;

        // Assert
        Assert.Equal((40, 30), (surface.Width, surface.Height));
        Assert.Equal(0, surface[1, 1].A);
        Assert.Equal(Red, surface[20, 15]);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Echo);
        Assert.Null(result.Format);
    }

    /// <summary>
    ///     Test that the requested box is the exact size of the picture, and that one requested dimension keeps the
    ///     aspect ratio.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_RequestedSize_DrawsAtThatSize()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act
        using var boxed = source.Render(Request(RedDocument, 80, 60)).Surface;
        using var widthOnly = source.Render(Request(RedDocument, 80)).Surface;

        // Assert: drawn at the box, so the red rectangle covers the middle at twice the size
        Assert.Equal((80, 60), (boxed.Width, boxed.Height));
        Assert.Equal(Red, boxed[40, 30]);
        Assert.Equal((80, 60), (widthOnly.Width, widthOnly.Height));
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the document is drawn (the allocation stays far below the
    ///     64 MB of the picture), and that a size that exactly fits is drawn.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act: a 4000 by 4000 picture against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(RedDocument, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(RedDocument, 80, 60, remaining: 4800)).Surface;

        // Assert
        Assert.Equal((16_000_000L, 1000L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal((80, 60), (fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that the <c>image</c> elements of the document are resolved through the resolver of the request, with the
    ///     size of their placement, and are drawn.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_NestedImage_UsesResolverOfRequest()
    {
        // Arrange: a document that embeds a picture in a 20 by 10 placement
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='30' viewBox='0 0 40 30'>"
                           + "<image href='inner.png' x='10' y='10' width='20' height='10' preserveAspectRatio='none'/></svg>";
        var resolver = new RecordingResolver();

        // Act
        using var surface = new SvgImageSource().Render(Request(svg, resolver: resolver)).Surface;

        // Assert
        Assert.Equal([("inner.png", 20, 10)], resolver.Requests);
        Assert.Equal(Green, surface[20, 15]);
    }

    /// <summary>
    ///     Test that without a resolver the nested links are not drawn and nothing fails.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_NestedImageWithoutResolver_DrawsNothing()
    {
        // Arrange
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='30'>"
                           + "<image href='inner.png' x='10' y='10' width='20' height='10'/></svg>";

        // Act
        using var surface = new SvgImageSource().Render(Request(svg)).Surface;

        // Assert
        Assert.Equal(0, surface[20, 15].A);
    }

    /// <summary>
    ///     Test that every call draws a new surface (nothing is kept or reused).
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_TwiceForSameRequest_ReturnsDistinctSurfaces()
    {
        // Arrange
        var source = new SvgImageSource();
        var request = Request(RedDocument);

        // Act
        using var first = source.Render(request).Surface;
        using var second = source.Render(request).Surface;

        // Assert
        Assert.NotSame(first, second);
    }

    /// <summary>
    ///     Test that content that is not an SVG document, a request without a file, a null request and a canceled
    ///     request are rejected.
    /// </summary>
    [Fact]
    public void SvgImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new SvgImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => source.Render(Request("this is not an svg")));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(
            () => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.svg", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, its extension, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void SvgImageSource_Properties_NameExtensionAndNoSchemes()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act / Assert
        Assert.Equal("svg", source.Name);
        Assert.Equal([".svg"], source.Extensions);
        Assert.Empty(source.Schemes);
    }

    /// <summary>
    ///     Test that the shared extension and scheme lists cannot be changed by casting them to an array or
    ///     <see cref="IList{T}"/>.
    /// </summary>
    [Fact]
    public void SvgImageSource_Properties_ExtensionsAndSchemesAreReadOnly()
    {
        // Arrange
        var source = new SvgImageSource();

        // Act
        var extensions = source.Extensions;
        var schemes = source.Schemes;

        // Assert
        Assert.False(extensions is string[]);
        Assert.False(schemes is string[]);
        Assert.True(extensions is IList<string> { IsReadOnly: true });
        Assert.True(schemes is IList<string> { IsReadOnly: true });
        Assert.Throws<NotSupportedException>(() => ((IList<string>)extensions).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)schemes).Add("x"));
    }
}
