### SequenceParser Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceParser` unit in the
`DemaConsulting.CanvasNet.PlantUml.Sequence` namespace.

#### Verification Approach

The unit is verified with small original diagram bodies preprocessed and detected by the Syntax subsystem (used as is,
because the parser reads located statements), asserting the participants, events, boxes, page texts, settings, reports,
and the codes and lines of the errors. No mocking is required. Unit tests reside in `Sequence/SequenceParserTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A participant, an event, a value, a report, a code, or a
line that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Sequence-SequenceParser-Participants: Declarations And First Uses

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Declarations_ReadKindsAliasesColorsAndOrder`,
`SequenceParserTests.PlantUmlSequenceParser_ImplicitParticipant_LaterDeclarationUpdatesIt`

Declares the eight kinds, a quoted name with an alias and a color, an alias with a quoted display, and an order value,
and asserts the kinds, codes, displays, the color, and that the ordered participant comes first; uses a participant
before declaring it as an actor and asserts one participant of the declared kind.

##### CanvasNetPlantUml-Sequence-SequenceParser-Messages: Arrows

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Arrows_ReadBodyHeadsAndDirection` (eleven arrows),
`SequenceParserTests.PlantUmlSequenceParser_Arrows_CircleAndColor`

Asserts the body, the heads at both ends, the direction, and the label of `->`, `-->`, `->>`, `-->>`, `->x`, `-\`,
`-/`, `<-`, `<--`, `<->`, and `-[#red]>`; asserts the circle of `->o`, an empty label, and the color of `-[#0000FF]>`.

##### CanvasNetPlantUml-Sequence-SequenceParser-Activations: Life Changes And Return

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_LifeChanges_AttachToTheirMessage`,
`SequenceParserTests.PlantUmlSequenceParser_Return_RepliesAndDeactivates`

Asserts a standalone activation before any message, the changes of `++`, `--`, `!!`, and of `activate` and
`deactivate` statements attached to the message before them; asserts that `return` replies dotted from the receiver to
the sender with its label and deactivates the receiver, and that a return with nothing to return to fails at line 3.

##### CanvasNetPlantUml-Sequence-SequenceParser-Autonumber: Numbering

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Autonumber_NumbersMessages`

Asserts the numbers `<b>1`, `<b>[010]`, `<b>[015]`, none after `autonumber stop`, and `<b>[020]` after `autonumber
resume`.

##### CanvasNetPlantUml-Sequence-SequenceParser-Notes: Note Forms

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Notes_ReadPositionsShapesAndText`

Asserts a note on a message's row, a colored note left of a participant, a note over two participants, a hexagon, a
rectangle, a note across all participants, and a multi-line note with an empty line.

##### CanvasNetPlantUml-Sequence-SequenceParser-Groups: Groups And Sections

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Groups_ReadKeywordsConditionsAndSections`

Asserts an `alt` with a color and a condition, a `loop` without one, a `group` with its title (keeping the space before
the bracket) and condition, the `else` condition, and the three ends.

##### CanvasNetPlantUml-Sequence-SequenceParser-Boxes: Boxes

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Boxes_ReadTitleColorAndMembers`

Asserts a quoted title with a color around two participants and a plain title with a trailing color closed by
`endbox`.

##### CanvasNetPlantUml-Sequence-SequenceParser-Separators: Dividers, Delays, And Spaces

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Separators_ReadDividersDelaysAndSpaces`

Asserts a divider's text, a delay without and with a text, the default space of 25 and a space of 45, and that a space
that is not a number fails at line 3.

##### CanvasNetPlantUml-Sequence-SequenceParser-PageTexts: Page Texts

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_PageTexts_ReadTextsAndAlignments`

Asserts a title, a left-aligned header, a footer block centered by default, a caption, and a left-aligned legend.

##### CanvasNetPlantUml-Sequence-SequenceParser-Settings: Foot Box, Scale, And Skin

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Settings_ApplyFootboxScaleAndSkin`

Asserts the hidden foot box, the scale 3/2, and a participant background from a skin parameter.

##### CanvasNetPlantUml-Sequence-SequenceParser-Unsupported: Statements That Are Not Drawn

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_UnsupportedStatements_AreReported`

Asserts nine `plantuml-statement-unsupported` reports (a stereotype, `**`, a one-line and a block reference,
`newpage`, a border message, `hide unlinked`, `scale 200 width`, and a parallel message) and the three messages that are
still read.

##### CanvasNetPlantUml-Sequence-SequenceParser-Errors: Located Syntax Errors

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_InvalidStatement_FailsLocated` (ten statements),
`SequenceParserTests.PlantUmlSequenceParser_Blocks_InvalidFailLocated` (seven texts)

Asserts the code, the line, and a hint for an unknown statement, a message without a receiver or with text after it,
incomplete declarations and life statements, an invalid color, notes without a position, without a message, and not
closed, `else` and `end` outside a group, an unclosed group, a nested box, an unclosed box, `end box` without a box, and
an `end` that closes nothing.

##### CanvasNetPlantUml-Sequence-SequenceParser-Limits: Limits

**Tests**: `SequenceParserTests.PlantUmlSequenceParser_Limits_AreEnforced`

Asserts `plantuml-limit-exceeded` at the statement over `MaxNodes`, `MaxEdges`, `MaxLabelLength`, and
`MaxNestingDepth`.
