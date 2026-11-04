### CommandRegistry

<!-- cspell:ignore canvasnet -->

![Commands Structure](CommandsView.svg)

#### Purpose

`CommandRegistry` holds the table of commands the tool offers. Its single responsibility is to resolve a typed
command name or alias to a command, dispatch to it, and render help. The supporting types `ICommand` and
`OptionSpec` (file `ICommand.cs`) are documented here rather than as units of their own. The unit is the partial class
`CommandRegistry`: `CommandRegistry.cs` holds the registration, the lookup, the dispatch, and the unknown-command
report, and `CommandRegistry.Help.cs` holds the built-in `help` command (the subjects, the `--example` and `--examples`
options, and the plain-text and JSON help). The content of the help topics is the unit `HelpTopics`.

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
verb), `compare` (`CompareCommand`), `montage` (`MontageCommand`), and `inspect` (`InspectCommand`), in that order.
A command is added to this method
in the change that implements it.

**EditDistance(string, string)** (`internal static`): the Levenshtein distance used to suggest the closest command
name; `ArgumentReader` uses it to suggest the closest option.

**Register(ICommand)**: adds a command. Every name the command answers to (its name and aliases) must be unique
across the registry and within the command, compared ignoring case, and must not be the reserved name `help`;
otherwise `InvalidOperationException` is thrown. Throws `ArgumentNullException` for a null command.

**Find(string)**: returns the command whose name or alias equals the argument ignoring case, or `null`.

**Run(Context)**: requires a command name (else a `usage-error` `ToolException` with a hint to run
`canvasnet --help`). The name `help` is handled by `RunHelp` and returns. Otherwise
`Find` resolves the command (an unknown name throws the exception described under _Error Handling_), and the
command's `Run` is called with `Context.CommandArguments`. A `ToolException` escaping the command has its
`Command` set to the command's canonical name when empty and is re-thrown.

**RunHelp(Context)** (private, `CommandRegistry.Help.cs`): the built-in `help` command. It parses the command
arguments with `ArgumentReader` over its own option table `HelpOptions`: `--example <name>` (print only the source or
command line of one example of a topic) and `--examples` (list the examples of a topic). It accepts at most one
positional argument, the subject. Without either option it writes `RenderHelp(subject, Context.Json)`. With an option
it requires a topic (see `RequireTopicForExamples`) and writes, to standard output and nothing else:

- `--example <name>` as text: the exact content of the example (`HelpTopics.RenderExample`), so that the output can be
  saved as a file or run as a command; with `--json`, the `example` document.
- `--examples` as text: one line per example (`HelpTopics.RenderExampleList`); with `--json`, the `examples` document.

`--example` together with `--examples`, either option without a subject, and either option on a command or on `topics`
(which have usage text, not examples) are usage errors, as is an unknown example name (reported by `HelpTopics`).

**RenderHelp(string? subject, bool json)** (`public`, `CommandRegistry.Help.cs`): renders the help for a subject, as
plain text or, with `json`, as a JSON document. The subject is resolved in this order:

1. `null`, `help`, or `topics` (ignoring case): the index.
2. A command name or alias (`Find`): the help of the command.
3. A topic name or alias (`HelpTopics.Find`): the topic.
4. Anything else: the unknown-subject exception.

The text index is the usage lines (including one for `help` with its subject, `--example`, `--examples`, and `--json`),
the command list (each command's name and aliases aligned with its summary, omitted when no command is
registered), the pointer to `canvasnet <command> --help`, the `Help topics:` list (each topic's name and summary, then
the aliases, then the pointer to `canvasnet help <topic>` and `canvasnet help topics`, wrapped as topic text is), and
the global options. The text help of a command is `Usage:` plus the command's usage text, the summary, the command's
options, and the global options, then a `See also:` paragraph (`CommandRegistry.SeeAlso`, wrapped as topic text is) that
names `canvasnet help <topic>`, every topic name, and `canvasnet help topics`, so that the help of every command
(`render --help`, `formats --help`, `info --help`, ...) leads to the format topics. An option is listed as its aliases
and name followed by `<value name>` when it takes
a value, with the descriptions aligned. The help text of a topic is `HelpTopics.RenderText`.

With `json` the document is written with `ResultWriter.Success("help", ...)`, and its `kind` field names the shape:

- `kind` `index` (no subject, `help`, or `topics`): `topic` (`topics`), `usage` (the usage lines joined by line feeds),
  `commands` and `topics` (arrays of `name`, `aliases`, and `summary`), and `globalOptions`.
- `kind` `command` (a command): `topic` (the canonical name), `aliases`, `summary`, `usage`, `options`, and
  `globalOptions`.
- `kind` `topic` (a topic): as written by `HelpTopics.RenderJson`.

An option in `options` or `globalOptions` is `{name, aliases, takesValue, valueName (only when it takes a value),
description}`, written from its `OptionSpec`. The text of a command's help is unchanged by the topics.

**RequireTopicForExamples(subject)** (private): resolves the subject of `--example` or `--examples` to a topic. No
subject, a command, or `topics` is a usage error (`'--example' and '--examples' need a topic.`, or `'<subject>' has no
examples; only help topics do.`); an unknown subject throws the unknown-subject exception.

**CreateUnknownSubjectException(subject)** (private): builds the `ToolException` for an unknown help subject: code
`usage-error`, message `Unknown help subject '{subject}'.`, `Command` set to `help`, and a hint that suggests the
closest name (the edit distance of `ClosestName`, over every command name and alias, every topic name and alias, and
`topics`) followed by `Valid subjects: <commands>, <topics>, topics. Run 'canvasnet help topics' for descriptions.`

**CreateUnknownCommandException** (private): builds the `ToolException` for an unknown name: code
`usage-error`, message `Unknown command '{name}'.`, `Command` set to the name typed, and a hint. When the name is a
help topic name or alias (`canvasnet scad`), the hint is `'{name}' is a help topic: run 'canvasnet help {topic}'.`
followed by a pointer to `canvasnet --help`. Otherwise the hint suggests the closest command name or alias
(`ClosestName`: Levenshtein edit distance of at most two, ties resolved in registration order, `help` included as a
candidate) followed by a pointer to `canvasnet --help`.

#### Error Handling

The registry reports problems with the command line as `ToolException` with code `usage-error` (no command, an
unknown command, and for `help` an unknown option, an unknown subject, more than one subject, an unknown example,
`--example` with `--examples`, or an example option without a topic). A `ToolException` thrown by a command passes
through with its details intact and the command
name recorded. Any other exception thrown by a command propagates unchanged: it is a defect, and the entry
point reports and re-throws it. Registration errors (`InvalidOperationException`, `ArgumentNullException`) are
programming errors detected at start-up.

#### Dependencies

- **Context** - the command name, the command arguments, the `Json` flag, and `WriteLine` for the built-in help
  command.
- **ArgumentReader** - parses the options of the `help` command and supplies the edit distance for suggestions.
- **ResultWriter** - the JSON help documents.
- **HelpTopics** - the topic catalog and its renderings, for the topic list, the topic help, and the examples.
- **ToolException** and **ErrorCodes** - the error report (documented in _Program Unit Design_).

#### Callers

- **Program** - `CreateDefault`, `Run`, and `RenderHelp` (with the `Json` flag of the context).
- Tests - build registries with injected commands.
