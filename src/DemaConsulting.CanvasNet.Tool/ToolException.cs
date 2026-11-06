using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     An expected, user-correctable failure of the command-line tool: a <see cref="ViewerException"/> that also
///     knows which command it came from and can carry the fields of a result that was produced before it failed.
/// </summary>
/// <remarks>
///     <para>
///         The code and the hint are those of <see cref="ViewerException"/>: the stable <see cref="ErrorCodes"/> of the
///         Viewer library. This derived type adds only what a command line needs, so the library stays free of it.
///     </para>
///     <para>
///         <see cref="Program.Main"/> and <see cref="ResultWriter"/> handle every <see cref="ViewerException"/> (the
///         tool's own and the library's) on the expected-error path: a concise message on standard error and exit code
///         1, with no stack trace. The command registry turns a library exception into a
///         <see cref="ToolException"/> that names the command.
///     </para>
/// </remarks>
internal sealed class ToolException : ViewerException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolException"/> class.
    /// </summary>
    /// <param name="code">The stable error code; one of the <see cref="ErrorCodes"/> constants.</param>
    /// <param name="message">A concise, human-readable description of the failure.</param>
    /// <param name="hint">An optional suggestion for correcting the failure.</param>
    /// <param name="inner">The optional exception that caused this failure.</param>
    public ToolException(string code, string message, string? hint = null, Exception? inner = null)
        : base(code, message, hint, inner)
    {
    }

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
