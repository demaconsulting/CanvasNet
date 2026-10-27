// cspell:ignore lanczos canvasnet zabcz zdefz zabczz zdefzz ccdd fghij eeff deskew deskewing deskewed grayscale otsu luma nocolor
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImagePipeline unit: parsing the ordered operations of a command line and applying them to
///     tiny hand-built surfaces with exact pixel expectations.
/// </summary>
public class ImagePipelineTests
{
    /// <summary>
    ///     The color of a pixel that is a border in the tests: red, whose letter in <see cref="Letters"/> is <c>z</c>.
    /// </summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     The fully transparent color.
    /// </summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     Creates an image whose pixels are numbered <c>a</c> (0), <c>b</c> (1), and so on in reading order.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface; the letter of a pixel is its number, and its red channel is ten times the number.</returns>
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
    ///     Renders a surface made of <see cref="Numbered"/> pixels as letters, one line per row, separated by slashes.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The text, for example <c>abc/def</c>; the red border color is <c>z</c>.</returns>
    private static string Letters(Surface surface)
    {
        var rows = new List<string>();
        for (var y = 0; y < surface.Height; y++)
        {
            var row = new char[surface.Width];
            for (var x = 0; x < surface.Width; x++)
            {
                row[x] = (char)('a' + (surface[x, y].R / 10));
            }

            rows.Add(new string(row));
        }

        return string.Join('/', rows);
    }

    /// <summary>
    ///     Parses command-line options into operations, as the render command does.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The operations.</returns>
    private static IReadOnlyList<IImageOperation> Parse(params string[] args)
    {
        return ImagePipeline.Parse(ArgumentReader.Parse("render", args, RenderCommand.OptionTable), null, null);
    }

    /// <summary>
    ///     Parses command-line options, then applies the operations to a surface.
    /// </summary>
    /// <param name="input">The surface.</param>
    /// <param name="args">The pipeline options.</param>
    /// <returns>The result surface, which the caller disposes.</returns>
    private static Surface Run(Surface input, params string[] args)
    {
        return ImagePipeline.Apply(input, Parse(args), Transparent, out _);
    }

    /// <summary>
    ///     Test that a rotation by 90 degrees of a non-square image turns the top row into the right column.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Rotate90_TurnsClockwiseAndSwapsSize()
    {
        // Arrange: a 3x2 image
        using var input = Numbered(3, 2);

        // Act: rotate it by 90 degrees
        using var result = Run(input, "--rotate", "90");

        // Assert: 2x3, with the left column of the input (a, d) as the top row, read bottom to top
        Assert.Equal((2, 3), (result.Width, result.Height));
        Assert.Equal("da/eb/fc", Letters(result));
    }

    /// <summary>
    ///     Test that a rotation by 180 degrees reverses both axes and keeps the size.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Rotate180_ReversesBothAxes()
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--rotate", "180");

        // Assert
        Assert.Equal("fed/cba", Letters(result));
    }

    /// <summary>
    ///     Test that a rotation by 270 degrees turns counter-clockwise and swaps the size.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Rotate270_TurnsCounterClockwiseAndSwapsSize()
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--rotate", "270");

        // Assert: 2x3, the top row of the input (a, b, c) is the left column, read bottom to top
        Assert.Equal((2, 3), (result.Width, result.Height));
        Assert.Equal("cf/be/ad", Letters(result));
    }

    /// <summary>
    ///     Test that a negative angle turns the other way and that four quarter turns give the original.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_NegativeAngleAndFullTurn_AreEquivalentRotations()
    {
        // Arrange
        using var input = Numbered(3, 2);

        // Act: -90 is 270; four times 90 is the identity
        using var counter = Run(input, "--rotate", "-90");
        using var full = Run(input, "--rotate", "90", "--rotate", "90", "--rotate", "90", "--rotate", "90");

        // Assert
        Assert.Equal("cf/be/ad", Letters(counter));
        Assert.Equal("abc/def", Letters(full));
    }

    /// <summary>
    ///     Test that each flip axis mirrors the right way.
    /// </summary>
    /// <param name="axis">The axis argument.</param>
    /// <param name="expected">The expected result of flipping <c>abc/def</c>.</param>
    [Theory]
    [InlineData("h", "cba/fed")]
    [InlineData("horizontal", "cba/fed")]
    [InlineData("v", "def/abc")]
    [InlineData("vertical", "def/abc")]
    [InlineData("both", "fed/cba")]
    public void ImagePipeline_Apply_Flip_MirrorsAboutTheAxis(string axis, string expected)
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--flip", axis);

        // Assert: same size, mirrored pixels
        Assert.Equal((3, 2), (result.Width, result.Height));
        Assert.Equal(expected, Letters(result));
    }

    /// <summary>
    ///     Test that one border width pads every side with the given color.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadOneValue_AddsBorderOnEverySide()
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--pad", "1", "--pad-color", "red");

        // Assert: the image sits at 1,1 inside a red border
        Assert.Equal((5, 4), (result.Width, result.Height));
        Assert.Equal("zzzzz/zabcz/zdefz/zzzzz", Letters(result));
    }

    /// <summary>
    ///     Test that four border widths are the left, top, right, and bottom.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadFourValues_UsesLeftTopRightBottom()
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--pad", "1,0,2,3", "--pad-color", "#ff0000");

        // Assert: 1 left, 0 top, 2 right, 3 bottom
        Assert.Equal((6, 5), (result.Width, result.Height));
        Assert.Equal("zabczz/zdefzz/zzzzzz/zzzzzz/zzzzzz", Letters(result));
    }

    /// <summary>
    ///     Test that a border without a color of its own takes the default of the pipeline, and a given color wins.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadColor_DefaultComesFromCallerAndOptionOverridesIt()
    {
        // Arrange
        using var input = Numbered(1, 1);
        var plain = Parse("--pad", "1");
        var colored = Parse("--pad", "1", "--pad-color", "red");

        // Act: apply with a white default
        var white = new Rgba32(255, 255, 255, 255);
        using var defaulted = ImagePipeline.Apply(input, plain, white, out var defaultedOps);
        using var overridden = ImagePipeline.Apply(input, colored, white, out _);

        // Assert: white without the option (also reported), red with it, and the image itself unchanged in the middle
        Assert.Equal(white, defaulted[0, 0]);
        Assert.Equal(Red, overridden[0, 0]);
        Assert.Equal(input[0, 0], defaulted[1, 1]);
        Assert.Contains(new KeyValuePair<string, object>("color", "#FFFFFF"), Assert.Single(defaultedOps).Fields);
    }

    /// <summary>
    ///     Test that a transparent border stays fully transparent.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadTransparent_LeavesBorderTransparent()
    {
        // Arrange / Act
        using var input = Numbered(2, 2);
        using var result = Run(input, "--pad", "1", "--pad-color", "transparent");

        // Assert
        Assert.Equal(Transparent, result[0, 0]);
        Assert.Equal(Transparent, result[3, 3]);
        Assert.Equal(input[1, 1], result[2, 2]);
    }

    /// <summary>
    ///     Test that a semi-transparent border color is stored as given (straight alpha).
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadSemiTransparentColor_KeepsStraightAlpha()
    {
        // Arrange / Act
        using var input = Numbered(1, 1);
        using var result = Run(input, "--pad", "1", "--pad-color", "#11223380");

        // Assert
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x80), result[0, 0]);
    }

    /// <summary>
    ///     Test that a crop in pixels cuts out the rectangle, in both geometry forms.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    /// <param name="expected">The expected letters.</param>
    [Theory]
    [InlineData("1,0,2,2", "bc/ef")]
    [InlineData("2x1+1+1", "ef")]
    [InlineData("1px,1px,1px,1px", "e")]
    [InlineData("3x2", "abc/def")]
    [InlineData("2x2", "ab/de")]
    public void ImagePipeline_Apply_CropPixels_CutsOutTheRectangle(string geometry, string expected)
    {
        // Arrange / Act
        using var input = Numbered(3, 2);
        using var result = Run(input, "--crop", geometry);

        // Assert
        Assert.Equal(expected, Letters(result));
    }

    /// <summary>
    ///     Test that percentages are of the image at that point of the pipeline.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropPercent_IsRelativeToTheImage()
    {
        // Arrange: a 4x4 image a..p
        using var input = Numbered(4, 4);

        // Act: the bottom-right quarter, and (after a crop to the left half) a percentage of the half
        using var quarter = Run(input, "--crop", "50%,50%,50%,50%");
        using var halfOfLeftHalf = Run(input, "--crop", "0,0,50%,100%", "--crop", "0,0,100%,50%");

        // Assert
        Assert.Equal("kl/op", Letters(quarter));
        Assert.Equal("ab/ef", Letters(halfOfLeftHalf));
    }

    /// <summary>
    ///     Test that percentage edges round half away from zero, so that two halves tile the image.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropPercentHalfPixel_RoundsEdgesAwayFromZero()
    {
        // Arrange: 3 pixels wide, where 50% is 1.5
        using var input = Numbered(3, 2);

        // Act: the left half and the right half
        using var left = Run(input, "--crop", "0,0,50%,100%");
        using var right = Run(input, "--crop", "50%,0,50%,100%");

        // Assert: the edge at 1.5 is 2, so the halves are 2 and 1 pixels wide and tile the image
        Assert.Equal("ab/de", Letters(left));
        Assert.Equal("c/f", Letters(right));
    }

    /// <summary>
    ///     Test that a crop that extends past an edge is cut off at the edge and reported as clamped.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropPastEdge_IsClampedAndReported()
    {
        // Arrange
        using var input = Numbered(3, 2);

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--crop", "2,0,5,5"), Transparent, out var applied);

        // Assert: only the last column remains, and the operation says what it really did
        Assert.Equal("c/f", Letters(result));
        var crop = Assert.Single(applied);
        Assert.Equal("crop", crop.Op);
        KeyValuePair<string, object>[] expected =
        [
            new("x", 2),
            new("y", 0),
            new("width", 1),
            new("height", 2),
            new("clamped", true)
        ];
        Assert.Equal(expected, crop.Fields);
    }

    /// <summary>
    ///     Test that a crop that starts outside the image is a usage error that names the image size.
    /// </summary>
    /// <param name="geometry">The geometry.</param>
    [Theory]
    [InlineData("3,0,1,1")]
    [InlineData("0,2,1,1")]
    [InlineData("100,100,5,5")]
    public void ImagePipeline_Apply_CropOutside_ThrowsUsageError(string geometry)
    {
        // Arrange
        using var input = Numbered(3, 2);
        var operations = Parse("--crop", geometry);

        // Act
        var error = Assert.Throws<ToolException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert: a usage error with the size of the image, and the input is untouched
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("outside the 3x2 image", error.Message);
        Assert.Equal("abc/def", Letters(input));
    }

    /// <summary>
    ///     Test that a percentage region that rounds to nothing is a usage error.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_CropRoundsToNothing_ThrowsUsageError()
    {
        // Arrange: 0.1% of 3 pixels is 0.003 pixels
        using var input = Numbered(3, 2);
        var operations = Parse("--crop", "0,0,0.1%,50%");

        // Act
        var error = Assert.Throws<ToolException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains("smaller than one pixel", error.Message);
    }

    /// <summary>
    ///     Test that nearest-neighbor resizing replicates pixels exactly.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ResizeNearest_ReplicatesPixels()
    {
        // Arrange: 2x2 a b / c d
        using var input = Numbered(2, 2);

        // Act: double it with the nearest filter
        using var result = Run(input, "--resize", "4x4", "--filter", "nearest");

        // Assert
        Assert.Equal("aabb/aabb/ccdd/ccdd", Letters(result));
    }

    /// <summary>
    ///     Test that shrinking with the area filter averages the pixels.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ResizeArea_AveragesPixels()
    {
        // Arrange: 2x2 with red channels 0, 10, 20, 30
        using var input = Numbered(2, 2);

        // Act: shrink to one pixel by area averaging and by the default (which is area when shrinking)
        using var area = Run(input, "--resize", "1x1", "--filter", "area");
        using var auto = Run(input, "--resize", "1x1");

        // Assert: the mean is 15
        Assert.InRange(area[0, 0].R, 14, 16);
        Assert.Equal(area[0, 0], auto[0, 0]);
    }

    /// <summary>
    ///     Test that every filter resizes to the exact size and keeps a flat color flat.
    /// </summary>
    /// <param name="filter">The filter name.</param>
    [Theory]
    [InlineData("auto")]
    [InlineData("nearest")]
    [InlineData("bilinear")]
    [InlineData("bicubic")]
    [InlineData("lanczos3")]
    [InlineData("area")]
    [InlineData("LANCZOS3")]
    public void ImagePipeline_Apply_ResizeEveryFilter_GivesExactSizeAndKeepsFlatColor(string filter)
    {
        // Arrange: a flat 6x6 image
        using var input = new Surface(6, 6);
        input.Clear(new Rgba32(100, 150, 200, 255));

        // Act: shrink and enlarge
        using var smaller = Run(input, "--resize", "3x2", "--filter", filter);
        using var larger = Run(input, "--resize", "9x12", "--filter", filter);

        // Assert: exact sizes, flat color (within rounding)
        Assert.Equal((3, 2), (smaller.Width, smaller.Height));
        Assert.Equal((9, 12), (larger.Width, larger.Height));
        foreach (var surface in new[] { smaller, larger })
        {
            for (var y = 0; y < surface.Height; y++)
            {
                for (var x = 0; x < surface.Width; x++)
                {
                    var pixel = surface[x, y];
                    Assert.InRange(pixel.R, 99, 101);
                    Assert.InRange(pixel.G, 149, 151);
                    Assert.InRange(pixel.B, 199, 201);
                    Assert.Equal(255, pixel.A);
                }
            }
        }
    }

    /// <summary>
    ///     Test that different filters give different pixels on a hard edge, so the option is really used.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ResizeFilters_DifferOnAHardEdge()
    {
        // Arrange: black and white halves
        using var input = new Surface(8, 1);
        input.Clear(new Rgba32(0, 0, 0, 255));
        for (var x = 4; x < 8; x++)
        {
            input[x, 0] = new Rgba32(255, 255, 255, 255);
        }

        // Act: enlarge with three filters
        using var nearest = Run(input, "--resize", "20x1", "--filter", "nearest");
        using var bilinear = Run(input, "--resize", "20x1", "--filter", "bilinear");
        using var lanczos = Run(input, "--resize", "20x1", "--filter", "lanczos3");

        // Assert: nearest keeps the edge hard (only black and white), the others blend or differ
        for (var x = 0; x < 20; x++)
        {
            Assert.True(nearest[x, 0].R is 0 or 255, $"nearest pixel {x} is {nearest[x, 0].R}");
        }

        Assert.Contains(Enumerable.Range(0, 20), x => bilinear[x, 0].R is > 0 and < 255);
        Assert.NotEqual(Enumerable.Range(0, 20).Select(x => bilinear[x, 0]), Enumerable.Range(0, 20).Select(x => lanczos[x, 0]));
    }

    /// <summary>
    ///     Test that fit shrinks an image that is larger than the box, keeping the aspect ratio.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData(100, 50, 20, 10)]
    [InlineData(40, 10, 20, 5)]
    [InlineData(10, 80, 3, 20)]
    public void ImagePipeline_Apply_FitLargerImage_ShrinksIntoBox(int width, int height, int expectedWidth, int expectedHeight)
    {
        // Arrange
        using var input = new Surface(width, height);

        // Act: fit into 20x20
        using var result = Run(input, "--fit", "20x20");

        // Assert
        Assert.Equal((expectedWidth, expectedHeight), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that fit never enlarges unless upscaling is allowed.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_FitSmallerImage_IsUnchangedUnlessUpscaleAllowed()
    {
        // Arrange: a 5x2 image
        using var input = Numbered(5, 2);

        // Act
        using var unchanged = ImagePipeline.Apply(input, Parse("--fit", "20x20"), Transparent, out var unchangedOps);
        using var enlarged = ImagePipeline.Apply(input, Parse("--fit", "20x20", "--allow-upscale"), Transparent, out var enlargedOps);

        // Assert: the same pixels (a copy) without the option, 20x8 with it; the report says whether it scaled
        Assert.NotSame(input, unchanged);
        Assert.Equal("abcde/fghij", Letters(unchanged));
        Assert.Equal((20, 8), (enlarged.Width, enlarged.Height));
        Assert.Contains(new KeyValuePair<string, object>("scaled", false), Assert.Single(unchangedOps).Fields);
        Assert.Contains(new KeyValuePair<string, object>("scaled", true), Assert.Single(enlargedOps).Fields);
        Assert.Contains(new KeyValuePair<string, object>("allowUpscale", true), Assert.Single(enlargedOps).Fields);
    }

    /// <summary>
    ///     Test that operations are applied left to right: crop then resize is not resize then crop.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_OperationOrder_CropThenResizeDiffersFromResizeThenCrop()
    {
        // Arrange: 4x4 a..p
        using var input = Numbered(4, 4);

        // Act: take the top-left 2x2, then double it; or double the whole image, then take the top-left 2x2
        using var cropFirst = Run(input, "--crop", "0,0,2,2", "--resize", "4x4", "--filter", "nearest");
        using var resizeFirst = Run(input, "--resize", "8x8", "--filter", "nearest", "--crop", "0,0,2,2");

        // Assert: different sizes and different pixels
        Assert.Equal((4, 4), (cropFirst.Width, cropFirst.Height));
        Assert.Equal((2, 2), (resizeFirst.Width, resizeFirst.Height));
        Assert.Equal("aabb/aabb/eeff/eeff", Letters(cropFirst));
        Assert.Equal("aa/aa", Letters(resizeFirst));
    }

    /// <summary>
    ///     Test that parsing keeps the command-line order and puts the verb's operation first.
    /// </summary>
    [Fact]
    public void ImagePipeline_Parse_KeepsCommandLineOrderAndPutsVerbFirst()
    {
        // Arrange
        var reader = ArgumentReader.Parse(
            "crop",
            ["--rotate", "90", "--resize", "4x4", "--pad", "2", "--flip", "h", "--fit", "9x9", "--crop", "0,0,1,1"],
            RenderCommand.OptionTable);

        // Act: as a verb, and as render
        var asVerb = ImagePipeline.Parse(reader, "crop", "1,1,5,5");
        var asRender = ImagePipeline.Parse(reader, null, null);

        // Assert
        Assert.Equal(["crop", "rotate", "resize", "pad", "flip", "fit", "crop"], asVerb.Select(o => o.Name));
        Assert.Equal(["rotate", "resize", "pad", "flip", "fit", "crop"], asRender.Select(o => o.Name));
        Assert.Equal(new CropOperation(ArgumentReader.ParseGeometry("1,1,5,5", "x")), asVerb[0]);
    }

    /// <summary>
    ///     Test that each verb turns its operand into its own operation.
    /// </summary>
    [Fact]
    public void ImagePipeline_Parse_EachVerb_ParsesItsOperand()
    {
        // Arrange: one reader with a filter, and one that also has a border color (which only a pad can use)
        var reader = ArgumentReader.Parse("render", ["--filter", "bicubic"], RenderCommand.OptionTable);
        var padReader = ArgumentReader.Parse("render", ["--filter", "bicubic", "--pad-color", "red"], RenderCommand.OptionTable);
        var fillReader = ArgumentReader.Parse("render", ["--filter", "bicubic", "--rotate-fill", "red"], RenderCommand.OptionTable);

        // Act / Assert: the operation, parameters, and shared settings
        Assert.Equal(new CropOperation(ArgumentReader.ParseGeometry("1,2,3,4", "x")), Assert.Single(ImagePipeline.Parse(reader, "crop", "1,2,3,4")));
        Assert.Equal(new ResizeOperation(5, 6, ResamplingMode.Bicubic), Assert.Single(ImagePipeline.Parse(reader, "resize", "5x6")));
        Assert.Equal(new RotateOperation(270, null, ResamplingMode.Bicubic), Assert.Single(ImagePipeline.Parse(reader, "rotate", "-90")));
        Assert.Equal(new RotateOperation(3.5, Red, ResamplingMode.Bicubic), Assert.Single(ImagePipeline.Parse(fillReader, "rotate", "3.5")));
        Assert.Equal(new FlipOperation(FlipAxis.Vertical), Assert.Single(ImagePipeline.Parse(reader, "flip", "v")));
        Assert.Equal(new PadOperation(new PadSpec(1, 2, 3, 4), Red), Assert.Single(ImagePipeline.Parse(padReader, "pad", "1,2,3,4")));
        Assert.Equal(new PadOperation(new PadSpec(7, 7, 7, 7), Red), Assert.Single(ImagePipeline.Parse(padReader, "pad", "7")));
        Assert.Equal(new PadOperation(new PadSpec(7, 7, 7, 7), null), Assert.Single(ImagePipeline.Parse(reader, "pad", "7")));
    }

    /// <summary>
    ///     Test that malformed operations and settings are usage errors that name the option and the value.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">The value.</param>
    /// <param name="message">Text the message contains.</param>
    [Theory]
    [InlineData("--rotate", "abc", "Invalid value 'abc' for --rotate")]
    [InlineData("--rotate", "NaN", "Invalid value 'NaN' for --rotate")]
    [InlineData("--rotate", "Infinity", "Invalid value 'Infinity' for --rotate")]
    [InlineData("--rotate", "left", "Invalid value 'left' for --rotate")]
    [InlineData("--rotate-fill", "chartreuse-ish", "Invalid value 'chartreuse-ish' for --rotate-fill")]
    [InlineData("--flip", "diagonal", "Invalid value 'diagonal' for --flip")]
    [InlineData("--crop", "1,2,3", "Invalid value '1,2,3' for --crop")]
    [InlineData("--crop", "0,0,0,5", "Invalid value '0,0,0,5' for --crop")]
    [InlineData("--resize", "100", "Invalid value '100' for --resize")]
    [InlineData("--resize", "0x10", "Invalid value '0x10' for --resize")]
    [InlineData("--fit", "x10", "Invalid value 'x10' for --fit")]
    [InlineData("--pad", "1,2,3", "Invalid value '1,2,3' for --pad")]
    [InlineData("--pad", "-1", "Invalid value '-1' for --pad")]
    [InlineData("--pad-color", "chartreuse-ish", "Invalid value 'chartreuse-ish' for --pad-color")]
    [InlineData("--filter", "sharp", "expected one of auto, nearest, bilinear, bicubic, lanczos3, area")]
    [InlineData("--threshold", "256", "Invalid value '256' for --threshold: expected an integer from 0 to 255, or auto")]
    [InlineData("--threshold", "-1", "Invalid value '-1' for --threshold")]
    [InlineData("--threshold", "dark", "Invalid value 'dark' for --threshold")]
    [InlineData("--levels", "200,100", "Invalid value '200,100' for --levels: expected a black level below the white level")]
    [InlineData("--levels", "5,5", "Invalid value '5,5' for --levels")]
    [InlineData("--levels", "5", "Invalid value '5' for --levels: expected black,white or black,white,gamma")]
    [InlineData("--levels", "1,2,3,4", "Invalid value '1,2,3,4' for --levels")]
    [InlineData("--levels", "255,255", "for --levels")]
    [InlineData("--levels", "0,255,0.05", "for --levels")]
    [InlineData("--levels", "0,255,11", "for --levels")]
    [InlineData("--brightness", "256", "Invalid value '256' for --brightness")]
    [InlineData("--brightness", "1.5", "Invalid value '1.5' for --brightness")]
    [InlineData("--contrast", "101", "Invalid value '101' for --contrast")]
    [InlineData("--contrast", "abc", "Invalid value 'abc' for --contrast")]
    [InlineData("--auto-levels-clip", "21", "Invalid value '21' for --auto-levels-clip")]
    [InlineData("--auto-levels-clip", "-1", "Invalid value '-1' for --auto-levels-clip")]
    [InlineData("--trim-tolerance", "256", "Invalid value '256' for --trim-tolerance")]
    [InlineData("--grid", "4", "Invalid value '4' for --grid")]
    [InlineData("--grid", "5000", "Invalid value '5000' for --grid")]
    [InlineData("--grid", "100,nocolor", "Invalid value 'nocolor' for --grid")]
    public void ImagePipeline_Parse_BadValue_ThrowsUsageError(string option, string value, string message)
    {
        // Arrange: a setting needs the operation it modifies to be meaningful, so give it one
        var args = option switch
        {
            "--pad-color" => new[] { "--pad", "1", option, value },
            "--rotate-fill" => ["--rotate", "10", option, value],
            "--auto-levels-clip" => ["--auto-levels", option, value],
            "--trim-tolerance" => ["--trim", option, value],
            _ => new[] { option, value }
        };

        // Act
        var error = Assert.Throws<ToolException>(() => Parse(args));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Contains(message, error.Message);
    }

    /// <summary>
    ///     Test that settings that only qualify an operation are errors without that operation.
    /// </summary>
    [Fact]
    public void ImagePipeline_Parse_ModifierWithoutItsOperation_ThrowsUsageError()
    {
        // Act
        var upscale = Assert.Throws<ToolException>(() => Parse("--allow-upscale", "--resize", "4x4"));
        var padColor = Assert.Throws<ToolException>(() => Parse("--pad-color", "red", "--crop", "0,0,1,1"));
        var clip = Assert.Throws<ToolException>(() => Parse("--auto-levels-clip", "2", "--grayscale"));
        var tolerance = Assert.Throws<ToolException>(() => Parse("--trim-tolerance", "4", "--crop", "0,0,1,1"));
        var fill = Assert.Throws<ToolException>(() => Parse("--rotate-fill", "red", "--flip", "h"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, upscale.Code);
        Assert.Contains("--allow-upscale only applies with --fit", upscale.Message);
        Assert.Equal(ErrorCodes.UsageError, padColor.Code);
        Assert.Contains("--pad-color only applies with a pad", padColor.Message);
        Assert.Equal(ErrorCodes.UsageError, clip.Code);
        Assert.Contains("--auto-levels-clip only applies with --auto-levels", clip.Message);
        Assert.Equal(ErrorCodes.UsageError, tolerance.Code);
        Assert.Contains("--trim-tolerance only applies with --trim", tolerance.Message);
        Assert.Equal(ErrorCodes.UsageError, fill.Code);
        Assert.Contains("--rotate-fill only applies with a rotation or --deskew", fill.Message);

        // The settings are fine with their operation, and --rotate-fill also with --deskew
        Assert.Single(Parse("--auto-levels-clip", "2", "--auto-levels"));
        Assert.Single(Parse("--trim-tolerance", "4", "--trim"));
        Assert.Single(Parse("--rotate-fill", "red", "--deskew"));
    }

    /// <summary>
    ///     Test that a size or border above the maximum dimension is refused while parsing, with the size-limit code.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData("--resize", "8193x10")]
    [InlineData("--resize", "10x9000")]
    [InlineData("--fit", "9000x9000")]
    [InlineData("--pad", "8193")]
    [InlineData("--pad", "0,0,0,9000")]
    public void ImagePipeline_Parse_BeyondMaxDimension_ThrowsSizeLimit(string option, string value)
    {
        // Act
        var error = Assert.Throws<ToolException>(() => Parse(option, value));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.Contains("8192", error.Message);
    }

    /// <summary>
    ///     Test that a border that makes the image larger than the maximum is refused before the result is allocated.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadBeyondMaxDimension_ThrowsSizeLimit()
    {
        // Arrange: 8000 wide, padded by 100 on each side
        using var input = new Surface(8000, 1);
        var operations = Parse("--pad", "100");

        // Act
        var error = Assert.Throws<ToolException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert: the code and a message that gives the size and the limit
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.Contains("8200x201", error.Message);
        Assert.Contains("8192", error.Message);
    }

    /// <summary>
    ///     Test that an operation at exactly the maximum dimension works.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_PadToMaxDimension_Succeeds()
    {
        // Arrange: 8000 wide, padded to exactly 8192
        using var input = new Surface(8000, 1);
        input.Clear(Red);

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--pad", "0,0,192,0"), Transparent, out _);

        // Assert
        Assert.Equal((Surface.MaxDimension, 1), (result.Width, result.Height));
        Assert.Equal(Red, result[7999, 0]);
        Assert.Equal(Transparent, result[8000, 0]);
    }

    /// <summary>
    ///     Test that the applied operations list every step in order with its parameters and the size of its result.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Chain_ReportsEachStepInOrderWithSizes()
    {
        // Arrange
        using var input = Numbered(4, 4);

        // Act
        using var result = ImagePipeline.Apply(
            input,
            Parse("--crop", "0,0,4,2", "--rotate", "90", "--flip", "both", "--pad", "1,2,3,4", "--resize", "5x5"),
            Transparent,
            out var applied);

        // Assert: five steps, in order, each with its own result size
        Assert.Equal(["crop", "rotate", "flip", "pad", "resize"], applied.Select(a => a.Op));
        Assert.Equal((4, 2), SizeOf(applied[0]));
        Assert.Equal((2, 4), SizeOf(applied[1]));
        Assert.Equal((2, 4), SizeOf(applied[2]));
        Assert.Equal((6, 10), SizeOf(applied[3]));
        Assert.Equal((5, 5), SizeOf(applied[4]));
        Assert.Contains(new KeyValuePair<string, object>("degrees", 90), applied[1].Fields);
        Assert.Contains(new KeyValuePair<string, object>("axis", "both"), applied[2].Fields);
        Assert.Contains(new KeyValuePair<string, object>("left", 1), applied[3].Fields);
        Assert.Contains(new KeyValuePair<string, object>("bottom", 4), applied[3].Fields);
        Assert.Contains(new KeyValuePair<string, object>("filter", "auto"), applied[4].Fields);
    }

    /// <summary>
    ///     Reads the width and height that an applied operation reports.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The size.</returns>
    private static (int Width, int Height) SizeOf(AppliedOperation operation)
    {
        return (
            (int)operation.Fields.Single(f => f.Key == "width").Value,
            (int)operation.Fields.Single(f => f.Key == "height").Value);
    }

    /// <summary>
    ///     Test that the input is never modified or disposed, an empty list gives an independent copy, and a failure still leaves the input usable.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Input_IsNeverModifiedOrDisposed()
    {
        // Arrange
        using var input = Numbered(3, 2);

        // Act: an empty list, a chain, and a chain whose last step fails
        using var copy = ImagePipeline.Apply(input, [], Transparent, out var none);
        using var chained = Run(input, "--flip", "h", "--rotate", "90");
        Assert.Throws<ToolException>(() => Run(input, "--flip", "h", "--crop", "9,9,1,1"));

        // Assert: nothing applied, a different but equal surface, and the input untouched and not disposed
        Assert.Empty(none);
        Assert.NotSame(input, copy);
        Assert.Equal("abc/def", Letters(copy));
        copy[0, 0] = Red;
        Assert.Equal("abc/def", Letters(input));
        Assert.Equal((2, 3), (chained.Width, chained.Height));
    }

    /// <summary>
    ///     Test that the same operations on the same image give the same pixels every time.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_SameInputTwice_GivesIdenticalPixels()
    {
        // Arrange
        using var input = FixtureFactory.CreateTranslucent(17, 11);
        string[] args = ["--crop", "10%,10%,80%,80%", "--rotate", "90", "--resize", "9x7", "--filter", "lanczos3", "--pad", "2"];

        // Act
        using var first = Run(input, args);
        using var second = Run(input, args);

        // Assert
        SurfaceAssert.Equal(first, second);
    }

    /// <summary>
    ///     Test that the filter names are parsed in any case and that unknown names list the valid ones.
    /// </summary>
    [Fact]
    public void ImagePipeline_ParseFilter_NamesAnyCase_RoundTripThroughFilterName()
    {
        // Arrange / Act / Assert
        foreach (var name in new[] { "auto", "nearest", "bilinear", "bicubic", "lanczos3", "area" })
        {
            Assert.Equal(name, ImagePipeline.FilterName(ImagePipeline.ParseFilter(name.ToUpperInvariant(), "--filter")));
        }

        Assert.Equal(ResamplingMode.Lanczos3, ImagePipeline.ParseFilter("Lanczos3", "--filter"));
    }

    /// <summary>
    ///     Test that a verb outside the known list is rejected.
    /// </summary>
    [Fact]
    public void ImagePipeline_Parse_UnknownVerb_ThrowsArgumentException()
    {
        // Arrange
        var reader = ArgumentReader.Parse("render", [], RenderCommand.OptionTable);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ImagePipeline.Parse(reader, "blur", "3"));
        Assert.Throws<ArgumentNullException>(() => ImagePipeline.Parse(reader, "crop", null));
        Assert.Equal(["crop", "resize", "rotate", "flip", "pad"], ImagePipeline.VerbNames);
    }

    /// <summary>
    ///     Reads one parameter of an applied operation.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The value.</returns>
    private static T Field<T>(AppliedOperation operation, string name)
    {
        return (T)operation.Fields.Single(f => f.Key == name).Value;
    }

    /// <summary>
    ///     Creates a surface filled with one color.
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
    ///     Creates a surface one pixel high from pixels.
    /// </summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The surface.</returns>
    private static Surface Row(params Rgba32[] pixels)
    {
        var surface = new Surface(pixels.Length, 1);
        for (var x = 0; x < pixels.Length; x++)
        {
            surface[x, 0] = pixels[x];
        }

        return surface;
    }

    /// <summary>
    ///     Creates an exact copy of a surface.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <returns>The copy.</returns>
    private static Surface Copy(Surface source)
    {
        return source.Crop(0, 0, source.Width, source.Height);
    }

    /// <summary>
    ///     Test that the photo options are parsed into their operations, in command-line order, with their settings and defaults.
    /// </summary>
    [Fact]
    public void ImagePipeline_Parse_AdjustmentOptions_KeepCommandLineOrder()
    {
        // Act
        var operations = Parse(
            "--grayscale", "--invert", "--threshold", "auto", "--levels", "10,200,1.5", "--brightness", "-20",
            "--contrast", "15.5", "--auto-levels", "--auto-levels-clip", "2.5", "--deskew", "--trim", "--trim-tolerance", "4",
            "--grid", "50,red", "--rotate", "12.5", "--rotate-fill", "blue", "--filter", "nearest");
        var defaults = Parse("--auto-levels", "--trim", "--grid", "16", "--threshold", "128", "--deskew", "--rotate", "-90");

        // Assert: the order of the line, and the operations with the settings of the whole command line
        Assert.Equal(
            ["grayscale", "invert", "threshold", "levels", "brightness", "contrast", "auto-levels", "deskew", "trim", "grid", "rotate"],
            operations.Select(o => o.Name));
        var blue = new Rgba32(0, 0, 255, 255);
        Assert.Equal(new ThresholdOperation(null), operations[2]);
        Assert.Equal(new LevelsOperation(10, 200, 1.5), operations[3]);
        Assert.Equal(new BrightnessOperation(-20), operations[4]);
        Assert.Equal(new ContrastOperation(15.5), operations[5]);
        Assert.Equal(new AutoLevelsOperation(2.5), operations[6]);
        Assert.Equal(new DeskewOperation(blue, ResamplingMode.Nearest), operations[7]);
        Assert.Equal(new TrimOperation(4), operations[8]);
        Assert.Equal(new GridOperation(50, Red), operations[9]);
        Assert.Equal(new RotateOperation(12.5, blue, ResamplingMode.Nearest), operations[10]);

        // Defaults: clip 1 percent, trim tolerance 8, default grid color, a negative angle is normalized
        Assert.Equal(new AutoLevelsOperation(1.0), defaults[0]);
        Assert.Equal(new TrimOperation(8), defaults[1]);
        Assert.Equal(new GridOperation(16, null), defaults[2]);
        Assert.Equal(new ThresholdOperation(128), defaults[3]);
        Assert.Equal(new DeskewOperation(), defaults[4]);
        Assert.Equal(new RotateOperation(270.0), defaults[5]);
    }

    /// <summary>
    ///     Test that grayscale, invert, and a fixed threshold give the documented pixels and report their size.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_GrayscaleInvertThreshold_ProduceExpectedPixels()
    {
        // Arrange: a red pixel, a translucent green pixel, and a dark blue-gray pixel
        using var input = Row(new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 128), new Rgba32(10, 20, 30, 255));

        // Act
        using var gray = ImagePipeline.Apply(input, Parse("--grayscale"), Transparent, out var grayApplied);
        using var inverted = ImagePipeline.Apply(input, Parse("--invert"), Transparent, out var invertedApplied);
        using var black = ImagePipeline.Apply(input, Parse("--threshold", "100"), Transparent, out var thresholdApplied);

        // Assert: luma 77, 149, and (770 + 3000 + 870 + 128) >> 8 = 18, with the alpha kept
        Assert.Equal(new Rgba32(77, 77, 77, 255), gray[0, 0]);
        Assert.Equal(new Rgba32(149, 149, 149, 128), gray[1, 0]);
        Assert.Equal(new Rgba32(18, 18, 18, 255), gray[2, 0]);
        Assert.Equal(new Rgba32(0, 255, 255, 255), inverted[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 255, 128), inverted[1, 0]);
        Assert.Equal(new Rgba32(245, 235, 225, 255), inverted[2, 0]);

        // Threshold 100: the luma over white is 77, 202 (translucent green over white), and 18; the result is opaque
        Assert.Equal(new Rgba32(0, 0, 0, 255), black[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), black[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), black[2, 0]);
        var step = Assert.Single(thresholdApplied);
        Assert.Equal("threshold", step.Op);
        Assert.False(Field<bool>(step, "auto"));
        Assert.Equal(100, Field<int>(step, "level"));
        Assert.Equal((3, 1), SizeOf(step));
        Assert.Equal("grayscale", Assert.Single(grayApplied).Op);
        Assert.Equal((3, 1), SizeOf(Assert.Single(invertedApplied)));
    }

    /// <summary>
    ///     Test that an automatic threshold reports the level Otsu's method chose.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ThresholdAuto_ReportsLevel()
    {
        // Arrange: five pixels at level 20 and five at level 200, whose Otsu threshold is the middle of the gap, 109
        var dark = new Rgba32(20, 20, 20, 255);
        var light = new Rgba32(200, 200, 200, 255);
        using var input = Row(dark, dark, dark, dark, dark, light, light, light, light, light);

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--threshold", "auto"), Transparent, out var applied);

        // Assert
        var step = Assert.Single(applied);
        Assert.True(Field<bool>(step, "auto"));
        Assert.Equal(109, Field<int>(step, "level"));
        Assert.Equal(new Rgba32(0, 0, 0, 255), result[4, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), result[5, 0]);
    }

    /// <summary>
    ///     Test that levels, brightness, and contrast are applied in the order written.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_LevelsBrightnessContrast_ApplyTablesInOrder()
    {
        // Arrange: one mid-gray pixel
        using var input = Row(new Rgba32(100, 100, 100, 200));

        // Act: levels 50..150 maps 100 to 128; then +10 gives 138; contrast 0 is the identity
        using var levelsFirst = ImagePipeline.Apply(input, Parse("--levels", "50,150", "--brightness", "10", "--contrast", "0"), Transparent, out var applied);
        using var brightnessFirst = Run(input, "--brightness", "10", "--levels", "50,150");

        // Assert: 100 -> 128 -> 138, but 100 -> 110 -> 153; the alpha is untouched
        Assert.Equal(new Rgba32(138, 138, 138, 200), levelsFirst[0, 0]);
        Assert.Equal(new Rgba32(153, 153, 153, 200), brightnessFirst[0, 0]);
        Assert.Equal(["levels", "brightness", "contrast"], applied.Select(a => a.Op));
        Assert.Equal(50, Field<int>(applied[0], "black"));
        Assert.Equal(150, Field<int>(applied[0], "white"));
        Assert.Equal(1.0, Field<double>(applied[0], "gamma"));
        Assert.Equal(10, Field<int>(applied[1], "amount"));
        Assert.Equal(0.0, Field<double>(applied[2], "amount"));
        Assert.Equal((1, 1), SizeOf(applied[2]));
    }

    /// <summary>
    ///     Test that auto-levels stretches the levels, reports the points, and leaves a flat image alone.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_AutoLevels_ReportsPointsAndFlatImageIsUnchanged()
    {
        // Arrange: levels 100 to 145, one pixel each, and a flat image
        using var ramp = new Surface(46, 1);
        for (var x = 0; x < 46; x++)
        {
            ramp[x, 0] = new Rgba32((byte)(100 + x), (byte)(100 + x), (byte)(100 + x), 255);
        }

        using var flat = Solid(8, 8, new Rgba32(128, 128, 128, 255));

        // Act
        using var stretched = ImagePipeline.Apply(ramp, Parse("--auto-levels"), Transparent, out var applied);
        using var same = ImagePipeline.Apply(flat, Parse("--auto-levels"), Transparent, out var flatApplied);

        // Assert: the ends become black and white (1 percent of 46 pixels rounds up to 1 pixel)
        Assert.Equal(0, stretched[0, 0].R);
        Assert.Equal(255, stretched[45, 0].R);
        var step = Assert.Single(applied);
        Assert.True(Field<bool>(step, "applied"));
        Assert.Equal(1.0, Field<double>(step, "clip"));
        Assert.Equal(100, Field<int>(step, "black"));
        Assert.Equal(145, Field<int>(step, "white"));
        SurfaceAssert.Equal(flat, same);
        var flatStep = Assert.Single(flatApplied);
        Assert.False(Field<bool>(flatStep, "applied"));
        Assert.DoesNotContain(flatStep.Fields, f => f.Key == "black");
    }

    /// <summary>
    ///     Test that an oblique angle grows the canvas, fills the corners, and is reported with its fill and filter.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ArbitraryAngle_ExpandsCanvasAndReports()
    {
        // Arrange: a solid red 100x50 image
        using var input = Solid(100, 50, Red);

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--rotate", "30"), Transparent, out var applied);

        // Assert: 112x94 (ceil of 100 cos 30 + 50 sin 30 and 100 sin 30 + 50 cos 30), white corners, red middle
        Assert.Equal((112, 94), (result.Width, result.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), result[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), result[111, 93]);
        Assert.Equal(Red, result[56, 47]);
        var step = Assert.Single(applied);
        Assert.Equal("rotate", step.Op);
        Assert.Equal(30.0, Field<double>(step, "degrees"));
        Assert.Equal("#FFFFFF", Field<string>(step, "fill"));
        Assert.Equal("bilinear", Field<string>(step, "filter"));
        Assert.Equal((112, 94), SizeOf(step));
    }

    /// <summary>
    ///     Test that rotating by nothing or by a full turn is a copy and not an error.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ZeroAndFullTurn_AreCopies()
    {
        // Arrange
        using var input = Numbered(3, 2);

        foreach (var angle in new[] { "0", "360", "-360", "720", "0.0" })
        {
            // Act
            using var result = ImagePipeline.Apply(input, Parse("--rotate", angle), Transparent, out var applied);

            // Assert: the same pixels in a different surface, reported as a rotation by 0
            Assert.NotSame(input, result);
            Assert.Equal("abc/def", Letters(result));
            Assert.Equal(0, Field<int>(Assert.Single(applied), "degrees"));
        }
    }

    /// <summary>
    ///     Test that a negative angle is the same rotation as its complement, and a turn plus an angle as the angle.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_NegativeObliqueAngle_EqualsComplement()
    {
        // Arrange
        using var input = FixtureFactory.SketchPhoto(60, 40, 0);

        // Act
        using var negative = Run(input, "--rotate", "-30");
        using var complement = Run(input, "--rotate", "330");
        using var turned = Run(input, "--rotate", "390");
        using var plain = Run(input, "--rotate", "30");
        using var unit = Run(input, "--rotate", "30deg");

        // Assert
        SurfaceAssert.Equal(complement, negative);
        SurfaceAssert.Equal(plain, turned);
        SurfaceAssert.Equal(plain, unit);
    }

    /// <summary>
    ///     Test that the fill of a rotation is its own color, else the background, else white for an opaque image, else transparent.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_RotateFill_DefaultRuleOpaqueWhiteTransparentOrBackground()
    {
        // Arrange: an opaque image and one with a transparent pixel
        using var opaque = Solid(20, 10, Red);
        using var holey = Solid(20, 10, Red);
        holey[0, 0] = Transparent;
        var blue = new Rgba32(0, 0, 255, 255);
        var green = new Rgba32(0, 128, 0, 255);

        // Act
        using var opaqueDefault = Run(opaque, "--rotate", "45");
        using var holeyDefault = Run(holey, "--rotate", "45");
        using var withBackground = ImagePipeline.Apply(holey, Parse("--rotate", "45"), Transparent, out _, blue);
        using var ownFill = ImagePipeline.Apply(holey, Parse("--rotate", "45", "--rotate-fill", "#008000"), Transparent, out var applied, blue);
        using var transparentFill = ImagePipeline.Apply(opaque, Parse("--rotate", "45", "--rotate-fill", "transparent"), Transparent, out _);

        // Assert: opaque -> white, transparent pixel -> transparent, background -> blue, own fill wins
        Assert.Equal(new Rgba32(255, 255, 255, 255), opaqueDefault[0, 0]);
        Assert.Equal(Transparent, holeyDefault[0, 0]);
        Assert.Equal(blue, withBackground[0, 0]);
        Assert.Equal(green, ownFill[0, 0]);
        Assert.Equal("#008000", Field<string>(Assert.Single(applied), "fill"));
        Assert.Equal(Transparent, transparentFill[0, 0]);
    }

    /// <summary>
    ///     Test that the nearest filter copies pixels while every other filter interpolates.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_RotateFilterNearest_UsesNearest()
    {
        // Arrange: black and white halves
        using var input = Solid(40, 40, new Rgba32(255, 255, 255, 255));
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                input[x, y] = new Rgba32(0, 0, 0, 255);
            }
        }

        // Act
        using var nearest = ImagePipeline.Apply(input, Parse("--rotate", "20", "--filter", "nearest"), Transparent, out var nearestApplied);
        using var smooth = ImagePipeline.Apply(input, Parse("--rotate", "20", "--filter", "lanczos3"), Transparent, out var smoothApplied);

        // Assert: nearest has only black and white; bilinear (used for lanczos3) has in-between grays
        for (var y = 0; y < nearest.Height; y++)
        {
            for (var x = 0; x < nearest.Width; x++)
            {
                Assert.True(nearest[x, y].R is 0 or 255, $"nearest pixel {x},{y} is {nearest[x, y]}");
            }
        }

        var grays = 0;
        for (var y = 0; y < smooth.Height; y++)
        {
            for (var x = 0; x < smooth.Width; x++)
            {
                grays += smooth[x, y].R is > 0 and < 255 ? 1 : 0;
            }
        }

        Assert.True(grays > 20, $"{grays} blended pixels");
        Assert.Equal("nearest", Field<string>(Assert.Single(nearestApplied), "filter"));
        Assert.Equal("bilinear", Field<string>(Assert.Single(smoothApplied), "filter"));
    }

    /// <summary>
    ///     Test that a rotation whose canvas would be larger than the maximum is refused with the size-limit code.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_RotateTooLarge_ThrowsSizeLimit()
    {
        // Arrange: 8192 wide, which a tilt of one degree makes 8193 wide
        using var input = new Surface(8192, 100);
        var operations = Parse("--rotate", "1");

        // Act
        var error = Assert.Throws<ToolException>(() => ImagePipeline.Apply(input, operations, Transparent, out _));

        // Assert: the code, the size, and the hint to shrink first
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.Contains("8193x", error.Message);
        Assert.Contains("--fit", error.Hint);
    }

    /// <summary>
    ///     Test that deskew rotates by the negative of the skew it found and reports the angle and the confidence.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Deskew_RotatesByNegativeSkewAndReportsAngle()
    {
        // Arrange: ruled lines that descend to the right by 5 degrees
        using var input = FixtureFactory.SketchPhoto(400, 300, 5.0);

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--deskew"), Transparent, out var applied);

        // Assert: the skew is about +5, the correction is exactly its negative, and the canvas grew
        var step = Assert.Single(applied);
        Assert.Equal("deskew", step.Op);
        Assert.True(Field<bool>(step, "applied"));
        var skew = Field<double>(step, "skewDegrees");
        Assert.InRange(skew, 4.8, 5.2);
        Assert.Equal(-skew, Field<double>(step, "rotateDegrees"));
        Assert.True(Field<double>(step, "confidence") >= ImageGeometry.MinConfidence);
        Assert.Equal("#FFFFFF", Field<string>(step, "fill"));
        Assert.Equal("bilinear", Field<string>(step, "filter"));
        Assert.True(result.Width > 400 && result.Height > 300);
        Assert.Equal((result.Width, result.Height), SizeOf(step));

        // The straightened image has (almost) no skew left
        Assert.InRange(ImageGeometry.EstimateSkew(result).Degrees, -0.3, 0.3);
    }

    /// <summary>
    ///     Test that an image without structure or without skew is copied unchanged, and the angle and confidence are still reported.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_DeskewNoStructure_LeavesImageUnchanged()
    {
        // Arrange: a blank sheet, and lines that are already level
        using var blank = Solid(120, 80, new Rgba32(200, 200, 200, 255));
        using var level = FixtureFactory.SketchPhoto(300, 200, 0.0);

        // Act
        using var blankResult = ImagePipeline.Apply(blank, Parse("--deskew"), Transparent, out var blankApplied);
        using var levelResult = ImagePipeline.Apply(level, Parse("--deskew"), Transparent, out var levelApplied);

        // Assert: not applied, same pixels, rotation 0
        SurfaceAssert.Equal(blank, blankResult);
        SurfaceAssert.Equal(level, levelResult);
        var blankStep = Assert.Single(blankApplied);
        Assert.False(Field<bool>(blankStep, "applied"));
        Assert.Equal(0.0, Field<double>(blankStep, "skewDegrees"));
        Assert.Equal(0.0, Field<double>(blankStep, "rotateDegrees"));
        Assert.Equal(0.0, Field<double>(blankStep, "confidence"));
        Assert.False(Field<bool>(Assert.Single(levelApplied), "applied"));
        Assert.DoesNotContain(blankStep.Fields, f => f.Key == "fill");
    }

    /// <summary>
    ///     Test that trim crops to the content and reports the box; the tolerance setting decides what is background.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Trim_CropsToContentAndReportsBox()
    {
        // Arrange: a white image with a 6x4 black block at 5,7 and one pixel of 245 at 1,1 (10 levels from white)
        using var input = Solid(20, 20, new Rgba32(255, 255, 255, 255));
        for (var y = 7; y < 11; y++)
        {
            for (var x = 5; x < 11; x++)
            {
                input[x, y] = new Rgba32(0, 0, 0, 255);
            }
        }

        input[1, 1] = new Rgba32(245, 245, 245, 255);

        // Act: the default tolerance of 8 keeps the speck; a tolerance of 12 ignores it
        using var withSpeck = ImagePipeline.Apply(input, Parse("--trim"), Transparent, out var speckApplied);
        using var clean = ImagePipeline.Apply(input, Parse("--trim", "--trim-tolerance", "12"), Transparent, out var cleanApplied);

        // Assert
        Assert.Equal((10, 10), (withSpeck.Width, withSpeck.Height));
        Assert.Equal((6, 4), (clean.Width, clean.Height));
        Assert.Equal(new Rgba32(0, 0, 0, 255), clean[0, 0]);
        var step = Assert.Single(cleanApplied);
        Assert.Equal("trim", step.Op);
        Assert.Equal(12, Field<int>(step, "tolerance"));
        Assert.Equal((5, 7), (Field<int>(step, "x"), Field<int>(step, "y")));
        Assert.Equal((6, 4), SizeOf(step));
        Assert.True(Field<bool>(step, "trimmed"));
        Assert.False(Field<bool>(step, "empty"));
        Assert.Equal(8, Field<int>(Assert.Single(speckApplied), "tolerance"));
    }

    /// <summary>
    ///     Test that an image that is all background is unchanged and reported as empty.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_TrimAllBackground_IsUnchangedAndReportsEmpty()
    {
        // Arrange
        using var input = Solid(10, 10, new Rgba32(255, 255, 255, 255));

        // Act
        using var result = ImagePipeline.Apply(input, Parse("--trim"), Transparent, out var applied);

        // Assert
        SurfaceAssert.Equal(input, result);
        Assert.NotSame(input, result);
        var step = Assert.Single(applied);
        Assert.False(Field<bool>(step, "trimmed"));
        Assert.True(Field<bool>(step, "empty"));
        Assert.Equal((0, 0), (Field<int>(step, "x"), Field<int>(step, "y")));
        Assert.Equal((10, 10), SizeOf(step));
    }

    /// <summary>
    ///     Test that the grid is drawn on a copy, never on the input, and reports what it drew.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_Grid_DrawsOverCopyAndNeverModifiesInput()
    {
        // Arrange
        var white = new Rgba32(255, 255, 255, 255);
        using var input = Solid(100, 100, white);

        // Act
        using var colored = ImagePipeline.Apply(input, Parse("--grid", "50,red"), Transparent, out var applied);
        using var defaulted = ImagePipeline.Apply(input, Parse("--grid", "50"), Transparent, out var defaultApplied);

        // Assert: the lines are on the result only
        Assert.NotSame(input, colored);
        Assert.Equal(Red, colored[50, 20]);
        Assert.Equal(white, input[50, 20]);
        Assert.NotEqual(white, defaulted[50, 20]);
        var step = Assert.Single(applied);
        Assert.Equal("grid", step.Op);
        Assert.Equal(50, Field<int>(step, "spacing"));
        Assert.Equal("#FF0000", Field<string>(step, "color"));
        Assert.Equal(2, Field<int>(step, "verticalLines"));
        Assert.Equal(2, Field<int>(step, "horizontalLines"));
        Assert.True(Field<int>(step, "labels") > 0);
        Assert.Equal((100, 100), SizeOf(step));
        Assert.Equal("#FF00FFA0", Field<string>(Assert.Single(defaultApplied), "color"));
    }

    /// <summary>
    ///     Test that in-place operations change a copy of the input, and chains of them leave the input as it was.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_InPlaceOperations_NeverModifyTheInputAndDisposeIntermediates()
    {
        // Arrange
        using var input = FixtureFactory.CreateTranslucent(40, 30);
        using var before = Copy(input);

        // Act: a single in-place operation, a chain of them, one after a crop, and one that is followed by a failure
        using var one = Run(input, "--invert");
        using var chain = Run(input, "--grayscale", "--invert", "--brightness", "10", "--levels", "10,200", "--grid", "16");
        using var afterCrop = Run(input, "--crop", "0,0,20,20", "--threshold", "128");
        Assert.Throws<ToolException>(() => Run(input, "--invert", "--threshold", "auto", "--crop", "999,999,1,1"));

        // Assert: the input is the same, not disposed, and every result is a different surface
        SurfaceAssert.Equal(before, input);
        Assert.NotSame(input, one);
        Assert.NotSame(input, chain);
        Assert.Equal((20, 20), (afterCrop.Width, afterCrop.Height));
        Assert.NotEqual(input[3, 3], one[3, 3]);
    }

    /// <summary>
    ///     Test that a chain of adjustments in one pipeline gives the same pixels as running each step on the result of the previous.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_ChainedAdjustments_MatchSeparateRuns()
    {
        // Arrange
        using var input = FixtureFactory.SketchPhoto(80, 60, 2.0, shade: 40);

        // Act
        using var chained = Run(input, "--grayscale", "--levels", "20,230", "--contrast", "10", "--threshold", "auto");
        using var first = Run(input, "--grayscale");
        using var second = Run(first, "--levels", "20,230");
        using var third = Run(second, "--contrast", "10");
        using var stepwise = Run(third, "--threshold", "auto");

        // Assert
        SurfaceAssert.Equal(stepwise, chained);
    }

    /// <summary>
    ///     Test that operations run in the order written, so cropping before deskewing gives a different image than after.
    /// </summary>
    [Fact]
    public void ImagePipeline_Apply_OperationOrder_CropThenDeskewDiffersFromDeskewThenCrop()
    {
        // Arrange
        using var input = FixtureFactory.SketchPhoto(400, 300, 4.0);

        // Act
        using var cropFirst = ImagePipeline.Apply(input, Parse("--crop", "0,0,200,300", "--deskew"), Transparent, out var cropFirstApplied);
        using var deskewFirst = ImagePipeline.Apply(input, Parse("--deskew", "--crop", "0,0,200,300"), Transparent, out var deskewFirstApplied);

        // Assert: both corrected the skew; the first grew past the crop, the second is exactly the crop
        Assert.True(Field<bool>(cropFirstApplied[1], "applied"));
        Assert.True(Field<bool>(deskewFirstApplied[0], "applied"));
        Assert.Equal((200, 300), (deskewFirst.Width, deskewFirst.Height));
        Assert.True(cropFirst.Width > 200 && cropFirst.Height > 300);
    }
}
