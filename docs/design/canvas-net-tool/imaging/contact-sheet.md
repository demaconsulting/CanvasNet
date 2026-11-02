### ContactSheet

<!-- cspell:ignore canvasnet ellipsis luma -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`ContactSheet` composes a contact sheet: a grid of images, each fitted into a cell of the same size, with optional
labels. Its single responsibility is the geometry and the pixels of the sheet; it knows nothing of files, command-line
options, or how an image is loaded, so the command decides what is added. The unit consists of the class `ContactSheet`
and the records `SheetOptions` and `SheetLayout`.

#### Data Model

**SheetOptions** (record): `Columns` (0 for automatic), `CellWidth`, `CellHeight` (the image area of a cell),
`Spacing` (between cells), `Padding` (around the sheet), `Background` (opaque), `Labels`, `Fit` (`FitMode`), and
`Filter` (`ResamplingMode`).

**SheetLayout** (record struct): `Count`, `Columns`, `Rows`, `CellWidth`, `CellHeight`, `LabelHeight`, `Spacing`,
`Padding`, `Width`, and `Height`; `CellOrigin(index)` gives the top-left corner of a cell's image area.

The constants are `LabelHeight` (22), `LabelFontSize` (14), `MinCell` (16), and `MaxColumns` (64).

#### Key Methods

**CalculateLayout(count, options)**: pure arithmetic, done in 64-bit integers. The columns are `options.Columns`, or when
that is 0 the ceiling of the square root of the count (the smallest `c` with `c * c >= count`, at most 64). Then
`rows = ceil(count / columns)`, `LabelHeight = labels ? 22 : 0`,

```text
width  = 2 * padding + columns * cellWidth + (columns - 1) * spacing
height = 2 * padding + rows * (cellHeight + LabelHeight) + (rows - 1) * spacing
```

A sheet larger than `Surface.MaxDimension` (8192) on a side throws `size-limit` (hint: a smaller `--cell`, more
`--columns`, or less spacing and padding) - from the numbers alone, so a command can refuse before loading any input.
Cell `i` is in column `i % columns` and row `i / columns`, with the origin
`(padding + column * (cellWidth + spacing), padding + row * (cellHeight + LabelHeight + spacing))`; a cell index outside
`0..count-1` is an `ArgumentOutOfRangeException`. A number of columns larger than the count simply leaves cells empty.

**ContactSheet(count, options)**: calculates the layout, allocates the sheet, and clears it to the background.

**Add(image, label)**: fits the next image into its cell with the core `Surface.FitInto` (`Contain`: scaled to fill the
limiting side, enlarging a small image, centered on the background; `Stretch`: resampled to exactly the cell), flattens
any transparency over the background (so the sheet is always opaque), copies the rows onto the sheet, and, when the sheet
has labels and the label is not `null`, draws it. The image is not disposed. It returns the cell origin. Adding more
images than the count is an `InvalidOperationException`. The command loads an image, adds it, and disposes it before the
next, so the memory in use is the sheet plus one image.

**Label drawing**: the font is `SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false,
italic: false)` - the bundled Liberation Sans, never a system font, so labels are the same on every machine. The text is
cut with `FitLabel`, measured with `TextRenderer.MeasureText` at 14 pixels, and drawn with `Canvas.DrawText` centered on
the cell's center, with the baseline at `stripTop + (22 - (ascent + descent)) / 2 + ascent`, where `stripTop` is the
bottom of the cell image. The color is white when the luma of the background (`(77 R + 150 G + 29 B + 128) >> 8`) is
below 128, otherwise black.

**FitLabel(text, maxWidth, font, size)** (internal): the text when it is not wider than `maxWidth`; otherwise the longest
beginning (by whole characters, so a surrogate pair is never split) followed by the ASCII text `...` that is not wider
than `maxWidth`; `...` alone when none fits. It delegates the cut to core `TextRenderer.TruncateToWidth`, passing `...`
explicitly (the shared default ellipsis is the single character U+2026), and adds only the label rule that a cut label
shows at least the ellipsis, because the shared method returns an empty string when nothing fits. Empty text stays empty
for any width the empty text fits.

**Detach()**: hands the sheet to the caller, who owns and disposes it; it can be taken once. **Dispose()** disposes the
sheet if it was not handed out.

#### Error Handling

A sheet too large is a `ToolException` with code `size-limit`, an expected error. Everything else is a defect of the
caller, since the command validates user input first: a null argument throws `ArgumentNullException`; a count below 1,
columns outside 0 to 64, a cell side below 16, negative spacing or padding, or an undefined fit mode or filter throw
`ArgumentOutOfRangeException`; a background that is not opaque throws `ArgumentException`; too many images or use after
`Detach` throws `InvalidOperationException`.

#### Dependencies

- **CanvasNet core library** - `Surface.FitInto` and `FitMode`, `Surface.FlattenOver`, `Surface.HasTransparency`, and
  `Surface.CopyFrom` (the same `Surface` and `Rgba32` as everywhere else).
- **ToolException** and **ErrorCodes** - the `size-limit` error.
- **CanvasNet core library** - `Surface`, `Rgba32`, and `ResamplingMode`.
- **CanvasNet rendering and fonts** - `Canvas`, `TextRenderer` (`MeasureText`, `TruncateToWidth`), `TextAlign`,
  `SystemFontCatalog.LoadBundledFallback`, and `TrueTypeFont`; only the bundled font is used.

#### Callers

- **MontageCommand** - calculates the layout before loading anything, then adds each loaded cell image and detaches the
  sheet to write it.
