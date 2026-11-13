using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     File access of <see cref="ScadFileResolver"/>: the bridge to the core file engine, the decoding and parsing of
///     what it returns, the display names of files and the translation of its refusals into OpenSCAD wording.
/// </summary>
/// <remarks>
///     <para>
///         <b>Delegation.</b> Everything that makes the access safe is done by the core
///         <see cref="FileReferenceResolver"/>: validation of the path text (an empty path, a control character, a
///         network or device path, a colon other than a drive), the file-access policy of the caller judged on the real
///         path of the opened file, the regular-file check, the open and bounded read, the resolver-callback mode with
///         normalized logical paths and the per-call file cache. The candidates are, in order, the directory of the
///         including file and each library path; the including file is the <see cref="FileReference"/> that the
///         previous resolution returned.
///     </para>
///     <para>
///         <b>What stays here.</b> The bytes the engine returns are decoded as UTF-8 with byte-order-mark detection and
///         bounded to <see cref="ScadParser.MaxSourceLength"/> characters, parsed once per file, and named for locations
///         (the path relative to the base directory with forward slashes when the file is under it, otherwise the full
///         or logical path). A missing file is a warning; every other refusal, including a denial of the policy and a
///         file that exists but cannot be read (a directory where a file is expected, a failing callback), is a located
///         <see cref="ScadEvaluationException"/> whose inner exception is the <see cref="FileReferenceException"/>.
///     </para>
/// </remarks>
internal sealed partial class ScadFileResolver
{
    /// <summary>
    ///     The largest file, in bytes, the engine may read: four bytes per character of the source limit (the widest
    ///     encoding the decoder detects, UTF-32) plus a four-byte byte-order mark, so the exact character limit is
    ///     enforced on the decoded text and the byte bound only stops hostile streams.
    /// </summary>
    private const long MaxSourceBytes = (4L * ScadParser.MaxSourceLength) + 4;

    /// <summary>The core engine that finds, checks and reads the files.</summary>
    private readonly FileReferenceResolver _engine;

    /// <summary>The full path of the base directory, or <see langword="null"/> when there is none; computed on first use.</summary>
    private string? _baseRoot;

    /// <summary>Whether <see cref="_baseRoot"/> has been computed.</summary>
    private bool _baseRootKnown;

    /// <summary>
    ///     Computes the identity of the main file so that a file that includes it is detected as a cycle.
    /// </summary>
    /// <param name="mainName">The display name of the main file.</param>
    /// <returns>The full path in file-system mode when the name is a valid path; otherwise <see langword="null"/>.</returns>
    private string? MainKey(string? mainName)
    {
        if (mainName is null || _options.FileResolver is not null)
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
    /// <param name="referrer">The file that contains the directive, or <see langword="null"/> for the main file.</param>
    /// <returns>The loaded file, or <see langword="null"/> when no candidate exists.</returns>
    /// <exception cref="ScadEvaluationException">Thrown for a refused or denied file.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file exceeds the source size limit.</exception>
    private LoadedFile? Resolve(ScadDirectiveStatement directive, FileReference? referrer)
    {
        _cancellation.ThrowIfCancellationRequested();
        FileReference? found;
        try
        {
            found = _engine.Resolve(directive.Path, referrer);
        }
        catch (FileReferenceException ex)
        {
            throw Translate(ex, directive.Location);
        }

        if (found is null)
        {
            return null;
        }

        // The file is parsed once per path: its display name, and so every location in it, depends on the path
        if (!_files.TryGetValue(found.Key, out var loaded))
        {
            var display = DisplayName(found.Key);
            loaded = new LoadedFile(found.Key, display, ScadParser.Parse(ScadDocument.DecodeBounded(found.Content), display), found);
            _files[found.Key] = loaded;
        }

        return loaded;
    }

    /// <summary>
    ///     Translates a refusal of the file engine into the OpenSCAD wording.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>
    ///     The exception to throw: a located evaluation error whose inner exception is <paramref name="ex"/>, or the
    ///     source-size error for a file that is too large. The message names the file as written (or, under the base
    ///     directory, relative to it), never a host directory.
    /// </returns>
    internal Exception Translate(FileReferenceException ex, ScadSourceLocation location)
    {
        var shown = Shown(ex);
        return ex.Kind switch
        {
            FileReferenceFailureKind.EmptyPath or FileReferenceFailureKind.ControlCharacter or FileReferenceFailureKind.InvalidPath
                or FileReferenceFailureKind.NetworkPath =>
                new ScadEvaluationException($"Include path '{ex.Reference}' is not allowed: {ex.Reason}", location, ex),
            FileReferenceFailureKind.AccessDenied => new ScadEvaluationException(
                $"Include file '{shown}' was denied by the file-access policy '{ex.PolicyName}': {ex.Reason}", location, ex),
            FileReferenceFailureKind.RealPathUnavailable => new ScadEvaluationException(
                $"Include file '{shown}' cannot be checked: its real path cannot be determined on this system, so the "
                + $"file-access policy '{ex.PolicyName}' cannot be applied",
                location,
                ex),
            FileReferenceFailureKind.NotARegularFile => new ScadEvaluationException(
                $"Include file '{shown}' is not a regular file", location, ex),
            FileReferenceFailureKind.TooLarge => ScadParser.CreateSourceTooLongException(),
            FileReferenceFailureKind.InvalidBaseDirectory => new ScadEvaluationException(
                "A configured directory (BaseDirectory or LibraryPaths) is not a valid path", location, ex),
            _ => new ScadEvaluationException($"Include file '{shown}' cannot be read", location, ex),
        };
    }

    /// <summary>
    ///     Gets the name of a refused file for a message: relative to the base directory when the file is under it,
    ///     otherwise the reference as written, so no host directory is shown.
    /// </summary>
    /// <param name="ex">The refusal.</param>
    /// <returns>The name.</returns>
    private string Shown(FileReferenceException ex)
    {
        if (ex.RequestedPath is not { } requested)
        {
            return ex.Reference;
        }

        var display = DisplayName(requested);
        return string.Equals(display, requested, StringComparison.Ordinal) && _options.FileResolver is null ? ex.Reference : display;
    }

    /// <summary>
    ///     Gets the name shown in source locations for a file.
    /// </summary>
    /// <param name="key">The file key: a full path, or a logical path in resolver mode.</param>
    /// <returns>The logical path in resolver mode; the path relative to the base directory (forward slashes) when the file is under it; otherwise the full path.</returns>
    private string DisplayName(string key)
    {
        if (_options.FileResolver is not null)
        {
            return key;
        }

        // The base directory was validated by the engine before any file was found, so computing it here cannot fail
        if (!_baseRootKnown)
        {
            _baseRoot = _options.BaseDirectory is null ? null : Path.GetFullPath(_options.BaseDirectory);
            _baseRootKnown = true;
        }

        if (_baseRoot is null)
        {
            return key;
        }

        // A file outside the base directory (or on another drive) has a ".."-leading or rooted relative path and keeps its full path
        var relative = Path.GetRelativePath(_baseRoot, key);
        var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative);
        return outside ? key : relative.Replace('\\', '/');
    }
}
