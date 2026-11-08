using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level tests of the <c>image</c> mark through <see cref="VegaLiteDocument"/> with a fake <see cref="IImageResolver"/> (Vega-Lite Phase 13): the position of the
///     pictures on the canvas (from the scene the document builds), the size the resolver is asked for at each scale, <see cref="VegaLiteDocument.Measure"/> never calling it, the per-render cache
///     and <see cref="VegaLiteLimits.MaxImages"/>, the report of a chart without a resolver (<c>vl-mark-image</c>, strict mode included) and of a picture the resolver does not give
///     (<c>vl-mark-image-unresolved</c>), the propagation of the resolver's exceptions and the release of every surface. Pixels come from generated pictures of one color, so no
///     expectation depends on a codec or a resampling filter.
/// </summary>
public class VegaLiteImageMarkIntegrationTests
{
    /// <summary>Opaque white, the default background of a chart.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque blue.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>A chart with two pictures of 20 by 20 pixels, a link for each row, on scales of 0 to 10 without axes.</summary>
    private const string TwoIcons = """
        {
          "width": 200, "height": 100, "padding": 0,
          "data": { "values": [ { "x": 2, "y": 5, "u": "red.png" }, { "x": 8, "y": 5, "u": "blue.png" } ] },
          "mark": { "type": "image", "width": 20, "height": 20 },
          "encoding": {
            "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
            "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
            "url": { "field": "u", "type": "nominal" }
          }
        }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A resolver that records its calls and hands out pictures it makes.
    /// </summary>
    private sealed class FakeResolver : IImageResolver
    {
        /// <summary>The calls: the link and the requested size.</summary>
        public List<(string Href, int Width, int Height)> Calls { get; } = [];

        /// <summary>The surfaces that were handed out.</summary>
        public List<Surface> Handed { get; } = [];

        /// <summary>Makes the picture of a link and a size; null for none.</summary>
        public Func<string, int, int, Surface?> Make { get; init; } = (href, w, h) => Solid(w, h, href.StartsWith("blue", StringComparison.Ordinal) ? Blue : Red);

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Calls.Add((href, pixelWidth, pixelHeight));
            var surface = Make(href, pixelWidth, pixelHeight);
            if (surface is not null)
            {
                Handed.Add(surface);
            }

            return surface;
        }
    }

    /// <summary>
    ///     Makes a picture of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Gets where the pictures of a document are on the canvas at a scale of one.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <returns>The pictures with the absolute left and top edge of each (for a picture that has a width and height of its own).</returns>
    private static List<(SceneImage Item, double Left, double Top)> Pictures(VegaLiteDocument document, VegaLiteRenderOptions options) =>
        [.. document.BuildScene(options, Ct).FindAll("mark-image").Select(e => (Assert.IsType<SceneImage>(e.Item), e.OffsetX + ((SceneImage)e.Item).X, e.OffsetY + ((SceneImage)e.Item).Y))];

    /// <summary>
    ///     Test that every picture is drawn in its box at the position of its row, with the picture its link names.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMark_DrawsEveryPictureAtItsPosition()
    {
        // Arrange: values 2 and 8 are at 40 and 160 pixels across and the value 5 at 50 pixels down; a box of 20 by 20 is centered there
        var document = VegaLiteDocument.Parse(TwoIcons);
        var resolver = new FakeResolver();
        var options = new VegaLiteRenderOptions { ImageResolver = resolver, ThrowOnUnsupportedFeature = true };

        // Act
        using var surface = document.Render(options, Ct);
        var pictures = Pictures(document, options);

        // Assert: the scene says where the boxes are (30 to 50 and 150 to 170 across, 40 to 60 down)
        Assert.Equal([(30d, 40d), (150d, 40d)], pictures.Select(p => (p.Left, p.Top)));
        Assert.Equal([("red.png", 20, 20), ("blue.png", 20, 20)], resolver.Calls);
        Assert.Equal(Red, surface[30, 40]);
        Assert.Equal(Red, surface[49, 59]);
        Assert.Equal(White, surface[29, 40]);
        Assert.Equal(White, surface[50, 60]);
        Assert.Equal(Blue, surface[150, 40]);
        Assert.Equal(Blue, surface[169, 59]);
        Assert.Equal(White, surface[100, 50]);
    }

    /// <summary>
    ///     Test that measuring never asks the resolver and gives the size of the rendered surface.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Measure_ImageMark_NeverCallsTheResolver()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TwoIcons);
        var resolver = new FakeResolver();
        var options = new VegaLiteRenderOptions { ImageResolver = resolver, ThrowOnUnsupportedFeature = true };

        // Act
        var size = document.Measure(options, Ct);
        using var surface = document.Render(options, Ct);

        // Assert
        Assert.Equal((200, 100), (size.Width, size.Height));
        Assert.Equal((surface.Width, surface.Height), (size.Width, size.Height));
        Assert.Equal(2, resolver.Calls.Count);
    }

    /// <summary>
    ///     Test that the resolver is asked for the box in device pixels of the render scale, and of the fit scale of the size overload.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMark_AsksForTheBoxInDevicePixels()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TwoIcons);
        var scaled = new FakeResolver();
        var fitted = new FakeResolver();

        // Act: a scale of 2.5 asks for 50 by 50; a fit into 600 by 300 is a factor of 3
        using var atScale = document.Render(new VegaLiteRenderOptions { ImageResolver = scaled, Scale = 2.5 }, Ct);
        using var inBox = document.Render(600, 300, new VegaLiteRenderOptions { ImageResolver = fitted }, Ct);

        // Assert
        Assert.Equal([("red.png", 50, 50), ("blue.png", 50, 50)], scaled.Calls);
        Assert.Equal([("red.png", 60, 60), ("blue.png", 60, 60)], fitted.Calls);
        Assert.Equal(Red, atScale[(int)(30 * 2.5), (int)(40 * 2.5)]);
        Assert.Equal(Blue, inBox[(150 * 3) + 5, (40 * 3) + 5]);
    }

    /// <summary>
    ///     Test that a chart with no resolver reports <c>vl-mark-image</c> once, draws no picture and asks nothing; with strict mode it fails with the same identifier.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkWithoutResolver_IsReportedAndDrawsNothing()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TwoIcons);
        var unsupported = new List<VegaLiteUnsupportedFeature>();

        // Act
        using var surface = document.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = unsupported.Add }, Ct);
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));

        // Assert
        var report = Assert.Single(unsupported);
        Assert.Equal(("vl-mark-image", "/mark"), (report.Feature, report.Location.Pointer));
        Assert.Contains("ImageResolver", report.Message, StringComparison.Ordinal);
        Assert.Equal("vl-mark-image", strict.Feature);
        Assert.Equal(White, surface[40, 50]);
        Assert.Equal(White, surface[160, 50]);
    }

    /// <summary>
    ///     Test that a resolver removes the report of the mark, in strict mode too, and that the caller's configuration (which makes the catalog visit again) keeps that.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkWithResolver_IsNotReported()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TwoIcons);
        var unsupported = new List<VegaLiteUnsupportedFeature>();

        // Act
        using var plain = document.Render(new VegaLiteRenderOptions { ImageResolver = new FakeResolver(), UnsupportedFeatureHandler = unsupported.Add }, Ct);
        using var configured = document.Render(new VegaLiteRenderOptions { ImageResolver = new FakeResolver(), Config = "{ \"background\": \"#eeeeee\" }", UnsupportedFeatureHandler = unsupported.Add }, Ct);
        var withoutResolver = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { Config = "{ \"background\": \"#eeeeee\" }", ThrowOnUnsupportedFeature = true }, Ct));

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal("vl-mark-image", withoutResolver.Feature);
    }

    /// <summary>
    ///     Test that a picture the resolver does not give is a warning (one for the link), that strict mode does not turn it into a failure, and that the other pictures are drawn.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkUnresolved_WarnsAndDrawsTheRest()
    {
        // Arrange: the first link is not resolved
        var document = VegaLiteDocument.Parse(TwoIcons);
        var resolver = new FakeResolver { Make = (href, w, h) => href == "red.png" ? null : Solid(w, h, Blue) };
        var warnings = new List<VegaLiteDiagnostic>();
        var options = new VegaLiteRenderOptions { ImageResolver = resolver, ThrowOnUnsupportedFeature = true, WarningHandler = warnings.Add };

        // Act
        using var surface = document.Render(options, Ct);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-mark-image-unresolved:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("red.png", warning.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", warning.Location.Pointer);
        Assert.Equal(White, surface[40, 50]);
        Assert.Equal(Blue, surface[160, 50]);
    }

    /// <summary>
    ///     Test that one picture that many rows show is asked for once, and that the number of different pictures is limited by <see cref="VegaLiteLimits.MaxImages"/>.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMark_CachesPerRenderAndHonorsMaxImages()
    {
        // Arrange: four rows with two links
        const string chart = """
            {
              "width": 200, "height": 100, "padding": 0,
              "data": { "values": [ { "x": 1, "u": "a.png" }, { "x": 2, "u": "a.png" }, { "x": 3, "u": "b.png" }, { "x": 4, "u": "a.png" } ] },
              "mark": { "type": "image", "width": 10, "height": 10 },
              "encoding": { "x": { "field": "x", "type": "quantitative", "axis": null }, "url": { "field": "u", "type": "nominal" } }
            }
            """;
        var document = VegaLiteDocument.Parse(chart);
        var resolver = new FakeResolver();
        var tight = new FakeResolver();

        // Act
        using var surface = document.Render(new VegaLiteRenderOptions { ImageResolver = resolver, Limits = new VegaLiteLimits { MaxImages = 2 } }, Ct);
        var ex = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { ImageResolver = tight, Limits = new VegaLiteLimits { MaxImages = 1 } }, Ct));

        // Assert: two links are two calls; with a limit of one the second link is not asked for and the error is located at the mark
        Assert.Equal(["a.png", "b.png"], resolver.Calls.Select(c => c.Href));
        Assert.Equal(["a.png"], tight.Calls.Select(c => c.Href));
        Assert.Equal("/mark", ex.Location.Pointer);
        Assert.Contains("MaxImages", ex.Message, StringComparison.Ordinal);
        Assert.All(tight.Handed, handed => Assert.Throws<ObjectDisposedException>(() => handed[0, 0]));
        Assert.All(resolver.Handed, handed => Assert.Throws<ObjectDisposedException>(() => handed[0, 0]));
    }

    /// <summary>
    ///     Test that the exception of the resolver propagates unchanged and a failed render releases the pictures.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkResolverThrows_Propagates()
    {
        // Arrange
        var failure = new IOException("disk on fire");
        var resolver = new FakeResolver { Make = (href, w, h) => href == "blue.png" ? throw failure : Solid(w, h, Red) };

        // Act
        var ex = Assert.Throws<IOException>(() => VegaLiteDocument.Parse(TwoIcons).Render(new VegaLiteRenderOptions { ImageResolver = resolver }, Ct));

        // Assert
        Assert.Same(failure, ex);
        Assert.Throws<ObjectDisposedException>(() => resolver.Handed[0][0, 0]);
    }

    /// <summary>
    ///     Test that a link is handed to the resolver exactly as the chart gives it, a remote one included: the library decides nothing about it.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkRemoteLink_IsPassedUntouched()
    {
        // Arrange
        const string chart = """
            {
              "width": 100, "height": 100, "padding": 0, "data": { "values": [ { "x": 1 } ] },
              "mark": { "type": "image", "width": 10, "height": 10 },
              "encoding": { "x": { "field": "x", "type": "quantitative", "axis": null }, "url": { "value": "https://example.com/a.png?x=1#page=2" } }
            }
            """;
        var resolver = new FakeResolver { Make = (_, _, _) => null };
        var warnings = new List<VegaLiteDiagnostic>();

        // Act
        using var surface = VegaLiteDocument.Parse(chart).Render(new VegaLiteRenderOptions { ImageResolver = resolver, WarningHandler = warnings.Add }, Ct);

        // Assert: the link reached the resolver unchanged, and the answer was nothing
        Assert.Equal("https://example.com/a.png?x=1#page=2", Assert.Single(resolver.Calls).Href);
        Assert.Single(warnings);
    }

    /// <summary>
    ///     Test that a picture is drawn over the layer below it and under the layer above it, as the layers are drawn in order, and that the layers of other marks are unaffected.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkInLayer_IsDrawnInLayerOrder()
    {
        // Arrange: a red rectangle over the whole chart in the first layer, a picture in the second and a small red square in the third
        const string chart = """
            {
              "width": 100, "height": 100, "padding": 0, "data": { "values": [ { "x": 5, "y": 5 } ] },
              "layer": [
                { "mark": "rect", "encoding": { "color": { "value": "#ff0000" } } },
                {
                  "mark": { "type": "image", "width": 40, "height": 40 },
                  "encoding": {
                    "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
                    "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
                    "url": { "value": "blue.png" }
                  }
                },
                {
                  "mark": { "type": "square", "size": 400, "opacity": 1 },
                  "encoding": {
                    "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
                    "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 10] }, "axis": null },
                    "color": { "value": "#ff0000" }
                  }
                }
              ]
            }
            """;
        var resolver = new FakeResolver();

        // Act
        using var surface = VegaLiteDocument.Parse(chart).Render(new VegaLiteRenderOptions { ImageResolver = resolver, ThrowOnUnsupportedFeature = true }, Ct);

        // Assert: the picture covers the first layer from 30 to 70 and the last layer covers the picture in the middle (from 40 to 60)
        Assert.Equal(("blue.png", 40, 40), Assert.Single(resolver.Calls));
        Assert.Equal(Red, surface[10, 10]);
        Assert.Equal(Blue, surface[35, 35]);
        Assert.Equal(Red, surface[50, 50]);
    }

    /// <summary>
    ///     Test that two renders give the same bytes, with and without the resolver being asked in between.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMark_IsDeterministic()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TwoIcons);

        // Act
        using var first = document.Render(new VegaLiteRenderOptions { ImageResolver = new FakeResolver() }, Ct);
        using var second = document.Render(new VegaLiteRenderOptions { ImageResolver = new FakeResolver() }, Ct);

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
    }

    /// <summary>
    ///     Validates defect B4 for image marks without a width and height: every picture is drawn at its natural size (256 by 256 here), which is not the author's to bound, so the pixels of the picture boxes are
    ///     charged to <see cref="VegaLiteLimits.MaxPaintedPixels"/> before each picture is drawn, and the failure names the limit and is located at the mark. The same chart renders under a limit it fits, and
    ///     a picture that is not smoothed is resampled once for the whole chart (the resolver is asked once for each render).
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ImageMarkWithoutSize_IsBoundedByThePaintLimitAndResamplesOnce()
    {
        // Arrange: 40 rows of a picture without a size and without smoothing on a 100 by 100 chart, each box covering the chart
        const string chart = """
            {
              "width": 100, "height": 100, "padding": 0,
              "data": { "sequence": { "start": 0, "stop": 40 } },
              "mark": { "type": "image", "smooth": false },
              "encoding": {
                "x": { "field": "data", "type": "quantitative", "axis": null },
                "y": { "value": 0 },
                "url": { "value": "icon.png" }
              }
            }
            """;
        var document = VegaLiteDocument.Parse(chart);
        var resolver = new FakeResolver();

        // Act: 40 pictures of at most 10,000 visible pixels each (and the 65,536 pixels of the one resampled copy) are far beyond a limit of 30,000, which the chart cannot fit
        var error = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { ImageResolver = resolver, Limits = new VegaLiteLimits { MaxPaintedPixels = 30_000 } }, Ct));
        using var fits = document.Render(new VegaLiteRenderOptions { ImageResolver = resolver, Limits = new VegaLiteLimits { MaxPaintedPixels = 1_000_000 } }, Ct);

        // Assert
        Assert.Contains("VegaLiteLimits.MaxPaintedPixels", error.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", error.Location.Pointer);
        Assert.Equal(2, resolver.Calls.Count);
        Assert.Equal(Red, fits[1, 1]);
    }
}
