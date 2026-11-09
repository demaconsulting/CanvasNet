### DagreNesting

![Dagre Structure](MermaidDagreView.svg)

The `DagreNesting` unit is the fourth software unit in the `Dagre` subsystem. It is one source file,
`DagreNesting.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Dagre` namespace, and defines the internal static class
`DagreNesting`. It ports dagre's `nesting-graph.js`, `parent-dummy-chains.js`, and `add-border-segments.js`.

#### Purpose

`DagreNesting` makes clusters (Mermaid's subgraphs) come out as contiguous boxes. Before ranking it adds the nesting
graph of Sander's "Layout of Compound Directed Graphs", which ranks the members of each cluster between a top and a
bottom border; after ranking it removes that graph again, moves the dummy nodes of long edges into the clusters they
cross, and adds the left and right border nodes that keep each cluster's members between its sides during ordering and
positioning.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreNesting-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-nesting.yaml`.

#### Data Model

The unit writes the nesting state to the layout labels: `LayoutGraphLabel.NestingRoot` (the dummy root while it
exists) and `NodeRankFactor` (the rank multiplier), `LayoutEdge.NestingEdge` on the edges it adds, `LayoutNode.BorderTop`
and `BorderBottom` on every cluster, and `BorderLeft` and `BorderRight` (lists indexed by rank, as the JavaScript arrays
are) with the `BorderType` of each side node. The cluster tree is numbered in postorder (`Low`, `Lim`) to find the
lowest common ancestor of the two ends of an edge. The comparisons on ranks that may be `undefined` (a cluster has no
rank) follow JavaScript: they are false when either side is `undefined` or NaN.

#### Key Methods

- `Run(LayoutGraph g, LayoutWorkMeter meter)` - dagre's `nestingGraph.run`: computes the depth of every node in the
  cluster tree, multiplies every edge's minimum length by the rank factor `2 * height + 1`, adds the root joined to
  every top-level node, and for every cluster adds a top and a bottom border node joined to its members (a member
  cluster through its own borders) with weights that pull the borders to the members; the graph label records the
  root and the rank factor.
- `Cleanup(LayoutGraph g)` - removes the root and every nesting edge.
- `ParentDummyChains(LayoutGraph g, LayoutWorkMeter meter)` - for every dummy chain, finds the cluster path of its
  edge (from the tail up to the lowest common ancestor, then down to the head) and parents each dummy in the cluster of
  the path that spans its rank.
- `AddBorderSegments(LayoutGraph g, LayoutWorkMeter meter)` - for every cluster with a rank range, adds a left and a
  right border node of zero size at each rank, each joined to the one of the rank above, with the cluster as parent.

**Preconditions and postconditions**: `Run` is called on the acyclic layout graph before ranking; `Cleanup` after the
ranks are known; `ParentDummyChains` and `AddBorderSegments` after normalization. After `AddBorderSegments` every
cluster has a border node on each side of each of its ranks, which `DagreLayout.RemoveBorderNodes` later turns into the
cluster's size.

#### Error Handling

The recursions (`Visit`, `TreeDepths`, `Postorder`, the border walk) call `RuntimeHelpers.EnsureSufficientExecutionStack`,
so a deeply nested graph fails with `mermaid-limit-exceeded` through `DagreLayout.Run`. A cluster path that reaches the
top level without meeting the lowest common ancestor throws `InvalidOperationException`, reported as
`mermaid-render-failed`. The stages charge the meter: the nesting graph its nodes and edges, the dummy chains their
nodes and each path's length, and the border segments the nodes and two steps per border pair.

#### Dependencies

- **Graphlib subsystem** - `Graph` and `GraphEdge`
- **Dagre subsystem** - `DagreLayout` (`AddDummyNode`, `AddBorderNode`) and `Lodash` (`Max`, `Num`), and the
  `LayoutModel.cs` labels; `LayoutWorkMeter`
- **.NET base class library** - `System.Runtime.CompilerServices.RuntimeHelpers`, `System.Linq`

#### Callers

`DagreLayout.RunLayout` calls `Run` before ranking, `Cleanup` after the empty ranks are removed, and
`ParentDummyChains` and `AddBorderSegments` after normalization. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreNestingTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

dagre walks from the head of an edge up toward the lowest common ancestor with `while (parent !== lca)`; on a broken
cluster path (one that reaches the top level first) JavaScript would loop forever. The port stops at the top level and
throws, so such a graph fails with `mermaid-render-failed` instead of hanging.
