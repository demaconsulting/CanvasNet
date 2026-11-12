## Dagre

![Dagre Structure](MermaidDagreView.svg)

### Overview

The `Dagre` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Dagre`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Dagre/`) is the layered graph layout that Mermaid draws its flowcharts with: a
port of the dagre layout of dagre-d3-es, the package that the pinned Mermaid release bundles. Given a directed graph
whose nodes and edge labels have sizes, it ranks the nodes, orders each rank to reduce crossings, assigns coordinates,
and routes the edges, with the same results as dagre-d3-es. It is **internal**: its types are visible only to the
other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains nine units:

- **DagreLayout** - the internal static partial `DagreLayout` (`DagreLayout.cs`, `DagreLayout.Util.cs`), the entry
  point `Run` and the stages and helpers of dagre's `layout.js` and `util.js`, with the grouped helper types of the
  input graph (`DagreGraph`, `DagreNode`, `DagreEdge`, `DagrePoint` in `DagreGraph.cs`), of the internal layout graph
  (`LayoutGraphLabel`, `LayoutNode`, `LayoutEdge`, `SelfEdge` in `LayoutModel.cs`), and the JavaScript and lodash
  collection semantics (`Lodash` in `Lodash.cs`); see _DagreLayout Unit Design_ (`dagre/dagre-layout.md`)
- **DagreAcyclic** - the internal static `DagreAcyclic`: the cycle breaking of `acyclic.js`, `greedy-fas.js`, and
  `data/list.js`; see _DagreAcyclic Unit Design_ (`dagre/dagre-acyclic.md`)
- **DagreRanker** - the internal static partial `DagreRanker` (`DagreRanker.cs`, `DagreRanker.FeasibleTree.cs`,
  `DagreRanker.NetworkSimplex.cs`): the ranking of `rank/index.js`, `rank/util.js`, `rank/feasible-tree.js`, and
  `rank/network-simplex.js`; see _DagreRanker Unit Design_ (`dagre/dagre-ranker.md`)
- **DagreNesting** - the internal static `DagreNesting`: the cluster handling of `nesting-graph.js`,
  `parent-dummy-chains.js`, and `add-border-segments.js`; see _DagreNesting Unit Design_ (`dagre/dagre-nesting.md`)
- **DagreNormalizer** - the internal static `DagreNormalizer`: the long-edge chains of `normalize.js`; see
  _DagreNormalizer Unit Design_ (`dagre/dagre-normalizer.md`)
- **DagreOrdering** - the internal static partial `DagreOrdering` (`DagreOrdering.cs`, `DagreOrdering.Sort.cs`,
  `DagreOrdering.CrossCount.cs`, `DagreOrdering.Constraints.cs`): the crossing reduction of the `order/` folder; see
  _DagreOrdering Unit Design_ (`dagre/dagre-ordering.md`)
- **DagrePositioning** - the internal static partial `DagrePositioning` (`DagrePositioning.cs`,
  `DagrePositioning.BrandesKoepf.cs`): the coordinate assignment of `position/index.js` and `position/bk.js`; see
  _DagrePositioning Unit Design_ (`dagre/dagre-positioning.md`)
- **DagreCoordinateSystem** - the internal static `DagreCoordinateSystem`: the rank-direction changes of
  `coordinate-system.js`; see _DagreCoordinateSystem Unit Design_ (`dagre/dagre-coordinate-system.md`)
- **LayoutWorkMeter** - the internal sealed `LayoutWorkMeter`: the work budget and cancellation check of the layout of
  one diagram, its two failures, and the counter of lodash's `_.uniqueId`; see _LayoutWorkMeter Unit Design_
  (`dagre/layout-work-meter.md`)

The subsystem's namespace documentation is in `Dagre/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Dagre-*` requirements in `docs/reqstream/canvas-net-mermaid/dagre.yaml`. The SysML2 model names the
subsystem `MermaidDagre` (see _CanvasNetMermaid System Design_).

**Attribution.** Every unit except `LayoutWorkMeter` is a port of dagre-d3-es (MIT; derived from dagre and graphlib by
Chris Pettitt), read from the package that the repository's lock file pins for the pinned Mermaid release; `Lodash`
and the counter of `LayoutWorkMeter` port the semantics of lodash (MIT) that dagre's results depend on, and the steps
of V8's array sort are reimplemented from the published algorithm. The work budget is original. Each source file
starts with a "Portions derived from" line, and the third-party notices of the package
(`src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md`, `LICENSE-dagre.txt`, `LICENSE-lodash.txt`) record which
unit ports which upstream file.

### Interfaces

**Exposed** (internal only):

- `DagreLayout.Run<TNode, TEdge>(Graph<DagreGraph, TNode, TEdge> g, LayoutWorkMeter meter)` - lays out the graph and
  writes the results into its labels (node centers `X` and `Y`, cluster `Width` and `Height`, edge `Points` and label
  `X` and `Y`, and the graph label's `Width` and `Height`); returns the internal layout graph with the ranks and orders,
  for checks. `TNode` and `TEdge` derive from the non-sealed `DagreNode` and `DagreEdge`, so a caller carries its own
  data on them.
- `DagreLayout.Same(double, double)` - JavaScript's `===` on numbers, also used by the edge builder.
- `LayoutWorkMeter(long maxWork, MermaidLocation location, CancellationToken cancellationToken)` with `Charge`,
  `UniqueId`, `Failure`, `LimitExceeded`, `MaxWork`, and `Used` - one meter per diagram, shared by every layout of the
  diagram and by the cluster code of the `Rendering` subsystem, which charges its own work to it.
- The stage entry points of the other units (`DagreAcyclic.Run`, `DagreRanker.Rank`, `DagreOrdering.Order`, and so
  on) are internal to the subsystem in purpose; they are reached by `DagreLayout` and by the unit tests.

**Consumed**: `Graph`, `GraphEdge`, `ObjectKeyMap`, and `GraphAlgorithms` (`Preorder`, `Postorder`) from the
`Graphlib` subsystem; the public `MermaidLocation`, `MermaidRenderException`, `MermaidDiagnosticCodes`, and
`MermaidLimits` of the package root; and the .NET base class library.

**Consumers**: `ClusterLayout` of the `Rendering` subsystem builds the input graphs (one per extracted cluster, in the
way Mermaid's dagre wrapper does) and calls `DagreLayout.Run`; the flowchart renderer of the `Flowchart` subsystem
creates the `LayoutWorkMeter` of a diagram from `MermaidLimits.MaxLayoutWork`, the location of the diagram keyword,
and the cancellation token of the call.

### Design

**A stage-by-stage port.** `DagreLayout.Run` copies the input graph into an internal compound multigraph with dagre's
defaults and runs dagre's `runLayout` in dagre's order: make room for edge labels, remove self edges, break cycles
(`DagreAcyclic`), add the nesting graph (`DagreNesting`), rank (`DagreRanker`), add edge-label proxies, remove empty
ranks, clean up the nesting graph, normalize the ranks, record the cluster rank ranges, remove the proxies, split long
edges (`DagreNormalizer`), move dummy chains into clusters and add cluster borders (`DagreNesting`), order
(`DagreOrdering`), insert self edges, turn the graph on its side (`DagreCoordinateSystem`), position
(`DagrePositioning`), route self edges, remove border nodes, restore the long edges (`DagreNormalizer`), fix side
labels, turn the graph back, translate it to the margins, cut the routes at the node borders, reverse the routes of
reversed edges, and restore them (`DagreAcyclic`). It then writes the results back to the caller's labels. Each unit
keeps the function names of its upstream file in its documentation comments, so that a reviewer can read the two side
by side.

**Exact, including ties.** A layered layout has many ties (equal barycenters, equal slacks, equal widths), and dagre
settles them by the order in which it iterates JavaScript objects and by the sorts of V8 and lodash. The port keeps
both: the `Graphlib` graph keeps its nodes and edges in JavaScript's key order (array-index names first, ascending,
then the other names in insertion order), the `Lodash` helpers reproduce `_.sortBy`, `_.min`, `_.max`, `_.minBy`, and
V8's TimSort steps, and values that JavaScript leaves `undefined` are nullable or NaN where only arithmetic sees them.
The result is checked against 224 layouts recorded from dagre-d3-es by
`tools/visual-diff/mermaid/gen-dagre-goldens.mjs` (`Fixtures/upstream-vectors/dagre-layouts.json`): ranks and
orders equal, coordinates, sizes, routes, label centers, and the drawing size within 1e-9 relative (in practice
bit-equal). The graphs come from a seeded generator and cover acyclic, cyclic (with both cycle breakers), compound,
deeply nested, large compound, self-loop, simple, tiny, and empty graphs, with numeric and Mermaid-style names,
parallel edges, every label position, label offsets, minimum lengths 1 to 3, weights 1 to 3, random separations and
margins, all three rankers, and all four alignments and none.

**Faithful quirks.** Where dagre's code does something surprising that changes its output, the port does the same:
the graph's `maxRank` is set with `_.max(a, b)` (which gives `undefined`, so the value is undefined as soon as a
cluster exists); edge-label space is made with an upper-case `TB`/`BT` test while every other direction test is lower
case; side labels are moved only for the lower-case positions `l` and `r`; an edge-label proxy at a half rank is never
shifted by the removal of empty ranks and then never gets a label node (dagre's half-rank label quirk, hit by nine of
the goldens); `translateGraph` starts its maximum x and y at 0; the network simplex updates the ranks from the first
tree node; the type-2 conflict scan runs inside its loop; `_.range(1, NaN)` gives one layer graph each way when no
node is ranked; and for an empty graph the feasible tree starts from a node named `undefined` (JavaScript's
`setNode(undefined)`) and the root of a layer graph has no child list to sort. Each quirk is commented where it is
kept.

**Bounded and cancelable.** Every iterative stage charges the `LayoutWorkMeter` in proportion to its work: a network
simplex pivot the tree and graph edges it scans and the nodes it renumbers, a feasible-tree iteration the nodes and
edges, an ordering sweep the size of its layer graphs, a crossing count its entries, a Brandes-Koepf pass twice the
nodes and edges plus its conflict scans, and the normalization, border, and dummy-chain stages their nodes. The
charges are deterministic: the complete acyclic graph on 30 nodes (435 edges) takes 2,955,593 steps, well within the
default `MaxLayoutWork` of 50,000,000, and a four-node cyclic graph with a cluster and a label takes 1,369; both are
pinned by tests. A charge that would pass the limit throws `mermaid-limit-exceeded` at the meter's location, and every
charge first checks the cancellation token. The recursive stages call `EnsureSufficientExecutionStack`, and
`DagreLayout.Run` turns an `InsufficientExecutionStackException` into `mermaid-limit-exceeded` and the
`InvalidOperationException` of a layout dagre itself would fail (a route that ends at its node's center, a graph to
rank that is not connected, a broken cluster path) into `mermaid-render-failed`, both located; an
`OperationCanceledException` passes through.

**Deterministic.** The units hold no static state. lodash's `_.uniqueId`, which names dagre's dummy nodes, has one
counter for a whole page in JavaScript; here the counter lives in the meter, so every diagram gets the same names
whatever was drawn before, and the names never change the geometry. One meter and one graph serve one call on one
thread, so concurrent renders do not interact.

### Known Differences From Mermaid

The layout matches dagre-d3-es on every golden. The port differs from it only where JavaScript would misbehave or
where Mermaid cannot reach:

- V8 merges long sorted runs with galloping; the port merges them plainly and stably, which differs only for an
  inconsistent comparator (a NaN barycenter) on 64 or more entries of one cluster (see _DagreLayout Unit Design_).
- Node names that are properties of JavaScript's object prototype (such as `constructor` or `__proto__`) are ordinary
  names here (see the `Graphlib` subsystem).
- A broken cluster path, on which dagre would loop forever, fails with `mermaid-render-failed` (see _DagreNesting Unit
  Design_).
- Negative and fractional edge weights, for which dagre's greedy cycle breaker indexes its bucket array with a
  non-integer, are not supported; Mermaid always uses whole positive weights (see _DagreAcyclic Unit Design_).
- graphlib's `json` serialization and the graph algorithms that dagre and Mermaid's wrapper do not use are not ported
  (see the `Graphlib` subsystem).
- The counter of `_.uniqueId` is per diagram rather than per page, and the work budget, its failures, and the mapping
  of a short stack are additions (see _LayoutWorkMeter Unit Design_).
