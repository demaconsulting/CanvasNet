## Context

<!-- cspell:ignore Postconditions -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`Context` handles command-line argument parsing and program output for one tool invocation. Its single
responsibility is to split the command line into the global options, the command name, and the command
arguments, expose them as read-only properties, own the output channels (standard output, standard error,
and the optional log file), and derive the exit code from whether any error was reported.

### Data Model

**_logWriter**: `StreamWriter?` - Log file writer; `null` when logging is not active.

**_hasErrors**: `bool` - Set to `true` on the first `WriteError` call; it never returns to `false`.

**Version**, **Help**, **Silent**, **Validate**, **Json**: `bool` - `true` when `-v`/`--version`, `-?`/`-h`/`--help`,
`--silent`, `--validate`, or `--json` was present anywhere on the command line. `Json` asks a command to write its
result as one JSON document (see _ResultWriter Unit Design_) instead of text.

**ResultsFile**: `string?` - Path supplied after `--results` or its legacy alias `--result`, or `null`.

**HeadingDepth**: `int` - Heading depth for markdown output; valid range 1-6, default 1; from `--depth`.

**Command**: `string?` - The command name: the first token that is neither an option nor the value of a global
option; `null` when there is none.

**CommandArguments**: `IReadOnlyList<string>` - Every token after the command name that is not a global option
or its value, in the original order. Tokens that look like options but are not global options (for example
`--width`) and their values are included untouched; the selected command interprets them.

**ExitCode**: `int` (derived) - 1 if `_hasErrors` is true; 0 otherwise.

### Key Methods

**Create**: Factory method that parses the arguments and returns a fully initialized `Context`.

- _Parameters_: `string[] args` - raw command-line arguments.
- _Returns_: `Context` - a new instance with all properties set.
- _Preconditions_: `args` is not null.
- _Postconditions_: the properties reflect the parsed command line; the log file is open if `--log` was given.

A private `ArgumentParser` walks the tokens once. A global option is recognized in any position and consumes its
value when it takes one (`--log`, `--results`, `--result`, `--depth`); the flags are `-v`/`--version`,
`-?`/`-h`/`--help`, `--silent`, `--validate`, and `--json`. Any other token goes to
`AcceptCommandToken`: after the command name has been seen it is appended to the command arguments; before it,
a token that starts with a dash (and is longer than one character) is rejected with an `ArgumentException`
("Unsupported argument '{token}'"), and any other token becomes the command name. After parsing, `OpenLogFile`
opens the log file with auto-flush.

**WriteLine**: Writes a message to standard output (unless silent) and to the log file (if open).

**WriteError**: Sets the error state, writes the message to standard error (unless silent) and to the log file
(if open). The message is colored red only when standard error is not redirected, so redirected error output
contains no control characters.

**WriteWarning**: Writes a non-fatal message to standard error (unless silent) and to the log file (if open) without
setting the error state, so the exit code stays 0. The message is colored yellow only when standard error is not
redirected.

**WriteDiagnostics**: Writes the notes of a successful command - `render`/`convert` and the verbs, `inspect`, `montage`,
and `compare` (also before a failed `compare` threshold is reported) - and the `echo` lines of a model to standard error so
that a feature left out of the picture is not silent in text mode. It writes nothing when `Json` is true (the JSON
result carries the same notes and standard output stays one document) and, like every other output, nothing to the
console when silent (the log file still receives it). Each echo line is written as the model printed it
(`ECHO: <text>`), then each note as `Unsupported: <code>: <message>` for severity `unsupported` or
`Warning: <code>: <message>` for any other severity, followed by `(<origin>, page <n>)` with whichever of the origin
(an input path or `input a`/`input b`) and the page there is. At most `MaxReportedLines` (20) echo lines and 20 notes are
written; one closing line each counts the rest (`... and N more echo lines (use --json for all)` and
`... and N more diagnostics (use --json for all)`). Every line is escaped first (`EscapeControlCharacters`): a control
character (C0, DEL, C1, such as ESC and NUL), a line or paragraph separator, or a bidirectional control is written as
`\n`, `\r`, `\t`, or `\uXXXX`, so that a model cannot inject terminal sequences; the same text is unchanged in the
JSON result. Notes are colored yellow, echo lines are not colored, and neither
sets the error state.

**Dispose**: Disposes the log file writer, flushing buffered content.

### Error Handling

`Create` throws `ArgumentNullException` for a null argument array, and `ArgumentException` for an unknown option
before the command name, a missing option value, or an out-of-range or non-integer `--depth`; the messages are
concise and carry no parameter-name suffix. It throws `InvalidOperationException` ("Failed to open log file
'{path}': {detail}") when the log file cannot be opened. These exceptions propagate to `Program.Execute`, which
reports them as expected failures. `WriteLine`, `WriteWarning`, `WriteDiagnostics`, and `WriteError` do not throw
(except `WriteDiagnostics` for a null argument). `Dispose` is idempotent.

### Dependencies

- **.NET BCL** - `Console`, `StreamWriter`, and `Path` are the only dependencies. `WriteDiagnostics` takes the
  `ImageDiagnostic` record of the imaging subsystem as data; no other tool unit is used.

### Callers

- **Program** - creates `Context` via `Context.Create` and reads the flags, the command name, and the exit code.
- **CommandRegistry** - reads `Command` and `CommandArguments` to dispatch.
- **Commands** - receive the `Context` and call `WriteLine`, `WriteDiagnostics`, and `WriteError`, and read `Json` to
  choose the output form.
- **Validation** - receives the `Context` from `Program` and calls `WriteLine`, `WriteError`, and reads
  `ResultsFile` and `HeadingDepth`; it also creates short-lived contexts to run the tool in-process.
