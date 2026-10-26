### CommandRegistry

<!-- cspell:ignore canvasnet -->

![Commands Structure](CommandsView.svg)

#### Purpose

`CommandRegistry` holds the table of commands the tool offers. Its single responsibility is to resolve a typed
command name or alias to a command, dispatch to it, and render help. The supporting types `ICommand` and
`OptionSpec` (file `ICommand.cs`) are documented here rather than as units of their own.

#### Data Model

**ICommand** (`internal interface`): `Name` (canonical name), `Aliases` (alternative names), `Summary` (one
line for the command list), `Options` (`IReadOnlyList<OptionSpec>`), `Usage` (the usage line(s) without the
`Usage:` label, optionally followed by examples), and `Run(Context, IReadOnlyList<string>)`.

**OptionSpec** (`internal sealed record`): `Name` (canonical option name including its dashes, for example
`--output`), `Aliases` (other spellings such as `-o`), `TakesValue`, `Description`, and `ValueName` (the
placeholder shown in help). `ArgumentReader` uses the same table to parse a command's options.

**_commands**: `List<ICommand>` - the registered commands in registration order, exposed read-only as
`Commands`.

**GlobalOptions**: a static `OptionSpec` table of the global options (version, help, silent, validate, json,
results, depth, and log), shown in every help screen.

#### Key Methods

**CreateDefault**: creates a registry with every command the tool ships with: `formats`
(`DiscoveryCommands.Formats`), `info` (`DiscoveryCommands.Info`), `render` with its alias `convert`
(`RenderCommand`), the verbs `crop`, `resize`, `rotate`, `flip`, and `pad` (each a `RenderCommand` constructed with the
verb), `compare` (`CompareCommand`), and `montage` (`MontageCommand`), in that order. A command is added to this method
in the change that implements it.

**EditDistance(string, string)** (`internal static`): the Levenshtein distance used to suggest the closest command
name; `ArgumentReader` uses it to suggest the closest option.

**Register(ICommand)**: adds a command. Every name the command answers to (its name and aliases) must be unique
across the registry and within the command, compared ignoring case, and must not be the reserved name `help`;
otherwise `InvalidOperationException` is thrown. Throws `ArgumentNullException` for a null command.

**Find(string)**: returns the command whose name or alias equals the argument ignoring case, or `null`.

**Run(Context)**: requires a command name (else a `usage-error` `ToolException` with a hint to run
`canvasnet --help`). The name `help` prints `RenderHelp` of the first command argument and returns. Otherwise
`Find` resolves the command (an unknown name throws the exception described under _Error Handling_), and the
command's `Run` is called with `Context.CommandArguments`. A `ToolException` escaping the command has its
`Command` set to the command's canonical name when empty and is re-thrown.

**RenderHelp(string?)**: with `null` or `help`, the top-level help: the usage lines, the command list (each
command's name and aliases aligned with its summary, omitted when no command is registered), and the global
options. With a command name or alias, that command's help: `Usage:` plus the command's usage text, the
summary, the command's options, and the global options. An option is listed as its aliases and name followed by
`<value name>` when it takes a value, with the descriptions aligned. An unknown name throws the unknown-command
exception.

**CreateUnknownCommandException** (private): builds the `ToolException` for an unknown name: code
`usage-error`, message `Unknown command '{name}'.`, `Command` set to the name typed, and a hint that suggests
the closest command name or alias (Levenshtein edit distance of at most two, ties resolved in registration
order, `help` included as a candidate) followed by a pointer to `canvasnet --help`.

#### Error Handling

The registry reports problems with the command line as `ToolException` with code `usage-error` (no command, an
unknown command). A `ToolException` thrown by a command passes through with its details intact and the command
name recorded. Any other exception thrown by a command propagates unchanged: it is a defect, and the entry
point reports and re-throws it. Registration errors (`InvalidOperationException`, `ArgumentNullException`) are
programming errors detected at start-up.

#### Dependencies

- **Context** - the command name, the command arguments, and `WriteLine` for the built-in help command.
- **ToolException** and **ErrorCodes** - the error report (documented in _Program Unit Design_).

#### Callers

- **Program** - `CreateDefault`, `Run`, and `RenderHelp`.
- Tests - build registries with injected commands.
