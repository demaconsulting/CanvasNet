### OpenXmlChartParser

The `OpenXmlChartParser` unit is the first (and, as of this release, only) software unit in the
`OpenXmlChart` subsystem. It comprises two public types, both in the
`DemaConsulting.CanvasNet.Charts.OpenXml` namespace:

- `OpenXmlChartParser` (static class): `Parse(XDocument)` and `Parse(XElement)` - the unit's one
  entry point
- `ChartUnsupportedFeatureException` (sealed class, derives `IOException`): carries a short,
  stable `Feature` token, mirroring `CanvasNetPptx`'s own `PptxUnsupportedFeatureException` shape
  (five parallel constructors, an `IOException` base) without referencing that type

#### Purpose

`OpenXmlChartParser` lets a caller turn a raw `c:chartSpace` (or bare `c:chart`) XML element into
a validated `Chart`, reading only cached values (`c:numCache`/`c:strCache`) and never
recomputing from the sibling `c:f` formula a spreadsheet application would use. It supports the
five chart kinds `ChartType` models:

| OOXML element | Classified `ChartType` |
| ------------- | ----------------------- |
| `c:barChart` with `c:barDir val="bar"` | `Bar` |
| `c:barChart` with `c:barDir val="col"` or no `c:barDir` | `Column` |
| `c:lineChart` | `Line` |
| `c:pieChart` | `Pie` |
| `c:doughnutChart` | `Doughnut` |
| `c:areaChart` | `Area` |

and throws `ChartUnsupportedFeatureException` - with a dedicated, documented `Feature` token - for
every other schema-legal plot-area shape:

| Plot-area shape | `Feature` token |
| ---------------- | --------------- |
| No recognized chart-type element | `charts-openxml-no-chart-type` |
| More than one recognized chart-type element (a combo chart) | `charts-openxml-combo-chart` |
| `c:radarChart` | `charts-openxml-radar-chart` |
| `c:scatterChart` | `charts-openxml-scatter-chart` |
| `c:bubbleChart` | `charts-openxml-bubble-chart` |
| `c:stockChart` | `charts-openxml-stock-chart` |
| `c:surfaceChart` / `c:surface3DChart` | `charts-openxml-surface-chart` |
| `c:bar3DChart` / `c:line3DChart` / `c:pie3DChart` / `c:area3DChart` | `charts-openxml-3d-chart` |
| `c:ofPieChart` | `charts-openxml-of-pie-chart` |
| A series' `c:val` with no cached `c:numCache` | `charts-openxml-uncached-values` |

Beyond chart-kind classification and the cached-values-only check above, `OpenXmlChartParser`
performs no independent data-shape validation: every other invariant (non-empty series, finite
values, matching category/value counts, a well-ordered value-axis range, and so on) is enforced
exactly once, by `ChartDocument`'s own validating constructors, which this parser ultimately
calls. A malformed cached value (for example, a series/category count mismatch) therefore
surfaces the identical `ArgumentException` a directly constructed `Chart`/`ChartSeries`/
`ChartAxis` would throw, not a parser-specific exception.

#### Extraction Rules

- **Series name** (`c:tx`): a cached `c:strRef`/`c:strCache` entry, or a literal `c:tx/c:v`,
  falling back to a 1-based `"Series N"` default when `c:tx` is absent or carries no usable text
  (never an error - unlike a missing value cache, a missing name has a safe, well-defined
  fallback).
- **Values** (`c:val`): the cached `c:val/c:numRef/c:numCache`, sized by `c:ptCount`, with a
  sparse `c:pt` gap defaulting to `0.0`. Required - its absence throws
  `ChartUnsupportedFeatureException` with `Feature` `"charts-openxml-uncached-values"`.
- **Categories** (`c:cat`): a cached `c:strRef`/`c:strCache`, or a cached `c:numRef`/`c:numCache`
  (converted to invariant-culture-formatted strings). For a category-based `ChartType` (`Bar`,
  `Column`, `Line`, `Area`), only the first series' categories become the shared
  `Chart.CategoryAxis.Labels` (every series of one chart-type element shares identical categories
  in valid OOXML); for `Pie`/`Doughnut` (which have no per-category concept in the `ChartDocument`
  model), each series' own categories instead become that series' own `ChartSeries.PointLabels`.
- **Title** (`c:title`, and identically for `c:catAx`/`c:valAx`'s own `c:title`): a cached
  `c:tx/c:strRef/c:strCache`, a rich-text `c:tx/c:rich` body (every descendant `a:t` run
  concatenated), or a literal `c:tx/c:v`. An absent `c:title`, a `c:title` with no `c:tx` child
  (a styled-but-textless PowerPoint auto-title placeholder - a real shape, confirmed directly
  against a genuine `chart1.xml` part), or empty/whitespace-only resolved text, all map to `null`.
- **Legend** (`c:legend`/`c:legendPos`): an absent `c:legend` maps to `null`; a present one maps
  `t`/`b`/`l`/`r` to the identically named `ChartLegendPosition`, and approximates `tr`
  (top-right - not representable by `ChartLegendPosition`'s five-value enumeration) and any
  missing/unrecognized value to `Right` (ECMA-376's own documented default).
- **Value axis** (`c:valAx`): `c:scaling/c:min`/`c:max` map to `Minimum`/`Maximum`, `c:majorUnit`
  maps to `TickInterval`, and `c:valAx`'s own `c:title` maps to `Title`; an absent `c:valAx`, or
  one carrying none of those four values, maps to a `null` `Chart.ValueAxis`.

#### Dependencies

`OpenXmlChartParser` depends on the `ChartModel` subsystem's `ChartDocument` unit (the model
types it constructs), and, beyond that, only `System.Xml.Linq` (`XDocument`/`XElement`/
`XNamespace`, for XML traversal) and `System.Globalization.CultureInfo.InvariantCulture` (for
deterministic numeric-category formatting) - both part of the .NET base class library, available
on every one of `CanvasNetCharts`'s target frameworks with no new runtime NuGet dependency. It
has no knowledge of, and no dependency on, OPC/ZIP packaging or any host document-format package
(`CanvasNetPptx`, `CanvasNetPdf`, `CanvasNetSvg`, or a future `CanvasNetVsdx`).

#### Callers

`OpenXmlChartParser` is a public API entry point, invoked directly by consumers of the
`DemaConsulting.CanvasNet.Charts` package, and is invoked by `CanvasNetPptx`'s own
chart-rendering integration (`PptxDocument.Charts.cs`), which locates and opens the relevant
`chart#.xml` OPC part and hands this unit only the resulting XML content. It calls
`ChartModel`'s `ChartDocument` unit (`Chart`, `ChartSeries`, `ChartAxis`, `ChartLegend`,
`ChartTitle` constructors) as its own final step.
