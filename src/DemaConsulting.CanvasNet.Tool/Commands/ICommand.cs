namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     Describes one command-line option accepted by a command, for parsing and help generation.
/// </summary>
/// <param name="Name">The canonical option name including its leading dashes, for example <c>--output</c>.</param>
/// <param name="Aliases">Alternative spellings of the option, for example <c>-o</c>; empty when there are none.</param>
/// <param name="TakesValue"><see langword="true"/> when the option requires a value; <see langword="false"/> for a flag.</param>
/// <param name="Description">A one-line description shown in help output.</param>
/// <param name="ValueName">The placeholder shown for the value in help output, for example <c>file</c>; <see langword="null"/> for flags.</param>
internal sealed record OptionSpec(
    string Name,
    string[] Aliases,
    bool TakesValue,
    string Description,
    string? ValueName = null);

/// <summary>
///     A command that the tool can run, such as <c>render</c> or <c>info</c>.
/// </summary>
/// <remarks>
///     Commands are registered with a <see cref="CommandRegistry"/>, which resolves the command name
///     (or one of its aliases) typed by the user and dispatches to <see cref="Run"/>. A command reports
///     expected failures by throwing <see cref="ToolException"/> and writes its results through the
///     supplied <see cref="Context"/>; it never writes to the console directly.
/// </remarks>
internal interface ICommand
{
    /// <summary>
    ///     Gets the canonical command name, for example <c>render</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets alternative names that select the same command, for example <c>convert</c>.
    /// </summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    ///     Gets a one-line summary shown in the command list of the top-level help.
    /// </summary>
    string Summary { get; }

    /// <summary>
    ///     Gets the options the command accepts, shown in the command's help.
    /// </summary>
    IReadOnlyList<OptionSpec> Options { get; }

    /// <summary>
    ///     Gets the usage text of the command: the usage line(s) without a leading <c>Usage:</c> label,
    ///     optionally followed by examples.
    /// </summary>
    string Usage { get; }

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="context">The context that receives the command's output.</param>
    /// <param name="args">The command arguments: everything after the command name except the global options.</param>
    /// <exception cref="ToolException">Thrown for an expected, user-correctable failure.</exception>
    void Run(Context context, IReadOnlyList<string> args);
}
