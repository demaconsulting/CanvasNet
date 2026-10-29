## Layout Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Layout` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Layout`). In this release the subsystem contains a single unit, `ViewLayout`, which
computes the data rectangle, padding, autosize, title and guide placement, background, and view border geometry of a
single view and assembles the scene (the composition layout of later releases adds units to this subsystem).

### Verification Approach

The `Layout` subsystem has one unit, so its unit verification (see the _ViewLayout Unit Verification Design_ under
`layout/`) is complete for the subsystem's own behavior and there are no unit-to-unit interactions inside the subsystem
to verify. The subsystem-level scenarios verify the unit working with its neighbors across the pipeline, in the way
the document uses it: the data rectangle size is resolved from the specification, the configuration, and the hints from
the scale stage (the discrete step sizes), the guide extents come from the real `AxisBuilder` and the title from the
real `TitleBuilder`, and the resulting scene is painted by the real `ScenePainter`. The scenarios assert the agreement of
the three stages: the canvas size that `Measure` reports is the size of the surface that `Render` produces, the title and
the axes leave ink in the bands the layout reserves for them, and a size that depends on the data (discrete bands) or on
a container is stable under the layout's iteration.

No mocking or stubbing is used. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through the real text measurer
- **Isolation**: Each test builds its own specification and layout request; no files are read except the checked-in
  fixtures used by the chart integration scenarios

### Acceptance Criteria

The `Layout` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the unit verification document passes without error or unexpected exception.

### Test Scenarios

- **Measure Equals Render**:
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Measure_ReportsExactlyTheRenderedSize`,
  `CanvasNetVegaLite_SystemIntegration_Measure_MatchesNaturalRenderedSize` — the layout is computed once and shared, so
  the measured size equals the rendered surface size for every fixture, with and without a scale.
- **Title And Padding Grow The Canvas By The Derived Amounts**:
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Measure_NaturalSizeFollowsPaddingTitleAndAutosize`,
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitleIsPaintedAboveThePlotAndCenteredOnIt` — a top title makes
  the empty 310 by 310 canvas 310 by 327, and the title ink lies in the band above the plot.
- **Guides Grow The Canvas And Fit Is Uniform**:
  `CanvasNetVegaLite_SystemIntegration_Render_InlineBarHasExpectedSamplePixels`,
  `CanvasNetVegaLite_SystemIntegration_Render_WidthHeightUniformlyFitsAndCenters` — the axes and title of the bar fixture
  grow the canvas around the 240 by 200 data rectangle to the derived size, and an explicit size fits the chart
  uniformly and centers it.
- **Container Sizes Are Laid Out Again For The Container**:
  `CanvasNetVegaLite_SystemIntegration_Render_ContainerWidthFillsTheContainer` — a container width gives a canvas of
  exactly the container width with the data rectangle computed from what the padding and the guides leave.
- **Layout Is Deterministic**:
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitledViewIsDeterministicAcrossThreadsAndFontOptions` — the layout
  and the pixels of a titled view are identical across threads and font options.
