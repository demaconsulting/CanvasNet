### DagreOrdering

![Dagre Structure](MermaidDagreView.svg)

The `DagreOrdering` unit is the sixth software unit in the `Dagre` subsystem, in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace. It is the internal static partial class `DagreOrdering` in four
source files: `DagreOrdering.cs` (the sweeps, the initial order, the layer graphs, and their label types
`LayerGraphLabel`, `LayerNode`, and `LayerEdge`), `DagreOrdering.Sort.cs` (the sort of one layer, with the entry types
`BarycenterEntry`, `ConflictEntry`, and `SortResult`), `DagreOrdering.CrossCount.cs` (the crossing count), and
`DagreOrdering.Constraints.cs` (the cluster constraints). It ports dagre's `order/index.js`, `init-order.js`,
`build-layer-graph.js`, `sort-subgraph.js`, `barycenter.js`, `resolve-conflicts.js`, `sort.js`, `cross-count.js`, and
`add-subgraph-constraints.js`.

#### Purpose

`DagreOrdering` decides the order of the nodes within each rank (the left-to-right position in a top-to-bottom
layout) so that edges cross as little as possible while the members of every cluster stay together. Since many orders
have the same number of crossings, the port follows dagre's sweep schedule, tie rules, and sorts exactly, so that it
keeps the order dagre keeps.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreOrdering-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-ordering.yaml`.

#### Data Model

The result is `LayoutNode.Order` of every ranked node. A **layer graph** holds the nodes of one rank under their
clusters (top-level nodes under a dummy root named so that the layout graph does not use the name), each cluster with
its left and right border nodes at that rank, and an edge from each node of the fixed adjacent rank with the summed
weight; a `LayerNode` shares the `LayoutNode` of a real node, so that the order written while sorting is the node's
order. A **constraint graph** records, for each sweep, that a cluster comes after the sibling cluster met before it.
A `BarycenterEntry` is a node or cluster with its barycenter and weight (both absent without in-edges); a
`ConflictEntry` is a group of nodes that stay together, kept in an `ObjectKeyMap` so that its iteration follows
JavaScript's key order; a `SortResult` is the sorted nodes with the barycenter and weight of the whole.

#### Key Methods

- `Order(LayoutGraph g, LayoutWorkMeter meter)` - builds the layer graphs of the down sweeps (ranks 1 to the greatest,
  by in-edges) and of the up sweeps (the greatest minus 1 down to 0, by out-edges), sets the initial order, then sweeps
  alternately up and down with ties biased right on two sweeps of every four, counts the crossings after each sweep,
  keeps the best order, and stops after four sweeps in a row without improvement.
- `InitOrder` - a depth-first search over successors from the nodes in rank order (`Lodash.SortBy`), appending each
  node to its rank when it is reached.
- `BuildLayerGraph` - the layer graph of one rank.
- `SortSubgraph` - sorts the members of a cluster (or of the layer root) by barycenter, sorting each sub-cluster first
  and keeping it contiguous, with the cluster's border nodes at the ends.
- `Barycenters` - the weighted average order of the neighbors in the fixed rank.
- `ResolveConflicts` - Forster's "A Fast and Simple Heuristic for Constrained Two-Level Crossing Reduction": merges
  entries whose barycenters contradict a constraint.
- `Sort` - sorts the entries with a barycenter by barycenter (ties by index, biased left or right) with `Lodash.Sort`,
  and puts the entries without one back at their original index.
- `AddSubgraphConstraints` - records the order of sibling clusters just found.
- `CrossCount` - the weighted crossings of a layering with the accumulator tree of Barth, Juenger, and Mutzel's
  "Simple and Efficient Bilayer Cross Counting".

**Preconditions and postconditions**: `Order` is called on the normalized layout graph with its border segments;
afterwards every rank's orders are 0 to its size minus 1, and the members of each cluster are contiguous. The faithful
quirk of `Order` is kept: when no node is ranked (`maxRank` is `undefined`), lodash's `_.range(1, NaN)` and
`_.range(NaN, -1, -1)` each give one rank, so one layer graph is built each way; and when no crossing count is a
number, the initial order is kept. The root of an empty layer graph has no child list, as in graphlib, and sorts to
nothing.

#### Error Handling

The unit throws no exception of its own. The recursive sort and the depth-first search call
`RuntimeHelpers.EnsureSufficientExecutionStack`. Every step charges the meter: building a layer graph the nodes of the
layout graph, the initial order its nodes and edges, every sorted layer graph its nodes and edges, and every two-rank
crossing count its two ranks and entries.

#### Dependencies

- **Graphlib subsystem** - `Graph`, `GraphEdge`, and `ObjectKeyMap`
- **Dagre subsystem** - `DagreLayout` (`BuildLayerMatrix`, `MaxRank`, `Same`, `Truthy`) and `Lodash` (`Sort`,
  `SortBy`, `Max`, `Num`), and the `LayoutModel.cs` labels; `LayoutWorkMeter`
- **.NET base class library** - `System.Runtime.CompilerServices.RuntimeHelpers`, `System.Linq`

#### Callers

`DagreLayout.RunLayout` calls `Order` after the border segments are added. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreOrderingTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

The sort of a cluster with 64 or more entries whose barycenters include NaN may differ from V8's, because `Lodash.Sort`
merges long runs without galloping (see _DagreLayout Unit Design_). Shorter lists, and any list whose barycenters are
all numbers, are sorted exactly as V8 sorts them.
