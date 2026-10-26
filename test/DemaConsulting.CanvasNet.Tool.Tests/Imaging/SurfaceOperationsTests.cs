using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the box-fitting operations of the SurfaceOperations class.
/// </summary>
public class SurfaceOperationsTests
{
    /// <summary>
    ///     The margin color used by the fit tests.
    /// </summary>
    private static readonly Rgba32 Fill = new(10, 20, 30, 255);

    /// <summary>
    ///     The color of the images that are fitted.
    /// </summary>
    private static readonly Rgba32 Ink = new(200, 100, 50, 255);

    /// <summary>
    ///     Creates a surface of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Test that FitInside scales to the limiting side, rounds half away from zero, and clamps into the box.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="boxWidth">The width of the box.</param>
    /// <param name="boxHeight">The height of the box.</param>
    /// <param name="expectedWidth">The expected fitted width.</param>
    /// <param name="expectedHeight">The expected fitted height.</param>
    [Theory]
    [InlineData(100, 50, 20, 20, 20, 10)]
    [InlineData(50, 100, 20, 20, 10, 20)]
    [InlineData(10, 10, 40, 20, 20, 20)]
    [InlineData(3, 2, 10, 10, 10, 7)]
    [InlineData(5, 1, 3, 3, 3, 1)]
    [InlineData(1, 5000, 100, 100, 1, 100)]
    [InlineData(7, 7, 7, 7, 7, 7)]
    public void SurfaceOperations_FitInside_RoundsAndClamps(
        int width, int height, int boxWidth, int boxHeight, int expectedWidth, int expectedHeight)
    {
        // Arrange / Act: fit the image into the box
        var fitted = SurfaceOperations.FitInside(width, height, boxWidth, boxHeight);

        // Assert: the expected size (3x2 into 10x10 is 10 x 6.67, which rounds to 7)
        Assert.Equal((expectedWidth, expectedHeight), fitted);
    }

    /// <summary>
    ///     Test that FitInside rejects sizes below one.
    /// </summary>
    [Fact]
    public void SurfaceOperations_FitInside_NonPositiveSize_ThrowsArgumentOutOfRange()
    {
        // Arrange / Act / Assert: each argument is checked
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceOperations.FitInside(0, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceOperations.FitInside(1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceOperations.FitInside(1, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceOperations.FitInside(1, 1, 1, 0));
    }

    /// <summary>
    ///     Test that a contain fit scales to the limiting side, enlarging a small image, and centers it on the fill.
    /// </summary>
    [Fact]
    public void SurfaceOperations_FitInto_Contain_ScalesToLimitingSideAndCenters()
    {
        // Arrange: a 4x2 image and a 20x20 box
        using var source = Solid(4, 2, Ink);

        // Act
        using var fitted = SurfaceOperations.FitInto(source, 20, 20, FitMode.Contain, Fill, ResamplingMode.Nearest);

        // Assert: the image became 20x10 and sits in rows 5 to 14; the bands above and below are the fill
        Assert.Equal((20, 20), (fitted.Width, fitted.Height));
        Assert.Equal(Fill, fitted[0, 0]);
        Assert.Equal(Fill, fitted[19, 4]);
        Assert.Equal(Ink, fitted[0, 5]);
        Assert.Equal(Ink, fitted[19, 14]);
        Assert.Equal(Fill, fitted[10, 15]);
        Assert.Equal(Fill, fitted[19, 19]);
    }

    /// <summary>
    ///     Test that a stretch fit resamples to exactly the box.
    /// </summary>
    [Fact]
    public void SurfaceOperations_FitInto_Stretch_ResamplesToExactBox()
    {
        // Arrange: a 4x2 image and a 20x20 box
        using var source = Solid(4, 2, Ink);

        // Act
        using var fitted = SurfaceOperations.FitInto(source, 20, 20, FitMode.Stretch, Fill, ResamplingMode.Nearest);

        // Assert: every pixel is the image color; there is no margin
        Assert.Equal((20, 20), (fitted.Width, fitted.Height));
        Assert.Equal(Ink, fitted[0, 0]);
        Assert.Equal(Ink, fitted[19, 19]);
        Assert.Equal(Ink, fitted[10, 3]);
    }

    /// <summary>
    ///     Test that an image that already has the size of the box is returned as an equal copy in both modes.
    /// </summary>
    /// <param name="stretch"><see langword="true"/> for the stretch mode, <see langword="false"/> for contain.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SurfaceOperations_FitInto_SameSize_ReturnsEqualPixels(bool stretch)
    {
        // Arrange: a gradient that already has the size of the box
        var mode = stretch ? FitMode.Stretch : FitMode.Contain;
        using var source = FixtureFactory.CreateGradient(12, 8);

        // Act
        using var fitted = SurfaceOperations.FitInto(source, 12, 8, mode, Fill, ResamplingMode.Auto);

        // Assert: an independent copy with the same pixels
        Assert.NotSame(source, fitted);
        SurfaceAssert.Equal(source, fitted);
    }

    /// <summary>
    ///     Test that FitInto rejects a null source and an undefined mode.
    /// </summary>
    [Fact]
    public void SurfaceOperations_FitInto_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert
        using var source = Solid(2, 2, Ink);
        Assert.Throws<ArgumentNullException>(() => SurfaceOperations.FitInto(null!, 4, 4, FitMode.Contain, Fill, ResamplingMode.Auto));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceOperations.FitInto(source, 4, 4, (FitMode)99, Fill, ResamplingMode.Auto));
    }
}
