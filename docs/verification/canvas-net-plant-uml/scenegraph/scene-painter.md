### ScenePainter Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePainter` unit in the
`DemaConsulting.CanvasNet.PlantUml.Scenegraph` namespace.

#### Verification Approach

The unit is verified by painting small scenes onto surfaces and sampling pixels, with the `TextLayout` unit and the
core drawing subsystems used as is. The budget and cancellation scenarios use a small budget and a pre-canceled token
(no clock). Unit tests reside in `Scenegraph/ScenePainterTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A sampled pixel, a placement, or an exception that
differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Scenegraph-ScenePainter-Paints: Fills, Strokes, Dashes, And Text

**Tests**: `ScenePainterTests.PlantUmlScenePainter_Paint_FillStrokeAndBackground`,
`ScenePainterTests.PlantUmlScenePainter_Paint_DashedLineAndScale`,
`ScenePainterTests.PlantUmlScenePainter_Paint_TextInsideItsBox`

Asserts a rectangle's fill, its stroke on the edge, and the background; the dashes and gaps of a dashed line at scale
2; and that struck text paints ink only inside its box.

##### CanvasNetPlantUml-Scenegraph-ScenePainter-Placement: Scale And Fit

**Tests**: `ScenePainterTests.PlantUmlScenePainter_Paint_DashedLineAndScale`,
`ScenePainterTests.PlantUmlScenePainter_Fit_ScalesUniformlyAndCenters`

Asserts the scaled dashes and the fitted placement of a wide view box and of a view box without area.

##### CanvasNetPlantUml-Scenegraph-ScenePainter-Bounded: Bounds, Budget, And Cancellation

**Tests**: `ScenePainterTests.PlantUmlScenePainter_Paint_NonFiniteItem_IsSkipped`,
`ScenePainterTests.PlantUmlScenePainter_Paint_OverPixelBudget_ThrowsLocated`,
`ScenePainterTests.PlantUmlScenePainter_Paint_Canceled_Throws`

Asserts that a rectangle with a coordinate that is not a number paints nothing, that a fill over a budget of 100
pixels throws `plantuml-limit-exceeded` at the item's location naming `MaxPaintedPixels`, and that a canceled token
throws.
