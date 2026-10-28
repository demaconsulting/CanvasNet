using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     A parsed Vega-Lite specification: the validated result of reading specification text from a string, a
///     stream or a file, ready to be measured and rendered.
/// </summary>
/// <remarks>
///     <para>
///         A document proves that the text is well-formed JSON within the limits of <see cref="VegaLiteLimits"/> and
///         describes the chart without drawing it (<see cref="SchemaUrl"/>, <see cref="Title"/>,
///         <see cref="MarkTypes"/>, <see cref="DataReferences"/>, <see cref="ParameterNames"/>), so tools can list
///         what a chart needs before they render it. Opening never evaluates anything, never touches another file and
///         never uses the network. <see cref="Measure(VegaLiteRenderOptions?, CancellationToken)"/> and the
///         <c>Render</c> methods produce the image under the rules of <see cref="VegaLiteRenderOptions"/>.
///     </para>
///     <para>
///         Opening is fail-fast and fail-closed: the first problem throws a <see cref="VegaLiteSpecException"/> with
///         its file, line, column and JSON pointer; nesting deeper than <see cref="VegaLiteLimits.MaxJsonDepth"/> is
///         a <see cref="VegaLiteSpecException"/> too, and source longer than
///         <see cref="VegaLiteLimits.MaxSourceLength"/> characters is an <see cref="InvalidDataException"/>; neither
///         can overflow the stack or exhaust memory. A <c>$schema</c> of Vega-Lite version 5 or 6 is interpreted with
///         the version 6 defaults; other versions are rendered with those defaults too and reported with the warning
///         <c>vl-schema-version</c> when the document is measured or rendered.
///     </para>
///     <para>
///         A document is immutable and safe for concurrent use: it can be measured and rendered from several threads
///         at once and every call returns a new surface. It holds no unmanaged resources, so it is not disposable, and
///         a stream passed to <see cref="Open(Stream, VegaLiteOpenOptions?)"/> is left open.
///     </para>
///     <para>
///         Rendering supports a limited subset of the language: marks and encodings that the renderer does not
///         support are reported with stable <c>vl-*</c> identifiers (see <see cref="VegaLiteUnsupportedFeature"/>),
///         never skipped silently.
///     </para>
/// </remarks>
public sealed partial class VegaLiteDocument
{
    /// <summary>The number of characters read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The metadata read from the text.</summary>
    private readonly VegaLiteSpecInfo _info;

    /// <summary>
    ///     Initializes a document from the metadata of its text and the effective options.
    /// </summary>
    /// <param name="info">The metadata read from the text.</param>
    /// <param name="options">The effective options (base directory and source name resolved).</param>
    private VegaLiteDocument(VegaLiteSpecInfo info, VegaLiteOpenOptions options)
    {
        _info = info;
        Options = options;
        SourceName = options.SourceName;
        MarkTypes = info.Marks.Select(m => m.Type).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the source name used in locations and messages, or <see langword="null"/> when the document is unnamed.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the <c>$schema</c> of the specification exactly as written, or <see langword="null"/> when there is none.
    /// </summary>
    public string? SchemaUrl => _info.SchemaUrl;

    /// <summary>
    ///     Gets the Vega-Lite major version named by <see cref="SchemaUrl"/> (for example 5 for
    ///     <c>https://vega.github.io/schema/vega-lite/v5.json</c>), or <see langword="null"/> when there is no schema
    ///     or it does not follow the Vega-Lite naming.
    /// </summary>
    public int? SchemaMajorVersion => _info.SchemaMajorVersion;

    /// <summary>
    ///     Gets the plain text of the title (several lines are joined with a line feed), or <see langword="null"/>
    ///     when the specification has no title text.
    /// </summary>
    /// <remarks>
    ///     Only the title of the top-level specification is reported. A title given as an expression reference is
    ///     not evaluated here, so it is reported as <see langword="null"/>.
    /// </remarks>
    public string? Title => _info.Title;

    /// <summary>
    ///     Gets the mark types in document order, as written (for example <c>bar</c>, or the <c>type</c> of a mark
    ///     definition object), including those of nested layers, concatenations and facets; empty for a specification
    ///     without marks.
    /// </summary>
    public IReadOnlyList<string> MarkTypes { get; }

    /// <summary>
    ///     Gets the data sources the specification names, in document order, including those of nested views.
    /// </summary>
    /// <remarks>
    ///     A view without a <c>data</c> object contributes no entry (it inherits its parent's data). The references
    ///     describe the text only: no file is opened and no URL is contacted to produce them.
    /// </remarks>
    public IReadOnlyList<VegaLiteDataReference> DataReferences => _info.DataReferences;

    /// <summary>
    ///     Gets the distinct names of the parameters (<c>params</c>) the specification declares, in document order.
    /// </summary>
    /// <remarks>
    ///     These are the names that <see cref="VegaLiteRenderOptions.Parameters"/> may override.
    /// </remarks>
    public IReadOnlyList<string> ParameterNames => _info.ParameterNames;

    /// <summary>
    ///     Gets the effective options, with the base directory and source name resolved (internal; consumed by the
    ///     data stage).
    /// </summary>
    internal VegaLiteOpenOptions Options { get; }

    /// <summary>
    ///     Gets the metadata read from the text (internal; consumed by the render pipeline).
    /// </summary>
    internal VegaLiteSpecInfo Info => _info;

    /// <summary>
    ///     Parses Vega-Lite specification text.
    /// </summary>
    /// <remarks>
    ///     A leading U+FEFF is ignored. Parsing is deterministic and does not depend on the culture or the current
    ///     directory.
    /// </remarks>
    /// <param name="json">The specification text. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON or not a valid specification, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="json"/> is longer than <see cref="VegaLiteLimits.MaxSourceLength"/> characters.</exception>
    public static VegaLiteDocument Parse(string json, VegaLiteOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Create(json, options ?? new VegaLiteOpenOptions());
    }

    /// <summary>
    ///     Opens and parses a specification file.
    /// </summary>
    /// <remarks>
    ///     The file is read as UTF-8 (a byte-order mark is detected and removed). Unless overridden in
    ///     <paramref name="options"/>, the source name is <paramref name="path"/> exactly as supplied and the base
    ///     directory for data files is the folder of the full path. Reading stops as soon as the size limit is
    ///     exceeded.
    /// </remarks>
    /// <param name="path">The path of the file. Must not be null, empty or whitespace.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON or not a valid specification, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file contains more than <see cref="VegaLiteLimits.MaxSourceLength"/> characters.</exception>
    public static VegaLiteDocument Open(string path, VegaLiteOpenOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Apply the documented defaults: the path names the source and its folder is the base directory
        var given = options ?? new VegaLiteOpenOptions();
        var effective = given.WithEffective(
            given.BaseDirectory ?? Path.GetDirectoryName(Path.GetFullPath(path)),
            given.SourceName ?? path);

        using var stream = File.OpenRead(path);
        return Create(ReadBounded(stream, effective.EffectiveLimits.MaxSourceLength), effective);
    }

    /// <summary>
    ///     Parses Vega-Lite specification text read from a stream.
    /// </summary>
    /// <remarks>
    ///     The stream is read from its current position to its end as UTF-8 (a byte-order mark is detected and
    ///     removed) and is left open; the caller keeps ownership. Reading stops as soon as the size limit is
    ///     exceeded, so a stream that never ends cannot consume unbounded memory. Data files of the chart can only be
    ///     found through <see cref="VegaLiteOpenOptions.BaseDirectory"/>, <see cref="VegaLiteOpenOptions.DataPaths"/>
    ///     or <see cref="VegaLiteOpenOptions.FileResolver"/>, because a stream has no folder.
    /// </remarks>
    /// <param name="stream">A readable stream. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not valid JSON or not a valid specification, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream contains more than <see cref="VegaLiteLimits.MaxSourceLength"/> characters.</exception>
    public static VegaLiteDocument Open(Stream stream, VegaLiteOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effective = options ?? new VegaLiteOpenOptions();
        return Create(ReadBounded(stream, effective.EffectiveLimits.MaxSourceLength), effective);
    }

    /// <summary>
    ///     Scans source text and wraps the result in a document.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="effective">The effective options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the text is not a valid specification.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the source limit.</exception>
    private static VegaLiteDocument Create(string source, VegaLiteOpenOptions effective)
    {
        var limits = effective.EffectiveLimits;
        if (source.Length > limits.MaxSourceLength)
        {
            throw CreateSourceTooLongException(limits.MaxSourceLength);
        }

        // A leading byte-order mark is not part of the JSON text
        var text = source.Length > 0 && source[0] == '\uFEFF' ? source[1..] : source;
        var info = new VegaLiteSpecScanner(VegaLiteSpecScanner.Encode(text), effective.SourceName, limits.MaxJsonDepth).Scan();
        return new VegaLiteDocument(info, effective);
    }

    /// <summary>
    ///     Reads a whole stream as text, stopping with an error once the size limit is exceeded.
    /// </summary>
    /// <param name="stream">The stream to read; it is left open.</param>
    /// <param name="maxLength">The maximum number of characters.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <paramref name="maxLength"/>.</exception>
    private static string ReadBounded(Stream stream, int maxLength)
    {
        // Read in chunks and check the running length so an endless stream stops one chunk past the limit
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: ReadChunkSize, leaveOpen: true);
        var text = new StringBuilder();
        var buffer = new char[ReadChunkSize];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > maxLength)
            {
                throw CreateSourceTooLongException(maxLength);
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Creates the exception for source that exceeds the size limit.
    /// </summary>
    /// <param name="maxLength">The limit that was exceeded.</param>
    /// <returns>The exception, ready to throw.</returns>
    private static InvalidDataException CreateSourceTooLongException(int maxLength) =>
        new($"The Vega-Lite specification is longer than the limit of {maxLength} characters (VegaLiteLimits.MaxSourceLength).");
}
