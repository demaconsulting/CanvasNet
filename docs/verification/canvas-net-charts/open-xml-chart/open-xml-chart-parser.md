### OpenXmlChartParser Unit Verification Design

This document describes the unit-level verification strategy for the `OpenXmlChartParser` static
class and its `ChartUnsupportedFeatureException`.

#### Verification Approach

The `OpenXmlChartParser` unit is verified through unit tests that call `Parse(XDocument)`/
`Parse(XElement)` with hand-authored, schema-legal ECMA-376 `c:chartSpace`/`c:chart` fragments
(and, for one scenario, a trimmed, styling-stripped excerpt derived directly from a real
PowerPoint-authored `chart1.xml` part), asserting on the resulting `Chart`'s properties or on a
thrown exception's type and (for `ChartUnsupportedFeatureException`) its `Feature` token. Because
`OpenXmlChartParser`'s only in-house dependency is the already-verified `ChartModel` subsystem's
`ChartDocument` unit, no mocking or stubbing is required. Several scenarios additionally feed the
parsed `Chart` directly into `ChartRenderer.Render`, reusing the Phase 2 pixel-sampling test
convention to prove the parser's output is directly renderable with no further adaptation.

Unit tests reside in `OpenXmlChartParserTests.cs` within the
`DemaConsulting.CanvasNet.Charts.Tests` project. Hand-authored XML fragments are built inline via
`System.Xml.Linq` object construction; the one real-fixture-derived fragment is documented, with
its provenance, in `test/DemaConsulting.CanvasNet.Charts.Tests/OpenXmlChartFixtures/README.md`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `OpenXmlChartParser`'s dependencies are `System.Xml.Linq`,
  `System.Globalization`, and the in-house `ChartDocument` unit
- **Isolation**: Each test method constructs its own `XElement`/`XDocument` fragment; no shared
  state between tests

#### Unit-Level Test Scenarios

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-ArgumentValidation: OpenXmlChartParser Validates Its Input Shape

**Tests**: `OpenXmlChartParserTests.Parse_NullDocument_ThrowsArgumentNullException`,
`OpenXmlChartParserTests.Parse_NullElement_ThrowsArgumentNullException`,
`OpenXmlChartParserTests.Parse_DocumentWithNoRoot_ThrowsArgumentException`,
`OpenXmlChartParserTests.Parse_ElementWithNoChartChild_ThrowsArgumentException`,
`OpenXmlChartParserTests.Parse_ChartWithNoPlotArea_ThrowsArgumentException`,
`OpenXmlChartParserTests.Parse_ChartSpaceWrapper_ParsesSameAsBareChart`,
`OpenXmlChartParserTests.Parse_Document_ParsesSameAsElement`

Calls each `Parse` overload with a `null` argument, a rootless `XDocument`, an element with no
`c:chart` descendant, and a `c:chart` with no `c:plotArea` child. Asserts `ArgumentNullException`
for the `null` cases and `ArgumentException` for the three structurally invalid cases. Separately
parses an identical chart both as a bare `c:chart` element and wrapped in a full `c:chartSpace`,
and both via `Parse(XElement)` and `Parse(XDocument)`, asserting both pairs produce an
equivalent `Chart`.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-BarChartClassification: OpenXmlChartParser Classifies by c:barDir

**Tests**: `OpenXmlChartParserTests.Parse_BarChartWithBarDirection_ClassifiesAsBar`,
`OpenXmlChartParserTests.Parse_BarChartWithColumnDirection_ClassifiesAsColumn`,
`OpenXmlChartParserTests.Parse_BarChartWithNoBarDirection_DefaultsToColumn`

Parses a `c:barChart` with `c:barDir val="bar"`, with `c:barDir val="col"`, and with no
`c:barDir` child at all. Asserts the resulting `Chart.Type` is `ChartType.Bar` for the first case
and `ChartType.Column` for the latter two.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-OtherChartTypeClassification: OpenXmlChartParser Classifies Line/Pie/Doughnut/Area

**Tests**: `OpenXmlChartParserTests.Parse_LineChart_ClassifiesAsLine`,
`OpenXmlChartParserTests.Parse_AreaChart_ClassifiesAsArea`,
`OpenXmlChartParserTests.Parse_PieChart_ClassifiesAsPieWithPointLabels`,
`OpenXmlChartParserTests.Parse_DoughnutChart_ClassifiesAsDoughnut`

Parses a `c:lineChart`, a `c:areaChart`, a `c:pieChart`, and a `c:doughnutChart`. Asserts the
resulting `Chart.Type` matches each element respectively, and - for the `Pie` case - that each
series' own `PointLabels` (rather than a shared `Chart.CategoryAxis`) carries the parsed
category text.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-SeriesNameExtraction: OpenXmlChartParser Extracts a Series Name

**Tests**: `OpenXmlChartParserTests.Parse_SeriesWithCachedName_ExtractsName`,
`OpenXmlChartParserTests.Parse_SeriesWithNoTx_DefaultsToSeriesIndexName`,
`OpenXmlChartParserTests.Parse_SeriesWithLiteralTxValue_ExtractsName`

Parses a series with a cached `c:tx/c:strRef/c:strCache` name, a series with no `c:tx` element
at all, and a series with a literal `c:tx/c:v` value. Asserts the resulting `ChartSeries.Name`
is the cached text, a 1-based `"Series N"` default, and the literal text, respectively.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-ValueExtraction: OpenXmlChartParser Extracts Cached Values

**Tests**: `OpenXmlChartParserTests.Parse_SparseNumCache_MissingIndexDefaultsToZero`,
`OpenXmlChartParserTests.Parse_MultipleSeries_ParsesAllInOrder`

Parses a `c:numCache` whose `c:pt` entries skip an index within its declared `c:ptCount`, and
separately parses a plot area with more than one `c:ser` element. Asserts the missing index
resolves to `0.0`, and that every series is parsed, in document order, into
`Chart.Series`.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-CategoryExtraction: OpenXmlChartParser Extracts Categories

**Tests**: `OpenXmlChartParserTests.Parse_NumericCategories_ConvertsToInvariantCultureStrings`,
`OpenXmlChartParserTests.Parse_CategoryBasedChart_ExtractsCategoryAxisLabels`

Parses a `c:cat/c:numCache` (rather than a `c:strCache`), and separately parses a category-based
chart's first series' `c:cat/c:strCache`. Asserts the numeric cache's values are converted to
`CultureInfo.InvariantCulture`-formatted strings, and that the shared `Chart.CategoryAxis.Labels`
reflects the first series' categories.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-TitleExtraction: OpenXmlChartParser Extracts a Chart Title

**Tests**: `OpenXmlChartParserTests.Parse_TitleWithCachedText_ExtractsTitle`,
`OpenXmlChartParserTests.Parse_TitleWithRichText_ConcatenatesRuns`,
`OpenXmlChartParserTests.Parse_TitlePresentWithNoTx_MapsToNullTitle`,
`OpenXmlChartParserTests.Parse_NoTitleElement_MapsToNullTitle`

Parses a `c:title/c:tx/c:strRef/c:strCache`, a `c:title/c:tx/c:rich` body with more than one
`a:t` run, a `c:title` with no `c:tx` child, and a chart with no `c:title` element at all.
Asserts the resulting `Chart.Title.Text` is the cached text, the concatenation of every run, and
`null` for the latter two cases.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-LegendExtraction: Extracts Legend Presence/Position

**Tests**: `OpenXmlChartParserTests.Parse_NoLegendElement_MapsToNullLegend`,
`OpenXmlChartParserTests.Parse_LegendPosition_MapsToExpectedChartLegendPosition`,
`OpenXmlChartParserTests.Parse_LegendWithNoPosition_DefaultsToRight`

Parses a chart with no `c:legend` element, a `c:legend` with each of `c:legendPos` `"t"`/`"b"`/
`"l"`/`"r"`/`"tr"` (via `[Theory]`), and a `c:legend` with no `c:legendPos` child. Asserts
`Chart.Legend` is `null` for the first case, the expected `ChartLegendPosition` for each defined
value (with `"tr"` approximated to `Right`), and `Right` for the missing-position case.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-AxisExtraction: OpenXmlChartParser Extracts Axis Titles/Range

**Tests**: `OpenXmlChartParserTests.Parse_CatAxTitle_ExtractsCategoryAxisTitle`,
`OpenXmlChartParserTests.Parse_ValAx_ExtractsRangeAndTitle`,
`OpenXmlChartParserTests.Parse_NoValAx_MapsToNullValueAxis`

Parses a `c:catAx` with a `c:title`, a `c:valAx` with `c:scaling/c:min`, `c:scaling/c:max`,
`c:majorUnit`, and its own `c:title`, and a chart with no `c:valAx` element at all. Asserts the
category axis title, and the value axis's `Minimum`/`Maximum`/`TickInterval`/`Title`, are
extracted correctly, and that `Chart.ValueAxis` is `null` for the absent case.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-UnsupportedChartTypeRejection: Rejects Unsupported Chart Kinds

**Tests**: `OpenXmlChartParserTests.Parse_UnsupportedChartType_ThrowsWithExpectedFeatureToken`,
`OpenXmlChartParserTests.Parse_EmptyPlotArea_ThrowsWithNoChartTypeFeatureToken`

Parses a plot area containing, in turn (via `[Theory]`), each of `c:radarChart`,
`c:scatterChart`, `c:bubbleChart`, `c:stockChart`, `c:surfaceChart`, `c:surface3DChart`,
`c:bar3DChart`, `c:line3DChart`, `c:pie3DChart`, `c:area3DChart`, and `c:ofPieChart`, and
separately a plot area with no chart-type element at all. Asserts
`ChartUnsupportedFeatureException` is thrown with the documented `Feature` token for each kind,
and `"charts-openxml-no-chart-type"` for the empty case.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-ComboChartRejection: OpenXmlChartParser Rejects a Combo Chart

**Test**: `OpenXmlChartParserTests.Parse_ComboChart_ThrowsWithComboChartFeatureToken`

Parses a plot area containing both a `c:barChart` and a `c:lineChart`. Asserts
`ChartUnsupportedFeatureException` is thrown with `Feature` `"charts-openxml-combo-chart"`.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-UncachedValuesRejection: Rejects a Series With No Cached Values

**Test**: `OpenXmlChartParserTests.Parse_SeriesWithUncachedValues_ThrowsWithUncachedValuesFeatureToken`

Parses a series whose `c:val/c:numRef` has no `c:numCache` child. Asserts
`ChartUnsupportedFeatureException` is thrown with `Feature` `"charts-openxml-uncached-values"`.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-ChartUnsupportedFeatureException: Carries a Feature Token

**Tests**: `OpenXmlChartParserTests.Parse_SeriesWithUncachedValues_ThrowsWithUncachedValuesFeatureToken`,
`OpenXmlChartParserTests.Parse_UnsupportedChartType_ThrowsWithExpectedFeatureToken`,
`OpenXmlChartParserTests.Parse_ComboChart_ThrowsWithComboChartFeatureToken`,
`OpenXmlChartParserTests.Parse_EmptyPlotArea_ThrowsWithNoChartTypeFeatureToken`

Each test above additionally asserts the thrown `ChartUnsupportedFeatureException`'s `Feature`
property equals the exact documented token for its scenario, and that the exception is an
`IOException`, proving the exception's shape in the same pass as its triggering condition.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-RealFixtureEndToEnd: OpenXmlChartParser Parses a Real chart1.xml Excerpt

**Tests**: `OpenXmlChartParserTests.Parse_RealFixtureBarChartExcerpt_ParsesExpectedShape`,
`OpenXmlChartParserTests.Parse_ThenRender_RealFixtureBarChartExcerpt_ProducesCorrectlySizedSurface`

Parses a trimmed, styling-stripped excerpt derived directly from the `aiden0z/pptx-renderer`
corpus's real `chart1.xml` part (a stacked column chart, three series, four categories, a
styled-but-textless title, title-less axes, and a bottom legend - see
`test/DemaConsulting.CanvasNet.Charts.Tests/OpenXmlChartFixtures/README.md` for full provenance).
Asserts the resulting `Chart`'s type, series count/names/values, category labels, `Title`
(`null`), and `Legend` (`Bottom`) all match the real part's actual content, and separately feeds
that same parsed `Chart` into `ChartRenderer.Render`, asserting the returned `Surface`'s
dimensions match the requested render size.

##### CanvasNetCharts-OpenXmlChart-OpenXmlChartParser-RenderRoundTrip: Output Renders for Every Supported Type

**Test**: `OpenXmlChartParserTests.Parse_ThenRender_EveryChartType_ProducesCorrectlySizedSurface`

For each of `Bar`, `Column`, `Line`, `Pie`, `Doughnut`, and `Area` (via `[Theory]`), parses a
minimal but complete hand-authored chart fragment of that kind and feeds the resulting `Chart`
directly into `ChartRenderer.Render`. Asserts the returned `Surface`'s `Width`/`Height` match the
requested render size, proving the parser's output is directly consumable by the Phase 2
renderer with no further adaptation, for every supported chart kind.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, wrong `Feature` token,
or wrong property value constitutes a failure.
