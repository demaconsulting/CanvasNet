## Commands

<!-- cspell:ignore deskew scadd -->

### Verification Approach

The `Commands` subsystem is verified at two tiers. The unit tests of `CommandRegistry`, `HelpTopics`, `DiscoveryCommands`,
`RenderCommand`, `CompareCommand`, `MontageCommand`, `InspectCommand`, and `FlattenCommand` (see
_CommandRegistry Unit Verification Design_, _HelpTopics Unit Verification Design_, _DiscoveryCommands Unit Verification
Design_, _RenderCommand Unit
Verification Design_, _CompareCommand Unit Verification Design_, _MontageCommand Unit Verification Design_,
_InspectCommand Unit Verification Design_, and _FlattenCommand Unit Verification Design_
under `commands/`)
verify the registry and the commands in isolation. The subsystem tests in
`CommandsSubsystemTests.cs` verify the registry working with `Context` and `Program` through the program's
entry point, with a registry holding `TestCommand` instances (a configurable `ICommand` defined in the test
project): resolution by name and alias, argument hand-off with global options mixed in, error context, the
unknown-command report, and help; and, with the default registry, that the shipped `formats`, `info`, `render`,
transform verb, `compare`, `montage`, `inspect`, and `flatten` commands are registered, run, and are listed in the help,
and that
the help topics are shown by name, by alias, and as JSON, their examples are printed, and an unknown help subject is
reported. The shipped
commands are verified by their own unit tests. The content of the help topics, which the help requirement of the
subsystem takes from its `HelpTopics` children (among them the `mermaid` topic, and its parity with the Mermaid user
guide: the same class, state, and entity relationship diagram syntax, relation or cardinality, and setting rows and
the same unsupported-feature and warning codes,
item by item and in order), is verified by the `HelpTopics` unit tests, the parity by
`HelpTopics_MermaidTopic_DiagramSyntaxItems_MatchUserGuide`.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A command runs with exactly its own arguments; global options never reach it.
- An unknown command is a usage error with a suggestion and exit code 1.
- Help lists the commands and the help topics, shows each command's usage and options without running it, and prints a
  topic, an example, or a JSON help document on request; an unknown help subject is a usage error that lists the valid
  subjects.
- The `mermaid` help topic and the Mermaid user guide give the same class, state, and entity relationship diagram rows
  and diagnostic codes.

### Test Scenarios

**CommandsSubsystem_Dispatch_RegisteredCommand_RunsWithItsArguments**: a command is run through its alias with
one argument; its output appears and it received exactly the argument.

**CommandsSubsystem_Dispatch_GlobalOptionsMixedIn_AreNotPassedToCommand**: global options (a flag and an option
with a value) among the command arguments are consumed; the command receives only its own tokens, and `--silent`
suppresses its output.

**CommandsSubsystem_Dispatch_CommandThrowsToolException_RecordsCommandName**: a `ToolException` thrown by a
command escapes `Program.Run` with its code intact and the command name recorded.

**CommandsSubsystem_UnknownCommand_Mistyped_ReportsUsageErrorWithSuggestion**: a mistyped command returns exit
code 1, with nothing on standard output and a usage error and a "Did you mean" hint on standard error.

**CommandsSubsystem_Help_TopLevel_ListsRegisteredCommands**: `--help` lists each command with its aliases and
summary, and the global options.

**CommandsSubsystem_Help_CommandHelp_ShownByFlagAndByHelpCommand**: `render --help` and `help render` both show
the command's usage and options, and the command itself does not run.

**CommandsSubsystem_Help_UnknownCommand_ReportsUsageError**: `help nothing` returns exit code 1 with the usage error
`Unknown help subject 'nothing'.` (the run path `nothing` still reports `Unknown command`).

**CommandsSubsystem_HelpTopics_SubjectsResolve_ByNameAliasAndFlag**: `help scad`, `help OpenSCAD`, `scad --help`, and
`help topics` all exit 0; the topic is shown by name and by alias with the same text (the flag form adds only the
banner), and `help topics` is the index with the `Help topics:` list naming every topic.

**CommandsSubsystem_HelpTopics_Json_IsOneLineWithoutBannerOrAnsi**: `help scad --json`, `--help --json`, and
`scad --help --json` each exit 0 with nothing on standard error and write exactly one JSON line without banner or escape
sequence: a `help` document of the kind `topic`, `index`, and `topic`.

**CommandsSubsystem_HelpTopics_Example_PrintsOnlyTheSource**: `help scad --example plate` exits 0 and writes exactly the
source of the example and one line end to standard output, and nothing to standard error.

**CommandsSubsystem_HelpTopics_UnknownSubject_ReportsUsageErrorWithValidSubjects**: `help scadd` returns exit code 1,
nothing on standard output, and on standard error the usage error `Unknown help subject 'scadd'.` with the suggestion
`scad` and the valid subjects; `help nothing --json` returns 1 and writes a failure document for the command `help` with
the code `usage-error`.

**CommandsSubsystem_Formats_DefaultRegistry_RunsFormatsCommand**: with the default registry, `formats` writes the
text listing (no banner, nothing on standard error) and `formats --json` writes the success document.

**CommandsSubsystem_Formats_DefaultRegistry_ListedInHelp**: the top-level help lists `formats` with its summary,
and `formats --help` shows its usage and JSON example.

**CommandsSubsystem_Imaging_DefaultRegistry_RunsInfoAndRender**: with the default registry, `info`, `render`, and
`convert` on a generated PNG each succeed with their own output (no banner, nothing on standard error).

**CommandsSubsystem_Imaging_DefaultRegistry_ListedInHelp**: the top-level help lists `render, convert` and `info`
with their summaries; `render --help`, `help convert`, and `info --help` show the usage and options.

**CommandsSubsystem_TransformVerbs_DefaultRegistry_RunAndAreListedInHelp**: with the default registry, `crop`,
`resize`, `rotate`, `flip`, and `pad` on a generated PNG each succeed with one result line and nothing on standard
error (with the sizes 6x4, 8x12, and 16x12 for the crop, the rotation, and the border); `help pad` shows its usage line
and the top-level help lists the verbs with their summaries.

**CommandsSubsystem_Comparison_DefaultRegistry_RunsCompareAndMontage**: with the default registry, `compare` with a
threshold that is met and `montage` on two generated PNG images each succeed with their own output (no banner,
nothing on standard error; the sheet is 88x48 for two 32x32 cells), and `compare` with a threshold that is not met
returns exit code 1 with the expected error `threshold-failed` on standard error.

**CommandsSubsystem_Comparison_DefaultRegistry_ListedInHelp**: the top-level help lists `compare` and `montage` with
their summaries; `help compare` and `montage --help` show the usage and the options (`--resize-to <a|b|WxH>`,
`-o, --output <file>`).

**CommandsSubsystem_Inspect_DefaultRegistry_RunsInspectAndIsListedInHelp**: through the default registry `inspect colors`
and `inspect pixel --json` each succeed with nothing on standard error (the JSON names the command and the
subcommand), an unknown subcommand is an expected `usage-error` on standard error with exit code 1, the top-level help
lists `inspect` with its summary, and `inspect --help` and `help inspect` show both usage lines and the options of
`render` such as `--deskew` and `--radius <R>`.

**CommandsSubsystem_Flatten_DefaultRegistry_RunsFlattenAndListsItInHelp**: through the default registry `flatten`
writes the copy of a drawing that links a Mermaid diagram with nothing on standard error, a missing output is an
expected `usage-error` on standard error with exit code 1, the top-level help lists `flatten` with its summary, and
`help flatten` shows its usage and `--scale <factor>`.

**CommandsSubsystem_FileAccess_EveryReadingCommand_AcceptsTheOptionsAndDeniesAnOutsidePicture**: through the default
registry, `render`, `rotate`, `inspect colors`, `compare`, `montage`, and `flatten` of a drawing that links a Mermaid
diagram in the folder above it, each with `--file-access roots --json`, exit with code 0 and report the
`image-access-denied` note.

**CommandsSubsystem_FileAccess_Info_HasNoFileAccessOption**: `info` with `--file-access roots` exits with code 1 and
reports the `usage-error` "Unknown option '--file-access' for 'info'.".

**CommandsSubsystem_FileAccess_SourceTree_EveryProbeAndRequestCarriesThePolicy**: every C# file of the tool is scanned;
each statement that calls `SourceLoader.Probe(` or builds a `SourceOptions`, `FlattenRequest`, or `MontageRequest` (at
least eleven are found) names a file-access policy.
