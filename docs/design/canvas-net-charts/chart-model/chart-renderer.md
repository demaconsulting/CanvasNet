### ChartRenderer

The `ChartRenderer` unit is the second software unit in the `ChartModel` subsystem. It comprises
three public types, all in the `DemaConsulting.CanvasNet.Charts` namespace:

- `ChartRenderer` (static class): the public rendering entry point, exposing two `Render`
  overloads and dispatching per `ChartType` to the appropriate painter
- `ChartRenderOptions` (sealed class): an immutable options bag for `ChartRenderer.Render` — a
  background color, an optional font override, per-element default font sizes, and an optional
  color-palette override — with a `Default` singleton
- `ChartColorPalette` (static class): a fixed, documented 10-entry categorical color palette,
  exposed as `ChartColorPalette.Default`

plus a private, file-partitioned implementation spread across `ChartRenderer.Layout.cs`,
`ChartRenderer.CategoryChart.cs`, `ChartRenderer.PieChart.cs`, `ChartRenderer.Legend.cs`, and
`ChartRenderer.Title.cs` (all `partial` continuations of the one public `ChartRenderer` class).

#### Purpose

`ChartRenderer` paints a validated `ChartDocument` unit's `Chart` onto a new core `CanvasNet`
`Canvas.Surface`, producing a deterministic, legible rendering with sensible defaults for every
optional `Chart`/`ChartRenderOptions` setting a caller omits. It is implemented entirely against
the core `CanvasNet` system's own public rendering primitives (`Surface`, `PathBuilder`,
`TrueTypeFont`/text layout, `Rgba32`, `Matrix3x2`/`RectangleF`), with no third-party dependency
and no reference to any other `DemaConsulting.CanvasNet.*` document-format package.

`ChartRenderer.Render(Chart, int, int, ChartRenderOptions?)` is the primary entry point: it
constructs a `Surface` of the requested pixel dimensions, fills it with
`ChartRenderOptions.BackgroundColor`, computes a title/legend/plot-area layout, then dispatches
per `Chart.Type`:

- `Bar`/`Column`/`Line`/`Area` → the shared category-chart painter (`ChartRenderer.
  CategoryChart.cs`), which also draws category/value axes and opt-in data labels
- `Pie`/`Doughnut` → the wedge painter (`ChartRenderer.PieChart.cs`), via `PathBuilder.ArcTo`

`ChartRenderer.Render(Chart, float, float, float, ChartRenderOptions?)` is a convenience overload
that computes pixel dimensions as `round(widthInches * dpi)`/`round(heightInches * dpi)` and
delegates to the primary overload. Unlike `PdfDocument.Render(int, float, PdfRenderOptions?)`,
which scales a page's own intrinsic physical size, a `Chart` carries no intrinsic physical
size — this overload exists purely so a caller who prefers to reason in physical units/DPI (for
example, to match a target print or slide size) need not compute the pixel dimensions by hand;
its behavior is otherwise identical to the primary overload once the pixel dimensions are known.

Both overloads validate eagerly, before constructing anything:

- `chart` is `null` → `ArgumentNullException`
- The requested (or DPI-computed) pixel width/height is not positive, or exceeds
  `Surface.MaxDimension` → `ArgumentOutOfRangeException`
- The DPI overload's `widthInches`/`heightInches`/`dpi` is not finite or not positive →
  `ArgumentOutOfRangeException`

Every other rendering decision degrades gracefully rather than throwing:

- A render target too small to reserve a title/legend/axis band simply skips that band (see
  `ChartRenderer.Layout.cs`), rather than throwing or painting a corrupted layout
- A legend with more entries than fit its reserved band omits the entries that do not fit,
  rather than overlapping the plot area or throwing
- A category/legend label too long to fit its available width is truncated, rather than
  overflowing its region or throwing
- A single-value `Pie`/`Doughnut` series, or a series whose total is zero, paints a well-defined
  degenerate full-circle (or omits wedges) rather than dividing by zero

#### Design

- `ChartRenderer.cs` — the public `Render` overloads, argument validation, background fill, and
  per-`ChartType` dispatch
- `ChartRenderer.Layout.cs` — internal layout computation: reserves a title band (when
  `Chart.Title` is set and the render target is tall enough), a legend band (when
  `Chart.Legend`/`ChartRenderOptions` requests a visible legend and the render target has room,
  sized per `ChartLegendPosition`), and the remaining plot-area rectangle
- `ChartRenderer.CategoryChart.cs` — the shared `Bar`/`Column`/`Line`/`Area` painter: value-range
  resolution (from `ChartAxis.Minimum`/`Maximum`, or the data's own min/max with zero included),
  axis gridline/tick-label painting, per-series/per-point bar/segment painting, and opt-in
  data-label painting
- `ChartRenderer.PieChart.cs` — the `Pie`/`Doughnut` wedge painter, converting each value's
  proportional share into a start/sweep angle pair and drawing each wedge via
  `PathBuilder.ArcTo`, with a documented degenerate-case rule (a single non-zero value, or a
  zero-total series, paints the single remaining non-zero wedge as a full circle instead of
  computing an empty 0° sweep)
- `ChartRenderer.Legend.cs` — the legend painter: a color swatch and name label per series/point
  (series-level for category charts, point-level for `Pie`/`Doughnut`), laid out per
  `ChartLegendPosition`, truncating or omitting entries that do not fit
- `ChartRenderer.Title.cs` — the title painter: centers `Chart.Title.Text` in the reserved title
  band at `Chart.Title.FontSize` or `ChartRenderOptions.TitleFontSize`

Every file's painting logic resolves series/point color via a single shared precedence: an
explicit `ChartSeries.Color`/`PointColors` entry, else `Chart.ColorPalette`, else
`ChartRenderOptions.ColorPalette`, else `ChartColorPalette.Default` — each resolved by index with
modulo wraparound, so a chart with more series/points than palette entries still renders every
one with a distinct (repeating) color rather than throwing or leaving a gap. Text is measured and
drawn via `ChartRenderOptions.Font`, falling back to the bundled Liberation Sans Regular font
(the same fallback font the core `CanvasNet` `Rendering` subsystem already bundles) when omitted.

`ChartColorPalette.Default` is a fixed 10-entry array of `Rgba32` values (a standard qualitative
categorical palette), documented inline with its exact RGB values and the fallback-by-index
(modulo) resolution rule described above. `ChartRenderOptions` is a `sealed class` with
`init`-only properties and a `public static readonly ChartRenderOptions Default` singleton,
mirroring `PptxRenderOptions`'s established shape so a caller already familiar with
`CanvasNetPptx`'s rendering options recognizes the same pattern immediately.

The `ChartModel` subsystem's dependencies gained by this unit (`Canvas.Surface`, `Drawing`,
`Geometry`, `Fonts`, and `Rendering`) are documented in _ChartModel Subsystem Design_
(`../chart-model.md`)'s Dependencies section.
