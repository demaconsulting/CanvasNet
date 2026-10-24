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
///         <see cref="BaseDirectory"/>, <see cref="LibraryPaths"/> and <see cref="FileResolver"/> are accepted
///         and stored now but are not consumed until file resolution is added in a later phase; parsing never
///         accesses another file. <see cref="SourceName"/> is consumed immediately: it appears in every
///         <see cref="ScadSourceLocation"/>.
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
    ///     Gets the directory against which relative <c>include</c>/<c>use</c> paths will be resolved, or
    ///     <see langword="null"/> to use the folder of the opened file (or the current directory for strings and streams).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        init => _baseDirectory = ValidateOptional(value);
    }

    /// <summary>
    ///     Gets the additional library directories that will be searched for referenced files; empty by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown on initialization when an entry is empty or whitespace.</exception>
    public IReadOnlyList<string> LibraryPaths
    {
        get => _libraryPaths;
        init => _libraryPaths = CopyPaths(value, nameof(LibraryPaths));
    }

    /// <summary>
    ///     Gets a callback that will supply the content of referenced files in place of the file system, or
    ///     <see langword="null"/>. It is never invoked while parsing.
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
