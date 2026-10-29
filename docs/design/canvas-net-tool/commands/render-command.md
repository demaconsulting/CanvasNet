### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad grayscale deskew -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`, and the verb commands `crop`, `resize`, `rotate`,
`flip`, and `pad`: it loads an image, drawing, document page, 3D model view, or Vega-Lite chart, transforms it with
the image
pipeline (including the photo tools: tone adjustments, deskew, trim, and grid), and writes it as PNG, JPEG, BMP, or
TIFF. Its single responsibility is to turn a command line into one
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
`--rotate-fill <color>`, `--flip <axis>`, `--pad <N|l,t,r,b>`, `--pad-color <color>`, the photo tools
`--grayscale`, `--invert`, `--threshold <0-255|auto>`, `--levels <black,white[,gamma]>`, `--brightness <n>`,
`--contrast <n>`, `--auto-levels`, `--auto-levels-clip <percent>`, `--deskew`, `--trim`, `--trim-tolerance <0-255>`,
and `--grid <N[,color]>`, and `--filter <name>`, then `-o`/`--output <file>`,
`--format <format>`, `--quality <1-100>`, and `--no-clobber`, in the order they are listed in the help. The photo tools
are declared here once, so `render`, the verbs, and `inspect` accept them with one grammar; `compare` and `montage` pick
their options from the table by name (`SpecsFor`) and so do not inherit them (`compare --threshold metric=value` and
`compare --grid CxR` are unrelated to `render --threshold` and `--grid`).

**Settings** (record): the `Input` path, the `Output` path, the `SourceOptions`, the `WriteOptions`, `AllPages`, and
the `Operations` of the image pipeline, produced by `ReadSettings`.

**PageOutput** (private record): one page that was loaded, transformed, and written - its page number, the
`SourceInfo` of the loaded page, its diagnostics, echo, and operations, and the `WriteResult`.

**VerbTexts** (private table): for each verb, the operand placeholder (`<geometry>`, `<WxH>`, `<degrees>`, `<axis>`,
or `<border>`), the summary, a description of the operand, and the examples; `VerbUsage` builds the usage text from
it.

The command's name is `render`, its alias is `convert`, and its usage text has examples, says how the pipeline options
combine, and says how an OpenSCAD model is drawn (the views, the 800 by 800 default size, `-D`) and where its
`include` and `use` files are read from, and how a Vega-Lite chart is drawn (at its laid-out size over its own
background, with UTC dates and the bundled fonts; its local CSV, TSV, DSV (`format.delimiter`), and JSON data files are
read from the folder of the specification
and below it only, a missing one is an error, delimited text is not type-inferred unless `format.parse` is `"auto"`,
there is no data-folder option, and nothing is read from the network; no
options of its own). A verb has no alias, and its
usage text is `canvasnet {verb} <input>
{operand} -o <output> [options]` with the description of its operand, a statement that it takes every option of
`render`, and its examples.

#### Key Methods

**ReadSettings(reader, verb)**: requires exactly one positional argument (the input) for `render`, and exactly two
(the input and the operand, in that order) for a verb; requires `-o` (the hint shows an example of the command);
reads the options that decide how the input is loaded with `ReadSourceOptions`; reads `--quality` as 1 to
100 (default 90); rejects `--page` together with `--all-pages`; reads `--all-pages` as a switch; and validates
`--format` early. A JPEG is flattened over the background color when it has some
opacity, otherwise over white. The option values reach `SourceLoader` and `ImageWriter` through `SourceOptions` and
`WriteOptions`. Last, `ImagePipeline.Parse` reads the operations (the verb's first, then the options in
command-line order), so a malformed operation is reported before anything is decoded.

**ReadSourceOptions(reader)**: `internal`, extracted from `ReadSettings` without a change of grammar so that `inspect`
reads the same source options. Reads the size
options (`--width` and `--height` as whole numbers of at least 1, `--scale` as a number from 0.001 to 8192, `--size`
with `ArgumentReader.ParseSize`, which refuses a side beyond the maximum with `size-limit`); rejects `--size`
combined with `--scale`, `--width`, or `--height`, and `--scale` combined with `--width` or `--height`
(`usage-error`); reads `--page` with `ArgumentReader.ParsePage` (1 or more) and `--dpi` with
`ArgumentReader.ParseDpi` (greater than 0 and at most 2400); reads `--strict`; reads `--background` with `ParseColor`;
reads `--no-auto-orient`; reads `--filter` (which the
loader uses for the resize of a raster that `--scale`, `--width`, or `--height` ask for, and the pipeline uses for
`--resize` and `--fit`); and reads the model options with `ReadScadOptions`. It returns the `SourceOptions`. The options
of the output (`--output`, `--format`, `--quality`, `--no-clobber`, `--all-pages`) are not read here.

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
and encoded; then all the outputs are published together (`ImageWriter.PublishAll`, overwriting unless
`--no-clobber`), and a `finally` block deletes every temporary file that was not committed. The result is
written through `context.WriteLine`: as one text line per output when `context.Json` is false, or as one JSON
document. The diagnostics and the echo of a model are part of the JSON document only; the text result is the one
line, so a caller that needs them uses `--json`.

**TransformAndStage(loaded, target, settings, isDocument)**: when there are operations, calls `ImagePipeline.Apply` on
the loaded surface (the loaded surface stays untouched and is disposed with the loaded source; the transformed one is
disposed in a `finally` block after the staging) and stages the transformed surface with `ImageWriter.Stage`;
with none, stages the loaded surface. It returns the page's report and the `StagedOutput`. A border with no color
of its own gets the default: the `--background` color when given (also a transparent one), otherwise white for a
vector source (SVG, document page, model, chart, which the loader flattened over white) and
transparent for a raster. The `--background` color the user gave is also passed to `ImagePipeline.Apply` as the fill of
a rotation (a rotation without `--rotate-fill` fills its corners with it; without a background the pipeline's rule
applies: opaque white for an image with no transparent pixel, else transparent). Everything the pipeline does happens
before the output is published, so a failing operation
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
with `--strict`) leaves no new or partial output and every existing output untouched. The outputs are then published
all or nothing by `ImageWriter.PublishAll`: if a move fails (for example a target became no longer writable, or
appeared while `--no-clobber` is set), the targets published before it are removed, every replaced target is restored
from its backup, and the error is reported. Only when restoring a backup itself fails does a previous file remain
under its backup name, which the error then reports.

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
| `operations` | array of objects in order, for every page: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `render` (`page`, `dpi`, `width`, `height`, `background`) for a document page, `render` (`view`, `azimuth`, `elevation`, `projection`, `width`, `height`, `background`, and when given `defines` as a list of `name=value`, `time`, `maxTriangles`, `defaultColor`) for a model, `render` (`width`, `height`, `timeZone` (`UTC`), and `background` when one was given) for a Vega-Lite chart, `fit` (`width`, `height`) when a page was fitted inside a box, `auto-orient` (`orientation`), `resize` (`width`, `height`, and `filter` when `--filter` was given), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output; then the operations of the image pipeline in the order written: `crop` (`x`, `y`, `width`, `height`, `clamped`), `resize` (`width`, `height`, `filter`), `fit` (`width`, `height`, `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled`), `rotate` (`degrees`, `width`, `height`; for an angle that is not a multiple of 90 also `fill` and `filter`), `flip` (`axis`, `width`, `height`), `pad` (`left`, `top`, `right`, `bottom`, `color`, `width`, `height`), and the photo operations `grayscale` and `invert` (`width`, `height`), `threshold` (`auto`, `level`, `width`, `height`), `levels` (`black`, `white`, `gamma`, `width`, `height`), `brightness` and `contrast` (`amount`, `width`, `height`), `auto-levels` (`clip`, `black`, `white`, `applied`, `width`, `height`), `deskew` (`skewDegrees`, `rotateDegrees`, `confidence`, `applied`, `fill`, `filter`, `width`, `height`), `trim` (`tolerance`, `x`, `y`, `width`, `height`, `trimmed`, `empty`), and `grid` (`spacing`, `color`, `verticalLines`, `horizontalLines`, `labels`, `width`, `height`); a JPEG's `flatten` comes last; with `--all-pages` of a document every operation also has `page` |
| `outputs` | array with one object per page written: `path`, `format`, `width`, `height`, `bytes`, and for a document `page` |
| `diagnostics` | array of objects: `source` (`pptx`, `vsdx`, `scad`, or `vega-lite`), `severity` (`unsupported`, or `warning` for a model or a chart), `code` (the feature token, or `scad-warning` or `vega-lite-warning`), `message`, and `page` (documents only) |
| `echo` | array of strings: the `echo` output of a model as the SCAD library prints it, with its `ECHO:` prefix (empty for every other source) |
<!-- markdownlint-enable MD013 -->

#### Error Handling

Command-line mistakes (no input or more than one, a verb without its operand or with more than one, no output, an
unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format, `--page` with `--all-pages`, a page or a resolution that is not a
number in range, `--all-pages` with an output that is the same file for every page, a view or projection that does
not exist, a model value that is malformed or out of range, a definition that is not `name=value`, a model
option for an input that is not a model, a malformed or out-of-range operation value (an angle that is not a number, a
threshold, levels, brightness, contrast, clip, tolerance, or grid spacing out of range),
`--allow-upscale` without `--fit`, `--pad-color` without a pad, `--rotate-fill` without a rotation or `--deskew`,
`--auto-levels-clip` without `--auto-levels`, `--trim-tolerance` without `--trim`, and a crop that starts outside the
image or is
smaller than a pixel) are `ToolException` with
`usage-error`; a size, box, or border larger than the maximum, a border that makes the image larger than the
maximum, or a rotation whose canvas would be larger than the maximum, is `size-limit`. Input failures come from `SourceLoader`
(`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature` - also with
`--strict` for a skipped picture, an approximated construct, or an unsupported feature of a model or a chart -
`page-out-of-range`, `size-limit`, `scad-error` for a syntax error, a failed `assert`, or an `include` that cannot be
read, `vega-lite-error` for an invalid chart specification or a chart that cannot be rendered, and `budget-exceeded`
for a model that exceeds a limit such as `--max-triangles` or a chart that exceeds a limit of the library) and output failures
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
- **InspectCommand** - the option table (`OptionTable` without the output options), `ReadSourceOptions`, and
  `WriteField`.
