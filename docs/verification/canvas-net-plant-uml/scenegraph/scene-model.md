### SceneModel Unit Verification Design

This document describes the unit-level verification strategy for the `SceneModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Scenegraph` namespace.

#### Verification Approach

The unit is verified by building shapes and scenes and comparing their bounds and device paths with literal values.
The core `Geometry` subsystem is used as is for the device paths. Unit tests reside in `Scenegraph/SceneModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A bound or a path that differs from the expected literal
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Scenegraph-SceneModel-Shapes: The Shapes And Their Device Paths

**Tests**: `SceneModelTests.PlantUmlSceneModel_Shapes_HaveTheirBounds`,
`SceneModelTests.PlantUmlSceneModel_ToPath_ScalesAndTranslates`

Asserts the bounds of a rectangle, a rounded rectangle, an ellipse, a polygon, and a line, the segment count of a
rounded rectangle and the closing of a polygon, and the device bounds of a scaled and translated rectangle and of a
path without a move.

##### CanvasNetPlantUml-Scenegraph-SceneModel-Bounds: The Bounds Of A Scene

**Tests**: `SceneModelTests.PlantUmlSceneModel_BoundsOf_CombinesPathsAndText`

Asserts the union of a rectangle and a text box, no bounds for no items, and the text of a line.

##### CanvasNetPlantUml-Scenegraph-SceneModel-PageTexts: Page Texts Around The Body

**Tests**: `SceneModelTests.PlantUmlSceneModel_ScenePage_PlacesPageTexts`,
`SceneModelTests.PlantUmlSceneModel_ScenePage_NoTextsAndTranslate`

Places a header, a title, a legend, a caption, and a footer around a body of 78.167 by 133.57 and asserts the page
size and the positions PlantUML gives them (the header at 54.871, the title at 15 and its baseline at 43.111, the body
moved to 12.475 and 62.188, and the baselines of the legend, caption, and footer); asserts that a body without page
texts is returned as it is, and that translation moves a text line and a path and returns an item unchanged for a zero
offset.
