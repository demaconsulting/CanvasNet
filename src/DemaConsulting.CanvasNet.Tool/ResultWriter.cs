// cspell:ignore canvasnet
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     Writes the tool's machine-readable JSON results: one deterministic document per invocation.
/// </summary>
/// <remarks>
///     <para>
///         Every document is a single JSON object on a single line. It starts with the keys
///         <c>schemaVersion</c>, <c>ok</c>, and <c>command</c>, in that order, followed by the fields the
///         command writes (success) or an <c>error</c> object (failure). The output is deterministic: keys
///         are written in the order the code writes them, numbers are formatted with the invariant culture
///         (the JSON writer never consults the current culture), and every non-ASCII character is escaped as
///         <c>\uXXXX</c>, so the same result is the same bytes on every machine and console code page.
///     </para>
///     <para>
///         The schema is versioned by <see cref="SchemaVersion"/>. Adding a key is not a breaking change;
///         removing or renaming a key, or changing its meaning, increments the version.
///     </para>
/// </remarks>
internal static class ResultWriter
{
    /// <summary>
    ///     The version of the JSON result schema, written as <c>schemaVersion</c> in every document.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    ///     Writer options: compact single-line output, readable (but still valid) escaping of the HTML-sensitive
    ///     ASCII characters; non-ASCII characters are escaped afterwards by <see cref="EscapeNonAscii"/>.
    /// </summary>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Creates the success document of a command.
    /// </summary>
    /// <param name="command">The canonical name of the command that succeeded.</param>
    /// <param name="writeFields">
    ///     Writes the command's own fields into the document's top-level object; it must write complete
    ///     properties (a name followed by a value) only.
    /// </param>
    /// <returns>The document, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static string Success(string command, Action<Utf8JsonWriter> writeFields)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(writeFields);

        return Write(writer =>
        {
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteBoolean("ok", true);
            writer.WriteString("command", command);
            writeFields(writer);
        });
    }

    /// <summary>
    ///     Creates the failure document of an expected failure.
    /// </summary>
    /// <param name="command">
    ///     The canonical name of the command that failed, or <see langword="null"/> when the failure happened
    ///     before a command was selected (written as JSON <c>null</c>).
    /// </param>
    /// <param name="code">The stable error code; one of the <see cref="ErrorCodes"/> constants.</param>
    /// <param name="message">The concise error message.</param>
    /// <param name="hint">The optional suggestion for correcting the failure; omitted from the document when <see langword="null"/>.</param>
    /// <returns>The document, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     The document has no exit-code field: every failure exits with code 1.
    /// </remarks>
    public static string Failure(string? command, string code, string message, string? hint)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);

        return Write(writer =>
        {
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteBoolean("ok", false);
            writer.WriteString("command", command);
            writer.WriteStartObject("error");
            writer.WriteString("code", code);
            writer.WriteString("message", message);
            if (hint != null)
            {
                writer.WriteString("hint", hint);
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>
    ///     Creates the failure document of an expected exception.
    /// </summary>
    /// <param name="command">The canonical name of the command that failed, or <see langword="null"/> when unknown.</param>
    /// <param name="ex">The expected exception.</param>
    /// <returns>The document, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ex"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     A <see cref="ToolException"/> contributes its own code and hint; an <see cref="ArgumentException"/>
    ///     is reported as <c>usage-error</c>; any other exception as <c>operation-failed</c>.
    /// </remarks>
    public static string Failure(string? command, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex switch
        {
            ToolException toolException => Failure(command, toolException.Code, toolException.Message, toolException.Hint),
            ArgumentException => Failure(command, ErrorCodes.UsageError, ex.Message, null),
            _ => Failure(command, ErrorCodes.OperationFailed, ex.Message, null)
        };
    }

    /// <summary>
    ///     Writes a top-level JSON object using the given body and returns it as ASCII-only text.
    /// </summary>
    /// <param name="writeBody">Writes the properties of the object.</param>
    /// <returns>The document text.</returns>
    private static string Write(Action<Utf8JsonWriter> writeBody)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }

        return EscapeNonAscii(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    /// <summary>
    ///     Escapes every non-ASCII UTF-16 code unit of a JSON text as <c>\uXXXX</c>.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The equivalent JSON text containing only ASCII characters.</returns>
    /// <remarks>
    ///     Non-ASCII characters can only occur inside JSON strings, where the escape is always valid; a
    ///     surrogate pair becomes two escapes, which JSON parsers combine again.
    /// </remarks>
    private static string EscapeNonAscii(string json)
    {
        var builder = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c < 0x80)
            {
                builder.Append(c);
            }
            else
            {
                builder.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
