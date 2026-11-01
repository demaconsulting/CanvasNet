### SurfaceAdjustments Unit Design

<!-- cspell:ignore luma otsu grayscale gamma percentile percentiles midtones -->

The `SurfaceAdjustments` unit is the tone and color adjustments of the public `Surface` class of the `Canvas`
subsystem. It is a partial class file, `Surface.Adjustments.cs`, so the adjustments read as instance methods
(`surface.Grayscale()`) while the pixel storage stays in `Surface.cs`.

#### Purpose

`SurfaceAdjustments` changes the tone and color of the pixels of a surface: the luma of a pixel, grayscale, invert, fixed
and automatic (Otsu) thresholds, levels, brightness, and contrast, and the auto-levels stretch. Its single
responsibility is the arithmetic of those adjustments; it knows nothing of files, command-line options, or how a
surface was loaded, so a caller decides which adjustment runs where. The adjustments were originally the
command-line tool's `ImageAdjustments` unit; they live in the core library so that every CanvasNet package, and any
library user, can use them, and the tool's image pipeline is a thin adapter over them.

#### Data Model

The unit adds no state to `Surface`. Its public surface is:

- `static int Luma(Rgba32 pixel)`.
- In-place instance methods `Grayscale()`, `Invert()`, `Threshold(int level)`,
  `Levels(int black, int white, double gamma = 1)`, `Brightness(int amount)`, and `Contrast(double contrast)`, all
  returning `void`.
- `long[] LumaHistogram()` (a new array; the surface is not modified), `static int OtsuThreshold(ReadOnlySpan<long> histogram)`,
  and `(int Black, int White)? AutoLevels(double clipPercent = 1)` (in place; returns the points applied).
- The public constants `MinGamma` (0.1), `MaxGamma` (10), `DefaultAutoLevelsClipPercent` (1), and
  `MaxAutoLevelsClipPercent` (20), which callers use to validate user input against the same limits.

Internal, and used by the public methods and by the tests: the table builders `BuildLevelsTable`,
`BuildBrightnessTable`, and `BuildContrastTable`, the table application `ApplyTable`, and `AutoLevelsRange`.

#### Conventions

Surfaces hold **straight** (non-premultiplied) `Rgba32` pixels. Every adjustment that changes pixels works **in place**,
like `Clear` and `PremultiplyAlpha`, so no adjustment allocates a second full-size surface; a caller that must keep the
original calls `Clone()` first. Wherever a number decides a pixel or a score the arithmetic is exact integer
arithmetic; floating point (always `double`, no fused multiply-add) is used only to build the three 256-entry tables
once per call and for the last division of the Otsu score. Rows are processed with `Parallel.For` and write only their
own row; the only reduction, the histogram, is summed in blocks of 64 rows (`HistogramBlockRows`), each block into its
own array, and the block arrays are added in block order. The sums are exact integers, so the result does not depend on
the number of cores or on scheduling. Nothing allocates per pixel.

#### Key Methods

**Luma(pixel)**: `Y = (77 R + 150 G + 29 B + 128) >> 8` (BT.601 in fixed point, 0 to 255). For a pixel with alpha `A`
below 255 the luma is that of the pixel flattened over white, `Y' = (Y * A + 255 * (255 - A) + 127) / 255` with integer
division; a fully transparent pixel has the luma 255. An opaque pixel gives `Y`.

**Grayscale()**: `R = G = B =` the luma `Y` of the pixel's own color (not flattened); alpha is unchanged and the surface
stays RGBA, so a gray pixel is unchanged because `(256 v + 128) >> 8 = v`.

**Invert()**: `R, G, B <- 255 - c`; alpha is unchanged.

**Threshold(level)**: a pixel becomes opaque white `(255, 255, 255, 255)` when `Luma > level` (over white) and opaque
black otherwise; level 255 gives an all-black result and level 0 makes only luma 0 black.

**LumaHistogram()**: the 256 counts of `Luma`, summed in blocks of 64 rows as described above.

**OtsuThreshold(histogram)**: with `N = sum h[v]` and `S = sum v * h[v]` (all `long`), for each candidate `t` from 0 to
254: `w0 = sum h[v]` for `v <= t`, `S0 = sum v * h[v]` for `v <= t`, `w1 = N - w0`, `S1 = S - S0`; a candidate with
`w0 = 0` or `w1 = 0` is skipped, and

```text
score(t) = ((double)(w1 * S0 - w0 * S1))^2 / ((double)w0 * w1)
```

(the between-class variance up to a constant factor; for the histogram of any image up to `MaxDimension` squared pixels
`w1 * S0` is at most about 6.7e7 * 1.7e10, which fits a `long`; the sums and products are computed in a `checked`
context, so a caller-supplied histogram with counts too large for a `long` throws `ArgumentException` instead of
wrapping into a wrong threshold;
the final division is the only inexact step and is deterministic IEEE arithmetic). The candidate with the largest score
wins. **Ties**: a gap of empty levels between two modes gives candidates with the same `(w0, S0)` and so bit-identical
scores, and the result is the middle of the plateau, `T = (tFirst + tLast) / 2` with integer division, where `tFirst`
and `tLast` are the first and last candidate with the maximal score. When no candidate is valid (all pixels share one
level, or there are none) the result is `127`. Example (pinned by a test): the histogram `{20: 100, 200: 100}` has the
valid candidates 20 to 199, all tied, so `T = (20 + 199) / 2 = 109`; `{30: 900, 40: 100, 220: 50}` splits between 40 and
220 and gives `T = (40 + 219) / 2 = 129`. The result is applied as a fixed threshold.

**Levels(black, white, gamma)**: the black level is 0 to 254, the white level 1 to 255 and above the black level, the
gamma 0.1 to 10 (otherwise `ArgumentOutOfRangeException`, or `ArgumentException` when black is not below white). It
applies `BuildLevelsTable`: with `t = clamp((v - black) / (white - black), 0, 1)` the entry for level `v` is
`Round(255 * t^(1/gamma))` (half away from zero); `Math.Pow` is not called when the gamma is exactly 1, which is
`Round(255 * t)`. A gamma above 1 brightens the midtones (the ImageMagick convention).

**Brightness(amount)**: applies `BuildBrightnessTable`, `clamp(v + amount, 0, 255)` for an amount from -255 to 255.

**Contrast(contrast)**: applies `BuildContrastTable`: for a contrast `c` from -100 to 100, `C = c * 255 / 100`,
`f = 259 * (C + 255) / (255 * (259 - C))`, and the entry is `clamp(Round(f * (v - 128) + 128), 0, 255)`; a contrast of
0 gives the identity (`f` is exactly 1) and levels pivot at 128.

**ApplyTable(table)** (internal): maps the red, green, and blue of every pixel through a 256-entry table, in parallel
rows, in place; alpha is unchanged, and the same table serves the three channels so a gray pixel stays gray.

**AutoLevelsRange(histogram, clipPercent)** (internal): with `N` pixels, `k = max(1, ceil(N * p / 100))` (a tolerance of
1e-9 is subtracted before the ceiling so that floating-point noise in the product, such as `1000 * 0.1`, cannot add a
pixel); `black` is the smallest level whose cumulative count from level 0 is at least `k`, and `white` is the largest
level whose cumulative count from level 255 is at least `k`. When `white - black < 1`, or there are no pixels, the image
is flat and the result is `null` (no change).

**AutoLevels(clipPercent)**: `AutoLevelsRange(LumaHistogram(), clipPercent)` and, unless it is `null`,
`ApplyTable(BuildLevelsTable(black, white, 1))`; it returns the points that were applied. The stretch is based on luma
and applied to all three channels, so hues are preserved; a color cast is not removed.

#### Error Handling

The adjustments have no expected runtime errors: a threshold, brightness, contrast, clip, gamma, or level outside its
range throws `ArgumentOutOfRangeException`; a black level not below the white level, a table or histogram that does not
have 256 entries, a histogram with a negative count, or a histogram whose sums or score products overflow a 64-bit
integer throws `ArgumentException`; every instance method throws
`ObjectDisposedException` on a disposed surface. Arguments are validated before any pixel is changed, so a failed call
leaves the surface as it was. The command-line tool validates user input first and reports usage errors, so any
exception from here is a defect of the caller.

#### Dependencies

- **Surface** and **Rgba32** (the same subsystem) - the pixel storage and value type.
- The .NET base class library (`System.Threading.Tasks.Parallel`, `System.Math`).

#### Callers

- **ImagePipeline** (command-line tool) - runs the adjustments as pipeline operations, using the public constants to
  validate the user's values.
- **ImageGeometry**, **ImageAnalysis**, and **GridOverlay** (command-line tool) - use `Surface.Luma`; `ImageAnalysis`
  also uses `Surface.OtsuThreshold`.
- Consumers of the CanvasNet package - the adjustments are public API.
