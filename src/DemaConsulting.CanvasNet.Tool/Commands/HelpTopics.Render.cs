using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The rendering of help cards: plain text and JSON.
/// </summary>
/// <remarks>
///     The text of a card is its title, summary, and Markdown body as written (the tokens replaced), followed by a
///     generated footer: the cards below it, its examples, and its See also links, each with the exact command and the
///     summary of the target. Nothing is converted for a terminal: no ANSI escape sequences, no console-width detection,
///     and line feeds only whatever the operating system, so the same text is produced on every machine. The generated
///     lines are wrapped at <see cref="TextWidth"/> columns. The JSON documents are written through
///     <see cref="ResultWriter"/>, so they have its envelope, key order, and ASCII-only escaping.
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The column at which the generated help text is wrapped.
    /// </summary>
    internal const int TextWidth = 78;

    /// <summary>
    ///     The width of the kind column of an example list: the length of <c>[command]</c>.
    /// </summary>
    private const int KindColumnWidth = 9;

    /// <summary>
    ///     The headings of the generated footer of a card, which a card body must not use.
    /// </summary>
    internal static IReadOnlyList<string> FooterHeadings { get; } = ["## Cards", "## Examples", "## See also"];

    /// <summary>
    ///     Renders a card as plain text.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The text, without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    public static string RenderText(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var builder = new StringBuilder();
        builder.Append("# ").Append(card.Title).Append('\n');
        builder.Append('\n');
        AppendWrapped(builder, card.Summary, string.Empty, string.Empty);
        if (card.Aliases.Count > 0)
        {
            builder.Append('\n');
            AppendWrapped(builder, $"Also known as: {string.Join(", ", card.Aliases)}", string.Empty, "  ");
        }

        builder.Append('\n');
        builder.Append(card.Body).Append('\n');

        // The cards below this one, each with the command that prints it
        var children = ChildrenOf(card);
        if (children.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[0]).Append("\n\n");
            AppendLinks(builder, children);
            if (SubtreeExamples(card).Skip(card.Examples.Count).Any())
            {
                builder.Append('\n');
                AppendWrapped(builder, $"List the examples of every card below with: {card.Command} --examples", string.Empty, "  ");
            }
        }

        // The card's own examples, and how to print one
        if (card.Examples.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[1]).Append("\n\n");
            AppendExampleLines(builder, card.Examples.Select(e => (card, e)).ToList());
            builder.Append('\n');
            AppendWrapped(
                builder,
                $"Print one with: {card.Command} --example <name> (add --json for a document).",
                string.Empty,
                "  ");
        }

        // The related cards, each with the command that prints it and its summary
        if (card.SeeAlso.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[2]).Append("\n\n");
            AppendLinks(builder, card.SeeAlso.Select(Get).ToList());
        }

        return builder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    /// <summary>
    ///     Renders the whole tree as plain text: one line per card, depth first, with its help path and summary.
    /// </summary>
    /// <returns>The text, without a trailing newline.</returns>
    public static string RenderTopicsText()
    {
        var builder = new StringBuilder();
        AppendWrapped(
            builder,
            "Help cards, depth first. Read one with 'canvasnet help <path>', for example 'canvasnet help mermaid gantt'.",
            string.Empty,
            string.Empty);
        builder.Append('\n');
        var labels = All.Select(c => new string(' ', 2 * c.Depth) + c.Path).ToList();
        var width = labels.Max(l => l.Length);
        for (var i = 0; i < All.Count; i++)
        {
            var prefix = $"{labels[i].PadRight(width)}  ";
            AppendWrapped(builder, All[i].Summary, prefix, new string(' ', prefix.Length));
        }

        return builder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    /// <summary>
    ///     Renders the whole tree as a JSON document (<c>kind</c> <c>topics</c>).
    /// </summary>
    /// <returns>The document, without a trailing newline.</returns>
    public static string RenderTopicsJson()
    {
        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "topics");
            writer.WriteStartArray("cards");
            foreach (var card in All)
            {
                writer.WriteStartObject();
                writer.WriteString("id", card.Id);
                WriteStrings(writer, "aliases", card.Aliases);
                writer.WriteString("summary", card.Summary);
                writer.WriteString("command", card.Command);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>
    ///     Renders the list of the examples of a card and of the cards below it as plain text.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>One line per example (name, kind, description), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    public static string RenderExampleList(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var builder = new StringBuilder();
        AppendExampleLines(builder, SubtreeExamples(card).ToList());
        return builder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    /// <summary>
    ///     Gets the exact content of one example of a card or of a card below it.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The content: the source file or the command line, with LF line endings and no trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when there is no such example.</exception>
    public static string RenderExample(HelpCard card, string name)
    {
        return GetExample(card, name).Example.Content;
    }

    /// <summary>
    ///     Renders a card as a JSON document.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The document (<c>kind</c> <c>card</c>), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    public static string RenderJson(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "card");
            writer.WriteString("id", card.Id);
            WriteStrings(writer, "aliases", card.Aliases);
            writer.WriteString("title", card.Title);
            writer.WriteString("summary", card.Summary);
            writer.WriteString("body", card.Body);
            WriteLinks(writer, "children", ChildrenOf(card));
            WriteLinks(writer, "seeAlso", card.SeeAlso.Select(Get));
            writer.WriteStartArray("examples");
            foreach (var example in card.Examples)
            {
                WriteExample(writer, null, example, includeContent: true);
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>
    ///     Renders the list of the examples of a card and of the cards below it as a JSON document.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The document (<c>kind</c> <c>examples</c>, without the example contents), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    public static string RenderExampleListJson(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "examples");
            writer.WriteString("id", card.Id);
            writer.WriteStartArray("examples");
            foreach (var (owner, example) in SubtreeExamples(card))
            {
                WriteExample(writer, owner, example, includeContent: false);
            }

            writer.WriteEndArray();
        });
    }

    /// <summary>
    ///     Renders one example of a card or of a card below it as a JSON document.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The document (<c>kind</c> <c>example</c>), without a trailing newline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when there is no such example.</exception>
    public static string RenderExampleJson(HelpCard card, string name)
    {
        var (owner, example) = GetExample(card, name);
        return ResultWriter.Success("help", writer =>
        {
            writer.WriteString("kind", "example");
            writer.WriteString("id", card.Id);
            writer.WritePropertyName("example");
            WriteExample(writer, owner, example, includeContent: true);
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
    ///     Appends a list of cards: for each, a bullet with the command that prints it and, indented below, its summary.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="cards">The cards.</param>
    private static void AppendLinks(StringBuilder builder, IEnumerable<HelpCard> cards)
    {
        foreach (var target in cards)
        {
            builder.Append("- ").Append(target.Command).Append('\n');
            AppendWrapped(builder, target.Summary, "  ", "  ");
        }
    }

    /// <summary>
    ///     Appends one line per example: the name, the kind in brackets, and the description.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="examples">The examples with their cards.</param>
    private static void AppendExampleLines(StringBuilder builder, IReadOnlyList<(HelpCard Card, HelpExample Example)> examples)
    {
        var nameWidth = examples.Max(e => e.Example.Name.Length);
        foreach (var (_, example) in examples)
        {
            var prefix = $"  {example.Name.PadRight(nameWidth)}  {$"[{example.Kind}]".PadRight(KindColumnWidth)}  ";
            AppendWrapped(builder, example.Description, prefix, new string(' ', prefix.Length));
        }
    }

    /// <summary>
    ///     Finds an example of a card or of a card below it by name.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="name">The example name; matched ignoring case.</param>
    /// <returns>The example with the card it belongs to.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when there is no such example.</exception>
    private static (HelpCard Card, HelpExample Example) GetExample(HelpCard card, string name)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(name);

        var examples = SubtreeExamples(card).ToList();
        var found = examples.FirstOrDefault(e => string.Equals(e.Example.Name, name, StringComparison.OrdinalIgnoreCase));
        if (found.Example != null)
        {
            return found;
        }

        var hint = examples.Count > 0
            ? $"Examples: {string.Join(", ", examples.Select(e => e.Example.Name))}."
            : $"Run 'canvasnet help {card.RootId} --examples' to list the examples of '{card.RootId}'.";
        throw new ToolException(ErrorCodes.UsageError, $"Help card '{card.Path}' has no example '{name}'.", hint)
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
    ///     Writes a JSON array of card links: the identifier, the summary, and the command of each card.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="cards">The cards.</param>
    private static void WriteLinks(Utf8JsonWriter writer, string name, IEnumerable<HelpCard> cards)
    {
        writer.WriteStartArray(name);
        foreach (var target in cards)
        {
            writer.WriteStartObject();
            writer.WriteString("id", target.Id);
            writer.WriteString("summary", target.Summary);
            writer.WriteString("command", target.Command);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes an example as a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="card">The card the example belongs to, or <see langword="null"/> to leave out the <c>card</c> key.</param>
    /// <param name="example">The example.</param>
    /// <param name="includeContent">Whether the object carries the content.</param>
    private static void WriteExample(Utf8JsonWriter writer, HelpCard? card, HelpExample example, bool includeContent)
    {
        writer.WriteStartObject();
        if (card != null)
        {
            writer.WriteString("card", card.Id);
        }

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
