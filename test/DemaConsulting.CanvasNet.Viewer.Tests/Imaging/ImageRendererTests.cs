// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageRenderer unit: loading a probed source, running the image pipeline, and writing the
///     pages, with the checks that precede any decoding and the all-or-nothing publish.
/// </summary>
public class ImageRendererTests
{
    /// <summary>
    ///     Writes a gradient PNG and probes it.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The path and the facts about the file.</returns>
    private static (string Path, SourceInfo Info) CreatePng(TempDirectory directory, string name)
    {
        var path = directory.Combine(name);
        using var surface = FixtureFactory.CreateGradient();
        PngCodec.Save(surface, path);
        return (path, SourceLoader.Probe(path));
    }

    /// <summary>
    ///     Creates a request with the default source and write options.
    /// </summary>
    /// <param name="output">The output path.</param>
    /// <param name="allPages"><see langword="true"/> to render every page.</param>
    /// <param name="operations">The operations.</param>
    /// <returns>The request.</returns>
    private static RenderRequest CreateRequest(string output, bool allPages = false, params IImageOperation[] operations)
    {
        return new RenderRequest(output, allPages, new SourceOptions(), WriteOptions.Default, operations);
    }

    /// <summary>
    ///     Test that a single-image source is written as a copy at its own size and reported page by page.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_SingleImage_WritesCopyAndReportsPage()
    {
        // Arrange: a gradient PNG
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        var output = directory.Combine("out.png");

        // Act
        var result = ImageRenderer.Execute(info, CreateRequest(output));

        // Assert: one page, the same pixels, and the facts of the source
        var page = Assert.Single(result.Pages);
        Assert.Equal(path, result.Info.Path);
        Assert.Equal(1, page.Page);
        Assert.Equal(output, page.Result.Path);
        Assert.Equal((FixtureFactory.Width, FixtureFactory.Height), (page.Result.Width, page.Result.Height));
        using var expected = FixtureFactory.CreateGradient();
        using var written = SurfaceAssert.Load(output);
        SurfaceAssert.Equal(expected, written);
    }

    /// <summary>
    ///     Test that the operations run between the load and the write, and are reported after those of the loader.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_Operations_AreAppliedInOrderAndReported()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (_, info) = CreatePng(directory, "in.png");
        var output = directory.Combine("out.png");

        // Act: crop to 8x4, then resize to 4x2
        var result = ImageRenderer.Execute(
            info,
            CreateRequest(
                output,
                false,
                new CropOperation(new GeometrySpec(new Dimension(0, false), new Dimension(0, false), new Dimension(8, false), new Dimension(4, false))),
                new ResizeOperation(4, 2, ResamplingMode.Nearest)));

        // Assert
        var page = Assert.Single(result.Pages);
        Assert.Equal((4, 2), (page.Result.Width, page.Result.Height));
        Assert.Equal(["crop", "resize"], page.Operations.Select(o => o.Op).TakeLast(2));
    }

    /// <summary>
    ///     Test that every page of a document is written into a directory output, named after the input and the page.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_AllPagesIntoDirectory_NamesEachPageAfterTheInput()
    {
        // Arrange: a two-page PDF and an output folder that does not exist yet
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var info = SourceLoader.Probe(pdf);
        var folder = directory.Combine("pages") + Path.DirectorySeparatorChar;

        // Act
        var result = ImageRenderer.Execute(info, CreateRequest(folder, true));

        // Assert: source-1.png and source-2.png in the folder, one per page
        Assert.Equal([1, 2], result.Pages.Select(p => p.Page));
        Assert.Equal(
            [Path.Combine(directory.Combine("pages"), "source-1.png"), Path.Combine(directory.Combine("pages"), "source-2.png")],
            result.Pages.Select(p => p.Result.Path));
        Assert.All(result.Pages, p => Assert.True(File.Exists(p.Result.Path)));
    }

    /// <summary>
    ///     Test that an output which is the input is refused before anything is written.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_OutputIsInput_ThrowsSameInputOutput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        var before = File.ReadAllBytes(path);

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageRenderer.Execute(info, CreateRequest(path)));

        // Assert: the code, and the input is untouched
        Assert.Equal(ErrorCodes.SameInputOutput, error.Code);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     Test that an output whose format cannot be told is a usage error and nothing is written.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_UnknownExtension_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (_, info) = CreatePng(directory, "in.png");

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageRenderer.Execute(info, CreateRequest(directory.Combine("out.xyz"))));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.False(File.Exists(directory.Combine("out.xyz")));
    }

    /// <summary>
    ///     Test that no-clobber refuses an existing output and leaves it untouched.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_NoClobberExistingOutput_ThrowsOutputExists()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (_, info) = CreatePng(directory, "in.png");
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old content");
        var request = CreateRequest(output) with { Write = WriteOptions.Default with { NoClobber = true } };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageRenderer.Execute(info, request));

        // Assert
        Assert.Equal(ErrorCodes.OutputExists, error.Code);
        Assert.Equal("old content", File.ReadAllText(output));
    }

    /// <summary>
    ///     Test that a page that does not exist is refused with the page code.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_PageOutOfRange_ThrowsPageOutOfRange()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (_, info) = CreatePng(directory, "in.png");
        var request = CreateRequest(directory.Combine("out.png")) with { Source = new SourceOptions { Page = 2 } };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageRenderer.Execute(info, request));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, error.Code);
    }

    /// <summary>
    ///     Test that a failing operation leaves no new output and no temporary file, and an existing output untouched.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_OperationFails_LeavesNothingBehind()
    {
        // Arrange: an existing output, and a crop that starts outside the image
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old content");
        var outside = new CropOperation(new GeometrySpec(new Dimension(100, false), new Dimension(100, false), new Dimension(5, false), new Dimension(5, false)));

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageRenderer.Execute(info, CreateRequest(output, false, outside)));

        // Assert: a usage error, the old output, and nothing else but the input
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal("old content", File.ReadAllText(output));
        Assert.Equal(
            new[] { path, output }.Order(StringComparer.Ordinal),
            Directory.GetFileSystemEntries(directory.Path).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that an explicit format writes a picture to an output without an extension.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_ExplicitFormatAndExtensionlessOutput_WritesThatFormat()
    {
        // Arrange: an output without an extension, with JPEG asked for in the write options
        using var directory = new TempDirectory();
        var (_, info) = CreatePng(directory, "in.png");
        var output = directory.Combine("out");
        var request = CreateRequest(output) with { Write = WriteOptions.Default with { Format = OutputFormat.Jpeg } };

        // Act
        var result = ImageRenderer.Execute(info, request);

        // Assert: the file is a JPEG, which starts with FF D8
        Assert.Equal(OutputFormat.Jpeg, Assert.Single(result.Pages).Result.Format);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(output).Take(2));
    }

    /// <summary>
    ///     Test that a request with a missing member is a caller error, and nothing is written.
    /// </summary>
    /// <param name="member">The member that is <see langword="null"/>.</param>
    [Theory]
    [InlineData("Output")]
    [InlineData("Source")]
    [InlineData("Write")]
    [InlineData("Operations")]
    public void ImageRenderer_Execute_NullRequestMember_ThrowsArgumentNullException(string member)
    {
        // Arrange: a valid request with one member removed
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        var request = CreateRequest(directory.Combine("out.png"));
        request = member switch
        {
            "Output" => request with { Output = null! },
            "Source" => request with { Source = null! },
            "Write" => request with { Write = null! },
            _ => request with { Operations = null! }
        };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImageRenderer.Execute(info, request));

        // Assert: the member is named, and nothing but the input is in the folder
        Assert.Contains(member, error.ParamName);
        Assert.Equal([path], Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that a missing operation in the list is a caller error, and nothing is written.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_NullOperationElement_ThrowsArgumentNullException()
    {
        // Arrange: a list whose second operation is missing
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        IImageOperation[] operations = [new InvertOperation(), null!];
        var request = CreateRequest(directory.Combine("out.png")) with { Operations = operations };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImageRenderer.Execute(info, request));

        // Assert: the list is named, and nothing but the input is in the folder
        Assert.Equal("operations", error.ParamName);
        Assert.Equal([path], Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that an operation with an invalid value is refused before the source is decoded or anything is written.
    /// </summary>
    [Fact]
    public void ImageRenderer_Execute_InvalidOperationValue_ThrowsUsageErrorBeforeDecodeOrWrite()
    {
        // Arrange: a probed source whose file is gone, so that decoding it would fail with another code
        using var directory = new TempDirectory();
        var (path, info) = CreatePng(directory, "in.png");
        File.Delete(path);

        // Act
        var error = Assert.Throws<ViewerException>(
            () => ImageRenderer.Execute(info, CreateRequest(directory.Combine("out.png"), false, new ResizeOperation(0, 0, ResamplingMode.Auto))));

        // Assert: the usage error of the operation, and nothing written
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("resize", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }
}
