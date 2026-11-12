### DiscoveryCommands

<!-- cspell:ignore canvasnet jpeg tiff pptx vsdx scad Visio openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`DiscoveryCommands` holds the read-only commands that tell a caller what the tool can do or what a file is. Its
single responsibility is to answer "what do you support?" and "what is this file?" without writing any image. It
provides two commands, `formats` and `info`, implemented as the nested classes `DiscoveryCommands.Formats` and
`DiscoveryCommands.Info`. The table of formats is data owned by this unit.

#### Data Model

**InputFormats** (`IReadOnlyList<InputFormat>`): one row per input format, in listing order - `png`, `jpeg`, `bmp`,
`tiff`, `gif`, `jpeg2000`, `svg`, `pdf`, `pptx`, `vsdx`, `scad`, `vega-lite`, `mermaid`. A row (`InputFormat`) has
`Name`, `Kind` (`raster`: decoded at its own size; `vector`: a drawing rendered at any requested size; `document`: pages
or slides rendered at any requested size; `model`: a 3D model rendered from a viewpoint; `chart`: a Vega-Lite chart
rendered at any requested size; `diagram`: a Mermaid diagram rendered at any requested size), `Extensions` (lower case,
with the dot), `MultiPage` (one file holds pages or slides that can be chosen), and `Vector` (rendered at the requested
size rather than decoded at its own size), and `HelpTopic` (the help topic that describes the format: `raster` for the
six raster formats, otherwise the name of the format). The rows are the `SourceKind` members in order, and their
extensions are those
that `SourceLoader.ExtensionKinds` maps to the kind (the `vega-lite` row lists `.vl.json` and `.vl`: `.vl.json` is
the two-part suffix `SourceLoader.VegaLiteJsonSuffix`, which is checked on the file name and cannot be an
extension; the `mermaid` row, of the kind `diagram`, lists `.mmd` and `.mermaid`, is a vector format, is not multi-page,
and has the help topic `mermaid`); a test keeps the table consistent with the enumeration and the
loader, so a listed format is one the code knows, and a further test probes a file of every listed kind to prove that
the loader reads it.

**OutputFormats** (`IReadOnlyList<OutputFormatInfo>`): `png`, `jpeg`, `bmp`, `tiff`. A row has `Name` (also the value
of the `--format` option), `Extensions`, `Alpha` (keeps transparency), and `Lossy` (takes a quality setting). The
rows are the `OutputFormat` members in order, with the extensions that `ImageWriter.ExtensionFormats` accepts.

**Formats**: `ICommand` named `formats`, with no aliases and no options of its own. It holds the tool version and the
CanvasNet library version it reports. The public constructor reads them from the assemblies'
informational versions (falling back to the assembly version, then `0.0.0`); an `internal` constructor takes them as
arguments so that tests can produce output that does not depend on the build.

**Info**: `ICommand` named `info`, with no aliases and two options, `--page <N>` and `--all-pages` (`--json` is
global). It holds no state.

**Constants**: the JPEG quality range (`1` to `100`) and default (`90`), which are those of `ImageWriter`, and the
maximum image dimension, which is `Surface.MaxDimension` (8192).

#### Key Methods

**Formats.Run(context, args)**: reads the arguments with `ArgumentReader` (against an empty option table) and
requires no positional arguments, so a stray argument or an unknown option is a `usage-error`. Then writes either
the JSON document (when `context.Json`) or the text listing, through `context.WriteLine`.

**Text listing**: the sections _Input formats_ (a table of format, kind, extensions, pages, scalable, and the help
topic as `help <topic>`), _Output formats_ (format, extensions, alpha, quality), _Limits_ (maximum image dimension,
JPEG quality range and default), and _Versions_ (tool and library), then a blank line and the footer
`Run 'canvasnet help <topic>' for supported features, examples, and known differences (for example
'canvasnet help vega-lite'). An SVG drawing can embed files of these formats with <image href>; see 'canvasnet help svg'.`
Columns are aligned and the last column is not padded. The help topic of a format is
the `HelpTopic` of its `InputFormat` row: `raster` for the six raster formats, otherwise the name of the format.

**JSON document**: written through `ResultWriter.Success("formats", ...)` with these fields after the envelope, in
this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `tool` | object: `name` (`canvasnet`), `version` |
| `library` | object: `name` (`DemaConsulting.CanvasNet`), `version` |
| `inputs` | array of objects: `format`, `kind`, `extensions` (array), `multiPage` (bool), `vector` (bool), `helpTopic` (the name of the help topic that describes the format) |
| `outputs` | array of objects: `format`, `extensions` (array), `alpha` (bool), `lossy` (bool) |
| `quality` | object: `min`, `max`, `default` |
| `maxDimension` | number |
<!-- markdownlint-enable MD013 -->

**Info.Run(context, args)**: reads the arguments with `ArgumentReader` (against the two page options) and requires
exactly one positional argument, the input, and rejects `--page` together with `--all-pages` (`usage-error`). Calls
`SourceLoader.Probe`, checks the page (`--page` counted from 1 with `ArgumentReader.ParsePage`, default 1;
`page-out-of-range` for a page the file does not have, for a single image too), and writes either the JSON document
(when `context.Json`) or the text through `context.WriteLine`. For a PDF, PPTX, or VSDX document the pages
described are page 1 by default, the page chosen with `--page`, or every page with `--all-pages`; a single image
accepts the options and describes itself as without them. Nothing is decoded and no file is written.

**Info text**: for an image, one line: `{path}: {format}, {width}x{height} px[ (intrinsic size)], {channels}
channel[s][ with alpha], {bytes} bytes`, followed when they apply by `, {n} frames (the first is used)` (an animated
GIF), `, EXIF orientation {n}` (a JPEG that has one), and `, cannot be decoded`. The size is the stored size, before
any EXIF orientation. For a document, a line `{path}: {format}, {n} pages|slides, {bytes} bytes`, then for each
page described a line `page|slide {n}["{name}"]: {w}x{h} px at 96 dpi ({w}x{h} pt)[, rotation {r}]` (a slide of a
presentation is called a slide; sizes in points are rounded to two decimals). For an OpenSCAD model, a line
`{path}: scad, {n} module[s], {n} function[s], {n} include/use directive[s], {bytes} bytes`, then a line
`module {name}` for each module, `function {name}` for each function, and `include <{path}> (line {n})` or
`use <{path}> (line {n})` for each directive, all in source order. For a Vega-Lite chart, one line:
`{path}: vega-lite, {w}x{h} px (laid out), {n} mark[s], {n} data reference[s], {n} parameter[s][, title "{title}"]
[, schema v{n}], {bytes} bytes` (the size is the one the library lays the chart out at; a title of several lines is
joined with spaces). For a Mermaid diagram, one line:
`{path}: mermaid, {w}x{h} px (laid out), {type}[ {direction}][ (not drawn by this version)], {n} node[s], {n} edge[s],`
`{n} subgraph[s][, title "{title}"][, accTitle "{accTitle}"], {bytes} bytes` (for example
`flow.mmd: mermaid, 674x286 px (laid out), flowchart LR, 5 nodes, 5 edges, 1 subgraph, title "Order flow", 313 bytes`;
a title of several lines is joined with spaces).

**Info JSON document**: `ResultWriter.Success("info", ...)` with these fields after the envelope, in this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `input` | object: `path`, `format`, `bytes` |
| `image` | for an image: object: `width`, `height`, `channels`, `hasAlpha`, `canDecode`, `frameCount`, and `exifOrientation` only when the file has one |
| `document` | for a PDF, PPTX, or VSDX document, instead of `image`: object: `pageCount` and `pages`, an array with one object for each page described: `page`, `width`, `height` (pixels at 96 dpi), `widthPt`, `heightPt`, `rotation` (PDF only), and `name` (VSDX only) |
| `chart` | for a Vega-Lite chart, after `image` (which gives the laid-out `width` and `height`, 4 `channels`, `hasAlpha` true, `canDecode` true, and `frameCount` 1): object: `title` and `schemaMajorVersion` only when the specification has them, `marks` (an array of the mark types as written, in document order, including those of layers and concatenations), `dataReferences` (an array of objects in document order: `kind` (`inline`, `url`, `name`, or `sequence`), `url` as written for a `url` source, `rows` when the text gives the row count, and `line` of the `data` object), and `parameters` (an array of the declared parameter names) |
| `diagram` | for a Mermaid diagram, after `image` (which gives the laid-out `width` and `height`, 4 `channels`, `hasAlpha` true, `canDecode` true, and `frameCount` 1), written by `WriteDiagram`: object: `type` (for example `flowchart`, `sequence`, or `mindmap`), `supported` (false for a type that is recognized but not drawn, such as a user journey, which `render` draws as an empty picture with the diagnostic `mermaid-diagram-unsupported`), `direction`, `title`, and `accTitle` only when the diagram has them, and `nodes`, `edges`, and `subgraphs` (for a mindmap its nodes, one edge fewer than its nodes, and 0 subgraphs; for a packet diagram its blocks as written, 0 edges, and 0 subgraphs; for a git graph its commits, the links to their parents as edges, and 0 subgraphs; 0 for a type that is not drawn) |
| `model` | for an OpenSCAD model, instead of `image`: object: `modules` and `functions` (arrays of the names defined at file scope, in source order, a repeated name kept) and `directives` (an array of objects: `kind` (`include` or `use`), `path` as written, and `line`) |
<!-- markdownlint-enable MD013 -->

The model is parsed, not evaluated, so `info` neither runs its code nor reads the files it includes (their
existence is not checked, and a syntax error in an included file is found only when the model is rendered).

A chart is parsed and measured, not drawn: `info` reads no data file and contacts no network to describe it (the data
references are the text of the specification), but the library lays the chart out to say how large it is, which reads
the data the chart names inline. The `chart` object keeps the style of the `model` object: arrays are always present
(empty when the chart declares nothing) and the optional scalars are omitted when absent.

A diagram is parsed and measured, not drawn: the library lays it out with the default theme and without the pictures of
its nodes (no other file is read), so the `supported` member tells an agent before it renders that a type would give an
empty picture. The `diagram` object keeps the style of the `chart` object: the counts are always present and the
optional strings are omitted when absent.

**Input fragment (info).** `info` probes its input with `SourceLoader.Probe(InputReference.Parse(input))`: the page of
the fragment replaces `--page` and `--all-pages`, the `input` object of the JSON result has the `fragment` member, and
the warnings about the fragment are written to standard error in text mode (the JSON document of `info` has no
`diagnostics`; a script that needs them uses `render --json`). The other parameters of the fragment are checked and
ignored, because `info` only reads headers: a `theme=` fragment of a diagram is checked (a name that is not a Mermaid
theme is `image-fragment-invalid`) and does not change the description, which is always measured in the default theme.

#### Error Handling

An argument or option on the `formats` command line throws a `ToolException` with the code `usage-error` (from
`ArgumentReader`); so does a missing input, an extra input, an unknown option, or both page options on the `info`
command line. The `info` command's failures to read the file come from `SourceLoader.Probe` (`input-not-found`,
`input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature`, `scad-error` for a model with a
syntax error, and `vega-lite-error` for a chart with an invalid specification, `budget-exceeded` for a chart that
exceeds a limit of the library, and `size-limit` for a chart that lays out larger than the maximum image size;
`mermaid-error` for a diagram that is not valid text, `budget-exceeded` for a diagram that exceeds a limit of the
library, and `corrupt-input` for a diagram text longer than the text limit; a diagram whose natural size is larger than
the maximum image size is described with that size, not refused), and
a page the file does not have
is `page-out-of-range`. Null arguments throw `ArgumentNullException`. The `formats` command does not touch the file
system and has no other failure mode.

#### Dependencies

- **ArgumentReader** - argument validation.
- **ResultWriter** - the JSON documents.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version.
- **SourceLoader** and **ImageWriter** (Imaging) - the `info` command's probe, and the format and quality
  definitions that the tables and constants must agree with.
- **CanvasNet core** - `Surface.MaxDimension` and the library version.

#### Callers

- **CommandRegistry** - `CreateDefault` registers `Formats` and `Info`; the registry dispatches to them.
- **Validation** - the `CanvasNetTool_Formats` and `CanvasNetTool_Info` self-tests run them in-process.
