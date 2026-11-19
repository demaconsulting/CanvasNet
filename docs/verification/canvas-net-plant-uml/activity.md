## Activity Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Activity` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Activity`): the `ActivityParser`, `ActivityModel`, `ActivityLayout`, and
`ActivityRenderer` units working together.

### Verification Approach

The subsystem is verified by reading original diagrams preprocessed and detected by the Syntax subsystem (used as is),
laying them out with the Styling and Scenegraph subsystems (used as is), and asserting the scene: the size, the action
boxes, the diamonds, the start and stop nodes, the texts, and the nesting of the blocks. No mocking is required; the
internal types are reached through the test assembly's `InternalsVisibleTo` access. Subsystem tests reside in
`Activity/ActivityTests.cs`; the shared helpers (parsing a diagram body, the context of a layout call, the bounds of a
shape) are in `Activity/ActivityTestHelpers.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and the read-only fixtures; no shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. A size, a box, a text, a color, or a count that
differs from the expected literal constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Activity-Reading: An Activity Diagram Is Read And Drawn

**Tests**: `ActivityTests.PlantUmlActivity_ParseLayoutRender_DrawsNodesActionsAndArrows`,
`ActivityTests.PlantUmlActivity_Fixtures_AllLayOutAndDraw`

Draws an if with two branches between a start and a stop and asserts PlantUML's size (230 by 326), four action
boxes, two diamonds, the start and stop nodes, and the test and arrow labels; parses every fixture into an activity
model and asserts a scene larger than the empty picture.

#### CanvasNetPlantUml-Activity-Drawing: Nested Blocks And Skin Parameters

**Tests**: `ActivityTests.PlantUmlActivity_NestedBlocks_AreDrawnInsideEachOther`,
`ActivityTests.PlantUmlActivity_SkinParams_ChangeTheDrawing`

Draws a while loop with a note and a repeat loop in the branches of a fork inside a partition in a swimlane, and
asserts that the actions are inside the frame, the note, the two swimlane titles, and the two bars; draws a diagram
with an activity background and an arrow color and asserts both.
