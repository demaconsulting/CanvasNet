### StateRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `StateRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.State` namespace.

#### Verification Approach

The unit is verified by building the images of elements read by `StateParser` with the text layout of the package and
drawing them at a known place, asserting their sizes, clip shapes, and the items of the scene: roles, segments,
strokes, fills, and texts. The sizes and offsets are the ones the PlantUML jar draws for the same elements. No mocking
is required. Unit tests reside in `State/StateRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A size, a point, a shape, a stroke, a fill, or a text
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-State-StateRenderer-States: State Boxes

**Tests**: `StateRendererTests.PlantUmlStateRenderer_Image_SimpleState_HasPlantUmlBox`,
`StateRendererTests.PlantUmlStateRenderer_Image_Descriptions_AreDrawnBelowTheLine`,
`StateRendererTests.PlantUmlStateRenderer_Image_HideEmptyDescription_DropsTheLine`,
`StateRendererTests.PlantUmlStateRenderer_Image_Colors_ReplaceTheTheme`,
`StateRendererTests.PlantUmlStateRenderer_Helpers_RuleAndRoundedTop`

Asserts a state without descriptions at least 50 by 50, rounded with a radius of 12.5, its name centered 5 below the
top and its line 5 below the name; the descriptions 5 from the left and 5 below the line, the box as tall as the name
and the lines with margins of 10, and a rule of four dashes drawn as a line 10 high; after `hide empty description` a
box at least 50 by 40 without the line while a state with descriptions keeps it; the colors of the declaration
replacing the theme's; and that a rule is four or more dashes and the header band has a rounded top and a square
bottom.

##### CanvasNetPlantUml-State-StateRenderer-PseudoStates: Pseudo-States

**Tests**: `StateRendererTests.PlantUmlStateRenderer_Image_PseudoStates_HavePlantUmlShapes`,
`StateRendererTests.PlantUmlStateRenderer_Image_StartAndEnd_HavePlantUmlColors`,
`StateRendererTests.PlantUmlStateRenderer_Image_ForkBar_LiesAcrossTheRanks`,
`StateRendererTests.PlantUmlStateRenderer_Image_History_HasItsMark`

Asserts the size, clip shape, and role of every pseudo-state; the start filled and outlined `#222222` and the end a
ring around a filled circle of radius 6; the fork and join bar 80 by 8 filled `#555555` without an outline, 8 by 80
left to right; and `H` and `H*` centered in a circle of 22 filled `#F1F1F1`.

##### CanvasNetPlantUml-State-StateRenderer-Composites: Composite States

**Tests**: `StateRendererTests.PlantUmlStateRenderer_CompositeImage_HasHeaderAndOutline`

Asserts the header as tall as the name with margins of 5, the minimum width of the name with margins of 10, the
padding of 10, the filled header band, the rounded outline, the line under the name, and the centered title; a
composite state with its own color is filled whole.
