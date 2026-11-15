using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the <c>pandoc-filter</c> command of the CanvasNet Tool, run as a real process via
///     <c>dotnet</c> with the document on standard input, as the Lua shim <c>docs/filters/canvasnet.lua</c> runs it.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, draws every picture of a document read from standard input in one
    ///     run, writes the transformed document to standard output, leaves other pictures and text alone, and reuses the
    ///     cache on a second run.
    /// </summary>
    [Fact]
    public void CanvasNetTool_PandocFilter_DocumentOnStandardInput_DrawsThePicturesInOneRun()
    {
        // Arrange: a diagram picture, a raster picture, non-ASCII text, and a captioned diagram block
        using var directory = new TempDirectory();
        var guide = directory.Combine("docs", "guide");
        Directory.CreateDirectory(guide);
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), Path.Combine(guide, "flow.mmd"));
        var document = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Image("flow.mmd"), PandocDoc.Image("logo.png"), """{"t":"Str","c":"Grüße"}"""),
            PandocDoc.CodeBlock("fig-order", ["mermaid"], "flowchart LR\n  O[Order] --> P[Pay]", ("caption", "The order")));
        string[] args =
        [
            _dllPath, "pandoc-filter", "--json", "--protocol", "1", "--filter-path", directory.Combine("docs", "filters", "canvasnet.lua"),
            "--format", "html5", "--resource-path", guide
        ];

        // Act: two runs over the same document
        var first = RunWithInput(document, out var output, out var errors, args);
        var second = RunWithInput(document, out var again, out _, args);

        // Assert: one document with both pictures drawn into the cache and the rest unchanged
        Assert.Equal(0, first);
        Assert.Equal(string.Empty, errors);
        Assert.Contains("Grüße", Encoding.UTF8.GetString(output), StringComparison.Ordinal);
        var sources = PandocDoc.ImageSources(output);
        Assert.Equal(3, sources.Count);
        Assert.Equal("logo.png", sources[1]);
        Assert.All(new[] { sources[0], sources[2] }, src => Assert.True(File.Exists(src), $"The picture {src} was not drawn."));
        using var json = JsonDocument.Parse(output);
        Assert.Equal("Figure", json.RootElement.GetProperty("blocks")[1].GetProperty("t").GetString());

        // Assert: the second run finds the pictures in the cache and writes the same document
        Assert.Equal(0, second);
        Assert.Equal(output, again);
        Assert.Equal(2, Directory.GetFiles(directory.Combine("docs", "generated", "canvasnet"), "*.png").Length);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, writes the Lua shim, and that a shim of another protocol and a
    ///     mistake in a picture are expected errors that write the JSON failure document and no transformed document.
    /// </summary>
    [Fact]
    public void CanvasNetTool_PandocFilter_ShimProtocolAndMistakes_AreReportedAsExpectedErrors()
    {
        // Arrange: a path for the shim, and a document whose model has a bad size
        using var directory = new TempDirectory();
        var shim = directory.Combine("docs", "filters", "canvasnet.lua");
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("part.scad", ("size", "0x10"))));

        // Act
        var write = Runner.Run(out var written, "dotnet", _dllPath, "pandoc-filter", "--write-shim", shim);
        var old = RunWithInput(document, out var oldOutput, out var oldErrors, _dllPath, "pandoc-filter", "--json", "--protocol", "0", "--filter-path", shim);
        var mistake = RunWithInput(
            document, out var mistakeOutput, out _, _dllPath, "pandoc-filter", "--json", "--protocol", "1", "--filter-path", shim, "--resource-path", directory.Path);

        // Assert: the shim is written and names its protocol
        Assert.Equal(0, write);
        Assert.Equal($"wrote {shim} (protocol 1)", written.TrimEnd());
        Assert.Contains("local PROTOCOL = \"1\"", File.ReadAllText(shim), StringComparison.Ordinal);

        // Assert: an outdated shim is a usage error whose hint writes the shim again
        Assert.Equal(1, old);
        using var oldFailure = JsonDocument.Parse(oldOutput);
        Assert.Equal("usage-error", oldFailure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal($"Write the shim again: dotnet canvasnet pandoc-filter --write-shim {shim}", oldFailure.RootElement.GetProperty("error").GetProperty("hint").GetString());
        Assert.Contains("Error: usage-error:", oldErrors, StringComparison.Ordinal);

        // Assert: a bad attribute stops the run with the filter's code and a message that names the picture
        Assert.Equal(1, mistake);
        using var failure = JsonDocument.Parse(mistakeOutput);
        Assert.Equal("pandoc-filter-error", failure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.StartsWith("'part.scad' has size=0x10", failure.RootElement.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Runs the tool through <c>dotnet</c> with bytes on standard input, as Pandoc's <c>pandoc.pipe</c> does, and keeps
    ///     standard output as raw bytes.
    /// </summary>
    /// <param name="input">The bytes written to standard input.</param>
    /// <param name="output">Receives standard output, as bytes.</param>
    /// <param name="errors">Receives standard error.</param>
    /// <param name="arguments">The arguments of <c>dotnet</c>.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process fails to start.</exception>
    private static int RunWithInput(byte[] input, out byte[] output, out string errors, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process");
        using var memory = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(memory);
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            process.StandardInput.BaseStream.Write(input);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The tool may exit before it reads the input (an outdated shim); the pipe is then closed
        }
        process.WaitForExit();
        outputTask.GetAwaiter().GetResult();
        errors = errorTask.GetAwaiter().GetResult();
        output = memory.ToArray();
        return process.ExitCode;
    }
}
