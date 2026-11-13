using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for links and the refusal wording of <see cref="ScadFileResolver"/>: by default an include through a
///     symbolic link or junction is followed, the no-symlinks and under-roots policies deny it, a dangling link is a
///     missing file, and every failure kind of the core engine maps to its located OpenSCAD wording. The engine
///     mechanics (the real path of the open handle, the race-free check, the caches) are proven in the core test
///     project; these tests show end to end, over real links, that a denial surfaces as a located OpenSCAD error.
///     Directory links are junctions on Windows, so they need no privilege; the file-link tests skip themselves where
///     the host cannot create file links.
/// </summary>
public class ScadFileResolverLinkTests
{
    /// <summary>
    ///     Validates that every kind of engine refusal (except an oversize file) becomes a located evaluation error with
    ///     the OpenSCAD wording, keeps the refusal as its inner exception, names the file relative to the base directory
    ///     when it lies under it and as written otherwise, and never shows a host directory.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    /// <param name="expected">The text the message must contain.</param>
    [Theory]
    [InlineData(FileReferenceFailureKind.EmptyPath, "Include path 'p' is not allowed: the path is empty")]
    [InlineData(FileReferenceFailureKind.ControlCharacter, "Include path 'p' is not allowed: the path contains a control character")]
    [InlineData(FileReferenceFailureKind.InvalidPath, "Include path 'p' is not allowed: the path contains a colon")]
    [InlineData(FileReferenceFailureKind.NetworkPath, "Include path 'p' is not allowed: network paths are not permitted")]
    [InlineData(FileReferenceFailureKind.AccessDenied, "was denied by the file-access policy 'roots': the stated reason")]
    [InlineData(FileReferenceFailureKind.RealPathUnavailable, "cannot be checked: its real path cannot be determined on this system, so the file-access policy 'roots' cannot be applied")]
    [InlineData(FileReferenceFailureKind.NotARegularFile, "is not a regular file")]
    [InlineData(FileReferenceFailureKind.CannotRead, "cannot be read")]
    [InlineData(FileReferenceFailureKind.InvalidBaseDirectory, "A configured directory (BaseDirectory or LibraryPaths) is not a valid path")]
    [InlineData(FileReferenceFailureKind.TooManyFiles, "cannot be read")]
    public void ScadFileResolverLink_Translate_EveryFailureKind_ProducesLocatedWording(FileReferenceFailureKind kind, string expected)
    {
        // Arrange: a refusal as the core would raise it, once for a file under the base directory and once outside it
        using var directory = new ScadTempDirectory();
        var resolver = CreateResolver(new ScadOpenOptions { BaseDirectory = directory.Combine("base") });
        var isPathText = kind is FileReferenceFailureKind.EmptyPath or FileReferenceFailureKind.ControlCharacter
            or FileReferenceFailureKind.InvalidPath or FileReferenceFailureKind.NetworkPath or FileReferenceFailureKind.InvalidBaseDirectory;
        var location = new ScadSourceLocation("main.scad", 4, 2);
        foreach (var (reference, requested, shown) in new[]
                 {
                     ("lib/x.scad", directory.Combine("base/lib/x.scad"), "lib/x.scad"),
                     ("../outside/x.scad", directory.Combine("outside/x.scad"), "../outside/x.scad"),
                 })
        {
            FileReferenceException ex;
            if (isPathText)
            {
                ex = new FileReferenceException(kind, "p", kind == FileReferenceFailureKind.InvalidBaseDirectory ? null : ReasonOf(kind));
            }
            else
            {
                ex = new FileReferenceException(kind, reference, kind == FileReferenceFailureKind.AccessDenied ? "the stated reason" : null)
                {
                    RequestedPath = requested,
                    PolicyName = "roots",
                };
            }

            // Act
            var translated = Assert.IsType<ScadEvaluationException>(resolver.Translate(ex, location));

            // Assert: located, the file named as the author knows it, the cause kept, and the host directory never shown
            Assert.Contains(expected, translated.Message, StringComparison.Ordinal);
            if (!isPathText)
            {
                Assert.Contains($"Include file '{shown}'", translated.Message, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(directory.Path, translated.Message, StringComparison.Ordinal);
            Assert.Same(ex, translated.InnerException);
            Assert.Equal(4, translated.Location.Line);
            Assert.Equal("main.scad", translated.Location.File);
        }
    }

    /// <summary>
    ///     Validates that a file that is too large is reported as the source-size error, as before.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Translate_TooLarge_IsSourceTooLong()
    {
        // Arrange
        var resolver = CreateResolver(new ScadOpenOptions { FileResolver = _ => null });
        var ex = new FileReferenceException(FileReferenceFailureKind.TooLarge, "big.scad");

        // Act
        var translated = resolver.Translate(ex, new ScadSourceLocation("main.scad", 1, 1));

        // Assert
        Assert.IsType<InvalidDataException>(translated);
        Assert.Contains("maximum length", translated.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with a real dangling file link that it is a missing file (a "can't open include" warning, not an
    ///     error) under the default policy and under no-symlinks alike. The test skips only when the host cannot create
    ///     file links (Windows without the privilege for symbolic links).
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDanglingLink_IsMissingUnderEveryPolicy()
    {
        // Arrange: a link whose target does not exist
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        if (!TryCreateFileLink(directory.Combine("root/dangling.scad"), directory.Combine("outside/nowhere.scad"), out var reason))
        {
            Assert.Skip("File symbolic links cannot be created in this environment (on Windows they need a privilege): " + reason);
        }

        foreach (var policy in new[] { FileAccessPolicy.AllowAll, FileAccessPolicy.NoSymlinks })
        {
            var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = policy };
            var sink = new ScadEvaluationSink(ScadLimits.Default);

            // Act
            var unit = Load("include <dangling.scad>\n", options, sink: sink);

            // Assert
            Assert.Empty(unit.Statements);
            Assert.Equal(ScadDiagnosticCodes.IncludeNotFound, Assert.Single(sink.Diagnostics).Code);
        }
    }

    /// <summary>
    ///     Validates with a real directory link that leaves the root that the no-symlinks policy denies the include
    ///     with a located error that names the policy and no host directory.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_EscapingRoot_NoSymlinks_IsDenied()
    {
        // Arrange
        using var directory = CreateEscapingLink();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["lnk/target.scad"]));

        // Assert
        Assert.Contains("Include file 'lnk/target.scad' was denied by the file-access policy 'no-symlinks'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
        Assert.IsType<FileReferenceException>(ex.InnerException);
    }

    /// <summary>
    ///     Validates with a real directory link that leaves the root that a policy confined to the root denies the
    ///     include, because the real path of the file is outside it.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_EscapingRoot_UnderRoots_IsDenied()
    {
        // Arrange
        using var directory = CreateEscapingLink();
        var options = new ScadOpenOptions
        {
            BaseDirectory = directory.Combine("root"),
            FileAccess = FileAccessPolicy.UnderRoots(directory.Combine("root")),
        };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["lnk/target.scad"]));

        // Assert
        Assert.Contains("denied by the file-access policy 'roots': its real path is outside the allowed roots", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with a real directory link that leaves the root that the default policy follows it.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_EscapingRoot_Default_IsFollowed()
    {
        // Arrange
        using var directory = CreateEscapingLink();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act
        var unit = LoadDirectives(options, ["lnk/target.scad"]);

        // Assert
        Assert.Equal(["t"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates with a real link that the no-symlinks policy denies a directory link whose target is inside the
    ///     root too, while the real directory itself works.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_InsideRoot_NoSymlinks_IsDenied()
    {
        // Arrange
        using var directory = CreateInsideLink();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["alias/x.scad"]));
        Assert.Equal(["x"], AssignmentNames(LoadDirectives(options, ["real/x.scad"])));
    }

    /// <summary>
    ///     Validates with a real link that a policy confined to the root allows a directory link whose target stays
    ///     inside the root.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDirectoryLink_InsideRoot_UnderRoots_IsAllowed()
    {
        // Arrange
        using var directory = CreateInsideLink();
        var options = new ScadOpenOptions
        {
            BaseDirectory = directory.Combine("root"),
            FileAccess = FileAccessPolicy.UnderRoots(directory.Combine("root")),
        };

        // Act
        var unit = LoadDirectives(options, ["alias/x.scad"]);

        // Assert
        Assert.Equal(["x"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates with real file links that the no-symlinks policy denies a file link whatever its target. The test
    ///     skips where file links cannot be created (Windows without the privilege for symbolic links).
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealFileLink_NoSymlinks_IsDenied()
    {
        // Arrange
        using var directory = CreateFileLinks();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks };

        // Act / Assert
        var escaping = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["escape.scad"]));
        Assert.Contains("denied by the file-access policy 'no-symlinks'", escaping.Message, StringComparison.Ordinal);
        Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["alias.scad"]));
        Assert.Equal(["r"], AssignmentNames(LoadDirectives(options, ["real.scad"])));
    }

    /// <summary>
    ///     Validates with real file links that the default policy follows a file link. The test skips where file links
    ///     cannot be created (Windows without the privilege for symbolic links).
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealFileLink_Default_IsFollowed()
    {
        // Arrange
        using var directory = CreateFileLinks();
        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };

        // Act / Assert
        Assert.Equal(["t"], AssignmentNames(LoadDirectives(options, ["escape.scad"])));
        Assert.Equal(["r"], AssignmentNames(LoadDirectives(options, ["alias.scad"])));
    }

    /// <summary>
    ///     Validates with a real link that a base directory that is itself reached through a link is trusted, under the
    ///     default policy and under no-symlinks.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealLinkedBaseDirectoryItself_IsAllowed()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("real/x.scad", "x = 1;\n");
        RequireDirectoryLink(directory.Combine("via"), directory.Combine("real"));

        foreach (var policy in new[] { FileAccessPolicy.AllowAll, FileAccessPolicy.NoSymlinks })
        {
            var options = new ScadOpenOptions { BaseDirectory = directory.Combine("via"), FileAccess = policy };

            // Act
            var unit = LoadDirectives(options, ["x.scad"]);

            // Assert
            Assert.Equal(["x"], AssignmentNames(unit));
        }
    }

    /// <summary>
    ///     Gets the rule a path text broke, as the core engine words it.
    /// </summary>
    /// <param name="kind">A path-text failure kind.</param>
    /// <returns>The rule.</returns>
    private static string ReasonOf(FileReferenceFailureKind kind) => kind switch
    {
        FileReferenceFailureKind.EmptyPath => "the path is empty",
        FileReferenceFailureKind.ControlCharacter => "the path contains a control character",
        FileReferenceFailureKind.InvalidPath => "the path contains a colon",
        _ => "network paths are not permitted",
    };

    /// <summary>
    ///     Creates <c>root</c> with a directory link <c>root/lnk</c> to <c>outside</c>, which holds <c>target.scad</c>.
    /// </summary>
    /// <returns>The temporary directory.</returns>
    private static ScadTempDirectory CreateEscapingLink()
    {
        var directory = new ScadTempDirectory();
        directory.Write("outside/target.scad", "t = 1;\n");
        Directory.CreateDirectory(directory.Combine("root"));
        RequireDirectoryLink(directory.Combine("root/lnk"), directory.Combine("outside"));
        return directory;
    }

    /// <summary>
    ///     Creates <c>root/real/x.scad</c> and a directory link <c>root/alias</c> to <c>root/real</c>.
    /// </summary>
    /// <returns>The temporary directory.</returns>
    private static ScadTempDirectory CreateInsideLink()
    {
        var directory = new ScadTempDirectory();
        directory.Write("root/real/x.scad", "x = 1;\n");
        RequireDirectoryLink(directory.Combine("root/alias"), directory.Combine("root/real"));
        return directory;
    }

    /// <summary>
    ///     Creates <c>root/real.scad</c>, <c>outside/target.scad</c> and the file links <c>root/escape.scad</c> (to the
    ///     outside file) and <c>root/alias.scad</c> (to the inside file), or skips the test where file links cannot be
    ///     created.
    /// </summary>
    /// <returns>The temporary directory.</returns>
    private static ScadTempDirectory CreateFileLinks()
    {
        var directory = new ScadTempDirectory();
        var outside = directory.Write("outside/target.scad", "t = 1;\n");
        var inside = directory.Write("root/real.scad", "r = 1;\n");
        if (!TryCreateFileLink(directory.Combine("root/escape.scad"), outside, out var reason)
            || !TryCreateFileLink(directory.Combine("root/alias.scad"), inside, out reason))
        {
            directory.Dispose();
            Assert.Skip("File symbolic links cannot be created in this environment (on Windows they need a privilege): " + reason);
        }

        return directory;
    }

    /// <summary>
    ///     Creates a resolver over open options, for the tests that call its translation directly.
    /// </summary>
    /// <param name="options">The open options.</param>
    /// <returns>The resolver.</returns>
    private static ScadFileResolver CreateResolver(ScadOpenOptions options) =>
        new(options, ScadLimits.Default, new ScadEvaluationSink(ScadLimits.Default), TestContext.Current.CancellationToken);

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
