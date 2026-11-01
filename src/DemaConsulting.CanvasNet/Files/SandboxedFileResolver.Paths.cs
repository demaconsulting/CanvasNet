namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Path handling of <see cref="SandboxedFileResolver"/>: validation, candidate generation, the confinement rule and
///     the link walk.
/// </summary>
/// <remarks>
///     <para>
///         <b>Path text.</b> Backslashes are first turned into forward slashes, so <c>a\b.txt</c> and <c>a/b.txt</c> are
///         the same path on every platform. An empty or white-space path, a control character (including NUL and the C1
///         range), a colon anywhere (drive letters and alternate data streams) and a leading slash (absolute and UNC
///         paths) are refused. A <c>..</c> that would leave the root a candidate is derived from is refused too;
///         <c>..</c> that stays inside the root is fine.
///     </para>
///     <para>
///         <b>File-system mode</b> (no resolver callback): the roots are the base directory and each search path as full
///         paths. A candidate is the full path of the text combined with a directory and must lie under the root that
///         directory belongs to (ordinal comparison; case-insensitive on Windows and macOS). Every component below the
///         root, including the file itself, must not be a symbolic link or junction, whatever its target: the candidate
///         is walked top-down from the root, only existing components above the file are probed (a missing component
///         above the file means the file is missing, and nothing below it is examined; the file itself is always probed
///         for a link, even when an existence check says it is missing, so a dangling link is refused; a file that is
///         not a link is classified by the open attempt, which tells a missing file from an existing one that cannot be
///         opened), each component is probed once per resolver, and a probe that fails rejects the file. The root itself
///         and everything above it are trusted, because the caller chose them. The check precedes opening the file, so
///         nothing is read through a link and a file behind a link is not reported as existing. A caller who wants to
///         use linked content adds the real directory as a search path. Other reparse points that redirect nothing (for
///         example the placeholders of a cloud-synchronized folder) are not links in this sense.
///     </para>
///     <para>
///         <b>Resolver mode</b>: no file-system access happens. Candidates are normalized logical paths (forward
///         slashes, no leading slash, <c>.</c> and <c>..</c> collapsed) handed to the callback; the first candidate that
///         yields a stream wins. The logical root is the empty path; each search path that is itself a valid logical path
///         is used as a logical directory prefix, and search paths that are not valid logical paths are ignored. The base
///         directory plays no role in this mode.
///     </para>
/// </remarks>
public sealed partial class SandboxedFileResolver
{
    /// <summary>The comparison used to decide whether a path is under a root and to compare file keys on this platform.</summary>
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The comparer for file keys in file-system mode.</summary>
    private static readonly StringComparer PlatformKeyComparer =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The link probe: tells whether a full path is a symbolic link or junction.</summary>
    private readonly Func<string, bool> _isLink;

    /// <summary>The probe results by full path of an existing component, so each component is probed once.</summary>
    private readonly Dictionary<string, bool> _linkStates;

    /// <summary>The full path of the base directory, or <see langword="null"/>; computed on first use.</summary>
    private string? _baseRoot;

    /// <summary>The full paths of the search directories (file-system mode); computed on first use.</summary>
    private List<string>? _searchRoots;

    /// <summary>The valid logical search prefixes (resolver mode); computed on first use.</summary>
    private List<string>? _logicalPrefixes;

    /// <summary>
    ///     A place a file may be found: where it is, and the root it must stay under.
    /// </summary>
    /// <param name="Key">The identity of the candidate (its normalized full or logical path).</param>
    /// <param name="Root">The root the candidate is confined to.</param>
    private sealed record Candidate(string Key, string Root);

    /// <summary>
    ///     Gets a value indicating whether the caller supplied a resolver callback (logical-path mode).
    /// </summary>
    private bool UsesResolver => _options.FileResolver is not null;

    /// <summary>
    ///     Validates and normalizes the path text of a request.
    /// </summary>
    /// <param name="raw">The path as written.</param>
    /// <returns>The text with backslashes turned into forward slashes.</returns>
    /// <exception cref="SandboxException">Thrown when the text is empty, contains a control character or a colon, or is absolute.</exception>
    private static string ValidatePathText(string raw)
    {
        if (!TryDescribeInvalidPath(raw, out var kind, out var reason))
        {
            return raw.Replace('\\', '/');
        }

        // Control characters are replaced so the exception can safely be shown in a message
        throw new SandboxException(kind, new string(raw.Select(c => char.IsControl(c) ? '?' : c).ToArray()), reason);
    }

    /// <summary>
    ///     Explains why a path text is not acceptable.
    /// </summary>
    /// <param name="raw">The path text.</param>
    /// <param name="kind">The failure kind when the text is not acceptable.</param>
    /// <param name="reason">The reason phrase when the text is not acceptable.</param>
    /// <returns><see langword="true"/> when the text is not acceptable.</returns>
    private static bool TryDescribeInvalidPath(string raw, out SandboxFailureKind kind, out string reason)
    {
        // The checks run from the most basic to the most specific, so the reason names the first rule that is broken
        kind = SandboxFailureKind.EmptyPath;
        reason = "the path is empty";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (raw.Any(char.IsControl))
        {
            kind = SandboxFailureKind.ControlCharacter;
            reason = "the path contains a control character";
            return true;
        }

        if (raw.Contains(':', StringComparison.Ordinal))
        {
            kind = SandboxFailureKind.ColonInPath;
            reason = "the path contains a colon";
            return true;
        }

        kind = SandboxFailureKind.AbsolutePath;
        reason = "absolute paths are not permitted";
        return raw[0] is '/' or '\\';
    }

    /// <summary>
    ///     Checks that a file passed as the starting point of a relative path was resolved through one of this resolver's
    ///     own roots, so a file from another resolver (with wider roots) cannot widen what this one may read.
    /// </summary>
    /// <remarks>
    ///     The root of the file must equal a root this resolver is configured with (same comparison as file keys) and its
    ///     directory must lie under that root; the candidate built from it is then confined to that root as usual.
    ///     <see cref="SandboxedFile"/> cannot be constructed outside this assembly, so these two facts identify a file
    ///     this resolver could have produced.
    /// </remarks>
    /// <param name="relativeTo">The file; its roots were computed by <see cref="EnsureRoots"/> already.</param>
    /// <exception cref="ArgumentException">Thrown when the file does not belong to this resolver's roots.</exception>
    private void ValidateRelativeTo(SandboxedFile relativeTo)
    {
        var roots = UsesResolver
            ? _logicalPrefixes!.Prepend(string.Empty)
            : _searchRoots!.Prepend(_baseRoot!).OfType<string>();
        var knownRoot = roots.Any(root => KeyComparer.Equals(root, relativeTo.Root));
        var directoryUnderRoot = UsesResolver
            ? relativeTo.Root.Length == 0
                || relativeTo.Directory == relativeTo.Root
                || relativeTo.Directory.StartsWith(relativeTo.Root + "/", StringComparison.Ordinal)
            : IsUnder(relativeTo.Directory, relativeTo.Root);
        if (!knownRoot || !directoryUnderRoot)
        {
            throw new ArgumentException("The file was not resolved through one of this resolver's roots.", nameof(relativeTo));
        }
    }

    /// <summary>
    ///     Generates the candidates for a path text in search order.
    /// </summary>
    /// <param name="text">The validated path text.</param>
    /// <param name="relativeTo">The file whose directory is searched first, or <see langword="null"/> for the base directory.</param>
    /// <returns>The candidates: the starting directory first, then each search path.</returns>
    private IEnumerable<Candidate> Candidates(string text, SandboxedFile? relativeTo)
    {
        EnsureRoots();
        if (relativeTo is not null)
        {
            yield return Combine(relativeTo.Directory, relativeTo.Root, text);
        }
        else if (UsesResolver)
        {
            yield return Combine(string.Empty, string.Empty, text);
        }
        else if (_baseRoot is not null)
        {
            yield return Combine(_baseRoot, _baseRoot, text);
        }

        foreach (var root in UsesResolver ? _logicalPrefixes! : _searchRoots!)
        {
            yield return Combine(root, root, text);
        }
    }

    /// <summary>
    ///     Combines a directory and a path text into a candidate confined to a root.
    /// </summary>
    /// <param name="directory">The directory to start from.</param>
    /// <param name="root">The root the result must stay under.</param>
    /// <param name="text">The validated path text.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="SandboxException">Thrown when the result is not under the root.</exception>
    private Candidate Combine(string directory, string root, string text)
    {
        var path = UsesResolver ? CombineLogical(directory, root, text) : CombineFileSystem(directory, root, text);
        return path is null
            ? throw new SandboxException(SandboxFailureKind.OutsideRoot, text)
            : new Candidate(path, root);
    }

    /// <summary>
    ///     Combines a directory and a path text in file-system mode.
    /// </summary>
    /// <param name="directory">The full path of the directory.</param>
    /// <param name="root">The full path of the root.</param>
    /// <param name="text">The validated path text.</param>
    /// <returns>The full path, or <see langword="null"/> when it is not under the root or is not a valid path.</returns>
    private static string? CombineFileSystem(string directory, string root, string text)
    {
        try
        {
            var full = Path.GetFullPath(text, directory);
            return IsUnder(full, root) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Combines a logical directory and a path text in resolver mode.
    /// </summary>
    /// <param name="directory">The logical directory (empty for the root).</param>
    /// <param name="root">The logical root (a prefix of <paramref name="directory"/>).</param>
    /// <param name="text">The validated path text.</param>
    /// <returns>The normalized logical path, or <see langword="null"/> when a <c>..</c> would leave the root.</returns>
    private static string? CombineLogical(string directory, string root, string text)
    {
        var segments = directory.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var floor = root.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

        // Collapse "." and ".." textually; popping below the root is the escape that is refused
        foreach (var segment in text.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count <= floor)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    /// <summary>
    ///     Determines whether a full path is the root or lies under it.
    /// </summary>
    /// <param name="full">The full path.</param>
    /// <param name="root">The full path of the root.</param>
    /// <returns><see langword="true"/> when <paramref name="full"/> is inside <paramref name="root"/>.</returns>
    private static bool IsUnder(string full, string root)
    {
        var trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, trimmed, PathComparison)
            || full.StartsWith(trimmed + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>
    ///     Gets the directory of a candidate key.
    /// </summary>
    /// <param name="key">The full path, or the logical path in resolver mode.</param>
    /// <returns>The directory; the empty string for a logical file at the root.</returns>
    private string DirectoryOf(string key)
    {
        if (UsesResolver)
        {
            var slash = key.LastIndexOf('/');
            return slash < 0 ? string.Empty : key[..slash];
        }

        return Path.GetDirectoryName(key)!;
    }

    /// <summary>
    ///     Walks the components of a candidate from the root down to the file and rejects the first link.
    /// </summary>
    /// <param name="candidate">The candidate; its key is a full path under its root.</param>
    /// <returns><see langword="true"/> when no component is a link and every component above the file exists (the file itself is left to the open attempt); <see langword="false"/> when a component above the file does not exist.</returns>
    /// <exception cref="SandboxException">Thrown when a component is a link, or cannot be checked.</exception>
    private bool WalkRejectingLinks(Candidate candidate)
    {
        // The root itself and everything above it are trusted; only components strictly below it are examined
        var current = candidate.Root;
        var segments = Path.GetRelativePath(candidate.Root, candidate.Key).Split(Path.DirectorySeparatorChar);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Join(current, segments[index]);
            if (!_linkStates.TryGetValue(current, out var isLink))
            {
                // The first missing component above the file ends the walk: nothing below it can exist, so it is never
                // probed. The final component is always probed, whatever an existence check says: the check reports
                // "missing" for a dangling link and for an existing file it cannot access, and a dangling link must be
                // refused rather than opened. A file that is truly missing is not a link; the open attempt (not the
                // probe) then decides whether it is missing or unreadable
                if (index < segments.Length - 1 && !File.Exists(current) && !Directory.Exists(current))
                {
                    return false;
                }

                isLink = ProbeLink(current, candidate);
                _linkStates[current] = isLink;
            }

            if (isLink)
            {
                throw new SandboxException(
                    SandboxFailureKind.ThroughLink,
                    candidate.Key,
                    Path.GetRelativePath(candidate.Root, current).Replace('\\', '/'));
            }
        }

        return true;
    }

    /// <summary>
    ///     Asks the link probe about one component, failing closed when the probe cannot answer.
    /// </summary>
    /// <param name="path">The full path of the component.</param>
    /// <param name="candidate">The candidate being examined.</param>
    /// <returns><see langword="true"/> when the component is a symbolic link or junction.</returns>
    /// <exception cref="SandboxException">Thrown when the probe fails.</exception>
    private bool ProbeLink(string path, Candidate candidate)
    {
        try
        {
            return _isLink(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new SandboxException(SandboxFailureKind.LinkCheckFailed, candidate.Key, innerException: ex);
        }
    }

    /// <summary>
    ///     The default link probe: a symbolic link or junction has a link target, whether it names a file or a directory
    ///     and whether or not the target exists.
    /// </summary>
    /// <remarks>
    ///     The probe reads the link itself and never follows it, so it also recognizes a dangling link, for which an
    ///     existence check says "missing"; a path that does not exist at all has no link target and is not a link.
    /// </remarks>
    /// <param name="path">The full path of a file or directory, or of a path that may not exist.</param>
    /// <returns><see langword="true"/> when the path is a symbolic link or junction.</returns>
    private static bool DefaultIsLink(string path) =>
        (Directory.Exists(path) ? new DirectoryInfo(path).LinkTarget : new FileInfo(path).LinkTarget) is not null;

    /// <summary>
    ///     Computes the roots on first use, so a resolver that is never asked for a file never touches the path options.
    /// </summary>
    /// <exception cref="SandboxException">Thrown when a configured directory is not a valid path.</exception>
    private void EnsureRoots()
    {
        if (_searchRoots is not null)
        {
            return;
        }

        if (UsesResolver)
        {
            _logicalPrefixes = _searchPaths.Select(NormalizePrefix).OfType<string>().ToList();
            _searchRoots = [];
            return;
        }

        _baseRoot = _options.BaseDirectory is null ? null : FullRoot(_options.BaseDirectory);
        _searchRoots = _searchPaths.Select(FullRoot).ToList();
        _logicalPrefixes = [];
    }

    /// <summary>
    ///     Computes the full path of a configured directory.
    /// </summary>
    /// <param name="directory">The configured directory.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="SandboxException">Thrown when the directory is not a valid path.</exception>
    private static string FullRoot(string directory)
    {
        try
        {
            return Path.GetFullPath(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new SandboxException(SandboxFailureKind.InvalidRoot, directory ?? string.Empty, innerException: ex);
        }
    }

    /// <summary>
    ///     Normalizes a search path into a logical directory prefix.
    /// </summary>
    /// <param name="path">The search path.</param>
    /// <returns>The prefix, or <see langword="null"/> when the path is not a valid logical path.</returns>
    private static string? NormalizePrefix(string path) =>
        path is not null && !TryDescribeInvalidPath(path, out _, out _)
            ? CombineLogical(string.Empty, string.Empty, path.Replace('\\', '/'))
            : null;
}
