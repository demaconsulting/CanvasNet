// cspell:ignore lanczos canvasnet zabcz zdefz zabczz zdefzz ccdd fghij eeff
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

        // Act / Assert: the operation, parameters, and shared settings
        Assert.Equal(new CropOperation(ArgumentReader.ParseGeometry("1,2,3,4", "x")), Assert.Single(ImagePipeline.Parse(reader, "crop", "1,2,3,4")));
        Assert.Equal(new ResizeOperation(5, 6, ResamplingMode.Bicubic), Assert.Single(ImagePipeline.Parse(reader, "resize", "5x6")));
        Assert.Equal(new RotateOperation(270), Assert.Single(ImagePipeline.Parse(reader, "rotate", "-90")));
        Assert.Equal(new FlipOperation(FlipAxis.Vertical), Assert.Single(ImagePipeline.Parse(reader, "flip", "v")));
        Assert.Equal(new PadOperation(new PadSpec(1, 2, 3, 4), Red), Assert.Single(ImagePipeline.Parse(padReader, "pad", "1,2,3,4")));
        Assert.Equal(new PadOperation(new PadSpec(7, 7, 7, 7), Red), Assert.Single(ImagePipeline.Parse(padReader, "pad", "7")));
        Assert.Equal(new PadOperation(new PadSpec(7, 7, 7, 7), null), Assert.Single(ImagePipeline.Parse(reader, "pad", "7")));
    }

    /// <summary>
    ///     Test that malformed operations and settings are usage errors, and an angle that is not a right angle says it is not supported yet.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">The value.</param>
    /// <param name="message">Text the message contains.</param>
    [Theory]
    [InlineData("--rotate", "45", "not supported yet")]
    [InlineData("--rotate", "0", "not supported yet")]
    [InlineData("--rotate", "360", "not supported yet")]
    [InlineData("--rotate", "12.5deg", "12.5 degrees is not supported yet")]
    [InlineData("--rotate", "left", "Invalid value 'left' for --rotate")]
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
    public void ImagePipeline_Parse_BadValue_ThrowsUsageError(string option, string value, string message)
    {
        // Arrange: pad-color needs a pad to be meaningful, so give it one
        var args = option == "--pad-color" ? new[] { "--pad", "1", option, value } : [option, value];

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

        // Assert
        Assert.Equal(ErrorCodes.UsageError, upscale.Code);
        Assert.Contains("--allow-upscale only applies with --fit", upscale.Message);
        Assert.Equal(ErrorCodes.UsageError, padColor.Code);
        Assert.Contains("--pad-color only applies with a pad", padColor.Message);
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
}
