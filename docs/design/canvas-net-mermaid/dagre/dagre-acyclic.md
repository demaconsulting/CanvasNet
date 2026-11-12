### DagreAcyclic

![Dagre Structure](MermaidDagreView.svg)

The `DagreAcyclic` unit is the second software unit in the `Dagre` subsystem. It is one source file,
`DagreAcyclic.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Dagre` namespace, and defines the internal static class
`DagreAcyclic` with two private nested types: `FasEntry` (the state of one node of the greedy heuristic) and `FasList`
(dagre's circular bucket list with a sentinel). It ports dagre's `acyclic.js`, `greedy-fas.js`, and `data/list.js`.

#### Purpose

Ranking needs an acyclic graph. `DagreAcyclic` reverses a feedback arc set - a set of edges whose reversal leaves no
cycle - before ranking, and restores the reversed edges after the layout, so that the caller sees every edge in its
own direction with a route that runs from its tail to its head.

The unit's behavior is specified by the `CanvasNetMermaid-Dagre-DagreAcyclic-*` requirements in
`docs/reqstream/canvas-net-mermaid/dagre/dagre-acyclic.yaml`.

#### Data Model

A reversed edge is removed and added again from head to tail, with a new name from the meter's `UniqueId("rev")`; its
`LayoutEdge` keeps the original name in `ForwardName` and is marked `Reversed`. The greedy heuristic works on a
private weighted simple graph whose nodes are `FasEntry` objects (remaining in- and out-weight, and the links of the
bucket list they are in) and whose edges carry the summed weight of parallel edges; the buckets are indexed by
out-weight minus in-weight, with the sinks in the first bucket and the sources in the last.

#### Key Methods

- `Run(LayoutGraph g, LayoutWorkMeter meter)` - picks the set with `GreedyFas` when the graph's `acyclicer` is
  `greedy`, else with `DfsFas`, and reverses every edge of it.
- `Undo(LayoutGraph g)` - removes every reversed edge and adds it again in its original direction, with its original
  name and its label.
- `DfsFas(LayoutGraph g, LayoutWorkMeter meter)` - a depth-first search over the out-edges from every node in graph
  order, collecting each edge that reaches a node on the stack.
- `GreedyFas(LayoutGraph g, Func<GraphEdge, double> weightOf, LayoutWorkMeter meter)` - dagre's `greedyFAS`, the
  heuristic of Eades, Lin, and Smyth: remove sinks and sources while there are any, else the node with the greatest
  out-minus-in weight, collecting the in-edges of the latter; every parallel edge of a collected pair is returned.

**Preconditions and postconditions**: `Run` is called after the self edges have been removed. After `Run` the graph has
no cycle; after `Undo` every edge has its original ends, name, and label.

#### Error Handling

The unit throws no exception of its own. The depth-first search calls `RuntimeHelpers.EnsureSufficientExecutionStack`
before each level, so a very deep graph fails with an `InsufficientExecutionStackException` that `DagreLayout.Run`
reports as `mermaid-limit-exceeded`. `Run` charges the meter (the nodes and edges for the depth-first search, twice
that for the greedy heuristic), which can throw the meter's limit and cancellation exceptions.

#### Dependencies

- **Graphlib subsystem** - `Graph` and `GraphEdge`
- **Dagre subsystem** - `DagreLayout` (`Truthy`) and the `LayoutModel.cs` labels; `LayoutWorkMeter`
- **.NET base class library** - `System.Runtime.CompilerServices.RuntimeHelpers`, `System.Linq`

#### Callers

`DagreLayout.RunLayout` calls `Run` before the nesting graph and `Undo` as the last stage. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreAcyclicTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

Negative and fractional edge weights are not supported: dagre's greedy heuristic indexes its bucket array with the
out-minus-in weight, which JavaScript tolerates for a non-integer (an object property) but which is not an array index
here. Mermaid gives dagre whole positive weights only, so a flowchart never reaches this case.
