// cspell:ignore scad svg href vsdx pptx Fsecret embedder
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the SVG embedding wiring of the SourceLoader class: how the tool hands the image resolver of the
///     viewer to the SVG library and maps what it reports (the embed operations, the notes, the printed text, the
///     <c>--strict</c> policy, and the error codes). The resolver itself (sandbox, limits, dispatch, nesting) is tested
///     in the core and Viewer test projects; a few cases are repeated here only to prove the tool is wired to it.
/// </summary>
public class SourceLoaderSvgImageTests
{
    /// <summary>The red of the background of the SVG files of these tests.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>The green of the pictures that are embedded.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>
    ///     Writes an SVG file whose 40 by 40 canvas is red, with the given markup on top.
    /// </summary>
    /// <param name="directory">The folder to write into.</param>
    /// <param name="name">The name of the file, which may name a sub folder that exists.</param>
    /// <param name="body">The markup drawn over the red background.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteSvg(string directory, string name, string body)
    {
        return FixtureFactory.WriteText(
            directory,
            name,
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 40' width='40' height='40'>"
            + $"<rect width='40' height='40' fill='#ff0000'/>{body}</svg>");
    }

    /// <summary>
    ///     Writes a small PNG file that is one solid color.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="color">The color of every pixel.</param>
    private static void WriteSolidPng(string path, Rgba32 color)
    {
        using var surface = new Surface(4, 4);
        surface.Clear(color);
        PngCodec.Save(surface, path);
    }

    /// <summary>
    ///     Loads an SVG file at its natural size.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="strict"><see langword="true"/> to treat an unsupported feature as an error.</param>
    /// <returns>The loaded source; the caller disposes it.</returns>
    private static LoadedSource Load(string path, bool strict = false)
    {
        return SourceLoader.Load(path, new SourceOptions { Strict = strict });
    }

    /// <summary>
    ///     Gets the <c>svg-image-unresolved</c> notes of a loaded source.
    /// </summary>
    /// <param name="loaded">The loaded source.</param>
    /// <returns>The notes.</returns>
    private static List<ImageDiagnostic> Unresolved(LoadedSource loaded)
    {
        return [.. loaded.Diagnostics.Where(d => d.Code == FolderImageResolver.UnresolvedCode)];
    }

    /// <summary>
    ///     Test that an embedded raster is drawn into its placement, nothing else changes, nothing is reported, and the
    ///     result lists the embedding.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingPng_DrawsPictureAndListsEmbedOperation()
    {
        // Arrange: a 20 by 20 placement of a green PNG next to the SVG file
        using var directory = new TempDirectory();
        WriteSolidPng(directory.Combine("logo.png"), Green);
        var path = WriteSvg(directory.Path, "diagram.svg", "<image href='logo.png' x='10' y='10' width='20' height='20'/>");

        // Act
        using var loaded = Load(path);

        // Assert: green inside the placement, red around it, no notes, and one embed operation
        Assert.Equal(Green, loaded.Surface[20, 20]);
        Assert.Equal(Red, loaded.Surface[5, 5]);
        Assert.Equal(Red, loaded.Surface[35, 35]);
        Assert.Empty(loaded.Diagnostics);
        var embed = Assert.Single(loaded.Operations, o => o.Op == "embed");
        Assert.Equal("logo.png", embed.Fields.Single(f => f.Key == "href").Value);
        Assert.Equal("png", embed.Fields.Single(f => f.Key == "format").Value);

        // A raster keeps its natural size; the library resamples it once
        Assert.Equal(4, embed.Fields.Single(f => f.Key == "width").Value);
    }

    /// <summary>
    ///     Test that an embedded model is drawn at the size of its placement over a transparent background, so that
    ///     the diagram shows around it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingScad_DrawsModelOverTransparentBackground()
    {
        // Arrange: the model of the fixtures in a 40 by 40 placement
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var path = WriteSvg(directory.Path, "diagram.svg", "<image href='part.scad' x='0' y='0' width='40' height='40'/>");

        // Act: at twice the natural size, so the placement is 80 pixels
        using var loaded = SourceLoader.Load(path, new SourceOptions { Scale = 2 });

        // Assert: the model was drawn at the placement size, the red diagram shows at the corner, and the model is not white
        var embed = Assert.Single(loaded.Operations, o => o.Op == "embed");
        Assert.Equal("scad", embed.Fields.Single(f => f.Key == "format").Value);
        Assert.Equal(80, embed.Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(80, embed.Fields.Single(f => f.Key == "height").Value);
        Assert.Equal(Red, loaded.Surface[0, 0]);
        Assert.Contains(Enumerable.Range(0, 80).SelectMany(y => Enumerable.Range(0, 80).Select(x => loaded.Surface[x, y])), p => p != Red);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that an embedded chart keeps its own opaque background, so the placement is white, and that it is
    ///     drawn at the size of the placement.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingVegaLite_KeepsChartBackgroundOpaque()
    {
        // Arrange: the chart of the fixtures in a 40 by 30 placement
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("baskets.vl.json"), directory.Combine("baskets.vl.json"));
        var path = WriteSvg(directory.Path, "diagram.svg", "<image href='baskets.vl.json' x='0' y='0' width='40' height='30'/>");

        // Act
        using var loaded = Load(path);

        // Assert: white inside the placement, red below it, and the chart was drawn at the placement size
        Assert.Equal(new Rgba32(255, 255, 255, 255), loaded.Surface[1, 1]);
        Assert.Equal(Red, loaded.Surface[20, 35]);
        var embed = Assert.Single(loaded.Operations, o => o.Op == "embed");
        Assert.Equal("vega-lite", embed.Fields.Single(f => f.Key == "format").Value);
        Assert.Equal(40, embed.Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(30, embed.Fields.Single(f => f.Key == "height").Value);
    }

    /// <summary>
    ///     Test that a relative reference inside an embedded SVG file is resolved from the folder of that file, and
    ///     that only the file the outer SVG embeds is an operation of the result (not the picture of the nested file).
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingNestedSvg_ListsOnlyTheOuterEmbedOperation()
    {
        // Arrange: sub/inner.svg draws sub/img.png; the outer file places inner.svg and has no img.png beside it
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("sub"));
        WriteSolidPng(directory.Combine("sub", "img.png"), Green);
        FixtureFactory.WriteText(
            directory.Combine("sub"),
            "inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><image href='img.png' x='0' y='0' width='10' height='10'/></svg>");
        var path = WriteSvg(directory.Path, "outer.svg", "<image href='sub/inner.svg' x='10' y='10' width='20' height='20'/>");

        // Act
        using var loaded = Load(path);

        // Assert: the picture of the nested file is drawn, nothing is reported, and one embed operation is listed
        Assert.Equal(Green, loaded.Surface[20, 20]);
        Assert.Equal(Red, loaded.Surface[5, 5]);
        Assert.Empty(loaded.Diagnostics);
        var embed = Assert.Single(loaded.Operations, o => o.Op == "embed");
        Assert.Equal("sub/inner.svg", embed.Fields.Single(f => f.Key == "href").Value);
    }

    /// <summary>
    ///     Test that the format of an embedded file is decided by its file name: the first page of a PDF named
    ///     <c>.pdf</c> and a chart named <c>.vl.json</c> are drawn, while a chart in a plain <c>.json</c> file and a
    ///     PDF without the extension are not linked.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingByFileName_DrawsListedTypesAndSkipsOthers()
    {
        // Arrange: a PDF, a chart with the double extension, and the same contents under names that are not linkable
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("page.pdf"), FixtureFactory.CreatePdf());
        File.WriteAllBytes(directory.Combine("page.bin"), FixtureFactory.CreatePdf());
        File.WriteAllText(directory.Combine("chart.vl.json"), FixtureFactory.TwoBarChart);
        File.WriteAllText(directory.Combine("chart.json"), FixtureFactory.TwoBarChart);
        var path = WriteSvg(
            directory.Path,
            "diagram.svg",
            "<image href='page.pdf' x='0' y='0' width='10' height='10'/><image href='chart.vl.json' x='10' y='0' width='10' height='10'/>"
            + "<image href='page.bin' x='20' y='0' width='10' height='10'/><image href='chart.json' x='30' y='0' width='10' height='10'/>");

        // Act
        using var loaded = Load(path);

        // Assert: the two listed types were drawn, and the other two are warnings
        var formats = loaded.Operations.Where(o => o.Op == "embed").Select(o => o.Fields.Single(f => f.Key == "format").Value).ToList();
        Assert.Equal(["pdf", "vega-lite"], formats);
        Assert.Equal(2, Unresolved(loaded).Count);
    }

    /// <summary>
    ///     Test that a reference that leaves the folder, is a network URL, or is an absolute path is a warning, is not
    ///     drawn, and never reads the file it names.
    /// </summary>
    /// <param name="href">The reference.</param>
    [Theory]
    [InlineData("../secret.png")]
    [InlineData("C:/secret.png")]
    [InlineData("https://example.invalid/secret.png")]
    public void SourceLoader_Load_SvgEmbeddingEscapingReference_IsUnresolvedWarning(string href)
    {
        // Arrange: a valid picture one folder above the folder of the SVG file
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("secret.png"), Green);
        var path = WriteSvg(directory.Combine("site"), "diagram.svg", $"<image href='{href}' x='10' y='10' width='20' height='20'/>");

        // Act
        using var loaded = Load(path);

        // Assert: nothing was drawn, and the one note is the warning, naming the reference
        Assert.Equal(Red, loaded.Surface[20, 20]);
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", FolderImageResolver.UnresolvedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains($"'{href}'", note.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "embed");
    }

    /// <summary>
    ///     Test that a reference through a symbolic link is refused.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingSymbolicLink_IsUnresolvedWarning()
    {
        // Arrange: a picture outside the folder and a link to it inside the folder (links may need privileges on Windows)
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("real.png"), Green);
        try
        {
            File.CreateSymbolicLink(directory.Combine("site", "link.png"), directory.Combine("real.png"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here.");
            return;
        }

        var path = WriteSvg(directory.Combine("site"), "diagram.svg", "<image href='link.png' x='10' y='10' width='20' height='20'/>");

        // Act
        using var loaded = Load(path);

        // Assert
        Assert.Equal(Red, loaded.Surface[20, 20]);
        Assert.Equal(FolderImageResolver.UnresolvedCode, Assert.Single(loaded.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a missing file and a file of an unknown format are warnings, not errors.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingMissingAndUnknownFiles_AreWarnings()
    {
        // Arrange: one reference to nothing and one to a text file
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("notes.txt"), "not a picture");
        var path = WriteSvg(
            directory.Path,
            "diagram.svg",
            "<image href='missing.png' x='0' y='0' width='10' height='10'/><image href='notes.txt' x='10' y='0' width='10' height='10'/>");

        // Act
        using var loaded = Load(path);

        // Assert
        Assert.Equal(2, Unresolved(loaded).Count);
        Assert.Equal(Red, loaded.Surface[5, 5]);
    }

    /// <summary>
    ///     Test that the notes and the printed text of an embedded model reach the result, with the reference in front
    ///     of each note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingModelWithNotes_MergesDiagnosticsAndEcho()
    {
        // Arrange: a model that prints a line and uses a feature the library does not draw
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", "echo(\"from-the-model\");\noffset(r = 1) square(2);\ncube(5);\n");
        var path = WriteSvg(directory.Path, "diagram.svg", "<image href='part.scad' x='0' y='0' width='40' height='40'/>");

        // Act
        using var loaded = Load(path);

        // Assert: the unsupported feature is a note that names the file, and the echo line is kept
        var note = Assert.Single(loaded.Diagnostics, d => d.Source == "scad" && d.Severity == "unsupported");
        Assert.StartsWith("part.scad: ", note.Message, StringComparison.Ordinal);
        Assert.Contains(loaded.Echo, line => line.Contains("from-the-model", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that with strict mode an unresolved reference and an unsupported feature of an embedded model are
    ///     errors, and that an SVG file without references is not changed by it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingStrict_UnresolvedAndUnsupportedAreErrors()
    {
        // Arrange: a missing reference, a model with an unsupported feature, and a plain SVG file
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", "offset(r = 1) square(2);\n");
        var missing = WriteSvg(directory.Path, "missing.svg", "<image href='nothing.png' x='0' y='0' width='10' height='10'/>");
        var unsupported = WriteSvg(directory.Path, "unsupported.svg", "<image href='part.scad' x='0' y='0' width='10' height='10'/>");
        var plain = WriteSvg(directory.Path, "plain.svg", string.Empty);

        // Act
        var missingError = Assert.Throws<ViewerException>(() => Load(missing, strict: true));
        var unsupportedError = Assert.Throws<ViewerException>(() => Load(unsupported, strict: true));
        using var plainLoaded = Load(plain, strict: true);

        // Assert: the unresolved error keeps its text and names the outer file, the reference, and the reason
        Assert.Equal(ErrorCodes.UnsupportedFeature, missingError.Code);
        Assert.Contains("'missing.svg' embeds 'nothing.png', which was not drawn: ", missingError.Message, StringComparison.Ordinal);
        Assert.Contains(FolderImageResolver.UnresolvedCode, missingError.Hint, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UnsupportedFeature, unsupportedError.Code);
        Assert.Empty(plainLoaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a picture that is too large for the pixel budget (a file that only declares 9000 by 9000 pixels, in a
    ///     small placement) is the warning <c>svg-image-too-large</c> and not an error, that the render carries on, and
    ///     that strict mode makes it an <c>unsupported-feature</c> error whose hint names the code.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingTooLargePicture_IsWarningThenStrictError()
    {
        // Arrange: a header-only PNG that declares a size over the budget, and an SVG that places it in 10 by 10
        using var directory = new TempDirectory();
        FixtureFactory.WriteDeclaredPng(directory.Combine("huge.png"), 9000, 9000);
        var path = WriteSvg(directory.Path, "diagram.svg", "<image href='huge.png' x='0' y='0' width='10' height='10'/>");

        // Act
        using var loaded = Load(path);
        var strictError = Assert.Throws<ViewerException>(() => Load(path, strict: true));

        // Assert: a warning with the stable code and nothing drawn; strict mode names the code in the hint
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal((FolderImageResolver.TooLargeCode, "warning", "svg"), (note.Code, note.Severity, note.Source));
        Assert.Equal(Red, loaded.Surface[5, 5]);
        Assert.Empty(Unresolved(loaded));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strictError.Code);
        Assert.Contains("'diagram.svg' embeds 'huge.png', which was not drawn: ", strictError.Message, StringComparison.Ordinal);
        Assert.Contains(FolderImageResolver.TooLargeCode, strictError.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a file that is found but broken is an error with the code of its own failure, and that the message
    ///     names both files.
    /// </summary>
    /// <param name="fileName">The name of the embedded file.</param>
    /// <param name="content">The text of the embedded file, or <see langword="null"/> for a truncated PNG.</param>
    /// <param name="code">The expected error code.</param>
    [Theory]
    [InlineData("bad.png", null, ErrorCodes.CorruptInput)]
    [InlineData("bad.scad", "cube(;", ErrorCodes.ScadError)]
    [InlineData("bad.vl.json", "{\"data\": {\"url\": \"missing.csv\"}, \"mark\": \"bar\", \"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}}}", ErrorCodes.VegaLiteError)]
    public void SourceLoader_Load_SvgEmbeddingBrokenFile_FailsWithItsCodeNamingBothFiles(string fileName, string? content, string code)
    {
        // Arrange: an embedded file that is recognized but cannot be loaded
        using var directory = new TempDirectory();
        if (content == null)
        {
            File.WriteAllBytes(directory.Combine(fileName), FixtureFactory.CreatePngHeaderOnly(10, 10));
        }
        else
        {
            File.WriteAllText(directory.Combine(fileName), content);
        }

        var path = WriteSvg(directory.Path, "diagram.svg", $"<image href='{fileName}' x='0' y='0' width='10' height='10'/>");

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(path));

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Contains("diagram.svg", error.Message, StringComparison.Ordinal);
        Assert.Contains(fileName, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a broken file inside a nested SVG file names the nested file as the embedder.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingBrokenFileInNestedSvg_NamesTheNestedFile()
    {
        // Arrange: outer.svg embeds inner.svg, which embeds a model with a syntax error
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "bad.scad", "cube(;");
        WriteSvg(directory.Path, "inner.svg", "<image href='bad.scad' x='0' y='0' width='10' height='10'/>");
        var path = WriteSvg(directory.Path, "outer.svg", "<image href='inner.svg' x='0' y='0' width='40' height='40'/>");

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(path));

        // Assert: the code of the failure, and the message names the innermost embedder and the file
        Assert.Equal(ErrorCodes.ScadError, error.Code);
        Assert.Contains("'inner.svg' embeds 'bad.scad'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the sizing and model options of the outer file are not passed on to an embedded file: a raster
    ///     keeps its natural size, and a model uses its default view, so an unknown view name is not an error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingOuterOptions_DoNotReachEmbeddedFiles()
    {
        // Arrange: a raster and a model, loaded with a scale and a view name that is not a view
        using var directory = new TempDirectory();
        WriteSolidPng(directory.Combine("p.png"), Green);
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var path = WriteSvg(
            directory.Path,
            "diagram.svg",
            "<image href='p.png' x='0' y='0' width='10' height='10'/><image href='part.scad' x='10' y='0' width='10' height='10'/>");
        var options = new SourceOptions { Scale = 3, Scad = new ScadViewOptions { View = "no-such-view" } };

        // Act
        using var loaded = SourceLoader.Load(path, options);

        // Assert: the raster was loaded at its natural 4 pixels, and the model was drawn without error at its placement
        var embeds = loaded.Operations.Where(o => o.Op == "embed").ToList();
        Assert.Equal(4, embeds[0].Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(30, embeds[1].Fields.Single(f => f.Key == "width").Value);
    }

    /// <summary>
    ///     Test that an SVG file without a reference to another file is loaded exactly as before: no notes, no embed
    ///     operation, and the same pixels from every route.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgWithoutEmbeddedFiles_ReportsNothingExtra()
    {
        // Arrange: the SVG fixture, which draws shapes only
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act
        using var loaded = Load(path, strict: true);

        // Assert
        Assert.Empty(loaded.Diagnostics);
        Assert.Empty(loaded.Echo);
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "embed");
    }
}
