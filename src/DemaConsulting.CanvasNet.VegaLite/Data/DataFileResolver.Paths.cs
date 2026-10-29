namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     Path handling of <see cref="DataFileResolver"/>: validation, candidate generation, the sandbox and the link walk.
/// </summary>
/// <remarks>
///     <para>
///         <b>Path text.</b> Backslashes are first turned into forward slashes, so <c>a\b.csv</c> and <c>a/b.csv</c> are
///         the same path on every platform. An empty or white-space path, a control character (including NUL), a colon
///         anywhere (drive letters and alternate data streams) and a leading slash (absolute and UNC paths) are sandbox
///         violations. A <c>..</c> that would leave the root a candidate is derived from is a sandbox violation too;
///         <c>..</c> that stays inside the root is fine.
///     </para>
///     <para>
///         <b>File-system mode</b> (no resolver callback): the roots are the base directory and each data path as full
///         paths. A candidate is the full path of the text combined with a root and must lie under that root (ordinal
///         comparison; case-insensitive on Windows and macOS). Every component below the root, including the file
///         itself, must not be a symbolic link or junction, whatever its target: the candidate is walked top-down from
///         the root, only existing components are probed (a missing component above the file means the file is missing,
///         and nothing below it is examined; the file itself is probed for a link when it exists, and is otherwise
///         classified by the open attempt, which tells a missing file from an existing one that cannot be opened), each component is probed once per resolver, and a probe that fails rejects the file.
///         The root itself and everything above it are trusted, because the caller chose them. The check precedes
///         opening the file, so nothing is read through a link and a file behind a link is not reported as existing. A
///         caller who wants to use linked content adds the real directory as a data path. Other reparse points that
///         redirect nothing (for example the placeholders of a cloud-synchronized folder) are not links in this sense.
///     </para>
///     <para>
///         <b>Resolver mode</b>: no file system access happens. Candidates are normalized logical paths (forward
///         slashes, no leading slash, <c>.</c> and <c>..</c> collapsed) handed to the callback; the first candidate that
///         yields a stream wins. The logical root is the empty path; each data path that is itself a valid logical path
///         is used as a logical directory prefix, and data paths that are not valid logical paths are ignored. The base
///         directory plays no role in this mode.
///     </para>
/// </remarks>
internal sealed partial class DataFileResolver
{
    /// <summary>The comparison used to decide whether a path is under a root and to compare file keys on this platform.</summary>
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The comparer for file keys in file-system mode.</summary>
    private static readonly StringComparer PlatformKeyComparer =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The loaded files by candidate key; <see langword="null"/> remembers a candidate that does not exist.</summary>
    private readonly Dictionary<string, DataFile?> _files;

    /// <summary>The comparer for file keys: platform path comparison in file-system mode, ordinal for logical paths.</summary>
    private readonly StringComparer _keyComparer;

    /// <summary>The link probe: tells whether a full path is a symbolic link or junction.</summary>
    private readonly Func<string, bool> _isLink;

    /// <summary>The opener of a file-system candidate: a seam so tests can raise an open failure.</summary>
    private readonly Func<string, Stream> _openFile;

    /// <summary>The probe results by full path of an existing component, so each component is probed once.</summary>
    private readonly Dictionary<string, bool> _linkStates;

    /// <summary>The full path of the base directory, or <see langword="null"/>; computed on first use.</summary>
    private string? _baseRoot;

    /// <summary>The full paths of the data directories (file-system mode); computed on first use.</summary>
    private List<string>? _dataRoots;

    /// <summary>The valid logical data prefixes (resolver mode); computed on first use.</summary>
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
    ///     Validates and normalizes the path text of a data URL.
    /// </summary>
    /// <param name="raw">The path as written.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns>The text with backslashes turned into forward slashes.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the text is empty, contains a control character or a colon, or is absolute.</exception>
    private static string ValidatePathText(string raw, VegaLiteLocation location)
    {
        var reason = DescribeInvalidPath(raw);
        if (reason is not null)
        {
            throw new VegaLiteRenderException($"The data path '{Printable(raw)}' is not allowed: {reason}.", location);
        }

        return raw.Replace('\\', '/');
    }

    /// <summary>
    ///     Explains why a path text is not acceptable.
    /// </summary>
    /// <param name="raw">The path text.</param>
    /// <returns>The reason, or <see langword="null"/> when the text is acceptable.</returns>
    private static string? DescribeInvalidPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "the path is empty";
        }

        if (raw.Any(c => c < ' ' || c == '\u007F'))
        {
            return "the path contains a control character";
        }

        if (raw.Contains(':', StringComparison.Ordinal))
        {
            return "the path contains a colon";
        }

        return raw[0] is '/' or '\\' ? "absolute paths are not permitted" : null;
    }

    /// <summary>
    ///     Replaces control characters so that a path can be shown in a message.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each control character replaced by a question mark.</returns>
    private static string Printable(string text) => new(text.Select(c => c < ' ' || c == '\u007F' ? '?' : c).ToArray());

    /// <summary>
    ///     Generates the candidates for a path text in search order.
    /// </summary>
    /// <param name="text">The validated path text.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns>The candidates: the base directory first, then each data path.</returns>
    private IEnumerable<Candidate> Candidates(string text, VegaLiteLocation location)
    {
        EnsureRoots(location);
        if (UsesResolver)
        {
            yield return Combine(string.Empty, string.Empty, text, location);
            foreach (var prefix in _logicalPrefixes!)
            {
                yield return Combine(prefix, prefix, text, location);
            }

            yield break;
        }

        if (_baseRoot is not null)
        {
            yield return Combine(_baseRoot, _baseRoot, text, location);
        }

        foreach (var root in _dataRoots!)
        {
            yield return Combine(root, root, text, location);
        }
    }

    /// <summary>
    ///     Combines a directory and a path text into a candidate confined to a root.
    /// </summary>
    /// <param name="directory">The directory to start from (the root itself).</param>
    /// <param name="root">The root the result must stay under.</param>
    /// <param name="text">The validated path text.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the result is not under the root.</exception>
    private Candidate Combine(string directory, string root, string text, VegaLiteLocation location)
    {
        var path = UsesResolver ? CombineLogical(directory, root, text) : CombineFileSystem(directory, root, text);
        return path is null
            ? throw new VegaLiteRenderException($"The data path '{text}' is outside the allowed directories.", location)
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
    ///     Walks the components of a candidate from the root down to the file and rejects the first link.
    /// </summary>
    /// <param name="candidate">The candidate; its key is a full path under its root.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns><see langword="true"/> when no component is a link and every component above the file exists (the file itself is left to the open attempt); <see langword="false"/> when a component above the file does not exist.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a component is a link, or cannot be checked.</exception>
    private bool WalkRejectingLinks(Candidate candidate, string display, VegaLiteLocation location)
    {
        // The root itself and everything above it are trusted; only components strictly below it are examined
        var current = candidate.Root;
        var segments = Path.GetRelativePath(candidate.Root, candidate.Key).Split(Path.DirectorySeparatorChar);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Join(current, segments[index]);
            if (!_linkStates.TryGetValue(current, out var isLink))
            {
                // The first missing component ends the walk: nothing below it can exist, so it is never probed. The
                // final component is the exception: an existence probe also says "missing" for an existing file it
                // cannot access, so the open attempt (not the probe) decides whether the file is missing or unreadable
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    return index == segments.Length - 1;
                }

                isLink = ProbeLink(current, display, location);
                _linkStates[current] = isLink;
            }

            if (isLink)
            {
                throw new VegaLiteRenderException(
                    $"The data file '{display}' passes through a link ('{Path.GetRelativePath(candidate.Root, current).Replace('\\', '/')}'); links are not followed inside the allowed directories.",
                    location);
            }
        }

        return true;
    }

    /// <summary>
    ///     Asks the link probe about one existing component, failing closed when the probe cannot answer.
    /// </summary>
    /// <param name="path">The full path of the component.</param>
    /// <param name="display">The name shown in messages.</param>
    /// <param name="location">The location of the path.</param>
    /// <returns><see langword="true"/> when the component is a symbolic link or junction.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the probe fails.</exception>
    private bool ProbeLink(string path, string display, VegaLiteLocation location)
    {
        try
        {
            return _isLink(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new VegaLiteRenderException($"The data file '{display}' cannot be checked for links and is not read.", location);
        }
    }

    /// <summary>
    ///     The default link probe: a symbolic link or junction has a link target, whether it names a file or a directory.
    /// </summary>
    /// <param name="path">The full path of an existing file or directory.</param>
    /// <returns><see langword="true"/> when the path is a symbolic link or junction.</returns>
    private static bool DefaultIsLink(string path) =>
        (Directory.Exists(path) ? new DirectoryInfo(path).LinkTarget : new FileInfo(path).LinkTarget) is not null;

    /// <summary>
    ///     Computes the roots on first use, so a document without file data never touches the path options.
    /// </summary>
    /// <param name="location">The location of the path being resolved, for errors.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when a configured directory is not a valid path.</exception>
    private void EnsureRoots(VegaLiteLocation location)
    {
        if (_dataRoots is not null)
        {
            return;
        }

        if (UsesResolver)
        {
            _logicalPrefixes = _options.DataPaths.Select(NormalizePrefix).OfType<string>().ToList();
            _dataRoots = [];
            return;
        }

        try
        {
            _baseRoot = _options.BaseDirectory is null ? null : Path.GetFullPath(_options.BaseDirectory);
            _dataRoots = _options.DataPaths.Select(Path.GetFullPath).ToList();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new VegaLiteRenderException("A configured data directory (BaseDirectory or DataPaths) is not a valid path.", location);
        }

        _logicalPrefixes = [];
    }

    /// <summary>
    ///     Normalizes a data path into a logical directory prefix.
    /// </summary>
    /// <param name="path">The data path.</param>
    /// <returns>The prefix, or <see langword="null"/> when the path is not a valid logical path.</returns>
    private static string? NormalizePrefix(string path) =>
        DescribeInvalidPath(path) is null ? CombineLogical(string.Empty, string.Empty, path.Replace('\\', '/')) : null;
}
