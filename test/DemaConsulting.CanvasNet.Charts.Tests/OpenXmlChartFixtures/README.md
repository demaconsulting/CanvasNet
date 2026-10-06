# OpenXML Chart Test Fixtures

<!-- cspell:ignore aiden0z chartspace barchart linechart piechart doughnutchart areachart ptcount -->
<!-- cspell:ignore strref numref strcache numcache barchart barDir -->

`OpenXmlChartParserTests.cs` builds every one of its test fixtures as an in-memory
`System.Xml.Linq.XElement` tree directly in the test file itself (the idiomatic pattern already
used by this project's own `.Pptx.Tests`/`.Svg.Tests` suites for fine-grained, single-construct
unit tests - small hand-authored element fragments, not embedded resource files), rather than
loading external `.xml` fixture files from this folder. This folder exists to document the
provenance of the one fixture shape that *is* derived from a real, third-party file: the
stacked-column bar-chart excerpt used by `Parse_RealFixtureBarChartExcerpt_ParsesExpectedShape`
and `Parse_ThenRender_RealFixtureBarChartExcerpt_ProducesCorrectlySizedSurface`.

## Real-World Fixture Provenance

Both tests above hand-reconstruct a trimmed, styling-stripped excerpt of the real
`ppt/charts/chart1.xml` part found inside
`test/DemaConsulting.CanvasNet.Pptx.Tests/PptxFixtures/aiden0z-1-chart-and-complex.pptx` - itself
taken unmodified from the [aiden0z/pptx-renderer](https://github.com/aiden0z/pptx-renderer)
project's own `docs/example/1-chart-and-complex/source.pptx` (Apache License 2.0; see that
folder's own `README.md` and `Aiden0zPptxRenderer.LICENSE` for the full provenance and license
text - this folder does not re-host either, to avoid two divergent copies of the same license
notice).

The real `chart1.xml` part was inspected directly (extracted locally via `Expand-Archive` after
copying the `.pptx` to a `.zip` extension, then pretty-printed) to confirm the exact shape this
parser's test excerpt reconstructs:

- A single `<c:barChart>` with `<c:barDir val="col"/>` and `<c:grouping val="stacked"/>` - a
  stacked column chart, classified by this parser as `ChartType.Column`.
- Three `<c:ser>` entries ("Series 1", "Series 2", "Series 3"), each with a cached
  `<c:tx>/<c:strRef>/<c:strCache>` name, a cached `<c:cat>/<c:strRef>/<c:strCache>` four-entry
  category list ("Category 1".."Category 4"), and a cached `<c:val>/<c:numRef>/<c:numCache>`
  four-entry value list.
- A `<c:title>` element that is present (carries real styling/`<c:spPr>`/`<c:txPr>` content) but
  has **no** `<c:tx>` child at all - a styled-but-textless PowerPoint auto-title placeholder,
  which this parser maps to a `null` `Chart.Title` (see `OpenXmlChartParser.ParseTitle`'s own
  documented rule) - confirmed directly against the real file, not assumed.
- `<c:catAx>` and `<c:valAx>` siblings of `<c:barChart>` (not nested inside it), neither carrying
  a `<c:title>` child.
- A `<c:legend>` with exactly `<c:legendPos val="b"/>` - a bottom legend.

The test excerpt reproduces every one of these structurally significant shapes (chart kind,
`barDir`/`grouping`, series count, per-series name/category/value cache shape and count, the
textless title, the title-less axes, and the bottom legend) while deliberately dropping every
real file's purely cosmetic styling element (`<c:spPr>`, `<c:txPr>`, `<c:dLbls>`, theme color
references, and so on) that this format-agnostic, cached-values-only parser does not read at
all - keeping the fixture small, readable, and focused on what this phase actually parses, while
still being traceable back to genuine real-world output rather than an arbitrary invention.

No real-world fixture exists anywhere in this project's corpus for `c:lineChart`, `c:pieChart`,
`c:doughnutChart`, or `c:areaChart` (confirmed during this phase's planning): every other chart
type's test fixture is therefore a minimal, hand-authored fragment following the ECMA-376
DrawingML-Charts schema shapes documented in `OpenXmlChartParser`'s own XML doc comments, not a
real-file excerpt.
