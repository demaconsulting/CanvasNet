### Graph Unit Verification Design

This document describes the unit-level verification strategy for the `Graph` unit in the
`DemaConsulting.CanvasNet.Mermaid.Graphlib` namespace: the internal `Graph<TGraph, TNode, TEdge>` class, the
`GraphEdge` record, and the `ObjectKeyMap<TValue>` map that keeps the key order of a JavaScript object.

#### Verification Approach

The unit is verified by building small graphs and maps and comparing every answer with the answer graphlib gives in
JavaScript, worked out by hand from graphlib's source and from the ECMAScript own-key order. The key order is checked
on names chosen to separate the rules: array indexes (`"0"`, `"2"`,
`"10"`, `"4294967294"`) against names that only look numeric (`"07"`, `"-1"`, `"1.5"`, `"4294967295"`, `"1e3"`,
`"+1"`, an eleven-digit number). The unit depends only on the .NET base class library, so no mocking or stubbing is
required; the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GraphTests.cs` and `ObjectKeyMapTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests`
project, in the `Graphlib` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph or map; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any list in another order than graphlib's, any label other than the expected one, or a refused operation
that does not throw constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Graphlib-Graph-Kinds: A New Graph Is Directed, Simple, And Flat

**Tests**: `GraphTests.Graph_Constructor_Defaults_DirectedSimpleFlat`

Asserts that a new graph is directed, not a multigraph, and not compound, has no nodes, no edges, and no graph label,
and keeps the graph label set through `SetGraph`.

##### CanvasNetMermaid-Graphlib-Graph-Nodes: Node Labels Are Kept, Replaced, Or Defaulted

**Tests**: `GraphTests.Graph_SetNode_ExistingNode_KeepsOrReplacesLabel`,
`GraphTests.Graph_SetPath_Nodes_AddsConsecutiveEdges`

Asserts that a node added again without a label keeps its first label, that a node added without a label gets the
default node label of its name, that a node added again with a label gets the new one, that an absent node has no
label, and that `SetNodes` adds a node.

##### CanvasNetMermaid-Graphlib-Graph-Edges: Edges Are Added, Labeled, Named, And Found

**Tests**: `GraphTests.Graph_SetEdge_Multigraph_KeepsParallelEdgesApart`,
`GraphTests.Graph_SetEdge_NamedEdgeInSimpleGraph_Throws`, `GraphTests.Graph_SetEdge_Undirected_FindsEdgeEitherWay`,
`GraphTests.Graph_SetPath_Nodes_AddsConsecutiveEdges`

Asserts that an unnamed and two named edges between the same nodes of a multigraph are three edges in insertion order
with the default label of the tail, head, and name or the given label, that adding the unnamed edge again keeps its
label, that a simple graph refuses a named edge with `InvalidOperationException`, that an undirected edge added as
`b`, `a` is stored as `a`, `b` and found from either end, and that a path adds an edge between each pair of
consecutive nodes with the path's label or the default label.

##### CanvasNetMermaid-Graphlib-Graph-Adjacency: Neighborhood Queries Match Graphlib

**Tests**: `GraphTests.Graph_NodeEdges_FilteredByOtherEnd_ListsMatchingEdges`,
`GraphTests.Graph_Successors_NumericNames_ComeFirst`, `GraphTests.Graph_SetEdge_Undirected_FindsEdgeEitherWay`,
`GraphTests.Graph_SetPath_Nodes_AddsConsecutiveEdges`

Asserts that the edges of a node are its in-edges followed by its out-edges, optionally only those to or from another
node, that in-edges can be filtered by their tail, that the predecessors, successors, and neighbors (predecessors then
the other successors) follow the key order, that the sources and sinks are found, that a node with successors or
neighbors is not a leaf and the end of a path is, and that the lists of an absent node are null.

##### CanvasNetMermaid-Graphlib-Graph-Removal: Removing A Node Removes Its Edges

**Tests**: `GraphTests.Graph_RemoveNode_WithEdges_RemovesEdgesAndReordersOnReAdd`,
`GraphTests.Graph_SetEdge_Multigraph_KeepsParallelEdgesApart`

Asserts that removing a node with an in-edge, an out-edge, and a named self loop leaves no edges and no stale
neighbors, that removing an absent node does nothing, that the node added again is listed last, and that removing one
named edge of a multigraph leaves the parallel edges.

##### CanvasNetMermaid-Graphlib-Graph-Compound: Parents And Children Are Tracked

**Tests**: `GraphTests.Graph_SetParent_Compound_TracksChildren`, `GraphTests.Graph_SetParent_CycleOrFlatGraph_Throws`

Asserts the parents and children of nested nodes of a compound graph (with nodes added by `SetParent`), the top-level
nodes, null for an absent node, that removing a parent moves its children to the top level, that a parent cycle and a
parent in a flat graph throw `InvalidOperationException`, and that in a flat graph every node is at the top level and
has no children.

##### CanvasNetMermaid-Graphlib-Graph-Filter: A Filtered Copy Keeps The Structure

**Tests**: `GraphTests.Graph_FilterNodes_Compound_ReparentsToNearestKeptAncestor`

Asserts that filtering out the middle node of a three-level compound multigraph keeps the graph label and kind, the
other nodes in key order, the named edge whose ends are kept, and places the leaf under the top node.

##### CanvasNetMermaid-Graphlib-Graph-KeyOrder: Collections Follow JavaScript's Own-Key Order

**Tests**: `GraphTests.Graph_Nodes_NumericNames_ComeFirstInNumericOrder`,
`GraphTests.Graph_Successors_NumericNames_ComeFirst`,
`ObjectKeyMapTests.ObjectKeyMap_Keys_MixedKeys_IndexesFirstThenInsertionOrder`,
`ObjectKeyMapTests.ObjectKeyMap_Remove_KeyAddedAgain_MovesToEnd`,
`ObjectKeyMapTests.ObjectKeyMap_IsArrayIndex_Key_MatchesJavaScript`

Asserts that nodes, successors, and predecessors list numeric names first in numeric order and the other names in
insertion order; that a map of mixed keys lists `0`, `2`, `10`, `4294967294` before the other keys in insertion order
with the values in the same order; that a removed key added again moves to the end while a key set again keeps its
place; and the array-index rule on its edge cases (`0`, `42`, and `4294967294` are indexes; `4294967295`, `00`, `01`,
the empty string, `1e3`, `+1`, and an eleven-digit number are not).
