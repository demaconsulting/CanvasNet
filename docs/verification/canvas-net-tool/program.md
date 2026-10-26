## Program

### Verification Approach

`Program` is verified with unit tests in `ProgramTests.cs`. `Run` is exercised in-process with a `Context`
created from real arguments and a `CommandRegistry`; where the shipped command set must not matter, the tests
register a `TestCommand` (a configurable `ICommand` defined in the test project). `Execute` and `Main` are
exercised through the `ToolHost` helper, which captures standard output and standard error and restores the
console afterward. No mocking framework is needed.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Exit code 0 is returned for valid invocations and exit code 1 for invalid arguments, an unknown command, and
  every expected failure.
- Expected failures are reported on standard error as specified, without a stack trace.
- An unexpected exception is reported and re-thrown unchanged.
- The banner appears for help, self-validation, and an empty command line, and never for a command.

### Test Scenarios

**Program_Run_WithVersionFlag_DisplaysVersionOnly**: `Run` with `--version` writes the version, no banner, no
copyright; exit code 0.

**Program_Run_WithShortVersionFlag_DisplaysVersion**: `Run` with `-v` writes the version.

**Program_Version_ReturnsNonEmptyString**: `Program.Version` is non-empty.

**Program_Run_WithHelpFlag_DisplaysUsageInformation**: `Run` with `--help` writes the banner, "Usage:",
"Options:", `--version`, and `--help`.

**Program_Run_WithShortHelpFlag_DisplaysUsage** and **Program_Run_WithQuestionMarkFlag_DisplaysUsage**: `-h` and
`-?` show the usage and options.

**Program_Run_WithCommandAndHelpFlag_DisplaysCommandHelp**: `greet --help` shows the command's own usage,
summary, and options.

**Program_Run_WithValidateFlag_RunsValidation**: `Run` with `--validate` writes the "Total Tests:" summary;
exit code 0.

**Program_Run_WithCommand_DispatchesToRegisteredCommand**: `greet input.png` runs the registered command with
the argument and shows its output.

**Program_Run_NoArguments_DisplaysBannerAndUsageHint**: an empty command line writes the banner, the copyright
notice, and the usage hint; exit code 0.

**Program_Run_WithCommand_DoesNotPrintBanner**: running a silent command writes nothing at all.

**Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode**: `Main` with an unknown option returns 1 and reports
`Unsupported argument`.

**Program_Execute_SuccessfulCommand_ReturnsZero**: a succeeding command returns 0 with empty standard error.

**Program_Execute_UnknownCommand_ReturnsOneWithUsageError**: an unknown command returns 1 and reports
`Error: usage-error: Unknown command 'bogus'.`.

**Program_Execute_ToolException_ReportsCodeMessageAndHint**: a `ToolException` with a hint is reported as exactly
two lines, `Error: {code}: {message}` and `Hint: {hint}`; exit code 1.

**Program_Execute_ToolExceptionWithoutHint_ReportsSingleLine**: without a hint, exactly one line.

**Program_Execute_InvalidOperationException_ReportsMessageOnly** and
**Program_Execute_ArgumentException_ReportsMessageOnly**: other expected exceptions are reported as
`Error: {message}`; exit code 1.

**Program_Execute_UnexpectedException_ReportsAndRethrows**: a `NotSupportedException` from a command escapes
`Execute` unchanged, after `Unexpected error: {message}` was written to standard error.

**Program_Execute_JsonToolException_WritesErrorDocumentToStdOut**: with `--json`, a `ToolException` with a hint
is also written to standard output as exactly the expected failure document (code, message, hint, command, no
exit code), while the text report is still on standard error; exit code 1.

**Program_Execute_JsonBadGlobalOption_WritesErrorDocumentWithNullCommand**: `--json --bogus` fails before any
command is selected; the document is a `usage-error` with `"command":null`.

**Program_Execute_JsonInvalidOperationException_ReportsOperationFailedForCommand**: a plain
`InvalidOperationException` is `operation-failed`, and the command name comes from the command line.

**Program_Execute_JsonSilentOrAbsent_WritesNoErrorDocument**: with `--silent`, or without `--json`, standard output
stays empty, and with `--silent` standard error is empty too; without `--json` the text report is still on standard
error; exit code 1.

**Program_Execute_SilentExpectedError_SuppressesConsoleOutput**: a `ToolException` with a hint, an
`InvalidOperationException`, and an `ArgumentException`, each run with `--silent` (before or after the command name),
exit with code 1 and write nothing to either stream.

**Program_Execute_LogExpectedError_CapturesReport**: with `--log`, the file holds exactly what standard error
received (the `Error:` and `Hint:` lines); the file is already closed, so it can be read.

**Program_Execute_SilentLogJsonExpectedError_WritesOnlyToLog**: with `--silent --json --log`, both console streams are
empty and the log holds the text report followed by the JSON failure document.

**Program_Execute_LogUnexpectedException_CapturesReportAndRethrows**: the `Unexpected error:` line is in the log as
well as on standard error, and the exception still escapes.

**Program_Execute_LogCannotBeOpened_ReportsOnConsole**: a log file in a folder that does not exist makes the failure
(`Error: Failed to open log file ...`) go to standard error, because no context exists; the command does not run;
exit code 1.

**Program_Execute_JsonUnexpectedException_WritesNoDocumentAndRethrows**: with `--json`, an unexpected exception
still escapes, is reported on standard error, and no document is written.
