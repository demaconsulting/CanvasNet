using System.Text;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for <see cref="SandboxedFileResolver"/>: the search order, the confinement and path-text rules, the
///     resolver-callback mode, the open and bounded-read rules, the failure mapping and the per-instance cache. The link
///     rule has its own class, <see cref="SandboxedFileResolverLinkTests"/>. No test needs a symbolic-link privilege or
///     a file permission: failures are provoked through the internal open seam.
/// </summary>
public class SandboxedFileResolverTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates a resolver over options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The resolver.</returns>
    private static SandboxedFileResolver Create(SandboxOptions options) => new(options, Ct);

    /// <summary>
    ///     Decodes the content of a resolved file as UTF-8.
    /// </summary>
    /// <param name="file">The file; must not be null.</param>
    /// <returns>The text.</returns>
    private static string Text(SandboxedFile? file)
    {
        Assert.NotNull(file);
        return Encoding.UTF8.GetString(file.Content.Span);
    }

    /// <summary>
    ///     Validates that a path is found under the base directory first and then under each search path in order, and
    ///     that the result carries its key, directory, root and bytes.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RelativePathUsesBaseDirectoryThenSearchPaths()
    {
        // Arrange: the same name in the base directory and in the first search path, another name only in the second
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("one/a.txt", "from one");
        directory.Write("one/b.txt", "b from one");
        directory.Write("two/b.txt", "b from two");
        directory.Write("two/sub/c.txt", "c from two");
        var resolver = Create(new SandboxOptions
        {
            BaseDirectory = directory.Combine("base"),
            SearchPaths = [directory.Combine("one"), directory.Combine("two")],
        });

        // Act
        var a = resolver.Resolve("a.txt");
        var b = resolver.Resolve("b.txt");
        var c = resolver.Resolve("sub\\c.txt");

        // Assert: the first root that has the file wins
        Assert.Equal("from base", Text(a));
        Assert.Equal("b from one", Text(b));
        Assert.Equal("c from two", Text(c));
        Assert.True(resolver.HasSource);

        // Assert: the result says where the file is and which root confines it
        Assert.Equal(Path.GetFullPath(directory.Combine("two/sub/c.txt")), c!.Key);
        Assert.Equal(Path.GetFullPath(directory.Combine("two/sub")), c.Directory);
        Assert.Equal(Path.GetFullPath(directory.Combine("two")), c.Root);
    }

    /// <summary>
    ///     Validates that a path resolved relative to a file starts in that file's directory (confined to the root of
    ///     that file), then falls back to the search paths, and that it can climb to, but not above, that root.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RelativeToFileStartsInItsDirectoryThenSearchPaths()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("base/sub/main.txt", "main");
        directory.Write("base/sub/near.txt", "near");
        directory.Write("base/top.txt", "top");
        directory.Write("lib/far.txt", "far");
        directory.Write("secret.txt", "secret");
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("lib")] });
        var main = resolver.Resolve("sub/main.txt");

        // Act
        var near = resolver.Resolve("near.txt", main);
        var top = resolver.Resolve("../top.txt", main);
        var far = resolver.Resolve("far.txt", main);
        var escape = Assert.Throws<SandboxException>(() => resolver.Resolve("../../secret.txt", main));

        // Assert: the including file's directory is searched first, then the library; the base directory's parent is out of reach
        Assert.Equal("near", Text(near));
        Assert.Equal("top", Text(top));
        Assert.Equal("far", Text(far));
        Assert.Equal(SandboxFailureKind.OutsideRoot, escape.Kind);
    }

    /// <summary>
    ///     Validates that <c>.</c> and <c>..</c> inside the root are normalized (also with backslashes) and that a
    ///     <c>..</c> that leaves the root is a sandbox violation, even when the target exists.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_DotDotInsideRootNormalizesAndEscapeIsRejected()
    {
        // Arrange: a file inside the root and a file beside the root that must stay out of reach
        using var directory = new SandboxTempDirectory();
        directory.Write("root/sub/a.txt", "inside");
        directory.Write("outside.txt", "outside");
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var normalized = resolver.Resolve("sub/../sub/./a.txt");
        var backslashes = resolver.Resolve("sub\\..\\sub\\a.txt");
        var escape = Assert.Throws<SandboxException>(() => resolver.Resolve("../outside.txt"));
        var deepEscape = Assert.Throws<SandboxException>(() => resolver.Resolve("sub/../../outside.txt"));

        // Assert: one file under every spelling, and the escapes are refused with the normalized text
        Assert.Same(normalized, backslashes);
        Assert.Equal("inside", Text(normalized));
        Assert.Equal(SandboxFailureKind.OutsideRoot, escape.Kind);
        Assert.Equal("../outside.txt", escape.Path);
        Assert.Equal(SandboxFailureKind.OutsideRoot, deepEscape.Kind);
    }

    /// <summary>
    ///     Validates that a sibling directory whose name merely starts with the root's name is not under the root, and
    ///     that a file that exists outside the root is refused, not read.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_SiblingWithSharedPrefixAndOutsideFileAreRefused()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "a");
        directory.Write("root-evil/b.txt", "b");
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });

        // Act / Assert
        var sibling = Assert.Throws<SandboxException>(() => resolver.Resolve("../root-evil/b.txt"));
        Assert.Equal(SandboxFailureKind.OutsideRoot, sibling.Kind);
        Assert.Equal("a", Text(resolver.Resolve("a.txt")));
    }

    /// <summary>
    ///     Validates that a search path is a confinement of its own: a relative path cannot climb out of it, while a
    ///     file directly under it is found when there is no base directory.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_SearchPathConfinesCandidates()
    {
        // Arrange: no base directory, so only the search-path candidate exists
        using var directory = new SandboxTempDirectory();
        directory.Write("lib/x.txt", "x");
        directory.Write("outside.txt", "o");
        var resolver = Create(new SandboxOptions { SearchPaths = [directory.Combine("lib")] });

        // Act / Assert
        Assert.Throws<SandboxException>(() => resolver.Resolve("../outside.txt"));
        Assert.Equal("x", Text(resolver.Resolve("x.txt")));
    }

    /// <summary>
    ///     Validates that a path text that is empty, white space, absolute, rooted, a drive or UNC path, or contains a
    ///     colon or a control character (C0, DEL and C1) is rejected, with the matching kind and a message that shows
    ///     the path with control characters replaced, before any file-system access or callback request.
    /// </summary>
    /// <param name="path">The hostile path text.</param>
    /// <param name="kind">The expected failure kind.</param>
    [Theory]
    [InlineData("", SandboxFailureKind.EmptyPath)]
    [InlineData("   ", SandboxFailureKind.EmptyPath)]
    [InlineData("/etc/hosts", SandboxFailureKind.AbsolutePath)]
    [InlineData("\\windows\\system32\\a.txt", SandboxFailureKind.AbsolutePath)]
    [InlineData("\\\\server\\share\\a.txt", SandboxFailureKind.AbsolutePath)]
    [InlineData("//server/share/a.txt", SandboxFailureKind.AbsolutePath)]
    [InlineData("C:\\data\\a.txt", SandboxFailureKind.ColonInPath)]
    [InlineData("C:a.txt", SandboxFailureKind.ColonInPath)]
    [InlineData("dir/a:b.txt", SandboxFailureKind.ColonInPath)]
    [InlineData("a\u0000b.txt", SandboxFailureKind.ControlCharacter)]
    [InlineData("a\nb.txt", SandboxFailureKind.ControlCharacter)]
    [InlineData("a\u007Fb.txt", SandboxFailureKind.ControlCharacter)]
    [InlineData("a\u0085b.txt", SandboxFailureKind.ControlCharacter)]
    [InlineData("a\u009Fb.txt", SandboxFailureKind.ControlCharacter)]
    public void SandboxedFileResolver_Resolve_HostilePathTextIsRejectedWithoutAnyAccess(string path, SandboxFailureKind kind)
    {
        // Arrange: a callback that records requests (the file system is never touched in this mode either)
        var files = new SandboxVirtualFiles().With("a.txt", "x");
        var resolver = Create(new SandboxOptions { FileResolver = files.Resolve });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve(path));

        // Assert: the kind, a reason in Detail, a printable path, and no request made
        Assert.Equal(kind, ex.Kind);
        Assert.False(string.IsNullOrEmpty(ex.Detail));
        Assert.DoesNotContain(ex.Path, c => char.IsControl(c));
        Assert.Contains("is not allowed", ex.Message, StringComparison.Ordinal);
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a null path is an argument error, not a sandbox failure.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_NullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = Create(new SandboxOptions { FileResolver = _ => null });

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    /// <summary>
    ///     Validates that a base directory or search path that is not a valid path is an <c>InvalidRoot</c> failure that
    ///     names the configured directory, not a raw argument exception.
    /// </summary>
    /// <param name="inBaseDirectory">Whether the invalid directory is the base directory (otherwise a search path).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SandboxedFileResolver_Resolve_InvalidConfiguredDirectoryIsInvalidRoot(bool inBaseDirectory)
    {
        // Arrange: a NUL character is never valid in a path
        var options = inBaseDirectory
            ? new SandboxOptions { BaseDirectory = "bad\0dir" }
            : new SandboxOptions { SearchPaths = ["bad\0dir"] };
        var resolver = Create(options);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.InvalidRoot, ex.Kind);
        Assert.Equal("bad\0dir", ex.Path);
    }

    /// <summary>
    ///     Validates resolver mode: the callback receives normalized logical paths (the root first, then each valid
    ///     search path as a prefix), the base directory and the file system play no role, streams are disposed, requests
    ///     are made on the calling thread, and an escape above the logical root is refused before the callback is asked.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_ResolverModeUsesNormalizedLogicalPathsWithoutFileSystem()
    {
        // Arrange: a base directory with a file of the same name that must never be read, and a mix of valid and invalid search paths
        using var directory = new SandboxTempDirectory();
        directory.Write("a/b.txt", "from the file system");
        var files = new SandboxVirtualFiles()
            .With("a/b.txt", "virtual b")
            .With("lib/x.txt", "virtual x")
            .With("lib/deep/y.txt", "virtual y");
        var resolver = Create(new SandboxOptions
        {
            BaseDirectory = directory.Path,
            SearchPaths = ["/absolute", "C:\\drive", "lib", "lib\\deep"],
            FileResolver = files.Resolve,
        });

        // Act
        var b = resolver.Resolve("a/./c/../b.txt");
        var x = resolver.Resolve("x.txt");
        var y = resolver.Resolve("y.txt");

        // Assert: normalized logical paths, root first and then the valid prefixes in declaration order
        Assert.Equal("virtual b", Text(b));
        Assert.Equal("virtual x", Text(x));
        Assert.Equal("virtual y", Text(y));
        Assert.Equal(["a/b.txt", "x.txt", "lib/x.txt", "y.txt", "lib/y.txt", "lib/deep/y.txt"], files.Requests);
        Assert.All(files.Streams, s => Assert.True(s.IsDisposed));
        Assert.All(files.Threads, t => Assert.Equal(Environment.CurrentManagedThreadId, t));
        Assert.Equal(("a/b.txt", "a", string.Empty), (b!.Key, b.Directory, b.Root));

        // Assert: an escape above the logical root is refused before the callback is asked
        var before = files.Requests.Count;
        Assert.Throws<SandboxException>(() => resolver.Resolve("../x.txt"));
        Assert.Equal(before, files.Requests.Count);
    }

    /// <summary>
    ///     Validates that in resolver mode a path relative to a logical file starts in that file's logical directory and
    ///     cannot climb above its logical root.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_ResolverModeRelativeToFileUsesItsLogicalDirectory()
    {
        // Arrange
        var files = new SandboxVirtualFiles().With("libs/main.txt", "main").With("libs/near.txt", "near").With("top.txt", "top");
        var resolver = Create(new SandboxOptions { FileResolver = files.Resolve });
        var main = resolver.Resolve("libs/main.txt");

        // Act
        var near = resolver.Resolve("near.txt", main);
        var top = resolver.Resolve("../top.txt", main);
        var escape = Assert.Throws<SandboxException>(() => resolver.Resolve("../../top.txt", main));

        // Assert
        Assert.Equal("near", Text(near));
        Assert.Equal("top", Text(top));
        Assert.Equal(SandboxFailureKind.OutsideRoot, escape.Kind);
    }

    /// <summary>
    ///     Validates that a file that does not exist yields <see langword="null"/> (never a failure), also in resolver
    ///     mode, and that without any source nothing is found and <c>HasSource</c> says so.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_MissingFileReturnsNullAndNoSourceHasNone()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        var onDisk = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });
        var virtualFiles = Create(new SandboxOptions { FileResolver = new SandboxVirtualFiles().Resolve });
        var none = Create(new SandboxOptions());

        // Act / Assert
        Assert.Null(onDisk.Resolve("nope.txt"));
        Assert.Null(virtualFiles.Resolve("nope.txt"));
        Assert.Null(none.Resolve("nope.txt"));
        Assert.False(none.HasSource);
        Assert.True(Create(new SandboxOptions { SearchPaths = ["x"] }).HasSource);
    }

    /// <summary>
    ///     Validates that an existing directory where a file is expected is a <c>CannotRead</c> failure (it exists but
    ///     cannot be read as data), not a missing file.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_ExistingDirectoryAtFilePathIsCannotRead()
    {
        // Arrange: a sub folder named like the requested file
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/folder.txt"));
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("folder.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an existing file whose open fails (an access or I/O failure) is a <c>CannotRead</c> failure,
    ///     never a missing file, and that no later root is tried. The opener is injected, so the test needs no file
    ///     permission or lock and behaves the same on every platform.
    /// </summary>
    /// <param name="denied">Whether the open fails with an access failure (otherwise an I/O failure).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SandboxedFileResolver_Resolve_ExistingFileOpenFailureIsCannotRead(bool denied)
    {
        // Arrange: the file exists in the base directory and in the second root; the open of every file fails
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("two/a.txt", "from two");
        var opened = new List<string>();
        var resolver = new SandboxedFileResolver(
            new SandboxOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("two")] },
            Ct,
            null,
            path =>
            {
                opened.Add(path);
                throw denied ? new UnauthorizedAccessException("denied") : new IOException("locked");
            });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert: unreadable rather than missing, the cause kept, and the second root was never opened
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
        Assert.NotNull(ex.InnerException);
        Assert.Equal(Path.GetFullPath(directory.Combine("base/a.txt")), ex.Path);
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.txt"))], opened);
    }

    /// <summary>
    ///     Validates that a file that is reported missing by the open itself (it vanished, or the existence probe could
    ///     not see it) is a missing candidate: the next root is tried, in base-directory-then-search-path order.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_FileNotFoundFromOpenIsMissingAndTriesNextRoot()
    {
        // Arrange: the file exists everywhere, but the open of the first root's file reports "not found" (a race)
        using var directory = new SandboxTempDirectory();
        directory.Write("base/a.txt", "from base");
        directory.Write("two/a.txt", "from two");
        var opened = new List<string>();
        var resolver = new SandboxedFileResolver(
            new SandboxOptions { BaseDirectory = directory.Combine("base"), SearchPaths = [directory.Combine("two")] },
            Ct,
            null,
            path =>
            {
                opened.Add(path);
                return path == Path.GetFullPath(directory.Combine("base/a.txt"))
                    ? throw new FileNotFoundException("gone")
                    : new MemoryStream(Encoding.UTF8.GetBytes("from two"));
            });
        var onlyMissing = new SandboxedFileResolver(
            new SandboxOptions { BaseDirectory = directory.Combine("base") },
            Ct,
            null,
            _ => throw new DirectoryNotFoundException("gone"));

        // Act
        var file = resolver.Resolve("a.txt");
        var missing = onlyMissing.Resolve("a.txt");

        // Assert: the next root supplied the file after the first was tried; with no other root it is missing
        Assert.Equal("from two", Text(file));
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.txt")), Path.GetFullPath(directory.Combine("two/a.txt"))], opened);
        Assert.Null(missing);
    }

    /// <summary>
    ///     Validates that a final component that no existence probe can see is left to the open attempt: a real missing
    ///     file is still missing, and the open is attempted rather than skipped (while a missing directory above the file
    ///     is not opened at all).
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_MissingFinalComponentIsDecidedByTheOpen()
    {
        // Arrange: an existing directory without the file; the opener records the attempt and delegates to the file system
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/sub"));
        var opened = new List<string>();
        var resolver = new SandboxedFileResolver(
            new SandboxOptions { BaseDirectory = directory.Combine("root") },
            Ct,
            null,
            path =>
            {
                opened.Add(path);
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            });

        // Act
        var final = resolver.Resolve("sub/none.txt");
        var intermediate = resolver.Resolve("absent/none.txt");

        // Assert: both are missing; only the missing final component reached the open (a missing directory did not)
        Assert.Null(final);
        Assert.Null(intermediate);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/sub/none.txt"))], opened);
    }

    /// <summary>
    ///     Validates that the default opener shares the file with a concurrent writer: a file that another handle holds
    ///     open for reading and writing (and may be deleted or renamed) is still read.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_FileHeldOpenByWriterIsStillRead()
    {
        // Arrange: another handle holds the file open for writing with the most permissive sharing
        using var directory = new SandboxTempDirectory();
        var path = directory.Write("root/live.txt", "live");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var file = resolver.Resolve("live.txt");

        // Assert
        Assert.Equal("live", Text(file));
    }

    /// <summary>
    ///     Validates that each candidate is requested at most once per resolver: a file that is resolved again comes
    ///     from the cache (as the same instance) and a miss is remembered too.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_CacheRequestsEachCandidateOncePerResolver()
    {
        // Arrange
        var files = new SandboxVirtualFiles().With("a.txt", "x");
        var resolver = Create(new SandboxOptions { FileResolver = files.Resolve, SearchPaths = ["lib"] });

        // Act
        var first = resolver.Resolve("a.txt");
        var second = resolver.Resolve("a.txt");
        Assert.Null(resolver.Resolve("miss.txt"));
        Assert.Null(resolver.Resolve("miss.txt"));

        // Assert: one request for the hit, one per candidate for the miss; the cache hands out the same file
        Assert.Same(first, second);
        Assert.Equal(["a.txt", "miss.txt", "lib/miss.txt"], files.Requests);

        // Assert: a new resolver (a new operation) starts with an empty cache
        Create(new SandboxOptions { FileResolver = files.Resolve }).Resolve("a.txt");
        Assert.Equal(["a.txt", "miss.txt", "lib/miss.txt", "a.txt"], files.Requests);
    }

    /// <summary>
    ///     Validates that a real file is read once per resolver however often it is resolved (also under another
    ///     spelling): a change made after the first read is not seen.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_FileSystemFilesAreCachedPerResolver()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/d/a.txt", "a");
        var resolver = Create(new SandboxOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var first = resolver.Resolve("d/a.txt");
        File.WriteAllText(directory.Combine("root/d/a.txt"), "changed after the first read");
        var second = resolver.Resolve("d/./a.txt");

        // Assert
        Assert.Equal("a", Text(first));
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Validates that the key comparer is ordinal for logical paths and follows the platform for full paths.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_KeyComparer_FollowsModeAndPlatform()
    {
        // Arrange
        var logical = Create(new SandboxOptions { FileResolver = _ => null });
        var onDisk = Create(new SandboxOptions { BaseDirectory = Path.GetTempPath() });
        var caseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        // Act / Assert
        Assert.False(logical.KeyComparer.Equals("A/b.txt", "a/b.txt"));
        Assert.Equal(caseInsensitive, onDisk.KeyComparer.Equals("A/b.txt", "a/b.txt"));
    }

    /// <summary>
    ///     Validates the byte limit for the resolver callback and for real files: exactly the limit is accepted and one
    ///     byte more is refused with <c>TooLarge</c>.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_MaxFileBytesAtLimitAndOneOver()
    {
        // Arrange: files of exactly 10 and 11 bytes, as virtual files and as real files
        var files = new SandboxVirtualFiles().With("ten.txt", "0123456789").With("eleven.txt", "0123456789X");
        var virtualResolver = Create(new SandboxOptions { FileResolver = files.Resolve, MaxFileBytes = 10 });
        using var directory = new SandboxTempDirectory();
        directory.Write("ten.txt", "0123456789");
        directory.Write("eleven.txt", "0123456789X");
        var fileResolver = Create(new SandboxOptions { BaseDirectory = directory.Path, MaxFileBytes = 10 });

        // Act / Assert: the limit itself is accepted in both modes
        Assert.Equal("0123456789", Text(virtualResolver.Resolve("ten.txt")));
        Assert.Equal("0123456789", Text(fileResolver.Resolve("ten.txt")));

        // Assert: one byte more is refused in both modes
        foreach (var resolver in new[] { virtualResolver, fileResolver })
        {
            var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("eleven.txt"));
            Assert.Equal(SandboxFailureKind.TooLarge, ex.Kind);
        }
    }

    /// <summary>
    ///     Validates that a stream that never ends is cut off at the byte limit plus one chunk, not read to its end.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_EndlessStreamStopsAtBound()
    {
        // Arrange
        var endless = new SandboxEndlessStream();
        var resolver = Create(new SandboxOptions { FileResolver = _ => endless, MaxFileBytes = 20_000 });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("forever.txt"));

        // Assert: refused for size after reading at most the limit plus one chunk of 8 KiB
        Assert.Equal(SandboxFailureKind.TooLarge, ex.Kind);
        Assert.InRange(endless.BytesRead, 20_001, 20_000 + 8192);
    }

    /// <summary>
    ///     Validates that a token that is already canceled stops the resolution before any request or file access.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_CanceledTokenStopsBeforeAnyRequest()
    {
        // Arrange
        var files = new SandboxVirtualFiles().With("a.txt", "x");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var resolver = new SandboxedFileResolver(new SandboxOptions { FileResolver = files.Resolve }, source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => resolver.Resolve("a.txt"));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a stream that fails while it is read is a <c>CannotRead</c> failure and that the stream is
    ///     disposed.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_CallbackStreamReadFailureIsCannotRead()
    {
        // Arrange
        var stream = new FailingStream();
        var resolver = Create(new SandboxOptions { FileResolver = _ => stream });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
        Assert.Equal(1, stream.DisposeCount);
    }

    /// <summary>
    ///     Validates that a callback that throws is a <c>CannotRead</c> failure (keeping the cause as the inner
    ///     exception but not copying it into the message), while a cancellation raised by the callback passes through
    ///     unchanged.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_CallbackFailureIsCannotReadAndCancellationPassesThrough()
    {
        // Arrange
        var throwing = Create(new SandboxOptions { FileResolver = _ => throw new InvalidOperationException("callback") });
        var canceling = Create(new SandboxOptions { FileResolver = _ => throw new OperationCanceledException() });

        // Act
        var ex = Assert.Throws<SandboxException>(() => throwing.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.DoesNotContain("callback", ex.Message, StringComparison.Ordinal);
        Assert.Throws<OperationCanceledException>(() => canceling.Resolve("a.txt"));
    }

    /// <summary>
    ///     Validates that a stream whose disposal fails after a complete read is a <c>CannotRead</c> failure, and that
    ///     the stream was disposed exactly once.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_StreamDisposeFailureIsCannotRead()
    {
        // Arrange
        var stream = new FailingDisposeStream();
        var resolver = Create(new SandboxOptions { FileResolver = _ => stream });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
        Assert.Equal(1, stream.DisposeCount);
    }

    /// <summary>
    ///     Validates that invalid construction arguments are rejected immediately.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Constructor_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new SandboxedFileResolver(null!));
        Assert.Throws<ArgumentNullException>(() => new SandboxedFileResolver(new SandboxOptions { SearchPaths = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxedFileResolver(new SandboxOptions { MaxFileBytes = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SandboxedFileResolver(new SandboxOptions { MaxFileBytes = -1 }));
    }

    /// <summary>
    ///     A stream whose reads fail with an I/O error.
    /// </summary>
    private sealed class FailingStream : MemoryStream
    {
        /// <summary>Gets the number of times the stream was disposed.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("broken");

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    ///     A readable stream whose disposal fails with an I/O error.
    /// </summary>
    private sealed class FailingDisposeStream : MemoryStream
    {
        /// <summary>Initializes the stream with a small, valid file.</summary>
        public FailingDisposeStream()
            : base([(byte)'x'])
        {
        }

        /// <summary>Gets the number of times the stream was disposed.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
            throw new IOException("dispose failed");
        }
    }
}
