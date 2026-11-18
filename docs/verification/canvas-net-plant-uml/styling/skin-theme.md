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
`SkinThemeTests.PlantUmlSkinTheme_Default_HasPlantUmlActivityLook`

Asserts the participant font, fill, line, width, corners, and padding, the square actor, the note, arrow, group, life
line, header, and title styles, the white background, the foot box, and the arrow thickness; and the activity, swimlane,
and bar styles.

##### CanvasNetPlantUml-Styling-SkinTheme-Immutable: A Change Returns A Copy

**Tests**: `SkinThemeTests.PlantUmlSkinTheme_With_ReturnsChangedCopy`

Asserts that changing the note's font size returns a theme with the new size while the default keeps 13 and the other
styles are shared.
