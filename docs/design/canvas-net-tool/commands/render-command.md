### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`: it loads an image, drawing, document page, or 3D
model view and writes it as PNG, JPEG, BMP, or TIFF. Its single responsibility is to turn a command line into one
pass of load, transform, and write for one page or every page, and to report the result. The unit is the partial
class `RenderCommand` in `RenderCommand.cs` (the run and the reporting) and `RenderCommand.Options.cs` (the option
table and the reading of the settings).

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the command's `Options`): `--page <N>`, `--all-pages`, `--dpi
<D>`, `--scale <factor>`, `--width <pixels>`, `--height <pixels>`, `--size <WxH>`, `--background <color>`,
`--no-auto-orient`, `--strict`, the options of an OpenSCAD model `--view <name>`, `--azimuth <degrees>`,
`--elevation <degrees>`, `--projection <type>`, `--define <name=value>` (alias `-D`, repeatable), `--time <T>`,
`--default-color <color>`, and `--max-triangles <N>`, then `-o`/`--output <file>`, `--format <format>`,
`--quality <1-100>`, and `--no-clobber`, in the order they are listed in the help. The options of the image
pipeline (crop, resize, rotate, ...) are added to this table as they are implemented.

**Settings** (record): the `Input` path, the `Output` path, the `SourceOptions`, the `WriteOptions`, and `AllPages`,
produced by `ReadSettings`.

**PageOutput** (private record): one page that was loaded and written - its page number, the `SourceInfo` of the
loaded page, its diagnostics, echo, and operations, and the `WriteResult`.

The command's name is `render`, its alias is `convert`, and its usage text has examples, and says how an OpenSCAD
model is drawn (the views, the 800 by 800 default size, `-D`) and where its `include` and `use` files are read from.

#### Key Methods

**ReadSettings(reader)**: requires exactly one positional argument (the input); requires `-o`; reads the size
options (`--width` and `--height` as whole numbers of at least 1, `--scale` as a number from 0.001 to 8192, `--size`
with `ArgumentReader.ParseSize`, which refuses a side beyond the maximum with `size-limit`); rejects `--size`
combined with `--scale`, `--width`, or `--height`, and `--scale` combined with `--width` or `--height`
(`usage-error`); reads `--page` with `ArgumentReader.ParsePage` (1 or more) and `--dpi` with
`ArgumentReader.ParseDpi` (greater than 0 and at most 2400), and rejects `--page` together with `--all-pages`;
reads `--strict` and `--all-pages` as switches; reads `--background` with `ParseColor`; reads `--quality` as 1 to
100 (default 90); and validates `--format` early. A JPEG is flattened over the background color when it has some
opacity, otherwise over white. The option values reach `SourceLoader` and `ImageWriter` through `SourceOptions` and
`WriteOptions`.

**ReadScadOptions(reader)**: when none of the model options was given, returns `null` (the loader then uses the
defaults: the `iso` view, orthographic). Otherwise reads `--view` (one of the names in `SourceLoader.ScadViews`, in
any case; another name is a `usage-error` whose hint lists the views), `--azimuth` with `ArgumentReader.ParseDegrees`
(normalized to 0 up to 360), `--elevation` as a number from -90 to 90 (read as a number, not as an angle to
normalize, so that -90 is the bottom view), `--projection` (`ortho` or `orthographic`, or `perspective`; another word
is a `usage-error`), every `--define` with `ArgumentReader.ParseDefines` (numbers, `true` and `false`, and text; a
later definition of a name wins), `--time` as a number from -1e9 to 1e9, `--max-triangles` as a whole number from 1
up to 2,147,483,647, and `--default-color` with `ParseColor`. The result is a `ScadViewOptions`.

**Run(context, args)**: parses the arguments, reads the settings, then reports every mistake about the input, the
pages, and the outputs before decoding anything: `SourceLoader.Probe` (missing, unreadable, unsupported, or corrupt
header; for a model, a syntax error), the check that model options were given only for a model (a model option with
another kind of input is a `usage-error` naming the kind, rather than being ignored), `SourceLoader.CheckPage`
(`page-out-of-range`; a model has only page 1), `PlanTargets`, `ImageWriter.ResolveFormat`, and
`ImageWriter.CheckTarget` for every output. Then opens the source once (`SourceLoader.OpenPages`), and for each page
loads it (`ReadPage`), writes it (`ImageWriter.Write`), and disposes the pixels before the next page. The result is
written through `context.WriteLine`: as one text line per output when `context.Json` is false, or as one JSON
document. The diagnostics and the echo of a model are part of the JSON document only; the text result is the one
line, so a caller that needs them uses `--json`.

**PlanTargets**: decides the output file of each page. `{page}` in the output is replaced by the page number (for one
page too). With `--all-pages`, an output that is an existing directory or ends with a directory separator names a
directory, and each page is written there as `{input name without extension}-{page}` with the extension of the
format (`.png` unless `--format` says otherwise); an output that has no `{page}` and is not a directory is a
`usage-error` when the input has more than one page (a single image has one page, so its output is used as given).
Because every target is planned and checked first, a mistake in any of them (for example an existing file with
`--no-clobber`) is reported before any page is written. A failure while a later page is rendered or written leaves
the files of the pages that were written before it, each of which is complete (the writer is atomic per file).

**Pipeline hook**: between the load and the write, the image pipeline (crop, resize, rotate, flip, pad) is applied
by later releases; the operations it applies are appended to the list reported in the result.

**Text result**: `wrote {output} ({width}x{height} {format}, {bytes} bytes) from {input}`, with `page {n}`
appended for a document, for example `wrote out.png (816x1056 png, 41208 bytes) from in.svg` and
`wrote p2.png (192x128 png, 3105 bytes) from deck.pdf page 2`; one line per page with `--all-pages`.

**JSON result**: `ResultWriter.Success("render", ...)` with these fields after the envelope, in this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `tool` | object: `name` (`canvasnet`), `version` |
| `input` | object: `path`, `format`, for a PDF, PPTX, or VSDX also `page` (omitted with `--all-pages`) and `pageCount`, and `natural` (object: `width`, `height`; the stored or intrinsic size, or the size of the first page rendered at the resolution) |
| `operations` | array of objects in order, for every page: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `render` (`page`, `dpi`, `width`, `height`, `background`) for a document page, `render` (`view`, `azimuth`, `elevation`, `projection`, `width`, `height`, `background`, and when given `defines` as a list of `name=value`, `time`, `maxTriangles`, `defaultColor`) for a model, `fit` (`width`, `height`) when a page was fitted inside a box, `auto-orient` (`orientation`), `resize` (`width`, `height`), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output; with `--all-pages` of a document every operation also has `page` |
| `outputs` | array with one object per page written: `path`, `format`, `width`, `height`, `bytes`, and for a document `page` |
| `diagnostics` | array of objects: `source` (`pptx`, `vsdx`, or `scad`), `severity` (`unsupported`, or `warning` for a model), `code` (the feature token, or `scad-warning`), `message`, and `page` (documents only) |
| `echo` | array of strings: the `echo` output of a model as the SCAD library prints it, with its `ECHO:` prefix (empty for every other source) |
<!-- markdownlint-enable MD013 -->

#### Error Handling

Command-line mistakes (no input or more than one, no output, an unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format, `--page` with `--all-pages`, a page or a resolution that is not a
number in range, `--all-pages` with an output that is the same file for every page, a view or projection that does
not exist, a model value that is malformed or out of range, a definition that is not `name=value`, and a model
option for an input that is not a model) are `ToolException` with
`usage-error`; a size larger than the maximum is `size-limit`. Input failures come from `SourceLoader`
(`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature` - also with
`--strict` for a skipped picture, an approximated construct, or an unsupported feature of a model -
`page-out-of-range`, `size-limit`, `scad-error` for a syntax error, a failed `assert`, or an `include` that cannot be
read, and `budget-exceeded` for a model that exceeds a limit such as `--max-triangles`) and output failures
from `ImageWriter` (`same-input-output`, `output-exists`, `output-write-failed`); none of them leaves an output file
for the page that failed. Null arguments throw `ArgumentNullException`. The registry records the command name on
every `ToolException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **SourceLoader** and **ImageWriter** (Imaging) - everything that touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `render` and `convert`.
