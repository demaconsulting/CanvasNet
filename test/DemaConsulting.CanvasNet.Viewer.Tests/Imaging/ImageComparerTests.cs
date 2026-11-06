// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageComparer unit: measuring two images, bringing them to one size, checking thresholds as
///     data, and writing the pictures of the differences.
/// </summary>
public class ImageComparerTests
{
    /// <summary>
    ///     The opaque white background of the comparisons.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Writes a PNG of one color and creates the comparison input for it.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="label">The side, <c>a</c> or <c>b</c>; the file is named after it.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The input.</returns>
    private static CompareInput CreateInput(TempDirectory directory, string label, int width, int height, Rgba32 color)
    {
        var path = directory.Combine(label + ".png");
        using var surface = new Surface(width, height);
        surface.Clear(color);
        PngCodec.Save(surface, path);
        return new CompareInput(label, SourceLoader.Probe(path));
    }

    /// <summary>
    ///     Creates a request with the defaults of the tool.
    /// </summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input.</param>
    /// <returns>The request, which the caller adjusts with <see langword="with"/>.</returns>
    private static CompareRequest CreateRequest(CompareInput a, CompareInput b)
    {
        return new CompareRequest(a, b);
    }

    /// <summary>
    ///     Test that two identical images are reported as identical, with no checks and no pictures.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_IdenticalImages_ReportsIdentical()
    {
        // Arrange
        using var directory = new TempDirectory();
        var red = new Rgba32(255, 0, 0, 255);
        var request = CreateRequest(CreateInput(directory, "a", 20, 10, red), CreateInput(directory, "b", 20, 10, red));

        // Act
        var result = ImageComparer.Execute(request);

        // Assert
        Assert.True(result.Metrics.Identical);
        Assert.Empty(result.Checks);
        Assert.Empty(result.Pictures);
        Assert.Equal((20, 10), (result.A.Width, result.A.Height));
        Assert.Equal("a", result.A.Label);
        Assert.Equal("b", result.B.Label);
    }

    /// <summary>
    ///     Test that a threshold that is not met is data in the result, not an exception.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_ThresholdNotMet_IsReturnedAsFailedCheck()
    {
        // Arrange: black against white, with a limit no difference can meet
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            Thresholds = [new ComparisonThreshold(ThresholdMetric.Mae, 1)]
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: not identical, and the one check failed with its measured value
        Assert.False(result.Metrics.Identical);
        var check = Assert.Single(result.Checks);
        Assert.False(check.Passed);
        Assert.Equal(ThresholdMetric.Mae, check.Metric);
        Assert.True(check.Value > 1);
    }

    /// <summary>
    ///     Test that inputs of different sizes are refused when no resize was requested.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_DifferentSizesWithoutResize_ThrowsSizeMismatch()
    {
        // Arrange
        using var directory = new TempDirectory();
        var red = new Rgba32(255, 0, 0, 255);
        var request = CreateRequest(CreateInput(directory, "a", 20, 10, red), CreateInput(directory, "b", 10, 10, red));

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageComparer.Execute(request));

        // Assert: the code, and a message naming both sizes
        Assert.Equal(ErrorCodes.SizeMismatch, error.Code);
        Assert.Contains("20x10", error.Message);
        Assert.Contains("10x10", error.Message);
    }

    /// <summary>
    ///     Test that a resize to the size of the first input fits the second into it and reports the fit.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_ResizeToA_FitsTheSecondInputAndReportsIt()
    {
        // Arrange: a 20x10 image against a 10x10 one
        using var directory = new TempDirectory();
        var red = new Rgba32(255, 0, 0, 255);
        var request = CreateRequest(CreateInput(directory, "a", 20, 10, red), CreateInput(directory, "b", 10, 10, red)) with
        {
            Resize = new ResizeTarget("a", 0, 0)
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: both are 20x10 now; b was loaded at 10x10 and fitted
        Assert.Equal((20, 10), (result.B.Width, result.B.Height));
        Assert.Equal((10, 10), (result.B.LoadedWidth, result.B.LoadedHeight));
        Assert.Contains(result.B.Operations, o => o.Op == "fit");
        Assert.Empty(result.A.Operations);
    }

    /// <summary>
    ///     Test that a grid with more cells than pixels is a usage error.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_GridLargerThanImage_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var red = new Rgba32(255, 0, 0, 255);
        var request = CreateRequest(CreateInput(directory, "a", 8, 8, red), CreateInput(directory, "b", 8, 8, red)) with
        {
            GridColumns = 9
        };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageComparer.Execute(request));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("9x4", error.Message);
    }

    /// <summary>
    ///     Test that the requested pictures are written in the order diff, side-by-side, overlay.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_Pictures_AreWrittenInOrder()
    {
        // Arrange
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            OverlayPath = directory.Combine("overlay.png"),
            DiffPath = directory.Combine("diff.png"),
            SideBySidePath = directory.Combine("side.png")
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: three files, in the order of the kinds, the side-by-side picture three panels wide
        Assert.Equal(["diff", "sideBySide", "overlay"], result.Pictures.Select(p => p.Kind));
        Assert.All(result.Pictures, p => Assert.True(File.Exists(p.Result.Path)));
        Assert.True(result.Pictures[1].Result.Width >= 3 * 20);
        Assert.Equal((20, 10), (result.Pictures[0].Result.Width, result.Pictures[0].Result.Height));
    }

    /// <summary>
    ///     Test that a picture which would overwrite an input is refused before the inputs are decoded.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_PictureIsAnInput_ThrowsSameInputOutput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var a = CreateInput(directory, "a", 20, 10, White);
        var request = CreateRequest(a, CreateInput(directory, "b", 20, 10, White)) with { DiffPath = a.Info.Path };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageComparer.Execute(request));

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, error.Code);
    }

    /// <summary>
    ///     Test that an explicit format writes a picture to an output without an extension.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_ExplicitFormatAndExtensionlessPath_WritesThatFormat()
    {
        // Arrange: a difference picture named without an extension, with the format given in the write options
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            DiffPath = directory.Combine("diff"),
            Write = WriteOptions.Default with { Format = OutputFormat.Png }
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: the file exists, is reported as a PNG, and starts with the PNG signature
        var picture = Assert.Single(result.Pictures);
        Assert.Equal(OutputFormat.Png, picture.Result.Format);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(directory.Combine("diff")).Take(4));
    }

    /// <summary>
    ///     Test that an explicit format wins over a custom extension.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_ExplicitFormatAndCustomExtension_FormatWinsOverExtension()
    {
        // Arrange: a picture named diff.dat with JPEG asked for
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            DiffPath = directory.Combine("diff.dat"),
            Write = WriteOptions.Default with { Format = OutputFormat.Jpeg }
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: a JPEG, which starts with FF D8
        Assert.Equal(OutputFormat.Jpeg, Assert.Single(result.Pictures).Result.Format);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(directory.Combine("diff.dat")).Take(2));
    }

    /// <summary>
    ///     Test that an output whose format cannot be told is refused before anything is written.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_NoFormatAndNoExtension_ThrowsUsageErrorBeforeWriting()
    {
        // Arrange: no explicit format and no extension
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            DiffPath = directory.Combine("diff")
        };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageComparer.Execute(request));

        // Assert: a usage error, and nothing but the two inputs in the folder
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Path).Length);
    }

    /// <summary>
    ///     Test that an explicit format that is not a known one is a usage error.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_UnknownExplicitFormat_ThrowsUsageError()
    {
        // Arrange: a format value that no member has
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, White),
            CreateInput(directory, "b", 20, 10, White)) with
        {
            DiffPath = directory.Combine("diff.png"),
            Write = WriteOptions.Default with { Format = (OutputFormat)99 }
        };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageComparer.Execute(request));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("Unknown output format", error.Message);
    }

    /// <summary>
    ///     Test that a request with a missing member is a caller error, and nothing is written.
    /// </summary>
    /// <param name="member">The member that is <see langword="null"/>.</param>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("A.Label")]
    [InlineData("B.Label")]
    [InlineData("A.Info")]
    [InlineData("B.Info")]
    [InlineData("A.Source")]
    [InlineData("B.Source")]
    [InlineData("Thresholds")]
    [InlineData("Write")]
    public void ImageComparer_Execute_NullRequestMember_ThrowsArgumentNullException(string member)
    {
        // Arrange: a valid request that writes a picture, with one member removed
        using var directory = new TempDirectory();
        var a = CreateInput(directory, "a", 20, 10, White);
        var b = CreateInput(directory, "b", 20, 10, White);
        var request = CreateRequest(a, b) with { DiffPath = directory.Combine("diff.png") };
        request = member switch
        {
            "A" => request with { A = null! },
            "B" => request with { B = null! },
            "A.Label" => request with { A = a with { Label = null! } },
            "B.Label" => request with { B = b with { Label = null! } },
            "A.Info" => request with { A = a with { Info = null! } },
            "B.Info" => request with { B = b with { Info = null! } },
            "A.Source" => request with { A = a with { Source = null! } },
            "B.Source" => request with { B = b with { Source = null! } },
            "Thresholds" => request with { Thresholds = null! },
            _ => request with { Write = null! }
        };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImageComparer.Execute(request));

        // Assert: the member is named, and nothing but the two inputs is in the folder
        Assert.Contains(member, error.ParamName);
        Assert.Equal(2, Directory.GetFileSystemEntries(directory.Path).Length);
    }

    /// <summary>
    ///     Test that an undefined value or a value out of range is a caller error before anything is loaded or written,
    ///     whichever outputs the request asks for.
    /// </summary>
    /// <param name="member">The member that is invalid.</param>
    [Theory]
    [InlineData("Fit")]
    [InlineData("Filter")]
    [InlineData("DiffStyle")]
    [InlineData("Tolerance")]
    [InlineData("GridColumns")]
    [InlineData("GridRows")]
    [InlineData("Gain")]
    [InlineData("OverlayWeight")]
    [InlineData("Threshold")]
    [InlineData("ResizeSize")]
    [InlineData("ResizeSide")]
    public void ImageComparer_Execute_InvalidValue_ThrowsBeforeLoading(string member)
    {
        // Arrange: two inputs of the same size and no requested picture, so no path would touch the invalid value
        using var directory = new TempDirectory();
        var a = CreateInput(directory, "a", 20, 10, White);
        var b = CreateInput(directory, "b", 20, 10, White);
        File.Delete(a.Info.Path);
        var request = CreateRequest(a, b);
        request = member switch
        {
            "Fit" => request with { Fit = (FitMode)99 },
            "Filter" => request with { Filter = (ResamplingMode)99 },
            "DiffStyle" => request with { DiffStyle = (DiffStyle)99 },
            "Tolerance" => request with { Tolerance = 256 },
            "GridColumns" => request with { GridColumns = 0 },
            "GridRows" => request with { GridRows = ComparisonResult.MaxGridSide + 1 },
            "Gain" => request with { Gain = double.NaN },
            "OverlayWeight" => request with { OverlayWeight = 1.5 },
            "Threshold" => request with { Thresholds = [new ComparisonThreshold((ThresholdMetric)99, 1)] },
            "ResizeSize" => request with { Resize = ResizeTarget.To(0, 10) },
            _ => request with { Resize = new ResizeTarget("c", 0, 0) }
        };

        // Act / Assert: refused as a caller error, although input A no longer exists (so nothing was loaded)
        Assert.ThrowsAny<ArgumentException>(() => ImageComparer.Execute(request));
        Assert.Single(Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that the metrics of the result are the metrics of the comparison, so the RMSE reads one level less deep,
    ///     and that the grid and the tolerance stay on the comparison.
    /// </summary>
    [Fact]
    public void ImageComparer_Execute_Result_MetricsIsShortcutToComparisonMetrics()
    {
        // Arrange: black against white
        using var directory = new TempDirectory();
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, new Rgba32(0, 0, 0, 255)),
            CreateInput(directory, "b", 20, 10, White));

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: the same numbers, on the 0 to 255 scale, and the grid is on the comparison
        Assert.Equal(result.Comparison.Metrics, result.Metrics);
        Assert.Equal(255.0, result.Metrics.Rmse, 6);
        Assert.Equal(ImageComparison.DefaultTolerance, result.Comparison.Tolerance);
        Assert.Equal(ImageComparison.DefaultGridColumns * ImageComparison.DefaultGridRows, result.Comparison.Cells.Count);
    }

    /// <summary>
    ///     Test that the resize targets that the helpers create are the ones the library understands: the size of one
    ///     input, or an explicit size.
    /// </summary>
    [Fact]
    public void ResizeTarget_Helpers_CreateTheThreeShapes()
    {
        // Arrange / Act / Assert
        Assert.Equal(new ResizeTarget("a", 0, 0), ResizeTarget.ToInputA);
        Assert.Equal(new ResizeTarget("b", 0, 0), ResizeTarget.ToInputB);
        Assert.Equal(new ResizeTarget(null, 30, 20), ResizeTarget.To(30, 20));
    }

    /// <summary>
    ///     Test that each helper brings both inputs to the size it names.
    /// </summary>
    /// <param name="mode">Which target: <c>a</c>, <c>b</c>, or <c>size</c>.</param>
    /// <param name="width">The expected compared width.</param>
    /// <param name="height">The expected compared height.</param>
    [Theory]
    [InlineData("a", 20, 10)]
    [InlineData("b", 40, 30)]
    [InlineData("size", 30, 12)]
    public void ImageComparer_Execute_ResizeHelper_BringsInputsToTheNamedSize(string mode, int width, int height)
    {
        // Arrange: a 20 x 10 raster against a 40 x 30 raster
        using var directory = new TempDirectory();
        var target = mode switch
        {
            "a" => ResizeTarget.ToInputA,
            "b" => ResizeTarget.ToInputB,
            _ => ResizeTarget.To(30, 12)
        };
        var request = CreateRequest(
            CreateInput(directory, "a", 20, 10, White),
            CreateInput(directory, "b", 40, 30, White)) with
        {
            Resize = target
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert
        Assert.Equal((width, height), (result.A.Width, result.A.Height));
        Assert.Equal((width, height), (result.B.Width, result.B.Height));
    }
}
