### GraphAlgorithms

![Graphlib Structure](MermaidGraphlibView.svg)

The `GraphAlgorithms` unit is the second software unit in the `Graphlib` subsystem. It is one source file,
`GraphAlgorithms.cs`, in the `DemaConsulting.CanvasNet.Mermaid.Graphlib` namespace, and defines the internal static
class `GraphAlgorithms` with its private nested `TarjanEntry` (the search state of one node of Tarjan's algorithm).

#### Purpose

`GraphAlgorithms` holds the graph traversals of graphlib (`graphlib/alg/` of dagre-d3-es) that the ported graphlib
surface offers: depth-first preorder and postorder, connected components, strongly connected components, and cycles.
Its single responsibility is to give the results graphlib gives, in graphlib's order, for any `Graph` of the `Graph`
unit. The dagre ranker walks its spanning tree with `Preorder` and `Postorder`, so their visiting order decides the
ranks of a diagram.

The unit's behavior is specified by the `CanvasNetMermaid-Graphlib-GraphAlgorithms-*` requirements in
`docs/reqstream/canvas-net-mermaid/graphlib/graph-algorithms.yaml`.

#### Data Model

The unit has no state between calls. Each call keeps its own visited set (an ordinal `HashSet<string>`), its result
lists, and, for `Tarjan`, a `Stack<string>`, a running index, and a `TarjanEntry` per node with its visit `Index`, its
`LowLink` (the lowest index reachable), and whether it is `OnStack`. The methods are generic in the label types of the
graph and never read or change a label.

#### Key Methods

- **`Preorder(g, vs)` / `Postorder(g, vs)`** - graphlib's `alg.preorder` and `alg.postorder` through one depth-first
  search (`alg.dfs`): the roots are visited in the given order; from each node the search follows the successors of a
  directed graph, or the neighbors of an undirected graph, in the graph's key order; each node is recorded once,
  before its descendants (preorder) or after them (postorder). The result is a `List<string>`.
- **`Components(g)`** - graphlib's `alg.components`: for each node in key order that is not yet visited, a component
  of every node reachable through successors first and then predecessors (edge direction ignored), in visit order.
- **`Tarjan(g)`** - graphlib's `alg.tarjan`: the strongly connected components in completion order, each in the order
  its nodes are popped from the stack.
- **`FindCycles(g)`** - graphlib's `alg.findCycles`: the components of `Tarjan` with more than one node, or with one
  node that has an edge to itself.

**Preconditions and postconditions**: the graph is not changed during a call. The result reflects the graph at the
time of the call, including every earlier removal and addition.

#### Error Handling

- `Preorder` and `Postorder` throw `ArgumentException` ("Graph does not have node") for a root that is not a node of
  the graph, as graphlib throws.
- The traversals recurse as graphlib does; the depth is bounded by the number of nodes, which the diagram limits keep
  small. Every recursive step calls `RuntimeHelpers.EnsureSufficientExecutionStack`, so a stack that runs short
  raises `InsufficientExecutionStackException` instead of ending the process; the `DagreLayout` unit reports it as a
  located `mermaid-limit-exceeded`.

The unit performs no I/O.

#### Dependencies

- **Graph** - `Graph<TGraph, TNode, TEdge>` (`Nodes`, `HasNode`, `Successors`, `Predecessors`, `Neighbors`, `HasEdge`,
  `IsDirected`) of the same subsystem
- **.NET base class library** - collections, LINQ, and `System.Runtime.CompilerServices.RuntimeHelpers`

#### Callers

The network-simplex ranker of the `DagreRanker` unit (`Dagre` subsystem) calls `Postorder` to compute the cut values
of the tree edges, children before parents, and `Preorder` to update the ranks from the root. `Components`, `Tarjan`,
and `FindCycles` have no caller in the package outside the
tests; they complete the ported traversal surface. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Graphlib/GraphAlgorithmsTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- **Stack exhaustion is an exception.** Where graphlib would overflow the JavaScript stack on a very deep graph, the
  port raises `InsufficientExecutionStackException` before the process stack runs out.
- **Not ported.** graphlib's shortest-path (Dijkstra's algorithm, all pairs), minimum-spanning-tree (Prim's
  algorithm), topological-sort, and cycle-check algorithms are not ported; neither dagre nor Mermaid's dagre
  wrapper reaches them.
