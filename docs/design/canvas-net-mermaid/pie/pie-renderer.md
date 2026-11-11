### PieRenderer

![MermaidPie Structure](MermaidPieView.svg)

The `PieRenderer` unit is the second software unit in the `Pie` subsystem. It comprises two source files in the
`DemaConsulting.CanvasNet.Mermaid.Pie` namespace: `PieRenderer.cs` (the drawing of Mermaid's pie renderer: the outer
circle, the slices, the percentage labels, the title, the legend, and the view box, with the texts as the browser
measures and shows them) and `PieRenderer.Arcs.cs` (the readonly record struct `PieArc` and the ports of d3-shape's
`pie()` and `arc()` with its `centroid`, of the arc of d3-path's `Path` with 3 digits, and of JavaScript's
`toFixed(0)`). `PieRenderer` is an `internal sealed partial` class, so the two files are one type. Both files carry the
provenance header comment for the parts of Mermaid and d3 they port.

#### Purpose

`PieRenderer` draws one render of a `PieDiagram` into a scene as the `draw` of Mermaid's `pieRenderer.ts` draws the
chart, with the paint of the pie style sheet of the render's theme. It exists so that the geometry, the texts, the
colors, and the size of a pie chart are Mermaid's for the same text, configuration, and theme, and so that each
function of d3 and JavaScript that decides a coordinate or a text can be compared with its original on its own.

The unit's behavior is specified by the `CanvasNetMermaid-Pie-PieRenderer-*` requirements in
`docs/reqstream/canvas-net-mermaid/pie/pie-renderer.yaml`.

#### Data Model

**The constants of Mermaid's renderer**: the margin around the pie (`MARGIN`, 40), the size of a legend square
(`LEGEND_RECT_SIZE`, 18), the space after it (`LEGEND_SPACING`, 4), and the height of the chart and the width of the
pie (450); the radius of the pie is half of 450 less the margin, 185. **The constants of d3**: the epsilon of
d3-shape (`1e-12`), the epsilon of d3-path (`1e-6`), and a full turn (τ).

**A renderer instance** draws one render on one thread and holds the diagram, the `DiagramRenderContext` of the render,
the `PieConfig` section of its configuration (`textPosition`, `donutHole`, `legendPosition`, and `highlightSlice`), the
`PieStyles` built from its configuration (reporting the problems of `themeCSS` to its sink), a `LayoutWorkMeter` of
`MaxLayoutWork` located at the diagram keyword and bound to the cancellation token of the render, and a `LabelBuilder`
over the fonts of the render that charges that meter.

**`PieArc`** is an arc of the pie: the start and end angles that d3's `pie()` gives a value, in radians clockwise from
twelve o'clock.

**`ArcPathBuilder`** (nested) is d3-path's `Path` for the commands of an arc: the unrounded start and current point of
the subpath, and the path text and the segments with the numbers rounded to 3 digits as `appendRound` writes them, the
segments being what the scene draws.

**The scene** has one group at the center of the pie (225, 225) with the role `pie`, holding, in order, the group of the
chart (`pie-chart`, moved by a top or left legend) with the outer circle (`pie-outer-circle`), the slices
(`pie-slice`), and the groups of the percentage labels (`pie-slice-label`); the title text (`title`); and a group for
each legend entry (`pie-legend`) with its square (`pie-legend-rect`) and its text (`pie-legend-text`). The slices, the
labels, and the legend items carry the section label as their id and the location of their section.

#### Key Methods

- **`Draw()`** follows Mermaid's `draw`:
  - It charges the meter for each section, reads the outer stroke width from the theme variable
    `pieOuterStrokeWidth` as `parseFontSize` reads it (2 when it has no number), takes the hole as `donutHole` when it
    is above 0 and at most 0.9 (else 0), and draws the outer circle at the radius plus half the outer stroke width with
    the paint of `.pieOuterCircle`.
  - It keeps the sections whose value is at least 1% of the sum of all values (`createPieArcs`), cuts their arcs with
    `Pie`, and keeps those whose percentage `toFixed(0)` is not `0`; each becomes a slice (`Slice`) and a label of its
    percentage `toFixed(0)` and `%` at the `Centroid` of the arc whose inner and outer radius are `textPosition` times
    the radius, in the font and color of `.slice`.
  - It draws the title of the diagram, or the sanitized frontmatter title when the diagram has none, centered at
    (0, -200) from the pie's center, in the font and color of `.pieTitleText`.
  - It makes the text of every section's legend entry (the label, with `showData` followed by the value in brackets)
    at (22, 14) in its group, in the font and color of `.legend text`, and keeps the widest of their measured widths
    (minus infinity when there are no sections).
  - It places the legend entries 22 apart: at the right by default (x 216, the entries centered vertically on the
    pie), widening the chart by 22 and the widest text; at the left (x -207, centered vertically), widening it the same
    way and moving the pie right by the widest text and 22; at the top (from y -185), growing the height by 22 for each
    entry and moving the pie down by 22 more than that; at the bottom (from y 207), growing the height the same way; or
    at the center (centered on the pie). The x of the top, bottom, and center legends centers the widest text. A group
    whose offset is not a number is drawn without its offset, as the browser ignores an invalid transform.
  - Each legend square is 18 by 18, filled with the color of its section (black when the color is not a color) and
    stroked with it at a width of 1.
  - The view box spans from the smaller of 0 and the left of the title (225 less half the title's width) to the larger
    of the chart's width (490, with the legend at the right or left 22 and the widest text more) and the right of the
    title, and from 0 to the chart's height (450, more with a top or bottom legend).
- **`Slice`** draws a slice with the outline of `ArcPath` from the hole to the radius, colored by `ColorOf`, with the
  paint that `PieStyles.Slice` resolves for it: the highlighted slice is the one whose label is `highlightSlice` (never
  for `hover`), and the scale of its paint (1.05 by default) scales the outline around the pie's center and its stroke
  width.
- **`ColorOf`** gives the color of a section as d3's `scaleOrdinal` over the theme's `pie1` to `pie12` and every label
  does: the variable of the index of the section's label, modulo 12, or null when the variable is not a color.
- **`TextLine`** makes one text: it measures the text as the browser measures it when Mermaid draws it (Mermaid's
  entity placeholders not yet decoded, white space collapsed), which gives the width the legend is placed by, and draws
  it as `DisplayText` shows it, with the box of the browser's `getBBox` from the `LabelBuilder`, anchored at its start
  for a legend text and at its middle otherwise; the meter is charged for the length of both texts.
- **`DisplayText`** gives the text the browser shows: each of Mermaid's entity placeholders decoded into its character,
  everything else, a reference the author wrote included, shown as written, and the white space collapsed as in all SVG
  text.
- **`Pie(values)`** is d3-shape's `pie()` with `sort(null)`, the start angle 0, the end angle τ, and no padding: the arcs
  of the values in their order, each the share of a full turn that the value has of the sum of the positive values, a
  value that is not positive taking no angle, and every arc empty when the sum is 0.
- **`ArcPath(innerRadius, outerRadius, arc)`** is d3-shape's `arc()` without corners or padding, written by d3-path with
  3 digits: the angles turned by a quarter so that 0 is at twelve o'clock, the larger radius used as the outer one, and
  a point (an outer radius of at most the epsilon), a circle or an annulus (an arc of a full turn), or a circular or
  annular sector. It returns the path text (for example `M0,-185A185,185,0,0,1,185,0L0,0Z` for a quarter) and its
  segments. `ArcPathBuilder.Arc` draws an arc of d3-path: a line to its start when the current point is elsewhere, then
  one elliptical arc with the large-arc flag of an angle of π or more, or two half circles for a whole circle.
- **`Centroid(innerRadius, outerRadius, arc)`** is d3-shape's `arc.centroid`: the point at the middle radius and the
  middle angle.
- **`ToFixed0(value)`** is JavaScript's `toFixed(0)`: the integer nearest to the exact value of the number, a tie away
  from zero, a negative number that rounds to zero written `-0`; `NaN`, the infinities, and magnitudes of `1e21` or more
  as `String` writes them.

#### Error Handling

- The layout work meter of the render throws a `MermaidRenderException` with code `mermaid-limit-exceeded`, located at
  the diagram keyword, when the work of the sections and the measured texts exceeds `MaxLayoutWork`, and an
  `OperationCanceledException` when the render is canceled; both propagate.
- `PieStyles.Create` throws `MermaidUnsupportedFeatureException` through the sink at the first unsupported `themeCSS`
  rule in strict mode; it propagates.
- No other error can occur: a value that is not finite, a zero sum, a chart without sections, and a theme variable
  that is not a color are drawn as Mermaid draws them (no slice for a zero sum, the left half of the chart as the view
  box when there are no sections, an offset that is not a number ignored, and black for a color that is not one).

#### Dependencies

- **PieParser** - `PieDiagram` (the sections, `ShowData`, the title, the keyword location, and `Sanitize` for the
  frontmatter title) and `PieSection` (the label, the value, the location, and the legend text).
- **Configuration subsystem** - `MermaidConfig` (the `Pie` section `PieConfig` and the theme variables), `PieStyles`
  with `SlicePaint`, `ShapePaint`, and `TextPaint` (the pie style sheet), `ConfigScalar`, and
  `StyleDeclarations.TryParsePaint` (a theme color).
- **Rendering subsystem** - `LabelBuilder` (the browser's text extents and vertical metrics, `ParseFontSize`,
  `CollapseSvgWhiteSpace`, and `DecodeEntities`) and `CurvePath.Round` (the 3 digits of d3-path).
- **Syntax subsystem** - `HtmlSanitizer.DecodeReferences` (the character references of a placeholder) and
  `YamlSubsetParser.FormatJsNumber` (JavaScript's `String(number)`).
- **Dagre subsystem** - `LayoutWorkMeter`, the work budget and cancellation of a render.
- **Scenegraph subsystem** - `Scene`, `SceneGroup`, `ScenePath`, `SceneSegment`, `SceneText`, `SceneTextRun`,
  `SceneFont`, `SceneRect`, and `SceneStroke`, the drawing.
- **Core `CanvasNet`** - `Rgba32` (the `Canvas` subsystem), through the public API.
- **System** - `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, and `MermaidRenderException`.

#### Callers

- **PieParser** - `PieDiagram.Layout` creates a renderer for each render and returns the scene of `Draw`.
- The unit tests call `Pie`, `ArcPath`, `Centroid`, `ToFixed0`, and `DisplayText` directly, and the unit and subsystem
  tests lay out whole texts.

#### Known Differences From Mermaid

- **Opacity folded into the colors.** The element opacity of a slice (`pieOpacity`, 0.7 by default) is folded into the
  alpha of its fill and stroke, so where the stroke of a slice overlaps its fill the pixels differ slightly from the
  browser's compositing of the whole element.
- **The highlighted slice.** The CSS `scale` of the highlighted slice is applied to its outline and its stroke width,
  and is read only as one number or one percentage (any other value draws the slice unscaled); a `highlightSlice` of
  `hover` highlights nothing, since a picture has no pointer.
- **No missing-glyph reports.** The texts of a pie chart are drawn with the fonts of the render but give no
  `mermaid-text-missing-glyph` report, as the direct texts of the sequence diagram give none.
- **Text metrics.** The widths and boxes of the texts are those of the `LabelBuilder`, which emulates the browser's SVG
  text measurement with the fonts of the render; the reference layout tests hold them to Mermaid's goldens.
