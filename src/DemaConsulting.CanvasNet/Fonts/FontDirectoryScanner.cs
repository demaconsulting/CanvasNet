// cspell:ignore ARIALN
using System.Security;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Scans directory trees for font files and reports the identity, name, and style metadata of every
///     well-formed face they hold, reading only each font's headers (the font is never fully loaded).
/// </summary>
/// <remarks>
///     <para>
///         The scan is deterministic: directories are visited depth first in alphabetical order (ordinal,
///         ignoring case, with the case-sensitive ordinal order as a tie-break), and the
///         font files found under one root are examined in that order of their full paths, so the same
///         tree always yields the same faces in the same order, and the part of an oversized tree that is
///         read when a bound is reached (<see cref="FontScanLimits"/>) is always the same part. Roots are
///         scanned in the order given, and a file reachable from more than one root is reported once.
///     </para>
///     <para>
///         The scan recognizes the <c>.ttf</c>, <c>.otf</c>, <c>.ttc</c> and <c>.otc</c> extensions in
///         any case. It is tolerant of failure: a missing or malformed root, an unreadable directory, an
///         unparseable font file, or one corrupt face of an otherwise well-formed collection is skipped,
///         never an error. An empty result is a valid outcome.
///     </para>
///     <para>
///         A subdirectory that is a link (a symbolic link, junction, or other reparse point) is never
///         followed, because a link can point anywhere, including back at an ancestor of the scan. A root
///         that is a link, and a font file that is a link, are skipped unless the caller passes
///         <c>trustLinkedFiles</c>: <see langword="true"/>, for directories it controls or whose layout is
///         known to use links (for example a system font directory).
///     </para>
///     <para>
///         <b>Thread safety</b>: <see cref="Scan"/> keeps all of its state in local variables and reads
///         the file system only, so it may be called concurrently from any number of threads.
///     </para>
/// </remarks>
public static class FontDirectoryScanner
{
    /// <summary>
    ///     The order paths are visited and reported in: ordinal ignoring case, so that the order matches the
    ///     alphabetical listing of the case-insensitive file systems of Windows and macOS (a font file named
    ///     <c>arial.ttf</c> before <c>ARIALN.TTF</c>, as a directory listing there would give it), with the
    ///     case-sensitive ordinal order breaking a tie so the order is total and identical on every platform.
    /// </summary>
    private static readonly Comparer<string> PathOrder = Comparer<string>.Create(
        static (left, right) =>
        {
            var order = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : string.CompareOrdinal(left, right);
        });

    /// <summary>
    ///     Scans directory trees for fonts.
    /// </summary>
    /// <param name="directories">The roots to scan, in order; a missing, empty or malformed entry contributes nothing.</param>
    /// <param name="limits">The bounds of the scan; <see langword="null"/> means the defaults of <see cref="FontScanLimits"/>.</param>
    /// <param name="trustLinkedFiles">
    ///     <see langword="true"/> to also accept a root that is a link and font files that are links
    ///     (subdirectories that are links are never followed); <see langword="false"/> (the default) to skip them.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the scan.</param>
    /// <returns>
    ///     Every well-formed face found, with <see cref="SystemFontInfo.FilePath"/> an absolute path; the
    ///     family name is the file name without its extension for a face with no usable <c>name</c> table.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directories"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IReadOnlyList<SystemFontInfo> Scan(
        IEnumerable<string> directories,
        FontScanLimits? limits = null,
        bool trustLinkedFiles = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directories);

        var budget = new ScanBudget(limits ?? new FontScanLimits());
        var faces = new List<SystemFontInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            // Nothing more is read once every bound is reached; what was found before it stays
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.CollectionStopped && budget.FacesStopped)
            {
                break;
            }

            // Sorted so that the result does not depend on the order the file system lists the files in
            var files = CollectFontFiles(directory, trustLinkedFiles, budget, cancellationToken);
            files.Sort(PathOrder);
            foreach (var file in files.Where(seen.Add))
            {
                if (budget.FacesStopped)
                {
                    break;
                }

                AddFaces(file, faces, budget, cancellationToken);
            }
        }

        return faces;
    }

    /// <summary>
    ///     Collects the font files of a root and of its subdirectories (except links), skipping what cannot be read.
    /// </summary>
    /// <param name="root">The root; nothing is collected when it does not exist (or is a link and links are not trusted).</param>
    /// <param name="trustLinkedFiles">Whether a root that is a link and linked font files are accepted.</param>
    /// <param name="budget">The bounds of the scan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The font files, in no particular order.</returns>
    private static List<string> CollectFontFiles(
        string? root,
        bool trustLinkedFiles,
        ScanBudget budget,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();
        if (string.IsNullOrEmpty(root) || budget.CollectionStopped)
        {
            return files;
        }

        string start;
        try
        {
            if (!Directory.Exists(root))
            {
                return files;
            }

            start = Path.GetFullPath(root);
            if (!trustLinkedFiles && IsLink(new DirectoryInfo(start)))
            {
                return files;
            }
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            // A path that cannot be examined (malformed, too long, not permitted) contributes nothing
            return files;
        }

        var pending = new Stack<string>();
        pending.Push(start);
        while (pending.Count > 0 && !budget.CollectionStopped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            if (!budget.TryCountDirectory())
            {
                break;
            }

            ListDirectory(directory, trustLinkedFiles, files, pending, budget, cancellationToken);
        }

        return files;
    }

    /// <summary>
    ///     Lists one directory: adds its font files to the result and its subdirectories (except links) to the pending stack.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="trustLinkedFiles">Whether linked font files are accepted.</param>
    /// <param name="files">The list to add the font files to.</param>
    /// <param name="pending">The stack to push the subdirectories on, so that the first in <see cref="PathOrder"/> is popped first.</param>
    /// <param name="budget">The bounds of the scan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void ListDirectory(
        string directory,
        bool trustLinkedFiles,
        List<string> files,
        Stack<string> pending,
        ScanBudget budget,
        CancellationToken cancellationToken)
    {
        var fonts = new List<string>();
        var subdirectories = new List<string>();
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!budget.TryCountEntry())
                {
                    break;
                }

                if (entry.Attributes.HasFlag(FileAttributes.Directory))
                {
                    // A link can point back at an ancestor, so a linked directory is never followed
                    if (!IsLink(entry))
                    {
                        subdirectories.Add(entry.FullName);
                    }
                }
                else if (IsFontFile(entry.Name) && (trustLinkedFiles || !IsLink(entry)))
                {
                    fonts.Add(entry.FullName);
                }
            }
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            // A directory that cannot be listed (removed meanwhile, not permitted, ...) contributes nothing
            return;
        }

        fonts.Sort(PathOrder);
        foreach (var font in fonts)
        {
            if (!budget.TryCountFontFile())
            {
                break;
            }

            files.Add(font);
        }

        // Pushed in reverse so that the first subdirectory in path order is visited first
        subdirectories.Sort(PathOrder);
        for (var i = subdirectories.Count - 1; i >= 0; i--)
        {
            pending.Push(subdirectories[i]);
        }
    }

    /// <summary>
    ///     Reads every face of one font file, skipping the whole file (or just the one face of a collection) that
    ///     does not parse.
    /// </summary>
    /// <remarks>
    ///     Only the font's headers are read, through one <see cref="FileStream"/> per file, via
    ///     <see cref="SfntHeaderReader"/>: a face whose headers are valid but whose glyph data is corrupt is listed
    ///     (it would fail later, when it is loaded).
    /// </remarks>
    /// <param name="path">The font file.</param>
    /// <param name="faces">The list to add the faces to.</param>
    /// <param name="budget">The bounds of the scan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void AddFaces(string path, List<SystemFontInfo> faces, ScanBudget budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.RandomAccess);

            var faceCount = SfntHeaderReader.GetFaceCount(stream);
            for (var faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!budget.TryCountFace())
                {
                    return;
                }

                if (TryReadFace(stream, faceIndex, out var info))
                {
                    faces.Add(new SystemFontInfo(
                        info.FamilyName ?? Path.GetFileNameWithoutExtension(path),
                        info.SubfamilyName ?? string.Empty,
                        info.Bold,
                        info.Italic,
                        info.FixedPitch,
                        path,
                        faceIndex));
                }
            }
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            // The file could not be opened (or vanished or was locked meanwhile) or its face count is unreadable
        }
    }

    /// <summary>
    ///     Reads the metadata of one face, reporting a face that does not parse as absent.
    /// </summary>
    /// <param name="stream">The font file.</param>
    /// <param name="faceIndex">The face index.</param>
    /// <param name="info">The metadata when the face parses.</param>
    /// <returns><see langword="true"/> when the face parses.</returns>
    private static bool TryReadFace(FileStream stream, int faceIndex, out SfntFaceInfo info)
    {
        try
        {
            info = SfntHeaderReader.ReadFace(stream, faceIndex);
            return true;
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            info = default;
            return false;
        }
    }

    /// <summary>
    ///     Reports whether a file system entry is a link: a symbolic link, junction, or other reparse point.
    /// </summary>
    /// <remarks>
    ///     This is the reparse-point <em>attribute</em>, deliberately: it also covers reparse points that
    ///     <see cref="FileSystemInfo.LinkTarget"/> does not report, so no kind of redirection can reopen a cycle.
    /// </remarks>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when the entry is a link, or cannot be inspected.</returns>
    private static bool IsLink(FileSystemInfo entry)
    {
        try
        {
            return entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (IsFileSystemFailure(ex))
        {
            return true;
        }
    }

    /// <summary>
    ///     Reports whether a file name has the extension of a font file the font loader supports.
    /// </summary>
    /// <param name="file">The file name or path.</param>
    /// <returns><see langword="true"/> for <c>.ttf</c>, <c>.otf</c>, <c>.ttc</c> and <c>.otc</c> in any case.</returns>
    private static bool IsFontFile(string file)
    {
        var extension = Path.GetExtension(file);
        return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".otc", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reports whether an exception means that a path or directory cannot be examined or listed.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> when the path should be skipped rather than the scan failed.</returns>
    private static bool IsFileSystemFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;

    /// <summary>
    ///     Reports whether an exception means that a font file is missing, unreadable or damaged.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> when the file should be skipped rather than the scan failed.</returns>
    private static bool IsSkippable(Exception ex) =>
        ex is InvalidDataException or OverflowException || IsFileSystemFailure(ex);

    /// <summary>
    ///     Counts the work of one scan against the bounds.
    /// </summary>
    /// <param name="limits">The bounds.</param>
    private sealed class ScanBudget(FontScanLimits limits)
    {
        /// <summary>The directories listed so far.</summary>
        private int _directories;

        /// <summary>The directory entries examined so far.</summary>
        private int _entries;

        /// <summary>The font files accepted so far.</summary>
        private int _fontFiles;

        /// <summary>The faces examined so far.</summary>
        private int _faces;

        /// <summary>Gets a value indicating whether a bound on directories, entries or font files was reached, so no more is collected.</summary>
        public bool CollectionStopped { get; private set; }

        /// <summary>Gets a value indicating whether the bound on faces was reached, so no more is read.</summary>
        public bool FacesStopped { get; private set; }

        /// <summary>Counts a directory to be listed.</summary>
        /// <returns><see langword="false"/> when the bound is reached.</returns>
        public bool TryCountDirectory() => CountCollected(ref _directories, limits.MaxDirectories);

        /// <summary>Counts a directory entry to be examined.</summary>
        /// <returns><see langword="false"/> when the bound is reached.</returns>
        public bool TryCountEntry() => CountCollected(ref _entries, limits.MaxEntries);

        /// <summary>Counts a font file to be accepted.</summary>
        /// <returns><see langword="false"/> when the bound is reached.</returns>
        public bool TryCountFontFile() => CountCollected(ref _fontFiles, limits.MaxFontFiles);

        /// <summary>Counts a face to be read.</summary>
        /// <returns><see langword="false"/> when the bound is reached.</returns>
        public bool TryCountFace()
        {
            if (_faces >= limits.MaxFaces)
            {
                FacesStopped = true;
                return false;
            }

            _faces++;
            return true;
        }

        /// <summary>Counts one unit against a collection bound, stopping the collection when it is reached.</summary>
        /// <param name="counter">The counter.</param>
        /// <param name="limit">The bound.</param>
        /// <returns><see langword="false"/> when the bound is reached.</returns>
        private bool CountCollected(ref int counter, int limit)
        {
            if (counter >= limit)
            {
                CollectionStopped = true;
                return false;
            }

            counter++;
            return true;
        }
    }
}
