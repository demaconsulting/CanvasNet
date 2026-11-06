// cspell:ignore href scad pdf slnx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;
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
}
