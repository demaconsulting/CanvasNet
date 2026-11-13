using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.Tests.Files;

/// <summary>
///     Subsystem tests for the <c>Files</c> subsystem: referenced files are read anywhere under the default policy and
///     confined by the built-in policies, the check follows the opened file rather than its name, unsafe references are
///     refused under every policy, and a denial is reported with its facts. A source-tree guard keeps every direct file
///     open in the source code to a reviewed list, so a new reader of referenced files cannot bypass the subsystem.
/// </summary>
public partial class FilesTests
{
    /// <summary>
    ///     The source files (paths relative to <c>src/</c>, a trailing slash for a folder) allowed to open files directly,
    ///     with the reason. Everything else that reads a file a document references must go through the subsystem.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedOpeners = new(StringComparer.Ordinal)
    {
        ["DemaConsulting.CanvasNet/Files/"] = "The subsystem itself: the resolvers and the real-path unit.",
        ["DemaConsulting.CanvasNet/Codecs/BmpCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/GifCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/Jpeg2000Codec.cs"] = "Load of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/Jpeg/JpegCodec.Exif.cs"] = "Load of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/JpegCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/PngCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet/Codecs/TiffCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet/Fonts/FontDirectoryScanner.cs"] = "Font folders the host configures, not documents.",
        ["DemaConsulting.CanvasNet/Fonts/TrueTypeFont.cs"] = "Font files the host configures, not documents.",
        ["DemaConsulting.CanvasNet.Mermaid/MermaidDocument.cs"] = "Open of the top-level document the caller names.",
        ["DemaConsulting.CanvasNet.Pdf/PdfDocument.cs"] = "Open of the top-level document the caller names.",
        ["DemaConsulting.CanvasNet.Pptx/PptxDocument.cs"] = "Open of the top-level document the caller names.",
        ["DemaConsulting.CanvasNet.Scad/ScadDocument.cs"] = "Open of the top-level document the caller names.",
        ["DemaConsulting.CanvasNet.Svg/SvgCodec.cs"] = "Load and Save of a file the caller names.",
        ["DemaConsulting.CanvasNet.Tool/Validation.cs"] = "The tool's own self-validation files.",
        ["DemaConsulting.CanvasNet.VegaLite/VegaLiteDocument.cs"] = "Open of the top-level document the caller names.",
        ["DemaConsulting.CanvasNet.Viewer/Imaging/ImageWriter.cs"] = "Output files the caller names.",
        ["DemaConsulting.CanvasNet.Viewer/Imaging/SourceLoader.Input.cs"] = "The top-level input the caller names.",
        ["DemaConsulting.CanvasNet.Viewer/Imaging/SvgFlattener.cs"] = "The top-level input and the output files the caller names.",
        ["DemaConsulting.CanvasNet.Vsdx/VsdxDocument.cs"] = "Open of the top-level document the caller names.",
    };

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that relative, dot-dot and absolute references are read under the default policy, and that a
    ///     missing reference is no file rather than a failure.
    /// </summary>
    [Fact]
    public void Files_ReferenceResolution_RelativeDotDotAndAbsoluteReferences_AreReadUnderTheDefaultPolicy()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("docs/images/a.txt", "relative");
        directory.Write("assets/b.txt", "dot-dot");
        var absolute = directory.Write("elsewhere/c.txt", "absolute");
        var resolver = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = directory.Combine("docs") }, Ct);

        // Act
        var relative = resolver.Resolve("images/a.txt");
        var dotDot = resolver.Resolve("../assets/b.txt");
        var rooted = resolver.Resolve(absolute);
        var missing = resolver.Resolve("images/none.txt");

        // Assert
        Assert.Equal("relative", Encoding.UTF8.GetString(relative!.Content.Span));
        Assert.Equal("dot-dot", Encoding.UTF8.GetString(dotDot!.Content.Span));
        Assert.Equal("absolute", Encoding.UTF8.GetString(rooted!.Content.Span));
        Assert.Null(missing);
    }

    /// <summary>
    ///     Validates that roots and no-symlinks combined read a plain file inside the root and deny an escape with
    ///     dot-dot and a file reached through a link inside the root.
    /// </summary>
    [Fact]
    public void Files_AccessPolicy_RootsAndNoSymlinksCombined_ReadsInsideAndDeniesEscapeAndLink()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("root/real/a.txt", "inside");
        directory.Write("outside.txt", "outside");
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("root/alias"), directory.Combine("root/real"));
        var root = directory.Combine("root");
        var policy = FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(root), FileAccessPolicy.NoSymlinks);
        var resolver = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = root, FileAccess = policy }, Ct);

        // Act
        var inside = resolver.Resolve("real/a.txt");
        var escape = Assert.Throws<FileReferenceException>(() => resolver.Resolve("../outside.txt"));
        var link = Assert.Throws<FileReferenceException>(() => resolver.Resolve("alias/a.txt"));

        // Assert
        Assert.Equal("inside", Encoding.UTF8.GetString(inside!.Content.Span));
        Assert.Equal(FileReferenceFailureKind.AccessDenied, escape.Kind);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, link.Kind);
        Assert.Equal("roots+no-symlinks", link.PolicyName);
    }

    /// <summary>
    ///     Validates for the engine and for the sandbox that a change of the tree around the open does not change the
    ///     decision: it follows the file that was opened.
    /// </summary>
    [Fact]
    public void Files_RaceFreeCheck_SwapAfterOpen_DecisionFollowsTheOpenedFile()
    {
        // Arrange: root/sub is a link to the outside; after the open it becomes a real folder with a benign file
        using var directory = new SandboxTempDirectory();
        directory.Write("outside/a.txt", "outside");
        directory.Write("sandbox/sub/a.txt", "inside");
        directory.Write("sandbox-outside/a.txt", "outside");
        Directory.CreateDirectory(directory.Combine("root"));
        FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("root/sub"), directory.Combine("outside"));
        var root = directory.Combine("root");
        var engine = new FileReferenceResolver(
            new FileReferenceOptions { BaseDirectory = root, FileAccess = FileAccessPolicy.UnderRoots(root) },
            Ct,
            new FileOpenHooks
            {
                AfterOpen = _ =>
                {
                    FileTestSupport.RemoveDirectoryLink(directory.Combine("root/sub"));
                    directory.Write("root/sub/a.txt", "benign");
                },
            });
        var sandbox = new SandboxedFileResolver(
            new SandboxOptions { BaseDirectory = directory.Combine("sandbox") },
            Ct,
            null,
            null,
            new FileOpenHooks
            {
                BeforeOpen = _ =>
                {
                    Directory.Move(directory.Combine("sandbox/sub"), directory.Combine("sandbox/sub-old"));
                    FileTestSupport.CreateDirectoryLinkOrSkip(directory.Combine("sandbox/sub"), directory.Combine("sandbox-outside"));
                },
            });

        // Act
        var engineError = Assert.Throws<FileReferenceException>(() => engine.Resolve("sub/a.txt"));
        var sandboxError = Assert.Throws<SandboxException>(() => sandbox.Resolve("sub/a.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, engineError.Kind);
        Assert.Equal(SandboxFailureKind.ThroughLink, sandboxError.Kind);
    }

    /// <summary>
    ///     Validates that network and device references, a directory, and (on Linux and macOS) a device reached by an
    ///     absolute path are refused even under the default policy.
    /// </summary>
    [Fact]
    public void Files_AlwaysRefused_NetworkDeviceAndNonRegularReferences_AreRefusedUnderAllowAll()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        Directory.CreateDirectory(directory.Combine("folder"));
        var resolver = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = directory.Path }, Ct);

        // Act
        var network = Assert.Throws<FileReferenceException>(() => resolver.Resolve("\\\\server\\share\\a.png"));
        var host = Assert.Throws<FileReferenceException>(() => resolver.Resolve("//server/share/a.png"));
        var device = Assert.Throws<FileReferenceException>(() => resolver.Resolve("\\\\.\\PhysicalDrive0"));
        var stream = Assert.Throws<FileReferenceException>(() => resolver.Resolve("a.txt:hidden"));
        var folder = Assert.Throws<FileReferenceException>(() => resolver.Resolve("folder"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.NetworkPath, network.Kind);
        Assert.Equal(FileReferenceFailureKind.NetworkPath, host.Kind);
        Assert.Equal(FileReferenceFailureKind.InvalidPath, device.Kind);
        Assert.Equal(FileReferenceFailureKind.InvalidPath, stream.Kind);
        Assert.Equal(FileReferenceFailureKind.NotARegularFile, folder.Kind);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(FileReferenceFailureKind.NotARegularFile, Assert.Throws<FileReferenceException>(() => resolver.Resolve("/dev/zero")).Kind);
        }
    }

    /// <summary>
    ///     Validates that a denial carries the reference, the policy, the reason and the paths, while its message names
    ///     the reference and the policy but no host directory.
    /// </summary>
    [Fact]
    public void Files_ReferenceFailureReporting_DenialCarriesFactsWithoutHostPathInMessage()
    {
        // Arrange
        using var directory = new SandboxTempDirectory();
        directory.Write("secret/key.txt", "key");
        Directory.CreateDirectory(directory.Combine("docs"));
        var resolver = new FileReferenceResolver(
            new FileReferenceOptions
            {
                BaseDirectory = directory.Combine("docs"),
                FileAccess = FileAccessPolicy.UnderRoots(directory.Combine("docs")),
            },
            Ct);

        // Act
        var ex = Assert.Throws<FileReferenceException>(() => resolver.Resolve("../secret/key.txt"));

        // Assert
        Assert.Equal(FileReferenceFailureKind.AccessDenied, ex.Kind);
        Assert.Equal("../secret/key.txt", ex.Reference);
        Assert.Equal("roots", ex.PolicyName);
        Assert.NotNull(ex.Reason);
        Assert.NotNull(ex.Hint);
        Assert.Equal(Path.GetFullPath(directory.Combine("secret/key.txt")), ex.RequestedPath);
        Assert.Contains("'../secret/key.txt'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'roots'", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every direct file open in the source code is in a reviewed file, so a new reader of
    ///     referenced files cannot silently bypass the subsystem and its policy.
    /// </summary>
    [Fact]
    public void Files_SourceTree_DirectFileOpens_AreOnlyInAllowlistedFiles()
    {
        // Act: every file that opens a file directly and is not allowed
        var offenders = Openers()
            .Where(opener => !IsAllowed(opener.File))
            .Select(opener => $"{opener.File}: {opener.Line}")
            .ToList();

        // Assert
        Assert.True(
            offenders.Count == 0,
            "Referenced files must be read through DemaConsulting.CanvasNet.Files; route these opens through it, or add "
            + "the file to the allow list with the reason it never reads a referenced file:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    ///     Validates that every entry of the allow list still opens a file, so the list cannot go stale.
    /// </summary>
    [Fact]
    public void Files_SourceTree_EveryAllowlistEntry_StillOpensAFile()
    {
        // Arrange
        var files = Openers().Select(opener => opener.File).ToHashSet(StringComparer.Ordinal);

        // Act
        var stale = AllowedOpeners.Keys
            .Where(entry => !files.Any(file => Matches(file, entry)))
            .ToList();

        // Assert
        Assert.True(stale.Count == 0, "These allow-list entries no longer open a file: " + string.Join(", ", stale));
    }

    /// <summary>
    ///     Gets the pattern of a direct file open: a <see cref="FileStream"/> constructed from a path, or a static
    ///     <see cref="File"/> method that opens or reads a file.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<![\w.])(?:System\.IO\.)?File\.(?:Open|OpenRead|OpenText|OpenHandle|ReadAllBytes|ReadAllText|ReadAllLines|ReadLines)\(|\bnew\s+FileStream\(")]
    private static partial Regex OpenerPattern();

    /// <summary>
    ///     Finds every line of the source code that opens a file directly, ignoring comments.
    /// </summary>
    /// <returns>The source file (relative to <c>src/</c>, forward slashes) and the trimmed line.</returns>
    private static IEnumerable<(string File, string Line)> Openers()
    {
        var source = FindSourceRoot();
        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("//", StringComparison.Ordinal) && OpenerPattern().IsMatch(trimmed))
                {
                    yield return (relative, trimmed);
                }
            }
        }
    }

    /// <summary>
    ///     Determines whether a source file is on the allow list.
    /// </summary>
    /// <param name="file">The source file, relative to <c>src/</c>.</param>
    /// <returns><see langword="true"/> when it is allowed.</returns>
    private static bool IsAllowed(string file) => AllowedOpeners.Keys.Any(entry => Matches(file, entry));

    /// <summary>
    ///     Determines whether a source file matches an allow-list entry (a file, or a folder with a trailing slash).
    /// </summary>
    /// <param name="file">The source file.</param>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    private static bool Matches(string file, string entry) =>
        entry.EndsWith('/') ? file.StartsWith(entry, StringComparison.Ordinal) : file == entry;

    /// <summary>
    ///     Finds the <c>src</c> folder by walking up from the test assembly to the repository root.
    /// </summary>
    /// <returns>The folder.</returns>
    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (File.Exists(Path.Combine(directory.FullName, "CanvasNet.slnx")) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "The repository root (the folder with CanvasNet.slnx) was not found above " + AppContext.BaseDirectory
            + "; the source-tree guard reads the source files.");
    }
}
