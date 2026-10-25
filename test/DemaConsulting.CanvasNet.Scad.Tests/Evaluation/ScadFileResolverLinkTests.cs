using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the link rule of <see cref="ScadFileResolver"/>: a file that is, or lies beneath, a symbolic
///     link or junction below its root is rejected whatever the link's target. The walk is proven everywhere with an
///     injected link probe over real temporary files; the tests that create real links skip themselves where the
///     host cannot create them.
/// </summary>
public class ScadFileResolverLinkTests
{
    /// <summary>Source text that fails to parse, so a test notices when a rejected file was read after all.</summary>
    private const string Unreadable = "this is not valid scad ;;;";

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
    ///     Validates that a linked directory between the root and the file is rejected and the file is not read.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_LinkedDirectoryBetweenRootAndFile_IsRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/lib/a.scad", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/lib"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["lib/a.scad"], isLink: probe.IsLink));

        // Assert
        Assert.Contains("link", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'lib'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
        Assert.Equal("main.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that a link at any depth below the root is rejected and named by its path below the root.
    /// </summary>
    /// <param name="depth">The depth of the linked directory (1 is directly below the root).</param>
    /// <param name="expectedComponent">The linked directory relative to the root.</param>
    [Theory]
    [InlineData(1, "d1")]
    [InlineData(2, "d1/d2")]
    [InlineData(3, "d1/d2/d3")]
    public void ScadFileResolverLink_Load_LinkedDirectoryAtAnyDepth_IsRejected(int depth, string expectedComponent)
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/d1/d2/d3/x.scad", Unreadable);
        var linked = directory.Combine("root/" + expectedComponent);
        var probe = new FakeLinks().With(linked);
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["d1/d2/d3/x.scad"], isLink: probe.IsLink));

        // Assert: the walk is top-down, so it stopped at the first link and never asked about anything below it
        Assert.Contains($"('{expectedComponent}')", ex.Message, StringComparison.Ordinal);
        Assert.Equal(depth, probe.Calls.Count);
    }

    /// <summary>
    ///     Validates that a file that is itself a link is rejected whatever its target, even a target inside the root.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_LinkedFile_IsRejectedWhateverItsTarget()
    {
        // Arrange: the "link" (reported by the probe) is an ordinary file that would parse badly if it were read
        using var directory = new ScadTempDirectory();
        directory.Write("root/real.scad", "r = 1;\n");
        directory.Write("root/alias.scad", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/alias.scad"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["alias.scad"], isLink: probe.IsLink));
        Assert.Contains("link", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["r"], AssignmentNames(LoadDirectives(options, ["real.scad"], isLink: probe.IsLink)));
    }

    /// <summary>
    ///     Validates that a link below a library root is rejected too (the base directory has no such file).
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_LinkedLibraryRootComponent_IsRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("base"));
        directory.Write("lib/shared/x.scad", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("lib/shared"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("base"), LibraryPaths = [directory.Combine("lib")] };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["shared/x.scad"], isLink: probe.IsLink));

        // Assert
        Assert.Contains("('shared')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the root and everything above it are trusted, even when the probe reports them as links.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_LinkedRootAndAncestors_AreTrusted()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("parent/root/ok.scad", "ok = 1;\n");
        var root = directory.Combine("parent/root");
        var probe = new FakeLinks().With(root, directory.Combine("parent"), directory.Path);
        var options = new ScadOpenOptions { BaseDirectory = root };

        // Act
        var unit = LoadDirectives(options, ["ok.scad"], isLink: probe.IsLink);

        // Assert: only the file itself was examined
        Assert.Equal(["ok"], AssignmentNames(unit));
        Assert.Equal([Path.GetFullPath(Path.Combine(root, "ok.scad"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that the first missing component ends the walk: the file is missing, nothing below it is probed,
    ///     and nothing is thrown even if the path below it would be a link.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_LinkAfterMissingComponent_IsNotProbed_AndFileIsMissing()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/dir"));
        var probe = new FakeLinks().With(directory.Combine("root/dir/gone"), directory.Combine("root/dir/gone/x.scad"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var unit = Load("include <dir/gone/x.scad>\n", options, sink: sink, isLink: probe.IsLink);

        // Assert: one warning, one probe (the existing directory), nothing below the missing component
        Assert.Empty(unit.Statements);
        Assert.Single(sink.Diagnostics);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/dir"))], probe.Calls);
    }

    /// <summary>
    ///     Validates that every existing component is probed exactly once however many files go through it.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_EveryComponentProbedOnce_AndCached()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/d/e/a.scad", "a = 1;\n");
        directory.Write("root/d/e/b.scad", "b = 1;\n");
        var probe = new FakeLinks();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var unit = LoadDirectives(options, ["d/e/a.scad", "d/e/b.scad", "d/e/a.scad"], isLink: probe.IsLink);

        // Assert
        Assert.Equal(["a", "b", "a"], AssignmentNames(unit));
        Assert.Equal(
            ["d", "d/e", "d/e/a.scad", "d/e/b.scad"],
            probe.Calls.Select(c => Path.GetRelativePath(directory.Combine("root"), c).Replace('\\', '/')).ToArray());
    }

    /// <summary>
    ///     Validates that a probe that cannot answer rejects the file (fail closed) instead of reading it.
    /// </summary>
    /// <param name="denied"><see langword="true"/> to throw an access error, <see langword="false"/> an I/O error.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScadFileResolverLink_Load_ProbeThrows_FailsClosed(bool denied)
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/a.scad", "a = 1;\n");
        var probe = new FakeLinks { Failure = denied ? new UnauthorizedAccessException("denied") : new IOException("broken") };
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["a.scad"], isLink: probe.IsLink));

        // Assert
        Assert.Contains("cannot be checked for links", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that resolver mode never asks the link probe: no file system access happens there.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_ResolverMode_NeverProbes()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a/b.scad", "b = 1;\n"));
        var probe = new FakeLinks { Failure = new IOException("must not be asked") };
        var options = new ScadOpenOptions { FileResolver = files.Resolve };

        // Act
        var unit = LoadDirectives(options, ["a/b.scad"], isLink: probe.IsLink);

        // Assert
        Assert.Equal(["b"], AssignmentNames(unit));
        Assert.Empty(probe.Calls);
    }

    /// <summary>
    ///     Validates that a route through dot-dot is judged on the normalized path: the directory that is stepped
    ///     over is not examined, the one that is entered is.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_DotDotRouteThroughLink_IsJudgedOnNormalizedPath()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("root/a"));
        directory.Write("root/lib/x.scad", Unreadable);
        var probe = new FakeLinks().With(directory.Combine("root/lib"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["a/../lib/x.scad"], isLink: probe.IsLink));

        // Assert
        Assert.Contains("('lib')", ex.Message, StringComparison.Ordinal);
        Assert.Equal([Path.GetFullPath(directory.Combine("root/lib"))], probe.Calls);
    }

    /// <summary>
    ///     Validates with a real link that a directory link that leaves the root is rejected.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_EscapingRoot_IsRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("outside/target.scad", "t = 1;\n");
        Directory.CreateDirectory(directory.Combine("root"));
        RequireDirectoryLink(directory.Combine("root/lnk"), directory.Combine("outside"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["lnk/target.scad"]));

        // Assert
        Assert.Contains("link", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with a real link that a directory link whose target is inside the root is rejected too, while
    ///     the real directory itself works.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_InsideRoot_IsRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/real/x.scad", "x = 1;\n");
        RequireDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["alias/x.scad"]));
        Assert.Equal(["x"], AssignmentNames(LoadDirectives(options, ["real/x.scad"])));
    }

    /// <summary>
    ///     Validates with a real link that a file link is rejected whatever its target.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealFileLink_IsRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var outside = directory.Write("outside/target.scad", "t = 1;\n");
        var inside = directory.Write("root/real.scad", "r = 1;\n");
        try
        {
            File.CreateSymbolicLink(directory.Combine("root/escape.scad"), outside);
            File.CreateSymbolicLink(directory.Combine("root/alias.scad"), inside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be created in this environment: " + ex.Message);
        }

        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        var escaping = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["escape.scad"]));
        Assert.Contains("link", escaping.Message, StringComparison.Ordinal);
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["alias.scad"]));
        Assert.Equal(["r"], AssignmentNames(LoadDirectives(options, ["real.scad"])));
    }

    /// <summary>
    ///     Validates with a real link that a base directory that is itself reached through a link is trusted.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealLinkedBaseDirectoryItself_IsAllowed()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("real/x.scad", "x = 1;\n");
        RequireDirectoryLink(directory.Combine("via"), directory.Combine("real"));
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("via") };

        // Act
        var unit = LoadDirectives(options, ["x.scad"]);

        // Assert
        Assert.Equal(["x"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Creates a directory link or skips the test when the host cannot.
    /// </summary>
    /// <param name="link">The full path of the link.</param>
    /// <param name="target">The full path of the target directory.</param>
    private static void RequireDirectoryLink(string link, string target)
    {
        if (!TryCreateDirectoryLink(link, target, out var reason))
        {
            Assert.Skip("Directory links cannot be created in this environment: " + reason);
        }
    }
}
