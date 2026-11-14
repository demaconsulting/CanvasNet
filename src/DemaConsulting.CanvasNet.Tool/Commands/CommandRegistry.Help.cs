using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The help part of <see cref="CommandRegistry"/>: the reserved <c>help</c> command, which describes the
///     commands, the help cards, and the examples of a card, as plain text or as JSON.
/// </summary>
/// <remarks>
///     A help path is resolved in this order: no argument or <c>help</c> gives the index (the commands, the help topics,
///     and the global options); <c>topics</c> gives the whole tree of help cards; a command name or alias gives the help
///     of the command; anything else is a card path such as <c>mermaid gantt</c> (see <see cref="HelpTopics"/>). An
///     unknown first word is a <c>usage-error</c> that lists the valid subjects, and an unknown further word is a
///     <c>usage-error</c> that lists the cards at that level.
/// </remarks>
internal sealed partial class CommandRegistry
{
    /// <summary>
    ///     The help subject that lists every help card.
    /// </summary>
    private const string TopicsSubject = "topics";

    /// <summary>
    ///     The most words a help path may have.
    /// </summary>
    private const int MaxHelpDepth = 8;

    /// <summary>
    ///     The options of the <c>help</c> command, besides the global options.
    /// </summary>
    private static readonly OptionSpec[] HelpOptions =
    [
        new("--example", [], true, "Print only the source (or command line) of one example of the card or a card below it", "name"),
        new("--examples", [], false, "Print every example of the card in full, and list the cards below it that have examples")
    ];

    /// <summary>
    ///     The usage lines of the tool, shown at the top of the index.
    /// </summary>
    private static readonly string[] UsageLines =
    [
        "canvasnet <command> [arguments] [options]",
        "canvasnet [options]",
        "canvasnet help [<command> | <topic> [<card> ...] | topics] [--example <name> | --examples] [--json]"
    ];

    /// <summary>
    ///     Runs the <c>help</c> command: prints the help of a subject, the examples of a card in full, or one
    ///     example alone, to the context.
    /// </summary>
    /// <param name="context">The context holding the arguments of <c>help</c> and receiving its output.</param>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> for an unknown option, subject, or card, an unknown example,
    ///     <c>--example</c> together with <c>--examples</c>, or either of them without a card that has examples.
    /// </exception>
    private void RunHelp(Context context)
    {
        var reader = ArgumentReader.Parse(HelpCommandName, context.CommandArguments, HelpOptions);
        reader.RequirePositionals(0, MaxHelpDepth, "a command, or a help card path");
        var path = HelpTopics.SplitPath(reader.Positionals);
        var exampleName = reader.Get("--example");
        var list = reader.Has("--examples");

        // Plain help: the index, the tree, a command, or a card
        if (exampleName == null && !list)
        {
            context.WriteLine(RenderHelp(path, context.Json));
            return;
        }

        if (exampleName != null && list)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The options '--example' and '--examples' cannot be combined.",
                "Use '--examples' to print the examples of a card and '--example <name>' to print one alone.")
            {
                Command = HelpCommandName
            };
        }

        var card = RequireCardForExamples(path);
        if (exampleName != null)
        {
            context.WriteLine(context.Json
                ? HelpTopics.RenderExampleJson(card, exampleName)
                : HelpTopics.RenderExample(card, exampleName));
            return;
        }

        context.WriteLine(context.Json ? HelpTopics.RenderExamplesJson(card) : HelpTopics.RenderExamples(card));
    }

    /// <summary>
    ///     Renders help text for one subject.
    /// </summary>
    /// <param name="subject">
    ///     The name or alias of a command or a help topic to describe, <c>topics</c> for the whole tree of help cards,
    ///     or <see langword="null"/> (or the reserved name <c>help</c>) for the index: the command list, the topic
    ///     list, and the global options.
    /// </param>
    /// <param name="json">Whether to render a JSON document instead of plain text.</param>
    /// <returns>The help text or document, without a trailing newline.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when <paramref name="subject"/> is unknown.</exception>
    public string RenderHelp(string? subject = null, bool json = false)
    {
        return RenderHelp(subject == null ? Array.Empty<string>() : [subject], json);
    }

    /// <summary>
    ///     Renders help text for a help path.
    /// </summary>
    /// <param name="path">
    ///     The words of the path: empty (or the reserved name <c>help</c>) for the index, <c>topics</c> for the whole
    ///     tree, a command name or alias for the help of the command, or a card path such as <c>mermaid gantt</c>.
    /// </param>
    /// <param name="json">Whether to render a JSON document instead of plain text.</param>
    /// <returns>The help text or document, without a trailing newline.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the path is unknown.</exception>
    public string RenderHelp(IReadOnlyList<string> path, bool json = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Count == 0)
        {
            return json ? RenderIndexJson() : RenderIndexText();
        }

        var subject = path[0];
        var isHelp = string.Equals(subject, HelpCommandName, StringComparison.OrdinalIgnoreCase);
        var isTopics = string.Equals(subject, TopicsSubject, StringComparison.OrdinalIgnoreCase);
        var command = isHelp || isTopics ? null : Find(subject);
        if (path.Count > 1 && (isHelp || isTopics || command != null))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{subject}' has no help cards below it.",
                $"Run 'canvasnet help {subject}' on its own, or 'canvasnet help topics' to list the help cards.")
            {
                Command = HelpCommandName
            };
        }

        if (isHelp)
        {
            return json ? RenderIndexJson() : RenderIndexText();
        }

        if (isTopics)
        {
            return json ? HelpTopics.RenderTopicsJson() : HelpTopics.RenderTopicsText();
        }

        if (command != null)
        {
            return json ? RenderCommandJson(command) : RenderCommandText(command);
        }

        var card = HelpTopics.Resolve(path) ?? throw CreateUnknownSubjectException(subject);
        return json ? HelpTopics.RenderJson(card) : HelpTopics.RenderText(card);
    }

    /// <summary>
    ///     Renders the index as text: usage, the command list, the topic list, and the global options.
    /// </summary>
    /// <returns>The help text.</returns>
    private string RenderIndexText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Usage: {UsageLines[0]}");
        foreach (var line in UsageLines.Skip(1))
        {
            builder.AppendLine($"       {line}");
        }

        // List the commands (name and aliases, aligned) when any are registered
        if (_commands.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Commands:");
            var labels = _commands.Select(c => string.Join(", ", c.Aliases.Prepend(c.Name))).ToList();
            var width = labels.Max(l => l.Length);
            for (var i = 0; i < _commands.Count; i++)
            {
                builder.AppendLine($"  {labels[i].PadRight(width)}  {_commands[i].Summary}");
            }

            builder.AppendLine();
            builder.AppendLine("Run 'canvasnet <command> --help' for help on a command.");
        }

        AppendTopicList(builder);

        builder.AppendLine();
        builder.AppendLine("Options:");
        AppendOptions(builder, GlobalOptions);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     Appends the topic list: the root help cards with their summaries, their aliases, and how to read a card.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    private static void AppendTopicList(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("Help topics:");
        var roots = HelpTopics.Roots.ToList();
        var width = roots.Max(t => t.Name.Length);
        foreach (var topic in roots)
        {
            var prefix = $"  {topic.Name.PadRight(width)}  ";
            HelpTopics.AppendWrapped(builder, topic.Summary, prefix, new string(' ', prefix.Length));
        }

        var aliases = roots.Where(t => t.Aliases.Count > 0).Select(t => $"{t.Name} = {string.Join(", ", t.Aliases)}");
        HelpTopics.AppendWrapped(builder, $"Aliases: {string.Join("; ", aliases)}", "  ", "    ");
        builder.AppendLine();
        HelpTopics.AppendWrapped(
            builder,
            "Run 'canvasnet help <topic> [<card> ...]' to read a card (for example 'canvasnet help mermaid gantt'); "
            + "'canvasnet help topics' lists every card. For good-looking diagrams start with 'canvasnet help style' "
            + "and 'canvasnet help composition'.",
            string.Empty,
            string.Empty);
    }

    /// <summary>
    ///     Renders the help of one command as text: usage, summary, its options, and the global options.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The help text.</returns>
    private static string RenderCommandText(ICommand command)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Usage: {command.Usage}");
        builder.AppendLine();
        builder.AppendLine(command.Summary);

        if (command.Options.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Options:");
            AppendOptions(builder, command.Options);
        }

        builder.AppendLine();
        builder.AppendLine("Global options:");
        AppendOptions(builder, GlobalOptions);
        builder.AppendLine();
        HelpTopics.AppendWrapped(builder, SeeAlso, string.Empty, "  ");
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     The line at the end of the help of every command that points to the help topics.
    /// </summary>
    internal static string SeeAlso { get; } =
        "See also: 'canvasnet help <topic>' for what a format supports, examples, known differences, and how to make "
        + "a picture look good (topics: "
        + string.Join(", ", HelpTopics.TopicNames)
        + "), and 'canvasnet help topics' to list every card.";

    /// <summary>
    ///     Appends an aligned option table.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="options">The options to list.</param>
    private static void AppendOptions(StringBuilder builder, IReadOnlyList<OptionSpec> options)
    {
        // Build each option label as "alias, --name <value>" and align the descriptions
        var labels = options.Select(o =>
        {
            var names = string.Join(", ", o.Aliases.Append(o.Name));
            return o.TakesValue ? $"{names} <{o.ValueName ?? "value"}>" : names;
        }).ToList();
        var width = labels.Max(l => l.Length);
        for (var i = 0; i < options.Count; i++)
        {
            builder.AppendLine($"  {labels[i].PadRight(width)}  {options[i].Description}");
        }
    }

    /// <summary>
    ///     Renders the index as a JSON document (<c>kind</c> <c>index</c>).
    /// </summary>
    /// <returns>The document.</returns>
    private string RenderIndexJson()
    {
        return ResultWriter.Success(HelpCommandName, writer =>
        {
            writer.WriteString("kind", "index");
            writer.WriteString("usage", string.Join("\n", UsageLines));

            writer.WriteStartArray("commands");
            foreach (var command in _commands)
            {
                WriteSummary(writer, command.Name, command.Aliases, command.Summary);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("topics");
            foreach (var topic in HelpTopics.Roots)
            {
                WriteSummary(writer, topic.Name, topic.Aliases, topic.Summary);
            }

            writer.WriteEndArray();
            WriteOptions(writer, "globalOptions", GlobalOptions);
        });
    }

    /// <summary>
    ///     Renders the help of one command as a JSON document (<c>kind</c> <c>command</c>).
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The document.</returns>
    private static string RenderCommandJson(ICommand command)
    {
        return ResultWriter.Success(HelpCommandName, writer =>
        {
            writer.WriteString("kind", "command");
            writer.WriteString("topic", command.Name);
            HelpTopics.WriteStrings(writer, "aliases", command.Aliases);
            writer.WriteString("summary", command.Summary);
            writer.WriteString("usage", command.Usage.ReplaceLineEndings("\n"));
            WriteOptions(writer, "options", command.Options);
            WriteOptions(writer, "globalOptions", GlobalOptions);
        });
    }

    /// <summary>
    ///     Writes the name, aliases, and summary of a command or topic as a JSON object.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The canonical name.</param>
    /// <param name="aliases">The aliases.</param>
    /// <param name="summary">The one-line summary.</param>
    private static void WriteSummary(Utf8JsonWriter writer, string name, IEnumerable<string> aliases, string summary)
    {
        writer.WriteStartObject();
        writer.WriteString("name", name);
        HelpTopics.WriteStrings(writer, "aliases", aliases);
        writer.WriteString("summary", summary);
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes an array of option declarations.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="property">The property name of the array.</param>
    /// <param name="options">The options.</param>
    private static void WriteOptions(Utf8JsonWriter writer, string property, IReadOnlyList<OptionSpec> options)
    {
        writer.WriteStartArray(property);
        foreach (var option in options)
        {
            writer.WriteStartObject();
            writer.WriteString("name", option.Name);
            HelpTopics.WriteStrings(writer, "aliases", option.Aliases);
            writer.WriteBoolean("takesValue", option.TakesValue);
            if (option.ValueName != null)
            {
                writer.WriteString("valueName", option.ValueName);
            }

            writer.WriteString("description", option.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Resolves the path of <c>--example</c> or <c>--examples</c> to a card that has examples (its own or below it).
    /// </summary>
    /// <param name="path">The words of the path; empty when none was given.</param>
    /// <returns>The card.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when no path was given, when it is a command, the index, or the tree (which
    ///     have no examples), when it is unknown, or when neither the card nor a card below it has an example.
    /// </exception>
    private HelpCard RequireCardForExamples(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "'--example' and '--examples' need a help card.",
                "Run 'canvasnet help topics' to list the help cards, then 'canvasnet help <topic> [<card> ...] --examples'.")
            {
                Command = HelpCommandName
            };
        }

        // Commands, the index, and the tree have usage text, not examples
        var subject = path[0];
        if (Find(subject) != null
            || string.Equals(subject, TopicsSubject, StringComparison.OrdinalIgnoreCase)
            || string.Equals(subject, HelpCommandName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{subject}' has no examples; only help cards do.",
                $"Run 'canvasnet help {subject}' for its usage, or 'canvasnet help topics' to list the help cards.")
            {
                Command = HelpCommandName
            };
        }

        var card = HelpTopics.Resolve(path) ?? throw CreateUnknownSubjectException(subject);
        if (!HelpTopics.SubtreeExamples(card).Any())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{card.Path}' has no examples.",
                $"Run 'canvasnet help {card.RootId} --examples' to find the cards of '{card.RootId}' that have examples.")
            {
                Command = HelpCommandName
            };
        }

        return card;
    }

    /// <summary>
    ///     Creates the exception reported for an unknown help subject: it suggests the closest name and lists the
    ///     valid subjects.
    /// </summary>
    /// <param name="subject">The unknown subject as typed.</param>
    /// <returns>The exception to throw.</returns>
    private ToolException CreateUnknownSubjectException(string subject)
    {
        var names = _commands.SelectMany(c => c.Aliases.Prepend(c.Name))
            .Concat(HelpTopics.Roots.SelectMany(t => t.Aliases.Prepend(t.Name)))
            .Append(TopicsSubject)
            .Append(HelpCommandName);
        var suggestion = ClosestName(subject, names);

        var valid = string.Join(", ", _commands.Select(c => c.Name).Concat(HelpTopics.TopicNames).Append(TopicsSubject).Append(HelpCommandName));
        var hint = $"Valid subjects: {valid}. Run 'canvasnet help topics' for descriptions.";
        if (suggestion != null)
        {
            hint = $"Did you mean '{suggestion}'? {hint}";
        }

        return new ToolException(ErrorCodes.UsageError, $"Unknown help subject '{subject}'.", hint)
        {
            Command = HelpCommandName
        };
    }
}
