using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the sandbox of <see cref="ScadFileResolver"/>: hostile paths, roots, links and resolver-only access.
/// </summary>
public class ScadFileResolverSandboxTests
{
    /// <summary>
    ///     Gets path texts that must be rejected in every mode.
    /// </summary>
    public static TheoryData<string> HostilePaths =>
    [
        "/etc/passwd",
        "\\windows\\system32\\x.scad",
        "\\\\server\\share\\x.scad",
        "C:\\Windows\\x.scad",
        "C:x.scad",
        "a:b.scad",
        "file.scad:stream",
        string.Empty,
        "   ",
        "a\0b.scad",
        "a\u0001b.scad",
        "a\u007Fb.scad",
        "a\nb.scad",
        "../x.scad",
        "..\\x.scad",
        "a/../../x.scad",
        "a/b/../../../x.scad",
        "./../x.scad",
    ];

    /// <summary>
    ///     Validates that hostile path texts are sandbox violations in resolver mode and never reach the callback.
    /// </summary>
    /// <param name="path">The hostile path text.</param>
    [Theory]
    [MemberData(nameof(HostilePaths))]
    public void ScadFileResolver_Load_HostilePath_InResolverMode_ThrowsWithoutAskingResolver(string path)
    {
        // Arrange
        var files = new ScadVirtualFiles(("x.scad", "x = 1;"), ("etc/passwd", "p = 1;"));
        var options = new ScadOpenOptions { FileResolver = files.Resolve };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, [path]));

        // Assert
        Assert.Equal(1, ex.Location.Line);
        Assert.Equal("main.scad", ex.Location.File);
        Assert.Empty(files.Requests);
        Assert.DoesNotContain('\0', ex.Message);
    }

    /// <summary>
    ///     Validates that hostile path texts are sandbox violations in file-system mode.
    /// </summary>
    /// <param name="path">The hostile path text.</param>
    [Theory]
    [MemberData(nameof(HostilePaths))]
    public void ScadFileResolver_Load_HostilePath_InFileSystemMode_Throws(string path)
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var options = new ScadOpenOptions { BaseDirectory = directory.Path };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, [path]));
    }

    /// <summary>
    ///     Validates that dot-dot that stays inside the root is allowed.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DotDotInsideRoot_IsAllowed()
    {
        // Arrange
        var files = new ScadVirtualFiles(("b.scad", "b = 1;\n"));
        var options = new ScadOpenOptions { FileResolver = files.Resolve };

        // Act
        var unit = LoadDirectives(options, ["a/c/../../b.scad"]);

        // Assert
        Assert.Equal(["b@b.scad:1"], Assignments(unit));
        Assert.Equal(["b.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that a nested file cannot climb above the root either.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_NestedFileEscapingRoot_Throws()
    {
        // Arrange
        var files = new ScadVirtualFiles(("libs/x.scad", "include <../../y.scad>\n"));

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <libs/x.scad>\n", files));

        // Assert
        Assert.Equal("libs/x.scad", ex.Location.File);
        Assert.Equal(["libs/x.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that every path handed to the callback is a normalized logical path.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ResolverMode_RequestsOnlyNormalizedLogicalPaths()
    {
        // Arrange
        var files = new ScadVirtualFiles();
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        LoadVirtual(
            "include <a\\b/../c\\.\\d.scad>\ninclude <./e.scad>\ninclude <f//g.scad>\n",
            files,
            libraryPaths: ["lib\\sub", "other/./dir/"],
            sink: sink);

        // Assert
        Assert.All(files.Requests, r =>
        {
            Assert.DoesNotContain('\\', r);
            Assert.DoesNotContain("..", r, StringComparison.Ordinal);
            Assert.DoesNotContain("./", r, StringComparison.Ordinal);
            Assert.DoesNotContain("//", r, StringComparison.Ordinal);
            Assert.False(r.StartsWith('/'));
        });
        Assert.Equal(
            ["a/c/d.scad", "lib/sub/a/c/d.scad", "other/dir/a/c/d.scad", "e.scad", "lib/sub/e.scad", "other/dir/e.scad", "f/g.scad", "lib/sub/f/g.scad", "other/dir/f/g.scad"],
            files.Requests);
        Assert.Equal(3, sink.Diagnostics.Count);
    }

    /// <summary>
    ///     Validates that resolver mode performs no file-system access: a real file under the base directory is not read.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ResolverMode_DoesNotTouchFileSystem()
    {
        // Arrange: a real file that a file-system search would find
        using var directory = new ScadTempDirectory();
        directory.Write("real.scad", "real = 1;\n");
        var files = new ScadVirtualFiles();
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var options = new ScadOpenOptions { BaseDirectory = directory.Path, LibraryPaths = [directory.Path], FileResolver = files.Resolve };

        // Act
        var unit = Load("include <real.scad>\n", options, sink: sink);

        // Assert: only the callback was consulted, and it said no
        Assert.Empty(unit.Statements);
        Assert.Equal(["real.scad"], files.Requests);
        Assert.Single(sink.Diagnostics);
    }

    /// <summary>
    ///     Validates that the callback is the only source in resolver mode even when it supplies the file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ResolverMode_UsesCallbackStream()
    {
        // Arrange
        var files = new ScadVirtualFiles(("virtual.scad", "v = 1;\n"));

        // Act
        var unit = LoadVirtual("include <virtual.scad>\n", files);

        // Assert
        Assert.Equal(["v@virtual.scad:1"], Assignments(unit));
        Assert.All(files.Threads, t => Assert.Equal(Environment.CurrentManagedThreadId, t));
    }

    /// <summary>
    ///     Validates that a resolver exception is not wrapped.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ResolverException_Propagates()
    {
        // Arrange
        var options = new ScadOpenOptions { FileResolver = _ => throw new IOException("boom") };

        // Act / Assert
        var ex = Assert.Throws<IOException>(() => LoadDirectives(options, ["a.scad"]));
        Assert.Equal("boom", ex.Message);
    }

    /// <summary>
    ///     Validates that the first library path that has the file wins, after the including directory.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileSystem_CandidateOrderIsIncludingDirectoryThenLibraries()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("base/dup.scad", "from_base = 1;\n");
        directory.Write("lib1/dup.scad", "from_lib1 = 1;\n");
        directory.Write("lib2/dup.scad", "from_lib2 = 1;\n");
        directory.Write("lib2/only2.scad", "only2 = 1;\n");
        directory.Write("lib1/only1.scad", "only1 = 1;\n");
        var options = new ScadOpenOptions
        {
            BaseDirectory = directory.Combine("base"),
            LibraryPaths = [directory.Combine("lib1"), directory.Combine("lib2")],
        };

        // Act
        var unit = Load("include <dup.scad>\ninclude <only2.scad>\ninclude <only1.scad>\n", options);

        // Assert
        Assert.Equal(["from_base", "only2", "only1"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates that a library root is a confinement: a relative path cannot climb out of it.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileSystem_LibraryRootConfinesCandidates()
    {
        // Arrange: no base directory, so only the library candidate exists, and it points outside its root
        using var directory = new ScadTempDirectory();
        directory.Write("lib/x.scad", "x = 1;\n");
        directory.Write("outside.scad", "o = 1;\n");
        var options = new ScadOpenOptions { LibraryPaths = [directory.Combine("lib")] };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["../outside.scad"]));
        Assert.Equal(["x"], AssignmentNames(LoadDirectives(options, ["x.scad"])));
    }

    /// <summary>
    ///     Validates that a file that exists outside the root is still refused by the sandbox, not read.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileSystem_ExistingFileOutsideRoot_IsRefused()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("secret.scad", "s = 1;\n");
        directory.Write("root/ok.scad", "ok = 1;\n");
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["../secret.scad"]));
        Assert.Equal(["ok"], AssignmentNames(LoadDirectives(options, ["ok.scad"])));
    }

    /// <summary>
    ///     Validates that a sibling folder whose name merely starts with the root's name is not "under" the root.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileSystem_SiblingWithSharedPrefix_IsOutsideRoot()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("root/a.scad", "a = 1;\n");
        directory.Write("root-evil/b.scad", "b = 1;\n");
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["../root-evil/b.scad"]));
    }

    /// <summary>
    ///     Validates that a link whose target is outside every root is rejected, and a link inside is allowed.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileSystem_LinkTargets_AreChecked()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var outside = directory.Write("outside/target.scad", "t = 1;\n");
        var inside = directory.Write("root/real.scad", "r = 1;\n");
        directory.Write("root/placeholder.txt", string.Empty);
        var escapingLink = directory.Combine("root/escape.scad");
        var allowedLink = directory.Combine("root/alias.scad");
        try
        {
            File.CreateSymbolicLink(escapingLink, outside);
            File.CreateSymbolicLink(allowedLink, inside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Symbolic links cannot be created in this environment: " + ex.Message);
            return;
        }

        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        var ex2 = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["escape.scad"]));
        Assert.Contains("link", ex2.Message, StringComparison.Ordinal);
        Assert.Equal(["r"], AssignmentNames(LoadDirectives(options, ["alias.scad"])));
    }
}
