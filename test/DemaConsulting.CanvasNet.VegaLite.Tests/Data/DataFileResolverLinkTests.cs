using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for the link rule of <see cref="DemaConsulting.CanvasNet.VegaLite.Data.DataFileResolver"/>: a file that
///     is, or lies beneath, a symbolic link or junction below its root is rejected whatever the link's target. The walk is
///     proven everywhere with an injected link probe over real temporary files; the tests that create real links skip
///     themselves only where the host cannot create them.
/// </summary>
public class DataFileResolverLinkTests
{
    /// <summary>The location the tests pass for the data path.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data/url", 3, 9);

    /// <summary>Bytes that are not valid UTF-8, so a test notices when a rejected file was read after all.</summary>
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
    /// <param name="dataPaths">Additional data paths.</param>
    /// <returns>The resolver.</returns>
    private static DataFileResolver Create(string baseDirectory, FakeLinks probe, params string[] dataPaths) =>
        new(new VegaLiteOpenOptions { BaseDirectory = baseDirectory, DataPaths = dataPaths }, VegaLiteLimits.Default, Ct, probe.IsLink);

    /// <summary>
    ///     Validates that a linked directory between the root and the file is rejected before the file is opened, at any
    ///     depth, and that the walk stops at the first link.
    /// </summary>
    /// <param name="depth">The depth of the linked directory (1 is directly below the root).</param>
    /// <param name="linked">The linked directory relative to the root.</param>
    [Theory]
    [InlineData(1, "d1")]
    [InlineData(2, "d1/d2")]
    [InlineData(3, "d1/d2/d3")]
    public void DataFileResolver_Load_LinkedDirectoryIsRejectedBeforeOpen(int depth, string linked)
    {
        // Arrange: the file is unreadable bytes, so reading it would give a different error than the link rejection
        using var directory = new DataTempDirectory();
        directory.Write("root/d1/d2/d3/x.csv", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/" + linked));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("d1/d2/d3/x.csv", Where));

        // Assert: a link failure naming the linked component, never a decoding failure; nothing below the link was probed
        Assert.Contains("passes through a link", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"('{linked}')", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("UTF-8", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
        Assert.Equal(depth, probe.Calls.Count);
    }

    /// <summary>
    ///     Validates that a file that is itself a link is rejected before it is opened, whatever its target, while its
    ///     neighbor that is not a link is read.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_LinkedFileIsRejectedBeforeOpen()
    {
        // Arrange: the "link" (reported by the probe) is an ordinary file that could not be decoded if it were read
        using var directory = new DataTempDirectory();
        directory.Write("root/real.csv", "r");
        directory.Write("root/alias.csv", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/alias.csv"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("alias.csv", Where));
        var real = resolver.Load("real.csv", Where);

        // Assert
        Assert.Contains("passes through a link", ex.Message, StringComparison.Ordinal);
        Assert.Contains("('alias.csv')", ex.Message, StringComparison.Ordinal);
        Assert.Equal("r", real.Text);
    }

    /// <summary>
    ///     Validates that a link below a data path root is rejected too.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_LinkBelowDataPathIsRejected()
    {
        // Arrange: the base directory has no such file, the data path has it below a link
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        directory.Write("lib/shared/x.csv", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("lib/shared"));
        var resolver = Create(directory.Combine("base"), probe, directory.Combine("lib"));

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("shared/x.csv", Where));

        // Assert
        Assert.Contains("('shared')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the root and everything above it are trusted, even when the probe reports them as links, and that
    ///     only the file itself is examined.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_LinkedRootAndAncestorsAreTrusted()
    {
        // Arrange
        using var directory = new DataTempDirectory();
        directory.Write("parent/root/ok.csv", "ok");
        var root = directory.Combine("parent/root");
        var probe = new FakeLinks().With(root, directory.Combine("parent"), directory.Path);
        var resolver = Create(root, probe);

        // Act
        var file = resolver.Load("ok.csv", Where);

        // Assert
        Assert.Equal("ok", file.Text);
        Assert.Equal([Path.GetFullPath(Path.Combine(root, "ok.csv"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that the first missing component ends the walk: the file is missing, nothing below it is probed and
    ///     no link is reported even if the path below it would be one.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_LinkAfterMissingComponentIsNotProbedAndFileIsMissing()
    {
        // Arrange
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/dir"));
        var probe = new FakeLinks().With(directory.Combine("root/dir/gone"), directory.Combine("root/dir/gone/x.csv"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("dir/gone/x.csv", Where));

        // Assert: reported missing (not as a link), and only the existing directory was probed
        Assert.Contains("was not found", ex.Message, StringComparison.Ordinal);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/dir"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that a route through dot-dot is judged on the normalized path: the directory that is stepped over is
    ///     not examined, the one that is entered is.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_DotDotRouteThroughLinkIsJudgedOnNormalizedPath()
    {
        // Arrange
        using var directory = new DataTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/a"));
        directory.Write("root/lib/x.csv", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/lib"));
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("a/../lib/x.csv", Where));

        // Assert
        Assert.Contains("('lib')", ex.Message, StringComparison.Ordinal);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/lib"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that a probe that cannot answer rejects the file (fail closed) instead of reading it, and that resolver
    ///     mode never asks the probe.
    /// </summary>
    /// <param name="denied"><see langword="true"/> to throw an access error, <see langword="false"/> an I/O error.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataFileResolver_Load_ReparseProbeFailureFailsClosed(bool denied)
    {
        // Arrange
        using var directory = new DataTempDirectory();
        directory.Write("root/a.csv", "a");
        var probe = new FakeLinks { Failure = denied ? new UnauthorizedAccessException("denied") : new IOException("broken") };
        var resolver = Create(directory.Combine("root"), probe);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("a.csv", Where));

        // Assert
        Assert.Contains("cannot be checked for links", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);

        // Assert: in resolver mode no file system access happens, so the probe is never asked
        var files = new VirtualDataFiles().With("a.csv", "virtual");
        var failing = new FakeLinks { Failure = new IOException("must not be asked") };
        var viaCallback = new DataFileResolver(new VegaLiteOpenOptions { FileResolver = files.Resolve }, VegaLiteLimits.Default, Ct, failing.IsLink);
        Assert.Equal("virtual", viaCallback.Load("a.csv", Where).Text);
        Assert.Empty(failing.Calls);
    }

    /// <summary>
    ///     Validates with real links that a directory link, a directory link inside the root, a file link and (on
    ///     Windows, where a symbolic link needs a privilege) a junction are rejected, that a base directory reached through
    ///     a link is trusted, and that the real file beside them is read. The test skips only when the host cannot create
    ///     the links.
    /// </summary>
    [Fact]
    public void DataFileResolver_Load_RealLinksAreRejectedOrCapabilitySkipped()
    {
        // Arrange: a real file inside the root, a target outside the root, and the links to them
        using var directory = new DataTempDirectory();
        directory.Write("outside/target.csv", "outside");
        directory.Write("root/real/x.csv", "real");
        var outsideFile = directory.Write("outside/file.csv", "outside file");
        if (!DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/escape"), directory.Combine("outside"), out var reason))
        {
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }

        Assert.True(DataLinkSupport.TryCreateDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"), out reason), reason);
        var canLinkFiles = DataLinkSupport.TryCreateFileLink(directory.Combine("root/file-link.csv"), outsideFile, out _);
        var resolver = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") }, VegaLiteLimits.Default, Ct);

        // Act / Assert: a link that leaves the root and one that stays inside it are both rejected
        var escaping = Assert.Throws<VegaLiteRenderException>(() => resolver.Load("escape/target.csv", Where));
        Assert.Contains("passes through a link", escaping.Message, StringComparison.Ordinal);
        Assert.Throws<VegaLiteRenderException>(() => resolver.Load("alias/x.csv", Where));

        // Assert: a file link is rejected whatever its target (only where the host can create file links)
        if (canLinkFiles)
        {
            Assert.Throws<VegaLiteRenderException>(() => resolver.Load("file-link.csv", Where));
        }

        // Assert: the real directory itself works, and so does a base directory that is itself reached through a link
        Assert.Equal("real", resolver.Load("real/x.csv", Where).Text);
        var viaLink = new DataFileResolver(new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root/alias") }, VegaLiteLimits.Default, Ct);
        Assert.Equal("real", viaLink.Load("x.csv", Where).Text);
    }
}
