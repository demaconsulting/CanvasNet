## Sequence Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Sequence` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Sequence`): the `SequenceParser`, `SequenceModel`, `SequenceLayout`, and
`SequenceRenderer` units working together.

### Verification Approach

The subsystem is verified by reading original diagrams preprocessed and detected by the Syntax subsystem (used as is),
laying them out with the Styling and Scenegraph subsystems (used as is), and asserting the scene: the boxes of the
participants, the messages, the texts, and the nesting of the rows. No mocking is required; the internal types are
reached through the test assembly's `InternalsVisibleTo` access. Subsystem tests reside in `Sequence/SequenceTests.cs`;
the shared helpers (parsing a diagram body, the context of a layout call) are in `Sequence/SequenceTestHelpers.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and the read-only fixtures; no shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. A box, a text, a color, or a count that differs
from the expected literal constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Sequence-Reading: A Sequence Diagram Is Read And Drawn

**Tests**: `SequenceTests.PlantUmlSequence_Registry_ParsesSequenceDiagrams`,
`SequenceTests.PlantUmlSequence_ParseLayoutRender_DrawsParticipantsAndMessages`

Opens a sequence diagram and asserts that it is supported and parsed into a sequence model; draws two participants and
two messages and asserts the head box of the first participant (at the margin, its text plus 14 wide and 31.609 high),
the two message lines, their labels, and the two life lines.

#### CanvasNetPlantUml-Sequence-Drawing: Blocks, Skin Parameters, And Every Fixture

**Tests**: `SequenceTests.PlantUmlSequence_Blocks_AreDrawnInRows`,
`SequenceTests.PlantUmlSequence_SkinParams_ChangeTheDrawing`, `SequenceTests.PlantUmlSequence_Fixtures_AllLayOutAndDraw`

Draws a loop with an activation and a note, a divider, and a delay, and asserts that the note is inside the frame, the
divider below it, and one activation box; draws a diagram with a participant background and an arrow color and asserts
both and the white background; lays out every fixture and asserts a scene larger than the empty picture.
