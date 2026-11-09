### Graph

![Graphlib Structure](MermaidGraphlibView.svg)

The `Graph` unit is the first software unit in the `Graphlib` subsystem. It consists of three source files in the
`DemaConsulting.CanvasNet.Mermaid.Graphlib` namespace: `Graph.cs` (the internal sealed generic class
`Graph<TGraph, TNode, TEdge>` with its private nested `EdgeEntry`), `GraphEdge.cs` (the internal sealed record
`GraphEdge`), and `ObjectKeyMap.cs` (the internal sealed generic class `ObjectKeyMap<TValue>` with its private nested
`Slot`), which are grouped into one unit because the graph stores every collection in the key-order map and
identifies every edge by the record.

#### Purpose

`Graph` is the graph that the layout builds and queries: a port of graphlib's `Graph` (`graphlib/graph.js` of
dagre-d3-es). It holds a label on the graph, on every node, and on every edge, and it can be directed or undirected,
a simple graph or a multigraph (several named edges between the same two nodes), and flat or compound (nodes nested
in parent nodes). Its single responsibility is to answer every query in exactly the way and in exactly the order
graphlib answers it, because dagre's choices depend on that order. `ObjectKeyMap` supplies the order: the own-key
order of a JavaScript object.

The unit's behavior is specified by the `CanvasNetMermaid-Graphlib-Graph-*` requirements in
`docs/reqstream/canvas-net-mermaid/graphlib/graph.yaml`.

#### Data Model

**`Graph<TGraph, TNode, TEdge>`** - the type parameters are the types of the graph label, the node labels, and the
edge labels. Fields and properties:

- `IsDirected`, `IsMultigraph`, `IsCompound` - fixed by the constructor (`true`, `false`, `false` by default).
- `GraphLabel` - the graph label (settable, also through `SetGraph`).
- `_nodes` - an `ObjectKeyMap<TNode?>` of the node labels; `NodeCount` is its count.
- `_parent` and `_children` - for a compound graph only: the parent of each node and an `ObjectKeyMap<bool>` of the
  children of each node, including the implicit root (the key `"\0"`), which holds the top-level nodes.
- `_in`, `_out` - per node, an `ObjectKeyMap<GraphEdge>` of the in-edges and out-edges by edge key.
- `_preds`, `_sucs` - per node, an `ObjectKeyMap<int>` counting the edges from each predecessor and to each
  successor, so parallel edges list a neighbor once and the neighbor disappears with its last edge.
- `_edges` - an `ObjectKeyMap<EdgeEntry>` of every edge with its (mutable) label by edge key; `EdgeCount` is its
  count. The edge key is the tail, the head (in ordinal order for an undirected graph), and the name (`"\0"` for an
  unnamed edge), separated by `"\u0001"`.
- `_defaultNodeLabel` (a function of the node name) and `_defaultEdgeLabel` (a function of the tail, the head, and
  the name) - both give `default` until set.

**`GraphEdge(V, W, Name)`** - the immutable identity of an edge: the tail, the head, and the name (null for an
unnamed edge). In an undirected graph `V` is the lesser end in ordinal order, as graphlib stores it. Record equality
makes two identities with the same parts equal.

**`ObjectKeyMap<TValue>`** - a `Dictionary<string, Slot>` (ordinal) of the entries, a `LinkedList<string>` of the
keys that are not array indexes in insertion order, and a `SortedSet<uint>` of the keys that are array indexes. An
array index is the canonical decimal form of an integer from 0 to 4294967294 (2<sup>32</sup> - 2): no sign, no
leading zero except `"0"` itself, no exponent, at most ten digits.

**Invariants**: every node has entries in `_in`, `_out`, `_preds`, and `_sucs` (and in `_parent` and `_children` for
a compound graph); every edge in `_edges` is in the `_out` of its tail and the `_in` of its head and counted once in
`_sucs` and `_preds`; the parent chain of a node never returns to the node.

#### Key Methods

- **`SetNode(v)` / `SetNode(v, value)`** - adds an absent node (with the default label or the given one) at its place
  in the key order and at the top level of a compound graph; `SetNode(v)` keeps the label of an existing node,
  `SetNode(v, value)` replaces it. `SetNodes(vs)` calls `SetNode(v)` for each name. `Node(v)` gives the label or the
  default for an absent node; `HasNode(v)` tells whether it is present.
- **`RemoveNode(v)`** - removes the node and its label, moves its children to the top level, then removes every
  in-edge and every out-edge (a self loop is gone after the first pass). An absent node is ignored. Because the node
  key is deleted, a node that is added again is listed after the others.
- **`SetParent(v, parent)`** - for a compound graph: walks the ancestors of the parent to refuse a cycle, adds the
  parent and the node when absent, detaches the node from its old parent, and attaches it to the new one (or to the
  top level for null). `Parent(v)` gives null at the top level, for an absent node, or in a flat graph;
  `Children(v)` gives the children (the top-level nodes for null), an empty list for a node of a flat graph, and null
  for an absent node.
- **`SetEdge(...)`** - the four overloads (ends and optional label and name, or a `GraphEdge` and an optional label)
  share `SetEdgeCore`: an existing edge keeps its label unless one is given; a new named edge is refused in a simple
  graph; the ends are added first; the label is the given one or the default edge label of the tail, head, and name;
  the edge is recorded in `_edges`, `_in`, `_out`, `_preds`, and `_sucs`. `SetPath(vs)` adds an edge between each pair
  of consecutive names. `Edge`, `HasEdge`, and `RemoveEdge` find an edge by the same key, so an undirected edge is
  found from either end.
- **`Nodes`, `Edges`, `Sources`, `Sinks`** - snapshots in key order (edges in insertion order); `Sources` and `Sinks`
  are the nodes without in-edges and without out-edges.
- **`Predecessors(v)`, `Successors(v)`, `Neighbors(v)`, `IsLeaf(v)`** - the keys of `_preds` and `_sucs`;
  `Neighbors` is the predecessors followed by the successors that are not predecessors (lodash's `_.union`);
  `IsLeaf` is "no successors" for a directed graph and "no neighbors" otherwise. The lists are null for an absent
  node.
- **`InEdges(v, u)`, `OutEdges(v, w)`, `NodeEdges(v, w)`** - the edges into, out of, or both into and then out of a
  node, optionally only those whose other end is a given node; null for an absent node.
- **`FilterNodes(filter)`** - a new graph of the same kind and graph label with the nodes that pass, the edges whose
  two ends passed, and, in a compound graph, each node under its nearest ancestor that passed (graphlib's memoized
  `findParent`); the labels are shared, not copied.
- **`ObjectKeyMap` indexer** - setting a new key appends it to the named keys or inserts it among the indexes;
  setting an existing key changes the value and keeps its place. `Remove` deletes the key (as the JavaScript `delete`
  operator), so a key that is added again goes to its new place. `Keys()` and `Values()` return snapshots (array
  indexes first, ascending, then the named keys in insertion order), so the map may change while a snapshot is
  iterated, as lodash's `_.keys` allows. `IsArrayIndex(key, out index)` applies the array-index rule.

**Preconditions and postconditions**: node names and edge names are any strings (compared character by character);
every mutating method returns the graph itself so that calls can be chained, as in graphlib. `IsLeaf` expects a
present node.

#### Error Handling

- `SetEdge` throws `InvalidOperationException` for a new named edge in a graph that is not a multigraph.
- `SetParent` throws `InvalidOperationException` in a graph that is not compound and when the parent is the node
  itself or one of its descendants (graphlib's "would create a cycle"); the graph is unchanged.
- The `ObjectKeyMap` getter throws `KeyNotFoundException` for an absent key; the graph reads it only for keys it
  knows to be present.
- Queries about an absent node do not throw: they answer null (or the default label), where graphlib answers
  `undefined`. Removing an absent node or edge does nothing.

The callers decide how a failure is reported; the `ClusterLayout` unit of the `Rendering` subsystem reports a
subgraph cycle as a located `mermaid-render-failed`. The unit performs no I/O.

#### Dependencies

- **.NET base class library** - `Dictionary`, `LinkedList`, `SortedSet`, LINQ, and invariant-culture number
  formatting and parsing

The unit has no dependency on another unit of the system or on the core `CanvasNet` library.

#### Callers

The `Dagre` subsystem (`DagreLayout`, `DagreAcyclic`, `DagreRanker`, `DagreOrdering`, `DagrePositioning`, and the
other stages) builds, transforms, and queries its layout graphs, and its ordering stage keeps the entries of its
conflict resolution in an `ObjectKeyMap`; the `ClusterLayout` unit of the `Rendering` subsystem builds the compound
multigraph of a diagram and its subgraphs; the `FlowchartModel` unit of the `Flowchart` subsystem keeps the
properties of an element in an `ObjectKeyMap`; the `GraphAlgorithms` unit traverses the graph. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Graphlib/GraphTests.cs` and `ObjectKeyMapTests.cs` call the unit
directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- **Prototype keys are ordinary names.** In JavaScript, a node named `constructor` or `__proto__` interacts with the
  prototype of graphlib's objects; the port treats every name as an ordinary key.
- **`undefined` is null.** Results that graphlib returns as `undefined` (the lists of an absent node, the parent at
  the top level) are null; a missing label is the default of the label type.
- **Not ported.** graphlib's `json` reader and writer are not ported; the pinned Mermaid release does not use them.
