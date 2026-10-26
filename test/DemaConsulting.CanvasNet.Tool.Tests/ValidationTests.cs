// cspell:ignore canvasnet
namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for the Validation class.
/// </summary>
[Collection("Sequential")]
public class ValidationTests
{
    /// <summary>
    ///     Test that Run prints the summary lines.
    /// </summary>
    [Fact]
    public void Validation_Run_NotSilent_PrintsSummary()
    {
        // Arrange: a context that is not silent so the summary reaches standard output
        using var context = Context.Create([]);

        // Act: run the self-validation
        var (stdOut, _) = ToolHost.Capture(() => Validation.Run(context));

        // Assert: header, both platform self-tests, and a summary with no failures are printed
        Assert.Contains("# DEMA Consulting CanvasNet Tool", stdOut);
        Assert.Contains("CanvasNetTool_VersionDisplay - Passed", stdOut);
        Assert.Contains("CanvasNetTool_HelpDisplay - Passed", stdOut);
        Assert.Contains("CanvasNetTool_Formats - Passed", stdOut);
        Assert.Matches(@"Total Tests: \d+", stdOut);
        Assert.Matches(@"Passed: \d+", stdOut);
        Assert.Contains("Failed: 0", stdOut);
    }

    /// <summary>
    ///     Test that Run executes the formats self-test and records it in the results file.
    /// </summary>
    [Fact]
    public void Validation_Run_FormatsSelfTest_PassesAndIsRecorded()
    {
        // Arrange: a temporary TRX path and a context that is not silent
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.trx");

        try
        {
            using var context = Context.Create(["--results", resultsFile]);

            // Act: run the self-validation
            var (stdOut, _) = ToolHost.Capture(() => Validation.Run(context));

            // Assert: the formats self-test passed, and its name is in the results file
            Assert.Equal(0, context.ExitCode);
            Assert.Contains("✓ CanvasNetTool_Formats - Passed", stdOut);
            Assert.Contains("CanvasNetTool_Formats", File.ReadAllText(resultsFile));
        }
        finally
        {
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that Run executes the imaging self-tests and records them in the results file.
    /// </summary>
    [Fact]
    public void Validation_Run_ImagingSelfTests_PassAndAreRecorded()
    {
        // Arrange: a temporary TRX path and a context that is not silent
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.trx");

        try
        {
            using var context = Context.Create(["--results", resultsFile]);

            // Act: run the self-validation
            var (stdOut, _) = ToolHost.Capture(() => Validation.Run(context));

            // Assert: each imaging self-test passed and is named in the results file
            Assert.Equal(0, context.ExitCode);
            var content = File.ReadAllText(resultsFile);
            foreach (var name in new[] { "CanvasNetTool_RenderPng", "CanvasNetTool_ConvertFormats", "CanvasNetTool_Info", "CanvasNetTool_RenderSvg", "CanvasNetTool_PdfMinimal", "CanvasNetTool_RenderScad" })
            {
                Assert.Contains($"✓ {name} - Passed", stdOut);
                Assert.Contains(name, content);
            }
        }
        finally
        {
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that Run succeeds with exit code zero.
    /// </summary>
    [Fact]
    public void Validation_Run_WithSilentContext_ExitCodeIsZero()
    {
        // Arrange: a silent context
        using var context = Context.Create(["--silent"]);

        // Act: run the self-validation
        Validation.Run(context);

        // Assert: every self-test passed
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that Run uses the requested heading depth.
    /// </summary>
    [Fact]
    public void Validation_Run_WithDepth_UsesHeadingDepth()
    {
        // Arrange: a context with heading depth 3
        using var context = Context.Create(["--depth", "3"]);

        // Act: run the self-validation
        var (stdOut, _) = ToolHost.Capture(() => Validation.Run(context));

        // Assert: the heading has three hashes
        Assert.Contains("### DEMA Consulting CanvasNet Tool", stdOut);
    }

    /// <summary>
    ///     Test that Run writes a TRX file naming the platform self-tests.
    /// </summary>
    [Fact]
    public void Validation_Run_WithTrxResultsFile_WritesTrxFile()
    {
        // Arrange: a temporary TRX path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.trx");

        try
        {
            using var context = Context.Create(["--silent", "--results", resultsFile]);

            // Act: run the self-validation
            Validation.Run(context);

            // Assert: the TRX file exists, is a TRX document, and records both contractual self-test names
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile));
            var content = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", content);
            Assert.Contains("CanvasNetTool_VersionDisplay", content);
            Assert.Contains("CanvasNetTool_HelpDisplay", content);
            Assert.Contains("CanvasNetTool_Formats", content);
        }
        finally
        {
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that Run writes a JUnit XML file.
    /// </summary>
    [Fact]
    public void Validation_Run_WithXmlResultsFile_WritesXmlFile()
    {
        // Arrange: a temporary XML path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.xml");

        try
        {
            using var context = Context.Create(["--silent", "--results", resultsFile]);

            // Act: run the self-validation
            Validation.Run(context);

            // Assert: the file exists and is a JUnit document
            Assert.Equal(0, context.ExitCode);
            Assert.True(File.Exists(resultsFile));
            Assert.Contains("<testsuites", File.ReadAllText(resultsFile));
        }
        finally
        {
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that Run reports an unsupported results extension and writes no file.
    /// </summary>
    [Fact]
    public void Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile()
    {
        // Arrange: a results path with an unsupported extension
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.json");

        try
        {
            using var context = Context.Create(["--silent", "--results", resultsFile]);

            // Act: run the self-validation
            Validation.Run(context);

            // Assert: the error exit code is set and nothing was written
            Assert.Equal(1, context.ExitCode);
            Assert.False(File.Exists(resultsFile));
        }
        finally
        {
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that Run rejects a null context.
    /// </summary>
    [Fact]
    public void Validation_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null context is rejected
        Assert.Throws<ArgumentNullException>(() => Validation.Run(null!));
    }
}
