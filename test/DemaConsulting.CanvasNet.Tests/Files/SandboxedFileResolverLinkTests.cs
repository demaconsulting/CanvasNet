using System.Text;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for the link rule of <see cref="SandboxedFileResolver"/>: a file that is, or lies beneath, a symbolic
///     link or junction below its root is rejected whatever the link's target. The walk is proven everywhere with an
///     injected link probe over real temporary files; the tests that create real links skip themselves only where the
///     host cannot create them.
/// </summary>
public class SandboxedFileResolverLinkTests
{
    /// <summary>Bytes that are not valid text in any encoding the tests use, so a test notices when a rejected file was read after all.</summary>
    private static readonly byte[] Unreadable = [0xFF, 0xFE, 0xFD];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     A link probe that reports a chosen set of full paths as links and records every question.
    /// </summary>
    private sealed class FakeLinks
    {
        /// <summary>The full paths reported as links.</summary>
        private readonly HashSet<string> _links = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The paths the probe was asked about, in order.</summary>
        private readonly List<string> _calls = [];

        /// <summary>Gets the paths the probe was asked about.</summary>
        public IReadOnlyList<string> Calls => _calls;

        /// <summary>Gets or sets the exception thrown by every question, or <see langword="null"/>.</summary>
        public Exception? Failure { get; set; }

        /// <summary>
        ///     Marks full paths as links.
        /// </summary>
        /// <param name="paths">The full paths.</param>
        /// <returns>This probe.</returns>
        public FakeLinks With(params string[] paths)
        {
            foreach (var path in paths)
            {
                _links.Add(Path.GetFullPath(path));
            }

            return this;
        }

        /// <summary>
        ///     The probe: records the question and answers from the configured set.
        /// </summary>
        /// <param name="path">The full path asked about.</param>
        /// <returns><see langword="true"/> when the path was marked as a link.</returns>
        public bool IsLink(string path)
        {
            _calls.Add(path);
            return Failure is null ? _links.Contains(Path.GetFullPath(path)) : throw Failure;
        }
    }

    /// <summary>
    ///     Creates a resolver over a base directory with an injected probe.
    /// </summary>
    /// <param name="baseDirectory">The base directory.</param>
    /// <param name="probe">The probe.</param>
    /// <param name="searchPaths">Additional search paths.</param>
    /// <returns>The resolver.</returns>
    private static SandboxedFileResolver Create(string baseDirectory, FakeLinks probe, params string[] searchPaths) =>
        new(new SandboxOptions { BaseDirectory = baseDirectory, SearchPaths = searchPaths }, Ct, probe.IsLink, null);

    /// <summary>
    ///     Validates that a linked directory between the root and the file is rejected before the file is opened, at any
    ///     depth, naming the linked component relative to the root, and that the walk stops at the first link.
    /// </summary>
    /// <param name="depth">The depth of the linked directory (1 is directly below the root).</param>
    /// <param name="linked">The linked directory relative to the root.</param>
    [Theory]
    [InlineData(1, "d1")]
    [InlineData(2, "d1/d2")]
    [InlineData(3, "d1/d2/d3")]
    public void SandboxedFileResolver_Resolve_LinkedDirectoryIsRejectedBeforeOpen(int depth, string linked)
    {
        // Arrange: the file is unreadable bytes, so reading it would give a different outcome than the link rejection
        using var directory = new SandboxTempDirectory();
        directory.Write("root/d1/d2/d3/x.txt", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/" + linked));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("d1/d2/d3/x.txt"));

        // Assert: a link failure naming the linked component, with no host path in the message; nothing below the link was probed
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal(linked, ex.Detail);
        Assert.Contains($"('{linked}')", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(directory.Combine("root/d1/d2/d3/x.txt")), ex.Path);
        Assert.Equal(depth, probe.Calls.Count);
    }

    /// <summary>
    ///     Validates that a file that is itself a link is rejected before it is opened, whatever its target, while its
    ///     neighbor that is not a link is read.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_LinkedFileIsRejectedBeforeOpen()
    {
        // Arrange: the "link" (reported by the probe) is an ordinary file that could not be decoded if it were read
        using var directory = new SandboxTempDirectory();
        directory.Write("root/real.txt", "r");
        directory.Write("root/alias.txt", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/alias.txt"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("alias.txt"));
        var real = resolver.Resolve("real.txt");

        // Assert
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal("alias.txt", ex.Detail);
        Assert.Equal("r", Encoding.UTF8.GetString(real!.Content.Span));
    }

    /// <summary>
    ///     Validates that a file read through one root is never served for another root: a file first resolved through a
    ///     trusted search path that is itself a link must still be refused when it is reached again through the base
    ///     directory, whose link checks run on the components below that root.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_FileCachedUnderLinkedSearchRootIsCheckedAgainForAnotherRoot()
    {
        // Arrange: "alias" is a link and also a (trusted) search path; the file is first found through that search path
        using var directory = new SandboxTempDirectory();
        directory.Write("root/alias/x.txt", "x");
        directory.Write("root/sub/f.txt", "f");
        var probe = new FakeLinks().With(directory.Combine("root/alias"));
        var resolver = Create(directory.Combine("root"), probe, directory.Combine("root/alias"));
        var viaSearchPath = resolver.Resolve("x.txt");
        var referrer = resolver.Resolve("sub/f.txt");

        // Act: the same file again, now through the base directory root, which makes "alias" a link below that root
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("../alias/x.txt", referrer));

        // Assert: the first resolution was legitimate; the second must not be answered from the cache
        Assert.Equal("x", Encoding.UTF8.GetString(viaSearchPath!.Content.Span));
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal("alias", ex.Detail);
    }

    /// <summary>
    ///     Validates that a link below a search path root is rejected too (the base directory has no such file).
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_LinkBelowSearchPathIsRejected()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        directory.Write("lib/shared/x.txt", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("lib/shared"));
        var resolver = Create(directory.Combine("base"), probe, directory.Combine("lib"));

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("shared/x.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal("shared", ex.Detail);
    }

    /// <summary>
    ///     Validates that the root and everything above it are trusted, even when the probe reports them as links, and
    ///     that only the file itself is examined.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_LinkedRootAndAncestorsAreTrusted()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("parent/root/ok.txt", "ok");
        var root = directory.Combine("parent/root");
        var probe = new FakeLinks().With(root, directory.Combine("parent"), directory.Path);
        var resolver = Create(root, probe);

        // Act
        var file = resolver.Resolve("ok.txt");

        // Assert
        Assert.Equal("ok", Encoding.UTF8.GetString(file!.Content.Span));
        Assert.Equal([Path.GetFullPath(Path.Combine(root, "ok.txt"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that the first missing component ends the walk: the file is missing, nothing below it is probed and
    ///     no link is reported even if the path below it would be one.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_LinkAfterMissingComponentIsNotProbedAndFileIsMissing()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/dir"));
        var probe = new FakeLinks().With(directory.Combine("root/dir/gone"), directory.Combine("root/dir/gone/x.txt"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var file = resolver.Resolve("dir/gone/x.txt");

        // Assert: reported missing (not as a link), and only the existing directory was probed
        Assert.Null(file);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/dir"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that a final component that an existence check calls missing is still probed for a link (a dangling
    ///     link is refused, not opened or reported missing), and that a file that is truly missing remains missing.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_MissingFinalComponentIsProbedAndDanglingLinkIsRejected()
    {
        // Arrange: nothing exists at either path; the probe says the first one is a (dangling) link
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/dir"));
        var probe = new FakeLinks().With(directory.Combine("root/dir/dangling.txt"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var dangling = Assert.Throws<SandboxException>(() => resolver.Resolve("dir/dangling.txt"));
        var missing = resolver.Resolve("dir/gone.txt");

        // Assert: the link is refused as a link; the plain missing file is missing; both were probed
        Assert.Equal(SandboxFailureKind.ThroughLink, dangling.Kind);
        Assert.Equal("dir/dangling.txt", dangling.Detail);
        Assert.Null(missing);
        Assert.Equal(
            [
                Path.GetFullPath(directory.Combine("root/dir")),
                Path.GetFullPath(directory.Combine("root/dir/dangling.txt")),
                Path.GetFullPath(directory.Combine("root/dir/gone.txt")),
            ],
            probe.Calls);
    }

    /// <summary>
    ///     Validates that every existing component is probed exactly once however many files go through it.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_EveryComponentProbedOnceAndCached()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/d/e/a.txt", "a");
        directory.Write("root/d/e/b.txt", "b");
        var probe = new FakeLinks();
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        resolver.Resolve("d/e/a.txt");
        resolver.Resolve("d/e/b.txt");
        resolver.Resolve("d/e/a.txt");

        // Assert
        Assert.Equal(
            ["d", "d/e", "d/e/a.txt", "d/e/b.txt"],
            probe.Calls.Select(c => Path.GetRelativePath(directory.Combine("root"), c).Replace('\\', '/')).ToArray());
    }

    /// <summary>
    ///     Validates that a route through dot-dot is judged on the normalized path: the directory that is stepped over is
    ///     not examined, the one that is entered is.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_DotDotRouteThroughLinkIsJudgedOnNormalizedPath()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/a"));
        directory.Write("root/lib/x.txt", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/lib"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a/../lib/x.txt"));

        // Assert
        Assert.Equal("lib", ex.Detail);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/lib"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that a probe that cannot answer rejects the file (fail closed) instead of reading it, keeping the
    ///     cause, and that resolver mode never asks the probe.
    /// </summary>
    /// <param name="denied"><see langword="true"/> to throw an access error, <see langword="false"/> an I/O error.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SandboxedFileResolver_Resolve_ProbeFailureFailsClosed(bool denied)
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "a");
        var probe = new FakeLinks { Failure = denied ? new UnauthorizedAccessException("denied") : new IOException("broken") };
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.LinkCheckFailed, ex.Kind);
        Assert.NotNull(ex.InnerException);

        // Assert: in resolver mode no file system access happens, so the probe is never asked
        var files = new SandboxVirtualFiles().With("a.txt", "virtual");
        var failing = new FakeLinks { Failure = new IOException("must not be asked") };
        var viaCallback = new SandboxedFileResolver(new SandboxOptions { FileResolver = files.Resolve }, Ct, failing.IsLink, null);
        Assert.Equal("virtual", Encoding.UTF8.GetString(viaCallback.Resolve("a.txt")!.Content.Span));
        Assert.Empty(failing.Calls);
    }

    /// <summary>
    ///     Validates with a real dangling symbolic link that it is rejected as a link while a truly missing file is
    ///     missing. The test skips only when the host cannot create file links.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RealDanglingLinkIsRejectedOrCapabilitySkipped()
    {
        // Arrange: a link whose target does not exist
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        if (!SandboxLinkSupport.TryCreateFileLink(directory.Combine("root/dangling.txt"), directory.Combine("outside/nowhere.txt"), out var reason))
        {
            Assert.Skip("File links cannot be created in this environment: " + reason);
        }

        var resolver = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = directory.Combine("root") }, Ct);

        // Act
        var dangling = Assert.Throws<SandboxException>(() => resolver.Resolve("dangling.txt"));
        var missing = resolver.Resolve("gone.txt");

        // Assert
        Assert.Equal(SandboxFailureKind.ThroughLink, dangling.Kind);
        Assert.Null(missing);
    }

    /// <summary>
    ///     Validates with real links that a directory link, a directory link inside the root, a file link and (on
    ///     Windows, where a symbolic link needs a privilege) a junction are rejected, that a base directory reached
    ///     through a link is trusted, and that the real file beside them is read. The test skips only when the host
    ///     cannot create the links.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RealLinksAreRejectedOrCapabilitySkipped()
    {
        // Arrange: a real file inside the root, a target outside the root, and the links to them
        using var directory = new SandboxTempDirectory();
        directory.Write("outside/target.txt", "outside");
        directory.Write("root/real/x.txt", "real");
        var outsideFile = directory.Write("outside/file.txt", "outside file");
        if (!SandboxLinkSupport.TryCreateDirectoryLink(directory.Combine("root/escape"), directory.Combine("outside"), out var reason))
        {
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }

        Assert.True(SandboxLinkSupport.TryCreateDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"), out reason), reason);
        var canLinkFiles = SandboxLinkSupport.TryCreateFileLink(directory.Combine("root/file-link.txt"), outsideFile, out _);
        var resolver = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = directory.Combine("root") }, Ct);

        // Act / Assert: a link that leaves the root and one that stays inside it are both rejected
        var escaping = Assert.Throws<SandboxException>(() => resolver.Resolve("escape/target.txt"));
        Assert.Equal(SandboxFailureKind.ThroughLink, escaping.Kind);
        Assert.Equal("escape", escaping.Detail);
        Assert.Throws<SandboxException>(() => resolver.Resolve("alias/x.txt"));

        // Assert: a file link is rejected whatever its target (only where the host can create file links)
        if (canLinkFiles)
        {
            Assert.Throws<SandboxException>(() => resolver.Resolve("file-link.txt"));
        }

        // Assert: the real directory itself works, and so does a base directory that is itself reached through a link
        Assert.Equal("real", Encoding.UTF8.GetString(resolver.Resolve("real/x.txt")!.Content.Span));
        var viaLink = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = directory.Combine("root/alias") }, Ct);
        Assert.Equal("real", Encoding.UTF8.GetString(viaLink.Resolve("x.txt")!.Content.Span));
    }
}
