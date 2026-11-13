### FlattenCommand

![Commands Structure](CommandsView.svg)

#### Purpose

`FlattenCommand` is the `flatten` command: it writes a copy of an SVG drawing in which every `image` link that only
CanvasNet can draw is a PNG picture, so that any SVG reader shows it. Its single responsibility is to turn a command line
into one `SvgFlattener` request and to report the result; the drawing, the rewrite, and the writing are the
`SvgFlattener` of the Viewer library. The unit is the class `FlattenCommand` in `FlattenCommand.cs`. The command has no
alias.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of the command): `--output` (alias `-o`, required),
`--scale <factor>`, and the declarations of `--strict`, `--file-access`, `--allow-root`, and `--no-clobber` of
`RenderCommand.OptionTable`
(`RenderCommand.SpecsFor`), so their grammar and help text are those of `render`.

#### Key Methods

**Run(context, args)**: parses the arguments with `ArgumentReader`, requires exactly one input (`usage-error`
otherwise) and an output (`usage-error` with a hint), reads `--scale` from 0.001 to 8192 (default 1), builds the
file-access policy with
`RenderCommand.ReadFileAccess` (the default folders of `roots` are the working directory and the folder of the
drawing), probes the input with `SourceLoader.Probe` under it, and calls `SvgFlattener.Execute` with it as
`FlattenRequest.FileAccess`. Without `--json` it writes one line, `wrote <output>
(<bytes> bytes): <N> pictures replaced (<href> <width>x<height> <format>, ...)`, or `nothing to flatten: <input> links
no picture that only CanvasNet draws; <output> was not written`, and the notes and echo with `Context.WriteDiagnostics`
(standard error). With `--json` it writes one document with `ResultWriter.Success` whose keys, in order, are `tool`,
`input` (`path`, `format`, `natural`), `scale`, `flattened`, `replaced` (`href`, `format`, `width`, `height`,
`elements`, `bytes`), `rebased` (`href`, `newHref`), `outputs` (`path`, `format`, `bytes`; empty when nothing was
written), `diagnostics` (the shape of `render`: `source`, `severity`, `code`, `message`, `page` when the report belongs
to a page of a linked document, then `ResultWriter.WriteDiagnosticExtras`), and `echo`. Nothing to flatten is a success
(exit code 0).

**RenderText(result, output)**: the text line above, in the invariant culture.

#### Error Handling

`ToolException` with `usage-error` for the command line, and the `ViewerException` codes of `SourceLoader.Probe` and
`SvgFlattener.Execute` (`unsupported-format`, `unsupported-feature` with `--strict`, `output-exists` with
`--no-clobber`, `size-limit`, `file-access-denied` for a denied link with `--strict`, and so on; without `--strict` a
denied link is the warning `image-access-denied`), which the registry reports as expected errors with exit code 1.

#### Dependencies

- **SvgFlattener** and **SourceLoader** (Viewer library) - the operation and the probe.
- **ArgumentReader**, **ResultWriter**, and **Context** - the grammar, the JSON document, and the output channels.
- **RenderCommand** - the declarations of `--strict`, `--file-access`, `--allow-root`, and `--no-clobber`, and
  `ReadFileAccess`.

#### Callers

- **CommandRegistry** - registers it in `CreateDefault` after `inspect`.
- **canvasnet.lua** (the Pandoc filter of the repository) - runs `flatten --json` for an SVG picture that links a file
  only CanvasNet draws, with `--file-access` and `--allow-root` when `CANVASNET_FILE_ACCESS` or `CANVASNET_ROOTS` is
  set.
