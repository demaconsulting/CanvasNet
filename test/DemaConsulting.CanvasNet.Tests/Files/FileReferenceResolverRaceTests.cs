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
    ///     Validates that a root renamed and replaced by a link to an outside folder after the pre-check and before the
    ///     open is denied under each restrictive policy: the real location of the trusted folder was captured before
    ///     (when the roots policy was created, and before the open for the no-symlinks policy) and is never resolved by
    ///     name again, so the outside file the open reached does not match it.
    /// </summary>
    /// <param name="noSymlinks">Whether to use the no-symlinks policy (otherwise the roots policy).</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileReferenceResolver_Resolve_RootReplacedByOutsideLinkBeforeTheOpen_IsAccessDenied(bool noSymlinks)
    {
        // Arrange: the root exists when the policy and the resolver are created; just before the open it is renamed
        // away and an outside folder takes its name through a link
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        directory.Write("outside/a.txt", "outside");
        var root = Path.GetFullPath(directory.Combine("root"));
        var policy = noSymlinks ? FileAccessPolicy.NoSymlinks : FileAccessPolicy.UnderRoots(root);
        var hooks = new FileOpenHooks
        {
            BeforeOpen = _ =>
            {
                Directory.Move(root, directory.Combine("root-old"));
                FileTestSupport.CreateDirectoryLinkOrSkip(root, directory.Combine("outside"));
            },
        };
        var resolver = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = root, FileAccess = policy }, Ct, hooks);

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert: the open reached the outside file, and the check refused it
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal(noSymlinks ? "no-symlinks" : "roots", ex.PolicyName);
        Assert.True(RealPath.PathsEqual(Path.Join(FileTestSupport.Canonical(directory.Combine("outside")), "a.txt"), ex.RealPath!));
    }

    /// <summary>
    ///     Validates that the no-symlinks policy fails closed when the real location of the folder a reference starts
    ///     from could not be captured before the open.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_AnchorLocationNotCaptured_IsAccessDeniedUnderNoSymlinks()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Combine("root"), FileAccess = FileAccessPolicy.NoSymlinks },
            Ct,
            new FileOpenHooks { DirectoryLocationOf = _ => null });

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("the real path of the folder the reference starts from cannot be determined", ex.Reason);
    }

    /// <summary>
    ///     Validates under the roots policy that when a link to an outside folder is replaced by a real inside folder
    ///     after the open, the file is still denied: the handle refers to the outside file.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_OutsideLinkReplacedByInsideFolderAfterOpen_IsAccessDeniedUnderUnderRoots()
    {
        // Arrange: the tree exists before the policy is created, so the policy captures the root's real location
        using var directory = new SandboxTempDirectory();
        var root = directory.Combine("root");
        var hooks = SwapLinkForBenignFolder(directory);
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = FileAccessPolicy.UnderRoots(root) },
            Ct,
            hooks);

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("sub/a.txt"));

        // Assert: denied on the real path of the handle, which is the outside file
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("its real path is outside the allowed roots", ex.Reason);
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
    ///     Validates under each restrictive policy that a file whose on-disk spelling names another file (a spelling the
    ///     identity check refuses) is denied before it is read, with a reason that names no host path, and is not
    ///     remembered: asking again opens it again.
    /// </summary>
    /// <param name="noSymlinks">Whether to use the no-symlinks policy (otherwise the roots policy).</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileReferenceResolver_Resolve_StoredSpellingNamingAnotherFile_IsAccessDenied(bool noSymlinks)
    {
        // Arrange: the computed on-disk spelling is replaced by the path of another existing file
        using var directory = new SandboxTempDirectory();
        directory.Write("root/a.txt", "inside");
        var other = Path.GetFullPath(directory.Write("root/b.txt", "other"));
        var root = directory.Combine("root");
        var policy = noSymlinks ? FileAccessPolicy.NoSymlinks : FileAccessPolicy.UnderRoots(root);
        var opens = 0;
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = policy },
            Ct,
            new FileOpenHooks { BeforeOpen = _ => opens++, ExactPathOf = _ => other });

        // Act
        var first = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));
        var second = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, first.Kind);
        Assert.Equal("the letter case its real path has on disk cannot be confirmed to name the opened file", first.Reason);
        Assert.Equal("reference the file in the letter case it has on disk", first.Hint);
        Assert.DoesNotContain(directory.Path, first.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, second.Kind);
        Assert.Equal(2, opens);
    }

    /// <summary>
    ///     Validates in a folder that ignores letter case that a real path reported in another letter case (as a file
    ///     system that reports the spelling used to open a file does) is given to the policy, and carried by the result,
    ///     in the letter case stored on disk. Skips where the temporary folder compares letter case.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_ReportedRealPathInAnotherLetterCase_IsCorrectedToTheStoredCase()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseInsensitive(directory.Path);
        directory.Write("a.txt", "a");
        var expected = Path.Join(FileTestSupport.Canonical(directory.Path), "a.txt");
        var policy = RecordingPolicy.AllowingAll();
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = policy },
            Ct,
            new FileOpenHooks { RealPathOf = handle => RealPath.Inspect(handle).FinalPath!.ToUpperInvariant() });

        // Act: a denial is reported with the reason the spelling is not confirmed, so a failure on a platform that cannot
        // be run locally tells which step failed
        FileReference? file;
        try
        {
            file = resolver.Resolve("a.txt");
        }
        catch (FileReferenceException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"The stored spelling is not confirmed: {DiagnoseConfirmation(directory.Combine("a.txt"))}", ex);
        }

        // Assert
        Assert.Equal(expected, Assert.Single(policy.Requests).RealPath);
        Assert.Equal(expected, file!.RealPath);
    }

    /// <summary>
    ///     Tells why the on-disk spelling of the upper-cased real path of a file is not confirmed, for the message of a
    ///     failing test.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The reason, or a note that it is confirmed when opened directly.</returns>
    private static string DiagnoseConfirmation(string path)
    {
        using var handle = RealPath.OpenForRead(Path.GetFullPath(path));
        var reported = RealPath.Inspect(handle).FinalPath!.ToUpperInvariant();
        var confirmed = RealPath.TryConfirmExactPath(handle, reported, RealPath.GetExactPathName(reported), out var reason);
        return confirmed is null ? reason ?? "no reason given" : "it is confirmed when the file is opened directly";
    }

    /// <summary>
    ///     Validates that the top-level file, which the policy does not judge, is read even when the on-disk spelling of
    ///     its real path cannot be confirmed, and that a reference that reaches it later is denied with the same reason.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_ResolveTopLevel_StoredSpellingNamingAnotherFile_IsDeniedWhenReferenced()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("doc.txt", "doc");
        var other = Path.GetFullPath(directory.Write("other.txt", "other"));
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = directory.Path, FileAccess = FileAccessPolicy.UnderRoots(directory.Path) },
            Ct,
            new FileOpenHooks { ExactPathOf = _ => other });
        var document = resolver.ResolveTopLevel("doc.txt");

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("doc.txt"));

        // Assert
        Assert.NotNull(document);
        Assert.Equal("doc", Encoding.UTF8.GetString(document.Content.Span));
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("the letter case its real path has on disk cannot be confirmed to name the opened file", ex.Reason);
    }

    /// <summary>
    ///     Validates in a folder that compares letter case that a link named like a file in another letter case
    ///     (<c>Target.txt</c> to <c>target.txt</c>), created after the on-disk spelling was captured and before the open,
    ///     is denied under the no-symlinks policy: the written name named no entry when the spelling was captured, so it
    ///     was kept as written, and the file the open reached through the link does not match it. Skips where the
    ///     temporary folder ignores letter case.
    /// </summary>
    [Fact]
    public void FileReferenceResolver_Resolve_CaseVariantLinkCreatedBeforeTheOpen_IsDeniedUnderNoSymlinks()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        FileTestSupport.RequireCaseSensitive(directory.Path);
        directory.Write("root/target.txt", "target");
        var root = Path.GetFullPath(directory.Combine("root"));
        var hooks = new FileOpenHooks
        {
            BeforeOpen = _ => File.CreateSymbolicLink(Path.Join(root, "Target.txt"), "target.txt"),
        };
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = FileAccessPolicy.NoSymlinks }, Ct, hooks);

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("Target.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("no-symlinks", ex.PolicyName);
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
