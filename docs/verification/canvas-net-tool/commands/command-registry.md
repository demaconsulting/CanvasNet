### CommandRegistry

#### Verification Approach

`CommandRegistry` is verified with unit tests in `CommandRegistryTests.cs`. The tests build registries holding
`TestCommand` instances (a configurable `ICommand` defined in the test project, which records the arguments it
received and can be made to throw), so no mocking framework is needed and no shipped command is involved.
`Context` instances are created from real argument arrays. One test runs the real `render` command of the default
registry on OpenSCAD models in a temporary folder, to show the codes of the library's shared mapping in the words of the
command line. The help tests use the real, embedded catalog of help cards (`HelpTopics`, verified by its own unit
tests) and the real `HelpCli` of the OTS library `DemaConsulting.HelpCards`, and capture the standard output, the
standard error, and the exit code of the context of the `help` command with `ToolHost`.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because some capture console
output.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Names and aliases resolve ignoring case; duplicates and the reserved name `help` are rejected.
- A command runs with exactly the command arguments; a `ToolException` gets the command name; a `ViewerException` of
  the library keeps its code, message, location, detail code, and inner exception, names the command, and has its hint
  in the words of the command line; any other exception propagates unchanged.
- An unknown command produces a usage error with a suggestion only when one is within an edit distance of two.
- Help text lists commands, aliases, summaries, options, the help topics, and the global options.
- Help resolves a command or a topic by name or alias; the examples of a topic can be listed or printed (as the exact
  source, or as JSON), cards can be found by a query or a tag, and every misuse is a usage error; an unknown subject
  suggests the closest name and lists the valid subjects, and a topic name typed as a command points at `help`.
- Help can be rendered as JSON (index, command, topics, card, examples, example, search, and cards documents) in the
  tool's envelope with the documented fields, asked with `--json` or `--format json` in any case, and every failure,
  with either, is the tool's failure document (`error` with `code`, `message`, and `hint`, no `kind`), the `Error` and
  `Hint` lines on standard error, and exit code 1.

#### Test Scenarios

**CommandRegistry_CreateDefault_ReturnsRegistry**: the default registry exists, contains the shipped commands in order
(`formats`, `info`, `render` with its alias `convert`, `crop`, `resize`, `rotate`, `flip`, `pad`, `compare`, `montage`,
and
`inspect`), and
every command in it resolves by its own name.

**CommandRegistry_Register_TwoCommands_KeepsRegistrationOrder**: the command list is in registration order.

**CommandRegistry_Register_DuplicateName_ThrowsInvalidOperationException**,
**CommandRegistry_Register_DuplicateAlias_ThrowsInvalidOperationException**,
**CommandRegistry_Register_AliasRepeatsOwnName_ThrowsInvalidOperationException**, and
**CommandRegistry_Register_ReservedHelpName_ThrowsInvalidOperationException**: a duplicate name (in another
case), an alias that collides with a registered name, an alias that repeats the command's own name, and the
reserved name `help` (as a name and as an alias) are rejected.

**CommandRegistry_Register_NullCommand_ThrowsArgumentNullException**: a null command is rejected.

**CommandRegistry_Find_NameOrAlias_ReturnsCommand** and **CommandRegistry_Find_UnknownName_ReturnsNull**: the
name and the alias resolve regardless of case; an unknown name resolves to nothing.

**CommandRegistry_Run_RegisteredCommand_RunsCommandWithArguments**: the command selected by alias receives the
command arguments.

**CommandRegistry_Run_NoCommand_ThrowsUsageError** and **CommandRegistry_Run_NullContext_ThrowsArgumentNullException**:
no command is a usage error with a hint; a null context is rejected.

**CommandRegistry_Run_UnknownCommandNearAName_SuggestsClosestName**,
**CommandRegistry_Run_UnknownCommandNearAnAlias_SuggestsTheAlias**, and
**CommandRegistry_Run_UnknownCommandFarFromAll_HasNoSuggestion**: a typo gets a "Did you mean" hint naming the
closest command or alias; a name far from every command gets only the pointer to the help; the exception names
the typed command.

**CommandRegistry_Run_ToolExceptionWithoutCommand_FillsCommandName** and
**CommandRegistry_Run_ToolExceptionWithCommand_KeepsCommandName**: the canonical command name is recorded on a
`ToolException` that lacks one and an existing name is kept; code and message are intact.

**CommandRegistry_Run_UnexpectedException_PropagatesUnchanged**: the same exception instance escapes.

**CommandRegistry_Run_ViewerException_KeepsLocationAndDetailCode**: a `ViewerException` thrown by a command becomes a
`ToolException` naming the command with the same code, message, location (`model.scad` line 2 column 1), detail code,
and inner exception, and with the hint "Without --strict ..." instead of "Without strict mode ...".

**CommandRegistry_CommandLineHint_LibraryPhrases_NameTheOptions**: every phrase of the library's hints is worded with
the option of the tool (`--strict`, `--max-triangles`, `--scale`, `--width`, `--height`), a hint without such a phrase
is kept, and a `null` hint stays `null`.

**CommandRegistry_Run_RenderOfFailingModels_ReportsLibraryCodesInCommandLineWords**: the `render` command of the
default registry reports an OpenSCAD syntax error as `scad-error` with `scad-syntax-error` at line 2, an unsupported
feature with `--strict` as `unsupported-feature` located at line 1 with a hint that names `--strict`, and a model over
`--max-triangles 1` as `budget-exceeded` with a hint that names `--max-triangles`; each names the command and writes no
output.

**CommandRegistry_Run_RenderOfOversizePictures_NamesTheSizeOptions**: the `render` command of the default registry
reports an SVG file 20000 pixels wide rendered at its natural size as `size-limit` with the hint "Use --scale 0.4 or
less, or a smaller --width or --height.", and an SVG file 10 by 100 pixels rendered with `--width 4000` as `size-limit`
with the hint "Use a smaller --scale, --width, or --height."; neither writes an output.

**CommandRegistry_Run_HelpCommandWithName_PrintsCommandHelp** and
**CommandRegistry_Run_HelpCommandWithoutName_PrintsTopLevelHelp**: the built-in help command prints a command's
help, or the top-level help.

**CommandRegistry_RenderHelp_NoCommand_ListsCommandsAndGlobalOptions**: the top-level help starts with the usage,
lists commands with aliases and summaries, lists every global option (including `--json`), and has no trailing
newline.

**CommandRegistry_RenderHelp_EmptyRegistry_OmitsCommandSection**: with no command the command section is
omitted.

**CommandRegistry_RenderHelp_Command_ShowsUsageSummaryAndOptions**: a command's help (requested by alias) shows
its usage, summary, options with value placeholders, and the global options.

**CommandRegistry_RenderHelp_HelpName_ReturnsTopLevelHelp**: the name `help` gives the top-level help.

**CommandRegistry_RenderHelp_UnknownCommand_ThrowsUsageError**: help for an unknown subject is a usage error.

**CommandRegistry_RenderHelp_Topic_ReturnsTopicHelp** and **CommandRegistry_RenderHelp_TopicAlias_EqualsCanonical**: a
topic name gives its root card (`# SVG drawings (.svg)`, the `## Cards` list with `canvasnet help svg embedding`, and
`## See also`) and not a command help; an alias, in any case, gives the same text and the same JSON document as the
canonical name.

**CommandRegistry_Run_HelpCardPath_PrintsTheCard**: `help mermaid gantt`, `help MMD gantt`, and `help mermaid/gantt`
print the same card text, `--json` gives its card document, and `RenderHelp` with the path gives the same text.

**CommandRegistry_Run_HelpUnknownCard_ListsChildren**: `help mermaid giant` exits with 1, writes nothing on standard
output and the two lines `Error: usage-error: Unknown help card 'giant' under 'mermaid'.` and
`Hint: Did you mean 'gantt'? Cards under 'mermaid': flowchart, ...` on standard error; with `--json` it writes the same
lines and the failure document with exactly the keys `schemaVersion` (2), `ok` (false), `command` (`help`), and
`error` (with exactly `code` `usage-error`, `message`, and `hint`); and `RenderHelp` throws the same usage error with
the command `help`.

**CommandRegistry_Run_HelpPathAfterCommand_ThrowsUsageError**: a word after a command, after `topics`, or after `help`
(`help help help mermaid`) is the usage error `'<word>' has no help cards below it.`.

**CommandRegistry_RenderHelp_LeadingHelpWord_IsIgnored**: `help help mermaid` prints the `mermaid` card.

**CommandRegistry_RenderHelp_Topics_ListsEveryCard** and **CommandRegistry_RenderHelp_TopLevel_ListsHelpTopics**: the
subject `topics` (in any case) gives the tree of every card as text (with the command of every card) and as JSON (the
envelope, the kind `topics`, and entries with `id`, `aliases`, `tags`, `summary`, and `command`), which differs from
the index and names `canvasnet help mermaid gantt settings`. The index shows the usage line of `help` with its card
path and help options, the `Help options:` section, the `Help topics:` list with the command of every root, the
pointers to `canvasnet help topics`, `canvasnet help style`, and `canvasnet help composition`, and the footer of
HelpCards.

**CommandRegistry_RenderHelp_Json_Command_HasOptions** and
**CommandRegistry_RenderHelp_Json_Index_ListsCommandsAndTopics**: the JSON help of a command (asked for by alias) has
the kind `command`, the canonical name, aliases, summary, usage, the options (with `takesValue`, and the `valueName`
only for an option that takes a value), and the global options; the JSON index (the same for no subject and for `help`)
starts with the keys of the envelope, `kind` (`index`), `usage` (a string), `commands`, `topics` (every root card with
its aliases and summary), and `globalOptions`, followed by the keys HelpCards adds.

**CommandRegistry_Run_HelpExample_WritesOnlyTheSource**: `help svg --example shapes` writes the exact source of the
example and one line end, and with `--json` the example document, whose `id` is the card that was asked for (`svg`)
and whose `example.card` is the card that holds the example (`svg/supported`).

**CommandRegistry_RenderHelp_ExampleBelowTheCard_KeepsTheRequestedCardAsId**: the JSON documents of
`help svg --example shapes` and `help svg supported --example shapes` have the keys `schemaVersion`, `ok`, `command`,
`kind`, `id`, and `example` in that order, the `id` of the card that was asked for (`svg` and `svg/supported`), and the
same `example.card` (`svg/supported`), and are the same text apart from the `id`, so the order and escaping of the
fields of HelpCards are kept.

**CommandRegistry_RenderHelp_HelpOptionValueAfterEqualsSign_IsAccepted**: `--example=shapes` (as text, as JSON, and with
the global `--json`), `--search=gantt`, `--tag=mathml`, `--format=json`, and `--width=60` give the same answers as the
value in the next word; after `--` the word `--example=shapes` is a path word, and so the usage error of an unknown
subject.

**CommandRegistry_Run_HelpExamples_PrintsTheCardsExamplesInFull**: `help svg flatten --examples` writes the examples
text of that card with the content of each of its examples; `help svg --examples` writes the examples text of `svg`,
which has the `--examples` command of every card below it that has examples and none of the content of the
`svg flatten` example, and with `--json` the examples document that lists those cards.

**CommandRegistry_RenderHelp_ExamplesOnCardWithoutExamples_GivesEmptyLists**: `help mermaid differences --examples`
says that the card has no examples of its own, and its JSON document has empty `examples` and `cards`.

**CommandRegistry_Run_HelpExampleUnknown_ThrowsUsageErrorWithNames**: an unknown example name is a usage error that
names the card and the example (`The help card 'svg' has no example 'nothing'.`) and lists the valid example names in
the hint.

**CommandRegistry_RenderHelp_WrongHelpOptions_AreUsageErrors**: `--example` or `--examples` on a command or on `topics`,
either without a subject, both together, an unknown card (`svg extra`), an unknown option (`--bogus`), an empty value
(`--example=`), a flag with a value (`--examples=x`), and `--lang` are usage errors of the command `help`.

**CommandRegistry_Run_HelpArgumentErrorsWithoutAHint_GetTheHelpOptionsHint**: `help --example x`, `help --examples`,
`help help --examples`, `help svg --examples --example x`, `help --search gantt --examples`, `help svg --example`, and
`help --tag` exit with 1, write nothing on standard output, and write `Error: usage-error: <HelpCards' message>` and
`Hint: Run 'canvasnet help' for the help options, or 'canvasnet help topics' to list the help cards.` on standard
error. With the global `--json` and with `--format json`, they write the same failure document, whose error keys are
`code`, `message`, and `hint`. `RenderHelp` throws the same message and hint.

**CommandRegistry_Run_HelpExamplesOfOwnSubject_SaysOnlyHelpCardsHaveExamples**: `help render --examples`,
`help convert --example x` (an alias), and `help topics --examples` exit with 1, write nothing on standard output, and
write `Error: usage-error: '<word>' has no examples; only help cards do.` and
`Hint: Run 'canvasnet help <word>' for its usage, or 'canvasnet help topics' to list the help cards.` on standard
error; with the global `--json` and with `--format json` they write the same failure document with that message and
hint; `RenderHelp` throws the same message; an unknown option (`--bogus`) and two modes together still report their own
error first.

**CommandRegistry_Run_HelpFormatJsonInAnyCase_GivesTheToolsOwnDocuments**: `help render --format JSON`,
`help topics --format Json`, and `help svg --format JSON` write the same documents as `--json` (the `command` document
of `render`, the `topics` document, and the card document).

**CommandRegistry_Run_HelpFormatJsonFailureOfOwnSubject_WritesTheFailureDocument**: `help nosuch --format json` and
`help render extra --format json` exit with 1, write exactly the `Error: usage-error: ...` and `Hint: ...` lines of the
unknown subject and of a word after a command on standard error, and write the tool's failure document of that error
once on standard output (keys `schemaVersion`, `ok`, `command`, `error`; error keys `code`, `message`, `hint`); with the
global `--json` the registry still throws and writes nothing itself (the entry point writes the document), and
`RenderHelp` with `--format json` throws the usage error of the command `help`.

**CommandRegistry_RenderHelp_SearchTagAndAll_AnswerInTheToolEnvelope**: `--search gantt`, `--tag mathml`, and `--all`
with `--json` give the documents `search`, `cards`, and `index` in the tool's envelope; the search finds
`mermaid/gantt`, the tag list holds only cards with the tag, the whole index lists every card, and the text search names
`canvasnet help mermaid gantt`.

**CommandRegistry_SplitTextError_DocumentedFormat_GivesMessageAndHint**: the text error of HelpCards splits into the
message after `Error: ` and the hint on the next line; text in another form is the message.

**CommandRegistry_Run_HelpUnknownSubject_SuggestsClosestAndListsValidSubjects** and
**CommandRegistry_Run_HelpUnknownSubjectFarFromAll_HasNoSuggestionButListsSubjects**: an unknown subject is the usage
error `Unknown help subject '...'.` with the command `help`; a mistyped topic (`scadd`) gets a "Did you mean 'scad'?"
suggestion, a subject far from every name gets none, and both list the valid subjects (the commands, the topics, and
`topics`).

**CommandRegistry_Run_TopicNameAsCommand_HintsAtHelp**: a topic name or alias run as a command (`openscad`) is still the
error `Unknown command '...'.`, and its hint points at `canvasnet help scad`.
