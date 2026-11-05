// cspell:ignore canvasnet lanczos
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for applying operation records directly with the ImagePipeline unit, without any command line:
///     the order, the reports, the ownership of surfaces, and the error contract of the public surface.
/// </summary>
public class ImagePipelineApplyTests
{
    /// <summary>
    ///     The fully transparent color, the default border of the tests.
    /// </summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     Creates an image whose red channel is ten times the position of the pixel in reading order.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    private static Surface Numbered(int width, int height)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = new Rgba32((byte)(((y * width) + x) * 10), 0, 0, 255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Creates a rectangle in pixels.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The region.</returns>
    private static GeometrySpec Pixels(double x, double y, double width, double height)
    {
        return new GeometrySpec(new Dimension(x, false), new Dimension(y, false), new Dimension(width, false), new Dimension(height, false));
    }

    /// <summary>
    ///     Test that a quarter turn moves the pixels and swaps the size, and the report names the operation.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_RotateRecord_TurnsClockwiseAndReportsTheStep()
    {
        // Arrange: 3x2, red channels 0 10 20 / 30 40 50
        using var input = Numbered(3, 2);

        // Act
        using var result = ImagePipeline.Apply(input, [new RotateOperation(90)], Transparent, out var applied);

        // Assert: 2x3 with the left column of the input (0, 30) as the top row
        Assert.Equal((2, 3), (result.Width, result.Height));
        Assert.Equal((byte)30, result[0, 0].R);
        Assert.Equal((byte)0, result[1, 0].R);
        Assert.Equal("rotate", Assert.Single(applied).Op);
    }

    /// <summary>
    ///     Test that the operations run in the order given.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropThenResize_DiffersFromResizeThenCrop()
    {
        // Arrange
        using var input = Numbered(4, 4);
        var crop = new CropOperation(Pixels(0, 0, 2, 2));
        var resize = new ResizeOperation(4, 4, ResamplingMode.Nearest);
        var bigResize = new ResizeOperation(8, 8, ResamplingMode.Nearest);

        // Act
        using var cropFirst = ImagePipeline.Apply(input, [crop, resize], Transparent, out var cropFirstApplied);
        using var resizeFirst = ImagePipeline.Apply(input, [bigResize, crop], Transparent, out _);

        // Assert: different sizes, and each step is reported in the order it ran
        Assert.Equal((4, 4), (cropFirst.Width, cropFirst.Height));
        Assert.Equal((2, 2), (resizeFirst.Width, resizeFirst.Height));
        Assert.Equal(["crop", "resize"], cropFirstApplied.Select(a => a.Op));
    }

    /// <summary>
    ///     Test that a border without a color takes the default of the call, and a color of its own wins.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadRecord_UsesDefaultColorUnlessGiven()
    {
        // Arrange
        using var input = Numbered(1, 1);
        var white = new Rgba32(255, 255, 255, 255);
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var defaulted = ImagePipeline.Apply(input, [new PadOperation(new PadSpec(1, 1, 1, 1), null)], white, out _);
        using var given = ImagePipeline.Apply(input, [new PadOperation(new PadSpec(1, 1, 1, 1), red)], white, out _);

        // Assert
        Assert.Equal(white, defaulted[0, 0]);
        Assert.Equal(red, given[0, 0]);
        Assert.Equal(input[0, 0], defaulted[1, 1]);
    }

    /// <summary>
    ///     Test that the input is neither modified nor disposed, and an empty list gives an independent copy.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_EmptyList_GivesIndependentCopyAndNeverTouchesInput()
    {
        // Arrange
        using var input = Numbered(3, 2);
        var before = input[0, 0];

        // Act
        using var copy = ImagePipeline.Apply(input, [], Transparent, out var applied);
        copy[0, 0] = new Rgba32(1, 2, 3, 4);

        // Assert
        Assert.Empty(applied);
        Assert.NotSame(input, copy);
        Assert.Equal(before, input[0, 0]);
    }

    /// <summary>
    ///     Test that a crop outside the image is a ViewerException with the usage code and the input stays usable.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropOutsideImage_ThrowsViewerExceptionAndKeepsInput()
    {
        // Arrange
        using var input = Numbered(3, 2);
        var operations = new IImageOperation[] { new InvertOperation(), new CropOperation(Pixels(9, 9, 1, 1)) };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("outside the 3x2 image", error.Message);
        Assert.Equal((byte)0, input[0, 0].R);
        Assert.Equal((byte)50, input[2, 1].R);
    }

    /// <summary>
    ///     Test that the filter names parse in any case and list the valid names for the caller to report.
    /// </summary>
    [Fact]
    public void ImagePipeline_TryParseFilter_NamesAnyCase_RoundTripThroughFilterName()
    {
        // Act / Assert: every listed name, in upper case
        foreach (var name in ImagePipeline.FilterNames)
        {
            Assert.True(ImagePipeline.TryParseFilter(name.ToUpperInvariant(), out var mode));
            Assert.Equal(name, ImagePipeline.FilterName(mode));
        }

        Assert.False(ImagePipeline.TryParseFilter("sharp", out var unknown));
        Assert.Equal(ResamplingMode.Auto, unknown);
        Assert.Equal(["auto", "nearest", "bilinear", "bicubic", "lanczos3", "area"], ImagePipeline.FilterNames);
    }

    /// <summary>
    ///     Test that the anchors of a model follow the pixels through a crop.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_AfterCrop_SubtractsTheOrigin()
    {
        // Arrange: an anchor at pixel 5,4 of a 10x8 image, and a crop that starts at 2,1
        using var input = Numbered(10, 8);
        var anchors = new[] { new SourceAnchor("tip", 1, 2, 3, 5, 4) };
        using var cropped = ImagePipeline.Apply(input, [new CropOperation(Pixels(2, 1, 6, 6))], Transparent, out var applied);

        // Act
        var mapped = ImagePipeline.MapAnchors(anchors, input.Width, input.Height, applied);

        // Assert
        var anchor = Assert.Single(mapped);
        Assert.Equal(3.0, anchor.PixelX);
        Assert.Equal(3.0, anchor.PixelY);
    }
}
