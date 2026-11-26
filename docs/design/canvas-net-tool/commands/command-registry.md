### CommandRegistry

![Commands Structure](CommandsView.svg)

#### Purpose

`CommandRegistry` holds the table of commands the tool offers. Its single responsibility is to resolve a typed
command name or alias to a command, dispatch to it, and render help. The supporting types `ICommand` and
`OptionSpec` (file `ICommand.cs`) are documented here rather than as units of their own. The unit is the partial class
`CommandRegistry`: `CommandRegistry.cs` holds the registration, the lookup, the dispatch, and the unknown-command
report, and `CommandRegistry.Help.cs` holds the built-in `help` command (the shell over `HelpCli` of the OTS library
`DemaConsulting.HelpCards`, the tool's own subjects, the index, and the plain-text and JSON help of a command). The
content of the help cards is the unit `HelpTopics` (the library cards of the Viewer with the tool's command-line layer).

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

**CommandLinePhrases**: a static table of the phrases of the Viewer library's hints that name a setting the tool sets
with an option of its own, each with the words of the command line: "Without strict mode" is "Without --strict",
"ScadViewOptions.MaxTriangles" is "--max-triangles", "a smaller scale, width, or height" is "a smaller --scale,
--width, or --height", "Use a scale of " is "Use --scale ", and "or a smaller width or height" is "or a smaller --width
or --height".

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
`Find` resolves the command (an unknown name throws the exception described under *Error Handling*), and the
command's `Run` is called with `Context.CommandArguments`. A `ToolException` escaping the command has its
`Command` set to the command's canonical name when empty and is re-thrown. Any other `ViewerException` (a failure of
the Viewer library, mapped by the loader's shared mapping) becomes a `ToolException` with the same code, message,
inner exception, `Location`, and `DetailCode`, the command's name, and the hint of `CommandLineHint`.

**CommandLineHint(string?)** (`internal static`): words a hint of the library in the terms of the command line by
replacing every phrase of `CommandLinePhrases` (ordinal, in table order); `null` stays `null`, and a hint without such
a phrase is kept. Only the wording changes: the codes are those of the library, so the tool and
`CanvasNetViewer.Render` report the same code for the same failure.

**RunHelp(Context)** and **RunHelp(Context, arguments)** (`CommandRegistry.Help.cs`): the built-in `help` command, with
the command arguments, and `--help` (`Program` passes the named command or topic). It calls `ExecuteHelp` with the
arguments and `Context.Json` and writes the result through the context: on a failure, first the lines
`Error: <code>: <message>` and `Hint: <hint>` with `Context.WriteError` (which makes the tool exit with 1), then the
output, which with `--json` is the failure document; on success, the output. So `--silent` and `--log` apply as for
every command.

**ExecuteHelp(arguments, json)** (`internal`): runs the help command on `HelpCli` and captures what it writes. A
leading word `help` is dropped (`help help` is the index), and `--json` is put first when the global option was given
(the context has already removed it from the arguments). Then each word before `--` of the form `--<name>=<value>`
whose name is a help option that takes a value (`--example`, `--search`, `--tag`, `--source`, `--format`, `--width`)
is split into the two words `--<name>` and `<value>` (`SplitInlineValues`), so help accepts the equals form of every
other command although HelpCards reads a value only as the next word. A flag with a value (`--examples=x`) stays one
word and is an unknown option, an empty value (`--example=`) is an option without a value, and a value that starts
with `--` (`--search=--x`) is rejected as missing, as HelpCards never takes such a word as a value. Each run creates
its own `HelpCli` from `HelpCliOptions`:

- `ToolName` `canvasnet`, `CommandTemplate` `canvasnet help {path}`, and `CatalogFactory` returning
  `HelpTopics.Catalog` (one shared catalog; a `HelpCatalogException` is recorded so the failure is reported as
  `operation-failed`).
- `IndexPreamble`: the usage lines (`Usage: canvasnet <command> [arguments] [options]`, `canvasnet [options]`, and
  `canvasnet help [<command> | <topic> [<card> ...] | topics] [<help options>] [--json]`) and the pointers to
  `canvasnet <command> --help`, `canvasnet help topics`, and the `style` and `composition` topics. `UsageLines` stays
  unset, so the index document keeps the tool's `usage` string rather than an array of HelpCards.
- `IndexSections`: `Commands:` (each command's name and aliases aligned with its summary, omitted when no command is
  registered), `Options:` (the global options), and `Help options:` (`--examples`, `--example <name>`,
  `--search <text>`, `--tag <tag>`, `--source <name>`, `--all`, `--format <format>`, and `--width <columns>`).
- `FallbackResolver`: `ResolveOwnSubject`, with whether the run asks for JSON (`--json`, or `--format json` before
  `--`, the value in any case, as HelpCards reads it; the option names are matched exactly).
- `Output` and `Error`: string writers of the run.
- `JsonEnvelope`: `HelpTopics.Envelope`, with the index fields of the tool (`WriteIndexFields`) for the kind `index`.
  For the kind `example`, the fields of HelpCards are written again with `id` set to the card that the path words
  name (`FindRequestedCardId`: the path words of the run, read as HelpCards reads them, resolved with
  `HelpTopics.Catalog.Find`), keeping their order (`WriteWithId`), because HelpCli writes the card that holds the
  example; `example.card` still names that card.
- `JsonError`: records the `HelpCliError` and writes `HelpTopics.WriteError`. A failure without a hint that is not a
  `catalog-error` (some argument errors of HelpCards) is given `HelpOptionsHint` first:
  `Run 'canvasnet help' for the help options, or 'canvasnet help topics' to list the help cards.`
- `ExitCodes`: `UsageError` and `InternalError` 1.

It returns a `HelpRun` record: the exit code (0 or 1), the output without its final line feed, and on a failure the
tool's error code, the message, and the hint. The error comes from the recorded `HelpCliError` with `--json`, and
otherwise from the text error that HelpCards writes, `Error: <message>` with an optional second line for the hint
(`SplitTextError`); any other text is kept as the message. A text failure without a hint that is not a defective
catalog is given `HelpOptionsHint` as well, so every usage error has the `Hint` line and the `hint` key. Two failures
are returned by the tool itself: a `ToolException` of `ResolveOwnSubject` when JSON was asked only with
`--format json` (see below), and the examples of a command, `topics`, or `help` (`CreateNoExamplesError`); with JSON
asked, the output of both is `ResultWriter.Failure("help", exception)`.

HelpCards parses the help options (`--examples`, `--example`, `--search`, `--tag`, `--source`, `--all`, `--format`,
`--width`; `--lang` is not enabled) and answers the index, a card, its examples, one example, a search, and a tag list
from the catalog. The JSON documents are therefore the tool's envelope followed by the fields of HelpCards: `index`
(the tool's `usage`, `commands`, `topics` with `name`, `aliases`, and `summary`, and `globalOptions`, then `tool`,
`version`, `sources`, and `cards`), `card`, `examples`, `example` (`id`, the card that was asked for, then `example`,
whose `card` is the card that holds it), `search`, and `cards`, and every failure is
`{"schemaVersion":2,"ok":false,"command":"help","error":{"code":"usage-error","message":...,"hint":...}}`.

**ResolveOwnSubject(path, json)** (private): offered the path words of a plain help command (no mode option) before the
catalog. It splits the words at `/` and white space and looks at the first segment: `topics` alone gives
`HelpTopics.RenderTopics(json)`; a command name or alias (`Find`) alone gives the help of the command (text or the
`command` document); `help` or a further word after `topics` or a command is a usage error
(`'<word>' has no help cards below it.`); a root card name or alias returns `null`, so HelpCards answers with the card;
anything else throws the unknown-subject exception. HelpCards does not catch what the resolver throws. With the global
`--json` or in text the exception reaches `Program`, which reports it as for every command (with the failure document
for `--json`). When JSON was asked only with `--format json`, `ExecuteHelp` catches it and returns it as a failed
`HelpRun` whose output is the failure document, because `Program` writes that document only for the global `--json`;
`RunHelp` then writes the `Error` and `Hint` lines and the document once, and the tool exits with 1.

**CreateNoExamplesError(words, message)** (private): HelpCards answers `--example` and `--examples` from the catalog
only, so it reports `help render --examples` as `Unknown help card 'render'.`. After a failed run (not a defective
catalog), when the first path segment (the path words read as HelpCards reads them, split by `Segments`) is a command
name or alias, `topics`, or `help` (`IsOwnSubject`, also used by `ResolveOwnSubject`), and the message of HelpCards is
exactly `Unknown help card '<segment>'.` (ignoring case), the failure is replaced by the tool's:
`'<segment>' has no examples; only help cards do.` with the hint
`Run 'canvasnet help <segment>' for its usage, or 'canvasnet help topics' to list the help cards.` HelpCards reports a
wrong option or combination first, so those failures are never replaced, and a different wording in a later HelpCards
keeps its own failure.

**RenderHelp(string? subject, bool json)** and **RenderHelp(IReadOnlyList<string> path, bool json)** (`public`,
`CommandRegistry.Help.cs`): render the help for a subject or a help path (with help options), as plain text or, with
`json`, as a JSON document; a failure throws a `ToolException` with the code, message, and hint of the run. They serve
the tests and callers that need the text.

The text help of a command is `Usage:` plus the command's usage text, the summary, the command's options, and the global
options. It ends with a `See also:` paragraph (`CommandRegistry.SeeAlso`, wrapped at 78 columns by `AppendWrapped`) that
names `canvasnet help <topic>`, every topic name, and `canvasnet help topics`, so the help of every command
(`render --help`, `formats --help`, `info --help`, ...) leads to the help cards. An option is listed as its aliases and
name followed by `<value name>` when it takes a value, with the descriptions aligned. The JSON help of a command is
written with `ResultWriter.Success("help", ...)`: `kind` `command`, `topic` (the canonical name), `aliases`, `summary`,
`usage`, `options`, and `globalOptions`, an option being
`{name, aliases, takesValue, valueName (only when it takes a value), description}`.
**CreateUnknownSubjectException(subject)** (private): builds the `ToolException` for an unknown first word of a help
path: code `usage-error`, message `Unknown help subject '{subject}'.`, `Command` set to `help`, and a hint. The hint
suggests the closest name (the edit distance of `ClosestName`, over every command name and alias, every root card name
and alias, `topics`, and `help`) followed by
`Valid subjects: <commands>, <topics>, topics, help. Run 'canvasnet help topics' for descriptions.`

**ClosestName(name, candidates)** (`internal static`): the candidate within an edit distance of two of the typed name,
the nearest first and ties in the order given, or `null`.
**CreateUnknownCommandException** (private): builds the `ToolException` for an unknown name: code
`usage-error`, message `Unknown command '{name}'.`, `Command` set to the name typed, and a hint. When the name is a
help topic name or alias (`canvasnet scad`), the hint is `'{name}' is a help topic: run 'canvasnet help {topic}'.`
followed by a pointer to `canvasnet --help`. Otherwise the hint suggests the closest command name or alias
(`ClosestName`: Levenshtein edit distance of at most two, ties resolved in registration order, `help` included as a
candidate) followed by a pointer to `canvasnet --help`.

#### Error Handling

The registry reports problems with the command line as `ToolException` with code `usage-error` (no command, an unknown
command, and for `help` an unknown subject, and a word after a command, `help`, or `topics`). When JSON was asked only
with `help ... --format json`, the help command returns those `help` failures as the failure document instead of
throwing them. The failures that HelpCards reports for `help` (an unknown card or example, an unknown or conflicting
option, an option without the card it needs) are written as the `Error` and `Hint` lines and, with `--json` or
`--format json`, the failure document, and make the tool exit with 1; a failure that HelpCards reports without a hint
(an option without its value, `--example` or `--examples` without a card path, and options that cannot be combined)
gets `HelpOptionsHint`; the examples of a command, `topics`, or `help` are reported with the tool's message that only
help cards have examples. A `ToolException` thrown by a command passes through with its details intact and the command
name recorded, and a `ViewerException` of the library becomes a `ToolException` that keeps its code, location, detail
code, and inner exception (see *Run*). Any other exception thrown by a command propagates unchanged: it is a defect,
and the entry point reports and re-throws it. Registration errors (`InvalidOperationException`,
`ArgumentNullException`) are programming errors detected at start-up.

#### Dependencies

- **Context** - the command name, the command arguments, the `Json` flag, and `WriteLine` and `WriteError` for the
  built-in help command.
- **ArgumentReader** - supplies the edit distance for suggestions.
- **ResultWriter** - the JSON help documents of the commands.
- **HelpTopics** - the combined catalog, the envelope and error object, the topics subject, and the topic list.
- **DemaConsulting.HelpCards** (OTS) - `HelpCli`, `HelpCliOptions`, `HelpCliSection`, `HelpCliAnswer`,
  `HelpCliError`, and `HelpCliExitCodes`.
- **ToolException** and **ErrorCodes** - the error report (documented in *Program Unit Design*).
- **ViewerException** (Viewer library) - the failures of the library that the registry reports with the command name.

#### Callers

- **Program** - `CreateDefault`, `Run`, and `RunHelp` (for `--help`).
- Tests - build registries with injected commands.
