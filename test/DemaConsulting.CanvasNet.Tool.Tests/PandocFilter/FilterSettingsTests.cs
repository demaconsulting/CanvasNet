using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the FilterSettings unit: the file-access variables, the tool identity, the cache folder, the
///     <c>src</c> of a picture, and the number of jobs.
/// </summary>
public class FilterSettingsTests
{
    /// <summary>
    ///     Test that without the variables there are no file-access arguments and the key is the bare header.
    /// </summary>
    [Fact]
    public void FilterSettings_FileAccessOf_NoVariables_IsTheDefaultMode()
    {
        // Arrange
        using var tree = new FilterTree();

        // Act
        var access = tree.Settings().FileAccessOf(Path.Combine(tree.Guide, "a.scad"));

        // Assert
        Assert.Empty(access.Arguments);
        Assert.Null(access.Roots);
        Assert.False(access.Restrictive);
        Assert.Equal("file-access\n", access.Key);
    }

    /// <summary>
    ///     Test the mode roots without folders: it is normalized, and each picture names the working directory and, when
    ///     outside it, its own folder.
    /// </summary>
    [Fact]
    public void FilterSettings_FileAccessOf_RootsWithoutFolders_AddsTheDefaultFolders()
    {
        // Arrange: the mode with white space and capitals, and a source outside the working directory
        using var tree = new FilterTree();
        using var outside = new TempDirectory();
        tree.Environment[FilterSettings.FileAccessVariable] = "  Roots , No-Symlinks ";
        var settings = tree.Settings();
        var working = FilterPaths.Collapse(tree.Root);
        var folder = FilterPaths.Collapse(outside.Path);

        // Act
        var inside = settings.FileAccessOf(Path.Combine(tree.Guide, "a.scad"));
        var beyond = settings.FileAccessOf(outside.Combine("b.scad"));

        // Assert
        Assert.Equal(["--file-access", "roots,no-symlinks", "--allow-root", working], inside.Arguments);
        Assert.Equal([working], inside.Roots!);
        Assert.True(inside.Restrictive);
        Assert.Equal(["--file-access", "roots,no-symlinks", "--allow-root", working, "--allow-root", folder], beyond.Arguments);
        Assert.Equal($"file-access\n--file-access\nroots,no-symlinks\n--allow-root\n{working}\n--allow-root\n{folder}", beyond.Key);
    }

    /// <summary>
    ///     Test that folders imply the mode roots, each made absolute from the working directory.
    /// </summary>
    [Fact]
    public void FilterSettings_FileAccessOf_FoldersWithoutMode_ImplyRoots()
    {
        // Arrange: two folders, one relative
        using var tree = new FilterTree();
        var separator = OperatingSystem.IsWindows() ? ";" : ":";
        tree.Environment[FilterSettings.RootsVariable] = $"docs{separator}{separator}{tree.Root}";
        var docs = FilterPaths.Absolute("docs", tree.Root);
        var root = FilterPaths.Collapse(tree.Root);

        // Act
        var access = tree.Settings().FileAccessOf(Path.Combine(tree.Guide, "a.scad"));

        // Assert: an empty entry is dropped on Windows and is the working directory elsewhere
        string[] folders = OperatingSystem.IsWindows() ? [docs, root] : [docs, root, root];
        Assert.Equal(["--file-access", "roots", .. folders.SelectMany(f => new[] { "--allow-root", f })], access.Arguments);
        Assert.Equal(folders, access.Roots!);
        Assert.True(access.Restrictive);
    }

    /// <summary>
    ///     Test the errors of the variables, with the Lua filter's messages.
    /// </summary>
    /// <param name="mode">The value of <c>CANVASNET_FILE_ACCESS</c>.</param>
    /// <param name="roots">The value of <c>CANVASNET_ROOTS</c>, or <see langword="null"/>.</param>
    /// <param name="message">The expected message.</param>
    [Theory]
    [InlineData("everything", null, "CANVASNET_FILE_ACCESS is 'everything'; use roots, no-symlinks, roots,no-symlinks, or any")]
    [InlineData("ANY", "docs", "CANVASNET_ROOTS cannot be used with CANVASNET_FILE_ACCESS=any; use roots or roots,no-symlinks")]
    [InlineData("no-symlinks", "docs", "CANVASNET_ROOTS cannot be used with CANVASNET_FILE_ACCESS=no-symlinks; use roots or roots,no-symlinks")]
    public void FilterSettings_FileAccessOf_BadVariables_ThrowTheLuaMessage(string mode, string? roots, string message)
    {
        // Arrange
        using var tree = new FilterTree();
        tree.Environment[FilterSettings.FileAccessVariable] = mode;
        if (roots != null)
        {
            tree.Environment[FilterSettings.RootsVariable] = roots;
        }

        // Act
        var error = Assert.Throws<ToolException>(() => tree.Settings().FileAccessOf("a.scad"));

        // Assert
        Assert.Equal(ErrorCodes.PandocFilterError, error.Code);
        Assert.Equal(message, error.Message);
    }

    /// <summary>
    ///     Test the policy of the dependency walk: none in the default mode, the tool's own policy for the arguments in a
    ///     restrictive mode, and the policy that denies every file when the tool would refuse the arguments.
    /// </summary>
    [Fact]
    public void FilterSettings_PolicyOf_BuildsTheToolsPolicyFromTheArguments()
    {
        // Arrange: the default mode, roots,no-symlinks, no-symlinks, and a folder that does not exist (the variables are
        // read on first use, so each mode's arguments are taken before the next is set)
        using var tree = new FilterTree();
        var source = Path.Combine(tree.Guide, "a.scad");
        var settings = new List<(FilterSettings Settings, FileAccessArguments Access)>();
        foreach (var (mode, roots) in new (string?, string?)[] { (null, null), ("roots,no-symlinks", null), ("no-symlinks", null), ("roots", Path.Combine(tree.Root, "gone")) })
        {
            if (mode != null)
            {
                tree.Environment[FilterSettings.FileAccessVariable] = mode;
            }

            if (roots != null)
            {
                tree.Environment[FilterSettings.RootsVariable] = roots;
            }

            var current = tree.Settings();
            settings.Add((current, current.FileAccessOf(source)));
        }

        // Act
        var policies = settings.Select(s => s.Settings.PolicyOf(s.Access, source)).ToList();

        // Assert
        Assert.Null(policies[0]);
        Assert.Equal("roots+no-symlinks", policies[1]!.Name);
        Assert.Same(DemaConsulting.CanvasNet.Files.FileAccessPolicy.NoSymlinks, policies[2]);
        Assert.Same(DependencyHasher.DenyAll, policies[3]);
    }

    /// <summary>
    ///     Test the tool identity: the version, the label, and for a local build the sorted SHA-1 of the DLLs beside it.
    /// </summary>
    [Fact]
    public void FilterSettings_ToolIdentity_DefaultAndLocalBuild_NameTheTool()
    {
        // Arrange: a local build with two DLLs and another file
        using var tree = new FilterTree();
        using var build = new TempDirectory();
        File.WriteAllText(build.Combine("Tool.dll"), "x");
        File.WriteAllText(build.Combine("Other.DLL"), "y");
        File.WriteAllText(build.Combine("readme.txt"), "z");
        var plain = tree.Settings().ToolIdentity;
        var command = $"dotnet \"{build.Combine("Tool.dll")}\"";
        tree.Environment[FilterSettings.ToolVariable] = command;

        // Act
        var local = tree.Settings().ToolIdentity;

        // Assert: the fingerprint of pandoc.sha1("y") and pandoc.sha1("x"), sorted by name
        Assert.Equal("1.0.0\ndefault\n", plain);
        Assert.Equal(
            $"1.0.0\nCANVASNET_TOOL={command}\nOther.DLL=95cb0bfd2977c761298d9624e4b4d4c72a39974a;Tool.dll=11f6ad8ec52a2984abaafd7c3b516503785c2072",
            local);
    }

    /// <summary>
    ///     Test the split of <c>CANVASNET_TOOL</c> and its errors, with the Lua filter's messages.
    /// </summary>
    [Fact]
    public void FilterSettings_SplitCommand_QuotesGroupWithoutEscapes()
    {
        // Act / Assert
        Assert.Equal(["dotnet", "C:/My Tools/t.dll", "ab"], FilterSettings.SplitCommand("dotnet \"C:/My Tools/t.dll\"  a\"\"b"));
        Assert.Equal("CANVASNET_TOOL has an empty quoted part: x \"\"", Assert.Throws<ToolException>(() => FilterSettings.SplitCommand("x \"\"")).Message);
        Assert.Equal("CANVASNET_TOOL has a quote that is never closed: \"x", Assert.Throws<ToolException>(() => FilterSettings.SplitCommand("\"x")).Message);
    }

    /// <summary>
    ///     Test the cache folder and the <c>src</c> of a picture for each writer: relative to the output file for HTML
    ///     without embedding, the full path with forward slashes otherwise.
    /// </summary>
    [Fact]
    public void FilterSettings_CacheFolderAndPictureSrc_FollowTheWriter()
    {
        // Arrange: the settings of the tree and a picture in its cache
        using var tree = new FilterTree();
        var html = tree.Settings();
        var picture = FilterPaths.Join([html.CacheFolder, "flow-k.png"]);
        var full = picture.Replace('\\', '/');

        // Act / Assert
        Assert.Equal(FilterPaths.Absolute(Path.Combine("docs", "generated", "canvasnet"), tree.Root), html.CacheFolder);
        Assert.Equal("../docs/generated/canvasnet/flow-k.png", html.PictureSrc(picture));
        Assert.Equal("../docs/generated/canvasnet/flow-k.png", WithFormat(html, "revealjs").PictureSrc(picture));
        Assert.Equal(full, WithFormat(html, "latex").PictureSrc(picture));
        Assert.Equal(full, new FilterSettings { FilterPath = html.FilterPath, WorkingDirectory = html.WorkingDirectory, Environment = html.Environment, Format = "html5", OutputFile = "out/doc.html", EmbedResources = true }.PictureSrc(picture));
        Assert.Equal(full, new FilterSettings { FilterPath = html.FilterPath, WorkingDirectory = html.WorkingDirectory, Environment = html.Environment, Format = "html5" }.PictureSrc(picture));
    }

    /// <summary>
    ///     Test the number of jobs: the option, else the variable, else the processors up to four; a bad variable is an
    ///     error.
    /// </summary>
    [Fact]
    public void FilterSettings_ResolveJobs_OptionVariableDefault()
    {
        // Act / Assert
        Assert.Equal(3, FilterSettings.ResolveJobs(3, _ => "8"));
        Assert.Equal(8, FilterSettings.ResolveJobs(null, _ => " 8 "));
        Assert.Equal(Math.Min(Environment.ProcessorCount, 4), FilterSettings.ResolveJobs(null, _ => null));
        var error = Assert.Throws<ToolException>(() => FilterSettings.ResolveJobs(null, _ => "0"));
        Assert.Equal("CANVASNET_JOBS is '0'; write a whole number from 1 up to 64", error.Message);
    }

    /// <summary>
    ///     Copies settings with another format.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="format">The format.</param>
    /// <returns>The copy.</returns>
    private static FilterSettings WithFormat(FilterSettings settings, string format)
    {
        return new FilterSettings
        {
            FilterPath = settings.FilterPath,
            WorkingDirectory = settings.WorkingDirectory,
            Environment = settings.Environment,
            Format = format,
            OutputFile = settings.OutputFile,
            ResourcePaths = settings.ResourcePaths
        };
    }
}
