using System.Text;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests that prove the check of <see cref="FileReferenceResolver"/> is free of races: the decision is made
///     about the file the open handle refers to, not about whatever the name refers to later, and a referring document's
///     folder never becomes a trusted starting point. The file system is changed inside the seams around the open, so
///     every test is deterministic and asserts outcomes,
///     never time. The swaps use directory links (a junction on Windows, which needs no privilege) and moves and deletes
///     that Windows allows while a file is open with delete sharing, so the tests run on every system.
/// </summary>
public class FileReferenceResolverRaceTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that when the opened inside file is moved away and its folder is replaced by a link to an outside
    ///     folder after the open, the file is still allowed and its original bytes are read: the handle, not the name,
    ///     decides.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_FileMovedAndFolderSwappedForOutsideLinkAfterOpen_IsAllowedWithOriginalBytes()
    {
        // Arrange: an inside file, an outside file of the same name, and a swap that runs between open and check
        using var directory = new SandboxTempDirectory();
        directory.Write("root/sub/a.txt", "inside");
        directory.Write("outside/a.txt", "outside");
        Directory.CreateDirectory(directory.Combine("root/kept"));
        var root = directory.Combine("root");
        var hooks = new FileOpenHooks
        {
            AfterOpen = _ =>
            {
                File.Move(directory.Combine("root/sub/a.txt"), directory.Combine("root/kept/a.txt"));
                Directory.Delete(directory.Combine("root/sub"));
                FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("root/sub"), directory.Combine("outside"));
            },
        };
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = FileAccessPolicy.UnderRoots(root) }, Ct, hooks);

        // Act
        var file = resolver.Resolve("sub/a.txt");

        // Assert: allowed with the bytes of the opened file, whose real path followed it; by name it is now outside
        Assert.NotNull(file);
        Assert.Equal("inside", Encoding.UTF8.GetString(file.Content.Span));
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Combine("root/kept")), "a.txt"), file.RealPath!));
        Assert.Equal("outside", File.ReadAllText(directory.Combine("root/sub/a.txt")));
    }

    /// <summary>
    ///     Validates under the roots policy that when a link to an outside folder is replaced by a real inside folder
    ///     after the open, the file is still denied: the handle refers to the outside file.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_OutsideLinkReplacedByInsideFolderAfterOpen_IsAccessDeniedUnderUnderRoots()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = directory.Combine("root");
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = FileAccessPolicy.UnderRoots(root) },
            Ct,
            SwapLinkForBenignFolder(directory));

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("sub/a.txt"));

        // Assert: denied on the real path of the handle, which is the outside file
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Combine("outside")), "a.txt"), ex.RealPath!));
        Assert.Equal("benign", File.ReadAllText(directory.Combine("root/sub/a.txt")));
    }

    /// <summary>
    ///     Validates the same swap under the no-symlinks policy: the link is gone by name, but the handle was opened
    ///     through it, so the file is denied.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_OutsideLinkReplacedByInsideFolderAfterOpen_IsAccessDeniedUnderNoSymlinks()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks },
            Ct,
            SwapLinkForBenignFolder(directory));

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("sub/a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("no-symlinks", ex.PolicyName);
    }

    /// <summary>
    ///     Validates on a plain tree without links that the decision uses the real path reported for the handle: an
    ///     injected outside real path is denied by both restrictive policies.
    /// </summary>
    /// <param name="noSymlinks">Whether to use the no-symlinks policy (otherwise the roots policy).</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileReferenceResolver_Resolve_InjectedOutsideRealPath_IsAccessDenied(bool noSymlinks)
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var root = directory.Combine("root");
        var outside = Path.GetFullPath(directory.Combine("outside/a.txt"));
        var policy = noSymlinks ? FileAccessPolicy.NoSymlinks : FileAccessPolicy.UnderRoots(root);
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = policy },
            Ct,
            new FileOpenHooks { RealPathOf = _ => outside });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal(outside, ex.RealPath);
    }

    /// <summary>
    ///     Validates that the real path reported for the handle is the one the policy sees and the one the result carries.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_InjectedRealPath_IsTheOneInTheRequestAndTheResult()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("a.txt", "a");
        var reported = Path.GetFullPath(directory.Combine("elsewhere/a.txt"));
        var policy = RecordingPolicy.AllowingAll();
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = policy },
            Ct,
            new FileOpenHooks { RealPathOf = _ => reported });

        // Act
        var file = resolver.Resolve("a.txt");

        // Assert
        Assert.Equal(reported, Assert.Single(policy.Requests).RealPath);
        Assert.Equal(reported, file!.RealPath);
    }

    /// <summary>
    ///     Validates under the no-symlinks policy that a referring document's folder swapped for a link to an outside
    ///     folder after the document was read does not become a trusted starting point: the nested reference is denied,
    ///     because the check is anchored at the host-chosen base directory the chain started from.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_ReferrerFolderSwappedForLinkAfterItWasRead_IsAccessDeniedUnderNoSymlinks()
    {
        // Arrange: the document is read from base/sub; just before the referenced open, base/sub becomes a link outside
        using var directory = new SandboxTempDirectory();
        directory.Write("base/sub/doc.txt", "doc");
        directory.Write("outside/secret.txt", "secret");
        var hooks = new FileOpenHooks
        {
            BeforeOpen = key =>
            {
                if (key.EndsWith("secret.txt", StringComparison.Ordinal))
                {
                    File.Delete(directory.Combine("base/sub/doc.txt"));
                    Directory.Delete(directory.Combine("base/sub"));
                    FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("base/sub"), directory.Combine("outside"));
                }
            },
        };
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Combine("base"), FileAccess = FileAccessPolicy.NoSymlinks },
            Ct,
            hooks);
        var doc = resolver.Resolve("sub/doc.txt");

        // Act
        FileReference? secret = null;
        var ex = Assert.Throws<FileReferenceException>(() => secret = resolver.Resolve("secret.txt", doc));

        // Assert: denied by the policy, and nothing was returned
        Assert.NotNull(doc);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("no-symlinks", ex.PolicyName);
        Assert.Null(secret);
    }

    /// <summary>
    ///     Builds the outside-to-inside swap: <c>root/sub</c> is a link to <c>outside</c> (which holds <c>a.txt</c>), and
    ///     after the open the link is removed and replaced by a real folder with a benign <c>a.txt</c>.
    /// </summary>
    /// <param name="directory">The temporary directory.</param>
    /// <returns>The seams that perform the swap.</returns>
    private static FileOpenHooks SwapLinkForBenignFolder(SandboxTempDirectory directory)
    {
        directory.Write("outside/a.txt", "outside");
        Directory.CreateDirectory(directory.Combine("root"));
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("root/sub"), directory.Combine("outside"));
        return new FileOpenHooks
        {
            AfterOpen = _ =>
            {
                FileTestSupport.RemoveDirectoryLink(directory.Combine("root/sub"));
                directory.Write("root/sub/a.txt", "benign");
            },
        };
    }
}
