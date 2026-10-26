## Program

<!-- cspell:ignore canvasnet Postconditions -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`Program` is the entry point and execution orchestrator for the CanvasNet tool. Its single responsibility is
to create a `Context` from the command-line arguments, dispatch to the appropriate action based on the parsed
flags and the command name, apply the tool's error policy, and return the exit code. The supporting types
`ToolException` and `ErrorCodes` (file `ToolException.cs`), which carry the error policy's stable error codes,
are documented here rather than as units of their own.

### Data Model

**Version**: `string` (static property) - The tool version read from `AssemblyInformationalVersionAttribute`
on every access, falling back to `AssemblyVersion`, then `"0.0.0"`. No caching is applied.

**ToolException** (`internal sealed`, derives from `InvalidOperationException`): the expected, user-correctable
failure. `Code` (`string`, one of the `ErrorCodes` constants), `Hint` (`string?`, a suggestion for correcting
the failure), and `Command` (`string?`, mutable; the name of the command that was running, filled in by the
command registry when it is empty), and `ResultFields` (`Action<Utf8JsonWriter>?`, init-only; optional top-level JSON
properties that `ResultWriter` appends after the `error` object of the JSON failure document, for a failure that
still produced a result, such as a comparison that missed a threshold). `ToolException` derives from
`InvalidOperationException` so that it follows the template's expected-failure path with no extra handling.

**ErrorCodes** (`internal static`): the stable error code constants: `usage-error`, `operation-failed`,
`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature`,
`page-out-of-range`, `size-limit`, `same-input-output`, `output-exists`, `output-write-failed`, `scad-error`,
`budget-exceeded`, `size-mismatch` (two images to compare differ in size and no size normalization was requested), and
`threshold-failed` (a comparison completed but a requested threshold was not met). Both new codes exit with code 1 like
every other failure. A value is never changed once released; new values may be added.

### Key Methods

**Main**: Entry point for the tool process.

- *Parameters*: `string[] args` - command-line arguments from the host environment.
- *Returns*: `int` - exit code; 0 for success, 1 for failure.
- *Postconditions*: equals `Execute(args, CommandRegistry.CreateDefault())`.

**Execute**: Runs the tool against a given command registry (a test seam for `Main`) with the error policy.

- *Parameters*: `string[] args`; `CommandRegistry registry`.
- *Returns*: `int` - 0 for success, 1 for an expected failure.
- *Postconditions*: the `Context` (if one was created) is disposed, after any error was reported through it.

Creates a `Context` using `Context.Create(args)`, remembers the command name, calls `Run(context, registry)`, and
returns `context.ExitCode`. Catches `ArgumentException` and `InvalidOperationException` (which includes
`ToolException`), reports them through `ReportExpected` (passing the context, if one exists, the raw arguments, and
the command name, if one was seen), and returns 1. Catches any other `Exception`, writes
`Unexpected error: {message}` through the context (standard error when there is none), and re-throws so the runtime
can record it. The context is held in a variable and disposed in a `finally` block rather than by a `using`
declaration, so that it is still open - and the `--log` file still receives the report - when the `catch` blocks run.

**Run**: Dispatches execution based on the parsed flags. The overload `Run(Context)` uses the default
registry; `Run(Context, CommandRegistry)` is the implementation.

- *Parameters*: `Context context`; `CommandRegistry registry`.
- *Preconditions*: `context` is not null.
- *Postconditions*: exactly one action has been taken.

Inspects the context in priority order: (1) the version flag writes the bare version and returns; (2) the help
flag prints the banner and then `registry.RenderHelp(context.Command)` - the top-level help when no command was
named, otherwise the help of that command; (3) the validate flag prints the banner and calls `Validation.Run`;
(4) a command name calls `registry.Run(context)`; (5) otherwise the banner and the hint
`Usage: canvasnet <command> [options] (try --help)` are written. The banner is therefore printed only for
help, self-validation, and an empty command line.

**ReportExpected(context, args, commandName, exception)**: Reports an expected failure. A `ToolException`
becomes the line `Error: {code}: {message}` followed, when the exception has a hint, by the line
`Hint: {hint}`; any other expected exception becomes the template's `Error: {message}`. No stack trace is written.
With a context, each line is written by `Context.WriteError` - so `--silent` suppresses it and `--log` records it,
like any other output - and, when the context has `--json`, the JSON failure document (`ResultWriter.Failure`) is
written as one line by `Context.WriteLine` (standard output unless `--silent`, and the log). Without a context
(a malformed global option, or a log file that cannot be opened) nothing can be honored: the lines go to
standard error, and the document goes to standard output when the raw argument array contains `--json` and does not
contain `--silent`; that scan is exact-match, so a `--json` that is the value of another option is also seen, which
is accepted. The command named in the document is the `ToolException.Command` when set, otherwise the command
name seen on the command line, otherwise `null`. A failure document is written only for expected failures: an
unexpected exception is a defect, written as `Unexpected error:` and re-thrown, with no document.

**PrintBanner**: Writes the tool name and version and the copyright line to `context`.

### Error Handling

`Execute` is the only place that handles exceptions. Expected failures (`ArgumentException`,
`InvalidOperationException`, `ToolException`) become a concise message and exit code 1. Any other exception is
treated as a defect: it is written as `Unexpected error: {message}` and re-thrown so the runtime records it.
Library and I/O exceptions that mean bad or unsupported user input are mapped to `ToolException` by the units
that read the input, using an explicit list of types; a blanket catch is deliberately not used, so that real
defects stay visible. `Run`, `PrintBanner`, and `ReportExpected` do not catch exceptions.

### Dependencies

- **Context** - parsed flags, command name, and the output channels.
- **CommandRegistry** (Commands subsystem) - command dispatch and help text.
- **Validation** - `Validation.Run(context)` for `--validate`.
- **ResultWriter** - the JSON failure document.

### Callers

- **Validation** - calls `Program.Run` in-process to exercise the version and help actions.
- The host environment calls `Main`; tests call `Execute` and `Run` directly.
