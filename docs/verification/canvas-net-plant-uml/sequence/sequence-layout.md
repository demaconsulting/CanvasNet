### SequenceLayout Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceLayout` unit in the
`DemaConsulting.CanvasNet.PlantUml.Sequence` namespace.

#### Verification Approach

The unit is verified by laying out models parsed from small original diagram bodies (the Syntax, Styling, and Scenegraph
subsystems used as is) and asserting the placed geometry against the values PlantUML 1.2026.10 gives (the whole-diagram
comparison with PlantUML is the system test `CanvasNetPlantUml_Sequence_ReferenceLayout_MatchesPlantUml`). No mocking is
required. Unit tests reside in `Sequence/SequenceLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A position or size that differs from the expected value
by more than its stated precision constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Sequence-SequenceLayout-Columns: Columns

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Columns_MessagePushesReceiver`,
`SequenceLayoutTests.PlantUmlSequenceLayout_SelfMessage_PushesNextParticipant`

Asserts the first column at the margin (its life line at 21.669) and a receiver pushed to its text (27.466) plus 24;
asserts a self message's loop 13 pixels high, leaving at the life line, its text 7 pixels right of it, and the next
participant past the longer label plus 14.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Rows: Rows

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Rows_StackMessagesBelowHeads`

Asserts the heads ending at 42.609, the first arrow at 72.961, the next 30.352 lower, the tails at 121.313, the height
with the foot box and the margin, and the direction of the reply.

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_ThickArrow_LeavesTheLabelsClearOfTheLine`

Lays out three messages at the default thickness and with `sequenceArrowThickness 4`, and asserts that the thick
arrow is 1.5 pixels lower under the same label top and that the next row starts 1.5 pixels later; with
`responseMessageBelowArrow`, asserts that the label below the response arrow is 1.5 pixels further from it.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Activations: Activation Boxes

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Activations_BoxesAndArrowEnds`

Asserts two boxes on one life line (the nested one 5 pixels right), the start of the first at its arrow, the arrow ends
at the boxes on both sides, and the end of the nested box at the deactivating arrow.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Notes: Notes

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Notes_PlacedBesideOverAndOnRows`

Asserts a note on a message's row 5 pixels right of the life line and 5 pixels below the row top, a left note whose
outline is moved to 15 pixels from the left, and a note across two participants as wide as their columns.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Groups: Group Frames

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Groups_FrameAroundContent`

Asserts the frame 16 pixels left of its first life line, the tab as wide as the keyword plus 45, one section, and the
next participant past the frame of a wide condition.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Boxes: Boxes

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Boxes_AroundParticipants`

Asserts a box widened for its title, the next participant 20 pixels after it, and the heads below the title.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Separators: Dividers, Delays, And Spaces

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Separators_DividersDelaysAndSpaces`

Asserts a divider from the left margin to the right one, the dotted parts of the two life lines in the delay, the delay
text, and a space of 40 after a 14-pixel message row.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Footbox: Foot Box And Destroy

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_HiddenFootboxAndDestroy`

Asserts no tails with a hidden foot box, the height 10 pixels below the life lines, and a destroyed life line ending 8
pixels below its cross.

##### CanvasNetPlantUml-Sequence-SequenceLayout-Limits: Work And Cancellation

**Tests**: `SequenceLayoutTests.PlantUmlSequenceLayout_Limits_WorkAndCancellation`

Asserts `plantuml-limit-exceeded` at line 2 for 300 messages under a work limit of 100, and `OperationCanceledException`
for a canceled token.
