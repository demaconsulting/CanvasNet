// cspell:ignore href scad pdf
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     System-level tests of the viewer through its public API: an SVG document that embeds every format, and the
///     custom image sources an application plugs in (a new file type, an override of a built-in type, a URI scheme and a
///     live source). The first test is also the shared platform and runtime proof of the package.
/// </summary>
public class CanvasNetViewerSystemIntegrationTests
{
    /// <summary>
    ///     Proves the platform and runtime requirements: the package loads on this platform and runtime and lists the
    ///     image source of every format it supports, with the extensions it claims.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_ImageSources_ListEveryFormat()
    {
        // Arrange / Act
        var sources = CanvasNetViewer.ImageSources;

        // Assert: one source per format, and the extensions are exactly the union of what they claim
        Assert.Equal(["raster", "svg", "pdf", "pptx", "vsdx", "scad", "vega-lite"], sources.Select(s => s.Name));
        Assert.Equal(
            [".bmp", ".gif", ".j2c", ".j2k", ".jp2", ".jpeg", ".jpg", ".pdf", ".png", ".pptx", ".scad", ".svg", ".tif", ".tiff", ".vl", ".vl.json", ".vsdx"],
            CanvasNetViewer.Extensions);
        Assert.Equal(sources.SelectMany(s => s.Extensions).Order(StringComparer.Ordinal), CanvasNetViewer.Extensions);
        Assert.All(sources, s => Assert.Empty(s.Schemes));
    }

    /// <summary>
    ///     Proves that an SVG document embeds a raster, a model, a chart, another SVG file (which finds its own picture
    ///     in its own folder) and a PDF page, all through the resolver of the viewer, with nothing reported.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_SvgEmbeddingEveryFormat_DrawsAllAndReportsEmbeds()
    {
        // Arrange: one file of each kind next to the document, and an SVG file in a sub folder that embeds its own raster
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("part.scad", ViewerFixtures.Cube);
        folder.WriteText("chart.vl.json", ViewerFixtures.BarChart);
        folder.WriteBytes("sub/img.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText(
            "sub/inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><image href='img.png' x='0' y='0' width='10' height='10'/></svg>");
        folder.WriteBytes("page.pdf", ViewerFixtures.Pdf());
        var svg = ViewerFixtures.Svg(
            ViewerFixtures.Cell("logo.png", 0)
            + ViewerFixtures.Cell("part.scad", 1)
            + ViewerFixtures.Cell("chart.vl.json", 2)
            + ViewerFixtures.Cell("sub/inner.svg", 3)
            + ViewerFixtures.Cell("page.pdf", 4));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: every cell shows its picture, and the resolver lists every embedded image and reports nothing
        Assert.Equal(ViewerFixtures.Green, surface[50, 50]);
        Assert.NotEqual(ViewerFixtures.Gray, surface[150, 50]);
        Assert.Equal(ViewerFixtures.Gray, surface[102, 2]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[202, 2]);
        Assert.Equal(ViewerFixtures.Green, surface[350, 50]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[425, 33]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[475, 66]);
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal(
            ["pdf", "png", "scad", "svg", "vega-lite"],
            resolver.Embedded.Select(e => e.Format).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(5, resolver.Embedded.Count(e => e.Depth == 0));
        Assert.Equal("img.png", Assert.Single(resolver.Embedded, e => e.Depth == 1).Href);
    }

    /// <summary>
    ///     Proves that an SVG document may link files with a query and a fragment (<c>logo.png?v=2#top</c>): they are not
    ///     part of the file name, the picture is drawn, and the embedded image reports the link as written.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_SvgEmbeddingWithQueryAndFragment_DrawsTheFile()
    {
        // Arrange: a raster and a model, linked with a cache-busting query and a fragment
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("part.scad", ViewerFixtures.Cube);
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("logo.png?v=2#top", 0) + ViewerFixtures.Cell("part.scad#view", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: both are drawn and nothing is reported
        Assert.Equal(ViewerFixtures.Green, surface[50, 50]);
        Assert.NotEqual(ViewerFixtures.Gray, surface[150, 50]);
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal(["logo.png?v=2#top", "part.scad#view"], resolver.Embedded.Select(e => e.Href));
    }

    /// <summary>
    ///     Proves that a small file which declares a huge picture, placed in a tiny rectangle of an SVG document, is
    ///     refused from its header: the render succeeds, the rest of the document is drawn, and the link is a
    ///     <c>svg-image-too-large</c> warning (and not an exception or a decode of 256 MiB).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_SvgEmbeddingHugeRasterInTinyPlacement_IsRefusedWithWarning()
    {
        // Arrange: a header-only 8192 by 8192 PNG in a 5 by 5 rectangle, next to a normal picture
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("huge.png", ViewerFixtures.PngDeclaring(Surface.MaxDimension, Surface.MaxDimension));
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var svg = ViewerFixtures.Svg(
            "<image href='huge.png' x='0' y='0' width='5' height='5'/>" + ViewerFixtures.Cell("logo.png", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: the normal picture is drawn, the huge one is not, and the warning has the stable code
        Assert.Equal(ViewerFixtures.Green, surface[150, 50]);
        Assert.Equal(ViewerFixtures.Gray, surface[2, 2]);
        var note = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.TooLargeCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Warning, note.Severity);
        Assert.Equal("logo.png", Assert.Single(resolver.Embedded).Href);
    }

    /// <summary>
    ///     Proves that an application can plug in a source for a file type the library does not know and embed it with
    ///     <c>&lt;image href&gt;</c>, and that without the source the same link is only a warning.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_CustomSourceForNewExtension_IsEmbedded()
    {
        // Arrange: a file of a made-up type, and a source for it that is not part of the library
        using var folder = new ViewerTestFolder();
        folder.WriteText("thing.colors", "blue");
        var mine = ViewerFixtures.ColorText("my-colors", ".colors");
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("thing.colors", 0));
        var withSource = CanvasNetViewer.CreateImageResolver(folder.Path, [mine]);
        var withoutSource = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var drawn = ViewerFixtures.RenderSvg(svg, withSource);
        using var skipped = ViewerFixtures.RenderSvg(svg, withoutSource);

        // Assert: drawn with the custom source, which got the file; only a warning without it
        Assert.Equal(new Rgba32(0, 0, 255, 255), drawn[50, 50]);
        Assert.Equal(new EmbeddedImage("thing.colors", "color-text", 100, 100), withSource.Embedded.Single());
        var request = Assert.Single(mine.Requests);
        Assert.Equal("thing.colors", request.FileName);
        Assert.Equal(folder.Path, request.Directory);
        Assert.Equal((100, 100), (request.PixelWidth, request.PixelHeight));
        Assert.Equal(ViewerFixtures.Gray, skipped[50, 50]);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(withoutSource.Diagnostics).Code);
    }

    /// <summary>
    ///     Proves that a custom source placed after the built-in ones overrides a built-in extension: the same
    ///     <c>.png</c> file is drawn by the custom source instead of the raster source.
    /// </summary>
    [Theory]
    [InlineData(".png")]
    [InlineData(".scad")]
    public void CanvasNetViewer_SystemIntegration_CustomSourceOverridingBuiltInExtension_Wins(string extension)
    {
        // Arrange: a real file of the built-in type whose text the custom source reads as a color
        using var folder = new ViewerTestFolder();
        folder.WriteText("thing" + extension, "magenta");
        var override1 = ViewerFixtures.ColorText("override", extension);
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("thing" + extension, 0));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path, [override1]);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: the custom source drew the picture (the text is not a valid PNG or model, so the built-in one could not)
        Assert.Equal(new Rgba32(255, 0, 255, 255), surface[50, 50]);
        Assert.Equal("color-text", resolver.Embedded.Single().Format);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Proves that a source can claim a URI scheme and is then handed the link as written, so
    ///     <c>&lt;image href="test-scheme:frame"&gt;</c> is drawn without any file.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_CustomSchemeSource_IsEmbeddedFromRawHref()
    {
        // Arrange: a folder with nothing in it, and a source that claims test-scheme
        using var folder = new ViewerTestFolder();
        var live = new LiveFrameSource();
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("test-scheme:frame", 0));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path, [live]);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: the frame is drawn, and the source got the raw link and no file
        Assert.Equal(LiveFrameSource.ColorOf(1), surface[50, 50]);
        var request = Assert.Single(live.Requests);
        Assert.Equal("test-scheme:frame", request.Href);
        Assert.False(request.HasContent);
        Assert.Null(request.FileName);
        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Proves that nothing is cached: a live source gives a different frame on every render, whether a new resolver
    ///     is created for each render or one resolver serves two renders.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_LiveFrameSource_GivesFreshFrameOnEveryRender()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var live = new LiveFrameSource();
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("test-scheme:frame", 0));

        // Act: two renders with a resolver each, then two renders with one shared resolver
        using var first = ViewerFixtures.RenderSvg(svg, CanvasNetViewer.CreateImageResolver(folder.Path, [live]));
        using var second = ViewerFixtures.RenderSvg(svg, CanvasNetViewer.CreateImageResolver(folder.Path, [live]));
        var shared = CanvasNetViewer.CreateImageResolver(folder.Path, [live]);
        using var third = ViewerFixtures.RenderSvg(svg, shared);
        using var fourth = ViewerFixtures.RenderSvg(svg, shared);

        // Assert: four renders, four different frames
        Assert.Equal(LiveFrameSource.ColorOf(1), first[50, 50]);
        Assert.Equal(LiveFrameSource.ColorOf(2), second[50, 50]);
        Assert.Equal(LiveFrameSource.ColorOf(3), third[50, 50]);
        Assert.Equal(LiveFrameSource.ColorOf(4), fourth[50, 50]);
        Assert.Equal(4, live.Requests.Count);
    }

    /// <summary>
    ///     Proves that the render-any-file API honors custom sources: a file of a made-up type is rendered by the custom
    ///     source, and an SVG file rendered by path embeds a scheme-claimed live frame.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_RenderWithExtraSources_UsesCustomSources()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        folder.WriteText("thing.colors", "blue");
        folder.WriteText("diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("test-scheme:frame", 0)));
        var colors = ViewerFixtures.ColorText("my-colors", ".colors");
        var live = new LiveFrameSource();
        var options = new ViewerRenderOptions { ExtraSources = [colors, live] };

        // Act
        using var thing = CanvasNetViewer.Render(folder.Combine("thing.colors"), options);
        using var diagram = CanvasNetViewer.Render(folder.Combine("diagram.svg"), options);

        // Assert
        Assert.Equal("color-text", thing.Format);
        Assert.Equal(new Rgba32(0, 0, 255, 255), thing.Surface[2, 2]);
        Assert.Equal("svg", diagram.Format);
        Assert.Equal(LiveFrameSource.ColorOf(1), diagram.Surface[50, 50]);
        Assert.Equal("live-frame", Assert.Single(diagram.Embedded).Format);
    }
}
