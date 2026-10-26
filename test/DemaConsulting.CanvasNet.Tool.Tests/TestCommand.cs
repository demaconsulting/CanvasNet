// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     A configurable command used to test the command registry and the entry point without
///     depending on any shipped command.
/// </summary>
internal sealed class TestCommand : ICommand
{
    /// <summary>
    ///     The action run by <see cref="Run"/>.
    /// </summary>
    private readonly Action<Context, IReadOnlyList<string>> _action;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TestCommand"/> class.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="action">The action to run; when <see langword="null"/> the command does nothing.</param>
    /// <param name="aliases">The command aliases.</param>
    public TestCommand(string name, Action<Context, IReadOnlyList<string>>? action = null, params string[] aliases)
    {
        Name = name;
        Aliases = aliases;
        _action = action ?? ((_, _) => { });
    }

    /// <summary>
    ///     Gets the arguments the command was last run with, or <see langword="null"/> if it has not run.
    /// </summary>
    public IReadOnlyList<string>? ReceivedArguments { get; private set; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases { get; }

    /// <inheritdoc />
    public string Summary => $"Summary of {Name}";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options { get; } =
    [
        new("--flag", ["-f"], false, "A flag option"),
        new("--value", [], true, "A value option", "text")
    ];

    /// <inheritdoc />
    public string Usage => $"canvasnet {Name} <input> [options]";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ReceivedArguments = args;
        _action(context, args);
    }
}
