## OpenXmlChart

![OpenXmlChart Structure](OpenXmlChartView.svg)

The `OpenXmlChart` subsystem is the second software subsystem in `CanvasNetCharts`, introduced in
Phase 3. It groups one unit, `OpenXmlChartParser`, located in its own `src/
DemaConsulting.CanvasNet.Charts/OpenXml/` sub-folder (distinct from `ChartModel`'s flat
`src/DemaConsulting.CanvasNet.Charts/` folder).

### Purpose

The `OpenXmlChart` subsystem provides a single, format-agnostic capability: turning a raw ECMA-376
DrawingML-Charts `c:chartSpace`/`c:chart` `System.Xml.Linq` element into the validated `Chart`
the `ChartModel` subsystem's `ChartRenderer` already knows how to paint. It is deliberately
format-agnostic — it has no knowledge of OPC/ZIP packaging, part relationships, or any specific
host document format (PresentationML/`.pptx`, SpreadsheetML/`.xlsx`, or a future `.vsdx`) — the
`CanvasNetPptx` integration is responsible for locating and opening the relevant `chart#.xml`
part and handing this subsystem only the resulting XML content. This
boundary is what keeps `OpenXmlChartParser` reusable by any future document-format library that
embeds an OOXML chart part, not coupled to one specific host format. `OpenXmlChartParser` reads
only cached values (`c:numCache`/`c:strCache`) and never recomputes from a `c:f` formula, mirroring
`CanvasNetPptx`'s own documented `<a:fld>` "paint the cached value, never recompute" convention.

### Units

- **OpenXmlChartParser** — the `OpenXmlChartParser` static class and its
  `ChartUnsupportedFeatureException`; see
  _OpenXmlChartParser Unit Design_ (`open-xml-chart/open-xml-chart-parser.md`)

### Dependencies

The `OpenXmlChart` subsystem depends on the `ChartModel` subsystem's `ChartDocument` unit (its
`Chart`/`ChartSeries`/`ChartAxis`/`ChartLegend`/`ChartTitle`/`ChartType` model types, which
`OpenXmlChartParser` constructs directly), and, beyond that, only the .NET base class library's
`System.Xml.Linq` (`XDocument`/`XElement`/`XNamespace`, for XML parsing) and `System.Globalization`
(`CultureInfo.InvariantCulture`, for formatting a numeric category label deterministically) — see
_CanvasNetCharts System Design_ (`../canvas-net-charts.md`)'s Dependencies section. It introduces
no new runtime NuGet dependency, and - per that same System Design document's hard architectural
constraint - must never reference `CanvasNetPptx`, `CanvasNetPdf`, `CanvasNetSvg`, or a future
`CanvasNetVsdx`.

### Callers

`OpenXmlChartParser` is a public API entry point, invoked directly by consumers of the
`DemaConsulting.CanvasNet.Charts` package, and is also invoked by `CanvasNetPptx`'s own
chart-rendering integration (`PptxDocument.Charts.cs`), which locates and opens the relevant
`chart#.xml` OPC part before handing its content to `OpenXmlChartParser`. No unit within the
`OpenXmlChart` subsystem is called by any other subsystem of `CanvasNetCharts` itself as of this
release; `OpenXmlChartParser` itself calls `ChartModel`'s `ChartDocument` unit, the one
intra-system subsystem-to-subsystem call `OpenXmlChart` contains.
