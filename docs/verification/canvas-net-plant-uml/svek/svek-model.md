### SvekModel Unit Verification Design

This document describes the unit-level verification strategy for the `SvekModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Svek` namespace.

#### Verification Approach

The unit is verified with small diagrams built in the tests from test entity images, asserting the defaults, the
members, the facts, and the validation. No mocking is required. Unit tests reside in `Svek/SvekModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A default, a member, a count, or an exception that
differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Svek-SvekModel-Elements: The Elements Of A Lowered Diagram

**Tests**: `SvekModelTests.PlantUmlSvekModel_Link_Defaults`,
`SvekModelTests.PlantUmlSvekModel_MembersOf_EntitiesNotesThenClusters`,
`SvekModelTests.PlantUmlSvekModel_Constructors_RefuseMissingParts`

Asserts the defaults of a link (length 2, weight 1, solid, no decorations, no hint); asserts that the members of a
cluster and of the top are the entities, then the floating notes, then the child clusters, in the order of the diagram,
without the attached notes; asserts that the constructors refuse an empty id and a missing image, text, or end.

##### CanvasNetPlantUml-Svek-SvekModel-Facts: The Facts Of A Diagram

**Tests**: `SvekModelTests.PlantUmlSvekModel_CountFacts_EntitiesLinksAndClusters`

Asserts the entities as nodes, the links that touch no note as edges, and the clusters with members as subgraphs, an
empty cluster and the regions of a cluster with concurrent regions not counted.

##### CanvasNetPlantUml-Svek-SvekModel-Validation: An Inconsistent Diagram Is Refused

**Tests**: `SvekModelTests.PlantUmlSvekModel_Validate_ConsistentDiagram_Passes`,
`SvekModelTests.PlantUmlSvekModel_Validate_Inconsistencies_Throw`

Asserts that a consistent diagram with clusters, notes, and links passes; asserts an `ArgumentException` for every kind
of inconsistency: an id used twice, a link end, parent, or note target outside the diagram, a link to an attached note,
a length below 1, a weight that is not positive, and a member of a cluster with concurrent regions that is not a region.
