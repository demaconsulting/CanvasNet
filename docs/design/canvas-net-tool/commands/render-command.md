### RenderCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad grayscale deskew -->

![Commands Structure](CommandsView.svg)

#### Purpose

`RenderCommand` is the `render` command and its alias `convert`, and the verb commands `crop`, `resize`, `rotate`,
`flip`, and `pad`: it loads an image, drawing, document page, 3D model view, Vega-Lite chart, or Mermaid diagram,
transforms it with the image
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
repeatable), `--time <T>`, `--default-color <color>`, and `--max-triangles <N>`, the option of a Vega-Lite chart
`--param <name=value>` (repeatable), the option of a Mermaid diagram `--theme <name>`, the options of the image
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
`SourceInfo` of the loaded page, its diagnostics, echo, and operations, the `WriteResult`, and the `SourceAnchor`
list of a
model already mapped through the pipeline.

**VerbTexts** (private table): for each verb, the operand placeholder (`<geometry>`, `<WxH>`, `<degrees>`, `<axis>`,
or `<border>`), the summary, a description of the operand, and the examples; `VerbUsage` builds the usage text from
it.

The command's name is `render`, its alias is `convert`, and its usage text has examples, says how the pipeline options
combine, and says how an OpenSCAD model is drawn (the views, a model that is entirely 2D drawn from the top unless a
view is chosen, the 800 by 800 default size, `-D`) and where its
`include` and `use` files are read from, and how a Vega-Lite chart is drawn (at its laid-out size over its own
background, with UTC dates and the bundled fonts; its local CSV, TSV, DSV (`format.delimiter`), and JSON data files are
read from the folder of the specification
and below it only, a missing one is an error, delimited text is not type-inferred unless `format.parse` is `"auto"`,
there is no data-folder option, and nothing is read from the network; its only option is `--param`, which sets the
parameters of the chart, and the usage text shows the option and the `param.` fragment of a link), and how a Mermaid
diagram is drawn (a flowchart, sequence, class, state, or entity relationship diagram, pie chart, gantt chart, or
mindmap at the size Mermaid lays it out at, with the bundled fonts, in the theme `--theme` sets
unless the text sets one; a diagram type that is not drawn (journey, timeline, and so on) is an empty picture with the
note
`mermaid-diagram-unsupported`, and `canvasnet help mermaid` says what is supported; an example draws
`flow.mmd --theme dark --scale 2`). A verb has no alias, and its
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
`WriteOptions`. Last, `ImagePipelineParser.Parse` reads the operations (the verb's first, then the options in
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
`--resize` and `--fit`); reads the model options with `ReadScadOptions`, the chart option with `ReadChartOptions`, and
the diagram option with `ReadDiagramOptions`. It
returns the `SourceOptions`. The options
of the output (`--output`, `--format`, `--quality`, `--no-clobber`, `--all-pages`) are not read here.

**ReadScadOptions(reader)**: when none of the model options was given, returns `null` (the loader then uses the
defaults: the view is chosen by the library - isometric, a flat top view for a model that is entirely 2D, or the
view the model sets - and the projection is orthographic). Otherwise reads `--view` (one of the names in
`SourceLoader.ScadViews`, in any case; another name is a `usage-error` whose hint lists the views; `ViewGiven` is set
whenever `--view` is present, even for `iso`, which is how `--view iso` asks for the isometric view of a model that
is entirely 2D), `--azimuth` with `ArgumentReader.ParseDegrees`
(normalized to 0 up to 360), `--elevation` as a number from -90 to 90 (read as a number, not as an angle to
normalize, so that -90 is the bottom view), `--projection` (`ortho` or `orthographic`, or `perspective`; another word
is a `usage-error`), every `--define` with `ArgumentReader.ParseDefines` (numbers, `true` and `false`, and text; a
later definition of a name wins), `--time` as a number from -1e9 to 1e9, `--max-triangles` as a whole number from 1
up to 2,147,483,647, and `--default-color` with `ParseColor`. The result is a `ScadViewOptions`. `ReadScadOptions` and
the table `ScadOptionNames` are `internal` so that `CompareCommand` and `MontageCommand` read the model options with the
same code; the view name is checked by the `internal` method `ParseScadView`, which they use for `--a-view` and
`--b-view` too. The option help of `--view` says that `iso` is the default and that a model that is entirely 2D is
drawn from the top (`top`) unless a view is chosen; the usage text of `render` says the same.

**ReadChartOptions(reader)**: when `--param` was not given, returns `null`; otherwise a `ChartOptions` whose
`Parameters` are
`ArgumentReader.ParseParams` of every `--param` (a later parameter of a name wins). The table `ChartOptionNames`
(`--param`) is
`internal` like `ScadOptionNames`. `Run` (and `inspect`) refuse the option for an input that is not a chart with a
`usage-error`
that names the kind ("only applies to a Vega-Lite chart", the mirror of the model-option rule), and `-D` on a chart
stays that
`usage-error`. A name the specification does not declare is not checked here: the loader reports it as `vega-lite-error`
with the
declared names, and the loader lays the chart out again with the parameters, so the natural size in the result
(`input.natural`) is
that of the parameterized chart. `compare` and `montage` have no chart options; `--param` is not in their tables.

**ReadDiagramOptions(reader)**: when `--theme` was not given, returns `null`; otherwise a `DiagramOptions` whose `Theme`
is the name of `MermaidRenderOptions.ThemeNames` that equals the value without regard to case (so `--theme Dark` is
reported as `dark`), and a value that is not a theme is a `usage-error` whose hint lists the themes (`default`, `base`,
`dark`, `forest`, `neutral`). The table `DiagramOptionNames` (`--theme`) is `internal` like `ChartOptionNames`. `Run`
(and `inspect`) refuse the option for an input that is not a diagram with a `usage-error` that names the kind ("only
applies to a Mermaid diagram"), before anything is drawn. The loader applies the theme: the theme fragment of the input
(`flow.mmd#theme=dark`) wins over the option, a theme the text of the diagram sets itself wins over both (as with the
Mermaid command line), and the `render` operation lists the theme. `compare` and `montage` have no diagram option;
`--theme` is not in their tables.

**SpecsFor(names)**: `internal`; returns the declarations of `OptionTable` with the given long names, in that order
(`ArgumentException` for a name the table does not declare), so that another command accepts an option with the same
grammar and the same help text. `CompareCommand` and `MontageCommand` build their option tables from it. `WriteField`,
which writes one parameter of an operation to a JSON object, is `internal` for `CompareCommand`.

**Run(context, args)**: parses the arguments, reads the settings, probes the input with `SourceLoader.Probe` (missing,
unreadable, unsupported, or corrupt header; for a model, a syntax error), checks that model options were given only for
a model (a model option with another kind of input is a `usage-error` naming the kind, rather than being ignored; the
message names the options of the tool, which is why the check is here and not in the library), and then calls
`ImageRenderer.Execute(info, request)` of the Viewer library with a `RenderRequest` built from the settings (the output
as the positional member, and `--all-pages`, the `SourceOptions`, the `WriteOptions`, and the operations read by
`ImagePipelineParser` set explicitly as `init` properties). The library
reports every mistake about the pages and the outputs before decoding anything, renders every page, and publishes all
the
outputs together or none (see _ImageRenderer Unit Design_). The command then writes the result through
`context.WriteLine`: as one text line per
output when `context.Json` is false, or as one JSON document. The diagnostics and the echo of a model are part of the
JSON document; without `--json` they are written
to standard error after the result lines by `context.WriteDiagnostics`, each note ending with the input path and, for a
document, the page, so a feature that was left out is not silent
(the exit code stays 0, and `--silent` suppresses them). A `ViewerException` of the library escapes the command and is
wrapped by the registry in a `ToolException` that names the command.

**ImagePipelineParser** (`ImagePipelineParser.cs`, an `internal static` class of this unit): reads the transformation
operations of the command line into the operation records of the Viewer's `ImagePipeline`. It names the options
(`CropOption`, `ResizeOption`, and so on) and lists the verb names (`VerbNames`).
**Parse(reader, verb, verbOperand)** reads the settings (`--filter`, `--allow-upscale`, `--pad-color`,
`--rotate-fill`, `--auto-levels-clip` from 0 to 20, `--trim-tolerance` from 0 to 255), then, for a verb command, the
operation of its operand first (`crop` with `ParseGeometry`, `resize` with `ParseSize`, `rotate` with the rotation
rule, `flip` with `ParseFlip`, `pad` with `ParsePad`), then every step option in `reader.Ordered` order, and returns
the list. Values are parsed with the `ArgumentReader` value grammars, so a malformed value is a `usage-error` and a size
or border above the maximum is `size-limit`; the verb operands are named `<geometry>`, `<size>`, `<degrees>`, `<axis>`,
and `<border>` in messages. A setting without the operation it modifies is a `usage-error` (`--allow-upscale` without
`--fit`, `--pad-color` without a pad, `--auto-levels-clip` without `--auto-levels`, `--trim-tolerance` without `--trim`,
and `--rotate-fill` without a rotation or `--deskew`). **ParseFilter(text, option)** turns a filter name into a
resampling mode with the library's `ImagePipeline.TryParseFilter`, and reports an unknown name as a `usage-error` that
lists the filters. The parser and the pipeline are tested together by the text-driven tests of `ImagePipelineTests.cs`
(see _ImagePipeline Unit Verification Design_).

**Transformation and staging of a page** (performed by `ImageRenderer`, called by `Run`): when there are operations, it
calls `ImagePipeline.Apply` on
the loaded surface (the loaded surface stays untouched and is disposed with the loaded source; the transformed one is
disposed in a `finally` block after the staging) and stages the transformed surface with `ImageWriter.Stage`;
with none, stages the loaded surface. It returns the page's report and the `StagedOutput`. A border with no color
of its own gets the default: the `--background` color when given (also a transparent one), otherwise white for a
vector source (SVG, document page, model, chart, which the loader flattened over white) and
transparent for a raster. The `--background` color the user gave is also passed to `ImagePipeline.Apply` as the fill of
a rotation (a rotation without `--rotate-fill` fills its corners with it; without a background the pipeline's rule
applies: opaque white for an image with no transparent pixel, else transparent). The anchors of a model
(`LoadedSource.Anchors`, positions in the loaded image) are mapped through the applied steps with
`ImagePipeline.MapAnchors` when there are operations, and used as loaded otherwise; the page's report carries the
mapped list. Everything the pipeline does happens
before the output is published, so a failing operation
(for example a crop that starts outside the image after an earlier crop) writes nothing and leaves an existing output
untouched.

**CollectOperations** (performed by `ImageRenderer`): the operations to report for a page, in this order: the loader's
(`auto-orient`, `resize`,
`flatten`, `render`, ...), the pipeline's, and the `flatten` of a JPEG when the writer flattened transparency; with
`--all-pages` of a document each operation without a `page` gets one.

**Interaction of the loader's sizing and the pipeline**: the size options of the source (`--scale`, `--width`,
`--height`, `--size`) are carried out by the loader, so they act before the first operation of the pipeline: a vector
source is rendered at the requested size (and never scaled again), a raster source is decoded, turned upright by its
EXIF orientation, and resized once (with `--filter`), and then the pipeline starts from that image. A crop given
with `--width 24` therefore crops the 24-pixel-wide image. The pipeline's own `--resize` and `--fit` apply to the
pixels at their point of the chain.

**PlanTargets** (performed by `ImageRenderer`): decides the output file of each page. `{page}` in the output is replaced
by the page number (for one
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
| `operations` | array of objects in order, for every page: `op` and the operation's parameters. `render` (`width`, `height`) for an SVG, `render` (`page`, `dpi`, `width`, `height`, `background`) for a document page, `render` (`view`, `azimuth`, `elevation`, `projection`, `width`, `height`, `background`, and when given `defines` as a list of `name=value`, `time`, `maxTriangles`, `defaultColor`) for a model (the `view`, `azimuth`, and `elevation` are those asked for when a view was chosen explicitly, otherwise those the library used: `iso`, 45, 35.264... for a 3D model, `top`, 0, 90 for a model that is entirely 2D, and `custom` with the angles for a view the model set with its viewport variables), `render` (`width`, `height`, `timeZone` (`UTC`), `background` when one was given, and `params` - a list of `name=value` in name order, the value as JSON - when `--param` was given) for a Vega-Lite chart, `render` (`width` and `height` as drawn, `background`, and `theme` when one is set) for a Mermaid diagram, followed by one `embed` (`href`, `format`, `width`, `height`) for each picture of a node that was drawn, `fit` (`width`, `height`) when a page was fitted inside a box, `auto-orient` (`orientation`), `resize` (`width`, `height`, and `filter` when `--filter` was given), `flatten` (`background`) when transparency was flattened by the loader or for JPEG output; then the operations of the image pipeline in the order written: `crop` (`x`, `y`, `width`, `height`, `clamped`), `resize` (`width`, `height`, `filter`), `fit` (`width`, `height`, `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled`), `rotate` (`degrees`, `width`, `height`; for an angle that is not a multiple of 90 also `fill` and `filter`), `flip` (`axis`, `width`, `height`), `pad` (`left`, `top`, `right`, `bottom`, `color`, `width`, `height`), and the photo operations `grayscale` and `invert` (`width`, `height`), `threshold` (`auto`, `level`, `width`, `height`), `levels` (`black`, `white`, `gamma`, `width`, `height`), `brightness` and `contrast` (`amount`, `width`, `height`), `auto-levels` (`clip`, `black`, `white`, `applied`, `width`, `height`), `deskew` (`skewDegrees`, `rotateDegrees`, `confidence`, `applied`, `fill`, `filter`, `width`, `height`), `trim` (`tolerance`, `x`, `y`, `width`, `height`, `trimmed`, `empty`), and `grid` (`spacing`, `color`, `verticalLines`, `horizontalLines`, `labels`, `width`, `height`); a JPEG's `flatten` comes last; with `--all-pages` of a document every operation also has `page` |
| `outputs` | array with one object per page written: `path`, `format`, `width`, `height`, `bytes`, and for a document `page` |
| `diagnostics` | array of objects: `source` (`pptx`, `vsdx`, `scad`, `vega-lite`, `mermaid`, or `svg`), `severity` (`unsupported`, or `warning` for a model, a chart, a diagram, or a fragment warning), `code` (the feature token such as `mermaid-diagram-unsupported`, `scad-warning`, `vega-lite-warning`, `mermaid-warning`, or an image-link code such as `svg-image-unresolved`, `image-fragment-invalid`, or `image-page-out-of-range`), `message`, `page` (documents only), and then, only when present, `detail`, `location` (`file`, `line`, `column`, `pointer`; unknown members omitted), and `origin` (list of files), written by `ResultWriter.WriteDiagnosticExtras` |
| `echo` | array of strings: the `echo` output of a model as the SCAD library prints it, with its `ECHO:` prefix (empty for every other source) |
| `anchors` | **only for a model** (no member for any other input), after `echo`: an array of objects in echo order, duplicates included, `name`, `point` (the world `[x, y, z]`), `pixel` (`[x, y]` in the written image, continuous with the origin at the top-left corner, rounded to three decimals, or `null` when the position is unknown: a point behind a perspective eye or after a deskew that corrected the image) and `inside` (the pixel lies in `0 <= x < width`, `0 <= y < height` of the written image; `false` for a `null` pixel); `[]` when the model declares none (`WriteAnchors`) |
<!-- markdownlint-enable MD013 -->

The text result does not change for a model with anchors: the one `wrote` line is on standard output and the `ECHO:`
lines of the declarations (and the `scad-anchor-invalid` warnings) are on standard error like any other echo and
warning.

**Input fragment.** `Run` probes the input with `SourceLoader.Probe(InputReference.Parse(settings.Input))`, so a `#`
fragment on the input path (`part.scad#view=top&def.width=20`, `report.pdf#page=3`, `flow.mmd#theme=dark`) is read and
checked before any pixel is decoded: the page, the view and variables of a model, the chart parameters, and the theme of
a diagram it names override `--page`, `--view`, `-D`, `--param`, and `--theme` for this input (the library applies the
selection over `settings.Source`), and a page it names makes the render a single page whatever `--all-pages` says
(`WriteJsonFields` then writes `page`). The text result names the input with its fragment (`SourceInfo.Specifier`), the
`input` object of the JSON result has the `fragment` member, and the warnings of the fragment are among the
`diagnostics` of the result (so the `input` is named with its fragment on standard error). A fragment that cannot be
used is `image-fragment-invalid`, a page that does not exist `page-out-of-range`, and an unknown or repeated parameter
name a warning, never an error (also with `--strict`); the check of the model, chart, and diagram options for the kind
of the input applies to the options of the command only, so `render "part.scad#def.x=1"` of a PNG is a warning and not a
usage error. A `#` is a fragment only when no file has that exact name (see `InputReference` in the _SourceLoader Unit
Design_). The verb commands inherit all of it, because they are this class.

#### Error Handling

Command-line mistakes (no input or more than one, a verb without its operand or with more than one, no output, an
unknown option, contradictory or malformed sizes,
a quality out of range, an unknown format, `--page` with `--all-pages`, a page or a resolution that is not a
number in range, `--all-pages` with an output that is the same file for every page, a view or projection that does
not exist, a model value that is malformed or out of range, a definition that is not `name=value`, a model
option for an input that is not a model, a `--theme` that is not a Mermaid theme or that is given for an input that is
not a diagram, a malformed or out-of-range operation value (an angle that is not a number, a
threshold, levels, brightness, contrast, clip, tolerance, or grid spacing out of range),
`--allow-upscale` without `--fit`, `--pad-color` without a pad, `--rotate-fill` without a rotation or `--deskew`,
`--auto-levels-clip` without `--auto-levels`, `--trim-tolerance` without `--trim`, and a crop that starts outside the
image or is
smaller than a pixel) are `ViewerException` (the library's, or the tool's own `ToolException`) with
`usage-error`; a size, box, or border larger than the maximum, a border that makes the image larger than the
maximum, or a rotation whose canvas would be larger than the maximum, is `size-limit`. Input failures come from
`SourceLoader`
(`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature` - also with
`--strict` for a skipped picture, an approximated construct, or an unsupported feature of a model, a chart, or a diagram
(including a diagram type that is not drawn) - `page-out-of-range`, `size-limit`, `scad-error` for a syntax error, a
failed `assert`, or an `include` that cannot be read, `vega-lite-error` for an invalid chart specification or a chart
that cannot be rendered, `mermaid-error` for diagram text that is not valid or a diagram that cannot be laid out, with
the hint of the library, and `budget-exceeded` for a model that exceeds a limit such as `--max-triangles` or a chart or
a diagram that exceeds a limit of the library) and output failures
from `ImageWriter` (`same-input-output`, `output-exists`, `output-write-failed`); none of them leaves an output file
for the page that failed. Null arguments throw `ArgumentNullException`. The registry records the command name on
every `ToolException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **ImageRenderer**, **SourceLoader**, **ImagePipeline**, and **ImageWriter** (Imaging, of the Viewer library) -
  everything that touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `render` and `convert`, and one instance for each
  verb (`crop`, `resize`, `rotate`, `flip`, and `pad`).
- **CompareCommand** and **MontageCommand** - the option declarations (`SpecsFor`), `ReadScadOptions`,
  `ScadOptionNames`, `ParseScadView`, and `WriteField`.
- **InspectCommand** - the option table (`OptionTable` without the output options), `ReadSourceOptions`, and
  `WriteField`.
