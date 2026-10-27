// cspell:ignore canvasnet jpeg jpg tiff tif exif pdf pptx vsdx scad rotat deskew
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the image pipeline of the RenderCommand unit: the ordered operation options of <c>render</c> and
///     the verb commands <c>crop</c>, <c>resize</c>, <c>rotate</c>, <c>flip</c>, and <c>pad</c>.
/// </summary>
[Collection("Sequential")]
public class RenderCommandPipelineTests
{
    /// <summary>
    ///     Runs a command and captures what it writes.
    /// </summary>
    /// <param name="verb">The verb, or <see langword="null"/> for <c>render</c>.</param>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string Run(string? verb, bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new RenderCommand(verb);
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs a command and returns the error it fails with.
    /// </summary>
    /// <param name="verb">The verb, or <see langword="null"/> for <c>render</c>.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunFailing(string? verb, params string[] args)
    {
        using var context = Context.Create([]);
        var command = new RenderCommand(verb);
        return Assert.Throws<ToolException>(() => command.Run(context, args));
    }

    /// <summary>
    ///     Parses the operations of a JSON result into their <c>op</c> names.
    /// </summary>
    /// <param name="json">The JSON document.</param>
    /// <returns>The names in order.</returns>
    private static List<string?> OperationNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()).ToList();
    }

    /// <summary>
    ///     Writes a JPEG of the gradient with an EXIF orientation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="orientation">The orientation, 1 to 8.</param>
    /// <returns>The path.</returns>
    private static string WriteOrientedJpeg(TempDirectory directory, int orientation)
    {
        var path = directory.Combine("camera.jpg");
        using var surface = FixtureFactory.CreateGradient();
        using var stream = new MemoryStream();
        JpegCodec.Save(surface, stream);
        File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(stream.ToArray(), orientation, true));
        return path;
    }

    /// <summary>
    ///     Test that each verb describes itself: its name, no alias, the operand in its usage, and the options of render.
    /// </summary>
    /// <param name="verb">The verb.</param>
    /// <param name="operand">The operand placeholder.</param>
    [Theory]
    [InlineData("crop", "<geometry>")]
    [InlineData("resize", "<WxH>")]
    [InlineData("rotate", "<degrees>")]
    [InlineData("flip", "<axis>")]
    [InlineData("pad", "<border>")]
    public void RenderCommand_Verb_Metadata_DescribesVerbAndSharesRenderOptions(string verb, string operand)
    {
        // Arrange / Act
        var command = new RenderCommand(verb);

        // Assert: the verb's own name and usage, and the same options as render
        Assert.Equal(verb, command.Name);
        Assert.Empty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith($"canvasnet {verb} <input> {operand} -o <output> [options]", command.Usage);
        Assert.Contains("Examples:", command.Usage);
        Assert.Contains($"canvasnet {verb} ", command.Usage[(command.Usage.IndexOf("Examples:", StringComparison.Ordinal))..]);
        Assert.Equal(new RenderCommand().Options.Select(o => o.Name), command.Options.Select(o => o.Name));
        Assert.Equal(["convert"], new RenderCommand().Aliases);
    }

    /// <summary>
    ///     Test that only the known verbs can be constructed.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_UnknownVerb_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new RenderCommand("blur"));
    }

    /// <summary>
    ///     Test that every verb writes exactly the bytes of the equivalent render invocation, in two output formats.
    /// </summary>
    /// <param name="verb">The verb.</param>
    /// <param name="operand">The operand.</param>
    /// <param name="option">The render option that is equivalent.</param>
    /// <param name="extra">An extra option for both, or empty.</param>
    [Theory]
    [InlineData("crop", "2,1,7,5", "--crop", "")]
    [InlineData("crop", "50%,0,50%,100%", "--crop", "")]
    [InlineData("resize", "6x4", "--resize", "")]
    [InlineData("resize", "20x13", "--resize", "lanczos3")]
    [InlineData("rotate", "90", "--rotate", "")]
    [InlineData("rotate", "180", "--rotate", "")]
    [InlineData("rotate", "270", "--rotate", "")]
    [InlineData("flip", "h", "--flip", "")]
    [InlineData("flip", "v", "--flip", "")]
    [InlineData("flip", "both", "--flip", "")]
    [InlineData("pad", "3", "--pad", "")]
    [InlineData("pad", "1,2,3,4", "--pad", "#336699")]
    public void RenderCommand_Verb_EqualsRenderEquivalent_WritesIdenticalFiles(string verb, string operand, string option, string extra)
    {
        // Arrange: a PNG with transparency, so that padding and flattening show
        using var directory = new TempDirectory();
        var input = directory.Combine("in.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, input);
        }

        foreach (var extension in new[] { ".png", ".jpg" })
        {
            var viaVerb = directory.Combine("verb" + extension);
            var viaRender = directory.Combine("render" + extension);
            string[] settings = [];
            if (extra.StartsWith('#'))
            {
                settings = ["--pad-color", extra];
            }
            else if (extra.Length > 0)
            {
                settings = ["--filter", extra];
            }

            // Act: the verb with its operand, and render with the option
            var verbText = Run(verb, false, [input, operand, .. settings, "-o", viaVerb]);
            var renderText = Run(null, false, [input, option, operand, .. settings, "-o", viaRender]);

            // Assert: the same bytes and the same result line (apart from the output name)
            Assert.Equal(File.ReadAllBytes(viaRender), File.ReadAllBytes(viaVerb));
            Assert.Equal(renderText.Replace(viaRender, viaVerb, StringComparison.Ordinal), verbText);
        }
    }

    /// <summary>
    ///     Test that options after the verb's own operation are applied after it, in the order written, wherever they stand.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_ExtraPipelineOptions_AreAppliedAfterTheVerbOperation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var viaVerb = directory.Combine("verb.png");
        var viaRender = directory.Combine("render.png");
        var optionFirst = directory.Combine("first.png");

        // Act: crop as the verb, then resize and pad as options; and render with the same chain; and the options before the operand
        var json = Run("crop", true, input, "2,1,8,6", "--resize", "4x3", "--pad", "1", "--pad-color", "red", "-o", viaVerb);
        Run(null, false, input, "--crop", "2,1,8,6", "--resize", "4x3", "--pad", "1", "--pad-color", "red", "-o", viaRender);
        Run("crop", false, "--resize", "4x3", "--pad", "1", "--pad-color", "red", input, "2,1,8,6", "-o", optionFirst);

        // Assert: the same files, and the verb's operation comes first in the report
        Assert.Equal(File.ReadAllBytes(viaRender), File.ReadAllBytes(viaVerb));
        Assert.Equal(File.ReadAllBytes(viaRender), File.ReadAllBytes(optionFirst));
        Assert.Equal(["crop", "resize", "pad"], OperationNames(json));
        using var result = PngCodec.Load(viaVerb);
        Assert.Equal((6, 5), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that a verb's own operation is always first, even when the same option is also given.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_SameOptionAlsoGiven_VerbOperationIsFirst()
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act: crop to 8x6 as the verb, then crop that to 4x3 as an option
        Run("crop", false, input, "0,0,8,6", "--crop", "2,2,4,3", "-o", output);

        // Assert: the second crop is relative to the first result
        using var result = PngCodec.Load(output);
        using var original = FixtureFactory.CreateGradient();
        Assert.Equal((4, 3), (result.Width, result.Height));
        Assert.Equal(original[2, 2], result[0, 0]);
        Assert.Equal(original[5, 4], result[3, 2]);
    }

    /// <summary>
    ///     Test that a verb reports its own name in the JSON document and lists its operation.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_Json_NamesTheVerbAndListsItsOperation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var json = Run("rotate", true, input, "90", "-o", directory.Combine("out.png"));

        // Assert: command, and the operation with its parameters and the size of the result
        using var document = JsonDocument.Parse(json);
        Assert.Equal("rotate", document.RootElement.GetProperty("command").GetString());
        Assert.Contains("\"operations\":[{\"op\":\"rotate\",\"degrees\":90,\"width\":8,\"height\":12}]", json);
        Assert.Equal(8, document.RootElement.GetProperty("outputs")[0].GetProperty("width").GetInt32());
        Assert.Equal(12, document.RootElement.GetProperty("outputs")[0].GetProperty("height").GetInt32());
        Assert.Equal(12, document.RootElement.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32());
    }

    /// <summary>
    ///     Test that a chain in render lists every operation in order with its parameters, between the loader's and the writer's.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MultiOpJson_ListsOperationsInOrder()
    {
        // Arrange: a translucent PNG, written as JPEG so that the writer flattens last
        using var directory = new TempDirectory();
        var input = directory.Combine("translucent.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, input);
        }

        // Act: the loader enlarges to 24 wide, then the pipeline crops, flips, rotates, and pads
        var json = Run(
            null,
            true,
            input,
            "--width",
            "24",
            "--crop",
            "0,0,12,8",
            "--flip",
            "h",
            "--rotate",
            "90",
            "--pad",
            "1,2,3,4",
            "-o",
            directory.Combine("out.jpg"));

        // Assert: resize (loader), the four pipeline operations, flatten (writer) - exactly, with sizes
        Assert.Contains(
            "\"operations\":[{\"op\":\"resize\",\"width\":24,\"height\":16},"
            + "{\"op\":\"crop\",\"x\":0,\"y\":0,\"width\":12,\"height\":8,\"clamped\":false},"
            + "{\"op\":\"flip\",\"axis\":\"horizontal\",\"width\":12,\"height\":8},"
            + "{\"op\":\"rotate\",\"degrees\":90,\"width\":8,\"height\":12},"
            + "{\"op\":\"pad\",\"left\":1,\"top\":2,\"right\":3,\"bottom\":4,\"color\":\"#00000000\",\"width\":12,\"height\":18},"
            + "{\"op\":\"flatten\",\"background\":\"#FFFFFF\"}]",
            json);
        using var result = JpegCodec.Load(directory.Combine("out.jpg"));
        Assert.Equal((12, 18), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that a border on a raster image is transparent by default and takes the background when one is given.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PadOnRaster_DefaultsToTransparentThenBackground()
    {
        // Arrange: an opaque PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act: pad without and with a background; and with an explicit pad color that wins over the background
        Run(null, false, input, "--pad", "2", "-o", directory.Combine("plain.png"));
        var withBackground = Run(null, true, input, "--pad", "2", "--background", "#336699", "-o", directory.Combine("blue.png"));
        Run(null, false, input, "--pad", "2", "--background", "#336699", "--pad-color", "red", "-o", directory.Combine("red.png"));

        // Assert
        using var plain = PngCodec.Load(directory.Combine("plain.png"));
        using var blue = PngCodec.Load(directory.Combine("blue.png"));
        using var red = PngCodec.Load(directory.Combine("red.png"));
        Assert.Equal(new Rgba32(0, 0, 0, 0), plain[0, 0]);
        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), blue[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), red[0, 0]);
        Assert.Contains("\"color\":\"#336699\"", withBackground);
    }

    /// <summary>
    ///     Test that a border on a vector source is white by default, like the page, and transparent for a transparent background.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PadOnVector_DefaultsToTheBackgroundOfThePage()
    {
        // Arrange: the SVG fixture
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");

        // Act
        Run(null, false, svg, "--width", "50", "--pad", "3", "-o", directory.Combine("white.png"));
        Run(null, false, svg, "--width", "50", "--pad", "3", "--background", "transparent", "-o", directory.Combine("clear.png"));

        // Assert: 56x46 either way; the border is white, or transparent
        using var white = PngCodec.Load(directory.Combine("white.png"));
        using var clear = PngCodec.Load(directory.Combine("clear.png"));
        Assert.Equal((56, 46), (white.Width, white.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), white[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), clear[0, 0]);
    }

    /// <summary>
    ///     Test that the loader's sizing of a vector source is not applied a second time by the pipeline.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VectorWithWidthAndCrop_SizesOnceThenCrops()
    {
        // Arrange: the SVG is 100x80 naturally
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");

        // Act: render at 50 wide, then crop the top-left 10x10 of that
        var json = Run(null, true, svg, "--width", "50", "--crop", "0,0,10,10", "-o", directory.Combine("out.png"));

        // Assert: the render operation is at 50x40 (not scaled again) and the crop is last but for the flatten
        Assert.Contains("{\"op\":\"render\",\"width\":50,\"height\":40}", json);
        Assert.Equal(["render", "flatten", "crop"], OperationNames(json));
        using var result = PngCodec.Load(directory.Combine("out.png"));
        Assert.Equal((10, 10), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that the loader resizes a raster before the pipeline starts, so a crop sees the resized image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_RasterWithWidthAndCrop_LoaderResizesFirst()
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var viaLoader = directory.Combine("loader.png");
        var viaPipeline = directory.Combine("pipeline.png");

        // Act: enlarge to 24 wide with --width, then crop 12x8; the same with --resize in the pipeline
        Run(null, false, input, "--width", "24", "--filter", "nearest", "--crop", "0,0,12,8", "-o", viaLoader);
        Run(null, false, input, "--resize", "24x16", "--filter", "nearest", "--crop", "0,0,12,8", "-o", viaPipeline);

        // Assert: the same bytes, and the pixels are the enlarged top-left quarter, not the original corner
        Assert.Equal(File.ReadAllBytes(viaPipeline), File.ReadAllBytes(viaLoader));
        using var result = PngCodec.Load(viaLoader);
        using var original = FixtureFactory.CreateGradient();
        Assert.Equal(original[0, 0], result[0, 0]);
        Assert.Equal(original[0, 0], result[1, 1]);
        Assert.Equal(original[5, 3], result[11, 7]);
    }

    /// <summary>
    ///     Test that the filter of a raster size option is applied and reported only when chosen.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_FilterWithRasterWidth_IsReportedWhenChosen()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var plain = Run(null, true, input, "--width", "24", "-o", directory.Combine("plain.png"));
        var chosen = Run(null, true, input, "--width", "24", "--filter", "bilinear", "-o", directory.Combine("chosen.png"));

        // Assert
        Assert.Contains("{\"op\":\"resize\",\"width\":24,\"height\":16}", plain);
        Assert.Contains("{\"op\":\"resize\",\"width\":24,\"height\":16,\"filter\":\"bilinear\"}", chosen);
        Assert.NotEqual(File.ReadAllBytes(directory.Combine("plain.png")), File.ReadAllBytes(directory.Combine("chosen.png")));
    }

    /// <summary>
    ///     Test that order matters end to end: crop then resize is not resize then crop.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_OperationOrder_CropThenResizeDiffersFromResizeThenCrop()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var first = Run(null, true, input, "--crop", "0,0,6,4", "--resize", "12x8", "-o", directory.Combine("a.png"));
        var second = Run(null, true, input, "--resize", "12x8", "--crop", "0,0,6,4", "-o", directory.Combine("b.png"));

        // Assert: different operation order, different sizes, different files
        Assert.Equal(["crop", "resize"], OperationNames(first));
        Assert.Equal(["resize", "crop"], OperationNames(second));
        using var a = PngCodec.Load(directory.Combine("a.png"));
        using var b = PngCodec.Load(directory.Combine("b.png"));
        Assert.Equal((12, 8), (a.Width, a.Height));
        Assert.Equal((6, 4), (b.Width, b.Height));
    }

    /// <summary>
    ///     Test that fit shrinks into the box, leaves a small image alone, and enlarges with --allow-upscale, end to end.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_Fit_ShrinksOnlyUnlessUpscaleAllowed()
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        Run(null, false, input, "--fit", "6x6", "-o", directory.Combine("shrunk.png"));
        Run(null, false, input, "--fit", "100x100", "-o", directory.Combine("same.png"));
        Run(null, false, input, "--fit", "24x24", "--allow-upscale", "-o", directory.Combine("larger.png"));

        // Assert
        using var shrunk = PngCodec.Load(directory.Combine("shrunk.png"));
        using var same = PngCodec.Load(directory.Combine("same.png"));
        using var larger = PngCodec.Load(directory.Combine("larger.png"));
        using var original = FixtureFactory.CreateGradient();
        Assert.Equal((6, 4), (shrunk.Width, shrunk.Height));
        SurfaceAssert.Equal(original, same);
        Assert.Equal((24, 16), (larger.Width, larger.Height));
    }

    /// <summary>
    ///     Test that a rotation follows the automatic EXIF orientation of a JPEG.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ExifJpegThenRotate_RotatesTheUprightImage()
    {
        // Arrange: a 12x8 JPEG stored with the orientation 6 (turn 90 degrees clockwise to display), which is 8x12 upright
        using var directory = new TempDirectory();
        var input = WriteOrientedJpeg(directory, 6);

        // Act: rotate by 90 more (default auto-orient first); and the same turn written as the stored image rotated by 180
        var json = Run("rotate", true, input, "90", "-o", directory.Combine("rotated.png"));
        Run(null, false, input, "--no-auto-orient", "--rotate", "180", "-o", directory.Combine("expected.png"));

        // Assert: the upright image is 8x12, so the result is 12x8, and equals the stored image turned by 180 degrees
        Assert.Equal(["auto-orient", "rotate"], OperationNames(json));
        using var rotated = PngCodec.Load(directory.Combine("rotated.png"));
        using var expected = PngCodec.Load(directory.Combine("expected.png"));
        Assert.Equal((12, 8), (rotated.Width, rotated.Height));
        SurfaceAssert.Equal(expected, rotated);
    }

    /// <summary>
    ///     Test that a verb works on every kind of input.
    /// </summary>
    /// <param name="key">The kind.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("jpeg2000")]
    [InlineData("svg")]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    [InlineData("scad")]
    public void RenderCommand_Verb_EveryInputKind_RotatesNaturalSize(string key)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);
        var info = SourceLoader.Probe(input);
        var output = directory.Combine("out.png");

        // Act
        Run("rotate", false, input, "90", "-o", output);

        // Assert: the natural size with the sides swapped
        using var result = PngCodec.Load(output);
        Assert.Equal((info.Height, info.Width), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that a chain of several operations works on every kind of input, whatever its size.
    /// </summary>
    /// <param name="key">The kind.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("jpeg2000")]
    [InlineData("svg")]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    [InlineData("scad")]
    public void RenderCommand_Run_MultiOpChain_EveryInputKind_WritesExpectedSize(string key)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);
        var output = directory.Combine("out.png");

        // Act: the left half (a percentage, so it fits any size), mirrored, resized to 10x10, then a border of 1
        var json = Run(null, true, input, "--crop", "0,0,50%,100%", "--flip", "h", "--resize", "10x10", "--pad", "1", "-o", output);

        // Assert: 12x12 and the four operations in order
        using var result = PngCodec.Load(output);
        Assert.Equal((12, 12), (result.Width, result.Height));
        Assert.Equal(["crop", "flip", "resize", "pad"], OperationNames(json).Where(n => n is "crop" or "flip" or "resize" or "pad"));
    }

    /// <summary>
    ///     Test that the pipeline applies to every page with --all-pages and each operation names its page.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesWithPipeline_AppliesToEveryPage()
    {
        // Arrange: the two-page PDF (192x128 and 128x192 at 96 dpi)
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act: the top-left quarter of every page
        var json = Run(null, true, input, "--all-pages", "--crop", "0,0,50%,50%", "-o", directory.Combine("p-{page}.png"));

        // Assert: 96x64 and 64x96; the crop of each page names the page
        using var first = PngCodec.Load(directory.Combine("p-1.png"));
        using var second = PngCodec.Load(directory.Combine("p-2.png"));
        Assert.Equal((96, 64), (first.Width, first.Height));
        Assert.Equal((64, 96), (second.Width, second.Height));
        using var document = JsonDocument.Parse(json);
        var crops = document.RootElement.GetProperty("operations").EnumerateArray().Where(o => o.GetProperty("op").GetString() == "crop").ToList();
        Assert.Equal([1, 2], crops.Select(o => o.GetProperty("page").GetInt32()));
    }

    /// <summary>
    ///     Test that a transform of a document page with the verb uses the page and dpi options.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_DocumentPageAndDpi_AreHonored()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");
        var output = directory.Combine("out.png");

        // Act: page 2 at 192 dpi is 256x384; crop the left half
        Run("crop", false, input, "0,0,50%,100%", "--page", "2", "--dpi", "192", "-o", output);

        // Assert
        using var result = PngCodec.Load(output);
        Assert.Equal((128, 384), (result.Width, result.Height));
    }

    /// <summary>
    ///     Test that the same multi-operation command gives the same bytes on every run, in every output format.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MultiOp_IsDeterministic()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = directory.Combine("in.png");
        using (var surface = FixtureFactory.CreateTranslucent(31, 23))
        {
            PngCodec.Save(surface, input);
        }

        foreach (var extension in new[] { ".png", ".jpg", ".bmp", ".tif" })
        {
            var first = directory.Combine("first" + extension);
            var second = directory.Combine("second" + extension);
            string[] chain =
            [
                "--crop", "5%,5%,90%,90%", "--rotate", "270", "--resize", "17x13", "--filter", "lanczos3", "--flip", "both", "--pad", "2,1,2,1"
            ];

            // Act
            Run(null, false, [input, .. chain, "-o", first]);
            Run(null, false, [input, .. chain, "-o", second]);

            // Assert
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
    }

    /// <summary>
    ///     Test that a verb without its operand, with too many arguments, or without an output is a usage error.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_MissingOrExtraArguments_AreUsageErrors()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act
        var missingOperand = RunFailing("crop", input, "-o", output);
        var tooMany = RunFailing("flip", input, "h", "v", "-o", output);
        var noInput = RunFailing("rotate", "-o", output);
        var noOutput = RunFailing("pad", input, "4");

        // Assert: usage errors naming the verb and its operand; no output was written
        Assert.Equal(ErrorCodes.UsageError, missingOperand.Code);
        Assert.Contains("'crop' expects an input file and a <geometry>", missingOperand.Message);
        Assert.Equal(ErrorCodes.UsageError, tooMany.Code);
        Assert.Contains("'flip' expects", tooMany.Message);
        Assert.Equal(ErrorCodes.UsageError, noInput.Code);
        Assert.Equal(ErrorCodes.UsageError, noOutput.Code);
        Assert.Contains("canvasnet pad input.png <border> -o output.png", noOutput.Hint);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that bad operands and options are expected errors reported before anything is written.
    /// </summary>
    /// <param name="verb">The verb, or null for render.</param>
    /// <param name="code">The expected error code.</param>
    /// <param name="message">Text the message contains.</param>
    /// <param name="args">The arguments after the input.</param>
    [Theory]
    [InlineData("rotate", "usage-error", "Invalid value 'abc' for <degrees>", "abc")]
    [InlineData("rotate", "usage-error", "Invalid value 'sideways' for <degrees>", "sideways")]
    [InlineData("crop", "usage-error", "Invalid value '1,2' for <geometry>", "1,2")]
    [InlineData("crop", "usage-error", "outside the 12x8 image", "12,0,3,3")]
    [InlineData("crop", "usage-error", "outside the 5x5 image", "0,0,5,5", "--crop", "0,9,1,1")]
    [InlineData("resize", "usage-error", "Invalid value '640' for <size>", "640")]
    [InlineData("resize", "size-limit", "exceeds the maximum image dimension of 8192", "9000x10")]
    [InlineData("flip", "usage-error", "Invalid value 'x' for <axis>", "x")]
    [InlineData("pad", "usage-error", "Invalid value '1,2' for <border>", "1,2")]
    [InlineData("pad", "size-limit", "which is larger than the maximum of 8192", "8190")]
    [InlineData("crop", "usage-error", "--pad-color only applies with a pad", "0,0,2,2", "--pad-color", "red")]
    [InlineData("crop", "usage-error", "--rotate-fill only applies with a rotation or --deskew", "0,0,2,2", "--rotate-fill", "red")]
    [InlineData(null, "usage-error", "Invalid value 'abc' for --rotate", "--rotate", "abc")]
    [InlineData(null, "usage-error", "Invalid value '3' for --grid", "--grid", "3")]
    [InlineData(null, "usage-error", "Invalid value '5,5' for --levels", "--levels", "5,5")]
    [InlineData(null, "usage-error", "--trim-tolerance only applies with --trim", "--trim-tolerance", "5")]
    [InlineData(null, "usage-error", "--auto-levels-clip only applies with --auto-levels", "--auto-levels-clip", "5")]
    [InlineData(null, "usage-error", "Invalid value 'x' for --flip", "--flip", "x")]
    [InlineData(null, "usage-error", "--allow-upscale only applies with --fit", "--allow-upscale")]
    [InlineData(null, "usage-error", "Invalid value 'blurry' for --filter", "--filter", "blurry")]
    [InlineData(null, "usage-error", "Unknown option '--rotat'", "--rotat", "90")]
    [InlineData(null, "size-limit", "8192", "--pad", "8190")]
    public void RenderCommand_Run_BadPipeline_IsExpectedErrorAndWritesNothing(string? verb, string code, string message, params string[] args)
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act: verbs take the operand first (it is the first of args); render takes only options
        var error = RunFailing(verb, [input, .. args, "-o", output]);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Contains(message, error.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a failing step in the middle of a chain leaves no output, not even a partial one.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_FailingStep_LeavesExistingOutputUntouched()
    {
        // Arrange: an existing output
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "previous content");

        // Act: the pad is fine, the crop that follows starts outside the padded image
        var error = RunFailing(null, input, "--pad", "1", "--crop", "100,100,5,5", "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal("previous content", File.ReadAllText(output));
    }

    /// <summary>
    ///     Test that a border that pushes the image past the maximum dimension is refused with the size-limit code and a short message.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PadBeyondMaxDimension_IsRefusedWithSizeLimit()
    {
        // Arrange: a 12x8 image; a border of 4090 on each side makes 8192 x 8188 which fits; one more does not
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.bmp");

        // Act
        var error = RunFailing(null, input, "--pad", "4090,4090,4091,4090", "-o", output);

        // Assert: 12 + 4090 + 4091 = 8193
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.Contains("8193x8188", error.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a crop to the full image and a pad of zero are valid copies.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_IdentityOperations_KeepThePixels()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act
        Run(null, false, input, "--crop", "0,0,100%,100%", "--pad", "0", "--flip", "both", "--flip", "both", "-o", output);

        // Assert
        using var result = PngCodec.Load(output);
        using var original = FixtureFactory.CreateGradient();
        SurfaceAssert.Equal(original, result);
    }

    /// <summary>
    ///     Test that the help of render lists the pipeline options, and that the registry offers the verbs with help text.
    /// </summary>
    [Fact]
    public void RenderCommand_Help_ListsPipelineOptionsAndVerbs()
    {
        // Arrange
        var registry = CommandRegistry.CreateDefault();

        // Act
        var renderHelp = registry.RenderHelp("render");
        var topHelp = registry.RenderHelp();

        // Assert: every pipeline option in the render help, every verb in the command list and in its own help
        foreach (var option in new[]
                 {
                     "--crop", "--resize", "--fit", "--allow-upscale", "--rotate", "--flip", "--pad", "--pad-color", "--filter",
                     "--rotate-fill", "--grayscale", "--invert", "--threshold", "--levels", "--brightness", "--contrast",
                     "--auto-levels", "--auto-levels-clip", "--deskew", "--trim", "--trim-tolerance", "--grid"
                 })
        {
            Assert.Contains(option, renderHelp);
        }

        foreach (var verb in ImagePipeline.VerbNames)
        {
            Assert.Contains($"  {verb}", topHelp);
            Assert.Contains($"canvasnet {verb} <input>", registry.RenderHelp(verb));
            Assert.Same(registry.Find(verb), registry.Find(verb.ToUpperInvariant()));
        }
    }

    /// <summary>
    ///     Test that the photo options are declared once for render and the verbs and run in the order written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PhotoOptions_AppearInHelpAndRunInOrder()
    {
        // Arrange: every photo option is in the option table once, and in the usage text of render and the verbs
        string[] photoOptions =
        [
            "--rotate-fill", "--grayscale", "--invert", "--threshold", "--levels", "--brightness", "--contrast",
            "--auto-levels", "--auto-levels-clip", "--deskew", "--trim", "--trim-tolerance", "--grid"
        ];
        foreach (var option in photoOptions)
        {
            Assert.Single(RenderCommand.OptionTable, spec => spec.Name == option);
            Assert.Contains(option, new RenderCommand("rotate").Options.Select(o => o.Name));
        }

        Assert.Contains("--threshold", new RenderCommand().Usage);
        Assert.Contains("--deskew", new RenderCommand("rotate").Usage);

        // Act: run a chain on the PNG gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");
        var json = Run(null, true, input, "--grayscale", "--invert", "--brightness", "10", "--contrast", "5", "--threshold", "auto", "--grid", "8", "-o", output);

        // Assert: the operations come in the order written, and the pixels are black and white with the grid on top
        Assert.Equal(["grayscale", "invert", "brightness", "contrast", "threshold", "grid"], OperationNames(json).TakeLast(6));
        using var result = PngCodec.Load(output);
        Assert.Equal((12, 8), (result.Width, result.Height));

        // Away from the grid lines the pixels are pure black or white; on the line at x = 8 they are tinted
        Assert.True(result[3, 3].R is 0 or 255 && result[3, 3].G == result[3, 3].R && result[3, 3].B == result[3, 3].R);
        Assert.NotEqual(result[8, 3].R, result[8, 3].G);
    }

    /// <summary>
    ///     Test that the rotate verb accepts any angle and writes what render --rotate writes.
    /// </summary>
    /// <param name="operand">The angle.</param>
    [Theory]
    [InlineData("30")]
    [InlineData("-3.5")]
    [InlineData("45deg")]
    [InlineData("0")]
    [InlineData("-90")]
    public void RenderCommand_Run_RotateVerbAnyAngle_MatchesRenderEquivalent(string operand)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var viaVerb = directory.Combine("verb.png");
        var viaRender = directory.Combine("render.png");

        // Act
        Run("rotate", false, input, operand, "--rotate-fill", "white", "-o", viaVerb);
        Run(null, false, input, "--rotate", operand, "--rotate-fill", "white", "-o", viaRender);

        // Assert
        Assert.Equal(File.ReadAllBytes(viaRender), File.ReadAllBytes(viaVerb));
    }

    /// <summary>
    ///     Test that the JSON of a deskew reports the angle that was found, the correction, and the confidence.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_DeskewJson_ReportsAngleAndConfidence()
    {
        // Arrange: ruled lines that descend to the right by 5 degrees
        using var directory = new TempDirectory();
        var input = directory.Combine("tilted.png");
        using (var surface = FixtureFactory.SketchPhoto(400, 300, 5.0))
        {
            PngCodec.Save(surface, input);
        }

        var output = directory.Combine("level.png");

        // Act
        var json = Run(null, true, input, "--deskew", "-o", output);

        // Assert
        using var document = JsonDocument.Parse(json);
        var operation = document.RootElement.GetProperty("operations").EnumerateArray().Single(o => o.GetProperty("op").GetString() == "deskew");
        var skew = operation.GetProperty("skewDegrees").GetDouble();
        Assert.InRange(skew, 4.8, 5.2);
        Assert.Equal(-skew, operation.GetProperty("rotateDegrees").GetDouble());
        Assert.True(operation.GetProperty("confidence").GetDouble() >= ImageGeometry.MinConfidence);
        Assert.True(operation.GetProperty("applied").GetBoolean());
        var reported = document.RootElement.GetProperty("outputs")[0];
        using var result = PngCodec.Load(output);
        Assert.Equal(result.Width, reported.GetProperty("width").GetInt32());
        Assert.Equal(operation.GetProperty("width").GetInt32(), result.Width);
        Assert.True(result.Width > 400);
    }

    /// <summary>
    ///     Test that an oversized automatic deskew succeeds, leaves the output unchanged, and warns in text mode.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_DeskewTooLarge_SucceedsWithReasonAndWarning()
    {
        // Arrange: a two-degree skew whose expanded 8192-pixel-wide canvas exceeds the limit
        using var directory = new TempDirectory();
        var input = directory.Combine("wide.png");
        using (var surface = FixtureFactory.SketchPhoto(8192, 400, 2.0))
        {
            PngCodec.Save(surface, input);
        }

        var jsonOutput = directory.Combine("json.png");
        var textOutput = directory.Combine("text.png");

        // Act: run the same best-effort operation in JSON and normal output modes
        var json = Run(null, true, input, "--deskew", "-o", jsonOutput);
        using var context = Context.Create([]);
        var (text, diagnostics) = ToolHost.Capture(() =>
            new RenderCommand().Run(context, [input, "--deskew", "-o", textOutput]));

        // Assert: both commands succeed, preserve the size, report the reason, and warn without failing
        using var document = JsonDocument.Parse(json);
        var operation = document.RootElement.GetProperty("operations").EnumerateArray()
            .Single(o => o.GetProperty("op").GetString() == "deskew");
        Assert.False(operation.GetProperty("applied").GetBoolean());
        Assert.Equal("rotated image would exceed the maximum size", operation.GetProperty("reason").GetString());
        Assert.Equal(8192, operation.GetProperty("width").GetInt32());
        Assert.Equal(400, operation.GetProperty("height").GetInt32());
        using var result = PngCodec.Load(jsonOutput);
        Assert.Equal((8192, 400), (result.Width, result.Height));
        Assert.Contains("wrote ", text);
        Assert.Contains("warning: deskew was not applied: rotated image would exceed the maximum size", diagnostics);
    }

    /// <summary>
    ///     Test that the corners a rotation exposes are filled with --rotate-fill, else the --background, else white for an opaque image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_RotateFillAndBackground_ChooseFill()
    {
        // Arrange: an opaque gradient and a copy with a transparent pixel
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var holey = directory.Combine("holey.png");
        using (var surface = FixtureFactory.CreateGradient())
        {
            surface[0, 0] = new Rgba32(0, 0, 0, 0);
            PngCodec.Save(surface, holey);
        }

        // Act
        var byDefault = directory.Combine("default.png");
        var withFill = directory.Combine("fill.png");
        var withBackground = directory.Combine("background.png");
        var fillBeatsBackground = directory.Combine("both.png");
        var transparent = directory.Combine("transparent.png");
        Run(null, false, input, "--rotate", "45", "-o", byDefault);
        Run(null, false, input, "--rotate", "45", "--rotate-fill", "#00FF00", "-o", withFill);
        Run(null, false, input, "--rotate", "45", "--background", "#0000FF", "-o", withBackground);
        Run(null, false, input, "--rotate", "45", "--rotate-fill", "#00FF00", "--background", "#0000FF", "-o", fillBeatsBackground);
        Run(null, false, holey, "--rotate", "45", "-o", transparent);

        // Assert: the pixel in the top-left corner is the fill
        Assert.Equal(new Rgba32(255, 255, 255, 255), PixelAt(byDefault));
        Assert.Equal(new Rgba32(0, 255, 0, 255), PixelAt(withFill));
        Assert.Equal(new Rgba32(0, 0, 255, 255), PixelAt(withBackground));
        Assert.Equal(new Rgba32(0, 255, 0, 255), PixelAt(fillBeatsBackground));
        Assert.Equal(new Rgba32(0, 0, 0, 0), PixelAt(transparent));
    }

    /// <summary>
    ///     Reads the top-left pixel of a PNG file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 PixelAt(string path)
    {
        using var surface = PngCodec.Load(path);
        return surface[0, 0];
    }

    /// <summary>
    ///     Test that reading the source options from a command line gives the documented settings and defaults, as the render command always did.
    /// </summary>
    [Fact]
    public void RenderCommand_ReadSourceOptions_SameAsBefore()
    {
        // Arrange / Act
        var defaults = RenderCommand.ReadSourceOptions(ArgumentReader.Parse("render", [], RenderCommand.OptionTable));
        var given = RenderCommand.ReadSourceOptions(ArgumentReader.Parse(
            "render",
            ["--page", "2", "--dpi", "150", "--scale", "2", "--background", "#11223344", "--no-auto-orient", "--strict", "--filter", "lanczos3", "--view", "top"],
            RenderCommand.OptionTable));
        var sized = RenderCommand.ReadSourceOptions(ArgumentReader.Parse("render", ["--size", "40x30"], RenderCommand.OptionTable));

        // Assert
        Assert.Equal(1, defaults.Page);
        Assert.Null(defaults.Dpi);
        Assert.Null(defaults.Scale);
        Assert.Null(defaults.Width);
        Assert.Null(defaults.Background);
        Assert.True(defaults.AutoOrient);
        Assert.False(defaults.Strict);
        Assert.Equal(ResamplingMode.Auto, defaults.Filter);
        Assert.Null(defaults.Scad);

        Assert.Equal(2, given.Page);
        Assert.Equal(150, given.Dpi);
        Assert.Equal(2, given.Scale);
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x44), given.Background);
        Assert.False(given.AutoOrient);
        Assert.True(given.Strict);
        Assert.Equal(ResamplingMode.Lanczos3, given.Filter);
        Assert.Equal("top", given.Scad?.View);
        Assert.Equal((40, 30), (sized.Width, sized.Height));

        // The errors are the same usage errors as before
        var conflict = Assert.Throws<ToolException>(() => RenderCommand.ReadSourceOptions(
            ArgumentReader.Parse("render", ["--size", "40x30", "--scale", "2"], RenderCommand.OptionTable)));
        Assert.Equal(ErrorCodes.UsageError, conflict.Code);
        Assert.Contains("--size and --scale cannot be used together", conflict.Message);
        Assert.Throws<ToolException>(() => RenderCommand.ReadSourceOptions(
            ArgumentReader.Parse("render", ["--width", "0"], RenderCommand.OptionTable)));
    }
}
