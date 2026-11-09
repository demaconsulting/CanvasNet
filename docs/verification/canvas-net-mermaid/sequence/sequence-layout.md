### SequenceLayout Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceLayout` unit in the
`DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: the internal sealed `SequenceLayout` (`Draw`, the participant
margins and placement, the walk over the messages, the blocks, the boxes, the menus, and the view box, in four partial
files) and its layout state types (`SequenceBounds`, `SequenceActorLayout`, `SequenceBoxLayout`,
`SequenceMessageModel`, `SequenceNoteModel`, `SequenceLoopModel`, `SequenceLoopSection`, `SequenceActivation`,
`SequenceMessageState`, and `SequenceRectData`).

#### Verification Approach

The unit is verified by laying out short texts through the document pipeline (the helper `SceneQuery.Layout` parses a
text, resolves the configuration as a render does, and returns the scene of `SequenceModel.Layout`) and asserting the
geometry of the scene items that the layout places, found by their roles and ids (`SceneQuery.SequenceItems`): the
boxes of the participants and their mirrors, the ends of the message lines, the segments of self messages, the boxes
of activations, the lifelines, the sequence numbers, the title, and the view box. The expected values are Mermaid's
for the same text, worked out from `sequenceRenderer.ts` and confirmed against the pinned Mermaid release (for example
participants 150 wide and 50 apart, the first message line 44 below the participants, and the view box grown by the
diagram margins of 50 and 10). The layout's agreement with Mermaid on whole diagrams is verified as well by the
system-level reference layout tests, which compare every fixture with a mermaid-cli golden; the work limit and
cancellation of the layout are verified at the system level.

No mocking or stubbing is used; the parser, the model, the configuration, the style sheet, the text utilities, the
renderer, and the bundled fonts are real. The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `SequenceLayoutTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Sequence` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts, with which the texts are measured
- **Isolation**: Each test lays out its own text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position, size, number, or view box that differs from the asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Sequence-SequenceLayout-ActorMargins: Participant Widths And Margins

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_OneMessage_FollowsMermaidsMeasures`,
`SequenceLayoutTests.SequenceLayout_Draw_WideMessage_WidensOnlyTheNeighborsMargin`

Asserts that two participants of one message are 150 by 65 at x 0 and x 200; and that a message much wider than the
gap moves the second of two neighbors right so that the text fits with the wrap padding, while the same message
between participants that are not neighbors leaves the third participant at its default x of 400.

##### CanvasNetMermaid-Sequence-SequenceLayout-MessagePlacement: Messages And Self Messages

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_OneMessage_FollowsMermaidsMeasures`,
`SequenceLayoutTests.SequenceLayout_Draw_SelfMessage_IsALoopOrRightAngles`

Asserts that the line of one message runs from x 76 to x 271 at y 109 (the lifelines less the arrowhead room), with
the mirrored participant at y 129; and that a self message starts at the lifeline (76, 109) and is Mermaid's cubic to
(76, 129) through (136, 99) and (136, 139).

##### CanvasNetMermaid-Sequence-SequenceLayout-Activations: Activations Stack And End The Lines

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_Activations_StackAndMoveTheLines`

Asserts that two nested activations of one participant are drawn from x 270 to 280 and from x 275 to 285 (moved right
by half the activation width), and that the line of the second call ends at x 267, the left side of the activation
that is open when it is placed, less the arrowhead room.

##### CanvasNetMermaid-Sequence-SequenceLayout-CreateDestroy: Created And Destroyed Participants

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_CreateAndDestroy_MoveTheParticipants`

Asserts that a created participant is drawn at x 475, centered vertically on its creating line, which ends 7 pixels
before its box; and that a destroyed participant's mirror is drawn half its height above the destroying line, where
its lifeline ends.

##### CanvasNetMermaid-Sequence-SequenceLayout-SequenceNumbers: Numbering From Autonumber And The Setting

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_Autonumber_FollowsStartStepAndOff`,
`SequenceLayoutTests.SequenceLayout_Draw_ShowSequenceNumbers_NumbersEveryMessage`

Asserts that `autonumber`, `autonumber 10 5`, `autonumber off`, and `autonumber 0.1 0.2` number six messages `1`, `10`,
`15`, (none), `0.1`, and `0.3` (rounded to hundredths); and that `showSequenceNumbers` numbers both messages of a text
without `autonumber`.

##### CanvasNetMermaid-Sequence-SequenceLayout-Configuration: Mirrors, Hidden Participants, And Right Angles

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_MirrorActorsOff_LifelinesRunTo2000`,
`SequenceLayoutTests.SequenceLayout_Draw_HideUnusedParticipants_LeavesThemOut`,
`SequenceLayoutTests.SequenceLayout_Draw_SelfMessage_IsALoopOrRightAngles`

Asserts that with `mirrorActors: false` there is no participant at the bottom and the lifelines run to y 2000; that
with `hideUnusedParticipants: true` a participant that no message names is not drawn and the next participant takes
its place at x 200; and that with `rightAngles: true` a self message is a path of a move and three lines, 25 high, whose
right side is at x 151.

##### CanvasNetMermaid-Sequence-SequenceLayout-ViewBox: The View Box And The Title

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_OneMessage_FollowsMermaidsMeasures`,
`SequenceLayoutTests.SequenceLayout_Draw_Title_AddsRoomAboveTheDiagram`,
`SequenceLayoutTests.SequenceLayout_Draw_MirrorActorsOff_LifelinesRunTo2000`,
`SequenceLayoutTests.SequenceLayout_JsMaxAndJsMin_LeaveOutMissingValues`

Asserts that the view box of one message is (-50, -10, 450, 215); that a `title` statement adds 40 pixels at the top
(the view box starts at y -50) and is drawn at x 75 on the baseline y -25, and that a frontmatter title is drawn the
same way; that without mirrored participants the view box is 129 high; and that Mermaid's maximum and minimum helpers
leave out missing values and values that are not a number (negative infinity when none is left).
