// cspell:ignore vega
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Tests of <see cref="VegaLiteImageSource"/>: a Vega-Lite chart as an embedded picture - laid out at the size of
///     its placement, keeping its own background, data files confined to the folder of the file, and notes as
///     diagnostics.
/// </summary>
public class VegaLiteImageSourceTests
{
    /// <summary>A small bar chart with inline data.</summary>
    private const string BarChart =
        "{\"data\": {\"values\": [{\"a\": \"A\", \"b\": 3}, {\"a\": \"B\", \"b\": 5}]}, \"mark\": \"bar\", "
        + "\"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}, \"y\": {\"field\": \"b\", \"type\": \"quantitative\"}}}";

    /// <summary>A bar chart whose data comes from a file next to it.</summary>
    private const string FileChart =
        "{\"data\": {\"url\": \"data.csv\"}, \"mark\": \"bar\", "
        + "\"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}, \"y\": {\"field\": \"b\", \"type\": \"quantitative\"}}}";

    /// <summary>
    ///     Creates a request for a chart.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="directory">The folder of the file, or <see langword="null"/>.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <param name="resolver">The resolver scoped to the file, or <see langword="null"/>.</param>
    /// <param name="strict">Whether an unsupported feature throws.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string json, int? width = null, int? height = null, string? directory = null, long remaining = long.MaxValue, IImageResolver? resolver = null, bool strict = false)
    {
        return new ImageSourceRequest
        {
            Href = "chart.vl.json",
            FileName = "chart.vl.json",
            Content = Encoding.UTF8.GetBytes(json),
            Directory = directory,
            PixelWidth = width,
            PixelHeight = height,
            Budget = new ImageBudget(remaining),
            Resolver = resolver,
            ThrowOnUnsupportedFeature = strict
        };
    }

    /// <summary>
    ///     Test that a chart without a requested size is drawn at its own layout size, on its own opaque background.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnChartBackground()
    {
        // Arrange: the natural size as the library measures it
        var expected = VegaLiteDocument.Parse(BarChart).Measure(
            new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc, Fonts = new VegaLiteFontOptions { UseSystemFonts = false } },
            TestContext.Current.CancellationToken);

        // Act
        var result = new VegaLiteImageSource().Render(Request(BarChart));
        using var surface = result.Surface;

        // Assert: the chart keeps its own white paper, so the corner is opaque
        Assert.Equal((expected.Width, expected.Height), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[0, 0]);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that the chart is laid out afresh at the requested box, and that one requested dimension keeps the
    ///     aspect ratio of the natural size.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_RequestedSize_DrawsAtThatSize()
    {
        // Arrange
        var source = new VegaLiteImageSource();

        // Act
        using var boxed = source.Render(Request(BarChart, 120, 80)).Surface;
        using var widthOnly = source.Render(Request(BarChart, 150)).Surface;

        // Assert
        Assert.Equal((120, 80), (boxed.Width, boxed.Height));
        Assert.Equal(150, widthOnly.Width);
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the chart is drawn (the allocation stays far below the 64 MB
    ///     of the picture), and that a size that exactly fits is drawn.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var source = new VegaLiteImageSource();

        // Act: a 4000 by 4000 picture against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(BarChart, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(BarChart, 120, 80, remaining: 9600)).Surface;

        // Assert
        Assert.Equal((16_000_000L, 1000L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal((120, 80), (fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that the data file of a chart is looked up in the folder of the file and that a missing data file fails.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_DataFile_ResolvesFromDirectoryOfRequest()
    {
        // Arrange: a data file next to the chart
        var folder = Path.Combine(Path.GetTempPath(), "vl-image-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "data.csv"), "a,b\nA,3\nB,5\n");
            var source = new VegaLiteImageSource();

            // Act
            using var found = source.Render(Request(FileChart, 100, 80, folder)).Surface;

            // Assert: drawn with the data, and failing without a folder to find it in
            Assert.Equal((100, 80), (found.Width, found.Height));
            Assert.ThrowsAny<Exception>(() => source.Render(Request(FileChart, 100, 80)));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Test that the file-access policy of the request applies to the data files of the chart: a policy confined to
    ///     the folder of the chart denies a data file in the parent folder with a located error whose cause is the denial,
    ///     and the default policy of a request reads it.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_RequestPolicy_AppliesToDataFiles()
    {
        // Arrange: a data file in the parent of the chart folder
        var root = Directory.CreateTempSubdirectory("vl-image-policy").FullName;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, "chart")).FullName;
            File.WriteAllText(Path.Combine(root, "data.csv"), "a,b\nA,3\nB,5\n");
            var json = FileChart.Replace("\"data.csv\"", "\"../data.csv\"", StringComparison.Ordinal);
            var request = Request(json, 100, 80, folder);
            var confined = new ImageSourceRequest
            {
                Href = request.Href,
                FileName = request.FileName,
                Content = request.Content,
                Directory = folder,
                PixelWidth = 100,
                PixelHeight = 80,
                FileAccess = Files.FileAccessPolicy.UnderRoots(folder),
            };
            var source = new VegaLiteImageSource();

            // Act
            using var allowed = source.Render(request).Surface;
            var ex = Assert.Throws<VegaLiteRenderException>(() => source.Render(confined));

            // Assert
            Assert.Equal((100, 80), (allowed.Width, allowed.Height));
            Assert.Contains("denied by the file-access policy 'roots'", ex.Message, StringComparison.Ordinal);
            Assert.Equal("/data/url", ex.Location.Pointer);
            Assert.Equal(
                Files.FileReferenceFailureKind.AccessDenied,
                Assert.IsType<Files.FileReferenceException>(ex.InnerException).Kind);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    ///     Test that a part of the chart that is not drawn is returned as an unsupported note with the source
    ///     <c>vega-lite</c>, reported once (the library may add warnings of its own), and that the strict request turns it into an exception.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_UnsupportedFeature_ReturnsDiagnosticOrThrowsWhenStrict()
    {
        // Arrange: a custom guide encoding is reported as unsupported
        var unsupported = BarChart.Replace("\"type\": \"nominal\"", "\"type\": \"nominal\", \"axis\": {\"encode\": {\"labels\": {}}}", StringComparison.Ordinal);
        var source = new VegaLiteImageSource();

        // Act
        var result = source.Render(Request(unsupported, 100, 80));
        using var surface = result.Surface;
        var strict = new ImageSourceRequest
        {
            Href = "chart.vl.json",
            FileName = "chart.vl.json",
            Content = Encoding.UTF8.GetBytes(unsupported),
            ThrowOnUnsupportedFeature = true
        };

        // Assert
        var note = Assert.Single(result.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Unsupported);
        Assert.Equal(("vega-lite", ImageDiagnosticSeverity.Unsupported, "vl-guide-encode"), (note.Source, note.Severity, note.Code));
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => source.Render(strict));
    }

    /// <summary>
    ///     A resolver that answers every link with a red picture of the requested size and records the calls.
    /// </summary>
    private sealed class RedResolver : IImageResolver
    {
        /// <summary>Gets the links that were asked for with their sizes.</summary>
        public List<(string Href, int Width, int Height)> Calls { get; } = [];

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls.Add((href, pixelWidth, pixelHeight));
            var surface = new Surface(pixelWidth, pixelHeight);
            surface.Clear(new Rgba32(255, 0, 0, 255));
            return surface;
        }
    }

    /// <summary>A chart that shows one picture, filling its whole view.</summary>
    private const string PictureChart =
        "{\"width\": 60, \"height\": 40, \"padding\": 0, \"data\": {\"values\": [{\"a\": 1}]}, "
        + "\"mark\": {\"type\": \"image\", \"width\": 60, \"height\": 40}, "
        + "\"encoding\": {\"x\": {\"value\": 30}, \"y\": {\"value\": 20}, \"url\": {\"value\": \"icon.png\"}}}";

    /// <summary>
    ///     Test that the chart draws its pictures through the resolver of the request (the one scoped to the chart's own file), and that without one
    ///     the <c>image</c> mark is a note (an unsupported feature, and an exception in strict mode) and draws nothing.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_ImageMark_UsesTheResolverOfTheRequest()
    {
        // Arrange: the chart is laid out at 60 by 40 and the resolver is asked for the box of the picture
        var resolver = new RedResolver();
        var withResolver = Request(PictureChart, 60, 40, resolver: resolver);
        var strict = Request(PictureChart, 60, 40, strict: true);
        var source = new VegaLiteImageSource();

        // Act
        var drawn = source.Render(withResolver);
        using var drawnSurface = drawn.Surface;
        var bare = source.Render(Request(PictureChart, 60, 40));
        using var bareSurface = bare.Surface;

        // Assert: the picture was asked for and drawn over the chart, without notes
        Assert.Equal([("icon.png", 60, 40)], resolver.Calls);
        Assert.Equal(new Rgba32(255, 0, 0, 255), drawnSurface[30, 20]);
        Assert.Empty(drawn.Diagnostics);

        // Assert: without a resolver the mark is reported and nothing is drawn
        var note = Assert.Single(bare.Diagnostics);
        Assert.Equal(("vega-lite", ImageDiagnosticSeverity.Unsupported, "vl-mark-image"), (note.Source, note.Severity, note.Code));
        Assert.Equal(new Rgba32(255, 255, 255, 255), bareSurface[30, 20]);
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => source.Render(strict));
    }

    /// <summary>
    ///     Test that an invalid specification and a request without file content fail.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new VegaLiteImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => source.Render(Request("{ not json")));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.vl.json", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, its two extensions (and not <c>.json</c>), and that it claims no
    ///     scheme.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Properties_NameExtensionsAndNoSchemes()
    {
        // Arrange
        var source = new VegaLiteImageSource();

        // Act / Assert
        Assert.Equal("vega-lite", source.Name);
        Assert.Equal([".vl", ".vl.json"], source.Extensions);
        Assert.DoesNotContain(".json", source.Extensions);
        Assert.Empty(source.Schemes);
    }
}
