using DemaConsulting.CanvasNet.Files;

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
///         <see cref="BaseDirectory"/>, <see cref="DataPaths"/> and <see cref="FileResolver"/> say where data files
///         are found, and <see cref="FileAccess"/> which of them may be read; they are consumed when the chart is
///         rendered, and opening never accesses another file. Only the directories given here (and the folder of an
///         opened file) are searched for a relative path. The current directory and environment variables are never
///         used implicitly, and a data <c>url</c> that names a remote resource is never fetched.
///     </para>
/// </remarks>
public sealed class VegaLiteOpenOptions
{
    /// <summary>The validated file-access policy.</summary>
    private readonly FileAccessPolicy _fileAccess = FileAccessPolicy.AllowAll;

    /// <summary>The validated base directory.</summary>
    private readonly string? _baseDirectory;

    /// <summary>The validated, copied data search paths.</summary>
    private readonly IReadOnlyList<string> _dataPaths = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>The validated source name.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Gets the directory against which relative data <c>url</c> paths are resolved (a base, not a boundary), or
    ///     <see langword="null"/> to use the folder of the opened file.
    /// </summary>
    /// <remarks>
    ///     A document created from a string or a stream has no folder: unless it supplies a base directory, a
    ///     <see cref="DataPaths"/> entry or a <see cref="FileResolver"/>, its data files cannot be resolved and
    ///     rendering a chart that needs one fails with a <see cref="VegaLiteRenderException"/>. Whether a data file may
    ///     be read, wherever it lies and whether it is reached through a symbolic link or junction, is decided by
    ///     <see cref="FileAccess"/>; to keep data files inside this directory and refuse links, use
    ///     <c>FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(directory), FileAccessPolicy.NoSymlinks)</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        init => _baseDirectory = ValidateOptional(value);
    }

    /// <summary>
    ///     Gets the additional directories that are searched, after the base directory, for data files; empty by
    ///     default.
    /// </summary>
    /// <remarks>
    ///     The entries are search locations, not boundaries: <see cref="FileAccess"/> decides which files may be read,
    ///     and a policy that confines files must list each entry among its roots. The list is copied when it is set.
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
    ///     system at all: the callback receives a normalized logical path (forward slashes, no leading slash, <c>.</c>
    ///     and <c>..</c> collapsed, never leaving the logical root) and
    ///     returns a readable stream, which the library disposes, or <see langword="null"/> when there is no such
    ///     file. The base directory plays no role then, and each <see cref="DataPaths"/> entry that is a valid
    ///     relative logical path is a directory prefix that is tried after the root. It is only called on the thread
    ///     that called the measure or render method, at most once per candidate path and call, and never for remote data
    ///     or an absolute path (an absolute path is a located error in this mode). The content must be UTF-8 and is bounded by
    ///     <see cref="VegaLiteLimits.MaxDataBytes"/>; a <see langword="null"/> result for every candidate makes the
    ///     chart fail with a <see cref="VegaLiteRenderException"/>.
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
    ///     Gets the policy that decides which local data files (and lookup sources) may be read; the default,
    ///     <see cref="FileAccessPolicy.AllowAll"/>, allows every regular local file, including one reached through
    ///     <c>..</c>, an absolute path, a local <c>file:</c> URI or a symbolic link. It is not consulted with a
    ///     <see cref="FileResolver"/>, which sees only logical paths.
    /// </summary>
    /// <remarks>
    ///     A denied file is a <see cref="VegaLiteRenderException"/> located at the <c>url</c>, whose
    ///     <see cref="Exception.InnerException"/> is the <see cref="FileReferenceException"/> of the denial. Remote data,
    ///     network paths, device paths and anything that is not a regular file are refused under every policy, and every
    ///     limit applies whatever the policy.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is null.</exception>
    public FileAccessPolicy FileAccess
    {
        get => _fileAccess;
        init => _fileAccess = value ?? throw new ArgumentNullException(nameof(FileAccess));
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
        FileAccess = _fileAccess,
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
