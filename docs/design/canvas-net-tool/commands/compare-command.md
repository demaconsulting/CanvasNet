### CompareCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad psnr rmse ssim -->

![Commands Structure](CommandsView.svg)

#### Purpose

`CompareCommand` is the `compare` command: it loads two inputs, brings them to one size, measures how much they differ,
optionally writes pictures of the differences, checks the measures against thresholds, and reports the result. Its
single responsibility is to turn a command line into one comparison and to report the result; the work (loading, sizing,
measuring, the thresholds, and the pictures) is the `ImageComparer` of the Viewer library, which calls the core `Surface`
(`Compare`, `CreateDiff`, `CreateOverlay`, `CreateSideBySide`), `ImageComparison`, and `SourceLoader`. The unit is the
partial class `CompareCommand`
in `CompareCommand.cs` (the run, the preparation of the sides, and the reporting) and
`CompareCommand.Options.cs` (the option table and the reading of the settings). The command has no alias.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of the command): the declarations of
`RenderCommand.OptionTable` for `--page <N>`, `--dpi <D>`, `--no-auto-orient`, `--strict`, the options of an OpenSCAD
model (`--view`, `--azimuth`, `--elevation`, `--projection`, `--define`/`-D`, `--time`, `--default-color`,
`--max-triangles`), `--filter`, `--quality`, and `--no-clobber` (taken with `RenderCommand.SpecsFor`, so the grammar and
the help text are the render command's), and the command's own: `--a-page`, `--b-page`, `--a-dpi`, `--b-dpi`
(overriding `--page` and `--dpi` for one input), `--a-view`, `--b-view`, `--a-define`, `--b-define` (overriding `--view`
and `-D` for one model), `--background <color>` (opaque; its own declaration, because the render command's accepts
transparency), `--resize-to <a|b|WxH>`, `--fit <mode>`, `--tolerance <0-255>`, `--grid <CxR>`,
`--threshold <metric=value>` (repeatable), `--diff <file>`, `--diff-style <style>`, `--gain <factor>`,
`--side-by-side <file>`, `--overlay <file>`, and `--overlay-weight <0-1>`.

**Settings** (record): the two `SideSettings` (the label `a` or `b`, the path, and the page, resolution, view, and
variables chosen for that input alone), the shared page and resolution, the shared `ScadViewOptions`, the opaque
background, strict, the `FileAccessPolicy` of both inputs, auto-orient, the filter, the `ResizeTarget` (`a`, `b`, or an
explicit size) or `null`, the
`FitMode`, the tolerance, the grid, the thresholds in the order written, the three picture paths, the `DiffStyle`, the
gain, the overlay weight, and the `WriteOptions` (the background is also the color a JPEG picture is flattened over).
The `ScadViewOptions` of one input is the shared options with that input's own view and variables; a view chosen
for one input alone (`--a-view`, `--b-view`) counts as an explicit view for that input (`ViewGiven` is set), while
an input with no view chosen leaves the choice to the library (isometric, or a flat top view for a model that is
entirely 2D).

**Side** and **PictureOutput** (the `CompareSide` and `ComparedPicture` records of the Viewer library): one compared
input - its label, `SourceInfo`, page, the size that is compared, the size as loaded, the operations applied, the
diagnostics, and the echo - and a picture that was written (`diff`, `sideBySide`, or `overlay`) with its `WriteResult`.
They are plain data, so the failure document of a threshold can be written after the library call has returned.

#### Key Methods

**ReadSettings(reader)**: requires exactly two positional arguments; reads `--background` with `ParseColor` and rejects
a color that is not opaque (`usage-error`; the default is white); reads `--grid` as `CxR` with `ParseInt` for each side
(1 to 64, `ComparisonResult.MaxGridSide`; default 4x4, `ImageComparison.DefaultGridColumns` and `DefaultGridRows`); reads
every `--threshold` as `metric=value` - the metric by `ImageComparison.TryParseMetric`, the
value by `ParseNumber` within `ImageComparison.Range` - and rejects a metric given twice; reads `--tolerance` (0 to
255, default 8), `--gain` (0.1 to 255, default 4), `--overlay-weight` (0 to 1, default 0.5), `--diff-style` (`heat` or
`abs`), `--fit` (`contain` or `stretch`, with `ParseFit`, also used by `MontageCommand`), `--resize-to` (`a`, `b`, or a
size with `ParseSize`, which refuses a side beyond the maximum with `size-limit`), `--quality`, the per-side options,
and, with `RenderCommand.ReadScadOptions`, the shared options of a model, and, with `RenderCommand.ReadFileAccess`
of both inputs, the file-access policy (under `roots` without `--allow-root`, the default folders cover the folders of
both inputs).

**Run(context, args)**: parses and reads the settings, probes both inputs with `SourceLoader.Probe` under the
file-access policy, checks that shared
model options were given only when an input is a model, and runs `PrepareSide` for each input (a per-side option
given for an input of the wrong kind is a `usage-error` naming the kind, in terms of the options of the tool; shared
options are skipped for an input they do not concern; `SourceLoader.CheckPage`, whose `page-out-of-range` is
re-thrown with `Input a:` or `Input b:` in front, and the `SourceOptions` of the side, which carry the policy). It then
calls
`ImageComparer.Execute(request)` of the Viewer library with a `CompareRequest` built from the settings and the two
`CompareInput` values (the inputs are the positional members; every other value is set explicitly as an `init`
property, so the result does not depend on the defaults of the record), and reports the result (the metrics are
`result.Comparison.Metrics`, the full measurement is `result.Comparison`). The library reports every mistake about the
pictures before decoding
anything, loads and sizes the sides, compares, evaluates the thresholds, and writes the pictures (see _ImageComparer Unit
Design_), so the behavior described below for loading and writing is the library's, called with the values read here. A
threshold that is not met is data in the library's result: the command writes the result and then throws
`ToolException(threshold-failed)`.

**Loading and sizing of the sides** (performed by `ImageComparer`): without `--resize-to` both inputs are loaded at
their natural size, and a difference in size
is `size-mismatch` with the hint to use `--resize-to`. With `--resize-to a` the first input is loaded at
its natural size and the second is loaded into that size; with `b` the roles are exchanged; with `WxH` both are loaded
into that size. Loading into a box is `SourceLoader.OpenPages` with `SourceLoader.BoxOptions` (a vector input - SVG, a
document page, a model - is rendered at the box, so it is sharp and never resampled), then, when the pixels are not
the box size, the core `Surface.FitInto` with `--fit` and the background (a raster input is enlarged or shrunk with
`contain`, keeping its aspect ratio and centered over the background, or resampled to the box with `stretch`); the
operation is reported as `fit` (with `mode`, `width`, `height`, and `background`) or `resize`. Every input is flattened
over the background by the loader, so both are opaque. The side owns the pixels of the loaded source from then on.

**Writing of the pictures** (performed by `ImageComparer`): each requested picture is created (`Surface.CreateDiff`,
`CreateOverlay`, or
`CreateSideBySide`), staged with `ImageWriter.Stage` (the first input
is the guard path; `CheckTargets` has already checked both), and disposed before the next is created, so at most the
two inputs and one picture are in memory; the order is `diff`, `sideBySide`, `overlay`. The side-by-side picture
always uses the heat map as its third panel. When every picture is staged, all are published together with
`ImageWriter.PublishAll` (all or nothing, with rollback); a `finally` block deletes the temporary file of every
picture that was not published, so a failure leaves no new or partial output and every existing output untouched.

**Threshold failure**: the result is written first (the pictures, and the text lines or nothing in JSON mode), then,
when a check of the library's result failed, the command throws `ToolException(threshold-failed)` whose message lists
each failed check (for
example `rmse 23.91 is above the limit 10; ssim 0.8123 is below the limit 0.9`) and whose `ResultFields` write the
command's JSON fields. `Program` reports it like any expected error: the message and hint on standard error, exit code
1, and with `--json` the failure document, in which `ResultWriter.Failure` writes the result fields after the `error`
object. With `--json` and no failed check the success document is written as usual.

**Text result**: with fixed formats and the invariant culture - `compared {a} with {b} ({W}x{H}): MAE {0.00}, RMSE
{0.00}, PSNR {0.00} dB, SSIM {0.0000}, {0.00}% of pixels differ (tolerance {t})`, or `compared {a} with {b}
({W}x{H}): identical` (and no grid) when no channel of any pixel differs; a document is named `{path} page {n}`; then
`RMSE per cell ({C}x{R}):` and one line per grid row with each cell's RMSE as `0.0` right-aligned in six characters;
then `wrote {path} ({w}x{h} {format}, {bytes} bytes) [diff|side-by-side|overlay]` for each picture; then, for each
threshold, `threshold {metric} {<=|>=} {limit}: {passed|failed} ({value})` (the value of an infinite PSNR is `inf`).

**JSON result**: `ResultWriter.Success(Name, ...)` (or the failure document of a threshold) with these fields after
the envelope, in this order: `tool` (`name`, `version`); `a` and `b` (`path`, `format`, for a document `page` and
`pageCount`, `loadedWidth`, `loadedHeight`, `width`, `height`, and `operations`, the loader's followed by the `fit` or
`resize` of the command); `size` (`width`, `height`); `resize` (`to`, `fit`) only when `--resize-to` was given;
`options` (`tolerance`, `background`, `gridColumns`, `gridRows`); `metrics` (`identical`, `mae`, `rmse`,
`rmseNormalized`, `psnrDb` - `null` when identical -, `ssim`, `maxChannelDiff`, `diffPixels`, `diffPixelRatio`); `grid`
(`columns`, `rows`, and `cells` of `column`, `row`, `x`, `y`, `width`, `height`, `mae`, `rmse`, `diffPixelRatio`);
`worstCells` (up to three, `column`, `row`, `rmse`, the largest first, only cells with an error); `thresholds`
(`passed`, and `checks` of `metric`, `comparison` as `<=` or `>=`, `limit`, `value` - `null` for infinity -, and
`passed`); `outputs` (`kind`, `path`, `format`, `width`, `height`, `bytes`); `diagnostics` (the loader's notes with the
`side`); and `echo`. Without `--json` the same notes and the echo are written to standard error by
`context.WriteDiagnostics` after the text result (each note ending with `(input a)` or `(input b)`), also when a
threshold fails. MAE, RMSE, and PSNR are rounded to four decimals; the normalized RMSE, SSIM, the ratios, and the
values of the checks to six.

**Input fragments.** Both inputs are probed with `SourceLoader.Probe(InputReference.Parse(path))`; the page of a
fragment replaces `--a-page`, `--b-page` and `--page` for that input (`PrepareSide`), and the rest of the selection is
applied by the library over the merged options (so the fragment wins over `--a-view` and `--a-define` as well).
`Describe` names an input with its fragment, and `WriteSide` writes the `fragment` member.

#### Error Handling

Command-line mistakes (not two inputs, an unknown option, a malformed or out-of-range value, a background that is not
opaque, a threshold that is malformed, out of range, or repeated, a grid larger than the image, a per-side option or a
model option given for an input that is not of the kind it concerns, and the same file named for two pictures) are
`ToolException` with `usage-error`; a `--resize-to` size beyond the maximum is `size-limit`; inputs of different size
without `--resize-to` are `size-mismatch`; a picture that would overwrite an input is `same-input-output`, an existing
picture with `--no-clobber` is `output-exists`, and a path that cannot be written is `output-write-failed`; input
failures come
from `SourceLoader` (`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`,
`unsupported-feature`, `page-out-of-range`, `size-limit`, `scad-error`, `file-access-denied`, and `budget-exceeded`);
the mistakes of `--file-access` and `--allow-root` are `usage-error` as for `render`; a threshold that is not
met is `threshold-failed` after the result was written. Every one is an expected error with exit code 1. An exception
that is not a `ToolException` (for example one thrown by `Surface.Compare` for an argument the command validated) is a
defect and propagates. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** and **ToolException** - the JSON document and the failure that carries it.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **RenderCommand** - the shared option declarations, the reading of the model options and of the file-access
  options (`ReadFileAccess`), and `WriteField`.
- **ImageComparer**, **SourceLoader**, **ImageComparison**, and **ImageWriter** (Imaging, of the Viewer library), and
  the core `Surface` (`FitInto`, `Compare`,
  `CreateDiff`, `CreateOverlay`, `CreateSideBySide`) - everything that
  touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `compare`.
- **MontageCommand** - uses `CompareCommand.ParseFit` to read `--fit`.
