// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     One option occurrence found on a command line.
/// </summary>
/// <param name="Name">The canonical option name (<see cref="OptionSpec.Name"/>), even when an alias was typed.</param>
/// <param name="Value">The option's value, or <see langword="null"/> for a flag.</param>
internal readonly record struct ParsedOption(string Name, string? Value);

/// <summary>
///     A length that is either an absolute number of pixels or a percentage of the image's size.
/// </summary>
/// <param name="Value">The number: pixels when <see cref="IsPercent"/> is <see langword="false"/>, percent (0-100) otherwise.</param>
/// <param name="IsPercent"><see langword="true"/> when <paramref name="Value"/> is a percentage.</param>
internal readonly record struct Dimension(double Value, bool IsPercent);

/// <summary>
///     A rectangle given as an origin and a size, each component in pixels or percent.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <remarks>
///     Percentages of <paramref name="X"/> and <paramref name="Width"/> are relative to the image width;
///     percentages of <paramref name="Y"/> and <paramref name="Height"/> to the image height.
/// </remarks>
internal readonly record struct GeometrySpec(Dimension X, Dimension Y, Dimension Width, Dimension Height);

/// <summary>
///     The widths of a border, in pixels, on each side of an image.
/// </summary>
/// <param name="Left">The left border width.</param>
/// <param name="Top">The top border width.</param>
/// <param name="Right">The right border width.</param>
/// <param name="Bottom">The bottom border width.</param>
internal readonly record struct PadSpec(int Left, int Top, int Right, int Bottom);

/// <summary>
///     The axis (or axes) an image is mirrored about.
/// </summary>
internal enum FlipAxis
{
    /// <summary>Mirror left to right (about the vertical axis).</summary>
    Horizontal,

    /// <summary>Mirror top to bottom (about the horizontal axis).</summary>
    Vertical,

    /// <summary>Mirror about both axes, which is a rotation by 180 degrees.</summary>
    Both
}

/// <summary>
///     Reads the options and positional arguments of one command's argument list, and parses the typed
///     values that the options carry.
/// </summary>
/// <remarks>
///     <para>
///         The reader is driven by the command's <see cref="OptionSpec"/> table, so the options a command
///         accepts and the help it prints come from the same declaration. An option is written
///         <c>--name value</c> or <c>--name=value</c>; a short alias is written <c>-o value</c>, and an alias
///         of a single letter may also carry its value attached (<c>-Dname=value</c>). A value may start with
///         a dash (<c>--azimuth -45</c>). A flag is written without a value. An option may be repeated; the
///         reader keeps every occurrence, in command-line order, in <see cref="Ordered"/>, so a command can
///         apply repeated or interleaved options as a pipeline. A token that does not start with a dash (or
///         is a lone <c>-</c>, or looks like a negative number), and every token after a bare <c>--</c>, is a
///         positional argument.
///     </para>
///     <para>
///         The value grammars (size, geometry, color, degrees, flip, padding, dots per inch, page, and
///         <c>-D name=value</c> literals) are static methods in <c>ArgumentReader.Values.cs</c> so that commands
///         can parse option values and positional operands with the same rules. Every parse failure is a
///         <see cref="ToolException"/> with the code <see cref="ErrorCodes.UsageError"/>.
///     </para>
/// </remarks>
internal sealed partial class ArgumentReader
{
    /// <summary>
    ///     The shortest option name (including its dashes) that is offered as a suggestion for a mistyped option.
    /// </summary>
    private const int MinSuggestionLength = 3;

    /// <summary>
    ///     The largest edit distance at which an option is suggested for an unknown option.
    /// </summary>
    private const int MaxSuggestionDistance = 2;

    /// <summary>
    ///     The occurrences of the options, in command-line order.
    /// </summary>
    private readonly List<ParsedOption> _ordered = [];

    /// <summary>
    ///     The positional arguments, in command-line order.
    /// </summary>
    private readonly List<string> _positionals = [];

    /// <summary>
    ///     The canonical names of the options the command declares.
    /// </summary>
    private readonly HashSet<string> _knownNames = new(StringComparer.Ordinal);

    /// <summary>
    ///     The name of the command the arguments belong to, for messages.
    /// </summary>
    private readonly string _command;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArgumentReader"/> class.
    /// </summary>
    /// <param name="command">The command name, used in messages.</param>
    private ArgumentReader(string command)
    {
        _command = command;
    }

    /// <summary>
    ///     Gets the positional arguments in command-line order.
    /// </summary>
    public IReadOnlyList<string> Positionals => _positionals;

    /// <summary>
    ///     Gets every option occurrence in command-line order, naming each option by its canonical name.
    /// </summary>
    public IReadOnlyList<ParsedOption> Ordered => _ordered;

    /// <summary>
    ///     Parses a command's arguments.
    /// </summary>
    /// <param name="command">The canonical name of the command, used in messages.</param>
    /// <param name="args">The command arguments: everything after the command name except the global options.</param>
    /// <param name="specs">The options the command accepts.</param>
    /// <returns>The reader holding the parsed options and positional arguments.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> for an unknown option (with a suggestion when one is close), a
    ///     missing option value, or a value given to a flag.
    /// </exception>
    public static ArgumentReader Parse(string command, IReadOnlyList<string> args, IReadOnlyList<OptionSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(specs);

        var reader = new ArgumentReader(command);
        foreach (var spec in specs)
        {
            reader._knownNames.Add(spec.Name);
        }

        reader.ReadTokens(args, specs);
        return reader;
    }

    /// <summary>
    ///     Gets a value indicating whether an option occurred at least once.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <returns><see langword="true"/> when the option was given.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the command does not declare the option (a defect of the caller).</exception>
    public bool Has(string name)
    {
        RequireKnown(name);
        return _ordered.Any(o => o.Name == name);
    }

    /// <summary>
    ///     Gets the value of an option; when the option was repeated, the value of its last occurrence.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <returns>The value, or <see langword="null"/> when the option was not given (or is a flag).</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the command does not declare the option (a defect of the caller).</exception>
    public string? Get(string name)
    {
        RequireKnown(name);
        return _ordered.LastOrDefault(o => o.Name == name).Value;
    }

    /// <summary>
    ///     Gets the values of every occurrence of an option, in command-line order.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <returns>The values; empty when the option was not given.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the command does not declare the option (a defect of the caller).</exception>
    public IReadOnlyList<string> GetAll(string name)
    {
        RequireKnown(name);
        return _ordered.Where(o => o.Name == name && o.Value != null).Select(o => o.Value!).ToList();
    }

    /// <summary>
    ///     Gets the value of an option as an integer within a range.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <returns>The value, or <see langword="null"/> when the option was not given.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the value is not an integer within the range.</exception>
    public int? GetInt(string name, int min, int max)
    {
        var text = Get(name);
        return text == null ? null : ParseInt(text, name, min, max);
    }

    /// <summary>
    ///     Gets the value of an option as a finite number within a range.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <returns>The value, or <see langword="null"/> when the option was not given.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the value is not a number within the range.</exception>
    public double? GetDouble(string name, double min, double max)
    {
        var text = Get(name);
        return text == null ? null : ParseNumber(text, name, min, max);
    }

    /// <summary>
    ///     Requires the number of positional arguments to be within a range.
    /// </summary>
    /// <param name="min">The smallest accepted count.</param>
    /// <param name="max">The largest accepted count.</param>
    /// <param name="expected">A description of the expected arguments, for the message, for example <c>an input file</c>.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the count is outside the range.</exception>
    public void RequirePositionals(int min, int max, string expected)
    {
        if (_positionals.Count >= min && _positionals.Count <= max)
        {
            return;
        }

        var found = _positionals.Count == 0 ? "none were given" : $"{_positionals.Count} were given";
        var message = max == 0
            ? $"'{_command}' takes no arguments, but {found}."
            : $"'{_command}' expects {expected}, but {found}.";
        throw new ToolException(ErrorCodes.UsageError, message, $"Run 'canvasnet {_command} --help' for usage.");
    }

    /// <summary>
    ///     Splits the argument list into options and positional arguments.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <param name="specs">The declared options.</param>
    private void ReadTokens(IReadOnlyList<string> args, IReadOnlyList<OptionSpec> specs)
    {
        var onlyPositionals = false;
        var index = 0;
        while (index < args.Count)
        {
            var token = args[index++];

            // After a bare "--" everything is positional; a lone "-" and negative numbers are positional too
            if (onlyPositionals || !LooksLikeOption(token))
            {
                _positionals.Add(token);
                continue;
            }

            if (token == "--")
            {
                onlyPositionals = true;
                continue;
            }

            var (spec, inlineValue) = Resolve(token, specs);
            if (!spec.TakesValue)
            {
                if (inlineValue != null)
                {
                    throw new ToolException(
                        ErrorCodes.UsageError,
                        $"Option '{spec.Name}' does not take a value.",
                        $"Run 'canvasnet {_command} --help' for usage.");
                }

                _ordered.Add(new ParsedOption(spec.Name, null));
                continue;
            }

            // A value is the inline part (--name=value) or the next token, whatever it looks like
            if (inlineValue == null)
            {
                if (index >= args.Count)
                {
                    throw new ToolException(
                        ErrorCodes.UsageError,
                        $"Option '{spec.Name}' requires a value <{spec.ValueName ?? "value"}>.",
                        $"Run 'canvasnet {_command} --help' for usage.");
                }

                inlineValue = args[index++];
            }

            _ordered.Add(new ParsedOption(spec.Name, inlineValue));
        }
    }

    /// <summary>
    ///     Resolves an option token to its declaration, splitting off an inline value.
    /// </summary>
    /// <param name="token">The option token.</param>
    /// <param name="specs">The declared options.</param>
    /// <returns>The declaration and the inline value (<see langword="null"/> when the token carries none).</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> for an unknown option.</exception>
    private (OptionSpec Spec, string? InlineValue) Resolve(string token, IReadOnlyList<OptionSpec> specs)
    {
        // --name=value
        var equals = token.IndexOf('=');
        if (token.StartsWith("--", StringComparison.Ordinal) && equals > 2)
        {
            var named = Find(specs, token[..equals]);
            if (named != null)
            {
                return (named, token[(equals + 1)..]);
            }

            throw UnknownOption(token[..equals], specs);
        }

        // --name, -o
        var exact = Find(specs, token);
        if (exact != null)
        {
            return (exact, null);
        }

        // -Dname=value: a single-letter alias with its value attached
        if (!token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
        {
            var attached = Find(specs, token[..2]);
            if (attached is { TakesValue: true })
            {
                // -o=file is accepted like -ofile; a value that itself starts with '=' needs the separate form
                var value = token[2..];
                return (attached, value[0] == '=' ? value[1..] : value);
            }
        }

        throw UnknownOption(token, specs);
    }

    /// <summary>
    ///     Finds the declaration that answers to an option name or alias.
    /// </summary>
    /// <param name="specs">The declared options.</param>
    /// <param name="name">The name or alias as typed.</param>
    /// <returns>The declaration, or <see langword="null"/>.</returns>
    private static OptionSpec? Find(IReadOnlyList<OptionSpec> specs, string name)
    {
        return specs.FirstOrDefault(s => s.Name == name || s.Aliases.Contains(name));
    }

    /// <summary>
    ///     Creates the exception for an unknown option, suggesting the closest declared option.
    /// </summary>
    /// <param name="name">The unknown option as typed (without an inline value).</param>
    /// <param name="specs">The declared options.</param>
    /// <returns>The exception to throw.</returns>
    private ToolException UnknownOption(string name, IReadOnlyList<OptionSpec> specs)
    {
        var typed = name.ToLowerInvariant();
        var suggestion = specs
            .SelectMany(s => s.Aliases.Prepend(s.Name))
            .Where(n => n.Length >= MinSuggestionLength)
            .Select(n => (Name: n, Distance: CommandRegistry.EditDistance(typed, n.ToLowerInvariant())))
            .Where(c => c.Distance <= MaxSuggestionDistance
                        || (typed.Length >= MinSuggestionLength && c.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Distance)
            .Select(c => c.Name)
            .FirstOrDefault();

        var hint = suggestion != null
            ? $"Did you mean '{suggestion}'? Run 'canvasnet {_command} --help' to list the options."
            : $"Run 'canvasnet {_command} --help' to list the options.";
        return new ToolException(ErrorCodes.UsageError, $"Unknown option '{name}' for '{_command}'.", hint);
    }

    /// <summary>
    ///     Determines whether a token is an option rather than a positional argument.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token starts a bare <c>--</c> or names an option.</returns>
    private static bool LooksLikeOption(string token)
    {
        // A lone "-" (the conventional name of a standard stream) is positional
        if (token.Length < 2 || token[0] != '-')
        {
            return false;
        }

        // A negative number such as -90 or -.5 is a value, not an option
        return !(char.IsAsciiDigit(token[1]) || (token[1] == '.' && token.Length > 2 && char.IsAsciiDigit(token[2])));
    }

    /// <summary>
    ///     Requires that the command declares an option.
    /// </summary>
    /// <param name="name">The canonical option name.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the option is not declared; this is a defect of the caller.</exception>
    private void RequireKnown(string name)
    {
        if (!_knownNames.Contains(name))
        {
            throw new KeyNotFoundException($"The command '{_command}' does not declare the option '{name}'.");
        }
    }
}
