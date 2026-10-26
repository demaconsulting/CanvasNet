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
`Commands` subsystem that dispatches the first non-option argument to a registered command, and an `Imaging`
subsystem that turns files into pixels and pixels into files. The system
consists of six direct units and two subsystems:

- **Program** (unit; `Program.cs` and `ToolException.cs`): the entry point. `Main` delegates to `Execute`,
  which creates the `Context`, runs the program logic, and applies the error policy. `Run` dispatches in
  priority order: version query, help, self-validation, then the selected command. See _Program Unit
  Design_ (`canvas-net-tool/program.md`).
- **Context** (unit; `Context.cs`): parses the global options in any position, captures the command name
  and the command arguments, owns standard output, standard error, and the optional log file, and derives
  the exit code. See _Context Unit Design_ (`canvas-net-tool/context.md`).
- **ArgumentReader** (unit; `ArgumentReader.cs` and `ArgumentReader.Values.cs`): the command-line grammar of
  a command: reads its options and positional arguments from its option table, and parses the typed values
  (sizes, rectangles, colors, angles, flip axes, border widths, resolutions, pages, and definitions). See
  _ArgumentReader Unit Design_ (`canvas-net-tool/argument-reader.md`).
- **ResultWriter** (unit; `ResultWriter.cs`): the machine contract: writes the deterministic JSON success and
  failure documents with the schema version. See _ResultWriter Unit Design_ (`canvas-net-tool/result-writer.md`).
- **Validation** (unit; `Validation.cs`): the self-validation framework behind `--validate`, with the
  contractual platform-evidence self-tests and TRX/JUnit results output. See _Validation Unit Design_
  (`canvas-net-tool/validation.md`).
- **PathHelpers** (unit; `PathHelpers.cs`): the safe path combination used for temporary files. See
  _PathHelpers Unit Design_ (`canvas-net-tool/path-helpers.md`).
- **Commands** (subsystem; namespace `DemaConsulting.CanvasNet.Tool.Commands`, folder `Commands/`): the
  command contract, the command registry, and the commands themselves. It is a subsystem, not a unit, because
  the individual command units are added to it as the commands are implemented; as of this release it holds
  `CommandRegistry`, `DiscoveryCommands` (the `formats` and `info` commands), and `RenderCommand` (the `render`
  command and its alias `convert`). See _Commands Subsystem Design_ (`canvas-net-tool/commands.md`).
- **Imaging** (subsystem; namespace `DemaConsulting.CanvasNet.Tool.Imaging`, folder `Imaging/`): everything that
  turns files into pixels and pixels into files, free of command-line concerns. It holds `SourceLoader` (format
  detection, header probing, and loading of PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, and SVG, with the size policy,
  EXIF orientation, background flattening, and the mapping of library exceptions to expected errors) and
  `ImageWriter` (PNG, JPEG, BMP, and TIFF output, written atomically with the overwrite policy). It is a
  subsystem because later releases add units for transformation, comparison, and montage. See _Imaging
  Subsystem Design_ (`canvas-net-tool/imaging.md`).

Dependencies inside the system are one-directional except for self-validation: `Program` calls `Context`,
`Commands`, `ResultWriter`, and `Validation`; `Validation` calls back into `Program.Run` (to exercise the tool
in-process) and uses `PathHelpers`; `Commands` uses `Context` for output, `ArgumentReader` for the command line,
`ResultWriter` for JSON results, `Imaging` for everything that touches an image, and `ToolException` for
failures, and never calls `Program`; `Imaging` knows nothing of `Commands`, `Context`, or `ArgumentReader`.
`ArgumentReader` uses the `OptionSpec` type of `Commands` and `ToolException`; `ResultWriter` uses
`ToolException` and `ErrorCodes`.

### Deliberate Deviations From The Template

The template's `Program` runs one demo action. CanvasNetTool differs in these respects, each on purpose:

- **Command capture.** `Context` captures the command name and the command arguments; the template has no
  commands. Global options are honored anywhere; unknown options are rejected only before the command name,
  and after it are passed to the command, which parses its own options with `ArgumentReader`.
- **Banner policy.** The template prints the banner for every action except the version query. The banner
  here is printed only for help, self-validation, and an empty command line, so that a command's standard
  output is exactly the command's own output and can be piped or parsed.
- **Error report format.** An expected failure is reported as `Error: {code}: {message}` (plus an optional
  `Hint:` line) when it is a `ToolException`, and as the template's `Error: {message}` otherwise.
- **JSON results.** The global `--json` option makes a command write its result as one JSON document on
  standard output (the template has no machine-readable output). With `--json` (and without `--silent`) an
  expected failure is additionally written to standard output as a failure document in the same envelope, so
  that one parser handles both outcomes; the text report on standard error is unchanged. The failure document is
  emitted by `Program.Execute`, which scans the raw arguments, so it also covers failures that happen before a
  `Context` exists. An unexpected exception produces no document.
- **Exit codes.** As in the template: 0 for success and 1 for any failure; there is no finer table. The
  stable error code in the message (and in the failure document) distinguishes failure kinds.
- **Unexpected exceptions.** As in the template, an unexpected exception is reported on standard error
  and re-thrown. Library and I/O exceptions that mean "bad or unsupported user input" are mapped to
  `ToolException` at the boundary where the input is read, using an explicit list of exception types;
  everything else is a defect and escapes.

## External Interfaces

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| Command line | Inbound | `canvasnet [global options] <command> [command arguments]` | Global options are `-v`/`--version`, `-?`/`-h`/`--help`, `--silent`, `--validate`, `--json`, `--results <file>` (alias `--result`), `--depth <1-6>`, and `--log <file>`, valid in any position; the first token that is not an option or an option value is the command name; unknown options before the command name are an error. A command's own options are written `--name value`, `--name=value`, or `-a value`, and may be repeated; see _ArgumentReader Unit Design_ |
| Exit code | Outbound | Integer | 0 for success; 1 for any failure; an unexpected exception terminates the process through the runtime's unhandled-exception path |
| Standard output | Outbound | UTF-8 text, or one single-line JSON document | The version, banner, help, self-validation report, or the selected command's own output; suppressed by `--silent`; clean of decoration when a command runs. With `--json` a command writes one JSON document: `{"schemaVersion":1,"ok":true,"command":"...", ...}`; an expected failure is written as `{"schemaVersion":1,"ok":false,"command":"...","error":{"code":"...","message":"...","hint":"..."}}` (`command` is null when no command was selected; `hint` only when there is one; no exit-code field); the document is ASCII-only and deterministic; see _ResultWriter Unit Design_ |
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
  systems. The imaging code calls the core `Surface` and `Rgba32` types and the PNG, JPEG, BMP, TIFF, GIF, and
  JPEG 2000 codecs, and the `CanvasNetSvg` system's `SvgCodec`; the commands that read and render documents and
  models will call the others. The references are declared now so that the packaging design is fixed from the
  start. Each
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
self-validation temporary files. Commands that write user-named output files do so through `ImageWriter`, which
refuses an output that is the input, writes through a temporary file so that a failure never leaves a partial
output, and overwrites an existing output only when overwriting is allowed. The maximum image size
(`Surface.MaxDimension`, 8192 pixels per side) is enforced by `SourceLoader` before a file is decoded or a
drawing is rendered, which bounds memory use.

## Data Flow

1. The host starts the process; `Program.Main` calls `Program.Execute` with the arguments and the default
   command registry.
2. `Execute` calls `Context.Create(args)`, which parses the global options and captures the command name and
   arguments, and opens the log file when `--log` was given. An `ArgumentException` or
   `InvalidOperationException` here is an expected failure.
3. `Program.Run(context, registry)` performs the first matching action: version query (prints the bare
   version); help (banner, then top-level help, or the help of the named command); self-validation (banner,
   then `Validation.Run`); the named command (`CommandRegistry.Run`); otherwise the banner and a usage hint.
4. A command reads its arguments with `ArgumentReader` (against its option table), reads its inputs, writes its
   results through the `Context` (as text, or through `ResultWriter` as one JSON document when `--json` was
   given), and reports an expected failure by throwing `ToolException`; the registry records the command name on
   it.
5. `Execute` catches `ArgumentException` and `InvalidOperationException` (including `ToolException`),
   writes the concise report to standard error and, when `--json` was given without `--silent`, the failure
   document to standard output, and returns 1. Any other exception is written as an
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
- Determinism: for the same inputs, tool version, and machine, command output is deterministic; JSON documents
  are additionally independent of the current culture and the console code page (numbers use the invariant
  culture and non-ASCII characters are escaped); output that
  depends on installed fonts (text in SVG, PDF, PowerPoint, and Visio documents) may differ between machines.
- Path safety: caller-supplied path components used for temporary files are validated by
  `PathHelpers.SafePathCombine`.
