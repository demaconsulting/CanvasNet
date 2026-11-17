using System.Text;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the PandocFilterShim unit: the embedded shim, its protocol, and writing it.
/// </summary>
public class PandocFilterShimTests
{
    /// <summary>
    ///     Test that the shim is the source template with the filter's tables filled in (line endings aside), speaks the
    ///     current protocol, and names the tool version that has the command.
    /// </summary>
    [Fact]
    public void PandocFilterShim_Bytes_AreTheSourceFileAndSpeakTheProtocol()
    {
        // Arrange: the source template of the shim
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "DemaConsulting.CanvasNet.Tool", "PandocFilter", "canvasnet.lua"));

        // Act
        var text = Encoding.UTF8.GetString(PandocFilterShim.Bytes);

        // Assert
        Assert.Contains(PandocFilterShim.ClassesPlaceholder, source, StringComparison.Ordinal);
        Assert.Contains(PandocFilterShim.SuffixesPlaceholder, source, StringComparison.Ordinal);
        Assert.Equal(PandocFilterShim.Generate(source).Replace("\r\n", "\n", StringComparison.Ordinal), text.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains($"local PROTOCOL = \"{PandocFilterShim.Protocol}\"", text, StringComparison.Ordinal);
        Assert.Contains("DemaConsulting.CanvasNet.Tool 0.2.0-beta.11 or later", text, StringComparison.Ordinal);
        Assert.InRange(PandocFilterShim.Protocol, PandocFilterShim.MinProtocol, PandocFilterShim.MaxProtocol);
    }

    /// <summary>
    ///     Test that the shim's fast path lists exactly the code-block classes and picture suffixes the engine draws, written
    ///     from the engine's own tables, and that no placeholder is left.
    /// </summary>
    [Fact]
    public void PandocFilterShim_Bytes_ListWhatTheEngineDraws()
    {
        // Act
        var text = Encoding.UTF8.GetString(PandocFilterShim.Bytes);

        // Assert
        Assert.Contains("local CLASSES = { \"mermaid\" }\n", text, StringComparison.Ordinal);
        Assert.Contains(
            "local SUFFIXES = { \".svg\", \".vl.json\", \".vl\", \".scad\", \".vsdx\", \".pptx\", \".pdf\", \".mmd\", \".mermaid\" }\n",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(PandocFilterShim.ClassesPlaceholder, text, StringComparison.Ordinal);
        Assert.DoesNotContain(PandocFilterShim.SuffixesPlaceholder, text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that generating the shim fills each placeholder with a Lua list of strings, and refuses a template whose
    ///     placeholder is missing or repeated (a defect of the build).
    /// </summary>
    [Fact]
    public void PandocFilterShim_Generate_PlaceholdersExactlyOnce_FillsOrRefuses()
    {
        // Act
        var filled = PandocFilterShim.Generate("c = @CLASSES@\ns = @SUFFIXES@\n");
        var missing = Assert.Throws<InvalidOperationException>(() => PandocFilterShim.Generate("c = @CLASSES@\n"));
        var twice = Assert.Throws<InvalidOperationException>(() => PandocFilterShim.Generate("@CLASSES@ @CLASSES@ @SUFFIXES@"));

        // Assert
        Assert.StartsWith("c = { \"mermaid\" }\ns = { \".svg\", ", filled, StringComparison.Ordinal);
        Assert.Contains("@SUFFIXES@", missing.Message, StringComparison.Ordinal);
        Assert.Contains("@CLASSES@", twice.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the repository's filter, <c>docs/filters/canvasnet.lua</c>, is the embedded shim (line endings aside),
    ///     so the documents of this repository are built with the shim the tool writes.
    /// </summary>
    [Fact]
    public void PandocFilterShim_Bytes_AreTheRepositoryFilter()
    {
        // Arrange: the filter the documents of the repository use
        var filter = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "filters", "canvasnet.lua"));

        // Act
        var text = Encoding.UTF8.GetString(PandocFilterShim.Bytes);

        // Assert
        Assert.Equal(text.Replace("\r\n", "\n", StringComparison.Ordinal), filter.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that writing the shim creates the folder, replaces an existing file, and leaves no partial file.
    /// </summary>
    [Fact]
    public void PandocFilterShim_Write_NewFolderAndExistingFile_WritesTheShim()
    {
        // Arrange: a path in a folder that does not exist yet
        using var directory = new TempDirectory();
        var path = directory.Combine("docs", "filters", "canvasnet.lua");

        // Act: write it, change it, and write it again
        PandocFilterShim.Write(path);
        File.WriteAllText(path, "old");
        var bytes = PandocFilterShim.Write(path);

        // Assert
        Assert.Equal(PandocFilterShim.Bytes, File.ReadAllBytes(path));
        Assert.Equal(PandocFilterShim.Bytes.Length, bytes);
        Assert.Single(Directory.GetFiles(directory.Combine("docs", "filters")));
    }

    /// <summary>
    ///     Test the protocol check: an older shim is told the command that writes it again for its own path, a newer one to
    ///     update the tool.
    /// </summary>
    [Fact]
    public void PandocFilterShim_CheckProtocol_OutsideTheRange_NamesTheFix()
    {
        // Act
        var old = Assert.Throws<ToolException>(() => PandocFilterShim.CheckProtocol(0, "docs/x/canvasnet.lua"));
        var future = Assert.Throws<ToolException>(() => PandocFilterShim.CheckProtocol(2, null));

        // Assert
        PandocFilterShim.CheckProtocol(1, "docs/x/canvasnet.lua");
        Assert.Equal(ErrorCodes.UsageError, old.Code);
        Assert.Equal("docs/x/canvasnet.lua speaks protocol 0; this tool needs protocol 1", old.Message);
        Assert.Equal("Write the shim again: dotnet canvasnet pandoc-filter --write-shim docs/x/canvasnet.lua", old.Hint);
        Assert.Equal("docs/filters/canvasnet.lua speaks protocol 2; this tool speaks protocol 1 at most", future.Message);
        Assert.StartsWith("Update DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json", future.Hint);
    }

    /// <summary>
    ///     Finds the repository root: the folder above the test assembly that holds the solution file.
    /// </summary>
    /// <returns>The repository root.</returns>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CanvasNet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
