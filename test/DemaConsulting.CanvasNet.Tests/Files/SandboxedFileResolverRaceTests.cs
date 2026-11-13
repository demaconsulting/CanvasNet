using System.Text;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for the post-open verification of <see cref="SandboxedFileResolver"/>: after the link walk, the open
///     handle must be a regular file whose real path is the canonical root followed by the path below the root. The
///     file system is changed, or the real path replaced, inside the seams around the open, so the tests are
///     deterministic and assert outcomes, never time.
/// </summary>
public class SandboxedFileResolverRaceTests
{
    /// <summary>Bytes that are not valid text, so a test notices when a refused file was read after all.</summary>
    private static readonly byte[] Unreadable = [0xFF, 0xFE, 0xFD];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates a resolver over a base directory with seams around the verified open.
    /// </summary>
    /// <param name="baseDirectory">The base directory.</param>
    /// <param name="hooks">The seams.</param>
    /// <returns>The resolver.</returns>
    private static SandboxedFileResolver Create(string baseDirectory, FileOpenHooks hooks) =>
        new(new SandboxOptions { BaseDirectory = baseDirectory }, Ct, null, null, hooks);

    /// <summary>
    ///     Validates that a link swapped into the tree after the link walk and before the open is refused as a link,
    ///     naming the file below the root, and that nothing is read through it.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_LinkSwappedInAfterTheWalk_IsRejectedAsThroughLink()
    {
        // Arrange: the walk sees a real folder; just before the open, the folder is replaced by a link to the outside
        using var directory = new SandboxTempDirectory();
        directory.Write("root/sub/a.txt", "inside");
        directory.Write("outside/a.txt", Unreadable);
        var hooks = new FileOpenHooks
        {
            BeforeOpen = _ =>
            {
                Directory.Move(directory.Combine("root/sub"), directory.Combine("root/sub-old"));
                FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("root/sub"), directory.Combine("outside"));
            },
        };
        var resolver = Create(directory.Combine("root"), hooks);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("sub/a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal("sub/a.txt", ex.Detail);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a real path outside the root, reported for the open handle, is refused as a link.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RealPathOutsideTheRoot_IsRejectedAsThroughLink()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var outside = Path.GetFullPath(directory.Combine("outside/a.txt"));
        var resolver = Create(directory.Combine("root"), new FileOpenHooks { RealPathOf = _ => outside });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.ThroughLink, ex.Kind);
        Assert.Equal("a.txt", ex.Detail);
    }

    /// <summary>
    ///     Validates that the file is refused (fail closed) when the real path of the open handle cannot be determined.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RealPathUnavailable_FailsClosedAsLinkCheckFailed()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var resolver = Create(directory.Combine("root"), new FileOpenHooks { RealPathOf = _ => null });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.LinkCheckFailed, ex.Kind);
    }

    /// <summary>
    ///     Validates that a handle whose type the platform cannot tell is refused (fail closed), not read.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_UnknownFileType_FailsClosedAsCannotRead()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var resolver = Create(directory.Combine("root"), new FileOpenHooks { IsRegularFileOf = _ => null });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
    }

    /// <summary>
    ///     Validates on Linux and macOS that a root the process may search but not read (execute-only) still serves its
    ///     files: the real location of the root needs no read permission. Skips when permissions are not enforced.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_ExecuteOnlyRoot_ReadsTheFile()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var root = Path.GetFullPath(directory.Combine("root"));
        try
        {
            if (!FileTestSupport.TryMakeExecuteOnly(root))
            {
                Assert.Skip(FileTestSupport.PermissionsNotEnforced);
            }

            var resolver = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = root }, Ct);

            // Act
            var file = resolver.Resolve("a.txt");

            // Assert
            Assert.NotNull(file);
            Assert.Equal("inside", Encoding.UTF8.GetString(file.Content.Span));
        }
        finally
        {
            FileTestSupport.RestoreOwnerAccess(root);
        }
    }

    /// <summary>
    ///     Validates on Linux and macOS that the file is refused as a failed check (fail closed), not as a link, when the
    ///     real location of its root cannot be determined: the root is renamed away while the file is open. Windows does
    ///     not allow renaming the folder of an open file.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_RootRealLocationUnavailable_FailsClosedAsLinkCheckFailed()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.UnixOnly);
        }

        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var resolver = Create(
            directory.Combine("root"),
            new FileOpenHooks { AfterOpen = _ => Directory.Move(directory.Combine("root"), directory.Combine("moved")) });

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(SandboxFailureKind.LinkCheckFailed, ex.Kind);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that something other than a regular file below the root is a read failure: a named pipe on Linux and
    ///     macOS (which also must not hang the open), and on Windows a device handle injected through the open seam.
    /// </summary>
    [Fact]
    public void SandboxedFileResolver_Resolve_NonRegularFile_IsCannotRead()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("root"));
        FileOpenHooks hooks;
        if (OperatingSystem.IsWindows())
        {
            directory.Write("root/special", "x");
            hooks = new FileOpenHooks { Open = _ => FileTestSupport.OpenNullDevice() };
        }
        else
        {
            var failure = FileTestSupport.CreateFifo(directory.Combine("root/special"));
            Assert.True(failure.Length == 0, failure);
            hooks = new FileOpenHooks();
        }

        var resolver = Create(directory.Combine("root"), hooks);

        // Act
        var ex = Assert.Throws<SandboxException>(() => resolver.Resolve("special"));

        // Assert
        Assert.Equal(SandboxFailureKind.CannotRead, ex.Kind);
    }
}
