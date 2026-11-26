using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.HelpCards.Integration;
using DemaConsulting.HelpCards.Presentation;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The rendering helpers of the help cards: the render options of the command line, the JSON envelope and error body
///     of the tool's contract, and the <c>topics</c> document.
/// </summary>
/// <remarks>
///     DemaConsulting.HelpCards renders the cards. Text has LF line endings, no ANSI escape sequences, and generated lines
///     wrapped at 78 columns, so the same text is produced on every machine; every in-text link reads as the
///     <c>canvasnet help</c> command that prints its target. The JSON documents are wrapped in the envelope of
///     <see cref="ResultWriter"/> (<c>schemaVersion</c>, <c>ok</c>, <c>command</c>, then <c>kind</c>), so they have its
///     key order and ASCII-only escaping, and HelpCards writes the fields of each kind after them.
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The help subject that lists every help card.
    /// </summary>
    internal const string TopicsSubject = "topics";

    /// <summary>
    ///     The name of the command whose documents the envelope writes.
    /// </summary>
    private const string HelpCommand = "help";

    /// <summary>
    ///     The kind of a JSON error document, which has no <c>kind</c> key in the tool's contract.
    /// </summary>
    private const string ErrorKind = "error";

    /// <summary>
    ///     The sentence that starts the text of the <c>topics</c> subject.
    /// </summary>
    private const string TopicsIntro =
        "Help cards, depth first. Read one with 'canvasnet help <path>', for example\n'canvasnet help mermaid gantt'.";

    /// <summary>
    ///     Creates the render options of the command line for a format.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <returns>Options with links written as <c>canvasnet help</c> commands and the tool's JSON envelope.</returns>
    public static HelpRenderOptions RenderOptions(HelpFormat format)
    {
        return RenderOptions(format, kind => kind);
    }

    /// <summary>
    ///     Creates the render options of the command line for a format, with the kind of the JSON documents renamed.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <param name="kindOf">Gives the kind to write for the kind of HelpCards.</param>
    /// <returns>Options with links written as <c>canvasnet help</c> commands and the tool's JSON envelope.</returns>
    private static HelpRenderOptions RenderOptions(HelpFormat format, Func<string, string> kindOf)
    {
        return new HelpRenderOptions
        {
            Format = format,
            LinkStyle = HelpLinkStyle.Command,
            CommandTemplate = CommandTemplate,
            JsonEnvelope = (kind, fields) => Envelope(kindOf(kind), fields, null)
        };
    }

    /// <summary>
    ///     Writes a help document in the tool's envelope: <c>schemaVersion</c>, <c>ok</c>, <c>command</c> (<c>help</c>),
    ///     then <c>kind</c> (left out for an error, as in every failure document of the tool), the tool's own extra
    ///     fields, and the fields that HelpCards writes for the kind.
    /// </summary>
    /// <param name="kind">
    ///     The kind of the document (<c>index</c>, <c>card</c>, <c>examples</c>, <c>example</c>, <c>search</c>,
    ///     <c>cards</c>, <c>topics</c>, or <c>error</c>). Must not be null.
    /// </param>
    /// <param name="fields">Writes the fields of the kind into the open object. Must not be null.</param>
    /// <param name="extra">Writes the tool's own fields before them, or <see langword="null"/> for none.</param>
    /// <returns>The document, on one line, ASCII only.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kind"/> or <paramref name="fields"/> is null.</exception>
    /// <remarks>Stateless and safe to call from several threads.</remarks>
    public static string Envelope(string kind, Action<Utf8JsonWriter> fields, Action<Utf8JsonWriter>? extra)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(fields);

        var ok = kind != ErrorKind;
        return ResultWriter.Envelope(HelpCommand, ok, writer =>
        {
            if (ok)
            {
                writer.WriteString("kind", kind);
            }

            extra?.Invoke(writer);
            fields(writer);
        });
    }

    /// <summary>
    ///     Writes the body of a help error in the tool's shape: <c>error</c> with <c>code</c>, <c>message</c>, and
    ///     <c>hint</c>.
    /// </summary>
    /// <param name="writer">The writer, positioned inside the open document object. Must not be null.</param>
    /// <param name="error">The error of HelpCards. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <remarks>
    ///     The codes of HelpCards are mapped to the tool's (see <see cref="ErrorCode"/>); the candidates of a
    ///     <c>not-found</c> or <c>ambiguous</c> error are left out, because the hint already names them.
    /// </remarks>
    public static void WriteError(Utf8JsonWriter writer, HelpCliError error)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(error);

        writer.WriteStartObject("error");
        writer.WriteString("code", ErrorCode(error.Code));
        writer.WriteString("message", error.Message);
        if (error.Hint != null)
        {
            writer.WriteString("hint", error.Hint);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    ///     Maps an error code of HelpCards to the tool's stable error code.
    /// </summary>
    /// <param name="helpCode">The code of HelpCards: <c>usage-error</c>, <c>not-found</c>, <c>ambiguous</c>, or <c>catalog-error</c>.</param>
    /// <returns>
    ///     <c>operation-failed</c> for a defective catalog (which a tested build never has), otherwise <c>usage-error</c>:
    ///     an unknown card, example, tag, or source, and a wrong option, are all mistakes on the command line.
    /// </returns>
    public static string ErrorCode(string helpCode)
    {
        return helpCode == "catalog-error" ? ErrorCodes.OperationFailed : ErrorCodes.UsageError;
    }

    /// <summary>
    ///     Renders the whole tree of help cards: the <c>topics</c> subject.
    /// </summary>
    /// <param name="json">Whether to render the JSON document (<c>kind</c> <c>topics</c>) instead of text.</param>
    /// <returns>
    ///     The text (an intro line, then one line per card, depth first, with the command that prints it and its
    ///     summary) or the document (the index document of HelpCards over the whole tree, with the kind <c>topics</c>),
    ///     without a trailing newline.
    /// </returns>
    public static string RenderTopics(bool json)
    {
        if (json)
        {
            return Catalog.RenderIndex(RenderOptions(HelpFormat.Json, _ => TopicsSubject), int.MaxValue);
        }

        return TopicsIntro + "\n\n" + Catalog.RenderIndex(RenderOptions(HelpFormat.Text), int.MaxValue);
    }

    /// <summary>
    ///     Writes a JSON array of strings.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="values">The strings.</param>
    internal static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
