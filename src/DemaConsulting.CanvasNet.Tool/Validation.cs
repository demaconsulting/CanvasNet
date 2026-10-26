// cspell:ignore canvasnet
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
