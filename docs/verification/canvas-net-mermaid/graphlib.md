## Graphlib Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Graphlib` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Graphlib`), containing two units: `Graph` (the graph with its edge identity
`GraphEdge` and the JavaScript key-order map `ObjectKeyMap`) and `GraphAlgorithms` (the graphlib traversals).

### Verification Approach

The `Graphlib` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`graphlib/`) verify each unit in isolation. The subsystem-level scenarios in `GraphlibTests` verify the units working
together: a compound multigraph is built and changed through `Graph`, and `GraphAlgorithms` traverses it, so the test
observes that the key order of the graph reaches the traversals and that a traversal sees the graph as it is after
removals and additions. The expected results are worked out by hand from graphlib's rules (JavaScript's own-key
order: array-index names first in ascending numeric order, then the others in insertion order). No mocking or
stubbing is used; the subsystem is internal and is reached through the test assembly's `InternalsVisibleTo` access.
The fidelity of the subsystem inside the complete layout is verified further by the dagre stage goldens of the
`Dagre` subsystem, which run on graphs with numeric node names.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own graph; no files, threads, or shared state

### Acceptance Criteria

The `Graphlib` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the two unit verification documents passes without error or unexpected exception. Any order of nodes, children, or
visited nodes other than the hand-derived JavaScript order constitutes a failure.

### Test Scenarios

#### CanvasNetMermaid-Graphlib-GraphStructure: Graphs Hold The Layout Model

**Tests**: `GraphlibTests.Graphlib_Traversal_NumericNames_FollowJavaScriptKeyOrder`,
`GraphlibTests.Graphlib_Mutation_RemoveAndAdd_TraversalsSeeCurrentGraph`

Builds a compound multigraph with named parallel edges and a node placed under a parent that did not exist, and a
path from which a node is removed and edges are added again, and asserts the nodes, the top-level children, the
children of the parent, the edges, and the nodes that the traversals follow.

#### CanvasNetMermaid-Graphlib-KeyOrder: Collections And Traversals Follow JavaScript's Key Order

**Tests**: `GraphlibTests.Graphlib_Traversal_NumericNames_FollowJavaScriptKeyOrder`

Asserts that a graph whose nodes `root`, `z`, `12`, `3`, `y`, and `cluster` are added in that order lists them as
`3`, `12`, `root`, `z`, `y`, `cluster`, that the top-level children follow the same order, and that a preorder over
all nodes visits `3`, `y`, `12`, `root`, `z`, `cluster`.

#### CanvasNetMermaid-Graphlib-Traversals: Traversals Give Graphlib's Results

**Tests**: `GraphlibTests.Graphlib_Traversal_NumericNames_FollowJavaScriptKeyOrder`,
`GraphlibTests.Graphlib_Mutation_RemoveAndAdd_TraversalsSeeCurrentGraph`

Asserts the hand-derived preorder of a compound multigraph, and the preorder and postorder of a path before and after
a node is removed and edges are added again.

#### CanvasNetMermaid-Graphlib-Consistency: Changes Are Seen By Later Queries

**Tests**: `GraphlibTests.Graphlib_Mutation_RemoveAndAdd_TraversalsSeeCurrentGraph`

Removes the middle node of the path `a`, `b`, `c` and asserts that a preorder from `a` visits only `a` and that the
edge from `a` to `b` is gone with it; then adds the edges `a`-`c` and `c`-`b` and asserts that the nodes are listed as
`a`, `c`, `b` (the node added again moves to the end) and that a postorder from `a` visits `b`, `c`, `a`.
