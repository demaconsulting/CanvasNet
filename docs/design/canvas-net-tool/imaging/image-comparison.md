### ImageComparison

<!-- cspell:ignore canvasnet psnr rmse ssim -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageComparison` is the command-line side of comparing two images. The comparison itself - the exact error metrics,
the windowed structural similarity, the per-cell grid, and the difference, overlay, and side-by-side pictures - is
computed by the core `Surface` class (`Compare`, `CreateDiff`, `CreateOverlay`, and `CreateSideBySide`; see
_SurfaceComparison Unit Design_, `canvas/surface-comparison.md`). What stays here is what only a command line needs: the
**thresholds** that turn a comparison result into a pass or a fail, the names under which the metrics are written on the
command line and in the results, the range of the limit of each, and the defaults of the comparison options. The unit has
no knowledge of files or commands: it works on the `ComparisonResult` of the core library. It is the static class
`ImageComparison` (`ImageComparison.cs`) and its supporting types.

#### Data Model

**ThresholdMetric** (enum): `Mae`, `Rmse`, `Psnr`, `Ssim`, `DiffRatio`, `MaxDiff`, and `CellRmse`.
**ComparisonThreshold** (record struct): a metric and a limit. **ThresholdCheck** (record struct): the metric, whether
its limit is a minimum, the limit, the measured value (unrounded; positive infinity for the PSNR of identical images),
and whether it passed.

The constants are the defaults of the options of the `compare` command: `DefaultTolerance` (8), `DefaultGridColumns`
and `DefaultGridRows` (4), `DefaultGain` (4), and `DefaultOverlayWeight` (0.5). The limits of the arguments (a grid
side of 1 to 64, a gain of 0.1 to 255, a weight of 0 to 1) are those of the core library, which refuses anything else.

#### Key Methods

**Evaluate(result, thresholds)**: one `ThresholdCheck` per threshold, in order, from the unrounded values: an error
measure (`mae`, `rmse`, `diff-ratio`, `max-diff`, `cell-rmse` - the worst cell's RMSE, `ComparisonResult.MaxCellRmse`)
passes when it is at most the limit; `psnr` (infinite for identical images) and `ssim` pass when at least the limit.

**MetricName**, **TryParseMetric** (case-insensitive), **IsMinimum**, and **Range** describe the metrics: the lower-case
names `mae`, `rmse`, `psnr`, `ssim`, `diff-ratio`, `max-diff`, and `cell-rmse`; the ranges 0 to 255 for `mae`, `rmse`,
`max-diff`, and `cell-rmse`, 0 to 1 for `diff-ratio`, 0 to 1000 for `psnr`, and -1 to 1 for `ssim`.

#### Error Handling

All failures are defects of the caller, since the command validates user input first: a null argument throws
`ArgumentNullException`; an undefined metric throws `ArgumentOutOfRangeException`.

#### Dependencies

- **CanvasNet core library** - `ComparisonResult` and its records, `Surface` (for the tests of the defaults), and
  `DiffStyle`.

#### Callers

- **CompareCommand** - checks the thresholds, reads the metric names and ranges, and takes the defaults of its options.
