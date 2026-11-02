## Commands

![Commands Structure](CommandsView.svg)

### Overview

The `Commands` subsystem (namespace `DemaConsulting.CanvasNet.Tool.Commands`, folder
`src/DemaConsulting.CanvasNet.Tool/Commands/`) turns the command named on the command line into running code.
It defines what a command is (the `ICommand` contract and the `OptionSpec` option description) and holds the
table of commands (`CommandRegistry`). It is a subsystem rather than a single unit because each family of
commands is its own unit behind the same contract and registry. It contains six units:
`CommandRegistry`, which also documents the supporting types `ICommand` and
`OptionSpec`; `DiscoveryCommands`, which provides the `formats` and `info` commands; `RenderCommand`, which
provides `render` and its alias `convert`, and the verb commands `crop`, `resize`, `rotate`, `flip`, and `pad`
(the same class, constructed with a verb); `CompareCommand`, which provides `compare`; `MontageCommand`, which
provides `montage`; and `InspectCommand`, which provides `inspect` with the subcommands `colors` and `pixel`.

### Interfaces

**ICommand** (consumed): the contract every command implements - `Name`, `Aliases`, `Summary`, `Options`,
`Usage`, and `Run(Context, IReadOnlyList<string>)`. A command writes its results through the `Context`, reports
expected failures by throwing `ToolException`, and never writes to the console directly.

**CommandRegistry.Run(Context)** (provided to `Program`): runs the command selected by `Context.Command` with
`Context.CommandArguments`; the built-in `help` command is handled here. Throws `ToolException` (code
`usage-error`) when the command is missing or unknown, and re-throws the command's own `ToolException` after
recording the command name on it.

**CommandRegistry.RenderHelp(string?)** (provided to `Program`): returns the top-level help or a command's help.

**CommandRegistry.CreateDefault()** (provided to `Program`): creates the registry with every command the tool
ships with (`formats`, `info`, `render` with its alias `convert`, the verbs `crop`,
`resize`, `rotate`, `flip`, and `pad`, each registered as its own `RenderCommand` instance, then `compare`,
`montage`, and `inspect`).

**ArgumentReader** (consumed): each command parses its arguments with `ArgumentReader`, driven by the command's
`OptionSpec` table, and writes JSON results through `ResultWriter` when `Context.Json` is set.

**Imaging** (consumed): the `info`, `render`, verb, `compare`, `montage`, and `inspect` commands read, compare, compose,
transform, analyze, and write files only through the `Imaging` subsystem (`SourceLoader`, `ImagePipeline`,
`ImageComparison`, `ContactSheet`, and `ImageWriter`) and the core `Surface` (comparison and analysis); a command never
calls a codec directly.

**Shared option declarations**: `CompareCommand` and `MontageCommand` take the declarations of the options they share
with `render` (`--page`, `--dpi`, the options of an OpenSCAD model, `--filter`, `--quality`, `--no-clobber`, and so on)
from `RenderCommand.OptionTable` through `RenderCommand.SpecsFor`, and read the model options with
`RenderCommand.ReadScadOptions`, so the grammar and the help text of a shared option are the same in every command.
`InspectCommand` takes every option of the table but the options of the output, and reads the source options with
`RenderCommand.ReadSourceOptions`; it is one registered command with the subcommands `colors` and `pixel`, taken from
its first positional argument, because the registry is flat.

**Expected failure after a result**: a command whose failure still produced a result (`compare` with a threshold that
is not met) writes the result and then throws a `ToolException` whose `ResultFields` write the result into the JSON
failure document; the exit code is 1 like every expected failure.

### Design

`Program.Run` hands the context to the registry; the registry resolves the name (ignoring case, accepting
aliases) to an `ICommand` and calls `Run`. A command parses its own arguments - the `Context` has already
removed the global options - so the global parser never needs to know any command's options, and a command can
be added without touching `Context`. Help is generated from the registered commands: the command list from each
command's name, aliases, and summary, and a command's help from its usage text and its `OptionSpec` table, so
the help cannot drift from the options a command declares. The registry is an instance rather than static state
so that tests can inject a purpose-made command - one that fails, or throws an unexpected exception - and prove
the error policy of `Program` end to end without depending on a shipped command.
