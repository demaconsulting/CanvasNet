## ChartModel

![ChartModel Structure](ChartModelView.svg)

The `ChartModel` subsystem is the first (and, as of this release, only) software subsystem in
`CanvasNetCharts`. It groups a single flat, hand-rolled unit, `ChartDocument`, containing the
public, immutable, validating chart data model and the `ChartBuilder` fluent construction API.

### Purpose

The `ChartModel` subsystem provides the in-memory representation of "what chart to draw" that
every later phase of `CanvasNetCharts` (a renderer, an OOXML chart1.xml parser) will consume.
It owns the complete set of data-shape validation rules — non-empty series, finite numeric
values, count-matched point colors/labels, consistent series/category-axis lengths, and
well-ordered axis ranges — so that both a future renderer and a future OOXML parser can trust
every `Chart` instance they receive without re-validating it themselves.

### Units

- **ChartDocument** — the `Chart`/`ChartSeries`/`ChartAxis`/`ChartLegend`/`ChartTitle`/
  `ChartType` model types and the `ChartBuilder` fluent construction API; see
  _ChartDocument Unit Design_ (`chart-model/chart-document.md`)

### Dependencies

The `ChartModel` subsystem depends on the core `CanvasNet` system's `Canvas` subsystem (its
`Rgba32` unit, for series/point/palette color) — see _CanvasNetCharts System Design_
(`../canvas-net-charts.md`)'s Dependencies section. Beyond this, `ChartModel`'s one unit uses
only the .NET base class library (`System.Collections.ObjectModel`'s `ReadOnlyCollection<T>`,
for defensive-copy immutable snapshots), available on every one of `CanvasNetCharts`'s target
frameworks with no new runtime NuGet dependency.

### Callers

`ChartDocument` is a public API entry point, invoked directly by consumers of the
`DemaConsulting.CanvasNet.Charts` package. No unit within the `ChartModel` subsystem is called by
any other subsystem as of this release (there is no other subsystem yet), and `ChartModel`
contains exactly one unit, so there is no intra-subsystem unit-to-unit call either.
