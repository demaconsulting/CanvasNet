// cspell:ignore scad svg href vsdx pptx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Tests of the Vega-Lite <c>image</c> mark wiring of <see cref="SourceLoader"/> (Vega-Lite Phase 13): a chart shows pictures that lie beside it through the sandboxed resolver of the
///     viewer, the same one an SVG file embeds with, rooted at the folder of the chart. The tests draw charts whose pictures are a generated PNG, a model (<c>.scad</c>), a page of a PDF
///     (<c>report.pdf#page=2</c>), a nested chart and the failures (a missing file, a path outside the folder, a remote URL, a file that is corrupt, a page that does not exist, a cycle, the
///     limit of pictures), and assert the pixels that the placement of each picture puts the picture's colors into, the notes, the <c>embed</c> operations and the <c>--strict</c> policy.
/// </summary>
public class SourceLoaderVegaLiteImageTests
{
    /// <summary>The color of the pictures that are generated PNG files.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>The background of a chart.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     A 200 by 100 chart without padding or axes that shows two pictures of 40 by 40 pixels: the first link at 20 to 60 across and the second at 120 to 160, both from 30 to 70 down.
    /// </summary>
    /// <param name="first">The link of the first picture.</param>
    /// <param name="second">The link of the second picture.</param>
    /// <returns>The specification text.</returns>
    private static string TwoPictures(string first, string second) =>
        "{ \"width\": 200, \"height\": 100, \"padding\": 0, "
        + "\"data\": { \"values\": [ { \"x\": 2, \"u\": \"" + first + "\" }, { \"x\": 7, \"u\": \"" + second + "\" } ] }, "
        + "\"mark\": { \"type\": \"image\", \"width\": 40, \"height\": 40 }, "
        + "\"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10] }, \"axis\": null }, "
        + "\"y\": { \"value\": 50 }, \"url\": { \"field\": \"u\", \"type\": \"nominal\" } } }";

    /// <summary>
    ///     A chart with one picture of 40 by 40 pixels centered at (50, 50).
    /// </summary>
    /// <param name="link">The link of the picture.</param>
    /// <returns>The specification text.</returns>
    private static string OnePicture(string link) =>
        "{ \"width\": 100, \"height\": 100, \"padding\": 0, \"data\": { \"values\": [ { \"x\": 5 } ] }, "
        + "\"mark\": { \"type\": \"image\", \"width\": 40, \"height\": 40 }, "
        + "\"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10] }, \"axis\": null }, "
        + "\"y\": { \"value\": 50 }, \"url\": { \"value\": \"" + link + "\" } } }";

    /// <summary>
    ///     Writes a PNG file of one color.
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
    ///     Loads a chart at its natural size.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="strict"><see langword="true"/> to treat an unsupported feature as an error.</param>
    /// <returns>The loaded source; the caller disposes it.</returns>
    private static LoadedSource Load(string path, bool strict = false) => SourceLoader.Load(path, new SourceOptions { Strict = strict });

    /// <summary>
    ///     Determines whether a region of a surface holds a pixel that differs from a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The first column.</param>
    /// <param name="top">The first row.</param>
    /// <param name="right">The column after the region.</param>
    /// <param name="bottom">The row after the region.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when some pixel of the region is not <paramref name="color"/>.</returns>
    private static bool HasOtherThan(Surface surface, int left, int top, int right, int bottom, Rgba32 color) =>
        Enumerable.Range(top, bottom - top).SelectMany(y => Enumerable.Range(left, right - left).Select(x => surface[x, y])).Any(p => p != color);

    /// <summary>
    ///     Test that a chart draws a PNG and a model that lie beside it, each in its placement, with nothing else changed, no notes, and one <c>embed</c> operation for each file.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImagePngAndScad_DrawsBothAndListsEmbedOperations()
    {
        // Arrange: a green PNG, the model of the fixtures and a chart that shows them side by side
        using var directory = new TempDirectory();
        WriteSolidPng(directory.Combine("logo.png"), Green);
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", TwoPictures("logo.png", "part.scad"));

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: the PNG fills its box (20 to 60 across, 30 to 70 down) and the model drew something in its own (120 to 160)
        Assert.Equal((200, 100), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.Equal(Green, loaded.Surface[20, 30]);
        Assert.Equal(Green, loaded.Surface[59, 69]);
        Assert.Equal(Green, loaded.Surface[40, 50]);
        Assert.Equal(White, loaded.Surface[19, 30]);
        Assert.Equal(White, loaded.Surface[60, 30]);
        Assert.True(HasOtherThan(loaded.Surface, 120, 30, 160, 70, White));
        Assert.Equal(White, loaded.Surface[100, 50]);
        Assert.Equal(White, loaded.Surface[119, 50]);
        Assert.Equal(White, loaded.Surface[160, 50]);

        // Assert: no note (the library's report of the mark is gone with the resolver) and one embed operation for each file
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(["logo.png", "part.scad"], loaded.Operations.Where(o => o.Op == "embed").Select(o => (string)o.Fields.Single(f => f.Key == "href").Value));
        Assert.Equal(["raster", "scad"], loaded.Operations.Where(o => o.Op == "embed").Select(o => ((string)o.Fields.Single(f => f.Key == "format").Value).Replace("png", "raster", StringComparison.Ordinal)));
        Assert.Contains(loaded.Operations, o => o.Op == "render");
    }

    /// <summary>
    ///     Test that a page of a document is drawn through the fragment of the link (<c>report.pdf#page=2</c>), and that the link is listed as written.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImagePdfPage_DrawsTheChosenPage()
    {
        // Arrange: page 2 of the PDF fixture is green (with a yellow corner), and a chart that shows it in a 40 by 40 box
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", OnePicture("report.pdf#page=2"));

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: the middle of the page is its green, and the link of the operation keeps its fragment
        var middle = loaded.Surface[50, 50];
        Assert.True(middle.G > 120 && middle.R < 40 && middle.B < 40, $"The middle of page 2 is {middle}.");
        Assert.Equal(White, loaded.Surface[5, 5]);
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal("report.pdf#page=2", Assert.Single(loaded.Operations, o => o.Op == "embed").Fields.Single(f => f.Key == "href").Value);
    }

    /// <summary>
    ///     Test that a chart can show another chart, resolved from the folder of the chart that shows it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageNestedChart_ResolvesFromItsFolder()
    {
        // Arrange: the inner chart shows the PNG that lies beside it, in a sub folder
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("sub"));
        WriteSolidPng(directory.Combine("sub", "logo.png"), Green);
        FixtureFactory.WriteText(directory.Path, "sub/inner.vl.json", OnePicture("logo.png"));
        var path = FixtureFactory.WriteText(directory.Path, "outer.vl.json", OnePicture("sub/inner.vl.json"));

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: the inner chart, with its own white paper, is drawn in the box of the outer one, and its green picture shows in the middle
        Assert.Equal(Green, loaded.Surface[50, 50]);
        Assert.Equal(White, loaded.Surface[5, 5]);
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal("sub/inner.vl.json", Assert.Single(loaded.Operations, o => o.Op == "embed").Fields.Single(f => f.Key == "href").Value);
    }

    /// <summary>
    ///     Test that a chart that shows itself stops at the second level: the link is a warning, and in strict mode an error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageSelfCycle_IsUnresolved()
    {
        // Arrange: the chart shows its own file
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "loop.vl.json", OnePicture("loop.vl.json"));

        // Act
        using var loaded = Load(path);
        var strict = Assert.Throws<ViewerException>(() => Load(path, strict: true));

        // Assert: the warning says why, and the strict error names the chart and the link
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", FolderImageResolver.UnresolvedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains("cycle", note.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains("'loop.vl.json' embeds 'loop.vl.json', which was not drawn", strict.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link that is missing, is a remote URL or is a drive path that does not exist (or is not valid on Linux and macOS) is a warning of the resolver (the reason is
    ///     the resolver's), draws nothing, and does not leave the library's own warning for the same link next to it.
    /// </summary>
    /// <param name="link">The link.</param>
    [Theory]
    [InlineData("missing.png")]
    [InlineData("C:/secret.png")]
    [InlineData("https://example.invalid/secret.png")]
    public void SourceLoader_Load_VegaLiteImageUnresolvedLink_IsOneWarningAndNothingIsDrawn(string link)
    {
        // Arrange: a valid picture one folder above the chart
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("secret.png"), Green);
        var path = FixtureFactory.WriteText(directory.Combine("site"), "chart.vl.json", OnePicture(link));

        // Act
        using var loaded = Load(path);

        // Assert: one note, the resolver's, naming the link; the box is background; no embed operation
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", FolderImageResolver.UnresolvedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains($"'{link}'", note.Message, StringComparison.Ordinal);
        Assert.Equal(White, loaded.Surface[50, 50]);
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "embed");
    }

    /// <summary>
    ///     Test that, under a policy confined to the folder of the chart, a picture in the parent folder is one access-denied warning of the resolver (the library's own warning for
    ///     the same link is left out), draws nothing, and is the error <c>file-access-denied</c> in strict mode.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageOutsideTheRoots_IsOneAccessDeniedWarning()
    {
        // Arrange: a valid picture one folder above the chart
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("secret.png"), Green);
        var path = FixtureFactory.WriteText(directory.Combine("site"), "chart.vl.json", OnePicture("../secret.png"));
        var policy = FileAccessPolicy.UnderRoots(directory.Combine("site"));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { FileAccess = policy });
        var strict = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { FileAccess = policy, Strict = true }));

        // Assert
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", FolderImageResolver.AccessDeniedCode), (note.Source, note.Severity, note.Code));
        Assert.Contains("'../secret.png'", note.Message, StringComparison.Ordinal);
        Assert.Equal(White, loaded.Surface[50, 50]);
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "embed");
        Assert.Equal(ErrorCodes.FileAccessDenied, strict.Code);
    }

    /// <summary>
    ///     Test that, under the default policy, a picture in the parent folder of the chart is drawn.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageInTheParentFolder_IsDrawnByDefault()
    {
        // Arrange
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteSolidPng(directory.Combine("secret.png"), Green);
        var path = FixtureFactory.WriteText(directory.Combine("site"), "chart.vl.json", OnePicture("../secret.png"));

        // Act
        using var loaded = Load(path, strict: true);

        // Assert
        Assert.Empty(loaded.Diagnostics);
        Assert.NotEqual(White, loaded.Surface[50, 50]);
        Assert.Contains(loaded.Operations, o => o.Op == "embed");
    }

    /// <summary>
    ///     Test that strict mode makes a link that was not drawn an <c>unsupported-feature</c> error that names the chart file and the link, and that a chart whose pictures are all
    ///     drawn is unaffected by it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageStrict_UnresolvedLinkIsAnError()
    {
        // Arrange: one chart with a missing picture and one with a picture that exists
        using var directory = new TempDirectory();
        WriteSolidPng(directory.Combine("logo.png"), Green);
        var missing = FixtureFactory.WriteText(directory.Path, "missing.vl.json", OnePicture("nothing.png"));
        var fine = FixtureFactory.WriteText(directory.Path, "fine.vl.json", OnePicture("logo.png"));

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(missing, strict: true));
        using var loaded = Load(fine, strict: true);

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, error.Code);
        Assert.Contains("'missing.vl.json' embeds 'nothing.png', which was not drawn: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(FolderImageResolver.UnresolvedCode, error.Hint, StringComparison.Ordinal);
        Assert.Equal(Green, loaded.Surface[50, 50]);
    }

    /// <summary>
    ///     Test that a page that the document does not have is the warning <c>image-page-out-of-range</c>, and an error in strict mode.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImagePageOutOfRange_IsWarningThenStrictError()
    {
        // Arrange: a two page document and a link to its page 9
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", OnePicture("report.pdf#page=9"));

        // Act
        using var loaded = Load(path);
        var strict = Assert.Throws<ViewerException>(() => Load(path, strict: true));

        // Assert
        Assert.Contains(loaded.Diagnostics, d => d.Code == FolderImageResolver.PageOutOfRangeCode);
        Assert.Equal(White, loaded.Surface[50, 50]);
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
    }

    /// <summary>
    ///     Test that a picture that is found but broken is an error with the code of its own failure that names both files.
    /// </summary>
    /// <param name="fileName">The name of the picture.</param>
    /// <param name="content">The text of the picture, or <see langword="null"/> for a truncated PNG.</param>
    /// <param name="code">The expected error code.</param>
    [Theory]
    [InlineData("bad.png", null, ErrorCodes.CorruptInput)]
    [InlineData("bad.scad", "cube(;", ErrorCodes.ScadError)]
    public void SourceLoader_Load_VegaLiteImageBrokenFile_FailsWithItsCodeNamingBothFiles(string fileName, string? content, string code)
    {
        // Arrange
        using var directory = new TempDirectory();
        if (content == null)
        {
            File.WriteAllBytes(directory.Combine(fileName), FixtureFactory.CreatePngHeaderOnly(10, 10));
        }
        else
        {
            File.WriteAllText(directory.Combine(fileName), content);
        }

        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", OnePicture(fileName));

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(path));

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Contains("chart.vl.json", error.Message, StringComparison.Ordinal);
        Assert.Contains(fileName, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a chart with more distinct pictures than the library's limit (256) is a <c>budget-exceeded</c> error, however the resolver answers.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLiteImageMoreThanTheLimit_IsBudgetExceeded()
    {
        // Arrange: 257 rows, each with its own link to a file that does not exist
        using var directory = new TempDirectory();
        var rows = string.Join(", ", Enumerable.Range(0, 257).Select(i => "{ \"x\": " + (i % 10) + ", \"u\": \"p" + i + ".png\" }"));
        var chart = "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": [ " + rows + " ] }, "
            + "\"mark\": { \"type\": \"image\", \"width\": 2, \"height\": 2 }, "
            + "\"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": null }, \"y\": { \"value\": 5 }, \"url\": { \"field\": \"u\", \"type\": \"nominal\" } } }";
        var path = FixtureFactory.WriteText(directory.Path, "many.vl.json", chart);

        // Act
        var error = Assert.Throws<ViewerException>(() => Load(path));

        // Assert
        Assert.Equal(ErrorCodes.BudgetExceeded, error.Code);
        Assert.Contains("MaxImages", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a chart that is embedded in an SVG file shows its pictures through the resolver of the SVG file's render, resolved from the folder of the chart.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgEmbeddingChartWithImage_DrawsTheChartsPicture()
    {
        // Arrange: sub/chart.vl.json shows sub/logo.png; the SVG file places the chart in 100 by 100
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("sub"));
        WriteSolidPng(directory.Combine("sub", "logo.png"), Green);
        FixtureFactory.WriteText(directory.Path, "sub/chart.vl.json", OnePicture("logo.png"));
        var path = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' width='100' height='100'><rect width='100' height='100' fill='#ff0000'/>"
            + "<image href='sub/chart.vl.json' x='0' y='0' width='100' height='100'/></svg>");

        // Act
        using var loaded = Load(path, strict: true);

        // Assert: the chart's picture is green in the middle and the chart's own paper is white around it
        Assert.Equal(Green, loaded.Surface[50, 50]);
        Assert.Equal(White, loaded.Surface[5, 5]);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that probing a chart with pictures needs no file of the pictures, and that the natural size of the chart does not depend on them.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_VegaLiteImageChart_ReadsTheSizeWithoutThePictures()
    {
        // Arrange: a chart whose picture does not exist
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "chart.vl.json", OnePicture("nothing.png"));

        // Act
        var info = SourceLoader.Probe(path);

        // Assert
        Assert.Equal((100, 100), (info.Width, info.Height));
        Assert.Equal(["image"], info.Chart!.Marks);
    }
}
