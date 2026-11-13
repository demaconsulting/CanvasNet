using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the file-access options of the CanvasNet Tool (<c>--file-access</c> and
///     <c>--allow-root</c>), run as a real process via <c>dotnet</c>.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that <c>--allow-root</c> confines the references of every command that reads an input (render, inspect,
    ///     compare, montage, and flatten): a diagram inside the named folder is drawn, one outside it is denied.
    /// </summary>
    [Fact]
    public void CanvasNetTool_FileAccess_AllowRootAsProcess_ConfinesEveryReadingCommand()
    {
        // Arrange: a drawing in "site" that links a diagram in "site/parts" (allowed) and one in "other" (denied)
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site", "parts"));
        Directory.CreateDirectory(directory.Combine("other"));
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("site", "parts", "inside.mmd"));
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("other", "outside.mmd"));
        var input = FixtureFactory.WriteText(
            directory.Path,
            "site/diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'>"
            + "<image href='parts/inside.mmd' width='50' height='40'/>"
            + "<image href='../other/outside.mmd' y='50' width='50' height='40'/></svg>");
        var root = directory.Combine("site");
        string[][] commands =
        [
            ["render", input, "-o", directory.Combine("render.png")],
            ["inspect", "colors", input],
            ["compare", input, input],
            ["montage", input, "-o", directory.Combine("montage.png")],
            ["flatten", input, "-o", directory.Combine("flat.svg")]
        ];

        foreach (var command in commands)
        {
            // Act
            var exitCode = Runner.Run(out var output, "dotnet", [_dllPath, .. command, "--allow-root", root, "--json"]);

            // Assert: success, with exactly one denial, for the diagram outside the folder
            Assert.True(exitCode == 0, $"{command[0]} failed: {output}");
            using var document = JsonDocument.Parse(output);
            var denied = document.RootElement.GetProperty("diagnostics").EnumerateArray()
                .Where(d => d.GetProperty("code").GetString() == "image-access-denied")
                .ToList();
            var note = Assert.Single(denied.DistinctBy(d => d.GetProperty("message").GetString()));
            Assert.Contains("outside.mmd", note.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        // Assert: flatten drew the allowed diagram
        Assert.Contains("data:image/png;base64,", File.ReadAllText(directory.Combine("flat.svg")), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the mistakes of the file-access options are usage errors with their documented messages, reported
    ///     before any input is read.
    /// </summary>
    [Fact]
    public void CanvasNetTool_FileAccess_MistakesAsProcess_AreUsageErrors()
    {
        // Arrange: an input that does not exist, so a mistake must be reported before the input is opened
        using var directory = new TempDirectory();
        var input = directory.Combine("missing.svg");
        var output = directory.Combine("out.png");
        var cases = new (string[] Args, string Message)[]
        {
            (["--file-access", "everything"], "Error: usage-error: 'everything' is not a value of --file-access."),
            (["--file-access", "any", "--allow-root", directory.Path], "Error: usage-error: The options --file-access any and --allow-root cannot be used together."),
            (["--allow-root", directory.Combine("nowhere")], $"Error: usage-error: The folder '{directory.Combine("nowhere")}' of --allow-root does not exist.")
        };

        foreach (var (args, message) in cases)
        {
            // Act
            var exitCode = Runner.Run(out var text, "dotnet", [_dllPath, "render", input, "-o", output, .. args]);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains(message, text, StringComparison.Ordinal);
        }
    }
}
