### ImageComparer

<!-- cspell:ignore canvasnet psnr rmse ssim -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageComparer` is the compare operation of the library: it brings two inputs to one size, measures how much they differ,
checks the thresholds, and writes the pictures of the differences that were asked for. Its single responsibility is the
order of that chain; the measures and the pictures are computed by the core `Surface`, the thresholds by
`ImageComparison`, the loading by `SourceLoader`, and the writing by `ImageWriter`. It is the library form of the
`compare` command of the `canvasnet` tool. The unit is the static class `ImageComparer` (`ImageComparer.cs`) with the
request and result records.

#### Data Model

**CompareInput** (record): the required `Label` (`a` or `b`) and `Info`; the optional `Page` (1) and `Source` (the
`SourceOptions` without a target size; a default `SourceOptions`).

**ResizeTarget** (record): `Side` (`a` or `b`, lower case, to load the other input at the size of that input, in which
case `Width` and `Height` are 0 and ignored; anything else, including `null`, is an explicit size), `Width`, and
`Height`; the static `ToInputA`, `ToInputB`, and `To(width, height)` build the three shapes. The library does not
validate `Side` or a 0 x 0 explicit target; the tool does.

**CompareRequest** (record): the required two inputs `A` and `B` (positional) and the optional members as `init`
properties with the defaults of the `compare` command: `Resize` (`ResizeTarget?`; `null` compares at the inputs' own
sizes), `Fit` (contain), `Filter` (auto), `Background` (opaque white), `Tolerance`, `GridColumns`, `GridRows`,
`Thresholds` (none), the optional output paths `DiffPath`, `SideBySidePath`, and `OverlayPath` (none), `DiffStyle`
(heat), `Gain`, `OverlayWeight`, and `Write` (`WriteOptions.Default`). The numeric defaults are the constants of
`ImageComparison`. The same rule applies to every request and option record of the subsystem: the positional members
are exactly the required data and everything optional is an `init` property with the default of the command, so a
request built from its required members alone does what the command does without options. The tool passes every value
explicitly, so its behavior does not depend on these defaults.

**CompareSide** (record): `Label`, `Info`, `Page`, `LoadedWidth` and `LoadedHeight` (before the fit), `Width` and
`Height` (compared), `Operations`, `Diagnostics`, and `Echo`. **ComparedPicture** (record): `Kind` (`diff`,
`sideBySide`, or `overlay`) and the `WriteResult`. **CompareResult** (record): `A`, `B`, `Comparison` (the core
`ComparisonResult`: the metrics, the grid cells, the worst cells, and the tolerance), `Checks` (one `ThresholdCheck`
per threshold), and `Pictures`; the property `Metrics` is the shortcut for `Comparison.Metrics` (the numbers: MAE, RMSE,
and the maximum difference on the 0 to 255 scale, the differing-pixel ratio as a fraction, PSNR in decibels, and SSIM).

#### Key Methods

**Execute(request)**: first refuses a request with a missing member (`ArgumentNullException`: the inputs with their label,
facts, and options, the thresholds, and the write options). It then checks what needs no pixels: the pictures' formats
(`ImageWriter.ResolveFormat`, with the format of the write options when set, else the extension of each output) and paths
(`CheckTargets` against both inputs, and two pictures may not be the same file). It then loads both inputs, a vector
input at the size of the target and a raster input at its own size fitted into it with `Surface.FitInto` (flattened
over the background); inputs of different sizes without a resize are `size-mismatch`, and a grid with more cells than
pixels in a direction is a `usage-error`. It then measures with `Surface.Compare` (tolerance and grid of the request),
checks the thresholds with `ImageComparison.Evaluate`, draws the pictures with `Surface.CreateDiff`,
`CreateOverlay`, and `CreateSideBySide`, stages them, and publishes them all together with `ImageWriter.PublishAll`.
A threshold that is not met is data (`ThresholdCheck.Passed` is `false`), not an exception: the pictures are written, and
the caller decides what the miss means (the tool fails with the code `threshold-failed` and still prints the result).

**Input fragment.** The page of the `SourceInfo.Selection` of an input replaces the `Page` of its `CompareInput`
(the `Side` takes it), and `SourceLoader.OpenPages` applies the rest of the selection to the `Source` options of the
input, so two inputs of one file, each with its own fragment, are two versions of it.

#### Error Handling

`ViewerException` with `usage-error`, `size-mismatch`, `same-input-output`, `output-exists`, `output-write-failed`, and
the
codes of `SourceLoader`. A null request, or a request with a missing member, is a defect (`ArgumentNullException`).
Any other exception propagates; staged
files are removed on every path, so a failure leaves no picture behind.

#### Dependencies

- **SourceLoader** - the loading of each input.
- **ImageComparison** - the thresholds and the defaults.
- **ImageWriter** - the format, the checks, the staging, and the publishing of the pictures.
- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface.Compare` and the picture methods, `ComparisonResult`, `DiffStyle`, `FitMode`,
  and `ResamplingMode`.

#### Callers

- **CompareCommand** (of the tool) - for `compare`.
- **Library users** - compare a rendering with its reference without a command line.
