using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests of the file-access options of the RenderCommand unit (<c>--file-access</c> and <c>--allow-root</c>):
///     the grammar, the policy each mode builds, the default folders of <c>roots</c>, the usage errors, and the effect
///     of the policy on what render and its verbs read.
/// </summary>
[Collection("Sequential")]
public class RenderCommandFileAccessTests
{
    /// <summary>The red of the background of the SVG files of these tests.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     Reads the policy of a command line of <c>render</c>.
    /// </summary>
    /// <param name="workingDirectory">The working directory a relative input or folder is taken from.</param>
    /// <param name="input">The input as written.</param>
    /// <param name="args">The file-access options.</param>
    /// <returns>The policy.</returns>
    private static FileAccessPolicy Read(string workingDirectory, string input, params string[] args)
    {
        var reader = ArgumentReader.Parse("render", [input, .. args], RenderCommand.OptionTable);
        return RenderCommand.ReadFileAccess(reader, [input], workingDirectory);
    }

    /// <summary>
    ///     Determines whether a policy allows a file, read by its own path.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="baseDirectory">The folder the lookup started from.</param>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true"/> when the policy allows it.</returns>
    private static bool Allows(FileAccessPolicy policy, string baseDirectory, string path)
    {
        return policy.Check(new FileAccessRequest
        {
            Reference = Path.GetFileName(path),
            BaseDirectory = baseDirectory,
            RequestedPath = path,
            RealPath = path
        }).IsAllowed;
    }

    /// <summary>
    ///     Writes a 120 by 80 red SVG diagram that links a picture outside its folder, and the green picture.
    /// </summary>
    /// <param name="directory">The temporary directory: the diagram is written to its <c>site</c> folder.</param>
    /// <returns>The path of the diagram.</returns>
    private static string WriteDiagramWithOutsidePicture(TempDirectory directory)
    {
        using (var surface = new Surface(4, 4))
        {
            surface.Clear(new Rgba32(0, 255, 0, 255));
            PngCodec.Save(surface, directory.Combine("secret.png"));
        }

        return FixtureFactory.WriteText(
            directory.Combine("site"),
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 80' width='120' height='80'>"
            + "<rect width='120' height='80' fill='#ff0000'/>"
            + "<image href='../secret.png' x='10' y='10' width='20' height='20'/></svg>");
    }

    /// <summary>
    ///     Test that without either option the policy is the default, which allows every file.
    /// </summary>
    [Fact]
    public void RenderCommand_ReadFileAccess_WithoutOptions_IsAllowAll()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var policy = Read(directory.Path, "in.svg");

        // Assert
        Assert.Same(FileAccessPolicy.AllowAll, policy);
    }

    /// <summary>
    ///     Test that each mode, in any letter case and with spaces around its tokens, builds its policy.
    /// </summary>
    /// <param name="mode">The value of <c>--file-access</c>.</param>
    /// <param name="expected">The name of the policy.</param>
    [Theory]
    [InlineData("any", "any")]
    [InlineData("roots", "roots")]
    [InlineData("no-symlinks", "no-symlinks")]
    [InlineData("roots,no-symlinks", "roots+no-symlinks")]
    [InlineData("NO-SYMLINKS, roots", "roots+no-symlinks")]
    public void RenderCommand_ReadFileAccess_EachMode_BuildsItsPolicy(string mode, string expected)
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var policy = Read(directory.Path, "in.svg", "--file-access", mode);

        // Assert: the name, and the shared instances for the modes without folders
        Assert.Equal(expected, policy.Name);
        if (expected == "any")
        {
            Assert.Same(FileAccessPolicy.AllowAll, policy);
        }
        else if (expected == "no-symlinks")
        {
            Assert.Same(FileAccessPolicy.NoSymlinks, policy);
        }
    }

    /// <summary>
    ///     Test that a value that is not a mode is a usage error that lists the modes.
    /// </summary>
    /// <param name="mode">The value of <c>--file-access</c>.</param>
    [Theory]
    [InlineData("all")]
    [InlineData("any,roots")]
    [InlineData("")]
    [InlineData("roots,,no-symlinks")]
    [InlineData("roots;no-symlinks")]
    public void RenderCommand_ReadFileAccess_InvalidMode_IsUsageErrorListingTheModes(string mode)
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var failure = Assert.Throws<ToolException>(() => Read(directory.Path, "in.svg", "--file-access", mode));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, failure.Code);
        Assert.Equal($"'{mode}' is not a value of --file-access.", failure.Message);
        Assert.Equal("Use any (the default), roots, no-symlinks, or roots,no-symlinks.", failure.Hint);
    }

    /// <summary>
    ///     Test that <c>--allow-root</c> alone confines references to the folders it names (a relative folder taken from
    ///     the working directory), which replace the default folders.
    /// </summary>
    [Fact]
    public void RenderCommand_ReadFileAccess_AllowRootAlone_ConfinesToTheNamedFolders()
    {
        // Arrange: two folders, the second named by its full path, and the input in a third
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("a"));
        Directory.CreateDirectory(directory.Combine("b"));
        Directory.CreateDirectory(directory.Combine("input"));
        var input = directory.Combine("input", "in.svg");

        // Act
        var policy = Read(directory.Path, input, "--allow-root", "a", "--allow-root", directory.Combine("b"));

        // Assert: the mode is roots; files in both folders are allowed; the folder of the input and the working
        // directory are not default folders any more
        Assert.Equal("roots", policy.Name);
        Assert.True(Allows(policy, directory.Path, directory.Combine("a", "x.png")));
        Assert.True(Allows(policy, directory.Path, directory.Combine("b", "y.png")));
        Assert.False(Allows(policy, directory.Path, directory.Combine("input", "z.png")));
        Assert.False(Allows(policy, directory.Path, directory.Combine("w.png")));
    }

    /// <summary>
    ///     Test that <c>--allow-root</c> with a mode that has no folders is a usage error that names the conflict.
    /// </summary>
    /// <param name="mode">The value of <c>--file-access</c>.</param>
    /// <param name="message">The message.</param>
    /// <param name="hint">The hint.</param>
    [Theory]
    [InlineData(
        "ANY",
        "The options --file-access any and --allow-root cannot be used together.",
        "--allow-root confines references to the folders it names: drop --file-access any, or write --file-access roots.")]
    [InlineData(
        "no-symlinks",
        "The options --file-access no-symlinks and --allow-root cannot be used together.",
        "To confine references to the folders and refuse links, write --file-access roots,no-symlinks with --allow-root.")]
    public void RenderCommand_ReadFileAccess_AllowRootWithAnyOrNoSymlinks_IsUsageErrorNamingTheConflict(string mode, string message, string hint)
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var failure = Assert.Throws<ToolException>(() => Read(directory.Path, "in.svg", "--file-access", mode, "--allow-root", "."));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, failure.Code);
        Assert.Equal(message, failure.Message);
        Assert.Equal(hint, failure.Hint);
    }

    /// <summary>
    ///     Test that an <c>--allow-root</c> folder that does not exist (or is a file) is a usage error.
    /// </summary>
    [Fact]
    public void RenderCommand_ReadFileAccess_AllowRootThatDoesNotExist_IsUsageError()
    {
        // Arrange: a file where a folder is expected
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "file.txt", "x");

        // Act
        var missing = Assert.Throws<ToolException>(() => Read(directory.Path, "in.svg", "--allow-root", "missing"));
        var file = Assert.Throws<ToolException>(() => Read(directory.Path, "in.svg", "--allow-root", "file.txt"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, missing.Code);
        Assert.Equal("The folder 'missing' of --allow-root does not exist.", missing.Message);
        Assert.Equal("Name an existing folder; a relative folder is taken from the current folder.", missing.Hint);
        Assert.Equal("The folder 'file.txt' of --allow-root does not exist.", file.Message);
    }

    /// <summary>
    ///     Test that the default folders of <c>roots</c> are the working directory and the folder of each input outside
    ///     it, once each, with a fragment split off by the file it follows.
    /// </summary>
    [Fact]
    public void RenderCommand_DefaultRoots_AreTheWorkingDirectoryAndEachInputFolderOutsideIt()
    {
        // Arrange: the working directory, an input inside it, two inputs in one other folder, and a fragment input
        using var directory = new TempDirectory();
        var work = directory.Combine("work");
        Directory.CreateDirectory(work);
        FixtureFactory.WriteText(directory.Path, "frag/part.scad", "cube(1);\n");
        var inputs = new[]
        {
            Path.Combine(work, "sub", "a.svg"),
            "relative.svg",
            directory.Combine("other", "b.svg"),
            directory.Combine("other", "c.svg"),
            directory.Combine("frag", "part.scad") + "#view=top"
        };

        // Act
        var roots = RenderCommand.DefaultRoots(inputs, work);

        // Assert
        Assert.Equal([work, directory.Combine("other"), directory.Combine("frag")], roots);
    }

    /// <summary>
    ///     Test that <c>--file-access roots</c> without <c>--allow-root</c> allows the working directory and the folder of
    ///     the input, and nothing else.
    /// </summary>
    [Fact]
    public void RenderCommand_ReadFileAccess_RootsWithoutAllowRoot_UsesTheDefaultRoots()
    {
        // Arrange: the input outside the working directory (the roots are taken when the policy is made, so the folders exist)
        using var directory = new TempDirectory();
        var work = directory.Combine("work");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(directory.Combine("site"));
        var input = directory.Combine("site", "in.svg");

        // Act
        var policy = Read(work, input, "--file-access", "roots");

        // Assert
        Assert.Equal("roots", policy.Name);
        Assert.True(Allows(policy, work, Path.Combine(work, "x.png")));
        Assert.True(Allows(policy, work, directory.Combine("site", "y.png")));
        Assert.False(Allows(policy, work, directory.Combine("z.png")));
    }

    /// <summary>
    ///     Test that an OpenSCAD include outside the folders of <c>roots</c> is the error <c>file-access-denied</c>, which
    ///     names the include, and nothing is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadIncludeOutsideTheRoots_FailsWithFileAccessDenied()
    {
        // Arrange: a model that includes a file in the folder above it
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "secret.scad", "module secret() { cube(1); }\n");
        var input = FixtureFactory.WriteText(directory.Path, "model/outside.scad", "include <../secret.scad>\nsecret();\n");
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--file-access", "roots", "--size", "16x16", "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.FileAccessDenied, error.GetProperty("code").GetString());
        Assert.Contains("../secret.scad", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a Vega-Lite data file outside the folders of <c>roots</c> is the error <c>file-access-denied</c>, and
    ///     nothing is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteDataOutsideTheRoots_FailsWithFileAccessDenied()
    {
        // Arrange: a chart whose data file is in the folder above it
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "bars.csv", FixtureFactory.TwoBarCsv);
        var input = FixtureFactory.WriteText(
            directory.Path,
            "charts/bars.vl.json",
            FixtureFactory.TwoBarFileChart.Replace("bars.csv", "../bars.csv", StringComparison.Ordinal));
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--allow-root", directory.Combine("charts"), "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(ErrorCodes.FileAccessDenied, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that with <c>--strict</c> a picture the mode denies is the error <c>file-access-denied</c>, and nothing is
    ///     written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingDeniedWithStrict_FailsWithFileAccessDenied()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDiagramWithOutsidePicture(directory);
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--file-access", "roots", "--strict", "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(ErrorCodes.FileAccessDenied, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the verbs take the file-access options too: <c>resize</c> under <c>roots</c> leaves out a picture
    ///     outside the folder and reports it.
    /// </summary>
    [Fact]
    public void RenderCommand_Verb_FileAccessOption_AppliesToTheVerbs()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDiagramWithOutsidePicture(directory);
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("resize", input, "120x80", "--file-access", "roots", "-o", output, "--json");

        // Assert: one warning, and the background where the picture would be
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("image-access-denied", diagnostic.GetProperty("code").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal(Red, image[20, 20]);
    }
}
