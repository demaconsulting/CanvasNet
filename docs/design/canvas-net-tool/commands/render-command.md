### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`, and the verb commands `crop`, `resize`, `rotate`,
`flip`, and `pad`: it loads an image, drawing, document page, or 3D model view, transforms it with the image
pipeline, and writes it as PNG, JPEG, BMP, or TIFF. Its single responsibility is to turn a command line into one
pass of load, transform, and write for one page or every page, and to report the result. The unit is the partial
class `RenderCommand` in `RenderCommand.cs` (the run and the reporting), `RenderCommand.Options.cs` (the option
table and the reading of the settings), and `RenderCommand.Verbs.cs` (the verbs). The verbs are not separate
classes: `new RenderCommand(verb)` is the verb command, which differs from `render` only in its name, summary, usage
text, and in taking one more positional operand, which becomes the first operation of the image pipeline. Because
verb and `render` share every line of the run, `crop in.png 10,10,100,100 -o out.png` writes exactly the file that
`render in.png --crop 10,10,100,100 -o out.png` writes.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of `render` and of every verb): `--page <N>`,
`--all-pages`, `--dpi <D>`, `--scale <factor>`, `--width <pixels>`, `--height <pixels>`, `--size <WxH>`,
`--background <color>`, `--no-auto-orient`, `--strict`, the options of an OpenSCAD model `--view <name>`,
`--azimuth <degrees>`, `--elevation <degrees>`, `--projection <type>`, `--define <name=value>` (alias `-D`,
repeatable), `--time <T>`, `--default-color <color>`, and `--max-triangles <N>`, the options of the image
pipeline `--crop <geometry>`, `--resize <WxH>`, `--fit <WxH>`, `--allow-upscale`, `--rotate <degrees>`,
`--flip <axis>`, `--pad <N|l,t,r,b>`, `--pad-color <color>`, and `--filter <name>`, then `-o`/`--output <file>`,
`--format <format>`, `--quality <1-100>`, and `--no-clobber`, in the order they are listed in the help.

**Settings** (record): the `Input` path, the `Output` path, the `SourceOptions`, the `WriteOptions`, `AllPages`, and
the `Operations` of the image pipeline, produced by `ReadSettings`.

**PageOutput** (private record): one page that was loaded, transformed, and written - its page number, the
`SourceInfo` of the loaded page, its diagnostics, echo, and operations, and the `WriteResult`.

**VerbTexts** (private table): for each verb, the operand placeholder (`<geometry>`, `<WxH>`, `<degrees>`, `<axis>`,
or `<border>`), the summary, a description of the operand, and the examples; `VerbUsage` builds the usage text from
it.

The command's name is `render`, its alias is `convert`, and its usage text has examples, says how the pipeline options
combine, and says how an OpenSCAD model is drawn (the views, the 800 by 800 default size, `-D`) and where its
`include` and `use` files are read from. A verb has no alias, and its usage text is `canvasnet {verb} <input>
{operand} -o <output> [options]` with the description of its operand, a statement that it takes every option of
`render`, and its examples.

#### Key Methods

**ReadSettings(reader, verb)**: requires exactly one positional argument (the input) for `render`, and exactly two
(the input and the operand, in that order) for a verb; requires `-o` (the hint shows an example of the command);
reads the size
options (`--width` and `--height` as whole numbers of at least 1, `--scale` as a number from 0.001 to 8192, `--size`
with `ArgumentReader.ParseSize`, which refuses a side beyond the maximum with `size-limit`); rejects `--size`
combined with `--scale`, `--width`, or `--height`, and `--scale` combined with `--width` or `--height`
(`usage-error`); reads `--page` with `ArgumentReader.ParsePage` (1 or more) and `--dpi` with
`ArgumentReader.ParseDpi` (greater than 0 and at most 2400), and rejects `--page` together with `--all-pages`;
reads `--strict` and `--all-pages` as switches; reads `--background` with `ParseColor`; reads `--filter` (which the
loader uses for the resize of a raster that `--scale`, `--width`, or `--height` ask for, and the pipeline uses for
`--resize` and `--fit`); reads `--quality` as 1 to
100 (default 90); and validates `--format` early. A JPEG is flattened over the background color when it has some
opacity, otherwise over white. The option values reach `SourceLoader` and `ImageWriter` through `SourceOptions` and
`WriteOptions`. Last, `ImagePipeline.Parse` reads the operations (the verb's first, then the options in
command-line order), so a malformed operation is reported before anything is decoded.

**ReadScadOptions(reader)**: when none of the model options was given, returns `null` (the loader then uses the
defaults: the `iso` view, orthographic). Otherwise reads `--view` (one of the names in `SourceLoader.ScadViews`, in
any case; another name is a `usage-error` whose hint lists the views), `--azimuth` with `ArgumentReader.ParseDegrees`
(normalized to 0 up to 360), `--elevation` as a number from -90 to 90 (read as a number, not as an angle to
normalize, so that -90 is the bottom view), `--projection` (`ortho` or `orthographic`, or `perspective`; another word
is a `usage-error`), every `--define` with `ArgumentReader.ParseDefines` (numbers, `true` and `false`, and text; a
later definition of a name wins), `--time` as a number from -1e9 to 1e9, `--max-triangles` as a whole number from 1
up to 2,147,483,647, and `--default-color` with `ParseColor`. The result is a `ScadViewOptions`. `ReadScadOptions` and
the table `ScadOptionNames` are `internal` so that `CompareCommand` and `MontageCommand` read the model options with the
same code; the view name is checked by the `internal` method `ParseScadView`, which they use for `--a-view` and
`--b-view` too.

**SpecsFor(names)**: `internal`; returns the declarations of `OptionTable` with the given long names, in that order
(`ArgumentException` for a name the table does not declare), so that another command accepts an option with the same
grammar and the same help text. `CompareCommand` and `MontageCommand` build their option tables from it. `WriteField`,
which writes one parameter of an operation to a JSON object, is `internal` for `CompareCommand`.

**Run(context, args)**: parses the arguments, reads the settings, then reports every mistake about the input, the
pages, and the outputs before decoding anything: `SourceLoader.Probe` (missing, unreadable, unsupported, or corrupt
header; for a model, a syntax error), the check that model options were given only for a model (a model option with
another kind of input is a `usage-error` naming the kind, rather than being ignored), `SourceLoader.CheckPage`
(`page-out-of-range`; a model has only page 1), `PlanTargets`, `ImageWriter.ResolveFormat`, and
`ImageWriter.CheckTarget` for every output. Then opens the source once (`SourceLoader.OpenPages`), and for each page
loads it (`ReadPage`), runs it through `TransformAndStage`, and disposes the pixels before the next page. Each page's
result is encoded to a temporary file next to its target, and no target is touched until every page has been rendered
and encoded; then the temporary files are moved into place one at a time (`StagedOutput.Commit`, overwriting unless
`--no-clobber`), and a `finally` block deletes every temporary file that was not committed. The result is
written through `context.WriteLine`: as one text line per output when `context.Json` is false, or as one JSON
document. The diagnostics and the echo of a model are part of the JSON document only; the text result is the one
line, so a caller that needs them uses `--json`.

**TransformAndStage(loaded, target, settings, isDocument)**: when there are operations, calls `ImagePipeline.Apply` on
the loaded surface (the loaded surface stays untouched and is disposed with the loaded source; the transformed one is
disposed in a `finally` block after the staging) and stages the transformed surface with `ImageWriter.Stage`;
with none, stages the loaded surface. It returns the page's report and the `StagedOutput`. A border with no color
of its own gets the default: the `--background` color when given (also a transparent one), otherwise white for a
vector source (SVG, document page, model, which the loader flattened over white) and
transparent for a raster. Everything the pipeline does happens before the output is published, so a failing operation
(for example a crop that starts outside the image after an earlier crop) writes nothing and leaves an existing output
untouched.

**CollectOperations**: the operations to report for a page, in this order: the loader's (`auto-orient`, `resize`,
`flatten`, `render`, ...), the pipeline's, and the `flatten` of a JPEG when the writer flattened transparency; with
`--all-pages` of a document each operation without a `page` gets one.

**Interaction of the loader's sizing and the pipeline**: the size options of the source (`--scale`, `--width`,
`--height`, `--size`) are carried out by the loader, so they act before the first operation of the pipeline: a vector
source is rendered at the requested size (and never scaled again), a raster source is decoded, turned upright by its
EXIF orientation, and resized once (with `--filter`), and then the pipeline starts from that image. A crop given
with `--width 24` therefore crops the 24-pixel-wide image. The pipeline's own `--resize` and `--fit` apply to the
pixels at their point of the chain.

**PlanTargets**: decides the output file of each page. `{page}` in the output is replaced by the page number (for one
page too). With `--all-pages`, an output that is an existing directory or ends with a directory separator names a
directory, and each page is written there as `{input name without extension}-{page}` with the extension of the
format (`.png` unless `--format` says otherwise); an output that has no `{page}` and is not a directory is a
`usage-error` when the input has more than one page (a single image has one page, so its output is used as given).
Because every target is planned and checked first, a mistake in any of them (for example an existing file with
`--no-clobber`) is reported before any page is rendered. Rendering and encoding of every page completes before any
target is touched: a failure while a later page is loaded, transformed, or encoded (for example an unsupported feature
with `--strict`) leaves no new or partial output and every existing output untouched. The final per-target moves are
best-effort and not transactional (no rollback is attempted): if a move itself fails (for example a target became
no longer writable, or appeared while `--no-clobber` is set), the targets moved before it remain, each complete,
and the error is reported.

**Text result**: `wrote {output} ({width}x{height} {format}, {bytes} bytes) from {input}`, with `page {n}`
appended for a document, for example `wrote out.png (816x1056 png, 41208 bytes) from in.svg` and
`wrote p2.png (192x128 png, 3105 bytes) from deck.pdf page 2`; one line per page with `--all-pages`.

**JSON result**: `ResultWriter.Success(Name, ...)` (the command is `render`, or the verb that was run) with these
fields after the envelope, in this order:

<!-- markdownlint-disable MD013 -->
| Key | Value |
| --- | ----- |
| `tool` | object: `name` (`canvasnet`), `version` |
| `input` | object: `path`, `format`, for a PDF, PPTX, or VSDX also `page` (omitted with `--all-pages`) and `pageCount`, and `natural` (object: `width`, `height`; the stored or intrinsic size, or the size of the first page rendered at the resolution) |
| `operations` | array of objects in order, for every page: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `render` (`page`, `dpi`, `width`, `height`, `background`) for a document page, `render` (`view`, `azimuth`, `elevation`, `projection`, `width`, `height`, `background`, and when given `defines` as a list of `name=value`, `time`, `maxTriangles`, `defaultColor`) for a model, `fit` (`width`, `height`) when a page was fitted inside a box, `auto-orient` (`orientation`), `resize` (`width`, `height`, and `filter` when `--filter` was given), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output; then the operations of the image pipeline in the order written: `crop` (`x`, `y`, `width`, `height`, `clamped`), `resize` (`width`, `height`, `filter`), `fit` (`width`, `height`, `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled`), `rotate` (`degrees`, `width`, `height`), `flip` (`axis`, `width`, `height`), and `pad` (`left`, `top`, `right`, `bottom`, `color`, `width`, `height`); a JPEG's `flatten` comes last; with `--all-pages` of a document every operation also has `page` |
| `outputs` | array with one object per page written: `path`, `format`, `width`, `height`, `bytes`, and for a document `page` |
| `diagnostics` | array of objects: `source` (`pptx`, `vsdx`, or `scad`), `severity` (`unsupported`, or `warning` for a model), `code` (the feature token, or `scad-warning`), `message`, and `page` (documents only) |
| `echo` | array of strings: the `echo` output of a model as the SCAD library prints it, with its `ECHO:` prefix (empty for every other source) |
<!-- markdownlint-enable MD013 -->

#### Error Handling

Command-line mistakes (no input or more than one, a verb without its operand or with more than one, no output, an
unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format, `--page` with `--all-pages`, a page or a resolution that is not a
number in range, `--all-pages` with an output that is the same file for every page, a view or projection that does
not exist, a model value that is malformed or out of range, a definition that is not `name=value`, a model
option for an input that is not a model, a malformed operation value, an angle that is not 90, 180, or 270 degrees,
`--allow-upscale` without `--fit`, `--pad-color` without a pad, and a crop that starts outside the image or is
smaller than a pixel) are `ToolException` with
`usage-error`; a size, box, or border larger than the maximum, or a border that makes the image larger than the
maximum, is `size-limit`. Input failures come from `SourceLoader`
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
- **SourceLoader**, **ImagePipeline**, and **ImageWriter** (Imaging) - everything that touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `render` and `convert`, and one instance for each
  verb (`crop`, `resize`, `rotate`, `flip`, and `pad`).
- **CompareCommand** and **MontageCommand** - the option declarations (`SpecsFor`), `ReadScadOptions`,
  `ScadOptionNames`, `ParseScadView`, and `WriteField`.
