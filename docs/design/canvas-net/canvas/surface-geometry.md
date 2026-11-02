### SurfaceGeometry Unit Design

<!-- cspell:ignore canvasnet bilinear premultiplied deskew -->

The `SurfaceGeometry` unit is the rotation by any angle and the skew estimation of the public `Surface` class of the
`Canvas` subsystem. It is the partial class files `Surface.Geometry.cs` (rotation) and `Surface.Skew.cs` (skew
estimation), together with the public record `SkewEstimate` (`SkewEstimate.cs`), so the operations read as methods of the
surface (`surface.RotateArbitrary(30, fill)`) while the pixel storage stays in `Surface.cs`.

#### Purpose

`Surface` offers `Crop`, `Resize`, and the exact right-angle moves of `SurfaceTransforms`. `SurfaceGeometry` adds the
geometry that needs resampling: rotation by any angle onto an expanded canvas with a fill color, and the estimation of the
skew of a scanned or photographed page, so that the two together level a tilted picture. They were originally part of the
command-line tool's image pipeline (`ImageGeometry`); they live in the core library so that every CanvasNet package, and
any library user, can use them, and the tool's pipeline is a thin adapter over them. The unit knows nothing of files,
command-line options, or how a surface was loaded.

#### Data Model

The unit adds no state to `Surface`. Its public surface is:

- `static (int Width, int Height) RotatedSize(int width, int height, double degrees)`.
- `Surface RotateArbitrary(double degrees, Rgba32 fill, bool nearest = false)`.
- `SkewEstimate EstimateSkew()`.
- The record struct `SkewEstimate(double Degrees, double Confidence, bool Estimated)` with the constants
  `MinConfidence` (0.4), `MinCorrectedDegrees` (0.05), and `MaxDegrees` (15), the property `IsCorrectable`, and the
  property `CorrectionDegrees`.

`RotateArbitraryCore(degrees, fill, nearest, useFastPaths)` is internal and reached from the tests through
`InternalsVisibleTo`; it is the one implementation, with the switch that turns the sampling fast paths off to prove they
give the same pixels. `PreferCandidate` (the tie-break of the angle search) is internal for the same reason.

#### Conventions

Angles are in degrees and a positive angle turns **clockwise** on screen; `(x, y)` has its origin at the top left with y
down; surfaces hold straight (non-premultiplied) `Rgba32` pixels. Rotation never modifies or disposes its input and
returns a new surface the caller owns. A rotation **resamples** (nearest or bilinear): its pixels are not exact copies of
source pixels, unlike the right-angle moves, and rotating by an angle and then by its negative is not lossless; even an
angle that is a multiple of 90 degrees is resampled (use `Rotate180` or `ApplyExifOrientation` for an exact move). Rows
are processed with `Parallel.For` and write only their own row, and every pixel is calculated from its own coordinates (no
incremental accumulation), so the result does not depend on the number of cores or on scheduling. Nothing allocates per
pixel. Floating point is always `double`, with no fused multiply-add; `Math.Sin` and `Math.Cos` could differ in the last
bit between platforms, which can only flip a rounding at an exact half, so the determinism guarantee is "the same machine
and runtime version"; the skew estimate avoids even that by rounding the sine and cosine to fixed point.

#### Key Methods

**RotatedSize(width, height, degrees)**: the angle is first reduced with `degrees % 360` (exact in IEEE arithmetic, so a
huge finite angle cannot overflow when converted to radians, and an angle below 360 in magnitude is unchanged); then,
with `c = |cos θ|` and `s = |sin θ|` (`θ = reduced * π / 180`) the canvas is

```text
newW = max(1, ceil(W * c + H * s - 1e-9))
newH = max(1, ceil(W * s + H * c - 1e-9))
```

The `1e-9` absorbs floating-point noise so that an exact fit (a right angle) does not gain a pixel. Worked examples
(pinned by a test): 100 by 50 gives 50 by 100 at 90 degrees, `ceil(106.066) = 107` by 107 at 45, and
`ceil(100 * 0.866 + 50 * 0.5 = 111.6) = 112` by `ceil(100 * 0.5 + 50 * 0.866 = 93.3) = 94` at 30; 160 by 100 at 30 gives
189 by 167. It is pure arithmetic and does not limit the result, which can exceed `MaxDimension` (8192 by 8192 at 45
degrees gives 11586 by 11586); the width and height are limited to 1 to `MaxDimension` so that the arithmetic cannot overflow.

**RotateArbitrary(degrees, fill, nearest)**: validates the angle (finite) and, from `RotatedSize`, refuses a canvas larger
than `MaxDimension` on a side with an `ArgumentException`, before anything is allocated; then allocates the canvas and
fills it row by row in parallel. For the destination pixel `(xd, yd)`, with `dx = xd + 0.5 - newW / 2.0` and
`dy = yd + 0.5 - newH / 2.0`, the continuous source coordinate (pixel `i` covers `[i, i + 1)`) is

```text
u =  dx * cos θ + dy * sin θ + W / 2.0
v = -dx * sin θ + dy * cos θ + H / 2.0
```

which is the inverse of a clockwise rotation about the center (`θ` is the angle reduced modulo 360 as above; a source
point right of the center, `sx > 0, sy = 0`, lands
below the center of the result for `θ = 90°`; a test pins it). The canvas is disposed if a row fails.

- **Nearest**: `x = floor(u)`, `y = floor(v)`; outside the image the result is the fill; no antialiasing.
- **Bilinear**: with `p = u - 0.5` and `q = v - 0.5`, `x0 = floor(p)`, `y0 = floor(q)`, `fx = p - x0`, `fy = q - y0`, the
  taps `(x0, y0)`, `(x0 + 1, y0)`, `(x0, y0 + 1)`, and `(x0 + 1, y0 + 1)` have the weights `(1 - fx)(1 - fy)`,
  `fx (1 - fy)`, `(1 - fx) fy`, and `fx fy`. **A tap outside the image is the fill color**, so the rotated edge is
  antialiased by a half-pixel blend between the border pixel and the fill, and a point more than one pixel outside is pure
  fill. The blend is premultiplied: with the tap alphas `a` and weights `w`, `sumA = sum(w a)`, the result alpha is
  `Round(sumA)` and each color channel is `Round(sum(w c a) / sumA)` (half away from zero). A result whose alpha is 0
  (including a fully transparent fill) is stored as `(0, 0, 0, 0)`.
- **Fast paths**: when all four taps are inside the image no bounds tests are made, and when all four are outside
  (`x0 + 1 < 0`, `x0 >= W`, `y0 + 1 < 0`, or `y0 >= H`) the fill is stored directly (a transparent fill as
  `(0, 0, 0, 0)`). Both use the same blend as the generic path, so they give identical pixels; a test compares them
  through `RotateArbitraryCore` with `useFastPaths: false`.

Kernels beyond bilinear are not provided. (The core resampler `SurfaceResampler` is not reused: it resizes, and its
sampling and rounding are not these.)

**EstimateSkew()** (in `Surface.Skew.cs`): finds the clockwise rotation `φ` of the dominant near-horizontal structure
(text lines, table rules, sketch baselines, page edges) within **15 degrees** either way; `φ` is positive when the lines
descend to the right, so the image is levelled by rotating it by `-φ` clockwise. Dark marks on a lighter ground are
assumed (invert first for light marks on a dark ground). The result is the record `SkewEstimate(Degrees, Confidence,
Estimated)`.

1. **Analysis plane.** `f = ceil(max(W, H) / 1024)` (1 for an image of up to 1024 pixels). The plane is
   `ceil(W / f)` by `ceil(H / f)`; each pixel is the integer mean `(sum + n / 2) / n` of `Surface.Luma` over its
   (possibly partial, at the edges) `f` by `f` block, `n` being the number of pixels in it, so transparency is flattened
   over white. Rows are computed in parallel into disjoint output.
2. **Dark marks** (immune to uneven lighting): from an integral image (`long`), a pixel of value `v` whose window of
   radius 8 (clipped to the plane) holds `count` pixels with the sum `sum` is a mark when `sum - v * count >= 10 * count`,
   that is when it is at least 10 levels darker than the local mean (exact integers). The marks are collected once into
   two `int` arrays.
3. **Too few marks.** With fewer than `max(100, 0.0005 * w * h)` marks the result is `SkewEstimate(0, 0, false)`.
4. **Score of an angle.** The angle is held as an integer number of hundredths of a degree `k`, `φ = k / 100`.
   `cosQ = Round(cos φ * 2^20)` and `sinQ = Round(sin φ * 2^20)` are fixed-point numbers, so that last-bit differences of
   `Math.Cos` and `Math.Sin` between platforms cannot move a mark to another bin. Each mark `(x, y)` goes to the bin
   `floor(y cos φ - x sin φ) = (y * cosQ - x * sinQ) >> 20` plus an offset (this projection is constant along a line that
   descends at the angle `φ`); the score is the sum over the bins of the **squared count**, accumulated as `2 c + 1` each
   time a bin holding `c` marks gains one (`long`; maximizing it maximizes the variance of the profile). One array of bins
   is reused and cleared for every angle, so nothing allocates per mark. The bins are **whole pixels**: the first design
   used half-pixel bins (`floor(2 r)`), which at exactly 0 degrees leave every other bin empty and so double the score of
   that one angle (a picture tilted by 0.3 degrees was estimated as 0, and random noise scored a confidence of 0.5).
5. **Coarse to fine search** over `k`: every 100 (1 degree) from -1500 to 1500 (31 angles); then
   `k1 + {-100, -75, ..., 100}`
   around the best `k1` (angles beyond 15 degrees are skipped); then `k2 + {-25, -20, ..., 25}` around the best `k2`. About
   51 evaluations. **Ties**: a higher score wins; equal scores prefer the smaller `|k|`; still equal, the smaller `k` (more
   negative).
6. **Result.** `Degrees = k / 100.0` (exactly the hundredths, no accumulated error). `Confidence =
   (best - median) / best` where `median` is the 16th smallest of the 31 first-stage scores (a flat profile, which is
   what an image without line structure gives, is near 0; strong lines give a value near 1), 0 when `best` is 0, rounded
   to 3 decimals.

**SkewEstimate thresholds (pinned here).** `MinConfidence = 0.4` and `MinCorrectedDegrees = 0.05` are the thresholds of
`IsCorrectable`: an estimate is worth correcting only when it was estimated, `|Degrees| >= 0.05`, and
`Confidence >= 0.4`. `CorrectionDegrees` is the clockwise angle in 0 up to (not including) 360 that levels the image,
`360 - Degrees` for a positive skew and `-Degrees` otherwise. The first guess of 0.03 for the confidence did not
separate noise from lines: the rectangle of the image
favors a horizontal profile (the profile of a rectangle is a sharp box at 0 degrees and a spread trapezoid at other
angles), so even noise has a baseline confidence that grows with the aspect ratio of the image. Measured values with the
whole-pixel bins (synthetic pictures made by `ImageFixtures.SketchPhoto` and in the same style):

- 400 by 300 ruled lines, tilts -14.5, -12, -7, -3, -0.5, 0, 0.3, 2.5, 5, 7, and 14.5: the estimate is within 0.07
  degrees of the tilt, the confidence 0.925 to 0.939.
- 160 by 100 ruled lines (1 to 4 lines), tilt 3: 2.95 to 3.15 degrees, confidence 0.885 to 0.902.
- 3000 by 2000 thick lines, tilt 4: 4.00 degrees, confidence 0.96.
- 8192 by 6000 lines, tilt 2.5: 2.50 degrees, confidence 0.90, in about 190 ms.
- 400 by 300 lines, tilt -6, paper shaded by 80 levels across the picture, ink 70 levels darker than the paper: -6.00
  degrees, confidence 0.925.
- 400 by 300 dashed text-like lines, tilt 5: 5.05 degrees, confidence 0.735.
- 400 by 300 with 40 random strokes in random directions: no meaningful angle, confidence 0.20 to 0.30.
- Random noise: confidence 0.053 (400 by 300), 0.037 (800 by 800), 0.009 (300 by 900), 0.179 (1000 by 250), and 0.304
  (2000 by 300).
- Uniform paper: not estimated, confidence 0.

0.4 is above everything that has no dominant line direction (at most 0.30) and below the weakest line structure tried
(0.735). A very wide image with random content can still approach it, so a caller that corrects the skew should still
show the confidence and let the user check the angle.

#### Error Handling

The methods take no reference arguments, so none can be null. An angle that is not finite, or a width or height outside
1 to `MaxDimension` given to `RotatedSize`,
throws `ArgumentOutOfRangeException`; a rotation whose canvas would be larger than `MaxDimension` on a side throws
`ArgumentException`, computed from the numbers alone, before anything is allocated; every method throws
`ObjectDisposedException` on a disposed surface. A canvas allocated for the result is disposed if filling it fails. The
command-line tool checks the size itself, before calling `RotateArbitrary`, to report its own `size-limit` error with a
hint, so the exception here is a defect of the caller.

#### Dependencies

- **Surface** (`GetRowSpan`, `Luma`, the constructor) and **Rgba32** (the same subsystem).
- The .NET base class library (`Parallel`, `Math`).

#### Callers

- **ImagePipeline** (command-line tool) - the rotate and deskew operations.
- Consumers of the CanvasNet package - rotation and skew estimation are public API.
