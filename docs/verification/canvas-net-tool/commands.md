## Commands

### Verification Approach

The `Commands` subsystem is verified at two tiers. The unit tests of `CommandRegistry` (see _CommandRegistry
Unit Verification Design_ under `commands/`) verify the registry in isolation. The subsystem tests in
`CommandsSubsystemTests.cs` verify the registry working with `Context` and `Program` through the program's
entry point, with a registry holding `TestCommand` instances (a configurable `ICommand` defined in the test
project): resolution by name and alias, argument hand-off with global options mixed in, error context, the
unknown-command report, and help. The shipped commands are verified by their own unit tests.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A command runs with exactly its own arguments; global options never reach it.
- An unknown command is a usage error with a suggestion and exit code 1.
- Help lists the commands and shows each command's usage and options without running it.

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

**CommandsSubsystem_Help_UnknownCommand_ReportsUsageError**: `help nothing` returns exit code 1 with a usage
error.
