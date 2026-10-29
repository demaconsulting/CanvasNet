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
- **Unsupported Marks Draw Nothing And Are Reported**:
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
  `CanvasNetVegaLite_SystemIntegration_Unsupported_MarkAndTransformCarryPointerLineColumn` — a mark that is not drawn
  gives an empty group, no axes are drawn for it, and the document reports it with its identifier, pointer, line, and
  column.
- **Scene Budget And Cancellation**:
  `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
  `CanvasNetVegaLite_SystemIntegration_Limits_ChartFailuresCarryLocations` — more items than `MaxSceneItems` fail with a
  `VegaLiteRenderException` at the mark's pointer before the memory is spent, and a canceled token stops the build.
