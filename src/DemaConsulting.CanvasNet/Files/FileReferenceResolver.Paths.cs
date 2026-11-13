namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Path handling of <see cref="FileReferenceResolver"/>: the text rules that apply under every policy, and the
///     candidates a reference is looked up at.
/// </summary>
/// <remarks>
///     <para>
///         <b>Text rules.</b> Backslashes are first turned into forward slashes, so a reference means the same on every
///         platform. Refused before anything is touched: an empty or white-space reference, a control character
///         (including NUL, DEL and the C1 range), a device path (<c>\\?\</c>, <c>\\.\</c>), a network path (UNC
///         <c>\\server\share</c> and <c>//host</c>), and a colon anywhere except in the drive of an absolute Windows
///         path (<c>C:/x</c>): an alternate data stream, a drive-relative path and, on Linux and macOS, a drive path.
///         <c>..</c> and absolute paths are allowed; whether the file they reach may be read is the policy's decision.
///     </para>
///     <para>
///         <b>File-system candidates</b> are full paths with <c>.</c> and <c>..</c> collapsed and links not resolved.
///         On Windows a reference that the system maps to a device (a legacy device name such as <c>NUL</c> in a
///         component) is refused before the open as well.
///         <b>Logical candidates</b> (resolver callback) are normalized logical paths: forward slashes, no leading slash,
///         <c>.</c> and <c>..</c> collapsed; an absolute reference and a <c>..</c> above the logical root are refused,
///         because there is no real path to judge them by. Search paths that are valid logical paths are prefixes.
///     </para>
/// </remarks>
public sealed partial class FileReferenceResolver
{
    /// <summary>The full path of the base directory, or <see langword="null"/>; computed on first use.</summary>
    private string? _baseDirectory;

    /// <summary>The full paths of the search directories (file-system mode); computed on first use.</summary>
    private List<string>? _searchDirectories;

    /// <summary>The valid logical search prefixes (callback mode); computed on first use.</summary>
    private List<string>? _logicalPrefixes;

    /// <summary>
    ///     A place a referenced file may be found.
    /// </summary>
    /// <param name="Key">The full path as written (or the logical path in callback mode).</param>
    /// <param name="Origin">
    ///     The host-chosen directory the lookup chain started from: the base directory or the search path of this
    ///     candidate, or the origin of the referring file. In callback mode it holds the logical directory, which is
    ///     never given to a policy (callback mode never asks the policy).
    /// </param>
    private sealed record Candidate(string Key, string Origin);

    /// <summary>
    ///     Gets the reference as it may be shown in a message: control characters replaced by <c>?</c>.
    /// </summary>
    /// <param name="reference">The reference as written.</param>
    /// <returns>The printable reference.</returns>
    private static string Shown(string reference) =>
        reference.Any(char.IsControl) ? new string(reference.Select(c => char.IsControl(c) ? '?' : c).ToArray()) : reference;

    /// <summary>
    ///     Validates and normalizes the text of a reference.
    /// </summary>
    /// <param name="raw">The reference as written.</param>
    /// <param name="shown">The printable reference.</param>
    /// <returns>The text with backslashes turned into forward slashes.</returns>
    /// <exception cref="FileReferenceException">Thrown when the text breaks a rule.</exception>
    private string ValidateText(string raw, string shown)
    {
        var problem = DescribeInvalidText(raw, UsesCallback);
        return problem is { } broken
            ? throw new FileReferenceException(broken.Kind, shown, broken.Reason)
            : raw.Replace('\\', '/');
    }

    /// <summary>
    ///     Explains why the text of a reference is not acceptable.
    /// </summary>
    /// <param name="raw">The reference as written.</param>
    /// <param name="logical">Whether the reference is for a resolver callback (logical paths).</param>
    /// <returns>The broken rule, or <see langword="null"/> when the text is acceptable.</returns>
    private static (FileReferenceFailureKind Kind, string Reason)? DescribeInvalidText(string raw, bool logical)
    {
        // The checks run from the most basic to the most specific, so the reason names the first rule that is broken
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (FileReferenceFailureKind.EmptyPath, "the path is empty");
        }

        if (raw.Any(char.IsControl))
        {
            return (FileReferenceFailureKind.ControlCharacter, "the path contains a control character");
        }

        var text = raw.Replace('\\', '/');
        if (text.StartsWith("//?/", StringComparison.Ordinal) || text.StartsWith("//./", StringComparison.Ordinal))
        {
            return (FileReferenceFailureKind.InvalidPath, "device paths are not permitted");
        }

        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            return (FileReferenceFailureKind.NetworkPath, "network paths are not permitted");
        }

        if (logical && (text[0] == '/' || HasDrive(text)))
        {
            return (FileReferenceFailureKind.InvalidPath, "absolute paths have no meaning for the file source");
        }

        // The only colon a local path may carry is the drive of an absolute Windows path; any other is an alternate data
        // stream, a drive-relative path, or a drive on a system that has none
        var allowedColon = !logical && OperatingSystem.IsWindows() && HasDrive(text) ? 1 : -1;
        return text.IndexOf(':', allowedColon + 1) >= 0
            ? (FileReferenceFailureKind.InvalidPath, "the path contains a colon")
            : null;
    }

    /// <summary>
    ///     Determines whether a text starts with a drive and a separator (<c>C:/</c>).
    /// </summary>
    /// <param name="text">The text, with forward slashes.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasDrive(string text) =>
        text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] == '/';

    /// <summary>
    ///     Generates the candidates of a reference in search order.
    /// </summary>
    /// <param name="text">The validated text.</param>
    /// <param name="shown">The printable reference.</param>
    /// <param name="relativeTo">The referring file, or <see langword="null"/>.</param>
    /// <returns>The candidates.</returns>
    private IEnumerable<Candidate> Candidates(string text, string shown, FileReference? relativeTo)
    {
        EnsureDirectories();
        if (UsesCallback)
        {
            yield return Logical(relativeTo?.Directory ?? string.Empty, text, shown);
            foreach (var prefix in _logicalPrefixes!)
            {
                yield return Logical(prefix, text, shown);
            }

            yield break;
        }

        // An absolute reference names one file; it is resolved against the starting directory only for its drive. Its
        // chain keeps the origin of the referring file, or starts at the first configured directory
        if (Path.IsPathRooted(text))
        {
            var start = relativeTo?.Directory ?? _baseDirectory ?? _searchDirectories![0];
            var origin = relativeTo?.Origin ?? _baseDirectory ?? _searchDirectories![0];
            yield return FullCandidate(start, origin, text, shown);
            yield break;
        }

        // A reference relative to a referring file is combined with that file's directory, but the chain keeps the
        // trusted origin of the referring file: the referrer's folder may have been swapped for a link since it was read
        if (relativeTo is not null)
        {
            yield return FullCandidate(relativeTo.Directory, relativeTo.Origin!, text, shown);
        }
        else if (_baseDirectory is not null)
        {
            yield return FullCandidate(_baseDirectory, _baseDirectory, text, shown);
        }

        foreach (var directory in _searchDirectories!)
        {
            yield return FullCandidate(directory, directory, text, shown);
        }
    }

    /// <summary>
    ///     Combines a directory and a reference into a file-system candidate.
    /// </summary>
    /// <param name="directory">The full path of the directory.</param>
    /// <param name="origin">The host-chosen directory the lookup chain started from.</param>
    /// <param name="text">The validated text.</param>
    /// <param name="shown">The printable reference.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="FileReferenceException">Thrown when the result is not a valid path or names a device.</exception>
    private static Candidate FullCandidate(string directory, string origin, string text, string shown)
    {
        string full;
        try
        {
            full = Path.GetFullPath(text, directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new FileReferenceException(FileReferenceFailureKind.InvalidPath, shown, "the path is not a valid path", ex);
        }

        // Windows maps a legacy device name in a component (NUL, and on older versions CON, COM1, LPT1 and others) to a
        // device path; such a path is refused before the open, under every policy. A host that configured an
        // extended-length directory (\\?\C:\docs) keeps working: there no device names are mapped
        if (OperatingSystem.IsWindows() && IsDevicePath(full) && !IsDevicePath(directory))
        {
            throw new FileReferenceException(FileReferenceFailureKind.InvalidPath, shown, "the path names a device");
        }

        return new Candidate(full, origin);
    }

    /// <summary>
    ///     Determines whether a full path is in the Windows device namespace (<c>\\.\</c> or <c>\\?\</c>).
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsDevicePath(string path) =>
        path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal);

    /// <summary>
    ///     Combines a logical directory and a reference into a logical candidate.
    /// </summary>
    /// <param name="directory">The logical directory (empty for the root).</param>
    /// <param name="text">The validated text.</param>
    /// <param name="shown">The printable reference.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="FileReferenceException">Thrown when a <c>..</c> leaves the logical root.</exception>
    private static Candidate Logical(string directory, string text, string shown) =>
        CombineLogical(directory, text) is { } path
            ? new Candidate(path, directory)
            : throw new FileReferenceException(FileReferenceFailureKind.InvalidPath, shown, "the path leaves the root of the file source");

    /// <summary>
    ///     Combines a logical directory and a text, collapsing <c>.</c> and <c>..</c>.
    /// </summary>
    /// <param name="directory">The logical directory.</param>
    /// <param name="text">The text, with forward slashes.</param>
    /// <returns>The normalized logical path, or <see langword="null"/> when a <c>..</c> would leave the logical root.</returns>
    private static string? CombineLogical(string directory, string text)
    {
        var segments = directory.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var segment in text.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
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
    ///     Computes the configured directories on first use, so a resolver that is never asked for a file never touches
    ///     the path options.
    /// </summary>
    /// <exception cref="FileReferenceException">Thrown when a configured directory is not a valid path.</exception>
    private void EnsureDirectories()
    {
        if (_searchDirectories is not null)
        {
            return;
        }

        if (UsesCallback)
        {
            _logicalPrefixes = _searchPaths.Select(NormalizePrefix).OfType<string>().ToList();
            _searchDirectories = [];
            return;
        }

        _baseDirectory = _options.BaseDirectory is null ? null : FullDirectory(_options.BaseDirectory);
        _searchDirectories = _searchPaths.Select(FullDirectory).ToList();
        _logicalPrefixes = [];
    }

    /// <summary>
    ///     Computes the full path of a configured directory.
    /// </summary>
    /// <param name="directory">The configured directory.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="FileReferenceException">Thrown when the directory is not a valid path.</exception>
    private static string FullDirectory(string directory)
    {
        try
        {
            return Path.GetFullPath(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new FileReferenceException(FileReferenceFailureKind.InvalidBaseDirectory, directory ?? string.Empty, null, ex);
        }
    }

    /// <summary>
    ///     Normalizes a search path into a logical directory prefix.
    /// </summary>
    /// <param name="path">The search path.</param>
    /// <returns>The prefix, or <see langword="null"/> when the path is not a valid relative logical path.</returns>
    private static string? NormalizePrefix(string path) =>
        path is not null && DescribeInvalidText(path, logical: true) is null
            ? CombineLogical(string.Empty, path.Replace('\\', '/'))
            : null;
}
