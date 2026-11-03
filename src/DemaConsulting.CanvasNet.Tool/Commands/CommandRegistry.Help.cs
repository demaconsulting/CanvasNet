// cspell:ignore canvasnet
using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The help part of <see cref="CommandRegistry"/>: the reserved <c>help</c> command, which describes the
///     commands, the help topics, and the examples of a topic, as plain text or as JSON.
/// </summary>
/// <remarks>
///     A help subject is resolved in this order: no subject, <c>help</c>, or <c>topics</c> give the index (the
///     commands, the topics, and the global options); a command name or alias gives the help of the command; a
///     topic name or alias gives the topic (see <see cref="HelpTopics"/>). Anything else is a <c>usage-error</c>
///     that lists the valid subjects.
/// </remarks>
internal sealed partial class CommandRegistry
{
    /// <summary>
    ///     The help subject that lists every command and topic.
    /// </summary>
    private const string TopicsSubject = "topics";

    /// <summary>
    ///     The options of the <c>help</c> command, besides the global options.
    /// </summary>
    private static readonly OptionSpec[] HelpOptions =
    [
        new("--example", [], true, "Print only the source (or command line) of one example of the topic", "name"),
        new("--examples", [], false, "List the examples of the topic")
    ];

    /// <summary>
    ///     The usage lines of the tool, shown at the top of the index.
    /// </summary>
    private static readonly string[] UsageLines =
    [
        "canvasnet <command> [arguments] [options]",
        "canvasnet [options]",
        "canvasnet help [<command>|<topic>] [--example <name> | --examples] [--json]"
    ];

    /// <summary>
    ///     Runs the <c>help</c> command: prints the help of a subject, or one of its examples, to the context.
    /// </summary>
    /// <param name="context">The context holding the arguments of <c>help</c> and receiving its output.</param>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> for an unknown option or subject, more than one subject, an unknown
    ///     example, <c>--example</c> together with <c>--examples</c>, or either of them without a topic.
    /// </exception>
    private void RunHelp(Context context)
    {
        var reader = ArgumentReader.Parse(HelpCommandName, context.CommandArguments, HelpOptions);
        reader.RequirePositionals(0, 1, "one command or topic");
        var subject = reader.Positionals.FirstOrDefault();
        var exampleName = reader.Get("--example");
        var list = reader.Has("--examples");

        // Plain help: the index, a command, or a topic
        if (exampleName == null && !list)
        {
            context.WriteLine(RenderHelp(subject, context.Json));
            return;
        }

        if (exampleName != null && list)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The options '--example' and '--examples' cannot be combined.",
                "Use '--examples' to list the examples and '--example <name>' to print one.")
            {
                Command = HelpCommandName
            };
        }

        var topic = RequireTopicForExamples(subject);
        if (exampleName != null)
        {
            context.WriteLine(context.Json
                ? HelpTopics.RenderExampleJson(topic, exampleName)
                : HelpTopics.RenderExample(topic, exampleName));
            return;
        }

        context.WriteLine(context.Json ? HelpTopics.RenderExampleListJson(topic) : HelpTopics.RenderExampleList(topic));
    }

    /// <summary>
    ///     Renders help text.
    /// </summary>
    /// <param name="subject">
    ///     The name or alias of a command or a help topic to describe, or <see langword="null"/> (or the reserved name
    ///     <c>help</c>, or <c>topics</c>) for the index: the command list, the topic list, and the global options.
    /// </param>
    /// <param name="json">Whether to render a JSON document instead of plain text.</param>
    /// <returns>The help text or document, without a trailing newline.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when <paramref name="subject"/> is unknown.</exception>
    public string RenderHelp(string? subject = null, bool json = false)
    {
        if (subject == null
            || string.Equals(subject, HelpCommandName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(subject, TopicsSubject, StringComparison.OrdinalIgnoreCase))
        {
            return json ? RenderIndexJson() : RenderIndexText();
        }

        if (Find(subject) is { } command)
        {
            return json ? RenderCommandJson(command) : RenderCommandText(command);
        }

        var topic = HelpTopics.Find(subject) ?? throw CreateUnknownSubjectException(subject);
        return json ? HelpTopics.RenderJson(topic) : HelpTopics.RenderText(topic);
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
    ///     Appends the topic list: the topics with their summaries, their aliases, and how to read one.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    private static void AppendTopicList(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("Help topics:");
        var width = HelpTopics.All.Max(t => t.Name.Length);
        foreach (var topic in HelpTopics.All)
        {
            var prefix = $"  {topic.Name.PadRight(width)}  ";
            HelpTopics.AppendWrapped(builder, topic.Summary, prefix, new string(' ', prefix.Length));
        }

        var aliases = HelpTopics.All.Where(t => t.Aliases.Count > 0).Select(t => $"{t.Name} = {string.Join(", ", t.Aliases)}");
        HelpTopics.AppendWrapped(builder, $"Aliases: {string.Join("; ", aliases)}", "  ", "    ");
        builder.AppendLine();
        HelpTopics.AppendWrapped(
            builder,
            "Run 'canvasnet help <topic>' for a format or topic, and 'canvasnet help topics' to list everything.",
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
        return builder.ToString().TrimEnd();
    }

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
            writer.WriteString("topic", TopicsSubject);
            writer.WriteString("usage", string.Join("\n", UsageLines));

            writer.WriteStartArray("commands");
            foreach (var command in _commands)
            {
                WriteSummary(writer, command.Name, command.Aliases, command.Summary);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("topics");
            foreach (var topic in HelpTopics.All)
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
            writer.WriteString("usage", command.Usage);
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
    ///     Resolves the subject of <c>--example</c> or <c>--examples</c> to a topic.
    /// </summary>
    /// <param name="subject">The subject as typed, or <see langword="null"/> when none was given.</param>
    /// <returns>The topic.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when no subject was given, when it is a command or the index (which have
    ///     no examples), or when it is unknown.
    /// </exception>
    private HelpTopic RequireTopicForExamples(string? subject)
    {
        if (subject == null)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "'--example' and '--examples' need a topic.",
                "Run 'canvasnet help topics' to list the topics, then 'canvasnet help <topic> --examples'.")
            {
                Command = HelpCommandName
            };
        }

        if (HelpTopics.Find(subject) is { } topic)
        {
            return topic;
        }

        // Commands and the index have usage text, not examples; anything else is an unknown subject
        if (Find(subject) != null || string.Equals(subject, TopicsSubject, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{subject}' has no examples; only help topics do.",
                $"Run 'canvasnet help {subject}' for its usage, or 'canvasnet help topics' to list the topics.")
            {
                Command = HelpCommandName
            };
        }

        throw CreateUnknownSubjectException(subject);
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
            .Concat(HelpTopics.All.SelectMany(t => t.Aliases.Prepend(t.Name)))
            .Append(TopicsSubject);
        var suggestion = ClosestName(subject, names);

        var valid = string.Join(", ", _commands.Select(c => c.Name).Concat(HelpTopics.TopicNames).Append(TopicsSubject));
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
