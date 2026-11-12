### DagreRanker

![Dagre Structure](MermaidDagreView.svg)

The `DagreRanker` unit is the third software unit in the `Dagre` subsystem, in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace. It is the internal static partial class `DagreRanker` in three
source files: `DagreRanker.cs` (the ranker choice, the longest-path ranking, the slack, and the nested tree label types
`TreeNode` and `TreeEdge`), `DagreRanker.FeasibleTree.cs` (the feasible tight tree), and
`DagreRanker.NetworkSimplex.cs` (the network simplex). It ports dagre's `rank/index.js`, `rank/util.js`,
`rank/feasible-tree.js`, and `rank/network-simplex.js`.

#### Purpose

`DagreRanker` assigns every node a rank (its row in a top-to-bottom layout) so that every edge points to a greater rank
by at least its minimum length. With the default network simplex ranker of Gansner et al., the ranks also minimize the
total weighted edge length, and the port takes the same pivots in the same order as dagre, so that among several
optimal rankings it reaches the one dagre reaches.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreRanker-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-ranker.yaml`.

#### Data Model

Ranks are written to `LayoutNode.Rank` and may be negative; `DagreLayout` normalizes them later. The spanning tree is
an undirected `Graphlib` graph whose node labels are `TreeNode` (the postorder `Low` and `Lim` of the subtree and the
`Parent` in the tree) and whose edge labels are `TreeEdge` (the `CutValue`). The network simplex works on the
simplified graph (`DagreLayout.Simplify`: parallel edges collapsed into one with the summed weight and the greatest
minimum length), which shares the node labels with the layout graph.

#### Key Methods

- `Rank(LayoutGraph g, LayoutWorkMeter meter)` - dagre's `rank`: `tight-tree` runs `LongestPath` and `FeasibleTree`,
  `longest-path` runs `LongestPath`, and any other value (including none) runs `NetworkSimplex`.
- `LongestPath` - from every source, each node's rank is the least of its successors' ranks minus the edge minimum
  lengths, or 0 for a sink.
- `Slack` - the rank difference of an edge minus its minimum length.
- `FeasibleTree` - starts from the first node, grows the tree along tight edges (`TightTree`), and while it does not
  span the graph shifts the ranks of the tree by the slack of the edge of least slack with one end in it
  (`FindMinSlackEdge`, `ShiftRanks`).
- `NetworkSimplex` - simplify, `LongestPath`, `FeasibleTree`, `InitLowLimValues`, `InitCutValues`, then while
  `LeaveEdge` finds a tree edge with a negative cut value, `EnterEdge` picks the graph edge of least slack that
  crosses its cut from the head to the tail component and `ExchangeEdges` swaps them and recomputes the numbering, the
  cut values, and the ranks (`UpdateRanks`, from the first tree node down).
- `InitLowLimValues`, `CalcCutValue`, `InitCutValues` - the postorder numbering of the tree and the cut value of each
  tree edge from those of its child edges (children before parents, by the `Graphlib` postorder).

**Preconditions and postconditions**: the graph is the flat, acyclic, connected copy of the layout graph that the
nesting root makes connected; afterwards every node has a rank and every edge has a non-negative slack. The faithful
quirk of `UpdateRanks` is kept: dagre picks as root the first tree node whose graph label has no `parent` property,
which for a layout label is always the first tree node.

#### Error Handling

`FeasibleTree` throws `InvalidOperationException` when no edge leaves a tree that does not span the graph (a graph that
is not connected), and `EnterEdge` when no edge crosses the cut; dagre fails in both cases too, and `DagreLayout.Run`
reports the exception as `mermaid-render-failed`. `LongestPath` throws `InvalidOperationException` naming the node
when a node it reaches has no label (the end of an edge at a compound node, which the flat graph leaves out), where
dagre's `label.rank = rank` fails on undefined. The recursions (`LongestPath`, `TightTree`, `DfsAssignLowLim`) call
`RuntimeHelpers.EnsureSufficientExecutionStack`. Every iteration charges the meter: a longest-path ranking and each
feasible-tree iteration the nodes and edges, each cut-value computation the tree nodes and graph edges, each leave
step the tree edges, each enter step the graph edges, and each exchange twice the tree nodes.

#### Dependencies

- **Graphlib subsystem** - `Graph`, `GraphEdge`, and `GraphAlgorithms` (`Postorder`, `Preorder`)
- **Dagre subsystem** - `DagreLayout` (`Simplify`, `Truthy`) and `Lodash` (`Min`, `MinBy`, `Num`), and the
  `LayoutModel.cs` labels; `LayoutWorkMeter`
- **.NET base class library** - `System.Runtime.CompilerServices.RuntimeHelpers`, `System.Linq`

#### Callers

`DagreLayout.RunLayout` calls `Rank` on the flat copy of the layout graph after the nesting graph is added. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreRankerTests.cs` call the unit directly through
`InternalsVisibleTo`, including the cut values of the example in Gansner's paper.
