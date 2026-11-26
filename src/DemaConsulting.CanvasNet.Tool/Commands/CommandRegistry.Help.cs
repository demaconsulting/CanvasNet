using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Integration;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The help part of <see cref="CommandRegistry"/>: the reserved <c>help</c> command, which describes the
///     commands, the help cards, and the examples of a card, as plain text or as JSON.
/// </summary>
/// <remarks>
///     <para>
///         The command runs on DemaConsulting.HelpCards' <see cref="HelpCli"/>, which parses the help options and answers
///         the index, a card, its examples, one example, a search, and a tag list from <see cref="HelpTopics.Catalog"/>.
///         The tool keeps its own contract through the options of the helper: the JSON envelope of
///         <see cref="ResultWriter"/> (<see cref="HelpTopics.Envelope"/>), the <c>error</c> object of every failure
///         document (<see cref="HelpTopics.WriteError"/>), and exit code 1 for every failure. The subjects that are not
///         cards are answered by the tool first (<see cref="ResolveOwnSubject"/>): a command or alias gives the help of
///         the command, <c>topics</c> the whole tree of help cards, and an unknown first word is a <c>usage-error</c>
///         that lists the valid subjects.
///     </para>
///     <para>
///         Four pieces of local glue keep the contract of the tool where HelpCli differs: a help option written as
///         <c>--name=value</c> is split into two words before the run (<see cref="SplitInlineValues"/>), the
///         document of one example keeps the card that was asked for as its <c>id</c> (<see cref="WriteWithId"/>),
///         the examples of a command, <c>topics</c>, or <c>help</c> fail with the tool's message that only help
///         cards have examples (<see cref="CreateNoExamplesError"/>), and a failure that HelpCards reports without a
///         hint (some of its argument errors) gets a hint that points at the help options and the help cards
///         (<see cref="HelpOptionsHint"/>). JSON is asked with <c>--json</c> or with
///         <c>--format json</c> (the value in any case), as HelpCards reads it, and with either a failure of a subject
///         that the tool answers itself is written as the failure document too.
///     </para>
///     <para>
///         Everything is written through the <see cref="Context"/>, so <c>--silent</c> and <c>--log</c> apply, and a
///         failure is reported as <c>Error: usage-error: {message}</c> and <c>Hint: {hint}</c> on standard error, as for
///         every other command.
///     </para>
/// </remarks>
internal sealed partial class CommandRegistry
{
    /// <summary>
    ///     The column at which the generated help text of a command is wrapped.
    /// </summary>
    private const int TextWidth = 78;

    /// <summary>
    ///     The kind of the index document, which the tool extends with its own fields.
    /// </summary>
    private const string IndexKind = "index";

    /// <summary>
    ///     The kind of the document of one example, whose <c>id</c> the tool sets to the card that was asked for.
    /// </summary>
    private const string ExampleKind = "example";

    /// <summary>
    ///     The hint of a help failure that HelpCards reports without one (an option without its value, <c>--example</c>
    ///     or <c>--examples</c> without a card path, or two options that cannot be combined): where the help options
    ///     and the help cards are listed.
    /// </summary>
    private const string HelpOptionsHint =
        "Run 'canvasnet help' for the help options, or 'canvasnet help topics' to list the help cards.";

    /// <summary>
    ///     The help options that take a value, as HelpCards reads them; their value may also follow an equals sign.
    /// </summary>
    private static readonly string[] HelpValueOptions = ["--example", "--search", "--tag", "--source", "--format", "--width"];

    /// <summary>
    ///     The usage lines of the tool, shown at the top of the index.
    /// </summary>
    private static readonly string[] UsageLines =
    [
        "canvasnet <command> [arguments] [options]",
        "canvasnet [options]",
        "canvasnet help [<command> | <topic> [<card> ...] | topics] [<help options>] [--json]"
    ];

    /// <summary>
    ///     The options of the <c>help</c> command, besides the global options, as the index lists them. HelpCards
    ///     parses them.
    /// </summary>
    private static readonly (string Label, string Description)[] HelpOptionLines =
    [
        ("--examples", "Print every example of the card in full, and list the cards below it that have examples"),
        ("--example <name>", "Print only the source (or command line) of one example of the card or a card below it"),
        ("--search <text>", "Find cards by their titles, summaries, aliases, tags, and example names"),
        ("--tag <tag>", "List the cards that have the tag (repeatable)"),
        ("--source <name>", "Limit the index, --search, and --tag to canvasnet-viewer or canvasnet-tool"),
        ("--all", "List every card in the index, not only the topics"),
        ("--format <format>", "text (default), markdown, or json (the same as --json)"),
        ("--width <columns>", "Wrap the generated text at 20 to 500 columns, or auto (default 78)")
    ];

    /// <summary>
    ///     Runs the <c>help</c> command with the arguments that follow it on the command line.
    /// </summary>
    /// <param name="context">The context holding the arguments of <c>help</c> and receiving its output.</param>
    private void RunHelp(Context context)
    {
        RunHelp(context, context.CommandArguments);
    }

    /// <summary>
    ///     Runs the help command: prints the help of a subject, the examples of a card in full, one example alone, a
    ///     search, or a tag list to the context.
    /// </summary>
    /// <param name="context">The context receiving the output; its <c>--json</c> selects JSON.</param>
    /// <param name="arguments">
    ///     The words and options after <c>help</c> (a leading <c>help</c> is ignored, so <c>help help</c> is the index),
    ///     or the command or topic named with <c>--help</c>.
    /// </param>
    /// <remarks>
    ///     A failure that HelpCards reports (an unknown card or example below a topic, or a wrong option) is written to
    ///     standard error as <c>Error: usage-error: ...</c> and <c>Hint: ...</c>, and with <c>--json</c> or
    ///     <c>--format json</c> also as the failure document on standard output; the context then exits with 1. A failure
    ///     of a subject that the tool answers itself is reported the same way when JSON was asked only with
    ///     <c>--format json</c>.
    /// </remarks>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> for an unknown first word, or for more words after a command,
    ///     <c>topics</c>, or <c>help</c>, unless JSON was asked only with <c>--format json</c> (then it is reported as
    ///     the failure document instead).
    /// </exception>
    internal void RunHelp(Context context, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(context);

        var run = ExecuteHelp(arguments, context.Json);
        if (run.ExitCode != 0)
        {
            context.WriteError($"Error: {run.ErrorCode}: {run.ErrorMessage}");
            if (run.ErrorHint != null)
            {
                context.WriteError($"Hint: {run.ErrorHint}");
            }
        }

        if (run.Output.Length > 0)
        {
            context.WriteLine(run.Output);
        }
    }

    /// <summary>
    ///     Renders help text for one subject.
    /// </summary>
    /// <param name="subject">
    ///     The name or alias of a command or a help topic to describe, <c>topics</c> for the whole tree of help cards,
    ///     or <see langword="null"/> (or the reserved name <c>help</c>) for the index: usage, the command list, the
    ///     options, and the topic list.
    /// </param>
    /// <param name="json">Whether to render a JSON document instead of plain text.</param>
    /// <returns>The help text or document, without a trailing newline.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when <paramref name="subject"/> is unknown.</exception>
    public string RenderHelp(string? subject = null, bool json = false)
    {
        return RenderHelp(subject == null ? Array.Empty<string>() : [subject], json);
    }

    /// <summary>
    ///     Renders help text for a help path, with the help options when there are any.
    /// </summary>
    /// <param name="path">
    ///     The words of the path and the help options: empty (or the reserved name <c>help</c>) for the index,
    ///     <c>topics</c> for the whole tree, a command name or alias for the help of the command, or a card path such as
    ///     <c>mermaid gantt</c>.
    /// </param>
    /// <param name="json">Whether to render a JSON document instead of plain text.</param>
    /// <returns>The help text or document, without a trailing newline.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with the code, message, and hint of the failure when the path or an option is wrong.
    /// </exception>
    public string RenderHelp(IReadOnlyList<string> path, bool json = false)
    {
        var run = ExecuteHelp(path, json);
        if (run.ExitCode != 0)
        {
            throw new ToolException(run.ErrorCode ?? ErrorCodes.UsageError, run.ErrorMessage ?? "Help failed.", run.ErrorHint)
            {
                Command = HelpCommandName
            };
        }

        return run.Output;
    }

    /// <summary>
    ///     Runs the help command on HelpCards and captures what it writes.
    /// </summary>
    /// <param name="arguments">The words and options after <c>help</c>. Must not be null.</param>
    /// <param name="json">Whether the global <c>--json</c> option was given.</param>
    /// <returns>The exit code (0 or 1), the output, and the error of a failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
    /// <exception cref="ToolException">
    ///     Thrown by <see cref="ResolveOwnSubject"/> for an unknown first word, or for more words after a command,
    ///     <c>topics</c>, or <c>help</c>, with the global <c>--json</c> or in text; when JSON was asked only with
    ///     <c>--format json</c> the failure is returned as the failure document instead, because <c>Program</c> writes
    ///     that document only for the global <c>--json</c>.
    /// </exception>
    /// <remarks>
    ///     Each run creates its own helper with its own writers, so runs are independent and may be concurrent; the
    ///     catalog is shared (<see cref="HelpTopics.Catalog"/>). A help option value may follow its option as the next
    ///     word or after an equals sign (<c>--example=shapes</c>), the <c>id</c> of the example document is the card
    ///     that was asked for, and the examples of a command, <c>topics</c>, or <c>help</c> fail with the message that
    ///     only help cards have examples, as before the move to HelpCards. A failure that HelpCards reports without a
    ///     hint (some of its argument errors) gets a hint that points at the help options and the help cards
    ///     (<see cref="HelpOptionsHint"/>).
    /// </remarks>
    internal HelpRun ExecuteHelp(IReadOnlyList<string> arguments, bool json)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // A leading 'help' names the index again ('help help'); the global --json, which the context already removed,
        // asks HelpCards for JSON
        var words = new List<string>(arguments);
        if (words.Count > 0 && string.Equals(words[0], HelpCommandName, StringComparison.OrdinalIgnoreCase))
        {
            words.RemoveAt(0);
        }

        if (json)
        {
            words.Insert(0, "--json");
        }

        // HelpCards reads an option value only as the next word, so the '--name=value' form of the tool is split first
        words = SplitInlineValues(words);

        // Run the helper with writers of this run, recording the error of a JSON failure and a defective catalog
        var jsonRequested = IsJsonRequested(words);
        HelpCliError? jsonError = null;
        var catalogFailed = false;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var cli = HelpCli.Create(new HelpCliOptions
        {
            ToolName = HelpTopics.ToolName,
            CatalogFactory = _ =>
            {
                try
                {
                    return HelpTopics.Catalog;
                }
                catch (HelpCatalogException)
                {
                    catalogFailed = true;
                    throw;
                }
            },
            CommandTemplate = HelpTopics.CommandTemplate,
            IndexPreamble = CreateIndexPreamble(),
            IndexSections = CreateIndexSections(),
            FallbackResolver = path => ResolveOwnSubject(path, jsonRequested),
            Output = output,
            Error = error,
            JsonEnvelope = (kind, fields) => kind switch
            {
                IndexKind => HelpTopics.Envelope(kind, fields, WriteIndexFields),
                ExampleKind when FindRequestedCardId(words) is { } id =>
                    HelpTopics.Envelope(kind, writer => WriteWithId(writer, fields, id), null),
                _ => HelpTopics.Envelope(kind, fields, null)
            },
            JsonError = (writer, failure) =>
            {
                // HelpCards gives some argument errors no hint; the tool gives every mistake one, as it always did
                jsonError = failure.Hint != null || failure.Code == "catalog-error"
                    ? failure
                    : failure with { Hint = HelpOptionsHint };
                HelpTopics.WriteError(writer, jsonError);
            },
            ExitCodes = new HelpCliExitCodes { UsageError = 1, InternalError = 1 }
        });
        int exitCode;
        try
        {
            exitCode = cli.Run(words);
        }
        catch (ToolException ex) when (jsonRequested && !json)
        {
            // Program writes the failure document only for the global --json, so with '--format json' alone the failure
            // of a subject the tool answers itself is returned as that document here
            return new HelpRun(1, ResultWriter.Failure(HelpCommandName, ex), ex.Code, ex.Message, ex.Hint);
        }

        var text = output.ToString().TrimEnd('\n');
        if (exitCode == 0)
        {
            return new HelpRun(0, text, null, null, null);
        }

        // A JSON failure carries the error of HelpCards; a text failure is the documented 'Error: {message}' line with
        // an optional hint line
        string code;
        string message;
        string? hint;
        if (jsonError != null)
        {
            (code, message, hint) = (HelpTopics.ErrorCode(jsonError.Code), jsonError.Message, jsonError.Hint);
        }
        else
        {
            code = catalogFailed ? ErrorCodes.OperationFailed : ErrorCodes.UsageError;
            (message, hint) = SplitTextError(error.ToString());

            // HelpCards gives some argument errors no hint; the tool gives every mistake one, as it always did
            if (!catalogFailed)
            {
                hint ??= HelpOptionsHint;
            }
        }

        // The examples of a command, 'topics', or 'help' keep the tool's own message: only help cards have examples
        if (!catalogFailed && CreateNoExamplesError(words, message) is { } noExamples)
        {
            return new HelpRun(
                exitCode,
                jsonRequested ? ResultWriter.Failure(HelpCommandName, noExamples) : text,
                noExamples.Code,
                noExamples.Message,
                noExamples.Hint);
        }

        return new HelpRun(exitCode, text, code, message, hint);
    }

    /// <summary>
    ///     Creates the error of the examples of a subject that has none: a command, <c>topics</c>, or <c>help</c>.
    /// </summary>
    /// <param name="words">The words of the help command, with the option values already split off.</param>
    /// <param name="message">The message of the failure that HelpCards reported.</param>
    /// <returns>
    ///     The exception with the tool's message (<c>'{subject}' has no examples; only help cards do.</c>) when the first
    ///     path word is a command, <c>topics</c>, or <c>help</c> and HelpCards reported exactly that it is an unknown help
    ///     card; otherwise <see langword="null"/>, so that the failure of HelpCards stands.
    /// </returns>
    /// <remarks>
    ///     HelpCards answers <c>--example</c> and <c>--examples</c> from the catalog only, so it reports a command as an
    ///     unknown card; it reports a wrong option or combination first, so those failures are never replaced. A
    ///     different wording in a later HelpCards keeps its own failure.
    /// </remarks>
    private ToolException? CreateNoExamplesError(IReadOnlyList<string> words, string message)
    {
        var segments = Segments(PathWords(words));
        if (segments.Count == 0)
        {
            return null;
        }

        var subject = segments[0];
        if (!IsOwnSubject(subject)
            || !string.Equals(message, $"Unknown help card '{subject}'.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ToolException(
            ErrorCodes.UsageError,
            $"'{subject}' has no examples; only help cards do.",
            $"Run 'canvasnet help {subject}' for its usage, or 'canvasnet help topics' to list the help cards.")
        {
            Command = HelpCommandName
        };
    }

    /// <summary>
    ///     Splits the path words of a help command into their segments: at a slash, a space, or a tab, trimmed, without
    ///     the empty ones.
    /// </summary>
    /// <param name="path">The path words.</param>
    /// <returns>The segments, in order.</returns>
    private static List<string> Segments(IEnumerable<string> path)
    {
        return path
            .SelectMany(p => p.Split(['/', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
    }

    /// <summary>
    ///     Tells whether a first path segment is a subject that the tool answers itself: <c>help</c>, <c>topics</c>, or
    ///     the name or alias of a command.
    /// </summary>
    /// <param name="subject">The segment.</param>
    /// <returns><see langword="true"/> for a subject of the tool.</returns>
    private bool IsOwnSubject(string subject)
    {
        return string.Equals(subject, HelpCommandName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(subject, HelpTopics.TopicsSubject, StringComparison.OrdinalIgnoreCase)
            || Find(subject) != null;
    }

    /// <summary>
    ///     Splits the text error of HelpCards (<c>Error: {message}</c>, then an optional line with the hint) into its
    ///     message and hint.
    /// </summary>
    /// <param name="text">The text written to the error writer.</param>
    /// <returns>The message and the hint (<see langword="null"/> when there is none).</returns>
    internal static (string Message, string? Hint) SplitTextError(string text)
    {
        const string Prefix = "Error: ";
        var lines = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n', 2);
        var message = lines[0].StartsWith(Prefix, StringComparison.Ordinal) ? lines[0][Prefix.Length..] : lines[0];
        var hint = lines.Length > 1 && lines[1].Length > 0 ? lines[1] : null;
        return (message, hint);
    }

    /// <summary>
    ///     Tells whether the words of a help command ask for JSON: <c>--json</c> or <c>--format json</c> (the value in
    ///     any case) before <c>--</c>, as HelpCards reads them.
    /// </summary>
    /// <param name="words">The words of the help command.</param>
    /// <returns><see langword="true"/> when the answer is JSON.</returns>
    private static bool IsJsonRequested(IReadOnlyList<string> words)
    {
        for (var i = 0; i < words.Count && words[i] != "--"; i++)
        {
            if (words[i] == "--json"
                || (words[i] == "--format"
                    && i + 1 < words.Count
                    && string.Equals(words[i + 1], "json", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Splits each help option written as <c>--name=value</c> into the two words <c>--name</c> and <c>value</c>, as
    ///     HelpCards reads them.
    /// </summary>
    /// <param name="words">The words of the help command.</param>
    /// <returns>
    ///     The words, with each word before <c>--</c> that is a help option that takes a value (see
    ///     <see cref="HelpValueOptions"/>) followed by an equals sign split at that sign; every other word, a flag with a
    ///     value included (which HelpCards then reports as an unknown option), and every word from <c>--</c> on are
    ///     copied unchanged.
    /// </returns>
    internal static List<string> SplitInlineValues(IReadOnlyList<string> words)
    {
        var result = new List<string>(words.Count);
        var optionsEnded = false;
        foreach (var word in words)
        {
            optionsEnded |= word == "--";
            var equals = optionsEnded || !word.StartsWith("--", StringComparison.Ordinal)
                ? -1
                : word.IndexOf('=', StringComparison.Ordinal);
            if (equals > 2 && HelpValueOptions.Contains(word[..equals], StringComparer.Ordinal))
            {
                result.Add(word[..equals]);
                result.Add(word[(equals + 1)..]);
            }
            else
            {
                result.Add(word);
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the words of the card path of a help command, as HelpCards reads them: the words that are not options or
    ///     option values, and every word after <c>--</c>.
    /// </summary>
    /// <param name="words">The words of the help command, with the option values already split off.</param>
    /// <returns>The path words, trimmed, without the empty ones.</returns>
    private static List<string> PathWords(IReadOnlyList<string> words)
    {
        var path = new List<string>();
        var optionsEnded = false;
        var skipValue = false;
        foreach (var word in words)
        {
            if (skipValue)
            {
                skipValue = false;
            }
            else if (optionsEnded || word.Length < 2 || word[0] != '-')
            {
                path.Add(word);
            }
            else if (word == "--")
            {
                optionsEnded = true;
            }
            else
            {
                skipValue = HelpValueOptions.Contains(word, StringComparer.Ordinal);
            }
        }

        return path.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList();
    }

    /// <summary>
    ///     Finds the card that a help command asks for, as HelpCards resolves its path words.
    /// </summary>
    /// <param name="words">The words of the help command, with the option values already split off.</param>
    /// <returns>The id of the card, or <see langword="null"/> when the path names no card.</returns>
    private static string? FindRequestedCardId(IReadOnlyList<string> words)
    {
        return HelpTopics.Catalog.Find(string.Join(' ', PathWords(words)))?.Id;
    }

    /// <summary>
    ///     Writes the fields of a HelpCards document with <c>id</c> set to another value, keeping their order.
    /// </summary>
    /// <param name="writer">The JSON writer, inside the open document object.</param>
    /// <param name="fields">Writes the fields of the document into an open object.</param>
    /// <param name="id">The value of <c>id</c>.</param>
    /// <remarks>
    ///     HelpCli writes the document of one example with the <c>id</c> of the card that holds the example, while the
    ///     tool's contract (and HelpCards' own documentation of the document) gives the card that was asked for; the
    ///     example object still names the card that holds it.
    /// </remarks>
    private static void WriteWithId(Utf8JsonWriter writer, Action<Utf8JsonWriter> fields, string id)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var temporary = new Utf8JsonWriter(buffer))
        {
            temporary.WriteStartObject();
            fields(temporary);
            temporary.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("id"))
            {
                writer.WriteString("id", id);
            }
            else
            {
                property.WriteTo(writer);
            }
        }
    }

    /// <summary>
    ///     Answers the help subjects that are not cards, before HelpCards searches the catalog.
    /// </summary>
    /// <param name="path">The path words of a plain help command (no <c>--example</c>, <c>--examples</c>, <c>--search</c>, or <c>--tag</c>).</param>
    /// <param name="json">Whether the answer is JSON.</param>
    /// <returns>
    ///     The help of a command or alias, or the whole tree for <c>topics</c>; <see langword="null"/> for a topic, so
    ///     that HelpCards answers with the card.
    /// </returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the first word is neither a command, <c>topics</c>, nor a topic (the
    ///     hint suggests the closest name and lists the valid subjects), or when more words follow a command,
    ///     <c>topics</c>, or <c>help</c>.
    /// </exception>
    private HelpCliAnswer? ResolveOwnSubject(IReadOnlyList<string> path, bool json)
    {
        var segments = Segments(path);
        if (segments.Count == 0)
        {
            return null;
        }

        // Commands, the tree, and the help command itself have no cards below them
        var subject = segments[0];
        var isHelp = string.Equals(subject, HelpCommandName, StringComparison.OrdinalIgnoreCase);
        var isTopics = string.Equals(subject, HelpTopics.TopicsSubject, StringComparison.OrdinalIgnoreCase);
        var command = isHelp || isTopics ? null : Find(subject);
        if (isHelp || (segments.Count > 1 && IsOwnSubject(subject)))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{subject}' has no help cards below it.",
                $"Run 'canvasnet help {subject}' on its own, or 'canvasnet help topics' to list the help cards.")
            {
                Command = HelpCommandName
            };
        }

        if (isTopics)
        {
            return new HelpCliAnswer(HelpTopics.RenderTopics(json));
        }

        if (command != null)
        {
            return new HelpCliAnswer(json ? RenderCommandJson(command) : RenderCommandText(command));
        }

        // A topic is a card; anything else is not a subject
        return HelpTopics.Find(subject) != null ? null : throw CreateUnknownSubjectException(subject);
    }

    /// <summary>
    ///     Creates the text of the index between the usage and the sections: the usage lines and where to go next.
    /// </summary>
    /// <returns>The text.</returns>
    private static string CreateIndexPreamble()
    {
        var builder = new StringBuilder();
        builder.Append("Usage: ").Append(UsageLines[0]).Append('\n');
        foreach (var line in UsageLines.Skip(1))
        {
            builder.Append("       ").Append(line).Append('\n');
        }

        builder.Append('\n');
        AppendWrapped(
            builder,
            "Run 'canvasnet <command> --help' for help on a command and 'canvasnet help topics' to list every help card. "
            + "For good-looking diagrams start with 'canvasnet help style' and 'canvasnet help composition'.",
            string.Empty,
            string.Empty);
        return builder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    /// <summary>
    ///     Creates the sections of the index: the commands, the global options, and the help options.
    /// </summary>
    /// <returns>The sections, each line aligned in two columns.</returns>
    private List<HelpCliSection> CreateIndexSections()
    {
        var sections = new List<HelpCliSection>();
        if (_commands.Count > 0)
        {
            sections.Add(new HelpCliSection(
                "Commands:",
                AlignColumns(_commands.Select(c => (string.Join(", ", c.Aliases.Prepend(c.Name)), c.Summary)).ToList())));
        }

        sections.Add(new HelpCliSection("Options:", AlignColumns(OptionRows(GlobalOptions))));
        sections.Add(new HelpCliSection("Help options:", AlignColumns(HelpOptionLines)));
        return sections;
    }

    /// <summary>
    ///     Writes the tool's own fields of the index document, before those of HelpCards: <c>usage</c>, <c>commands</c>,
    ///     <c>topics</c>, and <c>globalOptions</c>.
    /// </summary>
    /// <param name="writer">The JSON writer, inside the open document object.</param>
    private void WriteIndexFields(Utf8JsonWriter writer)
    {
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
        AppendWrapped(builder, SeeAlso, string.Empty, "  ");
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
        foreach (var line in AlignColumns(OptionRows(options)))
        {
            builder.AppendLine($"  {line}");
        }
    }

    /// <summary>
    ///     Gets the label (<c>alias, --name &lt;value&gt;</c>) and description of each option.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The rows, in order.</returns>
    private static List<(string Label, string Description)> OptionRows(IReadOnlyList<OptionSpec> options)
    {
        return options.Select(o =>
        {
            var names = string.Join(", ", o.Aliases.Append(o.Name));
            return (o.TakesValue ? $"{names} <{o.ValueName ?? "value"}>" : names, o.Description);
        }).ToList();
    }

    /// <summary>
    ///     Aligns rows of a label and a description in two columns, two spaces apart.
    /// </summary>
    /// <param name="rows">The rows; at least one.</param>
    /// <returns>One line per row, without an indent.</returns>
    private static List<string> AlignColumns(IReadOnlyList<(string Label, string Description)> rows)
    {
        var width = rows.Max(r => r.Label.Length);
        return rows.Select(r => $"{r.Label.PadRight(width)}  {r.Description}").ToList();
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
    private static void AppendWrapped(StringBuilder builder, string text, string firstPrefix, string restPrefix)
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
    ///     Creates the exception reported for an unknown help subject: it suggests the closest name and lists the
    ///     valid subjects.
    /// </summary>
    /// <param name="subject">The unknown subject as typed.</param>
    /// <returns>The exception to throw.</returns>
    private ToolException CreateUnknownSubjectException(string subject)
    {
        var names = _commands.SelectMany(c => c.Aliases.Prepend(c.Name))
            .Concat(HelpTopics.Roots.SelectMany(t => t.Aliases.Prepend(t.Name)))
            .Append(HelpTopics.TopicsSubject)
            .Append(HelpCommandName);
        var suggestion = ClosestName(subject, names);

        var valid = string.Join(
            ", ",
            _commands.Select(c => c.Name).Concat(HelpTopics.TopicNames).Append(HelpTopics.TopicsSubject).Append(HelpCommandName));
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

    /// <summary>
    ///     The result of one run of the help command.
    /// </summary>
    /// <param name="ExitCode">0 for success, 1 for a failure.</param>
    /// <param name="Output">What was written to standard output (a JSON failure document included), without a trailing newline.</param>
    /// <param name="ErrorCode">The tool's error code of a failure, or <see langword="null"/> on success.</param>
    /// <param name="ErrorMessage">The message of a failure, or <see langword="null"/> on success.</param>
    /// <param name="ErrorHint">The hint of a failure, or <see langword="null"/> when there is none.</param>
    internal sealed record HelpRun(int ExitCode, string Output, string? ErrorCode, string? ErrorMessage, string? ErrorHint);
}
