// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="FontResolver"/>, <see cref="FontResolverOptions"/>, <see cref="ResolvedFont"/> and
///     <see cref="FontSource"/>. The additional directories and (through the internal seam) the installed fonts are
///     injected temporary directories populated with copies of the bundled Liberation fonts, so no test depends on
///     what happens to be installed on the machine running the tests.
/// </summary>
public class FontResolverTests
{
    /// <summary>The bundled Liberation Serif bold face's file name.</summary>
    private const string SerifBold = "LiberationSerif-Bold.ttf";

    /// <summary>The bundled Liberation Serif italic face's file name.</summary>
    private const string SerifItalic = "LiberationSerif-Italic.ttf";

    /// <summary>The bundled Liberation Mono regular face's file name.</summary>
    private const string MonoRegular = "LiberationMono-Regular.ttf";

    /// <summary>
    ///     Proves that no, an empty and a blank family list resolve to the bundled Liberation Sans in the requested
    ///     style, without a failure, and that the CSS generic keywords resolve to the bundled font of their design.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_DefaultAndGenericKeywords_UseTheBundledFonts()
    {
        var resolver = new FontResolver();
        var sans = SystemFontCatalog.LoadBundledFallback(false, false, false, false);
        var serif = SystemFontCatalog.LoadBundledFallback(true, false, false, false);
        var mono = SystemFontCatalog.LoadBundledFallback(false, true, false, false);

        foreach (var list in new string?[] { null, string.Empty, "  ", " , ", "sans-serif", "system-ui", "cursive", "fantasy", "'SANS-SERIF', serif" })
        {
            var resolved = resolver.Resolve(list, false, false, out var failure);

            Assert.Null(failure);
            Assert.Same(sans, resolved.Font);
            Assert.Equal(("Liberation Sans", FontSource.Bundled, false, false), (resolved.Family, resolved.Source, resolved.Bold, resolved.Italic));
        }

        var serifResolved = resolver.Resolve("ui-serif", false, false, out var serifFailure);
        var monoResolved = resolver.Resolve("Missing, 'monospace'", false, false, out var monoFailure);

        Assert.Same(serif, serifResolved.Font);
        Assert.Equal("Liberation Serif", serifResolved.Family);
        Assert.Same(mono, monoResolved.Font);
        Assert.Equal("Liberation Mono", monoResolved.Family);
        Assert.Null(serifFailure);
        Assert.Null(monoFailure);
    }

    /// <summary>
    ///     Proves that the requested bold and italic flags select the bundled style variant and are reported, and
    ///     that asking again returns the same resolution.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_BoldAndItalic_SelectTheBundledStyleVariant()
    {
        var resolver = new FontResolver();

        var regular = resolver.Resolve("serif", false, false, out _);
        var boldItalic = resolver.Resolve("serif", true, true, out _);
        var again = resolver.Resolve("serif", true, true, out _);

        Assert.Same(SystemFontCatalog.LoadBundledFallback(true, false, false, false), regular.Font);
        Assert.Same(SystemFontCatalog.LoadBundledFallback(true, false, true, true), boldItalic.Font);
        Assert.Equal((true, true), (boldItalic.Bold, boldItalic.Italic));
        Assert.Same(boldItalic, again);
    }

    /// <summary>
    ///     Proves that a family that is found nowhere is reported as a failure and falls to Liberation Sans, that a
    ///     later family in the list that is met clears the failure, and that the common aliases are looked up like any
    ///     other name unless the options map them.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_UnknownFamily_FallsBackToSansAndReportsWhyOnlyWhenNothingWasMet()
    {
        var resolver = new FontResolver();
        var sans = SystemFontCatalog.LoadBundledFallback(false, false, true, false);

        var unknown = resolver.Resolve("No Such Family", true, false, out var unknownFailure);
        var rescued = resolver.Resolve("No Such Family, serif", false, false, out var rescuedFailure);
        var arial = resolver.Resolve("Arial", true, false, out var arialFailure);

        Assert.Same(sans, unknown.Font);
        Assert.Equal("no such font family was found", unknownFailure);
        Assert.Equal("Liberation Serif", rescued.Family);
        Assert.Null(rescuedFailure);
        Assert.Same(sans, arial.Font);
        Assert.Equal("no such font family was found", arialFailure);
    }

    /// <summary>
    ///     Proves that with <see cref="FontResolverOptions.MapCommonAliasesToBundled"/> the common aliases resolve to
    ///     the bundled font of their design, before any additional directory is consulted, and that without it the
    ///     same name is found in the directory.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_CommonAliases_AreMappedBeforeDirectoriesOnlyWhenRequested()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        var mapped = new FontResolver(new FontResolverOptions { AdditionalDirectories = [directory.Path], MapCommonAliasesToBundled = true });
        var unmapped = new FontResolver(new FontResolverOptions { AdditionalDirectories = [directory.Path] });

        // "Liberation Serif" and "Times New Roman" are aliases of the serif design; the directory holds the former
        var liberation = mapped.Resolve("Liberation Serif", true, false, out _);
        var times = mapped.Resolve("Times New Roman", false, false, out var timesFailure);
        var fromDirectory = unmapped.Resolve("Liberation Serif", false, false, out _);

        Assert.Equal(FontSource.Bundled, liberation.Source);
        Assert.Equal(FontSource.Bundled, times.Source);
        Assert.Equal("Liberation Serif", times.Family);
        Assert.Null(timesFailure);
        Assert.Equal(FontSource.AdditionalDirectory, fromDirectory.Source);
    }

    /// <summary>
    ///     Proves the order of the lookup for a named family: the additional directories (in the order given), then
    ///     the installed fonts (only when enabled), then the bundled family names - and that the closest style wins
    ///     within a source, with the first face found winning a tie.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_NamedFamily_SearchesDirectoriesThenSystemThenBundledNames()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        using var system = new TempDirectory();
        first.Write(SerifBold);
        second.Write(SerifItalic);
        system.Write(SerifItalic);
        system.Write(MonoRegular);
        var systemFaces = FontDirectoryScanner.Scan([system.Path], cancellationToken: TestContext.Current.CancellationToken);

        // Bold italic is one flag from either serif face: the first directory wins the tie
        var directoriesFirst = new FontResolver(
            new FontResolverOptions { AdditionalDirectories = [first.Path, second.Path], UseSystemFonts = true },
            default,
            () => systemFaces);
        var directoriesSecond = new FontResolver(
            new FontResolverOptions { AdditionalDirectories = [second.Path, first.Path] },
            default,
            () => systemFaces);
        var systemOnly = new FontResolver(new FontResolverOptions { UseSystemFonts = true }, default, () => systemFaces);
        var bundledOnly = new FontResolver(new FontResolverOptions { UseSystemFonts = false }, default, () => systemFaces);

        var fromFirst = directoriesFirst.Resolve("liberation serif", true, true, out var firstFailure);
        var fromSecond = directoriesSecond.Resolve("Liberation Serif", true, true, out _);
        var exactItalic = directoriesFirst.Resolve("Liberation Serif", false, true, out _);
        var installed = systemOnly.Resolve("Liberation Mono", false, false, out var systemFailure);
        var installedAbsent = systemOnly.Resolve("Liberation Sans", false, false, out _);
        var bundledName = bundledOnly.Resolve("Liberation Mono", true, false, out var bundledFailure);

        Assert.Equal((FontSource.AdditionalDirectory, true, false), (fromFirst.Source, fromFirst.Bold, fromFirst.Italic));
        Assert.Null(firstFailure);
        Assert.Equal((FontSource.AdditionalDirectory, false, true), (fromSecond.Source, fromSecond.Bold, fromSecond.Italic));
        Assert.Equal((FontSource.AdditionalDirectory, false, true), (exactItalic.Source, exactItalic.Bold, exactItalic.Italic));
        Assert.Equal((FontSource.System, "Liberation Mono"), (installed.Source, installed.Family));
        Assert.True(installed.Font.IsFixedPitch);
        Assert.Null(systemFailure);

        // A family that no source has but that is a bundled family name resolves to the bundled font of that name
        Assert.Equal((FontSource.Bundled, "Liberation Sans"), (installedAbsent.Source, installedAbsent.Family));
        Assert.Equal((FontSource.Bundled, "Liberation Mono", true, false), (bundledName.Source, bundledName.Family, bundledName.Bold, bundledName.Italic));
        Assert.Same(SystemFontCatalog.LoadBundledFallback(false, true, true, false), bundledName.Font);
        Assert.Null(bundledFailure);
    }

    /// <summary>
    ///     Proves that the installed fonts are not even asked for when they are disabled, and are not consulted for a
    ///     generic keyword.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_SystemFontsDisabled_NeverAsksForTheInstalledFaces()
    {
        var resolver = new FontResolver(
            new FontResolverOptions { UseSystemFonts = false },
            default,
            () => throw new InvalidOperationException("The installed faces must not be read."));
        var generic = new FontResolver(
            new FontResolverOptions { UseSystemFonts = true },
            default,
            () => throw new InvalidOperationException("The installed faces must not be read."));

        var named = resolver.Resolve("Some Family", false, false, out var failure);
        var keyword = generic.Resolve("monospace", false, false, out _);

        Assert.Equal(FontSource.Bundled, named.Source);
        Assert.Equal("no such font family was found", failure);
        Assert.Equal("Liberation Mono", keyword.Family);
    }

    /// <summary>
    ///     Proves that a face that is found but cannot be loaded makes its family fail (the installed fonts and the
    ///     bundled names are not tried for it), reports the file in the failure, and lets a later family in the list
    ///     be met.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_FoundFaceThatCannotBeLoaded_FailsTheFamilyAndReportsTheFile()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        var vanishing = directory.Write(MonoRegular);
        var resolver = new FontResolver(new FontResolverOptions { AdditionalDirectories = [directory.Path] });

        // The first request scans the directory; the file of the monospace face is then removed
        var present = resolver.Resolve("Liberation Serif", true, false, out var presentFailure);
        File.Delete(vanishing);
        var vanished = resolver.Resolve("Liberation Mono", false, false, out var vanishedFailure);
        var rescued = resolver.Resolve("Liberation Mono, serif", false, false, out var rescuedFailure);

        Assert.Equal(FontSource.AdditionalDirectory, present.Source);
        Assert.Null(presentFailure);

        // Not the bundled Liberation Mono: the family was found in the directory, so it is spent
        Assert.Equal((FontSource.Bundled, "Liberation Sans"), (vanished.Source, vanished.Family));
        Assert.Equal($"the font file '{vanishing}' could not be loaded", vanishedFailure);
        Assert.Equal("Liberation Serif", rescued.Family);
        Assert.Null(rescuedFailure);
    }

    /// <summary>
    ///     Proves that the scan of the additional directories is bounded by <see cref="FontResolverOptions.ScanLimits"/>
    ///     and that a missing directory or a file that is not a font is skipped.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_AdditionalDirectories_AreScannedWithTheLimitsAndSkipUnusableInput()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold, "a.ttf");
        directory.Write(SerifItalic, "b.ttf");
        File.WriteAllText(Path.Join(directory.Path, "c.ttf"), "not a font");
        var missing = Path.Join(directory.Path, "missing");

        var unbounded = new FontResolver(new FontResolverOptions { AdditionalDirectories = [missing, directory.Path] });
        var oneFile = new FontResolver(new FontResolverOptions
        {
            AdditionalDirectories = [missing, directory.Path],
            ScanLimits = new FontScanLimits { MaxFontFiles = 1 },
        });

        var all = unbounded.Resolve("Liberation Serif", false, true, out _);
        var limited = oneFile.Resolve("Liberation Serif", false, true, out _);

        // With both files the italic face is exact; with one file only the first (bold) face was scanned
        Assert.Equal((FontSource.AdditionalDirectory, true), (all.Source, all.Italic));
        Assert.Equal((FontSource.AdditionalDirectory, false, true), (limited.Source, limited.Italic, limited.Bold));
    }

    /// <summary>
    ///     Proves that a canceled token stops the scan with <see cref="OperationCanceledException"/>, that the
    ///     canceled scan is not kept, and that a resolver with no additional directory never scans (so never throws).
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_CanceledToken_StopsTheScanOnlyWhenThereIsOneToDo()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var scanning = new FontResolver(new FontResolverOptions { AdditionalDirectories = [directory.Path] }, cts.Token);
        var notScanning = new FontResolver(null, cts.Token);

        Assert.Throws<OperationCanceledException>(() => scanning.Resolve("Liberation Serif", false, false, out _));
        Assert.Throws<OperationCanceledException>(() => scanning.Resolve("Liberation Serif", false, false, out _));

        // A keyword never needs the directories
        Assert.Equal("Liberation Serif", scanning.Resolve("serif", false, false, out _).Family);
        Assert.Equal("Liberation Sans", notScanning.Resolve("Whatever", false, false, out _).Family);
    }

    /// <summary>
    ///     Proves that the resolver copies the directory list it is given, and rejects options with null members.
    /// </summary>
    [Fact]
    public void FontResolver_Constructor_CopiesDirectoriesAndRejectsNullMembers()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        var directories = new List<string>();
        var resolver = new FontResolver(new FontResolverOptions { AdditionalDirectories = directories });
        directories.Add(directory.Path);

        var resolved = resolver.Resolve("Liberation Serif", true, false, out _);

        Assert.Equal(FontSource.Bundled, resolved.Source);
        Assert.Throws<ArgumentNullException>(() => new FontResolver(new FontResolverOptions { AdditionalDirectories = null! }));
        Assert.Throws<ArgumentNullException>(() => new FontResolver(new FontResolverOptions { ScanLimits = null! }));
        Assert.False(new FontResolverOptions().UseSystemFonts);
        Assert.False(new FontResolverOptions().MapCommonAliasesToBundled);
        Assert.Empty(new FontResolverOptions().AdditionalDirectories);
        Assert.Equal(new FontScanLimits(), new FontResolverOptions().ScanLimits);
    }

    /// <summary>
    ///     Proves that a face of an additional directory is cached by the resolver that loaded it (so it is released
    ///     with that resolver) and not shared with another resolver over the same directory, and that one resolver
    ///     reuses its own face for different requests that select it.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_DirectoryFaces_AreCachedPerResolver()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        var options = new FontResolverOptions { AdditionalDirectories = [directory.Path] };
        var first = new FontResolver(options);
        var second = new FontResolver(options);

        var one = first.Resolve("Liberation Serif", true, false, out _);
        var sameFace = first.Resolve("missing, 'liberation serif'", true, false, out _);
        var other = second.Resolve("Liberation Serif", true, false, out _);

        Assert.Equal(FontSource.AdditionalDirectory, one.Source);
        Assert.NotSame(one, sameFace);
        Assert.Same(one.Font, sameFace.Font);
        Assert.NotSame(one.Font, other.Font);
    }

    /// <summary>
    ///     Proves that concurrent requests for one family list share one scan and one result font.
    /// </summary>
    [Fact]
    public void FontResolver_Resolve_ConcurrentRequests_ShareOneResultFont()
    {
        using var directory = new TempDirectory();
        directory.Write(SerifBold);
        var resolver = new FontResolver(new FontResolverOptions { AdditionalDirectories = [directory.Path] });

        var fonts = new TrueTypeFont[64];
        Parallel.For(0, fonts.Length, i => fonts[i] = resolver.Resolve("Liberation Serif", true, false, out _).Font);

        Assert.All(fonts, font => Assert.Same(fonts[0], font));
    }

    /// <summary>
    ///     A temporary directory, deleted with its content.
    /// </summary>
    private sealed class TempDirectory : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDirectory"/> class, creating the directory.</summary>
        public TempDirectory() =>
            Path = Directory.CreateDirectory(
                System.IO.Path.Join(System.IO.Path.GetTempPath(), "canvasnet-test-resolver-" + Guid.NewGuid())).FullName;

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
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
