### ImageComparison

<!-- cspell:ignore canvasnet psnr rmse ssim luma -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageComparison` compares two images of the same size and creates pictures of how they differ. Its single
responsibility is the arithmetic of comparison: the metrics, the structural similarity, the per-cell grid, the check of
the measures against thresholds, and the difference, overlay, and side-by-side pictures. It works on `Surface` values
only; it has no knowledge of files, options, or commands, so the commands decide how images are loaded and sized. The
unit consists of the static partial class `ImageComparison` (files `ImageComparison.cs` for the measures and
`ImageComparison.Images.cs` for the pictures) and its supporting records and enumerations.

#### Data Model

**ComparisonMetrics** (record struct): `PixelCount`, `Identical`, `Mae`, `Rmse`, `PsnrDb` (`null` when identical),
`Ssim`, `DifferingPixels`, `DifferingPixelRatio`, and `MaxChannelDifference`.

**GridCell** (record): `Column` and `Row` (0-based), `X`, `Y`, `Width`, `Height`, and the cell's `Mae`, `Rmse`, and
`DifferingPixelRatio`.

**ComparisonResult** (record): `Width`, `Height`, `Tolerance`, `Metrics`, `Columns`, `Rows`, and `Cells` (row-major).
`MaxCellRmse` is the largest cell RMSE (0 for no cells); `WorstCells(count)` returns up to `count` cells with an RMSE
above zero, the largest first, ties in row-major order.

**DiffStyle** (enum): `Heat` and `Abs`. **ThresholdMetric** (enum): `Mae`, `Rmse`, `Psnr`, `Ssim`, `DiffRatio`,
`MaxDiff`, and `CellRmse`. **ComparisonThreshold** (record struct): a metric and a limit. **ThresholdCheck** (record
struct): the metric, whether its limit is a minimum, the limit, the measured value (unrounded; positive infinity for
the PSNR of identical images), and whether it passed.

The constants are `DefaultTolerance` (8), `DefaultGridColumns` and `DefaultGridRows` (4), `MaxGridSide` (64),
`DefaultGain` (4), `DefaultOverlayWeight` (0.5), and `SideBySideGap` (8).

#### Key Methods

**Compare(a, b, tolerance, gridColumns, gridRows)**: the comparison is defined on opaque RGB; alpha is ignored. With
`N = W * H` pixels, for each pixel and each channel `c` of R, G, and B the difference is the integer
`d = |a_c - b_c|`. One pass over the rows (with row spans) accumulates, per grid cell, the exact `ulong` sums of `d`
and of `d^2` and the count of differing pixels, and overall the largest `d`. The measures are then:

| Measure | Definition |
| --- | --- |
| `Mae` | `sum(d) / (3 N)` |
| `Rmse` | `sqrt(sum(d^2) / (3 N))` |
| `PsnrDb` | `20 * log10(255 / Rmse)`; `null` when `Rmse` is 0 |
| `MaxChannelDifference` | `max d` over all pixels and channels |
| `DifferingPixels` | pixels whose largest channel `d` is above the tolerance; the ratio is that count over `N` |
| `Identical` | `MaxChannelDifference == 0`, independent of the tolerance |

The only floating-point steps are single divisions, `sqrt`, and `log10` at the end, so the same inputs give the same
numbers on every run. The grid divides the width into `C` columns with boundaries `x_i = floor(i * W / C)` and the
height into `R` rows with `y_j = floor(j * H / R)` (integer division), so the cells partition the image exactly. A cell
reports the same measures over its own pixels, with `3 * cellPixels` in place of `3 N`; tables that map each x and y
to its column and row make the accumulation one pass. The grid sides are 1 to 64 and no larger than the image.

**Ssim(a, b)** (internal): the "SSIM-lite" of the luma. The luma of a pixel is the integer
`Y = (77 R + 150 G + 29 B + 128) >> 8` (BT.601 in fixed point). The windows are uniform (box) windows of
`min(8, W)` by `min(8, H)` pixels with origins every 4 pixels in x and in y (`0, 4, 8, ...` while the window fits) plus
one final window flush with the right and bottom edge when the stride does not land there, so every pixel is covered.
For a window of `n = w * h` pixels with the exact integer sums `sA = sum(Ya)`, `sB = sum(Yb)`, `sAA = sum(Ya^2)`,
`sBB = sum(Yb^2)`, and `sAB = sum(Ya * Yb)`:

```text
SSIM = ( (2 sA sB + C1 n^2) * (2 (n sAB - sA sB) + C2 n^2) )
     / ( (sA^2 + sB^2 + C1 n^2) * (n (sAA + sBB) - sA^2 - sB^2 + C2 n^2) )
C1 = (0.01 * 255)^2 = 6.5025,  C2 = (0.03 * 255)^2 = 58.5225
```

This is the standard `(2 ma mb + C1)(2 cov + C2) / ((ma^2 + mb^2 + C1)(va + vb + C2))` with population variance,
multiplied through by `n^2`, so the sums stay exact integers until the final divisions. The result is the arithmetic
mean of the window values in row-major window order (a value from -1 to 1; exactly 1 for identical images, because the
numerator and denominator are then computed from the same integers). Each pixel lies in at most four windows, so the
cost is about four reads per pixel, with no extra memory.

**Evaluate(result, thresholds)**: one `ThresholdCheck` per threshold, in order, from the unrounded values: an error
measure (`mae`, `rmse`, `diff-ratio`, `max-diff`, `cell-rmse` - the worst cell's RMSE) passes when it is at most the
limit; `psnr` (infinite for identical images) and `ssim` pass when at least the limit. **MetricName**,
**TryParseMetric** (case-insensitive), **IsMinimum**, and **Range** describe the metrics: the lower-case names `mae`,
`rmse`, `psnr`, `ssim`, `diff-ratio`, `max-diff`, and `cell-rmse`; the ranges 0 to 255 for `mae`, `rmse`, `max-diff`,
and `cell-rmse`, 0 to 1 for `diff-ratio`, 0 to 1000 for `psnr`, and -1 to 1 for `ssim`.

**CreateDiff(a, b, style, gain)**: an opaque surface of the size of the inputs. The gain is 0.1 to 255. A channel
difference `d` is amplified to `round(d * gain)` (half away from zero) clamped to 255, through a 256-entry table. With
`Heat` the pixel's value is `v = amplify(max_c d)` and the color is `R = min(255, 3v)`, `G = min(255, max(0, 3v - 255))`,
`B = min(255, max(0, 3v - 510))`: black, then red (v = 85), yellow (v = 170), white (v = 255); identical pixels are
pure black. With `Abs` each channel is its own amplified difference.

**CreateOverlay(a, b, weightOfB)**: the weight is 0 to 1; `bw = round(weight * 255)` (half away from zero) and each
channel is `(a * (255 - bw) + b * bw + 127) / 255` in integer arithmetic, so weight 0 is the first image and weight 1 the
second exactly. The result is opaque.

**CreateSideBySide(a, b, diff)**: `a`, `b`, and `diff` left to right, separated by gaps of 8 pixels filled `#808080`;
there are no labels, so the order is the contract. The sheet is `3 W + 16` wide and `H` high. When `3 W + 16` or `H` is
larger than `Surface.MaxDimension`, the three panels are first resampled with the `Area` filter to `floor(W s)` by
`floor(H s)`, where `s` is the smaller of `(8192 - 16) / (3 W)` and `8192 / H` (each side at least 1), so that the sheet
fits.

#### Error Handling

All failures are defects of the caller, since the commands validate user input first: a null argument throws
`ArgumentNullException`; images of different sizes throw `ArgumentException` naming both sizes; a tolerance outside 0
to 255, a grid side outside 1 to 64 or larger than the image, a gain outside 0.1 to 255, a weight outside 0 to 1, an
undefined style or metric, or a negative cell count throw `ArgumentOutOfRangeException`. Created pictures are disposed
if their creation fails; the input surfaces are never modified or disposed.

#### Dependencies

- **CanvasNet core library** - `Surface`, `Rgba32`, and `ResamplingMode` (the area filter of the side-by-side image).
- **.NET BCL** - `Math` and `System.Linq`.

#### Callers

- **CompareCommand** - compares the two loaded images, evaluates the thresholds, and creates the requested pictures.
