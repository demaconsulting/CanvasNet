### DagreLayout

![Dagre Structure](MermaidDagreView.svg)

The `DagreLayout` unit is the first software unit in the `Dagre` subsystem, in the
`DemaConsulting.CanvasNet.Mermaid.Dagre` namespace. It is the internal static partial class `DagreLayout` in two
source files, `DagreLayout.cs` (the entry point and the stages of dagre's `layout.js`) and `DagreLayout.Util.cs` (the
helpers of dagre's `util.js`), with three files of grouped helper types: `DagreGraph.cs` (the input labels
`DagreGraph`, `DagreNode`, `DagreEdge`, and the mutable point `DagrePoint`), `LayoutModel.cs` (the internal labels
`LayoutGraphLabel`, `LayoutNode`, `LayoutEdge`, and the record `SelfEdge`), and `Lodash.cs` (the static class `Lodash`
with the JavaScript and lodash collection semantics that dagre's results depend on).

#### Purpose

`DagreLayout` is the entry point of the layout and the owner of everything that dagre's `layout.js` does itself:
copying the caller's graph with the defaults applied, running the stages of the other units in dagre's order, the
small stages between them (edge-label space, self edges, edge-label proxies, cluster rank ranges, border-node removal,
side-label fix-up, translation, and node-border intersections), and writing the results back. It also holds dagre's
shared helpers (`util.js`) and, in `Lodash`, the exact sort, comparison, and extremum semantics that make the port
settle ties as dagre does.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreLayout-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-layout.yaml`.

#### Data Model

- **Input labels** (`DagreGraph.cs`). `DagreGraph` has the graph attributes `NodeSep`, `EdgeSep`, `RankSep`,
  `MarginX`, `MarginY`, `Acyclicer`, `Ranker`, `RankDir`, and `Align` and receives the drawing `Width` and `Height`.
  `DagreNode` has a `Width` and `Height` and receives its center `X` and `Y` (and, for a cluster, its size).
  `DagreEdge` has `MinLen`, `Weight`, label `Width` and `Height`, `LabelOffset`, and `LabelPos` and receives its route
  `Points` (a list of `DagrePoint`) and its label center `X` and `Y`. A null attribute is one the caller did not set,
  so dagre's default applies; a caller that mirrors a key that JavaScript holds as `undefined` passes NaN, as dagre's
  `Number(undefined)` gives. `DagreNode` and `DagreEdge` are not sealed so that the `Rendering` subsystem can derive
  its own node and edge types from them, as Mermaid spreads its data into dagre's labels.
- **Layout labels** (`LayoutModel.cs`). The internal layout graph is a compound multigraph of the `Graphlib`
  subsystem with `LayoutGraphLabel` (the attributes with defaults, the nesting root and rank factor, the dummy chains,
  `MaxRank`, and the drawing size), `LayoutNode` (size, center, rank, order, the dummy kind - `edge`, `edge-label`,
  `edge-proxy`, `selfedge`, `border`, or `root` - the edge a dummy stands for, the border nodes of a cluster by rank,
  its rank range, and its removed self edges), and `LayoutEdge` (minimum length, weight, label size, offset and
  position, label rank, route, label center, and the reversal state). Every property a stage reads or writes is
  declared; a nullable member mirrors a property that dagre adds only to some objects, and node coordinates start as
  NaN, which behaves as `undefined` in arithmetic.
- **JavaScript values.** `undefined` is `null` on a nullable number and NaN where only arithmetic sees it;
  `Lodash.Num` converts the former to the latter, `Truthy` is JavaScript's truth value of a number, and `Same` is
  JavaScript's `===` (NaN never equal, both zeros equal).

#### Key Methods

- `Run<TNode, TEdge>(Graph<DagreGraph, TNode, TEdge> g, LayoutWorkMeter meter)` - charges the size of the input,
  builds the layout graph (`BuildLayoutGraph`: `ranksep` 50, `edgesep` 20, `nodesep` 50, `rankdir` `tb`, a size of 0,
  `minlen` 1, `weight` 1, `labeloffset` 10, `labelpos` `r`), runs `RunLayout`, writes the results back
  (`UpdateInputGraph`: every node center, the size of every node with children, every route, the label center of an
  edge that has a label node, and the drawing size), and returns the layout graph for checks.
- `RunLayout` - the stages in dagre's order: `MakeSpaceForEdgeLabels` (halves the rank separation, doubles every
  minimum length, and widens an edge with a side label by its offset), `RemoveSelfEdges`, `DagreAcyclic.Run`,
  `DagreNesting.Run`, `DagreRanker.Rank` on the flat copy, `InjectEdgeLabelProxies` (a proxy halfway along each
  labeled edge), `RemoveEmptyRanks`, `DagreNesting.Cleanup`, `NormalizeRanks`, `AssignRankMinMax`,
  `RemoveEdgeLabelProxies`, `DagreNormalizer.Run`, `DagreNesting.ParentDummyChains`, `DagreNesting.AddBorderSegments`,
  `DagreOrdering.Order`, `InsertSelfEdges` (a dummy right of the node), `DagreCoordinateSystem.Adjust`,
  `DagrePositioning.Position`, `PositionSelfEdges` (a five-point loop to the dummy and back, the label at the dummy),
  `RemoveBorderNodes` (a cluster sized and centered from its borders), `DagreNormalizer.Undo`, `FixupEdgeLabelCoords`,
  `DagreCoordinateSystem.Undo`, `TranslateGraph` (the top-left corner at the margins and the drawing size),
  `AssignNodeIntersects` (the ends of each route cut at the node borders), `ReversePointsForReversedEdges`, and
  `DagreAcyclic.Undo`.
- `AddDummyNode` and `AddBorderNode` - a new node named by the meter's `UniqueId`, skipping names in use.
- `Simplify`, `AsNonCompoundGraph`, `BuildLayerMatrix`, `NormalizeRanks`, `RemoveEmptyRanks`, `MaxRank` - dagre's
  `util.js`: parallel edges collapsed (summed weight, greatest minimum length), a flat copy sharing the labels, the
  node names by rank and order, ranks shifted to start at 0, the empty ranks that are not multiples of the nesting
  rank factor removed (walking every index of the sparse JavaScript array, only whole non-negative ranks counted), and
  the greatest rank.
- `IntersectRect` - where the line from a rectangle's center to a point leaves the rectangle.
- `Lodash.Sort` - V8's `Array.prototype.sort`: a natural run (a strictly descending one reversed) extended by binary
  insertion to the minimum run length (the whole list under 64 items), then the runs merged stably in V8's
  run-stack order. `Lodash.SortBy` - lodash's stable `_.sortBy` with `CompareAscending` (numbers ascending, then
  `undefined`, then NaN, including the inconsistent NaN-to-NaN case). `Lodash.Min`, `Max`, and `MinBy` - lodash's
  `baseExtremum`: `undefined` and NaN skipped, the first of equal values kept, `undefined` for none.

**Preconditions and postconditions**: the input graph is a `Graphlib` graph whose node labels may be null (a parent
created only by naming it); the edge labels are not null. After `Run` the input labels hold the results and the input
graph's structure is unchanged. The faithful quirks listed in _Dagre Subsystem Design_ that belong to `layout.js` are
kept here: the `_.max(a, b)` of `AssignRankMinMax`, the upper-case `TB`/`BT` test of `MakeSpaceForEdgeLabels`, the
case-sensitive `l`/`r` of `FixupEdgeLabelCoords`, the half-rank edge-label proxies that `RemoveEmptyRanks` neither
counts nor shifts, and the maximum x and y of `TranslateGraph` that start at 0.

#### Error Handling

`IntersectRect` throws `InvalidOperationException` for a point at the rectangle's center (or not a number), as dagre
throws, and `AssignNodeIntersects` throws it for a route without points. `Run` maps the exceptions of every stage:
`InsufficientExecutionStackException` (the recursive stages call `RuntimeHelpers.EnsureSufficientExecutionStack`)
becomes `meter.LimitExceeded()` (`mermaid-limit-exceeded`), an `InvalidOperationException` becomes
`meter.Failure(message)` (`mermaid-render-failed`), and a `MermaidRenderException` from the meter and an
`OperationCanceledException` pass through. The `Lodash` helpers never throw.

#### Dependencies

- **Graphlib subsystem** - `Graph` and `GraphEdge`
- **Dagre subsystem** - every other unit: `DagreAcyclic`, `DagreRanker`, `DagreNesting`, `DagreNormalizer`,
  `DagreOrdering`, `DagrePositioning`, `DagreCoordinateSystem`, and `LayoutWorkMeter`
- **Package root** - `MermaidRenderException` (through the meter)
- **.NET base class library** - `System.Math`, `System.Linq`, `System.Collections.Generic`

#### Callers

`ClusterLayout` of the `Rendering` subsystem calls `Run` for the graph of a diagram and of every extracted cluster, and
derives `ClusterLayoutNode` and `ClusterLayoutEdge` from `DagreNode` and `DagreEdge`; `EdgeBuilder` uses `Same`. Every
other unit of the subsystem uses the helpers (`AddDummyNode`, `AsNonCompoundGraph`, `BuildLayerMatrix`, `MaxRank`,
`Truthy`, `Same`) and `Lodash`. The tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreLayoutTests.cs`
and `LodashTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- V8 merges two long runs with galloping; `Lodash.Sort` merges them plainly and stably (taking from the second run
  only an item that is strictly less). For a consistent comparator every stable merge gives the same result; only an
  inconsistent comparator (dagre's barycenter comparator with a NaN barycenter) on 64 or more entries of one cluster
  could be ordered differently.
- JavaScript object keys that are properties of the object prototype (a node named `constructor` or `__proto__`)
  behave as ordinary names in the `Graphlib` graph that the stages iterate.
- The mapping of a short stack and of dagre's own errors to located `MermaidRenderException`s is an addition; in
  JavaScript they would be a `RangeError` or an uncaught `Error`.
