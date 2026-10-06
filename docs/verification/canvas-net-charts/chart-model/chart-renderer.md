### ChartRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `ChartRenderer`,
`ChartRenderOptions`, and `ChartColorPalette` types.

#### Verification Approach

The `ChartRenderer` unit is verified through tests that call `ChartRenderer.Render` (both
overloads) with a `Chart` built via `ChartBuilder` (or the model constructors directly), then
assert on the returned `Surface`: its pixel dimensions, specific-coordinate pixel sampling (an
exact color, or presence/absence of non-background content), and, where another always-present
painted feature (such as a value-axis gridline spanning the full plot width at every tick
y-position) would contaminate an absolute region scan, a differential pixel-count comparison
between the same chart definition rendered with and without the feature under test. Because
`ChartRenderer`'s dependencies are the core `CanvasNet` system's own public rendering primitives
(`Surface`, `PathBuilder`, `TrueTypeFont`/text layout, `Rgba32`), already-verified, in-house
units, no mocking or stubbing is required.

Unit tests reside in `ChartRendererTests.cs`, `ChartRendererAxisLegendTitleTests.cs`, and
`ChartRendererEdgeCaseTests.cs` within the `DemaConsulting.CanvasNet.Charts.Tests` project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `ChartRenderer`'s dependencies are the in-house `Surface`,
  `PathBuilder`, `TrueTypeFont`, and `Rgba32` units
- **Isolation**: Each test method constructs its own `Chart` and renders its own `Surface`; no
  shared state between tests

#### Unit-Level Test Scenarios

##### CanvasNetCharts-ChartModel-ChartRenderer-PixelRender: ChartRenderer Renders to a Caller-Specified Pixel Size

**Tests**: `ChartRendererTests.Render_PixelOverloadNullChart_ThrowsArgumentNullException`,
`ChartRendererTests.Render_PixelOverloadInvalidSize_ThrowsArgumentOutOfRangeException`

Calls `Render(Chart, int, int, ChartRenderOptions?)` with a `null` chart (asserting
`ArgumentNullException`) and with invalid width/height combinations (`[Theory]`, asserting
`ArgumentOutOfRangeException` propagated from `Surface`'s own constructor).

##### CanvasNetCharts-ChartModel-ChartRenderer-DpiRender: ChartRenderer Renders From a Physical Width/Height/DPI

**Tests**: `ChartRendererTests.Render_DpiOverloadNullChart_ThrowsArgumentNullException`,
`ChartRendererTests.Render_DpiOverloadInvalidInputs_ThrowsArgumentOutOfRangeException`,
`ChartRendererTests.Render_DpiOverloadExceedsMaxDimension_ThrowsArgumentOutOfRangeException`,
`ChartRendererTests.Render_DpiOverload_ComputesExpectedPixelDimensions`

Calls `Render(Chart, float, float, float, ChartRenderOptions?)` with a `null` chart (asserting
`ArgumentNullException`), with non-finite/non-positive width/height/dpi combinations (`[Theory]`,
asserting `ArgumentOutOfRangeException`), with inputs whose computed pixel dimension exceeds
`Surface.MaxDimension` (asserting `ArgumentOutOfRangeException`), and with valid inputs (asserting
the returned `Surface`'s `Width`/`Height` equal `round(widthInches * dpi)`/
`round(heightInches * dpi)`).

##### CanvasNetCharts-ChartModel-ChartRenderer-ChartTypeDispatch: ChartRenderer Dispatches Every Defined ChartType

**Tests**: `ChartRendererTests.Render_EveryChartType_ProducesCorrectlySizedSurface`,
`ChartRendererTests.Render_Bar_FullScaleBarPaintsInteriorPixel`,
`ChartRendererTests.Render_Column_FullScaleBarPaintsInteriorPixel`,
`ChartRendererTests.Render_Line_StrokesVisibleSegmentBetweenPoints`,
`ChartRendererTests.Render_Area_FillsRegionAndStrokesTopEdge`

Renders a `Chart` of each defined `ChartType` (`[Theory]`), asserting the returned `Surface`'s
dimensions match the request; separately renders a representative `Bar`/`Column`/`Line`/`Area`
chart and samples a pixel known (by deterministic geometry) to fall inside the painted
bar/line/area content, asserting it is non-background.

##### CanvasNetCharts-ChartModel-ChartRenderer-PieDoughnutWedgeLayout: ChartRenderer Lays Out Pie/Doughnut Wedges Proportionally

**Tests**: `ChartRendererTests.Render_Pie_SingleCategory_FillsEntireCircleWithoutDegenerateArc`,
`ChartRendererTests.Render_Pie_TwoEqualCategories_SplitsIntoRightAndLeftWedges`,
`ChartRendererTests.Render_Doughnut_SingleCategory_FillsRingAndLeavesHoleUnpainted`

Renders a single-category `Pie` chart and samples pixels at several angles around the circle,
asserting every sampled pixel is non-background (proving the single 100%-share wedge painted a
full circle rather than a degenerate zero-length arc); renders a two-equal-category `Pie` chart
and samples one pixel on each half, asserting each half resolved to its own series' color;
renders a single-category `Doughnut` chart and samples a pixel in the ring and a pixel in the
central hole, asserting the ring pixel is non-background and the hole pixel remains the
background color.

##### CanvasNetCharts-ChartModel-ChartRenderer-DefaultStyling: ChartRenderer Applies Default Styling and Colors

**Tests**: `ChartRendererTests.Render_CustomBackgroundColor_ClearsToThatColor`,
`ChartRendererTests.Render_TransparentBackgroundColor_ClearsToTransparent`,
`ChartRendererTests.Render_SeriesWithoutExplicitColor_ResolvesToDefaultPaletteInOrder`,
`ChartRendererTests.Render_ChartColorPaletteOverride_TakesPriorityOverDefaultPalette`,
`ChartRendererTests.Render_OptionsColorPaletteOverride_UsedWhenChartSuppliesNone`

Renders with a custom `ChartRenderOptions.BackgroundColor` and samples a corner pixel, asserting
it equals that color; separately renders with a fully transparent background color and asserts
the sampled pixel's alpha is zero; renders a chart whose series supply no explicit color and
asserts each series' painted color matches `ChartColorPalette.Default`'s corresponding by-index
entry; renders a chart whose `Chart.ColorPalette` is supplied and asserts it takes priority over
`ChartColorPalette.Default`; renders a chart whose `ChartRenderOptions.ColorPalette` is supplied
(and `Chart.ColorPalette` is not) and asserts it is used in preference to
`ChartColorPalette.Default`.

##### CanvasNetCharts-ChartModel-ChartRenderer-TitleRendering: ChartRenderer Paints an Optional Title Band

**Tests**: `ChartRendererAxisLegendTitleTests.Render_WithTitle_PaintsContentInTitleBand`,
`ChartRendererAxisLegendTitleTests.Render_WithTitle_PushesPlotContentDownComparedToNoTitle`

Renders a chart with `Chart.Title` set and counts non-background pixels within the reserved
title band, asserting a strictly greater count than the same chart rendered with no title
(a differential comparison, since the band itself is otherwise always background); separately
renders the same chart with and without a title and asserts the plot area's own content is
measurably shifted downward when a title band is reserved.

##### CanvasNetCharts-ChartModel-ChartRenderer-LegendRendering: ChartRenderer Paints a Legend at Any Position

**Tests**: `ChartRendererAxisLegendTitleTests.Render_LegendTop_PaintsSwatchesInTopBand`,
`ChartRendererAxisLegendTitleTests.Render_LegendBottom_PaintsSwatchesInBottomBand`,
`ChartRendererAxisLegendTitleTests.Render_LegendLeft_PaintsSwatchesInLeftBand`,
`ChartRendererAxisLegendTitleTests.Render_LegendRight_PaintsSwatchesInRightBand`,
`ChartRendererAxisLegendTitleTests.Render_LegendRight_NarrowsPlotAreaComparedToNoLegend`,
`ChartRendererAxisLegendTitleTests.Render_LegendPositionNone_RendersSuccessfullyWithoutLegendBand`,
`ChartRendererAxisLegendTitleTests.Render_LegendNotVisible_RendersSuccessfullyWithoutLegendBand`

Renders a chart with `ChartLegend.Position` set to each of `Top`/`Bottom`/`Left`/`Right` and
counts non-background pixels within the corresponding reserved band, asserting (by differential
comparison against the same chart with no legend) that swatches were painted there; separately
renders with a `Right` legend and asserts the plot area is measurably narrower than the same
chart with no legend; renders with `Position: None` and with `IsVisible: false`, asserting
successful rendering with no legend band reserved in either case.

##### CanvasNetCharts-ChartModel-ChartRenderer-LegendOverflowByOmission: ChartRenderer Omits Entries On Legend Overflow

**Test**: `ChartRendererEdgeCaseTests.Render_ManySeries_LegendDegradesByOmissionWithoutError`

Renders a chart with a series count far exceeding what its legend band can display at a modest
render-target size, asserting the render completes without exception and the surface is
returned at the requested dimensions, proving the excess entries were omitted rather than
overflowing or throwing.

##### CanvasNetCharts-ChartModel-ChartRenderer-AxisRendering: ChartRenderer Paints Category/Value Axes

**Test**: `ChartRendererAxisLegendTitleTests.Render_WithAxes_PaintsNonBackgroundContentInReservedMargins`

Renders a category-based chart with both a category axis and a value axis configured, and
samples pixels within the reserved axis margins, asserting non-background content (gridlines,
tick marks, and/or tick labels) is present there.

##### CanvasNetCharts-ChartModel-ChartRenderer-NegativeValueHandling: ChartRenderer Handles Values Spanning Zero

**Tests**:
`ChartRendererEdgeCaseTests.Render_Column_NegativeAndPositiveValues_PaintsBothAboveAndBelowZeroBaseline`,
`ChartRendererEdgeCaseTests.Render_Bar_NegativeAndPositiveValues_PaintsBothLeftAndRightOfZeroBaseline`,
`ChartRendererEdgeCaseTests.Render_Line_NegativeAndPositiveValues_RendersSuccessfully`,
`ChartRendererEdgeCaseTests.Render_Area_NegativeAndPositiveValues_RendersSuccessfully`

Renders a `Column` chart with a series spanning negative and positive values and samples pixels
both above and below the computed zero baseline, asserting non-background content on both sides;
renders the equivalent `Bar` chart and samples both left and right of the baseline; renders the
equivalent `Line`/`Area` charts and asserts successful rendering without exception.

##### CanvasNetCharts-ChartModel-ChartRenderer-DataLabelRendering: ChartRenderer Paints Opt-In Data Labels

**Tests**: `ChartRendererAxisLegendTitleTests.Render_PointWithDataLabel_PaintsVisibleTextNearPoint`,
`ChartRendererAxisLegendTitleTests.Render_PointWithoutDataLabel_RendersDeterministicallyWithNoLabelArtifacts`

Renders a (sub-maximum-value, to leave genuine headroom above the bar) series with
`ChartSeries.PointLabels` supplied and counts non-background pixels near the data point,
asserting (by differential comparison against the same chart with `PointLabels` omitted) that
label text was painted; separately asserts the two renders are pixel-for-pixel deterministic
when `PointLabels` is omitted from both.

##### CanvasNetCharts-ChartModel-ChartRenderer-SinglePointSeries: ChartRenderer Renders a Single-Point Series Successfully

**Tests**: `ChartRendererEdgeCaseTests.Render_Bar_SingleDataPoint_RendersSuccessfully`,
`ChartRendererEdgeCaseTests.Render_Column_SingleDataPoint_RendersSuccessfully`,
`ChartRendererEdgeCaseTests.Render_Line_SingleDataPoint_RendersSuccessfully`,
`ChartRendererEdgeCaseTests.Render_Area_SingleDataPoint_RendersSuccessfully`,
`ChartRendererEdgeCaseTests.Render_Pie_SingleValue_RendersFullCircleWedge`,
`ChartRendererEdgeCaseTests.Render_Doughnut_SingleValue_RendersFullCircleWedge`

Renders a `Bar`/`Column`/`Line`/`Area` chart whose single series contains exactly one data point,
and a `Pie`/`Doughnut` chart whose single series contains exactly one value, asserting each
renders successfully without exception (and, for `Pie`/`Doughnut`, that the full circle is
painted).

##### CanvasNetCharts-ChartModel-ChartRenderer-LongLabelTruncation: ChartRenderer Truncates Over-Long Labels

**Tests**: `ChartRendererEdgeCaseTests.Render_VeryLongCategoryLabel_RendersSuccessfullyWithoutOverflow`,
`ChartRendererEdgeCaseTests.Render_VeryLongLegendLabel_RendersSuccessfullyWithoutOverflow`

Renders a chart with an unusually long category-axis label, and separately with an unusually
long series/legend name, at a modest render-target size, asserting both render successfully
without exception.

##### CanvasNetCharts-ChartModel-ChartRenderer-RenderTargetSizeExtremes: ChartRenderer Handles Tiny and Huge Render Targets

**Tests**:
`ChartRendererEdgeCaseTests.Render_OnePixelByOnePixelTarget_RendersSuccessfullyForEveryChartType`,
`ChartRendererEdgeCaseTests.Render_VerySmallRenderTargets_RenderSuccessfully`,
`ChartRendererEdgeCaseTests.Render_MaximumDimensionRenderTarget_RendersSuccessfully`

Renders every `ChartType` at a 1x1 pixel target (`[Theory]`), asserting success; renders at
several other minimal sizes up to roughly 10x10 pixels, asserting success; renders at the
largest `Surface`-supported dimension (8192x8192), asserting success. All assert the absence of
any thrown exception.

##### CanvasNetCharts-ChartModel-ChartRenderer-ManySeriesGroupedLayout: ChartRenderer Narrows Bars for a Large Series Count

**Test**: `ChartRendererEdgeCaseTests.Render_ManySeriesGroupedColumn_NarrowsBarsRatherThanOverlapping`

Renders a grouped `Column` chart with a series count large enough that naive full-width bars
would overlap within their shared category band, asserting the render completes without
exception, proving each series' bar width was narrowed to fit.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong pixel/
dimension value constitutes a failure.
