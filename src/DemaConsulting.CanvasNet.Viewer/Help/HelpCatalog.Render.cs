using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The rendering of help cards for a reader: plain text and JSON.
/// </summary>
/// <remarks>
///     The text of a card is its title, summary, aliases, tags, and Markdown body, followed by a generated footer: the
///     cards below it, its examples, and its related cards, each named by identifier with its summary. Only what the
///     view shows is listed. The output is the same on every machine: line feeds only, no trailing newline, and the
///     generated lines wrapped at <see cref="TextWidth"/> columns.
/// </remarks>
public sealed partial class HelpCatalog
{
    /// <summary>
    ///     The column at which the generated help text is wrapped.
    /// </summary>
    internal const int TextWidth = 78;

    /// <summary>
    ///     Gets the headings of the generated footer of a card, which a card body must not use.
    /// </summary>
    internal static IReadOnlyList<string> FooterHeadings { get; } = Array.AsReadOnly(["## Cards", "## Examples", "## See also"]);

    /// <summary>
    ///     Writes a card as plain text for a reader such as an agent.
    /// </summary>
    /// <param name="card">A card visible in this view (a card of another view with the same identifier is accepted).</param>
    /// <returns>
    ///     The text, without a trailing newline: <c># Title</c>, the summary, the aliases and tags, the
    ///     <see cref="HelpCard.Markdown"/> body, and the <c>## Cards</c>, <c>## Examples</c> (names and descriptions; the
    ///     contents are in <see cref="HelpCard.Examples"/>), and <c>## See also</c> sections of what this view shows.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    public string RenderText(HelpCard card)
    {
        card = Require(card);

        // The heading: title, summary, aliases, and tags
        var builder = new StringBuilder();
        builder.Append("# ").Append(card.Title).Append("\n\n");
        AppendWrapped(builder, card.Summary, string.Empty, string.Empty);
        if (card.Aliases.Count > 0)
        {
            AppendWrapped(builder, $"Also known as: {string.Join(", ", card.Aliases)}", string.Empty, "  ");
        }

        AppendWrapped(builder, $"Tags: {string.Join(", ", card.Tags)}", string.Empty, "  ");
        builder.Append('\n').Append(card.Markdown).Append('\n');

        // The footer: the cards below, the examples, and the related cards
        var children = GetChildren(card);
        if (children.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[0]).Append("\n\n");
            AppendCardList(builder, children);
        }

        if (card.Examples.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[1]).Append("\n\n");
            foreach (var example in card.Examples)
            {
                var file = example.FileName == null ? string.Empty : $" {example.FileName}";
                AppendWrapped(builder, $"{example.Name} [{example.Kind}]{file}: {example.Description}", "- ", "  ");
            }
        }

        var seeAlso = GetSeeAlso(card);
        if (seeAlso.Count > 0)
        {
            builder.Append('\n').Append(FooterHeadings[2]).Append("\n\n");
            AppendCardList(builder, seeAlso);
        }

        return builder.ToString().TrimEnd('\n');
    }

    /// <summary>
    ///     Writes a card as one JSON object for a program or an agent that reads JSON.
    /// </summary>
    /// <param name="card">A card visible in this view (a card of another view with the same identifier is accepted).</param>
    /// <returns>
    ///     The object, on one line: <c>id</c>, <c>title</c>, <c>summary</c>, <c>tags</c>, <c>aliases</c>, <c>body</c> (the
    ///     <see cref="HelpCard.Markdown"/>), <c>children</c> and <c>seeAlso</c> (arrays of <c>id</c>, <c>title</c>, and
    ///     <c>summary</c> of what this view shows), and <c>examples</c> (<c>name</c>, <c>kind</c>, <c>language</c>,
    ///     <c>fileName</c> when there is one, <c>description</c>, and <c>content</c>). Characters outside ASCII and
    ///     HTML-sensitive characters are escaped.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    public string RenderJson(HelpCard card)
    {
        card = Require(card);
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", card.Id);
            writer.WriteString("title", card.Title);
            writer.WriteString("summary", card.Summary);
            WriteStrings(writer, "tags", card.Tags);
            WriteStrings(writer, "aliases", card.Aliases);
            writer.WriteString("body", card.Markdown);
            WriteCardList(writer, "children", GetChildren(card));
            WriteCardList(writer, "seeAlso", GetSeeAlso(card));
            writer.WriteStartArray("examples");
            foreach (var example in card.Examples)
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
                writer.WriteString("content", example.Content);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>
    ///     Writes the visible tree as plain text, so a reader can choose a card.
    /// </summary>
    /// <returns>
    ///     One line per visible card, depth first, indented by its depth in this view, with its identifier and summary;
    ///     no trailing newline.
    /// </returns>
    public string RenderIndexText()
    {
        // The depth of a card in this view: the number of its visible ancestors
        int Depth(HelpCard card)
        {
            var depth = 0;
            for (var parent = card.ParentId; parent != null; parent = _data.ById[parent].ParentId)
            {
                depth += IsVisible(parent) ? 1 : 0;
            }

            return depth;
        }

        var builder = new StringBuilder();
        var labels = Cards.Select(c => new string(' ', 2 * Depth(c)) + c.Id).ToList();
        var width = labels.Count == 0 ? 0 : labels.Max(l => l.Length);
        for (var i = 0; i < Cards.Count; i++)
        {
            var prefix = $"{labels[i].PadRight(width)}  ";
            AppendWrapped(builder, Cards[i].Summary, prefix, new string(' ', prefix.Length));
        }

        return builder.ToString().TrimEnd('\n');
    }

    /// <summary>
    ///     Writes the visible tree as one JSON object.
    /// </summary>
    /// <returns>
    ///     <c>{"cards":[...]}</c> with one object per visible card, depth first: <c>id</c>, <c>title</c>, <c>summary</c>,
    ///     <c>tags</c>, and <c>aliases</c>.
    /// </returns>
    public string RenderIndexJson()
    {
        return WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("cards");
            foreach (var card in Cards)
            {
                writer.WriteStartObject();
                writer.WriteString("id", card.Id);
                writer.WriteString("title", card.Title);
                writer.WriteString("summary", card.Summary);
                WriteStrings(writer, "tags", card.Tags);
                WriteStrings(writer, "aliases", card.Aliases);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>
    ///     Appends text wrapped at <see cref="TextWidth"/> columns, followed by a line feed.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="text">The text; words are separated by spaces.</param>
    /// <param name="firstPrefix">The text that starts the first line (an indent or a bullet).</param>
    /// <param name="restPrefix">The text that starts every further line (the hanging indent).</param>
    /// <remarks>A word longer than the free width is never split: it sits on a line of its own.</remarks>
    private static void AppendWrapped(StringBuilder builder, string text, string firstPrefix, string restPrefix)
    {
        var line = new StringBuilder(firstPrefix);
        var lineHasWord = false;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (lineHasWord && line.Length + 1 + word.Length > TextWidth)
            {
                builder.Append(line).Append('\n');
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

        builder.Append(line).Append('\n');
    }

    /// <summary>
    ///     Appends a list of cards: a bullet with the identifier and, indented below, the summary.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="cards">The cards.</param>
    private static void AppendCardList(StringBuilder builder, IEnumerable<HelpCard> cards)
    {
        foreach (var card in cards)
        {
            builder.Append("- ").Append(card.Id).Append('\n');
            AppendWrapped(builder, card.Summary, "  ", "  ");
        }
    }

    /// <summary>
    ///     Writes a JSON document to a string.
    /// </summary>
    /// <param name="write">Writes the document.</param>
    /// <returns>The document, on one line.</returns>
    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Writes a JSON array of strings.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="values">The strings.</param>
    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes a JSON array of cards: the identifier, the title, and the summary of each.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="cards">The cards.</param>
    private static void WriteCardList(Utf8JsonWriter writer, string name, IEnumerable<HelpCard> cards)
    {
        writer.WriteStartArray(name);
        foreach (var card in cards)
        {
            writer.WriteStartObject();
            writer.WriteString("id", card.Id);
            writer.WriteString("title", card.Title);
            writer.WriteString("summary", card.Summary);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
