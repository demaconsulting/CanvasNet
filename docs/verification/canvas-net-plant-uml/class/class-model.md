### ClassModel Unit Verification Design

This document describes the unit-level verification strategy for the `ClassModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Class` namespace.

#### Verification Approach

The unit is verified with models read by `ClassParser` from small original bodies and with member and body lines,
asserting the facts, the portions of the hide and show rules, the lowered Svek diagram, the scene of a call, and the
member and body rules. No mocking is required. Unit tests reside in `Class/ClassModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A count, a flag, an id, a setting, or a text that
differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Class-ClassModel-Members: Member And Body Rules

**Tests**: `ClassModelTests.PlantUmlClassModel_Member_ReadsVisibilityAndModifiers`,
`ClassModelTests.PlantUmlClassModel_Body_SeparatesFieldsAndMethods`

Asserts the visibility character, the static and abstract modifiers, the tags, the guillemets, and an empty member;
asserts the separators and their titles, methods and fields, and the split of a body without separators.

##### CanvasNetPlantUml-Class-ClassModel-Facts: Facts

**Tests**: `ClassModelTests.PlantUmlClassModel_Facts_CountEntitiesLinksAndPackages`

Asserts the entities with notes not counted, the links that do not touch a note with hidden links counted, and the
packages that hold something.

##### CanvasNetPlantUml-Class-ClassModel-Portions: Hide And Show In Order

**Tests**: `ClassModelTests.PlantUmlClassModel_PortionsOf_AppliesRulesInOrder`

Asserts rules applied in order to every entity, to a kind, a stereotype, or a name, and `empty` applied only to empty
compartments.

##### CanvasNetPlantUml-Class-ClassModel-Lowering: Every Element Is Lowered

**Tests**: `ClassModelTests.PlantUmlClassModel_Lower_MapsEveryElement`

Asserts every package as a cluster under its parent, every entity with its image and package, the notes with their
targets, links, and order, and the links with their decorations and settings.

##### CanvasNetPlantUml-Class-ClassModel-Layout: The Scene Of A Call

**Tests**: `ClassModelTests.PlantUmlClassModel_Layout_DrawsBodyAndPageTexts`

Asserts that the scene holds the boxes, the clusters, the links, and the page texts around them, at the diagram's own
scale.
