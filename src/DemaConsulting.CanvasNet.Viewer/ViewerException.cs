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

    /// <summary>A resource budget (such as the triangle budget) was exceeded.</summary>
    public const string BudgetExceeded = "budget-exceeded";

    /// <summary>The two images to compare have different sizes and no size normalization was requested.</summary>
    public const string SizeMismatch = "size-mismatch";

    /// <summary>A comparison completed but at least one requested quality threshold was not met.</summary>
    public const string ThresholdFailed = "threshold-failed";
}

/// <summary>
///     An expected, user-correctable failure of an imaging operation, carrying a stable error code.
/// </summary>
/// <remarks>
///     <see cref="ViewerException"/> derives from <see cref="InvalidOperationException"/> so that a caller can handle
///     every expected failure (a missing or corrupt input, an unsupported feature, a size limit, an output that cannot
///     be written, and so on) with one <c>catch</c> and still branch on <see cref="Code"/>, one of the
///     <see cref="ErrorCodes"/> constants. The command-line tool derives from it to add what only a command line
///     needs. The class is not sealed for that reason.
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
}
