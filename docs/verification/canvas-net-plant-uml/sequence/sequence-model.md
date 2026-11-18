### SequenceModel Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Sequence` namespace.

#### Verification Approach

The unit is verified with models parsed from small original diagram bodies, asserting the facts, the order of the
reports, and the scene of a layout call. No mocking is required. Unit tests reside in `Sequence/SequenceModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A fact, a report, or a scene value that differs from the
expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Sequence-SequenceModel-Facts: Facts

**Tests**: `SequenceModelTests.PlantUmlSequenceModel_Facts_CountParticipantsMessagesAndBoxes`

Asserts the title, two nodes, two edges, one subgraph, and a self message.

##### CanvasNetPlantUml-Sequence-SequenceModel-Reports: Reports In Text Order

**Tests**: `SequenceModelTests.PlantUmlSequenceModel_Reports_KeepParserReportsInOrder`

Asserts the ignored skin parameter of line 2 before the unsupported statement of line 3.

##### CanvasNetPlantUml-Sequence-SequenceModel-Layout: The Scene Of A Call

**Tests**: `SequenceModelTests.PlantUmlSequenceModel_Layout_GivesSceneWithBackgroundScaleAndPage`,
`SequenceModelTests.PlantUmlSequenceModel_Constructor_RejectsNull`

Asserts the background of a skin parameter, the diagram's scale, the title text, and the view box at the origin; asserts
`ArgumentNullException` for null participants, a null theme, and a null context.
