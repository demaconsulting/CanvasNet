using System.Text;
using DemaConsulting.CanvasNet.Files;
using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for <see cref="FileReferenceResolver"/>: the search order, the text rules that hold under every
///     policy, missing and non-regular files, the open and bounded-read rules, the file limit, the cache, the
///     resolver-callback mode, cancellation, and how the policy is asked and how its answer is reported. Links have
///     their own class (<see cref="FileReferenceResolverLinkTests"/>), and so does the race-free check
///     (<see cref="FileReferenceResolverRaceTests"/>). Open failures are provoked through the internal open seams, so no
///     test needs a file permission or a lock.
/// </summary>
public class FileReferenceResolverTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates a resolver.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="hooks">The open seams, or <see langword="null"/>.</param>
    /// <returns>The resolver.</returns>
    private static FileReferenceResolver Create(FileReferenceOptions options, FileOpenHooks? hooks = null) =>
        new(options, Ct, hooks);

    /// <summary>
    ///     Creates seams that record every full path that is about to be opened.
    /// </summary>
    /// <param name="opened">The list that receives the paths.</param>
    /// <returns>The seams.</returns>
    private static FileOpenHooks Recording(List<string> opened) => new() { BeforeOpen = opened.Add };

    /// <summary>
    ///     Opens a file the way the default opener does, for seams that delegate to the file system.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns>The handle.</returns>
    private static SafeFileHandle OpenShared(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>
    ///     Decodes the content of a resolved file as UTF-8.
    /// </summary>
    /// <param name="file">The file; must not be null.</param>
    /// <returns>The text.</returns>
    private static string Text(FileReference? file)
    {
        Assert.NotNull(file);
        return Encoding.UTF8.GetString(file.Content.Span);
    }

    /// <summary>
    ///     Validates that a relative reference is found next to the referring file first, otherwise in the base
    ///     directory, and then in each search path in order, and that the result carries its key and directory.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_RelativePathUsesReferrerThenBaseThenSearchPaths()
    {
        // Arrange: the same name in the base directory and the first search path, others only further along
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("base/sub/main.txt", "main");
        directory.Write("base/sub/near.txt", "near");
        directory.Write("one/a.txt", "from one");
        directory.Write("one/b.txt", "b from one");
        directory.Write("two/sub/c.txt", "c from two");
        var resolver = Create(new FileReferenceOptions
        {
            BaseDirectory = directory.Combine("base"),
            SearchPaths = [directory.Combine("one"), directory.Combine("two")],
        });

        // Act
        var a = resolver.Resolve("a.txt");
        var b = resolver.Resolve("b.txt");
        var c = resolver.Resolve("sub\\c.txt");
        var main = resolver.Resolve("sub/main.txt");
        var near = resolver.Resolve("near.txt", main);
        var far = resolver.Resolve("b.txt", main);

        // Assert: the first place that has the file wins, and a referring file's directory comes first
        Assert.Equal("from base", Text(a));
        Assert.Equal("b from one", Text(b));
        Assert.Equal("c from two", Text(c));
        Assert.Equal(Path.GetFullPath(directory.Combine("two/sub/c.txt")), c!.Key);
        Assert.Equal(Path.GetFullPath(directory.Combine("two/sub")), c.Directory);
        Assert.Equal("near", Text(near));
        Assert.Equal("b from one", Text(far));
        Assert.True(resolver.HasSource);
    }

    /// <summary>
    ///     Validates that a <c>..</c> that leaves the base directory is read under the default policy, like Pandoc.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_DotDotLeavingTheBase_IsReadUnderAllowAll()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        directory.Write("outside/secret.txt", "outside");
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Combine("base") });

        // Act
        var file = resolver.Resolve("../outside/secret.txt");

        // Assert
        Assert.Equal("outside", Text(file));
        Assert.Equal(Path.GetFullPath(directory.Combine("outside/secret.txt")), file!.Key);
    }

    /// <summary>
    ///     Validates that an absolute reference names exactly one candidate: it is read where it points, and a missing
    ///     absolute file is not looked up on the search paths.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_AbsolutePath_IsASingleCandidateAndIsRead()
    {
        // Arrange: the search path has a file of the same name as the missing absolute one
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        directory.Write("elsewhere/abs.txt", "abs");
        directory.Write("lib/missing.txt", "must not be found");
        var opened = new List<string>();
        var resolver = Create(
            new FileReferenceOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("lib")] },
            Recording(opened));
        var absolute = Path.GetFullPath(directory.Combine("elsewhere/abs.txt")).Replace('\\', '/');
        var missing = Path.GetFullPath(directory.Combine("elsewhere/missing.txt"));

        // Act
        var file = resolver.Resolve(absolute);
        var none = resolver.Resolve(missing);

        // Assert: the absolute file is read; the missing one is missing after one attempt only
        Assert.Equal("abs", Text(file));
        Assert.Null(none);
        Assert.Equal([Path.GetFullPath(absolute), missing], opened);
    }

    /// <summary>
    ///     Validates that backslashes and <c>.</c> and <c>..</c> segments are normalized, so every spelling names one file
    ///     (the very same instance).
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_BackslashesAndDotSegments_NameOneFile()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/sub/a.txt", "a");
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Combine("base") });

        // Act
        var plain = resolver.Resolve("sub/a.txt");
        var dotted = resolver.Resolve("sub/../sub/./a.txt");
        var backslashes = resolver.Resolve("sub\\a.txt");

        // Assert
        Assert.Same(plain, dotted);
        Assert.Same(plain, backslashes);
    }

    /// <summary>
    ///     Validates that a reference text that is never allowed is refused with the matching kind before any open or
    ///     callback request, in file-system and callback mode alike.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <param name="kind">The expected failure kind.</param>
    [Theory]
    [InlineData("", FileReferenceFailureKind.EmptyPath)]
    [InlineData("   ", FileReferenceFailureKind.EmptyPath)]
    [InlineData("a\0b.txt", FileReferenceFailureKind.ControlCharacter)]
    [InlineData("a\nb.txt", FileReferenceFailureKind.ControlCharacter)]
    [InlineData("a\u007Fb.txt", FileReferenceFailureKind.ControlCharacter)]
    [InlineData("a\u0085b.txt", FileReferenceFailureKind.ControlCharacter)]
    [InlineData("a.txt:stream", FileReferenceFailureKind.InvalidPath)]
    [InlineData("C:a.txt", FileReferenceFailureKind.InvalidPath)]
    [InlineData("\\\\server\\share\\a.txt", FileReferenceFailureKind.NetworkPath)]
    [InlineData("//server/share/a.txt", FileReferenceFailureKind.NetworkPath)]
    [InlineData("\\\\?\\C:\\a.txt", FileReferenceFailureKind.InvalidPath)]
    [InlineData("\\\\.\\pipe\\x", FileReferenceFailureKind.InvalidPath)]
    public void FileReferenceResolver_Resolve_HostilePathText_IsRejectedWithoutAnyAccess(string reference, FileReferenceFailureKind kind)
    {
        // Arrange: a file-system resolver that records opens, and a callback resolver that records requests
        using var directory = new SandboxTempDirectory();
        var opened = new List<string>();
        var onDisk = Create(new FileReferenceOptions { BaseDirectory = directory.Path }, Recording(opened));
        var files = new SandboxVirtualFiles();
        var viaCallback = Create(new FileReferenceOptions { FileResolver = files.Resolve });

        // Act
        var diskError = Assert.Throws<FileReferenceException>(() => onDisk.Resolve(reference));
        var callbackError = Assert.Throws<FileReferenceException>(() => viaCallback.Resolve(reference));

        // Assert: the rule is named, the reference is printable, and nothing was touched
        Assert.Equal(kind, diskError.Kind);
        Assert.Equal(kind, callbackError.Kind);
        Assert.NotNull(diskError.Reason);
        Assert.DoesNotContain(diskError.Reference, char.IsControl);
        Assert.Contains("is not allowed", diskError.Message, StringComparison.Ordinal);
        Assert.Empty(opened);
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a drive path is an absolute path on Windows and an invalid path on Linux and macOS, which have
    ///     no drives.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_DrivePath_IsAbsoluteOnWindowsAndInvalidElsewhere()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var full = directory.Write("drive/a.txt", "drive");
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path });

        // Act / Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("drive", Text(resolver.Resolve(full.Replace('\\', '/'))));
        }
        else
        {
            var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("C:/a.txt"));
            Assert.Equal(FileReferenceFailureKind.InvalidPath, ex.Kind);
        }
    }

    /// <summary>
    ///     Validates that a null reference is an argument error.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_NullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = Create(new FileReferenceOptions { BaseDirectory = "." });

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    /// <summary>
    ///     Validates that a missing file and a dangling link are both a miss: the next candidate is tried, and with none
    ///     the result is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_MissingFileAndDanglingLink_ReturnNull()
    {
        // Arrange: a link whose target is removed afterwards, and a search path that has the file the link would hide
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        Directory.CreateDirectory(directory.Combine("outside/gone"));
        directory.Write("lib/dangling/x.txt", "from lib");
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("base/dangling"), directory.Combine("outside/gone"));
        Directory.Delete(directory.Combine("outside/gone"));
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("lib")] });
        var alone = Create(new FileReferenceOptions { BaseDirectory = directory.Combine("base") });

        // Act
        var missing = alone.Resolve("none.txt");
        var danglingAlone = alone.Resolve("dangling/x.txt");
        var fallThrough = resolver.Resolve("dangling/x.txt");

        // Assert
        Assert.Null(missing);
        Assert.Null(danglingAlone);
        Assert.Equal("from lib", Text(fallThrough));
    }

    /// <summary>
    ///     Validates that a resolver without a base directory, search path or callback finds nothing.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_NoSource_ReturnsNullAndHasSourceIsFalse()
    {
        // Arrange
        var resolver = Create(new FileReferenceOptions());

        // Act
        var relative = resolver.Resolve("a.txt");
        var absolute = resolver.Resolve(Path.GetFullPath("a.txt"));

        // Assert
        Assert.False(resolver.HasSource);
        Assert.Null(relative);
        Assert.Null(absolute);
    }

    /// <summary>
    ///     Validates that a directory where a file is expected is not a regular file, whose message names no host
    ///     directory.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_DirectoryAtFilePath_IsNotARegularFile()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("folder.txt"));
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("folder.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, ex.Kind);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates on Windows that a device handle (the null device, injected through the open seam) is not a regular
    ///     file and is never read.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_InjectedNulHandle_IsNotARegularFile()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        directory.Write("device.txt", "x");
        var resolver = Create(
            new FileReferenceOptions { BaseDirectory = directory.Path },
            new FileOpenHooks { Open = _ => FileTestSupport.OpenNullDevice() });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("device.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, ex.Kind);
    }

    /// <summary>
    ///     Validates on Linux and macOS that a device reached through an absolute reference is not a regular file.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_DevNull_IsNotARegularFile()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("/dev/null"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, ex.Kind);
    }

    /// <summary>
    ///     Validates on Linux and macOS that a named pipe without a writer is refused as not a regular file instead of
    ///     hanging the open. The only wait is a guard that fails the test if the resolver hangs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FileReferenceResolver_Resolve_Fifo_IsNotARegularFileWithoutBlocking()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        var failure = FileTestSupport.CreateFifo(directory.Combine("pipe"));
        Assert.True(failure.Length == 0, failure);
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path });

        // Act
        var resolve = Task.Run(() => Assert.Throws<FileReferenceException>(() => resolver.Resolve("pipe")), Ct);
        var first = await Task.WhenAny(resolve, Task.Delay(TimeSpan.FromMinutes(1), Ct));

        // Assert: the resolve returned (it did not block on the pipe) and refused it
        Assert.Same(resolve, first);
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, (await resolve).Kind);
    }

    /// <summary>
    ///     Validates that an existing file whose open fails is a <c>CannotRead</c> failure that ends the search.
    /// </summary>
    /// <param name="denied">Whether the open fails with an access failure (otherwise an I/O failure).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileReferenceResolver_Resolve_OpenFailure_IsCannotReadAndStopsTheSearch(bool denied)
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("two/a.txt", "from two");
        var opened = new List<string>();
        var resolver = Create(
            new FileReferenceOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("two")] },
            new FileOpenHooks
            {
                Open = path =>
                {
                    opened.Add(path);
                    throw denied ? new UnauthorizedAccessException("denied") : new IOException("locked");
                },
            });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.CannotRead, ex.Kind);
        Assert.NotNull(ex.InnerException);
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.txt"))], opened);
    }

    /// <summary>
    ///     Validates that "not found" from the open itself is a miss, so the next candidate is tried.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_FileNotFoundFromOpen_TriesTheNextCandidate()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("two/a.txt", "from two");
        var first = Path.GetFullPath(directory.Combine("base/a.txt"));
        var resolver = Create(
            new FileReferenceOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("two")] },
            new FileOpenHooks { Open = path => path == first ? throw new FileNotFoundException("gone") : OpenShared(path) });

        // Act
        var file = resolver.Resolve("a.txt");

        // Assert
        Assert.Equal("from two", Text(file));
    }

    /// <summary>
    ///     Validates that a file held open by a writer with permissive sharing is still read.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_FileHeldOpenByWriter_IsStillRead()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var path = directory.Write("a.txt", "held");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path });

        // Act
        var file = resolver.Resolve("a.txt");

        // Assert
        Assert.Equal("held", Text(file));
    }

    /// <summary>
    ///     Validates the byte limit at its boundary, for real files and callback files alike.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_MaxFileBytesAtLimitAndOneOver()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("ten.txt", "0123456789");
        directory.Write("eleven.txt", "0123456789A");
        var files = new SandboxVirtualFiles().With("ten.txt", "0123456789").With("eleven.txt", "0123456789A");
        var onDisk = Create(new FileReferenceOptions { BaseDirectory = directory.Path, MaxFileBytes = 10 });
        var viaCallback = Create(new FileReferenceOptions { FileResolver = files.Resolve, MaxFileBytes = 10 });

        // Act / Assert
        Assert.Equal("0123456789", Text(onDisk.Resolve("ten.txt")));
        Assert.Equal(FileReferenceFailureKind.TooLarge, Assert.Throws<FileReferenceException>(() => onDisk.Resolve("eleven.txt")).Kind);
        Assert.Equal("0123456789", Text(viaCallback.Resolve("ten.txt")));
        Assert.Equal(FileReferenceFailureKind.TooLarge, Assert.Throws<FileReferenceException>(() => viaCallback.Resolve("eleven.txt")).Kind);
    }

    /// <summary>
    ///     Validates that a stream that never ends stops one chunk past the byte limit.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_EndlessCallbackStream_StopsAtTheBound()
    {
        // Arrange
        var endless = new SandboxEndlessStream();
        var resolver = Create(new FileReferenceOptions { FileResolver = _ => endless, MaxFileBytes = 20_000 });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("endless.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.TooLarge, ex.Kind);
        Assert.InRange(endless.BytesRead, 20_000, 20_000 + 8192);
    }

    /// <summary>
    ///     Validates the file limit: every file that was opened counts whatever the outcome (denied, too large, not a
    ///     regular file), a miss does not, a new file beyond the limit is refused before it is opened, and a file that
    ///     was opened before is not refused by the limit when it is asked for again.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_MaxFiles_CountsOpenedFilesAndRefusesBeforeOpen()
    {
        // Arrange: a limit of three, and three different failures before the fourth file
        using var directory = new SandboxTempDirectory();
        directory.Write("denied.txt", "d");
        directory.Write("big.txt", "0123456789");
        Directory.CreateDirectory(directory.Combine("folder"));
        directory.Write("a.txt", "a");
        var opened = new List<string>();
        var policy = FileAccessPolicy.FromDelegate(
            "deny-by-name",
            request => request.RequestedPath.EndsWith("denied.txt", StringComparison.Ordinal)
                ? FileAccessDecision.Deny("the name is denied")
                : FileAccessDecision.Allow);
        var resolver = Create(
            new FileReferenceOptions { BaseDirectory = directory.Path, MaxFiles = 3, MaxFileBytes = 5, FileAccess = policy },
            Recording(opened));

        // Act
        var missing = resolver.Resolve("missing.txt");
        var denied = Assert.Throws<FileReferenceException>(() => resolver.Resolve("denied.txt"));
        var big = Assert.Throws<FileReferenceException>(() => resolver.Resolve("big.txt"));
        var folder = Assert.Throws<FileReferenceException>(() => resolver.Resolve("folder"));
        var beyond = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));
        var deniedAgain = Assert.Throws<FileReferenceException>(() => resolver.Resolve("denied.txt"));

        // Assert: three files counted, the miss did not, and the fourth file was never opened
        Assert.Null(missing);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, denied.Kind);
        Assert.Equal(FileReferenceFailureKind.TooLarge, big.Kind);
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, folder.Kind);
        Assert.Equal(FileReferenceFailureKind.TooManyFiles, beyond.Kind);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, deniedAgain.Kind);
        Assert.DoesNotContain(Path.GetFullPath(directory.Combine("a.txt")), opened);
    }

    /// <summary>
    ///     Validates that one resolver opens a file and a missing file once, returns the same instance for the same file,
    ///     and does not read a file again after it changed.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_SameFileTwice_IsOpenedOnceAndReturnsTheSameInstance()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var path = directory.Write("a.txt", "first");
        var opened = new List<string>();
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path }, Recording(opened));

        // Act
        var first = resolver.Resolve("a.txt");
        File.WriteAllText(path, "second");
        var again = resolver.Resolve("./a.txt");
        resolver.Resolve("none.txt");
        resolver.Resolve("none.txt");

        // Assert
        Assert.Same(first, again);
        Assert.Equal("first", Text(again));
        Assert.Equal([Path.GetFullPath(path), Path.GetFullPath(directory.Combine("none.txt"))], opened);
    }

    /// <summary>
    ///     Validates that the key comparer is ordinal for logical paths and the platform's path comparison otherwise.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_KeyComparer_FollowsModeAndPlatform()
    {
        // Arrange
        var logical = Create(new FileReferenceOptions { FileResolver = _ => null });
        var onDisk = Create(new FileReferenceOptions { BaseDirectory = "." });
        var ignoresCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        // Act / Assert
        Assert.Same(StringComparer.Ordinal, logical.KeyComparer);
        Assert.Equal(ignoresCase, onDisk.KeyComparer.Equals("A.txt", "a.txt"));
    }

    /// <summary>
    ///     Validates that callback mode hands the callback only normalized logical paths (the referrer's directory or the
    ///     root, then each valid search path as a prefix), never reads the file system, and never asks the policy.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CallbackMode_UsesLogicalPathsAndNeverConsultsThePolicy()
    {
        // Arrange: a policy that would deny everything, and a base directory whose same-named file must not be read
        using var directory = new SandboxTempDirectory();
        directory.Write("sub/a.txt", "from disk");
        var files = new SandboxVirtualFiles().With("sub/a.txt", "a").With("sub/b.txt", "b").With("lib/x.txt", "x");
        var policy = new RecordingPolicy(_ => FileAccessDecision.Deny("never"));
        var resolver = Create(new FileReferenceOptions
        {
            BaseDirectory = directory.Path,
            FileResolver = files.Resolve,
            SearchPaths = ["lib", "C:bad"],
            FileAccess = policy,
        });

        // Act
        var a = resolver.Resolve("sub\\a.txt");
        var b = resolver.Resolve("./b.txt", a);
        var x = resolver.Resolve("x.txt");

        // Assert
        Assert.Equal("a", Text(a));
        Assert.Equal("b", Text(b));
        Assert.Equal("x", Text(x));
        Assert.Null(a!.RealPath);
        Assert.Equal("sub", a.Directory);
        Assert.Equal(["sub/a.txt", "sub/b.txt", "x.txt", "lib/x.txt"], files.Requests);
        Assert.Empty(policy.Requests);
    }

    /// <summary>
    ///     Validates that in callback mode a <c>..</c> above the logical root and an absolute reference are refused
    ///     without a request, while a <c>..</c> that stays inside is normalized.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CallbackMode_DotDotAboveRootAndAbsolutePath_AreInvalidPath()
    {
        // Arrange
        var files = new SandboxVirtualFiles().With("x.txt", "x");
        var resolver = Create(new FileReferenceOptions { FileResolver = files.Resolve });

        // Act
        var above = Assert.Throws<FileReferenceException>(() => resolver.Resolve("../x.txt"));
        var rooted = Assert.Throws<FileReferenceException>(() => resolver.Resolve("/x.txt"));
        var drive = Assert.Throws<FileReferenceException>(() => resolver.Resolve("C:/x.txt"));
        var inside = resolver.Resolve("sub/../x.txt");

        // Assert
        Assert.Equal(FileReferenceFailureKind.InvalidPath, above.Kind);
        Assert.Equal(FileReferenceFailureKind.InvalidPath, rooted.Kind);
        Assert.Equal(FileReferenceFailureKind.InvalidPath, drive.Kind);
        Assert.Equal("x", Text(inside));
        Assert.Equal(["x.txt"], files.Requests);
    }

    /// <summary>
    ///     Validates that every failure of the callback - even a forged <see cref="FileReferenceException"/> - is
    ///     <c>CannotRead</c> with the cause kept, while a cancellation passes through.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CallbackFailure_IsCannotReadAndCancellationPassesThrough()
    {
        // Arrange
        var failing = Create(new FileReferenceOptions { FileResolver = _ => throw new IOException("broken") });
        var forging = Create(new FileReferenceOptions
        {
            FileResolver = _ => throw new FileReferenceException(FileReferenceFailureKind.AccessDenied, "x"),
        });
        var canceling = Create(new FileReferenceOptions { FileResolver = _ => throw new OperationCanceledException() });

        // Act
        var broken = Assert.Throws<FileReferenceException>(() => failing.Resolve("a.txt"));
        var forged = Assert.Throws<FileReferenceException>(() => forging.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.CannotRead, broken.Kind);
        Assert.IsType<IOException>(broken.InnerException);
        Assert.DoesNotContain("broken", broken.Message, StringComparison.Ordinal);
        Assert.Equal(FileReferenceFailureKind.CannotRead, forged.Kind);
        Assert.Throws<OperationCanceledException>(() => canceling.Resolve("a.txt"));
    }

    /// <summary>
    ///     Validates that a canceled token stops a resolve before any request or open.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CanceledToken_StopsBeforeAnyAccess()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var files = new SandboxVirtualFiles().With("a.txt", "a");
        var opened = new List<string>();
        var viaCallback = new FileReferenceResolver(new FileReferenceOptions { FileResolver = files.Resolve }, source.Token);
        var onDisk = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = directory.Path }, source.Token, Recording(opened));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => viaCallback.Resolve("a.txt"));
        Assert.Throws<OperationCanceledException>(() => onDisk.Resolve("a.txt"));
        Assert.Empty(files.Requests);
        Assert.Empty(opened);
    }

    /// <summary>
    ///     Validates what the policy is asked (reference, referrer, base directory, requested and real path) and that a
    ///     denial is reported with the reason, the hint, the policy name and the paths, without a host directory in its
    ///     message.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_PolicyDenial_IsAccessDeniedWithFactsAndNothingRead()
    {
        // Arrange: the policy allows the main file and denies the one it references
        using var directory = new SandboxTempDirectory();
        directory.Write("base/sub/main.txt", "main");
        directory.Write("base/x.txt", "x");
        var document = directory.Combine("document.svg");
        var policy = new RecordingPolicy(request => request.RequestedPath.EndsWith("main.txt", StringComparison.Ordinal)
            ? FileAccessDecision.Allow
            : FileAccessDecision.Deny("not this one", "try another"));
        var resolver = Create(new FileReferenceOptions
        {
            BaseDirectory = directory.Combine("base"),
            ReferrerPath = document,
            FileAccess = policy,
        });

        // Act
        var main = resolver.Resolve("sub/main.txt");
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("../x.txt", main));

        // Assert: the first request came from the top-level document, the second from the main file
        Assert.Equal(document, policy.Requests[0].Referrer);
        Assert.Equal(Path.GetFullPath(directory.Combine("base")), policy.Requests[0].BaseDirectory);
        var request = policy.Requests[1];
        Assert.Equal("../x.txt", request.Reference);
        Assert.Equal(main!.Key, request.Referrer);
        Assert.Equal(Path.GetFullPath(directory.Combine("base/sub")), request.BaseDirectory);
        Assert.Equal(Path.GetFullPath(directory.Combine("base/x.txt")), request.RequestedPath);
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Combine("base")), "x.txt"), request.RealPath));

        // Assert: the denial carries the facts, and its message names the policy but no host directory
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("../x.txt", ex.Reference);
        Assert.Equal("not this one", ex.Reason);
        Assert.Equal("try another", ex.Hint);
        Assert.Equal("recording", ex.PolicyName);
        Assert.Equal(request.RequestedPath, ex.RequestedPath);
        Assert.Equal(request.RealPath, ex.RealPath);
        Assert.Contains("'recording'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not this one", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a denial ends the search: a later search path that has the file is not tried.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_PolicyDenial_StopsTheSearch()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("two/a.txt", "from two");
        var opened = new List<string>();
        var resolver = Create(
            new FileReferenceOptions
            {
                BaseDirectory = directory.Combine("base"),
                SearchPaths = [directory.Combine("two")],
                FileAccess = FileAccessPolicy.FromDelegate("deny", _ => FileAccessDecision.Deny("denied")),
            },
            Recording(opened));

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.txt"))], opened);
    }

    /// <summary>
    ///     Validates that a policy that throws or answers nothing denies the file (fail closed), while a cancellation it
    ///     raises passes through.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_ThrowingOrNullPolicy_IsAccessDeniedAndCancellationPassesThrough()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        FileReferenceResolver With(FileAccessPolicy policy) =>
            Create(new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = policy });
        var throwing = With(FileAccessPolicy.FromDelegate("boom", _ => throw new InvalidOperationException("boom")));
        var silent = With(FileAccessPolicy.FromDelegate("silent", _ => null!));
        var canceling = With(FileAccessPolicy.FromDelegate("cancel", _ => throw new OperationCanceledException()));

        // Act
        var thrown = Assert.Throws<FileReferenceException>(() => throwing.Resolve("a.txt"));
        var none = Assert.Throws<FileReferenceException>(() => silent.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, thrown.Kind);
        Assert.Equal("the file-access policy failed", thrown.Reason);
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, none.Kind);
        Assert.Throws<OperationCanceledException>(() => canceling.Resolve("a.txt"));
    }

    /// <summary>
    ///     Validates that a cached file is read once but the policy is asked on every resolve, so a later denial is
    ///     honored.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CachedFile_IsReCheckedOnEveryResolveButReadOnce()
    {
        // Arrange: a policy that allows the first two requests and denies the third
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        var opened = new List<string>();
        var asked = 0;
        var policy = new RecordingPolicy(_ => ++asked <= 2 ? FileAccessDecision.Allow : FileAccessDecision.Deny("changed its mind"));
        var resolver = Create(new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = policy }, Recording(opened));

        // Act
        var first = resolver.Resolve("a.txt");
        var second = resolver.Resolve("a.txt");
        var third = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Same(first, second);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, third.Kind);
        Assert.Equal(3, policy.Requests.Count);
        Assert.Single(opened);
    }

    /// <summary>
    ///     Validates that a roots policy refuses a path as written outside its roots before the file is opened, and that
    ///     such a refusal does not count against the file limit.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_UnderRootsPreCheck_DeniesBeforeTheOpen()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "a");
        directory.Write("outside/x.txt", "x");
        var opened = new List<string>();
        var resolver = Create(
            new FileReferenceOptions
            {
                BaseDirectory = directory.Combine("base"),
                FileAccess = FileAccessPolicy.UnderRoots(directory.Combine("base")),
                MaxFiles = 1,
            },
            Recording(opened));

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("../outside/x.txt"));
        var inside = resolver.Resolve("a.txt");

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("roots", ex.PolicyName);
        Assert.Null(ex.RealPath);
        Assert.Equal("a", Text(inside));
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.txt"))], opened);
    }

    /// <summary>
    ///     Validates that a restrictive policy fails closed when the real path is unavailable, while the default policy
    ///     still reads the file.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_RestrictivePolicyWithoutRealPath_IsRealPathUnavailable()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        var unavailable = new FileOpenHooks { RealPathOf = _ => null };
        var restrictive = Create(
            new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = FileAccessPolicy.NoSymlinks },
            unavailable);
        var permissive = Create(new FileReferenceOptions { BaseDirectory = directory.Path }, unavailable);

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => restrictive.Resolve("a.txt"));
        var file = permissive.Resolve("a.txt");

        // Assert
        Assert.Equal(FileReferenceFailureKind.RealPathUnavailable, ex.Kind);
        Assert.Equal("no-symlinks", ex.PolicyName);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
        Assert.Equal("a", Text(file));
        Assert.Null(file!.RealPath);
    }

    /// <summary>
    ///     Validates that a file of the other mode cannot be the starting point of a reference.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_RelativeToFromTheOtherMode_ThrowsArgumentException()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        var onDisk = Create(new FileReferenceOptions { BaseDirectory = directory.Path });
        var viaCallback = Create(new FileReferenceOptions { FileResolver = new SandboxVirtualFiles().With("a.txt", "a").Resolve });
        var diskFile = onDisk.Resolve("a.txt");
        var logicalFile = viaCallback.Resolve("a.txt");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => onDisk.Resolve("a.txt", logicalFile));
        Assert.Throws<ArgumentException>(() => viaCallback.Resolve("a.txt", diskFile));
    }

    /// <summary>
    ///     Validates that a configured directory that is not a valid path is reported on first use.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_InvalidBaseDirectory_IsInvalidBaseDirectory()
    {
        // Arrange
        var invalidBase = Create(new FileReferenceOptions { BaseDirectory = "bad\0dir" });
        var invalidSearch = Create(new FileReferenceOptions { SearchPaths = ["bad\0dir"] });

        // Act
        var baseError = Assert.Throws<FileReferenceException>(() => invalidBase.Resolve("a.txt"));
        var searchError = Assert.Throws<FileReferenceException>(() => invalidSearch.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.InvalidBaseDirectory, baseError.Kind);
        Assert.Equal("bad\0dir", baseError.Reference);
        Assert.Equal(FileReferenceFailureKind.InvalidBaseDirectory, searchError.Kind);
    }

    /// <summary>
    ///     Validates that invalid options are rejected when the resolver is created.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Constructor_InvalidArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new FileReferenceResolver(null!));
        Assert.Throws<ArgumentNullException>(() => new FileReferenceResolver(new FileReferenceOptions { SearchPaths = null! }));
        Assert.Throws<ArgumentNullException>(() => new FileReferenceResolver(new FileReferenceOptions { FileAccess = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileReferenceResolver(new FileReferenceOptions { MaxFileBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileReferenceResolver(new FileReferenceOptions { MaxFileBytes = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileReferenceResolver(new FileReferenceOptions { MaxFiles = -1 }));
    }
}
