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

**CompareInput** (record): `Label` (`a` or `b`), `Info`, `Page`, and `Source` (the `SourceOptions` without a target size).

**ResizeTarget** (record): `Side` (`a` or `b`, or `null` for an explicit size), `Width`, and `Height`.

**CompareRequest** (record): the two inputs `A` and `B`, `Resize` (`ResizeTarget?`; `null` compares at the inputs' own
sizes), `Fit`, `Filter`, `Background`, `Tolerance`, `GridColumns`, `GridRows`, `Thresholds`, the optional output paths
`DiffPath`, `SideBySidePath`, and `OverlayPath`, `DiffStyle`, `Gain`, `OverlayWeight`, and `Write`.

**CompareSide** (record): `Label`, `Info`, `Page`, `LoadedWidth` and `LoadedHeight` (before the fit), `Width` and
`Height` (compared), `Operations`, `Diagnostics`, and `Echo`. **ComparedPicture** (record): `Kind` (`diff`,
`sideBySide`, or `overlay`) and the `WriteResult`. **CompareResult** (record): `A`, `B`, `Metrics` (the core
`ComparisonResult`), `Checks` (one `ThresholdCheck` per threshold), and `Pictures`.

#### Key Methods

**Execute(request)**: first checks what needs no pixels: the pictures' formats and paths (`ImageWriter.ResolveFormat`
and `CheckTargets` against both inputs, and two pictures may not be the same file). It then loads both inputs, a vector
input at the size of the target and a raster input at its own size fitted into it with `Surface.FitInto` (flattened
over the background); inputs of different sizes without a resize are `size-mismatch`, and a grid with more cells than
pixels in a direction is a `usage-error`. It then measures with `Surface.Compare` (tolerance and grid of the request),
checks the thresholds with `ImageComparison.Evaluate`, draws the pictures with `Surface.CreateDiff`,
`CreateOverlay`, and `CreateSideBySide`, stages them, and publishes them all together with `ImageWriter.PublishAll`.
A threshold that is not met is data (`ThresholdCheck.Passed` is `false`), not an exception: the pictures are written, and
the caller decides what the miss means (the tool fails with the code `threshold-failed` and still prints the result).

#### Error Handling

`ViewerException` with `usage-error`, `size-mismatch`, `same-input-output`, `output-exists`, `output-write-failed`, and
the
codes of `SourceLoader`. A null request is a defect (`ArgumentNullException`). Any other exception propagates; staged
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
