### InspectCommand

<!-- cspell:ignore canvasnet jpeg luma otsu grayscale deskew percentiles -->

![Commands Structure](CommandsView.svg)

#### Purpose

`InspectCommand` is the `inspect` command with its two subcommands: `inspect colors` reports the dominant colors and the
luma statistics of an image, `inspect pixel` reports the pixel at a point and, optionally, the pixels around it. Its
single responsibility is to turn a command line into one pass of probe, load, transform, analyze, and report; the
loading is `SourceLoader`, the transformation is `ImagePipeline`, and the measuring is the core `Surface` (`Analyze` and
`Sample`). The command is
**read-only**: it writes no file, so it has no output option, no format, no overwrite policy, and no same-file check.
The unit is the partial class `InspectCommand` in `InspectCommand.cs` (the run and the results) and
`InspectCommand.Options.cs` (the option table and the reading of the settings). The command has no alias.

#### Why one command with subcommands

`CommandRegistry` is flat: a command is found by its name or an alias, and `Context` hands everything after the name to
the
command. So `inspect` is registered once and takes the first positional argument as the subcommand (`colors` or `pixel`,
in any case). A missing or unknown subcommand is a `usage-error`; the hint of an unknown one suggests the closest name
with the edit distance of `CommandRegistry` (`Did you mean 'colors'?`, at a distance of at most 2) and lists the
subcommands. `inspect` is a top-level class in its own file, like `CompareCommand` and `MontageCommand`, and not a member
of `DiscoveryCommands`, which holds the two commands that never decode pixels.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of the command): every option of
`RenderCommand.OptionTable` except `--output`, `--format`, `--quality`, `--no-clobber`, and `--all-pages` (the same
declarations, so the grammar and the help text are those of `render`: the source options, the options of an OpenSCAD
model, and the image pipeline with the photo tools), then `--count <N>` (the description starts `colors:`) and
`--radius <R>` (the description starts `pixel:`). An output option is therefore an unknown option.

**Settings** (record): the subcommand, the input path, the `SourceOptions`, the `Operations` of the image pipeline, the
count (default `InspectCommand.DefaultColors`, 8), the radius (default 0), and the point (`null` for `colors`).

#### Key Methods

**ReadSettings(reader)**: reads the subcommand (the first positional argument), rejects `--radius` for `colors` and
`--count` for `pixel` (`The option --radius only applies to 'inspect pixel'.`), requires the operands that follow
(`colors`: one input file; `pixel`: an input file and a point, with an example in the hint), parses the point with
`ParsePoint` (`x,y`: two whole numbers separated by a comma; otherwise `Invalid value '...' for <x,y>`), reads `--count`
(1 to 32) and `--radius` (0 to 8), reads the source options with `RenderCommand.ReadSourceOptions`, and parses the
pipeline with `ImagePipeline.Parse(reader, null, null)` (so a malformed operation is reported before anything is
decoded). The point is not checked against the image here; that needs the pixels.

**Run(context, args)**: parses the arguments with the table, reads the settings, then reports every mistake about the
input before decoding: `SourceLoader.Probe`, the check that model options were given only for a model (the message of
`render`), and `SourceLoader.CheckPage`. Then loads the page (`SourceLoader.Load`, which honors `--page`, the size
options, the background, and the EXIF orientation), runs the pipeline when there are operations (with the default
border color of `render`: the `--background`, else white for a vector source, else transparent, and the `--background`
also as the fill of a rotation), runs the subcommand on the resulting surface, and disposes the pixels. The operations
reported are those of the loader followed by those of the pipeline.

**Analysis background**: translucent pixels are flattened over white before they are counted. The loader and the
pipeline have already composited the picture over `--background` (a raster source is flattened over it, a drawing or a
model is rendered onto it, a rotation or a border is filled with it), so a pixel is never composited twice. The reported
`background` is the color a fully transparent pixel counts as: the `--background` when it is opaque, white when there
is none, and a translucent `--background` flattened over white (`(c * a + 255 * (255 - a) + 127) / 255` per channel;
`--background transparent` is white).

**inspect colors**: `surface.Analyze(background, count)`. Text, two lines:

```text
colors of napkin.jpg (4032x3024): #F2EFE6 71.23%, #2B2A2A 9.10%, #8A7F6B 4.00%
luma: mean 201.31, min 3, max 255, p1 24, p50 232, p99 255, otsu 118
```

(the size is that of the image after the pipeline; a share is the fraction times 100 with two decimals, in the invariant
culture). JSON (one line, this key order): `schemaVersion`, `ok`, `command`, `subcommand` (`colors`), `tool`
(`name`, `version`), `input` (`path`, `format`, `natural` with `width` and `height`, and for a document `page` and
`pageCount`), `operations` (the loader's and the pipeline's, as in the result of `render`), `image` (`width`, `height`,
`pixels`), `background` (the color used), `colors` (an array of `hex`, `r`, `g`, `b`, `pixels`, `fraction`), `luma`
(`mean`, `min`, `max`, `p1`, `p50`, `p99`, `otsu`, `histogram` with 16 counts), `diagnostics`, and `echo`.

**inspect pixel**: a point outside the image is the `usage-error` of the command, naming the point and the image size
(checked before the core `Surface.Sample` would refuse it with an `ArgumentOutOfRangeException`), then
`surface.Sample(x, y, radius)`. Text, one line, and a second line with a radius:

```text
pixel 10,20 of napkin.jpg (4032x3024): #A1B2C3 rgb(161,178,195) alpha 255 luma 176
mean of 5x5 at 8,18: #A0B1C2
```

(the hex color has the alpha pair when the pixel is not opaque; the window is the clipped rectangle that was actually
read). JSON, in this order: `schemaVersion`, `ok`, `command`, `subcommand` (`pixel`), `tool`, `input`, `operations`,
`image`, `point` (`x`, `y`), `pixel` (`r`, `g`, `b`, `a`, `hex`, `luma`), `neighborhood` only with a radius above 0
(`x`, `y`, `width`, `height` of the clipped window, `mean` as a hex color, and `pixels` as an array of rows of hex
colors), `diagnostics`, and `echo`. The pixel is reported as stored (not flattened); its luma is the luma over white.

#### Error Handling

`ToolException` with `usage-error` (subcommand, operands, options of the other subcommand, an unknown option, a
malformed value, a point that is malformed or outside the image, a setting without its operation, a model option for
another kind of input), the input codes of `SourceLoader` (`input-not-found`, `input-unreadable`,
`unsupported-format`, `corrupt-input`, `unsupported-feature` with `--strict`, `page-out-of-range`, `size-limit`,
`scad-error`, `budget-exceeded`), and `size-limit` from the pipeline. No new error code and no new exit code. Null
arguments throw `ArgumentNullException`. The registry records the command name on every `ToolException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **RenderCommand** - the option table, `ReadSourceOptions`, `ScadOptionNames`, and `WriteField`.
- **SourceLoader** and **ImagePipeline** (Imaging) and the core **Surface** (`Analyze`, `Sample`) - loading,
  transforming, and measuring.
- **CommandRegistry** - the edit distance that suggests a subcommand.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `inspect`.
