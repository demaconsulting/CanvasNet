### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`: it loads an image, drawing, or document page and
writes it as PNG, JPEG, BMP, or TIFF. Its single responsibility is to turn a command line into one pass of load,
transform, and write for one page or every page, and to report the result. The unit is the partial class
`RenderCommand` in `RenderCommand.cs` (the run and the reporting) and `RenderCommand.Options.cs` (the option table
and the reading of the settings).

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the command's `Options`): `--page <N>`, `--all-pages`, `--dpi
<D>`, `--scale <factor>`, `--width <pixels>`, `--height <pixels>`, `--size <WxH>`, `--background <color>`,
`--no-auto-orient`, `--strict`, `-o`/`--output <file>`, `--format <format>`, `--quality <1-100>`, and
`--no-clobber`, in the order they are listed in the help. The options of the OpenSCAD source (view, projection,
defines) and of the image pipeline (crop, resize, rotate, ...) are added to this table as they are implemented.

**Settings** (record): the `Input` path, the `Output` path, the `SourceOptions`, the `WriteOptions`, and `AllPages`,
produced by `ReadSettings`.

**PageOutput** (private record): one page that was loaded and written - its page number, the `SourceInfo` of the
loaded page, its diagnostics, echo, and operations, and the `WriteResult`.

The command's name is `render`, its alias is `convert`, and its usage text has examples.

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

**Run(context, args)**: parses the arguments, reads the settings, then reports every mistake about the input, the
pages, and the outputs before decoding anything: `SourceLoader.Probe` (missing, unreadable, unsupported, or corrupt
header), `SourceLoader.CheckPage` (`page-out-of-range`), `PlanTargets`, `ImageWriter.ResolveFormat`, and
`ImageWriter.CheckTarget` for every output. Then opens the source once (`SourceLoader.OpenPages`), and for each page
loads it (`ReadPage`), writes it (`ImageWriter.Write`), and disposes the pixels before the next page. The result is
written through `context.WriteLine`: as one text line per output when `context.Json` is false, or as one JSON
document.

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
| `operations` | array of objects in order, for every page: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `render` (`page`, `dpi`, `width`, `height`, `background`) for a document page, `fit` (`width`, `height`) when a page was fitted inside a box, `auto-orient` (`orientation`), `resize` (`width`, `height`), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output; with `--all-pages` of a document every operation also has `page` |
| `outputs` | array with one object per page written: `path`, `format`, `width`, `height`, `bytes`, and for a document `page` |
| `diagnostics` | array of objects: `source` (`pptx` or `vsdx`), `severity` (`unsupported`), `code` (the feature token), `message`, and `page` |
| `echo` | array of strings (empty for the formats of this release) |
<!-- markdownlint-enable MD013 -->

#### Error Handling

Command-line mistakes (no input or more than one, no output, an unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format, `--page` with `--all-pages`, a page or a resolution that is not a
number in range, `--all-pages` with an output that is the same file for every page) are `ToolException` with
`usage-error`; a size larger than the maximum is `size-limit`. Input failures come from `SourceLoader`
(`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature` - also with
`--strict` for a skipped picture or approximated construct - `page-out-of-range`, `size-limit`) and output failures
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
