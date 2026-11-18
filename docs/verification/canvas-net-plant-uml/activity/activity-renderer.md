### ActivityRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `ActivityRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.Activity` namespace.

#### Verification Approach

The unit is verified by converting placed shapes built in the test and the geometry of a laid-out diagram, and
asserting the scene items. No mocking is required. Unit tests reside in `Activity/ActivityRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A bound, a paint, a role, or a count that differs from
the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Activity-ActivityRenderer-Shapes: Shapes Become Paths

**Tests**: `ActivityRendererTests.PlantUmlActivityRenderer_Rect_BecomesARoundedPath`,
`ActivityRendererTests.PlantUmlActivityRenderer_EllipseLineAndPolygon_BecomePaths`

Asserts the bounds, role, location, fill, and curved corners of a rounded rectangle, and the bounds of an ellipse, a
line, and a polygon moved to its origin.

##### CanvasNetPlantUml-Activity-ActivityRenderer-Space: Space Without Paint

**Tests**: `ActivityRendererTests.PlantUmlActivityRenderer_UnpaintedShapes_DrawNothing`

Asserts no item for empty space and for a rectangle without fill or stroke.

##### CanvasNetPlantUml-Activity-ActivityRenderer-Scene: The Scene Of A Diagram

**Tests**: `ActivityRendererTests.PlantUmlActivityRenderer_Render_BuildsTheScene`

Asserts the view box of the geometry, the theme's background, two action boxes with their texts, the arrow label, and
three arrow heads.
