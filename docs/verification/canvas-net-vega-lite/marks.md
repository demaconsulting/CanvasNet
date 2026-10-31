## Marks Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Marks` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Marks`). In this release the subsystem contains a single unit, `MarkBuilder`, which
turns the rows of a view into the scene items of the supported marks (`bar`, `rect`, `point`, `circle`, `square`,
`rule`, and `tick`); later releases add the other marks and the curve and symbol-shape units to this subsystem.

### Verification Approach

The `Marks` subsystem has one unit, so its unit verification (see the _MarkBuilder Unit Verification Design_ under
`marks/`) is complete for the subsystem's own behavior and there are no unit-to-unit interactions inside the subsystem
to verify. The subsystem-level scenarios verify the unit working with the units that feed it and consume its output:
the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleResolver` produce the table and the scales that the
marks read (`ChartTestHelpers.Marks` runs this whole chain, and `ScaleModel` maps the values), the real `AxisBuilder`
draws the axes around them, the real `ViewLayout` places them, and the real `ScenePainter` paints them. The scenarios
assert that the marks, the axes, and the title agree on one geometry: the bar centers are the mark color at the
positions derived from the band and linear scale formulas, the points, rules, and ticks of the other fixtures are at the
derived sample positions with the derived blended colors, and an unsupported mark draws nothing while the document
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
  `CanvasNetVegaLite_SystemIntegration_Unsupported_MarkAndTransformCarryPointerLineColumn` — a mark that is not drawn
  gives an empty group, no axes are drawn for it, and the document reports it with its identifier, pointer, line, and
  column.
- **Scene Budget And Cancellation**:
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
  `MarkBuilderTests.MarkBuilder_Build_StackedAndBinnedMarksRespectTheBudgetAndCancellation`,
  `CanvasNetVegaLite_SystemIntegration_Limits_ChartFailuresCarryLocations` — more items than `MaxSceneItems` fail with a
  `VegaLiteRenderException` at the mark's pointer before the memory is spent, and a canceled token stops the build.
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
