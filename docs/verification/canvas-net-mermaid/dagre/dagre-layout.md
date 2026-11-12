### DagreLayout Unit Verification Design

This document describes the unit-level verification strategy for the `DagreLayout` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static partial `DagreLayout` class (`Run` with the
stages of dagre's `layout.js`, and the helpers of `util.js`), its input labels (`DagreGraph`, `DagreNode`, `DagreEdge`,
`DagrePoint`), its layout labels (`LayoutGraphLabel`, `LayoutNode`, `LayoutEdge`, `SelfEdge`), and the `Lodash` class.

#### Verification Approach

The unit is verified through `Run` on small graphs whose layout can be worked out by hand from dagre's rules, and
through its helpers directly. The expectations are exact numbers: a single 50 x 100 node is centered at (25, 50) in a
drawing of 50 x 100; two unconnected nodes of widths 50 and 75 with a node separation of 200 are centered at x = 25
and x = 50 + 200 + 37.5; two connected nodes of height 100 and 200 with a rank separation of 300 are centered at
y = 50 and y = 100 + 300 + 100; and a 60 x 70 label between them (the rank separation halved to 150 for the label rank)
puts the label at y = 100 + 150 + 35. Several cases are ported from dagre-d3-es's `layout.test.js` and `util.test.js`.
The `Lodash` tests compare the V8-style sort with an ordinary stable sort for consistent comparators and with the
hand-traced steps of V8 for an inconsistent one, and compare the lodash helpers with lodash's documented results. No
mocking or stubbing is used; the `Graphlib` graph and a meter without a practical limit are real, and the internal
types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `DagreLayoutTests.cs` and `LodashTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests`
project, in the `Dagre` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any coordinate, size, rank, or order that differs from the hand-derived value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreLayout-Defaults: Unset Attributes Take dagre's Defaults

**Tests**: `DagreLayoutTests.DagreLayout_Run_SingleNode_PlacedAtHalfSize`,
`DagreLayoutTests.DagreLayout_Run_TwoNodesSameRank_SeparatedByNodeSep`,
`DagreLayoutTests.DagreLayout_Run_TwoConnectedNodes_StackedByRankSep`

Asserts that a single node with no margins has its corner at the origin and the drawing its size, and that a node
separation and a rank separation that are set are used as given between two nodes of one rank and of two ranks.

##### CanvasNetMermaid-Dagre-DagreLayout-Results: Results Are Written To The Caller's Labels

**Tests**: `DagreLayoutTests.DagreLayout_Run_TwoConnectedNodes_StackedByRankSep`,
`DagreLayoutTests.DagreLayout_Run_EdgeRoute_EndsOnNodeBorders`,
`DagreLayoutTests.DagreLayout_Run_Cluster_MinimizesHeightAndIsSized`,
`DagreLayoutTests.DagreLayout_Run_ClusterInEveryDirection_SizedAroundMember`,
`DagreLayoutTests.DagreLayout_IntersectRect_PointOutside_OnLineAndBorder`,
`DagreLayoutTests.DagreLayout_Run_Twice_SameResults`

Asserts that an edge without a label gets no label center; that a route of minimum length 1 and 2 starts and ends on
the node borders and passes the dummy positions at the expected y values; that a cluster gets a size around its
members in every rank direction; that the cut of a route at a rectangle lies on the line to its center and on its
border; and that a graph with numeric node names and named edges laid out twice gets the same results in its input
labels and keeps its edges.

##### CanvasNetMermaid-Dagre-DagreLayout-EdgeLabels: Edge Labels Get Their Own Rank And Offset

**Tests**: `DagreLayoutTests.DagreLayout_Run_LabeledEdge_LabelBetweenNodes`,
`DagreLayoutTests.DagreLayout_Run_LongLabel_SeparatesNodes`,
`DagreLayoutTests.DagreLayout_Run_LabelOffset_MovesSideLabels`,
`DagreLayoutTests.DagreLayout_Run_LeftLabel_StaysInBoundingBox`

Asserts that a labeled edge places its label halfway between the two node ranks, that a 2,000-wide label keeps the
nodes more than 1,000 apart in every rank direction, that a left and a right label 10 wide with an offset of 1,000 sit
1,005 left and right of the edge (or above and below for a horizontal direction), and that a left label stays inside
the drawing in every rank direction.

##### CanvasNetMermaid-Dagre-DagreLayout-SelfLoops: Self Loops Are Routed Beside The Node

**Tests**: `DagreLayoutTests.DagreLayout_Run_SelfLoop_RoutedBesideNode`

Asserts, in every rank direction, that a self loop has seven points (five loop points and the two ends) that all lie on
the far side of the node and within its extent across the rank.

##### CanvasNetMermaid-Dagre-DagreLayout-CyclesAndClusters: Cycles And Clusters Are Laid Out

**Tests**: `DagreLayoutTests.DagreLayout_Run_ShortCycle_OneEdgeUpOneDown`,
`DagreLayoutTests.DagreLayout_Run_Cluster_MinimizesHeightAndIsSized`,
`DagreLayoutTests.DagreLayout_Run_ImplicitParent_DoesNotFail`

Asserts that in a two-node cycle one route runs down and the other up, each from its own tail; that two members of a
cluster joined only through outside nodes share a rank; and that a member of a parent created only by naming it is
laid out at finite coordinates while the parent keeps its null label.

##### CanvasNetMermaid-Dagre-DagreLayout-RankUtilities: The Shared Utilities Behave As dagre's

**Tests**: `DagreLayoutTests.DagreLayout_Simplify_MultiEdges_Collapsed`,
`DagreLayoutTests.DagreLayout_AsNonCompoundGraph_Cluster_Dropped`,
`DagreLayoutTests.DagreLayout_BuildLayerMatrix_RanksAndOrders_Matrix`,
`DagreLayoutTests.DagreLayout_NormalizeRanks_NegativeAndCluster_ShiftedToZero`,
`DagreLayoutTests.DagreLayout_RemoveEmptyRanks_BorderRanks_Removed`

Asserts that two parallel edges collapse into one with weight 3 and minimum length 2 sharing the graph label; that the
flat copy keeps the leaves and the named edge but not the cluster; that the layer matrix lists the nodes by rank and
order; that a negative least rank is shifted to 0 while a cluster stays without a rank; and that an empty rank is
removed unless it is a multiple of the nesting rank factor.

##### CanvasNetMermaid-Dagre-DagreLayout-JavaScriptSemantics: Sorts And Extremes Follow V8 And lodash

**Tests**: `LodashTests.Lodash_Sort_ConsistentComparator_MatchesStableSort`,
`LodashTests.Lodash_Sort_NaNKeys_FollowsBinaryInsertion`, `LodashTests.Lodash_SortBy_MixedKeys_NumbersUndefinedNaN`,
`LodashTests.Lodash_MinMaxMinBy_SkipUndefinedAndNaN`, `LodashTests.Lodash_CompareAscending_Keys_MatchesLodash`

Asserts that the V8-style sort is stable and agrees with an ordinary stable sort for a consistent comparator on short
lists and on lists long enough to be sorted in several runs; that a short list with a NaN key follows V8's natural run
and binary insertion; that `SortBy` orders numbers, then `undefined`, then NaN, stably; that the extremum helpers skip
`undefined` and NaN, keep the first of equal values, and give `undefined` for an empty list; and that the key
comparison matches lodash, including the inconsistent NaN-to-NaN case.

##### CanvasNetMermaid-Dagre-DagreLayout-Failures: A Route At The Node Center Fails

**Tests**: `DagreLayoutTests.DagreLayout_IntersectRect_PointAtCenter_Throws`

Asserts that cutting a route that ends at its node's center throws `InvalidOperationException`, as dagre throws; `Run`
reports that exception as `mermaid-render-failed` through the meter, whose failure is verified in the
`LayoutWorkMeter` unit.
