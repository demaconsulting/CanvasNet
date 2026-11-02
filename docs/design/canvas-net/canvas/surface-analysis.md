### SurfaceAnalysis Unit Design

<!-- cspell:ignore canvasnet luma otsu percentile percentiles -->

The `SurfaceAnalysis` unit is the analysis of what a surface contains, as methods of the public `Surface` class of the
`Canvas` subsystem. It is the partial class file `Surface.Analysis.cs`, together with the public records of
`SurfaceStatistics.cs` (`DominantColor`, `LumaStatistics`, `SurfaceStatistics`, `PixelWindow`, `PixelSample`).

#### Purpose

Choosing how to adjust or convert an image starts from what it contains: the colors of a region, the distribution of
its brightness, the value of one pixel. The unit reports the dominant colors and luma statistics of a surface and the
pixel at a point with the mean of its neighborhood, with exact results that are the same on every run. It was originally
part of the command-line tool (`ImageAnalysis`); it lives in the core library so that every CanvasNet package, and any
library
user, can use it. The unit knows nothing of files, options, or commands, and never changes the surface.

#### Data Model

The unit adds no state to `Surface`. Its public surface is:

- `SurfaceStatistics Analyze(Rgba32 background, int colorCount)`.
- `PixelSample Sample(int x, int y, int radius = 0)`.
- **SurfaceStatistics** (record): `TotalPixels`, `Colors` (a list of `DominantColor`, the most common first), and `Luma`
  (a `LumaStatistics`), with the constants `MinColors` (1) and `MaxColors` (32).
- **DominantColor** (record): the opaque `Color` (the mean of the flattened pixels of the cluster), `Pixels`, and
  `Fraction` (`Pixels / total` rounded to 6 decimals).
- **LumaStatistics** (record): `Mean` (2 decimals), `Min`, `Max`, `P1`, `P50`, `P99`, `Otsu` (the threshold
  `Surface.OtsuThreshold` chooses for the histogram), and `Histogram` (16 counts).
- **PixelSample** (record): `X`, `Y`, `Pixel`, `Luma` (`Surface.Luma` of the pixel), and `Window` (a `PixelWindow` or
  `null`), with the constant `MaxRadius` (8).
- **PixelWindow** (record): `X`, `Y`, `Width`, `Height` (clipped to the surface), the `Mean` of each channel (alpha
  included), and the `Pixels` in row-major order.

`ClusterRadius` (2) is internal and reached from the tests through `InternalsVisibleTo`.

#### Conventions

Colors are counted in 4096 buckets of 4 bits per channel and clustered greedily, so that a gradient of paper shades is one
color; the luma statistics come from the same single pass. All arithmetic is exact integer arithmetic (the only floating
point is the rounding of the reported fractions and mean), the pass is sequential, and nothing allocates per pixel, so the
result is the same on every run. A surface must not be changed by another thread while it is analyzed.

#### Key Methods

**Analyze(background, colorCount)**: each pixel is flattened over the opaque `background`, per channel
`(c * a + bg * (255 - a) + 127) / 255` (an opaque pixel is unchanged). Its bucket is
`((R >> 4) << 8) | ((G >> 4) << 4) | (B >> 4)` of the flattened color; the buckets count their pixels and sum their
flattened channels, and the luma histogram of 256 bins counts the integer luma `(77 R + 150 G + 29 B + 128) >> 8` of
the flattened pixel. The occupied buckets are ordered by count (largest first, ties by bucket index) and clustered
greedily in that order: a bucket joins the first cluster, in creation order, whose leader bucket is within 2 bucket
units on every channel, otherwise it leads a new cluster. A cluster has the pixels of its buckets and the mean color of
those pixels (rounded half up). The clusters are ordered by pixels (largest first, ties by the leader bucket) and the
first `colorCount` are reported. The luma statistics are: the mean (rounded to 2 decimals), the smallest and largest
level that occur, the percentiles 1, 50, and 99 (the smallest level whose cumulative count is at least
`ceil(N * p / 100)`), the Otsu threshold of the 256-bin histogram, and a 16-bin histogram of `level >> 4`.

**Sample(x, y, radius)**: the pixel at `(x, y)` and its `Surface.Luma`; with a radius of 0 no window. With a radius from
1 to 8 the window `[x - radius, x + radius] x [y - radius, y + radius]` clipped to the surface, with its actual
rectangle, all its pixels, and the mean of each channel (alpha included), `(2 sum + n) / (2 n)`, rounded half up.

#### Error Handling

A color count outside 1 to 32 and a radius outside 0 to 8 throw `ArgumentOutOfRangeException`, as does a point outside
the surface (naming `x` or `y`); a background that is not opaque throws `ArgumentException`; every method throws
`ObjectDisposedException` on a disposed surface. Arguments are validated before the pass over the pixels. The
command-line tool checks the point itself, before calling `Sample`, to report its own `usage-error` with a hint, so the
exception here is a defect of the caller.

#### Dependencies

- **Surface** (`GetRowSpan`, the indexer, `Luma`, `OtsuThreshold`) and **Rgba32** (the same subsystem).
- The .NET base class library (`System.Linq`, `Math`).

#### Callers

- **InspectCommand** (command-line tool) - the `colors` and `pixel` subcommands.
- Consumers of the CanvasNet package - the analysis is public API.
