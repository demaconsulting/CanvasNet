using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     File access of <see cref="ScadFileResolver"/>: the bridge to the core sandbox, the decoding and parsing of what it
///     returns, the display names of files and the translation of sandbox refusals into OpenSCAD wording.
/// </summary>
/// <remarks>
///     <para>
///         <b>Delegation.</b> Everything that makes the access safe is done by the core
///         <see cref="SandboxedFileResolver"/>: validation of the path text (an empty path, a control character, a colon,
///         a leading slash), confinement of every candidate to the root it was derived from (the base directory or a
///         library path), the refusal of symbolic links and junctions below a root whatever their target (a dangling
///         final link included), the open and bounded read, the resolver-callback mode with normalized logical paths and
///         the per-call file cache. The candidates are, in order, the directory of the including file and each library
///         path; the including file is the <see cref="SandboxedFile"/> that the previous resolution returned.
///     </para>
///     <para>
///         <b>What stays here.</b> The bytes the sandbox returns are decoded as UTF-8 with byte-order-mark detection and
///         bounded to <see cref="ScadParser.MaxSourceLength"/> characters, parsed once per file, and named for locations
///         (the path relative to the base directory with forward slashes when the file is under it, otherwise the full
///         or logical path). A missing file is a warning; every other sandbox refusal, including a file that exists but
///         cannot be read (a directory where a file is expected, a failing callback), is a located
///         <see cref="ScadEvaluationException"/>.
///     </para>
/// </remarks>
internal sealed partial class ScadFileResolver
{
    /// <summary>
    ///     The largest file, in bytes, the sandbox may read: four bytes per character of the source limit (the widest
    ///     encoding the decoder detects, UTF-32) plus a four-byte byte-order mark, so the exact character limit is
    ///     enforced on the decoded text and the byte bound only stops hostile streams.
    /// </summary>
    private const long MaxSourceBytes = (4L * ScadParser.MaxSourceLength) + 4;

    /// <summary>The core sandbox that finds and reads the files.</summary>
    private readonly SandboxedFileResolver _sandbox;

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
    /// <exception cref="ScadEvaluationException">Thrown for a sandbox refusal.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file exceeds the source size limit.</exception>
    private LoadedFile? Resolve(ScadDirectiveStatement directive, SandboxedFile? referrer)
    {
        _cancellation.ThrowIfCancellationRequested();
        SandboxedFile? found;
        try
        {
            found = _sandbox.Resolve(directive.Path, referrer);
        }
        catch (SandboxException ex)
        {
            throw Translate(ex, directive.Location);
        }

        if (found is null)
        {
            return null;
        }

        // The file is parsed once; its locations carry the display name so errors point into the right file
        if (!_files.TryGetValue(found.Key, out var loaded))
        {
            var display = DisplayName(found.Key);
            loaded = new LoadedFile(found.Key, display, ScadParser.Parse(ScadDocument.DecodeBounded(found.Content), display), found);
            _files[found.Key] = loaded;
        }

        return loaded;
    }

    /// <summary>
    ///     Translates a sandbox refusal into the OpenSCAD wording.
    /// </summary>
    /// <param name="ex">The sandbox exception.</param>
    /// <param name="location">The location of the directive.</param>
    /// <returns>The exception to throw: a located evaluation error, or the source-size error for a file that is too large.</returns>
    internal Exception Translate(SandboxException ex, ScadSourceLocation location) => ex.Kind switch
    {
        SandboxFailureKind.EmptyPath or SandboxFailureKind.ControlCharacter or SandboxFailureKind.ColonInPath
            or SandboxFailureKind.AbsolutePath => new ScadEvaluationException($"Include path '{ex.Path}' is not allowed: {ex.Detail}", location),
        SandboxFailureKind.OutsideRoot => new ScadEvaluationException($"Include path '{ex.Path}' is outside the allowed directories", location),
        SandboxFailureKind.ThroughLink => new ScadEvaluationException(
            $"Include file '{DisplayName(ex.Path)}' passes through a link ('{ex.Detail}'); links are not followed inside the allowed directories",
            location),
        SandboxFailureKind.LinkCheckFailed => new ScadEvaluationException(
            $"Include file '{DisplayName(ex.Path)}' cannot be checked for links and is not read", location),
        SandboxFailureKind.TooLarge => ScadParser.CreateSourceTooLongException(),
        SandboxFailureKind.InvalidRoot => new ScadEvaluationException(
            "A configured directory (BaseDirectory or LibraryPaths) is not a valid path", location),
        _ => new ScadEvaluationException($"Include file '{DisplayName(ex.Path)}' cannot be read", location),
    };

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

        // The base directory was validated by the sandbox before any file was found, so computing it here cannot fail
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
