using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for the real-path unit (<see cref="RealPath"/> and its native calls): the final path and the type of
///     an open handle on each operating system, the non-blocking open, the real location of a folder, and the path
///     comparison rules. Each operating-system scenario runs on its own system and skips on the others; the CI matrix
///     runs Windows, Linux and macOS.
/// </summary>
public class RealPathTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that a plain file opened for reading is a regular file whose final path is its full path in the
    ///     real location of its folder.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_PlainFile_ReportsItsCanonicalPathAndARegularFile()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var path = directory.Write("a.txt", "a");

        // Act
        using var handle = RealPath.OpenForRead(path);
        var facts = RealPath.Inspect(handle);

        // Assert
        Assert.True(facts.IsRegularFile);
        Assert.NotNull(facts.FinalPath);
        Assert.True(Path.IsPathFullyQualified(facts.FinalPath));
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Path), "a.txt"), facts.FinalPath));
    }

    /// <summary>
    ///     Validates that a file opened through a directory link reports the path of the link's target.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_FileThroughADirectoryLink_ReportsTheTarget()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("target/a.txt", "a");
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("link"), directory.Combine("target"));

        // Act
        using var handle = RealPath.OpenForRead(Path.GetFullPath(directory.Combine("link/a.txt")));
        var facts = RealPath.Inspect(handle);

        // Assert
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Combine("target")), "a.txt"), facts.FinalPath!));
    }

    /// <summary>
    ///     Validates on Windows that a file opened by its short (8.3) spelling reports its long spelling, and that short
    ///     names are expanded. Skips where the volume does not create short names.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_ShortNameSpelling_IsExpanded()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        var longFile = directory.Write("a-long-folder-name/a.txt", "a");
        var shortFolder = FileTestSupport.TryGetShortPath(Path.GetDirectoryName(longFile)!);
        if (shortFolder is null || !shortFolder.Contains('~', StringComparison.Ordinal))
        {
            Assert.Skip("The volume does not create short (8.3) names.");
        }

        // Act
        using var handle = RealPath.OpenForRead(Path.Combine(shortFolder, "a.txt"));
        var facts = RealPath.Inspect(handle);
        var expanded = RealPath.ExpandShortNames(Path.Combine(shortFolder, "a.txt"));

        // Assert
        Assert.Contains("a-long-folder-name", facts.FinalPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a-long-folder-name", expanded, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates on macOS that the final path comes back through <c>proc_pidfdinfo</c> in the real location of the
    ///     temporary folder (<c>/var</c> and <c>/tmp</c> are links to <c>/private</c>), which pins the offset of the path
    ///     in the kernel's structure.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_TempFileOnMacOS_ReportsThePrivateLocation()
    {
        // Arrange
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Skip("This scenario exists only on macOS.");
        }

        using var directory = new SandboxTempDirectory();
        var path = Path.GetFullPath(directory.Write("a.txt", "a"));
        var linked = path.StartsWith("/var/", StringComparison.Ordinal) || path.StartsWith("/tmp/", StringComparison.Ordinal);
        var expected = linked ? "/private" + path : path;

        // Act
        using var handle = RealPath.OpenForRead(path);
        var facts = RealPath.Inspect(handle);

        // Assert
        Assert.True(facts.IsRegularFile);
        Assert.True(RealPath.PathsEqual(expected, facts.FinalPath!), $"expected '{expected}', got '{facts.FinalPath}'");
    }

    /// <summary>
    ///     Validates on Linux that a file deleted after it was opened has no real path any more (fail closed for a
    ///     restrictive policy), while it is still a regular file.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_DeletedFile_IsUnavailable()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("This scenario exists only on Linux.");
        }

        using var directory = new SandboxTempDirectory();
        var path = directory.Write("a.txt", "a");
        using var handle = RealPath.OpenForRead(path);
        File.Delete(path);

        // Act
        var facts = RealPath.Inspect(handle);

        // Assert
        Assert.True(facts.IsRegularFile);
        Assert.Null(facts.FinalPath);
    }

    /// <summary>
    ///     Validates that a live file whose name ends with the text Linux appends to a deleted file's link still reports
    ///     its real path, suffix included: deletion is told from the open file, not from its name.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_LiveFileNamedLikeADeletedOne_ReportsItsRealPath()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var path = directory.Write("x (deleted)", "a");

        // Act
        using var handle = RealPath.OpenForRead(Path.GetFullPath(path));
        var facts = RealPath.Inspect(handle);

        // Assert
        Assert.True(facts.IsRegularFile);
        Assert.NotNull(facts.FinalPath);
        Assert.True(
            RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Path), "x (deleted)"), facts.FinalPath),
            $"got '{facts.FinalPath}'");
    }

    /// <summary>
    ///     Validates that a device and a directory are not regular files: on Linux and macOS both open without blocking
    ///     and are reported as such; on Windows the null device is a character device and a directory cannot be opened as
    ///     a file at all.
    /// </summary>
    [Fact]
    public void RealPath_Inspect_DirectoryAndDevice_AreNotRegularFiles()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();

        // Act / Assert
        if (OperatingSystem.IsWindows())
        {
            using var device = FileTestSupport.OpenNullDevice();
            Assert.False(RealPath.Inspect(device).IsRegularFile);
            Assert.Throws<UnauthorizedAccessException>(() => RealPath.OpenForRead(directory.Path));
        }
        else
        {
            using var device = RealPath.OpenForRead("/dev/null");
            using var folder = RealPath.OpenForRead(directory.Path);
            Assert.False(RealPath.Inspect(device).IsRegularFile);
            Assert.False(RealPath.Inspect(folder).IsRegularFile);
        }
    }

    /// <summary>
    ///     Validates on Linux and macOS that opening a named pipe without a writer returns at once and the handle is not
    ///     a regular file. The only wait is a guard that fails the test if the open hangs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RealPath_OpenForRead_Fifo_DoesNotBlockAndIsNotRegular()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        var pipe = directory.Combine("pipe");
        var failure = FileTestSupport.CreateFifo(pipe);
        Assert.True(failure.Length == 0, failure);

        // Act
        var open = Task.Run(
            () =>
            {
                using var handle = RealPath.OpenForRead(pipe);
                return RealPath.Inspect(handle);
            },
            Ct);
        var first = await Task.WhenAny(open, Task.Delay(TimeSpan.FromMinutes(1), Ct));

        // Assert
        Assert.Same(open, first);
        Assert.False((await open).IsRegularFile);
    }

    /// <summary>
    ///     Validates that a missing file and a missing folder fail with the "not found" exceptions a caller treats as a
    ///     missing candidate.
    /// </summary>
    [Fact]
    public void RealPath_OpenForRead_MissingFileAndMissingFolder_MapToNotFoundExceptions()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("file.txt", "f");

        // Act
        var missingFile = Record.Exception(() => RealPath.OpenForRead(Path.GetFullPath(directory.Combine("none.txt"))));
        var missingFolder = Record.Exception(() => RealPath.OpenForRead(Path.GetFullPath(directory.Combine("none/a.txt"))));
        var fileAsFolder = Record.Exception(() => RealPath.OpenForRead(Path.GetFullPath(directory.Combine("file.txt/a.txt"))));

        // Assert
        Assert.IsType<FileNotFoundException>(missingFile);
        Assert.True(missingFolder is FileNotFoundException or DirectoryNotFoundException, missingFolder?.GetType().Name);
        Assert.True(fileAsFolder is FileNotFoundException or DirectoryNotFoundException, fileAsFolder?.GetType().Name);
    }

    /// <summary>
    ///     Validates that the real location of a linked folder is its target, and that a missing folder has none.
    /// </summary>
    [Fact]
    public void RealPath_TryGetCanonicalDirectory_LinkedDirectory_ReturnsTheTarget()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("target"));
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("link"), directory.Combine("target"));

        // Act
        var viaLink = RealPath.TryGetCanonicalDirectory(Path.GetFullPath(directory.Combine("link")));
        var direct = RealPath.TryGetCanonicalDirectory(Path.GetFullPath(directory.Combine("target")));
        var missing = RealPath.TryGetCanonicalDirectory(Path.GetFullPath(directory.Combine("missing")));

        // Assert
        Assert.NotNull(viaLink);
        Assert.True(RealPath.PathsEqual(direct!, viaLink));
        Assert.EndsWith("target", viaLink, StringComparison.OrdinalIgnoreCase);
        Assert.Null(missing);
    }

    /// <summary>
    ///     Validates on Linux and macOS that a folder the process may search but not read (execute-only) still has a
    ///     real location, the same one it has while it is readable. Skips when permissions are not enforced (root).
    /// </summary>
    [Fact]
    public void RealPath_TryGetCanonicalDirectory_ExecuteOnlyDirectory_ReturnsItsLocation()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        var folder = Path.GetFullPath(directory.Combine("search-only"));
        Directory.CreateDirectory(folder);
        var readable = FileTestSupport.Canonical(folder);
        try
        {
            if (!FileTestSupport.TryMakeExecuteOnly(folder))
            {
                Assert.Skip(FileTestSupport.PermissionsNotEnforced);
            }

            // Act
            var canonical = RealPath.TryGetCanonicalDirectory(folder);

            // Assert
            Assert.NotNull(canonical);
            Assert.True(RealPath.PathsEqual(readable, canonical), $"expected '{readable}', got '{canonical}'");
        }
        finally
        {
            FileTestSupport.RestoreOwnerAccess(folder);
        }
    }

    /// <summary>
    ///     Validates on Windows that a UNC path is a network location, while a local path and the device namespaces are
    ///     not. A mapped network drive cannot be created on CI and is not covered here.
    /// </summary>
    [Fact]
    public void RealPath_IsNetworkLocation_UncAndLocalPaths_AreClassified()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        var local = Path.GetFullPath(directory.Write("a.txt", "a"));

        // Act / Assert
        Assert.True(RealPath.IsNetworkLocation(@"\\server\share\x"));
        Assert.False(RealPath.IsNetworkLocation(local));
        Assert.False(RealPath.IsNetworkLocation(@"\\?\C:\x"));
        Assert.True(RealPath.IsNetworkLocation(@"\\?\UNC\server\share\x"));
        Assert.False(RealPath.IsNetworkLocation(@"\\?\GLOBALROOT\Device\HarddiskVolume1\x"));
        Assert.False(RealPath.IsNetworkLocation(@"\\.\NUL"));
    }

    /// <summary>
    ///     Validates the comparison rules: a folder contains itself and what lies below its separator but not a sibling
    ///     that shares its name as a prefix, trailing separators do not matter, and the deepest shared folder is found by
    ///     whole components.
    /// </summary>
    [Fact]
    public void RealPath_IsUnder_BoundaryIsTheSeparator()
    {
        // Arrange
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "real-path-root"));
        var separator = Path.DirectorySeparatorChar;
        var systemRoot = Path.GetPathRoot(root)!;

        // Act / Assert
        Assert.True(RealPath.IsUnder(root, root));
        Assert.True(RealPath.IsUnder(root + separator + "a.txt", root));
        Assert.True(RealPath.IsUnder(root + separator + "a.txt", root + separator));
        Assert.False(RealPath.IsUnder(root + "-sibling" + separator + "a.txt", root));
        Assert.True(RealPath.IsUnder(root, systemRoot));
        Assert.True(RealPath.PathsEqual(root + separator, root));
        Assert.Equal(root, RealPath.CommonAncestor(root + separator + "a" + separator + "x.txt", root + separator + "b"));
        Assert.Equal(Path.GetDirectoryName(root), RealPath.CommonAncestor(root + separator + "a.txt", root + "-sibling" + separator + "b"));
    }

    /// <summary>
    ///     Validates the classification of extended-length and device-namespace paths (pure text, so it runs on every
    ///     system): only <c>\\?\</c> followed by a drive or a volume GUID is an ordinary extended-length path; every
    ///     device-namespace spelling is recognized as one.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="ordinary">Whether it is an ordinary extended-length path.</param>
    /// <param name="deviceNamespace">Whether it is in a device namespace.</param>
    [Theory]
    [InlineData(@"\\?\C:\docs", true, true)]
    [InlineData(@"\\?\c:\", true, true)]
    [InlineData(@"\\?\Z:", true, true)]
    [InlineData(@"\\?\Volume{67589fd6-ee90-4e7d-9ad7-a17168a66d75}\docs", true, true)]
    [InlineData(@"\\?\volume{67589FD6-EE90-4E7D-9AD7-A17168A66D75}", true, true)]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume1\docs", false, true)]
    [InlineData(@"\\?\UNC\server\share", false, true)]
    [InlineData(@"\\?\pipe\x", false, true)]
    [InlineData(@"\\?\Volume{not-a-guid}\docs", false, true)]
    [InlineData(@"\\?\Volume{67589fd6-ee90-4e7d-9ad7-a17168a66d75}x", false, true)]
    [InlineData(@"\\?\C:docs", false, true)]
    [InlineData(@"\\.\C:\docs", false, true)]
    [InlineData(@"\??\C:\docs", false, true)]
    [InlineData("//?/C:/docs", false, true)]
    [InlineData(@"C:\docs", false, false)]
    [InlineData(@"\\server\share", false, false)]
    [InlineData("/home/docs", false, false)]
    public void RealPath_IsOrdinaryExtendedPath_AcceptsOnlyDriveAndVolumeRoots(string path, bool ordinary, bool deviceNamespace)
    {
        // Act
        var isOrdinary = RealPath.IsOrdinaryExtendedPath(path);
        var isDevice = RealPath.IsWindowsDeviceNamespace(path);

        // Assert
        Assert.Equal(ordinary, isOrdinary);
        Assert.Equal(deviceNamespace, isDevice);
    }

    /// <summary>
    ///     Validates that the reserved Windows device names are recognized in every spelling Windows accepts, in a single
    ///     component and anywhere in a path, and that names that merely resemble them are not. The check is pure text, so
    ///     it runs on every system.
    /// </summary>
    /// <param name="component">The path component.</param>
    /// <param name="reserved">Whether it is a reserved device name.</param>
    [Theory]
    [InlineData("CON", true)]
    [InlineData("prn", true)]
    [InlineData("Aux", true)]
    [InlineData("NUL", true)]
    [InlineData("NUL.txt", true)]
    [InlineData("NUL ", true)]
    [InlineData("NUL...", true)]
    [InlineData("NUL .txt", true)]
    [InlineData("NUL:", true)]
    [InlineData("NUL:stream", true)]
    [InlineData("COM0", true)]
    [InlineData("com9", true)]
    [InlineData("LPT1.log", true)]
    [InlineData("COM\u00B9", true)]
    [InlineData("LPT\u00B2", true)]
    [InlineData("lpt\u00B3", true)]
    [InlineData("CONIN$", true)]
    [InlineData("conout$.x", true)]
    [InlineData("NULL", false)]
    [InlineData("CONSOLE", false)]
    [InlineData("COM10", false)]
    [InlineData("COM", false)]
    [InlineData("LPT", false)]
    [InlineData("a.NUL", false)]
    [InlineData(" NUL", false)]
    [InlineData("nul_x", false)]
    [InlineData("CONIN", false)]
    public void RealPath_IsWindowsDeviceName_RecognizesEveryReservedSpelling(string component, bool reserved)
    {
        // Act
        var single = RealPath.IsWindowsDeviceName(component);
        var inPath = RealPath.HasWindowsDeviceName("C:/docs/" + component + "/a.txt");

        // Assert
        Assert.Equal(reserved, single);
        Assert.Equal(reserved, inPath);
    }
}
