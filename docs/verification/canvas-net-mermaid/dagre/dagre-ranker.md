### DagreRanker Unit Verification Design

This document describes the unit-level verification strategy for the `DagreRanker` unit in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace: the internal static partial `DagreRanker` class (`Rank`,
`LongestPath`, `Slack`, `FeasibleTree`, `NetworkSimplex`, `InitLowLimValues`, `CalcCutValue`, `InitCutValues`,
`LeaveEdge`, `EnterEdge`, and `ExchangeEdges`), the port of dagre's `rank/` folder.

#### Verification Approach

The unit is verified with cases ported from dagre-d3-es's `rank/*.test.js` and with the example graph of Gansner et
al.'s paper. The rankings of small graphs are compared with the optimal ranks normalized to start at 0 (as dagre's
tests do), the tree numbering and cut values with values worked out by hand (the paper's tree edge g-h has the cut
value -1, the edges a-b to d-h 3, and e-g and f-g 0), and the entering edge is checked with the root on either side of
the cut. No mocking or stubbing is used; the internal type is reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `DagreRankerTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Dagre`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph and meter; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any rank, postorder number, cut value, or chosen edge that differs from the expected value constitutes a
failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Dagre-DagreRanker-NetworkSimplex: Network Simplex Gives The Optimal Ranks

**Tests**: `DagreRankerTests.DagreRanker_NetworkSimplex_SmallGraphs_OptimalRanks`,
`DagreRankerTests.DagreRanker_NetworkSimplex_MinLen_Honored`,
`DagreRankerTests.DagreRanker_NetworkSimplex_MultiEdges_GreatestMinLen`

Asserts the ranks of a single node, a chain, a diamond, and the Gansner graph (a=0, b=1, c=2, d=3, h=4, e=1, f=1,
g=2); that a minimum length of 2 is honored (ranks 0, 2, 1, 3); and that parallel edges take the greatest minimum
length.

##### CanvasNetMermaid-Dagre-DagreRanker-Pivots: Pivots Follow dagre

**Tests**: `DagreRankerTests.DagreRanker_InitLowLimValues_Tree_NumbersInPostorder`,
`DagreRankerTests.DagreRanker_CalcCutValue_GansnerTree_MatchesPaper`,
`DagreRankerTests.DagreRanker_LeaveEdge_NegativeCutValue_Found`, `DagreRankerTests.DagreRanker_EnterEdge_HeadToTail_Found`

Asserts the postorder low, lim, and parent of every node of a five-node tree; the cut values of the paper's tree; that
no edge leaves when every cut value is non-negative and that the first negative one leaves; and that the entering edge
crosses from the head to the tail component whether the root is on one side or the other.

##### CanvasNetMermaid-Dagre-DagreRanker-FeasibleTree: The Feasible Tree Is Tight And Spanning

**Tests**: `DagreRankerTests.DagreRanker_FeasibleTree_ShortensLooseEdges_SpansGraph`

Asserts that the tree of a four-node graph with a loose edge has four nodes and three edges and that every tree edge has
a slack of 0 after the ranks are shifted.

##### CanvasNetMermaid-Dagre-DagreRanker-RankerChoice: The Graph Picks The Ranker

**Tests**: `DagreRankerTests.DagreRanker_Rank_RankerAttribute_PicksRanker`,
`DagreRankerTests.DagreRanker_LongestPath_Chain_RanksBySinkDistance`

Asserts that `network-simplex`, `tight-tree`, and no ranker pull a node next to its successor while `longest-path`
leaves it as low as its successors allow; and that the longest-path ranks of a chain with minimum lengths are -3, -1,
and 0 with the slacks 1 and 0.

##### CanvasNetMermaid-Dagre-DagreRanker-UnlabeledNode: An Unlabeled Node Fails The Ranking

**Tests**: `DagreRankerTests.DagreRanker_LongestPath_UnlabeledNode_Throws`

Asserts that the longest-path ranking of a graph whose edge ends at a node created without a label (as dagre's flat
copy leaves the end of an edge at a subgraph) throws `InvalidOperationException` naming that node.
