// cspell:ignore canvasnet deskew deskewed otsu luma grayscale napkin
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the photo tools: the tone and geometry operations of <c>render</c> (and the verb
///     <c>rotate</c>) and the <c>inspect</c> commands. Most run the tool as a real process; the tests that examine
///     standard output and standard error separately run it in-process.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     The paper color that the sketch fixtures use (the gray level 200, as <c>#C8C8C8</c>).
    /// </summary>
    private const string PaperHex = "#C8C8C8";

    /// <summary>
    ///     Saves a surface as a PNG file and disposes it.
    /// </summary>
    /// <param name="surface">The surface; it is disposed.</param>
    /// <param name="path">The path of the file.</param>
    /// <returns>The path.</returns>
    private static string SavePng(Surface surface, string path)
    {
        using (surface)
        {
            PngCodec.Save(surface, path);
        }

        return path;
    }

    /// <summary>
    ///     Gets the operations of a JSON result of the tool.
    /// </summary>
    /// <param name="root">The root element of the document.</param>
    /// <returns>The elements of the <c>operations</c> array.</returns>
    private static List<JsonElement> OperationsOf(JsonElement root)
    {
        return [.. root.GetProperty("operations").EnumerateArray()];
    }

    /// <summary>
    ///     Test that the grayscale, threshold, levels, and brightness operations write the pixels their formulas give.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_GrayscaleThresholdLevels_WriteExpectedPixels()
    {
        // Arrange: six pixels in a row: red, green, blue, and the grays 200, 100, and 50
        using var directory = new TempDirectory();
        var surface = new Surface(6, 1);
        Rgba32[] colors =
        [
            new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255),
            new(200, 200, 200, 255), new(100, 100, 100, 255), new(50, 50, 50, 255)
        ];
        for (var x = 0; x < colors.Length; x++)
        {
            surface[x, 0] = colors[x];
        }

        var input = SavePng(surface, directory.Combine("in.png"));
        Rgba32 Gray(int level) => new((byte)level, (byte)level, (byte)level, 255);

        // Act: each operation as a real process
        var outputs = new Dictionary<string, string>();
        foreach (var (name, options) in new (string Name, string[] Options)[]
                 {
                     ("gray", ["--grayscale"]),
                     ("threshold", ["--threshold", "100"]),
                     ("levels", ["--levels", "50,150"]),
                     ("brightness", ["--brightness", "20"])
                 })
        {
            var output = directory.Combine($"{name}.png");
            var exitCode = Runner.Run(out _, "dotnet", [_dllPath, "render", input, .. options, "-o", output]);
            Assert.Equal(0, exitCode);
            outputs[name] = output;
        }

        // Assert: luma weights (77, 150, 29)/256; white only above the level; the black and white points; the offset
        using (var gray = PngCodec.Load(outputs["gray"]))
        {
            Assert.Equal([Gray(77), Gray(149), Gray(29), Gray(200), Gray(100), Gray(50)], Enumerable.Range(0, 6).Select(x => gray[x, 0]));
        }

        using (var thresholded = PngCodec.Load(outputs["threshold"]))
        {
            Assert.Equal(
                [Gray(0), Gray(255), Gray(0), Gray(255), Gray(0), Gray(0)],
                Enumerable.Range(0, 6).Select(x => thresholded[x, 0]));
        }

        using (var levels = PngCodec.Load(outputs["levels"]))
        {
            Assert.Equal([Gray(255), Gray(128), Gray(0)], Enumerable.Range(3, 3).Select(x => levels[x, 0]));
        }

        using var brightened = PngCodec.Load(outputs["brightness"]);
        Assert.Equal([Gray(220), Gray(120), Gray(70)], Enumerable.Range(3, 3).Select(x => brightened[x, 0]));
    }

    /// <summary>
    ///     Test that auto-levels stretches a low-contrast sketch to the full range and reports the points it found.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_AutoLevels_StretchesContrastAndReportsPoints()
    {
        // Arrange: paper at 200 and ink at 100 (8 percent of the pixels), lines level
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(160, 100, 0, lines: 4, paperLevel: 200, inkContrast: 100), directory.Combine("in.png"));
        var output = directory.Combine("out.png");

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--auto-levels", "-o", output, "--json");

        // Assert: the points are the ink and the paper, and the pictures' ends are black and white
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(text);
        var operation = Assert.Single(OperationsOf(document.RootElement));
        Assert.Equal("auto-levels", operation.GetProperty("op").GetString());
        Assert.True(operation.GetProperty("applied").GetBoolean());
        Assert.Equal(100, operation.GetProperty("black").GetInt32());
        Assert.Equal(200, operation.GetProperty("white").GetInt32());
        using var stretched = PngCodec.Load(output);
        Assert.Equal(new Rgba32(0, 0, 0, 255), stretched[50, 19]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), stretched[50, 5]);
    }

    /// <summary>
    ///     Test that rotating by an oblique angle grows the canvas to hold the image and fills the corners.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_RotateObliqueAngle_ExpandsCanvasAndFillsCorners()
    {
        // Arrange: an opaque 160x100 sketch
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(160, 100, 0, lines: 4), directory.Combine("in.png"));
        var filled = directory.Combine("filled.png");
        var defaulted = directory.Combine("default.png");

        // Act: with a fill color, and with the default fill of an opaque image
        var filledExit = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--rotate", "30", "--rotate-fill", "#ff0000", "-o", filled, "--json");
        var defaultExit = Runner.Run(out _, "dotnet", _dllPath, "render", input, "--rotate", "30", "-o", defaulted);

        // Assert: 189x167 (160 cos 30 + 100 sin 30 by 160 sin 30 + 100 cos 30, rounded up), red corners, white by default,
        // and the paper at the center
        Assert.Equal(0, filledExit);
        Assert.Equal(0, defaultExit);
        using var document = JsonDocument.Parse(text);
        var operation = Assert.Single(OperationsOf(document.RootElement));
        Assert.Equal("rotate", operation.GetProperty("op").GetString());
        Assert.Equal(30.0, operation.GetProperty("degrees").GetDouble());
        Assert.Equal((189, 167), (operation.GetProperty("width").GetInt32(), operation.GetProperty("height").GetInt32()));
        using var rotated = PngCodec.Load(filled);
        Assert.Equal((189, 167), (rotated.Width, rotated.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), rotated[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), rotated[188, 166]);
        Assert.Equal(new Rgba32(200, 200, 200, 255), rotated[94, 83]);
        using var white = PngCodec.Load(defaulted);
        Assert.Equal(new Rgba32(255, 255, 255, 255), white[0, 0]);
    }

    /// <summary>
    ///     Test that the rotate verb with a fill color writes exactly the file the equivalent render writes, for a
    ///     positive and a negative angle.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_RotateVerbWithFill_MatchesRenderEquivalent()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(120, 80, 0, lines: 3), directory.Combine("in.png"));

        foreach (var angle in new[] { "30", "-12.5" })
        {
            var viaVerb = directory.Combine($"verb{angle}.png");
            var viaRender = directory.Combine($"render{angle}.png");

            // Act: the verb as a real process, render in-process
            var verbExit = Runner.Run(out var text, "dotnet", _dllPath, "rotate", input, angle, "--rotate-fill", "#00ff00", "-o", viaVerb);
            var render = ToolHost.Invoke("render", input, "--rotate", angle, "--rotate-fill", "#00ff00", "-o", viaRender);

            // Assert: success, one result line, the same bytes
            Assert.Equal(0, verbExit);
            Assert.Equal(0, render.ExitCode);
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(File.ReadAllBytes(viaRender), File.ReadAllBytes(viaVerb));
        }
    }

    /// <summary>
    ///     Test that deskew finds the tilt of the lines, reports the angle, the correction, and the confidence in the
    ///     JSON result, and leaves lines that a second deskew finds level.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_Deskew_StraightensTiltedLinesAndReportsAngleInJson()
    {
        // Arrange: six lines tilted 4 degrees (descending to the right) on gray paper
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(320, 200, 4.0, lines: 6), directory.Combine("in.png"));
        var leveled = directory.Combine("leveled.png");

        // Act: deskew with a fill that is the paper, so that the rotated corners add no edge; then deskew the result
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--deskew", "--rotate-fill", PaperHex, "-o", leveled, "--json");
        var again = Runner.Run(out var againText, "dotnet", _dllPath, "render", leveled, "--deskew", "-o", directory.Combine("again.png"), "--json");

        // Assert: the tilt, the opposite correction, a confidence that passes, and level lines afterwards
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(text);
        var operation = Assert.Single(OperationsOf(document.RootElement));
        Assert.Equal("deskew", operation.GetProperty("op").GetString());
        Assert.True(operation.GetProperty("applied").GetBoolean());
        var skew = operation.GetProperty("skewDegrees").GetDouble();
        Assert.InRange(skew, 3.5, 4.5);
        Assert.Equal(-skew, operation.GetProperty("rotateDegrees").GetDouble());
        Assert.InRange(operation.GetProperty("confidence").GetDouble(), 0.4, 1.0);
        Assert.Equal(0, again);
        using var againDocument = JsonDocument.Parse(againText);
        var remaining = Assert.Single(OperationsOf(againDocument.RootElement)).GetProperty("skewDegrees").GetDouble();
        Assert.InRange(Math.Abs(remaining), 0.0, 0.5);
    }

    /// <summary>
    ///     Test that trim crops to the content inside a uniform border, ignores differences within the tolerance, and
    ///     reports the box.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_Trim_CropsUniformBorder()
    {
        // Arrange: a 100x80 block of gray at 30,20 on a white 160x120 image, with a ring of near-white around the block
        using var directory = new TempDirectory();
        var surface = new Surface(160, 120);
        for (var y = 0; y < 120; y++)
        {
            for (var x = 0; x < 160; x++)
            {
                var inBlock = x is >= 30 and < 130 && y is >= 20 and < 100;
                var inRing = !inBlock && x is >= 29 and < 131 && y is >= 19 and < 101;
                var level = 255;
                if (inBlock)
                {
                    level = 120;
                }
                else if (inRing)
                {
                    level = 250;
                }

                surface[x, y] = new Rgba32((byte)level, (byte)level, (byte)level, 255);
            }
        }

        var input = SavePng(surface, directory.Combine("in.png"));
        var trimmed = directory.Combine("trimmed.png");
        var strict = directory.Combine("strict.png");

        // Act: the default tolerance (8) ignores the ring; a tolerance of 2 keeps it
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--trim", "-o", trimmed, "--json");
        var strictExit = Runner.Run(out var strictText, "dotnet", _dllPath, "render", input, "--trim", "--trim-tolerance", "2", "-o", strict, "--json");

        // Assert: the boxes, and the cropped pictures
        Assert.Equal(0, exitCode);
        Assert.Equal(0, strictExit);
        using var document = JsonDocument.Parse(text);
        var operation = Assert.Single(OperationsOf(document.RootElement));
        Assert.Equal("trim", operation.GetProperty("op").GetString());
        Assert.Equal((30, 20, 100, 80), (operation.GetProperty("x").GetInt32(), operation.GetProperty("y").GetInt32(), operation.GetProperty("width").GetInt32(), operation.GetProperty("height").GetInt32()));
        Assert.True(operation.GetProperty("trimmed").GetBoolean());
        using var cropped = PngCodec.Load(trimmed);
        Assert.Equal((100, 80), (cropped.Width, cropped.Height));
        Assert.Equal(new Rgba32(120, 120, 120, 255), cropped[0, 0]);
        using var strictDocument = JsonDocument.Parse(strictText);
        var strictOperation = Assert.Single(OperationsOf(strictDocument.RootElement));
        Assert.Equal((102, 82), (strictOperation.GetProperty("width").GetInt32(), strictOperation.GetProperty("height").GetInt32()));
    }

    /// <summary>
    ///     Test that the grid draws one-pixel lines at multiples of the spacing and labels, over a copy that leaves the
    ///     input alone, and reports the lines and labels.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_Grid_DrawsLabeledLinesAtSpacing()
    {
        // Arrange: a sketch of 160x100 with level lines
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(160, 100, 0, lines: 4), directory.Combine("in.png"));
        var before = File.ReadAllBytes(input);
        var output = directory.Combine("grid.png");

        // Act: an opaque red grid every 20 pixels
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--grid", "20,#FF0000", "-o", output, "--json");

        // Assert: the report counts, red on the lines and the paper between them, a label box at the origin, and the input unchanged
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(text);
        var operation = Assert.Single(OperationsOf(document.RootElement));
        Assert.Equal("grid", operation.GetProperty("op").GetString());
        Assert.Equal(20, operation.GetProperty("spacing").GetInt32());
        Assert.Equal(8, operation.GetProperty("verticalLines").GetInt32());
        Assert.Equal(5, operation.GetProperty("horizontalLines").GetInt32());
        Assert.True(operation.GetProperty("labels").GetInt32() > 0);
        using var original = SurfaceAssert.Load(input);
        using var gridded = PngCodec.Load(output);
        var red = new Rgba32(255, 0, 0, 255);
        Assert.Equal(red, gridded[40, 50]);
        Assert.Equal(red, gridded[100, 60]);
        Assert.Equal(original[25, 50], gridded[25, 50]);
        Assert.NotEqual(original[2, 2], gridded[2, 2]);
        Assert.Equal(before, File.ReadAllBytes(input));
    }

    /// <summary>
    ///     Test that inspect colors reports the dominant colors with their counts and shares and the luma statistics as
    ///     JSON, and writes no file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Inspect_Colors_ReportsDominantColorsAndLumaAsJson()
    {
        // Arrange: 100 pixels, 70 red and 30 blue
        using var directory = new TempDirectory();
        var surface = new Surface(10, 10);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                surface[x, y] = y < 7 ? new Rgba32(255, 0, 0, 255) : new Rgba32(0, 0, 255, 255);
            }
        }

        var input = SavePng(surface, directory.Combine("in.png"));

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "inspect", "colors", input, "--json");
        var limited = Runner.Run(out var limitedText, "dotnet", _dllPath, "inspect", "colors", input, "--count", "1", "--json");

        // Assert: one document; the two colors in pixel order with fractions; luma 77 and 29, mean 62.6; no file but the input
        Assert.Equal(0, exitCode);
        Assert.Equal(0, limited);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("inspect", root.GetProperty("command").GetString());
        Assert.Equal("colors", root.GetProperty("subcommand").GetString());
        var colors = root.GetProperty("colors").EnumerateArray().ToList();
        Assert.Equal(["#FF0000", "#0000FF"], colors.Select(c => c.GetProperty("hex").GetString()));
        Assert.Equal([70L, 30L], colors.Select(c => c.GetProperty("pixels").GetInt64()));
        Assert.Equal(0.7, colors[0].GetProperty("fraction").GetDouble(), 6);
        var luma = root.GetProperty("luma");
        Assert.Equal((29, 77), (luma.GetProperty("min").GetInt32(), luma.GetProperty("max").GetInt32()));
        Assert.Equal(62.6, luma.GetProperty("mean").GetDouble(), 2);
        Assert.Equal(16, luma.GetProperty("histogram").GetArrayLength());
        using var limitedDocument = JsonDocument.Parse(limitedText);
        Assert.Single(limitedDocument.RootElement.GetProperty("colors").EnumerateArray());
        Assert.Equal([input], Directory.GetFiles(directory.Path));
    }

    /// <summary>
    ///     Test that inspect pixel reports the value of a pixel and the window around it in the coordinates of the
    ///     image after the pipeline.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Inspect_Pixel_ReportsValueAndNeighborhood()
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        using var gradient = PngCodec.Load(input);
        var expected = gradient[3, 2];

        // Act: the pixel with a radius of one, as JSON; as text; and after a crop that moves it to 1,1
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "inspect", "pixel", input, "3,2", "--radius", "1", "--json");
        var textExit = Runner.Run(out var plain, "dotnet", _dllPath, "inspect", "pixel", input, "3,2");
        var cropExit = Runner.Run(out var cropText, "dotnet", _dllPath, "inspect", "pixel", input, "1,1", "--crop", "2,1,4,4", "--json");

        // Assert: the value, the clipped window of 3x3 at 2,1 and its mean, the text line, and the pipeline coordinates
        Assert.Equal(0, exitCode);
        Assert.Equal(0, textExit);
        Assert.Equal(0, cropExit);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal("pixel", root.GetProperty("subcommand").GetString());
        Assert.Equal((3, 2), (root.GetProperty("point").GetProperty("x").GetInt32(), root.GetProperty("point").GetProperty("y").GetInt32()));
        var pixel = root.GetProperty("pixel");
        Assert.Equal((expected.R, expected.G, expected.B, expected.A), ((byte)pixel.GetProperty("r").GetInt32(), (byte)pixel.GetProperty("g").GetInt32(), (byte)pixel.GetProperty("b").GetInt32(), (byte)pixel.GetProperty("a").GetInt32()));
        Assert.Equal($"#{expected.R:X2}{expected.G:X2}{expected.B:X2}", pixel.GetProperty("hex").GetString());
        var window = root.GetProperty("neighborhood");
        Assert.Equal((2, 1, 3, 3), (window.GetProperty("x").GetInt32(), window.GetProperty("y").GetInt32(), window.GetProperty("width").GetInt32(), window.GetProperty("height").GetInt32()));
        Assert.Equal(3, window.GetProperty("pixels").GetArrayLength());
        Assert.StartsWith("pixel 3,2 of ", plain);
        Assert.Contains($"#{expected.R:X2}{expected.G:X2}{expected.B:X2}", plain);
        using var cropDocument = JsonDocument.Parse(cropText);
        Assert.Equal(
            $"#{expected.R:X2}{expected.G:X2}{expected.B:X2}",
            cropDocument.RootElement.GetProperty("pixel").GetProperty("hex").GetString());
    }

    /// <summary>
    ///     Test that a missing or unknown subcommand, an option of the other subcommand, and a bad or outside point are
    ///     expected errors that write nothing.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Inspect_Errors_AreReportedAsExpectedErrors()
    {
        // Arrange: the 12x8 gradient
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        (string[] Args, string Code, string Text)[] cases =
        [
            (["inspect"], "usage-error", "subcommand"),
            (["inspect", "colours", input], "usage-error", "colors"),
            (["inspect", "colors", input, "--radius", "2"], "usage-error", "--radius"),
            (["inspect", "pixel", input, "5000,10"], "usage-error", "outside"),
            (["inspect", "pixel", input, "abc"], "usage-error", "abc"),
            (["inspect", "pixel", input, "1,1", "--count", "3"], "usage-error", "--count"),
            (["inspect", "colors", directory.Combine("missing.png")], "input-not-found", "missing.png")
        ];

        foreach (var (args, code, text) in cases)
        {
            // Act: in-process, with JSON so the document can be checked
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke([.. args, "--json"]);

            // Assert: exit code 1, one concise error line with the code, and the code in the document
            Assert.Equal(1, exitCode);
            Assert.StartsWith($"Error: {code}:", stdErr);
            Assert.Contains(text, stdErr);
            Assert.DoesNotContain("   at ", stdErr);
            using var document = JsonDocument.Parse(stdOut);
            Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }

    /// <summary>
    ///     Test that the help of the tool lists the inspect command and the photo options of render.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Help_ListsInspectAndPhotoOptions()
    {
        // Act
        var top = ToolHost.Invoke("--help");
        var inspect = ToolHost.Invoke("inspect", "--help");
        var render = ToolHost.Invoke("render", "--help");

        // Assert
        Assert.Equal(0, top.ExitCode);
        Assert.Contains("  inspect", top.StdOut);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Contains("canvasnet inspect colors <input>", inspect.StdOut);
        Assert.Contains("canvasnet inspect pixel <input> <x,y>", inspect.StdOut);
        Assert.Equal(0, render.ExitCode);
        foreach (var option in new[]
                 {
                     "--grayscale", "--invert", "--threshold", "--levels", "--brightness", "--contrast", "--auto-levels",
                     "--auto-levels-clip", "--deskew", "--trim", "--trim-tolerance", "--grid", "--rotate-fill"
                 })
        {
            Assert.Contains(option, render.StdOut);
        }
    }

    /// <summary>
    ///     Test the workflow of the user guide: a tilted, low-contrast photograph of a sketch is shrunk, stretched,
    ///     straightened, trimmed, and gridded in one command; the operations are applied in order and reported, and the
    ///     inspect commands read the result.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_NapkinWorkflow_ChainedOperationsApplyInOrderAndReport()
    {
        // Arrange: a 400x300 photograph: paper at 200, low-contrast ink at 100, five lines tilted 3 degrees
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(400, 300, 3.0, lines: 5, paperLevel: 200, inkContrast: 100), directory.Combine("napkin.png"));
        var sketch = directory.Combine("sketch.png");
        var grid = directory.Combine("sketch-grid.png");

        // Act: step 3 of the recipe (fit, auto-levels, deskew, trim), the grid, and the two inspect commands
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "--fit", "300x300", "--auto-levels", "--deskew", "--trim", "-o", sketch, "--json");
        var gridExit = Runner.Run(out var gridText, "dotnet", _dllPath, "render", sketch, "--grid", "50", "-o", grid, "--json");
        var colorsExit = Runner.Run(out var colorsText, "dotnet", _dllPath, "inspect", "colors", input, "--auto-levels", "--count", "2", "--json");

        // Assert: the operations in order; the lines were straightened; the trim shrank the picture to the drawing
        Assert.Equal(0, exitCode);
        Assert.Equal(0, gridExit);
        Assert.Equal(0, colorsExit);
        using var document = JsonDocument.Parse(text);
        var operations = OperationsOf(document.RootElement);
        Assert.Equal(["fit", "auto-levels", "deskew", "trim"], operations.Select(o => o.GetProperty("op").GetString()));
        Assert.Equal((300, 225), (operations[0].GetProperty("width").GetInt32(), operations[0].GetProperty("height").GetInt32()));
        Assert.True(operations[1].GetProperty("applied").GetBoolean());
        Assert.True(operations[2].GetProperty("applied").GetBoolean());
        Assert.InRange(operations[2].GetProperty("skewDegrees").GetDouble(), 2.4, 3.6);
        var width = operations[3].GetProperty("width").GetInt32();
        var height = operations[3].GetProperty("height").GetInt32();
        Assert.True(width > 0 && height > 0 && width <= operations[2].GetProperty("width").GetInt32() && height < operations[2].GetProperty("height").GetInt32());
        using var sketchImage = PngCodec.Load(sketch);
        Assert.Equal((width, height), (sketchImage.Width, sketchImage.Height));

        // The grid is last and sees the final size; the stretched paper is the dominant color
        using var gridDocument = JsonDocument.Parse(gridText);
        var gridOperation = Assert.Single(OperationsOf(gridDocument.RootElement));
        Assert.Equal("grid", gridOperation.GetProperty("op").GetString());
        Assert.Equal((width, height), (gridOperation.GetProperty("width").GetInt32(), gridOperation.GetProperty("height").GetInt32()));
        using var colors = JsonDocument.Parse(colorsText);
        Assert.Equal("#FFFFFF", colors.RootElement.GetProperty("colors").EnumerateArray().First().GetProperty("hex").GetString());
    }

    /// <summary>
    ///     Test that the same photo operations on the same input write the same bytes on every run.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Photo_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange: a tilted sketch with uneven lighting
        using var directory = new TempDirectory();
        var input = SavePng(FixtureFactory.SketchPhoto(300, 200, 5.0, lines: 5, shade: 40), directory.Combine("in.png"));

        foreach (var options in new[]
                 {
                     new[] { "--auto-levels", "--deskew", "--trim", "--threshold", "auto" },
                     ["--grayscale", "--levels", "30,220,1.2", "--contrast", "15", "--rotate", "17.5", "--grid", "40"],
                     ["--invert", "--brightness", "-10", "--rotate", "45", "--filter", "nearest"]
                 })
        {
            // Act: the same options twice
            var first = directory.Combine("first.png");
            var second = directory.Combine("second.png");
            Assert.Equal(0, ToolHost.Invoke(["render", input, .. options, "-o", first]).ExitCode);
            Assert.Equal(0, ToolHost.Invoke(["render", input, .. options, "-o", second]).ExitCode);

            // Assert: the same bytes
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }
    }
}
