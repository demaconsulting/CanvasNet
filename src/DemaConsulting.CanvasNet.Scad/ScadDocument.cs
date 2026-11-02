using System.Text;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A parsed OpenSCAD-language source file: the validated result of reading source text from a string,
///     a stream or a file.
/// </summary>
/// <remarks>
///     <para>
///         A document proves that the source is syntactically valid and reports what it declares at file scope
///         (<see cref="Directives"/>, <see cref="ModuleNames"/>, <see cref="FunctionNames"/>). Parsing never
///         evaluates the program and never touches any other file, so <c>include</c>/<c>use</c> paths are recorded
///         but not resolved. <see cref="Evaluate(ScadEvaluationOptions?, CancellationToken)"/> runs the program
///         (resolving those directives under the sandbox rules of <see cref="ScadOpenOptions"/>) and reports
///         its output. The syntax tree itself is internal so it can evolve while rendering is added.
///     </para>
///     <para>
///         Parsing is fail-fast: the first lexical or grammatical error throws a
///         <see cref="ScadSyntaxException"/> with its file, line and column. Hostile input fails closed:
///         nesting deeper than 256 levels is a <see cref="ScadSyntaxException"/> and source longer than
///         4,194,304 UTF-16 characters is an <see cref="InvalidDataException"/>; neither can overflow the stack.
///     </para>
///     <para>
///         A document is immutable and safe for concurrent use. It holds no unmanaged resources, so it is not
///         disposable, and a stream passed to <see cref="Open(Stream, ScadOpenOptions?)"/> is left open.
///     </para>
/// </remarks>
public sealed partial class ScadDocument
{
    /// <summary>The number of characters read from a stream per call.</summary>
    private const int ReadChunkSize = 8192;

    /// <summary>
    ///     Initializes a document from its parsed program and the information derived from it.
    /// </summary>
    /// <param name="program">The syntax tree.</param>
    /// <param name="options">The effective options (base directory and source name resolved).</param>
    private ScadDocument(ScadProgram program, ScadOpenOptions options)
    {
        Program = program;
        Options = options;
        SourceName = options.SourceName;

        // File-scope facts are extracted once so that the public lists are cheap and immutable
        Directives = program.Statements
            .OfType<ScadDirectiveStatement>()
            .Select(d => new ScadDirective(d.Kind, d.Path, d.Location))
            .ToList()
            .AsReadOnly();
        ModuleNames = program.Statements.OfType<ScadModuleDefinition>().Select(m => m.Name).ToList().AsReadOnly();
        FunctionNames = program.Statements.OfType<ScadFunctionDefinition>().Select(f => f.Name).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the source name used in locations and messages, or <see langword="null"/> when the document is unnamed.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    ///     Gets the file-scope <c>include</c> and <c>use</c> directives in source order. Parsing only records them;
    ///     no referenced file is opened until the document is evaluated.
    /// </summary>
    public IReadOnlyList<ScadDirective> Directives { get; }

    /// <summary>
    ///     Gets the names of the file-scope <c>module</c> definitions in source order; duplicates are kept and
    ///     modules nested inside other definitions are not listed.
    /// </summary>
    public IReadOnlyList<string> ModuleNames { get; }

    /// <summary>
    ///     Gets the names of the file-scope <c>function</c> definitions in source order; duplicates are kept and
    ///     functions nested inside modules are not listed.
    /// </summary>
    public IReadOnlyList<string> FunctionNames { get; }

    /// <summary>
    ///     Gets the syntax tree (internal; consumed by the evaluator).
    /// </summary>
    internal ScadProgram Program { get; }

    /// <summary>
    ///     Gets the effective options, with the base directory and source name resolved (internal; consumed by the evaluator).
    /// </summary>
    internal ScadOpenOptions Options { get; }

    /// <summary>
    ///     Parses OpenSCAD-language source text.
    /// </summary>
    /// <param name="source">The source text. Empty, whitespace-only and comment-only text is a valid empty document; a leading U+FEFF is ignored.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ScadSyntaxException">Thrown at the first syntax error, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="source"/> is longer than 4,194,304 characters.</exception>
    public static ScadDocument Parse(string source, ScadOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effective = options ?? new ScadOpenOptions();
        return Create(source, effective);
    }

    /// <summary>
    ///     Opens and parses a source file.
    /// </summary>
    /// <remarks>
    ///     The file is read as UTF-8 (a byte-order mark is detected and removed). Unless overridden, the source
    ///     name is <paramref name="path"/> exactly as supplied and the base directory is the folder of the full path.
    /// </remarks>
    /// <param name="path">The path of the file. Must not be null, empty or whitespace.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when a directory of the path does not exist.</exception>
    /// <exception cref="ScadSyntaxException">Thrown at the first syntax error, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file contains more than 4,194,304 characters.</exception>
    public static ScadDocument Open(string path, ScadOpenOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Apply the documented defaults: the path names the source and its folder is the base directory
        var effective = (options ?? new ScadOpenOptions()).WithEffective(
            options?.BaseDirectory ?? Path.GetDirectoryName(Path.GetFullPath(path)),
            options?.SourceName ?? path);

        using var stream = File.OpenRead(path);
        return Create(ReadBounded(stream), effective);
    }

    /// <summary>
    ///     Parses OpenSCAD-language source read from a stream.
    /// </summary>
    /// <remarks>
    ///     The stream is read from its current position to its end as UTF-8 (a byte-order mark is detected and
    ///     removed) and is left open. Reading stops as soon as the size limit is exceeded, so a stream that never
    ///     ends cannot consume unbounded memory.
    /// </remarks>
    /// <param name="stream">A readable stream. Must not be null.</param>
    /// <param name="options">Optional open options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="ScadSyntaxException">Thrown at the first syntax error, or when nesting is too deep.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream contains more than 4,194,304 characters.</exception>
    public static ScadDocument Open(Stream stream, ScadOpenOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        var effective = options ?? new ScadOpenOptions();
        return Create(ReadBounded(stream), effective);
    }

    /// <summary>
    ///     Parses source text and wraps the result in a document.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="effective">The effective options.</param>
    /// <returns>The document.</returns>
    private static ScadDocument Create(string source, ScadOpenOptions effective) =>
        new(ScadParser.Parse(source, effective.SourceName), effective);

    /// <summary>
    ///     Decodes the bytes of an included file as text with the same bounded, UTF-8 rules as a stream.
    /// </summary>
    /// <remarks>
    ///     Internal so that the file resolver decodes included files with exactly the same rules as
    ///     <see cref="ReadBounded(Stream)"/>: UTF-8 with a detected byte-order mark, and the character limit enforced.
    /// </remarks>
    /// <param name="bytes">The bytes of the file.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the maximum source length.</exception>
    internal static string DecodeBounded(ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        return ReadBounded(stream);
    }

    /// <summary>
    ///     Reads a whole stream as text, stopping with an error once the size limit is exceeded.
    /// </summary>
    /// <remarks>
    ///     Internal so that stream, file and included-file reads share exactly the same bounded, UTF-8 rules.
    /// </remarks>
    /// <param name="stream">The stream to read; it is left open.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the maximum source length.</exception>
    internal static string ReadBounded(Stream stream)
    {
        // Read in chunks and check the running length so an endless stream stops one chunk past the limit
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: ReadChunkSize, leaveOpen: true);
        var text = new StringBuilder();
        var buffer = new char[ReadChunkSize];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > ScadParser.MaxSourceLength)
            {
                throw ScadParser.CreateSourceTooLongException();
            }
        }

        return text.ToString();
    }
}
