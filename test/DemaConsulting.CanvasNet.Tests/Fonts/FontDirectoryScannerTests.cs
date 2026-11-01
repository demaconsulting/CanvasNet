// cspell:ignore canvasnet mklink noextension
using System.Diagnostics;
using System.Runtime.InteropServices;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="FontDirectoryScanner"/> and <see cref="FontScanLimits"/>. Every test
///     scans a controlled temporary directory populated with copies of the bundled Liberation fonts,
///     so none depends on what happens to be installed on the machine running the tests. The link
///     tests create their links without administrator privileges where the platform allows it (a
///     symbolic link on Unix, a junction on Windows) and skip themselves where it does not.
/// </summary>
public class FontDirectoryScannerTests
{
    /// <summary>The bundled Liberation Sans regular face's file name.</summary>
    private const string Regular = "LiberationSans-Regular.ttf";

    /// <summary>The bundled Liberation Sans bold face's file name.</summary>
    private const string Bold = "LiberationSans-Bold.ttf";

    /// <summary>The bundled Liberation Serif italic face's file name.</summary>
    private const string SerifItalic = "LiberationSerif-Italic.ttf";

    /// <summary>The bundled Liberation Mono regular face's file name.</summary>
    private const string Mono = "LiberationMono-Regular.ttf";

    /// <summary>
    ///     Proves that a scan reports the family, style and absolute path of each face it finds, reading the
    ///     headers of a real font.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_BundledFonts_ReportsFamilyStyleAndAbsolutePath()
    {
        using var directory = new TempDirectory();
        directory.Write(Bold);
        directory.Write(SerifItalic);

        var result = Scan([directory.Path]);

        Assert.Equal(2, result.Count);
        var bold = Assert.Single(result, f => f.FamilyName == "Liberation Sans");
        Assert.True(bold.Bold);
        Assert.False(bold.Italic);
        Assert.False(bold.FixedPitch);
        Assert.Equal(Path.Join(directory.Path, Bold), bold.FilePath);
        Assert.Equal(0, bold.FaceIndex);
        var italic = Assert.Single(result, f => f.FamilyName == "Liberation Serif");
        Assert.True(italic.Italic);
        Assert.False(italic.Bold);
    }

    /// <summary>
    ///     Proves that the result is the same, in the same order, on every scan, and that the order is
    ///     the traversal order - a directory's font files in path order, then its subdirectories in path order -
    ///     regardless of the order the files were created in.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_NestedDirectories_ResultIsDeterministicallyOrdered()
    {
        using var directory = new TempDirectory();
        directory.Write(Mono, "z.ttf");
        directory.Write(Regular, "B.ttf");
        directory.Write(Bold, Path.Join("sub", "a.ttf"));
        directory.Write(SerifItalic, Path.Join("sub", "deeper", "c.ttf"));
        directory.Write(Bold, Path.Join("Another", "m.ttf"));
        directory.Write(Regular, "a.ttf");

        var first = Scan([directory.Path]);
        var second = Scan([directory.Path]);

        // Case-insensitive alphabetical (an ordinal order would put B.ttf and Another first)
        var expected = new[]
            {
                "a.ttf", "B.ttf", "z.ttf",
                Path.Join("Another", "m.ttf"),
                Path.Join("sub", "a.ttf"),
                Path.Join("sub", "deeper", "c.ttf"),
            }
            .Select(name => Path.Join(directory.Path, name))
            .ToArray();
        Assert.Equal(expected, first.Select(f => f.FilePath));
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Proves that roots are scanned in the order given and that a file reachable from two roots is reported once.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_SeveralRoots_RootOrderKeptAndDuplicatesReportedOnce()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        first.Write(Regular, "z.ttf");
        second.Write(Bold, "a.ttf");

        var result = Scan([first.Path, second.Path, first.Path, Path.Join(first.Path, ".")]);

        Assert.Equal(
            [Path.Join(first.Path, "z.ttf"), Path.Join(second.Path, "a.ttf")],
            result.Select(f => f.FilePath));
    }

    /// <summary>
    ///     Proves that a bound that is reached ends the scan before the remaining roots are even enumerated.
    /// </summary>
    [Theory]
    [InlineData(1, 5_000)]
    [InlineData(5_000, 1)]
    public void FontDirectoryScanner_Scan_BoundReached_RemainingRootsAreNotEnumerated(int maxFontFiles, int maxFaces)
    {
        using var directory = new TempDirectory();
        directory.Write(Regular, "a.ttf");
        directory.Write(Bold, "b.ttf");

        // Throws when the scan asks for a root after the first: it must have stopped by then
        static IEnumerable<string> RootThenFail(string root)
        {
            yield return root;
            throw new InvalidOperationException("The scan went on past a bound that was reached.");
        }

        var result = FontDirectoryScanner.Scan(
            RootThenFail(directory.Path),
            new FontScanLimits { MaxFontFiles = maxFontFiles, MaxFaces = maxFaces },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Path.Join(directory.Path, "a.ttf"), Assert.Single(result).FilePath);
    }

    /// <summary>
    ///     Proves that a root given more than once does not consume the font-file bound again: only unique files count.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_RepeatedRoot_DuplicatesDoNotConsumeTheFontFileBound()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        first.Write(Regular, "a.ttf");
        second.Write(Bold, "b.ttf");

        var result = Scan([first.Path, first.Path, second.Path], new FontScanLimits { MaxFontFiles = 2 });

        Assert.Equal(
            [Path.Join(first.Path, "a.ttf"), Path.Join(second.Path, "b.ttf")],
            result.Select(f => f.FilePath));
    }

    /// <summary>
    ///     Proves that a collection header declaring more faces than the file can hold is rejected without
    ///     reading or sizing anything from the count, and that a declared count that fits the file is still
    ///     bounded by the face bound (the faces after it are not read).
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_CraftedCollectionHeaders_AreBoundedByTheFileAndTheFaceLimit()
    {
        using var directory = new TempDirectory();
        File.WriteAllBytes(Path.Join(directory.Path, "a-huge.ttc"), CollectionHeader(uint.MaxValue, 0));
        File.WriteAllBytes(Path.Join(directory.Path, "b-big.ttc"), CollectionHeader(10_000, 10_000));
        directory.Write(Regular, "z.ttf");

        var result = Scan([directory.Path], new FontScanLimits { MaxFaces = 2 });

        // The huge declaration is rejected (and costs no face); the large one uses up the face bound
        Assert.Empty(result);
        Assert.Equal(Path.Join(directory.Path, "z.ttf"), Assert.Single(Scan([directory.Path], new FontScanLimits { MaxFaces = 10_003 })).FilePath);
    }

    /// <summary>
    ///     Builds a collection header declaring a face count, followed by an offset table of the given length
    ///     whose entries all point at the start of the file (so no face parses).
    /// </summary>
    /// <param name="declaredCount">The face count to declare.</param>
    /// <param name="tableEntries">The number of offset-table entries to include.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] CollectionHeader(uint declaredCount, int tableEntries)
    {
        var bytes = new byte[12 + (tableEntries * 4)];
        "ttcf"u8.CopyTo(bytes);
        bytes[5] = 1; // version 1.0
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), declaredCount);
        return bytes;
    }

    /// <summary>
    ///     Proves that a missing, empty or <see langword="null"/> root contributes nothing and does not stop the
    ///     other roots from being scanned.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_MissingEmptyAndNullRoots_AreSkipped()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular);

        var result = Scan(
            [Path.Join(directory.Path, "missing"), string.Empty, null!, "\0invalid", directory.Path]);

        Assert.Equal(Path.Join(directory.Path, Regular), Assert.Single(result).FilePath);
    }

    /// <summary>
    ///     Proves that the font extensions are recognized in any case, that other files are ignored, and that an
    ///     unparseable font file is skipped without hiding its siblings.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_ExtensionsAndUnparseableFiles_OnlyWellFormedFontsReported()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular, "a.ttf");
        directory.Write(Regular, "b.OTF");
        directory.Write(Regular, "c.ttc");
        directory.Write(Regular, "d.OtC");
        directory.Write(Regular, "e.woff");
        directory.Write(Regular, "f.txt");
        directory.Write(Regular, "noextension");
        File.WriteAllBytes(Path.Join(directory.Path, "g.ttf"), [0x00, 0x01, 0x02, 0x03, 0x04]);
        File.WriteAllBytes(Path.Join(directory.Path, "h.ttf"), []);

        var result = Scan([directory.Path]);

        Assert.Equal(
            ["a.ttf", "b.OTF", "c.ttc", "d.OtC"],
            result.Select(f => Path.GetFileName(f.FilePath)));
    }

    /// <summary>
    ///     Proves that the font-file bound stops the collection at the first files of a directory in path order.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_FontFileLimit_KeepsTheFirstFilesInPathOrder()
    {
        using var directory = new TempDirectory();
        foreach (var name in new[] { "d.ttf", "b.ttf", "a.ttf", "c.ttf" })
        {
            directory.Write(Regular, name);
        }

        var result = Scan([directory.Path], new FontScanLimits { MaxFontFiles = 2 });

        Assert.Equal(["a.ttf", "b.ttf"], result.Select(f => Path.GetFileName(f.FilePath)));
    }

    /// <summary>
    ///     Proves that a directory's own font files come before those of its subdirectories - so the first
    ///     of two equally good faces is the one in the parent directory, as in the earlier catalog scan - and
    ///     that the font-file bound counts in that same traversal order.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_RootFileAndChildFile_RootFileComesFirstAndCountsFirst()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular, "z.ttf");
        directory.Write(Regular, Path.Join("a", "a.ttf"));
        var rootFile = Path.Join(directory.Path, "z.ttf");

        var all = Scan([directory.Path]);
        var limited = Scan([directory.Path], new FontScanLimits { MaxFontFiles = 1 });

        Assert.Equal([rootFile, Path.Join(directory.Path, "a", "a.ttf")], all.Select(f => f.FilePath));
        Assert.Equal(rootFile, SystemFontCatalog.FindInFamily(all, "Liberation Sans", false, false)?.FilePath);
        Assert.Equal(rootFile, Assert.Single(limited).FilePath);
    }

    /// <summary>
    ///     Proves that the face bound stops the reading, keeping the faces read before it.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_FaceLimit_KeepsTheFirstFaces()
    {
        using var directory = new TempDirectory();
        foreach (var name in new[] { "d.ttf", "b.ttf", "a.ttf", "c.ttf" })
        {
            directory.Write(Regular, name);
        }

        var result = Scan([directory.Path], new FontScanLimits { MaxFaces = 3 });

        Assert.Equal(["a.ttf", "b.ttf", "c.ttf"], result.Select(f => Path.GetFileName(f.FilePath)));
    }

    /// <summary>
    ///     Proves that the directory bound stops the descent: the root's own fonts are kept, the subdirectory's are
    ///     not read.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_DirectoryLimit_StopsTheDescent()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular, "a.ttf");
        directory.Write(Bold, Path.Join("sub", "b.ttf"));

        var result = Scan([directory.Path], new FontScanLimits { MaxDirectories = 1 });

        Assert.Equal(Path.Join(directory.Path, "a.ttf"), Assert.Single(result).FilePath);
    }

    /// <summary>
    ///     Proves that the entry bound counts a directory's entries as a whole: a directory the remaining
    ///     budget cannot cover is not used at all, whatever order the file system lists its entries in, while
    ///     the complete directories before it are kept.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_EntryLimit_StopsTheExamination()
    {
        using var tooBig = new TempDirectory();
        foreach (var name in new[] { "a.ttf", "b.ttf", "c.ttf" })
        {
            tooBig.Write(Regular, name);
        }

        using var nested = new TempDirectory();
        nested.Write(Regular, "a.ttf");
        foreach (var name in new[] { "x.ttf", "y.ttf", "z.ttf" })
        {
            nested.Write(Bold, Path.Join("sub", name));
        }

        // Three entries do not fit in a budget of one: none of them is used
        Assert.Empty(Scan([tooBig.Path], new FontScanLimits { MaxEntries = 1 }));

        // The root's two entries (a font and the subdirectory) fit; the subdirectory's three do not
        var result = Scan([nested.Path], new FontScanLimits { MaxEntries = 2 });
        Assert.Equal(Path.Join(nested.Path, "a.ttf"), Assert.Single(result).FilePath);

        // With room for everything, the subdirectory is read too
        Assert.Equal(4, Scan([nested.Path], new FontScanLimits { MaxEntries = 5 }).Count);
    }

    /// <summary>
    ///     Proves that a bound is at least one, and that the defaults are the documented ones.
    /// </summary>
    [Fact]
    public void FontScanLimits_Properties_RejectLessThanOneAndHaveDocumentedDefaults()
    {
        var defaults = new FontScanLimits();

        Assert.Equal(10_000, defaults.MaxDirectories);
        Assert.Equal(200_000, defaults.MaxEntries);
        Assert.Equal(5_000, defaults.MaxFontFiles);
        Assert.Equal(20_000, defaults.MaxFaces);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontScanLimits { MaxDirectories = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontScanLimits { MaxEntries = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontScanLimits { MaxFontFiles = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontScanLimits { MaxFaces = 0 });
        Assert.Equal(1, new FontScanLimits { MaxFaces = 1 }.MaxFaces);
    }

    /// <summary>
    ///     Proves that a canceled token ends the scan with <see cref="OperationCanceledException"/>, and that a
    ///     <see langword="null"/> directory list is rejected.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_CanceledTokenAndNullDirectories_Throw()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        canceled.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => FontDirectoryScanner.Scan([directory.Path], cancellationToken: canceled.Token));
        Assert.Throws<ArgumentNullException>(
            () => FontDirectoryScanner.Scan(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a subdirectory that is a link is never followed, whether or not linked files are trusted.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FontDirectoryScanner_Scan_LinkedSubdirectory_IsNeverFollowed(bool trustLinkedFiles)
    {
        using var outside = new TempDirectory();
        outside.Write(Bold, "Outside.ttf");
        using var directory = new TempDirectory();
        directory.Write(Regular, "Inside.ttf");
        if (!TryCreateDirectoryLink(Path.Join(directory.Path, "linked"), outside.Path))
        {
            return; // This environment cannot create directory links: nothing more to verify
        }

        var result = Scan([directory.Path], trustLinkedFiles: trustLinkedFiles);

        Assert.Equal(Path.Join(directory.Path, "Inside.ttf"), Assert.Single(result).FilePath);
    }

    /// <summary>
    ///     Proves that a link cycle (a subdirectory linking back to its own parent) neither hangs the scan nor hides
    ///     the real font beside it.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_LinkBackToParent_TerminatesAndFindsTheFont()
    {
        using var directory = new TempDirectory();
        directory.Write(Regular, "Inside.ttf");
        if (!TryCreateDirectoryLink(Path.Join(directory.Path, "loop"), directory.Path))
        {
            return;
        }

        var result = Scan([directory.Path], trustLinkedFiles: true);

        Assert.Equal(Path.Join(directory.Path, "Inside.ttf"), Assert.Single(result).FilePath);
    }

    /// <summary>
    ///     Proves that a root that is a link is refused unless linked files are trusted.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_LinkedRoot_IsRefusedUnlessTrusted()
    {
        using var target = new TempDirectory();
        target.Write(Regular, "Target.ttf");
        using var holder = new TempDirectory();
        var root = Path.Join(holder.Path, "root");
        if (!TryCreateDirectoryLink(root, target.Path))
        {
            return;
        }

        var refused = Scan([root]);
        var trusted = Scan([root], trustLinkedFiles: true);

        Assert.Empty(refused);
        Assert.Equal("Target.ttf", Path.GetFileName(Assert.Single(trusted).FilePath));
    }

    /// <summary>
    ///     Proves that a font file that is a link is skipped unless linked files are trusted.
    /// </summary>
    [Fact]
    public void FontDirectoryScanner_Scan_LinkedFile_IsSkippedUnlessTrusted()
    {
        using var outside = new TempDirectory();
        var real = outside.Write(Bold, "Real.ttf");
        using var directory = new TempDirectory();
        directory.Write(Regular, "Plain.ttf");
        try
        {
            File.CreateSymbolicLink(Path.Join(directory.Path, "Linked.ttf"), real);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return; // This environment cannot create file symbolic links
        }

        var skipped = Scan([directory.Path]);
        var trusted = Scan([directory.Path], trustLinkedFiles: true);

        Assert.Equal(["Plain.ttf"], skipped.Select(f => Path.GetFileName(f.FilePath)));
        Assert.Equal(["Linked.ttf", "Plain.ttf"], trusted.Select(f => Path.GetFileName(f.FilePath)));
    }

    /// <summary>
    ///     Scans with the test's cancellation token.
    /// </summary>
    /// <param name="directories">The roots to scan.</param>
    /// <param name="limits">The bounds, or <see langword="null"/> for the defaults.</param>
    /// <param name="trustLinkedFiles">Whether linked files and roots are trusted.</param>
    /// <returns>The faces found.</returns>
    private static IReadOnlyList<SystemFontInfo> Scan(
        IEnumerable<string> directories,
        FontScanLimits? limits = null,
        bool trustLinkedFiles = false) =>
        FontDirectoryScanner.Scan(directories, limits, trustLinkedFiles, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Creates a directory link without requiring administrator privileges where possible: a symbolic link, or
    ///     on Windows a junction.
    /// </summary>
    /// <param name="link">The path of the link to create.</param>
    /// <param name="target">The directory the link points at.</param>
    /// <returns><see langword="true"/> when the link was created.</returns>
    private static bool TryCreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }
        }

        // A junction needs no privilege on Windows
        var info = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info);
        process?.StandardOutput.ReadToEnd();
        process?.WaitForExit();
        return process?.ExitCode == 0 && Directory.Exists(link);
    }

    /// <summary>
    ///     A temporary directory, deleted with its content (links first, so nothing outside it is touched).
    /// </summary>
    private sealed class TempDirectory : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDirectory"/> class, creating the directory.</summary>
        public TempDirectory() =>
            Path = Directory.CreateDirectory(
                System.IO.Path.Join(System.IO.Path.GetTempPath(), "canvasnet-test-scanner-" + Guid.NewGuid())).FullName;

        /// <summary>Gets the absolute path of the directory.</summary>
        public string Path { get; }

        /// <summary>
        ///     Writes a copy of a bundled font into the directory.
        /// </summary>
        /// <param name="bundledName">The bundled font's file name.</param>
        /// <param name="relativePath">The path to write to, relative to the directory; defaults to the bundled name.</param>
        /// <returns>The absolute path of the file written.</returns>
        public string Write(string bundledName, string? relativePath = null)
        {
            var destination = System.IO.Path.Join(Path, relativePath ?? bundledName);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            using var source = typeof(SystemFontCatalog).Assembly.GetManifestResourceStream(
                "DemaConsulting.CanvasNet.Fonts.BundledFonts." + bundledName)!;
            using var target = File.Create(destination);
            source.CopyTo(target);
            return destination;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            RemoveLinks(Path);
            Directory.Delete(Path, recursive: true);
        }

        /// <summary>
        ///     Removes every directory link below a directory, so that deleting the tree cannot follow one.
        /// </summary>
        /// <param name="directory">The directory.</param>
        private static void RemoveLinks(string directory)
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateDirectories())
            {
                if (entry.LinkTarget is not null)
                {
                    entry.Delete();
                }
                else
                {
                    RemoveLinks(entry.FullName);
                }
            }
        }
    }
}
