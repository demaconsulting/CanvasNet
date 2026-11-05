// cspell:ignore canvasnet deskew lanczos
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the validation of operation records by the ImagePipeline unit: a record built by hand with a
///     value that makes no sense is refused with the stable codes of the command line, before any operation runs.
/// </summary>
public class ImagePipelineValidationTests
{
    /// <summary>
    ///     The fully transparent color, the default border of the tests.
    /// </summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     The color of the test image.
    /// </summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     Creates a length in pixels.
    /// </summary>
    /// <param name="value">The number of pixels.</param>
    /// <returns>The length.</returns>
    private static Dimension Px(double value)
    {
        return new Dimension(value, false);
    }

    /// <summary>
    ///     Creates a length in percent.
    /// </summary>
    /// <param name="value">The percentage.</param>
    /// <returns>The length.</returns>
    private static Dimension Pct(double value)
    {
        return new Dimension(value, true);
    }

    /// <summary>
    ///     Creates a crop with a region.
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The operation.</returns>
    private static CropOperation Crop(Dimension x, Dimension y, Dimension width, Dimension height)
    {
        return new CropOperation(new GeometrySpec(x, y, width, height));
    }

    /// <summary>
    ///     Creates the operation with an invalid value that a case name stands for.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <returns>The operation.</returns>
    private static IImageOperation InvalidOperation(string name)
    {
        return name switch
        {
            "resize-zero" => new ResizeOperation(0, 0, ResamplingMode.Auto),
            "resize-negative-width" => new ResizeOperation(-1, 5, ResamplingMode.Auto),
            "resize-undefined-filter" => new ResizeOperation(5, 5, (ResamplingMode)99),
            "fit-zero-width" => new FitOperation(0, 10, false, ResamplingMode.Auto),
            "fit-negative-height" => new FitOperation(10, -3, true, ResamplingMode.Auto),
            "fit-undefined-filter" => new FitOperation(10, 10, true, (ResamplingMode)99),
            "pad-negative-side" => new PadOperation(new PadSpec(1, 2, -1, 4), null),
            "grid-too-small" => new GridOperation(7, null),
            "grid-too-large" => new GridOperation(4097, null),
            "flip-undefined-axis" => new FlipOperation((FlipAxis)99),
            "rotate-nan" => new RotateOperation(double.NaN),
            "rotate-infinity" => new RotateOperation(double.PositiveInfinity),
            "rotate-undefined-filter" => new RotateOperation(10, null, (ResamplingMode)99),
            "deskew-undefined-filter" => new DeskewOperation(null, (ResamplingMode)99),
            "threshold-negative" => new ThresholdOperation(-1),
            "threshold-too-large" => new ThresholdOperation(256),
            "levels-black-equals-white" => new LevelsOperation(255, 255, 1),
            "levels-black-above-white" => new LevelsOperation(100, 50, 1),
            "levels-equal" => new LevelsOperation(10, 10, 1),
            "levels-gamma-too-small" => new LevelsOperation(0, 255, 0.05),
            "levels-gamma-too-large" => new LevelsOperation(0, 255, 11),
            "levels-gamma-nan" => new LevelsOperation(0, 255, double.NaN),
            "brightness-too-small" => new BrightnessOperation(-256),
            "brightness-too-large" => new BrightnessOperation(256),
            "contrast-nan" => new ContrastOperation(double.NaN),
            "contrast-too-large" => new ContrastOperation(101),
            "auto-levels-negative" => new AutoLevelsOperation(-1),
            "auto-levels-too-large" => new AutoLevelsOperation(20.1),
            "auto-levels-nan" => new AutoLevelsOperation(double.NaN),
            "trim-negative" => new TrimOperation(-1),
            "trim-too-large" => new TrimOperation(256),
            "crop-zero-width" => Crop(Px(0), Px(0), Px(0), Px(5)),
            "crop-negative-height" => Crop(Px(0), Px(0), Px(5), Px(-1)),
            "crop-nan-x" => Crop(Px(double.NaN), Px(0), Px(5), Px(5)),
            "crop-negative-x" => Crop(Px(-1), Px(0), Px(5), Px(5)),
            "crop-percent-above-100" => Crop(Px(0), Px(0), Pct(101), Px(5)),
            "crop-default-region" => new CropOperation(default),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown case.")
        };
    }

    /// <summary>
    ///     Test that an operation with an invalid value is a usage error naming its position and operation.
    /// </summary>
    /// <param name="name">The case name, see <see cref="InvalidOperation"/>.</param>
    [Theory]
    [InlineData("resize-zero")]
    [InlineData("resize-negative-width")]
    [InlineData("resize-undefined-filter")]
    [InlineData("fit-zero-width")]
    [InlineData("fit-negative-height")]
    [InlineData("fit-undefined-filter")]
    [InlineData("pad-negative-side")]
    [InlineData("grid-too-small")]
    [InlineData("grid-too-large")]
    [InlineData("flip-undefined-axis")]
    [InlineData("rotate-nan")]
    [InlineData("rotate-infinity")]
    [InlineData("rotate-undefined-filter")]
    [InlineData("deskew-undefined-filter")]
    [InlineData("threshold-negative")]
    [InlineData("threshold-too-large")]
    [InlineData("levels-black-equals-white")]
    [InlineData("levels-black-above-white")]
    [InlineData("levels-equal")]
    [InlineData("levels-gamma-too-small")]
    [InlineData("levels-gamma-too-large")]
    [InlineData("levels-gamma-nan")]
    [InlineData("brightness-too-small")]
    [InlineData("brightness-too-large")]
    [InlineData("contrast-nan")]
    [InlineData("contrast-too-large")]
    [InlineData("auto-levels-negative")]
    [InlineData("auto-levels-too-large")]
    [InlineData("auto-levels-nan")]
    [InlineData("trim-negative")]
    [InlineData("trim-too-large")]
    [InlineData("crop-zero-width")]
    [InlineData("crop-negative-height")]
    [InlineData("crop-nan-x")]
    [InlineData("crop-negative-x")]
    [InlineData("crop-percent-above-100")]
    [InlineData("crop-default-region")]
    public void ImagePipeline_Apply_InvalidOperationValue_ThrowsUsageError(string name)
    {
        // Arrange
        using var input = ViewerFixtures.Solid(16, 16, Blue);
        var operation = InvalidOperation(name);

        // Act
        var error = Assert.Throws<ViewerException>(() => ImagePipeline.Apply(input, [operation], Transparent, out _));

        // Assert: the stable code, the position and name of the operation, and a hint
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.StartsWith($"Operation 1 ({operation.Name}): ", error.Message);
        Assert.EndsWith(".", error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.Hint));
    }

    /// <summary>
    ///     Test that a size or border beyond the maximum is a size-limit error.
    /// </summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [InlineData("resize")]
    [InlineData("fit")]
    [InlineData("pad")]
    public void ImagePipeline_Apply_SizeBeyondMaxDimension_ThrowsSizeLimit(string name)
    {
        // Arrange: one more than the maximum
        using var input = ViewerFixtures.Solid(16, 16, Blue);
        var tooLarge = Surface.MaxDimension + 1;
        IImageOperation operation = name switch
        {
            "resize" => new ResizeOperation(tooLarge, 1, ResamplingMode.Auto),
            "fit" => new FitOperation(1, tooLarge, true, ResamplingMode.Auto),
            _ => new PadOperation(new PadSpec(tooLarge, 0, 0, 0), null)
        };

        // Act
        var error = Assert.Throws<ViewerException>(() => ImagePipeline.Apply(input, [operation], Transparent, out _));

        // Assert: the size-limit code, and the maximum in the message
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.StartsWith($"Operation 1 ({operation.Name}): ", error.Message);
        Assert.Contains(Surface.MaxDimension.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message);
        Assert.False(string.IsNullOrWhiteSpace(error.Hint));
    }

    /// <summary>
    ///     Test that an invalid operation after valid ones fails before any operation runs, and the input stays usable.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_InvalidOperationAfterValidOnes_FailsBeforeAnyWork()
    {
        // Arrange: an invert that would change the input if it ran, then a resize to nothing
        using var input = ViewerFixtures.Solid(16, 16, Blue);
        IImageOperation[] operations = [new InvertOperation(), new ResizeOperation(0, 5, ResamplingMode.Auto)];

        // Act
        var error = Assert.Throws<ViewerException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert: the second operation is named, with the problem and the range
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal("Operation 2 (resize): the width must be at least 1 pixel, not 0.", error.Message);

        // The input is neither disposed nor changed
        Assert.Equal(Blue, input[0, 0]);
        Assert.Equal((16, 16), (input.Width, input.Height));
    }

    /// <summary>
    ///     Test that a missing operation in the list is a caller error naming its position.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_NullOperationElement_ThrowsArgumentNullException()
    {
        // Arrange: a list whose second element is missing
        using var input = ViewerFixtures.Solid(16, 16, Blue);
        IImageOperation[] operations = [new InvertOperation(), null!];

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert
        Assert.Equal("operations", error.ParamName);
        Assert.Contains("2", error.Message);
    }

    /// <summary>
    ///     Test that the values at the ends of their ranges are accepted, so the validation is not stricter than the
    ///     command line.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_BoundaryValues_AreAccepted()
    {
        // Arrange: the smallest and largest valid value of each range, one operation per entry
        using var input = ViewerFixtures.Solid(16, 16, Blue);
        IImageOperation[] operations =
        [
            new ResizeOperation(1, 1, ResamplingMode.Auto),
            new FitOperation(1, 1, false, ResamplingMode.Nearest),
            new GridOperation(GridOverlay.MinSpacing, null),
            new GridOperation(GridOverlay.MaxSpacing, null),
            new LevelsOperation(0, 1, Surface.MinGamma),
            new LevelsOperation(254, 255, Surface.MaxGamma),
            new BrightnessOperation(255),
            new BrightnessOperation(-255),
            new ContrastOperation(100),
            new ContrastOperation(-100),
            new AutoLevelsOperation(0),
            new AutoLevelsOperation(Surface.MaxAutoLevelsClipPercent),
            new TrimOperation(0),
            new TrimOperation(Surface.MaxTrimTolerance),
            new ThresholdOperation(0),
            new ThresholdOperation(255),
            new ThresholdOperation(null),
            new PadOperation(new PadSpec(0, 0, 0, 0), null),
            new RotateOperation(-90),
            new RotateOperation(450),
            new RotateOperation(1e6),
            new FlipOperation(FlipAxis.Both),
            new DeskewOperation(null, ResamplingMode.Lanczos3),
            Crop(Px(0), Px(0), Pct(100), Pct(100)),
            Crop(Pct(0), Pct(0), Px(1), Px(1))
        ];

        // Act / Assert: each operation alone is applied, and none is refused
        foreach (var operation in operations)
        {
            using var result = ImagePipeline.Apply(input, [operation], Transparent, out var applied);
            Assert.Single(applied);
        }
    }
}
