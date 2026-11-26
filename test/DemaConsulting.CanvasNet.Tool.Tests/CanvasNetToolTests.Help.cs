using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for the help topics, run as a real process via <c>dotnet</c> so that
///     the embedded topic files and example sources are proven to be part of the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, prints a topic as JSON, prints the raw source of an example, and
    ///     reports an unknown subject as a usage error.
    /// </summary>
    [Fact]
    public void CanvasNetTool_HelpTopics_RunAsProcess_PrintTopicJsonAndRawExample()
    {
        // Arrange / Act: run the help command as real processes
        var topicExit = Runner.Run(out var topic, "dotnet", _dllPath, "help", "scad", "--json");
        var exampleExit = Runner.Run(out var example, "dotnet", _dllPath, "help", "vega-lite", "--example", "bar");
        var unknownExit = Runner.Run(out var unknown, "dotnet", _dllPath, "help", "nope");
        var cardExit = Runner.Run(out var card, "dotnet", _dllPath, "help", "mmd", "gantt");

        // Assert: the topic document is complete, the example is the chart source and nothing else, and the unknown
        // subject fails with the usage error and the list of valid subjects
        Assert.Equal(0, topicExit);
        using var topicDocument = JsonDocument.Parse(topic);
        Assert.Equal("card", topicDocument.RootElement.GetProperty("kind").GetString());
        Assert.Equal("scad", topicDocument.RootElement.GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(topicDocument.RootElement.GetProperty("body").GetString()));
        Assert.NotEmpty(topicDocument.RootElement.GetProperty("children").EnumerateArray());

        Assert.Equal(0, cardExit);
        Assert.StartsWith("# Gantt charts", card);
        Assert.Contains("- canvasnet help mermaid gantt settings", card);

        Assert.Equal(0, exampleExit);
        using var chart = JsonDocument.Parse(example);
        Assert.Equal("bar", chart.RootElement.GetProperty("mark").GetString());

        Assert.Equal(1, unknownExit);
        Assert.Contains("Error: usage-error: Unknown help subject 'nope'.", unknown);
        Assert.Contains("Valid subjects:", unknown);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, reports a help failure with <c>--json</c> as the tool's failure
    ///     document and exit code 1, both for an unknown subject and for an unknown card below a topic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_HelpTopics_RunAsProcess_JsonFailures_HaveTheToolShapeAndExitCode1()
    {
        // Arrange / Act: run the failing help commands as real processes
        var subjectExit = Runner.Run(out var subject, "dotnet", _dllPath, "help", "nothing", "--json");
        var cardExit = Runner.Run(out var card, "dotnet", _dllPath, "help", "mathml", "nothing", "--json");

        // Assert: exit code 1, and one failure document of the help command with the error object
        foreach (var (exit, output, message) in new[]
                 {
                     (subjectExit, subject, "Unknown help subject 'nothing'."),
                     (cardExit, card, "Unknown help card 'nothing' under 'mathml'.")
                 })
        {
            Assert.Equal(1, exit);
            var line = Assert.Single(output.Split('\n'), l => l.StartsWith('{'));
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            Assert.Equal(["schemaVersion", "ok", "command", "error"], root.EnumerateObject().Select(p => p.Name));
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.Equal("help", root.GetProperty("command").GetString());
            Assert.Equal("usage-error", root.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(message, root.GetProperty("error").GetProperty("message").GetString());
            Assert.False(string.IsNullOrEmpty(root.GetProperty("error").GetProperty("hint").GetString()));
        }
    }

    /// <summary>
    ///     Test that the Mermaid mindmap example, whose source escapes the hexagon node <c>pkg{{Package}}</c> from the
    ///     token replacement, prints the node as Mermaid writes it and renders in strict mode without a diagnostic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_HelpTopics_MindmapExample_PrintsTheHexagonAndRendersStrict()
    {
        // Arrange: a folder for the example and the picture
        using var directory = new TempDirectory();
        var input = directory.Combine("mindmap.mmd");
        var output = directory.Combine("mindmap.png");

        // Act: print the example, save it, and render it in strict mode
        var exampleExit = Runner.Run(out var example, "dotnet", _dllPath, "help", "mermaid", "mindmap", "--example", "mindmap");
        File.WriteAllText(input, example);
        var renderExit = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");

        // Assert: the hexagon node is literal, and the picture is drawn without a diagnostic
        Assert.Equal(0, exampleExit);
        Assert.Contains("pkg{{Package}}", example);
        Assert.DoesNotContain("\\{{", example);
        Assert.Equal(0, renderExit);
        using var document = JsonDocument.Parse(render);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.True(File.Exists(output));
    }
}
