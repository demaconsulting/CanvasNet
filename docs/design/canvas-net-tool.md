# System Design

<!-- cspell:ignore canvasnet Visio -->

This document provides the system-level design for CanvasNetTool.

![CanvasNetTool Structure](CanvasNetToolView.svg)

## Architecture

CanvasNetTool is the `canvasnet` command-line tool, distributed as its own dotnet tool package
(`DemaConsulting.CanvasNet.Tool`, root namespace `DemaConsulting.CanvasNet.Tool`, command name
`canvasnet`). It lets people, scripts, and AI agents inspect, render, and transform images and
documents using the CanvasNet libraries, and it is designed to be driven by programs: its error
contract (exit codes, error text, stable error codes) is small, fixed, and documented.

The tool follows the DEMA Consulting tool template (a `Program` entry point, a `Context` that owns the
command line and the output channels, a self-validation mode, and safe path helpers) and adds a
`Commands` subsystem that dispatches the first non-option argument to a registered command. The system
consists of four direct units and one subsystem:

- **Program** (unit; `Program.cs` and `ToolException.cs`): the entry point. `Main` delegates to `Execute`,
  which creates the `Context`, runs the program logic, and applies the error policy. `Run` dispatches in
  priority order: version query, help, self-validation, then the selected command. See _Program Unit
  Design_ (`canvas-net-tool/program.md`).
- **Context** (unit; `Context.cs`): parses the global options in any position, captures the command name
  and the command arguments, owns standard output, standard error, and the optional log file, and derives
  the exit code. See _Context Unit Design_ (`canvas-net-tool/context.md`).
- **Validation** (unit; `Validation.cs`): the self-validation framework behind `--validate`, with the
  contractual platform-evidence self-tests and TRX/JUnit results output. See _Validation Unit Design_
  (`canvas-net-tool/validation.md`).
- **PathHelpers** (unit; `PathHelpers.cs`): the safe path combination used for temporary files. See
  _PathHelpers Unit Design_ (`canvas-net-tool/path-helpers.md`).
- **Commands** (subsystem; namespace `DemaConsulting.CanvasNet.Tool.Commands`, folder `Commands/`): the
  command contract and the command registry. It is a subsystem, not a unit, because the individual command
  units are added to it as the commands are implemented. See _Commands Subsystem Design_
  (`canvas-net-tool/commands.md`).

Dependencies inside the system are one-directional except for self-validation: `Program` calls `Context`,
`Commands`, and `Validation`; `Validation` calls back into `Program.Run` (to exercise the tool in-process)
and uses `PathHelpers`; `Commands` uses `Context` for output and `ToolException` for failures and never
calls `Program`.

### Deliberate Deviations From The Template

The template's `Program` runs one demo action. CanvasNetTool differs in these respects, each on purpose:

- **Command capture.** `Context` captures the command name and the command arguments; the template has no
  commands. Global options are honored anywhere; unknown options are rejected only before the command name,
  and after it are passed to the command, which parses its own options.
- **Banner policy.** The template prints the banner for every action except the version query. The banner
  here is printed only for help, self-validation, and an empty command line, so that a command's standard
  output is exactly the command's own output and can be piped or parsed.
- **Error report format.** An expected failure is reported as `Error: {code}: {message}` (plus an optional
  `Hint:` line) when it is a `ToolException`, and as the template's `Error: {message}` otherwise.
- **Exit codes.** As in the template: 0 for success and 1 for any failure; there is no finer table. The
  stable error code in the message distinguishes failure kinds.
- **Unexpected exceptions.** As in the template, an unexpected exception is reported on standard error
  and re-thrown. Library and I/O exceptions that mean "bad or unsupported user input" are mapped to
  `ToolException` at the boundary where the input is read, using an explicit list of exception types;
  everything else is a defect and escapes.

## External Interfaces

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| Command line | Inbound | `canvasnet [global options] <command> [command arguments]` | Global options are `-v`/`--version`, `-?`/`-h`/`--help`, `--silent`, `--validate`, `--results <file>` (alias `--result`), `--depth <1-6>`, and `--log <file>`, valid in any position; the first token that is not an option or an option value is the command name; unknown options before the command name are an error |
| Exit code | Outbound | Integer | 0 for success; 1 for any failure; an unexpected exception terminates the process through the runtime's unhandled-exception path |
| Standard output | Outbound | UTF-8 text | The version, banner, help, self-validation report, or the selected command's own output; suppressed by `--silent`; clean of decoration when a command runs |
| Standard error | Outbound | UTF-8 text | `Error: {code}: {message}` and optional `Hint: {hint}` lines for expected failures; `Error: {message}` for argument and operation failures; `Unexpected error: {message}` before an unexpected exception is re-thrown; no stack trace for expected failures; red only when standard error is an interactive console |
| Log file | Outbound | Text file | Written when `--log <file>` is given, with every line written to standard output or standard error whether or not `--silent` is set; truncated at open; failure to open is an expected failure |
| Results file | Outbound | TRX (`.trx`) or JUnit XML (`.xml`) | Written by `--validate --results <file>`; any other extension is an error and no file is written |
<!-- markdownlint-enable MD013 -->

The stable error codes are `usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `page-out-of-range`, `size-limit`,
`same-input-output`, `output-exists`, `output-write-failed`, `scad-error`, and `budget-exceeded`
(the `ErrorCodes` supporting type documented in _Program Unit Design_). A code is never changed once
released.

## Dependencies

- **DemaConsulting.TestResults** (OTS, runtime): records the self-validation results and, through its
  `TestResults.IO` serializers, writes the TRX and JUnit files. See _DemaConsulting.TestResults OTS
  Integration Design_ (`ots/test-results.md`).
- **The CanvasNet libraries** (same repository): the tool references the core `CanvasNet` package and the
  `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetRender3D`, and `CanvasNetScad`
  systems. The foundation delivered here does not yet call them; the commands that read and render
  documents do. The references are declared now so that the packaging design is fixed from the start. Each
  reference is a private asset of the tool project: the library assemblies are bundled into the tool
  package, and the package declares no NuGet dependency on them. The reasons are that a tool install must
  not require sibling packages (which may not be published at the same time) and that the tool and the
  libraries it uses are always released together. The integration-test job of the build workflow guards
  both properties: it fails if the package's nuspec declares a dependency on a `DemaConsulting.CanvasNet`
  package, or if a library assembly is missing from the package for any target framework. The test
  project references the libraries directly, which is what places their assemblies next to the tool
  assembly for the out-of-process system tests.

## Risk Control Measures

N/A - not a safety-classified software item. The path-traversal guard in `PathHelpers` protects the
self-validation temporary files; commands that write user-named output files are responsible for their
own overwrite and same-file protections (see their unit designs).

## Data Flow

1. The host starts the process; `Program.Main` calls `Program.Execute` with the arguments and the default
   command registry.
2. `Execute` calls `Context.Create(args)`, which parses the global options and captures the command name and
   arguments, and opens the log file when `--log` was given. An `ArgumentException` or
   `InvalidOperationException` here is an expected failure.
3. `Program.Run(context, registry)` performs the first matching action: version query (prints the bare
   version); help (banner, then top-level help, or the help of the named command); self-validation (banner,
   then `Validation.Run`); the named command (`CommandRegistry.Run`); otherwise the banner and a usage hint.
4. A command reads its inputs, writes its results through the `Context`, and reports an expected failure by
   throwing `ToolException`; the registry records the command name on it.
5. `Execute` catches `ArgumentException` and `InvalidOperationException` (including `ToolException`),
   writes the concise report to standard error, and returns 1. Any other exception is written as an
   unexpected error and re-thrown. On normal completion `Execute` returns the context's exit code.

## Design Constraints

- Platform: multi-targets `net8.0`, `net9.0`, and `net10.0` on Windows, Linux, and macOS; the platform
  requirements are proven by the self-validation results that the installed tool writes in each job of the
  integration-test matrix.
- Packaging: packed as a dotnet tool with the command name `canvasnet`; the sibling libraries are
  bundled (see _Dependencies_). The package readme is the repository README, so README links must be
  absolute URLs.
- Threading: single-threaded console application; no shared mutable state between invocations. Tests that
  redirect the console run sequentially.
- Resource lifecycle: `Context` is `IDisposable`; callers dispose it to flush and close the log file.
- Output contract: standard output carries results and standard error carries diagnostics, so a caller may
  redirect each independently. Expected-failure messages never contain a stack trace.
- Determinism: for the same inputs, tool version, and machine, command output is deterministic; output that
  depends on installed fonts (text in SVG, PDF, PowerPoint, and Visio documents) may differ between machines.
- Path safety: caller-supplied path components used for temporary files are validated by
  `PathHelpers.SafePathCombine`.
