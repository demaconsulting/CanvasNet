### SurfaceComparison Unit Design

<!-- cspell:ignore canvasnet psnr rmse ssim luma -->

The `SurfaceComparison` unit is the comparison of the public `Surface` class of the `Canvas` subsystem with another
surface of the same size. It is the partial class files `Surface.Comparison.cs` (the measures) and
`Surface.ComparisonImages.cs` (the pictures), together with the public types of `ComparisonResult.cs`
(`ComparisonMetrics`, `ComparisonCell`, `ComparisonResult`) and `DiffStyle.cs`, so the operations read as methods of the
surface (`reference.Compare(redraw, 8, 4, 4)`).

#### Purpose

A caller that redraws an image needs to know how close the result is to its reference and where it differs. The unit
gives exact, repeatable error and similarity measures, the error of each cell of a grid, and pictures of the difference.
It was originally part of the command-line tool (`ImageComparison`); it lives in the core library so that every CanvasNet
package, and any library user, can use it. What only a command line needs - the names of the metrics, the thresholds that
turn a result into pass or fail, and the defaults of the options - stays in the tool. The unit knows nothing of files,
options, or commands.

#### Data Model

The unit adds no state to `Surface`. Its public surface is:

- `ComparisonResult Compare(Surface other, int tolerance, int gridColumns, int gridRows)`.
- `Surface CreateDiff(Surface other, DiffStyle style, double gain)`.
- `Surface CreateOverlay(Surface other, double weightOfOther)`.
- `Surface CreateSideBySide(Surface other, Surface difference)` and the constant `SideBySideGap` (8).
- **ComparisonMetrics** (record struct): `PixelCount`, `Identical`, `Mae`, `Rmse`, `PsnrDb` (`null` when identical),
  `Ssim`, `DifferingPixels`, `DifferingPixelRatio`, and `MaxChannelDifference`.
- **ComparisonCell** (record): `Column` and `Row` (0-based), `X`, `Y`, `Width`, `Height`, and the cell's `Mae`, `Rmse`,
  and `DifferingPixelRatio`.
- **ComparisonResult** (record): `Width`, `Height`, `Tolerance`, `Metrics`, `Columns`, `Rows`, and `Cells` (row-major),
  the constant `MaxGridSide` (64), `MaxCellRmse` (the largest cell RMSE, 0 for no cells), and `WorstCells(count)`, which
  returns up to `count` cells with an RMSE above zero, the largest first, ties in row-major order.
- **DiffStyle** (enum): `Heat` and `Abs`.

`Ssim(a, b)` is internal (the structural similarity is reported in the metrics of `Compare`) and reached from the tests
through `InternalsVisibleTo`.

#### Conventions

The comparison is defined on opaque RGB; alpha is ignored. Every sum is accumulated exactly in integers; the only
floating-point steps are single divisions, `sqrt`, and `log10` at the end, so the same inputs give the same numbers on
every run. The difference and overlay pictures are opaque, and every picture is computed with integer arithmetic after
the gain and the overlay weight are rounded once. Neither surface is modified or disposed. A surface must not be changed
by another thread while it is compared.

#### Key Methods

**Compare(other, tolerance, gridColumns, gridRows)**: with `N = W * H` pixels, for each pixel and each channel `c` of R,
G, and B the difference is the integer `d = |a_c - b_c|`. One pass over the rows (with row spans) accumulates, per grid
cell, the exact `ulong` sums of `d` and of `d^2` and the count of differing pixels, and overall the largest `d`. The
measures are then:

| Measure | Definition |
| --- | --- |
| `Mae` | `sum(d) / (3 N)` |
| `Rmse` | `sqrt(sum(d^2) / (3 N))` |
| `PsnrDb` | `20 * log10(255 / Rmse)`; `null` when `Rmse` is 0 |
| `MaxChannelDifference` | `max d` over all pixels and channels |
| `DifferingPixels` | pixels whose largest channel `d` is above the tolerance; the ratio is that count over `N` |
| `Identical` | `MaxChannelDifference == 0`, independent of the tolerance |

The grid divides the width into `C` columns with boundaries `x_i = floor(i * W / C)` and the height into `R` rows with
`y_j = floor(j * H / R)` (integer division), so the cells partition the image exactly. A cell reports the same measures
over its own pixels, with `3 * cellPixels` in place of `3 N`; tables that map each x and y to its column and row make the
accumulation one pass. The grid sides are 1 to 64 and no larger than the image. The sums cannot overflow: the largest
surface is 8192 by 8192 pixels and `d^2 <= 65025`, so a sum is below `2^64` by a wide margin.

**Ssim(a, b)** (internal): the "SSIM-lite" of the luma. The luma of a pixel is the integer
`Y = (77 R + 150 G + 29 B + 128) >> 8` (BT.601 in fixed point, the same as `OpaqueLuma` of the adjustments, with alpha
ignored). The windows are uniform (box) windows of `min(8, W)` by `min(8, H)` pixels with origins every 4 pixels in x and
in y (`0, 4, 8, ...` while the window fits) plus one final window flush with the right and bottom edge when the stride
does not land there, so every pixel is covered. For a window of `n = w * h` pixels with the exact integer sums
`sA = sum(Ya)`, `sB = sum(Yb)`, `sAA = sum(Ya^2)`, `sBB = sum(Yb^2)`, and `sAB = sum(Ya * Yb)`:

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

**CreateDiff(other, style, gain)**: an opaque surface of the size of the inputs. The gain is 0.1 to 255. A channel
difference `d` is amplified to `round(d * gain)` (half away from zero) clamped to 255, through a 256-entry table. With
`Heat` the pixel's value is `v = amplify(max_c d)` and the color is `R = min(255, 3v)`, `G = min(255, max(0, 3v - 255))`,
`B = min(255, max(0, 3v - 510))`: black, then red (v = 85), yellow (v = 170), white (v = 255); identical pixels are
pure black. With `Abs` each channel is its own amplified difference.

**CreateOverlay(other, weightOfOther)**: the weight is 0 to 1; `bw = round(weight * 255)` (half away from zero) and each
color channel is `(a * (255 - bw) + b * bw + 127) / 255` in integer arithmetic, so weight 0 gives the colors of this
surface and weight 1 the colors of the other exactly. The result is opaque (alpha 255).

**CreateSideBySide(other, difference)**: this surface, `other`, and `difference` left to right, separated by gaps of 8
pixels filled `#808080`; there are no labels, so the order is the contract. The panels are copied as they are
(`CopyFrom`), alpha included, so a translucent input stays translucent. The sheet is
`3 W + 16` wide and `H` high. When `3 W + 16` or `H` is larger than `MaxDimension`, the three panels are first resampled
with the `Area` filter to `floor(W s)` by `floor(H s)`, where `s` is the smaller of `(8192 - 16) / (3 W)` and
`8192 / H` (each side at least 1), so that the sheet fits.

#### Error Handling

A null surface throws `ArgumentNullException`; surfaces of different sizes throw `ArgumentException` naming both sizes;
a tolerance outside 0 to 255, a grid side outside 1 to 64 or larger than the image, a gain outside 0.1 to 255 (or not a
number), a weight outside 0 to 1 (or not a number), an undefined style, or a negative cell count throw
`ArgumentOutOfRangeException`; a disposed surface, as either operand, throws `ObjectDisposedException`. Every argument is
validated before anything is allocated. A picture that was allocated is disposed if its creation fails, and the
intermediate panel of a scaled side-by-side image is disposed on every path.

#### Dependencies

- **Surface** (`GetRowSpan`, `Clear`, `CopyFrom`, `Resize`, `OpaqueLuma`) and **Rgba32** (the same subsystem), and
  **ResamplingMode** (the area filter of the side-by-side image).
- The .NET base class library (`Math`, `System.Linq` in `ComparisonResult`).

#### Callers

- **CompareCommand** (command-line tool) - compares the two loaded images and creates the requested pictures; the tool's
  `ImageComparison` unit checks the thresholds.
- Consumers of the CanvasNet package - the comparison is public API.
