using System.Text;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the PictureCache unit: the key, the names, and the atomic writes.
/// </summary>
public class PictureCacheTests
{
    /// <summary>
    ///     The identity of a tool of version 1.0.0 run by the default command, without a fingerprint.
    /// </summary>
    private const string Identity = "1.0.0\ndefault\n";

    /// <summary>
    ///     Test the key against golden values: fixed fields and parts give fixed SHA-1 values.
    /// </summary>
    /// <remarks>
    ///     The expected values were computed by the Lua filter's own composition in Pandoc 3.12.1 (<c>dotnet pandoc lua</c>
    ///     with <c>pandoc.sha1(table.concat({"canvasnet.lua 17", kind, identity, access, table.concat(options, ";"),
    ///     [folder], table.concat(parts, "\n\0\n")}, "\n\0\n"))</c>), so they prove the C# key is the Lua key. With the
    ///     version 16 the same inputs give 0f6ea14899f9d6b1c6831f9e4b3e1a4525e64121 (mermaid),
    ///     2e7f031493c27dbb90e0f093365fa039960ed733 (svg), 19c80853fc4381427cd646e3e62db50d50332e2d (scad), and
    ///     40decae4abb2a749cbe8d1f84fd070ae4da4f1d7 (no parts). A run of the old filter (version 16) and of the
    ///     pandoc-filter command built with <see cref="PictureCache.KeyVersion"/> set to 16 over the same documents wrote
    ///     identical cache names.
    /// </remarks>
    [Fact]
    public void PictureCache_ComputeKey_FixedInputs_GiveTheGoldenKeys()
    {
        // Act
        var mermaid = PictureCache.ComputeKey("mermaid", Identity, "file-access\n", ["--theme=forest"], null, [Bytes("flow.mmd\nflowchart LR\n  A --> B\n")]);
        var svg = PictureCache.ComputeKey(
            "svg", Identity, "file-access\n--file-access\nroots", ["--scale=2"], "../../guide", [Bytes("drawing.svg\n<svg/>"), Bytes("missing gone.mmd")]);
        var scad = PictureCache.ComputeKey(
            "scad", Identity, "file-access\n", ["--background=white", "--max-triangles=2000000", "--size=1600x1200"], null,
            [Bytes("part.scad\ncube(1);\n"), Bytes("lib/base.scad\nmodule b() {}\n")]);
        var empty = PictureCache.ComputeKey("pdf", Identity, "file-access\n", ["--page=1"], null, []);

        // Assert
        Assert.Equal("17", PictureCache.KeyVersion);
        Assert.Equal("6298b86b9e5bd7dda51b245c692ba403feed8b57", mermaid);
        Assert.Equal("efe907aef0d66f34f30a518c88f4f512686bb968", svg);
        Assert.Equal("fbeacb242b09040c49aab0312b6f397ff12b0101", scad);
        Assert.Equal("af1dcf142cef5c1f00e833354d31a6b66f05c2da", empty);
    }

    /// <summary>
    ///     Test the names: the stem made of bytes, the picture, the copy, the markers, and the file of a code block (whose
    ///     name the old filter wrote for the same block).
    /// </summary>
    [Fact]
    public void PictureCache_Names_FollowTheLuaFilter()
    {
        // Arrange: a cache folder
        var cache = FilterPaths.Collapse(Path.Combine(Path.GetTempPath(), "cache"));

        // Act / Assert: the stems
        Assert.Equal("My-Chart", PictureCache.StemOf(FilterPaths.Join([cache, "My Chart.vl.json"])));
        Assert.Equal("a-b-c", PictureCache.StemOf("a.b.c.pdf"));
        Assert.Equal("caf--", PictureCache.StemOf("café.scad"));

        // Act / Assert: the files
        Assert.Equal(FilterPaths.Join([cache, "flow-k.png"]), PictureCache.PictureFile(cache, "flow", "k"));
        Assert.Equal(FilterPaths.Join([cache, "d-k.svg"]), PictureCache.FlattenedFile(cache, "d", "k"));
        Assert.Equal(FilterPaths.Join([cache, "k.unsupported"]), PictureCache.UnsupportedMarker(cache, "k"));
        Assert.Equal(FilterPaths.Join([cache, "k.plain"]), PictureCache.PlainMarker(cache, "k"));
        Assert.Matches(@"k\.\d{9}\.partial\.png$", PictureCache.PartialFile(cache, "k", "png"));
        Assert.Equal(
            FilterPaths.Join([cache, "cec4b774cf6e92d591d5001e3909f9dc6ca2cdcc.mmd"]),
            PictureCache.BlockSourceFile(cache, "journey\n  title A day\n  section Go\n    Walk: 5: Me\n"));
        Assert.Equal(
            PictureCache.BlockSourceFile(cache, "journey\n  title A day\n  section Go\n    Walk: 5: Me\n"),
            PictureCache.BlockSourceFile(cache, "journey\n  title A day\n  section Go\n    Walk: 5: Me\n", PictureAttributes.Mermaid));
        Assert.Equal(
            FilterPaths.Join([cache, "eccade7d2733145c0211b90c5308527baf6876f0.puml"]),
            PictureCache.BlockSourceFile(cache, "@startuml\nAlice -> Bob\n@enduml\n", PictureAttributes.PlantUml));
    }

    /// <summary>
    ///     Test that the text of a MathML block is written to a <c>.mml</c> file named by the SHA-1 of the text after a prefix
    ///     that names the kind, so the same text in a Mermaid block gets another file.
    /// </summary>
    [Fact]
    public void PictureCache_BlockSourceFile_MathMlBlock_HasMmlExtension()
    {
        // Arrange: the cache folder, an equation, and the name its text and prefix give
        const string cache = "/docs/generated/canvasnet";
        const string text = "<math><mi>x</mi></math>\n";
        var hash = FilterSettings.Sha1Hex(System.Text.Encoding.UTF8.GetBytes("canvasnet.lua mathml block\n" + text));

        // Act
        var file = PictureCache.BlockSourceFile(cache, text, PictureAttributes.MathMl);

        // Assert: the hash of the prefixed text with the .mml extension, and another file than a Mermaid block's
        Assert.Equal(FilterPaths.Join([cache, hash + ".mml"]), file);
        Assert.NotEqual(Path.GetFileNameWithoutExtension(PictureCache.BlockSourceFile(cache, text)), Path.GetFileNameWithoutExtension(file));
    }

    /// <summary>
    ///     Test the atomic text write: a new file is written without a partial file left, and an existing one is kept.
    /// </summary>
    [Fact]
    public void PictureCache_WriteTextAtomically_NewAndExisting_WritesOnce()
    {
        // Arrange: a folder with one file already there
        using var directory = new TempDirectory();
        var existing = directory.Combine("old.plain");
        File.WriteAllText(existing, "kept");

        // Act
        PictureCache.WriteTextAtomically(directory.Combine("new.plain"), "é\n");
        PictureCache.WriteTextAtomically(existing, "replaced");

        // Assert
        Assert.Equal(new byte[] { 0xC3, 0xA9, 0x0A }, File.ReadAllBytes(directory.Combine("new.plain")));
        Assert.Equal("kept", File.ReadAllText(existing));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    /// <summary>
    ///     Test that a temporary file whose write fails after it was created is removed before the error is raised.
    /// </summary>
    [Fact]
    public void PictureCache_WriteTextAtomically_WriteFailsAfterCreate_RemovesThePartialFile()
    {
        // Arrange: a writer that creates the temporary file, then fails as a full disk would
        using var directory = new TempDirectory();
        var target = directory.Combine("flow.plain");
        string? created = null;
        void FailingWrite(string partial, byte[] bytes)
        {
            created = partial;
            File.WriteAllBytes(partial, bytes[..1]);
            throw new IOException("There is not enough space on the disk.");
        }

        // Act
        var error = Assert.Throws<ToolException>(() => PictureCache.WriteTextAtomically(target, "flowchart LR\n", FailingWrite));

        // Assert: the error names the temporary file, and neither it nor the target is left behind
        Assert.NotNull(created);
        Assert.Equal(ErrorCodes.PandocFilterError, error.Code);
        Assert.Equal($"could not write {created}", error.Message);
        Assert.IsType<IOException>(error.InnerException);
        Assert.False(File.Exists(created));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    /// <summary>
    ///     Test the commit: a rename that loses to another build keeps the other copy and removes the partial file, and a
    ///     rename that fails with nothing there is an error.
    /// </summary>
    [Fact]
    public void PictureCache_Commit_LostRaceKeepsTheOtherCopy_NothingThereThrows()
    {
        // Arrange: a picture another build wrote, and a partial file of this build
        using var directory = new TempDirectory();
        var target = directory.Combine("flow-k.png");
        var partial = directory.Combine("k.123456789.partial.png");
        File.WriteAllText(target, "first");
        File.WriteAllText(partial, "second");

        // Act
        PictureCache.Commit(partial, target, "unused");
        var error = Assert.Throws<ToolException>(() => PictureCache.Commit(directory.Combine("gone.partial.png"), directory.Combine("x.png"), "could not write x"));

        // Assert
        Assert.Equal("first", File.ReadAllText(target));
        Assert.False(File.Exists(partial));
        Assert.Equal(ErrorCodes.PandocFilterError, error.Code);
        Assert.Equal("could not write x", error.Message);
    }

    /// <summary>
    ///     Encodes a text as UTF-8.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }
}
