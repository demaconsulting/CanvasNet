using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     A parsed Mermaid diagram: the validated result of reading Mermaid text from a string, a stream or a file, ready to
///     be measured and rendered.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading.</b> The text is preprocessed as Mermaid preprocesses it (the YAML frontmatter, <c>%%{init: ...}%%</c>
///         directives and <c>%%</c> comments are taken out), its diagram type is detected with Mermaid's detectors, and a
///         supported diagram is parsed with a port of its grammar. Reading is fail-fast and fail-closed: text that is not a
///         Mermaid diagram, invalid frontmatter YAML or a grammar error throws a <see cref="MermaidSyntaxException"/> with the
///         file, line and column in the original text, a stable code, a hint and the expected tokens; text longer than
///         <see cref="MermaidLimits.MaxTextSize"/> is an <see cref="InvalidDataException"/>. Reading never touches another file
///         and never uses the network.
///     </para>
///     <para>
///         <b>Diagram types.</b> Every diagram type that Mermaid knows is recognized (<see cref="DiagramType"/>), but only the
///         supported ones are drawn (<see cref="IsSupported"/>): in this version flowcharts (<c>flowchart</c> and
///         <c>graph</c>), sequence diagrams (<c>sequenceDiagram</c>), class diagrams (<c>classDiagram</c>), state diagrams
///         (<c>stateDiagram</c>), entity relationship diagrams (<c>erDiagram</c>), pie charts (<c>pie</c>), gantt charts
///         (<c>gantt</c>) and mindmaps (<c>mindmap</c>). Any other known type (<c>journey</c>, <c>timeline</c>, ...) opens without
///         error; measuring or rendering it reports the unsupported feature
///         <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> and gives an empty, transparent picture of 16 by 16 pixels
///         (twice Mermaid's diagram padding), or throws <see cref="MermaidUnsupportedFeatureException"/> in strict mode.
///     </para>
///     <para>
///         <b>Thread safety.</b> A document is immutable and safe for concurrent use: it can be measured and rendered from
///         several threads at once and every call returns a new surface. It holds no unmanaged resources, so it is not
///         disposable, and a stream passed to <see cref="Open(Stream, MermaidOpenOptions?)"/> is left open.
///     </para>
/// </remarks>
public sealed partial class MermaidDocument
{
    /// <summary>The number of bytes read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The length of the UTF-8 byte-order mark.</summary>
    private const int MaxPreambleLength = 3;

    /// <summary>The strict UTF-8 decoder: invalid bytes are an error, never U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Initializes a document from its preprocessed text, its detected type and its parsed diagram.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram type.</param>
    /// <param name="diagram">The parsed diagram of a supported type, or null for a type that is not drawn.</param>
    /// <param name="sourceName">The source name.</param>
    private MermaidDocument(PreprocessedSource source, DetectedDiagram detected, IMermaidDiagram? diagram, string? sourceName)
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
    ///     Gets the diagram type: <c>flowchart</c> (written <c>flowchart</c> or <c>graph</c>), or the name of another
    ///     Mermaid type such as <c>sequence</c>, <c>class</c>, <c>state</c>, <c>er</c>, <c>gantt</c>, <c>pie</c> or
    ///     <c>mindmap</c>.
    /// </summary>
    public string DiagramType => Detected.DiagramType;

    /// <summary>
    ///     Gets a value indicating whether this version draws the diagram type; when it does not, rendering reports
    ///     <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> and draws nothing.
    /// </summary>
    public bool IsSupported => Detected.IsSupported;

    /// <summary>
    ///     Gets the title of the diagram (its own title statement, else the frontmatter <c>title</c>), or
    ///     <see langword="null"/> when it has none.
    /// </summary>
    public string? Title => Diagram?.Title ?? Source.Frontmatter?.Title;

    /// <summary>
    ///     Gets the accessible title (<c>accTitle</c>), or <see langword="null"/> when the diagram has none or its type is
    ///     not supported. It is metadata only and is not drawn.
    /// </summary>
    public string? AccTitle => Diagram?.AccTitle;

    /// <summary>
    ///     Gets the direction of a diagram that has one (<c>TB</c>, <c>TD</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>), or
    ///     <see langword="null"/> (always for a sequence diagram).
    /// </summary>
    public string? Direction => Diagram?.Direction;

    /// <summary>
    ///     Gets the number of nodes of the diagram (subgraphs not included; the participants of a sequence diagram); zero
    ///     for a type that is not supported.
    /// </summary>
    public int NodeCount => Diagram?.NodeCount ?? 0;

    /// <summary>
    ///     Gets the number of edges (links; the messages of a sequence diagram) of the diagram; zero for a type that is not
    ///     supported.
    /// </summary>
    public int EdgeCount => Diagram?.EdgeCount ?? 0;

    /// <summary>
    ///     Gets the number of subgraphs (the boxes of a sequence diagram) of the diagram; zero for a type that is not
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
    internal IMermaidDiagram? Diagram { get; }

    /// <summary>
    ///     Parses Mermaid text.
    /// </summary>
    /// <remarks>
    ///     Parsing is deterministic and does not depend on the culture or the current directory.
    /// </remarks>
    /// <param name="text">The diagram text. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is not a Mermaid diagram, its frontmatter is not valid, or a supported diagram does not follow its grammar.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MermaidLimits.MaxTextSize"/> allows.</exception>
    public static MermaidDocument Parse(string text, MermaidOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var effective = options ?? new MermaidOpenOptions();
        return Create(text, effective.SourceName, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Opens and parses a Mermaid file.
    /// </summary>
    /// <remarks>
    ///     The file is read as strict UTF-8 (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced), and
    ///     reading stops as soon as the size limit is certainly exceeded. Unless overridden in <paramref name="options"/>,
    ///     the source name is <paramref name="path"/> exactly as supplied.
    /// </remarks>
    /// <param name="path">The path of the file. Must not be null, empty or whitespace.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the file is not valid UTF-8 or not a valid diagram.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MermaidLimits.MaxTextSize"/> allows.</exception>
    public static MermaidDocument Open(string path, MermaidOpenOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var effective = options ?? new MermaidOpenOptions();
        using var stream = File.OpenRead(path);
        var text = ReadBounded(stream, effective.EffectiveLimits, effective.SourceName ?? path);
        return Create(text, effective.SourceName ?? path, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Parses Mermaid text read from a stream.
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
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the bytes are not valid UTF-8 or not a valid diagram.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MermaidLimits.MaxTextSize"/> allows.</exception>
    public static MermaidDocument Open(Stream stream, MermaidOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effective = options ?? new MermaidOpenOptions();
        var text = ReadBounded(stream, effective.EffectiveLimits, effective.SourceName);
        return Create(text, effective.SourceName, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Preprocesses, detects and parses a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The document.</returns>
    private static MermaidDocument Create(string text, string? sourceName, MermaidLimits limits)
    {
        // The pipeline of Mermaid's parse: preprocess, detect with the configuration of the text (the frontmatter and the
        // directives choose the flowchart, class and state renderers that the detectors read), then parse a supported type
        var source = Preprocessor.Run(text, sourceName, limits);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var textConfig = MermaidConfigResolver.Resolve(source, diagramId: null, options: null, sink: null);
        var settings = textConfig.DetectorSettings;
        if (settings != DetectorSettings.Default)
        {
            detected = DiagramDetector.Detect(source.Code, settings);
        }

        var diagram = detected.IsSupported ? DiagramRegistry.Parse(detected, source, limits, textConfig) : null;
        return new MermaidDocument(source, detected, diagram, sourceName);
    }

    /// <summary>
    ///     Reads a whole stream as strict UTF-8, stopping with an error once the size limit is certainly exceeded.
    /// </summary>
    /// <param name="stream">The stream; it is left open.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name, for the error location.</param>
    /// <returns>The text without a byte-order mark.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than the limit allows.</exception>
    /// <exception cref="MermaidSyntaxException">Thrown when the bytes are not valid UTF-8.</exception>
    private static string ReadBounded(Stream stream, MermaidLimits limits, string? sourceName)
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
                    $"The Mermaid text is longer than {Preprocessor.SourceLengthFactor} times the limit of {limits.MaxTextSize} characters (MermaidLimits.MaxTextSize)."));
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
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                "the text is not valid UTF-8",
                new MermaidLocation(sourceName, 0, 0),
                "save the file with UTF-8 encoding",
                null);
        }
    }
}
