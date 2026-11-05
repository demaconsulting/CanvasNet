// cspell:ignore deskew
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the anchor mapping of the ImagePipeline unit: the pixel position of an anchor follows the
///     pixels through every step the pipeline reports, checked both against the reports (the arithmetic of each step)
///     and against real surfaces (where a marked block really lands).
/// </summary>
public class ImagePipelineAnchorTests
{
    /// <summary>The white used as background, fill and border in the sampling tests.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The color of the marked block.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Creates the report of one applied step.
    /// </summary>
    /// <param name="op">The operation name.</param>
    /// <param name="fields">The fields in order.</param>
    /// <returns>The report.</returns>
    private static AppliedOperation Step(string op, params (string Name, object Value)[] fields) =>
        SourceLoader.Operation(op, fields);

    /// <summary>
    ///     Maps one anchor through a list of steps.
    /// </summary>
    /// <param name="x">The horizontal position in the loaded image.</param>
    /// <param name="y">The vertical position in the loaded image.</param>
    /// <param name="width">The width of the loaded image.</param>
    /// <param name="height">The height of the loaded image.</param>
    /// <param name="steps">The steps.</param>
    /// <returns>The mapped anchor.</returns>
    private static SourceAnchor Map(double x, double y, int width, int height, params AppliedOperation[] steps) =>
        Assert.Single(ImagePipeline.MapAnchors([new SourceAnchor("a", 1, 2, 3, x, y)], width, height, steps));

    /// <summary>
    ///     Validates the arithmetic of the steps that translate or scale the position.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_CropTrimResizeFitPad_FollowTheirReports()
    {
        // Arrange / Act
        var crop = Map(100, 50, 400, 200, Step("crop", ("x", 20), ("y", 10), ("width", 300), ("height", 150), ("clamped", false)));
        var trim = Map(100, 50, 400, 200, Step("trim", ("tolerance", 8), ("x", 7), ("y", 3), ("width", 380), ("height", 190), ("trimmed", true), ("empty", false)));
        var resize = Map(100, 50, 400, 200, Step("resize", ("width", 200), ("height", 400), ("filter", "bilinear")));
        var fit = Map(100, 50, 400, 200, Step("fit", ("width", 100), ("height", 50), ("boxWidth", 100), ("boxHeight", 100), ("scaled", true)));
        var pad = Map(100, 50, 400, 200, Step("pad", ("left", 10), ("top", 20), ("right", 30), ("bottom", 40), ("width", 440), ("height", 260)));

        // Assert
        Assert.Equal((80.0, 40.0), (crop.PixelX!.Value, crop.PixelY!.Value));
        Assert.Equal((93.0, 47.0), (trim.PixelX!.Value, trim.PixelY!.Value));
        Assert.Equal((50.0, 100.0), (resize.PixelX!.Value, resize.PixelY!.Value));
        Assert.Equal((25.0, 12.5), (fit.PixelX!.Value, fit.PixelY!.Value));
        Assert.Equal((110.0, 70.0), (pad.PixelX!.Value, pad.PixelY!.Value));
    }

    /// <summary>
    ///     Validates the mirroring of the flip, on the axis the report names.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_Flip_MirrorsTheNamedAxis()
    {
        // Arrange / Act
        var horizontal = Map(100, 50, 400, 200, Step("flip", ("axis", "horizontal"), ("width", 400), ("height", 200)));
        var vertical = Map(100, 50, 400, 200, Step("flip", ("axis", "vertical"), ("width", 400), ("height", 200)));
        var both = Map(100, 50, 400, 200, Step("flip", ("axis", "both"), ("width", 400), ("height", 200)));

        // Assert
        Assert.Equal((300.0, 50.0), (horizontal.PixelX!.Value, horizontal.PixelY!.Value));
        Assert.Equal((100.0, 150.0), (vertical.PixelX!.Value, vertical.PixelY!.Value));
        Assert.Equal((300.0, 150.0), (both.PixelX!.Value, both.PixelY!.Value));
    }

    /// <summary>
    ///     Validates the right-angle rotations, clockwise, including the swap of the size.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_RightAngleRotate_IsAnExactMove()
    {
        // Arrange / Act
        var none = Map(100, 50, 400, 200, Step("rotate", ("degrees", 0), ("width", 400), ("height", 200)));
        var quarter = Map(100, 50, 400, 200, Step("rotate", ("degrees", 90), ("width", 200), ("height", 400)));
        var half = Map(100, 50, 400, 200, Step("rotate", ("degrees", 180), ("width", 400), ("height", 200)));
        var threeQuarters = Map(100, 50, 400, 200, Step("rotate", ("degrees", 270), ("width", 200), ("height", 400)));

        // Assert: no rounding noise at all for a right angle
        Assert.Equal((100.0, 50.0), (none.PixelX!.Value, none.PixelY!.Value));
        Assert.Equal((150.0, 100.0), (quarter.PixelX!.Value, quarter.PixelY!.Value));
        Assert.Equal((300.0, 150.0), (half.PixelX!.Value, half.PixelY!.Value));
        Assert.Equal((50.0, 300.0), (threeQuarters.PixelX!.Value, threeQuarters.PixelY!.Value));
    }

    /// <summary>
    ///     Validates that steps that only change colors leave the position alone, and that an empty list of steps
    ///     keeps every anchor as it was (the world point and the name included).
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_ColorOnlyStepsAndNoSteps_KeepThePosition()
    {
        // Arrange
        var grayscale = Step("grayscale", ("width", 400), ("height", 200));
        var grid = Step("grid", ("spacing", 10), ("width", 400), ("height", 200));
        var noSkew = Step("deskew", ("skewDegrees", 0.1), ("rotateDegrees", 0.0), ("confidence", 0.5), ("applied", false), ("width", 400), ("height", 200));

        // Act
        var mapped = Map(100.25, 50.5, 400, 200, grayscale, grid, noSkew);
        var untouched = ImagePipeline.MapAnchors([new SourceAnchor("keep", 1, 2, 3, 7, 8)], 400, 200, []);

        // Assert
        Assert.Equal(new SourceAnchor("a", 1, 2, 3, 100.25, 50.5), mapped);
        Assert.Equal(new SourceAnchor("keep", 1, 2, 3, 7, 8), Assert.Single(untouched));
    }

    /// <summary>
    ///     Validates that a position that cannot be followed becomes null: a deskew that corrected the image, and a
    ///     step that changed the size without being a known one. A null position stays null through later steps.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_StepsNotFollowed_LoseThePositionForGood()
    {
        // Arrange
        var deskew = Step("deskew", ("skewDegrees", 1.5), ("rotateDegrees", -1.5), ("confidence", 0.9), ("applied", true), ("width", 410), ("height", 210));
        var unknown = Step("future-warp", ("width", 300), ("height", 200));
        var later = Step("crop", ("x", 1), ("y", 1), ("width", 100), ("height", 100), ("clamped", false));

        // Act
        var afterDeskew = Map(100, 50, 400, 200, deskew, later);
        var afterUnknown = Map(100, 50, 400, 200, unknown);
        var alreadyNull = ImagePipeline.MapAnchors([new SourceAnchor("n", 0, 0, 0, null, null)], 400, 200, [later]);

        // Assert
        Assert.Null(afterDeskew.PixelX);
        Assert.Null(afterDeskew.PixelY);
        Assert.Null(afterUnknown.PixelX);
        Assert.Null(afterUnknown.PixelY);
        Assert.Null(Assert.Single(alreadyNull).PixelX);
    }

    /// <summary>
    ///     Validates that the mapping keeps every anchor in the list, in order, and does not modify the input.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_ManyAnchors_KeepsOrderAndInput()
    {
        // Arrange
        SourceAnchor[] anchors = [new("b", 0, 0, 0, 10, 10), new("a", 1, 1, 1, 20, 20), new("b", 2, 2, 2, 30, 30)];

        // Act
        var mapped = ImagePipeline.MapAnchors(anchors, 100, 100, [Step("pad", ("left", 5), ("top", 5), ("right", 0), ("bottom", 0), ("width", 105), ("height", 105))]);

        // Assert
        Assert.Equal(["b", "a", "b"], mapped.Select(a => a.Name));
        Assert.Equal([15.0, 25.0, 35.0], mapped.Select(a => a.PixelX!.Value));
        Assert.Equal(10.0, anchors[0].PixelX);
        Assert.Throws<ArgumentNullException>(() => ImagePipeline.MapAnchors(null!, 1, 1, []));
        Assert.Throws<ArgumentNullException>(() => ImagePipeline.MapAnchors(anchors, 1, 1, null!));
    }

    /// <summary>
    ///     Creates a white surface with a red 7 by 7 block whose top-left pixel is given.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="left">The left column of the block.</param>
    /// <param name="top">The top row of the block.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface WithBlock(int width, int height, int left, int top)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = x >= left && x < left + 7 && y >= top && y < top + 7 ? Red : White;
            }
        }

        return surface;
    }

    /// <summary>
    ///     Finds the center of the red pixels of a surface as continuous coordinates.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The mean of the pixel centers of the red pixels.</returns>
    private static (double X, double Y) RedCenter(Surface surface)
    {
        double sumX = 0, sumY = 0;
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var p = surface[x, y];
                if (p.R > 200 && p.G < 60 && p.B < 60)
                {
                    sumX += x + 0.5;
                    sumY += y + 0.5;
                    count++;
                }
            }
        }

        Assert.True(count > 0, "The marked block was lost.");
        return (sumX / count, sumY / count);
    }

    /// <summary>
    ///     Validates the mapping against real pixels: the center of a marked block lands, in the processed image, where
    ///     the mapped position says, within a pixel, for single steps, right angles, arbitrary angles of both signs and
    ///     a chain of steps.
    /// </summary>
    /// <param name="steps">The steps, as <c>name:value</c> items separated by a space.</param>
    [Theory]
    [InlineData("flip:h")]
    [InlineData("flip:v")]
    [InlineData("flip:both")]
    [InlineData("pad:5,3,2,4")]
    [InlineData("resize:150x90")]
    [InlineData("resize:60x70")]
    [InlineData("fit:50x50")]
    [InlineData("rotate:90")]
    [InlineData("rotate:180")]
    [InlineData("rotate:270")]
    [InlineData("rotate:30")]
    [InlineData("rotate:-30")]
    [InlineData("rotate:45")]
    [InlineData("rotate:100")]
    [InlineData("rotate:215.5")]
    [InlineData("flip:h pad:5,3,2,4 resize:150x90 rotate:30 fit:100x100")]
    [InlineData("rotate:-37 pad:10,10,10,10 flip:v rotate:90 resize:120x120")]
    public void ImagePipeline_MapAnchors_FollowsAMarkedBlockInARealImage(string steps)
    {
        // Arrange: a 100 x 60 image with the block off center; the anchor is the center of the block
        using var source = WithBlock(100, 60, 70, 10);
        var operations = steps.Split(' ').Select(Operation).ToList();
        var anchor = new SourceAnchor("block", 0, 0, 0, 73.5, 13.5);

        // Act
        using var result = ImagePipeline.Apply(source, operations, White, out var applied, White);
        var mapped = Assert.Single(ImagePipeline.MapAnchors([anchor], source.Width, source.Height, applied));
        var actual = RedCenter(result);

        // Assert
        Assert.InRange(actual.X, mapped.PixelX!.Value - 1.0, mapped.PixelX.Value + 1.0);
        Assert.InRange(actual.Y, mapped.PixelY!.Value - 1.0, mapped.PixelY.Value + 1.0);
    }

    /// <summary>
    ///     Creates the pipeline operation for a <c>name:value</c> item of a test.
    /// </summary>
    /// <param name="item">The item, for example <c>rotate:30</c> or <c>pad:5,3,2,4</c>.</param>
    /// <returns>The operation; filters are nearest so that a marked block stays red.</returns>
    private static IImageOperation Operation(string item)
    {
        var parts = item.Split(':');
        var value = parts[1];
        switch (parts[0])
        {
            case "flip":
                return new FlipOperation(value switch { "h" => FlipAxis.Horizontal, "v" => FlipAxis.Vertical, _ => FlipAxis.Both });
            case "pad":
                var sides = value.Split(',').Select(int.Parse).ToArray();
                return new PadOperation(new PadSpec(sides[0], sides[1], sides[2], sides[3]), White);
            case "resize":
                var size = value.Split('x').Select(int.Parse).ToArray();
                return new ResizeOperation(size[0], size[1], ResamplingMode.Nearest);
            case "fit":
                var box = value.Split('x').Select(int.Parse).ToArray();
                return new FitOperation(box[0], box[1], false, ResamplingMode.Nearest);
            default:
                return new RotateOperation(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture), White, ResamplingMode.Nearest);
        }
    }

    /// <summary>
    ///     Validates that the steps that were really applied by the pipeline and mapped from their reports keep an
    ///     anchor inside a blank image of the same size (a smoke test that every report carries the fields the mapping reads).
    /// </summary>
    [Fact]
    public void ImagePipeline_MapAnchors_RealReports_CarryTheFieldsTheMappingReads()
    {
        // Arrange
        using var source = new Surface(80, 40);
        var operations = new List<IImageOperation>
        {
            new GrayscaleOperation(),
            new TrimOperation(8),
            new PadOperation(new PadSpec(4, 4, 4, 4), White),
            new FlipOperation(FlipAxis.Horizontal),
            new RotateOperation(90),
            new ResizeOperation(40, 80, ResamplingMode.Nearest),
        };

        // Act
        using var result = ImagePipeline.Apply(source, operations, White, out var applied, White);
        var mapped = Assert.Single(ImagePipeline.MapAnchors([new SourceAnchor("a", 0, 0, 0, 40, 20)], 80, 40, applied));

        // Assert: every step was followed (the position is known) and is inside the final image
        Assert.NotNull(mapped.PixelX);
        Assert.InRange(mapped.PixelX.Value, 0, result.Width);
        Assert.InRange(mapped.PixelY!.Value, 0, result.Height);
    }
}
