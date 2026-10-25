namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     Options that control how a <see cref="ScadDocument"/> is opened.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and documents. Collections are
///         copied defensively, so later changes to the caller's list have no effect.
///     </para>
///     <para>
///         <see cref="SourceName"/> is consumed when the document is parsed: it appears in every
///         <see cref="ScadSourceLocation"/>. <see cref="BaseDirectory"/>, <see cref="LibraryPaths"/> and
///         <see cref="FileResolver"/> are consumed when the document is evaluated
///         (<see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/>) to find the files named by
///         <c>include</c> and <c>use</c>; parsing never accesses another file. Only the directories given here (and
///         the folder of an opened file) are searched, and a path that leaves them is rejected; environment variables
///         are ignored.
///     </para>
/// </remarks>
public sealed class ScadOpenOptions
{
    /// <summary>The validated base directory.</summary>
    private readonly string? _baseDirectory;

    /// <summary>The validated, copied library search paths.</summary>
    private readonly IReadOnlyList<string> _libraryPaths = [];

    /// <summary>The validated source name.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Gets the directory against which relative <c>include</c>/<c>use</c> paths are resolved and outside of which
    ///     no file is read, or <see langword="null"/> to use the folder of the opened file.
    /// </summary>
    /// <remarks>
    ///     A document created from a string or a stream has no folder: unless it supplies a base directory, a
    ///     <see cref="LibraryPaths"/> entry or a <see cref="FileResolver"/>, its <c>include</c> and <c>use</c>
    ///     directives cannot be resolved and evaluating them fails with a <see cref="ScadEvaluationException"/>. The
    ///     current directory is never used implicitly. A symbolic link or junction below this directory (a directory on
    ///     the path to an included file, or the file itself) is never followed: the file is rejected whatever the
    ///     link's target. The directory itself may be reached through links.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        init => _baseDirectory = ValidateOptional(value);
    }

    /// <summary>
    ///     Gets the additional library directories that are searched, after the including file's folder, for
    ///     referenced files; empty by default.
    /// </summary>
    /// <remarks>
    ///     The same link rule as for <see cref="BaseDirectory"/> applies below each entry. To use linked content, add
    ///     the real directory as an entry.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown on initialization when an entry is empty or whitespace.</exception>
    public IReadOnlyList<string> LibraryPaths
    {
        get => _libraryPaths;
        init => _libraryPaths = CopyPaths(value, nameof(LibraryPaths));
    }

    /// <summary>
    ///     Gets a callback that supplies the content of referenced files in place of the file system, or
    ///     <see langword="null"/>. It is never invoked while parsing. When set, evaluation does not touch the file
    ///     system at all: the callback receives normalized logical paths (forward slashes, no leading slash) and
    ///     returns a readable stream, which the evaluator disposes, or <see langword="null"/> when there is no such file.
    ///     It is only called on the thread that called <c>Evaluate</c>.
    /// </summary>
    public Func<string, Stream?>? FileResolver { get; init; }

    /// <summary>
    ///     Gets the name placed in every <see cref="ScadSourceLocation.File"/> and diagnostic, or
    ///     <see langword="null"/> to leave locations unnamed (opening a file defaults it to the path).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? SourceName
    {
        get => _sourceName;
        init => _sourceName = ValidateOptional(value);
    }

    /// <summary>
    ///     Creates a copy with the effective base directory and source name filled in.
    /// </summary>
    /// <param name="baseDirectory">The effective base directory.</param>
    /// <param name="sourceName">The effective source name.</param>
    /// <returns>The new options object.</returns>
    internal ScadOpenOptions WithEffective(string? baseDirectory, string? sourceName) => new()
    {
        BaseDirectory = baseDirectory,
        LibraryPaths = _libraryPaths,
        FileResolver = FileResolver,
        SourceName = sourceName,
    };

    /// <summary>
    ///     Validates an optional text value: null is allowed, empty or whitespace is not.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="parameterName">The property name for the exception.</param>
    /// <returns>The value, unchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is empty or whitespace.</exception>
    private static string? ValidateOptional(string? value, [System.Runtime.CompilerServices.CallerMemberName] string? parameterName = null)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value must be null or contain a non-whitespace character.", parameterName);
        }

        return value;
    }

    /// <summary>
    ///     Validates and copies a list of paths.
    /// </summary>
    /// <param name="paths">The paths.</param>
    /// <param name="propertyName">The property name for exceptions.</param>
    /// <returns>A read-only copy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the list or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry is empty or whitespace.</exception>
    private static IReadOnlyList<string> CopyPaths(IReadOnlyList<string> paths, string propertyName)
    {
        if (paths is null)
        {
            throw new ArgumentNullException(propertyName);
        }
        var copy = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            if (path is null)
            {
                throw new ArgumentNullException(propertyName, "Entries must not be null.");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Entries must contain a non-whitespace character.", propertyName);
            }
            copy.Add(path);
        }

        return copy.AsReadOnly();
    }
}
