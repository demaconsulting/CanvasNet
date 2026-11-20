using System.Buffers.Binary;
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
    ///     a regular file. The open runs on the test thread with no time limit: an open that blocked would never return,
    ///     which the time limit of the CI job reports.
    /// </summary>
    [Fact]
    public void RealPath_OpenForRead_Fifo_DoesNotBlockAndIsNotRegular()
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
        using var handle = RealPath.OpenForRead(pipe);
        var info = RealPath.Inspect(handle);

        // Assert
        Assert.False(info.IsRegularFile);
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

    /// <summary>
    ///     Validates that real paths are compared letter for letter on every platform (pure text): a folder that differs
    ///     from the root only in letter case is outside it, while the root itself, a path below it and trailing
    ///     separators behave as before, and a sibling that shares the root's name as a prefix is outside. The written-path
    ///     test keeps the platform rule.
    /// </summary>
    [Fact]
    public void RealPath_IsUnder_ComparesLetterCaseExactly()
    {
        // Arrange: a root and its letter-case variant spelled for this platform
        var windows = OperatingSystem.IsWindows();
        var separator = Path.DirectorySeparatorChar;
        var root = windows ? @"C:\work\allowed" : "/work/allowed";
        var variant = windows ? @"C:\work\ALLOWED" : "/work/ALLOWED";
        var platformIgnoresCase = windows || OperatingSystem.IsMacOS();

        // Act
        var variantInside = RealPath.IsUnder(variant + separator + "x", root);
        var inside = RealPath.IsUnder(root + separator + "x", root);
        var itself = RealPath.IsUnder(root, root);
        var rootWithSeparator = RealPath.IsUnder(root + separator + "x", root + separator);
        var pathWithSeparator = RealPath.IsUnder(root + separator, root);
        var sibling = RealPath.IsUnder(root + "2" + separator + "x", root);
        var equal = RealPath.PathsEqual(variant, root);
        var writtenVariantInside = RealPath.IsUnderAsWritten(variant + separator + "x", root);

        // Assert
        Assert.False(variantInside);
        Assert.True(inside);
        Assert.True(itself);
        Assert.True(rootWithSeparator);
        Assert.True(pathWithSeparator);
        Assert.False(sibling);
        Assert.False(equal);
        Assert.Equal(platformIgnoresCase, writtenVariantInside);
    }

    /// <summary>
    ///     Validates that the deepest shared folder is found by components compared letter for letter (pure text), so a
    ///     folder that differs only in letter case is not shared, while on Windows a drive letter in either case is the
    ///     same root.
    /// </summary>
    [Fact]
    public void RealPath_CommonAncestor_ComparesComponentsExactly()
    {
        // Arrange
        var windows = OperatingSystem.IsWindows();
        var separator = Path.DirectorySeparatorChar;
        var parent = windows ? @"C:\work" : "/work";

        // Act
        var shared = RealPath.CommonAncestor(parent + separator + "ALLOWED" + separator + "x", parent + separator + "allowed");
        var same = RealPath.CommonAncestor(parent + separator + "allowed" + separator + "x", parent + separator + "allowed");

        // Assert
        Assert.Equal(parent, shared);
        Assert.Equal(parent + separator + "allowed", same);
        if (windows)
        {
            Assert.Equal(@"c:\work\allowed", RealPath.CommonAncestor(@"c:\work\allowed\x", @"C:\work\allowed"));
        }
    }

    /// <summary>
    ///     Validates in a folder that ignores letter case that a folder and a file written in another letter case are
    ///     given in the case stored on disk (the single entry that matches ignoring case, which is the same entry as the
    ///     name as written). Skips where the temporary folder compares letter case.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_FolderAndFileInAnotherCase_GiveTheStoredCase()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        directory.Write("Docs/Readme.txt", "r");
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));

        // Act
        var exact = RealPath.GetExactPathName(Path.Join(folder, "docs", "README.txt"));

        // Assert
        Assert.Equal(Path.Join(folder, "Docs", "Readme.txt"), exact);
    }

    /// <summary>
    ///     Validates that the part of a path below a folder that does not exist is kept as written, on every platform.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_MissingTail_IsKeptAsWritten()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("Docs"));
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));

        // Act
        var exact = RealPath.GetExactPathName(Path.Join(folder, "Docs", "Missing", "Sub.txt"));

        // Assert
        Assert.Equal(Path.Join(folder, "Docs", "Missing", "Sub.txt"), exact);
    }

    /// <summary>
    ///     Validates in a folder that compares letter case, holding only <c>target.txt</c>, that <c>TARGET.txt</c> is
    ///     kept as written: it names no entry there, so mapping it to the case variant would let a link created later
    ///     under the written name pass as the stored entry. Skips where the temporary folder ignores letter case.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_CaseVariantOfAnEntryInACaseSensitiveFolder_KeepsTheNameAsWritten()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        FileTestSupport.RequireCaseSensitive(folder);
        File.WriteAllText(Path.Join(folder, "target.txt"), "target");

        // Act
        var exact = RealPath.GetExactPathName(Path.Join(folder, "TARGET.txt"));

        // Assert
        Assert.Equal(Path.Join(folder, "TARGET.txt"), exact);
    }

    /// <summary>
    ///     Validates the identity check of two directory entries on every platform: an entry is the same entry as
    ///     itself, another file is not, and a name that does not exist is unknown.
    /// </summary>
    [Fact]
    public void RealPath_IsSameEntry_SameEntryOtherFileAndMissingName_AreTold()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        File.WriteAllText(Path.Join(folder, "a.txt"), "a");
        File.WriteAllText(Path.Join(folder, "b.txt"), "b");

        // Act
        var itself = RealPath.IsSameEntry(Path.Join(folder, "a.txt"), Path.Join(folder, "a.txt"));
        var other = RealPath.IsSameEntry(Path.Join(folder, "a.txt"), Path.Join(folder, "b.txt"));
        var missing = RealPath.IsSameEntry(Path.Join(folder, "missing.txt"), Path.Join(folder, "a.txt"));

        // Assert
        Assert.True(itself);
        Assert.False(other);
        Assert.Null(missing);
    }

    /// <summary>
    ///     Validates in a folder that ignores letter case that a name written in another letter case is the same entry
    ///     as the stored one. Skips where the temporary folder compares letter case.
    /// </summary>
    [Fact]
    public void RealPath_IsSameEntry_CaseVariantInACaseInsensitiveFolder_IsTheSameEntry()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        File.WriteAllText(Path.Join(folder, "target.txt"), "target");

        // Act
        var same = RealPath.IsSameEntry(Path.Join(folder, "TARGET.txt"), Path.Join(folder, "target.txt"));

        // Assert
        Assert.True(same);
    }

    /// <summary>
    ///     Validates that a symbolic link to an entry is not the same entry: the link at the last component is opened
    ///     itself (Linux, Windows) or refuses the open (macOS), never followed. File links need a privilege on Windows,
    ///     so the test skips there when the host cannot create one.
    /// </summary>
    [Fact]
    public void RealPath_IsSameEntry_LinkToAnEntry_IsNotTheSameEntryOrCapabilitySkipped()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        var target = Path.Join(folder, "target.txt");
        File.WriteAllText(target, "target");
        var link = Path.Join(folder, "link.txt");
        if (!SandboxLinkSupport.TryCreateFileLink(link, target, out var reason))
        {
            Assert.Skip("File symbolic links need Developer Mode or SeCreateSymbolicLinkPrivilege on Windows: " + reason);
        }

        // Act
        var same = RealPath.IsSameEntry(link, target);

        // Assert
        Assert.NotEqual(true, same);
    }

    /// <summary>
    ///     Validates in a folder that compares letter case, holding <c>Docs</c> and <c>docs</c>, that the name spelled
    ///     exactly wins and a name that matches both only ignoring case is kept as written. Skips where the temporary
    ///     folder ignores letter case.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_BothCaseVariantsExist_PrefersTheExactNameAndKeepsAnAmbiguousOne()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        FileTestSupport.RequireCaseSensitive(folder);
        Directory.CreateDirectory(Path.Join(folder, "Docs"));
        Directory.CreateDirectory(Path.Join(folder, "docs"));

        // Act
        var exactName = RealPath.GetExactPathName(Path.Join(folder, "docs"));
        var ambiguous = RealPath.GetExactPathName(Path.Join(folder, "DOCS"));

        // Assert
        Assert.Equal(Path.Join(folder, "docs"), exactName);
        Assert.Equal(Path.Join(folder, "DOCS"), ambiguous);
    }

    /// <summary>
    ///     Validates on macOS (a folder that ignores letter case, where a name may hold <c>*</c> and <c>?</c>) that such
    ///     characters match only themselves: <c>A*B</c> gives the stored <c>a*b</c>, never <c>aXb</c>, and <c>X?</c>
    ///     gives <c>x?</c>, never <c>xy</c>. Skips where the temporary folder compares letter case, where a name in
    ///     another letter case is kept as written whatever it matches.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_WildcardCharactersInAName_MatchOnlyThatName()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        var folder = RealPath.GetExactPathName(Path.GetFullPath(directory.Path));
        directory.Write("a*b", "1");
        directory.Write("aXb", "2");
        directory.Write("x?", "3");
        directory.Write("xy", "4");

        // Act
        var star = RealPath.GetExactPathName(Path.Join(folder, "A*B"));
        var question = RealPath.GetExactPathName(Path.Join(folder, "X?"));

        // Assert
        Assert.Equal(Path.Join(folder, "a*b"), star);
        Assert.Equal(Path.Join(folder, "x?"), question);
    }

    /// <summary>
    ///     Validates on Windows that a drive letter is given in upper case, as Windows stores and reports it.
    /// </summary>
    [Fact]
    public void RealPath_GetExactPathName_DriveLetter_IsUpperCase()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        var full = Path.GetFullPath(directory.Path);
        var lower = char.ToLowerInvariant(full[0]) + full[1..];

        // Act
        var exact = RealPath.GetExactPathName(lower);

        // Assert
        Assert.Equal(char.ToUpperInvariant(full[0]), exact[0]);
        Assert.Equal(RealPath.GetExactPathName(full), exact);
    }

    /// <summary>
    ///     Validates the root rule (pure text, so it runs on every system): a drive letter is upper-cased, also after the
    ///     extended-length prefix, while a UNC root, a volume GUID root and <c>/</c> are kept as they are.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="expected">The root in its on-disk spelling.</param>
    [Theory]
    [InlineData(@"c:\", @"C:\")]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"\\?\c:\", @"\\?\C:\")]
    [InlineData(@"\\server\share", @"\\server\share")]
    [InlineData(@"\\?\Volume{67589fd6-ee90-4e7d-9ad7-a17168a66d75}\", @"\\?\Volume{67589fd6-ee90-4e7d-9ad7-a17168a66d75}\")]
    [InlineData("/", "/")]
    public void RealPath_ExactRoot_DriveRootsUpperCasedAndOtherRootsKept(string root, string expected)
    {
        // Act
        var exact = RealPath.ExactRoot(root);

        // Assert
        Assert.Equal(expected, exact);
    }

    /// <summary>
    ///     Validates in a folder that ignores letter case that the real location of a folder written in another case is
    ///     given in the case stored on disk. Skips where the temporary folder compares letter case.
    /// </summary>
    [Fact]
    public void RealPath_TryGetExactCanonicalDirectory_FolderWrittenInAnotherCase_GivesTheStoredCase()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        Directory.CreateDirectory(directory.Combine("Stored"));

        // Act
        var canonical = RealPath.TryGetExactCanonicalDirectory(Path.GetFullPath(directory.Combine("STORED")));

        // Assert
        Assert.NotNull(canonical);
        Assert.EndsWith(Path.DirectorySeparatorChar + "Stored", canonical, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a spelling naming another file is not confirmed for an open handle, and that the reported real
    ///     path itself needs no confirmation.
    /// </summary>
    [Fact]
    public void RealPath_TryConfirmExactPath_SpellingOfAnotherFile_IsUnconfirmed()
    {
        // Arrange: the other file is spelled through the real folder of the open file, so the spelling has no link in it
        // (on macOS the temporary folder /var/folders is reached through the /var link, which a confirmation refuses)
        using var directory = new SandboxTempDirectory();
        var path = Path.GetFullPath(directory.Write("a.txt", "a"));
        directory.Write("b.txt", "b");
        using var handle = RealPath.OpenForRead(path);
        var realPath = RealPath.Inspect(handle).FinalPath!;
        var other = Path.Join(Path.GetDirectoryName(realPath), "b.txt");

        // Act
        var otherFile = RealPath.TryConfirmExactPath(handle, realPath, other);
        var itself = RealPath.TryConfirmExactPath(handle, realPath, realPath);
        var same = RealPath.IsSameFile(handle, other, out var reason);

        // Assert
        Assert.Null(otherFile);
        Assert.Same(realPath, itself);
        Assert.True(same is not null, $"The identity of the other file is unknown: {reason}");
        Assert.False(same);
    }

    /// <summary>
    ///     Validates in a folder that ignores letter case that, for a real path reported in another letter case, its
    ///     on-disk spelling is confirmed by identity to name the open file. Skips where the temporary folder compares
    ///     letter case.
    /// </summary>
    [Fact]
    public void RealPath_TryConfirmExactPath_StoredSpellingOfARealPathInAnotherCase_IsConfirmed()
    {
        // Arrange: the reported real path is upper-cased, as a file system that reports the spelling used to open would
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        var path = Path.GetFullPath(directory.Write("a.txt", "a"));
        using var handle = RealPath.OpenForRead(path);
        var reported = RealPath.Inspect(handle).FinalPath!.ToUpperInvariant();
        var exact = RealPath.GetExactPathName(reported);

        // Act
        var confirmed = RealPath.TryConfirmExactPath(handle, reported, exact, out var reason);

        // Assert
        Assert.NotEqual(reported, exact);
        Assert.True(confirmed is not null, $"The stored spelling is not confirmed: {reason}");
        Assert.Equal(exact, confirmed);
        Assert.EndsWith(Path.DirectorySeparatorChar + "a.txt", confirmed, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a spelling that reaches the open file through a linked folder is not confirmed: the spelling
    ///     must name the file through no link, so a link in a folder of the spelling cannot make a path elsewhere pass as
    ///     the real path of the file.
    /// </summary>
    [Fact]
    public void RealPath_TryConfirmExactPath_SpellingThroughALinkedFolder_IsUnconfirmed()
    {
        // Arrange: target/a.txt is open; link is a directory link (a junction on Windows) to target
        using var directory = new SandboxTempDirectory();
        var path = Path.GetFullPath(directory.Write("target/a.txt", "a"));
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("link"), directory.Combine("target"));
        using var handle = RealPath.OpenForRead(path);
        var realPath = RealPath.Inspect(handle).FinalPath!;
        var throughLink = Path.Join(Path.GetDirectoryName(Path.GetDirectoryName(realPath)), "link", "a.txt");

        // Act
        var confirmed = RealPath.TryConfirmExactPath(handle, realPath, throughLink);

        // Assert
        Assert.Null(confirmed);
    }

    /// <summary>
    ///     Validates on Linux and macOS that a symbolic link to the open file is not confirmed as its spelling: the link
    ///     is opened itself, never followed.
    /// </summary>
    [Fact]
    public void RealPath_TryConfirmExactPath_LinkToTheOpenedFile_IsUnconfirmed()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        var path = Path.GetFullPath(directory.Write("a.txt", "a"));
        var link = Path.GetFullPath(directory.Combine("link.txt"));
        File.CreateSymbolicLink(link, path);
        using var handle = RealPath.OpenForRead(path);
        var realPath = RealPath.Inspect(handle).FinalPath!;

        // Act
        var confirmed = RealPath.TryConfirmExactPath(handle, realPath, link);

        // Assert
        Assert.Null(confirmed);
    }

    /// <summary>
    ///     Validates on Windows that a file symbolic link to the open file is not confirmed as its spelling: the link is
    ///     opened itself, so its identity (read through <c>FileIdInfo</c> where the volume reports it) is that of
    ///     another file. File links need a privilege on Windows, so the test skips when the host cannot create one.
    /// </summary>
    [Fact]
    public void RealPath_TryConfirmExactPath_WindowsFileSymbolicLink_IsUnconfirmedOrCapabilitySkipped()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        var path = Path.GetFullPath(directory.Write("a.txt", "a"));
        if (!SandboxLinkSupport.TryCreateFileLink(directory.Combine("link.txt"), path, out var linkFailure))
        {
            Assert.Skip("File symbolic links need Developer Mode or SeCreateSymbolicLinkPrivilege on Windows: " + linkFailure);
        }

        using var handle = RealPath.OpenForRead(path);
        var realPath = RealPath.Inspect(handle).FinalPath!;
        var link = Path.Join(Path.GetDirectoryName(realPath), "link.txt");

        // Act
        var confirmed = RealPath.TryConfirmExactPath(handle, realPath, link);
        var same = RealPath.IsSameFile(handle, link, out var reason);

        // Assert
        Assert.Null(confirmed);
        Assert.True(same is not null, $"The identity of the link is unknown: {reason}");
        Assert.False(same);
        Assert.Contains("another file", reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the decoding of <c>FILE_ID_INFO</c> (pure bytes, so it runs on every system): two files on one
    ///     volume whose 128-bit identifiers differ only in their high 64 bits have different identities, as on ReFS, where
    ///     the low 64 bits alone do not name one file.
    /// </summary>
    [Fact]
    public void RealPath_WindowsFileId_IdsDifferingOnlyInTheHigh64Bits_AreDifferent()
    {
        // Arrange: the same 64-bit volume serial number and the same low 64 bits of the identifier
        var first = new byte[WindowsFileId.FileIdInfoSize];
        var second = new byte[WindowsFileId.FileIdInfoSize];
        BinaryPrimitives.WriteUInt64LittleEndian(first.AsSpan(0), 0x1122334455667788UL);
        BinaryPrimitives.WriteUInt64LittleEndian(second.AsSpan(0), 0x1122334455667788UL);
        BinaryPrimitives.WriteUInt64LittleEndian(first.AsSpan(8), 0x0000000000000042UL);
        BinaryPrimitives.WriteUInt64LittleEndian(second.AsSpan(8), 0x0000000000000042UL);
        BinaryPrimitives.WriteUInt64LittleEndian(first.AsSpan(16), 0x0000000000000001UL);
        BinaryPrimitives.WriteUInt64LittleEndian(second.AsSpan(16), 0x0000000000000002UL);

        // Act
        var firstId = WindowsFileId.FromFileIdInfo(first);
        var secondId = WindowsFileId.FromFileIdInfo(second);

        // Assert
        Assert.Equal(0x1122334455667788UL, firstId.Volume);
        Assert.Equal(new UInt128(1, 0x42), firstId.Id);
        Assert.True(firstId.Extended);
        Assert.NotEqual(firstId, secondId);
    }

    /// <summary>
    ///     Validates the decoding of <c>BY_HANDLE_FILE_INFORMATION</c> (pure bytes, so it runs on every system): the
    ///     32-bit volume serial number at offset 28 and the 64-bit file index from its high (offset 44) and low (offset
    ///     48) halves.
    /// </summary>
    [Fact]
    public void RealPath_WindowsFileId_ByHandleInformation_DecodesSerialAndIndex()
    {
        // Arrange
        var information = new byte[WindowsFileId.ByHandleInformationSize];
        BinaryPrimitives.WriteUInt32LittleEndian(information.AsSpan(28), 0xCAFEF00DU);
        BinaryPrimitives.WriteUInt32LittleEndian(information.AsSpan(44), 0x00000007U);
        BinaryPrimitives.WriteUInt32LittleEndian(information.AsSpan(48), 0x89ABCDEFU);

        // Act
        var id = WindowsFileId.FromByHandleInformation(information);

        // Assert
        Assert.Equal(0xCAFEF00DUL, id.Volume);
        Assert.Equal((UInt128)0x0000000789ABCDEFUL, id.Id);
        Assert.False(id.Extended);
    }
}
