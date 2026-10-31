## Marks Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Marks` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Marks`). The subsystem contains two units: `MarkBuilder`, which turns the rows of a
view into the scene items of the supported marks (`bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick`, `line`,
`area`, `trail`, `text`, and `arc`), and `CurveBuilder`, which interpolates the paths of the line, area, and trail marks
with the curves of d3-shape and bounds their segments; later releases add the symbol-shape unit to this subsystem.

### Verification Approach

The unit verification of the subsystem (see the _MarkBuilder Unit Verification Design_ and the _CurveBuilder Unit
Verification Design_ under `marks/`) is complete for the behavior of each unit; the one unit-to-unit interaction inside
the subsystem is that `MarkBuilder` places the points of a path, charges its segments by the bound of
`CurveBuilder.MaxSegments`, and draws the path that `CurveBuilder.Line` or `CurveBuilder.Area` writes. The path unit
scenarios verify it with the real `CurveBuilder` (for example, the monotone line of `MarkBuilderPathTests` is compared with
the path that `CurveBuilder` writes for the same pixels, and the budget of a line is the group, the item, and the
bound of
the three segments). The subsystem-level scenarios verify the units working with the units that feed them and consume
their output:
the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleResolver` produce the table and the scales that the
marks read (`ChartTestHelpers.Marks` runs this whole chain, and `ScaleModel` maps the values), the real `AxisBuilder`
draws the axes around them, the real `ViewLayout` places them, and the real `ScenePainter` paints them. The scenarios
assert that the marks, the axes, and the title agree on one geometry: the bar centers are the mark color at the
positions derived from the band and linear scale formulas, the points, rules, and ticks of the other fixtures are at the
derived sample positions with the derived blended colors, the line of three points (0, 0), (1, 10), (2, 5) in the
chart of
300 by 200 pixels is the polyline through the pixels (0, 200), (150, 0), and (300, 100) with the stroke width 2 and the
middles of both segments inked, the stacked areas of ten are the polygons between the pixel rows 0, 100, and 200, a trail
is as wide as its sizes, a pie of the weights 1, 1, 2 covers a quarter, a quarter, and a half of the disc of radius
100, and
its labels sit at the middles of its slices, and an unsupported mark (`geoshape`) draws nothing while the document
reports it.

No mocking or stubbing is used. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: The original fixtures in `Fixtures/` copied to the test output directory (read only)
- **Isolation**: Each test builds its own specification and marks; no state is shared between tests

### Acceptance Criteria

The `Marks` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the unit verification document passes without error or unexpected exception.

### Test Scenarios

- **Bars Drawn Through The Whole Pipeline**:
  `CanvasNetVegaLite_SystemIntegration_Render_InlineBarHasExpectedSamplePixels`,
  `MarkBuilderTests.MarkBuilder_Build_BarAndRectPositionsMatchExpectedSceneItems` — the bar fixture's bars are the mark
  color at the centers of bands 54 pixels wide starting at 3, 63, 123, and 183, rising to y = 114, 144, 90, and 18 in a
  240 by 200 data rectangle, and the same positions are the geometry of the scene items.
- **Points, Rules, And Ticks Drawn Through The Whole Pipeline**:
  `CanvasNetVegaLite_SystemIntegration_Render_PointRuleTickHaveExpectedSamplePixels`,
  `MarkBuilderTests.MarkBuilder_Build_PointCircleSquareRuleAndTickMatchExpectedSceneItems` — scatter points, rule stems,
  and ticks are at the derived positions with the derived opacity-blended colors, and a row without a value is not drawn.
- **Paint From Channels And Configuration**:
  `MarkBuilderTests.MarkBuilder_Build_ColorSizeAndOpacityUseExpectedProperties`,
  `CanvasNetVegaLite_SystemIntegration_Render_CallerConfigOverridesSpecificationConfig` — the color, size, and opacity
  channels and the mark configuration (the definition over `config.point` over `config.mark`) set the paint of the items,
  and a caller configuration changes the chart.
- **Ramp And Classified Colors, And The Stroke And Symbol Channels**:
  `MarkBuilderChannelTests.MarkBuilder_Build_SequentialColorInterpolatesTheRange`,
  `MarkBuilderChannelTests.MarkBuilder_Build_DiscretizedColorsGiveTheColorOfTheClass`,
  `MarkBuilderChannelTests.MarkBuilder_Build_ShapeChannelChoosesTheSymbolOfEachRow` — the color of a row is the color of
  its ramp position or class, and the shape, stroke width, dash pattern, opacities, and angle of a row come from their
  channels, as the unit scenarios derive.
- **Unsupported Marks Draw Nothing And Are Reported**:
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
  `CanvasNetVegaLite_SystemIntegration_Unsupported_MarkAndTransformCarryPointerLineColumn`,
  `CanvasNetVegaLite_SystemIntegration_Unsupported_GeoshapeImageAndSpecialInvalidModesAreReported` — a mark that is not
  drawn (`geoshape` is the sentinel; `line`, `area`, and `trail` are drawn since this release) gives an empty group, no
  axes are drawn for it, and the document reports it with its identifier, pointer, line, and column; the `image` mark,
  the invalid mode `show`, the bundle interpolation of an area, the `size` channel of an area, the `theta` channel of a
  bar, the `text` channel of a point, and the `strokeDash` channel of an area are each reported once, in order, with
  their identifiers and pointers (`vl-mark-geoshape /mark`, `vl-mark-image /mark`, `vl-mark-invalid-show /mark/invalid`,
  `vl-mark-interpolate-bundle-area /mark/interpolate`, `vl-channel-size /encoding/size`,
  `vl-channel-theta /encoding/theta`, `vl-channel-text /encoding/text`, and
  `vl-channel-strokedash /encoding/strokeDash`) while the chart is still drawn, and strict mode stops at the first.
- **Scene Budget And Cancellation**:
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
  `MarkBuilderTests.MarkBuilder_Build_StackedAndBinnedMarksRespectTheBudgetAndCancellation`,
  `MarkBuilderPathTests.MarkBuilder_Build_PathSegmentsAreChargedBeforeCreation`,
  `MarkBuilderTextTests.MarkBuilder_Build_TextCharactersAreChargedBeforeFormatting`,
  `MarkBuilderArcTests.MarkBuilder_Build_ArcsAreChargedPerSliceAndPollCancellation`,
  `CurveBuilderTests.CurveBuilder_MaxSegments_IsAnUpperBoundForEveryKind`,
  `CanvasNetVegaLite_SystemIntegration_Limits_ChartFailuresCarryLocations`,
  `CanvasNetVegaLite_SystemIntegration_PathLimits_SegmentsTextAndItemsFailClosedBeforeAllocation`,
  `CanvasNetVegaLite_SystemIntegration_PathLimits_CancellationIsHonoredWhileBuildingPaths` — more items than
  `MaxSceneItems` fail with a `VegaLiteRenderException` at the mark's pointer before the memory is spent, and a canceled
  token stops the build. A path is charged with its upper bound of segments before they exist (a line of three points in
  one
  run costs the marks group, the path, and 3 + 6 = 9 segments, 11 items, and an area 1 + 1 + 12 = 14), a label with its
  characters (`MaxStringLength`, and sixteen times it for all the labels of a mark) and its lines, and an arc with one
  item; the system tests refuse a line, an area, and a trail of 2,000 points, the 300 symbols of a point overlay, and 600
  arcs at a limit of 500 items, refuse a label of 40 characters at a `MaxStringLength` of 39 and twenty such labels at a
  limit of 40 (sixteen of them are accepted), while the same charts render with the default limits, and a
  canceled token stops the render and the measure of a line, an area, a trail, a text, and an arc chart without a report.
- **Stacked, Binned, And Ranged Marks Drawn Through The Whole Pipeline**:
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Stacking_LayersAreDrawnBetweenTheirStackBounds`,
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Stacking_SignedLayersAreDrawnOnBothSidesOfZero`,
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Binning_HistogramBarsFillTheirBinsLessOnePixel`,
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_PreBinned_RangesAreBarsOverTheirRanges`,
  `MarkBuilderTests.MarkBuilder_Build_StackedLayersSpanFromTheStackStartToItsEnd`,
  `MarkBuilderTests.MarkBuilder_Build_SignedStacksHangBelowZeroInBothOrientations`,
  `MarkBuilderTests.MarkBuilder_Build_BinnedBarsSpanTheirBinsLessTheSpacing` — the layers of a stack are drawn from their
  stack start to their end in their colors, the bars of a histogram fill their bins less a pixel of spacing (the bars at
  x = 1, 41, and so on, 39 pixels wide, for the bins 0 to 35 by 5 over 280 pixels), and the bars of pre-binned data span
  their ranges, with the geometry of the scene items equal to the pixels that the painter draws.
- **Time-Unit Marks Drawn Through The Whole Pipeline**:
  `MarkBuilderTimeTests.MarkBuilder_Time_YearMonthBars_SpanTheirMonths`,
  `MarkBuilderTimeTests.MarkBuilder_Time_PointsAtTheStartOfTheUnit`,
  `MarkBuilderTimeTests.MarkBuilder_Time_Zone_BarsSpanLocalMonths`,
  `VegaLiteTemporalScaleTests.VegaLiteTemporalScale_Points_SitAtTheirDates`,
  `VegaLiteTemporalScaleTests.VegaLiteTemporalScale_RangeBars_SpanFromStartToEnd`,
  `VegaLiteTimeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Time_YearMonthBarsSpanTheirMonthsAndLabelTheMonths`
  — the real `SpecParser`,
  `DataLoader` (in the calendar of the zone), the time-unit transform, `ScaleResolver` with its time scale, `Ticks`,
  `AxisBuilder`, and `MarkBuilder` agree on one geometry: the bars of the first quarter of 2024 span their months of the
  91 day domain (the bar of January starts at 1 and is 31 x 300 / 91 - 1 = 101.198 pixels wide, derived in the
  _MarkBuilder Unit Verification Design_), points sit at their dates or at the start of their unit (three dates five days
  apart are 100 pixels apart on 200), a range of two temporal fields is a bar from its start to its end (a bar over the
  whole ten days is 200 pixels wide and one over the second half 100), and a zone moves the unit boundaries to local
  midnights.
- **Lines Drawn Through The Whole Pipeline**:
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Lines_PolylineHasTheDocumentedSegmentsAndPixels`,
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Lines_InterpolatedLineCoversItsCurvePixels`,
  `MarkBuilderPathTests.MarkBuilder_Build_LineHasHandDerivedSegmentsAndPaint`,
  `MarkBuilderPathTests.MarkBuilder_Build_LineGroupsByColorAndDetailInFirstSeenOrder`,
  `MarkBuilderPathTests.MarkBuilder_Build_LineSortsByPixelXAndByTheOrderChannel`,
  `MarkBuilderPathTests.MarkBuilder_Build_LineSizeIsTheStrokeWidthOfTheFirstRow` — the three points (0, 0), (1, 10),
  (2, 5) of the chart of 300 by 200 pixels (x(v) = 150 v on the domain 0 to 2, y(v) = 200 - 20 v on 0 to 10, without axes
  so that the data rectangle is at the padding of the chart) are the pixels (0, 200), (150, 0), and (300, 100), the path
  is `M0,200 L150,0 L300,100` stroked in `#4c78a8` with the width 2 and not filled, nothing is reported in strict mode,
  the pixels at the middles of the segments (75, 100) and (225, 50) are inked and those at (75, 40) and (225, 150) are
  the background, and the flat line y = 5 on the domain 0 to 10 is the pixel row 100, two pixels wide (the rows 99 and 100
  are inked and 98 and 101 are not). The monotone line through (0, 0), (1, 4), (2, 6), (3, 10) (x(v) = 100 v,
  y(v) = 200 -
  20 v, so the pixels (0, 200), (100, 120), (200, 80), (300, 0)) is a move and three cubics ending at those pixels, each
  cubic stays between its end points, and the samples at 0.25, 0.5, and 0.75 of every cubic are inked.
- **Interpolations Drawn Through The Whole Pipeline**:
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Curves_AllFifteenInterpolationsRenderAndAreFinite`,
  `MarkBuilderPathTests.MarkBuilder_Build_InterpolateAndTensionSelectTheCurve`,
  `MarkBuilderPathTests.MarkBuilder_Build_AreaBundleFallsBackToLinear`,
  `CurveBuilderTests.CurveBuilder_TryParse_AcceptsTheFifteenVegaLiteNamesAndRejectsOthers` — each of the fifteen
  interpolations of Vega-Lite draws a line and an area of the five points x = 0 to 4 (75 pixels apart) and y = 0, 10,
  4, 8,
  2 with finite coordinates, straight segments only for the linear and step interpolations, cubics for the others, the
  closing command only for `linear-closed` (the closed basis and cardinal curves end where they started), the first point
  as the start of the interpolations that pass through it, and the ink of the curve; the area of `bundle` is reported with
  `vl-mark-interpolate-bundle-area` at `/mark/interpolate` and drawn with the linear interpolation, and the areas of
  the closed
  curves are two closed loops without the closing command.
- **Areas, Stacks, And Imputed Keys Drawn Through The Whole Pipeline**:
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Areas_StackedAreasFillBetweenTheirLayers`,
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Impute_EncodingAndStackImputeFillMissingKeys`,
  `MarkBuilderPathTests.MarkBuilder_Build_AreaPolygonRunsBetweenTheValueAndTheBaseline`,
  `MarkBuilderPathTests.MarkBuilder_Build_StackedAreaLayersMeetAndImputeMissingKeys`,
  `MarkBuilderPathTests.MarkBuilder_Build_HorizontalAreaAndRangeAreaFollowOrient` — the area of the same points is
  `M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z` in `#4c78a8` with no stroke, and two series of ten at x = 0, 1, 2
  share the 20 of the domain: the first series is on top (the rows 0 to 100,
  `M0,0 L150,0 L300,0 L300,100 L150,100 L0,100 Z`)
  and the second below (100 to 200) in its own color, and the pixels of the layers have their colors. The `impute` of
  the y
  channel with the value 0 gives the second series of a line (b at x = 0 and 2 against a at x = 0, 1, 2, the domain 0
  to 6,
  so y(6) = 0 and y(4) = 66.6667) its third point, `M0,66.6667 L150,200 L300,66.6667` instead of
  `M0,66.6667 L300,66.6667`; the implicit impute of a stack of areas (a at x = 1, 2, 3 and b at x = 1, 3, one each,
  the x
  domain 1 to 3, the stack domain 0 to 2 at 100 pixels a unit) gives b the zero-thick vertex at (150, 200) and makes a dip
  to the baseline of b (`M0,0 L150,100 L300,0 L300,100 L150,200 L0,100 Z` above
  `M0,100 L150,200 L300,100 L300,200 L150,200 L0,200 Z`), while `impute: null` leaves b the two vertices of its own rows
  (`M0,100 L300,100 L300,200 L0,200 Z`).
- **Trails Drawn Through The Whole Pipeline**:
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Trails_WidthFollowsTheSizeChannel`,
  `MarkBuilderPathTests.MarkBuilder_Build_TrailPointsCarryTheSizeChannel`,
  `MarkBuilderPathTests.MarkBuilder_Build_TrailBreaksAtInvalidRows` — the sizes 4, 8, and 12 (the identity scale) at
  x = 1,
  5, and 9 on the domain 0 to 10 (the pixels 30, 150, 270 on the row 100) are the diameters of the trail, so the columns
  of those pixels have 4, 8, and 12 rows of ink (half widths 2, 4, and 6), in `#4c78a8` without a stroke.
- **Overlays, Invalid Modes, And The Draw Order Through The Whole Pipeline**:
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Overlays_LinePointsAndAreaLineAreDrawn`,
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_InvalidData_NullsBreakAFilterConnectsAndShowIsReported`,
  `VegaLiteLineAreaIntegrationTests.CanvasNetVegaLite_SystemIntegration_Order_OrderChannelControlsOverlapAndPathSort`,
  `MarkBuilderOverlayTests.MarkBuilder_Build_PointOverlayDrawsSymbolsWithTheOverlayDefinition`,
  `MarkBuilderOverlayTests.MarkBuilder_Build_AreaLineOverlayAndDefaultOpacity`,
  `MarkBuilderOverlayTests.MarkBuilder_Build_OverlayFromConfigAndTransparentPoints`,
  `MarkBuilderPathTests.MarkBuilder_Build_DefaultInvalidModeBreaksThePath`,
  `MarkBuilderPathTests.MarkBuilder_Build_InvalidFilterConnectsTheNeighbors`,
  `MarkBuilderPathTests.MarkBuilder_Build_InvalidShowIsTheDefaultModeAndReported`,
  `MarkBuilderOrderTests.MarkBuilder_Build_OrderChannelSortsTheDrawOrderOfAnyMark`,
  `MarkBuilderOrderTests.MarkBuilder_Build_OrderNullKeepsTheDataOrder` — a line with `point: true` is the path and three
  filled symbols at (0, 200), (150, 0), and (300, 100), an area with `line: true` and `point: true` is the polygon
  with the
  opacity 0.7, the line `M0,200 L150,0 L300,100` along its top, and the three symbols in the order area, line, point,
  and the
  points of two series have the colors of their series. For the rows y = 0, 10, null, 5, 8 at x = 0 to 4 (75 pixels apart,
  y(v) = 200 - 20 v) the default line is broken, `M0,200 L75,0 M225,100 L300,40`, the filter mode connects the neighbors,
  `M0,200 L75,0 L225,100 L300,40`, with the warning `vl-mark-invalid-domains`, the explicit default mode is silent, and
  `show` and null are reported as `vl-mark-invalid-show` at `/mark/invalid` (and thrown in strict mode). A line over the
  rows x = 1, 0, 2 is sorted by x (`M0,200 L150,0 L300,100`), by the `order` field s = 2, 0, 1 follows the sequence
  `M0,200 L300,100 L150,0`, and with `order: false` follows the data, `M150,0 L0,200 L300,100`; of three overlapping squares
  of the categories a, b, c with the order values 2, 3, 1 the drawing is c, a, b ascending (b on top, so the pixel in the
  middle is orange) and b, a, c descending (c on top, red).
- **Arcs And Labels Drawn Through The Whole Pipeline**:
  `VegaLiteArcTextIntegrationTests.CanvasNetVegaLite_SystemIntegration_Arcs_PieAndDonutCoverTheirWedges`,
  `VegaLiteArcTextIntegrationTests.CanvasNetVegaLite_SystemIntegration_Arcs_PieLabelsAreAtTheSliceMiddles`,
  `VegaLiteArcTextIntegrationTests.CanvasNetVegaLite_SystemIntegration_Text_LabelsAreFormattedPositionedAndPainted`,
  `MarkBuilderArcTests.MarkBuilder_Build_PieSlicesHaveHandDerivedAnglesAndRadii`,
  `MarkBuilderPolarTextTests.MarkBuilder_Build_PieLabelsSitAtTheMiddleOfTheirSlice`,
  `MarkBuilderTextTests.MarkBuilder_Build_TextHasPositionDefaultsAndPaint`,
  `MarkBuilderTextTests.MarkBuilder_Build_TextFormatsNumbersTimesAndNulls` — a pie of the weights 1, 1, 2 in the data
  rectangle of 200 by 200 pixels has three slices from 0 to PI / 2, PI / 2 to PI, and PI to 2 PI around (100, 100)
  with the
  outer radius 100; the points at half the radius in the middle of each slice (45, 135, and 270 degrees) have the
  colors of
  the slices, and the number of pixels of each color is the area of its wedge (a quarter of PI x 100 x 100 for the first
  two and a half for the third, within seven percent below and never above); the donut (`innerRadius` 50) has a white hole
  and the areas of the ring segments (PI x (100 x 100 - 50 x 50) / 4); the labels of the radius 60 are at
  (100 + 60 sin(PI / 4), 100 - 60 cos(PI / 4)) = (142.4264, 57.5736), (100 + 60 sin(3 PI / 4), 100 - 60 cos(3 PI / 4)) =
  (142.4264, 142.4264), and (100 + 60 sin(3 PI / 2), 100 - 60 cos(3 PI / 2)) = (40, 100) with the ink of the labels in
  the colors of the slices; and the labels of the values 1234.5, 7, and 0.26 with `,.1f` are `1,234.5`, `7.0`, and
  `0.3` at
  the pixels (100, 133.3333), (200, 66.6667), and (300, 0) (x(v) = 100 v on the domain 0 to 3, y(v) = 200 - 200 v / 30 on
  0 to 30) with the red ink around the first two.
