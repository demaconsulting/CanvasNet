### PandocFilterShim

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PandocFilterShim` is the Lua shim of the Pandoc filter and the protocol it speaks with the `pandoc-filter` command. A
repository keeps only the shim, `docs/filters/canvasnet.lua`, which Pandoc runs as a Lua filter: it sends the whole
document to `canvasnet pandoc-filter` once and applies what comes back, so a fix of the filter ships with the tool. The
unit has two parts:

- The Lua shim template `canvasnet.lua` in `src/DemaConsulting.CanvasNet.Tool/PandocFilter/`, embedded in the tool,
  and its repository copy `docs/filters/canvasnet.lua`, which is byte-identical to the shim the tool writes (the
  template with its tables filled in).
- The internal static class `PandocFilterShim` in `PandocFilterShim.cs`, which generates the shim from the embedded
  template, checks the protocol a shim passes, and writes the shim to a file. The class is stateless and thread-safe.

The protocol changes only when the arguments or the output contract change, never for a change of the filter's logic.
The shim's fast path, which decides whether a document needs the tool at all, is the one part of the shim that follows
the filter's logic: it lists the code-block classes and picture suffixes the engine draws, written from the engine's
own tables (`PictureAttributes.BlockClasses` and `PictureAttributes.PictureSuffixes`) when the shim is generated, so it
cannot drift from the tool. A shim written by an older tool keeps working with a newer one (the protocol is the same),
but it starts the tool for documents the newer tool would not touch, or misses a format the newer tool draws, so a
repository writes its shim again after updating the tool.

#### Data Model

**Class constants**:

- `Protocol` = 1: the protocol the embedded shim speaks.
- `MinProtocol` = 1 and `MaxProtocol` = 1: the oldest and the newest protocol the tool accepts.
- `ResourceName` = `DemaConsulting.CanvasNet.Tool.PandocFilter.canvasnet.lua`: the logical name of the embedded
  template (the `EmbeddedResource` of `PandocFilter\canvasnet.lua` in the tool's project file).
- `ClassesPlaceholder` = `@CLASSES@` and `SuffixesPlaceholder` = `@SUFFIXES@`: the placeholders of the template that
  `Generate` fills. Each makes the template invalid Lua, so a copy of the unfilled template fails at once instead of
  silently never starting the tool.
- `DefaultPath` = `docs/filters/canvasnet.lua`: the usual path of the shim, used in a message when the path is unknown.

**Bytes**: the shim, generated once on first use (`Lazy<byte[]>`): the embedded template read as UTF-8, passed through
`Generate`, and encoded as UTF-8 without a byte order mark.

**Shim constants**: `PROTOCOL` = `"1"`; `CLASSES`, the code-block classes the engine draws (generated:
`{ "mermaid" }`); and `SUFFIXES`, the lowercase picture name ends the engine may change (generated:
`{ ".svg", ".vl.json", ".vl", ".scad", ".vsdx", ".pptx", ".pdf", ".mmd", ".mermaid" }`, that is `.svg`, which the
engine flattens when it links a picture only the tool draws, then the inputs it draws). The header comment names the
oldest tool release that has the `pandoc-filter` command, says it is written by
`dotnet canvasnet pandoc-filter --write-shim <path>`, must not be edited, and that `CANVASNET_TOOL` replaces
`dotnet canvasnet`; the comment above the lists says `--write-shim` fills them from the tool's tables, so the shim is
written again after the tool is updated.

#### Key Methods

**Pandoc(doc)** (Lua): the filter function Pandoc calls once with the whole document.

1. **When the tool starts** (`needs_tool`): the document is walked; the tool is needed when a `CodeBlock` has a class
   that is one of `CLASSES` (exact, case-sensitive, as the engine compares it), or an `Image` is a local picture whose
   source, lowercased, ends in one of `SUFFIXES` (`has_suffix`, which compares the end of the text, so the rule is the
   engine's `KindOf` and `IsSvg`). A source is remote when it starts with a URI scheme (`^%a[%w+.-]+:`, a letter and at
   least one more character before the colon, so a drive letter such as `C:` is local). The source is not cut at a
   fragment or query, as the engine does not cut it either: `report.pdf#page=2` and `chart.vl.json?x` do not end in a
   listed suffix, and the engine leaves them alone. Everything the engine would leave alone is therefore returned
   unchanged (`nil`) without starting the tool: code blocks of any other class (`bash`, `yaml`, `csharp`, `text`, or
   `Mermaid`), rasters (`.png`, `.bmp`, `.tif`, `.ico`, `.avif`, and so on), names without a listed suffix, and remote
   pictures.
2. **The command**: `CANVASNET_TOOL` when it is set and has a non-blank character, split by `split_command` (white space
   separates, a double quote groups and may hold spaces, there are no escapes), else `dotnet canvasnet`. The first word
   is the program; the other words come before the arguments.
3. **The arguments (protocol 1)**: `pandoc-filter --json --protocol 1 --filter-path <PANDOC_SCRIPT_FILE> --format
   <FORMAT> --pandoc-version <PANDOC_VERSION>`, then `--resource-path <dir>` for each folder of
   `PANDOC_STATE.resource_path` (`.` when there is none), `--pandoc-output <file>` when Pandoc has an output file, and
   `--embed-resources` when the writer option `embed_resources` is true (read in protected mode, as older Pandoc
   versions do not have it).
4. **The run**: `pandoc.pipe` runs the program with the document as JSON (`pandoc.write(doc, "json")`) on standard
   input and returns standard output.
5. **The result**: standard output is read as a Pandoc JSON document. Each item of the metadata key
   `canvasnet-warnings` is shown with `warn("canvasnet.lua: " .. pandoc.utils.stringify(item))`, which uses
   `pandoc.log.warn` when Pandoc has it and otherwise writes `[WARNING] <message>` to standard error; the key is then
   removed. Every `Span` whose first class is `canvasnet-markdown` is replaced by its `markdown` attribute
   parsed with Pandoc's Markdown reader and turned from blocks into inline elements, so emphasis, math, and links in a
   caption work. The document is returned.

**Bytes** (C#): the generated shim (see Data Model); **Load** reads the manifest resource `ResourceName` into an
array.

**Generate(template)** (C#): replaces `ClassesPlaceholder` with a Lua list of `PictureAttributes.BlockClasses` and
`SuffixesPlaceholder` with a Lua list of `PictureAttributes.PictureSuffixes` (`{ "a", "b" }`, each item in double
quotes with a backslash or double quote escaped). Each placeholder must be in the template exactly once.

**CheckProtocol(protocol, filterPath)** (C#): accepts `MinProtocol` to `MaxProtocol` and refuses anything else (see
Error Handling). The path in the messages is `filterPath`, or `DefaultPath` when it is null or empty.

**Write(path)** (C#): writes `Bytes` to `<path>.<random 9-digit number>.partial` in the same folder (created when
missing), then moves it over `path` (`File.Move` with overwrite), so a Pandoc run that starts meanwhile reads either the
old shim or the new one, never half of it. Returns the number of bytes written.

#### Error Handling

**Lua shim**: every failure stops Pandoc with `error("canvasnet.lua: " .. message, 0)`:

- `CANVASNET_TOOL` cannot be split: `CANVASNET_TOOL has an empty quoted part: <value>`, `CANVASNET_TOOL has a quote
  that is never closed: <value>`, or `CANVASNET_TOOL names no program`.
- The program cannot start: `cannot run the canvasnet tool ('<command>'): <error>. Pin
  DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json (dotnet tool install DemaConsulting.CanvasNet.Tool) or set
  CANVASNET_TOOL to the command to run.` Only an error that is a table whose `error_code` is a number (the failure
  table `pandoc.pipe` raises for a program that ran and exited with an error) is a tool that ran; any other error (a
  string, a table without a numeric `error_code`, a userdata) is this message. The error's fields are read in protected
  mode, so a value that raises on indexing cannot hide the failure, and `<error>` is `tostring` of the error, or
  `an error that cannot be shown` when that raises too.
- The tool exits with an error and its standard output (the `output` field, when it is a string) is a JSON document
  with `error.message`: the message, followed by a space and the hint in parentheses when `error.hint` is present.
- The tool exits with an error and prints no such document: `the canvasnet tool exited with code <code> and printed no
  message`.

**Class**: `ToolException` with:

- `usage-error` for an older protocol: `<path> speaks protocol <n>; this tool needs protocol 1`, hint
  `Write the shim again: dotnet canvasnet pandoc-filter --write-shim <path>`.
- `usage-error` for a newer protocol: `<path> speaks protocol <n>; this tool speaks protocol 1 at most`, hint
  `Update DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json (dotnet tool update DemaConsulting.CanvasNet.Tool)`.
- `output-write-failed` when `Write` fails with an `IOException`, `UnauthorizedAccessException`, `ArgumentException`, or
  `NotSupportedException`: `Could not write the shim to '<path>': <reason>`, hint `Check the path and its
  permissions.`; the partial file is deleted first.

A missing embedded resource is a defect of the build: `InvalidOperationException` with
`The resource 'DemaConsulting.CanvasNet.Tool.PandocFilter.canvasnet.lua' is not embedded in the tool.` So is a template
without a placeholder or with a placeholder twice: `InvalidOperationException` with
`The shim template must hold the placeholder '<placeholder>' exactly once.`

#### Dependencies

- **ToolException** and **ErrorCodes** (Viewer library) - the `usage-error` and `output-write-failed` errors.
- **PictureCache** - `Delete` removes the partial file after a failed write.
- **PictureAttributes** - `BlockClasses` and `PictureSuffixes`, the tables `Generate` writes into the fast path.
- **Pandoc 3 Lua API** (shim) - `pandoc.pipe`, `pandoc.read`, `pandoc.write`, `pandoc.json.decode`, `pandoc.utils`,
  `pandoc.log`, and the globals `PANDOC_SCRIPT_FILE`, `FORMAT`, `PANDOC_VERSION`, `PANDOC_STATE`, and
  `PANDOC_WRITER_OPTIONS`.
- **PandocFilterCommand** (shim) - the `pandoc-filter` command the shim starts; its output contract (the
  `canvasnet-warnings` key and the `canvasnet-markdown` placeholder) is defined by *PandocAst*.

#### Callers

- **PandocFilterCommand** - `--write-shim` calls `Write` and reports `Protocol`; a run calls `CheckProtocol` before
  reading anything and uses `DefaultPath` in the hints of a missing `--protocol` or `--filter-path`.
- **Pandoc** - runs the shim as a Lua filter, listed under `filters` in the Pandoc defaults file of a repository.
