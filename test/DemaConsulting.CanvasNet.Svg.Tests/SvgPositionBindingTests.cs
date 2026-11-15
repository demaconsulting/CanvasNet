using System.Numerics;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests of the SVG position bindings (<c>cn:x1y1</c>, <c>cn:x2y2</c>, <c>cn:center</c>, <c>cn:xy</c>): the namespace
///     and value syntax, the coordinates through every transform and <c>preserveAspectRatio</c>, the single load of a bound
///     image, and every failure code with its fallback. The expected coordinates are computed in the tests from the
///     placement, not read from the codec.
/// </summary>
public class SvgPositionBindingTests
{
    /// <summary>The namespace declaration every test document carries.</summary>
    private const string CanvasNetNs = "xmlns:cn='https://demaconsulting.com/canvasnet'";

    /// <summary>The bound image of most tests: 40 by 20 at 10,20.</summary>
    private const string Image = "<image id='img' href='pic.mmd' x='10' y='20' width='40' height='20'/>";

    /// <summary>The green of the pictures the resolver returns.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>
    ///     A resolver that reports positions: every call is recorded, and each picture is a green surface of the requested
    ///     size (or of a fixed size) with positions placed relative to that size.
    /// </summary>
    private sealed class PositionResolver : IImagePositionResolver
    {
        /// <summary>Gets the calls: the link, the requested size, and the selection (null for a plain call).</summary>
        public List<(string Href, int Width, int Height, PositionSelection? Selection)> Calls { get; } = [];

        /// <summary>Gets or sets the fixed size of the returned surface, or <see langword="null"/> for the requested size.</summary>
        public (int Width, int Height)? SurfaceSize { get; set; }

        /// <summary>Gets or sets a value indicating whether the positions are left out (a source that reports none).</summary>
        public bool NoPositions { get; set; }

        /// <summary>Gets or sets a value indicating whether nothing is drawn.</summary>
        public bool ReturnNull { get; set; }

        /// <summary>Gets or sets a value indicating whether the positions are marked truncated.</summary>
        public bool Truncated { get; set; }

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls.Add((href, pixelWidth, pixelHeight, null));
            return ReturnNull ? null : Draw(pixelWidth, pixelHeight);
        }

        /// <inheritdoc />
        public ImageResolution? ResolveWithPositions(string href, int pixelWidth, int pixelHeight, PositionSelection positions)
        {
            Calls.Add((href, pixelWidth, pixelHeight, positions));
            if (ReturnNull)
            {
                return null;
            }

            var surface = Draw(pixelWidth, pixelHeight);
            return new ImageResolution(surface)
            {
                Positions = NoPositions ? null : Positions(surface.Width, surface.Height),
                PositionsTruncated = Truncated
            };
        }

        /// <summary>
        ///     The positions of a picture: <c>node:a</c> at a quarter of the width and half the height, <c>node:b</c> at three
        ///     quarters, two <c>node:dup</c>, and <c>node:blank</c> without a pixel.
        /// </summary>
        /// <param name="width">The width of the picture.</param>
        /// <param name="height">The height of the picture.</param>
        /// <returns>The positions.</returns>
        public static List<ImagePosition> Positions(int width, int height) =>
        [
            new("node", "a") { Pixel = new PositionPoint(width / 4.0, height / 2.0) },
            new("node", "b") { Pixel = new PositionPoint(width * 0.75, height / 2.0) },
            new("node", "dup") { Pixel = new PositionPoint(1, 1) },
            new("node", "dup") { Pixel = new PositionPoint(2, 2) },
            new("node", "blank")
        ];

        /// <summary>
        ///     Draws the green picture.
        /// </summary>
        /// <param name="width">The requested width.</param>
        /// <param name="height">The requested height.</param>
        /// <returns>The surface.</returns>
        private Surface Draw(int width, int height)
        {
            var (w, h) = SurfaceSize ?? (width, height);
            var surface = new Surface(w, h);
            surface.Clear(Green);
            return surface;
        }
    }

    /// <summary>
    ///     A resolver without positions.
    /// </summary>
    private sealed class PlainResolver : IImageResolver
    {
        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight) => new(pixelWidth, pixelHeight);
    }

    /// <summary>
    ///     Wraps markup in a 100 by 100 document that declares the CanvasNet namespace as <c>cn</c>.
    /// </summary>
    /// <param name="body">The markup.</param>
    /// <returns>The document.</returns>
    private static string Doc(string body) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' {CanvasNetNs} viewBox='0 0 100 100'>{body}</svg>";

    /// <summary>
    ///     Renders a document and returns the binding reports.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="resolver">The resolver.</param>
    /// <param name="size">The size of the square output.</param>
    /// <returns>The reports, in document order.</returns>
    private static List<SvgPositionBinding> Bind(string svg, IImageResolver? resolver, int size = 100)
    {
        var reports = new List<SvgPositionBinding>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var surface = SvgCodec.LoadWithFontFaces(stream, size, size, null, new SvgLoadOptions
        {
            ImageResolver = resolver,
            BindingHandler = reports.Add,
            UseSystemFonts = false
        });
        return reports;
    }

    /// <summary>
    ///     Asserts that a binding was resolved to a point within 1e-3.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="x">The expected x.</param>
    /// <param name="y">The expected y.</param>
    private static void AssertAt(SvgPositionBinding report, double x, double y)
    {
        Assert.True(report.IsResolved, report.Message);
        Assert.Equal(x, report.X!.Value, 3);
        Assert.Equal(y, report.Y!.Value, 3);
    }

    /// <summary>
    ///     Test that a line endpoint bound to a position lands on the pixel of the position plus the image origin, with any
    ///     prefix bound to the namespace.
    /// </summary>
    /// <param name="prefix">The prefix of the namespace.</param>
    [Theory]
    [InlineData("cn")]
    [InlineData("q")]
    public void SvgCodec_Binding_LineEndpoint_LandsOnThePosition(string prefix)
    {
        // Arrange: node:a is at 10,10 of the 40 by 20 picture placed at 10,20
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' xmlns:{prefix}='https://demaconsulting.com/canvasnet' viewBox='0 0 100 100'>"
                  + $"{Image}<line id='l' x1='0' y1='0' x2='1' y2='1' {prefix}:x2y2='#img node:a'/></svg>";

        // Act
        var report = Assert.Single(Bind(svg, new PositionResolver()));

        // Assert
        AssertAt(report, 20, 30);
        Assert.Equal(("line", "l", "x2y2", "img", "node:a", "x2", "y2"), (report.Element, report.ElementId, report.Attribute, report.ImageId, report.Selector, report.XAttribute, report.YAttribute));
        Assert.Equal(2, report.ElementIndex);
        Assert.Null(report.Code);
    }

    /// <summary>
    ///     Test that an attribute named like a binding in another namespace, or without a namespace, is not a binding, and
    ///     that a document without bindings never asks for positions.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_OtherNamespaceOrUnprefixed_IsIgnored()
    {
        // Arrange
        var resolver = new PositionResolver();
        var svg = "<svg xmlns='http://www.w3.org/2000/svg' xmlns:o='https://example.com/other' viewBox='0 0 100 100'>"
                  + $"{Image}<line x1='0' y1='0' x2='1' y2='1' x2y2='#img node:a' o:x2y2='#img node:a'/></svg>";

        // Act
        var reports = Bind(svg, resolver);

        // Assert: no report, and the image was resolved plainly
        Assert.Empty(reports);
        Assert.Null(Assert.Single(resolver.Calls).Selection);
    }

    /// <summary>
    ///     Test that a prefix that is not declared is not well-formed XML.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_UndeclaredPrefix_IsInvalidData()
    {
        // Arrange
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'>{Image}<line cn:x2y2='#img node:a'/></svg>";

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Bind(svg, new PositionResolver()));
    }

    /// <summary>
    ///     Test that each binding attribute writes its pair, and that a text keeps its <c>dx</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_EachElementAndAttribute_IsResolved()
    {
        // Arrange: node:a at 20,30 and node:b at 40,30 in the user space of the root
        var svg = Doc(
            Image
            + "<line x1='0' y1='0' x2='0' y2='0' stroke='black' cn:x1y1='#img node:a' cn:x2y2='#img node:b'/>"
            + "<circle r='2' cn:center='#img node:a'/>"
            + "<ellipse rx='2' ry='1' cn:center='#img node:b'/>"
            + "<text dx='3' font-size='4' cn:xy='#img node:a'>T</text>");

        // Act
        var reports = Bind(svg, new PositionResolver());

        // Assert
        Assert.Equal(5, reports.Count);
        AssertAt(reports[0], 20, 30);
        AssertAt(reports[1], 40, 30);
        AssertAt(reports[2], 20, 30);
        AssertAt(reports[3], 40, 30);
        AssertAt(reports[4], 20, 30);
        Assert.Equal(("x1", "x2", "cx", "x"), (reports[0].XAttribute, reports[1].XAttribute, reports[2].XAttribute, reports[4].XAttribute));
    }

    /// <summary>
    ///     Test that a value that is not <c>#imageId selector</c> is malformed and keeps the written numbers.
    /// </summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("img node:a")]
    [InlineData("#img")]
    [InlineData("# node:a")]
    [InlineData("#img node:\"open")]
    public void SvgCodec_Binding_MalformedValue_IsReported(string value)
    {
        // Arrange
        var svg = Doc($"{Image}<line x1='0' y1='0' x2='1' y2='1' cn:x2y2='{value}'/>");

        // Act
        var report = Assert.Single(Bind(svg, new PositionResolver()));

        // Assert
        Assert.Equal(SvgPositionBinding.MalformedCode, report.Code);
        Assert.False(report.IsResolved);
        Assert.Null(report.X);
    }

    /// <summary>
    ///     Test that an id that names nothing, or names an element that is not an image, is image-missing.
    /// </summary>
    /// <param name="id">The id.</param>
    [Theory]
    [InlineData("nothing")]
    [InlineData("box")]
    public void SvgCodec_Binding_MissingImage_IsReported(string id)
    {
        // Arrange
        var svg = Doc($"{Image}<rect id='box' width='1' height='1'/><line cn:x2y2='#{id} node:a'/>");

        // Act / Assert
        Assert.Equal(SvgPositionBinding.ImageMissingCode, Assert.Single(Bind(svg, new PositionResolver())).Code);
    }

    /// <summary>
    ///     Test the bindings this version does not resolve: a pair on the wrong element, an unknown name, a list of text
    ///     coordinates, content that is not painted directly or is drawn again by a use, an image that does not link a file,
    ///     a resolver without positions, and a source that reports none.
    /// </summary>
    /// <param name="body">The markup after the namespace declaration.</param>
    [Theory]
    [InlineData(Image + "<line cn:center='#img node:a'/>")]
    [InlineData(Image + "<circle r='1' cn:spot='#img node:a'/>")]
    [InlineData("<defs>" + Image + "</defs><line cn:x2y2='#img node:a'/>")]
    [InlineData(Image + "<defs><line id='d' cn:x2y2='#img node:a'/></defs>")]
    [InlineData(Image + "<pattern id='p'><line cn:x2y2='#img node:a'/></pattern>")]
    [InlineData(Image + "<marker id='m'><line cn:x2y2='#img node:a'/></marker>")]
    [InlineData(Image + "<mask id='k'><line cn:x2y2='#img node:a'/></mask>")]
    [InlineData(Image + "<clipPath id='c'><line cn:x2y2='#img node:a'/></clipPath>")]
    [InlineData(Image + "<svg><line cn:x2y2='#img node:a'/></svg>")]
    [InlineData(Image + "<use href='#img'/><line cn:x2y2='#img node:a'/>")]
    [InlineData(Image + "<g id='grp'><line cn:x2y2='#img node:a'/></g><use href='#grp'/>")]
    [InlineData("<image id='img' href='data:image/png;base64,AAAA' width='4' height='4'/><line cn:x2y2='#img node:a'/>")]
    public void SvgCodec_Binding_UnsupportedCases_AreReported(string body)
    {
        // Act
        var report = Assert.Single(Bind(Doc(body), new PositionResolver()));

        // Assert
        Assert.Equal(SvgPositionBinding.UnsupportedCode, report.Code);
    }

    /// <summary>
    ///     Test that a text with a list of x coordinates is not bound (the binding is reported before the codec refuses
    ///     the list while it paints).
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_TextCoordinateList_IsUnsupported()
    {
        // Arrange
        var reports = new List<SvgPositionBinding>();
        var svg = Doc($"{Image}<text x='1 2' y='3' cn:xy='#img node:a'>T</text>");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        var options = new SvgLoadOptions { ImageResolver = new PositionResolver(), BindingHandler = reports.Add, UseSystemFonts = false };

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => SvgCodec.LoadWithFontFaces(stream, 100, 100, null, options));
        Assert.Equal(SvgPositionBinding.UnsupportedCode, Assert.Single(reports).Code);
    }

    /// <summary>
    ///     Test that a resolver that cannot report positions, and a source that reports none, are unsupported.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_NoPositionsAvailable_IsUnsupported()
    {
        // Arrange
        var svg = Doc($"{Image}<line cn:x2y2='#img node:a'/>");

        // Act
        var plain = Assert.Single(Bind(svg, new PlainResolver()));
        var none = Assert.Single(Bind(svg, new PositionResolver { NoPositions = true }));

        // Assert
        Assert.Equal(SvgPositionBinding.UnsupportedCode, plain.Code);
        Assert.Equal(SvgPositionBinding.UnsupportedCode, none.Code);
    }

    /// <summary>
    ///     Test the outcomes of matching: no match, several matches, a cut list, a match without a pixel, a picture that is
    ///     not drawn, a placement of no size, and a transform that cannot be inverted.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_MatchFailures_AreReported()
    {
        // Arrange
        var svg = Doc(
            Image
            + "<line cn:x2y2='#img node:zzz'/>"
            + "<line cn:x2y2='#img node:dup'/>"
            + "<line cn:x2y2='#img node:blank'/>"
            + "<g transform='scale(0)'><line cn:x2y2='#img node:a'/></g>");

        // Act
        var reports = Bind(svg, new PositionResolver());
        var truncated = Bind(Doc($"{Image}<line cn:x2y2='#img node:a'/>"), new PositionResolver { Truncated = true });
        var notDrawn = Bind(Doc($"{Image}<line cn:x2y2='#img node:a'/>"), new PositionResolver { ReturnNull = true });
        var noSize = Bind(Doc("<image id='img' href='p.mmd' width='0' height='4'/><line cn:x2y2='#img node:a'/>"), new PositionResolver());

        // Assert
        Assert.Equal(
            [SvgPositionBinding.UnmatchedCode, SvgPositionBinding.AmbiguousCode, SvgPositionBinding.UnavailableCode, SvgPositionBinding.SingularCode],
            reports.Select(r => r.Code));
        Assert.Equal(SvgPositionBinding.AmbiguousCode, Assert.Single(truncated).Code);
        Assert.Equal(SvgPositionBinding.UnavailableCode, Assert.Single(notDrawn).Code);
        Assert.Equal(SvgPositionBinding.UnavailableCode, Assert.Single(noSize).Code);
    }

    /// <summary>
    ///     Test that a binding to an image the painter does not paint (a degenerate transform, or an empty or oversized
    ///     filter or mask region) is unavailable, and that the image is neither loaded nor charged: the resolver is never
    ///     called and the work charged is that of the same document without the binding.
    /// </summary>
    /// <param name="defs">The filter or mask the image refers to, if any.</param>
    /// <param name="attributes">The attributes added to the bound image.</param>
    [Theory]
    [InlineData("", "transform='scale(0)'")]
    [InlineData("", "transform='matrix(1 0 0 0 0 0)'")]
    [InlineData("<filter id='f' width='0'><feOffset/></filter>", "filter='url(#f)'")]
    [InlineData("<filter id='f' width='1000'><feOffset/></filter>", "filter='url(#f)'")]
    [InlineData("<mask id='m' width='0'><rect width='100' height='100' fill='white'/></mask>", "mask='url(#m)'")]
    [InlineData("<mask id='m' maskUnits='userSpaceOnUse' width='100000' height='10'><rect width='100' height='100' fill='white'/></mask>", "mask='url(#m)'")]
    public void SvgCodec_Binding_ImageNotPainted_IsUnavailableAndNotLoaded(string defs, string attributes)
    {
        // Arrange
        var image = $"<image id='img' href='pic.mmd' x='10' y='20' width='40' height='20' {attributes}/>";
        var resolver = new PositionResolver();
        var bound = Doc($"<defs>{defs}</defs>{image}<circle r='1' cn:center='#img node:a'/>");
        var unbound = Doc($"<defs>{defs}</defs>{image}<circle r='1'/>");

        // Act
        var report = Assert.Single(Bind(bound, resolver));

        // Assert: the written numbers are kept, nothing was asked of the resolver, and nothing was charged for the image
        Assert.Equal(SvgPositionBinding.UnavailableCode, report.Code);
        Assert.Null(report.X);
        Assert.Empty(resolver.Calls);
        Assert.Equal(Charged(unbound), Charged(bound));
    }

    /// <summary>
    ///     Test that a binding to a painted image with a filter or a mask whose region is valid still resolves as before,
    ///     from one load, and charges what the same image charges when it is not bound.
    /// </summary>
    /// <param name="defs">The filter or mask the image refers to.</param>
    /// <param name="attributes">The attributes added to the bound image.</param>
    [Theory]
    [InlineData("<filter id='f'><feOffset dx='0' dy='0'/></filter>", "filter='url(#f)'")]
    [InlineData("<mask id='m'><rect width='100' height='100' fill='white'/></mask>", "mask='url(#m)'")]
    public void SvgCodec_Binding_ImageWithValidEffects_StillResolves(string defs, string attributes)
    {
        // Arrange
        var image = $"<image id='img' href='pic.mmd' x='10' y='20' width='40' height='20' {attributes}/>";
        var resolver = new PositionResolver();
        var bound = Doc($"<defs>{defs}</defs>{image}<circle r='1' cn:center='#img node:a'/>");
        var unbound = Doc($"<defs>{defs}</defs>{image}<circle r='1'/>");

        // Act
        var report = Assert.Single(Bind(bound, resolver));

        // Assert: node:a of the 40 by 20 picture at 10,20 is 20,30
        AssertAt(report, 20, 30);
        Assert.NotNull(Assert.Single(resolver.Calls).Selection);
        Assert.Equal(Charged(unbound), Charged(bound));
    }

    /// <summary>
    ///     Test the coordinates through the transforms of the image and of the receiving element, computed independently
    ///     with <see cref="Matrix3x2"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_Transforms_AreAppliedBothWays()
    {
        // Arrange: the callout in a translated, rotated and scaled group; a second image scaled in a group; a third rotated
        var svg = Doc(
            Image
            + "<g transform='translate(5,5) rotate(90) scale(2)'><line cn:x2y2='#img node:a'/></g>"
            + "<g transform='translate(10,0) scale(0.5)'><image id='half' href='h.mmd' x='10' y='20' width='40' height='20'/></g>"
            + "<line cn:x2y2='#half node:a'/>"
            + "<g transform='rotate(90 50 50)'><image id='turned' href='t.mmd' x='10' y='20' width='40' height='20'/></g>"
            + "<line cn:x2y2='#turned node:a'/>");
        var group = Matrix3x2.CreateScale(2) * Matrix3x2.CreateRotation(MathF.PI / 2) * Matrix3x2.CreateTranslation(5, 5);
        Matrix3x2.Invert(group, out var inverse);
        var expected = Vector2.Transform(new Vector2(20, 30), inverse);

        // Act
        var reports = Bind(svg, new PositionResolver());

        // Assert: node:a of the 20 by 10 picture is 5,5, fitted x2 into 40 by 20 is 10,10, then +10,20, x0.5, +10,0
        AssertAt(reports[0], expected.X, expected.Y);
        AssertAt(reports[1], 20, 15);

        // 20,30 turned by 90 degrees about 50,50 is 70,20
        AssertAt(reports[2], 70, 20);
    }

    /// <summary>
    ///     Test that the fit of the actual surface into the placement follows <c>preserveAspectRatio</c>, for a 20 by 20
    ///     picture in a 40 by 20 placement, with <c>node:a</c> at 5,10 of the picture.
    /// </summary>
    /// <param name="preserveAspectRatio">The value of the image.</param>
    /// <param name="x">The expected x: the fitted pixel plus 10.</param>
    /// <param name="y">The expected y: the fitted pixel plus 20.</param>
    [Theory]
    [InlineData("xMidYMid meet", 25, 30)]
    [InlineData("xMinYMin meet", 15, 30)]
    [InlineData("xMaxYMax slice", 20, 20)]
    [InlineData("none", 20, 30)]
    public void SvgCodec_Binding_PreserveAspectRatio_FitsTheActualSurface(string preserveAspectRatio, double x, double y)
    {
        // Arrange
        var svg = Doc($"<image id='img' href='p.mmd' x='10' y='20' width='40' height='20' preserveAspectRatio='{preserveAspectRatio}'/>"
                      + "<line cn:x2y2='#img node:a'/>");

        // Act
        var report = Assert.Single(Bind(svg, new PositionResolver { SurfaceSize = (20, 20) }));

        // Assert
        AssertAt(report, x, y);
    }

    /// <summary>
    ///     Test that the numbers are in user units whatever the output size, that the image is asked for at the size the
    ///     painter asks for, and that a binding before its image resolves.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_OutputSize_GivesTheSameUserCoordinates()
    {
        // Arrange: the line comes before the image (a forward reference)
        var svg = Doc($"<line cn:x2y2='#img node:a'/>{Image}");
        var small = new PositionResolver();
        var large = new PositionResolver();
        var plain = new PositionResolver();

        // Act
        var atSmall = Assert.Single(Bind(svg, small));
        var atLarge = Assert.Single(Bind(svg, large, 300));
        _ = Bind(Doc(Image), plain, 300);

        // Assert
        AssertAt(atSmall, 20, 30);
        AssertAt(atLarge, 20, 30);
        Assert.Equal((40, 20), (small.Calls[0].Width, small.Calls[0].Height));
        Assert.Equal((plain.Calls[0].Width, plain.Calls[0].Height), (large.Calls[0].Width, large.Calls[0].Height));
    }

    /// <summary>
    ///     Test that a bound image is asked for once, with the union of its selectors, and is still painted from that
    ///     surface; and that a line bound to it ends at the bound point.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_BoundImage_IsLoadedOnceAndPainted()
    {
        // Arrange: node:a of the image at 10,50 is 20,60; the line is black, 4 wide, drawn after the image
        var resolver = new PositionResolver();
        var svg = Doc(
            "<image id='img' href='p.mmd' x='10' y='50' width='40' height='20'/>"
            + "<line x1='0' y1='60' x2='90' y2='60' stroke='black' stroke-width='4' cn:x2y2='#img node:a'/>"
            + "<circle r='1' cn:center='#img node:b'/>");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));

        // Act
        using var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, null, new SvgLoadOptions { ImageResolver = resolver });

        // Assert: one call with both selectors; the picture is painted; the line stops at x 20
        var call = Assert.Single(resolver.Calls);
        Assert.Equal(["node:a", "node:b"], call.Selection!.Selectors.Select(s => s.Text));
        Assert.Equal(Green, surface[40, 55]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[10, 60]);
        Assert.Equal(Green, surface[30, 60]);
        Assert.Equal(0, surface[80, 60].A);
    }

    /// <summary>
    ///     Test that a bound image charges the shared work budget exactly what the same image charges when it is not bound.
    /// </summary>
    [Fact]
    public void SvgCodec_Binding_WorkBudget_IsChargedOnce()
    {
        // Arrange
        var bound = Doc($"{Image}<circle r='1' cn:center='#img node:a'/>");
        var unbound = Doc($"{Image}<circle r='1'/>");

        // Act
        var boundWork = Charged(bound);
        var unboundWork = Charged(unbound);

        // Assert
        Assert.Equal(unboundWork, boundWork);
        Assert.True(boundWork > 0);
    }

    /// <summary>
    ///     Renders a document with the work counters and returns the work charged.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <returns>The work charged to the shared budget.</returns>
    private static long Charged(string svg)
    {
        var diagnostics = new SvgCodec.RenderDiagnostics();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var surface = SvgCodec.LoadWithDiagnostics(stream, 100, 100, new SvgLoadOptions { ImageResolver = new PositionResolver() }, diagnostics);
        return diagnostics.WorkCharged;
    }
}
