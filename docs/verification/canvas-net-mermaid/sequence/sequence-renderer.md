### SequenceRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: the internal sealed `SequenceRenderer` (the participant types,
messages and markers, sequence numbers, central connections, notes, activations, frames, backgrounds, boxes, menus,
texts, and the paint order, in five partial files), `SceneSlot`, and `SequenceTextData`.

#### Verification Approach

The unit is verified by laying out short texts through the document pipeline (the helper `SceneQuery.Layout`) and
asserting the scene items the renderer draws, found by their roles and ids (`SceneQuery.SequenceItems`): the marker
names at the ends of every arrow, the strokes and dashes of the lines, the placement of the markers, the parts of the
participant drawings, the start of each lifeline, the paint of notes, activations, frames, backgrounds, boxes, and
menus, the circles and numbers, the texts, and the paint order. The expected values are Mermaid's for the same text
and the default theme, worked out from `svgDraw.js` and the theme's style sheet and confirmed against the pinned
Mermaid release (for example the actor fill `#ECECFF` and border `#9370DB`, the note fill `#fff5ad`, and the signal
color `#333333`). The `DisplayText` function is called directly. The renderer's agreement with Mermaid on whole
diagrams, paint and paint order included, is verified as well by the system-level reference layout tests.

No mocking or stubbing is used; the parser, the model, the configuration, the style sheet, the text utilities, the
layout, and the bundled fonts are real. The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `SequenceRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Sequence` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts, with which the texts are measured
- **Isolation**: Each test lays out its own text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any marker, stroke, color, position, size, text, order, or failure that differs from the asserted Mermaid
value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Sequence-SequenceRenderer-ParticipantTypes: The Eight Participant Types

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawActor_Actor_IsAStickFigure`,
`SequenceRendererTests.SequenceRenderer_DrawActor_Type_StartsTheLifelineBelowIt`,
`SequenceRendererTests.SequenceRenderer_DrawActor_Control_HasTheFilledHeadControl`,
`SequenceRendererTests.SequenceRenderer_DrawActor_UnknownType_DrawsNothing`

Asserts that an `actor` is a stick figure of four limbs and a head of radius 15 at (60, -5), 65 high, with one label;
that the lifeline of each of the eight types (a theory) starts below its drawing, at y 65 for `participant`,
`collections`, and `queue`, 75 for `control`, `entity`, and `database`, and 80 for `actor` and `boundary`, and that
each type is drawn at the top and at the bottom; that a `control` carries the `filled-head-control` marker in the
participant fill and border, 2.4 wide; and that a participant of the unknown type `robot` draws neither a box nor a
lifeline while the other participant is drawn.

##### CanvasNetMermaid-Sequence-SequenceRenderer-Arrows: Markers, Lines, And Central Connections

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawMessage_Arrow_HasMermaidsMarkers`,
`SequenceRendererTests.SequenceRenderer_DrawMessage_Lines_AreSolidOrDashed`,
`SequenceRendererTests.SequenceRenderer_DrawMessage_Markers_FollowTheLine`,
`SequenceRendererTests.SequenceRenderer_DrawMessage_CentralConnection_DrawsCircles`

Asserts, for 22 arrows (a theory), Mermaid's marker names at the start and the end of the line: none for the open
arrows, `arrowhead` at the end (and at the start for the bidirectional arrows), `crosshead`, `filled-head`, the solid
and stick half heads at the end, and the opposite half heads at the start of the reverse arrows; that a solid line is
1.5 wide in `#333333` and a dotted line is dashed 3, 3; that the arrowhead's tip is 2.1 to 3.5 pixels past the end of
the line and 10 to 11.5 high, mirrored for a line to the left, and that the filled head scales with the stroke width
(9 units times 1.5); and that a central connection at both ends draws two circles of radius 5 on the lifelines at
(70, 104) and (270, 104) in `#333333`.

##### CanvasNetMermaid-Sequence-SequenceRenderer-SequenceNumbers: Numbers In Circles

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawMessage_SequenceNumber_IsACircleWithTheNumber`

Asserts that the number of `autonumber 1000` is a circle 24 pixels across at (63, 97) at the sender's end of the line,
with the text `1000` in white at a size of 9 pixels.

##### CanvasNetMermaid-Sequence-SequenceRenderer-Frames: Block Frames And Empty Blocks

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawLoop_Frame_HasDashedSidesSeparatorsAndLabel`,
`SequenceRendererTests.SequenceRenderer_DrawLoop_EmptyBlock_FailsLocated`

Asserts that an `alt` block with an `else` section has four sides dashed 2, 2 and one separator dashed 3, 3, all 2
pixels wide in `#9370DB`, a label box of six segments (its cut corner) filled `#ECECFF`, and the texts `alt`, `[Yes]`,
and `[No]`; and that a `loop` with nothing inside is a `MermaidRenderException` of code `mermaid-render-failed` at
line 3, its start.

##### CanvasNetMermaid-Sequence-SequenceRenderer-BackgroundsAndBoxes: Backgrounds And Boxes Behind Everything

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawBackgroundRect_IsBehindEverythingWithoutStroke`,
`SequenceRendererTests.SequenceRenderer_DrawBox_IsBehindWithBorderTitleAndColor`

Asserts that the background of `rect rgb(1, 2, 3)` is the first item of the scene, filled with that color, without a
stroke or a shadow, and that `rect nonsense` takes the inherited root fill `#333333`; and that `box ButtonFace Team` is
the first item of the scene, a box filled with the browser's `ButtonFace` (240, 240, 240) and stroked in the node
border `#9370DB`, at (-20, -5), with the title `Team`.

##### CanvasNetMermaid-Sequence-SequenceRenderer-Paint: Theme Paint And Properties Classes

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawNoteAndActivation_UseTheThemeColors`,
`SequenceRendererTests.SequenceRenderer_DrawActor_PropertiesClass_ReplacesTheActorPaint`

Asserts that a note is filled `#fff5ad` and stroked `#aaaa33` and an activation (id `activation0`) filled `#f4f4f4`
and stroked `#666666`, the theme's colors over Mermaid's attributes; and that a `properties` class (a theory) gives the
participant box the note fill attribute `#EDF2AE` for a class without a rule and for a number, the `.note` rule's
`#fff5ad` for the class `note`, and the actor fill `#ECECFF` for an empty class.

##### CanvasNetMermaid-Sequence-SequenceRenderer-PaintOrder: Mermaid's Paint Order

**Tests**: `SequenceRendererTests.SequenceRenderer_Items_PaintOrder_IsMermaids`

Asserts that a message and a note give the order: the mirrored participants `B` and `A`, then the lifeline and the box
of `B`, then those of `A` (each lowered to the back), then the note, then the message.

##### CanvasNetMermaid-Sequence-SequenceRenderer-Text: Text Shown And The Old Placement

**Tests**: `SequenceRendererTests.SequenceRenderer_DisplayText_DecodesEntityPlaceholders`,
`SequenceRendererTests.SequenceRenderer_TextPlacementOld_DrawsStrokedLabels`

Asserts that Mermaid's entity placeholders for a numeric reference and for `&amp;` become their characters and that a
plain text is unchanged; and that with `textPlacement: old` a participant label is one text `One<br/>Two` as written,
anchored in the middle at (75, 37.5), filled with the participant fill `#ECECFF` and stroked with the participant
border `#9370DB`.

##### CanvasNetMermaid-Sequence-SequenceRenderer-Menus: Hidden And Forced Menus

**Tests**: `SequenceRendererTests.SequenceRenderer_DrawPopup_Hidden_IsEmptyAndGrowsTheViewBox`,
`SequenceRendererTests.SequenceRenderer_DrawPopup_Forced_DrawsThePanelAndLinks`,
`SequenceRendererTests.SequenceRenderer_DrawPopup_ActorWithLinks_FailsLocated`

Asserts that a participant with six links has an empty menu group `actor0_popup` and a view box 276 pixels high (the
participant, 20 pixels and 30 per link of the panel, the margin, and the bottom adjustment); that with `forceMenus`
the menu panel is drawn at (0, 65), 150 by 80, in the participant fill with a drop shadow, with the link texts `Home`
and `Help` at x 10; and that links on an `actor` are a `MermaidRenderException` of code `mermaid-render-failed` at
line 2, the participant's declaration.
