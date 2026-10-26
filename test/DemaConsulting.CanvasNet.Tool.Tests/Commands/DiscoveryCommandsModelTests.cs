// cspell:ignore canvasnet scad openscad
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class with OpenSCAD models: the info description of a model and the
///     link between the formats listing and what the loader reads.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsModelTests
{
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
    ///     Test the text description of a model: a line with the counts, then its modules, functions, and directives.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ModelText_ListsModulesFunctionsAndDirectives()
    {
        // Arrange: a model with two modules, one function, and an include and a use
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "include <lib/a.scad>\nuse <lib/b.scad>\nmodule one() {}\nmodule two() {}\nfunction f() = 1;\n");

        // Act
        var text = RunInfo(false, path);

        // Assert
        var bytes = new FileInfo(path).Length;
        var expected = string.Join(
            Environment.NewLine,
            $"{path}: scad, 2 modules, 1 function, 2 include/use directives, {bytes} bytes",
            "  module one",
            "  module two",
            "  function f",
            "  include <lib/a.scad> (line 1)",
            "  use <lib/b.scad> (line 2)") + Environment.NewLine;
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Test the text description of a model that declares nothing, in the singular and with zero counts.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ModelWithoutDeclarations_DescribesCounts()
    {
        // Arrange: a model of one statement, and one with a single module
        using var directory = new TempDirectory();
        var plain = FixtureFactory.WriteText(directory.Path, "plain.scad", "cube(1);\n");
        var single = FixtureFactory.WriteText(directory.Path, "single.scad", "module only() {}\n");

        // Act
        var plainText = RunInfo(false, plain);
        var singleText = RunInfo(false, single);

        // Assert
        Assert.Equal($"{plain}: scad, 0 modules, 0 functions, 0 include/use directives, {new FileInfo(plain).Length} bytes{Environment.NewLine}", plainText);
        Assert.StartsWith($"{single}: scad, 1 module, 0 functions, 0 include/use directives", singleText);
    }

    /// <summary>
    ///     Test that the JSON description of a model has a <c>model</c> object instead of <c>image</c>.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ModelJson_HasModelObject()
    {
        // Arrange: a model with a module, a function, and both kinds of directive
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "include <lib/a.scad>\nuse <lib/b.scad>\nmodule one() {}\nfunction f() = 1;\n");

        // Act
        var json = RunInfo(true, path);

        // Assert: the documented keys and exact layout
        var bytes = new FileInfo(path).Length;
        var escaped = JsonSerializer.Serialize(path);
        var expected = "{\"schemaVersion\":1,\"ok\":true,\"command\":\"info\",\"input\":{\"path\":" + escaped
            + ",\"format\":\"scad\",\"bytes\":" + bytes + "},\"model\":{\"modules\":[\"one\"],\"functions\":[\"f\"],"
            + "\"directives\":[{\"kind\":\"include\",\"path\":\"lib/a.scad\",\"line\":1},{\"kind\":\"use\",\"path\":\"lib/b.scad\",\"line\":2}]}}";
        Assert.Equal(expected + Environment.NewLine, json);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("image", out _));
        Assert.False(document.RootElement.TryGetProperty("document", out _));
    }

    /// <summary>
    ///     Test that a model with a syntax error is a scad error from the info command, and that the second page of a model does not exist.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_BrokenModelAndPageTwo_ThrowExpectedErrors()
    {
        // Arrange: a broken model and a good one
        using var directory = new TempDirectory();
        var broken = FixtureFactory.WriteText(directory.Path, "broken.scad", "cube(;\n");
        var good = FixtureFactory.WriteSource(directory.Path, "scad");
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Info();

        // Act
        var syntax = Assert.Throws<ToolException>(() => command.Run(context, [broken]));
        var page = Assert.Throws<ToolException>(() => command.Run(context, [good, "--page", "2"]));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, syntax.Code);
        Assert.Equal(ErrorCodes.PageOutOfRange, page.Code);
    }

    /// <summary>
    ///     Test that every input format the listing names can be probed from a file of that kind, and that the listing
    ///     describes the model as a vector kind that is not multi-page.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_FormatTables_EveryListedInputFormatIsReadable()
    {
        // Arrange: a file of each listed kind
        using var directory = new TempDirectory();

        foreach (var row in DiscoveryCommands.InputFormats)
        {
            var path = FixtureFactory.WriteSource(directory.Path, row.Name);

            // Act
            var info = SourceLoader.Probe(path);

            // Assert: the loader recognizes the format the listing names, and a document is the only multi-page kind
            Assert.Equal(row.Name, info.Format.ToFormatName());
            Assert.Equal(row.MultiPage, info.Format.IsDocument());
            Assert.Equal(row.Kind == "model", info.Format.IsModel());
        }

        var scad = DiscoveryCommands.InputFormats.Single(r => r.Name == "scad");
        Assert.Equal(("model", true, false), (scad.Kind, scad.Vector, scad.MultiPage));
        Assert.Equal([".scad"], scad.Extensions);
    }
}
