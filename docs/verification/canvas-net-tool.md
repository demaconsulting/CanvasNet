# System Verification Design

<!-- cspell:ignore canvasnet -->

This document describes the system-level verification strategy for CanvasNetTool.

## Verification Approach

System-level verification uses two kinds of tests in `CanvasNetToolTests.cs`, which treat the tool as a black
box and assert only on observable behavior.

- **Out-of-process tests** run the built tool as a real process through the `Runner.Run` helper
  (`dotnet DemaConsulting.CanvasNet.Tool.dll ...`), exercising the full stack - argument parsing, dispatch,
  execution, and output - and validating the exit code and the combined console output. `Runner.Run` merges
  standard output and standard error, so assertions about one stream alone cannot be made at this level.
- **In-process tests** run `Program.Execute` through the `ToolHost` helper with a command registry that holds
  a purpose-made `TestCommand`. They cover behavior that needs the two streams examined separately, or a
  command that fails or misbehaves on demand: the clean-output rule, the concise error report, command
  dispatch with global options in any position, and the re-throw of an unexpected exception.

Platform and runtime support is proven differently: the integration-test job of the build workflow installs the
packaged tool in each operating system and .NET runtime of its matrix and runs `canvasnet --validate` with a
results file; the self-test results (`CanvasNetTool_VersionDisplay` and `CanvasNetTool_HelpDisplay`) are the
evidence for the platform requirements, selected by the source filters in the platform requirements file.
The same job guards the package contents (no dependency on sibling CanvasNet packages, library assemblies
bundled for every target framework).

## Test Environment

System tests run on .NET 8.0, .NET 9.0, and .NET 10.0 on Windows, Linux, and macOS in CI; all scenarios are
expected to produce identical results everywhere. Out-of-process tests need the tool assembly and its
libraries in the test output directory, which the test project's project references provide. Temporary files
are created in the system temporary directory and removed by each test. Tests that redirect the console
belong to the "Sequential" collection because console redirection is process-wide.

## Acceptance Criteria

- All system tests pass with zero failures on every supported runtime and platform.
- Exit code 0 is returned for every valid invocation and exit code 1 for every invalid argument, unknown
  command, and expected failure.
- Expected failures are one-line messages on standard error with a stable error code and no stack trace; an
  unexpected exception is reported and re-thrown.
- A command's standard output contains only the command's own output.
- Results files are created at the requested paths when `--results` is used with `--validate`.
- Silent mode produces no console output.

## Test Scenarios

**CanvasNetTool_VersionFlag_Provided_OutputsVersion**: `--version` is the sole argument; the output is a
version number with no banner, copyright, or error text, and the exit code is 0.

**CanvasNetTool_HelpFlag_Provided_OutputsUsageInformation**: `--help` is the sole argument; the output contains
"Usage:", "Options:", `--version`, and `--help`, and the exit code is 0.

**CanvasNetTool_NoArguments_Invoked_DisplaysBanner**: the tool is invoked with no arguments; the output
contains the banner, the copyright notice, and the usage hint, and the exit code is 0.

**CanvasNetTool_ValidateFlag_Provided_RunsValidation**: `--validate` is the sole argument; the output contains
the "Total Tests:" and "Passed:" summary and the exit code is 0.

**CanvasNetTool_ValidateWithTrxResults_Requested_GeneratesTrxFile**: `--validate` with `--results <path>.trx`;
a TRX file containing a `TestRun` element is created and the exit code is 0.

**CanvasNetTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile**: `--validate` with `--results <path>.xml`;
a JUnit XML file containing a `testsuites` element is created and the exit code is 0.

**CanvasNetTool_ResultAlias_LegacyFlag_WritesResultsFile**: `--validate` with the legacy `--result <path>.trx`;
the file is created and the exit code is 0.

**CanvasNetTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero**: `--validate` with a results path
with an unsupported extension; the exit code is non-zero and no file is created.

**CanvasNetTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel**: `--validate --depth 3`; the report
heading has three hashes and the exit code is 0.

**CanvasNetTool_SilentFlag_Provided_SuppressesOutput**: `--version --silent`; the output is empty and the exit
code is 0.

**CanvasNetTool_LogFlag_Provided_WritesOutputToFile**: `--log <path>`; the log file is created, contains the
banner, and the same text appears on the console.

**CanvasNetTool_UnknownArgument_Provided_ReturnsError**: an unknown option (`--unknown`) before any command;
the exit code is non-zero and the output contains an error naming the option.

**CanvasNetTool_UnknownCommand_Provided_ReturnsConciseUsageError**: an unknown command name; the exit code is 1
and the output contains `Error: usage-error: Unknown command 'bogus'` with no stack trace.

**CanvasNetTool_CommandOutput_CommandRuns_HasNoBanner**: a command that writes one line runs in-process;
standard output is exactly that line and standard error is empty.

**CanvasNetTool_CommandDispatch_GlobalFlagsAnywhere_RunsCommandWithItsArguments**: `--silent` precedes the
command name and the command arguments include an option the global parser does not know; the command receives
exactly its own tokens and the global flag is honored.

**CanvasNetTool_ExpectedFailure_CommandThrowsToolException_ReportsConciseError**: a command throws a
`ToolException` with a hint; the exit code is 1, standard output is empty, standard error carries the code,
message, and hint, and no stack trace.

**CanvasNetTool_UnexpectedError_CommandThrowsUnexpectedException_IsReThrown**: a command throws an exception
that is not an expected failure; the same exception escapes the entry point and `Unexpected error:` was
written to standard error first.
