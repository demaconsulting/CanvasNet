// cspell:ignore href xlink hrefs
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests of the opt-in <see cref="SvgLoadOptions.ImageResolver"/> resolver: what an <c>image</c> element asks the
///     host for, which hrefs are never passed on, how the returned surface is placed, and how the work is bounded.
///     Timing is never asserted.
/// </summary>
public class SvgImageResolverTests
{
    /// <summary>
    ///     What a test resolver was asked for: the link and the device-pixel size of the placement.
    /// </summary>
    /// <param name="Href">The link.</param>
    /// <param name="PixelWidth">The placement width in device pixels.</param>
    /// <param name="PixelHeight">The placement height in device pixels.</param>
    private sealed record ImageRequest(string Href, int PixelWidth, int PixelHeight);

    /// <summary>
    ///     An <see cref="IImageResolver"/> that calls a delegate, so each test states its resolver in one line.
    /// </summary>
    /// <param name="Handler">The delegate.</param>
    private sealed record DelegateResolver(Func<ImageRequest, Surface?> Handler) : IImageResolver
    {
        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            return Handler(new ImageRequest(href, pixelWidth, pixelHeight));
        }
    }

    /// <summary>The green of the surfaces the resolvers return.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>The red of the backgrounds.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Builds a surface of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color of every pixel.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Encodes a surface as a PNG <c>data:</c> URI.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The URI.</returns>
    private static string PngUri(Surface surface)
    {
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>
    ///     Wraps markup in a 10 by 10 SVG document with a red background.
    /// </summary>
    /// <param name="body">The markup after the background.</param>
    /// <returns>The document.</returns>
    private static string Document(string body)
    {
        return $"<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' viewBox='0 0 10 10'>"
               + $"<rect width='10' height='10' fill='rgb(255,0,0)'/>{body}</svg>";
    }

    /// <summary>
    ///     Renders a document with options.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="options">The options, or <see langword="null"/> for the overload without options.</param>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    /// <returns>The surface.</returns>
    private static Surface Render(string svg, SvgLoadOptions? options, int width = 10, int height = 10)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return options == null
            ? SvgCodec.Load(stream, width, height)
            : SvgCodec.LoadWithFontFaces(stream, width, height, null, options);
    }

    /// <summary>
    ///     Renders a document and records the requests of a resolver that returns nothing.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    /// <returns>The requests, in call order.</returns>
    private static List<ImageRequest> Requests(string svg, int width = 10, int height = 10)
    {
        var requests = new List<ImageRequest>();
        using var surface = Render(
            svg,
            new SvgLoadOptions
            {
                ImageResolver = new DelegateResolver(request =>
                {
                    requests.Add(request);
                    return null;
                })
            },
            width,
            height);
        return requests;
    }

    /// <summary>
    ///     Renders a document with a resolver that returns a copy of a surface for every request.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="picture">The surface to return a copy of.</param>
    /// <returns>The rendered surface.</returns>
    private static Surface RenderWith(string svg, Surface picture)
    {
        return Render(
            svg,
            new SvgLoadOptions
            {
                ImageResolver = new DelegateResolver(_ =>
                {
                    var copy = new Surface(picture.Width, picture.Height);
                    for (var y = 0; y < picture.Height; y++)
                    {
                        for (var x = 0; x < picture.Width; x++)
                        {
                            copy[x, y] = picture[x, y];
                        }
                    }

                    return copy;
                })
            });
    }

    /// <summary>
    ///     Determines whether two surfaces hold the same pixels.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    /// <returns><see langword="true"/> when the sizes and every pixel are equal.</returns>
    private static bool SameSurface(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                if (!a[x, y].Equals(b[x, y]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Test that without a resolver (null by default) an external href still draws nothing, and that default options
    ///     are the same as no options.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverNullByDefault_ExternalHrefStillRendersNothing()
    {
        // Arrange
        var svg = Document("<image href='picture.png' x='2' y='2' width='6' height='6'/>");

        // Act
        using var withoutOptions = Render(svg, null);
        using var withDefaultOptions = Render(svg, new SvgLoadOptions());

        // Assert: the default is null, nothing is drawn, and both routes agree
        Assert.Null(new SvgLoadOptions().ImageResolver);
        Assert.Equal(Red, withoutOptions[5, 5]);
        Assert.True(SameSurface(withoutOptions, withDefaultOptions));
    }

    /// <summary>
    ///     Test that the resolver receives the raw href of both attribute forms, trimmed.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverSet_ReceivesRawHrefAndXlinkHref()
    {
        // Arrange
        var svg = Document(
            "<image href='  sub/a%20b.png ' x='0' y='0' width='4' height='4'/>"
            + "<image xlink:href='b.svg' x='4' y='0' width='4' height='4'/>");

        // Act
        var requests = Requests(svg);

        // Assert
        Assert.Equal(["sub/a%20b.png", "b.svg"], requests.Select(r => r.Href));
    }

    /// <summary>
    ///     Test that the requested size is the size of the placement in device pixels after the transforms, rounded up
    ///     and at least one pixel.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverSet_ReceivesPixelSizeAfterTransform()
    {
        // Arrange: the viewBox scale (2), a group scale, a rotation, a fractional size, and a tiny size
        var svg = Document(
            "<image href='plain' x='0' y='0' width='4' height='2'/>"
            + "<g transform='scale(2)'><image href='scaled' x='0' y='0' width='1' height='1.25'/></g>"
            + "<image href='rotated' transform='rotate(90)' x='0' y='0' width='3' height='1'/>"
            + "<image href='fraction' x='0' y='0' width='1.2' height='1.2'/>"
            + "<image href='tiny' x='0' y='0' width='0.01' height='0.01'/>");

        // Act: a 20 by 20 surface for a 10 by 10 viewBox
        var requests = Requests(svg, 20, 20).ToDictionary(r => r.Href);

        // Assert
        Assert.Equal((8, 4), (requests["plain"].PixelWidth, requests["plain"].PixelHeight));
        Assert.Equal((4, 5), (requests["scaled"].PixelWidth, requests["scaled"].PixelHeight));
        Assert.Equal((6, 2), (requests["rotated"].PixelWidth, requests["rotated"].PixelHeight));
        Assert.Equal((3, 3), (requests["fraction"].PixelWidth, requests["fraction"].PixelHeight));
        Assert.Equal((1, 1), (requests["tiny"].PixelWidth, requests["tiny"].PixelHeight));
    }

    /// <summary>
    ///     Test that a returned surface is drawn exactly like the same picture as a <c>data:</c> URI.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsSurface_IsPlacedLikeADataUri()
    {
        // Arrange: the same two-color picture by data URI and by resolver
        using var picture = Solid(2, 2, Green);
        picture[1, 1] = new Rgba32(0, 0, 255, 255);
        var attributes = "x='2' y='2' width='6' height='6'";
        var byData = Document($"<image href='{PngUri(picture)}' {attributes}/>");
        var byResolver = Document($"<image href='picture.png' {attributes}/>");

        // Act
        using var expected = Render(byData, null);
        using var actual = RenderWith(byResolver, picture);

        // Assert: it is placed in the rectangle, and nothing differs from the data URI route
        Assert.Equal(Green, actual[3, 3]);
        Assert.Equal(Red, actual[1, 1]);
        Assert.True(SameSurface(expected, actual));
    }

    /// <summary>
    ///     Test that the aspect-ratio rule of the element fits the returned surface into the placement.
    /// </summary>
    /// <param name="rule">The <c>preserveAspectRatio</c> value.</param>
    /// <param name="drawnAtTop">Whether the top edge of the placement is drawn.</param>
    [Theory]
    [InlineData("xMidYMid meet", false)]
    [InlineData("xMidYMid slice", true)]
    [InlineData("none", true)]
    public void SvgCodec_Load_ImageResolverReturnsSurface_HonorsPreserveAspectRatio(string rule, bool drawnAtTop)
    {
        // Arrange: a picture twice as wide as high in a square placement
        using var picture = Solid(4, 2, Green);
        var svg = Document($"<image href='p' x='0' y='0' width='10' height='10' preserveAspectRatio='{rule}'/>");

        // Act
        using var surface = RenderWith(svg, picture);

        // Assert: the middle is always drawn; the top row only when the fit fills the placement
        Assert.Equal(Green, surface[5, 5]);
        Assert.Equal(drawnAtTop ? Green : Red, surface[5, 0]);
    }

    /// <summary>
    ///     Test that opacity and a clip path apply to a returned surface, as they do to a <c>data:</c> raster.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsSurface_AppliesOpacityAndClipPath()
    {
        // Arrange: the left half of the placement is clipped away, and the rest is half transparent
        using var picture = Solid(2, 2, Green);
        var svg = Document(
            "<clipPath id='c'><rect x='5' y='0' width='5' height='10'/></clipPath>"
            + "<image href='p' x='0' y='0' width='10' height='10' opacity='0.5' clip-path='url(#c)'/>");

        // Act
        using var surface = RenderWith(svg, picture);

        // Assert: the left half still shows the background; the right half is a blend of green over red
        Assert.Equal(Red, surface[2, 5]);
        var blended = surface[7, 5];
        Assert.InRange(blended.R, 100, 155);
        Assert.InRange(blended.G, 100, 155);
    }

    /// <summary>
    ///     Test that <c>image-rendering: pixelated</c> selects nearest-neighbor sampling of a returned surface.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsSurface_ImageRenderingPixelatedUsesNearest()
    {
        // Arrange: a black and a white pixel enlarged to 10 by 10
        using var picture = new Surface(2, 1);
        picture[0, 0] = new Rgba32(0, 0, 0, 255);
        picture[1, 0] = new Rgba32(255, 255, 255, 255);
        var smooth = Document("<image href='p' x='0' y='0' width='10' height='10' preserveAspectRatio='none'/>");
        var pixelated = Document("<image href='p' x='0' y='0' width='10' height='10' preserveAspectRatio='none' image-rendering='pixelated'/>");

        // Act
        using var smoothSurface = RenderWith(smooth, picture);
        using var pixelatedSurface = RenderWith(pixelated, picture);

        // Assert: only two gray levels with nearest-neighbor, more with smoothing
        var pixelatedLevels = Enumerable.Range(0, 10).Select(x => (int)pixelatedSurface[x, 5].R).Distinct().Count();
        var smoothLevels = Enumerable.Range(0, 10).Select(x => (int)smoothSurface[x, 5].R).Distinct().Count();
        Assert.True(pixelatedLevels <= 2);
        Assert.True(smoothLevels > 2);
    }

    /// <summary>
    ///     Test that a resolver that returns nothing is a tolerant no-op.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsNull_RendersNothingWithoutThrowing()
    {
        // Arrange
        var svg = Document("<image href='p' x='0' y='0' width='10' height='10'/>");

        // Act
        using var surface = Render(svg, new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => null) });

        // Assert
        Assert.Equal(Red, surface[5, 5]);
    }

    /// <summary>
    ///     Test that an empty href, a <c>data:</c> URI (valid, malformed, or SVG), and a fragment never reach the
    ///     resolver.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverSet_DataUriAndFragmentAndEmptyHrefNeverCallResolver()
    {
        // Arrange: every kind of href the codec handles itself or ignores
        using var picture = Solid(2, 2, Green);
        var svg = Document(
            $"<image href='{PngUri(picture)}' x='0' y='0' width='2' height='2'/>"
            + "<image href='data:image/png;base64,@@@' x='0' y='0' width='2' height='2'/>"
            + "<image href='data:image/svg+xml;base64,PHN2Zy8+' x='0' y='0' width='2' height='2'/>"
            + "<image href='data:text/plain,hello' x='0' y='0' width='2' height='2'/>"
            + "<image href=' data:image/png;base64,AAAA' x='0' y='0' width='2' height='2'/>"
            + "<image href='#fragment' x='0' y='0' width='2' height='2'/>"
            + "<image href='' x='0' y='0' width='2' height='2'/>"
            + "<image href='   ' x='0' y='0' width='2' height='2'/>"
            + "<image x='0' y='0' width='2' height='2'/>");

        // Act
        var requests = Requests(svg);

        // Assert
        Assert.Empty(requests);
    }

    /// <summary>
    ///     Test that nothing is cached: two elements with the same href, and two renders of the same document, call the
    ///     resolver each time, so a resolver backed by a live source is asked again for every picture.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverSet_SameHrefTwiceAndTwoRenders_CallsResolverEachTime()
    {
        // Arrange: one document with the same href twice, and a resolver that counts its calls
        var svg = Document("<image href='p' x='0' y='0' width='4' height='4'/><image href='p' x='5' y='0' width='4' height='4'/>");
        var calls = 0;
        var options = new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => { calls++; return null; }) };

        // Act
        using var first = Render(svg, options);
        using var second = Render(svg, options);

        // Assert: two elements by two renders
        Assert.Equal(4, calls);
    }

    /// <summary>
    ///     Test that an exception of the resolver reaches the caller unchanged.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverThrows_ExceptionPropagatesUnchanged()
    {
        // Arrange
        var failure = new IOException("host failure");
        var svg = Document("<image href='p' x='0' y='0' width='10' height='10'/>");

        // Act
        var thrown = Assert.Throws<IOException>(() => Render(svg, new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => throw failure) }));

        // Assert
        Assert.Same(failure, thrown);
    }

    /// <summary>
    ///     Test that a <see cref="FormatException"/> of the resolver is not reported as invalid SVG data.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverThrowsFormatException_IsNotReportedAsInvalidSvgData()
    {
        // Arrange
        var failure = new FormatException("host format problem");
        var svg = Document("<image href='p' x='0' y='0' width='10' height='10'/>");

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() => Render(svg, new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => throw failure) }));

        // Assert: a host error, not an InvalidDataException about numeric data, with the cause kept
        Assert.Same(failure, thrown.InnerException);
    }

    /// <summary>
    ///     Test that every kind of exception of the resolver, other than <see cref="FormatException"/>, leaves
    ///     <c>Load</c> with its own type and instance (the output surface is released on the way out).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverThrows_PropagatesTheSameException()
    {
        // Arrange
        Exception[] failures =
        [
            new ImageSourceException("source failure", "p", null, new InvalidDataException("bad picture")),
            new ImageSourceBudgetException(100, 10),
            new IOException("host failure"),
            new OperationCanceledException("canceled"),
            new InvalidOperationException("host state")
        ];
        var svg = Document("<image href='p' x='0' y='0' width='10' height='10'/>");

        foreach (var failure in failures)
        {
            // Act
            var thrown = Assert.Throws(failure.GetType(), () => Render(svg, new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => throw failure) }));

            // Assert
            Assert.Same(failure, thrown);
        }
    }

    /// <summary>
    ///     Test that every call is charged before it is made, so a fan-out of references cannot call the resolver
    ///     without bound.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverBudget_ChargedBeforeCallAndFanOutDeclined()
    {
        // Arrange: a thousand references with a one-pixel placement each
        var body = string.Concat(Enumerable.Repeat("<image href='p' x='0' y='0' width='1' height='1'/>", 1000));
        var calls = 0;
        var diagnostics = new SvgCodec.RenderDiagnostics();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Document(body)));
        var options = new SvgLoadOptions
        {
            ImageResolver = new DelegateResolver(_ =>
            {
                calls++;
                return null;
            })
        };

        // Act
        using var surface = SvgCodec.LoadWithDiagnostics(stream, 10, 10, options, diagnostics);

        // Assert: fewer than 500 calls (50 million units, at least 100 thousand each), and the rest were declined
        Assert.InRange(calls, 100, 499);
        Assert.True(diagnostics.BudgetRejections > 0);
        Assert.True(diagnostics.WorkCharged >= calls * 100_000L);
        Assert.True(diagnostics.WorkCharged <= 50_000_000L);
    }

    /// <summary>
    ///     Test that a surface larger than the requested size is charged for the extra pixels and is dropped, and
    ///     disposed, when the budget does not allow it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsOversizedSurface_ChargedAndDeclinedOverBudget()
    {
        // Arrange: four one-pixel placements, each answered with a 4000 by 4000 surface (16 million units more than
        // was requested), so the third fits within the 50 million unit budget and the fourth does not
        var returned = new List<Surface>();
        var svg = Document(string.Concat(Enumerable.Range(0, 4).Select(i => $"<image href='p{i}' x='{i}' y='0' width='1' height='1'/>")));

        // Act
        using var surface = Render(
            svg,
            new SvgLoadOptions
            {
                ImageResolver = new DelegateResolver(_ =>
                {
                    var picture = Solid(4000, 4000, Green);
                    returned.Add(picture);
                    return picture;
                })
            });

        // Assert: the first three were drawn, the fourth was declined and shows the background, and all were disposed
        Assert.Equal(Green, surface[0, 0]);
        Assert.Equal(Green, surface[2, 0]);
        Assert.Equal(Red, surface[3, 0]);
        Assert.Equal(4, returned.Count);
        Assert.All(returned, picture => Assert.Throws<ObjectDisposedException>(() => picture[0, 0]));
    }

    /// <summary>
    ///     Test that the codec disposes a surface it drew, so the resolver cannot keep it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverReturnsSurface_IsDisposedByCodec()
    {
        // Arrange
        var picture = Solid(2, 2, Green);
        var svg = Document("<image href='p' x='0' y='0' width='10' height='10'/>");

        // Act
        using var surface = Render(svg, new SvgLoadOptions { ImageResolver = new DelegateResolver(_ => picture) });

        // Assert: drawn, then disposed
        Assert.Equal(Green, surface[5, 5]);
        Assert.Throws<ObjectDisposedException>(() => picture[0, 0]);
    }

    /// <summary>
    ///     Test that an external href of an <c>feImage</c> primitive does not use the resolver: the scope is the
    ///     <c>image</c> element.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ImageResolverSet_FeImageExternalHrefDoesNotCallResolver()
    {
        // Arrange: a filter whose only primitive is an feImage with an external href
        var svg = Document(
            "<filter id='f' x='0' y='0' width='1' height='1'><feImage href='picture.png'/></filter>"
            + "<rect x='1' y='1' width='8' height='8' fill='rgb(0,0,255)' filter='url(#f)'/>");

        // Act
        var requests = Requests(svg);

        // Assert
        Assert.Empty(requests);
    }
}
