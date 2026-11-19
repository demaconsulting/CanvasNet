### ActivityModel Unit Verification Design

This document describes the unit-level verification strategy for the `ActivityModel` unit in the
`DemaConsulting.CanvasNet.PlantUml.Activity` namespace.

#### Verification Approach

The unit is verified with models parsed from small original diagram bodies, asserting the facts, the order of the
reports, and the scene of a layout call. No mocking is required. Unit tests reside in `Activity/ActivityModelTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A fact, a report, or a scene value that differs from the
expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Activity-ActivityModel-Facts: Facts

**Tests**: `ActivityModelTests.PlantUmlActivityModel_Facts_CountNodesEdgesAndSubgraphs`,
`ActivityModelTests.PlantUmlActivityModel_Facts_CountDecisionsAndLoops`

Asserts 8 nodes, 11 edges, and no subgraph for a fork of three branches; 5 nodes, 4 edges, 3 subgraphs (two
swimlanes and a partition), and the title of a diagram with groups; and the nodes and edges of an if with an elseif and
an else followed by a while loop.

##### CanvasNetPlantUml-Activity-ActivityModel-Reports: Reports In Text Order

**Tests**: `ActivityModelTests.PlantUmlActivityModel_Reports_AreInTextOrder`

Asserts that the reports of an ignored skin parameter and of unsupported statements are in line order.

##### CanvasNetPlantUml-Activity-ActivityModel-Layout: The Scene Of A Call

**Tests**: `ActivityModelTests.PlantUmlActivityModel_Layout_DecoratesThePageAndScales`,
`ActivityModelTests.PlantUmlActivityModel_Registry_ParsesActivityDiagrams`

Asserts the diagram's scale, the theme's background, the title above the action, and the legend below it; asserts that
the document hands an activity diagram to the model and lays it out.
