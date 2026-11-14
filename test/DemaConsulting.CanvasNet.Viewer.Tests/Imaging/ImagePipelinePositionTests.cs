// cspell:ignore deskew
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the position mapping of the ImagePipeline unit: the box and pixel of a feature follow one transform
///     composed over every step, the original corners are enclosed once, and a step that cannot be followed loses them.
/// </summary>
public class ImagePipelinePositionTests
{
    /// <summary>
    ///     Creates the report of one applied step.
    /// </summary>
    /// <param name="op">The operation name.</param>
    /// <param name="fields">The fields in order.</param>
    /// <returns>The report.</returns>
    private static AppliedOperation Step(string op, params (string Name, object Value)[] fields) =>
        SourceLoader.Operation(op, fields);

    /// <summary>
    ///     Maps one position (box 100,50 40x20, pixel at its center) through a list of steps.
    /// </summary>
    /// <param name="width">The width of the loaded image.</param>
    /// <param name="height">The height of the loaded image.</param>
    /// <param name="steps">The steps.</param>
    /// <returns>The mapped position.</returns>
    private static ImagePosition Map(int width, int height, params AppliedOperation[] steps)
    {
        var position = new ImagePosition("node", "a") { Box = new PositionBox(100, 50, 40, 20), Pixel = new PositionPoint(120, 60) };
        return Assert.Single(ImagePipeline.MapPositions([position], width, height, steps));
    }

    /// <summary>
    ///     Validates the steps that translate, scale or mirror the box and the pixel.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapPositions_CropTrimResizeFitPadFlip_FollowTheirReports()
    {
        // Arrange / Act
        var crop = Map(400, 200, Step("crop", ("x", 20), ("y", 10), ("width", 300), ("height", 150), ("clamped", false)));
        var trim = Map(400, 200, Step("trim", ("tolerance", 8), ("x", 7), ("y", 3), ("width", 380), ("height", 190), ("trimmed", true), ("empty", false)));
        var resize = Map(400, 200, Step("resize", ("width", 200), ("height", 400), ("filter", "bilinear")));
        var fit = Map(400, 200, Step("fit", ("width", 100), ("height", 50), ("boxWidth", 100), ("boxHeight", 100), ("scaled", true)));
        var pad = Map(400, 200, Step("pad", ("left", 10), ("top", 20), ("right", 30), ("bottom", 40), ("width", 440), ("height", 260)));
        var flip = Map(400, 200, Step("flip", ("axis", "horizontal"), ("width", 400), ("height", 200)));

        // Assert
        Assert.Equal((new PositionBox(80, 40, 40, 20), new PositionPoint(100, 50)), (crop.Box!.Value, crop.Pixel!.Value));
        Assert.Equal(new PositionBox(93, 47, 40, 20), trim.Box);
        Assert.Equal((new PositionBox(50, 100, 20, 40), new PositionPoint(60, 120)), (resize.Box!.Value, resize.Pixel!.Value));
        Assert.Equal(new PositionBox(25, 12.5, 10, 5), fit.Box);
        Assert.Equal(new PositionBox(110, 70, 40, 20), pad.Box);
        Assert.Equal((new PositionBox(260, 50, 40, 20), new PositionPoint(280, 60)), (flip.Box!.Value, flip.Pixel!.Value));
    }

    /// <summary>
    ///     Validates that a right-angle rotation is exact and swaps the sides of the box.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapPositions_RightAngleRotate_IsExact()
    {
        // Act
        var quarter = Map(400, 200, Step("rotate", ("degrees", 90), ("width", 200), ("height", 400)));

        // Assert: the anchor rule maps (120, 60) to (140, 120); the box turns into 20 x 40
        Assert.Equal(new PositionPoint(140, 120), quarter.Pixel);
        Assert.Equal(new PositionBox(130, 100, 20, 40), quarter.Box);
    }

    /// <summary>
    ///     Validates that an arbitrary rotation encloses the original corners once: rotating by 30 degrees and back is
    ///     the original box, where enclosing after each step would have grown it.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapPositions_RotateAndBack_EnclosesOnce()
    {
        // Act: the same size before and after, so the center stays and the two turns cancel
        var mapped = Map(
            400,
            400,
            Step("rotate", ("degrees", 30.0), ("width", 400), ("height", 400)),
            Step("rotate", ("degrees", -30.0), ("width", 400), ("height", 400)));

        // Assert
        var box = mapped.Box!.Value;
        Assert.Equal(100, box.X, 9);
        Assert.Equal(50, box.Y, 9);
        Assert.Equal(40, box.Width, 9);
        Assert.Equal(20, box.Height, 9);
        Assert.Equal(120, mapped.Pixel!.Value.X, 9);
    }

    /// <summary>
    ///     Validates that color steps keep the position, and that a deskew that corrected the image or an unknown step
    ///     that changed the size loses it for good.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapPositions_ColorStepsKeep_UnfollowedStepsLose()
    {
        // Arrange
        var grid = Step("grid", ("spacing", 10), ("width", 400), ("height", 200));
        var deskew = Step("deskew", ("skewDegrees", 2.0), ("rotateDegrees", -2.0), ("confidence", 0.9), ("applied", true), ("width", 410), ("height", 210));
        var unknown = Step("future", ("width", 300), ("height", 200));

        // Act
        var kept = Map(400, 200, grid);
        var deskewed = Map(400, 200, deskew, Step("pad", ("left", 1), ("top", 1), ("right", 1), ("bottom", 1), ("width", 412), ("height", 212)));
        var lost = Map(400, 200, unknown);

        // Assert
        Assert.Equal(new PositionBox(100, 50, 40, 20), kept.Box);
        Assert.Null(deskewed.Box);
        Assert.Null(deskewed.Pixel);
        Assert.Null(lost.Pixel);
        Assert.Equal("a", lost.Id);
    }

    /// <summary>
    ///     Validates the argument checks and that the input list keeps its order.
    /// </summary>
    [Fact]
    public void ImagePipeline_MapPositions_ManyPositions_KeepsOrder()
    {
        // Arrange
        ImagePosition[] positions = [new("node", "a") { Pixel = new PositionPoint(1, 1) }, new("edge", "b")];

        // Act
        var mapped = ImagePipeline.MapPositions(positions, 10, 10, [Step("pad", ("left", 5), ("top", 5), ("right", 0), ("bottom", 0), ("width", 15), ("height", 15))]);

        // Assert
        Assert.Equal(["a", "b"], mapped.Select(p => p.Id));
        Assert.Equal(new PositionPoint(6, 6), mapped[0].Pixel);
        Assert.Null(mapped[1].Box);
        Assert.Throws<ArgumentNullException>(() => ImagePipeline.MapPositions(null!, 1, 1, []));
    }
}
