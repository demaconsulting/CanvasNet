// cspell:ignore canvasnet pdf endobj endstream xref startxref rmse ssim deskew otsu thresholded
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.TestResults.IO;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     Provides self-validation functionality for the CanvasNet Tool.
/// </summary>
internal static class Validation
{
    /// <summary>
    ///     Runs self-validation tests and optionally writes results to a file.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    ///     If any self-test fails, <c>context.WriteError</c> is called for each failure, which sets
    ///     <c>context.ExitCode</c> to 1 as a side-effect. If a results file is requested and its
    ///     extension is unsupported, <c>context.WriteError</c> is also called, resulting in a
    ///     non-zero exit code. The names of the self-tests are a contract: the platform requirements
    ///     are proven by the results the installed tool writes on each platform and runtime.
    /// </remarks>
    public static void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Print validation header
        PrintValidationHeader(context);

        // Create test results collection
        var testResults = new DemaConsulting.TestResults.TestResults
        {
            Name = "CanvasNet Tool Self-Validation"
        };

        // Run core functionality tests
        RunVersionTest(context, testResults);
        RunHelpTest(context, testResults);
        RunFormatsTest(context, testResults);
        RunRenderPngTest(context, testResults);
        RunConvertFormatsTest(context, testResults);
        RunInfoTest(context, testResults);
        RunRenderSvgTest(context, testResults);
        RunPdfMinimalTest(context, testResults);
        RunRenderScadTest(context, testResults);
        RunRenderVegaLiteTest(context, testResults);
        RunCropResizeTest(context, testResults);
        RunRotateFlipPadTest(context, testResults);
        RunCompareTest(context, testResults);
        RunMontageTest(context, testResults);
        RunAdjustmentsTest(context, testResults);
        var totalTests = testResults.Results.Count;
        var passedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Passed);
        var failedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Failed);

        // Print summary
        context.WriteLine("");
        context.WriteLine($"Total Tests: {totalTests}");
        context.WriteLine($"Passed: {passedTests}");
        if (failedTests > 0)
        {
            context.WriteError($"Failed: {failedTests}");
        }
        else
        {
            context.WriteLine($"Failed: {failedTests}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>
    ///     Prints the validation header with system information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} DEMA Consulting CanvasNet Tool");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs a test for version display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunVersionTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("CanvasNetTool_VersionDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "version-test.log");

            // Run the program, capturing its output in the log file
            var exitCode = RunProgram("--silent", "--log", logFile, "--version");

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify version string is in log (version contains dots like 0.0.0)
                var versionPattern = new Regex(@"\b\d+\.\d+\.\d+", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (!string.IsNullOrWhiteSpace(logContent) &&
                    versionPattern.IsMatch(logContent))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine("✓ CanvasNetTool_VersionDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Version string not found in log";
                    context.WriteError("✗ CanvasNetTool_VersionDisplay - Failed: Version string not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ CanvasNetTool_VersionDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "CanvasNetTool_VersionDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test for help display functionality.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunHelpTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("CanvasNetTool_HelpDisplay");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "help-test.log");

            // Run the program, capturing its output in the log file
            var exitCode = RunProgram("--silent", "--log", logFile, "--help");

            // Check if execution succeeded
            if (exitCode == 0)
            {
                // Read log content
                var logContent = File.ReadAllText(logFile);

                // Verify help text is in log
                if (logContent.Contains("Usage:") && logContent.Contains("Options:"))
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                    context.WriteLine("✓ CanvasNetTool_HelpDisplay - Passed");
                }
                else
                {
                    test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                    test.ErrorMessage = "Help text not found in log";
                    context.WriteError("✗ CanvasNetTool_HelpDisplay - Failed: Help text not found in log");
                }
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ CanvasNetTool_HelpDisplay - Failed: Exit code {exitCode}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "CanvasNetTool_HelpDisplay", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs a test for the formats command and its JSON result document.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunFormatsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("CanvasNetTool_Formats");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = PathHelpers.SafePathCombine(tempDir.DirectoryPath, "formats-test.log");

            // Run the command in-process with JSON output, capturing the document in the log file
            var exitCode = RunProgram("--silent", "--log", logFile, "formats", "--json");

            if (exitCode != 0)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = $"Program exited with code {exitCode}";
                context.WriteError($"✗ CanvasNetTool_Formats - Failed: Exit code {exitCode}");
            }
            else if (FormatsDocumentIsValid(File.ReadAllText(logFile)))
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ CanvasNetTool_Formats - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Formats JSON document is not valid";
                context.WriteError("✗ CanvasNetTool_Formats - Failed: Formats JSON document is not valid");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "CanvasNetTool_Formats", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Determines whether a text is the successful JSON document of the formats command.
    /// </summary>
    /// <param name="json">The text.</param>
    /// <returns><see langword="true"/> when it has the schema version, a success flag, and the PNG input and output formats.</returns>
    private static bool FormatsDocumentIsValid(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.GetProperty("schemaVersion").GetInt32() == ResultWriter.SchemaVersion
               && root.GetProperty("ok").GetBoolean()
               && root.GetProperty("command").GetString() == "formats"
               && root.GetProperty("inputs").EnumerateArray().Any(f => f.GetProperty("format").GetString() == "png")
               && root.GetProperty("outputs").EnumerateArray().Any(f => f.GetProperty("format").GetString() == "png");
    }

    /// <summary>
    ///     Runs a test that renders a generated PNG to a PNG and checks that the pixels are unchanged.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunRenderPngTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_RenderPng", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.png");
            var output = PathHelpers.SafePathCombine(directory, "output.png");
            using (var source = CreateTestSurface())
            {
                PngCodec.Save(source, input);
            }

            var json = RunCommand(directory, "render", input, "-o", output, "--json");
            using var document = JsonDocument.Parse(json);
            var written = document.RootElement.GetProperty("outputs")[0];
            if (!document.RootElement.GetProperty("ok").GetBoolean() || written.GetProperty("format").GetString() != "png")
            {
                return "The render result document does not describe a PNG output";
            }

            using var original = PngCodec.Load(input);
            using var rendered = PngCodec.Load(output);
            return SurfacesEqual(original, rendered) ? null : "The rendered PNG differs from the input";
        });
    }

    /// <summary>
    ///     Runs a test that converts a generated PNG to every output format and checks the results.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunConvertFormatsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_ConvertFormats", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.png");
            using var original = CreateTestSurface();
            PngCodec.Save(original, input);

            foreach (var extension in new[] { ".bmp", ".tif", ".jpg", ".png" })
            {
                var output = PathHelpers.SafePathCombine(directory, "output" + extension);
                RunCommand(directory, "convert", input, "-o", output);

                // Decode the output with the codec of its own format; the lossless formats must keep every pixel
                using var converted = extension switch
                {
                    ".bmp" => BmpCodec.Load(output),
                    ".tif" => TiffCodec.Load(output),
                    ".jpg" => JpegCodec.Load(output),
                    _ => PngCodec.Load(output)
                };
                if (converted.Width != original.Width || converted.Height != original.Height)
                {
                    return $"The {extension} output has the wrong size";
                }

                if (extension != ".jpg" && !SurfacesEqual(original, converted))
                {
                    return $"The {extension} output differs from the input";
                }
            }

            return null;
        });
    }

    /// <summary>
    ///     Runs a test that describes a generated PNG with the info command.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunInfoTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_Info", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.png");
            using (var source = CreateTestSurface())
            {
                PngCodec.Save(source, input);
            }

            var json = RunCommand(directory, "info", input, "--json");
            using var document = JsonDocument.Parse(json);
            var image = document.RootElement.GetProperty("image");
            var correct = document.RootElement.GetProperty("input").GetProperty("format").GetString() == "png"
                          && image.GetProperty("width").GetInt32() == TestImageWidth
                          && image.GetProperty("height").GetInt32() == TestImageHeight;
            return correct ? null : "The info document does not describe the generated PNG";
        });
    }

    /// <summary>
    ///     Runs a test that renders a generated SVG to a PNG at twice its size and checks the pixels.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunRenderSvgTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_RenderSvg", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.svg");
            var output = PathHelpers.SafePathCombine(directory, "output.png");
            File.WriteAllText(
                input,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"><rect width=\"4\" height=\"4\" fill=\"#ff0000\"/></svg>");

            RunCommand(directory, "render", input, "--width", "16", "-o", output);
            using var rendered = PngCodec.Load(output);
            if (rendered.Width != 16 || rendered.Height != 16)
            {
                return "The rendered SVG has the wrong size";
            }

            // The red square fills the top-left quarter; the rest is flattened over white
            var red = new Rgba32(255, 0, 0, 255);
            var white = new Rgba32(255, 255, 255, 255);
            return rendered[2, 2] == red && rendered[13, 13] == white ? null : "The rendered SVG has the wrong pixels";
        });
    }

    /// <summary>
    ///     Runs a test that describes and renders a hand-written one-page PDF and checks the page size and pixels.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunPdfMinimalTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_PdfMinimal", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.pdf");
            var output = PathHelpers.SafePathCombine(directory, "output.png");
            var sharp = PathHelpers.SafePathCombine(directory, "sharp.png");
            File.WriteAllBytes(input, CreateMinimalPdf());

            // The page is 72x48 points: one page, 96x64 pixels at the default 96 dots per inch
            var json = RunCommand(directory, "info", input, "--json");
            using (var document = JsonDocument.Parse(json))
            {
                var pdf = document.RootElement.GetProperty("document");
                var page = pdf.GetProperty("pages")[0];
                if (pdf.GetProperty("pageCount").GetInt32() != 1
                    || page.GetProperty("width").GetInt32() != 96
                    || page.GetProperty("height").GetInt32() != 64)
                {
                    return "The info document does not describe the one-page PDF";
                }
            }

            // The left half is red; the rest is flattened over white
            RunCommand(directory, "render", input, "-o", output);
            using var rendered = PngCodec.Load(output);
            var red = new Rgba32(255, 0, 0, 255);
            var white = new Rgba32(255, 255, 255, 255);
            if (rendered.Width != 96 || rendered.Height != 64)
            {
                return "The rendered PDF page has the wrong size";
            }

            if (rendered[10, 32] != red || rendered[80, 32] != white)
            {
                return "The rendered PDF page has the wrong pixels";
            }

            // Twice the resolution is twice the size
            RunCommand(directory, "render", input, "--dpi", "192", "-o", sharp);
            using var larger = PngCodec.Load(sharp);
            return larger.Width == 192 && larger.Height == 128 ? null : "The PDF page at 192 dpi has the wrong size";
        });
    }

    /// <summary>
    ///     Runs a test that describes and renders an OpenSCAD model written by the test, and checks the picture.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunRenderScadTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_RenderScad", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.scad");
            var output = PathHelpers.SafePathCombine(directory, "output.png");
            var wide = PathHelpers.SafePathCombine(directory, "wide.png");
            File.WriteAllText(input, "w = 10;\nmodule block(length) { cube([length, 10, 10], center = true); }\nblock(w);\n");

            // The model is parsed, not drawn: it defines one module and no function
            var json = RunCommand(directory, "info", input, "--json");
            using (var document = JsonDocument.Parse(json))
            {
                var model = document.RootElement.GetProperty("model");
                if (model.GetProperty("modules").GetArrayLength() != 1
                    || model.GetProperty("functions").GetArrayLength() != 0)
                {
                    return "The info document does not describe the model";
                }
            }

            // A cube seen from the front fills the picture; the corner is the white background
            var white = new Rgba32(255, 255, 255, 255);
            RunCommand(directory, "render", input, "--view", "front", "--size", "40x40", "-o", output);
            using var rendered = PngCodec.Load(output);
            if (rendered.Width != 40 || rendered.Height != 40)
            {
                return "The rendered model has the wrong size";
            }

            if (rendered[20, 20] == white || rendered[20, 6] == white || rendered[0, 0] != white)
            {
                return "The rendered model has the wrong pixels";
            }

            // Setting the variable makes the block four times as long, so it no longer reaches the top of the picture
            RunCommand(directory, "render", input, "--view", "front", "--size", "40x40", "-D", "w=40", "-o", wide);
            using var widened = PngCodec.Load(wide);
            return widened[20, 20] != white && widened[20, 6] == white ? null : "The variable did not change the rendered model";
        });
    }

    /// <summary>
    ///     Runs a test that describes and renders a Vega-Lite chart written by the test, and checks the picture.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunRenderVegaLiteTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_RenderVegaLite", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "chart.vl.json");
            var output = PathHelpers.SafePathCombine(directory, "chart.png");
            File.WriteAllText(
                input,
                """
                {
                  "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
                  "width": 200,
                  "height": 100,
                  "data": { "values": [ { "k": "a", "v": 3 }, { "k": "b", "v": 5 } ] },
                  "mark": "bar",
                  "encoding": {
                    "x": { "field": "k", "type": "nominal" },
                    "y": { "field": "v", "type": "quantitative" }
                  }
                }
                """);

            // The chart is parsed and measured, not drawn: one bar mark and one inline data source
            var json = RunCommand(directory, "info", input, "--json");
            int width;
            int height;
            using (var document = JsonDocument.Parse(json))
            {
                var chart = document.RootElement.GetProperty("chart");
                var image = document.RootElement.GetProperty("image");
                width = image.GetProperty("width").GetInt32();
                height = image.GetProperty("height").GetInt32();
                if (document.RootElement.GetProperty("input").GetProperty("format").GetString() != "vega-lite"
                    || chart.GetProperty("marks").GetArrayLength() != 1
                    || chart.GetProperty("marks")[0].GetString() != "bar"
                    || chart.GetProperty("dataReferences").GetArrayLength() != 1)
                {
                    return "The info document does not describe the chart";
                }
            }

            // The chart is laid out around its 200x100 data rectangle (axes and padding), so it is larger than that
            RunCommand(directory, "render", input, "-o", output);
            using var rendered = PngCodec.Load(output);
            if (rendered.Width != width || rendered.Height != height || width <= 200 || height <= 100)
            {
                return "The rendered chart has the wrong size";
            }

            // The corner is the white background. The padding is 5 pixels at the top and the right and the data
            // rectangle is 200x100, so its two bands are 100 wide with a bar of 90 in each: the second bar (all of the
            // height) is 50 pixels left of the right edge of the rectangle, the first (3 of the 5 units) 150 pixels
            // left of it, with white above it
            var white = new Rgba32(255, 255, 255, 255);
            var barColor = new Rgba32(0x4c, 0x78, 0xa8, 255);
            var right = width - 5;
            var correct = rendered[0, 0] == white
                          && rendered[right - 50, 15] == barColor
                          && rendered[right - 150, 80] == barColor
                          && rendered[right - 150, 15] == white;
            return correct ? null : "The rendered chart has the wrong pixels";
        });
    }

    /// <summary>
    ///     Runs a test that crops and resizes a generated PNG with the verb commands and with the ordered pipeline of
    ///     <c>render</c>, and checks the sizes, the pixels, and that a verb equals its <c>render</c> equivalent.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunCropResizeTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_CropResize", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.png");
            using var original = CreateTestSurface();
            PngCodec.Save(original, input);

            // The crop verb cuts out the 4x3 rectangle at 2,1 and writes exactly what render --crop writes
            var cropped = PathHelpers.SafePathCombine(directory, "cropped.png");
            var viaRender = PathHelpers.SafePathCombine(directory, "via-render.png");
            RunCommand(directory, "crop", input, "2,1,4,3", "-o", cropped);
            RunCommand(directory, "render", input, "--crop", "2,1,4,3", "-o", viaRender);
            using (var part = PngCodec.Load(cropped))
            {
                if (part.Width != 4 || part.Height != 3 || part[0, 0] != original[2, 1] || part[3, 2] != original[5, 3])
                {
                    return "The cropped image has the wrong size or pixels";
                }
            }

            if (!File.ReadAllBytes(cropped).AsSpan().SequenceEqual(File.ReadAllBytes(viaRender)))
            {
                return "The crop command and render --crop wrote different files";
            }

            // The resize verb gives the exact size
            var resized = PathHelpers.SafePathCombine(directory, "resized.png");
            RunCommand(directory, "resize", input, "4x3", "-o", resized);
            using (var small = PngCodec.Load(resized))
            {
                if (small.Width != 4 || small.Height != 3)
                {
                    return "The resized image has the wrong size";
                }
            }

            // A chain is applied in the order written: crop, then resize, and both are listed in that order
            var chained = PathHelpers.SafePathCombine(directory, "chained.png");
            var json = RunCommand(directory, "render", input, "--crop", "2,1,4,3", "--resize", "8x6", "-o", chained, "--json");
            using var document = JsonDocument.Parse(json);
            var operations = document.RootElement.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString());
            using var result = PngCodec.Load(chained);
            return operations.SequenceEqual(["crop", "resize"]) && result.Width == 8 && result.Height == 6
                ? null
                : "The chained crop and resize did not apply in order";
        });
    }

    /// <summary>
    ///     Runs a test that rotates, flips, and pads a generated PNG with the verb commands and checks the pixels.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunRotateFlipPadTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_RotateFlipPad", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "input.png");
            using var original = CreateTestSurface();
            PngCodec.Save(original, input);
            var (w, h) = (original.Width, original.Height);

            // Rotating by 90 degrees clockwise turns the top row into the right column and swaps the size
            var rotatedPath = PathHelpers.SafePathCombine(directory, "rotated.png");
            RunCommand(directory, "rotate", input, "90", "-o", rotatedPath);
            using (var rotated = PngCodec.Load(rotatedPath))
            {
                if (rotated.Width != h || rotated.Height != w || rotated[h - 1, 0] != original[0, 0] || rotated[0, 0] != original[0, h - 1])
                {
                    return "The rotated image has the wrong size or pixels";
                }
            }

            // Flipping horizontally mirrors every row; vertically, every column
            var mirroredPath = PathHelpers.SafePathCombine(directory, "mirrored.png");
            var upsidePath = PathHelpers.SafePathCombine(directory, "upside.png");
            RunCommand(directory, "flip", input, "h", "-o", mirroredPath);
            RunCommand(directory, "flip", input, "v", "-o", upsidePath);
            using (var mirrored = PngCodec.Load(mirroredPath))
            using (var upside = PngCodec.Load(upsidePath))
            {
                if (mirrored[0, 0] != original[w - 1, 0] || upside[0, 0] != original[0, h - 1])
                {
                    return "The flipped images have the wrong pixels";
                }
            }

            // A border of 2, 1, 0, 3 pixels in red: the image sits at 2,1 and the border has the color
            var paddedPath = PathHelpers.SafePathCombine(directory, "padded.png");
            RunCommand(directory, "pad", input, "2,1,0,3", "--pad-color", "#ff0000", "-o", paddedPath);
            using var padded = PngCodec.Load(paddedPath);
            var red = new Rgba32(255, 0, 0, 255);
            return padded.Width == w + 2 && padded.Height == h + 4 && padded[0, 0] == red && padded[w + 1, h + 3] == red
                   && padded[2, 1] == original[0, 0]
                ? null
                : "The padded image has the wrong size or pixels";
        });
    }

    /// <summary>
    ///     Runs a test that compares a generated PNG with an identical copy (the result says so and the thresholds pass)
    ///     and with a copy that differs in a known region (the metrics and pictures of the differences are right, and an
    ///     unmet threshold is an expected failure that still reports the metrics).
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunCompareTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_Compare", directory =>
        {
            var first = PathHelpers.SafePathCombine(directory, "first.png");
            var same = PathHelpers.SafePathCombine(directory, "same.png");
            using (var original = CreateTestSurface())
            {
                PngCodec.Save(original, first);
                PngCodec.Save(original, same);
            }

            // Identical images have no error, a similarity of one, and no PSNR; thresholds that only identical images meet pass
            var identicalJson = RunCommand(
                directory, "compare", first, same, "--threshold", "rmse=0", "--threshold", "ssim=1", "--json");
            using (var identicalDocument = JsonDocument.Parse(identicalJson))
            {
                var root = identicalDocument.RootElement;
                var metrics = root.GetProperty("metrics");
                if (!root.GetProperty("ok").GetBoolean()
                    || !metrics.GetProperty("identical").GetBoolean()
                    || Math.Abs(metrics.GetProperty("mae").GetDouble()) > 0.0001
                    || Math.Abs(metrics.GetProperty("ssim").GetDouble() - 1) > 0.0001
                    || metrics.GetProperty("psnrDb").ValueKind != JsonValueKind.Null
                    || metrics.GetProperty("diffPixels").GetInt64() != 0
                    || !root.GetProperty("thresholds").GetProperty("passed").GetBoolean())
                {
                    return "The comparison of identical images did not report them as identical";
                }
            }

            // The copy has 12 pixels (4x3 at 2,1) whose blue channel is 50 higher: every other pixel is the same
            var second = PathHelpers.SafePathCombine(directory, "second.png");
            SaveChangedTestImage(second);

            var diff = PathHelpers.SafePathCombine(directory, "diff.png");
            var sideBySide = PathHelpers.SafePathCombine(directory, "side-by-side.png");
            var logFile = PathHelpers.SafePathCombine(directory, "failure.log");
            // The tool is run through its entry point (as the installed tool is), which reports an expected failure as exit code 1
            var exitCode = Program.Main(
                ["--silent", "--log", logFile, "compare", first, second,
                 "--diff", diff, "--side-by-side", sideBySide, "--threshold", "ssim=1", "--json"]);
            if (exitCode != 1)
            {
                return $"The unmet threshold exited with code {exitCode} instead of 1";
            }

            // The log holds the error lines and then the failure document, which carries the metrics as well as the error
            var documentLine = File.ReadLines(logFile).FirstOrDefault(line => line.StartsWith('{'));
            if (documentLine == null)
            {
                return "The unmet threshold wrote no failure document";
            }

            using var document = JsonDocument.Parse(documentLine);
            var failure = document.RootElement;
            var differentMetrics = failure.GetProperty("metrics");
            if (failure.GetProperty("ok").GetBoolean()
                || failure.GetProperty("error").GetProperty("code").GetString() != "threshold-failed"
                || differentMetrics.GetProperty("identical").GetBoolean()
                || differentMetrics.GetProperty("maxChannelDiff").GetInt32() != 50
                || differentMetrics.GetProperty("diffPixels").GetInt64() != 12
                || Math.Abs(differentMetrics.GetProperty("mae").GetDouble() - (12 * 50 / (3.0 * TestImageWidth * TestImageHeight))) > 0.0001
                || differentMetrics.GetProperty("ssim").GetDouble() >= 1)
            {
                return "The metrics of the changed region are wrong";
            }

            // The difference picture is black where the images agree and not black inside the region
            using var difference = PngCodec.Load(diff);
            var black = new Rgba32(0, 0, 0, 255);
            if (difference.Width != TestImageWidth || difference.Height != TestImageHeight
                || difference[0, 0] != black || difference[3, 2] == black)
            {
                return "The difference picture does not show the changed region";
            }

            // The side-by-side picture is the two images and the differences with a gap of 8 pixels between them
            using var sheet = PngCodec.Load(sideBySide);
            return sheet.Width == (3 * TestImageWidth) + 16 && sheet.Height == TestImageHeight
                ? null
                : "The side-by-side picture has the wrong size";
        });
    }

    /// <summary>
    ///     Saves the test image with the blue channel of the 4x3 region at 2,1 raised by 50 as a PNG.
    /// </summary>
    /// <param name="path">The path of the PNG file to write.</param>
    private static void SaveChangedTestImage(string path)
    {
        using var changed = CreateTestSurface();
        for (var y = 1; y < 4; y++)
        {
            for (var x = 2; x < 6; x++)
            {
                var pixel = changed[x, y];
                changed[x, y] = new Rgba32(pixel.R, pixel.G, (byte)(pixel.B + 50), 255);
            }
        }

        PngCodec.Save(changed, path);
    }

    /// <summary>
    ///     Runs a test that composes two generated PNGs into a labeled contact sheet and checks the geometry and the cells.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunMontageTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_Montage", directory =>
        {
            var first = PathHelpers.SafePathCombine(directory, "first.png");
            var second = PathHelpers.SafePathCombine(directory, "second.png");
            using (var original = CreateTestSurface())
            {
                PngCodec.Save(original, first);
                PngCodec.Save(original, second);
            }

            // Two 32x32 cells in two columns, 8 pixels of padding and spacing, a 22-pixel label strip: 88x70
            var output = PathHelpers.SafePathCombine(directory, "sheet.png");
            var json = RunCommand(
                directory, "montage", first, second, "--cell", "32x32", "--columns", "2", "--labels", "-o", output, "--json");
            using var document = JsonDocument.Parse(json);
            var sheetObject = document.RootElement.GetProperty("sheet");
            if (sheetObject.GetProperty("width").GetInt32() != 88 || sheetObject.GetProperty("height").GetInt32() != 70
                || document.RootElement.GetProperty("cells").GetArrayLength() != 2)
            {
                return "The contact sheet has the wrong geometry in the result";
            }

            using var sheet = PngCodec.Load(output);
            var white = new Rgba32(255, 255, 255, 255);
            if (sheet.Width != 88 || sheet.Height != 70 || sheet[0, 0] != white)
            {
                return "The contact sheet has the wrong size or padding";
            }

            // The 8x6 image is contained in the 32x32 cell (scaled to 32x24, 4 pixels from the top of the cell at 8,8)
            if (sheet[8 + 16, 8 + 16] == white || sheet[8 + 16, 8 + 1] != white)
            {
                return "The first cell does not hold the image fitted and centered";
            }

            // The label strip below the first cell (y 40 to 61) has text pixels
            for (var y = 40; y < 62; y++)
            {
                for (var x = 8; x < 40; x++)
                {
                    if (sheet[x, y] != white)
                    {
                        return null;
                    }
                }
            }

            return "The first cell has no label";
        });
    }

    /// <summary>
    ///     The width of the sketch the adjustments self-test generates.
    /// </summary>
    private const int SketchWidth = 160;

    /// <summary>
    ///     The height of the sketch the adjustments self-test generates.
    /// </summary>
    private const int SketchHeight = 100;

    /// <summary>
    ///     The tilt, in degrees clockwise, of the lines of the sketch the adjustments self-test generates.
    /// </summary>
    private const double SketchTiltDegrees = 3.0;

    /// <summary>
    ///     The gray level of the paper of the sketch.
    /// </summary>
    private const byte SketchPaper = 200;

    /// <summary>
    ///     The gray level of the ink of the sketch.
    /// </summary>
    private const byte SketchInk = 40;

    /// <summary>
    ///     Runs a test of the photo operations: it generates a light-gray sketch with four dark lines tilted by three
    ///     degrees and checks the results of the tone operations, deskew, rotation by an oblique angle, trim, grid, and
    ///     inspect on it.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    private static void RunAdjustmentsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        RunSelfTest(context, testResults, "CanvasNetTool_Adjustments", directory =>
        {
            var input = PathHelpers.SafePathCombine(directory, "sketch.png");
            using (var sketch = CreateSketchSurface())
            {
                PngCodec.Save(sketch, input);
            }

            return CheckToneOperations(directory, input)
                   ?? CheckDeskewAndRotate(directory, input)
                   ?? CheckTrimAndGrid(directory, input)
                   ?? CheckInspect(directory, input);
        });
    }

    /// <summary>
    ///     Creates the sketch of the adjustments self-test: gray paper with four dark two-pixel lines tilted clockwise by
    ///     <see cref="SketchTiltDegrees"/>, drawn with plain arithmetic.
    /// </summary>
    /// <returns>A new opaque surface; the pixel at 80,19 is ink and the pixel at 0,0 is paper.</returns>
    private static Surface CreateSketchSurface()
    {
        var surface = new Surface(SketchWidth, SketchHeight);
        var paper = new Rgba32(SketchPaper, SketchPaper, SketchPaper, 255);
        var ink = new Rgba32(SketchInk, SketchInk, SketchInk, 255);
        for (var y = 0; y < SketchHeight; y++)
        {
            for (var x = 0; x < SketchWidth; x++)
            {
                surface[x, y] = paper;
            }
        }

        var slope = Math.Tan(SketchTiltDegrees * Math.PI / 180.0);
        for (var line = 1; line <= 4; line++)
        {
            var baseline = SketchHeight * line / 5.0;
            for (var x = 0; x < SketchWidth; x++)
            {
                var top = (int)Math.Round(baseline + ((x - (SketchWidth / 2.0)) * slope) - 1.0);
                surface[x, top] = ink;
                surface[x, top + 1] = ink;
            }
        }

        return surface;
    }

    /// <summary>
    ///     Checks the black and white result of an automatic threshold and the stretch of auto-levels.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="input">The sketch.</param>
    /// <returns><see langword="null"/> when the results are right; otherwise a description of the failure.</returns>
    private static string? CheckToneOperations(string directory, string input)
    {
        // Grayscale then an automatic (Otsu) threshold: every pixel is black or white, the ink black, the paper white
        var blackAndWhite = PathHelpers.SafePathCombine(directory, "bw.png");
        var json = RunCommand(
            directory, "render", input, "--grayscale", "--threshold", "auto", "-o", blackAndWhite, "--json");
        using (var document = JsonDocument.Parse(json))
        {
            var thresholdOperation = document.RootElement.GetProperty("operations").EnumerateArray().Last();
            var level = thresholdOperation.GetProperty("level").GetInt32();
            if (thresholdOperation.GetProperty("op").GetString() != "threshold" || level < 1 || level > 254)
            {
                return "The automatic threshold did not report a level between 1 and 254";
            }
        }

        var black = new Rgba32(0, 0, 0, 255);
        var white = new Rgba32(255, 255, 255, 255);
        using (var thresholded = PngCodec.Load(blackAndWhite))
        {
            if (!AllPixelsAre(thresholded, black, white) || thresholded[80, 19] != black || thresholded[0, 0] != white)
            {
                return "The thresholded image is not black and white with black ink on white paper";
            }
        }

        // Auto-levels stretches the ink (40) and the paper (200) to the ends of the range
        var stretchedPath = PathHelpers.SafePathCombine(directory, "levels.png");
        RunCommand(directory, "render", input, "--auto-levels", "-o", stretchedPath);
        using var stretched = PngCodec.Load(stretchedPath);
        return stretched[80, 19].R <= 5 && stretched[0, 0].R >= 250
            ? null
            : "Auto-levels did not stretch the ink and the paper to the ends of the range";
    }

    /// <summary>
    ///     Checks that deskew finds the tilt of the sketch and rotates it level, and that an oblique rotation grows the
    ///     canvas and fills the corners.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="input">The sketch.</param>
    /// <returns><see langword="null"/> when the results are right; otherwise a description of the failure.</returns>
    private static string? CheckDeskewAndRotate(string directory, string input)
    {
        // Deskew reports the tilt it found (clockwise positive) and corrects it
        var leveled = PathHelpers.SafePathCombine(directory, "leveled.png");
        var json = RunCommand(directory, "render", input, "--deskew", "-o", leveled, "--json");
        using (var document = JsonDocument.Parse(json))
        {
            var deskewOperation = document.RootElement.GetProperty("operations").EnumerateArray().Last();
            if (deskewOperation.GetProperty("op").GetString() != "deskew"
                || !deskewOperation.GetProperty("applied").GetBoolean()
                || Math.Abs(deskewOperation.GetProperty("skewDegrees").GetDouble() - SketchTiltDegrees) > 0.7)
            {
                return "Deskew did not find the tilt of the lines";
            }
        }

        // Rotating 160x100 by 30 degrees needs a canvas of 189x167; the corners take the fill color
        var rotatedPath = PathHelpers.SafePathCombine(directory, "rotated.png");
        RunCommand(directory, "rotate", input, "30", "--rotate-fill", "#ff0000", "-o", rotatedPath);
        using var rotated = PngCodec.Load(rotatedPath);
        return rotated.Width == 189 && rotated.Height == 167 && rotated[0, 0] == new Rgba32(255, 0, 0, 255)
            ? null
            : "The rotated image has the wrong size or corner color";
    }

    /// <summary>
    ///     Checks that trim crops to the content inside a border and that the grid draws over a copy of the image.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="input">The sketch.</param>
    /// <returns><see langword="null"/> when the results are right; otherwise a description of the failure.</returns>
    private static string? CheckTrimAndGrid(string directory, string input)
    {
        // A white border of 10 pixels is trimmed away again: the box is the sketch
        var trimmedPath = PathHelpers.SafePathCombine(directory, "trimmed.png");
        var json = RunCommand(
            directory, "render", input, "--pad", "10", "--pad-color", "#ffffff", "--trim", "-o", trimmedPath, "--json");
        using (var document = JsonDocument.Parse(json))
        {
            var trimOperation = document.RootElement.GetProperty("operations").EnumerateArray().Last();
            if (trimOperation.GetProperty("op").GetString() != "trim"
                || trimOperation.GetProperty("width").GetInt32() != SketchWidth
                || trimOperation.GetProperty("height").GetInt32() != SketchHeight)
            {
                return "Trim did not crop to the content box";
            }
        }

        // The grid line at x=20 changes the pixel at 20,50 of the copy, and the input file is not changed
        var gridPath = PathHelpers.SafePathCombine(directory, "grid.png");
        RunCommand(directory, "render", input, "--grid", "20", "-o", gridPath);
        using var original = CreateSketchSurface();
        using var gridded = PngCodec.Load(gridPath);
        using var stored = PngCodec.Load(input);
        return gridded.Width == SketchWidth && gridded.Height == SketchHeight
               && gridded[20, 50] != original[20, 50] && SurfacesEqual(stored, original)
            ? null
            : "The grid did not change the pixel on its line, or the input was changed";
    }

    /// <summary>
    ///     Checks that the inspect commands report the paper color as the dominant color and as the color of a pixel.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="input">The sketch.</param>
    /// <returns><see langword="null"/> when the results are right; otherwise a description of the failure.</returns>
    private static string? CheckInspect(string directory, string input)
    {
        var colorsJson = RunCommand(directory, "inspect", "colors", input, "--json");
        var pixelJson = RunCommand(directory, "inspect", "pixel", input, "0,0", "--json");
        using var colors = JsonDocument.Parse(colorsJson);
        using var pixel = JsonDocument.Parse(pixelJson);
        var dominant = colors.RootElement.GetProperty("colors").EnumerateArray().First().GetProperty("hex").GetString();
        var sample = pixel.RootElement.GetProperty("pixel").GetProperty("hex").GetString();
        return dominant == "#C8C8C8" && sample == dominant
            ? null
            : "The inspect commands did not report the paper color";
    }

    /// <summary>
    ///     Determines whether every pixel of a surface is one of two colors.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="first">The first color.</param>
    /// <param name="second">The second color.</param>
    /// <returns><see langword="true"/> when no pixel has another color.</returns>
    private static bool AllPixelsAre(Surface surface, Rgba32 first, Rgba32 second)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel != first && pixel != second)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Creates the bytes of a one-page PDF document that is 72 by 48 points with a red rectangle over its left half.
    /// </summary>
    /// <returns>The bytes of the document, with a correct cross-reference table.</returns>
    private static byte[] CreateMinimalPdf()
    {
        const string content = "1 0 0 rg 0 0 36 48 re f";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 48] /Contents 4 0 R /Resources << >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        ];

        var builder = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(builder.Length);
            builder.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Runs a self-test whose body reports a failure as a message.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection.</param>
    /// <param name="testName">The name of the test.</param>
    /// <param name="body">
    ///     The test body: it receives a fresh temporary directory and returns <see langword="null"/> when the test
    ///     passed, or a description of the failure. An exception is recorded as a failure too.
    /// </param>
    private static void RunSelfTest(
        Context context,
        DemaConsulting.TestResults.TestResults testResults,
        string testName,
        Func<string, string?> body)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult(testName);

        try
        {
            using var tempDir = new TemporaryDirectory();
            var failure = body(tempDir.DirectoryPath);
            if (failure == null)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ {testName} - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = failure;
                context.WriteError($"✗ {testName} - Failed: {failure}");
            }
        }
        // Generic catch is justified here as this is a test framework - any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, testName, ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }

    /// <summary>
    ///     Runs the tool in-process with the given command and returns what it wrote.
    /// </summary>
    /// <param name="directory">A directory for the log file that captures the output.</param>
    /// <param name="args">The command and its arguments.</param>
    /// <returns>The text the command wrote to standard output.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the command did not succeed.</exception>
    private static string RunCommand(string directory, params string[] args)
    {
        var logFile = PathHelpers.SafePathCombine(directory, $"{Guid.NewGuid():N}.log");
        var exitCode = RunProgram(["--silent", "--log", logFile, .. args]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"'{args[0]}' exited with code {exitCode}");
        }

        return File.ReadAllText(logFile);
    }

    /// <summary>
    ///     The width of the image the self-tests generate.
    /// </summary>
    private const int TestImageWidth = 8;

    /// <summary>
    ///     The height of the image the self-tests generate.
    /// </summary>
    private const int TestImageHeight = 6;



    /// <summary>
    ///     Creates the small opaque test image the self-tests convert: a gradient that differs at every pixel.
    /// </summary>
    /// <returns>A new surface.</returns>
    private static Surface CreateTestSurface()
    {
        var surface = new Surface(TestImageWidth, TestImageHeight);
        for (var y = 0; y < TestImageHeight; y++)
        {
            for (var x = 0; x < TestImageWidth; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 30), (byte)(y * 40), (byte)(100 + x + y), 255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Determines whether two surfaces have the same size and the same pixels.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    /// <returns><see langword="true"/> when every pixel is equal.</returns>
    private static bool SurfacesEqual(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            if (!a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Runs the tool in-process with the given arguments and returns its exit code.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code the tool would return.</returns>
    private static int RunProgram(params string[] args)
    {
        using var testContext = Context.Create(args);
        Program.Run(testContext);
        return testContext.ExitCode;
    }

    /// <summary>
    ///     Writes test results to a file in TRX or JUnit format.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results to write.</param>
    private static void WriteResultsFile(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            string content;

            if (extension == ".trx")
            {
                content = TrxSerializer.Serialize(testResults);
            }
            else if (extension == ".xml")
            {
                // Assume JUnit format for .xml extension
                content = JUnitSerializer.Serialize(testResults);
            }
            else
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified here as a top-level handler to log file write errors
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a new test result object with common properties.
    /// </summary>
    /// <param name="testName">The name of the test.</param>
    /// <returns>A new test result object.</returns>
    private static DemaConsulting.TestResults.TestResult CreateTestResult(string testName)
    {
        return new DemaConsulting.TestResults.TestResult
        {
            Name = testName,
            ClassName = "Validation",
            CodeBase = "CanvasNetTool"
        };
    }

    /// <summary>
    ///     Finalizes a test result by setting its duration and adding it to the collection.
    /// </summary>
    /// <param name="test">The test result to finalize.</param>
    /// <param name="startTime">The start time of the test.</param>
    /// <param name="testResults">The test results collection to add to.</param>
    private static void FinalizeTestResult(
        DemaConsulting.TestResults.TestResult test,
        DateTime startTime,
        DemaConsulting.TestResults.TestResults testResults)
    {
        test.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(test);
    }

    /// <summary>
    ///     Handles test exceptions by setting failure information and logging the error.
    /// </summary>
    /// <param name="test">The test result to update.</param>
    /// <param name="context">The context for output.</param>
    /// <param name="testName">The name of the test for error messages.</param>
    /// <param name="ex">The exception that occurred.</param>
    private static void HandleTestException(
        DemaConsulting.TestResults.TestResult test,
        Context context,
        string testName,
        Exception ex)
    {
        test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
        test.ErrorMessage = $"Exception: {ex.Message}";
        context.WriteError($"✗ {testName} - FAILED: {ex.Message}");
    }

    /// <summary>
    ///     Represents a temporary directory that is automatically deleted when disposed.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        ///     Gets the path to the temporary directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
        /// </summary>
        public TemporaryDirectory()
        {
            DirectoryPath = PathHelpers.SafePathCombine(Path.GetTempPath(), $"canvasnet_validation_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Deletes the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup errors during disposal
            }
        }
    }
}
