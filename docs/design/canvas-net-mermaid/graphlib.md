## Graphlib

![Graphlib Structure](MermaidGraphlibView.svg)

### Overview

The `Graphlib` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Graphlib`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Graphlib/`) is the graph data structure of the layout: a port of graphlib as
dagre-d3-es bundles it, the version of dagre that the pinned Mermaid release lays its diagrams out with. It is
**internal**: its types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to
the test assembly. It contains two units:

- **Graph** - the generic graph `Graph<TGraph, TNode, TEdge>` (`Graph.cs`), the edge identity `GraphEdge`
  (`GraphEdge.cs`), and the string-keyed map that keeps the key order of a JavaScript object, `ObjectKeyMap<TValue>`
  (`ObjectKeyMap.cs`): directed or undirected, simple or multi, flat or compound graphs with labels on the graph, the
  nodes, and the edges; see _Graph Unit Design_ (`graphlib/graph.md`)
- **GraphAlgorithms** - the static `GraphAlgorithms` (`GraphAlgorithms.cs`): the graphlib traversals `preorder`,
  `postorder`, `components`, `tarjan`, and `findCycles`; see _GraphAlgorithms Unit Design_
  (`graphlib/graph-algorithms.md`)

The subsystem's namespace documentation is in `Graphlib/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Graphlib-*` requirements in `docs/reqstream/canvas-net-mermaid/graphlib.yaml`. The SysML2 model
names the subsystem `MermaidGraphlib` (see _CanvasNetMermaid System Design_).

### Interfaces

**Exposed** (internal only): `Graph<TGraph, TNode, TEdge>` with graphlib's surface - the constructor options
`directed`, `multigraph`, and `compound`; `GraphLabel` and `SetGraph`; `SetDefaultNodeLabel` and
`SetDefaultEdgeLabel`; `Nodes`, `Sources`, `Sinks`, `SetNode`, `SetNodes`, `Node`, `HasNode`, `RemoveNode`, and
`NodeCount`; `SetParent`, `Parent`, and `Children`; `Predecessors`, `Successors`, `Neighbors`, and `IsLeaf`;
`FilterNodes`; `Edges`, `SetEdge`, `SetPath`, `Edge`, `HasEdge`, `RemoveEdge`, `InEdges`, `OutEdges`, `NodeEdges`,
and `EdgeCount`; the record `GraphEdge(V, W, Name)`; `ObjectKeyMap<TValue>` (indexer, `ContainsKey`,
`TryGetValue`, `Remove`, `Keys`, `Values`, `Count`, and `IsArrayIndex`); and `GraphAlgorithms.Preorder`,
`Postorder`, `Components`, `Tarjan`, and `FindCycles`.

**Consumed**: the .NET base class library only (collections, `RuntimeHelpers.EnsureSufficientExecutionStack`, and
invariant-culture number formatting). The subsystem uses no other subsystem of the system and no part of the core
`CanvasNet` library.

**Consumers**: the `Dagre` subsystem builds and transforms its layout graphs with `Graph` and walks the spanning tree
of its ranker with `GraphAlgorithms.Preorder` and `Postorder`, and keeps the conflict entries of its ordering stage
in an `ObjectKeyMap`; the `ClusterLayout` unit of the `Rendering` subsystem builds the compound multigraph of a
diagram (Mermaid's dagre wrapper) with `Graph`; and the `FlowchartModel` unit of the `Flowchart` subsystem keeps
the properties of an element in an `ObjectKeyMap`, in the order Mermaid's database keeps them.

### Design

**The order of a JavaScript object is part of the contract.** graphlib keeps the nodes, the children, the
predecessors, the successors, and the edges of a graph in JavaScript objects, and dagre iterates them and breaks
ties by the order it finds. ECMAScript lists the own string keys of an ordinary object with the array indexes first,
in ascending numeric order, then every other key in the order it was added; a key that is deleted and added again
moves to the end. `ObjectKeyMap` reproduces this rule (it is written from the ECMAScript specification, not ported),
and `Graph` stores every collection that graphlib stores in an object in an `ObjectKeyMap`. As a result, a diagram
whose node names are `1`, `2`, and `10` is laid out with the same choices as in Mermaid, and the same diagram always
gives the same layout.

**A faithful port of graphlib's graph.** `Graph` ports `graphlib/graph.js` of dagre-d3-es: the edge key built from
the two ends (ordered for an undirected graph) and the name, the default labels as functions of the node name or of
the tail, head, and name, the implicit root of a compound graph, the predecessor and successor counts that make
parallel edges count once, the removal of a node with its edges and the re-parenting of its children, and
`filterNodes` with its nearest-kept-ancestor search. Methods that graphlib answers with `undefined` for an absent
node answer null. `GraphAlgorithms` ports the traversals of `graphlib/alg/` (`dfs.js`, `preorder.js`,
`postorder.js`, `components.js`, `tarjan.js`, and `find-cycles.js`) on top of the same key order, so its results are
those graphlib gives. Of these, the dagre ranker uses `Preorder` and `Postorder`; the others complete the ported
surface.

**Work and depth are bounded by the callers.** The traversals recurse as graphlib does; the depth is bounded by the
number of nodes, which the diagram limits keep small, and every recursive step calls
`RuntimeHelpers.EnsureSufficientExecutionStack`, so a stack that runs short raises
`InsufficientExecutionStackException` (which the `Dagre` subsystem reports as `mermaid-limit-exceeded`) instead of
ending the process. The graph does not meter its own work; the dagre stages charge their work to the layout work
meter of the `Dagre` subsystem.

**Not ported.** graphlib's `json` reader and writer and the algorithms that neither dagre nor Mermaid's dagre wrapper
reach (shortest paths, minimum spanning tree, topological sort, and the cycle check) are not ported; the pinned
Mermaid release refers to `json.write` only in logging that is commented out.

**Attribution.** `Graph.cs`, `GraphEdge.cs`, and `GraphAlgorithms.cs` contain portions derived from dagre-d3-es
(MIT; the license text ships as `LICENSE-dagre.txt`), as recorded in `third-party/README.md` of the package.
`ObjectKeyMap.cs` is original work that follows the ECMAScript specification.

**Threading.** A graph is mutable and is used by one call on one thread; `GraphAlgorithms` is stateless.
