using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of the options of <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/> beyond the size: the
///     page, the resolution, the background and the options of a model, the new members of <see cref="ViewerImage"/>
///     (<c>Page</c>, <c>PageCount</c>, <c>Anchors</c>), the validation before the file is read, the error shapes, and the
///     parity with <see cref="SourceLoader"/>.
/// </summary>
public class CanvasNetViewerRenderOptionsTests
{
    /// <summary>
    ///     A stream that fails when it is read, to prove that a render is refused before the content is touched.
    /// </summary>
    private sealed class UnreadableStream : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("The stream must not be read.");
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    ///     Gets the kinds of source that are compared with the loader of the tool.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string> ParityKeys()
    {
        return ["png", "svg", "pdf", "pptx", "vsdx", "scad", "vega-lite"];
    }

    /// <summary>
    ///     Test that <c>Page</c> and <c>Dpi</c> draw the chosen page at the resolution, by path, and that the result
    ///     reports the page and the page count.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PageAndDpi_DrawsThePageAtTheResolution()
    {
        // Arrange: page 2 of the PDF is displayed 96 by 144 points
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Page = 2, Dpi = 192 });
        using var first = CanvasNetViewer.Render(path);

        // Assert: 2 times the 96 dpi size of the page, green, over a transparent background
        Assert.Equal((256, 384), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(new Rgba32(0, 153, 0, 255), image.Surface[128, 192]);
        Assert.Equal((2, 2), (image.Page, image.PageCount));
        Assert.Equal("pdf", image.Format);
        Assert.Equal((192, 128), (first.Surface.Width, first.Surface.Height));
        Assert.Equal((1, 2), (first.Page, first.PageCount));
        Assert.Equal(0, first.Surface[170, 20].A);
        Assert.Empty(image.Anchors);
    }

    /// <summary>
    ///     Test that a page can be chosen when the file is given as a stream, with the same result as by path.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StreamWithPage_DrawsThePage()
    {
        // Arrange
        using var stream = new MemoryStream(FixtureFactory.CreatePptx());

        // Act
        using var image = CanvasNetViewer.Render(stream, "deck.pptx", new ViewerRenderOptions { Page = 2 });

        // Assert: slide 2 is blue over its top half
        Assert.Equal((FixtureFactory.PptxSlideWidth, FixtureFactory.PptxSlideHeight), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(new Rgba32(0, 0, 255, 255), image.Surface[240, 90]);
        Assert.Equal(0, image.Surface[240, 270].A);
        Assert.Equal((2, 2), (image.Page, image.PageCount));
    }

    /// <summary>
    ///     Test that a page the file does not have is a <see cref="ViewerException"/> with the page-out-of-range code, for
    ///     a document and for a single-image file.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PageBeyondTheLast_ThrowsViewerExceptionPageOutOfRange()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var png = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var document = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(pdf, new ViewerRenderOptions { Page = 3 }));
        var image = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(png, new ViewerRenderOptions { Page = 2 }));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, document.Code);
        Assert.Contains("page 3 does not exist: the document has 2 pages", document.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.PageOutOfRange, image.Code);
        Assert.Contains("page 2 does not exist: the file has 1 page", image.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that options that cannot be used are refused with <see cref="ArgumentOutOfRangeException"/> before the
    ///     stream is read.
    /// </summary>
    /// <param name="label">What is wrong.</param>
    /// <param name="options">The options.</param>
    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void CanvasNetViewer_Render_InvalidOptions_ThrowsBeforeReadingTheContent(string label, ViewerRenderOptions options)
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(new UnreadableStream(), "a.pdf", options));

        // Assert
        Assert.True(exception.ParamName == "options", label);
    }

    /// <summary>
    ///     Gets the render options that cannot be used, with a label.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string, ViewerRenderOptions> InvalidOptions()
    {
        return new TheoryData<string, ViewerRenderOptions>
        {
            { "page 0", new ViewerRenderOptions { Page = 0 } },
            { "page negative", new ViewerRenderOptions { Page = -1 } },
            { "dpi 0", new ViewerRenderOptions { Dpi = 0 } },
            { "dpi NaN", new ViewerRenderOptions { Dpi = double.NaN } },
            { "dpi above the maximum", new ViewerRenderOptions { Dpi = 2401 } },
            { "width 0", new ViewerRenderOptions { Width = 0 } },
            { "unknown view", new ViewerRenderOptions { Scad = new ScadViewOptions { View = "sideways" } } },
            { "elevation 91", new ViewerRenderOptions { Scad = new ScadViewOptions { Elevation = 91 } } },
            { "time too large", new ViewerRenderOptions { Scad = new ScadViewOptions { Time = 2e9 } } },
            { "triangle limit 0", new ViewerRenderOptions { Scad = new ScadViewOptions { MaxTriangles = 0 } } }
        };
    }

    /// <summary>
    ///     Test that a fractional resolution for a Visio drawing is a usage error of the loader, as for the tool.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_FractionalDpiForVsdx_ThrowsViewerExceptionUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        var exception = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { Dpi = 100.5 }));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }

    /// <summary>
    ///     Test that a background flattens a transparent page, an SVG file and a raster with transparency, and that the
    ///     defaults stay transparent for the vector kinds.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Background_FlattensTransparentAreas()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var svg = FixtureFactory.WriteText(directory.Path, "empty.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>");
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var pdfDefault = CanvasNetViewer.Render(pdf);
        using var pdfRed = CanvasNetViewer.Render(pdf, new ViewerRenderOptions { Background = red });
        using var svgDefault = CanvasNetViewer.Render(svg);
        using var svgRed = CanvasNetViewer.Render(svg, new ViewerRenderOptions { Background = red });

        // Assert: the top-right quarter of the page is transparent by default and red with a background
        Assert.Equal(0, pdfDefault.Surface[170, 20].A);
        Assert.Equal(red, pdfRed.Surface[170, 20]);
        Assert.Equal(0, svgDefault.Surface[5, 5].A);
        Assert.Equal(red, svgRed.Surface[5, 5]);
    }

    /// <summary>
    ///     Test that a chart keeps its own background by default and that a background replaces it.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_VegaLiteBackground_DefaultIsTheChartsOwn()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "vega-lite");
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var chart = CanvasNetViewer.Render(path);
        using var replaced = CanvasNetViewer.Render(path, new ViewerRenderOptions { Background = red });

        // Assert: opaque by default; the corner is red when the background is replaced
        Assert.Equal(255, chart.Surface[0, 0].A);
        Assert.Equal(red, replaced.Surface[0, 0]);
        Assert.Equal("vega-lite", chart.Format);
    }

    /// <summary>
    ///     Test that the options of a model choose its view and its variables, that its anchors and printed text come
    ///     back, and that a malformed anchor is a note with the finer code and the position.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadOptions_ChooseViewDefinesAndReportAnchorsEchoAndDetail()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "size = 4;\ncube([12, 4, size]);\ntranslate([0, 0, size]) cube([4, 4, 8]);\necho(\"anchor\", \"top\", [0, 0, 4]);\necho(\"anchor\", \"bad\", [1, 2]);\necho(\"hello\");\n");
        var small = new ViewerRenderOptions { Width = 64, Height = 64 };

        // Act
        using var front = CanvasNetViewer.Render(model, small with { Scad = new ScadViewOptions { View = "front" } });
        using var top = CanvasNetViewer.Render(model, small with { Scad = new ScadViewOptions { View = "top" } });
        using var big = CanvasNetViewer.Render(
            model,
            small with { Scad = new ScadViewOptions { View = "front", Defines = new Dictionary<string, object?> { ["size"] = 30.0 } } });

        // Assert: different views and different variables are different pictures; the anchors, echo and notes come back
        SurfaceAssertDifferent(front.Surface, top.Surface);
        SurfaceAssertDifferent(front.Surface, big.Surface);
        Assert.Equal(["top"], front.Anchors.Select(a => a.Name));
        Assert.Equal((0, 0, 4), (front.Anchors[0].X, front.Anchors[0].Y, front.Anchors[0].Z));
        Assert.NotNull(front.Anchors[0].PixelX);
        Assert.Contains("ECHO: \"hello\"", front.Echo);
        var warning = Assert.Single(front.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Warning);
        Assert.Equal("scad-warning", warning.Code);
        Assert.Equal("scad-anchor-invalid", warning.Detail);
        Assert.Equal(5, warning.Location?.Line);
    }

    /// <summary>
    ///     Test that a model whose include is missing is a <see cref="ViewerException"/> with the scad-error code, as for
    ///     the tool, and that a corrupt raster and a truncated chart are <see cref="ViewerException"/>s with the codes of
    ///     the tool whose inner exception is the library's.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Failures_HaveTheDocumentedShapes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "model.scad", "include <missing.scad>\ncube(1);\n");
        var corrupt = Path.Combine(directory.Path, "broken.png");
        File.WriteAllBytes(corrupt, FixtureFactory.CreatePngHeaderOnly(4, 4));
        var chart = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{ \"mark\": ");

        // Act
        var missingInclude = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(model));
        var broken = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(corrupt));
        var badChart = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(chart));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, missingInclude.Code);
        Assert.Equal(ErrorCodes.CorruptInput, broken.Code);
        Assert.IsAssignableFrom<InvalidDataException>(broken.InnerException);
        Assert.Equal(ErrorCodes.VegaLiteError, badChart.Code);
        Assert.IsType<VegaLiteSpecException>(badChart.InnerException);
    }

    /// <summary>
    ///     Test that rendering a file with the viewer gives the pixels the loader of the tool gives for the same file and
    ///     the equivalent options (the defaults of the two differ only in the background of a vector source, which is
    ///     given here).
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(ParityKeys))]
    public void CanvasNetViewer_Render_MatchesTheLoaderOfTheTool(string key)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var white = new Rgba32(255, 255, 255, 255);

        // Act
        using var viewer = CanvasNetViewer.Render(path, new ViewerRenderOptions { Background = white });
        using var loader = SourceLoader.Load(path, new SourceOptions { Background = white });

        // Assert: a chart keeps its own background (also white here) in both
        SurfaceAssert.Equal(loader.Surface, viewer.Surface);
        Assert.Equal(loader.Info.Format.ToFormatName(), viewer.Format);
        Assert.Equal(loader.Diagnostics.Select(d => (d.Code, d.Message)), viewer.Diagnostics.Select(d => (d.Code, d.Message)));
    }

    /// <summary>
    ///     Test that a raster is still fitted inside a requested box over a transparent margin and charged to the budget,
    ///     as before the loader was used.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_RasterWithBox_IsFittedAndCharged()
    {
        // Arrange: a 4 by 4 picture in a 10 by 6 box, and a budget that cannot hold the final buffer next to the source
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "a.png");
        File.WriteAllBytes(path, ViewerFixtures.Png(4, 4, ViewerFixtures.Green));

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6 });
        var refused = Assert.IsType<ImageSourceBudgetException>(Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6, MaxDecodedPixels = 16 + 59 })).InnerException);

        // Assert
        Assert.Equal((10, 6), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(0, image.Surface[0, 3].A);
        Assert.Equal(ViewerFixtures.Green, image.Surface[5, 3]);
        Assert.Equal(60, refused.RequestedPixels);
    }

    /// <summary>
    ///     Test that a canceled token stops a render of a document.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     A chart of three rows, which renders under the default limits.
    /// </summary>
    private const string ThreeRowChart =
        "{\"data\":{\"values\":[{\"a\":1},{\"a\":2},{\"a\":3}]},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"}}}";

    /// <summary>
    ///     A flowchart of three nodes, which renders under the default limits.
    /// </summary>
    private const string ThreeNodeFlowchart = "flowchart LR\n  A --> B --> C\n";

    /// <summary>
    ///     A sequence diagram of two participants, which renders under the default limits.
    /// </summary>
    private const string TwoParticipantSequence = "@startuml\nAlice -> Bob : hi\n@enduml\n";

    /// <summary>
    ///     Test that the limits of a chart and a diagram are not set by default, and that a render without them draws
    ///     the same pictures as a render with the default limits of each library.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_LimitsNotSet_AreTheLibraryDefaults()
    {
        // Arrange
        using var directory = new TempDirectory();
        var chart = FixtureFactory.WriteText(directory.Path, "chart.vl.json", ThreeRowChart);
        var flow = FixtureFactory.WriteText(directory.Path, "flow.mmd", ThreeNodeFlowchart);
        var sequence = FixtureFactory.WriteText(directory.Path, "sequence.puml", TwoParticipantSequence);
        var options = new ViewerRenderOptions();
        var defaults = new ViewerRenderOptions
        {
            VegaLiteLimits = VegaLiteLimits.Default,
            MermaidLimits = MermaidLimits.Default,
            PlantUmlLimits = PlantUmlLimits.Default
        };

        // Act / Assert
        Assert.Null(options.VegaLiteLimits);
        Assert.Null(options.MermaidLimits);
        Assert.Null(options.PlantUmlLimits);
        foreach (var path in new[] { chart, flow, sequence })
        {
            using var unset = CanvasNetViewer.Render(path, options);
            using var set = CanvasNetViewer.Render(path, defaults);
            SurfaceAssert.Equal(unset.Surface, set.Surface);
        }
    }

    /// <summary>
    ///     Test that a lower Vega-Lite limit refuses a chart that renders by default, with a
    ///     <see cref="ViewerException"/> of the code budget-exceeded that names the limit.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_LowerVegaLiteLimits_ThrowBudgetExceeded()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", ThreeRowChart);
        using var byDefault = CanvasNetViewer.Render(path);

        // Act
        var ex = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { VegaLiteLimits = new VegaLiteLimits { MaxDataRows = 2 } }));

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, ex.Code);
        Assert.Contains("MaxDataRows", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a higher Vega-Lite limit admits a chart that the default limit refuses, so the probe of the chart
    ///     uses the limit as well as its render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_RaisedVegaLiteLimits_AdmitALargerChart()
    {
        // Arrange: one row of more fields than the default limit of 1,024
        using var directory = new TempDirectory();
        var fields = string.Join(",", Enumerable.Range(0, 1100).Select(i => $"\"f{i}\":{i}"));
        var path = FixtureFactory.WriteText(
            directory.Path,
            "wide.vl.json",
            $"{{\"data\":{{\"values\":[{{{fields}}}]}},\"mark\":\"point\",\"encoding\":{{\"x\":{{\"field\":\"f0\",\"type\":\"quantitative\"}}}}}}");

        // Act
        var refused = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path));
        using var admitted = CanvasNetViewer.Render(path, new ViewerRenderOptions { VegaLiteLimits = new VegaLiteLimits { MaxFieldCount = 2048 } });

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, refused.Code);
        Assert.Contains("MaxFieldCount", refused.Message, StringComparison.Ordinal);
        Assert.Equal("vega-lite", admitted.Format);
    }

    /// <summary>
    ///     Test that a lower Mermaid limit refuses a diagram that renders by default, with a
    ///     <see cref="ViewerException"/> of the code budget-exceeded that names the limit.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_LowerMermaidLimits_ThrowBudgetExceeded()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "flow.mmd", ThreeNodeFlowchart);
        using var byDefault = CanvasNetViewer.Render(path);

        // Act
        var ex = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { MermaidLimits = new MermaidLimits { MaxNodes = 2 } }));

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, MermaidDiagnosticCodes.LimitExceeded), (ex.Code, ex.DetailCode));
        Assert.Contains("MaxNodes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a higher Mermaid limit admits a diagram that the default limit refuses, so the probe of the diagram
    ///     uses the limit as well as its render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_RaisedMermaidLimits_AdmitALargerDiagram()
    {
        // Arrange: a text longer than the default limit of 50,000 characters
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "flow.mmd", ThreeNodeFlowchart + new string(' ', 60_000) + "\n");

        // Act
        var refused = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path));
        using var admitted = CanvasNetViewer.Render(path, new ViewerRenderOptions { MermaidLimits = new MermaidLimits { MaxTextSize = 100_000 } });

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, refused.Code);
        Assert.Contains("MaxTextSize", refused.Message, StringComparison.Ordinal);
        Assert.Equal("mermaid", admitted.Format);
    }

    /// <summary>
    ///     Test that a lower PlantUML limit refuses a diagram that renders by default, with a
    ///     <see cref="ViewerException"/> of the code budget-exceeded that names the limit.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_LowerPlantUmlLimits_ThrowBudgetExceeded()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "sequence.puml", TwoParticipantSequence);
        using var byDefault = CanvasNetViewer.Render(path);

        // Act
        var ex = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { PlantUmlLimits = new PlantUmlLimits { MaxNodes = 1 } }));

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, PlantUmlDiagnosticCodes.LimitExceeded), (ex.Code, ex.DetailCode));
        Assert.Contains("MaxNodes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a higher PlantUML limit admits a diagram that the default limit refuses, so the probe of the diagram
    ///     uses the limit as well as its render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_RaisedPlantUmlLimits_AdmitALargerDiagram()
    {
        // Arrange: a diagram of more includes than the default limit of 64
        using var directory = new TempDirectory();
        var includes = new System.Text.StringBuilder();
        for (var i = 0; i < 70; i++)
        {
            FixtureFactory.WriteText(directory.Path, $"part{i}.iuml", "' part\n");
            includes.Append($"!include part{i}.iuml\n");
        }

        var path = FixtureFactory.WriteText(directory.Path, "sequence.puml", $"@startuml\n{includes}Alice -> Bob : hi\n@enduml\n");

        // Act
        var refused = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path));
        using var admitted = CanvasNetViewer.Render(path, new ViewerRenderOptions { PlantUmlLimits = new PlantUmlLimits { MaxIncludes = 100 } });

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, refused.Code);
        Assert.Contains("MaxIncludes", refused.Message, StringComparison.Ordinal);
        Assert.Equal("plantuml", admitted.Format);
    }

    /// <summary>
    ///     Test that the limits apply to a chart and a diagram an SVG file embeds: a lower limit refuses an embedded
    ///     diagram that renders by default, and a higher limit admits an embedded chart that the default refuses.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Limits_ApplyToEmbeddedChartsAndDiagrams()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", ThreeNodeFlowchart);
        var fields = string.Join(",", Enumerable.Range(0, 1100).Select(i => $"\"f{i}\":{i}"));
        FixtureFactory.WriteText(
            directory.Path,
            "wide.vl.json",
            $"{{\"data\":{{\"values\":[{{{fields}}}]}},\"mark\":\"point\",\"encoding\":{{\"x\":{{\"field\":\"f0\",\"type\":\"quantitative\"}}}}}}");
        var diagram = FixtureFactory.WriteText(directory.Path, "diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("flow.mmd", 0)));
        var chart = FixtureFactory.WriteText(directory.Path, "chart.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("wide.vl.json", 0)));
        using var diagramByDefault = CanvasNetViewer.Render(diagram);

        // Act
        var lowered = Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(diagram, new ViewerRenderOptions { MermaidLimits = new MermaidLimits { MaxNodes = 2 } }));
        var refusedChart = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(chart));
        using var admittedChart = CanvasNetViewer.Render(chart, new ViewerRenderOptions { VegaLiteLimits = new VegaLiteLimits { MaxFieldCount = 2048 } });

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, lowered.Code);
        Assert.StartsWith("'diagram.svg' embeds 'flow.mmd': ", lowered.Message, StringComparison.Ordinal);
        Assert.Contains("MaxNodes", lowered.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.BudgetExceeded, refusedChart.Code);
        Assert.Equal("svg", admittedChart.Format);
        Assert.DoesNotContain(admittedChart.Diagnostics, d => d.Severity != ImageDiagnosticSeverity.Info);
    }

    /// <summary>
    ///     Test that the new members of the render options have the documented defaults.
    /// </summary>
    [Fact]
    public void ViewerRenderOptions_Defaults_PageOneAndNoDpiBackgroundOrScad()
    {
        // Act
        var options = new ViewerRenderOptions();

        // Assert
        Assert.Equal(1, options.Page);
        Assert.Null(options.Dpi);
        Assert.Null(options.Background);
        Assert.Null(options.Scad);
    }

    /// <summary>
    ///     Asserts that two surfaces of the same size are not the same picture.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    private static void SurfaceAssertDifferent(Surface a, Surface b)
    {
        var different = false;
        for (var y = 0; y < a.Height && !different; y++)
        {
            different = !a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y));
        }

        Assert.True(different, "The two pictures are the same.");
    }
}
