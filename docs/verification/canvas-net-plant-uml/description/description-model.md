### DescriptionModel Unit Verification Design

This document describes the unit-level verification strategy for the `DescriptionModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Description` namespace.

#### Verification Approach

The unit is verified with models read by `DescriptionParser` from small original bodies, asserting the facts, the
lowered Svek diagram, and the scene of a call. No mocking is required. Unit tests reside in
`Description/DescriptionModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A count, an id, a parent, a setting, a role, or a text
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Description-DescriptionModel-Facts: Facts

**Tests**: `DescriptionModelTests.PlantUmlDescriptionModel_Facts_CountLeavesLinksAndContainers`

Asserts the leaf elements as nodes, the containers as subgraphs, and the links that do not touch a note as edges (a
hidden link counted, the link of a floating note not), and the title.

##### CanvasNetPlantUml-Description-DescriptionModel-Lowering: Every Element Is Lowered

**Tests**: `DescriptionModelTests.PlantUmlDescriptionModel_Lower_ContainersBecomeClusters`,
`DescriptionModelTests.PlantUmlDescriptionModel_Lower_GeneratedIds_NeverCollide`,
`DescriptionModelTests.PlantUmlDescriptionModel_Lower_ObservesCancellation`

Asserts the containers as clusters under their parents with their images (a component container with its own image),
the leaf elements as entities in the cluster of their container, a link that ends at a container, and the links and
notes in text order; asserts that with elements named `note"1` and `group"1` beside a note without an alias and an
anonymous container, every entity, cluster, and note has an id of its own and the diagram is consistent; asserts that
a canceled call stops with `OperationCanceledException`.

##### CanvasNetPlantUml-Description-DescriptionModel-Layout: The Scene Of A Call

**Tests**: `DescriptionModelTests.PlantUmlDescriptionModel_Layout_DrawsTheScene`

Asserts that the scene draws the elements, the container, the note, the link label, and the title with their roles, at
the diagram's own scale over the theme's background.
