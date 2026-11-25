using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     Stable, machine-readable error codes reported by the imaging operations and the <c>canvasnet</c> tool.
/// </summary>
/// <remarks>
///     The code strings are part of the documented contract of the library and of the command-line tool built on it:
///     scripts and agents may branch on them, so a value is never changed once released. New codes may be added.
/// </remarks>
public static class ErrorCodes
{
    /// <summary>The command line is malformed: unknown command or option, missing or invalid value.</summary>
    public const string UsageError = "usage-error";

    /// <summary>An expected operation failure that has no more specific code.</summary>
    public const string OperationFailed = "operation-failed";

    /// <summary>The input file does not exist.</summary>
    public const string InputNotFound = "input-not-found";

    /// <summary>The input file exists but cannot be read.</summary>
    public const string InputUnreadable = "input-unreadable";

    /// <summary>The input file has a format the tool does not support.</summary>
    public const string UnsupportedFormat = "unsupported-format";

    /// <summary>The input file is corrupt or truncated.</summary>
    public const string CorruptInput = "corrupt-input";

    /// <summary>The input uses a feature that the underlying library does not support.</summary>
    public const string UnsupportedFeature = "unsupported-feature";

    /// <summary>The requested page does not exist in the input document.</summary>
    public const string PageOutOfRange = "page-out-of-range";

    /// <summary>
    ///     The fragment of an input (<c>part.scad#def.width=20</c>) cannot be used: it is too long or has too many parameters,
    ///     or the value of a parameter is not valid. The same code names the same mistake in the link of an SVG image.
    /// </summary>
    public const string ImageFragmentInvalid = "image-fragment-invalid";

    /// <summary>The requested or resulting image is larger than the supported maximum size.</summary>
    public const string SizeLimit = "size-limit";

    /// <summary>The output path is the same file as the input path.</summary>
    public const string SameInputOutput = "same-input-output";

    /// <summary>The output file exists and overwriting was disabled.</summary>
    public const string OutputExists = "output-exists";

    /// <summary>The output file could not be written.</summary>
    public const string OutputWriteFailed = "output-write-failed";

    /// <summary>An OpenSCAD model has a syntax or evaluation error.</summary>
    public const string ScadError = "scad-error";

    /// <summary>A Vega-Lite chart has a specification error or cannot be rendered.</summary>
    public const string VegaLiteError = "vega-lite-error";

    /// <summary>A Mermaid diagram has a syntax error or cannot be laid out or drawn.</summary>
    public const string MermaidError = "mermaid-error";

    /// <summary>A PlantUML diagram has a syntax error, an include that cannot be read, or cannot be laid out or drawn.</summary>
    public const string PlantUmlError = "plantuml-error";

    /// <summary>A MathML equation has a syntax error or cannot be laid out or drawn.</summary>
    public const string MathMlError = "mathml-error";

    /// <summary>A resource budget (such as the triangle budget) was exceeded.</summary>
    public const string BudgetExceeded = "budget-exceeded";

    /// <summary>
    ///     A file the rendered document references (a picture with the strict option, a model's include, a chart's
    ///     data) was denied by the file-access policy of the caller, or the policy could not be applied.
    /// </summary>
    public const string FileAccessDenied = "file-access-denied";

    /// <summary>The two images to compare have different sizes and no size normalization was requested.</summary>
    public const string SizeMismatch = "size-mismatch";

    /// <summary>A comparison completed but at least one requested quality threshold was not met.</summary>
    public const string ThresholdFailed = "threshold-failed";

    /// <summary>
    ///     A position selector matched no feature of the diagram or chart and the strict option was given; without it this
    ///     is the warning <c>positions-unmatched</c>.
    /// </summary>
    public const string PositionsUnmatched = "positions-unmatched";

    /// <summary>
    ///     An SVG position binding (<c>cn:x1y1</c>, <c>cn:x2y2</c>, <c>cn:center</c>, <c>cn:xy</c>) was not resolved and the
    ///     strict option was given; without it this is the warning with the binding's own <c>svg-binding-*</c> code and
    ///     the numbers written in the file are kept.
    /// </summary>
    public const string SvgBindingFailed = "svg-binding-failed";

    /// <summary>
    ///     The <c>pandoc-filter</c> command of the tool cannot draw the pictures of a Pandoc document: a picture
    ///     attribute is not valid, a source is not found, a dependency limit is exceeded, an environment variable of the
    ///     filter is not valid, or a file of the picture cache cannot be written. The message names the picture.
    /// </summary>
    public const string PandocFilterError = "pandoc-filter-error";
}

/// <summary>
///     An expected, user-correctable failure of an imaging operation, carrying a stable error code.
/// </summary>
/// <remarks>
///     <see cref="ViewerException"/> derives from <see cref="InvalidOperationException"/> so that a caller can handle
///     every expected failure (a missing or corrupt input, an unsupported feature, a size limit, an output that cannot
///     be written, and so on) with one <c>catch</c> and still branch on <see cref="Code"/>, one of the
///     <see cref="ErrorCodes"/> constants. The command-line tool derives from it to add what only a command line
///     needs. The class is not sealed for that reason. <see cref="ViewerException"/> and <see cref="ErrorCodes"/> are in the
///     <c>DemaConsulting.CanvasNet.Viewer</c> namespace, while the operations that throw them are in
///     <c>DemaConsulting.CanvasNet.Viewer.Imaging</c>: an example that catches one needs both <c>using</c> directives.
///     A failure of the source of a file (a syntax error of a diagram, a model or a chart, malformed SVG markup, a
///     reference the file-access policy denied) also says where it is (<see cref="Location"/>) and carries the code of
///     the format library (<see cref="DetailCode"/>), so a caller can point at the line without reading the message; the
///     original exception of the library is the <see cref="Exception.InnerException"/>. The failures of
///     <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/> never name an option of the command-line tool in
///     their message or hint. The exception is immutable once thrown and safe to read from any thread.
/// </remarks>
public class ViewerException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ViewerException"/> class.
    /// </summary>
    /// <param name="code">The stable error code; one of the <see cref="ErrorCodes"/> constants.</param>
    /// <param name="message">A concise, human-readable description of the failure.</param>
    /// <param name="hint">An optional suggestion for correcting the failure.</param>
    /// <param name="inner">The optional exception that caused this failure.</param>
    public ViewerException(string code, string message, string? hint = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Hint = hint;
    }

    /// <summary>
    ///     Gets the stable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    ///     Gets the optional suggestion for correcting the failure, or <see langword="null"/>.
    /// </summary>
    public string? Hint { get; }

    /// <summary>
    ///     Gets where in the source the failure is, or <see langword="null"/> when it is not known.
    /// </summary>
    /// <remarks>
    ///     Set for a failure of the source of a file: a syntax or evaluation error of an OpenSCAD model, an error of a
    ///     Vega-Lite chart (with the JSON pointer of the property when the library knows it), an error of a Mermaid or a
    ///     PlantUML diagram or of a MathML equation, an unsupported feature the library located, and malformed XML of an
    ///     SVG file. The file is the
    ///     name the library reports (the file name for a stream), and the line and column count from 1; any part the
    ///     library does not know is <see langword="null"/>. A failure that has no place in a source (a missing file, a
    ///     size or pixel limit) has no location.
    /// </remarks>
    public ImageDiagnosticLocation? Location { get; init; }

    /// <summary>
    ///     Gets the code the format library gave the failure, or <see langword="null"/> when it gave none.
    /// </summary>
    /// <remarks>
    ///     <see cref="Code"/> is the stable code that is the same for every format (<c>mermaid-error</c>); this is the
    ///     finer code of the library: for example <c>mermaid-syntax-error</c>, <c>plantuml-syntax-error</c>,
    ///     <c>mathml-syntax-error</c>, <c>scad-syntax-error</c>, or the name of the feature of an <c>unsupported-feature</c> failure (a <c>vl-*</c>
    ///     identifier for a chart). A Vega-Lite specification or render error, malformed SVG markup, and the failures
    ///     that do not come from a format library have none.
    /// </remarks>
    public string? DetailCode { get; init; }
}
