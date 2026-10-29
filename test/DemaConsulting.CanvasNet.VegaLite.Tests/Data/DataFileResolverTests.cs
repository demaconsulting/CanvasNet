using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for <see cref="DemaConsulting.CanvasNet.VegaLite.Data.DataFileResolver"/>: the search order, the path
///     rules of the sandbox, resolver-callback mode, the per-call cache and the limits and strict decoding of reads. The
///     link rule has its own class, <see cref="DataFileResolverLinkTests"/>.
/// </summary>
public class DataFileResolverTests
{
    /// <summary>The location the tests pass for the data path, so that errors can be checked for carrying it.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data/url", 3, 9);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates a resolver over open options.
    /// </summary>
    /// <param name="options">The open options.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The resolver.</returns>
    private static DataFileResolver Create(VegaLiteOpenOptions options, VegaLiteLimits? limits = null) =>
        new(options, limits ?? VegaLiteLimits.Default, Ct);

    /// <summary>
    ///     Validates that a path is found under the base directory first and then under each data path in order, and that
    ///     the result carries the decoded text and the path as written.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RelativePathUsesBaseDirectoryThenDataPaths()
    {
        // Arrange: the same name in the base directory and in the first data path, another name only in the second
        using var directory = new DataTempDirectory();
        directory.Write("base/a.csv", "from base");
        directory.Write("one/a.csv", "from one");
        directory.Write("one/b.csv", "b from one");
        directory.Write("two/b.csv", "b from two");
        directory.Write("two/c.csv", "c from two");
        var resolver = Create(new VegaLiteOpenOptions
        {
            BaseDirectory = directory.Combine("base"),
            DataPaths = [directory.Combine("one"), directory.Combine("two")],
        });

        // Act
        var a = resolver.Load("a.csv", Where);
        var b = resolver.Load("b.csv", Where);
        var c = resolver.Load("c.csv", Where);

        // Assert: the first root that has the file wins, and the display name is the path as written
        Assert.Equal("from base", a.Text);
        Assert.Equal("b from one", b.Text);
        Assert.Equal("c from two", c.Text);
        Assert.Equal("a.csv", a.Display);
        Assert.True(resolver.HasFileSource);
    }

    /// <summary>
    ///     Validates that <c>.</c> and <c>..</c> inside the root are normalized (also with backslashes) and that a
    ///     <c>..</c> that leaves the root is a sandbox violation, even when the target exists.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_DotDotInsideRootNormalizesAndEscapeIsRejected()
    {
        // Arrange: a file inside the root and a file beside the root that must stay out of reach
        using var directory = new DataTempDirectory();
        directory.Write("root/sub/a.csv", "inside");
        directory.Write("outside.csv", "outside");
        var resolver = Create(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var normalized = resolver.Load("sub/../sub/./a.csv", Where);
        var backslashes = resolver.Load("sub\\..\\sub\\a.csv", Where);
        var escape = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("../outside.csv", Where));
        var deepEscape = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("sub/../../outside.csv", Where));

        // Assert
        Assert.Equal("inside", normalized.Text);
        Assert.Equal("inside", backslashes.Text);
        Assert.Contains("outside the allowed directories", escape.Message, StringComparison.Ordinal);
        Assert.Contains("outside the allowed directories", deepEscape.Message, StringComparison.Ordinal);
        Assert.Equal(Where, escape.Location);
        Assert.StartsWith("spec.vl.json:3:9 /data/url: ", escape.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a path text that is empty, absolute, rooted, a drive or UNC path, or contains a colon or a control
    ///     character is rejected before any file system access or resolver call.
    /// </summary>
    /// <param name="path">The hostile path text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/etc/hosts")]
    [InlineData("\\windows\\system32\\a.csv")]
    [InlineData("\\\\server\\share\\a.csv")]
    [InlineData("//server/share/a.csv")]
    [InlineData("C:\\data\\a.csv")]
    [InlineData("C:a.csv")]
    [InlineData("dir/a:b.csv")]
    [InlineData("a\u0000b.csv")]
    [InlineData("a\nb.csv")]
    [InlineData("a\u007Fb.csv")]
    [InlineData("a\u0085b.csv")]
    [InlineData("a\u009Fb.csv")]
    public void DataFileResolver_Load_HostilePathTextIsRejectedWithoutAnyAccess(string path)
    {
        // Arrange: a resolver callback that records requests (resolver mode never touches the file system either)
        var files = new VirtualDataFiles().With("a.csv", "x");
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load(path, Where));

        // Assert: refused as a sandbox violation, carrying the location, and no request was made
        Assert.Contains("is not allowed", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates resolver mode: the callback receives normalized logical paths (the root first, then each valid data
    ///     path as a prefix), the base directory and the file system play no role, streams are disposed and requests are
    ///     made on the calling thread.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_ResolverModeUsesNormalizedLogicalPathsWithoutFileSystem()
    {
        // Arrange: a base directory with a file of the same name that must never be read, and a mix of valid and invalid data paths
        using var directory = new DataTempDirectory();
        directory.Write("a/b.csv", "from the file system");
        var files = new VirtualDataFiles()
            .With("a/b.csv", "virtual b")
            .With("lib/x.csv", "virtual x")
            .With("lib/deep/y.csv", "virtual y");
        var resolver = Create(new VegaLiteOpenOptions
        {
            BaseDirectory = directory.Path,
            DataPaths = ["/absolute", "C:\\drive", "lib", "lib\\deep"],
            FileResolver = files.Resolve,
        });

        // Act
        var b = resolver.Load("a/./c/../b.csv", Where);
        var x = resolver.Load("x.csv", Where);
        var y = resolver.Load("y.csv", Where);

        // Assert: normalized logical paths, root first and then the valid prefixes in declaration order
        Assert.Equal("virtual b", b.Text);
        Assert.Equal("virtual x", x.Text);
        Assert.Equal("virtual y", y.Text);
        Assert.Equal(["a/b.csv", "x.csv", "lib/x.csv", "y.csv", "lib/y.csv", "lib/deep/y.csv"], files.Requests);
        Assert.All(files.Streams, s => Assert.True(s.IsDisposed));
        Assert.All(files.Threads, t => Assert.Equal(Environment.CurrentManagedThreadId, t));

        // Assert: an escape above the logical root is refused before the callback is asked
        var before = files.Requests.Count;
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("../x.csv", Where));
        Assert.Equal(before, files.Requests.Count);
    }

    /// <summary>
    ///     Validates that without a base directory, data path or resolver no file can be found and the failure says so.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_StreamWithoutFileSourceThrows()
    {
        // Arrange: the options a document opened from a string or a stream has
        var resolver = Create(new VegaLiteOpenOptions());

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("a.csv", Where));

        // Assert
        Assert.False(resolver.HasFileSource);
        Assert.Contains("no base directory, data path or file resolver", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
    }

    /// <summary>
    ///     Validates that a file that does not exist is a failure with the location, never an empty result.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_MissingFileIsHardFailure()
    {
        // Arrange: an existing base directory without the requested file
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        var resolver = Create(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") });
        var callback = new VirtualDataFiles();
        var resolverMode = Create(new VegaLiteOpenOptions { FileResolver = callback.Resolve });

        // Act
        var missing = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("nope.csv", Where));
        var virtualMissing = Assert.Throws<VegaLiteRenderException>(() => resolverMode.Load("nope.csv", Where));

        // Assert
        Assert.Contains("'nope.csv' was not found", missing.Message, StringComparison.Ordinal);
        Assert.Contains("'nope.csv' was not found", virtualMissing.Message, StringComparison.Ordinal);
        Assert.Equal(Where, missing.Location);
        Assert.DoesNotContain(directory.Path, missing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an existing directory where a file is expected is a located read failure (it exists but cannot
    ///     be read as data), not a missing file, and that the message does not reveal the host path.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_ExistingDirectoryAtFilePathIsLocatedReadFailure()
    {
        // Arrange: a sub folder named like the requested file
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/folder.csv"));
        var resolver = Create(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("folder.csv", Where));

        // Assert
        Assert.Contains("'folder.csv' cannot be read", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an existing file whose open fails (an access or I/O failure) is a located read failure, never a
    ///     missing file, and that no later root is tried. The opener is injected, so the test needs no file permission
    ///     or lock and behaves the same on every platform.
    /// </summary>
    /// <param name="denied">Whether the open fails with an access failure (otherwise an I/O failure).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DataFileResolver_Load_ExistingFileOpenFailureIsLocatedReadFailure(bool denied)
    {
        // Arrange: the file exists in the base directory and in the second root; the open of every file fails
        using var directory = new DataTempDirectory();
        directory.Write("base/a.csv", "from base");
        directory.Write("two/a.csv", "from two");
        var opened = new List<string>();
        var resolver = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("base"), DataPaths = [directory.Combine("two")] },
            VegaLiteLimits.Default,
            Ct,
            openFile: path =>
            {
                opened.Add(path);
                throw denied ? new UnauthorizedAccessException("denied") : new IOException("locked");
            });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("a.csv", Where));

        // Assert: unreadable rather than missing, located, without the host path, and the second root was never opened
        Assert.Contains("'a.csv' cannot be read", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not found", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.csv"))], opened);
    }

    /// <summary>
    ///     Validates that a file that is reported missing by the open itself (it vanished, or the existence probe could
    ///     not see it) is a missing candidate: the next root is tried, in base-directory-then-data-path order.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_FileNotFoundFromOpenIsMissingAndTriesNextRoot()
    {
        // Arrange: the file exists everywhere, but the open of the first root's file reports "not found" (a race)
        using var directory = new DataTempDirectory();
        directory.Write("base/a.csv", "from base");
        directory.Write("two/a.csv", "from two");
        var opened = new List<string>();
        var resolver = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("base"), DataPaths = [directory.Combine("two")] },
            VegaLiteLimits.Default,
            Ct,
            openFile: path =>
            {
                opened.Add(path);
                return path == Path.GetFullPath(directory.Combine("base/a.csv"))
                    ? throw new FileNotFoundException("gone")
                    : new MemoryStream(Encoding.UTF8.GetBytes("from two"));
            });
        var onlyMissing = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("base") },
            VegaLiteLimits.Default,
            Ct,
            openFile: _ => throw new DirectoryNotFoundException("gone"));

        // Act
        var file = resolver.Load("a.csv", Where);
        var missing = Assert.Throws<VegaLiteRenderException>(() => onlyMissing.Load("a.csv", Where));

        // Assert: the next root supplied the file after the first was tried; with no other root it is "not found"
        Assert.Equal("from two", file.Text);
        Assert.Equal([Path.GetFullPath(directory.Combine("base/a.csv")), Path.GetFullPath(directory.Combine("two/a.csv"))], opened);
        Assert.Contains("'a.csv' was not found", missing.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a final component that no existence probe can see is left to the open attempt: a real missing
    ///     file is still "not found", and the open is attempted rather than skipped.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_MissingFinalComponentIsDecidedByTheOpen()
    {
        // Arrange: an existing directory without the file; the opener records the attempt and delegates to the file system
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/sub"));
        var opened = new List<string>();
        var resolver = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") },
            VegaLiteLimits.Default,
            Ct,
            openFile: path =>
            {
                opened.Add(path);
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            });

        // Act
        var final = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("sub/none.csv", Where));
        var intermediate = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("absent/none.csv", Where));

        // Assert: both are "not found"; only the missing final component reached the open (a missing directory did not)
        Assert.Contains("'sub/none.csv' was not found", final.Message, StringComparison.Ordinal);
        Assert.Contains("'absent/none.csv' was not found", intermediate.Message, StringComparison.Ordinal);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/sub/none.csv"))], opened);
    }

    /// <summary>
    ///     Validates that each candidate is requested at most once per call: a file that is loaded again comes from the
    ///     cache and a miss is remembered too.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_CacheRequestsEachCandidateOncePerCall()
    {
        // Arrange
        var files = new VirtualDataFiles().With("a.csv", "x");
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve, DataPaths = ["lib"] });

        // Act
        var first = resolver.Load("a.csv", Where);
        var second = resolver.Load("a.csv", Where);
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("miss.csv", Where));
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("miss.csv", Where));

        // Assert: one request for the hit, one per candidate for the miss; the cache hands out the same file
        Assert.Same(first, second);
        Assert.Equal(["a.csv", "miss.csv", "lib/miss.csv"], files.Requests);

        // Assert: a new resolver (a new call) starts with an empty cache
        var next = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve });
        next.Load("a.csv", Where);
        Assert.Equal(["a.csv", "miss.csv", "lib/miss.csv", "a.csv"], files.Requests);
    }

    /// <summary>
    ///     Validates that every file system component is probed once however often files below it are loaded (the cache
    ///     also covers the file system mode).
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_FileSystemFilesAreCachedPerCall()
    {
        // Arrange: a probe that records its questions
        using var directory = new DataTempDirectory();
        directory.Write("root/d/a.csv", "a");
        var asked = new List<string>();
        var resolver = new DataFileResolver(
            new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") },
            VegaLiteLimits.Default,
            Ct,
            path =>
            {
                asked.Add(Path.GetRelativePath(directory.Combine("root"), path).Replace('\\', '/'));
                return false;
            });

        // Act
        var first = resolver.Load("d/a.csv", Where);
        File.WriteAllText(directory.Combine("root/d/a.csv"), "changed after the first read");
        var second = resolver.Load("d/a.csv", Where);

        // Assert: the second load did not read the file again and did not ask the probe again
        Assert.Equal("a", first.Text);
        Assert.Equal("a", second.Text);
        Assert.Equal(["d", "d/a.csv"], asked);
    }

    /// <summary>
    ///     Validates the data file limit: files up to the limit are read, a file that is already loaded does not count
    ///     again, a file that does not exist does not count, and the first file over the limit is refused.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_MaxDataFilesAtLimitAndOneOver()
    {
        // Arrange
        var files = new VirtualDataFiles().With("a.csv", "a").With("b.csv", "b").With("c.csv", "c");
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve }, new VegaLiteLimits { MaxDataFiles = 2 });

        // Act: exactly the limit, and one of them again
        resolver.Load("a.csv", Where);
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("missing.csv", Where));
        resolver.Load("b.csv", Where);
        resolver.Load("a.csv", Where);
        var over = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("c.csv", Where));

        // Assert: the message names the limit in the form the Tool recognizes, and carries the location
        Assert.Contains("the limit of 2 data files (MaxDataFiles)", over.Message, StringComparison.Ordinal);
        Assert.Equal(Where, over.Location);
    }

    /// <summary>
    ///     Validates the byte limit for the resolver callback and for real files: exactly the limit is accepted and one
    ///     byte more is refused.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_MaxDataBytesAtLimitAndOneOver()
    {
        // Arrange: files of exactly 10 and 11 bytes, as virtual files and as real files
        var limits = new VegaLiteLimits { MaxDataBytes = 10 };
        var files = new VirtualDataFiles().With("ten.csv", "0123456789").With("eleven.csv", "0123456789X");
        var virtualResolver = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve }, limits);
        using var directory = new DataTempDirectory();
        directory.Write("ten.csv", "0123456789");
        directory.Write("eleven.csv", "0123456789X");
        var fileResolver = Create(new VegaLiteOpenOptions { BaseDirectory = directory.Path }, limits);

        // Act / Assert: the limit itself is accepted in both modes
        Assert.Equal("0123456789", virtualResolver.Load("ten.csv", Where).Text);
        Assert.Equal("0123456789", fileResolver.Load("ten.csv", Where).Text);

        // Assert: one byte more is refused in both modes, with the limit named as the Tool recognizes it
        foreach (var resolver in new[] { virtualResolver, fileResolver })
        {
            var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("eleven.csv", Where));
            Assert.Contains("the limit of 10 bytes (MaxDataBytes)", ex.Message, StringComparison.Ordinal);
            Assert.Equal(Where, ex.Location);
        }
    }

    /// <summary>
    ///     Validates that invalid UTF-8 is rejected, never replaced, that UTF-16 text is not UTF-8, and that a UTF-8
    ///     byte-order mark is removed.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_InvalidUtf8IsRejectedStrictly()
    {
        // Arrange: a stray byte, a truncated sequence, UTF-16 text with its byte-order mark, and a BOM-prefixed UTF-8 text
        var files = new VirtualDataFiles()
            .With("stray.csv", new byte[] { 0x61, 0xFF, 0x62 })
            .With("truncated.csv", new byte[] { 0x61, 0xE2, 0x82 })
            .With("utf16.csv", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("a,b")).ToArray())
            .With("bom.csv", Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("\u00E9x")).ToArray());
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = files.Resolve });

        // Act / Assert: each invalid file is a located failure that names the problem
        foreach (var name in new[] { "stray.csv", "truncated.csv", "utf16.csv" })
        {
            var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load(name, Where));
            Assert.Contains("is not valid UTF-8", ex.Message, StringComparison.Ordinal);
            Assert.Equal(Where, ex.Location);
        }

        // Assert: valid UTF-8 keeps its characters and loses only the byte-order mark
        Assert.Equal("\u00E9x", resolver.Load("bom.csv", Where).Text);
    }

    /// <summary>
    ///     Validates that a stream that never ends is cut off at the byte limit plus one chunk, not read to its end.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_EndlessStreamStopsAtBound()
    {
        // Arrange
        var endless = new EndlessDataStream();
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = _ => endless }, new VegaLiteLimits { MaxDataBytes = 20_000 });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("forever.csv", Where));

        // Assert: refused for size after reading at most the limit plus one chunk of 8 KiB
        Assert.Contains("MaxDataBytes", ex.Message, StringComparison.Ordinal);
        Assert.InRange(endless.BytesRead, 20_001, 20_000 + 8192);
    }

    /// <summary>
    ///     Validates that a token that is already canceled stops the load before any request or file access.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_CanceledTokenStopsBeforeAnyRequest()
    {
        // Arrange
        var files = new VirtualDataFiles().With("a.csv", "x");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var resolver = new DataFileResolver(new VegaLiteOpenOptions { FileResolver = files.Resolve }, VegaLiteLimits.Default, source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => resolver.Load("a.csv", Where));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a stream that fails while it is read is a located read failure and that the stream is disposed.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_CallbackStreamReadFailureIsLocated()
    {
        // Arrange
        var stream = new FailingStream();
        var broken = Create(new VegaLiteOpenOptions { FileResolver = _ => stream });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => broken.Load("a.csv", Where));

        // Assert
        Assert.Contains("'a.csv' cannot be read", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Equal(1, stream.DisposeCount);
    }

    /// <summary>
    ///     Validates that a callback that throws is a located read failure, while a cancellation raised by the callback
    ///     passes through unchanged.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_CallbackFailureIsLocatedReadFailure()
    {
        // Arrange
        var throwing = Create(new VegaLiteOpenOptions { FileResolver = _ => throw new InvalidOperationException("callback") });
        var canceling = Create(new VegaLiteOpenOptions { FileResolver = _ => throw new OperationCanceledException() });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => throwing.Load("a.csv", Where));

        // Assert: the failure is located and does not leak the callback's own text; cancellation is never rewritten
        Assert.Contains("'a.csv' cannot be read", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("callback", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Throws<OperationCanceledException>(() => canceling.Load("a.csv", Where));
    }

    /// <summary>
    ///     Validates that a stream whose disposal fails after a complete read is a located read failure, and that the
    ///     stream was disposed exactly once.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_StreamDisposeFailureIsLocatedReadFailure()
    {
        // Arrange
        var stream = new FailingDisposeStream();
        var resolver = Create(new VegaLiteOpenOptions { FileResolver = _ => stream });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("a.csv", Where));

        // Assert
        Assert.Contains("'a.csv' cannot be read", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Equal(1, stream.DisposeCount);
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
            : base([(byte)'x']) { }

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
