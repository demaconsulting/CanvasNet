using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the link wording of <see cref="ScadFileResolver"/>: a file that is, or lies beneath, a symbolic
///     link or junction below its root is rejected whatever the link's target. The sandbox mechanics (the link walk,
///     the dangling-link rule, the fail-closed probe, the caches) are proven in the core test project with an injected
///     link probe; these tests show end to end, over real links, that the refusal surfaces as a located OpenSCAD error,
///     and map every sandbox failure kind to its wording. The real-link tests skip themselves where the host cannot
///     create links.
/// </summary>
public class ScadFileResolverLinkTests
{
    /// <summary>
    ///     Validates that every kind of sandbox refusal becomes a located evaluation error with the OpenSCAD wording,
    ///     naming the file as the author wrote it and never a host directory, except that an oversize file is the
    ///     source-size error.
    /// </summary>
    /// <param name="kind">The failure kind.</param>
    /// <param name="expected">The text the message must contain.</param>
    [Theory]
    [InlineData(SandboxFailureKind.EmptyPath, "Include path 'p' is not allowed: the path is empty")]
    [InlineData(SandboxFailureKind.OutsideRoot, "Include path 'p' is outside the allowed directories")]
    [InlineData(SandboxFailureKind.ThroughLink, "passes through a link ('a'); links are not followed inside the allowed directories")]
    [InlineData(SandboxFailureKind.LinkCheckFailed, "cannot be checked for links and is not read")]
    [InlineData(SandboxFailureKind.CannotRead, "cannot be read")]
    [InlineData(SandboxFailureKind.InvalidRoot, "A configured directory (BaseDirectory or LibraryPaths) is not a valid path")]
    public void ScadFileResolverLink_Translate_EverySandboxKind_ProducesLocatedWording(SandboxFailureKind kind, string expected)
    {
        // Arrange: a sandbox exception as the core would raise it; a file failure carries a full path under the base directory
        using var directory = new ScadTempDirectory();
        var options = new ScadOpenOptions { BaseDirectory = directory.Path };
        var resolver = CreateResolver(options);
        var isPathText = kind is SandboxFailureKind.EmptyPath or SandboxFailureKind.OutsideRoot;
        var detail = kind == SandboxFailureKind.ThroughLink ? "a" : null;
        if (kind == SandboxFailureKind.EmptyPath)
        {
            detail = "the path is empty";
        }

        var ex = new SandboxException(kind, isPathText ? "p" : directory.Combine("lib/x.scad"), detail);
        var location = new ScadSourceLocation("main.scad", 4, 2);

        // Act
        var translated = Assert.IsType<ScadEvaluationException>(resolver.Translate(ex, location));

        // Assert: located, the file named relative to the base directory, and the host directory never shown
        Assert.Contains(expected, translated.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, translated.Message, StringComparison.Ordinal);
        Assert.Equal(4, translated.Location.Line);
        Assert.Equal("main.scad", translated.Location.File);
    }

    /// <summary>
    ///     Validates that a file that is too large is reported as the source-size error, as before.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Translate_TooLarge_IsSourceTooLong()
    {
        // Arrange
        var resolver = CreateResolver(new ScadOpenOptions { FileResolver = _ => null });
        var ex = new SandboxException(SandboxFailureKind.TooLarge, "big.scad");

        // Act
        var translated = resolver.Translate(ex, new ScadSourceLocation("main.scad", 1, 1));

        // Assert
        Assert.IsType<InvalidDataException>(translated);
        Assert.Contains("maximum length", translated.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates with a real dangling file link that it is a located link error (not a "can't open include" warning)
    ///     while a truly missing file is only a warning. The test skips only when the host cannot create file links.
    /// </summary>
    [Fact]
    public void ScadFileResolverLink_Load_RealDanglingLink_IsRejected_WhileMissingFileWarns()
    {
        // Arrange: a link whose target does not exist
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        if (!TryCreateFileLink(directory.Combine("root/dangling.scad"), directory.Combine("outside/nowhere.scad"), out var reason))
        {
            Assert.Skip("File links cannot be created in this environment: " + reason);
        }

        var options = new ScadOpenOptions { BaseDirectory = directory.Combine("root") };
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadDirectives(options, ["dangling.scad"]));
        var unit = Load("include <gone.scad>\n", options, sink: sink);

        // Assert
        Assert.Contains("passes through a link ('dangling.scad')", ex.Message, StringComparison.Ordinal);
        Assert.Empty(unit.Statements);
        Assert.Single(sink.Diagnostics);
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
        Assert.Contains("passes through a link ('lnk')", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
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
