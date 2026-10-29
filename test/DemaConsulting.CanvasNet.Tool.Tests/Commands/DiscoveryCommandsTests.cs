// cspell:ignore canvasnet jpeg jpg tif tiff jp2 j2k j2c vsdx pptx
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class (the formats and info commands).
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
        + "{\"format\":\"scad\",\"kind\":\"model\",\"extensions\":[\".scad\"],\"multiPage\":false,\"vector\":true},"
        + "{\"format\":\"vega-lite\",\"kind\":\"chart\",\"extensions\":[\".vl.json\",\".vl\"],\"multiPage\":false,\"vector\":true}],"
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
        Assert.All(DiscoveryCommands.InputFormats, f => Assert.Contains(f.Kind, new[] { "raster", "vector", "document", "model", "chart" }));
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

    /// <summary>
    ///     Test that the input format table names exactly the source kinds, in the same order, with the extensions the loader detects.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_FormatTables_InputFormatsMatchSourceKinds()
    {
        // Arrange: the kinds and the table
        var kinds = Enum.GetValues<SourceKind>();
        var rows = DiscoveryCommands.InputFormats;

        // Assert: one row per kind, in order, and the extensions of each row are those that detect that kind (a chart
        // is also detected by the two-part suffix .vl.json, which is not an extension)
        Assert.Equal(kinds.Select(k => k.ToFormatName()), rows.Select(r => r.Name));
        foreach (var (kind, row) in kinds.Zip(rows))
        {
            var detected = SourceLoader.ExtensionKinds.Where(e => e.Value == kind).Select(e => e.Key)
                .Concat(kind == SourceKind.VegaLite ? [SourceLoader.VegaLiteJsonSuffix] : [])
                .Order();
            Assert.Equal(detected, row.Extensions.Order());
        }
    }

    /// <summary>
    ///     Test that the output format table names exactly the output formats, in the same order, with the extensions the writer accepts.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_FormatTables_OutputFormatsMatchOutputFormatsOfWriter()
    {
        // Arrange: the formats and the table
        var formats = Enum.GetValues<OutputFormat>();
        var rows = DiscoveryCommands.OutputFormats;

        // Assert: one row per format, in order; the extensions are those the writer maps; lossy only for JPEG
        Assert.Equal(formats.Select(f => f.ToFormatName()), rows.Select(r => r.Name));
        foreach (var (format, row) in formats.Zip(rows))
        {
            var accepted = ImageWriter.ExtensionFormats.Where(e => e.Value == format).Select(e => e.Key).Order();
            Assert.Equal(accepted, row.Extensions.Order());
            Assert.Equal(format == OutputFormat.Jpeg, row.Lossy);
        }
    }

    /// <summary>
    ///     Test that the quality range in the formats listing is the one the writer enforces.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_FormatTables_QualityRangeMatchesWriter()
    {
        // Arrange / Act / Assert: the same constants
        Assert.Equal(ImageWriter.MinQuality, DiscoveryCommands.Formats.MinQuality);
        Assert.Equal(ImageWriter.MaxQuality, DiscoveryCommands.Formats.MaxQuality);
        Assert.Equal(ImageWriter.DefaultQuality, DiscoveryCommands.Formats.DefaultQuality);
    }

    /// <summary>
    ///     Runs the info command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunInfo(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new DiscoveryCommands.Info();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the info command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunInfoFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Info();
        return Assert.Throws<ToolException>(() => command.Run(context, args));
    }

    /// <summary>
    ///     Test that the info command describes itself.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_Metadata_DescribesCommand()
    {
        // Arrange / Act: create the command
        var command = new DiscoveryCommands.Info();

        // Assert: name, no aliases, the page options, and a usage line with examples
        Assert.Equal("info", command.Name);
        Assert.Empty(command.Aliases);
        Assert.Equal(["--page", "--all-pages"], command.Options.Select(o => o.Name));
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet info <input> [--page N | --all-pages] [--json]", command.Usage);
        Assert.Contains("Examples:", command.Usage);
    }

    /// <summary>
    ///     Test that the default registry offers the info command.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_DefaultRegistry_RegistersInfo()
    {
        // Arrange / Act / Assert: the command is registered
        Assert.IsType<DiscoveryCommands.Info>(CommandRegistry.CreateDefault().Find("info"));
    }

    /// <summary>
    ///     Test the one-line text description of a PNG and of an SVG.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_TextOutput_DescribesFile()
    {
        // Arrange: a PNG and the SVG fixture
        using var directory = new TempDirectory();
        var png = FixtureFactory.WriteSource(directory.Path, "png");
        var svg = FixtureFactory.FixturePath("shapes.svg");

        // Act
        var pngText = RunInfo(false, png);
        var svgText = RunInfo(false, svg);

        // Assert: one line each, with the path, the format, the size, the channels, and the bytes
        Assert.Matches($@"^{Regex.Escape(png)}: png, 12x8 px, 4 channels with alpha, {new FileInfo(png).Length} bytes\r?\n$", pngText);
        Assert.Matches($@"^{Regex.Escape(svg)}: svg, 100x80 px \(intrinsic size\), 4 channels with alpha, {new FileInfo(svg).Length} bytes\r?\n$", svgText);
    }

    /// <summary>
    ///     Test that the JSON document has the documented keys and exact layout.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_JsonOutput_MatchesGoldenDocument()
    {
        // Arrange: the SVG fixture
        var svg = FixtureFactory.FixturePath("shapes.svg");

        // Act
        var stdOut = RunInfo(true, svg);

        // Assert: exactly the documented document
        var expected = "{\"schemaVersion\":1,\"ok\":true,\"command\":\"info\","
                       + $"\"input\":{{\"path\":{JsonSerializer.Serialize(svg)},\"format\":\"svg\",\"bytes\":{new FileInfo(svg).Length}}},"
                       + "\"image\":{\"width\":100,\"height\":80,\"channels\":4,\"hasAlpha\":true,\"canDecode\":true,\"frameCount\":1}}";
        Assert.Equal(expected + Environment.NewLine, stdOut);
    }

    /// <summary>
    ///     Test that a JPEG with an EXIF orientation reports it in text and JSON, and one without does not.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_JpegOrientation_IsReportedOnlyWhenPresent()
    {
        // Arrange: a JPEG with an orientation, and one without
        using var directory = new TempDirectory();
        var plain = FixtureFactory.WriteSource(directory.Path, "jpeg");
        var camera = directory.Combine("camera.jpg");
        File.WriteAllBytes(camera, FixtureFactory.InsertExifOrientation(File.ReadAllBytes(plain), 6, true));

        // Act
        var cameraText = RunInfo(false, camera);
        var cameraJson = JsonDocument.Parse(RunInfo(true, camera)).RootElement.GetProperty("image");
        var plainJson = JsonDocument.Parse(RunInfo(true, plain)).RootElement.GetProperty("image");

        // Assert: the orientation is present in both forms for the camera file, absent for the plain one
        Assert.Contains(", EXIF orientation 6", cameraText);
        Assert.Equal(6, cameraJson.GetProperty("exifOrientation").GetInt32());
        Assert.False(plainJson.TryGetProperty("exifOrientation", out _));
        Assert.Equal(3, plainJson.GetProperty("channels").GetInt32());
        Assert.False(plainJson.GetProperty("hasAlpha").GetBoolean());
    }

    /// <summary>
    ///     Test that every raster kind can be described.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("jpeg2000")]
    [InlineData("svg")]
    public void DiscoveryCommands_Info_EachKind_ReportsFormatAndSize(string key)
    {
        // Arrange: a file of the kind
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act
        var image = JsonDocument.Parse(RunInfo(true, path)).RootElement;

        // Assert: the format name and a plausible size
        Assert.Equal(key, image.GetProperty("input").GetProperty("format").GetString());
        Assert.True(image.GetProperty("image").GetProperty("width").GetInt32() > 0);
        Assert.True(image.GetProperty("image").GetProperty("canDecode").GetBoolean());
    }

    /// <summary>
    ///     Test that a missing or corrupt input, and a truncated document, is an expected error.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_BadInput_ThrowsToolException()
    {
        // Arrange: a missing file, a truncated PNG, a PDF with no pages, and a text file
        using var directory = new TempDirectory();
        var truncated = FixtureFactory.WriteSource(directory.Path, "png");
        File.WriteAllBytes(truncated, File.ReadAllBytes(truncated)[..10]);
        var pdf = directory.Combine("doc.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\n");
        var text = directory.Combine("notes.txt");
        File.WriteAllText(text, "notes");

        // Act / Assert: the codes
        Assert.Equal(ErrorCodes.InputNotFound, RunInfoFailing(directory.Combine("missing.png")).Code);
        Assert.Equal(ErrorCodes.CorruptInput, RunInfoFailing(truncated).Code);
        Assert.Equal(ErrorCodes.CorruptInput, RunInfoFailing(pdf).Code);
        Assert.Equal(ErrorCodes.UnsupportedFormat, RunInfoFailing(text).Code);
    }

    /// <summary>
    ///     Test that a missing input, extra inputs, and unknown options are usage errors.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_CommandLineMistakes_ThrowUsageError()
    {
        // Arrange / Act
        var none = RunInfoFailing();
        var two = RunInfoFailing("a.png", "b.png");
        var unknown = RunInfoFailing("a.png", "--pages", "2");

        // Assert: usage errors
        Assert.Equal(ErrorCodes.UsageError, none.Code);
        Assert.Contains("expects one input file", none.Message);
        Assert.Equal(ErrorCodes.UsageError, two.Code);
        Assert.Equal(ErrorCodes.UsageError, unknown.Code);
        Assert.Contains("Unknown option '--pages'", unknown.Message);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange: the command and a context
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Info();

        // Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => command.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => command.Run(context, null!));
    }
}
