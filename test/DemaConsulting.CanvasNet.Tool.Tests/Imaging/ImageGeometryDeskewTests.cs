// cspell:ignore deskew downscales
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the skew estimation of the ImageGeometry class.
/// </summary>
public class ImageGeometryDeskewTests
{
    /// <summary>
    ///     The largest error in degrees accepted for the estimate of a clean synthetic picture.
    /// </summary>
    private const double Tolerance = 0.15;

    /// <summary>
    ///     Creates a surface of pseudo-random gray noise from a fixed seed.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The surface.</returns>
    private static Surface Noise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var level = (byte)random.Next(256);
                surface[x, y] = new Rgba32(level, level, level, 255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Finds the first row from the top of a column that holds ink.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The column.</param>
    /// <returns>The first row whose pixel is darker than 150.</returns>
    private static int FirstDarkRow(Surface surface, int x)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            if (surface[x, y].R < 150)
            {
                return y;
            }
        }

        throw new InvalidOperationException($"Column {x} has no ink");
    }

    /// <summary>
    ///     Verifies that the tilt of ruled lines is found within a tolerance, with a high confidence.
    /// </summary>
    /// <param name="tilt">The tilt of the lines in degrees; positive descends to the right.</param>
    [Theory]
    [InlineData(-12.0)]
    [InlineData(-3.0)]
    [InlineData(0.0)]
    [InlineData(2.5)]
    [InlineData(7.0)]
    [InlineData(14.5)]
    public void ImageGeometry_EstimateSkew_TiltedLines_FindsAngleWithinTolerance(double tilt)
    {
        using var picture = FixtureFactory.SketchPhoto(400, 300, tilt, lines: 8);

        var estimate = ImageGeometry.EstimateSkew(picture);

        Assert.True(estimate.Estimated);
        Assert.InRange(estimate.Degrees, tilt - Tolerance, tilt + Tolerance);
        Assert.True(estimate.Confidence >= 2 * ImageGeometry.MinConfidence, $"The confidence is {estimate.Confidence}");
    }

    /// <summary>
    ///     Verifies that lines that descend to the right have a positive skew and lines that ascend a negative one.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_SignConvention_DescendingToRightIsPositive()
    {
        using var descending = FixtureFactory.SketchPhoto(400, 300, 5, lines: 6);
        using var ascending = FixtureFactory.SketchPhoto(400, 300, -5, lines: 6);

        // The line at the left of a descending picture is higher than at the right: check the drawing itself first.
        Assert.True(FirstDarkRow(descending, 0) < FirstDarkRow(descending, 399));
        Assert.True(FirstDarkRow(ascending, 0) > FirstDarkRow(ascending, 399));

        Assert.True(ImageGeometry.EstimateSkew(descending).Degrees > 4.5);
        Assert.True(ImageGeometry.EstimateSkew(ascending).Degrees < -4.5);
    }

    /// <summary>
    ///     Verifies that a large image is analyzed on a smaller plane and still gives the angle.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_LargeImage_DownscalesAndStillFindsAngle()
    {
        // 3000 pixels on a side of the longest means a block of 3 by 3; thick lines survive the averaging.
        using var picture = FixtureFactory.SketchPhoto(3000, 2000, 4, lines: 8, thickness: 6);

        var estimate = ImageGeometry.EstimateSkew(picture);

        Assert.True(estimate.Estimated);
        Assert.InRange(estimate.Degrees, 4 - Tolerance, 4 + Tolerance);
        Assert.True(estimate.Confidence >= 2 * ImageGeometry.MinConfidence);
    }

    /// <summary>
    ///     Verifies that a strong lighting gradient across the paper does not hide the lines.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_LightGroundUnevenLighting_StillFindsAngle()
    {
        // The paper goes from 160 on the left to 240 on the right; the ink is always 70 levels darker than the paper.
        using var picture = FixtureFactory.SketchPhoto(400, 300, -6, lines: 8, thickness: 2, paperLevel: 200, inkContrast: 70, shade: 80);

        var estimate = ImageGeometry.EstimateSkew(picture);

        Assert.True(estimate.Estimated);
        Assert.InRange(estimate.Degrees, -6 - Tolerance, -6 + Tolerance);
        Assert.True(estimate.Confidence >= 2 * ImageGeometry.MinConfidence);
    }

    /// <summary>
    ///     Verifies that an image without dark marks, or with too few of them, is not estimated.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_BlankImage_IsNotEstimated()
    {
        using var blank = new Surface(400, 300);
        blank.Clear(new Rgba32(200, 200, 200, 255));

        Assert.Equal(new SkewEstimate(0, 0, false), ImageGeometry.EstimateSkew(blank));

        // 30 dark pixels are fewer than the 100 that are needed.
        using var speck = new Surface(400, 300);
        speck.Clear(new Rgba32(200, 200, 200, 255));
        for (var x = 100; x < 130; x++)
        {
            speck[x, 150] = new Rgba32(20, 20, 20, 255);
        }

        Assert.False(ImageGeometry.EstimateSkew(speck).Estimated);

        using var transparent = new Surface(50, 50);
        Assert.Equal(new SkewEstimate(0, 0, false), ImageGeometry.EstimateSkew(transparent));
    }

    /// <summary>
    ///     Verifies that a pixel exactly 10 levels below its window mean is a dark mark, and one 9 levels below is not.
    /// </summary>
    /// <param name="markLevel">The level of the one mark pixel of each period of 17 columns.</param>
    /// <param name="brighterCount">How many of the other 16 pixels of the period are 111 rather than 110.</param>
    /// <param name="expectedEstimated">Whether enough marks are found for the skew to be estimated.</param>
    [Theory]
    [InlineData(100, 10, true)]
    [InlineData(101, 9, false)]
    public void ImageGeometry_EstimateSkew_MarkContrastAtThreshold_IsMarkOnlyWhenAtLeastTenLevelsDarker(
        int markLevel,
        int brighterCount,
        bool expectedEstimated)
    {
        // Arrange: identical rows of a period of 17 columns, so every window of 17 by 17 holds exactly one period
        // per row and its mean is exactly 110 (the sum of the period is 17 * 110 = 1870); the mark is then
        // exactly 10 (or 9) levels below the mean of its window, and no other pixel is a mark
        using var picture = new Surface(340, 20);
        for (var x = 0; x < picture.Width; x++)
        {
            var phase = x % 17;
            var level = 110;
            if (phase == 0)
            {
                level = markLevel;
            }
            else if (phase <= brighterCount)
            {
                level = 111;
            }
            for (var y = 0; y < picture.Height; y++)
            {
                picture[x, y] = new Rgba32((byte)level, (byte)level, (byte)level, 255);
            }
        }

        // Act: estimate the skew, which needs at least 100 marks
        var estimate = ImageGeometry.EstimateSkew(picture);

        // Assert: the 380 marks at exactly 10 levels are found, the none at 9 levels are not
        Assert.Equal(expectedEstimated, estimate.Estimated);
    }

    /// <summary>
    ///     Verifies that random noise has no line structure, so its confidence is below the confidence the pipeline needs.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_RandomNoise_HasLowConfidence()
    {
        foreach (var (width, height) in new[] { (400, 300), (800, 800), (1000, 250), (300, 900) })
        {
            using var noise = Noise(width, height, 7);

            var estimate = ImageGeometry.EstimateSkew(noise);

            Assert.True(estimate.Estimated);
            Assert.True(
                estimate.Confidence < ImageGeometry.MinConfidence,
                $"The confidence of {width}x{height} noise is {estimate.Confidence}");
        }
    }

    /// <summary>
    ///     Verifies that the same picture gives the same estimate every time.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_SameInputTwice_ReturnsIdenticalResult()
    {
        using var picture = FixtureFactory.SketchPhoto(500, 400, 3.35, lines: 7, thickness: 3, shade: 40);
        using var copy = picture.Crop(0, 0, picture.Width, picture.Height);

        var first = ImageGeometry.EstimateSkew(picture);
        var second = ImageGeometry.EstimateSkew(picture);
        var fromCopy = ImageGeometry.EstimateSkew(copy);

        Assert.Equal(first, second);
        Assert.Equal(first, fromCopy);
        Assert.True(first.Estimated);
    }

    /// <summary>
    ///     Verifies the tie-break between equal scores: the smaller absolute angle, then the more negative one.
    /// </summary>
    [Fact]
    public void ImageGeometry_EstimateSkew_EqualScores_PrefersSmallerAbsoluteAngle()
    {
        // A higher score always wins.
        Assert.True(ImageGeometry.PreferCandidate(101, 900, 100, 0));
        Assert.False(ImageGeometry.PreferCandidate(99, 0, 100, 900));

        // Equal scores: the angle closer to 0 wins.
        Assert.True(ImageGeometry.PreferCandidate(100, 50, 100, 200));
        Assert.True(ImageGeometry.PreferCandidate(100, -50, 100, 200));
        Assert.False(ImageGeometry.PreferCandidate(100, 200, 100, 50));
        Assert.False(ImageGeometry.PreferCandidate(100, -200, 100, 50));

        // Equal scores at the same distance: the smaller (more negative) angle wins.
        Assert.True(ImageGeometry.PreferCandidate(100, -50, 100, 50));
        Assert.False(ImageGeometry.PreferCandidate(100, 50, 100, -50));

        // The same angle is not better than itself.
        Assert.False(ImageGeometry.PreferCandidate(100, 50, 100, 50));
    }
}
