## Validation

<!-- cspell:ignore Postconditions -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`Validation` orchestrates the self-validation test suite behind `--validate`. Its single responsibility is to
run a fixed set of internal tests that exercise the tool's own functionality, print a summary to the provided
`Context`, and optionally serialize the results to a TRX or JUnit XML file. It does not define the tool's
requirements; it produces the qualification evidence that the tool behaves correctly in the deployment
environment, and the per-platform and per-runtime evidence that the platform requirements rely on.

### Data Model

`Validation` holds no instance state. The class is `internal static`; all state is local to `Run` and the
private test methods. The names of the self-tests are a contract with the platform requirements and are never
changed: `CanvasNetTool_VersionDisplay` and `CanvasNetTool_HelpDisplay`. Each is recorded as a `TestResult` with
`ClassName` "Validation" and `CodeBase` "CanvasNetTool". Further self-tests are added together with the commands
they exercise.

### Key Methods

**Run**: Entry point for the self-validation suite.

- *Parameters*: `Context context` - the output channel and results configuration.
- *Preconditions*: `context` is not null.
- *Postconditions*: a summary has been printed; if `context.ResultsFile` was set, a results file has been
  written or an error has been recorded for an unsupported extension.

Prints the header (a markdown heading at `context.HeadingDepth` and a table of tool version, machine name, OS,
runtime, and time stamp), constructs a `TestResults` object named "CanvasNet Tool Self-Validation", runs the
self-tests, prints the totals (using `WriteError` for the failed count when any test failed), and calls
`WriteResultsFile` if `context.ResultsFile` is set.

**RunVersionTest** and **RunHelpTest**: each creates a `TemporaryDirectory`, builds a log path with
`PathHelpers.SafePathCombine`, runs the tool in-process (`Context.Create` and `Program.Run`) with
`--silent --log {path} --version` or `--help`, reads the log, and checks it: the version log must contain a
dotted version number; the help log must contain both "Usage:" and "Options:". Any exception is recorded as a
test failure by `HandleTestException`.

**WriteResultsFile**: serializes the results to `context.ResultsFile`: `.trx` uses `TrxSerializer`, `.xml`
uses `JUnitSerializer`; any other extension calls `context.WriteError` and writes nothing.

**TemporaryDirectory** (nested class): creates a uniquely named directory under the system temporary
directory and deletes it on `Dispose`, ignoring cleanup errors.

### Error Handling

`Run` throws `ArgumentNullException` if `context` is null. Each self-test wraps its body in a broad
`catch (Exception)` - justified because this is a test framework in which any failure, including an unexpected
one, must be recorded as a failed test so the remaining tests still run and the evidence is complete.
`WriteResultsFile` catches file-write exceptions and reports them through `context.WriteError`. An unsupported
results extension is a user error: `WriteError` is called ("Unsupported results file format") and the exit code
becomes 1.

### Dependencies

- **Context** - output channel and results configuration.
- **Program** - `Program.Run` is called in-process to exercise the tool.
- **PathHelpers** - `SafePathCombine` builds log paths inside temporary directories.
- **DemaConsulting.TestResults** (OTS) - `TestResults`, `TestResult`, `TestOutcome`, and the
  `TrxSerializer` and `JUnitSerializer` of `TestResults.IO`. See *DemaConsulting.TestResults OTS Integration
  Design* (`ots/test-results.md`).

### Callers

- **Program** - calls `Validation.Run(context)` when the `--validate` flag is set.
