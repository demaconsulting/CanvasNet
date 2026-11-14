### CommandRegistry

<!-- cspell:ignore canvasnet -->

![Commands Structure](CommandsView.svg)

#### Purpose

`CommandRegistry` holds the table of commands the tool offers. Its single responsibility is to resolve a typed
command name or alias to a command, dispatch to it, and render help. The supporting types `ICommand` and
`OptionSpec` (file `ICommand.cs`) are documented here rather than as units of their own. The unit is the partial class
`CommandRegistry`: `CommandRegistry.cs` holds the registration, the lookup, the dispatch, and the unknown-command
report, and `CommandRegistry.Help.cs` holds the built-in `help` command (the subjects, the `--example` and `--examples`
options, and the plain-text and JSON help). The content of the help cards is the unit `HelpTopics`.

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
verb), `compare` (`CompareCommand`), `montage` (`MontageCommand`), `inspect` (`InspectCommand`), and `flatten`
(`FlattenCommand`), in that order.
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
command line of one example of the card or of a card below it) and `--examples` (print every example of the card in
full, and list the cards below it that have examples). It accepts at most eight positional arguments, the help path
(`HelpTopics.SplitPath` also splits a word at `/`). Without either option it writes `RenderHelp(path, Context.Json)`.
With an option it requires a card (see `RequireCardForExamples`) and writes, to standard output and nothing else:

- `--example <name>` as text: the exact content of the example (`HelpTopics.RenderExample`), so that the output can be
  saved as a file or run as a command; with `--json`, the `example` document.
- `--examples` as text: each example of the card in full, with its name, kind, description, and content, followed by
  the cards below it that have examples, each with its `--examples` command (`HelpTopics.RenderExamples`); with
  `--json`, the `examples` document (`HelpTopics.RenderExamplesJson`).

`--example` together with `--examples`, either option without a path, either option on a command, on `help`, or on
`topics` (which have usage text, not examples), and either option on a card whose tree has no example are usage
errors, as is an unknown example name (reported by `HelpTopics`).

**RenderHelp(string? subject, bool json)** and **RenderHelp(IReadOnlyList<string> path, bool json)** (`public`,
`CommandRegistry.Help.cs`): render the help for a subject or a help path, as plain text or, with `json`, as a JSON
document. `Program` uses the first for `<command> --help` (a root card for a topic name). The path is resolved in this
order:

1. Empty, or `help` alone (ignoring case): the index.
2. `topics` alone: the whole tree of help cards (`HelpTopics.RenderTopicsText` or `RenderTopicsJson`).
3. A command name or alias (`Find`) alone: the help of the command.
4. Anything else: `HelpTopics.Resolve`, which finds the root card by name or alias and each further word among the
   cards below the previous one. An unknown first word is the unknown-subject exception, and an unknown further word
   is the unknown-card exception of `HelpTopics`.

A further word after `help`, `topics`, or a command is a usage error (`'<word>' has no help cards below it.`).

The text index is the usage lines (including one for `help`, its path, `topics`, `--example`, `--examples`, and
`--json`), the command list (each command's name and aliases aligned with its summary, omitted when no command is
registered), and the pointer to `canvasnet <command> --help`. Then comes the `Help topics:` list: each root card's name
and summary, then the aliases, then the pointer to `canvasnet help <topic> [<card> ...]` and `canvasnet help topics`
and to the `style` and `composition` topics, wrapped as card text is. The index ends with the global options.

The text help of a command is `Usage:` plus the command's usage text, the summary, the command's options, and the
global options. It ends with a `See also:` paragraph (`CommandRegistry.SeeAlso`, wrapped as card text is) that names
`canvasnet help <topic>`, every topic name, and `canvasnet help topics`, so the help of every command
(`render --help`, `formats --help`, `info --help`, ...) leads to the help cards. An option is listed as its aliases and
name followed by `<value name>` when it takes a value, with the descriptions aligned. The help text of a card is
`HelpTopics.RenderText`.

With `json` the document is written with `ResultWriter.Success("help", ...)`, and its `kind` field names the shape:

- `kind` `index` (no path, or `help`): `usage` (the usage lines joined by line feeds), `commands` and `topics` (arrays
  of `name`, `aliases`, and `summary`; the topics are the root cards), and `globalOptions`.
- `kind` `command` (a command): `topic` (the canonical name), `aliases`, `summary`, `usage`, `options`, and
  `globalOptions`.
- `kind` `topics` (`topics`) and `kind` `card` (a card path): as written by `HelpTopics.RenderTopicsJson` and
  `HelpTopics.RenderJson`.

An option in `options` or `globalOptions` is `{name, aliases, takesValue, valueName (only when it takes a value),
description}`, written from its `OptionSpec`. The text of a command's help is unchanged by the cards.

**RequireCardForExamples(path)** (private): resolves the path of `--example` or `--examples` to a card. These are
usage errors:

- no path: `'--example' and '--examples' need a help card.`;
- a command, `help`, or `topics`: `'<word>' has no examples; only help cards do.`;
- a card whose tree has no example: `'<path>' has no examples.`, with the hint
  `Run 'canvasnet help <root> --examples' to find the cards of '<root>' that have examples.`

An unknown path throws the unknown-subject or unknown-card exception.

**CreateUnknownSubjectException(subject)** (private): builds the `ToolException` for an unknown first word of a help
path: code `usage-error`, message `Unknown help subject '{subject}'.`, `Command` set to `help`, and a hint. The hint
suggests the closest name (the edit distance of `ClosestName`, over every command name and alias, every root card name
and alias, `topics`, and `help`) followed by
`Valid subjects: <commands>, <topics>, topics, help. Run 'canvasnet help topics' for descriptions.`

**ClosestName(name, candidates)** (`internal static`): the candidate within an edit distance of two of the typed name,
the nearest first and ties in the order given, or `null`. `HelpTopics` uses it to suggest a card.
**CreateUnknownCommandException** (private): builds the `ToolException` for an unknown name: code
`usage-error`, message `Unknown command '{name}'.`, `Command` set to the name typed, and a hint. When the name is a
help topic name or alias (`canvasnet scad`), the hint is `'{name}' is a help topic: run 'canvasnet help {topic}'.`
followed by a pointer to `canvasnet --help`. Otherwise the hint suggests the closest command name or alias
(`ClosestName`: Levenshtein edit distance of at most two, ties resolved in registration order, `help` included as a
candidate) followed by a pointer to `canvasnet --help`.

#### Error Handling

The registry reports problems with the command line as `ToolException` with code `usage-error` (no command, an
unknown command, and for `help` an unknown option, an unknown subject or card, a word after a command, more than eight
words, an unknown example, `--example` with `--examples`, or an example option without a card that has examples). A
`ToolException` thrown by a command passes through with its details intact and the command
name recorded. Any other exception thrown by a command propagates unchanged: it is a defect, and the entry
point reports and re-throws it. Registration errors (`InvalidOperationException`, `ArgumentNullException`) are
programming errors detected at start-up.

#### Dependencies

- **Context** - the command name, the command arguments, the `Json` flag, and `WriteLine` for the built-in help
  command.
- **ArgumentReader** - parses the options of the `help` command and supplies the edit distance for suggestions.
- **ResultWriter** - the JSON help documents.
- **HelpTopics** - the tree of help cards and its renderings, for the topic list, the cards, the tree, and the examples.
- **ToolException** and **ErrorCodes** - the error report (documented in _Program Unit Design_).

#### Callers

- **Program** - `CreateDefault`, `Run`, and `RenderHelp` (with the `Json` flag of the context).
- Tests - build registries with injected commands.
