### SkinTheme Unit Verification Design

This document describes the unit-level verification strategy for the `SkinTheme` unit in the
`DemaConsulting.CanvasNet.PlantUml.Styling` namespace.

#### Verification Approach

The unit is verified by reading the styles of the default theme and comparing them with the values of PlantUML's
default skin as literals, and by changing a copy. The unit has no collaborator; no mocking is required. Unit tests
reside in `Styling/SkinThemeTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. Any value that differs from PlantUML's default skin
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Styling-SkinTheme-DefaultLook: The Values Of PlantUML's Default Skin

**Tests**: `SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlSequenceLook`,
`SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlActivityLook`,
`SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlGraphLook`

Asserts the participant font, fill, line, width, corners, and padding, the square actor, the note, arrow, group, life
line, header, and title styles, the white background, the foot box, and the arrow thickness; the activity, swimlane,
and bar styles; and the link label size and line, and the package and container outlines, corners, fills, and bold
titles of the graph diagrams.

##### CanvasNetPlantUml-Styling-SkinTheme-ClassLook: The Class And Object Boxes

**Tests**: `SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlClassLook`

Asserts the fill, line, line width, corners, and font size of classes and objects, the italic 12 of stereotypes, the
size and color of members, and the icon size of 10 and the circle radius of 11.

##### CanvasNetPlantUml-Styling-SkinTheme-StateLook: The State Shapes

**Tests**: `SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlStateLook`

Asserts the `#F1F1F1` fill, the `#181818` line half a pixel wide, the corners of 25, and the size 14 of states; the
size 14 and the black of descriptions; the `#222222` fill and line one pixel wide of the start and end circles; and
the `#F1F1F1` fill and half-pixel line of choices and histories.

##### CanvasNetPlantUml-Styling-SkinTheme-Immutable: A Change Returns A Copy

**Tests**: `SkinThemeTests.PlantUmlSkinTheme_With_ReturnsChangedCopy`

Asserts that changing the note's font size returns a theme with the new size while the default keeps 13 and the other
styles are shared.
