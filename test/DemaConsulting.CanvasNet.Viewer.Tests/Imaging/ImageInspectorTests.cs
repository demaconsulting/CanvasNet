// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageInspector unit: the dominant colors of an image and the pixel at a point, after the
///     image pipeline.
/// </summary>
public class ImageInspectorTests
{
    /// <summary>
    ///     The red pixel color of the test images.
    /// </summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Writes a PNG of one color and probes it.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo CreateSolid(TempDirectory directory, int width, int height, Rgba32 color)
    {
        var path = directory.Combine("solid.png");
        using var surface = new Surface(width, height);
        surface.Clear(color);
        PngCodec.Save(surface, path);
        return SourceLoader.Probe(path);
    }

    /// <summary>
    ///     Creates a request without operations.
    /// </summary>
    /// <param name="kind">What to report.</param>
    /// <param name="point">The point, for a pixel.</param>
    /// <param name="radius">The radius of the window, for a pixel.</param>
    /// <returns>The request.</returns>
    private static InspectRequest CreateRequest(InspectKind kind, PixelPoint? point = null, int radius = 0)
    {
        return new InspectRequest(kind) { Radius = radius, Point = point };
    }

    /// <summary>
    ///     Test that the colors of a flat image are that color alone.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_Colors_ReportsTheDominantColor()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 10, 6, Red);

        // Act
        var result = ImageInspector.Execute(info, CreateRequest(InspectKind.Colors));

        // Assert: one color covering every pixel, and no pixel sample
        Assert.Null(result.Pixel);
        Assert.NotNull(result.Colors);
        var color = Assert.Single(result.Colors.Colors);
        Assert.Equal(Red, color.Color);
        Assert.Equal(1.0, color.Fraction, 6);
        Assert.Equal((10, 6), (result.Width, result.Height));
        Assert.Equal(1, result.Page);
        Assert.Equal(new Rgba32(255, 255, 255, 255), result.Background);
    }

    /// <summary>
    ///     Test that the pixel at a point is sampled with its window when a radius is given.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_PixelWithRadius_SamplesPixelAndWindow()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 10, 6, Red);

        // Act
        var result = ImageInspector.Execute(info, CreateRequest(InspectKind.Pixel, new PixelPoint(4, 3), 1));

        // Assert: the pixel, and a 3x3 window around it; no colors
        Assert.Null(result.Colors);
        Assert.NotNull(result.Pixel);
        Assert.Equal((4, 3), (result.Pixel.X, result.Pixel.Y));
        Assert.Equal(Red, result.Pixel.Pixel);
        Assert.NotNull(result.Pixel.Window);
        Assert.Equal((3, 3), (result.Pixel.Window.Width, result.Pixel.Window.Height));
    }

    /// <summary>
    ///     Test that a point outside the image is a usage error that names the valid range.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_PointOutsideImage_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 10, 6, Red);

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageInspector.Execute(info, CreateRequest(InspectKind.Pixel, new PixelPoint(10, 0))));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("outside the 10x6 image", error.Message);
        Assert.Contains("9,5", error.Hint);
    }

    /// <summary>
    ///     Test that the operations run first, so the size and the coordinates are those of the transformed image.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_Operations_AreAppliedBeforeTheAnalysis()
    {
        // Arrange: a 10x6 image, padded by 2 with a blue border
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 10, 6, Red);
        var blue = new Rgba32(0, 0, 255, 255);
        var request = InspectRequest.ForPixel(new PixelPoint(0, 0)) with
        {
            Operations = [new PadOperation(new PadSpec(2, 2, 2, 2), blue)]
        };

        // Act
        var result = ImageInspector.Execute(info, request);

        // Assert: the corner is the border, the image is 14x10, and the pad is reported
        Assert.Equal((14, 10), (result.Width, result.Height));
        Assert.Equal(blue, result.Pixel!.Pixel);
        Assert.Contains(result.Operations, o => o.Op == "pad");
    }

    /// <summary>
    ///     Test that a translucent background is reported as the opaque color it looks like over white.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_TranslucentBackground_IsReportedFlattenedOverWhite()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);
        var request = InspectRequest.ForColors(4) with { Source = new SourceOptions { Background = new Rgba32(0, 0, 0, 128) } };

        // Act
        var result = ImageInspector.Execute(info, request);

        // Assert: (0 * 128 + 255 * 127 + 127) / 255 = 127
        Assert.Equal(new Rgba32(127, 127, 127, 255), result.Background);
    }

    /// <summary>
    ///     Test that a pixel inspection without a point is a caller error.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_PixelWithoutPoint_ThrowsArgumentException()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ImageInspector.Execute(info, CreateRequest(InspectKind.Pixel)));
    }

    /// <summary>
    ///     Test that a request with a missing member is a caller error.
    /// </summary>
    /// <param name="member">The member that is <see langword="null"/>.</param>
    [Theory]
    [InlineData("Source")]
    [InlineData("Operations")]
    public void ImageInspector_Execute_NullRequestMember_ThrowsArgumentNullException(string member)
    {
        // Arrange: a valid request with one member removed
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);
        var request = CreateRequest(InspectKind.Colors);
        request = member == "Source" ? request with { Source = null! } : request with { Operations = null! };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImageInspector.Execute(info, request));

        // Assert: the member is named
        Assert.Contains(member, error.ParamName);
    }

    /// <summary>
    ///     Test that an undefined kind, or a count or radius out of range, is a caller error before the source is loaded.
    /// </summary>
    /// <param name="member">The member that is invalid.</param>
    [Theory]
    [InlineData("Kind")]
    [InlineData("CountLow")]
    [InlineData("CountHigh")]
    [InlineData("RadiusLow")]
    [InlineData("RadiusHigh")]
    public void ImageInspector_Execute_InvalidValue_ThrowsBeforeLoading(string member)
    {
        // Arrange: facts about a source whose file is gone, so loading it would fail differently
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);
        File.Delete(info.Path);
        var request = member switch
        {
            "Kind" => new InspectRequest((InspectKind)99),
            "CountLow" => CreateRequest(InspectKind.Colors) with { Count = SurfaceStatistics.MinColors - 1 },
            "CountHigh" => CreateRequest(InspectKind.Colors) with { Count = SurfaceStatistics.MaxColors + 1 },
            "RadiusLow" => InspectRequest.ForPixel(new PixelPoint(1, 1), radius: -1),
            _ => InspectRequest.ForPixel(new PixelPoint(1, 1), radius: PixelSample.MaxRadius + 1)
        };

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageInspector.Execute(info, request));
    }

    /// <summary>
    ///     Test that a missing operation in the list is a caller error.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_NullOperationElement_ThrowsArgumentNullException()
    {
        // Arrange: a list whose only operation is missing
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);
        IImageOperation[] operations = [null!];
        var request = CreateRequest(InspectKind.Colors) with { Operations = operations };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImageInspector.Execute(info, request));

        // Assert: the list is named
        Assert.Equal("operations", error.ParamName);
    }

    /// <summary>
    ///     Test that an operation with an invalid value is refused before the source is decoded.
    /// </summary>
    [Fact]
    public void ImageInspector_Execute_InvalidOperationValue_ThrowsUsageError()
    {
        // Arrange: a probed source whose file is gone, so that decoding it would fail with another code
        using var directory = new TempDirectory();
        var info = CreateSolid(directory, 4, 4, Red);
        File.Delete(info.Path);
        var request = CreateRequest(InspectKind.Colors) with { Operations = [new ResizeOperation(0, 0, ResamplingMode.Auto)] };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImageInspector.Execute(info, request));

        // Assert: the usage error of the operation
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("resize", error.Message);
    }
}
