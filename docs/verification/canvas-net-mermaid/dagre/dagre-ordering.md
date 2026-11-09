### DagreOrdering Unit Verification Design

This document describes the unit-level verification strategy for the `DagreOrdering` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static partial `DagreOrdering` class (`Order`,
`InitOrder`, `BuildLayerGraph`, `SortSubgraph`, `Barycenters`, `ResolveConflicts`, `Sort`, `AddSubgraphConstraints`,
and `CrossCount`), the port of dagre's `order/` folder.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `order/*.test.js`: weighted crossing counts of small
layerings, barycenter sorts with ties and entries without a barycenter, conflict resolution with and without
constraints, the barycenters of a movable layer, the initial order of two chains, the constraints of sibling clusters,
and a whole ordering that removes a crossing. The expected orders, counts, and merged entries are those that dagre's
tests assert. No mocking or stubbing is used; the internal type is reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `DagreOrderingTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any order, crossing count, barycenter, or merged entry that differs from dagre's constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreOrdering-InitialOrder: The Initial Order Follows A Depth-First Search

**Tests**: `DagreOrderingTests.DagreOrdering_InitOrder_TwoChains_OrderedBySearch`

Asserts that two chains are ordered rank by rank in the order a depth-first search from the nodes in rank order reaches
them.

##### CanvasNetMermaid-Dagre-DagreOrdering-Sweeps: Sweeps Remove Crossings

**Tests**: `DagreOrderingTests.DagreOrdering_Order_CrossingGraph_RemovesCrossing`

Asserts that the whole ordering of a graph whose initial order has a crossing ends with no crossing.

##### CanvasNetMermaid-Dagre-DagreOrdering-CrossCount: Crossings Are Counted With Their Weights

**Tests**: `DagreOrderingTests.DagreOrdering_CrossCount_Layering_CountsWeightedCrossings`

Asserts the crossing counts of small layerings: none for an empty layering and for parallel edges, one for a crossing
pair, six for a crossing pair of weights 2 and 3, and the sum over several rank pairs.

##### CanvasNetMermaid-Dagre-DagreOrdering-Barycenter: Nodes Are Sorted By Barycenter

**Tests**: `DagreOrderingTests.DagreOrdering_Barycenters_MovableLayer_WeightedAverages`,
`DagreOrderingTests.DagreOrdering_Sort_Entries_SortedByBarycenter`

Asserts that a node without predecessors has no barycenter and the others have the weighted average order of their
predecessors; and that entries are sorted by barycenter, ties by index biased left or right, with the entries without a
barycenter kept at their original index.

##### CanvasNetMermaid-Dagre-DagreOrdering-ClusterConstraints: Clusters Stay Together And In Order

**Tests**: `DagreOrderingTests.DagreOrdering_ResolveConflicts_NoConflict_Unchanged`,
`DagreOrderingTests.DagreOrdering_ResolveConflicts_Conflicts_Coalesced`,
`DagreOrderingTests.DagreOrdering_ResolveConflicts_NoBarycenter_MergedOnlyWithConstraint`,
`DagreOrderingTests.DagreOrdering_AddSubgraphConstraints_SiblingClusters_Constrained`

Asserts that entries without constraints, or whose constraints hold, are returned unchanged; that entries whose
barycenters contradict a constraint are merged with the weighted barycenter; that an entry without a barycenter is kept
alone without a constraint and merged with one; and that sibling clusters met in order get a constraint while a cluster
seen again does not.
