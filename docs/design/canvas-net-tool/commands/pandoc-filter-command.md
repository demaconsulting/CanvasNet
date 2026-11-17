### PandocFilterCommand

![Commands Structure](CommandsView.svg)

#### Purpose

`PandocFilterCommand` is the `pandoc-filter` command: the Lua shim `docs/filters/canvasnet.lua` sends it the whole
Pandoc document once and applies the document it writes back. Its single responsibility is to turn the command line into
the inputs of one `PandocFilterEngine` run and to move the bytes in and out; the filter itself is the `PandocFilter`
subsystem. With `--write-shim` it writes the shim the tool generates from its embedded template
(`PandocFilterShim.Bytes`). The unit is the class `PandocFilterCommand` in `PandocFilterCommand.cs`. The command has no
alias.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of the command): `--protocol <n>`,
`--filter-path <file>`, `--format <format>`, `--pandoc-version <version>`, `--resource-path <folder>` (repeatable),
`--pandoc-output <file>`, `--embed-resources`, `--jobs <n>` (1 to `FilterSettings.MaxJobs`, 64), `--input <file>`,
`--output <file>` (alias `-o`), and `--write-shim <file>` (`WriteShimOption`). The shim passes the first eight; the
last three are for people, tests, and debugging.

#### Key Methods

**Run(context, args)**: parses the arguments with `ArgumentReader` and requires no positional argument. With
`--write-shim` it calls `PandocFilterShim.Write` and writes `wrote <file> (protocol <n>)`, or with `--json` a success
document with `tool`, `protocol`, and `outputs` (`path`, `format` `lua`, `bytes`), and returns. Otherwise it requires
`--protocol` as a whole number and checks it with `PandocFilterShim.CheckProtocol` (with the `--filter-path` given),
before anything is read, then requires `--filter-path`. It builds the `FilterSettings` of the run: the filter path, the
working directory (`Environment.CurrentDirectory`, read once), the environment (`Environment.GetEnvironmentVariable`),
the format, the output file of Pandoc, the embed flag, and the resource paths (`.` when none is given), and resolves the
number of jobs with `FilterSettings.ResolveJobs` (`--jobs`, else `CANVASNET_JOBS`, else the processors up to 4). It
reads the input, runs `PandocFilterEngine.Run(input, pandocVersion)` with a `ToolPictureDrawer`, and writes the output.
When the run fails, it first writes each of `PandocFilterEngine.WarningsBeforeFailure` to standard error as
`[WARNING] canvasnet.lua: <message>` (control characters escaped), which Pandoc passes on, and then lets the exception
escape.

**ReadInput(file)**: the bytes of the file, or all of standard input read as raw bytes from
`Console.OpenStandardInput()`, never through the console's text encoding, which would corrupt non-ASCII text.

**WriteOutput(context, file, bytes)**: writes the bytes to the file, or as raw bytes to `Console.OpenStandardOutput()`
unless `--silent` is given. Nothing is written unless the run succeeded.

#### Error Handling

`ToolException` with `usage-error` for the command line (a positional argument, no `--protocol`, a protocol that is not
a whole number, a protocol outside the range the tool speaks, no `--filter-path`, a `--jobs` outside 1 to 64),
`input-not-found` and `input-unreadable` for the input file, and `output-write-failed` for the output file or the shim.
The failures of the engine escape unchanged: `pandoc-filter-error` for a mistake in a picture, `corrupt-input` for a
document that is not Pandoc's JSON, and the code of the tool for a drawing that fails. The registry reports them as
expected errors with exit code 1, with `Error: ...` on standard error and, with `--json`, the JSON failure document on
standard output, which the shim turns into the error of the build.

#### Dependencies

- **PandocFilterEngine**, **ToolPictureDrawer**, and **FilterSettings** (`PandocFilter` subsystem) - the filter, the
  drawing of a picture, and the inputs of the run.
- **PandocFilterShim** (`PandocFilter` subsystem) - the embedded shim, `Write`, and `CheckProtocol`.
- **ArgumentReader**, **ResultWriter**, and **Context** - the grammar, the JSON document, and the output channels.

#### Callers

- **CommandRegistry** - registers it in `CreateDefault` after `flatten`.
- **canvasnet.lua** (the Lua shim of a repository, see *PandocFilterShim*) - runs
  `pandoc-filter --json --protocol 1 --filter-path ... --format ...` once per Pandoc run with the document on standard
  input.
- **Validation** - the self-test `CanvasNetTool_PandocFilter` runs it on a document file.
