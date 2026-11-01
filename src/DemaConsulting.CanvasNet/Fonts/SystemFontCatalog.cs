// cspell:ignore Dejavu Nimbus Consolas ttcf rescanned Segoe LOCALAPPDATA Noto Zapf
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     A single OS-discovered font file's identity, name, and style metadata, as recorded by
///     <see cref="SystemFontCatalog.Fonts"/>.
/// </summary>
/// <param name="FamilyName">
///     The font's <see cref="TrueTypeFont.GetNameInfo"/>-resolved family name, or (when that is
///     <see langword="null"/>) the font file's own name without its extension - every entry in
///     <see cref="SystemFontCatalog.Fonts"/> therefore always has a non-null family name, even
///     for a font with no usable <c>name</c> table.
/// </param>
/// <param name="SubfamilyName">
///     The font's <see cref="TrueTypeFont.GetNameInfo"/>-resolved subfamily name, or
///     <see cref="string.Empty"/> when absent.
/// </param>
/// <param name="Bold">The font's <see cref="TrueTypeFont.IsBold"/> value.</param>
/// <param name="Italic">The font's <see cref="TrueTypeFont.IsItalic"/> value.</param>
/// <param name="FixedPitch">The font's <see cref="TrueTypeFont.IsFixedPitch"/> value.</param>
/// <param name="FilePath">The absolute path of the font file on disk.</param>
/// <param name="FaceIndex">
///     The zero-based face index this entry was loaded from - <c>0</c> for an ordinary
///     (non-collection) font file, or the matched face within a <c>.ttc</c> collection.
/// </param>
public readonly record struct SystemFontInfo(
    string FamilyName,
    string SubfamilyName,
    bool Bold,
    bool Italic,
    bool FixedPitch,
    string FilePath,
    int FaceIndex);

/// <summary>
///     Provides directory-scan-only discovery of fonts installed on the host operating system
///     (<see cref="Fonts"/>), best-effort name/style matching against that catalog
///     (<see cref="FindBestMatch"/>), and a bundled, always-available Liberation Sans/Serif/Mono
///     last-resort fallback font (<see cref="LoadBundledFallback"/>).
/// </summary>
/// <remarks>
///     <para>
///         <c>SystemFontCatalog</c> is a format-agnostic building block: it knows nothing about
///         any particular document format's font-naming conventions (for example a PDF subset
///         tag or a Standard-14 name) - a caller (today, <c>DemaConsulting.CanvasNet.Pdf</c>'s
///         <c>PdfDocument</c>) is responsible for cleaning up a format-specific font name into a
///         plain family-name hint before calling <see cref="FindBestMatch"/>.
///     </para>
///     <para>
///         <see cref="Fonts"/> is built exactly once per process, the first time it is accessed,
///         and is never rescanned afterward - installing or removing a font on the host machine
///         while the process is running is not detected. Both <see cref="Fonts"/>'s scan itself
///         and <see cref="LoadBundledFallback"/> are deliberately tolerant of failure: a missing
///         or inaccessible scan directory, an unparseable candidate font file, or a single
///         corrupt face within an otherwise well-formed <c>.ttc</c> collection is silently
///         skipped rather than aborting the whole catalog build.
///     </para>
///     <para>
///         <b>Thread safety</b>: every member of this class is safe to call concurrently from
///         multiple threads, with no external synchronization required. <see cref="Fonts"/> is
///         backed by a <see cref="Lazy{T}"/> constructed with
///         <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> - concurrent first-time
///         callers block until a single winning thread finishes the directory scan, after which
///         every caller (concurrent or later) observes that same published result; the scan
///         itself is never run more than once per process, and no caller can observe a
///         partially-built catalog. <see cref="LoadBundledFallback"/>'s process-lifetime cache
///         (<see cref="BundledFontCache"/>) is guarded by a dedicated lock
///         (<see cref="BundledFontLock"/>) around both its lookup and its populate-on-miss path,
///         so concurrent requests for the same bundled style can never race on the cache
///         dictionary or load/parse the same embedded font file twice. <see cref="Load"/>'s
///         process-lifetime cache is a <see cref="ConcurrentDictionary{TKey, TValue}"/> of
///         <see cref="Lazy{T}"/> values constructed with
///         <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so a face is read from disk at
///         most once however many threads ask for it. <see cref="FindBestMatch"/> and
///         <see cref="FindInFamily"/>
///         only read already-published, immutable data (<see cref="Fonts"/>'s own
///         <see cref="IReadOnlyList{T}"/> and this class's read-only generic-family-name arrays)
///         and performs no mutation, so it requires no locking of its own.
///     </para>
/// </remarks>
public static class SystemFontCatalog
{
    /// <summary>
    ///     The well-known family names searched, in order, when no exact family match is found
    ///     and a "generic sans-serif" font is requested (<see cref="FindBestMatch"/>'s tier 2) -
    ///     the first of these actually present in <see cref="Fonts"/> wins.
    /// </summary>
    private static readonly string[] GenericSansFamilies =
    [
        "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans", "Nimbus Sans", "Segoe UI",
    ];

    /// <summary>
    ///     The well-known family names searched, in order, when no exact family match is found
    ///     and a "generic serif" font is requested (<see cref="FindBestMatch"/>'s tier 2) - the
    ///     first of these actually present in <see cref="Fonts"/> wins.
    /// </summary>
    private static readonly string[] GenericSerifFamilies =
    [
        "Times New Roman", "Times", "Liberation Serif", "DejaVu Serif", "Nimbus Roman",
    ];

    /// <summary>
    ///     The well-known family names searched, in order, when no exact family match is found
    ///     and a "generic monospace" font is requested (<see cref="FindBestMatch"/>'s tier 2) -
    ///     the first of these actually present in <see cref="Fonts"/> wins.
    /// </summary>
    private static readonly string[] GenericMonospaceFamilies =
    [
        "Courier New", "Courier", "Liberation Mono", "DejaVu Sans Mono", "Consolas",
    ];

    /// <summary>
    ///     Backs <see cref="Fonts"/> - built once per process, on first access, and never
    ///     rescanned afterward. Uses <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>
    ///     so concurrent first-time accessors block on a single winning thread's scan rather than
    ///     each independently (and redundantly) scanning, and so no thread can ever observe a
    ///     partially-built catalog - see the class remarks' "Thread safety" section.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<SystemFontInfo>> LazyFonts =
        new(BuildCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Caches every <see cref="TrueTypeFont"/> loaded so far by <see cref="LoadBundledFallback"/>
    ///     or, cross-assembly, by <c>DemaConsulting.CanvasNet.Pdf</c>'s own Noto symbolic-font
    ///     fallback (see <see cref="LoadBundledFallbackCore"/>'s remarks), keyed by the embedded
    ///     resource's bundled file name (for example <c>"LiberationSans-Bold.ttf"</c>) - at most
    ///     15 entries are ever created (12 Liberation Sans/Serif/Mono style variants plus 3 Noto
    ///     substitute fonts), each created at most once, for the lifetime of the process. All
    ///     access to this dictionary is guarded by <see cref="BundledFontLock"/> - see the class
    ///     remarks' "Thread safety" section.
    /// </summary>
    private static readonly Dictionary<string, TrueTypeFont> BundledFontCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Guards <see cref="BundledFontCache"/> against concurrent population - every read and
    ///     write of the cache (in <see cref="LoadBundledFallbackCore"/>) holds this lock for its
    ///     full lookup-or-populate critical section, so two threads requesting the same not-yet-
    ///     cached bundled style can never both load/parse that embedded font file.
    /// </summary>
    private static readonly object BundledFontLock = new();

    /// <summary>
    ///     The bounds of the scan behind <see cref="Fonts"/>: far more generous than
    ///     <see cref="FontScanLimits"/>'s defaults, since this scan reads only the headers of the host
    ///     operating system's own font directories and exists to keep a pathological tree from running
    ///     without end, not to cut a normal installation short.
    /// </summary>
    private static readonly FontScanLimits CatalogScanLimits = new()
    {
        MaxDirectories = 100_000,
        MaxEntries = 2_000_000,
        MaxFontFiles = 50_000,
        MaxFaces = 200_000,
    };

    /// <summary>
    ///     Backs <see cref="Load"/>: every font loaded (or failed to load) so far, keyed by file path and
    ///     face index, for the lifetime of the process.
    /// </summary>
    private static readonly ConcurrentDictionary<(string FilePath, int FaceIndex), Lazy<TrueTypeFont?>> LoadedFonts = new();

    /// <summary>
    ///     Gets every font (or, for a <c>.ttc</c> collection, every face) discovered by scanning
    ///     this operating system's well-known font directories, built exactly once per process on
    ///     first access.
    /// </summary>
    /// <remarks>
    ///     Never throws: a missing or inaccessible scan directory, or a candidate file that fails
    ///     to parse as a valid font, is silently skipped rather than aborting the scan. An empty
    ///     result (for example on a minimal container image with no fonts installed at all) is a
    ///     valid, expected outcome, not an error. Safe to call concurrently from any number of
    ///     threads - see the class remarks' "Thread safety" section.
    /// </remarks>
    public static IReadOnlyList<SystemFontInfo> Fonts => LazyFonts.Value;

    /// <summary>
    ///     Finds the closest-matching font in <see cref="Fonts"/> for a requested family-name
    ///     hint and style, following a two-tier strategy: an exact (case-insensitive) family-name
    ///     match first, then - only when no exact family match exists - a search of a small,
    ///     hand-maintained list of well-known generic-family names (sans/serif/monospace) for the
    ///     first one actually present on this system.
    /// </summary>
    /// <param name="familyNameHint">
    ///     A plain font family name to match, already cleaned of any format-specific decoration
    ///     (for example a PDF subset tag or a <c>,Bold</c>/<c>-Italic</c>-style suffix) by the
    ///     caller - <c>SystemFontCatalog</c> itself has no knowledge of any particular document
    ///     format's naming conventions.
    /// </param>
    /// <param name="bold">Whether a bold style is requested.</param>
    /// <param name="italic">Whether an italic style is requested.</param>
    /// <param name="serif">
    ///     Whether a serif design is requested - consulted only for tier 2's generic-bucket
    ///     search, and mutually exclusive with <paramref name="fixedPitch"/> in this model
    ///     (<paramref name="fixedPitch"/> takes priority when both are <see langword="true"/>).
    /// </param>
    /// <param name="fixedPitch">
    ///     Whether a fixed-pitch (monospace) design is requested - consulted only for tier 2's
    ///     generic-bucket search.
    /// </param>
    /// <returns>
    ///     The best-matching <see cref="SystemFontInfo"/>, or <see langword="null"/> when
    ///     <see cref="Fonts"/> is empty, or genuinely has no exact family match and no
    ///     generic-bucket match either. This method never consults
    ///     <see cref="LoadBundledFallback"/> - composing "system match, else bundled fallback" is
    ///     the caller's responsibility. Safe to call concurrently from any number of threads -
    ///     see the class remarks' "Thread safety" section.
    /// </returns>
    public static SystemFontInfo? FindBestMatch(
        string familyNameHint,
        bool bold,
        bool italic,
        bool serif,
        bool fixedPitch) =>
        FindBestMatchCore(Fonts, familyNameHint, bold, italic, serif, fixedPitch);

    /// <summary>
    ///     Loads (and process-lifetime-caches) the closest-matching bundled Liberation
    ///     Sans/Serif/Mono style for the requested axes, from this assembly's embedded resources
    ///     - never consults <see cref="Fonts"/> or the host operating system's installed fonts at
    ///     all.
    /// </summary>
    /// <param name="serif">Whether the serif family (Liberation Serif) is requested.</param>
    /// <param name="fixedPitch">
    ///     Whether the fixed-pitch family (Liberation Mono) is requested - takes priority over
    ///     <paramref name="serif"/> when both are <see langword="true"/>, mirroring
    ///     <see cref="FindBestMatch"/>'s own tier 2 priority.
    /// </param>
    /// <param name="bold">Whether the bold style is requested.</param>
    /// <param name="italic">Whether the italic style is requested.</param>
    /// <returns>The loaded (or cached) bundled <see cref="TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown only when the expected embedded resource is unexpectedly absent - a defensive,
    ///     effectively-unreachable packaging-integrity guard under normal operation, since all 12
    ///     Liberation Sans/Serif/Mono style-variant bundled files this method can request are
    ///     guaranteed present at build time.
    /// </exception>
    /// <remarks>
    ///     Safe to call concurrently from any number of threads: the underlying cache lookup and
    ///     populate-on-miss path are both guarded by <see cref="BundledFontLock"/>, so no two
    ///     threads can load/parse the same embedded font file twice - see the class remarks'
    ///     "Thread safety" section.
    /// </remarks>
    public static TrueTypeFont LoadBundledFallback(bool serif, bool fixedPitch, bool bold, bool italic)
    {
        string family;
        if (fixedPitch)
        {
            family = "LiberationMono";
        }
        else
        {
            family = serif ? "LiberationSerif" : "LiberationSans";
        }

        var style = (bold, italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            (false, false) => "Regular",
        };

        return LoadBundledFallbackCore($"{family}-{style}.ttf");
    }

    /// <summary>
    ///     The <see cref="FindBestMatch"/> scoring algorithm, exposed over an injectable
    ///     candidate list so its tier-1/tier-2/no-match correctness can be tested without
    ///     depending on what happens to be installed on the machine running the tests.
    /// </summary>
    /// <param name="candidates">The font catalog to search (in production, <see cref="Fonts"/>).</param>
    /// <param name="familyNameHint">See <see cref="FindBestMatch"/>.</param>
    /// <param name="bold">See <see cref="FindBestMatch"/>.</param>
    /// <param name="italic">See <see cref="FindBestMatch"/>.</param>
    /// <param name="serif">See <see cref="FindBestMatch"/>.</param>
    /// <param name="fixedPitch">See <see cref="FindBestMatch"/>.</param>
    /// <returns>See <see cref="FindBestMatch"/>.</returns>
    internal static SystemFontInfo? FindBestMatchCore(
        IReadOnlyList<SystemFontInfo> candidates,
        string familyNameHint,
        bool bold,
        bool italic,
        bool serif,
        bool fixedPitch)
    {
        var exactMatch = FindInFamily(candidates, familyNameHint, bold, italic);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        string[] genericFamilies;
        if (fixedPitch)
        {
            genericFamilies = GenericMonospaceFamilies;
        }
        else
        {
            genericFamilies = serif ? GenericSerifFamilies : GenericSansFamilies;
        }

        foreach (var genericFamily in genericFamilies)
        {
            var bucketMatch = FindInFamily(candidates, genericFamily, bold, italic);
            if (bucketMatch is not null)
            {
                return bucketMatch;
            }
        }

        return null;
    }

    /// <summary>
    ///     Resolves (and process-lifetime-caches) a bundled embedded-resource font by its bundled
    ///     file name, exposed separately from <see cref="LoadBundledFallback"/> so a test can
    ///     exercise the missing-resource defensive guard with a deliberately-wrong file name
    ///     without needing to delete a real embedded resource. This method is also called
    ///     directly, cross-assembly (via <c>InternalsVisibleTo</c>), by
    ///     <c>DemaConsulting.CanvasNet.Pdf</c>'s own <c>PdfDocument.FontFallback.cs</c> to load
    ///     the bundled Noto substitute font(s) for a <c>Symbol</c>/<c>ZapfDingbats</c>
    ///     <c>/BaseFont</c> with no embedded font program - this is deliberate reuse of this
    ///     method's embedded-resource-loading and caching logic, not duplication of it.
    /// </summary>
    /// <param name="bundledFileName">
    ///     The bundled file's name (for example <c>"LiberationSans-Bold.ttf"</c>), used both as
    ///     the cache key and (combined with this assembly's embedded-resource namespace prefix)
    ///     as the resource's logical name.
    /// </param>
    /// <returns>The loaded (or cached) <see cref="TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no embedded resource with the resulting logical name exists in this
    ///     assembly.
    /// </exception>
    internal static TrueTypeFont LoadBundledFallbackCore(string bundledFileName)
    {
        lock (BundledFontLock)
        {
            if (BundledFontCache.TryGetValue(bundledFileName, out var cached))
            {
                return cached;
            }

            var logicalName = $"DemaConsulting.CanvasNet.Fonts.BundledFonts.{bundledFileName}";
            using var stream = typeof(SystemFontCatalog).Assembly.GetManifestResourceStream(logicalName)
                ?? throw new InvalidOperationException(
                    $"Embedded bundled font resource '{logicalName}' is missing; this indicates " +
                    "a packaging defect in the DemaConsulting.CanvasNet assembly.");

            var font = TrueTypeFont.Load(stream);
            BundledFontCache[bundledFileName] = font;
            return font;
        }
    }

    /// <summary>
    ///     Finds the face of <paramref name="faces"/> whose family name equals <paramref name="family"/>
    ///     (case-insensitively, exactly - no generic-family substitution) in the closest style.
    /// </summary>
    /// <param name="faces">The faces to search, for example <see cref="Fonts"/> or the result of <see cref="FontDirectoryScanner.Scan"/>.</param>
    /// <param name="family">The family name to match.</param>
    /// <param name="bold">Whether a bold style is requested.</param>
    /// <param name="italic">Whether an italic style is requested.</param>
    /// <returns>
    ///     The face with the fewest differing bold/italic flags (the first one in <paramref name="faces"/>
    ///     wins a tie), or <see langword="null"/> when no face has the family.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="faces"/> or <paramref name="family"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Safe to call concurrently from any number of threads: it only reads <paramref name="faces"/>.
    /// </remarks>
    public static SystemFontInfo? FindInFamily(IReadOnlyList<SystemFontInfo> faces, string family, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(family);

        SystemFontInfo? best = null;
        var bestScore = int.MaxValue;
        foreach (var candidate in faces)
        {
            if (!string.Equals(candidate.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var score = (candidate.Bold != bold ? 1 : 0) + (candidate.Italic != italic ? 1 : 0);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    ///     Loads the font a <see cref="SystemFontInfo"/> describes, remembering the outcome for the lifetime of
    ///     the process: asking again for the same file and face returns the same instance (or the same failure)
    ///     without reading the file again.
    /// </summary>
    /// <param name="info">The face, as found in <see cref="Fonts"/> or by <see cref="FontDirectoryScanner.Scan"/>.</param>
    /// <returns>
    ///     The loaded font, or <see langword="null"/> when the file has vanished, cannot be read, or is damaged
    ///     (a face that headers-only scanning listed can still fail to load). A failure is cached too, so a
    ///     font that cannot be loaded is not retried.
    /// </returns>
    /// <remarks>
    ///     Safe to call concurrently from any number of threads: a face is loaded at most once, and a
    ///     <see cref="TrueTypeFont"/> is immutable, so the returned instance may be shared freely. Every loaded
    ///     font stays in memory for the lifetime of the process, so use this for the few faces a caller actually
    ///     selects, not to examine every face of a catalog.
    /// </remarks>
    public static TrueTypeFont? Load(SystemFontInfo info) =>
        LoadedFonts.GetOrAdd(
            (info.FilePath, info.FaceIndex),
            static key => new Lazy<TrueTypeFont?>(
                () => LoadUncached(key.FilePath, key.FaceIndex),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>
    ///     Loads a face from disk, reporting a file that is missing, unreadable or damaged as <see langword="null"/>.
    /// </summary>
    /// <param name="path">The font file.</param>
    /// <param name="faceIndex">The face index.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be loaded.</returns>
    private static TrueTypeFont? LoadUncached(string path, int faceIndex)
    {
        try
        {
            return TrueTypeFont.Load(path, faceIndex);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or OverflowException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Builds <see cref="Fonts"/>'s backing catalog: scans every well-known font directory for
    ///     this operating system with <see cref="FontDirectoryScanner"/> (header-level metadata
    ///     only; the fonts are not fully loaded).
    /// </summary>
    private static IReadOnlyList<SystemFontInfo> BuildCatalog() => BuildCatalogFromRoots(GetScanRoots());

    /// <summary>
    ///     Builds a catalog by scanning an explicit, caller-supplied set of directory roots,
    ///     exposed separately from <see cref="BuildCatalog"/> so this scan/tolerance logic (a
    ///     missing root, an inaccessible subdirectory, or an unparseable candidate file are all
    ///     silently skipped) can be tested against controlled temporary directories, without
    ///     depending on what happens to be installed on the machine running the tests. Linked
    ///     font files and linked roots are accepted (a system font directory commonly uses them);
    ///     linked subdirectories are never followed.
    /// </summary>
    /// <param name="roots">The directory roots to scan.</param>
    /// <returns>Every well-formed font face discovered.</returns>
    internal static IReadOnlyList<SystemFontInfo> BuildCatalogFromRoots(IEnumerable<string> roots) =>
        FontDirectoryScanner.Scan(roots, CatalogScanLimits, trustLinkedFiles: true);

    /// <summary>
    ///     Reports this process's well-known font-directory scan roots for the current operating
    ///     system, per <see cref="RuntimeInformation.IsOSPlatform"/>.
    /// </summary>
    private static IEnumerable<string> GetScanRoots()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Join(localAppData, "Microsoft", "Windows", "Fonts");
            }

            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Join(home, "Library", "Fonts");
            }

            yield break;
        }

        // Linux (and any other non-Windows/non-macOS platform): the FreeDesktop-conventional
        // system, local, and per-user font directories.
        yield return "/usr/share/fonts";
        yield return "/usr/local/share/fonts";

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(homeDirectory))
        {
            yield return Path.Join(homeDirectory, ".local", "share", "fonts");
            yield return Path.Join(homeDirectory, ".fonts");
        }
    }
}
