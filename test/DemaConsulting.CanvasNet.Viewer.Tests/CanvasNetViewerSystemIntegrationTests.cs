using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.HelpCards.Presentation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
    [Trait("Category", "Platform")]
    public void CanvasNetViewer_SystemIntegration_ImageSources_ListEveryFormat()
    {
        // Arrange / Act
        var sources = CanvasNetViewer.ImageSources;

        // Assert: one source per format, and the extensions are exactly the union of what they claim
        Assert.Equal(["raster", "svg", "pdf", "pptx", "vsdx", "scad", "vega-lite", "mermaid", "plantuml", "mathml"], sources.Select(s => s.Name));
        Assert.Equal(
            [".bmp", ".gif", ".j2c", ".j2k", ".jp2", ".jpeg", ".jpg", ".mermaid", ".mmd", ".mml", ".pdf", ".plantuml", ".png", ".pptx", ".pu", ".puml", ".scad", ".svg", ".tif", ".tiff", ".vl", ".vl.json", ".vsdx"],
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
    ///     Proves that a Mermaid diagram is drawn by the viewer directly (over white by default, with the fill of the
    ///     default theme) and as a picture of an SVG document, where the <c>theme</c> fragment of the link chooses the theme and a
    ///     name that is not a theme refuses the link with <c>image-fragment-invalid</c>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_MermaidDiagram_RendersDirectlyAndEmbeddedWithTheme()
    {
        // Arrange: a flowchart, and an SVG document that shows it twice in the dark theme and once with a theme that does not exist
        using var folder = new ViewerTestFolder();
        folder.WriteText("flow.mmd", "flowchart LR\n    a[Start] --> b[Stop]\n");
        var defaultFill = new Rgba32(0xEC, 0xEC, 0xFF, 255);
        var darkFill = new Rgba32(0x1F, 0x20, 0x20, 255);
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("flow.mmd#theme=dark", 0) + ViewerFixtures.Cell("flow.mmd#theme=midnight", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var direct = CanvasNetViewer.Render(Path.Combine(folder.Path, "flow.mmd"));
        using var embedded = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: the direct render is a diagram over white with the default fill and no note
        Assert.Equal("mermaid", direct.Format);
        Assert.Equal(new Rgba32(255, 255, 255, 255), direct.Surface[0, 0]);
        Assert.Contains(Enumerable.Range(0, direct.Surface.Width), x => direct.Surface[x, direct.Surface.Height / 2] == defaultFill);
        Assert.Empty(direct.Diagnostics);

        // Assert: the first cell is drawn in the dark theme, and the second link is refused with its code
        Assert.Contains(Enumerable.Range(0, 100), x => embedded[x, 50] == darkFill);
        Assert.Equal("flow.mmd#theme=dark", Assert.Single(resolver.Embedded).Href);
        var refused = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, refused.Code);
        Assert.Contains("theme", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a PlantUML diagram is drawn by the viewer directly (over white by default, with the participant fill of
    ///     PlantUML's default look), as the picture of an SVG <c>image</c>, and as the picture of a Mermaid node (listed
    ///     once among the embedded images), with the <c>theme</c> fragment of a link reported as a parameter PlantUML
    ///     does not take.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_PlantUmlDiagram_RendersDirectlyAndEmbeddedInSvgAndMermaid()
    {
        // Arrange: a sequence diagram, an SVG document that shows it, and a Mermaid flowchart whose node shows it
        using var folder = new ViewerTestFolder();
        folder.WriteText("seq.puml", "@startuml\nAlice -> Bob: hello\n@enduml\n");
        folder.WriteText("flow.mmd", "flowchart LR\n    p@{ img: \"seq.puml\", w: 120, h: 120 }\n");
        var participantFill = new Rgba32(0xE2, 0xE2, 0xF0, 255);
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("seq.puml", 0) + ViewerFixtures.Cell("seq.puml#theme=dark", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var direct = CanvasNetViewer.Render(Path.Combine(folder.Path, "seq.puml"));
        using var embedded = ViewerFixtures.RenderSvg(svg, resolver);
        using var inMermaid = CanvasNetViewer.Render(Path.Combine(folder.Path, "flow.mmd"));

        // Assert: the direct render is a diagram over white with the participant fill and no note
        Assert.Equal("plantuml", direct.Format);
        Assert.Equal(new Rgba32(255, 255, 255, 255), direct.Surface[0, 0]);
        Assert.Contains(Enumerable.Range(0, direct.Surface.Height), y => Enumerable.Range(0, direct.Surface.Width).Any(x => direct.Surface[x, y] == participantFill));
        Assert.Empty(direct.Diagnostics);

        // Assert: both cells of the SVG document show the diagram, and the theme of the second link is reported as unknown
        Assert.Contains(Enumerable.Range(0, 100), y => Enumerable.Range(0, 100).Any(x => embedded[x, y] == participantFill));
        Assert.Contains(Enumerable.Range(0, 100), y => Enumerable.Range(100, 100).Any(x => embedded[x, y] == participantFill));
        Assert.Equal(["plantuml"], resolver.Embedded.Select(e => e.Format).Distinct());
        Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, Assert.Single(resolver.Diagnostics).Code);

        // Assert: the Mermaid node shows the diagram as its picture, which is listed once (the measuring load is not)
        Assert.Equal("mermaid", inMermaid.Format);
        Assert.Contains(
            Enumerable.Range(0, inMermaid.Surface.Height),
            y => Enumerable.Range(0, inMermaid.Surface.Width).Any(x => inMermaid.Surface[x, y] == participantFill));
        Assert.Empty(inMermaid.Diagnostics);
        Assert.Equal("seq.puml", Assert.Single(inMermaid.Embedded).Href);
    }

    /// <summary>
    ///     Proves that a MathML equation is drawn by the viewer directly (over white by default, in black ink), as the
    ///     picture of an SVG <c>image</c> (transparent, so the gray of the document shows around it), of a Mermaid node and
    ///     of a Vega-Lite <c>image</c> mark, that the <c>display</c> fragment of an input chooses the display mode (the
    ///     inline form of the quadratic formula is smaller), and that a display mode that does not exist refuses the link
    ///     with <c>image-fragment-invalid</c>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_MathMlEquation_RendersDirectlyAndEmbeddedInSvgMermaidAndVegaLite()
    {
        // Arrange: the quadratic formula, an SVG document that shows it twice (once with a display mode that does not
        // exist), a Mermaid flowchart whose node shows it, and a Vega-Lite chart whose image mark shows it
        using var folder = new ViewerTestFolder();
        var equation = folder.Combine("eq.mml");
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")), equation);
        folder.WriteText("flow.mmd", "flowchart LR\n    q@{ img: \"eq.mml\", w: 140, h: 50 }\n");
        var chart = folder.WriteText(
            "chart.vl.json",
            "{ \"width\": 200, \"height\": 100, \"padding\": 0, \"data\": { \"values\": [ { \"x\": 5 } ] }, "
            + "\"mark\": { \"type\": \"image\", \"width\": 140, \"height\": 50 }, \"encoding\": { \"x\": { \"field\": \"x\", "
            + "\"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10] }, \"axis\": null }, \"y\": { \"value\": 50 }, "
            + "\"url\": { \"value\": \"eq.mml\" } } }");
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("eq.mml", 0) + ViewerFixtures.Cell("eq.mml#display=sideways", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);
        static bool IsInk(Rgba32 pixel) => pixel.A == 255 && pixel.R < 80 && pixel.G < 80 && pixel.B < 80;

        // Act
        using var direct = CanvasNetViewer.Render(equation);
        var block = SourceLoader.Probe(InputReference.Parse(equation));
        var inline = SourceLoader.Probe(InputReference.Parse(equation + "#display=inline"));
        using var embedded = ViewerFixtures.RenderSvg(svg, resolver);
        using var inMermaid = CanvasNetViewer.Render(Path.Combine(folder.Path, "flow.mmd"));
        using var inChart = CanvasNetViewer.Render(chart);

        // Assert: the direct render is the equation in black over white at its natural size, with no note
        Assert.Equal("mathml", direct.Format);
        Assert.Equal(new Rgba32(255, 255, 255, 255), direct.Surface[0, 0]);
        Assert.Equal((block.Width, block.Height), (direct.Surface.Width, direct.Surface.Height));
        Assert.Contains(Enumerable.Range(0, direct.Surface.Height), y => Enumerable.Range(0, direct.Surface.Width).Any(x => IsInk(direct.Surface[x, y])));
        Assert.Empty(direct.Diagnostics);

        // Assert: the display fragment of an input lays the equation out inline, which is smaller
        Assert.Equal(("block", "inline"), (block.Equation?.Display, inline.Equation?.Display));
        Assert.True(inline.Height < block.Height);

        // Assert: the first cell shows the equation over the gray of the document, and the second link is refused
        Assert.Equal(ViewerFixtures.Gray, embedded[2, 2]);
        Assert.Contains(Enumerable.Range(0, 100), y => Enumerable.Range(0, 100).Any(x => IsInk(embedded[x, y])));
        Assert.Equal("eq.mml", Assert.Single(resolver.Embedded).Href);
        var refused = Assert.Single(resolver.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, refused.Code);
        Assert.Contains("display", refused.Message, StringComparison.Ordinal);

        // Assert: the Mermaid node and the image mark of the chart show the equation, each listed once
        Assert.Contains(
            Enumerable.Range(0, inMermaid.Surface.Height),
            y => Enumerable.Range(0, inMermaid.Surface.Width).Any(x => IsInk(inMermaid.Surface[x, y])));
        Assert.Equal("eq.mml", Assert.Single(inMermaid.Embedded).Href);
        Assert.Empty(inMermaid.Diagnostics);
        Assert.Equal("eq.mml", Assert.Single(inChart.Embedded).Href);
        Assert.Equal("mathml", Assert.Single(inChart.Embedded).Format);
        Assert.Empty(inChart.Diagnostics);
    }

    /// <summary>
    ///     Proves that the <c>display</c> fragment of a link chooses the display mode of a linked equation in an SVG
    ///     <c>image</c>, a Mermaid node and a Vega-Lite <c>image</c> mark: <c>eq.mml#display=inline</c> draws exactly the
    ///     picture of a copy of the equation that says <c>display="inline"</c> itself (so the inline layout is used), which
    ///     differs from the block picture, and <c>eq.mml#display=BLOCK</c> (in any case) draws exactly the block picture.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_MathMlDisplayFragmentOfALink_ChoosesTheLayout()
    {
        // Arrange: the quadratic formula (display="block") and a copy that says display="inline", an SVG document that
        // links the equation plainly, as BLOCK and as inline, and the copy; and a flowchart and a chart for each link
        using var folder = new ViewerTestFolder();
        var block = File.ReadAllText(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")));
        folder.WriteText("eq.mml", block);
        folder.WriteText("eq-inline.mml", block.Replace("display=\"block\"", "display=\"inline\"", StringComparison.Ordinal));
        var links = new[] { "eq.mml", "eq.mml#display=BLOCK", "eq.mml#display=inline", "eq-inline.mml" };
        var svg = folder.WriteText("figure.svg", ViewerFixtures.Svg(string.Concat(links.Select(ViewerFixtures.Cell))));
        var flows = links
            .Select((href, i) => folder.WriteText($"flow{i}.mmd", $"flowchart LR\n    q@{{ img: \"{href}\", w: 140, h: 50 }}\n"))
            .ToList();
        var charts = links
            .Select((href, i) => folder.WriteText(
                $"chart{i}.vl.json",
                "{ \"width\": 200, \"height\": 100, \"padding\": 0, \"data\": { \"values\": [ { \"x\": 5 } ] }, "
                + "\"mark\": { \"type\": \"image\", \"width\": 140, \"height\": 50 }, \"encoding\": { \"x\": { \"field\": \"x\", "
                + "\"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10] }, \"axis\": null }, \"y\": { \"value\": 50 }, "
                + $"\"url\": {{ \"value\": \"{href}\" }} }} }}"))
            .ToList();

        // Act
        using var figure = CanvasNetViewer.Render(svg);
        var inFlows = flows.Select(path => CanvasNetViewer.Render(path)).ToList();
        var inCharts = charts.Select(path => CanvasNetViewer.Render(path)).ToList();

        try
        {
            // Assert: in the SVG document, BLOCK draws the block picture and inline draws the picture of the inline copy,
            // which differs from the block picture; every link is listed and nothing is reported
            Assert.True(SameCell(figure.Surface, 0, 1), "eq.mml#display=BLOCK differs from the block picture in the SVG image.");
            Assert.True(SameCell(figure.Surface, 2, 3), "eq.mml#display=inline differs from the inline picture in the SVG image.");
            Assert.False(SameCell(figure.Surface, 0, 2), "eq.mml#display=inline draws the block picture in the SVG image.");
            Assert.Equal(links, figure.Embedded.Select(e => e.Href));
            Assert.All(figure.Embedded, e => Assert.Equal("mathml", e.Format));
            Assert.Empty(figure.Diagnostics);

            // Assert: the Mermaid node and the image mark of the chart follow the fragment the same way
            foreach (var (pictures, where) in new[] { (inFlows, "Mermaid node"), (inCharts, "Vega-Lite image mark") })
            {
                Assert.True(SamePixels(pictures[0].Surface, pictures[1].Surface), $"eq.mml#display=BLOCK differs from the block picture in the {where}.");
                Assert.True(SamePixels(pictures[2].Surface, pictures[3].Surface), $"eq.mml#display=inline differs from the inline picture in the {where}.");
                Assert.False(SamePixels(pictures[0].Surface, pictures[2].Surface), $"eq.mml#display=inline draws the block picture in the {where}.");
                Assert.Equal(links, pictures.Select(picture => Assert.Single(picture.Embedded).Href));
                Assert.All(pictures, picture => Assert.Empty(picture.Diagnostics));
            }
        }
        finally
        {
            foreach (var picture in inFlows.Concat(inCharts))
            {
                picture.Dispose();
            }
        }
    }

    /// <summary>
    ///     Determines whether two cells of a 500 by 100 document drawn by <see cref="ViewerFixtures.Svg"/> have the same pixels.
    /// </summary>
    /// <param name="surface">The picture of the document.</param>
    /// <param name="first">The first cell, 0 to 4.</param>
    /// <param name="second">The second cell, 0 to 4.</param>
    /// <returns><see langword="true"/> when every pixel of the first cell equals the one of the second.</returns>
    private static bool SameCell(Surface surface, int first, int second)
    {
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (surface[first * 100 + x, y] != surface[second * 100 + x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Determines whether two pictures have the same size and the same pixels.
    /// </summary>
    /// <param name="first">The first picture.</param>
    /// <param name="second">The second picture.</param>
    /// <returns><see langword="true"/> when the sizes and every pixel are equal.</returns>
    private static bool SamePixels(Surface first, Surface second)
    {
        if ((first.Width, first.Height) != (second.Width, second.Height))
        {
            return false;
        }

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first[x, y] != second[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Proves that a model that declares anchors is embedded exactly like the same model without them: the cell shows
    ///     the same picture and nothing is reported for the valid declaration (the viewer does not use anchors).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_SvgEmbeddingModelWithAnchors_DrawsTheSamePicture()
    {
        // Arrange: the cube twice, once with a valid anchor declaration, each in its own file
        using var folder = new ViewerTestFolder();
        folder.WriteText("part.scad", ViewerFixtures.Cube);
        folder.WriteText("anchored.scad", ViewerFixtures.Cube + "\necho(\"anchor\", \"top\", [0, 0, 5]);\n");
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var plain = ViewerFixtures.RenderSvg(ViewerFixtures.Svg(ViewerFixtures.Cell("part.scad", 0)), resolver);
        using var anchored = ViewerFixtures.RenderSvg(ViewerFixtures.Svg(ViewerFixtures.Cell("anchored.scad", 0)), resolver);

        // Assert: the same pixels in the cell, and the declaration is not a diagnostic
        for (var y = 0; y < plain.Height; y++)
        {
            Assert.True(plain.GetRowSpanBytes(y).SequenceEqual(anchored.GetRowSpanBytes(y)), $"Row {y} differs.");
        }

        Assert.Empty(resolver.Diagnostics);
    }

    /// <summary>
    ///     Proves that an SVG document may link files with a query and a fragment (<c>logo.png?v=2</c>,
    ///     <c>part.scad?v=2#view=front</c>): they are not part of the file name, the pictures are drawn (the fragment
    ///     chooses the viewpoint of the model), and the embedded images report the links as written.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_SvgEmbeddingWithQueryAndFragment_DrawsTheFile()
    {
        // Arrange: a raster linked with a cache-busting query, and a model with a query and a fragment
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("part.scad", ViewerFixtures.Cube);
        var svg = ViewerFixtures.Svg(ViewerFixtures.Cell("logo.png?v=2", 0) + ViewerFixtures.Cell("part.scad?v=2#view=front", 1));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var surface = ViewerFixtures.RenderSvg(svg, resolver);

        // Assert: both are drawn and nothing is reported
        Assert.Equal(ViewerFixtures.Green, surface[50, 50]);
        Assert.NotEqual(ViewerFixtures.Gray, surface[150, 50]);
        Assert.Empty(resolver.Diagnostics);
        Assert.Equal(["logo.png?v=2", "part.scad?v=2#view=front"], resolver.Embedded.Select(e => e.Href));
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

    /// <summary>
    ///     Proves that a library user can do what the canvasnet commands do through the public types alone: report the
    ///     facts of a file, render it with a transformation, inspect the written picture, compare it with the original,
    ///     and put both on a contact sheet.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_ImageOperations_ProbeRenderInspectCompareMontage()
    {
        // Arrange: the sketch fixture as an SVG file
        using var folder = new ViewerTestFolder();
        var source = FixtureFactory.WriteSource(folder.Path, "svg");
        var write = WriteOptions.Default;

        // Act: probe, render flipped, inspect the output, compare with an unflipped render, and compose a sheet of both
        var info = SourceLoader.Probe(source);
        var flipped = ImageRenderer.Execute(
            info,
            new RenderRequest(folder.Combine("flipped.png")) { Write = write, Operations = [new FlipOperation(FlipAxis.Horizontal)] });
        var plain = ImageRenderer.Execute(
            info, new RenderRequest(folder.Combine("plain.png")) { Write = write });
        var inspected = ImageInspector.Execute(
            SourceLoader.Probe(flipped.Pages[0].Result.Path),
            InspectRequest.ForColors(4));
        var compared = ImageComparer.Execute(
            new CompareRequest(
                new CompareInput("a", SourceLoader.Probe(plain.Pages[0].Result.Path)),
                new CompareInput("b", SourceLoader.Probe(flipped.Pages[0].Result.Path)))
            {
                Write = write
            });
        var sheet = MontageBuilder.Execute(
            [SourceLoader.Probe(plain.Pages[0].Result.Path), SourceLoader.Probe(flipped.Pages[0].Result.Path)],
            new MontageRequest(folder.Combine("sheet.png"))
            {
                Sheet = new SheetOptions { CellWidth = 32, CellHeight = 32, Spacing = 4, Padding = 4 },
                Write = write
            });

        // Assert: every operation produced its result, and the flip is a real difference between the two renders
        Assert.Equal(SourceKind.Svg, info.Format);
        Assert.Equal(plain.Pages[0].Result.Width, flipped.Pages[0].Result.Width);
        Assert.NotEmpty(inspected.Colors!.Colors);
        Assert.False(compared.Metrics.Identical);
        Assert.Equal(2, sheet.Cells.Count);
        Assert.True(File.Exists(folder.Combine("sheet.png")));
    }

    /// <summary>
    ///     Proves that a flattened drawing shows, in an SVG reader that follows no links (the SVG library without a
    ///     resolver), what CanvasNet draws for the original: the nested SVG file with its diagram and the page of a
    ///     document become pictures, and the original shows neither in such a reader.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_Flatten_ComposedSvgBecomesPlainSvg()
    {
        // Arrange: a drawing that links a PDF page and a nested SVG file that links a Mermaid diagram
        using var folder = new ViewerTestFolder();
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), folder.Combine("flow.mmd"));
        File.WriteAllBytes(folder.Combine("doc.pdf"), FixtureFactory.CreatePdf());
        folder.WriteText("inner.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60'><image href='flow.mmd' width='80' height='60'/></svg>");
        var input = folder.WriteText(
            "dashboard.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='200' height='100'>"
            + "<rect width='200' height='100' fill='#ffffff'/><image xlink:href='inner.svg' width='120' height='90'/>"
            + "<image href='doc.pdf#page=1' x='130' width='60' height='90'/></svg>");
        var output = folder.Combine("flat.svg");

        // Act: flatten, then draw the original with CanvasNet and both files with a reader that follows no links
        var result = SvgFlattener.Execute(SourceLoader.Probe(input), new FlattenRequest(output));
        using var canvasNet = SourceLoader.Load(input, new SourceOptions());
        using var flattened = Svg.SvgCodec.LoadWithFontFaces(output, 200, 100, null);
        using var plain = Svg.SvgCodec.LoadWithFontFaces(input, 200, 100, null);

        // Assert: both links were replaced, the flattened copy looks like CanvasNet's drawing, and the original does not
        Assert.Equal(["inner.svg", "doc.pdf#page=1"], result.Replaced.Select(r => r.Href));
        Assert.True(Difference(canvasNet.Surface, flattened) < Difference(canvasNet.Surface, plain) / 4);
    }

    /// <summary>
    ///     Measures how much two pictures of one size differ.
    /// </summary>
    /// <param name="a">The first picture.</param>
    /// <param name="b">The second picture, flattened over white first.</param>
    /// <returns>The mean absolute difference of the color channels, from 0 to 255.</returns>
    private static double Difference(Surface a, Surface b)
    {
        var total = 0L;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                var (p, q) = (a[x, y], Over(b[x, y]));
                total += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
            }
        }

        return total / (3.0 * a.Width * a.Height);
    }

    /// <summary>
    ///     Puts a pixel over white.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns>The opaque pixel.</returns>
    private static Rgba32 Over(Rgba32 pixel)
    {
        int Mix(byte channel) => ((channel * pixel.A) + (255 * (255 - pixel.A))) / 255;
        return new Rgba32((byte)Mix(pixel.R), (byte)Mix(pixel.G), (byte)Mix(pixel.B), 255);
    }

    /// <summary>
    ///     Proves that an operation that cannot be done fails with the one exception type of the library and a stable code
    ///     that a program can act on, naming nothing of the command line it came from in the type.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_FailedOperation_IsViewerExceptionWithStableCode()
    {
        // Arrange: a file that is not an image, and a file that does not exist
        using var folder = new ViewerTestFolder();
        folder.WriteText("note.txt", "not an image");

        // Act
        var unsupported = Assert.Throws<ViewerException>(() => SourceLoader.Probe(folder.Combine("note.txt")));
        var missing = Assert.Throws<ViewerException>(() => SourceLoader.Probe(folder.Combine("missing.png")));

        // Assert: the documented codes, a message, and a type that is an InvalidOperationException
        Assert.Equal(ErrorCodes.UnsupportedFormat, unsupported.Code);
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.All([unsupported, missing], e => Assert.False(string.IsNullOrEmpty(e.Message)));
        Assert.IsAssignableFrom<InvalidOperationException>(unsupported);
    }

    /// <summary>
    ///     Proves the help an agent toolkit reads through the public API: the library catalog filtered to the Mermaid
    ///     cards without the hosting cards resolves the Gantt card, whose Markdown and text read without the command line
    ///     and point only at cards of the view.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_Help_FilterResolveAndRead()
    {
        // Arrange: the Mermaid cards a host shows its agent, without what the host controls
        var view = CanvasNetHelp.Catalog.Filter([HelpTags.Mermaid], [HelpTags.Hosting]);

        // Act: resolve a card by path and by alias, and read it
        var gantt = view.Find("mermaid/gantt");
        var byAlias = view.Find("mmd gantt");
        var linked = new List<string>();
        var markdown = gantt == null ? string.Empty : view.FormatBody(gantt, new HelpRenderOptions
        {
            LinkFormatter = link =>
            {
                linked.Add(link.ExampleCardId ?? link.CardId ?? string.Empty);
                return link.CardId ?? link.ExampleName ?? string.Empty;
            }
        });

        // Assert: the card, its text, and only visible cards linked
        Assert.NotNull(gantt);
        Assert.Same(gantt, byAlias);
        Assert.Contains(HelpTags.Mermaid, gantt.Tags);
        Assert.DoesNotContain(HelpTags.Hosting, gantt.Tags);
        Assert.False(string.IsNullOrWhiteSpace(markdown));
        Assert.DoesNotContain("canvasnet ", gantt.Body, StringComparison.Ordinal);
        Assert.StartsWith($"# {gantt.Title}\n", view.Render(gantt));
        Assert.All(linked, id => Assert.NotNull(view.Find(id)));
        Assert.All(view.Cards, card => Assert.Contains(HelpTags.Mermaid, card.Tags));
        Assert.Null(view.Find("mermaid/options"));
    }

    /// <summary>
    ///     Proves that every C# example of the Viewer user guide that follows a <c>&lt;!-- compile --&gt;</c> line is a complete
    ///     program that compiles without an error against the current API (the examples are compiled, never run), that
    ///     every C# block of the guide is classified (compiled, or a signature listing), and that the guide shows the
    ///     namespace of <c>ViewerException</c> next to the namespace of the operations.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_UserGuideExamples_Compile()
    {
        // Arrange: the guide, and its C# blocks
        var guide = Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-viewer.md");
        var blocks = ExtractCSharpBlocks(File.ReadAllLines(guide));
        var examples = blocks.Where(b => b.Marker == CompileMarker).ToList();

        // Act: compile every marked example on its own
        var failures = examples
            .Select(e => (Example: e, Errors: CompileErrors(e.Code)))
            .Where(r => r.Errors.Count > 0)
            .Select(r => $"Example at line {r.Example.Line} ({FirstLine(r.Example.Code)}):{Environment.NewLine}  "
                + string.Join($"{Environment.NewLine}  ", r.Errors))
            .ToList();

        // Assert: nothing is unclassified, enough examples exist, none fails, and the namespaces are shown
        Assert.All(
            blocks,
            b => Assert.True(
                b.Marker is CompileMarker or SignatureMarker,
                $"The C# block at line {b.Line} of the guide has no '{CompileMarker}' or '{SignatureMarker}' marker."));
        Assert.True(examples.Count >= 15, $"Only {examples.Count} compile-checked examples were found in the guide.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        var usingViewerException = examples.Where(e => e.Code.Contains("ViewerException", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(usingViewerException);
        Assert.All(usingViewerException, e =>
        {
            Assert.Contains("using DemaConsulting.CanvasNet.Viewer;", e.Code, StringComparison.Ordinal);
            Assert.Contains("using DemaConsulting.CanvasNet.Viewer.Imaging;", e.Code, StringComparison.Ordinal);
        });
    }

    /// <summary>
    ///     Proves that the harness of the guide test can fail: a program that does not compile is reported with its error,
    ///     and a program that does compile is reported with none (so a passing guide test is not an empty check).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_UserGuideExamples_NegativeControl_ReportsErrors()
    {
        // Arrange: a program with an unknown member of the API, a program with an unknown namespace, and a good program
        const string unknownMember =
            "using DemaConsulting.CanvasNet.Viewer.Imaging;\nvar result = ImageComparer.NoSuchMethod(null!);";
        const string unknownNamespace = "using DemaConsulting.CanvasNet.NoSuchNamespace;\nConsole.WriteLine(1);";
        const string good =
            "using DemaConsulting.CanvasNet.Viewer.Imaging;\nvar request = new RenderRequest(\"out.png\");\nConsole.WriteLine(request.Output);";

        // Act
        var memberErrors = CompileErrors(unknownMember);
        var namespaceErrors = CompileErrors(unknownNamespace);
        var goodErrors = CompileErrors(good);

        // Assert
        Assert.Contains(memberErrors, e => e.Contains("NoSuchMethod", StringComparison.Ordinal));
        Assert.Contains(namespaceErrors, e => e.Contains("NoSuchNamespace", StringComparison.Ordinal));
        Assert.Empty(goodErrors);
    }

    /// <summary>The marker that precedes a C# block of the guide that is a complete program and is compiled.</summary>
    private const string CompileMarker = "<!-- compile -->";

    /// <summary>The marker that precedes a C# block of the guide that is only a signature listing.</summary>
    private const string SignatureMarker = "<!-- signature -->";

    /// <summary>The namespaces that the implicit usings of a .NET console project provide to the guide's programs.</summary>
    private const string ImplicitUsings =
        "global using System;\nglobal using System.Collections.Generic;\nglobal using System.IO;\nglobal using System.Linq;\n"
        + "global using System.Net.Http;\nglobal using System.Threading;\nglobal using System.Threading.Tasks;\n";

    /// <summary>
    ///     The references of every compilation: all assemblies of the running test host, which include the platform
    ///     libraries and the viewer package with the packages it uses.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList());

    /// <summary>
    ///     Proves through the public API that the file-access policy of a render reaches every kind of reference: by
    ///     default the picture of an SVG file, of a Mermaid diagram and of a Vega-Lite image mark in the parent folder are
    ///     drawn and the data of a chart and the include of a model there are read; under a policy confined to the folder
    ///     of the document the pictures are access-denied warnings and the data and the include fail the render with the
    ///     denial in the chain of inner exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_FileAccess_DefaultFollowsAndPolicyConfinesEveryReferenceKind()
    {
        // Arrange: a picture, a data file and a library in the parent of the folder of the documents
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("pic.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("data.csv", "a,b\nA,3\nB,5\n");
        folder.WriteText("lib.scad", "module part() { cube(8, center = true); }\n");
        var pictures = new[]
        {
            folder.WriteText("site/a.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("../pic.png", 0))),
            folder.WriteText("site/b.mmd", "flowchart LR\n    p@{ img: \"../pic.png\", w: 60, h: 60, constraint: \"on\" }\n"),
            folder.WriteText(
                "site/c.vl.json",
                "{ \"width\": 100, \"height\": 100, \"padding\": 0, \"data\": { \"values\": [ { \"x\": 5 } ] }, "
                + "\"mark\": { \"type\": \"image\", \"width\": 40, \"height\": 40 }, \"encoding\": { \"x\": { \"field\": \"x\", "
                + "\"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10] }, \"axis\": null }, \"y\": { \"value\": 50 }, "
                + "\"url\": { \"value\": \"../pic.png\" } } }"),
        };
        var files = new[]
        {
            folder.WriteText(
                "site/d.vl.json",
                "{\"data\":{\"url\":\"../data.csv\"},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}"),
            folder.WriteText("site/e.scad", "include <../lib.scad>\npart();\n"),
        };
        var confined = new ViewerRenderOptions { FileAccess = Files.FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act / Assert: the pictures
        foreach (var path in pictures)
        {
            using var drawn = CanvasNetViewer.Render(path);
            using var denied = CanvasNetViewer.Render(path, confined);
            Assert.DoesNotContain(drawn.Diagnostics, d => d.Code is FolderImageResolver.UnresolvedCode or FolderImageResolver.AccessDeniedCode);
            Assert.NotEmpty(drawn.Embedded);
            Assert.Contains(denied.Diagnostics, d => d.Code == FolderImageResolver.AccessDeniedCode);
            Assert.Empty(denied.Embedded);
        }

        // Act / Assert: the files a chart and a model read themselves
        foreach (var path in files)
        {
            using var drawn = CanvasNetViewer.Render(path);
            Assert.True(drawn.Surface.Width > 0);
            var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, confined));
            Assert.Equal(ErrorCodes.FileAccessDenied, ex.Code);
            Exception? current = ex;
            while (current is not null and not Files.FileReferenceException)
            {
                current = current.InnerException;
            }

            Assert.Equal(Files.FileReferenceFailureKind.AccessDenied, Assert.IsType<Files.FileReferenceException>(current).Kind);
        }
    }

    /// <summary>
    ///     Guards the propagation of the policy: every options object of a format library, a resolver or the loader that
    ///     the viewer creates in its source passes a file-access policy on, apart from the listed ones that only check
    ///     options or measure without reading a file, so a new code path cannot fall back to the allow-all default
    ///     silently.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SourceTree_EveryOpenOptionsCarriesTheFileAccess()
    {
        // Arrange: the creations that never read a file with the options they build
        var allowed = new[] { "FindOptionProblem(new SourceOptions", "CreateVegaLiteOptions(new SourceOptions()" };
        var source = Path.Combine(FindRepositoryRoot(), "src", "DemaConsulting.CanvasNet.Viewer");
        var pattern = new System.Text.RegularExpressions.Regex(
            @"new (ScadOpenOptions|VegaLiteOpenOptions|FolderImageResolverOptions|SourceOptions)\b[^;]*",
            System.Text.RegularExpressions.RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        // Act
        var missing = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadAllLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(line => (file, line)))
            .Where(entry => pattern.IsMatch(entry.line))
            .Where(entry => !entry.line.Contains("FileAccess", StringComparison.Ordinal)
                && !allowed.Any(a => entry.line.Contains(a, StringComparison.Ordinal))
                && !IsMultiLineInitializerWithFileAccess(entry.file, entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}: {entry.line.Trim()}")
            .ToList();

        // Assert
        Assert.Empty(missing);
    }

    /// <summary>
    ///     Determines whether a line opens a multi-line object initializer that sets the file-access policy.
    /// </summary>
    /// <param name="file">The source file.</param>
    /// <param name="line">The line that creates the options.</param>
    /// <returns><see langword="true"/> when the initializer that the line opens sets <c>FileAccess</c>.</returns>
    private static bool IsMultiLineInitializerWithFileAccess(string file, string line)
    {
        var text = File.ReadAllText(file);
        var start = text.IndexOf(line, StringComparison.Ordinal);
        var end = text.IndexOf(';', start);
        return start >= 0 && end > start && text[start..end].Contains("FileAccess", StringComparison.Ordinal);
    }

    /// <summary>A C# block of the guide: the marker that precedes it, its first line number, and its text.</summary>
    /// <param name="Marker">The HTML comment on the line before the block, or an empty string.</param>
    /// <param name="Line">The 1-based line number of the opening fence.</param>
    /// <param name="Code">The text between the fences.</param>
    private sealed record GuideBlock(string Marker, int Line, string Code);

    /// <summary>Finds the folder that contains the solution file by walking up from the test output folder.</summary>
    /// <returns>The repository root.</returns>
    private static string FindRepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CanvasNet.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            $"The repository root (the folder with CanvasNet.slnx) was not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>Extracts the fenced C# blocks of a markdown file with the marker that precedes each.</summary>
    /// <param name="lines">The lines of the file.</param>
    /// <returns>The blocks in file order.</returns>
    private static List<GuideBlock> ExtractCSharpBlocks(string[] lines)
    {
        var blocks = new List<GuideBlock>();
        var next = 0;
        while (next < lines.Length)
        {
            var i = next++;
            if (!lines[i].StartsWith("```csharp", StringComparison.Ordinal))
            {
                continue;
            }

            var end = Array.FindIndex(lines, i + 1, l => l.StartsWith("```", StringComparison.Ordinal));
            Assert.True(end > i, $"The C# block at line {i + 1} of the guide is not closed.");
            var before = i - 1;
            while (before >= 0 && string.IsNullOrWhiteSpace(lines[before]))
            {
                before--;
            }

            var marker = before >= 0 && lines[before].StartsWith("<!-- ", StringComparison.Ordinal) ? lines[before].Trim() : string.Empty;
            blocks.Add(new GuideBlock(marker, i + 1, string.Join('\n', lines[(i + 1)..end])));
            next = end + 1;
        }

        return blocks;
    }

    /// <summary>Compiles one program (top-level statements, then types) and returns its errors, not its warnings.</summary>
    /// <param name="code">The C# source of the program.</param>
    /// <returns>One line for each error; empty when the program compiles.</returns>
    private static List<string> CompileErrors(string code)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "GuideExample",
            [CSharpSyntaxTree.ParseText(ImplicitUsings, parse), CSharpSyntaxTree.ParseText(code, parse)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
    }

    /// <summary>Gets the first non-empty line of a program, for the failure message.</summary>
    /// <param name="code">The C# source.</param>
    /// <returns>The first line that is not blank.</returns>
    private static string FirstLine(string code) =>
        code.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? string.Empty;

    /// <summary>
    ///     Proves that a chart rendered through the viewer reports the positions its selectors match, mapped into the
    ///     written image through the pipeline, so that a callout can be pinned to a bar of the written picture.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_SystemIntegration_Positions_FollowThePipelineIntoTheWrittenImage()
    {
        // Arrange: a bar chart, rendered with a pad of 10 pixels on every side
        using var directory = new TempDirectory();
        var path = directory.Combine("bars.vl.json");
        File.WriteAllText(
            path,
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\"," +
            "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");
        var info = SourceLoader.Probe(path);
        var source = new SourceOptions { Positions = DemaConsulting.CanvasNet.Positions.PositionSelection.Parse(["mark:x=B"]) };
        var output = directory.Combine("bars.png");

        // Act
        var page = Assert.Single(ImageRenderer.Execute(info, new RenderRequest(output) { Source = source, Operations = [new PadOperation(new PadSpec(10, 10, 10, 10), null)] }).Pages);

        // Assert: the bar's center in the written file has the bar's color
        var bar = Assert.Single(page.Positions);
        using var written = SurfaceAssert.Load(output);
        Assert.Equal(new Rgba32(0x4C, 0x78, 0xA8, 255), written[(int)bar.Pixel!.Value.X, (int)bar.Pixel.Value.Y]);
    }
}
