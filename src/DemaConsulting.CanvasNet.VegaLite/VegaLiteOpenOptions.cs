namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     Options that control how a <see cref="VegaLiteDocument"/> is opened.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and documents. Collections are copied
///         defensively, so later changes to the caller's list have no effect.
///     </para>
///     <para>
///         <see cref="SourceName"/> and <see cref="Limits"/> are consumed when the document is opened (the name
///         appears in every <see cref="VegaLiteLocation"/>, and the limits bound the source size and nesting).
///         <see cref="BaseDirectory"/>, <see cref="DataPaths"/> and <see cref="FileResolver"/> define the sandbox
///         for data files and are consumed when the chart is rendered; opening never accesses another file. Only
///         the directories given here (and the folder of an opened file) are used, and a path that leaves them is
///         rejected. The current directory and environment variables are never used implicitly, and a data
///         <c>url</c> that names a remote resource is never fetched.
///     </para>
/// </remarks>
public sealed class VegaLiteOpenOptions
{
    /// <summary>The validated base directory.</summary>
    private readonly string? _baseDirectory;

    /// <summary>The validated, copied data search paths.</summary>
    private readonly IReadOnlyList<string> _dataPaths = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>The validated source name.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Gets the directory against which relative data <c>url</c> paths are resolved and outside of which no file
    ///     is read, or <see langword="null"/> to use the folder of the opened file.
    /// </summary>
    /// <remarks>
    ///     A document created from a string or a stream has no folder: unless it supplies a base directory, a
    ///     <see cref="DataPaths"/> entry or a <see cref="FileResolver"/>, its data files cannot be resolved and
    ///     rendering a chart that needs one fails with a <see cref="VegaLiteRenderException"/>. A symbolic link or
    ///     junction below this directory is never followed. These checks protect against path-based escapes in static
    ///     file trees; they are not a security boundary against a concurrent local attacker with write access to the
    ///     data folders.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        init => _baseDirectory = ValidateOptional(value);
    }

    /// <summary>
    ///     Gets the additional sandbox roots that are searched, after the base directory, for data files; empty by
    ///     default.
    /// </summary>
    /// <remarks>
    ///     The same link rule as for <see cref="BaseDirectory"/> applies below each entry. The list is copied when it
    ///     is set.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown on initialization when an entry is empty or whitespace.</exception>
    public IReadOnlyList<string> DataPaths
    {
        get => _dataPaths;
        init => _dataPaths = CopyPaths(value, nameof(DataPaths));
    }

    /// <summary>
    ///     Gets a callback that supplies the content of data files in place of the file system, or
    ///     <see langword="null"/>. It is never invoked while opening. When set, rendering does not touch the file
    ///     system at all: the callback receives a normalized logical path (forward slashes, no leading slash) and
    ///     returns a readable stream, which the library disposes, or <see langword="null"/> when there is no such
    ///     file. It is only called on the thread that called the render method.
    /// </summary>
    public Func<string, Stream?>? FileResolver { get; init; }

    /// <summary>
    ///     Gets the name placed in every <see cref="VegaLiteLocation.File"/> and diagnostic, or
    ///     <see langword="null"/> to leave locations unnamed (opening a file defaults it to the path).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? SourceName
    {
        get => _sourceName;
        init => _sourceName = ValidateOptional(value);
    }

    /// <summary>
    ///     Gets the limits that apply while the specification is read (source length and JSON depth), or
    ///     <see langword="null"/> to use <see cref="VegaLiteLimits.Default"/>.
    /// </summary>
    /// <remarks>
    ///     The limits of rendering are set separately in <see cref="VegaLiteRenderOptions.Limits"/>, because the same
    ///     document can be rendered under different budgets.
    /// </remarks>
    public VegaLiteLimits? Limits { get; init; }

    /// <summary>
    ///     Gets the limits that are in effect: <see cref="Limits"/>, or the defaults when none were given.
    /// </summary>
    internal VegaLiteLimits EffectiveLimits => Limits ?? VegaLiteLimits.Default;

    /// <summary>
    ///     Creates a copy with the effective base directory and source name filled in.
    /// </summary>
    /// <param name="baseDirectory">The effective base directory.</param>
    /// <param name="sourceName">The effective source name.</param>
    /// <returns>The new options object.</returns>
    internal VegaLiteOpenOptions WithEffective(string? baseDirectory, string? sourceName) => new()
    {
        BaseDirectory = baseDirectory,
        DataPaths = _dataPaths,
        FileResolver = FileResolver,
        SourceName = sourceName,
        Limits = Limits,
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
