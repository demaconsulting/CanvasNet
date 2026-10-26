// cspell:ignore canvasnet pdf endobj endstream xref startxref
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

        // Calculate totals
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
