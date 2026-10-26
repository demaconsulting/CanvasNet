### ImagePipeline

<!-- cspell:ignore canvasnet jpeg lanczos RRGGBBAA -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ImagePipeline` is the ordered image pipeline. Its single responsibility is to turn the transformation options of a
command line into operations and to apply them, left to right, to the pixels of a surface: crop, resize to an exact
size, fit inside a box, rotate by a right angle, flip, and pad. The `render` command and the verb commands `crop`,
`resize`, `rotate`, `flip`, and `pad` share it, so a verb is exactly `render` with one operation placed first. The unit
is the static class `ImagePipeline` (`ImagePipeline.cs`) and the operation types in `ImageOperations.cs`.

#### Data Model

**IImageOperation** (interface): a step of the pipeline, with its `Name` (`crop`, `resize`, `fit`, `rotate`, `flip`, or
`pad`, the `op` of the result). The records that implement it are plain descriptions of what to do:

<!-- markdownlint-disable MD013 -->
| Record | Parameters |
| ------ | ---------- |
| `CropOperation` | `Region` (a `GeometrySpec`: origin and size, each in pixels or percent) |
| `ResizeOperation` | `Width`, `Height`, `Filter` |
| `FitOperation` | `Width`, `Height` (the box), `AllowUpscale`, `Filter` |
| `RotateOperation` | `Degrees` (90, 180, or 270, clockwise) |
| `FlipOperation` | `Axis` (`FlipAxis`: horizontal, vertical, or both) |
| `PadOperation` | `Border` (a `PadSpec`: left, top, right, bottom) and `Color` (`Rgba32?`; `null` for the caller's default) |
<!-- markdownlint-enable MD013 -->

**Options** the unit reads from an `ArgumentReader` whose table declares them: `--crop`, `--resize`, `--fit`,
`--rotate`, `--flip`, and `--pad` are steps (repeatable, in command-line order); `--pad-color`, `--allow-upscale`, and
`--filter` are settings of the whole command line (the last occurrence applies to every step it concerns). The
constants `CropOption` and so on name them, and `VerbNames` lists the verb commands.

**Filters**: `auto`, `nearest`, `bilinear`, `bicubic`, `lanczos3`, and `area`, in any case, mapped to the core library's
`ResamplingMode`; `FilterName` gives the lower-case name used in results.

#### Key Methods

**Parse(reader, verb, verbOperand)**: reads the settings (`--filter`, `--allow-upscale`, `--pad-color`), then, for a
verb command, the operation of its operand first (`crop` with `ParseGeometry`, `resize` with `ParseSize`, `rotate` with
the rotation rule, `flip` with `ParseFlip`, `pad` with `ParsePad`), then every step option in `reader.Ordered` order.
Values are parsed with the `ArgumentReader` value grammars, so a malformed value is a `usage-error` and a size or
border above the maximum is `size-limit`; the verb operands are named `<geometry>`, `<size>`, `<degrees>`, `<axis>`,
and `<border>` in messages. A rotation is parsed as an angle in degrees, normalized to 0 up to 360 (so `-90` is 270),
and must then be exactly 90, 180, or 270; any other angle, including 0 and 360, is a `usage-error` that says the
rotation is not supported yet. `--allow-upscale` without a `--fit` and `--pad-color` without a pad are usage errors.

**Apply(input, operations, defaultPadColor, out applied)**: runs the steps in order. Every step makes a new surface and
the previous intermediate is disposed at once (and also when a step fails); the input is never modified or disposed.
The result is always a surface different from the input - for an empty list a copy - which the caller disposes. Each
step adds an `AppliedOperation` to `applied`:

<!-- markdownlint-disable MD013 -->
| Operation | Fields (in order) |
| --------- | ----------------- |
| `crop` | `x`, `y`, `width`, `height` (the rectangle actually cut out), `clamped` |
| `resize` | `width`, `height`, `filter` |
| `fit` | `width`, `height` (the result), `boxWidth`, `boxHeight`, `filter`, `allowUpscale`, `scaled` |
| `rotate` | `degrees`, `width`, `height` |
| `flip` | `axis` (`horizontal`, `vertical`, or `both`), `width`, `height` |
| `pad` | `left`, `top`, `right`, `bottom`, `color` (`#RRGGBB`, or `#RRGGBBAA` when not opaque), `width`, `height` |
<!-- markdownlint-enable MD013 -->

**Crop**: a percentage is of the size of the image at that point of the pipeline. The four edges (`x`, `y`, `x + w`,
`y + h`) are each rounded to the nearest pixel, half away from zero, so two percentage halves tile the image. A
rectangle whose left or top edge is at or beyond the image is a `usage-error` naming the image size; one that extends
past the right or bottom edge is cut off there (`clamped` is `true`); one that rounds to less than a pixel is a
`usage-error`. **Resize** calls `Surface.Resize` with the filter. **Fit** shrinks with `Surface.ResizeToFit` only when
the image is larger than the box on some side or `AllowUpscale` is set (the core method also enlarges); otherwise it is
a copy and `scaled` is `false`. **Rotate** moves each pixel (x, y) to (height - 1 - y, x) for 90 degrees (the top row
becomes the right column), to (width - 1 - x, height - 1 - y) for 180, and to (y, width - 1 - x) for 270, and swaps the
size for a quarter turn. **Flip** reverses each row, the order of the rows, or both. **Pad** allocates the larger
surface, fills it with the color (not at all for fully transparent, which a new surface already is; pixels are stored
with straight alpha, so a color is written as given), and copies the image rows into place; a result larger than
`Surface.MaxDimension` on a side is `size-limit` before it is allocated.

#### Error Handling

`ToolException` with `usage-error` (malformed value, unsupported rotation, crop outside the image or empty,
a setting without its operation) and `size-limit`. A null argument throws `ArgumentNullException`; an unknown verb
name, or a verb without its operand, is a defect of the caller (`ArgumentException`, `ArgumentNullException`). Any
other exception is a defect and propagates.

#### Dependencies

- **ArgumentReader** - reading the options and the value grammars (`ParseGeometry`, `ParseSize`, `ParseFlip`,
  `ParsePad`, `ParseDegrees`, `ParseColor`). This is the one place where `Imaging` uses a root-namespace unit; it is
  the parsing of the operations, not any command-line output.
- **ToolException** and **ErrorCodes** - the expected-error contract.
- **SourceLoader** - the `AppliedOperation` record, `Operation` helper, and `FormatColor`.
- **CanvasNet core library** - `Surface`, `Rgba32`, and `ResamplingMode`.

#### Callers

- **RenderCommand** - parses the operations while reading its settings (so a mistake is reported before anything is
  decoded) and applies them between the load and the write of every page.
- **SourceLoader** - uses `FilterName` to report the filter of a raster resize.
