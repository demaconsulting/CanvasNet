## Context

### Verification Approach

`Context` is verified with unit tests in `ContextTests.cs`. Because `Context` depends only on .NET BCL types
(`Console`, `StreamWriter`), no mocking or test doubles are required. The tests call `Context.Create` with
controlled argument arrays, inspect the resulting properties and exit codes, and verify what is written to
captured console streams and temporary log files.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every global option sets its property, in any position, and is removed with its value from the command
  arguments.
- The command name and command arguments are captured as specified.
- `ArgumentException` is thrown for unknown options before the command name and for malformed global options.
- `ExitCode` is 1 after `WriteError` and 0 otherwise; silent mode suppresses console output but not the log file.

### Test Scenarios

**Context_Create_NoArguments_ReturnsDefaultContext**: an empty command line gives all flags off, no results
file, heading depth 1, no command, no command arguments, and exit code 0.

**Context_Create_VersionFlag_SetsVersionTrue**, **Context_Create_ShortVersionFlag_SetsVersionTrue**,
**Context_Create_HelpFlag_SetsHelpTrue**, **Context_Create_ShortHelpFlag_H_SetsHelpTrue**,
**Context_Create_ShortHelpFlag_Question_SetsHelpTrue**, **Context_Create_SilentFlag_SetsSilentTrue**, and
**Context_Create_ValidateFlag_SetsValidateTrue**: each flag sets its property; the version flag is not mistaken
for a command.

**Context_Create_ResultsFlag_SetsResultsFile** and **Context_Create_ResultAliasFlag_SetsResultsFile**: both
spellings capture the results file.

**Context_Create_LogFlag_OpensLogFile**: a silent context with `--log` writes a line to the log file only.

**Context_Create_DepthFlag_SetsHeadingDepth** and **Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth**:
`--depth 3` gives 3; the default is 1.

**Context_Create_CommandName_CapturesCommand**: `render` is the command with no arguments.

**Context_Create_NoCommand_CommandIsNull**: only global options (with values) leave the command null.

**Context_Create_CommandWithArguments_PassesArgumentsInOrder**: tokens after the command, including option-like
tokens and their values, are kept in order.

**Context_Create_UnknownOptionAfterCommand_PassesThroughToCommand**: options unknown to the global parser after
the command are passed through, not rejected.

**Context_Create_GlobalFlagAfterCommand_IsHonored**: `--silent` and `--help` after the command are consumed
and not passed on.

**Context_Create_GlobalFlagBetweenCommandArguments_IsRemovedFromArguments**: `--depth 4` between command
arguments is consumed with its value and the other tokens keep their order.

**Context_Create_OptionValueBeforeCommand_IsNotTheCommand**: in `--results out.trx info` the value belongs to
the option and the command is `info`.

**Context_Create_UnknownArgument_ThrowsArgumentException**: an unknown option before the command throws with
the message `Unsupported argument '--unknown'`.

**Context_Create_LogFlag_WithoutValue_ThrowsArgumentException**,
**Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException**,
**Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException**, and
**Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException**: a missing option value throws
`ArgumentException`.

**Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException**,
**Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException**, and
**Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException**: depth values that are not integers, below
1, or above 6 throw `ArgumentException`.

**Context_Create_NullArguments_ThrowsArgumentNullException**: a null argument array throws.

**Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException**: a log file in a missing directory throws
`InvalidOperationException` naming the file.

**Context_WriteLine_NotSilent_WritesToConsole** and **Context_WriteLine_Silent_DoesNotWriteToConsole**: output
goes to standard output only, and nothing is written when silent.

**Context_WriteError_NotSilent_WritesToConsole** and **Context_WriteError_Silent_DoesNotWriteToConsole**: errors
go to standard error only, and nothing is written when silent.

**Context_WriteError_SetsErrorExitCode**: after `WriteError` the exit code is 1 even when the message was
suppressed.

**Context_WriteError_WritesToLogFile**: a silent context's error still reaches the log file.
