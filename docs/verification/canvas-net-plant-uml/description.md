## Description Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Description` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Description`): the `DescriptionParser`, `DescriptionModel`, and
`DescriptionRenderer` units working together over the Svek engine.

### Verification Approach

The subsystem is verified by reading small original component and use case diagram texts with `DescriptionParser` (after
the `Preprocessor` unit of the Syntax subsystem, with a `component` detected type), laying them out with
`DescriptionModel.Layout` (which lowers them to the Svek engine, used as is with the dagre layout of the CanvasNetLayout
system), and asserting the model and the scene: the elements and their ids, the roles and texts of the scene items, the
colors of the skin parameters, the reports, and that two layouts give the same scene. The Svek, Syntax, Styling, and
Scenegraph subsystems are used as is. No mocking is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access. Subsystem tests reside in `Description/DescriptionTests.cs`; the shared helpers (parsing a
diagram body, the context of a layout call, drawing an element) are in `Description/DescriptionTestHelpers.cs`. The
pictures of the original fixtures are compared with the PlantUML jar's in the system's reference shape tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. An id, a role, a text, a color, a count, or a line
that differs from the expected value constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Description-Reading: The Statements Are Read

**Tests**: `DescriptionTests.PlantUmlDescription_ReadLayoutRender_DrawsTheModel`,
`DescriptionTests.PlantUmlDescription_SkinAndReports_ReachTheDrawing`

Reads a diagram with a title, a component with a stereotype in a package, a component container in a node with a
database, a cloud, an interface written `() HTTP`, a name only written in a link (an interface), a note, and labeled
links, and asserts the facts (six leaves, five links, three containers) and no report. Reads a diagram with an
artifact and a middle decoration and asserts the two reports at their lines, in text order.

**Tests**: `DescriptionTests.PlantUmlDescription_UseCase_DrawsActorsAndUseCases`

Reads a use case diagram with an actor, a use case and a business use case in a boundary, a name only written in a
link (an actor), and an `<<include>>` link, and asserts the facts (four leaves, three links, one boundary) and no
report.

#### CanvasNetPlantUml-Description-Layout: The Diagram Is Lowered And Laid Out

**Tests**: `DescriptionTests.PlantUmlDescription_ReadLayoutRender_DrawsTheModel`

Lays the read diagram out twice and asserts the container title, the note text, a link label, the title, and that the
two scenes have the same view box and number of items.

#### CanvasNetPlantUml-Description-Drawing: The Shapes Are Drawn

**Tests**: `DescriptionTests.PlantUmlDescription_ReadLayoutRender_DrawsTheModel`,
`DescriptionTests.PlantUmlDescription_SkinAndReports_ReachTheDrawing`

Asserts the name, the stereotype, and the icon of a component, the outline of an interface, and the detail of a
database by their roles and ids; asserts the fill and the line of a component and the color of an interface's name
from `componentBackgroundColor`, `componentBorderColor`, and `interfaceFontColor`.

**Tests**: `DescriptionTests.PlantUmlDescription_UseCase_DrawsActorsAndUseCases`

Asserts the head of the actor named only in a link outlined in the color of `actorBorderColor`, the ellipse of a use
case filled with `usecaseBackgroundColor`, the slash of the business use case, the `«include»` link label, and the
boundary's title.

#### CanvasNetPlantUml-Description-Limits: The Diagram Is Bounded

**Tests**: `DescriptionParserTests.PlantUmlDescriptionParser_Limits_FailClosedAtTheLine`

Asserts that the node, edge, label length, and nesting limits fail with a located `plantuml-limit-exceeded` error at
the line that crosses them (see the unit scenario).
