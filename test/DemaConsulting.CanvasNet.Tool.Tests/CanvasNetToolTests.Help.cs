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
}
