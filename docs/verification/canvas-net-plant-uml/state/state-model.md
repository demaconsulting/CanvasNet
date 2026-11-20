### StateModel Unit Verification Design

This document describes the unit-level verification strategy for the `StateModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.State` namespace.

#### Verification Approach

The unit is verified with models read by `StateParser` from small original bodies, asserting the facts, the lowered
Svek diagram, and the scene of a call. No mocking is required. Unit tests reside in `State/StateModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A count, an id, a parent, a setting, a role, or a text
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-State-StateModel-Facts: Facts

**Tests**: `StateModelTests.PlantUmlStateModel_Facts_CountStatesTransitionsAndComposites`

Asserts the simple states and pseudo-states as nodes, the composite states as subgraphs, and the transitions that do
not touch a note as edges.

##### CanvasNetPlantUml-State-StateModel-Lowering: Every Element Is Lowered

**Tests**: `StateModelTests.PlantUmlStateModel_Lower_CompositeStates_BecomeClusters`,
`StateModelTests.PlantUmlStateModel_Lower_ConcurrentRegions_BecomeRegionClusters`,
`StateModelTests.PlantUmlStateModel_Lower_ObservesCancellation`,
`StateModelTests.PlantUmlStateModel_Lower_GeneratedIds_NeverCollide`

Asserts the simple elements as entities in the cluster of their composite state, the composite states as clusters with
their images, and the links and notes in text order; asserts a composite state with concurrent regions as a cluster
with the arrangement of its first separator and one cluster without image per region holding the region's elements;
asserts that a canceled call stops with `OperationCanceledException`; asserts that with states named `note#1` and
`A#region1` and a floating note aliased `note#2` beside notes without an alias and a composite state with two
regions, every entity, cluster, and note of the lowered diagram has an id of its own and the layout draws.

##### CanvasNetPlantUml-State-StateModel-Layout: The Scene Of A Call

**Tests**: `StateModelTests.PlantUmlStateModel_Layout_DrawsTheScene`

Asserts that the scene draws every element with its role, the separators of the concurrent regions, and the page
texts, at the diagram's own scale, and that two calls give the same scene.
