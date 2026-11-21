### SequenceRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceRenderer` unit in the
`DemaConsulting.CanvasNet.PlantUml.Sequence` namespace.

#### Verification Approach

The unit is verified by drawing small original diagram bodies through the parser and layout (used as is) and asserting
the scene items by their roles: their number, colors, strokes, dashes, and texts. No mocking is required. Unit tests
reside in `Sequence/SequenceRendererTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A count, a color, a stroke, or a text that differs from
the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Participants: Participant Shapes

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_Participants_DrawEveryKind`

Asserts a head, a tail, and a head text for each of the eight kinds, and the grey-blue fill and the half-pixel `#181818`
outline of a participant box.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Lifelines: Life Lines And Activations

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_LifelinesAndActivations`

Asserts dashed (5, 5) and dotted (1, 4) life line parts and a white activation box 10 pixels wide.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Messages: Arrows And Self Messages

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_Messages_DrawHeadsAndBodies`,
`SequenceRendererTests.PlantUmlSequenceRenderer_SelfMessage_LoopNumberAndLabel`,
`SequenceRendererTests.PlantUmlSequenceRenderer_ThickArrow_HeadsKeepAThinOutline`

Asserts nine heads for seven arrows (two of them unfilled: the open head and the cross), seven lines with only the
dotted one dashed; asserts the three lines and one head of a self message, its bold number, and its label. With
`sequenceArrowThickness 4`, asserts that the filled and both half heads are filled with a 1 pixel outline, that the
open head is drawn with width 4, and that every line is 4 wide.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Notes: Note Shapes

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_Notes_DrawShapes`

Asserts the two paths of a note, a hexagon of six corners, a rectangle, the note color, and three texts.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Groups: Group Frames

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_Groups_DrawTabFrameAndSections`

Asserts the tab, the frame, the fill, a dashed section line, and the texts `alt`, `[yes]`, and `[no]`.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Blocks: Dividers, Boxes, Delays, And Destroy

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_DividersBoxesDelaysAndDestroy`

Asserts the five paths of a divider with text and an empty one, the divider text, the box and its title, the delay text,
and the `#A80036` cross of a destroyed life line.

##### CanvasNetPlantUml-Sequence-SequenceRenderer-Placement: Offset And View Box

**Tests**: `SequenceRendererTests.PlantUmlSequenceRenderer_Render_MovesByOffsetAndSizesViewBox`

Asserts a positive offset for a note sticking out on the left, the view box of the body size, a head moved by the
offset, and the note outline at 15 pixels.
