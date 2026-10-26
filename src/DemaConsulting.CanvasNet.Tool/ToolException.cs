namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     Stable, machine-readable error codes reported by the tool.
/// </summary>
/// <remarks>
///     The code strings are part of the tool's documented contract: scripts and agents may branch
///     on them, so a value is never changed once released. New codes may be added.
/// </remarks>
internal static class ErrorCodes
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

    /// <summary>A resource budget (such as the triangle budget) was exceeded.</summary>
    public const string BudgetExceeded = "budget-exceeded";

    /// <summary>The two images to compare have different sizes and no size normalization was requested.</summary>
    public const string SizeMismatch = "size-mismatch";

    /// <summary>A comparison completed but at least one requested quality threshold was not met.</summary>
    public const string ThresholdFailed = "threshold-failed";
}

/// <summary>
///     An expected, user-correctable failure of the tool, carrying a stable error code.
/// </summary>
/// <remarks>
///     <see cref="ToolException"/> derives from <see cref="InvalidOperationException"/> so that it is
///     handled by the expected-error path of <see cref="Program.Main"/> exactly like any other
///     expected failure: a concise message on standard error and exit code 1, with no stack trace.
///     The code, hint, and command name let callers (and the JSON error contract) give a more precise
///     report than the message alone.
/// </remarks>
internal sealed class ToolException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolException"/> class.
    /// </summary>
    /// <param name="code">The stable error code; one of the <see cref="ErrorCodes"/> constants.</param>
    /// <param name="message">A concise, human-readable description of the failure.</param>
    /// <param name="hint">An optional suggestion for correcting the failure.</param>
    /// <param name="inner">The optional exception that caused this failure.</param>
    public ToolException(string code, string message, string? hint = null, Exception? inner = null)
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
    ///     Gets or sets the name of the command that was running when the failure occurred, or
    ///     <see langword="null"/> when the failure happened before a command was selected.
    /// </summary>
    /// <remarks>
    ///     The command registry fills this in when the exception escapes a command, so individual
    ///     commands do not need to know their own name when throwing.
    /// </remarks>
    public string? Command { get; set; }

    /// <summary>
    ///     Gets the optional writer of additional top-level JSON fields to append to the failure document, or
    ///     <see langword="null"/> when the failure carries none.
    /// </summary>
    /// <remarks>
    ///     A command whose failure still produced a result (for example a comparison that completed but missed a
    ///     threshold) sets this so that the <c>ok: false</c> document also carries the result's fields. The writer
    ///     must write complete properties only; <see cref="ResultWriter.Failure(string?, Exception)"/> calls it after
    ///     the <c>error</c> object.
    /// </remarks>
    public Action<System.Text.Json.Utf8JsonWriter>? ResultFields { get; init; }
}
