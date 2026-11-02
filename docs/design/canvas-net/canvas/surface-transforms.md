### SurfaceTransforms Unit Design

<!-- cspell:ignore premultiplied transposed transversed -->

The `SurfaceTransforms` unit is the whole-surface transformations of the public `Surface` class of the `Canvas`
subsystem. It is a partial class file, `Surface.Transforms.cs`, together with the public enum `FitMode` (`FitMode.cs`),
so the transformations read as instance methods (`surface.FlipHorizontal()`) while the pixel storage stays in
`Surface.cs`.

#### Purpose

`Surface` offers `Crop`, `Resize`, and `ResizeToFit` for taking a region and changing the size. `SurfaceTransforms` adds
the rest of the pure, whole-surface pixel moves that image tools need: mirror about either axis and the half turn,
turning a camera photograph upright by its EXIF orientation, a
border (pad), cropping to the content inside a uniform border (trim), fitting into a box of an exact size, flattening
over a color, and the transparency test. They were originally buried in the command-line tool (its image pipeline and
`SurfaceOperations` helpers); they live in the core library so that every CanvasNet package, and any library user, can
use them, and the tool's image pipeline is a thin adapter over them.

#### Data Model

The unit adds no state to `Surface`. Its public surface is:

- `Surface FlipHorizontal()`, `Surface FlipVertical()`, `Surface Rotate180()`.
- `Surface ApplyExifOrientation(int orientation)`.
- `Surface Pad(int left, int top, int right, int bottom, Rgba32 color)`.
- `(int X, int Y, int Width, int Height)? FindTrimBounds(int tolerance)` and `Surface Trim(int tolerance)`, with the
  constant `MaxTrimTolerance` (255).
- `Surface FitInto(int boxWidth, int boxHeight, FitMode mode, Rgba32 fill, ResamplingMode filter = Auto)` and the enum
  `FitMode` (`Contain`, `Stretch`), and the static size calculation
  `(int Width, int Height) FitInside(int width, int height, int boxWidth, int boxHeight)`.
- `Surface FlattenOver(Rgba32 background)` and `bool HasTransparency()`.

#### Conventions

Every transformation is a **pure function**: it never modifies or disposes the surface it is called on and returns a new
surface that the caller owns. `(x, y)` has its origin at the top left with y down; surfaces hold straight
(non-premultiplied) alpha. The flips, the half turn, `ApplyExifOrientation`, `Pad`, and `Trim` are pixel moves or per-row
copies with no resampling, so the pixels of their results are exact copies of pixels of the source; `FitInto` resamples
(through `Resize`) and `FlattenOver` blends translucent pixels over its background.

#### Key Methods

**FlipHorizontal()**: copies each row and reverses it, so the pixel at `(x, y)` is the source pixel at
`(Width - 1 - x, y)`. **FlipVertical()** copies the row `Height - 1 - y` to row `y`. **Rotate180()** does both, the
mirror about both axes.

**ApplyExifOrientation(orientation)**: the orientation is the EXIF `Orientation` value 1 to 8, as read by
`JpegCodec.ReadExifOrientation` (a camera stores the pixels as the sensor delivered them and records how the image has to
be turned to be viewed upright; the JPEG decoder ignores the tag). For every pixel `(x, y)` of the result the source pixel
is, with `W` and `H` the size of the source: 1 `(x, y)`, 2 `(W - 1 - x, y)` (mirrored left to right), 3
`(W - 1 - x, H - 1 - y)` (turned 180 degrees), 4 `(x, H - 1 - y)` (mirrored top to bottom), 5 `(y, x)` (transposed),
6 `(y, H - 1 - x)` (to be turned 90 degrees clockwise), 7 `(W - 1 - y, H - 1 - x)` (transversed), and 8
`(W - 1 - y, x)` (to be turned 90 degrees counter-clockwise). For 5 to 8 the result is `H` wide and `W` high. The pixels,
alpha included, are copied exactly; orientation 1 is an independent copy. An orientation outside 1 to 8 throws before
anything is allocated.

**Pad(left, top, right, bottom, color)**: refuses a negative width (`ArgumentOutOfRangeException`) and a result wider or
higher than `MaxDimension` (`ArgumentException`, computed with `long` arithmetic before anything is allocated), then
creates the surface, fills it with `color` unless the color is fully transparent black (a new surface already is), and
`CopyFrom`s the source at `(left, top)`. The source pixels are copied, not blended with the border color.

**FindTrimBounds(tolerance)**: the **background color** is the most frequent color among the four corner pixels (top
left, top right, bottom left, bottom right, exact RGBA equality), a tie going to the earliest in that order. The
**distance** of a pixel from it is 0 when both have alpha 0 (every transparent pixel is the same), else the largest of
the absolute differences of red, green, blue, and alpha; a pixel is background when its distance is at most the
tolerance (0 to 255). The top and bottom are the first and last rows with a foreground pixel and the left and right the
smallest and largest foreground columns over those rows; the row scans exit early, and only a pixel left of the current
left edge or right of the current right edge can extend the box. The result is the inclusive box, or `null` when every
pixel is background.

**Trim(tolerance)**: `Crop` of the bounds, or a `Clone()` when `FindTrimBounds` is `null`, so nothing is trimmed from an
image without content.

**FitInto(boxWidth, boxHeight, mode, fill, filter)**: `Stretch`, or a source that already has the box size, is
`Resize(boxWidth, boxHeight, filter)` (an exact copy for the same size). `Contain` first finds the fitted size with
`FitInside` (`factor = min(boxWidth / width, boxHeight / height)`, each side rounded half away from zero and clamped
to 1 and the box; a small image is enlarged), resizes to it, and, when that is not the box, creates the box-sized
surface, `Clear`s it with `fill`, and `CopyFrom`s the fitted image at the centered position
`((boxWidth - width) / 2, (boxHeight - height) / 2)`. The intermediate surface is disposed on every path, including
failure.

**FlattenOver(background)**: a new surface cleared to `background` with the source composited over it
(`CompositeOver(Surface)`). **HasTransparency()**: scans the rows for a pixel with alpha below 255.

#### Error Handling

An undefined `FitMode`, a box outside 1 to `MaxDimension`, a negative border width, an EXIF orientation outside 1 to 8,
or a tolerance outside 0 to 255 throws `ArgumentOutOfRangeException`; a padded size beyond `MaxDimension` throws
`ArgumentException`; every method throws `ObjectDisposedException` on a disposed surface. Arguments are validated before
any allocation. A surface that was
allocated for a result is disposed when a later step fails, so a failed call leaks nothing. The command-line tool maps a
refused size to its `size-limit` error before calling `Pad`, so the exception here is a defect of the caller.

#### Dependencies

- **Surface** (`Crop`, `Clone`, `CopyFrom`, `Clear`, `CompositeOver`, `Resize`) and **Rgba32** (the same subsystem).
- The .NET base class library.

#### Callers

- **ImagePipeline**, **SourceLoader**, **ContactSheet**, **ImageWriter**, and **CompareCommand** (command-line tool) -
  flip, pad, trim, fit (and the `FitInside` size calculation), flatten, the transparency test, and the EXIF orientation.
- Consumers of the CanvasNet package - the transformations are public API.
