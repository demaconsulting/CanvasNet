## State Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `State` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.State`): the `StateParser`, `StateModel`, and `StateRenderer` units
working together over the Svek engine.

### Verification Approach

The subsystem is verified by reading small original state diagram texts with `StateParser` (after the `Preprocessor`
and `DiagramDetector` units of the Syntax subsystem), laying them out with `StateModel.Layout` (which lowers them to the
Svek engine, used as is with the dagre layout of the CanvasNetLayout system), and asserting the model and the scene:
the elements and their ids, the roles and texts of the scene items, the colors of the skin parameters, the reports, and
that two layouts give the same scene. The Svek, Syntax, Styling, and Scenegraph subsystems are used as is. No mocking is
required; the internal types are reached through the test assembly's `InternalsVisibleTo` access. Subsystem tests
reside in `State/StateTests.cs`; the shared helpers (parsing a diagram body, the context of a layout call) are in
`State/StateTestHelpers.cs`. The pictures of the original fixtures are compared with the PlantUML jar's in the system's
reference shape tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. An id, a role, a text, a color, a count, or a line
that differs from the expected value constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-State-Reading: The Statements Are Read

**Tests**: `StateTests.PlantUmlState_ReadLayoutRender_DrawsTheModel`,
`StateTests.PlantUmlState_SkinAndReports_ReachTheDrawing`

Reads a diagram with a title, states with descriptions, the start and end pseudo-states, a choice, a fork, a composite
state with a history named from outside (`Playing[H]`), a composite state with concurrent regions, a note, and
labeled transitions, and asserts the elements by their PlantUML ids (the history as `Playing..historical.Playing`) and
no report. Reads a diagram with a cross at the start of a transition and an `<<entryPoint>>` state and asserts the two
reports at their lines, in text order.

#### CanvasNetPlantUml-State-Layout: The Diagram Is Lowered And Laid Out

**Tests**: `StateTests.PlantUmlState_ReadLayoutRender_DrawsTheModel`

Lays the read diagram out twice and asserts the two composite states, the region separator of the composite state with
concurrent regions, the note text, a transition label, the title, and that the two scenes have the same view box and
number of items.

#### CanvasNetPlantUml-State-Drawing: The Shapes Are Drawn

**Tests**: `StateTests.PlantUmlState_ReadLayoutRender_DrawsTheModel`,
`StateTests.PlantUmlState_SkinAndReports_ReachTheDrawing`

Asserts the name and the description of a state, the choice diamond, the fork bar, and the history circle by their
roles and ids; asserts the fill and the line of a state and the color of a description from `stateBackgroundColor`,
`stateBorderColor`, and `stateAttributeFontColor`.

#### CanvasNetPlantUml-State-Limits: The Diagram Is Bounded

**Tests**: `StateParserTests.PlantUmlStateParser_Limits_FailClosedAtTheLine`

Asserts that the node, edge, label length, and nesting limits fail with a located `plantuml-limit-exceeded` error at
the line that crosses them (see the unit scenario).
