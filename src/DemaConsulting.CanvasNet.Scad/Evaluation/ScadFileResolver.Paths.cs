using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Path handling of <see cref="ScadFileResolver"/>: validation, candidate generation, the sandbox and reading.
/// </summary>
/// <remarks>
///     <para>
///         <b>Path text.</b> Backslashes are first turned into forward slashes, so <c>a\b.scad</c> and
///         <c>a/b.scad</c> are the same path on every platform. An empty path, a control character (including NUL),
///         a colon anywhere (drive letters and alternate data streams) and a leading slash (absolute and UNC paths)
///         are sandbox violations. A <c>..</c> that would leave the root a candidate is derived from is a sandbox
///         violation too; <c>..</c> that stays inside the root is fine.
///     </para>
///     <para>
///         <b>File-system mode</b> (no resolver callback): the roots are the base directory and each library path as
///         full paths. A candidate is the full path of the text combined with a directory and must lie under the root
///         that directory belongs to (ordinal comparison; case-insensitive on Windows and macOS). A file that is a link
///         whose final target is outside every root is rejected. Directory-level symbolic links inside a root are not
///         detected (a limitation of the framework file API; the roots are chosen by the caller).
///     </para>
///     <para>
///         <b>Resolver mode</b>: no file system access happens. Candidates are normalized logical paths (forward
///         slashes, no leading slash, <c>.</c> and <c>..</c> collapsed) handed to the callback; the first candidate that
///         yields a stream wins. The logical root is the empty path; each library path that is itself a valid logical
///         path is used as a logical directory prefix, and library paths that are not valid logical paths are ignored.
///         The base directory plays no role in this mode.
///     </para>
/// </remarks>
internal sealed partial class ScadFileResolver
{
    /// <summary>The comparison used to decide whether a path is under a root and to compare file keys on this platform.</summary>
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The comparer for file keys in file-system mode.</summary>
    private static readonly StringComparer PlatformKeyComparer =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The loaded files by candidate key; <see langword="null"/> remembers a candidate that does not exist.</summary>
    private readonly Dictionary<string, LoadedFile?> _files;

    /// <summary>The comparer for file keys: platform path comparison in file-system mode, ordinal for logical paths.</summary>
    private readonly StringComparer _keyComparer;

    /// <summary>The full path of the base directory, or <see langword="null"/>; computed on first use.</summary>
    private string? _baseRoot;

    /// <summary>The full paths of the library directories; computed on first use.</summary>
    private List<string>? _libraryRoots;

    /// <summary>The valid logical library prefixes (resolver mode); computed on first use.</summary>
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
    ///     Creates the directory context of the main file.
    /// </summary>
    /// <returns>The base directory as both directory and root; the logical root in resolver mode; <see langword="null"/> when there is no base directory.</returns>
    private FileContext? CreateMainContext()
    {
        if (UsesResolver)
        {
            return new FileContext(string.Empty, string.Empty);
        }

        EnsureRoots();
        return _baseRoot is null ? null : new FileContext(_baseRoot, _baseRoot);
    }

    /// <summary>
    ///     Computes the identity of the main file so that a file that includes it is detected as a cycle.
    /// </summary>
    /// <param name="mainName">The display name of the main file.</param>
    /// <returns>The full path in file-system mode when the name is a valid path; otherwise <see langword="null"/>.</returns>
    private string? MainKey(string? mainName)
    {
        if (mainName is null || UsesResolver)
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(mainName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Finds, reads and parses the file a directive refers to.
    /// </summary>
    /// <param name="directive">The directive.</param>
    /// <param name="context">The directory context of the file that contains it, or <see langword="null"/>.</param>
    /// <returns>The loaded file, or <see langword="null"/> when no candidate exists.</returns>
    /// <exception cref="ScadEvaluationException">Thrown for a sandbox violation.</exception>
    private LoadedFile? Resolve(ScadDirectiveStatement directive, FileContext? context)
    {
        _cancellation.ThrowIfCancellationRequested();
        var text = ValidatePathText(directive.Path, directive.Location);

        // Candidates are produced lazily: the first one that exists wins, so later roots are only examined on a miss
        foreach (var candidate in Candidates(text, context, directive.Location))
        {
            var file = LoadCandidate(candidate, directive.Location);
            if (file is not null)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    ///     Validates and normalizes the path text of a directive.
    /// </summary>
    /// <param name="raw">The text between the angle brackets.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>The text with backslashes turned into forward slashes.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the text is empty, contains a control character or a colon, or is absolute.</exception>
    private static string ValidatePathText(string raw, ScadSourceLocation location)
    {
        var reason = DescribeInvalidPath(raw);
        if (reason is not null)
        {
            throw new ScadEvaluationException($"Include path '{Printable(raw)}' is not allowed: {reason}", location);
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
    /// <param name="context">The directory context of the including file.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>The candidates: the including file's directory first, then each library path.</returns>
    private IEnumerable<Candidate> Candidates(string text, FileContext? context, ScadSourceLocation location)
    {
        EnsureRoots();
        if (context is not null)
        {
            yield return Combine(context.Directory, context.Root, text, location);
        }

        foreach (var root in UsesResolver ? _logicalPrefixes! : _libraryRoots!)
        {
            yield return Combine(root, root, text, location);
        }
    }

    /// <summary>
    ///     Combines a directory and a path text into a candidate confined to a root.
    /// </summary>
    /// <param name="directory">The directory to start from.</param>
    /// <param name="root">The root the result must stay under.</param>
    /// <param name="text">The validated path text.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the result is not under the root.</exception>
    private Candidate Combine(string directory, string root, string text, ScadSourceLocation location)
    {
        var path = UsesResolver ? CombineLogical(directory, root, text) : CombineFileSystem(directory, root, text);
        return path is null
            ? throw new ScadEvaluationException($"Include path '{text}' is outside the allowed directories", location)
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
    ///     Loads a candidate through the cache.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="location">The location of the directive, for sandbox errors.</param>
    /// <returns>The loaded file, or <see langword="null"/> when it does not exist.</returns>
    private LoadedFile? LoadCandidate(Candidate candidate, ScadSourceLocation location)
    {
        if (_files.TryGetValue(candidate.Key, out var cached))
        {
            return cached;
        }

        _cancellation.ThrowIfCancellationRequested();
        var text = ReadText(candidate, location);
        LoadedFile? loaded = null;
        if (text is not null)
        {
            // The file is parsed once; its locations carry the display name so errors point into the right file
            var display = DisplayName(candidate.Key);
            loaded = new LoadedFile(
                candidate.Key,
                display,
                ScadParser.Parse(text, display),
                new FileContext(DirectoryOf(candidate.Key), candidate.Root));
        }

        _files[candidate.Key] = loaded;
        return loaded;
    }

    /// <summary>
    ///     Reads the text of a candidate from the resolver callback or the file system.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="location">The location of the directive, for sandbox errors.</param>
    /// <returns>The text, or <see langword="null"/> when the candidate does not exist.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file is longer than the source size limit.</exception>
    private string? ReadText(Candidate candidate, ScadSourceLocation location)
    {
        if (UsesResolver)
        {
            // The callback owns what it returns until it is handed to us; we dispose it after reading
            using var stream = _options.FileResolver!(candidate.Key);
            return stream is null ? null : ScadDocument.ReadBounded(stream);
        }

        if (!File.Exists(candidate.Key))
        {
            return null;
        }

        RejectEscapingLink(candidate.Key, location);
        using var file = File.OpenRead(candidate.Key);
        return ScadDocument.ReadBounded(file);
    }

    /// <summary>
    ///     Rejects a file that is a link to a target outside every allowed root.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="location">The location of the directive.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the final link target is outside every root.</exception>
    private void RejectEscapingLink(string path, ScadSourceLocation location)
    {
        var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
        if (target is null)
        {
            return;
        }

        var roots = _libraryRoots!.Concat(_baseRoot is null ? [] : [_baseRoot]);
        if (!roots.Any(root => IsUnder(target.FullName, root)))
        {
            throw new ScadEvaluationException($"Include file '{DisplayName(path)}' is a link to a location outside the allowed directories", location);
        }
    }

    /// <summary>
    ///     Gets the name shown in source locations for a file.
    /// </summary>
    /// <param name="key">The candidate key.</param>
    /// <returns>The logical path in resolver mode; the path relative to the base directory (forward slashes) when the file is under it; otherwise the full path.</returns>
    private string DisplayName(string key)
    {
        if (UsesResolver || _baseRoot is null || !IsUnder(key, _baseRoot))
        {
            return key;
        }

        return Path.GetRelativePath(_baseRoot, key).Replace('\\', '/');
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
    ///     Computes the roots on first use, so a document without directives never touches the path options.
    /// </summary>
    private void EnsureRoots()
    {
        if (_libraryRoots is not null)
        {
            return;
        }

        if (UsesResolver)
        {
            _logicalPrefixes = _options.LibraryPaths.Select(NormalizePrefix).OfType<string>().ToList();
            _libraryRoots = [];
            return;
        }

        _baseRoot = _options.BaseDirectory is null ? null : Path.GetFullPath(_options.BaseDirectory);
        _libraryRoots = _options.LibraryPaths.Select(Path.GetFullPath).ToList();
        _logicalPrefixes = [];
    }

    /// <summary>
    ///     Normalizes a library path into a logical directory prefix.
    /// </summary>
    /// <param name="path">The library path.</param>
    /// <returns>The prefix, or <see langword="null"/> when the path is not a valid logical path.</returns>
    private static string? NormalizePrefix(string path) =>
        DescribeInvalidPath(path) is null ? CombineLogical(string.Empty, string.Empty, path.Replace('\\', '/')) : null;
}
