using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     Options that control how a <see cref="PlantUmlDocument"/> is opened.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SourceName"/> appears in every <see cref="PlantUmlLocation"/> of the document, and
///         <see cref="Limits"/> bound what is checked while the text is read (<see cref="PlantUmlLimits.MaxTextSize"/> and
///         the structural limits of the diagram parser). Opening never uses the network.
///     </para>
///     <para>
///         <see cref="BaseDirectory"/> and <see cref="FileAccess"/> are consumed while the text is read, to find and
///         read the local files named by <c>!include</c>, <c>!include_once</c> and <c>!include_many</c>; only the
///         directory given here (or the folder of an opened file) is searched, and which of the files found may be read
///         is decided by <see cref="FileAccess"/>, which by default allows every regular local file. A URL, the standard
///         library (<c>&lt;name&gt;</c>), <c>!includeurl</c> and <c>!includesub</c> are never followed.
///     </para>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and documents.
///     </para>
/// </remarks>
public sealed class PlantUmlOpenOptions
{
    /// <summary>The validated source name.</summary>
    private readonly string? _sourceName;

    /// <summary>The validated base directory.</summary>
    private readonly string? _baseDirectory;

    /// <summary>The validated file-access policy.</summary>
    private readonly FileAccessPolicy _fileAccess = FileAccessPolicy.AllowAll;

    /// <summary>
    ///     Gets the name placed in every <see cref="PlantUmlLocation.File"/> and diagnostic, or <see langword="null"/>
    ///     to leave locations unnamed (opening a file defaults it to the path).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? SourceName
    {
        get => _sourceName;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value must be null or contain a non-whitespace character.", nameof(SourceName));
            }

            _sourceName = value;
        }
    }

    /// <summary>
    ///     Gets the directory against which the relative paths of the include directives are resolved (a base, not a
    ///     boundary), or <see langword="null"/> to use the folder of the opened file.
    /// </summary>
    /// <remarks>
    ///     A document parsed from a string or a stream has no folder: unless it supplies a base directory, its include
    ///     directives cannot be resolved and opening it fails with a <see cref="PlantUmlSyntaxException"/> whose code is
    ///     <see cref="PlantUmlDiagnosticCodes.IncludeFailed"/>. The current directory is never used implicitly. A file
    ///     included by another file is looked up in the folder of that file first.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value must be null or contain a non-whitespace character.", nameof(BaseDirectory));
            }

            _baseDirectory = value;
        }
    }

    /// <summary>
    ///     Gets the policy that decides which files named by the include directives may be read; the default,
    ///     <see cref="FileAccessPolicy.AllowAll"/>, allows every regular local file, including one reached through
    ///     <c>..</c>, an absolute path or a symbolic link.
    /// </summary>
    /// <remarks>
    ///     A denied file is a <see cref="PlantUmlSyntaxException"/> with the code
    ///     <see cref="PlantUmlDiagnosticCodes.IncludeFailed"/>, located at the directive, whose
    ///     <see cref="Exception.InnerException"/> is the <see cref="FileReferenceException"/> of the denial. Network
    ///     paths, device paths and anything that is not a regular file are refused under every policy.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is null.</exception>
    public FileAccessPolicy FileAccess
    {
        get => _fileAccess;
        init => _fileAccess = value ?? throw new ArgumentNullException(nameof(FileAccess));
    }

    /// <summary>
    ///     Gets the limits that apply while the text is read, or <see langword="null"/> to use
    ///     <see cref="PlantUmlLimits.Default"/>.
    /// </summary>
    /// <remarks>
    ///     The limits of measuring and rendering are set separately in <see cref="PlantUmlRenderOptions.Limits"/>,
    ///     because the same document can be rendered under different budgets.
    /// </remarks>
    public PlantUmlLimits? Limits { get; init; }

    /// <summary>
    ///     Gets the limits that are in effect: <see cref="Limits"/>, or the defaults when none were given.
    /// </summary>
    internal PlantUmlLimits EffectiveLimits => Limits ?? PlantUmlLimits.Default;
}
