# System Design

<!-- cspell:ignore canvasnet Visio scad openscad psnr rmse ssim deskew otsu luma grayscale gamma -->

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
subsystem that turns files into pixels and pixels into files. Its commands inspect (`formats`, `info`), render and
transform (`render`, `convert`, `crop`, `resize`, `rotate`, `flip`, `pad`), clean up photographs and sketches (the
tone, deskew, trim, and grid operations of `render`, and `rotate` by any angle), measure (`inspect colors` and
`inspect pixel`), compare (`compare`), and compose
(`montage`) images. The system
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
  each family of commands is its own unit behind the shared command contract; it holds
  `CommandRegistry`, `DiscoveryCommands` (the `formats` and `info` commands), `RenderCommand` (the `render`
  command and its alias `convert`, and the verb commands `crop`, `resize`, `rotate`, `flip`, and `pad`),
  `CompareCommand` (the `compare` command: exact metrics, a grid of per-cell errors, difference pictures, and
  threshold gating), `MontageCommand` (the `montage` command: a labeled contact sheet of images and document
  pages), and `InspectCommand` (the read-only `inspect colors` and `inspect pixel` commands). See
  _Commands Subsystem Design_ (`canvas-net-tool/commands.md`).
- **Imaging** (subsystem; namespace `DemaConsulting.CanvasNet.Tool.Imaging`, folder `Imaging/`): everything that
  turns files into pixels, transforms pixels, and turns pixels into files, free of command-line concerns. It
  holds `SourceLoader` (format detection, header probing, and loading of PNG, JPEG, BMP, TIFF, GIF, JPEG 2000,
  and SVG, of the pages of PDF, PPTX, and VSDX documents, of a view of an OpenSCAD model, and of a Vega-Lite
  chart, with the size policy, page and resolution handling, the views and variables of a model, EXIF orientation, background
  flattening, and the mapping of library exceptions to expected errors),
  `ImagePipeline` (the ordered crop, resize, fit, rotate by any angle, flip, pad, tone-adjustment, deskew, trim, and
  grid operations, which the `render` command, the verb commands `crop`, `resize`, `rotate`, `flip`, and `pad`, and
  `inspect` share, delegating flip, pad, trim, rotation, skew estimation, and the tone adjustments to the core `Surface`),
  `GridOverlay` (the labeled coordinate grid),
  `ImageWriter` (PNG, JPEG, BMP, and TIFF output, written atomically with the overwrite policy),
  `ImageComparison` (the threshold checks of a comparison and the defaults of its options; the exact error metrics, the
  windowed structural similarity, the per-cell grid, and the difference, overlay, and side-by-side pictures of two images
  of the same size are the core `Surface`'s), and
  `ContactSheet` (the layout and composition of a contact sheet, with labels drawn in the bundled font). It is a
  subsystem because loading, transforming, comparing, composing, and writing are separate responsibilities
  that are reviewed and tested separately. See _Imaging Subsystem Design_ (`canvas-net-tool/imaging.md`).

Dependencies inside the system are one-directional except for self-validation: `Program` calls `Context`,
`Commands`, `ResultWriter`, and `Validation`; `Validation` calls back into `Program.Run` (to exercise the tool
in-process) and uses `PathHelpers`; `Commands` uses `Context` for output, `ArgumentReader` for the command line,
`ResultWriter` for JSON results, `Imaging` for everything that touches an image, and `ToolException` for
failures, and never calls `Program`; `Imaging` knows nothing of `Commands` or `Context` and uses `ArgumentReader`
only in `ImagePipeline`, to read the transformation operations from a parsed command line.
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
  stable error code in the message (and in the failure document) distinguishes failure kinds. A threshold that
  `compare` is asked to check and does not meet is such a failure (`threshold-failed`, exit code 1), raised only
  after the result and the requested pictures were written; with `--json` the failure document then carries the
  command's result fields (the metrics, the checks, and the outputs) after the `error` object, through
  `ToolException.ResultFields`, so that a caller needs one parse to see both the failure and the measures.
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
| Standard output | Outbound | UTF-8 text, or one single-line JSON document | The version, banner, help, self-validation report, or the selected command's own output; suppressed by `--silent`; clean of decoration when a command runs. With `--json` a command writes one JSON document: `{"schemaVersion":1,"ok":true,"command":"...", ...}`; an expected failure is written as `{"schemaVersion":1,"ok":false,"command":"...","error":{"code":"...","message":"...","hint":"..."}}` (`command` is null when no command was selected; `hint` only when there is one; no exit-code field; the failure of an unmet `compare` threshold appends the result fields of the command after `error`); the document is ASCII-only and deterministic; see _ResultWriter Unit Design_ |
| Standard error | Outbound | UTF-8 text | `Error: {code}: {message}` and optional `Hint: {hint}` lines for expected failures; `Error: {message}` for argument and operation failures; `Unexpected error: {message}` before an unexpected exception is re-thrown; no stack trace for expected failures; red only when standard error is an interactive console; suppressed by `--silent` (the runtime's own report of an unexpected exception is not) except when no context could be created (a malformed global option, or a log file that cannot be opened), when the report is always written |
| Log file | Outbound | Text file | Written when `--log <file>` is given, with every line written to standard output or standard error whether or not `--silent` is set, including the report of an expected or unexpected failure; truncated at open; failure to open is an expected failure |
| Results file | Outbound | TRX (`.trx`) or JUnit XML (`.xml`) | Written by `--validate --results <file>`; any other extension is an error and no file is written |
<!-- markdownlint-enable MD013 -->

The stable error codes are `usage-error`, `operation-failed`, `input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature`, `page-out-of-range`, `size-limit`,
`same-input-output`, `output-exists`, `output-write-failed`, `scad-error`, `vega-lite-error`, `budget-exceeded`, `size-mismatch`,
and `threshold-failed`
(the `ErrorCodes` supporting type documented in _Program Unit Design_). A code is never changed once
released. For a Vega-Lite chart the codes are exact: `vega-lite-error` is an invalid specification (including a
nesting or string longer than the parser limit, as the SCAD nesting limit is a `scad-error`) or a chart the library
cannot lay out or draw (including any argument failure of the library, and a local data file that is missing, outside the
folder of the specification, or not valid); `budget-exceeded` is a limit applied while the
chart is laid out or drawn (including the data row, data file, and data byte limits); `corrupt-input` is a
specification file longer than the limit of the library, as for an
overlong SCAD model; and `size-limit` is only a chart that lays out larger than the maximum image size. See _SourceLoader
Unit Design_ for the mapping.

## Dependencies

- **DemaConsulting.TestResults** (OTS, runtime): records the self-validation results and, through its
  `TestResults.IO` serializers, writes the TRX and JUnit files. See _DemaConsulting.TestResults OTS
  Integration Design_ (`ots/test-results.md`).
- **The CanvasNet libraries** (same repository): the tool references the core `CanvasNet` package and the
  `CanvasNetSvg`, `CanvasNetPdf`, `CanvasNetPptx`, `CanvasNetVsdx`, `CanvasNetRender3D`, `CanvasNetScad`, and
  `CanvasNetVegaLite` systems. The imaging code calls the core `Surface` and `Rgba32` types and the PNG, JPEG,
  BMP, TIFF, GIF, and JPEG 2000 codecs, the `CanvasNetSvg` system's `SvgCodec`, the `PdfDocument`, `PptxDocument`, and
  `VsdxDocument` types of the `CanvasNetPdf`, `CanvasNetPptx`, and `CanvasNetVsdx` systems, and the
  `ScadDocument` type of the `CanvasNetScad` system (with the `Camera` and `SceneRenderOptions` types of the
  `CanvasNetRender3D` system, which draws the model), and the `VegaLiteDocument` type of the `CanvasNetVegaLite`
  system, which parses, measures, and draws a chart (the tool uses only its public API and sets the options that
  make a render static and deterministic: UTC dates, only the bundled fonts, and no network; the data files of a chart
  are read by the library from the folder of the specification and below it only, because the tool opens the chart by its
  path and passes no other data folder, so there is no `--data-dir` option). The core `Fonts` and
  `Rendering` subsystems also supply the
  bundled font and the text renderer with which `ContactSheet` draws the labels of a contact sheet, so that labels
  never depend on the fonts installed on a machine. Each
  reference is a private asset of the tool project: the library assemblies are bundled into the tool
  package, and the package declares no NuGet dependency on them. The reasons are that a tool install must
  not require sibling packages (which may not be published at the same time) and that the tool and the
  libraries it uses are always released together. The integration-test job of the build workflow guards
  both properties: it fails if the package's nuspec declares any dependency (a tool package must carry
  everything it needs, including the third-party `DemaConsulting.TestResults` and `System.Numerics.Tensors`
  libraries and the transitively used `CanvasNetCharts` assembly), if it is not a `DotnetTool` package, or if a
  library assembly (the `CanvasNetVegaLite` assembly included), the tool's dependency manifest (`.deps.json`) or
  runtime configuration file, or `DotnetToolSettings.xml` is missing from the package for any target framework. The
  package also carries the third-party license notices of the `CanvasNetVegaLite` system (the BSD-3-Clause texts of
  Vega and Vega-Lite and the ISC text of d3, with the notice README) in its `licenses` folder, because binary
  distributions must reproduce them, and the same job checks that they are present. The test
  project references the libraries directly, which is what places their assemblies next to the tool
  assembly for the out-of-process system tests.

## Risk Control Measures

N/A - not a safety-classified software item. The path-traversal guard in `PathHelpers` protects the
self-validation temporary files. Commands that write user-named output files do so through `ImageWriter`, which
refuses an output that is the input (by path, after link resolution; a hard link is safe because the input is fully
read first and an output is moved over its name as a new file), writes through a temporary file so that a failure
while encoding never leaves a partial
output, and overwrites an existing output only when overwriting is allowed. A command that writes several outputs
touches no target until every one is encoded and then publishes them all or nothing
(`ImageWriter.PublishAll`, with rollback). The maximum image size
(`Surface.MaxDimension`, 8192 pixels per side) is enforced by `SourceLoader` before a file is decoded or a
drawing is rendered, which bounds memory use. `compare` holds the two loaded images and one picture at a time, and
`montage` holds the sheet plus one source at a time; a sheet larger than the limit is refused before any input is
loaded.

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
   writes the concise report through the `Context` (so `--silent` suppresses it and `--log` records it; to standard
   error only when no context could be created) and, when `--json` was given without `--silent`, the failure
   document to standard output, and returns 1. Any other exception is written as an
   unexpected error and re-thrown. On normal completion `Execute` returns the context's exit code.

## Design Constraints

- Platform: multi-targets `net8.0`, `net9.0`, and `net10.0` on Windows, Linux, and macOS; the platform
  requirements are proven by the self-validation results that the installed tool writes in each job of the
  integration-test matrix. The same job also smoke-tests the installed tool (`formats --json`, an SVG `render`, a
  `crop`, and the `info` and `render` of a small Vega-Lite chart) so that the bundled libraries are proven to
  load on every platform.
- Packaging: packed as a dotnet tool with the command name `canvasnet`; the sibling libraries are
  bundled (see _Dependencies_). The package readme is the repository README, so README links must be
  absolute URLs.
- Threading: a console application with no shared mutable state between invocations. The pixel operations of the
  photo tools may use row-partitioned parallel loops whose outputs are disjoint, and reduce histograms in fixed blocks
  that are merged in order, so a result never depends on the number of cores. Tests that
  redirect the console run sequentially.
- Resource lifecycle: `Context` is `IDisposable`; callers dispose it to flush and close the log file.
- Output contract: standard output carries results and standard error carries diagnostics, so a caller may
  redirect each independently. Expected-failure messages never contain a stack trace.
- Determinism: for the same inputs, tool version, and machine, command output is deterministic; JSON documents
  are additionally independent of the current culture and the console code page (numbers use the invariant
  culture and non-ASCII characters are escaped); the metrics of `compare` are computed from exact integer sums with
  one final division, so they do not vary from run to run, the tone operations, the skew estimation, the trim, and the
  analysis use integer arithmetic (the rotation uses `double` sampling), and the labels of `montage` and `--grid` use the
  bundled font; output that
  depends on installed fonts (text in SVG, PDF, PowerPoint, and Visio documents) may differ between machines.
- Fill and defaults of the photo operations: the corners that a rotation (and `--deskew`) exposes are filled with
  `--rotate-fill`, else with `--background` when it is given, else with opaque white when every pixel of the image is
  opaque (a photograph, a scan, or a flattened drawing, whose corners then look like paper), else transparent; the
  canvas grows to the bounding box of the rotated image. An explicitly requested rotation larger than 8192 pixels on
  a side is refused as `size-limit`; automatic deskew instead leaves an oversized correction unchanged, reports a
  reason, and emits a warning. `--trim-tolerance` defaults to 8, `--auto-levels-clip` to 1 percent, and the `--grid`
  color to `#FF00FFA0`. The `deskew` quantities: a tilt is corrected only when it is at least 0.05 degrees and its
  confidence is at least 0.4.
- Path safety: caller-supplied path components used for temporary files are validated by
  `PathHelpers.SafePathCombine`.
