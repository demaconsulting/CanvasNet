// cspell:ignore canvasnet luma otsu colours pixle frobnicate deskew
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the InspectCommand class: the subcommands <c>colors</c> and <c>pixel</c>, their options, results,
///     and errors, with a small image whose colors and statistics are known exactly.
/// </summary>
[Collection("Sequential")]
public class InspectCommandTests
{
    /// <summary>The red of the left three quarters of the test image.</summary>
    internal static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>The blue of the right quarter of the test image.</summary>
    internal static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     Runs the inspect command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    internal static string Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new InspectCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the inspect command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    internal static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new InspectCommand();
        return Assert.Throws<ToolException>(() => command.Run(context, args));
    }

    /// <summary>
    ///     Writes the test image: 20x10, red where x is below 15 and blue from 15 on.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The path of the PNG file.</returns>
    internal static string WriteTwoColors(TempDirectory directory)
    {
        var path = directory.Combine("two.png");
        using var surface = new Surface(20, 10);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                surface[x, y] = x < 15 ? Red : Blue;
            }
        }

        PngCodec.Save(surface, path);
        return path;
    }

    /// <summary>
    ///     Test that the two subcommands are dispatched and give their own results.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_ColorsAndPixel_AreDispatched()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act
        var colors = Run(false, "colors", input);
        var pixel = Run(false, "pixel", input, "3,4");
        var upperCase = Run(false, "COLORS", input);

        // Assert
        Assert.StartsWith($"colors of {input} (20x10): ", colors);
        Assert.StartsWith($"pixel 3,4 of {input} (20x10): ", pixel);
        Assert.Equal(colors, upperCase);
        var command = new InspectCommand();
        Assert.Equal("inspect", command.Name);
        Assert.Empty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
    }

    /// <summary>
    ///     Test that a missing or unknown subcommand is a usage error, with the closest subcommand suggested.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_MissingOrUnknownSubcommand_ThrowsUsageErrorWithSuggestion()
    {
        // Act
        var missing = RunFailing();
        var colours = RunFailing("colours", "x.png");
        var pixle = RunFailing("pixle", "x.png", "1,1");
        var unrelated = RunFailing("frobnicate");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, missing.Code);
        Assert.Contains("needs a subcommand: colors or pixel", missing.Message);
        Assert.Equal(ErrorCodes.UsageError, colours.Code);
        Assert.Contains("Unknown inspect subcommand 'colours'", colours.Message);
        Assert.Contains("Did you mean 'colors'?", colours.Hint);
        Assert.Contains("Did you mean 'pixel'?", pixle.Hint);
        Assert.Equal(ErrorCodes.UsageError, unrelated.Code);
        Assert.DoesNotContain("Did you mean", unrelated.Hint);
        Assert.Contains("colors, pixel", unrelated.Hint);
    }

    /// <summary>
    ///     Test that an option of the other subcommand, and a wrong number of operands, are usage errors.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_OptionOfOtherSubcommand_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act
        var radius = RunFailing("colors", input, "--radius", "2");
        var count = RunFailing("pixel", input, "1,1", "--count", "3");
        var noPoint = RunFailing("pixel", input);
        var extraPoint = RunFailing("colors", input, "1,1");
        var noInput = RunFailing("colors");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, radius.Code);
        Assert.Contains("--radius only applies to 'inspect pixel'", radius.Message);
        Assert.Equal(ErrorCodes.UsageError, count.Code);
        Assert.Contains("--count only applies to 'inspect colors'", count.Message);
        Assert.Contains("'inspect pixel' expects an input file and a point x,y, but 1 were given", noPoint.Message);
        Assert.Contains("'inspect colors' expects an input file, but 2 were given", extraPoint.Message);
        Assert.Contains("'inspect colors' expects an input file, but none were given", noInput.Message);
        Assert.All([radius, count, noPoint, extraPoint, noInput], error => Assert.Equal(ErrorCodes.UsageError, error.Code));
    }

    /// <summary>
    ///     Test that the options are those of render except the options of the output, plus the two of the subcommands.
    /// </summary>
    [Fact]
    public void InspectCommand_Options_SharedWithRenderExceptOutputOptions()
    {
        // Arrange
        string[] outputOptions = ["--output", "--format", "--quality", "--no-clobber", "--all-pages"];
        var renderNames = new RenderCommand().Options.Select(o => o.Name).ToList();

        // Act
        var inspect = new InspectCommand().Options;

        // Assert: the render options in their order without the output options, then --count and --radius
        Assert.Equal(
            [.. renderNames.Where(name => !outputOptions.Contains(name)), "--count", "--radius"],
            inspect.Select(o => o.Name));
        Assert.All(inspect.Take(inspect.Count - 2), option => Assert.Same(
            new RenderCommand().Options.Single(o => o.Name == option.Name),
            option));
        Assert.StartsWith("colors:", inspect.Single(o => o.Name == "--count").Description);
        Assert.StartsWith("pixel:", inspect.Single(o => o.Name == "--radius").Description);
        Assert.All(inspect, o => Assert.Equal(o.TakesValue, o.ValueName != null));
        foreach (var photo in new[] { "--deskew", "--trim", "--grid", "--threshold", "--rotate-fill", "--crop", "--background", "--page" })
        {
            Assert.Contains(photo, inspect.Select(o => o.Name));
        }

        // An output option is an unknown option
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);
        Assert.Equal(ErrorCodes.UsageError, RunFailing("colors", input, "-o", directory.Combine("x.png")).Code);
    }

    /// <summary>
    ///     Test that the help shows both subcommands, with examples, also through the registry.
    /// </summary>
    [Fact]
    public void InspectCommand_Help_ListsBothSubcommands()
    {
        // Act
        var usage = new InspectCommand().Usage;
        var help = CommandRegistry.CreateDefault().RenderHelp("inspect");

        // Assert
        Assert.StartsWith("canvasnet inspect colors <input> [--count N] [options]", usage);
        Assert.Contains("canvasnet inspect pixel <input> <x,y> [--radius R] [options]", usage);
        Assert.Contains("Examples:", usage);
        Assert.Contains("canvasnet inspect colors napkin.jpg --count 6", usage);
        Assert.Contains("canvasnet inspect colors <input>", help);
        Assert.Contains("canvasnet inspect pixel <input>", help);
        Assert.Contains("--count <N>", help);
        Assert.Contains("--radius <R>", help);
        Assert.Contains("--deskew", help);
    }

    /// <summary>
    ///     Test that <c>inspect colors</c> prints the colors and the luma statistics as two lines.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_Text_PrintsColorsAndLumaLines()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act
        var text = Run(false, "colors", input);

        // Assert: red 75 percent, blue 25 percent; the luma of red is 77 and of blue 29, so the mean is 65 and the Otsu
        // threshold is the middle of the gap, (29 + 76) / 2 = 52
        Assert.Equal(
            $"colors of {input} (20x10): #FF0000 75.00%, #0000FF 25.00%{Environment.NewLine}"
            + $"luma: mean 65.00, min 29, max 77, p1 29, p50 77, p99 77, otsu 52{Environment.NewLine}",
            text);
    }

    /// <summary>
    ///     Test that the JSON document of <c>inspect colors</c> has the documented fields in the documented order.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_Json_HasDocumentedShape()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act
        var stdOut = Run(true, "colors", input);

        // Assert: one line, and the fields in order
        Assert.Single(stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "ok", "command", "subcommand", "tool", "input", "operations", "image", "background", "colors", "luma", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("inspect", root.GetProperty("command").GetString());
        Assert.Equal("colors", root.GetProperty("subcommand").GetString());
        Assert.Equal("canvasnet", root.GetProperty("tool").GetProperty("name").GetString());
        Assert.Equal(input, root.GetProperty("input").GetProperty("path").GetString());
        Assert.Equal("png", root.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(20, root.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32());
        Assert.Equal(0, root.GetProperty("operations").GetArrayLength());
        Assert.Equal(200, root.GetProperty("image").GetProperty("pixels").GetInt64());
        Assert.Equal("#FFFFFF", root.GetProperty("background").GetString());

        var colors = root.GetProperty("colors").EnumerateArray().ToList();
        Assert.Equal(2, colors.Count);
        Assert.Equal("#FF0000", colors[0].GetProperty("hex").GetString());
        Assert.Equal((255, 0, 0), (colors[0].GetProperty("r").GetInt32(), colors[0].GetProperty("g").GetInt32(), colors[0].GetProperty("b").GetInt32()));
        Assert.Equal(150, colors[0].GetProperty("pixels").GetInt64());
        Assert.Equal(0.75, colors[0].GetProperty("fraction").GetDouble());
        Assert.Equal("#0000FF", colors[1].GetProperty("hex").GetString());
        Assert.Equal(0.25, colors[1].GetProperty("fraction").GetDouble());

        var luma = root.GetProperty("luma");
        Assert.Equal(65.0, luma.GetProperty("mean").GetDouble());
        Assert.Equal((29, 77, 29, 77, 77, 52), (
            luma.GetProperty("min").GetInt32(),
            luma.GetProperty("max").GetInt32(),
            luma.GetProperty("p1").GetInt32(),
            luma.GetProperty("p50").GetInt32(),
            luma.GetProperty("p99").GetInt32(),
            luma.GetProperty("otsu").GetInt32()));
        var histogram = luma.GetProperty("histogram").EnumerateArray().Select(e => e.GetInt64()).ToList();
        Assert.Equal(16, histogram.Count);
        Assert.Equal(50, histogram[1]);
        Assert.Equal(150, histogram[4]);
        Assert.Equal(200, histogram.Sum());
        Assert.Equal(0, root.GetProperty("diagnostics").GetArrayLength());
        Assert.Equal(0, root.GetProperty("echo").GetArrayLength());
    }

    /// <summary>
    ///     Test that --count limits the number of colors and is validated.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_Count_LimitsTheList()
    {
        // Arrange: three well separated colors
        using var directory = new TempDirectory();
        var input = directory.Combine("three.png");
        using (var surface = new Surface(9, 1))
        {
            for (var x = 0; x < 9; x++)
            {
                var color = Red;
                if (x >= 5)
                {
                    color = x < 8 ? Blue : new Rgba32(0, 255, 0, 255);
                }

                surface[x, 0] = color;
            }

            PngCodec.Save(surface, input);
        }

        // Act
        using var all = JsonDocument.Parse(Run(true, "colors", input));
        using var two = JsonDocument.Parse(Run(true, "colors", input, "--count", "2"));
        using var one = JsonDocument.Parse(Run(true, "colors", input, "--count", "1"));

        // Assert: the most common colors, in order
        Assert.Equal(["#FF0000", "#0000FF", "#00FF00"], all.RootElement.GetProperty("colors").EnumerateArray().Select(c => c.GetProperty("hex").GetString()));
        Assert.Equal(["#FF0000", "#0000FF"], two.RootElement.GetProperty("colors").EnumerateArray().Select(c => c.GetProperty("hex").GetString()));
        Assert.Equal(["#FF0000"], one.RootElement.GetProperty("colors").EnumerateArray().Select(c => c.GetProperty("hex").GetString()));
        Assert.Equal(ErrorCodes.UsageError, RunFailing("colors", input, "--count", "0").Code);
        Assert.Equal(ErrorCodes.UsageError, RunFailing("colors", input, "--count", "33").Code);
        Assert.Equal(ErrorCodes.UsageError, RunFailing("colors", input, "--count", "many").Code);
    }

    /// <summary>
    ///     Test that <c>inspect pixel</c> prints the hex color, the channels, the alpha, and the luma.
    /// </summary>
    [Fact]
    public void InspectCommand_Pixel_Text_PrintsHexRgbAlphaAndLuma()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act
        var red = Run(false, "pixel", input, "3,4");
        var blue = Run(false, "pixel", input, "19,9");

        // Assert
        Assert.Equal($"pixel 3,4 of {input} (20x10): #FF0000 rgb(255,0,0) alpha 255 luma 77{Environment.NewLine}", red);
        Assert.Equal($"pixel 19,9 of {input} (20x10): #0000FF rgb(0,0,255) alpha 255 luma 29{Environment.NewLine}", blue);
    }

    /// <summary>
    ///     Test that the JSON document of <c>inspect pixel</c> has the documented fields, and no neighborhood without a radius.
    /// </summary>
    [Fact]
    public void InspectCommand_Pixel_Json_HasDocumentedShape()
    {
        // Arrange: a translucent pixel is reported as stored, with the alpha in the hex color
        using var directory = new TempDirectory();
        var input = directory.Combine("alpha.png");
        using (var surface = new Surface(4, 3))
        {
            surface[2, 1] = new Rgba32(161, 178, 195, 128);
            PngCodec.Save(surface, input);
        }

        // Act
        var stdOut = Run(true, "pixel", input, "2,1");

        // Assert
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "ok", "command", "subcommand", "tool", "input", "operations", "image", "point", "pixel", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("pixel", root.GetProperty("subcommand").GetString());
        Assert.Equal((2, 1), (root.GetProperty("point").GetProperty("x").GetInt32(), root.GetProperty("point").GetProperty("y").GetInt32()));
        var pixel = root.GetProperty("pixel");
        Assert.Equal(["r", "g", "b", "a", "hex", "luma"], pixel.EnumerateObject().Select(p => p.Name));
        Assert.Equal((161, 178, 195, 128), (pixel.GetProperty("r").GetInt32(), pixel.GetProperty("g").GetInt32(), pixel.GetProperty("b").GetInt32(), pixel.GetProperty("a").GetInt32()));
        Assert.Equal("#A1B2C380", pixel.GetProperty("hex").GetString());

        // The luma of a translucent pixel is that of the pixel over white: Y = (77 * 161 + 150 * 178 + 29 * 195 + 128) >> 8 = 175
        // and (175 * 128 + 255 * 127 + 127) / 255 = 215
        Assert.Equal(215, pixel.GetProperty("luma").GetInt32());
        Assert.Equal(12, root.GetProperty("image").GetProperty("pixels").GetInt64());
    }

    /// <summary>
    ///     Test that a radius adds the clipped window, its mean, and its pixels to the text and the JSON.
    /// </summary>
    [Fact]
    public void InspectCommand_Pixel_RadiusWithJson_ListsNeighborhood()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act: a 3x3 window across the red/blue edge (columns 13 to 15), and one clipped at the corner
        var text = Run(false, "pixel", input, "14,5", "--radius", "1");
        using var edge = JsonDocument.Parse(Run(true, "pixel", input, "14,5", "--radius", "1"));
        using var corner = JsonDocument.Parse(Run(true, "pixel", input, "0,0", "--radius", "2"));

        // Assert: six red and three blue pixels have the mean (170, 0, 85)
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("mean of 3x3 at 13,4: #AA0055", lines[1]);
        var neighborhood = edge.RootElement.GetProperty("neighborhood");
        Assert.Equal((13, 4, 3, 3), (neighborhood.GetProperty("x").GetInt32(), neighborhood.GetProperty("y").GetInt32(), neighborhood.GetProperty("width").GetInt32(), neighborhood.GetProperty("height").GetInt32()));
        Assert.Equal("#AA0055", neighborhood.GetProperty("mean").GetString());
        var rows = neighborhood.GetProperty("pixels").EnumerateArray().Select(row => row.EnumerateArray().Select(p => p.GetString()).ToList()).ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(["#FF0000", "#FF0000", "#0000FF"], row));
        Assert.Equal(
            ["schemaVersion", "ok", "command", "subcommand", "tool", "input", "operations", "image", "point", "pixel", "neighborhood", "diagnostics", "echo"],
            edge.RootElement.EnumerateObject().Select(p => p.Name));

        // The window at the corner is clipped to 3x3 and starts at 0,0
        var clipped = corner.RootElement.GetProperty("neighborhood");
        Assert.Equal((0, 0, 3, 3), (clipped.GetProperty("x").GetInt32(), clipped.GetProperty("y").GetInt32(), clipped.GetProperty("width").GetInt32(), clipped.GetProperty("height").GetInt32()));
        Assert.Equal("#FF0000", clipped.GetProperty("mean").GetString());
    }

    /// <summary>
    ///     Test that a malformed point, a point outside the image, and a bad radius are usage errors.
    /// </summary>
    [Fact]
    public void InspectCommand_Pixel_BadOrOutsidePoint_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act / Assert: malformed
        foreach (var point in new[] { "abc", "1", "1,2,3", "1.5,2", "x,y", "," })
        {
            var error = RunFailing("pixel", input, point);
            Assert.Equal(ErrorCodes.UsageError, error.Code);
            Assert.Contains($"Invalid value '{point}' for <x,y>", error.Message);
        }

        // Outside the image, naming the point and the size
        foreach (var point in new[] { "20,0", "0,10", "5000,10", "-1,0" })
        {
            var error = RunFailing("pixel", input, "--", point);
            Assert.Equal(ErrorCodes.UsageError, error.Code);
            Assert.Contains("outside the 20x10 image", error.Message);
        }

        Assert.Equal(ErrorCodes.UsageError, RunFailing("pixel", input, "1,1", "--radius", "9").Code);
        Assert.Equal(ErrorCodes.UsageError, RunFailing("pixel", input, "1,1", "--radius", "-1").Code);
    }

    /// <summary>
    ///     Test that the operations of the pipeline run before the colors are analyzed.
    /// </summary>
    [Fact]
    public void InspectCommand_Colors_AfterCrop_AnalyzesTheCroppedRegion()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act: the right quarter is all blue; a grayscale makes it the gray of its luma
        using var cropped = JsonDocument.Parse(Run(true, "colors", input, "--crop", "15,0,5,10"));
        using var gray = JsonDocument.Parse(Run(true, "colors", input, "--crop", "15,0,5,10", "--grayscale"));

        // Assert: one color, 50 pixels, the size and the operation of the crop reported
        var root = cropped.RootElement;
        var color = Assert.Single(root.GetProperty("colors").EnumerateArray());
        Assert.Equal("#0000FF", color.GetProperty("hex").GetString());
        Assert.Equal(50, color.GetProperty("pixels").GetInt64());
        Assert.Equal(1.0, color.GetProperty("fraction").GetDouble());
        Assert.Equal((5, 10, 50), (root.GetProperty("image").GetProperty("width").GetInt32(), root.GetProperty("image").GetProperty("height").GetInt32(), (int)root.GetProperty("image").GetProperty("pixels").GetInt64()));
        Assert.Equal(["crop"], root.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()));
        Assert.Equal("#1D1D1D", Assert.Single(gray.RootElement.GetProperty("colors").EnumerateArray()).GetProperty("hex").GetString());
        Assert.Equal(["crop", "grayscale"], gray.RootElement.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()));
    }

    /// <summary>
    ///     Test that the point is a coordinate of the image after the pipeline.
    /// </summary>
    [Fact]
    public void InspectCommand_Pixel_AfterPipeline_UsesPipelineCoordinates()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);

        // Act: after the crop of the right quarter, 0,0 is blue, and 5,0 is outside; after a rotation by 90 degrees
        // the image is 10x20 and 0,19 is the first pixel of the last column of the original, which is blue
        var blue = Run(false, "pixel", input, "0,0", "--crop", "15,0,5,10");
        var outside = RunFailing("pixel", input, "5,0", "--crop", "15,0,5,10");
        var rotated = Run(false, "pixel", input, "0,19", "--rotate", "90");
        var rotatedRed = Run(false, "pixel", input, "0,0", "--rotate", "90");

        // Assert
        Assert.Contains("(5x10): #0000FF", blue);
        Assert.Equal(ErrorCodes.UsageError, outside.Code);
        Assert.Contains("outside the 5x10 image", outside.Message);
        Assert.Contains("(10x20): #0000FF", rotated);
        Assert.Contains("(10x20): #FF0000", rotatedRed);
    }

    /// <summary>
    ///     Test that running the command writes no file.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_WritesNoFiles()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);
        var before = Directory.GetFileSystemEntries(directory.Path).Order().ToList();

        // Act: both subcommands, with pipeline operations that would write a picture elsewhere, as text and as JSON
        Run(false, "colors", input, "--grid", "8", "--trim");
        Run(true, "pixel", input, "1,1", "--radius", "2", "--deskew");
        Run(false, "colors", input, "--rotate", "30");

        // Assert: nothing was created in the directory, and the input is as it was
        Assert.Equal(before, Directory.GetFileSystemEntries(directory.Path).Order());
        using var original = PngCodec.Load(input);
        Assert.Equal(Red, original[0, 0]);
    }

    /// <summary>
    ///     Test that failures are expected errors with stable codes.
    /// </summary>
    [Fact]
    public void InspectCommand_Errors_AreExpectedErrorsWithStableCodes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteTwoColors(directory);
        var text = directory.Combine("notes.txt");
        File.WriteAllText(text, "not an image");

        // Act
        var missing = RunFailing("colors", directory.Combine("absent.png"));
        var unsupported = RunFailing("colors", text);
        var badOption = RunFailing("colors", input, "--rotate", "abc");
        var unknownOption = RunFailing("colors", input, "--no-such-option");
        var badPage = RunFailing("pixel", input, "0,0", "--page", "2");
        var modifier = RunFailing("colors", input, "--rotate-fill", "red");
        var modelOption = RunFailing("colors", input, "--view", "top");
        var tooLarge = RunFailing("colors", input, "--rotate", "1", "--size", "8192x100");

        // Assert
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.Equal(ErrorCodes.UnsupportedFormat, unsupported.Code);
        Assert.Equal(ErrorCodes.UsageError, badOption.Code);
        Assert.Contains("Invalid value 'abc' for --rotate", badOption.Message);
        Assert.Equal(ErrorCodes.UsageError, unknownOption.Code);
        Assert.Equal(ErrorCodes.PageOutOfRange, badPage.Code);
        Assert.Equal(ErrorCodes.UsageError, modifier.Code);
        Assert.Contains("--rotate-fill only applies with a rotation", modifier.Message);
        Assert.Equal(ErrorCodes.UsageError, modelOption.Code);
        Assert.Contains("only apply to an OpenSCAD model", modelOption.Message);
        Assert.Equal(ErrorCodes.SizeLimit, tooLarge.Code);
    }

    /// <summary>
    ///     Test that the JSON result is the same bytes every time.
    /// </summary>
    [Fact]
    public void InspectCommand_Json_SameInputTwice_IsByteIdentical()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = directory.Combine("sketch.png");
        using (var surface = FixtureFactory.SketchPhoto(160, 100, 3.0))
        {
            PngCodec.Save(surface, input);
        }

        // Act
        var firstColors = Run(true, "colors", input, "--auto-levels", "--deskew", "--count", "5");
        var secondColors = Run(true, "colors", input, "--auto-levels", "--deskew", "--count", "5");
        var firstPixel = Run(true, "pixel", input, "20,30", "--radius", "3", "--deskew");
        var secondPixel = Run(true, "pixel", input, "20,30", "--radius", "3", "--deskew");

        // Assert
        Assert.Equal(firstColors, secondColors);
        Assert.Equal(firstPixel, secondPixel);
        Assert.Contains("\"op\":\"deskew\"", firstColors);
    }
}
