### ImageAnalysis

<!-- cspell:ignore canvasnet luma otsu percentile percentiles -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImageAnalysis` reads what an image contains: its dominant colors, the statistics of its luma, and the pixels around a
point. Its single responsibility is that measuring; it knows nothing of files, command-line options, or how a surface was
loaded, and it never changes the surface, so the `inspect` command decides which image is analyzed and how the result
is presented. The unit is the static class `ImageAnalysis` with the records `DominantColor`, `LumaStatistics`,
`ImageStatistics`, `PixelWindow`, and `PixelSample`.

#### Conventions

Surfaces hold **straight** (non-premultiplied) `Rgba32` pixels. A translucent pixel is **flattened** over an opaque
background color before it is counted or measured: `c' = (c * a + bg * (255 - a) + 127) / 255` per channel, and an opaque
pixel is unchanged. All arithmetic is exact integer arithmetic (sums are `long`); the only floating point is the final
rounding of the reported fractions and the mean. The pass over the pixels is sequential and allocates only the fixed
tables (4096 buckets and 256 levels), never per pixel, so an 8192 by 8192 image takes about a third of a second.

#### Key Methods

**Analyze(surface, background, count)**: the count is 1 to 32 (`MinColors`, `MaxColors`; the command default is
`DefaultColors`, 8) and the background must be opaque. One pass over the flattened pixels fills three structures:

- *Color buckets.* `bucket = ((R' >> 4) << 8) | ((G' >> 4) << 4) | (B' >> 4)` (4096 buckets); each bucket has a pixel
  count and the sums of the flattened `R'`, `G'`, `B'`.
- *Luma histogram.* `Y = (77 R' + 150 G' + 29 B' + 128) >> 8` of the flattened pixel, counted in 256 levels.
- *Total pixels.*

*Clustering* (so that a gradient of paper shades is one color): the occupied buckets are ordered by pixel count
(largest first, ties by the smaller bucket index) and clustered greedily in that order. A bucket joins the **first**
cluster, in creation order, whose *leader* bucket (the bucket that started the cluster) is within `ClusterRadius` = 2
bucket units on every channel, `max(|dr|, |dg|, |db|) <= 2` (about 32 levels); otherwise it starts a new cluster. The
distance is measured to the leader, not to the nearest member, so a long gradient is split into several colors. A
cluster has the sum of the pixels of its buckets and the mean color of those pixels, `(2 * sum + n) / (2 * n)` per channel
(rounded half up). The clusters are ordered by pixels (largest first, ties by the smaller leader bucket) and the first
`count` are reported as `DominantColor(Color, Pixels, Fraction)`, where the color is opaque and
`Fraction = pixels / total` is rounded to 6 decimals. The cost is at most 4096 times the number of clusters.
Example (pinned by a test): the gray levels 128 (4 pixels), 144 (2), 160 (1), and 208 (1) are the buckets 8, 9, 10, and
13; the buckets 9 and 10 are within 2 of the leader 8, so there are two colors, `gray 137` (the mean of
`(128 * 4 + 144 * 2 + 160) / 7 = 137.14`, 7 pixels, 0.875) and `gray 208` (0.125).

*Luma statistics* (`LumaStatistics`) come from the histogram: `Mean` is the sum of the levels divided by the pixel count,
rounded to 2 decimals; `Min` and `Max` are the smallest and largest level that occurs; the percentiles `P1`, `P50`, and
`P99` are the smallest level whose cumulative count is at least `ceil(N * p / 100)`, with a minimum of 1 pixel; `Otsu`
is `ImageAdjustments.OtsuThreshold` of the histogram, which is what an automatic threshold would choose for an image
flattened over the same background; and `Histogram` is 16 counts, the number of pixels whose level divided by 16 (an
integer shift) is the index. Example: an image with each gray level once has the mean 127.5, `P1` 2 (the third pixel), `P50`
127, `P99` 253, `Otsu` 127, and 16 pixels in each bin.

**Sample(surface, x, y, radius)**: `x` and `y` are integers inside the image, otherwise `ToolException` with code
`usage-error` and the message `The point 5000,10 is outside the 4032x3024 image.` with a hint naming the valid range;
the radius is 0 to 8 (`MaxRadius`). The result is `PixelSample(X, Y, Pixel, Luma, Window)` with the pixel as stored (not
flattened) and `ImageAdjustments.Luma` of it (over white). When the radius is above 0 the window is
`[x - r, x + r] x [y - r, y + r]` **clipped** to the image, so `PixelWindow` carries the actual rectangle (`X`, `Y`,
`Width`, `Height`), all of its pixels in row-major order, and `Mean`, each channel (alpha included) rounded half up as
`(2 * sum + n) / (2 * n)`. For a radius of 0 the window is `null`.

#### Error Handling

The only expected error is the point outside the image, a `usage-error`, because the point comes from the user. A null
surface throws `ArgumentNullException`; a translucent background throws `ArgumentException`; a color count or radius
outside its range throws `ArgumentOutOfRangeException`; these are defects of the caller, which validates user input.

#### Dependencies

- **CanvasNet core library** - `Surface` and `Rgba32`.
- **ImageAdjustments** - `Luma` and `OtsuThreshold`.
- **ToolException and ErrorCodes** - the usage error for a point outside the image.

#### Callers

- **InspectCommand** - runs `Analyze` for `inspect colors` and `Sample` for `inspect pixel`.
