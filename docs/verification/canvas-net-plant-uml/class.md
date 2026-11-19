## Class Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Class` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Class`): the `ClassParser`, `ClassModel`, and `ClassRenderer` units
working together over the Svek engine.

### Verification Approach

The subsystem is verified by reading small original class and object diagram texts with `ClassParser` (after the
`Preprocessor` and `DiagramDetector` units of the Syntax subsystem), laying them out with `ClassModel.Layout` (which
lowers them to the Svek engine, used as is with the dagre layout of the CanvasNetLayout system), and asserting the
model and the scene: the entities and their ids, the roles and texts of the scene items, the colors of the skin
parameters, the reports, and that two layouts give the same scene. The Svek, Syntax, Styling, and Scenegraph subsystems
are used as is. No mocking is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access. Subsystem tests reside in `Class/ClassTests.cs`; the shared helpers (parsing a diagram
body, the context of a layout call) are in `Class/ClassTestHelpers.cs`. The pictures of the original fixtures are
compared with the PlantUML jar's in the system's reference shape tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. An id, a role, a text, a color, a count, or a line
that differs from the expected value constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Class-Reading: The Statements Are Read

**Tests**: `ClassTests.PlantUmlClass_ReadLayoutRender_DrawsTheModel`,
`ClassTests.PlantUmlClass_SkinAndReports_ReachTheDrawing`

Reads a diagram with a title, a package holding an abstract class with a private field and a public method and an
enum with two constants, an interface, a realization, a composition with end labels and a label with its direction
mark to a class that the link creates, an association, a note, an object, a map with an entry, and a link between
them; asserts the
qualified ids of the six entities in order. Reads a diagram with a crow's foot link and a `remove` statement and
asserts the two reports at their lines.

#### CanvasNetPlantUml-Class-Layout: The Diagram Is Lowered And Laid Out

**Tests**: `ClassTests.PlantUmlClass_ReadLayoutRender_DrawsTheModel`

Lays the read diagram out twice and asserts the six boxes, the cluster of the package, the note text, the link label,
the title, and that the two scenes have the same view box and number of items.

#### CanvasNetPlantUml-Class-Drawing: The Boxes Are Drawn

**Tests**: `ClassTests.PlantUmlClass_ReadLayoutRender_DrawsTheModel`,
`ClassTests.PlantUmlClass_SkinAndReports_ReachTheDrawing`

Asserts the four spots of the classes (none for the object and the map), the italic name of the abstract class, the
enum constant, the two visibility icons, and the map key; asserts the fill and the line of a class and the color of a
member from `classBackgroundColor`, `classBorderColor`, and `classAttributeFontColor`.

#### CanvasNetPlantUml-Class-Limits: The Diagram Is Bounded

**Tests**: `ClassParserTests.PlantUmlClassParser_Limits_FailClosedAtTheLine`

Asserts that the node, edge, label length, and nesting limits fail with a located `plantuml-limit-exceeded` error at
the line that crosses them (see the unit scenario).
