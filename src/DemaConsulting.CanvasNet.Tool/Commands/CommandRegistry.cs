// cspell:ignore canvasnet
using System.Text;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The table of commands the tool offers, with command resolution, dispatch, and help rendering.
/// </summary>
/// <remarks>
///     The registry is an instance (not static) so that tests can build a registry containing a
///     purpose-made command, for example one that throws an unexpected exception to prove that the
///     entry point re-throws it. The name <c>help</c> is reserved: it prints the top-level help, or the
///     help of the command named by its first argument.
/// </remarks>
internal sealed class CommandRegistry
{
    /// <summary>
    ///     The reserved name of the built-in help command.
    /// </summary>
    private const string HelpCommandName = "help";

    /// <summary>
    ///     The largest edit distance at which a command name is suggested for an unknown command.
    /// </summary>
    private const int MaxSuggestionDistance = 2;

    /// <summary>
    ///     The global options, shown in every help screen.
    /// </summary>
    private static readonly OptionSpec[] GlobalOptions =
    [
        new("--version", ["-v"], false, "Display version information"),
        new("--help", ["-?", "-h"], false, "Display this help message"),
        new("--silent", [], false, "Suppress console output"),
        new("--validate", [], false, "Run self-validation"),
        new("--json", [], false, "Write the result as one JSON document to standard output"),
        new("--results", ["--result"], true, "Write validation results to file (.trx or .xml)", "file"),
        new("--depth", [], true, "Set heading depth for markdown output (default: 1)", "#"),
        new("--log", [], true, "Write output to log file", "file")
    ];

    /// <summary>
    ///     The registered commands in registration order.
    /// </summary>
    private readonly List<ICommand> _commands = [];

    /// <summary>
    ///     Gets the registered commands in registration order.
    /// </summary>
    public IReadOnlyList<ICommand> Commands => _commands;

    /// <summary>
    ///     Creates the registry containing every command the tool ships with.
    /// </summary>
    /// <returns>A new registry.</returns>
    /// <remarks>
    ///     The set of shipped commands grows as commands are added to the tool; a command is added to
    ///     this method in the change that implements it.
    /// </remarks>
    public static CommandRegistry CreateDefault()
    {
        var registry = new CommandRegistry();
        registry.Register(new DiscoveryCommands.Formats());
        return registry;
    }

    /// <summary>
    ///     Registers a command.
    /// </summary>
    /// <param name="command">The command to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the command's name or one of its aliases is already registered, is repeated within
    ///     the command, or is the reserved name <c>help</c>.
    /// </exception>
    public void Register(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Every name the command answers to must be unique across the registry and within the command
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in command.Aliases.Prepend(command.Name))
        {
            if (string.Equals(name, HelpCommandName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The command name '{name}' is reserved.");
            }

            if (!seen.Add(name) || Find(name) != null)
            {
                throw new InvalidOperationException($"The command name '{name}' is already registered.");
            }
        }

        _commands.Add(command);
    }

    /// <summary>
    ///     Finds a command by its name or one of its aliases.
    /// </summary>
    /// <param name="nameOrAlias">The command name or alias; matched ignoring case.</param>
    /// <returns>The matching command, or <see langword="null"/> when there is none.</returns>
    public ICommand? Find(string nameOrAlias)
    {
        return _commands.FirstOrDefault(c =>
            string.Equals(c.Name, nameOrAlias, StringComparison.OrdinalIgnoreCase)
            || c.Aliases.Any(a => string.Equals(a, nameOrAlias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Runs the command selected on the command line of <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The context holding the command name and the command arguments.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when no command was given or the command is unknown (the
    ///     message suggests the closest command name), and with the command's own code when the command
    ///     fails; <see cref="ToolException.Command"/> is filled in when the command left it empty.
    /// </exception>
    /// <remarks>
    ///     Any other exception thrown by a command propagates unchanged: it is a defect, and the entry
    ///     point reports and re-throws it.
    /// </remarks>
    public void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A command name is required to dispatch anything
        var name = context.Command
                   ?? throw new ToolException(
                       ErrorCodes.UsageError,
                       "No command was specified.",
                       "Run 'canvasnet --help' to list the available commands.");

        // The built-in help command prints help for the command named by its first argument
        if (string.Equals(name, HelpCommandName, StringComparison.OrdinalIgnoreCase))
        {
            context.WriteLine(RenderHelp(context.CommandArguments.FirstOrDefault()));
            return;
        }

        // Resolve the command, suggesting the closest name when it is unknown
        var command = Find(name) ?? throw CreateUnknownCommandException(name);

        try
        {
            command.Run(context, context.CommandArguments);
        }
        catch (ToolException ex)
        {
            // Record which command failed so the error report can name it, then let it propagate
            ex.Command ??= command.Name;
            throw;
        }
    }

    /// <summary>
    ///     Renders help text.
    /// </summary>
    /// <param name="command">
    ///     The name or alias of the command to describe, or <see langword="null"/> (or the reserved name
    ///     <c>help</c>) for the top-level help (the command list and the global options).
    /// </param>
    /// <returns>The help text, without a trailing newline.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when <paramref name="command"/> is unknown.</exception>
    public string RenderHelp(string? command = null)
    {
        return command == null || string.Equals(command, HelpCommandName, StringComparison.OrdinalIgnoreCase)
            ? RenderTopLevelHelp()
            : RenderCommandHelp(command);
    }

    /// <summary>
    ///     Renders the top-level help: usage, the command list, and the global options.
    /// </summary>
    /// <returns>The help text.</returns>
    private string RenderTopLevelHelp()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Usage: canvasnet <command> [arguments] [options]");
        builder.AppendLine("       canvasnet [options]");

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

        builder.AppendLine();
        builder.AppendLine("Options:");
        AppendOptions(builder, GlobalOptions);
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     Renders the help of one command: usage, summary, its options, and the global options.
    /// </summary>
    /// <param name="name">The command name or alias.</param>
    /// <returns>The help text.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the command is unknown.</exception>
    private string RenderCommandHelp(string name)
    {
        var command = Find(name) ?? throw CreateUnknownCommandException(name);

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
    ///     Creates the exception reported for an unknown command, suggesting the closest known name.
    /// </summary>
    /// <param name="name">The unknown command name as typed by the user.</param>
    /// <returns>The exception to throw.</returns>
    private ToolException CreateUnknownCommandException(string name)
    {
        // Suggest the closest command name or alias, when one is near enough to be a likely typo
        var candidates = _commands.SelectMany(c => c.Aliases.Prepend(c.Name)).Append(HelpCommandName);
        var suggestion = candidates
            .Select(c => (Name: c, Distance: EditDistance(name.ToLowerInvariant(), c.ToLowerInvariant())))
            .Where(c => c.Distance <= MaxSuggestionDistance)
            .OrderBy(c => c.Distance)
            .Select(c => c.Name)
            .FirstOrDefault();

        var hint = suggestion != null
            ? $"Did you mean '{suggestion}'? Run 'canvasnet --help' to list the available commands."
            : "Run 'canvasnet --help' to list the available commands.";
        return new ToolException(ErrorCodes.UsageError, $"Unknown command '{name}'.", hint) { Command = name };
    }

    /// <summary>
    ///     Computes the Levenshtein edit distance between two strings.
    /// </summary>
    /// <param name="a">The first string.</param>
    /// <param name="b">The second string.</param>
    /// <returns>The minimum number of single-character insertions, deletions, and substitutions turning <paramref name="a"/> into <paramref name="b"/>.</returns>
    /// <remarks>
    ///     Shared with <see cref="ArgumentReader"/>, which uses it to suggest the closest option.
    /// </remarks>
    internal static int EditDistance(string a, string b)
    {
        // Classic two-row dynamic-programming formulation
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
