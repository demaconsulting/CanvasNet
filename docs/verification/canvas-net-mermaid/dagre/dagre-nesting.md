### DagreNesting Unit Verification Design

This document describes the unit-level verification strategy for the `DagreNesting` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static `DagreNesting` class (`Run`, `Cleanup`,
`ParentDummyChains`, and `AddBorderSegments`), the port of dagre's `nesting-graph.js`, `parent-dummy-chains.js`, and
`add-border-segments.js`.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `nesting-graph.test.js`, `parent-dummy-chains.test.js`, and
`add-border-segments.test.js`: a flat graph, a graph with one cluster, the clean-up after a run, an edge whose dummy
chain leaves a cluster, and a cluster that spans two ranks. The tests assert the nodes, edges, parents, weights,
minimum lengths, and graph label values that dagre's tests assert. No mocking or stubbing is used; the internal type is
reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `DagreNestingTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A missing or extra border node, nesting edge, or parent, or a length or rank factor that differs from dagre's,
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreNesting-ClusterBorders: Clusters Are Ranked Between Their Borders

**Tests**: `DagreNestingTests.DagreNesting_Run_FlatGraph_ConnectsRoot`, `DagreNestingTests.DagreNesting_Run_Cluster_AddsBorders`

Asserts that a flat graph gets a root joined to every node, edge lengths multiplied by 1, and a rank factor of 1; and
that a cluster gets a top and a bottom border node joined to its members, with every edge length multiplied by the rank
factor.

##### CanvasNetMermaid-Dagre-DagreNesting-Cleanup: The Nesting Graph Is Removed

**Tests**: `DagreNestingTests.DagreNesting_Cleanup_AfterRun_RemovesNestingGraph`

Asserts that after a run and the clean-up the root and every nesting edge are gone and the caller's edges remain.

##### CanvasNetMermaid-Dagre-DagreNesting-DummyChains: Dummy Nodes Follow The Cluster They Cross

**Tests**: `DagreNestingTests.DagreNesting_ParentDummyChains_EdgeLeavingCluster_DummiesFollowCluster`

Asserts that the dummy nodes of an edge that leaves a cluster are parented in the cluster while the cluster spans their
rank and at the top level after it.

##### CanvasNetMermaid-Dagre-DagreNesting-BorderSegments: Clusters Get Side Borders On Every Rank

**Tests**: `DagreNestingTests.DagreNesting_AddBorderSegments_ClusterOverTwoRanks_AddsChains`

Asserts that a cluster over two ranks gets a left and a right border node at each rank, with the cluster as parent and
the side recorded, joined from the first rank to the second.
