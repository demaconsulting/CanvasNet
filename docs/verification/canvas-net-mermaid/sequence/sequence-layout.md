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
of activations, the lifelines, the sequence numbers, the notes, the lines of the block frames, the rect backgrounds,
the boxes and their titles, the menu panels, the title, and the view box. The expected values are Mermaid's
for the same text, worked out from `sequenceRenderer.ts` and confirmed against the pinned Mermaid release (for example
participants 150 wide and 50 apart, the first message line 44 below the participants, and the view box grown by the
diagram margins of 50 and 10). The expected values for notes, blocks, boxes, and menus were recorded by running the
reference renderer (`benchmarks/visual-diff/render-mermaid.mjs`, the pinned Mermaid release) on the same texts. The
layout's agreement with Mermaid on whole diagrams is verified as well by the
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
`SequenceLayoutTests.SequenceLayout_Draw_WideMessage_WidensOnlyTheNeighborsMargin`,
`SequenceLayoutTests.SequenceLayout_Draw_WrapAfterMeasuredBreak_KeepsTheLaterBreak`

Asserts that two participants of one message are 150 by 65 at x 0 and x 200; and that a message much wider than the
gap moves the second of two neighbors right so that the text fits with the wrap padding, while the same message
between participants that are not neighbors leaves the third participant at its default x of 400; and that,
with wrapping on, a wrapped message with a break after a measured message with a later break keeps its break, so the
second participant is at x 335 as the reference draws it.

##### CanvasNetMermaid-Sequence-SequenceLayout-MessagePlacement: Messages And Self Messages

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_OneMessage_FollowsMermaidsMeasures`,
`SequenceLayoutTests.SequenceLayout_Draw_SelfMessage_IsALoopOrRightAngles`

Asserts that the line of one message runs from x 76 to x 271 at y 109 (the lifelines less the arrowhead room), with
the mirrored participant at y 129; and that a self message starts at the lifeline (76, 109) and is Mermaid's cubic to
(76, 129) through (136, 99) and (136, 139).

##### CanvasNetMermaid-Sequence-SequenceLayout-NotePlacement: Notes Beside, Over, And Across Participants

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_Notes_BesideOverAndAcrossParticipants`,
`SequenceLayoutTests.SequenceLayout_Draw_NoteAlign_AnchorsTheNoteText`

Asserts that, for two participants, six notes are placed one under another, each 37 high and 47 below the one before
it: left of A at (-100, 75) and right of A at (100, 122), both 150 wide; over B at (279, 169), 150 wide; over A,B and
over B,A both at x 50 and 329 wide, at y 216 and 263; and a wide note over A at (-154, 310), 458 wide, which moves B
to x 279 (200 without it), with the view box (-204, -10, 683, 453). Asserts as well that with `noteAlign: left` a note
over A,B at (50, 75, 250, 37) and a note right of B at (300, 122, 150, 37) have their texts at x 60 and 310 anchored
at the start, and that with `noteAlign: right` the same notes have their texts at x 290 and 440 anchored at the end.

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

##### CanvasNetMermaid-Sequence-SequenceLayout-Blocks: Frames, Sections, And Backgrounds, Nested

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_NestedLoopAndAlt_FramesEncloseTheirSections`,
`SequenceLayoutTests.SequenceLayout_Draw_NestedRects_BackgroundsEncloseTheirMessages`

Asserts that a loop holding a message and an alt with an else section is framed from (54, 75) to (296, 352); that the
alt frame inside it runs from (64, 164) to (286, 342), 10 pixels inside on each side, with its else separator at y 258
and the section title `[No]` at x 175; and that the view box is (-50, -10, 450, 458). Asserts as well that a rect
holding a message and a nested rect has the backgrounds (54, 75, 242, 138) and (64, 139, 222, 64), in that scene
order, with the view box (-50, -10, 450, 319).

##### CanvasNetMermaid-Sequence-SequenceLayout-Boxes: Boxes And Their Titles

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_Boxes_EncloseTheirParticipantsBelowTheTitles`,
`SequenceLayoutTests.SequenceLayout_Draw_BoxTitleWiderThanParticipants_CentersThem`

Asserts that a box titled "Front end" around A and B is (-20, -5, 400, 334) with its title at x 180, that an untitled
box around C is (400, -5, 200, 334) with no text, that the participants A, B, C, and D are at x 5, 205, 425, and 640
and y 27, below the titles, and that the view box is (-50, -10, 890, 340). Asserts as well that a box whose title is
wider than its only participant is (-20, -5, 322, 246) with its title at x 141, that the participant is centered in it
at (66, 27, 150, 65), that the next participant is at x 342, and that the view box is (-50, -10, 592, 252).

##### CanvasNetMermaid-Sequence-SequenceLayout-Menus: Menus, Shown Or Hidden, Take Their Room

**Tests**: `SequenceLayoutTests.SequenceLayout_Draw_HiddenMenus_GrowTheViewBox`,
`SequenceLayoutTests.SequenceLayout_Draw_ForceMenus_PanelBelowTheParticipant`

Asserts that the view box of one message is (-50, -10, 450, 215) without links, (-50, -10, 450, 336) when B has eight
links (a hidden panel 20 + 8 x 30 = 260 high), and (-50, -10, 639, 215) when B has one link with a label wider than B,
while the hidden menu draws nothing. Asserts as well that with `forceMenus: true` the menu panel of B with two links is
drawn at (200, 65, 339, 80), below B, 20 + 2 x 30 high, and as wide as the wide label, with both link texts at x 210,
and that the view box is (-50, -10, 639, 215).

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
