### ImageGeometry

<!-- cspell:ignore canvasnet bilinear premultiplied deskew -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageGeometry` holds the geometry of a surface that the pipeline needs beyond right-angle moves: rotation by any angle
onto an expanded canvas with a fill color, and the bounds of the content inside a uniform border (trim). Its single
responsibility is that geometry; it knows nothing of files, command-line options, or how a surface was loaded, so the
pipeline decides when and with what fill it runs. The unit is the static class `ImageGeometry`.

#### Conventions

Angles are in degrees and a positive angle turns **clockwise** on screen; `(x, y)` has its origin at the top left with
y down; surfaces hold straight (non-premultiplied) `Rgba32` pixels. Rotation and trim never modify or dispose their input
and each returns a new surface the caller owns. Rows are processed with `Parallel.For` and write only their own row, and
every pixel is calculated from its own coordinates (no incremental accumulation), so the result does not depend on the
number of cores or on scheduling. Nothing allocates per pixel. Floating point is always `double`, with no fused
multiply-add; `Math.Sin` and `Math.Cos` could differ in the last bit between platforms, which can only flip a rounding at
an exact half, so the determinism guarantee is "the same machine and runtime version" as for the rest of the tool.

#### Key Methods

**RotatedSize(width, height, degrees)**: with `c = |cos θ|` and `s = |sin θ|` (`θ = degrees * π / 180`) the canvas is

```text
newW = max(1, ceil(W * c + H * s - 1e-9))
newH = max(1, ceil(W * s + H * c - 1e-9))
```

The `1e-9` absorbs floating-point noise so that an exact fit (a right angle) does not gain a pixel. Worked examples
(pinned by a test): 100 by 50 gives 50 by 100 at 90 degrees, `ceil(106.066) = 107` by 107 at 45, and
`ceil(100 * 0.866 + 50 * 0.5 = 111.6) = 112` by `ceil(100 * 0.5 + 50 * 0.866 = 93.3) = 94` at 30; 160 by 100 at 30 gives
189 by 167. It is pure arithmetic and does not limit the size.

**CheckRotatedSize(width, height, degrees)**: `RotatedSize`, then a canvas larger than `Surface.MaxDimension` (8192) on
a side throws a `ToolException` with code `size-limit`, the message "The rotated image would be NxM pixels, more than the
maximum of 8192 pixels on a side.", and the hint "Shrink the image first, for example --fit 4096x4096 before --rotate or
--deskew." The refusal is from the numbers alone, before any allocation.

**RotateArbitrary(source, degrees, fill, nearest)**: checks the size, allocates the canvas, and fills it row by row in
parallel. For the destination pixel `(xd, yd)`, with `dx = xd + 0.5 - newW / 2.0` and `dy = yd + 0.5 - newH / 2.0`, the
continuous source coordinate (pixel `i` covers `[i, i + 1)`) is

```text
u =  dx * cos θ + dy * sin θ + W / 2.0
v = -dx * sin θ + dy * cos θ + H / 2.0
```

which is the inverse of a clockwise rotation about the center (a source point right of the center, `sx > 0, sy = 0`, lands
below the center of the result for `θ = 90°`; a test pins it). The unit does not special-case right angles: the pipeline
moves the pixels of 90, 180, and 270 degrees exactly and copies for 0, and uses this method for every other angle.

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
  `(0, 0, 0, 0)`). Both use the same `Blend` as the generic path, so they give identical pixels; a test compares them with
  the optional `useFastPaths: false` argument.

Rotating by an angle and then by its negative is not lossless; kernels beyond bilinear are not provided.

**FindTrimBounds(surface, tolerance)**: the **background color** is the most frequent color among the four corner pixels
(top left, top right, bottom left, bottom right, by exact RGBA equality), a tie going to the earliest in that order. The
**distance** of a pixel `p` from the background `b` is `0` when both have alpha 0 (all transparent pixels are equal),
otherwise `max(|dR|, |dG|, |dB|, |dA|)`; a pixel is **background** when its distance is at most the tolerance (0 to 255),
otherwise foreground. The result is the inclusive bounding box of the foreground: `top` is the first row with a
foreground pixel, `bottom` the last such row, and `left` and `right` the smallest and largest foreground columns over
the rows `top` to `bottom` (row scans with early exit: only a pixel left of the current left edge or right of the current
right edge is looked for, so the cost is `O(W * H)` at worst and one thread is enough). When there is no foreground
pixel (an all-background image, or a 1 by 1 image) the result is `null`.

**Trim(surface, tolerance)**: `surface.Crop` of the bounds, or a copy of the whole surface when `FindTrimBounds` is
`null`, so nothing is trimmed from an image without content.

#### Error Handling

A rotation that would be too large is a `ToolException` with code `size-limit`, an expected error. Everything else is a
defect of the caller, since the pipeline validates user input first: a null surface throws `ArgumentNullException`; an
angle that is not finite, a width or height below 1, or a tolerance outside 0 to 255 throws
`ArgumentOutOfRangeException`.

#### Dependencies

- **ToolException** and **ErrorCodes** - the `size-limit` error.
- **CanvasNet core library** - `Surface` and `Rgba32`.

#### Callers

- **ImagePipeline** - the rotate and trim operations.
