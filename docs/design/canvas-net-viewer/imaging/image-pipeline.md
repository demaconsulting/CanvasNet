### ImagePipeline

<!-- cspell:ignore canvasnet jpeg lanczos RRGGBBAA grayscale deskew luma otsu -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImagePipeline` is the ordered image pipeline. Its single responsibility is to apply a list of operations, left to
right, to the pixels of a surface: crop, resize to an exact
size, fit inside a box, rotate by any angle, flip, pad, the tone adjustments (grayscale, invert, threshold, levels,
brightness, contrast, auto-levels), deskew, trim, and grid. The `ImageRenderer` and `ImageInspector` units share it, so
a verb command of the tool (`crop`, `resize`, `rotate`, `flip`, `pad`) is exactly a render with one operation placed
first. The arithmetic of the pixel operations lives in the core `Surface` class (flip, pad, trim, the tone
adjustments, rotation by any angle, and skew estimation) and in the unit `GridOverlay`;
the pipeline decides which surface they work on, maps a refusal to a `ViewerException`, and
reports what they did. The reading of the operations from the text of a command line is not part of this unit: it is
the `ImagePipelineParser` type of the `canvasnet` tool (documented with the `RenderCommand` unit of the `CanvasNetTool`
system), which builds the operation records described below and the settings (the filter, the border color, the
rotation fill, the clip of auto-levels, and the trim tolerance) that the records carry. The unit is the
static class `ImagePipeline` (`ImagePipeline.cs`, `ImagePipeline.Validation.cs` for the check of the operation values, and
`ImagePipeline.Anchors.cs` for the mapping of the anchors of a
model through the applied steps) and the operation types in `ImageOperations.cs`, which also holds the value types
`Dimension` (with the helpers `Dimension.Pixels(value)` and `Dimension.Percent(value)`), `GeometrySpec` (a rectangle in
pixels or percent), `PadSpec`, and `FlipAxis` that the records carry. Every value of a crop region is non-negative
(there is no inset form: x and y are 0 or more, the size more than 0, a percentage at most 100, all refused as
`usage-error` by the check of the operation values), a percentage is of the size at that step of the pipeline, and a
region past the right or bottom edge is cut at the edge.

#### Data Model

**IImageOperation** (interface): a step of the pipeline, with its `Name` (the `op` of the result). The records that
implement it are plain descriptions of what to do:

<!-- markdownlint-disable MD013 -->
| Record | Name | Parameters |
| ------ | ---- | ---------- |
| `CropOperation` | `crop` | `Region` (a `GeometrySpec`: origin and size, each in pixels or percent) |
| `ResizeOperation` | `resize` | `Width`, `Height`, `Filter` (default auto) |
| `FitOperation` | `fit` | `Width`, `Height` (the box), `AllowUpscale` (default off), `Filter` (default auto) |
| `RotateOperation` | `rotate` | `Degrees` (a `double` from 0 up to 360, clockwise), `Fill` (`Rgba32?`; `null` for the default rule), `Filter` |
| `FlipOperation` | `flip` | `Axis` (`FlipAxis`: horizontal, vertical, or both) |
| `PadOperation` | `pad` | `Border` (a `PadSpec`: left, top, right, bottom) and `Color` (`Rgba32?`; `null` for the caller's default) |
| `GrayscaleOperation` | `grayscale` | none |
| `InvertOperation` | `invert` | none |
| `ThresholdOperation` | `threshold` | `Level` (`int?`: 0 to 255, or `null` for automatic) |
| `LevelsOperation` | `levels` | `Black` (0 to 254), `White` (1 to 255, above black), `Gamma` (0.1 to 10) |
| `BrightnessOperation` | `brightness` | `Amount` (-255 to 255) |
| `ContrastOperation` | `contrast` | `Amount` (-100 to 100) |
| `AutoLevelsOperation` | `auto-levels` | `ClipPercent` (0 to 20; default `Surface.DefaultAutoLevelsClipPercent`, 1) |
| `DeskewOperation` | `deskew` | `Fill` (`Rgba32?`), `Filter` |
| `TrimOperation` | `trim` | `Tolerance` (0 to 255) |
| `GridOperation` | `grid` | `Spacing` (8 to 4096), `Color` (`Rgba32?`; `null` for `GridOverlay.DefaultColor`) |
<!-- markdownlint-enable MD013 -->

**Command-line options of the tool.** The tool reads `--crop`, `--resize`, `--fit`,
`--rotate`, `--flip`, `--pad`, `--grayscale`, `--invert`, `--threshold`, `--levels`, `--brightness`, `--contrast`,
`--auto-levels`, `--deskew`, `--trim`, and `--grid` as steps (repeatable, in command-line order); `--pad-color`,
`--allow-upscale`, `--filter`, `--rotate-fill`, `--auto-levels-clip`, and `--trim-tolerance` are settings of the whole
command line (the last occurrence applies to every step it concerns). This reading is done by `ImagePipelineParser` in
the tool; this unit receives the finished records.

**Filters**: `auto`, `nearest`, `bilinear`, `bicubic`, `lanczos3`, and `area`, in any case, mapped to the core library's
`ResamplingMode` by `TryParseFilter` (the names are listed by `FilterNames`, a list that cannot be changed, not even
through a cast); `FilterName` gives the lower-case name used
in results. A rotation uses only two kernels: `nearest`
copies pixels and every other filter interpolates bilinearly.

#### Key Methods

**The values of the operations** (checked by the tool's parser for the text a user types, and again by
`ImagePipeline.Validate` in `ImagePipeline.Validation.cs`, which `Apply` runs on the whole list before it applies the
first operation and which `ImageRenderer` and `ImageInspector` also run before they decode anything, so a record built by
hand, copied with `with`, or taken as a default value is refused like the same text would be). A violation is a `usage-error`
(`size-limit` for a size or border above `Surface.MaxDimension`) whose message names the position (from 1) and the
operation: `Operation 2 (resize): the width must be at least 1 pixel, not 0.` The rules:

- A crop region has finite values, an origin of 0 or more, a width and height above 0, and a percentage of at most 100;
  what depends on the image (a region outside it) is decided when the step is applied.
- A resize or fit has a width and height of at least 1 (and at most `Surface.MaxDimension`, else `size-limit`) and a
  defined filter; a pad has sides of 0 or more (at most `Surface.MaxDimension`, else `size-limit`; the total size is
  still checked when the border is applied); a flip has a defined axis; a deskew has a defined filter.
- A rotation is an angle in degrees, any finite number, normalized to 0 up to 360 (so `-90` is 270 and `390` is 30); a
  full turn is 0. Only a value that is not finite (NaN or an infinity) is refused.
- A threshold is a whole number from 0 to 255 or automatic; levels are a black point (a whole number from 0 to 254), a
  white point (from 1 to 255 and above black), and a gamma (a finite number from 0.1 to 10, default 1); brightness is a
  whole number from -255 to 255; contrast a finite number from -100 to 100; auto-levels a clip from 0 to 20; trim a
  tolerance from 0 to 255; a grid has a spacing (a whole number from 8 to 4096) and an optional color.
- A null element of the list is a defect of the caller (`ArgumentNullException`), not a value error.

**Apply(input, operations, defaultPadColor, out applied, userBackground)**: validates the list (see above), then runs the
steps in order. The input is never
modified or disposed, and the result is always a surface different from the input - for an empty list a copy - which
the caller disposes. `userBackground` is the background color the user gave (or `null`) and only decides the fill of a
rotation. Each step adds an `AppliedOperation` to `applied`.

*Ownership.* A step that changes pixels in place (`grayscale`, `invert`, `threshold`, `levels`, `brightness`,
`contrast`, `auto-levels`, `grid`) is given the current surface when the pipeline owns it (an intermediate), and a
copy of the input (`Crop` of the whole image) when the current surface is the caller's input; either way it returns the
surface it changed. Every other step makes a new surface. After each step the previous surface is disposed unless it is
the input or the step returned it, and also when a step fails, so a failure leaves nothing behind but the input and at
most two full-size surfaces ever exist.

<!-- markdownlint-disable MD013 -->
| Operation | Fields (in order) |
| --------- | ----------------- |
| `crop` | `x`, `y`, `width`, `height` (the rectangle actually cut out), `clamped` |
| `resize` | `width`, `height`, `filter` |
| `fit` | `width`, `height` (the result), `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled` |
| `rotate` by a multiple of 90 | `degrees` (a whole number: 0, 90, 180, or 270), `width`, `height` |
| `rotate` by another angle | `degrees` (the normalized angle), `fill` (`#RRGGBB` or `#RRGGBBAA`), `filter` (`nearest` or `bilinear`), `width`, `height` |
| `flip` | `axis` (`horizontal`, `vertical`, or `both`), `width`, `height` |
| `pad` | `left`, `top`, `right`, `bottom`, `color` (`#RRGGBB`, or `#RRGGBBAA` when not opaque), `width`, `height` |
| `grayscale`, `invert` | `width`, `height` |
| `threshold` | `auto`, `level` (the level used), `width`, `height` |
| `levels` | `black`, `white`, `gamma`, `width`, `height` |
| `brightness`, `contrast` | `amount`, `width`, `height` |
| `auto-levels` | `clip`, then `black`, `white` and `applied: true`, or only `applied: false` for a flat image, then `width`, `height` |
| `deskew` | `skewDegrees`, `rotateDegrees`, `confidence`, `applied`, then `reason` when a size limit skips the correction, then (only when applied) `fill`, `filter`, then `width`, `height` |
| `trim` | `tolerance`, `x`, `y`, `width`, `height` (the box kept), `trimmed`, `empty` |
| `grid` | `spacing`, `color`, `verticalLines`, `horizontalLines`, `labels`, `width`, `height` |
<!-- markdownlint-enable MD013 -->

**Crop**: a percentage is of the size of the image at that point of the pipeline. The four edges (`x`, `y`, `x + w`,
`y + h`) are each rounded to the nearest pixel, half away from zero, so two percentage halves tile the image. A
rectangle whose left or top edge is at or beyond the image is a `usage-error` naming the image size; one that extends
past the right or bottom edge is cut off there (`clamped` is `true`); one that rounds to less than a pixel is a
`usage-error`. **Resize** calls `Surface.Resize` with the filter. **Fit** shrinks with `Surface.ResizeToFit` only when
the image is larger than the box on some side or `AllowUpscale` is set (the core method also enlarges); otherwise it is
a copy and `scaled` is `false`.

**Rotate**: the angle is a right angle when it is within 1e-9 degrees of a multiple of 90. A right angle is an exact
pixel move: (x, y) goes to (height - 1 - y, x) for 90 degrees (the top row becomes the right column), to
(width - 1 - x, height - 1 - y) for 180, and to (y, width - 1 - x) for 270, with the size swapped for a quarter turn;
0 (or a full turn) is a copy. Any other angle is `Surface.RotateArbitrary` onto the expanded canvas
(the pipeline's `CheckRotatedSize` refuses a canvas above 8192 on a side, found with `Surface.RotatedSize`, as `size-limit`
before anything is allocated, with the hint to shrink first; the core method would refuse it with an
`ArgumentException`), with the nearest kernel for `--filter nearest` and the bilinear kernel for every other
value. **The fill** is the fill of the operation (`--rotate-fill`), else the background the user gave, else opaque white
when every pixel of the surface being rotated is opaque (a photograph, a scan, a flattened drawing: the corners look
like the paper and survive a viewer that paints transparency black), else transparent. This differs on purpose from
`--pad`, whose raster default stays transparent: a pad adds a border that was asked for, a rotation exposes corners
that nobody asked for.

**Deskew**: `Surface.EstimateSkew` is run on the surface. The skew is corrected only when the estimate is correctable
(`SkewEstimate.IsCorrectable`: it was estimated, its size is at least `MinCorrectedDegrees`, 0.05 degrees, and its
confidence is at least `MinConfidence`, 0.4, the value pinned by the design of `SurfaceGeometry`; very wide images with
random content can approach it), and the expanded canvas fits the maximum dimension. Otherwise the surface is copied
unchanged and `applied` is `false`;
when the size limit is the reason, the operation also reports `reason: "rotated image would exceed the maximum size"`.
This automatic skip is non-fatal, unlike an explicit `--rotate` request. When it is corrected the surface is rotated by
the negative of the skew (`rotateDegrees = -skewDegrees`, a signed clockwise angle; the angle given to the rotation is
`SkewEstimate.CorrectionDegrees`, which is that angle normalized to 0 up
to 360) with the rules of rotate, so the report gives the skew that was found (positive when the
lines descend to the right), the correction, the confidence, and the fill and filter. Text mode prints a warning for
the size-limit skip; the numbers are in the `--json` result.

**Trim**: `Surface.FindTrimBounds` with the tolerance; the surface is cropped to the box (a copy of the box, not of
the whole image). An image that is all background is copied unchanged and reported with the whole image as the box,
`trimmed: false` and `empty: true`; an image whose content touches every edge is also unchanged, with
`trimmed: false` and `empty: false`.

**The adjustments**: grayscale and invert call the core `Surface` methods directly; `threshold` computes
`Surface.OtsuThreshold(surface.LumaHistogram())` first for `auto` and reports the level used; `levels`, `brightness`,
and `contrast` call `Surface.Levels`, `Brightness`, and `Contrast` (a chain of them is a chain of passes in the order
written); `auto-levels` is `Surface.AutoLevels(clip)` and reports the points it applied, or that the image was flat.
Each runs in place on a surface the pipeline owns (a `Surface.Clone` when the surface is the caller's input). **Grid**
is `GridOverlay.Draw` with the color (default `GridOverlay.DefaultColor`); the coordinates are those of the image at
that point of the pipeline, so `--grid` last gives the final coordinates.

**Pad** checks the size first, so that a result larger than `Surface.MaxDimension` on a side is `size-limit` (with the
size and the hint) before anything is allocated, then calls `Surface.Pad` with the color (a fully transparent color is
not filled, because a new surface already is; pixels are stored with straight alpha, so a color is written as given).
**Flip** is `Surface.FlipHorizontal`, `FlipVertical`, or `Rotate180` (both axes); a rotation by 180 degrees is
`Rotate180` too, while the quarter turns stay in the pipeline.

**MapAnchors(anchors, width, height, applied)** (`ImagePipeline.Anchors.cs`, a partial of the class): follows the
pixel positions of the anchors of a model through the operations that were applied, from what each
`AppliedOperation` reported, so the report stays the single contract between the steps and the mapping. For each
anchor with a position, step by step, the current width and height are those the step reported, and the position
(continuous, pixel `i` spanning `[i, i + 1)`) changes as follows:

<!-- markdownlint-disable MD013 -->
| Step | New position |
| ---- | ------------ |
| `crop`, `trim` | `(x - x0, y - y0)` with the reported `x` and `y` of the box |
| `resize`, `fit` | `(x * newWidth / width, y * newHeight / height)` |
| `pad` | `(x + left, y + top)` |
| `flip` | `horizontal`: `(width - x, y)`; `vertical`: `(x, height - y)`; `both`: `(width - x, height - y)` |
| `rotate` | clockwise by the reported `degrees`: `u = x - width / 2`, `v = y - height / 2`, then `(u cos a - v sin a + newWidth / 2, u sin a + v cos a + newHeight / 2)`, the inverse of the sampling of `Surface.RotateArbitrary`; a right angle uses the exact sine and cosine, so 90 gives `(height - y, x)` with no rounding noise |
| `grayscale`, `invert`, `threshold`, `levels`, `brightness`, `contrast`, `auto-levels`, `grid`, a `deskew` that did not correct | unchanged (the size does not change) |
| a `deskew` that corrected (`applied: true`), and any other step whose reported size differs from the current size | the position is lost: `null` |
<!-- markdownlint-enable MD013 -->

A lost position stays lost, so a step the mapping does not know (a future geometry step) gives no position rather
than a wrong one. `inside` is not computed here: it depends on the size of the written image, so
`RenderCommand` of the tool computes it. The method does not modify the list it is given.

#### Error Handling

`ViewerException` with `usage-error` (an operation value out of range, not finite, or not a member of its enumeration,
found before the first step runs; a crop outside the image or empty) and `size-limit` (a size, box, or border above the
maximum, found before the first step runs; a border or an explicitly requested rotated canvas that would make the image
larger than the maximum). An oversized automatic deskew is copied unchanged and reported as a non-fatal operation
reason. A null argument, or a null element of the list of operations, throws `ArgumentNullException`. Any other
exception is a defect and propagates. The errors in the text of a
command line (a malformed value, a setting without its operation, an unknown verb) are reported by the tool's parser
as the same `usage-error`.

#### Dependencies

- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **SourceLoader** - the `AppliedOperation` record, `Operation` helper, and `FormatColor`.
- **GridOverlay** - the grid.
- **CanvasNet core library** - `Surface` (the flip, pad, trim, tone adjustments, rotation, and skew estimation, `Clone`,
  and `CopyFrom`), `SkewEstimate`, `Rgba32`, and `ResamplingMode`.

#### Callers

- **ImageRenderer** - applies the operations between the load and the write of every page, passing the background the
  user gave; maps the anchors of a model through the applied steps with `MapAnchors`.
- **ImageInspector** - applies the same pipeline before it analyzes the image.
- **SourceLoader** - uses `FilterName` to report the filter of a raster resize.
- **RenderCommand** (of the tool) - its `ImagePipelineParser` builds the operation records from the command line.
- **Library users** - call `Apply` on a surface of their own.
