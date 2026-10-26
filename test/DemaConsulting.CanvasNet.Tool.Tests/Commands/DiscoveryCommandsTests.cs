// cspell:ignore canvasnet jpeg jpg tif tiff jp2 j2k j2c vsdx pptx
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class (the formats command).
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsTests
{
    /// <summary>
    ///     The exact JSON document of the formats command for the versions 1.2.3 and 4.5.6.
    /// </summary>
    private const string GoldenFormatsJson =
        "{\"schemaVersion\":1,\"ok\":true,\"command\":\"formats\","
        + "\"tool\":{\"name\":\"canvasnet\",\"version\":\"1.2.3\"},"
        + "\"library\":{\"name\":\"DemaConsulting.CanvasNet\",\"version\":\"4.5.6\"},"
        + "\"inputs\":["
        + "{\"format\":\"png\",\"kind\":\"raster\",\"extensions\":[\".png\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"jpeg\",\"kind\":\"raster\",\"extensions\":[\".jpg\",\".jpeg\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"bmp\",\"kind\":\"raster\",\"extensions\":[\".bmp\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"tiff\",\"kind\":\"raster\",\"extensions\":[\".tif\",\".tiff\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"gif\",\"kind\":\"raster\",\"extensions\":[\".gif\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"jpeg2000\",\"kind\":\"raster\",\"extensions\":[\".jp2\",\".j2k\",\".j2c\"],\"multiPage\":false,\"vector\":false},"
        + "{\"format\":\"svg\",\"kind\":\"vector\",\"extensions\":[\".svg\"],\"multiPage\":false,\"vector\":true},"
        + "{\"format\":\"pdf\",\"kind\":\"document\",\"extensions\":[\".pdf\"],\"multiPage\":true,\"vector\":true},"
        + "{\"format\":\"pptx\",\"kind\":\"document\",\"extensions\":[\".pptx\"],\"multiPage\":true,\"vector\":true},"
        + "{\"format\":\"vsdx\",\"kind\":\"document\",\"extensions\":[\".vsdx\"],\"multiPage\":true,\"vector\":true},"
        + "{\"format\":\"scad\",\"kind\":\"model\",\"extensions\":[\".scad\"],\"multiPage\":false,\"vector\":true}],"
        + "\"outputs\":["
        + "{\"format\":\"png\",\"extensions\":[\".png\"],\"alpha\":true,\"lossy\":false},"
        + "{\"format\":\"jpeg\",\"extensions\":[\".jpg\",\".jpeg\"],\"alpha\":false,\"lossy\":true},"
        + "{\"format\":\"bmp\",\"extensions\":[\".bmp\"],\"alpha\":true,\"lossy\":false},"
        + "{\"format\":\"tiff\",\"extensions\":[\".tif\",\".tiff\"],\"alpha\":true,\"lossy\":false}],"
        + "\"quality\":{\"min\":1,\"max\":100,\"default\":90},"
        + "\"maxDimension\":8192}";

    /// <summary>
    ///     Runs the formats command (reporting fixed versions) and captures its output.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <param name="globalArgs">Global options to put on the command line, such as <c>--json</c>.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunFormats(string[] args, params string[] globalArgs)
    {
        using var context = Context.Create(globalArgs);
        var command = new DiscoveryCommands.Formats("1.2.3", "4.5.6");
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Test that the command describes itself.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_Metadata_DescribesCommand()
    {
        // Arrange / Act: create the command
        var command = new DiscoveryCommands.Formats();

        // Assert: name, no aliases, no options, and a usage line with examples
        Assert.Equal("formats", command.Name);
        Assert.Empty(command.Aliases);
        Assert.Empty(command.Options);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet formats [--json]", command.Usage);
        Assert.Contains("Examples:", command.Usage);
    }

    /// <summary>
    ///     Test that the text listing shows the input and output formats, the limits, and the versions.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_TextOutput_ListsFormatsLimitsAndVersions()
    {
        // Arrange / Act: run the command for text output
        var stdOut = RunFormats([]);

        // Assert: the sections, representative rows, the limits, and the versions
        Assert.Contains("Input formats:", stdOut);
        Assert.Contains("Output formats:", stdOut);
        Assert.Matches(@"png\s+raster\s+\.png\s+single\s+no", stdOut);
        Assert.Matches(@"pdf\s+document\s+\.pdf\s+multiple\s+yes", stdOut);
        Assert.Matches(@"jpeg\s+\.jpg \.jpeg\s+no\s+yes", stdOut);
        Assert.Contains("Maximum image dimension: 8192 pixels", stdOut);
        Assert.Contains("JPEG quality: 1-100 (default 90)", stdOut);
        Assert.Contains("canvasnet tool: 1.2.3", stdOut);
        Assert.Contains("CanvasNet library: 4.5.6", stdOut);
        Assert.DoesNotContain("{", stdOut);
    }

    /// <summary>
    ///     Test that the JSON document matches the documented schema, byte for byte.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_JsonOutput_MatchesGoldenDocument()
    {
        // Arrange / Act: run the command with --json
        var stdOut = RunFormats([], "--json");

        // Assert: exactly the golden document and a line ending
        Assert.Equal(GoldenFormatsJson + Environment.NewLine, stdOut);
    }

    /// <summary>
    ///     Test that the default constructor reports the running tool's version.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_DefaultVersions_ReportsRunningVersions()
    {
        // Arrange: the command with the default versions
        using var context = Context.Create(["--json"]);
        var command = new DiscoveryCommands.Formats();

        // Act: run it
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, []));

        // Assert: the tool version is the program version and the library version is not empty
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(Program.Version, document.RootElement.GetProperty("tool").GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("library").GetProperty("version").GetString()));
    }

    /// <summary>
    ///     Test that the format tables are well formed: unique names, and unique lower-case extensions with a dot.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_FormatTables_AreWellFormed()
    {
        // Arrange: the extensions of each table
        var inputExtensions = DiscoveryCommands.InputFormats.SelectMany(f => f.Extensions).ToList();
        var outputExtensions = DiscoveryCommands.OutputFormats.SelectMany(f => f.Extensions).ToList();

        // Assert: names are unique, and every extension is lower case, starts with a dot, and is unique within its table
        Assert.Equal(DiscoveryCommands.InputFormats.Count, DiscoveryCommands.InputFormats.Select(f => f.Name).Distinct().Count());
        Assert.Equal(DiscoveryCommands.OutputFormats.Count, DiscoveryCommands.OutputFormats.Select(f => f.Name).Distinct().Count());
        Assert.Equal(inputExtensions.Count, inputExtensions.Distinct().Count());
        Assert.Equal(outputExtensions.Count, outputExtensions.Distinct().Count());
        Assert.All(inputExtensions.Concat(outputExtensions), e =>
        {
            Assert.StartsWith(".", e);
            Assert.Equal(e.ToLowerInvariant(), e);
        });
        Assert.All(DiscoveryCommands.InputFormats, f => Assert.Contains(f.Kind, new[] { "raster", "vector", "document", "model" }));
    }

    /// <summary>
    ///     Test that a positional argument is a usage error.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_PositionalArgument_ThrowsUsageError()
    {
        // Arrange: the command and a context
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Formats();

        // Act: run with a stray argument
        var exception = Assert.Throws<ToolException>(() => command.Run(context, ["extra"]));

        // Assert: a usage error
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("'formats' takes no arguments, but 1 were given.", exception.Message);
    }

    /// <summary>
    ///     Test that an unknown option is a usage error.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_UnknownOption_ThrowsUsageError()
    {
        // Arrange: the command and a context
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Formats();

        // Act: run with an unknown option
        var exception = Assert.Throws<ToolException>(() => command.Run(context, ["--bogus"]));

        // Assert: a usage error naming the option and the command
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Unknown option '--bogus' for 'formats'.", exception.Message);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange: the command and a context
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Formats();

        // Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => command.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => command.Run(context, null!));
    }
}
