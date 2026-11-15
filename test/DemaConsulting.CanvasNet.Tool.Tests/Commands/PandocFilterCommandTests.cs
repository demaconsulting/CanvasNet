using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.PandocFilter;
using DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the PandocFilterCommand unit, through the tool's entry point with files instead of standard input
///     and output.
/// </summary>
[Collection("Sequential")]
public class PandocFilterCommandTests
{
    /// <summary>
    ///     Test that <c>--write-shim</c> writes the embedded shim and says so, as text and as JSON.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Run_WriteShim_WritesTheShimAsTextOrJson()
    {
        // Arrange: two paths in a new folder
        using var directory = new TempDirectory();
        var text = directory.Combine("filters", "canvasnet.lua");
        var json = directory.Combine("json", "canvasnet.lua");

        // Act
        var (textCode, textOut, _) = ToolHost.Invoke("pandoc-filter", "--write-shim", text);
        var (jsonCode, jsonOut, _) = ToolHost.Invoke("pandoc-filter", "--write-shim", json, "--json");

        // Assert: the shim is written both times; the line and the document describe it
        Assert.Equal(0, textCode);
        Assert.Equal($"wrote {text} (protocol 1)", textOut.TrimEnd());
        Assert.Equal(PandocFilterShim.Bytes, File.ReadAllBytes(text));
        Assert.Equal(0, jsonCode);
        using var document = JsonDocument.Parse(jsonOut);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("pandoc-filter", document.RootElement.GetProperty("command").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("protocol").GetInt32());
        Assert.Equal(json, document.RootElement.GetProperty("outputs")[0].GetProperty("path").GetString());
        Assert.Equal(PandocFilterShim.Bytes, File.ReadAllBytes(json));
    }

    /// <summary>
    ///     Test that a shim of another protocol is a usage error, reported before the input is read, that names the
    ///     command that fixes it.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Run_ProtocolMismatch_IsUsageErrorNamingTheFix()
    {
        // Act: an input that does not exist, which is never read
        var (oldCode, oldOut, oldErr) = ToolHost.Invoke("pandoc-filter", "--json", "--protocol", "0", "--filter-path", "docs/filters/canvasnet.lua", "--input", "missing.json");
        var (newCode, newOut, _) = ToolHost.Invoke("pandoc-filter", "--json", "--protocol", "2", "--filter-path", "docs/filters/canvasnet.lua");

        // Assert: the JSON failure documents the shim turns into the build's error
        Assert.Equal(1, oldCode);
        using var old = JsonDocument.Parse(oldOut);
        Assert.Equal("usage-error", old.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("docs/filters/canvasnet.lua speaks protocol 0; this tool needs protocol 1", old.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal("Write the shim again: dotnet canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua", old.RootElement.GetProperty("error").GetProperty("hint").GetString());
        Assert.Contains("Error: usage-error: docs/filters/canvasnet.lua speaks protocol 0", oldErr);
        Assert.Equal(1, newCode);
        Assert.Contains("this tool speaks protocol 1 at most", newOut);
    }

    /// <summary>
    ///     Test the usage errors of the options.
    /// </summary>
    /// <param name="args">The arguments after the command.</param>
    /// <param name="message">A part of the expected message.</param>
    [Theory]
    [InlineData(new[] { "--filter-path", "f.lua" }, "No --protocol was given.")]
    [InlineData(new[] { "--protocol", "one", "--filter-path", "f.lua" }, "The option --protocol needs a whole number, but 'one' was given.")]
    [InlineData(new[] { "--protocol", "1" }, "No --filter-path was given.")]
    [InlineData(new[] { "--protocol", "1", "--filter-path", "f.lua", "--jobs", "0" }, "--jobs")]
    [InlineData(new[] { "--protocol", "1", "--filter-path", "f.lua", "extra" }, "'pandoc-filter' takes no arguments")]
    public void PandocFilterCommand_Run_BadOptions_AreUsageErrors(string[] args, string message)
    {
        // Act
        var (code, _, stdErr) = ToolHost.Invoke(["pandoc-filter", .. args]);

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("Error: usage-error: ", stdErr);
        Assert.Contains(message, stdErr);
    }

    /// <summary>
    ///     Test a run over files: the document is transformed and written to the output file, and nothing goes to
    ///     standard output.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Run_InputAndOutputFiles_TransformTheDocument()
    {
        // Arrange: a document with a Mermaid code block and non-ASCII text
        using var directory = new TempDirectory();
        var input = directory.Combine("doc.json");
        var output = directory.Combine("out.json");
        File.WriteAllBytes(input, PandocDoc.Document(PandocDoc.Para("""{"t":"Str","c":"Grüße"}"""), PandocDoc.CodeBlock("", ["mermaid"], "flowchart LR\n  A --> B")));

        // Act
        var (code, stdOut, stdErr) = ToolHost.Invoke(
            "pandoc-filter", "--protocol", "1", "--filter-path", directory.Combine("filters", "canvasnet.lua"), "--format", "html5",
            "--resource-path", directory.Path, "--jobs", "1", "--input", input, "--output", output);

        // Assert: a paragraph with the picture drawn into the cache, and the text unchanged
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stdOut);
        Assert.Equal(string.Empty, stdErr);
        var bytes = File.ReadAllBytes(output);
        Assert.Contains("Grüße", Encoding.UTF8.GetString(bytes));
        var src = Assert.Single(PandocDoc.ImageSources(bytes));
        Assert.True(File.Exists(src), $"The picture {src} was not drawn.");
        Assert.Contains("/generated/canvasnet/mermaid-", src);
    }

    /// <summary>
    ///     Test that a failure of the filter is the JSON failure document with the filter's code and message, and that no
    ///     output is written.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Run_MissingSource_IsTheFilterErrorAndWritesNothing()
    {
        // Arrange: a document whose model does not exist, and a malformed document
        using var directory = new TempDirectory();
        var input = directory.Combine("doc.json");
        var broken = directory.Combine("broken.json");
        var output = directory.Combine("out.json");
        File.WriteAllBytes(input, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("part.scad"))));
        File.WriteAllText(broken, "{");
        string[] common = ["pandoc-filter", "--json", "--protocol", "1", "--filter-path", directory.Combine("f", "canvasnet.lua"), "--resource-path", directory.Path, "--output", output];

        // Act
        var (code, stdOut, _) = ToolHost.Invoke([.. common, "--input", input]);
        var (corruptCode, corruptOut, _) = ToolHost.Invoke([.. common, "--input", broken]);

        // Assert
        Assert.Equal(1, code);
        using var failure = JsonDocument.Parse(stdOut);
        Assert.Equal("pandoc-filter-error", failure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("cannot find 'part.scad' in the document's resource path", failure.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(1, corruptCode);
        Assert.Contains("\"corrupt-input\"", corruptOut);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a failed run writes the warnings of the pictures before the failure to standard error, in Pandoc's
    ///     format, as the Lua filter printed them before its error.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Run_FailureAfterAWarning_WritesTheWarningToStandardError()
    {
        // Arrange: a model that echoes a line, then a model that does not exist
        using var directory = new TempDirectory();
        File.WriteAllText(directory.Combine("echo.scad"), "echo(\"hello from the model\"); cube(5);");
        var input = directory.Combine("doc.json");
        File.WriteAllBytes(input, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("echo.scad"), PandocDoc.Image("part.scad"))));

        // Act
        var (code, _, stdErr) = ToolHost.Invoke(
            "pandoc-filter", "--json", "--protocol", "1", "--filter-path", directory.Combine("f", "canvasnet.lua"), "--resource-path", directory.Path,
            "--jobs", "1", "--input", input, "--output", directory.Combine("out.json"));

        // Assert: the warning, then the error
        Assert.Equal(1, code);
        var warning = stdErr.IndexOf("[WARNING] canvasnet.lua: echo.scad: ", StringComparison.Ordinal);
        Assert.True(warning >= 0, stdErr);
        Assert.Contains("hello from the model", stdErr[warning..], StringComparison.Ordinal);
        Assert.True(stdErr.IndexOf("Error: pandoc-filter-error: cannot find 'part.scad'", StringComparison.Ordinal) > warning, stdErr);
    }

    /// <summary>
    ///     Test that the help of the command lists its options and points at the help card.
    /// </summary>
    [Fact]
    public void PandocFilterCommand_Help_ListsOptionsAndTheCard()
    {
        // Act
        var (code, stdOut, _) = ToolHost.Invoke("help", "pandoc-filter");

        // Assert
        Assert.Equal(0, code);
        foreach (var option in PandocFilterCommand.OptionTable.Select(o => o.Name))
        {
            Assert.Contains(option, stdOut);
        }

        Assert.Contains("canvasnet help pandoc", stdOut);
    }
}
