using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Unit tests for <see cref="FileAccessPolicy"/> and <see cref="FileAccessDecision"/>: the built-in policies, their
///     composition, and policies made from a function. The policies are asked directly with requests built by the test
///     over real temporary folders, because roots and anchors are compared in their real location.
/// </summary>
public class FileAccessPolicyTests
{
    /// <summary>
    ///     Builds a request.
    /// </summary>
    /// <param name="baseDirectory">The base directory.</param>
    /// <param name="requestedPath">The path as written.</param>
    /// <param name="realPath">The real path.</param>
    /// <returns>The request.</returns>
    private static FileAccessRequest Request(string baseDirectory, string requestedPath, string realPath) => new()
    {
        Reference = Path.GetFileName(requestedPath),
        BaseDirectory = Path.GetFullPath(baseDirectory),
        RequestedPath = Path.GetFullPath(requestedPath),
        RealPath = realPath,
    };

    /// <summary>
    ///     Validates that the default policy allows every request, is named <c>any</c>, and needs no real path.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_AllowAll_AnyRequest_IsAllowedAndNamedAny()
    {
        // Arrange
        var policy = FileAccessPolicy.AllowAll;
        var request = Request(Path.GetTempPath(), "/anywhere/at/all.txt", "/somewhere/else.txt");

        // Act
        var decision = policy.Check(request);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal("any", policy.Name);
        Assert.Equal("any", policy.ToString());
        Assert.False(policy.RequiresRealPath);
        Assert.Null(policy.PreCheck(Path.GetFullPath("/anywhere/at/all.txt")));
    }

    /// <summary>
    ///     Validates that the roots policy allows a file whose written and real paths lie inside a root, or equal it.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_RealPathInsideOrEqualToARoot_IsAllowed()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var canonical = FileTestSupport.Canonical(root);
        var policy = FileAccessPolicy.UnderRoots(directory.Combine("other"), root);

        // Act
        var inside = policy.Check(Request(root, Path.Combine(root, "sub", "a.txt"), Path.Join(canonical, "sub", "a.txt")));
        var equal = policy.Check(Request(root, root, canonical));

        // Assert
        Assert.True(inside.IsAllowed);
        Assert.True(equal.IsAllowed);
        Assert.Equal("roots", policy.Name);
        Assert.True(policy.RequiresRealPath);
    }

    /// <summary>
    ///     Validates that a sibling folder whose name starts with the root's name is outside the root.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_SiblingWithSharedPrefix_IsDenied()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var sibling = Directory.CreateDirectory(directory.Combine("root-sibling")).FullName;
        var policy = FileAccessPolicy.UnderRoots(root);
        var written = Path.Combine(sibling, "a.txt");

        // Act
        var early = policy.PreCheck(written);
        var decision = policy.Check(Request(root, written, Path.Join(FileTestSupport.Canonical(sibling), "a.txt")));

        // Assert
        Assert.False(early!.IsAllowed);
        Assert.False(decision.IsAllowed);
    }

    /// <summary>
    ///     Validates that a root that did not exist when the policy was created allows nothing, even once it exists: its
    ///     real location was not captured, and the policy never resolves it by name later (fail closed).
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_RootCreatedAfterThePolicy_AllowsNothing()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Path.GetFullPath(directory.Combine("later"));
        var policy = FileAccessPolicy.UnderRoots(root);
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "a.txt");

        // Act
        var early = policy.PreCheck(file);
        var decision = policy.Check(Request(root, file, Path.Join(FileTestSupport.Canonical(root), "a.txt")));

        // Assert: the written path is inside, so the pre-check lets the open go ahead; the check after it denies
        Assert.Null(early);
        Assert.False(decision.IsAllowed);
        Assert.Equal("the allowed root it is in did not exist when the policy was created, so it allows nothing", decision.Reason);
        Assert.NotNull(decision.Hint);
    }

    /// <summary>
    ///     Validates that a file written inside a root whose real path lies outside every root is denied with a reason
    ///     and a hint.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_RealPathOutsideEveryRoot_IsDeniedWithReasonAndHint()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var outside = Directory.CreateDirectory(directory.Combine("outside")).FullName;
        var policy = FileAccessPolicy.UnderRoots(root);

        // Act
        var decision = policy.Check(Request(root, Path.Combine(root, "link", "a.txt"), Path.Join(FileTestSupport.Canonical(outside), "a.txt")));

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal("its real path is outside the allowed roots", decision.Reason);
        Assert.NotNull(decision.Hint);
    }

    /// <summary>
    ///     Validates that a path as written outside every root is denied before the open, and also after it even when
    ///     its real path lies inside a root (an outside link that points in is refused).
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_WrittenPathOutsideEveryRoot_IsDeniedByThePreCheck()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var outside = Directory.CreateDirectory(directory.Combine("outside")).FullName;
        var policy = FileAccessPolicy.UnderRoots(root);
        var written = Path.Combine(outside, "link-in", "a.txt");

        // Act
        var early = policy.PreCheck(written);
        var insideEarly = policy.PreCheck(Path.Combine(root, "a.txt"));
        var late = policy.Check(Request(outside, written, Path.Join(FileTestSupport.Canonical(root), "a.txt")));

        // Assert
        Assert.False(early!.IsAllowed);
        Assert.Equal("the path as written is outside the allowed roots", early.Reason);
        Assert.Null(insideEarly);
        Assert.False(late.IsAllowed);
    }

    /// <summary>
    ///     Validates that the roots policy rejects missing, empty and invalid roots when it is created.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_InvalidRoots_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.UnderRoots((string[])null!));
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.UnderRoots((IEnumerable<string>)null!));
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.UnderRoots((string)null!));
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.UnderRoots());
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.UnderRoots(string.Empty));
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.UnderRoots("   "));
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.UnderRoots("bad\0root"));
    }

    /// <summary>
    ///     Validates that paths are compared under the platform rule: case-insensitive on Windows and macOS, exact on
    ///     Linux.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_UnderRoots_PlatformCaseRule_IsApplied()
    {
        // Arrange: the written and real paths spell the root in upper case
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var policy = FileAccessPolicy.UnderRoots(root);
        var upper = Path.Combine(root.ToUpperInvariant(), "A.TXT");
        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        // Act
        var decision = policy.Check(Request(root, upper, Path.Join(FileTestSupport.Canonical(root).ToUpperInvariant(), "A.TXT")));

        // Assert
        Assert.Equal(expected, decision.IsAllowed);
    }

    /// <summary>
    ///     Validates that the no-symlinks policy allows a file whose real path is the written path below the folder the
    ///     reference starts from.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_NoSymlinks_RealPathMatchesTheWrittenPath_IsAllowed()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var baseDirectory = Directory.CreateDirectory(directory.Combine("base")).FullName;
        var policy = FileAccessPolicy.NoSymlinks;

        // Act
        var decision = policy.Check(Request(
            baseDirectory, Path.Combine(baseDirectory, "sub", "a.txt"), Path.Join(FileTestSupport.Canonical(baseDirectory), "sub", "a.txt")));

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal("no-symlinks", policy.Name);
        Assert.True(policy.RequiresRealPath);
    }

    /// <summary>
    ///     Validates that the no-symlinks policy denies a file whose real path differs from the written path below the
    ///     folder the reference starts from.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_NoSymlinks_RealPathDiffersBelowTheAnchor_IsDenied()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var baseDirectory = Directory.CreateDirectory(directory.Combine("base")).FullName;

        // Act
        var decision = FileAccessPolicy.NoSymlinks.Check(Request(
            baseDirectory, Path.Combine(baseDirectory, "link", "a.txt"), Path.Join(FileTestSupport.Canonical(baseDirectory), "target", "a.txt")));

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Contains("symbolic link or junction", decision.Reason, StringComparison.Ordinal);
        Assert.NotNull(decision.Hint);
    }

    /// <summary>
    ///     Validates that a link at or above the folder a reference starts from is trusted by the no-symlinks policy.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_NoSymlinks_LinkAboveTheAnchor_IsTrusted()
    {
        // Arrange: the base directory is a link to a real folder
        using var directory = new SandboxTempDirectory();
        var real = Directory.CreateDirectory(directory.Combine("real-base")).FullName;
        var linked = directory.Combine("linked-base");
        FileTestSupport.CreateDirectoryLinkOrSkip(linked, real);

        // Act
        var decision = FileAccessPolicy.NoSymlinks.Check(Request(
            linked, Path.Combine(linked, "a.txt"), Path.Join(FileTestSupport.Canonical(real), "a.txt")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    /// <summary>
    ///     Validates on Windows that a path written with short (8.3) names is compared in its long spelling, so it is not
    ///     mistaken for a link. Skips where the volume does not create short names.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_NoSymlinks_ShortNameSpelling_IsAllowed()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(FileTestSupport.WindowsOnly);
        }

        using var directory = new SandboxTempDirectory();
        var baseDirectory = Directory.CreateDirectory(directory.Combine("base")).FullName;
        var longFolder = Directory.CreateDirectory(Path.Combine(baseDirectory, "a-long-folder-name")).FullName;
        File.WriteAllText(Path.Combine(longFolder, "a.txt"), "a");
        var shortFolder = FileTestSupport.TryGetShortPath(longFolder);
        if (shortFolder is null || !shortFolder.Contains('~', StringComparison.Ordinal))
        {
            Assert.Skip("The volume does not create short (8.3) names.");
        }

        // Act
        var decision = FileAccessPolicy.NoSymlinks.Check(Request(
            baseDirectory, Path.Combine(shortFolder, "a.txt"), Path.Join(FileTestSupport.Canonical(longFolder), "a.txt")));

        // Assert
        Assert.True(decision.IsAllowed);
    }

    /// <summary>
    ///     Validates that a combination asks its parts in order, the first denial wins, names are joined with <c>+</c>,
    ///     nested combinations are flattened, a single policy is returned as it is, and the lexical pre-check and the
    ///     need for a real path come from the parts.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_Combine_FirstDenialWinsAndNamesAreJoined()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        var root = Directory.CreateDirectory(directory.Combine("root")).FullName;
        var first = FileAccessPolicy.FromDelegate("first", _ => FileAccessDecision.Deny("first reason"));
        var second = FileAccessPolicy.FromDelegate("second", _ => FileAccessDecision.Deny("second reason"));
        var third = FileAccessPolicy.FromDelegate("third", _ => FileAccessDecision.Allow);
        var request = Request(root, Path.Combine(root, "a.txt"), Path.Join(FileTestSupport.Canonical(root), "a.txt"));

        // Act
        var combined = FileAccessPolicy.Combine(FileAccessPolicy.Combine(third, first), second);
        var standard = FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(root), FileAccessPolicy.NoSymlinks);
        var decision = combined.Check(request);

        // Assert
        Assert.Equal("third+first+second", combined.Name);
        Assert.Equal("first reason", decision.Reason);
        Assert.Equal("roots+no-symlinks", standard.Name);
        Assert.True(standard.Check(request).IsAllowed);
        Assert.False(standard.PreCheck(Path.Combine(directory.Path, "outside.txt"))!.IsAllowed);
        Assert.Same(third, FileAccessPolicy.Combine(third));
        Assert.False(FileAccessPolicy.Combine(FileAccessPolicy.AllowAll, FileAccessPolicy.AllowAll).RequiresRealPath);
        Assert.True(FileAccessPolicy.Combine(FileAccessPolicy.AllowAll, FileAccessPolicy.NoSymlinks).RequiresRealPath);
    }

    /// <summary>
    ///     Validates that a combination rejects a missing, empty or null-holding list.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_Combine_InvalidArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.Combine(null!));
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.Combine());
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.Combine(FileAccessPolicy.AllowAll, null!));
    }

    /// <summary>
    ///     Validates that a policy made from a function uses its name and function, and rejects a missing name or
    ///     function.
    /// </summary>
    [Fact]
    public void FileAccessPolicy_FromDelegate_UsesNameAndCheckAndRejectsInvalidArguments()
    {
        // Arrange
        var asked = new List<FileAccessRequest>();
        var policy = FileAccessPolicy.FromDelegate("custom", request =>
        {
            asked.Add(request);
            return FileAccessDecision.Deny("custom reason");
        });
        var request = Request(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "a.txt"), "/x/a.txt");

        // Act
        var decision = policy.Check(request);

        // Assert
        Assert.Equal("custom", policy.Name);
        Assert.Equal("custom reason", decision.Reason);
        Assert.Same(request, Assert.Single(asked));
        Assert.True(policy.RequiresRealPath);
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.FromDelegate(null!, _ => FileAccessDecision.Allow));
        Assert.Throws<ArgumentException>(() => FileAccessPolicy.FromDelegate(" ", _ => FileAccessDecision.Allow));
        Assert.Throws<ArgumentNullException>(() => FileAccessPolicy.FromDelegate("x", null!));
    }

    /// <summary>
    ///     Validates that a decision keeps its reason and hint, that the allow decision is one instance, and that a denial
    ///     needs a reason.
    /// </summary>
    [Fact]
    public void FileAccessDecision_Deny_KeepsReasonAndHintAndRejectsAnEmptyReason()
    {
        // Act
        var denied = FileAccessDecision.Deny("a reason", "a hint");
        var withoutHint = FileAccessDecision.Deny("a reason", " ");

        // Assert
        Assert.False(denied.IsAllowed);
        Assert.Equal("a reason", denied.Reason);
        Assert.Equal("a hint", denied.Hint);
        Assert.Null(withoutHint.Hint);
        Assert.True(FileAccessDecision.Allow.IsAllowed);
        Assert.Null(FileAccessDecision.Allow.Reason);
        Assert.Same(FileAccessDecision.Allow, FileAccessDecision.Allow);
        Assert.Throws<ArgumentNullException>(() => FileAccessDecision.Deny(null!));
        Assert.Throws<ArgumentException>(() => FileAccessDecision.Deny(string.Empty));
    }
}
