// cspell:ignore canvasnet
using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The rendering of help topics: plain text and JSON.
/// </summary>
/// <remarks>
///     The text is deterministic and plain (no ANSI escape sequences, no console-width detection, line feeds
///     only, whatever the operating system): it is wrapped at
///     <see cref="TextWidth"/> columns, so the same text is produced on every machine. The JSON documents are written
///     through <see cref="ResultWriter"/>, so they have its envelope, key order, and ASCII-only escaping.
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The column at which topic text is wrapped.
    /// </summary>
    internal const int TextWidth = 78;

    /// <summary>
    ///     The width of the kind column of an example list: the length of <c>[command]</c>.
    /// </summary>
    private const int KindColumnWidth = 9;

    /// <summary>
    ///     Renders a topic as plain text.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The text, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="topic"/> is <see langword="null"/>.</exception>
    public static string RenderText(HelpTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        var builder = new StringBuilder();
        builder.AppendLine($"Topic: {topic.Title}");
        builder.AppendLine();
        AppendWrapped(builder, topic.Summary, string.Empty, string.Empty);
        if (topic.Aliases.Count > 0)
        {
            builder.AppendLine();
            AppendWrapped(builder, $"Also known as: {string.Join(", ", topic.Aliases)}", string.Empty, "    ");
        }

        foreach (var section in topic.Sections)
        {
            builder.AppendLine();
            builder.AppendLine($"{section.Title}:");
            foreach (var item in section.Items)
            {
                AppendItem(builder, item);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Examples:");
        AppendExampleLines(builder, topic);
        builder.AppendLine();
        builder.AppendLine($"Print one with: canvasnet help {topic.Name} --example <name>");
        builder.AppendLine("List them with --examples; add --json for machine-readable output.");
        return builder.ToString().TrimEnd().ReplaceLineEndings("\n");
    }

    /// <summary>
    ///     Renders the list of the examples of a topic as plain text.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>One line per example (name, kind, description), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="topic"/> is <see langword="null"/>.</exception>
    public static string RenderExampleList(HelpTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        var builder = new StringBuilder();
        AppendExampleLines(builder, topic);
        return builder.ToString().TrimEnd().ReplaceLineEndings("\n");
    }

    /// <summary>
    ///     Gets the exact content of one example of a topic.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The content: the source file or the command line, with LF line endings and no trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the topic has no such example.</exception>
    public static string RenderExample(HelpTopic topic, string name)
    {
        return GetExample(topic, name).Content;
    }

    /// <summary>
    ///     Renders a topic as a JSON document.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The document (<c>kind</c> <c>topic</c>), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="topic"/> is <see langword="null"/>.</exception>
    public static string RenderJson(HelpTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "topic");
            writer.WriteString("topic", topic.Name);
            WriteStrings(writer, "aliases", topic.Aliases);
            writer.WriteString("title", topic.Title);
            writer.WriteString("summary", topic.Summary);
            writer.WriteStartArray("sections");
            foreach (var section in topic.Sections)
            {
                WriteSection(writer, section);
            }

            writer.WriteEndArray();
            WriteExamples(writer, topic, includeContent: true);
        });
    }

    /// <summary>
    ///     Renders the list of the examples of a topic as a JSON document.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The document (<c>kind</c> <c>examples</c>, without the example contents), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="topic"/> is <see langword="null"/>.</exception>
    public static string RenderExampleListJson(HelpTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "examples");
            writer.WriteString("topic", topic.Name);
            WriteExamples(writer, topic, includeContent: false);
        });
    }

    /// <summary>
    ///     Renders one example of a topic as a JSON document.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The document (<c>kind</c> <c>example</c>), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the topic has no such example.</exception>
    public static string RenderExampleJson(HelpTopic topic, string name)
    {
        var example = GetExample(topic, name);
        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "example");
            writer.WriteString("topic", topic.Name);
            writer.WritePropertyName("example");
            WriteExample(writer, example, includeContent: true);
        });
    }

    /// <summary>
    ///     Appends text wrapped at <see cref="TextWidth"/> columns.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="text">The text; words are separated by single spaces.</param>
    /// <param name="firstPrefix">The text that starts the first line (an indent or a bullet).</param>
    /// <param name="restPrefix">The text that starts every further line (the hanging indent).</param>
    /// <remarks>
    ///     A word longer than the free width is never split: it sits on a line of its own.
    /// </remarks>
    internal static void AppendWrapped(StringBuilder builder, string text, string firstPrefix, string restPrefix)
    {
        var line = new StringBuilder(firstPrefix);
        var lineHasWord = false;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (lineHasWord && line.Length + 1 + word.Length > TextWidth)
            {
                builder.AppendLine(line.ToString());
                line.Clear().Append(restPrefix);
                lineHasWord = false;
            }

            if (lineHasWord)
            {
                line.Append(' ');
            }

            line.Append(word);
            lineHasWord = true;
        }

        builder.AppendLine(line.ToString());
    }

    /// <summary>
    ///     Appends one item of a section: a bullet, or a term followed by its indented text.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="item">The item.</param>
    private static void AppendItem(StringBuilder builder, HelpItem item)
    {
        if (item.Term == null)
        {
            AppendWrapped(builder, item.Text, "  - ", "    ");
            return;
        }

        builder.AppendLine($"  {item.Term}");
        AppendWrapped(builder, item.Text, "      ", "      ");
    }

    /// <summary>
    ///     Appends one line per example: the name, the kind in brackets, and the description.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="topic">The topic.</param>
    private static void AppendExampleLines(StringBuilder builder, HelpTopic topic)
    {
        var nameWidth = topic.Examples.Max(e => e.Name.Length);
        foreach (var example in topic.Examples)
        {
            var prefix = $"  {example.Name.PadRight(nameWidth)}  {$"[{example.Kind}]".PadRight(KindColumnWidth)}  ";
            AppendWrapped(builder, example.Description, prefix, new string(' ', prefix.Length));
        }
    }

    /// <summary>
    ///     Finds an example of a topic by name.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The example.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the topic has no such example.</exception>
    private static HelpExample GetExample(HelpTopic topic, string name)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(name);

        return topic.Examples.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ToolException(
                   ErrorCodes.UsageError,
                   $"Topic '{topic.Name}' has no example '{name}'.",
                   $"Examples: {string.Join(", ", topic.Examples.Select(e => e.Name))}.")
               {
                   Command = "help"
               };
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

    /// <summary>
    ///     Writes a section as a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="section">The section.</param>
    private static void WriteSection(Utf8JsonWriter writer, HelpSection section)
    {
        writer.WriteStartObject();
        writer.WriteString("id", section.Id);
        writer.WriteString("title", section.Title);
        writer.WriteStartArray("items");
        foreach (var item in section.Items)
        {
            writer.WriteStartObject();
            if (item.Term != null)
            {
                writer.WriteString("term", item.Term);
            }

            writer.WriteString("text", item.Text);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes the <c>examples</c> array of a topic.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="topic">The topic.</param>
    /// <param name="includeContent">Whether each example carries its content.</param>
    private static void WriteExamples(Utf8JsonWriter writer, HelpTopic topic, bool includeContent)
    {
        writer.WriteStartArray("examples");
        foreach (var example in topic.Examples)
        {
            WriteExample(writer, example, includeContent);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes an example as a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="example">The example.</param>
    /// <param name="includeContent">Whether the object carries the content.</param>
    private static void WriteExample(Utf8JsonWriter writer, HelpExample example, bool includeContent)
    {
        writer.WriteStartObject();
        writer.WriteString("name", example.Name);
        writer.WriteString("kind", example.Kind);
        writer.WriteString("language", example.Language);
        if (example.FileName != null)
        {
            writer.WriteString("fileName", example.FileName);
        }

        writer.WriteString("description", example.Description);
        if (includeContent)
        {
            writer.WriteString("content", example.Content);
        }

        writer.WriteEndObject();
    }
}
