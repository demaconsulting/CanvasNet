## ChartModel

![ChartModel Structure](ChartModelView.svg)

The `ChartModel` subsystem is the first (and, as of this release, only) software subsystem in
`CanvasNetCharts`. It groups two flat, hand-rolled units: `ChartDocument`, containing the public,
immutable, validating chart data model and the `ChartBuilder` fluent construction API; and
`ChartRenderer`, a pixel-rendering engine that paints a `Chart` onto a core
`CanvasNet.Canvas.Surface`.

### Purpose

The `ChartModel` subsystem provides both the in-memory representation of "what chart to draw"
(`ChartDocument`) and, as of Phase 2, "how to draw it" (`ChartRenderer`) — the complete set of
chart functionality a future OOXML `chart1.xml` parser (Phase 3) and `CanvasNetPptx` integration
(Phase 4) will consume. `ChartDocument` owns the complete set of data-shape validation rules —
non-empty series, finite numeric values, count-matched point colors/labels, consistent
series/category-axis lengths, and well-ordered axis ranges — so that both `ChartRenderer` and a
future OOXML parser can trust every `Chart` instance they receive without re-validating it
themselves. `ChartRenderer` owns every pixel-rendering decision — layout, default styling,
color-palette resolution, and graceful degradation for extreme render-target sizes or legend
overflow — so that a future OOXML parser and `CanvasNetPptx` integration need not duplicate any
of that logic.

### Units

- **ChartDocument** — the `Chart`/`ChartSeries`/`ChartAxis`/`ChartLegend`/`ChartTitle`/
  `ChartType` model types and the `ChartBuilder` fluent construction API; see
  _ChartDocument Unit Design_ (`chart-model/chart-document.md`)
- **ChartRenderer** — the `ChartRenderer`/`ChartRenderOptions`/`ChartColorPalette` pixel-rendering
  engine; see _ChartRenderer Unit Design_ (`chart-model/chart-renderer.md`)

### Dependencies

The `ChartModel` subsystem depends on the core `CanvasNet` system's `Canvas` subsystem (its
`Rgba32` unit, for series/point/palette color, and, for `ChartRenderer` only, its `Surface` unit,
the pixel buffer it paints onto and returns), and, for `ChartRenderer` only, the `Drawing`,
`Geometry`, and `Fonts` subsystems (path filling/stroking, transforms/rectangles, and TrueType
text layout/metrics, respectively) and the `Rendering` subsystem (the bundled Liberation Sans
fallback font) — see _CanvasNetCharts System Design_ (`../canvas-net-charts.md`)'s Dependencies
section. Beyond this, `ChartDocument` uses only the .NET base class library
(`System.Collections.ObjectModel`'s `ReadOnlyCollection<T>`, for defensive-copy immutable
snapshots), available on every one of `CanvasNetCharts`'s target frameworks with no new runtime
NuGet dependency.

### Callers

`ChartDocument` and `ChartRenderer` are each a public API entry point, invoked directly by
consumers of the `DemaConsulting.CanvasNet.Charts` package. `ChartRenderer` additionally calls
`ChartDocument`'s public types as its own input (a caller-supplied `Chart` instance and its
constituent `ChartSeries`/`ChartAxis`/`ChartLegend`/`ChartTitle`/`ChartType` members) — this is
the one intra-subsystem unit-to-unit call `ChartModel` contains as of this release. No unit
within the `ChartModel` subsystem is called by any other subsystem as of this release (there is
no other subsystem yet).
