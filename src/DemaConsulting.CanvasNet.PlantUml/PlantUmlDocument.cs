using System.Text;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     A parsed PlantUML diagram: the validated result of reading PlantUML text from a string, a stream or a file, ready
///     to be measured and rendered.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading.</b> The first <c>@startuml</c> ... <c>@enduml</c> block of the text is the diagram (a text without
///         an <c>@start</c> tag is one diagram, as PlantUML reads a piped text). It is preprocessed (comments, continued
///         lines, and the supported subset of the preprocessor: <c>!define</c>, <c>!undef</c> and <c>!$variable</c>),
///         its diagram type is detected as PlantUML detects it, and a supported diagram is parsed. Reading is fail-fast
///         and fail-closed: text that holds no diagram, an unknown <c>@start</c> tag, or a statement the diagram cannot
///         read throws a <see cref="PlantUmlSyntaxException"/> with the file, line and column in the original text, a
///         stable code and a hint; text longer than <see cref="PlantUmlLimits.MaxTextSize"/> allows is an
///         <see cref="InvalidDataException"/>. Reading never uses the network; it reads the local files of the include
///         directives only under <see cref="PlantUmlOpenOptions.FileAccess"/>.
///     </para>
///     <para>
///         <b>Diagram types.</b> Every diagram type that PlantUML knows is recognized (<see cref="DiagramType"/>), but
///         only the supported ones are drawn (<see cref="IsSupported"/>). Any other known type opens without error;
///         measuring or rendering it reports the unsupported feature
///         <see cref="PlantUmlDiagnosticCodes.DiagramUnsupported"/> and gives an empty, transparent picture of 20 by 20
///         pixels (twice PlantUML's document margin), or throws <see cref="PlantUmlUnsupportedFeatureException"/> in
///         strict mode.
///     </para>
///     <para>
///         <b>Thread safety.</b> A document is immutable and safe for concurrent use: it can be measured and rendered from
///         several threads at once and every call returns a new surface. It holds no unmanaged resources, so it is not
///         disposable, and a stream passed to <see cref="Open(Stream, PlantUmlOpenOptions?)"/> is left open.
///     </para>
/// </remarks>
public sealed partial class PlantUmlDocument
{
    /// <summary>The number of bytes read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The length of the UTF-8 byte-order mark.</summary>
    private const int MaxPreambleLength = 3;

    /// <summary>The strict UTF-8 decoder: invalid bytes are an error, never U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Initializes a document from its preprocessed text and its detected type.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram type.</param>
    /// <param name="diagram">The parsed diagram of a supported type, or null for a type that is not drawn.</param>
    /// <param name="sourceName">The source name.</param>
    private PlantUmlDocument(PreprocessedSource source, DetectedDiagram detected, IPlantUmlDiagram? diagram, string? sourceName)
    {
        Source = source;
        Detected = detected;
        Diagram = diagram;
        SourceName = sourceName;
    }

    /// <summary>
    ///     Gets the source name used in locations and messages, or <see langword="null"/> when the document is unnamed.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the diagram type: <c>sequence</c>, <c>activity</c>, <c>class</c>, <c>object</c>, or the name of another
    ///     PlantUML type such as <c>usecase</c>, <c>component</c>, <c>deployment</c>, <c>state</c>,
    ///     <c>timing</c>, <c>activity-legacy</c>, <c>mindmap</c>, <c>gantt</c>, <c>wbs</c>, <c>json</c>, <c>yaml</c> or
    ///     <c>salt</c>.
    /// </summary>
    public string DiagramType => Detected.DiagramType;

    /// <summary>
    ///     Gets a value indicating whether this version draws the diagram type; when it does not, rendering reports
    ///     <see cref="PlantUmlDiagnosticCodes.DiagramUnsupported"/> and draws nothing.
    /// </summary>
    public bool IsSupported => Detected.IsSupported;

    /// <summary>
    ///     Gets the title of the diagram (its <c>title</c> statement, the lines of a multi-line title joined with line
    ///     feeds), or <see langword="null"/> when it has none or its type is not supported.
    /// </summary>
    public string? Title => Diagram?.Title;

    /// <summary>
    ///     Gets the number of nodes of the diagram (the participants of a sequence diagram); zero for a type that is not
    ///     supported.
    /// </summary>
    public int NodeCount => Diagram?.NodeCount ?? 0;

    /// <summary>
    ///     Gets the number of edges of the diagram (the messages of a sequence diagram); zero for a type that is not
    ///     supported.
    /// </summary>
    public int EdgeCount => Diagram?.EdgeCount ?? 0;

    /// <summary>
    ///     Gets the number of subgraphs of the diagram (the boxes of a sequence diagram); zero for a type that is not
    ///     supported or has none.
    /// </summary>
    public int SubgraphCount => Diagram?.SubgraphCount ?? 0;

    /// <summary>
    ///     Gets the preprocessed text (internal; consumed by every measure and render).
    /// </summary>
    internal PreprocessedSource Source { get; }

    /// <summary>
    ///     Gets the detected diagram type (internal).
    /// </summary>
    internal DetectedDiagram Detected { get; }

    /// <summary>
    ///     Gets the parsed diagram of a supported type, or null (internal).
    /// </summary>
    internal IPlantUmlDiagram? Diagram { get; }

    /// <summary>
    ///     Parses PlantUML text.
    /// </summary>
    /// <remarks>
    ///     Parsing is deterministic and does not depend on the culture or the current directory.
    /// </remarks>
    /// <param name="text">The diagram text. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when the text holds no diagram, its tag is unknown, a structural limit is exceeded, an included file cannot be read (<see cref="PlantUmlDiagnosticCodes.IncludeFailed"/>; a refusal of the file-access policy is the inner exception), or a statement cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="PlantUmlLimits.MaxTextSize"/> allows.</exception>
    public static PlantUmlDocument Parse(string text, PlantUmlOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var effective = options ?? new PlantUmlOpenOptions();
        return Create(text, effective.SourceName, effective.EffectiveLimits, Includes(effective, effective.BaseDirectory, null));
    }

    /// <summary>
    ///     Opens and parses a PlantUML file.
    /// </summary>
    /// <remarks>
    ///     The file is read as strict UTF-8 (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced), and
    ///     reading stops as soon as the size limit is certainly exceeded. Unless overridden in <paramref name="options"/>,
    ///     the source name is <paramref name="path"/> exactly as supplied, and the include directives start in the folder
    ///     of the file.
    /// </remarks>
    /// <param name="path">The path of the file. Must not be null, empty or whitespace.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when the file is not valid UTF-8, not a valid diagram, or an included file cannot be read (<see cref="PlantUmlDiagnosticCodes.IncludeFailed"/>).</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="PlantUmlLimits.MaxTextSize"/> allows.</exception>
    public static PlantUmlDocument Open(string path, PlantUmlOpenOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var effective = options ?? new PlantUmlOpenOptions();
        using var stream = File.OpenRead(path);
        var text = ReadBounded(stream, effective.EffectiveLimits, effective.SourceName ?? path);

        // The includes of a file start in its folder unless the caller names another one
        var fullPath = Path.GetFullPath(path);
        var includes = Includes(effective, effective.BaseDirectory ?? Path.GetDirectoryName(fullPath), fullPath);
        return Create(text, effective.SourceName ?? path, effective.EffectiveLimits, includes);
    }

    /// <summary>
    ///     Parses PlantUML text read from a stream.
    /// </summary>
    /// <remarks>
    ///     The stream is read from its current position to its end as strict UTF-8 (a byte-order mark is removed; invalid
    ///     UTF-8 is rejected) and is left open; the caller keeps ownership. Reading stops as soon as the size limit is
    ///     certainly exceeded, so a stream that never ends cannot consume unbounded memory.
    /// </remarks>
    /// <param name="stream">A readable stream. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when the bytes are not valid UTF-8, not a valid diagram, or an included file cannot be read (<see cref="PlantUmlDiagnosticCodes.IncludeFailed"/>).</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="PlantUmlLimits.MaxTextSize"/> allows.</exception>
    public static PlantUmlDocument Open(Stream stream, PlantUmlOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effective = options ?? new PlantUmlOpenOptions();
        var text = ReadBounded(stream, effective.EffectiveLimits, effective.SourceName);
        return Create(text, effective.SourceName, effective.EffectiveLimits, Includes(effective, effective.BaseDirectory, null));
    }

    /// <summary>
    ///     Preprocesses, detects and parses a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="includes">The resolver of the include directives.</param>
    /// <returns>The document.</returns>
    private static PlantUmlDocument Create(string text, string? sourceName, PlantUmlLimits limits, IncludeResolver includes)
    {
        // The pipeline of PlantUML's reading: frame and preprocess, detect the type, then parse a supported type
        var source = Preprocessor.Run(text, sourceName, limits, includes);
        var detected = DiagramDetector.Detect(source);
        var diagram = detected.IsSupported ? DiagramRegistry.Parse(detected, source, limits) : null;
        return new PlantUmlDocument(source, detected, diagram, sourceName);
    }

    /// <summary>
    ///     Creates the resolver of the include directives of one open call.
    /// </summary>
    /// <param name="options">The effective open options.</param>
    /// <param name="baseDirectory">The directory relative paths start in, or null when the text has no folder.</param>
    /// <param name="referrerPath">The full path of the opened file, or null.</param>
    /// <returns>The resolver.</returns>
    private static IncludeResolver Includes(PlantUmlOpenOptions options, string? baseDirectory, string? referrerPath) =>
        new(baseDirectory, referrerPath, options.FileAccess, options.EffectiveLimits);

    /// <summary>
    ///     Reads a whole stream as strict UTF-8, stopping with an error once the size limit is certainly exceeded.
    /// </summary>
    /// <param name="stream">The stream; it is left open.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name, for the error location.</param>
    /// <returns>The text without a byte-order mark.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than the limit allows.</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the bytes are not valid UTF-8.</exception>
    private static string ReadBounded(Stream stream, PlantUmlLimits limits, string? sourceName)
    {
        // A character takes at most three bytes per UTF-16 unit, so more bytes than that is over the limit whatever the content
        var maxChars = (long)limits.MaxTextSize * Preprocessor.SourceLengthFactor;
        var maxBytes = (3 * maxChars) + MaxPreambleLength;
        using var bytes = new MemoryStream();
        var buffer = new byte[ReadChunkSize];
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            bytes.Write(buffer, 0, count);
            if (bytes.Length > maxBytes)
            {
                throw new InvalidDataException(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"The PlantUML text is longer than {Preprocessor.SourceLengthFactor} times the limit of {limits.MaxTextSize} characters (PlantUmlLimits.MaxTextSize)."));
            }
        }

        // Strict decoding: malformed UTF-8 is an error, never U+FFFD
        var data = bytes.GetBuffer().AsSpan(0, (int)bytes.Length);
        if (data.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            data = data[3..];
        }

        try
        {
            return StrictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.SyntaxError,
                "the text is not valid UTF-8",
                new PlantUmlLocation(sourceName, 0, 0),
                "save the file with UTF-8 encoding",
                null);
        }
    }
}
