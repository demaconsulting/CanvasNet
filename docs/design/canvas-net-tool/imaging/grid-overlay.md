### GridOverlay

<!-- cspell:ignore canvasnet luma -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`GridOverlay` draws a labeled coordinate grid over the pixels of a surface, so that positions and sizes can be read off
the picture. Its single responsibility is the drawing of the lines, their blending, and their labels; it knows nothing of
files, command-line options, or how a surface was loaded, so the pipeline decides when it runs and with which spacing and
color. The unit is the static class `GridOverlay`.

#### Conventions

Surfaces hold **straight** (non-premultiplied) `Rgba32` pixels with the origin at the top left and y down. The grid is
drawn **in place** on a surface the caller owns (the pipeline passes a surface it owns, a copy when the surface is the
caller's input). The blending is exact integer arithmetic. Rows are drawn with `Parallel.For` and write only their own
row; the labels are drawn afterwards, sequentially. Nothing allocates per pixel. The coordinates are those of the surface
at the moment of the call, so a caller that wants to read final coordinates draws the grid last.

#### Key Methods

**Draw(surface, spacing, color)**: the spacing is 8 to 4096 pixels (`MinSpacing`, `MaxSpacing`; otherwise
`ArgumentOutOfRangeException`) and the color is the line color, whose alpha is the opacity of the lines
(`DefaultColor` is `#FF00FFA0`, a translucent magenta). The result is `GridResult(VerticalLines, HorizontalLines, Labels)`.

*Lines.* Vertical 1-pixel lines are at `x = k * spacing` and horizontal 1-pixel lines at `y = k * spacing`, for
`k >= 0` and the position inside the surface; the lines at 0 are the first column and the first row, so there are
`(width - 1) / spacing + 1` vertical and `(height - 1) / spacing + 1` horizontal lines. Each row is processed on its own:
a row that lies on a horizontal line blends every pixel, any other row blends the pixels at the multiples of the spacing,
so a pixel where two lines cross is blended once and the intersections are not darker.

*Blend.* A color `c` with alpha `a` is blended over a pixel `p` with alpha `pa` by source-over on straight alpha. An alpha
of 0 changes nothing and an alpha of 255 replaces the pixel with the opaque color. For an opaque pixel
`out = (c * a + p * (255 - a) + 127) / 255` per channel (integer division) and the alpha stays 255. Otherwise
`outA = (255 * a + pa * (255 - a) + 127) / 255` and the color is
`(c * a * 255 + p * pa * (255 - a) + outA * 255 / 2) / (outA * 255)` (integer division, clamped to 255); when `outA`
is 0 the pixel becomes transparent black `(0, 0, 0, 0)`. Example (pinned by a test): `(200, 100, 0, 100)` over the pixel
`(100, 150, 200, 255)` gives `(139, 130, 122, 255)`; over a transparent pixel it gives itself.

*Labels.* The label of a line is its pixel coordinate in decimal digits, drawn 12 pixels high with the bundled Liberation
Sans font (loaded once per call with `SystemFontCatalog.LoadBundledFallback`, never a system font) through the Rendering
text renderer. With the metrics of the text, the label box is the text width (rounded up) plus 4 pixels wide and the
ascent plus descent (rounded up) plus 2 pixels high. The box is filled by blending the **backing** color at alpha 192: white
when the luma of the line color is below 128 (`ImageAdjustments.Luma` of the opaque color), black otherwise; the text is
drawn in the line color made opaque.

- A vertical-line label sits at the top edge: box left `x + 1`, box top 1, text left `x + 3`, baseline
  `2 + ascent`. It is
  drawn for every `Lv`-th line starting at `k = 0` (the origin, label `0`), where
  `Lv = max(1, ceil((widest vertical label width + 6) / spacing))` and the widest label is the widest text of any vertical
  line of the surface.
- A horizontal-line label sits at the left edge: box left 1, box top `y + 1`, text left 3, baseline
  `y + 2 + ascent`. It is
  drawn for every `Lh`-th line from `k = Lh`, where `Lh = max(1, ceil((box height + 2) / spacing))`, and only when
  `y + 1 >= box height + 3`, so that it is clear of the row of top labels.
- A label whose box does not lie fully inside the surface is skipped and not counted.

The text is not clipped separately: a box that fits contains its text, because the box is at least the width of the text
plus 4 pixels and as high as the ascent plus descent plus 2 pixels.

#### Error Handling

The unit has no expected errors: the pipeline validates user input and reports usage errors, so everything here is a
defect of the caller. A null surface throws `ArgumentNullException`; a spacing outside 8 to 4096 throws
`ArgumentOutOfRangeException`.

#### Dependencies

- **CanvasNet core library** - `Surface` and `Rgba32`.
- **CanvasNet Rendering and Fonts libraries** - the text renderer and the bundled font.
- **ImageAdjustments** - `Luma`, to choose the backing of a label.

#### Callers

- **ImagePipeline** - runs the grid as the `--grid` pipeline operation.
