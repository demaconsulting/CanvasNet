// cspell:ignore Dejavu Nimbus Consolas ttcf rescanned Segoe LOCALAPPDATA Noto Zapf
using System.Runtime.InteropServices;

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
    ///     rescanned afterward.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<SystemFontInfo>> LazyFonts =
        new(BuildCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Caches every <see cref="TrueTypeFont"/> loaded so far by <see cref="LoadBundledFallback"/>
    ///     or, cross-assembly, by <c>DemaConsulting.CanvasNet.Pdf</c>'s own Noto symbolic-font
    ///     fallback (see <see cref="LoadBundledFallbackCore"/>'s remarks), keyed by the embedded
    ///     resource's bundled file name (for example <c>"LiberationSans-Bold.ttf"</c>) - at most
    ///     15 entries are ever created (12 Liberation Sans/Serif/Mono style variants plus 3 Noto
    ///     substitute fonts), each created at most once, for the lifetime of the process.
    /// </summary>
    private static readonly Dictionary<string, TrueTypeFont> BundledFontCache = new(StringComparer.Ordinal);

    /// <summary>Guards <see cref="BundledFontCache"/> against concurrent population.</summary>
    private static readonly object BundledFontLock = new();

    /// <summary>
    ///     Gets every font (or, for a <c>.ttc</c> collection, every face) discovered by scanning
    ///     this operating system's well-known font directories, built exactly once per process on
    ///     first access.
    /// </summary>
    /// <remarks>
    ///     Never throws: a missing or inaccessible scan directory, or a candidate file that fails
    ///     to parse as a valid font, is silently skipped rather than aborting the scan. An empty
    ///     result (for example on a minimal container image with no fonts installed at all) is a
    ///     valid, expected outcome, not an error.
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
    ///     the caller's responsibility.
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
        var exactMatch = BestStyleMatch(
            candidates,
            f => string.Equals(f.FamilyName, familyNameHint, StringComparison.OrdinalIgnoreCase),
            bold,
            italic);
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
            var bucketMatch = BestStyleMatch(
                candidates,
                f => string.Equals(f.FamilyName, genericFamily, StringComparison.OrdinalIgnoreCase),
                bold,
                italic);
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
    ///     Finds the best style match (minimizing mismatched bold/italic flag count, first
    ///     encountered wins any tie) among every candidate satisfying <paramref name="familyFilter"/>.
    /// </summary>
    private static SystemFontInfo? BestStyleMatch(
        IReadOnlyList<SystemFontInfo> candidates,
        Func<SystemFontInfo, bool> familyFilter,
        bool bold,
        bool italic)
    {
        SystemFontInfo? best = null;
        var bestScore = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!familyFilter(candidate))
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
    ///     Builds <see cref="Fonts"/>'s backing catalog: scans every well-known font directory for
    ///     this operating system, loading every well-formed face of every candidate
    ///     <c>.ttf</c>/<c>.ttc</c>/<c>.otf</c> file found.
    /// </summary>
    private static IReadOnlyList<SystemFontInfo> BuildCatalog() => BuildCatalogFromRoots(GetScanRoots());

    /// <summary>
    ///     Builds a catalog by scanning an explicit, caller-supplied set of directory roots,
    ///     exposed separately from <see cref="BuildCatalog"/> so this scan/tolerance logic (a
    ///     missing root, an inaccessible subdirectory, or an unparseable candidate file are all
    ///     silently skipped) can be tested against controlled temporary directories, without
    ///     depending on what happens to be installed on the machine running the tests.
    /// </summary>
    /// <param name="roots">The directory roots to scan.</param>
    /// <returns>Every well-formed font face discovered.</returns>
    internal static IReadOnlyList<SystemFontInfo> BuildCatalogFromRoots(IEnumerable<string> roots)
    {
        var result = new List<SystemFontInfo>();
        foreach (var root in roots)
        {
            foreach (var path in EnumerateFontFiles(root))
            {
                AddFacesForFile(path, result);
            }
        }

        return result;
    }

    /// <summary>
    ///     Loads every face of a single candidate font file, appending a <see cref="SystemFontInfo"/>
    ///     for each well-formed face found, and silently skipping the whole file (or, for a
    ///     <c>.ttc</c>, just the one corrupt face) when it fails to parse.
    /// </summary>
    private static void AddFacesForFile(string path, List<SystemFontInfo> result)
    {
        int faceCount;
        try
        {
            faceCount = TrueTypeFont.GetFaceCount(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        for (var faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            TrueTypeFont font;
            try
            {
                font = TrueTypeFont.Load(path, faceIndex);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var nameInfo = font.GetNameInfo();
            var familyName = nameInfo.FamilyName ?? Path.GetFileNameWithoutExtension(path);
            var subfamilyName = nameInfo.SubfamilyName ?? string.Empty;
            result.Add(new SystemFontInfo(
                familyName,
                subfamilyName,
                font.IsBold,
                font.IsItalic,
                font.IsFixedPitch,
                Path.GetFullPath(path),
                faceIndex));
        }
    }

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
                yield return Path.Combine(localAppData, "Microsoft", "Windows", "Fonts");
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
                yield return Path.Combine(home, "Library", "Fonts");
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
            yield return Path.Combine(homeDirectory, ".local", "share", "fonts");
            yield return Path.Combine(homeDirectory, ".fonts");
        }
    }

    /// <summary>
    ///     Recursively enumerates every <c>.ttf</c>/<c>.ttc</c>/<c>.otf</c> file (case-insensitive
    ///     extension match) under <paramref name="root"/>, tolerating a missing root and any
    ///     inaccessible subdirectory encountered along the way (both are silently skipped rather
    ///     than aborting the whole scan). Symlinked or junction subdirectories (reparse points) are
    ///     skipped entirely, and every other subdirectory's canonical path is tracked so a cycle
    ///     (for example, a symlink pointing back at an ancestor) cannot cause an infinite scan.
    /// </summary>
    private static IEnumerable<string> EnumerateFontFiles(string root)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return results;
        }

        // Tracks every directory's canonicalized path already queued or visited, so a symlink (or
        // junction/reparse point) that cycles back to an ancestor - or to another already-visited
        // branch - cannot make this scan revisit the same real directory indefinitely.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeDirectoryPath(root),
        };

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(file);
                }
            }

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                try
                {
                    // A symlinked or junction subdirectory can point anywhere - including back
                    // at an ancestor of this scan - so it is skipped entirely rather than
                    // followed, matching common font-discovery tooling's own treatment of
                    // reparse points.
                    if (new DirectoryInfo(subdirectory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                if (!visited.Add(NormalizeDirectoryPath(subdirectory)))
                {
                    continue;
                }

                pending.Push(subdirectory);
            }
        }

        return results;
    }

    /// <summary>
    ///     Canonicalizes a directory path (resolving <c>.</c>/<c>..</c> segments and relative
    ///     roots via <see cref="Path.GetFullPath(string)"/>, then trimming any trailing directory
    ///     separator) so two different textual spellings of the same real directory compare equal
    ///     in <see cref="EnumerateFontFiles"/>'s visited-directory set.
    /// </summary>
    private static string NormalizeDirectoryPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
