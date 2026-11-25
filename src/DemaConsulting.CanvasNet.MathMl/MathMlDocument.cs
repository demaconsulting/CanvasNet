using System.Text;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     A parsed MathML equation: the validated result of reading MathML text from a string, a stream or a file, ready to
///     be measured and rendered.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading.</b> The text is one <c>math</c> element of MathML (in the MathML namespace, or in no namespace as
///         HTML writes it). Named character references of the full W3C HTML+MathML set (<c>&amp;alpha;</c>,
///         <c>&amp;InvisibleTimes;</c>) and numeric references are decoded without a document type. Reading is fail-fast
///         and fail-closed: text that is not valid UTF-8, not well-formed XML, declares a document type, uses an unknown
///         name, or has another root throws a <see cref="MathMlSyntaxException"/> with the line and column in the original
///         text, a stable code and a hint; text longer than <see cref="MathMlLimits.MaxTextSize"/> allows is an
///         <see cref="InvalidDataException"/>. Reading never uses the network and never reads another file: links are
///         reported, not followed.
///     </para>
///     <para>
///         <b>Elements.</b> This version draws the token elements (<c>mi</c>, <c>mn</c>, <c>mo</c>, <c>mtext</c>,
///         <c>ms</c>, <c>mspace</c>), <c>mrow</c> and the grouping elements (<c>mstyle</c>, <c>mpadded</c>,
///         <c>mphantom</c>, <c>merror</c>, <c>semantics</c>, <c>maction</c>), with the automatic italic of a
///         single-character <c>mi</c> and every <c>mathvariant</c>. Every other element is laid out as a row of its
///         children, as MathML Core lays out an unknown element, and reported as
///         <see cref="MathMlDiagnosticCodes.ElementUnsupported"/>.
///     </para>
///     <para>
///         <b>Thread safety.</b> A document is immutable and safe for concurrent use: it can be measured and rendered from
///         several threads at once and every call returns a new surface. It holds no unmanaged resources, so it is not
///         disposable, and a stream passed to <see cref="Open(Stream, MathMlOpenOptions?)"/> is left open.
///     </para>
/// </remarks>
public sealed partial class MathMlDocument
{
    /// <summary>The number of bytes read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>The length of the UTF-8 byte-order mark.</summary>
    private const int MaxPreambleLength = 3;

    /// <summary>The strict UTF-8 decoder: invalid bytes are an error, never U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Initializes a document from the result of reading.
    /// </summary>
    /// <param name="read">The result of reading.</param>
    /// <param name="sourceName">The source name.</param>
    private MathMlDocument(MathReadResult read, string? sourceName)
    {
        Root = read.Root;
        Reports = read.Reports;
        ElementCount = read.ElementCount;
        Annotations = read.Annotations;
        SourceName = sourceName;
        StyleResolver.TryParseDisplay(read.Root.GetAttribute("display"), out var display);
        Display = display;
        AltText = read.Root.GetAttribute("alttext");
    }

    /// <summary>
    ///     Gets the source name used in locations and messages, or <see langword="null"/> when the document is unnamed.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the display mode of the equation: the <c>display</c> attribute of the <c>math</c> element
    ///     (<see cref="MathMlDisplay.Inline"/> when it is absent or invalid).
    /// </summary>
    public MathMlDisplay Display { get; }

    /// <summary>
    ///     Gets the number of MathML elements of the equation, the <c>math</c> element included (the content of
    ///     annotations and skipped elements of other namespaces excluded).
    /// </summary>
    public int ElementCount { get; }

    /// <summary>
    ///     Gets the text alternative of the equation (the <c>alttext</c> attribute of the <c>math</c> element), or
    ///     <see langword="null"/> when it has none.
    /// </summary>
    public string? AltText { get; }

    /// <summary>
    ///     Gets the annotations of the equation (<c>annotation</c> and <c>annotation-xml</c>, such as its TeX source), in
    ///     document order; empty when it has none. They are never drawn.
    /// </summary>
    public IReadOnlyList<MathMlAnnotation> Annotations { get; }

    /// <summary>
    ///     Gets the equation tree (internal; laid out by every measure and render).
    /// </summary>
    internal MathElement Root { get; }

    /// <summary>
    ///     Gets the reports found while reading, replayed into every measure and render call (internal).
    /// </summary>
    internal IReadOnlyList<MathMlPendingReport> Reports { get; }

    /// <summary>
    ///     Parses MathML text.
    /// </summary>
    /// <remarks>
    ///     Parsing is deterministic and does not depend on the culture or the current directory.
    /// </remarks>
    /// <param name="text">The MathML text. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="MathMlSyntaxException">Thrown, located, when the text is not well-formed MathML, uses an unknown named reference, or a structural limit is exceeded.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MathMlLimits.MaxTextSize"/> allows.</exception>
    public static MathMlDocument Parse(string text, MathMlOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var effective = options ?? new MathMlOpenOptions();
        return Create(text, effective.SourceName, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Opens and parses a MathML file.
    /// </summary>
    /// <remarks>
    ///     The file is read as strict UTF-8 (a byte-order mark is removed; invalid UTF-8 is rejected, never replaced), and
    ///     reading stops as soon as the size limit is certainly exceeded. Unless overridden in <paramref name="options"/>,
    ///     the source name is <paramref name="path"/> exactly as supplied. No other file is read.
    /// </remarks>
    /// <param name="path">The path of the file. Must not be null, empty or whitespace.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="MathMlSyntaxException">Thrown, located, when the file is not valid UTF-8 or not well-formed MathML.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MathMlLimits.MaxTextSize"/> allows.</exception>
    public static MathMlDocument Open(string path, MathMlOpenOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var effective = options ?? new MathMlOpenOptions();
        var sourceName = effective.SourceName ?? path;
        using var stream = File.OpenRead(path);
        var text = ReadBounded(stream, effective.EffectiveLimits, sourceName);
        return Create(text, sourceName, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Parses MathML text read from a stream.
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
    /// <exception cref="MathMlSyntaxException">Thrown, located, when the bytes are not valid UTF-8 or not well-formed MathML.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than <see cref="MathMlLimits.MaxTextSize"/> allows.</exception>
    public static MathMlDocument Open(Stream stream, MathMlOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effective = options ?? new MathMlOpenOptions();
        var text = ReadBounded(stream, effective.EffectiveLimits, effective.SourceName);
        return Create(text, effective.SourceName, effective.EffectiveLimits);
    }

    /// <summary>
    ///     Checks the size of a text and reads it into the equation tree.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the limit allows.</exception>
    private static MathMlDocument Create(string text, string? sourceName, MathMlLimits limits)
    {
        if (text.Length > limits.MaxTextSize)
        {
            throw TooLong(limits);
        }

        return new MathMlDocument(MathMlReader.Read(text, sourceName, limits), sourceName);
    }

    /// <summary>
    ///     Reads a whole stream as strict UTF-8, stopping with an error once the size limit is certainly exceeded.
    /// </summary>
    /// <param name="stream">The stream; it is left open.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name, for the error location.</param>
    /// <returns>The text without a byte-order mark.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than the limit allows.</exception>
    /// <exception cref="MathMlSyntaxException">Thrown when the bytes are not valid UTF-8.</exception>
    private static string ReadBounded(Stream stream, MathMlLimits limits, string? sourceName)
    {
        // A UTF-16 unit takes at most three bytes, so more bytes than that is over the limit whatever the content
        var maxBytes = (3L * limits.MaxTextSize) + MaxPreambleLength;
        using var bytes = new MemoryStream();
        var buffer = new byte[ReadChunkSize];
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            bytes.Write(buffer, 0, count);
            if (bytes.Length > maxBytes)
            {
                throw TooLong(limits);
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
        catch (DecoderFallbackException ex)
        {
            throw new MathMlSyntaxException(
                ex,
                MathMlDiagnosticCodes.SyntaxError,
                "the text is not valid UTF-8",
                new MathMlLocation(sourceName, 0, 0),
                "save the file with UTF-8 encoding");
        }
    }

    /// <summary>
    ///     Creates the exception of a text over the size limit.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <returns>The exception.</returns>
    private static InvalidDataException TooLong(MathMlLimits limits) =>
        new(string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"The MathML text is longer than the limit of {limits.MaxTextSize} characters (MathMlLimits.MaxTextSize)."));
}
